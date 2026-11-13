import bpy,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Tools/IronRequiem/Exports/IR_NewRobot_Optimized_v01';spec=json.loads((OUT/'Verification/geometry.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(OUT/'IR_NewRobot_Editable.blend'))
source=bpy.data.objects['Source_Reference_50060'];root=bpy.data.objects.new('Original_Reference',None);bpy.context.scene.collection.objects.link(root);source.parent=root
roots=[root]+[bpy.data.objects['IR_NewRobot_LOD'+str(i)] for i in range(3)]
for i,root in enumerate(roots):
 root.location.x=(i-1.5)*1.14
 for o in root.children:
  if o.type=='MESH':o.hide_render=False;o.hide_set(False)
 bpy.ops.object.text_add(location=((i-1.5)*1.14-.40,-.01,.99),rotation=(math.pi/2,0,0));t=bpy.context.object;t.name='PreviewLabel'+str(i);t.data.size=.067;t.data.body='SOURCE 50,060' if i==0 else 'LOD'+str(i-1)+'  '+format(spec['lods'][i-1]['triangles'],',')
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.eevee.taa_render_samples=32;scene.render.resolution_x=2000;scene.render.resolution_y=550;scene.render.resolution_percentage=100;scene.world.color=(.12,.12,.12);scene.view_settings.view_transform='AgX'
center=Vector((0,0,.49))
for xyz,energy,size in [((0,-3,5),1500,6),((-4,1,3),1000,5),((3,3,4),1600,5)]:
 bpy.ops.object.light_add(type='AREA',location=xyz);o=bpy.context.object;o.data.energy=energy;o.data.size=size;o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(0,-6,.49));scene.camera=bpy.context.object;scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=4.6;scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler()
previews=OUT/'Previews';previews.mkdir(exist_ok=True)
for view,angle in [('front',0),('side',-math.pi/2),('back',math.pi)]:
 for root in roots:root.rotation_euler.z=angle
 scene.render.filepath=str(previews/('Comparison_'+view+'.png'));bpy.ops.render.render(write_still=True)
for o in list(bpy.data.objects):
 if o.name.startswith('PreviewLabel'):bpy.data.objects.remove(o,do_unlink=True)
for i,root in enumerate(roots):
 root.location.x=0;root.rotation_euler.z=0
 for o in root.children:
  if o.type=='MESH':o.hide_render=i!=1
scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.camera.data.ortho_scale=1.15;target=Vector((-.01,0,.43));scene.camera.location=target+Vector((-1.5,-3,1));scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(previews/'Assembled.png');bpy.ops.render.render(write_still=True)
for o in roots[1].children:
 if o.type!='MESH':continue
 name=o.name;side=1 if '_L_' in name else -1
 offset=Vector((0,0,0))
 if name.startswith('Torso'):offset.z=.1
 elif name.startswith('Backpack'):offset.y=.20;offset.z=.15
 elif name.startswith('Pelvis'):offset.z=-.05
 elif name.startswith('Shoulder'):offset.x=side*.10;offset.z=.10
 elif name.startswith('Forearm'):offset.x=side*.25;offset.z=.10
 elif name.startswith('Pincer'):offset.x=side*.25;offset.y=-.20;offset.z=.10
 elif name.startswith('Thigh'):offset.x=side*.05;offset.z=-.16
 elif name.startswith('Shin'):offset.x=side*.09;offset.z=-.28
 elif name.startswith('Foot'):offset.x=side*.13;offset.z=-.42
 o.location+=offset
scene.camera.data.ortho_scale=1.9;target=Vector((-.01,-.03,.24));scene.camera.location=target+Vector((-1.4,-3,1));scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(previews/'Separated_15_parts.png');bpy.ops.render.render(write_still=True)
