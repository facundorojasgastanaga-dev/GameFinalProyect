"""IR-09: final export of the refined protagonist v02 (read-only input, writes only to
Tools/IronRequiem/Exports/IR_Protagonist_Refined_v01/).

  blender --background --factory-startup --python-exit-code 1 --python Tools/IronRequiem/Blender/export_protagonist_v02.py

Input  (read only): Tools/IronRequiem/Blender/Working/protagonist_v02/refined/IR_Protagonist_v02_refined.blend (+ its
                    metrics.json and renders/), v01 FBX in Assets/IronRequiem/Art/Player/ProtagonistV01/Rigged (compare)
Output: IR_Protagonist_v02_Rigged.fbx, IR_Protagonist_v02_FPS_Rigged.fbx, Textures/, renders/, metrics.json, README.md
FBX convention = v01 (rig_protagonist.py): axis_forward -Z, axis_up Y, FBX_SCALE_UNITS, add_leaf_bones False,
use_mesh_modifiers False, path_mode STRIP, armature IR_Rig as root. Clips: one NLA strip per action (2 frames for the
validated poses), bone-only baked animation.
"""
import sys
sys.dont_write_bytecode = True
import bpy
import json
import hashlib
import shutil
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
REFINED = ROOT / 'Tools/IronRequiem/Blender/Working/protagonist_v02/refined'
SRC_BLEND = REFINED / 'IR_Protagonist_v02_refined.blend'
OUT = ROOT / 'Tools/IronRequiem/Exports/IR_Protagonist_Refined_v01'
V01 = {'Rigged': ROOT / 'Assets/IronRequiem/Art/Player/ProtagonistV01/Rigged/IR_Protagonist_Rigged.fbx',
       'FPS': ROOT / 'Assets/IronRequiem/Art/Player/ProtagonistV01/Rigged/IR_Protagonist_FPS_Rigged.fbx'}
PROTECTED = ROOT / 'Tools/IronRequiem/Backups/20261116-protagonist-v02/protected-sha256.txt'
FBX_NAMES = {'Rigged': 'IR_Protagonist_v02_Rigged.fbx', 'FPS': 'IR_Protagonist_v02_FPS_Rigged.fbx'}
POSE_CLIPS = ['IR_v02_Open', 'IR_v02_Relaxed', 'IR_v02_Fist', 'IR_v02_Grip', 'IR_v02_Inspect']
V01_CLIPS_KEPT = ['IR_Idle', 'IR_Walk']           # body-only, still valid with the moved hand bones
V01_CLIPS_DROPPED = ['IR_HandFist', 'IR_HandGrip', 'IR_HandNormal', 'IR_PunchPosePreview']
SOCKETS = ['Wrist_R', 'ModuleSocket_R', 'Elbow_R', 'Wrist_L', 'ModuleSocket_L', 'Elbow_L']
LOG = []


def log(*a):
    s = ' '.join(str(x) for x in a); LOG.append(s); print('[IR-v02-export]', s)


def sha(p):
    h = hashlib.sha256()
    with open(p, 'rb') as f:
        for c in iter(lambda: f.read(1 << 20), b''):
            h.update(c)
    return h.hexdigest()


def protected_check():
    res = {}
    for line in PROTECTED.read_text(encoding='utf-8').splitlines():
        if not line.strip():
            continue
        h, rel = line.split(' ', 1)
        rel = rel.lstrip('*').strip()
        p = ROOT / rel
        res[rel] = (p.exists() and sha(p) == h)
    return res


