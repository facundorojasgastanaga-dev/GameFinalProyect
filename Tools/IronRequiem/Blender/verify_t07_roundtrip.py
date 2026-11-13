"""Reimport all delivered FBX/GLB LODs, compare topology and dimensions, keep originals intact."""
import bpy, json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
expected=json.loads((ROOT/'Tools/IronRequiem/Reports/T07Optimization/geometry.json').read_text())
results=[]
for level in range(3):
    for suffix in ('fbx','glb'):
        for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
        path=ROOT/f'Tools/IronRequiem/Exports/T07Optimized/T07_LOD{level}.{suffix}'
        if suffix=='fbx':bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
        else:bpy.ops.import_scene.gltf(filepath=str(path))
        bpy.context.view_layer.update()
        meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
        assert len(meshes)==2
        for obj in meshes:obj.data.calc_loop_triangles()
        triangles=sum(len(o.data.loop_triangles) for o in meshes)
        assert triangles==expected['lods'][level]['triangles'], (path,triangles)
        assert all(o.data.uv_layers for o in meshes)
        assert all(len(o.data.materials)==1 for o in meshes)
        assert not any(o.type=='ARMATURE' for o in bpy.context.scene.objects)
        bounds=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
        size=[max(p[i] for p in bounds)-min(p[i] for p in bounds) for i in range(3)]
        assert 2.00<size[2]<2.10
        materials={m for o in meshes for m in o.data.materials if m}
        images={n.image for m in materials if m.use_nodes for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image}
        if suffix=='glb':assert all(max(im.size)<=2048 for im in images)
        results.append({'path':str(path),'triangles':triangles,'vertices':sum(len(o.data.vertices) for o in meshes),
                        'meshes':len(meshes),'dimensions_m':size,'valid_uv':True,'images':len(images),
                        'image_sizes':[list(im.size) for im in images]})
(ROOT/'Tools/IronRequiem/Reports/T07Optimization/roundtrip.json').write_text(json.dumps({'success':True,'models':results},indent=2))
print(json.dumps(results))
