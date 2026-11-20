"""Gets one character's chosen art ready for tools/blender/paint_from_art.py: the front, side and
back figures of a model sheet cut out on transparent backgrounds, and what Blender needs to place
them on the body: each view's height in pixels, its centre line, and its outline row by row.

  python tools/model/prep_art.py <name> <sheet.png> --front 0 --side 1 [--back 2] [--side-faces right|left]
      (art venv; writes tools/model/art/<name>/)
"""
import os
import sys
import json
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sheet import figures


def kmeans(x, k, iters=20, seed=1):
    rng = np.random.default_rng(seed)
    sub = x[rng.choice(len(x), min(len(x), 20000), replace=False)]
    c = sub[rng.choice(len(sub), k, replace=False)]
    for _ in range(iters):
        lab = np.argmin(((sub[:, None, :] - c[None]) ** 2).sum(-1), axis=1)
        for j in range(k):
            if (lab == j).any():
                c[j] = sub[lab == j].mean(axis=0)
    full = np.empty(len(x), np.int32)
    for a in range(0, len(x), 50000):
        full[a:a + 50000] = np.argmin(((x[a:a + 50000, None, :] - c[None]) ** 2).sum(-1), axis=1)
    return c, full


def clean(rgb, mask, k=12):
    """The drawing split into what a hand-painted game texture keeps: flat colour regions (the paint,
    without its brush noise) and the thin ink lines (folds, seams, features). Big black shapes are
    the drawing's lighting or a black garment: they stay paint, not line."""
    from scipy import ndimage
    from skimage.color import rgb2lab, lab2rgb
    from skimage.morphology import disk
    H = mask.shape[0]
    mx = rgb.max(axis=2)
    dark = (mx < 0.22) & mask
    r = max(2, int(round(H * 0.005)))
    thick = ndimage.binary_opening(dark, structure=disk(r))
    lines = dark & ~ndimage.binary_dilation(thick, structure=disk(1))
    lab_, n = ndimage.label(lines)
    if n:
        sizes = ndimage.sum(lines, lab_, range(1, n + 1))
        lines = np.isin(lab_, [i + 1 for i, s in enumerate(sizes) if s >= 5])
    # The outline of the whole figure is the 3D edge pass's job, not the texture's.
    rim = mask & ~ndimage.binary_erosion(mask, structure=disk(r + 1))
    lines &= ~rim
    ink = np.where(lines, np.clip((0.3 - mx) / 0.22, 0, 1), 0.0)
    ink = np.maximum(ink, ndimage.gaussian_filter(ink, 0.6) * 0.8)
    # Paint: line pixels take their nearest paint, then a median and a few flat colours.
    src = mask & ~lines
    idx = ndimage.distance_transform_edt(~src, return_distances=False, return_indices=True)
    base = rgb[idx[0], idx[1]]
    base = np.stack([ndimage.median_filter(base[..., c], size=5) for c in range(3)], axis=2)
    lab = rgb2lab(base)
    c, lbl = kmeans(lab[mask].astype(np.float32), k)
    L = np.full(mask.shape, -1, np.int32)
    L[mask] = lbl
    # Majority vote in a small window: no speckles between the regions.
    votes = np.stack([ndimage.uniform_filter((L == j).astype(np.float32), 7) for j in range(k)], axis=0)
    L = np.where(mask, np.argmax(votes, axis=0), -1)
    flat = np.zeros_like(rgb)
    flat[mask] = lab2rgb(c[L[mask]][None])[0]
    return flat, ink


def runs(row):
    r = np.flatnonzero(np.diff(np.concatenate([[0], row.astype(np.int8), [0]])))
    return [[int(a), int(b)] for a, b in zip(r[0::2], r[1::2])]


def main():
    a = sys.argv
    name, path = a[1], a[2]
    get = lambda k, d=None: a[a.index(k) + 1] if k in a else d
    figs = figures(path)
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "art", name)
    os.makedirs(out, exist_ok=True)
    views = {}
    for view in ("front", "side", "back"):
        idx = get("--" + view)
        if idx is None:
            continue
        f = figs[int(idx)]
        m = f["mask"]
        rgba = np.dstack([f["rgb"], m[..., None].astype(np.float32)])
        file = os.path.join(out, view + ".png")
        Image.fromarray((rgba * 255).astype(np.uint8), "RGBA").save(file)
        flat, ink = clean(f["rgb"], m)
        Image.fromarray((np.dstack([flat, m[..., None].astype(np.float32)]) * 255).astype(np.uint8), "RGBA").save(
            os.path.join(out, view + "_flat.png"))
        Image.fromarray((ink * 255).astype(np.uint8), "L").save(os.path.join(out, view + "_ink.png"))
        band = m[int(m.shape[0] * 0.42):int(m.shape[0] * 0.52)]
        xs = np.nonzero(band)[1]
        c = float(np.median(xs)) if len(xs) else m.shape[1] / 2
        views[view] = dict(file=view + ".png", width=int(m.shape[1]), height=int(m.shape[0]), centre=c,
                           rows=[runs(m[r]) for r in range(m.shape[0])])
    views["side_faces"] = get("--side-faces", "right")
    # Face landmarks in the front cut-out (pixels), marked by hand: tools/model/art/<name>.marks.json
    mk = os.path.join(os.path.dirname(out), name + ".marks.json")
    if os.path.exists(mk):
        views["landmarks"] = json.load(open(mk))
    views["sheet"] = os.path.abspath(path)
    with open(os.path.join(out, "views.json"), "w") as fh:
        json.dump(views, fh)
    print("ART", out, {k: (v["width"], v["height"]) for k, v in views.items() if isinstance(v, dict) and "width" in v})


if __name__ == "__main__":
    main()
