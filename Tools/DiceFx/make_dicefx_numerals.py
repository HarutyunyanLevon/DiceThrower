"""Part of make_dicefx_textures.py. Re-renders the numerals of the glow atlas from a vector font: finds every glyph blob in the 512px emission map, works out which
number it is and how it is posed (rotation, mirroring, scale, position), and redraws it crisp at SDF_SIZE."""
import glob, os, sys, math, json
import numpy as np, cv2
from skimage.morphology import skeletonize
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

FONT = os.environ.get("DICEFX_FONT", "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf")  # any bold serif .ttf with digits
NOMINAL = 80.0
CANDS = [str(i) for i in range(0, 21)] + ["6.", "9."]

def render(text, size):
    f = ImageFont.truetype(FONT, size)
    l, t, r, b = f.getbbox(text)
    pad = int(size * 0.2) + 2
    w, h = int(r - l) + 2 * pad, int(b - t) + 2 * pad
    im = Image.new("L", (w, h), 0)
    ImageDraw.Draw(im).text((pad - l, pad - t), text, font=f, fill=255)
    a = np.array(im).astype(np.float32) / 255.0
    return a

def centroid(a):
    ys, xs = np.nonzero(a > 0.5)
    return xs.mean(), ys.mean()

def hull_area(mask):
    pts = cv2.findNonZero((mask > 0.3).astype(np.uint8))
    return cv2.contourArea(cv2.convexHull(pts)) if pts is not None and len(pts) >= 3 else 1.0

def xform(cx, cy, theta, s, flip, tx, ty):
    c, sn = math.cos(theta), math.sin(theta)
    fx = -1.0 if flip else 1.0
    R = np.array([[c, -sn], [sn, c]]) @ np.array([[fx * s, 0], [0, s]])
    t = np.array([tx, ty]) - R @ np.array([cx, cy])
    return np.hstack([R, t[:, None]]).astype(np.float32)

def score(a, sk, M, w, h, bs, bd, bm):
    w_ = cv2.warpAffine(a, M, (w, h), flags=cv2.INTER_LINEAR)
    sc = ncc(cv2.GaussianBlur(w_, (0, 0), 1.6), bs)
    k_ = cv2.warpAffine(sk, M, (w, h), flags=cv2.INTER_NEAREST) > 0.3
    prec = (k_ & bd).sum() / max(k_.sum(), 1)
    rec = (bm & (cv2.dilate((w_ > 0.5).astype(np.uint8), np.ones((3, 3), np.uint8)) > 0)).sum() / max(bm.sum(), 1)
    return sc + prec + rec

def ncc(a, b):
    a = a - a.mean(); b = b - b.mean()
    d = math.sqrt((a * a).sum() * (b * b).sum()) + 1e-6
    return float((a * b).sum() / d)

def load_faces(root, size, flipv):
    j = json.load(open(os.path.join(root, "Assets", "DiceModels", "Data", "glow.json")))
    faces = []
    for k in j["kinds"]:
        fs = k["faceSize"]; n = len(k["pos"]) // 3 // fs
        for f in range(n):
            pts = []
            for i in range(fs):
                u, v = k["uv"][(f * fs + i) * 2], k["uv"][(f * fs + i) * 2 + 1]
                pts.append((u * size, (1 - v) * size if flipv else v * size))
            if k["kind"] == "D4":
                vals = [k["values"][k["corner"][f * fs + i]] for i in range(fs)]
                faces.append((np.array(pts, np.float32), None, vals))
            else:
                faces.append((np.array(pts, np.float32), k["values"][f], None))
    return faces

def allowed_for(faces, cx, cy):
    for pts, val, corner_vals in faces:
        if cv2.pointPolygonTest(pts.reshape(-1, 1, 2), (float(cx), float(cy)), False) >= 0:
            if val is not None: return [str(val)] + ([str(val) + "."] if val in (6, 9) else [])
            d = [np.hypot(p[0] - cx, p[1] - cy) for p in pts]
            return [str(corner_vals[int(np.argmin(d))])]
    return None

