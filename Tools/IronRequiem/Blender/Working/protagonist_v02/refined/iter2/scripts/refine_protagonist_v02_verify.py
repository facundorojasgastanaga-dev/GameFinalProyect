"""IR-09 verification for refine_protagonist_v02.py: weights, rigidity, joint gaps, interpenetration, mesh health,
UV regions, budgets and Workbench renders. Runs on the in-memory refined scene AFTER it was saved (never re-saves)."""
import bpy
import bmesh
import math
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion
from mathutils.bvhtree import BVHTree

import refine_protagonist_v02_geo as G
import refine_protagonist_v02_build as BLD

FINGERS = ('Index', 'Middle', 'Ring', 'Little')
RIM_TOL = 0.0003
COVER_TOL = 0.0002        # 0.2 mm: a rim point on/inside the covering surface counts as covered
PEN_LIMIT_MM = 1.0


# ================================================================ data access
def piece_table(report, objname):
    key = objname.rsplit('_', 1)[0]          # Hand_R_LOD0 -> Hand_R
    return report['piece_tables'][key]


def world_coords(obj, dg=None):
    if dg is None:
        me = obj.data
        return np.array([tuple(obj.matrix_world @ v.co) for v in me.vertices]), me, None
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    mw = obj.matrix_world
    co = np.array([tuple(mw @ v.co) for v in me.vertices])
    return co, me, ev


def faces_by_piece(me):
    ids = BLD.piece_ids(me)
    out = {}
    for p, pid in zip(me.polygons, ids):
        out.setdefault(pid, []).append(tuple(p.vertices))
    return out


def piece_meshes(obj, dg):
    """{pid: (verts np.array world, faces list, vert index list)} for the posed (dg) or rest (None) state."""
    co, me, ev = world_coords(obj, dg)
    fb = faces_by_piece(me)
    out = {}
    for pid, faces in fb.items():
        vids = sorted({i for f in faces for i in f})
        remap = {v: k for k, v in enumerate(vids)}
        out[pid] = (co[vids], [tuple(remap[i] for i in f) for f in faces], vids)
    if ev is not None:
        ev.to_mesh_clear()
    return out


def bvh_of(verts, faces):
    return BVHTree.FromPolygons([Vector(v) for v in verts], faces, all_triangles=False, epsilon=0.0)


RAY_DIRS = [Vector((0.5774, 0.5773, 0.5774)), Vector((-0.31, 0.9, -0.3)).normalized(),
            Vector((0.7, -0.2, -0.68)).normalized()]


def is_inside(p, tree):
    votes = 0
    for d in RAY_DIRS:
        n = 0; o = Vector(p)
        for _ in range(64):
            hit = tree.ray_cast(o, d)
            if hit[0] is None:
                break
            n += 1
            o = hit[0] + d * 1e-6
        votes += n % 2
    return votes >= 2


def inside_depth(p, tree):
    """(depth, distance): depth > 0 if p is inside the closed mesh (3-ray parity vote), depth = distance to surface."""
    hit = tree.find_nearest(Vector(p))
    if hit[0] is None:
        return 0.0, 1e9
    dist = hit[3]
    if dist > 1e-6 and is_inside(p, tree):
        return dist, dist
    return 0.0, dist


# ================================================================ metrics
def weights_metrics(objs):
    out = {}
    for o in objs:
        me = o.data
        maxinf = 0; multi = 0
        for v in me.vertices:
            n = sum(1 for g in v.groups if g.weight > 1e-6)
            maxinf = max(maxinf, n)
            multi += n > 1
        unweighted = sum(1 for v in me.vertices if not any(g.weight > 1e-6 for g in v.groups))
        out[o.name] = {'max_bones_per_vertex': maxinf, 'vertices_with_2plus_bones': multi, 'unweighted': unweighted,
                       'all_weights_one': all(abs(g.weight - 1.0) < 1e-6 for v in me.vertices for g in v.groups
                                              if g.weight > 1e-6)}
    return out


def kabsch_residual(a, b):
    ca, cb = a.mean(0), b.mean(0)
    H = (a - ca).T @ (b - cb)
    U, S, Vt = np.linalg.svd(H)
    d = np.sign(np.linalg.det(Vt.T @ U.T))
    D = np.diag([1, 1, d])
    R = Vt.T @ D @ U.T
    res = (R @ (a - ca).T).T + cb - b
    return float(np.sqrt((res ** 2).sum(1)).max())


def health(obj):
    bm = bmesh.new(); bm.from_mesh(obj.data)
    h = G.mesh_health(bm)
    bad_wind = 0
    for e in bm.edges:
        if len(e.link_loops) == 2:
            l1, l2 = e.link_loops
            if l1.vert == l2.vert:
                bad_wind += 1
    h['inconsistent_winding_edges'] = bad_wind
    pc = bm.faces.layers.int.get('ir_piece')
    neg = []
    if pc is not None:
        vols = {}
        for f in bm.faces:
            # signed volume contribution (divergence theorem)
            vs = [v.co for v in f.verts]
            for i in range(1, len(vs) - 1):
                vols[f[pc]] = vols.get(f[pc], 0.0) + vs[0].dot(vs[i].cross(vs[i + 1])) / 6.0
        neg = sorted(k for k, v in vols.items() if v <= 0)
    h['pieces_with_inward_normals'] = neg
    h['triangles'] = G.tri_count_bm(bm)
    bm.free()
    return h


def joint_list(table):
    """(child piece ids, child bone, parent bone, rotula ids, plane) for every finger/thumb joint."""
    by_bone = {}
    for t in table:
        by_bone.setdefault(t['bone'], []).append(t['id'])
    joints = []
    for t in table:
        if t['name'].startswith('Rotula_'):
            joints.append({'joint': t['joint_bone'], 'parent_bone': t['bone'], 'rotula': t['id']})
    return joints, by_bone


