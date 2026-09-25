#!/usr/bin/env python3
"""Assemble a whole level in Blender, exactly like the game does it, and render screenshots.

The house comes from the game's own layout generator (tools/LevelDump dumps it as JSON) and is placed
with the same transforms as Assets/Scripts/Game/World/HouseBuilder.cs; the village mirrors
VillageBuilder.cs. Useful to eyeball layouts without opening Unity, and for docs/ screenshots.

Usage:
  dotnet run --project tools/LevelDump -c Release -- 12345 /tmp/layout.json
  python3 blender/preview_level.py house /tmp/layout.json docs/shots/house.png
  python3 blender/preview_level.py village docs/shots/village.png
Options: --res=WxH  --samples=N  --view=top|cutaway|porch|living|kitchen
"""
import json
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'models'))

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from gnomelib import core  # noqa: E402
from gnomelib.core import trs_u, u2b_matrix  # noqa: E402
from gnomelib.render import _aim, _light  # noqa: E402

HS = core.HS

FLOOR_COLORS = {
    'Wood': 0xb07a48, 'DarkWood': 0x6e4a30, 'Tile': 0xe8e2d4, 'Checker': 0xdcd8d0,
    'Carpet': 0x7d5a8c, 'Grass': 0x6fa04a,
}
WALL_COLORS = {
    'StripesBlue': 0x9cb8d8, 'MintTile': 0xa8dcc8, 'Yellow': 0xf0d27a, 'Damask': 0xb8646a,
    'Floral': 0xe8c8b0, 'Panel': 0x8a6448, 'Plaster': 0xefe6d6,
}


def opt(name, default):
    for a in sys.argv:
        if a.startswith(f'--{name}='):
            return a.split('=', 1)[1]
    return default


# ---------------------------------------------------------------- model instancing

_registry = None
_protos = {}


def registry():
    global _registry
    if _registry is None:
        import build
        _registry = build.all_models()
    return _registry


def proto(name):
    """Build a model once into a hidden collection; instances reuse it."""
    if name in _protos:
        return _protos[name]
    fn = registry().get(name)
    if fn is None:
        print('  (no model)', name)
        _protos[name] = None
        return None
    coll = bpy.data.collections.new('proto_' + name)
    root = fn()
    root.realize(coll)
    _protos[name] = (coll, root)
    return _protos[name]


def place(name, pos, rot_y=0.0, scale=(1, 1, 1), tint=None):
    """Instance a model at a Unity-space position / yaw (radians), like ModelLibrary.Instantiate."""
    p = proto(name)
    if p is None:
        return None
    coll, _ = p
    inst = bpy.data.objects.new(f'I_{name}', None)
    inst.instance_type = 'COLLECTION'
    inst.instance_collection = coll
    inst.matrix_world = u2b_matrix(trs_u(pos, (0, rot_y, 0), scale))
    bpy.context.scene.collection.objects.link(inst)
    return inst


def marker_lights(inst, name, energy_scale=1.0):
    """Point lights for a model's LIGHT_ markers (like HouseBuilder.AddMarkerLight)."""
    p = proto(name)
    if p is None or inst is None:
        return
    coll, _ = p
    bpy.context.view_layer.update()
    for o in coll.objects:
        if not o.name.startswith('LIGHT_'):
            continue
        c = int(o.get('color', 'ffd9a0'), 16)
        rgb = (((c >> 16) & 255) / 255, ((c >> 8) & 255) / 255, (c & 255) / 255)
        rng = float(o.get('range', 12))
        inten = float(o.get('intensity', 1.0))
        ld = bpy.data.lights.new('L_' + o.name, 'POINT')
        ld.color = rgb
        ld.energy = inten * 90 * (rng / 12) ** 2 * energy_scale
        ld.shadow_soft_size = 0.3
        lo = bpy.data.objects.new('L_' + o.name, ld)
        bpy.context.scene.collection.objects.link(lo)
        lo.matrix_world = inst.matrix_world @ o.matrix_world


def markers(name, prefix):
    """World-space (Unity) matrices of a model's markers, relative to the model root."""
    p = proto(name)
    if p is None:
        return {}
    coll, _ = p
    bpy.context.view_layer.update()
    out = {}
    for o in coll.objects:
        if o.name.split('.')[0].startswith(prefix + '_'):
            out[o.name.split('.')[0]] = core.b2u_matrix(o.matrix_world)
    return out


