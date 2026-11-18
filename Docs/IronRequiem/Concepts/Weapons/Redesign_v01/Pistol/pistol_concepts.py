"""IRON REQUIEM - Pistola modular: direcciones visuales (Redesign_v01).

Regenera los bocetos y la lamina de esta carpeta:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
     --python-exit-code 1 --python pistol_concepts.py -- [P-A P-B P-C]
Sin argumentos regenera todas. Referencia actual: PistolModule_1000 (0,379 m acople-boca).
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from ir_concept_lib import *  # noqa

L = 0.379


def muzzle(y_front, r_out=0.032, r_in=0.018, depth=0.03, name='Muzzle'):
    """Boca: anillo de metal desnudo + anima negra."""
    ring(name + '_Rim', r_out, r_in, y_front + depth, y_front, 'graphite', seg=16)
    ring(name + '_Edge', r_out + 0.001, r_in, y_front + 0.004, y_front, 'steel', seg=16)
    cyl(name + '_Bore', r_in, 0.004, (0, y_front + depth * 0.7, 0), 'black', seg=12, bevel=0)


# ------------------------------------------------------------- P-A ---
def p_a():
    """Monolito: evolucion directa del bloque recto actual, rejilla de calor superior."""
    collar()
    cyl('Adapter', 0.048, 0.03, (0, -0.036, 0.0), 'graphite', r2=0.052, seg=8, bevel=0.003)
    box('Body', (0.096, 0.235, 0.104), (0, -0.168, 0.006), 'plate', bevel=0.009, top=(0.86, 0.96))
    box('Frame', (0.088, 0.27, 0.034), (0, -0.18, -0.045), 'graphite', bevel=0.006)
    for s in (-1, 1):
        box(f'SidePanel{s}', (0.006, 0.12, 0.04), (s * 0.049, -0.19, -0.012), 'graphite', bevel=0.003)
        box(f'Status{s}', (0.003, 0.055, 0.007), (s * 0.0525, -0.205, -0.012), 'cyan', bevel=0)
        box(f'Tab{s}', (0.008, 0.03, 0.04), (s * 0.05, -0.075, 0.012), 'orange', bevel=0.003)
    # rejilla de calor/OVERDRIVE sobre el lomo: visible desde la camara FPS
    box('VentFrame', (0.062, 0.11, 0.016), (0, -0.155, 0.062), 'graphite', bevel=0.004)
    for i in range(5):
        box(f'VentSlot{i}', (0.046, 0.009, 0.006), (0, -0.115 - i * 0.02, 0.0705), 'heat', bevel=0)
    box('RailFront', (0.04, 0.06, 0.014), (0, -0.25, 0.064), 'graphite', bevel=0.003)
    # cana y protectores de boca
    cyl('Barrel', 0.028, 0.08, (0, -0.315, -0.004), 'graphite', seg=12)
    box('ShroudTop', (0.07, 0.05, 0.02), (0, -0.315, 0.04), 'graphite', bevel=0.005, top=(0.8, 0.8))
    box('ShroudLow', (0.07, 0.04, 0.018), (0, -0.31, -0.045), 'graphite', bevel=0.005)
    muzzle(-0.378, r_out=0.033, r_in=0.019)


# ------------------------------------------------------------- P-B ---
def p_b():
    """Tambor de celdas: un cilindro rotatorio de seis celdas que se calientan."""
    collar()
    box('RearHousing', (0.092, 0.085, 0.10), (0, -0.065, 0.008), 'plate', bevel=0.010, top=(0.9, 0.9))
    for s in (-1, 1):
        box(f'Tab{s}', (0.008, 0.028, 0.034), (s * 0.048, -0.06, 0.02), 'orange', bevel=0.003)
        box(f'Status{s}', (0.003, 0.03, 0.006), (s * 0.0465, -0.085, -0.02), 'cyan', bevel=0)
    # tambor (eje Y), celdas en canales
    cyl('Drum', 0.054, 0.085, (0, -0.152, 0.0), 'graphite', seg=6, bevel=0.006, smooth=0)
    for i in range(6):
        a = math.radians(30 + 60 * i)
        x, z = 0.050 * math.cos(a), 0.050 * math.sin(a)
        m = 'heat_hi' if i in (1,) else 'heat'
        cyl(f'Cell{i}', 0.0105, 0.064, (x, -0.152, z), m, seg=8, bevel=0.001)
    ring('DrumCapRear', 0.056, 0.02, -0.108, -0.114, 'steel', seg=6, phase=math.radians(30))
    ring('DrumCapFront', 0.056, 0.02, -0.190, -0.196, 'steel', seg=6, phase=math.radians(30))
    # puente superior y cana
    box('TopStrap', (0.05, 0.26, 0.018), (0, -0.17, 0.066), 'plate', bevel=0.005)
    box('Hinge', (0.02, 0.03, 0.03), (0, -0.20, 0.048), 'graphite', bevel=0.004)
    box('LowerFrame', (0.05, 0.22, 0.026), (0, -0.19, -0.058), 'graphite', bevel=0.005)
    cyl('Barrel', 0.021, 0.17, (0, -0.285, 0.0), 'graphite', seg=12)
    box('BarrelShroud', (0.062, 0.12, 0.046), (0, -0.282, 0.022), 'plate', bevel=0.008, top=(0.8, 1.0))
    box('BarrelShroudBand', (0.066, 0.012, 0.05), (0, -0.25, 0.022), 'graphite', bevel=0.003)
    muzzle(-0.372, r_out=0.03, r_in=0.016)


# ------------------------------------------------------------- P-C ---
def p_c():
    """Bobina en horquilla: dos patas abiertas con una bobina expuesta que se pone al rojo."""
    collar()
    box('Body', (0.10, 0.13, 0.112), (0, -0.085, 0.004), 'plate', bevel=0.010, top=(0.88, 0.92))
    box('BodyCore', (0.075, 0.03, 0.08), (0, -0.155, 0.0), 'graphite', bevel=0.006)
    for s in (-1, 1):
        box(f'Tab{s}', (0.008, 0.035, 0.03), (s * 0.051, -0.05, -0.02), 'orange', bevel=0.003)
        box(f'Status{s}', (0.003, 0.05, 0.006), (s * 0.0515, -0.10, 0.03), 'cyan', bevel=0)
    # horquilla superior (placa clara) e inferior (grafito)
    box('ProngTop', (0.074, 0.23, 0.026), (0, -0.265, 0.044), 'plate', bevel=0.006, top=(0.8, 0.9), shift=(0, 0.01))
    box('ProngTopSpine', (0.03, 0.2, 0.012), (0, -0.255, 0.062), 'graphite', bevel=0.003)
    box('ProngLow', (0.06, 0.21, 0.024), (0, -0.255, -0.046), 'graphite', bevel=0.006)
    for s in (-1, 1):
        box(f'SideRail{s}', (0.008, 0.08, 0.07), (s * 0.034, -0.205, 0.0), 'graphite', bevel=0.003)
    # bobina de calor/OVERDRIVE
    cyl('CoilCore', 0.013, 0.20, (0, -0.265, 0.0), 'heat_hi', seg=10)
    for i in range(6):
        ring(f'CoilWrap{i}', 0.024, 0.014, -0.19 - i * 0.03, -0.202 - i * 0.03, 'heat' if i % 2 else 'copper', seg=12)
    # anillo de enfoque = boca
    ring('FocusRing', 0.036, 0.022, -0.355, -0.378, 'graphite', seg=16)
    ring('FocusEdge', 0.037, 0.022, -0.374, -0.379, 'steel', seg=16)
    cyl('FocusLens', 0.022, 0.003, (0, -0.362, 0), 'cyan', seg=12, bevel=0)


DIRECTIONS = [
    dict(id='P-A', slug='Monolito', name='Monolito', build=p_a, heat='rejilla superior',
         idea='Evolución del bloque recto actual con rejilla de calor en el lomo.'),
    dict(id='P-B', slug='TamborCeldas', name='Tambor de celdas', build=p_b, heat='6 celdas del tambor',
         idea='Tambor hexagonal de celdas que gira y se pone al rojo.'),
    dict(id='P-C', slug='BobinaHorquilla', name='Bobina en horquilla', build=p_c, heat='bobina expuesta',
         idea='Horquilla abierta con bobina expuesta entre dos patas.'),
]

if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    run_weapon(HERE, 'Pistola', L, DIRECTIONS, selected=args or None, sheet_name='Pistol')
