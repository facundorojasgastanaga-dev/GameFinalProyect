"""IRON REQUIEM - Puertas Redesign_v01 (fase P1): tres direcciones x cuatro variantes.

  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --factory-startup ^
      --python-exit-code 1 --python doors_concepts.py -- [D-A D-B D-C]

Sin IDs regenera las tres direcciones. Escribe solo en esta carpeta:
  <ID>/<ID>_<Nombre>_<estado|variante>.png, <ID>/<ID>_<Nombre>_lamina.png, report.json

Progreso p en [0,1] = k de AirlockDoorMotion (el mismo k que hoy mueve el root).
p=1 -> el root de la puerta queda INACTIVO: sus hojas no se dibujan (en D-C se ve
el proxy de hojas aparcadas, que vive en el marco).
"""
import sys, os, math, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import door_lib as L
from door_lib import box_minmax, prism_xz, prism_frame, cyl, cyl_between, rivets, text, ss, clamp01
from mathutils import Vector

CLEAR = {'V1': 0.5, 'V2': 0.95, 'V3': 0.5, 'V4': 0.95}  # techo de la esclusa sobre el vano (m)
VARIANTS = [
    # id, nombre, W, H, T, puertas actuales que sustituiria
    ('V1', 'Esclusa principal', 8.4, 4.8, 0.6, 'ChamberHangarDoor / ChamberCorridorDoor'),
    ('V2', 'Paso entre salas', 10.0, 8.0, 0.6, 'Transfer Source/DestinationDoor x6, InitialLoadingDoor'),
    ('V3', 'Persiana interior', 8.4, 4.8, 0.5, 'VestibuleShutter, ChaseEntrance/ExitGate'),
    ('V4', 'Salida de sala', 12.0, 7.5, 1.0, 'Room01ExitDoor, Room02EntryDoor, LobbyRear'),
]


def _rot(r, x, y):
    c, s = math.cos(r), math.sin(r)
    return (x * c - y * s, x * s + y * c)


# =============================================================== D-A ===
# MANDIBULA: dos hojas con junta dentada que se separan en horizontal y se
# telescopan (2 segmentos por lado) dentro de casetes laterales fijos.

A_TIMES = dict(open=0.80, close=0.45)


