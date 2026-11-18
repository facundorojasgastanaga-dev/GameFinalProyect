# Revisión final IR-09 v02 (protagonista refinado + rediseño de armas)

Fecha: 17/11/2026, ~01:10. Revisión **solo de lectura**: no se abrió Unity ni Blender (tampoco hizo falta Blender en background), no se modificó ningún archivo del proyecto salvo este informe. Evidencia: métricas JSON, `verify.json`, `play.xml`, capturas, diff de scripts contra el respaldo, `git status` limitado a `Assets/` y `sha256sum -c`.

**Aviso de estado:** a las 01:08:37 apareció `Tools/IronRequiem/Reports/WeaponsRedesign/Unity/open-scenes.txt` y un nuevo `Assets/InitTestScenedd95cd1f-….unity`. Hay una **ejecución de PlayMode en curso**. Lo que se dice aquí sobre tests corresponde al `play.xml` de las 00:33.

## Veredicto

**Aprobado con reservas. No está listo para integrarse en campaña.** La geometría y el estándar IR-ACOPLE-01 están bien medidos y entran en presupuesto. Las cifras de Blender, export y Unity coinciden en el protagonista. Los activos protegidos están intactos (28/28 OK) y no se tocó ningún prefab ni escena existente. Quedan cuatro reservas:

1. El "3/3 aprobadas" de `ProtagonistV02Tests` ejecutó la versión **anterior** del test, sin las aserciones de cierre ≥ 0,9.
2. En reposo desarmado, la mano derecha v02 sale **abierta**.
3. En el jugador v02, el antebrazo armado sigue siendo el receptor v01/v07, así que el acople no se ve en la vista FPS armada.
4. La estación de montaje del laboratorio entrega el arma **antigua**.

## 1. Métricas antes/después

### Protagonista (tris)

| Conjunto | v01 | v02 Blender (`refined/metrics.json`) | v02 export reimportado | Unity (`ProtagonistV02/Unity/verify.json`) | Límite | Estado |
|---|---|---|---|---|---|---|
| LOD0 | 21.108 | 23.480 | 23.480 | 14.712 + 2×496 + 2×3.888 = 23.480 | ≤ 26.000 | OK (margen 2.520) |
| LOD1 | 11.628 | 11.954 | 11.954 | 9.198 + 2×156 + 2×1.222 = 11.954 | ~12.000 | OK |
| LOD2 | 6.122 | 6.490 | 6.490 | 4.850 + 2×86 + 752 + 716 = 6.490 | ~6.500 | OK |
| FPS | 6.070 | 8.768 | 8.768 | 2×496 + 2×3.888 = 8.768 | ≤ 9.000 | OK (margen **232**, justo) |

Por pieza:
- Hand_R LOD0: 3.888 = Palm 718 + 15 falanges (142–254) + Collar 168. Supera el objetivo interno de 3.300 (`hand_R_lod0_budget.target`).
- Forearm_R LOD0: 496 = 300 + FlangeSpigot 196.
- Body LOD0 baja de 15.038 a 14.712, por el corte del codo.

Huesos por vértice:
- Antebrazos y manos, en todos los LOD y en FPS: **1** (antes 2/3), en Blender y en Unity.
- Body LOD0–2: **2**, sin cambios. El cuerpo sigue sin ser rígido (hallazgo 10 de la auditoría, abierto).

Materiales: los mismos de v01 (`Material.001–.003` en el Rigged, `.004` en FPS). El hallazgo 14 de la auditoría, un material por LOD, sigue sin resolver.

Atlas y texel: sin cambios, ~760–833 px/m. La auditoría calculó que caben manos ×2 (`atlas.x2_fits_in_free_area = true`), pero no se aplicó.

Discrepancias entre fuentes:
- **Hand_L_LOD2 = 752 frente a Hand_R_LOD2 = 716** (en Blender, el export y Unity), pese a `hand_L_mirror_max_dev_mm = 0.0`. El espejo exacto solo está comprobado en LOD0/FPS.
- `PROTAGONIST_V02_REFINE.md` §3–§5 sigue en "En curso / Pendiente": el documento no recoge estos resultados.

### Armas (tris LOD0/1/2)

