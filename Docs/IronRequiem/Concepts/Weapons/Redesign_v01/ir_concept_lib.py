"""IRON REQUIEM - libreria comun de bocetos de armas (Redesign_v01).

Solo genera geometria procedural y renders dentro de esta carpeta. No abre,
importa ni modifica assets de Unity. Uso desde los scripts de cada arma:

  blender --background --factory-startup --python-exit-code 1 \
          --python <Arma>/<arma>_concepts.py -- [IDs de direccion]

Convencion: origen = acople trasero (collar de antebrazo, diametro 0,09 m),
disparo hacia -Y, arriba +Z, unidades en metros.
"""
import bpy, bmesh, math, random, json, os
from mathutils import Vector, Matrix, Euler
import numpy as np

COLLAR_R = 0.045  # diametro provisional 0,09 m

PALETTE = {
    # nombre: (color base lineal, rugosidad, metalico)
    'plate':    ((0.62, 0.58, 0.52), 0.55, 0.15),   # placa clara del protagonista
    'graphite': ((0.035, 0.036, 0.040), 0.45, 0.70),
    'orange':   ((0.80, 0.21, 0.03), 0.50, 0.10),   # naranja industrial (pintura)
    'black':    ((0.004, 0.004, 0.005), 0.80, 0.0),  # anima / interior de boca
    'steel':    ((0.45, 0.45, 0.47), 0.30, 1.0),     # metal desnudo / rebaba de corte
    't07':      ((0.15, 0.19, 0.26), 0.55, 0.55),   # blindaje del T-07 (gris azulado, T07_BaseColor)
    't07dark':  ((0.028, 0.03, 0.034), 0.65, 0.6),
    'rust':     ((0.16, 0.05, 0.02), 0.85, 0.2),     # interior cortado
    'cable':    ((0.012, 0.012, 0.012), 0.6, 0.0),
    'cable_r':  ((0.30, 0.02, 0.01), 0.6, 0.0),
    'cable_y':  ((0.55, 0.38, 0.02), 0.6, 0.0),
    'copper':   ((0.75, 0.35, 0.15), 0.35, 1.0),
    'ruler':    ((0.02, 0.02, 0.02), 0.9, 0.0),
    'arm_ref':  ((0.30, 0.29, 0.27), 0.7, 0.1),
}
EMISSIVE = {
    'cyan':    ((0.05, 0.75, 1.0), 1.6),   # estado / luces del protagonista
    'heat':    ((1.0, 0.16, 0.01), 1.4),   # calor / OVERDRIVE (gradiente cian->naranja->rojo)
    'heat_hi': ((1.0, 0.05, 0.004), 1.7),  # punto mas caliente
    'magenta': ((0.85, 0.05, 1.0), 2.0),  # carga del canon T-07 (CANNON_CHARGE.md)
    'cut_hot': ((1.0, 0.12, 0.02), 0.9),   # borde de corte aun caliente
}

_mats = {}
_rng = random.Random(7)


def reset_scene():
    global _mats
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats = {}
    _rng.seed(7)


def seed(n):
    _rng.seed(n)


def rnd(a, b):
    return _rng.uniform(a, b)


