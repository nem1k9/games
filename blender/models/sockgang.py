"""Models specific to "The Sock Gang": the Great Sock, the village under the porch, pickle jars,
Kesha the parrot, mousetraps, the porch, the yarn basket, the sardine-tin cart and a few items.
Authored in METRES (k = HS) unless noted."""
import math
import random

from gnomelib.core import HS, MAT_EMIT, MAT_GLASS, MAT_TINT, Node

R90 = math.pi / 2
WOOD_L = 0xc89560
WOOD_M = 0x9a6236
WOOD_D = 0x6a3f22
PLANK = 0x8a6a4a
DIRT = 0x6e5238
MOSS = 0x5b8a3c
STONE = 0x8d8a84
BRASS = 0xc6a14a
GOLD = 0xe8b83a
WHITE = 0xf1efe8
CREAM = 0xf3e3bf
RED = 0xc23b30
BLACK = 0x1d1e22
YARN_COLS = [0xd8342c, 0x2f6fd6, 0x3aa845, 0xe2b21e, 0x9b44c9, 0xef7a24, 0xe79aa8]


def mk(name):
    n = Node(name, k=HS)
    n.props['kind'] = name
    return n


def marker_light(n, name, pos, color, rng, intensity):
    lt = n.marker('LIGHT', name, pos, size=(1, 1, 1), scale_size=False)
    lt.props.update({'color': '%06x' % color, 'range': rng * HS, 'intensity': intensity, 'on': 1})
    return lt


def mech(n, mid, role, kind, pivot, axis='y', open_=0.0, handle=(0, 0, 0)):
    m = Node('MECH_' + mid, n, pivot)
    m.props.update({'role': role, 'kind': kind, 'axis': axis, 'open': open_ * HS if kind == 'slide' else open_,
                    'hx': handle[0] * HS, 'hy': handle[1] * HS, 'hz': handle[2] * HS, 'humanOnly': 0})
    return m


# ------------------------------------------------------------------ the Great Sock

def great_sock():
    """The Great Sock: ancient, wise, slightly smelly. Gives out the nightly list of pranks."""
    n = mk('greatSock')
    stripes = [0xd8342c, 0xf3ead6, 0x2f6fd6, 0xf3ead6, 0xe2b21e, 0xf3ead6, 0xd8342c]
    # foot resting on the ground (it stands like a boot), toe points forward
    n.sphere(0.2, (0, 0.13, 0.08), 0xd8342c, scale=(1.0, 0.65, 1.45), seg=10, rings=7, wonk=0.006)
    n.sphere(0.13, (0, 0.11, 0.34), 0xf3ead6, scale=(1.05, 0.75, 0.8), seg=9, rings=6)  # toe
    n.sphere(0.13, (0, 0.14, -0.14), 0xf3ead6, scale=(1.1, 0.9, 0.8), seg=9, rings=6)  # heel
    # the tall tube, striped, slightly wobbly
    y = 0.2
    for i, c in enumerate(stripes):
        h = 0.1
        n.cyl(0.19 - i * 0.004, h + 0.004, (0, y + h / 2, -0.06 - i * 0.008), c, seg=12, wonk=0.004)
        y += h
    # ribbed cuff
    for k in range(4):
        n.torus(0.175, 0.018, (0, y + 0.02 + k * 0.03, -0.12), 0xf6f0e2, seg=14, tseg=4)
    n.cyl(0.16, 0.02, (0, y + 0.12, -0.12), 0x5a3a2a, seg=12)  # dark opening at the top
    # button eyes (mismatched!)
    for x, col, r in ((-0.075, 0x2f6fd6, 0.052), (0.08, 0xd8342c, 0.042)):
        n.cyl(r, 0.025, (x, 0.72, 0.13), col, rot=(R90 - 0.1, 0, 0), seg=12)
        for hx, hy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
            n.cyl(0.008, 0.028, (x + hx * r * 0.3, 0.72 + hy * r * 0.3, 0.135), BLACK, rot=(R90 - 0.1, 0, 0), seg=5)
    # thick eyebrows of yarn
    n.cyl(0.012, 0.11, (-0.075, 0.8, 0.14), 0x6a4a2a, rot=(0, 0, R90 - 0.35), seg=5)
    n.cyl(0.012, 0.1, (0.08, 0.79, 0.14), 0x6a4a2a, rot=(0, 0, R90 + 0.25), seg=5)
    # stitched wobbly mouth
    for i in range(7):
        x = -0.07 + i * 0.023
        yy = 0.58 + 0.012 * math.sin(i * 1.3)
        n.box((0.02, 0.006, 0.01), (x, yy, 0.175), BLACK, rot=(0, 0, 0.6 if i % 2 else -0.6))
    # darned patch with cross stitches
    n.box((0.12, 0.12, 0.01), (-0.15, 0.45, 0.08), 0x7a9a4a, rot=(0, -0.9, 0.15))
    for k in range(3):
        n.box((0.1, 0.008, 0.012), (-0.155, 0.41 + k * 0.04, 0.085), 0xf2e6c8, rot=(0, -0.9, 0.7))
    # knitting-needle staff with a yarn ball on top
    n.cylb(0.012, 1.05, (0.3, 0.0, 0.05), 0xb9c0c8, seg=6)
    n.sphere(0.07, (0.3, 1.08, 0.05), 0x9b44c9, ico=1, wonk=0.006)
    n.sphere(0.025, (0.3, 1.16, 0.05), 0xfff4a0, seg=6, rings=4, kind=MAT_EMIT)
    # magic sparkles
    rng = random.Random(4)
    for i in range(9):
        a = rng.random() * 6.28
        r = rng.uniform(0.3, 0.45)
        n.sphere(0.012, (math.cos(a) * r, rng.uniform(0.3, 1.1), math.sin(a) * r), 0xfff0a0, seg=5, rings=3, kind=MAT_EMIT)
    # tuna-can stage
    n.cylb(0.32, 0.07, (0, -0.07, 0), 0xb9c0c8, seg=16)
    n.cyl(0.33, 0.015, (0, -0.005, 0), 0x8e969f, seg=16)
    n.box((0.3, 0.05, 0.004), (0, -0.035, 0.32), 0x2f6fd6)  # the tuna label
    n.col_boxb((0.6, 1.0, 0.55), (0, -0.07, 0))
    n.marker('ANCHOR', 'talk', (0, 0, 0.7), size=(1, 1, 1), scale_size=False)
    marker_light(n, 'glow', (0, 0.9, 0.4), 0xffd8a0, 2.5, 0.9)
    return n


