"""IRON REQUIEM - Modulo principal (fuego rapido): direcciones visuales (Redesign_v01).

Regenera los bocetos y la lamina de esta carpeta:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
     --python-exit-code 1 --python primary_concepts.py -- [PR-A PR-B PR-C]
Referencia actual: IR_PrimaryModule_v02 (0,333 m acople-boca, capsulas de calor).
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from ir_concept_lib import *  # noqa

L = 0.333


def capsule(name, loc, rot, length=0.062, r=0.011, glow='heat'):
    """Capsula de calor: cristal emisivo con tapas de grafito."""
    cyl(name + '_Glass', r, length, loc, glow, seg=8, rot=rot, bevel=0.001, axis='Z')
    off = Vector((0, 0, length / 2)).copy()
    off.rotate(Euler([math.radians(a) for a in rot]))
    for s in (-1, 1):
        cyl(f'{name}_Cap{s}', r * 1.3, 0.009, tuple(Vector(loc) + off * s), 'graphite', seg=8, rot=rot, axis='Z')


# ------------------------------------------------------------- PR-A ---
def pr_a():
    """Corona de capsulas: tres capsulas inclinadas sobre el lomo, eyectables."""
    collar()
    box('Body', (0.088, 0.21, 0.082), (0, -0.13, 0.0), 'plate', bevel=0.009, top=(0.85, 0.95))
    box('Belly', (0.075, 0.23, 0.03), (0, -0.14, -0.042), 'graphite', bevel=0.006)
    box('Cradle', (0.06, 0.15, 0.022), (0, -0.13, 0.048), 'graphite', bevel=0.005)
    for i, y in enumerate((-0.08, -0.13, -0.18)):
        capsule(f'Cap{i}', (0, y, 0.078), (-28, 0, 0), glow='heat_hi' if i == 2 else 'heat')
        box(f'Clip{i}', (0.03, 0.012, 0.03), (0, y + 0.012, 0.062), 'graphite', bevel=0.003)
    for s in (-1, 1):
        box(f'Status{s}', (0.003, 0.07, 0.007), (s * 0.0445, -0.15, -0.005), 'cyan', bevel=0)
        box(f'Tab{s}', (0.008, 0.025, 0.03), (s * 0.045, -0.055, 0.0), 'orange', bevel=0.003)
    cyl('Barrel', 0.019, 0.10, (0, -0.28, 0.0), 'graphite', seg=12)
    for i in range(3):
        ring(f'Radiator{i}', 0.030, 0.018, -0.245 - i * 0.022, -0.253 - i * 0.022, 'steel', seg=12)
    ring('Muzzle', 0.026, 0.014, -0.31, -0.333, 'graphite', seg=12)
    cyl('MuzzleBore', 0.014, 0.004, (0, -0.32, 0), 'black', seg=10, bevel=0)
    for s in (-1, 1):
        box(f'MuzzlePort{s}', (0.012, 0.012, 0.006), (s * 0.026, -0.322, 0), 'heat', bevel=0)


# ------------------------------------------------------------- PR-B ---
def pr_b():
    """Rotativa: motor corto y tres canas giratorias con anillo de calor."""
    collar()
    cyl('Motor', 0.052, 0.09, (0, -0.065, 0.0), 'plate', r2=0.048, seg=10, bevel=0.006)
    box('MotorTop', (0.05, 0.08, 0.03), (0, -0.07, 0.05), 'graphite', bevel=0.006)
    box('Counterweight', (0.03, 0.03, 0.016), (0, -0.07, 0.07), 'orange', bevel=0.004)
    for s in (-1, 1):
        box(f'Status{s}', (0.004, 0.04, 0.007), (s * 0.05, -0.06, 0.0), 'cyan', bevel=0)
    # anillo de calor/OVERDRIVE entre motor y canas
    ring('HeatRing', 0.050, 0.030, -0.110, -0.126, 'heat', seg=16)
    ring('HeatRingGuard', 0.054, 0.046, -0.106, -0.130, 'graphite', seg=16, arc=(200, 340))
    cyl('Hub', 0.032, 0.20, (0, -0.22, 0), 'graphite', seg=6, bevel=0.002)
    for i in range(3):
        a = math.radians(90 + 120 * i)
        x, z = 0.026 * math.cos(a), 0.026 * math.sin(a)
        cyl(f'Barrel{i}', 0.0125, 0.205, (x, -0.228, z), 'graphite', seg=8)
        cyl(f'BarrelTip{i}', 0.014, 0.012, (x, -0.322, z), 'steel', seg=8)
        cyl(f'Bore{i}', 0.0075, 0.003, (x, -0.3295, z), 'black', seg=8, bevel=0)
    for y in (-0.19, -0.27):
        cyl(f'Clamp{y}', 0.046, 0.012, (0, y, 0), 'plate', seg=6, bevel=0.002, smooth=0)
    cyl('FrontPlate', 0.044, 0.006, (0, -0.312, 0), 'graphite', seg=6, bevel=0.001, smooth=0)


# ------------------------------------------------------------- PR-C ---
def pr_c():
    """Espina radiadora: cuerpo bajo con aletas que se encienden de atras hacia delante."""
    collar()
    box('Body', (0.084, 0.26, 0.068), (0, -0.148, -0.01), 'plate', bevel=0.008, top=(0.9, 0.92), shift=(0, 0.008))
    box('Keel', (0.05, 0.24, 0.022), (0, -0.16, -0.05), 'graphite', bevel=0.005)
    box('HeatBar', (0.028, 0.20, 0.018), (0, -0.155, 0.032), 'heat_hi', bevel=0.002)
    for i in range(9):
        y = -0.065 - i * 0.022
        h = 0.05 - abs(i - 4) * 0.003
        box(f'Fin{i}', (0.104 - i * 0.003, 0.0045, h), (0, y, 0.035 + h / 2 - 0.01), 'graphite', bevel=0.0015, top=(0.75, 1.0))
        box(f'FinEdge{i}', (0.10 - i * 0.003, 0.005, 0.004), (0, y, 0.035 + h - 0.012), 'heat' if i < 5 else 'steel', bevel=0)
    for s in (-1, 1):
        box(f'Status{s}', (0.003, 0.06, 0.006), (s * 0.0425, -0.20, -0.018), 'cyan', bevel=0)
        box(f'Tab{s}', (0.008, 0.03, 0.026), (s * 0.043, -0.05, -0.01), 'orange', bevel=0.003)
    box('Shroud', (0.052, 0.07, 0.048), (0, -0.295, -0.004), 'graphite', bevel=0.006, top=(0.85, 0.9))
    for i in range(3):
        box(f'ShroudSlot{i}L', (0.054, 0.008, 0.01), (0, -0.28 - i * 0.017, 0.012), 'black', bevel=0)
    ring('Muzzle', 0.022, 0.012, -0.322, -0.334, 'steel', seg=12)
    cyl('MuzzleBore', 0.012, 0.004, (0, -0.326, 0), 'black', seg=10, bevel=0)


DIRECTIONS = [
    dict(id='PR-A', slug='CoronaCapsulas', name='Corona de cápsulas', build=pr_a, heat='3 cápsulas en el lomo',
         idea='Tres cápsulas de calor inclinadas sobre el lomo que se eyectan al saturarse.'),
    dict(id='PR-B', slug='Rotativa', name='Rotativa', build=pr_b, heat='anillo tras las cañas',
         idea='Mini rotativa de tres cañas con anillo de calor entre motor y cañas.'),
    dict(id='PR-C', slug='EspinaRadiadora', name='Espina radiadora', build=pr_c, heat='barra y aletas',
         idea='Cuerpo bajo con aletas de radiador que se encienden progresivamente.'),
]

if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    run_weapon(HERE, 'Módulo principal', L, DIRECTIONS, selected=args or None, sheet_name='Primary')
