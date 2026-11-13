"""Rigid 15-piece robot skeleton and first animation set. No source geometry edits."""
import bpy,json,math,hashlib,sys
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion
ROOT=Path(__file__).resolve().parents[3];SRC=ROOT/'Tools/IronRequiem/Exports/IR_NewRobot_Optimized_v01';OUT=ROOT/'Tools/IronRequiem/Exports/IR_NewRobot_Animated_v01';REPORT=ROOT/'Tools/IronRequiem/Reports/NewRobotAnimation'
assert bpy.app.background and '--factory-startup' in sys.argv
assert not (OUT/'IR_NewRobot_Animated.fbx').exists() or '--revise-draft' in sys.argv,'Preserve delivered assets'
spec=json.loads((SRC/'Verification/geometry.json').read_text());before={p.name:hashlib.file_digest(p.open('rb'),'sha256').hexdigest() for p in SRC.glob('*.fbx')}
bpy.ops.wm.open_mainfile(filepath=str(SRC/'IR_NewRobot_Editable.blend'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name!='Source_Reference_50060'];assert len(meshes)==45
for o in list(bpy.context.scene.objects):
 if o not in meshes:bpy.data.objects.remove(o,do_unlink=True)
for o in meshes:
 o.hide_set(False);o.hide_render=False;world=o.matrix_world.copy();o.parent=None;o.matrix_world=world
pivots={p:Vector(v['pivot_m']) for p,v in spec['parts'].items()};parents={'Pelvis':'Root','Torso':'Pelvis','Backpack':'Torso'}
for side in ('L','R'):
 parents.update({'Shoulder_'+side:'Torso','Forearm_'+side:'Shoulder_'+side,'Pincer_'+side:'Forearm_'+side,'Thigh_'+side:'Pelvis','Shin_'+side:'Thigh_'+side,'Foot_'+side:'Shin_'+side})
order=['Root','Pelvis','Torso','Backpack']+[kind+'_'+side for side in ('L','R') for kind in ('Shoulder','Forearm','Pincer','Thigh','Shin','Foot')]
heads={'Root':Vector((0,0,0)),**pivots};tails={n:heads[n]+Vector((0,0,.04)) for n in order}
tails['Pelvis']=heads['Torso'];tails['Torso']=Vector((-.01,0,.77))
for side in ('L','R'):
 for a,b in [('Shoulder','Forearm'),('Forearm','Pincer'),('Thigh','Shin'),('Shin','Foot')]:tails[a+'_'+side]=heads[b+'_'+side]
 tails['Foot_'+side]=heads['Foot_'+side]+Vector((0,-.12,-.035));tails['Pincer_'+side]=heads['Pincer_'+side]+Vector((0,-.18,0))
arm=bpy.data.armatures.new('IR_NewRobot_RigidSkeleton');rig=bpy.data.objects.new('NewRobotRig',arm);bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for n in order:
 b=arm.edit_bones.new(n);b.head=heads[n];b.tail=tails[n];b.use_deform=True
 if n in parents:b.parent=arm.edit_bones[parents[n]]
bpy.ops.object.mode_set(mode='OBJECT');rest={n:arm.bones[n].matrix_local.copy() for n in order};restq={n:m.to_quaternion() for n,m in rest.items()}
records=[]
for o in meshes:
 part=o.name.rsplit('_LOD',1)[0];uv=[tuple(v.uv) for v in o.data.uv_layers.active.data];o.data.calc_loop_triangles();tri=len(o.data.loop_triangles)
 group=o.vertex_groups.new(name=part);group.add(list(range(len(o.data.vertices))),1,'REPLACE');m=o.modifiers.new('RigidSharedSkeleton','ARMATURE');m.object=rig;world=o.matrix_world.copy();o.parent=rig;o.matrix_world=world
 assert uv==[tuple(v.uv) for v in o.data.uv_layers.active.data];assert all(len(v.groups)==1 and abs(v.groups[0].weight-1)<1e-7 for v in o.data.vertices)
 records.append({'mesh':o.name,'triangles':tri,'bone':part,'rigid_weight':1,'uv_unchanged':True})
# Presentation socket on the upper front tube, not the lower open hook. No weapon logic is added.
# At rest the part extends to y=-0.373; local +Z points forward (-Y Blender).
socket=bpy.data.objects.new('WeaponMuzzle_R',None);bpy.context.collection.objects.link(socket);socket.empty_display_type='ARROWS';socket.empty_display_size=.04
world=Matrix.Translation(Vector((-.427,-.374,.682)))@Vector((0,-1,0)).to_track_quat('Z','Y').to_matrix().to_4x4();socket.parent=rig;socket.parent_type='BONE';socket.parent_bone='Pincer_R';socket.matrix_world=world
scene=bpy.context.scene;scene.render.fps=60;actions=[];checks=[]
def qrot(axis,degrees):return Quaternion(Vector(axis),math.radians(degrees))
def identity():
 for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4);p.rotation_mode='QUATERNION'
 bpy.context.view_layer.update()
