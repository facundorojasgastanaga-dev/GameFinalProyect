import bpy
p = str(ROOT / 'Tools/IronRequiem/Blender/Working/protagonist_v02/refined/IR_Protagonist_v02_refined_iter1.blend')
bpy.ops.wm.open_mainfile(filepath=p, load_ui=False)
print('opened', p, len(bpy.data.objects))
