"""Loose physics items (loot). Authored in METRES (k = HS), origin roughly at the centre of mass.

Items without COL_ markers get a convex-hull collider at runtime; items with COL_ boxes
(chair, box, trash bin...) use those instead.
"""
import math

from gnomelib.core import (HS, MAT_EMIT, MAT_FABRIC, MAT_FUR, MAT_GLASS, MAT_GLOSSY, MAT_HAIR, MAT_KNIT, MAT_LEATHER,
                           MAT_METAL, MAT_SKIN, MAT_STONE, MAT_WOOD, Node)

WHITE = 0xf1efe8
METAL = 0xb9c0c8
STEEL = 0x8e969f
DMETAL = 0x3d4148
BLACK = 0x1d1e22
GOLD = 0xe8b83a
BRASS = 0xc6a14a
RED = 0xc23b30
NAVY = 0x2c3b63
CREAM = 0xf3e3bf
WOOD_M = 0x9a6236
WOOD_L = 0xc89560
WOOD_D = 0x6a3f22
YELLOW = 0xf2cf3c
PINK = 0xe79aa8
R90 = math.pi / 2


def item(name):
    n = Node(name, k=HS)
    n.props['kind'] = name
    return n


def mug():
    n = item('mug')
    n.lathe([(0.04, -0.05), (0.045, -0.045), (0.047, 0.05), (0.041, 0.05), (0.038, -0.04), (0.0, -0.04)], (0, 0, 0), WHITE, seg=10, cap_top=False)
    n.torus(0.028, 0.009, (0.052, 0.0, 0), WHITE, rot=(R90, 0, 0), seg=8, tseg=4)
    n.cyl(0.039, 0.004, (0, 0.03, 0), 0x5a3520, seg=10)
    n.box((0.05, 0.03, 0.002), (0, 0.0, 0.047), RED)  # "WORLD'S BEST GRANDPA"
    return n


def plate():
    n = item('plate')
    n.lathe([(0.0, -0.009), (0.08, -0.009), (0.12, 0.006), (0.125, 0.01), (0.0, 0.004)], (0, 0, 0), WHITE, seg=14)
    n.cyl(0.065, 0.003, (0, 0.005, 0), 0x9ec3e0, seg=12)
    return n


def teapot():
    n = item('teapot')
    n.sphere(0.08, (0, 0, 0), 0xe9e2d0, scale=(1, 0.8, 1), seg=10, rings=6)
    n.cyl(0.012, 0.09, (0.09, 0.02, 0), 0xe9e2d0, rot=(0, 0, -0.9), seg=6)
    n.torus(0.04, 0.01, (-0.08, 0.01, 0), 0xe9e2d0, rot=(R90, 0, 0), seg=8, tseg=4)
    n.sphere(0.02, (0, 0.07, 0), 0x3f6fb5, seg=6, rings=4)
    for a in (0, 2.1, 4.2):
        n.sphere(0.015, (math.cos(a) * 0.075, 0.0, math.sin(a) * 0.075), 0x3f6fb5, seg=5, rings=3)
    return n


def kettle():
    n = item('kettle')
    n.cyl(0.075, 0.15, (0, 0, 0), STEEL, r2=0.06, seg=10)
    n.box((0.02, 0.08, 0.1), (0, 0.1, 0), BLACK)
    n.cyl(0.012, 0.08, (0.08, 0.02, 0), STEEL, rot=(0, 0, -0.9), seg=6)
    return n


def toaster():
    n = item('toaster')
    n.box((0.26, 0.17, 0.14), (0, 0, 0), 0xbfc5cc, bevel=0.02)
    n.box((0.18, 0.01, 0.03), (0, 0.086, -0.03), BLACK)
    n.box((0.18, 0.01, 0.03), (0, 0.086, 0.03), BLACK)
    n.box((0.02, 0.04, 0.02), (0.14, 0.03, 0), BLACK)
    # two burnt toasts sticking out
    n.box((0.12, 0.06, 0.012), (0, 0.1, -0.03), 0x6a3a1a, rot=(0, 0, 0.05))
    n.box((0.12, 0.05, 0.012), (0.01, 0.1, 0.03), 0x8a4a22, rot=(0, 0, -0.08))
    return n