# ------------------------------------------------------------------ the village (hub)

def thimble_house(n, x, z, s=1.0, color=0xb9c0c8):
    """An upside-down thimble with a door and a window."""
    n.lathe([(0.16 * s, 0.0), (0.155 * s, 0.2 * s), (0.13 * s, 0.3 * s), (0.08 * s, 0.34 * s), (0.0, 0.35 * s)], (x, 0, z), color, seg=12)
    for k in range(4):
        n.torus(0.157 * s - k * 0.004, 0.006, (x, 0.04 * s + k * 0.05 * s, z), 0x8e969f, seg=12, tseg=3)
    n.boxb((0.09 * s, 0.14 * s, 0.02), (x, 0, z + 0.155 * s), 0x7a4a2a)
    n.cyl(0.03 * s, 0.01, (x + 0.07 * s, 0.22 * s, z + 0.14 * s), 0xffe39a, rot=(R90, 0, 0), seg=8, kind=MAT_EMIT)
    n.col_boxb((0.3 * s, 0.34 * s, 0.3 * s), (x, 0, z))


def village():
    """The gnomes' home under the porch: 7 x 5 m of cosy junk-built houses. Origin at ground centre."""
    n = mk('village')
    W, D, H = 7.0, 5.0, 0.85
    rng = random.Random(8)
    # ground: dirt with moss patches
    n.boxb((W, 0.1, D), (0, -0.1, 0), DIRT)
    n.col_boxb((W + 2, 0.4, D + 2), (0, -0.4, 0))
    for i in range(14):
        x, z = rng.uniform(-3.2, 3.2), rng.uniform(-2.2, 2.2)
        n.cyl(rng.uniform(0.2, 0.5), 0.01, (x, 0.003, z), MOSS, seg=7, wonk=0.03)
    # porch deck overhead (planks with moonlit gaps)
    x = -W / 2
    while x < W / 2:
        n.box((0.14, 0.04, D), (x + 0.07, H + 0.02, 0), PLANK, wonk=0.003)
        x += 0.16
    n.col_boxb((W, 0.05, D), (0, H, 0))
    for sx in (-1, 1):
        for sz in (-1, 1):
            n.solidb((0.1, H, 0.1), (sx * (W / 2 - 0.2), 0, sz * (D / 2 - 0.2)), WOOD_D)
    n.solidb((W, 0.08, 0.12), (0, H - 0.08, D / 2 - 0.1), WOOD_D)
    # back: stone foundation of the house
    n.solidb((W, H, 0.2), (0, 0, -D / 2), STONE)
    for i in range(12):
        n.box((0.5, 0.012, 0.01), (-W / 2 + 0.3 + i * 0.58, 0.2 + (i % 2) * 0.2, -D / 2 + 0.105), 0x6c6964)
    # side lattice skirts
    for sx in (-1, 1):
        n.col_boxb((0.1, H, D), (sx * W / 2, 0, 0))
        for k in range(10):
            z = -D / 2 + 0.25 + k * 0.5
            n.box((0.02, 0.8, 0.03), (sx * W / 2, H / 2, z), WOOD_L, rot=(0.78, 0, 0))
            n.box((0.02, 0.8, 0.03), (sx * W / 2, H / 2, z), WOOD_L, rot=(-0.78, 0, 0))
    # front lattice with the opening in the middle (the sock tunnel goes there)
    for sx in (-1, 1):
        n.col_boxb((W / 2 - 0.6, H, 0.1), (sx * (W / 4 + 0.3), 0, D / 2))
        for k in range(6):
            xx = sx * (0.8 + k * 0.45)
            n.box((0.03, 0.8, 0.02), (xx, H / 2, D / 2), WOOD_L, rot=(0, 0, 0.78))
            n.box((0.03, 0.8, 0.02), (xx, H / 2, D / 2), WOOD_L, rot=(0, 0, -0.78))
    # thimble houses & a matchbox house
    thimble_house(n, 2.3, -1.6, 1.0, 0xb9c0c8)
    thimble_house(n, 2.9, 0.2, 0.85, GOLD)
    thimble_house(n, -2.9, 0.9, 0.9, 0xc9855a)
    n.solidb((0.4, 0.14, 0.25), (-1.4, 0, 1.7), 0xd9453a)  # matchbox house
    n.boxb((0.38, 0.1, 0.26), (-1.4, 0.14, 1.7), 0x3a6ac0)
    n.boxb((0.08, 0.1, 0.02), (-1.4, 0, 1.83), 0x5a3a2a)
    # spool table with bottle-cap stools
    n.cylb(0.12, 0.16, (1.0, 0, 0.9), WOOD_L, seg=10)
    n.cylb(0.1, 0.12, (1.0, 0.02, 0.9), 0xd8342c, seg=10)  # thread on the spool
    n.cylb(0.16, 0.02, (1.0, 0.16, 0.9), WOOD_L, seg=10)
    n.col_boxb((0.3, 0.18, 0.3), (1.0, 0, 0.9))
    for a in (0.3, 2.4, 4.4):
        cx, cz = 1.0 + math.cos(a) * 0.3, 0.9 + math.sin(a) * 0.3
        n.cylb(0.06, 0.03, (cx, 0, cz), [RED, 0x2f6fd6, GOLD][int(a) % 3], seg=12)
    n.cyl(0.02, 0.03, (1.0, 0.195, 0.9), 0xf3ead6, seg=8)  # tiny cup (a bead)
    # button path from the tunnel to the Great Sock
    for i in range(9):
        z = 2.1 - i * 0.42
        col = YARN_COLS[i % len(YARN_COLS)]
        n.cyl(0.07, 0.012, (0.12 * math.sin(i * 0.9), 0.006, z), col, seg=10)
        for hx, hz in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
            n.cyl(0.008, 0.014, (0.12 * math.sin(i * 0.9) + hx * 0.02, 0.006, z + hz * 0.02), BLACK, seg=4)
    # washing line with drying socks between two posts
    for x in (-2.8, -0.9):
        n.solidb((0.03, 0.45, 0.03), (x, 0, -1.9), WOOD_M)
    n.cyl(0.004, 1.9, (-1.85, 0.43, -1.9), 0xf2f0e8, rot=(0, 0, R90), seg=4)
    for i in range(5):
        x = -2.6 + i * 0.36
        c = YARN_COLS[(i * 2) % len(YARN_COLS)]
        n.boxb((0.05, 0.12, 0.02), (x, 0.3, -1.9), c)
        n.boxb((0.07, 0.03, 0.02), (x + 0.015, 0.29, -1.9), c)
        n.box((0.012, 0.03, 0.01), (x, 0.43, -1.9), WOOD_L)  # peg
    # candle-stub lanterns
    for (x, z) in ((-1.7, -0.6), (1.8, -0.4), (-0.6, 1.2), (2.4, 1.5)):
        n.cylb(0.035, 0.07, (x, 0, z), CREAM, seg=8)
        n.cone(0.014, 0.035, (x, 0.09, z), 0xffc040, seg=5, kind=MAT_EMIT)
        marker_light(n, 'candle%d' % int((x + 3) * 10), (x, 0.14, z), 0xffb060, 2.4, 1.1)
    # mushrooms & pebbles
    for i in range(10):
        x, z = rng.uniform(-3.2, 3.2), rng.uniform(-2.2, 2.2)
        if abs(x) < 0.5 and z > -1.5:
            continue
        s = rng.uniform(0.5, 1.0)
        n.cylb(0.015 * s, 0.07 * s, (x, 0, z), 0xf3ead6, seg=5)
        n.sphere(0.04 * s, (x, 0.07 * s, z), rng.choice([0xd8342c, 0xc98a47, 0xe2b21e]), scale=(1, 0.55, 1), seg=7, rings=4)
    n.marker('SPAWN', 'center', (0, 0.05, 1.2), size=(1, 1, 1), scale_size=False)
    marker_light(n, 'moon', (0, H - 0.05, 0), 0x8fa8ff, 6, 0.5)
    return n


