"""IRON REQUIEM - libreria comun de bocetos de PUERTAS (Doors/Redesign_v01, fase P1).

Solo genera geometria procedural y PNG dentro de esta carpeta. No abre, importa
ni modifica assets de Unity. Sigue el patron de Concepts/Weapons/Redesign_v01.

Convencion de escena (metros): vano centrado en x0, suelo z=0, arriba +Z.
Plano de la puerta en y=0 (grosor T en Y). Lado SALA = -Y (donde mira la camara),
lado ESCLUSA = +Y. En Unity: X->X, Z->Y, Y->Z.
"""
import bpy, bmesh, math, json, os
from mathutils import Vector, Matrix, Euler
import numpy as np

# ------------------------------------------------------------ materiales ---
PALETTE = {
    # nombre: (color base lineal, rugosidad, metalico, ruido_oxido)
    'leaf':    ((0.085, 0.085, 0.082), 0.55, 0.60, (0.16, 0.055, 0.022)),   # hoja: acero pintado gastado
    'frame':   ((0.035, 0.035, 0.040), 0.60, 0.65, (0.09, 0.035, 0.018)),   # marco/alojamiento fijo
    'wall':    ((0.022, 0.023, 0.026), 0.85, 0.20, (0.045, 0.025, 0.018)),
    'floor':   ((0.022, 0.022, 0.024), 0.70, 0.50, False),
    'steel':   ((0.50, 0.50, 0.52), 0.30, 1.00, False),     # vastagos, pernos
    'black':   ((0.006, 0.006, 0.007), 0.80, 0.00, False),
    'screen':  ((0.004, 0.004, 0.005), 0.25, 0.00, False),
    'human':   ((0.30, 0.30, 0.31), 0.70, 0.00, False),
}
EMISSIVE = {
    'em_red':   ((1.0, 0.025, 0.015), 9.0),
    'em_amber': ((1.0, 0.36, 0.02), 9.0),
    'em_green': ((0.10, 1.0, 0.22), 7.0),
    'em_cyan':  ((0.05, 0.75, 1.0), 7.0),
    'em_white': ((1.0, 0.92, 0.82), 6.0),
    'em_dim':   ((1.0, 0.36, 0.02), 1.2),   # marcas de barrido en suelo
    'cap_bg':   ((0.0, 0.0, 0.0), 0.0),
}
STATES = {
    'blocked': dict(em='em_red',   text='BLOQUEADA', beams=None,      light=(1.0, 0.05, 0.03), label='BLOQUEADA (rojo fijo)'),
    'unlock':  dict(em='em_amber', text='ABRIENDO',  beams='em_amber', light=(1.0, 0.40, 0.05), label='ABRIENDOSE - cerrojos fuera'),
    'half':    dict(em='em_amber', text='ABRIENDO',  beams='em_amber', light=(1.0, 0.40, 0.05), label='ABRIENDOSE - a mitad'),
    'open':    dict(em='em_green', text='PASO',      beams=None,      light=(0.15, 1.0, 0.30), label='ABIERTA / PASA (verde)'),
    'alarm':   dict(em='em_alarm', text='ALERTA',    beams='em_red',  light=(1.0, 0.05, 0.03), label='ALARMA (CorridorAlarm)'),
}

_mats = {}


def reset_scene():
    global _mats
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats = {}


def _principled(m):
    m.use_nodes = True
    return m.node_tree, m.node_tree.nodes.get('Principled BSDF')


