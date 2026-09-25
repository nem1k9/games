"""Models specific to "The Sock Gang": the Great Sock, the village under the porch, pickle jars,
Kesha the parrot, mousetraps, the porch, the yarn basket, the sardine-tin cart and a few items.
Authored in METRES (k = HS) unless noted."""
import math
import random

from gnomelib.core import (HS, MAT_EMIT, MAT_FABRIC, MAT_FUR, MAT_GLASS, MAT_GLOSSY, MAT_HAIR, MAT_KNIT, MAT_LEATHER,
                           MAT_METAL, MAT_SKIN, MAT_STONE, MAT_TINT, MAT_WOOD,
                           Node)

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
    """The Great Sock: ancient, wise, slightly smelly. Gives out the nightly list of pranks.

    One knitted tube (stripes, ribbed cuff, contrasting heel and toe) standing like a boot on a
    tuna-can stage. The 'body' node sways (GreatSockIdle); the can stays put."""
    n = mk('greatSock')
    n.detail = 2.6  # chunky stitches: this is a giant sock
    body = Node('body', n, (0, 0.0, 0), surface=MAT_KNIT)
    RED_Y, CREAM_Y, BLUE_Y, GOLD_Y, GREEN_Y = 0xd8342c, 0xf3ead6, 0x2f6fd6, 0xe2b21e, 0x4f9a4a
    stripes = [BLUE_Y, CREAM_Y, GOLD_Y, CREAM_Y, RED_Y, CREAM_Y, GREEN_Y, CREAM_Y]

    def paint(t, a):
        if t < 0.13:
            return CREAM_Y  # ribbed cuff
        if t < 0.165:
            return RED_Y
        if t < 0.43:
            return CREAM_Y  # plain band for the face
        if 0.63 < t < 0.8 and math.cos(a) < 0.35:
            return RED_Y  # heel patch (back side of the bend)
        if t > 0.86:
            return RED_Y  # toe
        return stripes[int((t - 0.43) / 0.05) % len(stripes)]

    path = [(0, 1.08, -0.1), (0, 0.88, -0.1), (0, 0.66, -0.095), (0, 0.44, -0.08), (0, 0.25, -0.05),
            (0, 0.15, 0.05), (0, 0.13, 0.2), (0, 0.13, 0.33)]
    radii = [0.2, 0.198, 0.195, 0.19, 0.18, 0.16, 0.145, 0.125]
    body.sweep(path, radii, CREAM_Y, seg=28, steps=7, start='open', end='dome', paint=paint,
               ribs=lambda t: 0.035 if t < 0.13 else 0.0, rim=(0.035, 0.3, 0x4a2e22))
    # face on the front of the leg
    fz = 0.1  # front surface of the tube (tube axis z = -0.1, radius ~0.2)
    for x, col, r, y in ((-0.085, BLUE_Y, 0.068, 0.74), (0.088, 0xd8342c, 0.054, 0.73)):
        zz = fz - 0.2 + math.sqrt(max(0.0, 0.2 ** 2 - x * x)) + 0.004
        yaw = math.asin(x / 0.2)
        body.lathe([(0.0, 0.0), (r, 0.0), (r * 1.02, 0.012), (r * 0.92, 0.024), (0.0, 0.026)], (x, y, zz),
                   col, rot=(R90, yaw, 0), seg=18, kind=MAT_GLOSSY)
        for hx, hy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
            body.cyl(r * 0.13, 0.01, (x + hx * r * 0.32 * math.cos(yaw), y + hy * r * 0.32, zz + 0.024), 0x1d1e22,
                     rot=(R90, yaw, 0), seg=8, kind=MAT_GLOSSY)
        # thread cross through the holes
        body.box((r * 0.9, 0.006, 0.006), (x, y, zz + 0.029), 0xf3ead6, rot=(0, yaw, 0.785))
        body.box((r * 0.9, 0.006, 0.006), (x, y, zz + 0.029), 0xf3ead6, rot=(0, yaw, -0.785))
    # bushy yarn eyebrows
    body.sweep([(-0.15, 0.82, 0.058), (-0.1, 0.852, 0.09), (-0.04, 0.84, 0.1)], [0.014, 0.019, 0.013],
               0x6a4a2a, kind=MAT_HAIR, seg=8, steps=4)
    body.sweep([(0.045, 0.828, 0.1), (0.095, 0.842, 0.093), (0.145, 0.82, 0.062)], [0.013, 0.018, 0.014],
               0x6a4a2a, kind=MAT_HAIR, seg=8, steps=4)
    # stitched wobbly smile: a thick red yarn with little cross stitches
    smile = [(-0.085, 0.605, 0.083), (-0.04, 0.585, 0.098), (0.01, 0.585, 0.1), (0.055, 0.6, 0.093), (0.09, 0.62, 0.077)]
    body.sweep(smile, [0.009] * len(smile), 0x8a2020, kind=MAT_KNIT, seg=8, steps=4)
    for i in range(1, 4):
        x, y, z = smile[i]
        body.box((0.006, 0.04, 0.006), (x, y, z + 0.006), 0xf3ead6, rot=(0, 0, 0.5))
    # darned patch with cross stitches on the side
    body.lathe([(0.0, 0.0), (0.07, 0.0), (0.075, 0.008), (0.0, 0.012)], (-0.17, 0.45, -0.02), 0x7a9a4a,
               rot=(0, -1.1, R90), seg=10, kind=MAT_FABRIC)
    for kx in range(3):
        body.box((0.09, 0.006, 0.006), (-0.182, 0.42 + kx * 0.03, 0.0), 0xf2e6c8, rot=(0, -1.1, 0.6))
    # knitting-needle staff standing beside the sock, with a yarn ball on top
    body.sweep([(0.4, 0.0, -0.06), (0.4, 1.1, -0.06)], [0.013, 0.011], 0xb9c0c8, kind=MAT_METAL, seg=10,
               steps=2, start='flat', end='dome')
    body.blob(0.075, (0.4, 1.15, -0.06), 0x9b44c9, kind=MAT_KNIT, detail=3)
    body.sweep([(0.4, 1.15, -0.135), (0.37, 1.22, -0.1), (0.4, 1.23, -0.02), (0.44, 1.18, 0.0)], [0.012] * 4,
               0x9b44c9, kind=MAT_KNIT, seg=8, steps=4)
    body.blob(0.022, (0.4, 1.25, -0.06), 0xfff4a0, kind=MAT_EMIT, detail=2)
    # magic sparkles
    rng = random.Random(4)
    for i in range(7):
        a = rng.random() * 6.28
        r = rng.uniform(0.32, 0.45)
        body.blob(0.01, (math.cos(a) * r, rng.uniform(0.35, 1.1), math.sin(a) * r), 0xfff0a0, kind=MAT_EMIT, detail=1)
    # tuna-can stage
    n.lathe([(0.0, -0.07), (0.315, -0.07), (0.325, -0.065), (0.325, -0.005), (0.315, 0.0), (0.29, 0.0), (0.29, -0.006),
             (0.0, -0.006)], (0, 0, 0), 0xb9c0c8, seg=32, kind=MAT_METAL)
    for yy in (-0.055, -0.02):
        n.lathe([(0.327, yy), (0.329, yy + 0.004), (0.327, yy + 0.008)], (0, 0, 0), 0x8e969f, seg=32, kind=MAT_METAL,
                cap_bottom=False, cap_top=False)
    # the tuna label wraps the can (a band just outside it)
    n.lathe([(0.3265, -0.05), (0.3265, -0.025)], (0, 0, 0), 0x2f6fd6, seg=32, kind=MAT_GLOSSY,
            cap_bottom=False, cap_top=False)
    n.col_boxb((0.6, 1.0, 0.55), (0, -0.07, 0))
    n.marker('ANCHOR', 'talk', (0, 0, 0.7), size=(1, 1, 1), scale_size=False)
    marker_light(n, 'glow', (0, 0.9, 0.4), 0xffd8a0, 2.5, 0.9)
    return n