def knitting_corner():
    n = mk('knittingCorner')
    # tomato pincushion with pins
    n.sphere(0.16, (0, 0.13, 0), 0xd8342c, scale=(1, 0.8, 1), seg=10, rings=7)
    for k in range(5):
        a = k * 1.25
        n.box((0.05, 0.02, 0.02), (math.cos(a) * 0.04, 0.24, math.sin(a) * 0.04), 0x3aa845, rot=(0, -a, 0.3))
    rng = random.Random(2)
    for k in range(8):
        a, e = rng.uniform(0, 6.28), rng.uniform(0.3, 1.2)
        d = (math.cos(a) * math.cos(e), math.sin(e), math.sin(a) * math.cos(e))
        base = (d[0] * 0.14, 0.13 + d[1] * 0.11, d[2] * 0.14)
        n.cyl(0.003, 0.12, (base[0] + d[0] * 0.05, base[1] + d[1] * 0.05, base[2] + d[2] * 0.05), 0xd0d4da, rot=(math.atan2(d[2], d[1]) if False else -e + R90, 0, 0), seg=4)
        n.sphere(0.012, (base[0] + d[0] * 0.1, base[1] + d[1] * 0.1, base[2] + d[2] * 0.1), YARN_COLS[k % len(YARN_COLS)], seg=5, rings=3)
    n.col_boxb((0.34, 0.26, 0.34))
    # yarn balls with crossed knitting needles
    for i, (x, z) in enumerate(((0.35, 0.1), (0.45, -0.12), (0.28, -0.2))):
        n.sphere(0.07, (x, 0.07, z), YARN_COLS[i + 2], ico=1, wonk=0.004)
    n.cyl(0.006, 0.35, (0.38, 0.12, 0.0), 0xb9c0c8, rot=(0.6, 0, 0.3), seg=4)
    n.cyl(0.006, 0.35, (0.38, 0.12, 0.0), 0xb9c0c8, rot=(-0.6, 0, -0.3), seg=4)
    # a spinning wheel made of a button and a spool
    n.torus(0.14, 0.012, (-0.35, 0.22, 0), WOOD_M, rot=(0, 0, R90), seg=14, tseg=4)
    for k in range(6):
        n.box((0.008, 0.26, 0.008), (-0.35, 0.22, 0), WOOD_L, rot=(k * 0.52, 0, 0))
    n.cylb(0.015, 0.22, (-0.35, 0, 0.1), WOOD_D, seg=5)
    n.cylb(0.015, 0.22, (-0.35, 0, -0.1), WOOD_D, seg=5)
    n.boxb((0.3, 0.02, 0.26), (-0.35, 0, 0), WOOD_D)
    # sign
    n.cylb(0.012, 0.4, (0.1, 0, 0.3), WOOD_D, seg=4)
    n.box((0.28, 0.1, 0.015), (0.1, 0.38, 0.3), WOOD_L, rot=(0, 0.2, 0))
    n.marker('ANCHOR', 'use', (0, 0, 0.55), size=(1, 1, 1), scale_size=False)
    return n


