"""IRON REQUIEM - Pase visual con Fable (F1 d): esquema de toda la ruta en seccion.

Genera concepts/ruta_subida.png (2880 x 1620): los 18 cuartos de Map01 en orden de juego, de las
profundidades a la salida, con la cota que tendrian con la opcion O1 (invertir cotas), el color
dominante propuesto, la evolucion de la luz (objetivo indicativo frente a lo medido en F0 y el
minimo legible) y los niveles de deterioro, ocupacion robot y senales del exterior.

Datos:
  - cotas actuales y desniveles: Assets/IronRequiem/Levels/Map01/Data/Structure/R<NN>.json (leidos el 21/11/2026;
    aqui van copiados para que el script no dependa de Assets/). O1 = se invierte el signo y los 6 cuartos con
    entrada elevada se recorren de abajo arriba.
  - luz medida hoy: Tools/IronRequiem/Reports/VisualPassFable/F0/zones.csv (frame_mean de las tomas 'vacio').
  - objetivo de luz, D, M y E: PROPUESTA de direccion de arte (01d_DECORACION.md). El objetivo de luz es
    indicativo: los valores definitivos son de la seccion a) del coordinador.

Ejecucion:
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --factory-startup --python ruta_subida.py
"""
import bpy, bmesh, sys, os, csv, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fable_lib as L
from fable_lib import srgb

W_PX, H_PX = 2880, 1620
W_U = 32.0
H_U = W_U * H_PX / W_PX       # 18

# id, nombre nuevo (2 lineas), nombre actual, tramo, cota O1 del suelo, subida interior, techo, color, D, M, E, luz objetivo, clave de F0
ROOMS = [
    ('R01', ('Control de', 'cuarentena'), 'Control acceso', 1, 0, 0, 9, '#6E8FA6', 1, 0, 0, 46, 'Map01_R01_ControlAcceso'),
    ('R02', ('Pozo de', 'izado'), 'Pozo montacargas', 1, 0, 12, 21, '#6E8FA6', 2, 0, 0, 46, 'Map01_R02_PozoMontacargas'),
    ('R03', ('Criostasis', 'de serie'), 'Dormitorios', 1, 12, 0, 8, '#7FA3B8', 1, 0, 0, 47, 'Map01_R03_Dormitorios'),
    ('R04', ('Bancos de', 'pruebas'), 'Comedor', 1, 12, 0, 9, '#86A0AE', 1, 1, 0, 48, 'Map01_R04_Comedor'),
    ('R05', ('Quirófano de', 'ensamblaje'), 'Enfermería', 1, 12, 0, 8, '#A9BEC6', 1, 1, 0, 50, 'Map01_R05_Enfermeria'),
    ('R06', ('Armería 7', ''), 'Armería', 2, 12, 6, 14, '#A8845C', 2, 1, 0, 50, 'Map01_R06_Armeria'),
    ('R07', ('Nodo de', 'comunicación'), 'Comunicaciones', 2, 18, 0, 10, '#7E9A94', 2, 1, 1, 51, 'Map01_R07_Comunicaciones'),
    ('R08', ('Almacén', 'tomado'), 'Almacenes', 2, 18, 4, 14, '#B08D5A', 2, 2, 1, 52, 'Map01_R08_Almacenes'),
    ('R09', ('Depósito de', 'mantenimiento'), 'Taquillas', 2, 22, 0, 8, '#9A8F80', 1, 1, 1, 53, 'Map01_R09_Taquillas'),
    ('R10', ('Túnel de', 'servicio T-9'), 'Túnel trituradora', 2, 22, 0, 10, '#8C6F62', 3, 2, 1, 50, 'Map01_R10_TunelTrituradora'),
    ('R11', ('Decantadores', ''), 'Decantadores', 2, 22, 6, 14, '#5E9C98', 2, 2, 1, 54, 'Map01_R11_Decantadores'),
    ('R12', ('Colector', 'derrumbado'), 'Colector', 2, 28, 0, 14, '#5E9C98', 3, 2, 2, 54, 'Map01_R12_Colector'),
    ('R13', ('Bombas y', 'generadores'), 'Sala de bombas', 2, 28, 0, 10, '#6FA8A0', 2, 2, 1, 56, 'Map01_R13_SalaBombas'),
    ('R14', ('Forja de', 'reciclaje'), 'Fundición', 3, 28, 0, 13, '#C07A45', 2, 3, 2, 58, 'Map01_R14_Fundicion'),
    ('R15', ('Línea de', 're-ensamblaje'), 'Ensamblaje', 3, 28, 0, 13, '#B9B9AE', 2, 3, 2, 60, 'Map01_R15_Ensamblaje'),
    ('R16', ('Toma de aire', 'y filtrado'), 'Reactor', 3, 28, 6, 14, '#BFD3E6', 3, 3, 3, 64, 'Map01_R16_Reactor'),
    ('R17', ('Puesto de', 'defensa'), 'Centro de mando', 3, 34, 3, 12, '#A9BCCB', 2, 2, 3, 66, 'Map01_R17_CentroMando'),
    ('R18', ('Compuerta', 'Cero'), 'Bóveda', 3, 37, 0, 24, '#D5DEE8', 3, 3, 3, 72, 'Map01_R18_Boveda'),
]
# cota actual (worldOrigin.y de Structure) para la fila comparativa
CURRENT = dict(R01=0, R02=-12, R03=-12, R04=-12, R05=-12, R06=-18, R07=-18, R08=-22, R09=-22, R10=-22, R11=-28,
               R12=-28, R13=-28, R14=-28, R15=-28, R16=-34, R17=-37, R18=-37)
