"""IRON REQUIEM - mount station art prep (19/11/2026).

Turns the Tripo-segmented gantry (256 parts, no UVs, normalised to 1 m wide, front = -Y, up = +Z) into the pieces
the montage station animates, WITHOUT touching the source file:
  Static        gantry frame + carriage + the two back arms + the two bells (never move)
  FR_Hanger/Upper/Fore, FL_Hanger/Upper/Fore   the two front arms, split at their joints
Every loose island of every part is classified by its centroid against the measured joint centres (P0 carriage
pivot, P1 shoulder hub, P2 elbow cylinder, P3 tool tip) and gets one of five material slots by size/shape
(IR_MS_Steel, IR_MS_Paint, IR_MS_Bolt, IR_MS_Hose, IR_MS_Brass). Joint centres are exported as empties
(FR_P0..P3, FL_P0..P3) so Unity reads them in its own axes. Meshes keep their origin at the model origin.

Usage (background):
  blender -b --factory-startup --python-exit-code 1 --python mount_station_prep.py -- --input SRC.fbx --output OUT.fbx
      --report REPORT.json [--preview DIR]
"""
import argparse, json, math, os, sys
import bpy, bmesh
from mathutils import Vector

# ---- measured joint centres (model space, see Docs/IronRequiem/MOUNT_STATION_ART_20261119.md) -------------------
J = {
    "FR": {"P0": (0.126, -0.090, 0.640), "P1": (0.126, -0.090, 0.539), "P2": (0.204, -0.090, 0.235), "P3": (0.121, -0.081, 0.210)},
    "FL": {"P0": (-0.126, -0.090, 0.640), "P1": (-0.126, -0.090, 0.539), "P2": (-0.196, -0.090, 0.245), "P3": (-0.115, -0.081, 0.213)},
}
DEBUG_LOW = "--debug-low" in sys.argv
UV_FRAME_SCALE = 3.8      # model units -> metres at the station (MountStationArtBuilder.frameScale)
GRIP_AT = .72             # grip point along the claw, top (0) -> tip (1)
GROUPS = ["Static", "FR_Hanger", "FR_Upper", "FR_Fore", "FR_Claw", "FL_Hanger", "FL_Upper", "FL_Fore", "FL_Claw", "BR_Arm", "BL_Arm"]
# back arms (static, parked) and the bells beside them: scaled with the front arms about their own carriage pivot
J_BACK = {"BR": (0.130, 0.140, 0.640), "BL": (-0.130, 0.140, 0.640)}
MATS = {"IR_MS_Steel": (.16, .17, .19), "IR_MS_Paint": (.75, .36, .08), "IR_MS_Bolt": (.55, .56, .58),
        "IR_MS_Hose": (.04, .04, .045), "IR_MS_Brass": (.55, .40, .18)}


def seg_dist(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-12)))
    return (a + ab * t - p).length


def classify(c, lo, hi):
    """Group of one island from its centroid and bounds (model space): an island belongs to a front arm only when
    its whole bounding box lies in that arm's column (front half, between carriage and floor)."""
    for side, s in (("FR", 1), ("FL", -1)):
        P0, P1, P2, P3 = (Vector(J[side][k]) for k in ("P0", "P1", "P2", "P3"))
        xs = sorted((lo.x * s, hi.x * s))
        # the claw hanging under the tool tip (the part that grips)
        if xs[0] >= .06 and xs[1] <= .2 and lo.y >= -.12 and hi.y <= -.06 and lo.z >= .08 and hi.z <= .215 and abs(c.x - P3.x) < .03:
            return side + "_Claw"
        if xs[0] >= .06 and xs[1] <= .28 and lo.y >= -.012 and hi.y <= .26 and lo.z >= .06 and hi.z <= .63:
            return ("BR" if s > 0 else "BL") + "_Arm"
        if not (xs[0] >= .06 and xs[1] <= .28 and lo.y >= -.215 and hi.y <= .005 and lo.z >= .14 and hi.z <= .63):
            continue
        if seg_dist(c, P2, P3) < .042 and c.x * s < abs(P2.x) - .028:
            return side + "_Fore"
        if c.z > P1.z + .025:
            return side + "_Hanger"
        return side + "_Upper"
    return "Static"


