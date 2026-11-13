"""Adapt actual template weapon geometry and use UV regions from real protagonist maps."""
import bpy,bmesh,json,math,numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Tools/IronRequiem/Blender/Working/HitWeaponV04';OUT.mkdir(parents=True,exist_ok=True)
source=json.loads((OUT/'template-geometry.json').read_text(encoding='utf-8-sig'))
bpy.ops.wm.read_factory_settings(use_empty=True)
tex=ROOT/'Assets/IronRequiem/Art/Player/ProtagonistV01/Source/Textures/IR_Protagonist_BaseColor.png'
im=bpy.data.images.load(str(tex));width,height=im.size;pixels=np.asarray(im.pixels[:],dtype=np.float32).reshape(height,width,4)[:,:,:3]
regions={}
def region(name,n,predicate):
    best=-1;rect=None
    for y in range(0,height-n,max(8,n//2)):
        for x in range(0,width-n,max(8,n//2)):
            patch=pixels[y:y+n,x:x+n];score=float(predicate(patch).mean())
            if score>best:best=score;rect=(x,y,n,n)
    x,y,w,h=rect;avg=pixels[y:y+h,x:x+w].mean(axis=(0,1));regions[name]={'x':x/width,'y':y/height,'width':w/width,'height':h/height,'coverage':best,'rgb':[float(v) for v in avg]};return regions[name]
region('armor',128,lambda p:(p[:,:,0]>.52)&(p[:,:,1]>.44)&(p[:,:,2]>.35)&((p.max(2)-p.min(2))<.27))
region('metal',64,lambda p:(p.max(2)<.18)&(p.max(2)>.025)&((p.max(2)-p.min(2))<.06))
region('orange',32,lambda p:(p[:,:,0]>.5)&(p[:,:,1]>.12)&(p[:,:,1]<.6)&(p[:,:,2]<.13))
region('cyan',16,lambda p:(p[:,:,2]>.3)&(p[:,:,1]>.25)&(p[:,:,0]<.13))
verts=[];faces=[];roles=[]
def collect(bm,role):
    bm.verts.ensure_lookup_table();base=len(verts);verts.extend([tuple(v.co) for v in bm.verts]);faces.extend([[base+v.index for v in f.verts] for f in bm.faces]);roles.extend([role]*len(bm.faces))
parts_report=[]
for part in source['parts']:
    bm=bmesh.new();v=[bm.verts.new((p['x'],p['y'],p['z'])) for p in part['vertices']];tri=part['triangles']
    for i in range(0,len(tri),3):
        try:bm.faces.new([v[tri[i+j]] for j in range(3)])
        except ValueError:pass
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
    for point,normal in [((0,.075,0),(0,1,0)),((0,0,.05),(0,0,1))]:
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),plane_co=point,plane_no=normal,clear_inner=True,dist=1e-6)
        edges=[e for e in bm.edges if e.is_boundary]
        if edges:bmesh.ops.holes_fill(bm,edges=edges,sides=0)
    for vtx in bm.verts:
        x,y,z=vtx.co;z=(z-.456)*.82+.333;y=(y-.124)*.6;x*=.65 if part['name']=='Primary_Weapon' else .52
        if z<.055:
            t=max(0,min(1,z/.055));x=max(-.0385-.020*t,min(.0385+.020*t,x))+.0035*(1-t);y=max(-.038-.010*t,min(.040+.025*t,y))
        vtx.co=(x,-z,y)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6);bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-7);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));parts_report.append({'source':part['name'],'faces':len(bm.faces),'open_edges':sum(e.is_boundary for e in bm.edges)});collect(bm,'armor' if part['name']=='Primary_Weapon' else 'metal');bm.free()
def face(points,role):
    start=len(verts);verts.extend(points);faces.append(list(range(start,len(verts))));roles.append(role)
