"""Characters: the gnome (player), the grumpy old man, the cat, the robot vacuum, the High-Gnome."""
import math

from gnomelib.core import (MAT_EMIT, MAT_FABRIC, MAT_FUR, MAT_GLASS, MAT_GLOSSY, MAT_HAIR, MAT_KNIT, MAT_LEATHER,
                           MAT_METAL, MAT_SKIN, MAT_TINT, Node)

R90 = math.pi / 2

SKIN = 0xf2b393
NOSE = 0xe86f63
BEARD = 0xf4f0e6
TUNIC = 0x5b8c3c
PANTS = 0x7a5230
BOOT = 0x4a2f1c
BELT = 0x3a2412
GOLD = 0xe8b83a
HAT = 0xd8342c
MITT = 0xc9892e
EYE_W = 0xffffff
EYE_B = 0x1b1b1f


def gnome():
    """Player gnome. ~1.0 unit tall (1.3 with the hat), origin at the feet, front = +Z.

    Nodes used by the game at runtime:
      body (hips pivot) > head > hat > hatMid > hatTip   (floppy hat wobble)
      armL / armR   (shoulder pivots; mesh is a unit-length tube along +Z, scaled at runtime)
      handL / handR (placed at runtime)
      legL / legR   (hip joints, swing when walking)
    Organic parts are fused smooth meshes; the hat is knitted and tinted per player.
    """
    root = Node('gnome')
    root.props['kind'] = 'gnome'
    root.detail = 0.8

    # --- legs: short trouser stubs with curly-toed leather boots ---
    for side, x in (('L', -0.1), ('R', 0.1)):
        leg = Node('leg' + side, root, (x, 0.27, 0.0), surface=MAT_FABRIC)
        leg.sweep([(0, 0.02, 0), (0, -0.1, 0.0), (0, -0.17, 0.0)], [0.068, 0.062, 0.058], PANTS, seg=14, steps=3,
                  start='dome', end='flat')
        leg.sweep([(0, -0.14, -0.045), (0, -0.19, -0.02), (0, -0.2, 0.08), (0, -0.177, 0.16), (0, -0.135, 0.19)],
                  [0.07, 0.072, 0.064, 0.045, 0.022], BOOT, kind=MAT_LEATHER, seg=16, steps=4)
        leg.blob(0.024, (0, -0.13, 0.2), GOLD, kind=MAT_METAL, detail=2)  # tiny bell on the curled toe
        leg.sweep([(-0.072, -0.145, 0.0), (0, -0.14, 0.074), (0.072, -0.145, 0.0)], [0.012] * 3, BELT,
                  kind=MAT_LEATHER, seg=6, steps=4, start='flat', end='flat')  # boot strap

    # --- body (pivot at the hips): one pear-shaped tunic with trousers and belt painted on ---
    def tunic(p, n, mat):
        y = p[1]
        if y < 0.07:
            return MAT_FABRIC, PANTS
        if y < 0.135:
            return MAT_LEATHER, BELT
        return MAT_FABRIC, TUNIC

    body = Node('body', root, (0, 0.27, 0), surface=MAT_FABRIC)
    with body.fuse(voxel=0.016, smooth=0.6, iterations=5, decimate=0.15, paint=tunic):
        body.blob(0.265, (0, 0.21, 0), TUNIC, scale=(1.0, 1.0, 0.92))
        body.blob(0.25, (0, 0.08, 0.02), TUNIC, scale=(1.02, 0.8, 0.95))
        for x in (-0.18, 0.18):
            body.blob(0.085, (x, 0.34, 0.0), TUNIC)
    # brass belt buckle
    body.lathe([(0.0, 0.0), (0.055, 0.0), (0.06, 0.012), (0.05, 0.02), (0.0, 0.02)], (0, 0.103, 0.245), GOLD,
               rot=(R90, 0, 0), seg=4, kind=MAT_METAL)
    body.box((0.05, 0.03, 0.02), (0, 0.103, 0.265), BELT, kind=MAT_LEATHER, bevel=0.005)
    # loot sack on the back, tied with a cord
    with body.fuse(voxel=0.012, smooth=0.5, iterations=4, decimate=0.3):
        body.blob(0.13, (0.02, 0.22, -0.25), 0xb58a54, scale=(1.0, 1.15, 0.85), kind=MAT_FABRIC)
        body.blob(0.045, (0.02, 0.37, -0.26), 0xb58a54, scale=(1.0, 1.3, 1.0), kind=MAT_FABRIC)
    body.sweep([(0.02 + 0.045 * math.cos(a), 0.345, -0.26 + 0.045 * math.sin(a)) for a in
                [i * math.pi / 5 for i in range(11)]], [0.011] * 11, 0x6b4a2a, kind=MAT_FABRIC, seg=6, steps=2,
               start='flat', end='flat')
    body.sweep([(0.14, 0.4, -0.08), (0.2, 0.25, -0.14), (0.13, 0.12, -0.2)], [0.014] * 3, 0x6b4a2a,
               kind=MAT_LEATHER, seg=6, steps=4)  # strap

    # --- head: round face, potato nose, googly eyes, huge beard ---
    head = Node('head', body, (0, 0.34, 0.02), surface=MAT_SKIN)
    with head.fuse(voxel=0.011, smooth=0.5, iterations=4, decimate=0.18):
        head.blob(0.16, (0, 0.1, 0.0), SKIN, scale=(1.0, 0.96, 0.95))
        for x, sgn in ((-0.155, -1), (0.155, 1)):
            head.blob(0.048, (x, 0.1, -0.01), SKIN, scale=(0.55, 1.1, 0.8), rot=(0, 0, sgn * 0.3))
        for x in (-0.09, 0.09):
            head.blob(0.05, (x, 0.055, 0.1), 0xf09a86, scale=(1, 0.75, 0.6))  # rosy cheeks
    head.blob(0.078, (0, 0.085, 0.168), NOSE, scale=(1.05, 0.95, 1.0), detail=2)  # potato nose
    for x, px in ((-0.06, 0.012), (0.06, -0.008)):
        head.blob(0.04, (x, 0.158, 0.125), EYE_W, kind=MAT_GLOSSY, detail=2)
        head.blob(0.021, (x + px, 0.155, 0.158), EYE_B, kind=MAT_GLOSSY, detail=2)
        head.blob(0.007, (x + px + 0.008, 0.165, 0.176), EYE_W, kind=MAT_EMIT, detail=1)  # glint
    # bushy eyebrows and a curly moustache (yarn-like hair tubes)
    head.sweep([(-0.11, 0.2, 0.115), (-0.065, 0.222, 0.14), (-0.02, 0.212, 0.148)], [0.016, 0.02, 0.013], BEARD,
               kind=MAT_HAIR, seg=10, steps=4)
    head.sweep([(0.02, 0.212, 0.148), (0.065, 0.222, 0.14), (0.11, 0.2, 0.115)], [0.013, 0.02, 0.016], BEARD,
               kind=MAT_HAIR, seg=10, steps=4)
    for sgn in (-1, 1):
        head.sweep([(0.0, 0.045, 0.2), (sgn * 0.05, 0.035, 0.19), (sgn * 0.1, 0.045, 0.16), (sgn * 0.12, 0.075, 0.13)],
                   [0.024, 0.026, 0.018, 0.008], BEARD, kind=MAT_HAIR, seg=10, steps=4)
    # the huge beard flowing over the belly
    with head.fuse(voxel=0.013, smooth=0.6, iterations=5, decimate=0.14):
        head.blob(0.17, (0, -0.05, 0.19), BEARD, scale=(1.08, 1.1, 0.55), kind=MAT_HAIR)
        head.blob(0.125, (0, -0.2, 0.24), BEARD, scale=(0.95, 1.1, 0.5), kind=MAT_HAIR)
        head.sweep([(0, -0.24, 0.26), (0, -0.34, 0.28), (0.02, -0.43, 0.265)], [0.09, 0.05, 0.012], BEARD,
                   kind=MAT_HAIR, seg=12, steps=4)
        for x in (-0.12, 0.12):
            head.blob(0.07, (x, 0.02, 0.12), BEARD, scale=(0.9, 1.2, 0.7), kind=MAT_HAIR)

    # --- floppy knitted hat (tinted per player), three segments that wobble ---
    hat = Node('hat', head, (0, 0.235, -0.015), rot=(-0.14, 0, 0))
    hat.sweep([(0, -0.02, 0), (0, 0.07, 0), (0, 0.3, 0)], [0.19, 0.182, 0.118], HAT, kind=MAT_TINT, seg=22, steps=4,
              start='flat', end='dome', ribs=lambda t: 0.03 if t < 0.28 else 0.0)
    mid = Node('hatMid', hat, (0, 0.29, 0), rot=(-0.7, 0, 0.2))
    mid.sweep([(0, -0.02, 0), (0, 0.1, 0), (0, 0.21, 0)], [0.116, 0.092, 0.066], HAT, kind=MAT_TINT, seg=18, steps=3,
              start='dome', end='dome')
    tip = Node('hatTip', mid, (0, 0.2, 0), rot=(-1.0, 0, 0.35))
    tip.sweep([(0, -0.015, 0), (0, 0.1, 0), (0, 0.2, 0)], [0.066, 0.038, 0.014], HAT, kind=MAT_TINT, seg=14, steps=3,
              start='dome', end='dome')
    tip.blob(0.045, (0, 0.215, 0), 0xfff4d8, kind=MAT_KNIT, detail=2, wonk=0.004)  # pompom

    # --- stretchy arms: tube along +Z from the shoulder, unit length (scaled at runtime) ---
    for side, x in (('L', -0.2), ('R', 0.2)):
        arm = Node('arm' + side, root, (x, 0.56, 0.0), rot=(math.pi / 2 - 0.25, 0, 0), surface=MAT_FABRIC)
        arm.scale = (1, 1, 0.3)
        arm.sweep([(0, 0, 0), (0, 0, 1.0)], [0.047, 0.043], TUNIC, seg=12, steps=1, start='flat', end='flat')
        hand = Node('hand' + side, root, (x * 1.05, 0.28, 0.07), surface=MAT_KNIT)
        s_ = -1 if side == 'L' else 1
        with hand.fuse(voxel=0.009, smooth=0.5, iterations=4, decimate=0.25):
            hand.blob(0.068, (0, 0, 0.01), MITT, scale=(0.85, 0.95, 1.15), kind=MAT_KNIT)
            hand.blob(0.03, (s_ * 0.052, 0.018, 0.03), MITT, scale=(1.0, 1.0, 1.4), kind=MAT_KNIT)
        hand.sweep([(0, 0, -0.08), (0, 0, -0.035)], [0.05, 0.052], 0xc8a070, kind=MAT_KNIT, seg=14, steps=1,
                   start='flat', end='flat', ribs=lambda t: 0.06)  # ribbed cuff

    root.props['height'] = 1.0
    return root


