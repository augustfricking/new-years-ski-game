"""Cuts the figures out of a character model sheet (several views of one person side by side on a
plain background) and measures them.

  python tools/model/sheet.py <sheet.png> [--out dir]     (art venv: numpy, scipy, scikit-image)

For each figure: its mask (the outline filled), its colour crop, its box, and the rows where skis
or a board stick out under the boots are cut away. Used by build_from_art.py; run alone it writes
the masks so they can be looked at.
"""
import os
import sys
import numpy as np
from PIL import Image
from scipy import ndimage


def background(a):
    border = np.concatenate([a[:6].reshape(-1, 3), a[-6:].reshape(-1, 3), a[:, :6].reshape(-1, 3), a[:, -6:].reshape(-1, 3)])
    return np.median(border, axis=0)


def figure_mask(a):
    bg = background(a)
    diff = np.abs(a - bg).sum(axis=2)
    dark = a.mean(axis=2) < 0.45
    m = (diff > 0.16) | dark
    m = ndimage.binary_closing(m, iterations=2)
    # Fill only small holes (specks inside the drawing): a big one is real background, such as the
    # gap between the legs closed off by a shadow on the ground.
    holes = ndimage.binary_fill_holes(m) & ~m
    lab, n = ndimage.label(holes)
    if n:
        sizes = ndimage.sum(holes, lab, range(1, n + 1))
        small = np.isin(lab, [i + 1 for i, sz in enumerate(sizes) if sz < 0.0004 * m.size])
        m = m | small
    m = ndimage.binary_opening(m, iterations=1)
    return m


def cut_gear(m):
    """Skis and boards under the boots: the bottom rows that are suddenly much wider than the boots."""
    rows = np.nonzero(m.any(axis=1))[0]
    if len(rows) < 20:
        return m
    top, bottom = rows[0], rows[-1]
    h = bottom - top
    widths = m.sum(axis=1)
    ref = np.median(widths[bottom - int(0.13 * h): bottom - int(0.06 * h)])
    cut = bottom + 1
    for r in range(bottom - int(0.06 * h), bottom + 1):
        if widths[r] > 1.45 * ref:
            cut = r
            break
    out = m.copy()
    out[cut:] = False
    # Whatever is left below must still be part of the figure (no ski tips floating loose).
    lab, n = ndimage.label(out)
    if n > 1:
        sizes = ndimage.sum(out, lab, range(1, n + 1))
        out = lab == (1 + int(np.argmax(sizes)))
    return out


def figures(path, expect=None):
    img = Image.open(path).convert("RGB")
    a = np.asarray(img).astype(np.float32) / 255.0
    m = figure_mask(a)
    lab, n = ndimage.label(m)
    sizes = ndimage.sum(m, lab, range(1, n + 1))
    big = [i + 1 for i, s in enumerate(sizes) if s > 0.012 * m.size]
    figs = []
    for i in big:
        fm = lab == i
        ys, xs = np.nonzero(fm)
        figs.append((xs.min(), fm))
    figs.sort(key=lambda t: t[0])
    out = []
    for _, fm in figs:
        fm = cut_gear(fm)
        ys, xs = np.nonzero(fm)
        box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
        out.append(dict(mask=fm[box[1]:box[3], box[0]:box[2]], rgb=a[box[1]:box[3], box[0]:box[2]], box=box))
    return out


if __name__ == "__main__":
    path = sys.argv[1]
    od = sys.argv[sys.argv.index("--out") + 1] if "--out" in sys.argv else os.path.dirname(path)
    figs = figures(path)
    for k, f in enumerate(figs):
        x0, y0, x1, y1 = f["box"]
        print("figure %d: box %s  size %dx%d" % (k, f["box"], x1 - x0, y1 - y0))
        rgba = np.dstack([f["rgb"], f["mask"][..., None].astype(np.float32)])
        Image.fromarray((rgba * 255).astype(np.uint8), "RGBA").save(os.path.join(od, "_fig%d.png" % k))
