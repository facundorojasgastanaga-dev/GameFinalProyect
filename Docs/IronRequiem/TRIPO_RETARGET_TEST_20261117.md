# Prueba de animación de un robot: Tripo vía Composio y animación propia (17/11/2026)

Robot: **T-07 Mech**, fuente `Assets/IronRequiem/Art/Enemies/T07Mech/T07Mech_Rigged.fbx`, prefab de prueba `T07_Mech_Test`. Todo vive en una variante y en escenas de laboratorio; el juego no cambia.

## Resumen y recomendación

| Vía | Resultado | Recomendación |
|---|---|---|
| Tripo `RETARGET_ANIMATION` por Composio | Bloqueada: la comprobación de rig de Tripo devolvió `riggable: false`, el saldo de la API es 0 y el rig hecho en Tripo Studio no es visible para la API. **Créditos gastados: 0.** | **Descartar** por API para este robot. Si se quiere Tripo, animar en Tripo Studio a mano (créditos del usuario) y exportar FBX con esqueleto Mixamo. |
| Clips propios con lenguaje de robot (R1–R6) | Funcionan en Play Mode, compilan sin errores y no hubo errores de consola. El revisor independiente ve una mejora clara en ataque, golpe y en distinguir patrulla de combate. | **Usar con limpieza** (ver Pendientes) para el T-07 Mech. Aplicar el mismo método a otros bípedos con rig (Carroñero) solo tras aprobar este. |

No se reemplazó ninguna animación del juego. La integración queda propuesta y pendiente de aprobación.

## 1. Tripo3D por Composio

- Composio CLI 0.6.2 instalada en `%USERPROFILE%\.composio`, con el instalador nativo de Windows (experimental) y su checksum verificado. Login hecho por el usuario. Toolkit `tripo3d` 20261026_00 con 23 herramientas; la conexión Tripo3D ya estaba ACTIVE.
- Esquemas reales:
  - `RETARGET_ANIMATION` exige `input` = UUID de una **tarea de rig de Tripo** terminada, más `animation` o `animations` (1–5), `out_format` glb/fbx, `animate_in_place` y `export_with_geometry`.
  - `RIG_MODEL` acepta `model` `v1.0-20240301` (solo bípedo, presets `preset:biped:*`) o `v2.5-20260210` (criaturas), `spec` tripo/mixamo y `rig_type`.
  - No hay herramienta de descarga: las URL salen de `GET_TASK` y caducan.
  - Precios públicos de Tripo: rig 25 créditos y retarget 10 por animación (1 crédito = 0,01 USD).
- Entrada: copia limpia en `Tools/IronRequiem/Generated/Tripo_T07Mech_20261117/input/` (README con el origen; script `Tools/IronRequiem/Blender/export_tripo_input.py`). Tiene una sola malla de 9.227 triángulos, la misma cifra que la fuente, girada 33,5° hacia el frente. El FBX fuente conservó su hash.
- Llamadas (registro en `.../tasks/LOG.md`):
  1. `UPLOAD_FILE` → token `file_53cb5d73-…`; Tripo creó la tarea implícita `import_model` `4fcd6d20-…` (0 créditos).
  2. `CHECK_MODEL_RIGGABILITY` → tarea `24315c55-fe7b-4fd2-a714-dbb2346bbfe0`: `riggable: false`, `rig_type: biped` (0 créditos).
- Me detuve según la regla de no reintentar. El usuario mostró el modelo ya riggeado en Tripo Studio, pero el historial de la API solo contiene las dos tareas anteriores, así que ese rig no sirve como `input` del retarget. **Total gastado: 0 créditos.**
- Problemas del rig automático: no hubo diagnóstico de Tripo para `riggable: false`. Hipótesis sin probar: los brazos pegados al cuerpo (sin A-pose) o las 99 primitivas de material en el GLB.

## 2. Animación propia de robot

Por indicación del usuario ("intenta tú crearle las animaciones, recuerda que es un robot"), se generaron clips nuevos sobre el mismo rig rígido del T-07 Mech, reutilizando el IK de piernas y la zancada F8, de modo que el contrato distancia/zancada de `T07AnimationDriver` no cambia.

