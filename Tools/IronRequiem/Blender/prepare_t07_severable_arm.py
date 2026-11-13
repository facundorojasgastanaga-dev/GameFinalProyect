"""Separate the existing unarmed RIGHT limb by its real skin groups, cap the shoulder in all LODs."""
import bpy,bmesh,json,hashlib
from pathlib import Path
from mathutils import Vector
r=Path(__file__).resolve().parents[3]
source=r/'Tools/IronRequiem/Blender/Working/T07Destructible/T07_ArmorRig.blend'
out=r/'Assets/IronRequiem/Art/Enemies/T07Destructible/T07_SeverableArm.fbx'
assert not out.exists(),'Preserve an existing export before authoring'
bpy.ops.wm.open_mainfile(filepath=str(source))
if hasattr(bpy.context.preferences.filepaths,'use_save_preview_images'):bpy.context.preferences.filepaths.use_save_preview_images=False
rig=bpy.data.objects['T07Rig'];records=[]
for lod in range(3):
 body=bpy.data.objects['Body_LOD'+str(lod)]
 groups={g.index:g.name for g in body.vertex_groups}
 arm_indices={g.index for g in body.vertex_groups if g.name in {'UpperArm_R','Forearm_R','Hand_R'}}
 selected=set()
 for face in body.data.polygons:
  votes=0
  for index in face.vertices:
   vertex=body.data.vertices[index]
   if sum(w.weight for w in vertex.groups if w.group in arm_indices)>=.5:votes+=1
  if votes>len(face.vertices)/2:selected.add(face.index)
 assert len(selected)>12,('No coherent right limb',lod,len(selected))
 arm=body.copy();arm.data=body.data.copy();bpy.context.collection.objects.link(arm);arm.name='Arm_R_LOD'+str(lod)
 for obj,keep_arm in [(arm,True),(body,False)]:
  bm=bmesh.new();bm.from_mesh(obj.data);bm.faces.ensure_lookup_table()
  previous_boundary={e for e in bm.edges if e.is_boundary}
  remove=[f for f in bm.faces if (f.index in selected)!=keep_arm]
  bmesh.ops.delete(bm,geom=remove,context='FACES')
  loose=[v for v in bm.verts if not v.link_faces]
  if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
  if keep_arm:
   bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
   bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.000001)
  edges=[e for e in bm.edges if e.is_boundary and (keep_arm or e not in previous_boundary)]
  filled=bmesh.ops.holes_fill(bm,edges=edges,sides=0) if edges else {}
  if keep_arm:
   remaining=[e for e in bm.edges if e.is_boundary]
   if remaining:
    extra=bmesh.ops.triangle_fill(bm,edges=remaining,use_beauty=True)
    filled.setdefault('faces',[]).extend(f for f in extra.get('geom',[]) if isinstance(f,bmesh.types.BMFace))
   remaining={e for e in bm.edges if e.is_boundary}
   deform=bm.verts.layers.deform.verify()
   while remaining:
    first=remaining.pop();component={first};vertices=set(first.verts);pending=list(first.verts)
    while pending:
     v=pending.pop()
     for e in list(v.link_edges):
      if e in remaining:
       remaining.remove(e);component.add(e)
       for other in e.verts:
        if other not in vertices:vertices.add(other);pending.append(other)
    center=bm.verts.new(sum((v.co for v in vertices),Vector())/len(vertices))
    center[deform][body.vertex_groups['UpperArm_R'].index]=1
    for edge in component:
     try:
      face=bm.faces.new((edge.verts[1],edge.verts[0],center));filled.setdefault('faces',[]).append(face)
     except ValueError:pass
  for face in filled.get('faces',[]):face.material_index=len(obj.data.materials)-1
  bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free();obj.data.update()
  if keep_arm:
   # The source limb contains open ports/T-junctions. A 2 mm inward shell closes
   # them while preserving the outside UV surface and silhouette.
   bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
   solid=obj.modifiers.new('ClosedMechanicalInterior','SOLIDIFY');solid.solidify_mode='NON_MANIFOLD';solid.thickness=.002;solid.offset=-1
   solid.material_offset=0;solid.material_offset_rim=len(obj.data.materials)-1
   bpy.ops.object.modifier_apply(modifier=solid.name)
  obj.data.calc_loop_triangles()
  check=bmesh.new();check.from_mesh(obj.data)
  records.append({'name':obj.name,'triangles':len(obj.data.loop_triangles),'boundary':sum(e.is_boundary for e in check.edges),'uv':len(obj.data.uv_layers),'capped_faces':len(filled.get('faces',[]))});check.free()
  if keep_arm:assert records[-1]['boundary']==0,records[-1]
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for obj in bpy.context.scene.objects:
 if obj.type=='MESH' and (obj.name.startswith('Body_LOD') or obj.name.startswith('Armor_') or obj.name.startswith('Arm_R_LOD')):obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,use_armature_deform_only=False,use_mesh_modifiers=False,path_mode='STRIP')
bpy.ops.wm.save_as_mainfile(filepath=str(source.with_name('T07_SeverableArm.blend')))
(r/'Tools/IronRequiem/Requests/t07-arm-geometry.json').write_text(json.dumps({'source':str(source),'source_sha256':hashlib.file_digest(source.open('rb'),'sha256').hexdigest(),'meshes':records},indent=2))
print('CLOSED_RIGHT_ARM',json.dumps(records))
