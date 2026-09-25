"""House furniture. Authored in METRES (root k = HS), origin at the floor centre, front = +Z.

Markers understood by the game:
  COL_*    box collider (node scale = size)
  SURF_n   item spawn surface (scale x/z = rect size, y = surface height)
  ZONE_x   trigger volume (toilet bowl, oven, fridge, ...)
  ANCHOR_x named point (where the old man sits/sleeps, where trapped gnomes go)
  LIGHT_x  point light (props: color, range, intensity, on)
  MECH_x   movable part (props: role, kind=hinge|slide|button, axis, open, hx/hy/hz handle)
"""
import math

from gnomelib.core import HS, MAT_EMIT, MAT_GLASS, Node

# palette
WOOD_L = 0xc89560
WOOD_M = 0x9a6236
WOOD_D = 0x6a3f22
WOOD_R = 0x8c3b28
WHITE = 0xf1efe8
OFFWHITE = 0xe4ded0
CREAM = 0xf3e3bf
METAL = 0xb9c0c8
STEEL = 0x8e969f
DMETAL = 0x3d4148
BLACK = 0x1d1e22
GLASS = 0xa9d6ec
GOLD = 0xe8b83a
BRASS = 0xc6a14a
RED = 0xc23b30
BURGUNDY = 0x7e2833
GREEN = 0x4d7a4f
MUSTARD = 0xcf9d33
TEAL = 0x2f7f86
NAVY = 0x2c3b63
PINK = 0xe79aa8
LEAF = 0x3f8f45
TERRA = 0xb9623e
PURPLE = 0x7d4c9e

R90 = math.pi / 2


def root(name):
    n = Node(name, k=HS)
    n.props['kind'] = name
    return n


def surf(n, i, x, y, z, w, d):
    n.marker('SURF', str(i), (x, y, z), size=(w, 0.05, d))


def zone(n, name, c, size):
    n.marker('ZONE', name, c, size=size)


def anchor(n, name, p, rot=(0, 0, 0)):
    n.marker('ANCHOR', name, p, rot, size=(1, 1, 1), scale_size=False)


def light(n, name, p, color, rng, intensity, on=True):
    m = n.marker('LIGHT', name, p, size=(1, 1, 1), scale_size=False)
    m.props.update({'color': '%06x' % color, 'range': rng * HS, 'intensity': intensity, 'on': 1 if on else 0})
    return m


def mech(n, mid, role, kind, pivot, axis='y', open_=0.0, handle=(0, 0, 0), human_only=False, rot=(0, 0, 0)):
    m = Node('MECH_' + mid, n, pivot, rot)
    m.props.update({'role': role, 'kind': kind, 'axis': axis, 'open': open_ if kind != 'slide' else open_ * HS,
                    'hx': handle[0] * HS, 'hy': handle[1] * HS, 'hz': handle[2] * HS, 'humanOnly': 1 if human_only else 0})
    return m


def legs4(n, w, d, h, r, color, inset=0.05, collide=True, square=False):
    x = w / 2 - inset
    z = d / 2 - inset
    for sx in (-1, 1):
        for sz in (-1, 1):
            if square:
                n.boxb((r * 2, h, r * 2), (sx * x, 0, sz * z), color)
            else:
                n.cylb(r, h, (sx * x, 0, sz * z), color, seg=6)
            if collide:
                n.col_boxb((r * 2, h, r * 2), (sx * x, 0, sz * z))


# ------------------------------------------------------------------ living room

def sofa(w=2.0, color=GREEN):
    n = root('sofa')
    d = 0.9
    lighter = 0x5f9161
    legs4(n, w, d, 0.1, 0.03, WOOD_D, 0.08, collide=False)
    n.solidb((w, 0.25, d - 0.05), (0, 0.1, 0), color, bevel=0.03)
    n.boxb((w / 2 - 0.2, 0.14, 0.66), (-w / 4, 0.35, 0.08), lighter, bevel=0.04, wonk=0.006)
    n.boxb((w / 2 - 0.2, 0.14, 0.66), (w / 4, 0.35, 0.08), lighter, bevel=0.04, wonk=0.006)
    n.col_boxb((w - 0.3, 0.14, 0.66), (0, 0.35, 0.08))
    n.solidb((w - 0.01, 0.5, 0.22), (0, 0.35, -d / 2 + 0.115), color, bevel=0.04)
    n.solidb((0.2, 0.3, d - 0.06), (-w / 2 + 0.1, 0.35, 0.005), color, bevel=0.05)
    n.solidb((0.2, 0.3, d - 0.06), (w / 2 - 0.1, 0.35, 0.005), color, bevel=0.05)
    # tired cushions and a hole with a spring sticking out (funny)
    n.boxb((0.45, 0.35, 0.12), (-w / 4, 0.48, -0.24), MUSTARD, rot=(-0.25, 0.1, 0.05), bevel=0.04, wonk=0.01)
    n.boxb((0.45, 0.35, 0.12), (w / 4, 0.48, -0.24), CREAM, rot=(-0.25, -0.1, -0.08), bevel=0.04, wonk=0.01)
    n.box((0.08, 0.005, 0.08), (w / 4 + 0.1, 0.492, 0.2), 0x2f4a30)
    n.cyl(0.02, 0.06, (w / 4 + 0.1, 0.51, 0.2), METAL, seg=5)
    surf(n, 0, 0, 0.49, 0.08, w - 0.5, 0.5)
    anchor(n, 'sit', (0, 0.45, 0.1))
    return n


def armchair(color=BURGUNDY):
    n = root('armchair')
    legs4(n, 0.9, 0.85, 0.1, 0.03, WOOD_D, 0.08, collide=False)
    n.solidb((0.9, 0.27, 0.8), (0, 0.1, 0), color, bevel=0.03)
    n.boxb((0.58, 0.13, 0.62), (0, 0.37, 0.07), 0x9a3844, bevel=0.04)
    n.col_boxb((0.58, 0.13, 0.62), (0, 0.37, 0.07))
    n.solidb((0.89, 0.62, 0.2), (0, 0.37, -0.315), color, bevel=0.05)
    n.solidb((0.16, 0.3, 0.79), (-0.37, 0.37, 0.004), color, bevel=0.05)
    n.solidb((0.16, 0.3, 0.79), (0.37, 0.37, 0.004), color, bevel=0.05)
    # knitted blanket + newspaper
    n.box((0.55, 0.03, 0.42), (0.05, 0.99, -0.3), MUSTARD, rot=(0.2, 0, 0.05), wonk=0.01)
    n.box((0.3, 0.01, 0.22), (0.3, 0.505, 0.15), 0xe8e4d8, rot=(0, 0.4, 0.1))
    surf(n, 0, 0, 0.5, 0.1, 0.35, 0.35)
    anchor(n, 'sit', (0, 0.5, 0.12))
    return n


