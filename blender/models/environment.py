"""Garden decor, the house roof and the floating hub island. Authored in METRES (k = HS)."""
import math
import random

from gnomelib.core import HS, MAT_EMIT, MAT_GLASS, Node

LEAF = 0x3f8f45
LEAF2 = 0x4fa152
LEAF3 = 0x357a3a
BARK = 0x6b4a2e
STONE = 0x8d8a84
STONE2 = 0x74716c
GRASS = 0x5b9a3c
GRASS2 = 0x6aae47
DIRT = 0x7a5a3a
R90 = math.pi / 2


def env(name):
    n = Node(name, k=HS)
    n.props['kind'] = name
    return n


def tree():
    n = env('tree')
    n.cylb(0.18, 2.2, (0, 0, 0), BARK, seg=7, r2=0.12, wonk=0.02)
    n.col_boxb((0.34, 2.2, 0.34))
    rng = random.Random(3)
    for i in range(5):
        a = i * 1.3
        r = 0.4 + rng.random() * 0.3
        n.sphere(0.9 + rng.random() * 0.3, (math.cos(a) * r, 2.4 + rng.random() * 0.8, math.sin(a) * r), [LEAF, LEAF2, LEAF3][i % 3], ico=1, wonk=0.12)
    n.sphere(0.8, (0, 3.3, 0), LEAF2, ico=1, wonk=0.1)
    return n


def bush():
    n = env('bush')
    n.sphere(0.35, (0, 0.25, 0), LEAF, ico=1, wonk=0.05, scale=(1.2, 0.8, 1))
    n.sphere(0.25, (0.25, 0.2, 0.1), LEAF2, ico=1, wonk=0.04)
    n.sphere(0.22, (-0.2, 0.18, -0.1), LEAF3, ico=1, wonk=0.04)
    for (x, z) in ((0.1, 0.3), (-0.2, 0.2), (0.3, -0.1)):
        n.sphere(0.035, (x, 0.35, z), 0xd84a5a, seg=5, rings=3)  # berries
    n.col_boxb((0.7, 0.45, 0.6))
    return n


def flowers():
    n = env('flowers')
    rng = random.Random(5)
    cols = [0xe24a6a, 0xf2cf3c, 0xffffff, 0x9b6ae0, 0xff8a3a]
    for i in range(7):
        x, z = rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2)
        h = rng.uniform(0.15, 0.35)
        n.cylb(0.006, h, (x, 0, z), LEAF, seg=4)
        c = cols[i % len(cols)]
        for k in range(5):
            a = k * 2 * math.pi / 5
            n.sphere(0.02, (x + math.cos(a) * 0.022, h, z + math.sin(a) * 0.022), c, scale=(1, 0.4, 1), seg=5, rings=3)
        n.sphere(0.012, (x, h + 0.005, z), 0xf2cf3c, seg=5, rings=3)
        n.box((0.05, 0.004, 0.02), (x + 0.02, h * 0.5, z), LEAF2, rot=(0, rng.random() * 3, 0.4))
    return n


def rock():
    n = env('rock')
    n.sphere(0.25, (0, 0.1, 0), STONE, scale=(1.3, 0.7, 1), ico=1, wonk=0.05)
    n.sphere(0.12, (0.25, 0.05, 0.1), STONE2, ico=1, wonk=0.03)
    n.col_boxb((0.6, 0.3, 0.45))
    return n


def toadstool():
    n = env('toadstool')
    for (x, z, s, c) in ((0, 0, 1.0, 0xd8342c), (0.12, 0.08, 0.7, 0xc98a47), (-0.1, 0.1, 0.55, 0xd8342c)):
        n.cylb(0.025 * s, 0.14 * s, (x, 0, z), 0xf3ead6, seg=6)
        n.sphere(0.07 * s, (x, 0.14 * s, z), c, scale=(1, 0.55, 1), seg=8, rings=5)
        n.sphere(0.012 * s, (x + 0.03 * s, 0.17 * s, z), 0xffffff, seg=4, rings=3)
    return n


def garden_lamp():
    n = env('gardenLamp')
    n.cylb(0.03, 0.9, (0, 0, 0), 0x2a2a2a, seg=6)
    n.col_boxb((0.08, 0.9, 0.08))
    n.box((0.16, 0.2, 0.16), (0, 0.98, 0), 0xfff0b0, kind=MAT_EMIT)
    n.cyl(0.14, 0.08, (0, 1.12, 0), 0x2a2a2a, r2=0.02, seg=4, rot=(0, math.pi / 4, 0))
    m = n.marker('LIGHT', 'lamp', (0, 0.98, 0), size=(1, 1, 1), scale_size=False)
    m.props.update({'color': 'ffd890', 'range': 5 * HS, 'intensity': 1.4, 'on': 1})
    return n