def sock_tunnel():
    """A giant sock lying on its side: crawl in to set off for the night."""
    n = mk('sockTunnel')
    L, R = 1.2, 0.3
    cols = [0x2f6fd6, 0xf3ead6, 0x3aa845, 0xf3ead6]
    for i in range(6):
        z = -L / 2 + (i + 0.5) * L / 6
        n.cyl(R, L / 6 + 0.005, (0, R, z), cols[i % 4], rot=(R90, 0, 0), seg=14, wonk=0.006)
    for k in range(3):
        n.torus(R + 0.005, 0.02, (0, R, L / 2 + 0.02 + k * 0.03), 0xf6f0e2, rot=(R90, 0, 0), seg=14, tseg=4)
    n.sphere(R, (0, R, -L / 2), 0x2f6fd6, scale=(1, 1, 0.8), seg=14, rings=7)  # toe end
    n.cyl(R * 0.8, 0.01, (0, R, -L / 2 + 0.02), 0x9a6ae0, rot=(R90, 0, 0), seg=14, kind=MAT_EMIT)  # the glow deep inside
    n.cyl(R * 0.45, 0.012, (0, R, -L / 2 + 0.03), 0xe0c8ff, rot=(R90, 0, 0), seg=12, kind=MAT_EMIT)
    n.col_boxb((R * 1.7, 0.03, L), (0, -0.02, 0))
    n.marker('ZONE', 'portal', (0, R, -0.1), size=(R * 1.6, R * 2, L * 0.9))
    marker_light(n, 'glow', (0, R, 0), 0xa080ff, 3, 1.5)
    return n