def coffee_table():
    n = root('coffeeTable')
    w, d = 1.0, 0.55
    n.solidb((w, 0.05, d), (0, 0.4, 0), WOOD_M, bevel=0.01)
    legs4(n, w, d, 0.4, 0.025, WOOD_D)
    n.boxb((w - 0.1, 0.02, d - 0.1), (0, 0.12, 0), WOOD_M)
    n.col_boxb((w - 0.1, 0.02, d - 0.1), (0, 0.12, 0))
    # crossword + coffee ring stain
    n.box((0.25, 0.003, 0.2), (-0.2, 0.452, 0.05), 0xefe9dc, rot=(0, 0.3, 0))
    n.cyl(0.04, 0.002, (0.25, 0.451, -0.1), 0x6a4422, seg=10)
    surf(n, 0, 0, 0.45, 0, w - 0.15, d - 0.15)
    return n


def tv_stand():
    n = root('tvStand')
    for y in (0.46, 0.06):
        n.solidb((1.3, 0.04, 0.45), (0, y, 0), WOOD_D)
    n.solidb((0.04, 0.418, 0.446), (-0.63, 0.081, 0), WOOD_D)
    n.solidb((0.04, 0.418, 0.446), (0.63, 0.081, 0), WOOD_D)
    n.solidb((1.216, 0.418, 0.02), (0, 0.081, -0.212), WOOD_D)
    n.solidb((0.04, 0.378, 0.43), (0, 0.101, 0), WOOD_D)
    legs4(n, 1.3, 0.45, 0.06, 0.03, BLACK, 0.06, collide=False)
    # VHS tapes
    for i in range(4):
        n.boxb((0.03, 0.2, 0.12), (-0.5 + i * 0.035, 0.1, 0.05), [0x222222, 0x2a2a44, 0x442a2a, 0x222222][i])
    surf(n, 0, -0.35, 0.1, 0.05, 0.25, 0.3)
    surf(n, 1, 0.45, 0.1, 0.05, 0.3, 0.3)
    return n


def tv():
    n = root('tv')
    n.props['hp'] = 60
    n.solidb((0.7, 0.52, 0.5), (0, 0, -0.02), 0x4b4b4f, bevel=0.03)
    n.boxb((0.5, 0.36, 0.22), (0, 0.08, -0.33), 0x3e3e42, bevel=0.02)
    n.col_boxb((0.5, 0.36, 0.22), (0, 0.08, -0.33))
    scr = Node('screen', n, (-0.04, 0.27, 0.235))
    scr.box((0.56, 0.4, 0.02), (0, 0, 0), 0x223038)
    n.box((0.09, 0.4, 0.02), (0.3, 0.27, 0.235), BLACK)
    n.cyl(0.02, 0.02, (0.3, 0.38, 0.25), 0x777777, rot=(R90, 0, 0), seg=8)
    n.cyl(0.02, 0.02, (0.3, 0.32, 0.25), 0x777777, rot=(R90, 0, 0), seg=8)
    # bunny-ear antenna with foil on the tip (grandpa fixed it himself)
    n.cyl(0.008, 0.45, (-0.1, 0.72, -0.05), METAL, rot=(0, 0, 0.5), seg=5)
    n.cyl(0.008, 0.45, (0.1, 0.72, -0.05), METAL, rot=(0, 0, -0.5), seg=5)
    n.sphere(0.04, (0, 0.54, -0.05), BLACK, seg=6, rings=4)
    n.sphere(0.03, (0.21, 0.92, -0.05), 0xd8dde3, ico=1, wonk=0.01)
    btn = mech(n, 'power', 'tvPower', 'button', (0.3, 0.12, 0.25), 'z', 0, handle=(0, 0, 0.03))
    btn.cyl(0.028, 0.025, (0, 0, 0), RED, rot=(R90, 0, 0), seg=8)
    return n


BOOKS = [RED, NAVY, GREEN, MUSTARD, BURGUNDY, TEAL, CREAM, PURPLE]


def bookshelf(w=0.9, h=1.9, seed=0, name='bookshelf'):
    n = root(name)
    d = 0.32
    n.solidb((0.03, h, d), (-w / 2 + 0.015, 0, 0), WOOD_M)
    n.solidb((0.03, h, d), (w / 2 - 0.015, 0, 0), WOOD_M)
    n.solidb((w, h, 0.02), (0, 0, -d / 2 + 0.01), WOOD_D)
    shelves = [0.0, 0.46, 0.92, 1.38, h - 0.03]
    bi = seed
    si = 0
    for i, y in enumerate(shelves):
        n.solidb((w - 0.062, 0.03, d - 0.034), (0, y, 0.004), WOOD_M)
        if i < len(shelves) - 1:
            x = -w / 2 + 0.05
            end = 0.05 if i % 2 == 0 else -0.1
            while x < end:
                bw = 0.03 + ((bi * 7) % 5) * 0.008
                bh = 0.2 + ((bi * 13) % 6) * 0.025
                tilt = 0.15 if (bi * 5) % 9 == 0 else 0
                n.boxb((bw, bh, 0.22), (x + bw / 2, y + 0.03, 0), BOOKS[bi % len(BOOKS)], rot=(0, 0, tilt))
                x += bw + 0.004
                bi += 1
            n.col_boxb((end - (-w / 2 + 0.05), 0.2, 0.22), ((end + (-w / 2 + 0.05)) / 2, y + 0.03, 0))
            surf(n, si, w / 4, y + 0.03, 0.02, w / 2 - 0.1, 0.2)
            si += 1
    return n


def floor_lamp():
    n = root('floorLamp')
    n.cylb(0.16, 0.03, (0, 0, 0), DMETAL, seg=10)
    n.col_boxb((0.3, 0.03, 0.3))
    n.cylb(0.015, 1.45, (0, 0.03, 0), BRASS, seg=6)
    n.col_boxb((0.04, 1.45, 0.04), (0, 0.03, 0))
    n.cyl(0.22, 0.3, (0, 1.52, 0), CREAM, r2=0.12, seg=10)
    n.col_boxb((0.36, 0.3, 0.36), (0, 1.37, 0))
    n.sphere(0.05, (0, 1.44, 0), 0xfff2c0, seg=6, rings=4, kind=MAT_EMIT)
    # tassels
    for i in range(8):
        a = i * math.pi / 4
        n.box((0.01, 0.05, 0.01), (math.cos(a) * 0.21, 1.34, math.sin(a) * 0.21), MUSTARD)
    light(n, 'bulb', (0, 1.42, 0), 0xffc27a, 4.5, 1.0)
    return n


def table_lamp(color=PINK, name='tableLamp'):
    n = root(name)
    n.cylb(0.07, 0.02, (0, 0, 0), BRASS, seg=8)
    n.cylb(0.012, 0.22, (0, 0.02, 0), BRASS, seg=6)
    n.cyl(0.12, 0.16, (0, 0.3, 0), color, r2=0.07, seg=8)
    n.col_boxb((0.2, 0.4, 0.2))
    n.sphere(0.03, (0, 0.25, 0), 0xfff2c0, seg=6, rings=4, kind=MAT_EMIT)
    light(n, 'bulb', (0, 0.25, 0), 0xffb870, 3.2, 0.8)
    return n


