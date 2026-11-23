# Ejecuciones v02 — F1 propuesta de animación (22/11/2026)

Solo propuesta. No se tocó `Assets/`, ProjectSettings, Packages ni escenas, y no se ejecutó Unity. Base: `F0_DIAGNOSTICO.md`, las 6 hojas de contacto y los frames de `Tools/IronRequiem/Reports/Executions/before_v2/`, `telemetry.csv`, `ARMS_FIX_20261119.md`, los documentos de rig y el código citado.

Storyboards:
- [storyboard_T07.svg](storyboard_T07.svg)
- [storyboard_SawBot.svg](storyboard_SawBot.svg)
- [storyboard_BigBot.svg](storyboard_BigBot.svg) (requiere aprobación)

## 0. Qué arregla y cómo

F0 mide los mismos cuatro fallos en los tres robots. La propuesta responde a cada uno:

| Fallo medido en F0 | Respuesta F1 |
|---|---|
| Robot congelado 30/44 (T-07) y 32/44 (SawBot) frames vivos | Capa de reacción por beats sobre huesos existentes: forcejeo, pieza que cede, anticipación del puño, golpe |
| La mano nunca toca la unión (0,51–0,72 m de hueco; agarre a 0,55 m y unión a ≥1,2 m) | **La pieza viene a la mano:** el robot es arrastrado hacia la mano. El brazo nunca pasa de 0,55 m |
| La mano agarra el pecho, no el arma (la unión sale del rayo de interacción) | El punto de agarre pasa a ser el socket real del arma |
| Sin hit-stop, kicks casi invisibles, golpe 2 sin sonido | Hit-stop de 2 frames en la muerte, kicks con pico y tope definidos, un clip existente por beat |
| El arma desaparece (SawBot: 0,37 s sin sierra; Big Bot: el cañón se oculta de golpe y aparece duplicado en la mano desde E) | Continuidad: robot → mano izquierda (proxy) → brazo derecho, sin huecos ni duplicados |
| Big Bot: 2,30 s sin control, latigazo de 17°/frame, 49 frames >1°/frame | 1,58 s, sin aim override; la cámara solo recibe kicks con tope |

Otros hallazgos de las hojas (no estaban en F0):
- **T-07:** tinte rojo de cuerpo entero entre 0,40 y 0,67 s. Encaja con el gradiente de daño de plantilla (`EnemyController.OnHitBodyGradient`, 0,5 s), disparado por el `Blow` de 0,01 HP; hay que confirmar el origen en F2. Tapa la lectura de la pieza que cede.
- **Big Bot:** su barra de vida en mundo (`WorldspaceHealthBar`, plantilla) ocupa ~15 % de la vista a 1,10 s, con el jugador encima.
- **SawBot y Big Bot:** los restos de la muerte salen volando por encima del encuadre (vista lateral 1,2–1,6 s y 2,0–3,5 s). Es el flujo de muerte existente: fuera de alcance, queda anotado como riesgo.
- **`ExtractionArcFx`:** en código llama a `GameFeel.HitStop(0,06)` en el arranque, pero F0 no mide ningún hit-stop en el T-07. Hay que comprobar si el componente está en el jugador de campaña antes de contar con él.

## 1. Estándar de la ejecución (reglas numeradas)

Se escriben antes de las claves, como pide el estándar de animación de combate. **[T]** = se mide en un test; **[R]** = se revisa sobre frames.