def high_gnome():
    """The High-Gnome: giant, ancient, very judgemental. Stands on the hub island."""
    root = Node('highGnome')
    s = 2.4
    robe = 0x5a2f8a
    root.cylb(0.35 * s, 0.55 * s, (0, 0, 0), robe, seg=10, r2=0.28 * s, wonk=0.01)
    root.sphere(0.3 * s, (0, 0.62 * s, 0), robe, scale=(1, 0.9, 0.95), seg=10, rings=7)
    root.cyl(0.31 * s, 0.05 * s, (0, 0.48 * s, 0), GOLD, seg=10)
    root.sphere(0.17 * s, (0, 0.98 * s, 0.02 * s), SKIN, seg=10, rings=7)
    root.sphere(0.08 * s, (0, 0.96 * s, 0.19 * s), NOSE, seg=8, rings=6)
    for x in (-0.06, 0.06):
        root.sphere(0.03 * s, (x * s, 1.04 * s, 0.155 * s), EYE_W, seg=6, rings=4)
        root.sphere(0.015 * s, (x * s, 1.035 * s, 0.18 * s), EYE_B, seg=6, rings=4)
        root.box((0.08 * s, 0.025 * s, 0.03 * s), (x * s, 1.09 * s, 0.155 * s), BEARD, rot=(0, 0, -0.35 if x < 0 else 0.35))
    # enormous beard reaching the floor
    root.sphere(0.22 * s, (0, 0.62 * s, 0.2 * s), BEARD, scale=(1.1, 1.6, 0.55), ico=2, wonk=0.03)
    root.cone(0.16 * s, 0.5 * s, (0, 0.15 * s, 0.26 * s), BEARD, rot=(math.pi, 0, 0), seg=8, wonk=0.02)
    # tall hat with a crown ring and a star
    root.cyl(0.19 * s, 0.6 * s, (0, 1.38 * s, -0.02 * s), 0x2c2a6a, seg=10, r2=0.05 * s, wonk=0.01)
    root.cyl(0.2 * s, 0.07 * s, (0, 1.13 * s, -0.02 * s), GOLD, seg=10)
    root.sphere(0.07 * s, (0, 1.72 * s, -0.02 * s), 0xffe36a, seg=6, rings=4, kind=MAT_EMIT)
    # staff
    root.cylb(0.03 * s, 1.5 * s, (0.42 * s, 0, 0.1 * s), 0x7a4a2a, seg=6)
    root.sphere(0.09 * s, (0.42 * s, 1.55 * s, 0.1 * s), 0x7fe0ff, seg=8, rings=6, kind=MAT_EMIT)
    root.col_boxb((0.8 * s, 1.8 * s, 0.8 * s))
    return root



