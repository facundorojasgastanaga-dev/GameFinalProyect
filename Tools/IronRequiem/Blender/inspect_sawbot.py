import bpy,sys,json,hashlib,importlib.util,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3];REPORT=ROOT/'Tools/IronRequiem/Reports/SawBot';WORK=ROOT/'Tools/IronRequiem/Blender/Working/SawBot';WORK.mkdir(parents=True,exist_ok=True)
assert bpy.app.background and '--factory-startup' in sys.argv
spec=importlib.util.spec_from_file_location('asset_tool',ROOT/'Tools/IronRequiem/Blender/blender_asset_tool.py');tool=importlib.util.module_from_spec(spec);spec.loader.exec_module(tool)
for name in ['sawbot','sawbot2']:
 path=Path('C:/Users/gehbd/OneDrive/Escritorio/Juegos GJ/SawBOT')/(name+'.fbx')
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True);bpy.context.view_layer.update()
 report=tool.inspect_model(path,tool.sha256(path));coords=[o.matrix_world@v.co for o in bpy.context.scene.objects if o.type=='MESH' for v in o.data.vertices];lo=Vector([min(v[i] for v in coords) for i in range(3)]);hi=Vector([max(v[i] for v in coords) for i in range(3)])
 report['bounds']={'min':list(lo),'max':list(hi),'dimensions':list(hi-lo)}
 for o in bpy.context.scene.objects:
  if o.type=='MESH':
   h=hashlib.sha256();h.update(str([tuple(round(x,6) for x in v.co) for v in o.data.vertices]).encode());report.setdefault('geometry_hashes',{})[o.name]=h.hexdigest()
 (REPORT/(name+'-inspection.json')).write_text(json.dumps(report,indent=2))
 print(name,json.dumps({'objects':[(o['name'],o['type'],o.get('triangles'),len(o.get('vertex_groups',[]))) for o in report['objects']],'bounds':report['bounds'],'actions':report['actions'],'materials':report['materials']}),flush=True)
 bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(WORK/(name+'_source.blend')))
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True;scene.render.resolution_x=768;scene.render.resolution_y=768;scene.render.resolution_percentage=100;scene.world=bpy.data.worlds.new('PreviewWorld');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.12,.12,1);scene.view_settings.view_transform='AgX';center=(lo+hi)/2;size=max(hi-lo)
 for xyz,energy in [((-2,-3,4),900),((3,-1,2),650),((0,3,3),1000)]:
  bpy.ops.object.light_add(type='AREA',location=center+Vector(xyz)*size);light=bpy.context.object;light.data.energy=energy*size*size;light.data.shape='DISK';light.data.size=size*3;light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
 bpy.ops.object.camera_add();scene.camera=bpy.context.object;scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=size*1.2;scene.camera.data.clip_end=size*100
 for label,xyz in [('front',(0,-3,.1)),('side',(3,0,.1))]:
  scene.camera.location=center+Vector(xyz)*size;scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(REPORT/(name+'-'+label+'.png'));bpy.ops.render.render(write_still=True)