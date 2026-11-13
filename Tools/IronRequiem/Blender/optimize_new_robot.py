"""Asset-only modular optimization of the supplied robot. Originals stay immutable."""
import bpy,bmesh,json,math,hashlib,sys,tempfile
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
import numpy as np
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Tools/IronRequiem/Exports/IR_NewRobot_Optimized_v01';REPORT=ROOT/'Tools/IronRequiem/Reports/NewRobotOptimization'
SOURCE=ROOT/'Tools/IronRequiem/SourceModels/NewRobot_v01/9e4ba63501b568bf7eff159488112f21.fbx'
assert bpy.app.background and '--factory-startup' in sys.argv
assert not (OUT/'IR_NewRobot_Editable.blend').exists() or '--revise-draft' in sys.argv,'Preserve delivered output'
def sha(path):
 with open(path,'rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
original_hash=sha(SOURCE)
TEMP=ROOT/'Tools/IronRequiem/Blender/Working/NewRobot/ExportTemp';TEMP.mkdir(exist_ok=True);tempfile.tempdir=str(TEMP)
for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(SOURCE),use_anim=False)
source=next(o for o in bpy.context.scene.objects if o.type=='MESH');assert not source.vertex_groups
source.data.transform(source.matrix_world);source.matrix_world=Matrix.Identity(4);source.name='Source_Reference_50060'
# Keep the exact original position, size and silhouette. No character height normalization.
material=source.data.materials[0];material.name='IR_NewRobot_Surface';source.data.materials.clear();source.data.materials.append(material)
tex=OUT/'Textures';tex.mkdir(exist_ok=True)
textures={}
for node in material.node_tree.nodes:
 if node.type!='TEX_IMAGE' or not node.image:continue
 image=node.image;name=image.name.lower();channel='Normal' if 'normal' in name else 'Roughness' if 'roughness' in name else 'Metallic' if 'metallic' in name else 'BaseColor'
 image.colorspace_settings.name='sRGB' if channel=='BaseColor' else 'Non-Color'
 image.scale(2048,2048);image.filepath_raw=str(tex/f'IR_NewRobot_{channel}.png');image.file_format='PNG';image.save()
 if image.packed_file:image.unpack(method='REMOVE')
 image.reload();textures[channel]=image
