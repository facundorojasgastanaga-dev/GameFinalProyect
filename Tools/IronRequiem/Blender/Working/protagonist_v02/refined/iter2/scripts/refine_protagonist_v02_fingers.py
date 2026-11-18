"""IR-09 iter2: clean mechanical fingers (hinged phalanges) for refine_protagonist_v02.py.

Every phalanx is a chamfered octagonal prism between two joints. Joint = hinge on the flexion axis through the
(recentred) bone head: the proximal end of each phalanx carries a centred drum (tongue, radius = half height) and
the distal end of the previous phalanx (or the palm) carries two cheeks of the same radius that hug it; both are
solids of revolution about the joint axis, so between 0 and 100 deg nothing opens or collides inside the joint.
Outside the joint radius the dorsal end faces meet at 0 deg and the palmar ends are relieved by ALPHA each side
(sum = 100 deg), so no body-body contact appears inside the flexion range.
All coordinates are Blender world metres, right hand; the left hand is an exact X mirror (done by the caller).
"""
import math
import bmesh
import bpy
from mathutils import Vector, Matrix

import refine_protagonist_v02_geo as G

FINGERS = ('Index', 'Middle', 'Ring', 'Little')
CHAINS = FINGERS + ('Thumb',)
# (width along the flexion axis, height dorsal-palmar) in metres for phalanx 1, 2, 3
DIMS = {'Index': [(0.0235, 0.0210), (0.0220, 0.0195), (0.0200, 0.0180)],
        'Middle': [(0.0235, 0.0210), (0.0220, 0.0195), (0.0200, 0.0180)],
        'Ring': [(0.0225, 0.0205), (0.0210, 0.0190), (0.0195, 0.0175)],
        'Little': [(0.0205, 0.0190), (0.0190, 0.0175), (0.0175, 0.0160)],
        'Thumb': [(0.0230, 0.0210), (0.0215, 0.0195), (0.0200, 0.0180)]}
CHAMFER = 0.0018
SCALE_W = 1.0
SCALE_H = 0.85   # thinner than the 20-22 mm brief so a fist closes (fingers are long vs the palm)
ALPHA = math.radians(50.0)          # palmar relief per side (2 x 50 = 100 deg flexion)
TONGUE = 0.48                       # drum width / phalanx width
GAP_R = 0.0004                      # radial clearance drum/slot and cheek/scoop
GAP_A = 0.0006                      # axial clearance drum/cheek
SEG_HINGE = 6
SEG_HINGE_SMALL = 6   # joints with radius < 10.4 mm (budget)
HINGE_CHAMFER = 0.0   # plain drums/cheeks (budget); bodies carry the 1.8 mm chamfers
PLATE = {'width': 0.62, 'thick': 0.0016, 'overlap': 0.0004, 'chamfer': 0.0}
SWEEP_RANGE = (-14.0, 96.0)          # palm clearance swept by phalanx 1 (Open hyper-extension .. fist MCP + margin)

KIND_HINGE, KIND_BODY, KIND_PLATE = 4, 5, 6


class Regions:
    def __init__(self, hinge, body, plate):
        self.hinge, self.body, self.plate = hinge, body, plate


# ---------------------------------------------------------------- primitives
def octagon(hw, hh, c):
    if c <= 0:
        return [(hw, hh), (-hw, hh), (-hw, -hh), (hw, -hh)]
    c = min(c, hw * 0.45, hh * 0.45)
    return [(hw, hh - c), (hw - c, hh), (-hw + c, hh), (-hw, hh - c), (-hw, -hh + c), (-hw + c, -hh), (hw - c, -hh),
            (hw, -hh + c)]


