"""Builds a skier from nothing but numbers: no base mesh, no template, no scan.

Every surface is lofted from cross-sections the script draws itself, the way a modeller blocks
out a character: a skull and face shaped by a handful of measurements (jaw, cheekbones, brow,
chin, nose, eyes), a neck, a jacket torso, sleeves, mitten fists, a pelvis and legs. The look is
The Wolf Among Us: adult comic proportions (about 7.5 heads), broad simple planes, hard angular
faces, and above all the ink: graphic brows, heavy upper lash lines, a stroke down the shadow side
of the nose, cheekbone and jaw hatching, and long tapered fold strokes on the clothes. Those are
drawn as thin ribbons of ink lying on the surfaces, so they are crisp at any distance and move
with the body.

Run:
  blender --background --factory-startup --python tools/blender/build_own.py -- <profile> <export dir> <preview dir>

Profiles: erik, jonte, maja, linn (close-up characters) and male, female (the crowd).
Output: <export dir>/<profile>.bytes in the format Assets/_Game/Scripts/Game/SkinModel.cs reads.
"""
import math
import struct
import sys
import bmesh
import bpy
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
NAME, OUT, PREVIEW = argv[0], argv[1], argv[2]

BONES = ["Hips", "Torso", "Head", "ShoulderL", "ElbowL", "HandL", "ShoulderR", "ElbowR", "HandR",
         "HipL", "KneeL", "FootL", "HipR", "KneeR", "FootR"]
SKIN, LIP, MOUTH, BROW, JACKET, TRIM, PANTS, GLOVE, INK, TEETH = range(10)

# ---- the characters ------------------------------------------------------------------------------
# Lengths in metres. Face numbers are for a head 0.235 m from chin to crown and scale with it.
#   height, head (chin to crown), shoulders (half width at the joints), chest/waist/hips (half width,
#   half depth), belly (forward), neck (radius), limbs (thickness factor).
#   Face: skull (half width), cheek (half width at the cheekbones), jaw (half width at the jaw
#   corner), chin (half width, forward), brow (ridge forward), bone (cheekbones out), hollow (cheeks
#   in), face (length factor below the eyes), nose (length, half width, projection, tip up, hook),
#   eye (radius, half gap, tilt of the outer corner up), mouth (half width), lips (fullness),
#   ears (size), lids (how open: 0 sleepy .. 1 wide), lash (weight of the upper lash line).
BASE = dict(sex="m", height=1.8, head=0.235, shoulders=0.19, chest=(0.175, 0.12), waist=(0.15, 0.105), hips=(0.165, 0.11),
            belly=0.0, neck=0.056, limbs=1.0, style="puffer", detail=1.0,
            skull=0.074, cheek=0.071, jaw=0.06, chin=(0.022, 0.006), brow=0.008, bone=0.004, hollow=0.003, face=1.0,
            nose=(0.052, 0.0175, 0.022, 0.0, 0.0), eye=0.0118, eye_gap=0.0325, tilt=0.0, mouth=0.025, lips=0.004,
            ears=1.0, lids=0.6, lash=1.0, age=0.0)
PROFILES = {
    # The leading man: tall, a V of a torso, a long hard face, square jaw and a cleft chin, heavy brow,
    # hooded eyes, a long straight nose with a bump. The old-school ski suit.
    "erik": dict(height=1.88, head=0.24, shoulders=0.205, chest=(0.185, 0.122), waist=(0.142, 0.1), hips=(0.155, 0.105), neck=0.058,
                 style="retro", skull=0.073, cheek=0.07, jaw=0.066, chin=(0.03, 0.011), brow=0.012, bone=0.006, hollow=0.006, face=1.1,
                 nose=(0.058, 0.017, 0.024, -0.002, 0.003), eye=0.0115, eye_gap=0.0325, lids=0.45, mouth=0.026, lips=0.003, age=0.4),
    # The snowboarder: big and soft, round fleshy face, small eyes, a wide flat nose turned up, big ears,
    # thick neck, a belly under the baggy jacket.
    "jonte": dict(height=1.82, head=0.245, shoulders=0.2, chest=(0.19, 0.135), waist=(0.18, 0.14), hips=(0.175, 0.125), belly=0.035,
                  neck=0.068, limbs=1.12, style="baggy", skull=0.078, cheek=0.081, jaw=0.072, chin=(0.026, 0.0), brow=0.009, bone=0.0,
                  hollow=-0.006, face=0.94, nose=(0.05, 0.024, 0.02, 0.004, 0.0), eye=0.0108, eye_gap=0.034, lids=0.5, mouth=0.027,
                  lips=0.005, ears=1.25, age=0.2),
    # Small and quick: a heart-shaped face, big wide-set eyes, a little upturned nose, full cheeks
    # and lips. The one-piece suit.
    "maja": dict(sex="f", height=1.64, head=0.228, shoulders=0.165, chest=(0.15, 0.105), waist=(0.125, 0.09), hips=(0.162, 0.11), neck=0.046,
                 limbs=0.86, style="onepiece", skull=0.072, cheek=0.07, jaw=0.052, chin=(0.016, 0.002), brow=0.004, bone=0.003, hollow=-0.004,
                 face=0.9, nose=(0.043, 0.014, 0.018, 0.005, 0.0), eye=0.0132, eye_gap=0.035, lids=0.85, mouth=0.024, lips=0.0065, lash=1.25),
    # Tall and sharp: high cheekbones over hollow cheeks, a pointed chin, long nose, almond eyes tilted
    # up, a long neck. The sleek futuristic shell.
    "linn": dict(sex="f", height=1.76, head=0.232, shoulders=0.172, chest=(0.152, 0.104), waist=(0.118, 0.086), hips=(0.155, 0.105), neck=0.045,
                 limbs=0.84, style="sleek", skull=0.071, cheek=0.071, jaw=0.054, chin=(0.013, 0.008), brow=0.006, bone=0.009, hollow=0.008,
                 face=1.06, nose=(0.054, 0.0145, 0.023, 0.001, 0.0015), eye=0.0122, eye_gap=0.0335, tilt=0.004, lids=0.55, mouth=0.023,
                 lips=0.0055, lash=1.35),
    "male": dict(detail=0.55, age=0.2),
    "female": dict(sex="f", height=1.68, head=0.228, shoulders=0.168, chest=(0.152, 0.106), waist=(0.125, 0.09), hips=(0.16, 0.11), neck=0.046,
                   limbs=0.86, skull=0.072, cheek=0.069, jaw=0.053, chin=(0.016, 0.004), brow=0.004, nose=(0.046, 0.0145, 0.019, 0.003, 0.0),
                   eye=0.0126, lids=0.7, lips=0.006, lash=1.2, detail=0.55),
}
P = dict(BASE); P.update(PROFILES[NAME])
STYLE, FEMALE, DETAIL = P["style"], P["sex"] == "f", P["detail"]
H = P["height"]
L = P["head"]                     # chin to crown
FS = L / 0.235                    # face numbers scale with the head
S = H / 1.8                       # body numbers scale with the height


