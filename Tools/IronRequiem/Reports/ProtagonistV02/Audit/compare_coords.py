import numpy as np, os, sys, json
from mathutils import kdtree, Vector
base = os.path.dirname(os.path.abspath(__file__)) + '/data'
def L(d, t, o):
    p = f'{base}/{d}/{t}__{o}.npy'; return np.load(p) if os.path.exists(p) else None
out = []
def cmp(a, b):
    A = L(*a); B = L(*b)
    rec = {'a': '/'.join(a), 'b': '/'.join(b)}
    if A is None or B is None: rec['error'] = 'missing'; out.append(rec); return
    rec['verts'] = [len(A), len(B)]
    if A.shape == B.shape: rec['index_max_diff_mm'] = round(float(np.linalg.norm(A - B, axis=1).max() * 1000), 5)
    kd = kdtree.KDTree(len(B))
    for i, p in enumerate(B): kd.insert(Vector(p), i)
    kd.balance()
    rec['nearest_max_diff_mm'] = round(max(kd.find(Vector(p))[2] for p in A) * 1000, 5)
    out.append(rec)
for lod in ['LOD0', 'LOD1', 'LOD2']:
    for pc in ['Body', 'Forearm_L', 'Forearm_R', 'Hand_L', 'Hand_R']:
        cmp(('coords', f'IR_Protagonist_{lod}', f'{pc}_{lod}'), ('coords', 'IR_Protagonist_Rigged', f'{pc}_{lod}'))
        cmp(('coords', 'IR_Protagonist_Rigged', f'{pc}_{lod}'), ('coords_blend', 'IR_Protagonist_Rigged', f'{pc}_{lod}'))
for pc in ['Forearm_L', 'Forearm_R', 'Hand_L', 'Hand_R']:
    cmp(('coords', 'IR_Protagonist_LOD0', f'{pc}_LOD0'), ('coords', 'IR_Protagonist_FPS_Parts', f'{pc}_LOD0'))
    cmp(('coords', 'IR_Protagonist_FPS_Parts', f'{pc}_LOD0'), ('coords', 'IR_Protagonist_FPS_Rigged', f'{pc}_FPS'))
    cmp(('coords', 'IR_Protagonist_Rigged', f'{pc}_LOD0'), ('coords', 'IR_Protagonist_FPS_Rigged', f'{pc}_FPS'))
# L/R mirror symmetry
for lod in ['LOD0', 'LOD1', 'LOD2']:
    for pc in ['Forearm', 'Hand']:
        A = L('coords', f'IR_Protagonist_{lod}', f'{pc}_L_{lod}'); B = L('coords', f'IR_Protagonist_{lod}', f'{pc}_R_{lod}')
        Bm = B * np.array([-1, 1, 1])
        kd = kdtree.KDTree(len(Bm))
        for i, p in enumerate(Bm): kd.insert(Vector(p), i)
        kd.balance()
        d = np.array([kd.find(Vector(p))[2] for p in A])
        out.append({'mirror_check': f'{pc}_L_{lod} vs mirrored {pc}_R_{lod}', 'verts': [len(A), len(B)], 'nn_max_mm': round(float(d.max() * 1000), 3), 'nn_p95_mm': round(float(np.percentile(d, 95) * 1000), 3)})
json.dump(out, open(f'{base}/coord_compare.json', 'w'), indent=1)
for o in out: print(o)
