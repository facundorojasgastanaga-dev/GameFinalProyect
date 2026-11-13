import bpy,math
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[3];O=R/'Tools/IronRequiem/Reports/NewRobotAnimation'
bpy.ops.wm.open_mainfile(filepath=str(R/'Tools/IronRequiem/Exports/IR_NewRobot_Animated_v01/IR_NewRobot_Animated.blend'))
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.eevee.taa_render_samples=16;scene.render.resolution_x=700;scene.render.resolution_y=800;scene.render.resolution_percentage=100;scene.world.color=(.1,.1,.1);scene.view_settings.view_transform='AgX';target=Vector((-.01,-.04,.43));rig=bpy.data.objects['NewRobotRig']
for xyz,power in [((-2,-3,4),900),((2,2,3),1200)]:
 bpy.ops.object.light_add(type='AREA',location=xyz);l=bpy.context.object;l.data.energy=power;l.data.size=3;l.rotation_euler=(target-l.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(-2,-.15,.55));scene.camera=bpy.context.object;scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=1.05;scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler()
for name,action,frame in [('pose-idle','IRN_Idle',1),('pose-walk','IRN_Walk',46),('pose-recoil','IRN_Fire',145)]:
 rig.animation_data.action=bpy.data.actions[action];scene.frame_set(frame);scene.render.filepath=str(O/(name+'.png'));bpy.ops.render.render(write_still=True)