def rim_points(rest_pieces, pid, plane_p, plane_n, tol=2e-6):
    """Vertices of piece pid on the cap plane that are shared with a non-cap face (cap boundary)."""
    verts, faces, vids = rest_pieces[pid]
    on = np.abs((verts - np.array(plane_p)) @ np.array(plane_n)) < tol
    cap_faces = [f for f in faces if all(on[i] for i in f)]
    other = [f for f in faces if not all(on[i] for i in f)]
    in_cap = {i for f in cap_faces for i in f}
    in_other = {i for f in other for i in f}
    return sorted(in_cap & in_other)


def gap_for(points, cover_trees):
    worst = 0.0
    for p in points:
        best = 1e9
        for tr in cover_trees:
            dep, dist = inside_depth(p, tr)
            if dep > 0:
                best = 0.0; break
            best = min(best, dist)
        if best <= COVER_TOL:
            best = 0.0
        worst = max(worst, best)
    return worst


def adjacency(bone_a, bone_b, arm):
    if bone_a == bone_b:
        return True
    pa = arm.data.bones[bone_a].parent; pb = arm.data.bones[bone_b].parent
    return (pa is not None and pa.name == bone_b) or (pb is not None and pb.name == bone_a)


def penetrations(posed, table, arm, limit_mm=PEN_LIMIT_MM):
    bone = {t['id']: t['bone'] for t in table}
    name = {t['id']: t['name'] for t in table}
    trees = {pid: bvh_of(v, f) for pid, (v, f, _) in posed.items()}
    boxes = {pid: (v.min(0) - 0.001, v.max(0) + 0.001) for pid, (v, f, _) in posed.items()}
    out = []
    ids = sorted(posed)
    for i, a in enumerate(ids):
        for b in ids[i + 1:]:
            if bone[a] == bone[b]:
                continue
            adj = adjacency(bone[a], bone[b], arm)
            lo = np.maximum(boxes[a][0], boxes[b][0]); hi = np.minimum(boxes[a][1], boxes[b][1])
            if (lo > hi).any():
                continue
            depth = 0.0
            for src, dst in ((a, b), (b, a)):
                v = posed[src][0]
                m = ((v >= lo) & (v <= hi)).all(1)
                for p in v[m]:
                    d, _ = inside_depth(p, trees[dst])
                    depth = max(depth, d)
            if depth * 1000 > 0.05:
                out.append({'a': name[a], 'b': name[b], 'depth_mm': round(depth * 1000, 2), 'adjacent': adj})
    out.sort(key=lambda x: -x['depth_mm'])
    return {'max_depth_mm': out[0]['depth_mm'] if out else 0.0,
            'max_depth_non_adjacent_mm': max([x['depth_mm'] for x in out if not x['adjacent']] or [0.0]),
            'max_depth_adjacent_mm': max([x['depth_mm'] for x in out if x['adjacent']] or [0.0]),
            'pairs_over_limit': [x for x in out if x['depth_mm'] > limit_mm], 'pairs_touching': len(out)}


def pose_metrics(arm, objs, report, poses, side='R'):
    hand = bpy.data.objects['Hand_%s_LOD0' % side]
    fore = bpy.data.objects['Forearm_%s_LOD0' % side]
    table = piece_table(report, hand.name)
    joints, by_bone = joint_list(table)
    BLD.apply_pose(arm, {})
    rest = piece_meshes(hand, None)
    rest_fore = piece_meshes(fore, None)
    # cap planes from the build report (proximal plane of each child piece = through bone head, normal bone Y)
    planes = {}
    for j in joints:
        b = arm.data.bones[j['joint']]
        planes[j['joint']] = (tuple(arm.matrix_world @ b.head_local),
                              tuple((arm.matrix_world.to_3x3() @ (b.tail_local - b.head_local)).normalized()))
    mcp = report.get('mcp_plane')
    mp = Vector(mcp['point']); mn = Vector(mcp['normal'])
    if side == 'L':
        mp.x = -mp.x; mn.x = -mn.x
    for F in FINGERS:
        planes['%s1_%s' % (F, side)] = (tuple(mp), tuple(mn))
    name_id = {t['name']: t['id'] for t in table}
    id_name = {t['id']: t['name'] for t in table}
    rims = {}
    for j in joints:
        child = name_id[j['joint']]
        pp, pn = planes[j['joint']]
        ctree = bvh_of(rest[child][0], rest[child][1])
        parents = []
        for pid in by_bone[j['parent_bone']]:
            if id_name[pid].startswith(('Rotula_', 'Collar_')):
                continue
            rp = [i for i in rim_points(rest, pid, pp, pn, RIM_TOL)
                  if ctree.find_nearest(Vector(rest[pid][0][i]))[3] < 0.0005]   # only the rim facing this finger
            parents.append((pid, rp))
        rims[j['joint']] = {'child': (child, rim_points(rest, child, pp, pn, RIM_TOL)), 'parents': parents}
    report.setdefault('verification_rim_points', {})[side] = {
        k: {'child': len(v['child'][1]), 'parent': sum(len(x[1]) for x in v['parents'])} for k, v in rims.items()}
    out = {}
    dg = bpy.context.evaluated_depsgraph_get()
    for pname, quats in poses.items():
        BLD.apply_pose(arm, quats)
        dg.update()
        posed = piece_meshes(hand, dg)
        posed_fore = piece_meshes(fore, dg)
        rig = max(kabsch_residual(rest[pid][0], posed[pid][0]) for pid in rest if len(rest[pid][0]) >= 3)
        rig_f = max(kabsch_residual(rest_fore[pid][0], posed_fore[pid][0]) for pid in rest_fore)
        trees = {pid: bvh_of(v, f) for pid, (v, f, _) in posed.items()}
        gaps = {}
        for j in joints:
            child, cr = rims[j['joint']]['child']
            cover_parent = [trees[pid] for pid in by_bone[j['parent_bone']]]
            g1 = gap_for([posed[child][0][i] for i in cr], cover_parent)
            cover_child = [trees[pid] for pid in by_bone[j['joint']]] + [trees[j['rotula']]]
            g2 = 0.0
            for pid, pr in rims[j['joint']]['parents']:
                g2 = max(g2, gap_for([posed[pid][0][i] for i in pr], cover_child))
            gaps[j['joint']] = round(max(g1, g2) * 1000, 2)
        pen = penetrations(posed, table, arm)
        out[pname] = {'rigid_residual_hand_mm': round(rig * 1000, 4), 'rigid_residual_forearm_mm': round(rig_f * 1000, 4),
                      'joint_gap_mm': gaps, 'max_joint_gap_mm': max(gaps.values()) if gaps else 0.0,
                      'interpenetration': pen}
    BLD.apply_pose(arm, {})
    return out


