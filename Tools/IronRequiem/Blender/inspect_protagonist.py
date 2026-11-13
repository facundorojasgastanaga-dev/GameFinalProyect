import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[3];source=root/'Tools/IronRequiem/Exports/IR_Protagonist_Optimized_v01'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source/'IR_Protagonist_FPS_Parts.fbx'),use_anim=False)
result=[]
for o in bpy.context.scene.objects:
 item={'name':o.name,'type':o.type,'matrix':[list(row) for row in o.matrix_world]}
 if o.type=='MESH':
  item.update(verts=len(o.data.vertices),triangles=len(o.data.polygons),groups=len(o.vertex_groups),uv=len(o.data.uv_layers),bounds=[list(o.matrix_world@Vector(v)) for v in o.bound_box])
  if 'Hand_R' in o.name:
   coords=[o.matrix_world@v.co for v in o.data.vertices]
   item['bands']=[{'z':z,'n':len(points),'min':list(map(min,zip(*points))),'max':list(map(max,zip(*points)))} for z in [.80,.82,.84,.86,.88,.90,.92,.94,.96,.98,1.] if (points:=[tuple(p) for p in coords if abs(p.z-z)<.008])]
 result.append(item)
(root/'Tools/IronRequiem/Reports/Protagonist/fps-source-inspection.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result))