def build_A(x0, var, p, state):
    vid, _, W, H, T, _ = var
    em = 'em_alarm' if state == 'alarm' else L.STATES[state]['em']
    single = vid == 'V3'
    tooth = min(0.45, W * 0.045)
    nt = 3 if H < 6 else 4
    jw = W / 4 + tooth + 0.45
    jl, jr = (0.6, W / 2 + 0.5) if single else (jw, jw)
    fr = L.frame(x0, W, H, T, state, jl=jl, jr=jr, head_h=0.8, front=0.45, back=0.25, tag=f'A{vid}', tunnel=CLEAR[vid])
    b = clamp01(p / 0.21)                 # cerrojos (dogs) + giro del cubo
    pop = clamp01((p - 0.21) / 0.10)      # separacion de sello
    s = ss((p - 0.31) / 0.69)             # deslizamiento telescopico
    split = -T / 2 + T * 0.45
    # --- dogs: piezas del MARCO que se retraen hacia los casetes (siempre activas)
    sides = (1,) if single else (-1, 1)
    for sg in (-1, 1):
        for i, f in enumerate((0.2, 0.5, 0.8)):
            xin = sg * (W / 2 - 0.45 + 0.65 * b)
            fr.append(box_minmax(f'A{vid}_Dog{sg}{i}', (x0 + min(xin, xin + sg * 1.0), -T / 2 - 0.30, f * H - 0.17),
                                 (x0 + max(xin, xin + sg * 1.0), -T / 2 - 0.04, f * H + 0.17), 'steel', 0.03))
    mv = []
    if p >= 1.0:
        return fr, mv
    if single:
        # persiana lateral: 2 segmentos que se recogen hacia +X
        d1 = s * (W + 0.25) + 0.06 * pop
        d2 = s * (W / 2 + 0.25) + 0.06 * pop
        x1a, x1b = -W / 2, 0.15
        mv.append(prism_xz(f'A{vid}_Seg1', [(x0 + x1a + d1, 0), (x0 + x1b + d1, 0), (x0 + x1b + d1, H), (x0 + x1a + d1, H)],
                           -T / 2, split, 'leaf', origin=(x0 + x1a + d1, 0, 0)))
        mv.append(prism_xz(f'A{vid}_Seg2', [(x0 + d2, 0), (x0 + W / 2 + d2, 0), (x0 + W / 2 + d2, H), (x0 + d2, H)],
                           split, T / 2, 'leaf', origin=(x0 + d2, 0, 0)))
        for j, z in enumerate((H * 0.3, H * 0.5, H * 0.7)):
            mv.append(box_minmax(f'A{vid}_Slot{j}', (x0 - W / 2 + 0.5 + d1, -T / 2 - 0.05, z - 0.07), (x0 - 0.4 + d1, -T / 2 + 0.01, z + 0.07), em, 0.0))
        mv.append(box_minmax(f'A{vid}_Haz1', (x0 - W / 2 + 0.1 + d1, -T / 2 - 0.03, 0.08), (x0 + 0.05 + d1, -T / 2 + 0.01, 0.65), 'hazard', 0.0))
        mv.append(box_minmax(f'A{vid}_Haz2', (x0 + 0.2 + d2, split - 0.03, 0.08), (x0 + W / 2 - 0.1 + d2, split + 0.01, 0.65), 'hazard', 0.0))
        pts = [(x0 + x + d1, -T / 2 - 0.01, z) for x in (-W / 2 + 0.25, -0.1) for z in [0.4 + 0.5 * k for k in range(int((H - 0.6) / 0.5))]]
        mv.append(rivets(f'A{vid}_Riv', pts, 0.07))
        return fr, mv
    seam = [((tooth if k % 2 else -tooth), k * H / (2 * nt)) for k in range(2 * nt + 1)]
    rh = min(1.0, H * 0.17)
    for sg in (-1, 1):
        di = sg * (0.08 * pop + s * (W / 2 + tooth + 0.25))
        do = sg * (0.08 * pop + s * (W / 4 + 0.25))
        xq = sg * (W / 4 + 0.15)
        if sg > 0:
            poly = [(xq, 0), (xq, H)] + [(x, z) for x, z in reversed(seam)]
        else:
            poly = [(xq, 0)] + seam + [(xq, H)]
        poly = [(x0 + x + di, z) for x, z in poly]
        mv.append(prism_xz(f'A{vid}_Inner{sg}', poly, -T / 2, split, 'leaf', origin=(x0 + xq + di, 0, 0)))
        mv.append(prism_xz(f'A{vid}_Outer{sg}', [(x0 + sg * W / 4 + do, 0), (x0 + sg * W / 2 + do, 0), (x0 + sg * W / 2 + do, H), (x0 + sg * W / 4 + do, H)],
                           split, T / 2, 'leaf', origin=(x0 + sg * W / 2 + do, 0, 0)))
        # costillas, franja de peligro, rendija emisiva de estado, remaches
        xa, xb = sorted((x0 + sg * (tooth + 0.45) + di, x0 + sg * (W / 4 + 0.05) + di))
        for j, f in enumerate((0.22, 0.78)):
            mv.append(box_minmax(f'A{vid}_Rib{sg}{j}', (xa, -T / 2 - 0.10, f * H - 0.12), (xb, -T / 2 + 0.01, f * H + 0.12), 'leaf', 0.03))
        mv.append(box_minmax(f'A{vid}_Haz{sg}', (xa, -T / 2 - 0.03, 0.06), (xb, -T / 2 + 0.01, 0.62), 'hazard', 0.0))
        xs = x0 + sg * (tooth + 0.3) + di
        mv.append(box_minmax(f'A{vid}_Slit{sg}', (xs - 0.05, -T / 2 - 0.05, 0.75), (xs + 0.05, -T / 2 + 0.01, H - 0.6), em, 0.0))
        xo = x0 + sg * (3 * W / 8) + do
        mv.append(box_minmax(f'A{vid}_OHaz{sg}', (xo - W / 8 + 0.1, split - 0.03, 0.06), (xo + W / 8 - 0.1, split + 0.01, 0.62), 'hazard', 0.0))
        pts = [(xa + 0.1, -T / 2 - 0.01, z) for z in [0.8 + 0.45 * k for k in range(int((H - 1.4) / 0.45))]]
        pts += [(xo + dx, split - 0.01, z) for dx in (-W / 8 + 0.2, W / 8 - 0.2) for z in [0.8 + 0.6 * k for k in range(int((H - 1.2) / 0.6))]]
        mv.append(rivets(f'A{vid}_Riv{sg}', pts, 0.08))
        # medio cubo de cierre (gira 30 grados con los cerrojos y viaja con su hoja)
        rot = math.radians(30 * b)
        a0 = -math.pi / 2 if sg > 0 else math.pi / 2
        arc = [(x0 + di + rh * math.cos(a0 + rot + math.pi * k / 10), H / 2 + rh * math.sin(a0 + rot + math.pi * k / 10)) for k in range(11)]
        mv.append(prism_xz(f'A{vid}_Hub{sg}', arc, -T / 2 - 0.22, -T / 2 + 0.01, 'hazard', origin=(x0 + di, 0, H / 2)))
        for k in range(2):
            a = a0 + rot + math.pi * (0.25 + 0.5 * k)
            cx, cz = x0 + di + (rh + 0.12) * math.cos(a), H / 2 + (rh + 0.12) * math.sin(a)
            mv.append(box_minmax(f'A{vid}_Lug{sg}{k}', (cx - 0.15, -T / 2 - 0.2, cz - 0.15), (cx + 0.15, -T / 2 + 0.01, cz + 0.15), 'steel', 0.03))
    return fr, mv


