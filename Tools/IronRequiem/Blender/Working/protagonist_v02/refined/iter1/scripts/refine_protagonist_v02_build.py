"""IR-09 helpers for refine_protagonist_v02.py: mirror, FPS finish, rigid weights, LODs, sockets, test poses."""
import bpy
import bmesh
import math
import re
from mathutils import Vector, Matrix, Quaternion

import refine_protagonist_v02_geo as G

FINGERS = ('Index', 'Middle', 'Ring', 'Little')
BEVEL = {'angle_deg': 30.0, 'width': 0.0015, 'segments': 2}


def new_object(name, me, col):
    o = bpy.data.objects.new(name, me)
    col.objects.link(o)
    return o


def remove_object(o):
    me = o.data if o.type == 'MESH' else None
    bpy.data.objects.remove(o, do_unlink=True)
    if me is not None and me.users == 0:
        bpy.data.meshes.remove(me)


# ================================================================ mirror / finish
def rename_side(s):
    return s[:-2] + '_L' if s.endswith('_R') else s


def mirror_bm(src):
    bm = src.copy()
    G.ensure_layers(bm)
    bm.transform(G.mirror_x_matrix())
    bmesh.ops.reverse_faces(bm, faces=bm.faces[:], flip_multires=False)
    return bm


def mark_sharp(bm, angle_deg):
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        e.smooth = not (e.is_manifold and e.calc_face_angle(0.0) > math.radians(angle_deg))


def finish_mesh(name, bm, material, col, bevel=True, bevel_cfg=None):
    """Hard-edge bevel (by angle) + Weighted Normal (keep sharp), both applied. Returns a new Mesh."""
    cfg = dict(BEVEL); cfg.update(bevel_cfg or {})
    mark_sharp(bm, cfg['angle_deg'])
    me = G.bm_to_mesh(bm, name + '_src')
    me.materials.append(material)
    o = new_object(name + '_tmpfinish', me, col)
    if bevel:
        bv = o.modifiers.new('IR_bevel', 'BEVEL')
        bv.limit_method = 'ANGLE'; bv.angle_limit = math.radians(cfg['angle_deg'])
        bv.width = cfg['width']; bv.segments = cfg['segments']; bv.use_clamp_overlap = True
        bv.harden_normals = False
    wn = o.modifiers.new('IR_wn', 'WEIGHTED_NORMAL')
    wn.keep_sharp = True; wn.mode = 'FACE_AREA'; wn.weight = 50
    dg = bpy.context.evaluated_depsgraph_get(); dg.update()
    out = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    remove_object(o)
    out.name = name
    return out


# ================================================================ weights
def piece_ids(me):
    a = me.attributes.get('ir_piece')
    if a is None:
        return [0] * len(me.polygons)
    return [a.data[i].value for i in range(len(me.polygons))]


def assign_rigid_weights(obj, table, all_bones):
    me = obj.data
    bone_of = {t['id']: t['bone'] for t in table}
    vb = {}
    for p, pid in zip(me.polygons, piece_ids(me)):
        for v in p.vertices:
            vb.setdefault(v, set()).add(bone_of[pid])
    conflicts = sum(1 for s in vb.values() if len(s) > 1)
    obj.vertex_groups.clear()
    groups = {n: obj.vertex_groups.new(name=n) for n in all_bones}
    per_bone = {}
    for v, s in vb.items():
        per_bone.setdefault(sorted(s)[0], []).append(v)
    for b, vs in sorted(per_bone.items()):
        groups[b].add(sorted(vs), 1.0, 'REPLACE')
    return conflicts


def add_armature(obj, arm):
    m = obj.modifiers.new('SharedSkeleton', 'ARMATURE')
    m.object = arm
    w = obj.matrix_world.copy()
    obj.parent = arm
    obj.matrix_world = w


# ================================================================ LOD
def split_pieces_bm(me):
    bm = bmesh.new(); bm.from_mesh(me); G.ensure_layers(bm)
    pc = bm.faces.layers.int['ir_piece']
    ids = sorted({f[pc] for f in bm.faces})
    out = {}
    for pid in ids:
        b2 = bm.copy(); G.ensure_layers(b2)
        p2 = b2.faces.layers.int['ir_piece']
        bmesh.ops.delete(b2, geom=[f for f in b2.faces if f[p2] != pid], context='FACES')
        out[pid] = b2
    bm.free()
    return out


