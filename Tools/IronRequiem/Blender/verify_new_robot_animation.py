import bpy,json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Tools/IronRequiem/Exports/IR_NewRobot_Animated_v01';R=ROOT/'Tools/IronRequiem/Reports/NewRobotAnimation';spec=json.loads((ROOT/'Tools/IronRequiem/Exports/IR_NewRobot_Optimized_v01/Verification/geometry.json').read_text())
for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(OUT/'IR_NewRobot_Animated.fbx'),use_anim=True)
rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE'];meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(rigs)==1 and len(rigs[0].data.bones)==16;assert len(meshes)==45;assert len(bpy.data.actions)==7
expected={p['name']:p['triangles'] for lod in spec['lods'] for p in lod['parts']}
for o in meshes:
 o.data.calc_loop_triangles();assert len(o.data.loop_triangles)==expected[o.name];assert len(o.data.uv_layers)==1;assert all(len(v.groups)==1 and abs(v.groups[0].weight-1)<1e-6 for v in o.data.vertices);assert o.modifiers[0].object==rigs[0]
assert bpy.data.objects.get('WeaponMuzzle_R') is not None
result={'passed':True,'meshes':45,'shared_bones':16,'clips':list(a.name for a in bpy.data.actions),'rigid_weight_per_vertex':1,'topology_and_uv_layers_preserved':True,'muzzle_exists':True}
(R/'fbx-check.json').write_text(json.dumps(result,indent=2));print('ANIMATED_FBX_CHECK_PASSED',flush=True)
