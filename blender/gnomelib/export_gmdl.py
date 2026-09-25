"""GMDL exporter: Blender hierarchy -> compact binary read by the Unity runtime.

Layout (little endian):
  "GMDL" int32 version
  int32 nodeCount
  per node (parents always before children):
    str name, int32 parent, float3 pos, float4 rot(xyzw), float3 scale   (Unity space, local)
    int32 submeshCount
    per submesh: u8 kind ('C' lit palette, 'E' emissive, 'G' glass, 'T' tint)
                 int32 rgb, int32 vcount, float3[v] pos, float3[v] nrm, float2[v] uv,
                 int32 icount, int32[i] indices
    int32 propCount, (str key, str value)[]
str = int32 byteLen + utf8
"""
import struct

import bpy

from .core import M_B2U, M_U2B, PALETTE

VERSION = 1
CELLS = 16


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


def node_submeshes(obj):
    """Returns {kind: (rgb, positions, normals, uvs, indices)} in Unity space."""
    if obj.type != 'MESH':
        return {}
    me = obj.data
    me.calc_loop_triangles()
    m3 = M_B2U.to_3x3()
    groups = {}
    for tri in me.loop_triangles:
        mat = me.materials[tri.material_index] if me.materials else None
        mname = mat.name if mat else 'C_ffffff'
        kind, hx = mname.split('_', 1)
        hx = hx[:6]
        g = groups.setdefault(kind, {'rgb': int(hx, 16), 'p': [], 'n': [], 'uv': [], 'i': []})
        u, v = _uv_for(hx)
        fn = m3 @ tri.normal
        base = len(g['p'])
        verts = [me.vertices[vi] for vi in tri.vertices]
        for k, vert in enumerate(verts):
            p = m3 @ vert.co
            if tri.use_smooth:
                n = (m3 @ me.loops[tri.loops[k]].normal).normalized()
            else:
                n = fn
            g['p'].append((p.x, p.y, p.z))
            g['n'].append((n.x, n.y, n.z))
            g['uv'].append((u, v))
        # the basis change is a reflection -> reverse winding to keep front faces
        g['i'].extend((base, base + 2, base + 1))
    return groups


def export_gmdl(root_obj, path, extra_props=None):
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
        subs = node_submeshes(o)
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
