"""Low-poly modelling helpers for Sneaky Gnomes.

All authoring happens in *Unity space* (x = right, y = up, z = forward, left-handed).
Geometry is built directly in Blender with bmesh and converted with M_U2B, so that the
exporter (export_gmdl.py) can convert it back exactly.

Model front faces +Z in Unity (= -Y in Blender).
"""
import json
import math
import os
import random
import zlib

import bpy  # must be imported before bmesh
import bmesh
from mathutils import Euler, Matrix, Vector

HS = 4.0  # real metres -> world units (a gnome is 1 unit tall)

# Unity -> Blender basis change (a reflection, det = -1)
M_U2B = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
M_B2U = M_U2B.transposed()

HERE = os.path.dirname(os.path.abspath(__file__))
PALETTE_PATH = os.path.join(HERE, '..', 'palette.json')


def u2b_matrix(mu: Matrix) -> Matrix:
    return M_U2B @ mu @ M_B2U


def b2u_matrix(mb: Matrix) -> Matrix:
    return M_B2U @ mb @ M_U2B


def rot_u(rx=0.0, ry=0.0, rz=0.0) -> Matrix:
    """Unity-style Euler (radians): Z first, then X, then Y."""
    return Euler((rx, ry, rz), 'ZXY').to_matrix().to_4x4()


def trs_u(pos=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)) -> Matrix:
    t = Matrix.Translation(Vector(pos))
    r = rot_u(*rot)
    s = Matrix.Diagonal(Vector((scale[0], scale[1], scale[2], 1.0)))
    return t @ r @ s


# ---------------------------------------------------------------- palette

class Palette:
    """Global colour palette shared by every model (max 256 entries)."""

    def __init__(self):
        self.colors = []
        if os.path.exists(PALETTE_PATH):
            with open(PALETTE_PATH) as f:
                self.colors = json.load(f)

    def index(self, c: int) -> int:
        hx = '%06x' % (c & 0xFFFFFF)
        if hx not in self.colors:
            if len(self.colors) >= 256:
                # snap to the nearest existing colour
                r, g, b = (c >> 16) & 255, (c >> 8) & 255, c & 255
                best, bd = 0, 1e9
                for i, h in enumerate(self.colors):
                    v = int(h, 16)
                    d = (r - (v >> 16 & 255)) ** 2 + (g - (v >> 8 & 255)) ** 2 + (b - (v & 255)) ** 2
                    if d < bd:
                        best, bd = i, d
                return best
            self.colors.append(hx)
        return self.colors.index(hx)

    def save(self):
        with open(PALETTE_PATH, 'w') as f:
            json.dump(self.colors, f, indent=0)


PALETTE = Palette()

# material kinds understood by the exporter/runtime
MAT_OPAQUE = 'C'  # palette, lit
MAT_EMIT = 'E'  # palette, emissive/unlit glow
MAT_GLASS = 'G'  # palette, transparent
MAT_TINT = 'T'  # recoloured at runtime (gnome hats, player colours)


def material(kind: str, color: int):
    name = f'{kind}_{color & 0xFFFFFF:06x}'
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        r, g, b = ((color >> 16) & 255) / 255, ((color >> 8) & 255) / 255, (color & 255) / 255
        m.diffuse_color = (r, g, b, 0.4 if kind == MAT_GLASS else 1.0)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get('Principled BSDF')
        if bsdf:
            # Cycles preview: sRGB -> linear
            lin = tuple(((v + 0.055) / 1.055) ** 2.4 if v > 0.04045 else v / 12.92 for v in (r, g, b))
            bsdf.inputs['Base Color'].default_value = (*lin, 1)
            bsdf.inputs['Roughness'].default_value = 0.75
            if kind == MAT_EMIT:
                bsdf.inputs['Emission Color'].default_value = (*lin, 1)
                bsdf.inputs['Emission Strength'].default_value = 3.0
            if kind == MAT_GLASS:
                bsdf.inputs['Alpha'].default_value = 0.35
                bsdf.inputs['Roughness'].default_value = 0.1
        PALETTE.index(color)
    return m


# ---------------------------------------------------------------- node builder