# =============================================================== D-B ===
# PERSIANA TELESCOPICA: lamas horizontales que se recogen hacia arriba en un
# casete corto (alto = una lama + 0,5 m) montado en la cara de la SALA.

B_TIMES = dict(open=0.70, close=0.45)


def build_B(x0, var, p, state):
    vid, _, W, H, T, _ = var
    em = 'em_alarm' if state == 'alarm' else L.STATES[state]['em']
    if vid == 'V3':
        n, ts = math.ceil(H / 0.5), 0.07
    elif vid == 'V1':
        n, ts = math.ceil(H / 0.85), 0.14
    else:
        n, ts = math.ceil(H / 0.9), 0.12
    h = H / n
    ystack0 = T / 2 - n * ts                 # cara de la lama 0 (la mas cercana a la sala)
    hf = max(0.35, -ystack0 - T / 2 + 0.25)
    fr = L.frame(x0, W, H, T, state, jl=0.6, jr=0.6, head_h=h + 0.5, front=hf, back=0.15, tag=f'B{vid}', tunnel=CLEAR[vid])
    b = clamp01(p / 0.21)
    d = ss((p - 0.21) / 0.79) * (H + 0.15)
    # levas laterales (MARCO): giran 90 grados hacia arriba
    for sg in (-1, 1):
        for i, f in enumerate((0.3, 0.7)):
            px, pz = x0 + sg * (W / 2 + 0.25), f * H
            r = math.radians(90 * b) * (1 if sg < 0 else -1)
            Lg = 0.95
            pts = []
            for u, v in ((0, -0.13), (Lg, -0.13), (Lg, 0.13), (0, 0.13)):
                uu = -sg * u
                xx, zz = _rot(r, uu, v)
                pts.append((px + xx, pz + zz))
            y_l = -T / 2 - hf
            fr.append(prism_xz(f'B{vid}_Cam{sg}{i}', pts, y_l - 0.16, y_l - 0.02, 'steel', origin=(px, 0, pz)))
            fr.append(cyl(f'B{vid}_CamHub{sg}{i}', 0.18, 0.2, (px, y_l - 0.1, pz), 'frame', axis='Y'))
    # alojamientos de pasador en el umbral
    for k, fx in enumerate((-1 / 3, 0, 1 / 3)):
        fr.append(box_minmax(f'B{vid}_Socket{k}', (x0 + fx * W - 0.14, ystack0 + 0.0, 0.0), (x0 + fx * W + 0.14, ystack0 + ts, 0.075), 'black', 0.0))
    mv = []
    if p >= 1.0:
        return fr, mv
    for i in range(n):
        dz = max(0.0, d - i * h)
        y0 = ystack0 + i * ts
        z0, z1 = i * h - (0.0 if i == 0 else 0.07) + dz, (i + 1) * h + 0.05 + dz
        matn = 'hazard' if (i == 0 or (vid == 'V1' and i == 1)) else 'leaf'
        mv.append(prism_xz(f'B{vid}_Slat{i}', [(x0 - W / 2 - 0.3, z0), (x0 + W / 2 + 0.3, z0), (x0 + W / 2 + 0.3, z1), (x0 - W / 2 - 0.3, z1)],
                           y0, y0 + ts, matn, origin=(x0, 0, z0), bevel=0.02))
        # nervio de refuerzo (corrugado) en la cara de sala
        if h > 0.6:
            zc = (z0 + z1) / 2
            mv.append(box_minmax(f'B{vid}_Rib{i}', (x0 - W / 2 + 0.05, y0 - 0.05, zc - 0.07), (x0 + W / 2 - 0.05, y0 + 0.01, zc + 0.07), 'leaf', 0.02))
        if i % 3 == 2 and i < n - 1:
            mv.append(box_minmax(f'B{vid}_Band{i}', (x0 - W * 0.3, y0 - 0.07, z0 + h * 0.3), (x0 + W * 0.3, y0 - 0.02, z0 + h * 0.3 + 0.09), em, 0.0))
        if i == 0:
            # labio emisivo inferior + pasadores de suelo (hijos de la lama 0)
            mv.append(box_minmax(f'B{vid}_Lip', (x0 - W / 2 + 0.1, y0 - 0.06, z0 + 0.1), (x0 + W / 2 - 0.1, y0 - 0.01, z0 + 0.2), em, 0.0))
            for k, fx in enumerate((-1 / 3, 0, 1 / 3)):
                pz0 = -0.0 + 0.32 * b + dz
                mv.append(box_minmax(f'B{vid}_Pin{k}', (x0 + fx * W - 0.08, y0 - 0.12, pz0 + 0.0), (x0 + fx * W + 0.08, y0 - 0.0, pz0 + 0.55), 'steel', 0.02))
            pts = [(x0 + x, y0 - 0.01, z0 + h * 0.75) for x in [-W / 2 + 0.3 + 0.6 * k for k in range(int((W - 0.6) / 0.6) + 1)]]
            mv.append(rivets(f'B{vid}_Riv', pts, 0.07))
    return fr, mv