def material_for(group, lo, hi, nverts):
    d = hi - lo; diag = d.length; dims = sorted([d.x, d.y, d.z])
    if diag < .026 and nverts < 400: return "IR_MS_Bolt"
    if dims[2] > .06 and dims[1] < .016: return "IR_MS_Hose"                 # long and thin: pipes, rods
    if group.endswith("_Fore"): return "IR_MS_Steel"
    if group != "Static": return "IR_MS_Paint"
    return "IR_MS_Steel"


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    ap = argparse.ArgumentParser(); ap.add_argument("--input", required=True); ap.add_argument("--output", required=True)
    ap.add_argument("--report", required=True); ap.add_argument("--preview"); ap.add_argument("--debug-low", action="store_true")
    a = ap.parse_args(argv)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=a.input)
    src = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    mats = {}
    for n, col in MATS.items():
        m = bpy.data.materials.new(n); m.diffuse_color = (*col, 1); mats[n] = m

    # one bmesh per group, filled island by island (world space = model space)
    out = {g: bmesh.new() for g in GROUPS}
    mat_index = {n: i for i, n in enumerate(MATS)}
    counts = {g: {"islands": 0, "verts": 0} for g in GROUPS}
    per_part = {}
    for o in src:
        me = o.data; mw = o.matrix_world
        bm = bmesh.new(); bm.from_mesh(me); bm.transform(mw); bm.faces.ensure_lookup_table()
        seen = set()
        for f0 in bm.faces:
            if f0.index in seen: continue
            stack = [f0]; island = []
            seen.add(f0.index)
            while stack:
                f = stack.pop(); island.append(f)
                for e in f.edges:
                    for g in e.link_faces:
                        if g.index not in seen: seen.add(g.index); stack.append(g)
            vs = {v for f in island for v in f.verts}
            co = [v.co for v in vs]
            lo = Vector(map(min, *co)) if len(co) > 1 else co[0].copy(); hi = Vector(map(max, *co)) if len(co) > 1 else co[0].copy()
            c = sum(co, Vector()) / len(co)
            grp = classify(c, lo, hi); mname = material_for(grp, lo, hi, len(vs))
            if DEBUG_LOW and grp == "Static" and .04 <= abs(c.x) <= .3 and -.22 <= c.y <= .01 and c.z < .26 and (hi - lo).length < .25:
                print("LOWISL", o.name, len(vs), "c=(%.3f,%.3f,%.3f)" % tuple(c), "lo=(%.3f,%.3f,%.3f)" % tuple(lo), "hi=(%.3f,%.3f,%.3f)" % tuple(hi))
            per_part.setdefault(o.name, {}).setdefault(grp, 0); per_part[o.name][grp] += len(vs)
            counts[grp]["islands"] += 1; counts[grp]["verts"] += len(vs)
            dst = out[grp]; vmap = {}
            for v in vs: vmap[v] = dst.verts.new(v.co)
            for f in island:
                try:
                    nf = dst.faces.new([vmap[v] for v in f.verts]); nf.material_index = mat_index[mname]; nf.smooth = f.smooth
                except ValueError:
                    pass                      # duplicate face inside the island: skip
        bm.free()
    for o in src: bpy.data.objects.remove(o, do_unlink=True)

    # the claw is one closed piece: split it through its own axis into two halves (filled cut) that open and close
    # as the gripper jaws; P4 = grip point, 30% down from the top of the claw, on its axis
    grip = {}
    for side in ("FR", "FL"):
        cb = out.pop(side + "_Claw")
        co = [v.co for v in cb.verts]; hi = Vector(map(max, *co))
        top_vs = [v for v in co if v.z > hi.z - .012]; top = sum(top_vs, Vector()) / len(top_vs)
        tip = min(co, key=lambda v: v.z).copy()
        axis = (tip - top).normalized()
        n = axis.cross(Vector((0, 1, 0))).normalized()          # cut plane holds the claw axis and the depth axis
        grip[side] = top + (tip - top) * GRIP_AT          # tongs: the halves pinch near the tip
        J[side]["P5"] = tuple(round(x, 4) for x in tip)
        J[side]["P6"] = tuple(round(x, 4) for x in top)
        for suffix, keep_positive in (("_ClawN", False), ("_ClawP", True)):
            h = cb.copy()
            bmesh.ops.bisect_plane(h, geom=h.verts[:] + h.edges[:] + h.faces[:], dist=1e-6, plane_co=top, plane_no=n,
                                   clear_inner=keep_positive, clear_outer=not keep_positive)
            cut = [e for e in h.edges if e.is_boundary and all(abs((v.co - top).dot(n)) < 1e-4 for v in e.verts)]
            if cut:
                filled = bmesh.ops.holes_fill(h, edges=cut, sides=0)
                for f in filled.get("faces", []): f.material_index = mat_index["IR_MS_Steel"]
            out[side + suffix] = h
        cb.free()
        J[side]["P4"] = tuple(round(x, 4) for x in grip[side])

    # frame volumes for colliders and props (min/max corners as empties): posts, lintel, whole frame
    st = [v.co for v in out["Static"].verts]
    def box(name, pts):
        if not pts: return
        lo = Vector(map(min, *pts)); hi = Vector(map(max, *pts))
        J.setdefault("Frame", {})[name + "_Min"] = tuple(round(x, 4) for x in lo)
        J["Frame"][name + "_Max"] = tuple(round(x, 4) for x in hi)
    box("PostL", [p for p in st if p.x < -.33 and p.z < .6]); box("PostR", [p for p in st if p.x > .33 and p.z < .6])
    box("Lintel", [p for p in st if p.z > .66]); box("All", st)

    made = []
    for g, bm in out.items():
        if not bm.verts: bm.free(); continue
        me = bpy.data.meshes.new(g); bm.to_mesh(me); bm.free()
        for n in MATS: me.materials.append(mats[n])
        # drop unused slots so Unity gets only what the piece uses
        used = sorted({p.material_index for p in me.polygons})
        ob = bpy.data.objects.new(g, me); bpy.context.scene.collection.objects.link(ob); made.append(ob)
        for p in me.polygons: p.use_smooth = True
        me.validate(); me.update()
        try:
            me.set_sharp_from_angle(angle=math.radians(40))
        except Exception:
            pass
    # box-projected UVs (the source has none): 1 UV unit = 1 m at the station's frame scale, so the hangar's tiling
    # plate textures read at the same density as on the rest of the set
    for ob in made:
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.cube_project(cube_size=1.0 / UV_FRAME_SCALE, correct_aspect=True, clip_to_bounds=False, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode='OBJECT')
    for side, p0 in J_BACK.items(): J.setdefault(side, {})["P0"] = p0
    empties = []
    for side, pts in J.items():
        for k, p in pts.items():
            e = bpy.data.objects.new(side + "_" + k, None); e.location = p; e.empty_display_size = .01
            bpy.context.scene.collection.objects.link(e); empties.append(e)

    if a.preview:
        render_preview(made, a.preview)

    bpy.ops.object.select_all(action='DESELECT')
    for ob in made + empties: ob.select_set(True)
    os.makedirs(os.path.dirname(a.output), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=a.output, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, object_types={'MESH', 'EMPTY'},
                             mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
                             path_mode='STRIP')
    rep = {"input": a.input, "output": a.output, "groups": counts, "joints": J,
           "materials": list(MATS), "parts_split_across_groups": {k: v for k, v in per_part.items() if len(v) > 1}}
    with open(a.report, "w") as fh: json.dump(rep, fh, indent=1)
    print("IR_MS_PREP OK", json.dumps(counts))


