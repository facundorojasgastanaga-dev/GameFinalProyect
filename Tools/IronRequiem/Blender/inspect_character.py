"""Read-only FBX/GLB inspection in a disposable Blender process, with rendered evidence."""
import sys, json, hashlib, argparse, importlib.util, math
from pathlib import Path
import bpy
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument('--input', required=True)
parser.add_argument('--report', required=True)
parser.add_argument('--preview', required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
args.report = str(Path(args.report).resolve())
args.preview = str(Path(args.preview).resolve())
assert bpy.app.background and '--factory-startup' in sys.argv
source = Path(args.input).resolve(strict=True)
digest = lambda: hashlib.sha256(source.read_bytes()).hexdigest()
before = digest()
for obj in list(bpy.context.scene.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
if source.suffix.lower() == '.fbx':
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=True)
else:
    bpy.ops.import_scene.gltf(filepath=str(source))
bpy.context.view_layer.update()
spec = importlib.util.spec_from_file_location('base_inspector', Path(__file__).with_name('blender_asset_tool.py'))
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)
report = base.inspect_model(source, before)
for obj, data in zip(bpy.context.scene.objects, report['objects']):
    if obj.type == 'MESH':
        weighted = [v for v in obj.data.vertices if any(g.weight > 0 for g in v.groups)]
        data['weighted_vertices'] = len(weighted)
        data['weight_sum_min_max'] = [min((sum(g.weight for g in v.groups) for v in weighted), default=0), max((sum(g.weight for g in v.groups) for v in weighted), default=0)]
        data['armature_targets'] = [m.object.name if m.object else None for m in obj.modifiers if m.type == 'ARMATURE']
report['action_details'] = [{'name': a.name, 'frame_range': list(a.frame_range), 'slots': len(a.slots)} for a in bpy.data.actions]
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
low = Vector(tuple(min(c[i] for c in corners) for i in range(3)))
high = Vector(tuple(max(c[i] for c in corners) for i in range(3)))
report['combined_bounds'] = {'min': list(low), 'max': list(high), 'size': list(high-low)}
Path(args.report).parent.mkdir(parents=True, exist_ok=True)
Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
center = (low+high)/2
span = max(high-low)
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 900
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.world.color = (0.18,0.18,0.18)
scene.view_settings.view_transform = 'AgX'
for offset, power in [((3,-4,5),1100),((-3,-2,3),850),((0,4,4),1400)]:
    bpy.ops.object.light_add(type='AREA', location=center+Vector(offset)*span)
    bpy.context.object.data.energy = power*span*span
    bpy.context.object.data.shape = 'DISK'
    bpy.context.object.data.size = span*3
    bpy.context.object.rotation_euler = (center-bpy.context.object.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add()
scene.camera = bpy.context.object
scene.camera.data.type = 'ORTHO'
scene.camera.data.ortho_scale = span*1.3
prefix = Path(args.preview)
prefix.parent.mkdir(parents=True, exist_ok=True)
for name, offset in [('front',(0,-4,0.2)),('back',(0,4,0.2)),('side',(4,0,0.2))]:
    scene.camera.location = center+Vector(offset)*span
    scene.camera.rotation_euler = (center-scene.camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath = str(prefix.with_name(prefix.name+'-'+name+'.png'))
    bpy.ops.render.render(write_still=True)
assert before == digest(), 'Source changed!'
report['source_unchanged'] = True
Path(args.report).parent.mkdir(parents=True, exist_ok=True)
Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps({'report': args.report, 'bounds':report['combined_bounds'], 'objects':len(report['objects']), 'actions': report['actions']}))