PJ = 0x9cbde0  # pyjama light blue
PJ_STRIPE = 0x33568f
OLD_SKIN = 0xe8b49a
HAIR = 0xeeeeea


def old_man():
    """The grumpy old man. ~7 units tall (a giant to gnomes). Origin at the feet, front +Z.

    Joint nodes (animated procedurally):
      hips > spine > neck > head
      spine > upperArmL > foreArmL > handL   (same for R)
      hips > thighL > shinL > footL          (same for R)
    Smooth fused body, pinstriped pyjamas, nightcap, round glasses, fluffy bunny slippers.
    """
    root = Node('oldMan')
    root.props['kind'] = 'oldMan'
    root.detail = 3.0

    def pinstripes(t, a):
        return PJ_STRIPE if (a / (2 * math.pi) * 9) % 1.0 < 0.16 else PJ


    hips = Node('hips', root, (0, 3.25, 0), surface=MAT_FABRIC)
    with hips.fuse(voxel=0.06, smooth=0.6, iterations=5, decimate=0.2):
        hips.blob(0.75, (0, 0.05, 0), PJ, scale=(1.0, 0.55, 0.68))
        for x in (-0.42, 0.42):
            hips.blob(0.42, (x, -0.1, 0), PJ)

    # --- legs ---
    for side, x in (('L', -0.42), ('R', 0.42)):
        thigh = Node('thigh' + side, hips, (x, -0.1, 0.0), surface=MAT_FABRIC)
        thigh.sweep([(0, 0.1, 0), (0, -0.8, 0.02), (0, -1.6, 0)], [0.41, 0.39, 0.36], PJ, seg=18, steps=3,
                    start='dome', end='dome', paint=pinstripes)
        shin = Node('shin' + side, thigh, (0, -1.55, 0.0), surface=MAT_FABRIC)
        shin.sweep([(0, 0.05, 0), (0, -0.7, 0.02), (0, -1.28, 0)], [0.36, 0.33, 0.31], PJ, seg=18, steps=3,
                   start='dome', end='flat', paint=pinstripes)
        shin.sweep([(0, -1.22, 0), (0, -1.3, 0)], [0.33, 0.33], PJ_STRIPE, seg=18, steps=1, start='flat', end='flat')
        shin.sweep([(0, -1.2, 0), (0, -1.5, 0.02)], [0.2, 0.19], OLD_SKIN, kind=MAT_SKIN, seg=12, steps=2,
                   start='flat', end='dome')  # skinny ankle
        foot = Node('foot' + side, shin, (0, -1.45, 0.05), surface=MAT_FUR)
        # fluffy pink bunny slipper
        with foot.fuse(voxel=0.04, smooth=0.6, iterations=5, decimate=0.16):
            foot.blob(0.42, (0, 0.0, 0.25), 0xf4a7c0, scale=(0.95, 0.52, 1.45))
            foot.blob(0.3, (0, 0.12, 0.72), 0xf4a7c0, scale=(1.0, 0.85, 0.9))
        foot.blob(0.1, (0, 0.14, 1.0), 0xff7fa5, detail=2, kind=MAT_FUR)  # nose
        for ex in (-0.14, 0.14):
            foot.blob(0.055, (ex, 0.3, 0.9), 0x1b1b1f, kind=MAT_GLOSSY, detail=2)  # button eyes
            foot.sweep([(ex * 1.2, 0.3, 0.62), (ex * 1.6, 0.75, 0.5), (ex * 2.2, 1.05, 0.25)], [0.1, 0.09, 0.03],
                       0xf4a7c0, seg=10, steps=4, kind=MAT_FUR, squash=lambda t: (1.0, 0.45))  # floppy ears
        foot.box((0.72, 0.08, 1.35), (0, -0.2, 0.28), 0xd98aa5, bevel=0.03, kind=MAT_LEATHER)  # sole

    # --- torso (hunched, pot belly), striped pyjama shirt with buttons ---
    spine = Node('spine', hips, (0, 0.35, 0), rot=(0.22, 0, 0), surface=MAT_FABRIC)
    with spine.fuse(voxel=0.06, smooth=0.6, iterations=5, decimate=0.16):
        spine.blob(0.95, (0, 0.55, 0.12), PJ, scale=(1.0, 0.85, 0.95))
        spine.blob(0.8, (0, 1.3, -0.05), PJ, scale=(1.0, 1.0, 0.9))
        for x in (-0.8, 0.8):
            spine.blob(0.36, (x, 1.72, -0.05), PJ)
    for y, z in ((1.5, 0.72), (1.1, 0.87), (0.7, 0.98)):
        spine.lathe([(0.0, 0.0), (0.075, 0.0), (0.08, 0.02), (0.0, 0.035)], (0, y, z), 0xfff6d8,
                    rot=(R90 - 0.2, 0, 0), seg=10, kind=MAT_GLOSSY)
    # collar: a soft folded band around the neck
    spine.sweep([(0.5 * math.cos(a), 2.02 + 0.06 * math.sin(a), 0.05 + 0.42 * math.sin(a)) for a in
                 [i * math.pi / 8 for i in range(17)]], [0.1] * 17, PJ_STRIPE, seg=10, steps=2, start='flat', end='flat')

    # --- head ---
    neck = Node('neck', spine, (0, 2.05, 0.1), rot=(-0.12, 0, 0), surface=MAT_SKIN)
    neck.sweep([(0, -0.1, 0), (0, 0.5, 0.02)], [0.28, 0.26], OLD_SKIN, seg=14, steps=2, start='flat', end='flat')
    head = Node('head', neck, (0, 0.4, 0.1), surface=MAT_SKIN)
    with head.fuse(voxel=0.035, smooth=0.6, iterations=5, decimate=0.18):
        head.blob(0.62, (0, 0.55, 0), OLD_SKIN, scale=(0.95, 1.05, 1.0))
        head.blob(0.4, (0, 0.14, 0.22), OLD_SKIN, scale=(1.25, 0.8, 1.0))  # jowls / jaw
        for x, sgn in ((-0.6, -1), (0.6, 1)):
            head.blob(0.2, (x, 0.5, -0.05), OLD_SKIN, scale=(0.45, 1.3, 0.9), rot=(0, 0, sgn * 0.2))
    # enormous red nose
    with head.fuse(voxel=0.03, smooth=0.5, iterations=4, decimate=0.2):
        head.blob(0.26, (0, 0.5, 0.66), 0xe08b78, scale=(0.9, 1.1, 1.0))
        head.blob(0.16, (0, 0.33, 0.76), 0xe08b78)
    # white hair tufts sticking out sideways, bushy angry eyebrows
    with head.fuse(voxel=0.035, smooth=0.6, iterations=4, decimate=0.15):
        for x, sgn in ((-0.56, -1), (0.56, 1)):
            head.blob(0.22, (x, 0.78, -0.25), HAIR, scale=(1.3, 0.9, 1.4), rot=(0, 0, sgn * 0.5), kind=MAT_HAIR)
            head.blob(0.15, (x * 1.12, 0.62, -0.1), HAIR, scale=(1.1, 0.9, 1.2), kind=MAT_HAIR)
    for sgn in (-1, 1):
        head.sweep([(sgn * 0.06, 0.88, 0.6), (sgn * 0.24, 0.96, 0.56), (sgn * 0.44, 0.9, 0.44)], [0.07, 0.09, 0.05],
                   HAIR, kind=MAT_HAIR, seg=10, steps=4)
    # thick round glasses; huge eyes behind the lenses
    for x in (-0.24, 0.24):
        head.blob(0.13, (x, 0.72, 0.5), 0xffffff, kind=MAT_GLOSSY, detail=2)
        head.blob(0.065, (x * 0.92, 0.7, 0.62), 0x1b1b1f, kind=MAT_GLOSSY, detail=2)
        head.torus(0.2, 0.035, (x, 0.72, 0.62), 0x2a211b, rot=(math.pi / 2, 0, 0), seg=16, tseg=5)
        head.cyl(0.19, 0.015, (x, 0.72, 0.63), 0xbfe6f5, rot=(math.pi / 2, 0, 0), seg=18, kind=MAT_GLASS)
    head.sweep([(-0.05, 0.74, 0.66), (0, 0.77, 0.7), (0.05, 0.74, 0.66)], [0.025] * 3, 0x2a211b, kind=MAT_METAL,
               seg=6, steps=3)  # bridge
    for sgn in (-1, 1):
        head.sweep([(sgn * 0.43, 0.74, 0.6), (sgn * 0.6, 0.76, 0.3), (sgn * 0.6, 0.72, 0.0)], [0.022] * 3, 0x2a211b,
                   kind=MAT_METAL, seg=6, steps=3)  # temples
    # droopy moustache + grumpy frown
    for sgn in (-1, 1):
        head.sweep([(0, 0.24, 0.8), (sgn * 0.15, 0.2, 0.74), (sgn * 0.3, 0.08, 0.6), (sgn * 0.34, -0.04, 0.5)],
                   [0.09, 0.1, 0.07, 0.03], HAIR, kind=MAT_HAIR, seg=10, steps=4)
    head.sweep([(-0.16, 0.0, 0.58), (0, 0.05, 0.63), (0.16, 0.0, 0.58)], [0.03, 0.035, 0.03], 0x8a3a3a,
               kind=MAT_SKIN, seg=8, steps=4)
    # nightcap (looks suspiciously like a gnome hat...)
    cap = Node('cap', head, (0, 1.0, -0.05), rot=(-0.25, 0, 0.1), surface=MAT_KNIT)
    cap.sweep([(0, -0.12, 0), (0, 0.12, 0)], [0.63, 0.63], 0xf3f0e6, seg=24, steps=2, start='flat', end='flat',
              ribs=lambda t: 0.03)
    cap.sweep([(0, 0.05, 0), (0, 0.55, -0.05), (0, 0.95, -0.25), (0, 1.1, -0.55), (0, 1.0, -0.8)],
              [0.58, 0.48, 0.3, 0.16, 0.06], 0x6f8fc9, seg=20, steps=4, start='flat', end='dome')
    cap.blob(0.17, (0, 0.98, -0.85), 0xf3f0e6, kind=MAT_KNIT, detail=2, wonk=0.02)

    # --- arms ---
    for side, x in (('L', -1.0), ('R', 1.0)):
        up = Node('upperArm' + side, spine, (x * 0.95, 1.75, -0.05), rot=(0, 0, x * 0.12), surface=MAT_FABRIC)
        up.sweep([(0, 0.05, 0), (0, -0.65, 0), (0, -1.35, 0)], [0.34, 0.3, 0.28], PJ, seg=16, steps=3,
                 start='dome', end='dome', paint=pinstripes)
        fore = Node('foreArm' + side, up, (0, -1.3, 0), rot=(-0.35, 0, 0), surface=MAT_FABRIC)
        fore.sweep([(0, 0.05, 0), (0, -0.55, 0), (0, -1.05, 0)], [0.27, 0.25, 0.23], PJ, seg=16, steps=3,
                   start='dome', end='flat', paint=pinstripes)
        fore.sweep([(0, -1.0, 0), (0, -1.08, 0)], [0.25, 0.25], PJ_STRIPE, seg=16, steps=1, start='flat', end='flat')
        fore.sweep([(0, -1.0, 0), (0, -1.3, 0.02)], [0.17, 0.16], OLD_SKIN, kind=MAT_SKIN, seg=12, steps=2,
                   start='flat', end='dome')  # wrist
        hand = Node('hand' + side, fore, (0, -1.25, 0.05), surface=MAT_SKIN)
        with hand.fuse(voxel=0.03, smooth=0.5, iterations=4, decimate=0.25):
            hand.blob(0.2, (0, -0.12, 0.0), OLD_SKIN, scale=(0.95, 1.15, 0.6))
            for i, fx in enumerate((-0.11, -0.037, 0.037, 0.11)):
                ln = 0.34 if i in (1, 2) else 0.29
                hand.sweep([(fx, -0.2, 0.0), (fx * 1.05, -0.2 - ln * 0.6, 0.03), (fx * 1.1, -0.2 - ln, 0.09)],
                           [0.052, 0.046, 0.04], OLD_SKIN, seg=10, steps=3)
            hand.sweep([(x * -0.14, -0.1, 0.04), (x * -0.24, -0.22, 0.1), (x * -0.26, -0.33, 0.13)], [0.06, 0.05, 0.042],
                       OLD_SKIN, seg=10, steps=3)  # thumb
    return root


