"""Read-only pose study: same fitted mouth/aim, rotate only the equipped shell."""
import bpy, math,json
from mathutils import Vector,Matrix,Quaternion
from pathlib import Path
r=Path(__file__).resolve().parents[3];out=r/'Tools/IronRequiem/Reports/CannonCharge'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(r/'Assets/IronRequiem/Art/Enemies/T07Optimized/T07_LOD0.fbx'))
gun=next(o for o in bpy.context.scene.objects if o.type=='MESH' and 'Cannon' in o.name)
for o in list(bpy.context.scene.objects):
 if o!=gun:bpy.data.objects.remove(o,do_unlink=True)
gun.matrix_world.identity()
for v in gun.data.vertices:v.co=Vector((-v.co.x,v.co.y,v.co.z))*.01
gun.data.update()
mat=bpy.data.materials.new('PoseStudy');mat.diffuse_color=(.25,.3,.35,1);gun.data.materials.clear();gun.data.materials.append(mat)
fit=json.loads((out/'mouth-fit.json').read_text());point=Vector(fit['unity_mesh_local_center']);normal=Vector(fit['unity_mesh_local_forward']);mouth=Vector((.279,-.282,.945))
base=Quaternion(Vector((1,0,0)),math.pi);rotation=(base@normal).rotation_difference(Vector((0,0,1)))@base
bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=.03,location=mouth)
core=bpy.context.object;mat2=bpy.data.materials.new('MouthMarker');mat2.diffuse_color=(1,.02,1,1);core.data.materials.append(mat2)
sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sc.render.resolution_x=960;sc.render.resolution_y=540;sc.render.resolution_percentage=100
sc.display.shading.light='STUDIO';sc.display.shading.color_type='MATERIAL';sc.display.shading.show_cavity=True;sc.display.shading.cavity_type='BOTH'
bpy.ops.object.camera_add(location=(0,0,0));cam=bpy.context.object;sc.camera=cam;cam.rotation_euler=Vector((0,0,1)).to_track_quat('-Z','Y').to_euler();cam.data.lens_unit='FOV';cam.data.angle=math.radians(91.49)
for roll in [-90,-45,0,45,90,135,180]:
 rot=Quaternion(Vector((0,0,1)),math.radians(roll))@rotation
 gun.rotation_mode='QUATERNION';gun.rotation_quaternion=rot;gun.scale=(100,100,100);gun.location=mouth-rot@(point*100)
 sc.render.filepath=str(out/f'pose-roll-{roll}.png');bpy.ops.render.render(write_still=True)
