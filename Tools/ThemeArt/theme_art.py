"""Generates the textures and 9-slice UI frames of the DiceThrower themes (PIL + numpy).
Usage: python theme_art.py <Assets dir> [theme ...]   (themes: cult cath elf druid tavla)
Everything is seeded, so a run always gives the same pictures. Re-run after changing a palette.
"""
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

# ----------------------------------------------------------------------------- helpers


def fbm(n, seed, beta=2.0):
    """Tileable cloud noise in 0..1 (random-phase spectrum shaped by 1/f^beta)."""
    rng = np.random.default_rng(seed)
    spec = np.fft.fft2(rng.standard_normal((n, n)))
    fx = np.fft.fftfreq(n)[:, None]
    fy = np.fft.fftfreq(n)[None, :]
    r = np.sqrt(fx * fx + fy * fy)
    r[0, 0] = 1.0
    spec = spec / (r ** beta)
    spec[0, 0] = 0
    a = np.real(np.fft.ifft2(spec))
    return (a - a.min()) / (a.max() - a.min())


def to_img(arr):
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))


def tint(h, dark, light):
    """Maps a 0..1 field to a colour ramp -> float array (n, n, 3)."""
    dark = np.array(dark, dtype=float)
    light = np.array(light, dtype=float)
    return dark + (light - dark) * h[..., None]


def cracks(n, seed, count, length, width, wander=0.5):
    """Tileable crack mask 0..1: random walks drawn on a 3x3 canvas and folded back."""
    rnd = random.Random(seed)
    canvas = Image.new("L", (3 * n, 3 * n), 0)
    d = ImageDraw.Draw(canvas)
    for _ in range(count):
        x = rnd.uniform(n, 2 * n)
        y = rnd.uniform(n, 2 * n)
        ang = rnd.uniform(0, math.tau)
        w = width
        for _ in range(length):
            ang += rnd.uniform(-wander, wander)
            step = rnd.uniform(6, 14)
            nx, ny = x + math.cos(ang) * step, y + math.sin(ang) * step
            d.line([(x, y), (nx, ny)], fill=255, width=max(1, int(w)))
            if rnd.random() < 0.07:  # a branch
                bx, by, ba = nx, ny, ang + rnd.choice([-1, 1]) * rnd.uniform(0.5, 1.1)
                for _ in range(length // 4):
                    ba += rnd.uniform(-wander, wander)
                    ex, ey = bx + math.cos(ba) * 8, by + math.sin(ba) * 8
                    d.line([(bx, by), (ex, ey)], fill=255, width=max(1, int(w * 0.6)))
                    bx, by = ex, ey
            x, y = nx, ny
            w = max(1.0, w - 0.01)
    a = np.asarray(canvas, dtype=float) / 255.0
    return a.reshape(3, n, 3, n).max(axis=(0, 2))


def joints(n, cells, seed, thickness):
    """Tileable flagstone joints: 1 on the gaps between slabs, 0 on the slabs."""
    rnd = random.Random(seed)
    c = n // cells
    m = np.zeros((n, n))
    yy, xx = np.mgrid[0:n, 0:n]
    for row in range(cells):
        off = rnd.randint(0, c - 1)
        y0 = row * c
        m[(yy - y0) % n < thickness] = 1
        for col in range(cells):
            x0 = (col * c + off) % n
            band = (yy >= y0) & (yy < y0 + c)
            m[band & (((xx - x0) % n) < thickness)] = 1
    return np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))) / 255.0


def save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    print("wrote", path, img.size)


def supersample(size, factor=2):
    return size * factor


def circle_mask(size):
    m = Image.new("L", (size, size), 0)
    ImageDraw.Draw(m).ellipse([0, 0, size - 1, size - 1], fill=255)
    return m


def polar(c, r, ang):
    return (c + r * math.cos(ang), c + r * math.sin(ang))


# ----------------------------------------------------------------------------- Cultist


def cult_floor(n=512):
    h = fbm(n, 11, 2.2) * 0.65 + fbm(n, 12, 1.2) * 0.35
    col = tint(h, (14, 10, 12), (44, 32, 34))
    stain = fbm(n, 13, 2.6)
    col += (stain[..., None] > 0.62) * np.array([22, 0, 2]) * ((stain[..., None] - 0.62) * 6)
    j = joints(n, 4, 14, 5)
    col *= (1 - 0.8 * j)[..., None]
    cr = cracks(n, 15, 7, 50, 3, 0.55)
    col *= (1 - 0.92 * cr)[..., None]
    return to_img(col).convert("RGB")


