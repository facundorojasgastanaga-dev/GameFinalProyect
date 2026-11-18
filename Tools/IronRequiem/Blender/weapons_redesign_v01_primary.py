"""IRON REQUIEM - Modulo principal PR-C "Espina radiadora" (Redesign v01, fase B2: produccion).

Parte del boceto PR-C de Docs/IronRequiem/Concepts/Weapons/Redesign_v01/Primary/primary_concepts.py
(cuerpo bajo de placa clara, quilla de grafito, barra de calor central, 9 aletas de radiador cuyo
borde se enciende de atras hacia delante, luces cian laterales, pestanas naranja, protector frontal
ranurado y boca de acero) con el collar IR-ACOPLE-01. Largo acople-boca 0,333 m.

  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup \
     --python-exit-code 1 --python Tools/IronRequiem/Blender/weapons_redesign_v01_primary.py
Salida: Tools/IronRequiem/Exports/Primary_Redesign_v01/ y Working/WeaponsRedesign_v01/PrimaryEspina.blend
"""
import os, sys
sys.dont_write_bytecode = True  # no dejar .pyc en Tools/IronRequiem/Blender/__pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from weapons_redesign_v01_lib import *  # noqa

L = 0.333
WEAPON = 'PrimaryEspina'
FINS = 9


def build():
    collar()
    # el cuerpo empieza al final del collar (0,032); el boceto lo metia 14 mm dentro y ensanchaba el collar
    box('Body', (0.084, 0.246, 0.068), (0, -0.155, -0.01), 'plate', bevel=0.008, top=(0.9, 0.92), shift=(0, 0.008))
    box('Keel', (0.05, 0.24, 0.022), (0, -0.16, -0.05), 'graphite', bevel=0.005)
    box('HeatBar', (0.028, 0.20, 0.018), (0, -0.155, 0.032), 'heat_hi', bevel=0.002, step=0.0)
    for i in range(FINS):
        y = -0.065 - i * 0.022
        h = 0.05 - abs(i - 4) * 0.003
        # aleta: 5 mm mas larga hacia abajo para apoyar en el cuerpo (el boceto flotaba 1 mm)
        box(f'Fin{i}', (0.104 - i * 0.003, 0.0045, h + 0.005), (0, y, 0.035 + h / 2 - 0.01 - 0.0025), 'graphite',
            bevel=0.0015, top=(0.75, 1.0))
        # borde emisivo: HeatStep i/8 -> la presentacion puede encenderlas de atras hacia delante
        box(f'FinEdge{i}', (0.10 - i * 0.003, 0.005, 0.004), (0, y, 0.035 + h - 0.011), 'heat_fin', bevel=0.001,
            bevel_top=True, step=round(i / (FINS - 1), 3))
    for s in (-1, 1):
        box(f'Status{s}', (0.006, 0.06, 0.003), (s * 0.0425, -0.20, -0.018), 'cyan', bevel=0.001,
            bevel_top=True, rot=(0, s * 90, 0))
        box(f'Tab{s}', (0.008, 0.03, 0.026), (s * 0.043, -0.05, -0.01), 'orange', bevel=0.003)
    box('Shroud', (0.052, 0.07, 0.048), (0, -0.295, -0.004), 'graphite', bevel=0.006, top=(0.85, 0.9))
    for i in range(3):
        box(f'ShroudSlot{i}', (0.054, 0.008, 0.01), (0, -0.28 - i * 0.017, 0.012), 'black', bevel=0.001,
            bevel_top=True, maxlod=1)
    # boca: tubo de revolucion de acero con bisel frontal
    yb, yf, ro, ri = -0.322, -L, 0.022, 0.012
    lathe('Muzzle', [(ri, yb), (ro, yb), (ro, yf + 0.0012), (ro - 0.0012, yf), (ri + 0.001, yf), (ri, yf + 0.001)],
          12, ['steel'] * 6, closed=True, smooth=40)
    cyl('MuzzleBore', ri + 0.001, 0.004, (0, -0.326, 0), 'black', seg=10, bevel=None, maxlod=1)


if __name__ == '__main__':
    run(WEAPON, 'Primary_Redesign_v01', L, build, budget=1500,
        current_fbx=ROOT / 'Assets/IronRequiem/Art/Weapons/PrimaryModuleV02/IR_PrimaryModule_v02.fbx',
        symmetric=True, seed_id='PR-C', render_heat_level=0.55,
        extra=dict(concept='Docs/IronRequiem/Concepts/Weapons/Redesign_v01/Primary (PR-C Espina radiadora)',
                   heat_parts='HeatBar (HeatStep 0) + FinEdge0..8 (HeatStep 0..1 en pasos de 0,125); renders con nivel 0,55 = 5 aletas encendidas, como el boceto'),
        notes=['Cuerpo acortado 14 mm por detras para dejar el collar de 32 mm del contrato (frente y silueta sin cambios).',
               'Aletas apoyadas en el cuerpo (+5 mm hacia abajo) y bordes elevados 1 mm para eliminar caras coplanares.',
               'Las 9 aletas comparten el slot Heat_Emissive; el escalonado se lee del color de vertice HeatStep.'])
