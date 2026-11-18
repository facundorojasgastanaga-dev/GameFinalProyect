"""Prepare a clean, unrigged GLB copy of a rigged FBX for Tripo3D auto-rig.

Run only in a fresh background Blender:
  blender --background --factory-startup --python-exit-code 1 --python export_tripo_input.py -- \
      --input <rigged.fbx> --output <out.glb> --report <report.json> [--preview-dir <dir>]

What it does (on the in-memory copy only; the source FBX is never written):
  1. Imports the FBX and records its SHA-256.
  2. Puts every armature in REST pose, removes Armature modifiers and the armature/empties,
     keeping each mesh's world transform (bind pose geometry is unchanged).
  3. Joins all meshes into one object (materials and UVs kept).
  4. Turns the model about the vertical axis so it faces Blender -Y (glTF/Unity +Z), measured from
     the shoulder/hip bones, puts the feet on z=0 and centres the hips on the origin. Scale is kept.
  5. Exports GLB with textures, then re-hashes the source to prove it did not change.
"""
import argparse
import hashlib
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def parse():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--input", required=True)
    p.add_argument("--output", required=True)
    p.add_argument("--report", required=True)
    p.add_argument("--preview-dir")
    return p.parse_args(argv)


def bone_world(arm, name):
    b = arm.data.bones.get(name)
    return None if b is None else arm.matrix_world @ b.head_local


def render_previews(obj, out_dir, report):
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1100
    scene.render.film_transparent = False
    dims = obj.dimensions
    h = dims.z
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = max(dims) * 1.25
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    views = {
        "front": (Vector((0, -5 * h, h * 0.5)), (math.radians(90), 0, 0)),
        "side": (Vector((5 * h, 0, h * 0.5)), (math.radians(90), 0, math.radians(90))),
        "back": (Vector((0, 5 * h, h * 0.5)), (math.radians(90), 0, math.radians(180))),
    }
    report["previews"] = []
    for name, (loc, rot) in views.items():
        cam.location = loc
        cam.rotation_euler = rot
        path = os.path.join(out_dir, f"input_{name}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        report["previews"].append(path)


def main():
    args = parse()
    src = os.path.abspath(args.input)
    out = os.path.abspath(args.output)
    if os.path.exists(out):
        raise SystemExit(f"Refusing to overwrite existing output: {out}")
    report = {"source": src, "source_sha256_before": sha256(src), "output": out}

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src)

    arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    report["imported"] = {"armatures": len(arms), "meshes": len(meshes),
                          "triangles": sum(sum(len(p.vertices) - 2 for p in m.data.polygons) for m in meshes)}

    # Facing from the skeleton, before it is removed.
    facing = None
    if arms:
        arm = arms[0]
        pairs = [("Left_UpperArm", "Right_UpperArm"), ("Left_UpperLeg", "Right_UpperLeg")]
        left = Vector((0, 0, 0))
        for l, r in pairs:
            pl, pr = bone_world(arm, l), bone_world(arm, r)
            if pl is not None and pr is not None:
                left += (pl - pr)
        left.z = 0
        if left.length > 1e-6:
            left.normalize()
            forward = Vector((left.y, -left.x, 0))  # character left rotated -90 deg about +Z
            toes = Vector((0, 0, 0))
            for side in ("Left", "Right"):
                f, t = bone_world(arm, side + "_Foot"), bone_world(arm, side + "_Toes")
                if f is not None and t is not None:
                    toes += (t - f)
            toes.z = 0
            agree = forward.dot(toes.normalized()) if toes.length > 1e-6 else None
            yaw = math.atan2(forward.x, -forward.y)  # angle from -Y
            facing = {"forward_xy": [forward.x, forward.y], "toes_agreement_cos": agree,
                      "yaw_from_minus_y_deg": math.degrees(yaw)}
        for a in arms:
            a.data.pose_position = "REST"
    report["facing"] = facing
    bpy.context.view_layer.update()

    # Unparent meshes keeping world transform, drop armature modifiers.
    for m in meshes:
        mw = m.matrix_world.copy()
        for mod in list(m.modifiers):
            if mod.type == "ARMATURE":
                m.modifiers.remove(mod)
        m.parent = None
        m.matrix_world = mw
        m.vertex_groups.clear()
    for o in list(bpy.context.scene.objects):
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)

    # Apply transforms and join.
    bpy.ops.object.select_all(action="DESELECT")
    for m in meshes:
        m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = "T07Mech_TripoInput"

    # Face -Y, feet on ground, hips at origin.
    if facing:
        yaw = math.radians(facing["yaw_from_minus_y_deg"])
        obj.data.transform(Matrix.Rotation(-yaw, 4, "Z"))
    zs = [v.co.z for v in obj.data.vertices]
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    obj.data.transform(Matrix.Translation(Vector((-(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, -min(zs)))))
    obj.data.update()
    bpy.context.view_layer.update()

    report["output_mesh"] = {
        "vertices": len(obj.data.vertices),
        "triangles": sum(len(p.vertices) - 2 for p in obj.data.polygons),
        "materials": len(obj.data.materials),
        "uv_layers": [uv.name for uv in obj.data.uv_layers],
        "dimensions_xyz_m": list(obj.dimensions),
    }

    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=False,
                              export_apply=True, export_skins=False, export_animations=False)
    report["output_bytes"] = os.path.getsize(out)
    report["output_sha256"] = sha256(out)
    if args.preview_dir:
        render_previews(obj, os.path.abspath(args.preview_dir), report)
    report["source_sha256_after"] = sha256(src)
    report["source_unchanged"] = report["source_sha256_before"] == report["source_sha256_after"]
    with open(args.report, "w", encoding="utf-8") as f:
        json.dump(report, f, indent=2)
    print("IRON REQUIEM TRIPO INPUT:", json.dumps({k: report[k] for k in ("output_mesh", "facing", "output_bytes", "source_unchanged")}))
    if not report["source_unchanged"]:
        raise SystemExit("Source hash changed")


main()