- **E1 [T] Golpes:** T-07 y SawBot reciben exactamente 2 impactos: arranque a YankTime 0,34 s y puño con muerte a StrikeTime 0,732 s (±0,05 s). Nada después.
- **E2 [T] Duración real** (de E a la mano fuera de vista): ≤1,20 s en T-07/SawBot; Big Bot ≤2,0 s hasta recuperar el control (objetivo 1,58 s).
- **E3 [T] Alcance:** la mano nunca pasa de 0,55 m de profundidad en cámara. En Forcejeo (0,18–0,34 s), la distancia de la mano a la unión es ≤0,08 m cuando la unión estaba a ≤1,52 m en E.
- **E4 [T] Codo:** el codo del brazo izquierdo FPS queda entre 10° y 152°, y ≥20° en el 80 % central de la ejecución (el brazo no se bloquea recto).
- **E5 [T] Arma continua:** en cada frame hay exactamente una instancia visible del arma: montada en el robot, proxy en la mano o arma del jugador. Se tolera un frame de solape. Salto de posición entre frames ≤0,15 m (≤0,25 m solo en el lanzamiento de 0,44–0,52 s).
- **E6 [T] Robot vivo:** como máximo 6 frames congelados (<0,1° de cambio en todos los huesos) entre E y la muerte. Ningún hueso gira >25° en un frame.
- **E7 [T] Cámara sin mareo:** la velocidad angular no causada por el ratón es ≤1,5°/frame a 60 FPS (≤90°/s), con ≤6 frames por encima de 1°/frame en toda la ejecución. Yaw de kick ≤1°. Cero frames con aim override.
- **E8 [T] Hit-stop:** ≤2 por ejecución; el timeScale vuelve a 1,0 ≤0,1 s después; mientras dura la ejecución no se suma otro hit-stop ajeno (HeavyDeathFeel, ExtractionArcFx).
- **E9 [T] Presupuesto:** pico de partículas ≤ línea base F0 (T-07 461, SawBot 406, Big Bot 486); número de Lights sin cambios.
- **E10 [T] Robot en sitio legal:** al terminar, la raíz no se solapa con muros (OverlapCapsule vacío salvo el suelo), queda a ≤0,1 m del NavMesh y su altura cambia ≤0,05 m. El robot solo se mueve por el arrastre y el empuje del jugador.
- **E11 [R] Poses de mano:** abierta al acercarse, agarre sobre la unión, puño solo de WindUp a Impact, relajada al salir. Nunca abierta y estirada contra el aire.
- **E12 [R] Lectura:** cada beat se entiende a 60 FPS con FOV horizontal 90° y 110°. La mano y el puño quedan dentro del 5–95 % de la pantalla.
- **E13 [T] Controles:** M1, F, 1, 2 y G se bloquean hasta el final de Impact (0,83 s de reloj, como hoy). En T-07/SawBot, movimiento y vista quedan libres todo el tiempo.

## 2. Mecánicas comunes

### 2.1 La pieza viene a la mano (sin estirar el brazo)
1. **Punto de agarre = socket real**, no el punto del rayo:
   - T-07: `T07CannonSocket.socket` (Forearm_L, el lado derecho del robot).
   - SawBot: unión de la sierra que oculta `SawBotWeaponSource`. Hay que confirmar si es el hueso `_L` o `_R`, igual que en el T-07.
   - Big Bot: `BigBotDestruction.CannonPivot`.
2. Se mide **d** = profundidad de la unión en cámara al pulsar E y se elige una banda:

| Banda | d en E | Qué ocurre |
|---|---|---|
| A | ≤0,62 m | La mano llega directamente. El robot solo se inclina |
| B | 0,62–1,52 m | **Arrastre:** el robot es arrastrado hacia el jugador `clamp(d − 0,60 − 0,12, 0, 0,80)` m entre 0,00 y 0,18 s (suavizado entrada-salida, pico ≈6,7 m/s). La inclinación del pecho aporta los otros ~0,12 m |
| C | >1,52 m, o arrastre bloqueado por geometría | **El arma vuela a la mano:** la mano se queda a 0,55 m con la pose de tirón, y en el arranque el proxy recorre un arco de 0,08 s hasta la mano abierta, que cierra a 0,42 s. Variante con paso del jugador en «Requiere aprobación» |

- **Seguridad del arrastre:** usa la misma técnica que `ExecutionShove`, en sentido contrario: `agent.Move` mientras el agente está en el NavMesh, SphereCast contra muros y parada a radio de cápsula del jugador + 0,25 m.
- **No es movimiento propio:** sin IA y causado por la mano. En el T-07 los pies (IK de `T07FootPlacement`) se arrastran. Es un riesgo que hay que medir (§6).

### 2.2 Arma continua (E5)
1. **De E a 0,34 s:** el arma sigue montada en el robot. En el SawBot se elimina la ocultación en E.
2. **A 0,34 s (mismo frame):** se apagan los renderers del arma del robot y se enciende el **proxy** en la misma pose de mundo.
   - El proxy es la vista del `StolenCannon` ya creado en E por `CreateReservedModule`, sin otra instancia.
   - Se pega a un socket de agarre de la mano izquierda con una mezcla de 0,05 s.
3. **De 0,44 a 0,52 s:** la mano izquierda lanza el proxy en arco corto (0,08 s) al socket del brazo derecho.
4. **A 0,52 s (= WindUpTime):**
   - El arma robada real aparece en el brazo derecho, en la pose del proxy y sin la subida de `WeaponDeployMotion`. El proxy se apaga en el mismo frame.
   - `ShowHeldWeapon` pasa de 0,34 a 0,52 s, que sigue siendo «después del arranque» (regla del arms-fix). La mano izquierda queda libre para cerrar el puño.

