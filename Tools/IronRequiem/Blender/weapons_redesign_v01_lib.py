"""IRON REQUIEM - Weapons Redesign v01, fase B2: libreria de produccion.

Lleva los bocetos elegidos en B1 (Docs/IronRequiem/Concepts/Weapons/Redesign_v01)
a mallas de produccion sin cambiar idea ni silueta:

  piezas cerradas y biseladas -> triangulado -> bordes duros por pieza ->
  UV (Smart UV + escala media + empaquetado, densidad acotada) -> LOD1/LOD2 por
  colapso -> Weighted Normal -> horneado Cycles (BaseColor, MetalSmooth, Emission)
  -> slots finales (Surface / Heat_Emissive / Charge_Emissive) -> sockets
  Attachment/Muzzle -> FBX -> renders Eevee -> reimportacion y verificacion.

Contrato IR-ACOPLE-01 (Docs/IronRequiem/PROTAGONIST_V02_REFINE.md par. 2):
origen = Attachment = centro de la cara trasera del collar (y=0 en Blender),
disparo -Y, arriba +Z, nada con y>0, alojamiento interior diam. 0,066 con
profundidad >= 0,006, collar exterior 0,080-0,092, largo 0,032.

Solo escribe en Tools/IronRequiem/Blender/Working/WeaponsRedesign_v01 y en
Tools/IronRequiem/Exports/<Arma>_Redesign_v01. Los FBX actuales de Assets se
importan unicamente en memoria para la comparacion (no se guardan).
Ejecutar siempre:
  blender --background --factory-startup --python-exit-code 1 --python weapons_redesign_v01_<arma>.py
"""
import bpy, bmesh, math, random, json, os, time, hashlib
from pathlib import Path
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
WORK = ROOT / 'Tools/IronRequiem/Blender/Working/WeaponsRedesign_v01'
EXPORTS = ROOT / 'Tools/IronRequiem/Exports'
TEX = int(os.environ.get('IR_TEX', '1024'))  # 1024 por defecto (contrato B2)

# ------------------------------------------------------------------ contrato
COLLAR_OUT_R = 0.045       # collar exterior diam. 0,090
LIP_OUT_R = 0.0455         # labio trasero de acero (mismo radio que el collar, cambia solo el material)
SOCKET_R = 0.033           # alojamiento diam. 0,066 (radio inscrito)
SOCKET_DEPTH = 0.008       # profundidad del alojamiento (>= 0,006)
COLLAR_LEN = 0.032
COLLAR_SEG = 18

# ------------------------------------------------------------------ clases de superficie
# base lineal, rugosidad, metalico, desgaste ('paint' = pintura que deja ver acero,
# 'metal' = metal oscuro que se aclara en aristas, None), color de la mascara de emision, slot final
CLASSES = {
    'plate':    dict(base=(0.62, 0.58, 0.52), rough=0.55, metal=0.15, wear='paint'),
    'graphite': dict(base=(0.035, 0.036, 0.040), rough=0.45, metal=0.70, wear='metal'),
    'orange':   dict(base=(0.80, 0.21, 0.03), rough=0.50, metal=0.10, wear='paint'),
    'black':    dict(base=(0.006, 0.006, 0.007), rough=0.80, metal=0.0, wear=None),
    'steel':    dict(base=(0.45, 0.45, 0.47), rough=0.30, metal=1.0, wear='metal'),
    't07':      dict(base=(0.15, 0.19, 0.26), rough=0.55, metal=0.55, wear='paint'),
    't07dark':  dict(base=(0.028, 0.030, 0.034), rough=0.65, metal=0.60, wear='metal'),
    'rust':     dict(base=(0.16, 0.05, 0.02), rough=0.85, metal=0.20, wear=None),
    'cable':    dict(base=(0.012, 0.012, 0.012), rough=0.60, metal=0.0, wear=None),
    'cable_r':  dict(base=(0.30, 0.02, 0.01), rough=0.60, metal=0.0, wear=None),
    'cable_y':  dict(base=(0.55, 0.38, 0.02), rough=0.60, metal=0.0, wear=None),
    'copper':   dict(base=(0.75, 0.35, 0.15), rough=0.35, metal=1.0, wear=None),
    # emisivos
    'cyan':     dict(base=(0.03, 0.25, 0.33), rough=0.30, metal=0.0, wear=None, emit=(0.05, 0.75, 1.0)),
    'heat':     dict(base=(0.30, 0.07, 0.02), rough=0.40, metal=0.30, wear=None, emit=(1, 1, 1), slot='heat'),
    'heat_hi':  dict(base=(0.34, 0.05, 0.01), rough=0.40, metal=0.30, wear=None, emit=(1, 1, 1), slot='heat'),
    'heat_fin': dict(base=(0.45, 0.45, 0.47), rough=0.30, metal=1.0, wear=None, emit=(1, 1, 1), slot='heat'),
    'cut_hot':  dict(base=(0.35, 0.08, 0.03), rough=0.50, metal=0.60, wear=None, emit=(1, 1, 1), slot='heat'),
    'magenta':  dict(base=(0.30, 0.03, 0.35), rough=0.30, metal=0.0, wear=None, emit=(1, 1, 1), slot='charge'),
}
SLOT_NAMES = {'surface': None, 'heat': 'Heat_Emissive', 'charge': 'Charge_Emissive'}
# emision usada solo en los renders de presentacion
RENDER_EMIT = {'heat': ((1.0, 0.20, 0.02), 2.2), 'charge': ((0.85, 0.05, 1.0), 3.0)}

_rng = random.Random(7)
PARTS = []


def seed(n):
    _rng.seed(n)


def rnd(a, b):
    return _rng.uniform(a, b)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    PARTS.clear()
    _rng.seed(7)


def cls_mat(name):
    mn = 'cls_' + name
    m = bpy.data.materials.get(mn)
    if m is None:
        m = bpy.data.materials.new(mn)
    return m


# ------------------------------------------------------------------ piezas
def _finish(name, bm, cls, loc=(0, 0, 0), rot=(0, 0, 0), smooth=50, maxlod=2, step=0.0, mat_classes=None, stack=None):
    """Registra una pieza cerrada. smooth = angulo (grados) a partir del cual una arista es dura.
    maxlod = ultimo LOD que conserva la pieza. step = valor HeatStep (0..1) para calor por pasos."""
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-7)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = Euler([math.radians(a) for a in rot])
    for c in (mat_classes or [cls]):
        me.materials.append(cls_mat(c))
    PARTS.append(dict(obj=ob, cls=cls, smooth=smooth, maxlod=maxlod, step=step, stack=stack))
    return ob


def _bevel(bm, amount, segments=1):
    if amount > 0:
        bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges), offset=amount,
                        segments=segments, profile=0.5, affect='EDGES', clamp_overlap=True)


def box(name, size, loc, cls, rot=(0, 0, 0), bevel=0.004, top=(1.0, 1.0), shift=(0.0, 0.0), seg=1,
        bevel_top=False, **kw):
    """Caja biselada (como el boceto). Bisel minimo de produccion 1 mm salvo bevel=None.
    bevel_top=True bisela solo las aristas de la cara superior (emisores pequenos: 24 tris en vez de 44)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= size[0]; v.co.y *= size[1]; v.co.z *= size[2]
        if v.co.z > 0:
            v.co.x = v.co.x * top[0] + shift[0]
            v.co.y = v.co.y * top[1] + shift[1]
    if bevel is not None:
        amt = min(max(bevel, 0.001), min(size) * 0.45)
        if bevel_top:
            topf = max(bm.faces, key=lambda f: f.calc_center_median().z)
            bmesh.ops.bevel(bm, geom=list(topf.edges) + list(topf.verts), offset=amt, segments=1, profile=0.5,
                            affect='EDGES', clamp_overlap=True)
        else:
            _bevel(bm, amt, seg)
    return _finish(name, bm, cls, loc, rot, **kw)


def cyl(name, r, length, loc, cls, r2=None, seg=12, rot=(0, 0, 0), bevel=0.002, axis='Y', smooth=40, **kw):
    """Cilindro/cono a lo largo de Y: r detras (+Y), r2 delante (-Y). Bisel en las tapas."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg,
                          radius1=r, radius2=r if r2 is None else r2, depth=length)
    if axis == 'Y':
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'X'))
    elif axis == 'X':
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'Y'))
    bevel = max(bevel, 0.001) if bevel is not None else 0
    if bevel > 0:
        caps = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(60)]
        bmesh.ops.bevel(bm, geom=caps + list({v for e in caps for v in e.verts}), offset=min(bevel, length * 0.4, r * 0.4),
                        segments=1, profile=0.5, affect='EDGES', clamp_overlap=True)
    return _finish(name, bm, cls, loc, rot, smooth=smooth, **kw)


