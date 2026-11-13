"""Cut actual pectoral/shoulder shells, cap both sides and add internal supports in copies."""
import bpy,bmesh,json,hashlib,math
from pathlib import Path
from mathutils import Vector
from mathutils.kdtree import KDTree
r=Path(__file__).resolve().parents[3];out=r/'Assets/IronRequiem/Art/Enemies/T07Destructible';out.mkdir(parents=True,exist_ok=True)
report=r/'Tools/IronRequiem/Reports/T07Destruction';report.mkdir(parents=True,exist_ok=True)
source=r/'Tools/IronRequiem/Blender/Working/T07Animated/T07_SharedRig.blend'
source_hash=hashlib.file_digest(source.open('rb'),'sha256').hexdigest()
assert not (out/'T07_ArmorRig.fbx').exists(),'Never overwrite authored output without inspecting it'
bpy.ops.wm.open_mainfile(filepath=str(source));rig=bpy.data.objects['T07Rig'];originals=[bpy.data.objects[f'Body_LOD{i}'] for i in range(3)]
spec=[('Pectoral_L',(-.225,-.25,1.31),(-.025,-.105,1.51),'Chest',1),('Shoulder_R',(.255,-.17,1.39),(.49,.16,1.67),'UpperArm_R',1),('Pectoral_R',(.025,-.25,1.31),(.225,-.105,1.51),'Chest',2),('Shoulder_L',(-.49,-.17,1.39),(-.245,.16,1.67),'UpperArm_L',2)]
inner=bpy.data.materials.new('ExposedMechanism');inner.diffuse_color=(.08,.11,.13,1)
shell=bpy.data.materials.new('DetachedArmor');shell.diffuse_color=(.32,.38,.42,1)
cutters=[]
for name,a,b,bone,stage in spec:
 bpy.ops.mesh.primitive_cube_add(size=1,location=(Vector(a)+Vector(b))*.5);c=bpy.context.object;c.name='Cut_'+name;c.scale=Vector(b)-Vector(a)
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);c.data.materials.append(inner);cutters.append(c)
outputs=[];records=[]
def activate(obj):
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
def boolean(obj,cutter,operation):
 activate(obj);m=obj.modifiers.new('SeparateAlongArmorSeams','BOOLEAN');m.operation=operation;m.solver='EXACT';m.object=cutter;m.material_mode='TRANSFER'
 bpy.ops.object.modifier_apply(modifier=m.name)
def cap_and_weights(obj,bone=None,reference=None):
 bm=bmesh.new();bm.from_mesh(obj.data);edges=[e for e in bm.edges if e.is_boundary]
 if edges:
  result=bmesh.ops.holes_fill(bm,edges=edges,sides=0)
  for face in result.get('faces',[]):face.material_index=len(obj.data.materials)-1
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free();obj.data.update()
 if bone:
  obj.vertex_groups.clear();g=obj.vertex_groups.new(name=bone);g.add(list(range(len(obj.data.vertices))),1,'REPLACE')
 else:
  for v in obj.data.vertices:
   groups=sorted([(g.group,g.weight) for g in v.groups if g.weight>1e-6],key=lambda a:-a[1])[:2]
   if not groups:
    _,idx,_=reference['tree'].find(v.co);groups=reference['weights'][idx]
   total=sum(w for _,w in groups)
   for group in obj.vertex_groups:group.remove([v.index])
   for index,weight in groups:obj.vertex_groups[index].add([v.index],weight/total,'REPLACE')
 obj.data.calc_loop_triangles();check=bmesh.new();check.from_mesh(obj.data)
 result={'name':obj.name,'vertices':len(obj.data.vertices),'triangles':len(obj.data.loop_triangles),'boundary_edges':sum(e.is_boundary for e in check.edges),'nonmanifold_edges':sum(not e.is_manifold for e in check.edges),'uv_layers':len(obj.data.uv_layers),'weighted_vertices':sum(bool(v.groups) for v in obj.data.vertices)};check.free();records.append(result)