# ------------------------------------------------------------------ the village (hub)

def thimble_house(n, x, z, s=1.0, color=0xb9c0c8):
    """An upside-down thimble with a round-topped door and a glowing window."""
    n.lathe([(0.16 * s, 0.0), (0.157 * s, 0.2 * s), (0.135 * s, 0.3 * s), (0.09 * s, 0.338 * s), (0.0, 0.35 * s)],
            (x, 0, z), color, seg=28, kind=MAT_METAL)
    # the dimples of a thimble: little dents in rows
    for row in range(3):
        yy = 0.24 * s + row * 0.028 * s
        rr = 0.148 * s - row * 0.012 * s
        for k in range(14 - row * 3):
            a = (k + row * 0.5) / (14 - row * 3) * 2 * math.pi
            n.blob(0.008 * s, (x + math.cos(a) * rr, yy, z + math.sin(a) * rr), 0x8e969f, kind=MAT_METAL, detail=1)
    for k in range(2):
        n.torus(0.158 * s - k * 0.002, 0.006, (x, 0.03 * s + k * 0.035 * s, z), 0x8e969f, seg=28, tseg=4)
    # round-topped wooden door on the front
    dz = z + 0.158 * s
    n.box((0.09 * s, 0.1 * s, 0.02), (x, 0.05 * s, dz), 0x7a4a2a, kind=MAT_WOOD, bevel=0.004)
    n.cyl(0.045 * s, 0.02, (x, 0.1 * s, dz), 0x7a4a2a, rot=(R90, 0, 0), seg=14, kind=MAT_WOOD)
    n.blob(0.008 * s, (x + 0.025 * s, 0.06 * s, dz + 0.014), GOLD, kind=MAT_METAL, detail=1)
    # glowing round window
    n.torus(0.03 * s, 0.006, (x + 0.075 * s, 0.22 * s, z + 0.14 * s), 0x7a4a2a, rot=(R90, 0, -0.45), seg=14, tseg=4)
    n.cyl(0.028 * s, 0.01, (x + 0.075 * s, 0.22 * s, z + 0.138 * s), 0xffe39a, rot=(R90, 0, -0.45), seg=14, kind=MAT_EMIT)
    n.col_boxb((0.3 * s, 0.34 * s, 0.3 * s), (x, 0, z))


