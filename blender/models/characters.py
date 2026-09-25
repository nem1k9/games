"""Characters: the gnome (player), the grumpy old man, the cat, the robot vacuum, the High-Gnome."""
import math

from gnomelib.core import MAT_EMIT, MAT_GLASS, MAT_TINT, Node

SKIN = 0xf2b393
NOSE = 0xe86f63
BEARD = 0xf4f0e6
TUNIC = 0x5b8c3c
PANTS = 0x7a5230
BOOT = 0x4a2f1c
BELT = 0x3a2412
GOLD = 0xe8b83a
HAT = 0xd8342c
MITT = 0x8f5b36
EYE_W = 0xffffff
EYE_B = 0x1b1b1f


def gnome():
    """Player gnome. ~1.0 unit tall, origin at the feet, front = +Z.

    Nodes used by the game at runtime:
      body (hips pivot) > head > hat > hatMid > hatTip   (floppy hat wobble)
      armL / armR   (shoulder pivots; mesh is a unit-length tube along +Z, scaled at runtime)
      handL / handR (placed at runtime)
      legL / legR   (hip joints, swing when walking)
    """
    root = Node('gnome')
    root.props['kind'] = 'gnome'

    # --- legs ---
    for side, x in (('L', -0.1), ('R', 0.1)):
        leg = Node('leg' + side, root, (x, 0.27, 0.0))
        leg.cyl(0.062, 0.16, (0, -0.09, 0), PANTS, seg=7)
        # curly-toed boot
        leg.box((0.13, 0.085, 0.19), (0, -0.225, 0.03), BOOT, bevel=0.025)
        leg.cone(0.045, 0.1, (0, -0.2, 0.15), BOOT, rot=(math.radians(-60), 0, 0), seg=6)
        leg.sphere(0.02, (0, -0.155, 0.19), GOLD, seg=6, rings=4)

    # --- body (pivot at the hips) ---
    body = Node('body', root, (0, 0.27, 0))
    body.sphere(0.265, (0, 0.2, 0), TUNIC, scale=(1.0, 1.02, 0.93), seg=10, rings=7, wonk=0.006)
    body.cyl(0.25, 0.1, (0, 0.03, 0), PANTS, seg=10, r2=0.262)  # trousers bottom
    body.cyl(0.272, 0.055, (0, 0.105, 0), BELT, seg=10)  # belt
    body.box((0.11, 0.08, 0.03), (0, 0.105, 0.258), GOLD, bevel=0.01)
    body.box((0.05, 0.035, 0.035), (0, 0.105, 0.27), BELT)
    # loot sack on the back
    body.sphere(0.13, (0.02, 0.22, -0.25), 0xb58a54, scale=(1.0, 1.15, 0.85), seg=8, rings=6, wonk=0.01)
    body.cyl(0.035, 0.06, (0.02, 0.37, -0.26), 0xb58a54, seg=6)
    body.torus(0.04, 0.012, (0.02, 0.35, -0.26), 0x6b4a2a, seg=8, tseg=4)
    body.box((0.04, 0.3, 0.02), (0.12, 0.3, -0.13), 0x6b4a2a, rot=(0.3, 0, -0.5))  # strap
    # shoulders / sleeve stubs
    for x in (-0.19, 0.19):
        body.sphere(0.075, (x, 0.29, 0.0), TUNIC, seg=7, rings=5)

    # --- head ---
    head = Node('head', body, (0, 0.34, 0.02))
    head.sphere(0.16, (0, 0.1, 0.0), SKIN, seg=10, rings=7, scale=(1.0, 0.95, 0.95))
    # ears
    for x, s in ((-0.16, -1), (0.16, 1)):
        head.sphere(0.045, (x, 0.1, -0.01), SKIN, scale=(0.6, 1.1, 0.8), seg=6, rings=4, rot=(0, 0, s * 0.3))
    # giant potato nose
    head.sphere(0.072, (0, 0.085, 0.165), NOSE, scale=(1.05, 0.95, 1.0), seg=8, rings=6, wonk=0.004)
    # rosy cheeks
    for x in (-0.095, 0.095):
        head.sphere(0.04, (x, 0.06, 0.12), 0xf08a7a, scale=(1, 0.7, 0.5), seg=6, rings=4)
    # googly eyes (slightly cross-eyed for comedy)
    for x, px in ((-0.058, 0.012), (0.058, -0.006)):
        head.sphere(0.036, (x, 0.155, 0.13), EYE_W, seg=8, rings=6)
        head.sphere(0.019, (x + px, 0.15, 0.162), EYE_B, seg=6, rings=4)
    # bushy eyebrows
    head.box((0.07, 0.022, 0.03), (-0.06, 0.205, 0.135), BEARD, rot=(0, 0, 0.25), bevel=0.008)
    head.box((0.07, 0.022, 0.03), (0.06, 0.205, 0.135), BEARD, rot=(0, 0, -0.25), bevel=0.008)
    # moustache
    head.sphere(0.05, (-0.05, 0.03, 0.17), BEARD, scale=(1.3, 0.55, 0.7), rot=(0, 0, 0.35), seg=7, rings=5)
    head.sphere(0.05, (0.05, 0.03, 0.17), BEARD, scale=(1.3, 0.55, 0.7), rot=(0, 0, -0.35), seg=7, rings=5)
    # huge beard flowing over the belly
    head.sphere(0.17, (0, -0.07, 0.11), BEARD, scale=(1.05, 1.15, 0.6), ico=2, wonk=0.018)
    head.cone(0.1, 0.18, (0, -0.25, 0.14), BEARD, rot=(math.radians(180) + 0.25, 0, 0), seg=7, wonk=0.01)

    # --- floppy hat (tinted per player) ---
    hat = Node('hat', head, (0, 0.2, -0.01), rot=(-0.12, 0, 0))
    hat.cyl(0.185, 0.07, (0, 0.0, 0), HAT, seg=10, r2=0.178, kind=MAT_TINT)
    hat.cyl(0.178, 0.26, (0, 0.165, 0), HAT, seg=10, r2=0.115, kind=MAT_TINT, wonk=0.004)
    mid = Node('hatMid', hat, (0, 0.29, 0), rot=(-0.55, 0, 0.18))
    mid.sphere(0.113, (0, 0, 0), HAT, seg=9, rings=6, kind=MAT_TINT)
    mid.cyl(0.115, 0.2, (0, 0.1, 0), HAT, seg=9, r2=0.065, kind=MAT_TINT)
    tip = Node('hatTip', mid, (0, 0.2, 0), rot=(-0.95, 0, 0.35))
    tip.sphere(0.063, (0, 0, 0), HAT, seg=8, rings=5, kind=MAT_TINT)
    tip.cone(0.065, 0.21, (0, 0.105, 0), HAT, seg=8, kind=MAT_TINT)
    tip.sphere(0.04, (0, 0.21, 0), 0xfff4d8, seg=7, rings=5, wonk=0.004)

    # --- stretchy arms: tube along +Z from the shoulder, unit length (scaled at runtime) ---
    for side, x in (('L', -0.2), ('R', 0.2)):
        arm = Node('arm' + side, root, (x, 0.56, 0.0), rot=(math.pi / 2 - 0.25, 0, 0))
        arm.scale = (1, 1, 0.3)
        arm.cyl(0.045, 1.0, (0, 0, 0.5), TUNIC, rot=(math.pi / 2, 0, 0), seg=7)
        hand = Node('hand' + side, root, (x * 1.05, 0.28, 0.07))
        hand.sphere(0.07, (0, 0, 0), MITT, scale=(0.85, 1.0, 1.1), seg=8, rings=5)
        hand.sphere(0.03, (-0.05 if side == 'L' else 0.05, 0.02, 0.03), MITT, seg=6, rings=4)

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

    Joint nodes (animated procedurally in Unity):
      hips > spine > neck > head
      spine > upperArmL > foreArmL > handL   (same for R)
      hips > thighL > shinL > footL          (same for R)
    """
    root = Node('oldMan')
    root.props['kind'] = 'oldMan'
    hips = Node('hips', root, (0, 3.25, 0))
    hips.box((1.45, 0.7, 0.95), (0, 0.05, 0), PJ, bevel=0.12)

    # --- legs ---
    for side, x in (('L', -0.42), ('R', 0.42)):
        thigh = Node('thigh' + side, hips, (x, -0.1, 0.0))
        thigh.cyl(0.36, 1.55, (0, -0.78, 0), PJ, seg=8, r2=0.4)
        thigh.cyl(0.405, 0.12, (0, -0.35, 0), PJ_STRIPE, seg=8)
        thigh.cyl(0.39, 0.12, (0, -1.05, 0), PJ_STRIPE, seg=8)
        shin = Node('shin' + side, thigh, (0, -1.55, 0.0))
        shin.sphere(0.35, (0, 0, 0), PJ, seg=8, rings=5)
        shin.cyl(0.3, 1.35, (0, -0.68, 0), PJ, seg=8, r2=0.34)
        shin.cyl(0.33, 0.12, (0, -0.5, 0), PJ_STRIPE, seg=8)
        shin.cyl(0.24, 0.25, (0, -1.35, 0), OLD_SKIN, seg=8)  # skinny ankle
        foot = Node('foot' + side, shin, (0, -1.45, 0.05))
        # fluffy pink bunny slipper
        foot.sphere(0.42, (0, 0.0, 0.25), 0xf4a7c0, scale=(0.95, 0.55, 1.45), seg=9, rings=6, wonk=0.02)
        foot.sphere(0.12, (0, 0.12, 0.8), 0xff7fa5, seg=6, rings=4)  # nose
        for ex in (-0.15, 0.15):
            foot.sphere(0.06, (ex, 0.24, 0.62), 0x1b1b1f, seg=6, rings=4)  # button eyes
            foot.sphere(0.1, (ex * 1.2, 0.55, 0.35), 0xf4a7c0, scale=(0.9, 2.6, 0.5), rot=(-0.4, 0, ex * 1.5), seg=6, rings=5)  # ears
        foot.box((0.7, 0.08, 1.2), (0, -0.2, 0.25), 0xd98aa5)  # sole

    # --- torso (hunched) ---
    spine = Node('spine', hips, (0, 0.35, 0), rot=(0.22, 0, 0))
    spine.sphere(0.95, (0, 0.55, 0.12), PJ, scale=(1.0, 0.85, 0.95), seg=10, rings=7, wonk=0.02)  # pot belly
    spine.cyl(0.8, 1.5, (0, 1.2, -0.05), PJ, seg=9, r2=0.72)  # chest
    for y in (0.25, 0.75, 1.25, 1.75):
        spine.cyl(0.84 - (y * 0.05), 0.12, (0, y + 0.2, -0.02), PJ_STRIPE, seg=9)
    # pyjama buttons
    for y in (1.2, 1.6, 0.8):
        spine.sphere(0.07, (0, y, 0.8 - (y - 0.8) * 0.12), 0xfff6d8, seg=6, rings=4)
    spine.box((0.9, 0.35, 0.55), (0, 1.95, 0.25), PJ, rot=(0.3, 0, 0), bevel=0.08)  # collar

    # --- head ---
    neck = Node('neck', spine, (0, 2.05, 0.1), rot=(-0.12, 0, 0))
    neck.cyl(0.26, 0.45, (0, 0.2, 0), OLD_SKIN, seg=8)
    head = Node('head', neck, (0, 0.4, 0.1))
    head.sphere(0.62, (0, 0.55, 0), OLD_SKIN, scale=(0.95, 1.05, 1.0), seg=10, rings=8, wonk=0.012)
    head.sphere(0.38, (0, 0.1, 0.25), OLD_SKIN, scale=(1.3, 0.8, 1.0), seg=9, rings=6)  # jowls / jaw
    # enormous nose
    head.sphere(0.25, (0, 0.5, 0.66), 0xe08b78, scale=(0.9, 1.1, 1.0), seg=8, rings=6, wonk=0.01)
    head.sphere(0.15, (0, 0.35, 0.75), 0xe08b78, seg=8, rings=5)
    # ears
    for x in (-0.62, 0.62):
        head.sphere(0.2, (x, 0.5, -0.05), OLD_SKIN, scale=(0.45, 1.3, 0.9), seg=7, rings=5)
    # white hair tufts sticking out sideways
    for x, sgn in ((-0.55, -1), (0.55, 1)):
        head.sphere(0.2, (x, 0.75, -0.25), HAIR, scale=(1.3, 0.9, 1.4), rot=(0, 0, sgn * 0.5), ico=1, wonk=0.05)
    # bushy angry eyebrows
    head.box((0.36, 0.1, 0.12), (-0.24, 0.9, 0.52), HAIR, rot=(0, 0.2, 0.35), bevel=0.03, wonk=0.015)
    head.box((0.36, 0.1, 0.12), (0.24, 0.9, 0.52), HAIR, rot=(0, -0.2, -0.35), bevel=0.03, wonk=0.015)
    # thick round glasses (the lenses are real glass, the eyes behind are huge)
    for x in (-0.24, 0.24):
        head.sphere(0.13, (x, 0.72, 0.5), 0xffffff, seg=8, rings=6)
        head.sphere(0.06, (x * 0.95, 0.7, 0.62), 0x1b1b1f, seg=6, rings=4)
        head.torus(0.19, 0.035, (x, 0.72, 0.6), 0x2a211b, rot=(math.pi / 2, 0, 0), seg=10, tseg=4)
        head.cyl(0.18, 0.02, (x, 0.72, 0.62), 0xbfe6f5, rot=(math.pi / 2, 0, 0), seg=10, kind=MAT_GLASS)
    head.box((0.18, 0.04, 0.04), (0, 0.74, 0.64), 0x2a211b)
    # droopy moustache + frown
    head.sphere(0.2, (-0.16, 0.2, 0.62), HAIR, scale=(1.2, 0.45, 0.6), rot=(0, 0, -0.45), seg=7, rings=5, wonk=0.01)
    head.sphere(0.2, (0.16, 0.2, 0.62), HAIR, scale=(1.2, 0.45, 0.6), rot=(0, 0, 0.45), seg=7, rings=5, wonk=0.01)
    head.box((0.3, 0.05, 0.05), (0, 0.02, 0.6), 0x8a3a3a, rot=(0, 0, 0))
    # nightcap (looks suspiciously like a gnome hat...)
    cap = Node('cap', head, (0, 1.0, -0.05), rot=(-0.25, 0, 0.1))
    cap.cyl(0.6, 0.22, (0, 0, 0), 0xf3f0e6, seg=10)
    cap.cyl(0.55, 0.7, (0, 0.45, 0), 0x6f8fc9, seg=10, r2=0.3)
    cap.cyl(0.3, 0.6, (0, 0.95, -0.12), 0x6f8fc9, rot=(-0.6, 0, 0), seg=9, r2=0.1)
    cap.sphere(0.16, (0, 1.2, -0.45), 0xf3f0e6, ico=1, wonk=0.02)

    # --- arms ---
    for side, x in (('L', -1.0), ('R', 1.0)):
        up = Node('upperArm' + side, spine, (x * 0.95, 1.75, -0.05), rot=(0, 0, x * 0.12))
        up.sphere(0.34, (0, 0, 0), PJ, seg=8, rings=5)
        up.cyl(0.26, 1.3, (0, -0.65, 0), PJ, seg=8, r2=0.3)
        up.cyl(0.29, 0.12, (0, -0.55, 0), PJ_STRIPE, seg=8)
        fore = Node('foreArm' + side, up, (0, -1.3, 0), rot=(-0.35, 0, 0))
        fore.sphere(0.24, (0, 0, 0), PJ, seg=8, rings=5)
        fore.cyl(0.21, 1.1, (0, -0.55, 0), PJ, seg=8, r2=0.24)
        fore.cyl(0.2, 0.18, (0, -1.08, 0), OLD_SKIN, seg=8)
        hand = Node('hand' + side, fore, (0, -1.25, 0.05))
        hand.box((0.34, 0.45, 0.2), (0, -0.12, 0.0), OLD_SKIN, bevel=0.06)
        for i, fx in enumerate((-0.11, -0.035, 0.04, 0.115)):
            hand.box((0.07, 0.3, 0.08), (fx, -0.45 - (0.03 if i in (1, 2) else 0), 0.02), OLD_SKIN, bevel=0.02)
        hand.box((0.08, 0.22, 0.09), (x * -0.2, -0.2, 0.1), OLD_SKIN, rot=(0, 0, x * 0.6), bevel=0.02)  # thumb
    return root


CAT_ORANGE = 0xe3913e
CAT_STRIPE = 0xb8641f
CAT_WHITE = 0xf6efe2


def cat():
    """Barsik the chubby, grumpy cat. ~1 unit at the shoulder, 1.8 long. Front +Z."""
    root = Node('cat')
    root.props['kind'] = 'cat'
    body = Node('body', root, (0, 0.72, 0))
    body.sphere(0.55, (0, 0.05, 0), CAT_ORANGE, scale=(0.95, 0.85, 1.55), seg=10, rings=7, wonk=0.012)
    body.sphere(0.38, (0, -0.15, 0.25), CAT_WHITE, scale=(0.9, 0.8, 1.4), seg=8, rings=6)  # white belly
    for z, k in ((-0.5, 0.86), (-0.15, 0.97), (0.2, 0.95)):
        body.sphere(0.56, (0, 0.07, z), CAT_STRIPE, scale=(0.97 * k, 0.87 * k, 0.12), seg=10, rings=7)
    head = Node('head', body, (0, 0.35, 0.8))
    head.sphere(0.42, (0, 0.05, 0), CAT_ORANGE, scale=(1.15, 0.95, 0.95), seg=10, rings=7, wonk=0.01)
    head.sphere(0.2, (0, -0.08, 0.3), CAT_WHITE, scale=(1.3, 0.8, 0.8), seg=8, rings=5)  # muzzle
    head.sphere(0.06, (0, 0.0, 0.46), 0xf0788c, seg=6, rings=4)  # nose
    for x in (-0.24, 0.24):
        head.cone(0.17, 0.3, (x, 0.42, -0.02), CAT_ORANGE, rot=(0, 0, -x * 0.9), seg=4)
        head.cone(0.09, 0.18, (x * 0.98, 0.4, 0.04), 0xf0a0b0, rot=(0, 0, -x * 0.9), seg=4)
        # grumpy half-closed yellow eyes
        head.sphere(0.1, (x * 0.7, 0.12, 0.33), 0xf2d23c, scale=(1.1, 0.7, 0.6), seg=8, rings=5)
        head.box((0.03, 0.12, 0.03), (x * 0.7, 0.12, 0.39), 0x1b1b1f)
        head.box((0.24, 0.07, 0.08), (x * 0.7, 0.2, 0.37), CAT_ORANGE, rot=(0, 0, x * 0.8))  # eyelid (grumpy)
        for k in (-1, 0, 1):
            head.box((0.36, 0.012, 0.012), (x * 1.3, -0.04 + k * 0.05, 0.34), 0xffffff, rot=(0, 0, x * k * 0.25))
    tail = Node('tail1', body, (0, 0.15, -0.8), rot=(-0.9, 0, 0))
    tail.cyl(0.11, 0.5, (0, 0.25, 0), CAT_ORANGE, seg=6)
    t2 = Node('tail2', tail, (0, 0.5, 0), rot=(0.5, 0, 0))
    t2.sphere(0.11, (0, 0, 0), CAT_ORANGE, seg=6, rings=4)
    t2.cyl(0.1, 0.45, (0, 0.22, 0), CAT_STRIPE, seg=6)
    t3 = Node('tail3', t2, (0, 0.45, 0), rot=(0.5, 0, 0))
    t3.sphere(0.1, (0, 0, 0), CAT_ORANGE, seg=6, rings=4)
    t3.cone(0.1, 0.35, (0, 0.17, 0), CAT_ORANGE, seg=6)
    for name, x, z in (('legFL', -0.28, 0.5), ('legFR', 0.28, 0.5), ('legBL', -0.3, -0.5), ('legBR', 0.3, -0.5)):
        leg = Node(name, root, (x, 0.55, z))
        leg.cyl(0.13, 0.5, (0, -0.25, 0), CAT_ORANGE, seg=7)
        leg.sphere(0.15, (0, -0.5, 0.05), CAT_WHITE, scale=(1, 0.6, 1.2), seg=7, rings=4)
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
    'highGnome': high_gnome,
    'oldMan': old_man,
    'cat': cat,
    'roomba': roomba,
}
