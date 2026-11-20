"""A body from concept art: the front, side and back views of a model sheet become a closed mesh
that matches their outlines, plus the joints of the game's skeleton, measured from the outlines.

  python tools/model/build_from_art.py <sheet.png> --front 0 --side 1 --back 2 --height 1.84 --out <dir>/<name>
      (art venv: numpy, scipy, scikit-image)

How: the body is rebuilt row by row. In each row of the front view the outline splits into the
trunk (or head), the arms and the legs; each piece becomes a cross-section, an ellipse as wide as
the front view says and as deep as the side view says (slightly boxy for head and trunk, round
for limbs). The nose is added from the profile. The stacked cross-sections fill a voxel grid, which
is smoothed and turned into a surface (marching cubes).

Coordinates (as tools/blender): x = the character's left, -y = front, z = up, feet at z = 0.
Writes <out>.obj (the surface), <out>.json (joints, landmarks, and how each view maps onto the
body, for painting the texture) and <out>_check.png (the outlines and joints drawn over the views).
"""
import json
import sys
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
from skimage import measure

sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
from sheet import figures

VOX = 0.004          # metres per voxel


def runs(row):
    """Runs of True in a boolean row: [(start, end_exclusive), ...]."""
    r = np.flatnonzero(np.diff(np.concatenate([[0], row.astype(np.int8), [0]])))
    return list(zip(r[0::2], r[1::2]))


class View:
    def __init__(self, fig, height, sign, is_side=False):
        self.m = fig["mask"]
        self.rgb = fig["rgb"]
        self.box = fig["box"]
        self.h = self.m.shape[0]
        self.s = height / self.h                 # metres per pixel
        self.H = height
        self.sign = sign
        self.side = is_side
        # The middle of the body: the centre of the outline over the hips.
        band = self.m[int(self.h * 0.42):int(self.h * 0.52)]
        cols = np.nonzero(band.any(axis=0))[0]
        xs = np.nonzero(band)[1]
        self.c = float(np.median(xs)) if len(xs) else self.m.shape[1] / 2

    def row(self, z):
        r = int(round((self.H - z) / self.s))
        if r < 0 or r >= self.h:
            return np.zeros(self.m.shape[1], bool)
        return self.m[r]

    def to_m(self, px):
        """Image column -> metres along this view's horizontal axis (x for front, -y for side)."""
        return (px - self.c) * self.s * self.sign