def box(name, center_u, size_u, color, rot_y=0.0):
    """Unit cube scaled/rotated in Unity space (the game's BoxObj)."""
    import bmesh
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(core.material('C', color))
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.matrix_world = u2b_matrix(trs_u(center_u, (0, rot_y, 0), size_u))
    return o


def look_rotation_u(forward, up):
    """Unity's Quaternion.LookRotation as a 4x4 matrix (Unity space)."""
    f = Vector(forward).normalized()
    r = Vector(up).cross(f).normalized()
    u = f.cross(r)
    m = Matrix.Identity(4)
    for i in range(3):
        m[i][0], m[i][1], m[i][2] = r[i], u[i], f[i]
    return m


# ---------------------------------------------------------------- house

def build_house(L, rng, awake=None):
    H = L['wallHeight']

    for r in L['rooms']:
        cx, cz = (r['x0'] + r['x1']) / 2, (r['z0'] + r['z1']) / 2
        box('Floor_' + r['id'], (cx, -0.25, cz), (r['x1'] - r['x0'], 0.5, r['z1'] - r['z0']), FLOOR_COLORS[r['floor']])

    def room_at(x, z):
        for r in L['rooms']:
            if r['x0'] <= x <= r['x1'] and r['z0'] <= z <= r['z1']:
                return r
        return None

    for w in L['walls']:
        ax, az, bx, bz = w['ax'], w['az'], w['bx'], w['bz']
        ln = math.hypot(bx - ax, bz - az)
        d = ((bx - ax) / ln, (bz - az) / ln)
        n = (-d[1], d[0])
        yaw = math.atan2(n[0], n[1])  # LookRotation(normal)
        for (t0, t1, y0, y1) in w['solids']:
            mid = (t0 + t1) / 2
            c = (ax + d[0] * mid, (y0 + y1) / 2, az + d[1] * mid)
            # two half-thickness slabs so each side gets its room's wallpaper (outside = plaster)
            for sgn in (1, -1):
                probe = room_at(c[0] + sgn * n[0] * (w['thick'] / 2 + 1), c[2] + sgn * n[1] * (w['thick'] / 2 + 1))
                col = WALL_COLORS[probe['wall']] if probe else WALL_COLORS['Plaster']
                off = sgn * w['thick'] / 4
                box(f"Wall_{w['id']}_{mid:.0f}", (c[0] + n[0] * off, c[1], c[2] + n[1] * off), (t1 - t0, y1 - y0, w['thick'] / 2), col, yaw)
        for o in w['openings']:
            if o['kind'] == 'FakeDoor':
                mid = (o['t0'] + o['t1']) / 2
                c = (ax + d[0] * mid, o['y1'] / 2, az + d[1] * mid)
                box('BackDoor', c, (o['t1'] - o['t0'], o['y1'], w['thick'] * 0.6), 0x784628, yaw)

    for f in L['furniture']:
        sc = (f['prms'].get('w', 1), f['prms'].get('h', 1), 1) if f['model'] == 'window' else (1, 1, 1)
        place(f['model'], f['pos'], f['rotY'], sc)

    # garden, porch, yarn basket, sardine cart (HouseBuilder.Build)
    g0, g1 = L['worldMin'], L['worldMax']
    box('Garden', ((g0[0] + g1[0]) / 2, -0.52, (g0[2] + g1[2]) / 2), (g1[0] - g0[0] + 40, 1, g1[2] - g0[2] + 40), FLOOR_COLORS['Grass'])
    for dcr in L['garden']:
        s = dcr['scale']
        marker_lights(place(dcr['model'], dcr['pos'], dcr['rotY'], (s, s, s)), dcr['model'], 0.3)
    place('porch', L['porch'])
    place('yarnBasket', L['yarnBasket'], math.pi)
    place('stashBasket', (L['stash'][0], 0, L['stash'][2]), math.radians(170))

    # characters: grandpa asleep, the cat on its cushion, the gang at the porch
    bed = next((f for f in L['furniture'] if f['id'] == 'bed'), None)
    if awake:
        place('oldMan', awake[0], awake[1])
    elif bed:
        # OldMan.EnterSleep: anchor + fwd * 3.3 + up * 0.35, LookRotation(up, -fwd)
        fwd = Vector((math.sin(bed['rotY']), 0, math.cos(bed['rotY'])))
        bed_m = trs_u(bed['pos'], (0, bed['rotY'], 0))
        a = markers('bed', 'ANCHOR').get('ANCHOR_sleep')
        anchor = (bed_m @ a).to_translation() if a is not None else Vector(bed['pos']) + Vector((0, 2.4, 0))
        pos = anchor + fwd * 3.3 + Vector((0, 0.35, 0))
        inst = place('oldMan', tuple(pos))
        if inst:
            inst.matrix_world = u2b_matrix(Matrix.Translation(pos) @ look_rotation_u((0, 1, 0), tuple(-fwd)))
    cat_bed = next((f for f in L['furniture'] if f['model'] == 'catBed'), None)
    if cat_bed:
        place('cat', cat_bed['pos'], cat_bed['rotY'] + 0.6)
    for i, s in enumerate(L['spawns'][:4]):
        place('gnome', (s[0], 0.0, s[2]), math.pi + (i - 1.5) * 0.25)

    # loot: on furniture surfaces (SURF markers) or on the floor
    furn_by_id = {f['id']: f for f in L['furniture']}
    for it in L['items']:
        pos = None
        if it['furniture']:
            f = furn_by_id.get(it['furniture'])
            if not f:
                continue
            surfs = list(markers(f['model'], 'SURF').values())
            if not surfs:
                continue
            k = it['surface'] if it['surface'] >= 0 else rng.randrange(len(surfs))
            sm = surfs[min(k, len(surfs) - 1)]
            local = sm @ Vector((rng.uniform(-0.4, 0.4), 0, rng.uniform(-0.4, 0.4), 1))
            wm = trs_u(f['pos'], (0, f['rotY'], 0)) @ local
            pos = (wm.x, wm.y + 0.05, wm.z)
        else:
            r = next((r for r in L['rooms'] if r['id'] == it['room']), None)
            if r is None:
                continue
            if it['floorPos']:
                pos = tuple(it['floorPos'])
            else:
                pos = (rng.uniform(r['x0'] + 1.5, r['x1'] - 1.5), 0.02, rng.uniform(r['z0'] + 1.5, r['z1'] - 1.5))
        place(it['kind'], pos, rng.uniform(0, math.tau))
    return H