def ring(name, r_out, r_in, y0, y1, cls, seg=16, loc=(0, 0, 0), arc=None, jag=0.0,
         jag_end='y0', rot=(0, 0, 0), smooth=40, phase=0.0, **kw):
    """Tubo a lo largo de Y entre y0 (trasero) e y1 (delantero). Misma secuencia aleatoria que el boceto."""
    bm = bmesh.new()
    if arc:
        a0, a1 = [math.radians(a) for a in arc]
        angs = [a0 + (a1 - a0) * i / seg for i in range(seg + 1)]
        closed = False
    else:
        angs = [phase + math.tau * i / seg for i in range(seg)]
        closed = True
    rows = []
    for a in angs:
        ja = rnd(-jag, jag) if jag else 0.0
        jy0 = ja if jag_end in ('y0', 'both') else 0.0
        jy1 = (rnd(-jag, jag) if jag_end == 'both' else ja) if jag_end in ('y1', 'both') else 0.0
        c, s = math.cos(a), math.sin(a)
        rows.append([bm.verts.new((r_out * c, y0 + jy0, r_out * s)),
                     bm.verts.new((r_out * c, y1 + jy1, r_out * s)),
                     bm.verts.new((r_in * c, y1 + jy1, r_in * s)),
                     bm.verts.new((r_in * c, y0 + jy0, r_in * s))])
    n = len(rows)
    for i in (range(n) if closed else range(n - 1)):
        A, B = rows[i], rows[(i + 1) % n]
        for k in range(4):
            k2 = (k + 1) % 4
            bm.faces.new((A[k], A[k2], B[k2], B[k]))
    if not closed:
        for R in (rows[0], rows[-1]):
            bm.faces.new(R)
    return _finish(name, bm, cls, loc, rot, smooth=smooth, **kw)


def lathe(name, profile, seg, face_classes, smooth=50, phase=0.0, closed=False, **kw):
    """Solido de revolucion cerrado alrededor de Y. profile = [(r, y)...] de eje a eje;
    face_classes[i] = clase del tramo profile[i]->profile[i+1]."""
    bm = bmesh.new()
    cols = []
    for i in range(seg):
        a = phase + math.tau * i / seg
        c, s = math.cos(a), math.sin(a)
        cols.append([(r * c, y, r * s) for r, y in profile])
    axis_v = {}
    grid = []
    for i in range(seg):
        row = []
        for j, (r, y) in enumerate(profile):
            if r == 0:
                if j not in axis_v:
                    axis_v[j] = bm.verts.new((0, y, 0))
                row.append(axis_v[j])
            else:
                row.append(bm.verts.new(cols[i][j]))
        grid.append(row)
    classes = sorted(set(face_classes))
    npf = len(profile)
    for i in range(seg):
        A, B = grid[i], grid[(i + 1) % seg]
        for j in range(npf if closed else npf - 1):
            j2 = (j + 1) % npf
            vs = [A[j], A[j2], B[j2], B[j]]
            uniq = []
            for v in vs:
                if v not in uniq:
                    uniq.append(v)
            if len(uniq) >= 3:
                f = bm.faces.new(uniq)
                f.material_index = classes.index(face_classes[j])
    return _finish(name, bm, face_classes[0], smooth=smooth, mat_classes=classes, **kw)


def prism(name, pts, thickness, loc, cls, plane='XZ', rot=(0, 0, 0), bevel=0.0, **kw):
    """Extruye un poligono 2D (posiblemente concavo). Las tapas concavas se triangulan aqui."""
    bm = bmesh.new()

    def to3(u, v, w):
        if plane == 'XZ':
            return (u, w, v)
        if plane == 'YZ':
            return (w, u, v)
        return (u, v, w)
    a = [bm.verts.new(to3(u, v, -thickness / 2)) for u, v in pts]
    b = [bm.verts.new(to3(u, v, thickness / 2)) for u, v in pts]
    caps = [bm.faces.new(a), bm.faces.new(list(reversed(b)))]
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel:
        _bevel(bm, bevel)
    big = [f for f in bm.faces if len(f.verts) > 4]
    bmesh.ops.triangulate(bm, faces=big, quad_method='BEAUTY', ngon_method='EAR_CLIP')
    return _finish(name, bm, cls, loc, rot, **kw)


def jagged_outline(w, h, teeth=7, depth=0.012, edge='top'):
    pts = [(-w / 2, -h / 2), (w / 2, -h / 2)]
    if edge == 'top':
        pts.append((w / 2, h / 2))
        for i in range(teeth, 0, -1):
            x = -w / 2 + w * i / teeth
            pts.append((x - w / teeth * rnd(0.3, 0.7), h / 2 - rnd(0.2, 1.0) * depth))
        pts.append((-w / 2, h / 2))
    return pts


def cable(name, points, radius, cls='cable', resolution_u=5, sides_res=1, **kw):
    """Cable cerrado (tubo con tapas) a partir de una curva Bezier, convertido a malla."""
    cu = bpy.data.curves.new(name + '_crv', 'CURVE')
    cu.dimensions = '3D'
    cu.bevel_depth = radius
    cu.bevel_resolution = sides_res
    cu.resolution_u = resolution_u
    cu.use_fill_caps = True
    sp = cu.splines.new('BEZIER')
    sp.bezier_points.add(len(points) - 1)
    for bp, p in zip(sp.bezier_points, points):
        bp.co = p
        bp.handle_left_type = bp.handle_right_type = 'AUTO'
    tmp = bpy.data.objects.new(name + '_tmp', cu)
    bpy.context.collection.objects.link(tmp)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    bm = bmesh.new()
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)
    return _finish(name, bm, cls, smooth=70, **kw)


def shards(prefix, center, radius, count, y, cls='steel', size=0.012, arc=(0, 360), **kw):
    """Rebabas finas alrededor de un corte (misma secuencia aleatoria que el boceto)."""
    obs = []
    for i in range(count):
        a = math.radians(rnd(*arc))
        r = radius * rnd(0.9, 1.05)
        loc = (center[0] + r * math.cos(a), y + rnd(-0.006, 0.006), center[1] + r * math.sin(a))
        obs.append(box(f'{prefix}_{i}', (size * rnd(0.3, 0.6), size * rnd(1.0, 2.2), size * rnd(0.15, 0.3)),
                       loc, cls, rot=(rnd(-40, 40), rnd(-90, 90), math.degrees(a) + 90),
                       bevel=None, top=(0.2, 0.3), **kw))
    return obs


