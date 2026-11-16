"""Builds a dressed, rigged skier from one of Blender Studio's Human Base Meshes (CC0) and exports
it for the game.

The base mesh is realistic; the game is a comic. So the body is first pushed into comic
proportions (bigger head and hands, harder jaw, cheekbones and brow, a nose with a clear shape,
larger eyes), differently for each character. Then the head and neck stay skin, the torso and arms
become a puffer jacket, the legs ski pants, the hands gloves: each is the body's own surface lifted
off it, smoothed and given seams, so clothes follow the anatomy and bend where the body bends.
Skin weights come from the body's own regions, blended across the joints. The face gets expressions
(blink, mouth open, smile, brows) and inked brows and lash lines.

Run:
  blender --background --disable-autoexec --factory-startup <bundle.blend> \
          --python tools/blender/build_hero.py -- <profile> <export dir> <preview dir>

Profiles: erik, jonte, maja, linn (close-up models) and male, female (lighter models for the crowd).
Output: <export dir>/<profile>.bytes, read by Assets/_Game/Scripts/Game/SkinModel.cs.
"""
import math
import struct
import numpy
import sys
import bmesh
import bpy
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

argv = sys.argv[sys.argv.index("--") + 1:]
NAME, OUT, PREVIEW = argv[0], argv[1], argv[2]

# How far each character is pushed away from the realistic base (metres unless noted).
#   head, hands, eyes, nose: scale factors.   legs: leg length factor.   shoulders: sideways.
#   jaw: the angle of the jaw outward.        chin: (forward, down, width factor).
#   cheek: cheekbones out.                    hollow: the cheek under them drawn in.
#   brow: the ridge forward.                  nose_w: nostril width factor.   lips: fuller (forward).
#   neck: neck width factor.                  detail: how many triangles are kept (1 = close-up).
BASE = dict(head=1.1, hands=1.14, eyes=1.1, nose=1.0, nose_w=1.0, legs=1.04, shoulders=0.0, jaw=0.0, chin=(0.0, 0.0, 1.0),
            cheek=0.003, hollow=0.002, brow=0.0, lips=0.0, neck=1.0, detail=0.72)
PROFILES = {
    # The leading man: square jaw, strong chin, straight nose.
    "erik": dict(sex="male", shoulders=0.012, jaw=0.0055, chin=(0.004, 0.003, 1.1), brow=0.0025, nose=1.06, hollow=0.0035),
    # The snowboarder: broad, soft face, big nose, heavy brow, thick neck.
    "jonte": dict(sex="male", shoulders=0.016, jaw=0.008, chin=(0.001, -0.002, 1.25), brow=0.004, nose=1.2, nose_w=1.22, cheek=0.001, hollow=0.0, neck=1.12, eyes=1.05),
    # Heart-shaped face, big eyes, small nose.
    "maja": dict(sex="female", jaw=-0.003, chin=(0.001, 0.001, 0.82), cheek=0.0045, nose=0.9, nose_w=0.9, eyes=1.17, lips=0.002, hollow=0.0015),
    # Longer and sharper: high cheekbones, pointed chin, straight nose.
    "linn": dict(sex="female", jaw=0.001, chin=(0.003, 0.004, 0.9), cheek=0.0035, hollow=0.0035, nose=1.03, nose_w=0.92, eyes=1.12, lips=0.001),
    "male": dict(sex="male", shoulders=0.01, jaw=0.004, chin=(0.002, 0.001, 1.08), brow=0.002, detail=0.3),
    "female": dict(sex="female", chin=(0.001, 0.001, 0.9), eyes=1.12, lips=0.001, detail=0.3),
}
P = dict(BASE); P.update(PROFILES[NAME])
SEX = P["sex"]
BODY = "GEO-body_%s_realistic" % SEX

# Face sets of the realistic bodies (the same numbers on both).
CHEST, BELLY, PELVIS = 1, 19, 18
UPPER_ARM, FOREARM, HAND = (20, 21), (11, 12), (9, 10)
THIGH, SHIN, FOOT = (23, 24), (15, 16), (13, 14)
HEAD, LIPS_UP, LIPS_LOW, JAW, EYES, EARS = 17, 7, 8, 22, (2, 3), (4, 5)

# Bones, in the order the game expects (Person's joints). L is the character's left: +X here.
BONES = ["Hips", "Torso", "Head", "ShoulderL", "ElbowL", "HandL", "ShoulderR", "ElbowR", "HandR",
         "HipL", "KneeL", "FootL", "HipR", "KneeR", "FootR"]
# Colour slots: the game paints each with the character's own colours.
SKIN, LIP, MOUTH, BROW, JACKET, TRIM, PANTS, GLOVE, INK, TEETH = range(10)


def smoothstep(a, b, x):
    if a == b:
        return 0.0 if x < a else 1.0
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def evaluated_copy(ob, level):
    for m in ob.modifiers:
        if m.type == 'MULTIRES':
            m.levels = level
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    new = bpy.data.objects.new("work%d" % level, me)
    bpy.context.scene.collection.objects.link(new)
    return new


def face_sets(me):
    att = me.attributes.get(".sculpt_face_set")
    return [att.data[i].value for i in range(len(me.polygons))]