# ---------------------------------------------------------------- village

def build_village():
    marker_lights(place('village', (0, 0, 0)), 'village')
    marker_lights(place('greatSock', (0, 0.3, -1.55 * HS)), 'greatSock')
    place('knittingCorner', (-2.2 * HS, 0, -0.9 * HS), math.radians(35))
    marker_lights(place('sockTunnel', (0, 0, 2.05 * HS), math.pi), 'sockTunnel')
    spawn = markers('village', 'SPAWN').get('SPAWN_center')
    c = (spawn[0][3], spawn[1][3], spawn[2][3]) if spawn else (0, 0.2, 1.2 * HS)
    for i in range(4):
        a = i / 6 * math.tau
        place('gnome', (c[0] + math.cos(a) * 1.6, c[1], c[2] + math.sin(a) * 1.2), math.pi + a * 0.3)


# ---------------------------------------------------------------- render

def setup(res, samples, sky=(0.62, 0.72, 0.85), strength=0.9):
    s = bpy.context.scene
    s.render.engine = 'CYCLES'
    s.cycles.device = 'CPU'
    s.cycles.samples = samples
    s.cycles.use_denoising = True
    s.render.resolution_x, s.render.resolution_y = res
    s.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('World')
    s.world = w
    w.use_nodes = True
    bg = w.node_tree.nodes['Background']
    bg.inputs[0].default_value = (*[c ** 2.2 for c in sky], 1)
    bg.inputs[1].default_value = strength


def u2b(p):
    return Vector((-p[0], -p[2], p[1]))