| Arma | Presupuesto LOD0 | Blender (`metrics.tris`) | Blender reimport (`verification.lods`) | Unity (`verify.json` lodTris) | Materiales | Texel |
|---|---|---|---|---|---|---|
| P-A PistolMonolito | 1.300 | 1.256 / 628 / 314 | 1.256 / 628 / 314 | 1.256 / 628 / 314 | 2 (Surface, Heat) | 1.350 px/m |
| PR-C PrimaryEspina | 1.500 | 1.460 / 730 / 364 | 1.460 / 730 / 364 | 1.460 / 730 / 364 | 2 | 1.350 px/m |
| C-A T07CannonCrudo | 2.600 | 2.420 / 1.210 / 604 | **2.420 / 1.201 / 602** | 2.420 / 1.210 / 604 | 3 (Surface, Heat, Charge) | **737 px/m** (objetivo 1.000–1.400) |

- **Discrepancia T-07:** el reimport de Blender cuenta 9 y 2 triángulos menos en LOD1 y LOD2 que la cabecera y Unity. Probablemente son triángulos degenerados que el importador de Blender descarta. Hay que comprobar si existen caras de área cero en LOD1/2.
- En las 3 armas, los 12 checks del estándar pasan, pero solo en LOD0: `socket_*` y `collar_*` miden el collar y el alojamiento sobre LOD0. LOD1/2 solo garantizan `max_y_all_lods = 0`, es decir, nada por detrás del Attachment. **IR-ACOPLE en LOD1/2 está sin verificar** (Ø 0,066 y profundidad ≥ 6 mm).
- Unity: escala visual 1, error de acople y de boca de 0 mm en pistola y principal, eje a 0°. En el cañón, `attachErr_m = -1` y `muzzleErr_m = -1` significan **no medido** por `Verify()`. El asiento sí se mide en el test de Play: 0,00 mm (`Play/cannon.txt`).

## 2. Costuras en vista FPS (capturas)

| Captura | Observación | Severidad |
|---|---|---|
| `refined/renders/wrist_R_closeup.png` | Entre la brida del antebrazo y el collar de la mano queda un **cuello visible de 12 mm**: la espiga Ø 64 entre la brida Ø 78 y el collar Ø 80 (`wrist_R.axial_gap_collar_to_flange_mm = 12`). Es intencionado por el estándar (espiga de −12 a +3 mm) y se lee como un hueco. Además, la brida tiene muescas por las que asoma la carcasa clara del antebrazo. Sin z-fighting. | Media (diseño) |
| `refined/renders/elbow_R_closeup.png` | En el corte superior del codo aparecen **láminas sueltas o solapes** finos, en la zona x≈570–640, y≈185. La rótula está entera. | Media |
| `hand_R_fist_front.png`, `hand_R_fist_palm34.png` | Las caras de la palma, remapeadas al "metal region", muestran la **textura muy estirada** en vetas. Hay una **espiga o astilla fina** entre palma y nudillo (x≈880,y≈380 y x≈665,y≈480). Los tambores de las bisagras solapan ligeramente las placas: el máximo es 0,83 mm, dentro del límite, pero se ve. | Media |
| `fps_overforearm_R_{Fist,Grip}.png` | Desde la cámara FPS se ve una costura limpia entre el collar y el manguito, sin huecos visibles. | OK |
| `WeaponsRedesign/Play/pistol-joint.png` | La transición del manguito al collar de la pistola está limpia. No hay huecos visibles. | OK |
| `WeaponsRedesign/Play/primary-joint.png` | Asoma una **franja emisiva amarilla** del primer borde de aleta pegada al borde del manguito. Las 9 aletas están siempre encendidas, porque URP/Lit ignora `HeatStep` y la principal no tiene OverheatBehavior. | Baja |
| `WeaponsRedesign/Play/cannon-joint.png` | Entre el manguito y el collar octogonal del cañón queda un **hueco radial**: se ve el interior rojo del alojamiento a las 12 h. | Baja-media |
| `ProtagonistV02/Play/v02-right-fist-closeup.png`, `v02-left-fist-closeup.png` | El collar y la brida en Unity no muestran huecos ni z-fighting. El puño izquierdo cierra. En el derecho, los dedos aparecen rectos, sin cerrar. | Ver hallazgo 2 |
| `*-rest.png` (todas) | El letrero "ESTACIÓN DE MONTAJE : C-04" sale **espejado** en la escena de laboratorio. No es de este trabajo, pero se ve en todas las capturas. La PR-C queda casi fuera de pantalla, abajo a la derecha (ya documentado). | Baja |

Las capturas FPS armadas (`v02-armed-rest.png` es igual a `pistol-rest.png`) **no muestran el antebrazo v02**: el antebrazo armado es el receptor v07. Ver hallazgo 3.

