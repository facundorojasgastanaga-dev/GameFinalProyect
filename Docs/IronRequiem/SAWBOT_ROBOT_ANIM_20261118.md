# Animación de robot para el SawBot (18/11/2026)

Es el tercer enemigo, tras el T-07 Mech y el T-07 de campaña. Mismo estándar R1–R6, adaptado a un robot con ruedas y dos brazos sierra. Todo está en una variante y en escenas de prueba. No se tocó el juego, no se hicieron commits y no se gastaron créditos.

## Rig y combate (medidos, `Reports/SawBotRobotAnim/inspect-log.txt`)
- **Rig:** `Presentation` (Animator, girado 180° dentro del prefab y con escala 2) → `Rig/Body` → `UpperArm_L/R` → `Forearm_L/R` → `GuideBar` y `Chain`, más `Wheel_L/R`.
  - El frente real es +Z del prefab, así que la herramienta toma los ejes de la raíz del robot y no los de `Presentation`.
- **Controller** `SawBot_SawPolish`:
  - IdleDrive (MoveSpeed, con velocidad DriveSpeed).
  - Attack (ContactPrepare de 0,25 s, a `AttackSpeed = 0,25 / preparation`, es decir, 0,45 s).
  - SawHold, en bucle hasta CancelAttack.
  - Capa aditiva HitReaction (Hit).
  - Tres muertes.
- **Código:** `SawBotCombat` lanza `OnDamaged` por su cuenta, así que el SawBot ya reaccionaba al golpe y no necesita disparador. También mueve las ruedas y la cadena en LateUpdate. Las cuchillas (`SawContactBase/Tip`) hacen el daño.

## Clips nuevos (`Assets/IronRequiem/Art/Enemies/SawBotRobotAnim/Clips/`, 60 fps)
Las **poses clave salen de los clips antiguos**: el inicio del ataque y la pose de corte coinciden con las originales, así que la geometría de contacto que usa el daño no cambia. Lo nuevo es el ritmo y el movimiento añadido encima.

| Clip | Duración | Qué hace |
|---|---|---|
| R_SawIdle | 2 s, bucle | Zumbido de servo sin respiración; la cúpula sensora barre en saltos (±8°); reasiento discreto; cada brazo hace por turnos una comprobación de servo |
| R_SawDrive | 1 s, bucle | Inclinación hacia la marcha (6°), traqueteo de transmisión, dos sacudidas de suelo por ciclo y brazos que responden con retraso |
| R_SawContactPrepare | 0,25 s exactos | 0–35 %: sierras atrás y arriba (servo) mientras el cuerpo se echa atrás; 35–50 %: pausa; 50–100 %: salto a la pose de corte con sobreimpulso y el cuerpo entrando |
| R_SawHold | 0,4 s, bucle | Mordida de las cadenas a 25 Hz en contrafase, dos empujes de la tracción por ciclo |
| R_SawHit | 0,32 s, aditivo | Retroceso con giro, tartamudeo de servos (fotogramas retenidos) y recalibración del sensor. El fotograma 0 es la referencia, así que el delta empieza en cero |

- **Sin cambios:** las muertes (`*_FloorFixed`), las ruedas y la cadena.
- **Controller** `SawBotRobot.controller`: copia de `SawBot_SawPolish` con los 5 clips reasignados.
- **Variante** `Prefabs/VisualPass/SawBotRobot/SawBot_RobotAnim_Test.prefab`: solo cambia el controller. No lleva `MechHitReactionTrigger`, porque el SawBot ya reacciona solo.

## Verificación
- **Compilación** sin errores.
- **Play automático** en los laboratorios `Scenes/SawBotRobotAnimLab_Before/After.unity`, con detección a 20 m y olvido a 1 s; el alcance de ataque de 3 m no se toca:
  - 0 errores en ambos.
  - La misma secuencia de estados y velocidades, fotograma a fotograma: patrulla a 9,5 m/s, Attack a 8,11 s, SawHold y Hit al recibir daño.
  - **Limitaciones de la captura:** en la fila de patrulla la cámara queda tapada por una pared, y la fila de ataque sale vacía porque el SawBot no usa el evento `onAttack`. Ninguna depende de los clips.
- **Vitrina** `Scenes/SawBotRobotAnimShowcase.unity`, con 5 columnas: delante el nuevo, detrás el actual. Preparar va a ×0,55, la velocidad del juego.
  - Comprobación en Play: se mueven 9 de 10 robots. El que no supera 1 cm es el SawHold antiguo (0,7 cm), que casi no se mueve; el nuevo llega a 3,9 cm. El golpe nuevo llega a 48 cm frente a 3,7 cm, y el reposo a 29 cm frente a 3 cm.
  - Unity vuelve solo a IntroMenu, sin cambios.
- **Hashes:** los 19 archivos protegidos del SawBot (`Backups/20261118-sawbot-robot-anim/`) siguen iguales, y también los del T-07 y el Mech.
- **Sin hacer:** prueba manual con teclado y ratón, revisor ciego y medición de coste.

## Uso
- `Tools > Iron Requiem > Visual Pass > SawBot robot anim (test) - Play SHOWCASE (all clips)`
- `... Play lab AFTER (manual)` para jugar contra él, y `... Play lab BEFORE (manual)` para comparar.

## Archivos
- **Nuevos:**
  - `Editor/VisualPass/VisualPassSawBotRobotAnim.cs`
  - `Art/Enemies/SawBotRobotAnim/`
  - `Prefabs/VisualPass/SawBotRobot/`
  - las escenas `SawBotRobotAnim*`
  - `Tools/IronRequiem/Reports/SawBotRobotAnim/`
- **Herramientas compartidas** (respaldo en `Backups/20261118-sawbot-robot-anim/`):
  - Laboratorios sin el disparador del T-07 y sin forzar el alcance para robots cuerpo a cuerpo.
  - Captura que busca cualquier robot.
  - Vitrina con velocidad por columna.
  - `StripToVisual` común.

## Integración propuesta (requiere aprobación)
Asignar `SawBotRobot.controller` en `SawBot_Current.prefab`, o en una variante de campaña. Antes conviene probarlo a mano.
