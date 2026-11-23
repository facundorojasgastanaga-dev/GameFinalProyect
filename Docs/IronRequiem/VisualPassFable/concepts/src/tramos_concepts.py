"""IRON REQUIEM - Pase visual con Fable (F1 d): tres laminas de ambiente, una por tramo.

  tramo_1_profundidades.png   laboratorio / experimentacion (capsulas, banco de pruebas, luz de emergencia fria)
  tramo_2_infraestructura.png almacen tomado por robots (nido de reparacion, chatarra apilada, cables arrancados)
  tramo_3_superficie.png      antesala de la salida (brecha con luz natural fria, tierra, restos de guerra, compuerta)

Cada lamina es una sala tipo de 40 x 30 x 8 m en bloques, con 3 landmarks numerados, una silueta de
enemigo de 2 m y el espacio de combate marcado en el suelo. Son maquetas de direccion de arte, no assets.

Ejecucion:
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --factory-startup --python tramos_concepts.py
  ... --python tramos_concepts.py -- 2        (solo el tramo 2)
"""
import bpy, sys, os, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fable_lib as L
from fable_lib import srgb, mat, box, box_mm, cyl, cyl_between, text

CAM_LOC = (-3.0, -40.0, 15.5)
CAM_TGT = (0.0, 3.5, -3.3)
FOV = 62

HAZARD = ('#C9A227', '#15130F')


def stencil_mat():
    return mat('stencil', (0.7, 0.7, 0.66), 0.9, emit=(0.8, 0.8, 0.74), strength=0.45)


def hazard_strip(name, mn, mx, n=8):
    """Franja amarilla/negra de pintura (no emisiva)."""
    ya = mat('haz_y', srgb(HAZARD[0]), 0.8)
    bl = mat('haz_b', srgb(HAZARD[1]), 0.8)
    long_x = (mx[0] - mn[0]) >= (mx[1] - mn[1])
    for i in range(n):
        if long_x:
            a = mn[0] + (mx[0] - mn[0]) * i / n; b = mn[0] + (mx[0] - mn[0]) * (i + 1) / n
            box_mm('%s_%d' % (name, i), (a, mn[1], mn[2]), (b, mx[1], mx[2]), ya if i % 2 == 0 else bl)
        else:
            a = mn[1] + (mx[1] - mn[1]) * i / n; b = mn[1] + (mx[1] - mn[1]) * (i + 1) / n
            box_mm('%s_%d' % (name, i), (mn[0], a, mn[2]), (mx[0], b, mx[2]), ya if i % 2 == 0 else bl)


# ============================================================ TRAMO 1 ======

def capsule(name, x, y, lit=0.0, broken=False, face=(0, -1)):
    """Capsula de estasis de pie: 1,4 x 1,4 x 3,0 m. lit = emision del visor (0 apagada)."""
    shell = mat('caps_shell', srgb('#3B4650'), 0.45, 0.7)
    white = mat('caps_white', srgb('#AEB8BD'), 0.5, 0.1)
    glass_off = mat('caps_glass_off', (0.012, 0.016, 0.02), 0.15, 0.0)
    cyl(name + '_base', 0.78, 0.5, (x, y, 0.25), shell, seg=18)
    cyl(name + '_cap', 0.78, 0.45, (x, y, 2.78), shell, seg=18)
    cyl(name + '_back', 0.72, 2.1, (x - face[0] * 0.12, y - face[1] * 0.12, 1.55), white, seg=18)
    g = mat('caps_glass_%d' % int(lit * 10), (0.01, 0.014, 0.018), 0.2, emit=srgb('#9FD4FF'), strength=lit) if lit > 0 else glass_off
    if not broken:
        box(name + '_glass', (0.95, 0.12, 1.9) if face[1] else (0.12, 0.95, 1.9),
            (x + face[0] * 0.66, y + face[1] * 0.66, 1.55), g)
    else:
        # puerta arrancada, apoyada en el suelo: primera marca de ocupacion robot
        box(name + '_hole', (0.9, 0.1, 1.85) if face[1] else (0.1, 0.9, 1.85),
            (x + face[0] * 0.62, y + face[1] * 0.62, 1.55), mat('caps_void', (0.003, 0.003, 0.004), 1.0))
        box(name + '_door', (0.95, 0.12, 1.9), (x + face[0] * 1.5 + 0.5, y + face[1] * 1.5, 0.12), shell, rot=(82, 0, 24))
    # umbilical al techo
    cyl_between(name + '_umb', (x, y, 3.0), (x, y, 5.5), 0.09, mat('cable', (0.012, 0.012, 0.014), 0.7), 6)


