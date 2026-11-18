# Cañón T-07 C-A «Arrancado crudo» — Redesign v01, fase B2

Versión de producción del boceto C-A (`Docs/IronRequiem/Concepts/Weapons/Redesign_v01/T07Cannon`). Conserva el cañón octogonal del T-07 con tres bandas y cuatro varillas, la ventana de carga magenta en el lado +X (el que se ve en FPS) y la carga en el ánima. También mantiene el extremo arrancado: corte de acero, borde caliente, rebabas, interior oxidado y chapa inferior dentada. Los cuatro cables cuelgan hacia delante, y la abrazadera del jugador lleva pernos y flejes naranja. Se usa la misma semilla que el boceto, así que los dientes, las rebabas y los cables mantienen su forma. El blindaje toma el tono medio gris azulado de `T07_BaseColor.png`, leído sin modificarlo.

Regenerar (unos 80 s):

```
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python-exit-code 1 --python Tools\IronRequiem\Blender\weapons_redesign_v01_t07cannon.py
```

Script: `Tools/IronRequiem/Blender/weapons_redesign_v01_t07cannon.py` y `weapons_redesign_v01_lib.py`. Blend: `Tools/IronRequiem/Blender/Working/WeaponsRedesign_v01/T07CannonCrudo.blend`.

## Archivos

| Archivo | Contenido |
|---|---|
| `T07CannonCrudo.fbx` | `T07CannonCrudo_LOD0/1/2` (2.420 / 1.210 / 604 tris), `Attachment` (0,0,0), `Muzzle` (0,−0,664,0) en el eje del ánima |
| `T07CannonCrudo_BaseColor.png` / `_MetalSmooth.png` / `_Emission.png` | 1024² |
| `*_render_*.png` | 3/4, perfil, FPS y comparación con `T07_Cannon_PivotFixed.fbx`, que se ve gris porque su FBX no trae texturas |
| `metrics.json` | Métricas y verificación |

## Slots

0. `T07CannonCrudo_Surface`
1. `Heat_Emissive`: `Torn_CutHot`, el borde del corte caliente.
2. `Charge_Emissive`: `ChargeSlot` (ventana lateral) y `Muzzle_Charge`, el disco del ánima, 26 mm por detrás de la boca.

## Densidad de texel

A 1024², la densidad queda en **≈ 737 px/m**, por debajo del objetivo de 1.000–1.400. Las caras visibles suman 0,84 m², 4,5 veces lo que tiene la pistola en caras visibles. Las varillas, las bandas, los pernos, los flejes, las abrazaderas y los terminales ya comparten UV (apilado intencionado), y las caras enterradas ocupan una celda mínima. La opción `IR_TEX=2048` (no ejecutada) duplicaría la densidad hasta el tope del script, 1.350 px/m. Escribe con los mismos nombres de archivo.

## Importación en Unity

Igual que las otras armas, con escala 1 y `bakeAxisConversion 0`. Los nodos se llaman `Attachment` y `Muzzle`, no `WeaponAttachment`/`WeaponMuzzle`, y cuelgan de la raíz, no de LOD0.