def decimate_mesh(me, ratio, col, protect=False):
    o = new_object('IR_tmp_dec', me, col)
    m = o.modifiers.new('dec', 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = ratio
    m.use_collapse_triangulate = True
    ka = me.attributes.get('ir_kind')
    if protect and ka is not None:
        vg = o.vertex_groups.new(name='decimate')
        keep = set()
        for poly in me.polygons:
            if ka.data[poly.index].value != 0:
                keep.update(poly.vertices)
        free = [v.index for v in me.vertices if v.index not in keep]
        if free:
            vg.add(free, 1.0, 'REPLACE')
        if keep:
            vg.add(sorted(keep), 0.0, 'REPLACE')
        m.vertex_group = 'decimate'; m.vertex_group_factor = 1.0
    dg = bpy.context.evaluated_depsgraph_get(); dg.update()
    out = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    bpy.data.objects.remove(o, do_unlink=True)
    return out


def build_lod_bm(src_me, ratio, col, keep_below=40, min_tris=12, keep_ids=(), protect=False):
    """Per-piece collapse decimation (UV interpolated, islands and rigid pieces kept; pieces < keep_below kept)."""
    parts = split_pieces_bm(src_me)
    out = bmesh.new(); G.ensure_layers(out)
    for pid, pbm in sorted(parts.items()):
        tris = G.tri_count_bm(pbm)
        if tris >= keep_below and ratio < 0.999 and pid not in keep_ids:
            G.triangulate_ngons(pbm, limit=3)
            me = G.bm_to_mesh(pbm, 'IR_tmp_piece')
            r = max(ratio, float(min_tris) / tris)
            chosen = None
            for attempt in range(6):
                dm = decimate_mesh(me, min(1.0, r), col, protect)
                p2 = bmesh.new(); p2.from_mesh(dm); G.ensure_layers(p2)
                bpy.data.meshes.remove(dm)
                loose = [v for v in p2.verts if not v.link_faces]
                if loose:
                    bmesh.ops.delete(p2, geom=loose, context='VERTS')
                h = G.mesh_health(p2)
                if h['non_manifold_edges'] == 0 and h['boundary_edges'] == 0 and h['zero_area_faces'] == 0:
                    chosen = p2
                    break
                p2.free()
                r = min(1.0, r * 1.2)
            bpy.data.meshes.remove(me)
            if chosen is None:
                chosen = pbm.copy(); G.ensure_layers(chosen)
            G.append_bm(out, chosen, piece=pid)
            chosen.free()
        else:
            G.append_bm(out, pbm, piece=pid)
        pbm.free()
    return out


def lod_ratio_for(src_meshes, budget, keep_below=40, keep_ids=()):
    small = 0; big = 0
    for me in src_meshes:
        parts = split_pieces_bm(me)
        for pid, pbm in parts.items():
            t = G.tri_count_bm(pbm)
            if t >= keep_below and pid not in keep_ids:
                big += t
            else:
                small += t
            pbm.free()
    return max(0.05, min(1.0, (budget - small) / max(big, 1))), small, big


# ================================================================ sockets
def rebuild_sockets(arm, frames, col):
    removed = []
    for o in list(bpy.data.objects):
        if o.type == 'EMPTY' and re.match(r'^(Wrist|ModuleSocket|Elbow)_[LR](\.\d+)?$', o.name):
            removed.append(o.name)
            bpy.data.objects.remove(o, do_unlink=True)
    made = []
    for side in ('R', 'L'):
        for name, fr, bone in (('Wrist_' + side, frames['Wrist_' + side], 'Hand_' + side),
                               ('ModuleSocket_' + side, frames['Wrist_' + side], 'Hand_' + side),
                               ('Elbow_' + side, frames['Elbow_' + side], 'Forearm_' + side)):
            e = bpy.data.objects.new(name, None)
            e.empty_display_type = 'ARROWS'; e.empty_display_size = 0.05
            col.objects.link(e)
            e.parent = arm; e.parent_type = 'BONE'; e.parent_bone = bone
            bpy.context.view_layer.update()
            e.matrix_world = fr.copy()
            bpy.context.view_layer.update()
            made.append({'name': e.name, 'parent_bone': bone,
                         'origin': [round(x, 5) for x in fr.translation],
                         'x': [round(x, 4) for x in fr.col[0].xyz], 'y': [round(x, 4) for x in fr.col[1].xyz],
                         'z': [round(x, 4) for x in fr.col[2].xyz]})
    return {'removed': sorted(removed), 'created': made}


# ================================================================ poses
def unity_delta_to_blender(bind, pose):
    d = bind.inverted() @ pose
    return Quaternion((d.w, d.x, -d.y, -d.z)).normalized()


def library_poses(lib):
    out = {}
    for pose in ('open', 'relaxed', 'fist', 'grip', 'inspect'):
        out[pose] = {n: unity_delta_to_blender(d['bind'], d[pose]) for n, d in lib.items()
                     if 'bind' in d and pose in d}
    return out


def apply_pose(arm, quats):
    for pb in arm.pose.bones:
        pb.rotation_mode = 'XYZ'
        pb.location = (0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
    for n, q in quats.items():
        pb = arm.pose.bones.get(n)
        if pb:
            pb.rotation_euler = q.to_euler('XYZ')
    bpy.context.view_layer.update()


def make_test_actions(arm, poses):
    made = []
    arm.animation_data_create()
    prev = arm.animation_data.action
    for pname, quats in poses.items():
        act = bpy.data.actions.new('IR_v02_' + pname)
        act.use_fake_user = True
        arm.animation_data.action = act
        apply_pose(arm, quats)
        for n in sorted(quats):
            pb = arm.pose.bones.get(n)
            if pb:
                pb.keyframe_insert('rotation_euler', frame=1, group=n)
        made.append(act.name)
    arm.animation_data.action = prev
    apply_pose(arm, {})
    return made


def decimate_to_budget(src_me, budget, col, keep_ids=(), keep_below=40, iterations=4, protect=True, min_ratio=0.0):
    """Per-piece collapse so that the total lands near budget (never below min_ratio); returns (bmesh, ratio, tris)."""
    ratio, small, big = lod_ratio_for([src_me], budget, keep_below, keep_ids)
    ratio = max(ratio, min_ratio)
    best = None
    for it in range(iterations):
        bm = build_lod_bm(src_me, ratio, col, keep_below=keep_below, keep_ids=keep_ids, protect=protect)
        tris = G.tri_count_bm(bm)
        if best is not None:
            best[0].free()
        best = (bm, ratio, tris)
        if ratio >= 0.999 or abs(tris - budget) <= max(20, 0.015 * budget):
            break
        ratio = max(min_ratio, 0.05, min(1.0, ratio * (budget - small) / max(tris - small, 1)))
        if ratio <= min_ratio and it > 0:
            break
    return best
