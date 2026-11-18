"""Read-only 'before' renders (Workbench). Never saves .blend files.
blender --background --factory-startup --python-exit-code 1 --python render_before.py -- <mode> <src> <outdir>
modes: body | fps | capsule | capsule_rig | fist"""
import bpy, sys, os, math, json
from mathutils import Vector, Euler

args = sys.argv[sys.argv.index('--') + 1:]
MODE, SRC, OUTD = args
os.makedirs(OUTD, exist_ok=True)
if SRC.endswith('.blend'):
    bpy.ops.wm.open_mainfile(filepath=SRC, load_ui=False)
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC, use_anim=True)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.render.resolution_x = 1200; sc.render.resolution_y = 1200; sc.render.resolution_percentage = 100
sc.render.film_transparent = False
sh = sc.display.shading
sh.light = 'STUDIO'; sh.color_type = 'OBJECT'
sh.show_cavity = True; sh.cavity_type = 'BOTH'; sh.cavity_ridge_factor = 1.0; sh.cavity_valley_factor = 1.0
sh.show_object_outline = True; sh.object_outline_color = (0.02, 0.02, 0.02)
sh.show_specular_highlight = True
sh.background_type = 'VIEWPORT'; sh.background_color = (0.42, 0.43, 0.45)
if sc.world is None: sc.world = bpy.data.worlds.new('W')
sc.world.color = (0.42, 0.43, 0.45)
sc.view_settings.view_transform = 'Standard'
log = []

arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
def reset_pose():
    if not arm: return
    if arm.animation_data: arm.animation_data.action = None
    for p in arm.pose.bones:
        p.location = (0, 0, 0); p.rotation_quaternion = (1, 0, 0, 0); p.rotation_euler = (0, 0, 0); p.scale = (1, 1, 1)
def set_action(sub):
    reset_pose()
    act = next((a for a in bpy.data.actions if a.name.endswith(sub)), None)
    if not act: return None
    arm.animation_data_create(); arm.animation_data.action = act
    if hasattr(arm.animation_data, 'action_slot') and act.slots: arm.animation_data.action_slot = act.slots[0]
    sc.frame_set(int(act.frame_range[1])); return act.name

for o in bpy.data.objects:
    if o.type == 'EMPTY': o.hide_render = True
    if o.type == 'ARMATURE': o.hide_render = True
colors = {'Body': (0.62, 0.62, 0.62, 1), 'Forearm': (0.35, 0.55, 0.80, 1), 'Hand': (0.90, 0.55, 0.25, 1)}
for o in bpy.data.objects:
    if o.type == 'MESH':
        o.color = next((c for k, c in colors.items() if o.name.startswith(k)), (0.65, 0.65, 0.65, 1))

cam_data = bpy.data.cameras.new('AuditCam'); cam = bpy.data.objects.new('AuditCam', cam_data); sc.collection.objects.link(cam); sc.camera = cam
def look(loc, target, ortho=None, lens=50, name='x'):
    cam.location = Vector(loc)
    d = Vector(target) - Vector(loc)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    if ortho:
        cam_data.type = 'ORTHO'; cam_data.ortho_scale = ortho
    else:
        cam_data.type = 'PERSP'; cam_data.lens = lens
    cam_data.clip_start = 0.005; cam_data.clip_end = 50
    sc.render.filepath = os.path.join(OUTD, name + '.png')
    bpy.ops.render.render(write_still=True)
    log.append({'file': name + '.png', 'camera_loc': [round(x, 4) for x in loc], 'target': [round(x, 4) for x in target],
                'type': 'ORTHO' if ortho else 'PERSP', 'ortho_scale': ortho, 'lens_mm': None if ortho else lens})

def only(pred):
    for o in bpy.data.objects:
        if o.type == 'MESH': o.hide_render = not pred(o.name)

if MODE == 'body':
    reset_pose(); bpy.context.view_layer.update()
    only(lambda n: n.endswith('LOD0'))
    c = (0, 0, 0.9)
    look((0, -6, 0.9), c, 2.0, name='body_front')
    look((-6, 0, 0.9), c, 2.0, name='body_profile_right')
    look((6, 0, 0.9), c, 2.0, name='body_profile_left')
    look((0, 6, 0.9), c, 2.0, name='body_back')
    look((-4.24, -4.24, 1.4), c, 2.0, name='body_three_quarter')
    for lod in ['LOD1', 'LOD2']:
        only(lambda n, l=lod: n.endswith(l)); look((-4.24, -4.24, 1.4), c, 2.0, name=f'body_three_quarter_{lod}')
    only(lambda n: n.endswith('LOD0'))
    W = {'R': Vector((-0.41976, 0.0002, 0.99742)), 'L': Vector((0.41984, 0.0002, 0.99742))}
    E = {'R': Vector((-0.34085, 0.06648, 1.25467)), 'L': Vector((0.34093, 0.06648, 1.25467))}
    for s, sx in [('R', -1), ('L', 1)]:
        hc = W[s] + Vector((sx * 0.01, -0.01, -0.1))
        look(hc + Vector((0, -1.5, 0)), hc, 0.30, name=f'hand_{s}_front')
        look(hc + Vector((sx * 1.5, 0, 0)), hc, 0.30, name=f'hand_{s}_outer_side')
        look(hc + Vector((-sx * 1.5, -0.3, 0)), hc, 0.30, name=f'hand_{s}_inner_side')
        look(W[s] + Vector((sx * 0.8, -1.2, 0.3)), W[s], 0.16, name=f'wrist_{s}_closeup')
        look(W[s] + Vector((-sx * 0.6, -1.2, -0.4)), W[s], 0.16, name=f'wrist_{s}_closeup_under')
        look(E[s] + Vector((sx * 0.8, -1.2, 0.2)), E[s], 0.20, name=f'elbow_{s}_closeup')
        look(E[s] + Vector((sx * 0.4, 1.4, 0.1)), E[s], 0.20, name=f'elbow_{s}_closeup_back')
    # fist pose deformation (before)
    a = set_action('IR_HandFist'); bpy.context.view_layer.update()
    for s, sx in [('R', -1), ('L', 1)]:
        hc = W[s] + Vector((sx * 0.01, -0.01, -0.08))
        look(hc + Vector((0, -1.5, 0)), hc, 0.26, name=f'hand_{s}_fist_pose_front')
        look(hc + Vector((sx * 1.2, -0.6, 0.4)), hc, 0.26, name=f'hand_{s}_fist_pose_34')
    a2 = set_action('IR_PunchPosePreview'); bpy.context.view_layer.update()
    look((-4.24, -4.24, 1.4), c, 2.0, name='body_punch_preview_pose')
    log.append({'note': 'fist renders use action ' + str(a) + ' last frame; punch preview ' + str(a2)})
