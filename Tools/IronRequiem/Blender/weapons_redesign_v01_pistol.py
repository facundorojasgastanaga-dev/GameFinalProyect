"""IRON REQUIEM - Pistola P-A "Monolito" (Redesign v01, fase B2: produccion).

Parte del boceto P-A de Docs/IronRequiem/Concepts/Weapons/Redesign_v01/Pistol/pistol_concepts.py
(bloque recto, rejilla de calor de 5 ranuras en el lomo, paneles laterales con luz cian,
pestanas naranja, cana con protectores y boca de acero) y lo lleva a produccion con el
collar IR-ACOPLE-01. Largo acople-boca 0,379 m (igual que PistolModule_1000).

  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
     --python-exit-code 1 --python Tools/IronRequiem/Blender/weapons_redesign_v01_pistol.py
Salida: Tools/IronRequiem/Exports/Pistol_Redesign_v01/ y Working/WeaponsRedesign_v01/PistolMonolito.blend
(IR_COUNT=1 en el entorno solo imprime el recuento de triangulos por pieza).
"""
import os, sys
sys.dont_write_bytecode = True  # no dejar .pyc en Tools/IronRequiem/Blender/__pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from weapons_redesign_v01_lib import *  # noqa

L = 0.379
WEAPON = 'PistolMonolito'


def muzzle(y_front, r_out=0.0335, r_in=0.019, depth=0.03):
    """Boca: un solo tubo de revolucion cerrado; cuerpo de grafito y banda frontal de acero con bisel
    (el boceto solapaba dos anillos con caras frontales coplanares). Anima negra encastrada."""
    yb, yf = y_front + depth, y_front
    prof = [(r_in, yb), (r_out, yb), (r_out, yf + 0.0045), (r_out, yf + 0.0015), (r_out - 0.0015, yf),
            (r_in + 0.001, yf), (r_in, yf + 0.001)]
    lathe('Muzzle', prof, 14, ['graphite', 'graphite', 'steel', 'steel', 'steel', 'steel', 'graphite'],
          closed=True, smooth=40)
    cyl('Muzzle_Bore', r_in + 0.001, 0.004, (0, y_front + depth * 0.7, 0), 'black', seg=10, bevel=None, maxlod=1)


def build():
    collar()
    # adaptador: empieza al final del collar (0,032) para no ensanchar el collar del contrato
    cyl('Adapter', 0.048, 0.022, (0, -0.043, 0.0), 'graphite', r2=0.052, seg=8, bevel=0.003, smooth=30)
    box('Body', (0.096, 0.235, 0.104), (0, -0.168, 0.006), 'plate', bevel=0.009, top=(0.86, 0.96))
    box('Frame', (0.088, 0.27, 0.034), (0, -0.18, -0.045), 'graphite', bevel=0.006)
    for s in (-1, 1):
        box(f'SidePanel{s}', (0.006, 0.12, 0.04), (s * 0.049, -0.19, -0.012), 'graphite', bevel=0.002)
        box(f'Status{s}', (0.007, 0.055, 0.003), (s * 0.0525, -0.205, -0.012), 'cyan', bevel=0.001,
            bevel_top=True, rot=(0, s * 90, 0))
        box(f'Tab{s}', (0.008, 0.03, 0.04), (s * 0.05, -0.075, 0.012), 'orange', bevel=0.003)
    # rejilla de calor/OVERDRIVE sobre el lomo (visible desde la camara FPS); HeatStep de atras a delante
    box('VentFrame', (0.062, 0.11, 0.016), (0, -0.155, 0.062), 'graphite', bevel=0.004)
    for i in range(5):
        box(f'VentSlot{i}', (0.046, 0.009, 0.006), (0, -0.115 - i * 0.02, 0.0705), 'heat', bevel=0.0012,
            bevel_top=True, step=round(i / 4, 3))
    box('RailFront', (0.04, 0.06, 0.014), (0, -0.25, 0.064), 'graphite', bevel=0.003)
    # cana y protectores de boca
    cyl('Barrel', 0.028, 0.08, (0, -0.315, -0.004), 'graphite', seg=12, bevel=None)  # tapas ocultas
    box('ShroudTop', (0.07, 0.05, 0.02), (0, -0.315, 0.04), 'graphite', bevel=0.005, top=(0.8, 0.8))
    box('ShroudLow', (0.07, 0.04, 0.018), (0, -0.31, -0.045), 'graphite', bevel=0.005)
    muzzle(-L)


if __name__ == '__main__':
    run(WEAPON, 'Pistol_Redesign_v01', L, build, budget=1300,
        current_fbx=ROOT / 'Assets/IronRequiem/Art/Weapons/BasicPistolModule/PistolModule_1000.fbx',
        symmetric=True, seed_id='P-A',
        extra=dict(concept='Docs/IronRequiem/Concepts/Weapons/Redesign_v01/Pistol (P-A Monolito)',
                   heat_parts='VentSlot0..4 (HeatStep 0, .25, .5, .75, 1 de atras a delante)'),
        notes=['Collar del boceto (diam. 0,090, 22 mm) sustituido por el collar IR-ACOPLE-01 de 32 mm; el adaptador empieza en 0,032.',
               'Muzzle_Rim/Muzzle_Edge ya no comparten la cara frontal (el boceto tenia caras coplanares).'])
