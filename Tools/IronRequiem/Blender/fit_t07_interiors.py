"""Fit the NEW shoulder bearings below intact armor; never edits an original asset."""
import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector
r=Path(__file__).resolve().parents[3];work=r/'Tools/IronRequiem/Blender/Working/T07Destructible/T07_ArmorRig.blend'
if hasattr(bpy.context.preferences.filepaths,'use_save_preview_images'):bpy.context.preferences.filepaths.use_save_preview_images=False
bpy.ops.wm.open_mainfile(filepath=str(work));changes=[]
for lod in range(3):
 obj=bpy.data.objects[f'Body_LOD{lod}'];mesh=obj.data;adj=[[] for v in mesh.vertices]
 for e in mesh.edges:a,b=e.vertices;adj[a].append(b);adj[b].append(a)
 seen=set()
 for v in mesh.vertices:
  if v.index in seen:continue
  stack=[v.index];island=[];seen.add(v.index)
  while stack:
   i=stack.pop();island.append(i)
   for j in adj[i]:
    if j not in seen:seen.add(j);stack.append(j)
  center=sum((mesh.vertices[i].co for i in island),Vector())/len(island)
  if len(island)<=24 and abs(center.z-1.49)<.002 and abs(abs(center.x)-.305)<.002:
   for i in island:
    p=mesh.vertices[i].co-center;p.x*=.7;p.z*=.7;p.y*=.65;mesh.vertices[i].co=center+p+Vector((0,.025,-.035))
   changes.append({'mesh':obj.name,'vertices':len(island),'original_center':list(center)})
 mesh.update()
rig=bpy.data.objects['T07Rig'];bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for obj in bpy.context.scene.objects:
 if obj.type=='MESH' and (obj.name.startswith('Armor_') or obj.name.startswith('Body_LOD')):obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(r/'Assets/IronRequiem/Art/Enemies/T07Destructible/T07_ArmorRig.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,use_armature_deform_only=False,use_mesh_modifiers=False,path_mode='STRIP')
bpy.ops.wm.save_as_mainfile(filepath=str(work))
(r/'Tools/IronRequiem/Reports/T07Destruction/interior-fit.json').write_text(json.dumps(changes,indent=2));print('Fitted',len(changes),'bearings')
