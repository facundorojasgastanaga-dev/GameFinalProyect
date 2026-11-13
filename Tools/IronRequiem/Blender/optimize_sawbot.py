"""Asset-only SawBot optimization. Rebuild only the identified distal chainsaw regions."""
import bpy,bmesh,json,math,hashlib,sys,shutil
import numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Tools/IronRequiem/Exports/IR_SawBot_Optimized_v01';REPORT=ROOT/'Tools/IronRequiem/Reports/SawBot';WORK=ROOT/'Tools/IronRequiem/Blender/Working/SawBot'
assert bpy.app.background and '--factory-startup' in sys.argv
assert not (OUT/'IR_SawBot_Editable.blend').exists() or '--revise-draft' in sys.argv,'Do not overwrite delivered asset'
OUT.mkdir(parents=True,exist_ok=True);TEX=OUT/'Textures';TEX.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(WORK/'sawbot_source.blend'));source=next(o for o in bpy.context.scene.objects if o.type=='MESH');source.data.transform(source.matrix_world);source.matrix_world=Matrix.Identity(4);source.name='SOURCE_REFERENCE'
material=source.data.materials[0];material.name='IR_SawBot_Surface';textures={}
for node in material.node_tree.nodes:
 if node.type!='TEX_IMAGE' or not node.image:continue
 image=node.image;name=image.name.lower();channel='Normal' if 'normal' in name else 'Roughness' if 'roughness' in name else 'Metallic' if 'metallic' in name else 'BaseColor'
 image.colorspace_settings.name='sRGB' if channel=='BaseColor' else 'Non-Color';image.scale(2048,2048)
 if channel=='Normal':
  pixels=np.empty(2048*2048*4,np.float32);image.pixels.foreach_get(pixels);rgba=pixels.reshape(-1,4);n=rgba[:,:3]*2-1;n/=np.maximum(np.linalg.norm(n,axis=1)[:,None],1e-6);rgba[:,:3]=n*.5+.5;image.pixels.foreach_set(pixels)
 image.filepath_raw=str(TEX/f'IR_SawBot_{channel}.png');image.file_format='PNG';image.save()
 if image.packed_file:image.unpack(method='REMOVE')
 image.reload();textures[channel]=image
assert set(textures)=={'BaseColor','Normal','Roughness','Metallic'}
metal=np.empty(2048*2048*4,np.float32);rough=metal.copy();textures['Metallic'].pixels.foreach_get(metal);textures['Roughness'].pixels.foreach_get(rough);packed=np.ones((2048*2048,4),np.float32);packed[:,:3]=metal.reshape(-1,4)[:,:1];packed[:,3]=1-rough.reshape(-1,4)[:,0]
ms=bpy.data.images.new('IR_SawBot_MetalSmooth',2048,2048,alpha=True);ms.colorspace_settings.name='Non-Color';ms.pixels.foreach_set(packed.ravel());ms.filepath_raw=str(TEX/'IR_SawBot_MetalSmooth.png');ms.file_format='PNG';ms.save()
source.data.calc_loop_triangles();tree=BVHTree.FromPolygons([v.co for v in source.data.vertices],[tuple(p.vertices) for p in source.data.polygons]);source_uv=source.data.uv_layers.active.data

# Reuse quiet metal patches from the existing atlas. Per-face planar UVs avoid crossing old UV islands.
base=np.empty(2048*2048*4,np.float32);normal=base.copy();textures['BaseColor'].pixels.foreach_get(base);textures['Normal'].pixels.foreach_get(normal);bc=base.reshape(2048,2048,4);nm=normal.reshape(2048,2048,4);mt=metal.reshape(2048,2048,4);palette={}
for label,target in [('blade',(.13,.12,.10)),('chain',(.31,.29,.25))]:
 sample=bc[20:-20:4,20:-20:4,:3];score=((sample-np.array(target))**2).sum(axis=2)*3+((nm[20:-20:4,20:-20:4,:2]-.5)**2).sum(axis=2)*8+(1-nm[20:-20:4,20:-20:4,2])*4+(mt[20:-20:4,20:-20:4,0]-.65)**2*.1
 for dy,dx in [(4,0),(-4,0),(0,4),(0,-4)]:score+=((sample-bc[20+dy:2048-20+dy:4,20+dx:2048-20+dx:4,:3])**2).sum(axis=2)*8
 row,col=np.unravel_index(score.argmin(),score.shape);palette[label]=Vector(((20+col*4+.5)/2048,(20+row*4+.5)/2048))

