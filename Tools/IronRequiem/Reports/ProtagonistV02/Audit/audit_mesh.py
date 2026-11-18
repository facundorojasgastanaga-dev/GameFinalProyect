"""IRON REQUIEM protagonist audit (read-only). Run:
blender --background --factory-startup --python-exit-code 1 --python audit_mesh.py -- <source> <out.json> [uvres]
Imports/opens the source in a fresh factory session; never saves anything."""
import bpy, bmesh, sys, json, math, time
import numpy as np
from mathutils import Vector, kdtree, Matrix
from mathutils.bvhtree import BVHTree
from collections import Counter, defaultdict

args = sys.argv[sys.argv.index('--') + 1:]
SRC, OUT = args[0], args[1]
UVRES = int(args[2]) if len(args) > 2 else 1024
TEX = 2048
t0 = time.time()
if SRC.endswith('.blend'):
    bpy.ops.wm.open_mainfile(filepath=SRC, load_ui=False)
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC, use_anim=True)
# rest pose for analysis
for a in bpy.data.objects:
    if a.type == 'ARMATURE':
        if a.animation_data: a.animation_data.action = None
        for p in a.pose.bones:
            p.location = (0, 0, 0); p.rotation_quaternion = (1, 0, 0, 0); p.rotation_euler = (0, 0, 0); p.scale = (1, 1, 1)
bpy.context.view_layer.update()

def pct(a, q):
    return float(np.percentile(a, q)) if len(a) else None

def r(v, n=5):
    return round(float(v), n)

def concave(face):
    n = face.normal
    vs = [v.co for v in face.verts]
    k = len(vs)
    signs = []
    for i in range(k):
        e1 = vs[i] - vs[i - 1]; e2 = vs[(i + 1) % k] - vs[i]
        signs.append(e1.cross(e2).dot(n))
    return any(s < -1e-12 for s in signs) and any(s > 1e-12 for s in signs) or all(s < -1e-12 for s in signs)

def islands_of(bm):
    parent = list(range(len(bm.verts)))
    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]; x = parent[x]
        return x
    for e in bm.edges:
        a, b = find(e.verts[0].index), find(e.verts[1].index)
        if a != b: parent[a] = b
    roots = [find(i) for i in range(len(bm.verts))]
    ids = {}
    lab = [ids.setdefault(x, len(ids)) for x in roots]
    return lab, len(ids)