def cult_sigil(out=1024):
    S = out * 2
    c = S / 2
    R = S / 2
    base = np.asarray(cult_floor(512).resize((S, S), Image.BICUBIC), dtype=float) * 0.8
    # candle light pooling on the stone at the five star points (the scene has no real light per candle)
    yy, xx = np.mgrid[0:S, 0:S]
    for i in range(5):
        px, py = polar(c, 0.88 * R, -math.pi / 2 + i * math.tau / 5)
        dist = np.sqrt((xx - px) ** 2 + (yy - py) ** 2) / (0.34 * R)
        k = np.clip(1 - dist, 0, 1) ** 2
        base += k[..., None] * np.array([210.0, 80.0, 28.0])
    img = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)).convert("RGBA")

    red = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(red)
    crimson = (196, 32, 46, 255)
    dark = (122, 16, 24, 255)
    bone = (233, 223, 196, 255)

    def ring(f, w, col):
        r = f * R
        d.ellipse([c - r, c - r, c + r, c + r], outline=col, width=max(1, int(w * R)))

    ring(0.985, 0.012, dark)
    ring(0.955, 0.006, crimson)
    ring(0.905, 0.006, crimson)
    for i in range(40):  # rune ticks between the two rings
        a = i * math.tau / 40
        long_ = i % 2 == 0
        r0, r1 = (0.915, 0.945) if long_ else (0.925, 0.94)
        d.line([polar(c, r0 * R, a), polar(c, r1 * R, a)], fill=crimson, width=int(0.006 * R))
    ring(0.88, 0.005, dark)

    pts = [polar(c, 0.88 * R, -math.pi / 2 + i * math.tau / 5) for i in range(5)]
    order = [0, 2, 4, 1, 3, 0]
    star = [pts[k] for k in order]
    d.line(star, fill=crimson, width=int(0.02 * R), joint="curve")
    for p in pts:
        d.ellipse([p[0] - 0.03 * R, p[1] - 0.03 * R, p[0] + 0.03 * R, p[1] + 0.03 * R], fill=dark, outline=crimson, width=int(0.006 * R))
    ring(0.34, 0.006, crimson)

    glow = red.filter(ImageFilter.GaussianBlur(S * 0.012))
    img = Image.alpha_composite(img, glow)
    img = Image.alpha_composite(img, red)

    # blood pools and drips
    blood = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    bd = ImageDraw.Draw(blood)
    rnd = random.Random(3)
    for _ in range(7):
        a = rnd.uniform(0, math.tau)
        rr = rnd.uniform(0.45, 0.8) * R
        x, y = polar(c, rr, a)
        w, h = rnd.uniform(0.05, 0.12) * R, rnd.uniform(0.03, 0.07) * R
        bd.ellipse([x - w, y - h, x + w, y + h], fill=(86, 6, 12, 210))
    for _ in range(14):
        a = rnd.uniform(0, math.tau)
        x, y = polar(c, rnd.uniform(0.3, 0.62) * R, a)
        ln = rnd.uniform(0.04, 0.16) * R
        bd.line([(x, y), (x + rnd.uniform(-8, 8), y + ln)], fill=(96, 8, 14, 220), width=int(0.008 * R))
        bd.ellipse([x - 7, y + ln - 6, x + 7, y + ln + 8], fill=(96, 8, 14, 220))
    blood = blood.filter(ImageFilter.GaussianBlur(S * 0.0025))
    img = Image.alpha_composite(img, blood)

    # the eye
    eye = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ed = ImageDraw.Draw(eye)
    w, h = 0.24 * R, 0.13 * R
    upper = [(c + w * t, c - h * (1 - t * t) ** 0.9) for t in np.linspace(-1, 1, 60)]
    lower = [(c + w * t, c + h * (1 - t * t) ** 0.9) for t in np.linspace(1, -1, 60)]
    ed.polygon(upper + lower, fill=(16, 4, 8, 255), outline=(232, 217, 138, 255))
    ed.line(upper + lower[:1], fill=(232, 217, 138, 255), width=int(0.008 * R))
    ed.line(lower + upper[:1], fill=(232, 217, 138, 255), width=int(0.008 * R))
    ir = 0.085 * R
    ed.ellipse([c - ir, c - ir, c + ir, c + ir], fill=(179, 32, 44, 255), outline=(232, 217, 138, 255), width=int(0.004 * R))
    ed.ellipse([c - 0.012 * R, c - 0.075 * R, c + 0.012 * R, c + 0.075 * R], fill=(5, 1, 2, 255))
    img = Image.alpha_composite(img, eye.filter(ImageFilter.GaussianBlur(0.8)))

    img = img.convert("RGB").resize((out, out), Image.LANCZOS)
    return img


# ----------------------------------------------------------------------------- Cathedral


