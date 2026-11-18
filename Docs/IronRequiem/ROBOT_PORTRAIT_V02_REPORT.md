# UNIDAD 01 — retrato v02

Implementado el 16 de noviembre de 2026 en IRON REQUIEM, Unity 6000.5.5f1.

## Resultado y alcance

El retrato existente utiliza los 23 PNG de `Docs/IronRequiem/Concepts/Player/HUD_Reactions_v02/`, copiados sin alterar sus bytes a `Assets/IronRequiem/Art/UI/RobotPortrait/v02/`.

Se amplió RobotPortraitGraphic y su banco de sprites. RetroStatusBar se reutiliza sin cambios: conserva Canvas, paneles, etiquetas, inventario y distribución. La integración existente de Bind enlaza Health, armas, módulos y ProtagonistMelee. No se modificaron scripts de gameplay, controles, modelos 3D, menús, URP, Input System ni paquetes. La versión v01 de los assets se conserva.

Cada PNG se importó como Sprite Single, Full Rect, pivote central, alpha del archivo y transparencia, sin mipmaps ni Read/Write, máximo 512. Se eligió RGBA sin compresión para conservar las grietas y el visor al tamaño real del panel. Los 23 sprites tienen el mismo rectángulo y pivote. No se reflejan ni se cambia la escala del rostro entre poses.

## Estados independientes

| Salud real | Nivel |
| --- | --- |
| >=75 % | d0 |
| >=50 % y <75 % | d1 |
| >=25 % y <50 % | d2 |
| >0 % y <25 % | d3 |
| <=0 % | apagado persistente |

La curación actualiza el nivel. Frente, izquierda, derecha, impacto y carga se seleccionan siempre dentro del nivel vigente. La muerte permanece hasta ResetReactions o Bind al propietario reaparecido; una curación tardía sobre Health ya muerta no la revive visualmente.

Prioridad: muerte, impacto temporal de 0,2 s, carga, ataque/ejecución y mirada normal. Daño y OVERDRIVE persisten por separado. Ataque y OVERDRIVE emplean sus PNG sanos únicamente en d0; en niveles dañados mantienen la pose con grietas y añaden brillo al visor. Un cambio de arma cancela la reacción transitoria de disparo, conservando daño y mirada.

La carga consulta IsCharging y ChargeLevel reales. Centrado utiliza la pose de carga del nivel; durante el giro mantiene la pose lateral del mismo nivel. El brillo cian aumenta con la carga. Soltar, cancelar, recibir daño, curarse y conectar un golpe restauran la combinación correcta. No se añadieron manos ni se alteró el melee.

## Mirada

PlayerCharacterController es la autoridad del yaw horizontal: rota la raíz del jugador y aplica pitch a PlayerCamera. El retrato lee ese yaw y utiliza PlayerCamera como identidad de la cámara de gameplay. Así excluye el bob, recoil y vibraciones locales de la cámara sin cambiar sus controles.

Se utiliza Mathf.DeltaAngle, velocidad angular firmada, suavizado exponencial e histéresis. No se consulta Mouse.delta en el código del retrato y se eliminó el recorrido aleatorio anterior. La detección de giro utiliza tiempo real, también durante cámara lenta; los tiempos de reacciones visuales utilizan tiempo de juego. Pausar congela la presentación y reinicia la muestra del giro.

Parámetros serializados en RobotPortraitGraphic:
- Entrada de mirada: 20 grados/segundo.
- Zona de salida: 7 grados/segundo.
- Suavizado: 0,055 s.
- Retorno al centro: 0,2 s, ajustable entre 0,15 y 0,25 s.
- Discontinuidad de posición: más de 1,5 m entre muestras.
- Discontinuidad angular: más de 75 grados en una muestra o 1.800 grados/segundo.
- Un frame superior a 0,25 s reinicia la muestra.

También reinicia al cambiar PlayerCamera, durante PlayerInputLock o movimiento de ejecución/scripted, y al enlazar/reaparecer el jugador. Los saltos pequeños de posición sin señal propia no se distinguen universalmente del movimiento normal: existe ResetLookSample para notificaciones explícitas futuras. Se verificó el salto de posición grande utilizado en la prueba y el bloqueo cinemático real; no se afirma haber probado todos los puntos de teletransporte de la campaña.