def camera(eye_u, target_u, lens=24, ortho=None):
    cd = bpy.data.cameras.new('cam')
    cd.lens = lens
    cd.clip_end = 2000
    if ortho:
        cd.type = 'ORTHO'
        cd.ortho_scale = ortho
    cam = bpy.data.objects.new('cam', cd)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = u2b(eye_u)
    _aim(cam, u2b(target_u))
    bpy.context.scene.camera = cam


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    kind = args[0]
    res = tuple(int(v) for v in opt('res', '1600x1000').split('x'))
    samples = int(opt('samples', '48'))
    view = opt('view', 'cutaway')
    core.reset_scene()
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    rng = random.Random(7)
    setup(res, samples)
    if kind == 'house':
        L = json.load(open(args[1]))
        out = args[2]
        night = view == 'night'
        build_house(L, rng, awake=((16.8, 0, 26.6), 1.25) if night else None)
        sun = _light('sun', 'SUN', (0, 0, 50), 3.2, color=(1.0, 0.95, 0.85))
        sun.rotation_euler = (math.radians(38), math.radians(12), math.radians(-35))
        views = {
            'top': ((28, 150, 21), (28, 0, 21.01), 50, 80),
            'cutaway': ((84, 66, 86), (28, 0, 22), 26, None),
            'north': ((70, 62, -22), (28, 0, 22), 26, None),
            'porch': ((30, 5, 64), (24, 2, 40), 22, None),
            'living': ((4, 8, 38), (22, 1.5, 28), 18, None),
            'kitchen': ((50, 7.5, 16.5), (40, 2, 4), 18, None),
        }
        views['night'] = ((34.2, 1.25, 28.4), (17, 3.3, 27.4), 17, None)
        eye, tgt, lens, ortho = views[view]
        if night:
            # grandpa's night round: ceilings on, moonlight, fireplace embers and his torch
            setup(res, samples, sky=(0.05, 0.06, 0.12), strength=0.3)
            H = L['wallHeight']
            for r in L['rooms']:
                box('Ceiling_' + r['id'], ((r['x0'] + r['x1']) / 2, H + 0.15, (r['z0'] + r['z1']) / 2), (r['x1'] - r['x0'], 0.3, r['z1'] - r['z0']), 0xf2eee6)
            sun.data.energy = 0.25
            sun.data.color = (0.55, 0.65, 1.0)
            sun.rotation_euler = (math.radians(62), 0, math.radians(160))
            fire = _light('fire', 'POINT', u2b((10.8, 1.6, 27.0)), 380, color=(1.0, 0.55, 0.2))
            fire.data.shadow_soft_size = 0.8
            amb = _light('amb', 'AREA', u2b((24, H - 0.4, 32)), 520, 14, color=(0.4, 0.48, 0.95))
            torch = _light('torch', 'SPOT', u2b((18.2, 4.4, 27.4)), 9000, color=(1.0, 0.92, 0.72))
            torch.data.spot_size = math.radians(46)
            torch.data.spot_blend = 0.35
            torch.data.shadow_soft_size = 0.15
            _aim(torch, u2b((27.0, 0, 28.6)))
            for gp, gy in [((30.2, 0, 29.4), 1.9), ((28.9, 0, 27.6), 1.2), ((31.3, 0, 27.1), 2.3)]:
                place('gnome', gp, gy)
            place('remote', (29.3, 0.62, 27.9), 0.3)
            place('slipper', (27.4, 0.05, 29.6), 1.1)
        if view in ('living', 'kitchen'):
            # indoors: soft ceiling light instead of the sun through the (missing) ceiling
            sun.data.energy = 1.0
            for r in L['rooms']:
                cx, cz = (r['x0'] + r['x1']) / 2, (r['z0'] + r['z1']) / 2
                area = _light('room_' + r['id'], 'AREA', u2b((cx, L['wallHeight'] - 0.3, cz)), 2600, 8)
                area.rotation_euler = (0, 0, 0)
        camera(eye, tgt, lens, ortho)
    else:
        out = args[1]
        build_village()
        setup(res, samples, sky=(0.3, 0.26, 0.24), strength=0.6)
        sun = _light('sun', 'SUN', (0, 0, 50), 2.0, color=(1.0, 0.85, 0.65))
        sun.rotation_euler = (math.radians(50), 0, math.radians(20))
        views = {
            'cutaway': ((7.5, 2.3, 6.5), (-2, 0.9, -4), 16),
            'sock': ((3.5, 1.6, 2.5), (0, 1.6, -6), 20),
        }
        eye, tgt, lens = views.get(view, views['cutaway'])
        camera(eye, tgt, lens)
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    bpy.context.scene.render.filepath = os.path.abspath(out)
    bpy.ops.render.render(write_still=True)
    print('wrote', out)


if __name__ == '__main__':
    main()