def marble(n, seed, base, vein, veins=1.0, contrast=0.5):
    """Soft cloudy stone with a few long wavy veins (veins = how many cross the tile)."""
    h = fbm(n, seed, 2.0)
    yy, xx = np.mgrid[0:n, 0:n] / n
    warp = (h - 0.5) * 1.6
    v = np.abs(np.sin((xx * 1.0 + yy * 1.0 + warp) * math.pi * veins))
    v = np.power(1 - v, 40)
    v2 = np.power(1 - np.abs(np.sin((xx * 1.0 - yy * 1.0 + warp * 1.4) * math.pi * veins * 1.5 + 1.3)), 90) * 0.5
    vm = Image.fromarray((np.clip(v + v2, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(n / 256))
    v = np.asarray(vm, dtype=float) / 255.0
    soft = fbm(n, seed + 5, 2.6)
    col = tint(soft, np.array(base) * 0.93, np.array(base))
    col = col * (1 - v[..., None] * contrast) + np.array(vein) * (v[..., None] * contrast)
    return to_img(col).convert("RGB")


def cath_floor(n=512):
    img = marble(n, 31, (58, 40, 104), (170, 140, 235), 1.0, 0.6)
    arr = np.asarray(img, dtype=float)
    j = joints(n, 2, 33, 6)
    arr = arr * (1 - 0.75 * j[..., None]) + np.array([227, 191, 106]) * (0.35 * j[..., None] * 0.0)
    return to_img(arr).convert("RGB")


def cath_marble(n=512):
    return marble(n, 41, (234, 227, 244), (126, 96, 190), 1.0, 0.55)


def cath_pedestal(out=1024):
    S = out * 2
    c = S / 2
    R = S / 2
    img = marble(S // 2, 43, (240, 234, 248), (130, 100, 196), 1.0, 0.5).resize((S, S), Image.BICUBIC).convert("RGBA")
    gold = (227, 191, 106, 255)
    gold_d = (176, 138, 48, 255)
    violet = (107, 75, 176, 255)
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    def ring(f, w, col):
        r = f * R
        d.ellipse([c - r, c - r, c + r, c + r], outline=col, width=max(1, int(w * R)))

    ring(0.985, 0.016, gold)
    ring(0.955, 0.006, gold_d)
    ring(0.90, 0.008, violet)
    for i in range(16):  # the studs of the outer band
        p = polar(c, 0.93 * R, i * math.tau / 16)
        d.ellipse([p[0] - 0.012 * R, p[1] - 0.012 * R, p[0] + 0.012 * R, p[1] + 0.012 * R], fill=gold)
    ring(0.86, 0.004, gold)

    # 8-point star: two squares, one turned 45 degrees
    def square(rot, r):
        return [polar(c, r, rot + k * math.pi / 2 + math.pi / 4) for k in range(4)]

    star_r = 0.76 * R
    for rot in (0, math.pi / 4):
        pts = square(rot, star_r)
        d.polygon(pts, fill=(107, 75, 176, 70), outline=violet)
        d.line(pts + pts[:1], fill=gold, width=int(0.006 * R))
    ring(0.5, 0.01, gold)
    ring(0.46, 0.004, violet)
    d.ellipse([c - 0.4 * R, c - 0.4 * R, c + 0.4 * R, c + 0.4 * R], fill=(107, 75, 176, 90))

    img = Image.alpha_composite(img, layer)

    # glowing cross
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    arm, th = 0.30 * R, 0.035 * R
    gd.rectangle([c - th, c - arm * 1.1, c + th, c + arm * 1.1], fill=(255, 251, 232, 255))
    gd.rectangle([c - arm * 0.75, c - arm * 0.45 - th, c + arm * 0.75, c - arm * 0.45 + th], fill=(255, 251, 232, 255))
    halo = glow.filter(ImageFilter.GaussianBlur(S * 0.03))
    halo2 = glow.filter(ImageFilter.GaussianBlur(S * 0.07))
    img = Image.alpha_composite(img, halo2)
    img = Image.alpha_composite(img, halo)
    cross = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    cd = ImageDraw.Draw(cross)
    cd.rectangle([c - th * 0.8, c - arm * 1.05, c + th * 0.8, c + arm * 1.05], fill=gold)
    cd.rectangle([c - arm * 0.7, c - arm * 0.45 - th * 0.8, c + arm * 0.7, c - arm * 0.45 + th * 0.8], fill=gold)
    img = Image.alpha_composite(img, cross)

    return img.convert("RGB").resize((out, out), Image.LANCZOS)


def cath_ray(w=128, h=512, color=(255, 246, 208)):
    u = np.linspace(0, 1, w)[None, :]
    v = np.linspace(0, 1, h)[:, None]
    across = np.power(np.clip(1 - np.abs(2 * u - 1), 0, 1), 1.6)
    along = np.power(np.clip(1 - v, 0, 1), 1.3) * np.clip(v * 14, 0, 1)
    a = np.clip(across * along, 0, 1)
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = color
    rgba[..., 3] = (a * 255).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def cath_window(w=256, h=512):
    S = 2
    W, H = w * S, h * S
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    mask = Image.new("L", (W, H), 0)
    md = ImageDraw.Draw(mask)
    r = W / 2
    # lancet arch: two arcs meeting at the top
    top_y = 0.06 * H
    spring = top_y + r * 1.15
    pts = [(0.04 * W, H * 0.98), (0.04 * W, spring)]
    for t in np.linspace(0, 1, 30):
        pts.append((0.04 * W + t * 0.46 * W, spring - (spring - top_y) * (t ** 1.7)))
    for t in np.linspace(1, 0, 30):
        pts.append((0.96 * W - t * 0.46 * W, spring - (spring - top_y) * (t ** 1.7)))
    pts += [(0.96 * W, spring), (0.96 * W, H * 0.98)]
    md.polygon(pts, fill=255)
    arr = np.zeros((H, W, 3))
    yy = np.linspace(0, 1, H)[:, None]
    noise = fbm(W, 51, 2.0)[:H, :W] if H <= W else np.tile(fbm(W, 51, 2.0), (2, 1))[:H, :W]
    top = np.array([140, 100, 230])
    bot = np.array([60, 40, 140])
    arr = top + (bot - top) * yy[..., None] * np.ones((1, W, 1))
    arr = arr * (0.75 + 0.5 * noise[..., None])
    glass = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).convert("RGBA")
    lead = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lead)
    lc = (227, 191, 106, 255)
    for k in range(1, 4):
        x = k * W / 4
        ld.line([(x, top_y), (x, H * 0.98)], fill=lc, width=5)
    for k in range(1, 7):
        y = spring + k * (H * 0.98 - spring) / 7
        ld.line([(0, y), (W, y)], fill=lc, width=5)
    ld.ellipse([W / 2 - 0.2 * W, spring - 0.34 * W, W / 2 + 0.2 * W, spring + 0.06 * W], outline=lc, width=6)
    ld.line([(0.04 * W, H * 0.98), (0.04 * W, spring)], fill=lc, width=9)
    ld.line([(0.96 * W, H * 0.98), (0.96 * W, spring)], fill=lc, width=9)
    glass = Image.alpha_composite(glass, lead)
    img.paste(glass, (0, 0), mask)
    img = img.resize((w, h), Image.LANCZOS)
    return img


# ----------------------------------------------------------------------------- 9-slice UI frames (96px; the corner is 32px)


def ss_canvas(size=96, k=4):
    return Image.new("RGBA", (size * k, size * k), (0, 0, 0, 0)), k


def finish(img, size=96):
    return img.resize((size, size), Image.LANCZOS)


def vgrad(box, top, bottom, k):
    x0, y0, x1, y1 = box
    g = Image.new("RGBA", (x1 - x0, y1 - y0))
    px = g.load()
    for y in range(y1 - y0):
        t = y / max(1, y1 - y0 - 1)
        col = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(4))
        for x in range(x1 - x0):
            px[x, y] = col
    return g


def cult_panel():
    img, k = ss_canvas()
    d = ImageDraw.Draw(img)
    s = 96 * k
    inset = 3 * k
    cut = 7 * k
    poly = [(inset + cut, inset), (s - inset - cut, inset), (s - inset, inset + cut), (s - inset, s - inset - cut),
            (s - inset - cut, s - inset), (inset + cut, s - inset), (inset, s - inset - cut), (inset, inset + cut)]
    d.polygon(poly, fill=(12, 4, 7, 240))
    d.line(poly + poly[:1], fill=(122, 16, 24, 255), width=2 * k, joint="curve")
    d.line([(inset + 4 * k + cut, inset + 4 * k), (s - inset - 4 * k - cut, inset + 4 * k)], fill=(60, 8, 14, 200), width=k)
    rnd = random.Random(7)
    for cx, cy, sx, sy in [(inset, inset, 1, 1), (s - inset, inset, -1, 1), (inset, s - inset, 1, -1), (s - inset, s - inset, -1, -1)]:
        x, y = cx + sx * 3 * k, cy + sy * 3 * k
        a = math.atan2(sy, sx) + rnd.uniform(-0.3, 0.3)
        for _ in range(7):
            a += rnd.uniform(-0.5, 0.5)
            nx, ny = x + math.cos(a) * 5 * k, y + math.sin(a) * 5 * k
            d.line([(x, y), (nx, ny)], fill=(196, 32, 46, 255), width=k)
            x, y = nx, ny
    return finish(img)


