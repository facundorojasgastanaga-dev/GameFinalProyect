import bpy
p = str(ROOT / 'Tools/IronRequiem/Blender/Working/protagonist_v02/refined/IR_Protagonist_v02_refined_iter3.blend')
bpy.ops.wm.open_mainfile(filepath=p, load_ui=False)
act = bpy.data.actions.get('IR_v02_Fist'); rig = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
if act and rig:
    rig.animation_data_create(); rig.animation_data.action = act
print('opened', p, 'pose', act.name if act else None)