class Node:
    """A transform in the model hierarchy that owns (optional) low-poly geometry.

    Positions/rotations are Unity-space, relative to the parent node.
    """

    def __init__(self, name, parent=None, pos=(0, 0, 0), rot=(0, 0, 0), seed=None, k=None):
        self.name = name
        self.parent = parent
        # unit scale: furniture is authored in metres (k = HS), characters in world units (k = 1)
        self.k = k if k is not None else (parent.k if parent is not None else 1.0)
        self.pos = tuple(v * self.k for v in pos)
        self.rot = rot
        self.scale = (1, 1, 1)
        self.children = []
        self.bm = bmesh.new()
        self.mats = []
        self.rng = random.Random(seed if seed is not None else zlib.crc32(name.encode()))
        self.obj = None
        self.props = {}
        if parent:
            parent.children.append(self)

    # -- materials --
    def _mat_index(self, kind, color):
        m = material(kind, color)
        if m.name not in self.mats:
            self.mats.append(m.name)
        return self.mats.index(m.name)

    def _wonk(self, verts, amount):
        if amount <= 0:
            return
        for v in verts:
            v.co.x += self.rng.uniform(-amount, amount)
            v.co.y += self.rng.uniform(-amount, amount)
            v.co.z += self.rng.uniform(-amount, amount)

    def _commit(self, tb, kind, color, smooth=False, wonk=0.0, fix_normals=False):
        """Colour every face of the temporary bmesh and merge it into this node."""
        if wonk:
            self._wonk(tb.verts, wonk)
        if fix_normals:
            bmesh.ops.recalc_face_normals(tb, faces=list(tb.faces))
        mi = self._mat_index(kind, color)
        for f in tb.faces:
            f.material_index = mi
            f.smooth = smooth
        scratch = bpy.data.meshes.get('__scratch__') or bpy.data.meshes.new('__scratch__')
        tb.to_mesh(scratch)
        tb.free()
        self.bm.from_mesh(scratch)
        return self

    # -- primitives (sizes are full extents, Unity space) --
    def box(self, size, pos=(0, 0, 0), color=0xffffff, rot=(0, 0, 0), bevel=0.0, kind=MAT_OPAQUE, wonk=0.0, taper=None):
        """Box. bevel = chamfer width (local units). taper=(sx,sz) scales the top face."""
        k = self.k
        size, pos, bevel, wonk = tuple(v * k for v in size), tuple(v * k for v in pos), bevel * k, wonk * k
        tb = bmesh.new()
        verts = bmesh.ops.create_cube(tb, size=1.0)['verts']
        if taper:
            for v in verts:
                if v.co.z > 0:  # blender z = unity up
                    v.co.x *= taper[0]
                    v.co.y *= taper[1]
        bmesh.ops.transform(tb, matrix=u2b_matrix(trs_u(pos, rot, size)), verts=verts)
        if bevel > 0:
            bmesh.ops.bevel(tb, geom=list(tb.edges), offset=bevel, segments=1, affect='EDGES', profile=0.5, clamp_overlap=True)
        return self._commit(tb, kind, color, wonk=wonk)

    def boxb(self, size, pos=(0, 0, 0), color=0xffffff, **kw):
        """Box with its bottom at pos.y."""
        return self.box(size, (pos[0], pos[1] + size[1] / 2, pos[2]), color, **kw)

    def cyl(self, r, h, pos=(0, 0, 0), color=0xffffff, rot=(0, 0, 0), seg=8, r2=None, kind=MAT_OPAQUE, wonk=0.0, cap=True, smooth=False):
        """Cylinder along local Y centred at pos. r2 = top radius (frustum)."""
        k = self.k
        r, h, pos, wonk = r * k, h * k, tuple(v * k for v in pos), wonk * k
        r2 = None if r2 is None else r2 * k
        tb = bmesh.new()
        verts = bmesh.ops.create_cone(tb, cap_ends=cap, cap_tris=False, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=h)['verts']
        bmesh.ops.transform(tb, matrix=u2b_matrix(trs_u(pos, rot)), verts=verts)
        if r2 == 0.0:
            bmesh.ops.remove_doubles(tb, verts=list(tb.verts), dist=1e-6)
        return self._commit(tb, kind, color, smooth=smooth, wonk=wonk)

    def cylb(self, r, h, pos=(0, 0, 0), color=0xffffff, **kw):
        return self.cyl(r, h, (pos[0], pos[1] + h / 2, pos[2]), color, **kw)

    def cone(self, r, h, pos=(0, 0, 0), color=0xffffff, rot=(0, 0, 0), seg=8, kind=MAT_OPAQUE, wonk=0.0):
        return self.cyl(r, h, pos, color, rot=rot, seg=seg, r2=0.0, kind=kind, wonk=wonk)

    def sphere(self, r, pos=(0, 0, 0), color=0xffffff, scale=(1, 1, 1), rot=(0, 0, 0), seg=8, rings=5, kind=MAT_OPAQUE, wonk=0.0, ico=0, smooth=False):
        k = self.k
        r, pos, wonk = r * k, tuple(v * k for v in pos), wonk * k
        tb = bmesh.new()
        if ico:
            verts = bmesh.ops.create_icosphere(tb, subdivisions=ico, radius=r)['verts']
        else:
            verts = bmesh.ops.create_uvsphere(tb, u_segments=seg, v_segments=rings, radius=r)['verts']
        bmesh.ops.transform(tb, matrix=u2b_matrix(trs_u(pos, rot, scale)), verts=verts)
        return self._commit(tb, kind, color, smooth=smooth, wonk=wonk)

    def torus(self, R, r, pos=(0, 0, 0), color=0xffffff, rot=(0, 0, 0), seg=10, tseg=5, kind=MAT_OPAQUE):
        """Torus lying in the XZ plane (hole along Y)."""
        k = self.k
        R, r, pos = R * k, r * k, tuple(v * k for v in pos)
        tb = bmesh.new()
        rings = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            ring = []
            for j in range(tseg):
                b = 2 * math.pi * j / tseg
                x = (R + r * math.cos(b)) * math.cos(a)
                z = (R + r * math.cos(b)) * math.sin(a)
                y = r * math.sin(b)
                ring.append(tb.verts.new((x, y, z)))
            rings.append(ring)
        for i in range(seg):
            for j in range(tseg):
                a, b = rings[i][j], rings[(i + 1) % seg][j]
                c, d = rings[(i + 1) % seg][(j + 1) % tseg], rings[i][(j + 1) % tseg]
                tb.faces.new((a, d, c, b))
        # geometry above was written in unity-local numbers; convert
        bmesh.ops.transform(tb, matrix=u2b_fix(trs_u(pos, rot)), verts=list(tb.verts))
        return self._commit(tb, kind, color, fix_normals=True)

    def lathe(self, profile, pos=(0, 0, 0), color=0xffffff, rot=(0, 0, 0), seg=8, kind=MAT_OPAQUE, wonk=0.0, cap_bottom=True, cap_top=True):
        """Revolve a profile [(radius, y), ...] (bottom to top) around local Y."""
        k = self.k
        profile = [(a * k, b * k) for (a, b) in profile]
        pos, wonk = tuple(v * k for v in pos), wonk * k
        tb = bmesh.new()
        rings = []
        for (rad, y) in profile:
            ring = []
            for i in range(seg):
                a = 2 * math.pi * i / seg
                ring.append(tb.verts.new((rad * math.cos(a), y, rad * math.sin(a))))
            rings.append(ring)
        for k in range(len(rings) - 1):
            for i in range(seg):
                a, b = rings[k][i], rings[k][(i + 1) % seg]
                c, d = rings[k + 1][(i + 1) % seg], rings[k + 1][i]
                tb.faces.new((a, d, c, b))
        if cap_bottom and profile[0][0] > 1e-5:
            tb.faces.new(list(rings[0]))
        if cap_top and profile[-1][0] > 1e-5:
            tb.faces.new(list(reversed(rings[-1])))
        bmesh.ops.transform(tb, matrix=u2b_fix(trs_u(pos, rot)), verts=list(tb.verts))
        bmesh.ops.remove_doubles(tb, verts=list(tb.verts), dist=1e-6)
        return self._commit(tb, kind, color, wonk=wonk, fix_normals=True)

    def poly_prism(self, pts2d, depth, pos=(0, 0, 0), color=0xffffff, rot=(0, 0, 0), kind=MAT_OPAQUE, bevel=0.0):
        """Extrude a 2D polygon (x, y) along local Z by depth (centred)."""
        k = self.k
        pts2d = [(a * k, b * k) for (a, b) in pts2d]
        depth, pos, bevel = depth * k, tuple(v * k for v in pos), bevel * k
        tb = bmesh.new()
        front = [tb.verts.new((x, y, depth / 2)) for (x, y) in pts2d]
        back = [tb.verts.new((x, y, -depth / 2)) for (x, y) in pts2d]
        n = len(pts2d)
        tb.faces.new(front)
        tb.faces.new(list(reversed(back)))
        for i in range(n):
            a, b = front[i], front[(i + 1) % n]
            c, d = back[(i + 1) % n], back[i]
            tb.faces.new((a, d, c, b))
        bmesh.ops.transform(tb, matrix=u2b_fix(trs_u(pos, rot)), verts=list(tb.verts))
        if bevel > 0:
            bmesh.ops.bevel(tb, geom=list(tb.edges), offset=bevel, segments=1, affect='EDGES', profile=0.5, clamp_overlap=True)
        return self._commit(tb, kind, color, fix_normals=True)

    # -- markers (exported as special child nodes) --
    def marker(self, prefix, name, pos=(0, 0, 0), rot=(0, 0, 0), size=(1, 1, 1), scale_size=True):
        n = Node(f'{prefix}_{name}', self, pos, rot)
        n.scale = tuple(v * self.k for v in size) if scale_size else size
        return n

    def col_box(self, size, pos=(0, 0, 0), rot=(0, 0, 0), name=None):
        """Physics collider (invisible) - exported as COL_ node with scale = size."""
        return self.marker('COL', name or f'b{len(self.children)}', pos, rot, size)

    def col_boxb(self, size, pos=(0, 0, 0), rot=(0, 0, 0), name=None):
        return self.col_box(size, (pos[0], pos[1] + size[1] / 2, pos[2]), rot, name)

    def solid(self, size, pos=(0, 0, 0), color=0xffffff, rot=(0, 0, 0), **kw):
        """Visible box + matching collider."""
        self.box(size, pos, color, rot=rot, **kw)
        self.col_box(size, pos, rot)
        return self

    def solidb(self, size, pos=(0, 0, 0), color=0xffffff, **kw):
        return self.solid(size, (pos[0], pos[1] + size[1] / 2, pos[2]), color, **kw)

    # -- build blender objects --
    def realize(self, collection=None):
        coll = collection or bpy.context.scene.collection
        if len(self.bm.faces):
            me = bpy.data.meshes.new(self.name)
            self.bm.normal_update()
            self.bm.to_mesh(me)
            for mn in self.mats:
                me.materials.append(bpy.data.materials[mn])
            obj = bpy.data.objects.new(self.name, me)
        else:
            obj = bpy.data.objects.new(self.name, None)
            obj.empty_display_size = 0.15
            obj.empty_display_type = 'CUBE' if self.name.startswith(('COL_', 'ZONE_', 'SURF_')) else 'PLAIN_AXES'
        self.bm.free()
        coll.objects.link(obj)
        if self.parent is not None:
            obj.parent = self.parent.obj
        obj.matrix_basis = u2b_matrix(trs_u(self.pos, self.rot, self.scale))
        if obj.type == 'EMPTY' and self.name.startswith(('COL_', 'ZONE_', 'SURF_')):
            obj.empty_display_size = 0.5
            obj.hide_render = True
        for k, v in self.props.items():
            obj[k] = v
        self.obj = obj
        for c in self.children:
            c.realize(coll)
        return obj


def u2b_fix(mu: Matrix) -> Matrix:
    """For geometry authored directly in unity-local numbers (torus/lathe/prism):
    vertices were created with unity coords, so convert them: v_b = M * (mu * v_u)."""
    return M_U2B @ mu


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)


def m(v):
    """metres -> world units"""
    if isinstance(v, (tuple, list)):
        return tuple(x * HS for x in v)
    return v * HS