def side_table():
    n = root('sideTable')
    n.cylb(0.25, 0.03, (0, 0.6, 0), WOOD_M, seg=10)
    n.col_boxb((0.42, 0.03, 0.42), (0, 0.6, 0))
    n.cylb(0.03, 0.6, (0, 0, 0), WOOD_D, seg=6)
    n.col_boxb((0.06, 0.6, 0.06))
    n.cylb(0.16, 0.03, (0, 0, 0), WOOD_D, seg=8)
    n.cyl(0.18, 0.004, (0, 0.633, 0), 0xf5f0e4, seg=12)  # doily
    surf(n, 0, 0, 0.63, 0, 0.1, 0.1)
    return n


def rug(name, w, d, c1, c2):
    n = root(name)
    n.boxb((w, 0.008, d), (0, 0, 0), c1)
    n.boxb((w - 0.2, 0.01, d - 0.2), (0, 0, 0), c2)
    n.boxb((w - 0.4, 0.012, d - 0.4), (0, 0, 0), c1)
    # fringe
    for i in range(int(w / 0.08)):
        x = -w / 2 + 0.04 + i * 0.08
        n.box((0.015, 0.004, 0.06), (x, 0.002, d / 2 + 0.03), c2)
        n.box((0.015, 0.004, 0.06), (x, 0.002, -d / 2 - 0.03), c2)
    n.props['noNav'] = 1
    return n


def grandfather_clock():
    n = root('grandfatherClock')
    n.solidb((0.5, 0.25, 0.32), (0, 0, 0), WOOD_R, bevel=0.01)
    n.solidb((0.4, 1.35, 0.26), (0, 0.25, 0), WOOD_R)
    n.solidb((0.5, 0.4, 0.32), (0, 1.6, 0), WOOD_R, bevel=0.01)
    n.cyl(0.15, 0.02, (0, 1.8, 0.16), CREAM, rot=(R90, 0, 0), seg=12)
    n.box((0.012, 0.11, 0.01), (0, 1.84, 0.175), BLACK)
    n.box((0.012, 0.08, 0.01), (0.03, 1.8, 0.175), BLACK, rot=(0, 0, -1.2))
    n.box((0.26, 0.9, 0.01), (0, 0.8, 0.135), 0x3a2418)
    n.cone(0.28, 0.12, (0, 2.06, 0), WOOD_R, seg=4, rot=(0, math.pi / 4, 0))
    pend = Node('pendulum', n, (0, 1.2, 0.14))
    pend.box((0.01, 0.5, 0.01), (0, -0.25, 0), BRASS)
    pend.cyl(0.06, 0.012, (0, -0.52, 0), GOLD, rot=(R90, 0, 0), seg=10)
    return n


def counter(w=1.26, top=0x5d6770, front=0x87a9a0, name='counter'):
    n = root(name)
    d = 0.62
    n.boxb((w - 0.04, 0.08, d - 0.08), (0, 0, -0.03), BLACK)
    n.boxb((w, 0.8, d - 0.04), (0, 0.08, -0.02), CREAM)
    n.col_boxb((w, 0.88, d - 0.04), (0, 0, -0.02))
    n.solidb((w + 0.02, 0.04, d), (0, 0.88, 0), top)
    cnt = max(1, round(w / 0.6))
    for i in range(cnt):
        cx = -w / 2 + (w / cnt) * (i + 0.5)
        n.box((w / cnt - 0.03, 0.72, 0.02), (cx, 0.48, d / 2 - 0.02), front)
        n.box((0.1, 0.018, 0.02), (cx, 0.78, d / 2 + 0.005), METAL)
    surf(n, 0, 0, 0.92, 0, w - 0.15, d - 0.2)
    return n


def sink_counter(w=1.26, top=0x5d6770, front=0x87a9a0):
    n = root('sinkCounter')
    d = 0.62
    n.boxb((w - 0.04, 0.08, d - 0.08), (0, 0, -0.03), BLACK)
    n.boxb((w, 0.64, d - 0.04), (0, 0.08, -0.02), CREAM)
    n.col_boxb((w, 0.72, d - 0.04), (0, 0, -0.02))
    for i in range(2):
        cx = -w / 2 + (w / 2) * (i + 0.5)
        n.box((w / 2 - 0.03, 0.6, 0.02), (cx, 0.4, d / 2 - 0.02), front)
    for (bw, x) in ((w / 2 - 0.25, -w / 4 - 0.125), (w / 2 - 0.25, w / 4 + 0.125)):
        n.solidb((bw - 0.004, 0.16, d - 0.044), (x, 0.72, -0.02), CREAM)
        n.solidb((bw, 0.04, d), (x, 0.88, 0), top)
    n.solidb((0.498, 0.04, 0.12), (0, 0.88, -0.25), top)
    n.solidb((0.498, 0.04, 0.1), (0, 0.88, 0.26), top)
    # basin (hollow)
    n.solidb((0.46, 0.02, 0.36), (0, 0.72, 0), STEEL)
    n.solidb((0.02, 0.158, 0.4), (-0.24, 0.721, 0), STEEL)
    n.solidb((0.02, 0.158, 0.4), (0.24, 0.721, 0), STEEL)
    n.solidb((0.458, 0.157, 0.02), (0, 0.722, -0.19), STEEL)
    n.solidb((0.458, 0.157, 0.02), (0, 0.722, 0.19), STEEL)
    # dirty dishes pile in the sink
    n.cyl(0.1, 0.015, (-0.06, 0.75, 0.02), WHITE, rot=(0.1, 0, 0.1), seg=10)
    n.cyl(0.1, 0.015, (-0.04, 0.765, 0.01), 0xdfe8ef, rot=(-0.08, 0, 0.05), seg=10)
    n.cylb(0.02, 0.28, (0, 0.92, -0.27), METAL, seg=6)
    n.box((0.03, 0.03, 0.16), (0, 1.19, -0.2), METAL)
    f = mech(n, 'faucet', 'faucet', 'button', (0.08, 0.95, -0.27), 'y', 0, handle=(0, 0.02, 0))
    f.cyl(0.03, 0.03, (0, 0, 0), RED, seg=8)
    zone(n, 'sink', (0, 0.82, 0), (0.46, 0.2, 0.36))
    surf(n, 0, -w / 4 - 0.12, 0.92, 0, w / 2 - 0.35, 0.4)
    surf(n, 1, 0, 0.745, 0, 0.3, 0.25)
    return n