def sm(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a))) if b != a else (1.0 if x >= b else 0.0)
    return t * t * (3.0 - 2.0 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def gauss(d, r):
    return math.exp(-(d / r) ** 2)


# ---- the skeleton (Blender: x = the character's left, -y = front, z = up) -------------------------
CROWN = H
CHIN_Z = H - L
EYE_Z = H - 0.49 * L
BROW_Z = EYE_Z + 0.042 * FS
NOSE_Z = EYE_Z - 0.06 * FS * P["face"]           # the bottom of the nose
MOUTH_Z = NOSE_Z - 0.026 * FS * P["face"]
J = {}
J["Head"] = Vector((0, 0.012 * FS, H - 0.83 * L))
J["Torso"] = Vector((0, 0.01, 1.12 * S))
J["Hips"] = Vector((0, 0.0, 0.97 * S))
SH_Z = CHIN_Z - 0.105 * S
for side, tag in ((1, "L"), (-1, "R")):
    sh = Vector((side * P["shoulders"] * S / 1.0, 0.012, SH_Z))
    J["Shoulder" + tag] = sh
    out = math.radians(17.0)
    up_len, fore_len = 0.305 * S, 0.262 * S
    d1 = Vector((side * math.sin(out), -0.04, -math.cos(out))).normalized()
    J["Elbow" + tag] = sh + d1 * up_len
    d2 = Vector((side * math.sin(out * 0.75), -0.12, -math.cos(out * 0.75))).normalized()
    J["Hand" + tag] = J["Elbow" + tag] + d2 * fore_len
    hip = Vector((side * 0.092 * S, 0.0, 0.915 * S))
    J["Hip" + tag] = hip
    knee = Vector((side * 0.1 * S, -0.012, 0.5 * S))
    J["Knee" + tag] = knee
    J["Foot" + tag] = Vector((side * 0.105 * S, 0.02, 0.095 * S))
HR = 0.43 * L                      # the head radius the game fits helmets and hair with

# ---- a mesh being built ---------------------------------------------------------------------------


class Part:
    """Vertices with a colour slot and bone weights, and faces; becomes one sub-mesh in the game."""

    def __init__(self, name):
        self.name = name
        self.co, self.slot, self.w, self.faces = [], [], [], []
        self.lofts = []

    def add(self, co, slot, w):
        self.co.append(Vector(co)); self.slot.append(slot); self.w.append(dict(w))
        return len(self.co) - 1

    def face(self, *ids):
        self.faces.append(tuple(ids))


def blend(a, b, t):
    """Bone weights: a little of a and b, t of the way to b."""
    w = {}
    for k, v in a.items():
        w[k] = w.get(k, 0.0) + v * (1.0 - t)
    for k, v in b.items():
        w[k] = w.get(k, 0.0) + v * t
    return {k: v for k, v in w.items() if v > 0.001}


class Loft:
    """A tube of rings (each the same number of points), kept so strokes can be laid on it later."""

    def __init__(self, part, rings, slots, weights, cap0=False, cap1=False):
        self.part = part
        first = len(part.faces)
        self.rings = rings
        self.n = len(rings[0])
        self.ids = []
        for i, ring in enumerate(rings):
            row = []
            for j, co in enumerate(ring):
                row.append(part.add(co, slots(i, j), weights(i, j)))
            self.ids.append(row)
        n = self.n
        for i in range(len(rings) - 1):
            for j in range(n):
                a, b = self.ids[i][j], self.ids[i][(j + 1) % n]
                c, d = self.ids[i + 1][(j + 1) % n], self.ids[i + 1][j]
                part.face(a, b, c, d)
        for cap, i in ((cap0, 0), (cap1, len(rings) - 1)):
            if not cap:
                continue
            centre = sum(rings[i], Vector()) / n
            c = part.add(centre, slots(i, 0), weights(i, 0))
            for j in range(n):
                if i == 0:
                    part.face(self.ids[i][(j + 1) % n], self.ids[i][j], c)
                else:
                    part.face(self.ids[i][j], self.ids[i][(j + 1) % n], c)
        # Faces must look outward: check one in the middle and turn them all if not.
        mid = len(rings) // 2 - (1 if len(rings) > 2 else 0)
        a, b, d = (part.co[self.ids[mid][0]], part.co[self.ids[mid][1]], part.co[self.ids[mid + 1][0]])
        centre = sum(rings[mid], Vector()) / n
        if (b - a).cross(d - a).dot(a - centre) < 0:
            for k in range(first, len(part.faces)):
                part.faces[k] = tuple(reversed(part.faces[k]))
        part.lofts.append(self)

    def at(self, u, v):
        """A point on the surface: u along the rings (0 .. rings-1), v around them (0 .. n)."""
        i = max(0, min(len(self.rings) - 2, int(math.floor(u))))
        fu = min(1.0, max(0.0, u - i))
        j = int(math.floor(v)) % self.n
        fv = v - math.floor(v)
        j2 = (j + 1) % self.n
        a = self.rings[i][j].lerp(self.rings[i][j2], fv)
        b = self.rings[i + 1][j].lerp(self.rings[i + 1][j2], fv)
        p = a.lerp(b, fu)
        # The normal from the neighbours.
        du = self.rings[min(i + 1, len(self.rings) - 1)][j] - self.rings[i][j]
        dv = self.rings[i][j2] - self.rings[i][j]
        nrm = dv.cross(du)
        if nrm.length < 1e-9:
            nrm = Vector((0, 0, 1))
        return p, nrm.normalized()

    def weight_at(self, u, v):
        i = max(0, min(len(self.rings) - 1, int(round(u))))
        j = int(round(v)) % self.n
        return self.part.w[self.ids[i][j]]


def frame(direction):
    """Two axes across a direction (for drawing rings round a limb)."""
    d = direction.normalized()
    ref = Vector((0, -1, 0)) if abs(d.y) < 0.9 else Vector((1, 0, 0))
    x = ref.cross(d).normalized()
    y = d.cross(x).normalized()
    return x, y


def ring(centre, xaxis, yaxis, a, b, n, power=2.0, front=0.0, phase=0.0):
    """A superellipse round centre: half widths a (along x) and b (along y); power > 2 is boxier.
    front pushes the -y side out (a chest, a belly)."""
    pts = []
    for j in range(n):
        t = 2.0 * math.pi * j / n + phase
        c, s = math.cos(t), math.sin(t)
        x = math.copysign(abs(c) ** (2.0 / power), c) * a
        y = math.copysign(abs(s) ** (2.0 / power), s) * b
        if y > 0.0:
            y *= 1.0 + front
        pts.append(centre + xaxis * x + yaxis * y)
    return pts


def ribbon(part, pts, normals, widths, weights, slot=INK, lift=0.0012):
    """A flat stroke of ink lying on a surface: points, the surface normal at each, half widths."""
    ids = []
    for k, (p, nrm, w) in enumerate(zip(pts, normals, widths)):
        t = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)])
        if t.length < 1e-9:
            t = Vector((1, 0, 0))
        side = nrm.cross(t).normalized() * w
        base = p + nrm * lift * FS
        ids.append((part.add(base + side, slot, weights[k]), part.add(base - side, slot, weights[k])))
    for k in range(len(ids) - 1):
        a, b = ids[k]
        c, d = ids[k + 1]
        part.face(a, c, d, b)
    return [i for pair in ids for i in pair]


def taper(n, w, start=0.25, end=0.0, peak=0.35):
    """Half widths along a brush stroke: thin in, full at peak, thin out."""
    out = []
    for k in range(n):
        t = k / max(1, n - 1)
        if t < peak:
            f = lerp(start, 1.0, sm(0.0, peak, t))
        else:
            f = lerp(1.0, end, sm(peak, 1.0, t))
        out.append(w * max(f, 0.05))
    return out


def stroke_on(loft, path, w, start=0.25, end=0.0, peak=0.35, slot=INK, lift=0.0016, steps=10):
    """A stroke along a path of (u, v) points on a lofted surface."""
    pts, nrms, ws = [], [], []
    seg = []
    for a, b in zip(path[:-1], path[1:]):
        for k in range(steps):
            t = k / steps
            seg.append((lerp(a[0], b[0], t), lerp(a[1], b[1], t)))
    seg.append(path[-1])
    for u, v in seg:
        p, nrm = loft.at(u, v)
        pts.append(p); nrms.append(nrm); ws.append(loft.weight_at(u, v))
    ribbon(loft.part, pts, nrms, taper(len(pts), w, start, end, peak), ws, slot, lift)


# ---- the head: one surface, shaped by measurements ------------------------------------------------
# Rings of points round the head, from inside the neck up to the crown. Each ring is a superellipse
# (half width, depth to the front, depth to the back, squareness), and on top of that the face is
# pushed and pulled: brow ridge, eye sockets, cheekbones and hollows, the nose, lips and chin.
YC = 0.012 * FS                    # the axis of the skull, a little behind the joint line
SQ = 0.35 if P["jaw"] > 0.063 * FS else 0.0     # a square jaw is squarer


def head_table():
    sk, ch, jw, (cw, cf) = P["skull"] * FS, P["cheek"] * FS, P["jaw"] * FS, P["chin"]
    cw *= FS; cf *= FS
    nk = P["neck"] * S
    rows = [
        # h (0 chin .. 1 crown), half width, front, back, power
        (-0.42, nk, nk * 0.98, nk * 1.02, 2.0),
        (-0.18, nk * 1.04, nk * 1.0, nk * 1.05, 2.0),
        (-0.07, jw * 0.86, 0.058 * FS, 0.064 * FS, 2.2),
        (0.00, cw + 0.017 * FS, 0.077 * FS + cf, 0.066 * FS, 2.5),
        (0.07, lerp(cw + 0.017 * FS, jw, 0.55), 0.087 * FS + cf * 0.5, 0.07 * FS, 2.6 + SQ),
        (0.16, jw, 0.091 * FS, 0.076 * FS, 2.65 + SQ),
        (0.26, lerp(jw, ch, 0.6), 0.093 * FS, 0.088 * FS, 2.6),
        (0.36, ch, 0.093 * FS, 0.098 * FS, 2.5),
        (0.46, ch * 0.995, 0.092 * FS, 0.105 * FS, 2.4),
        (0.56, sk, 0.091 * FS, 0.109 * FS, 2.3),
        (0.68, sk, 0.09 * FS, 0.111 * FS, 2.2),
        (0.80, sk * 0.97, 0.083 * FS, 0.108 * FS, 2.1),
        (0.90, sk * 0.83, 0.066 * FS, 0.094 * FS, 2.0),
        (0.96, sk * 0.6, 0.044 * FS, 0.067 * FS, 2.0),
        (0.995, sk * 0.18, 0.014 * FS, 0.02 * FS, 2.0),
    ]
    return rows


