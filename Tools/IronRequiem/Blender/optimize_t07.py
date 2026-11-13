"""Local T07 LOD authoring. Run in a disposable Blender process; never overwrite sources."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[3]
REPORTS = ROOT / 'Tools/IronRequiem/Reports/T07Optimization'
OUT = ROOT / 'Assets/IronRequiem/Art/Enemies/T07Optimized'
SOURCES = [Path('C:/Users/gehbd/OneDrive/Escritorio/Juegos GJ/vistasROBOT/T-07.fbx'),
           Path('C:/Users/gehbd/OneDrive/Escritorio/Juegos GJ/vistasROBOT/T-07(Texture).glb')]
PREPARED = ROOT / 'Assets/IronRequiem/Art/Enemies/T07New/T07_Prepared.fbx'


def digest(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


def clear():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def stats(obj):
    obj.data.calc_loop_triangles()
    return {'name': obj.name, 'vertices': len(obj.data.vertices),
            'triangles': len(obj.data.loop_triangles), 'materials': len(obj.data.materials),
            'uv_layers': [u.name for u in obj.data.uv_layers],
            'dimensions': list(obj.dimensions), 'location': list(obj.location),
            'rotation': list(obj.rotation_euler), 'scale': list(obj.scale),
            'weighted_vertices': sum(any(g.weight > 0 for g in v.groups) for v in obj.data.vertices)}


def inspect():
    spec = importlib.util.spec_from_file_location('base', Path(__file__).with_name('blender_asset_tool.py'))
    base = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(base)
    result = []
    for path in SOURCES + [PREPARED]:
        before = digest(path)
        clear()
        if path.suffix == '.fbx':
            bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True)
        else:
            bpy.ops.import_scene.gltf(filepath=str(path))
        bpy.context.view_layer.update()
        data = base.inspect_model(path, before)
        data['mesh_details'] = [stats(o) for o in bpy.context.scene.objects if o.type == 'MESH']
        assert digest(path) == before
        data['source_unchanged'] = True
        result.append(data)
    (REPORTS / 'inspection.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps([{'source': d['source'], 'sha256': d['source_sha256'],
                       'meshes': d['mesh_details'], 'actions': d['actions'],
                       'armatures': sum(o['type'] == 'ARMATURE' for o in d['objects'])} for d in result]))


def surface_error(a, b):
    def tree(obj):
        return BVHTree.FromPolygons([v.co for v in obj.data.vertices],
                                   [list(p.vertices) for p in obj.data.polygons])
    def distances(source, target_tree):
        vertices = source.data.vertices
        step = max(1, len(vertices) // 4000)
        return [target_tree.find_nearest(vertices[i].co)[3] for i in range(0, len(vertices), step)]
    # Both directions, in local coordinates shared by each original/LOD pair.
    ds = sorted(distances(a, tree(b)) + distances(b, tree(a)))
    return {'samples': len(ds), 'mean_m': sum(ds)/len(ds),
            'p95_m': ds[int(.95*(len(ds)-1))], 'max_m': ds[-1]}


def generate():
    expected = json.loads((REPORTS/'inspection.json').read_text(encoding='utf-8'))
    for data in expected:
        assert digest(Path(data['source'])) == data['source_sha256'], 'Inspection is stale'
    assert expected[0]['source_sha256'] == json.loads((ROOT/'Tools/IronRequiem/Reports/new-t07-fbx.json').read_text())['source_sha256'], 'Prepared copy belongs to a different FBX'
    assert not (OUT/'T07_LOD0.fbx').exists() or args.revise_draft, 'Never overwrite a delivered LOD'
    OUT.mkdir(parents=True, exist_ok=True)
    clear()
    bpy.ops.import_scene.fbx(filepath=str(PREPARED), use_anim=False)
    originals = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    assert {o.name for o in originals} == {'Body', 'CannonModule'}
    assert not any(o.type == 'ARMATURE' for o in bpy.context.scene.objects)
    for o in originals:
        assert not o.vertex_groups
    result = {'source_hashes': {d['source']: d['source_sha256'] for d in expected},
              'method': 'Blender Decimate COLLAPSE on each prepared part; UV attributes retained',
              'rig': False, 'animation_clips': 0, 'source_parts': [stats(o) for o in originals], 'lods': []}
    all_lods = []
    for level, ratio in enumerate((.24, .12, .05)):
        copies = []
        data = {'level': level, 'ratio': ratio, 'parts': []}
        for src in originals:
            obj = src.copy()
            obj.data = src.data.copy()
            bpy.context.collection.objects.link(obj)
            obj.name = src.name + '_LOD' + str(level)
            bpy.ops.object.select_all(action='DESELECT')
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
            mod = obj.modifiers.new('TopologyReduction', 'DECIMATE')
            mod.decimate_type = 'COLLAPSE'
            mod.ratio = ratio
            mod.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier=mod.name)
            obj.data.validate(verbose=True, clean_customdata=False)
            obj.data.update()
            bpy.context.view_layer.update()
            entry = stats(obj)
            entry['surface_error'] = surface_error(src, obj)
            data['parts'].append(entry)
            assert len(obj.data.uv_layers) == len(src.data.uv_layers) > 0
            assert all(abs(obj.matrix_world[i][j]-src.matrix_world[i][j]) < 1e-6 for i in range(4) for j in range(4))
            copies.append(obj)
        bpy.ops.object.select_all(action='DESELECT')
        for o in copies:
            o.select_set(True)
        dest = OUT/f'T07_LOD{level}.fbx'
        bpy.ops.export_scene.fbx(filepath=str(dest), use_selection=True, object_types={'MESH'},
                                 axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
                                 bake_anim=False, path_mode='STRIP', use_mesh_modifiers=False)
        data['triangles'] = sum(p['triangles'] for p in data['parts'])
        data['vertices'] = sum(p['vertices'] for p in data['parts'])
        data['fbx'] = str(dest)
        result['lods'].append(data)
        all_lods.append(copies)
    # Packed editable project, not placed under Assets (avoids Blender auto-import).
    for im in bpy.data.images:
        if im.type != 'IMAGE':
            continue
        # FBX path references are relative to the prepared source texture folder.
        suffix = Path(bpy.path.abspath(im.filepath)).name
        candidate = PREPARED.parent/'Textures'/suffix
        if candidate.exists():
            im.filepath = str(candidate)
            im.reload()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.preferences.filepaths.file_preview_type = 'NONE'
    bpy.ops.file.pack_all()
    working = ROOT/'Tools/IronRequiem/Blender/Working/T07Optimized'
    working.mkdir(parents=True, exist_ok=True)
    for o in originals + [o for group in all_lods[1:] for o in group]:
        o.hide_render = True
        o.hide_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(working/'T07_Optimized.blend'))
    for path, before in result['source_hashes'].items():
        assert digest(Path(path)) == before
    result['sources_unchanged'] = True
    (REPORTS/'geometry.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({'lods': [{'level': d['level'], 'triangles': d['triangles'], 'vertices': d['vertices']} for d in result['lods']]}))


assert bpy.app.background and '--factory-startup' in sys.argv
parser = argparse.ArgumentParser()
parser.add_argument('operation', choices=['inspect', 'generate'])
parser.add_argument('--revise-draft', action='store_true', help='Revise only this task\'s new derived output files')
args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
REPORTS.mkdir(parents=True, exist_ok=True)
inspect() if args.operation == 'inspect' else generate()