def head_now(n):
 parent=parents.get(n)
 return rig.pose.bones[parent].matrix@rest[parent].inverted()@heads[n] if parent else heads[n]
def pose(n,delta=Quaternion(),point=None):
 p=rig.pose.bones[n];p.matrix=Matrix.Translation(head_now(n) if point is None else point)@(delta@restq[n]).to_matrix().to_4x4();bpy.context.view_layer.update()
def leg(side,target,pitch=0):
 thigh='Thigh_'+side;shin='Shin_'+side;foot='Foot_'+side;hip=head_now(thigh);l1=(heads[shin]-heads[thigh]).length;l2=(heads[foot]-heads[shin]).length;v=target-hip;d=min(max(v.length,abs(l1-l2)+1e-5),l1+l2-1e-5);direction=v.normalized();a=(l1*l1-l2*l2+d*d)/(2*d);h=math.sqrt(max(0,l1*l1-a*a));bend=Vector((0,1,0));bend=(bend-direction*bend.dot(direction)).normalized();knee=hip+direction*a+bend*h
 pose(thigh,(heads[shin]-heads[thigh]).rotation_difference(knee-hip),hip);pose(shin,(heads[foot]-heads[shin]).rotation_difference(target-knee),knee);pose(foot,qrot((1,0,0),pitch),target)
def gait(u):
 duty=.62;step=.09
 if u<duty:return -step+2*step*u/duty,0,0,True
 a=(u-duty)/(1-duty);ease=a*a*(3-2*a);return step-2*step*ease,math.sin(math.pi*a)**2*.038,-7*math.sin(math.pi*a),False