def moss_clump(n, rng, x, z, size):
    """A soft mound of moss: a few squashed fuzzy blobs sunk into the ground."""
    for k in range(rng.randint(3, 5)):
        a, d = rng.uniform(0, 6.28), rng.uniform(0, size * 0.6)
        r = size * rng.uniform(0.35, 0.6)
        n.blob(r, (x + math.cos(a) * d, -r * 0.25, z + math.sin(a) * d), rng.choice([0x3f6a2c, 0x4a7a33, 0x557f38]),
               scale=(1.0, 0.62, 1.0), kind=MAT_FUR, detail=2, wonk=r * 0.08)


def fly_agaric(n, x, z, s, cap):
    n.sweep([(x, 0.0, z), (x, 0.05 * s, z), (x, 0.08 * s, z)], [0.018 * s, 0.014 * s, 0.013 * s], 0xf3ead6,
            kind=MAT_SKIN, seg=10, steps=2, start='flat', end='flat')
    n.blob(0.045 * s, (x, 0.085 * s, z), cap, scale=(1, 0.55, 1), kind=MAT_GLOSSY, detail=2)
    for k in range(5):
        a = k * 1.3
        n.blob(0.007 * s, (x + math.cos(a) * 0.028 * s, 0.105 * s, z + math.sin(a) * 0.028 * s), 0xfff8ee,
               kind=MAT_GLOSSY, detail=1, scale=(1, 0.5, 1))


