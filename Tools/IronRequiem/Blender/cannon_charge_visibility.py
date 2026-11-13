import bpy,json,math
from pathlib import Path
from mathutils import Vector,Quaternion
from mathutils.bvhtree import BVHTree
r=Path(__file__).resolve().parents[3];out=r/'Tools/IronRequiem/Reports/CannonCharge'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(r/'Assets/IronRequiem/Art/Enemies/T07Optimized/T07_LOD0.fbx'))
gun=next(o for o in bpy.context.scene.objects if o.type=='MESH' and 'Cannon' in o.name)
fit=json.loads((out/'mouth-fit.json').read_text());point=Vector(fit['unity_mesh_local_center']);normal=Vector(fit['unity_mesh_local_forward']);mouth=Vector((.279,-.282,.945))
base=Quaternion(Vector((1,0,0)),math.pi);rotation=(base@normal).rotation_difference(Vector((0,0,1)))@base
verts=[Vector((-v.co.x,v.co.y,v.co.z)) for v in gun.data.vertices];faces=[list(p.vertices) for p in gun.data.polygons];records=[]
for roll in [-90,-60,-45,-30,0,30,45,60,90,135,180]:
 rot=Quaternion(Vector((0,0,1)),math.radians(roll))@rotation;position=mouth-rot@(point*100)
 tree=BVHTree.FromPolygons([position+rot@v for v in verts],faces)
 for depth in [0,.025,.05,.075,.1,.125,.15,.175,.2]:
  visible=0;total=0;center=False
  for i in range(-8,9):
   for j in range(-8,9):
    x=i*.01;y=j*.01;weight=math.exp(-(x*x+y*y)/.001)
    p=mouth+Vector((x,y,depth));hit,_,_,distance=tree.ray_cast(Vector((0,0,0)),p.normalized(),p.length)
    open=hit is None or distance>=p.length-.001
    visible+=weight*open;total+=weight
    if i==j==0:center=open
  records.append({'roll':roll,'depth_m':depth,'core_center_visible':center,'weighted_visible_fraction':visible/total})
(out/'visibility-study.json').write_text(json.dumps(records,indent=2))
for d in sorted(records,key=lambda d:(not d['core_center_visible'],d['depth_m'],-d['weighted_visible_fraction']))[:20]:print(d)