# =============================================================== D-C ===
# BOVEDA BATIENTE: hojas pesadas con junta diagonal que pivotan sobre bisagras
# verticales hacia el lado SALA (V1/V2: 2 hojas; V3/V4: plegables de 4 paneles).

C_TIMES = dict(open=1.00, close=0.45)


def _leaf_details(tag, pts_poly, T, O, U, V, em, H, seam_u, bolts_u, mv):
    """Detalles de una hoja en su marco local (u desde la bisagra, v hacia la esclusa)."""
    ul = min(u for u, _ in pts_poly)
    uh = max(u for u, _ in pts_poly)
    for j, f in enumerate((0.25, 0.62)):
        u1 = (seam_u(f * H) - 0.6) if seam_u else uh - 0.3
        mv.append(prism_frame(f'{tag}_Rib{j}', [(0.35, f * H - 0.13), (u1, f * H - 0.13), (u1, f * H + 0.13), (0.35, f * H + 0.13)], 0.10, O, U, V, 'leaf', v0=-0.10))
    u1 = (seam_u(0.3) - 0.15) if seam_u else uh - 0.1
    mv.append(prism_frame(f'{tag}_Haz', [(0.1, 0.06), (u1, 0.06), (u1, 0.6), (0.1, 0.6)], 0.035, O, U, V, 'hazard', v0=-0.03, bevel=0.0))
    if seam_u:
        z0, z1 = 0.8, H - 0.6
        q = [(seam_u(z0) - 0.42, z0), (seam_u(z0) - 0.30, z0), (seam_u(z1) - 0.30, z1), (seam_u(z1) - 0.42, z1)]
        mv.append(prism_frame(f'{tag}_Slit', q, 0.05, O, U, V, em, v0=-0.05, bevel=0.0))
    for k, ub in enumerate(bolts_u):
        mv.append(prism_frame(f'{tag}_BoltSeat{k}', [(ub - 0.22, H - 0.75), (ub + 0.22, H - 0.75), (ub + 0.22, H - 0.2), (ub - 0.22, H - 0.2)], 0.22, O, U, V, 'frame', v0=-0.22))
    pts = []
    for z in [0.9 + 0.55 * k for k in range(int((H - 1.5) / 0.55))]:
        for u in (0.2,):
            pts.append(Vector(O) + u * Vector((*U, 0)) + (-0.01) * Vector((*V, 0)) + Vector((0, 0, z)))
    if pts:
        mv.append(rivets(f'{tag}_Riv', [tuple(p) for p in pts], 0.08))


