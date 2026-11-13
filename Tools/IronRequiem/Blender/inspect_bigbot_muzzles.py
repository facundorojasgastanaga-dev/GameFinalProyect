import bpy,json
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[3];S=R/'Tools/IronRequiem/Exports/IR_NewRobot_Optimized_v01/IR_NewRobot_LOD0.fbx'
for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(S),use_anim=False)
result={}
for side,key in [('R','right'),('L','left')]:
 o=bpy.data.objects['Pincer_'+side+'_LOD0'];points=[o.matrix_world@v.co for v in o.data.vertices];front=min(v.y for v in points if v.z>.655)
 faces=[]
 for face in o.data.polygons:
  vs=[points[i] for i in face.vertices];co=sum(vs,Vector())/len(vs);normal=o.matrix_world.to_3x3()@face.normal
  if co.z>.655 and co.y<front+.024 and normal.y<-.4:faces.append((co,face.area))
 assert faces,side
 total=sum(a for _,a in faces);center=sum((c*a for c,a in faces),Vector())/total;center.y=front-.003
 result[key]={'x':float(-center.x),'y':float(center.z),'z':float(-center.y)}
result['note']='Unity model-space coordinates; upper front tube face centroid, 3 mm source-space forward clearance; original mesh unchanged.'
(R/'Tools/IronRequiem/Reports/BigBotCombat/muzzle-fit.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