def wrist_metrics(arm, frames, report, std, side='R'):
    """Measured in the socket frame on the rest geometry (hand poses never rotate Hand_X)."""
    S = frames['Wrist_' + side]; Si = S.inverted()
    hand = bpy.data.objects['Hand_%s_LOD0' % side]; fore = bpy.data.objects['Forearm_%s_LOD0' % side]
    hz = [(Si @ (hand.matrix_world @ v.co)).z for v in hand.data.vertices]
    fz = [(Si @ (fore.matrix_world @ v.co)).z for v in fore.data.vertices]
    fr = [(Si @ (fore.matrix_world @ v.co)) for v in fore.data.vertices]
    tip = [p for p in fr if p.z > std['forearm_end_z'] + 1e-5]
    res = {'hand_min_z_mm': round(min(hz) * 1000, 3), 'forearm_max_z_mm': round(max(fz) * 1000, 3),
           'axial_gap_collar_to_flange_mm': round((min(hz) - std['forearm_end_z']) * 1000, 3),
           'spigot_engagement_mm': round((max(fz) - min(hz)) * 1000, 3),
           'spigot_max_radius_mm': round(max(math.hypot(p.x, p.y) for p in tip) * 1000, 3) if tip else None,
           'radial_clearance_spigot_housing_mm': round((std['housing_d'] - std['spigot_d']) / 2 * 1000, 3),
           # free wrist rotation (deg) about any axis through the socket origin before metal contact
           'free_rotation_deg_before_spigot_contact': round(math.degrees(math.atan2(
               std['housing_depth'] - std['spigot_end_z'], std['spigot_d'] / 2)), 2),
           'free_rotation_deg_before_collar_hits_flange': round(math.degrees(math.atan2(
               -std['forearm_end_z'], std['collar_d'] / 2)), 2)}
    res['nothing_of_forearm_beyond_z_plus_3mm'] = max(fz) <= std['spigot_end_z'] + 1e-6
    res['nothing_of_hand_behind_z0'] = min(hz) >= -1e-6
    return res


def uv_metrics(objs, region, report):
    out = {}
    u0, v0, sz = region.u0, region.v0, region.size
    for o in objs:
        me = o.data
        uv = me.uv_layers.active.data
        ka = me.attributes.get('ir_kind')
        n_new = 0; inside = 0
        for p in me.polygons:
            k = ka.data[p.index].value if ka else 0
            if k in (1, 2):
                n_new += 1
                if all(u0 - 1e-6 <= uv[i].uv.x <= u0 + sz + 1e-6 and v0 - 1e-6 <= uv[i].uv.y <= v0 + sz + 1e-6
                       for i in p.loop_indices):
                    inside += 1
        out[o.name] = {'new_faces': n_new, 'new_faces_in_metal_region': inside}
    return out


def atlas_proposal(objs, hand_objs, res=512):
    occ = np.zeros((res, res), dtype=bool)
    def raster(o, mark):
        me = o.data; uv = me.uv_layers.active.data
        ka = me.attributes.get('ir_kind')
        area = 0.0
        for p in me.polygons:
            if ka and ka.data[p.index].value != 0 and mark is None:
                continue
            pts = [uv[i].uv.copy() for i in p.loop_indices]
            for i in range(1, len(pts) - 1):
                a, b, c = pts[0], pts[i], pts[i + 1]
                area += abs((b - a).cross(c - a)) * 0.5
                xs = [a.x, b.x, c.x]; ys = [a.y, b.y, c.y]
                x0 = max(0, int(min(xs) * res)); x1 = min(res - 1, int(max(xs) * res) + 1)
                y0 = max(0, int(min(ys) * res)); y1 = min(res - 1, int(max(ys) * res) + 1)
                for yy in range(y0, y1 + 1):
                    for xx in range(x0, x1 + 1):
                        q = Vector(((xx + 0.5) / res, (yy + 0.5) / res))
                        d1 = (b - a).cross(q - a); d2 = (c - b).cross(q - b); d3 = (a - c).cross(q - c)
                        if (d1 >= 0 and d2 >= 0 and d3 >= 0) or (d1 <= 0 and d2 <= 0 and d3 <= 0):
                            occ[yy, xx] = True
        return area
    for o in objs:
        raster(o, True)
    hand_area = sum(raster(o, None) for o in hand_objs)
    free = 1.0 - occ.mean()
    # largest empty square (DP)
    dp = np.zeros((res + 1, res + 1), dtype=np.int32)
    best = 0
    for y in range(res):
        for x in range(res):
            if not occ[y, x]:
                dp[y + 1, x + 1] = 1 + min(dp[y, x + 1], dp[y + 1, x], dp[y, x])
                best = max(best, dp[y + 1, x + 1])
    need = hand_area * 3.0     # x2 linear scale = x4 area, minus the area already used by the hands
    return {'atlas_raster_px': res, 'occupied_fraction': round(float(occ.mean()), 4), 'free_fraction': round(free, 4),
            'largest_empty_square_px_of_2048': int(best * 2048 / res),
            'hand_islands_area_uv_both_hands': round(hand_area, 5),
            'extra_area_needed_for_x2_uv': round(need, 5),
            'x2_fits_in_free_area': bool(need < free * 0.7),
            'proposal': ('Free space is enough to scale the hand islands x2 if repacked'
                         if need < free * 0.7 else
                         'Not enough free atlas space for x2 hand islands: needs a dedicated FPS arm/hand texture '
                         '(audit item 8) or a repack of the whole atlas; no texture repainted in this pass.')}


