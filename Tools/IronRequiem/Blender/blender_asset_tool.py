"""Run inside Blender: --background --factory-startup --python THIS -- inspect|prepare-copy ..."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector


def sha256(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def islands(mesh):
    parents = list(range(len(mesh.vertices)))
    def find(i):
        while parents[i] != i:
            parents[i] = parents[parents[i]]
            i = parents[i]
        return i
    for edge in mesh.edges:
        a, b = map(find, edge.vertices)
        parents[a] = b
    sizes = {}
    for i in range(len(parents)):
        root = find(i)
        sizes[root] = sizes.get(root, 0) + 1
    return sorted(sizes.values(), reverse=True)


def inspect_model(source, source_hash):
    data = {'source': str(source), 'source_sha256': source_hash,
            'blender': bpy.app.version_string, 'unit_system': bpy.context.scene.unit_settings.system,
            'unit_scale': bpy.context.scene.unit_settings.scale_length, 'objects': [], 'materials': []}
    for obj in bpy.context.scene.objects:
        item = {'name': obj.name, 'type': obj.type, 'parent': obj.parent.name if obj.parent else None,
                'location': list(obj.location), 'rotation_radians': list(obj.rotation_euler),
                'scale': list(obj.scale), 'dimensions': list(obj.dimensions),
                'world_bounds': [list(obj.matrix_world @ Vector(v)) for v in obj.bound_box],
                'animation': obj.animation_data.action.name if obj.animation_data and obj.animation_data.action else None}
        if obj.type == 'MESH':
            mesh = obj.data
            mesh.calc_loop_triangles()
            edge_faces = {}
            for polygon in mesh.polygons:
                for edge in polygon.edge_keys:
                    edge_faces[edge] = edge_faces.get(edge, 0) + 1
            item.update(vertices=len(mesh.vertices), edges=len(mesh.edges), polygons=len(mesh.polygons),
                        triangles=len(mesh.loop_triangles), uv_layers=[uv.name for uv in mesh.uv_layers],
                        vertex_groups=[g.name for g in obj.vertex_groups],
                        modifiers=[{'name': m.name, 'type': m.type} for m in obj.modifiers],
                        materials=[m.name if m else None for m in mesh.materials],
                        boundary_edges=sum(v == 1 for v in edge_faces.values()),
                        nonmanifold_edges=sum(v > 2 for v in edge_faces.values()),
                        loose_island_vertex_counts=islands(mesh))
        elif obj.type == 'ARMATURE':
            item['bones'] = [{'name': b.name, 'parent': b.parent.name if b.parent else None,
                              'head': list(b.head_local), 'tail': list(b.tail_local)} for b in obj.data.bones]
        data['objects'].append(item)
    for material in bpy.data.materials:
        textures = []
        if material.use_nodes:
            textures = [{'node': n.name, 'image': n.image.name, 'path': n.image.filepath,
                         'packed': bool(n.image.packed_file), 'size': list(n.image.size)}
                        for n in material.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image]
        data['materials'].append({'name': material.name, 'textures': textures})
    data['actions'] = [a.name for a in bpy.data.actions]
    return data


def main():
    if not bpy.app.background or '--factory-startup' not in sys.argv:
        raise RuntimeError('Use --background --factory-startup to protect any open Blender scene.')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=['inspect', 'prepare-copy'])
    parser.add_argument('--input', required=True, type=Path)
    parser.add_argument('--report', required=True, type=Path)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--objects', nargs='+', help='Exact object names to export from the imported copy')
    parser.add_argument('--separate-loose', action='store_true', help='Separate selected mesh islands in the temporary scene only')
    parser.add_argument('--pivots-json', type=Path, help='Mapping of exact object name to world-space [x,y,z] origin in Blender units')
    parser.add_argument('--export-animation', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    source = args.input.resolve(strict=True)
    if source.suffix.lower() != '.fbx':
        parser.error('This tool currently imports FBX only.')
    if args.report.resolve() == source:
        parser.error('Report cannot overwrite the source.')
    if args.operation == 'prepare-copy':
        if not args.output or args.output.suffix.lower() != '.fbx':
            parser.error('prepare-copy requires a new .fbx --output path.')
        if args.output.exists() or args.output.resolve() == source:
            parser.error('Output must be a new file; existing files are never overwritten.')
    before = sha256(source)
    # --factory-startup is part of the invocation; do not erase any user scene here.
    for obj in list(bpy.context.scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=True)
    bpy.context.view_layer.update()
    report = inspect_model(source, before)
    if args.operation == 'prepare-copy':
        selected = [o for o in bpy.context.scene.objects if args.objects is None or o.name in args.objects]
        if args.objects and set(args.objects) - {o.name for o in selected}:
            parser.error('One or more exact object names were not found; inspect first.')
        if not selected:
            parser.error('No objects selected.')
        bpy.ops.object.select_all(action='DESELECT')
        for obj in selected:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = selected[0]
        if args.separate_loose:
            if any(o.type != 'MESH' for o in selected):
                parser.error('Separate-loose requires only mesh objects.')
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.mesh.select_all(action='SELECT')
            bpy.ops.mesh.separate(type='LOOSE')
            bpy.ops.object.mode_set(mode='OBJECT')
            selected = list(bpy.context.selected_objects)
        if args.pivots_json:
            pivots = json.loads(args.pivots_json.read_text(encoding='utf-8-sig'))
            names = {o.name for o in selected}
            if set(pivots) - names:
                parser.error('Pivot name is absent from the selected copy.')
            for obj in selected:
                if obj.name not in pivots:
                    continue
                values = pivots[obj.name]
                if not isinstance(values, list) or len(values) != 3 or not all(isinstance(v, (int, float)) for v in values):
                    parser.error('Each pivot must contain three numeric coordinates.')
                bpy.ops.object.select_all(action='DESELECT')
                obj.select_set(True)
                bpy.context.view_layer.objects.active = obj
                bpy.context.scene.cursor.location = Vector(values)
                bpy.ops.object.origin_set(type='ORIGIN_CURSOR', center='MEDIAN')
            bpy.ops.object.select_all(action='DESELECT')
            for obj in selected:
                obj.select_set(True)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.export_scene.fbx(filepath=str(args.output.resolve()), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
                                 add_leaf_bones=False, bake_anim=args.export_animation)
        report['export'] = {'path': str(args.output.resolve()), 'sha256': sha256(args.output),
                            'objects': [o.name for o in selected], 'animation': args.export_animation}
    if sha256(source) != before:
        raise RuntimeError('Source integrity verification failed.')
    report['source_unchanged'] = True
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')
    print('IRON REQUIEM REPORT: ' + str(args.report.resolve()))


if __name__ == '__main__':
    main()
