"""IRON REQUIEM - Cañón arrancado al T-07: direcciones visuales (Redesign_v01).

Regenera los bocetos y la lamina de esta carpeta:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
     --python-exit-code 1 --python t07cannon_concepts.py -- [C-A C-B C-C]
Referencia actual: T07_Cannon_PivotFixed (0,664 m acople-boca). Lenguaje T-07:
blindaje redondeado y grueso, boca octogonal, varillas/pistones sobre el cañón.
La carga usa magenta (CANNON_CHARGE.md); el corte se lee con metal desnudo y oxido.
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from ir_concept_lib import *  # noqa

L = 0.664
B = 0.012  # bisel grueso del lenguaje T-07


def adapter(r_front=0.072, y_front=-0.06):
    """Abrazadera del jugador soldada al resto arrancado."""
    collar('Collar', style='clamp')
    cyl('Adapter', 0.047, abs(y_front) - 0.022, (0, (y_front - 0.022) / 2, 0), 'graphite', r2=r_front, seg=12, bevel=0.003)
    for i in range(3):
        a = math.radians(-30 + 120 * i)
        box(f'WeldStrap{i}', (0.016, 0.07, 0.006), (0.064 * math.cos(a), y_front - 0.01, 0.064 * math.sin(a)),
            'orange', bevel=0.002, rot=(0, -math.degrees(a) + 90, 0))


def torn_end(r_out, r_in, y0, y1, prefix='Torn', jag=0.022, arc=None):
    """Extremo arrancado: blindaje con borde irregular, corte caliente, rebabas e interior oxidado."""
    ring(prefix + '_Shell', r_out, r_in, y0, y1, 't07', seg=12, jag=jag, jag_end='y0', arc=arc)
    ring(prefix + '_CutFace', r_out + 0.001, r_in - 0.001, y0 + 0.003, y0 - 0.006, 'steel', seg=12, jag=jag * 0.4, jag_end='both', arc=arc)
    ring(prefix + '_CutHot', r_out + 0.003, r_out - 0.004, y0 + 0.005, y0 - 0.004, 'cut_hot', seg=12, jag=jag * 0.4, jag_end='both', arc=arc)
    cyl(prefix + '_Interior', r_in, 0.01, (0, y0 - 0.02, 0), 'rust', seg=12, bevel=0)
    shards(prefix + '_Burr', (0, 0), r_out, 9, y0 + 0.004, size=0.016, arc=arc or (0, 360))


def cables(prefix, origins, seed_n=3):
    seed(seed_n)
    mats = ['cable', 'cable_r', 'cable_y', 'cable']
    for i, (x, y, z) in enumerate(origins):
        drop = rnd(0.07, 0.14)
        # cuelgan hacia abajo y hacia delante: nunca por detras del acople (y > 0)
        pts = [(x, y, z), (x * 1.3, y - 0.02, z - drop * 0.5), (x * 1.1 + rnd(-0.02, 0.02), y - 0.05, z - drop),
               (x * 0.8, y - rnd(0.07, 0.11), z - drop - rnd(0.03, 0.06))]
        cable(f'{prefix}{i}', pts, rnd(0.004, 0.007), mats[i % len(mats)])
        cyl(f'{prefix}{i}_Tip', 0.006, 0.012, pts[-1], 'copper', seg=6, axis='Z', bevel=0)


def octo_muzzle(y_back, y_front, r_out, r_in, prefix='Muzzle'):
    """Boca octogonal T-07; el cuerpo macizo termina 3 cm antes para dejar ver la carga."""
    yb = y_front + 0.03
    cyl(prefix + '_Body', r_out, y_back - yb, (0, (y_back + yb) / 2, 0), 't07', r2=r_out * 0.96, seg=8, bevel=B, smooth=0)
    ring(prefix + '_Face', r_out * 0.96, r_in, yb, y_front, 't07dark', seg=8, smooth=0, phase=math.radians(22.5))
    ring(prefix + '_Bore', r_in, r_in * 0.86, yb, y_front, 'black', seg=12)
    cyl(prefix + '_Charge', r_in * 0.9, 0.003, (0, yb - 0.004, 0), 'magenta', seg=12, bevel=0)


# ------------------------------------------------------------- C-A ---
def c_a():
    """Arrancado crudo: el cañón actual casi literal, con abrazadera y cables colgando."""
    adapter()
    torn_end(0.088, 0.062, -0.055, -0.13)
    cyl('Housing', 0.08, 0.40, (0, -0.32, 0), 't07', seg=8, bevel=B, smooth=0)
    for i, y in enumerate((-0.17, -0.31, -0.45)):
        ring(f'Band{i}', 0.088, 0.078, y, y - 0.026, 't07dark', seg=8, smooth=0, phase=math.radians(22.5))
    for i, x in enumerate((-0.045, -0.015, 0.015, 0.045)):
        cyl(f'Rod{i}', 0.010, 0.40, (x, -0.35, 0.088), 'steel', seg=8)
        box(f'RodClamp{i}', (0.022, 0.018, 0.016), (x, -0.20 - 0.1 * (i % 2), 0.084), 't07dark', bevel=0.003)
    # ventana de carga lateral (lado visto en FPS, +X)
    box('ChargeSlotFrame', (0.012, 0.16, 0.05), (0.078, -0.33, 0.0), 't07dark', bevel=0.004)
    box('ChargeSlot', (0.006, 0.13, 0.026), (0.084, -0.33, 0.0), 'magenta', bevel=0)
    # tapa de corte inferior (CutInterior original queda abajo)
    prism('CutPlate', jagged_outline(0.10, 0.14, teeth=6, depth=0.02), 0.01, (0, -0.10, -0.07), 'rust', plane='XY')
    octo_muzzle(-0.52, -0.664, 0.074, 0.042)
    cables('Cable', [(0.04, -0.075, -0.03), (-0.03, -0.075, -0.045), (0.0, -0.08, -0.055), (-0.05, -0.08, 0.0)])


# ------------------------------------------------------------- C-B ---
def c_b():
    """Núcleo expuesto: carcasa abierta que deja ver la celda de carga magenta."""
    adapter(r_front=0.06, y_front=-0.05)
    torn_end(0.084, 0.064, -0.05, -0.12, arc=(-40, 220))
    # nucleo de carga suspendido
    sphere('Core', 0.062, (0, -0.19, 0.0), 'magenta', u=12, v=8, scale=(1, 1.25, 1))
    ring('CoreCageA', 0.074, 0.066, -0.168, -0.178, 't07dark', seg=12)
    ring('CoreCageB', 0.074, 0.066, -0.205, -0.215, 't07dark', seg=12)
    for i in range(3):
        a = math.radians(90 + 120 * i)
        box(f'CoreBracket{i}', (0.016, 0.16, 0.014), (0.078 * math.cos(a), -0.19, 0.078 * math.sin(a)), 't07dark',
            bevel=0.004, rot=(0, -math.degrees(a) + 90, 0))
    # canon delantero con placa superior pelada hacia arriba
    cyl('Barrel', 0.066, 0.36, (0, -0.44, 0), 't07', seg=8, bevel=B, smooth=0)
    ring('BarrelCollar', 0.076, 0.064, -0.255, -0.29, 't07dark', seg=8, smooth=0, phase=math.radians(22.5))
    prism('PeeledPlate', jagged_outline(0.09, 0.16, teeth=5, depth=0.025), 0.012, (0.0, -0.335, 0.105), 't07',
          plane='XY', rot=(-28, 0, 0))
    for x in (-0.03, 0.03):
        cyl(f'Rod{x}', 0.009, 0.30, (x, -0.47, 0.072), 'steel', seg=8)
    octo_muzzle(-0.60, -0.664, 0.07, 0.04)
    cables('Cable', [(0.05, -0.13, 0.05), (-0.05, -0.13, 0.05), (0.06, -0.12, -0.04), (-0.04, -0.12, -0.06)], seed_n=5)
    # puentes de cable del nucleo al acople, por encima
    cable('Bridge0', [(0.03, -0.17, 0.065), (0.035, -0.11, 0.1), (0.03, -0.05, 0.075)], 0.006, 'cable_r')
    cable('Bridge1', [(-0.03, -0.17, 0.065), (-0.04, -0.11, 0.095), (-0.03, -0.05, 0.07)], 0.006, 'cable')


# ------------------------------------------------------------- C-C ---
def c_c():
    """Antebrazo completo: conserva la concha del antebrazo del T-07 y restos de su mano."""
    adapter(r_front=0.06, y_front=-0.05)
    # concha de antebrazo T-07 desplazada arriba-fuera (lado -X, lejos de la camara)
    ring('ForearmShell', 0.108, 0.090, -0.04, -0.42, 't07', seg=10, arc=(20, 200), jag=0.03, jag_end='y0',
         loc=(-0.012, 0, 0.012))
    ring('ShellCut', 0.110, 0.088, -0.036, -0.046, 'cut_hot', seg=10, arc=(20, 200), jag=0.012, jag_end='both',
         loc=(-0.012, 0, 0.012))
    shards('ShellBurr', (-0.012, 0.012), 0.108, 8, -0.04, size=0.018, arc=(20, 200))
    for i in range(3):
        y = -0.17 - i * 0.07
        box(f'ShellVent{i}', (0.05, 0.03, 0.012), (-0.03, y, 0.112), 'magenta', bevel=0.002, rot=(0, -20, 0))
        box(f'ShellVentFrame{i}', (0.064, 0.044, 0.01), (-0.03, y, 0.108), 't07dark', bevel=0.003, rot=(0, -20, 0))
    # canon en el eje
    cyl('Barrel', 0.058, 0.56, (0, -0.35, 0), 't07dark', seg=10, bevel=0.006)
    for i in range(5):
        ring(f'Rib{i}', 0.068, 0.056, -0.44 - i * 0.03, -0.452 - i * 0.03, 't07', seg=10)
    cyl('Piston', 0.011, 0.24, (0.0, -0.30, 0.074), 'steel', seg=8, rot=(4, 0, 0))
    cyl('PistonSleeve', 0.016, 0.09, (0.0, -0.22, 0.072), 't07dark', seg=8)
    cyl('Flare', 0.064, 0.05, (0, -0.615, 0), 't07', r2=0.076, seg=8, bevel=B, smooth=0)
    ring('FlareFace', 0.076, 0.040, -0.638, -0.666, 't07dark', seg=8, smooth=0)
    ring('Bore', 0.040, 0.034, -0.638, -0.665, 'black', seg=12)
    cyl('BoreCharge', 0.037, 0.003, (0, -0.642, 0), 'magenta', seg=12, bevel=0)
    # restos de dedos del T-07 colgando bajo el acople
    for i, x in enumerate((-0.03, 0.0, 0.03)):
        box(f'Knuckle{i}', (0.022, 0.03, 0.026), (x, -0.09, -0.07), 't07', bevel=0.006)
        box(f'FingerStub{i}', (0.018, 0.022, 0.05), (x, -0.075 + 0.01 * i, -0.105), 't07dark', bevel=0.005, rot=(25 + 10 * i, 0, 0))
    cables('Cable', [(0.05, -0.06, 0.02), (-0.06, -0.06, 0.07), (0.03, -0.07, -0.05)], seed_n=9)


DIRECTIONS = [
    dict(id='C-A', slug='ArrancadoCrudo', name='Arrancado crudo', build=c_a, heat='ranura lateral + ánima',
         idea='El cañón actual casi literal: corte con rebabas, cables y abrazadera soldada.'),
    dict(id='C-B', slug='NucleoExpuesto', name='Núcleo expuesto', build=c_b, heat='núcleo magenta central',
         idea='Carcasa reventada que deja a la vista la celda de carga magenta.'),
    dict(id='C-C', slug='AntebrazoCompleto', name='Antebrazo completo', build=c_c, heat='rejillas de la concha',
         idea='Se arranca también la concha del antebrazo y restos de la mano del T-07.'),
]

if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    run_weapon(HERE, 'Cañón T-07', L, DIRECTIONS, selected=args or None, sheet_name='T07Cannon')