def little_sock(n, x, y, z, color, heel):
    """A small knitted sock hanging from a peg (drying on the washing line)."""
    n.sweep([(x, y, z), (x, y - 0.08, z), (x, y - 0.115, z + 0.01), (x + 0.012, y - 0.125, z + 0.05)],
            [0.024, 0.023, 0.022, 0.017], color, kind=MAT_KNIT, seg=12, steps=3, start='open', end='dome',
            ribs=lambda t: 0.06 if t < 0.2 else 0.0, rim=(0.006, 0.02, 0x5a3a2a),
            paint=lambda t, a: heel if t > 0.8 or (0.52 < t < 0.68) else (0xf3ead6 if 0.2 < t < 0.27 else color))


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
        if abs(x) < 0.45 and z > -1.4:
            continue  # keep the button path clear
        moss_clump(n, rng, x, z, rng.uniform(0.12, 0.28))
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
    n.cylb(0.16, 0.02, (1.0, 0.0, 0.9), WOOD_L, seg=22)
    n.cylb(0.1, 0.14, (1.0, 0.02, 0.9), 0xd8342c, seg=22, kind=MAT_KNIT)  # thread on the spool
    n.cylb(0.16, 0.02, (1.0, 0.16, 0.9), WOOD_L, seg=22)
    n.col_boxb((0.3, 0.18, 0.3), (1.0, 0, 0.9))
    for a in (0.3, 2.4, 4.4):
        cx, cz = 1.0 + math.cos(a) * 0.3, 0.9 + math.sin(a) * 0.3
        n.lathe([(0.0, 0.0), (0.06, 0.0), (0.062, 0.028), (0.055, 0.032), (0.0, 0.032)], (cx, 0, cz),
                [RED, 0x2f6fd6, GOLD][int(a) % 3], seg=20, kind=MAT_METAL)  # bottle-cap stools
    n.cyl(0.02, 0.03, (1.0, 0.195, 0.9), 0xf3ead6, seg=8)  # tiny cup (a bead)
    # button path from the tunnel to the Great Sock
    for i in range(9):
        z = 2.1 - i * 0.42
        col = YARN_COLS[i % len(YARN_COLS)]
        bx = 0.12 * math.sin(i * 0.9)
        n.lathe([(0.0, 0.0), (0.07, 0.0), (0.072, 0.008), (0.066, 0.014), (0.03, 0.01), (0.0, 0.01)], (bx, 0.0, z), col,
                seg=20, kind=MAT_GLOSSY)
        for hx, hz in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
            n.cyl(0.008, 0.004, (bx + hx * 0.02, 0.011, z + hz * 0.02), 0x2a2a2a, seg=8, kind=MAT_GLOSSY)
    # washing line with drying socks between two posts
    for x in (-2.8, -0.9):
        n.solidb((0.03, 0.45, 0.03), (x, 0, -1.9), WOOD_M)
    n.cyl(0.004, 1.9, (-1.85, 0.43, -1.9), 0xf2f0e8, rot=(0, 0, R90), seg=4)
    for i in range(5):
        x = -2.6 + i * 0.36
        c = YARN_COLS[(i * 2) % len(YARN_COLS)]
        little_sock(n, x, 0.42, -1.9, c, YARN_COLS[(i * 2 + 3) % len(YARN_COLS)])
        n.box((0.012, 0.035, 0.012), (x, 0.435, -1.9), WOOD_L)  # peg
    # candle-stub lanterns
    for (x, z) in ((-1.7, -0.6), (1.8, -0.4), (-0.6, 1.2), (2.4, 1.5)):
        n.lathe([(0.0, 0.0), (0.035, 0.0), (0.035, 0.06), (0.03, 0.07), (0.0, 0.068)], (x, 0, z), CREAM, seg=16,
                kind=MAT_GLOSSY)
        n.blob(0.01, (x + 0.03, 0.05, z + 0.01), CREAM, kind=MAT_GLOSSY, detail=1, scale=(0.6, 1.6, 0.6))  # wax drip
        n.sweep([(x, 0.07, z), (x, 0.085, z)], [0.003, 0.003], 0x2a2a2a, seg=4, steps=1)  # wick
        n.blob(0.013, (x, 0.1, z), 0xffc040, kind=MAT_EMIT, detail=2, scale=(0.8, 1.6, 0.8))
        marker_light(n, 'candle%d' % int((x + 3) * 10), (x, 0.14, z), 0xffb060, 2.4, 1.1)
    # fly agarics
    for i in range(10):
        x, z = rng.uniform(-3.2, 3.2), rng.uniform(-2.2, 2.2)
        if abs(x) < 0.5 and z > -1.5:
            continue
        fly_agaric(n, x, z, rng.uniform(0.7, 1.2), rng.choice([0xd8342c, 0xc98a47, 0xe2b21e]))
    n.marker('SPAWN', 'center', (0, 0.05, 1.2), size=(1, 1, 1), scale_size=False)
    marker_light(n, 'moon', (0, H - 0.05, 0), 0x8fa8ff, 6, 0.5)
    return n