def tris(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def scene_info():
    """Names, hierarchy, tris, influences, bones, actions and sockets of the current scene."""
    info = {'objects': {}, 'bones': [], 'actions': sorted(a.name for a in bpy.data.actions), 'armature': None}
    for o in sorted(bpy.data.objects, key=lambda o: o.name):
        e = {'type': o.type, 'parent': o.parent.name if o.parent else None,
             'parent_bone': o.parent_bone or None}
        if o.type == 'MESH':
            me = o.data
            e['tris'] = tris(me)
            e['max_bones_per_vertex'] = max((sum(1 for g in v.groups if g.weight > 1e-6) for v in me.vertices), default=0)
            e['materials'] = [m.name if m else None for m in me.materials]
        if o.type == 'EMPTY':
            m = o.matrix_world
            e['origin'] = [round(x, 5) for x in m.translation]
            e['z_axis'] = [round(x, 4) for x in m.col[2].xyz.normalized()]
            e['y_axis'] = [round(x, 4) for x in m.col[1].xyz.normalized()]
        if o.type == 'ARMATURE':
            info['armature'] = {'name': o.name, 'data': o.data.name,
                                'location': [round(x, 5) for x in o.location],
                                'rotation_euler_deg': [round(x * 57.29578, 3) for x in o.rotation_euler],
                                'scale': [round(x, 5) for x in o.scale]}
            info['bones'] = [b.name for b in o.data.bones]
            info['bone_heads'] = {b.name: [round(x, 5) for x in (o.matrix_world @ b.head_local)] for b in o.data.bones}
            info['bone_parents'] = {b.name: (b.parent.name if b.parent else None) for b in o.data.bones}
        info['objects'][o.name] = e
    return info


def prepare_clips(arm):
    """Two-frame validated poses + kept v01 clips, one NLA track/strip per action; no object keys."""
    scene = bpy.context.scene
    arm.animation_data_create()
    for name in POSE_CLIPS:
        act = bpy.data.actions[name]
        arm.animation_data.action = act
        scene.frame_set(1)
        for pb in arm.pose.bones:
            for path in ('rotation_euler', 'location', 'scale'):
                pb.keyframe_insert(path, frame=1, group=pb.name)
                pb.keyframe_insert(path, frame=2, group=pb.name)
    arm.animation_data.action = None
    for t in list(arm.animation_data.nla_tracks):
        arm.animation_data.nla_tracks.remove(t)
    # one take per action (v01 convention, bake_anim_use_all_actions): only the clips listed are left in memory.
    # The NLA-strip mode was tried first: it also bakes constant curves for the bone-parented socket empties,
    # whereas the per-action mode keys the armature only (checked on reimport).
    for a in list(bpy.data.actions):
        if a.name not in POSE_CLIPS + V01_CLIPS_KEPT:
            bpy.data.actions.remove(a)
    for o in bpy.data.objects:            # no object-level animation in the export
        if o is not arm and o.animation_data:
            o.animation_data_clear()
    for p in arm.pose.bones:
        p.rotation_mode = 'XYZ'; p.location = (0, 0, 0); p.rotation_euler = (0, 0, 0); p.scale = (1, 1, 1)
    scene.frame_set(1)


def export(kind, arm):
    if kind == 'Rigged':
        meshes = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.split('_')[-1] in ('LOD0', 'LOD1', 'LOD2')]
    else:
        meshes = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.endswith('_FPS')]
    empties = [bpy.data.objects[n] for n in SOCKETS]
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in meshes + empties + [arm]:
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    path = OUT / FBX_NAMES[kind]
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH', 'ARMATURE', 'EMPTY'},
                             axis_forward='-Z', axis_up='Y', add_leaf_bones=False, use_mesh_modifiers=False,
                             bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             bake_anim_use_all_bones=True, bake_anim_force_startend_keying=True,
                             bake_anim_simplify_factor=0, apply_scale_options='FBX_SCALE_UNITS', path_mode='STRIP')
    log('exported', path.name, len(meshes), 'meshes')
    return sorted(o.name for o in meshes)


