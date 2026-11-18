# Protagonista v02: refinado, manos modulares y estándar de acople

Trabajo IR-09, iniciado el 16/11/2026. Blender 5.2 en una sesión visible: `Tools/IronRequiem/Blender/Working/protagonist_v02/IR_Protagonist_v02_work.blend`, con el puente `live_bridge_v02.py`. Las fuentes de v01 no se modifican. Sus hashes están en `Tools/IronRequiem/Backups/20261116-protagonist-v02/protected-sha256.txt`.

## 1. Auditoría (antes de cambiar nada)

Evidencia completa en `Tools/IronRequiem/Reports/ProtagonistV02/Audit/` (`audit.md`, `audit.json`, 54 renders). Sin incidencias en ninguna malla de v01: normales invertidas, caras duplicadas, vértices sueltos, aristas no-manifold y escala distinta de 1. La geometría coincide en Source, Rigged, FPS_Parts y FPS_Rigged, con una diferencia máxima de 0,00028 mm.

| # | Severidad | Pieza | Evidencia | Corrección v02 |
|---|---|---|---|---|
| 1 | Crítica | Hand_L/R (todas las LOD) | Una sola isla con palma y dedos fusionados. El 72 % de los vértices tiene ≥2 huesos y 274 tienen 3. Al cerrar el puño, los segmentos se doblan hasta 5,9 mm y la palma 5,7 mm | Separar segmentos por las articulaciones, cerrar tapas, añadir rótulas y pesar 1,0 a un único hueso por pieza |
| 2 | Alta | Muñeca | El corte es oblicuo. La mano sube 15,6 mm dentro del manguito y hay 38 vértices mezclados entre antebrazo y mano. El antebrazo se deforma 6,1 mm | Corte perpendicular en el plano del socket; collar estándar (§2) |
| 3 | Alta | Tapas de corte | N-gons cóncavos de 48–70 lados en antebrazo, mano y codo. En FPS, sin la mano derecha, la tapa queda a la vista | Rehacer las tapas: grid fill o triangulado limpio y espiga mecánica |
| 4 | Media | Codo | El corte parte la rótula en dos | Rótula entera en una sola pieza |
| 5 | Media | FBX FPS | Sin sockets; 50 huesos, 16 de ellos sin vértices | Sockets únicos en el FBX v02 |
| 6 | Media | FBX Rigged | `Wrist_*`, `ModuleSocket_*` y `Elbow_*` están triplicados (.001/.002) | Un vacío por socket |
| 7 | Media | Puño rígido Plan B | 34 interpenetraciones; las 23 piezas comparten la misma UV de cubo y la densidad varía ×4,5 | Puño integrado en la mano v02 con las mismas piezas y UV |
| 8 | Media | Texturas | 760–833 px/m frente a ~2.494 px/m en pantalla a 0,5 m en 1440p; la mano usa el 1,3 % del atlas | Más área de atlas para las manos, si hay espacio libre (§4) |
| 9 | Baja | Simetría | Las manos L/R difieren hasta 15,3 mm; Hand_L_LOD1 tiene una isla suelta y LOD1/LOD2 tienen solapes UV | Mano izquierda como espejo exacto de la derecha |
| 10 | Info | CapsuleRobot(Rig) | Mide ~1,00 m frente a 1,80 m; 742k tris; non-manifold en el Rig | Fuera de alcance: solo referencia de cinemática y brazo superior |

## 2. Estándar de acople (IR-ACOPLE-01)

El mismo contrato sirve para la mano, la pistola, el cañón y el módulo primario. Medidas en metros.

**Marco del socket** (lado del jugador, espejado en la izquierda):
- Nombre: `ModuleSocket_R` / `ModuleSocket_L`. Un único vacío o hueso por lado, sin duplicados `.001`. En Unity se mantiene `RightModuleSocket` en `ProtagonistHandsPresentation`.
- Origen: pivote de la muñeca (cabeza del hueso `Hand_X`), situado en v01 en (−0,41976, 0,0002, 0,99742) en Blender para el lado R.
- +Z local: eje del antebrazo hacia distal, que es también el eje de inserción y de disparo. +Y local: dorso de la mano (arriba, en la pose FPS de agarre). Escala 1.

**Extremo del antebrazo** (macho):
- Corte perpendicular a +Z en z = −0,012.
- Brida Ø 0,078 con bisel de 1,5 mm.
- Espiga Ø 0,064 desde z = −0,012 hasta z = +0,003. La espiga entra 3 mm en el módulo, como el contrato actual: puerto = `Attachment` + forward × 0,003.
- Nada del antebrazo pasa de z = +0,003.

**Módulo** (hembra: mano o arma):
- Origen = `Attachment` = centro de la cara trasera del collar, en z = 0.
- Interfaz obligatoria, idéntica en todos los módulos: alojamiento interior Ø 0,066 con profundidad ≥ 0,006, y cara trasera plana en z = 0.
- Collar exterior Ø 0,080 en la mano, para conservar la silueta: el manguito del antebrazo mide ~0,076. En armas, Ø 0,080–0,092 (los conceptos B1 usan 0,090). Largo 0,032 y bisel de 1,5 mm.
- Ninguna geometría por detrás de z = 0.

