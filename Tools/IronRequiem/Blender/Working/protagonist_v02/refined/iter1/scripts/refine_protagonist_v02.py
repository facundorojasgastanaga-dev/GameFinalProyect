"""IR-09 / A2-A3: refine ProtagonistV01 rig into v02 (rigid mechanical hands, IR-ACOPLE-01 wrist, whole elbow
joint, unique sockets, FPS finish, regenerated LODs) and verify it.

Input  (read only): Tools/IronRequiem/Blender/Working/Protagonist/IR_Protagonist_Rigged.blend
                    Assets/IronRequiem/Art/Player/HandPoses/FPS_R.asset / FPS_L.asset (pose library, read only)
                    Tools/IronRequiem/Exports/PlayerRigidFist/closed_fist.blend (Plan B reference, read only)
Output (only):      Tools/IronRequiem/Blender/Working/protagonist_v02/refined/
                    IR_Protagonist_v02_refined.blend, metrics.json, renders/*.png

Run (background, deterministic):
  blender --background --factory-startup --python-exit-code 1 --python Tools/IronRequiem/Blender/refine_protagonist_v02.py
Options after "--":
  --in-place      use the file already open in this Blender session (must be the v01 rigged source) instead of
                  opening it; the session is modified in memory and a COPY is written to refined/.
  --no-renders    skip Workbench renders.
  --no-verify     skip verification (build + save only).
GUI: blender Tools/IronRequiem/Blender/Working/Protagonist/IR_Protagonist_Rigged.blend --python <this> -- --in-place
     (or run from the Text editor with the env var IR_V02_ARGS=--in-place). Never run --in-place on another file.
"""
import sys
sys.dont_write_bytecode = True   # write nothing outside refined/ (no __pycache__ next to the tools)
import bpy
import bmesh
import json
import math
import os
import re
import sys
import hashlib
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

try:
    HERE = Path(__file__).resolve().parent
    if not (HERE / 'refine_protagonist_v02_geo.py').exists():
        raise NameError
except NameError:   # run from a Text datablock inside the GUI
    HERE = Path(os.environ.get('IR_BLENDER_TOOLS', r'C:\Users\gehbd\ProyectoGameFinal\Tools\IronRequiem\Blender'))
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))
import importlib
import refine_protagonist_v02_geo as G
importlib.reload(G)

ROOT = HERE.parents[2]
SRC = ROOT / 'Tools/IronRequiem/Blender/Working/Protagonist/IR_Protagonist_Rigged.blend'
FIST_SRC = ROOT / 'Tools/IronRequiem/Exports/PlayerRigidFist/closed_fist.blend'
POSE_LIB = {'R': ROOT / 'Assets/IronRequiem/Art/Player/HandPoses/FPS_R.asset',
            'L': ROOT / 'Assets/IronRequiem/Art/Player/HandPoses/FPS_L.asset'}
OUT = HERE / 'Working/protagonist_v02/refined'
OUT_BLEND = OUT / 'IR_Protagonist_v02_refined.blend'

ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ARGS += os.environ.get('IR_V02_ARGS', '').split()      # GUI Text editor: set IR_V02_ARGS=--in-place
IN_PLACE = '--in-place' in ARGS
DO_RENDERS = '--no-renders' not in ARGS
DO_VERIFY = '--no-verify' not in ARGS

# ---------------------------------------------------------------- IR-ACOPLE-01 (metres, socket frame)
STD = {
    'forearm_end_z': -0.012, 'flange_d': 0.078, 'flange_bevel': 0.0015, 'flange_back_z': -0.022,
    'spigot_d': 0.064, 'spigot_end_z': 0.003,
    'collar_d': 0.080, 'collar_len': 0.032, 'housing_d': 0.066, 'housing_depth': 0.006, 'collar_bevel': 0.0015,
    'forearm_cut_z': -0.016,     # sleeve cut, hidden inside the flange (flange spans -0.022..-0.012)
    'hand_cut_z': 0.012,         # hand surface cut, hidden inside the collar (collar spans 0..0.032)
}
ELBOW_NECK = 0.040               # body cut 40 mm above the elbow pivot along UpperArm: crease between arm box and joint
SEG_LATHE = 20
PRE_RATIO = {'hand': 0.6}   # collapse pre-pass on the whole v01 hand (shape-preserving); see surface_pre_decimation
ROTULA_COVER = 0.9
BOOL_SOLVER = 'MANIFOLD'   # guaranteed manifold output (EXACT leaves 4-face edges where v01 fingers touch)
APPLY_SURFACE_BEVEL = False   # see metrics finish: spec bevel exceeds FPS budget
ROTULA_LEN = 0.75
SEG_ROTULA = 8
ROTULA_CHAMFER = 0.0012
BEVEL = {'angle_deg': 30.0, 'width': 0.0015, 'segments': 2}
LOD_TARGET = {1: 12000, 2: 6500}
BUDGET = {'LOD0_total': 26000, 'FPS_total': 9000, 'hand_side': 3300, 'forearm_surface': 760, 'min_piece_ratio': 0.75}
FINGERS = ('Index', 'Middle', 'Ring', 'Little')
# Neutral graphite metal reused for caps/joints/collars (BaseColor lum 0.17, sat 0.013, rough 0.93),
# found by scanning 16 px blocks of the v01 atlas inside existing UV islands (see metrics.json:uv).
UV_METAL = G.UVRegion(112 / 2048, 1072 / 2048, 128 / 2048, density=832.0 / 2048.0)

LOG = []


def log(*a):
    s = ' '.join(str(x) for x in a)
    LOG.append(s)
    print('[IR-v02]', s)


# ================================================================ scene helpers
def load_source():
    if IN_PLACE:
        if 'IR_Rig' not in bpy.data.objects or 'Hand_R_LOD0' not in bpy.data.objects:
            raise RuntimeError('--in-place requires the v01 rigged source to be the open file')
        log('in-place: using open session', bpy.data.filepath)
    else:
        bpy.ops.wm.open_mainfile(filepath=str(SRC), load_ui=False)
        log('opened', SRC)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')


def temp_collection(name='IR_v02_tmp'):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    return col


def new_object(name, me, col=None):
    o = bpy.data.objects.new(name, me)
    (col or bpy.context.scene.collection).objects.link(o)
    return o


def remove_object(o):
    me = o.data if o.type == 'MESH' else None
    bpy.data.objects.remove(o, do_unlink=True)
    if me is not None and me.users == 0:
        bpy.data.meshes.remove(me)