def build_tramo1(out):
    L.reset_scene()
    pal = dict(floor=srgb('#2F363D'), wall=srgb('#414B55'), trim=srgb('#232A31'), cover=srgb('#5A6068'))
    L.setup_stage(world=(0.010, 0.014, 0.020), haze=0.0025, haze_color=(0.8, 0.9, 1.0))
    L.room_shell(pal, door_x=2.0)
    L.door_frame(2.0, label='LAB-03  >>  BANCOS DE PRUEBAS')
    cam = L.camera('Cam', CAM_LOC, CAM_TGT, FOV)
    cable = mat('cable', (0.012, 0.012, 0.014), 0.7)
    steel = mat('steel_dark', srgb('#39424A'), 0.5, 0.7)
    ice = mat('ice_strip', (0, 0, 0), 0.4, emit=srgb('#9FD4FF'), strength=3.2)

    # --- luz de emergencia fria: tiras de zocalo (emisivo bajo) y dos luminarias de techo debiles
    for (mn, mx) in (((-19.8, 14.78, 1.2), (-3.0, 14.86, 1.32)), ((7.0, 14.78, 1.2), (19.8, 14.86, 1.32)),
                     ((-19.86, -14, 1.2), (-19.78, 14.5, 1.32)), ((19.78, -14, 1.2), (19.86, 14.5, 1.32))):
        box_mm('IceStrip', mn, mx, ice)
    L.light('EmergA', 'AREA', 2800, (-9, 2, 7.8), (0.62, 0.80, 1.0), size=5)
    L.light('EmergB', 'AREA', 2800, (10, 1, 7.8), (0.62, 0.80, 1.0), size=5)
    L.light('EmergC', 'AREA', 1900, (0, -11, 7.8), (0.70, 0.84, 1.0), size=6)
    L.light('WallWash', 'AREA', 1500, (0, 9, 2.0), (0.6, 0.82, 1.0), rot=(-80, 0, 0), size=14)

    # --- LANDMARK 1: fila de capsulas (muro del fondo, lado oeste) + bateria en el muro oeste
    xs = [-17.5, -15.2, -12.9, -8.7, -6.4, -4.1]
    for i, x in enumerate(xs):
        capsule('CapN%d' % i, x, 13.2, lit=(2.4 if i in (1,) else 0.5 if i == 4 else 0.0), broken=(i == 3))
    for i, y in enumerate((9.2, 6.8, 2.2, -0.2, -2.6)):
        capsule('CapW%d' % i, -18.3, y, lit=(1.6 if i == 2 else 0.0), face=(1, 0))
    box_mm('CapPlinthN', (-19.5, 12.2, 0), (-2.8, 14.5, 0.12), steel)
    hazard_strip('CapHazN', (-19.5, 12.05, 0.0), (-2.8, 12.2, 0.125), 16)
    text('CapStencil', 'SUJETOS 01-12  ·  CRIOSTASIS', (-11.0, 14.45, 6.5), 0.95, stencil_mat())
    box_mm('CapTray', (-19.5, 12.9, 5.5), (-2.8, 13.5, 5.7), steel)
    L.light('CapGlow', 'POINT', 260, (-15.2, 12.0, 1.6), srgb('#9FD4FF'), size=0.3)

    # --- LANDMARK 2: banco de pruebas con portico (lado este), con un chasis colgado y oscuro
    gx, gy = 13.5, 2.5
    box_mm('RigBase', (gx - 3.2, gy - 2.6, 0), (gx + 3.2, gy + 2.6, 0.25), steel)
    hazard_strip('RigHaz', (gx - 3.2, gy - 2.8, 0.0), (gx + 3.2, gy - 2.6, 0.26), 10)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box_mm('RigPost', (gx + sx * 2.9 - 0.18, gy + sy * 2.3 - 0.18, 0.25), (gx + sx * 2.9 + 0.18, gy + sy * 2.3 + 0.18, 5.4), steel)
    box_mm('RigBeamA', (gx - 3.1, gy - 2.5, 5.2), (gx + 3.1, gy - 2.1, 5.6), steel)
    box_mm('RigBeamB', (gx - 3.1, gy + 2.1, 5.2), (gx + 3.1, gy + 2.5, 5.6), steel)
    box_mm('RigBeamC', (gx - 0.25, gy - 2.5, 5.2), (gx + 0.25, gy + 2.5, 5.6), steel)
    dark = mat('wreck', (0.02, 0.02, 0.022), 0.75, 0.2)
    box('RigTorso', (0.9, 0.55, 0.75), (gx, gy, 3.6), dark)
    box('RigHead', (0.36, 0.36, 0.28), (gx, gy, 4.15), dark)
    cyl_between('RigHang', (gx, gy, 4.3), (gx, gy, 5.2), 0.05, cable, 6)
    for sx in (-1, 1):   # brazos de ensayo articulados
        cyl_between('RigArm1', (gx + sx * 2.7, gy, 3.0), (gx + sx * 1.6, gy, 3.9), 0.12, steel, 8)
        cyl_between('RigArm2', (gx + sx * 1.6, gy, 3.9), (gx + sx * 0.7, gy, 3.5), 0.09, steel, 8)
    box_mm('RigConsole', (gx + 3.6, gy - 1.0, 0), (gx + 4.6, gy + 1.0, 1.15), steel)
    box('RigScreen', (0.08, 1.5, 0.7), (gx + 3.62, gy, 1.6), mat('scr_ice', (0, 0, 0), 0.3, emit=srgb('#9FD4FF'), strength=1.4))

    # --- LANDMARK 3: muro de registros (muro del fondo, lado este): monitores muertos + estarcido
    scr_off = mat('scr_off', (0.006, 0.008, 0.01), 0.2)
    scr_on = mat('scr_on', (0, 0, 0), 0.3, emit=srgb('#9FD4FF'), strength=1.5)
    for r in range(3):
        for c in range(6):
            lit = (r, c) in ((1, 2), (0, 4))
            box('Reg_%d_%d' % (r, c), (1.5, 0.12, 0.9), (8.6 + c * 1.8, 14.38, 2.1 + r * 1.1), scr_on if lit else scr_off)
    box_mm('RegFrame', (7.6, 14.42, 1.5), (18.9, 14.5, 4.95), steel)
    text('RegStencil', 'REGISTRO DE PROYECTO', (13.2, 14.45, 6.2), 0.8, stencil_mat())
    for c in range(5):
        box_mm('RegDesk%d' % c, (8.0 + c * 2.2, 13.2, 0), (9.6 + c * 2.2, 14.3, 1.1), steel)

    # --- marcas de ocupacion robot (minimas en este tramo): cables arrancados y un monton S
    L.hanging_cables('Torn', (-1.2, 12.5, 7.6), [(-2.1, 11.2, 4.7), (-0.4, 11.6, 5.2), (-1.4, 10.6, 4.9)], cable, sag=0.5)
    L.scrap_pile('ScrapS', (-18.2, -10.5), 1.6, 1.3, 11, [dark, mat('rust', srgb('#3A2A22'), 0.8, 0.3)], n=22, wall_dir=(-1, 0))

    # --- techo: cerchas y bandejas (decoracion colgante >= 4,5 m)
    tr = mat('truss', srgb('#20262C'), 0.7, 0.5)
    # (las cerchas de techo del kit no se dibujan en la lamina: taparian la maqueta desde esta camara)
    L.pipe_run('TrayPipe', (-20, 14.2, 7.5), (20, 14.2, 7.5), 0.22, tr, 8)

    # --- estructura de combate (no cambia) + enemigo de escala + espacio despejado
    L.cover('CoverA', (-6.5, -2.0, 0), (-3.5, -0.8, 1.2), pal)
    L.cover('CoverB', (3.5, 3.5, 0), (6.5, 4.7, 1.2), pal)
    L.cover('CoverH', (-1.5, -8.5, 0), (1.5, -7.3, 2.4), pal)
    L.combat_zone((-14.5, -12.5), (8.6, 10.6))
    L.robot('Enemy', (-1.0, 4.5, 0), yaw=8)
    L.light('EnemyKey', 'SPOT', 2600, (-1.0, -3.0, 7.5), (0.8, 0.9, 1.0), rot=(48, 0, 0), size=0.5, spot=38)

    L.marker(1, (-11.0, 11.4, 1.2), cam)
    L.marker(2, (13.5, -1.2, 1.2), cam)
    L.marker(3, (9.0, 11.2, 1.2), cam)

    L.caption(cam, 'TRAMO 1 · PROFUNDIDADES  —  sala tipo «Criostasis de serie» (40 × 30 × 8 m)', [
        '1  Fila de cápsulas de estasis: muertas, una encendida y una reventada desde fuera',
        '2  Banco de pruebas con pórtico: chasis colgado, oscuro y sin emisivo',
        '3  Muro de registros: monitores apagados y estarcido del proyecto (rastro humano)',
        'Luz: emergencia fría (zócalo azul hielo) · Robots: marca mínima · Exterior: ninguna',
    ], [('grafito', srgb('#232A31')), ('acero', srgb('#414B55')), ('cerámica', srgb('#AEB8BD')), ('hielo', srgb('#9FD4FF')), ('peligro', srgb('#C9A227'))],
        FOV, note='Maqueta de bloques (Blender/Eevee, script reproducible). Rojo solo en la barra de estado de la puerta y en el ojo del enemigo (2 m).')
    return L.render(out)


