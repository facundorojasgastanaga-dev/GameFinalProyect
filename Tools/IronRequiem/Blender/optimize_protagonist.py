"""Optimize the supplied protagonist into portable modular assets; never import into Unity."""
import argparse,bpy,bmesh,hashlib,json,math,sys,tempfile
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[3]
REPORTS=ROOT/'Tools/IronRequiem/Reports/ProtagonistOptimization'
OUT=ROOT/'Tools/IronRequiem/Exports/IR_Protagonist_Optimized_v01'
SOURCE=Path('C:/Users/gehbd/OneDrive/Escritorio/Juegos GJ/PrincipalBot/71443cd84dd6034935129fc02c5facfa.fbx')
parser=argparse.ArgumentParser();parser.add_argument('operation',choices=['probe','generate']);parser.add_argument('--revise-draft',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
assert bpy.app.background and '--factory-startup' in sys.argv
REPORTS.mkdir(parents=True,exist_ok=True)
EXPECTED=json.loads((REPORTS/'fbx-inspection.json').read_text())
def sha(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert sha(SOURCE)==EXPECTED['source_sha256']
for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(SOURCE),use_anim=False)
source=next(o for o in bpy.context.scene.objects if o.type=='MESH')
assert len([o for o in bpy.context.scene.objects if o.type=='MESH'])==1
assert not source.vertex_groups and not any(o.type=='ARMATURE' for o in bpy.context.scene.objects)
source.data.transform(source.matrix_world);source.matrix_world=Matrix.Identity(4)
low=Vector(EXPECTED['bounds']['min']);high=Vector(EXPECTED['bounds']['max'])
offset=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))
scale=1.80/(high.z-low.z)
for v in source.data.vertices:v.co=(v.co-offset)*scale
source.name='Source_Normalized';source.data.update()
def pos(xyz):return (Vector(xyz)-offset)*scale
landmarks={
    'R':{'elbow':pos((-.214,.025,.795)),'wrist':pos((-.264,-.017,.632)),'sign':-1},
    'L':{'elbow':pos((.218,.025,.795)),'wrist':pos((.268,-.017,.632)),'sign':1}}
for row in landmarks.values():row['normal']=(row['wrist']-row['elbow']).normalized()
def region(co):
    if co.z<.47*scale:return 'Body'
    for side,row in landmarks.items():
        if row['sign']*co.x>.16*scale:
            if (co-row['wrist']).dot(row['normal'])>=-1e-7:return 'Hand_'+side
            if (co-row['elbow']).dot(row['normal'])>=-1e-7:return 'Forearm_'+side
    return 'Body'
def tri_count(obj):obj.data.calc_loop_triangles();return len(obj.data.loop_triangles)
def hand_count(obj):
    obj.data.calc_loop_triangles()
    return sum(region(sum((obj.data.vertices[i].co for i in t.vertices),Vector())/3).startswith('Hand') for t in obj.data.loop_triangles)
def duplicate(obj,name):
    out=obj.copy();out.data=obj.data.copy();bpy.context.collection.objects.link(out);out.name=name;return out
def reduce(obj,ratio,mode,factor):
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    if mode!='none':
        group=obj.vertex_groups.new(name='HandPriority')
        indices=[v.index for v in obj.data.vertices if region(v.co).startswith('Hand')]
        group.add(indices,1,'REPLACE')
    mod=obj.modifiers.new('SurfaceReduction','DECIMATE');mod.decimate_type='COLLAPSE';mod.ratio=ratio;mod.use_collapse_triangulate=True
    if mode!='none':mod.vertex_group=group.name;mod.vertex_group_factor=factor;mod.invert_vertex_group=(mode=='inverted')
    bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.validate(verbose=False,clean_customdata=False)
    for group in list(obj.vertex_groups):obj.vertex_groups.remove(group)
    obj.data.update()