def cult_button(red=False):
    img, k = ss_canvas()
    d = ImageDraw.Draw(img)
    s = 96 * k
    inset = 3 * k
    cut = 5 * k
    poly = [(inset + cut, inset), (s - inset - cut, inset), (s - inset, inset + cut), (s - inset, s - inset - cut),
            (s - inset - cut, s - inset), (inset + cut, s - inset), (inset, s - inset - cut), (inset, inset + cut)]
    if red:
        g = vgrad((0, 0, s, s), (206, 38, 52, 255), (132, 16, 26, 255), k)
        m = Image.new("L", (s, s), 0)
        ImageDraw.Draw(m).polygon(poly, fill=255)
        img.paste(g, (0, 0), m)
        edge = (233, 223, 196, 255)
    else:
        d.polygon(poly, fill=(21, 7, 9, 245))
        edge = (122, 16, 24, 255)
    d.line(poly + poly[:1], fill=edge, width=2 * k, joint="curve")
    d.line([(inset + 4 * k + cut, inset + 4 * k), (s - inset - 4 * k - cut, inset + 4 * k)],
           fill=(255, 255, 255, 60) if red else (90, 14, 22, 160), width=k)
    return finish(img)


def cult_coin(size=128):
    k = 4
    S = size * k
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = S / 2
    d.ellipse([k, k, S - k, S - k], fill=(28, 6, 10, 255))
    d.ellipse([5 * k, 5 * k, S - 5 * k, S - 5 * k], fill=(233, 223, 196, 255))
    d.ellipse([8 * k, 8 * k, S - 8 * k, S - 8 * k], fill=(150, 20, 32, 255))
    inner = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    idr = ImageDraw.Draw(inner)
    for i in range(10):
        rr = (c - 8 * k) * (1 - i / 10)
        col = (int(150 + 50 * i / 10), int(20 + 18 * i / 10), int(32 + 18 * i / 10), 255)
        idr.ellipse([c - rr, c - rr, c + rr, c + rr], fill=col)
    mask = circle_mask(S).resize((S, S))
    inner_mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(inner_mask).ellipse([8 * k, 8 * k, S - 8 * k, S - 8 * k], fill=255)
    img.paste(inner, (0, 0), inner_mask)
    d = ImageDraw.Draw(img)
    pts = [polar(c, c - 16 * k, -math.pi / 2 + i * math.tau / 5) for i in range(5)]
    d.line([pts[o] for o in [0, 2, 4, 1, 3, 0]], fill=(90, 8, 16, 255), width=2 * k, joint="curve")
    return img.resize((size, size), Image.LANCZOS)


def cath_panel():
    img, k = ss_canvas()
    d = ImageDraw.Draw(img)
    s = 96 * k
    gold = (227, 191, 106, 255)
    d.rectangle([3 * k, 3 * k, s - 3 * k, s - 3 * k], fill=(40, 24, 82, 238))
    d.rectangle([3 * k, 3 * k, s - 3 * k, s - 3 * k], outline=gold, width=2 * k)
    d.rectangle([8 * k, 8 * k, s - 8 * k, s - 8 * k], outline=(227, 191, 106, 150), width=k)
    for cx, cy in [(3 * k, 3 * k), (s - 3 * k, 3 * k), (3 * k, s - 3 * k), (s - 3 * k, s - 3 * k)]:
        r = 6 * k
        d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=(36, 20, 71, 255), outline=gold)
    return finish(img)


def cath_button(gold_fill=False):
    img, k = ss_canvas()
    d = ImageDraw.Draw(img)
    s = 96 * k
    r = 28 * k
    inset = 3 * k
    mask = Image.new("L", (s, s), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle([inset, inset, s - inset, s - inset], radius=r, fill=255, corners=(True, True, False, False))
    if gold_fill:
        g = vgrad((0, 0, s, s), (246, 222, 150, 255), (196, 158, 68, 255), k)
        edge = (140, 104, 36, 255)
    else:
        g = vgrad((0, 0, s, s), (70, 44, 128, 255), (40, 24, 82, 255), k)
        edge = (227, 191, 106, 255)
    img.paste(g, (0, 0), mask)
    d.rounded_rectangle([inset, inset, s - inset, s - inset], radius=r, outline=edge, width=2 * k, corners=(True, True, False, False))
    return finish(img)


def cath_coin(size=128):
    k = 4
    S = size * k
    c = S / 2
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([k, k, S - k, S - k], fill=(36, 20, 71, 255))
    d.ellipse([4 * k, 4 * k, S - 4 * k, S - 4 * k], fill=(176, 138, 48, 255))
    grad = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grad)
    for i in range(12):
        rr = (c - 6 * k) * (1 - i / 12)
        col = (int(214 + 34 * i / 12), int(176 + 38 * i / 12), int(84 + 60 * i / 12), 255)
        gd.ellipse([c - rr, c - rr, c + rr, c + rr], fill=col)
    gm = Image.new("L", (S, S), 0)
    ImageDraw.Draw(gm).ellipse([6 * k, 6 * k, S - 6 * k, S - 6 * k], fill=255)
    img.paste(grad, (0, 0), gm)
    d = ImageDraw.Draw(img)
    ring = (107, 75, 176, 255)
    d.ellipse([14 * k, 14 * k, S - 14 * k, S - 14 * k], outline=ring, width=2 * k)
    for i in range(8):  # rose window petals
        p = polar(c, c - 14 * k, i * math.tau / 8)
        q = polar(c, c - 22 * k, i * math.tau / 8)
        d.line([p, q], fill=ring, width=2 * k)
    return img.resize((size, size), Image.LANCZOS)



# ----------------------------------------------------------------------------- shared by the Sky Elf, Druid and Tavla frames


def arc(cx, cy, r, a0, a1, steps=12):
    return [(cx + r * math.cos(a0 + (a1 - a0) * t / steps), cy + r * math.sin(a0 + (a1 - a0) * t / steps)) for t in range(steps + 1)]


