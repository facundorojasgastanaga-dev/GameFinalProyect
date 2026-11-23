# Ejecuciones v02 — F0 diagnóstico (22/11/2026)

Solo lectura del juego. Se añadió únicamente la prueba de captura `Assets/IronRequiem/Tests/PlayMode/ExecutionsF0Capture.cs`. No se guardó ninguna escena.

## Cómo se midió
- **Escenario:** laboratorio `MainScene_VisualLab` con el jugador de campaña, plataforma de prueba y prefabs de campaña: `T07_Current_Repaired`, `SawBot_Current` y `BigBot_Current`.
- **Ejecución:** con E real (teclado virtual del Input System), a 60 Hz fijos.
- **Muestreo:** al final de cada frame, después de LateUpdate, así que incluye las reacciones, los acentos, los kicks de cámara y el IK de manos.
- **Capturas:** cada 2 frames, desde la vista del jugador (cámara base + overlay FPS) y desde una cámara lateral de prueba.
- **Evidencia:** `Tools/IronRequiem/Reports/Executions/before_v2/<Robot>/` (`player/`, `side/`, `telemetry.csv`, `summary.txt`) y hojas de contacto `before_v2/<Robot>_{player,side}_sheet.png`.
- **Captura `before/`:** es la primera pasada, muestreada antes de LateUpdate. No muestra reacciones ni kicks. Solo sirve para la mano y la explosión.
- **Limitaciones:**
  - `floorGap` no es fiable, porque los bounds incluyen los LOD.
  - La escena de laboratorio es oscura.
  - En el Big Bot, el arnés desactiva `BigBotCombat`, así que su pose congelada puede deberse en parte al arnés.

## Hallazgo previo: Big Bot
**`BigBot_Current` tiene `executionEnabled = false`.** Es así desde v31 («Big Bot nunca ejecutable», CampaignTests), y nadie llama a `SetExecutionEnabled`. **Hoy el Big Bot no se puede ejecutar en la campaña.** La secuencia existe en código y se grabó activándola solo en la instancia de la prueba. Volver a activarla cambia la regla de juego y requiere aprobación.

## T-07 (`DisarmMotion`: Reach 0,18 · Strain 0,16 · Yank 0,18 · WindUp 0,14 · Strike 0,09 · Impact 0,08 · Recover 0,34)

| t (s) | Mano | Robot | Cámara / feel / sonido |
|---|---|---|---|
| 0–0,18 | sube desde abajo | quieto (0,00°/frame) | kick ≤0,2° |
| 0,18–0,34 | «agarra» a 0,67–0,72 m de la unión | quieto | — |
| 0,367 | tirón (golpe 1) | tinte rojo en todo el cuerpo, torso ≤0,4°/frame | `Damage_Tick2` (plantilla), sin hit-stop |
| 0,52–0,73 | puño, preparación de 0,13 s | quieto | — |
| 0,75 | golpe 2, muerte | empuje de 1,4 m en 0,32 s (≈13 m/s desde parado), pose que salta (hasta 108° en 3 frames) | **sin sonido de golpe**, sin hit-stop, kick ≤0,7°/frame |
| 1,03–1,33 | la mano sale | flujo de muerte/explosión existente | `Hoverbot_Death` (plantilla) |

- **Robot:** congelado en 30 de 44 frames vivos. La única reacción es un tirón de torso de ~2,5° al arrancar. No hay forcejeo, anticipación ni pieza que ceda.
- **Mano:** nunca llega a la unión. `maxGripReachV3` = 0,55 m en espacio de cámara y el robot está a ≥1,2 m, así que el cañón se «arranca» en el aire.
- **Peso:** ningún hit-stop y kicks de cámara casi invisibles. El golpe 2 no tiene sonido propio.
- **Tiempos:**
  - Mano en pantalla 1,17 s.
  - Acciones bloqueadas 0,82 s.
  - Movimiento y vista libres todo el tiempo.

## SawBot (misma línea de tiempo de manos)
- **Sierra:** `SawBotWeaponSource` la oculta en E (0,00 s), pero la mano tira a 0,34 s. Son **0,37 s sin sierra en ningún sitio**; la sierra del jugador aparece en 0,367 s.
- **Mano:** ≥0,51 m de la unión.
- **Robot:**
  - Congelado en 32 de 44 frames vivos; el tirón llega como máximo a 0,9°/frame.
  - Recibe un empuje de 1,4 m.
  - `DeathDisarmed` se asienta hacia 2,4 s.
- **Peso:** un solo frame de hit-stop (0,06) en la muerte, de `HeavyDeathFeel`. Mismos sonidos de plantilla y sin sonido de golpe.
- **Tiempos:** acciones bloqueadas 0,83 s; movimiento libre.

## Big Bot (`BigBotExecutionSequence`: leap 0,45 · rip 0,70 · aim 0,35 · recoil 0,25 · dismount 0,55)
- **Duración y control:** **2,30 s sin control** (movimiento por script + aim override). Supera el límite pedido de 2,0 s. La muerte llega a 1,50 s y quedan 0,8 s bloqueados después.
- **Golpe de mano:**
  - El puñetazo no toca nada (0 impactos).
  - La mano queda a 2,7–3,3 m del cañón, porque el agarre se recorta a 0,55 m de la cámara.
- **Cañón:** se oculta de golpe a yank + 0,06 s, sin fundido ni pieza que se suelte.
- **Cámara:** latigazo de 17°/frame al pasar el aim override del cañón al núcleo (Rip→Aim); >1°/frame durante 49 frames.
- **Disparo:**
  - `Blaster_Shot` (plantilla), 2 frames de hit-stop.
  - Al bajar suena `Jetpack_Use` (plantilla) y al derrumbarse `Hoverbot_Death` (plantilla).
  - No hay sonido de salto, agarre ni tirón.
- **Robot:**
  - Quieto 1,5 s (ver la limitación del arnés).
  - Sus colliders se ignoran durante toda la secuencia, lo que puede hacer que el jugador atraviese la malla al subir y bajar. No está medido.

## Conclusión de F0
En las tres ejecuciones falta lo mismo:
1. Reacción del robot por beats (forcejeo, pieza que cede, golpe y derrumbe).
2. Contacto real de la mano con la unión.
3. Peso en los impactos: hit-stop corto, kick legible y sonido propio.
4. El arma que pasa de la mano del robot a la del jugador sin desaparecer.

En el Big Bot sobran además el tiempo sin control y el giro forzado de cámara.
