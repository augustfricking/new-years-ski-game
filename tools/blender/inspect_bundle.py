"""Lists what is in a .blend: objects, sizes, vertex counts, UVs, materials, shape keys.
Run: blender --background <file.blend> --python tools/blender/inspect_bundle.py
"""
import bpy

print("=== OBJECTS ===")
for ob in sorted(bpy.data.objects, key=lambda o: o.name):
    if ob.type != 'MESH':
        print(f"{ob.name:44s} {ob.type}")
        continue
    me = ob.data
    d = ob.dimensions
    mods = ",".join(m.type for m in ob.modifiers)
    keys = len(me.shape_keys.key_blocks) if me.shape_keys else 0
    asset = "asset" if ob.asset_data else ""
    print(f"{ob.name:44s} v={len(me.vertices):6d} f={len(me.polygons):6d} dim=({d.x:.2f},{d.y:.2f},{d.z:.2f}) "
          f"loc=({ob.location.x:.2f},{ob.location.y:.2f},{ob.location.z:.2f}) uv={len(me.uv_layers)} keys={keys} mods=[{mods}] {asset}")
print("=== COLLECTIONS ===")
for c in bpy.data.collections:
    print(c.name, [o.name for o in c.objects][:12])
