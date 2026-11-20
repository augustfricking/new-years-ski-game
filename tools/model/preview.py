"""Renders an OBJ (and optionally a texture) from the front, side and three-quarter, for looking at.

  blender --background --factory-startup --python tools/model/preview.py -- <mesh.obj> <out.png> [texture.png]
"""
import sys
import math
import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
path, out = argv[0], argv[1]
tex = argv[2] if len(argv) > 2 else None
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=path, forward_axis='Y', up_axis='Z')
ob = bpy.context.selected_objects[0]
bpy.ops.object.shade_smooth()
mat = bpy.data.materials.new("m"); mat.use_nodes = True
bsdf = mat.node_tree.nodes["Principled BSDF"]
if tex:
    t = mat.node_tree.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(tex)
    mat.node_tree.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
else:
    bsdf.inputs["Base Color"].default_value = (0.8, 0.75, 0.7, 1)
ob.data.materials.append(mat)
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'TEXTURE' if tex else 'MATERIAL'
scene.display.shading.show_object_outline = True
scene.render.resolution_x, scene.render.resolution_y = 1500, 900
world = bpy.data.worlds.new("w"); scene.world = world
lo = Vector([min(v.co[i] for v in ob.data.vertices) for i in range(3)])
hi = Vector([max(v.co[i] for v in ob.data.vertices) for i in range(3)])
c = (lo + hi) / 2
H = hi.z - lo.z
cam_data = bpy.data.cameras.new("c"); cam_data.type = 'ORTHO'; cam_data.ortho_scale = H * 1.1
cam = bpy.data.objects.new("c", cam_data); scene.collection.objects.link(cam); scene.camera = cam
import os
shots = []
for name, yaw in (("front", 0), ("side", 90), ("three", 35), ("back", 180)):
    a = math.radians(yaw)
    cam.location = c + Vector((math.sin(a) * 5, -math.cos(a) * 5, 0))
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.resolution_x, scene.render.resolution_y = 500, 900
    p = out.replace(".png", "_%s.png" % name)
    scene.render.filepath = p
    bpy.ops.render.render(write_still=True)
    shots.append(p)
print("PREVIEW", shots)
