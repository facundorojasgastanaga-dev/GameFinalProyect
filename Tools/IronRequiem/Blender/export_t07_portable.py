"""Portable FBX/GLB delivery from derived geometry; resize only temporary texture copies."""
import bpy, sys
from pathlib import Path
import shutil
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Tools/IronRequiem/Exports/T07Optimized'
OUT.mkdir(parents=True,exist_ok=True)
assert not (OUT/'T07_LOD0.glb').exists() or '--revise-draft' in sys.argv, 'Do not overwrite delivered GLBs'
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Tools/IronRequiem/Blender/Working/T07Optimized/T07_Optimized.blend'))
for im in bpy.data.images:
    if im.type=='IMAGE' and im.size[0] and im.size[1]:
        im.scale(2048,2048)
        # Names stored by FBX refer to the preserved T07_New texture names.
        name=Path(bpy.path.abspath(im.filepath)).name
        im.filepath_raw=str(OUT/name);im.file_format='PNG';im.save()
        # Drop the packed original bytes, which the glTF exporter otherwise reuses
        # even after resizing the live pixels and writing a smaller PNG.
        if im.packed_file: im.unpack(method='REMOVE')
        im.reload()
        assert tuple(im.size)==(2048,2048)
for level in range(3):
    bpy.ops.object.select_all(action='DESELECT')
    for name in ('Body','CannonModule'):
        obj=bpy.data.objects[name+'_LOD'+str(level)]
        obj.hide_set(False);obj.hide_render=False;obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(OUT/f'T07_LOD{level}.glb'), export_format='GLB',
                             use_selection=True, export_animations=False, export_cameras=False,
                             export_lights=False, export_image_format='AUTO')
    shutil.copy2(ROOT/f'Assets/IronRequiem/Art/Enemies/T07Optimized/T07_LOD{level}.fbx',OUT/f'T07_LOD{level}.fbx')
print('Portable models and 2048px textures: '+str(OUT))