assert set(textures)=={'BaseColor','Normal','Roughness','Metallic'}
n=2048*2048*4
metal=np.empty(n,dtype=np.float32);rough=np.empty(n,dtype=np.float32);base=np.empty(n,dtype=np.float32)
textures['Metallic'].pixels.foreach_get(metal);textures['Roughness'].pixels.foreach_get(rough);textures['BaseColor'].pixels.foreach_get(base)
packed=np.ones((n//4,4),dtype=np.float32);packed[:,:3]=metal.reshape(-1,4)[:,:1];packed[:,3]=1-rough.reshape(-1,4)[:,0]
ms=bpy.data.images.new('IR_NewRobot_MetalSmooth',width=2048,height=2048,alpha=True);ms.colorspace_settings.name='Non-Color';ms.pixels.foreach_set(packed.ravel());ms.filepath_raw=str(tex/'IR_NewRobot_MetalSmooth.png');ms.file_format='PNG';ms.save()
# Joint cap material uses a neutral graphite sample of the existing atlas (no extra submesh).
rgb=base.reshape(2048,2048,4)[32:-32:8,32:-32:8,:3];score=rgb.mean(axis=2)+(rgb.max(axis=2)-rgb.min(axis=2))*3
y,x=np.unravel_index(score.argmin(),score.shape);cap_uv=Vector(((32+x*8+.5)/2048,(32+y*8+.5)/2048))
# The robot faces -Y in Blender; left is +X. These planes sit at the mechanical connections.
WAIST=.484;SHOULDER=.157;ELBOW=.335;HIP=.133;KNEE=.225;ANKLE=.117;WRIST=-.135;BACK=.165
parts=['Torso','Pelvis','Backpack']+[p+'_'+s for s in ('L','R') for p in ('Shoulder','Forearm','Pincer','Thigh','Shin','Foot')]
pivots={'Torso':Vector((-.01,0,WAIST)),'Pelvis':Vector((-.01,0,.385)),'Backpack':Vector((-.01,BACK,.69))}
for side,sign in [('L',1),('R',-1)]:
 x=sign*.195-.009
 pivots.update({'Shoulder_'+side:Vector((sign*SHOULDER,0,.68)),'Forearm_'+side:Vector((sign*ELBOW,.075,.67)),'Pincer_'+side:Vector((sign*.419-.009,WRIST,.665)),'Thigh_'+side:Vector((x,-.05,.375)),'Shin_'+side:Vector((x,.095,KNEE)),'Foot_'+side:Vector((x,0,ANKLE))})
def region(p):
 side='L' if p.x>0 else 'R';a=abs(p.x)
 if p.z-.15*p.y>=WAIST:
  if a>=ELBOW:return ('Pincer_' if p.y<WRIST else 'Forearm_')+side
  if a>=SHOULDER:return 'Shoulder_'+side
  if p.y>BACK and p.z>.57:return 'Backpack'
  return 'Torso'
 if a<HIP and p.z>.30:return 'Pelvis'
 if p.z>=KNEE:return 'Thigh_'+side
 if p.z>=ANKLE:return 'Shin_'+side
 return 'Foot_'+side
planes=[(Vector((v,0,0)),Vector((1,0,0))) for v in (-ELBOW,-SHOULDER,-HIP,0,HIP,SHOULDER,ELBOW)]+[(Vector((0,v,0)),Vector((0,1,0))) for v in (WRIST,BACK)]+[(Vector((0,0,v)),Vector((0,0,1))) for v in (ANKLE,KNEE,.30,.57)]
planes.append((Vector((0,0,WAIST)),Vector((0,-.15,1))))
def tris(o):o.data.calc_loop_triangles();return len(o.data.loop_triangles)
def dup(o,name):
 c=o.copy();c.data=o.data.copy();bpy.context.collection.objects.link(c);c.name=name;return c
def reduce(o,ratio):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 m=o.modifiers.new('OptimizedSurface','DECIMATE');m.ratio=ratio;m.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=m.name)
 o.data.validate(verbose=False,clean_customdata=False);o.data.update()
reference=BVHTree.FromPolygons([v.co for v in source.data.vertices],[list(p.vertices) for p in source.data.polygons])
def errors(o):
 # Bidirectional distances before caps; source and simplified exterior only.
 o.data.calc_loop_triangles();tree=BVHTree.FromPolygons([v.co for v in o.data.vertices],[list(p.vertices) for p in o.data.polygons])
 a=sorted(reference.find_nearest(v.co)[3] for v in o.data.vertices)
 b=sorted(tree.find_nearest(v.co)[3] for v in list(source.data.vertices)[::3])
 return {'optimized_to_source_p95_m':a[int(.95*(len(a)-1))],'optimized_to_source_max_m':a[-1],'source_to_optimized_p95_m':b[int(.95*(len(b)-1))],'source_to_optimized_max_m':b[-1]}
def partition(o,level):
 bm=bmesh.new();bm.from_mesh(o.data)
 for co,normal in planes:bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,plane_co=co,plane_no=normal,clear_inner=False,clear_outer=False)
 bm.to_mesh(o.data);bm.free();o.data.update();result=[]
 for part in parts:
  c=dup(o,part+'_LOD'+str(level));bm=bmesh.new();bm.from_mesh(c.data)
  bmesh.ops.delete(bm,geom=[f for f in bm.faces if region(f.calc_center_median())!=part],context='FACES')
  bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
  assert len(bm.faces)>0,(part,'empty region')
  # Remove coplanar subdivisions introduced by cuts outside this part's boundary.
  bmesh.ops.dissolve_limit(bm,angle_limit=.00001,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV','MATERIAL','SHARP'},use_dissolve_boundaries=True)
  boundaries=[e for e in bm.edges if e.is_boundary];caps=bmesh.ops.holes_fill(bm,edges=boundaries,sides=0).get('faces',[]) if boundaries else []
  uv=bm.loops.layers.uv.active
  for f in caps:
   f.smooth=False
   if uv:
    for loop in f.loops:loop[uv].uv=cap_uv
  bm.normal_update();bad=sum(e.is_boundary or len(e.link_faces)!=2 for e in bm.edges)
  if bad:
   (REPORT/'cut-error.json').write_text(json.dumps({'part':part,'lod':level,'bad_edges':bad,'boundaries_before':len(boundaries),'caps':len(caps)}));raise RuntimeError('Open or nonmanifold joint: '+part)
  # Freeze cap triangulation so FBX and GLB export identical triangle counts.
  bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(c.data);bm.free();c.data.update()
  pivot=pivots[part]
  for v in c.data.vertices:v.co-=pivot
  c.location=pivot
  result.append((c,len(caps)))
 bpy.data.objects.remove(o,do_unlink=True);return result

def export(objects,path):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.hide_set(False);o.select_set(True)
 if path.suffix=='.fbx':bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',use_mesh_modifiers=False)
 else:bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_animations=False,export_cameras=False,export_lights=False)