TABLE = head_table()


def table_at(h):
    if h <= TABLE[0][0]:
        return TABLE[0][1:]
    for a, b in zip(TABLE[:-1], TABLE[1:]):
        if h <= b[0]:
            t = (h - a[0]) / (b[0] - a[0])
            t = t * t * (3 - 2 * t) * 0.35 + t * 0.65          # a little easing, mostly straight: planes
            return tuple(lerp(x, y, t) for x, y in zip(a[1:], b[1:]))
    return TABLE[-1][1:]


def spow(x, p):
    return math.copysign(abs(x) ** (2.0 / p), x)


def head_base(theta, z):
    """The skull before the face is pushed about: theta 0 = front, + toward the character's left."""
    h = (z - CHIN_Z) / L
    w, f, b, p = table_at(h)
    s, c = math.sin(theta), math.cos(theta)
    x = spow(s, p) * w
    cy = spow(c, p)
    y = -cy * (f if cy > 0 else b)
    yc = lerp(0.01 * S, YC, sm(-0.3, -0.05, h))
    return Vector((x, yc + y, z))


def front_point(x, z):
    """The point of the bare skull at a given height and sideways offset, on the front."""
    lo, hi = 0.0, math.pi * 0.5
    for _ in range(40):
        mid = (lo + hi) * 0.5
        if head_base(mid, z).x < abs(x):
            lo = mid
        else:
            hi = mid
    t = math.copysign((lo + hi) * 0.5, x)
    return head_base(t, z), t


# Eyes: where the balls sit (behind the bare surface, so the sockets can be cut round them).
ER = P["eye"] * FS
EYE = []
for side in (1, -1):
    p0, _ = front_point(side * P["eye_gap"] * FS, EYE_Z)
    EYE.append(Vector((side * P["eye_gap"] * FS, p0.y + 0.5 * ER, EYE_Z)))
MOUTH_HALF = P["mouth"] * FS
NZ, NB = P["nose"][0] * FS, NOSE_Z                 # nose length, the bottom of it
NW, NPROJ, NUP, NHOOK = P["nose"][1] * FS, P["nose"][2] * FS, P["nose"][3] * FS, P["nose"][4] * FS
ROOT_Z = NB + NZ                                   # where the bridge starts, between the eyes
TIP_Z = NB + 0.011 * FS + NUP


def nose_out(x, z):
    """How far the nose stands out of the face at a point."""
    if z < NB - 0.0006 or z > ROOT_Z + 0.004 * FS:
        return 0.0
    # Along the bridge: from a low root to the tip, then round under to the base.
    if z >= TIP_Z:
        t = (z - TIP_Z) / max(ROOT_Z - TIP_Z, 1e-6)
        prof = lerp(NPROJ, 0.004 * FS, t ** 0.85) + NHOOK * gauss(t - 0.45, 0.2)
        width = lerp(NW * 0.62, 0.0075 * FS, t ** 0.7)
    else:
        t = (TIP_Z - z) / max(TIP_Z - NB, 1e-6)          # 0 at the tip, 1 at the base
        prof = NPROJ * math.sqrt(max(0.0, 1.0 - t * t * 0.92))
        width = lerp(NW * 0.62, NW * 0.5, t)
    k = max(0.0, 1.0 - (abs(x) / width) ** 2)
    d = prof * k ** 0.6
    # The wings either side of the tip.
    for side in (1, -1):
        g = gauss(x - side * NW * 0.78, NW * 0.36) * gauss(z - (NB + 0.0055 * FS), 0.0055 * FS)
        d = max(d, NPROJ * 0.36 * g)
    return d


def face_push(theta, x, z):
    """Everything that makes a face: metres outward from the bare skull."""
    front = max(0.0, math.cos(theta)) ** 1.5
    if front <= 0.0:
        return 0.0
    d = 0.0
    ax = abs(x)
    # Brow ridge: a hard shelf over the eyes, a dip in the middle.
    band = 1.0 - sm(0.052 * FS, 0.066 * FS, ax)
    d += P["brow"] * FS * gauss(z - (BROW_Z - 0.004 * FS), 0.0075 * FS) * band * (1.0 - 0.35 * gauss(x, 0.01 * FS))
    # Forehead: slightly flat; temples in.
    d -= 0.003 * FS * gauss(ax - 0.066 * FS, 0.01 * FS) * gauss(z - (BROW_Z + 0.012 * FS), 0.016 * FS)
    # Eye sockets, deep enough for the balls to sit in.
    for e in EYE:
        g = gauss(x - e.x, 1.55 * ER) * gauss(z - e.z - 0.002 * FS, 1.2 * ER)
        d -= 0.95 * ER * g
    # Cheekbones out, the cheeks under them in (or full and round).
    for side in (1, -1):
        d += P["bone"] * FS * gauss(x - side * 0.047 * FS, 0.019 * FS) * gauss(z - (EYE_Z - 0.026 * FS), 0.011 * FS)
        d -= P["hollow"] * FS * gauss(x - side * 0.05 * FS, 0.017 * FS) * gauss(z - (MOUTH_Z + 0.017 * FS), 0.019 * FS)
    # The muzzle round the mouth.
    d += 0.004 * FS * gauss(x, 0.03 * FS) * gauss(z - (MOUTH_Z + 0.004 * FS), 0.02 * FS)
    # Lips: upper and lower, a groove between.
    lip = 1.0 - sm(MOUTH_HALF * 0.75, MOUTH_HALF * 1.15, ax)
    d += P["lips"] * FS * lip * gauss(z - (MOUTH_Z + 0.0055 * FS), 0.0045 * FS)
    d += P["lips"] * FS * 1.15 * lip * gauss(z - (MOUTH_Z - 0.0075 * FS), 0.0055 * FS)
    d -= 0.0015 * FS * lip * gauss(z - MOUTH_Z, 0.0016 * FS)
    # The chin, forward; Erik's with a cleft.
    cw, cf = P["chin"]
    d += cf * FS * 0.7 * gauss(x, (cw + 0.01) * FS) * gauss(z - (CHIN_Z + 0.016 * FS), 0.013 * FS)
    if NAME == "erik":
        d -= 0.0018 * FS * gauss(x, 0.0035 * FS) * gauss(z - (CHIN_Z + 0.014 * FS), 0.008 * FS)
    d += nose_out(x, z)
    return d * front


def head_point(theta, z):
    p = head_base(theta, z)
    d = face_push(theta, p.x, z)
    if d != 0.0:
        out = Vector((p.x, p.y - lerp(0.01 * S, YC, 1.0), 0.0))
        if out.length < 1e-6:
            out = Vector((0, -1, 0))
        out.normalize()
        # Near the front the push is straight forward (a nose sticks out, it does not swell).
        fwd = max(0.0, math.cos(theta)) ** 3
        out = (out * (1.0 - fwd) + Vector((0, -1, 0)) * fwd).normalized()
        p = p + out * d
    return p


def head_point_xz(x, z, lift=0.0):
    """The finished face at a sideways offset and height (for laying strokes on it)."""
    _, t = front_point(x, z)
    p = head_point(t, z)
    a = head_point(t + 0.01, z)
    b = head_point(t, z + 0.001)
    n = (a - p).cross(b - p)
    outward = Vector((p.x, p.y - YC, 0.0))
    if n.dot(outward) < 0:
        n = -n
    n.normalize()
    return p + n * lift, n


# ---- building the head mesh ---------------------------------------------------------------------
HEADW = {"Head": 1.0}
KEYS = {k: {} for k in ("Blink", "MouthOpen", "Smile", "BrowUp", "BrowDown")}
HINGE = Vector((0, J["Head"].y + 0.006 * FS, EYE_Z - 0.036 * FS))      # the jaw turns about a point under the ear
JAW_ROT = Matrix.Rotation(math.radians(12.0), 4, 'X')


def head_heights():
    """Ring heights (relative to the chin, in head lengths): close together where the face is busy."""
    hs = [-0.42, -0.3, -0.18, -0.11, -0.06, -0.03, 0.0, 0.025, 0.05, 0.075, 0.1, 0.125]
    m = (MOUTH_Z - CHIN_Z) / L
    hs += [m - 0.06, m - 0.04, m - 0.025, m - 0.012, m, m + 0.012, m + 0.025, m + 0.04]
    nb = (NB - CHIN_Z) / L
    hs += [nb - 0.02, nb - 0.006, nb, nb + 0.008, nb + 0.02, nb + 0.035, (TIP_Z - CHIN_Z) / L + 0.005]
    e = (EYE_Z - CHIN_Z) / L
    hs += [e - 0.11, e - 0.085, e - 0.06, e - 0.04, e - 0.02, e, e + 0.02, e + 0.04, e + 0.06, e + 0.085, e + 0.11]
    hs += [0.75, 0.8, 0.85, 0.9, 0.94, 0.97, 0.99]
    hs = sorted(set(round(h, 4) for h in hs))
    out = []
    for h in hs:
        if not out or h - out[-1] > 0.004:
            out.append(h)
    if DETAIL < 0.8:
        out = [h for i, h in enumerate(out) if i % 2 == 0 or abs(h - m) < 0.001 or abs(h - nb) < 0.03]
    return out, m


