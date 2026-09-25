#!/usr/bin/env python3
"""Find z-fighting in GMDL models: pairs of visible triangles that lie in the same plane, face the same way
and overlap. Such pairs flicker in game. Usage: python3 tools/zfight.py [model ...]"""
import math
import os
import struct
import sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
MODELS = os.path.join(HERE, '..', 'Assets', 'Resources', 'Models')


def read(path):
    d = open(path, 'rb').read()
    o = 4
    ver, cnt = struct.unpack_from('<ii', d, o)
    o += 8
    nodes = []

    def s():
        nonlocal o
        n = struct.unpack_from('<i', d, o)[0]
        o += 4
        v = d[o:o + n].decode()
        o += n
        return v

    for _ in range(cnt):
        name = s()
        parent, = struct.unpack_from('<i', d, o)
        o += 4
        pos = struct.unpack_from('<3f', d, o); o += 12
        rot = struct.unpack_from('<4f', d, o); o += 16
        sca = struct.unpack_from('<3f', d, o); o += 12
        nsub, = struct.unpack_from('<i', d, o); o += 4
        subs = []
        for _ in range(nsub):
            kind = chr(d[o]); o += 1
            rgb, vc = struct.unpack_from('<ii', d, o); o += 8
            P = struct.unpack_from('<%df' % (vc * 3), d, o); o += vc * 12
            o += vc * 12 + vc * 8  # normals, uvs
            if ver >= 2:
                o += vc * 4
            ic, = struct.unpack_from('<i', d, o); o += 4
            I = struct.unpack_from('<%di' % ic, d, o); o += ic * 4
            subs.append((kind, P, I))
        npr, = struct.unpack_from('<i', d, o); o += 4
        for _ in range(npr):
            s(); s()
        nodes.append(dict(name=name, parent=parent, pos=pos, rot=rot, sca=sca, subs=subs))
    return nodes


def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz)


def qrot(q, v):
    x, y, z, w = q
    vx, vy, vz = v
    tx, ty, tz = 2 * (y * vz - z * vy), 2 * (z * vx - x * vz), 2 * (x * vy - y * vx)
    return (vx + w * tx + y * tz - z * ty, vy + w * ty + z * tx - x * tz, vz + w * tz + x * ty - y * tx)


def to_model(nodes, i, memo):
    if i in memo:
        return memo[i]
    n = nodes[i]
    if n['parent'] < 0:
        r = ((0, 0, 0), (0, 0, 0, 1), (1, 1, 1))
    else:
        pp, pr, ps = to_model(nodes, n['parent'], memo)
        lp = tuple(n['pos'][k] * ps[k] for k in range(3))
        rp = qrot(pr, lp)
        r = (tuple(pp[k] + rp[k] for k in range(3)), qmul(pr, n['rot']), tuple(ps[k] * n['sca'][k] for k in range(3)))
    memo[i] = r
    return r


def tri_area2(a, b, c):
    u = [b[k] - a[k] for k in range(3)]
    v = [c[k] - a[k] for k in range(3)]
    cr = (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])
    return cr


def clip(poly, a, b):
    """Sutherland-Hodgman: keep the part of poly left of edge a->b (2D)."""
    out = []
    def side(p):
        return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])
    for i in range(len(poly)):
        p, q = poly[i], poly[(i + 1) % len(poly)]
        sp, sq = side(p), side(q)
        if sp >= 0:
            out.append(p)
        if (sp >= 0) != (sq >= 0):
            t = sp / (sp - sq)
            out.append((p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t))
    return out


def area(poly):
    return abs(sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1]
                   for i in range(len(poly)))) / 2


def overlap(t1, t2, n):
    # project to the plane's dominant axes
    ax = max(range(3), key=lambda k: abs(n[k]))
    i, j = [k for k in range(3) if k != ax]
    A = [(p[i], p[j]) for p in t1]
    B = [(p[i], p[j]) for p in t2]
    def ccw(P):
        return P if (P[1][0] - P[0][0]) * (P[2][1] - P[0][1]) - (P[1][1] - P[0][1]) * (P[2][0] - P[0][0]) > 0 else P[::-1]
    A, B = ccw(A), ccw(B)
    poly = A
    for k in range(3):
        poly = clip(poly, B[k], B[(k + 1) % 3])
        if len(poly) < 3:
            return 0.0
    return area(poly)


def check(name, min_area=1e-4, eps=None):
    nodes = read(os.path.join(MODELS, name + '.bytes'))
    memo = {}
    tris = []
    for i, n in enumerate(nodes):
        if not n['subs']:
            continue
        p0, r0, s0 = to_model(nodes, i, memo)
        for kind, P, I in n['subs']:
            if kind == 'G':
                continue
            V = []
            for v in range(len(P) // 3):
                lp = (P[v * 3] * s0[0], P[v * 3 + 1] * s0[1], P[v * 3 + 2] * s0[2])
                rp = qrot(r0, lp)
                V.append((p0[0] + rp[0], p0[1] + rp[1], p0[2] + rp[2]))
            for t in range(0, len(I), 3):
                a, b, c = V[I[t]], V[I[t + 1]], V[I[t + 2]]
                cr = tri_area2(a, b, c)
                L = math.sqrt(sum(x * x for x in cr))
                if L < 1e-9:
                    continue
                nn = tuple(x / L for x in cr)
                d = sum(nn[k] * a[k] for k in range(3))
                tris.append((n['name'], (a, b, c), nn, d, L / 2))
    if not tris:
        return []
    size = max(max(abs(c) for t in tris for p in t[1] for c in p), 1.0)
    eps = eps or size * 2e-4
    buckets = defaultdict(list)
    for idx, t in enumerate(tris):
        nn, d = t[2], t[3]
        key = (round(nn[0] * 50), round(nn[1] * 50), round(nn[2] * 50), round(d / (eps * 4)))
        buckets[key].append(idx)
    found = []
    seen = set()
    for key, lst in buckets.items():
        # neighbours in d as well
        cand = list(lst)
        for dd in (-1, 1):
            cand += buckets.get((key[0], key[1], key[2], key[3] + dd), [])
        for x in lst:
            for y in cand:
                if y <= x or (x, y) in seen:
                    continue
                seen.add((x, y))
                A, B = tris[x], tris[y]
                if sum(A[2][k] * B[2][k] for k in range(3)) < 0.999 or abs(A[3] - B[3]) > eps:
                    continue
                ov = overlap(A[1], B[1], A[2])
                if ov > min_area * size * size:
                    found.append((ov, A[0], B[0], tuple(round(c, 3) for c in A[1][0])))
    return found


def main():
    names = sys.argv[1:] or sorted(f[:-6] for f in os.listdir(MODELS) if f.endswith('.bytes') and f != 'palette.bytes')
    total = 0
    for nm in names:
        f = check(nm)
        if f:
            total += len(f)
            area_sum = sum(x[0] for x in f)
            where = sorted(set((a, b) for _, a, b, _ in f))[:4]
            print(f'{nm:18s} {len(f):4d} overlapping coplanar pairs (area {area_sum:.3f}) e.g. {where} at {f[0][3]}')
    print('total', total)
    return 1 if total else 0


if __name__ == '__main__':
    sys.exit(main())
