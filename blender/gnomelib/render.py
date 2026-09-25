"""Cycles (CPU) preview renders of models, used to eyeball the art from a headless box."""
import math

import bpy
from mathutils import Vector


def world_bounds(root):
    mn = Vector((1e9, 1e9, 1e9))
    mx = Vector((-1e9, -1e9, -1e9))

    def rec(o):
        nonlocal mn, mx
        if o.type == 'MESH' and not o.hide_render:
            for c in o.bound_box:
                w = o.matrix_world @ Vector(c)
                mn = Vector((min(mn.x, w.x), min(mn.y, w.y), min(mn.z, w.z)))
                mx = Vector((max(mx.x, w.x), max(mx.y, w.y), max(mx.z, w.z)))
        for ch in o.children:
            rec(ch)

    rec(root)
    return mn, mx


def setup_render(res=480, samples=24, bg=(0.93, 0.89, 0.8)):
    s = bpy.context.scene
    s.render.engine = 'CYCLES'
    s.cycles.device = 'CPU'
    s.cycles.samples = samples
    try:
        s.cycles.use_denoising = True
    except Exception:
        pass
    s.render.resolution_x = res
    s.render.resolution_y = res
    s.render.film_transparent = False
    s.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.get('World') or bpy.data.worlds.new('World')
    s.world = w
    w.use_nodes = True
    bgn = w.node_tree.nodes.get('Background')
    lin = tuple(c ** 2.2 for c in bg)
    bgn.inputs[0].default_value = (*lin, 1)
    bgn.inputs[1].default_value = 0.55


def _light(name, kind, loc, energy, size=1.0, color=(1, 1, 1)):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = color
    if kind == 'AREA':
        ld.size = size
    if kind == 'SUN':
        ld.angle = 0.3
    o = bpy.data.objects.new(name, ld)
    bpy.context.scene.collection.objects.link(o)
    o.location = loc
    return o


def _aim(o, target):
    d = (Vector(target) - o.location)
    o.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()


def render_preview(root, path, yaw_deg=35, pitch_deg=18, res=480, samples=24, ground=True, zoom=1.0):
    """Frame the model from its front-left (model front = Blender -Y)."""
    setup_render(res, samples)
    bpy.context.view_layer.update()
    mn, mx = world_bounds(root)
    center = (mn + mx) / 2
    size = (mx - mn).length
    temp = []
    if ground:
        bpy.ops.mesh.primitive_plane_add(size=size * 8 + 2, location=(center.x, center.y, mn.z - 0.001))
        g = bpy.context.active_object
        mat = bpy.data.materials.new('preview_ground')
        mat.use_nodes = True
        mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.75, 0.68, 0.55, 1)
        g.data.materials.append(mat)
        temp.append(g)
    cam_d = bpy.data.cameras.new('pcam')
    cam_d.lens = 50
    cam = bpy.data.objects.new('pcam', cam_d)
    bpy.context.scene.collection.objects.link(cam)
    yaw = math.radians(yaw_deg)
    pitch = math.radians(pitch_deg)
    dist = size * 1.3 / zoom + 0.2
    # front is -Y in blender; orbit around
    cam.location = center + Vector((-math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch))) * dist
    _aim(cam, center)
    bpy.context.scene.camera = cam
    temp.append(cam)
    d2 = size * size
    key = _light('key', 'AREA', center + Vector((-size, -size * 1.2, size * 1.5)), 22 * d2 + 2, size * 1.5)
    _aim(key, center)
    fill = _light('fill', 'AREA', center + Vector((size * 1.5, -size * 0.5, size * 0.6)), 7 * d2 + 1, size * 2, (0.8, 0.85, 1.0))
    _aim(fill, center)
    rim = _light('rim', 'AREA', center + Vector((size * 0.3, size * 1.5, size * 1.2)), 14 * d2 + 1, size, (1.0, 0.9, 0.8))
    _aim(rim, center)
    temp += [key, fill, rim]
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for o in temp:
        bpy.data.objects.remove(o, do_unlink=True)
