"""Makes the two textures the DiceFX shaders need. Run from anywhere: python make_dicefx_textures.py --root <project root>

  dicefx_numerals.png  numeral SIGNED DISTANCE FIELD, 2048x2048, same UV atlas as the glow/cosmic dice models. Every numeral is redrawn from a
                       bold font (make_dicefx_numerals.py), placed like the glyph of the glow set's emission map. 0.5 = glyph edge, above 0.5 inside, below outside (SDF_SPREAD pixels
                       = full range). Shaders threshold it, so the numbers stay sharp at any size and can be thickened.
  dicefx_noise.png     256x256 tileable noise pack, linear data:
                       R = Worley cell edge distance (0 on a crack line, 1 deep inside a cell)
                       G = soft fbm noise A     B = soft fbm noise B     A = Worley F1 distance (cell shape)
"""
import argparse, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from PIL import Image

NOISE_SIZE = 256
WORLEY_CELLS = 8


def fft_noise(n, beta, seed):
    rng = np.random.default_rng(seed)
    f = np.fft.fft2(rng.standard_normal((n, n)))
    fx = np.fft.fftfreq(n)[:, None]
    fy = np.fft.fftfreq(n)[None, :]
    r = np.sqrt(fx * fx + fy * fy)
    r[0, 0] = 1.0
    f = f / (r ** beta)
    f[0, 0] = 0.0
    x = np.real(np.fft.ifft2(f))
    lo, hi = np.percentile(x, [1, 99])
    return np.clip((x - lo) / (hi - lo), 0, 1)


def worley(n, cells, seed):
    rng = np.random.default_rng(seed)
    pts = rng.random((cells, cells, 2))
    ys, xs = np.mgrid[0:n, 0:n]
    px = xs / n * cells
    py = ys / n * cells
    cx = np.floor(px).astype(int)
    cy = np.floor(py).astype(int)
    d1 = np.full((n, n), 9.0)
    d2 = np.full((n, n), 9.0)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            ix = (cx + dx) % cells
            iy = (cy + dy) % cells
            qx = cx + dx + pts[iy, ix, 0]
            qy = cy + dy + pts[iy, ix, 1]
            d = np.sqrt((px - qx) ** 2 + (py - qy) ** 2)
            d2 = np.minimum(d2, np.maximum(d1, d))
            d1 = np.minimum(d1, d)
    edge = d2 - d1
    return np.clip(edge / np.percentile(edge, 99), 0, 1), np.clip(d1 / np.percentile(d1, 99), 0, 1)


def make_noise(path):
    edge, f1 = worley(NOISE_SIZE, WORLEY_CELLS, 7)
    g = fft_noise(NOISE_SIZE, 1.6, 11)
    b = fft_noise(NOISE_SIZE, 1.2, 23)
    img = np.stack([edge, g, b, f1], axis=-1)
    Image.fromarray((img * 255 + 0.5).astype(np.uint8), "RGBA").save(path)


def make_numerals(root, path):
    import make_dicefx_numerals   # redraws every numeral of the atlas from a font, as a 2048px signed distance field
    make_dicefx_numerals.main(root, path)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--out", default=None)
    a = ap.parse_args()
    out = a.out or os.path.join(a.root, "Assets", "DiceModels", "FX")
    os.makedirs(out, exist_ok=True)
    make_numerals(a.root, os.path.join(out, "dicefx_numerals.png"))
    make_noise(os.path.join(out, "dicefx_noise.png"))
    print("written to", out)