def render_preview(objs, out):
    import colorsys
    os.makedirs(out, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'; scene.display.shading.color_type = 'OBJECT'
    scene.display.shading.show_cavity = True
    scene.render.resolution_x = 1100; scene.render.resolution_y = 900
    w = bpy.data.worlds.new("w"); scene.world = w; w.color = (.08, .08, .09)
    col = {"Static": (.45, .47, .5), "FR_Hanger": (1, .9, .2), "FR_Upper": (1, .45, .1), "FR_Fore": (.9, .1, .1),
           "FR_ClawN": (1, 1, 1), "FR_ClawP": (.7, .7, .7), "FL_Hanger": (.4, 1, .9), "FL_Upper": (.2, .6, 1), "FL_Fore": (.6, .2, 1), "FL_ClawN": (.2, 1, .3), "FL_ClawP": (.1, .6, .2), "BR_Arm": (.9, .5, .7), "BL_Arm": (.5, .9, .7)}
    for o in objs: o.color = (*col.get(o.name, (1, 1, 1)), 1)
    cd = bpy.data.cameras.new("c"); cd.type = 'ORTHO'; cd.ortho_scale = 1.15
    cam = bpy.data.objects.new("c", cd); scene.collection.objects.link(cam); scene.camera = cam
    center = Vector((0, 0, .43))
    for name, d in {"front": Vector((0, -1, 0)), "p34": Vector((1, -1.2, .6)).normalized(), "side": Vector((1, 0, 0)),
                    "p34b": Vector((-1, -1, .3)).normalized()}.items():
        cam.location = center + d * 3; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out, "groups_" + name + ".png"); bpy.ops.render.render(write_still=True)
    # moving pieces only
    for o in objs: o.hide_render = o.name == "Static"
    for name, d in {"arms_front": Vector((0, -1, 0)), "arms_p34": Vector((1, -1.2, .6)).normalized(), "arms_side": Vector((1, 0, 0))}.items():
        cam.location = center + d * 3; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out, name + ".png"); bpy.ops.render.render(write_still=True)
    for o in objs: o.hide_render = False


if __name__ == "__main__":
    main()
