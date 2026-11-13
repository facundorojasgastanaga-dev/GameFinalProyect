"""Read-only geometry/weights/LOD inspection and plate-region study."""
import bpy,json
from pathlib import Path
from mathutils import Vector
r=Path(__file__).resolve().parents[3];o=r/'Tools/IronRequiem/Reports/T07Destruction';o.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(r/'Tools/IronRequiem/Blender/Working/T07Animated/T07_SharedRig.blend'))
spec=[('Pectoral_L',[-.225,-.24,1.31],[-.025,-.105,1.51],'Chest',1),('Shoulder_R',[.245,-.16,1.52],[.48,.16,1.68],'UpperArm_R',1),('Pectoral_R',[.025,-.24,1.31],[.225,-.105,1.51],'Chest',2),('Shoulder_L',[-.48,-.16,1.52],[-.245,.16,1.68],'UpperArm_L',2)]
records=[]
for obj in bpy.context.scene.objects:
 if obj.type=='MESH':
  obj.data.calc_loop_triangles();records.append({'name':obj.name,'vertices':len(obj.data.vertices),'triangles':len(obj.data.loop_triangles),'uv_layers':len(obj.data.uv_layers),'groups':[g.name for g in obj.vertex_groups],'bounds':[[min(v.co[i] for v in obj.data.vertices),max(v.co[i] for v in obj.data.vertices)] for i in range(3)],'candidate_vertices':{n:sum(all(a[i]<=v.co[i]<=b[i] for i in range(3)) for v in obj.data.vertices) for n,a,b,_,_ in spec}})
(o/'geometry-inspection.json').write_text(json.dumps({'lods':records,'bones':[b.name for a in bpy.data.armatures for b in a.bones],'plate_candidates':[{'name':n,'min':a,'max':b,'bone':bone,'stage':s} for n,a,b,bone,s in spec]},indent=2))
for obj in bpy.context.scene.objects:obj.hide_render=obj.type=='MESH' and obj.name!='Body_LOD0'
sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sc.render.resolution_x=1100;sc.render.resolution_y=1100;sc.render.resolution_percentage=100
sc.display.shading.light='STUDIO';sc.display.shading.color_type='MATERIAL';sc.display.shading.show_cavity=True;sc.display.shading.cavity_type='BOTH'
for n,a,b,_,_ in spec:
 bpy.ops.mesh.primitive_cube_add(size=1,location=(Vector(a)+Vector(b))*.5);cube=bpy.context.object;cube.name='Candidate_'+n;cube.scale=Vector(b)-Vector(a);cube.display_type='WIRE';cube.hide_render=True
bpy.ops.object.camera_add();cam=bpy.context.object;sc.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.25
for name,pos in [('front',(0,-5,1.08)),('side',(4,-3,1.7)),('back',(0,5,1.08))]:
 cam.location=pos;cam.rotation_euler=(Vector((0,0,1.03))-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(o/f'inspect-{name}.png');bpy.ops.render.render(write_still=True)
print(json.dumps(records))