def build(path, fi, si, bi, H, out):
    figs = figures(path)
    front = View(figs[fi], H, +1)
    side = View(figs[si], H, -1, True)      # facing right: image right is the front (-y)
    back = View(figs[bi], H, -1) if bi is not None else None
    nz = int(H / VOX) + 6
    nx, ny = int(1.3 / VOX), int(0.8 / VOX)
    vol = np.zeros((nx, ny, nz), np.float32)
    X = (np.arange(nx) - nx / 2) * VOX
    Y = (np.arange(ny) - ny / 2) * VOX
    XX, YY = np.meshgrid(X, Y, indexing="ij")

    # The crotch: the highest row, from the bottom, where the middle column is outside the legs.
    crotch = None
    for z in np.arange(H * 0.15, H * 0.6, VOX):          # above the boots and any shadow on the ground
        r = front.row(z)
        if r[int(front.c)]:
            crotch = z
            break
    crotch = crotch or H * 0.45

    # The side view's front edge over the head, smoothed: the nose is what sticks out of it.
    zs = np.arange(0, H, VOX)
    side_front = np.full(len(zs), np.nan)
    side_back = np.full(len(zs), np.nan)
    for i, z in enumerate(zs):
        rr = runs(side.row(z))
        if rr:
            a, b = rr[0][0], rr[-1][1]
            y0, y1 = side.to_m(a), side.to_m(b - 1)       # sign -1: image left is back (+y)
            side_front[i], side_back[i] = min(y0, y1), max(y0, y1)
    head_from = H * 0.80
    win = int(0.03 / VOX)
    smooth_front = side_front.copy()
    for i, z in enumerate(zs):
        if z >= head_from and not np.isnan(side_front[i]):
            lo, hi = max(0, i - win), min(len(zs), i + win + 1)
            smooth_front[i] = np.nanmax(side_front[lo:hi])         # the most backward: protrusions removed

    arms_rows, legs_rows, trunk_rows = [], [], []
    for i, z in enumerate(zs):
        rr = runs(front.row(z))
        if not rr or np.isnan(side_front[i]):
            continue
        yf, yb = side_front[i], side_back[i]
        for a, b in rr:
            x0, x1 = front.to_m(a), front.to_m(b - 1)
            cx, hw = (x0 + x1) / 2, max((x1 - x0) / 2, VOX)
            contains_mid = a <= front.c < b
            if z < crotch:
                kind = "leg"
            elif contains_mid:
                kind = "trunk"
            else:
                kind = "arm"
            if kind == "trunk":
                cy, hd, p = (yf + yb) / 2, max((yb - yf) / 2, VOX), (2.2 if z >= head_from else 2.5)
                front_lim = None
                if z >= head_from and not np.isnan(smooth_front[i]):
                    bump = max(0.0, smooth_front[i] - yf)                # how far the nose (or chin) stands out
                    cy = (smooth_front[i] + yb) / 2
                    hd = max((yb - smooth_front[i]) / 2, VOX)
                    front_lim = bump
                trunk_rows.append((z, cx, hw))
                ex = np.abs(XX - cx) / hw
                ey = np.abs(YY - cy) / hd
                inside = ex ** p + ey ** p <= 1.0
                if front_lim:
                    # The nose: a narrow ridge down the middle of the face, as far out as the profile says.
                    ridge = front_lim * np.exp(-((XX - cx) / 0.011) ** 2)
                    nose = (YY < cy) & (YY >= cy - hd - ridge) & (np.abs(XX - cx) < 0.03)
                    inside |= nose & (ex < 0.6)
            elif kind == "arm":
                arms_rows.append((z, cx, hw))
                cy = (yf + yb) / 2
                inside = ((XX - cx) / hw) ** 2 + ((YY - cy) / hw) ** 2 <= 1.0
            else:
                legs_rows.append((z, cx, hw))
                cy, hd = (yf + yb) / 2, max((yb - yf) / 2, VOX)
                inside = ((XX - cx) / hw) ** 2 + ((YY - cy) / hd) ** 2 <= 1.0
            k = int(round(z / VOX)) + 2
            vol[:, :, k] = np.maximum(vol[:, :, k], inside.astype(np.float32))

    vol = ndimage.gaussian_filter(vol, sigma=1.3)
    verts, faces, normals, _ = measure.marching_cubes(vol, level=0.5)
    verts[:, 0] = (verts[:, 0] - nx / 2) * VOX
    verts[:, 1] = (verts[:, 1] - ny / 2) * VOX
    verts[:, 2] = (verts[:, 2] - 2) * VOX
    with open(out + ".obj", "w") as f:
        for v in verts:
            f.write("v %.5f %.5f %.5f\n" % tuple(v))
        for t in faces:
            f.write("f %d %d %d\n" % (t[0] + 1, t[2] + 1, t[1] + 1))

    J = joints(front, side, H, crotch, trunk_rows, arms_rows, legs_rows, zs, side_front, side_back)
    views = {"front": dict(box=[int(v) for v in front.box], c=front.c, s=front.s, sign=1, sheet=path),
             "side": dict(box=[int(v) for v in side.box], c=side.c, s=side.s, sign=-1, sheet=path)}
    if back is not None:
        views["back"] = dict(box=[int(v) for v in back.box], c=back.c, s=back.s, sign=-1, sheet=path)
    with open(out + ".json", "w") as f:
        json.dump(dict(height=H, crotch=crotch, joints=J, views=views), f, indent=1)
    check(front, side, J, H, out + "_check.png")
    print("MESH %s  %d verts  %d tris  crotch %.2f m" % (out, len(verts), len(faces), crotch))


