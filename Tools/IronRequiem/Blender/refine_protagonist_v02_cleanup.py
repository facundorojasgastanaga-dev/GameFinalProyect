"""IR-09 iter4 (cleanup only, no design/proportion change) for refine_protagonist_v02.py.

1. Elbow: elbow cut plane chosen among small offsets of the iter3 plane so that the body cut leaves no thin shelf
   (< 1.5 mm) nor loose island in any body LOD; forearm neck re-cut after every decimation (LOD0/1/2) on a plane
   parallel to the body cap, NECK_GAP towards the elbow, so the two caps are planar, closed and never coplanar.
   Islands < 1 cm3 or thinner than 1.5 mm are removed from body / forearm surfaces.
2. Palm: knife-thin fins (< 1 mm, same piece) collapsed; faces whose UV/3D area ratio is outside the hand p5-p95
   (or whose UV anisotropy is above the hand p95) re-projected into the light plate already used by the phalanges.
3. Measurement helpers (islands, thickness, coplanar overlaps, UV stretch, L/R mirror per LOD) and extra close-up
   renders. Also runnable on an existing blend to measure the "before" state:
     blender --background --factory-startup --python-exit-code 1 <blend> --python refine_protagonist_v02_cleanup.py
             -- --measure <out.json> [--renders <dir>]
"""
import sys
sys.dont_write_bytecode = True
import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree
import numpy as np

try:
    _HERE = Path(__file__).resolve().parent
except NameError:
    _HERE = Path(r'C:\Users\gehbd\ProyectoGameFinal\Tools\IronRequiem\Blender')
if str(_HERE) not in sys.path:
    sys.path.insert(0, str(_HERE))
import refine_protagonist_v02_geo as G

T_ELBOW = 0.0015           # minimum wall thickness at the elbow cut (m)
V_MIN = 1e-6               # minimum island volume (1 cm3)
T_PALM = 0.001             # minimum palm fin thickness (m)
NECK_GAP = 0.0015          # forearm neck cap sits 1.5 mm below the body cap (parallel, never coplanar)
NECK_CANDIDATES = (0.040, 0.042, 0.038, 0.044, 0.036, 0.046, 0.034, 0.048)   # iter3 value first, then +-2 mm steps
ELBOW_RADIUS = 0.10        # measurement region around the elbow pivot
COPLANAR_DIST = 0.0005
COPLANAR_DOT = 0.95
SHARP_SUM_DEG = 200.0      # a surface vertex whose face angles add up to < 200 deg is a pointed wedge/blade tip


# ================================================================ measurement primitives
def _obb_min_extent(points):
    if len(points) < 4:
        return 0.0
    a = np.array([tuple(p) for p in points])
    a = a - a.mean(0)
    w, v = np.linalg.eigh(a.T @ a)
    proj = a @ v[:, 0]
    return float(proj.max() - proj.min())


def island_stats(faces):
    vol = 0.0
    area = 0.0
    for f in faces:
        vs = [v.co for v in f.verts]
        for i in range(1, len(vs) - 1):
            vol += vs[0].dot(vs[i].cross(vs[i + 1])) / 6.0
        area += f.calc_area()
    verts = {v for f in faces for v in f.verts}
    closed = all(e.is_manifold for f in faces for e in f.edges)
    return {'faces': len(faces), 'volume_cm3': round(abs(vol) * 1e6, 4), 'area_cm2': round(area * 1e4, 3),
            'obb_min_mm': round(_obb_min_extent([v.co for v in verts]) * 1000, 3), 'closed': closed,
            'center': [round(x, 4) for x in (sum((v.co for v in verts), Vector()) / max(1, len(verts)))]}


def _skip_set(f):
    return {g.index for v in f.verts for g in v.link_faces}


def face_thickness(bm, faces, tmax, piece_layer=None):
    """Wall thickness behind each face: ray along -normal from the face centre to the first non-neighbour face
    whose normal opposes it (same piece if piece_layer). Returns {face_index: thickness} for thickness < tmax."""
    bm.normal_update()
    bm.faces.ensure_lookup_table(); bm.faces.index_update()
    tree = BVHTree.FromBMesh(bm)
    out = {}
    for f in faces:
        if f.normal.length < 0.5:
            continue
        nb = _skip_set(f)
        o = f.calc_center_median() - f.normal * 1e-7
        d = 0.0
        for _ in range(6):
            loc, no, idx, dd = tree.ray_cast(o, -f.normal, tmax - d)
            if idx is None:
                break
            g = bm.faces[idx]
            if idx in nb or (piece_layer is not None and g[piece_layer] != f[piece_layer]):
                o = loc - f.normal * 1e-7; d += dd
                continue
            if f.normal.dot(g.normal) < -0.3:
                out[f.index] = d + dd
            break
    return out