period=1.0;source_speed=.18/(.62*period)
clipdefs=[('IRN_Idle',2,True),('IRN_Walk',period,True),('IRN_TurnLeft',1,True),('IRN_TurnRight',1,True),('IRN_PrepareFire',.6,False),('IRN_Fire',2.4,False),('IRN_RecoverFire',2,False)]
for name,duration,loop in clipdefs:
 frames=round(duration*60);duration=frames/60;action=bpy.data.actions.new(name);action.use_fake_user=True;rig.animation_data_create().action=action;actions.append(action)
 samples=[]
 for frame in range(frames+1):
  scene.frame_set(frame+1);u=frame/frames;t=frame/60;phase=2*math.pi*u;identity();pelvis_delta=Vector((0,0,0));pelvis_yaw=0
  if name=='IRN_Idle':
   pelvis_delta.z=.0015*math.sin(phase);pose('Pelvis',point=heads['Pelvis']+pelvis_delta);pose('Torso',qrot((1,0,0),math.sin(phase)*.45))
  elif name=='IRN_Walk':
   pelvis_delta=Vector((.006*math.sin(phase),0,-.018*math.cos(2*phase)));pelvis_yaw=1.8*math.sin(phase)
   pose('Pelvis',qrot((0,0,1),pelvis_yaw),heads['Pelvis']+pelvis_delta);pose('Torso',qrot((0,0,1),-pelvis_yaw*.65)@qrot((1,0,0),.7*math.sin(2*phase)))
   for side,offset in [('L',0),('R',.5)]:
    y,lift,pitch,stance=gait((u+offset)%1);target=heads['Foot_'+side]+Vector((0,y,lift));leg(side,target,pitch);pose('Shoulder_'+side,qrot((1,0,0),-.6*math.sin(2*phase)))
    if frame<frames:samples.append({'t':t,'side':side,'stance':stance,'foot':list(rig.pose.bones['Foot_'+side].matrix.translation)})
  elif name.startswith('IRN_Turn'):
   sign=1 if name.endswith('Left') else -1;pose('Pelvis',qrot((0,0,1),sign*2.5*math.sin(phase)),heads['Pelvis']+Vector((0,0,-.005*(1-math.cos(2*phase)))));pose('Torso',qrot((0,0,1),sign*4*math.sin(phase)))
   for side,offset in [('L',0),('R',.5)]:
    p=(u+offset)%1;lift=.025*math.sin(math.pi*p)**4;target=heads['Foot_'+side]+Vector((0,0,lift));leg(side,target)
  else:
   # User reference: subtle bilateral arm/barrel lift and short rearward travel.
   # Slow presentation timing follows the stationary first seconds of the second video.
   if name=='IRN_PrepareFire':kick=0
   elif name=='IRN_Fire':kick=u*u*(3-2*u)
   else:kick=1-u*u*(3-2*u)
   pose('Pelvis');pose('Torso',qrot((1,0,0),-.8*kick))
   for side in ('L','R'):
    pose('Shoulder_'+side,qrot((1,0,0),-3.5*kick));pose('Forearm_'+side,qrot((1,0,0),-7.5*kick));pose('Pincer_'+side,qrot((1,0,0),-7.5*kick),point=head_now('Pincer_'+side)+Vector((0,.006*kick,0)))
  # Idle uses the same fixed sole positions while the pelvis breathes.
  if name=='IRN_Idle':
   for side in ('L','R'):leg(side,heads['Foot_'+side])
  for p in rig.pose.bones:
   p.keyframe_insert('rotation_quaternion',frame=frame+1);p.keyframe_insert('location',frame=frame+1);p.keyframe_insert('scale',frame=frame+1)
  if frame in {0,frames//4,frames//2,frames}:
   deps=bpy.context.evaluated_depsgraph_get()
   for o in meshes:
    evaluated=o.evaluated_get(deps);m=evaluated.to_mesh();assert len(m.vertices)==len(o.data.vertices);assert all(all(math.isfinite(c) for c in v.co) for v in m.vertices);evaluated.to_mesh_clear()
  checks.append({'clip':name,'duration':duration,'loop':loop}) if frame==0 else None
 if samples:(REPORT/'walk-foot-contacts.json').write_text(json.dumps({'period_s':period,'reference':'Video 0.4–2.0s, march and upper-body steadiness adapted to supplied robot proportions','stride_speed_source_m_s':source_speed,'preview_height_m':3,'preview_walk_speed_m_s':source_speed*3/spec['height_m'],'samples':samples},indent=2))
 rig.animation_data.action=None
identity();scene.frame_set(1)
# Ensure all pieces are exported at rest with their topology and assembly exactly retained.
for o in bpy.context.scene.objects:o.select_set(False)
for o in meshes+[rig,socket]:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(OUT/'IR_NewRobot_Animated.fbx'),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,use_mesh_modifiers=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0,path_mode='STRIP')
for o in meshes:
 if not o.name.endswith('_LOD0'):o.hide_set(True);o.hide_render=True
rig.animation_data.action=actions[0];scene.frame_start=1;scene.frame_end=120;scene.frame_set(1)
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':area.spaces.active.region_3d.view_location=Vector((0,0,.43));area.spaces.active.region_3d.view_distance=1.7
bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.filepaths.file_preview_type='NONE';bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'IR_NewRobot_Animated.blend'))
for p in SRC.glob('*.fbx'):assert hashlib.file_digest(p.open('rb'),'sha256').hexdigest()==before[p.name]
result={'rig_type':'Rigid parts, one bone influence per vertex; no plate deformation','bones':len(arm.bones),'parts':records,'clips':checks,'root_motion':False,'source_height_m':spec['height_m'],'presentation_height_m':3,'preview_walk_speed_m_s':source_speed*3/spec['height_m'],'muzzle':'Right upper front tube presentation socket, +Z outward. No WeaponController/AI attached.','source_fbx_preserved':True,'reference_discharge_visible':True,'reference_shot_note':'Subtle arm/barrel rise and return from second reference, first 0–7 s; no flash/projectile in reference. Shot role requested explicitly by user.'}
(REPORT/'rig.json').write_text(json.dumps(result,indent=2));print('NEW_ROBOT_RIG_READY',len(arm.bones),len(meshes),len(actions),flush=True)