def joints(front, side, H, crotch, trunk, arms, legs, zs, sf, sb):
    S = H / 1.8

    def depth_mid(z):
        i = int(np.clip(round(z / VOX), 0, len(zs) - 1))
        if np.isnan(sf[i]):
            return 0.0
        return float((sf[i] + sb[i]) / 2)

    t = np.array(trunk)
    # The neck: the narrowest trunk row between the shoulders and the head.
    band = t[(t[:, 0] > H * 0.72) & (t[:, 0] < H * 0.92)]
    neck_z = float(band[np.argmin(band[:, 2]), 0]) if len(band) else H * 0.84
    J = {}
    J["Hips"] = [0.0, depth_mid(crotch + 0.06 * H), crotch + 0.06 * H]
    J["Torso"] = [0.0, depth_mid(crotch + 0.16 * H), crotch + 0.16 * H]
    J["Head"] = [0.0, depth_mid(neck_z + 0.02 * H), neck_z + 0.02 * H]
    a = np.array(arms) if arms else np.zeros((0, 3))
    for side_sign, tag in ((1, "L"), (-1, "R")):
        arm = a[(a[:, 1] * side_sign) > 0] if len(a) else a
        if len(arm) < 4:
            # No arm away from the body: put it where a hanging arm would be.
            sx = float(np.max(np.abs(t[(t[:, 0] > H * 0.66) & (t[:, 0] < H * 0.74), 1]) + t[(t[:, 0] > H * 0.66) & (t[:, 0] < H * 0.74), 2]))
            top = H * 0.80
            J["Shoulder" + tag] = [side_sign * sx * 0.82, depth_mid(top), top]
            J["Elbow" + tag] = [side_sign * sx * 1.0, depth_mid(top - 0.17 * H), top - 0.17 * H]
            J["Hand" + tag] = [side_sign * sx * 1.05, depth_mid(top - 0.33 * H), top - 0.33 * H]
            continue
        arm = arm[np.argsort(-arm[:, 0])]                     # top (armpit) first
        armpit = arm[0]
        tip = arm[-1]
        # The shoulder sits inside the top of the arm, above the armpit.
        trunk_at = t[np.argmin(np.abs(t[:, 0] - armpit[0]))]
        edge = abs(trunk_at[1]) + trunk_at[2]
        sh = np.array([side_sign * (edge - 0.03 * S), armpit[0] + 0.045 * H])
        tipp = np.array([tip[1], tip[0]])
        # Along the arm's middle: the wrist at 84% of the way to the fingertips, the elbow at 45%.
        pts = np.vstack([sh, arm[:, [1, 0]]])
        seg = np.sqrt((np.diff(pts, axis=0) ** 2).sum(axis=1))
        acc = np.concatenate([[0], np.cumsum(seg)])

        def along(f):
            d = f * acc[-1]
            i = int(np.searchsorted(acc, d)) - 1
            i = int(np.clip(i, 0, len(pts) - 2))
            u = (d - acc[i]) / max(seg[i], 1e-6)
            return pts[i] + (pts[i + 1] - pts[i]) * u

        el, wr = along(0.45), along(0.84)
        J["Shoulder" + tag] = [float(sh[0]), depth_mid(sh[1]), float(sh[1])]
        J["Elbow" + tag] = [float(el[0]), depth_mid(el[1]), float(el[1])]
        J["Hand" + tag] = [float(wr[0]), depth_mid(wr[1]), float(wr[1])]
    l = np.array(legs) if legs else np.zeros((0, 3))
    for side_sign, tag in ((1, "L"), (-1, "R")):
        leg = l[(l[:, 1] * side_sign) > 0] if len(l) else l

        def cx_at(z):
            if len(leg) == 0:
                return side_sign * 0.09 * S
            return float(leg[np.argmin(np.abs(leg[:, 0] - z)), 1])
        hip_z = crotch + 0.03 * H
        knee_z, ankle_z = 0.27 * H, 0.055 * H
        J["Hip" + tag] = [cx_at(crotch - 0.02 * H), depth_mid(hip_z), hip_z]
        J["Knee" + tag] = [cx_at(knee_z), depth_mid(knee_z), knee_z]
        J["Foot" + tag] = [cx_at(ankle_z + 0.03 * H), depth_mid(ankle_z) - 0.01 * S, ankle_z]
    J["_neck_z"] = neck_z
    return J


def check(front, side, J, H, path):
    """The outlines with the joints on top: to look at."""
    def img(v):
        a = (v.rgb * 255).astype(np.uint8).copy()
        a[~v.m] = (a[~v.m] * 0.3 + 180).astype(np.uint8)
        return Image.fromarray(a)
    fi, si = img(front), img(side)
    canvas = Image.new("RGB", (fi.width + si.width + 20, max(fi.height, si.height)), (255, 255, 255))
    canvas.paste(fi, (0, 0)); canvas.paste(si, (fi.width + 20, 0))
    d = ImageDraw.Draw(canvas)
    for name, p in J.items():
        if name.startswith("_"):
            continue
        x, y, z = p
        fx, fy = front.c + x / front.s, (H - z) / front.s
        sx, sy = fi.width + 20 + side.c + (-y) / side.s, (H - z) / side.s
        for (u, v) in ((fx, fy), (sx, sy)):
            d.ellipse([u - 4, v - 4, u + 4, v + 4], fill=(255, 0, 80))
    canvas.save(path)


if __name__ == "__main__":
    a = sys.argv
    path = a[1]
    get = lambda k, d=None: a[a.index(k) + 1] if k in a else d
    back = get("--back")
    build(path, int(get("--front", 0)), int(get("--side", 1)), int(back) if back is not None else None, float(get("--height", 1.8)), get("--out"))
