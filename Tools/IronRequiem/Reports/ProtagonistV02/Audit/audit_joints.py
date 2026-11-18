"""Read-only: wrist/elbow seams under poses, rigid-piece deformation, wrist cross-sections, coordinate dumps.
blender --background --factory-startup --python-exit-code 1 --python audit_joints.py -- <src> <out.json> <coords_dir>"""
import bpy, bmesh, sys, json, math, os
import numpy as np
from mathutils import Vector, kdtree
from mathutils.bvhtree import BVHTree

args = sys.argv[sys.argv.index('--') + 1:]
SRC, OUT, CDIR = args
os.makedirs(CDIR, exist_ok=True)
tag = os.path.splitext(os.path.basename(SRC))[0]
if SRC.endswith('.blend'):
    bpy.ops.wm.open_mainfile(filepath=SRC, load_ui=False)
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC, use_anim=True)
arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
deps = lambda: bpy.context.evaluated_depsgraph_get()

def reset():
    if arm is None: return
    if arm.animation_data: arm.animation_data.action = None
    for p in arm.pose.bones:
        p.location = (0, 0, 0); p.rotation_quaternion = (1, 0, 0, 0); p.rotation_euler = (0, 0, 0); p.scale = (1, 1, 1)
    bpy.context.view_layer.update()

def coords(o, evaluated=True):
    if evaluated:
        e = o.evaluated_get(deps()); m = e.to_mesh()
        a = np.array([(o.matrix_world @ v.co)[:] for v in m.vertices]); e.to_mesh_clear(); return a
    return np.array([(o.matrix_world @ v.co)[:] for v in o.data.vertices])

def r(x, n=5): return round(float(x), n)

res = {'source': SRC, 'poses': {}, 'sections': {}, 'sockets': [], 'bones_hand_wrist': {}}
reset()
rest = {o.name: coords(o, False) for o in meshes}
for n, c in rest.items():
    np.save(os.path.join(CDIR, f'{tag}__{n}.npy'), c)

# sockets / empties
for o in bpy.data.objects:
    if o.type == 'EMPTY':
        m = o.matrix_world
        res['sockets'].append({'name': o.name, 'parent': o.parent.name if o.parent else None, 'parent_bone': o.parent_bone,
                               'pos': [r(x) for x in m.translation], 'x': [r(x, 4) for x in m.col[0][:3]], 'y': [r(x, 4) for x in m.col[1][:3]], 'z': [r(x, 4) for x in m.col[2][:3]],
                               'euler_world_deg': [r(math.degrees(a), 2) for a in m.to_euler()]})
if arm:
    for bn in ['Forearm_L', 'Hand_L', 'Forearm_R', 'Hand_R', 'mixamorig:LeftForeArm', 'mixamorig:LeftHand', 'mixamorig:RightForeArm', 'mixamorig:RightHand']:
        b = arm.data.bones.get(bn)
        if not b: continue
        ml = arm.matrix_world @ b.matrix_local
        res['bones_hand_wrist'][bn] = {'head': [r(x) for x in arm.matrix_world @ b.head_local], 'tail': [r(x) for x in arm.matrix_world @ b.tail_local],
                                       'length': r(b.length), 'roll_axes': {'x': [r(x, 4) for x in ml.col[0][:3]], 'y_along_bone': [r(x, 4) for x in ml.col[1][:3]], 'z': [r(x, 4) for x in ml.col[2][:3]]}}

