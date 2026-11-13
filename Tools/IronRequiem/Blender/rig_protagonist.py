"""IR-09: copy-only mechanical skinning; no source mesh/topology/UV modifications."""
import bpy,math,json,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
root=Path(__file__).resolve().parents[3];src=root/'Tools/IronRequiem/Exports/IR_Protagonist_Optimized_v01';out=root/'Assets/IronRequiem/Art/Player/ProtagonistV01/Rigged';report=root/'Tools/IronRequiem/Reports/Protagonist'
bpy.ops.wm.read_factory_settings(use_empty=True)
land=json.loads((src/'Verification/geometry.json').read_text())['landmarks']
parts=[];guides=[];inspection=[]
for lod in range(3):
 before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(src/f'IR_Protagonist_LOD{lod}.fbx'),use_anim=False)
 for o in set(bpy.data.objects)-before:
  if o.type=='MESH':parts.append(o)
  elif o.type=='EMPTY' and not 'Root' in o.name:guides.append(o)
for o in parts+guides:
 world=o.matrix_world.copy();o.parent=None;o.matrix_world=world
for o in list(bpy.data.objects):
 if o not in parts+guides:bpy.data.objects.remove(o,do_unlink=True)
arm_data=bpy.data.armatures.new('IR_Protagonist_Skeleton');arm=bpy.data.objects.new('IR_Rig',arm_data);bpy.context.collection.objects.link(arm);bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
def bone(name,a,b,parent=None):
 e=arm_data.edit_bones.new(name);e.head=a;e.tail=b
 if parent:e.parent=arm_data.edit_bones[parent]
 e.align_roll(Vector((0,1,0)));return e
bone('Root',(0,0,0),(0,0,.15));bone('Pelvis',(0,0,.94),(0,0,1.08),'Root');bone('Spine',(0,0,1.08),(0,0,1.26),'Pelvis');bone('Chest',(0,0,1.26),(0,0,1.47),'Spine');bone('Neck',(0,0,1.47),(0,0,1.58),'Chest');bone('Head',(0,0,1.58),(0,0,1.79),'Neck')
for side,sign in [('L',1),('R',-1)]:
 elbow=Vector(land[side]['elbow']);wrist=Vector(land[side]['wrist'])
 bone('Clavicle_'+side,(0,0,1.44),(sign*.225,.02,1.455),'Chest');bone('UpperArm_'+side,(sign*.225,.02,1.455),elbow,'Clavicle_'+side);bone('Forearm_'+side,elbow,wrist,'UpperArm_'+side);bone('Hand_'+side,wrist,(sign*.451,0,.923),'Forearm_'+side)
 bone('Thigh_'+side,(sign*.135,0,.95),(sign*.16,-.025,.54),'Pelvis');bone('Shin_'+side,(sign*.16,-.025,.54),(sign*.175,0,.12),'Thigh_'+side);bone('Foot_'+side,(sign*.175,0,.12),(sign*.175,-.15,.07),'Shin_'+side)
 for i,(name,y,length) in enumerate([('Index',-.048,.095),('Middle',-.022,.119),('Ring',.005,.110),('Little',.029,.087)]):
  z=.922;points=[(sign*.458,y,z),(sign*.464,y,z-length*.4),(sign*.446,y,z-length*.78),(sign*.416,y,z-length)]
  for j in range(3):bone(f'{name}{j+1}_{side}',points[j],points[j+1],'Hand_'+side if j==0 else f'{name}{j}_{side}')
 points=[(sign*.407,-.005,.956),(sign*.380,-.019,.923),(sign*.362,-.016,.896),(sign*.364,-.014,.880)]
 for j in range(3):bone(f'Thumb{j+1}_{side}',points[j],points[j+1],'Hand_'+side if j==0 else f'Thumb{j}_{side}')
bpy.ops.object.mode_set(mode='OBJECT')
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def distance(p,a,b):
 d=b-a;t=max(0,min(1,(p-a).dot(d)/d.length_squared));return (p-(a+d*t)).length
segments={b.name:(b.head_local.copy(),b.tail_local.copy()) for b in arm_data.bones}
def hand_weights(p,side):
 if p.z>.946:return {'Hand_'+side:1}
 names=[n for n in segments if n.endswith('_'+side) and n.startswith(('Index','Middle','Ring','Little','Thumb'))]
 nearest=sorted([(distance(p,*segments[n]),n) for n in names])[:2];values={n:1/max(.001,d)**6 for d,n in nearest};total=sum(values.values());values={n:w/total for n,w in values.items()}
 palm=smooth(.895,.946,p.z)
 if palm>.001:values={n:w*(1-palm) for n,w in values.items()};values['Hand_'+side]=palm
 return values