def vertex_thickness(bm, verts, tmax, piece_layer=None):
    """Thickness under each vertex along -vertex normal (same piece if piece_layer); {vert_index: t} for t < tmax."""
    bm.normal_update()
    bm.faces.ensure_lookup_table(); bm.faces.index_update(); bm.verts.index_update()
    tree = BVHTree.FromBMesh(bm)
    out = {}
    for v in verts:
        n = v.normal
        if n.length < 0.5 or not v.link_faces:
            continue
        nb = {f.index for f in v.link_faces}
        pid = v.link_faces[0][piece_layer] if piece_layer is not None else None
        o = v.co - n * 1e-7
        d = 0.0
        for _ in range(6):
            loc, no, idx, dd = tree.ray_cast(o, -n, tmax - d)
            if idx is None:
                break
            if idx in nb or (piece_layer is not None and bm.faces[idx][piece_layer] != pid):
                o = loc - n * 1e-7; d += dd
                continue
            out[v.index] = d + dd
            break
    return out


def self_coplanar_pairs(bm, faces):
    """Faces with a non-neighbour face of the same mesh, (anti)parallel, closer than COPLANAR_DIST in front of or
    behind them (overlapping near-coplanar sheets)."""
    bm.normal_update()
    bm.faces.ensure_lookup_table(); bm.faces.index_update()
    tree = BVHTree.FromBMesh(bm)
    n = 0
    for f in faces:
        if f.normal.length < 0.5:
            continue
        nb = _skip_set(f)
        c = f.calc_center_median()
        for sgn in (1, -1):
            loc, no, idx, dd = tree.ray_cast(c + f.normal * sgn * 1e-7, f.normal * sgn, COPLANAR_DIST)
            if idx is not None and idx not in nb and abs(f.normal.dot(bm.faces[idx].normal)) > COPLANAR_DOT:
                n += 1
                break
    return n


def cross_coplanar(bm_a, faces_a, bm_b):
    """Faces of A lying within COPLANAR_DIST of an (anti)parallel face of B, and overlapping triangle pairs."""
    bm_b.faces.ensure_lookup_table()
    tb = BVHTree.FromBMesh(bm_b)
    n = 0
    for f in faces_a:
        c = f.calc_center_median()
        loc, no, idx, d = tb.find_nearest(c, COPLANAR_DIST)
        if idx is not None and abs(f.normal.dot(bm_b.faces[idx].normal)) > COPLANAR_DOT:
            n += 1
    ta = BVHTree.FromBMesh(bm_a)
    return n, len(ta.overlap(tb))


def world_bm(obj):
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.transform(obj.matrix_world)
    G.ensure_layers(bm)
    bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
    return bm


# ================================================================ elbow
def bad_islands(bm, vmin=V_MIN, tmin=T_ELBOW):
    return [(c, s) for c in G.components(bm.faces[:]) for s in [island_stats(c)]
            if s['volume_cm3'] * 1e-6 < vmin or s['obb_min_mm'] * 1e-3 < tmin]


