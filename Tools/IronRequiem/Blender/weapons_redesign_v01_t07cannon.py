"""IRON REQUIEM - Canon T-07 C-A "Arrancado crudo" (Redesign v01, fase B2: produccion).

Parte del boceto C-A de Docs/IronRequiem/Concepts/Weapons/Redesign_v01/T07Cannon/t07cannon_concepts.py
(canon octogonal del T-07 con bandas, cuatro varillas, ventana de carga magenta lateral, extremo
arrancado con corte caliente, rebabas, interior oxidado, chapa inferior dentada, cables colgando
hacia delante y abrazadera del jugador con pernos y flejes naranja) con el collar IR-ACOPLE-01.
Misma semilla aleatoria que el boceto: rebabas, dientes y cables conservan su forma.
Largo acople-boca 0,664 m. Tono del blindaje tomado de T07_BaseColor.png (solo lectura).

  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
     --python-exit-code 1 --python Tools/IronRequiem/Blender/weapons_redesign_v01_t07cannon.py
Salida: Tools/IronRequiem/Exports/T07Cannon_Redesign_v01/ y Working/WeaponsRedesign_v01/T07CannonCrudo.blend
"""
import os, sys
sys.dont_write_bytecode = True  # no dejar .pyc en Tools/IronRequiem/Blender/__pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from weapons_redesign_v01_lib import *  # noqa

L = 0.664
WEAPON = 'T07CannonCrudo'
B = 0.012  # bisel grueso del lenguaje T-07
T07_TEX = ROOT / 'Assets/IronRequiem/Art/Enemies/T07New/Textures/T07_BaseColor.png'
OCT = 30   # angulo de arista dura: octogonos con caras planas, como el T-07


def t07_tone():
    """Color medio (lineal) de los pixeles gris-azulados de T07_BaseColor.png."""
    img = bpy.data.images.load(str(T07_TEX), check_existing=False)
    w, h = img.size
    px = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    px = px.reshape(-1, 4)[:, :3]  # valores codificados sRGB (imagen de 8 bits)
    lin = np.where(px <= 0.04045, px / 12.92, ((px + 0.055) / 1.055) ** 2.4)
    lum = lin @ np.array([0.2126, 0.7152, 0.0722])
    sel = (lin[:, 2] > lin[:, 0] * 1.15) & (lum > 0.02) & (lum < 0.5)
    col = lin[sel].mean(0) if sel.sum() > 1000 else np.array([0.15, 0.19, 0.26])
    return tuple(round(float(c), 4) for c in col), int(sel.sum()), w * h


def adapter(r_front=0.072, y_front=-0.06):
    """Abrazadera del jugador soldada al resto arrancado (empieza al final del collar de 32 mm)."""
    collar('Collar', style='clamp')
    cyl('Adapter', 0.047, abs(y_front) - COLLAR_LEN, (0, (y_front - COLLAR_LEN) / 2, 0), 'graphite', r2=r_front,
        seg=12, bevel=0.003)
    for i in range(3):
        a = math.radians(-30 + 120 * i)
        box(f'WeldStrap{i}', (0.016, 0.07, 0.006), (0.064 * math.cos(a), y_front - 0.01, 0.064 * math.sin(a)),
            'orange', bevel=0.002, bevel_top=True, rot=(0, -math.degrees(a) + 90, 0), stack='weld')


def torn_end(r_out, r_in, y0, y1, prefix='Torn', jag=0.022):
    """Extremo arrancado: blindaje dentado, cara de corte de acero, borde caliente, interior oxidado y rebabas."""
    ring(prefix + '_Shell', r_out, r_in, y0, y1, 't07', seg=12, jag=jag, jag_end='y0', smooth=40)
    ring(prefix + '_CutFace', r_out + 0.001, r_in - 0.001, y0 + 0.003, y0 - 0.006, 'steel', seg=12, jag=jag * 0.4,
         jag_end='both')
    ring(prefix + '_CutHot', r_out + 0.003, r_out - 0.004, y0 + 0.005, y0 - 0.004, 'cut_hot', seg=12, jag=jag * 0.4,
         jag_end='both')
    cyl(prefix + '_Interior', r_in, 0.01, (0, y0 - 0.02, 0), 'rust', seg=12, bevel=None)
    burrs = shards(prefix + '_Burr', (0, 0), r_out, 9, y0 + 0.004, size=0.016, maxlod=1)
    # ninguna rebaba invade el collar (y > -0,034): se adelanta la que lo haga
    for ob in burrs:
        ymax = max((ob.matrix_basis @ v.co).y for v in ob.data.vertices)
        if ymax > -0.034:
            ob.location.y -= ymax + 0.034
    return burrs