## Overlay reutilizado

Un único RobotPortraitOverlay hijo del rostro dibuja el brillo y las chispas mediante una malla UI ligera, sobre el Canvas existente. Comparte el espacio cuadrado del sprite y sigue su movimiento de impacto.

La grieta anatómica izquierda permanece a la derecha de pantalla. Sus anclas cambian por pose; d1 ajusta la altura a su fisura menor. Las chispas se disparan al entrar en mirada derecha, sujetas a cooldown, y pueden repetirse únicamente al agotarlo si el giro continúa. La carga añade chispas en d2/d3 desde la misma rotura.

- d0: ninguna chispa de daño.
- d1: una chispa por ráfaga, cooldown 1,4 s.
- d2: cuatro, cooldown 0,8 s.
- d3: ocho, cooldown 0,48 s y leve parpadeo irregular del rostro.
- Capacidad fija: 12 chispas, con movimiento y desvanecimiento.

No hay partículas del mundo, cámaras ni RenderTextures en el sistema de retrato. El banco se carga una vez y conserva las referencias. Los sprites solo se asignan al cambiar de pose. No se instancian objetos ni materiales por frame. Ambos gráficos tienen raycastTarget desactivado. Los eventos se limpian al desactivar o destruir el componente.

## Archivos de esta actualización

Modificados, conservando sus GUID:
- `Assets/IronRequiem/Scripts/UI/RobotPortraitGraphic.cs`.
- `Assets/IronRequiem/Scripts/UI/RobotPortraitSprites.cs`.
- `Assets/IronRequiem/Editor/RobotPortraitAuthoring.cs`.
- `Assets/IronRequiem/Editor/Tests/RobotPortraitTestRunner.cs`.
- `Assets/IronRequiem/Tests/PlayMode/RobotPortraitTests.cs`.

Añadidos, con sus metas y las de sus carpetas:
- `Assets/IronRequiem/Scripts/UI/RobotPortraitOverlay.cs`.
- `Assets/IronRequiem/Resources/IronRequiem/RobotPortraitV02.asset`.
- Los 23 PNG en `Assets/IronRequiem/Art/UI/RobotPortrait/v02/`: cinco poses por nivel d0/d1/d2/d3, ataque, overdrive y apagado.
- `Assets/IronRequiem/Scenes/RobotPortraitV02Lab.unity`. Es un laboratorio aislado, no añadido a Build Settings; reutiliza una copia del jugador y del HUD existente.
- Este informe y evidencias en `Tools/IronRequiem/Reports/RobotPortraitV02/`.

Respaldo de los archivos de implementación previos, metas, ProjectSettings y Packages: `Tools/IronRequiem/Backups/20261116-RobotPortrait-v02/`. También se usó el puente local ya instalado para solicitar importación, pruebas y auditoría; no se instaló un servidor ni se automatizó el escritorio.

## Validación ejecutada

Resultado final del Test Runner: **9 aprobadas, 0 fallidas**, terminado el 16/11/2026 a las **08:35:11, America/La_Paz**. Evidencia completa: `Tools/IronRequiem/Reports/RobotPortraitV02/playmode.xml`.

1. Los 23 imports: alpha, tamaño 512, pivote común, Full Rect, ausencia de mipmaps y Read/Write.
2. Los límites exactos 75/50/25 %, daño resultante, impactos de cada nivel y curación.
3. Giros a ambos lados, cruce 359/0 grados, retorno al centro, histéresis, movimiento A/D sin giro, recoil local y cámara lenta.
4. Pausa/reanudación, cambio de cámara, salto de posición, discontinuidad angular y bloqueo cinemático.
5. Cantidad y cooldown de chispas, anclas sin reflejo, movimiento de vértices y reducción real de alpha de la malla; detención al pausar y desaparición al curarse.
6. Carga mediante el binding real F de Input System, aumento de ChargeLevel, mirada mientras carga, cancelación, daño y curación durante carga, carga completa y golpe pesado confirmado mediante colisión y daño real a un objetivo de laboratorio.
7. Disparo aceptado, supresión de reinicios rápidos, duración de ejecución, cambio de arma, OVERDRIVE con casco roto, curación, apagado y enlace de reaparición.
8. Una sola suscripción de daño tras varios Bind, un solo overlay reutilizado, limpieza de eventos y asignaciones administradas.
9. Ratón virtual con inversión horizontal y sensibilidades 0,75/1,5; mando virtual con right stick en ambos sentidos y retorno al centro. Pasan por Input System y el controlador real del jugador, sin enviar teclas o movimientos al escritorio.

