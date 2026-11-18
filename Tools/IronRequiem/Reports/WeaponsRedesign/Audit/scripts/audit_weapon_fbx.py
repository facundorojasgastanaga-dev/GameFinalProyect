"""Read-only weapon audit probe + renders.

blender --background --factory-startup --python-exit-code 1 --python audit_weapon_fbx.py -- <fbx> <report.json> <render_dir> <tag>

Imports the FBX into a fresh factory scene (never saved), measures meshes/empties in Blender
and Unity conventions (bakeAxisConversion=0, useFileScale=1: Unity = (-bx, bz, -by)),
detects emissive faces by sampling the emission texture at face UV centroids, and renders
front / side / three-quarter views with Workbench. Markers for sockets are added only to the
temporary scene. The source file hash is checked before and after.
"""
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
src = Path(args[0]).resolve(strict=True)
out = Path(args[1])
render_dir = Path(args[2]).resolve()
tag = args[3]
render_dir.mkdir(parents=True, exist_ok=True)
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
before = sha(src)

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(src))
bpy.context.view_layer.update()


def to_unity(v):
    return [-float(v[0]), float(v[2]), -float(v[1])]


def image_pixels(img):
    if img is None or img.size[0] == 0:
        return None
    a = np.array(img.pixels[:], dtype=np.float32).reshape(img.size[1], img.size[0], img.channels)
    return a


def find_images(mat):
    res = {}
    if not mat or not mat.use_nodes:
        return res
    for n in mat.node_tree.nodes:
        if n.type == 'TEX_IMAGE' and n.image:
            # follow links to decide role
            for l in n.outputs['Color'].links:
                sock = l.to_socket.name.lower()
                if 'emission' in sock:
                    res['emission'] = n.image
                elif 'base' in sock or 'color' in sock:
                    res.setdefault('base', n.image)
            if 'emis' in n.image.name.lower():
                res['emission'] = n.image
            elif 'base' in n.image.name.lower() or 'color' in n.image.name.lower():
                res.setdefault('base', n.image)
    return res


report = {'source': str(src), 'sha256_before': before, 'tag': tag, 'objects': [], 'empties': []}
all_world = []
for o in bpy.context.scene.objects:
    mw = o.matrix_world
    if o.type == 'EMPTY':
        p = mw.translation
        fwd = (mw.to_3x3() @ Vector((0, -1, 0))).normalized()
        report['empties'].append({'name': o.name, 'parent': o.parent.name if o.parent else None,
                                  'blender_world': list(p), 'unity_local_of_fbx_root': to_unity(p),
                                  'blender_minusY_dir': list(fwd), 'unity_dir_of_blender_minusY': to_unity(fwd)})
        continue
    if o.type != 'MESH':
        continue
    m = o.data
    m.calc_loop_triangles()
    v = np.array([(mw @ x.co)[:] for x in m.vertices])
    is_lod0 = ('LOD1' not in o.name and 'LOD2' not in o.name)
    if is_lod0:
        all_world.append(v)
    item = {'name': o.name, 'vertices': len(m.vertices), 'triangles': len(m.loop_triangles), 'polygons': len(m.polygons),
            'origin_blender_world': list(mw.translation),
            'bounds_blender_min': v.min(0).tolist(), 'bounds_blender_max': v.max(0).tolist(),
            'size_blender_xyz_m': (v.max(0) - v.min(0)).tolist(),
            'size_unity_xyz_m': [float(np.ptp(v[:, 0])), float(np.ptp(v[:, 2])), float(np.ptp(v[:, 1]))],
            'unity_bounds_min': [float(-v[:, 0].max()), float(v[:, 2].min()), float(-v[:, 1].max())],
            'unity_bounds_max': [float(-v[:, 0].min()), float(v[:, 2].max()), float(-v[:, 1].min())],
            'materials': [], 'emissive': None}
    uv = m.uv_layers.active.data if m.uv_layers.active else None
    for mi, mat in enumerate(m.materials):
        imgs = find_images(mat)
        entry = {'name': mat.name if mat else None,
                 'images': {k: {'name': i.name, 'file': Path(bpy.path.abspath(i.filepath)).name if i.filepath else None,
                                'size': list(i.size)} for k, i in imgs.items()},
                 'tris': int(sum(1 for t in m.loop_triangles if t.material_index == mi))}
        if mat and mat.use_nodes:
            bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
            if bsdf:
                es = bsdf.inputs.get('Emission Strength')
                ec = bsdf.inputs.get('Emission Color')
                entry['emission_strength'] = float(es.default_value) if es else None
                entry['emission_color_linked'] = bool(ec.is_linked) if ec else None
        item['materials'].append(entry)
    # Emissive faces by sampling emission texture.
    emis_img = None
    for mat in m.materials:
        emis_img = find_images(mat).get('emission') or emis_img
    if emis_img is not None and uv is not None:
        px = image_pixels(emis_img)
        H, W = px.shape[0], px.shape[1]
        lum = px[..., :3].max(axis=2)
        em_faces = []
        cols = []
        for p in m.polygons:
            uvs = np.array([uv[li].uv[:] for li in p.loop_indices])
            cu = uvs.mean(0)
            x = int(np.clip(cu[0] % 1.0 * W, 0, W - 1))
            y = int(np.clip(cu[1] % 1.0 * H, 0, H - 1))
            if lum[y, x] > 0.2:
                em_faces.append(p)
                cols.append(px[y, x, :3].tolist())
        if em_faces:
            area = np.array([p.area for p in em_faces])
            cen = np.array([(mw @ p.center)[:] for p in em_faces])
            # cluster by rounding the unity Z (length) coordinate to 1 cm
            u = np.array([to_unity(c) for c in cen])
            clusters = {}
            for k, (c, a) in enumerate(zip(u, area)):
                key = (round(c[0], 2), round(c[1], 2), round(c[2], 2))
                clusters.setdefault(key, [0.0, 0])
                clusters[key][0] += float(a)
                clusters[key][1] += 1
            item['emissive'] = {'image': emis_img.name, 'faces': len(em_faces), 'faces_total': len(m.polygons),
                                'area_m2': float(area.sum()), 'area_total_m2': float(sum(p.area for p in m.polygons)),
                                'unity_centroid': np.average(u, axis=0, weights=area).tolist(),
                                'unity_min': u.min(0).tolist(), 'unity_max': u.max(0).tolist(),
                                'mean_rgb': np.mean(np.array(cols), axis=0).tolist(),
                                'clusters_unity_1cm': sorted([[list(k), round(v[0] * 1e4, 2), v[1]] for k, v in clusters.items()],
                                                              key=lambda r: r[0][2])}
            item['emissive_texel_fraction'] = float((lum > 0.2).mean())
    report['objects'].append(item)