def yarn_ball(n, x, y, z, r, color, rng):
    n.blob(r, (x, y, z), color, kind=MAT_KNIT, detail=3)
    for k in range(4):  # a few loose strands wound around it
        tilt, spin = rng.uniform(0, math.pi), rng.uniform(0, math.pi)
        pts = []
        for i in range(13):
            a = i / 12 * 2 * math.pi
            px, py = math.cos(a) * r * 1.02, math.sin(a) * r * 1.02
            # rotate the circle by tilt around X then spin around Y
            py, pz = py * math.cos(tilt), py * math.sin(tilt)
            px, pz = px * math.cos(spin) - pz * math.sin(spin), px * math.sin(spin) + pz * math.cos(spin)
            pts.append((x + px, y + py, z + pz))
        n.sweep(pts, [r * 0.07] * len(pts), color, kind=MAT_KNIT, seg=6, steps=2, start='flat', end='flat')


def knitting_corner():
    n = mk('knittingCorner')
    n.detail = 0.5
    rng = random.Random(2)
    # tomato pincushion with pins
    n.blob(0.16, (0, 0.13, 0), 0xd8342c, scale=(1, 0.78, 1), kind=MAT_FABRIC, detail=3)
    for k in range(6):  # the tomato's segments are stitched grooves
        a = k / 6 * 2 * math.pi
        n.sweep([(math.cos(a) * 0.02, 0.255, math.sin(a) * 0.02), (math.cos(a) * 0.13, 0.2, math.sin(a) * 0.13),
                 (math.cos(a) * 0.165, 0.12, math.sin(a) * 0.165), (math.cos(a) * 0.13, 0.03, math.sin(a) * 0.13)],
                [0.006] * 4, 0xa82a22, kind=MAT_FABRIC, seg=5, steps=3)
    for k in range(5):
        a = k * 1.25
        n.sweep([(0, 0.25, 0), (math.cos(a) * 0.05, 0.265, math.sin(a) * 0.05), (math.cos(a) * 0.09, 0.25, math.sin(a) * 0.09)],
                [0.018, 0.014, 0.004], 0x3aa845, kind=MAT_FABRIC, seg=6, steps=2, squash=lambda t: (1.0, 0.35))
    for k in range(8):
        a, e = rng.uniform(0, 6.28), rng.uniform(0.3, 1.2)
        d = (math.cos(a) * math.cos(e), math.sin(e), math.sin(a) * math.cos(e))
        base = (d[0] * 0.13, 0.13 + d[1] * 0.1, d[2] * 0.13)
        tip = (base[0] + d[0] * 0.1, base[1] + d[1] * 0.1, base[2] + d[2] * 0.1)
        n.sweep([base, tip], [0.003, 0.003], 0xd0d4da, kind=MAT_METAL, seg=5, steps=1, start='flat', end='flat')
        n.blob(0.013, tip, YARN_COLS[k % len(YARN_COLS)], kind=MAT_GLOSSY, detail=2)
    n.col_boxb((0.34, 0.26, 0.34))
    # yarn balls with crossed knitting needles
    for i, (x, z) in enumerate(((0.35, 0.1), (0.46, -0.12), (0.28, -0.2))):
        yarn_ball(n, x, 0.07, z, 0.07, YARN_COLS[i + 2], rng)
    for rx, rz in ((0.6, 0.3), (-0.6, -0.3)):
        n.sweep([(0.38 - 0.17 * math.sin(rz), 0.12 - 0.17 * math.cos(rx), -0.17 * math.sin(rx)),
                 (0.38 + 0.17 * math.sin(rz), 0.12 + 0.17 * math.cos(rx), 0.17 * math.sin(rx))], [0.006, 0.006], 0xb9c0c8,
                kind=MAT_METAL, seg=6, steps=1, start='dome', end='flat')
    # a little spinning wheel
    n.torus(0.14, 0.013, (-0.35, 0.24, 0), WOOD_M, rot=(0, 0, R90), seg=28, tseg=6)
    for k in range(8):
        a = k / 8 * math.pi
        n.sweep([(-0.35, 0.24 - math.cos(a) * 0.14, -math.sin(a) * 0.14), (-0.35, 0.24 + math.cos(a) * 0.14, math.sin(a) * 0.14)],
                [0.005, 0.005], WOOD_L, seg=5, steps=1, start='flat', end='flat')
    n.cyl(0.025, 0.05, (-0.35, 0.24, 0), WOOD_D, rot=(0, 0, R90), seg=12)
    for zz in (0.1, -0.1):
        n.sweep([(-0.35, 0.0, zz), (-0.35, 0.26, zz * 0.2)], [0.014, 0.012], WOOD_D, seg=8, steps=1, start='flat', end='dome')
    n.boxb((0.3, 0.02, 0.26), (-0.35, 0, 0), WOOD_D, bevel=0.005)
    # sign
    n.sweep([(0.1, 0.0, 0.3), (0.1, 0.44, 0.3)], [0.012, 0.011], WOOD_D, seg=8, steps=1, start='flat', end='dome')
    n.box((0.28, 0.1, 0.015), (0.1, 0.38, 0.31), WOOD_L, rot=(0, 0.2, 0), bevel=0.004)
    n.marker('ANCHOR', 'use', (0, 0, 0.55), size=(1, 1, 1), scale_size=False)
    return n