def bake(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    old = ob.data
    ob.modifiers.clear()
    ob.data = me
    bpy.data.meshes.remove(old)
    return ob


def smooth(ob, iterations, factor=0.5):
    m = ob.modifiers.new("s", 'SMOOTH')
    m.factor = factor
    m.iterations = iterations
    return bake(ob)


def inflate(ob, amount):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * (amount(v.co) if callable(amount) else amount)
    bm.to_mesh(ob.data)
    bm.free()
    return ob


def rim(ob, thickness):
    """Gives open edges (hem, cuffs, collar) a turned-in lip, so cloth has thickness where you see its edge."""
    m = ob.modifiers.new("t", 'SOLIDIFY')
    m.thickness = thickness
    m.offset = -1.0
    m.use_rim_only = True
    return bake(ob)


def decimate(ob, ratio):
    m = ob.modifiers.new("d", 'DECIMATE')
    m.ratio = ratio
    return bake(ob)


def set_slots(ob, slot):
    """Per-vertex colour slot, stored in a float attribute (slot may be a function of position)."""
    me = ob.data
    att = me.attributes.get("slot") or me.attributes.new("slot", 'FLOAT', 'POINT')
    for v in me.vertices:
        att.data[v.index].value = float(slot(v.co) if callable(slot) else slot)
    for p in me.polygons:
        p.use_smooth = True


# ---- the body, its regions and its skeleton -----------------------------------------------------
src = bpy.data.objects[BODY]
eyes_src = [bpy.data.objects[BODY + ".eye.L"], bpy.data.objects[BODY + ".eye.R"]]
def _centre(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return sum(pts, Vector()) / 8 - src.matrix_world.translation


EYE = [_centre(o) for o in eyes_src]                            # L (+x), R (-x)
EYE = [Vector((abs(EYE[0].x), EYE[0].y, EYE[0].z)), Vector((-abs(EYE[0].x), EYE[0].y, EYE[0].z))]
EYE_R = max(eyes_src[0].dimensions) * 0.5
src.location = (0, 0, 0)
fine = evaluated_copy(src, 2)
mid = evaluated_copy(src, 1)


def region_ring(me, sets_a, sets_b, side=0):
    """Centre of the vertices shared by two groups of face sets (optionally one side of the body)."""
    fs = face_sets(me)
    in_a, in_b = set(), set()
    for p in me.polygons:
        s = fs[p.index]
        if s in sets_a:
            in_a.update(p.vertices)
        if s in sets_b:
            in_b.update(p.vertices)
    ring = [me.vertices[i].co for i in in_a & in_b if side == 0 or me.vertices[i].co.x * side > 0]
    return sum(ring, Vector()) / max(len(ring), 1), ring


base = src.data
J = {}


def measure():
    """Joints and face landmarks, from the body as it is now."""
    global CHIN_Z, CROWN_Z, EYE_Z, HR, MOUTH_Z, MOUTH_HALF, MOUTH_Y, HEIGHT, NOSE, CHIN_Y, EAR, _slit
    HEIGHT = max(v.co.z for v in mid.data.vertices)
    for side, tag in ((1, "L"), (-1, "R")):
        J["Shoulder" + tag], _ = region_ring(base, {CHEST}, set(UPPER_ARM), side)
        J["Elbow" + tag], _ = region_ring(base, set(UPPER_ARM), set(FOREARM), side)
        J["Hand" + tag], _ = region_ring(base, set(FOREARM), set(HAND), side)
        J["Hip" + tag], _ = region_ring(base, {PELVIS}, set(THIGH), side)
        J["Knee" + tag], _ = region_ring(base, set(THIGH), set(SHIN), side)
        J["Foot" + tag], _ = region_ring(base, set(SHIN), set(FOOT), side)
        # The crease of the leg is below and outside the hip joint itself.
        J["Hip" + tag] = J["Hip" + tag] + Vector((-0.012 * side, 0, 0.035))
        # Likewise the ring round the top of the arm sits below and inside the shoulder joint.
        J["Shoulder" + tag] = J["Shoulder" + tag] + Vector((0.016 * side, 0, 0.05))
    # Both sides the same: the game mirrors its poses.
    for k in ("Shoulder", "Elbow", "Hand", "Hip", "Knee", "Foot"):
        l, r = J[k + "L"], J[k + "R"]
        m = Vector(((l.x - r.x) * 0.5, (l.y + r.y) * 0.5, (l.z + r.z) * 0.5))
        J[k + "L"], J[k + "R"] = m, Vector((-m.x, m.y, m.z))
    waist, _ = region_ring(base, {BELLY}, {PELVIS})
    EAR, _ = region_ring(base, set(EARS), {HEAD}, 1)
    J["Hips"] = Vector((0, waist.y, (J["HipL"].z + J["HipR"].z) * 0.5 + 0.04))
    J["Torso"] = Vector((0, waist.y, J["Hips"].z + 0.02))
    # The slit between the lips: where the skin of the face meets the lining of the mouth.
    mouth_c, mouth_ring = region_ring(fine.data, {JAW, HEAD}, {LIPS_UP, LIPS_LOW})
    fs = face_sets(base)
    CHIN_Z = min(base.vertices[i].co.z for p in base.polygons if fs[p.index] == JAW for i in p.vertices if abs(base.vertices[i].co.x) < 0.02)
    CROWN_Z = HEIGHT
    EYE_Z = EYE[0].z
    # The game's head is measured in "head radii": chin at -0.25, crown at 1.93.
    HR = (CROWN_Z - CHIN_Z) / 2.18
    J["Head"] = Vector((0, EAR.y + 0.012, CHIN_Z + 0.25 * HR))
    MOUTH_Z = mouth_c.z
    MOUTH_HALF = max(abs(c.x) for c in mouth_ring)
    MOUTH_Y = min(c.y for c in mouth_ring)
    front = [v.co for v in fine.data.vertices if abs(v.co.x) < 0.012 and v.co.y < EAR.y - 0.05]
    NOSE = min((c for c in front if MOUTH_Z + 0.008 < c.z < EYE_Z), key=lambda c: c.y).copy()
    NOSE.x = 0.0
    CHIN_Y = min(c.y for c in front if CHIN_Z < c.z < CHIN_Z + 0.03)
    bins = [[] for _ in range(13)]
    for c in mouth_ring:
        bins[min(12, max(0, int(round((c.x / MOUTH_HALF * 0.5 + 0.5) * 12))))].append(c.z)
    _slit = [sum(b) / len(b) if b else None for b in bins]
    for i in range(13):
        if _slit[i] is None:
            near = [j for j in range(13) if _slit[j] is not None]
            _slit[i] = _slit[min(near, key=lambda j: abs(j - i))]


def slit_z(x):
    """Height of the line between the lips at a sideways position."""
    f = min(12.0, max(0.0, (x / MOUTH_HALF * 0.5 + 0.5) * 12))
    i = min(11, int(f))
    return _slit[i] + (_slit[i + 1] - _slit[i]) * (f - i)


measure()


# ---- from a real body to a comic one ----------------------------------------------------------------
def make_warp():
    """Returns position -> position. Everything is measured on the realistic body, before it moves."""
    chin_z, eye_z, mouth_z, mouth_y, chin_y = CHIN_Z, EYE_Z, MOUTH_Z, MOUTH_Y, CHIN_Y
    nose, ear, eyes = NOSE.copy(), EAR.copy(), [e.copy() for e in EYE]
    neck = Vector((0, J["Head"].y, chin_z - 0.07))
    wrist = {1: J["HandL"].copy(), -1: J["HandR"].copy()}
    arm_dir = {s: (wrist[s] - J["Elbow" + ("L" if s > 0 else "R")]).normalized() for s in (1, -1)}
    hip_z, chest_z = J["Hips"].z, J["ShoulderL"].z - 0.1
    # The hands: which way the palm faces (the flat of the hand, on the side the relaxed fingers
    # curl toward) and where the knuckles are, so the fingers can be closed round a pole.
    palm, knuckle, hand_len = {}, {}, {}
    for s in (1, -1):
        pts = [v.co - wrist[s] for v in base.vertices if v.co.x * s > 0.25 and (v.co - wrist[s]).dot(arm_dir[s]) > 0.012 and (v.co - wrist[s]).length < 0.26]
        arr = numpy.array([[c.x, c.y, c.z] for c in pts])
        mean = arr.mean(axis=0)
        vals, vecs = numpy.linalg.eigh(numpy.cov((arr - mean).T))
        n = Vector(vecs[:, 0])                                   # least spread: across the flat of the hand
        n = (n - arm_dir[s] * n.dot(arm_dir[s])).normalized()
        along = [c.dot(arm_dir[s]) for c in pts]
        hand_len[s] = max(along)
        palm[s] = n if n.x * s < 0 else -n                       # hanging arms: palms face the thighs
        knuckle[s] = hand_len[s] * 0.5
    grip = {s: wrist[s] + arm_dir[s] * knuckle[s] * 0.82 + palm[s] * 0.024 for s in (1, -1)}

    def curl(p, s):
        """Closes the fingers: two bends, at the knuckles and at the middle joints."""
        a, n = arm_dir[s], palm[s]
        axis = a.cross(n).normalized()
        d = p - wrist[s]
        t = d.dot(a)
        for at, deg in ((knuckle[s] + hand_len[s] * 0.2, 80.0), (knuckle[s], 62.0)):
            w = smoothstep(at - 0.006, at + 0.012, t)
            if w <= 0.0:
                continue
            pivot = wrist[s] + a * at + n * 0.006
            p = pivot + Matrix.Rotation(math.radians(deg) * w, 3, axis) @ (p - pivot)
        return p
    chin_fwd, chin_down, chin_wide = P["chin"]

    def g2(dx, dz, rx, rz):
        return math.exp(-(dx / rx) ** 2 - (dz / rz) ** 2)

    def warp(co):
        p = co.copy()
        sgn = 1.0 if co.x >= 0 else -1.0
        ax = abs(co.x)
        if co.z > chin_z - 0.05:
            face = smoothstep(ear.y - 0.03, ear.y - 0.075, co.y)              # 1 on the front of the head
            side = smoothstep(ear.y + 0.03, ear.y - 0.03, co.y)               # 1 from the ears forward
            # Jaw: its angle, below the ear, and the line from there to the chin.
            w = math.exp(-((co.z - (chin_z + 0.032)) / 0.034) ** 2) * smoothstep(0.012, 0.045, ax) * side
            p.x += sgn * P["jaw"] * w
            # Chin.
            w = g2(co.x, co.z - (chin_z + 0.012), 0.026, 0.02) * face
            p.y -= chin_fwd * w
            p.z -= chin_down * w
            p.x += co.x * (chin_wide - 1.0) * w
            # Cheekbones out, the cheek under them in: the planes a comic artist draws.
            w = g2(ax - 0.05, co.z - (eye_z - 0.028), 0.024, 0.017) * side
            p.x += sgn * P["cheek"] * w
            p.y -= P["cheek"] * 0.6 * w * face
            w = g2(ax - 0.046, co.z - (mouth_z + 0.012), 0.02, 0.02) * face
            p.x -= sgn * P["hollow"] * w
            p.y += P["hollow"] * 0.5 * w
            # Brow ridge.
            w = (g2(ax - 0.028, co.z - (eye_z + 0.02), 0.03, 0.011) + 0.6 * g2(co.x, co.z - (eye_z + 0.016), 0.014, 0.012)) * face
            p.y -= P["brow"] * w
            p.z -= P["brow"] * 0.3 * w
            # Nose: bigger or smaller about its root in the face, wider or narrower.
            root = Vector((0, nose.y + 0.024, nose.z + 0.006))
            w = g2(co.x, co.z - (nose.z + 0.004), 0.021, 0.026) * smoothstep(nose.y + 0.03, nose.y + 0.012, co.y)
            d = co - root
            p += d * (P["nose"] - 1.0) * w
            p.x += co.x * (P["nose_w"] - 1.0) * w * smoothstep(nose.z + 0.012, nose.z - 0.004, co.z)
            # Eyes: the whole eye, lids and socket, grows about its centre.
            for e in eyes:
                d = co - e
                w = math.exp(-(d.length / 0.021) ** 2)
                p += d * (P["eyes"] - 1.0) * w
            # Lips.
            w = g2(co.x, co.z - mouth_z, 0.03, 0.012) * smoothstep(mouth_y + 0.02, mouth_y + 0.006, co.y)
            p.y -= P["lips"] * w
            p.z += (co.z - mouth_z) * P["lips"] * 60.0 * w
            # Neck thickness.
            w = smoothstep(chin_z - 0.005, chin_z - 0.03, co.z) * smoothstep(0.075, 0.05, ax)
            p.x += co.x * (P["neck"] - 1.0) * w
            p.y += (co.y - neck.y) * (P["neck"] - 1.0) * w
        # The head as a whole, about the base of the neck.
        w = smoothstep(chin_z - 0.07, chin_z - 0.005, co.z) * smoothstep(0.2, 0.14, ax)
        p = neck + (p - neck) * (1.0 + (P["head"] - 1.0) * w)
        # Hands: fingers closed, then the whole hand bigger about the wrist.
        if ax > 0.25 and (co - wrist[1 if co.x > 0 else -1]).length < 0.28:
            s = 1 if co.x > 0 else -1
            along = (co - wrist[s]).dot(arm_dir[s])
            if along > 0.0:
                p = curl(p, s)
            w = smoothstep(-0.02, 0.02, along)
            p = wrist[s] + (p - wrist[s]) * (1.0 + (P["hands"] - 1.0) * w)
        # Shoulders: the arms move out, the chest widens to meet them.
        arm = smoothstep(0.17, 0.21, ax)
        up = smoothstep(hip_z, chest_z, co.z)
        p.x += sgn * P["shoulders"] * (arm + (1.0 - arm) * min(ax / 0.19, 1.0) * up)
        # Legs.
        k = P["legs"]
        p.z = p.z * k if p.z < hip_z else p.z + hip_z * (k - 1.0)
        return p
    warp.grip, warp.palm = grip[1], palm[1]
    return warp


FS = P["head"]                      # how much bigger the face is than the one the numbers below were tuned on
ES = P["head"] * P["eyes"]          # and the eyes
_warp = make_warp()
for _me in (fine.data, mid.data, base):
    for v in _me.vertices:
        v.co = _warp(v.co)
GRIP = _warp(_warp.grip)            # where a pole sits in the left hand
PALM = _warp.palm                   # and which way that palm faces
EYE = [_warp(e) for e in EYE]
EYE_R *= P["eyes"] * P["head"]
measure()
print("HEIGHT %.3f HR %.4f chin %.3f eye %s mouth z %.3f half %.3f nose %s" % (HEIGHT, HR, CHIN_Z, tuple(round(c, 3) for c in EYE[0]), MOUTH_Z, MOUTH_HALF, tuple(round(c, 3) for c in NOSE)))
for k in BONES:
    print("JOINT %-10s %s" % (k, tuple(round(c, 3) for c in J[k])))


def head_rings():
    """The outline of the skull at a row of heights, for fitting helmets, hair and goggles in the game."""
    fs = face_sets(fine.data)
    keep = set()
    for p in fine.data.polygons:
        if fs[p.index] in (HEAD, JAW):
            keep.update(p.vertices)
    pts = [fine.data.vertices[i].co for i in keep]
    rings = []
    n = 16
    for i in range(n):
        y = -0.2 + (1.9 + 0.2) * i / (n - 1)                       # in head radii above the head joint
        z = J["Head"].z + y * HR
        band = [c for c in pts if abs(c.z - z) < HR * 0.09]
        if not band:
            continue
        off_nose = [c for c in band if abs(c.x) > 0.02] or band
        rings.append((y * HR, max(abs(c.x) for c in band), -(min(c.y for c in off_nose) - J["Head"].y), -(max(c.y for c in band) - J["Head"].y)))
    return rings


RINGS = head_rings()


def bone_of(s, c):
    side = "L" if c.x > 0 else "R"
    if s in UPPER_ARM: return "Shoulder" + side
    if s in FOREARM: return "Elbow" + side
    if s in THIGH: return "Hip" + side
    if s in SHIN: return "Knee" + side
    if s in FOOT: return "Foot" + side
    if s == PELVIS: return "Hips"
    if s in (CHEST, BELLY): return "Torso"
    if s in (HEAD, LIPS_UP, LIPS_LOW, JAW, *EYES, *EARS): return "Head" if c.z > CHIN_Z - 0.035 else "Torso"
    return "Hand" + side                      # hands and fingers


def weigh(ob, spread):
    """Vertex groups from the body's regions, then diffused over the surface so joints bend smoothly."""
    me = ob.data
    fs = face_sets(me)
    n = len(me.vertices)
    w = [[0.0] * len(BONES) for _ in range(n)]
    for p in me.polygons:
        b = BONES.index(bone_of(fs[p.index], p.center))
        for vi in p.vertices:
            w[vi][b] += 1.0
    nbr = [[] for _ in range(n)]
    for e in me.edges:
        a, b = e.vertices
        nbr[a].append(b); nbr[b].append(a)
    for _ in range(spread):
        new = []
        for i in range(n):
            acc = list(w[i])
            for j in nbr[i]:
                wj = w[j]
                for k in range(len(BONES)):
                    acc[k] += wj[k]
            new.append(acc)
        w = new
    groups = [ob.vertex_groups.new(name=b) for b in BONES]
    for i in range(n):
        tot = sum(w[i]) or 1.0
        for k, g in enumerate(groups):
            v = w[i][k] / tot
            if v > 0.02:
                g.add([i], v, 'REPLACE')
    # Markers for the face regions, used for the expressions later.
    marks = {"m_low": {LIPS_LOW}, "m_up": {LIPS_UP}, "m_jaw": {JAW}, "m_eyeL": {EYES[0], EYES[1]}}
    for name, sets in marks.items():
        g = ob.vertex_groups.new(name=name)
        ids = set()
        for p in me.polygons:
            if fs[p.index] in sets:
                ids.update(p.vertices)
        g.add(list(ids), 1.0, 'REPLACE')


weigh(mid, 7)
weigh(fine, 12)


def extract(src_ob, name, keep):
    me = src_ob.data.copy()
    sets = face_sets(me)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for g in src_ob.vertex_groups:
        ob.vertex_groups.new(name=g.name)
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    doomed = [f for f in bm.faces if not keep(sets[f.index], f.calc_center_median())]
    bmesh.ops.delete(bm, geom=doomed, context='FACES')
    bm.to_mesh(me)
    bm.free()
    return ob


def split_at(ob, z):
    """Cuts a clean edge loop at a height, so a colour change there is a crisp line."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, z), plane_no=(0, 0, 1))
    bm.to_mesh(ob.data)
    bm.free()


def build(detail):
    """detail 1 = close-up characters, 0.4 = the crowd."""
    parts = []
    head_sets = {HEAD, LIPS_UP, LIPS_LOW, JAW, *EYES, *EARS}
    neck_cut = CHIN_Z - 0.085

    # --- skin: head and neck ---
    skin = extract(fine, "skin", lambda s, c: s in head_sets and c.z > neck_cut)
    skin = decimate(skin, 0.125 * detail)
    def skin_slot(co):
        return SKIN
    set_slots(skin, skin_slot)
    parts.append(skin)

    # --- jacket ---
    hem = J["Hips"].z - 0.07
    collar_top = CHIN_Z - 0.012
    jacket_sets = {CHEST, BELLY, *UPPER_ARM, *FOREARM}
    jacket = extract(mid, "jacket", lambda s, c: s in jacket_sets or (s == PELVIS and c.z > hem) or (s == HEAD and neck_cut - 0.03 < c.z < collar_top))
    jacket = smooth(jacket, 34)                               # a down jacket hides the muscles under it
    def puff(co):
        arm = smoothstep(0.2, 0.3, abs(co.x))
        collar = smoothstep(CHIN_Z - 0.11, CHIN_Z - 0.07, co.z)
        return 0.05 - 0.012 * arm - 0.03 * collar
    inflate(jacket, puff)
    jacket = smooth(jacket, 10)
    def quilt(co):
        arm = smoothstep(0.21, 0.3, abs(co.x))
        along = co.z if arm < 0.5 else (co.z * 0.95 - abs(co.x) * 0.32)
        seam = abs(math.sin(math.pi * along / 0.092)) ** 0.3
        collar = smoothstep(CHIN_Z - 0.12, CHIN_Z - 0.09, co.z)
        return -0.012 * (1 - seam) * (1 - collar)
    inflate(jacket, quilt)
    jacket = rim(jacket, 0.014)
    jacket = decimate(jacket, 0.42 * detail)
    # A yoke in the second colour: collar, shoulders and the tops of the sleeves.
    yoke = J["ShoulderL"].z - 0.085
    split_at(jacket, yoke)
    split_at(jacket, yoke - 0.003)
    set_slots(jacket, lambda co: TRIM if co.z > yoke - 0.0005 else JACKET)
    zip_line(jacket, hem + 0.01, collar_top - 0.004)
    parts.append(jacket)

    # --- pants ---
    ankle = J["FootL"].z + 0.035
    pants_sets = {PELVIS, *THIGH, *SHIN}
    pants = extract(mid, "pants", lambda s, c: (s in pants_sets or s == BELLY and c.z < J["Hips"].z + 0.06) and c.z > ankle)
    pants = smooth(pants, 16)
    inflate(pants, lambda co: 0.026 + 0.03 * smoothstep(J["KneeL"].z - 0.1, ankle, co.z))
    pants = smooth(pants, 4)
    pants = rim(pants, 0.012)
    pants = decimate(pants, 0.32 * detail)
    set_slots(pants, PANTS)
    parts.append(pants)

    # --- gloves ---
    wrist = J["HandL"].z
    def is_hand(s, c):
        if s in FOREARM:
            return c.z < wrist + 0.045
        return abs(c.x) > 0.3 and c.z < wrist + 0.06 and s not in jacket_sets | pants_sets | head_sets | {*FOOT, PELVIS}
    gloves = extract(mid, "gloves", is_hand)
    gloves = smooth(gloves, 2)
    inflate(gloves, lambda co: 0.0065 + 0.034 * smoothstep(wrist + 0.012, wrist + 0.03, co.z))
    gloves = smooth(gloves, 2)
    gloves = rim(gloves, 0.008)
    gloves = decimate(gloves, 0.34 * detail)
    split_at(gloves, wrist + 0.028)
    split_at(gloves, wrist + 0.0255)
    set_slots(gloves, lambda co: TRIM if co.z > wrist + 0.0275 else GLOVE)
    parts.append(gloves)
    return parts


def zip_line(ob, z0, z1):
    """The zip: a dark ribbon down the front, lying on the jacket."""
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bvh = BVHTree.FromBMesh(bm)
    kd = KDTree(len(me.vertices))
    for v in me.vertices:
        kd.insert(v.co, v.index)
    kd.balance()
    dl = bm.verts.layers.deform.verify()
    bm.verts.ensure_lookup_table()
    originals = list(bm.verts)
    first = len(bm.verts)
    prev = None
    n = 26
    for i in range(n + 1):
        z = z0 + (z1 - z0) * i / n
        hit, nrm, _, _ = bvh.ray_cast(Vector((0, -1.0, z)), Vector((0, 1, 0)))
        if hit is None:
            prev = None
            continue
        srcv = originals[kd.find(hit)[1]]
        pair = []
        for sx in (-0.006, 0.006):
            v = bm.verts.new(hit + nrm * 0.003 + Vector((sx, 0, 0)))
            for gi, wgt in srcv[dl].items():
                v[dl][gi] = wgt
            pair.append(v)
        if prev is not None:
            bm.faces.new([prev[0], prev[1], pair[1], pair[0]])
        prev = pair
    count = len(bm.verts) - first
    bm.to_mesh(me)
    bm.free()
    slot = me.attributes["slot"]
    for i in range(count):
        slot.data[first + i].value = float(INK)
    for p in me.polygons:
        p.use_smooth = True


def face_keys(skin):
    """Expressions, as per-vertex offsets on the finished head: {name: [Vector per vertex]}."""
    me = skin.data
    n = len(me.vertices)
    grp = {g.name: g.index for g in skin.vertex_groups}
    def mark(v, name):
        for ge in v.groups:
            if ge.group == grp[name]:
                return ge.weight
        return 0.0
    keys = {k: [Vector((0, 0, 0)) for _ in range(n)] for k in ("Blink", "MouthOpen", "Smile", "BrowUp", "BrowDown")}
    hinge = Vector((0, J["Head"].y - 0.004 * FS, EYE_Z - 0.03 * FS))
    jaw_rot = Matrix.Rotation(math.radians(13.0), 4, 'X')     # chin down and back
    for v in me.vertices:
        co = v.co
        # Mouth: the lower lip, chin and jaw swing down about the hinge under the ear. Between the
        # corners of the mouth the lips part cleanly; out on the cheeks the skin stretches over a
        # few centimetres instead.
        ax = abs(co.x)
        rel = co.z - slit_z(co.x)                                                 # above (+) or below (-) the line of the lips
        wide = smoothstep(MOUTH_HALF * 0.8, MOUTH_HALF * 1.4, ax)
        band = (0.0012 + 0.04 * wide) * FS
        w = smoothstep(band * 0.3, -band, rel)
        # Between the corners the mesh itself says which lip a vertex is on (the lips touch, so height cannot).
        lower = 1.0 if (mark(v, "m_low") > 0.5 or (mark(v, "m_jaw") > 0.5 and mark(v, "m_up") < 0.5)) else 0.0
        w += (lower - w) * (1.0 - smoothstep(MOUTH_HALF * 0.85, MOUTH_HALF * 1.35, ax))
        w *= 1.0 - 0.8 * smoothstep(MOUTH_HALF * 0.7, MOUTH_HALF * 1.6, ax)       # the corners hardly move, the cheeks less
        w *= smoothstep(hinge.y + 0.01 * FS, hinge.y - 0.045 * FS, co.y)         # nothing behind the jaw joint
        w *= smoothstep(CHIN_Z - 0.06 * FS, CHIN_Z - 0.015 * FS, co.z)           # and the throat stays
        if w > 0.001:
            keys["MouthOpen"][v.index] = ((jaw_rot @ (co - hinge)) + hinge - co) * w
        # Smile: the corners of the mouth go up, out and back; the cheeks lift a little.
        for side in (1, -1):
            corner = Vector((side * MOUTH_HALF, MOUTH_Y + 0.012 * FS, slit_z(side * MOUTH_HALF)))
            d = (co - corner).length / FS
            g = math.exp(-(d / 0.02) ** 2)
            if g > 0.01:
                keys["Smile"][v.index] += Vector((side * 0.0045, 0.003, 0.0065)) * g * FS
        for side, eye in ((1, EYE[0]), (-1, EYE[1])):
            rel = (co - eye) / ES
            # Blink: the upper lid rolls down over the eyeball.
            if abs(rel.x) < 0.02 and rel.y < -0.004 and 0.0015 < rel.z < 0.02 and rel.length < 0.024:
                wl = 1.0 - smoothstep(0.0045, 0.014, rel.z)
                ang = math.radians(46.0) * wl * smoothstep(0.019, 0.012, abs(rel.x))
                rot = Matrix.Rotation(ang, 4, 'X')
                keys["Blink"][v.index] = ((rot @ rel) - rel) * ES
            # Brows.
            bx, bz = eye.x * 0.94, EYE_Z + 0.021 * ES
            if co.y < eye.y + 0.02 * FS:
                g = math.exp(-((co.x - bx) / (0.03 * FS)) ** 2 - ((co.z - bz) / (0.012 * ES)) ** 2)
                if g > 0.01:
                    keys["BrowUp"][v.index] += Vector((0, -0.001, 0.0065)) * g * FS
                    inner = math.exp(-((co.x - side * 0.014 * FS) / (0.014 * FS)) ** 2 - ((co.z - bz) / (0.012 * ES)) ** 2)
                    keys["BrowDown"][v.index] += Vector((-side * 0.0025 * inner, -0.0015 * inner, -0.0035 * g - 0.0025 * inner)) * FS
    return keys


def add_strokes(skin, keys):
    """Brows and upper lash lines, drawn as ribbons lying on the skin. They move with the expressions."""
    me = skin.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bvh = BVHTree.FromBMesh(bm)
    kd = KDTree(len(me.vertices))
    for v in me.vertices:
        kd.insert(v.co, v.index)
    kd.balance()
    new_pts = []                      # (position, source vertex for weights and keys)
    faces = []
    def ribbon(points, widths, lift):
        prev = None
        for i, (p, wdt) in enumerate(zip(points, widths)):
            hit, nrm, _, _ = bvh.ray_cast(Vector((p.x, -1.0, p.z)), Vector((0, 1, 0)))
            if hit is None:
                prev = None
                continue
            t = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)])
            t = Vector((t.x, 0, t.z)).normalized()
            up = nrm.cross(t).normalized() * (wdt * 0.5)
            a, b = hit + nrm * lift - up, hit + nrm * lift + up
            ia = len(new_pts); new_pts.append((a, kd.find(hit)[1])); new_pts.append((b, kd.find(hit)[1]))
            if prev is not None:
                faces.append((prev, prev + 1, ia + 1, ia))
            prev = ia
    thick = (0.0075 if SEX == "male" else 0.0045) * FS
    for side in (1, -1):
        # Brow: heavy by the nose, arching, thinning to a point.
        pts, wid = [], []
        for i in range(9):
            t = i / 8
            x = side * (0.011 + 0.045 * t) * FS
            z = EYE_Z + (0.0185 + 0.0055 * math.sin(math.pi * min(t * 1.25, 1.0)) - 0.004 * max(0.0, t - 0.7) / 0.3) * ES
            pts.append(Vector((x, 0, z))); wid.append(thick * (1.0 - 0.75 * t ** 1.5) + 0.001)
        ribbon(pts, wid, 0.0012)
        # Upper lash line, along the edge of the lid.
        eye = EYE[0] if side > 0 else EYE[1]
        pts, wid = [], []
        for i in range(9):
            t = i / 8
            x = eye.x + (t - 0.5) * 0.029 * ES
            z = eye.z + (0.0048 * math.sin(math.pi * t) + 0.0006) * ES
            pts.append(Vector((x, 0, z))); wid.append(((0.0022 if SEX == "male" else 0.003) * (0.5 + 0.5 * math.sin(math.pi * t)) + 0.0006) * ES)
        ribbon(pts, wid, 0.0006)
    first = len(bm.verts)
    made = [bm.verts.new(p) for p, _ in new_pts]
    bm.verts.ensure_lookup_table()
    for f in faces:
        try:
            bm.faces.new([made[i] for i in f])
        except ValueError:
            pass
    dl = bm.verts.layers.deform.verify()
    bm.verts.ensure_lookup_table()
    for v, (_, srcv) in zip(made, new_pts):
        for gi, wgt in bm.verts[srcv][dl].items():
            v[dl][gi] = wgt
    bm.to_mesh(me)
    bm.free()
    slot = me.attributes["slot"]
    for i, (_, srcv) in enumerate(new_pts):
        slot.data[first + i].value = float(BROW)
        for k in keys:
            keys[k].append(keys[k][srcv].copy())
    for p in me.polygons:
        p.use_smooth = True


def paint_face(skin):
    """Lips and the lining of the mouth get their own colour slots."""
    me = skin.data
    slot = me.attributes["slot"]
    grp = {g.name: g.index for g in skin.vertex_groups}
    up = (0.0085 if SEX == "male" else 0.0095) * FS
    low = (0.0105 if SEX == "male" else 0.012) * FS
    for v in me.vertices:
        lining = 0.0
        for ge in v.groups:
            if ge.group in (grp["m_low"], grp["m_up"]):
                lining = max(lining, ge.weight)
        co = v.co
        if lining > 0.5:
            slot.data[v.index].value = float(MOUTH)
        elif abs(co.x) < MOUTH_HALF and co.y < MOUTH_Y + 0.016 * FS:
            taper = math.sqrt(max(0.0, 1.0 - (co.x / MOUTH_HALF) ** 2))
            rel = co.z - slit_z(co.x)
            if -low * taper < rel < up * taper * (0.75 + 0.25 * abs(math.cos(co.x / MOUTH_HALF * math.pi))):
                slot.data[v.index].value = float(LIP)


def concavity(me):
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.normal_update()
    out = [0.0] * len(bm.verts)
    for v in bm.verts:
        if not v.link_edges:
            continue
        avg = Vector(); mean = 0.0
        for e in v.link_edges:
            o = e.other_vert(v)
            avg += o.co; mean += (o.co - v.co).length
        k = len(v.link_edges)
        out[v.index] = max(0.0, ((avg / k) - v.co).dot(v.normal)) / max(mean / k, 1e-6)
    bm.free()
    return out


def game(v):
    """Blender (x = character's left, -y = front, z up) -> game (x right, y up, z front)."""
    return (-v.x, v.z, -v.y)


def export(parts, keys, path):
    """One file: every part as a skinned sub-mesh, the skeleton and the face landmarks. See SkinModel.cs."""
    out = bytearray()
    out += b"SKM3"
    out += struct.pack("<f", HEIGHT)
    out += struct.pack("<i", len(BONES))
    for b in BONES:
        out += struct.pack("<3f", *game(J[b]))
    out += struct.pack("<f", HR)
    out += struct.pack("<3f", *game(EYE[0])) + struct.pack("<3f", *game(EYE[1])) + struct.pack("<f", EYE_R)
    out += struct.pack("<3f", *game(Vector((0, MOUTH_Y, MOUTH_Z)))) + struct.pack("<f", MOUTH_HALF)
    out += struct.pack("<3f", *game(NOSE)) + struct.pack("<3f", *game(EAR))
    out += struct.pack("<3f", *game(GRIP)) + struct.pack("<3f", *game(PALM))
    out += struct.pack("<i", len(RINGS))
    for r in RINGS:
        out += struct.pack("<4f", *r)
    out += struct.pack("<i", len(parts))
    for ob in parts:
        me = ob.data
        me.calc_loop_triangles()
        name = ob.name.split(".")[0].encode()
        out += struct.pack("<i", len(name)) + name
        n = len(me.vertices)
        assert n < 65536
        slot = me.attributes["slot"]
        crease = concavity(me)
        gname = {g.index: g.name for g in ob.vertex_groups}
        pts = [game(v.co) for v in me.vertices]
        lo = [min(p[i] for p in pts) for i in range(3)]
        size = [max(max(p[i] for p in pts) - lo[i], 1e-6) for i in range(3)]
        out += struct.pack("<i", n) + struct.pack("<3f", *lo) + struct.pack("<3f", *size)
        for p in pts:
            out += struct.pack("<3H", *(int(round((p[i] - lo[i]) / size[i] * 65535)) for i in range(3)))
        for v in me.vertices:
            out += struct.pack("<2B", int(round(slot.data[v.index].value)), int(min(1.0, crease[v.index] * 4.0) * 255))
        for v in me.vertices:
            ws = sorted(((ge.weight, BONES.index(gname[ge.group])) for ge in v.groups if gname[ge.group] in BONES), reverse=True)[:4]
            tot = sum(w for w, _ in ws) or 1.0
            ws += [(0.0, 0)] * (4 - len(ws))
            q = [int(round(w / tot * 255)) for w, _ in ws]
            q[0] += 255 - sum(q)
            for (w, b), qw in zip(ws, q):
                out += struct.pack("<2B", b, qw)
        tris = me.loop_triangles
        out += struct.pack("<i", len(tris) * 3)
        for t in tris:
            # Mirrored coordinates: the winding flips with them.
            out += struct.pack("<3H", t.vertices[0], t.vertices[2], t.vertices[1])
        k = keys if ob.name.startswith("skin") else {}
        out += struct.pack("<i", len(k))
        for kn, deltas in k.items():
            bn = kn.encode()
            moved = [(i, d) for i, d in enumerate(deltas) if d.length > 0.00005]
            scale = max((max(abs(c) for c in d) for _, d in moved), default=1e-6)
            out += struct.pack("<i", len(bn)) + bn + struct.pack("<f", scale) + struct.pack("<i", len(moved))
            for i, d in moved:
                gd = (-d.x, d.z, -d.y)
                out += struct.pack("<H3h", i, *(int(round(c / scale * 32767)) for c in gd))
    with open(path, "wb") as f:
        f.write(out)
    tris = sum(len(o.data.loop_triangles) for o in parts)
    print("EXPORT %s  %d bytes  %d tris  %s" % (path, len(out), tris, {o.name: len(o.data.loop_triangles) for o in parts}))


parts = build(P["detail"])
skin = parts[0]
paint_face(skin)
keys = face_keys(skin)
add_strokes(skin, keys)
export(parts, keys, "%s/%s.bytes" % (OUT, NAME))

# ---- previews of the close-up model -----------------------------------------------------------------
PALETTE = {SKIN: (0.93, 0.74, 0.62), LIP: (0.72, 0.36, 0.34), MOUTH: (0.25, 0.08, 0.1), BROW: (0.16, 0.1, 0.08), JACKET: (0.95, 0.5, 0.23),
           TRIM: (0.13, 0.19, 0.36), PANTS: (0.13, 0.19, 0.36), GLOVE: (0.14, 0.12, 0.18), INK: (0.1, 0.08, 0.19), TEETH: (0.95, 0.95, 0.9)}
for ob in parts:
    me = ob.data
    att = me.color_attributes.new("Col", 'FLOAT_COLOR', 'POINT')
    slot = me.attributes["slot"]
    crease = concavity(me)
    for v in me.vertices:
        c = PALETTE[int(round(slot.data[v.index].value))]
        k = 1.0 - 0.55 * min(1.0, crease[v.index] * 4.0)
        att.data[v.index].color = (c[0] * k, c[1] * k, c[2] * k, 1.0)
    me.color_attributes.active_color = att
shown = list(parts)
for eye in EYE:
    em = bpy.data.meshes.new("eye")
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=EYE_R, matrix=Matrix.Translation(eye) @ Matrix.Rotation(math.radians(90), 4, 'X'))
    bm.to_mesh(em); bm.free()
    att = em.color_attributes.new("Col", 'FLOAT_COLOR', 'POINT')
    for v in em.vertices:
        d = (v.co - eye)
        front = -d.y / EYE_R
        att.data[v.index].color = (0.05, 0.04, 0.06, 1) if front > 0.94 else (0.25, 0.45, 0.3, 1) if front > 0.78 else (0.95, 0.95, 0.93, 1)
    eo = bpy.data.objects.new("eye", em)
    bpy.context.scene.collection.objects.link(eo)
    shown.append(eo)
for o in bpy.data.objects:
    o.hide_render = o not in shown
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
sh = scene.display.shading
sh.light = 'STUDIO'; sh.color_type = 'VERTEX'
sh.show_cavity = True; sh.cavity_type = 'BOTH'; sh.curvature_valley_factor = 1.6; sh.curvature_ridge_factor = 0.4
sh.show_object_outline = True; sh.object_outline_color = (0.06, 0.04, 0.12)
cam_data = bpy.data.cameras.new("c"); cam = bpy.data.objects.new("c", cam_data)
scene.collection.objects.link(cam); scene.camera = cam
cam_data.type = 'ORTHO'


def shot(file, offset, scale, target, w=700, h=900):
    scene.render.resolution_x = w; scene.render.resolution_y = h
    cam.location = target + offset
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam_data.ortho_scale = scale
    scene.render.filepath = PREVIEW + "/" + file
    bpy.ops.render.render(write_still=True)


shot("hero_%s_front.png" % NAME, Vector((0.8, -5, 0.4)), HEIGHT * 1.1, Vector((0, 0, HEIGHT * 0.5)))
face = Vector((0, -0.05, EYE_Z - 0.02))
shot("hero_%s_face.png" % NAME, Vector((0.9, -4, 0.2)), 0.3, face, 800, 800)
shot("hero_%s_hand.png" % NAME, Vector((2.5, -3.5, 0.6)), 0.34, J["HandL"] + Vector((0.02, 0, -0.08)), 800, 800)
shot("hero_%s_hand2.png" % NAME, Vector((-4, -0.5, 0.2)), 0.34, J["HandL"] + Vector((0.02, 0, -0.08)), 800, 800)
shot("hero_%s_side.png" % NAME, Vector((3.2, -2.6, 0.3)), 0.3, face, 800, 800)
# The expressions, applied one after another to the same view.
skin = parts[0]
rest = [v.co.copy() for v in skin.data.vertices]
for combo, file in ((("Blink", "MouthOpen"), "talk"), (("Smile", "BrowUp"), "smile")):
    for i, v in enumerate(skin.data.vertices):
        v.co = rest[i] + sum((keys[k][i] for k in combo), Vector())
    shot("hero_%s_face_%s.png" % (NAME, file), Vector((0.9, -4, 0.2)), 0.3, face, 800, 800)
