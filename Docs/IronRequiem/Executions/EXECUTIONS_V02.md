# Ejecuciones v02 — robots más brutales y legibles (22/11/2026)

Chat 13. El usuario aprobó «Todo, Big Bot incluido» sobre `F1_PROPUESTA.md`: guion, reactivación del Big Bot, Recover 0,24 s, micro-paso en banda lejana y destello en el disparo del Big Bot. La regla <75 %, el daño, la vida, la IA, el inventario y los controles no cambian. No hay commits ni paquetes, no se gastaron créditos y no se guardó ninguna escena.

- **Respaldo:** `Tools/IronRequiem/Backups/20261122-executions-v02/` (41 archivos, `hashes-before.sha256` y `protected-zone.sha256`, que sigue intacto). Los prefabs también están en `Backups/2026-11-22/Executions` y los scripts del jugador en `-player/`.
- **Diagnóstico y propuesta:** `F0_DIAGNOSTICO.md` y `F1_PROPUESTA.md`, con los storyboards SVG.

## Guion final (reloj de la ejecución; tiempo real = reloj + hit-stops)

**T-07 / T-07 Mech / SawBot.** Duración real 1,17–1,20 s. El movimiento y la vista quedan libres; las acciones se bloquean hasta 0,93–0,97 s.

| Beat | t | Robot | Jugador | Feel |
|---|---|---|---|---|
| Begin/Grab | 0 | Arrastrado hacia la mano (hasta 0,80 m, sin acercar ningún collider del cuerpo a menos de 0,40 m de la cámara; se detiene en muros) y se inclina | La mano sube y agarra la superficie del arma (≤0,55 m de profundidad); micro-paso ≤0,45 m si está lejos | Kick 0,4° |
| Struggle | 0,18 | Tres tirones del brazo del arma, retenidos; servo continuo; la unión no deriva más de 3 cm | Agarre con temblor | Kick + trauma 0,10 |
| Rip ★1 | 0,34 | El arma se oculta en el mismo frame; el brazo vacío cae −35° con rebote; pulso emisivo local | Proxy del arma en la mano izquierda | Hit-stop 0,05 s @0,10; arco de extracción |
| Handoff | 0,44 | Se recompone y se pone en guardia (anticipación) | El proxy viaja en arco hasta el brazo derecho | — |
| WindUp | 0,52 | — | El arma real aparece en el brazo derecho (sin despliegue genérico); puño | Kick |
| Strike ★2 | 0,732 | `Complete()`: muerte por el flujo existente, pecho −18°, empuje de 0,9 m | El puño llega a la profundidad del pecho | Hit-stop 0,06 s @0,08 |
| Recover/End | 0,83 / 1,07 | Los restos de la muerte se desvían para no ir hacia la cámara | La mano sale por abajo | — |

**Big Bot.** Control devuelto a 1,40 s de reloj (1,52 s reales; antes 2,30 s).

| Beat | t | Resumen |
|---|---|---|
| Begin | 0 | Salto de 0,40 s al perch, delante del cañón y en el lado del jugador. No hay aim override duro: la vista se asiste con tope de 1,0°/frame |
| Grab | 0,40 | El robot se hunde 0,08 m y se ladea 6°; la mano cierra sobre el cañón |
| Struggle | 0,46 | Tirones de −14°, +10° y −18° en el hombro |
| Rip ★1 | 0,62 | El cañón se oculta en el robot y pasa al proxy; chispas `breakSparks`; hit-stop 0,05 s @0,10 |
| Handoff | 0,80 | Cambio de arma diferido (antes ocurría en E) |
| Strike ★2 | 0,95 | Disparo y `Complete()`; destello a ≥0,6 m de la cámara; hit-stop 0,06 s @0,08. El salto atrás empieza en este frame |
| End | 1,40 | Aterrizaje en el punto de salida si está libre; si no, en un punto comprobado sobre el NavMesh |

**Sonido.** Lo reproduce el robot por beat (`ExecutionRobotAudio`), con clips existentes y los grupos del mixer actual:

| Clip | Uso |
|---|---|
| IR_Mount_Clamp | agarre |
| IR2_Capsule_Creak / SawMotor_Attack_Loop / IR_Hangar_Hydraulic | forcejeo, con fundido |
| IR_Hangar_Tear + IR_Hangar_Spark | arranque |
| IR_Mount_Lock | traspaso |
| IR_Fist_HeavyImpact | golpe final (antes no tenía sonido) |
| IR_BigBot_Blast + IR2_Impact_SubBoom | disparo del Big Bot |
| Jump / Land (plantilla) | salto del Big Bot |