def remove_bad_islands(bm, vmin=V_MIN, tmin=T_ELBOW, near=None):
    """Delete islands < vmin or thinner than tmin (only those whose centre is within near=(center, radius))."""
    bad = bad_islands(bm, vmin, tmin)
    if near is not None:
        bad = [(c, s) for c, s in bad if (Vector(s['center']) - Vector(near[0])).length < near[1]]
    for c, s in bad:
        bmesh.ops.delete(bm, geom=list(c), context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    return [s for c, s in bad]


def region_faces(bm, center, radius):
    return [f for f in bm.faces if (f.calc_center_median() - center).length < radius]


def body_cut_ok(R, arm, body_objs, neck):
    """Cut copies of every body LOD with ELBOW_NECK=neck; return per-LOD thin/island counts."""
    old = R.ELBOW_NECK
    R.ELBOW_NECK = neck
    rows = []
    try:
        for k, o in sorted(body_objs.items()):
            for side in ('R', 'L'):
                bm = G.bm_from_object(o)
                ch, p, n = R.elbow_split(bm, side, arm, {}, 'probe')
                ch.free()
                G.triangulate_ngons(bm)
                E = R.bone_world(arm, 'Forearm_' + side)['head']
                th = face_thickness(bm, region_faces(bm, E, ELBOW_RADIUS), T_ELBOW)
                isl = bad_islands(bm)
                rows.append({'lod': k, 'side': side, 'thin_faces': len(th),
                             'min_thickness_mm': round(min(th.values()) * 1000, 3) if th else None,
                             'bad_islands': [s for c, s in isl]})
                bm.free()
    finally:
        R.ELBOW_NECK = old
    return rows


def choose_elbow_neck(R, arm, body_objs, report):
    scan = []
    chosen = None
    for neck in NECK_CANDIDATES:
        rows = body_cut_ok(R, arm, body_objs, neck)
        bad = sum(r['thin_faces'] + len(r['bad_islands']) for r in rows)
        scan.append({'neck_m': neck, 'thin_faces_total': sum(r['thin_faces'] for r in rows),
                     'bad_islands_total': sum(len(r['bad_islands']) for r in rows), 'rows': rows})
        if bad == 0:
            chosen = neck
            break
    if chosen is None:
        chosen = min(scan, key=lambda s: (s['thin_faces_total'] + s['bad_islands_total'],
                                          abs(s['neck_m'] - NECK_CANDIDATES[0])))['neck_m']
    report['iter4_elbow_neck'] = {'iter3_neck_m': NECK_CANDIDATES[0], 'chosen_neck_m': chosen,
                                  'shift_mm': round((chosen - NECK_CANDIDATES[0]) * 1000, 2), 'scan': scan,
                                  'criterion': 'no face with wall < 1.5 mm within 100 mm of the elbow pivot and no '
                                               'island < 1 cm3 or < 1.5 mm thick, in Body LOD0/1/2 both sides'}
    return chosen


SNAP_DEV = 0.001          # decimated neck vertices further than 1 mm from the full-resolution surface are spikes
SNAP_BAND = 0.030         # only within 30 mm of the neck plane


def snap_neck_spikes(bm, p, n, ref_tree):
    """Collapse decimation can push a vertex of the coarse neck outwards (a spike standing out of the body silhouette).
    Vertices near the neck that deviate > SNAP_DEV from the full-resolution surface are put back on it."""
    moved = []
    for v in bm.verts:
        s = (v.co - p).dot(n)
        if -0.002 < s < SNAP_BAND:
            loc, no, idx, d = ref_tree.find_nearest(v.co)
            if loc is not None and d > SNAP_DEV:
                moved.append(round(d * 1000, 2))
                v.co = loc
    return sorted(moved, reverse=True)


def collapse_sharp_neck(bm, p, n, band=0.004, rounds=12):
    """Wedge tips left on the neck rim where the cut grazes the crease (pointed silhouette): merge each tip into its
    closest neighbour on the cap plane (or closest neighbour) while the mesh stays closed and manifold."""
    log = []
    for r in range(rounds):
        bm.verts.ensure_lookup_table(); bm.verts.index_update()
        tips = [v for v in bm.verts if v.link_faces and abs((v.co - p).dot(n)) < band
                and sum(l.calc_angle() for l in v.link_loops) < math.radians(SHARP_SUM_DEG)]
        if not tips:
            break
        merged = False
        for v0 in tips:
            vi = v0.index
            nb = sorted((e.other_vert(v0) for e in v0.link_edges),
                        key=lambda u: (abs((u.co - p).dot(n)) > 1e-6, (u.co - v0.co).length))
            for u0 in nb:
                work = bm.copy(); G.ensure_layers(work); work.verts.ensure_lookup_table()
                bmesh.ops.pointmerge(work, verts=[work.verts[vi], work.verts[u0.index]], merge_co=u0.co.copy())
                bmesh.ops.dissolve_degenerate(work, dist=1e-7, edges=work.edges[:])
                loose = [x for x in work.verts if not x.link_faces]
                if loose:
                    bmesh.ops.delete(work, geom=loose, context='VERTS')
                G.triangulate_ngons(work, limit=3)
                if _merge_ok(work):
                    log.append(round(math.degrees(sum(l.calc_angle() for l in v0.link_loops)), 1))
                    # copy back into bm (caller keeps its handle)
                    bm.clear()
                    G.ensure_layers(bm)
                    G.append_bm(bm, work)
                    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-9)
                    work.free()
                    merged = True
                    break
                work.free()
            if merged:
                break
        if not merged:
            break
    return log