TRAMOS = {1: ('TRAMO 1 · PROFUNDIDADES', 'laboratorios · emergencia fría · tecnología intacta pero muerta', '#9FD4FF'),
          2: ('TRAMO 2 · INFRAESTRUCTURA', 'armería, almacenes, mantenimiento, agua y energía · cada vez más dañada y tomada por los robots', '#E0A868'),
          3: ('TRAMO 3 · CERCA DE LA SUPERFICIE', 'polvo, tierra, luz natural fría · salida', '#D5E4F5')}
SURFACE = 65.0     # m sobre el hangar: R18 (+37) + 24 de techo + 4 de losa. Propuesta de rotulacion.
MIN_LEGIBLE = 45   # 05_ILUMINACION §L1.3 (carriles y spawns)


def f0_means():
    out = {}
    path = os.path.join(L.F0, 'zones.csv')
    try:
        acc = {}
        with open(path, encoding='utf-8') as f:
            for r in csv.DictReader(f):
                if r['shot'] == 'vacio':
                    acc.setdefault(r['room'], []).append(float(r['frame_mean']))
        out = {k: sum(v) / len(v) for k, v in acc.items()}
    except Exception as e:
        print('VPF sin zones.csv:', e)
    return out


_m = {}
def fm(color):
    key = tuple(round(c, 4) for c in color)
    if key not in _m:
        m = bpy.data.materials.new('f')
        m.diffuse_color = (*color, 1)
        _m[key] = m
    return _m[key]


Z = [0.0]
def _z():
    Z[0] += 0.0005
    return Z[0]


def rect(x0, y0, x1, y1, color):
    bm = bmesh.new()
    z = _z()
    vs = [bm.verts.new(p) for p in ((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z))]
    bm.faces.new(vs)
    me = bpy.data.meshes.new('r'); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new('r', me); bpy.context.scene.collection.objects.link(ob)
    me.materials.append(fm(color))
    return ob


def poly(pts, color):
    bm = bmesh.new()
    z = _z()
    vs = [bm.verts.new((p[0], p[1], z)) for p in pts]
    bm.faces.new(vs)
    me = bpy.data.meshes.new('p'); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new('p', me); bpy.context.scene.collection.objects.link(ob)
    me.materials.append(fm(color))
    return ob


def line(a, b, w, color):
    dx, dy = b[0] - a[0], b[1] - a[1]
    n = math.hypot(dx, dy) or 1.0
    px, py = -dy / n * w / 2, dx / n * w / 2
    return poly([(a[0] + px, a[1] + py), (b[0] + px, b[1] + py), (b[0] - px, b[1] - py), (a[0] - px, a[1] - py)], color)


