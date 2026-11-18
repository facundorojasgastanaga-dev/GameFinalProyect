# Auditoría del protagonista — IRON REQUIEM (antes de V02)

Fecha: 16/11/2026. Trabajo **solo de lectura**: Blender 5.2.3 LTS en procesos nuevos `--background --factory-startup --python-exit-code 1`; ningún FBX/.blend/.meta/prefab/escena se guardó ni reimportó. Datos completos: `audit.json` (todo lo medido) y `data/*.json`. Scripts propios: `inventory.py`, `audit_mesh.py`, `audit_joints.py`, `compare_coords.py`, `render_before.py`, `summarize.py`.

Coordenadas en espacio Blender (Z arriba, frente del personaje hacia −Y, lado derecho del robot en −X). Conversión observada en Unity (`Forearm_R_Receiver`): Unity = (−x, z, −y).

## 1. Tabla de errores priorizada

| # | Sev. | Pieza | Evidencia | Propuesta |
|---|---|---|---|---|
| 1 | **Crítica** | `Hand_R/L` (LOD0, FPS y LOD1–2) | Cada mano es **una sola isla** (1.095 / 1.071 vértices LOD0) con palma y dedos fusionados. **789/1.095 vértices (72 %)** de `Hand_R_LOD0` tienen ≥2 huesos con peso >0,001; 274 tienen 3. Mezclas **entre dedos vecinos**: Index3+Middle3 33, Middle3+Ring3 28, Middle2+Ring2 21, Little3+Ring3 20, Index1+Middle1 14, Index2+Middle2 13. Con `IR_HandFist` los segmentos que deberían ser rígidos se doblan: residuo rígido (Kabsch) máx. **5,9 mm** (Little1_R), 5,7 mm en la palma (`Hand_R`), p95 hasta 4,0 mm. `Thumb1_R/L` nunca es hueso dominante (0 vértices) aunque influye en 106 vértices. | Separar la mano en piezas mecánicas (palma, 3 falanges × 4 dedos, pulgar) con **peso 1,0 a un solo hueso** por pieza; tapas en cada articulación; nada de mezclas entre dedos. Es el origen del puño deformado. |
| 2 | **Alta** | Corte de muñeca `Forearm_*`/`Hand_*` | Plano oblicuo y dentro del puño del antebrazo: `Hand_R_LOD0` llega a z = 1,0131 (15,6 mm por encima del pivote de muñeca z = 0,9974) y `Forearm_R` baja a z = 0,9876 (9,8 mm por debajo). Una lámina del manguito pertenece a la mano (render `wrist_R_closeup.png`). 38 vértices del antebrazo mezclan Forearm+Hand; con la pose de estrés (antebrazo 0,8 rad, mano 0,4 rad), los vértices del antebrazo dominados por `Hand_R` se deforman hasta 6,1 mm (p95 5,2 mm). El anillo compartido (49 vértices LOD0 R / 48 L) no se abre (máx. 0,0004 mm en las 14 poses), porque la deformación lo cierra. | Rehacer el corte perpendicular al eje del antebrazo, en el final del manguito, con un collar/rótula de muñeca como pieza propia. Pesos rígidos: el antebrazo, 100 % `Forearm`; la mano, 100 % `Hand`. |
| 3 | **Alta** | Tapas de corte (n-gons cóncavos) | Concavas: `Forearm_R_LOD0` tiene un 49-gono y un 70-gono; `Forearm_L_LOD0`, un 48-gono y un 65-gono; `Hand_R/L_LOD0`, un 49/48-gono; `Body_LOD0`, un 65-gono y un 70-gono en los codos. Se repiten en LOD1 (58/61) y LOD2 (36/40). En FPS se oculta la mano derecha con arma (IR-09), y queda expuesta la tapa cóncava del antebrazo; en `fps_*_overforearm_R.png` se ve la tapa plana del codo. Tapas coincidentes entre piezas: 3–104 pares de triángulos en contacto (Body↔Forearm) y 0–71 (Forearm↔Hand), cifra inestable porque son caras coplanares. | Sustituir las tapas por geometría modelada (collar, rótula) o triangularlas de forma controlada (`--triangulate`) con un relleno en abanico/rejilla. No dejar caras internas coplanares en las uniones. |
| 4 | **Alta** | Codo (Body/Forearm) | El corte del codo atraviesa la rótula: la mitad superior es de `Body`, la inferior de `Forearm` (`elbow_R_closeup.png`). Body tiene 70 vértices Forearm+UpperArm mezclados. | Rótula de codo como pieza rígida propia (hueso `Forearm` o un hueso de giro dedicado), con el corte a ambos lados de la esfera. |
| 5 | **Alta** | `IR_Protagonist_FPS_Rigged.fbx` | No contiene los vacíos `Wrist_*`, `ModuleSocket_*` ni `Elbow_*` (solo 4 mallas y `IR_Rig`). Lleva el esqueleto completo de 50 huesos, con 16 sin vértices (piernas, columna, cabeza, clavículas, brazos superiores). Los sockets se añaden después en el prefab (`ModuleSocket_R/L` en identidad bajo `rightHand`/`leftHand`). | En la exportación V02 incluir sockets FPS explícitos (`Wrist_R/L`, `ModuleSocket_R/L`) parentados al hueso y recortar el esqueleto FPS a Clavicle→dedos, o documentar la dependencia del prefab. |
| 6 | Media | `IR_Protagonist_Rigged.fbx` (sockets duplicados) | Vacíos triplicados con transformación idéntica: `Wrist_R`, `Wrist_R.001`, `Wrist_R.002` (ídem `_L`, `ModuleSocket_*`, `Elbow_*`), uno por LOD. Hay 12 vacíos redundantes; las búsquedas por nombre son ambiguas. | Un único socket por articulación en el rig compartido. |
| 7 | Media | Puño rígido Plan B (`closed_fist.blend`) | 23 cajas biseladas independientes (2.484 tris). **34 pares de piezas se interpenetran** (p. ej., Hinge_n↔Middle_n 28–32 pares, Knuckle_n↔Palm 18–22, Palm↔Wrist coupler 20). UV de cubo por defecto: cada pieza ocupa el 38–39 % del 0–1, con lo que **las 23 se solapan entre sí**. Densidad de 6.439 px/m (palma) a 28.886 px/m (bisagra), un rango de ×4,5. Estiramiento p95 de 10,9 en el panel naranja y 5,5 en la placa dorsal. `closed_fist.json` solo contiene vértices, triángulos y material, sin UV ni normales compartidas (sombreado plano). No está riggeado (no hay dedos ni huesos). | Si se conserva como fallback: UV reales en atlas con densidad uniforme y eliminar las caras ocultas en las intersecciones. Para V02 conviene un puño modular articulado con piezas rígidas. |
| 8 | Media | Densidad de texel (FPS) | LOD0 ≈ **760–833 px/m** con texturas de 2048 (manos 832,6; antebrazos 758–761). En FPS, con la mano a ~0,5 m, FOV vertical de 60° y 1440p, se ven ~2.494 px/m de pantalla: **magnificación ×3**. Las manos usan solo el 1,3 % del atlas cada una (cobertura total LOD0 ≈ 48 %). | Para la versión FPS, un atlas propio de brazo/mano (≥2.000–2.500 px/m) o una textura de 2048 dedicada a los brazos. |
| 9 | Media | Asimetría L/R | Las manos no son espejo: 1.071 frente a 1.095 vértices, distancia al vecino más cercano máx. 15,3 mm (p95 5,6 mm); antebrazos máx. 14,2 mm. | Modelar un lado y reflejarlo (ya se hizo así con el puño Plan B). |
| 10 | Media | Huesos sin vértices / pesos | Cuerpo completo: `Root`, `Neck`, `Clavicle_L`, `Clavicle_R` sin vértices. `Body_LOD0`: 2.831/7.505 vértices (37,7 %) con 2 huesos. Es un cuerpo de una sola isla de 7.505 vértices: las placas mecánicas se deforman de forma orgánica. | Si V02 es un robot por piezas: cuerpo segmentado en placas rígidas con un hueso cada una. |
| 11 | Baja | LOD1/LOD2 | `Hand_L_LOD1` tiene una isla suelta de 11 vértices centrada en (0,4512, −0,0049, 1,0075), **2,88 mm** separada de la mano y por encima del plano de muñeca. Solapes UV entre islas: LOD1 34 px (1 par), LOD2 108 px (6 pares) a 1024². La densidad de LOD2 en manos baja a 679–681 px/m. | Rehacer los LOD desde el LOD0 corregido y validar islas. |
| 12 | Baja | Intersecciones internas | `Body_LOD0`: 3 pares de autointersección (caras 6327/6338, 6324/6338, 6324/14155) y 1 cara encerrada (1593). `Hand_R_LOD0`: par 316/320 y caras encerradas 320 y 323. `Hand_R_LOD2`: 4 caras encerradas. 1 cara de área cero en `Body_LOD0`. | Limpiar durante la reconstrucción. |
| 13 | Baja | UV generales | ~50 % de las caras con winding UV invertido (islas espejadas: `Body_LOD0` 7.426/14.315; `Hand_L_LOD0` 1.078/2.067). Estiramiento `Body_LOD0`: p5 0,53, p95 1,47, máx. 33,4, con 863 caras fuera de ×2. Manos LOD0: p5 0,86–0,87, p95 1,23–1,24 (bien). Solapes LOD0 despreciables (37 px en la misma isla). | Revisar las costuras de tangentes del normal map en islas espejadas. Desplegar de nuevo las placas con estiramiento alto. |
| 14 | Baja | Materiales | El FBX riggeado tiene `Material.001/.002/.003` con firma idéntica (las mismas 4 imágenes), uno por LOD; el FPS usa `Material.004`. En Unity `materialImportMode: 0`, así que se usa `IR_Protagonist_URP.mat`. | Un solo material nombrado en la exportación V02. |
| 15 | Info | CapsuleRobot / CapsuleRobotRig | `CapsuleRobot.fbx`: **741.923 tris**, una malla sin rig, 7 aristas abiertas, 2 fragmentos sueltos (3 y 4 vértices en (0,2152, −0,0199, 0,5104) y (−0,0336, 0,0061, 0,632)), 4.129 caras UV de área cero, estiramiento p5 0,35 / p95 2,33 (27 % de las caras fuera de ×2). `CapsuleRobotRig.fbx`: 18.255 tris, 65 huesos Mixamo (13 huesos terminales sin vértices, normal), hasta 4 influencias (1.440 vértices), 5 aristas abiertas, 4 aristas no-manifold, 6 vértices no-manifold, 2 caras invertidas. **Ambos miden ~1,00 m de alto frente a 1,80 m de ProtagonistV01** (factor 1,80). | No usarlos como fuente de geometría final sin reescalar y limpiar. CapsuleRobot no es apto para tiempo real. |