def loft(bm, origin, d, A, W, rings, chamfer, kind, region, cu=0.0, cv=0.0):
    """Octagonal loft along d. rings: [(t, half_width, half_height)]; section in (A, W) centred at (cu, cv)."""
    kd = bm.faces.layers.int['ir_kind']
    loops = []
    for (t, hw, hh) in rings:
        loops.append([bm.verts.new(origin + d * t + A * (cu + u) + W * (cv + v)) for (u, v) in octagon(hw, hh, chamfer)])
    faces = []
    n = len(loops[0])
    for a, b in zip(loops, loops[1:]):
        for i in range(n):
            j = (i + 1) % n
            faces.append(bm.faces.new([a[i], a[j], b[j], b[i]]))
    faces.append(bm.faces.new(list(reversed(loops[0]))))
    faces.append(bm.faces.new(loops[-1]))
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    for f in faces:
        f[kd] = kind
    G.uv_box_faces(bm, faces, region, origin)
    return faces


def clip(bm, p, n_inward, region, kind):
    caps = G.cut_keep(bm, p, n_inward, keep_positive=True, region=region)
    kd = bm.faces.layers.int['ir_kind']
    for f in caps:
        f[kd] = kind
    G.triangulate_ngons(bm)


def clearance_radius(R):
    """Vertex radius of a polygonal clearance cutter whose flats clear a rotating polygon of vertex radius R."""
    n = SEG_HINGE if R >= 0.0104 else SEG_HINGE_SMALL
    return (R + GAP_R) / math.cos(math.pi / n)


def cylinder(bm, center, axis, radius, length, chamfer, region, segs_for=None):
    kd = bm.faces.layers.int['ir_kind']
    r_ref = radius if segs_for is None else segs_for
    segs = SEG_HINGE if r_ref >= 0.0104 else SEG_HINGE_SMALL
    if chamfer <= 0:
        fr = G.orthonormal_frame(center, axis, Vector((0, 0, 1)) if abs(Vector(axis).normalized().z) < 0.9
                                 else Vector((1, 0, 0)))
        h = length / 2
        fs = G.lathe(bm, fr, [(0.0, -h), (radius, -h), (radius, h), (0.0, h)], segs, region, 0, KIND_HINGE)
    else:
        fs = G.bevelled_cylinder(bm, center, axis, radius, length, segs, chamfer, region, 0, KIND_HINGE)
    for f in fs:
        f[kd] = KIND_HINGE
    return fs


def to_obj(bm, name, col):
    G.triangulate_ngons(bm)
    me = G.bm_to_mesh(bm, name)
    o = bpy.data.objects.new(name, me)
    col.objects.link(o)
    return o


def new_bm():
    bm = bmesh.new()
    G.ensure_layers(bm)
    return bm


def kill(o):
    me = o.data
    bpy.data.objects.remove(o, do_unlink=True)
    if me.users == 0:
        bpy.data.meshes.remove(me)


def bool_many(target_obj, objs, op, col):
    """Manifold boolean of target with a temporary collection of objs; returns a new bmesh."""
    sub = bpy.data.collections.new('IR_tmp_ops')
    col.children.link(sub)
    for o in objs:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        sub.objects.link(o)
    me = G.boolean_eval(target_obj, sub, op, solver='MANIFOLD')
    for o in list(sub.objects):
        kill(o)
    bpy.data.collections.remove(sub)
    bm = bmesh.new(); bm.from_mesh(me); G.ensure_layers(bm)
    bpy.data.meshes.remove(me)
    return bm


# ---------------------------------------------------------------- chain description
def chain_spec(B, axes, centers, side='R'):
    """Joint/segment frames per chain. A_eff is oriented so that flexion = rotation by -theta about A_eff and moves
    the phalanx towards +W (palm side)."""
    out = {}
    for F in CHAINS:
        segs = []
        for s in (1, 2, 3):
            b = B['%s%d' % (F, s)]
            J0 = b['head'].copy()
            if s < 3:
                J1 = B['%s%d' % (F, s + 1)]['head'].copy()
                d = (J1 - J0).normalized(); L = (J1 - J0).length
            else:
                d = b['y'].copy(); L = (b['tail'] - b['head']).length; J1 = J0 + d * L
            a = axes['%s%d_%s' % (F, s, side)].copy()
            if F == 'Thumb':
                a = -a            # library thumb flexion is +theta about its axis
            a = (a - d * a.dot(d)).normalized()
            W = d.cross(a).normalized()
            w, h = DIMS[F][s - 1]
            w, h = w * SCALE_W, h * SCALE_H
            segs.append({'bone': '%s%d_%s' % (F, s, side), 'J0': J0, 'J1': J1, 'd': d, 'A': a, 'W': W, 'L': L,
                         'hw': w / 2, 'hh': h / 2, 'R': h / 2})
        # centre of the finger along the flexion axis (v01 finger centre) at every joint
        for sg in segs:
            sg['cu'] = 0.0 if F == 'Thumb' else (centers[F] - sg['J0']).dot(sg['A'])
        out[F] = segs
    return out