# ============================================================ TRAMO 2 ======

def container(name, mn, mx, m):
    box_mm(name, mn, mx, m, bevel=0.04)
    rib = mat('cont_rib', tuple(c * 0.6 for c in m.diffuse_color[:3]), 0.8, 0.4)
    n = int((mx[0] - mn[0]) / 0.6)
    for i in range(1, n):
        x = mn[0] + i * (mx[0] - mn[0]) / n
        box_mm(name + '_r%d' % i, (x - 0.04, mn[1] - 0.03, mn[2] + 0.15), (x + 0.04, mn[1], mx[2] - 0.15), rib)


def build_tramo2(out):
    L.reset_scene()
    pal = dict(floor=srgb('#463C31'), wall=srgb('#5A4D3F'), trim=srgb('#2F261E'), cover=srgb('#6A645C'))
    L.setup_stage(world=(0.020, 0.015, 0.011), haze=0.0012, haze_color=(1.0, 0.9, 0.78))
    L.room_shell(pal, door_x=4.0)
    L.door_frame(4.0, label='ALM-08  >>  MANTENIMIENTO')
    cam = L.camera('Cam', CAM_LOC, CAM_TGT, FOV)
    cable = mat('cable', (0.010, 0.010, 0.011), 0.7)
    dark = mat('wreck', (0.022, 0.021, 0.02), 0.75, 0.25)
    rust = mat('rust', srgb('#4A2E20'), 0.85, 0.3)
    rust2 = mat('rust2', srgb('#33261F'), 0.85, 0.3)
    steel = mat('steel_dark', srgb('#3B3833'), 0.55, 0.7)

    # --- luz: incandescente sucia (blanco calido, no ambar de puerta)
    L.light('WarmA', 'AREA', 11500, (-12, -2, 7.8), (1.0, 0.80, 0.56), size=5)
    L.light('WarmB', 'AREA', 11500, (8, 4, 7.8), (1.0, 0.80, 0.56), size=5)
    L.light('WarmC', 'AREA', 6800, (0, -11, 7.8), (1.0, 0.84, 0.64), size=6)
    L.light('WallWash', 'AREA', 2600, (0, 9, 2.2), (1.0, 0.82, 0.6), rot=(-80, 0, 0), size=16)
    lamp = mat('lamp_warm', (0, 0, 0), 0.4, emit=(1.0, 0.80, 0.56), strength=5.0)
    for (x, y) in ((-12, -2), (8, 4)):
        box('LampBox', (1.6, 0.5, 0.12), (x, y, 7.6), lamp)
        cyl_between('LampRod', (x, y, 7.66), (x, y, 8.0), 0.03, steel, 6)

    # --- LANDMARK 1: nido de reparacion (aro de chatarra + portico con brazos colgantes + chispa de soldadura)
    nx, ny = -13.0, 6.2
    for i in range(14):
        a = i / 14 * math.tau
        box('NestRing%d' % i, (1.5, 0.5, 0.5 + (i % 3) * 0.2), (nx + math.cos(a) * 3.0, ny + math.sin(a) * 2.6, 0.3), rust if i % 2 else dark,
            rot=((i * 37) % 25 - 12, (i * 53) % 20 - 10, math.degrees(a) + 90))
    legs = [(nx - 2.4, ny - 1.9), (nx + 2.5, ny - 1.6), (nx + 0.2, ny + 2.6)]
    top = (nx, ny, 5.6)
    for i, (lx, ly) in enumerate(legs):
        cyl_between('NestLeg%d' % i, (lx, ly, 0), top, 0.13, steel, 8)
    cyl('NestHub', 0.55, 0.4, top, steel, seg=12)
    for i, (dx, dy, lz) in enumerate(((-0.9, -0.4, 2.2), (0.8, -0.7, 2.9), (0.2, 0.9, 1.9))):
        cyl_between('NestArmA%d' % i, (nx + dx * 0.4, ny + dy * 0.4, 5.4), (nx + dx, ny + dy, 4.0), 0.08, steel, 8)
        cyl_between('NestArmB%d' % i, (nx + dx, ny + dy, 4.0), (nx + dx * 0.5, ny + dy * 0.5, lz), 0.06, steel, 8)
    box('NestPatient', (0.9, 0.55, 0.7), (nx, ny, 1.25), dark, rot=(0, 78, 30))        # robot a medio reparar, tumbado en la cuna
    box_mm('NestCradle', (nx - 1.0, ny - 0.6, 0), (nx + 1.0, ny + 0.6, 0.85), rust2)
    weld = mat('weld', (0, 0, 0), 0.4, emit=srgb('#CFE6FF'), strength=40.0)
    cyl('WeldSpark', 0.16, 0.16, (nx + 0.25, ny - 0.3, 1.8), weld, seg=8)
    L.light('WeldLight', 'POINT', 5000, (nx + 0.25, ny + 0.1, 1.9), srgb('#CFE6FF'), size=0.1)
    L.hanging_cables('NestCab', (nx, ny, 7.7), [top, (nx - 2.0, ny + 1.5, 5.0), (nx + 2.2, ny + 0.8, 4.8)], cable, sag=0.3, r=0.06)

    # --- LANDMARK 2: montana de chatarra canibalizada contra el muro este (hasta 5,5 m)
    L.scrap_pile('ScrapL', (18.6, 2.0), 6.2, 5.6, 7, [dark, rust, rust2, steel], n=150, wall_dir=(1, 0))
    L.wreck_limbs('ScrapWreckA', (13.8, -2.2), 3, dark, 3)
    L.wreck_limbs('ScrapWreckB', (14.6, 6.4), 5, dark, 2)

    # --- LANDMARK 3: tuberia maestra arrancada (muro del fondo) + contenedores numerados (rastro humano)
    pipe = mat('pipe', srgb('#3F4B4A'), 0.6, 0.6)
    L.pipe_run('MainPipeW', (-20, 14.1, 5.6), (-4.2, 14.1, 5.6), 0.62, pipe, 6)
    L.pipe_run('MainPipeE', (10.5, 14.1, 5.6), (20, 14.1, 5.6), 0.62, pipe, 4)
    cyl_between('PipeTornA', (-4.2, 14.1, 5.6), (-2.6, 13.3, 4.3), 0.62, pipe, 14)     # tramo doblado hacia abajo
    L.hanging_cables('PipeCab', (-3.0, 14.0, 7.6), [(-3.6, 13.0, 4.8), (-1.6, 13.4, 5.3), (0.4, 13.6, 4.6), (-2.4, 12.6, 5.6)], cable, sag=0.7)
    L.hanging_cables('PipeCab2', (9.5, 14.0, 7.6), [(9.0, 13.2, 5.0), (10.6, 13.0, 4.7)], cable, sag=0.5)
    stain = mat('stain', srgb('#1F2A2A'), 0.3)
    L.quad('Puddle', [(-5.5, 10.6, 0.011), (-0.5, 10.2, 0.011), (0.6, 13.6, 0.011), (-4.8, 14.2, 0.011)], stain)
    kraft = mat('kraft', srgb('#8A6B43'), 0.8, 0.2)
    olive = mat('olive', srgb('#4D5B45'), 0.8, 0.2)
    container('ContA', (-19.4, 11.4, 0), (-13.4, 13.9, 2.6), kraft)
    container('ContB', (-19.4, 11.6, 2.6), (-14.2, 13.9, 5.2), olive)
    text('ContStencil', 'B-12', (-16.4, 11.36, 1.4), 0.9, stencil_mat())
    text('WallStencil', 'ALM-08  ·  INVENTARIO', (-6.6, 14.45, 3.3), 1.0, stencil_mat())

    # --- primera senal del exterior: grieta alta en el muro del fondo y cono de polvo debajo
    crack = mat('crack', (0.004, 0.004, 0.004), 1.0, emit=srgb('#D8CDB6'), strength=0.22)
    pts = [(14.2, 7.9), (14.6, 7.0), (14.1, 6.3), (14.9, 5.5), (14.5, 4.9), (15.3, 4.2)]
    for i in range(len(pts) - 1):
        cyl_between('Crack%d' % i, (pts[i][0], 14.95, pts[i][1]), (pts[i + 1][0], 14.95, pts[i + 1][1]), 0.07, crack, 5)
    dust = mat('dust', srgb('#B9AE98'), 0.95)
    cyl('DustCone', 1.3, 0.45, (15.0, 13.7, 0.225), dust, r2=0.05, seg=16)
    L.frustum_beam('DustFall', (15.0, 14.7, 4.6), (0.15, 0.12), (15.0, 13.8, 0.45), (0.8, 0.7), L.beam_mat('dustfall', srgb('#D8CDB6'), 0.8, 0.018))

    # --- techo: cerchas; un conducto reventado
    tr = mat('truss', srgb('#261F19'), 0.7, 0.5)
    # (las cerchas de techo del kit no se dibujan en la lamina: taparian la maqueta desde esta camara)
    box_mm('DuctA', (-19.8, -12, 6.0), (-18.8, 2.0, 6.8), steel)
    box('DuctTorn', (1.0, 4.0, 0.8), (-19.0, 3.6, 5.5), steel, rot=(-20, 0, 0))

    # --- estructura de combate + enemigo + espacio despejado
    L.cover('CoverA', (-4.5, -4.0, 0), (-1.5, -2.8, 1.2), pal)
    L.cover('CoverB', (4.0, 1.5, 0), (7.0, 2.7, 1.2), pal)
    L.cover('CoverH', (-1.5, -9.5, 0), (1.5, -8.3, 2.4), pal)
    L.combat_zone((-9.0, -12.5), (10.5, 9.0))
    L.robot('Enemy', (1.5, 6.0, 0), yaw=-6)
    L.light('EnemyKey', 'SPOT', 3000, (1.5, -2.0, 7.5), (1.0, 0.86, 0.68), rot=(48, 0, 0), size=0.5, spot=36)

    L.marker(1, (nx + 1.0, ny - 4.6, 1.2), cam)
    L.marker(2, (12.0, -2.6, 1.2), cam)
    L.marker(3, (-3.0, 11.6, 1.2), cam)

    L.caption(cam, 'TRAMO 2 · INFRAESTRUCTURA  —  sala tipo «Almacén tomado» (40 × 30 × 8 m)', [
        '1  Nido de reparación: aro de chatarra, pórtico con brazos y chispa de soldadura',
        '2  Montaña de chatarra canibalizada: oscura, sin emisivo, fuera de los carriles',
        '3  Tubería maestra arrancada, cables colgando (≥ 4,5 m) y contenedores numerados',
        'Luz: incandescente sucia · Robots: ocupación alta · Exterior: grieta y cono de polvo',
    ], [('hollín', srgb('#2F261E')), ('hormigón', srgb('#5A4D3F')), ('kraft', srgb('#8A6B43')), ('óxido', srgb('#4A2E20')), ('soldadura', srgb('#CFE6FF'))],
        FOV, note='Maqueta de bloques (Blender/Eevee, script reproducible). La chispa del nido es un emisivo + 1 luz de sala, no una luz por robot.')
    return L.render(out)


