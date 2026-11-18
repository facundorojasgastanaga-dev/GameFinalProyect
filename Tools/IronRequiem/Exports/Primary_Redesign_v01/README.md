# Módulo principal PR-C «Espina radiadora» — Redesign v01, fase B2

Versión de producción del boceto PR-C (`Docs/IronRequiem/Concepts/Weapons/Redesign_v01/Primary`). Conserva el cuerpo bajo de placa clara, la quilla de grafito, la barra de calor central y las 9 aletas de radiador con borde emisivo. También mantiene las luces cian, las pestañas naranja, el protector ranurado y la boca de acero. Se añade el collar IR-ACOPLE-01. Para que el collar mida 32 mm, el cuerpo empieza 14 mm más adelante; el frente y la silueta no cambian.

Regenerar (unos 75 s):

```
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python-exit-code 1 --python Tools\IronRequiem\Blender\weapons_redesign_v01_primary.py
```

Script: `Tools/IronRequiem/Blender/weapons_redesign_v01_primary.py` y `weapons_redesign_v01_lib.py`. Blend: `Tools/IronRequiem/Blender/Working/WeaponsRedesign_v01/PrimaryEspina.blend`.

## Archivos

| Archivo | Contenido |
|---|---|
| `PrimaryEspina.fbx` | `PrimaryEspina_LOD0/1/2` (1.460 / 730 / 364 tris), `Attachment` (0,0,0), `Muzzle` (0,−0,333,0) |
| `PrimaryEspina_BaseColor.png` / `_MetalSmooth.png` / `_Emission.png` | 1024². Mismo formato que la pistola. |
| `*_render_*.png` | 3/4, perfil, FPS y comparación con `IR_PrimaryModule_v02.fbx`. Los renders muestran el nivel de calor 0,55, con 5 aletas encendidas como en el boceto. |
| `metrics.json` | Métricas y verificación |

## Slots

0. `PrimaryEspina_Surface`
1. `Heat_Emissive`: `HeatBar` (HeatStep 0) y `FinEdge0..8` (HeatStep 0, 0,125 … 1, de atrás hacia delante).

Para encender las aletas una a una, el material del slot 1 tiene que comparar `HeatStep` (color de vértice R) con el nivel de calor. URP/Lit ignora los colores de vértice, así que hace falta un Shader Graph o una variante equivalente. Sin él, `OverheatBehavior` tinta todas las aletas a la vez. Consulta las notas de compatibilidad en el informe B2.

## Importación en Unity

Igual que la pistola: escala 1, `bakeAxisConversion 0`, normales en Import, materiales en None, LODGroup por sufijo y colocación por `Attachment`.