def recut_neck(bm, p, n, region, piece=100, gap=NECK_GAP, ref_tree=None):
    """Planar, closed neck cap parallel to the body cap, gap towards the elbow; removes decimation spikes beyond it."""
    p = Vector(p); n = Vector(n).normalized()
    snapped = snap_neck_spikes(bm, p, n, ref_tree) if ref_tree is not None else None
    caps = G.cut_keep(bm, p + n * gap, n, keep_positive=True, region=region, piece=piece)
    removed = remove_bad_islands(bm)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=bm.edges[:])
    G.triangulate_ngons(bm, limit=3)
    tips = collapse_sharp_neck(bm, p + n * gap, n)
    return {'cap_tris': len(caps), 'removed_islands': removed, 'snapped_spike_vertices_dev_mm': snapped,
            'collapsed_wedge_tips_angle_sum_deg': tips}


# ================================================================ palm
def _merge_ok(bm):
    h = G.mesh_health(bm)
    return h['non_manifold_edges'] == 0 and h['boundary_edges'] == 0


def collapse_palm_fins(bm, palm_id, tmin=T_PALM, rounds=40):
    """Collapse vertices of knife-thin palm fins (vertex or face thickness < tmin, same piece) into their thickest
    neighbour. Each merge is kept only if the mesh stays closed and manifold. Returns (new bmesh, info); the input
    bmesh is freed when it is replaced."""
    log = []
    for r in range(rounds):
        pc = bm.faces.layers.int['ir_piece']
        palm_faces = [f for f in bm.faces if f[pc] == palm_id]
        pverts = {v for f in palm_faces for v in f.verts}
        vt = vertex_thickness(bm, pverts, tmin, pc)
        ft = face_thickness(bm, palm_faces, tmin, pc)
        bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
        flagged = set(vt)
        tips = {v.index for v in pverts if v.link_faces and
                sum(l.calc_angle() for l in v.link_loops) < math.radians(SHARP_SUM_DEG)}   # blade / needle tips
        flagged |= tips
        for fi in ft:
            for v in bm.faces[fi].verts:
                flagged.add(v.index)
        if not flagged:
            break
        allt = vertex_thickness(bm, pverts, 1.0, pc)
        done = 0
        for vi in sorted(flagged, key=lambda i: vt.get(i, tmin)):
            bm.verts.ensure_lookup_table()
            v0 = bm.verts[vi]
            ang0 = round(math.degrees(sum(l.calc_angle() for l in v0.link_loops)), 1)
            nbrs = sorted((e.other_vert(v0).index for e in v0.link_edges),
                          key=lambda i: (i in flagged, -allt.get(i, 0.0)))
            for ui in nbrs:          # thickest unflagged neighbour first; keep the first merge that stays manifold
                work = bm.copy(); G.ensure_layers(work)
                work.verts.ensure_lookup_table()
                v, u = work.verts[vi], work.verts[ui]
                bmesh.ops.pointmerge(work, verts=[v, u], merge_co=u.co.copy())
                bmesh.ops.dissolve_degenerate(work, dist=1e-7, edges=work.edges[:])
                loose = [x for x in work.verts if not x.link_faces]
                if loose:
                    bmesh.ops.delete(work, geom=loose, context='VERTS')
                G.triangulate_ngons(work, limit=3)
                if _merge_ok(work):
                    bm.free()
                    bm = work
                    done += 1
                    log.append({'round': r, 'reason': 'thin' if vi in vt else ('tip' if vi in tips else 'thin_face'),
                                'vertex_thickness_mm': round(vt[vi] * 1000, 3) if vi in vt else None,
                                'angle_sum_deg': ang0})
                    break
                work.free()
            if done:
                break      # indices changed: re-measure
        if done == 0:
            break
    # thin islands of the palm piece (none expected)
    pc = bm.faces.layers.int['ir_piece']
    removed = []
    for c in G.components([f for f in bm.faces if f[pc] == palm_id]):
        s = island_stats(c)
        if s['obb_min_mm'] < tmin * 1000:
            bmesh.ops.delete(bm, geom=list(c), context='FACES')
            removed.append(s)
    return bm, {'collapsed_vertices': log, 'removed_thin_palm_islands': removed}


