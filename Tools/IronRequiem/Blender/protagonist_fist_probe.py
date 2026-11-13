import bpy,math,json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[3];out=root/'Tools/IronRequiem/Reports/Protagonist'
bpy.ops.wm.open_mainfile(filepath=str(root/'Tools/IronRequiem/Blender/Working/Protagonist/IR_Protagonist_Rigged.blend'))
arm=bpy.data.objects['IR_Rig'];arm.animation_data.action=None
for o in bpy.context.scene.objects:
 if o.type=='MESH':o.hide_render=not o.name=='Hand_R_LOD0'
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=8;sc.render.resolution_x=700;sc.render.resolution_y=650;sc.render.resolution_percentage=100;sc.world=bpy.data.worlds.new('Review');sc.world.color=(.2,.2,.2)
for loc,power in [((1,-3,3),500),((-3,0,2),700)]:bpy.ops.object.light_add(type='AREA',location=loc);bpy.context.object.data.energy=power;bpy.context.object.data.size=3
bpy.ops.object.camera_add();cam=bpy.context.object;sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=.28;target=Vector((-.42,0,.925));cam.location=target+Vector((-2,-3,.8));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
metrics=[]
for curl in [1.55]:
 for p in arm.pose.bones:p.rotation_euler=(0,0,0)
 for finger in ['Index','Middle','Ring','Little']:
  for joint,amount in enumerate((.45,.65,.3),1):arm.pose.bones[f'{finger}{joint}_R'].rotation_euler.z=-amount*curl
 arm.pose.bones['Thumb1_R'].rotation_euler.z=.8;arm.pose.bones['Thumb2_R'].rotation_euler.z=.5;arm.pose.bones['Thumb3_R'].rotation_euler.z=.2
 bpy.context.view_layer.update();sc.render.filepath=str(out/f'fist-closed-{curl}.png');bpy.ops.render.render(write_still=True)
 metrics.append({'curl':curl,'index_tip':list(arm.pose.bones['Index3_R'].tail),'wrist':list(arm.pose.bones['Hand_R'].head)})
print(json.dumps(metrics))