### Estándar aplicado (en `VisualPassMechRobotAnim.cs`)
- **R1 Servo:** movimientos cortos (0,1–0,3 s) con ~10 % de sobrepaso y después pausa. Tangentes lineales para que las pausas no se suavicen.
- **R2 Sensor:** cabeza estabilizada en el espacio del modelo y barridos en saltos.
- **R3 Sin respiración:** zumbido de servo de baja amplitud y reajustes de peso discretos.
- **R4 Masa:** sacudida subamortiguada en cada pisada; el cañón bajado va con retraso.
- **R5 Disparo:** el evento Attack llega con el tiro ya hecho, así que el pico del retroceso es en ≤2 fotogramas, en cadena cañón → hombro → pecho, seguido de un re-centrado subamortiguado.
- **R6 Daño:** sacudida, tartamudeo de servo (fotogramas retenidos) y recalibración rápida de la cabeza.

### Clips y conexión
| Clip | Sustituye | Estado del controller |
|---|---|---|
| `R_Idle` (4 s, bucle) | Mech_Idle | ArmedTree, DisarmedTree |
| `R_CombatIdle` (2,4 s) | Mech_CombatIdle | AlertedTree |
| `R_PatrolWalk` (ciclo) | Mech_Walk | ArmedTree (patrulla) |
| `R_CombatAdvance` (ciclo) | Mech_Walk | AlertedTree (persecución): más baja, más ancha, inclinada y con el cañón apuntado |
| `R_CannonAttack` (0,7 s) | Mech_CannonAttack | UpperBody (aditiva) |
| `R_HitReaction` (0,6 s) | Mech_HitReaction | UpperBody (aditiva) |

Turn, DisarmedWalk, Death y UpperBodyNeutral se conservan. Se usa un controller **nuevo** (copia del F8 con el mismo grafo, parámetros y máscara) en vez de un override, porque la patrulla y la persecución compartían el mismo clip Walk. Root motion: **no** (NavMeshAgent manda, igual que antes).

### Prueba en Play Mode
Escenas `T07MechRobotAnimLab_Before` y `T07MechRobotAnimLab_After`, ambas copias de CharacterLab con la misma instancia y posición. La única diferencia es el prefab: `T07_Mech_Test` frente a la variante `T07_Mech_RobotAnim_Test`.

Solo en el laboratorio se pusieron detección a 20 m, ataque a 10 m y olvido del objetivo a 1 s. Con los valores de campaña (45/38 m) el robot dispara desde cualquier punto de la arena de 32 m y nunca patrulla.

Secuencia automática, la misma en ambas escenas:
1. Patrulla, con el jugador aparcado a 24,7 m.
2. Persecución, con el jugador a 15 m.
3. Ráfaga cada 0,05 s desde el primer disparo.
4. Golpe, con ráfaga cada 0,05 s.

Resultado: estados ArmedLocomotion → AlertedLocomotion → +CannonAttack → +HitReaction, 1 disparo, 1 evento de daño y **0 errores de consola** en las dos. El Editor volvió a IntroMenu sin cambios y la auditoría da 0 errores de compilación.

Capturas en `Tools/IronRequiem/Reports/T07MechRobotAnim/`:
- `play_before.png` y `play_after.png` (filas: patrulla, persecución, disparo y golpe);
- `before_*.png` y `after_*.png` (hojas de los clips en edición);
- registros `play_*.txt`, `build-log.txt` y `labs-log.txt`.

### Revisión independiente (ciega A/B, 0–2 por criterio)
| Situación | Peso | Anticipación/impacto | Lectura robótica | Pies | Legibilidad |
|---|---|---|---|---|---|
| Patrulla | 1 → 1 | n/o | 1 → 1 | 1 → 1 | **0 → 2** |
| Persecución | 1 → 1 | n/o | 1 → 1 | 1 → 1 | 1 → 2 |
| Ataque | **0 → 2** | **0 → 2** | 1 → 1 | 2 → 2 | **0 → 2** |
| Daño | 0 → 1 | **0 → 2** | 1 → 1 | 2 → 2 | 0 → 1 |
| Idle | 1 → 1 | — | 1 → 1 | 2 → 2 | **2 → 1** |