def shell(profiles,role):
    rings=[]
    for z,w,h in profiles:
        a=w/2;b=h/2;k=.010;rings.append([(x+.0035,-z,y+.0013) for x,y in [(-a+k,-b),(a-k,-b),(a,-b+k),(a,b-k),(a-k,b),(-a+k,b),(-a,b-k),(-a,-b+k)]])
    face(rings[0][::-1],role);face(rings[-1],role)
    for a,b in zip(rings,rings[1:]):
        for i in range(8):j=(i+1)%8;face([a[i],a[j],b[j],b[i]],role)
shell([(-.0045,.077,.079),(.008,.0775,.0795),(.032,.087,.088)],'metal')
def ring():
    n=16
    def p(z,r,i):a=math.tau*i/n;return (r*math.cos(a),-z,r*math.sin(a))
    for i in range(n):
        j=(i+1)%n;face([p(.313,.033,i),p(.313,.033,j),p(.333,.033,j),p(.333,.033,i)],'metal');face([p(.313,.022,j),p(.313,.022,i),p(.333,.022,i),p(.333,.022,j)],'metal');face([p(.313,.022,i),p(.313,.022,j),p(.313,.033,j),p(.313,.033,i)],'metal');face([p(.333,.033,i),p(.333,.033,j),p(.333,.022,j),p(.333,.022,i)],'metal')
ring()
# Small repaired latch and a painted status inset, using actual orange/cyan player pixels.
for sign in (-1,1):
    for z0,z1,y0,y1,role in [(.045,.065,-.014,.014,'orange'),(.14,.205,.008,.014,'cyan')]:
        x=sign*.059;face([(x,-z0,y0),(x,-z1,y0),(x,-z1,y1),(x,-z0,y1)],role);face([(x,-z0,y1),(x,-z1,y1),(x,-z1,y0),(x,-z0,y0)],role)
mesh=bpy.data.meshes.new('IR_TemplateArmModule_v04');mesh.from_pydata(verts,[],faces);mesh.update();obj=bpy.data.objects.new('IR_TemplateArmModule_v04',mesh);bpy.context.collection.objects.link(obj);bpy.context.view_layer.objects.active=obj;obj.select_set(True)
uv=mesh.uv_layers.new(name='ProtagonistSurfaceRegions')
for poly,role in zip(mesh.polygons,roles):
    points=[mesh.vertices[i].co for i in poly.vertices];axes=sorted(range(3),key=lambda a:max(p[a] for p in points)-min(p[a] for p in points),reverse=True)[:2];sample=[v.co for v in mesh.vertices] if role in ("armor","metal") else points;mins=[min(p[a] for p in sample) for a in axes];ranges=[max(p[a] for p in sample)-mins[i] for i,a in enumerate(axes)];rect=regions[role]
    for loop,p in zip(poly.loop_indices,points):
        u=(p[axes[0]]-mins[0])/max(ranges[0],1e-8);v=(p[axes[1]]-mins[1])/max(ranges[1],1e-8);uv.data[loop].uv=(rect['x']+rect['width']*(.03+.94*u),rect['y']+rect['height']*(.03+.94*v))
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update()
mat=bpy.data.materials.new('ProtagonistSurface');mat.use_nodes=True;node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=im;mat.node_tree.links.new(node.outputs['Color'],mat.node_tree.nodes['Principled BSDF'].inputs['Base Color']);mesh.materials.append(mat)
for polygon in obj.data.polygons:polygon.use_smooth=True
split=obj.modifiers.new('TemplateHardEdges','EDGE_SPLIT');split.split_angle=math.radians(38);split.use_edge_angle=True;split.use_edge_sharp=False
bpy.ops.object.modifier_apply(modifier=split.name);mesh=obj.data
for name,loc in [('Attachment',(0,0,0)),('Muzzle',(0,-.333,0))]:o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.parent=obj;o.location=loc
for o in bpy.context.scene.objects:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'IR_TemplateArmModule_v04.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'IR_TemplateArmModule_v04.blend'));mesh.calc_loop_triangles();report={'source':'Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab','protagonist_basecolor':str(tex),'uv_regions':regions,'parts':parts_report,'triangles':len(mesh.loop_triangles),'dimensions':list(obj.dimensions),'muzzle_distance':.333};(OUT/'weapon-geometry.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))