def cables(prefix, origins, seed_n=3):
    seed(seed_n)
    mats = ['cable', 'cable_r', 'cable_y', 'cable']
    for i, (x, y, z) in enumerate(origins):
        drop = rnd(0.07, 0.14)
        # cuelgan hacia abajo y hacia delante: nunca por detras del acople
        pts = [(x, y, z), (x * 1.3, y - 0.02, z - drop * 0.5), (x * 1.1 + rnd(-0.02, 0.02), y - 0.05, z - drop),
               (x * 0.8, y - rnd(0.07, 0.11), z - drop - rnd(0.03, 0.06))]
        cable(f'{prefix}{i}', pts, rnd(0.004, 0.007), mats[i % len(mats)], resolution_u=3)
        cyl(f'{prefix}{i}_Tip', 0.006, 0.012, pts[-1], 'copper', seg=6, axis='Z', bevel=None, maxlod=1, stack='tip')  # terminal de 12 mm


def octo_muzzle(y_back, y_front, r_out, r_in, prefix='Muzzle'):
    """Boca octogonal T-07; el cuerpo macizo termina 3 cm antes para dejar ver la carga en el anima."""
    yb = y_front + 0.03
    # piezas solapadas 4-5 mm en vez de tocarse en planos coplanares
    cyl(prefix + '_Body', r_out, (y_back + 0.005) - yb, (0, (y_back + 0.005 + yb) / 2, 0), 't07', r2=r_out * 0.96,
        seg=8, bevel=B, smooth=OCT)
    ring(prefix + '_Face', r_out * 0.96, r_in, yb + 0.004, y_front, 't07dark', seg=8, smooth=OCT,
         phase=math.radians(22.5))
    ring(prefix + '_Bore', r_in, r_in * 0.86, yb + 0.004, y_front + 0.002, 'black', seg=8, phase=math.radians(22.5))
    cyl(prefix + '_Charge', r_in * 0.9, 0.003, (0, yb - 0.004, 0), 'magenta', seg=8, bevel=None)


def build():
    adapter()
    torn_end(0.088, 0.062, -0.055, -0.13)
    cyl('Housing', 0.08, 0.40, (0, -0.32, 0), 't07', seg=8, bevel=B, smooth=OCT)
    for i, y in enumerate((-0.17, -0.31, -0.45)):
        ring(f'Band{i}', 0.088, 0.078, y, y - 0.026, 't07dark', seg=8, smooth=OCT, phase=math.radians(22.5),
             stack='band')
    for i, x in enumerate((-0.045, -0.015, 0.015, 0.045)):
        cyl(f'Rod{i}', 0.010, 0.40, (x, -0.35, 0.088), 'steel', seg=8, bevel=0.0015, stack='rod')
        box(f'RodClamp{i}', (0.022, 0.018, 0.016), (x, -0.20 - 0.1 * (i % 2), 0.084), 't07dark', bevel=0.003,
            bevel_top=True, maxlod=1, stack='rodclamp')
    # ventana de carga lateral (lado visto en FPS, +X)
    box('ChargeSlotFrame', (0.012, 0.16, 0.05), (0.078, -0.33, 0.0), 't07dark', bevel=0.004)
    box('ChargeSlot', (0.026, 0.13, 0.006), (0.084, -0.33, 0.0), 'magenta', bevel=0.0015, bevel_top=True,
        rot=(0, 90, 0))
    # chapa de corte inferior (dentada: sin bisel, borde de chapa arrancada)
    prism('CutPlate', jagged_outline(0.10, 0.14, teeth=6, depth=0.02), 0.01, (0, -0.10, -0.07), 'rust', plane='XY')
    octo_muzzle(-0.52, -L, 0.074, 0.042)
    cables('Cable', [(0.04, -0.075, -0.03), (-0.03, -0.075, -0.045), (0.0, -0.08, -0.055), (-0.05, -0.08, 0.0)])


if __name__ == '__main__':
    tone, npx, total = t07_tone()
    run(WEAPON, 'T07Cannon_Redesign_v01', L, build, budget=2600,
        current_fbx=ROOT / 'Assets/IronRequiem/Art/Weapons/T07CannonPivotFixed/T07_Cannon_PivotFixed.fbx',
        symmetric=False, seed_id='C-A', overrides={'t07': {'base': tone}},
        extra=dict(concept='Docs/IronRequiem/Concepts/Weapons/Redesign_v01/T07Cannon (C-A Arrancado crudo)',
                   t07_tone=dict(source='Assets/IronRequiem/Art/Enemies/T07New/Textures/T07_BaseColor.png',
                                 linear_rgb=tone, pixels_used=npx, pixels_total=total),
                   heat_parts='Torn_CutHot (borde de corte caliente) en Heat_Emissive; ChargeSlot + Muzzle_Charge en Charge_Emissive'),
        notes=['Adaptador acortado 10 mm por detras para el collar de 32 mm; rebabas adelantadas si invaden el collar.',
               'Boca: cuerpo, cara y anima solapados 2-5 mm (el boceto tenia caras coplanares en -0,52/-0,634/-0,664).',
               'Cables: curvas convertidas a tubos cerrados (resolucion 4); las puntas de cobre, pernos, abrazaderas de varilla y rebabas desaparecen en LOD2.'])