W = np.concatenate(all_world)
lo, hi = W.min(0), W.max(0)
center = (lo + hi) / 2
size = float(np.linalg.norm(hi - lo))
report['lod0_bounds_blender'] = [lo.tolist(), hi.tolist()]

# ---------- renders (temporary scene only) ----------
scene = bpy.context.scene
# hide non-LOD0 meshes
for o in scene.objects:
    if o.type == 'MESH' and ('LOD1' in o.name or 'LOD2' in o.name):
        o.hide_render = True
# give untextured materials a readable viewport color
palette = {'OriginalCannonSurface': (0.55, 0.52, 0.48, 1), 'CutInterior': (0.85, 0.25, 0.1, 1)}
for mat in bpy.data.materials:
    if mat.name in palette:
        mat.diffuse_color = palette[mat.name]


def marker(name, loc, color, r):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=16, ring_count=8)
    ob = bpy.context.active_object
    ob.name = 'AUDIT_' + name
    mat = bpy.data.materials.new('AUDIT_' + name)
    mat.diffuse_color = color
    ob.data.materials.append(mat)
    return ob


r = max(size * 0.02, 0.006)
for e in report['empties']:
    col = (0.1, 0.9, 0.2, 1) if 'Attach' in e['name'] else (1, 0.1, 0.1, 1)
    marker(e['name'], e['blender_world'], col, r)
# firing-axis rod: from attachment toward muzzle if both exist
names = {e['name']: e for e in report['empties']}
if 'Attachment' in names and 'Muzzle' in names:
    a = Vector(names['Attachment']['blender_world'])
    b = Vector(names['Muzzle']['blender_world'])
    d = (b - a)
    bpy.ops.mesh.primitive_cylinder_add(radius=r * 0.25, depth=d.length * 1.25, location=a + d * 0.625)
    rod = bpy.context.active_object
    rod.rotation_mode = 'QUATERNION'
    rod.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d.normalized())
    m2 = bpy.data.materials.new('AUDIT_axis')
    m2.diffuse_color = (1, 0.85, 0.1, 1)
    rod.data.materials.append(m2)

scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'TEXTURE'
scene.display.shading.show_cavity = True
scene.display.shading.show_object_outline = True
scene.render.resolution_x = 1280
scene.render.resolution_y = 800
scene.render.film_transparent = False
scene.world = bpy.data.worlds.new('AUDIT_world') if scene.world is None else scene.world
cam_data = bpy.data.cameras.new('AUDIT_cam')
cam_data.type = 'ORTHO'
cam_data.ortho_scale = size * 1.75
cam = bpy.data.objects.new('AUDIT_cam', cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
c = Vector(center)
# Blender -Y is weapon forward for pivoted assets (Unity +Z). For T07_CannonClean the barrel is tilted;
# views remain axis aligned in the source frame and are labelled as such.
views = {
    'front': Vector((0, -1, 0)),          # looking at the muzzle (camera in front, -Y side)
    'side': Vector((1, 0, 0)),            # profile from Blender +X (Unity -X / left side)
    'three_quarter': Vector((0.75, -0.75, 0.55)),
    'top': Vector((0, 0, 1)),
}
for vname, dirv in views.items():
    dirv = dirv.normalized()
    cam.location = c + dirv * size * 3
    up = Vector((0, 1, 0)) if vname == 'top' else Vector((0, 0, 1))
    rot = (-dirv).to_track_quat('-Z', 'Y')
    if vname == 'top':
        from mathutils import Euler
        rot = Euler((0, 0, 0)).to_quaternion()
    cam.rotation_mode = 'QUATERNION'
    cam.rotation_quaternion = rot
    scene.render.filepath = str(render_dir / f'{tag}_{vname}.png')
    bpy.ops.render.render(write_still=True)
    report.setdefault('renders', []).append(scene.render.filepath)

report['sha256_after'] = sha(src)
report['source_unchanged'] = report['sha256_after'] == before
out.write_text(json.dumps(report, indent=1), encoding='utf-8')
print('AUDIT_DONE', tag, report['source_unchanged'])
