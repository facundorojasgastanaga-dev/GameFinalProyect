import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[3];bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(root/'Assets/IronRequiem/Art/Weapons/BasicPistolModule/PistolModule_1000.fbx'),use_anim=False)
r=[]
for o in bpy.context.scene.objects:
 item={'name':o.name,'type':o.type,'position':list(o.matrix_world.translation)}
 if o.type=='MESH':item['bounds']=[list(o.matrix_world@Vector(v)) for v in o.bound_box]
 r.append(item)
(root/'Tools/IronRequiem/Reports/ProtagonistActions/pistol-source.json').write_text(json.dumps(r,indent=2));print(json.dumps(r))