## Cambios (archivo:línea)

**Contrato**
- `Scripts/Enemies/Execution/ExecutionBeat.cs` (nuevo): beats, tipos y datos de cada beat.
- `Scripts/Enemies/EnemyExecutionSource.cs`:
  - l.26 `EnsureReactionLayers`
  - l.58 `RaiseBeat`
  - l.61 `WeaponJoint`
  - La regla <75 % no cambia.

**Robot** (`Scripts/Enemies/Execution/`, todo nuevo)
- `ExecutionReaction.cs` (orden 160):
  - capa aditiva por beat sobre huesos existentes;
  - l.19 perfiles por tipo;
  - l.405 servo continuo circular (sin Animator en el Mech).
- `ExecutionPullIn.cs`: arrastre seguro con `agent.Move` y SphereCast.
- `ExecutionWeaponDetach.cs`: oculta el arma en Rip y publica su pose.
- `ExecutionLocalFlash.cs`: solo el pulso local; respeta la fase de daño.
- `ExecutionRobotAudio.cs`: sonido por beat.
- `ExecutionHealthBarHider.cs`: oculta la barra de vida en mundo durante la ejecución.
- `ExecutionDebrisGuard.cs`: desvía de la cámara los restos de la muerte, por reflexión cacheada.

**Robot, archivos existentes**
- `SawBotWeaponSource.cs`:
  - l.15 `Rip()`: la sierra se oculta en el arranque, no en E.
  - l.20 `Joint` = `Forearm_R`.
- `AI/SawBotCombat.cs` l.47 `SetExecutionChain`.
- `VisualPass/HeavyDeathFeel.cs` l.31: sin hit-stop si la muerte es una ejecución.

**Jugador** (`Scripts/Player/Execution/`, todo nuevo)
- `ExecutionDirector.cs`: reloj único, kicks, hit-stop y beats.
  - l.57 `BeginPunch`
  - l.81 `BuildPunchCues`
  - l.97 `BeginBig`
  - l.160 `Fire`
- `ExecutionReachResolver.cs`:
  - l.29 constantes (profundidad objetivo 0,40, arrastre ≤0,80, paso ≤0,45);
  - l.91 `Resolve` (bandas, superficie del arma, margen de 0,40 m con el cuerpo).
- `ExecutionWeaponCarry.cs`: proxy del arma con los renderers del `StolenCannon` ya creado.

**Jugador, archivos existentes**
- `DisarmMotion.cs`:
  - l.29 perfil `RipOnly`;
  - Recover 0,24 (`recoverTimeV4`);
  - `SetGrip` con x [−0,50, 0,20], y [−0,40, 0,18] y z ≤0,55, limitado al 90 % del encuadre.
- `ProtagonistHandsPresentation.cs`:
  - l.94 `SetGripViews`
  - l.106 `DisarmEndedAt`
  - l.108 `SetWeaponAimOffset`
  - Los kicks pasan al director.
- `StolenWeaponCarrier.cs`:
  - l.23 `ExecutionBusy`
  - l.115 `TryExecute`: el Big Bot ya no cambia de arma en E.
  - l.151 `ExecutionBlow`
- `BigBotExecutionSequence.cs`, reescrito:
  - l.91 `Begin`
  - l.123 perch delante del cañón
  - l.155 `UpdateViewAssist`
  - l.293 `ComputeBackPoint`
- `DistanceCameraImpulse.cs` l.17/85: tope de 90°/s.
- `VisualPass/GameFeel.cs` l.25/29: presupuesto de hit-stop de la ejecución.
- `VisualPass/ExtractionArcFx.cs` l.51: se reduce cerca de la cámara.
- `VisualPass/StolenShellFit.cs`: absorción silenciosa durante la ejecución.

**Prefabs**
- `Editor/Executions/ExecutionsAuthoring.cs` + `VisualOverhaulExecutions.cs` añadieron los componentes y los clips a `T07_Current_Repaired`, `T07_Mech`, `SawBot_Current` y `BigBot_Current`, y pusieron `executionEnabled = true` en el Big Bot. Las variantes de Map01 lo heredan.