def collar(prefix='Collar', style='protagonist', locks=True):
    """Collar IR-ACOPLE-01 como un unico solido de revolucion cerrado:
    labio de acero (diam. 0,092, cara trasera plana en y=0), cuerpo de grafito diam. 0,090,
    alojamiento diam. 0,066 (radio inscrito) de 8 mm de profundidad con fondo negro, largo 0,032."""
    k = math.cos(math.pi / COLLAR_SEG)
    rs = SOCKET_R / k                 # radio a vertices para que el inscrito sea 0,033
    ro, rl = COLLAR_OUT_R, LIP_OUT_R  # el exterior se mide a vertices (silueta)
    b = 0.0015
    prof = [
        (0.0, -SOCKET_DEPTH),            # fondo del alojamiento (eje)
        (rs, -SOCKET_DEPTH),             # pared del alojamiento
        (rs, 0.0),
        (rl - b, 0.0),                   # cara trasera plana (labio de acero)
        (rl, -b),                        # bisel trasero 1,5 mm
        (rl, -0.005),                    # banda de acero -> cuerpo de grafito
        (ro, -COLLAR_LEN + b),
        (ro - b, -COLLAR_LEN),           # bisel frontal 1,5 mm
        (0.0, -COLLAR_LEN),              # cara frontal (oculta por el cuerpo)
    ]
    fc = ['black', 'graphite', 'steel', 'steel', 'steel', 'graphite', 'graphite', 'graphite']
    obs = [lathe(prefix, prof, COLLAR_SEG, fc, smooth=40)]
    if locks:
        for s in (-1, 1):
            obs.append(box(f'{prefix}_Lock{s}', (0.010, 0.016, 0.012), (s * 0.047, -0.013, 0), 'orange', bevel=0.002,
                           stack='collar_lock'))
    if style == 'clamp':  # abrazadera improvisada sobre pieza arrancada
        for i in range(4):
            a = math.radians(45 + 90 * i)
            obs.append(box(f'{prefix}_Bolt{i}', (0.012, 0.026, 0.012),
                           (0.051 * math.cos(a), -0.018, 0.051 * math.sin(a)), 'steel', bevel=0.002,
                           bevel_top=True, rot=(0, -math.degrees(a), 0), maxlod=1, stack='collar_bolt'))
    return obs


# ------------------------------------------------------------------ ensamblado
def _mark_sharp_and_triangulate(ob, angle_deg):
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method='BEAUTY', ngon_method='BEAUTY')
    lim = math.radians(angle_deg)
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        if not e.is_manifold:
            e.smooth = False
        else:
            e.smooth = e.calc_face_angle(0) <= lim
    # aristas entre materiales distintos tambien duras (cambio de pieza/color)
    for e in bm.edges:
        if len(e.link_faces) == 2 and e.link_faces[0].material_index != e.link_faces[1].material_index:
            e.smooth = False
    bm.to_mesh(me)
    bm.free()


def _part_stats(ob):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.transform(ob.matrix_basis)
    boundary = sum(1 for e in bm.edges if e.is_boundary)
    nonman = sum(1 for e in bm.edges if not e.is_manifold)
    vol = bm.calc_volume(signed=True)
    bm.free()
    return boundary, nonman, vol


_DIRS = [Vector(d).normalized() for d in ((0.31, 0.83, 0.47), (-0.77, 0.21, 0.6), (0.52, -0.44, -0.73))]


def _inside(tree, p):
    """Punto dentro de un solido cerrado: paridad de cortes en 3 direcciones, mayoria."""
    votes = 0
    for d in _DIRS:
        n, q = 0, p.copy()
        for _ in range(64):
            hit = tree.ray_cast(q, d)
            if hit[0] is None:
                break
            n += 1
            q = hit[0] + d * 1e-6
        votes += n % 2
    return votes >= 2


def hidden_faces():
    """Marca (atributo de cara ir_hidden) los triangulos completamente enterrados en otra pieza cerrada:
    su centro y sus esquinas (a 15 % hacia el centro, 0,3 mm hacia fuera) quedan dentro. Solo se usa
    para no gastar texeles en ellos; la geometria no se toca (las piezas siguen cerradas)."""
    bpy.context.view_layer.update()
    solids = []
    for p in PARTS:
        bm = bmesh.new(); bm.from_mesh(p['obj'].data); bm.transform(p['obj'].matrix_basis)
        closed = all(e.is_manifold for e in bm.edges)
        co = [v.co.copy() for v in bm.verts]
        mn = Vector([min(c[i] for c in co) for i in range(3)]); mx = Vector([max(c[i] for c in co) for i in range(3)])
        solids.append((BVHTree.FromBMesh(bm), mn, mx, closed))
        bm.free()
    total = 0
    for k, p in enumerate(PARTS):
        ob = p['obj']; me = ob.data; mw = ob.matrix_basis
        flags = []
        for poly in me.polygons:
            n = (mw.to_3x3() @ poly.normal).normalized()
            c = mw @ poly.center
            pts = [c + n * 0.0003] + [(mw @ me.vertices[i].co) * 0.85 + c * 0.15 + n * 0.0003 for i in poly.vertices]
            hid = False
            for j, (tree, mn, mx, closed) in enumerate(solids):
                if j == k or not closed:
                    continue
                if all(mn.x - 1e-4 <= q.x <= mx.x + 1e-4 and mn.y - 1e-4 <= q.y <= mx.y + 1e-4 and mn.z - 1e-4 <= q.z <= mx.z + 1e-4 for q in pts):
                    if all(_inside(tree, q) for q in pts):
                        hid = True
                        break
            flags.append(int(hid))
        a = me.attributes.new('ir_hidden', 'INT', 'FACE')
        a.data.foreach_set('value', flags)
        total += sum(flags)
    return total


def assemble(name):
    """Une las piezas en <name>_base: triangula, marca aristas duras por pieza, atributos
    ir_maxlod (cara) y HeatStep (color de esquina). Devuelve (objeto, informe de piezas)."""
    report = []
    for p in PARTS:
        ob = p['obj']
        b, nm, vol = _part_stats(ob)
        if vol < 0:  # normales hacia fuera siempre
            bm = bmesh.new(); bm.from_mesh(ob.data)
            bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
            bm.to_mesh(ob.data); bm.free()
            vol = -vol
        _mark_sharp_and_triangulate(ob, p['smooth'])
        report.append(dict(name=ob.name, cls=p['cls'], open_edges=b, nonmanifold=nm, volume_cm3=round(vol * 1e6, 3),
                           tris=len(ob.data.polygons)))
        me = ob.data
        a = me.attributes.new('ir_maxlod', 'INT', 'FACE')
        a.data.foreach_set('value', [p['maxlod']] * len(me.polygons))
        # pilas UV de piezas identicas: 0 = normal, 1 = maestra, 2 = copia (comparte la UV de la maestra)
        same = [q for q in PARTS if p['stack'] and q['stack'] == p['stack']]
        role = 0 if len(same) < 2 else (1 if same[0] is p else 2)
        if role == 2 and len(same[0]['obj'].data.polygons) != len(me.polygons):
            role = 0
        a = me.attributes.new('ir_stack', 'INT', 'FACE')
        a.data.foreach_set('value', [role] * len(me.polygons))
        a = me.attributes.new('ir_lidx', 'INT', 'FACE')
        a.data.foreach_set('value', list(range(len(me.polygons))))
        a = me.attributes.new('ir_part', 'INT', 'FACE')
        a.data.foreach_set('value', [PARTS.index(p)] * len(me.polygons))
        col = me.color_attributes.new('HeatStep', 'BYTE_COLOR', 'CORNER')
        col.data.foreach_set('color', [p['step'], p['step'], p['step'], 1.0] * len(me.loops))
    n_hidden = hidden_faces()
    bpy.ops.object.select_all(action='DESELECT')
    objs = [p['obj'] for p in PARTS]
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = ob.data.name = name + '_base'
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    me = ob.data
    me.color_attributes.active_color = me.color_attributes['HeatStep']
    # duplicados exactos (caras con los mismos vertices) -> deberian ser 0
    keys = set(); dup = 0
    for poly in me.polygons:
        k = tuple(sorted(tuple(round(c, 6) for c in me.vertices[i].co) for i in poly.vertices))
        if k in keys:
            dup += 1
        keys.add(k)
    return ob, dict(parts=report, duplicate_faces=dup, hidden_tris=n_hidden)


def _edit(ob):
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')


def _select_faces(ob, mask):
    """Seleccion de caras en modo edicion (caras, aristas y vertices coherentes)."""
    if ob.mode != 'EDIT':
        bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_mode(type='FACE')
    bm = bmesh.from_edit_mesh(ob.data)
    bm.faces.ensure_lookup_table()
    for v in bm.verts:
        v.select = False
    for e in bm.edges:
        e.select = False
    for f in bm.faces:
        f.select = False
    for f in bm.faces:
        if mask[f.index]:
            f.select_set(True)
    bm.select_flush_mode()
    bmesh.update_edit_mesh(ob.data)