# cross sections at the wrist
def section(o, origin, normal, basis_u, basis_v):
    bm = bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world)
    pts = []
    for e in bm.edges:
        a, b = e.verts[0].co, e.verts[1].co
        da, db = (a - origin).dot(normal), (b - origin).dot(normal)
        if (da > 0) != (db > 0) and abs(da - db) > 1e-12:
            p = a + (b - a) * (da / (da - db))
            q = p - origin
            pts.append((q.dot(basis_u), q.dot(basis_v)))
    bm.free()
    if len(pts) < 3: return None
    P = np.array(pts)
    # convex hull (monotone chain)
    S = sorted(map(tuple, P))
    def cross(o_, a, b): return (a[0] - o_[0]) * (b[1] - o_[1]) - (a[1] - o_[1]) * (b[0] - o_[0])
    lo, up = [], []
    for p in S:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], p) <= 0: lo.pop()
        lo.append(p)
    for p in reversed(S):
        while len(up) >= 2 and cross(up[-2], up[-1], p) <= 0: up.pop()
        up.append(p)
    H = np.array(lo[:-1] + up[:-1])
    area = 0.5 * abs(np.dot(H[:, 0], np.roll(H[:, 1], 1)) - np.dot(H[:, 1], np.roll(H[:, 0], 1)))
    per = float(np.sum(np.linalg.norm(H - np.roll(H, 1, axis=0), axis=1)))
    widths = []
    for k in range(180):
        t = math.radians(k); d = np.array([math.cos(t), math.sin(t)])
        pr = H @ d; widths.append(pr.max() - pr.min())
    dmax = max(np.linalg.norm(H[i] - H[j]) for i in range(len(H)) for j in range(i + 1, len(H)))
    cen = H.mean(0)
    return {'points': len(pts), 'hull_area_m2': r(area, 7), 'equiv_diameter_m': r(2 * math.sqrt(area / math.pi), 4), 'hull_perimeter_m': r(per, 4),
            'max_diameter_m': r(dmax, 4), 'min_width_m': r(min(widths), 4), 'max_width_m': r(max(widths), 4),
            'extent_bone_x_m': r(P[:, 0].max() - P[:, 0].min(), 4), 'extent_bone_z_m': r(P[:, 1].max() - P[:, 1].min(), 4),
            'centroid_offset_from_axis_m': [r(cen[0], 4), r(cen[1], 4)]}

if arm:
    for side, fa, hb in [('L', 'Forearm_L', 'Hand_L'), ('R', 'Forearm_R', 'Hand_R')]:
        b = arm.data.bones.get(hb); f = arm.data.bones.get(fa)
        if not b: continue
        wrist = arm.matrix_world @ b.head_local
        axis = (arm.matrix_world @ f.tail_local - arm.matrix_world @ f.head_local).normalized()
        ml = arm.matrix_world @ f.matrix_local
        u = Vector(ml.col[0][:3]).normalized(); v = Vector(ml.col[2][:3]).normalized()
        out = {'wrist_point': [r(x) for x in wrist], 'forearm_axis': [r(x, 4) for x in axis], 'basis_u_forearm_bone_x': [r(x, 4) for x in u], 'basis_v_forearm_bone_z': [r(x, 4) for x in v], 'cuts': []}
        for o in meshes:
            if not (o.name.startswith('Forearm_' + side) or o.name.startswith('Hand_' + side)): continue
            if o.name.endswith(('LOD1', 'LOD2')): continue
            for off in [-0.06, -0.04, -0.02, -0.01, -0.005, 0.005, 0.01, 0.02]:
                s = section(o, wrist + axis * off, axis, u, v)
                if s: out['cuts'].append({'mesh': o.name, 'offset_along_forearm_m': off, **s})
        res['sections'][side] = out

# poses
pose_list = [('rest', None, None)]
for a in bpy.data.actions:
    fr = a.frame_range
    pose_list.append((a.name + '@mid', a, int((fr[0] + fr[1]) / 2)))
    pose_list.append((a.name + '@end', a, int(fr[1])))
pose_list.append(('stress_forearm0.8_hand0.4', 'stress', None))

def kabsch(P, Q):
    pc, qc = P.mean(0), Q.mean(0)
    H = (P - pc).T @ (Q - qc)
    U, S, Vt = np.linalg.svd(H)
    d = np.sign(np.linalg.det(Vt.T @ U.T))
    D = np.diag([1, 1, d])
    R = Vt.T @ D @ U.T
    return np.linalg.norm((P - pc) @ R.T + qc - Q, axis=1)

