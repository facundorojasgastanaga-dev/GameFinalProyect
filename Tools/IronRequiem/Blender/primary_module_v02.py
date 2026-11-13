"""Local revision of existing pistol module; source blend/FBX/textures untouched."""
import bpy, bmesh, math, json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Tools/IronRequiem/Blender/Working/PrimaryModuleV02'
OUT.mkdir(parents=True,exist_ok=True)
source=ROOT/'Tools/IronRequiem/Blender/Working/BasicPistolModule/PistolModule_1000.blend'
if not bpy.app.background: raise RuntimeError('Background copy only')
bpy.ops.wm.open_mainfile(filepath=str(source))
source_stats=[{'name':o.name,'vertices':len(o.data.vertices),'dimensions':list(o.dimensions)} for o in bpy.context.scene.objects if o.type=='MESH']
for o in list(bpy.data.objects): bpy.data.objects.remove(o,do_unlink=True)
verts=[];faces=[];colors=[]
def face(points,color):
    start=len(verts);verts.extend(points);faces.append(tuple(range(start,len(verts))));colors.append(color)
def cross(y,w,h,bevel,vertical=0,horizontal=0):
    x=w/2;z=h/2;k=bevel
    return [(a+horizontal,y,b+vertical) for a,b in [(-x+k,-z),(x-k,-z),(x,-z+k),(x,z-k),(x-k,z),(-x+k,z),(-x,z-k),(-x,-z+k)]]
def shell(profiles,color):
    rings=[cross(*p) for p in profiles]
    face(list(reversed(rings[0])),color);face(rings[-1],color)
    for a,b in zip(rings,rings[1:]):
        for i in range(8):j=(i+1)%8;face([a[i],a[j],b[j],b[i]],color)
def block(c,size,color):
    x,y,z=c;a,b,d=[s/2 for s in size]
    pts=[(x+i*a,y+j*b,z+k*d) for i,j,k in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    for ids in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]:face([pts[i] for i in ids],color)
def ring(y0,y1,outer,inner,color,n=16):
    def p(y,r,i):a=math.tau*i/n;return (r*math.cos(a),y,r*math.sin(a))
    for i in range(n):
        j=(i+1)%n
        face([p(y0,outer,i),p(y0,outer,j),p(y1,outer,j),p(y1,outer,i)],color)
        face([p(y0,inner,j),p(y0,inner,i),p(y1,inner,i),p(y1,inner,j)],4)
        face([p(y0,inner,i),p(y0,inner,j),p(y0,outer,j),p(y0,outer,i)],color)
        face([p(y1,outer,i),p(y1,outer,j),p(y1,inner,j),p(y1,inner,i)],color)
# Shallow wrist connection, tapered receiver, no handle or glove volume.
shell([(.004,.076,.078,.010,.0013,.0035),(-.016,.080,.080,.011,.001,.003),(-.068,.098,.092,.013),(-.238,.098,.090,.013),(-.306,.090,.086,.013)],1)
shell([(-.050,.090,.084,.012),(-.085,.104,.096,.014),(-.230,.102,.094,.014),(-.296,.094,.089,.013)],0)
shell([(-.065,.048,.014,.004,.050),(-.092,.054,.020,.004,.050),(-.240,.052,.018,.004,.050),(-.283,.042,.012,.003,.047)],1)
shell([(-.070,.047,.011,.003,-.045),(-.240,.048,.014,.003,-.046),(-.283,.037,.010,.002,-.043)],1)
ring(-.298,-.328,.038,.025,5)
ring(-.309,-.333,.028,.022,1)
block((0,-.311,0),(.041,.002,.041),4)
for sign in (-1,1):
    block((sign*.0525,-.158,.006),(.002,.084,.010),1)
    block((sign*.0536,-.158,.006),(.0015,.065,.004),3)
    block((sign*.046,-.060,-.005),(.005,.020,.024),2)
    for y in (-.230,-.249,-.268):block((sign*.048,y,-.012),(.002,.009,.020),1)
shell([(.0045,.077,.079,.010,.0013,.0035),(0,.0775,.0795,.010,.0013,.0035),(-.008,.0785,.0805,.011,.001,.0035)],5)
mesh=bpy.data.meshes.new('IR_PrimaryModule_v02');mesh.from_pydata(verts,[],faces);mesh.update()
obj=bpy.data.objects.new('IR_PrimaryModule_v02',mesh);bpy.context.collection.objects.link(obj);bpy.context.view_layer.objects.active=obj;obj.select_set(True)
uv=mesh.uv_layers.new(name='UV_Atlas')
for poly,color in zip(mesh.polygons,colors):
    coords=[mesh.vertices[i].co for i in poly.vertices]
    axes=sorted(range(3),key=lambda i:max(p[i] for p in coords)-min(p[i] for p in coords),reverse=True)[:2]
    mins=[min(p[a] for p in coords) for a in axes];ranges=[max(p[a] for p in coords)-mins[j] for j,a in enumerate(axes)]
    for li,point in zip(poly.loop_indices,coords):
        u=(point[axes[0]]-mins[0])/max(ranges[0],1e-9);v=(point[axes[1]]-mins[1])/max(ranges[1],1e-9)
        uv.data[li].uv=((color%3+.06+.84*u)/3,(color//3+.06+.84*v)/2)
mat=bpy.data.materials.get('PistolModule_Atlas');assert mat is not None;mesh.materials.append(mat)
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));open_edges=sum(1 for e in bm.edges if e.is_boundary);nonmanifold=sum(1 for e in bm.edges if not e.is_manifold);assert open_edges==0,(open_edges,nonmanifold);bm.to_mesh(mesh);bm.free();mesh.update()
for name,loc in [('Attachment',(0,0,0)),('Muzzle',(0,-.333,0))]:
    socket=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(socket);socket.parent=obj;socket.location=loc;socket.empty_display_size=.012
bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
for o in bpy.context.scene.objects:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'IR_PrimaryModule_v02.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'IR_PrimaryModule_v02.blend'))
mesh.calc_loop_triangles();data={'source':str(source),'source_meshes':source_stats,'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),'dimensions':list(obj.dimensions),'length_attachment_to_muzzle':.333,'rear_overlap':.004,'materials':len(mesh.materials),'boundary_edges':open_edges,'nonmanifold_edges':nonmanifold};(OUT/'geometry.json').write_text(json.dumps(data,indent=2));print(json.dumps(data))