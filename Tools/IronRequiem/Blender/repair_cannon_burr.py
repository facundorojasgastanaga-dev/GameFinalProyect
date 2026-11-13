import bpy,bmesh,json,hashlib,sys
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[3];out=root/'Tools/IronRequiem/Reports/CannonBurr';asset=root/'Assets/IronRequiem/Art/Weapons/T07CannonClean';asset.mkdir(parents=True,exist_ok=True)
assert not (asset/'T07_CannonClean.fbx').exists() or '--revise-draft' in sys.argv,'Never overwrite delivered source/derivative silently'
bpy.ops.wm.read_factory_settings(use_empty=True)
body=bpy.data.materials.new('OriginalCannonSurface');body.diffuse_color=(.35,.4,.44,1)
cap=bpy.data.materials.new('CutInterior');cap.diffuse_color=(.065,.075,.08,1)
meshes=[];report=[]
for lod in range(3):
 source=root/f'Assets/IronRequiem/Art/Enemies/T07Optimized/T07_LOD{lod}.fbx'
 before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(source),use_anim=False)
 imported=set(bpy.data.objects)-before;gun=next(o for o in imported if o.type=='MESH' and 'Cannon' in o.name)
 matrix=gun.matrix_world.copy();gun.parent=None;gun.matrix_world=matrix
 for o in imported:
  if o!=gun:bpy.data.objects.remove(o,do_unlink=True)
 original=gun.data.copy();source_tri=len(original.polygons)
 rim=[original.vertices[i].co.copy() for i in [45,48,46,70,63,64,69,68]] if lod==0 else []
 gun.data.materials.clear();gun.data.materials.append(body);gun.data.materials.append(cap)
 bm=bmesh.new();bm.from_mesh(gun.data)
 # All original open cut boundaries are on the medial arm edge / rear joint.
 # Actual plane intersections replace the jagged cuts; no arbitrary polygon masking.
 for point,normal in [((.0385,0,0),(1,0,0)),((0,0,.020),(0,0,1))]:
  bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,plane_co=point,plane_no=normal,clear_outer=True,clear_inner=False)
 bmesh.ops.dissolve_degenerate(bm,dist=1e-8,edges=list(bm.edges))
 for f in bm.faces:f.material_index=0
 edges=[e for e in bm.edges if e.is_boundary]
 faces=bmesh.ops.holes_fill(bm,edges=edges,sides=0)['faces']
 uv=bm.loops.layers.uv.active
 for f in faces:
  f.material_index=1;f.smooth=False
  if uv:
   for loop in f.loops:loop[uv].uv=(.5,.5)
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 assert sum(e.is_boundary for e in bm.edges)==0,'Cut closure failed'
 assert all(min((v.co-p).length for v in bm.verts)<1e-8 for p in rim),'Muzzle rim moved'
 bm.to_mesh(gun.data);bm.free();gun.data.update();gun.data.calc_loop_triangles();gun.name=f'CannonClean_LOD{lod}';gun.data.name=gun.name
 meshes.append(gun)
 report.append({'lod':lod,'source_sha256':hashlib.file_digest(source.open('rb'),'sha256').hexdigest(),'before_triangles':source_tri,'after_triangles':len(gun.data.loop_triangles),'new_cap_faces':len(faces),'boundary_edges':0,'muzzle_rim_max_error_m':0 if lod==0 else None})
for o in bpy.context.scene.objects:o.select_set(False)
for o in meshes:o.select_set(True)
bpy.context.view_layer.objects.active=meshes[0]
bpy.ops.export_scene.fbx(filepath=str(asset/'T07_CannonClean.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=False,path_mode='STRIP')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'Tools/IronRequiem/Blender/Working/T07CannonClean/T07_CannonClean.blend'))
(out/'repair-geometry.json').write_text(json.dumps(report,indent=2))
for o in meshes[1:]:o.hide_render=True
sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sc.render.resolution_x=900;sc.render.resolution_y=700;sc.render.resolution_percentage=100
sc.display.shading.light='STUDIO';sc.display.shading.color_type='MATERIAL';sc.display.shading.show_cavity=True;sc.display.shading.cavity_type='BOTH';sc.render.image_settings.file_format='PNG'
gun=meshes[0];points=[gun.matrix_world@Vector(c) for c in gun.bound_box];center=sum(points,Vector())/8;radius=max((p-center).length for p in points)
bpy.ops.object.camera_add();cam=bpy.context.object;sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=radius*2.35
for i,direction in enumerate([(-2,-3,1.5),(2,3,1.5),(-3,1,0)]):
 cam.location=center+Vector(direction).normalized()*radius*5;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(out/f'clean-{i}.png');bpy.ops.render.render(write_still=True)