def mat(name):
    if name in _mats:
        return _mats[name]
    m = bpy.data.materials.new('IR_' + name)
    nt, bsdf = _principled(m)
    if name in EMISSIVE:
        col, st = EMISSIVE[name]
        bsdf.inputs['Base Color'].default_value = (*col, 1)
        bsdf.inputs['Emission Color'].default_value = (*col, 1)
        bsdf.inputs['Emission Strength'].default_value = st
        bsdf.inputs['Roughness'].default_value = 0.4
        if name == 'cap_bg':
            bsdf.inputs['Roughness'].default_value = 1.0
            bsdf.inputs['Specular IOR Level'].default_value = 0.0
            bsdf.inputs['Base Color'].default_value = (0, 0, 0, 1)
    elif name == 'em_alarm':
        # franjas rojo/blanco emisivas (estroboscopio de alarma congelado)
        tc = nt.nodes.new('ShaderNodeTexCoord')
        wv = nt.nodes.new('ShaderNodeTexWave')
        wv.wave_type = 'BANDS'; wv.bands_direction = 'Z'
        wv.inputs['Scale'].default_value = 1.6
        wv.inputs['Distortion'].default_value = 0.0
        rp = nt.nodes.new('ShaderNodeValToRGB')
        rp.color_ramp.interpolation = 'CONSTANT'
        rp.color_ramp.elements[0].color = (1.0, 0.02, 0.01, 1)
        rp.color_ramp.elements[1].position = 0.55
        rp.color_ramp.elements[1].color = (1.0, 0.95, 0.9, 1)
        nt.links.new(tc.outputs['Object'], wv.inputs['Vector'])
        nt.links.new(wv.outputs['Fac'], rp.inputs['Fac'])
        nt.links.new(rp.outputs['Color'], bsdf.inputs['Base Color'])
        nt.links.new(rp.outputs['Color'], bsdf.inputs['Emission Color'])
        bsdf.inputs['Emission Strength'].default_value = 9.0
    elif name.startswith('beam_'):
        # cono de luz giratoria falso (malla aditiva, no es una luz dinamica)
        col = EMISSIVE['em_' + name[5:]][0]
        nt.nodes.remove(bsdf)
        out = nt.nodes.get('Material Output')
        em = nt.nodes.new('ShaderNodeEmission')
        em.inputs['Color'].default_value = (*col, 1)
        em.inputs['Strength'].default_value = 1.6
        tr = nt.nodes.new('ShaderNodeBsdfTransparent')
        mix = nt.nodes.new('ShaderNodeMixShader')
        mix.inputs['Fac'].default_value = 0.16
        nt.links.new(tr.outputs[0], mix.inputs[1])
        nt.links.new(em.outputs[0], mix.inputs[2])
        nt.links.new(mix.outputs[0], out.inputs['Surface'])
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            pass
    elif name == 'hazard':
        tc = nt.nodes.new('ShaderNodeTexCoord')
        wv = nt.nodes.new('ShaderNodeTexWave')
        wv.wave_type = 'BANDS'; wv.bands_direction = 'DIAGONAL'
        wv.inputs['Scale'].default_value = 0.55
        wv.inputs['Distortion'].default_value = 0.0
        rp = nt.nodes.new('ShaderNodeValToRGB')
        rp.color_ramp.interpolation = 'CONSTANT'
        rp.color_ramp.elements[0].color = (0.010, 0.010, 0.010, 1)
        rp.color_ramp.elements[1].position = 0.5
        rp.color_ramp.elements[1].color = (0.75, 0.33, 0.015, 1)
        nz = nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 3.0
        nz.inputs['Detail'].default_value = 8.0
        r2 = nt.nodes.new('ShaderNodeValToRGB')
        r2.color_ramp.elements[0].position = 0.55; r2.color_ramp.elements[0].color = (0.35, 0.14, 0.05, 1)
        r2.color_ramp.elements[1].position = 0.70; r2.color_ramp.elements[1].color = (1, 1, 1, 1)
        mul = nt.nodes.new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'
        mul.inputs['Factor'].default_value = 1.0
        nt.links.new(tc.outputs['Object'], wv.inputs['Vector'])
        nt.links.new(wv.outputs['Fac'], rp.inputs['Fac'])
        nt.links.new(tc.outputs['Object'], nz.inputs['Vector'])
        nt.links.new(nz.outputs['Fac'], r2.inputs['Fac'])
        nt.links.new(rp.outputs['Color'], mul.inputs['A'])
        nt.links.new(r2.outputs['Color'], mul.inputs['B'])
        nt.links.new(mul.outputs['Result'], bsdf.inputs['Base Color'])
        bsdf.inputs['Roughness'].default_value = 0.6
        bsdf.inputs['Metallic'].default_value = 0.3
    else:
        col, rough, metal, rust = PALETTE[name]
        rust = rust if isinstance(rust, tuple) else None
        bsdf.inputs['Roughness'].default_value = rough
        bsdf.inputs['Metallic'].default_value = metal
        if rust:
            tc = nt.nodes.new('ShaderNodeTexCoord')
            nz = nt.nodes.new('ShaderNodeTexNoise')
            nz.inputs['Scale'].default_value = 1.4
            nz.inputs['Detail'].default_value = 9.0
            nz.inputs['Roughness'].default_value = 0.65
            rp = nt.nodes.new('ShaderNodeValToRGB')
            rp.color_ramp.elements[0].position = 0.48
            rp.color_ramp.elements[0].color = (*col, 1)
            rp.color_ramp.elements[1].position = 0.78
            rp.color_ramp.elements[1].color = (*rust, 1)  # oxido
            nt.links.new(tc.outputs['Object'], nz.inputs['Vector'])
            nt.links.new(nz.outputs['Fac'], rp.inputs['Fac'])
            nt.links.new(rp.outputs['Color'], bsdf.inputs['Base Color'])
            bm_ = nt.nodes.new('ShaderNodeBump'); bm_.inputs['Strength'].default_value = 0.25
            nz2 = nt.nodes.new('ShaderNodeTexNoise'); nz2.inputs['Scale'].default_value = 18.0
            nt.links.new(tc.outputs['Object'], nz2.inputs['Vector'])
            nt.links.new(nz2.outputs['Fac'], bm_.inputs['Height'])
            nt.links.new(bm_.outputs['Normal'], bsdf.inputs['Normal'])
        else:
            bsdf.inputs['Base Color'].default_value = (*col, 1)
    _mats[name] = m
    return m