**Tests**
- Nuevos: `ExecutionsV02Tests.cs` y `ExecutionsF0Capture.cs` (captura con fixtures).
- Ajustados por cambios aprobados:
  - `CampaignTests.cs` l.294/313: el Big Bot es ejecutable.
  - `FastCombatTests.cs` l.62: Big Bot 1,3–2,0 s, junto al hombro.
  - `ArmsFixPassTests.cs` l.220: el cambio de arma ocurre en WindUp.
- `ArmsFixPassTests.cs` y `ExecutionsV02Tests.cs` además aíslan las reasignaciones de teclas del usuario.

## Verificación

**Compilación:** 0 errores.

**Tests PlayMode** (Editor abierto, entrada real del Input System): 13 de 14 aprobados.
- `ExecutionsV02Tests` 8/9 y `ArmsFixPassTests` 4/4; aparte, `FastCombat.BigBotExecution` 1/1.
- Aprobados:
  - T-07, T-07 Mech y SawBot:
    - E real, exactamente 2 golpes (0,35 y 0,737 s), una muerte en StrikeTime ±0,05 y nada después;
    - ≤1,2 s reales;
    - mano a ≤0,08 m de la superficie del arma, codo sin estirar (profundidad ≤0,55);
    - robot vivo (≤2 frames sin movimiento), cámara ≤0,76°/frame y fuera del cuerpo (≥0,75 m);
    - M1, F, 1, 2 y G ignorados, y F vuelve a funcionar después.
  - Muro detrás del robot.
  - Muerte del jugador a mitad (T-07 y Big Bot) y pausa a mitad, sin residuos: timeScale 1, sin movimiento por script, sin bucles de sonido y beat Cancel/End.
- **Falla `BigBot_RipAndShot…`** por 4 criterios:
  - la cámara gira 1,53°/frame (límite 1,5);
  - la cámara roza el cuerpo a 0,28 s, en pleno arco del salto (0,02 m);
  - el robot sale del 15–85 % del encuadre en 43 frames;
  - a 1,2 s el jugador está a 1,8 m (límite 2,0).
  - Sí cumple: muerte a 0,955 s, control a 1,52 s reales, mano sobre el cañón y bloqueos.
- **Regresión** (Combat, Fast, SawBot, Shotgun, BigBot, Bunker, VisualOverhaul, Campaign, ModularArm, CombatV03): 70 de 81 aprobados.
  - Los 11 fallos son 1 cambio esperado (ya ajustado) y 10 previos y ajenos.
  - 6 de esos 10 se comprobaron con el código original restaurado temporalmente:
    - BigBotDestruction ×2
    - Campaign «F carga» ×2
    - ModularArm
    - VisualOverhaul ADS
  - Los otros 4 ya fallaban en la línea base de ARMS_FIX:
    - menú → Map01
    - escopeta
    - brazo crítico
    - umbral FastCombat
  - Varios se deben a que **en PlayerPrefs del usuario Melee está en clic derecho y Apuntar en F**.

**Captura antes/después:** `Tools/IronRequiem/Reports/Executions/before_v3/` y `after_v3/` (vista del jugador y lateral, con hojas de contacto); las pasadas intermedias están en `before*/` y `after*/`.
- Las dos se grabaron con daño real previo. Con la vida fijada a mano, el primer golpe disparaba la fase de daño y pintaba de rojo.

**Revisor independiente ciego** (opus, sin guion, A/B aleatorio; claves en el scratchpad):
- Ronda 1: prefería el original en T-07 y Big Bot porque la cámara entraba en el cuerpo. Se corrigió.
- Ronda 2: **prefiere la versión nueva en los tres robots**.

| Versión nueva (ronda 2) | Peso | Legibilidad | Brazos | Reacción | Mareo |
|---|---|---|---|---|---|
| T-07 | 7 | 5 | 6 | 7 | 7 |
| SawBot | 6 | 6 | 6 | 5 | 9 |
| Big Bot | 7 | 4 | 7 | 6 | 5 |

**Prueba manual con teclado y ratón físicos:** no se hizo; queda para el usuario.

## Ronda 2 del Big Bot (22/11/2026, aprobada por el usuario)

