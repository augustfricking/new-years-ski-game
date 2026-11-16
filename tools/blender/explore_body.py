"""Reports the structure of a base body: attributes, face sets, openings, landmarks; renders previews.
Run: blender --background --disable-autoexec <bundle.blend> --python tools/blender/explore_body.py -- <object name> <out dir>
"""
import sys
import bpy
import bmesh
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
name, out = argv[0], argv[1]
ob = bpy.data.objects[name]
me = ob.data
print("OBJ", ob.name, "verts", len(me.vertices), "polys", len(me.polygons), "loc", tuple(round(c, 3) for c in ob.location), "scale", tuple(ob.scale))
print("ATTRS", [(a.name, a.domain, a.data_type) for a in me.attributes])
print("MODS", [(m.type, getattr(m, 'total_levels', None)) for m in ob.modifiers])
print("VGROUPS", [g.name for g in ob.vertex_groups][:20])
print("MATS", [m.name if m else None for m in me.materials])

co = [ob.matrix_world @ v.co for v in me.vertices]
mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
print("BBOX", tuple(round(c, 3) for c in mn), tuple(round(c, 3) for c in mx))

# Face sets: id -> count and bounding box (relative to the object's origin).
fs = me.attributes.get(".sculpt_face_set")
if fs:
    groups = {}
    for p in me.polygons:
        groups.setdefault(fs.data[p.index].value, []).append(p)
    for gid, polys in sorted(groups.items()):
        cs = [p.center for p in polys]
        lo = Vector((min(c.x for c in cs), min(c.y for c in cs), min(c.z for c in cs)))
        hi = Vector((max(c.x for c in cs), max(c.y for c in cs), max(c.z for c in cs)))
        print(f"FACESET {gid:4d} n={len(polys):5d} lo=({lo.x:.2f},{lo.y:.2f},{lo.z:.2f}) hi=({hi.x:.2f},{hi.y:.2f},{hi.z:.2f})")

# Openings: boundary edge loops.
bm = bmesh.new()
bm.from_mesh(me)
bm.edges.ensure_lookup_table()
boundary = [e for e in bm.edges if e.is_boundary]
seen = set()
loops = []
for e in boundary:
    if e.index in seen:
        continue
    stack, loop = [e], []
    while stack:
        cur = stack.pop()
        if cur.index in seen:
            continue
        seen.add(cur.index)
        loop.append(cur)
        for v in cur.verts:
            for o in v.link_edges:
                if o.is_boundary and o.index not in seen:
                    stack.append(o)
    loops.append(loop)
print("BOUNDARY LOOPS", len(loops))
for loop in loops:
    vs = {v for e in loop for v in e.verts}
    c = sum((v.co for v in vs), Vector()) / len(vs)
    lo = Vector((min(v.co.x for v in vs), min(v.co.y for v in vs), min(v.co.z for v in vs)))
    hi = Vector((max(v.co.x for v in vs), max(v.co.y for v in vs), max(v.co.z for v in vs)))
    print(f"  loop edges={len(loop):3d} centre=({c.x:.3f},{c.y:.3f},{c.z:.3f}) size=({hi.x-lo.x:.3f},{hi.y-lo.y:.3f},{hi.z-lo.z:.3f})")
print("NONMANIFOLD verts", sum(1 for v in bm.verts if not v.is_manifold), "loose", sum(1 for v in bm.verts if not v.link_edges))
bm.free()

# Related objects (eyes) near this body.
for o in bpy.data.objects:
    if o.name.startswith(name + "."):
        print("PART", o.name, tuple(round(c, 3) for c in (o.location - ob.location)), "dim", tuple(round(c, 3) for c in o.dimensions))

# Previews: hide everything else, frame the body.
for o in bpy.data.objects:
    o.hide_render = not (o == ob or o.name.startswith(name + "."))
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
sh = scene.display.shading
sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.8, 0.72, 0.66)
sh.show_cavity = True; sh.cavity_type = 'BOTH'; sh.show_object_outline = True
scene.render.resolution_x = 700; scene.render.resolution_y = 900
cam_data = bpy.data.cameras.new("c"); cam = bpy.data.objects.new("c", cam_data); scene.collection.objects.link(cam); scene.camera = cam
cam_data.type = 'ORTHO'
centre = ob.matrix_world @ ((mn + mx) * 0.5 - ob.location) if False else (mn + mx) * 0.5
def shot(file, offset, scale, target):
    cam.location = target + offset
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam_data.ortho_scale = scale
    scene.render.filepath = out + "/" + file
    bpy.ops.render.render(write_still=True)
height = mx.z - mn.z
shot("explore_front.png", Vector((0, -5, 0)), height * 1.08, centre)
shot("explore_side.png", Vector((5, 0, 0)), height * 1.08, centre)
head = Vector((centre.x, centre.y - 0.02, mx.z - 0.12))
scene.render.resolution_y = 700
shot("explore_face.png", Vector((1.2, -4, 0.3)), 0.34, head)
shot("explore_hand.png", Vector((0.5, -4, 1.0)), 0.3, Vector((mx.x - 0.08, centre.y, mn.z + height * 0.47)))