def stove():
    """Hollow oven: gnomes get locked in here."""
    n = root('stove')
    w, d = 0.62, 0.62
    body = OFFWHITE
    n.solidb((w, 0.12, d), (0, 0, 0), 0x55575c)
    n.solidb((0.04, 0.518, d - 0.002), (-w / 2 + 0.02, 0.121, 0), body)
    n.solidb((0.04, 0.518, d - 0.002), (w / 2 - 0.02, 0.121, 0), body)
    n.solidb((w - 0.082, 0.518, 0.04), (0, 0.121, -d / 2 + 0.02), body)
    n.boxb((w - 0.08, 0.03, d - 0.06), (0, 0.12, 0), DMETAL)
    n.boxb((w - 0.08, 0.04, d - 0.06), (0, 0.6, 0), DMETAL)
    n.solidb((w, 0.26, d), (0, 0.64, 0), body)
    n.solidb((w - 0.004, 0.03, d - 0.004), (0, 0.9, 0), BLACK)
    n.solidb((w - 0.008, 0.14, 0.04), (0, 0.931, -d / 2 + 0.022), body)
    for (x, z, r) in ((-0.15, -0.13, 0.08), (0.15, -0.13, 0.08), (-0.15, 0.15, 0.065), (0.15, 0.15, 0.065)):
        n.cyl(r, 0.012, (x, 0.925, z), DMETAL, seg=10)
        n.cyl(r * 0.5, 0.014, (x, 0.926, z), 0x2a2a2a, seg=8)
    for x in (-0.2, -0.07, 0.07, 0.2):
        n.cyl(0.022, 0.03, (x, 0.8, d / 2 + 0.01), BLACK, rot=(R90, 0, 0), seg=8)
    n.cylb(0.02, 0.03, (0.26, 0.8, d / 2 + 0.02), 0xff4422, seg=6, kind=MAT_EMIT)  # pilot light
    # oven rack
    for i in range(5):
        n.box((0.01, 0.01, d - 0.12), (-0.2 + i * 0.1, 0.34, 0), STEEL)
    glow = Node('ovenGlow', n, (0, 0.58, 0))
    glow.box((w - 0.1, 0.02, d - 0.1), (0, 0, 0), 0xff5a1f, kind=MAT_EMIT)
    door = mech(n, 'ovenDoor', 'trap', 'hinge', (0, 0.14, d / 2 + 0.015), 'x', R90, handle=(0, 0.42, 0.04))
    door.boxb((w - 0.04, 0.48, 0.03), (0, 0, 0), body)
    door.col_boxb((w - 0.04, 0.48, 0.03), (0, 0, 0))
    door.box((0.36, 0.2, 0.01), (0, 0.26, 0.017), 0x2a1c14, kind=MAT_GLASS)
    door.box((0.4, 0.025, 0.025), (0, 0.44, 0.04), METAL)
    zone(n, 'oven', (0, 0.37, 0.02), (w - 0.1, 0.44, d - 0.1))
    anchor(n, 'trap', (0, 0.16, 0.0))
    anchor(n, 'use', (0, 0, 0.95))
    surf(n, 0, 0, 0.93, 0, 0.4, 0.4)
    return n


def fridge():
    """Hollow fridge + freezer. The freezer is a gnome prison."""
    n = root('fridge')
    w, d, h = 0.72, 0.7, 1.85
    body = WHITE
    inner = 0xdde6ea
    n.solidb((w, 0.05, d), (0, 0, 0), 0x55575c)
    n.solidb((0.04, h - 0.092, d - 0.042), (-w / 2 + 0.02, 0.051, -0.021), body)
    n.solidb((0.04, h - 0.092, d - 0.042), (w / 2 - 0.02, 0.051, -0.021), body)
    n.solidb((w - 0.082, h - 0.092, 0.04), (0, 0.051, -d / 2 + 0.02), body)
    n.solidb((w, 0.04, d - 0.04), (0, h - 0.04, -0.02), body)
    n.boxb((w - 0.08, 0.03, d - 0.08), (0, 0.05, -0.02), inner)
    n.solidb((w - 0.08, 0.05, d - 0.06), (0, 0.66, -0.02), body)
    n.solidb((w - 0.08, 0.02, d - 0.12), (0, 1.12, -0.04), 0xcfe3ec)
    n.solidb((w - 0.08, 0.02, d - 0.12), (0, 1.45, -0.04), 0xcfe3ec)
    lamp = Node('fridgeLight', n, (0, 1.76, -0.02))
    lamp.box((w - 0.1, 0.02, d - 0.1), (0, 0, 0), 0xfff8e0, kind=MAT_EMIT)
    # ice crust inside the freezer
    n.box((w - 0.1, 0.02, d - 0.1), (0, 0.62, -0.02), 0xeaf6ff)
    fd = mech(n, 'fridgeDoor', 'fridge', 'hinge', (-w / 2, 0.71, d / 2 - 0.02), 'y', -1.9, handle=(w - 0.08, 0.3, 0.07))
    fd.boxb((w, 1.12, 0.05), (w / 2, 0, 0.025), body)
    fd.col_boxb((w, 1.12, 0.05), (w / 2, 0, 0.025))
    fd.box((0.03, 0.4, 0.04), (w - 0.08, 0.3, 0.07), METAL)
    fd.box((0.1, 0.07, 0.01), (0.2, 0.8, 0.055), RED)
    fd.box((0.06, 0.08, 0.01), (0.4, 0.7, 0.055), 0xf2cf3c)
    fd.box((0.14, 0.1, 0.005), (0.28, 0.55, 0.053), 0xffffff)  # drawing by a grandchild
    fd.box((0.1, 0.05, 0.004), (0.28, 0.55, 0.056), 0x7fb5e8)
    fd.boxb((w - 0.1, 0.1, 0.06), (w / 2, 0.2, -0.03), 0xcfe3ec)
    fz = mech(n, 'freezerDoor', 'trap', 'hinge', (-w / 2, 0.06, d / 2 - 0.02), 'y', -1.9, handle=(w / 2, 0.55, 0.07))
    fz.boxb((w, 0.62, 0.05), (w / 2, 0, 0.025), body)
    fz.col_boxb((w, 0.62, 0.05), (w / 2, 0, 0.025))
    fz.box((0.3, 0.03, 0.04), (w / 2, 0.55, 0.07), METAL)
    fz.box((0.2, 0.08, 0.005), (w / 2, 0.35, 0.053), 0x9ad0ee)
    zone(n, 'freezer', (0, 0.37, -0.02), (w - 0.1, 0.55, d - 0.1))
    zone(n, 'fridge', (0, 1.2, -0.02), (w - 0.1, 1.0, d - 0.1))
    anchor(n, 'trap', (0, 0.09, -0.02))
    anchor(n, 'use', (0.3, 0, 1.05))
    surf(n, 0, 0, 1.14, -0.05, 0.45, 0.35)
    surf(n, 1, 0, 1.47, -0.05, 0.45, 0.35)
    surf(n, 2, 0, 0.71, -0.05, 0.45, 0.35)
    return n


def kitchen_table():
    n = root('kitchenTable')
    w, d = 1.2, 0.8
    n.solidb((w, 0.04, d), (0, 0.72, 0), WOOD_L, bevel=0.01)
    legs4(n, w, d, 0.72, 0.03, WOOD_M, 0.07, square=True)
    # checkered table cloth
    for i in range(6):
        for j in range(4):
            if (i + j) % 2 == 0:
                n.box((w * 0.1, 0.004, d * 0.22), (-w * 0.25 + i * w * 0.1, 0.762, -d * 0.33 + j * d * 0.22), 0xd9534f)
    n.box((w * 0.6, 0.003, d + 0.04), (0, 0.761, 0), 0xf6f0e8)
    surf(n, 0, 0, 0.765, 0, w - 0.2, d - 0.2)
    return n