# ------------------------------------------------------------ geometria ---

def _finish(name, bm, material, loc=(0, 0, 0)):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    ob.location = loc
    ob.data.materials.append(mat(material))
    return ob


def _bevel(bm, amount):
    if amount > 0:
        bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges), offset=amount,
                        segments=1, profile=0.5, affect='EDGES', clamp_overlap=True)


def box(name, size, center, material, bevel=0.02):
    """Caja biselada con su centro en `center` (origen = centro)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= size[0]; v.co.y *= size[1]; v.co.z *= size[2]
    _bevel(bm, min(bevel, min(size) * 0.45))
    return _finish(name, bm, material, center)


def box_minmax(name, mn, mx, material, bevel=0.02):
    c = [(a + b) / 2 for a, b in zip(mn, mx)]
    s = [abs(b - a) for a, b in zip(mn, mx)]
    return box(name, s, c, material, bevel)


def cyl(name, r, length, center, material, axis='Z', r2=None, seg=16, rot_z=0.0, rot_x=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg,
                          radius1=r, radius2=r if r2 is None else r2, depth=length)
    if axis == 'Y':
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'X'))
    elif axis == 'X':
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'Y'))
    if rot_x:
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(rot_x), 3, 'X'))
    if rot_z:
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(rot_z), 3, 'Z'))
    ob = _finish(name, bm, material, center)
    for p in ob.data.polygons:
        p.use_smooth = True
    return ob


def cyl_between(name, a, b, r, material, seg=12):
    a, b = Vector(a), Vector(b)
    d = b - a
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r, depth=d.length)
    q = d.to_track_quat('Z', 'Y')
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=q.to_matrix())
    ob = _finish(name, bm, material, (a + b) / 2)
    for p in ob.data.polygons:
        p.use_smooth = True
    return ob


def prism_frame(name, pts, thick, origin, udir, vdir, material, v0=0.0, bevel=0.015):
    """Poligono 2D (u, z) extruido 'thick' a lo largo de vdir (desde v0).
    El origen del objeto es el PIVOTE `origin`: world = origin + u*udir + v*vdir + z*Z."""
    O = Vector(origin); U = Vector((udir[0], udir[1], 0)); V = Vector((vdir[0], vdir[1], 0)); Z = Vector((0, 0, 1))
    bm = bmesh.new()
    a = [bm.verts.new(u * U + v0 * V + z * Z) for u, z in pts]
    b = [bm.verts.new(u * U + (v0 + thick) * V + z * Z) for u, z in pts]
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    _bevel(bm, bevel)
    return _finish(name, bm, material, O)


def prism_xz(name, pts, y0, y1, material, origin=(0, 0, 0), bevel=0.015):
    """Poligono en el plano XZ (coordenadas absolutas) extruido entre y0 e y1.
    El origen del objeto se coloca en `origin` (pivote)."""
    O = Vector(origin)
    rel = [(x - O.x, z - O.z) for x, z in pts]
    return prism_frame(name, rel, y1 - y0, (O.x, y0, O.z), (1, 0), (0, 1), material, 0.0, bevel)


def rivets(name, pts, size, material='steel', normal='Y'):
    """Muchos remaches pequenos en un solo objeto (cubos achaflanados)."""
    bm = bmesh.new()
    s = size
    for p in pts:
        r = bmesh.ops.create_cube(bm, size=1.0)
        vs = r['verts']
        for v in vs:
            if normal == 'Y':
                v.co = Vector((v.co.x * s, v.co.y * s * 0.6, v.co.z * s)) + Vector(p)
            else:
                v.co = Vector((v.co.x * s * 0.6, v.co.y * s, v.co.z * s)) + Vector(p)
    return _finish(name, bm, material)


def text(name, body, loc, size, material, align='CENTER', rot=(90, 0, 0), extrude=0.0):
    cu = bpy.data.curves.new(name, 'FONT')
    cu.body = body
    cu.size = size
    cu.align_x = align
    cu.align_y = 'CENTER'
    cu.extrude = extrude
    ob = bpy.data.objects.new(name, cu)
    bpy.context.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = Euler([math.radians(a) for a in rot])
    cu.materials.append(mat(material))
    return ob


def human(x, y, name='Escala_1p8m'):
    """Silueta de escala de 1,8 m (jugador)."""
    return [cyl(name + '_body', 0.32, 1.35, (x, y, 0.675 + 0.05), 'human', seg=12),
            cyl(name + '_head', 0.17, 0.30, (x, y, 1.62), 'human', seg=12)]


def light(name, kind, energy, loc, color=(1, 1, 1), rot=(0, 0, 0), size=0.3, spot=None):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = color
    if kind == 'SUN':
        ld.angle = math.radians(10)
    else:
        ld.shadow_soft_size = size
    if kind == 'SPOT' and spot:
        ld.spot_size = math.radians(spot)
        ld.spot_blend = 0.4
    if kind == 'AREA':
        ld.size = size
    ob = bpy.data.objects.new(name, ld)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = Euler([math.radians(a) for a in rot])
    return ob


def ss(k):
    k = max(0.0, min(1.0, k))
    return k * k * (3 - 2 * k)


def clamp01(k):
    return max(0.0, min(1.0, k))


# ------------------------------------------------- marco comun (FIJO) ---

def frame(x0, W, H, T, state, jl=0.5, jr=0.5, head_h=0.7, front=0.35, back=0.15,
          head_front=None, wall_ext=3.0, wall_top=3.0, tag='F', sill=True, beacons=True,
          display=True, light_on=True, tunnel=None):
    """Marco/alojamiento FIJO (hermano siempre activo del root de la puerta).
    jl/jr: ancho de jamba o casete lateral; head_h: alto del dintel o casete superior.
    Devuelve la lista de objetos del marco."""
    st = STATES[state]
    em = st['em']
    objs = []
    hf = front if head_front is None else head_front
    y0, y1 = -T / 2 - front, T / 2 + back
    # muro (no es parte de la puerta: solo contexto)
    hw_l = W / 2 + jl + wall_ext
    hw_r = W / 2 + jr + wall_ext
    yw0, yw1 = -T / 2 - 0.05, T / 2 + 0.6
    objs.append(box_minmax(f'{tag}_WallL', (x0 - hw_l, yw0, 0), (x0 - W / 2 - jl + 0.02, yw1, H + head_h + wall_top), 'wall', 0.0))
    objs.append(box_minmax(f'{tag}_WallR', (x0 + W / 2 + jr - 0.02, yw0, 0), (x0 + hw_r, yw1, H + head_h + wall_top), 'wall', 0.0))
    objs.append(box_minmax(f'{tag}_WallTop', (x0 - W / 2 - jl, yw0, H + head_h - 0.02), (x0 + W / 2 + jr, yw1, H + head_h + wall_top), 'wall', 0.0))
    # jambas / casetes laterales
    objs.append(box_minmax(f'{tag}_JambL', (x0 - W / 2 - jl, y0, 0), (x0 - W / 2, y1, H + head_h), 'frame', 0.04))
    objs.append(box_minmax(f'{tag}_JambR', (x0 + W / 2, y0, 0), (x0 + W / 2 + jr, y1, H + head_h), 'frame', 0.04))
    # dintel / casete superior
    objs.append(box_minmax(f'{tag}_Header', (x0 - W / 2 - 0.01, -T / 2 - hf, H), (x0 + W / 2 + 0.01, y1, H + head_h), 'frame', 0.04))
    # franjas de peligro en el canto interior de las jambas
    for s, jw in ((-1, jl), (1, jr)):
        xi = x0 + s * (W / 2 + 0.11)
        objs.append(box_minmax(f'{tag}_HazJamb{s}', (xi - 0.1, y0 - 0.03, 0.0), (xi + 0.1, y0 + 0.02, H + head_h - 0.05), 'hazard', 0.0))
        # tira de estado (emisivo por mascara en el material HardSurface)
        xs = x0 + s * (W / 2 + 0.28)
        objs.append(box_minmax(f'{tag}_Strip{s}', (xs - 0.05, y0 - 0.06, 0.35), (xs + 0.05, y0 - 0.01, H - 0.1), em, 0.0))
        # remaches sobre la cara del casete
        pts = []
        xa, xb = x0 + s * (W / 2 + 0.45), x0 + s * (W / 2 + jw - 0.12)
        if abs(xb - xa) > 0.2:
            for z in np.arange(0.4, H + head_h - 0.2, 0.55):
                for xx in (xa, xb):
                    pts.append((xx, y0 - 0.02, z))
            objs.append(rivets(f'{tag}_RivJ{s}', pts, 0.07))
    # tira de estado del dintel
    objs.append(box_minmax(f'{tag}_StripTop', (x0 - W / 2 + 0.2, -T / 2 - hf - 0.06, H + 0.08), (x0 + W / 2 - 0.2, -T / 2 - hf - 0.01, H + 0.16), em, 0.0))
    if sill:
        objs.append(box_minmax(f'{tag}_Sill', (x0 - W / 2 - 0.3, -T / 2 - 0.7, -0.02), (x0 + W / 2 + 0.3, T / 2 + 0.3, 0.06), 'hazard', 0.02))
    if display:
        dw = min(3.2, W * 0.42)
        zc = H + min(head_h, 1.4) * 0.5
        yd = -T / 2 - hf
        objs.append(box_minmax(f'{tag}_Display', (x0 - dw / 2, yd - 0.08, zc - 0.27), (x0 + dw / 2, yd + 0.02, zc + 0.27), 'screen', 0.02))
        txt_mat = 'em_red' if state == 'alarm' else em
        objs.append(text(f'{tag}_DisplayTxt', st['text'], (x0, yd - 0.1, zc), min(0.42, dw / 6.5), txt_mat))
    if beacons:
        for s in (-1, 1):
            bx = x0 + s * (W / 2 + 0.25)
            bz = H + head_h + 0.02
            objs.append(cyl(f'{tag}_BeaconBase{s}', 0.2, 0.12, (bx, -T / 2 - front + 0.25, bz + 0.06), 'frame'))
            lens_m = st['beams'] or em
            if state == 'alarm':
                lens_m = 'em_red'
            objs.append(cyl(f'{tag}_BeaconLens{s}', 0.15, 0.3, (bx, -T / 2 - front + 0.25, bz + 0.27), lens_m, r2=0.12))
            if st['beams']:
                # dos haces opuestos (malla aditiva que gira; sin luz dinamica)
                ang = 35 if s < 0 else 160
                for k in (0, 180):
                    a = math.radians(ang + k)
                    L = 4.0
                    c = Vector((bx, -T / 2 - front + 0.25, bz + 0.27)) + Vector((math.cos(a), math.sin(a), 0)) * (L / 2)
                    objs.append(cyl(f'{tag}_Beam{s}_{k}', 0.05, L, c, 'beam_' + st['beams'][3:], axis='X', r2=0.42, rot_z=ang + k + 180))
    if tunnel is not None:
        ty0, ty1 = T / 2 + 0.6, T / 2 + 9.0
        tx = W / 2 + 0.45
        objs.append(box_minmax(f'{tag}_WallTunL', (x0 - tx - 0.6, ty0, 0), (x0 - tx, ty1, H + tunnel + 0.5), 'wall', 0.0))
        objs.append(box_minmax(f'{tag}_WallTunR', (x0 + tx, ty0, 0), (x0 + tx + 0.6, ty1, H + tunnel + 0.5), 'wall', 0.0))
        objs.append(box_minmax(f'{tag}_WallTunCeil', (x0 - tx - 0.6, ty0, H + tunnel), (x0 + tx + 0.6, ty1, H + tunnel + 0.5), 'wall', 0.0))
        objs.append(box_minmax(f'{tag}_WallTunEnd', (x0 - tx - 0.6, ty1, 0), (x0 + tx + 0.6, ty1 + 0.4, H + tunnel + 0.5), 'frame', 0.0))
        objs.append(box_minmax(f'{tag}_WallTunLamp', (x0 - 1.2, ty1 - 0.05, H * 0.55), (x0 + 1.2, ty1, H * 0.55 + 0.12), 'em_cyan', 0.0))
        light(f'{tag}_TunLight', 'POINT', 6 * W * H, (x0, (ty0 + ty1) / 2, H + tunnel - 0.6), (0.6, 0.75, 1.0), size=1.0)
    if light_on:
        lg = light(f'{tag}_StateLight', 'POINT', 18 * W * H, (x0, -T / 2 - front - 1.3, H - 0.3), st['light'], size=0.5)
        objs.append(lg)
    return [o for o in objs if o.type != 'LIGHT']


def floor_plane(xmin, xmax, ymin, ymax):
    return box_minmax('Floor', (xmin, ymin, -0.3), (xmax, ymax, -0.02), 'floor', 0.0)


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


def _bloom(sc):
    """Glare/bloom en el compositor si la API lo permite (5.x usa node groups)."""
    try:
        tree = bpy.data.node_groups.new('IR_Comp', 'CompositorNodeTree')
        rl = tree.nodes.new('CompositorNodeRLayers')
        gl = tree.nodes.new('CompositorNodeGlare')
        out = tree.nodes.new('NodeGroupOutput')
        tree.interface.new_socket('Image', in_out='OUTPUT', socket_type='NodeSocketColor')
        for k, v in (('Type', 'Bloom'), ('Threshold', 1.0), ('Strength', 0.6), ('Size', 0.5), ('Quality', 'Medium')):
            try:
                gl.inputs[k].default_value = v
            except Exception:
                pass
        tree.links.new(rl.outputs['Image'], gl.inputs['Image'])
        tree.links.new(gl.outputs['Image'], out.inputs[0])
        sc.compositing_node_group = tree
        return 'bloom(5.x)'
    except Exception as e:
        try:
            sc.compositing_node_group = None
        except Exception:
            pass
        return 'sin bloom: %s' % e


def setup_stage(world=0.010):
    sc = bpy.context.scene
    eng = _engine(sc)
    try:
        sc.eevee.taa_render_samples = 24
    except Exception:
        pass
    try:
        sc.view_settings.view_transform = 'Standard'
        sc.view_settings.look = 'None'
    except Exception:
        pass
    w = bpy.data.worlds.new('World')
    w.use_nodes = True
    bg = w.node_tree.nodes.get('Background')
    bg.inputs['Color'].default_value = (world, world, world * 1.15, 1)
    bg.inputs['Strength'].default_value = 1.0
    sc.world = w
    sc.render.resolution_x, sc.render.resolution_y = 1280, 720
    sc.render.film_transparent = False
    bloom = _bloom(sc)
    return eng, bloom


def key_lights(center, span):
    """Luz de escena (no forma parte del presupuesto de la puerta)."""
    c = Vector(center)
    light('Key', 'SUN', 1.1, (0, 0, 0), (0.85, 0.9, 1.0), rot=(62, 0, -28))
    light('Fill', 'AREA', 14 * span * span, c + Vector((span * 0.2, -span * 1.1, span * 0.35)),
          (0.75, 0.82, 1.0), rot=(70, 0, 8), size=span * 0.8)


def camera(name, loc, target, fov=50, lens_shift=(0, 0)):
    cd = bpy.data.cameras.new(name)
    cd.sensor_fit = 'HORIZONTAL'
    cd.angle = math.radians(fov)
    cd.clip_start = 0.05
    cd.clip_end = 500
    cd.shift_x, cd.shift_y = lens_shift
    ob = bpy.data.objects.new(name, cd)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    d = Vector(target) - Vector(loc)
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.camera = ob
    return ob


def caption(cam, line1, line2='', fov=50):
    """Rotulo pegado a la camara (esquina inferior izquierda)."""
    sc = bpy.context.scene
    aspect = sc.render.resolution_y / sc.render.resolution_x
    hw = math.tan(math.radians(fov / 2))
    hh = hw * aspect
    objs = []
    bg = box('CapBG', (hw * 2.2, hh * 0.30, 0.001), (0, -hh + hh * 0.15, -1.002), 'cap_bg', 0.0)
    bg.parent = cam
    t1 = text('Cap1', line1, (-hw + 0.03, -hh + hh * 0.20, -1.0), min(hh * 0.085, hw * 1.9 / (len(line1) * 0.56)), 'em_white', align='LEFT', rot=(0, 0, 0))
    t1.parent = cam
    objs += [bg, t1]
    if line2:
        t2 = text('Cap2', line2, (-hw + 0.03, -hh + hh * 0.08, -1.0), min(hh * 0.06, hw * 1.9 / (len(line2) * 0.56)), 'em_white', align='LEFT', rot=(0, 0, 0))
        t2.parent = cam
        objs.append(t2)
    return objs


def render(path):
    sc = bpy.context.scene
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def tri_count(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    total = 0
    for o in objs:
        if o.type not in ('MESH', 'FONT', 'CURVE'):
            continue
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        total += len(me.loop_triangles)
        ev.to_mesh_clear()
    return total


# --------------------------------------------- titulos / cronograma / lamina ---

def _flat_scene(width, height):
    reset_scene()
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'FLAT'
    sh.color_type = 'MATERIAL'
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x, sc.render.resolution_y = width, height
    w = bpy.data.worlds.new('w'); w.color = (0.02, 0.02, 0.022); sc.world = w
    return sc


def _fm(color):
    m = bpy.data.materials.new('f')
    m.diffuse_color = (*color, 1)
    return m


def _ftxt(sc, body, x, y, size, color, align='LEFT'):
    cu = bpy.data.curves.new('t', 'FONT')
    cu.body = body; cu.size = size; cu.align_x = align; cu.align_y = 'CENTER'
    ob = bpy.data.objects.new('t', cu); sc.collection.objects.link(ob)
    ob.location = (x, y, 0)
    cu.materials.append(_fm(color))
    return ob


def _fbox(sc, x0, y0, x1, y1, color):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x = x0 + (v.co.x + 0.5) * (x1 - x0)
        v.co.y = y0 + (v.co.y + 0.5) * (y1 - y0)
        v.co.z *= 0.01
    me = bpy.data.meshes.new('b'); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new('b', me); sc.collection.objects.link(ob)
    me.materials.append(_fm(color))
    return ob


def _ortho(sc, width_units, cx, cy):
    cd = bpy.data.cameras.new('c'); cd.type = 'ORTHO'; cd.ortho_scale = width_units
    co = bpy.data.objects.new('c', cd); sc.collection.objects.link(co)
    co.location = (cx, cy, 5)
    sc.camera = co


def render_title(path, title, subtitle, width=1280, height=110):
    sc = _flat_scene(width, height)
    W = 20.0; Hh = W * height / width
    _ftxt(sc, title, -W / 2 + 0.3, Hh * 0.18, Hh * 0.34, (1.0, 0.45, 0.08))
    _ftxt(sc, subtitle, -W / 2 + 0.3, -Hh * 0.25, Hh * 0.19, (0.85, 0.85, 0.85))
    _ortho(sc, W, 0, 0)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


PHASE_COLORS = [(1.0, 0.36, 0.02), (0.95, 0.85, 0.2), (0.15, 0.75, 1.0), (0.2, 0.9, 0.3), (0.9, 0.2, 0.15)]


def render_timeline(path, title, open_phases, close_phases, notes=(), width=1280, height=720):
    """Cronograma por fases. *_phases: lista de (etiqueta, t0, t1, sonido)."""
    sc = _flat_scene(width, height)
    W = 20.0; Hh = W * height / width   # 20 x 11.25
    x_l, x_r = -8.6, 9.4
    tmax = 1.1
    sx = (x_r - x_l) / tmax
    _ftxt(sc, title, -9.7, 4.95, 0.45, (1.0, 0.45, 0.08))
    def row(y, label, phases, ref):
        _ftxt(sc, label, -9.7, y + 1.15, 0.36, (0.9, 0.9, 0.9))
        for i, (lab, t0, t1, snd) in enumerate(phases):
            col = PHASE_COLORS[i % len(PHASE_COLORS)]
            _fbox(sc, x_l + t0 * sx, y - 0.32, x_l + t1 * sx - 0.03, y + 0.32, col)
            lv = i % 3
            _ftxt(sc, f'{t0:.2f}-{t1:.2f}s', x_l + t0 * sx + 0.05, y + 0.55 + 0.3 * lv, 0.22, col)
            _ftxt(sc, lab, x_l + t0 * sx + 0.05, y - 0.6 - 0.62 * lv, 0.24, (0.9, 0.9, 0.9))
            if snd:
                _ftxt(sc, snd, x_l + t0 * sx + 0.05, y - 0.88 - 0.62 * lv, 0.2, (0.55, 0.75, 1.0))
        # referencia actual
        _fbox(sc, x_l + ref * sx - 0.02, y - 0.6, x_l + ref * sx + 0.02, y + 0.9, (1, 1, 1))
        _ftxt(sc, f'actual {ref:.2f}s', x_l + ref * sx + 0.08, y + 0.95, 0.2, (1, 1, 1))
    row(2.7, 'APERTURA', open_phases, 0.70)
    row(-1.1, 'CIERRE', close_phases, 0.45)
    # eje
    for i in range(12):
        t = i * 0.1
        _fbox(sc, x_l + t * sx - 0.01, -3.85, x_l + t * sx + 0.01, -3.65, (0.6, 0.6, 0.6))
        _ftxt(sc, f'{t:.1f} s', x_l + t * sx, -4.05, 0.2, (0.6, 0.6, 0.6), align='CENTER')
    _fbox(sc, x_l, -3.76, x_r, -3.74, (0.6, 0.6, 0.6))
    for j, n in enumerate(notes):
        _ftxt(sc, n, -9.7, -4.6 - j * 0.34, 0.2, (0.8, 0.8, 0.8))
    _ortho(sc, W, 0, 0)
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


def compose_grid(out_path, title_png, rows, gutter=8, scale=0.5):
    """rows: lista de filas (de arriba a abajo), cada una lista de PNG."""
    BG = (0.02, 0.02, 0.022, 1)
    row_imgs = []
    for paths in rows:
        tiles = [_load(p, scale) for p in paths]
        h = max(t.shape[0] for t in tiles)
        parts = []
        for i, t in enumerate(tiles):
            if t.shape[0] < h:
                t = np.concatenate([np.full((h - t.shape[0], t.shape[1], 4), BG, np.float32), t], 0)
            parts.append(t)
            if i < len(tiles) - 1:
                parts.append(np.full((h, gutter, 4), BG, np.float32))
        row_imgs.append(np.concatenate(parts, 1))
    wmax = max(r.shape[1] for r in row_imgs)
    stack = []
    for r in row_imgs:
        if r.shape[1] < wmax:
            r = np.concatenate([r, np.full((r.shape[0], wmax - r.shape[1], 4), BG, np.float32)], 1)
        stack += [r, np.full((gutter, wmax, 4), BG, np.float32)]
    title = _load(title_png, 1.0)
    tw = np.full((title.shape[0], wmax, 4), BG, np.float32)
    n = min(title.shape[1], wmax)
    tw[:, :n] = title[:, :n]
    stack = [tw, np.full((gutter, wmax, 4), BG, np.float32)] + stack[:-1]
    full = np.concatenate(list(reversed(stack)), 0)  # pixels de Blender: abajo -> arriba
    full[..., 3] = 1.0
    h, w = full.shape[:2]
    out = bpy.data.images.new('sheet', w, h, alpha=False)
    out.pixels.foreach_set(full.ravel())
    out.filepath_raw = out_path
    out.file_format = 'PNG'
    out.save()
    return (w, h)


def stack_vertical(out_path, paths, gutter=4):
    """Apila PNG de arriba a abajo (mismo ancho)."""
    BG = (0.02, 0.02, 0.022, 1)
    tiles = [_load(pth, 1.0) for pth in paths]
    parts = []
    for t in tiles:
        parts += [t, np.full((gutter, t.shape[1], 4), BG, np.float32)]
    full = np.concatenate(list(reversed(parts[:-1])), 0)
    full[..., 3] = 1.0
    h, w = full.shape[:2]
    out = bpy.data.images.new('stack', w, h, alpha=False)
    out.pixels.foreach_set(full.ravel())
    out.filepath_raw = out_path
    out.file_format = 'PNG'
    out.save()
    return (w, h)
