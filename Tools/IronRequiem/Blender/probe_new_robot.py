import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[3];R=ROOT/'Tools/IronRequiem/Reports/NewRobotOptimization'
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Tools/IronRequiem/Blender/Working/NewRobot/SourceInspection.blend'))
o=next(o for o in bpy.context.scene.objects if o.type=='MESH');o.data.transform(o.matrix_world);o.matrix_world=Matrix.Identity(4)
coords=[v.co for v in o.data.vertices]
rows=[]
for z in [.1,.14,.18,.22,.26,.30,.34,.38,.42,.46,.50,.54,.58,.62,.66,.70,.74,.78]:
    for side,amin,amax in [('center',0,.12),('arm',.27,.6),('leg',.14,.3)]:
        points=[v for v in coords if abs(v.z-z)<.012 and amin<abs(v.x)<amax]
        if points:rows.append({'z':z,'region':side,'n':len(points),'min':[min(v[i] for v in points) for i in range(3)],'max':[max(v[i] for v in points) for i in range(3)]})
(R/'coordinate-bands.json').write_text(json.dumps(rows,indent=2))
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.eevee.taa_render_samples=16;scene.render.resolution_x=1200;scene.render.resolution_y=1050;scene.render.resolution_percentage=100;scene.world.color=(.1,.1,.1);scene.view_settings.view_transform='AgX';center=Vector((-.01,-.05,.43))
for xyz,power in [((0,-3,4),1100),((-3,1,3),800),((3,3,3),1400)]:
 bpy.ops.object.light_add(type='AREA',location=xyz);l=bpy.context.object;l.data.energy=power;l.data.size=3;l.rotation_euler=(center-l.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();scene.camera=bpy.context.object;scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=1.13
for name,position in [('full-front',(0,-3,.43)),('threequarter',(-2,-3,1.1)),('top',(0,-.03,3))]:
 scene.camera.location=position;scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(R/('source-'+name+'.png'));bpy.ops.render.render(write_still=True)