def bathtub():
    n = root('bathtub')
    w, d, h = 1.7, 0.75, 0.55
    c = WHITE
    n.solidb((w, 0.08, d), (0, 0.06, 0), c)
    n.solidb((w, h - 0.08, 0.08), (0, 0.14, -d / 2 + 0.04), c)
    n.solidb((w, h - 0.08, 0.08), (0, 0.14, d / 2 - 0.04), c)
    n.solidb((0.08, h - 0.082, d - 0.162), (-w / 2 + 0.04, 0.141, 0), c)
    n.solidb((0.08, h - 0.082, d - 0.162), (w / 2 - 0.04, 0.141, 0), c)
    # golden lion feet
    for sx in (-1, 1):
        for sz in (-1, 1):
            n.sphere(0.06, (sx * (w / 2 - 0.12), 0.05, sz * (d / 2 - 0.1)), GOLD, scale=(1, 0.9, 1.3), seg=6, rings=4)
    n.cylb(0.02, 0.2, (-w / 2 + 0.1, h + 0.06, 0), METAL, seg=6)
    n.box((0.12, 0.03, 0.03), (-w / 2 + 0.16, h + 0.25, 0), METAL)
    water = Node('water', n, (0, 0.2, 0))
    water.box((w - 0.18, 0.01, d - 0.18), (0, 0, 0), 0x6fb7d9, kind=MAT_GLASS)
    # bubbles
    for i, (x, z) in enumerate(((-0.3, 0.1), (0.1, -0.12), (0.4, 0.05), (-0.1, 0.15))):
        water.sphere(0.05 + (i % 2) * 0.02, (x, 0.02, z), 0xffffff, seg=6, rings=4)
    tap = mech(n, 'tap', 'tubFaucet', 'button', (-w / 2 + 0.1, h + 0.26, 0.08), 'y', 0, handle=(0, 0.02, 0))
    tap.cyl(0.035, 0.03, (0, 0, 0), 0x3f6fb5, seg=8)
    zone(n, 'tub', (0, 0.35, 0), (w - 0.16, 0.4, d - 0.16))
    return n


def toilet():
    n = root('toilet')
    c = WHITE
    n.solidb((0.22, 0.3, 0.3), (0, 0, 0.05), c, bevel=0.02)
    n.solidb((0.38, 0.1, 0.07), (0, 0.3, -0.12), c)
    n.solidb((0.38, 0.1, 0.07), (0, 0.3, 0.25), c)
    n.solidb((0.07, 0.098, 0.298), (-0.155, 0.301, 0.065), c)
    n.solidb((0.07, 0.098, 0.298), (0.155, 0.301, 0.065), c)
    n.solidb((0.24, 0.02, 0.3), (0, 0.26, 0.065), 0x7cc3e0)
    # fluffy pink seat cover (grandma's taste)
    n.box((0.4, 0.025, 0.46), (0, 0.412, 0.065), 0xf4a7c0, bevel=0.008)
    n.box((0.36, 0.42, 0.03), (0, 0.62, -0.17), 0xf4a7c0, rot=(-0.12, 0, 0), bevel=0.01)
    n.col_box((0.36, 0.42, 0.03), (0, 0.62, -0.17), rot=(-0.12, 0, 0))
    n.solidb((0.42, 0.4, 0.18), (0, 0.42, -0.26), c, bevel=0.02)
    n.solidb((0.44, 0.03, 0.2), (0, 0.82, -0.26), c)
    fl = mech(n, 'flush', 'flush', 'button', (0, 0.86, -0.26), 'y', 0, handle=(0, 0.01, 0))
    fl.cyl(0.035, 0.02, (0, 0, 0), METAL, seg=8)
    zone(n, 'toiletBowl', (0, 0.34, 0.065), (0.24, 0.16, 0.3))
    surf(n, 0, 0.12, 0.85, -0.26, 0.1, 0.08)
    return n


def vanity():
    n = root('vanity')
    w, d = 0.7, 0.46
    n.solidb((w, 0.8, d), (0, 0, 0), 0xa7c4c2)
    n.solidb((w + 0.04, 0.05, d + 0.04), (0, 0.8, 0), WHITE)
    n.box((0.4, 0.02, 0.28), (0, 0.86, 0.02), WHITE)
    n.cylb(0.018, 0.16, (0, 0.85, -0.17), METAL, seg=6)
    n.box((0.03, 0.03, 0.1), (0, 1.0, -0.13), METAL)
    n.box((0.3, 0.6, 0.02), (-0.17, 0.4, d / 2 + 0.005), 0xb8d3d1)
    n.box((0.3, 0.6, 0.02), (0.17, 0.4, d / 2 + 0.005), 0xb8d3d1)
    n.box((0.6, 0.7, 0.02), (0, 1.55, -d / 2 + 0.01), 0xcfe8f2)
    n.box((0.66, 0.76, 0.015), (0, 1.55, -d / 2), WOOD_L)
    surf(n, 0, -0.25, 0.86, 0.0, 0.12, 0.3)
    surf(n, 1, 0.25, 0.86, 0.0, 0.12, 0.3)
    return n


def bed():
    n = root('bed')
    n.props['hideUnder'] = 1
    w, L, clear = 1.6, 2.1, 0.3
    legs4(n, w, L, clear, 0.04, WOOD_D, 0.06, square=True)
    n.solidb((w, 0.1, L), (0, clear, 0), WOOD_D)
    n.solidb((w - 0.06, 0.2, L - 0.1), (0, clear + 0.1, 0.03), WHITE, bevel=0.03)
    # patchwork blanket
    cols = [0x4a6aa8, 0x6a8ac8, 0xd9534f, 0xf2cf3c, 0x4d7a4f]
    for i in range(4):
        for j in range(3):
            n.boxb((w / 4, 0.06, L * 0.62 / 3), (-w / 2 + w / 8 + i * w / 4, clear + 0.27, L * 0.19 - L * 0.62 / 3 + j * L * 0.62 / 3), cols[(i + j * 2) % 5])
    n.col_boxb((w, 0.06, L * 0.62), (0, clear + 0.27, L * 0.19))
    n.box((w + 0.02, 0.28, 0.03), (0, clear + 0.18, L / 2 + 0.01), 0x4a6aa8)
    n.boxb((0.6, 0.12, 0.35), (-0.38, clear + 0.3, -L / 2 + 0.3), WHITE, bevel=0.04, wonk=0.01)
    n.boxb((0.6, 0.12, 0.35), (0.38, clear + 0.3, -L / 2 + 0.3), CREAM, bevel=0.04, wonk=0.01)
    n.col_boxb((1.3, 0.12, 0.35), (0, clear + 0.3, -L / 2 + 0.3))
    n.solidb((w + 0.1, 1.05, 0.08), (0, 0, -L / 2 - 0.04), WOOD_D, bevel=0.01)
    n.solidb((w + 0.1, 0.55, 0.06), (0, 0, L / 2 + 0.03), WOOD_D, bevel=0.01)
    for x in (-0.6, -0.2, 0.2, 0.6):
        n.sphere(0.04, (x, 1.07, -L / 2 - 0.04), GOLD, seg=6, rings=4)
    anchor(n, 'sleep', (0, clear + 0.4, 0.1))
    surf(n, 0, 0, clear + 0.33, 0.4, w - 0.4, 0.8)
    return n


