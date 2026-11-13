import bpy,json,bmesh
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
r=Path(__file__).resolve().parents[3];out=r/'Tools/IronRequiem/Reports/T07Destruction'
bpy.ops.wm.open_mainfile(filepath=str(r/'Tools/IronRequiem/Blender/Working/T07Destructible/T07_ArmorRig.blend'))
with bpy.data.libraries.load(str(r/'Tools/IronRequiem/Blender/Working/T07Animated/T07_SharedRig.blend'),link=False) as (src,dst):dst.objects=['Body_LOD0','Body_LOD1','Body_LOD2']
topology=[]
for source in dst.objects:
 bm=bmesh.new();bm.from_mesh(source.data);topology.append({'source':source.name,'boundary':sum(e.is_boundary for e in bm.edges),'nonmanifold':sum(not e.is_manifold for e in bm.edges)});bm.free()
(out/'source-topology.json').write_text(json.dumps(topology,indent=2))
source=dst.objects[0];mesh=source.data;mesh.calc_loop_triangles();tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[list(p.vertices) for p in mesh.polygons])
points=[]
for x,z in [(-.1,1.42),(.12,1.40),(.045,1.88),(0,1.16),(-.17,.35),(.18,.74)]:
 hit,_,_,_=tree.ray_cast(Vector((x,-1,z)),Vector((0,1,0)))
 if hit is not None:points.append(list(hit))
assert len(points)>=3,'Need verified surface points for damage marks'
triangles=[]
for t in mesh.loop_triangles:
 co=[mesh.vertices[i].co for i in t.vertices]
 if any(min((v-Vector(p)).length for v in co)<.12 for p in points):triangles.append({'co':[list(v) for v in co],'uv':[list(mesh.uv_layers.active.data[i].uv) for i in t.loops]})
(out/'damage-uv.json').write_text(json.dumps({'points':points,'triangles':triangles}))
sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sc.render.resolution_x=1000;sc.render.resolution_y=1000;sc.render.resolution_percentage=100
sc.display.shading.light='STUDIO';sc.display.shading.color_type='MATERIAL';sc.display.shading.show_cavity=True;sc.display.shading.cavity_type='BOTH'
for obj in sc.objects:
 if obj.type=='MESH':obj.hide_render=not obj.name.endswith('LOD0')
bpy.ops.object.camera_add(location=(2.6,-4,2.2));cam=bpy.context.object;sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.3;cam.rotation_euler=(Vector((0,0,1.03))-cam.location).to_track_quat('-Z','Y').to_euler()
regions=json.loads((out/'armor-authoring.json').read_text())['regions']
for state in [0,1,2]:
 for obj in sc.objects:
  if obj.type=='MESH' and obj.name.startswith('Armor_') and obj.name.endswith('LOD0'):
   obj.hide_render=state>=next(a['stage'] for a in regions if a['name'] in obj.name)
 sc.render.filepath=str(out/f'armor-stage-{state}.png');bpy.ops.render.render(write_still=True)