def pot():
    n = item('pot')
    n.cyl(0.1, 0.1, (0, 0, 0), 0x9aa0a8, seg=12)
    n.box((0.04, 0.015, 0.03), (0.12, 0.03, 0), BLACK)
    n.box((0.04, 0.015, 0.03), (-0.12, 0.03, 0), BLACK)
    n.cyl(0.098, 0.005, (0, 0.051, 0), 0xb8753a, seg=12)  # soup!
    return n


def pan():
    n = item('pan')
    n.cyl(0.12, 0.03, (0, 0, 0), DMETAL, r2=0.13, seg=12)
    n.box((0.2, 0.018, 0.03), (0.22, 0.0, 0), BLACK)
    n.cyl(0.03, 0.003, (0.02, 0.016, 0.02), 0xfff0a0, seg=8)  # fried egg
    n.cyl(0.012, 0.004, (0.02, 0.018, 0.02), 0xf2a02a, seg=6)
    return n


def spoon():
    n = item('spoon')
    n.box((0.12, 0.012, 0.018), (-0.02, 0, 0), METAL)
    n.sphere(0.022, (0.06, 0, 0), METAL, scale=(1.3, 0.4, 1), seg=6, rings=4)
    return n


def fork():
    n = item('fork')
    n.box((0.11, 0.012, 0.018), (-0.025, 0, 0), METAL)
    for z in (-0.009, -0.003, 0.003, 0.009):
        n.box((0.05, 0.008, 0.004), (0.055, 0, z), METAL)
    return n


def can():
    n = item('can')
    n.cyl(0.035, 0.1, (0, 0, 0), RED, seg=10)
    n.cyl(0.036, 0.04, (0, 0.0, 0), WHITE, seg=10)
    n.cyl(0.036, 0.004, (0, 0.05, 0), METAL, seg=10)
    return n


def bottle():
    n = item('bottle')
    n.lathe([(0.0, -0.1), (0.035, -0.1), (0.035, 0.03), (0.014, 0.07), (0.014, 0.12), (0.0, 0.12)], (0, 0, 0), 0x2f7a4a, seg=8)
    n.cyl(0.036, 0.05, (0, -0.03, 0), CREAM, seg=8)
    n.cyl(0.016, 0.02, (0, 0.12, 0), 0xc7a24a, seg=6)
    return n


def cheese():
    n = item('cheese')
    n.poly_prism([(-0.06, -0.03), (0.06, -0.03), (-0.06, 0.03)], 0.09, (0, 0, 0), YELLOW)
    for (x, y) in ((-0.03, -0.01), (0.0, -0.015), (-0.045, 0.012)):
        n.sphere(0.008, (x, y, 0.045), 0xd4a820, seg=5, rings=3)
    return n


def apple():
    n = item('apple')
    n.sphere(0.04, (0, 0, 0), 0xc8302a, seg=8, rings=6, scale=(1, 0.9, 1))
    n.cyl(0.004, 0.02, (0, 0.042, 0), 0x5a3a1a, seg=4)
    n.box((0.02, 0.003, 0.01), (0.01, 0.05, 0), 0x3f8f45, rot=(0, 0, 0.4))
    # a tiny worm peeking out
    n.sphere(0.006, (0.03, 0.02, 0.025), 0xf0a0b0, seg=5, rings=3)
    return n


def sausage():
    n = item('sausage')
    n.cyl(0.025, 0.14, (0, 0, 0), 0xa3452f, rot=(0, 0, R90), seg=8)
    n.sphere(0.025, (0.07, 0, 0), 0xa3452f, seg=8, rings=4)
    n.sphere(0.025, (-0.07, 0, 0), 0xa3452f, seg=8, rings=4)
    return n


def bread():
    n = item('bread')
    n.box((0.2, 0.07, 0.1), (0, -0.01, 0), 0xc98a47, bevel=0.02)
    n.sphere(0.05, (0, 0.02, 0), 0xc98a47, scale=(2, 0.7, 1), seg=8, rings=5)
    for x in (-0.05, 0, 0.05):
        n.box((0.01, 0.005, 0.06), (x, 0.055, 0), 0xe0b070, rot=(0, 0.5, 0))
    return n


def cat_bowl():
    n = item('catBowl')
    n.cyl(0.06, 0.05, (0, 0, 0), PINK, r2=0.08, seg=10)
    n.cyl(0.065, 0.006, (0, 0.022, 0), 0x8a5a2a, seg=10)
    n.box((0.05, 0.02, 0.002), (0, 0.0, 0.07), WHITE, rot=(-0.4, 0, 0))  # "BARSIK"
    return n