### 2.3 Feel con presupuesto (E2, E8)
- **Sin hit-stop global en el arranque:** «hold local» de 2 frames. La pose de la mano y la capa del robot se congelan de 0,340 a 0,373 s sin parar el reloj; el salto al retomar es despreciable (EaseIn del Yank: 0,6 %).
- **Hit-stop global solo en el puño:** `GameFeel.HitStop(0,033, 0,10)`, 2 frames, +0,030 s reales. Total real: 1,167 + 0,030 = **1,197 s**.
- **Presupuesto de ejecución:** mientras dura, se ignoran el hit-stop de `HeavyDeathFeel` (SawBot: hoy 0,06–0,08 s), el de `ExtractionArcFx` y el asiento de `StolenShellFit` (ya silencioso).
- **Kicks** (`DistanceCameraImpulse.Kick`):
  - Se dan como pico deseado. El pico real ≈ 0,78 × el valor pasado con frecuencia 26 y amortiguación 0,7.
  - Tope nuevo de velocidad angular: 90°/s.
  - Se respeta `GameFeel.ShakeScale` (opción del jugador; 0 = sin kicks).

## 3. Beats por robot

Tiempos en el reloj de `DisarmMotion` (sin cambios: Reach 0,18 · Strain 0,16 · Yank 0,18 · WindUp 0,14 · Strike 0,09 · Impact 0,08 · Recover 0,34 → 1,167 s).
- **Kick:** pitch + = hacia abajo, roll + = a la derecha. Valores en grados de pico.
- **Grupos de mixer:** los de `MainAudioMixer`. Los del jugador van por `AudioUtility.AudioGroups`; los del robot, por su AudioSource 3D.

### 3.1 T-07 — total 1,20 s reales; sin control 0 s (movimiento y vista libres); acciones bloqueadas 0,86 s reales (0,83 de reloj)

| Beat | t (s) | Robot (capa de reacción) | Brazos del jugador | Cámara (kick pico) | Hit-stop | VFX | Sonido → grupo |
|---|---|---|---|---|---|---|---|
| 1 Agarre | 0,00–0,18 | Arrastrado hacia la mano (§2.1). Chest +8° hacia el jugador; Head gira 15° hacia la mano (la mira); UpperArm_L (cañón) se alza 10°, como si intentara apuntar | Mano izq. sube desde abajo abierta y cierra 0,15–0,18 s sobre Forearm_L. El brazo derecho con el arma actual baja 4 cm para dejar sitio | 0,00: pitch +0,4, roll +0,3 (hoy 0,6/−0,4/0,6) | — | — | 0,17 `IR_Mount_Clamp` 0,7 → Impact (2D) |
| 2 Forcejeo | 0,18–0,34 | 3 tirones servo del brazo del cañón: UpperArm_L −6°/+4°/−8°, cada uno retenido 2 frames y con salto. Chest contra-rota ±3° de yaw; Hips −3 cm (planta los pies); Head tiembla ±2°. La unión sigue a la mano (≤3 cm) | Agarre cerrado, temblor de 1,2 cm (existente), tira un 12 % hacia el pullPoint; el codo pasa de ~95° a ~110° | 0,18: pitch +0,5, roll +0,6; trauma 0,10 (radio 4 m, 0,16 s) | — | `RobotHitFeedback.sparks` Emit(3) a 0,26 y 0,31 en la unión | 0,18–0,34 `IR2_Capsule_Creak` 0,45, fundido de salida 0,05 s a 0,34 → EnemyMovement (3D) |
| 3 Arranque ★1 | 0,34 (–0,44) | La pieza cede: Forearm_L queda vacío e inerte (UpperArm_L cae −35° con un rebote, 0,12 s). Chest +4° hacia el jugador durante 1 frame, luego −12° atrás con sobreimpulso (0,34–0,44); Hips −4 cm; Head con glitch de 20° (2 frames retenidos). Pulso emisivo local en el muñón (0,09 s) en lugar del tinte rojo de cuerpo entero | La mano tira del proxy hacia el pullPoint (EaseIn existente); el proxy va pegado al agarre | pitch −1,8, yaw −0,5, roll +1,2, z −2,4 cm | Hold local 2 frames (0,033 s), sin global | `ExtractionArcFx` exactamente a 0,34 (extraDelay 0,02 → 0) desde la unión real | `IR_Hangar_Tear` 1,0 + `IR_Hangar_Spark` 0,6 → Impact. Se mantiene `Damage_Tick2` (confirmación de la plantilla) |
| 4 Traspaso + carga | 0,44–0,66 | Se rehace: Chest vuelve a +4°; Head mira al jugador; UpperArm_R se alza 15° en guardia. **Anticipación:** ve venir el puño | 0,44–0,52 lanza el proxy al brazo derecho; 0,52 encaja el arma real; la mano izq. cierra el puño y retrocede al windUpPoint | 0,52: roll −0,8, pitch −0,5 (el cuerpo carga) | — | — | 0,52 `IR_Mount_Lock` 0,6 → Pickup. Opcional: `IR2_Impact_PreSuck` desde 0,33 s del clip, a 0,44 → termina en el golpe, 0,35 → Impact |
| 5 Puñetazo ★2 / muerte | 0,66–0,75 (contacto 0,732) | 0,732: Chest −18° (se dobla), Head −25°. `Complete()` en el mismo frame (muerte por el flujo existente). Empuje de 0,9 m en 0,32 s (hoy 1,4 m) | Puño a `min(0,80, profundidad del pecho + 0,05)` m: toca el pecho | pitch +2,8, yaw +0,4, roll −1,4, z +3,2 cm | **Global 0,033 s @0,10** | `RobotHitFeedback.sparks` Emit(6) en el punto del puño (hoy en raíz + 1,2 m) | 0,732 `IR_Fist_HeavyImpact` 1,0 → Impact (hoy sin sonido) |
| 6 Derrumbe + salida | 0,75–1,20 | Muerto: el empuje termina (se para en muros) y la explosión existente llega a 1,03 | Impact mantiene el puño en el contacto 0,08 s; Recover se relaja y sale por abajo | Asentamiento del resorte | — | Explosión existente | 1,03 `Hoverbot_Death` (existente) → EnemyDeath |