def mat(name):
    if name in _mats:
        return _mats[name]
    m = bpy.data.materials.new('IR_' + name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes.get('Principled BSDF')
    if name in EMISSIVE:
        col, strength = EMISSIVE[name]
        bsdf.inputs['Base Color'].default_value = (*col, 1)
        bsdf.inputs['Emission Color'].default_value = (*col, 1)
        bsdf.inputs['Emission Strength'].default_value = strength
        bsdf.inputs['Roughness'].default_value = 0.3
    else:
        col, rough, metal = PALETTE[name]
        bsdf.inputs['Roughness'].default_value = rough
        bsdf.inputs['Metallic'].default_value = metal
        if name in ('plate', 'graphite', 't07', 't07dark', 'orange'):
            # desgaste simple: ruido que oscurece el color base (metal gastado)
            tex = nt.nodes.new('ShaderNodeTexNoise')
            tex.inputs['Scale'].default_value = 22.0
            tex.inputs['Detail'].default_value = 6.0
            ramp = nt.nodes.new('ShaderNodeValToRGB')
            ramp.color_ramp.elements[0].position = 0.42
            ramp.color_ramp.elements[0].color = (*[c * 0.78 for c in col], 1)
            ramp.color_ramp.elements[1].position = 0.62
            ramp.color_ramp.elements[1].color = (*col, 1)
            nt.links.new(tex.outputs['Fac'], ramp.inputs['Fac'])
            nt.links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
        else:
            bsdf.inputs['Base Color'].default_value = (*col, 1)
    _mats[name] = m
    return m


def _finish(name, bm, material, loc=(0, 0, 0), rot=(0, 0, 0), smooth=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = Euler([math.radians(a) for a in rot])
    ob.data.materials.append(mat(material))
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
        for e in me.edges:
            pass
        # bordes duros por angulo
        me.validate()
        bm2 = bmesh.new(); bm2.from_mesh(me)
        for e in bm2.edges:
            if len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(smooth):
                e.smooth = False
        bm2.to_mesh(me); bm2.free()
    return ob


def _bevel(bm, amount, segments=1):
    if amount > 0:
        bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges), offset=amount,
                        segments=segments, profile=0.5, affect='EDGES', clamp_overlap=True)


def box(name, size, loc, material, rot=(0, 0, 0), bevel=0.004, top=(1.0, 1.0), shift=(0.0, 0.0), seg=1):
    """Caja biselada. top=(sx,sy) escala la cara superior; shift desplaza la cara superior."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= size[0]; v.co.y *= size[1]; v.co.z *= size[2]
        if v.co.z > 0:
            v.co.x = v.co.x * top[0] + shift[0]
            v.co.y = v.co.y * top[1] + shift[1]
    _bevel(bm, min(bevel, min(size) * 0.45), seg)
    return _finish(name, bm, material, loc, rot)


def cyl(name, r, length, loc, material, r2=None, seg=12, rot=(0, 0, 0), bevel=0.002, axis='Y', smooth=40):
    """Cilindro/cono. Eje Y por defecto: r en la parte trasera (+Y), r2 delante (-Y)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg,
                          radius1=r, radius2=r if r2 is None else r2, depth=length)
    if axis == 'Y':
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'X'))
    elif axis == 'X':
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'Y'))
    # bisel solo en bordes de tapa
    if bevel > 0:
        caps = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(60)]
        bmesh.ops.bevel(bm, geom=caps + list({v for e in caps for v in e.verts}), offset=bevel,
                        segments=1, profile=0.5, affect='EDGES', clamp_overlap=True)
    return _finish(name, bm, material, loc, rot, smooth=smooth)


def sphere(name, r, loc, material, u=12, v=8, scale=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r)
    for vt in bm.verts:
        vt.co.x *= scale[0]; vt.co.y *= scale[1]; vt.co.z *= scale[2]
    return _finish(name, bm, material, loc, smooth=60)


def ring(name, r_out, r_in, y0, y1, material, seg=16, loc=(0, 0, 0), arc=None, jag=0.0,
         jag_end='y0', rot=(0, 0, 0), inner_material=None, smooth=40, phase=0.0):
    """Anillo/tubo a lo largo de Y entre y0 (trasero) e y1 (delantero).
    arc=(a0,a1) en grados limita el sector (casco abierto). jag>0 hace irregular
    el extremo indicado (rotura/rebaba)."""
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
    rng_ = range(n) if closed else range(n - 1)
    for i in rng_:
        A, B = rows[i], rows[(i + 1) % n]
        for k in range(4):
            k2 = (k + 1) % 4
            try:
                bm.faces.new((A[k], A[k2], B[k2], B[k]))
            except ValueError:
                pass
    if not closed:
        for R in (rows[0], rows[-1]):
            try:
                bm.faces.new(R)
            except ValueError:
                pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _finish(name, bm, material, loc, rot, smooth=smooth)


