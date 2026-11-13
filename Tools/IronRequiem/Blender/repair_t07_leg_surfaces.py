import bpy, bmesh, json, hashlib, sys
from pathlib import Path
root = Path(__file__).resolve().parents[3]
source = root / 'Tools/IronRequiem/Blender/Working/T07Destructible/T07_SeverableArm.blend'
target = root / 'Assets/IronRequiem/Art/Enemies/T07LegRepair/T07_LegInteriors.fbx'
assert not target.exists() or '--resume-own-export' in sys.argv, 'Preserve existing repaired export'
target.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source))
records = []
for name in ('Body_LOD0', 'Body_LOD1', 'Body_LOD2'):
    obj = bpy.data.objects[name]; bm = bmesh.new(); bm.from_mesh(obj.data)
    pending = {e for e in bm.edges if e.is_boundary}; new_faces = []; repaired = []
    original_triangles = sum(len(p.vertices)-2 for p in obj.data.polygons)
    while pending:
        first = pending.pop(); edges = [first]; verts = set(first.verts); stack = list(first.verts)
        while stack:
            for edge in stack.pop().link_edges:
                if edge in pending:
                    pending.remove(edge); edges.append(edge)
                    for v in edge.verts:
                        if v not in verts: verts.add(v); stack.append(v)
        points = [obj.matrix_world @ v.co for v in verts]
        if len(edges) < 3 or max(v.z for v in points) > 1.08 or max(abs(v.x) for v in points) > .43 or min(abs(v.x) for v in points) < .05:
            continue
        if any(sum(e in edges for e in v.link_edges) != 2 for v in verts):
            continue
        # Several scan panels are single open sheets: holes_fill cannot create a duplicate ngon.
        # Back the measured boundary with a shallow, weighted mechanical cap instead.
        deform = bm.verts.layers.deform.verify()
        centre_pos = sum((v.co for v in verts), verts.copy().pop().co.copy()*0) / len(verts)
        normal = sum((e.link_faces[0].normal for e in edges), centre_pos.copy()*0)
        if normal.length > .00001: normal.normalize()
        centre = bm.verts.new(centre_pos - normal*.002)
        weights = {}
        for v in verts:
            for group, weight in v[deform].items(): weights[group] = weights.get(group,0) + weight/len(verts)
        weights = sorted(weights.items(),key=lambda p:p[1],reverse=True)[:4]
        total = sum(w for _,w in weights)
        for group, weight in weights: centre[deform][group] = weight/total
        created = []
        for edge in edges:
            loop = edge.link_loops[0]
            face = bm.faces.new((loop.link_loop_next.vert, loop.vert, centre))
            face.material_index = len(obj.data.materials)-1; created.append(face)
        assert all(not e.is_boundary for e in edges), 'Leg cap must close its actual boundary'
        new_faces.extend(created); repaired.append({'edges': len(edges), 'added_triangles':len(created), 'height': [min(v.z for v in points), max(v.z for v in points)]})
    if new_faces: bmesh.ops.triangulate(bm, faces=new_faces, quad_method='BEAUTY', ngon_method='BEAUTY')
    bm.to_mesh(obj.data); bm.free(); obj.data.update(); obj.data.calc_loop_triangles()
    records.append({'name': name, 'original_triangles': original_triangles, 'triangles': len(obj.data.loop_triangles), 'closed_leg_openings': repaired, 'uv_layers': len(obj.data.uv_layers)})
assert all(r['triangles'] > r['original_triangles'] for r in records), 'Each LOD must gain real interior triangles'
bpy.ops.object.select_all(action='DESELECT')
rig = bpy.data.objects['T07Rig']; rig.select_set(True)
for obj in bpy.context.scene.objects:
    if obj.type == 'MESH' and obj.name in ('Body_LOD0', 'Body_LOD1', 'Body_LOD2'): obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(target), use_selection=True, object_types={'ARMATURE','MESH'}, axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=False, use_armature_deform_only=False, use_mesh_modifiers=False, path_mode='STRIP')
if hasattr(bpy.context.preferences.filepaths, 'use_save_preview_images'): bpy.context.preferences.filepaths.use_save_preview_images = False
bpy.ops.wm.save_as_mainfile(filepath=str(root / 'Tools/IronRequiem/Blender/Working/T07Destructible/T07_LegInteriors.blend'))
(root / 'Tools/IronRequiem/Reports/ShredderT07Repair/leg-repair-geometry.json').write_text(json.dumps({'source': str(source), 'source_sha256': hashlib.file_digest(source.open('rb'),'sha256').hexdigest(), 'meshes': records}, indent=2))
print('T07_LEG_SURFACES_CLOSED', json.dumps(records))