def rib(body,bone,location,radius,depth,lod):
 bpy.ops.mesh.primitive_cylinder_add(vertices=[10,8,6][lod],radius=radius,depth=depth,location=location,rotation=(math.pi/2,0,0));obj=bpy.context.object
 obj.data.materials.append(inner);obj.vertex_groups.new(name=bone).add(list(range(len(obj.data.vertices))),1,'REPLACE');activate(obj);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 activate(body);obj.select_set(True);bpy.ops.object.join()
for lod,body in enumerate(originals):
 reference={'tree':KDTree(len(body.data.vertices)),'weights':{}}
 for v in body.data.vertices:reference['tree'].insert(v.co,v.index);reference['weights'][v.index]=[(g.group,g.weight) for g in v.groups]
 reference['tree'].balance();body.data.materials.append(inner)
 for (name,a,b,bone,stage),c in zip(spec,cutters):
  plate=body.copy();plate.data=body.data.copy();bpy.context.collection.objects.link(plate);plate.name=f'Armor_{name}_LOD{lod}'
  boolean(plate,c,'INTERSECT');cap_and_weights(plate,bone=bone)
  assert len(plate.data.polygons)>0,f'Empty armor {plate.name}'
  outputs.append(plate);boolean(body,c,'DIFFERENCE')
 # Piston blocks/bearings are actual enclosed interior geometry behind the removed shells.
 for name,a,b,bone,stage in spec:
  if name.startswith('Pectoral'):
   x=(a[0]+b[0])/2
   for dz in [-.045,.045]:rib(body,bone,(x,-.113,1.41+dz),.026,.024,lod)
  else:
   x=.305 if name.endswith('R') else -.305
   rib(body,bone,(x,-.015,1.49),.065,.11,lod)
 cap_and_weights(body,reference=reference);outputs.append(body)
for c in cutters:bpy.data.objects.remove(c,do_unlink=True)
# Retain the original skeleton and bind pose. All exported surfaces retain UVs; new caps use a separate shared material.
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for obj in outputs:obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(out/'T07_ArmorRig.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,use_armature_deform_only=False,use_mesh_modifiers=False,path_mode='STRIP')
working=r/'Tools/IronRequiem/Blender/Working/T07Destructible';working.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(working/'T07_ArmorRig.blend'))
assert source_hash==hashlib.file_digest(source.open('rb'),'sha256').hexdigest()
(report/'armor-authoring.json').write_text(json.dumps({'source':str(source),'source_sha256':source_hash,'source_unchanged':True,'bones':len(rig.data.bones),'regions':[dict(name=n,min=a,max=b,bone=bone,stage=s) for n,a,b,bone,s in spec],'meshes':records},indent=2))
# Views prove the removed surfaces and interior supports, rather than arbitrary renderer masking.
sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sc.render.resolution_x=1000;sc.render.resolution_y=1000;sc.render.resolution_percentage=100
sc.display.shading.light='STUDIO';sc.display.shading.color_type='MATERIAL';sc.display.shading.show_cavity=True;sc.display.shading.cavity_type='BOTH'
for obj in outputs:obj.hide_render=not obj.name.endswith('LOD0')
bpy.ops.object.camera_add(location=(2.6,-4,2.2));cam=bpy.context.object;sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.3;cam.rotation_euler=(Vector((0,0,1.03))-cam.location).to_track_quat('-Z','Y').to_euler()
for state in [0,1,2]:
 for obj in outputs:
  if obj.name.startswith('Armor_') and obj.name.endswith('LOD0'):
   index=next(i for i,(n,*_) in enumerate(spec) if n in obj.name);obj.hide_render=state>=spec[index][4]
 sc.render.filepath=str(report/f'armor-stage-{state}.png');bpy.ops.render.render(write_still=True)
print(json.dumps({'meshes':records}))
