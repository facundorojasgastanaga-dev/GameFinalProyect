"""IRON REQUIEM - Pase visual con Fable (F1, direccion de arte, seccion d).

Libreria comun de las laminas de concepto. Solo genera geometria procedural en
memoria y escribe PNG dentro de Docs/IronRequiem/VisualPassFable/concepts/.
No abre, importa ni modifica nada de Assets/. Sin red, sin IA generativa.

Patron tomado de Docs/IronRequiem/Concepts/Doors/Redesign_v01/door_lib.py.

Uso (Blender 5.2, sin interfaz):
  blender.exe -b --factory-startup --python <script>.py

Convencion de escena (metros): suelo z=0, arriba +Z. La sala tipo mide
40 (X) x 30 (Y) x 8 (Z); la camara mira hacia +Y (muro del fondo) y el muro
cercano (y=-15) no se construye (corte de maqueta). En Unity: X->X, Z->Y, Y->Z.
"""
import bpy, bmesh, math, os, random
from mathutils import Vector, Euler
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, '..'))                 # .../VisualPassFable/concepts
REPO = os.path.normpath(os.path.join(HERE, '..', '..', '..', '..', '..'))
F0 = os.path.join(REPO, 'Tools', 'IronRequiem', 'Reports', 'VisualPassFable', 'F0')

_mats = {}
_font = None


def reset_scene():
    global _mats, _font
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats = {}
    _font = None


def font():
    """Arial del sistema para tener tildes y la letra enie; si falta, la fuente interna."""
    global _font
    if _font is None:
        for p in (r'C:\Windows\Fonts\arialbd.ttf', r'C:\Windows\Fonts\arial.ttf'):
            if os.path.exists(p):
                try:
                    _font = bpy.data.fonts.load(p)
                    break
                except Exception:
                    pass
        if _font is None:
            _font = False
    return _font or None


# ------------------------------------------------------------ materiales ---

def mat(name, color=(0.1, 0.1, 0.1), rough=0.7, metal=0.0, emit=None, strength=0.0, alpha=None):
    """Material Principled con nombre. emit=(r,g,b) + strength para emisivos."""
    if name in _mats:
        return _mats[name]
    m = bpy.data.materials.new('VPF_' + name)
    m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*color, 1)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    if emit is not None:
        b.inputs['Emission Color'].default_value = (*emit, 1)
        b.inputs['Emission Strength'].default_value = strength
    if alpha is not None:
        b.inputs['Alpha'].default_value = alpha
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            pass
    m.diffuse_color = (*(emit if emit is not None else color), 1)
    _mats[name] = m
    return m


def beam_mat(name, color, strength=1.0, fac=0.10):
    """Haz de luz falso: emision mezclada con transparente (no es una Light)."""
    if name in _mats:
        return _mats[name]
    m = bpy.data.materials.new('VPF_' + name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.remove(nt.nodes.get('Principled BSDF'))
    out = nt.nodes.get('Material Output')
    em = nt.nodes.new('ShaderNodeEmission')
    em.inputs['Color'].default_value = (*color, 1)
    em.inputs['Strength'].default_value = strength
    tr = nt.nodes.new('ShaderNodeBsdfTransparent')
    mix = nt.nodes.new('ShaderNodeMixShader')
    mix.inputs['Fac'].default_value = fac
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs['Surface'])
    try:
        m.surface_render_method = 'BLENDED'
    except Exception:
        pass
    _mats[name] = m
    return m


# -------------------------------------------------------------- geometria ---

def _finish(name, bm, material, loc=(0, 0, 0), rot=(0, 0, 0)):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = Euler([math.radians(a) for a in rot])
    if material is not None:
        me.materials.append(material)
    return ob


def box(name, size, center, material, rot=(0, 0, 0), bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= size[0]; v.co.y *= size[1]; v.co.z *= size[2]
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, affect='EDGES')
    return _finish(name, bm, material, center, rot)


def box_mm(name, mn, mx, material, bevel=0.0):
    size = [mx[i] - mn[i] for i in range(3)]
    center = [(mx[i] + mn[i]) * 0.5 for i in range(3)]
    return box(name, size, center, material, bevel=bevel)


