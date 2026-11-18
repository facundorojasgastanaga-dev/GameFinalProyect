# Cañón T-07: pivote del arma

16 de noviembre de 2026. Blender 5.2 y Unity 6000.5.5f1. Skills: `iron-requiem-enemy-ai`, `iron-requiem-performance` y `iron-requiem-modular-weapons`. Lectura previa: T07_INSPECTION, T07_OPTIMIZATION, ENEMY_V01, CHARACTER_PREPARATION_REPORT, VISUAL_AUDIT_F0 y CANNON_CHARGE.

## Problema

`Assets/IronRequiem/Art/Weapons/T07CannonClean/T07_CannonClean.fbx` conserva el origen del robot completo. Ese origen queda unos 7,7 cm fuera del eje del cañón, sus ejes locales siguen alineados con el mundo y el cañón está inclinado unos 31° respecto de la vertical. Por eso `Weapon_T07_Temporary_Optimized` usa escala 100, una rotación compuesta y varios scripts (`StolenShellFit`, `StolenChargeAnchor`, `EnemyMuzzleAnchor`) que miden la boca en tiempo de ejecución. Las armas propias (`PistolModule_1000` y `IR_PrimaryModule_v02`) usan otra convención: origen en el acople trasero, disparo por +Z y sockets `Attachment`/`Muzzle`.

## Inspección (Blender, `blender_asset_tool.py inspect`)

| Pieza | Triángulos | Materiales | Huesos / acciones | Bordes abiertos / no manifold | Islas |
|---|---:|---|---|---|---:|
| CannonClean_LOD0 | 1.702 | OriginalCannonSurface, CutInterior | 0 / 0 | 0 / 0 | 1 |
| CannonClean_LOD1 | 906 | idénticos | 0 / 0 | 0 / 0 | 1 |
| CannonClean_LOD2 | 412 | idénticos | 0 / 0 | 0 / 0 | 1 |

Escala 1, unidades métricas y sin texturas incrustadas: los materiales los asigna Unity. LOD0 tiene un volumen mundial de 0,16 × 0,43 × 0,61 m, con origen en (−0,300, −0,027, 1,092). Los informes del resto de armas inspeccionadas también están en `Tools/IronRequiem/Reports/WeaponPivot/inspect-*.json`.

Medición del LOD0 (`probe_cannon_pivot.py`, solo lectura):

- **Boca:** 10 vértices en los últimos 8 mm del eje principal. Plano con RMS de 1,8 mm, centro en (−0,3154, −0,2148, 0,5363) y normal (−0,074, −0,513, −0,855). Coincide con la normal medida en CANNON_CHARGE (0,071, −0,576, −0,814), cuyo X está invertido por la conversión FBX. Esto confirma que es la boca y no el codo.
- **Extremo trasero:** el corte horizontal del codo está en z = 1,1117. El acople es su proyección sobre el eje de disparo: (−0,2661, 0,1260, 1,1045). Queda a 2,1 cm del centro del corte, y el cañón mide **0,6644 m** desde el acople hasta la boca.
- **Corte lateral (CutInterior):** 6 caras con normal media (0,873, 0, 0,154). Se orienta hacia −Y local (abajo en Unity), así que en primera persona la tapa queda del lado contrario a la cámara.

## Exportación

`Tools/IronRequiem/Exports/T07_Cannon_PivotFixed/`:

- `T07_Cannon_PivotFixed.fbx` (SHA-256 `C9F8132E…1DC45FF2`): tres LOD triangulados en Blender con el mismo marco horneado; vacíos `Attachment` (0,0,0) y `Muzzle` (0, −0,6644, 0), hijos de LOD0.
- Entrada explícita: `frames.json` (origen/adelante/arriba) y `empties.json`; selección por nombre exacto, sin separar islas.
- Evidencia: `export-report.json`, `reimport-inspect.json`, `reimport-probe.json`, `verify_hashes.ps1`.
- `superseded/`: primera exportación sin triangular. Unity la reimportó con 1.562/807/355 triángulos porque retriangula mal los n-gonos cóncavos de las tapas. Se conserva solo como diagnóstico.

En la reimportación en Blender quedan 1.702/906/412 triángulos, 2 materiales, 0 bordes abiertos y 1 isla. La boca refitada de LOD0 cae en (0, −0,6644, 0) con normal exactamente −Y. En LOD1 y LOD2 la boca simplificada se desvía 1–4 cm.

