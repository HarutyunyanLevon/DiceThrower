"""Builds Assets/DiceModels/Data/crystal.json from glow.json: the d4, d6, d8 and d10 are stretched along their main axis into
crystal points (the d6 along a body diagonal, so it becomes a long rhombohedron); d12 and d20 stay as they are. UVs, face order and numbering are untouched, so the numeral atlas still fits.
Usage: python make_crystal_geometry.py <repo root>
"""
import json, math, sys, os

STRETCH = {"D4": 2.1, "D6": 1.9, "D8": 1.6, "D10": 1.7}   # tune here


def sub(a, b): return [a[i] - b[i] for i in range(3)]
def norm(a):
    l = math.sqrt(sum(x * x for x in a)); return [x / l for x in a]
def dot(a, b): return sum(a[i] * b[i] for i in range(3))


def corners_of(kind):
    pts = {}
    p = kind["pos"]
    for i in range(len(p) // 3):
        pts[kind["corner"][i]] = [p[i * 3], p[i * 3 + 1], p[i * 3 + 2]]
    return pts


def axis_of(kind):
    pts = corners_of(kind)
    name = kind["kind"]
    if name == "D4":      # apex = the corner furthest from the centroid of the other three
        best = None
        for c, v in pts.items():
            others = [pts[o] for o in pts if o != c]
            cen = [sum(o[i] for o in others) / 3 for i in range(3)]
            d = math.dist(v, cen)
            if best is None or d > best[0]: best = (d, norm(sub(v, cen)))
        return best[1]
    if name in ("D6", "D8"):   # through two opposite corners (the d6: a body diagonal of the cube)
        c0 = pts[min(pts)]
        return norm(c0)
    # D10: the two polar corners are the furthest from the centre
    far = sorted(pts.values(), key=lambda v: -math.sqrt(dot(v, v)))
    return norm(sub(far[0], far[1]))


def main(root):
    src = os.path.join(root, "Assets/DiceModels/Data/glow.json")
    dst = os.path.join(root, "Assets/DiceModels/Data/crystal.json")
    data = json.load(open(src))
    data["family"] = "crystal"
    for kind in data["kinds"]:
        s = STRETCH.get(kind["kind"])
        if not s: continue
        a = axis_of(kind)
        p = kind["pos"]
        for i in range(len(p) // 3):
            v = [p[i * 3], p[i * 3 + 1], p[i * 3 + 2]]
            t = dot(v, a) * (s - 1.0)
            for j in range(3): p[i * 3 + j] = round(v[j] + a[j] * t, 6)
    json.dump(data, open(dst, "w"), separators=(",", ":"))
    print("wrote", dst)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else ".")