def nightstand():
    n = root('nightstand')
    n.solidb((0.45, 0.5, 0.4), (0, 0.05, 0), WOOD_M, bevel=0.01)
    legs4(n, 0.45, 0.4, 0.05, 0.02, WOOD_D, 0.04, collide=False)
    n.box((0.4, 0.2, 0.02), (0, 0.42, 0.205), WOOD_L)
    n.box((0.08, 0.02, 0.02), (0, 0.42, 0.22), BRASS)
    surf(n, 0, 0.08, 0.55, 0.02, 0.2, 0.25)
    return n


def wardrobe():
    n = root('wardrobe')
    n.solidb((1.0, 1.95, 0.55), (0, 0.05, 0), WOOD_M)
    n.solidb((1.04, 0.05, 0.58), (0, 2.0, 0), WOOD_D)
    n.box((0.47, 1.8, 0.02), (-0.245, 1.0, 0.28), WOOD_L)
    n.box((0.47, 1.8, 0.02), (0.245, 1.0, 0.28), WOOD_L)
    n.box((0.02, 0.15, 0.03), (-0.04, 1.1, 0.3), BRASS)
    n.box((0.02, 0.15, 0.03), (0.04, 1.1, 0.3), BRASS)
    # a sleeve sticking out of the door
    n.box((0.1, 0.4, 0.06), (0.1, 0.8, 0.31), 0x5b4636, rot=(0.2, 0, 0.15))
    n.boxb((0.4, 0.25, 0.3), (0.1, 2.05, 0), 0xc4a174)  # old suitcase on top
    surf(n, 0, -0.25, 2.05, 0, 0.2, 0.3)
    return n


def dresser():
    n = root('dresser')
    n.solidb((1.1, 0.8, 0.46), (0, 0.05, 0), WOOD_R)
    legs4(n, 1.1, 0.46, 0.05, 0.025, WOOD_D, 0.05, collide=False)
    for i in range(3):
        n.box((1.02, 0.22, 0.02), (0, 0.2 + i * 0.25, 0.235), 0x9d4a33)
        n.box((0.14, 0.025, 0.02), (0, 0.23 + i * 0.25, 0.25), BRASS)
    n.box((0.7, 0.6, 0.03), (0, 1.2, -0.2), 0xcfe8f2, kind=MAT_GLASS)
    n.box((0.76, 0.66, 0.025), (0, 1.2, -0.215), WOOD_R)
    n.col_box((0.76, 0.66, 0.05), (0, 1.2, -0.21))
    surf(n, 0, 0, 0.85, 0.06, 0.9, 0.2)
    return n


def desk():
    n = root('desk')
    n.solidb((1.3, 0.04, 0.65), (0, 0.72, 0), WOOD_D)
    n.solidb((0.4, 0.72, 0.6), (-0.43, 0, 0), WOOD_M)
    n.solidb((0.04, 0.72, 0.6), (0.62, 0, 0), WOOD_M)
    for y in (0.6, 0.38, 0.16):
        n.box((0.36, 0.18, 0.02), (-0.43, y, 0.305), WOOD_L)
        n.box((0.08, 0.02, 0.02), (-0.43, y, 0.32), BRASS)
    n.boxb((0.6, 0.005, 0.4), (0.15, 0.76, 0.05), GREEN)
    # typewriter-ish clutter
    n.boxb((0.22, 0.08, 0.16), (-0.35, 0.76, -0.15), 0x3a3a3a)
    n.col_boxb((0.22, 0.08, 0.16), (-0.35, 0.76, -0.15))
    surf(n, 0, 0.1, 0.765, 0, 0.9, 0.4)
    return n


def safe():
    n = root('safe')
    w, h, d = 0.55, 0.6, 0.5
    c = 0x39424a
    n.solidb((w, 0.05, d), (0, 0, 0), c)
    n.solidb((w, 0.05, d), (0, h - 0.05, 0), c)
    n.solidb((0.05, h - 0.102, d - 0.002), (-w / 2 + 0.025, 0.051, 0), c)
    n.solidb((0.05, h - 0.102, d - 0.002), (w / 2 - 0.025, 0.051, 0), c)
    n.solidb((w - 0.102, h - 0.102, 0.05), (0, 0.051, -d / 2 + 0.025), c)
    n.box((0.3, 0.05, 0.3), (0, h + 0.005, 0), 0xf5f0e4)  # doily on top, of course
    door = mech(n, 'safeDoor', 'safe', 'hinge', (-w / 2, 0, d / 2), 'y', -1.6, handle=(w / 2, h / 2, 0.05))
    door.boxb((w, h, 0.05), (w / 2, 0, 0), 0x46515a)
    door.col_boxb((w, h, 0.05), (w / 2, 0, 0))
    door.cyl(0.08, 0.03, (w / 2, h / 2, 0.04), GOLD, rot=(R90, 0, 0), seg=10)
    door.cyl(0.02, 0.05, (w / 2, h / 2, 0.06), BLACK, rot=(R90, 0, 0), seg=6)
    door.box((0.04, 0.12, 0.03), (w - 0.07, h / 2, 0.04), METAL)
    zone(n, 'safeInside', (0, h / 2, 0), (w - 0.1, h - 0.1, d - 0.1))
    surf(n, 0, 0, 0.05, 0, 0.2, 0.2)
    return n


def fish_tank():
    n = root('fishTank')
    n.solidb((0.8, 0.7, 0.36), (0, 0, 0), WOOD_D)
    n.solidb((0.72, 0.02, 0.3), (0, 0.7, 0), 0x2b2b2b)
    n.solidb((0.72, 0.4, 0.015), (0, 0.72, -0.145), 0x9ed4ea, kind=MAT_GLASS)
    n.solidb((0.72, 0.4, 0.015), (0, 0.72, 0.145), 0x9ed4ea, kind=MAT_GLASS)
    n.solidb((0.015, 0.4, 0.3), (-0.355, 0.72, 0), 0x9ed4ea, kind=MAT_GLASS)
    n.solidb((0.015, 0.4, 0.3), (0.355, 0.72, 0), 0x9ed4ea, kind=MAT_GLASS)
    n.boxb((0.69, 0.32, 0.27), (0, 0.72, 0), 0x3f9fc7, kind=MAT_GLASS)
    n.boxb((0.69, 0.03, 0.27), (0, 0.72, 0), TERRA)
    n.cone(0.04, 0.2, (-0.2, 0.85, 0), LEAF, seg=5)
    n.cone(0.03, 0.15, (-0.25, 0.82, 0.05), 0x5aa85f, seg=5)
    n.boxb((0.1, 0.08, 0.06), (0.2, 0.75, -0.05), 0x8a8a8a)  # tiny castle
    n.cone(0.03, 0.05, (0.2, 0.855, -0.05), RED, seg=4)
    fish = Node('fish', n, (0, 0.9, 0))
    fish.sphere(0.03, (0.1, 0.02, 0.0), 0xff8a2a, scale=(1.6, 1, 0.6), seg=6, rings=4)
    fish.cone(0.025, 0.03, (0.05, 0.02, 0), 0xff8a2a, rot=(0, 0, R90), seg=4)
    fish.sphere(0.025, (-0.08, -0.05, 0.05), 0xf2cf3c, scale=(1.6, 1, 0.6), seg=6, rings=4)
    zone(n, 'fishTank', (0, 0.9, 0), (0.68, 0.36, 0.26))
    light(n, 'tank', (0, 1.1, 0.3), 0x66c8ff, 1.8, 0.6)
    return n