**Causas encontradas** (medidas en el test, no supuestas):
- El único collider del Big Bot es una caja de 3,5 × 3 × 2,3 m; los bounds de sus mallas skinned (escala 348) abarcan todo el esqueleto. El perchero se calculaba contra esa caja: quedaba al costado del robot, con el robot a 90° de la vista, y el arco (0,9 m) pasaba por encima del hombro.
- `BigBotCombat.LateUpdate` seguía apuntando brazos y torso al jugador mientras saltaba: el cañón se alejaba 0,6 m de la mano.
- En `Update` los huesos tienen la pose cruda del Animator; la puntería y `ExecutionReaction` se suman en `LateUpdate`. Todo lo calculado en `Update` apuntaba a una pose que nadie ve.
- Los tirones del hombro (−14…−18°) y la inclinación de aterrizaje (−7°), con una palanca 3,5 veces mayor, hundían el cañón ~0,6 m.
- `BakeMesh` devuelve vértices degenerados en estas mallas (no legibles, escala 348): la primera métrica de malla del test era falsa y se descartó.

**Cambios**
- Nuevo `Scripts/Enemies/Execution/ExecutionBodyProxy.cs` (en `BigBot_Current`, generado por `ExecutionsAuthoring.BakeParts`): una caja por pieza rígida (Torso, Shoulder_R, Forearm_R…) en espacio de hueso, sacada de los vértices LOD0 por bindpose. Las piezas del cañón guardan además ≤256 muestras de superficie. Guarda las matrices de la **pose renderizada** al final de `LateUpdate` (orden 500) y todas las consultas usan esa pose.
- `BigBotExecutionSequence.cs`:
  - `ChoosePerchCamera`: recorre el brazo cañón (15–95 %), agarre sobre la superficie real, cámara delante, a ≥0,40 m del cuerpo visible sin empujarla, alcance ≤0,45 m; gana la más centrada en el robot.
  - `LeapBend` + arco 0,35 m: el salto se curva lateralmente si roza el cuerpo.
  - `FollowCannon`: del aterrizaje al arranque el jugador cuelga del cañón (sigue un ancla sobre la armadura, sin girar la cámara). Colisiones con el robot ignoradas hasta el salto de vuelta.
  - Asistencia de vista: apunta entre el agarre y el centro del cuerpo, presupuesto 0,95 − kick (≤0,9°/frame) y se apaga ±50 ms del arranque y 40 ms antes del disparo.
  - Vuelve al punto de salida (guion F1 §3.3).
- `AdvancedPlayerMovement.cs`: sobrecarga `BeginScriptedMove(..., horizontalBend)`; la de 3 argumentos sigue igual.
- `AI/BigBotCombat.cs`: `HoldAim(bool)`; durante la ejecución mantiene la puntería que tenía al pulsar E (solo presentación).
- `ExecutionReaction.cs` (perfil Big Bot): tirones {5, −2, 6} (el robot intenta levantar el cañón del que cuelgas) y aterrizaje −4°.
- `ProtagonistHandsPresentation.cs` / `ExecutionReachResolver.CannonArmPoint`: la mano del Big Bot se cierra sobre la superficie del cañón del proxy (antes, sobre bounds de 3,9 m).

**Tests** (`ExecutionsV02Tests`): el Big Bot ahora se mide contra los **triángulos reales** (22 900, colocados por bindpose): cámara ≥0,30 m del cuerpo visible y ≥0,12 m del cañón; mano ≤0,08 m del cañón en todos los frames de forcejeo salvo uno (la mano lee la pose del frame anterior y un tirón brusco abre 1 frame de hueco), y nunca >0,20 m.

**Resultado**
- `ExecutionsV02Tests` **9/9**.

| Criterio Big Bot | Ronda 1 | Ronda 2 |
|---|---|---|
| Giro de cámara máx. | 1,53°/frame | 1,26°/frame |
| Cámara–cuerpo visible mín. | (caja) 0,02 m | 0,97 m |
| Frames con el robot fuera del 15–85 % | 43 | 0 |
| Distancia a 1,2 s | 1,80 m | 2,09 m |
| Mano–cañón real | sin medir (bounds) | 0,01–0,05 m; 1 frame 0,13 m |