def leaf_poly(x0, y0, x1, y1, big, small):
    """Rectangle whose top-left and bottom-right corners have the big radius, the others the small one (CSS: 22px 6px 22px 6px)."""
    p = arc(x0 + big, y0 + big, big, math.pi, 1.5 * math.pi)
    p += arc(x1 - small, y0 + small, small, 1.5 * math.pi, 2 * math.pi)
    p += arc(x1 - big, y1 - big, big, 0, 0.5 * math.pi)
    p += arc(x0 + small, y1 - small, small, 0.5 * math.pi, math.pi)
    return p


def frame(top, bottom, edge, inner=None, big=20, small=6, edge_w=2, deco=None, dashed_inner=False):
    """A 96px 9-slice frame: gradient fill, outline, optional inner line (dashed = a stitch) and a corner decoration callback."""
    img, k = ss_canvas()
    s = 96 * k
    inset = 3 * k
    poly = leaf_poly(inset, inset, s - inset, s - inset, big * k, small * k)
    grad = vgrad((0, 0, s, s), top, bottom, k)
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)
    img.paste(grad, (0, 0), mask)
    d = ImageDraw.Draw(img)
    d.line(poly + poly[:1], fill=edge, width=edge_w * k, joint="curve")
    if inner is not None:
        gap = 5 * k
        ip = leaf_poly(inset + gap, inset + gap, s - inset - gap, s - inset - gap, max(2, big - 5) * k, max(2, small - 3) * k)
        if dashed_inner:
            for i in range(0, len(ip) - 1, 2):
                d.line([ip[i], ip[i + 1]], fill=inner, width=k)
            d.line(ip[::3], fill=inner, width=k)
        else:
            d.line(ip + ip[:1], fill=inner, width=k)
    if deco is not None:
        deco(d, k, s)
    return finish(img)


def diamond(d, cx, cy, r, fill, outline=None):
    d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=fill, outline=outline)


def coin(size, rim, face_top, face_bottom, ring, mark):
    """A round Roll seal: dark rim, gradient face, a ring and a callback that draws the emblem."""
    k = 4
    S = size * k
    c = S / 2
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([k, k, S - k, S - k], fill=rim)
    face = vgrad((0, 0, S, S), face_top, face_bottom, k)
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).ellipse([5 * k, 5 * k, S - 5 * k, S - 5 * k], fill=255)
    img.paste(face, (0, 0), m)
    d = ImageDraw.Draw(img)
    d.ellipse([12 * k, 12 * k, S - 12 * k, S - 12 * k], outline=ring, width=2 * k)
    mark(d, c, k, S)
    return img.resize((size, size), Image.LANCZOS)


def wood(n, seed, dark, light, lines=14):
    """Tileable plank grain running along x."""
    h = fbm(n, seed, 2.4)
    yy = np.arange(n)[:, None] / n * np.ones((1, n))
    grain = 0.5 + 0.5 * np.sin((yy * lines + (h - 0.5) * 3.0) * math.tau)
    v = 0.6 * grain + 0.4 * fbm(n, seed + 1, 1.2)
    return to_img(tint(v, dark, light)).convert("RGB")


def soft_blob(w, h, seed, count, spread):
    """White soft cloud on transparent: overlapping blurred ellipses with a noisy edge."""
    rnd = random.Random(seed)
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    for _ in range(count):
        cx = w * (0.5 + rnd.uniform(-spread, spread))
        cy = h * (0.55 + rnd.uniform(-spread, spread) * 0.4)
        rx, ry = w * rnd.uniform(0.12, 0.26), h * rnd.uniform(0.18, 0.36)
        d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255)
    m = m.filter(ImageFilter.GaussianBlur(h * 0.06))
    a = np.asarray(m, dtype=float) / 255.0
    a = np.clip((a - 0.25) * 1.6, 0, 1)
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = (a * 235).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


# ----------------------------------------------------------------------------- Sky Elf Cafe

ELF_GOLD = (201, 162, 74, 255)
ELF_GOLD_D = (140, 104, 36, 255)
ELF_PEARL = (255, 246, 220, 255)


def elf_table(out=1024):
    S = out * 2
    c = S / 2
    R = S / 2
    img = marble(S // 2, 61, (252, 246, 232), (214, 190, 140), 1.0, 0.35).resize((S, S), Image.BICUBIC).convert("RGBA")
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    def ring(f, w, col):
        r = f * R
        d.ellipse([c - r, c - r, c + r, c + r], outline=col, width=max(1, int(w * R)))

    ring(0.985, 0.014, ELF_GOLD)
    ring(0.95, 0.005, ELF_GOLD)
    for i in range(24):  # the leaf studs of the rim band
        a = i * math.tau / 24
        p = polar(c, 0.92 * R, a)
        q = polar(c, 0.97 * R, a)
        d.line([p, q], fill=ELF_GOLD, width=int(0.006 * R))
    ring(0.89, 0.006, ELF_GOLD)

    def petal(angle, mid, rx, ry, fill, outline, width):
        pts = []
        for t in np.linspace(0, math.tau, 40):
            x, y = rx * math.cos(t), ry * math.sin(t)
            ca, sa = math.cos(angle), math.sin(angle)
            px, py = polar(c, mid, angle)
            pts.append((px + y * ca - x * sa, py + y * sa + x * ca))  # y along the radius, x across it
        d.polygon(pts, fill=fill, outline=outline)
        d.line(pts + pts[:1], fill=outline, width=width)

    for i in range(12):  # long petals
        petal(i * math.tau / 12, 0.56 * R, 0.1 * R, 0.3 * R, (201, 162, 74, 28), ELF_GOLD, int(0.004 * R))
    for i in range(12):  # short petals between them
        petal((i + 0.5) * math.tau / 12, 0.4 * R, 0.05 * R, 0.17 * R, (0, 0, 0, 0), ELF_GOLD, int(0.003 * R))
    ring(0.2, 0.005, ELF_GOLD)
    for i in range(24):  # dotted inner ring
        p = polar(c, 0.15 * R, i * math.tau / 24)
        d.ellipse([p[0] - 0.006 * R, p[1] - 0.006 * R, p[0] + 0.006 * R, p[1] + 0.006 * R], fill=ELF_GOLD)
    ring(0.07, 0.004, ELF_GOLD)
    img = Image.alpha_composite(img, layer.filter(ImageFilter.GaussianBlur(0.6)))
    return img.convert("RGB").resize((out, out), Image.LANCZOS)


def elf_cloud():
    return soft_blob(512, 256, 71, 7, 0.28)


def elf_corner(size=256):
    """A gold art-nouveau whiplash scroll for the corners (transparent background)."""
    k = 3
    S = size * k
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def spiral(cx, cy, r0, r1, a0, turns, direction, width):
        pts = []
        n = 90
        for i in range(n + 1):
            t = i / n
            a = a0 + direction * turns * math.tau * t
            r = r0 + (r1 - r0) * t
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
        d.line(pts, fill=ELF_GOLD, width=width, joint="curve")
        return pts[-1]

    w = int(0.014 * S)
    spiral(0.30 * S, 0.30 * S, 0.17 * S, 0.02 * S, 0.0, 1.35, 1, w)
    spiral(0.66 * S, 0.14 * S, 0.1 * S, 0.015 * S, math.pi, 1.2, -1, w)
    spiral(0.14 * S, 0.66 * S, 0.1 * S, 0.015 * S, -math.pi / 2, 1.2, 1, w)
    d.line([(0.02 * S, 0.02 * S), (0.3 * S, 0.13 * S), (0.47 * S, 0.3 * S)], fill=ELF_GOLD, width=w, joint="curve")
    d.line([(0.02 * S, 0.02 * S), (0.13 * S, 0.3 * S), (0.3 * S, 0.47 * S)], fill=ELF_GOLD, width=w, joint="curve")
    for cx, cy in [(0.47 * S, 0.3 * S), (0.3 * S, 0.47 * S), (0.05 * S, 0.05 * S)]:
        r = 0.03 * S
        d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=ELF_GOLD)
    return img.resize((size, size), Image.LANCZOS)


