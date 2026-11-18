# Retrato animado UNIDAD 01 — v01

Fecha: 16 de noviembre de 2026, America/La_Paz.

## Resultado

El panel existente conserva su jerarquía y distribución. Solo el rostro procedural se sustituye por un `UnityEngine.UI.Image` derivado, que utiliza los ocho PNG suministrados. No se editaron escenas de producción, prefabs, menús, controles ni scripts de gameplay.

Los PNG originales se copiaron sin modificar sus bytes a `Assets/IronRequiem/Art/UI/RobotPortrait/v01/`. Unity los importó como Sprite, Single, alpha del archivo, transparencia, pivote central, Full Rect, sin mipmaps, tamaño máximo predeterminado 512, Bilinear y Clamp. Se usa RGBA sin compresión para conservar transparencia y detalles a este tamaño. Las poses comparten el mismo lienzo, pivote y RectTransform; no se recortan por silueta.

Una referencia ScriptableObject en Resources precarga los ocho sprites una vez por dominio. No hay cargas por frame, instancias de materiales ni creación de objetos en la actualización del retrato. Raycast Target está desactivado. Solo se asigna un sprite cuando cambia la pose. Los pulsos utilizan color de vértices y el impacto desplaza localmente el mismo RectTransform.

## Reacciones

Prioridad: muerte > impacto > crítico > OVERDRIVE > ataque > reposo.

- Reposo: neutral, izquierda, neutral, derecha, con pausas independientes de 1–2 segundos. Usa su propio generador aleatorio y no altera el RNG de gameplay.
- Daño: Health.OnDamaged, 0,2 segundos y movimiento local de hasta 1,5 unidades de UI.
- Crítico: salud estrictamente inferior al 25 %, persistente, con leve variación de brillo. La curación actualiza el estado en la siguiente actualización.
- Ataque: WeaponController.OnShoot, emitido por disparos aceptados, o LastHitCount de una nueva consulta real de ProtagonistMelee. Una pulsación sin disparo o un golpe fallido no bastan. Expresión de 0,14 segundos, con intervalo de recuperación para evitar reinicios continuos.
- Ejecución: evento StolenWeaponCarrier.Executed, expresión de ataque durante 0,5 segundos.
- OVERDRIVE: estado real del módulo sostenido y CannonOverdrive.IsArmed. Con salud crítica mantiene el sprite agrietado y aplica el pulso a ese retrato.
- Muerte: Health.OnDie y comprobación de salud; permanece apagado incluso ante una curación tardía. Un nuevo Bind al jugador reaparecido o ResetReactions restablece el retrato.
- Pausa: reloj, pulso, pose y movimiento congelados cuando Time.timeScale es cero.

## Archivos de implementación

Modificados:
- `Assets/IronRequiem/Scripts/UI/RetroStatusBar.cs`: conecta el retrato al propietario en Start; retira exclusivamente el dibujo/reacción anterior del rostro.
- `Assets/IronRequiem/Scripts/UI/RobotPortraitGraphic.cs`: presentación, prioridad, secuencias y suscripciones; conserva su meta/GUID original.

Añadidos, junto con sus metadatos:
- `Assets/IronRequiem/Scripts/UI/RobotPortraitSprites.cs`.
- `Assets/IronRequiem/Resources/IronRequiem/RobotPortraitV01.asset`.
- `Assets/IronRequiem/Art/UI/RobotPortrait/v01/01_neutral.png`.
- `Assets/IronRequiem/Art/UI/RobotPortrait/v01/02_mirada_izquierda.png`.
- `Assets/IronRequiem/Art/UI/RobotPortrait/v01/03_mirada_derecha.png`.
- `Assets/IronRequiem/Art/UI/RobotPortrait/v01/04_impacto.png`.
- `Assets/IronRequiem/Art/UI/RobotPortrait/v01/05_critico.png`.
- `Assets/IronRequiem/Art/UI/RobotPortrait/v01/06_ataque.png`.
- `Assets/IronRequiem/Art/UI/RobotPortrait/v01/07_overdrive.png`.
- `Assets/IronRequiem/Art/UI/RobotPortrait/v01/08_apagado.png`.
- `Assets/IronRequiem/Scenes/RobotPortraitLab.unity`: laboratorio aislado con copia del jugador actual, gestores existentes y el mismo RetroStatusBar. No se añadió a Build Settings.
- `Assets/IronRequiem/Editor/RobotPortraitAuthoring.cs`: importación y laboratorio mediante APIs del Editor, guardando solamente el asset propio.
- `Assets/IronRequiem/Editor/Tests/RobotPortraitTestRunner.cs`: pruebas filtradas y restauración del punto de entrada del Editor.
- `Assets/IronRequiem/Tests/PlayMode/RobotPortraitTests.cs`.
- Este informe y evidencias en `Tools/IronRequiem/Reports/RobotPortrait/`.

Se utilizaron las colas locales existentes de auditoría y comandos. No se instalaron paquetes ni se controló el escritorio.

## Pruebas ejecutadas

