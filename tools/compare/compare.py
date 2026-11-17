"""How alike a render is to a target picture of the same character, as numbers.

  python tools/compare/compare.py <target.png> <render.png> [--out diff.png]

Both pictures are cropped to the figure (everything not the background colour of the corners),
scaled to the same size and compared three ways:

  shape   structural similarity (SSIM) of the grey images: forms, shadows, proportions
  ink     how well the dark lines agree (F1 of the ink masks, allowing 2 px of wobble)
  colour  how alike the colours are (histogram intersection in Lab, 8x8x8 bins)

score = 0.4 shape + 0.4 ink + 0.2 colour, from 0 to 1. A diff picture shows the target's ink in
blue, the render's in red, both in black: what to move. 0.9 is the bar for "looks the same".
"""
import sys
import numpy as np
from PIL import Image, ImageFilter

SIZE = 512


def load(path):
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(np.float32) / 255.0
    # The background: the median of the four corners. Crop to whatever differs from it.
    corners = np.concatenate([a[:8, :8].reshape(-1, 3), a[:8, -8:].reshape(-1, 3), a[-8:, :8].reshape(-1, 3), a[-8:, -8:].reshape(-1, 3)])
    bg = np.median(corners, axis=0)
    mask = np.abs(a - bg).sum(axis=2) > 0.12
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return im.resize((SIZE, SIZE))
    pad = 6
    box = (max(xs.min() - pad, 0), max(ys.min() - pad, 0), min(xs.max() + pad, im.width), min(ys.max() + pad, im.height))
    im = im.crop(box)
    # Fit into a square, keeping the proportions (they are part of the likeness).
    w, h = im.size
    s = SIZE / max(w, h)
    im = im.resize((max(1, int(w * s)), max(1, int(h * s))), Image.LANCZOS)
    out = Image.new("RGB", (SIZE, SIZE), tuple(int(c * 255) for c in bg))
    out.paste(im, ((SIZE - im.width) // 2, (SIZE - im.height) // 2))
    return out


def grey(im):
    return np.asarray(im.convert("L")).astype(np.float64) / 255.0


def blur(a, r):
    im = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(r))).astype(np.float64) / 255.0


def ssim(x, y):
    c1, c2 = 0.01 ** 2, 0.03 ** 2
    mx, my = blur(x, 3), blur(y, 3)
    vx = blur(x * x, 3) - mx * mx
    vy = blur(y * y, 3) - my * my
    cxy = blur(x * y, 3) - mx * my
    m = ((2 * mx * my + c1) * (2 * cxy + c2)) / ((mx * mx + my * my + c1) * (vx + vy + c2))
    return float(np.clip(m.mean(), 0, 1))


def ink(im):
    """The dark strokes: much darker than their surroundings, and dark in themselves."""
    g = grey(im)
    local = blur(g, 4)
    return (g < local - 0.12) & (g < 0.35)


def dilate(m, r):
    im = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(2 * r + 1))
    return np.asarray(im) > 0


def ink_f1(a, b):
    if a.sum() == 0 and b.sum() == 0:
        return 1.0
    precision = (b & dilate(a, 2)).sum() / max(b.sum(), 1)
    recall = (a & dilate(b, 2)).sum() / max(a.sum(), 1)
    return float(2 * precision * recall / max(precision + recall, 1e-9))


def lab_hist(im):
    a = np.asarray(im.convert("LAB")).reshape(-1, 3) // 32
    idx = a[:, 0] * 64 + a[:, 1] * 8 + a[:, 2]
    h = np.bincount(idx, minlength=512).astype(np.float64)
    return h / h.sum()


def main():
    args = [x for x in sys.argv[1:] if not x.startswith("--")]
    out = sys.argv[sys.argv.index("--out") + 1] if "--out" in sys.argv else None
    t, r = load(args[0]), load(args[1])
    shape = ssim(grey(t), grey(r))
    it, ir = ink(t), ink(r)
    lines = ink_f1(it, ir)
    colour = float(np.minimum(lab_hist(t), lab_hist(r)).sum())
    score = 0.4 * shape + 0.4 * lines + 0.2 * colour
    print("shape %.3f  ink %.3f  colour %.3f  ->  score %.3f%s" % (shape, lines, colour, score, "  (the same)" if score >= 0.9 else ""))
    if out:
        d = np.full((SIZE, SIZE, 3), 255, np.uint8)
        d[it] = (60, 110, 230)
        d[ir] = (230, 70, 60)
        d[it & ir] = (20, 20, 20)
        Image.fromarray(d).save(out)


if __name__ == "__main__":
    main()