def prism(name, pts, thickness, loc, material, plane='XZ', rot=(0, 0, 0), bevel=0.0):
    """Extruye un poligono 2D. plane='XZ' -> espesor en Y; 'YZ' -> espesor en X; 'XY' -> en Z."""
    bm = bmesh.new()
    def to3(u, v, w):
        if plane == 'XZ':
            return (u, w, v)
        if plane == 'YZ':
            return (w, u, v)
        return (u, v, w)
    a = [bm.verts.new(to3(u, v, -thickness / 2)) for u, v in pts]
    b = [bm.verts.new(to3(u, v, thickness / 2)) for u, v in pts]
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    _bevel(bm, bevel)
    return _finish(name, bm, material, loc, rot)


def jagged_outline(w, h, teeth=7, depth=0.012, edge='top'):
    """Contorno rectangular con un borde dentado (chapa arrancada)."""
    pts = [(-w / 2, -h / 2), (w / 2, -h / 2)]
    if edge == 'top':
        pts.append((w / 2, h / 2))
        for i in range(teeth, 0, -1):
            x = -w / 2 + w * i / teeth
            pts.append((x - w / teeth * rnd(0.3, 0.7), h / 2 - rnd(0.2, 1.0) * depth))
        pts.append((-w / 2, h / 2))
    return pts


def cable(name, points, radius, material='cable'):
    cu = bpy.data.curves.new(name, 'CURVE')
    cu.dimensions = '3D'
    cu.bevel_depth = radius
    cu.bevel_resolution = 1
    cu.resolution_u = 5
    cu.use_fill_caps = True
    sp = cu.splines.new('BEZIER')
    sp.bezier_points.add(len(points) - 1)
    for bp, p in zip(sp.bezier_points, points):
        bp.co = p
        bp.handle_left_type = bp.handle_right_type = 'AUTO'
    ob = bpy.data.objects.new(name, cu)
    bpy.context.collection.objects.link(ob)
    ob.data.materials.append(mat(material))
    return ob


def shards(prefix, center, radius, count, y, material='steel', size=0.012, arc=(0, 360)):
    """Rebabas: astillas finas alrededor de un corte circular."""
    obs = []
    for i in range(count):
        a = math.radians(rnd(*arc))
        r = radius * rnd(0.9, 1.05)
        loc = (center[0] + r * math.cos(a), y + rnd(-0.006, 0.006), center[1] + r * math.sin(a))
        obs.append(box(f'{prefix}_{i}', (size * rnd(0.3, 0.6), size * rnd(1.0, 2.2), size * rnd(0.15, 0.3)),
                       loc, material, rot=(rnd(-40, 40), rnd(-90, 90), math.degrees(a) + 90),
                       bevel=0.0, top=(0.2, 0.3)))
    return obs


def collar(prefix='Collar', style='protagonist'):
    """Acople trasero en el origen: collar circular de antebrazo, diametro 0,09 m."""
    obs = [ring(prefix + '_Ring', COLLAR_R, 0.031, 0.0, -0.022, 'graphite', seg=20),
           cyl(prefix + '_Seal', 0.031, 0.004, (0, -0.006, 0), 'black', seg=16, bevel=0)]
    obs.append(ring(prefix + '_Lip', COLLAR_R + 0.002, COLLAR_R - 0.004, 0.0, -0.004, 'steel', seg=20))
    for s in (-1, 1):
        obs.append(box(f'{prefix}_Lock{s}', (0.010, 0.016, 0.012), (s * 0.047, -0.011, 0), 'orange', bevel=0.002))
    if style == 'clamp':  # abrazadera improvisada sobre pieza arrancada
        for i in range(4):
            a = math.radians(45 + 90 * i)
            obs.append(box(f'{prefix}_Bolt{i}', (0.012, 0.026, 0.012),
                           (0.051 * math.cos(a), -0.016, 0.051 * math.sin(a)), 'steel', bevel=0.002,
                           rot=(0, -math.degrees(a), 0)))
    return obs