# ================================================================ renders
def setup_render(scene):
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 1200; scene.render.resolution_y = 1200; scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    sh = scene.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'TEXTURE'
    sh.show_cavity = True; sh.cavity_type = 'BOTH'; sh.cavity_ridge_factor = 1.0; sh.cavity_valley_factor = 1.0
    sh.show_object_outline = True; sh.object_outline_color = (0.02, 0.02, 0.02)
    sh.show_specular_highlight = True
    sh.background_type = 'VIEWPORT'; sh.background_color = (0.42, 0.43, 0.45)
    scene.view_settings.view_transform = 'Standard'
    cam_data = bpy.data.cameras.new('IR_v02_Cam')
    cam = bpy.data.objects.new('IR_v02_Cam', cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def look(cam, loc, target, up=Vector((0, 0, 1)), ortho=None, lens=85):
    cam.location = Vector(loc)
    d = (Vector(target) - Vector(loc)).normalized()
    up = Vector(up)
    if abs(d.dot(up.normalized())) > 0.98:
        up = Vector((0, 1, 0))
    rot = d.to_track_quat('-Z', 'Y')
    # roll so that camera up follows `up`
    x = d.cross(up).normalized(); y = x.cross(d).normalized()
    m = Matrix((x, y, -d)).transposed()
    cam.rotation_euler = m.to_euler()
    if ortho:
        cam.data.type = 'ORTHO'; cam.data.ortho_scale = ortho
    else:
        cam.data.type = 'PERSP'; cam.data.lens = lens
    cam.data.clip_start = 0.005; cam.data.clip_end = 50


def render_to(scene, path, log):
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    log.append(Path(path).name)


def posed_center(obj, dg):
    ev = obj.evaluated_get(dg); me = ev.to_mesh()
    c = sum((obj.matrix_world @ v.co for v in me.vertices), Vector()) / max(1, len(me.vertices))
    ev.to_mesh_clear()
    return c


def renders(arm, frames, poses, out_dir, report):
    rd = Path(out_dir) / 'renders'
    rd.mkdir(parents=True, exist_ok=True)
    for old in rd.glob('*.png'):      # renders are regenerated every run (only inside refined/renders)
        old.unlink()
    scene = bpy.context.scene
    cam = setup_render(scene)
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.hide_render = not (o.name.endswith('LOD0'))
        elif o.type in ('EMPTY', 'ARMATURE'):
            o.hide_render = True
    done = []
    BLD.apply_pose(arm, {})
    # body (LOD0) orthographic views, like the audit
    for name, loc in (('body_front', (0, -6, 0.9)), ('body_profile_right', (-6, 0, 0.9)), ('body_back', (0, 6, 0.9)),
                      ('body_three_quarter', (-4.2, -4.2, 1.6))):
        look(cam, loc, (0, 0, 0.9), ortho=2.0)
        render_to(scene, rd / (name + '.png'), done)
    dg = bpy.context.evaluated_depsgraph_get()
    hand = bpy.data.objects['Hand_R_LOD0']
    S = frames['Wrist_R']; Z = S.col[2].xyz; Y = S.col[1].xyz; X = S.col[0].xyz
    shots = [('hand_R_open', 'Open'), ('hand_R_fist', 'Fist'), ('hand_R_grip', 'Grip'),
             ('hand_R_relaxed', 'Relaxed'), ('hand_R_inspect', 'Inspect')]
    for name, pose in shots:
        BLD.apply_pose(arm, poses[pose]); dg.update()
        c = posed_center(hand, dg)
        # palm faces +X for R: three-quarter view from the palm/front side and one from the dorsal side
        look(cam, c + (Vector((0.55, -0.75, 0.25)).normalized() * 0.42), c, lens=85)
        render_to(scene, rd / (name + '_palm34.png'), done)
        look(cam, c + (Vector((-0.6, -0.7, 0.3)).normalized() * 0.42), c, lens=85)
        render_to(scene, rd / (name + '_dorsal34.png'), done)
    BLD.apply_pose(arm, poses['Fist']); dg.update()
    c = posed_center(hand, dg)
    bodies = [o for o in bpy.data.objects if o.name.startswith('Body_') or o.name.startswith(('Hand_L', 'Forearm_L'))]
    for o in bodies:
        o.hide_render = True
    look(cam, c + Vector((0.0, -1.0, 0.0)) * 0.36, c, lens=85)
    render_to(scene, rd / 'hand_R_fist_front.png', done)
    look(cam, c + Vector((1.0, 0.0, 0.0)) * 0.36, c, lens=85)
    render_to(scene, rd / 'hand_R_fist_profile_palm.png', done)
    look(cam, c + Vector((-1.0, 0.0, 0.0)) * 0.36, c, lens=85)
    render_to(scene, rd / 'hand_R_fist_profile_dorsal.png', done)
    look(cam, c + Vector((0.3, -0.3, -0.9)).normalized() * 0.36, c, up=Vector((0, 1, 0)), lens=85)
    render_to(scene, rd / 'hand_R_fist_below.png', done)
    for o in bodies:
        o.hide_render = not o.name.endswith('LOD0')
    BLD.apply_pose(arm, poses['Open']); dg.update()
    o = S.translation
    look(cam, o + (X * 0.9 + Y * 0.35 - Z * 0.1).normalized() * 0.22, o + Z * 0.01, up=-Z, lens=85)
    render_to(scene, rd / 'wrist_R_closeup.png', done)
    look(cam, o + (-X * 0.8 - Y * 0.5 + Z * 0.2).normalized() * 0.22, o + Z * 0.01, up=-Z, lens=85)
    render_to(scene, rd / 'wrist_R_closeup_under.png', done)
    E = frames['Elbow_R'].translation
    look(cam, E + Vector((-0.6, -0.8, 0.15)).normalized() * 0.42, E + Vector((0, 0, -0.02)), lens=85)
    render_to(scene, rd / 'elbow_R_closeup.png', done)
    look(cam, E + Vector((-0.5, 0.85, 0.1)).normalized() * 0.42, E + Vector((0, 0, -0.02)), lens=85)
    render_to(scene, rd / 'elbow_R_closeup_back.png', done)
    # FPS: camera above/behind the forearm looking down its axis at the hand, dorsum up
    for pose in ('Open', 'Grip', 'Fist'):
        BLD.apply_pose(arm, poses[pose]); dg.update()
        c = posed_center(hand, dg)
        eye = o - Z * 0.33 + Y * 0.16
        look(cam, eye, c, up=Y, lens=35)
        render_to(scene, rd / ('fps_overforearm_R_%s.png' % pose), done)
    # pieces colour view (temporary colour attribute, not saved)
    BLD.apply_pose(arm, poses['Open']); dg.update()
    me = hand.data
    attr = me.color_attributes.new('ir_debug', 'BYTE_COLOR', 'CORNER')
    import colorsys
    ids = BLD.piece_ids(me)
    names = {t['id']: t['name'] for t in report['piece_tables']['Hand_R']}
    for p, pid in zip(me.polygons, ids):
        nm = names.get(pid, '')
        col = (0.13, 0.13, 0.15) if nm.startswith(('Rotula', 'Collar')) else colorsys.hsv_to_rgb((pid * 0.137) % 1, 0.5, 0.9)
        for li in p.loop_indices:
            attr.data[li].color = (*col, 1.0)
    scene.display.shading.color_type = 'VERTEX'
    me.color_attributes.active_color = attr
    c = posed_center(hand, dg)
    look(cam, c + Vector((0.55, -0.75, 0.25)).normalized() * 0.42, c, lens=85)
    render_to(scene, rd / 'hand_R_open_pieces.png', done)
    BLD.apply_pose(arm, poses['Fist']); dg.update()
    c = posed_center(hand, dg)
    look(cam, c + Vector((0.55, -0.75, 0.25)).normalized() * 0.42, c, lens=85)
    render_to(scene, rd / 'hand_R_fist_pieces.png', done)
    look(cam, c + Vector((-0.6, -0.7, 0.3)).normalized() * 0.42, c, lens=85)
    render_to(scene, rd / 'hand_R_fist_pieces_dorsal.png', done)
    me.color_attributes.remove(attr)
    scene.display.shading.color_type = 'TEXTURE'
    BLD.apply_pose(arm, {})
    return done


# ================================================================ entry
def verify(report, arm, frames, poses, lib, out_dir, do_renders=True, uv_region=None, std=None):
    arm_objs = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(('Hand_', 'Forearm_'))]
    arm_objs.sort(key=lambda o: o.name)
    v = {}
    v['weights'] = weights_metrics(arm_objs)
    v['health'] = {o.name: health(o) for o in arm_objs + [bpy.data.objects['Body_LOD%d' % k] for k in (0, 1, 2)]}
    v['poses_R'] = pose_metrics(arm, arm_objs, report, poses, 'R')
    v['wrist_R'] = wrist_metrics(arm, frames, report, std, 'R')
    v['wrist_L'] = wrist_metrics(arm, frames, report, std, 'L')
    # L hand mirror exactness
    hr = bpy.data.objects['Hand_R_LOD0'].data; hl = bpy.data.objects['Hand_L_LOD0'].data
    mr = bpy.data.objects['Hand_R_LOD0'].matrix_world; ml = bpy.data.objects['Hand_L_LOD0'].matrix_world
    dev = max(((ml @ b.co) - Vector((-(mr @ a.co).x, (mr @ a.co).y, (mr @ a.co).z))).length
              for a, b in zip(hr.vertices, hl.vertices)) if len(hr.vertices) == len(hl.vertices) else None
    v['hand_L_mirror_max_dev_mm'] = None if dev is None else round(dev * 1000, 5)
    v['uv_new_faces'] = uv_metrics(arm_objs, uv_region, report)
    lod0 = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.endswith('LOD0')]
    v['atlas'] = atlas_proposal(lod0, [bpy.data.objects['Hand_R_LOD0'], bpy.data.objects['Hand_L_LOD0']])
    tris = {o.name: G.tri_count_mesh(o.data) for o in bpy.data.objects if o.type == 'MESH'}
    tot = lambda suf: sum(t for n, t in tris.items() if n.endswith(suf))
    v['budget'] = {'LOD0_total': tot('LOD0'), 'LOD1_total': tot('LOD1'), 'LOD2_total': tot('LOD2'),
                   'FPS_total': tot('FPS'), 'limits': {'LOD0_total_max': 26000, 'LOD1_total_approx': 12000,
                                                         'LOD2_total_approx': 6500, 'FPS_total_max': 9000}}
    v['budget']['LOD0_ok'] = v['budget']['LOD0_total'] <= 26000
    v['budget']['FPS_ok'] = v['budget']['FPS_total'] <= 9000
    pt = {}
    for key, objn in (('Hand_R', 'Hand_R_LOD0'), ('Forearm_R', 'Forearm_R_LOD0')):
        for k in ('LOD0', 'LOD1', 'LOD2'):
            o = bpy.data.objects[objn.replace('LOD0', k)]
            ids = BLD.piece_ids(o.data)
            cnt = {}
            for p, pid in zip(o.data.polygons, ids):
                cnt[pid] = cnt.get(pid, 0) + len(p.vertices) - 2
            names = {t['id']: t['name'] for t in report['piece_tables'][key]}
            pt.setdefault(key, {})[k] = {names.get(pid, str(pid)): n for pid, n in sorted(cnt.items())}
    v['tris_per_piece'] = pt
    report['verification'] = v
    if do_renders:
        report['renders'] = renders(arm, frames, poses, out_dir, report)
    return v