Veredicto del revisor: B mejora claramente el disparo (pico en ≤1 fotograma, retroceso, sobrepaso y asentamiento), el golpe y la distinción patrulla/combate. El peso de las pisadas y la lectura robótica quedan iguales con esta resolución de muestreo. El idle es menos legible. Algunas afirmaciones del revisor se verificaron aparte:
- **Estiramiento del antebrazo:** descartado. Los clips solo rotan huesos; la única traslación es la de Hips.
- **"La caída del cañón al recibir daño no llega a Play":** es correcto y la causa está confirmada. `T07CannonSocket.LateUpdate` (l.36–38) impone la rotación de apuntado del cañón siempre que `AiState != Patrol`, así que en combate la pose del brazo y del cañón de los clips queda anulada. El retroceso visible en Play viene del torso y del hombro.
- **Posible interpenetración** de la parte trasera del cañón con hombro y pecho (disparo, fotograma 1) y con la cadera (golpe, fotogramas 1–2): sin confirmar, queda abierta.

### Hallazgos que afectan a producción (no corregidos; fuera del alcance)
1. En el juego **el clip HitReaction del T-07 Mech no se reproduce nunca**: `T07_Mech_Test` y el prefab de campaña tienen `EnemyMobile.UseExternalDamagePresentation = 1` y ningún otro script dispara `OnDamaged`. El golpe solo se ve mediante `RobotMotionAccents` y el destello. En la prueba se forzó el disparador en los dos laboratorios.
2. `T07CannonSocket` anula los brazos de los clips en combate (punto anterior).
3. El alcance de campaña (45/38 m) impide ver patrulla o persecución en arenas pequeñas. Es una decisión de diseño; solo se anota.

## Pasada de limpieza (17/11/2026, aprobada por el usuario)
Copia de la versión anterior (script y clips de la pasada 1) en `Tools/IronRequiem/Backups/20261117-t07mech-robot-anim/`.
- [x] **Idle:** el cañón ya no sube hacia la horizontal; se queda unos 35° abajo y gira con los barridos de la cabeza (`inspect_idle.png`). Ya no se confunde con el combat_idle.
- [x] **Daño distinto del disparo:** el disparo inclina el torso hacia atrás; el golpe ahora lo gira y lo ladea de lado (Spine rueda 10°, Chest gira 16°), con un tartamudeo de servo más largo (0,1–0,4 s en pasos de 0,07 s) y una recalibración de la cabeza al final. En Play (`play_after.png`, fila 3, columnas 1–3) se lee como torsión lateral; en la fila 2 se ve el retroceso hacia atrás.
- [x] **Interpenetración:** vistas cercanas frontal, cenital y de los dos lados en los fotogramas pico (`inspect_attack.png`, `inspect_hit.png`, `inspect_idle.png`). En el disparo, el cañón sube por delante del hombro y su parte trasera queda en el codo, sin entrar en el pecho. En el golpe, la caída inicial casi rozaba el muslo; se redujo (0,6 → 0,45) y se abrió hacia fuera (0,28 → 0,36), y ahora queda separado. Solo se comprobó a ojo con esas vistas, sin medir distancias de malla.
- [x] Recaptura en Play del laboratorio After: patrulla → persecución → disparo → golpe, 1 disparo, 1 daño, 0 errores. Compilación con 0 errores y Editor de vuelta en IntroMenu sin cambios. Los 19 hashes protegidos siguen iguales.

## Disparador del golpe y cesión del brazo (17/11/2026, aprobado: "Proponer disparador")
**Causa real, corregida respecto a lo anterior:** quien anula el brazo de los clips en combate no es `weapon.rotation = aim`. Esa línea no tiene efecto, porque `CannonMount` (el pivote) es hijo de `Weapon_EyeLazers` (el arma). Lo anula `T07CannonSocket.AimArm`, que en cada fotograma vuelve a girar el brazo superior para que la boca apunte al objetivo. Eso cancelaba la caída del cañón al recibir daño y casi todo el retroceso del disparo.