def uv_ratio_aniso(f, uv):
    """(UV area / 3D area, UV anisotropy sigma_max/sigma_min) of a face (first triangle for anisotropy)."""
    pts = [l[uv].uv for l in f.loops]
    a_uv = 0.0
    for i in range(1, len(pts) - 1):
        a_uv += abs((pts[i] - pts[0]).cross(pts[i + 1] - pts[0])) * 0.5
    a3 = f.calc_area()
    ratio = a_uv / a3 if a3 > 1e-14 else 0.0
    l = f.loops
    p0, p1, p2 = l[0].vert.co, l[1].vert.co, l[2].vert.co
    t0, t1, t2 = l[0][uv].uv, l[1][uv].uv, l[2][uv].uv
    e1 = p1 - p0; e2 = p2 - p0
    nn = e1.cross(e2)
    if e1.length < 1e-10 or nn.length < 1e-14:
        return ratio, 1e9
    x = e1.normalized(); y = nn.normalized().cross(x)
    A = np.array([[e1.dot(x), e2.dot(x)], [e1.dot(y), e2.dot(y)]])
    D = np.array([[t1.x - t0.x, t2.x - t0.x], [t1.y - t0.y, t2.y - t0.y]])
    try:
        J = D @ np.linalg.inv(A)
    except np.linalg.LinAlgError:
        return ratio, 1e9
    s = np.linalg.svd(J, compute_uv=False)
    return ratio, float(s[0] / max(s[1], 1e-12))


MIN_FACE_AREA = 1e-8      # 0.01 mm2: below this a triangle is a zero-area needle (invisible, UV ratio undefined)