def sha(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


# ================================================================ rig data
def bone_world(arm, name):
    b = arm.data.bones[name]
    m = arm.matrix_world @ b.matrix_local
    return {'head': (arm.matrix_world @ b.head_local), 'tail': (arm.matrix_world @ b.tail_local),
            'x': m.col[0].xyz.normalized(), 'y': m.col[1].xyz.normalized(), 'z': m.col[2].xyz.normalized(),
            'parent': b.parent.name if b.parent else None}


def socket_frames(arm):
    """IR-ACOPLE-01 frames: origin Hand_X head, +Z forearm axis distal, +Y dorsum. L = exact mirror of R."""
    fo = bone_world(arm, 'Forearm_R'); ha = bone_world(arm, 'Hand_R')
    z = (fo['tail'] - fo['head']).normalized()
    dorsal = -ha['x']                     # palm normal of Hand_R is bone +X (pose library palmNormal)
    wrist_R = G.orthonormal_frame(ha['head'], z, dorsal)
    elbow_R = G.orthonormal_frame(fo['head'], z, dorsal)
    return {'Wrist_R': wrist_R, 'Elbow_R': elbow_R,
            'Wrist_L': G.mirror_frame(wrist_R), 'Elbow_L': G.mirror_frame(elbow_R)}


def parse_pose_library(path):
    txt = Path(path).read_text(encoding='utf-8')
    bones = {}
    for block in re.split(r'\n  - name: ', txt)[1:]:
        name = block.split('\n', 1)[0].strip()
        d = {}
        for key in ('bind', 'open', 'relaxed', 'fist', 'grip', 'inspect'):
            m = re.search(key + r': \{x: ([-\deE.]+), y: ([-\deE.]+), z: ([-\deE.]+), w: ([-\deE.]+)\}', block)
            if m:
                x, y, z, w = (float(v) for v in m.groups())
                d[key] = Quaternion((w, x, y, z)).normalized()
        bones[name] = d
    return bones


def unity_delta_to_blender(bind, pose):
    """Unity local rotations are absolute (bind = rest). Delta in bone frame, RH<-LH by negating x."""
    d = bind.inverted() @ pose
    return Quaternion((d.w, d.x, -d.y, -d.z)).normalized()


def refit_thumb_bones(arm, hand_bm_world, report):
    """v01 thumb chain sits ~30 mm off the modelled thumb (audit: Thumb1 never dominant). Translate each thumb
    bone (orientation and length preserved, so pose-library rotations stay valid) onto the thumb tube axis."""
    pts = [v.co.copy() for v in hand_bm_world.verts]
    tube = [p for p in pts if p.x > -0.405 and p.y < -0.018 and p.z < 0.947]
    offs = {}
    data = {n: bone_world(arm, n + '_R') for n in ('Thumb1', 'Thumb2', 'Thumb3')}

    def section_centroid(p0, n, near, guess):
        bm = hand_bm_world.copy()
        cut = G.bisect(bm, p0, n)
        vs = {v for e in cut for v in e.verts if (v.co - guess).length < near}
        c = sum((v.co for v in vs), Vector()) / max(1, len(vs))
        bm.free()
        return c, len(vs)

    # Thumb1: root of the free thumb tube (top 3 mm of the separated tube)
    top = sorted(tube, key=lambda p: -p.z)
    root = [p for p in top if p.z > top[0].z - 0.003]
    guess = sum(root, Vector()) / len(root)
    c1, n1 = section_centroid(guess, data['Thumb1']['y'], 0.022, guess)
    new_heads = {'Thumb1': c1}
    for name in ('Thumb2', 'Thumb3'):
        b = data[name]
        g = b['head'].copy()
        # project the old head onto the tube: nearest tube points to the old cut plane
        band = [p for p in tube if abs((p - b['head']).dot(b['y'])) < 0.002]
        g = sum(band, Vector()) / len(band)
        c, n = section_centroid(b['head'], b['y'], 0.020, g)
        new_heads[name] = c
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    inv = arm.matrix_world.inverted()
    for name, c in new_heads.items():
        for side, sgn in (('R', 1), ('L', -1)):
            eb = arm.data.edit_bones[name + '_' + side]
            target = c.copy()
            if side == 'L':
                target.x = -target.x
            target = inv @ target
            off = target - eb.head
            roll = eb.roll
            eb.head = eb.head + off
            eb.tail = eb.tail + off
            eb.roll = roll
            eb.use_connect = False
            offs[name + '_' + side] = [round(x, 5) for x in off]
    bpy.ops.object.mode_set(mode='OBJECT')
    report['thumb_refit_offsets_m'] = offs
    log('thumb refit', offs)


# ================================================================ separators between fingers
def fit_separator(lo_pts, hi_pts):
    """Plane n.p = c with n ~ +Y (tilted in X/Z) maximising the robust margin between two vertex clouds."""
    best = None
    def q(vals, f):
        s = sorted(vals); return s[min(len(s) - 1, max(0, int(f * (len(s) - 1))))]
    for ia in range(-30, 31, 2):
        for ib in range(-30, 31, 2):
            n = Vector((ia / 100.0, 1.0, ib / 100.0)).normalized()
            a = [n.dot(p) for p in lo_pts]; b = [n.dot(p) for p in hi_pts]
            margin = q(b, 0.01) - q(a, 0.99)
            key = (round(margin, 7), -abs(ia) - abs(ib))
            if best is None or key > best[0]:
                best = (key, n, (q(b, 0.01) + q(a, 0.99)) * 0.5, margin)
    _, n, c, margin = best
    return n, c, margin


# ================================================================ forearm + elbow
def find_cap_faces(bm, center, min_verts=30, radius=0.09):
    return [f for f in bm.faces if len(f.verts) >= min_verts and (f.calc_center_median() - center).length < radius]


def elbow_split(body_bm, side, arm, report, tag):
    """Cut the body above the elbow joint (crease under the upper-arm box) and return the joint chunk bmesh
    (joint ball + v01 elbow cap) for merging into the forearm. Body hole is capped (beauty triangulation)."""
    fo = bone_world(arm, 'Forearm_' + side); ua = bone_world(arm, 'UpperArm_' + side)
    E = fo['head']; n = (ua['tail'] - ua['head']).normalized()
    p = E - n * ELBOW_NECK
    seeds = find_cap_faces(body_bm, E)
    if not seeds:
        raise RuntimeError('elbow cap not found on body ' + tag)
    chunk, cut = G.local_split(body_bm, p, n, E, 0.14, seeds)
    if len(chunk) > 3000:
        raise RuntimeError('elbow chunk leaked into the body ' + tag)
    chunk_bm = bmesh.new(); G.ensure_layers(chunk_bm)
    body_bm.faces.index_update()
    idx = {f.index for f in chunk}
    tmp = body_bm.copy()
    tmp.faces.index_update()
    bmesh.ops.delete(tmp, geom=[f for f in tmp.faces if f.index not in idx], context='FACES')
    G.ensure_layers(tmp)
    G.append_bm(chunk_bm, tmp)
    tmp.free()
    bmesh.ops.remove_doubles(chunk_bm, verts=chunk_bm.verts[:], dist=1e-7)
    # remove from body and cap the body
    bmesh.ops.delete(body_bm, geom=list(chunk), context='FACES')
    border = [e for e in body_bm.edges if e.is_boundary]
    caps = G.fill_planar_holes(body_bm, border, n, UV_METAL)
    report.setdefault('elbow', {})[tag] = {'plane_point': [round(x, 5) for x in p], 'plane_normal': [round(x, 5) for x in n],
                                          'chunk_faces': len(chunk), 'body_cap_tris': len(caps)}
    return chunk_bm, p, n


def merge_chunk_into_forearm(fore_bm, chunk_bm, E, p, n, tag, report):
    """Delete the coincident v01 elbow caps (concave 60-70-gons) on both sides and weld the shared ring."""
    caps_f = find_cap_faces(fore_bm, E)
    caps_c = find_cap_faces(chunk_bm, E)
    cap_sizes = [len(f.verts) for f in caps_f] + [len(f.verts) for f in caps_c]
    bmesh.ops.delete(fore_bm, geom=caps_f, context='FACES_ONLY')
    bmesh.ops.delete(chunk_bm, geom=caps_c, context='FACES_ONLY')
    # cap the chunk at the new neck plane (faces the upper arm)
    border = [e for e in chunk_bm.edges if e.is_boundary and abs(G.plane_side(e.verts[0].co, p, n)) < 1e-5
              and abs(G.plane_side(e.verts[1].co, p, n)) < 1e-5]
    neck = G.fill_planar_holes(chunk_bm, border, -n, UV_METAL)
    G.append_bm(fore_bm, chunk_bm)
    ring = [v for v in fore_bm.verts if any(e.is_boundary for e in v.link_edges)]
    bmesh.ops.remove_doubles(fore_bm, verts=ring, dist=2e-5)
    bmesh.ops.dissolve_degenerate(fore_bm, dist=1e-7, edges=fore_bm.edges[:])
    left = sum(1 for e in fore_bm.edges if e.is_boundary)
    report.setdefault('elbow', {}).setdefault(tag, {}).update(
        {'removed_v01_caps': cap_sizes,
         'neck_cap_tris': len(neck), 'boundary_edges_after_weld': left})
    if left:
        # close any residual opening (should be 0)
        G.fill_planar_holes(fore_bm, [e for e in fore_bm.edges if e.is_boundary], n, UV_METAL)


def build_forearm(arm, side, frames, fore_obj, chunk_bm, p, n, report):
    S = frames['Wrist_' + side]
    bm = G.bm_from_object(fore_obj)
    E = bone_world(arm, 'Forearm_' + side)['head']
    merge_chunk_into_forearm(bm, chunk_bm, E, p, n, fore_obj.name, report)
    z = S.col[2].xyz
    cut_p = S @ Vector((0, 0, STD['forearm_cut_z']))
    G.cut_keep(bm, cut_p, z, keep_positive=False, region=UV_METAL, piece=100)
    t0 = G.tri_count_bm(bm)
    ratio = min(1.0, BUDGET['forearm_surface'] / float(t0))
    bm = decimate_bm(bm, ratio)
    report.setdefault('forearm_lod0_budget', {})[fore_obj.name] = {'ratio': round(ratio, 4), 'tris_before': t0,
                                                                   'tris_after': G.tri_count_bm(bm)}
    pc = bm.faces.layers.int['ir_piece']
    for f in bm.faces:
        f[pc] = 100
    r_f = STD['flange_d'] / 2; r_s = STD['spigot_d'] / 2; bv = STD['flange_bevel']
    zb, ze, zs = STD['flange_back_z'], STD['forearm_end_z'], STD['spigot_end_z']
    prof = [(0.0, zb), (r_f - bv, zb), (r_f, zb + bv), (r_f, ze - bv), (r_f - bv, ze), (r_s, ze),
            (r_s, zs - 0.0008), (r_s - 0.0008, zs), (0.0, zs)]
    G.lathe(bm, S, prof, SEG_LATHE, UV_METAL, piece=101)
    return bm


def decimate_bm(bm, ratio):
    col = temp_collection()
    me = G.bm_to_mesh(bm, 'IR_tmp_dec'); bm.free()
    o = new_object('IR_tmp_dec', me, col)
    if ratio < 0.999:
        m = o.modifiers.new('dec', 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = ratio
        m.use_collapse_triangulate = True
    dg = bpy.context.evaluated_depsgraph_get(); dg.update()
    me2 = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    remove_object(o)
    out = bmesh.new(); out.from_mesh(me2); G.ensure_layers(out); bpy.data.meshes.remove(me2)
    return out


# ================================================================ hand partition
def hand_labels(obj):
    names = [g.name for g in obj.vertex_groups]
    out = []
    for v in obj.data.vertices:
        if not v.groups:
            out.append((obj.matrix_world @ v.co, '')); continue
        g = max(v.groups, key=lambda x: x.weight)
        out.append((obj.matrix_world @ v.co, names[g.group]))
    return out


def decimated_copy_bm(obj, ratio, report_key, report):
    """Budget pre-pass: collapse-decimate a copy of a v01 surface (UV interpolated) and measure the deviation."""
    from mathutils.bvhtree import BVHTree
    col = temp_collection()
    src = obj.data.copy()
    o = new_object('IR_tmp_pre', src, col)
    o.matrix_world = obj.matrix_world.copy()
    o.modifiers.clear()
    if ratio < 0.999:
        m = o.modifiers.new('pre', 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = ratio
        m.use_collapse_triangulate = True
    dg = bpy.context.evaluated_depsgraph_get(); dg.update()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    remove_object(o)
    bm = bmesh.new(); bm.from_mesh(me); bm.transform(obj.matrix_world); G.ensure_layers(bm)
    bpy.data.meshes.remove(me)
    ref = G.bm_from_object(obj)
    t_ref = BVHTree.FromBMesh(ref); t_new = BVHTree.FromBMesh(bm)
    d1 = [t_ref.find_nearest(v.co)[3] for v in bm.verts]
    d2 = [t_new.find_nearest(v.co)[3] for v in ref.verts]
    report.setdefault('surface_pre_decimation', {})[report_key] = {
        'ratio': ratio, 'tris_before': G.tri_count_bm(ref), 'tris_after': G.tri_count_bm(bm),
        'hausdorff_mm': round(max(max(d1), max(d2)) * 1000, 3),
        'mean_dev_mm': round((sum(d1) + sum(d2)) / (len(d1) + len(d2)) * 1000, 3)}
    ref.free()
    return bm


def cut_pieces(src_obj, specs, center):
    """specs: [(name, bone, halfspaces)] -> [(name, bone, halfspaces, Mesh)] via EXACT intersect with convex cells."""
    col = temp_collection()
    out = []
    for name, bone, hs in specs:
        cbm = G.convex_cell_bm(hs, center, 0.25)
        G.uv_box_faces(cbm, cbm.faces[:], UV_METAL, center)
        co = new_object('IR_cell_' + name, G.bm_to_mesh(cbm, 'IR_cell_' + name), col)
        cbm.free()
        me = G.boolean_eval(src_obj, co, 'INTERSECT', solver=BOOL_SOLVER)
        remove_object(co)
        out.append((name, bone, hs, me))
    return out


def nearest_finger(c, B):
    def seg_dist(p, a, b):
        d = b - a
        t = max(0.0, min(1.0, (p - a).dot(d) / d.length_squared))
        return (p - (a + d * t)).length
    return min(FINGERS, key=lambda F: min(seg_dist(c, B[F + str(i)]['head'], B[F + str(i)]['tail']) for i in (1, 2, 3)))


def extract_faces(bm, faces):
    bm.faces.index_update()
    keep = {f.index for f in faces}
    out = bm.copy(); G.ensure_layers(out)
    out.faces.index_update()
    bmesh.ops.delete(out, geom=[f for f in out.faces if f.index not in keep], context='FACES')
    return out


def build_hand_R(arm, frames, hand_obj, report, pre_ratio):
    S = frames['Wrist_R']
    labels = hand_labels(hand_obj)
    bm = decimated_copy_bm(hand_obj, pre_ratio, 'Hand_R_LOD0', report)
    z = S.col[2].xyz
    G.cut_keep(bm, S @ Vector((0, 0, STD['hand_cut_z'])), z, keep_positive=True, region=UV_METAL, piece=0)
    comps = G.components(bm.faces[:])
    if len(comps) > 1:  # drop wrist leftovers that are not the hand (none expected)
        keep = max(comps, key=len)
        bmesh.ops.delete(bm, geom=[f for c in comps if c is not keep for f in c], context='FACES')
    report['hand_after_wrist_cut_tris'] = G.tri_count_bm(bm)

    B = {n: bone_world(arm, n + '_R') for n in
         [f + str(i) for f in FINGERS for i in (1, 2, 3)] + ['Thumb1', 'Thumb2', 'Thumb3', 'Hand']}

    def cloud(f):
        return [p for p, lab in labels if lab.startswith(f) and lab.endswith('_R')
                and (p - B[f + '1']['head']).dot(B[f + '1']['y']) > 0.002]
    seps = {}
    for a, b in (('Index', 'Middle'), ('Middle', 'Ring'), ('Ring', 'Little')):
        n, c, margin = fit_separator(cloud(a), cloud(b))
        seps[a + '|' + b] = (n * c, n)
        report.setdefault('separators', {})[a + '|' + b] = {'normal': [round(x, 4) for x in n], 'offset': round(c, 5),
                                                            'margin_mm': round(margin * 1000, 2)}
    XT = -0.405   # thumb box: x > XT (thumb tube x >= -0.399; index/middle x <= -0.414)
    YT = -0.018   # thumb box: y < YT (thumb y <= -0.027; ring y >= -0.011)
    center = B['Hand']['head']
    # MCP: the four Finger1 heads share x/z in v01, so one plane through all heads (normal = mean bone
    # direction; deviation from each bone perpendicular is reported) gives one clean palm/finger cut.
    heads = [B[F + '1']['head'] for F in FINGERS]
    mcp_p = sum(heads, Vector()) / 4
    mcp_n = sum((B[F + '1']['y'] for F in FINGERS), Vector()).normalized()
    report['mcp_plane'] = {'point': [round(x, 5) for x in mcp_p], 'normal': [round(x, 4) for x in mcp_n],
                           'max_head_offset_mm': round(max(abs((h - mcp_p).dot(mcp_n)) for h in heads) * 1000, 3),
                           'max_angle_to_bone_deg': round(max(math.degrees(mcp_n.angle(B[F + '1']['y']))
                                                              for F in FINGERS), 2)}
    T1, T2, T3 = B['Thumb1'], B['Thumb2'], B['Thumb3']
    t_box = [(T1['head'], T1['y']), (Vector((XT, 0, 0)), Vector((1, 0, 0))), (Vector((0, YT, 0)), Vector((0, -1, 0)))]
    specs_thumb = [('Thumb1_R', 'Thumb1_R', t_box + [(T2['head'], -T2['y'])]),
                   ('Thumb2_R', 'Thumb2_R', t_box + [(T2['head'], T2['y']), (T3['head'], -T3['y'])]),
                   ('Thumb3_R', 'Thumb3_R', t_box + [(T2['head'], T2['y']), (T3['head'], T3['y'])])]
    prox_planes = {'Thumb1_R': t_box[0], 'Thumb2_R': (T2['head'], T2['y']), 'Thumb3_R': (T3['head'], T3['y'])}
    col = temp_collection()
    hand_obj_tmp = new_object('IR_tmp_hand', G.bm_to_mesh(bm, 'IR_tmp_hand'), col)
    bm.free()
    thumb_pieces = cut_pieces(hand_obj_tmp, specs_thumb, center)
    # remove the whole thumb box once, then palm / fingers from the rest
    tb = G.convex_cell_bm(t_box, center, 0.25); G.uv_box_faces(tb, tb.faces[:], UV_METAL, center)
    tbo = new_object('IR_cell_thumbbox', G.bm_to_mesh(tb, 'IR_cell_thumbbox'), col); tb.free()
    rest_me = G.boolean_eval(hand_obj_tmp, tbo, 'DIFFERENCE', solver=BOOL_SOLVER)
    remove_object(tbo)
    rest = new_object('IR_tmp_rest', rest_me, col)
    specs = [('Palm_R', 'Hand_R', [(mcp_p, -mcp_n)])]
    clips = {'Thumb': t_box[1:]}
    cut_planes = [(mcp_p, mcp_n)] + list(t_box) + [(p0, n0) for p0, n0 in seps.values()] + \
        [(B[n]['head'], B[n]['y']) for n in ('Thumb2', 'Thumb3')] + \
        [(S @ Vector((0, 0, STD['hand_cut_z'])), z)]
    for fi, F in enumerate(FINGERS):
        slab = [(mcp_p, mcp_n)]
        if fi > 0:
            p0, n0 = seps[FINGERS[fi - 1] + '|' + F]; slab.append((p0, n0))
        if fi < 3:
            p0, n0 = seps[F + '|' + FINGERS[fi + 1]]; slab.append((p0, -n0))
        clips[F] = slab[1:]
        cut_planes += [(B[F + '2']['head'], B[F + '2']['y']), (B[F + '3']['head'], B[F + '3']['y'])]
        b2, b3 = B[F + '2'], B[F + '3']
        specs += [(F + '1_R', F + '1_R', slab + [(b2['head'], -b2['y'])]),
                  (F + '2_R', F + '2_R', slab + [(b2['head'], b2['y']), (b3['head'], -b3['y'])]),
                  (F + '3_R', F + '3_R', slab + [(b2['head'], b2['y']), (b3['head'], b3['y'])])]
        prox_planes[F + '1_R'] = (mcp_p, mcp_n)
        prox_planes[F + '2_R'] = (b2['head'], b2['y'])
        prox_planes[F + '3_R'] = (b3['head'], b3['y'])
    pieces = cut_pieces(rest, specs, center) + thumb_pieces
    remove_object(rest)

    out = bmesh.new(); G.ensure_layers(out)
    table = []
    dropped = []
    for pid, (name, bone, hs, me) in enumerate(pieces):
        pbm = bmesh.new(); pbm.from_mesh(me); G.ensure_layers(pbm)
        bpy.data.meshes.remove(me)
        kd = pbm.faces.layers.int['ir_kind']
        planes = [(Vector(p), Vector(n).normalized()) for p, n in hs]
        if name == 'Palm_R':
            planes += [(Vector(p), Vector(n).normalized()) for p, n in t_box]
        for f in pbm.faces:
            f[kd] = 0
            for (p, n) in planes:
                if all(abs((v.co - p).dot(n)) < 2e-6 for v in f.verts):
                    f[kd] = 1
                    break
        G.triangulate_ngons(pbm)
        pbm.faces.ensure_lookup_table()
        caps = [f for f in pbm.faces if f[kd] == 1]
        for f in caps:
            f.smooth = False
        G.uv_planar_faces(pbm, caps, UV_METAL)
        own = name[:-3] if (name[-3] in '123' and not name.startswith('Thumb')) else None
        cl = sorted(G.components(pbm.faces[:]), key=len, reverse=True)
        for c in cl[1:]:
            cc = sum((f.calc_center_median() for f in c), Vector()) / len(c)
            tgt = nearest_finger(cc, B)
            for key, (sp, sn) in seps.items():   # a sliver carries a cap on the separator it was cut by
                if own in key.split('|') and any(f[kd] == 1 and all(abs((v.co - sp).dot(sn)) < 2e-6 for v in f.verts)
                                                 for f in c):
                    tgt = [x for x in key.split('|') if x != own][0]
            if own and tgt != own:
                thick = 0.0
                for key, (sp, sn) in seps.items():
                    if own in key.split('|') and tgt in key.split('|'):
                        thick = max(abs((v.co - sp).dot(sn)) for f in c for v in f.verts)
                dropped.append({'piece': name, 'belongs_to': tgt, 'faces': len(c),
                                'max_thickness_across_separator_mm': round(thick * 1000, 2)})
                bmesh.ops.delete(pbm, geom=list(c), context='FACES')
        comps = [len(c) for c in G.components(pbm.faces[:])]
        entry = {'id': pid, 'name': name, 'bone': bone, 'tris': G.tri_count_bm(pbm), 'components': comps,
                 'cap_tris': sum(len(f.verts) - 2 for f in pbm.faces if f[kd] == 1)}
        if name in prox_planes:
            pp, pn = prox_planes[name]
            entry['prox_plane'] = (Vector(pp), Vector(pn).normalized())
        table.append(entry)
        G.append_bm(out, pbm, piece=pid)
        pbm.free()
    # thin slivers of a touching neighbour finger cut by a planar separator are dropped: the neighbour keeps a
    # flat cap on the separator, so the only change is a <= few mm shave where the two v01 fingers touched
    report['separator_slivers_dropped'] = dropped
    report['hand_partition'] = [{k: v for k, v in t.items() if k != 'prox_plane'} for t in table]
    log('hand partition', [(t['name'], t['tris'], t['components']) for t in table])
    cut_planes = [(Vector(p), Vector(n).normalized()) for p, n in cut_planes]
    return out, table, B, hand_obj_tmp, clips, cut_planes


def cap_section(bm, table, bname, B, axes):
    pc = bm.faces.layers.int['ir_piece']; kd = bm.faces.layers.int['ir_kind']
    t = {x['name']: x for x in table}[bname]
    pp, pn = t['prox_plane']
    b = B[bname[:-2]]
    a = axes[bname]
    w = b['y'].cross(a).normalized()
    vs = {v for f in bm.faces if f[pc] == t['id'] and f[kd] == 1 for v in f.verts if abs((v.co - pp).dot(pn)) < 2e-6}
    return vs, a, w, b


def recentre_joints(arm, bm, table, B, axes, report):
    """v01 finger heads sit 4-7 mm under the dorsal surface, so extending from the curled bind (Open) opens the
    palmar side by up to 25 mm. Each finger/thumb bone is translated along w = bone_y x flex_axis (inside its own
    cut plane, so the cuts do not change) to the middle of the cut section. Orientation and length are kept,
    therefore the HandPoseLibrary rotations remain valid; only bone local positions change."""
    offs = {}
    for F in FINGERS + ('Thumb',):
        for s_ in (1, 2, 3):
            bname = '%s%d_R' % (F, s_)
            vs, a, w, b = cap_section(bm, table, bname, B, axes)
            if len(vs) < 3:
                continue
            wl = [(v.co - b['head']).dot(w) for v in vs]
            offs[bname] = w * ((min(wl) + max(wl)) * 0.5)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    inv = arm.matrix_world.inverted().to_3x3()
    rep = {}
    for bname, off in sorted(offs.items()):
        for side in ('R', 'L'):
            o = off.copy()
            if side == 'L':
                o.x = -o.x
            eb = arm.data.edit_bones[bname[:-1] + side]
            roll = eb.roll
            lo = inv @ o
            eb.head = eb.head + lo; eb.tail = eb.tail + lo; eb.roll = roll; eb.use_connect = False
        rep[bname] = round(off.length * 1000, 2)
    bpy.ops.object.mode_set(mode='OBJECT')
    report['joint_recentre_mm'] = rep
    log('joint recentre (mm)', rep)


def add_rotulas(bm, table, B, axes, report):
    """Mechanical joint drum at each finger/thumb joint: axis = flex axis through the bone head, sized from the
    proximal cap of the distal piece so that it covers the dorsal opening between 0 and 100 deg of flexion."""
    pc = bm.faces.layers.int['ir_piece']; kd = bm.faces.layers.int['ir_kind']
    by_name = {t['name']: t for t in table}
    res = []
    next_id = max(t['id'] for t in table) + 1
    for F in FINGERS + ('Thumb',):
        for s in (1, 2, 3):
            bname = F + str(s) + '_R'
            b = B[F + str(s)]
            h, d = b['head'], b['y']
            a = axes[bname]
            w = d.cross(a).normalized()          # + = palm side for the flexion sign used by the poses
            t = by_name[bname]
            pp, pn = t['prox_plane']
            vs = {v for f in bm.faces if f[pc] == t['id'] and f[kd] == 1 for v in f.verts
                  if abs((v.co - pp).dot(pn)) < 2e-6}
            if len(vs) < 3:
                log('WARN rotula without cap section', bname)
                continue
            al = [(v.co - h).dot(a) for v in vs]; wl = [(v.co - h).dot(w) for v in vs]
            amin, amax = min(al), max(al)
            dorsal = max(-x for x in wl); palmar = max(wl)
            # thickness of the distal segment just past the joint (the MCP section is the whole palm block)
            sec = bmesh.new()
            G.ensure_layers(sec)
            G.append_bm(sec, extract_faces(bm, [f for f in bm.faces if f[pc] == t['id']]))
            cut = G.bisect(sec, h + d * 0.004, d)
            near = [(v.co - h).dot(w) for e in cut for v in e.verts]
            sec.free()
            t_child = (max(near) - min(near)) * 0.5 if len(near) > 3 else min(dorsal, palmar)
            half = min(dorsal, palmar, t_child)
            # joint axis is centred (recentre_joints): drum vertices at ROTULA_COVER x half thickness keep it inside
            # the v01 silhouette at rest; on flexion only a shallow groove opens over the drum (no through hole)
            segs = SEG_ROTULA
            radius = ROTULA_COVER * half
            length = ROTULA_LEN * (amax - amin)
            c = h + a * ((amin + amax) * 0.5)
            nf = G.bevelled_cylinder(bm, c, a, radius, length, segs, ROTULA_CHAMFER, UV_METAL, next_id)
            entry = {'id': next_id, 'name': 'Rotula_' + bname, 'bone': b['parent'], 'joint_bone': bname,
                     'radius_mm': round(radius * 1000, 2), 'length_mm': round(length * 1000, 2),
                     'dorsal_extent_mm': round(dorsal * 1000, 2), 'palmar_extent_mm': round(palmar * 1000, 2),
                     'axis': [round(x, 4) for x in a], 'center': [round(x, 5) for x in c]}
            table.append(entry); res.append(entry)
            next_id += 1
    report['rotulas'] = res


def add_collar(bm, S, table):
    r_o = STD['collar_d'] / 2; r_h = STD['housing_d'] / 2; L = STD['collar_len']; dz = STD['housing_depth']
    bv = STD['collar_bevel']
    prof = [(0.0, dz), (r_h, dz), (r_h, 0.0), (r_o - bv, 0.0), (r_o, bv), (r_o, L - bv), (r_o - bv, L), (0.0, L)]
    pid = max(t['id'] for t in table) + 1
    G.lathe(bm, S, prof, SEG_LATHE, UV_METAL, pid)
    table.append({'id': pid, 'name': 'Collar_R', 'bone': 'Hand_R'})


def flex_axes(B, lib_R):
    axes = {}
    for F in FINGERS + ('Thumb',):
        for s in (1, 2, 3):
            n = F + str(s) + '_R'
            b = B[F + str(s)]
            ax = b['z']
            if F == 'Thumb' and n in lib_R:
                q = unity_delta_to_blender(lib_R[n]['bind'], lib_R[n]['fist'])
                if q.angle > math.radians(5):
                    loc = q.axis
                    ax = (b['x'] * loc.x + b['y'] * loc.y + b['z'] * loc.z)
                    # a joint axis must be ~perpendicular to the bone: drop the twist component
                    ax = (ax - b['y'] * ax.dot(b['y'])).normalized()
            axes[n] = ax.normalized()
    return axes


# ================================================================ main build
def tri_report(objs):
    return {o.name: G.tri_count_mesh(o.data) for o in objs}


def build(report):
    import refine_protagonist_v02_build as BLD
    importlib.reload(BLD)
    arm = bpy.data.objects['IR_Rig']
    col_main = bpy.data.objects['Hand_R_LOD0'].users_collection[0]
    tmp = temp_collection()
    all_bones = [b.name for b in arm.data.bones]
    mats = {k: bpy.data.objects['Hand_R_' + k].data.materials[0] for k in ('LOD0', 'LOD1', 'LOD2', 'FPS')}
    before = tri_report([o for o in bpy.data.objects if o.type == 'MESH'])
    import refine_protagonist_v02_verify as VER0
    importlib.reload(VER0)
    v01_arm = sorted([o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(('Hand_', 'Forearm_'))],
                     key=lambda o: o.name)
    report['before'] = {'tris': before, 'weights': VER0.weights_metrics(v01_arm),
                        'sockets': sorted(o.name for o in bpy.data.objects if o.type == 'EMPTY'),
                        'from_audit': {'rigid_residual_fist_mm': {'Little1_R': 5.9, 'Hand_R': 5.7},
                                       'wrist_hand_intrusion_into_sleeve_mm': 15.6,
                                       'forearm_deformation_stress_pose_mm': 6.1,
                                       'mixed_forearm_hand_vertices': 38, 'concave_cap_ngons': '48-70 sides',
                                       'source': 'Tools/IronRequiem/Reports/ProtagonistV02/Audit/audit.md'}}

    lib = {s: parse_pose_library(POSE_LIB[s]) for s in ('R', 'L')}
    hand_src = bpy.data.objects['Hand_R_LOD0']
    hb = G.bm_from_object(hand_src)
    refit_thumb_bones(arm, hb, report)
    hb.free()
    frames = socket_frames(arm)
    report['frames'] = {k: {'origin': [round(x, 5) for x in m.translation], 'x': [round(x, 4) for x in m.col[0].xyz],
                            'y': [round(x, 4) for x in m.col[1].xyz], 'z': [round(x, 4) for x in m.col[2].xyz]}
                        for k, m in frames.items()}

    # ---- elbows: body LOD0 chunk goes to the forearm, LOD1/2 chunks are discarded (forearm LODs come from LOD0)
    body = {k: bpy.data.objects['Body_' + k] for k in ('LOD0', 'LOD1', 'LOD2')}
    body_bm = {k: G.bm_from_object(o) for k, o in body.items()}
    chunks = {}
    for side in ('R', 'L'):
        for k in ('LOD0', 'LOD1', 'LOD2'):
            ch, p, n = elbow_split(body_bm[k], side, arm, report, 'Body_%s_%s' % (k, side))
            if k == 'LOD0':
                chunks[side] = (ch, p, n)
            else:
                ch.free()
    for k, o in body.items():
        bm = body_bm[k]
        G.triangulate_ngons(bm)
        bm.transform(o.matrix_world.inverted())
        bm.to_mesh(o.data); o.data.update(); bm.free()
        # Body keeps its organic skin weights; joint chunk left it, so no Forearm influence remains in Body
        names = {g.name: g.index for g in o.vertex_groups}
        for side in ('R', 'L'):
            gf = o.vertex_groups['Forearm_' + side]; gu = o.vertex_groups['UpperArm_' + side]
            for v in o.data.vertices:
                w = next((g.weight for g in v.groups if g.group == gf.index), 0.0)
                if w > 0:
                    wu = next((g.weight for g in v.groups if g.group == gu.index), 0.0)
                    gu.add([v.index], wu + w, 'REPLACE'); gf.remove([v.index])

    # ---- right hand pieces (LOD0 surface), rotulas, collar
    hand_bm, table, B, H_obj, clips, cut_planes = build_hand_R(arm, frames, hand_src, report, PRE_RATIO['hand'])
    axes = flex_axes(B, lib['R'])
    recentre_joints(arm, hand_bm, table, B, axes, report)
    B = {n: bone_world(arm, n + '_R') for n in B}
    axes = flex_axes(B, lib['R'])
    remove_object(H_obj)
    add_rotulas(hand_bm, table, B, axes, report)
    add_collar(hand_bm, frames['Wrist_R'], table)
    # LOD0 budget: per-piece collapse of the cut surface pieces; rotulas and collar are kept as modelled
    fixed_ids = tuple(t['id'] for t in table if t['name'].startswith(('Rotula_', 'Collar_')))
    G.triangulate_ngons(hand_bm)
    raw = G.bm_to_mesh(hand_bm, 'IR_tmp_hand_raw'); hand_bm.free()
    report['hand_R_lod0_before_budget_tris'] = G.tri_count_mesh(raw)
    hand_bm, ratio, tris = BLD.decimate_to_budget(raw, BUDGET['hand_side'], temp_collection(), keep_ids=fixed_ids,
                                                  protect=False, min_ratio=BUDGET['min_piece_ratio'])
    bpy.data.meshes.remove(raw)
    report['hand_R_lod0_budget'] = {'target': BUDGET['hand_side'], 'ratio_surface_pieces': round(ratio, 4), 'tris': tris}
    table_R = table
    table_L = [dict(t, name=BLD.rename_side(t['name']), bone=BLD.rename_side(t['bone'])) for t in table]
    hand_bm_L = BLD.mirror_bm(hand_bm)

    # ---- forearms
    fore_bm = {}
    for side in ('R', 'L'):
        ch, p, n = chunks[side]
        fore_bm[side] = build_forearm(arm, side, frames, bpy.data.objects['Forearm_%s_LOD0' % side], ch, p, n, report)
        ch.free()
    fore_table = {s: [{'id': 100, 'name': 'Forearm_' + s, 'bone': 'Forearm_' + s},
                      {'id': 101, 'name': 'FlangeSpigot_' + s, 'bone': 'Forearm_' + s}] for s in ('R', 'L')}

    # ---- remove v01 hand/forearm objects (all LODs + FPS) and build the new ones
    old = {}
    for s in ('R', 'L'):
        for part in ('Hand', 'Forearm'):
            for k in ('LOD0', 'LOD1', 'LOD2', 'FPS'):
                o = bpy.data.objects['%s_%s_%s' % (part, s, k)]
                old[o.name] = o.matrix_world.copy()
                remove_object(o)
    new_objs = {}
    lod0_src = {}
    bevel_cost = {}
    for s in ('R', 'L'):
        for part, bm, tab in (('Hand', hand_bm if s == 'R' else hand_bm_L, table_R if s == 'R' else table_L),
                              ('Forearm', fore_bm[s], fore_table[s])):
            name = '%s_%s_LOD0' % (part, s)
            G.triangulate_ngons(bm)
            lod0_src[name] = (G.bm_to_mesh(bm, name + '_raw'), tab)
            if s == 'R':   # cost of the literal finishing spec (bevel by angle on the whole surface)
                tst = BLD.finish_mesh(name + '_bevtest', bm.copy(), mats['LOD0'], tmp, bevel=True)
                bevel_cost[name] = {'without_bevel': G.tri_count_bm(bm), 'with_spec_bevel': G.tri_count_mesh(tst)}
                bpy.data.meshes.remove(tst)
            me = BLD.finish_mesh(name, bm.copy(), mats['LOD0'], tmp, bevel=APPLY_SURFACE_BEVEL)
            o = new_object(name, me, col_main)
            new_objs[name] = (o, tab)
    hand_bm.free(); hand_bm_L.free()
    for b in fore_bm.values():
        b.free()
    fps_with_bevel = 2 * sum(v['with_spec_bevel'] for v in bevel_cost.values())
    report['finish'] = {'spec': BEVEL, 'surface_bevel_applied': APPLY_SURFACE_BEVEL,
                        'cost_right_side_tris': bevel_cost, 'fps_total_if_spec_bevel': fps_with_bevel,
                        'note': 'Angle-limited bevel on the v01 optimized surfaces bevels ~half of all edges (forearm 539/1107 '
                                'edges > 30 deg) and breaks the FPS budget; mechanical parts (collar, flange/spigot, '
                                'rotulas) carry modelled 1.2-1.5 mm chamfers; all meshes get Weighted Normal (keep sharp).'}

    # ---- LOD1 / LOD2 from LOD0 (per piece collapse), FPS = LOD0 copy
    report['lod'] = {}
    for k in (1, 2):
        body_t = G.tri_count_mesh(body['LOD%d' % k].data)
        budget = LOD_TARGET[k] - body_t
        srcs = [new_objs['%s_%s_LOD0' % (p, s)][0].data for s in ('R', 'L') for p in ('Hand', 'Forearm')]
        ratio, small, big = BLD.lod_ratio_for(srcs, budget)
        for it in range(3):
            total = 0; made = {}
            for s in ('R', 'L'):
                for p in ('Hand', 'Forearm'):
                    o0, tab = new_objs['%s_%s_LOD0' % (p, s)]
                    lbm = BLD.build_lod_bm(o0.data, ratio, tmp)
                    made[(p, s)] = (lbm, tab)
                    total += G.tri_count_bm(lbm)
            if abs(total - budget) / budget < 0.03 or it == 2:
                break
            for v in made.values():
                v[0].free()
            ratio = max(0.03, min(1.0, ratio * (budget - small) / max(total - small, 1)))
        report['lod']['LOD%d' % k] = {'target_total': LOD_TARGET[k], 'body_tris': body_t, 'arms_budget': budget,
                                      'ratio': round(ratio, 4), 'arms_tris': total}
        for (p, s), (lbm, tab) in made.items():
            name = '%s_%s_LOD%d' % (p, s, k)
            me = BLD.finish_mesh(name, lbm, mats['LOD%d' % k], tmp, bevel=False)
            new_objs[name] = (new_object(name, me, col_main), tab)
    for s in ('R', 'L'):
        for p in ('Hand', 'Forearm'):
            o0, tab = new_objs['%s_%s_LOD0' % (p, s)]
            me = o0.data.copy(); me.name = '%s_%s_FPS' % (p, s)
            me.materials.clear(); me.materials.append(mats['FPS'])
            new_objs[me.name] = (new_object(me.name, me, col_main), tab)
    for me, tab in lod0_src.values():
        bpy.data.meshes.remove(me)

    # ---- keep v01 object origins (wrist / elbow pivots)
    for name, (o, tab) in new_objs.items():
        if name in old:
            t = old[name].translation.copy()
            o.data.transform(Matrix.Translation(-t))
            o.matrix_world = Matrix.Translation(t)
    # ---- weights, parenting, armature
    conflicts = {}
    for name, (o, tab) in sorted(new_objs.items()):
        conflicts[name] = BLD.assign_rigid_weights(o, tab, all_bones)
        BLD.add_armature(o, arm)
    report['weight_conflict_vertices'] = conflicts
    report['sockets'] = BLD.rebuild_sockets(arm, frames, col_main)
    report['piece_tables'] = {'Hand_R': table_R, 'Hand_L': table_L, 'Forearm_R': fore_table['R'],
                              'Forearm_L': fore_table['L']}
    # ---- test poses as actions (fake user): library poses + spec fist found by a collision search
    import refine_protagonist_v02_verify as VER
    importlib.reload(VER)
    poses = {}
    for s_ in ('R', 'L'):
        lp = BLD.library_poses(lib[s_])
        for k, v in lp.items():
            poses.setdefault(k.capitalize(), {}).update(v)
    search_log = []
    report['fist_search'] = {}
    for pname, mode in (('FistTest', 'spec'), ('FistClean', 'clean')):
        qR, fist_res, chosen, t = VER.search_fist(arm, new_objs['Hand_R_LOD0'][0], table_R, lib['R'], 'R',
                                                  search_log, mode=mode)
        poses[pname] = dict(qR)
        poses[pname].update(VER.mirror_fist(lib['L'], chosen, t, 'L'))
        report['fist_search'][pname] = fist_res
    for line in search_log:
        log(line)
    report['test_actions'] = BLD.make_test_actions(arm, poses)
    for o in list(tmp.objects):
        remove_object(o)
    bpy.data.collections.remove(tmp)
    report['after'] = {'tris': tri_report([o for o in bpy.data.objects if o.type == 'MESH'])}
    tot = lambda d, suf: sum(t for n, t in d.items() if n.endswith(suf) and n.startswith(('Body_', 'Hand_', 'Forearm_')))
    report['summary_tris'] = {k: {'before': tot(report['before']['tris'], k), 'after': tot(report['after']['tris'], k)}
                              for k in ('LOD0', 'LOD1', 'LOD2', 'FPS')}
    return arm, frames, poses, lib


def save(report):
    OUT.mkdir(parents=True, exist_ok=True)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND), copy=IN_PLACE, compress=False)
    b1 = Path(str(OUT_BLEND) + '1')
    if b1.exists():
        b1.unlink()
    report['output_blend'] = str(OUT_BLEND.relative_to(ROOT))
    log('saved', OUT_BLEND)


def main():
    report = {'task': 'IR-09 A2/A3 protagonist v02 refine', 'blender': bpy.app.version_string,
              'source': str(SRC.relative_to(ROOT)), 'standard': 'IR-ACOPLE-01', 'std_values_m': STD}
    report['source_sha256_before'] = {'rigged_blend': sha(SRC), 'closed_fist_blend': sha(FIST_SRC)}
    load_source()
    arm, frames, poses, lib = build(report)
    save(report)
    if DO_VERIFY:
        import refine_protagonist_v02_verify as VER
        importlib.reload(VER)
        VER.verify(report, arm, frames, poses, lib, OUT, do_renders=DO_RENDERS, uv_region=UV_METAL, std=STD)
    report['source_sha256_after'] = {'rigged_blend': sha(SRC), 'closed_fist_blend': sha(FIST_SRC)}
    report['sources_unchanged'] = report['source_sha256_before'] == report['source_sha256_after']
    report['log'] = LOG
    (OUT / 'metrics.json').write_text(json.dumps(report, indent=1, default=str), encoding='utf-8')
    log('metrics written')


if __name__ == '__main__':
    main()
