"""Fits a built character (build_hero.py's parts and joints) to its concept art and paints it with
the art itself, so the drawn lines, shadows and colours of the illustration are on the model.

Called from build_hero.py when ART_DIR is set (see tools/model/prep_art.py for that folder).

1. Proportions: heights of the crotch, neck and crown are moved to where the art has them, then
   every height band is widened or narrowed (trunk, legs) and deepened (trunk) to the art's
   outline; arms ride along with their shoulders.
2. One UV atlas for all parts; vertices are split along its seams (the game stores one UV per
   vertex), carrying their colour slot, bone weights and expressions with them.
3. The atlas is painted texel by texel: each point of the body is looked up in the front, side
   (both sides from the one profile) and back views, weighted by how squarely it faces each, and
   only where nothing else of the body is in front of it in that view.
   The art is first cleaned (prep_art.py): flat paint regions and thin ink lines. A two-tone garment
   takes its panels from the art (each face goes to the slot whose colour the art shows there).
   The texture stores the art relative to each colour slot's own colour (1/RATIO = that colour), so
   the shop can still recolour clothes: the game multiplies slot colour by RATIO times the texture.
"""
import os
import json
import math
import numpy as np
import bpy
import bmesh
from mathutils import Vector

TEX = 2048
RATIO = 4.0    # the texture holds art / slot colour divided by this (the game multiplies by it)
REFS = {}      # slot -> the art's colour for it (printed, for the game's default outfit)


def load_png(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]      # top row first
    bpy.data.images.remove(img)
    return a


def dominant(ob):
    """For each vertex the name of its heaviest bone."""
    names = {g.index: g.name for g in ob.vertex_groups}
    out = []
    for v in ob.data.vertices:
        best, bw = None, -1.0
        for ge in v.groups:
            if ge.weight > bw:
                best, bw = names[ge.group], ge.weight
        out.append(best or "Torso")
    return out


TRUNK = {"Hips", "Torso", "Head"}
ARMS = {"ShoulderL", "ElbowL", "HandL", "ShoulderR", "ElbowR", "HandR"}
LEGS = {"HipL", "KneeL", "FootL", "HipR", "KneeR", "FootR"}


def piecewise(xs, ys):
    xs, ys = np.asarray(xs, float), np.asarray(ys, float)
    return lambda z: np.interp(z, xs, ys)


