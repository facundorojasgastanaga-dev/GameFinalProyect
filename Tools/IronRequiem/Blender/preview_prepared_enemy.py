import bpy, math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[3]
bpy.ops.wm.open_mainfile(filepath=str(root/'Tools/IronRequiem/Blender/Working/T07_Working.blend'))
scene=bpy.context.scene
cannon=bpy.data.objects['CannonModule']
cannon.location.x-=0.45
scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.world.color=(0.18,0.18,0.18)
center=Vector((-0.15,0,1))
for pos in [(3,-4,5),(-3,-2,3),(0,4,4)]:
    bpy.ops.object.light_add(type='AREA', location=pos)
    bpy.context.object.data.energy=650;bpy.context.object.data.size=4
    bpy.context.object.rotation_euler=(center-bpy.context.object.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(-0.15,-6,1.2))
scene.camera=bpy.context.object;scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=2.6
scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(root/'Tools/IronRequiem/Reports/new-t07-separated.png')
bpy.ops.render.render(write_still=True)