def analyse_mesh(o):
    me = o.data
    mw = o.matrix_world
    bm = bmesh.new(); bm.from_mesh(me); bm.transform(mw)
    bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table(); bm.edges.ensure_lookup_table()
    bm.normal_update()
    res = {'object': o.name, 'verts': len(bm.verts), 'faces': len(bm.faces),
           'tris': sum(len(f.verts) - 2 for f in bm.faces), 'scale': [r(x) for x in o.scale],
           'rotation_euler': [r(x) for x in o.rotation_euler],
           'origin_world': [r(x) for x in mw.translation]}
    co = np.array([v.co[:] for v in bm.verts]) if len(bm.verts) else np.zeros((0, 3))
    mn, mx = co.min(0), co.max(0)
    res['bounds_min'] = [r(x) for x in mn]; res['bounds_max'] = [r(x) for x in mx]
    res['dimensions'] = [r(x) for x in (mx - mn)]
    res['origin_to_bbox_center'] = [r(x) for x in (mw.translation - Vector(((mn + mx) / 2).tolist()))]
    # topology
    sides = Counter(len(f.verts) for f in bm.faces)
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    conc = [f.index for f in ngons if concave(f)]
    concq = [f.index for f in bm.faces if len(f.verts) == 4 and concave(f)]
    res['face_sides'] = {str(k): v for k, v in sorted(sides.items())}
    res['ngons'] = len(ngons); res['ngons_concave'] = len(conc); res['ngons_concave_idx_sample'] = conc[:20]
    res['ngon_max_sides'] = max((len(f.verts) for f in ngons), default=0)
    res['quads_concave'] = len(concq)
    boundary = [e for e in bm.edges if len(e.link_faces) == 1]
    nonman = [e for e in bm.edges if len(e.link_faces) > 2]
    wire = [e for e in bm.edges if len(e.link_faces) == 0]
    res['edges_boundary'] = len(boundary); res['edges_nonmanifold_gt2'] = len(nonman); res['edges_wire'] = len(wire)
    res['nonmanifold_idx_sample'] = [e.index for e in nonman[:20]]
    res['verts_loose'] = sum(1 for v in bm.verts if not v.link_edges)
    res['verts_no_faces'] = sum(1 for v in bm.verts if not v.link_faces)
    res['verts_nonmanifold'] = sum(1 for v in bm.verts if not v.is_manifold)
    # degenerate
    res['faces_zero_area'] = sum(1 for f in bm.faces if f.calc_area() < 1e-10)
    res['edges_zero_length'] = sum(1 for e in bm.edges if e.calc_length() < 1e-7)
    # duplicates
    keyset = Counter(tuple(sorted(v.index for v in f.verts)) for f in bm.faces)
    res['faces_duplicate_same_verts'] = sum(c - 1 for c in keyset.values() if c > 1)
    poskey = Counter(tuple(sorted(tuple(round(c, 5) for c in v.co) for v in f.verts)) for f in bm.faces)
    res['faces_duplicate_same_positions'] = sum(c - 1 for c in poskey.values() if c > 1)
    vkey = Counter(tuple(round(c, 6) for c in v.co) for v in bm.verts)
    res['verts_coincident_extra'] = sum(c - 1 for c in vkey.values() if c > 1)
    # interior faces (all edges shared by >2 faces)
    res['faces_interior_bmesh'] = sum(1 for f in bm.faces if all(len(e.link_faces) > 2 for e in f.edges))
    # winding consistency
    incons = 0
    for e in bm.edges:
        if len(e.link_faces) == 2:
            l1, l2 = e.link_loops[0], e.link_loops[1]
            if l1.vert == l2.vert: incons += 1
    res['edges_inconsistent_winding'] = incons
    before = [f.normal.copy() for f in bm.faces]
    bm2 = bm.copy(); bm2.faces.ensure_lookup_table()
    bmesh.ops.recalc_face_normals(bm2, faces=bm2.faces[:])
    flipped = [i for i, f in enumerate(bm2.faces) if f.normal.dot(before[i]) < 0]
    res['faces_flipped_vs_recalc_outside'] = len(flipped); res['faces_flipped_idx_sample'] = flipped[:20]
    bm2.free()
    # islands
    lab, nisl = islands_of(bm)
    lab = np.array(lab)
    res['islands'] = nisl
    isl_info = []
    # signed volume per island (closed-ness)
    vol = np.zeros(nisl); fcount = np.zeros(nisl, int); closed = np.ones(nisl, bool)
    for e in boundary + nonman:
        closed[lab[e.verts[0].index]] = False
    for f in bm.faces:
        i = lab[f.verts[0].index]; fcount[i] += 1
        v0 = f.verts[0].co
        for k in range(1, len(f.verts) - 1):
            vol[i] += v0.dot(f.verts[k].co.cross(f.verts[k + 1].co)) / 6.0
    vcount = np.bincount(lab, minlength=nisl)
    kd = kdtree.KDTree(len(bm.verts))
    for v in bm.verts: kd.insert(v.co, v.index)
    kd.balance()
    big = int(np.argmax(vcount)) if nisl else -1
    for i in range(nisl):
        idx = np.where(lab == i)[0]
        c = co[idx]
        info = {'island': i, 'verts': int(len(idx)), 'faces': int(fcount[i]), 'closed': bool(closed[i]),
                'signed_volume_m3': float(vol[i]), 'bbox_min': [r(x, 4) for x in c.min(0)], 'bbox_max': [r(x, 4) for x in c.max(0)],
                'center': [r(x, 4) for x in c.mean(0)]}
        # nearest distance to any other island
        best = 1e9
        if nisl > 1:
            step = max(1, len(idx) // 400)
            for vi in idx[::step]:
                for (p, j, d) in kd.find_n(Vector(co[vi]), 24):
                    if lab[j] != i:
                        best = min(best, d); break
            if best == 1e9:  # fall back: full scan of sampled verts against other island points
                other = co[lab != i]
                if len(other):
                    sub = c[::max(1, len(c) // 200)]
                    best = float(np.min(np.linalg.norm(other[None, ::max(1, len(other) // 4000)] - sub[:, None], axis=2)))
            info['nearest_other_island_m'] = r(best, 6)
        isl_info.append(info)
    res['inverted_closed_islands'] = [d['island'] for d in isl_info if d['closed'] and d['signed_volume_m3'] < 0]
    res['open_islands'] = sum(1 for d in isl_info if not d['closed'])
    res['floating_islands_gt_1mm'] = [{'island': d['island'], 'verts': d['verts'], 'dist_m': d.get('nearest_other_island_m'), 'center': d['center']}
                                      for d in isl_info if d.get('nearest_other_island_m', 0) > 0.001]
    res['tiny_islands_lt_8verts'] = sum(1 for d in isl_info if d['verts'] < 8)
    res['island_list'] = isl_info if nisl <= 400 else sorted(isl_info, key=lambda d: -d['verts'])[:400]
    # hidden / enclosed faces: ray from centroid along normal hits a back face of the same object
    bvh = BVHTree.FromBMesh(bm)
    nf = len(bm.faces); stepf = max(1, nf // 60000)
    enclosed = 0; tested = 0; enclosed_idx = []
    for f in list(bm.faces)[::stepf]:
        tested += 1
        c = f.calc_center_median(); n = f.normal
        if n.length < .5: continue
        loc, nor, fi, d = bvh.ray_cast(c + n * 1e-4, n, 1.0)
        if fi is not None and nor.dot(n) > 0.2 and fi != f.index:
            loc2, nor2, fi2, d2 = bvh.ray_cast(c - n * 1e-4, -n, 1.0)
            if fi2 is not None and fi2 != f.index:
                enclosed += 1
                if len(enclosed_idx) < 20: enclosed_idx.append(f.index)
    res['faces_enclosed_by_own_shell'] = {'tested': tested, 'count': enclosed, 'fraction': r(enclosed / max(1, tested), 4), 'idx_sample': enclosed_idx, 'sample_step': stepf}
    # self intersections
    if res['tris'] <= 120000:
        pairs = bvh.overlap(bvh)
        polys = [set(v.index for v in f.verts) for f in bm.faces]
        real = [(a, b) for a, b in pairs if a < b and not (polys[a] & polys[b])]
        isl_pairs = Counter()
        for a, b in real:
            ia, ib = lab[bm.faces[a].verts[0].index], lab[bm.faces[b].verts[0].index]
            isl_pairs['same_island' if ia == ib else 'cross_island'] += 1
        res['self_intersections'] = {'face_pairs': len(real), **isl_pairs, 'idx_sample': real[:15]}
    else:
        res['self_intersections'] = 'skipped (too many tris for BVH self-overlap in Python)'
    # UV
    uvl = bm.loops.layers.uv.active
    res['uv_layers'] = [u.name for u in me.uv_layers]
    if uvl is not None:
        a3 = np.array([f.calc_area() for f in bm.faces])
        auv = []; flipped_uv = 0
        for f in bm.faces:
            uv = [l[uvl].uv for l in f.loops]
            s = 0.0
            for k in range(len(uv)):
                s += uv[k - 1].x * uv[k].y - uv[k].x * uv[k - 1].y
            s *= .5
            if s < 0: flipped_uv += 1
            auv.append(abs(s))
        auv = np.array(auv)
        ok = a3 > 1e-12
        ratio_raw = auv[ok] / a3[ok]
        glob = auv.sum() / max(a3.sum(), 1e-12)
        rn = ratio_raw / glob if glob > 0 else ratio_raw
        res['uv'] = {
            'uv_area_total_0_1': r(auv.sum(), 5), 'area3d_m2': r(a3.sum(), 5),
            'faces_uv_zero_area': int((auv < 1e-12).sum()), 'faces_uv_flipped_winding': flipped_uv,
            'texel_density_px_per_m_at_2048': r(math.sqrt(glob) * TEX, 1),
            'stretch_ratio_normalised': {'mean': r(rn.mean(), 4), 'p5': r(pct(rn, 5), 4), 'p50': r(pct(rn, 50), 4), 'p95': r(pct(rn, 95), 4), 'min': r(rn.min(), 4), 'max': r(rn.max(), 4)},
            'faces_area_ratio_off_by_2x': int(((rn > 2) | (rn < .5)).sum()),
            'uv_out_of_0_1': int(sum(1 for f in bm.faces for l in f.loops if not (-1e-4 <= l[uvl].uv.x <= 1.0001 and -1e-4 <= l[uvl].uv.y <= 1.0001))),
        }
        # UV islands via union find over faces sharing uv-continuous edges
        fpar = list(range(len(bm.faces)))
        def ff(x):
            while fpar[x] != x:
                fpar[x] = fpar[fpar[x]]; x = fpar[x]
            return x
        for e in bm.edges:
            if len(e.link_loops) != 2: continue
            l1, l2 = e.link_loops
            a1 = l1[uvl].uv; b1 = l1.link_loop_next[uvl].uv
            a2 = l2[uvl].uv; b2 = l2.link_loop_next[uvl].uv
            same = ((a1 - b2).length < 1e-5 and (b1 - a2).length < 1e-5) or ((a1 - a2).length < 1e-5 and (b1 - b2).length < 1e-5)
            if same:
                x, y = ff(l1.face.index), ff(l2.face.index)
                if x != y: fpar[x] = y
        froots = [ff(i) for i in range(len(bm.faces))]
        uid = {}; ulab = np.array([uid.setdefault(x, len(uid)) for x in froots])
        res['uv']['uv_islands'] = len(uid)
        # per UV-island texel density
        per = defaultdict(lambda: [0.0, 0.0])
        for f, au, aa in zip(bm.faces, auv, a3):
            i = int(ulab[f.index]); per[i][0] += au; per[i][1] += aa
        dens = {int(i): r(math.sqrt(u / a) * TEX, 1) for i, (u, a) in per.items() if a > 1e-9}
        dv = np.array(list(dens.values())) if dens else np.array([0.0])
        res['uv']['texel_density_per_uv_island_px_per_m'] = {'min': r(dv.min(), 1), 'p5': r(pct(dv, 5), 1), 'median': r(pct(dv, 50), 1), 'p95': r(pct(dv, 95), 1), 'max': r(dv.max(), 1)}
        res['uv']['texel_density_by_uv_island_largest30'] = dict(list(sorted(dens.items(), key=lambda kv: -per[kv[0]][1]))[:30])
        # rasterise UV triangles
        R = UVRES
        cnt = np.zeros((R, R), np.int32); own = np.full((R, R), -1, np.int32); cross = np.zeros((R, R), bool)
        pairc = Counter()
        tri_uv = []; tri_isl = []
        for f in bm.faces:
            uv = [l[uvl].uv for l in f.loops]
            for k in range(1, len(uv) - 1):
                tri_uv.append((uv[0].x, uv[0].y, uv[k].x, uv[k].y, uv[k + 1].x, uv[k + 1].y)); tri_isl.append(ulab[f.index])
        T = np.array(tri_uv) * R
        for (x0, y0, x1, y1, x2, y2), isl in zip(T, tri_isl):
            minx = max(int(math.floor(min(x0, x1, x2) - .5)), 0); maxx = min(int(math.ceil(max(x0, x1, x2) - .5)), R - 1)
            miny = max(int(math.floor(min(y0, y1, y2) - .5)), 0); maxy = min(int(math.ceil(max(y0, y1, y2) - .5)), R - 1)
            if maxx < minx or maxy < miny: continue
            xs = np.arange(minx, maxx + 1) + .5; ys = np.arange(miny, maxy + 1) + .5
            X, Y = np.meshgrid(xs, ys)
            d = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
            if abs(d) < 1e-12: continue
            w1 = ((X - x0) * (y2 - y0) - (x2 - x0) * (Y - y0)) / d
            w2 = ((x1 - x0) * (Y - y0) - (X - x0) * (y1 - y0)) / d
            m = (w1 > 1e-6) & (w2 > 1e-6) & (w1 + w2 < 1 - 1e-6)
            if not m.any(): continue
            sl = (slice(miny, maxy + 1), slice(minx, maxx + 1))
            o_ = own[sl]; c_ = cnt[sl]; cr = cross[sl]
            other = m & (o_ >= 0) & (o_ != isl)
            if other.any():
                for j, n_ in Counter(o_[other].tolist()).items():
                    pairc[tuple(sorted((int(isl), int(j))))] += n_
                cr[other] = True
            o_[m & (o_ < 0)] = isl
            c_[m] += 1
        cov = int((cnt > 0).sum()); ov = int((cnt > 1).sum()); crossn = int(cross.sum())
        res['uv']['raster'] = {'res': R, 'covered_px': cov, 'coverage_fraction': r(cov / R / R, 4),
                               'overlap_px_total': ov, 'overlap_px_cross_island': crossn, 'overlap_px_same_island': ov - crossn,
                               'overlap_fraction_of_covered': r(ov / max(cov, 1), 4),
                               'overlapping_island_pairs_top': [{'islands': list(k), 'px': v} for k, v in pairc.most_common(15)],
                               'overlapping_island_pairs_count': len(pairc)}
    # materials
    res['materials'] = [m.name if m else None for m in me.materials]
    used = Counter(f.material_index for f in bm.faces)
    res['material_slots_unused'] = [i for i in range(len(me.materials)) if used.get(i, 0) == 0]
    res['material_slots_empty'] = [i for i, m in enumerate(me.materials) if m is None]
    # weights
    if o.vertex_groups:
        gname = {g.index: g.name for g in o.vertex_groups}
        infl = []; sums = []; mixed_pairs = Counter(); dom = []
        for v in me.vertices:
            ws = [(gname[g.group], g.weight) for g in v.groups if g.weight > 0.001]
            infl.append(len(ws)); sums.append(sum(w for _, w in ws))
            if len(ws) >= 2:
                mixed_pairs[' + '.join(sorted(n for n, _ in ws))] += 1
            dom.append(max(ws, key=lambda t: t[1])[0] if ws else None)
        infl = np.array(infl); sums = np.array(sums)
        res['weights'] = {'influence_histogram': {str(k): int(v) for k, v in sorted(Counter(infl.tolist()).items())},
                          'verts_unweighted': int((infl == 0).sum()), 'verts_gt4_influences': int((infl > 4).sum()),
                          'verts_mixed_ge2': int((infl >= 2).sum()),
                          'weight_sum_not_1': int((np.abs(sums - 1) > 0.01).sum()),
                          'groups': [g.name for g in o.vertex_groups],
                          'groups_with_weight': sorted(set(d for d in dom if d)),
                          'mixed_bone_sets_top': dict(mixed_pairs.most_common(25))}
        # rigid piece analysis: islands influenced by more than one bone
        per_isl = defaultdict(lambda: {'bones': Counter(), 'mixed': 0, 'verts': 0})
        for v in me.vertices:
            i = int(lab[v.index]); d = per_isl[i]; d['verts'] += 1
            ws = [gname[g.group] for g in v.groups if g.weight > 0.001]
            for n in ws: d['bones'][n] += 1
            if len(ws) >= 2: d['mixed'] += 1
        multi = [{'island': i, 'verts': d['verts'], 'mixed_verts': d['mixed'], 'bones': dict(d['bones']), 'center': isl_info[i]['center']}
                 for i, d in per_isl.items() if len(d['bones']) > 1]
        res['weights']['islands_total'] = nisl
        res['weights']['islands_with_multiple_bones'] = len(multi)
        res['weights']['verts_in_multi_bone_islands'] = int(sum(d['verts'] for d in multi))
        res['weights']['multi_bone_islands'] = sorted(multi, key=lambda d: -d['verts'])[:60]
    bm.free()
    return res, lab

report = {'source': SRC, 'blender': bpy.app.version_string, 'objects': [], 'armatures': [], 'empties': [], 'actions': [a.name for a in bpy.data.actions]}
labs = {}
for o in bpy.data.objects:
    if o.type == 'MESH':
        print('analysing', o.name, len(o.data.polygons), flush=True)
        d, lab = analyse_mesh(o); report['objects'].append(d); labs[o.name] = lab
    elif o.type == 'EMPTY':
        m = o.matrix_world
        report['empties'].append({'name': o.name, 'parent': o.parent.name if o.parent else None, 'parent_bone': o.parent_bone,
                                  'world_pos': [r(x) for x in m.translation], 'axis_x': [r(x, 4) for x in m.col[0][:3]],
                                  'axis_y': [r(x, 4) for x in m.col[1][:3]], 'axis_z': [r(x, 4) for x in m.col[2][:3]],
                                  'world_scale': [r(x, 4) for x in m.to_scale()]})
    elif o.type == 'ARMATURE':
        bones = []
        used = set()
        for c in bpy.data.objects:
            if c.type == 'MESH' and any(md.type == 'ARMATURE' and md.object == o for md in c.modifiers):
                gname = {g.index: g.name for g in c.vertex_groups}
                for v in c.data.vertices:
                    for g in v.groups:
                        if g.weight > 0.001: used.add(gname[g.group])
        for b in o.data.bones:
            ml = o.matrix_world @ b.matrix_local
            bones.append({'name': b.name, 'parent': b.parent.name if b.parent else None, 'deform': b.use_deform,
                          'head': [r(x) for x in (o.matrix_world @ b.head_local)], 'tail': [r(x) for x in (o.matrix_world @ b.tail_local)],
                          'length': r(b.length), 'axis_x': [r(x, 4) for x in ml.col[0][:3]], 'axis_y': [r(x, 4) for x in ml.col[1][:3]],
                          'axis_z': [r(x, 4) for x in ml.col[2][:3]], 'has_weighted_verts': b.name in used})
        report['armatures'].append({'name': o.name, 'scale': [r(x) for x in o.scale], 'rotation_euler': [r(x) for x in o.rotation_euler],
                                    'bone_count': len(bones), 'bones_without_vertices': [b['name'] for b in bones if not b['has_weighted_verts']],
                                    'bones': bones})

# cross-object: material duplicates
mats = [m for m in bpy.data.materials if m.users]
stem = Counter(m.name.split('.')[0] for m in mats)
def mat_sig(m):
    imgs = []
    if m.use_nodes:
        imgs = sorted(n.image.name for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image)
    return (tuple(round(c, 3) for c in m.diffuse_color), tuple(imgs))
sigs = defaultdict(list)
for m in mats: sigs[mat_sig(m)].append(m.name)
report['materials'] = {'count': len(mats), 'names': [m.name for m in mats],
                       'same_stem_groups': {k: v for k, v in stem.items() if v > 1},
                       'identical_signature_groups': [v for v in sigs.values() if len(v) > 1],
                       'images': [(i.name, list(i.size), i.filepath) for i in bpy.data.images]}
# cross-object intersections between mesh pieces
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
deps = bpy.context.evaluated_depsgraph_get()
def bvh_of(o):
    bm = bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world)
    t = BVHTree.FromBMesh(bm); bm.free(); return t
inter = []
if len(meshes) <= 30 and sum(len(o.data.polygons) for o in meshes) < 300000:
    trees = {o.name: bvh_of(o) for o in meshes}
    for i, a in enumerate(meshes):
        for b in meshes[i + 1:]:
            # skip LOD / FPS duplicates of the same piece family
            fam = lambda n: n.rsplit('_', 1)[-1] if n.rsplit('_', 1)[-1][:3] in ('LOD', 'FPS') else 'all'
            if fam(a.name) != fam(b.name): continue
            n = len(trees[a.name].overlap(trees[b.name]))
            if n: inter.append({'a': a.name, 'b': b.name, 'overlapping_tri_pairs': n})
report['cross_object_intersections_same_lod'] = inter
report['elapsed_s'] = round(time.time() - t0, 1)
with open(OUT, 'w', encoding='utf-8') as fh:
    json.dump(report, fh, indent=1)
print('AUDIT_DONE', SRC, report['elapsed_s'])
