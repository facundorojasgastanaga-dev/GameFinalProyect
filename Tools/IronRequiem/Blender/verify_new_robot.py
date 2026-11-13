import bpy,bmesh,json,hashlib,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Tools/IronRequiem/Exports/IR_NewRobot_Optimized_v01';R=ROOT/'Tools/IronRequiem/Reports/NewRobotOptimization';spec=json.loads((R/'geometry.json').read_text())
def clear():
 for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
def load(path):
 clear()
 if path.suffix=='.fbx':bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True)
 else:bpy.ops.import_scene.gltf(filepath=str(path))
 bpy.context.view_layer.update()
 return [o for o in bpy.context.scene.objects if o.type=='MESH']
def check(path,expected):
 meshes=load(path);assert len(meshes)==len(expected),(path,'Part count',len(meshes));total=0
 for o in meshes:
  p=next(p for p in expected if p['name']==o.name);o.data.calc_loop_triangles();count=len(o.data.loop_triangles);assert count==p['triangles'],(path,o.name,count,p['triangles']);total+=count
  assert len(o.data.uv_layers)==1 and len(o.data.materials)==1
  assert not o.vertex_groups and not any(m.type=='ARMATURE' for m in o.modifiers)
  assert (o.matrix_world.translation-Vector(p['pivot_m'])).length<1e-5,(path,o.name,'pivot')
  assert all(math.isfinite(v) for loop in o.data.uv_layers[0].data for v in loop.uv)
  bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7);bm.normal_update();bad=sum(len(e.link_faces)!=2 for e in bm.edges);bm.free();assert bad==0,(path,o.name,'open caps',bad)
 assert not any(o.type=='ARMATURE' for o in bpy.context.scene.objects)
 assert len(bpy.data.actions)==0
 images={n.image for o in meshes for m in o.data.materials if m and m.use_nodes for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image}
 assert images and all(tuple(i.size)==(2048,2048) for i in images),(path,'textures')
 return {'file':str(path.relative_to(OUT)),'parts':len(meshes),'triangles':total,'pivots_uv_caps_textures_verified':True}
rows=[]
for level in range(3):
 for ext in ('fbx','glb'):rows.append(check(OUT/f'IR_NewRobot_LOD{level}.{ext}',spec['lods'][level]['parts']))
rows.append(check(OUT/'IR_NewRobot_Parts.fbx',spec['lods'][0]['parts']))
for p in spec['lods'][0]['parts']:rows.append(check(OUT/'Parts'/(p['name'].replace('_LOD0','')+'.fbx'),[p]))
# Confirm that the supplied GLB is the same surface, despite seam vertex splits.
source=ROOT/'Tools/IronRequiem/SourceModels/NewRobot_v01';fbx=load(next(source.glob('*.fbx')))[0]
vertices=[fbx.matrix_world@v.co for v in fbx.data.vertices];tree=BVHTree.FromPolygons(vertices,[list(p.vertices) for p in fbx.data.polygons]);glb=load(next(source.glob('*.glb')))[0]
errors=[tree.find_nearest(glb.matrix_world@v.co)[3] for v in list(glb.data.vertices)[::11]];errors.sort();assert errors[-1]<.0001,('Source equivalence',errors[-1])
sources=json.loads((R/'source-hashes.json').read_text(encoding='utf-8-sig'))
for s in sources:
 with open(s['path'],'rb') as f:assert hashlib.file_digest(f,'sha256').hexdigest().upper()==s['hash']
result={'passed':True,'models_verified':len(rows),'source_originals_unchanged':True,'source_fbx_glb_max_sample_distance_m':errors[-1],'unity_imported':False,'fps_measured':False,'rig':False,'files':rows}
(R/'roundtrip.json').write_text(json.dumps(result,indent=2),encoding='utf8');(OUT/'Verification/roundtrip.json').write_text(json.dumps(result,indent=2),encoding='utf8');print(json.dumps({'passed':True,'models_verified':len(rows),'sources_unchanged':True}))