## 3. Regresiones posibles (lectura de código y referencias)

- **StolenShellFit / StolenChargeAnchor**, comparados con `Backups/20261116-protagonist-v02/scripts/`. El diff solo añade `rearSocket` + `socketOverlap` (con su rama `if (rearSocket) {…; return;}` en `Measure()`) y `mouthSocket` (con su rama `if (mouthSocket) {…; return;}`).
  - Con el campo vacío, el flujo es idéntico byte a byte al original. En prefabs existentes el campo deserializa a null, así que **no hay cambio de comportamiento**.
  - Con socket, `overlap` se sobrescribe con 0,003. Coincide con el contrato: puerto = Attachment + forward × 0,003, comprobado por el test (0,00 mm).
  - Limitación previa que sigue igual: `Measure()` solo se ejecuta en `Awake`. Si se usa `Configure()` en runtime, no se vuelve a medir.
- **Collider de StolenCannon y pickup del mundo:** sin cambios. El módulo del mundo sigue con mallas y collider del T-07. El test se salta el rayo de recogida: coloca el módulo y desactiva el jugador, y si el candidato no coincide solo escribe un `LogWarning`.
- **Cinemática del hangar:** v02 no la toca. `CapsuleRobot(Rig).fbx/.meta`, `Cinematic_L/R.asset` y `CapsuleUpperArm_*` están en la lista protegida con OK. `WakeUpRobotPose` usa su propio `HandPoseController` con las librerías Cinematic. El jugador v02 solo lo referencia `ProtagonistV02Lab.unity`.
- **Melee y puño cargado:** `ProtagonistMelee` y `FistChargePresentation` (modificados el 15/11, fuera de esta tarea) anclan a `hands.RightHand`, `LeftHandBone` y los antebrazos. v02 no mueve las cabezas de `Hand_*` ni de `Forearm_*`, solo las de dedos y pulgar, así que el anclaje no cambia. `RightFistClosure` y `LeftFistClosure` pasan a leer `HandPoseController` (librerías v02). Por eso importa el hallazgo 2.
- **WeaponMountStation:** el proxy de mano se hornea de `hands.RightHandRenderers` con `BakeMesh`, así que la mano v02 se verá en la pose actual (abierta, según el hallazgo 2). El proxy del arma busca el **primer `Attachment`**. En las variantes de pistola y principal es el legacy, con error 0 respecto al nuevo módulo; el `Attachment` del módulo se renombra a `ModuleAttachment`. Problema: en `WeaponsRedesignLab`, `BuildLab()` instancia `MountStation_C04` **después** del remapeo de referencias. Su `weaponPrefab` sigue apuntando a `Weapon_IR_ArmCannon_VO` (guid 65ecd48e…), no a la variante rediseñada, así que la prueba de estación del laboratorio entrega el arma antigua. Las dos sobrescrituras de `weaponPrefab` que hay en la escena son de los T-07 destructibles (`T07_Common_Destructible_VO`), que sí apuntan a `Weapon_T07_Redesign_v01`.
- **`ProtagonistV02Authoring.ApplyV02`:**
  - Desactiva **cualquier** transform cuyo nombre termine en `.001` o `.002` bajo la raíz del jugador. Hay 12 `m_IsActive: 0` en `Player_ModularArm_ProtagonistV02.prefab`, que coinciden con los 12 vacíos duplicados, pero la regla por nombre es frágil.
  - El ancla de los sockets (`model.transform.GetChild(0)`) depende del orden de hijos del FBX.
  - Los vacíos v02 tienen otro roll respecto a v01: la X del socket cambia de (0,643, −0,766, 0) a (0,093, −0,971, 0,222), unos 37°. Con arma activa da igual, porque `ProtagonistHandsPresentation` reescribe el socket con la rotación de la boca cada frame. Sin arma, el socket vuelve a la pose de bind capturada en `Awake`.
- **Dependencia no versionada:** el jugador v02 usa `HandPoseController`, `ConfigureHandPoses`, `LeftHandBone` y `RightFistClosure`. Son cambios **sin commit** de `ProtagonistHandsPresentation.cs` (16/11 12:26) y de archivos sin seguimiento (`HandPoseAuthoring.cs`, `Art/Player/HandPoses/`), anteriores a IR-09. Si se revierten, se rompe v02.
- **`WeaponsRedesignAuthoring.cs`** se modificó a las 00:26:59, después de su último build/verify (`verify.json` de las 23:55). La versión en disco no regeneró los activos. `Unity/status.txt` sigue con el `ERROR test` (InitTestScene sucia) de las 00:19, aunque los tests posteriores pasaron.