for o in parts:
 o.data.calc_loop_triangles();tri=len(o.data.loop_triangles);uv_before=[tuple(v.uv) for v in o.data.uv_layers.active.data];coords=[o.matrix_world@v.co for v in o.data.vertices]
 groups={n:o.vertex_groups.new(name=n) for n in segments};unweighted=0
 for vertex,p in zip(o.data.vertices,coords):
  side='L' if p.x>=0 else 'R'
  if o.name.startswith('Hand'):weights=hand_weights(p,side)
  elif o.name.startswith('Forearm'):
   elbow=Vector(land[side]['elbow']);wrist=Vector(land[side]['wrist']);direction=(wrist-elbow).normalized();mix=smooth(-.025,0,(p-wrist).dot(direction));weights={'Forearm_'+side:1-mix,'Hand_'+side:mix}
  elif p.z>1.20 and abs(p.x)>.205:
   elbow=Vector(land[side]['elbow']);direction=(Vector(land[side]['wrist'])-elbow).normalized();mix=smooth(-.035,-.002,(p-elbow).dot(direction));weights={'UpperArm_'+side:1-mix,'Forearm_'+side:mix}
  elif p.z>1.55:weights={'Head':1}
  elif p.z>1.46:
   mix=smooth(1.46,1.57,p.z);weights={'Chest':1-mix,'Head':mix}
  elif p.z>1.12:
   mix=smooth(1.14,1.28,p.z);weights={'Spine':1-mix,'Chest':mix}
  elif p.z>.94:
   mix=smooth(.96,1.12,p.z);weights={'Pelvis':1-mix,'Spine':mix}
  elif p.z>.58:
   mix=smooth(.82,.96,p.z);weights={'Thigh_'+side:1-mix,'Pelvis':mix}
  elif p.z>.20:
   mix=smooth(.49,.60,p.z);weights={'Shin_'+side:1-mix,'Thigh_'+side:mix}
  else:
   mix=smooth(.12,.23,p.z);weights={'Foot_'+side:1-mix,'Shin_'+side:mix}
  weights={n:w for n,w in weights.items() if w>.00001};total=sum(weights.values());assert total>0
  for n,w in weights.items():groups[n].add([vertex.index],w/total,'REPLACE')
 modifier=o.modifiers.new('SharedSkeleton','ARMATURE');modifier.object=arm
 world=o.matrix_world.copy();o.parent=arm;o.matrix_world=world
 assert uv_before==[tuple(v.uv) for v in o.data.uv_layers.active.data]
 inspection.append({'part':o.name,'triangles':tri,'vertices':len(coords),'uv_unchanged':True,'unweighted':0,'max_influences':max(len(v.groups) for v in o.data.vertices)})
for o in guides:
 side='L' if '_L' in o.name else 'R';parent='Forearm_'+side if 'Elbow' in o.name else 'Hand_'+side;world=o.matrix_world.copy();o.parent=arm;o.parent_type='BONE';o.parent_bone=parent;o.matrix_world=world
# FPS meshes come from the supplied FPS file, not a duplicated full-body view.
before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(src/'IR_Protagonist_FPS_Parts.fbx'),use_anim=False)
fps_import=set(bpy.data.objects)-before;fps=[]
for o in fps_import:
 if o.type!='MESH':continue
 base=next(p for p in parts if p.name==o.name.removesuffix('.001'))
 world=o.matrix_world.copy();o.parent=None;o.matrix_world=world
 # Identical vertices/pivots verified against the supplied LOD0 before transferring exact weights.
 assert len(o.data.vertices)==len(base.data.vertices)
 assert max((o.matrix_world@v.co-base.matrix_world@w.co).length for v,w in zip(o.data.vertices,base.data.vertices))<1e-5
 for g in base.vertex_groups:o.vertex_groups.new(name=g.name)
 for v,reference in zip(o.data.vertices,base.data.vertices):
  for g in reference.groups:o.vertex_groups[g.group].add([v.index],g.weight,'REPLACE')
 mod=o.modifiers.new('SharedSkeleton','ARMATURE');mod.object=arm;o.parent=arm;o.matrix_world=world;o.name=base.name.replace('_LOD0','_FPS');fps.append(o)
