"""Reimport exported FBX/GLB parts; verify geometry, textures, caps, sockets and rest layout."""
import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Tools/IronRequiem/Exports/IR_Protagonist_Optimized_v01'
REPORT=ROOT/'Tools/IronRequiem/Reports/ProtagonistOptimization'
geometry=json.loads((REPORT/'geometry.json').read_text())
rows=[]
for stem,level in [(f'IR_Protagonist_LOD{i}',i) for i in range(3)]+[('IR_Protagonist_FPS_Parts',0)]:
    for extension in ('fbx','glb'):
        for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
        path=OUT/(stem+'.'+extension)
        if extension=='fbx':bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
        else:bpy.ops.import_scene.gltf(filepath=str(path))
        bpy.context.view_layer.update()
        meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
        expected=geometry['lods'][level]['parts']
        if stem.endswith('FPS_Parts'):expected=[p for p in expected if not p['name'].startswith('Body')]
        assert len(meshes)==len(expected),(path,'Unexpected body/duplicate meshes')
        total=0;parts=[]
        for obj in meshes:
            spec=next(p for p in expected if p['name']==obj.name)
            mesh=obj.data;mesh.calc_loop_triangles()
            assert len(mesh.loop_triangles)==spec['triangles'],(path,obj.name,len(mesh.loop_triangles),spec['triangles'])
            assert len(mesh.materials)==1 and len(mesh.uv_layers)==1
            assert not obj.vertex_groups and not any(m.type=='ARMATURE' for m in obj.modifiers)
            expected_pivot=Vector(spec['pivot_blender_m'])
            assert (obj.matrix_world.translation-expected_pivot).length<1e-5,(path,obj.name,'Pivot changed')
            corners=[obj.matrix_world@Vector(c) for c in obj.bound_box]
            parts.append({'name':obj.name,'triangles':len(mesh.loop_triangles),'vertices':len(mesh.vertices),'pivot_m':list(obj.matrix_world.translation),
                          'bounds_min':[min(c[i] for c in corners) for i in range(3)],'bounds_max':[max(c[i] for c in corners) for i in range(3)]})
            total+=len(mesh.loop_triangles)
        nodes={o.name:o for o in bpy.context.scene.objects}
        assert all(name in nodes for name in ('Elbow_L','Elbow_R','Wrist_L','Wrist_R','ModuleSocket_L','ModuleSocket_R'))
        for side,landmark in geometry['landmarks'].items():
            assert (nodes['ModuleSocket_'+side].matrix_world.translation-Vector(landmark['wrist'])).length<1e-5
            aim=nodes['ModuleSocket_'+side].matrix_world.to_3x3()@Vector((0,0,1))
            assert aim.normalized().dot(Vector(landmark['normal']))>.99999
        materials={m for o in meshes for m in o.data.materials if m}
        images={n.image for m in materials if m.use_nodes for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image}
        assert images and all(tuple(im.size)==(2048,2048) for im in images),(path,'Missing or oversized textures')
        assert not any(o.type=='ARMATURE' for o in bpy.context.scene.objects)
        rows.append({'file':path.name,'triangles':total,'mesh_count':len(meshes),'sockets_verified':True,'textures_2k':True,'parts':parts})
result={'success':True,'unity_imported':False,'rig':False,'models':rows}
(REPORT/'roundtrip.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({'success':True,'models':[{'file':r['file'],'triangles':r['triangles'],'meshes':r['mesh_count']} for r in rows]}))