- Regresión de esta ronda: ArmsFix 4/4, BigBotCombat 10/10, BigBotBurst 6/6, AnimationRestore 3/3, FastCombat 5/6 (el Big Bot vuelve a aterrizar en la salida; falla el umbral T-07, previo), BigBotDestruction 8/10 (los 2 previos), Campaign 4/7 tras repetir `TwelveRounds…` (falló por un PNG bloqueado, IO 1224, y pasó al repetirlo; los otros 3 ya fallaban en la ronda 1).
- `BigBotBazooka.StolenBazookaFramingChargeThrowAndOverdrive` falla (socket del módulo a 0,37 m). Último pase registrado: 08/11. Los cambios de ejecución solo actúan durante el desarme o con la puntería del Big Bot activa, así que no deberían afectarlo, pero **no se verificó restaurando el código**.

**Revisor ciego, ronda 3** (opus, solo Big Bot, ronda 1 contra ronda 2 en A/B aleatorio; clave en el scratchpad): **prefiere la ronda 2**, porque mantiene la silueta y el núcleo del robot en cuadro hasta el disparo y el fogonazo a 1,00 s ilumina el golpe. Lo que mejor se lee de la ronda 1 es la mano sobre el cañón entre 0,53 y 0,73 s.

| Big Bot | Peso | Legibilidad | Brazos | Reacción | Mareo |
|---|---|---|---|---|---|
| Ronda 2 | 5 | 5 | 5 | 4 | 7 |
| Ronda 1 | 5 | 4 | 5 | 4 | 4 |

Este revisor puntúa más duro que el de la ronda anterior: a la ronda 1 le dio 4 en mareo, frente al 5 anterior. Ninguna nota llega a 8. Defectos que señala en la ronda 2:
- **0,80–0,90 s:** una losa oscura (el cañón arrancado, el proxy de `ExecutionWeaponCarry`) ocupa ~60 % del encuadre y el brazo izquierdo desaparece.
- **1,00 s:** el montaje en el brazo derecho no se ve; el cañón flota arriba a la izquierda y el arma montada solo aparece hacia 1,40 s.
- **0,40–0,63 s:** el antebrazo izquierdo es muy grande en la esquina.
- **Reacción:** el robot casi no reacciona al arranque ni al disparo.

## Ronda 3 del Big Bot (23/11/2026, aprobada por el usuario)

**Causas medidas:**
- **0,63–0,80 s:** el cañón arrancado (el módulo `StolenCannon`, a escala del robot) medía 2,5–2,9 m y volaba a 0,33 m de la lente. Esa era la «losa oscura».
- **Disparo de 0,95 s:** el bazooka robado no estaba activo hasta ~1,40 s (cambio de arma normal), así que el disparo salía con el arma vieja.
- **Muerte del robot:** su fase 3 llamaba a `DetachCannon()` y lanzaba un **segundo cañón** (el que flotaba arriba a la izquierda a 1,00 s), aunque el jugador ya tenía el suyo.

**Cambios**
- `ExecutionWeaponCarry.cs`: un módulo de más de `maxHeldSize` (0,45 m) se encoge a ese tamaño en el primer 60 % del vuelo a la mano. Al nacer coincide con el cañón que el robot acaba de ocultar. La escala se restaura al ocultarlo. Los módulos de T-07 y SawBot son menores y no cambian.
- `BigBotExecutionSequence.cs`, beat Handoff (0,80 s): `PlayerWeaponsManager.SnapActiveWeaponUp()`. El cañón que llega al brazo derecho es el arma activa en ese frame, y el proxy se oculta en el mismo frame.
- `BigBotDestruction.cs`: `MarkCannonTakenByExecution`. `ExecutionWeaponDetach` lo marca en el arranque y lo desmarca si la ejecución se cancela. Con la marca puesta, `DetachCannon` solo da el cañón por perdido, sin restos ni coleccionable. Fuera de la ejecución no cambia nada.
- `ExecutionReaction.cs`, Big Bot, beat Rip: tambaleo.
  - El torso cede 5° hacia el jugador, gira 15° y se ladea 8° hacia el lado arrancado, y la cadera cae 12 cm.
  - Tras la congelación de 2 frames se echa atrás 14° (subamortiguado) y se asienta.

**Tests nuevos** en `BigBot_RipAndShot…`:
- en el disparo, el arma activa y visible es el cañón robado y el proxy ya no se ve;
- a menos de 0,6 m de la lente, el proxy mide ≤0,5 m (medido: 0,45);
- tras la muerte no queda cañón caído ni restos de cañón sueltos (el único es el que lleva el jugador).

