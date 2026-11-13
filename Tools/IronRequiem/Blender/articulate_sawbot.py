import bpy,bmesh,sys,json,math
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[3];SOURCE=ROOT/'Tools/IronRequiem/Exports/IR_SawBot_Optimized_v01';OUT=ROOT/'Assets/IronRequiem/Art/Enemies/SawBot';WORK=ROOT/'Tools/IronRequiem/Exports/IR_SawBot_Articulated_v01';REPORT=ROOT/'Tools/IronRequiem/Reports/SawBotGameplay';OUT.mkdir(parents=True,exist_ok=True);WORK.mkdir(parents=True,exist_ok=True);REPORT.mkdir(parents=True,exist_ok=True)
assert bpy.app.background and '--factory-startup' in sys.argv
assert not (WORK/'IR_SawBot_Articulated.blend').exists(),'Preserve delivered articulated copy'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'IR_SawBot_Editable.blend'))
originals={i:[o for o in bpy.data.collections['LOD'+str(i)+'_Editable'].objects if o.type=='MESH'] for i in range(3)}
for o in bpy.context.scene.objects:o.hide_render=True
pivots={'Body':Vector((0,.02,.32))}
for side,sign in [('L',1),('R',-1)]:
 pivots.update({'Wheel_'+side:Vector((sign*.197,.055,.145)),'UpperArm_'+side:Vector((sign*.225,.005,.49)),'Forearm_'+side:Vector((sign*.29,-.045,.39)),'GuideBar_'+side:Vector((sign*.367,-.078,.267)),'Chain_'+side:Vector((sign*.367,-.078,.267))})
planes=[(Vector((s*x,0,0)),Vector((1,0,0))) for s in [-1,1] for x in [.145,.225,.275]]+[(Vector((0,0,z)),Vector((0,0,1))) for z in [.235,.285,.30]]+[(Vector((0,-.115,0)),Vector((0,1,0)))]+[(pivots['Forearm_'+side],Vector((sign*.55,-.15,-.82)).normalized()) for side,sign in [('L',1),('R',-1)]]
def region(p):
 side='L' if p.x>0 else 'R';sign=1 if p.x>0 else -1
 if abs(p.x)>.225 and p.z>.30:return ('Forearm_' if (p-pivots['Forearm_'+side]).dot(Vector((sign*.55,-.15,-.82)))>0 else 'UpperArm_')+side
 if abs(p.x)>.275 and p.z>.235:return 'Forearm_'+side
 if abs(p.x)>.145 and p.z<.285 and p.y>-.115:return 'Wheel_'+side
 return 'Body'
def duplicate(o,name):
 c=o.copy();c.data=o.data.copy();bpy.context.collection.objects.link(c);c.hide_set(False);c.name=name;return c
def tris(o):o.data.calc_loop_triangles();return len(o.data.loop_triangles)
def apply_boolean(obj,tool,operation):
 bpy.ops.object.select_all(action='DESELECT');obj.hide_set(False);obj.select_set(True);bpy.context.view_layer.objects.active=obj;m=obj.modifiers.new('MechanicalSeparation','BOOLEAN');m.operation=operation;m.solver='EXACT';m.object=tool; m.use_self=False;bpy.ops.object.modifier_apply(modifier=m.name);obj.data.update()
def box(name,lo,hi,material):
 lo=Vector(lo);hi=Vector(hi);bpy.ops.mesh.primitive_cube_add(size=1,location=(lo+hi)*.5);o=bpy.context.object;o.name=name;o.scale=hi-lo;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(material);return o