La prueba de ejecución dispara NotifyRip, no una ejecución completa contra IA. Para aislar el estado de OVERDRIVE, el fixture equipa un módulo por su API real y asigna el vínculo Held del carrier; la activación ocurre drenando CannonAmmo. No se verificó una recogida completa. La reaparición verifica el enlace a una nueva Health, no el flujo de LoseScene. La prueba de armas rápidas combina un disparo aceptado con emisiones repetidas de la señal de disparo para probar el throttling de presentación.

Los primeros intentos tuvieron dos errores del código de prueba por firmas de API de Unity; fueron corregidos. Una ejecución del runner también falló al solaparse con otra tras recompilar. El resultado de nueve casos corresponde a la ejecución final sobre el código vigente. No se borró la Console para ocultar errores históricos. El residuo InitTestScene generado por ese intento se retiró después de respaldarlo y comprobar que no existía en la instantánea original; las escenas originales se conservaron.

### Capturas reales

Hay **150 PNG de la cámara de Unity** a 1280×720, 1920×1080 y 2560×1440. Se renderiza el HUD real del laboratorio y se verifican sus límites en cada destino. Son capturas de render a esas resoluciones; no se redimensionó ni controló una ventana del escritorio.

Ejemplos, dentro de `Tools/IronRequiem/Reports/RobotPortraitV02/`:
- `d0-front-1080p.png`, `d1-front-1080p.png`, `d2-front-1080p.png`, `d3-front-1080p.png`.
- `d2-left-720p.png`, `d2-right-1440p.png`.
- `d2-sparks-frame1-1080p.png` y `d2-sparks-frame2-1080p.png`.
- `d3-charge-left-1440p.png`, `damage-during-charge-1080p.png`, `heavy-hit-d3-1080p.png`.
- `d3-overdrive-1080p.png`, `d0-overdrive-1080p.png`, `offline-1080p.png`, `respawn-1080p.png`.
- `mouse-invert-True-sensitivity-1.5-720p.png`, `gamepad-right-1080p.png` y `gamepad-left-1080p.png`.

Inspección visual realizada sobre chispas d2 a 1080p, carga d3 mirando a izquierda a 1440p y estados frontales/impacto. El panel conserva escala y alineación; el casco permanece roto durante energía y carga. Los tests inspeccionan las tres resoluciones y la transparencia de los 23 archivos.

### Rendimiento y preservación

Medición acotada en el Editor: **0 bytes de asignación administrada en 1.000 llamadas calentadas a LateUpdate**, tanto en reposo como durante carga con simulación de chispas. Incluye el overlay reutilizado; excluye reconstrucción/render del Canvas y GC total de la partida. No es un benchmark de FPS de Player. Evidencia: `allocation.txt`.

Memoria de las 23 texturas, medida con Profiler.GetRuntimeMemorySizeLong en el Editor: **25.192.816 bytes**. Evidencia: `texture-memory.txt`.

Comparación de **846 archivos protegidos** con la instantánea de esta tarea: **0 cambios**. Incluye escenas y prefabs preexistentes, scripts protegidos de gameplay/base, ProjectSettings y manifest/packages-lock. Los 23 PNG copiados coinciden byte por byte con sus originales. Evidencia: `verification.json` y `protected-hashes.csv` en el respaldo.

Esta comparación se refiere al estado al iniciar v02; no reconstruye la diferencia histórica de ProjectSettings registrada durante v01.

La auditoría final del Editor y la restauración de IntroMenu están en `editor-restoration.txt` y `Tools/IronRequiem/Reports/unity-audit.json`.

Pendientes: Player compilado, benchmark CPU/GPU/Canvas global, mando físico y partida manual completa. No se declaran probados.