def body_rings(sg, tip):
    hw, hh, L = sg['hw'], sg['hh'], sg['L']
    if not tip:
        return [(0.0, hw, hh), (L, hw, hh)]
    return [(0.0, hw, hh), (L - 0.0065, hw, hh), (L - 0.0025, hw * 0.86, hh * 0.84), (L, hw * 0.58, hh * 0.55)]


def clipped_body(sg, tip, regions, grow=0.0):
    bm = new_bm()
    rings = [(t, hw + grow, hh + grow) for (t, hw, hh) in body_rings(sg, tip)]
    loft(bm, sg['J0'], sg['d'], sg['A'], sg['W'], rings, CHAMFER, KIND_BODY, regions.body, cu=sg['cu'])
    d, W = sg['d'], sg['W']
    clip(bm, sg['J0'], d * math.cos(ALPHA) - W * math.sin(ALPHA), regions.body, KIND_BODY)
    if not tip:
        clip(bm, sg['J1'], -d * math.cos(ALPHA) - W * math.sin(ALPHA), regions.body, KIND_BODY)
    return bm


def cheek_pair(bm, J, A, cu, R, tongue_half, outer_half, region):
    inner = tongue_half + GAP_A
    if outer_half - inner < 0.0015:
        return
    ln = outer_half - inner
    for sgn in (1, -1):
        c = J + A * (cu + sgn * (inner + ln / 2))
        cylinder(bm, c, A, R, ln, HINGE_CHAMFER, region)


def plate(bm, sg, tip, regions):
    hw, hh, L = sg['hw'], sg['hh'], sg['L']
    t0, t1 = (0.24 * L, 0.70 * L) if tip else (0.24 * L, 0.76 * L)
    ph = (PLATE['thick'] + PLATE['overlap']) / 2
    cv = -(hh + PLATE['thick'] / 2 - PLATE['overlap'] / 2)
    loft(bm, sg['J0'], sg['d'], sg['A'], sg['W'], [(t0, hw * PLATE['width'], ph), (t1, hw * PLATE['width'], ph)],
         PLATE['chamfer'], KIND_PLATE, regions.plate, cu=sg['cu'], cv=cv)


def tidy(bm, angle_deg=0.6):
    """Merge the coplanar slivers left by the booleans (UV/kind islands kept) and re-triangulate beautifully."""
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=bm.edges[:])
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(angle_deg), use_dissolve_boundaries=False,
                             verts=bm.verts[:], edges=bm.edges[:], delimit={'UV'})
    G.triangulate_ngons(bm, limit=3)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=bm.edges[:])
    G.triangulate_ngons(bm, limit=3)
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    return bm