def smooth(a, n=7):
    k = np.ones(n) / n
    pad = np.pad(a, n // 2, mode="edge")
    return np.convolve(pad, k, mode="valid")


def run(parts, J, art_dir, height, marks=None, pairs=None, pair_hint=None):
    pairs_flat = {x for p_ in (pairs or []) for x in p_}
    V = json.load(open(os.path.join(art_dir, "views.json")))
    front, side = V["front"], V["side"]
    back = V.get("back")
    imgs = {k: load_png(os.path.join(art_dir, V[k]["file"])) for k in ("front", "side", "back") if k in V}
    # The cleaned art (tools/model/prep_art.py): flat paint regions and the ink lines on their own.
    inks = {}
    for k in list(imgs):
        fp, ip = os.path.join(art_dir, k + "_flat.png"), os.path.join(art_dir, k + "_ink.png")
        if os.path.exists(fp) and os.path.exists(ip):
            imgs[k] = load_png(fp)
            inks[k] = load_png(ip)[..., 0]

    # ---- 1. proportions -------------------------------------------------------------------------
    sF = height / front["height"]
    rowsF = front["rows"]
    cF = front["centre"]

    def art_row(z, v, s):
        r = int(round((height - z) / s))
        return V[v]["rows"][r] if 0 <= r < len(V[v]["rows"]) else []

    # The art's crotch: the lowest height where the centre column is still inside the outline, going up.
    crotch_a = None
    for z in np.arange(height * 0.15, height * 0.6, 0.004):
        if any(a <= cF < b for a, b in art_row(z, "front", sF)):
            crotch_a = z
            break
    # The art's neck: the narrowest trunk run in the upper part.
    best, neck_a = 1e9, height * 0.84
    for z in np.arange(height * 0.72, height * 0.93, 0.003):
        for a, b in art_row(z, "front", sF):
            if a <= cF < b and (b - a) < best:
                best, neck_a = b - a, z
    crotch_m = (J["HipL"].z + J["HipR"].z) * 0.5 - 0.05 * height / 1.8
    neck_m = J["Head"].z - 0.03 * height / 1.8
    crotch_a = crotch_a or crotch_m
    zmap = piecewise([0.0, crotch_m, neck_m, height], [0.0, crotch_a, neck_a, height])
    print("FIT crotch %.3f->%.3f neck %.3f->%.3f" % (crotch_m, crotch_a, neck_m, neck_a))

    allv = []
    for ob in parts:
        dom = dominant(ob)
        for v, d in zip(ob.data.vertices, dom):
            v.co.z = float(zmap(v.co.z))
            allv.append((ob, v, d))
    for k in J:
        J[k] = Vector((J[k].x, J[k].y, float(zmap(J[k].z))))

    # Widths and depths per height band, art against mesh.
    zs = np.arange(0.0, height, 0.01)
    kx_t, ky_t, cy_t = np.ones(len(zs)), np.ones(len(zs)), np.zeros(len(zs))
    legx = {1: (np.zeros(len(zs)), np.ones(len(zs))), -1: (np.zeros(len(zs)), np.ones(len(zs)))}
    sS = height / side["height"]
    cS = side["centre"]
    trunk_pts = [(v.co.x, v.co.y, v.co.z) for ob, v, d in allv if d in TRUNK]
    leg_pts = [(v.co.x, v.co.y, v.co.z) for ob, v, d in allv if d in LEGS]
    T = np.array(trunk_pts); L = np.array(leg_pts) if leg_pts else np.zeros((0, 3))
    yc_hips = float(np.median(T[np.abs(T[:, 2] - J["Hips"].z) < 0.05, 1])) if len(T) else 0.0
    for i, z in enumerate(zs):
        band = T[np.abs(T[:, 2] - z) < 0.012]
        runs = art_row(z, "front", sF)
        if len(band) > 8 and z > crotch_a:
            for a, b in runs:
                if a <= cF < b:
                    hw_a = max(b - cF, cF - a) * sF
                    hw_m = np.abs(band[:, 0]).max()
                    kx_t[i] = np.clip(hw_a / max(hw_m, 1e-4), 0.7, 1.45)
            sr = art_row(z, "side", sS)
            if sr:
                # Whole outline in the profile (arms hang inside it): front edge and back edge.
                a, b = sr[0][0], sr[-1][1]
                yf_a, yb_a = -(b - cS) * sS + yc_hips, -(a - cS) * sS + yc_hips   # image right is the front (-y)
                yf_m, yb_m = band[:, 1].min(), band[:, 1].max()
                ky = np.clip((yb_a - yf_a) / max(yb_m - yf_m, 1e-4), 0.7, 1.45)
                ky_t[i] = ky
                cy_t[i] = (yf_a + yb_a) / 2 - (yf_m + yb_m) / 2 * ky
        if z < crotch_a and len(L):
            lb = L[np.abs(L[:, 2] - z) < 0.012]
            for sgn in (1, -1):
                side_pts = lb[lb[:, 0] * sgn > 0]
                cand = [(a, b) for a, b in runs if ((a + b) / 2 - cF) * sgn > 0]
                if len(side_pts) > 4 and cand:
                    a, b = max(cand, key=lambda r: r[1] - r[0])
                    ca = ((a + b) / 2 - cF) * sF
                    hw_a = (b - a) / 2 * sF
                    cm = side_pts[:, 0].mean()
                    hw_m = (side_pts[:, 0].max() - side_pts[:, 0].min()) / 2
                    k = np.clip(hw_a / max(hw_m, 1e-4), 0.7, 1.6)
                    legx[sgn][0][i] = ca - cm * k
                    legx[sgn][1][i] = k
    kx_t, ky_t, cy_t = smooth(kx_t), smooth(ky_t), smooth(cy_t)
    for sgn in (1, -1):
        legx[sgn] = (smooth(legx[sgn][0]), smooth(legx[sgn][1]))
    at = lambda arr, z: float(np.interp(z, zs, arr))
    sh_z = (J["ShoulderL"].z + J["ShoulderR"].z) / 2

    def move(x, y, z, d):
        if d in TRUNK:
            return x * at(kx_t, z), y * at(ky_t, z) + at(cy_t, z)
        if d in LEGS:
            sgn = 1 if d.endswith("L") else -1
            return at(legx[sgn][0], z) + x * at(legx[sgn][1], z), y * at(ky_t, crotch_a) + at(cy_t, crotch_a)
        # Arms: carried by the shoulder.
        k = at(kx_t, sh_z)
        sgn = 1 if d.endswith("L") else -1
        return x + J["Shoulder" + ("L" if sgn > 0 else "R")].x * (k - 1.0), y * at(ky_t, sh_z) + at(cy_t, sh_z)

    def map_point(p, region="Head"):
        """Any point (a landmark) through the same fit as the body."""
        z = float(zmap(p.z))
        x, y = move(p.x, p.y, z, region)
        return Vector((x, y, z))
    head_k = (float(np.interp(height, [0, crotch_m, neck_m, height], [0, crotch_a, neck_a, height])) - neck_a) / max(height - neck_m, 1e-4)
    head_k = (height - neck_a) / max(height - neck_m, 1e-4)
    zhead = J["Head"].z
    fit = dict(map=map_point, kz=head_k, kx=at(kx_t, zhead + 0.08 * height / 1.8), ky=at(ky_t, zhead + 0.08 * height / 1.8))

    for ob, v, d in allv:
        v.co.x, v.co.y = move(v.co.x, v.co.y, v.co.z, d)
    for k in list(J.keys()):
        d = "Torso" if k in TRUNK else k
        if not (k in TRUNK or k in ARMS or k in LEGS):
            continue
        x, y = move(J[k].x, J[k].y, J[k].z, d)
        J[k] = Vector((x, y, J[k].z))
    for ob in parts:
        ob.data.update()

    # ---- 2. looking the body up in the art ---------------------------------------------------------
    tris_all = []
    for ob in parts:
        me = ob.data
        me.calc_loop_triangles()
        co = np.array([v.co[:] for v in me.vertices], np.float32)
        for t in me.loop_triangles:
            tris_all.append(co[list(t.vertices)])
    tris_all = np.array(tris_all, np.float32)

    def project(view, pts):
        """Image (u, v) and depth (towards the viewer is smaller) of points in a view."""
        v = V[view]; s = height / v["height"]; c = v["centre"]
        if view == "front":
            return c + pts[..., 0] / s, (height - pts[..., 2]) / s, pts[..., 1]
        if view == "back":
            return c - pts[..., 0] / s, (height - pts[..., 2]) / s, -pts[..., 1]
        return c + (-(pts[..., 1] - yc_hips)) / s, (height - pts[..., 2]) / s, None    # side: depth set per side below

    buffers = {}

    def depth_buffer(view, sign=0):
        if (view, sign) in buffers:
            return buffers[(view, sign)]
        v = V[view]; Hh, Ww = v["height"], v["width"]
        buf = np.full((Hh, Ww), np.inf, np.float32)
        u, w, dep = project(view, tris_all)
        if dep is None:
            dep = -sign * tris_all[..., 0]                   # sign -1: seen from -x (their right side)
        for k in range(len(tris_all)):
            uu, ww, dd = u[k], w[k], dep[k]
            x0, x1 = int(max(np.floor(uu.min()), 0)), int(min(np.ceil(uu.max()), Ww - 1))
            y0, y1 = int(max(np.floor(ww.min()), 0)), int(min(np.ceil(ww.max()), Hh - 1))
            if x1 < x0 or y1 < y0:
                continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
            (ax, ay), (bx, by), (cx, cy) = zip(uu, ww)
            den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(den) < 1e-9:
                continue
            w0 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / den
            w1 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / den
            w2 = 1 - w0 - w1
            ins = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
            if not ins.any():
                continue
            z = w0 * dd[0] + w1 * dd[1] + w2 * dd[2]
            sub = buf[y0:y1 + 1, x0:x1 + 1]
            np.minimum(sub, np.where(ins, z, np.inf), out=sub)
        buffers[(view, sign)] = buf
        return buf

    # The face: the art's eyes, nose, mouth and chin (marked by hand in views.json) are moved onto
    # the model's own, by an affine fitted between them, blended in over the neck.
    warp = None
    lm_art = V.get("landmarks")
    if marks and lm_art:
        names = [k for k in ("eyeL", "eyeR", "nose", "mouth", "chin") if k in lm_art and k in marks]
        src = np.array([[*project("front", np.array(map_point(marks[k])[:], np.float32))[:2]] for k in names], np.float64)
        dst = np.array([lm_art[k] for k in names], np.float64)
        A_, *_ = np.linalg.lstsq(np.hstack([src, np.ones((len(src), 1))]), dst, rcond=None)
        chin_z = map_point(marks["chin"]).z if "chin" in marks else J["Head"].z
        warp = (A_, chin_z - 0.02 * height / 1.8, chin_z + 0.01 * height / 1.8)
        print("FACE warp", np.round(A_.T, 3).tolist())

    def warped(view, u, w, pts):
        if warp is None or view == "back":
            return u, w
        A_, z0, z1 = warp
        h = np.clip((pts[:, 2] - z0) / max(z1 - z0, 1e-4), 0, 1)
        h = h * h * (3 - 2 * h)
        if view == "front":
            uw = np.stack([u, w, np.ones_like(u)], axis=1) @ A_
            return u + (uw[:, 0] - u) * h, w + (uw[:, 1] - w) * h
        # The profile: only heights move (eyes, mouth, chin at the art's heights).
        uw = np.stack([u * 0 + V["front"]["centre"], w, np.ones_like(u)], axis=1) @ A_
        return u, w + (uw[:, 1] - w) * h

    looks = [("front", 0, np.array([0, -1, 0])), ("side", -1, np.array([-1, 0, 0])), ("side", 1, np.array([1, 0, 0]))]
    if back:
        looks.append(("back", 0, np.array([0, 1, 0])))

    def sample(P, N, sharp=6):
        """The art's paint and ink at body points: each view weighted by how squarely the point faces
        it, and only where nothing else of the body is in front of it there."""
        acc = np.zeros((len(P), 3), np.float32)
        iacc = np.zeros(len(P), np.float32)
        wsum = np.zeros(len(P), np.float32)
        for view, sign, d in looks:
            img = imgs[view]
            buf = depth_buffer(view, sign)
            u, w, dep = project(view, P)
            if dep is None:
                dep = -sign * P[:, 0]
            u, w = warped(view, u, w, P)
            ui = np.clip(np.round(u).astype(int), 0, img.shape[1] - 1)
            wi = np.clip(np.round(w).astype(int), 0, img.shape[0] - 1)
            inside = (u >= 0) & (u < img.shape[1]) & (w >= 0) & (w < img.shape[0])
            seen = inside & (dep <= buf[wi, ui] + 0.012)
            facing = np.clip(N @ d, 0, 1) ** sharp
            col = img[wi, ui]
            wgt = facing * seen * col[:, 3] * (1.4 if view == "front" else 1.0)
            # The face is drawn from the front only: the profile's features would land beside the front's.
            if warp is not None:
                face = (P[:, 2] > warp[1]) & (N[:, 1] < -0.2)
                wgt = wgt * np.where(face, 4.0 if view == "front" else 0.05, 1.0)
            acc += col[:, :3] * wgt[:, None]
            if view in inks:
                iacc += inks[view][wi, ui] * wgt
            wsum += wgt
        got = wsum > 1e-3
        colour = np.zeros_like(acc)
        colour[got] = acc[got] / wsum[got, None]
        ink = np.zeros(len(P), np.float32)
        ink[got] = np.clip(iacc[got] / wsum[got] * 1.3, 0, 1)
        return colour, ink, got

    # ---- 3. the jacket's panels where the art has them ---------------------------------------------
    # A two-tone garment (jacket and trim) takes its panels from the art: each face goes to the slot
    # whose colour the art shows there, so the shop's two colours land on the drawn panels.
    fslots = {}
    for ob in parts:
        me = ob.data
        sl = me.attributes["slot"].data
        fs = np.array([int(round(sl[p.vertices[0]].value)) for p in me.polygons], np.int32)
        fslots[ob.name] = fs
    for pair in pairs or []:
        a_, b_ = pair
        cents, norms, where = [], [], []
        for ob in parts:
            me = ob.data
            fs = fslots[ob.name]
            for p in me.polygons:
                if fs[p.index] in pair:
                    cents.append(p.center[:]); norms.append(p.normal[:]); where.append((ob.name, p.index))
        if len(cents) < 50:
            continue
        col, _, got = sample(np.array(cents, np.float32), np.array(norms, np.float32), sharp=3)
        X = col[got]
        # Two colours: start from the two slots' own averages.
        was = np.array([fslots[n][i] for n, i in where])[got]
        c0 = X[was == a_].mean(axis=0) if (was == a_).any() else X.mean(axis=0)
        c1 = X[was == b_].mean(axis=0) if (was == b_).any() else X.mean(axis=0) * 0.5
        for _ in range(12):
            lab = (((X - c1) ** 2).sum(1) < ((X - c0) ** 2).sum(1))
            if lab.all() or (~lab).all():
                break
            c0, c1 = X[~lab].mean(axis=0), X[lab].mean(axis=0)
        if np.abs(c0 - c1).sum() < 0.25 or lab.all() or (~lab).all():
            print("PANELS one colour", pair)
            continue
        # The first slot (the jacket) takes the colour nearer the character's own jacket colour.
        hint = np.array(pair_hint, np.float32) if pair_hint is not None else c1
        one = a_ if np.abs(c1 - hint).sum() < np.abs(c0 - hint).sum() else b_    # the slot that takes colour c1
        zero = b_ if one == a_ else a_
        gi = np.flatnonzero(got)
        for k, j in enumerate(gi):
            n, i = where[j]
            fslots[n][i] = one if lab[k] else zero
        print("PANELS", pair, "colours", np.round(c0, 2).tolist(), np.round(c1, 2).tolist(), "faces", int((~lab).sum()), int(lab.sum()))

    # ---- 4. one atlas, split at its seams and at the panels' edges ----------------------------------
    bpy.ops.object.select_all(action='DESELECT')
    for ob in parts:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(72), island_margin=0.003, area_weight=0.6, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=0.004, rotate=True)
    bpy.ops.uv.seams_from_islands()
    bpy.ops.object.mode_set(mode='OBJECT')
    for ob in parts:
        fs = fslots[ob.name]
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bm.faces.ensure_lookup_table()
        seams = [e for e in bm.edges if e.seam or (len(e.link_faces) == 2 and fs[e.link_faces[0].index] != fs[e.link_faces[1].index])]
        bmesh.ops.split_edges(bm, edges=seams)
        lay = bm.verts.layers.float.get("slot")
        if lay is not None:
            for f in bm.faces:
                if fs[f.index] in (pairs_flat or ()):
                    for v in f.verts:
                        v[lay] = float(fs[f.index])
        bm.to_mesh(ob.data)
        bm.free()
        ob.data.update()

    # ---- 5. paint ----------------------------------------------------------------------------------
    pos = np.zeros((TEX, TEX, 3), np.float32)
    nrm = np.zeros((TEX, TEX, 3), np.float32)
    slot = np.full((TEX, TEX), -1, np.int16)
    for ob in parts:
        me = ob.data
        me.calc_loop_triangles()
        uv = me.uv_layers.active.data
        sl = me.attributes["slot"].data
        co = np.array([v.co[:] for v in me.vertices], np.float32)
        vn = np.array([v.normal[:] for v in me.vertices], np.float32)
        for t in me.loop_triangles:
            vi = t.vertices
            uvs = np.array([uv[l].uv[:] for l in t.loops], np.float32) * TEX
            P_ = co[list(vi)]; N_ = vn[list(vi)]
            s_ = int(round(sl[vi[0]].value))
            x0, y0 = np.floor(uvs.min(axis=0)).astype(int); x1, y1 = np.ceil(uvs.max(axis=0)).astype(int)
            x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, TEX - 1), min(y1, TEX - 1)
            if x1 < x0 or y1 < y0:
                continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            (ax, ay), (bx, by), (cx, cy) = uvs
            den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(den) < 1e-9:
                continue
            w0 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / den
            w1 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / den
            w2 = 1 - w0 - w1
            inside = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
            if not inside.any():
                continue
            W = np.stack([w0[inside], w1[inside], w2[inside]], axis=1)
            px = gx[inside].astype(int); py = gy[inside].astype(int)
            pos[py, px] = W @ P_
            nn = W @ N_
            nrm[py, px] = nn / np.maximum(np.linalg.norm(nn, axis=1, keepdims=True), 1e-6)
            slot[py, px] = s_
    filled = slot >= 0
    P = pos[filled]; N = nrm[filled]
    colour, ink, got = sample(P, N)

    # Relative to each slot's own colour: RATIO (1/RATIO in the texture) is that colour, darker ink,
    # lighter highlights; the game multiplies the slot colour by RATIO times the texture.
    S = slot[filled]
    REFS.clear()
    rel = np.full((len(P), 3), 1.0 / RATIO, np.float32)
    for s_ in np.unique(S):
        m = (S == s_) & got
        if m.sum() < 20:
            continue
        # The slot's own colour is the paint it shows most (the art's flat regions make this a clear mode).
        q = np.round(colour[m] * 24).astype(np.int32)
        keys, counts = np.unique(q[:, 0] * 10000 + q[:, 1] * 100 + q[:, 2], return_counts=True)
        top = keys[np.argmax(counts)]
        mode = np.array([top // 10000, (top // 100) % 100, top % 100], np.float32) / 24.0
        near = np.abs(colour[m] - mode).sum(axis=1) < 0.08
        mean = colour[m][near].mean(axis=0) if near.sum() > 10 else mode
        REFS[int(s_)] = mean
        ratio = np.clip(colour[m] / np.maximum(mean, 0.03)[None, :], 0.0, RATIO)
        ratio = ratio * (1.0 - 0.88 * ink[m])[:, None]
        rel[m] = ratio / RATIO
    tex = np.full((TEX, TEX, 4), 1.0, np.float32)
    tex[..., :3] = 1.0 / RATIO
    tex[filled, :3] = rel
    # Dilate into the gaps between islands (no dark seams when mipmapped).
    for _ in range(6):
        hole = ~filled
        nb = np.zeros_like(tex[..., :3]); cnt = np.zeros((TEX, TEX), np.float32)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sh = np.roll(np.roll(filled, dy, 0), dx, 1)
            nb += np.roll(np.roll(tex[..., :3], dy, 0), dx, 1) * sh[..., None]
            cnt += sh
        grow = hole & (cnt > 0)
        tex[grow, :3] = nb[grow] / cnt[grow, None]
        filled = filled | grow
    print("PAINT", int(got.sum()), "of", len(P), "texels seen in the art")
    return tex[::-1].copy(), J, fit


def save_png(tex, path):
    h, w = tex.shape[:2]
    img = bpy.data.images.new("tex", w, h, alpha=False)
    img.pixels = tex[::-1].ravel()
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)