CAT_ORANGE = 0xe3913e
CAT_STRIPE = 0xb8641f
CAT_WHITE = 0xf6efe2


def cat():
    """Barsik the chubby, grumpy cat. ~1 unit at the shoulder, 1.8 long. Front +Z.

    Nodes: body > head, body > tail1 > tail2 > tail3, legFL/legFR/legBL/legBR (hips/shoulders).
    One smooth furry body with tabby stripes painted across the back."""
    root = Node('cat')
    root.props['kind'] = 'cat'
    root.detail = 1.2

    def tabby(p, n, mat):
        x, y, z = p
        if mat.endswith('%06x' % CAT_WHITE):
            return MAT_FUR, CAT_WHITE
        # stripes run across the back and fade out down the flanks
        if y > -0.05 and (math.sin(z * 11.0 + math.sin(x * 6.0) * 0.8) > 0.55):
            return MAT_FUR, CAT_STRIPE
        return MAT_FUR, CAT_ORANGE

    body = Node('body', root, (0, 0.72, 0), surface=MAT_FUR)
    with body.fuse(voxel=0.028, smooth=0.6, iterations=5, decimate=0.22, paint=tabby):
        body.blob(0.55, (0, 0.05, 0), CAT_ORANGE, scale=(0.95, 0.85, 1.5))
        body.blob(0.42, (0, 0.12, 0.45), CAT_ORANGE, scale=(1.0, 0.95, 1.0))  # chest / shoulders
        body.blob(0.46, (0, 0.02, -0.42), CAT_ORANGE, scale=(1.05, 0.9, 1.0))  # big round rump
        body.blob(0.36, (0, -0.2, 0.2), CAT_WHITE, scale=(0.85, 0.7, 1.4))  # white belly
    head = Node('head', body, (0, 0.35, 0.8), surface=MAT_FUR)

    def face(p, n, mat):
        x, y, z = p
        if mat.endswith('%06x' % CAT_WHITE):
            return MAT_FUR, CAT_WHITE
        if y > 0.2 and abs(x) < 0.22 and z > 0.05 and math.sin(x * 40.0) > 0.3:
            return MAT_FUR, CAT_STRIPE  # forehead "M" stripes
        return MAT_FUR, CAT_ORANGE

    with head.fuse(voxel=0.02, smooth=0.6, iterations=5, decimate=0.22, paint=face):
        head.blob(0.42, (0, 0.05, 0), CAT_ORANGE, scale=(1.18, 0.95, 0.95))
        for x in (-0.3, 0.3):
            head.blob(0.16, (x, -0.1, 0.12), CAT_ORANGE, scale=(1.1, 0.9, 1.0))  # fluffy cheeks
        head.blob(0.2, (0, -0.1, 0.3), CAT_WHITE, scale=(1.35, 0.8, 0.8))  # muzzle
        for x in (-0.24, 0.24):
            head.sweep([(x, 0.3, -0.02), (x * 1.1, 0.5, -0.03), (x * 1.3, 0.62, -0.05)], [0.15, 0.09, 0.015],
                       CAT_ORANGE, seg=10, steps=3, squash=lambda t: (1.0, 0.55))
    for x in (-0.24, 0.24):
        head.sweep([(x * 1.02, 0.34, 0.03), (x * 1.12, 0.5, 0.02), (x * 1.28, 0.58, 0.0)], [0.085, 0.05, 0.01],
                   0xf0a0b0, kind=MAT_SKIN, seg=8, steps=3, squash=lambda t: (1.0, 0.35))  # pink inner ear
        # grumpy half-closed yellow eyes with a heavy lid
        head.blob(0.1, (x * 0.68, 0.12, 0.31), 0xf2d23c, scale=(1.1, 0.75, 0.6), kind=MAT_GLOSSY, detail=2)
        head.blob(0.04, (x * 0.68, 0.11, 0.37), 0x1b1b1f, scale=(0.35, 1.4, 0.5), kind=MAT_GLOSSY, detail=2)
        head.sweep([(x * 0.4, 0.19, 0.39), (x * 0.68, 0.2, 0.4), (x * 0.95, 0.15, 0.34)], [0.035, 0.045, 0.03],
                   CAT_ORANGE, kind=MAT_FUR, seg=8, steps=3)  # the lid (grumpy)
        for k in (-1, 0, 1):
            head.sweep([(x * 0.5, -0.05 + k * 0.04, 0.4), (x * 1.3, -0.02 + k * 0.08, 0.36)], [0.006, 0.003],
                       0xffffff, kind=MAT_GLOSSY, seg=4, steps=1, start='flat', end='flat')  # whiskers
    head.blob(0.058, (0, 0.0, 0.46), 0xf0788c, scale=(1.2, 0.8, 0.8), kind=MAT_SKIN, detail=2)  # nose
    head.sweep([(-0.08, -0.13, 0.42), (0, -0.1, 0.45), (0.08, -0.13, 0.42)], [0.012] * 3, 0x5a3a2a,
               kind=MAT_SKIN, seg=6, steps=3)  # mouth

    # tail: three curling segments, the tip striped
    stripes = lambda t, a: CAT_STRIPE if (t * 3.2) % 1.0 < 0.35 else CAT_ORANGE
    tail = Node('tail1', body, (0, 0.15, -0.8), rot=(-0.9, 0, 0), surface=MAT_FUR)
    tail.sweep([(0, -0.05, 0), (0, 0.25, 0), (0, 0.52, 0)], [0.13, 0.12, 0.11], CAT_ORANGE, seg=12, steps=3,
               start='dome', end='dome', paint=stripes)
    t2 = Node('tail2', tail, (0, 0.5, 0), rot=(0.5, 0, 0), surface=MAT_FUR)
    t2.sweep([(0, -0.02, 0), (0, 0.22, 0), (0, 0.47, 0)], [0.11, 0.105, 0.1], CAT_ORANGE, seg=12, steps=3,
             start='dome', end='dome', paint=stripes)
    t3 = Node('tail3', t2, (0, 0.45, 0), rot=(0.5, 0, 0), surface=MAT_FUR)
    t3.sweep([(0, -0.02, 0), (0, 0.2, 0), (0, 0.38, 0)], [0.1, 0.085, 0.04], CAT_ORANGE, seg=12, steps=3,
             start='dome', end='dome', paint=lambda t, a: CAT_STRIPE if t > 0.7 or (t * 3) % 1.0 < 0.3 else CAT_ORANGE)

    # legs: chunky furry columns with white paws
    for name, x, z in (('legFL', -0.28, 0.5), ('legFR', 0.28, 0.5), ('legBL', -0.3, -0.5), ('legBR', 0.3, -0.5)):
        leg = Node(name, root, (x, 0.55, z), surface=MAT_FUR)
        with leg.fuse(voxel=0.02, smooth=0.5, iterations=4, decimate=0.25):
            leg.sweep([(0, 0.1, 0), (0, -0.2, 0), (0, -0.42, 0.02)], [0.16, 0.13, 0.12], CAT_ORANGE, seg=12, steps=3)
            leg.blob(0.15, (0, -0.48, 0.06), CAT_WHITE, scale=(1.0, 0.6, 1.25))
        for tx in (-0.06, 0.0, 0.06):
            leg.blob(0.03, (tx, -0.5, 0.2), 0xf0a0b0, kind=MAT_SKIN, detail=1, scale=(1, 0.6, 1))  # toe beans peek
    return root