Sin hallazgos (0 en todas las mallas de ProtagonistV01, LOD0–2, FPS y Rigged): normales invertidas frente al exterior (recalc), aristas con winding inconsistente, caras duplicadas (por índices o posiciones), vértices sueltos, aristas no-manifold o de borde, caras interiores bmesh y escalas distintas de 1 (todas las escalas de objeto y armature = 1). Las mallas son cerradas, con volumen firmado positivo.

## 2. Diferencias LOD0/1/2 y FPS frente a cuerpo completo

| | Body | Forearm L/R | Hand L/R | Total | Bounds del cuerpo (m) |
|---|---|---|---|---|---|
| LOD0 | 15.038 | 858 / 872 | 2.142 / 2.198 | **21.108** | 0,7191 × 0,3967 × 1,8000 |
| LOD1 | 9.426 | 542 / 570 | 528 / 562 | **11.628** (55 %) | 0,7187 × 0,3967 × 1,7994 |
| LOD2 | 4.962 | 310 / 320 | 252 / 278 | **6.122** (29 %) | 0,7163 × 0,3958 × 1,7980 |
| FPS | — | 858 / 872 | 2.142 / 2.198 | **6.070** | — |

- Hay 5 piezas por LOD, con los mismos nombres y sufijo `_LODn`. Todos comparten el mismo rig de 50 huesos (los nombres coinciden) y las mismas pertenencias de grupo: Body 14 grupos, Forearm 2 y Hand 16. El anillo compartido de muñeca L/R tiene 48/49 vértices en LOD0, 26/23 en LOD1 y 17/18 en LOD2; el del codo, 65/70, 58/61 y 36/40.
- La geometría es idéntica en Source, Rigged FBX, `IR_Protagonist_Rigged.blend`, FPS_Parts y FPS_Rigged: diferencia máxima **0,00028 mm**. La versión FPS es el LOD0 de antebrazo/mano sin cambios, con la misma escala (1), los mismos ejes (Z arriba, −Y frente) y los mismos huesos de mano y muñeca. Diferencias: material (`.004`), sin vacíos de socket, y Body ausente.
- Importación en Unity (los 6 `.meta`): `globalScale 1`, `useFileScale 1`, `bakeAxisConversion 0`, `maxBonesPerVertex 4`, `weldVertices 1`. Los riggeados usan `animationType 2` (Generic) con `avatarSetup 0` (sin avatar).
- Acciones (en FBX: `IR_Rig|IR_Rig|…`): `IR_HandFist`, `IR_HandGrip`, `IR_HandNormal`, `IR_Idle`, `IR_PunchPosePreview` (solo cierra dedos, el brazo no se mueve) e `IR_Walk`.

