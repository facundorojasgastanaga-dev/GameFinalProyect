"""Author a fictional low-poly game prop locally; export and verify a 1000-vertex budget."""
import bpy, math, json, struct
from pathlib import Path
from mathutils import Vector
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Assets/IronRequiem/Art/Weapons/BasicPistolModule'
EDIT = ROOT / 'Tools/IronRequiem/Blender/Working/BasicPistolModule'
REPORT = ROOT / 'Tools/IronRequiem/Reports'
for p in (OUT, EDIT, REPORT): p.mkdir(parents=True, exist_ok=True)
if not bpy.app.background or '--factory-startup' not in __import__('sys').argv:
    raise RuntimeError('Run in a fresh background factory-startup Blender process.')
if (OUT/'PistolModule_1000.fbx').exists() and '--revise-draft' not in __import__('sys').argv: raise RuntimeError('Do not overwrite a delivered asset.')
for ob in list(bpy.data.objects): bpy.data.objects.remove(ob, do_unlink=True)
verts, faces, colors, parts = [], [], [], []

def face(points, color, part):
    start=len(verts); verts.extend(points); faces.append(list(range(start,len(verts))))
    colors.append(color); parts.append(part)

def prism(name, center, length, width, height, bevel, color, axis='Y'):
    first=len(faces)
    a,b=width/2,height/2; k=bevel
    cross=[(-a+k,-b),(a-k,-b),(a,-b+k),(a,b-k),(a-k,b),(-a+k,b),(-a,b-k),(-a,-b+k)]
    def point(u,v,t):
        q=(u,t,v) if axis=='Y' else (t,u,v)
        return tuple(q[i]+center[i] for i in range(3))
    rings=[[point(u,v,t) for u,v in cross] for t in (-length/2,length/2)]
    face(rings[0][::-1],color,name); face(rings[1],color,name)
    for i in range(8): j=(i+1)%8; face([rings[0][i],rings[0][j],rings[1][j],rings[1][i]],color,name)
    if axis=='Y':
        for f in faces[first:]: f.reverse()

def ring(name,y,z,outer,inner,length,n,color):
    first=len(faces)
    def p(r,i,t):
        angle=2*math.pi*i/n
        return (r*math.cos(angle),y+t,z+r*math.sin(angle))
    for i in range(n):
        j=(i+1)%n; t0=-length/2; t1=length/2
        face([p(outer,i,t0),p(outer,j,t0),p(outer,j,t1),p(outer,i,t1)],color,name)
        face([p(inner,j,t0),p(inner,i,t0),p(inner,i,t1),p(inner,j,t1)],4,name)
        face([p(inner,i,t0),p(inner,j,t0),p(outer,j,t0),p(outer,i,t0)],color,name)
        face([p(outer,i,t1),p(outer,j,t1),p(inner,j,t1),p(inner,i,t1)],color,name)
    for f in faces[first:]: f.reverse()

def box(name,c,size,color):
    x,y,z=c; a,b,d=[s/2 for s in size]
    p=[(x+i*a,y+j*b,z+k*d) for i,j,k in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    for ids in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]: face([p[i] for i in ids],color,name)