### 3.2 SawBot — total 1,20 s reales; sin control 0 s; acciones bloqueadas 0,86 s reales

Mismo reloj, golpes y hit-stop que el T-07. Huesos: `Body`, brazo de la sierra (`UpperArm/Forearm/GuideBar`), `Wheel_L/R`, cadena. Los ejes se toman de la raíz, no de `Presentation` (girado 180°).

| Beat | t (s) | Robot | Brazos | Cámara | Hit-stop | VFX | Sonido → grupo |
|---|---|---|---|---|---|---|---|
| 1 Agarre | 0,00–0,18 | **La sierra sigue en el robot.** Arrastrado hacia la mano; las ruedas giran hacia atrás a 0,6 × la velocidad del arrastre (frenan y patinan); Body +8° hacia el jugador | Como el T-07, sobre la unión de la sierra | pitch +0,4, roll +0,3 | — | — | 0,17 `IR_Mount_Clamp` 0,7 → Impact |
| 2 Forcejeo | 0,18–0,34 | El brazo de la sierra tira (−6/+4/−8°, retenidos); la cadena vibra al doble de velocidad; Body ±3° de roll | Agarre y temblor | pitch +0,5, roll +0,6; trauma 0,10 | — | Chispas Emit(3) ×2 | `SawMotor_Attack_Loop` 0,5 con tono de 1,0 a 1,25 → EnemyAttack (3D) |
| 3 Arranque ★1 | 0,34 | El motor muere en seco (cadena parada). Body −14° atrás con sobreimpulso; las ruedas rebotan 2 cm en la pose visual (la raíz no despega); pulso local en el hombro | Proxy de la sierra en la mano | pitch −1,8, roll +1,2, z −2,4 cm | Hold local 2 frames | `ExtractionArcFx` | Corte del motor (fundido 0,03 s) + `IR_Hangar_Tear` 1,0 + `IR_Slide_Deflect` 0,7 → Impact |
| 4 Traspaso + carga | 0,44–0,66 | Gira 6° hacia el jugador; el brazo sano en guardia (anticipación) | Sierra al brazo derecho a 0,52; puño | roll −0,8, pitch −0,5 | — | — | 0,52 `IR_Mount_Lock` 0,6 → Pickup |
| 5 Puñetazo ★2 / muerte | 0,732 | Body −20°; `Complete()`; empuje de 0,9 m en 0,32 s | Puño a la cúpula | pitch +2,8, roll −1,4, z +3,2 cm | Global 0,033 s @0,10; se anula el de `HeavyDeathFeel` | Chispas Emit(6) en el puño | `IR_Fist_HeavyImpact` 1,0 → Impact |
| 6 Derrumbe + salida | 0,75–1,20 | `DeathDisarmed` existente, con las ruedas en el suelo | La mano sale | Asentamiento | — | Muerte existente | Muerte existente → EnemyDeath |