# ================================================================ fist search (spec: phalanges ~85-95 deg, thumb closed)
def deform_matrix(arm, name):
    pb = arm.pose.bones[name]; b = arm.data.bones[name]
    return arm.matrix_world @ pb.matrix @ b.matrix_local.inverted() @ arm.matrix_world.inverted()


def transform(verts, M):
    m = np.array(M)
    return verts @ m[:3, :3].T + m[:3, 3]


def max_pen(moving, static, bone_of, arm):
    """moving/static: {pid: (verts, faces)}; returns (depth_m, pair) over non-adjacent pairs."""
    allp = dict(moving); allp.update(static)
    trees = {pid: bvh_of(v, f) for pid, (v, f) in allp.items()}
    best = (0.0, None)
    done = set()
    for a in sorted(moving):
        for b in sorted(allp):
            if a == b or (b, a) in done:
                continue
            done.add((a, b))
            if adjacency(bone_of[a], bone_of[b], arm):
                continue
            va, vb = allp[a][0], allp[b][0]
            lo = np.maximum(va.min(0), vb.min(0)) - 0.0005; hi = np.minimum(va.max(0), vb.max(0)) + 0.0005
            if (lo > hi).any():
                continue
            for src, dst in ((a, b), (b, a)):
                v = allp[src][0]
                m = ((v >= lo) & (v <= hi)).all(1)
                for p in v[m]:
                    d, _ = inside_depth(p, trees[dst])
                    if d > best[0]:
                        best = (d, (a, b))
    return best


