import bpy
for o in sorted(bpy.data.objects, key=lambda o: (o.type, o.name)):
    extra = ''
    if o.type == 'MESH':
        extra = f" tris={sum(len(p.vertices)-2 for p in o.data.polygons)} groups={len(o.vertex_groups)} mats={[m.name if m else None for m in o.data.materials]}"
    if o.type == 'ARMATURE':
        extra = f" bones={len(o.data.bones)}"
    print(o.type, o.name, 'parent=', o.parent.name if o.parent else None, 'scale=', tuple(round(v,4) for v in o.scale), 'hide=', o.hide_get(), extra)
print('collections', [c.name for c in bpy.data.collections])
