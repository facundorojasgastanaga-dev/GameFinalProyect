import bpy,json,sys,hashlib,math,importlib.util
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
WORK=ROOT/'Tools/IronRequiem/Blender/Working/NewRobot'
REPORT=ROOT/'Tools/IronRequiem/Reports/NewRobotOptimization'
SOURCE=ROOT/'Tools/IronRequiem/SourceModels/NewRobot_v01'
spec=importlib.util.spec_from_file_location('asset_tool',ROOT/'Tools/IronRequiem/Blender/blender_asset_tool.py');tool=importlib.util.module_from_spec(spec);spec.loader.exec_module(tool)
assert bpy.app.background and '--factory-startup' in sys.argv
for ext in ('fbx','glb'):
    for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
    path=next(SOURCE.glob('*.'+ext))
    if ext=='fbx':bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True)
    else:bpy.ops.import_scene.gltf(filepath=str(path))
    bpy.context.view_layer.update()
    data=tool.inspect_model(path,tool.sha256(path))
    coords=[o.matrix_world@v.co for o in bpy.context.scene.objects if o.type=='MESH' for v in o.data.vertices]
    low=Vector(tuple(min(v[i] for v in coords) for i in range(3)));high=Vector(tuple(max(v[i] for v in coords) for i in range(3)))
    data['bounds']={'min':list(low),'max':list(high),'dimensions':list(high-low)}
    (REPORT/(ext+'-inspection.json')).write_text(json.dumps(data,indent=2),encoding='utf8')
    print(json.dumps({'source':ext,'meshes':[{'name':o['name'],'triangles':o.get('triangles'),'islands':len(o.get('loose_island_vertex_counts',[])),'weights':len(o.get('vertex_groups',[]))} for o in data['objects'] if o['type']=='MESH'],'armatures':sum(o['type']=='ARMATURE' for o in data['objects']),'actions':data['actions'],'bounds':data['bounds'],'materials':data['materials']}))
    if ext!='fbx':continue
    bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.filepaths.file_preview_type='NONE'
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'SourceInspection.blend'))
    scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.eevee.taa_render_samples=16
    scene.render.resolution_x=800;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    scene.world.color=(.15,.15,.15);scene.view_settings.view_transform='AgX'
    center=(low+high)/2;height=high.z-low.z
    for xyz,power in [((0,-3,4),1200),((-3,1,3),1000),((3,3,3),1600)]:
        bpy.ops.object.light_add(type='AREA',location=center+Vector(xyz)*height/1.8)
        o=bpy.context.object;o.data.energy=power*(height/1.8)**2;o.data.size=height*2;o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add();scene.camera=bpy.context.object;scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=height*1.16
    for name,angle in [('front',0),('side',math.pi/2),('back',math.pi)]:
        scene.camera.location=center+Vector((math.sin(angle),-math.cos(angle),.04))*height*3
        scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(REPORT/('source-'+name+'.png'));bpy.ops.render.render(write_still=True)