## 4. Cambios en Assets existentes (`git status --short -- Assets/`)

Hay 41 archivos rastreados modificados. Solo **2 son atribuibles a IR-09**: `Scripts/VisualPass/StolenShellFit.cs` y `StolenChargeAnchor.cs`, ambos del 16/11 a las 20:45, con respaldo y solo con los campos opcionales del diff anterior.

El resto es de otros trabajos del usuario, por fecha y contenido:
- **13–15/11:** escenas de campaña, NavMesh/Reflection, IntroMenu, menús, `HangarIntroCinematic`, `WakeUpRobotPose`, `ProtagonistMelee`, `FistChargePresentation`, `CampaignFlow`, `Default_PipelineAsset`, etc.
- **16/11 12:26–14:25:** `ProtagonistHandsPresentation`, `WeaponDeployMotion`, `WeaponMountStation`, `FistChargeHud`, `VisualOverhaulDriver` y `WeaponHeatHud` (trabajo de manos, HUD y estación). El TMP `LiberationSans SDF - Fallback.asset` es de las 13:27 (atlas dinámico).
- **16/11 19:10–19:37:** `IndustrialPanelGraphic`, `HangarButtonFx`, `OptionsSwitchInstaller` y `CctvActorFitter` (UI y menú; el trabajo de v02 arranca con el respaldo de las 19:15).

Otros comprobantes:
- `Assets/FPS/Tutorials/.../13-Spawn-Room.asset` y `015.asset` tienen fecha de las 00:31:53, pero git no ve cambios de contenido (reserialización del Editor).
- Prefabs y escenas base sin cambios en contenido ni fecha: `Protagonist_FPS/Body.prefab` (31/10), `Player_ModularArm_v07.prefab` (04/11), `Weapon_IR_ArmCannon_VO`, `Weapon_Shotgun_ArmModule_VO` y `Weapon_T07_Temporary_Optimized` (11/11), `MountStation_C04` (13/11), `MainScene_VisualLab` (08/11) y `MainScene.unity` (06/11). Todos limpios en git.
- Las variantes nuevas (`Prefabs/Weapons/Redesign_v01/*`, el jugador v02 y los `*_Visual.prefab`) solo las referencian `WeaponsRedesignLab.unity` y `ProtagonistV02Lab.unity`. La campaña no se toca.
- `sha256sum -c Tools/IronRequiem/Backups/20261116-protagonist-v02/protected-sha256.txt`: **28/28 OK**.

## 5. Hallazgos principales

| # | Sev. | Hallazgo | Evidencia |
|---|---|---|---|
| 1 | **Alta** | El 3/3 de las 00:32 no valida el cierre de puño: corrió la versión anterior de `ProtagonistV02Tests`. | `ProtagonistV02Tests.cs` guardado a las 00:31, con capturas `v02-unarmed-relaxed` y `v02-right-jab` y aserciones `leftClosure/rightClosure > .9`. Lo que hay en `ProtagonistV02/Play/` (00:32) es `v02-unarmed-fist.png`, sin `v02-right-jab`, y `v02-hands.txt` con el texto antiguo. Hay otra ejecución en curso (01:08). |
| 2 | **Alta** | La mano derecha v02 queda abierta en reposo desarmado. El código documenta que el reposo es puño cerrado (HandPose 0, NormalFist). | `Play/v02-unarmed-fist.png` muestra los dedos extendidos y separados. El comentario está en `ProtagonistHandsPresentation` ("right hand is a closed fist (NormalFist, HandPose 0) at rest"). Puede ser un cambio intencional a "relaxed", pero no está documentado. |
| 3 | Media | El jugador v02 conserva `Forearm_R_Receiver` v01/v07 (mesh `Protagonist_RightForearm_Receiver.asset`). Con arma, el acople IR-ACOPLE de v02 no se ve. | `ProtagonistV02/Unity/verify.json` → `fpsMeshes`; `v02-armed-rest.png` es igual a `pistol-rest.png`. |
| 4 | Media | La estación del laboratorio entrega la pistola VO antigua, porque `MountStation_C04` se instancia después del remapeo. | `WeaponsRedesignAuthoring.BuildLab()` (líneas 239–268); `MountStation_C04.prefab: weaponPrefab guid 65ecd48e…` = `Weapon_IR_ArmCannon_VO`. |
| 5 | Media | Costuras visibles: cuello de 12 mm en la muñeca, láminas en el corte del codo, textura estirada y astillas en la palma, hueco radial en el collar del cañón. | §2 |
| 6 | Media | El puño no es completo: Middle PIP 60°/DIP 20° (Middle3_R −7,2° local, hiperextendido), punta a palma 9,25 mm; Ring PIP 65°; penetración del pulgar 0,83 mm. | `Exports/IR_Protagonist_Refined_v01/metrics.json → validated_poses` |
| 7 | Media | IR-ACOPLE sin verificar en LOD1/2 de las armas. Discrepancia de tris en T-07 (1.210/604 frente a 1.201/602). Asimetría en Hand_L/R_LOD2 (752/716). | `*_Redesign_v01/metrics.json`; `verify.json` |
| 8 | Baja | Pendientes de las armas: texel del cañón (737 px/m), PR-C sin calor por aleta, `Charge_Emissive` sin animar. Sockets renombrados a `ModuleAttachment`/`ModuleMuzzle`, que no es el nombre `Attachment` del estándar (`T07_Cannon_PivotFixed_Visual` sigue con `WeaponAttachment`). Hay 4 `Assets/InitTestScene*.unity` sobrantes y `status.txt` de armas desactualizado. | `WEAPONS_REDESIGN_V01.md` §Pendientes; `WeaponPivotAuthoring.cs:29`; `ls Assets/InitTestScene*` |