def txt(body, x, y, size, color, align='LEFT'):
    cu = bpy.data.curves.new('t', 'FONT')
    cu.body = body; cu.size = size; cu.align_x = align; cu.align_y = 'CENTER'
    f = L.font()
    if f:
        cu.font = f
    ob = bpy.data.objects.new('t', cu); bpy.context.scene.collection.objects.link(ob)
    ob.location = (x, y, _z() + 0.01)
    cu.materials.append(fm(color))
    return ob


def mix(c, k, to=(0, 0, 0)):
    return tuple(c[i] * k + to[i] * (1 - k) for i in range(3))


def main():
    L.reset_scene()
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'FLAT'
    sh.color_type = 'MATERIAL'
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x, sc.render.resolution_y = W_PX, H_PX
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = 'PNG'
    try:
        sc.display.render_aa = '16'
    except Exception:
        pass
    w = bpy.data.worlds.new('w'); w.color = (0.012, 0.013, 0.016); sc.world = w

    WHITE = (0.9, 0.9, 0.88); GREY = (0.42, 0.44, 0.47); DIM = (0.2, 0.21, 0.23); ORANGE = (1.0, 0.45, 0.08)
    f0 = f0_means()
    lx = -15.75

    # ---------------------------------------------------------------- titulo
    txt('IRON REQUIEM · MAP01 · RUTA DE SUBIDA: de las profundidades a la salida', -15.75, 8.56, 0.42, ORANGE)
    txt('Sección esquemática en orden de juego (una columna por cuarto; no es el plano real). Cotas con la opción O1 «invertir cotas»: mismo orden de cuartos y de encuentros.',
        -15.75, 8.1, 0.215, WHITE)

    # ------------------------------------------------------------- geometria
    x0, cw = -13.35, 1.5
    yb, sy = -0.05, 0.097
    cols = [('C-04', ('Hangar de', 'cápsulas'), 'Hangar C-04 (prólogo)', 1, 0, 0, 16, '#31486B', 1, 0, 0, None, 'Room00_Hangar')] + ROOMS
    n = len(cols)
    xe = x0 + n * cw
    ysurf = yb + SURFACE * sy

    # tierra, cielo y bandas de tramo
    rect(-16, ysurf, 16, ysurf + 0.4, srgb('#5E7186'))
    txt('SUPERFICIE  ·  exterior, restos de la guerra', xe - 0.1, ysurf + 0.2, 0.21, (0.93, 0.95, 1.0), 'RIGHT')
    txt('≈ +65 m sobre el hangar (propuesta de rotulación: solo pintura)', x0 + 0.08, ysurf + 0.2, 0.18, (0.88, 0.92, 0.98))
    spans = {}
    for i, c in enumerate(cols):
        spans.setdefault(c[3], [i, i])[1] = i
    for t, (a, b) in spans.items():
        name, sub, hexc = TRAMOS[t]
        col = srgb(hexc)
        xa, xb = x0 + a * cw, x0 + (b + 1) * cw
        rect(xa + 0.03, yb - 0.25, xb - 0.03, ysurf, mix(col, 0.07, (0.03, 0.03, 0.035)))
        rect(xa + 0.03, ysurf - 0.06, xb - 0.03, ysurf, mix(col, 0.5))
        rect(xa + 0.03, 6.98, xb - 0.03, 7.03, col)
        txt(name, xa + 0.1, 7.55, 0.29, col)
        txt(sub, xa + 0.1, 7.24, 0.18, WHITE)

    # escala vertical
    for m_ in range(0, 70, 10):
        y = yb + m_ * sy
        rect(x0 - 0.12, y - 0.006, xe, y + 0.006, (0.09, 0.095, 0.105))
        txt('+%d m' % m_ if m_ else '0 m', x0 - 0.2, y, 0.17, GREY, 'RIGHT')
    txt('COTA O1', lx, yb + 34 * sy, 0.19, WHITE)
    txt('m sobre el', lx, yb + 31 * sy, 0.15, GREY)
    txt('suelo del hangar', lx, yb + 28.6 * sy, 0.15, GREY)

    path = []
    for i, c in enumerate(cols):
        rid, name2, old, tramo, cota, climb, ceil, hexc, D, M, E, target, key = c
        col = srgb(hexc)
        xa, xb = x0 + i * cw + 0.07, x0 + (i + 1) * cw - 0.07
        y0 = yb + cota * sy
        y1 = y0 + ceil * sy
        rect(xa, y0, xb, y1, mix(col, 0.42, (0.02, 0.02, 0.025)))
        rect(xa, y1 - 0.045, xb, y1, mix(col, 0.25))                 # techo
        rect(xa, y0 - 0.07, xb, y0, mix(col, 0.95))                   # suelo
        yout = y0 + climb * sy
        if climb:
            xm = xa + (xb - xa) * 0.5
            poly([(xa + 0.12, y0), (xm, yout), (xb, yout), (xb, y0)], mix(col, 0.68, (0.02, 0.02, 0.025)))
            rect(xm, yout - 0.05, xb, yout, mix(col, 0.95))
            path += [(xa, y0 + 0.11), (xa + 0.12, y0 + 0.11), (xm, yout + 0.11), (xb, yout + 0.11)]
        else:
            path += [(xa, y0 + 0.11), (xb, y0 + 0.11)]
        if i < n - 1:                                                # esclusa plana a la cota de salida
            rect(xb, yout - 0.07, xb + 0.14, yout + 0.2, (0.3, 0.31, 0.33))
        # rotulos de cota
        lab = ('0' if rid == 'C-04' else ('+%d' % cota if cota else '0')) + (' → +%d' % (cota + climb) if climb else '')
        txt(lab + (' m' if not climb else ''), (xa + xb) / 2, y1 + 0.17, 0.185, WHITE, 'CENTER')
        if rid == 'C-04':
            txt('prólogo', (xa + xb) / 2, y0 + 1.1, 0.16, WHITE, 'CENTER')
            txt('protegido', (xa + xb) / 2, y0 + 0.85, 0.16, WHITE, 'CENTER')
            txt('sin cambios', (xa + xb) / 2, y0 + 0.6, 0.16, WHITE, 'CENTER')
        if rid == 'R18':                                              # compuerta y salida
            rect(xb - 0.1, y0, xb - 0.02, y0 + 7 * sy, srgb('#EAF2FB'))
            line((xb - 0.02, y0 + 0.2), (xb + 0.5, ysurf), 0.05, srgb('#EAF2FB'))
            txt('SALIDA', xb + 0.16, y0 + 9 * sy, 0.2, srgb('#EAF2FB'))
        # rotulos bajo la seccion
        xc = (xa + xb) / 2
        txt(rid, xc, yb - 0.58, 0.3, WHITE, 'CENTER')
        bright = mix(col, 0.75, (1, 1, 1))
        txt(name2[0], xc, yb - 0.98, 0.19, bright, 'CENTER')
        if name2[1]:
            txt(name2[1], xc, yb - 1.23, 0.19, bright, 'CENTER')
        if rid != 'C-04':
            txt(old, xc, yb - 1.6, 0.155, (0.6, 0.62, 0.65), 'CENTER')
        if rid in CURRENT:
            txt(('%d m' % CURRENT[rid]).replace('-', '−'), xc, yb - 1.88, 0.17, (0.6, 0.62, 0.65), 'CENTER')
    # recorrido del jugador
    for a, b in zip(path[:-1], path[1:]):
        line(a, b, 0.035, (1.0, 1.0, 1.0))
    txt('NOMBRE', lx, yb - 0.98, 0.19, WHITE)
    txt('PROPUESTO', lx, yb - 1.23, 0.19, WHITE)
    txt('nombre actual', lx, yb - 1.6, 0.15, GREY)
    txt('cota actual (baja)', lx, yb - 1.88, 0.15, GREY)
    txt('línea blanca:', lx, yb + 6 * sy, 0.15, WHITE)
    txt('recorrido', lx, yb + 3.6 * sy, 0.15, WHITE)

    # ------------------------------------------------------------------- luz
    yl0, ls = -4.85, 0.026          # base y escala (unidades por nivel 0-255)
    txt('LUZ DE FONDO', lx, yl0 + 2.0, 0.2, WHITE)
    txt('escala 0–255', lx, yl0 + 1.74, 0.15, GREY)
    txt('color: objetivo', lx, yl0 + 0.86, 0.15, GREY)
    txt('(indicativo)', lx, yl0 + 0.64, 0.15, GREY)
    txt('blanco: medido', lx, yl0 + 0.36, 0.15, WHITE)
    txt('hoy en F0', lx, yl0 + 0.14, 0.15, WHITE)
    rect(x0, yl0 - 0.012, xe, yl0, DIM)
    ymin = yl0 + MIN_LEGIBLE * ls
    xx = x0
    while xx < xe:                                                   # linea discontinua del minimo legible
        rect(xx, ymin - 0.012, min(xx + 0.22, xe), ymin + 0.012, srgb('#F2C84B'))
        xx += 0.36
    txt('mínimo legible %d' % MIN_LEGIBLE, lx, ymin, 0.165, srgb('#F2C84B'))
    for i, c in enumerate(cols):
        rid, name2, old, tramo, cota, climb, ceil, hexc, D, M, E, target, key = c
        col = srgb(hexc)
        xa, xb = x0 + i * cw + 0.07, x0 + (i + 1) * cw - 0.07
        now = f0.get(key)
        if target is not None:
            rect(xa + 0.18, yl0, xb - 0.42, yl0 + target * ls, col)
            txt('%d' % target, (xa + 0.18 + xb - 0.42) / 2, yl0 + target * ls + 0.17, 0.2, mix(col, 0.8, (1, 1, 1)), 'CENTER')
        if now is not None:
            rect(xb - 0.36, yl0, xb - 0.14, yl0 + now * ls, WHITE)
            txt(('%.0f' % now), xb - 0.25, yl0 + now * ls + 0.15, 0.17, WHITE, 'CENTER')
        if target is None:
            txt('no se', xa + 0.45, yl0 + 1.0, 0.15, GREY, 'CENTER')
            txt('toca', xa + 0.45, yl0 + 0.78, 0.15, GREY, 'CENTER')

    # ------------------------------------------------- deterioro / robots / exterior
    rows = [('DETERIORO', 'D0 intacto … D3 tomado', 8, srgb('#C9B79A')),
            ('OCUPACIÓN ROBOT', 'chatarra, cables, nidos', 9, srgb('#E08A4A')),
            ('SEÑAL DEL EXTERIOR', 'polvo → tierra → luz día', 10, srgb('#D5E4F5'))]
    for r, (title, sub, idx, colr) in enumerate(rows):
        y = -5.75 - r * 0.72
        txt(title, lx, y + 0.12, 0.185, colr)
        txt(sub, lx, y - 0.14, 0.158, (0.6, 0.62, 0.65))
        for i, c in enumerate(cols):
            v = c[idx]
            xa, xb = x0 + i * cw + 0.07, x0 + (i + 1) * cw - 0.07
            xc = (xa + xb) / 2
            for k in range(3):
                px = xc - 0.48 + k * 0.34
                rect(px, y - 0.13, px + 0.28, y + 0.15, colr if k < v else (0.07, 0.075, 0.085))
    # leyenda
    for k, ln in enumerate((
        'Luz: la barra de color es un objetivo INDICATIVO de dirección de arte (más luz real al subir, nunca por debajo de 45). Los valores definitivos son de la sección a) Luminosidad.',
        'Barra blanca = media del cuadro medida hoy en F0 (zones.csv, tomas vacías). Mínimo legible 45 = carriles y spawns según 05_ILUMINACION §L1.3.',
        'Rampa dibujada = cuartos con subida interior: R02 +12, R06 +6, R08 +4, R11 +6, R16 +6 y R17 +3 = 37 m. Hoy esos seis se recorren de arriba abajo; los otros doce son planos.')):
        txt(ln, -15.75, -7.95 - k * 0.3, 0.19, (0.6, 0.62, 0.65))

    cd = bpy.data.cameras.new('c'); cd.type = 'ORTHO'; cd.ortho_scale = W_U
    co = bpy.data.objects.new('c', cd); sc.collection.objects.link(co)
    co.location = (0, 0, 5)
    sc.camera = co
    out = os.path.join(L.OUT, 'ruta_subida.png')
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print('VPF render ->', out, L.image_stats(out))


if __name__ == '__main__':
    main()
