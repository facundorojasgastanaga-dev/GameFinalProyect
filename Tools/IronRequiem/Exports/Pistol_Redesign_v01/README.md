# Pistola P-A «Monolito» — Redesign v01, fase B2

Versión de producción del boceto P-A (`Docs/IronRequiem/Concepts/Weapons/Redesign_v01/Pistol`): misma silueta, rejilla de calor de 5 ranuras en el lomo, paneles laterales con luz cian, pestañas naranja, caña con protectores y boca de acero. El collar del boceto se sustituye por el collar IR-ACOPLE-01.

Regenerar (unos 75 s):

```
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python-exit-code 1 --python Tools\IronRequiem\Blender\weapons_redesign_v01_pistol.py
```

Script: `Tools/IronRequiem/Blender/weapons_redesign_v01_pistol.py` y la librería común `weapons_redesign_v01_lib.py`. Blend de trabajo: `Tools/IronRequiem/Blender/Working/WeaponsRedesign_v01/PistolMonolito.blend`.

## Archivos

| Archivo | Contenido |
|---|---|
| `PistolMonolito.fbx` | `PistolMonolito_LOD0/1/2` (1.256 / 628 / 314 tris, triangulados), vacíos `Attachment` (0,0,0) y `Muzzle` (0,−0,379,0) |
| `PistolMonolito_BaseColor.png` | 1024², sRGB |
| `PistolMonolito_MetalSmooth.png` | 1024², lineal: metálico en R (=G=B) y smoothness en A (URP/Lit, Source = Metallic Alpha) |
| `PistolMonolito_Emission.png` | 1024², sRGB. Cian = luces de estado. Blanco = ranuras de calor tintables. |
| `*_render_34/perfil/fps.png` | Eevee a 1200 px. La vista FPS usa una cámara 0,35 m detrás y 0,12 m sobre el acople, con FOV 60. |
| `*_render_comparacion.png` | Perfil frente a `PistolModule_1000.fbx`, importado solo en memoria |
| `metrics.json` | Tris, slots, UV, horneado, dimensiones, sockets y verificación por reimportación |

## Slots (mismo orden en los 3 LOD)

0. `PistolMonolito_Surface`: todo lo no tintable, cian incluido mediante el mapa de emisión.
1. `Heat_Emissive`: `VentSlot0..4`. Color de vértice `HeatStep` (R): 0 / 0,25 / 0,5 / 0,75 / 1, de atrás hacia delante.

## Importación en Unity

`globalScale 1`, `useFileScale 1` y `bakeAxisConversion 0`. Normales en Import, porque llevan Weighted Normal. Tangentes en Calculate y `materialImportMode` en None. El LODGroup sale de los sufijos `_LOD`. Colocar el arma por `Attachment`. El disparo queda en +Z de Unity.