def cookie_jar():
    n = item('cookieJar')
    n.cyl(0.08, 0.16, (0, 0, 0), 0xd9a066, seg=10)
    n.cyl(0.07, 0.03, (0, 0.09, 0), 0xc08050, seg=10)
    n.sphere(0.03, (0, 0.11, 0), 0xc08050, seg=6, rings=4)
    n.box((0.08, 0.05, 0.002), (0, 0.0, 0.08), CREAM)
    return n


def cookie():
    n = item('cookie')
    n.cyl(0.03, 0.012, (0, 0, 0), 0xb97d3f, seg=8)
    for (x, z) in ((0.01, 0.01), (-0.012, 0.0), (0.0, -0.013)):
        n.sphere(0.005, (x, 0.006, z), 0x4a2a1a, seg=4, rings=3)
    return n


def remote():
    n = item('remote')
    n.box((0.05, 0.022, 0.16), (0, 0, 0), BLACK, bevel=0.005)
    n.cyl(0.008, 0.006, (0, 0.013, -0.055), RED, seg=6)
    for i in range(3):
        for j in range(3):
            n.box((0.009, 0.004, 0.009), (-0.013 + i * 0.013, 0.012, -0.01 + j * 0.018), 0x777777)
    n.box((0.03, 0.023, 0.03), (0, 0.0, 0.06), 0x9a9a9a)  # duct tape
    return n


def vase():
    n = item('vase')
    n.lathe([(0.0, -0.13), (0.05, -0.13), (0.075, -0.06), (0.07, 0.01), (0.035, 0.07), (0.04, 0.1), (0.0, 0.1)], (0, 0, 0), 0x3d6fb0, seg=10)
    n.torus(0.07, 0.006, (0, -0.04, 0), WHITE, seg=10, tseg=3)
    for a in range(3):
        ang = a * 2.1
        n.cyl(0.004, 0.12, (math.cos(ang) * 0.015, 0.15, math.sin(ang) * 0.015), 0x3f8f45, rot=(math.sin(ang) * 0.3, 0, math.cos(ang) * 0.3), seg=4)
        n.sphere(0.02, (math.cos(ang) * 0.04, 0.21, math.sin(ang) * 0.04), [0xe2584c, 0xf2cf3c, 0xffffff][a], seg=6, rings=4)
    return n


def book():
    n = item('book')
    n.box((0.15, 0.04, 0.21), (0, 0, 0), NAVY)
    n.box((0.14, 0.034, 0.2), (0.006, 0, 0), CREAM)
    n.box((0.01, 0.042, 0.212), (-0.072, 0, 0), 0x1f2a4a)
    return n


def photo_frame():
    n = item('photoFrame')
    n.box((0.14, 0.18, 0.02), (0, 0, 0), GOLD, bevel=0.005)
    n.box((0.11, 0.14, 0.004), (0, 0, 0.01), 0xb2c9a8)
    n.sphere(0.03, (0, 0.0, 0.012), 0xe8b49a, scale=(1, 1.2, 0.2), seg=6, rings=4)  # grandma portrait
    n.box((0.05, 0.03, 0.004), (0, 0.035, 0.012), 0xcccccc)
    return n


def candle():
    n = item('candle')
    n.cyl(0.02, 0.12, (0, 0, 0), CREAM, seg=8)
    n.cone(0.008, 0.025, (0, 0.075, 0), 0xffb040, seg=5, kind=MAT_EMIT)
    n.cyl(0.03, 0.01, (0, -0.06, 0), BRASS, seg=8)
    return n


def globe():
    n = item('globe')
    n.sphere(0.12, (0, 0.04, 0), 0x3f7fc0, ico=2)
    for (a, b) in ((0.4, 0.3), (2.5, -0.2), (4.0, 0.5)):
        n.sphere(0.05, (math.cos(a) * 0.1, 0.04 + b * 0.1, math.sin(a) * 0.1), 0x5aa85f, scale=(1, 0.8, 0.5), rot=(0, -a, 0), seg=6, rings=4)
    n.torus(0.13, 0.008, (0, 0.04, 0), BRASS, rot=(R90, 0, 0.4), seg=12, tseg=3)
    n.cyl(0.06, 0.02, (0, -0.14, 0), BRASS, seg=8)
    n.cyl(0.01, 0.08, (0, -0.1, 0), BRASS, seg=5)
    return n