### 3.3 Big Bot (requiere aprobación) — 1,50 s de reloj + 0,082 s de hit-stop = **1,58 s sin control** (hoy 2,30)

**Idea:** el jugador no se sube a la cima. Se queda **colgado junto al hombro del cañón**, con la cámara a 0,45 m de `CannonPivot` (+0,10 m de altura, por el lado del que viene). Así la mano toca la unión real (hoy la mano queda a 2,7–3,3 m). No hay aim override: **apunta el arma, no la cámara**.

La mano usa el perfil `RipOnly` de `DisarmMotion`, sin puño: Reach 0,28–0,46, Strain 0,46–0,62, Yank 0,62–0,80, traspaso 0,80 y Recover 0,80–1,05.

| Beat | t (s) | Robot | Jugador (brazos y cámara) | Kick pico | Hit-stop | VFX | Sonido → grupo |
|---|---|---|---|---|---|---|---|
| 1 Salto lateral | 0,00–0,40 | La cabeza sigue al jugador 20° (saltos de servo); el brazo libre sube 10° | Arco de 0,40 s (`BeginScriptedMove`, arco 0,9 m) al punto de agarre lateral; **la vista sigue siendo del jugador**; la mano sube en el aire desde 0,28 | Despegue: pitch +0,6 | — | — | 0,00 `Jump` (plantilla) → PlayerMovement |
| 2 Aterriza y agarra | 0,40–0,62 | El torso absorbe el peso (−4° y rebote). De 0,46 a 0,62 se sacude: roll ±5° en 2 pulsos; el brazo del cañón tira | La mano cierra en `CannonPivot` a 0,46 (≤0,05 m). La cámara acompaña con roll = 30 % del roll del torso (≤1,5°), sin yaw ni pitch forzados | Aterrizaje: pitch +1,5 (hoy 3/0/1,5) | — | `breakSparks` Emit(4) a 0,55 | 0,40 `IR_Hangar_FootMetal` 0,9 → PlayerMovement; 0,46 `IR_Mount_Clamp` → Impact; 0,46–0,62 `IR_Hangar_Hydraulic` 0,5 con fundido → EnemyMovement |
| 3 Arranque ★1 | 0,62 | El cañón cede y pasa al proxy (hoy se oculta de golpe a yank + 0,06). Pulso local en el hombro; torso −8° al lado contrario; glitch de cabeza | Yank: la mano tira del cañón | pitch −2,0, roll +1,5 | Global 0,04 s @0,10 (+0,036 s) | `ExtractionArcFx` + `breakSparks` Emit(18) (el mismo de `DetachCannon`) | `IR_Hangar_Tear` + `IR_Hangar_CannonArm` 0,8 → Impact |
| 4 Montar el cañón | 0,62–0,90 | El brazo libre sube 40–70° (amenaza: anticipa el disparo); el torso gira 8° hacia el jugador | 0,80: el cañón encaja en el brazo derecho (**cambio de arma diferido**: hoy se hace en E y el cañón sale duplicado). El arma se orienta al núcleo con un offset ≤35° en 0,12 s; la cámara no gira | roll −0,6 | — | — | 0,80 `IR_Mount_Lock` → Pickup |
| 5 Disparo ★2 / muerte | 0,95 | `Complete()` en el mismo frame; el torso cae −15°; `BigBotDestruction.BeginDeath` existente | Disparo a quemarropa; muzzle flash existente | Retroceso: pitch −3,0, roll +1,0, z −4 cm; trauma 0,4 | Global 0,05 s @0,08 (+0,046 s) | Muzzle flash del arma + `breakSparks` Emit(14) (existentes) | `IR_BigBot_Blast` → WeaponShoot (hoy `Blaster_Shot`); `IR2_Impact_SubBoom` 0,5 → Impact |
| 6 Salto atrás | 1,05–1,50 | Colapso existente (su sonido a deathAge 1,25) | Arco atrás de 0,45 s (altura 0,6 m) al inicio si está libre; si no, punto a 2 m comprobado con CapsuleCast y `NavMesh.SamplePosition`. Control a 1,50 | Aterrizaje: pitch +1,2 | — | — | 1,50 `Land` (plantilla) → PlayerMovement (hoy `Jetpack_Use`) |

Además, en el Big Bot:
- La barra de vida en mundo se oculta durante la secuencia.
- Los colliders del robot se ignoran solo durante los dos arcos, no en toda la secuencia.
- La cámara nunca supera 1,5°/frame sin ratón (hoy 17°/frame).