def roomba():
    """RoboVac 3000: a vacuum robot with ONE angry red eye. Radius 0.7, front +Z."""
    root = Node('roomba')
    root.props['kind'] = 'roomba'
    root.cylb(0.7, 0.26, (0, 0.04, 0), 0x2b2d33, seg=16)
    root.cylb(0.62, 0.06, (0, 0.3, 0), 0x3e424b, seg=16)
    root.cylb(0.72, 0.12, (0, 0.08, 0), 0x9aa0aa, seg=16, r2=0.72)  # bumper ring
    root.cylb(0.25, 0.04, (0, 0.34, 0), 0xc8ccd4, seg=12)  # button
    root.cylb(0.08, 0.03, (0, 0.375, 0), 0x46ff7a, seg=8, kind=MAT_EMIT)  # status led
    # angry eye + eyebrow sticker
    root.box((0.26, 0.1, 0.05), (0, 0.22, 0.69), 0x111111)
    root.sphere(0.08, (0, 0.22, 0.7), 0xff2a2a, scale=(1.4, 0.8, 0.6), seg=8, rings=5, kind=MAT_EMIT)
    root.box((0.3, 0.05, 0.03), (0.0, 0.32, 0.66), 0x111111, rot=(0, 0, 0.2))
    brush = Node('brush', root, (0.45, 0.05, 0.45))
    for i in range(3):
        brush.box((0.35, 0.02, 0.04), (0.17, 0, 0), 0xe6d8a0, rot=(0, i * 2.094, 0))
    brush.cylb(0.06, 0.05, (0, -0.02, 0), 0x333333, seg=6)
    for x in (-0.55, 0.55):
        root.cyl(0.1, 0.08, (x, 0.08, 0), 0x111111, rot=(0, 0, math.pi / 2), seg=8)
    return root


MODELS = {
    'gnome': gnome,
    'oldMan': old_man,
    'cat': cat,
}