def coat_rack():
    n = root('coatRack')
    n.cylb(0.2, 0.03, (0, 0, 0), WOOD_D, seg=8)
    n.col_boxb((0.34, 0.03, 0.34))
    n.cylb(0.025, 1.75, (0, 0.03, 0), WOOD_D, seg=6)
    n.col_boxb((0.05, 1.75, 0.05), (0, 0.03, 0))
    n.box((0.4, 0.8, 0.18), (0.1, 1.2, 0.05), 0x5b4636, rot=(0, 0.3, 0), wonk=0.01)
    n.sphere(0.1, (0.05, 1.8, -0.05), 0x3a3a3a, scale=(1.2, 0.5, 1.2), seg=8, rings=4)
    n.cyl(0.02, 0.9, (-0.15, 0.5, 0.1), BLACK, rot=(0, 0, 0.15), seg=5)  # umbrella
    return n


def shoe_rack():
    n = root('shoeRack')
    n.solidb((0.8, 0.03, 0.3), (0, 0.2, 0), WOOD_L)
    n.solidb((0.8, 0.03, 0.3), (0, 0.42, 0), WOOD_L)
    legs4(n, 0.8, 0.3, 0.45, 0.015, WOOD_M, 0.02, square=True)
    for i, x in enumerate((-0.25, -0.1, 0.12, 0.27)):
        n.boxb((0.1, 0.1, 0.27), (x, 0.23, 0), [0x3a2a1a, 0x3a2a1a, 0x6a2a2a, 0x2a2a3a][i], bevel=0.02)
    surf(n, 0, 0, 0.45, 0, 0.6, 0.15)
    return n


def hall_table():
    n = root('hallTable')
    n.solidb((0.9, 0.04, 0.35), (0, 0.8, 0), WOOD_R)
    legs4(n, 0.9, 0.35, 0.8, 0.02, WOOD_D, 0.04)
    n.box((0.5, 0.003, 0.2), (0, 0.842, 0), 0xf5f0e4)
    surf(n, 0, 0, 0.84, 0, 0.7, 0.2)
    return n


def plant(s=1.0, name='plant'):
    n = root(name)
    n.cyl(0.13 * s, 0.35 * s, (0, 0.175 * s, 0), TERRA, r2=0.18 * s, seg=8)
    n.col_boxb((0.3 * s, 0.35 * s, 0.3 * s))
    zone(n, 'plantPot', (0, 0.4 * s, 0), (0.34 * s, 0.14 * s, 0.34 * s))
    n.sphere(0.28 * s, (0, 0.6 * s, 0), LEAF, scale=(1, 1.2, 1), ico=1, wonk=0.03 * s)
    n.sphere(0.2 * s, (0.15 * s, 0.85 * s, 0.05), 0x4fa152, ico=1, wonk=0.03 * s)
    n.sphere(0.18 * s, (-0.12 * s, 0.8 * s, -0.08), 0x357a3a, ico=1, wonk=0.03 * s)
    return n


def cat_bed():
    n = root('catBed')
    n.torus(0.24, 0.07, (0, 0.07, 0), 0x8b6b9e, seg=12, tseg=6)
    n.cylb(0.24, 0.04, (0, 0, 0), 0xb599c4, seg=12)
    n.props['noNav'] = 1
    anchor(n, 'rest', (0, 0.05, 0))
    zone(n, 'catBed', (0, 0.08, 0), (0.44, 0.14, 0.44))
    return n


def ceiling_lamp():
    n = root('ceilingLamp')
    n.cylb(0.008, 0.4, (0, -0.4, 0), BLACK, seg=4)
    n.cyl(0.2, 0.15, (0, -0.45, 0), CREAM, r2=0.06, seg=10)
    n.sphere(0.05, (0, -0.5, 0), 0xfff2c0, seg=6, rings=4, kind=MAT_EMIT)
    n.props['noNav'] = 1
    light(n, 'bulb', (0, -0.58, 0), 0xffd9a0, 7, 1.3)
    return n


def wall_shelf(w=0.8):
    n = root('wallShelf')
    n.solidb((w, 0.03, 0.22), (0, 0, 0), WOOD_M)
    n.box((0.03, 0.1, 0.15), (-w / 2 + 0.08, -0.05, -0.03), DMETAL)
    n.box((0.03, 0.1, 0.15), (w / 2 - 0.08, -0.05, -0.03), DMETAL)
    n.props['noNav'] = 1
    surf(n, 0, 0, 0.03, 0, w - 0.15, 0.12)
    return n


def fireplace():
    n = root('fireplace')
    n.solidb((1.4, 0.1, 0.5), (0, 0, 0), 0x8a8078)
    n.solidb((0.3, 1.0, 0.4), (-0.55, 0.1, -0.05), 0xb05a3c)
    n.solidb((0.3, 1.0, 0.4), (0.55, 0.1, -0.05), 0xb05a3c)
    n.solidb((1.4, 0.3, 0.45), (0, 1.1, -0.03), 0xb05a3c)
    n.solidb((1.6, 0.06, 0.55), (0, 1.4, 0), WOOD_D)
    n.solidb((0.8, 1.0, 0.1), (0, 0.1, -0.2), 0x2a1d18)
    # brick lines
    for y in (0.3, 0.55, 0.8):
        n.box((0.31, 0.01, 0.41), (-0.55, 0.1 + y, -0.05), 0x8a4430)
        n.box((0.31, 0.01, 0.41), (0.55, 0.1 + y, -0.05), 0x8a4430)
    n.cyl(0.05, 0.5, (0, 0.18, 0.0), 0x5a3a22, rot=(0, 0, R90), seg=6)
    n.cyl(0.05, 0.45, (0.05, 0.25, 0.02), 0x4a2e1a, rot=(0, 0.4, R90), seg=6)
    fire = Node('fire', n, (0, 0.2, 0))
    fire.sphere(0.12, (0, 0, 0), 0xff7a2a, scale=(2, 0.6, 0.8), seg=8, rings=5, kind=MAT_EMIT)
    fire.cone(0.08, 0.25, (-0.08, 0.12, 0), 0xffb030, seg=5, kind=MAT_EMIT, wonk=0.01)
    fire.cone(0.06, 0.2, (0.1, 0.1, 0.02), 0xffd050, seg=5, kind=MAT_EMIT, wonk=0.01)
    surf(n, 0, 0, 1.46, 0, 1.2, 0.3)
    light(n, 'fire', (0, 0.35, 0.3), 0xff7a30, 5, 1.1)
    return n