**Resultado**
- `ExecutionsV02Tests` 9/9.
- Regresión: ArmsFix 4/4, BigBotCombat 10/10, BigBotBurst 6/6. Los fallos de FastCombat (umbral T-07), BigBotDestruction ×2, ModularArm ×1, Campaign ×3 y BigBotBazooka ×1 son los mismos que antes de esta ronda.
- Incidencia: el Editor tardó ~6 min por recarga de dominio (346 s en el log, con errores de red del paquete AI Assistant) y el vigilante cortó 2 pruebas. Tras reiniciar Unity volvió a ~10 s.

**Revisor ciego, ronda 4** (opus, ronda 2 contra ronda 3 en A/B aleatorio): **prefiere la ronda 3**. Dice que es legible y cómoda: la mano sujeta el cañón a 0,77 s y el disparo de muerte se ve limpio.

| Big Bot | Peso | Legibilidad | Brazos | Reacción | Mareo |
|---|---|---|---|---|---|
| Ronda 3 | 5 | 5 | 5 | 4 | **8** |
| Ronda 2 | 6 | 4 | 4 | 3 | 5 |

Le sigue fallando:
- **0,77 s:** el cañón encogido parece un palo negro fino. Gana legibilidad, pero pierde peso.
- **0,83–1,00 s:** el cañón montado solo asoma como un muñón en el borde inferior derecho; no se ve que el disparo salga del arma robada.
- **0,30–0,90 s:** el robot apenas se mueve mientras le arrancan el brazo. El tambaleo de 15° no se aprecia a esa distancia.
- **1,20 s:** los rayos blancos de muerte (sistema existente) cubren ~70 % un instante.

## Pendientes (no corregidos)
1. **Big Bot:**
   - el cañón montado no se encuadra en el disparo: solo asoma el cañón del arma en el borde inferior derecho, y la pose del arma montada (`BigBot_PlayerWeapon`) es la del juego normal;
   - el tambaleo existe (medido en los huesos) pero no se lee desde tan cerca; haría falta un gesto mayor o el cuerpo entero en cuadro;
   - el cañón encogido a 0,45 m pierde sensación de tamaño (compromiso elegido frente a la losa de 2,7 m en la lente);
   - los rayos blancos de la explosión de muerte existente llenan la pantalla a 1,2 s, aun a 2,1 m (sistema existente, fuera de alcance);
   - el jugador queda colgado ~0,6 m sobre el suelo delante del cañón (no hay asidero real en el modelo);
   - la mano lee la pose del frame anterior: en un tirón brusco se separa 1 frame.
2. **T-07 0,47 s:** el brazo o el arma arrancada tapa ~60 % de la pantalla. La transferencia mano izquierda → derecha no se lee.
3. **Golpe final del SawBot:** solo rayas, sin fogonazo; el cuerpo sale volando entero (muerte existente).
4. **Cuadrados amarillos del arco de extracción:** siguen viéndose planos y grandes cerca de la cámara.
5. **`ExecutionDebrisGuard`:** usa reflexión sobre campos privados de `RobotArmorDebris` y `MechDebrisPiece`. Es frágil si se renombran.
6. **Big Bot ejecutable:** `Editor/HangarIntro/CombatV4Authoring.cs` (zona protegida) lo desactivaría si se vuelve a ejecutar.
7. **Teclas del usuario:** las reasignaciones en PlayerPrefs (Melee = clic derecho, Apuntar = F) hacen fallar varios tests antiguos que pulsan F. Son ajenas.

## Skills usadas
- **game-dev-combat-animation:** estándar E1–E13, tests que miden por frame, revisores ciegos con A/B.
- **gamedev:game-feel:** hit-stop con presupuesto, kicks con tope, sin hit-stops apilados.
- **gamedev:fps-shooter / camera-systems:** sin giro forzado, encuadre y FOV.
- **gamedev:unity-animation / unity-csharp-scripting:** capa aditiva en LateUpdate y restauración en Update, orden de ejecución, sin asignaciones por frame.
- **gamedev:audio-design:** un clip por beat por el mixer, con fundidos.
- **iron-requiem-fps / enemy-ai / modular-weapons / performance:** un solo sistema de armas, IA intacta, sin luces nuevas por robot.
- **create-game-vfx / design-action-combat / build-rigged-game-assets:** solo como principios.
