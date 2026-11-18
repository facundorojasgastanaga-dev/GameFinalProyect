"""IR-09 geometry helpers for refine_protagonist_v02.py (Blender 5.2, bmesh only, deterministic).

Coordinates are Blender world metres unless stated. No file IO here.
"""
import math
import bmesh
import bpy
from mathutils import Vector, Matrix

UV_NAME = 'UVMap'
EPS = 1e-7


# ---------------------------------------------------------------- frames
def frame_matrix(origin, x, y, z):
    m = Matrix.Identity(4)
    for i in range(3):
        m[i][0], m[i][1], m[i][2], m[i][3] = x[i], y[i], z[i], origin[i]
    return m


def orthonormal_frame(origin, z_dir, y_hint):
    z = Vector(z_dir).normalized()
    y = Vector(y_hint) - z * Vector(y_hint).dot(z)
    y.normalize()
    x = y.cross(z).normalized()
    return frame_matrix(Vector(origin), x, y, z)


def mirror_x_matrix():
    m = Matrix.Identity(4)
    m[0][0] = -1.0
    return m


def mirror_frame(fr):
    """Mirror a right-handed frame across world X and keep it right-handed (+Y/+Z mirrored, X recomputed)."""
    o = fr.translation.copy(); o.x = -o.x
    y = fr.col[1].xyz.copy(); y.x = -y.x
    z = fr.col[2].xyz.copy(); z.x = -z.x
    return frame_matrix(o, y.cross(z).normalized(), y.normalized(), z.normalized())


# ---------------------------------------------------------------- bmesh io
def bm_from_object(obj, world=True):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    if world:
        bm.transform(obj.matrix_world)
    ensure_layers(bm)
    return bm


def ensure_layers(bm):
    if UV_NAME not in bm.loops.layers.uv:
        bm.loops.layers.uv.new(UV_NAME)
    if 'ir_piece' not in bm.faces.layers.int:
        bm.faces.layers.int.new('ir_piece')
    if 'ir_kind' not in bm.faces.layers.int:
        bm.faces.layers.int.new('ir_kind')   # 0 original surface, 1 cap, 2 mechanical part
    return bm


def bm_to_mesh(bm, name):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    me.update()
    return me


def tri_count_bm(bm):
    return sum(len(f.verts) - 2 for f in bm.faces)


def tri_count_mesh(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def append_bm(dst, src, piece=None, kind=None):
    """Append geometry of src into dst keeping UV / ir_* layers and material index."""
    ensure_layers(dst); ensure_layers(src)
    uv_s = src.loops.layers.uv[UV_NAME]; uv_d = dst.loops.layers.uv[UV_NAME]
    pc_s = src.faces.layers.int['ir_piece']; pc_d = dst.faces.layers.int['ir_piece']
    kd_s = src.faces.layers.int['ir_kind']; kd_d = dst.faces.layers.int['ir_kind']
    vmap = {}
    for v in src.verts:
        vmap[v] = dst.verts.new(v.co)
    out = []
    for f in src.faces:
        try:
            nf = dst.faces.new([vmap[v] for v in f.verts])
        except ValueError:
            continue
        nf.material_index = f.material_index
        nf.smooth = f.smooth
        nf[pc_d] = f[pc_s] if piece is None else piece
        nf[kd_d] = f[kd_s] if kind is None else kind
        for ls, ld in zip(f.loops, nf.loops):
            ld[uv_d].uv = ls[uv_s].uv
        out.append(nf)
    return out


# ---------------------------------------------------------------- UV region for new metal faces
class UVRegion:
    """Square atlas region (UV units) of neutral graphite metal reused for caps / joints / collars."""

    def __init__(self, u0, v0, size, density):
        self.u0, self.v0, self.size, self.density = u0, v0, size, density   # density: UV units per metre

    def clamp(self, u, v):
        m = self.size * 0.04
        return (min(max(u, self.u0 + m), self.u0 + self.size - m),
                min(max(v, self.v0 + m), self.v0 + self.size - m))

    def planar(self, co, origin, ax_u, ax_v):
        d = co - origin
        c = self.size * 0.5
        u = self.u0 + c + d.dot(ax_u) * self.density
        v = self.v0 + c + d.dot(ax_v) * self.density
        return self.clamp(u, v)


def planar_axes(normal):
    n = Vector(normal).normalized()
    ref = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((1, 0, 0))
    au = ref.cross(n).normalized()
    av = n.cross(au).normalized()
    return au, av


def uv_planar_faces(bm, faces, region, origin=None):
    uv = bm.loops.layers.uv[UV_NAME]
    for f in faces:
        au, av = planar_axes(f.normal if f.normal.length > 0 else Vector((0, 0, 1)))
        o = origin if origin is not None else f.calc_center_median()
        for l in f.loops:
            l[uv].uv = region.planar(l.vert.co, o, au, av)


def uv_box_faces(bm, faces, region, origin):
    """Box projection by dominant normal axis; keeps texel density uniform inside the region."""
    uv = bm.loops.layers.uv[UV_NAME]
    axes = [(Vector((0, 1, 0)), Vector((0, 0, 1))), (Vector((1, 0, 0)), Vector((0, 0, 1))),
            (Vector((1, 0, 0)), Vector((0, 1, 0)))]
    for f in faces:
        n = f.normal
        k = max(range(3), key=lambda i: abs(n[i]))
        au, av = axes[k]
        for l in f.loops:
            l[uv].uv = region.planar(l.vert.co, origin, au, av)


# ---------------------------------------------------------------- triangulation / fills
def triangulate_ngons(bm, faces=None, limit=4):
    faces = [f for f in (faces if faces is not None else bm.faces) if f.is_valid and len(f.verts) > limit]
    if faces:
        bmesh.ops.triangulate(bm, faces=faces, quad_method='BEAUTY', ngon_method='BEAUTY')


def fill_planar_holes(bm, edges, normal, region, kind=1, piece=0):
    """Fill closed boundary loops lying on one plane with a beautified constrained triangulation.
    Returns the new faces (normals oriented to the mesh outside)."""
    edges = [e for e in edges if e.is_valid and e.is_boundary]
    if not edges:
        return []
    res = bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=edges, normal=Vector(normal))
    faces = [g for g in res['geom'] if isinstance(g, bmesh.types.BMFace)]
    pc = bm.faces.layers.int['ir_piece']; kd = bm.faces.layers.int['ir_kind']
    for f in faces:
        f[kd] = kind
        f[pc] = piece
        f.smooth = False
    # orient consistently with neighbours
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    if region is not None:
        uv_planar_faces(bm, faces, region)
    return faces