## 3. Nombres reales de huesos y sockets (muñeca y mano)

**Huesos IR_Rig (ProtagonistV01, idénticos en Rigged y FPS_Rigged).** Cadena: `Root → Pelvis → Spine → Chest → Clavicle_R → UpperArm_R → Forearm_R → Hand_R → {Index1-3_R, Middle1-3_R, Ring1-3_R, Little1-3_R, Thumb1-3_R}`, con su espejo `_L`.

| Hueso | Head (m) | Tail (m) | Long. | Eje Y del hueso (dirección) | Eje Z (roll) |
|---|---|---|---|---|---|
| `Forearm_R` | (−0,34085, 0,06648, 1,25467) | (−0,41976, 0,0002, 0,99742) | 0,2771 | (−0,2847, −0,2392, −0,9283) | (−0,0701, 0,971, −0,2287) |
| `Hand_R` | (−0,41976, 0,0002, 0,99742) | (−0,45091, 0,0, 0,92321) | 0,0805 | (−0,387, −0,0025, −0,9221) | (−0,0009, 1,0, −0,0023) |
| `Forearm_L` / `Hand_L` | espejo en X | | 0,2771 / 0,0805 | (0,2847, −0,2392, −0,9283) / (0,3862, −0,0025, −0,9224) | |