def fence():
    """Picket fence along the back of the garden, 20 m long, centred on the origin (runs along X)."""
    n = env('fence')
    L = 20.0
    c = 0xe8e2d4
    n.box((L, 0.06, 0.03), (0, 0.25, 0), c)
    n.box((L, 0.06, 0.03), (0, 0.65, 0), c)
    for i in range(int(L / 0.25) + 1):
        x = -L / 2 + i * 0.25
        n.boxb((0.09, 0.8, 0.025), (x, 0, 0.03), c, wonk=0.004)
        n.cone(0.064, 0.08, (x, 0.84, 0.03), c, seg=4, rot=(0, math.pi / 4, 0))
    n.col_boxb((L, 1.0, 0.2))
    return n


def roof():
    """Gable roof covering the 14 x 10 m house. Origin at wall-top level, centred."""
    n = env('roof')
    W, D, H = 14.8, 10.8, 3.0
    tile = 0x9e4a35
    tile2 = 0x86402e
    # two slopes built as tilted boxes
    slope = math.atan2(H, D / 2)
    length = math.hypot(H, D / 2) + 0.2
    for s in (-1, 1):
        n.box((W, 0.12, length), (0, H / 2, s * D / 4), tile, rot=(s * slope, 0, 0))
        for k in range(6):
            t = (k + 0.5) / 6
            y = H * (1 - t)
            z = s * (D / 2) * t
            n.box((W + 0.02, 0.03, 0.08), (0, y + 0.07, z), tile2, rot=(s * slope, 0, 0))
    # gable walls
    tri = [(-D / 2, 0), (D / 2, 0), (0, H)]
    n.poly_prism(tri, 0.15, (W / 2 - 0.1, 0, 0), 0xe7dcc6, rot=(0, R90, 0))
    n.poly_prism(tri, 0.15, (-W / 2 + 0.1, 0, 0), 0xe7dcc6, rot=(0, R90, 0))
    n.boxb((0.6, 1.6, 0.6), (4.0, H * 0.45, -1.2), 0xa0503a)  # chimney
    n.boxb((0.7, 0.1, 0.7), (4.0, H * 0.45 + 1.6, -1.2), 0x7a3a2a)
    # little round attic window
    n.cyl(0.35, 0.05, (W / 2 - 0.02, H * 0.4, 0), 0xfff0b0, rot=(0, 0, R90), seg=10, kind=MAT_EMIT)
    return n


# ------------------------------------------------------------------ hub island

def hub_island():
    """Floating island, ~14 m across. Top surface at y = 0."""
    n = env('hubIsland')
    R = 7.0
    rng = random.Random(11)
    # grass top (wonky 16-gon)
    n.cyl(R, 0.5, (0, -0.25, 0), GRASS, seg=16, wonk=0.08)
    n.cyl(R * 0.97, 0.52, (0, -0.24, 0), GRASS2, seg=16, r2=R * 0.6)
    # underside: stacked shrinking rock layers + dangling roots
    layers = [(R * 0.98, -0.6, 0.8), (R * 0.8, -1.5, 1.2), (R * 0.55, -2.7, 1.4), (R * 0.3, -4.0, 1.4), (R * 0.1, -5.2, 1.2)]
    for i, (r, y, h) in enumerate(layers):
        n.cyl(r, h, (0, y, 0), [DIRT, STONE, STONE2, STONE, STONE2][i], seg=12, r2=r * 1.05, wonk=0.25)
    for i in range(10):
        a = rng.random() * 6.28
        r = rng.uniform(1, R * 0.8)
        n.cyl(0.05, rng.uniform(1, 2.5), (math.cos(a) * r, -2.0, math.sin(a) * r), BARK, rot=(rng.uniform(-0.3, 0.3), 0, rng.uniform(-0.3, 0.3)), seg=4)
    # colliders: 4 rotated boxes approximate the disc
    side = R * 2 * math.cos(math.pi / 8)
    for k in range(4):
        n.col_box((side, 1.0, R * 2 * math.sin(math.pi / 8) * 2.2), (0, -0.5, 0), rot=(0, k * math.pi / 4, 0))
    # rim of rocks (so gnomes don't fall off too easily)
    for i in range(22):
        a = i / 22 * 2 * math.pi
        if abs(a - math.pi * 1.5) < 0.25:
            continue  # gap facing the portal side
        r = R - 0.25
        s = rng.uniform(0.18, 0.3)
        n.sphere(s, (math.cos(a) * r, s * 0.6, math.sin(a) * r), STONE, ico=1, wonk=0.04, scale=(1.3, 1, 1))
        n.col_box((s * 2.4, 0.8, s * 2.4), (math.cos(a) * r, 0.4, math.sin(a) * r), rot=(0, -a, 0))
    # path stones
    for i in range(7):
        n.cyl(0.18, 0.04, (0.2 * math.sin(i), 0.0, 2.2 - i * 0.7), STONE, seg=7, wonk=0.02)
    # little pond
    n.cyl(1.0, 0.03, (-3.2, 0.01, -1.5), 0x4f9fcf, seg=12, kind=MAT_GLASS)
    for i in range(10):
        a = i / 10 * 6.28
        n.sphere(0.12, (-3.2 + math.cos(a) * 1.05, 0.03, -1.5 + math.sin(a) * 1.05), STONE2, ico=1, wonk=0.02)
    n.cyl(0.15, 0.01, (-3.0, 0.035, -1.2), 0x5aa85f, seg=6)  # lily pad
    return n