# ---------------------------------------------------------------- plane cuts
def plane_side(co, p, n):
    return (co - p).dot(n)


def bisect(bm, p, n, geom=None):
    geom = geom if geom is not None else bm.verts[:] + bm.edges[:] + bm.faces[:]
    res = bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=Vector(p), plane_no=Vector(n),
                                 clear_inner=False, clear_outer=False)
    return [e for e in res['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]


def cut_keep(bm, p, n, keep_positive, region=None, fill=True, piece=0):
    """Global planar cut of a closed bmesh, discard one side and cap the hole. Returns cap faces."""
    p, n = Vector(p), Vector(n).normalized()
    bisect(bm, p, n)
    kill = []
    for f in bm.faces:
        s = plane_side(f.calc_center_median(), p, n)
        if (s < 0) == keep_positive:
            kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bm.edges.ensure_lookup_table()
    if not fill:
        return []
    border = [e for e in bm.edges if e.is_boundary]
    caps = fill_planar_holes(bm, border, n if not keep_positive else -n, region, piece=piece)
    return caps


def flood_faces(seed_faces, allowed, blocked_edges):
    seen = set(); stack = list(seed_faces)
    while stack:
        f = stack.pop()
        if f in seen or f not in allowed:
            continue
        seen.add(f)
        for e in f.edges:
            if e in blocked_edges:
                continue
            for g in e.link_faces:
                if g not in seen:
                    stack.append(g)
    return seen


def local_split(bm, p, n, center, radius, seed_faces):
    """Bisect only faces within radius of center; return faces on the positive side connected to seeds
    without crossing the new cut edges (the chunk) and the cut loop edges."""
    p, n = Vector(p), Vector(n).normalized()
    faces = [f for f in bm.faces if all((v.co - center).length < radius for v in f.verts)]
    edges = {e for f in faces for e in f.edges}
    verts = {v for f in faces for v in f.verts}
    cut = bisect(bm, p, n, geom=list(verts) + list(edges) + faces)
    cut_set = set(cut)
    positive = {f for f in bm.faces if plane_side(f.calc_center_median(), p, n) > 0}
    chunk = flood_faces([f for f in seed_faces if f.is_valid], positive, cut_set)
    return chunk, cut


# ---------------------------------------------------------------- lathe (revolved) parts
def lathe(bm, frame, profile, segments, region, piece, kind=2, smooth_angle=35.0):
    """Revolve profile [(r, z), ...] around frame +Z. First and last points may have r == 0 (closed caps).
    Profile must go so that the outward normal is on the right of increasing index in (r,z) plane
    (i.e. listed from the back axis point, outward, along the outside, back to the front axis point)."""
    uv = bm.loops.layers.uv[UV_NAME]
    pc = bm.faces.layers.int['ir_piece']; kd = bm.faces.layers.int['ir_kind']
    rings = []
    for (r, z) in profile:
        if r <= 1e-9:
            rings.append([bm.verts.new(frame @ Vector((0, 0, z)))])
        else:
            ring = []
            for i in range(segments):
                a = 2 * math.pi * i / segments
                ring.append(bm.verts.new(frame @ Vector((r * math.cos(a), r * math.sin(a), z))))
            rings.append(ring)
    faces = []
    # arc length for v coordinate
    acc = [0.0]
    for a, b in zip(profile, profile[1:]):
        acc.append(acc[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
    rmax = max(r for r, _ in profile)
    circ = 2 * math.pi * rmax
    scale = min(1.0, (region.size * 0.92) / max(circ * region.density, 1e-9),
                (region.size * 0.92) / max(acc[-1] * region.density, 1e-9))
    def uvat(i, k):
        u = region.u0 + region.size * 0.04 + (i / segments) * circ * region.density * scale
        v = region.v0 + region.size * 0.04 + acc[k] * region.density * scale
        return (u, v)
    for k in range(len(rings) - 1):
        A, B = rings[k], rings[k + 1]
        for i in range(segments):
            j = (i + 1) % segments
            if len(A) == 1 and len(B) == 1:
                continue
            if len(A) == 1:
                vs = [A[0], B[j], B[i]]
                idx = [(i + 0.5, k), (i + 1, k + 1), (i, k + 1)]
            elif len(B) == 1:
                vs = [A[i], A[j], B[0]]
                idx = [(i, k), (i + 1, k), (i + 0.5, k + 1)]
            else:
                vs = [A[i], A[j], B[j], B[i]]
                idx = [(i, k), (i + 1, k), (i + 1, k + 1), (i, k + 1)]
            f = bm.faces.new(vs)
            f[pc] = piece; f[kd] = kind
            for l, (ii, kk) in zip(f.loops, idx):
                l[uv].uv = uvat(ii, kk)
            faces.append(f)
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    # smooth shading on the round walls, flat on planar rings
    for f in faces:
        f.smooth = True
    return faces


def bevelled_cylinder(bm, center, axis, radius, length, segments, chamfer, region, piece, kind=2):
    """Short chamfered cylinder (rotula / hinge pin) centred at center with axis direction."""
    fr = orthonormal_frame(center, axis, Vector((0, 0, 1)) if abs(Vector(axis).normalized().z) < 0.9 else Vector((1, 0, 0)))
    h = length * 0.5
    c = min(chamfer, radius * 0.3, h * 0.45)
    prof = [(0.0, -h), (radius - c, -h), (radius, -h + c), (radius, h - c), (radius - c, h), (0.0, h)]
    return lathe(bm, fr, prof, segments, region, piece, kind)


# ---------------------------------------------------------------- convex cells for boolean partition
def convex_cell_bm(halfspaces, center, half_size):
    """Convex polyhedron = box(center, half_size) intersected with half-spaces [(point, inward_normal)]."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2 * half_size, matrix=Matrix.Translation(center))
    ensure_layers(bm)
    for (p, n) in halfspaces:
        p, n = Vector(p), Vector(n).normalized()
        bisect(bm, p, n)
        kill = [f for f in bm.faces if plane_side(f.calc_center_median(), p, n) < -1e-9]
        if kill:
            bmesh.ops.delete(bm, geom=kill, context='FACES')
        border = [e for e in bm.edges if e.is_boundary]
        if border:
            res = bmesh.ops.edgeloop_fill(bm, edges=border)
            nf = [g for g in res['faces']]
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        if not bm.faces:
            break
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def boolean_eval(target_obj, cutter, operation, use_self=False, hole_tolerant=False, solver='EXACT'):
    """Apply a boolean (cutter = object or collection) on the evaluated mesh, return new Mesh."""
    mod = target_obj.modifiers.new('IR_bool', 'BOOLEAN')
    mod.solver = solver
    mod.operation = operation
    if solver == 'EXACT':
        mod.use_self = use_self
        mod.use_hole_tolerant = hole_tolerant
    if isinstance(cutter, bpy.types.Collection):
        mod.operand_type = 'COLLECTION'; mod.collection = cutter
    else:
        mod.operand_type = 'OBJECT'; mod.object = cutter
    try:
        mod.material_mode = 'TRANSFER'
    except Exception:
        pass
    dg = bpy.context.evaluated_depsgraph_get()
    dg.update()
    ev = target_obj.evaluated_get(dg)
    me = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=dg)
    target_obj.modifiers.remove(mod)
    return me


# ---------------------------------------------------------------- misc
def components(faces):
    faces = set(faces); out = []
    while faces:
        f0 = faces.pop(); comp = {f0}; stack = [f0]
        while stack:
            f = stack.pop()
            for e in f.edges:
                for g in e.link_faces:
                    if g in faces:
                        faces.discard(g); comp.add(g); stack.append(g)
        out.append(comp)
    return out


def mesh_health(bm):
    nm_edges = sum(1 for e in bm.edges if not e.is_manifold)
    boundary = sum(1 for e in bm.edges if e.is_boundary)
    ngons = sum(1 for f in bm.faces if len(f.verts) > 4)
    loose = sum(1 for v in bm.verts if not v.link_faces)
    zero = sum(1 for f in bm.faces if f.calc_area() < 1e-12)
    # duplicate faces by vertex set
    keys = {}
    dup = 0
    for f in bm.faces:
        k = tuple(sorted(v.index for v in f.verts))
        if k in keys:
            dup += 1
        keys[k] = 1
    return {'non_manifold_edges': nm_edges, 'boundary_edges': boundary, 'ngons_gt4': ngons,
            'loose_verts': loose, 'zero_area_faces': zero, 'duplicate_faces': dup}
