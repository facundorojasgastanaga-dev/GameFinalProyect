"""Render real optimized geometry and separated modules; no AI image editing."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Tools/IronRequiem/Exports/IR_Protagonist_Optimized_v01'
data=json.loads((ROOT/'Tools/IronRequiem/Reports/ProtagonistOptimization/geometry.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(OUT/'IR_Protagonist_Editable.blend'))
previews=OUT/'Previews';previews.mkdir(exist_ok=True)
source=bpy.data.objects['Source_Normalized']
source_root=bpy.data.objects.new('SourceCompare',None);bpy.context.scene.collection.objects.link(source_root);source.parent=source_root
roots=[source_root]+[bpy.data.objects['IR_Protagonist_Root_LOD'+str(i)] for i in range(3)]
for i,root in enumerate(roots):
    root.location.x=(i-1.5)*1.25
    for child in root.children:
        if child.type=='MESH':child.hide_render=False;child.hide_set(False)
    bpy.ops.object.text_add(location=((i-1.5)*1.25-.43,0,1.98),rotation=(math.pi/2,0,0))
    text=bpy.context.object;text.name='CompareLabel'+str(i);text.data.size=.095
    text.data.body='SOURCE 50,000' if i==0 else 'LOD'+str(i-1)+' '+format(data['lods'][i-1]['triangles'],',')
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.eevee.taa_render_samples=32
scene.render.resolution_x=1800;scene.render.resolution_y=950;scene.render.resolution_percentage=100
scene.world.color=(.12,.12,.12);scene.view_settings.view_transform='AgX'
center=Vector((0,0,.95))
for position,power,size in [((0,-4,5),1800,7),((-4,-2,3),900,6),((0,4,4),2100,7)]:
    bpy.ops.object.light_add(type='AREA',location=position)
    light=bpy.context.object;light.data.energy=power;light.data.size=size
    light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(0,-7,.95));scene.camera=bpy.context.object
scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=5.2
scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler()
for view,angle in [('front',0),('side',-math.pi/2),('back',math.pi)]:
    for root in roots:root.rotation_euler.z=angle
    scene.render.filepath=str(previews/f'Comparison_{view}.png');bpy.ops.render.render(write_still=True)
for obj in list(bpy.data.objects):
    if obj.name.startswith('CompareLabel'):bpy.data.objects.remove(obj,do_unlink=True)
for i,root in enumerate(roots):
    root.location.x=0;root.rotation_euler.z=0
    if i!=1:
        for child in root.children:
            if child.type=='MESH':child.hide_render=True
for obj in roots[1].children:
    if obj.name.startswith('Forearm_'):obj.location.x+=(-1 if '_R_' in obj.name else 1)*.12
    elif obj.name.startswith('Hand_'):obj.location.x+=(-1 if '_R_' in obj.name else 1)*.26
scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.camera.data.ortho_scale=2.45
scene.render.filepath=str(previews/'Modular_parts.png');bpy.ops.render.render(write_still=True)
# Close, identical-camera comparison of normal/source and optimized right hand.
for root in roots:
    for child in root.children:
        if child.type=='MESH':child.hide_render=True
source.hide_render=False
for obj in roots[1].children:
    if obj.name.startswith('Hand_R_'):
        obj.location.x+=.26;obj.hide_render=False
        pivot=obj.location.copy()
source.hide_render=True
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.camera.data.ortho_scale=.30
target=pivot+Vector((-.02,-.01,-.08))
scene.camera.location=target+Vector((0,-2,.08))
scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(previews/'Right_hand_LOD0.png');bpy.ops.render.render(write_still=True)