def _uv_density(me, visible_only=False):
    uv = me.uv_layers.active.data
    hid = me.attributes.get('ir_hidden')
    hv = [d.value for d in hid.data] if hid else [0] * len(me.polygons)
    a3 = auv = 0.0
    for t in me.polygons:
        if visible_only and hv[t.index]:
            continue
        a3 += t.area
        pts = [uv[li].uv for li in t.loop_indices]
        s = 0.0
        for i in range(len(pts)):
            x1, y1 = pts[i]; x2, y2 = pts[(i + 1) % len(pts)]
            s += x1 * y2 - x2 * y1
        auv += abs(s) / 2
    return math.sqrt(auv * TEX * TEX / a3), auv, a3


def unwrap(ob, max_density=1350.0, sectors=1):
    """Smart UV (angulo 66), escala media de islas, empaquetado con rotacion y margen;
    si la densidad supera max_density px/m a 1024^2, se reduce uniformemente. sectors>1 divide el
    desplegado en sectores angulares alrededor del eje Y (las coronas planas dejan de ser islas anulares).
    Las caras enterradas van a una celda minima y las copias apiladas comparten la UV de su maestra."""
    from bpy_extras import mesh_utils
    me = ob.data
    if not me.uv_layers:
        me.uv_layers.new(name='UVMap')
    n = len(me.polygons)
    hv = np.zeros(n, np.int32); me.attributes['ir_hidden'].data.foreach_get('value', hv)
    st = np.zeros(n, np.int32); me.attributes['ir_stack'].data.foreach_get('value', st)
    pid = np.zeros(n, np.int32); me.attributes['ir_part'].data.foreach_get('value', pid)
    lid = np.zeros(n, np.int32); me.attributes['ir_lidx'].data.foreach_get('value', lid)
    packed = (hv == 0) & (st != 2)      # caras que ocupan atlas propio
    buried = (hv == 1) & (st != 2)
    cen = np.zeros(n * 3, np.float32); me.polygons.foreach_get('center', cen); cen = cen.reshape(-1, 3)
    sec = (np.floor((np.arctan2(cen[:, 2], cen[:, 0]) + math.pi) / (math.tau / sectors)).astype(int)) % sectors
    _edit(ob)
    # interiores negros (fondo del alojamiento, animas) en pasada propia: comparten proyeccion con la cara
    # trasera del collar y Smart UV los dejaba superpuestos
    mi = np.zeros(n, np.int32); me.polygons.foreach_get('material_index', mi)
    dark = np.array([me.materials[i].name == 'cls_black' for i in mi], bool)
    passes = [packed & ~dark & (sec == k) for k in range(sectors)] + [packed & dark, buried]
    for sel in passes:
        if not sel.any():
            continue
        _select_faces(ob, sel)
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004, area_weight=0.0,
                                 correct_aspect=True, scale_to_bounds=False)
    _select_faces(ob, packed)
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.pack_islands(rotate=True, rotate_method='ANY', scale=True, margin_method='FRACTION', margin=0.005,
                            shape_method='CONCAVE')
    bpy.ops.object.mode_set(mode='OBJECT')
    uv = me.uv_layers.active.data
    # caras enterradas: todas a una celda vacia de ~3 px (no se ven; se solapan solo entre ellas)
    if buried.any():
        res = 256
        cov = np.zeros((res, res), bool)
        for poly in me.polygons:
            if packed[poly.index]:
                pts = np.array([uv[li].uv for li in poly.loop_indices])
                x0, y0 = np.clip(np.floor(pts.min(0) * res).astype(int) - 2, 0, res - 1)
                x1, y1 = np.clip(np.ceil(pts.max(0) * res).astype(int) + 2, 0, res)
                cov[y0:y1, x0:x1] = True
        free = np.argwhere(~cov[1:-1, 1:-1])
        cy, cx = (free[len(free) // 2] + 1) if len(free) else (0, 0)
        hl = [li for poly in me.polygons if buried[poly.index] for li in poly.loop_indices]
        pts = np.array([uv[li].uv for li in hl])
        mn, mx = pts.min(0), pts.max(0)
        span = max(float((mx - mn).max()), 1e-9)
        for li, q in zip(hl, pts):
            uv[li].uv = ((cx + 0.2 + 0.6 * (q[0] - mn[0]) / span) / res, (cy + 0.2 + 0.6 * (q[1] - mn[1]) / span) / res)
    # copias apiladas: misma UV que la maestra (repeticion intencionada)
    masters = {}
    for poly in me.polygons:
        if st[poly.index] == 1:
            masters[(PARTS[pid[poly.index]]['stack'], int(lid[poly.index]))] = poly.index
    for poly in me.polygons:
        if st[poly.index] == 2:
            m = me.polygons[masters[(PARTS[pid[poly.index]]['stack'], int(lid[poly.index]))]]
            for a, b in zip(poly.loop_indices, m.loop_indices):
                uv[a].uv = uv[b].uv
    d, auv, a3 = _uv_density(me, visible_only=True)
    scaled = 1.0
    if d > max_density:
        scaled = max_density / d
        uv = me.uv_layers.active.data
        arr = np.empty(len(uv) * 2, np.float32)
        uv.foreach_get('uv', arr)
        uv.foreach_set('uv', arr * scaled)
        d, auv, a3 = _uv_density(me, visible_only=True)
    dall, fill, aall = _uv_density(me)
    return dict(texel_px_per_m=round(d, 1), texel_note='densidad de las caras visibles (las enterradas usan 8 %)',
                uv_fill=round(fill, 4), area_visible_m2=round(a3, 5), area_total_m2=round(aall, 5),
                hidden_tris=int(hv.sum()), stacked_copy_tris=int((st == 2).sum()),
                stacked_groups=sorted({PARTS[i]['stack'] for i in set(pid[st == 2].tolist())}),
                uv_downscale=round(scaled, 4))


def uv_overlap(ob, res=512):
    """Fraccion de pixeles UV cubiertos por mas de un triangulo (rasterizado por centros), contando solo
    las caras con atlas propio (excluye copias apiladas intencionadas y caras enterradas)."""
    me = ob.data
    me.calc_loop_triangles()
    uv = me.uv_layers.active.data
    n = len(me.polygons)
    hv = np.zeros(n, np.int32); me.attributes['ir_hidden'].data.foreach_get('value', hv)
    st = np.zeros(n, np.int32); me.attributes['ir_stack'].data.foreach_get('value', st)
    cnt = np.zeros((res, res), np.uint8)
    for t in me.loop_triangles:
        if hv[t.polygon_index] or st[t.polygon_index] == 2:
            continue
        p = np.array([uv[li].uv for li in t.loops]) * res
        x0, y0 = np.floor(p.min(0)).astype(int); x1, y1 = np.ceil(p.max(0)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, res), min(y1, res)
        if x1 <= x0 or y1 <= y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
        (ax, ay), (bx, by), (cx, cy) = p
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-12:
            continue
        l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / den
        l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / den
        l3 = 1 - l1 - l2
        inside = (l1 > 1e-4) & (l2 > 1e-4) & (l3 > 1e-4)
        cnt[y0:y1, x0:x1] += inside.astype(np.uint8)
    covered = int((cnt > 0).sum())
    over = int((cnt > 1).sum())
    return dict(covered_px_512=covered, overlap_px_512=over, overlap_pct=round(100.0 * over / max(covered, 1), 3))


def tris(ob):
    ob.data.calc_loop_triangles()
    return len(ob.data.loop_triangles)


def make_lod(base, name, level, target_tris, symmetry=True):
    """Copia de base sin piezas con ir_maxlod < level y colapso iterativo hasta target_tris."""
    me = base.data.copy()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    me.name = name
    bm = bmesh.new()
    bm.from_mesh(me)
    lay = bm.faces.layers.int.get('ir_maxlod')
    kill = [f for f in bm.faces if f[lay] < level]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(me)
    bm.free()
    keep = me.copy()
    ratio = target_tris / max(tris(ob), 1)
    for _ in range(6):
        ob.data = keep.copy()
        if ratio < 0.999:
            m = ob.modifiers.new('dec', 'DECIMATE')
            m.decimate_type = 'COLLAPSE'
            m.ratio = min(ratio, 1.0)
            m.use_collapse_triangulate = True
            m.use_symmetry = symmetry
            m.symmetry_axis = 'X'
            bpy.ops.object.select_all(action='DESELECT')
            bpy.context.view_layer.objects.active = ob
            ob.select_set(True)
            bpy.ops.object.modifier_apply(modifier=m.name)
        t = tris(ob)
        if abs(t - target_tris) / target_tris < 0.03:
            break
        ratio *= target_tris / max(t, 1)
    ob.data.name = name
    return ob


def weighted_normals(ob):
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    # tras el colapso las aristas nuevas heredan flags; re-marcar duras por angulo (45)
    bm = bmesh.new(); bm.from_mesh(ob.data)
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        if not e.is_manifold:
            e.smooth = False
        elif len(e.link_faces) == 2 and e.link_faces[0].material_index != e.link_faces[1].material_index:
            e.smooth = False
    bm.to_mesh(ob.data); bm.free()
    m = ob.modifiers.new('wn', 'WEIGHTED_NORMAL')
    m.mode = 'FACE_AREA'
    m.weight = 50
    m.keep_sharp = True
    m.thresh = 0.01
    bpy.ops.object.modifier_apply(modifier=m.name)


# ------------------------------------------------------------------ horneado
def _n(nt, kind, loc=(0, 0), **props):
    nd = nt.nodes.new(kind)
    nd.location = loc
    for k, v in props.items():
        setattr(nd, k, v)
    return nd


def _math(nt, op, a, b=None, clamp=False):
    nd = nt.nodes.new('ShaderNodeMath')
    nd.operation = op
    nd.use_clamp = clamp
    for i, v in enumerate((a, b)):
        if v is None:
            continue
        if isinstance(v, (int, float)):
            nd.inputs[i].default_value = v
        else:
            nt.links.new(v, nd.inputs[i])
    return nd.outputs[0]


def _mix(nt, fac, a, b):
    nd = nt.nodes.new('ShaderNodeMix')
    nd.data_type = 'RGBA'
    nt.links.new(fac, nd.inputs['Factor'])
    for sock, v in ((nd.inputs[6], a), (nd.inputs[7], b)):
        if isinstance(v, tuple):
            sock.default_value = (*v, 1)
        else:
            nt.links.new(v, sock)
    return nd.outputs[2]


def build_bake_material(m, cname, overrides):
    """Arbol de horneado para una clase: salidas base / metal / smooth / emit en nodos Emission."""
    c = dict(CLASSES[cname]); c.update(overrides.get(cname, {}))
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = _n(nt, 'ShaderNodeOutputMaterial', (900, 0))
    em = _n(nt, 'ShaderNodeEmission', (700, 0))
    nt.links.new(em.outputs[0], out.inputs[0])
    tc = _n(nt, 'ShaderNodeTexCoord')
    ao = _n(nt, 'ShaderNodeAmbientOcclusion', samples=16, only_local=True)
    ao.inputs['Distance'].default_value = 0.025
    bev = _n(nt, 'ShaderNodeBevel', samples=12)
    bev.inputs['Radius'].default_value = 0.0025
    geo = _n(nt, 'ShaderNodeNewGeometry')
    dot = nt.nodes.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'
    nt.links.new(bev.outputs[0], dot.inputs[0]); nt.links.new(geo.outputs['Normal'], dot.inputs[1])
    edge = _math(nt, 'MULTIPLY', _math(nt, 'SUBTRACT', 1.0, dot.outputs['Value']), 16.0, clamp=True)
    nz = _n(nt, 'ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 140.0; nz.inputs['Detail'].default_value = 8.0
    nt.links.new(tc.outputs['Object'], nz.inputs['Vector'])
    grime = _n(nt, 'ShaderNodeTexNoise'); grime.inputs['Scale'].default_value = 18.0; grime.inputs['Detail'].default_value = 5.0
    nt.links.new(tc.outputs['Object'], grime.inputs['Vector'])
    # desgaste = arista * ruido de rayado
    wear = _math(nt, 'MULTIPLY', edge, _math(nt, 'MULTIPLY', _math(nt, 'SUBTRACT', nz.outputs['Fac'], 0.25), 3.0, clamp=True), clamp=True)
    cav = _math(nt, 'ADD', 0.5, _math(nt, 'MULTIPLY', ao.outputs['AO'], 0.5))
    gfac = _math(nt, 'ADD', 0.70, _math(nt, 'MULTIPLY', grime.outputs['Fac'], 0.45))
    base = c['base']
    if c['wear'] == 'paint':
        exposed = (0.52, 0.52, 0.53)
        col = _mix(nt, wear, base, exposed)
        metal = _math(nt, 'ADD', c['metal'], _math(nt, 'MULTIPLY', wear, 1.0 - c['metal']), clamp=True)
        rough = _math(nt, 'SUBTRACT', c['rough'], _math(nt, 'MULTIPLY', wear, c['rough'] - 0.32), clamp=True)
    elif c['wear'] == 'metal':
        light = tuple(min(1.0, v + 0.22) for v in base)
        col = _mix(nt, _math(nt, 'MULTIPLY', wear, 0.8), base, light)
        metal = _math(nt, 'ADD', c['metal'], 0.0)
        rough = _math(nt, 'SUBTRACT', c['rough'], _math(nt, 'MULTIPLY', wear, 0.15), clamp=True)
    else:
        col = _mix(nt, _math(nt, 'MULTIPLY', edge, 0.0), base, base)
        metal = _math(nt, 'ADD', c['metal'], 0.0)
        rough = _math(nt, 'ADD', c['rough'], 0.0)
    # cavidad y suciedad
    shade = _math(nt, 'MULTIPLY', cav, gfac)
    colm = nt.nodes.new('ShaderNodeVectorMath'); colm.operation = 'SCALE'
    nt.links.new(col, colm.inputs[0]); nt.links.new(shade, colm.inputs['Scale'])
    rough = _math(nt, 'ADD', rough, _math(nt, 'MULTIPLY', _math(nt, 'SUBTRACT', grime.outputs['Fac'], 0.5), 0.12), clamp=True)
    smooth = _math(nt, 'SUBTRACT', 1.0, rough, clamp=True)
    emitc = c.get('emit', (0, 0, 0))
    m['_outs'] = 0
    return dict(em=em, base=colm.outputs[0], metal=metal, smooth=smooth, emit=emitc, nt=nt)


def bake(ob, out_dir, prefix, overrides=None, samples=8):
    """Hornea BaseColor (sRGB), Metallic y Smoothness (lineales) y la mascara de emision
    sobre ob (LOD0 con materiales de clase). Devuelve rutas y tiempos."""
    overrides = overrides or {}
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    sc.cycles.use_denoising = False
    sc.render.bake.margin = 8
    sc.render.bake.margin_type = 'EXTEND'
    sc.render.bake.use_clear = True
    trees = {}
    for slot in ob.data.materials:
        cname = slot.name[4:]
        trees[slot.name] = build_bake_material(slot, cname, overrides)
    imgs = {}
    for key, cs in (('base', 'sRGB'), ('metal', 'Non-Color'), ('smooth', 'Non-Color'), ('emit', 'sRGB')):
        img = bpy.data.images.new(f'{prefix}_{key}', TEX, TEX, alpha=False, float_buffer=(cs == 'Non-Color'))
        img.colorspace_settings.name = cs
        imgs[key] = img
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    # copias apiladas fuera del tile 0-1 durante el horneado (solo hornea la maestra)
    me = ob.data
    st = np.zeros(len(me.polygons), np.int32); me.attributes['ir_stack'].data.foreach_get('value', st)
    uvd = me.uv_layers.active.data
    shift = [li for poly in me.polygons if st[poly.index] == 2 for li in poly.loop_indices]
    for li in shift:
        uvd[li].uv.x += 2.0
    times = {}
    for key in ('base', 'metal', 'smooth', 'emit'):
        for mname, t in trees.items():
            nt = t['nt']
            for l in list(t['em'].inputs[0].links):
                nt.links.remove(l)
            if key == 'emit':
                t['em'].inputs[0].default_value = (*t['emit'], 1)
            elif key in ('metal', 'smooth'):
                nt.links.new(t[key], t['em'].inputs[0])
            else:
                nt.links.new(t['base'], t['em'].inputs[0])
            tex = nt.nodes.get('BAKE_TARGET') or nt.nodes.new('ShaderNodeTexImage')
            tex.name = 'BAKE_TARGET'
            tex.image = imgs[key]
            nt.nodes.active = tex
        sc.cycles.samples = samples if key != 'emit' else 1
        t0 = time.time()
        bpy.ops.object.bake(type='EMIT', margin=8, use_clear=True)
        times[key] = round(time.time() - t0, 1)
    for li in shift:
        uvd[li].uv.x -= 2.0
    paths = {}

    def save(img, path):
        img.filepath_raw = str(path)
        img.file_format = 'PNG'
        img.save()
        return str(path)
    # BaseColor y Emission tal cual (8 bits sRGB)
    for key, suffix in (('base', 'BaseColor'), ('emit', 'Emission')):
        out = bpy.data.images.new(f'{prefix}_{suffix}', TEX, TEX, alpha=False)
        px = np.empty(TEX * TEX * 4, np.float32)
        imgs[key].pixels.foreach_get(px)
        out.pixels.foreach_set(px)
        paths[suffix] = save(out, Path(out_dir) / f'{prefix}_{suffix}.png')
    # MetalSmooth URP/Lit: metalico en R (y G/B), smoothness en A
    pm = np.empty(TEX * TEX * 4, np.float32); imgs['metal'].pixels.foreach_get(pm)
    ps = np.empty(TEX * TEX * 4, np.float32); imgs['smooth'].pixels.foreach_get(ps)
    pm = pm.reshape(-1, 4); ps = ps.reshape(-1, 4)
    ms = np.stack([pm[:, 0], pm[:, 0], pm[:, 0], ps[:, 0]], 1).clip(0, 1).astype(np.float32)
    out = bpy.data.images.new(f'{prefix}_MetalSmooth', TEX, TEX, alpha=True)
    out.colorspace_settings.name = 'Non-Color'
    out.alpha_mode = 'STRAIGHT'
    out.pixels.foreach_set(ms.ravel())
    paths['MetalSmooth'] = save(out, Path(out_dir) / f'{prefix}_MetalSmooth.png')
    stats = dict(metal_mean=round(float(pm[:, 0].mean()), 3), smooth_mean=round(float(ps[:, 0].mean()), 3),
                 smooth_range=[round(float(ps[:, 0].min()), 3), round(float(ps[:, 0].max()), 3)])
    return paths, times, stats


# ------------------------------------------------------------------ materiales finales
def final_materials(objs, weapon, paths, render_heat_level=1.0):
    """Remapea las clases a slots finales: 0 = <weapon>_Surface, Heat_Emissive, Charge_Emissive (si existen).
    Los tres comparten el mismo atlas y UV; difieren solo en la emision que Unity tinta por slot."""
    used = set()
    for ob in objs:
        for m in ob.data.materials:
            used.add(CLASSES[m.name[4:]].get('slot', 'surface'))
    order = ['surface'] + [s for s in ('heat', 'charge') if s in used]
    names = [f'{weapon}_Surface' if s == 'surface' else SLOT_NAMES[s] for s in order]
    imgs = {k: bpy.data.images.load(v, check_existing=True) for k, v in paths.items()}
    imgs['MetalSmooth'].colorspace_settings.name = 'Non-Color'
    imgs['MetalSmooth'].alpha_mode = 'STRAIGHT'
    mats = []
    for s, nm in zip(order, names):
        m = bpy.data.materials.get(nm) or bpy.data.materials.new(nm)
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        out = _n(nt, 'ShaderNodeOutputMaterial', (900, 0))
        bs = _n(nt, 'ShaderNodeBsdfPrincipled', (600, 0))
        nt.links.new(bs.outputs[0], out.inputs[0])
        tb = _n(nt, 'ShaderNodeTexImage', (0, 200)); tb.image = imgs['BaseColor']
        tm = _n(nt, 'ShaderNodeTexImage', (0, -100)); tm.image = imgs['MetalSmooth']
        te = _n(nt, 'ShaderNodeTexImage', (0, -400)); te.image = imgs['Emission']
        nt.links.new(tb.outputs[0], bs.inputs['Base Color'])
        sep = nt.nodes.new('ShaderNodeSeparateColor')
        nt.links.new(tm.outputs[0], sep.inputs[0])
        nt.links.new(sep.outputs[0], bs.inputs['Metallic'])
        nt.links.new(_math(nt, 'SUBTRACT', 1.0, tm.outputs['Alpha']), bs.inputs['Roughness'])
        if s == 'surface':
            nt.links.new(te.outputs[0], bs.inputs['Emission Color'])
            bs.inputs['Emission Strength'].default_value = 2.0
        else:
            col, strength = RENDER_EMIT[s]
            # presentacion: HeatStep (R) <= nivel -> encendido (barra de calor por pasos)
            attr = nt.nodes.new('ShaderNodeVertexColor'); attr.layer_name = 'HeatStep'
            on = _math(nt, 'LESS_THAN', _math(nt, 'SUBTRACT', attr.outputs['Color'], 0.001), render_heat_level)
            mixc = nt.nodes.new('ShaderNodeVectorMath'); mixc.operation = 'MULTIPLY'
            nt.links.new(te.outputs[0], mixc.inputs[0]); mixc.inputs[1].default_value = col
            nt.links.new(mixc.outputs[0], bs.inputs['Emission Color'])
            nt.links.new(_math(nt, 'MULTIPLY', on, strength), bs.inputs['Emission Strength'])
        mats.append(m)
    for ob in objs:
        me = ob.data
        old = [CLASSES[m.name[4:]].get('slot', 'surface') for m in me.materials]
        idx = np.empty(len(me.polygons), np.int32)
        me.polygons.foreach_get('material_index', idx)
        remap = np.array([order.index(s) for s in old], np.int32)
        new = remap[idx]
        me.materials.clear()
        for m in mats:
            me.materials.append(m)
        me.polygons.foreach_set('material_index', new)
        me.update()
    return names


def slot_tris(ob):
    me = ob.data
    me.calc_loop_triangles()
    out = {m.name: 0 for m in me.materials}
    for t in me.loop_triangles:
        out[me.materials[t.material_index].name] += 1
    return out


def add_sockets(L):
    socks = []
    for nm, loc in (('Attachment', (0, 0, 0)), ('Muzzle', (0, -L, 0))):
        e = bpy.data.objects.new(nm, None)
        e.empty_display_type = 'ARROWS'
        e.empty_display_size = 0.03
        e.location = loc
        bpy.context.collection.objects.link(e)
        socks.append(e)
    return socks


def export_fbx(objs, path):
    bpy.ops.object.mode_set(mode='OBJECT') if bpy.context.object and bpy.context.object.mode != 'OBJECT' else None
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH', 'EMPTY'},
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, add_leaf_bones=False,
                             bake_anim=False, mesh_smooth_type='OFF', use_mesh_modifiers=True,
                             colors_type='LINEAR', path_mode='RELATIVE', use_custom_props=False)


# ------------------------------------------------------------------ render
def _engine(scene):
    for e in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
        try:
            scene.render.engine = e
            return e
        except TypeError:
            continue
    scene.render.engine = 'BLENDER_WORKBENCH'
    return 'BLENDER_WORKBENCH'


def setup_stage(bg=0.07):
    sc = bpy.context.scene
    eng = _engine(sc)
    try:
        sc.eevee.taa_render_samples = 48
    except Exception:
        pass
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('World')
    w.use_nodes = True
    bgn = w.node_tree.nodes.get('Background')
    bgn.inputs['Color'].default_value = (bg, bg, bg * 1.04, 1)
    bgn.inputs['Strength'].default_value = 1.0
    sc.world = w

    def light(name, energy, rot, color=(1, 1, 1)):
        ld = bpy.data.lights.new(name, 'SUN')
        ld.energy = energy
        ld.color = color
        ld.angle = math.radians(8)
        o = bpy.data.objects.new(name, ld)
        sc.collection.objects.link(o)
        o.rotation_euler = Euler([math.radians(a) for a in rot])
    light('Key', 2.6, (50, 0, 145))
    light('Fill', 0.8, (60, 0, -60), (0.75, 0.85, 1.0))
    light('Rim', 2.5, (110, 0, 20), (1.0, 0.9, 0.8))
    return eng


def _cam(name, loc, target, ortho=None, fov=None):
    cd = bpy.data.cameras.new(name)
    if ortho:
        cd.type = 'ORTHO'
        cd.ortho_scale = ortho
    else:
        cd.sensor_fit = 'HORIZONTAL'
        cd.angle = math.radians(fov or 50)
    cd.clip_start = 0.01
    o = bpy.data.objects.new(name, cd)
    bpy.context.scene.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return o


def _label(text, loc, size, color=(1.0, 0.45, 0.08), rot=(90, 0, 90)):
    cu = bpy.data.curves.new('lbl', 'FONT')
    cu.body = text
    cu.size = size
    o = bpy.data.objects.new('lbl_' + text[:8], cu)
    bpy.context.scene.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = Euler([math.radians(a) for a in rot])
    m = bpy.data.materials.new('lbl')
    m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    e = _n(nt, 'ShaderNodeEmission'); e.inputs[0].default_value = (*color, 1); e.inputs[1].default_value = 1.0
    out = _n(nt, 'ShaderNodeOutputMaterial'); nt.links.new(e.outputs[0], out.inputs[0])
    cu.materials.append(m)
    return o


def render_views(out_dir, prefix, L, lod0):
    """3/4 frontal (lado -X), perfil desde +X (lado visto en FPS) y vista FPS:
    camara 0,35 m detras y 0,12 m sobre el acople, FOV 60 horizontal, arma a la derecha."""
    sc = bpy.context.scene
    c = Vector((0, -L * 0.5, 0.0))
    files = {}
    sc.render.resolution_x, sc.render.resolution_y = 1200, 800
    d = Vector((-0.95, -1.15, 0.70)).normalized()
    sc.camera = _cam('Cam34', c + d * 3.0, c, ortho=L * 1.45)
    sc.render.filepath = files['34'] = str(Path(out_dir) / f'{prefix}_render_34.png')
    bpy.ops.render.render(write_still=True)
    sc.camera = _cam('CamSide', c + Vector((3.0, 0, 0.0)), c, ortho=L * 1.45)
    sc.render.filepath = files['perfil'] = str(Path(out_dir) / f'{prefix}_render_perfil.png')
    bpy.ops.render.render(write_still=True)
    sc.render.resolution_x, sc.render.resolution_y = 1200, 675
    sc.camera = _cam('CamFPS', Vector((0.13, 0.35, 0.12)), Vector((0.05, -0.45, -0.05)), fov=60)
    sc.render.filepath = files['fps'] = str(Path(out_dir) / f'{prefix}_render_fps.png')
    bpy.ops.render.render(write_still=True)
    return files


def render_compare(out_dir, prefix, L, new_objs, current_fbx, hide_patterns=('LOD1', 'LOD2'), cur_label='ACTUAL'):
    """Perfil ortografico (+X): rediseno arriba, arma actual (FBX de Assets importado solo en memoria) debajo."""
    sc = bpy.context.scene
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(current_fbx))
    imported = [o for o in bpy.data.objects if o not in before]
    for o in imported:
        if any(p in o.name for p in hide_patterns):
            o.hide_render = True
    # altura del nuevo
    zs = []
    for o in new_objs:
        zs += [(o.matrix_world @ v.co).z for v in o.data.vertices]
    h_new = max(zs) - min(zs)
    dz = -(h_new + 0.06 + (0.10 if L > 0.5 else 0.03))
    for o in imported:
        if o.parent is None:
            o.location.z += dz
    bpy.context.view_layer.update()
    zc = []
    for o in imported:
        if o.type == 'MESH' and not o.hide_render:
            zc += [(o.matrix_world @ Vector(b)).z for b in o.bound_box]
    zmid = (max(zs) + min(zc)) / 2
    _label('REDISENO v01', (0.2, -L, max(zs) + 0.012), L * 0.05)
    _label(cur_label, (0.2, -L, max(zc) + 0.012), L * 0.05, color=(0.8, 0.8, 0.8))
    sc.render.resolution_x, sc.render.resolution_y = 1200, 900
    span = max(L * 1.35, (max(zs) - min(zc)) * 1.45 * 1200 / 900)
    sc.camera = _cam('CamCmp', Vector((3.0, -L * 0.5, zmid)), Vector((0, -L * 0.5, zmid)), ortho=span)
    sc.render.filepath = path = str(Path(out_dir) / f'{prefix}_render_comparacion.png')
    bpy.ops.render.render(write_still=True)
    for o in imported:
        bpy.data.objects.remove(o, do_unlink=True)
    return path


# ------------------------------------------------------------------ verificacion
def verify_fbx(path, L, lod_names, slot_names):
    """Reimporta el FBX en una escena vacia y comprueba el contrato."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), axis_forward='-Z', axis_up='Y')
    res = dict(file=os.path.basename(str(path)), checks={})
    obs = {o.name: o for o in bpy.data.objects}
    res['nodes'] = sorted(obs)
    att, muz = obs.get('Attachment'), obs.get('Muzzle')
    res['attachment_world'] = [round(v, 5) for v in att.matrix_world.translation] if att else None
    res['muzzle_world'] = [round(v, 5) for v in muz.matrix_world.translation] if muz else None
    ck = res['checks']
    ck['unique_attachment'] = sum(1 for n in obs if n.split('.')[0] == 'Attachment') == 1 and 'WeaponAttachment' not in obs
    ck['attachment_at_origin'] = bool(att) and att.matrix_world.translation.length < 1e-5
    ck['muzzle_on_axis'] = bool(muz) and abs(muz.matrix_world.translation.x) < 1e-5 and abs(muz.matrix_world.translation.z) < 1e-5
    ck['muzzle_length'] = round(-muz.matrix_world.translation.y, 5) if muz else None
    lods = {}
    ymax = -1
    for nm in lod_names:
        o = obs.get(nm)
        if not o:
            lods[nm] = None
            continue
        me = o.data
        me.calc_loop_triangles()
        mw = o.matrix_world
        co = np.array([tuple(mw @ v.co) for v in me.vertices])
        ymax = max(ymax, float(co[:, 1].max()))
        lods[nm] = dict(tris=len(me.loop_triangles), polys=len(me.polygons),
                        all_triangles=all(len(p.vertices) == 3 for p in me.polygons),
                        materials=[m.name for m in me.materials],
                        bounds_min=[round(float(v), 4) for v in co.min(0)], bounds_max=[round(float(v), 4) for v in co.max(0)],
                        color_attributes=[a.name for a in me.color_attributes],
                        uv_layers=[u.name for u in me.uv_layers])
    res['lods'] = lods
    ck['nothing_behind_attachment'] = ymax <= 1e-5
    res['max_y_all_lods'] = round(ymax, 6)
    ck['all_triangulated'] = all(v and v['all_triangles'] for v in lods.values())
    ck['slot_names'] = all(v and v['materials'] == slot_names for v in lods.values())
    # LOD0: boca, alojamiento y collar
    o = obs[lod_names[0]]
    me = o.data
    mw = o.matrix_world
    bm = bmesh.new(); bm.from_mesh(me); bm.transform(mw); bm.normal_update()
    tree = BVHTree.FromBMesh(bm)
    mouth = [f for f in bm.faces if abs(f.calc_center_median().y + L) < 0.0025 and f.normal.y < -0.99]
    ck['muzzle_face_normal_minus_y'] = len(mouth) > 0
    res['muzzle_faces'] = len(mouth)
    # profundidad del alojamiento: rayos desde detras hacia -Y
    depths = []
    for r in (0.0, 0.015, 0.025, 0.030):
        for a in range(0, 360, 45):
            orig = Vector((r * math.cos(math.radians(a)), 0.02, r * math.sin(math.radians(a))))
            hit = tree.ray_cast(orig, Vector((0, -1, 0)))
            depths.append(-hit[0].y if hit[0] else 99)
    res['socket_depth_min'] = round(min(depths), 5)
    ck['socket_depth_ge_6mm'] = min(depths) >= 0.006 - 1e-5
    # diametro del alojamiento: rayos radiales a 4 mm de profundidad
    rr = []
    for a in range(0, 360, 5):
        dvec = Vector((math.cos(math.radians(a)), 0, math.sin(math.radians(a))))
        hit = tree.ray_cast(Vector((0, -0.004, 0)), dvec)
        rr.append((hit[0] - Vector((0, -0.004, 0))).length if hit[0] else 99)
    res['socket_diameter_min'] = round(2 * min(rr), 5)
    res['socket_diameter_max'] = round(2 * max(rr), 5)
    ck['socket_diameter_066'] = abs(2 * min(rr) - 0.066) < 0.0006
    # collar exterior: rayos desde fuera hacia el eje a lo largo del collar, evitando cierres (0/180) y pernos (45+90k)
    rad = []
    for y in (-0.002, -0.010, -0.018, -0.026):
        for a in (20, 70, 110, 160, 200, 250, 290, 340):
            dvec = Vector((math.cos(math.radians(a)), 0, math.sin(math.radians(a))))
            hit = tree.ray_cast(Vector((0, y, 0)) + dvec * 0.3, -dvec)
            if hit[0]:
                rad.append(math.hypot(hit[0].x, hit[0].z))
    res['collar_outer_diameter'] = round(2 * max(rad), 4) if rad else None
    res['collar_outer_diameter_min'] = round(2 * min(rad), 4) if rad else None
    ck['collar_080_092'] = bool(rad) and 0.080 - 1e-4 <= 2 * min(rad) and 2 * max(rad) <= 0.092 + 1e-4
    back = [f for f in bm.faces if abs(f.calc_center_median().y) < 1e-5 and f.normal.y > 0.99]
    ck['flat_back_face_y0'] = len(back) > 0
    bm.free()
    res['all_pass'] = all(v for k, v in ck.items() if isinstance(v, bool))
    return res


def sha256(p):
    h = hashlib.sha256()
    with open(p, 'rb') as f:
        h.update(f.read())
    return h.hexdigest()


# ------------------------------------------------------------------ pipeline
def run(weapon, folder, L, build, budget, current_fbx, symmetric=True, overrides=None, seed_id=None,
        render_heat_level=1.0, notes=None, extra=None, uv_sectors=1):
    t_start = time.time()
    out_dir = EXPORTS / folder
    out_dir.mkdir(parents=True, exist_ok=True)
    WORK.mkdir(parents=True, exist_ok=True)
    reset_scene()
    if seed_id:
        seed(sum(map(ord, seed_id)))
    build()
    n_parts = len(PARTS)
    base, topo = assemble(weapon)
    if os.environ.get('IR_COUNT'):
        for p in sorted(topo['parts'], key=lambda p: -p['tris']):
            print('[IR-COUNT]', p['name'], p['tris'], 'open' if p['open_edges'] else '')
        print('[IR-COUNT] TOTAL', tris(base), 'budget', budget, 'parts', n_parts, 'dup', topo['duplicate_faces'],
              'hidden', topo['hidden_tris'])
        if os.environ.get('IR_COUNT') == 'uv':
            print('[IR-COUNT] UV', unwrap(base, sectors=int(os.environ.get('IR_SECTORS', uv_sectors))), uv_overlap(base))
        return None
    uv_sectors = int(os.environ.get('IR_SECTORS', uv_sectors))
    uvinfo = unwrap(base, sectors=uv_sectors)
    uvinfo.update(uv_overlap(base))
    t0 = tris(base)
    assert t0 <= budget, f'{weapon}: LOD0 {t0} tris > presupuesto {budget}'
    lod0 = base
    lod1 = make_lod(base, f'{weapon}_LOD1', 1, int(round(t0 * 0.5)), symmetry=symmetric)
    lod2 = make_lod(base, f'{weapon}_LOD2', 2, int(round(t0 * 0.25)), symmetry=symmetric)
    lod0.name = lod0.data.name = f'{weapon}_LOD0'
    lods = [lod0, lod1, lod2]
    for o in lods:
        weighted_normals(o)
    paths, bake_times, bake_stats = bake(lod0, out_dir, weapon, overrides=overrides)
    slots = final_materials(lods, weapon, paths, render_heat_level)
    socks = add_sockets(L)
    # comprobaciones de tris
    lt = [tris(o) for o in lods]
    ratios = [round(t / lt[0], 3) for t in lt]
    assert 0.40 <= ratios[1] <= 0.60 and 0.18 <= ratios[2] <= 0.32, ratios
    blend = WORK / f'{weapon}.blend'
    for o in lods:
        o.data.materials  # noqa
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), check_existing=False, copy=True)
    fbx = out_dir / f'{weapon}.fbx'
    export_fbx(lods + socks, fbx)
    # renders: solo LOD0
    for o in (lod1, lod2):
        o.hide_render = True
    for o in bpy.data.objects:
        if o.type == 'MESH' and o not in lods:
            o.hide_render = True
    eng = setup_stage()
    renders = render_views(out_dir, weapon, L, lod0)
    renders['comparacion'] = render_compare(out_dir, weapon, L, [lod0], current_fbx)
    slot_tri = slot_tris(lod0)
    dims = [round(v, 4) for v in lod0.dimensions]
    mn = [round(min((v.co[i]) for v in lod0.data.vertices), 4) for i in range(3)]
    mx = [round(max((v.co[i]) for v in lod0.data.vertices), 4) for i in range(3)]
    ver = verify_fbx(fbx, L, [f'{weapon}_LOD{i}' for i in range(3)], slots)
    metrics = dict(
        weapon=weapon, folder=str(out_dir.relative_to(ROOT)).replace('\\', '/'),
        concept=extra.get('concept') if extra else None,
        generated=time.strftime('%Y-%m-%d %H:%M'), blender=bpy.app.version_string,
        budget_lod0=budget, tris=dict(LOD0=lt[0], LOD1=lt[1], LOD2=lt[2]), lod_ratios=ratios,
        parts=n_parts, materials=slots, tris_per_slot_lod0=slot_tri,
        textures={k: dict(file=os.path.basename(v), size=[TEX, TEX]) for k, v in paths.items()},
        texture_notes=dict(BaseColor='sRGB', MetalSmooth='lineal: R=G=B metalico, A=smoothness (URP/Lit _MetallicGlossMap, Source = Metallic Alpha)',
                           Emission='sRGB: cian = estado (Surface), blanco = zonas tintables (Heat_Emissive / Charge_Emissive), negro = sin emision',
                           Normal='no se hornea: biseles reales + Weighted Normal; importar normales del FBX'),
        bake_seconds=bake_times, bake_stats=bake_stats,
        uv=uvinfo, topology=dict(duplicate_faces=topo['duplicate_faces'], hidden_tris=topo['hidden_tris'],
                                  open_parts=[p for p in topo['parts'] if p['open_edges']],
                                  parts_checked=len(topo['parts'])),
        dimensions_lod0_blender=dims, bounds_lod0_blender=dict(min=mn, max=mx),
        length_attachment_to_muzzle=L,
        sockets=dict(Attachment=[0, 0, 0], Muzzle=[0, -L, 0], unity_attachment=[0, 0, 0], unity_muzzle=[0, 0, L]),
        heat_step_attribute='Color de vertice HeatStep (R 0..1) en las caras de Heat_Emissive; 0 en el resto',
        verification=ver, renders={k: os.path.basename(v) for k, v in renders.items()},
        fbx=dict(file=fbx.name, sha256=sha256(fbx), bytes=fbx.stat().st_size),
        working_blend=str(blend.relative_to(ROOT)).replace('\\', '/'),
        engine=eng, seconds=round(time.time() - t_start, 1), notes=notes or [])
    if extra:
        metrics.update({k: v for k, v in extra.items() if k != 'concept'})
    with open(out_dir / 'metrics.json', 'w', encoding='utf-8') as f:
        json.dump(metrics, f, indent=2, ensure_ascii=False)
    print('[IR-B2]', weapon, json.dumps(dict(tris=metrics['tris'], slots=slot_tri, uv=uvinfo,
                                               checks=ver['checks'], all_pass=ver['all_pass']), ensure_ascii=False))
    return metrics