def transfer_uv(obj):
 uv=(obj.data.uv_layers.active or obj.data.uv_layers.new(name='UVMap')).data;center=palette['chain' if obj.name.startswith('Chain') or obj.name.startswith('Cylinder') else 'blade']
 for poly in obj.data.polygons:
  n=poly.normal.normalized();u=n.orthogonal().normalized();v=n.cross(u).normalized();origin=poly.center
  for li in poly.loop_indices:
   delta=obj.data.vertices[obj.data.loops[li].vertex_index].co-origin;uv[li].uv=center+Vector((delta.dot(u),delta.dot(v)))*.015
def mesh(name,verts,faces):
 data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update();o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);o.data.materials.append(material);transfer_uv(o);return o

def duplicates(o,name):
 c=o.copy();c.data=o.data.copy();bpy.context.collection.objects.link(c);c.name=name;return c

def tris(o):o.data.calc_loop_triangles();return len(o.data.loop_triangles)
def repair_normals(o):
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
def reduce(o,ratio):
 original=o.data.copy()
 for attempt in range(5):
  if attempt:
   old=o.data;o.data=original.copy();bpy.data.meshes.remove(old)
  bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
  m=o.modifiers.new('SurfaceReduction','DECIMATE');m.ratio=min(1,ratio*1.2**attempt);m.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=m.name);o.data.validate(clean_customdata=False);o.data.update()
  bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.dissolve_degenerate(bm,dist=1e-8,edges=list(bm.edges));boundaries=[e for e in bm.edges if e.is_boundary]
  if boundaries:
   assert len(boundaries)<=12,(o.name,'unexpected open surface',len(boundaries));newcaps=bmesh.ops.holes_fill(bm,edges=boundaries,sides=0).get('faces',[]);uv=bm.loops.layers.uv.active
   for f in newcaps:
    if uv:
     for loop in f.loops:loop[uv].uv=palette['blade']
  # Remove exact duplicate faces only: they cover the same triangle, not a unique exterior surface.
  bm.verts.index_update();seen=set();duplicates=[]
  for f in bm.faces:
   key=tuple(sorted(v.index for v in f.verts))
   if key in seen:duplicates.append(f)
   else:seen.add(key)
  if duplicates:bmesh.ops.delete(bm,geom=duplicates,context='FACES_ONLY')
  bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bad=[e for e in bm.edges if not e.is_manifold];diagnostics=[{'ends':[list(v.co) for v in e.verts],'faces':len(e.link_faces)} for e in bad];bm.to_mesh(o.data);bm.free();o.data.update()
  if not bad:
   bpy.data.meshes.remove(original);return
  print('Retry lower reduction',o.name,attempt,diagnostics,flush=True)
 raise RuntimeError('Reduction would break topology: '+o.name)
# The source is a closed, fused mesh. Cut at the two actual wrist mouths, not arbitrary hidden polygons.
roots={s:Vector((sign*.367,-.078,.267)) for s,sign in [('L',1),('R',-1)]};directions={s:Vector((sign*.68,0,-.733212)).normalized() for s,sign in [('L',1),('R',-1)]}
body=duplicates(source,'Body_Fixed_Reference');bm=bmesh.new();bm.from_mesh(body.data)
for s in roots:bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,plane_co=roots[s],plane_no=directions[s],clear_inner=False,clear_outer=False)
to_delete=[]
for f in bm.faces:
 p=f.calc_center_median();s='L' if p.x>0 else 'R'
 if abs(p.x)>.30 and (p-roots[s]).dot(directions[s])>1e-7:to_delete.append(f)
removed=len(to_delete);bmesh.ops.delete(bm,geom=to_delete,context='FACES');bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');boundaries=[e for e in bm.edges if e.is_boundary];caps=bmesh.ops.holes_fill(bm,edges=boundaries,sides=0).get('faces',[]);uv=bm.loops.layers.uv.active
for f in caps:
 f.smooth=False
 for loop in f.loops:loop[uv].uv=palette['blade']
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(body.data);bm.free();body.data.update();body_ref_tree=BVHTree.FromPolygons([v.co for v in body.data.vertices],[tuple(p.vertices) for p in body.data.polygons])

