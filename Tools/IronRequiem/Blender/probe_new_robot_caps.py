import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[3];O=R/'Tools/IronRequiem/Exports/IR_NewRobot_Optimized_v01'
bpy.ops.wm.open_mainfile(filepath=str(O/'IR_NewRobot_Editable.blend'))
for name in ('Torso_LOD0','Torso_LOD2'):
 o=bpy.data.objects[name];bm=bmesh.new();bm.from_mesh(o.data)
 for f in sorted(bm.faces,key=lambda f:f.calc_area())[:3]:print(name,'SMALL',f.calc_area(),[[list(v.co),len(v.link_edges)] for v in f.verts],[(e.calc_length()) for e in f.edges])
 bm.free()
for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(O/'IR_NewRobot_LOD0.fbx'),use_anim=False)
o=bpy.data.objects['Torso_LOD0'];bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
print('HOLE',[[list(v.co) for v in e.verts] for e in bm.edges if len(e.link_faces)!=2]);bm.free()