def mushroom_house():
    """The gnomes' spawn mushroom in the garden. Revive fallen gnomes here."""
    n = root('mushroomHouse')
    n.lathe([(0.3, 0), (0.32, 0.15), (0.28, 0.45), (0.3, 0.55), (0.0, 0.55)], (0, 0, 0), 0xf3ead6, seg=12, wonk=0.005)
    n.col_boxb((0.52, 0.55, 0.52))
    cap = Node('cap', n, (0, 0.5, 0))
    cap.lathe([(0.0, -0.02), (0.62, -0.02), (0.66, 0.05), (0.6, 0.18), (0.42, 0.33), (0.2, 0.41), (0.0, 0.43)], (0, 0, 0), 0xd8342c, seg=14, wonk=0.006)
    cap.col_boxb((1.1, 0.35, 1.1), (0, 0.02, 0))
    for i, (a, r, y, s) in enumerate(((0.3, 0.45, 0.2, 0.09), (1.5, 0.5, 0.15, 0.07), (2.6, 0.3, 0.33, 0.08), (3.9, 0.52, 0.12, 0.1), (5.0, 0.35, 0.28, 0.07), (0.9, 0.15, 0.4, 0.06))):
        cap.sphere(s, (math.cos(a) * r, y, math.sin(a) * r), 0xfff8ea, scale=(1, 0.45, 1), seg=6, rings=4)
    n.cylb(0.12, 0.26, (0, 0.03, 0.29), 0x7a4a2a, seg=8)  # round door
    n.cyl(0.02, 0.02, (0.07, 0.15, 0.33), GOLD, rot=(R90, 0, 0), seg=6)
    n.cyl(0.06, 0.02, (0.18, 0.36, 0.27), 0xffe9a0, rot=(R90, 0, -0.5), seg=8, kind=MAT_EMIT)  # warm window
    n.boxb((0.14, 0.02, 0.12), (0, 0.0, 0.42), 0x9a8a78)  # doormat stone
    light(n, 'window', (0.25, 0.4, 0.45), 0xffc070, 2.5, 0.8)
    anchor(n, 'revive', (0, 0, 0.55))
    return n


def stash_basket():
    n = root('stashBasket')
    w, h, d = 0.7, 0.35, 0.55
    c = 0xb8894e
    n.solidb((w, 0.03, d), (0, 0, 0), c)
    for (sz, pos) in (((w, h, 0.03), (0, 0, -d / 2)), ((w, h, 0.03), (0, 0, d / 2)), ((0.03, h - 0.006, d - 0.034), (-w / 2, 0.003, 0)), ((0.03, h - 0.006, d - 0.034), (w / 2, 0.003, 0))):
        n.solidb(sz, pos, c)
    for y in (0.08, 0.18, 0.28):
        n.box((w + 0.01, 0.025, d + 0.01), (0, y, 0), 0x9a6f3a)
    n.torus(0.3, 0.02, (0, h + 0.2, 0), 0x9a6f3a, rot=(R90, 0, 0), seg=10, tseg=4)  # handle
    # flag with a gnome symbol
    n.cylb(0.01, 0.6, (w / 2 - 0.02, h, -d / 2 + 0.02), WOOD_D, seg=4)
    n.box((0.2, 0.13, 0.005), (w / 2 + 0.08, h + 0.5, -d / 2 + 0.02), 0xd8342c)
    zone(n, 'stash', (0, 0.3, 0), (w - 0.05, 0.6, d - 0.05))
    return n


def window():
    """Unit window (1 x 1 m), stretched at runtime to fit the wall opening. Sash slides up."""
    n = root('window')
    fc = 0xf4f0e6
    n.solidb((1.0, 0.06, 0.24), (0, 0, 0.02), fc)  # sill (gnome ledge)
    n.boxb((0.05, 1.0, 0.12), (-0.475, 0, 0), fc)
    n.boxb((0.05, 1.0, 0.12), (0.475, 0, 0), fc)
    n.boxb((1.0, 0.05, 0.12), (0, 0.95, 0), fc)
    n.solidb((0.9, 0.46, 0.02), (0, 0.49, -0.03), 0xbcdbee, kind=MAT_GLASS)  # upper fixed pane
    sash = mech(n, 'sash', 'window', 'slide', (0, 0.05, 0.02), 'y', 0.4, handle=(0, 0.04, 0.03))
    sash.boxb((0.9, 0.04, 0.04), (0, 0, 0), fc)
    sash.boxb((0.9, 0.04, 0.04), (0, 0.42, 0), fc)
    sash.boxb((0.03, 0.46, 0.04), (0, 0, 0), fc)
    sash.boxb((0.86, 0.42, 0.015), (0, 0.02, 0), 0xbcdbee, kind=MAT_GLASS)
    sash.col_boxb((0.9, 0.46, 0.04))
    sash.box((0.08, 0.02, 0.03), (0, 0.035, 0.03), BRASS)
    # curtains (visual)
    n.box((0.12, 1.05, 0.03), (-0.52, 0.5, 0.08), 0xc26a6a, wonk=0.01)
    n.box((0.12, 1.05, 0.03), (0.52, 0.5, 0.08), 0xc26a6a, wonk=0.01)
    return n


MODELS = {
    'sofa': sofa,
    'armchair': armchair,
    'coffeeTable': coffee_table,
    'tvStand': tv_stand,
    'tv': tv,
    'bookshelf': bookshelf,
    'floorLamp': floor_lamp,
    'tableLamp': table_lamp,
    'tableLampGreen': lambda: table_lamp(GREEN, 'tableLampGreen'),
    'sideTable': side_table,
    'rugLiving': lambda: rug('rugLiving', 2.6, 2.2, BURGUNDY, MUSTARD),
    'rugBedroom': lambda: rug('rugBedroom', 1.8, 1.1, 0x5a4a8a, 0xc9b3d9),
    'rugHall': lambda: rug('rugHall', 5.5, 0.8, 0x7e2833, 0xcf9d33),
    'rugStudy': lambda: rug('rugStudy', 2.0, 1.6, NAVY, 0xa33a3a),
    'grandfatherClock': grandfather_clock,
    'counter': counter,
    'sinkCounter': sink_counter,
    'stove': stove,
    'fridge': fridge,
    'kitchenTable': kitchen_table,
    'bathtub': bathtub,
    'toilet': toilet,
    'vanity': vanity,
    'bed': bed,
    'nightstand': nightstand,
    'wardrobe': wardrobe,
    'dresser': dresser,
    'desk': desk,
    'safe': safe,
    'fishTank': fish_tank,
    'coatRack': coat_rack,
    'shoeRack': shoe_rack,
    'hallTable': hall_table,
    'plant': plant,
    'plantBig': lambda: plant(1.25, 'plantBig'),
    'catBed': cat_bed,
    'ceilingLamp': ceiling_lamp,
    'wallShelf': wall_shelf,
    'fireplace': fireplace,
    'window': window,
}