Cambios:
1. **`Scripts/Enemies/T07CannonSocket.cs`** (6 líneas añadidas y 1 cambiada; respaldo en `Backups/20261117-t07mech-robot-anim/T07CannonSocket.cs`): API nueva `YieldAim(seconds)`. Mientras dura la cesión, el peso del apuntado del brazo baja a 0 en unos 0,05 s y vuelve en unos 0,17 s, como un servo que re-adquiere el objetivo. **Si nadie la llama no hace nada:** `yieldWeight` no se actualiza mientras `yieldUntil` sea 0 y el cálculo queda `aimWeight*(1-0)`, idéntico al anterior. El laboratorio Before, que usa este script sin el disparador, repitió el mismo flujo, los mismos tiempos (primer disparo a 8,79 s) y 0 errores.
2. **`Scripts/Animation/MechHitReactionTrigger.cs`** (nuevo, solo en la variante `T07_Mech_RobotAnim_Test`):
   - Escucha `Health.OnDamaged` y dispara `OnDamaged` en el Animator.
   - Limita la frecuencia (como mucho cada 0,7 s), ignora el daño a un enemigo muerto y no actúa si `EnemyMobile` ya presenta el daño, para no dispararlo dos veces.
   - Cede el brazo 0,5 s en el golpe y 0,3 s tras cada disparo (`EnemyController.onAttack`), para que se vean la caída y el retroceso.
   - No cambia el daño, la IA ni la navegación.
3. Captura en Play **sin forzar el disparador** en ninguna de las dos escenas:
   - **Before** (como el juego hoy): nunca entra en HitReaction.
   - **After:** HitReaction desde t=10,31 con el evento de daño real.
   - En los dos casos, 1 disparo y 0 errores.
   - En `play_after.png`, fila 2, el cañón sube ~25° y se asienta antes de re-apuntar. En la fila 3 el cañón cae y el torso se gira, y re-apunta hacia la columna 5.
4. **La puntería no cambia:** `EnemyMuzzleAnchor` (aimAtTarget, cono de 60°) reorienta cada disparo hacia el objetivo, y la desviación durante la cesión es menor que ese cono. El origen del disparo sigue la boca del cañón. Con una cadencia de 0,85 s y cesiones de 0,3 s, normalmente no se dispara dentro de una cesión. Si llega un golpe justo antes de un disparo, el proyectil sale desde la boca desplazada, pero en dirección al objetivo.
5. Compilación con 0 errores; Editor en IntroMenu sin cambios; los 19 hashes protegidos iguales; único script de producción tocado: `T07CannonSocket.cs`. **No hay tests que cubran `T07CannonSocket` ni los prefabs T07_Mech**: la equivalencia sin el disparador se verificó solo en Play.

## Escena vitrina (17/11/2026, pedida: "armame la escena vitrina")
`Assets/IronRequiem/Scenes/T07MechRobotAnimShowcase.unity` tiene 12 copias del mech visual `T07Mech_Visual`, sin IA:
- **Fila delantera:** NUEVO.
- **Fila trasera:** ANTES (F8).
- **Columnas:** reposo de patrulla, reposo de combate, patrulla, persecución, disparo y golpe.

Cada copia reproduce su clip con `AnimationClipShowcase` (nuevo, `Scripts/Animation/`, un PlayableGraph que no necesita controller). Los clips en bucle se repiten seguidos; el disparo y el golpe vuelven a empezar tras 0,8 s.
- **Abrir:** menú `Tools/Iron Requiem/Visual Pass/T07 Mech robot anim (test) - Play SHOWCASE (all clips)`. El menú cambia temporalmente la escena de inicio de Play (hoy IntroMenu) y la restaura al salir. También se puede abrir la escena y pulsar Play.
- **Comprobación en Play** (`showcase-check`, máxima excursión de cualquier hueso entre 0,6 s y 2,6 s): 11 de 12 se mueven más de 1 cm.
  - Andares: 34 cm.
  - NUEVO disparo: 12 cm. NUEVO golpe: 15 cm.
  - `R_CombatIdle` se queda en 0,6 cm por diseño: sin respiración (R3), solo zumbido y correcciones de ~3°. Visualmente parece casi quieto.
  - Resultado en `Reports/T07MechRobotAnim/showcase-play-check.txt`. La imagen fija está en `showcase.png`.