def trophy():
    n = item('trophy')
    n.box((0.1, 0.04, 0.1), (0, -0.1, 0), WOOD_D)
    n.cyl(0.015, 0.08, (0, -0.04, 0), GOLD, seg=6)
    n.cyl(0.03, 0.1, (0, 0.05, 0), GOLD, r2=0.07, seg=10)
    n.torus(0.03, 0.007, (-0.07, 0.05, 0), GOLD, rot=(R90, 0, 0), seg=8, tseg=3)
    n.torus(0.03, 0.007, (0.07, 0.05, 0), GOLD, rot=(R90, 0, 0), seg=8, tseg=3)
    n.box((0.05, 0.015, 0.002), (0, -0.1, 0.051), CREAM)  # "BEST BOWLING 1974"
    return n


def piggy_bank():
    n = item('piggyBank')
    n.sphere(0.09, (0, 0, 0), PINK, scale=(1.3, 1, 1), seg=10, rings=7)
    n.cyl(0.03, 0.03, (0.12, 0.0, 0), PINK, rot=(0, 0, R90), seg=8)
    n.sphere(0.006, (0.136, 0.005, 0.01), 0xb05a6a, seg=4, rings=3)
    n.sphere(0.006, (0.136, 0.005, -0.01), 0xb05a6a, seg=4, rings=3)
    n.cone(0.025, 0.04, (0.07, 0.09, 0.04), PINK, seg=4, rot=(0.3, 0, -0.2))
    n.cone(0.025, 0.04, (0.07, 0.09, -0.04), PINK, seg=4, rot=(-0.3, 0, -0.2))
    for sx in (-1, 1):
        for sz in (-1, 1):
            n.cyl(0.018, 0.04, (sx * 0.06, -0.08, sz * 0.045), PINK, seg=6)
    n.sphere(0.01, (0.1, 0.035, 0.035), BLACK, seg=4, rings=3)
    n.sphere(0.01, (0.1, 0.035, -0.035), BLACK, seg=4, rings=3)
    n.box((0.04, 0.005, 0.008), (0, 0.09, 0), BLACK)
    return n


def alarm_clock():
    n = item('alarmClock')
    n.cyl(0.06, 0.05, (0, 0, 0), RED, rot=(R90, 0, 0), seg=10)
    n.cyl(0.05, 0.01, (0, 0, 0.026), CREAM, rot=(R90, 0, 0), seg=10)
    n.box((0.004, 0.035, 0.004), (0, 0.015, 0.032), BLACK)
    n.box((0.004, 0.025, 0.004), (0.01, 0.0, 0.032), BLACK, rot=(0, 0, -1.2))
    n.sphere(0.025, (-0.04, 0.06, 0), BRASS, scale=(1, 0.7, 1), seg=6, rings=4)
    n.sphere(0.025, (0.04, 0.06, 0), BRASS, scale=(1, 0.7, 1), seg=6, rings=4)
    n.box((0.01, 0.03, 0.01), (0, 0.065, 0), BRASS)
    n.cyl(0.008, 0.03, (-0.04, -0.06, 0), BLACK, seg=4)
    n.cyl(0.008, 0.03, (0.04, -0.06, 0), BLACK, seg=4)
    return n


def telephone():
    n = item('telephone')
    n.box((0.18, 0.08, 0.16), (0, 0, 0), 0x2b2b2b, bevel=0.015, taper=(0.85, 0.8))
    n.box((0.2, 0.04, 0.05), (0, 0.06, -0.02), 0x2b2b2b, bevel=0.01)
    n.cyl(0.05, 0.01, (0, 0.036, 0.045), CREAM, rot=(-0.4, 0, 0), seg=10)
    for i in range(8):
        a = i * 0.7
        n.cyl(0.006, 0.012, (math.cos(a) * 0.033, 0.042, 0.045 + math.sin(a) * 0.02), 0x2b2b2b, rot=(-0.4, 0, 0), seg=4)
    return n


def yarn():
    n = item('yarn')
    n.sphere(0.06, (0, 0, 0), 0xd0506a, ico=1, wonk=0.004)
    n.torus(0.058, 0.004, (0, 0, 0), 0xe0708a, rot=(0.5, 0, 0.3), seg=10, tseg=3)
    n.torus(0.058, 0.004, (0, 0, 0), 0xe0708a, rot=(-0.6, 0.5, 0), seg=10, tseg=3)
    n.cyl(0.004, 0.1, (0.06, -0.05, 0), 0xd0506a, rot=(0, 0, 1.2), seg=4)
    return n