FIST_GRID = {'a1': [85, 90, 95], 'a2': [85, 90, 95, 100], 'a3': [40, 50, 60, 70, 80, 90]}
FIST_GRID_CLEAN = {'a1': [50, 60, 70, 80, 90], 'a2': [50, 60, 70, 80, 90], 'a3': [20, 30, 45, 60]}


def search_fist(arm, hand, table, lib, side='R', log=None, grid=None, mode='spec'):
    """mode 'spec': least interpenetration inside the 85-95 deg grid (spec fist);
    mode 'clean': the most closed fist with every non-adjacent pair <= 1 mm."""
    grid = grid or (FIST_GRID if mode == 'spec' else FIST_GRID_CLEAN)
    rest = piece_meshes(hand, None)
    bone_of = {t['id']: t['bone'] for t in table}
    name_of = {t['id']: t['name'] for t in table}
    sign = -1.0 if side == 'R' else 1.0
    bind_deg = {}
    for F in FINGERS:
        for s in (1, 2, 3):
            n = '%s%d_%s' % (F, s, side)
            q = lib[n]['bind']
            bind_deg[n] = math.degrees(2 * math.atan2(q.z, q.w))

    def finger_q(F, angles):
        return {'%s%d_%s' % (F, s, side): Quaternion((0, 0, 1), sign * math.radians(
            angles[s - 1] - bind_deg['%s%d_%s' % (F, s, side)])) for s in (1, 2, 3)}
    hand_bone = 'Hand_' + side
    static = {pid: (rest[pid][0], rest[pid][1]) for pid in rest
              if bone_of[pid] == hand_bone or bone_of[pid].startswith('Thumb')}
    result = {'mode': mode, 'fingers': {}, 'grid_deg': grid,
              'angle_definition': 'Unity local angle of each phalanx relative to its parent (HandPoseLibrary bind '
                                  'convention); 90 deg = right angle'}
    chosen = {}
    for F in FINGERS:
        moving_ids = [pid for pid in rest if bone_of[pid].startswith(F)]
        best = None
        for a1 in grid['a1']:
            for a2 in grid['a2']:
                for a3 in grid['a3']:
                    BLD.apply_pose(arm, finger_q(F, (a1, a2, a3)))
                    mov = {pid: (transform(rest[pid][0], deform_matrix(arm, bone_of[pid])), rest[pid][1])
                           for pid in moving_ids}
                    d, pair = max_pen(mov, static, bone_of, arm)
                    if mode == 'spec':
                        score = (round(max(0.0, d - 0.001), 4), abs(a1 - 90) + abs(a2 - 90) + abs(a3 - 90))
                    else:
                        ok = d <= 0.001
                        score = (0 if ok else 1, -(a1 + a2 + a3) if ok else round(d, 4), round(d, 4))
                    if best is None or score < best[0]:
                        best = (score, (a1, a2, a3), d, pair)
        chosen[F] = best[1]
        result['fingers'][F] = {'angles_deg': list(best[1]), 'max_pen_mm': round(best[2] * 1000, 2),
                                'worst_pair': [name_of[x] for x in best[3]] if best[3] else None}
        if log is not None:
            log.append('fist search %s %s %s %.2f mm' % (mode, F, best[1], best[2] * 1000))
    fingers_q = {}
    for F in FINGERS:
        fingers_q.update(finger_q(F, chosen[F]))
    thumb_full = {n: BLD.unity_delta_to_blender(lib[n]['bind'], lib[n]['fist'])
                  for n in ('Thumb1_' + side, 'Thumb2_' + side, 'Thumb3_' + side)}

    def thumb_q(t):
        out = {}
        for n, q in thumb_full.items():
            ax, ang = q.to_axis_angle()
            out[n] = Quaternion(ax, ang * t)
        return out
    all_ids = sorted(rest)
    thumb_ids = [pid for pid in all_ids if bone_of[pid].startswith('Thumb')]
    best_t = (0.0, None, None)
    scan = []
    for k in range(0, 14):
        t = k * 0.1
        q = dict(fingers_q); q.update(thumb_q(t))
        BLD.apply_pose(arm, q)
        posed = {pid: (transform(rest[pid][0], deform_matrix(arm, bone_of[pid])), rest[pid][1]) for pid in all_ids}
        mov = {pid: posed[pid] for pid in thumb_ids}
        stat = {pid: posed[pid] for pid in all_ids if pid not in thumb_ids}
        d, pair = max_pen(mov, stat, bone_of, arm)
        scan.append((round(t, 1), round(d * 1000, 2)))
        if best_t[1] is None or (d <= 0.001 and t > best_t[0]) or (best_t[1] > 0.001 and d < best_t[1]):
            best_t = (t, d, pair)
    result['thumb'] = {'library_fist_fraction': round(best_t[0], 2), 'max_pen_mm': round(best_t[1] * 1000, 2),
                       'worst_pair': [name_of[x] for x in best_t[2]] if best_t[2] else None,
                       'scan_fraction_vs_pen_mm': scan}
    q = dict(fingers_q); q.update(thumb_q(best_t[0]))
    BLD.apply_pose(arm, {})
    return q, result, chosen, best_t[0]