- **Incidencia:** la primera versión de la comprobación repetía nombres (las dos filas de ANTES usan `Mech_Walk`) y lanzaba una excepción en cada fotograma. Además, la restauración se ejecutaba mientras Play todavía se estaba cerrando. Hubo que pararlo a mano. Corregido:
  - claves por objeto;
  - try/catch que sale de Play;
  - la restauración espera a `!isPlaying` y se reintenta si falla.
  La escena del usuario (IntroMenu) se reabrió con la operación `open:` de la cola, que no guarda nada y rechaza escenas modificadas.

## Pendientes antes de integrar
- [ ] Prueba manual con teclado y ratón y coste del Animator con varios T-07 Mech.
- [ ] Integración (requiere aprobación): asignar `T07MechRobot_Locomotion` a `T07_Mech.prefab` y añadirle `MechHitReactionTrigger`.
- [ ] Prueba manual con teclado y ratón (solo se hizo la secuencia automática) y medición del coste del Animator (los clips tienen 59–75 curvas, frente a una cantidad similar en F8; no se midió).
- [ ] Integración propuesta: cambiar en `T07_Mech.prefab` el controller por `T07MechRobot_Locomotion`, o hacer una variante de campaña. **Requiere aprobación.**

## Skills usadas
- `iron-requiem-enemy-ai`: contrato MoveSpeed/Attack/Alerted/OnDamaged, variante de prefab, no tocar el FBX y la regla de que Generic no prueba un rig.
- `gamedev:create-game-assets`: inspeccionar antes de inventar, registro del origen (README) y QA de escala, orientación y triángulos de la copia.
- `game-dev-combat-animation`: estándar escrito antes de los clips, línea base antes de cambiar, rúbrica de 5 criterios con revisor ciego A/B y verificación de cada afirmación con números.
- No cargadas por no ser necesarias en el trabajo real: `gamedev:unity-animation` y `gamedev:unity-navmesh`. Se usaron directamente las APIs del proyecto y su NavMesh existente.

## Archivos
Creados:
- `Assets/IronRequiem/Editor/VisualPass/VisualPassMechRobotAnim.cs`
- `Assets/IronRequiem/Editor/VisualPass/VisualPassMechRobotAnimLab.cs`
- `Assets/IronRequiem/Art/Enemies/T07MechRobotAnim/` (6 clips y `T07MechRobot_Locomotion.controller`)
- `Assets/IronRequiem/Prefabs/VisualPass/Mech/T07_Mech_RobotAnim_Test.prefab` (variante)
- `Assets/IronRequiem/Scenes/T07MechRobotAnimLab_Before.unity` y `T07MechRobotAnimLab_After.unity`
- `Assets/IronRequiem/Scenes/T07MechRobotAnimShowcase.unity` (vitrina) y `Assets/IronRequiem/Scripts/Animation/AnimationClipShowcase.cs`
- `Assets/IronRequiem/Scripts/Animation/MechHitReactionTrigger.cs`
- `Tools/IronRequiem/Blender/export_tripo_input.py`
- `Tools/IronRequiem/Generated/Tripo_T07Mech_20261117/`
- `Tools/IronRequiem/Reports/T07MechRobotAnim/`
- este informe

Respaldo y hashes en `Tools/IronRequiem/Backups/20261117-t07mech-robot-anim/`: los 19 archivos protegidos (CharacterLab, `T07_Mech_Test`, `T07_Mech`, el controller y los clips F8, el FBX, los scripts F8 y EditorBuildSettings) siguen **iguales**.

Uso:
- **Regenerar los clips:** menú `Tools/Iron Requiem/Visual Pass/T07 Mech robot anim (test) - build clips + sheets`, o escribir `clips` en `Tools/IronRequiem/Requests/t07mech-robot-anim.txt`.
- **Probar:** abrir `T07MechRobotAnimLab_After` y pulsar Play.

Sin commits.
