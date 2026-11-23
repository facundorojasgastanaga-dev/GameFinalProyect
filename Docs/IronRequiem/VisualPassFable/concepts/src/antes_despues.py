"""IRON REQUIEM - Pase visual con Fable (F1 d): laminas "antes / despues" por tramo.

Solo coloca lado a lado (sin retoque) una captura F0 de una sala del tramo (izquierda, estado actual en
motor) y la lamina de concepto del tramo (derecha, propuesta). No son la misma camara ni la misma sala
exacta: sirven para comparar ambiente, luz y densidad, no encuadre.

  antes_despues_tramo_1.png   F0 Map01_R03_Dormitorios_vp1_vacio.jpg | tramo_1_profundidades.png
  antes_despues_tramo_2.png   F0 Map01_R08_Almacenes_vp1_vacio.jpg   | tramo_2_infraestructura.png
  antes_despues_tramo_3.png   F0 Map01_R18_Boveda_vp2_vacio.jpg      | tramo_3_superficie.png

Ejecucion (despues de tramos_concepts.py):
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --factory-startup --python antes_despues.py
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fable_lib as L

PAIRS = [
    ('antes_despues_tramo_1.png', 'Map01_R03_Dormitorios_vp1_vacio.jpg', 'tramo_1_profundidades.png'),
    ('antes_despues_tramo_2.png', 'Map01_R08_Almacenes_vp1_vacio.jpg', 'tramo_2_infraestructura.png'),
    ('antes_despues_tramo_3.png', 'Map01_R18_Boveda_vp2_vacio.jpg', 'tramo_3_superficie.png'),
]

for out, before, after in PAIRS:
    b = os.path.join(L.F0, 'zones', before)
    a = os.path.join(L.OUT, after)
    if not (os.path.exists(b) and os.path.exists(a)):
        print('VPF falta', b if not os.path.exists(b) else a)
        continue
    size = L.side_by_side(os.path.join(L.OUT, out), b, a)
    print('VPF', out, size, L.image_stats(os.path.join(L.OUT, out)))
