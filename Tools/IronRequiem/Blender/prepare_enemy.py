"""Prepare the inspected 50k T-07 copy. World-space selection is specific to this source hash."""
import bpy, bmesh, sys, json, hashlib
from pathlib import Path
from mathutils import Vector
import numpy as np

root = Path(__file__).resolve().parents[3]
source = Path(sys.argv[sys.argv.index('--')+1]).resolve(strict=True)
assert bpy.app.background and '--factory-startup' in sys.argv
assert hashlib.sha256(source.read_bytes()).hexdigest() == '1dcc48cdf250fb66f609167ed5d20722754ca44302e85301a5f0802785fd05d3'
out = root/'Assets/IronRequiem/Art/Enemies/T07New'
out.mkdir(parents=True, exist_ok=True)
target = out/'T07_Prepared.fbx'
assert not target.exists() or '--rebuild' in sys.argv, 'Use --rebuild for this derived output only.'
for o in list(bpy.context.scene.objects): bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(source))
obj = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
# Coordinates were inspected in the source front/side/back renders. The removable
# assembly is the right lower arm + cannon, not an invented disconnected island.
candidate = {p.index for p in obj.data.polygons if p.center.x < -0.12 and 0.275 < p.center.z < 0.625}
vertex_faces = {}
for i in candidate:
    for v in obj.data.polygons[i].vertices: vertex_faces.setdefault(v, set()).add(i)
components=[]
remaining=set(candidate)
while remaining:
    connected=set(); todo=[remaining.pop()]
    while todo:
        i=todo.pop(); connected.add(i)
        for v in obj.data.polygons[i].vertices:
            for j in vertex_faces[v].intersection(remaining):
                remaining.remove(j); todo.append(j)
    components.append(connected)
indices=max(components,key=len)
cannon = obj.copy(); cannon.data = obj.data.copy(); bpy.context.collection.objects.link(cannon)
for part, keep in [(obj, False), (cannon, True)]:
    bm=bmesh.new(); bm.from_mesh(part.data)
    bm.faces.ensure_lookup_table()
    remove=[f for f in bm.faces if (f.index in indices) != keep]
    bmesh.ops.delete(bm, geom=remove, context='FACES')
    bm.to_mesh(part.data); bm.free()
obj.name = 'Body'
cannon.name = 'CannonModule'
scale = 2.05/1.1266160011291504
for o in [obj,cannon]:
    for v in o.data.vertices: v.co *= scale
    bm=bmesh.new(); bm.from_mesh(o.data)
    boundary=[e for e in bm.edges if e.is_boundary]
    if boundary: bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
    bm.to_mesh(o.data); bm.free()
    o.data.update()
bpy.context.view_layer.update()
assert len(obj.data.polygons)>10000 and 1000<len(cannon.data.polygons)<15000
pivot=Vector((-0.165,-0.015,0.60))*scale
for v in cannon.data.vertices: v.co -= pivot
cannon.location=pivot
tex=out/'Textures'; tex.mkdir(exist_ok=True)
for im in list(bpy.data.images):
    if not im.packed_file: continue
    if 'normal' in im.name: name='T07_Normal.png'
    elif 'roughness' in im.name: name='T07_Roughness.png'
    elif 'metallic' in im.name: name='T07_Metallic.png'
    else: name='T07_BaseColor.png'
    im.filepath_raw=str(tex/name); im.file_format='PNG'; im.save()
# URP expects metallic in R and smoothness in A. Preserve raw maps as well.
metal=next(i for i in bpy.data.images if 'metallic' in i.name)
rough=next(i for i in bpy.data.images if 'roughness' in i.name)
n=metal.size[0]*metal.size[1]*4
m=np.empty(n,dtype=np.float32); r=np.empty(n,dtype=np.float32)
metal.pixels.foreach_get(m); rough.pixels.foreach_get(r)
packed=np.ones((n//4,4),dtype=np.float32)
packed[:,:3]=m.reshape(-1,4)[:,:1]; packed[:,3]=1-r.reshape(-1,4)[:,0]
im=bpy.data.images.new('T07_MetalSmooth',width=metal.size[0],height=metal.size[1],alpha=True)
im.colorspace_settings.name='Non-Color'; im.pixels.foreach_set(packed.ravel())
im.filepath_raw=str(tex/'T07_MetalSmooth.png'); im.file_format='PNG'; im.save()
bpy.ops.object.select_all(action='DESELECT')
for o in [obj,cannon]: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
working=root/'Tools/IronRequiem/Blender/Working'; working.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.ops.wm.save_as_mainfile(filepath=str(working/'T07_Working.blend'))
report={'source':str(source),'scale_to_2_05m':scale,'source_armatures':0,'source_weights':0,'source_clips':0,'selection':{'x_max':-0.12,'z_min':0.275,'z_max':0.625,'faces':len(indices),'candidate_components':sorted([len(c) for c in components],reverse=True)},'cannon_pivot_blender':list(pivot),'authored_rig':False,'parts':[]}
for o in [obj,cannon]:
    o.data.calc_loop_triangles()
    report['parts'].append({'name':o.name,'triangles':len(o.data.loop_triangles),'dimensions':list(o.dimensions)})
(root/'Tools/IronRequiem/Reports/new-t07-preparation.json').write_text(json.dumps(report,indent=2))