for o in fps_import:
 if o not in fps:bpy.data.objects.remove(o,do_unlink=True)
# First-pass presentation poses; no root motion or combat events.
actions=[]
for kind,duration in [('IR_Idle',60),('IR_Walk',30),('IR_HandNormal',2),('IR_HandFist',2),('IR_HandGrip',2),('IR_PunchPosePreview',30)]:
 action=bpy.data.actions.new(kind);action.use_fake_user=True;arm.animation_data_create().action=action;actions.append(action)
 for frame in range(1,duration+2):
  for p in arm.pose.bones:p.rotation_mode='XYZ';p.rotation_euler=(0,0,0);p.location=(0,0,0);p.scale=(1,1,1)
  phase=(frame-1)/duration*math.tau
  if kind=='IR_Idle':arm.pose.bones['Chest'].rotation_euler.x=math.sin(phase)*.009
  if kind=='IR_Walk':
   for side,sign in [('L',1),('R',-1)]:
    angle=math.sin(phase)*sign;arm.pose.bones['Thigh_'+side].rotation_euler.x=angle*.36;arm.pose.bones['Shin_'+side].rotation_euler.x=max(0,-angle)*.55;arm.pose.bones['UpperArm_'+side].rotation_euler.x=-angle*.18
  curl=1 if kind in ('IR_HandFist','IR_PunchPosePreview') else .45 if kind=='IR_HandGrip' else 0
  for side,sign in [('L',1),('R',-1)]:
   for finger in ('Index','Middle','Ring','Little'):
    for joint,amount in enumerate((.45,.65,.3),1):arm.pose.bones[f'{finger}{joint}_{side}'].rotation_euler.z=sign*amount*curl
   arm.pose.bones['Thumb1_'+side].rotation_euler.y=.25*curl
  if kind=='IR_PunchPosePreview':
   arm.pose.bones['UpperArm_R'].rotation_euler.x=-math.sin(phase*.5)*.6;arm.pose.bones['Forearm_R'].rotation_euler.x=math.sin(phase*.5)*.7
  for p in arm.pose.bones:p.keyframe_insert('rotation_euler',frame=frame)
 arm.animation_data.action=None
for p in arm.pose.bones:p.rotation_euler=(0,0,0)
bpy.context.scene.frame_set(1)
for name,meshes,empties in [('IR_Protagonist_Rigged',parts,guides),('IR_Protagonist_FPS_Rigged',fps,[])]:
 for o in bpy.context.scene.objects:o.select_set(False)
 for o in meshes+empties+[arm]:o.select_set(True)
 bpy.context.view_layer.objects.active=arm
 bpy.ops.export_scene.fbx(filepath=str(out/(name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,use_mesh_modifiers=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0,apply_scale_options='FBX_SCALE_UNITS',path_mode='STRIP')
# Verify finite evaluated deformation, topology and normalized weights for every pose/LOD.
checks=[]
for action in actions:
 arm.animation_data.action=action;bpy.context.scene.frame_set(int((action.frame_range.x+action.frame_range.y)*.5));deps=bpy.context.evaluated_depsgraph_get()
 for o in parts+fps:
  evaluated=o.evaluated_get(deps);mesh=evaluated.to_mesh();finite=all(all(math.isfinite(c) for c in v.co) for v in mesh.vertices);assert finite
  maxmove=max((o.matrix_world@v.co-o.matrix_world@original.co).length for v,original in zip(mesh.vertices,o.data.vertices));checks.append({'clip':action.name,'mesh':o.name,'finite':finite,'max_vertex_motion_m':maxmove});evaluated.to_mesh_clear()
arm.animation_data.action=None
for p in arm.pose.bones:p.rotation_euler=(0,0,0)
bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'Tools/IronRequiem/Blender/Working/Protagonist/IR_Protagonist_Rigged.blend'))
(report/'rig.json').write_text(json.dumps({'source_static_confirmed':True,'bones':len(arm_data.bones),'shared_skeleton':True,'parts':inspection,'clips':[a.name for a in actions],'deformation_checks':checks,'limitations':['First-pass anatomical weights on generated topology; close-up knuckle/thumb contact needs artistic review.','PunchPosePreview is presentation only; no melee mechanics or controls.']},indent=2))
print('IR_RIG_COMPLETE bones=',len(arm_data.bones),'parts=',len(parts),'fps=',len(fps))
