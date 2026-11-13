import bpy,json
from pathlib import Path
from mathutils import Vector,kdtree
root=Path(__file__).resolve().parents[3];src=root/'Tools/IronRequiem/Exports/IR_Protagonist_Optimized_v01';out=root/'Tools/IronRequiem/Reports/Protagonist'
bpy.ops.wm.open_mainfile(filepath=str(root/'Tools/IronRequiem/Blender/Working/Protagonist/IR_Protagonist_Rigged.blend'))
arm=bpy.data.objects['IR_Rig'];land=json.loads((src/'Verification/geometry.json').read_text())['landmarks'];pairs=[]
for lod in range(3):
 for side in ['L','R']:
  normal=(Vector(land[side]['wrist'])-Vector(land[side]['elbow'])).normalized()
  for label,a,b,point in [('elbow',f'Body_LOD{lod}',f'Forearm_{side}_LOD{lod}',land[side]['elbow']),('wrist',f'Forearm_{side}_LOD{lod}',f'Hand_{side}_LOD{lod}',land[side]['wrist'])]:
   left=bpy.data.objects[a];right=bpy.data.objects[b];tree=kdtree.KDTree(len(left.data.vertices))
   for v in left.data.vertices:tree.insert(left.matrix_world@v.co,v.index)
   tree.balance();joint=[]
   for v in right.data.vertices:
    p=right.matrix_world@v.co
    if abs((p-Vector(point)).dot(normal))>2e-5:continue
    co,index,d=tree.find(p)
    if d<2e-5:joint.append((index,v.index))
   assert len(joint)>=3,(a,b,'unmatched cut');pairs.append((lod,side,label,left,right,joint))
checks=[]
for pose in ['normal','grip','stress','walk']:
 arm.animation_data.action=None
 for p in arm.pose.bones:p.rotation_euler=(0,0,0)
 if pose=='grip':arm.animation_data.action=bpy.data.actions['IR_HandGrip'];bpy.context.scene.frame_set(1)
 if pose=='walk':arm.animation_data.action=bpy.data.actions['IR_Walk'];bpy.context.scene.frame_set(11)
 if pose=='stress':
  for side in ['L','R']:arm.pose.bones['Forearm_'+side].rotation_euler.x=.8;arm.pose.bones['Hand_'+side].rotation_euler.y=.4
 bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
 for lod,side,label,left,right,joint in pairs:
  le=left.evaluated_get(deps);re=right.evaluated_get(deps);lm=le.to_mesh();rm=re.to_mesh();gap=max((left.matrix_world@lm.vertices[i].co-right.matrix_world@rm.vertices[j].co).length for i,j in joint)
  checks.append({'pose':pose,'lod':lod,'side':side,'joint':label,'paired_vertices':len(joint),'max_gap_m':gap});le.to_mesh_clear();re.to_mesh_clear()
assert max(c['max_gap_m'] for c in checks)<.0002,'Skinned seam opened'
(out/'seams.json').write_text(json.dumps(checks,indent=2));print('SHARED_JOINTS_VERIFIED',len(checks),'max gap',max(c['max_gap_m'] for c in checks))