def uv_stats(bm, palm_id, thresholds=None):
    pc = bm.faces.layers.int['ir_piece']; uv = bm.loops.layers.uv[G.UV_NAME]
    vals = [(f, *uv_ratio_aniso(f, uv)) for f in bm.faces if f.calc_area() >= MIN_FACE_AREA]
    needles = sum(1 for f in bm.faces if f[pc] == palm_id and f.calc_area() < MIN_FACE_AREA)
    if thresholds is None:
        r = sorted(x[1] for x in vals); a = sorted(x[2] for x in vals); n = len(r)
        thresholds = {'ratio_p5': r[int(0.05 * (n - 1))], 'ratio_p95': r[int(0.95 * (n - 1))],
                      'aniso_p95': a[int(0.95 * (n - 1))]}
    palm = [x for x in vals if x[0][pc] == palm_id]
    out = [x for x in palm if x[1] < thresholds['ratio_p5'] or x[1] > thresholds['ratio_p95']
           or x[2] > thresholds['aniso_p95']]
    pr = sorted(x[1] for x in palm)
    return out, thresholds, {
        'palm_faces': len(palm), 'palm_zero_area_needles_excluded': needles, 'palm_faces_outside_p5_p95_or_aniso_gt_p95': len(out),
        'palm_faces_ratio_outside_p5_p95': sum(1 for x in palm if x[1] < thresholds['ratio_p5'] or x[1] > thresholds['ratio_p95']),
        'palm_faces_aniso_gt_p95': sum(1 for x in palm if x[2] > thresholds['aniso_p95']),
        'palm_area_outliers_cm2': round(sum(x[0].calc_area() for x in out) * 1e4, 3),
        'palm_ratio_min_median_max': [round(pr[0], 5), round(pr[len(pr) // 2], 5), round(pr[-1], 5)],
        'palm_aniso_max': round(max(x[2] for x in palm), 2),
        'thresholds_from_whole_hand': {k: round(v, 5) for k, v in thresholds.items()}}


def reproject_palm_uv(bm, palm_id, region, report_key, report):
    """Planar-project each stretched palm face into `region` (uniform light plate used by the phalanx bodies) at the
    phalanx texel density, centred on the face: UV/3D ratio = density^2 and anisotropy 1 (the plate is uniform, so
    per-face seams are invisible)."""
    bm.normal_update()
    out, thr, before = uv_stats(bm, palm_id)
    faces = [x[0] for x in out]
    dens = region.density
    G.uv_planar_faces(bm, faces, region)
    # nothing may land on the clamp border (clamped = stretched)
    uv = bm.loops.layers.uv[G.UV_NAME]
    m = region.size * 0.04
    clamped = sum(1 for f in faces for l in f.loops
                  if min(abs(l[uv].uv.x - (region.u0 + m)), abs(l[uv].uv.x - (region.u0 + region.size - m)),
                         abs(l[uv].uv.y - (region.v0 + m)), abs(l[uv].uv.y - (region.v0 + region.size - m))) < 1e-7)
    _, _, after = uv_stats(bm, palm_id, thr)
    inside = all(region.u0 <= l[uv].uv.x <= region.u0 + region.size and region.v0 <= l[uv].uv.y <= region.v0 + region.size
                 for f in faces for l in f.loops)
    report[report_key] = {'before': before, 'after': after, 'reprojected_faces': len(faces),
                          'region_px_of_2048': [round(region.u0 * 2048), round(region.v0 * 2048), round(region.size * 2048)],
                          'density_uv_per_m': round(dens, 5), 'phalanx_density_uv_per_m': round(region.density, 5),
                          'all_inside_region': inside, 'loops_on_clamp_border': clamped,
                          'note': 'region = UV_LIGHT, the uniform light plate (lum 0.81) of the v01 atlas already used by '
                                  'the phalanx bodies; texture reuse by design, so it is shared with those faces only'}
    return report[report_key]


# ================================================================ mirror per LOD
def mirror_dev(obj_r, obj_l):
    mr, ml = obj_r.matrix_world, obj_l.matrix_world
    a, b = obj_r.data.vertices, obj_l.data.vertices
    if len(a) != len(b):
        return None
    m = 0.0
    for va, vb in zip(a, b):
        p = mr @ va.co; q = ml @ vb.co
        m = max(m, (q - Vector((-p.x, p.y, p.z))).length)
    return round(m * 1000, 5)


def mirror_report():
    out = {}
    for part in ('Hand', 'Forearm'):
        for k in ('LOD0', 'LOD1', 'LOD2', 'FPS'):
            r = bpy.data.objects.get('%s_R_%s' % (part, k)); l = bpy.data.objects.get('%s_L_%s' % (part, k))
            if r and l:
                out['%s_%s' % (part, k)] = {'tris_R': G.tri_count_mesh(r.data), 'tris_L': G.tri_count_mesh(l.data),
                                            'max_dev_mm': mirror_dev(r, l)}
    return out


# ================================================================ whole-scene measurement (before / after)
def elbow_plane(report_or_none, side, arm):
    b = arm.data.bones
    E = arm.matrix_world @ b['Forearm_' + side].head_local
    return E


def measure_scene(arm):
    res = {'elbow': {}, 'palm': {}, 'mirror': mirror_report(), 'wrist_spigot': {}}
    bodies = {k: world_bm(bpy.data.objects['Body_' + k]) for k in ('LOD0', 'LOD1', 'LOD2')}
    for side in ('R', 'L'):
        E = elbow_plane(None, side, arm)
        for k in ('LOD0', 'LOD1', 'LOD2', 'FPS'):
            fo = bpy.data.objects.get('Forearm_%s_%s' % (side, k))
            fb = world_bm(fo)
            rf = region_faces(fb, E, ELBOW_RADIUS)
            th = face_thickness(fb, rf, T_ELBOW)
            isl = [island_stats(c) for c in G.components(fb.faces[:])]
            ent = {'islands': len(isl), 'small_or_thin_islands': [s for s in isl if s['volume_cm3'] < 1.0 or s['obb_min_mm'] < 1.5],
                   'thin_faces_lt_1p5mm': len(th), 'min_thickness_mm': round(min(th.values()) * 1000, 3) if th else None,
                   'self_coplanar_overlaps': self_coplanar_pairs(fb, rf)}
            body = bodies['LOD0' if k == 'FPS' else k]
            if k != 'FPS':
                cop, ov = cross_coplanar(fb, rf, body)
                ent['coplanar_faces_vs_body'] = cop
                ent['triangle_overlaps_vs_body'] = ov
            res['elbow']['Forearm_%s_%s' % (side, k)] = ent
            fb.free()
        for k, bb in bodies.items():
            rf = region_faces(bb, E, ELBOW_RADIUS)
            th = face_thickness(bb, rf, T_ELBOW)
            isl = [island_stats(c) for c in G.components(bb.faces[:])]
            res['elbow']['Body_%s_%s' % (k, side)] = {
                'islands': len(isl), 'small_or_thin_islands': [s for s in isl if s['volume_cm3'] < 1.0 or s['obb_min_mm'] < 1.5],
                'thin_faces_lt_1p5mm': len(th), 'min_thickness_mm': round(min(th.values()) * 1000, 3) if th else None,
                'self_coplanar_overlaps': self_coplanar_pairs(bb, rf)}
    for b in bodies.values():
        b.free()
    # palm (Hand_R_LOD0, piece 0 = Palm_R)
    for k in ('LOD0', 'LOD1', 'LOD2'):
        hb = world_bm(bpy.data.objects['Hand_R_' + k])
        pc = hb.faces.layers.int['ir_piece']
        palm = [f for f in hb.faces if f[pc] == 0]
        vt = vertex_thickness(hb, {v for f in palm for v in f.verts}, T_PALM, pc)
        ft = face_thickness(hb, palm, T_PALM, pc)
        hb.normal_update()
        tips = [round(math.degrees(sum(l.calc_angle() for l in v.link_loops)), 1) for v in {v for f in palm for v in f.verts}
                if sum(l.calc_angle() for l in v.link_loops) < math.radians(SHARP_SUM_DEG)]
        _, thr, st = uv_stats(hb, 0)
        res['palm']['Hand_R_' + k] = {'fin_vertices_lt_1mm': len(vt), 'fin_faces_lt_1mm': len(ft),
                                      'blade_tip_vertices_angle_sum_lt_200deg': sorted(tips),
                                      'min_vertex_thickness_mm': round(min(vt.values()) * 1000, 3) if vt else None,
                                      'uv': st,
                                      'palm_islands': [island_stats(c) for c in G.components(palm)]}
        hb.free()
    # wrist spigot profile (socket frame)
    S = bpy.data.objects['Wrist_R'].matrix_world.normalized() if 'Wrist_R' in bpy.data.objects else None
    if S is not None:
        Si = S.inverted()
        fo = bpy.data.objects['Forearm_R_LOD0']
        pts = [Si @ (fo.matrix_world @ v.co) for v in fo.data.vertices]
        neck = sorted({(round(math.hypot(p.x, p.y) * 1000, 2), round(p.z * 1000, 2)) for p in pts
                       if -12.001e-3 <= p.z <= 3.001e-3 and math.hypot(p.x, p.y) > 0.02}, key=lambda t: t[1])
        res['wrist_spigot'] = {'profile_r_z_mm_between_flange_and_tip': neck,
                               'max_radius_in_spigot_zone_mm': max((r for r, z in neck if z > -12.0), default=None)}
    return res


# ================================================================ renders (close-ups requested in iter4)
def _action_quats(act):
    """{bone: Euler} from an IR_v02_* action (frame 1); works with slotted (layered) and legacy actions."""
    curves = []
    try:
        curves = list(act.fcurves)
    except Exception:
        pass
    if not curves:
        try:
            for layer in act.layers:
                for strip in layer.strips:
                    for slot in act.slots:
                        cb = strip.channelbag(slot)
                        if cb:
                            curves += list(cb.fcurves)
        except Exception:
            pass
    rot = {}
    for fc in curves:
        if fc.data_path.endswith('rotation_euler'):
            bone = fc.data_path.split('"')[1]
            rot.setdefault(bone, [0.0, 0.0, 0.0])[fc.array_index] = fc.evaluate(1)
    return {b: Euler(v, 'XYZ') for b, v in rot.items()}


def apply_euler_pose(arm, eul):
    for pb in arm.pose.bones:
        pb.rotation_mode = 'XYZ'
        pb.location = (0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
    for n, e in eul.items():
        pb = arm.pose.bones.get(n)
        if pb:
            pb.rotation_euler = e
    bpy.context.view_layer.update()


def extra_renders(arm, set_pose, out_dir, prefix=''):
    import refine_protagonist_v02_verify as V
    rd = Path(out_dir).resolve()
    rd.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    cam = scene.camera if scene.camera and scene.camera.name.startswith('IR_v02_Cam') else V.setup_render(scene)
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.hide_render = not o.name.endswith('LOD0')
        elif o.type in ('EMPTY', 'ARMATURE'):
            o.hide_render = True
    done = []
    S = bpy.data.objects['Wrist_R'].matrix_world.normalized()
    X, Y, Z = S.col[0].xyz, S.col[1].xyz, S.col[2].xyz
    O = S.translation
    set_pose('Open')
    dg = bpy.context.evaluated_depsgraph_get(); dg.update()
    # elbow cut close-ups (body cap + forearm neck)
    E = arm.matrix_world @ arm.data.bones['Forearm_R'].head_local
    ua = arm.data.bones['UpperArm_R']
    n = ((arm.matrix_world @ ua.tail_local) - (arm.matrix_world @ ua.head_local)).normalized()
    P = E - n * 0.04
    for name, d in (('elbow_R_cut_front', Vector((-0.6, -0.8, 0.15))), ('elbow_R_cut_outer', Vector((-1.0, -0.15, 0.1))),
                    ('elbow_R_cut_back', Vector((-0.5, 0.85, 0.1)))):
        V.look(cam, P + d.normalized() * 0.22, P, lens=85)
        V.render_to(scene, rd / (prefix + name + '.png'), done)
    # wrist spigot, perpendicular to the socket axis
    V.look(cam, O + (X * 0.9 + Y * 0.45).normalized() * 0.16 - Z * 0.006, O - Z * 0.006, up=-Z, lens=85)
    V.render_to(scene, rd / (prefix + 'wrist_R_spigot_side.png'), done)
    V.look(cam, O + (-X * 0.7 + Y * 0.7).normalized() * 0.16 - Z * 0.006, O - Z * 0.006, up=-Z, lens=85)
    V.render_to(scene, rd / (prefix + 'wrist_R_spigot_dorsal.png'), done)
    # palm: palmar (+X for R) and dorsal (-X), open hand, without body
    hidden = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(('Body_', 'Hand_L', 'Forearm_L'))]
    for o in hidden:
        o.hide_render = True
    hand = bpy.data.objects['Hand_R_LOD0']
    c = V.posed_center(hand, dg)
    c = c + (O - c) * 0.25
    V.look(cam, c + Vector((1.0, -0.12, 0.05)).normalized() * 0.42, c, lens=85)
    V.render_to(scene, rd / (prefix + 'palm_R_palmar.png'), done)
    V.look(cam, c + Vector((-1.0, -0.12, 0.05)).normalized() * 0.42, c, lens=85)
    V.render_to(scene, rd / (prefix + 'palm_R_dorsal.png'), done)
    set_pose('Fist')
    dg.update()
    c = V.posed_center(hand, dg)
    V.look(cam, c + Vector((0.55, -0.75, 0.25)).normalized() * 0.30, c, lens=85)
    V.render_to(scene, rd / (prefix + 'fist_R_palm_closeup.png'), done)
    V.look(cam, c + Vector((0.0, -1.0, 0.0)) * 0.30, c, lens=85)
    V.render_to(scene, rd / (prefix + 'fist_R_front_closeup.png'), done)
    for o in hidden:
        o.hide_render = not o.name.endswith('LOD0')
    set_pose(None)
    return done


# ================================================================ standalone "measure" mode
def _main():
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    if '--measure' not in args:
        return
    out = Path(args[args.index('--measure') + 1])
    if '--texroot' in args:     # a copied blend (iter3/) keeps '//' image paths relative to the original folder
        root = Path(args[args.index('--texroot') + 1]).resolve()
        for img in bpy.data.images:
            if img.filepath.startswith('//') and not Path(bpy.path.abspath(img.filepath)).exists():
                cand = (root / img.filepath[2:].replace('\\', '/')).resolve()
                if cand.exists():
                    img.filepath = str(cand)
                    img.reload()
    arm = bpy.data.objects['IR_Rig']
    res = {'blend': bpy.data.filepath, 'measure': measure_scene(arm)}
    out.write_text(json.dumps(res, indent=1, default=str), encoding='utf-8')
    if '--renders' in args:
        acts = {a.name[len('IR_v02_'):]: _action_quats(a) for a in bpy.data.actions if a.name.startswith('IR_v02_')}
        def set_pose(name):
            apply_euler_pose(arm, acts.get(name, {}) if name else {})
        res['renders'] = extra_renders(arm, set_pose, args[args.index('--renders') + 1],
                                       prefix=args[args.index('--prefix') + 1] if '--prefix' in args else '')
        out.write_text(json.dumps(res, indent=1, default=str), encoding='utf-8')
    print('[IR-v02-cleanup] measured ->', out)


if __name__ == '__main__':
    _main()