def build_C(x0, var, p, state, show_proxy=True):
    vid, _, W, H, T, _ = var
    em = 'em_alarm' if state == 'alarm' else L.STATES[state]['em']
    bifold = vid in ('V3', 'V4')
    a = W * 0.07 if not bifold else W * 0.035
    fr = L.frame(x0, W, H, T, state, jl=0.75, jr=0.75, head_h=1.15, front=0.4, back=0.15, tag=f'C{vid}', tunnel=CLEAR[vid])
    b = clamp01(p / 0.20)
    pop = 0.06 * clamp01((p - 0.20) / 0.08)
    th = math.radians(90 * ss((p - 0.28) / 0.72))
    reach = W / 4 if bifold else W / 2
    # MARCO: bisagras, marcas de barrido en el suelo y cerrojos superiores
    for sg in (-1, 1):
        hx = x0 + sg * W / 2
        for f in (0.12, 0.5, 0.88):
            fr.append(cyl(f'C{vid}_Knuckle{sg}{f}', 0.2, 0.7, (hx + sg * 0.05, -T / 2 - 0.18, f * H), 'steel'))
        for k in range(9):
            ang = math.radians(4 + k * 10)
            r = reach + T + 0.25
            # arco desde la bisagra: de la direccion "hacia el centro" a "hacia la sala"
            u = (-sg * math.cos(ang), -math.sin(ang))
            c = (hx + u[0] * r, -T / 2 + u[1] * r)
            fr.append(box_minmax(f'C{vid}_Sweep{sg}{k}', (c[0] - 0.18, c[1] - 0.18, 0.0), (c[0] + 0.18, c[1] + 0.18, 0.03), 'em_dim', 0.0))
    if bifold:
        bolts_x = [x0 + sg * (W / 2 - W / 8 * 3) for sg in (-1, 1)]
    else:
        bolts_x = [x0 + sg * (W / 2 - W / 4) for sg in (-1, 1)]
    for k, bx in enumerate(bolts_x):
        z0 = H - 0.6 + 0.7 * b
        fr.append(cyl(f'C{vid}_Bolt{k}', 0.11, 0.95, (bx, -T / 2 - 0.12 - pop, z0 + 0.475), 'steel'))
    mv = []
    proxy = p >= 1.0
    if proxy and not show_proxy:
        return fr, mv
    tgt = fr if proxy else mv   # en p=1 las hojas son el PROXY aparcado del marco
    for sg in (-1, 1):
        r = sg * th
        O = (x0 + sg * W / 2, -T / 2 - pop, 0.0)
        U = _rot(r, -sg, 0)
        V = _rot(r, 0, 1)
        tag = f'C{vid}_{"Proxy" if proxy else "Leaf"}{sg}'
        if not bifold:
            if sg < 0:
                su = lambda z: W / 2 + a - 2 * a * z / H
            else:
                su = lambda z: W / 2 - a + 2 * a * z / H
            poly = [(0, 0), (su(0), 0), (su(H), H), (0, H)]
            tgt.append(prism_frame(tag, poly, T, O, U, V, 'leaf'))
            _leaf_details(tag, poly, T, O, U, V, em, H, su, [W / 4], tgt)
            # brazo hidraulico (MARCO): ancla en jamba -> soporte en la hoja
            A = Vector((x0 + sg * (W / 2 + 0.55), -T / 2 - 0.45, 0.8 * H))
        else:
            L1 = W / 4
            tgt.append(prism_frame(tag + 'P1', [(0, 0), (L1, 0), (L1, H), (0, H)], T, O, U, V, 'leaf'))
            _leaf_details(tag + 'P1', [(0, 0), (L1, H)], T, O, U, V, em, H, None, [], tgt)
            J = Vector(O) + L1 * Vector((*U, 0)) + T * Vector((*V, 0))
            U2 = _rot(-r, -sg, 0)
            V2 = _rot(-r, 0, 1)
            if sg < 0:
                su = lambda z: W / 4 + a - 2 * a * z / H
            else:
                su = lambda z: W / 4 - a + 2 * a * z / H
            poly = [(0, 0), (su(0), 0), (su(H), H), (0, H)]
            tgt.append(prism_frame(tag + 'P2', poly, T, tuple(J), U2, V2, 'leaf', v0=-T))
            _leaf_details(tag + 'P2', poly, T, tuple(J + (-T) * Vector((*V2, 0))), U2, V2, em, H, su, [W / 8 * 3 - W / 4], tgt)
            fr.append(cyl(f'C{vid}_MidHinge{sg}', 0.14, H * 0.9, (J.x, J.y, H / 2), 'steel'))
            A = Vector((x0 + sg * (W / 2 + 0.55), -T / 2 - 0.45, 0.8 * H))
        Bp = Vector(O) + (W / 8 if bifold else W / 4) * Vector((*U, 0)) + (-0.15) * Vector((*V, 0)) + Vector((0, 0, 0.8 * H))
        mid = A + (Bp - A) * 0.55
        fr.append(cyl_between(f'C{vid}_ArmTube{sg}', A, mid, 0.16, 'frame'))
        fr.append(cyl_between(f'C{vid}_ArmRod{sg}', A + (Bp - A) * 0.3, Bp, 0.08, 'steel'))
    return fr, mv


