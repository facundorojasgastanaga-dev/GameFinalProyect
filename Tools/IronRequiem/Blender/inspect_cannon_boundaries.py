import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[3];out=root/'Tools/IronRequiem/Reports/CannonBurr';result=[]
for lod in range(3):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(root/f'Assets/IronRequiem/Art/Enemies/T07Optimized/T07_LOD{lod}.fbx'),use_anim=False)
 obj=next(o for o in bpy.context.scene.objects if o.type=='MESH' and 'Cannon' in o.name);bm=bmesh.new();bm.from_mesh(obj.data);bm.verts.ensure_lookup_table()
 remaining={e for e in bm.edges if e.is_boundary};loops=[]
 while remaining:
  stack=[remaining.pop()];edges=[]
  while stack:
   e=stack.pop();edges.append(e)
   for v in e.verts:
    for other in v.link_edges:
     if other in remaining:remaining.remove(other);stack.append(other)
  verts=set(v for e in edges for v in e.verts);p=[v.co for v in verts];center=sum(p,Vector())/len(p)
  loops.append({'edges':len(edges),'center':list(center),'min':[min(v[i] for v in p) for i in range(3)],'max':[max(v[i] for v in p) for i in range(3)],'vertex_indices':[v.index for v in verts]})
 result.append({'lod':lod,'loops':loops})
(out/'boundary-loops.json').write_text(json.dumps(result,indent=2))