def craft_bench():
    """A tree stump turned into a workbench."""
    n = env('craftBench')
    n.cylb(0.45, 0.55, (0, 0, 0), BARK, seg=10, r2=0.42, wonk=0.02)
    n.cyl(0.43, 0.04, (0, 0.56, 0), 0xc89560, seg=10)
    for i in range(3):
        n.torus(0.15 + i * 0.1, 0.006, (0, 0.585, 0), 0xa87848, seg=12, tseg=3)
    n.col_boxb((0.8, 0.58, 0.8))
    # tools
    n.box((0.25, 0.02, 0.03), (0.1, 0.6, 0.1), 0x7a4a2a, rot=(0, 0.5, 0))
    n.box((0.08, 0.04, 0.05), (0.2, 0.61, 0.17), 0x777777, rot=(0, 0.5, 0))
    n.box((0.3, 0.01, 0.08), (-0.15, 0.6, -0.05), 0xb9c0c8, rot=(0, -0.3, 0))
    n.cyl(0.04, 0.08, (-0.2, 0.62, 0.2), 0x9ad0ee, seg=6, kind=MAT_GLASS)
    n.sphere(0.03, (-0.2, 0.62, 0.2), 0x9b44c9, seg=5, rings=3, kind=MAT_EMIT)
    n.cyl(0.035, 0.1, (0.25, 0.63, -0.15), 0xf2cf3c, seg=6)
    # a tiny sign
    n.cylb(0.015, 0.5, (0.5, 0, 0.1), BARK, seg=4)
    n.box((0.3, 0.15, 0.02), (0.5, 0.5, 0.1), 0xc89560, rot=(0, -0.3, 0))
    n.marker('ANCHOR', 'use', (0, 0, 0.75), size=(1, 1, 1), scale_size=False)
    return n


def portal():
    """Fairy ring of glowing toadstools: step inside to start the night."""
    n = env('portal')
    R = 0.9
    n.cyl(R, 0.02, (0, 0.01, 0), 0x6a3aa0, seg=16, kind=MAT_EMIT)
    n.cyl(R * 0.7, 0.025, (0, 0.012, 0), 0x9a6ae0, seg=16, kind=MAT_EMIT)
    n.cyl(R * 0.35, 0.03, (0, 0.014, 0), 0xd0b0ff, seg=12, kind=MAT_EMIT)
    for i in range(9):
        a = i / 9 * 2 * math.pi
        x, z = math.cos(a) * (R + 0.1), math.sin(a) * (R + 0.1)
        n.cylb(0.03, 0.14, (x, 0, z), 0xf3ead6, seg=5)
        n.sphere(0.08, (x, 0.15, z), 0x7fe0ff, scale=(1, 0.5, 1), seg=7, rings=4, kind=MAT_EMIT)
    # twig arch
    n.torus(R * 0.9, 0.04, (0, 0, 0), BARK, rot=(R90, 0, 0), seg=16, tseg=4)
    m = n.marker('ZONE', 'portal', (0, 0.5, 0), size=(R * 1.6, 1.0, R * 1.6))
    lt = n.marker('LIGHT', 'glow', (0, 0.6, 0), size=(1, 1, 1), scale_size=False)
    lt.props.update({'color': 'a080ff', 'range': 4 * HS, 'intensity': 1.6, 'on': 1})
    return n


def lantern():
    n = env('lantern')
    n.cylb(0.025, 1.1, (0, 0, 0), BARK, seg=5)
    n.box((0.25, 0.03, 0.03), (0.1, 1.1, 0), BARK)
    n.box((0.1, 0.14, 0.1), (0.2, 0.98, 0), 0xffc860, kind=MAT_EMIT)
    n.cone(0.08, 0.06, (0.2, 1.07, 0), 0x3a2a1a, seg=4, rot=(0, math.pi / 4, 0))
    n.col_boxb((0.08, 1.1, 0.08))
    lt = n.marker('LIGHT', 'l', (0.2, 0.98, 0), size=(1, 1, 1), scale_size=False)
    lt.props.update({'color': 'ffc070', 'range': 3.5 * HS, 'intensity': 1.2, 'on': 1})
    return n


def signpost():
    n = env('signpost')
    n.cylb(0.03, 0.9, (0, 0, 0), BARK, seg=5)
    n.box((0.45, 0.12, 0.03), (0.15, 0.8, 0), 0xc89560, rot=(0, 0, 0.05))
    n.cone(0.07, 0.1, (0.42, 0.8, 0), 0xc89560, rot=(0, 0, -R90), seg=3)
    n.box((0.4, 0.12, 0.03), (-0.12, 0.6, 0), 0xb88550, rot=(0, 0.3, -0.05))
    n.col_boxb((0.08, 0.9, 0.08))
    return n


MODELS = {
    'tree': tree,
    'bush': bush,
    'flowers': flowers,
    'rock': rock,
    'toadstool': toadstool,
    'gardenLamp': garden_lamp,
    'fence': fence,
    'roof': roof,
    'hubIsland': hub_island,
    'craftBench': craft_bench,
    'portal': portal,
    'lantern': lantern,
    'signpost': signpost,
}