elif MODE == 'fps':
    # Approximate first-person camera: template Player.prefab CameraHeightRatio 0.8 x CapsuleHeightStanding 1.8 = 1.44 m.
    eye = Vector((0, -0.02, 1.44))
    WR = Vector((-0.41976, 0.0002, 0.99742)); WL = Vector((0.41984, 0.0002, 0.99742))
    for pose in [None, 'IR_HandGrip', 'IR_HandFist']:
        nm = set_action(pose) if pose else (reset_pose() or 'bind')
        bpy.context.view_layer.update()
        tag = pose or 'bind'
        look(tuple(eye), tuple(eye + Vector((0, -1, -1.2))), None, 18, name=f'fps_{tag}_eye1.44_pitch50')
        # Approximate over-the-forearm first-person framing (camera ~0.55 m behind/above/inside each wrist).
        look(tuple(WR + Vector((0.22, 0.45, 0.30))), tuple(WR + Vector((0, -0.06, -0.06))), None, 24, name=f'fps_{tag}_overforearm_R')
        look(tuple(WL + Vector((-0.22, 0.45, 0.30))), tuple(WL + Vector((0, -0.06, -0.06))), None, 24, name=f'fps_{tag}_overforearm_L')
    reset_pose(); bpy.context.view_layer.update()
    look((0, -1.2, 1.3), (0, 0, 1.05), 1.1, name='fps_parts_overview_front')
elif MODE in ('capsule', 'capsule_rig'):
    reset_pose(); bpy.context.view_layer.update()
    ms = [o for o in bpy.data.objects if o.type == 'MESH']
    zmax = max((o.matrix_world @ Vector(c)).z for o in ms for c in o.bound_box)
    c = (0, 0, zmax / 2); sz = zmax * 1.15
    look((0, -6, c[2]), c, sz, name=f'{MODE}_front')
    look((-4.24, -4.24, c[2] + 0.4), c, sz, name=f'{MODE}_three_quarter')
    look((-6, 0, c[2]), c, sz, name=f'{MODE}_profile_right')
    look((0, 6, c[2]), c, sz, name=f'{MODE}_back')
    if MODE == 'capsule_rig' and arm:
        for s, bn in [('R', 'mixamorig:RightHand'), ('L', 'mixamorig:LeftHand')]:
            b = arm.data.bones[bn]; h = arm.matrix_world @ b.head_local
            look(tuple(h + Vector((0, -1.5, -0.03))), tuple(h + Vector((0, 0, -0.03))), 0.16, name=f'{MODE}_hand_{s}_front')
            fa = arm.data.bones[bn.replace('Hand', 'ForeArm')]; e = arm.matrix_world @ fa.head_local
            look(tuple(e + Vector((-1 if s == 'R' else 1, -1, 0.2))), tuple(e), 0.14, name=f'{MODE}_elbow_{s}')
elif MODE == 'fist':
    ms = [o for o in bpy.data.objects if o.type == 'MESH']
    for o in ms:
        o.color = (0.9, 0.5, 0.2, 1) if 'Orange' in o.name else ((0.75, 0.77, 0.8, 1) if o.data.materials and o.data.materials[0] and o.data.materials[0].name == 'Steel' else (0.3, 0.32, 0.35, 1))
    c = (0, 0.065, 0.01)
    look((0.6, -0.4, 0.5), c, 0.22, name='rigid_fist_34')
    look((0, 0.065, 1.0), c, 0.22, name='rigid_fist_top_palm_side')
    look((0, 0.065, -1.0), c, 0.22, name='rigid_fist_dorsal')
    look((0, 1.0, 0.0), c, 0.22, name='rigid_fist_knuckles')
    look((-1.0, 0.065, 0.0), c, 0.22, name='rigid_fist_thumb_side')
with open(os.path.join(OUTD, f'render_log_{MODE}.json'), 'w') as fh: json.dump(log, fh, indent=1)
print('RENDER_DONE', MODE, len(log))
