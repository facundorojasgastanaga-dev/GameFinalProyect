import bpy, sys, json
from pathlib import Path
args = sys.argv[sys.argv.index('--')+1:]
src, out = args[0], args[1]
if src.endswith('.blend'):
    bpy.ops.wm.open_mainfile(filepath=src, load_ui=False)
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src, use_anim=True)
res = []
for o in bpy.data.objects:
    it = {'name': o.name, 'type': o.type, 'parent': o.parent.name if o.parent else None,
          'parent_type': o.parent_type, 'parent_bone': o.parent_bone,
          'loc': list(o.location), 'rot': list(o.rotation_euler), 'scale': list(o.scale),
          'world_loc': list(o.matrix_world.translation)}
    if o.type == 'MESH':
        me = o.data
        it.update(verts=len(me.vertices), faces=len(me.polygons), tris=sum(len(p.vertices)-2 for p in me.polygons),
                  vgroups=[g.name for g in o.vertex_groups], uv=[u.name for u in me.uv_layers],
                  mats=[m.name if m else None for m in me.materials],
                  mods=[(m.type, getattr(m, 'object', None) and m.object.name) for m in o.modifiers])
    if o.type == 'ARMATURE':
        it['bones'] = [{'name': b.name, 'parent': b.parent.name if b.parent else None,
                        'head': list(o.matrix_world @ b.head_local), 'tail': list(o.matrix_world @ b.tail_local)} for b in o.data.bones]
    res.append(it)
acts = [{'name': a.name, 'range': list(a.frame_range)} for a in bpy.data.actions]
Path(out).write_text(json.dumps({'source': src, 'objects': res, 'actions': acts}, indent=1))
print('INV', src, len(res))
