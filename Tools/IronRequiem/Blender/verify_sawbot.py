import bpy,bmesh,json,hashlib,sys
from pathlib import Path
import numpy as np
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Tools/IronRequiem/Exports/IR_SawBot_Optimized_v01';REPORT=ROOT/'Tools/IronRequiem/Reports/SawBot';WORK=ROOT/'Tools/IronRequiem/Blender/Working/SawBot'
assert bpy.app.background and '--factory-startup' in sys.argv
sha=lambda p:hashlib.file_digest(open(p,'rb'),'sha256').hexdigest()
result={'originals_unchanged':{},'source_signatures':{},'roundtrips':[]}
for name in ['sawbot','sawbot2']:
 info=json.loads((REPORT/(name+'-inspection.json')).read_text());result['originals_unchanged'][name]=sha(Path(info['source']))==info['source_sha256'];assert result['originals_unchanged'][name]
 bpy.ops.wm.open_mainfile(filepath=str(WORK/(name+'_source.blend')));o=next(o for o in bpy.context.scene.objects if o.type=='MESH');m=o.data
 co=np.empty(len(m.vertices)*3,np.float32);m.vertices.foreach_get('co',co);vi=np.empty(len(m.loops),np.int32);m.loops.foreach_get('vertex_index',vi);uv=np.empty(len(m.loops)*2,np.float32);m.uv_layers.active.data.foreach_get('uv',uv)
 result['source_signatures'][name]={'vertices':hashlib.sha256(co.tobytes()).hexdigest(),'topology':hashlib.sha256(vi.tobytes()).hexdigest(),'uv':hashlib.sha256(uv.tobytes()).hexdigest(),'textures':{i.name:hashlib.sha256(i.packed_file.data).hexdigest() for i in bpy.data.images if i.packed_file}}
result['both_sources_identical_geometry_uv_textures']=result['source_signatures']['sawbot']==result['source_signatures']['sawbot2']
geometry=json.loads((OUT/'geometry.json').read_text())
for lod in range(3):
 for ext in ['fbx','glb']:
  bpy.ops.wm.read_factory_settings(use_empty=True);path=OUT/f'IR_SawBot_LOD{lod}.{ext}'
  if ext=='fbx':bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
  else:bpy.ops.import_scene.gltf(filepath=str(path))
  objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(objects)==1
  total=0;points=[];bad=0
  for o in objects:
   m=o.data;m.calc_loop_triangles();total+=len(m.loop_triangles);assert len(m.materials)==1;assert len(m.uv_layers)==1
   uv=np.empty(len(m.loops)*2,np.float32);m.uv_layers.active.data.foreach_get('uv',uv);assert np.isfinite(uv).all()
   points.extend(o.matrix_world@v.co for v in m.vertices)
   bm=bmesh.new();bm.from_mesh(m);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7);bad+=sum(not e.is_manifold for e in bm.edges);bm.free()
  assert total==geometry['lods'][lod]['triangles'],(path,total);assert bad==0,(path,bad)
  bounds={'min':[min(p[i] for p in points) for i in range(3)],'max':[max(p[i] for p in points) for i in range(3)]};print('BOUNDS',path.name,bounds,flush=True);assert abs(bounds['min'][2])<.003,(path.name,bounds);assert abs(bounds['max'][2]-.778153)<.003,(path.name,bounds)
  assert not any(o.type=='ARMATURE' for o in bpy.context.scene.objects);assert not bpy.data.actions
  result['roundtrips'].append({'file':path.name,'triangles':total,'renderers':len(objects),'materials':1,'uv_finite':True,'welded_nonmanifold_edges':bad,'bounds':bounds})
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(OUT/'IR_SawBot_Parts.fbx'),use_anim=False);objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(objects)==5
result['parts']=[{'name':o.name,'pivot':list(o.location)} for o in objects];result['rig_or_animations']=False
(REPORT/'verification.json').write_text(json.dumps(result,indent=2));(OUT/'Verification.json').write_text(json.dumps(result,indent=2));print('VERIFIED',json.dumps(result),flush=True)