def head_thetas(n):
    """Angles round the head, closer together over the face."""
    out = []
    for j in range(n):
        u = -1.0 + 2.0 * j / n                       # -1 .. 1, 0 = front
        out.append(math.copysign(abs(u) ** 1.55, u) * math.pi)
    return out


def build_head(part):
    n = 60 if DETAIL > 0.8 else 34
    thetas = head_thetas(n)
    hs, hm = head_heights()
    im = hs.index(min(hs, key=lambda h: abs(h - hm)))
    rows = []
    for i, h in enumerate(hs):
        z = CHIN_Z + h * L
        row = []
        for j, t in enumerate(thetas):
            co = head_point(t, z)
            w = HEADW if h > -0.12 else blend({"Torso": 1.0}, HEADW, sm(-0.42, -0.12, h))
            slot = SKIN
            if abs(co.x) < MOUTH_HALF * 0.95 and co.y < 0 and abs(z - MOUTH_Z) < 0.011 * FS:
                slot = LIP
            row.append(part.add(co, slot, w))
        rows.append(row)
    # The mouth: along the line of the lips the lower lip gets its own vertices, so it can open.
    mouth_cols = [j for j, t in enumerate(thetas) if abs(head_point(t, MOUTH_Z).x) < MOUTH_HALF * 0.98 and abs(t) < math.pi / 2]
    lower = {}
    for j in mouth_cols[1:-1]:
        v = rows[im][j]
        lower[j] = part.add(part.co[v], LIP, HEADW)
    for i in range(len(hs) - 1):
        for j in range(n):
            j2 = (j + 1) % n
            a, b = rows[i][j], rows[i][j2]
            c, d = rows[i + 1][j2], rows[i + 1][j]
            if i + 1 == im:                          # faces just below the lips use the lower lip's own row
                c = lower.get(j2, c)
                d = lower.get(j, d)
            part.face(a, b, c, d)
    top = part.add(head_point(0.0, CHIN_Z + L * 0.999) + Vector((0, 0.01 * FS, 0)), SKIN, HEADW)
    for j in range(n):
        part.face(rows[-1][j], rows[-1][(j + 1) % n], top)
    # Expressions on the face itself.
    lower_ids = set(lower.values())
    for i, h in enumerate(hs):
        for j in range(n):
            vs = [rows[i][j]]
            if i == im and j in lower:
                vs.append(lower[j])
            for v in vs:
                co = part.co[v]
                if co.y > 0.02 * FS:
                    continue
                # Mouth open: everything below the lips swings down about the hinge, the corners less.
                below = v in lower_ids or co.z < MOUTH_Z - 0.0005
                if below:
                    w = 1.0 - 0.75 * sm(MOUTH_HALF * 0.6, MOUTH_HALF * 2.2, abs(co.x))
                    w *= sm(HINGE.y + 0.02 * FS, HINGE.y - 0.03 * FS, co.y)
                    w *= sm(CHIN_Z - 0.055 * FS, CHIN_Z - 0.01 * FS, co.z)
                    if w > 0.001:
                        KEYS["MouthOpen"][v] = ((JAW_ROT @ (co - HINGE)) + HINGE - co) * w
                # Smile: the corners of the mouth up, out and back; the cheeks lift.
                for side in (1, -1):
                    d = math.hypot(co.x - side * MOUTH_HALF, co.z - MOUTH_Z)
                    g = gauss(d, 0.019 * FS)
                    if g > 0.01:
                        KEYS["Smile"][v] = KEYS["Smile"].get(v, Vector()) + Vector((side * 0.004, 0.003, 0.0055)) * g * FS
                # Brows: the ridge skin moves with the brows a little.
                for e in EYE:
                    g = gauss(co.x - e.x, 0.024 * FS) * gauss(co.z - (BROW_Z + 0.002 * FS), 0.01 * FS)
                    if g > 0.02:
                        KEYS["BrowUp"][v] = KEYS["BrowUp"].get(v, Vector()) + Vector((0, 0, 0.0045)) * g * FS
                        KEYS["BrowDown"][v] = KEYS["BrowDown"].get(v, Vector()) + Vector((0, -0.001, -0.003)) * g * FS
    return rows, hs, thetas


def sphere_part(part, centre, radii, slot, w, n=12, m=8, rot=None, keep=None):
    """A (possibly squashed, partial) sphere; keep(lat, lon) decides which faces stay."""
    rot = rot or Matrix.Identity(3)
    grid = []
    for a in range(m + 1):
        lat = -math.pi / 2 + math.pi * a / m
        row = []
        for b in range(n):
            lon = 2 * math.pi * b / n
            p = Vector((math.cos(lat) * math.sin(lon) * radii[0], -math.cos(lat) * math.cos(lon) * radii[1], math.sin(lat) * radii[2]))
            row.append(part.add(centre + rot @ p, slot, w))
        grid.append(row)
    for a in range(m):
        for b in range(n):
            lat = -math.pi / 2 + math.pi * (a + 0.5) / m
            lon = 2 * math.pi * (b + 0.5) / n
            if keep and not keep(lat, lon):
                continue
            part.face(grid[a][b], grid[a][(b + 1) % n], grid[a + 1][(b + 1) % n], grid[a + 1][b])
    return grid