# shared-ring pairs between pieces (rest)
def ring_pairs(a, b):
    A, B = rest[a], rest[b]
    kd = kdtree.KDTree(len(A))
    for i, p in enumerate(A): kd.insert(Vector(p), i)
    kd.balance()
    pairs = []
    for j, p in enumerate(B):
        co, i, d = kd.find(Vector(p))
        if d < 2e-5: pairs.append((i, j))
    return pairs
fam = {}
for o in meshes:
    suf = o.name.rsplit('_', 1)[-1]
    fam.setdefault(suf, []).append(o.name)
joint_defs = []
for suf, names in fam.items():
    for side in 'LR':
        body = f'Body_{suf}'; fore = f'Forearm_{side}_{suf}'; hand = f'Hand_{side}_{suf}'
        if body in rest and fore in rest: joint_defs.append(('elbow', side, body, fore))
        if fore in rest and hand in rest: joint_defs.append(('wrist', side, fore, hand))
pairs = {(j, s, a, b): ring_pairs(a, b) for j, s, a, b in joint_defs}
res['joint_shared_vertices'] = [{'joint': j, 'side': s, 'a': a, 'b': b, 'shared_verts': len(p)} for (j, s, a, b), p in pairs.items()]

if arm:
    gname = {o.name: {g.index: g.name for g in o.vertex_groups} for o in meshes}
    dom = {}
    for o in meshes:
        d = []
        for v in o.data.vertices:
            ws = [(gname[o.name][g.group], g.weight) for g in v.groups if g.weight > 0.001]
            d.append(max(ws, key=lambda t: t[1])[0] if ws else None)
        dom[o.name] = np.array(d, dtype=object)
    for pname, act, frame in pose_list:
        reset()
        if act == 'stress':
            for side in 'LR':
                if 'Forearm_' + side in arm.pose.bones:
                    arm.pose.bones['Forearm_' + side].rotation_mode = 'XYZ'; arm.pose.bones['Forearm_' + side].rotation_euler.x = .8
                    arm.pose.bones['Hand_' + side].rotation_mode = 'XYZ'; arm.pose.bones['Hand_' + side].rotation_euler.y = .4
        elif act is not None:
            if not arm.animation_data: arm.animation_data_create()
            arm.animation_data.action = act
            if hasattr(arm.animation_data, 'action_slot') and act.slots:
                arm.animation_data.action_slot = act.slots[0]
            bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        posed = {o.name: coords(o) for o in meshes if o.name.split('_')[0] in ('Forearm', 'Hand', 'Body')}
        pr = {'seams': [], 'rigid_residual_mm': {}}
        for (j, s, a, b), p in pairs.items():
            if not p or a not in posed or b not in posed: continue
            gaps = np.linalg.norm(posed[a][[i for i, _ in p]] - posed[b][[k for _, k in p]], axis=1)
            pr['seams'].append({'joint': j, 'side': s, 'a': a, 'b': b, 'max_gap_mm': r(gaps.max() * 1000, 4)})
        # rigid-ness: per piece and per dominant-bone cluster
        for n in posed:
            if not (n.startswith('Hand_') or n.startswith('Forearm_')) or n.endswith(('LOD1', 'LOD2')): continue
            P, Q = rest[n], posed[n]
            entry = {'whole_piece_max': r(kabsch(P, Q).max() * 1000, 3)}
            for bone in sorted(set(x for x in dom[n] if x)):
                idx = np.where(dom[n] == bone)[0]
                if len(idx) < 4: continue
                rr = kabsch(P[idx], Q[idx])
                entry[bone] = {'verts': int(len(idx)), 'max': r(rr.max() * 1000, 3), 'p95': r(np.percentile(rr, 95) * 1000, 3)}
            pr['rigid_residual_mm'][n] = entry
        res['poses'][pname] = pr
    reset()
with open(OUT, 'w', encoding='utf-8') as fh: json.dump(res, fh, indent=1)
print('JOINTS_DONE', SRC)