## 6. Pendientes priorizados

1. **Esperar o repetir** la ejecución PlayMode en curso con la versión actual de `ProtagonistV02Tests`. Confirmar `v02-right-jab.png`, `v02-unarmed-relaxed.png` y el cierre ≥ 0,9 en ambas manos. Hasta entonces, el cierre del puño no está demostrado.
2. **Decidir el reposo desarmado** (puño o relaxed) y alinear `ProtagonistHandsPresentation`, `HandPoseController` y el documento.
3. **Play manual**:
   - Vista FPS armada y desarmada.
   - Puñetazo izquierdo (F), jab derecho (M1) y puño cargado (energía y burst sobre la mano v02).
   - Estación hand→arma con la variante rediseñada. Antes hay que corregir el `weaponPrefab` de la estación del laboratorio o remapearla.
   - Cinemática del hangar (regresión cero esperada, por confirmar a la vista).
   - Recogida real del cañón con rayo y oclusión, y lanzamiento.
4. **Forearm_R_Receiver v01 en el jugador v07:** generar un receptor v02 con brida y espiga, o documentar que el acople armado sigue siendo v07.
5. **IR-ACOPLE en LOD1/2 de las armas:** medir el alojamiento (Ø 0,066 y profundidad ≥ 6 mm) y el collar en cada LOD. Revisar los triángulos degenerados del T-07 en LOD1/2.
6. **Puño:** dedos medio y anular sin PIP de ~95° (60° y 65°) y Middle3 hiperextendido. Rehacer `IR_v02_Fist` y regenerar `HandPoses/V02`.
7. **Costuras:** decidir si el cuello de 12 mm de la muñeca se queda (o adelantar la brida). Limpiar las láminas del codo y la astilla de la palma. Reasignar la UV de las caras nuevas de la palma (ahora estirada en la región metálica) y aplicar el ×2 de las manos en el atlas, que cabe.
8. **Texel del cañón:** usar `IR_TEX=2048` (≈1.350 px/m) o reempaquetar.
9. **ExposedMechanism_FPS.mat:** lo usa `WeaponPivotAuthoring` (T07_Cannon_PivotFixed_Visual). Decidir si se retira con el C-A, que no lo usa.
10. **Limpieza:**
    - `Assets/InitTestScene{c31c00f1,c5b73971,dd95cd1f,fc14b308}.unity` y sus `.meta` (borrar a mano cuando no haya tests en ejecución).
    - `Tools/IronRequiem/Reports/WeaponsRedesign/Unity/status.txt` desactualizado.
    - La carpeta vacía `C:\renders` de la auditoría.
11. **Documentación:** completar `PROTAGONIST_V02_REFINE.md` §3–§5 con estas métricas. Hacer commit, o al menos registrar, la dependencia de `HandPoseController` y de `ProtagonistHandsPresentation` sin commit.
12. Integración en campaña (`RequiemCampaign`) solo tras aprobar los puntos 1–4.