# ================================================================ runner ===

DIRS = [
    dict(id='D-A', slug='Mandibula', name='Mandibula telescopica', build=build_A, times=A_TIMES,
         idea='Dos hojas con junta dentada; cerrojos laterales y cubo central giran, se separan 8 cm y se telescopan a casetes laterales.',
         p_unlock=0.31, p_half=0.62,
         open_ph=[('Dogs fuera + cubo gira 30', 0.0, 0.17, 'IR_Mount_Lock'), ('Sello 8 cm + vapor', 0.17, 0.25, 'IR_Hangar_Hydraulic'),
                  ('Deslizamiento telescopico', 0.25, 0.80, 'IR_Hangar_Servo (pitch 0.8)')],
         close_ph=[('Deslizamiento (ease-in)', 0.0, 0.33, 'IR_Hangar_Servo'), ('Choque', 0.33, 0.37, 'IR_Fist_HeavyImpact'),
                   ('Dogs + cubo', 0.37, 0.45, 'IR_Mount_Clamp')]),
    dict(id='D-B', slug='PersianaTelescopica', name='Persiana telescopica', build=build_B, times=B_TIMES,
         idea='Lamas horizontales; levas laterales y pasadores de suelo sueltan, la lama inferior sube y recoge al resto en un casete corto.',
         p_unlock=0.22, p_half=0.55,
         open_ph=[('Levas 90 + pasadores', 0.0, 0.15, 'IR_Mount_Clamp'), ('Elevacion: la lama 0 recoge a las demas', 0.15, 0.70, 'IR_Hangar_Hydraulic + IR_Hangar_Clatter')],
         close_ph=[('Caida escalonada', 0.0, 0.36, 'IR_Hangar_Clatter'), ('Golpe', 0.36, 0.39, 'IR_Fist_HeavyImpact'),
                   ('Pasadores + levas', 0.39, 0.45, 'IR_Mount_Lock')]),
    dict(id='D-C', slug='BovedaBatiente', name='Boveda batiente', build=build_C, times=C_TIMES,
         idea='Hojas de boveda con junta diagonal; cerrojos superiores suben, sello cede y pivotan 90 grados hacia la sala con brazos hidraulicos.',
         p_unlock=0.28, p_half=0.62,
         open_ph=[('Cerrojos suben', 0.0, 0.20, 'IR_Mount_Lock'), ('Sello cede 6 cm', 0.20, 0.28, 'IR_Hangar_Hydraulic'),
                  ('Giro 90 con brazos', 0.28, 1.00, 'IR_Hangar_CannonArm + IR_Hangar_Servo')],
         close_ph=[('Giro (ease-in)', 0.0, 0.35, 'IR_Hangar_Servo'), ('Choque', 0.35, 0.38, 'IR_Fist_HeavyImpact'),
                   ('Cerrojos bajan', 0.38, 0.45, 'IR_Mount_Clamp')]),
]