## 4. Parámetros y archivos (sin código)

### 4.1 Robot ejecutado — `Assets/IronRequiem/Scripts/Enemies/Execution/` (nuevo)

| Archivo propuesto | Responsabilidad | Parámetros |
|---|---|---|
| `ExecutionReactionProfile.cs` (ScriptableObject de solo lectura, uno por tipo) | Ángulos por beat y huesos | T-07: los de §3.1. SawBot: §3.2. Big Bot: §3.3. Resorte: rigidez 22, amortiguación 0,5. Frames retenidos por tirón: 2. Pico máximo por hueso 25°/frame |
| `ExecutionReaction.cs` (orden 160, después de `RobotMotionAccents`) | Capa aditiva en LateUpdate; restaura en Update (mismo patrón que `RobotHitFeedback` y `RobotMotionAccents`); sin Animator ni clips nuevos | Escucha los beats a través de `EnemyExecutionSource`, sin conocer al jugador |
| `ExecutionPullIn.cs` | Arrastre hacia la mano (§2.1) | Profundidad objetivo de la unión 0,60 m; arrastre máximo 0,80 m; duración 0,18 s (entrada-salida); radio de sonda 0,35 m; margen con el jugador 0,25 m |
| `ExecutionWeaponDetach.cs` | Oculta el arma del robot en el arranque y publica su pose para el proxy | Ocultar en YankTime; mezcla con el proxy 0,05 s |
| `ExecutionLocalFlash.cs` | Pulso emisivo local (MaterialPropertyBlock) y supresión del tinte rojo de cuerpo entero durante la ejecución | Pulso 0,09 s, intensidad 2,5 (HDR) |
| `ExecutionRobotAudio.cs` | Creak, motor e hidráulico en el AudioSource del robot | Clips y volúmenes de §3; fundido de salida 0,03–0,05 s |

Cambios mínimos en archivos existentes:
- **`EnemyExecutionSource.cs`:** evento `BeatReached(beat)`, para que el robot no dependa del jugador.
- **`SawBotWeaponSource.cs`:** oculta la sierra en el arranque, no en la reserva. `SetExecutionReserved` se mantiene en E.
- **`SawBotCombat`:** bandera para parar la cadena en el arranque.
- **`HeavyDeathFeel.cs`:** no hace hit-stop si la muerte es de ejecución (presupuesto §2.3).
- **`ExecutionShove.cs`:** sin cambios; se usa su sobrecarga v4 con 0,9 m y 0,32 s.
- **Prefabs:** añadir los componentes en variantes de `T07_Current_Repaired`, `SawBot_Current` y `BigBot_Current`, con herramienta de Editor y respaldo.

### 4.2 Jugador

| Archivo | Cambio | Parámetros |
|---|---|---|
| `Player/DisarmMotion.cs` | El agarre apunta al socket real con la profundidad recortada (`maxGripReachV3` 0,55, sin cambio). Ventana de traspaso; hold local; puño a la profundidad del pecho; perfil `RipOnly` para el Big Bot. **Duraciones intactas** | `handoffStartV4` 0,44; `handoffEndV4` 0,52; `handoffPointV4` (0,22, −0,20, 0,42); `yankHoldFramesV4` 2; `strikeDepthV4` = min(0,80, pecho + 0,05) |
| `Player/ProtagonistHandsPresentation.cs` (`StepDisarm`) | Tabla de kicks de §3; el brazo derecho baja 4 cm en el Agarre; socket de agarre del proxy en la mano izquierda | Kicks en grados de pico de §3 (pasados ÷0,78) |
| `Player/StolenWeaponCarrier.cs` | `Blow` 1 en la unión real y `Blow` 2 en el puño; `ShowHeldWeapon` a WindUpTime (0,52); `ExecutionShove` v4 con 0,9 m/0,32 s; emitir beats; Big Bot con cambio diferido (aprobación) | — |
| `Player/BigBotExecutionSequence.cs` | Perch lateral, sin `SetAimOverride`, apuntado por el brazo, tiempos de §3.3, aterrizaje comprobado, ocultar la barra de vida | Salto 0,40 (arco 0,9); mano desde 0,28; disparo 0,95; retroceso 1,05–1,50 (arco 0,6); perch = CannonPivot + 0,45 m hacia el jugador + 0,10 m arriba; offset del arma ≤35° |
| `Player/DistanceCameraImpulse.cs` | Tope de velocidad angular del kick | `maxKickDegreesPerSecond` 90 |
| `VisualPass/GameFeel.cs` | Ámbito de presupuesto de ejecución: ignora hit-stops ajenos mientras está activo | — |
| `VisualPass/ExtractionArcFx.cs` | Dispara en YankTime exacto desde la unión real; sin hit-stop dentro del presupuesto | `extraDelay` 0 |
| **Nuevo** `Player/Execution/ExecutionDirector.cs` | Reloj único: lee los beats de `DisarmMotion` (o del Big Bot) y lanza una vez por beat el kick, el hit-stop, los sonidos y el evento al robot | — |
| **Nuevo** `Player/Execution/ExecutionReachResolver.cs` | Socket, banda A/B/C, cantidad de arrastre | Umbrales de banda 0,62 y 1,52 m |
| **Nuevo** `Player/Execution/ExecutionWeaponCarry.cs` | Proxy del arma de 0,34 a 0,52 (o de 0,62 a 0,80 en el Big Bot) | Arco de traspaso 0,08 s, altura 0,06 m |
| **Nuevo** `Player/Execution/ExecutionCueSet.cs` (ScriptableObject) | Por beat: clip, grupo, volumen, offset y fundido | Los de §3 |

