import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[3];out=root/'Assets/IronRequiem/Art/Player/ProtagonistActions';out.mkdir(parents=True,exist_ok=True);report=root/'Tools/IronRequiem/Reports/ProtagonistActions'
bpy.ops.wm.open_mainfile(filepath=str(root/'Tools/IronRequiem/Blender/Working/Protagonist/IR_Protagonist_Rigged.blend'));arm=bpy.data.objects['IR_Rig'];arm.animation_data.action=None
for p in arm.pose.bones:p.rotation_mode='QUATERNION';p.rotation_quaternion=(1,0,0,0)
# Articulated closed left fist; fingertips fold onto palm, thumb crosses the index.
for finger in ['Index','Middle','Ring','Little']:
 for joint,amount in enumerate((.45,.65,.3),1):
  p=arm.pose.bones[f'{finger}{joint}_L'];p.rotation_mode='XYZ';p.rotation_euler.z=amount*1.55
bpy.ops.object.empty_add(location=(.424,-.045,.925));target=bpy.context.object;target.name='ThumbFistTarget';constraint=arm.pose.bones['Thumb3_L'].constraints.new('IK');constraint.target=target;constraint.chain_count=3;constraint.use_stretch=False;constraint.iterations=128
bpy.context.view_layer.update();evaluated=arm.evaluated_get(bpy.context.evaluated_depsgraph_get());rotations={}
for name in ['Thumb1_L','Thumb2_L','Thumb3_L']:
 p=arm.pose.bones[name];e=evaluated.pose.bones[name];basis=p.bone.convert_local_to_pose(e.matrix,p.bone.matrix_local,parent_matrix=e.parent.matrix,parent_matrix_local=p.parent.bone.matrix_local,invert=True);rotations[name]=basis.to_quaternion().copy()
arm.pose.bones['Thumb3_L'].constraints.remove(constraint);bpy.data.objects.remove(target,do_unlink=True)
for name,q in rotations.items():p=arm.pose.bones[name];p.rotation_mode='QUATERNION';p.rotation_quaternion=q
closed={p.name:p.rotation_quaternion.copy() if p.rotation_mode=='QUATERNION' else p.rotation_euler.to_quaternion() for p in arm.pose.bones}
# Drop unrelated actions from this derivative only; do not change the v01 source file.
for action in list(bpy.data.actions):bpy.data.actions.remove(action)
action=bpy.data.actions.new('IR_LeftFistClosed');action.use_fake_user=True;arm.animation_data.action=action
for p in arm.pose.bones:
 p.rotation_mode='QUATERNION';p.rotation_quaternion=closed[p.name]
 for frame in [1,13]:p.keyframe_insert('rotation_quaternion',frame=frame)
arm.animation_data.action=None
for p in arm.pose.bones:p.rotation_quaternion=(1,0,0,0)
# Actual lower-body surface from a copy, cut/capped at the hip; no head or torso renderer.
original=bpy.data.objects['Body_LOD0'];legs=original.copy();legs.data=original.data.copy();bpy.context.collection.objects.link(legs);legs.name='SlideLegs_LOD0';legs.data.name=legs.name
bm=bmesh.new();bm.from_mesh(legs.data);plane=legs.matrix_world.inverted()@Vector((0,0,.95));normal=legs.matrix_world.to_3x3().inverted()@Vector((0,0,1));bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),plane_co=plane,plane_no=normal,dist=1e-6,clear_outer=True,clear_inner=False)
faces=bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary],sides=0)['faces'];uv=bm.loops.layers.uv.active
for f in faces:
 f.smooth=False
 if uv:
  for loop in f.loops:loop[uv].uv=(.02,.02)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(legs.data);bm.free();legs.data.calc_loop_triangles()
fps=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.endswith('_FPS')]
for name,meshes,animate in [('IR_ActionHandPoses',fps,True),('IR_SlideLegs', [legs],False)]:
 for o in bpy.context.scene.objects:o.select_set(False)
 for o in meshes+[arm]:o.select_set(True)
 bpy.context.view_layer.objects.active=arm;bpy.ops.export_scene.fbx(filepath=str(out/(name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,use_mesh_modifiers=False,bake_anim=animate,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,apply_scale_options='FBX_SCALE_UNITS',path_mode='STRIP')
(report/'geometry.json').write_text(json.dumps({'bones':len(arm.data.bones),'fps_source_unchanged':True,'slide_triangles':len(legs.data.loop_triangles),'hip_cut_z_m':.95,'closed_thumb_tip_target':list((.424,-.045,.925)),'left_finger_bones':15},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(root/'Tools/IronRequiem/Blender/Working/Protagonist/IR_Protagonist_Actions.blend'));print('ACTION_ART_READY')