def main(root, out, size=2048, spread=28, level=0.5):
    """size/spread: SDF resolution and its range in pixels (28 at 2048 = 14 at 1024); the shaders only see the 0..1 distance."""
    files = sorted(glob.glob(os.path.join(root, "Assets", "DiceModels", "Textures", "glow_*_emission.png")))
    lum = None
    for f in files:
        v = np.array(Image.open(f).convert("RGB")).astype(np.float32).max(axis=2) / 255.0
        lum = v if lum is None else np.maximum(lum, v)
    m = np.clip((lum - 0.04) / 0.30, 0, 1)
    merged = cv2.dilate((m > 0.3).astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7)))
    lab, n = ndimage.label(merged)
    k = size / m.shape[0]
    canvas = np.zeros((size, size), np.float32)
    jobs = []
    best_f = None
    for flipv in (True, False):
        faces_try = load_faces(root, m.shape[0], flipv)
        hits = 0
        for ci in range(1, n + 1):
            yy, xx = np.nonzero(lab == ci)
            if allowed_for(faces_try, xx.mean(), yy.mean()): hits += 1
        print("flipv", flipv, "blobs assigned", hits, "of", n)
        if best_f is None or hits > best_f[0]: best_f = (hits, faces_try)
    faces = best_f[1]
    cache = {}
    report = []
    for ci in range(1, n + 1):
        region = (lab == ci)
        if (region & (m > 0.3)).sum() < 12: continue
        ys, xs = np.nonzero(region)
        x0, x1, y0, y1 = xs.min() - 6, xs.max() + 7, ys.min() - 6, ys.max() + 7
        x0, y0 = max(x0, 0), max(y0, 0); x1 = min(x1, m.shape[1]); y1 = min(y1, m.shape[0])
        blob = (m * region)[y0:y1, x0:x1]
        h, w = blob.shape
        bs = cv2.GaussianBlur(blob, (0, 0), 1.6)
        bcx, bcy = centroid(blob)
        bm = blob > 0.3
        bd = cv2.dilate(bm.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))) > 0
        harea = hull_area(blob)
        yy_, xx_ = np.nonzero(region)
        allowed = allowed_for(faces, xx_.mean(), yy_.mean()) or CANDS
        best = (-2, None)
        for text in allowed:
            if text not in cache:
                a = render(text, NOMINAL)
                cache[text] = (a, skeletonize(a > 0.5).astype(np.float32), hull_area(a), centroid(a))
            a, sk, ca, (cx, cy) = cache[text]
            s0 = math.sqrt(harea / ca)
            for flip in (False, True):
                for th in np.arange(0, 360, 6):
                    M = xform(cx, cy, math.radians(th), s0, flip, bcx, bcy)
                    # blurred candidate scaled to the blob: blur sigma in blob space ~ 1.6 -> blur in cand space 1.6/s0
                    sc = score(a, sk, M, w, h, bs, bd, bm)
                    if sc > best[0]: best = (sc, (text, flip, th, s0, bcx, bcy, cx, cy, a, sk))
        sc, (text, flip, th, s, tx, ty, cx, cy, a, sk) = best
        # refine
        step = [3.0, 0.06, 1.0, 1.0]
        cur = [th, s, tx, ty]
        for it in range(40):
            improved = False
            for i in range(4):
                for sign in (-1, 1):
                    t = cur.copy(); t[i] += sign * step[i]
                    M = xform(cx, cy, math.radians(t[0]), t[1], flip, t[2], t[3])
                    sc2 = score(a, sk, M, w, h, bs, bd, bm)
                    if sc2 > sc + 1e-4: sc, cur, improved = sc2, t, True
            if not improved:
                step = [x * 0.5 for x in step]
                if step[0] < 0.2: break
        th, s, tx, ty = cur
        report.append((ci, text, flip, round(th), round(s, 2), round(sc, 2), (x0, y0)))
        if sc < 1.2:   # no good match: keep the original glyph, just smoothed
            up = cv2.resize(blob, (int(w * k), int(h * k)), interpolation=cv2.INTER_CUBIC)
            yy, xx = int(y0 * k), int(x0 * k)
            canvas[yy:yy + up.shape[0], xx:xx + up.shape[1]] = np.maximum(canvas[yy:yy + up.shape[0], xx:xx + up.shape[1]], up[:canvas.shape[0] - yy, :canvas.shape[1] - xx])
            continue
        jobs.append([text, flip, th, s, x0 + tx, y0 + ty, (xx_.mean(), yy_.mean())])
    two = [j[3] for j in jobs if len(j[0]) == 2 and j[0] != "20"]
    med = float(np.median(two)) if two else None
    for text, flip, th, s, ax, ay, cen in jobs:
        if text == "20" and med:       # the atlas holds only a clipped crescent of this glyph: size and place it like the other two-digit numbers
            s = med
            for pts, val, _ in faces:
                if val == 20: ax, ay = pts[:, 0].mean(), pts[:, 1].mean()
        hi = render(text, NOMINAL * s * k)
        hcx, hcy = centroid(hi)
        M = xform(hcx, hcy, math.radians(th), 1.0, flip, ax * k, ay * k)
        w_ = cv2.warpAffine(hi, M, (size, size), flags=cv2.INTER_LINEAR)
        canvas = np.maximum(canvas, w_)
    # signed distance field
    canvas = cv2.GaussianBlur(canvas, (0, 0), 0.7)
    inside = canvas > level
    dist = ndimage.distance_transform_edt(inside) - ndimage.distance_transform_edt(~inside)
    sdf = np.clip(0.5 + dist / (2.0 * spread), 0, 1)
    g = (sdf * 255 + 0.5).astype(np.uint8)
    Image.fromarray(np.stack([g, g, g], -1), "RGB").save(out)
    for r in report: print(r)
    print("numerals written:", out)

if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