## 5. Variantes por ángulo de entrada (opcional)

Ángulo = entre el frente del robot y la dirección hacia el jugador.
- **Frente (<60°):** el guion base.
- **Espalda (>120°):**
  - El robot no ve venir nada: la cabeza no mira la mano en el Agarre y gira 30° tarde, a 0,26 s (sorpresa).
  - El brazo del cañón se dobla hacia atrás por encima del hombro (UpperArm −30°).
  - El puño es un martillazo a la nuca (Head +25° hacia delante) y el robot cae de cara; el empuje va en la misma dirección.
- **Lado (60–120°):**
  - Si el arma está en el lado lejano, el arrastre gira el robot hasta 35° de yaw para presentar la unión. Es un giro causado por la mano, con la misma sonda contra muros.
  - Si está en el lado cercano, se usa el guion base con la inclinación lateral (roll) en lugar de pitch.

## 6. Requiere aprobación

1. **Reactivar la ejecución del Big Bot** (`executionEnabled = true`). Cambia una regla de juego vigente desde v31 y el test de CampaignTests «Big Bot nunca ejecutable».
2. **Big Bot: golpes y duraciones.**
   - El puñetazo al aire se sustituye por el arranque con impacto, así que hay 2 golpes: arranque y disparo con muerte.
   - La secuencia pasa de 2,30 a 1,50 s de reloj, con tiempos nuevos para salto, agarre, disparo y retroceso.
   - El cambio de arma se difiere al encaje, a 0,80 s.
3. **Recover de 0,34 a 0,24 s** en T-07/SawBot, solo para recuperar un hit-stop global en el arranque:
   - Arranque 0,033 s @0,15 y puño 0,05 s @0,08.
   - Total 1,141 s reales.
   - YankTime y StrikeTime no cambian.
4. **Banda C con paso del jugador:** micro-paso de ≤0,45 m en 0,12 s (`BeginScriptedMove`) durante el Agarre, en lugar del arma que vuela. Quita el control de movimiento durante 0,12 s.
5. **Luz flash (opcional):** luz puntual de 0,06 s en el disparo del Big Bot (rango 4 m, sin sombras), solo si el muzzle flash existente no lleva luz. Nada más necesita luz.

Decisión menor a confirmar: mover `ShowHeldWeapon` de 0,34 a 0,52 s. Sigue siendo posterior al arranque y la ventana de bloqueo no cambia.

## 7. Riesgos y cómo se probará