def sock_tunnel():
    """A giant sock lying on its side: crawl in to set off for the night."""
    n = mk('sockTunnel')
    n.detail = 2.2
    L, R = 1.2, 0.3
    stripes = [0x2f6fd6, 0xf3ead6, 0x3aa845, 0xf3ead6]

    def paint(t, a):
        if t < 0.14:
            return 0xf6f0e2  # ribbed cuff at the entrance
        if t > 0.82:
            return 0x2f6fd6  # toe
        return stripes[int((t - 0.14) / 0.085) % len(stripes)]

    n.sweep([(0, R, L / 2 + 0.06), (0, R, 0.0), (0, R * 0.98, -L / 2 + 0.1), (0, R * 0.95, -L / 2 - 0.05)],
            [R * 1.02, R, R * 0.98, R * 0.9], 0xf3ead6, kind=MAT_KNIT, seg=30, steps=6, start='open', end='dome',
            paint=paint, ribs=lambda t: 0.035 if t < 0.14 else 0.0, rim=(0.04, L * 0.95, 0x3a2a4a),
            squash=lambda t: (1.0, 0.92))
    # the glow deep inside
    n.cyl(R * 0.7, 0.01, (0, R, -L / 2 + 0.2), 0x9a6ae0, rot=(R90, 0, 0), seg=20, kind=MAT_EMIT)
    n.cyl(R * 0.4, 0.012, (0, R, -L / 2 + 0.21), 0xe0c8ff, rot=(R90, 0, 0), seg=16, kind=MAT_EMIT)
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
    for k in range(18):
        a = k / 18 * 6.28
        c, sn = math.cos(a), math.sin(a)
        n.sweep([(c * 0.19, top + 0.03, sn * 0.19), (c * 0.19, top + 0.38, sn * 0.19), (c * 0.16, top + 0.46, sn * 0.16),
                 (c * 0.06, top + 0.52, sn * 0.06), (0, top + 0.535, 0)], [0.0045] * 5, BRASS, kind=MAT_METAL, seg=5,
                steps=3, start='flat', end='flat')
    for yy in (top + 0.2, top + 0.38):
        n.torus(0.19, 0.006, (0, yy, 0), BRASS, seg=24, tseg=4)
    n.torus(0.04, 0.008, (0, top + 0.58, 0), BRASS, rot=(R90, 0, 0), seg=12, tseg=4)
    n.cyl(0.012, 0.05, (0, top + 0.545, 0), BRASS, seg=8)
    n.col_boxb((0.42, 0.55, 0.42), (0, top, 0))
    n.cyl(0.006, 0.36, (0, top + 0.16, 0), WOOD_M, rot=(0, 0, R90), seg=4)  # perch
    bird = Node('parrot', n, (0, top + 0.17, 0), surface=MAT_FUR)
    bird.detail = 0.4  # fine feathers

    def plumage(p, nrm, mat):
        if p[1] > 0.105:
            return MAT_FUR, 0xd8342c  # red head
        if p[1] < 0.03 and p[2] > 0.0:
            return MAT_FUR, 0xf2cf3c  # yellow chest
        return MAT_FUR, 0x3aa845

    with bird.fuse(voxel=0.006, smooth=0.6, iterations=4, decimate=0.3, paint=plumage):
        bird.blob(0.058, (0, 0.045, 0), 0x3aa845, scale=(0.9, 1.25, 0.95))
        bird.blob(0.048, (0, 0.135, 0.02), 0xd8342c)
    # big hooked beak
    bird.sweep([(0, 0.145, 0.06), (0, 0.14, 0.09), (0, 0.115, 0.105), (0, 0.095, 0.095)], [0.024, 0.02, 0.012, 0.004],
               0xf2cf3c, kind=MAT_GLOSSY, seg=10, steps=3, start='flat', end='dome')
    bird.blob(0.015, (0, 0.115, 0.075), 0x3a3a3a, kind=MAT_GLOSSY, detail=1, scale=(1.2, 0.8, 1))  # lower beak
    for x in (-0.03, 0.03):
        bird.blob(0.015, (x, 0.155, 0.045), 0xffffff, kind=MAT_GLOSSY, detail=2)
        bird.blob(0.0075, (x * 1.1, 0.156, 0.058), BLACK, kind=MAT_GLOSSY, detail=1)
    # folded wings and a long blue tail
    for sgn in (-1, 1):
        bird.sweep([(sgn * 0.05, 0.09, 0.01), (sgn * 0.058, 0.03, -0.03), (sgn * 0.045, -0.04, -0.07)],
                   [0.03, 0.028, 0.008], 0x2b8a3a, seg=10, steps=3, squash=lambda t: (0.35, 1.0))
    for k, (x, c) in enumerate(((-0.012, 0x2f6fd6), (0.012, 0x2f6fd6), (0.0, 0xd8342c))):
        bird.sweep([(x, -0.01, -0.05), (x * 1.5, -0.08, -0.08), (x * 2.5, -0.16, -0.09 - k * 0.005)], [0.016, 0.013, 0.004],
                   c, seg=8, steps=3, squash=lambda t: (1.0, 0.35))
    for x in (-0.018, 0.018):
        bird.sweep([(x, -0.03, 0.01), (x, -0.012, 0.018), (x, -0.012, -0.012)], [0.006] * 3, 0x7a7a7a, kind=MAT_SKIN,
                   seg=5, steps=2)  # feet on the perch
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
    """A fallen gnome's hat (tinted with the owner's colour at runtime): the same knitted hat, flopped over."""
    n = Node('gnomeHat', k=1.0)
    n.props['kind'] = 'gnomeHat'
    n.detail = 0.8
    n.sweep([(0, -0.12, 0), (0, -0.03, 0), (0, 0.1, -0.02), (0, 0.2, -0.1), (0, 0.24, -0.24), (0, 0.22, -0.36)],
            [0.19, 0.182, 0.15, 0.1, 0.06, 0.016], 0xd8342c, kind=MAT_TINT, seg=22, steps=5, start='open', end='dome',
            ribs=lambda t: 0.03 if t < 0.13 else 0.0, rim=(0.02, 0.12, 0x3a2a22))
    n.blob(0.045, (0, 0.235, -0.38), 0xfff4d8, kind=MAT_KNIT, detail=2, wonk=0.004)
    n.blob(0.03, (0, -0.04, 0), 0xfff4a0, kind=MAT_EMIT, detail=1)  # a faint glow so friends find it
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