Unity 6000.5.5f1, PlayMode en el Editor ya abierto. Resultado final: **4 pruebas aprobadas, 0 fallidas**, finalizado a las 01:22:49, hora de Bolivia. Resultado completo: `Tools/IronRequiem/Reports/RobotPortrait/playmode.xml`.

1. Importación de los ocho sprites, alpha real de los PNG, resolución importada, pivote, secuencia de reposo, pausas y referencias estables.
2. Daño, movimiento local, pausa, reanudación, crítico, curación, muerte persistente y enlace a una nueva Health de reaparición.
3. Disparo aceptado mediante HandleShootInputs, supresión de reinicios rápidos, evento de ejecución durante 0,5 segundos, activación real de OVERDRIVE drenando CannonAmmo, prioridades impacto/crítico/OVERDRIVE y muerte.
4. Consulta real de colisión del puño: daño al objetivo y reacción; golpe fallido sin reacción.

Las pruebas del laboratorio llaman APIs del juego. El puño se inicia mediante reflexión sobre sus métodos existentes para ejecutar su consulta de colisión sin automatizar controles. La prueba de ejecución utiliza NotifyRip; no verifica una ejecución completa contra un enemigo. El módulo se equipa mediante Equip y la prueba inyecta únicamente el vínculo de propiedad Held del carrier para aislar la presentación; no verifica el flujo completo de recogida. La prueba de armas rápidas incluye emisión repetida del evento real de disparo, además de un disparo aceptado por el arma.

Se guardaron **33 capturas reales de la cámara de Unity**, a 1280×720, 1920×1080 y 2560×1440. Cada pose y las combinaciones importantes tienen capturas en las tres resoluciones. También se comprobaron los límites del RectTransform dentro de cada destino de render. Son renders de cámara a esas resoluciones, no capturas de una ventana redimensionada manualmente ni de un Player compilado.

Ejemplos:
- `neutral-720p.png`, `neutral-1080p.png`, `neutral-1440p.png`.
- `impact.png`, `critical-1080p.png`, `attack.png`, `offline.png`, `respawn.png`.
- `overdrive.png`, `critical-overdrive.png`, `melee-confirmed.png` y sus variantes `-720p` / `-1440p`.

Inspección visual realizada sobre neutral a 1080p, impacto a 720p y crítico + OVERDRIVE a 1440p: rostro visible, sin fondo opaco añadido, sin recortes del panel ni desplazamiento permanente.

Medición acotada: **0 bytes de asignación administrada en 1.000 llamadas calentadas a LateUpdate del retrato en reposo**, en el Editor. Evidencia: `allocation.txt`. No representa asignaciones totales de la partida, CPU/GPU, draw calls ni FPS del Player.

La auditoría final registra cero errores de compilación capturados y cero scripts faltantes en la escena restaurada. El Editor quedó fuera de Play Mode, IntroMenu sin cambios pendientes y playModeStartScene restaurado a IntroMenu. Evidencia: `editor-restoration.txt`.

Los primeros intentos del runner quedaron desviados por el arranque forzado a IntroMenu. Se corrigió suspendiendo ese override durante las pruebas y restaurándolo después. El primer fallo de la prueba del módulo fue un Rigidbody ausente en el fixture, corregido dentro del laboratorio. Las guardas de escenas modificadas no guardaron escenas del usuario. Los errores anteriores de diagnóstico permanecen en los logs; no se borró la Console para ocultarlos.

## Preservación y pendiente de configuración

Respaldo de ambos scripts originales y sus metas: `Tools/IronRequiem/Backups/20261116-RobotPortrait/`.

La comparación final comprobó 534 archivos protegidos: escenas y prefabs preexistentes, scripts base de FPS, ProjectSettings y manifest/packages-lock. **533 conservan exactamente el hash inicial**; los ocho PNG copiados coinciden con sus originales. No cambiaron las versiones de paquetes.

**Excepción pendiente: ProjectSettings/ProjectSettings.asset cambió de hash durante esta sesión**, con marca de escritura coincidente con la primera importación. La primera versión del importador utilizó AssetDatabase.SaveAssets; se corrigió a SaveAssetIfDirty para guardar únicamente el banco de retratos. Es posible que el guardado global persistiera configuración que el Editor ya tenía en memoria; no hay evidencia suficiente para identificar el cambio exacto.

El hash inicial se registró, pero no se copió el contenido inicial de este archivo porque la tarea no preveía editar configuración. No se reconstruyó su contenido a partir de conjeturas ni se sustituyó por un respaldo antiguo. Su contenido final coincide byte por byte con el respaldo previo `20261115-hands-hud-campaign/ProjectSettings/ProjectSettings.asset`, pero eso no demuestra que coincida con el estado al comenzar esta sesión.

Hashes y copia final se conservan en `protected-changes.json` y `Backups/20261116-RobotPortrait/ProjectSettings.after.asset`. No se declara que ProjectSettings quedó intacto. Esta diferencia requiere revisión si el archivo inicial contenía un ajuste reciente que debía conservarse.

No se ejecutó un Player compilado, una sesión manual de controles, una recogida/ejecución completa ni un benchmark global. Esas verificaciones permanecen pendientes; no se atribuyen al resultado de las cuatro pruebas de presentación.