def extents(d, var):
    """Huella del marco (izq, der, alto) para encuadrar."""
    vid, _, W, H, T, _ = var
    if d['id'] == 'D-A':
        tooth = min(0.45, W * 0.045); jw = W / 4 + tooth + 0.45
        jl, jr = (0.6, W / 2 + 0.5) if vid == 'V3' else (jw, jw)
        return W / 2 + jl, W / 2 + jr, H + 0.8
    if d['id'] == 'D-B':
        n = math.ceil(H / (0.5 if vid == 'V3' else 0.85 if vid == 'V1' else 0.9))
        return W / 2 + 0.6, W / 2 + 0.6, H + H / n + 0.5
    return W / 2 + 0.75, W / 2 + 0.75, H + 1.15


def counts(fr, mv):
    frame_objs = [o for o in fr if '_Wall' not in o.name and '_Beam' not in o.name and 'Sweep' not in o.name and 'Proxy' not in o.name]
    proxy = [o for o in fr if 'Proxy' in o.name]
    return dict(root_tris=L.tri_count(mv), frame_tris=L.tri_count(frame_objs), proxy_tris=L.tri_count(proxy),
                root_objects=len(mv), frame_objects=len(frame_objs))


def render_single(d, var, state, p, out):
    L.reset_scene()
    eng, bloom = L.setup_stage()
    lft, rgt, top = extents(d, var)
    vid, vname, W, H, T, _ = var
    fr, mv = d['build'](0.0, var, p, state)
    L.floor_plane(-lft - 8, rgt + 8, -30, 12)
    L.human(0.6, -T / 2 - 2.4)
    span = max(lft + rgt + 3.0, (top + 3.6) * 1.78)
    fov = 52
    dist = span / 2 / math.tan(math.radians(fov / 2)) * 1.05
    tgt = Vector(((rgt - lft) / 2, 0, top * 0.40))
    eye = tgt + Vector((span * 0.20, -dist, max(1.8, H * 0.55) - top * 0.47 + dist * 0.05))
    L.key_lights(tgt, span)
    cam = L.camera('Cam', eye, tgt, fov)
    tl = d['times']
    L.caption(cam, f"{d['id']} {d['name'].upper()}  |  {vid} {vname} {W:g}x{H:g}x{T:g} m  |  {L.STATES[state]['label']}",
              f"p = k = {p:.2f} de apertura {tl['open']:.2f} s (t = {p * tl['open']:.2f} s)" + ("  |  root INACTIVO: solo queda el marco" + (" + proxy de hojas aparcadas" if d['id'] == 'D-C' else '') if p >= 1 else ''), fov)
    L.render(out)
    return counts(fr, mv), eng, bloom


