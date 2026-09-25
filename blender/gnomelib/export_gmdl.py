"""GMDL exporter: Blender hierarchy -> compact binary read by the Unity runtime.

Layout (little endian):
  "GMDL" int32 version
  int32 nodeCount
  per node (parents always before children):
    str name, int32 parent, float3 pos, float4 rot(xyzw), float3 scale   (Unity space, local)
    int32 submeshCount
    per submesh: u8 kind ('C' lit palette, 'E' emissive, 'G' glass, 'T' tint, or a surface:
                 'K' knit, 'F' fabric, 'U' fur, 'S' skin, 'H' hair, 'W' wood, 'M' metal,
                 'N' glossy, 'L' leather, 'R' stone)
                 int32 rgb, int32 vcount, float3[v] pos, float3[v] nrm, float2[v] uv,
                 u8[v*4] rgba (version 2: r = baked ambient occlusion (255 = open), g = wood grain axis
                              (0 none, 85 x, 170 y, 255 z),
                              a = detail texture scale, 128 = x1, +32 per doubling),
                 int32 icount, int32[i] indices
    int32 propCount, (str key, str value)[]
str = int32 byteLen + utf8
"""
import math
import random
import struct

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

from .core import M_B2U, M_U2B, PALETTE

VERSION = 2
CELLS = 32