# ============================================================ TRAMO 3 ======

def hedgehog(name, x, y, m, s=1.0):
    for i, rot in enumerate(((45, 0, 20), (-45, 0, 80), (0, 45, 140))):
        box('%s_%d' % (name, i), (0.16 * s, 0.16 * s, 2.2 * s), (x, y, 0.75 * s), m, rot=rot)


def build_tramo3(out):
    L.reset_scene()
    pal = dict(floor=srgb('#605C55'), wall=srgb('#716D66'), trim=srgb('#4A4741'), cover=srgb('#585856'))
    L.setup_stage(world=(0.030, 0.036, 0.046), haze=0.0035, haze_color=(0.86, 0.92, 1.0))
    gate_x, gate_w, gate_h = 3.0, 14.0, 7.0
    # techo con una brecha (hueco entre losas) sobre el lado oeste
    L.room_shell(pal, door_x=gate_x, door_w=gate_w, door_h=gate_h,
                 ceiling=[((-20, 6.5, 8.0), (-4.5, 11, 8.6)), ((-20, -1.5, 8.0), (-15.5, 6.5, 8.6)), ((-7.5, -1.5, 8.0), (-4.5, 6.5, 8.6)), ((-20, -5.0, 8.0), (-4.5, -1.5, 8.6))])
    cam = L.camera('Cam', CAM_LOC, CAM_TGT, FOV)
    cable = mat('cable', (0.012, 0.012, 0.013), 0.7)
    dark = mat('wreck', (0.03, 0.03, 0.03), 0.8, 0.2)
    steel = mat('steel_dark', srgb('#45474A'), 0.55, 0.7)
    earth = mat('earth', srgb('#5A4838'), 0.95)
    earth2 = mat('earth2', srgb('#6E5B48'), 0.95)
    rubble = mat('rubble', srgb('#8C887F'), 0.95)
    olive = mat('olive', srgb('#5E6548'), 0.85, 0.2)

    # --- LANDMARK 1: compuerta de salida entreabierta (14 x 7 m) con rendija de luz de dia
    fr = mat('gate_frame', srgb('#3A3C40'), 0.6, 0.6)
    leaf = mat('gate_leaf', srgb('#4B4E52'), 0.55, 0.6)
    x0, x1 = gate_x - gate_w / 2, gate_x + gate_w / 2
    gap = 1.5
    box_mm('GateJambL', (x0 - 1.0, 14.4, 0), (x0, 15.4, gate_h + 0.9), fr)
    box_mm('GateJambR', (x1, 14.4, 0), (x1 + 1.0, 15.4, gate_h + 0.9), fr)
    box_mm('GateHead', (x0 - 1.0, 14.4, gate_h), (x1 + 1.0, 15.4, 8.0), fr)
    box_mm('GateLeafL', (x0, 15.0, 0), (gate_x - gap / 2, 15.5, gate_h), leaf)
    box_mm('GateLeafR', (gate_x + gap / 2, 15.0, 0), (x1, 15.5, gate_h), leaf)
    for i in range(1, 5):   # costillas de la hoja
        box_mm('GateRibL%d' % i, (x0 + 0.2, 14.9, i * 1.4 - 0.12), (gate_x - gap / 2 - 0.2, 15.0, i * 1.4 + 0.12), fr)
        box_mm('GateRibR%d' % i, (gate_x + gap / 2 + 0.2, 14.9, i * 1.4 - 0.12), (x1 - 0.2, 15.0, i * 1.4 + 0.12), fr)
    day = mat('daylight', (0, 0, 0), 0.5, emit=srgb('#D5E4F5'), strength=9.0)
    box_mm('GateSky', (gate_x - gap / 2, 15.9, 0), (gate_x + gap / 2, 16.0, gate_h), day)
    L.light('GateSpill', 'AREA', 11000, (gate_x, 15.6, 3.4), srgb('#D5E4F5'), rot=(92, 0, 0), size=1.4)
    L.frustum_beam('GateBeam', (gate_x, 15.3, 3.5), (gap, 0.2), (gate_x, 6.0, 0.6), (5.0, 0.2), L.beam_mat('gatebeam', srgb('#D5E4F5'), 1.0, 0.045))
    text('GateStencil', 'SALIDA  ·  COMPUERTA 0', (gate_x, 14.3, 7.45), 0.62, stencil_mat())
    # tierra que entra por la rendija
    cyl('GateEarth', 2.3, 0.9, (gate_x, 13.6, 0.4), earth, r2=0.3, seg=18)

    # --- LANDMARK 2: brecha del techo con haz de luz natural fria + cono de escombro y tierra
    bx, by = -11.5, 2.5
    L.light('BreachSun', 'SPOT', 90000, (bx, by, 21.0), srgb('#DCE8F7'), rot=(0, 0, 0), size=0.2, spot=28, blend=0.3)
    L.frustum_beam('BreachBeam', (bx, by, 8.0), (7.0, 6.5), (bx + 0.8, by + 0.2, 0.6), (8.0, 7.4), L.beam_mat('breachbeam', srgb('#DCE8F7'), 1.0, 0.035))
    for i, (dx, dy, rz, tilt) in enumerate(((-3.2, 2.6, 20, 38), (2.8, 3.2, -30, 52), (3.3, -2.9, 65, 44))):   # losas partidas colgando
        box('BreachSlab%d' % i, (2.6, 1.8, 0.45), (bx + dx, by + dy, 7.2), mat('slab', srgb('#7C786F'), 0.95), rot=(tilt, 0, rz))
    for i in range(7):   # varillas
        a = i / 7 * math.tau
        cyl_between('Rebar%d' % i, (bx + math.cos(a) * 3.6, by + math.sin(a) * 3.2, 8.0), (bx + math.cos(a) * 2.9, by + math.sin(a) * 2.6, 6.4 + (i % 3) * 0.4), 0.04, steel, 5)
    cyl('BreachCone', 4.3, 2.2, (bx, by + 0.3, 1.1), earth2, r2=0.5, seg=22)
    L.scrap_pile('BreachRubble', (bx, by + 0.3), 4.6, 2.4, 21, [rubble, earth, mat('slab', srgb('#7C786F'), 0.95)], n=70)

    # --- LANDMARK 3: restos de guerra (blindado medio enterrado, erizos, sacos) contra el muro este
    wx, wy = 14.5, 5.5
    box('ApcHull', (5.6, 2.6, 1.7), (wx, wy, 1.0), olive, rot=(6, -9, 24))
    box('ApcTurret', (1.8, 1.6, 0.7), (wx - 0.4, wy + 0.1, 2.25), olive, rot=(4, -9, 50))
    cyl_between('ApcGun', (wx - 0.6, wy + 0.2, 2.4), (wx - 3.2, wy - 1.9, 3.1), 0.11, steel, 8)
    for i, (dx, dy) in enumerate(((-2.0, -1.6), (-0.3, -1.9), (1.5, -1.1))):
        cyl('ApcWheel%d' % i, 0.55, 0.4, (wx + dx, wy + dy, 0.5), dark, axis='Y', seg=14, rot=(0, 0, 24))
    cyl('EarthSpillA', 5.0, 2.4, (19.0, 7.5, 1.2), earth, r2=0.8, seg=20)
    cyl('EarthSpillB', 4.0, 1.4, (16.0, 10.5, 0.7), earth2, r2=0.5, seg=18)
    cyl('EarthSpillC', 3.4, 1.0, (17.0, 3.2, 0.5), earth2, r2=0.4, seg=16)
    for i, (hx, hy) in enumerate(((9.5, -3.5), (12.5, -6.0), (15.5, -3.8))):
        hedgehog('Hedge%d' % i, hx, hy, steel, 1.0)
    sand = mat('sandbag', srgb('#7B735F'), 0.95)
    for r in range(3):
        for c in range(5 - r):
            box('Sand_%d_%d' % (r, c), (0.9, 0.5, 0.3), (-18.3 + c * 0.92 + r * 0.46, 11.8, 0.16 + r * 0.3), sand)
    L.wreck_limbs('WarWreck', (12.2, 10.6), 9, dark, 3)
    text('WarStencil', 'NIVEL 0  ·  DEFENSA DE ENTRADA', (-12.0, 14.45, 4.2), 0.85, stencil_mat())

    # --- ocupacion robot: chatarra y cables arrancados
    rust = mat('rust', srgb('#4A3528'), 0.85, 0.3)
    L.scrap_pile('ScrapM', (-18.4, -8.0), 2.6, 2.4, 5, [dark, rust, steel], n=45, wall_dir=(-1, 0))
    L.hanging_cables('Torn', (6.0, 9.0, 7.9), [(5.2, 8.0, 5.0), (7.2, 9.6, 4.8), (6.4, 10.4, 5.4)], cable, sag=0.5)
    L.hanging_cables('Torn2', (-2.0, -6.0, 7.9), [(-2.8, -6.6, 5.2), (-1.2, -5.2, 4.9)], cable, sag=0.5)

    # --- relleno de dia frio (rebote) para que nada baje del minimo legible
    L.light('FillA', 'AREA', 4200, (6, -4, 7.8), (0.78, 0.86, 1.0), size=8)
    L.light('FillB', 'AREA', 3200, (-6, -11, 7.8), (0.78, 0.86, 1.0), size=8)
    L.light('FillC', 'AREA', 2400, (10, 8, 7.8), (0.78, 0.86, 1.0), size=6)

    # --- estructura de combate + enemigo + espacio despejado
    L.cover('CoverA', (-3.0, -5.5, 0), (0.0, -4.3, 1.2), pal)
    L.cover('CoverB', (2.5, 3.0, 0), (5.5, 4.2, 1.2), pal)
    L.cover('CoverH', (-6.5, -10.5, 0), (-3.5, -9.3, 2.4), pal)
    L.combat_zone((-6.4, -12.5), (8.0, 11.0))
    L.robot('Enemy', (0.5, 7.0, 0), yaw=5)

    L.marker(1, (gate_x - 4.5, 12.6, 1.2), cam)
    L.marker(2, (bx + 1.0, by - 5.6, 1.2), cam)
    L.marker(3, (wx - 3.6, wy - 4.2, 1.2), cam)

    L.caption(cam, 'TRAMO 3 · CERCA DE LA SUPERFICIE  —  sala tipo «Antesala de la compuerta» (40 × 30 × 8 m)', [
        '1  Compuerta de salida entreabierta: rendija de luz de día y tierra que entra',
        '2  Brecha del techo: haz de luz natural fría, losas partidas, escombro y tierra',
        '3  Restos de guerra: blindado medio enterrado, erizos y sacos (rastro humano)',
        'Luz: natural fría (la más alta) · Robots: chatarra y cables · Exterior: máxima',
    ], [('polvo', srgb('#8A867D')), ('tierra', srgb('#5A4838')), ('oliva', srgb('#5E6548')), ('acero', srgb('#45474A')), ('día frío', srgb('#D5E4F5'))],
        FOV, note='Maqueta de bloques (Blender/Eevee, script reproducible). Los haces son mallas aditivas + luces de sala: las coloca Iluminación, no Decoración.')
    return L.render(out)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    which = [int(a) for a in argv] or [1, 2, 3]
    jobs = {1: ('tramo_1_profundidades.png', build_tramo1), 2: ('tramo_2_infraestructura.png', build_tramo2), 3: ('tramo_3_superficie.png', build_tramo3)}
    report = {}
    for k in which:
        name, fn = jobs[k]
        p = os.path.join(L.OUT, name)
        fn(p)
        mean, dark = L.image_stats(p)
        report[name] = dict(media_0_255=round(mean, 1), pct_pixeles_oscuros=round(dark, 1))
        print('VPF stats', name, report[name])
    rp = os.path.join(L.OUT, 'src', 'tramos_report.json')
    old = {}
    if os.path.exists(rp):
        try:
            old = json.load(open(rp, encoding='utf-8'))
        except Exception:
            old = {}
    old.update(report)
    json.dump(old, open(rp, 'w', encoding='utf-8'), indent=2, ensure_ascii=False)


if __name__ == '__main__':
    main()
