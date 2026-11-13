"""Read-only arm cross-section samples for choosing cuts in the supplied source."""
import bpy,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.import_scene.fbx(filepath='C:/Users/gehbd/OneDrive/Escritorio/Juegos GJ/PrincipalBot/71443cd84dd6034935129fc02c5facfa.fbx',use_anim=False)
obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
world=[obj.matrix_world@v.co for v in obj.data.vertices]
rows=[]
for z in [0.59+i*.01 for i in range(25)]:
    for s in [-1,1]:
        points=[v for v in world if s*v.x>.19 and abs(v.z-z)<.003]
        if points:
            rows.append({'z':round(z,3),'side':s,'count':len(points),'center_x':sum(v.x for v in points)/len(points),'center_y':sum(v.y for v in points)/len(points),
                         'width_x':max(v.x for v in points)-min(v.x for v in points),'depth_y':max(v.y for v in points)-min(v.y for v in points)})
(ROOT/'Tools/IronRequiem/Reports/ProtagonistOptimization/joint-samples.json').write_text(json.dumps(rows,indent=2))
print(json.dumps(rows))