**FBX de módulo:**
- Blender: origen en `Attachment`, disparo o dedos hacia −Y, arriba +Z, escala 1.
- Exportación `axis_forward=-Z`, `axis_up=Y`.
- Vacíos `Attachment` (0,0,0) y, en armas, `Muzzle` (0, −L, 0).
- Importación en Unity: `useFileScale 1`, `globalScale 1`, `bakeAxisConversion 0`. El bake invierte el módulo (comprobado con el cañón T-07).
- Socket con nombre único `Attachment`, que es el que reconoce el receptor. `WeaponAttachment` de `T07_Cannon_PivotFixed_Visual` no lo cumple y se corrige en B2.
- Colocación en WeaponRoot por `Attachment`, no por `Muzzle`.

**Mano como módulo:** raíz `Hand_X`, colocada en el socket con offset 0, con su collar Ø 0,080. La mano abierta, la de agarre y el puño cerrado comparten el mismo collar y las mismas piezas.

## 3. Refinado y manos separadas

Scripts reproducibles, que se ejecutan en segundo plano y solo escriben copias:
- `Tools/IronRequiem/Blender/refine_protagonist_v02.py`, con los módulos `_geo`, `_build`, `_fingers` y `_verify`.
- `export_protagonist_v02.py`.

El progreso se fue cargando en la sesión visible de Blender mediante `live_bridge_v02.py`. Hubo tres iteraciones, guardadas en `Working/protagonist_v02/refined/iter1|iter2/`:
1. **Iteración 1:** se cortó la malla fusionada de v01 por las articulaciones. Muñeca, pesos rígidos, codo, sockets y LOD quedaron aprobados, pero los dedos se rechazaron por las tapas oscuras irregulares y las rótulas en forma de corona.
2. **Iteración 2:** los 15 segmentos de dedo se reconstruyeron como piezas mecánicas limpias: prisma octogonal con chaflán, placa dorsal y bisagra de tambor centrada en la articulación, más una palma tallada con ranuras. Con las longitudes de v01 el puño quedaba a medias.
3. **Iteración 3**, por decisión del usuario: falanges 2 y 3 un 28 % más cortas y eje de bisagra oblicuo en el pulgar para que cruce sobre los dedos.

| | v01 | v02 |
|---|---|---|
| Tris LOD0 / LOD1 / LOD2 | 21.108 / 11.628 / 6.122 | **23.480 / 11.954 / 6.490** (presupuesto LOD0 ≤ 26.000) |
| Tris FPS (antebrazos + manos) | 6.070 | **8.768** (≤ 9.000) |
| Máx. huesos por vértice en mano y antebrazo | 3 / 2 | **1 / 1**, peso 1,0 |
| Deformación de piezas rígidas | 5,9 mm | **0,0001 mm** |
| N-gons, non-manifold, duplicados, área cero | tapas de 48–70 lados | **0** |
| Espejo de la mano izquierda | 15,3 mm de diferencia | **0,0 mm** |
| Interpenetración en Open / Relaxed / Inspect / Fist / Grip | — | 0,32 / 0,32 / 0,32 / 0,83 / 0,73 mm |
| Sockets | 18 vacíos, triplicados | **6 únicos**, en el marco IR-ACOPLE-01 |

**Puño validado:** índice 90/100/70° con la punta a 1,8 mm de la palma, y pulgar cruzado sobre la falange 1 del índice. **No se alcanzó:** medio y anular quedan en PIP 60–65°, porque su falange 1 es larga; llevarlos a PIP 95° exigiría acortarla o hacer una cavidad palmar. El pulgar tampoco llega a apoyarse en las falanges 2 sin añadir un hueso.

## 4. Exportación y Unity

**Exportación** en `Tools/IronRequiem/Exports/IR_Protagonist_Refined_v01/`:
- `IR_Protagonist_v02_Rigged.fbx` y `IR_Protagonist_v02_FPS_Rigged.fbx`, con los mismos nombres de objeto y de los 50 huesos que v01.
- Clips IR_v02_Open, Relaxed, Fist, Grip e Inspect, más IR_Idle e IR_Walk.
- Las texturas de v01, sin repintar.
- `metrics.json`, los renders y un README.

**Unity:** `Assets/IronRequiem/Editor/ProtagonistV02Authoring.cs` (menú `Tools > Iron Requiem > Protagonist v02`, o la cola `protagonist-v02.txt`):
- **Arte:** `Art/Player/ProtagonistV02/Rigged/*.fbx`, con la importación de v01 (Generic, escala de archivo, sin bake).
- **Variantes:** `Art/Player/ProtagonistV02/Protagonist_FPS_v02.prefab` y `Protagonist_Body_v02.prefab` son variantes de los prefabs v01 con las mallas v02 (4 y 15 renderers) y el bind de v02 (50 huesos, error 0). Los duplicados `.001`/`.002` quedan desactivados.
- **Poses:** `Art/Player/HandPoses/V02/FPS_L.asset` y `FPS_R.asset`, horneados muestreando los clips exportados sobre el rig importado: 15 huesos cada una, con palma y dirección espejadas. Las bibliotecas de v01 no cambian.
- **Jugador:** `Prefabs/Player/Player_ModularArm_ProtagonistV02.prefab`, variante de v07 con v02 en el rig FPS y en el cuerpo, más un `HandPoseController` con las bibliotecas V02.
- **Laboratorio:** `Scenes/ProtagonistV02Lab.unity`, copia del de armas con ese jugador.