result={'source_triangles':tris(source),'source_sha256':original_hash,'height_m':.8617969858,'scale_changed':False,'rig':False,'animations':0,'blender_forward':'-Y','blender_up':'+Z','material_count':1,'part_count':len(parts),'texture_resolution':2048,'lods':[],'parts':{p:{'pivot_m':list(v)} for p,v in pivots.items()},'cuts':'Mechanical shoulder/elbow/wrist/hip/knee/ankle/waist/backpack connections; shared cut planes and closed graphite caps'}
all_lods=[]
for level,ratio in enumerate((.30,.13,.045)):
 o=dup(source,'Working_LOD'+str(level));reduce(o,ratio);error=errors(o);objects=partition(o,level)
 root=bpy.data.objects.new('IR_NewRobot_LOD'+str(level),None);bpy.context.collection.objects.link(root)
 for c,_ in objects:c.parent=root
 entries=[]
 for c,caps in objects:entries.append({'name':c.name,'triangles':tris(c),'vertices':len(c.data.vertices),'pivot_m':list(c.location),'caps':caps})
 # Stable export names and sockets across all LODs.
 sockets=[]
 for part,pivot in pivots.items():
  s=bpy.data.objects.new('Joint_'+part,None);bpy.context.collection.objects.link(s);s.parent=root;s.location=pivot;s.empty_display_type='ARROWS';s.empty_display_size=.025;sockets.append(s)
 export([c for c,_ in objects]+[root]+sockets,OUT/f'IR_NewRobot_LOD{level}.fbx')
 export([c for c,_ in objects]+[root]+sockets,OUT/f'IR_NewRobot_LOD{level}.glb')
 for s in sockets:s.name+='__LOD'+str(level)
 result['lods'].append({'level':level,'triangles':sum(r['triangles'] for r in entries),'parts':entries,'surface_error':error})
 all_lods.append(([c for c,_ in objects],root,sockets))
# The selected parts file preserves assembly transforms, exact pivots and names.
export(all_lods[0][0]+[all_lods[0][1]]+all_lods[0][2],OUT/'IR_NewRobot_Parts.fbx')
# Separately named derivatives for direct import; same shared textures.
part_dir=OUT/'Parts';part_dir.mkdir(exist_ok=True)
for c in all_lods[0][0]:export([c],part_dir/(c.name.replace('_LOD0','')+'.fbx'))
for image in bpy.data.images:
 if image.type=='IMAGE' and image.size[0]:image.pack()
source.hide_render=True;source.hide_set(True)
for meshes,root,sockets in all_lods[1:]:
 for o in meshes+ sockets:o.hide_render=True;o.hide_set(True)
bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.filepaths.file_preview_type='NONE';bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'IR_NewRobot_Editable.blend'))
assert sha(SOURCE)==original_hash
(REPORT/'geometry.json').write_text(json.dumps(result,indent=2),encoding='utf8');(OUT/'Verification').mkdir(exist_ok=True);(OUT/'Verification/geometry.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print(json.dumps({'parts':len(parts),'source':result['source_triangles'],'lods':[r['triangles'] for r in result['lods']],'output':str(OUT)}))