def elf_deco(d, k, s):
    diamond(d, 8 * k, 8 * k, 3 * k, ELF_GOLD)
    diamond(d, s - 8 * k, s - 8 * k, 3 * k, ELF_GOLD)


def elf_panel():
    return frame((255, 251, 238, 242), (250, 240, 214, 242), ELF_GOLD, (201, 162, 74, 150), 20, 6, 2, elf_deco)


def elf_button(gold=False):
    if gold:
        return frame((246, 222, 150, 255), (201, 162, 74, 255), ELF_GOLD_D, (255, 246, 220, 160), 24, 6, 2)
    return frame((255, 250, 232, 255), (246, 232, 196, 255), ELF_GOLD, None, 24, 6, 2)


def elf_coin(size=128):
    def mark(d, c, k, S):
        for i in range(8):  # a lotus: eight petals
            a = i * math.tau / 8
            p, q = polar(c, 0.1 * S, a), polar(c, 0.27 * S, a)
            d.line([p, q], fill=(255, 246, 220, 255), width=2 * k)
        d.ellipse([c - 0.07 * S, c - 0.07 * S, c + 0.07 * S, c + 0.07 * S], fill=(255, 246, 220, 255))
    return coin(size, (140, 104, 36, 255), (246, 222, 150, 255), (201, 162, 74, 255), (255, 246, 220, 255), mark)


# ----------------------------------------------------------------------------- Druid Cove


def druid_nest(out=1024):
    S = out * 2
    c = S / 2
    R = S / 2
    rnd = random.Random(81)
    base = tint(fbm(512, 82, 2.0), (112, 84, 44), (176, 146, 84))
    img = Image.fromarray(np.clip(np.asarray(Image.fromarray(base.astype(np.uint8)).resize((S, S), Image.BICUBIC)), 0, 255).astype(np.uint8)).convert("RGBA")
    straw = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(straw)
    palette = [(205, 172, 96, 255), (182, 148, 85, 255), (150, 118, 66, 255), (222, 196, 124, 255)]
    for _ in range(2600):  # woven straw: short strokes that mostly follow the circle
        a = rnd.uniform(0, math.tau)
        r = math.sqrt(rnd.uniform(0, 1)) * R * 0.98
        x, y = polar(c, r, a)
        ang = a + math.pi / 2 + rnd.uniform(-0.9, 0.9)
        ln = rnd.uniform(0.05, 0.14) * R
        d.line([(x, y), (x + ln * math.cos(ang), y + ln * math.sin(ang))], fill=rnd.choice(palette), width=max(2, int(0.005 * R)))
    img = Image.alpha_composite(img, straw)
    moss = fbm(512, 83, 2.6)
    ma = np.clip((moss - 0.58) * 5.0, 0, 1)
    ma = np.asarray(Image.fromarray((ma * 255).astype(np.uint8)).resize((S, S), Image.BICUBIC).filter(ImageFilter.GaussianBlur(S * 0.004)), dtype=float) / 255.0
    mossc = np.zeros((S, S, 4), dtype=np.uint8)
    mossc[..., 0], mossc[..., 1], mossc[..., 2] = 74, 122, 58
    mossc[..., 3] = (ma * 210).astype(np.uint8)
    img = Image.alpha_composite(img, Image.fromarray(mossc, "RGBA"))
    twigs = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    td = ImageDraw.Draw(twigs)
    for _ in range(22):
        a = rnd.uniform(0, math.tau)
        r = rnd.uniform(0.15, 0.9) * R
        x, y = polar(c, r, a)
        ang = rnd.uniform(0, math.tau)
        ln = rnd.uniform(0.12, 0.3) * R
        td.line([(x, y), (x + ln * math.cos(ang), y + ln * math.sin(ang))], fill=(84, 58, 30, 255), width=max(3, int(0.008 * R)))
    img = Image.alpha_composite(img, twigs.filter(ImageFilter.GaussianBlur(0.8)))
    return img.convert("RGB").resize((out, out), Image.LANCZOS)


def druid_bark(n=256):
    h = fbm(n, 91, 1.8)
    yy = np.arange(n)[:, None] / n * np.ones((1, n))
    ridge = np.abs(np.sin((yy * 9 + (h - 0.5) * 2.2) * math.pi))
    v = 0.55 * ridge + 0.45 * fbm(n, 92, 1.4)
    return to_img(tint(v, (52, 34, 20), (128, 92, 56))).convert("RGB")