def render_overview(d, state, p, out):
    """Conjunto de las 4 variantes a escala: dos franjas (V1+V2 arriba, V3+V4 abajo)
    de 1280x360 apiladas en un PNG de 1280x720."""
    info = {}
    halves = []
    for hi, pair in enumerate((VARIANTS[:2], VARIANTS[2:])):
        L.reset_scene()
        L.setup_stage()
        sc = L.bpy.context.scene
        sc.render.resolution_y = 360
        x = 0.0
        xs = []
        gap = 3.0
        for var in pair:
            lft, rgt, top = extents(d, var)
            x += lft
            xs.append((x, var, top))
            x += rgt + gap
        total = x - gap
        tmax = max(t for _, _, t in xs)
        for x0, var, top in xs:
            vid, vname, W, H, T, cur = var
            fr, mv = d['build'](x0, var, p, state)
            info[vid] = counts(fr, mv)
            text(f'Lbl{vid}', f'{vid}  {vname.upper()}  {W:g} x {H:g} m', (x0, -T / 2 - 0.12, top + 0.75), 0.6, 'em_white')
            L.human(x0 + 0.6, -T / 2 - 2.0, name=f'H{vid}')
        L.floor_plane(-10, total + 10, -40, 12)
        fov = 50
        dist = max((total + 3) / 2 / math.tan(math.radians(fov / 2)),
                   (tmax + 2.5) / 2 / (math.tan(math.radians(fov / 2)) * 360 / 1280))
        tgt = Vector((total / 2, 0, (tmax + 1.2) / 2))
        eye = tgt + Vector((total * 0.04, -dist, 1.2))
        L.key_lights(tgt, total * 0.45)
        cam = L.camera('Cam', eye, tgt, fov)
        if hi == 1:
            L.caption(cam, f"{d['id']} {d['name'].upper()}  |  4 variantes a escala real (silueta 1,8 m)  |  {L.STATES[state]['label']}  |  p = {p:.2f}", '', fov)
        path = out.replace('.png', f'_part{hi}.png')
        L.render(path)
        halves.append(path)
    L.stack_vertical(out, halves)
    for h in halves:
        os.remove(h)
    return info


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    sel = [a for a in argv if a.startswith('D-')] or [d['id'] for d in DIRS]
    rp = os.path.join(HERE, 'report.json')
    report = {}
    if os.path.exists(rp):
        with open(rp, encoding='utf-8') as f:
            report = json.load(f)
    for d in DIRS:
        if d['id'] not in sel:
            continue
        dd = os.path.join(HERE, d['id'])
        os.makedirs(dd, exist_ok=True)
        pre = os.path.join(dd, f"{d['id']}_{d['slug']}")
        rec = dict(name=d['name'], idea=d['idea'], open_s=d['times']['open'], close_s=d['times']['close'],
                   open_phases=d['open_ph'], close_phases=d['close_ph'], renders=[], variants={})
        V1 = VARIANTS[0]
        for state, p in (('blocked', 0.0), ('unlock', d['p_unlock']), ('half', d['p_half']), ('open', 1.0), ('alarm', 0.0)):
            out = f'{pre}_V1_{state}.png'
            c, eng, bloom = render_single(d, V1, state, p, out)
            rec['renders'].append(os.path.basename(out))
            rec['engine'], rec['post'] = eng, bloom
            if state == 'blocked':
                rec['variants']['V1'] = c
            print('[IR]', out, c)
        for state, p in (('blocked', 0.0), ('half', d['p_half'])):
            out = f'{pre}_conjunto_{state}.png'
            info = render_overview(d, state, p, out)
            rec['renders'].append(os.path.basename(out))
            if state == 'blocked':
                for k, v in info.items():
                    rec['variants'][k] = v
            print('[IR]', out, info)
        tl = f'{pre}_cronograma.png'
        L.render_timeline(tl, f"{d['id']} {d['name'].upper()}  -  secuencia por fases (apertura {d['times']['open']:.2f} s, cierre {d['times']['close']:.2f} s)",
                          d['open_ph'], d['close_ph'],
                          notes=['Barra blanca = duracion actual (abrir 0,70 s / cerrar 0,45 s). En azul, audio candidato existente (no copiado).',
                                 'El root mantiene su BoxCollider solido durante toda la apertura y desde el primer frame del cierre (contrato AirlockDoorMotion).'])
        rec['renders'].append(os.path.basename(tl))
        title = os.path.join(dd, '_title.png')
        vv = rec['variants']
        L.render_title(title, f"IRON REQUIEM  -  PUERTAS P1  -  {d['id']} {d['name'].upper()}",
                       f"{d['idea']}  |  root V1 {vv['V1']['root_tris']} tris, marco {vv['V1']['frame_tris']} tris (boceto)", width=1288, height=110)
        sheet = f'{pre}_lamina.png'
        rows = [[f'{pre}_V1_blocked.png', f'{pre}_V1_unlock.png'],
                [f'{pre}_V1_half.png', f'{pre}_V1_open.png'],
                [f'{pre}_V1_alarm.png', tl],
                [f'{pre}_conjunto_blocked.png', f'{pre}_conjunto_half.png']]
        size = L.compose_grid(sheet, title, rows)
        rec['renders'].append(os.path.basename(sheet))
        rec['sheet_size'] = size
        report[d['id']] = rec
        print('[IR] sheet', sheet, size)
    with open(rp, 'w', encoding='utf-8') as f:
        json.dump(report, f, indent=2, ensure_ascii=False)


main()
