"""Read-only probe: blender --background --factory-startup --python THIS -- <fbx> <report.json>
Measures, per mesh object, the principal axis, the cut-cap faces and the extents along the axis."""
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np

src = Path(sys.argv[sys.argv.index('--') + 1]).resolve(strict=True)
out = Path(sys.argv[sys.argv.index('--') + 2])
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
before = sha(src)
for o in list(bpy.context.scene.objects):
    bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(src))
bpy.context.view_layer.update()
report = {'source': str(src), 'sha256': before, 'objects': []}
for o in bpy.context.scene.objects:
    if o.type != 'MESH':
        continue
    m = o.data
    mw = np.array(o.matrix_world)
    v = np.array([c.co[:] for c in m.vertices])
    w = v @ mw[:3, :3].T + mw[:3, 3]
    c = w.mean(axis=0)
    vals, vecs = np.linalg.eigh(np.cov((w - c).T))
    axis = vecs[:, 2]
    t = (w - c) @ axis
    item = {'name': o.name, 'origin_world': list(mw[:3, 3]), 'centroid': c.tolist(), 'pca_axis': axis.tolist(),
            'pca_sqrt_eigen': np.sqrt(vals).tolist(), 'axis_min': float(t.min()), 'axis_max': float(t.max()),
            'materials': [x.name if x else None for x in m.materials], 'by_material': []}
    for i, mat in enumerate(m.materials):
        polys = [p for p in m.polygons if p.material_index == i]
        if not polys:
            continue
        area = np.array([p.area for p in polys])
        cen = np.array([p.center[:] for p in polys]) @ mw[:3, :3].T + mw[:3, 3]
        nrm = np.array([p.normal[:] for p in polys]) @ mw[:3, :3].T
        tt = (cen - c) @ axis
        item['by_material'].append({
            'material': mat.name if mat else None, 'faces': len(polys), 'area': float(area.sum()),
            'area_centroid': (cen * area[:, None]).sum(axis=0).__truediv__(area.sum()).tolist(),
            'area_normal': ((nrm * area[:, None]).sum(axis=0) / area.sum()).tolist(),
            'axis_t_min': float(tt.min()), 'axis_t_max': float(tt.max()),
            'axis_t_hist': np.histogram(tt, bins=10, range=(float(t.min()), float(t.max())), weights=area)[0].tolist()})
    # cross-section radius along the axis (10 slices)
    perp = (w - c) - np.outer(t, axis)
    r = np.linalg.norm(perp, axis=1)
    edges = np.linspace(t.min(), t.max(), 11)
    item['slices'] = [{'t': float((edges[k] + edges[k + 1]) / 2), 'n': int(((t >= edges[k]) & (t <= edges[k + 1])).sum()),
                       'r_max': float(r[(t >= edges[k]) & (t <= edges[k + 1])].max(initial=0)),
                       'center_offset': perp[(t >= edges[k]) & (t <= edges[k + 1])].mean(axis=0).tolist()
                       if ((t >= edges[k]) & (t <= edges[k + 1])).any() else None} for k in range(10)]
    # End fits: vertices within 8 mm of each end of the PCA axis. Mouth plane normal = smallest eigenvector.
    for key, mask in (('end_max', t >= t.max() - 0.008), ('end_min', t <= t.min() + 0.008)):
        pts = w[mask]
        pc = pts.mean(axis=0)
        ev, evec = np.linalg.eigh(np.cov((pts - pc).T)) if len(pts) > 3 else (np.zeros(3), np.eye(3))
        n = evec[:, 0] * (1 if (evec[:, 0] @ axis) * (1 if key == 'end_max' else -1) > 0 else -1)
        item[key] = {'count': int(mask.sum()), 'center': pc.tolist(), 'normal': n.tolist(),
                     'plane_rms': float(np.sqrt(max(ev[0], 0))), 'radius': float(np.linalg.norm(pts - pc, axis=1).max())}
    report['objects'].append(item)
assert sha(src) == before
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(report, indent=1, default=float), encoding='utf-8')
print('PROBE OK', out)