def druid_moss(n=512):
    h = fbm(n, 93, 2.0) * 0.6 + fbm(n, 94, 1.0) * 0.4
    col = tint(h, (22, 58, 34), (58, 112, 62))
    spots = fbm(n, 95, 2.8)
    col *= (1 - 0.35 * (spots[..., None] > 0.68))
    return to_img(col).convert("RGB")


def druid_fern(size=256):
    k = 3
    S = size * k
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    greens = [(46, 107, 58, 255), (61, 130, 71, 255), (37, 96, 51, 255), (74, 150, 84, 255)]
    spine = [(0.5 * S + 0.06 * S * math.sin(t * 2.4), S * (0.97 - 0.94 * t)) for t in np.linspace(0, 1, 40)]
    d.line(spine, fill=(37, 96, 51, 255), width=int(0.012 * S))
    for i, (x, y) in enumerate(spine[3::2]):
        t = (i * 2 + 3) / 40
        ln = 0.3 * S * (1 - t) ** 0.7 + 0.03 * S
        for side in (-1, 1):
            a = -math.pi / 2 + side * 1.0
            ex, ey = x + ln * math.cos(a), y + ln * math.sin(a)
            mx, my = (x + ex) / 2, (y + ey) / 2
            w = ln * 0.18
            nx, ny = -math.sin(a), math.cos(a)
            d.polygon([(x, y), (mx + nx * w, my + ny * w), (ex, ey), (mx - nx * w, my - ny * w)], fill=greens[(i + (side > 0)) % 4])
    return img.resize((size, size), Image.LANCZOS)


def druid_egg(n=128):
    rnd = random.Random(96)
    img = Image.new("RGB", (n, n), (207, 230, 228))
    d = ImageDraw.Draw(img)
    for _ in range(90):
        x, y, r = rnd.uniform(0, n), rnd.uniform(0, n), rnd.uniform(0.8, 2.4)
        d.ellipse([x - r, y - r, x + r, y + r], fill=rnd.choice([(106, 127, 122), (138, 169, 166), (86, 104, 96)]))
    return img


DRUID_MOSS = (46, 80, 40)


def druid_deco(d, k, s):
    for cx, cy, a in [(9 * k, 9 * k, -0.8), (s - 9 * k, s - 9 * k, 2.3)]:  # a small leaf in two corners
        pts = []
        for t in np.linspace(0, math.tau, 24):
            x, y = 7 * k * math.cos(t), 3 * k * math.sin(t)
            pts.append((cx + x * math.cos(a) - y * math.sin(a), cy + x * math.sin(a) + y * math.cos(a)))
        d.polygon(pts, fill=(111, 168, 74, 255))


def druid_panel():
    return frame((48, 82, 42, 244), (30, 56, 36, 244), (139, 106, 63, 255), (223, 236, 170, 70), 20, 6, 2, druid_deco)


def druid_button(amber=False):
    if amber:
        return frame((240, 196, 96, 255), (200, 150, 50, 255), (122, 88, 40, 255), (255, 244, 200, 140), 22, 6, 2)
    return frame((52, 80, 44, 255), (40, 66, 40, 255), (139, 106, 63, 255), None, 22, 6, 2)


def druid_coin(size=128):
    def mark(d, c, k, S):
        pts = []
        for t in np.linspace(0, math.tau, 40):
            x, y = 0.2 * S * math.cos(t), 0.09 * S * math.sin(t)
            a = -0.7
            pts.append((c + x * math.cos(a) - y * math.sin(a), c + x * math.sin(a) + y * math.cos(a)))
        d.polygon(pts, fill=(74, 118, 56, 255))
        d.line([polar(c, 0.19 * S, -0.7 + math.pi), polar(c, 0.19 * S, -0.7)], fill=(223, 236, 170, 255), width=k)
    return coin(size, (92, 64, 30, 255), (244, 206, 112, 255), (204, 156, 56, 255), (139, 106, 63, 255), mark)


# ----------------------------------------------------------------------------- Armenian Tavla

TAVLA_RED = (163, 32, 47, 255)
TAVLA_APRICOT = (232, 135, 43, 255)


def tavla_board(w=640, h=970):
    k = 2
    W, H = w * k, h * k
    base = np.asarray(wood(512, 101, (206, 170, 120), (236, 208, 160)).resize((W, H), Image.BICUBIC), dtype=float)
    img = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)).convert("RGBA")
    d = ImageDraw.Draw(img)
    colors = [(128, 34, 40, 255), (58, 36, 24, 255)]
    bar = int(0.05 * H)
    pad = int(0.012 * W)
    half = (H - bar) / 2
    tip = 0.4 * W
    pw = half / 6
    for side in (0, 1):  # left edge, right edge
        for row in range(12):
            quarter = row // 6
            i = row % 6
            y0 = (0 if quarter == 0 else half + bar) + i * pw
            col = colors[(i + side + quarter) % 2]
            if side == 0:
                tri = [(pad, y0 + 4), (pad, y0 + pw - 4), (tip, y0 + pw / 2)]
            else:
                tri = [(W - pad, y0 + 4), (W - pad, y0 + pw - 4), (W - tip, y0 + pw / 2)]
            d.polygon(tri, fill=col)
            d.line(tri + tri[:1], fill=(201, 162, 74, 255), width=2)
    d.rectangle([0, half, W, half + bar], fill=(74, 44, 24, 255))
    d.line([(0, half), (W, half)], fill=(201, 162, 74, 255), width=4)
    d.line([(0, half + bar), (W, half + bar)], fill=(201, 162, 74, 255), width=4)
    for x in (0.18 * W, 0.5 * W, 0.82 * W):
        d.ellipse([x - 7 * k, half + bar / 2 - 7 * k, x + 7 * k, half + bar / 2 + 7 * k], fill=(201, 162, 74, 255))
    img = img.filter(ImageFilter.GaussianBlur(0.5))
    return img.convert("RGB").resize((w, h), Image.LANCZOS)