def cyl(name, r, length, center, material, axis='Z', r2=None, seg=20, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=length)
    base = {'Z': (0, 0, 0), 'X': (0, 90, 0), 'Y': (90, 0, 0)}[axis]
    ob = _finish(name, bm, material, center, (base[0] + rot[0], base[1] + rot[1], base[2] + rot[2]))
    return ob


def cyl_between(name, a, b, r, material, seg=10):
    a = Vector(a); b = Vector(b)
    d = b - a
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=d.length)
    ob = _finish(name, bm, material, (a + b) * 0.5)
    ob.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
    return ob


def quad(name, pts, material):
    """Poligono plano a partir de puntos 3D (en orden)."""
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in pts]
    bm.faces.new(vs)
    return _finish(name, bm, material)


def frustum_beam(name, top_center, top_size, bot_center, bot_size, material):
    """Tronco de piramide para un haz de luz falso."""
    bm = bmesh.new()
    t = Vector(top_center); b = Vector(bot_center)
    def ring(c, s):
        return [bm.verts.new((c.x + sx * s[0] / 2, c.y + sy * s[1] / 2, c.z)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    rt = ring(t, top_size); rb = ring(b, bot_size)
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((rt[i], rt[j], rb[j], rb[i]))
    return _finish(name, bm, material)


def text(name, body, loc, size, material, align='CENTER', rot=(90, 0, 0), align_y='CENTER'):
    cu = bpy.data.curves.new(name, 'FONT')
    cu.body = body
    cu.size = size
    cu.align_x = align
    cu.align_y = align_y
    f = font()
    if f:
        cu.font = f
    ob = bpy.data.objects.new(name, cu)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = Euler([math.radians(a) for a in rot])
    cu.materials.append(material)
    return ob


def light(name, kind, energy, loc, color=(1, 1, 1), rot=(0, 0, 0), size=0.5, spot=None, blend=0.5, shadow=True):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = color
    if kind == 'AREA':
        ld.size = size
    elif kind == 'SUN':
        ld.angle = math.radians(3)
    else:
        ld.shadow_soft_size = size
    if kind == 'SPOT' and spot:
        ld.spot_size = math.radians(spot)
        ld.spot_blend = blend
    try:
        ld.use_shadow = shadow
    except Exception:
        pass
    ob = bpy.data.objects.new(name, ld)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = Euler([math.radians(a) for a in rot])
    return ob


# ----------------------------------------------------------------- piezas ---

def room_shell(pal, door_x=0.0, door_w=8.0, door_h=6.0, ceiling=None):
    """Sala tipo 40 x 30 x 8 m en bloques. Devuelve nada; construye suelo, 3 muros y el vano del fondo."""
    floor = mat('floor', pal['floor'], 0.85)
    wall = mat('wall', pal['wall'], 0.9)
    trim = mat('trim', pal['trim'], 0.8)
    box_mm('Floor', (-20, -15, -0.4), (20, 15, 0), floor)
    # juntas del suelo cada 10 m (tesela maxima de Structure)
    joint = mat('joint', tuple(c * 0.55 for c in pal['floor']), 0.9)
    for x in (-10, 0, 10):
        box_mm('JointX', (x - 0.04, -15, 0), (x + 0.04, 15, 0.012), joint)
    for y in (-5, 5):
        box_mm('JointY', (-20, y - 0.04, 0), (20, y + 0.04, 0.012), joint)
    # muro del fondo con vano de puerta
    x0, x1 = door_x - door_w / 2, door_x + door_w / 2
    box_mm('WallN_L', (-20, 15, 0), (x0, 16, 8), wall)
    box_mm('WallN_R', (x1, 15, 0), (20, 16, 8), wall)
    box_mm('WallN_T', (x0, 15, door_h), (x1, 16, 8), wall)
    box_mm('WallW', (-21, -15, 0), (-20, 16, 8), wall)
    box_mm('WallE', (20, -15, 0), (21, 16, 8), wall)
    # zocalo y cornisa: dan escala y rompen el plano
    for (mn, mx) in (((-20, 14.85, 0), (x0, 15, 1.2)), ((x1, 14.85, 0), (20, 15, 1.2)),
                     ((-20, -15, 0), (-19.85, 15, 1.2)), ((19.85, -15, 0), (20, 15, 1.2))):
        box_mm('Skirt', mn, mx, trim)
    for (mn, mx) in (((-20, 14.6, 7.2), (20, 15, 8)), ((-20, -15, 7.2), (-19.6, 15, 8)), ((19.6, -15, 7.2), (20, 15, 8))):
        box_mm('Cornice', mn, mx, trim)
    # pilastras cada 10 m
    for x in (-10, 10):
        box_mm('Pilaster', (x - 0.5, 14.5, 0), (x + 0.5, 15, 8), trim)
    for y in (-5, 5):
        box_mm('PilasterW', (-20, y - 0.5, 0), (-19.5, y + 0.5, 8), trim)
        box_mm('PilasterE', (19.5, y - 0.5, 0), (20, y + 0.5, 8), trim)
    if ceiling:
        for (mn, mx) in ceiling:
            box_mm('Ceiling', mn, mx, wall)


def door_frame(x=0.0, w=8.0, h=6.0, state=(1.0, 0.03, 0.02), label='', leaf_color=(0.05, 0.05, 0.055)):
    """Compuerta de zona Z (8 x 6): marco, hoja cerrada y barra de estado de PUERTAS (no es decoracion)."""
    fr = mat('door_frame', (0.03, 0.03, 0.035), 0.6, 0.6)
    leaf = mat('door_leaf', leaf_color, 0.55, 0.6)
    box_mm('DoorJambL', (x - w / 2 - 0.7, 14.55, 0), (x - w / 2, 15.2, h + 0.7), fr)
    box_mm('DoorJambR', (x + w / 2, 14.55, 0), (x + w / 2 + 0.7, 15.2, h + 0.7), fr)
    box_mm('DoorHead', (x - w / 2 - 0.7, 14.55, h), (x + w / 2 + 0.7, 15.2, h + 0.9), fr)
    box_mm('DoorLeaf', (x - w / 2, 15.0, 0), (x + w / 2, 15.3, h), leaf)
    box_mm('DoorSeam', (x - 0.04, 14.97, 0), (x + 0.04, 15.0, h), fr)
    if state:
        st = mat('door_state', (0, 0, 0), 0.4, emit=state, strength=6.0)
        box_mm('DoorState', (x - w / 2 + 0.6, 14.5, h + 0.2), (x + w / 2 - 0.6, 14.56, h + 0.5), st)
    if label:
        text('DoorLabel', label, (x, 14.45, h + 1.05), 0.5, mat('stencil', (0.75, 0.75, 0.7), 0.9, emit=(0.8, 0.8, 0.74), strength=0.5))


def robot(name, loc, yaw=0.0, scale=1.0, eye=(1.0, 0.04, 0.02)):
    """Silueta de enemigo de 2 m (escala). Mas clara que la decoracion, con ojo rojo (excepcion aprobada)."""
    body = mat('enemy', (0.34, 0.33, 0.32), 0.55, 0.3)
    dark = mat('enemy_dark', (0.14, 0.14, 0.15), 0.6, 0.3)
    em = mat('enemy_eye', (0, 0, 0), 0.4, emit=eye, strength=14.0)
    parent = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(parent)
    parts = [
        box(name + '_legL', (0.24, 0.26, 0.95), (-0.22, 0, 0.475), dark),
        box(name + '_legR', (0.24, 0.26, 0.95), (0.22, 0, 0.475), dark),
        box(name + '_pelvis', (0.7, 0.36, 0.22), (0, 0, 1.03), body),
        box(name + '_torso', (0.86, 0.5, 0.62), (0, 0, 1.43), body),
        box(name + '_head', (0.34, 0.34, 0.26), (0, -0.03, 1.87), body),
        box(name + '_armL', (0.2, 0.22, 0.8), (-0.56, 0, 1.3), dark),
        box(name + '_armR', (0.2, 0.22, 0.8), (0.56, 0, 1.3), dark),
        box(name + '_gun', (0.14, 0.7, 0.14), (0.56, -0.3, 0.98), dark),
        box(name + '_eye', (0.2, 0.03, 0.07), (0, -0.21, 1.88), em),
    ]
    for p in parts:
        p.parent = parent
    parent.location = loc
    parent.rotation_euler = Euler((0, 0, math.radians(yaw)))
    parent.scale = (scale, scale, scale)
    return parent


def scrap_pile(name, center, radius, height, seed, mats, n=60, wall_dir=None):
    """Monton de chatarra: bloques y tubos oscuros dentro de un cono. wall_dir=(dx,dy) lo aplasta contra un muro."""
    rnd = random.Random(seed)
    cx, cy = center
    for i in range(n):
        h = rnd.random() ** 1.6                     # mas piezas abajo
        ang = rnd.random() * math.tau
        rr = radius * (1 - h) * math.sqrt(rnd.random())
        px, py = cx + math.cos(ang) * rr, cy + math.sin(ang) * rr
        if wall_dir:
            # refleja lo que cruce el plano del muro
            d = (px - cx) * wall_dir[0] + (py - cy) * wall_dir[1]
            if d > 0:
                px -= 2 * d * wall_dir[0]; py -= 2 * d * wall_dir[1]
        pz = h * height
        m = mats[rnd.randrange(len(mats))]
        s = (0.35 + rnd.random() * 1.1) * (1.15 - 0.55 * h)
        if rnd.random() < 0.3:
            cyl('%s_t%d' % (name, i), 0.08 + rnd.random() * 0.14, s * 1.6, (px, py, pz + 0.2), m, seg=8,
                rot=(rnd.uniform(40, 140), rnd.uniform(0, 180), rnd.uniform(0, 360)))
        else:
            box('%s_b%d' % (name, i), (s, s * rnd.uniform(0.3, 0.9), s * rnd.uniform(0.15, 0.6)), (px, py, pz + 0.2), m,
                rot=(rnd.uniform(-35, 35), rnd.uniform(-35, 35), rnd.uniform(0, 360)))


def wreck_limbs(name, loc, seed, material, count=3):
    """Restos de robot tumbados y oscuros (torso + extremidades), sin emisivo."""
    rnd = random.Random(seed)
    x, y = loc
    box(name + '_torso', (0.85, 0.5, 0.6), (x, y, 0.3), material, rot=(rnd.uniform(-20, 20), 80, rnd.uniform(0, 360)))
    for i in range(count):
        a = rnd.uniform(0, math.tau)
        r = rnd.uniform(0.7, 1.4)
        box('%s_l%d' % (name, i), (0.2, 0.22, 0.85), (x + math.cos(a) * r, y + math.sin(a) * r, 0.12), material,
            rot=(90, rnd.uniform(-15, 15), rnd.uniform(0, 360)))


def hanging_cables(name, anchor, ends, material, sag=0.6, r=0.05):
    """Cables arrancados: de un punto del techo a varios extremos colgantes (siempre a >= 4,5 m del suelo salvo en muros)."""
    a = Vector(anchor)
    for i, e in enumerate(ends):
        e = Vector(e)
        mid = (a + e) * 0.5 - Vector((0, 0, sag))
        cyl_between('%s_%da' % (name, i), a, mid, r, material, 6)
        cyl_between('%s_%db' % (name, i), mid, e, r, material, 6)


def pipe_run(name, a, b, r, material, flanges=4):
    cyl_between(name, a, b, r, material, 14)
    a = Vector(a); b = Vector(b)
    for i in range(flanges + 1):
        p = a.lerp(b, i / flanges)
        ob = cyl_between('%s_fl%d' % (name, i), p - (b - a).normalized() * 0.06, p + (b - a).normalized() * 0.06, r * 1.25, material, 14)


def truss(name, x0, x1, y, z, material):
    """Cercha de techo (decoracion colgante, borde inferior >= 4,5 m)."""
    box_mm(name + '_top', (x0, y - 0.12, z + 0.7), (x1, y + 0.12, z + 0.9), material)
    box_mm(name + '_bot', (x0, y - 0.12, z), (x1, y + 0.12, z + 0.2), material)
    n = max(2, int(round((x1 - x0) / 2.0)))
    for i in range(n):
        xa = x0 + (x1 - x0) * i / n
        xb = x0 + (x1 - x0) * (i + 1) / n
        if i % 2 == 0:
            cyl_between('%s_d%d' % (name, i), (xa, y, z + 0.1), (xb, y, z + 0.8), 0.06, material, 6)
        else:
            cyl_between('%s_d%d' % (name, i), (xa, y, z + 0.8), (xb, y, z + 0.1), 0.06, material, 6)


def combat_zone(mn, mx, color=(0.75, 0.75, 0.72), label='ESPACIO DE COMBATE DESPEJADO'):
    """Marca en el suelo el espacio que la decoracion no pisa (carriles + spawns). Solo es anotacion de la lamina."""
    m = mat('lane', (0, 0, 0), 0.9, emit=color, strength=0.55)
    dash, gap, w = 1.2, 0.8, 0.08
    x = mn[0]
    while x < mx[0]:
        xe = min(x + dash, mx[0])
        box_mm('LaneS', (x, mn[1] - w, 0.012), (xe, mn[1], 0.03), m)
        box_mm('LaneN', (x, mx[1], 0.012), (xe, mx[1] + w, 0.03), m)
        x += dash + gap
    y = mn[1]
    while y < mx[1]:
        ye = min(y + dash, mx[1])
        box_mm('LaneW', (mn[0] - w, y, 0.012), (mn[0], ye, 0.03), m)
        box_mm('LaneE', (mx[0], y, 0.012), (mx[0] + w, ye, 0.03), m)
        y += dash + gap
    if label:
        text('LaneLabel', label, ((mn[0] + mx[0]) / 2, mn[1] + 0.9, 0.03), 0.62, m, rot=(0, 0, 0))


def cover(name, mn, mx, pal):
    """Cobertura de Structure (gris neutro): la decoracion no la cambia."""
    return box_mm(name, mn, mx, mat('cover', pal['cover'], 0.8), bevel=0.03)


def marker(n, loc, cam, size=1.5):
    """Numero de landmark orientado a camara."""
    disc_m = mat('marker_bg', (0, 0, 0), 0.5, emit=(1.0, 1.0, 1.0), strength=1.6)
    txt_m = mat('marker_tx', (0, 0, 0), 1.0, emit=(0.0, 0.0, 0.0), strength=0.0)
    d = cyl('Marker%d' % n, size * 0.62, 0.02, loc, disc_m, seg=28)
    d.rotation_euler = cam.rotation_euler
    t = text('MarkerT%d' % n, str(n), loc, size, txt_m, rot=(0, 0, 0))
    t.rotation_euler = cam.rotation_euler
    # empuja el texto 3 cm hacia la camara
    fwd = (cam.location - Vector(loc)).normalized()
    t.location = Vector(loc) + fwd * 0.05
    return d


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


def setup_stage(world=(0.01, 0.01, 0.012), haze=0.0, haze_color=(1, 1, 1), width=1280, height=720):
    sc = bpy.context.scene
    eng = _engine(sc)
    for k, v in (('taa_render_samples', 48), ('use_volumetric_shadows', True), ('volumetric_tile_size', '4'),
                 ('volumetric_samples', 96), ('volumetric_end', 120.0), ('volumetric_start', 0.5),
                 ('use_shadows', True)):
        try:
            setattr(sc.eevee, k, v)
        except Exception:
            pass
    try:
        sc.view_settings.view_transform = 'Standard'
        sc.view_settings.look = 'None'
    except Exception:
        pass
    w = bpy.data.worlds.new('World')
    w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes.get('Background')
    bg.inputs['Color'].default_value = (*world, 1)
    bg.inputs['Strength'].default_value = 1.0
    if haze > 0:
        try:
            vol = nt.nodes.new('ShaderNodeVolumeScatter')
            vol.inputs['Color'].default_value = (*haze_color, 1)
            vol.inputs['Density'].default_value = haze
            vol.inputs['Anisotropy'].default_value = 0.35
            nt.links.new(vol.outputs[0], nt.nodes.get('World Output').inputs['Volume'])
        except Exception as e:
            print('VPF sin niebla volumetrica:', e)
    sc.world = w
    sc.render.resolution_x, sc.render.resolution_y = width, height
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = 'PNG'
    return eng


def camera(name, loc, target, fov=55):
    cd = bpy.data.cameras.new(name)
    cd.sensor_fit = 'HORIZONTAL'
    cd.angle = math.radians(fov)
    cd.clip_start = 0.05
    cd.clip_end = 500
    ob = bpy.data.objects.new(name, cd)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    d = Vector(target) - Vector(loc)
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.camera = ob
    return ob


def caption(cam, title, lines, palette, fov=55, note=''):
    """Franja inferior (25 % del cuadro) pegada a la camara: titulo, leyenda de landmarks y muestras de paleta.
    Lineas de leyenda: <= 80 caracteres para no pisar las muestras."""
    sc = bpy.context.scene
    aspect = sc.render.resolution_y / sc.render.resolution_x
    hw = math.tan(math.radians(fov / 2))
    hh = hw * aspect
    bg_m = mat('cap_bg', (0, 0, 0), 1.0, emit=(0.012, 0.012, 0.014), strength=1.0)
    wh = mat('cap_white', (0, 0, 0), 1.0, emit=(0.92, 0.92, 0.9), strength=1.0)
    og = mat('cap_orange', (0, 0, 0), 1.0, emit=(1.0, 0.45, 0.08), strength=1.0)
    gr = mat('cap_grey', (0, 0, 0), 1.0, emit=(0.55, 0.57, 0.6), strength=1.0)
    band = hh * 0.50
    top = -hh + band
    objs = []
    def P(o):
        o.parent = cam
        objs.append(o)
        return o
    P(box('CapBG', (hw * 2.1, band, 0.001), (0, -hh + band / 2, -1.003), bg_m))
    x0 = -hw + 0.02
    P(text('CapTitle', title, (x0, top - 0.024, -1.0), 0.0235, og, align='LEFT', rot=(0, 0, 0)))
    y = top - 0.056
    for ln in lines:
        P(text('CapLine', ln, (x0, y, -1.0), 0.0158, wh, align='LEFT', rot=(0, 0, 0)))
        y -= 0.0215
    if note:
        P(text('CapNote', note, (x0, -hh + 0.012, -1.0), 0.0118, gr, align='LEFT', rot=(0, 0, 0)))
    # paleta (derecha, a la altura de la leyenda)
    chip = 0.043
    px = hw - 0.02 - len(palette) * (chip + 0.004)
    for i, (nm, col) in enumerate(palette):
        cm = mat('chip_%d' % i, (0, 0, 0), 1.0, emit=col, strength=1.0)
        cx = px + i * (chip + 0.004) + chip / 2
        P(box('Chip', (chip, chip, 0.001), (cx, top - 0.072, -1.001), cm))
        P(text('ChipT', nm, (cx, top - 0.105, -1.0), 0.0098, wh, align='CENTER', rot=(0, 0, 0)))
    return objs


def render(path):
    sc = bpy.context.scene
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print('VPF render ->', path)
    return path


def srgb(hexstr):
    """'#RRGGBB' -> lineal (para que el color pintado coincida con el hex del documento)."""
    h = hexstr.lstrip('#')
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return tuple(out)


def image_stats(path):
    """Media y % de pixeles oscuros (<16/255) de un PNG, para detectar laminas negras."""
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    a = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    a = a.reshape(h, w, 4)[..., :3]
    lum = a.mean(axis=2)                       # ya en espacio de visualizacion (PNG sRGB)
    return float(lum.mean() * 255), float((lum < 16 / 255).mean() * 100)


# --------------------------------------------- composicion antes / despues ---

def _load(path, tw, th):
    img = bpy.data.images.load(path, check_existing=False)
    img.scale(tw, th)
    a = np.empty(tw * th * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    return a.reshape(th, tw, 4)


def side_by_side(out_path, left, right, tw=960, th=540, gutter=8):
    """Coloca dos imagenes lado a lado (antes | despues). Sin retoque: solo escala y junta."""
    BG = (0.02, 0.02, 0.022, 1)
    L = _load(left, tw, th)
    R = _load(right, tw, th)
    full = np.concatenate([L, np.full((th, gutter, 4), BG, np.float32), R], 1)
    full[..., 3] = 1.0
    h, w = full.shape[:2]
    out = bpy.data.images.new('sheet', w, h, alpha=False)
    out.pixels.foreach_set(full.ravel())
    out.filepath_raw = out_path
    out.file_format = 'PNG'
    out.save()
    return (w, h)