def coins():
    n = item('coins')
    for i in range(5):
        n.cyl(0.03, 0.006, (0.004 * (i % 2), -0.012 + i * 0.006, 0.003 * (i % 3)), GOLD, seg=10)
    return n


def ring():
    n = item('ring')
    n.torus(0.02, 0.007, (0, 0, 0), GOLD, seg=10, tseg=4)
    n.sphere(0.013, (0.02, 0.012, 0), 0x7fd8ff, ico=1, kind=MAT_EMIT)
    return n


def watch():
    n = item('watch')
    n.cyl(0.03, 0.015, (0, 0, 0), GOLD, seg=10)
    n.cyl(0.024, 0.017, (0, 0.001, 0), CREAM, seg=10)
    n.box((0.002, 0.003, 0.018), (0, 0.01, 0.006), BLACK)
    n.box((0.1, 0.008, 0.022), (0, -0.004, 0), 0x5a3520)
    return n


def pearls():
    n = item('pearls')
    for i in range(14):
        a = i / 14 * math.pi * 2
        n.sphere(0.009, (math.cos(a) * 0.045, 0, math.sin(a) * 0.05), 0xf2eee6, seg=5, rings=3)
    return n


def jewel_box():
    n = item('jewelBox')
    n.box((0.16, 0.08, 0.11), (0, 0, 0), 0x8b2d4a, bevel=0.008)
    n.box((0.16, 0.02, 0.11), (0, 0.05, 0), 0xa33a5c, bevel=0.005)
    n.box((0.03, 0.02, 0.01), (0, 0.02, 0.06), GOLD)
    n.sphere(0.012, (0.05, 0.065, 0.02), 0x7fd8ff, seg=5, rings=3, kind=MAT_EMIT)
    return n


def gold_bar():
    n = item('goldBar')
    n.box((0.1, 0.035, 0.05), (0, 0, 0), GOLD, taper=(0.8, 0.75))
    n.box((0.04, 0.002, 0.015), (0, 0.018, 0), 0xc99a20)
    return n


def cash():
    n = item('cash')
    n.box((0.14, 0.02, 0.07), (0, 0, 0), 0x7fb07a)
    n.box((0.02, 0.022, 0.071), (0, 0, 0), 0xe7e0c0)
    return n


def pipe():
    n = item('pipe')
    n.cyl(0.02, 0.05, (0, 0, 0), 0x5a3520, seg=8)
    n.cyl(0.006, 0.1, (0.06, -0.015, 0), 0x2a1a10, rot=(0, 0, 1.3), seg=5)
    n.cyl(0.016, 0.003, (0, 0.024, 0), 0x2a1a10, seg=8)
    return n


def dentures():
    n = item('dentures')
    n.sphere(0.035, (0, -0.008, 0), 0xf0a0a8, scale=(1.2, 0.45, 1), seg=8, rings=5)
    n.sphere(0.035, (0, 0.012, 0), 0xf0a0a8, scale=(1.2, 0.45, 1), seg=8, rings=5)
    for i in range(7):
        a = -1.0 + i * (2.0 / 6)
        n.box((0.01, 0.014, 0.008), (math.sin(a) * 0.036, 0.004, math.cos(a) * 0.028), WHITE, rot=(0, a, 0))
    n.box((0.008, 0.012, 0.007), (0.012, 0.004, 0.03), GOLD)  # gold tooth
    return n


def glasses():
    n = item('glasses')
    n.box((0.13, 0.008, 0.008), (0, 0.015, 0), 0x3a2a1a)
    for x in (-0.032, 0.032):
        n.torus(0.022, 0.004, (x, 0, 0.004), 0x3a2a1a, rot=(R90, 0, 0), seg=10, tseg=3)
        n.cyl(0.02, 0.003, (x, 0, 0.004), 0xbfe6f5, rot=(R90, 0, 0), seg=10, kind=MAT_GLASS)
    n.box((0.005, 0.005, 0.1), (-0.064, 0.005, -0.05), 0x3a2a1a)
    n.box((0.005, 0.005, 0.1), (0.064, 0.005, -0.05), 0x3a2a1a)
    return n


