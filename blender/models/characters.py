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


MODELS = {
    'gnome': gnome,
    'highGnome': high_gnome,
}