# ---------------------------------------------------------------- render ---

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
        sc.eevee.taa_render_samples = 32
    except Exception:
        pass
    try:
        sc.view_settings.view_transform = 'Standard'
    except Exception:
        pass
    w = bpy.data.worlds.new('World')
    w.use_nodes = True
    bgn = w.node_tree.nodes.get('Background')
    bgn.inputs['Color'].default_value = (bg, bg, bg * 1.04, 1)
    bgn.inputs['Strength'].default_value = 1.0
    sc.world = w
    def light(name, kind, energy, rot, size=1.0, color=(1, 1, 1), loc=(0, 0, 0)):
        ld = bpy.data.lights.new(name, kind)
        ld.energy = energy
        ld.color = color
        if kind == 'SUN':
            ld.angle = math.radians(8)
        else:
            ld.size = size
        ob = bpy.data.objects.new(name, ld)
        sc.collection.objects.link(ob)
        ob.rotation_euler = Euler([math.radians(a) for a in rot])
        ob.location = loc
        return ob
    light('Key', 'SUN', 2.6, (50, 0, 145))
    light('Fill', 'SUN', 0.8, (60, 0, -60), color=(0.75, 0.85, 1.0))
    light('Rim', 'SUN', 2.5, (110, 0, 20), color=(1.0, 0.9, 0.8))
    return eng


def bounds(objs):
    """Limites reales a partir de la malla evaluada (las curvas no dan bound_box fiable)."""
    dg = bpy.context.evaluated_depsgraph_get()
    pts = []
    for o in objs:
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        pts += [ev.matrix_world @ v.co for v in me.vertices]
        ev.to_mesh_clear()
    mn = Vector([min(p[i] for p in pts) for i in range(3)])
    mx = Vector([max(p[i] for p in pts) for i in range(3)])
    return mn, mx


