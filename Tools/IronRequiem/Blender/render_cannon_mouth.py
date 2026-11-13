"""Read-only geometry evidence of the optimized cannon, no source writes."""
import bpy, math
from pathlib import Path
from mathutils import Vector
r=Path(__file__).resolve().parents[3]
out=r/'Tools/IronRequiem/Reports/CannonCharge'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(r/'Assets/IronRequiem/Art/Enemies/T07Optimized/T07_LOD0.fbx'))
cannon=next(o for o in bpy.context.scene.objects if o.type=='MESH' and 'Cannon' in o.name)
for o in list(bpy.context.scene.objects):
 if o!=cannon:bpy.data.objects.remove(o,do_unlink=True)
cannon.matrix_world.identity()
mat=bpy.data.materials.new('Inspection graphite');mat.diffuse_color=(.2,.25,.3,1);cannon.data.materials.clear();cannon.data.materials.append(mat)
sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sc.render.resolution_x=1000;sc.render.resolution_y=1000;sc.render.resolution_percentage=100
sc.display.shading.light='STUDIO';sc.display.shading.color_type='MATERIAL';sc.display.shading.show_cavity=True;sc.display.shading.cavity_type='BOTH';sc.display.shading.show_shadows=True;sc.display.shading.show_object_outline=True
sc.world=bpy.data.worlds.new('World');sc.world.color=(.08,.08,.08)
bpy.ops.object.camera_add();cam=bpy.context.object;sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=.85
target=Vector((0,-.06,-.30))
for name,position in [('front',(0,-.05,-2)),('side',(1.4,-.5,-1.4)),('rear',(0,-.05,2))]:
 cam.location=position;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(out/f'geometry-{name}.png');bpy.ops.render.render(write_still=True)