def _uv_for(color_hex: str):
    i = PALETTE.index(int(color_hex, 16))
    return ((i % CELLS) + 0.5) / CELLS, ((i // CELLS) + 0.5) / CELLS


class _W:
    def __init__(self):
        self.parts = []

    def i32(self, v):
        self.parts.append(struct.pack('<i', int(v)))

    def u8(self, v):
        self.parts.append(struct.pack('<B', v))

    def f(self, *vals):
        self.parts.append(struct.pack('<%df' % len(vals), *vals))

    def s(self, text):
        b = text.encode('utf-8')
        self.i32(len(b))
        self.parts.append(b)

    def bytes(self):
        return b''.join(self.parts)


def _ordered(root):
    out = []

    def rec(o):
        out.append(o)
        for c in sorted(o.children, key=lambda x: x.name):
            rec(c)

    rec(root)
    return out


class AmbientOcclusion:
    """Per-corner ambient occlusion baked by ray casting against the whole model."""

    RAYS = 28

    def __init__(self, root_obj):
        verts, polys = [], []
        mn = Vector((1e9, 1e9, 1e9))
        mx = -mn

        def rec(o):
            nonlocal mn, mx
            if o.type == 'MESH' and not o.name.startswith(('COL_', 'ZONE_')):
                me = o.data
                mw = o.matrix_world
                base = len(verts)
                for v in me.vertices:
                    w = mw @ v.co
                    verts.append(w)
                    mn = Vector((min(mn.x, w.x), min(mn.y, w.y), min(mn.z, w.z)))
                    mx = Vector((max(mx.x, w.x), max(mx.y, w.y), max(mx.z, w.z)))
                for p in me.polygons:
                    mat = me.materials[p.material_index] if me.materials else None
                    if mat is not None and mat.name[0] in 'GE':
                        continue  # glass and lights do not darken what is behind them
                    polys.append([base + i for i in p.vertices])
            for c in o.children:
                rec(c)

        rec(root_obj)
        self.tree = BVHTree.FromPolygons(verts, polys) if polys else None
        size = (mx - mn).length if verts else 1.0
        self.reach = min(max(size * 0.06, 0.1), 0.7)
        self.eps = max(size * 2e-4, 1e-4)
        rng = random.Random(7)
        self.dirs = []
        for i in range(self.RAYS):
            # stratified cosine-weighted hemisphere (z up)
            u = (i + rng.random()) / self.RAYS
            a = rng.random() * 2 * math.pi
            r = math.sqrt(u)
            self.dirs.append(Vector((r * math.cos(a), r * math.sin(a), math.sqrt(max(0.0, 1 - u)))))
        self.cache = {}

    def at(self, p, n):
        """p, n in Blender world space -> 0..1 (1 = fully open)."""
        if self.tree is None:
            return 1.0
        key = (round(p.x, 4), round(p.y, 4), round(p.z, 4), round(n.x, 2), round(n.y, 2), round(n.z, 2))
        v = self.cache.get(key)
        if v is not None:
            return v
        t = n.orthogonal().normalized()
        b = n.cross(t)
        o = p + n * self.eps
        occ = 0.0
        for d in self.dirs:
            w = t * d.x + b * d.y + n * d.z
            hit = self.tree.ray_cast(o, w, self.reach)
            if hit[0] is not None:
                occ += 1.0 - (hit[3] / self.reach) ** 2
        v = max(0.0, 1.0 - occ / len(self.dirs))
        self.cache[key] = v
        return v


def node_submeshes(obj, ao=None):
    """Returns {kind: (rgb, positions, normals, uvs, colors, indices)} in Unity space."""
    if obj.type != 'MESH':
        return {}
    me = obj.data
    me.calc_loop_triangles()
    m3 = M_B2U.to_3x3()
    mw = obj.matrix_world
    nmat = mw.to_3x3().inverted_safe().transposed()
    # detail texture scale multiplier -> alpha (128 = x1, +32 per doubling)
    detail = float(obj.get('_detail', 1.0))
    detail_a = max(0, min(255, int(round(128 + 32 * math.log2(max(detail, 1e-3))))))
    grain_attr = me.attributes.get('grain')
    groups = {}
    for tri in me.loop_triangles:
        mat = me.materials[tri.material_index] if me.materials else None
        mname = mat.name if mat else 'C_ffffff'
        kind, hx = mname.split('_', 1)
        hx = hx[:6]
        g = groups.setdefault(kind, {'rgb': int(hx, 16), 'p': [], 'n': [], 'uv': [], 'c': [], 'i': []})
        u, v = _uv_for(hx)
        fn = m3 @ tri.normal
        base = len(g['p'])
        verts = [me.vertices[vi] for vi in tri.vertices]
        for k, vert in enumerate(verts):
            p = m3 @ vert.co
            bn = me.loops[tri.loops[k]].normal if tri.use_smooth else tri.normal
            n = (m3 @ bn).normalized()
            g['p'].append((p.x, p.y, p.z))
            g['n'].append((n.x, n.y, n.z))
            g['uv'].append((u, v))
            occ = 1.0
            if ao is not None and kind not in 'EG':
                wn = (nmat @ bn).normalized()
                occ = ao.at(mw @ vert.co, wn)
            grain = grain_attr.data[tri.polygon_index].value if grain_attr is not None else 0
            g['c'].append((int(round(occ * 255)), grain * 85, detail_a))
        # the basis change is a reflection -> reverse winding to keep front faces
        g['i'].extend((base, base + 2, base + 1))
    return groups


def export_gmdl(root_obj, path, extra_props=None, bake_ao=True):
    bpy.context.view_layer.update()
    ao = AmbientOcclusion(root_obj) if bake_ao else None
    w = _W()
    w.parts.append(b'GMDL')
    w.i32(VERSION)
    nodes = _ordered(root_obj)
    index = {o.name: i for i, o in enumerate(nodes)}
    w.i32(len(nodes))
    for o in nodes:
        w.s(o.name)
        w.i32(index[o.parent.name] if (o.parent is not None and o is not root_obj) else -1)
        mu = M_B2U @ (o.matrix_basis if o is not root_obj else o.matrix_basis) @ M_U2B
        loc, rot, sca = mu.decompose()
        w.f(loc.x, loc.y, loc.z)
        w.f(rot.x, rot.y, rot.z, rot.w)
        w.f(sca.x, sca.y, sca.z)
        subs = node_submeshes(o, ao)
        w.i32(len(subs))
        for kind in sorted(subs):
            g = subs[kind]
            w.u8(ord(kind))
            w.i32(g['rgb'])
            w.i32(len(g['p']))
            for p in g['p']:
                w.f(*p)
            for n in g['n']:
                w.f(*n)
            for uv in g['uv']:
                w.f(*uv)
            w.parts.append(bytes(b for (c, gr, a) in g['c'] for b in (c, gr, c, a)))
            w.i32(len(g['i']))
            w.parts.append(struct.pack('<%di' % len(g['i']), *g['i']))
        props = {k: str(o[k]) for k in o.keys() if not k.startswith('_') and isinstance(o[k], (int, float, str))}
        if o is root_obj and extra_props:
            props.update({k: str(v) for k, v in extra_props.items()})
        w.i32(len(props))
        for k, v in sorted(props.items()):
            w.s(k)
            w.s(v)
    data = w.bytes()
    with open(path, 'wb') as f:
        f.write(data)
    return len(data)


def export_palette(path):
    w = _W()
    w.parts.append(b'GPAL')
    w.i32(len(PALETTE.colors))
    for hx in PALETTE.colors:
        w.i32(int(hx, 16))
    with open(path, 'wb') as f:
        f.write(w.bytes())


def export_fbx(root_obj, path):
    bpy.ops.object.select_all(action='DESELECT')

    def sel(o):
        o.select_set(True)
        for c in o.children:
            sel(c)

    sel(root_obj)
    bpy.context.view_layer.objects.active = root_obj
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,
        object_types={'MESH', 'EMPTY'},
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
        path_mode='AUTO',
    )