**v01 no se reemplaza** y ninguna escena ni prefab existente cambió.

## 5. Verificación

- **Blender:** métricas de cada iteración y reimportación del FBX (tris, huesos por vértice, nombres, clips y sockets). Los 28 hashes protegidos coinciden, y la fuente del T-07 conserva `A0E48EAF…`.
- **Unity, importación y compilación:** `Tools/IronRequiem/Reports/ProtagonistV02/Unity/verify.json`.
- **PlayMode** (`Tests/PlayMode/ProtagonistV02Tests.cs`, entrada virtual): **aprobada**.
  - Mallas v02 con un solo hueso por vértice y `HandPoseController` presente.
  - Sin arma, la mano derecha queda en Relaxed (el diseño actual solo cierra el puño al golpear).
  - Golpe izquierdo con F: la mano aparece, se cierra (≥ 0,9) y se oculta.
  - Jab derecho con M1: cierre ≥ 0,9.
  - Capturas en `Tools/IronRequiem/Reports/ProtagonistV02/Play/`.
- **Revisión independiente** en `Tools/IronRequiem/Reports/IR09_V02_Review/review.md`: aprobado con reservas, no apto aún para campaña.

## 6. Pendientes

1. **Prueba manual en Play:**
   - Vista FPS con movimiento, F y M1 y puño cargado.
   - Cinemática del hangar: usa CapsuleRobotRig y no se tocó; sus hashes coinciden.
   - Recogida y lanzamiento del cañón.
   - Intercambio mano → arma en la estación: la del laboratorio se corrigió para entregar el arma nueva.
2. ~~Receptor de v01 en el jugador v07~~: resuelto en la iteración 4 (ver §7).
3. **Puño:** medio y anular por debajo de PIP 95°. Además, la falange 3 del medio queda hiperextendida −7°.
4. ~~Costuras de codo, palma y muñeca~~: resueltas en la iteración 4 (ver §7). Quedan las vetas de la textura v01 en la palma, que vienen de su baja densidad y no de estiramiento.
5. **Texturas:** densidad baja en las manos. Se propone reempaquetar el atlas o usar una textura FPS propia, sin repintar.
6. **Mantenimiento:**
   - Borrar a mano los `Assets/InitTestScene*.unity` que dejaron ejecuciones interrumpidas del Test Runner (uno de otra sesión, del 16/11 a las 13:58).
   - Borrar la carpeta vacía `C:\renders`.
7. **Integración en campaña:** solo tras aprobar v02.

## 7. Iteración 4 (17/11/2026): limpieza y receptor

Respaldo previo en `refined/iter3/` y `Exports/IR_Protagonist_Refined_v01/iter3/`. Módulo nuevo: `refine_protagonist_v02_cleanup.py`.

| Punto | iter3 | iter4 |
|---|---|---|
| Codo: islas o láminas < 1,5 mm, y caras coplanares o solapadas | 1 isla, 2 láminas, 39 triángulos solapados | **0**; queda una ranura continua de 1,5 mm entre tapas cerradas |
| Palma: astillas < 1 mm y caras con UV atípica (LOD0) | 3 astillas y 53 caras (anisotropía 368) | **0**; 59 caras re-proyectadas sobre la placa clara |
| Espejo L/R en LOD1/LOD2 | 1.222/1.222 y 752/716 tris (desviación 89,8 mm) | **idéntico** en todas las LOD (0,0001 mm) |
| Espiga de la muñeca | cilindro liso | chaflán de raíz y ranura en V de grafito; **medidas del estándar sin cambios** |
| Tris FPS / LOD0 | 8.768 / 23.480 | **8.964 / 23.690** (≤ 9.000 / 26.000) |

Interpenetración por pose, huesos por vértice, nombres, clips y sockets: sin cambios.

**Receptor v02:** `ProtagonistV02Authoring.BuildReceiver` aplica al antebrazo v02 el mismo corte que v07, los 0,14 m junto al codo (228 de 604 tris), y crea `Art/Player/ProtagonistV02/Protagonist_RightForearm_Receiver_v02.asset` en `Player_ModularArm_ProtagonistV02`. Con arma en mano se ve el muñón del antebrazo nuevo.

**Unity:** reconstrucción OK a las 02:23. La estación de ambos laboratorios entrega ahora `Weapon_IR_ArmCannon_Redesign_v01`. PlayMode **3/3** a las 02:25. Hashes protegidos: OK.
