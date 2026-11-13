import bpy, bmesh, json
from pathlib import Path
root = Path(__file__).resolve().parents[3]
result = []
for name in ('Body_LOD0', 'Body_LOD1', 'Body_LOD2'):
    obj = bpy.data.objects[name]
    bm = bmesh.new(); bm.from_mesh(obj.data)
    pending = {e for e in bm.edges if e.is_boundary}
    loops = []
    while pending:
        first = pending.pop(); edges = [first]; verts = set(first.verts); stack = list(first.verts)
        while stack:
            v = stack.pop()
            for edge in v.link_edges:
                if edge in pending:
                    pending.remove(edge); edges.append(edge)
                    for other in edge.verts:
                        if other not in verts: verts.add(other); stack.append(other)
        points = [obj.matrix_world @ v.co for v in verts]
        lo = [min(v[i] for v in points) for i in range(3)]
        hi = [max(v[i] for v in points) for i in range(3)]
        loops.append({'edges': len(edges), 'min': lo, 'max': hi})
    result.append({'name': name, 'boundary_loops': sorted(loops, key=lambda r:r['edges'], reverse=True), 'triangles': sum(len(p.vertices)-2 for p in obj.data.polygons)})
    bm.free()
path = root / 'Tools/IronRequiem/Reports/ShredderT07Repair/blender-leg-boundaries.json'
path.write_text(json.dumps(result, indent=2))
print('T07_LEG_SURFACES', str(path))
