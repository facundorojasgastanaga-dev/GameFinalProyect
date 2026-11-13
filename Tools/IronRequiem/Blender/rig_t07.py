"""Author a new hybrid robot skin for all three LODs; preserve input FBX files."""
import bpy, json, math, hashlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Assets/IronRequiem/Art/Enemies/T07Animated'
REPORT=ROOT/'Tools/IronRequiem/Reports/T07Animation'
OUT.mkdir(parents=True,exist_ok=True);REPORT.mkdir(parents=True,exist_ok=True)
assert not (OUT/'T07_SharedRig.fbx').exists(),'Do not overwrite delivered rig'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bodies=[];source_hashes={};records=[]
for lod in range(3):
    path=ROOT/f'Assets/IronRequiem/Art/Enemies/T07Optimized/T07_LOD{lod}.fbx'
    source_hashes[str(path)]=hashlib.file_digest(path.open('rb'),'sha256').hexdigest()
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    body=bpy.data.objects[f'Body_LOD{lod}'];bodies.append(body)
    cannon=bpy.data.objects[f'CannonModule_LOD{lod}'];bpy.data.objects.remove(cannon,do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)

spec=[('Root',None,(0,0,0),(0,0,.2)),
 ('Hips','Root',(0,0,.96),(0,0,1.2)),('Spine','Hips',(0,0,1.2),(0,0,1.44)),
 ('Chest','Spine',(0,0,1.44),(0,0,1.65)),('Head','Chest',(0,0,1.7),(0,0,1.97))]
for side,s in [('L',-1),('R',1)]:
    spec += [(f'UpperArm_{side}','Chest',(s*.30,0,1.49),(s*.32,-.015,1.1)),
      (f'Forearm_{side}',f'UpperArm_{side}',(s*.32,-.015,1.1),(s*.34,-.025,.76)),
      (f'Hand_{side}',f'Forearm_{side}',(s*.34,-.025,.76),(s*.34,-.03,.65)),
      (f'Thigh_{side}','Hips',(s*.145,0,.95),(s*.165,-.005,.535)),
      (f'Shin_{side}',f'Thigh_{side}',(s*.165,-.005,.535),(s*.17,-.015,.12)),
      (f'Foot_{side}',f'Shin_{side}',(s*.17,-.015,.12),(s*.17,-.165,.065))]
armature=bpy.data.armatures.new('T07SharedSkeleton');rig=bpy.data.objects.new('T07Rig',armature)
bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name,parent,head,tail in spec:
    bone=armature.edit_bones.new(name);bone.head=head;bone.tail=tail
    if parent:bone.parent=armature.edit_bones[parent]
    bone.use_deform=True
bpy.ops.object.mode_set(mode='OBJECT')

def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def blend(low,high,a,b,z):
    t=smooth(a,b,z);return {low:1-t,high:t}
def weights(v):
    x,y,z=v;side='L' if x<0 else 'R';a=abs(x)
    if a>.235 and z>1.08 or a>.275 and z>.65:
        if z<.79:return blend(f'Hand_{side}',f'Forearm_{side}',.72,.81,z)
        if z<1.19:return blend(f'Forearm_{side}',f'UpperArm_{side}',1.06,1.2,z)
        # Narrow shoulder blend; the rest of each plate remains rigid.
        return blend(f'UpperArm_{side}','Chest',1.45,1.59,z) if a<.30 else {f'UpperArm_{side}':1}
    if z<1.02:
        if z<.18:return blend(f'Foot_{side}',f'Shin_{side}',.10,.18,z)
        if z<.61:return blend(f'Shin_{side}',f'Thigh_{side}',.48,.61,z)
        if z>.90:return blend(f'Thigh_{side}','Hips',.90,1.02,z)
        return {f'Thigh_{side}':1}
    if z>1.62:return blend('Chest','Head',1.62,1.74,z)
    if z>1.32:return blend('Spine','Chest',1.32,1.44,z)
    return blend('Hips','Spine',1.09,1.25,z)

for body in bodies:
    for name,*_ in spec:body.vertex_groups.new(name=name)
    histogram={name:0 for name,*_ in spec};mixed=0
    for v in body.data.vertices:
        w={n:a for n,a in weights(v.co).items() if a>1e-6}
        if len(w)>1:mixed+=1
        for name,amount in w.items():body.vertex_groups[name].add([v.index],amount,'REPLACE');histogram[name]+=1
    modifier=body.modifiers.new('SharedRobotSkin','ARMATURE');modifier.object=rig
    body.parent=rig;body.data.calc_loop_triangles()
    records.append({'name':body.name,'vertices':len(body.data.vertices),'triangles':len(body.data.loop_triangles),
       'uv_layers':len(body.data.uv_layers),'weighted_vertices':len(body.data.vertices),'mixed_joint_vertices':mixed,
       'maximum_influences':2,'vertices_per_bone':histogram})

bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for body in bodies:body.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(OUT/'T07_SharedRig.fbx'),use_selection=True,
 object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,
 bake_anim=False,use_armature_deform_only=False,use_mesh_modifiers=False,path_mode='STRIP')
working=ROOT/'Tools/IronRequiem/Blender/Working/T07Animated';working.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(working/'T07_SharedRig.blend'))
assert all(hashlib.file_digest(Path(p).open('rb'),'sha256').hexdigest()==h for p,h in source_hashes.items())
(REPORT/'rig-authoring.json').write_text(json.dumps({'sources':source_hashes,'method':'hybrid rigid plates with short two-bone joint blends',
 'bones':[{'name':n,'parent':p,'head_blender':h,'tail_blender':t} for n,p,h,t in spec],
 'lods':records,'cannon':'original independent static LODs, excluded from skin','source_hashes_preserved':True},indent=2))
print(json.dumps({'rig_bones':len(spec),'lods':records}))