| Riesgo | Mitigación | Criterio medible (test PlayMode, mismo arnés que `ExecutionsF0Capture`, a 30/60/144 FPS) |
|---|---|---|
| El arrastre del T-07 desliza los pies del IK o los hace saltar | Arrastre con suavizado entrada-salida; Hips −3 cm; revisar `T07FootPlacement` en la vista lateral | Salto de pie ≤0,10 m/frame; E10 |
| El arrastre o el empuje meten al robot en un muro o lo sacan del NavMesh | SphereCast y `agent.Move`, como `ExecutionShove` | E10 en 3 sitios: abierto, robot de espaldas a un muro a 0,5 m, esquina |
| Ejes de hueso mal leídos (rig ×100 del T-07; `Presentation` del SawBot girado 180°) | Ejes de la raíz del robot; comprobar contra referencias de la malla | Dirección del Chest a 0,34 s hacia atrás respecto al jugador (signo del producto escalar) |
| Hit-stops apilados alargan la ejecución | Presupuesto §2.3 | E2 y E8; real ≤1,20 s también cuando muere otro enemigo en el mismo frame |
| Un proxy y el arma real visibles a la vez, o ninguno | Cambio en el mismo frame | E5, contando renderers visibles en cada frame |
| Bandas mal calibradas para la campaña (distancia típica en E desconocida) | Medir la distribución de d en Room00b–04 | Informe: % de casos en A, B y C; objetivo C <10 % |
| Mareo por kicks o roll | Topes y opción de temblor | E7; con `ShakeScale = 0`, desviación de cámara = 0° |
| FOV alto: la mano o el puño se salen del cuadro | Puntos en espacio de cámara con margen | E12 a FOV 90° y 110° |
| Big Bot: aterrizaje en zona no transitable o atravesando la malla | Colliders ignorados solo en los arcos; CapsuleCast | Ningún solape de la cápsula del jugador con el robot al terminar; E2 |
| Restos de muerte que vuelan fuera del cuadro (existente) | Fuera de alcance de F1 | Anotado para el pase de muerte |
| Clips elegidos por nombre sin escucharlos | Escucha del usuario en F2 | Lista de cues por beat con ±1 frame de error (E1 y tabla §3) |
| Tests previos: ArmsFixPassTests (a) espera la mano a ≤0,6 m y sin puño antes del arranque, 1 cambio después del arranque y bloqueos | La propuesta los conserva | Volver a pasar ArmsFixPassTests y CombatFollowup. Por regla del estándar, cada test nuevo debe fallar con el comportamiento F0 restaurado |

Revisión de F3 (según el estándar de animación de combate):
- Dos críticos ciegos (rúbrica de 5 criterios × 0–2) y A/B ciego contra `before_v2`, con hojas del mismo título desde la vista del jugador y la lateral.
- Verificar cada afirmación sobre frames a resolución completa.
- Prueba manual del usuario con teclado y ratón.

## 8. Skills aplicadas

Prioridad: AGENTS.md > iron-requiem-* > unity:* > gamedev:* > genéricas.

| Skill | Qué se aplicó |
|---|---|
| AGENTS.md | Solo se escribió en `Docs/IronRequiem/Executions/`. Cambios futuros mediante variantes de prefab, Editor y respaldo; sin paquetes; muerte por el flujo existente |
| iron-requiem-fps | Un solo dueño del movimiento (scripted move de `AdvancedPlayerMovement`); cámara FPS sin sustituir; arma por `PlayerWeaponsManager`; movimiento y vista libres en T-07/SawBot |
| iron-requiem-enemy-ai | IA, vida y daño intactos; capa sobre los 17 huesos reales del T-07 y los del SawBot, sin inventar huesos ni clips; contrato por eventos (`EnemyExecutionSource.BeatReached`) |
| iron-requiem-modular-weapons | Una sola transición del arma; el proxy reutiliza el módulo ya creado (sin segunda jerarquía); el arma nunca desaparece |
| game-dev-combat-animation | Estándar numerado antes de las claves (E1–E13, test o revisado); anticipación, contacto en el frame de la regla y recuperación; manos siempre en pose; sin brazo bloqueado (codo ≥20°); alcance sin estirar; medir antes de mirar; dos cámaras; tests que fallan con el bug restaurado; críticos ciegos y A/B. Ignorado lo específico de Thornvigil (Tripo, keydesign.mjs) |
| gamedev:game-feel | Niveles: arranque medio, muerte grande pero recortada por el presupuesto; hit-stop en tiempo real donde manda el más nuevo; temblor por trauma; suavizado con sobreimpulso; una sola sacudida por impacto; opciones de accesibilidad existentes |
| gamedev:fps-shooter | Sin giro de cámara forzado; legibilidad con FOV horizontal alto; telegrafía PvE (el robot anticipa el puño, el Big Bot amenaza antes del disparo) |
| design-action-combat (solo principios) | Cada verbo con inicio, ventana activa y recuperación; resultado autoritativo (impactos de `Blow` y `Complete`) con el visual detrás; tests deterministas por FPS |
| create-game-vfx (solo principios) | Por efecto: disparador, dueño, duración, límite de emisión y limpieza; sistemas existentes y agrupados; sin luces nuevas; el pulso local sustituye al flash de cuerpo entero |
| iron-requiem-performance (leída) | Mismo presupuesto de partículas y luces que F0, medido; sin asignaciones nuevas por frame en la capa de reacción |