def hearing_aid():
    n = item('hearingAid')
    n.sphere(0.018, (0, 0, 0), 0xe8b49a, scale=(1, 1.4, 0.8), seg=6, rings=4)
    n.cyl(0.004, 0.03, (0, -0.03, 0.01), 0xd8a48a, rot=(0.4, 0, 0), seg=4)
    return n


def slipper():
    n = item('slipper')
    n.box((0.1, 0.03, 0.26), (0, -0.01, 0), 0x7a3b2e, bevel=0.01)
    n.sphere(0.055, (0, 0.015, 0.06), 0x8c4a3a, scale=(1, 0.6, 1.3), seg=8, rings=5)
    n.sphere(0.02, (0, 0.04, 0.1), 0xf2eee6, ico=1, wonk=0.004)  # pompom
    return n


def sock():
    """A lonely sock (the gnomes' favourite loot): knitted, a bit flat, heel and toe in another colour."""
    n = item('sock')
    n.detail = 0.6
    body, heel = 0x9aa8c4, RED
    n.sweep([(0, 0, -0.1), (0, 0, 0.0), (0.008, 0, 0.045), (0.035, 0, 0.085), (0.055, 0, 0.115)],
            [0.03, 0.029, 0.028, 0.026, 0.021], body, kind=MAT_KNIT, seg=16, steps=4, start='open', end='dome',
            squash=lambda t: (0.42, 1.0), ribs=lambda t: 0.05 if t < 0.2 else 0.0, rim=(0.004, 0.05, 0x3a3440),
            paint=lambda t, a: heel if (t > 0.86 or 0.5 < t < 0.64) else (0xf3ead6 if 0.2 < t < 0.26 else body))
    n.blob(0.011, (0.012, 0.011, -0.02), 0xf0c0a0, kind=MAT_SKIN, detail=1, scale=(1, 0.3, 1))  # hole: a toe peeks out
    return n


def duck():
    n = item('duck')
    n.sphere(0.05, (0, 0, 0), YELLOW, scale=(1.2, 0.9, 1), seg=8, rings=6)
    n.sphere(0.032, (0.035, 0.05, 0), YELLOW, seg=8, rings=5)
    n.cone(0.014, 0.03, (0.075, 0.048, 0), 0xe07b2e, rot=(0, 0, -R90), seg=5)
    n.sphere(0.006, (0.055, 0.062, 0.02), BLACK, seg=4, rings=3)
    n.sphere(0.006, (0.055, 0.062, -0.02), BLACK, seg=4, rings=3)
    n.cone(0.02, 0.03, (-0.06, 0.015, 0), YELLOW, rot=(0, 0, R90 + 0.5), seg=4)
    return n


def toilet_paper():
    n = item('toiletPaper')
    n.cyl(0.055, 0.1, (0, 0, 0), WHITE, seg=10)
    n.cyl(0.02, 0.102, (0, 0, 0), 0x9a7a5a, seg=6)
    n.box((0.05, 0.08, 0.004), (0.02, -0.03, 0.055), WHITE, rot=(0.1, 0, 0))  # dangling sheet
    return n


def soap():
    n = item('soap')
    n.box((0.08, 0.03, 0.05), (0, 0, 0), PINK, bevel=0.01)
    for (x, y) in ((0.03, 0.02), (-0.02, 0.025), (0.0, 0.03)):
        n.sphere(0.008, (x, y, 0), WHITE, seg=4, rings=3)
    return n


def shampoo():
    n = item('shampoo')
    n.box((0.06, 0.16, 0.035), (0, 0, 0), 0x2f7f86, bevel=0.008)
    n.cyl(0.012, 0.03, (0, 0.095, 0), WHITE, seg=6)
    n.box((0.04, 0.06, 0.002), (0, 0.0, 0.018), WHITE)
    return n


def toothbrush():
    n = item('toothbrush')
    n.box((0.17, 0.012, 0.015), (0, 0, 0), 0x3f6fb5)
    n.box((0.03, 0.02, 0.014), (0.07, 0.012, 0), WHITE)
    return n


def denture_glass():
    n = item('dentureGlass')
    n.cyl(0.04, 0.1, (0, 0, 0), 0xcfe8f2, seg=8, kind=MAT_GLASS)
    return n