`Hand_R` forma **14,8°** con el eje del antebrazo.

**Vacíos en los FBX** (Source LOD0–2, FPS_Parts y Rigged; **no** en FPS_Rigged):

| Socket | Padre | Posición (m) | Eje X | Eje Y | Eje Z | Euler mundo (°) |
|---|---|---|---|---|---|---|
| `Wrist_R` = `ModuleSocket_R` | hueso `Hand_R` (Rigged) o `IR_Protagonist_Root_LODn` (Source) | (−0,41976, 0,0002, 0,99742) | (0,6432, −0,7657, 0) | (−0,7108, −0,5971, 0,3719) | (−0,2847, −0,2392, −0,9283) | (158,17, 0, −49,97) |
| `Wrist_L` = `ModuleSocket_L` | `Hand_L` | (0,41984, 0,0002, 0,99742) | (0,6432, 0,7657, 0) | (0,7108, −0,5971, 0,3719) | (0,2847, −0,2392, −0,9283) | (158,17, 0, 49,97) |
| `Elbow_R` / `Elbow_L` | `Forearm_R` / `Forearm_L` | (∓0,34085, 0,06648, 1,25467) | igual que la muñeca | | | |

El +Z de los sockets apunta del codo a la mano (eje del antebrazo, no el de `Hand_R`). En Unity, `Wrist_R` ≈ (0,41976, 0,99742, −0,0002). Además hay copias `.001`/`.002` de cada vacío en el Rigged.

**Sockets en prefabs de Unity (leídos del YAML):** `ModuleSocket_R`/`ModuleSocket_L` (Player_IR_Protagonist_v01, hijos en identidad de `rightHand`/`leftHand`); `RightArm_ModuleSocket` (Player_ModularArm_v07, local (0, 0,14, 0), rot (−0,6354, −0,3103, −0,3103, 0,6354)); `Forearm_R_Receiver` (Player_ModularArm_v07, local (0,34085, 1,25467, −0,06648), es decir, el codo); `LeftPunchWrist`/`LeftPunchElbow` (Actions_v02, bajo la raíz FPS); `ForearmSocket_R/L` (prototipo `SurvivorRobot_Visual`, (∓0,49, 0,99, 0)). Offsets de `ProtagonistHandsPresentation`: pistolWrist (0, −0,015, −0,23), cannonWrist (0,015, −0,04, −0,38).

**CapsuleRobotRig (Mixamo):** `mixamorig:RightForeArm` head (−0,17, 0,033, 0,692), tail (−0,232, 0,024, 0,577); `mixamorig:RightHand` head (−0,232, 0,024, 0,577), tail (−0,256, 0,02, 0,516); `mixamorig:LeftHand` head (0,233, 0,01, 0,577), con dedos `RightHandIndex1-4`, `Middle1-4`, `Ring1-4`, `Pinky1-4` y `Thumb1-4`. No tiene sockets.

## 4. Medidas de muñeca (sección transversal, LOD0)

Cortes perpendiculares al eje `Forearm` por el pivote de muñeca. Envolvente convexa; Ø equivalente = 2·√(A/π).