# ------------------------------------------------------------------ house additions

def jar_shelf():
    """Grandpa's pickle shelf. Caught gnomes get jarred up here."""
    n = mk('jarShelf')
    W = 1.0
    n.solidb((W, 0.03, 0.26), (0, 0, 0), WOOD_M)
    for x in (-W / 2 + 0.1, W / 2 - 0.1):
        n.box((0.03, 0.14, 0.2), (x, -0.07, -0.02), 0x3d4148)
    for i in range(3):
        x = -0.34 + i * 0.26
        jar = Node('jar%d' % i, n, (x, 0.03, 0))
        jar.lathe([(0.0, 0.0), (0.1, 0.0), (0.105, 0.02), (0.105, 0.25), (0.085, 0.29), (0.085, 0.3), (0.0, 0.3)], (0, 0, 0), 0xcfe8f2, seg=12, kind=MAT_GLASS)
        lid = mech(n, 'lid%d' % i, 'jar', 'slide', (x, 0.33, 0), 'y', 0.14, handle=(0, 0.04, 0))
        lid.cyl(0.095, 0.035, (0, 0, 0), BRASS, seg=12)
        lid.torus(0.095, 0.008, (0, 0.0, 0), 0xa88a3a, seg=12, tseg=3)
        n.marker('ANCHOR', 'jar%d' % i, (x, 0.04, 0), size=(1, 1, 1), scale_size=False)
    # a jar of actual pickles for decoration
    n.cyl(0.07, 0.2, (0.44, 0.13, -0.02), 0xcfe8f2, seg=10, kind=MAT_GLASS)
    for k in range(4):
        n.cyl(0.018, 0.14, (0.44 + (k % 2) * 0.03 - 0.015, 0.12, -0.02 + (k // 2) * 0.03 - 0.015), 0x5a8a2a, seg=6)
    n.cyl(0.072, 0.025, (0.44, 0.24, -0.02), RED, seg=10)
    n.marker('ANCHOR', 'free', (0, -1.3, 0.5), size=(1, 1, 1), scale_size=False)  # where freed gnomes drop to
    return n


def parrot_cage():
    """Kesha the parrot. Snitches loudly."""
    n = mk('parrotCage')
    n.cylb(0.2, 0.03, (0, 0, 0), 0x3d4148, seg=10)
    n.cylb(0.015, 1.2, (0, 0.03, 0), 0x3d4148, seg=6)
    n.col_boxb((0.06, 1.2, 0.06), (0, 0.03, 0))
    n.col_boxb((0.3, 0.03, 0.3))
    top = 1.23
    n.cylb(0.2, 0.03, (0, top, 0), BRASS, seg=14)  # tray
    for k in range(16):
        a = k / 16 * 6.28
        n.cylb(0.004, 0.34, (math.cos(a) * 0.19, top + 0.03, math.sin(a) * 0.19), BRASS, seg=4)
    n.lathe([(0.19, 0.0), (0.17, 0.08), (0.1, 0.14), (0.0, 0.16)], (0, top + 0.37, 0), BRASS, seg=14)
    n.torus(0.04, 0.008, (0, top + 0.56, 0), BRASS, rot=(R90, 0, 0), seg=8, tseg=3)
    n.col_boxb((0.42, 0.55, 0.42), (0, top, 0))
    n.cyl(0.006, 0.36, (0, top + 0.16, 0), WOOD_M, rot=(0, 0, R90), seg=4)  # perch
    bird = Node('parrot', n, (0, top + 0.17, 0))
    bird.sphere(0.06, (0, 0.05, 0), 0x3aa845, scale=(0.9, 1.2, 0.9), seg=8, rings=6)
    bird.sphere(0.045, (0, 0.14, 0.02), 0xd8342c, seg=8, rings=6)
    bird.cone(0.022, 0.05, (0, 0.13, 0.07), 0xf2cf3c, rot=(R90 + 0.4, 0, 0), seg=5)
    for x in (-0.028, 0.028):
        bird.sphere(0.012, (x, 0.16, 0.05), 0xffffff, seg=5, rings=3)
        bird.sphere(0.006, (x, 0.16, 0.06), BLACK, seg=4, rings=3)
    bird.box((0.04, 0.12, 0.012), (0, -0.05, -0.05), 0x2f6fd6, rot=(0.5, 0, 0))  # tail
    bird.box((0.012, 0.07, 0.05), (-0.055, 0.05, 0), 0x2b8a3a, rot=(0, 0, 0.2))
    bird.box((0.012, 0.07, 0.05), (0.055, 0.05, 0), 0x2b8a3a, rot=(0, 0, -0.2))
    n.marker('ZONE', 'cage', (0, top + 0.18, 0), size=(0.36, 0.3, 0.36))
    n.marker('ZONE', 'cageTop', (0, top + 0.5, 0), size=(0.6, 0.35, 0.6))
    n.marker('ANCHOR', 'eye', (0, top + 0.33, 0.06), size=(1, 1, 1), scale_size=False)
    return n


def mousetrap():
    n = mk('mousetrap')
    n.boxb((0.1, 0.01, 0.05), (0, 0, 0), WOOD_L)
    n.box((0.02, 0.004, 0.02), (0.03, 0.012, 0), GOLD)  # bait plate with cheese
    n.box((0.014, 0.012, 0.012), (0.03, 0.02, 0), 0xf2cf3c)
    n.cyl(0.008, 0.04, (-0.01, 0.014, 0), 0x8e969f, rot=(R90, 0, 0), seg=6)  # spring
    snap = mech(n, 'snap', 'mousetrap', 'hinge', (-0.01, 0.014, 0), 'z', math.pi * 0.95, handle=(0, 0, 0))
    snap.box((0.06, 0.004, 0.004), (0.03, 0, 0.02), 0xb9c0c8)
    snap.box((0.06, 0.004, 0.004), (0.03, 0, -0.02), 0xb9c0c8)
    snap.box((0.004, 0.004, 0.044), (0.06, 0, 0), 0xb9c0c8)
    n.marker('ZONE', 'trap', (0, 0.05, 0), size=(0.12, 0.1, 0.07))
    n.props['noNav'] = 1
    return n


def creaky_board():
    n = mk('creakyBoard')
    for k in range(3):
        n.boxb((0.8, 0.004, 0.13), (0, 0.001, -0.14 + k * 0.14), 0x6a4630, wonk=0.002)
        n.cyl(0.006, 0.006, (-0.35, 0.004, -0.14 + k * 0.14), 0x3a3a3a, seg=5)
        n.cyl(0.006, 0.006, (0.35, 0.004, -0.14 + k * 0.14), 0x3a3a3a, seg=5)
    n.marker('ZONE', 'creak', (0, 0.1, 0), size=(0.8, 0.2, 0.42))
    n.props['noNav'] = 1
    return n


def porch():
    """Front porch against the south wall. The gang's village is underneath."""
    n = mk('porch')
    W, D, H = 3.6, 1.5, 0.62
    x = -W / 2
    while x < W / 2 - 0.01:
        n.boxb((0.14, 0.04, D), (x + 0.07, H - 0.04, D / 2), PLANK, wonk=0.003)
        x += 0.15
    n.col_boxb((W, 0.05, D), (0, H - 0.05, D / 2))
    for sx in (-1, 1):
        n.solidb((0.12, H, 0.12), (sx * (W / 2 - 0.06), 0, D - 0.06), WOOD_D)
    # steps in the middle
    for i in range(2):
        n.solidb((1.0, 0.2, 0.3), (0, 0.2 * i, D + 0.3 - i * 0.3 + 0.15 - 0.15), WOOD_M)
    # lattice skirt with a dark gnome-sized gap at the left
    for sx in (-1, 1):
        n.col_boxb((W / 2 - 0.55, H - 0.05, 0.05), (sx * (W / 4 + 0.27), 0, D))
    n.boxb((0.5, 0.45, 0.02), (-1.25, 0, D - 0.3), 0x15120f)  # darkness under the porch
    for k in range(9):
        xx = -W / 2 + 0.2 + k * 0.4
        if -1.55 < xx < -0.95:
            continue
        n.box((0.03, 0.7, 0.02), (xx, H / 2, D + 0.01), WOOD_L, rot=(0, 0, 0.78))
        n.box((0.03, 0.7, 0.02), (xx, H / 2, D + 0.01), WOOD_L, rot=(0, 0, -0.78))
    # a little sign over the gap
    n.box((0.22, 0.07, 0.01), (-1.25, 0.47, D + 0.03), 0xc89560, rot=(0, 0, 0.08))
    n.box((0.12, 0.02, 0.012), (-1.25, 0.47, D + 0.035), 0xd8342c, rot=(0, 0, 0.08))
    # railing
    for sx in (-1, 1):
        n.solidb((0.05, 0.5, 0.05), (sx * (W / 2 - 0.06), H, D - 0.06), WOOD_D)
    n.solidb((W, 0.05, 0.06), (0, H + 0.48, D - 0.06), WOOD_D)
    n.marker('ANCHOR', 'gap', (-1.25, 0, D + 0.25), size=(1, 1, 1), scale_size=False)
    return n


def yarn_basket():
    """Fallen gnomes are re-knitted here: bring their hats!"""
    n = mk('yarnBasket')
    c = 0xb8894e
    n.cylb(0.28, 0.25, (0, 0, 0), c, seg=12, r2=0.32)
    n.cyl(0.3, 0.03, (0, 0.25, 0), 0x9a6f3a, seg=12)
    for k in range(4):
        n.torus(0.29 + k * 0.01, 0.008, (0, 0.05 + k * 0.05, 0), 0x9a6f3a, seg=12, tseg=3)
    for i, (x, z) in enumerate(((0.1, 0.05), (-0.1, 0.08), (0.02, -0.12), (-0.14, -0.08), (0.15, -0.1))):
        n.sphere(0.09, (x, 0.24, z), YARN_COLS[i], ico=1, wonk=0.005)
    n.cyl(0.006, 0.4, (0.08, 0.35, 0.0), 0xb9c0c8, rot=(0.3, 0, 0.4), seg=4)
    n.cyl(0.006, 0.4, (0.06, 0.35, 0.02), 0xb9c0c8, rot=(-0.2, 0, 0.5), seg=4)
    n.col_boxb((0.6, 0.25, 0.6))
    n.marker('ZONE', 'revive', (0, 0.45, 0), size=(0.9, 0.6, 0.9))
    n.marker('ANCHOR', 'revive', (0, 0, 0.55), size=(1, 1, 1), scale_size=False)
    # candle in a jar lid
    n.cylb(0.05, 0.015, (0.4, 0, 0.2), BRASS, seg=8)
    n.cylb(0.025, 0.06, (0.4, 0.015, 0.2), CREAM, seg=8)
    n.cone(0.012, 0.03, (0.4, 0.09, 0.2), 0xffc040, seg=5, kind=MAT_EMIT)
    marker_light(n, 'candle', (0.4, 0.14, 0.2), 0xffb060, 3.0, 1.1)
    return n


def stash_cart():
    """A sardine tin on thread-spool wheels. Loot put in here goes to the village."""
    n = mk('stashBasket')
    W, H, D = 0.7, 0.18, 0.45
    tin = 0xb9c0c8
    n.solidb((W, 0.02, D), (0, 0.12, 0), tin)
    for (sz, pos) in (((W, H, 0.02), (0, 0.12, -D / 2)), ((W, H, 0.02), (0, 0.12, D / 2)), ((0.02, H - 0.004, D - 0.024), (-W / 2, 0.122, 0)), ((0.02, H - 0.004, D - 0.024), (W / 2, 0.122, 0))):
        n.solidb(sz, pos, tin)
    n.box((W * 0.7, 0.1, 0.004), (0, 0.21, D / 2 + 0.012), 0x2f6fd6)  # label
    n.box((W * 0.3, 0.04, 0.005), (0, 0.21, D / 2 + 0.015), 0xf2cf3c)
    n.torus(0.1, 0.012, (W / 2 + 0.08, 0.3, 0), tin, rot=(R90, 0, 0), seg=10, tseg=3)  # peeled lid roll
    for sx in (-1, 1):
        for sz in (-1, 1):
            p = (sx * (W / 2 - 0.1), 0.07, sz * (D / 2 + 0.03))
            n.cyl(0.07, 0.05, p, WOOD_L, rot=(R90, 0, 0), seg=10)
            n.cyl(0.055, 0.052, p, YARN_COLS[(sx + 1) + (sz + 1) // 2], rot=(R90, 0, 0), seg=10)
    n.cyl(0.006, 0.6, (-W / 2 - 0.25, 0.2, 0), 0xf2f0e8, rot=(0, 0, R90 - 0.3), seg=4)  # string handle
    n.marker('ZONE', 'stash', (0, 0.3, 0), size=(W - 0.05, 0.4, D - 0.05))
    return n


# ------------------------------------------------------------------ items (loose props)

def keys():
    n = mk('keys')
    n.torus(0.022, 0.004, (0, 0, 0), 0xb9c0c8, seg=10, tseg=3)
    n.box((0.07, 0.006, 0.018), (0.05, 0, 0.01), GOLD, rot=(0, 0.3, 0))
    n.box((0.02, 0.008, 0.008), (0.08, 0, 0.024), GOLD, rot=(0, 0.3, 0))
    n.box((0.06, 0.006, 0.016), (0.04, 0, -0.02), 0xb9c0c8, rot=(0, -0.4, 0))
    n.sphere(0.018, (-0.035, 0, 0.0), 0xef7a24, scale=(1.6, 0.8, 0.9), seg=6, rings=4)  # rubber fish keychain
    n.cone(0.012, 0.02, (-0.068, 0, 0), 0xef7a24, rot=(0, 0, R90), seg=4)
    return n


def towel():
    n = mk('towel')
    n.box((0.25, 0.04, 0.18), (0, 0, 0), 0x4fa0c8, bevel=0.01, wonk=0.002)
    for k in range(3):
        n.box((0.252, 0.042, 0.02), (0, 0, -0.06 + k * 0.06), WHITE)
    return n


def teacup():
    n = mk('teacup')
    n.cyl(0.07, 0.01, (0, -0.03, 0), WHITE, seg=12)
    n.lathe([(0.0, -0.025), (0.03, -0.025), (0.045, 0.02), (0.042, 0.02), (0.0, -0.018)], (0, 0, 0), WHITE, seg=10)
    n.torus(0.015, 0.005, (0.05, 0.0, 0), WHITE, rot=(R90, 0, 0), seg=8, tseg=3)
    n.cyl(0.04, 0.003, (0, 0.012, 0), 0x8a5a2a, seg=10)
    n.box((0.03, 0.01, 0.002), (0, 0.0, 0.043), 0x3d6fb0)
    return n


def gnome_hat():
    """A fallen gnome's hat (tinted with the owner's colour at runtime)."""
    n = Node('gnomeHat', k=1.0)
    n.props['kind'] = 'gnomeHat'
    n.cyl(0.185, 0.07, (0, -0.1, 0), 0xd8342c, seg=10, r2=0.178, kind=MAT_TINT)
    n.cyl(0.178, 0.26, (0, 0.065, 0), 0xd8342c, seg=10, r2=0.115, kind=MAT_TINT)
    n.cyl(0.115, 0.2, (0, 0.2, -0.1), 0xd8342c, seg=9, r2=0.065, rot=(-0.6, 0, 0), kind=MAT_TINT)
    n.cone(0.065, 0.2, (0, 0.25, -0.26), 0xd8342c, rot=(-1.3, 0, 0), seg=8, kind=MAT_TINT)
    n.sphere(0.04, (0, 0.24, -0.36), 0xfff4d8, seg=7, rings=5)
    n.sphere(0.03, (0, 0.0, 0), 0xfff4a0, seg=5, rings=3, kind=MAT_EMIT)  # a faint glow so friends find it
    return n


def sleep_dust():
    n = mk('sleepDust')
    n.sphere(0.04, (0, 0, 0), 0x7d4c9e, scale=(1, 1.1, 1), seg=8, rings=5, wonk=0.002)
    n.cyl(0.015, 0.03, (0, 0.045, 0), 0x7d4c9e, seg=6)
    n.torus(0.016, 0.004, (0, 0.04, 0), 0xf2cf3c, seg=8, tseg=3)
    for k in range(5):
        a = k * 1.3
        n.sphere(0.006, (math.cos(a) * 0.035, 0.02 * math.sin(k), math.sin(a) * 0.035), 0xfff0a0, seg=4, rings=3, kind=MAT_EMIT)
    return n


MODELS = {
    'greatSock': great_sock,
    'village': village,
    'knittingCorner': knitting_corner,
    'sockTunnel': sock_tunnel,
    'jarShelf': jar_shelf,
    'parrotCage': parrot_cage,
    'mousetrap': mousetrap,
    'creakyBoard': creaky_board,
    'porch': porch,
    'yarnBasket': yarn_basket,
    'stashBasket': stash_cart,
    'keys': keys,
    'towel': towel,
    'teacup': teacup,
    'gnomeHat': gnome_hat,
    'sleepDust': sleep_dust,
}