# Manufactured guide bars and a real perimeter chain: uniform links, alternating cutter sides, no floating spikes.
def make_saw(side,level):
 root=roots[side];d=directions[side];u=Vector((-d.z,0,d.x));y=Vector((0,1,0));length=.175;radius=.031;segments=[12,10,8][level]
 def point(t,w,depth):return root+d*t+u*w+y*depth
 outline=[]
 for j in range(segments+1):
  a=-math.pi/2+j*math.pi/segments;outline.append((length+radius*math.cos(a),radius*math.sin(a)))
 for j in range(segments+1):
  a=math.pi/2+j*math.pi/segments;outline.append((radius*math.cos(a),radius*math.sin(a)))
 verts=[];n=len(outline)
 for depth,inset in [(-.008,.002),(-.006,0),(.006,0),(.008,.002)]:
  for t,w in outline:
   center=length if t>length*.5 else 0;delta=Vector((t-center,w));delta.normalize();verts.append(point(t-delta.x*inset,w-delta.y*inset,depth))
 faces=[tuple(reversed(range(n))),tuple(range(3*n,4*n))]
 for ring in range(3):
  for i in range(n):j=(i+1)%n;faces.append((ring*n+i,ring*n+j,(ring+1)*n+j,(ring+1)*n+i))
 blade=mesh('GuideBar_'+side+'_LOD'+str(level),verts,faces);repair_normals(blade)
 # A closed rectangular-section chain band follows a stadium path.
 cr=.035;steps=[80,60,40][level];perimeter=2*length+2*math.pi*cr
 def path(distance):
  q=distance%perimeter
  if q<length:return q,cr,0,1
  q-=length
  if q<math.pi*cr:
   a=math.pi/2-q/cr;return length+cr*math.cos(a),cr*math.sin(a),math.cos(a),math.sin(a)
  q-=math.pi*cr
  if q<length:return length-q,-cr,0,-1
  q-=length;a=-math.pi/2-q/cr;return cr*math.cos(a),cr*math.sin(a),math.cos(a),math.sin(a)
 verts=[];faces=[]
 for j in range(steps):
  t,w,nt,nw=path(j*perimeter/steps)
  for offset,depth in [(-.002,-.012),(.002,-.012),(.002,.012),(-.002,.012)]:verts.append(point(t+nt*offset,w+nw*offset,depth))
 for j in range(steps):
  k=(j+1)%steps
  for c in range(4):faces.append((j*4+c,j*4+(c+1)%4,k*4+(c+1)%4,k*4+c))
 def prism(local,depth0,depth1):
  start=len(verts);m=len(local)
  for depth in (depth0,depth1):
   for t,w in local:verts.append(point(t,w,depth))
  faces.append(tuple(reversed(range(start,start+m))));faces.append(tuple(range(start+m,start+2*m)))
  for i in range(m):j=(i+1)%m;faces.append((start+i,start+j,start+m+j,start+m+i))
 links=[28,24,18][level]
 for j in range(links):
  t,w,nt,nw=path((j+.5)*perimeter/links);tangent=Vector((nw,-nt));normal=Vector((nt,nw));center=Vector((t,w));side_sign=1 if j%2 else -1
  # Connected cutter root overlaps the chain band. Raked cutting tip points along chain travel.
  coords=[(-.006,-.001),(.006,-.001),(.006,.008),(.001,.009)]
  poly=[tuple(center+tangent*a+normal*b) for a,b in coords];prism(poly,side_sign*.007-.0025,side_sign*.007+.0025)
  # Side links remain in both front and rear views, aligned to the same even pitch.
  link=[tuple(center+tangent*a+normal*b) for a,b in [(-.006,-.0025),(.006,-.0025),(.006,.0025),(-.006,.0025)]]
  prism(link,-.014,-.012);prism(link,.012,.014)
 chain=mesh('Chain_'+side+'_LOD'+str(level),verts,faces);repair_normals(chain)
 # End and drive rivets are closed cylinders, joined into the guide-bar mesh.
 rivets=[]
 for t in [0,length]:
  for depth in [-.009,.009]:
   bpy.ops.mesh.primitive_cylinder_add(vertices=[16,12,8][level],radius=.0075,depth=.003,location=point(t,0,depth),rotation=(math.pi/2,0,0));o=bpy.context.object;o.data.transform(o.matrix_world);o.matrix_world=Matrix.Identity(4);o.data.materials.clear();o.data.materials.append(material);transfer_uv(o);rivets.append(o)
 bpy.ops.object.select_all(action='DESELECT');blade.select_set(True)
 for o in rivets:o.select_set(True)
 bpy.context.view_layer.objects.active=blade;bpy.ops.object.join()
 for o in [blade,chain]:
  o.data.transform(Matrix.Translation(-root));o.location=root
 return blade,chain