| Pieza | Desplazamiento | Ø equiv. | Ø máx. | Ancho mín. | Extensión X/Z del hueso | Perímetro |
|---|---|---|---|---|---|---|
| Forearm_R | −60 mm | 69,0 mm | 77,1 | 64,7 | 66,5 / 76,0 | 223 mm |
| Forearm_R | −40 mm | 75,6 | 81,6 | 72,7 | 76,7 / 81,4 | 245 |
| Forearm_R | −20 mm | **75,7** | 83,1 | 71,5 | 73,5 / 83,0 | 244 |
| Forearm_R | −10 mm | 69,9 | 76,8 | 61,9 | 71,5 / 66,1 | 226 |
| Forearm_R | −5 mm | **61,8** | 71,9 | 55,0 | 65,5 / 56,4 | 201 |
| Hand_R | +5 mm | **47,6** | 50,7 | 45,5 | 46,4 / 50,4 | 151 |
| Hand_R | +10 mm | 47,9 | 53,1 | 43,6 | 44,6 / 52,3 | 153 |
| Forearm_L / Hand_L | −20 / +5 mm | 75,8 / 50,9 | 83,5 / 56,7 | 71,4 / 48,2 | | |

En resumen, el manguito del antebrazo tiene **~76 mm** de diámetro a 2 cm de la muñeca y se estrecha a ~62 mm en la tapa; el cuello de la mano mide **~48–51 mm**. El collar del puño Plan B (`Wrist coupler`) mide 76 × 31 × 61 mm y la palma 108 × 95 × 56 mm.

## 5. Renders "antes" (`renders/`, Workbench, cavidad y contorno, fondo neutro, 1200 px)

Colores por pieza: cuerpo gris, antebrazo azul, mano naranja. Cámaras en `renders/render_log_*.json`.

- **Cuerpo LOD0 (orto):** `body_front`, `body_profile_right`, `body_profile_left`, `body_back`, `body_three_quarter`; además `body_three_quarter_LOD1`, `_LOD2` y `body_punch_preview_pose`.
- **Manos:** `hand_R_front`, `hand_R_outer_side`, `hand_R_inner_side` y lo mismo para `_L`. Pose de puño (`IR_HandFist`): `hand_R_fist_pose_front`, `hand_R_fist_pose_34` y su `_L`.
- **Muñecas:** `wrist_R_closeup`, `wrist_R_closeup_under` y su `_L`. **Codos:** `elbow_R_closeup`, `elbow_R_closeup_back` y su `_L`.
- **FPS (aproximadas):** `fps_{bind,IR_HandGrip,IR_HandFist}_eye1.44_pitch50` usan la cámara a 1,44 m de la plantilla (`Player.prefab`, CameraHeightRatio 0,8 × 1,8), inclinada 50°. `fps_*_overforearm_R/L` usan una cámara de ~0,55 m detrás y encima de cada muñeca. `fps_parts_overview_front` también existe. **No se pudo reproducir la cámara real:** la raíz FPS cuelga de un Transform anidado sin nombre legible en el YAML (fileID 4335993182941230083) y la IK de antebrazos se aplica en runtime.
- **Puño Plan B:** `rigid_fist_34`, `_top_palm_side`, `_dorsal`, `_knuckles`, `_thumb_side`.
- **Referencias:** `capsule_rig_{front,three_quarter,profile_right,back,hand_R_front,hand_L_front,elbow_R,elbow_L}` y `capsule_{front,three_quarter,profile_right,back}`.
- Conceptos de destino: `Docs/IronRequiem/Concepts/Player/Protagonist_Multiview_v02/01–08` (son IA, no ortográficos calibrados). Muestran manos mecánicas con falanges separadas oscuras, frente al bloque fusionado actual.

## 6. Lo que no se pudo medir

- Autointersección BVH de `CapsuleRobot.fbx` (741.923 tris), omitida. Su prueba de caras encerradas se muestreó a 1/12 caras.
- La cámara FPS exacta de Unity y las poses runtime (`HandPoseController`, IK, `FirstPersonUpperArms`, copias horneadas). Las variantes `HandPoses/*.asset` no se inspeccionaron.
- Efectos de la importación en Unity (`weldVertices`, tangentes, compresión): todo se midió en Blender.
- Diámetro de muñeca del CapsuleRobotRig: no se seccionó.
- Comparación cuantitativa con los conceptos: solo es visual, porque no son vistas calibradas.

Incidencia: en la primera tanda de renders se pasó una ruta relativa y Blender escribió 26 PNG y un JSON en `C:\renders`. Se movieron a `renders/`; la carpeta `C:\renders` quedó **vacía** porque la eliminación fue bloqueada por una comprobación de seguridad. Puede borrarse a mano.