def mirror_fist(lib_L, chosen, t, side='L'):
    q = {}
    for F, angles in chosen.items():
        for s in (1, 2, 3):
            n = '%s%d_%s' % (F, s, side)
            b = lib_L[n]['bind']
            bd = math.degrees(2 * math.atan2(b.z, b.w))
            q[n] = Quaternion((0, 0, 1), math.radians(angles[s - 1] - bd))
    for s in (1, 2, 3):
        n = 'Thumb%d_%s' % (s, side)
        full = BLD.unity_delta_to_blender(lib_L[n]['bind'], lib_L[n]['fist'])
        ax, ang = full.to_axis_angle()
        q[n] = Quaternion(ax, ang * t)
    return q


# ================================================================ iter2: hinge poses (validated)
def max_pen_any(moving, static, bone_of=None):
    """Deepest vertex of one piece inside another over every pair (adjacent pieces included, pieces of the same
    bone excluded: they are one rigid body), moving x all."""
    allp = dict(moving); allp.update(static)
    trees = {pid: bvh_of(v, f) for pid, (v, f) in allp.items()}
    best = (0.0, None)
    done = set()
    for a in sorted(moving):
        for b in sorted(allp):
            if a == b or (b, a) in done:
                continue
            done.add((a, b))
            if bone_of is not None and bone_of[a] == bone_of[b]:
                continue
            va, vb = allp[a][0], allp[b][0]
            lo = np.maximum(va.min(0), vb.min(0)) - 0.0005; hi = np.minimum(va.max(0), vb.max(0)) + 0.0005
            if (lo > hi).any():
                continue
            for src, dst in ((a, b), (b, a)):
                v = allp[src][0]
                m = ((v >= lo) & (v <= hi)).all(1)
                for p in v[m]:
                    d, _ = inside_depth(p, trees[dst])
                    if d > best[0]:
                        best = (d, (a, b))
    return best


def hinge_q(arm, bone, axis_world, theta):
    """Pose quaternion (bone local) for a flexion of theta (rad, towards the palm) about the hinge axis."""
    b = arm.data.bones[bone]
    m = (arm.matrix_world @ b.matrix_local).to_3x3().normalized()
    loc = (m.inverted() @ Vector(axis_world)).normalized()
    return Quaternion(loc, -theta)


def library_hinge_angle(arm, bone, axis_world, q_delta):
    b = arm.data.bones[bone]
    m = (arm.matrix_world @ b.matrix_local).to_3x3().normalized()
    loc = (m.inverted() @ Vector(axis_world)).normalized()
    v = Vector((q_delta.x, q_delta.y, q_delta.z))
    return -2.0 * math.atan2(v.dot(loc), q_delta.w)


def posed_pieces(arm, rest, bone_of, ids):
    return {pid: (transform(rest[pid][0], deform_matrix(arm, bone_of[pid])), rest[pid][1]) for pid in ids}