def build_phalanx(spec_chain, s, regions, col):
    """Return bmesh of phalanx s (1..3) of a chain: body - cheek-zone scoop (prox) - slot (dist) + drum + cheeks
    of the next joint + dorsal plate."""
    sg = spec_chain[s - 1]
    tip = s == 3
    body = to_obj(clipped_body(sg, tip, regions), 'IR_tmp_body', col)
    cut = []
    # proximal scoop in the cheek zones (where the parent's cheeks sit)
    th = sg['hw'] * TONGUE
    sc = new_bm()
    ln = sg['hw'] + 0.003 - th
    for sgn in (1, -1):
        c = sg['J0'] + sg['A'] * (sg['cu'] + sgn * (th + ln / 2))
        cylinder(sc, c, sg['A'], clearance_radius(sg['R']), ln, 0.0, regions.hinge, segs_for=sg['R'])
    cut.append(to_obj(sc, 'IR_tmp_scoop', col))
    if not tip:
        nx = spec_chain[s]
        slot = new_bm()
        cylinder(slot, nx['J0'] + nx['A'] * nx['cu'], nx['A'], clearance_radius(nx['R']),
                 2 * (nx['hw'] * TONGUE + GAP_A), 0.0, regions.hinge, segs_for=nx['R'])
        cut.append(to_obj(slot, 'IR_tmp_slot', col))
    bm = bool_many(body, cut, 'DIFFERENCE', col)
    kill(body)
    base = to_obj(bm, 'IR_tmp_base', col)
    add = new_bm()
    cylinder(add, sg['J0'] + sg['A'] * sg['cu'], sg['A'], sg['R'], 2 * th, HINGE_CHAMFER, regions.hinge)
    if not tip:
        nx = spec_chain[s]
        cheek_pair(add, nx['J0'], nx['A'], nx['cu'], nx['R'], nx['hw'] * TONGUE, nx['hw'], regions.hinge)
    plate(add, sg, tip, regions)
    parts = [to_obj(add, 'IR_tmp_add', col)]
    out = bool_many(base, parts, 'UNION', col)
    kill(base)
    return tidy(out)


def sweep_solid(sg, regions, rmax=0.05, n=4, hw_override=None, cu_override=None):
    """Extruded 2-D envelope of phalanx 1 rotating about its joint between SWEEP_RANGE (radius >= half height:
    angular range widened by asin(h/r); radius < half height: full disc)."""
    h = sg['hh'] + 0.0006
    a0, a1 = (math.radians(x) for x in SWEEP_RANGE)
    L = min(rmax, sg['L'])
    rs = [h + (L - h) * i / (n - 1) for i in range(n)]
    def pt(r, ang):     # angle measured from d towards +W (flexion side)
        return (r * math.cos(ang), r * math.sin(ang))
    outer = [pt(r, a1 + math.asin(min(1.0, h / r))) for r in rs]
    inner = [pt(r, a0 - math.asin(min(1.0, h / r))) for r in reversed(rs)]
    s0 = a0 - math.pi / 2; s1 = a1 + math.pi / 2       # back arc at radius h closing the full disc
    arc = [pt(h, s0 - (2 * math.pi - (s1 - s0)) * k / 5) for k in range(1, 5)]
    poly = outer + inner + arc
    bm = new_bm()
    kd = bm.faces.layers.int['ir_kind']
    hw = (hw_override if hw_override is not None else sg['hw']) + 0.0012
    cu = cu_override if cu_override is not None else sg['cu']
    d, W, A, J = sg['d'], sg['W'], sg['A'], sg['J0'] + sg['A'] * cu
    lo = [bm.verts.new(J + d * x + W * y - A * hw) for x, y in poly]
    hi = [bm.verts.new(J + d * x + W * y + A * hw) for x, y in poly]
    fs = []
    m = len(poly)
    for i in range(m):
        j = (i + 1) % m
        fs.append(bm.faces.new([lo[i], lo[j], hi[j], hi[i]]))
    fs.append(bm.faces.new(list(reversed(lo))))
    fs.append(bm.faces.new(hi))
    bmesh.ops.recalc_face_normals(bm, faces=fs)
    for f in fs:
        f[kd] = KIND_HINGE
    G.uv_box_faces(bm, fs, regions.hinge, J)
    G.triangulate_ngons(bm)
    return bm