def tavla_carpet(n=512):
    cell = 64
    img = Image.new("RGB", (n, n), (110, 27, 36))
    d = ImageDraw.Draw(img)
    for gy in range(n // cell):
        for gx in range(n // cell):
            cx, cy = gx * cell + cell / 2, gy * cell + cell / 2
            r = cell * 0.44
            d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], outline=(232, 135, 43), width=3)
            r2 = r * 0.5
            d.polygon([(cx, cy - r2), (cx + r2, cy), (cx, cy + r2), (cx - r2, cy)], fill=(232, 135, 43))
            d.ellipse([cx - 3, cy - 3, cx + 3, cy + 3], fill=(246, 234, 208))
            d.ellipse([gx * cell - 2, gy * cell - 2, gx * cell + 2, gy * cell + 2], fill=(246, 234, 208))
    a = np.asarray(img, dtype=float) * (0.85 + 0.3 * fbm(n, 102, 1.6))[..., None]  # worn, woven unevenness
    return to_img(a).convert("RGB")


def tavla_deco(d, k, s):
    pass


def tavla_panel():
    return frame((76, 40, 24, 245), (52, 26, 16, 245), TAVLA_RED, TAVLA_APRICOT, 8, 3, 2, tavla_deco, dashed_inner=True)


def tavla_button(apricot=False):
    if apricot:
        return frame((246, 164, 74, 255), (214, 112, 32, 255), (110, 50, 14, 255), (255, 232, 190, 150), 12, 4, 2)
    return frame((76, 46, 26, 255), (58, 34, 20, 255), (201, 162, 74, 255), None, 12, 4, 2)


def tavla_coin(size=128):
    def mark(d, c, k, S):
        for i in range(4):  # a kilim diamond, turned
            a = i * math.pi / 4
            p, q = polar(c, 0.26 * S, a), polar(c, 0.26 * S, a + math.pi)
            d.line([p, q], fill=(110, 27, 36, 255), width=2 * k)
        diamond(d, c, c, 0.12 * S, (110, 27, 36, 255), (246, 234, 208, 255))
    return coin(size, (90, 28, 20, 255), (246, 164, 74, 255), (214, 112, 32, 255), (201, 162, 74, 255), mark)


# ----------------------------------------------------------------------------- run


def run(assets, which):
    tex = lambda theme, name: os.path.join(assets, "Textures", theme, name)
    ui = lambda theme, name: os.path.join(assets, "UI", theme, name)
    if "cult" in which:
        save(cult_floor(), tex("CultistHideout", "Tex_CultFloor.png"))
        save(cult_sigil(), tex("CultistHideout", "Tex_CultSigil.png"))
        save(cult_panel(), ui("Cultist", "Ui_CultPanel.png"))
        save(cult_button(False), ui("Cultist", "Ui_CultButton.png"))
        save(cult_button(True), ui("Cultist", "Ui_CultButtonRed.png"))
        save(cult_coin(), ui("Cultist", "Ui_CultRollCoin.png"))
    if "cath" in which:
        save(cath_floor(), tex("Cathedral", "Tex_CathFloor.png"))
        save(cath_marble(), tex("Cathedral", "Tex_CathMarble.png"))
        save(cath_pedestal(), tex("Cathedral", "Tex_CathPedestal.png"))
        save(cath_ray(), tex("Cathedral", "Tex_CathRay.png"))
        save(cath_window(), tex("Cathedral", "Tex_CathWindow.png"))
        save(cath_panel(), ui("Cathedral", "Ui_CathPanel.png"))
        save(cath_button(False), ui("Cathedral", "Ui_CathButton.png"))
        save(cath_button(True), ui("Cathedral", "Ui_CathButtonGold.png"))
        save(cath_coin(), ui("Cathedral", "Ui_CathRollCoin.png"))
    if "elf" in which:
        save(elf_table(), tex("SkyElfCafe", "Tex_ElfTable.png"))
        save(elf_cloud(), tex("SkyElfCafe", "Tex_ElfCloud.png"))
        save(elf_corner(), tex("SkyElfCafe", "Tex_ElfCorner.png"))
        save(elf_panel(), ui("Elf", "Ui_ElfPanel.png"))
        save(elf_button(False), ui("Elf", "Ui_ElfButton.png"))
        save(elf_button(True), ui("Elf", "Ui_ElfButtonGold.png"))
        save(elf_coin(), ui("Elf", "Ui_ElfRollCoin.png"))
    if "druid" in which:
        save(druid_nest(), tex("DruidCove", "Tex_DruidNest.png"))
        save(druid_bark(), tex("DruidCove", "Tex_DruidBark.png"))
        save(druid_moss(), tex("DruidCove", "Tex_DruidMoss.png"))
        save(cath_ray(color=(236, 246, 170)), tex("DruidCove", "Tex_DruidShaft.png"))
        save(druid_fern(), tex("DruidCove", "Tex_DruidFern.png"))
        save(druid_egg(), tex("DruidCove", "Tex_DruidEgg.png"))
        save(druid_panel(), ui("Druid", "Ui_DruidPanel.png"))
        save(druid_button(False), ui("Druid", "Ui_DruidButton.png"))
        save(druid_button(True), ui("Druid", "Ui_DruidButtonAmber.png"))
        save(druid_coin(), ui("Druid", "Ui_DruidRollCoin.png"))
    if "tavla" in which:
        save(tavla_board(), tex("ArmenianTavla", "Tex_TavlaBoard.png"))
        save(tavla_carpet(), tex("ArmenianTavla", "Tex_TavlaCarpet.png"))
        save(wood(256, 103, (62, 38, 24), (118, 78, 46)), tex("ArmenianTavla", "Tex_TavlaWalnut.png"))
        save(tavla_panel(), ui("Tavla", "Ui_TavlaPanel.png"))
        save(tavla_button(False), ui("Tavla", "Ui_TavlaButton.png"))
        save(tavla_button(True), ui("Tavla", "Ui_TavlaButtonApricot.png"))
        save(tavla_coin(), ui("Tavla", "Ui_TavlaRollCoin.png"))


if __name__ == "__main__":
    out = sys.argv[1]
    themes = sys.argv[2:] or ["cult", "cath", "elf", "druid", "tavla"]
    run(out, themes)