def build_lids(part):
    """Upper and lower lids: thin shells round each ball; the upper one rolls down to blink."""
    rl = ER * 1.13
    open_top = lerp(0.42, 0.12, P["lids"])          # how low the upper lid comes (radians above the middle)
    for side, e in zip((1, -1), EYE):
        n, m = 16, 8
        tilt = P["tilt"] * FS
        for upper in (True, False):
            lo, hi = (open_top, 1.45) if upper else (-1.45, -0.42)
            rows = []
            for a in range(m + 1):
                lat = lerp(lo, hi, a / m) if upper else lerp(hi, lo, a / m)   # row 0 is the lid's edge
                row = []
                for b in range(n + 1):
                    lon = lerp(-1.25, 1.35, b / n) * side           # inner corner .. past the outer corner
                    # The outer corner lifts for tilted eyes.
                    lat2 = lat + (tilt / rl) * sm(-0.2, 1.3, lon * side) * (1.0 if upper else 0.6)
                    p = Vector((math.cos(lat2) * math.sin(lon) * rl, -math.cos(lat2) * math.cos(lon) * rl, math.sin(lat2) * rl * 0.92))
                    v = part.add(e + p, SKIN, HEADW)
                    row.append(v)
                    if upper:
                        # Blink: roll the lid down over the ball, the edge most.
                        k = 1.0 - sm(0.0, 1.0, (lat - lo) / (hi - lo))
                        ang = -(open_top + 0.55) * (0.35 + 0.65 * k)
                        q = Matrix.Rotation(ang, 3, Vector((1, 0, 0))) @ p
                        KEYS["Blink"][v] = (q - p)
                rows.append(row)
            for a in range(m):
                for b in range(n):
                    if (side > 0) == upper:                  # faces turned away from the ball
                        part.face(rows[a][b], rows[a][b + 1], rows[a + 1][b + 1], rows[a + 1][b])
                    else:
                        part.face(rows[a][b], rows[a + 1][b], rows[a + 1][b + 1], rows[a][b + 1])
            # The lash line along the lid's edge: heavy on top, thin below, longest at the outer corner.
            edge = rows[0]
            pts = [part.co[v] for v in edge]
            nrms = [(p - e).normalized() for p in pts]
            if upper:
                ws = [0.0011 * FS * P["lash"] * (0.45 + 1.1 * sm(0.1, 0.95, b / n)) for b in range(n + 1)]
                ids = ribbon(part, pts, nrms, ws, [HEADW] * len(pts), INK, 0.0006)
                for k, v in enumerate(ids):
                    KEYS["Blink"][v] = KEYS["Blink"][edge[k // 2]]
                last, nrm = pts[-1], nrms[-1]
                tail = [last, last + Vector((side * 0.0045, 0.0015, 0.0018)) * FS]
                ids = ribbon(part, tail, [nrm, nrm], [ws[-1], 0.0002 * FS], [HEADW] * 2, INK, 0.0006)
                for v in ids:
                    KEYS["Blink"][v] = KEYS["Blink"][edge[-1]]
            else:
                k0 = n // 3
                ws = [0.00045 * FS * sm(0.0, 0.6, (b - k0) / max(1, n - k0)) for b in range(k0, n + 1)]
                ribbon(part, pts[k0:], nrms[k0:], ws, [HEADW] * len(ws), INK, 0.0006)


def build_ears(part):
    s = P["ears"]
    for side in (1, -1):
        base = head_base(math.pi / 2 * side, lerp(EYE_Z, NB, 0.45))
        centre = Vector((side * (abs(base.x) + 0.004 * FS), YC + 0.006 * FS, lerp(EYE_Z + 0.004 * FS, NB - 0.004 * FS, 0.5)))
        rot = Matrix.Rotation(math.radians(-12 * side), 3, 'Z') @ Matrix.Rotation(math.radians(14), 3, 'X')
        g = sphere_part(part, centre, (0.008 * FS * s, 0.017 * FS * s, 0.03 * FS * s), SKIN, HEADW, 10, 8, rot)
        # The inside of the ear: a C of ink.
        pts, nrms = [], []
        for k in range(9):
            a = lerp(-1.1, 1.6, k / 8)
            q = centre + rot @ Vector((side * 0.0085 * FS * s, -math.sin(a) * 0.009 * FS * s, math.cos(a) * 0.017 * FS * s))
            pts.append(q)
            nrms.append((rot @ Vector((side, 0, 0))).normalized())
        ribbon(part, pts, nrms, taper(9, 0.0009 * FS, 0.5, 0.2), [HEADW] * 9, INK, 0.0004)


def build_mouth_inside(part):
    """Behind the lips: a dark mouth and a row of teeth, seen only when it opens."""
    p, _ = front_point(0.0, MOUTH_Z)
    c = Vector((0, p.y + 0.012 * FS, MOUTH_Z - 0.003 * FS))
    g = sphere_part(part, c, (MOUTH_HALF * 0.95, 0.008 * FS, 0.012 * FS), MOUTH, HEADW, 12, 6)
    t = sphere_part(part, c + Vector((0, -0.0045 * FS, 0.0055 * FS)), (MOUTH_HALF * 0.7, 0.004 * FS, 0.0032 * FS), TEETH, HEADW, 10, 4)
    for row in g + t:
        for v in row:
            co = part.co[v]
            if co.z < MOUTH_Z - 0.002 * FS:
                KEYS["MouthOpen"][v] = (JAW_ROT @ (co - HINGE)) + HINGE - co


# ---- strokes on the face -------------------------------------------------------------------------
def face_stroke(part, pts_xz, w, start=0.3, end=0.0, peak=0.4, slot=INK, lift=0.0009, keys=None, steps=6):
    """A brush stroke on the face through (x, z) points."""
    pts, nrms = [], []
    seg = []
    for a, b in zip(pts_xz[:-1], pts_xz[1:]):
        for k in range(steps):
            t = k / steps
            seg.append((lerp(a[0], b[0], t), lerp(a[1], b[1], t)))
    seg.append(pts_xz[-1])
    for x, z in seg:
        p, nrm = head_point_xz(x, z)
        pts.append(p); nrms.append(nrm)
    ids = ribbon(part, pts, nrms, taper(len(pts), w, start, end, peak), [HEADW] * len(pts), slot, lift)
    if keys:
        for name, fn in keys.items():
            for v in ids:
                d = fn(part.co[v])
                if d is not None and d.length > 0:
                    KEYS[name][v] = KEYS[name].get(v, Vector()) + d
    return ids


def brow_up(co):
    return Vector((0, 0, 0.0055 * FS))


def brow_down(co):
    inner = gauss(abs(co.x) - 0.012 * FS, 0.012 * FS)
    return Vector((-math.copysign(0.0022 * FS * inner, co.x), -0.0012 * FS, -0.0032 * FS - 0.0022 * FS * inner))


def build_face_ink(part):
    f = FS
    male = not FEMALE
    for side, e in zip((1, -1), EYE):
        sx = side
        # Brows: graphic, angular shapes; heavy for the men, a thinner high arch for the women.
        bz = BROW_Z + (0.002 if male else 0.004) * f
        inner = (sx * 0.011 * f, bz - 0.002 * f)
        peak = (sx * (0.033 if male else 0.036) * f, bz + (0.0035 if male else 0.0065) * f)
        outer = (sx * 0.054 * f, bz - (0.003 if male else 0.002) * f)
        w = (0.0032 if male else 0.0019) * f * (1.15 if NAME == "jonte" else 1.0)
        face_stroke(part, [inner, peak, outer], w, 0.85, 0.1, 0.3, BROW, 0.0011, {"BrowUp": brow_up, "BrowDown": brow_down})
        # A crease above the lid (heavier on the men).
        face_stroke(part, [(e.x - sx * 0.008 * f, e.z + ER * 1.45), (e.x + sx * 0.004 * f, e.z + ER * 1.65), (e.x + sx * 0.013 * f, e.z + ER * 1.25)],
                    0.0006 * f * (1.4 if male else 1.0), 0.2, 0.0, 0.5, INK, 0.0008, {"BrowUp": lambda c: Vector((0, 0, 0.002 * FS))})
        # Under the eye: a short line out from the middle (age, tiredness).
        if male or NAME == "linn":
            face_stroke(part, [(e.x - sx * 0.002 * f, e.z - ER * 1.5), (e.x + sx * 0.012 * f, e.z - ER * 1.25)], 0.0005 * f * (1 + P["age"]), 0.2, 0.0, 0.5)
        # Cheekbone hatching: a few short strokes under the bone.
        if P["bone"] > 0.004 or male:
            for k in range(3 if male else 1):
                x0 = sx * (0.04 + 0.006 * k) * f
                z0 = EYE_Z - (0.036 + 0.004 * k) * f
                face_stroke(part, [(x0, z0), (x0 + sx * 0.012 * f, z0 - 0.009 * f)], 0.0006 * f, 0.1, 0.0, 0.4)
        # From the nose wing down past the mouth.
        face_stroke(part, [(sx * NW * 1.15, NB + 0.006 * f), (sx * (NW * 1.15 + 0.008 * f), NB - 0.006 * f), (sx * (MOUTH_HALF + 0.006 * f), MOUTH_Z - 0.004 * f)],
                    0.0007 * f * (1.0 + P["age"]), 0.2, 0.0, 0.35, INK, 0.0009, {"Smile": lambda c: Vector((math.copysign(0.002 * FS, c.x), 0, 0.003 * FS))})
        # Nose wing: the curve round it.
        face_stroke(part, [(sx * NW * 0.5, NB + 0.0005 * f), (sx * NW * 1.05, NB + 0.003 * f), (sx * NW * 1.12, NB + 0.009 * f)], 0.0007 * f, 0.4, 0.1, 0.5)
    # The shadow side of the nose: one stroke from the brow down the bridge to the wing (on the right).
    face_stroke(part, [(-0.009 * f, ROOT_Z - 0.002 * f), (-NW * 0.42, lerp(ROOT_Z, TIP_Z, 0.55)), (-NW * 0.6, TIP_Z - 0.002 * f)], 0.0008 * f, 0.1, 0.25, 0.6)
    # Nostrils: two dark commas under the tip.
    for sx in (1, -1):
        face_stroke(part, [(sx * NW * 0.15, NB + 0.0008 * f), (sx * NW * 0.45, NB + 0.0016 * f)], 0.0011 * f, 0.5, 0.3, 0.5, INK, 0.0006)
    # The mouth: the line between the lips, heavy in the middle.
    pts = [(x * MOUTH_HALF, MOUTH_Z + (0.0004 * f if abs(x) < 0.5 else -0.0008 * f * (abs(x) - 0.5) * 2)) for x in (-1.0, -0.6, -0.25, 0.0, 0.25, 0.6, 1.0)]
    face_stroke(part, pts, 0.0008 * f, 0.15, 0.15, 0.5, INK, 0.0007,
                {"Smile": lambda c: Vector((math.copysign(0.004 * FS, c.x), 0.003 * FS, 0.0055 * FS)) * gauss(abs(c.x) - MOUTH_HALF, 0.014 * FS)})
    # Under the lower lip, a short shadow; Erik's cleft; the line under the chin.
    face_stroke(part, [(-MOUTH_HALF * 0.45, MOUTH_Z - 0.013 * f), (MOUTH_HALF * 0.45, MOUTH_Z - 0.013 * f)], 0.0007 * f, 0.1, 0.1, 0.5, INK, 0.0008,
                {"MouthOpen": lambda c: (JAW_ROT @ (c - HINGE)) + HINGE - c})
    if NAME == "erik":
        face_stroke(part, [(0.0, CHIN_Z + 0.022 * f), (0.0005 * f, CHIN_Z + 0.009 * f)], 0.0006 * f, 0.2, 0.2, 0.5, INK, 0.0008,
                    {"MouthOpen": lambda c: (JAW_ROT @ (c - HINGE)) + HINGE - c})
    if P["age"] > 0.3:
        for k in range(2):
            z = BROW_Z + (0.018 + 0.008 * k) * f
            face_stroke(part, [(-0.022 * f, z), (0.0, z + 0.001 * f), (0.022 * f, z)], 0.0004 * f, 0.1, 0.1, 0.5)


# ---- the body --------------------------------------------------------------------------------------
BAGGY = STYLE == "baggy"
PUFF = {"puffer": 1.14, "baggy": 1.3, "retro": 1.05, "sleek": 0.96, "onepiece": 1.04}[STYLE]
NR = 24 if DETAIL > 0.8 else 14            # points round a torso ring
NL = 16 if DETAIL > 0.8 else 10            # round a limb


def limb_rings(points, radii, n, flat=1.0, phase=0.0):
    """Rings along a bent path (points), one per point, each across the local direction."""
    rings, frames = [], []
    for k, (p, r) in enumerate(zip(points, radii)):
        a = points[max(k - 1, 0)]
        b = points[min(k + 1, len(points) - 1)]
        d = (b - a).normalized()
        x, y = frame(d)
        rings.append(ring(p, x, y, r * flat if isinstance(r, float) else r[0], r if isinstance(r, float) else r[1], n, 2.2, 0.0, phase))
        frames.append((x, y, d))
    return rings, frames


def path(points, counts):
    """Points along a polyline, counts[i] steps on segment i."""
    out = []
    for i in range(len(points) - 1):
        for k in range(counts[i]):
            out.append(points[i].lerp(points[i + 1], k / counts[i]))
    out.append(points[-1])
    return out


def build_neck(part):
    top = Vector((0, J["Head"].y + 0.002, CHIN_Z + 0.02 * FS))
    bot = Vector((0, 0.016 * S, SH_Z - 0.01 * S))
    pts = path([bot, top], [5])
    r = P["neck"] * S
    rings = [ring(p, Vector((1, 0, 0)), Vector((0, -1, 0)), r * 1.02, r * 0.96, NL) for p in pts]

    def wts(i, j):
        return blend({"Torso": 1.0}, HEADW, sm(0.15, 0.9, i / (len(pts) - 1)))
    lo = Loft(part, rings, lambda i, j: SKIN, wts)
    if not FEMALE:
        # Adam's apple and two cords down the neck.
        lo_u = len(pts) * 0.45
        stroke_on(lo, [(lo_u - 0.6, NL * 0.25 - 0.4), (lo_u + 0.4, NL * 0.25 + 0.4)], 0.0007 * FS, 0.3, 0.3, 0.5)
        for sx in (1, -1):
            v0 = NL * 0.25 - sx * NL * 0.12
            stroke_on(lo, [(0.6, v0 - sx * 0.6), (len(pts) - 1.6, v0 + sx * 0.4)], 0.0006 * FS, 0.1, 0.0, 0.5)
    return lo


def torso_rows():
    (ca, cb), (wa, wb), (ha, hb) = P["chest"], P["waist"], P["hips"]
    s, b = S, P["belly"]
    hem = {"baggy": 0.84, "sleek": 0.95, "onepiece": 1.0, "retro": 0.92}.get(STYLE, 0.9)
    k = PUFF
    rows = [
        # z, half width, half depth, power, front, y offset
        (hem * s, ha * k + 0.012, hb * k + 0.01, 2.4, b * 0.4, 0.0),
        ((hem + 0.05) * s, ha * k + 0.01, hb * k + 0.008, 2.4, b * 0.5, 0.0),
        (1.06 * s, wa * k + 0.012, wb * k + 0.008, 2.5, b * 1.2, 0.0),
        (1.15 * s, lerp(wa, ca, 0.45) * k + 0.012, lerp(wb, cb, 0.45) * k + 0.008, 2.55, b * 1.0, -0.004),
        (1.27 * s, ca * k + 0.012, cb * k + 0.01, 2.6, (0.16 if FEMALE else 0.06), -0.006),
        (1.36 * s, ca * k + 0.016, cb * k + 0.002, 2.75, 0.04, -0.002),
        (SH_Z - 0.002, P["shoulders"] * S + 0.045 * k, cb * 0.8 * k, 3.1, 0.0, 0.012),
        (SH_Z + 0.032 * s, lerp(P["shoulders"] * S, P["neck"] * S, 0.55) + 0.01, 0.07 * k, 2.4, 0.0, 0.014),
        (SH_Z + 0.06 * s, P["neck"] * S + 0.018, P["neck"] * S + 0.016, 2.0, 0.0, 0.014),
    ]
    return rows


def build_jacket(part):
    rows = torso_rows()
    rings = [ring(Vector((0, yo, z)), Vector((1, 0, 0)), Vector((0, -1, 0)), a, b, NR, pw, fr) for z, a, b, pw, fr, yo in rows]
    collar_top = SH_Z + 0.105 * S
    rings.append(ring(Vector((0, 0.016, collar_top)), Vector((1, 0, 0)), Vector((0, -1, 0)), P["neck"] * S + 0.022, P["neck"] * S + 0.02, NR))
    n = len(rings)

    def slot(i, j):
        z = rings[i][j].z
        if i >= n - 2:
            return TRIM                                   # the collar
        if STYLE == "retro" and 1.24 * S < z < 1.33 * S:
            return TRIM                                   # the band across the chest
        if STYLE == "onepiece" and abs(z - 1.04 * S) < 0.03 * S:
            return TRIM                                   # the belt
        if STYLE == "baggy" and i == 0:
            return TRIM
        return JACKET

    def wts(i, j):
        z = rings[i][j].z
        w = blend({"Hips": 1.0}, {"Torso": 1.0}, sm(0.98 * S, 1.12 * S, z))
        x = rings[i][j].x
        # The tops of the shoulders go with the arms a little.
        if z > 1.33 * S and abs(x) > P["shoulders"] * S * 0.65:
            sidebone = "ShoulderL" if x > 0 else "ShoulderR"
            w = blend(w, {sidebone: 1.0}, 0.45 * sm(P["shoulders"] * S * 0.65, P["shoulders"] * S * 1.05, abs(x)))
        return w
    lo = Loft(part, rings, slot, wts)
    q = NR // 4                                           # the front, round the ring
    # Ink: the zip down the middle, folds from the armpits, creases at the waist.
    stroke_on(lo, [(0.2, q), (n - 2.2, q)], 0.0008 * S, 0.6, 0.6, 0.5, INK, 0.0015)
    for sx in (1, -1):
        v_side = 0 if sx > 0 else NR // 2
        v_front = q - sx * NR * 0.12
        stroke_on(lo, [(5.0, v_side + sx * 0.6 * -1), (4.3, v_front)], 0.0016 * S, 0.1, 0.0, 0.3)
        stroke_on(lo, [(4.7, v_side - sx * 0.3), (3.6, v_front + sx * 0.8)], 0.0012 * S, 0.1, 0.0, 0.3)
        stroke_on(lo, [(2.0, v_side + sx * 1.2 * -1 + (NR if sx < 0 else 0) * 0), (2.25, v_front - sx * 1.5)], 0.0012 * S, 0.1, 0.0, 0.4)
        stroke_on(lo, [(1.5, v_side - sx * 0.8), (1.6, v_front - sx * 2.2)], 0.001 * S, 0.1, 0.0, 0.4)
    if STYLE in ("puffer", "baggy"):
        # Quilting: seams round the jacket.
        for u in (1.6, 2.8, 3.9, 4.9):
            stroke_on(lo, [(u, 0.0), (u + 0.05, NR * 0.5), (u, NR * 1.0 - 0.01)], 0.0009 * S, 0.8, 0.8, 0.5, INK, 0.0016, steps=6)
    if STYLE == "sleek":
        for sx in (1, -1):                                # piping: a diagonal line of colour
            stroke_on(lo, [(5.6, q + sx * NR * 0.2), (2.0, q - sx * NR * 0.08)], 0.0022 * S, 1.0, 1.0, 0.5, TRIM, 0.0015)
    return lo


def build_arm(part, side):
    tag = "L" if side > 0 else "R"
    sh, el, hd = J["Shoulder" + tag], J["Elbow" + tag], J["Hand" + tag]
    d1 = (el - sh).normalized()
    start = sh - d1 * 0.035 * S + Vector((0, 0.004, 0.018 * S))
    k = PUFF * P["limbs"]
    pts = path([start, sh, el, hd], [2, 6, 6])
    pts[-1] = hd + (hd - el).normalized() * 0.012
    base = [0.064, 0.071, 0.066, 0.062, 0.06, 0.058, 0.055, 0.052, 0.05, 0.05, 0.051, 0.051, 0.05, 0.05, 0.055 if BAGGY else 0.052]
    radii = [r * k * S for r in base[:len(pts)]]
    rings, frames = limb_rings(pts, radii, NL)
    n = len(rings)

    def slot(i, j):
        if i >= n - 2:
            return TRIM                                   # the cuff
        if STYLE == "retro" and 4 <= i <= 5:
            return TRIM
        return JACKET

    def wts(i, j):
        if i <= 1:
            return blend({"Torso": 1.0}, {"Shoulder" + tag: 1.0}, 0.55 + 0.25 * i)
        if i < 7:
            return blend({"Shoulder" + tag: 1.0}, {"Elbow" + tag: 1.0}, sm(5.0, 8.0, i))
        if i < n - 2:
            return blend({"Shoulder" + tag: 1.0}, {"Elbow" + tag: 1.0}, sm(6.0, 8.5, i))
        return blend({"Elbow" + tag: 1.0}, {"Hand" + tag: 1.0}, 0.35)
    lo = Loft(part, rings, slot, wts, cap0=True)
    # Folds: the inside of the elbow, and the sleeve bunching over the wrist.
    inner = NL * 0.5 if side > 0 else 0.0                 # the side facing the body
    front = NL * 0.25
    e = 8.0
    for k2, off in enumerate((-0.5, 0.4, 1.2)):
        stroke_on(lo, [(e + off - 0.3, front - 1.6 + k2 * 0.3), (e + off + 0.3, front + 1.4 - k2 * 0.4)], 0.0012 * S, 0.1, 0.0, 0.4)
    for k2 in range(2):
        u = n - 3.2 + k2 * 0.7
        stroke_on(lo, [(u, front - 2.0), (u - 0.25, front + 0.5), (u + 0.1, front + 2.4)], 0.0009 * S, 0.1, 0.0, 0.5)
    stroke_on(lo, [(2.6, inner + 1.0), (4.8, inner - 0.6)], 0.001 * S, 0.1, 0.0, 0.5)
    return lo, frames


def build_glove(part, side):
    """A gloved fist: cuff over the sleeve, the back of the hand, fingers curled round a grip, a thumb."""
    tag = "L" if side > 0 else "R"
    el, hd = J["Elbow" + tag], J["Hand" + tag]
    d = (hd - el).normalized()
    palm = Vector((-side, 0.0, 0.0))
    palm = (palm - d * palm.dot(d)).normalized()         # the palm faces the body
    across = d.cross(palm).normalized()
    s = S * (1.08 if BAGGY else 1.0) * lerp(1.0, P["limbs"], 0.5)
    w = {"Hand" + tag: 1.0}
    rings = []
    # Cuff: over the end of the sleeve.
    for t, r in ((-0.05, 0.058), (-0.02, 0.06), (0.0, 0.056), (0.012, 0.047)):
        c = hd + d * t * s
        rings.append(ring(c, across, palm, r * s, r * s * 0.95, NL))
    # The hand: flat, then the fingers rolling in toward the palm.
    L1 = 0.075 * s
    for t, (ha, hb) in ((0.03, (0.044, 0.024)), (0.06, (0.046, 0.024)), (0.085, (0.046, 0.023))):
        c = hd + d * t * s
        rings.append(ring(c - palm * 0.002, across, palm, ha * s, hb * s, NL))
    knuckle = hd + d * 0.09 * s
    R = 0.022 * s
    centre = knuckle + palm * R
    for k in range(1, 6):
        a = math.pi * k / 6.0
        dirk = d * math.cos(a) + palm * math.sin(a)      # rolling the fingers in
        c = centre - palm * R * math.cos(a) * 1.0 + d * R * math.sin(a) - palm * R * 0.0
        c = centre + (d * math.sin(a) - palm * math.cos(a)) * R
        nrm_axis = (d * math.cos(a) + palm * math.sin(a)).normalized()
        y = nrm_axis.cross(across).normalized()
        rings.append(ring(c, across, y, 0.043 * s, 0.017 * s, NL))
    n = len(rings)
    lo = Loft(part, rings, lambda i, j: GLOVE, lambda i, j: w if i > 1 else blend(w, {"Elbow" + tag: 1.0}, 0.3), cap0=True, cap1=True)
    # The thumb, across the front of the curled fingers.
    tb = hd + d * 0.04 * s + palm * 0.025 * s + across * (-0.035 * s)
    tt = centre + palm * 0.012 * s + across * (-0.006 * s) + d * 0.01 * s
    tp = path([tb, tb.lerp(tt, 0.5) + palm * 0.008 * s, tt], [2, 2])
    trings, _ = limb_rings(tp, [0.017 * s, 0.016 * s, 0.015 * s, 0.0135 * s, 0.012 * s], 8 if DETAIL > 0.8 else 6)
    Loft(part, trings, lambda i, j: GLOVE, lambda i, j: w, cap0=True, cap1=True)
    # Ink: the knuckles, the seam of the cuff.
    stroke_on(lo, [(5.6, NL * 0.75 - 2.5), (5.6, NL * 0.75 + 2.5)], 0.0009 * S, 0.3, 0.3, 0.5)
    stroke_on(lo, [(2.1, 0.0), (2.1, NL * 0.5), (2.1, NL - 0.01)], 0.0008 * S, 0.8, 0.8, 0.5)
    grip = centre
    return grip, palm


def build_pants(part):
    (wa, wb), (ha, hb) = P["waist"], P["hips"]
    s = S
    k = (1.28 if BAGGY else 1.06 if STYLE in ("puffer", "retro") else 1.0)
    crotch = (0.8 if BAGGY else 0.85) * s
    rows = [(1.08 * s, wa * 1.02 + 0.006, wb + 0.006), (0.99 * s, ha * k + 0.004, hb * k), (0.92 * s, ha * k + 0.006, hb * k + 0.004),
            (crotch + 0.03 * s, ha * k * 0.96, hb * k * 0.95), (crotch, ha * k * 0.8, hb * k * 0.82)]
    rings = [ring(Vector((0, 0.0, z)), Vector((1, 0, 0)), Vector((0, -1, 0)), a, b, NR, 2.4, P["belly"] * 0.6 if i < 2 else 0.0) for i, (z, a, b) in enumerate(rows)]

    def pw(i, j):
        x = rings[i][j].x
        w = {"Hips": 1.0}
        if i >= 2:
            w = blend(w, {("HipL" if x > 0 else "HipR"): 1.0}, 0.3 * (i - 1) / 3)
        return w
    pelvis = Loft(part, rings, lambda i, j: TRIM if (STYLE == "onepiece" and i == 0) else PANTS, pw, cap1=True)
    legs = []
    for side in (1, -1):
        tag = "L" if side > 0 else "R"
        hp, kn, ft = J["Hip" + tag], J["Knee" + tag], J["Foot" + tag]
        start = hp + Vector((0, 0, 0.05 * s))
        hem = ft + Vector((0, -0.005, 0.085 * s))
        pts = path([start, kn, hem], [6, 7])
        base = [0.094, 0.092, 0.087, 0.081, 0.075, 0.069, 0.063, 0.062, 0.063, 0.062, 0.058, 0.057, 0.062, 0.07]
        base = base[:len(pts)]
        kk = k * P["limbs"]
        radii = [r * kk * s for r in base]
        if BAGGY:
            radii[-2] *= 1.25; radii[-1] *= 1.35
        rings, frames = limb_rings(pts, radii, NL)
        n = len(rings)

        def wts(i, j, tag=tag, n=n):
            if i == 0:
                return blend({"Hips": 1.0}, {"Hip" + tag: 1.0}, 0.5)
            if i < 6:
                return blend({"Hip" + tag: 1.0}, {"Knee" + tag: 1.0}, sm(4.5, 7.0, i))
            if i < n - 1:
                return blend({"Hip" + tag: 1.0}, {"Knee" + tag: 1.0}, sm(4.5, 7.0, i))
            return blend({"Knee" + tag: 1.0}, {"Foot" + tag: 1.0}, 0.3)
        lo = Loft(part, rings, lambda i, j, n=n: TRIM if (i == n - 1 and STYLE in ("retro", "sleek")) else PANTS, wts, cap0=True)
        front = NL * 0.25
        # Knee folds front and back, the crotch pull, folds stacking over the boot.
        for k2, off in enumerate((-0.6, 0.2, 0.9)):
            stroke_on(lo, [(6.0 + off, front - 2.2 + k2 * 0.3), (6.0 + off - 0.25, front + 0.2), (6.0 + off + 0.1, front + 2.1)], 0.0011 * S, 0.1, 0.0, 0.4)
            stroke_on(lo, [(6.0 + off * 0.7, front + NL * 0.5 - 1.8), (6.0 + off * 0.7 + 0.2, front + NL * 0.5 + 1.8)], 0.001 * S, 0.1, 0.0, 0.5)
        inner = NL * 0.5 if side > 0 else 0.0
        stroke_on(lo, [(0.4, inner + side * 0.8), (2.3, front + side * -1.5)], 0.0013 * S, 0.1, 0.0, 0.35)
        stroke_on(lo, [(0.9, inner + side * 0.3), (2.8, front + side * -0.4)], 0.001 * S, 0.1, 0.0, 0.35)
        for k2 in range(3 if BAGGY else 2):
            u = n - 2.6 + k2 * 0.6
            stroke_on(lo, [(u, front - 2.6), (u + 0.2, front), (u - 0.1, front + 2.6)], 0.001 * S, 0.1, 0.0, 0.5)
        legs.append(lo)
    return pelvis, legs


# ---- putting it together -------------------------------------------------------------------------
skin = Part("skin")
head_rows, head_hs, head_thetas_used = build_head(skin)
build_lids(skin)
build_ears(skin)
build_mouth_inside(skin)
build_face_ink(skin)
neck = build_neck(skin)

jacket = Part("jacket")
build_jacket(jacket)
build_arm(jacket, 1)
build_arm(jacket, -1)

gloves = Part("gloves")
GRIP, PALM = build_glove(gloves, 1)
build_glove(gloves, -1)

pants = Part("pants")
build_pants(pants)

PARTS = [skin, jacket, pants, gloves]


def head_rings():
    """The outline of the skull at a row of heights above the head joint (for helmets, hair, goggles)."""
    rings = []
    n = 16
    for i in range(n):
        y = -0.2 + (1.9 + 0.2) * i / (n - 1)
        z = J["Head"].z + y * HR
        if z > CROWN - 0.002:
            z = CROWN - 0.002
        pts = [head_base(t, z) for t in [k * 2 * math.pi / 48 - math.pi for k in range(48)]]
        off_nose = [p for p in pts if abs(p.x) > 0.02 * FS] or pts
        rings.append((y * HR, max(abs(p.x) for p in pts), -(min(p.y for p in off_nose) - J["Head"].y), -(max(p.y for p in pts) - J["Head"].y)))
    return rings


RINGS = head_rings()
MOUTH_Y = front_point(0.0, MOUTH_Z)[0].y
NOSE = head_point_xz(0.0, TIP_Z)[0]
EAR = Vector((P["skull"] * FS + 0.004 * FS, YC + 0.006 * FS, lerp(EYE_Z, NB, 0.5)))
HEIGHT = H


def to_blender(part):
    me = bpy.data.meshes.new(part.name)
    me.from_pydata([tuple(c) for c in part.co], [], part.faces)
    me.update()
    ob = bpy.data.objects.new(part.name, me)
    bpy.context.collection.objects.link(ob)
    att = me.attributes.new("slot", 'INT', 'POINT')
    for i, s in enumerate(part.slot):
        att.data[i].value = s
    for b in BONES:
        ob.vertex_groups.new(name=b)
    for i, w in enumerate(part.w):
        tot = sum(w.values()) or 1.0
        for b, x in w.items():
            ob.vertex_groups[b].add([i], x / tot, 'REPLACE')
    return ob


def concavity(me):
    """How far each vertex sits below its neighbours (0 = flat or convex): folds and sockets."""
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
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


def export(objs, path):
    out = bytearray()
    out += b"SKM3"
    out += struct.pack("<f", HEIGHT)
    out += struct.pack("<i", len(BONES))
    for b in BONES:
        out += struct.pack("<3f", *game(J[b]))
    out += struct.pack("<f", HR)
    out += struct.pack("<3f", *game(EYE[0])) + struct.pack("<3f", *game(EYE[1])) + struct.pack("<f", ER)
    out += struct.pack("<3f", *game(Vector((0, MOUTH_Y, MOUTH_Z)))) + struct.pack("<f", MOUTH_HALF)
    out += struct.pack("<3f", *game(NOSE)) + struct.pack("<3f", *game(EAR))
    out += struct.pack("<3f", *game(GRIP)) + struct.pack("<3f", *game(PALM))
    out += struct.pack("<i", len(RINGS))
    for r in RINGS:
        out += struct.pack("<4f", *r)
    out += struct.pack("<i", len(objs))
    tris_total = 0
    for part, ob in objs:
        me = ob.data
        me.calc_loop_triangles()
        name = part.name.encode()
        out += struct.pack("<i", len(name)) + name
        n = len(me.vertices)
        assert n < 65536, (part.name, n)
        crease = concavity(me)
        pts = [game(v.co) for v in me.vertices]
        lo = [min(p[i] for p in pts) for i in range(3)]
        size = [max(max(p[i] for p in pts) - lo[i], 1e-6) for i in range(3)]
        out += struct.pack("<i", n) + struct.pack("<3f", *lo) + struct.pack("<3f", *size)
        for p in pts:
            out += struct.pack("<3H", *(int(round((p[i] - lo[i]) / size[i] * 65535)) for i in range(3)))
        for i in range(n):
            c = 0.0 if part.slot[i] == INK else crease[i]
            out += struct.pack("<2B", part.slot[i], int(min(1.0, c * 4.0) * 255))
        for i in range(n):
            ws = sorted(((x, BONES.index(b)) for b, x in part.w[i].items()), reverse=True)[:4]
            tot = sum(x for x, _ in ws) or 1.0
            ws += [(0.0, 0)] * (4 - len(ws))
            q = [int(round(x / tot * 255)) for x, _ in ws]
            q[0] += 255 - sum(q)
            for (x, b), qw in zip(ws, q):
                out += struct.pack("<2B", b, qw)
        tris = me.loop_triangles
        tris_total += len(tris)
        out += struct.pack("<i", len(tris) * 3)
        for t in tris:
            out += struct.pack("<3H", t.vertices[0], t.vertices[2], t.vertices[1])   # mirrored: the winding flips
        keys = KEYS if part is skin else {}
        out += struct.pack("<i", len(keys))
        for kn, deltas in keys.items():
            bn = kn.encode()
            moved = [(i, d) for i, d in deltas.items() if d.length > 0.00005]
            scale = max((max(abs(c) for c in d) for _, d in moved), default=1e-6)
            out += struct.pack("<i", len(bn)) + bn + struct.pack("<f", scale) + struct.pack("<i", len(moved))
            for i, d in sorted(moved):
                gd = (-d.x, d.z, -d.y)
                out += struct.pack("<H3h", i, *(int(round(c / scale * 32767)) for c in gd))
    with open(path, "wb") as f:
        f.write(out)
    print("EXPORT %s  %d bytes  %d tris  %s" % (path, len(out), tris_total, {p.name: len(o.data.loop_triangles) for p, o in objs}))


bpy.ops.wm.read_factory_settings(use_empty=True)
objs = [(p, to_blender(p)) for p in PARTS]
print("HEIGHT %.3f  head %.3f  eyes %s" % (H, L, [tuple(round(c, 3) for c in e) for e in EYE]))
export(objs, "%s/%s.bytes" % (OUT, NAME))

# ---- previews ----------------------------------------------------------------------------------------
COLOURS = {SKIN: (0.93, 0.74, 0.62), LIP: (0.75, 0.4, 0.38), MOUTH: (0.25, 0.08, 0.1), BROW: (0.15, 0.1, 0.08),
           JACKET: (0.95, 0.5, 0.23), TRIM: (0.15, 0.3, 0.6), PANTS: (0.2, 0.22, 0.32), GLOVE: (0.12, 0.12, 0.14),
           INK: (0.06, 0.04, 0.1), TEETH: (0.95, 0.95, 0.92)}
for part, ob in objs:
    me = ob.data
    col = me.color_attributes.new("Col", 'FLOAT_COLOR', 'POINT')
    for i, s in enumerate(part.slot):
        c = COLOURS[s]
        col.data[i].color = (c[0], c[1], c[2], 1.0)
    for poly in me.polygons:
        poly.use_smooth = False
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'VERTEX'
scene.display.shading.show_object_outline = True
scene.display.shading.object_outline_color = (0.05, 0.03, 0.08)
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = 'BOTH'
scene.render.film_transparent = False
world = bpy.data.worlds.new("w"); scene.world = world
cam_data = bpy.data.cameras.new("cam")
cam = bpy.data.objects.new("cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam


def shot(file, target, dist, yaw, w=700, h=900, lens=50, pitch=4.0):
    scene.render.resolution_x, scene.render.resolution_y = w, h
    cam_data.lens = lens
    a = math.radians(yaw)
    pos = target + Vector((math.sin(a) * dist, -math.cos(a) * dist, math.tan(math.radians(pitch)) * dist))
    cam.location = pos
    cam.rotation_euler = (target - pos).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = file
    bpy.ops.render.render(write_still=True)


import os
os.makedirs(PREVIEW, exist_ok=True)
shot("%s/%s_body.png" % (PREVIEW, NAME), Vector((0, 0, H * 0.52)), 4.2, 25, 600, 900, 50, 2)
shot("%s/%s_face.png" % (PREVIEW, NAME), Vector((0, -0.05, EYE_Z - 0.02)), 0.75, 0, 600, 600, 85, 0)
shot("%s/%s_face34.png" % (PREVIEW, NAME), Vector((0, -0.04, EYE_Z - 0.02)), 0.75, 38, 600, 600, 85, 0)