def palm_cutters_and_cheeks(spec, regions, col):
    """Objects to subtract from the palm (slot + swept clearance of phalanx 1) and a bmesh of palm cheeks."""
    cutters = []
    cheeks = new_bm()
    for F in CHAINS:
        sg = spec[F][0]
        th = sg['hw'] * TONGUE
        slot = new_bm()
        cylinder(slot, sg['J0'] + sg['A'] * sg['cu'], sg['A'], clearance_radius(sg['R']), 2 * (th + GAP_A), 0.0,
                 regions.hinge, segs_for=sg['R'])
        cutters.append(to_obj(slot, 'IR_tmp_pslot', col))
        cheek_pair(cheeks, sg['J0'], sg['A'], sg['cu'], sg['R'], th, sg['hw'], regions.hinge)
    # one clearance sweep for the four fingers (their MCP heads share the axis line) + one for the thumb
    ref = spec['Middle'][0]
    lo = min(spec[F][0]['cu'] - spec[F][0]['hw'] + (spec[F][0]['J0'] - ref['J0']).dot(ref['A']) for F in FINGERS)
    hi = max(spec[F][0]['cu'] + spec[F][0]['hw'] + (spec[F][0]['J0'] - ref['J0']).dot(ref['A']) for F in FINGERS)
    big = dict(ref); big['hh'] = max(spec[F][0]['hh'] for F in FINGERS); big['L'] = max(spec[F][0]['L'] for F in FINGERS)
    cutters.append(to_obj(sweep_solid(big, regions, hw_override=(hi - lo) / 2, cu_override=(hi + lo) / 2),
                          'IR_tmp_sweep', col))
    cutters.append(to_obj(sweep_solid(spec['Thumb'][0], regions), 'IR_tmp_sweep_t', col))
    return cutters, cheeks


def build_hand(palm_bm, spec, regions, col, report):
    """palm_bm: refined v01 palm (world). Returns (bmesh with ir_piece ids, table)."""
    table = [{'id': 0, 'name': 'Palm_R', 'bone': 'Hand_R'}]
    palm_obj = to_obj(palm_bm, 'IR_tmp_palm', col)
    cutters, cheeks = palm_cutters_and_cheeks(spec, regions, col)
    t0 = G.tri_count_mesh(palm_obj.data)
    pb = bool_many(palm_obj, cutters, 'DIFFERENCE', col)
    kill(palm_obj)
    comps = sorted(G.components(pb.faces[:]), key=len, reverse=True)
    dropped = [len(c) for c in comps[1:]]
    for c in comps[1:]:                      # thin palm slivers left between the clearance cuts
        bmesh.ops.delete(pb, geom=list(c), context='FACES')
    report['palm_slivers_dropped_faces'] = dropped
    palm2 = to_obj(pb, 'IR_tmp_palm2', col)
    pb = bool_many(palm2, [to_obj(cheeks, 'IR_tmp_pcheeks', col)], 'UNION', col)
    kill(palm2)
    pb = tidy(pb)
    out = new_bm()
    G.append_bm(out, pb, piece=0)
    report['palm_tris'] = {'before_carve': t0, 'after_carve_and_cheeks': G.tri_count_bm(pb)}
    pb.free()
    pid = 1
    for F in CHAINS:
        for s in (1, 2, 3):
            ph = build_phalanx(spec[F], s, regions, col)
            name = spec[F][s - 1]['bone']
            comps = len(G.components(ph.faces[:]))
            table.append({'id': pid, 'name': name, 'bone': name, 'tris': G.tri_count_bm(ph), 'components': comps})
            G.append_bm(out, ph, piece=pid)
            ph.free()
            pid += 1
    G.triangulate_ngons(out)
    report['fingers_v2'] = {'dims_m': DIMS, 'palmar_relief_deg_each_side': math.degrees(ALPHA),
                            'drum_width_ratio': TONGUE, 'clearance_radial_mm': GAP_R * 1000,
                            'clearance_axial_mm': GAP_A * 1000, 'hinge_segments': SEG_HINGE,
                            'palm_sweep_deg': list(SWEEP_RANGE),
                            'pieces': [{k: v for k, v in t.items()} for t in table[1:]]}
    return out, table