def mirror_check():
    """iter4: Hand_L_X must be the exact X mirror of Hand_R_X in every LOD (vertex order kept by the FBX)."""
    out = {}
    for k in ('LOD0', 'LOD1', 'LOD2', 'FPS'):
        r = bpy.data.objects.get('Hand_R_' + k); l = bpy.data.objects.get('Hand_L_' + k)
        if not (r and l):
            continue
        dev = None
        if len(r.data.vertices) == len(l.data.vertices):
            mr, ml = r.matrix_world, l.matrix_world
            dev = 0.0
            for a, b in zip(r.data.vertices, l.data.vertices):
                p = mr @ a.co; q = ml @ b.co
                dev = max(dev, (q - Vector((-p.x, p.y, p.z))).length)
            dev = round(dev * 1000, 4)
        out[k] = {'tris_R': tris(r.data), 'tris_L': tris(l.data), 'max_dev_mm': dev}
    return out


def reimport(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True)
    info = scene_info()
    info['hand_mirror'] = mirror_check()
    return info


def write_readme(r):
    v = r['verification']
    lines = [
        '# IR_Protagonist_Refined_v01 (protagonista v02)',
        '',
        'Exportado por `Tools/IronRequiem/Blender/export_protagonist_v02.py` desde',
        '`Tools/IronRequiem/Blender/Working/protagonist_v02/refined/IR_Protagonist_v02_refined.blend` (solo lectura).',
        '',
        '- `IR_Protagonist_v02_Rigged.fbx`: IR_Rig (50 huesos v01) + Body/Forearm_L/Forearm_R/Hand_L/Hand_R LOD0-2 + sockets',
        '  Wrist_R/L, ModuleSocket_R/L (hueso Hand_X) y Elbow_R/L (hueso Forearm_X), unicos, +Z distal, +Y dorso.',
        '- `IR_Protagonist_v02_FPS_Rigged.fbx`: IR_Rig + Hand_R_FPS, Hand_L_FPS, Forearm_R_FPS, Forearm_L_FPS + los mismos sockets.',
        '- Clips: ' + ', '.join(r['clips']['exported']) + ' (poses v02: 2 fotogramas). Retirados de v01: '
        + ', '.join(r['clips']['dropped_v01']) + '.',
        '- Texturas: las mismas imagenes del atlas v01 (copiadas en `Textures/`); materiales Material.001-.004 como v01.',
        '- Convencion FBX de v01: axis_forward -Z, axis_up Y, FBX_SCALE_UNITS, sin leaf bones, armature raiz.',
        '  Importar en Unity igual que v01: useFileScale 1, globalScale 1, bakeAxisConversion 0, Generic.',
        '- Diferencias con v01: mismos nombres de objetos, huesos y jerarquia; cabezas de dedos/pulgar desplazadas',
        '  (recentrado, pulgar reajustado, falanges 2-3 x0,72) -> regenerar HandPoseLibrary y bind poses;',
        '  sockets unicos (v01 Rigged tenia .001/.002, v01 FPS no tenia sockets). Clips con el mismo formato que v01 (IR_Rig|IR_Rig|<accion>): una toma por accion, solo el armature lleva curvas.',
        '- Verificacion (reimportado en Blender): tris = refined: ' + str(v['Rigged']['tris_all_equal'] and v['FPS']['tris_all_equal'])
        + '; huesos = v01: ' + str(v['Rigged']['bones_equal_v01'] and v['FPS']['bones_equal_v01'])
        + '; 5 clips: ' + str(v['Rigged']['all_5_pose_clips_present'] and v['FPS']['all_5_pose_clips_present'])
        + '; SHA protegidos sin cambios: ' + str(r['protected_unchanged'])
        + '; mano L espejo exacto de R en LOD0/1/2/FPS: '
        + str(all(e['max_dev_mm'] is not None and e['max_dev_mm'] < 1e-3 and e['tris_R'] == e['tris_L']
                  for kind in ('Rigged', 'FPS') for e in v[kind]['hand_L_mirror_of_R'].values())) + '.',
        '- Iteracion 4 (solo limpieza): corte del codo sin laminas ni islas (cuello del cuerpo re-elegido, tapa del',
        '  antebrazo plana a 1,5 mm de la del cuerpo en todos los LOD), aletas de la palma < 1 mm colapsadas, UV estiradas',
        '  de la palma re-proyectadas a la placa clara de las falanges, espiga de la muneca con chaflan de raiz y ranura',
        '  en V (sin cambiar diametros ni posiciones de IR-ACOPLE-01), mano L espejo de R tambien en LOD1/LOD2.',
        '  Estado anterior en `iter3/`.',
        '- `metrics.json`: verificacion completa, cuaterniones de las poses (Blender local y Unity absoluto),',
        '  cambios de longitud de huesos y resumen del refinado; `renders/`: renders Workbench de la iteracion 4.',
    ]
    (OUT / 'README.md').write_text(chr(10).join(lines) + chr(10), encoding='utf-8')


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    report = {'task': 'IR-09 protagonist v02 export', 'blender': bpy.app.version_string,
              'source_blend': str(SRC_BLEND.relative_to(ROOT)), 'source_blend_sha256': sha(SRC_BLEND),
              'protected_before': protected_check()}
    bpy.ops.wm.open_mainfile(filepath=str(SRC_BLEND), load_ui=False)
    arm = bpy.data.objects['IR_Rig']
    refined = scene_info()
    # textures used by the materials (same files as v01)
    tex = {}
    for m in bpy.data.materials:
        if not m.use_nodes:
            continue
        for n in m.node_tree.nodes:
            if n.type == 'TEX_IMAGE' and n.image:
                p = Path(bpy.path.abspath(n.image.filepath)).resolve()
                tex[p.name] = str(p)
    (OUT / 'Textures').mkdir(exist_ok=True)
    copied = {}
    for name, p in sorted(tex.items()):
        dst = OUT / 'Textures' / name
        shutil.copy2(p, dst)
        copied[name] = {'source': str(Path(p).relative_to(ROOT)), 'sha256': sha(dst)}
    report['textures'] = {'copied': copied, 'note': 'same v01 atlas images (IR_Protagonist_Optimized_v01/Textures); '
                          'v02 only remaps new faces into existing graphite/light-plate areas, nothing repainted'}
    prepare_clips(arm)
    report['exported_objects'] = {k: export(k, arm) for k in ('Rigged', 'FPS')}
    report['clips'] = {'exported': POSE_CLIPS + V01_CLIPS_KEPT, 'dropped_v01': V01_CLIPS_DROPPED,
                       'why_dropped': 'v01 finger/thumb curls (IR_HandFist/Grip/Normal/PunchPosePreview) rotate the '
                                      'v01 thumb axes and finger angles: they interpenetrate the new hinged phalanges; '
                                      'replaced by IR_v02_Open/Relaxed/Fist/Grip/Inspect',
                       'frames_validated_poses': [1, 2]}
    sockets_ref = {n: refined['objects'][n] for n in SOCKETS}
    # ---- reimport and verify
    ver = {}
    for kind in ('Rigged', 'FPS'):
        new = reimport(OUT / FBX_NAMES[kind])
        old = reimport(V01[kind])
        meshes_new = {n: e for n, e in new['objects'].items() if e['type'] == 'MESH'}
        tri_ok = {n: (e['tris'], refined['objects'].get(n, {}).get('tris')) for n, e in meshes_new.items()}
        socks = {n: e for n, e in new['objects'].items() if e['type'] == 'EMPTY'}
        sock_check = {}
        for n in SOCKETS:
            e = socks.get(n)
            r = sockets_ref[n]
            sock_check[n] = {'present': e is not None,
                             'z_matches_refined': e is not None and (Vector(e['z_axis']) - Vector(r['z_axis'])).length < 2e-3,
                             'origin_matches_refined': e is not None and (Vector(e['origin']) - Vector(r['origin'])).length < 2e-4,
                             'parent_bone': e['parent_bone'] if e else None,
                             'z_axis': e['z_axis'] if e else None}
        dup = [n for n in new['objects'] if '.0' in n]
        clips = [a for a in new['actions']]
        ver[kind] = {
            'objects': sorted(new['objects']),
            'tris_reimport_vs_refined': tri_ok,
            'tris_all_equal': all(a == b for a, b in tri_ok.values()),
            'max_bones_per_vertex': {n: e['max_bones_per_vertex'] for n, e in meshes_new.items()},
            'bones_equal_v01': new['bones'] == old['bones'],
            'bone_count': [len(new['bones']), len(old['bones'])],
            'bones_missing_vs_v01': sorted(set(old['bones']) - set(new['bones'])),
            'bones_extra_vs_v01': sorted(set(new['bones']) - set(old['bones'])),
            'bone_parents_equal_v01': new['bone_parents'] == old['bone_parents'],
            'bone_heads_moved_mm_vs_v01': {b: round((Vector(new['bone_heads'][b]) - Vector(old['bone_heads'][b])).length * 1000, 2)
                                           for b in new['bones'] if b in old['bone_heads'] and
                                           (Vector(new['bone_heads'][b]) - Vector(old['bone_heads'][b])).length > 1e-5},
            'armature_new': new['armature'], 'armature_v01': old['armature'],
            'mesh_objects_v01': sorted(n for n, e in old['objects'].items() if e['type'] == 'MESH'),
            'mesh_objects_new': sorted(meshes_new),
            'mesh_parents_new': {n: e['parent'] for n, e in meshes_new.items()},
            'mesh_parents_v01': {n: e['parent'] for n, e in old['objects'].items() if e['type'] == 'MESH'},
            'empties_v01': sorted(n for n, e in old['objects'].items() if e['type'] == 'EMPTY'),
            'empties_new': sorted(socks), 'duplicate_suffix_objects': dup,
            'sockets': sock_check,
            'clips_reimported': clips, 'clips_v01_reimported': old['actions'],
            'all_5_pose_clips_present': all(any(c.endswith(p) for c in clips) for p in POSE_CLIPS),
            'hand_L_mirror_of_R': new['hand_mirror'],
            'materials_new': sorted({m for e in meshes_new.values() for m in e['materials'] if m}),
            'materials_v01': sorted({m for e in old['objects'].values() if e['type'] == 'MESH' for m in e['materials'] if m}),
        }
        log(kind, 'tris equal', ver[kind]['tris_all_equal'], 'bones equal', ver[kind]['bones_equal_v01'],
            'clips', ver[kind]['all_5_pose_clips_present'])
    report['verification'] = ver
    # ---- copy renders + refined metrics summary
    rd = OUT / 'renders'
    if rd.exists():
        shutil.rmtree(rd)
    shutil.copytree(REFINED / 'renders', rd)
    rm = json.loads((REFINED / 'metrics.json').read_text(encoding='utf-8'))
    report['refined_summary'] = {k: rm.get(k) for k in ('summary_tris', 'phalanx_shortening', 'thumb_axis',
                                                        'joint_recentre_mm', 'thumb_refit_offsets_m', 'sockets',
                                                        'finish', 'fingers_v2')}
    report['refined_summary']['budget'] = rm['verification']['budget']
    report['refined_summary']['iter4'] = {k: rm.get(k) for k in ('iter4', 'iter4_elbow_neck', 'iter4_neck_recut',
                                                                 'iter4_body_islands_removed', 'iter4_palm_fins',
                                                                 'iter4_palm_uv')}
    report['refined_summary']['weights_before'] = {k: v['max_bones_per_vertex'] for k, v in rm['before']['weights'].items()}
    report['validated_poses'] = rm['validated_poses']
    report['protected_after'] = protected_check()
    report['protected_unchanged'] = all(report['protected_after'].values()) and \
        report['protected_after'] == report['protected_before']
    report['log'] = LOG
    write_readme(report)
    (OUT / 'metrics.json').write_text(json.dumps(report, indent=1, default=str), encoding='utf-8')
    log('done; protected unchanged:', report['protected_unchanged'])


if __name__ == '__main__':
    main()
