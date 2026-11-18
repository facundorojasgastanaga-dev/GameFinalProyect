"""Author an original rigid mechanical fist. Never imports or overwrites source character assets."""
import bpy
import json
import os
from mathutils.bvhtree import BVHTree

OUT = os.path.abspath("Tools/IronRequiem/Exports/PlayerRigidFist")
os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

materials = []
for name, color, metal, roughness in [
    ("Graphite", (.065, .075, .085, 1), .85, .34),
    ("Steel", (.43, .46, .47, 1), .9, .32),
    ("SafetyOrange", (.82, .19, .025, 1), .65, .4),
]:
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs['Base Color'].default_value = color
    shader.inputs['Metallic'].default_value = metal
    shader.inputs['Roughness'].default_value = roughness
    materials.append(material)

pieces = []
def block(name, center, dimensions, material, bevel=.003, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(materials[material])
    modifier = obj.modifiers.new("Machined edges", 'BEVEL')
    modifier.width = bevel
    modifier.segments = 2
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    pieces.append(obj)
    return obj

# Canonical hand coordinates: wrist at origin, knuckles +Y, palm +Z, index toward -X.
block("Palm housing", (0, .063, 0), (.108, .095, .056), 0, .009)
block("Dorsal steel plate", (0, .063, -.032), (.102, .078, .014), 1, .005)
block("Orange identity panel", (0, .075, -.041), (.074, .028, .005), 2, .0015)
block("Wrist coupler", (0, .008, 0), (.076, .031, .061), 0, .008)
for i, x in enumerate((-.0405, -.0135, .0135, .0405)):
    # Closed rigid links: forward knuckle cap, middle link under the palm and folded distal link.
    y = .118 - (.005 if i == 3 else 0)
    block(f"Knuckle_{i}", (x, y, -.006), (.025, .032, .039), 1, .006)
    block(f"Middle_{i}", (x, y - .007, .031), (.023, .032, .038), 1, .004)
    block(f"Distal_{i}", (x, .083, .048), (.023, .030, .021), 1, .004)
    block(f"Hinge_{i}", (x, .103, .035), (.025, .009, .025), 0, .002)
# Thumb lies on the outside of the index/middle second links, with a 2 mm clear gap to their surfaces.
block("Thumb base", (-.064, .068, .041), (.026, .068, .030), 0, .006, (0, 0, -.28))
block("Thumb middle", (-.047, .103, .0635), (.035, .025, .023), 1, .005, (0, 0, .35))
block("Thumb tip", (-.017, .110, .0625), (.033, .024, .021), 1, .005, (0, 0, .15))

vertices, triangles, groups = [], [], []
for obj in pieces:
    obj.data.calc_loop_triangles()
    mat = materials.index(obj.data.materials[0])
    for triangle in obj.data.loop_triangles:
        base = len(vertices) // 3
        for index in triangle.vertices:
            point = obj.matrix_world @ obj.data.vertices[index].co
            vertices.extend(point)
        triangles.extend((base, base+1, base+2))
        groups.append(mat)
with open(os.path.join(OUT, "closed_fist.json.tmp"), 'w', encoding='utf-8') as stream:
    json.dump({"vertices": vertices, "triangles": triangles, "materials": groups}, stream)
os.replace(os.path.join(OUT, "closed_fist.json.tmp"), os.path.join(OUT, "closed_fist.json"))
surface_vertices, surface_faces = [], []
for obj in pieces:
    if obj.name not in {"Middle_0", "Middle_1"}:
        continue
    offset = len(surface_vertices)
    surface_vertices.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
    surface_faces.extend(tuple(offset + i for i in face.vertices) for face in obj.data.polygons)
surface = BVHTree.FromPolygons(surface_vertices, surface_faces)
tip = next(obj for obj in pieces if obj.name == "Thumb tip")
gap = min(surface.find_nearest(tip.matrix_world @ v.co)[3] for v in tip.data.vertices)
palm = next(obj for obj in pieces if obj.name == "Palm housing")
palm_surface = BVHTree.FromPolygons([palm.matrix_world @ v.co for v in palm.data.vertices],
                                   [tuple(face.vertices) for face in palm.data.polygons])
finger_gaps = {}
for index, name in enumerate(("Index", "Middle", "Ring", "Little")):
    distal = next(obj for obj in pieces if obj.name == f"Distal_{index}")
    finger_gaps[name] = min(palm_surface.find_nearest(distal.matrix_world @ v.co)[3]
                            for v in distal.data.vertices) * 1000
with open(os.path.join(OUT, "geometry_metrics.json"), 'w', encoding='utf-8') as stream:
    json.dump({"thumb_tip_to_index_middle_second_mm": gap * 1000,
               "distal_pad_to_palm_mm": finger_gaps,
               "all_blender_piece_scales_one": all(tuple(obj.scale) == (1, 1, 1) for obj in pieces),
               "rigid_fist_triangles": len(groups), "coordinate_units": "metres"}, stream, indent=2)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "closed_fist.blend"))
print(f"Rigid fist exported: {len(vertices)//3} vertices, {len(groups)} triangles")