def plane_split(o,co,no,positive):
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,plane_co=co,plane_no=no,clear_inner=positive,clear_outer=not positive);edges=[e for e in bm.edges if e.is_boundary];caps=bmesh.ops.holes_fill(bm,edges=edges,sides=0).get('faces',[]);uv=bm.loops.layers.uv.active
 for f in caps:
  for loop in f.loops:loop[uv].uv=cap_uv
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
result=[];all_parts=[]
for level in range(3):
 source=next(o for o in originals[level] if o.name=='Body_LOD'+str(level));remaining=duplicate(source,'Body');remaining.data.transform(remaining.matrix_world);remaining.matrix_world=Matrix.Identity(4);parts=[];material=remaining.data.materials[0];cap_uv=next(o for o in originals[level] if o.name.startswith('GuideBar_L')).data.uv_layers.active.data[0].uv.copy()
 for side,sign in [('L',1),('R',-1)]:
  xlo,xhi=(.145,1) if sign>0 else (-1,-.145);tool=box('WheelVolume',(xlo,-.115,-.1),(xhi,1,.285),material);wheel=duplicate(remaining,'Wheel_'+side);apply_boolean(wheel,tool,'INTERSECT');apply_boolean(remaining,tool,'DIFFERENCE');bpy.data.objects.remove(tool,do_unlink=True);parts.append(wheel)
 for side,sign in [('L',1),('R',-1)]:
  xlo,xhi=(.225,1) if sign>0 else (-1,-.225);tool=box('ArmVolume',(xlo,-1,.235),(xhi,1,1),material);xlo,xhi=(.224,.275) if sign>0 else (-.275,-.224);notch=box('KeepBodyPipe',(xlo,-1.1,.234),(xhi,1.1,.30),material);apply_boolean(tool,notch,'DIFFERENCE');bpy.data.objects.remove(notch,do_unlink=True)
  arm=duplicate(remaining,'UpperArm_'+side);apply_boolean(arm,tool,'INTERSECT');apply_boolean(remaining,tool,'DIFFERENCE');bpy.data.objects.remove(tool,do_unlink=True);forearm=duplicate(arm,'Forearm_'+side);plane_split(arm,pivots['Forearm_'+side],Vector((sign*.55,-.15,-.82)).normalized(),False);plane_split(forearm,pivots['Forearm_'+side],Vector((sign*.55,-.15,-.82)).normalized(),True);parts.extend([arm,forearm])
 parts.insert(0,remaining)
 for o in parts:
  name=o.name.split('.')[0];bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7);bmesh.ops.dissolve_degenerate(bm,dist=1e-7,edges=list(bm.edges));bmesh.ops.delete(bm,geom=[e for e in bm.edges if not e.link_faces],context='EDGES');bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.dissolve_degenerate(bm,dist=1e-7,edges=list(bm.edges));bmesh.ops.delete(bm,geom=[e for e in bm.edges if not e.link_faces],context='EDGES');bad=sum(not e.is_manifold for e in bm.edges);print('TOPOLOGY',name,level,[(len(e.link_faces),[list(v.co) for v in e.verts]) for e in bm.edges if not e.is_manifold],flush=True);assert len(bm.faces)>0 and bad==0,(name,level,bad);bm.to_mesh(o.data);bm.free();o.data.update();o.data.transform(Matrix.Translation(-pivots[name]));o.location=pivots[name]
 for prefix in ['GuideBar_L','GuideBar_R','Chain_L','Chain_R']:
  o=duplicate(next(o for o in originals[level] if o.name==prefix+'_LOD'+str(level)),prefix);parts.append(o) # Every LOD uses the same piece names and pivots. Unity creates one shared transform hierarchy.
 for o in parts:o.name=o.name.split('_LOD')[0]
 facing=bpy.data.objects.new('FacingProbe',None);bpy.context.collection.objects.link(facing);facing.location=(0,-1,0)
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts+[facing]:o.hide_set(False);o.select_set(True)
 path=OUT/f'IR_SawBot_Articulated_LOD{level}.fbx';bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',use_mesh_modifiers=False)
 result.append({'lod':level,'triangles':sum(tris(o) for o in parts),'parts':[{'name':o.name,'triangles':tris(o),'pivot':list(o.location)} for o in parts]});print(json.dumps(result[-1]),flush=True);bpy.data.objects.remove(facing,do_unlink=True);all_parts.extend(parts)
 for o in parts:o.name='LOD'+str(level)+'_'+o.name.split('.')[0];o.hide_set(True)
# Preserve an editable articulated copy without altering the previously delivered optimization.
for o in bpy.context.scene.objects:o.hide_render=True
for o in all_parts[:11]:o.hide_set(False);o.hide_render=False
bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.filepaths.file_preview_type='NONE';bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'IR_SawBot_Articulated.blend'));(REPORT/'geometry.json').write_text(json.dumps(result,indent=2));print('ARTICULATED_READY',flush=True)