`blender_asset_tool.py` añade `--frames-json`, `--empties-json` y `--triangulate` (ver USAGE.md); las opciones anteriores no cambian. Respaldo del original en `Tools/IronRequiem/Backups/20261116-weapon-pivot/`.

## Unity

`Assets/IronRequiem/Editor/WeaponPivotAuthoring.cs` (menú `Tools > Iron Requiem > Weapons`, o la petición `Tools/IronRequiem/Requests/weapon-pivot.txt` con `build`, `verify` o `test`):

1. Copia la exportación a `Assets/IronRequiem/Art/Weapons/T07CannonPivotFixed/T07_Cannon_PivotFixed.fbx`, un asset derivado que se refresca y conserva su GUID. La importa con la misma convención que el cañón fuente: escala de archivo, sin bake de ejes y sin materiales. Activar el bake de ejes invertía el cañón, así que se descartó.
2. Crea en una escena de previsualización `Assets/IronRequiem/Prefabs/Weapons/Visuals/T07_Cannon_PivotFixed_Visual.prefab`, una **variante del modelo**:
   - Reutiliza el LODGroup que genera el importador (0,18/0,06/0,003, los umbrales del cañón en T07_Common_Optimized), sin fundido.
   - Asigna los materiales URP del cañón equipado (`T07_URP_FPS`, `ExposedMechanism_FPS`).
   - Añade en la raíz los sockets `WeaponAttachment` y `WeaponMuzzle` con escala 1, adelante +Z y arriba +Y. Los nodos importados mantienen el habitual −90° X ×100 del FBX y los vacíos de Blender quedan como referencia.
3. Escribe `Tools/IronRequiem/Reports/WeaponPivot/unity-verify.json`.

**Resultado en Unity:**

| Comprobación | Valor |
|---|---|
| Variante del FBX derivado | sí |
| WeaponAttachment en la raíz | (0, 0, 0) |
| WeaponMuzzle en la raíz / esperado | (0, 0, 0,66442) / (0, 0, 0,6644), error de 0,02 mm |
| Adelante / arriba del muzzle | +Z / +Y |
| Escala global de los sockets | 1 |
| Sockets frente a vacíos de Blender | 0 mm |
| Triángulos LOD0/1/2 | **1.702 / 906 / 412** (el fuente importado en Unity da 1.694 / 807 / 401) |
| Materiales por LOD | 2, URP |
| Scripts ausentes | 0 |

No se abre, modifica ni guarda ninguna escena. `Weapon_T07_Temporary_Optimized` no se modifica: allí el renderer de CannonClean está desactivado (`m_Enabled: 0`) y lo visible es `VP_StolenShell`. La variante queda lista para sustituir esa representación o la del módulo del mundo en una tarea autorizada. En ese caso, `socketToShellRotation` pasaría a identidad y los scripts de medición ya no tendrían que compensar el giro.

Pruebas EditMode en `Assets/IronRequiem/Tests/EditMode/WeaponPivotAssetTests.cs` (pivote, boca, orientación y escala de los sockets; LOD, triángulos y materiales URP): **2/2 aprobadas** en el Editor abierto mediante TestRunnerApi (`edit-tests.xml`).

## Hashes

`Tools/IronRequiem/Reports/WeaponPivot/hash-comparison.json`, repetida después de la construcción final:

| Archivo | Sin cambios |
|---|---|
| Fuente T-07 `Meshy_AI_Rustborn_Sentinel_20261028_texture.fbx` (`A0E48EAF…FAA6238`) | sí |
| Su `.meta` (`3804C94F…A0DCD86`) | sí |
| `T07_CannonClean.fbx` (`4426CA8C…63C99557842`) | sí |
| `Weapon_T07_Temporary_Optimized.prefab` y `.meta` | sí, igual que el respaldo |

## Límites

- No hay prueba en Play Mode ni medición de rendimiento: la entrega cambia pivotes, no la carga.
- La consola contiene errores CS1501 (`GetMesh`) en `RobotPortraitTests.cs` (PlayMode) y `HandsHudCampaignVerification.cs`. Son archivos ajenos a esta tarea y no se tocaron.
