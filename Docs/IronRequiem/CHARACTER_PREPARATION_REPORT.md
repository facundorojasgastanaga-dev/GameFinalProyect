# IRON REQUIEM — jugador y enemigo v0.1

29 de octubre de 2026, America/La_Paz. Dos trabajos independientes sobre el proyecto existente, con una arena común de verificación.

## Entrega

| Trabajo | Asset principal | Informe específico |
|---|---|---|
| Jugador | `Assets/IronRequiem/Prefabs/Player/Player_IR_v01.prefab` | [PLAYER_V01.md](PLAYER_V01.md) |
| Enemigo nuevo | `Assets/IronRequiem/Prefabs/Enemies/T07_IR_v01.prefab` | [ENEMY_V01.md](ENEMY_V01.md) |
| Prueba integrada | `Assets/IronRequiem/Scenes/CharacterLab.unity` | Este informe |

Ambos prefabs son variantes de la plantilla. CharacterLab queda abierta en el Editor, fuera de Play Mode. MainScene y Build Settings se conservaron. Para probar: abrir CharacterLab y pulsar Play; usar los controles existentes, incluido C para agacharse, 1/2 para seleccionar armas y P/Tab para el menú. La zona naranja del codo señala la unión del cañón. El mapa de acciones original conserva sus demás bindings.

## Verificación final

**6/6 pruebas aprobadas: tres EditMode y tres PlayMode en el Editor visible.** Las entradas de las pruebas son dispositivos virtuales del Input System, consumidos por PlayerInputHandler y los controladores originales; no son una sesión manual con ratón físico.

| Prueba | Comprobaciones aprobadas |
|---|---|
| Assets del jugador | Variante, un solo controlador, ausencia de Rigidbody competidor, cámaras, sockets y armas/proyectiles válidos |
| Assets del enemigo | Variante independiente, unión elevada con salud propia, referencias de material URP, dos mallas reales sin bind poses |
| Escena | Un jugador, un enemigo, NavMesh guardado y cero scripts ausentes |
| Entradas FPS | Suelo, movimiento, cámara, diagonal limitada, salto, agachado, disparo, cambio de arma, menú y pausa |
| Enemigo | Patrulla en NavMesh, detección, ataque real de WeaponController, pérdida de visión/objetivo tras cobertura, daño de unión sin duplicación corporal, desprendimiento idempotente, fin del ataque, retirada y muerte |
| Ejecución | Avance de 120 frames y capturas durante Play Mode |

Evidencia final: `Tools/IronRequiem/Reports/character-edit-visible.xml`, `character-play-visible.xml`, `character-verification.json` (`allChecksPass: true`), `character-references.json` y `unity-audit.json`. Se salió de Play Mode sin guardar cambios de ejecución. Las pruebas están disponibles en el Test Runner de Unity. El menú **Tools > Iron Requiem > Characters > Run Visible Editor Verification** ejecuta la secuencia con la API oficial instalada.

El verificador `Tools/IronRequiem/verify_characters.ps1` confirmó intactos los **1.348 archivos protegidos al comenzar esta tarea** y los **1.617 archivos originales de Assets** de la instantánea inicial, incluidos MainScene, prefabs, scripts base, FBX anterior y sus metadatos. Ambos archivos externos nuevos también conservan su SHA-256. Todos los assets nuevos tienen `.meta`. Manifest, packages-lock y ProjectSettings coinciden con su estado al comenzar esta tarea.

El Test Runner reordenó temporalmente símbolos de compilación de Standalone. Se recuperó el orden exacto inicial y se verificó contra su SHA-256 previo; no se quitaron símbolos ni paquetes. Las copias de diagnóstico y configuración permanecen bajo `Tools/IronRequiem/Backups/20261029-characters/`, ignorado por Git.

## Rendimiento medido

Captura de referencia en **Editor**, NVIDIA RTX 4050 Laptop GPU, calidad Default, Game View 2560×1440, VSync 0, un enemigo y dos armas iniciales. Muestra de 120 frames: media **12,90 ms**, percentil 95 **17,22 ms**; último Main Thread registrado **8,56 ms** y última asignación GC en frame **9.153 bytes**. El test y el Editor contribuyen a esos valores. El JSON conserva los datos completos.

No es una medición de un Player compilado ni una comparación antes/después. No establece un presupuesto para oleadas ni un FPS garantizado. Se usó el agrupador de mallas de la plantilla para la figura del jugador: 588 triángulos, cuatro materiales compartidos. El enemigo preparado tiene 50.313 triángulos incluidos los cierres provisionales; las texturas usadas se importan a 2048 con mipmaps. No se añadieron LOD o pooling sin una carga medida que los justificara.

## Herramientas y límites

Se consultó el catálogo instalado: plugin oficial Unity 0.1.8-beta habilitado y sus skills disponibles. Se aplicaron las cuatro skills de IRON REQUIEM, Unity CLI, gestión de paquetes como comprobación, navegación, uGUI y Computer Use. Blender inspeccionó las dos fuentes y preparó la copia. Unity 6000.5.5f1 y sus APIs del Editor crearon materiales, meshes nativos, prefabs y escena; AI Navigation 2.0.14 horneó solo la arena; Test Framework 1.7.0 ejecutó las pruebas. No se usaron subagentes ni se crearon chats nuevos.

Animation Rigging 1.4.1 y DOTween 1.3.045 ya están instalados. No se añadieron constraints sobre una malla sin huesos ni tweens que compitan con la respuesta de armas o navegación de la plantilla. No se necesitó instalar paquetes, cambiar URP/Input System o añadir otro servidor. Pipeline/MCP sigue pendiente bajo la regla de paquetes estables. No hubo compras, generación remota ni consumo de créditos Meshy. No se hicieron commits.

La primera ejecución en batch no pudo validar bloqueo de cursor. Las pasadas iniciales detectaron la ausencia del EventSystem en la escena nueva y una referencia de material nula; se corrigieron reutilizando el EventSystem de MainScene y un material real del indicador de unión. También se corrigieron la posición persistida del cañón, el cierre de escenas vacías del Test Runner y el orden de ejecución EditMode tras salir de Play Mode. Los informes fallidos anteriores se conservan como diagnóstico, separados de los XML finales aprobados. El servicio cloud de Unity AI continúa registrando sus mensajes previos NoSubscription; no se activaron suscripciones ni se cambiaron cuentas.

## Pendientes

El protagonista es una representación provisional de formas simples, sin rig, animación corporal, desgaste pintado ni brazos FPS propios. El enemigo nuevo no trae huesos, pesos o clips: los sockets son guías y el cuerpo todavía se desplaza sin ciclo de caminar. El corte del antebrazo/cañón necesita limpieza artística, tapas definitivas y revisión de pivotes antes de producción. Falta el ciclo completo de recoger/equipar arma extraída, energía, OVERDRIVE y expulsión; esta entrega verifica únicamente desprendimiento físico y pérdida de capacidad del portador. Quedan pruebas de múltiples enemigos, rutas incompletas y rendimiento en un Player de PC.