# Broad silhouette and visible fictional wrist coupling; no functional weapon internals.
prism('Casing',(0,-.02,0),.27,.10,.10,.012,0)
prism('RearReceiver',(0,.115,0),.045,.09,.09,.014,1)
prism('UpperReceiver',(0,.065,.055),.12,.07,.024,.006,1)
prism('LowerShroud',(0,-.01,-.05),.20,.078,.022,.006,1)
prism('MuzzleBezel',(0,-.168,0),.018,.11,.10,.016,1)
ring('DockingCollar',.164,0,.05,.032,.038,16,5)
ring('EmitterRim',-.184,0,.032,.022,.024,12,5)
prism('LeftPanel',(-.053,-.022,-.006),.004,.20,.065,.009,0,'X')
prism('RightPanel',(.053,-.022,-.006),.004,.20,.065,.009,0,'X')
prism('RearCover',(0,.135,0),.032,.09,.085,.012,0)
box('LeftStatus',(-.057,-.015,.009),(.003,.065,.007),3)
box('RightStatus',(.057,-.015,.009),(.003,.065,.007),3)
box('LeftLatch',(-.049,.120,0),(.008,.022,.035),2)
box('RightLatch',(.049,.120,0),(.008,.022,.035),2)
box('DarkEmitter',(0,-.177,0),(.047,.002,.047),4)
box('FrontDetail',(0,-.110,.061),(.025,.020,.012),1)
box('RearDetail',(0,.105,.071),(.042,.025,.012),1)
assert len(verts)==1000, len(verts)
mesh=bpy.data.meshes.new('PistolModule_1000'); mesh.from_pydata(verts,[],faces); mesh.update()
obj=bpy.data.objects.new('PistolModule_1000',mesh); bpy.context.collection.objects.link(obj)
bpy.context.view_layer.objects.active=obj; obj.select_set(True)
# Winding is authored per solid. Face corners are split for flat normals and stable export count.
uv=mesh.uv_layers.new(name='UV_Atlas')
for poly,color in zip(mesh.polygons,colors):
    coords=[Vector(mesh.vertices[i].co) for i in poly.vertices]
    axes=sorted(range(3), key=lambda i: max(p[i] for p in coords)-min(p[i] for p in coords), reverse=True)[:2]
    mins=[min(p[a] for p in coords) for a in axes]; ranges=[max(p[a] for p in coords)-mins[j] for j,a in enumerate(axes)]
    offset=(poly.index+1)*.00006
    for li,p in zip(poly.loop_indices,coords):
        u=(p[axes[0]]-mins[0])/max(ranges[0],1e-9); v=(p[axes[1]]-mins[1])/max(ranges[1],1e-9)
        uv.data[li].uv=((color%3+.06+offset+.84*u)/3,(color//3+.06+offset+.84*v)/2)

# One authored atlas and one material, no remote generation or credits.
size=768; rng=np.random.default_rng(401)
palette=[(.69,.67,.59),(.095,.105,.115),(.90,.24,.035),(.015,.78,.94),(.012,.018,.024),(.28,.31,.33)]
pixels=np.ones((size,size,4),dtype=np.float32); emission=np.zeros_like(pixels); emission[:,:,3]=1
for c,base in enumerate(palette):
    x0=(c%3)*256; y0=(c//3)*384
    noise=rng.normal(0,.012,(384,256,1)).astype(np.float32)
    tile=np.clip(np.array(base,dtype=np.float32)[None,None,:]+noise,0,1)
    if c!=3:
        for _ in range(120):
            x=int(rng.integers(0,253)); y=int(rng.integers(0,370)); length=int(rng.integers(2,14))
            tile[y:y+length,x:x+1,:]*=.66
    pixels[y0:y0+384,x0:x0+256,:3]=tile
    if c==3: emission[y0:y0+384,x0:x0+256,:3]=base
def image_file(name,data):
    im=bpy.data.images.new(name,width=size,height=size,alpha=True); im.pixels.foreach_set(data.ravel()); im.filepath_raw=str(OUT/(name+'.png')); im.file_format='PNG'; im.save(); return im
base=image_file('PistolModule_BaseColor',pixels); glow=image_file('PistolModule_Emission',emission)
mat=bpy.data.materials.new('PistolModule_Atlas'); mat.use_nodes=True; nodes=mat.node_tree.nodes; bsdf=nodes.get('Principled BSDF')
for im,input_name in [(base,'Base Color'),(glow,'Emission Color')]:
    tex=nodes.new('ShaderNodeTexImage'); tex.image=im; mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs[input_name])
bsdf.inputs['Metallic'].default_value=.65; bsdf.inputs['Roughness'].default_value=.60; bsdf.inputs['Emission Strength'].default_value=.7
mesh.materials.append(mat)
# Pivot at rear docking center, not at the middle of the receiver.
for v in mesh.vertices: v.co.y-=.183
for name,location in [('Muzzle',(0,-.379,0)),('Attachment',(0,0,0))]:
    socket=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(socket); socket.parent=obj; socket.location=location; socket.empty_display_size=.015
bpy.context.scene.unit_settings.system='METRIC'; bpy.context.scene.unit_settings.scale_length=1
for ob in bpy.context.scene.objects: ob.select_set(True)
fbx=OUT/'PistolModule_1000.fbx'; glb=OUT/'PistolModule_1000.glb'
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
bpy.ops.export_scene.gltf(filepath=str(glb),export_format='GLB',use_selection=True,export_apply=True)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(EDIT/'PistolModule_1000.blend'))
raw=glb.read_bytes(); length,kind=struct.unpack_from('<II',raw,12); gltf=json.loads(raw[20:20+length]); access=gltf['accessors']
exported=sum(access[p['attributes']['POSITION']]['count'] for m in gltf['meshes'] for p in m['primitives'])
mesh.calc_loop_triangles()
report={'source_vertices':len(mesh.vertices),'glb_vertices':exported,'triangles':len(mesh.loop_triangles),'materials':len(mesh.materials),'dimensions_m':list(obj.dimensions),'pivot':'rear attachment center','source_forward':'Blender -Y','parts':sorted(set(parts)),'fbx':str(fbx),'glb':str(glb),'blend':str(EDIT/'PistolModule_1000.blend')}
assert exported<=1000, exported
(REPORT/'pistol-module-model.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
# Standalone visual QA render, not included in exports or editable model.
scene=bpy.context.scene; scene.render.engine='BLENDER_EEVEE'; scene.render.resolution_x=1200;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.world.color=(.18,.18,.18); center=Vector((0,-.19,0))
for position,power,width in [((-.6,-.5,.7),70,.7),((.5,-.2,.3),45,.6),((0,.5,.4),55,.5)]:
    bpy.ops.object.light_add(type='AREA',location=position); light=bpy.context.object; light.data.energy=power;light.data.shape='DISK';light.data.size=width;light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(-.63,-.60,.34));scene.camera=bpy.context.object;scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=.52
scene.render.filepath=str(REPORT/'pistol-module-preview.png');bpy.ops.render.render(write_still=True)
print(json.dumps(report))
