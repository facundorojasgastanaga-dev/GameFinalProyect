"""Render actual original and reduced geometry side by side, using identical lighting."""
import bpy
from pathlib import Path
from mathutils import Vector
import math, json

ROOT=Path(__file__).resolve().parents[3]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Tools/IronRequiem/Blender/Working/T07Optimized/T07_Optimized.blend'))
scene=bpy.context.scene
meshes=[o for o in scene.objects if o.type=='MESH']
geometry=json.loads((ROOT/'Tools/IronRequiem/Reports/T07Optimization/geometry.json').read_text())
groups=[]
for index, suffix in enumerate(('', '_LOD0', '_LOD1', '_LOD2')):
    parent=bpy.data.objects.new('Compare'+str(index),None)
    scene.collection.objects.link(parent)
    parent.location.x=(index-1.5)*1.4
    for name in ('Body','CannonModule'):
        obj=bpy.data.objects[name+suffix]
        obj.hide_render=False; obj.hide_set(False)
        obj.parent=parent
    groups.append(parent)
    bpy.ops.object.text_add(location=((index-1.5)*1.4-.52,0,2.28),rotation=(math.pi/2,0,0))
    text=bpy.context.object
    text.data.body=('SOURCE '+format(sum(p['triangles'] for p in geometry['source_parts']),',')) if index==0 else ('LOD'+str(index-1)+' '+format(geometry['lods'][index-1]['triangles'],','))
    text.data.size=.105
    material=bpy.data.materials.get('Label') or bpy.data.materials.new('Label')
    material.diffuse_color=(.8,.8,.8,1)
    text.data.materials.append(material)
scene.render.engine='BLENDER_EEVEE'
scene.eevee.taa_render_samples=24
scene.render.resolution_x=1800;scene.render.resolution_y=950;scene.render.resolution_percentage=100
scene.world.color=(.12,.12,.12)
scene.view_settings.view_transform='AgX'
center=Vector((0,0,1.1))
for position,power,size in [((0,-5,6),1800,8),((-4,-2,3),900,6),((0,4,5),2200,7)]:
    bpy.ops.object.light_add(type='AREA',location=position)
    light=bpy.context.object;light.data.energy=power;light.data.size=size
    light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(0,-8,1.12))
scene.camera=bpy.context.object
scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=5.8
scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler()
for view, angle in [('front',0),('side',-math.pi/2),('back',math.pi)]:
    for group in groups: group.rotation_euler.z=angle
    scene.render.filepath=str(ROOT/f'Tools/IronRequiem/Reports/T07Optimization/comparison-{view}.png')
    bpy.ops.render.render(write_still=True)