def chair():
    n = item('chair')
    oy = -0.45  # origin near the centre of mass
    n.boxb((0.42, 0.04, 0.42), (0, 0.43 + oy, 0), WOOD_L, bevel=0.005)
    n.col_boxb((0.42, 0.04, 0.42), (0, 0.43 + oy, 0))
    for sx in (-1, 1):
        for sz in (-1, 1):
            n.boxb((0.035, 0.43, 0.035), (sx * 0.18, oy, sz * 0.18), WOOD_M)
            n.col_boxb((0.035, 0.43, 0.035), (sx * 0.18, oy, sz * 0.18))
    n.boxb((0.42, 0.25, 0.03), (0, 0.67 + oy, -0.195), WOOD_L, bevel=0.005)
    n.col_boxb((0.42, 0.25, 0.03), (0, 0.67 + oy, -0.195))
    for x in (-0.18, 0.18):
        n.boxb((0.035, 0.26, 0.03), (x, 0.47 + oy, -0.195), WOOD_M)
    n.boxb((0.36, 0.03, 0.36), (0, 0.47 + oy, 0), 0xc26a6a, bevel=0.01)  # cushion
    return n


def trash_bin():
    n = item('trashBin')
    n.cyl(0.11, 0.38, (0, 0, 0), STEEL, r2=0.13, seg=10)
    n.cyl(0.135, 0.02, (0, 0.2, 0), DMETAL, seg=10)
    n.col_box((0.22, 0.4, 0.22), (0, 0.0, 0))
    n.box((0.08, 0.05, 0.004), (0.04, 0.22, 0.02), 0xe0d8c0, rot=(0.3, 0.2, 0.6))  # banana peel-ish
    return n


def cardboard_box():
    n = item('box')
    n.box((0.3, 0.2, 0.25), (0, 0, 0), 0xc4a174)
    n.box((0.31, 0.02, 0.05), (0, 0.1, 0), 0xd9c3a0)
    n.box((0.12, 0.06, 0.002), (0, 0.03, 0.126), 0x3a3a3a)  # "FRAGILE"
    n.col_box((0.3, 0.2, 0.25))
    return n


def battery():
    n = item('battery')
    n.cyl(0.012, 0.05, (0, 0, 0), 0x2b2b2b, rot=(0, 0, R90), seg=6)
    n.cyl(0.0122, 0.015, (0.019, 0, 0), 0xe07b2e, rot=(0, 0, R90), seg=6)
    return n


def lightbulb():
    n = item('lightbulb')
    n.sphere(0.03, (0, 0.02, 0), 0xfff5d0, seg=8, rings=5, kind=MAT_GLASS)
    n.cyl(0.014, 0.025, (0, -0.02, 0), METAL, seg=6)
    return n


def shard():
    n = item('shard')
    n.poly_prism([(-0.02, -0.012), (0.025, -0.005), (-0.005, 0.018)], 0.006, (0, 0, 0), WHITE, rot=(R90, 0, 0))
    return n


MODELS = {
    'mug': mug, 'plate': plate, 'teapot': teapot, 'kettle': kettle, 'toaster': toaster, 'pot': pot, 'pan': pan,
    'spoon': spoon, 'fork': fork, 'can': can, 'bottle': bottle, 'cheese': cheese, 'apple': apple,
    'sausage': sausage, 'bread': bread, 'catBowl': cat_bowl, 'cookieJar': cookie_jar, 'cookie': cookie,
    'remote': remote, 'vase': vase, 'book': book, 'photoFrame': photo_frame, 'candle': candle, 'globe': globe,
    'trophy': trophy, 'piggyBank': piggy_bank, 'alarmClock': alarm_clock, 'telephone': telephone, 'yarn': yarn,
    'coins': coins, 'ring': ring, 'watch': watch, 'pearls': pearls, 'jewelBox': jewel_box, 'goldBar': gold_bar,
    'cash': cash, 'pipe': pipe, 'dentures': dentures, 'glasses': glasses, 'hearingAid': hearing_aid,
    'slipper': slipper, 'sock': sock, 'duck': duck, 'toiletPaper': toilet_paper, 'soap': soap,
    'shampoo': shampoo, 'toothbrush': toothbrush, 'dentureGlass': denture_glass, 'chair': chair,
    'trashBin': trash_bin, 'box': cardboard_box, 'battery': battery, 'lightbulb': lightbulb, 'shard': shard,
}