levels=[];stats=[]
for level,target in enumerate([18000,9000,4000]):
 b=duplicates(body,'Body_LOD'+str(level));reduce(b,min(1,target/tris(body)))
 lo=min(v.co.z for v in b.data.vertices);hi=max(v.co.z for v in b.data.vertices);original_top=max(v.co.z for v in source.data.vertices)
 for v in b.data.vertices:
  if v.co.z<.08:v.co.z-=lo*(.08-v.co.z)/(.08-lo)
  elif v.co.z>.70:v.co.z+=(original_top-hi)*(v.co.z-.70)/(hi-.70)
 b.data.update();parts=[b]
 for side in roots:parts.extend(make_saw(side,level))
 for o in parts:
  repair_normals(o);o.data.calc_loop_triangles()
  bm=bmesh.new();bm.from_mesh(o.data);bad=sum(not e.is_manifold for e in bm.edges);bm.free();assert bad==0,(o.name,bad)
 errors=sorted(body_ref_tree.find_nearest(v.co)[3] for v in b.data.vertices)
 stats.append({'lod':level,'triangles':sum(tris(o) for o in parts),'body_triangles':tris(b),'body_to_source_p95_m':errors[int(.95*(len(errors)-1))],'body_to_source_max_m':errors[-1],'parts':[{'name':o.name,'triangles':tris(o)} for o in parts]});levels.append(parts)
 print('LOD',level,stats[-1],flush=True)

def export(objects,path):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.hide_set(False);o.select_set(True)
 if path.suffix=='.fbx':bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',use_mesh_modifiers=False)
 else:bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_animations=False,export_cameras=False,export_lights=False)
# Joined LODs minimize renderers; a separate parts export preserves useful saw pivots.
for i,parts in enumerate(levels):
 copies=[duplicates(o,'Export_'+o.name) for o in parts];bpy.ops.object.select_all(action='DESELECT')
 for o in copies:o.select_set(True)
 bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();joined=copies[0];joined.name='IR_SawBot_LOD'+str(i);export([joined],OUT/(joined.name+'.fbx'));export([joined],OUT/(joined.name+'.glb'));bpy.data.objects.remove(joined,do_unlink=True)
export(levels[0],OUT/'IR_SawBot_Parts.fbx')
# Preview identical cameras/lighting for source and repaired LOD0.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.use_denoising=True;scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.world=bpy.data.worlds.new('PreviewWorld');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.12,.12,1);scene.view_settings.view_transform='AgX';center=Vector((-.006,.033,.389));size=1.068
for xyz,energy in [((-2,-3,4),900),((3,-1,2),650),((0,3,3),1000)]:
 bpy.ops.object.light_add(type='AREA',location=center+Vector(xyz)*size);o=bpy.context.object;o.data.energy=energy*size*size;o.data.shape='DISK';o.data.size=size*3;o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();scene.camera=bpy.context.object;scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=size*1.2
for group in levels:
 for o in group:o.hide_render=True
body.hide_render=True
for label,xyz in [('front',(0,-3,.1)),('side',(3,0,.1)),('threequarter',(2,-3,.3))]:
 if '--quick-revise' in sys.argv and label!='front':continue
 scene.camera.location=center+Vector(xyz)*size;scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler()
 for version in ['before','after']:
  if '--quick-revise' in sys.argv and version=='before':continue
  source.hide_render=version!='before'
  for o in levels[0]:o.hide_render=version!='after'
  scene.render.filepath=str(REPORT/(version+'-'+label+'.png'));bpy.ops.render.render(write_still=True)
# Editable file: explicit LOD collections, original source kept in a hidden reference collection.
for i,group in enumerate(levels):
 collection=bpy.data.collections.new('LOD'+str(i)+'_Editable');scene.collection.children.link(collection)
 for o in group:
  for c in list(o.users_collection):c.objects.unlink(o)
  collection.objects.link(o);o.hide_render=i!=0;o.hide_set(i!=0)
ref=bpy.data.collections.new('Source_Reference_DoNotExport');scene.collection.children.link(ref)
for o in [source,body]:
 for c in list(o.users_collection):c.objects.unlink(o)
 ref.objects.link(o);o.hide_render=True;o.hide_set(True)
ref.hide_render=True;ref.hide_viewport=True
for image in textures.values():image.pack()
bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.filepaths.file_preview_type='NONE';bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'IR_SawBot_Editable.blend'))
report={'source_triangles':49948,'source_scale_preserved':True,'source_height_m':.778153,'source_static':True,'removed_distal_faces':removed,'wrist_caps':len(caps),'lods':stats,'saw_fix':'Closed guide bars, even perimeter chain links, alternating raked cutters and end/drive rivets; separate left/right pivots. No rig or animation added.','textures':'4096 ->2048, normal vectors renormalized, original atlas UV retained outside rebuilt saws, per-face planar UVs using quiet metal patches of the original atlas on rebuilt saws. URP metallic/smoothness packed separately.','fps_claim':False}
(REPORT/'optimization.json').write_text(json.dumps(report,indent=2));(OUT/'geometry.json').write_text(json.dumps(report,indent=2));print('COMPLETE',flush=True)