import bpy,json,math,bmesh
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[3];out=root/'Tools/IronRequiem/Reports/CannonBurr'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/IronRequiem/Art/Enemies/T07Optimized/T07_LOD0.fbx'),use_anim=False)
gun=next(o for o in bpy.context.scene.objects if o.type=='MESH' and 'Cannon' in o.name)
for o in list(bpy.context.scene.objects):
 if o!=gun:bpy.data.objects.remove(o,do_unlink=True)
mesh=gun.data;mesh.calc_loop_triangles();bm=bmesh.new();bm.from_mesh(mesh)
records=[]
for p in mesh.polygons:
 co=[mesh.vertices[i].co for i in p.vertices];longest=max((co[a]-co[(a+1)%len(co)]).length for a in range(len(co)))
 if longest*longest/max(p.area,1e-12)>250:records.append({'face':p.index,'area':p.area,'ratio':longest*longest/max(p.area,1e-12),'vertices':list(p.vertices),'coordinates':[list(v) for v in co]})
info={'mesh':gun.name,'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),'bounds':[[min(v.co[i] for v in mesh.vertices) for i in range(3)],[max(v.co[i] for v in mesh.vertices) for i in range(3)]],'boundary_edges':sum(e.is_boundary for e in bm.edges),'thin_faces':sorted(records,key=lambda x:-x['ratio'])[:40]}
(out/'cannon-geometry.json').write_text(json.dumps(info,indent=2));bm.free()
sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sc.render.resolution_x=900;sc.render.resolution_y=700;sc.render.resolution_percentage=100
sc.display.shading.light='STUDIO';sc.display.shading.color_type='SINGLE';sc.display.shading.single_color=(.36,.42,.46);sc.display.shading.show_cavity=True;sc.display.shading.cavity_type='BOTH';sc.render.image_settings.file_format='PNG'
points=[gun.matrix_world@Vector(c) for c in gun.bound_box];center=sum(points,Vector())/8;radius=max((p-center).length for p in points)
bpy.ops.object.camera_add();cam=bpy.context.object;sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=radius*2.35
for i,direction in enumerate([(-2,-3,1.5),(2,3,1.5),(-3,1,0),(0,0,4)]):
 cam.location=center+Vector(direction).normalized()*radius*5;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(out/f'geometry-{i}.png');bpy.ops.render.render(write_still=True)