if args.operation=='probe':
    rows=[]
    for mode,factor in [('none',1),('direct',1),('direct',1000),('inverted',1000)]:
        o=duplicate(source,'Probe');reduce(o,.38,mode,factor)
        rows.append({'mode':mode,'factor':factor,'triangles':tri_count(o),'hand_triangles':hand_count(o)})
        bpy.data.objects.remove(o,do_unlink=True)
    result={'source_triangles':tri_count(source),'source_hand_triangles':hand_count(source),'candidates':rows}
    (REPORTS/'reduction-probe.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result));sys.exit(0)

assert not (OUT/'IR_Protagonist_LOD0.fbx').exists() or args.revise_draft,'Never overwrite a delivered asset'
OUT.mkdir(parents=True,exist_ok=True)
tempdir=ROOT/'Tools/IronRequiem/Blender/Working/ProtagonistExportTemp'
tempdir.mkdir(parents=True,exist_ok=True);tempfile.tempdir=str(tempdir)
probe=json.loads((REPORTS/'reduction-probe.json').read_text())
best=max(probe['candidates'],key=lambda r:r['hand_triangles'])

# Real 2K copies of the original maps; never write the source FBX's .fbm paths.
tex=OUT/'Textures';tex.mkdir(exist_ok=True)
maps={}
for image in list(bpy.data.images):
    if not image.packed_file:continue
    name=image.name.lower()
    channel='Normal' if 'normal' in name else 'Roughness' if 'roughness' in name else 'Metallic' if 'metallic' in name else 'BaseColor'
    path=tex/f'IR_Protagonist_{channel}.png'
    image.colorspace_settings.name='sRGB' if channel=='BaseColor' else 'Non-Color'
    image.scale(2048,2048);image.filepath_raw=str(path);image.file_format='PNG';image.save()
    if image.packed_file:image.unpack(method='REMOVE')
    image.reload();maps[channel]=image
assert set(maps)=={'BaseColor','Normal','Roughness','Metallic'}
import numpy as np
n=2048*2048*4
metal=np.empty(n,dtype=np.float32);rough=np.empty(n,dtype=np.float32)
maps['Metallic'].pixels.foreach_get(metal);maps['Roughness'].pixels.foreach_get(rough)
packed=np.ones((n//4,4),dtype=np.float32)
packed[:,:3]=metal.reshape(-1,4)[:,:1];packed[:,3]=1-rough.reshape(-1,4)[:,0]
metal_smooth=bpy.data.images.new('IR_Protagonist_MetalSmooth',width=2048,height=2048,alpha=True)
metal_smooth.colorspace_settings.name='Non-Color';metal_smooth.pixels.foreach_set(packed.ravel())
metal_smooth.filepath_raw=str(tex/'IR_Protagonist_MetalSmooth.png');metal_smooth.file_format='PNG';metal_smooth.save()

def graphite_uv():
    # One flat atlas sample for hidden joint caps, using the same material/submesh.
    image=maps['BaseColor'];data=np.empty(n,dtype=np.float32);image.pixels.foreach_get(data)
    rgb=data.reshape(2048,2048,4)[16:-16:8,16:-16:8,:3]
    brightness=rgb.mean(axis=2);neutral=rgb.max(axis=2)-rgb.min(axis=2)
    score=brightness+neutral*3
    y,x=np.unravel_index(score.argmin(),score.shape)
    return Vector(((16+x*8+.5)/2048,(16+y*8+.5)/2048))
cap_uv=graphite_uv()

def partition(obj,level):
    # Bisect before splitting: both sides share precisely the same cut vertices.
    bm=bmesh.new();bm.from_mesh(obj.data)
    for row in landmarks.values():
        for point in (row['elbow'],row['wrist']):
            bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,plane_co=point,plane_no=row['normal'],clear_inner=False,clear_outer=False)
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    out=[]
    for part in ('Body','Forearm_L','Forearm_R','Hand_L','Hand_R'):
        copy=duplicate(obj,part+'_LOD'+str(level));work=bmesh.new();work.from_mesh(copy.data)
        remove=[f for f in work.faces if region(f.calc_center_median())!=part]
        bmesh.ops.delete(work,geom=remove,context='FACES')
        isolated=[v for v in work.verts if not v.link_faces]
        bmesh.ops.delete(work,geom=isolated,context='VERTS')
        boundary=[e for e in work.edges if e.is_boundary]
        caps=bmesh.ops.holes_fill(work,edges=boundary,sides=0)['faces'] if boundary else []
        uv=work.loops.layers.uv.active
        for face in caps:
            face.smooth=False
            if uv:
                for loop in face.loops:loop[uv].uv=cap_uv
        work.normal_update()
        bad=[e for e in work.edges if e.is_boundary or len(e.link_faces)>2]
        if bad:
            debug={'part':part,'level':level,'boundary_before_fill':len(boundary),'caps':len(caps),'boundary_after':sum(e.is_boundary for e in work.edges),
                   'nonmanifold_after':sum(len(e.link_faces)>2 for e in work.edges),'edges':[{'faces':len(e.link_faces),'verts':[list(v.co) for v in e.verts]} for e in bad[:20]]}
            (REPORTS/'cut-diagnostic.json').write_text(json.dumps(debug,indent=2));print(json.dumps(debug))
        assert not bad,(part,'Open joint cap')
        work.to_mesh(copy.data);work.free();copy.data.validate(verbose=False,clean_customdata=False);copy.data.update()
        if part!='Body':
            side=part[-1];pivot=landmarks[side]['wrist' if part.startswith('Hand') else 'elbow']
            for vertex in copy.data.vertices:vertex.co-=pivot
            copy.location=pivot
        out.append(copy)
    bpy.data.objects.remove(obj,do_unlink=True)
    return out

def quality(obj,reference):
    tree=BVHTree.FromPolygons([v.co for v in reference.data.vertices],[list(p.vertices) for p in reference.data.polygons])
    values=[tree.find_nearest(obj.matrix_world@v.co)[3] for v in obj.data.vertices if region(obj.matrix_world@v.co).startswith('Hand')]
    values.sort()
    return {'samples':len(values),'p95_m':values[int(.95*(len(values)-1))] if values else 0,'max_m':values[-1] if values else 0}

def socket(name,point,parent):
    o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.parent=parent;o.location=point
    # Empty local +Z aims outward from the wrist along the forearm's distal axis.
    if name.startswith(('Elbow_','Wrist_','ModuleSocket_')):
        side=name.split('_')[1];o.rotation_euler=landmarks[side]['normal'].to_track_quat('Z','Y').to_euler()
    return o

def export(objects,path):
    names={o:o.name for o in objects if o.type=='EMPTY' and o.name.startswith(('Elbow_','Wrist_','ModuleSocket_'))}
    for o,name in names.items():o.name=name.split('_LOD')[0]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.hide_set(False);o.select_set(True)
    if path.suffix=='.fbx':
        bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',use_mesh_modifiers=False)
    else:
        bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_animations=False,export_cameras=False,export_lights=False)
    for o,name in names.items():o.name=name

result={'sources':{str(SOURCE):sha(SOURCE)},'source_triangles':tri_count(source),'source_rig':False,'source_animations':0,'output_rig':False,
        'height_m':1.8,'scale_from_source':scale,'blender_forward':'-Y','blender_up':'+Z','normalization_offset_source':list(offset),
        'selection_method':'Measured elbow/wrist planes; bisect first, then partition; all cuts capped with shared atlas material',
        'reduction_mode':best,'landmarks':{s:{k:list(v) if isinstance(v,Vector) else v for k,v in row.items()} for s,row in landmarks.items()},'lods':[]}
all_levels=[]
for level,ratio in enumerate((.38,.20,.10)):
    obj=duplicate(source,'Working_LOD'+str(level));reduce(obj,ratio,best['mode'] if level==0 else 'none',1000 if level==0 else 1)
    objects=partition(obj,level)
    root=bpy.data.objects.new('IR_Protagonist_Root_LOD'+str(level),None);bpy.context.collection.objects.link(root)
    for o in objects:o.parent=root
    empties=[root]
    for side,row in landmarks.items():
        for role in ('Elbow','Wrist','ModuleSocket'):
            empties.append(socket(role+'_'+side+'_LOD'+str(level),row['elbow' if role=='Elbow' else 'wrist'],root))
    bpy.context.view_layer.update()
    data={'level':level,'mode':best['mode'] if level==0 else 'none','parts':[]}
    for o in objects:
        data['parts'].append({'name':o.name,'vertices':len(o.data.vertices),'triangles':tri_count(o),'materials':len(o.data.materials),
                              'pivot_blender_m':list(o.location),'hand_surface_error':quality(o,source) if o.name.startswith('Hand') else None})
    data['triangles']=sum(p['triangles'] for p in data['parts']);result['lods'].append(data)
    export(objects+empties,OUT/f'IR_Protagonist_LOD{level}.fbx')
    export(objects+empties,OUT/f'IR_Protagonist_LOD{level}.glb')
    all_levels.append({'meshes':objects,'empties':empties})

# Separate FPS assembly, same mesh data and joint coordinates, not a second character controller.
fps=[o for o in all_levels[0]['meshes'] if not o.name.startswith('Body')]+all_levels[0]['empties']
export(fps,OUT/'IR_Protagonist_FPS_Parts.fbx');export(fps,OUT/'IR_Protagonist_FPS_Parts.glb')
result['fps_triangles']=sum(tri_count(o) for o in fps if o.type=='MESH')
result['socket_axis']='Empty local +Z follows elbow-to-wrist direction; root remains in normalized Blender axes'
for im in bpy.data.images:
    if im.type=='IMAGE' and im.size[0]:im.pack()
bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.filepaths.file_preview_type='NONE'
for o in [source]+[o for group in all_levels[1:] for o in group['meshes']]:o.hide_render=True;o.hide_set(True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'IR_Protagonist_Editable.blend'))
assert sha(SOURCE)==EXPECTED['source_sha256']
(REPORTS/'geometry.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({'lods':[d['triangles'] for d in result['lods']],'fps_triangles':result['fps_triangles'],'output':str(OUT)}))