def build_validated_poses(arm, hand, table, lib, hinge_axes, side='R', log=None):
    """Open / Relaxed / Inspect = library hinge angles; Fist and Grip searched so that every pair of pieces
    (adjacent ones included) interpenetrates <= 1 mm and no fingertip enters the collar."""
    rest = piece_meshes(hand, None)
    bone_of = {t['id']: t['bone'] for t in table}
    name_of = {t['id']: t['name'] for t in table}
    all_ids = sorted(rest)
    hand_ids = [p for p in all_ids if bone_of[p] == 'Hand_' + side]
    thumb_ids = [p for p in all_ids if bone_of[p].startswith('Thumb')]
    bind = {}
    for F in FINGERS:
        for s in (1, 2, 3):
            n = '%s%d_%s' % (F, s, side)
            q = lib[n]['bind']
            bind[n] = math.degrees(2 * math.atan2(q.z, q.w))
    res = {'angles_deg': {}, 'checks': {}}

    def q_for(angles):     # angles: bone -> hinge flexion (deg, relative to bind)
        return {n: hinge_q(arm, n, hinge_axes[n], math.radians(a)) for n, a in angles.items()}

    lib_angles = {}
    for pose in ('open', 'relaxed', 'inspect', 'grip', 'fist'):
        lib_angles[pose] = {}
        for n in hinge_axes:
            if n.endswith('_' + side) and n in lib and pose in lib[n]:
                dq = BLD.unity_delta_to_blender(lib[n]['bind'], lib[n][pose])
                lib_angles[pose][n] = math.degrees(library_hinge_angle(arm, n, hinge_axes[n], dq))
    out = {}
    for pose in ('open', 'relaxed', 'inspect'):
        out[pose.capitalize()] = dict(lib_angles[pose])

    palm_ids = [p for p in hand_ids if name_of[p].startswith('Palm')]
    palm_tree = bvh_of(rest[palm_ids[0]][0], rest[palm_ids[0]][1])

    def finger_search(F, grids, base_static, mode):
        """Fist: MCP as high as possible, then the fingertip as close to the palm as possible (<= 1 mm overlap).
        Grip: closest to the library grip."""
        best = None
        mov_ids = [p for p in all_ids if bone_of[p].startswith(F)]
        tip_bone = '%s3_%s' % (F, side)
        for a1 in grids[0]:
            for a2 in grids[1]:
                for a3 in grids[2]:
                    ang = {'%s1_%s' % (F, side): a1 - bind['%s1_%s' % (F, side)],
                           '%s2_%s' % (F, side): a2 - bind['%s2_%s' % (F, side)],
                           '%s3_%s' % (F, side): a3 - bind['%s3_%s' % (F, side)]}
                    BLD.apply_pose(arm, q_for(ang))
                    mov = posed_pieces(arm, rest, bone_of, mov_ids)
                    d, pair = max_pen_any(mov, base_static, bone_of)
                    ok = d <= 0.001
                    tip = arm.matrix_world @ arm.pose.bones[tip_bone].tail
                    tipd = palm_tree.find_nearest(tip)[3]
                    if mode == 'fist':
                        key = (-a1, round(tipd, 3), -a2) if ok else (d, 0, 0)
                    else:
                        key = (abs(a1 - grids[0][0]) + abs(a2 - grids[1][0]) + abs(a3 - grids[2][0]), 0, 0) if ok                             else (d, 0, 0)
                    score = (0 if ok else 1, key)
                    if best is None or score < best[0]:
                        best = (score, (a1, a2, a3), d, pair, ang, tipd)
        return best

    static_rest = {p: (rest[p][0], rest[p][1]) for p in hand_ids + thumb_ids}
    for pname, grids_list in (('Fist', [([90, 88, 85], [100, 95, 90, 85, 80, 75, 70, 65, 60],
                                         [70, 60, 50, 40, 30, 20, 10, 0]),
                                        ([80, 75], [100, 90, 80, 70, 60], [60, 40, 20, 0])]),
                              ('Grip', None)):
        angles = {}
        info = {}
        for F in FINGERS:
            if pname == 'Grip':
                g = [lib_angles['grip']['%s%d_%s' % (F, s, side)] + bind['%s%d_%s' % (F, s, side)] for s in (1, 2, 3)]
                grids_list = [([round(g[0]) - k for k in (0, 5, 10)], [round(g[1]) - k for k in (0, 5, 10, 15)],
                               [round(g[2]) - k for k in (0, 10, 20, 30)])]
            best = None
            for grids in grids_list:
                best = finger_search(F, grids, static_rest, pname.lower())
                if best[2] <= 0.001:
                    break
            angles.update(best[4])
            info[F] = {'absolute_deg_mcp_pip_dip': list(best[1]), 'max_pen_mm': round(best[2] * 1000, 2),
                       'tip_to_palm_mm': round(best[5] * 1000, 2),
                       'worst_pair': [name_of[x] for x in best[3]] if best[3] else None}
            if log is not None:
                log.append('%s %s %s %.2f mm' % (pname, F, best[1], best[2] * 1000))
        # thumb: pure hinge flexion, closes over index/middle middle phalanges
        BLD.apply_pose(arm, q_for(angles))
        fingers_posed = posed_pieces(arm, rest, bone_of, [p for p in all_ids if p not in thumb_ids])
        target_ids = [p for p in all_ids if name_of[p] in ('Index2_' + side, 'Middle2_' + side)]
        tgt_tree = None
        bestT = None
        T = ['Thumb%d_%s' % (s, side) for s in (1, 2, 3)]
        grid1 = [-60, -45, -30, -15, 0, 15, 30, 45, 60, 75]
        for t1 in grid1:
            for t2 in (0, 15, 30, 45, 60):
                for t3 in (0, 20, 40, 60):
                    ang = dict(angles); ang.update({T[0]: t1, T[1]: t2, T[2]: t3})
                    BLD.apply_pose(arm, q_for(ang))
                    mov = posed_pieces(arm, rest, bone_of, thumb_ids)
                    d, pair = max_pen_any(mov, fingers_posed, bone_of)
                    tip_bone = arm.pose.bones[T[2]]
                    tip = arm.matrix_world @ tip_bone.tail
                    if tgt_tree is None:
                        vv = np.concatenate([fingers_posed[p][0] for p in target_ids])
                        ff = []
                        off = 0
                        for p in target_ids:
                            ff += [tuple(i + off for i in f) for f in fingers_posed[p][1]]
                            off += len(fingers_posed[p][0])
                        tgt_tree = bvh_of(vv, ff)
                    dist = tgt_tree.find_nearest(tip)[3]
                    ok = d <= 0.001
                    want = 0.004 if pname == 'Fist' else 0.012
                    score = (0 if ok else 1, abs(dist - want) if ok else d)
                    if bestT is None or score < bestT[0]:
                        bestT = (score, (t1, t2, t3), d, pair, dist)
        angles.update({T[0]: bestT[1][0], T[1]: bestT[1][1], T[2]: bestT[1][2]})
        info['Thumb'] = {'hinge_deg': list(bestT[1]), 'max_pen_mm': round(bestT[2] * 1000, 2),
                         'tip_to_index_middle_mm': round(bestT[4] * 1000, 2),
                         'worst_pair': [name_of[x] for x in bestT[3]] if bestT[3] else None}
        out[pname] = angles
        res['checks'][pname] = info
    # full-hand check of every pose + export
    export = {}
    for pname, ang in out.items():
        q = q_for(ang)
        BLD.apply_pose(arm, q)
        posed = posed_pieces(arm, rest, bone_of, all_ids)
        d, pair = max_pen_any(posed, {}, bone_of)
        res['checks'].setdefault(pname, {})['full_hand_max_pen_mm'] = round(d * 1000, 2)
        res['checks'][pname]['full_hand_worst_pair'] = [name_of[x] for x in pair] if pair else None
        res['angles_deg'][pname] = {n: round(a, 2) for n, a in sorted(ang.items())}
        export[pname] = {}
        for n, qq in sorted(q.items()):
            uq = Quaternion((qq.w, qq.x, -qq.y, -qq.z))          # Blender bone delta -> Unity local delta
            ub = lib[n]['bind'] if n in lib else Quaternion()
            ua = ub @ uq
            export[pname][n] = {'blender_pose_wxyz': [round(x, 6) for x in qq],
                                'unity_local_xyzw': [round(ua.x, 6), round(ua.y, 6), round(ua.z, 6), round(ua.w, 6)]}
    BLD.apply_pose(arm, {})
    res['quaternions'] = export
    poses = {k: q_for(v) for k, v in out.items()}
    return poses, res


def mirror_poses(poses_R, arm):
    """Left hand: same hinge angles about the mirrored axes (bone-local quaternions mirror as (w, x, -y, -z))."""
    out = {}
    for k, q in poses_R.items():
        out[k] = {}
        for n, qq in q.items():
            out[k][n[:-2] + '_L'] = Quaternion((qq.w, qq.x, -qq.y, -qq.z))
    return out
