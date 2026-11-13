import bpy,json,math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[3];out=root/'Tools/IronRequiem/Reports/Protagonist'
bpy.ops.wm.open_mainfile(filepath=str(root/'Tools/IronRequiem/Blender/Working/Protagonist/IR_Protagonist_Rigged.blend'))
arm=bpy.data.objects['IR_Rig'];sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=12;sc.render.resolution_x=1000;sc.render.resolution_y=900;sc.render.resolution_percentage=100;sc.render.image_settings.file_format='PNG'
sc.world=bpy.data.worlds.new('InspectionWorld');sc.world.color=(.2,.2,.2)
meshes=[o for o in sc.objects if o.type=='MESH']
for o in meshes:o.hide_render=not o.name.endswith('LOD0')
bpy.ops.object.camera_add();cam=bpy.context.object;sc.camera=cam;cam.data.type='ORTHO'
for loc,power,size in [((2,-4,4),600,4),((-3,-1,2),400,3),((1,3,3),700,3)]:
 bpy.ops.object.light_add(type='AREA',location=loc);bpy.context.object.data.energy=power;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=size
checks=[]
for pose in ['IR_HandNormal','IR_HandFist','IR_HandGrip','stress_joints']:
 arm.animation_data.action=None
 for p in arm.pose.bones:p.rotation_euler=(0,0,0)
 if pose=='stress_joints':
  for side in ['L','R']:arm.pose.bones['Forearm_'+side].rotation_euler.x=.7;arm.pose.bones['Hand_'+side].rotation_euler.y=.35
 else:arm.animation_data.action=bpy.data.actions[pose];sc.frame_set(1)
 bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get();evaluated={}
 for o in meshes:
  e=o.evaluated_get(deps);m=e.to_mesh();evaluated[o.name]=[o.matrix_world@v.co for v in m.vertices]
  ratios=[]
  for edge in m.edges:
   i,j=edge.vertices;before=(o.data.vertices[i].co-o.data.vertices[j].co).length
   if before>.0005:ratios.append((m.vertices[i].co-m.vertices[j].co).length/before)
  checks.append({'pose':pose,'mesh':o.name,'edge_stretch_p99':sorted(ratios)[int(len(ratios)*.99)],'edge_stretch_max':max(ratios)})
  e.to_mesh_clear()
 if pose in ['IR_HandNormal','IR_HandFist']:
  target=Vector((-.42,0,.91));cam.location=target+Vector((-2,-3,.8));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.34;sc.render.filepath=str(out/(pose+'-hand.png'));bpy.ops.render.render(write_still=True)
 if pose=='IR_HandNormal':
  target=Vector((0,0,.95));cam.location=target+Vector((2,-5,1));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.1;sc.render.filepath=str(out/'rigged-body.png');bpy.ops.render.render(write_still=True)
(out/'deformation-probe.json').write_text(json.dumps(checks,indent=2))
print('POSE_QA_COMPLETE')