def tri_count(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    total = 0
    for o in objs:
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        total += len(me.loop_triangles)
        ev.to_mesh_clear()
    return total


def _cam(name, loc, target, ortho=None, fov=None, roll=0.0):
    cd = bpy.data.cameras.new(name)
    if ortho:
        cd.type = 'ORTHO'
        cd.ortho_scale = ortho
    else:
        cd.sensor_fit = 'HORIZONTAL'
        cd.angle = math.radians(fov or 50)
    cd.clip_start = 0.01
    ob = bpy.data.objects.new(name, cd)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    d = Vector(target) - Vector(loc)
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    return ob


def render_views(out_prefix, nominal_len, weapon_objs, ref_len=None):
    """Tres vistas a 1200 px: 3/4 frontal (lado -X), perfil desde +X (el lado que
    ve la camara FPS, con regla del largo actual) y vista FPS aproximada.
    Con disparo hacia -Y, +X queda a la izquierda del arma vista desde atras."""
    sc = bpy.context.scene
    L = nominal_len
    c = Vector((0, -L * 0.5, 0.0))
    files = {}
    # helpers visibles solo en algunas vistas
    ruler = []
    rl = ref_len or L
    zr = -0.11 if L < 0.5 else -0.17
    ruler.append(box('Ruler_Bar', (0.002, rl, 0.003), (0, -rl / 2, zr), 'ruler', bevel=0))
    for y in (0.0, -rl):
        ruler.append(box(f'Ruler_Tick{y}', (0.002, 0.003, 0.025), (0, y, zr), 'ruler', bevel=0))
    arm = []  # sin antebrazo: un muñón tapaba el acople en la vista FPS
    for o in ruler + arm:
        o.hide_render = True

    sc.render.resolution_x, sc.render.resolution_y = 1200, 800
    d = Vector((-0.95, -1.15, 0.70)).normalized()
    cam = _cam('Cam34', c + d * 3.0, c, ortho=L * 1.45)
    sc.camera = cam
    sc.render.filepath = files['34'] = out_prefix + '_34.png'
    bpy.ops.render.render(write_still=True)

    cam = _cam('CamSide', c + Vector((3.0, 0, 0.0)), c, ortho=L * 1.45)
    sc.camera = cam
    for o in ruler:
        o.hide_render = False
    sc.render.filepath = files['side'] = out_prefix + '_perfil.png'
    bpy.ops.render.render(write_still=True)
    for o in ruler:
        o.hide_render = True

    # Vista FPS aproximada: 0,35 m detras, 0,12 m por encima del acople,
    # desplazada a la izquierda para que el arma quede a la derecha; FOV 60.
    sc.render.resolution_x, sc.render.resolution_y = 1200, 675
    eye = Vector((0.13, 0.35, 0.12))
    cam = _cam('CamFPS', eye, Vector((0.05, -0.45, -0.05)), fov=60)
    sc.camera = cam
    for o in arm:
        o.hide_render = False
    sc.render.filepath = files['fps'] = out_prefix + '_fps.png'
    bpy.ops.render.render(write_still=True)
    for o in arm:
        o.hide_render = True
    return files


def render_label(path, title, subtitle, width=1200, height=130):
    reset_scene()
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'FLAT'
    sh.color_type = 'SINGLE'
    sh.background_type = 'VIEWPORT' if hasattr(sh, 'background_type') else None
    try:
        sh.background_color = (0.02, 0.02, 0.022)
    except Exception:
        pass
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x, sc.render.resolution_y = width, height
    w = bpy.data.worlds.new('w'); w.color = (0.02, 0.02, 0.022); sc.world = w
    aspect = width / height
    scale = 2.0
    def txt(body, size, y, color):
        cu = bpy.data.curves.new('t', 'FONT')
        cu.body = body
        cu.size = size
        cu.align_x = 'LEFT'
        cu.align_y = 'CENTER'
        ob = bpy.data.objects.new('t', cu)
        sc.collection.objects.link(ob)
        ob.location = (-scale / 2 + 0.03, y, 0)
        m = bpy.data.materials.new('m'); m.diffuse_color = (*color, 1)
        cu.materials.append(m)
        return ob
    hh = scale / aspect
    txt(title, hh * 0.36, hh * 0.17, (1.0, 0.45, 0.08))
    txt(subtitle, hh * 0.20, -hh * 0.24, (0.85, 0.85, 0.85))
    sh.color_type = 'MATERIAL'
    cd = bpy.data.cameras.new('c'); cd.type = 'ORTHO'; cd.ortho_scale = scale
    co = bpy.data.objects.new('c', cd); sc.collection.objects.link(co)
    co.location = (0, 0, 5)
    sc.camera = co
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def _load(path, scale=0.5):
    img = bpy.data.images.load(path, check_existing=False)
    if scale != 1.0:
        img.scale(int(img.size[0] * scale), int(img.size[1] * scale))
    w, h = img.size
    a = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    return a.reshape(h, w, 4)


def compose_sheet(out_path, title_png, columns, gutter=10, scale=0.5):
    """columns: lista de listas de rutas PNG ordenadas de arriba a abajo."""
    cols = []
    for paths in columns:
        tiles = [_load(p, scale) for p in paths]
        wmax = max(t.shape[1] for t in tiles)
        stack = []
        for t in tiles:  # arriba -> abajo; pixels de Blender van de abajo a arriba
            pad = np.full((gutter, wmax, 4), (0.02, 0.02, 0.022, 1), np.float32)
            stack += [t, pad]
        col = np.concatenate(list(reversed(stack[:-1])), axis=0)
        cols.append(col)
    hmax = max(c.shape[0] for c in cols)
    parts = []
    for i, c in enumerate(cols):
        if c.shape[0] < hmax:
            c = np.concatenate([np.full((hmax - c.shape[0], c.shape[1], 4), 0.02, np.float32), c], axis=0)
        parts.append(c)
        if i < len(cols) - 1:
            parts.append(np.full((hmax, gutter, 4), (0.02, 0.02, 0.022, 1), np.float32))
    body = np.concatenate(parts, axis=1)
    title = _load(title_png, 1.0)
    if title.shape[1] != body.shape[1]:
        tw = np.full((title.shape[0], body.shape[1], 4), (0.02, 0.02, 0.022, 1), np.float32)
        n = min(title.shape[1], body.shape[1])
        tw[:, :n] = title[:, :n]
        title = tw
    pad = np.full((gutter, body.shape[1], 4), (0.02, 0.02, 0.022, 1), np.float32)
    full = np.concatenate([body, pad, title], axis=0)
    full[..., 3] = 1.0
    h, w = full.shape[:2]
    out = bpy.data.images.new('sheet', w, h, alpha=False)
    out.pixels.foreach_set(full.ravel())
    out.filepath_raw = out_path
    out.file_format = 'PNG'
    out.save()
    return (w, h)


def run_weapon(weapon_dir, weapon_name, nominal_len, directions, selected=None, sheet_name=None):
    """directions: lista de dicts {id, name, idea, heat, build}. Renderiza y compone la lamina."""
    import sys
    os.makedirs(weapon_dir, exist_ok=True)
    report_path = os.path.join(weapon_dir, 'report.json')
    report = {}
    if os.path.exists(report_path):
        with open(report_path, encoding='utf-8') as f:
            report = json.load(f)
    for d in directions:
        if selected and d['id'] not in selected:
            continue
        reset_scene()
        eng = setup_stage()
        seed(sum(map(ord, d['id'])))
        before = set(bpy.data.objects)
        d['build']()
        objs = [o for o in bpy.data.objects if o not in before and o.type in ('MESH', 'CURVE')]
        mn, mx = bounds(objs)
        tris = tri_count(objs)
        prefix = os.path.join(weapon_dir, f"{d['id']}_{d['slug']}")
        files = render_views(prefix, nominal_len, objs)
        report[d['id']] = {
            'name': d['name'], 'idea': d['idea'], 'heat_point': d['heat'],
            'tris': tris, 'objects': len(objs), 'engine': eng,
            'bounds_min': [round(v, 4) for v in mn], 'bounds_max': [round(v, 4) for v in mx],
            'length_coupling_to_front': round(-mn.y, 4),
            'length_vs_current_pct': round((-mn.y / nominal_len - 1) * 100, 1),
            'renders': {k: os.path.basename(v) for k, v in files.items()},
        }
        print('[IR]', d['id'], report[d['id']])
    with open(report_path, 'w', encoding='utf-8') as f:
        json.dump(report, f, indent=2, ensure_ascii=False)
    # lamina comparativa
    cols = []
    lab_dir = os.path.join(weapon_dir, '_labels')
    os.makedirs(lab_dir, exist_ok=True)
    for d in directions:
        r = report.get(d['id'])
        if not r:
            continue
        lab = os.path.join(lab_dir, f"{d['id']}.png")
        render_label(lab, f"{d['id']}  {d['name'].upper()}",
                     f"{r['length_coupling_to_front']:.3f} m ({r['length_vs_current_pct']:+.0f}%)  |  {r['tris']} tris  |  calor: {d['heat']}")
        base = os.path.join(weapon_dir, f"{d['id']}_{d['slug']}")
        cols.append([lab, base + '_34.png', base + '_perfil.png', base + '_fps.png'])
    title = os.path.join(lab_dir, '_title.png')
    ncols = len(cols)
    render_label(title, f"IRON REQUIEM  -  {weapon_name}  -  Redesign v01",
                 f"Filas: 3/4 frontal  |  perfil (lado visto en FPS; regla = largo actual {nominal_len:.3f} m)  |  vista FPS aprox. (FOV 60, sin brazo)",
                 width=600 * ncols + 10 * (ncols - 1), height=110)
    sheet = os.path.join(weapon_dir, f"{sheet_name or weapon_name.replace(' ', '')}_lamina.png")
    size = compose_sheet(sheet, title, cols)
    print('[IR] sheet', sheet, size)
