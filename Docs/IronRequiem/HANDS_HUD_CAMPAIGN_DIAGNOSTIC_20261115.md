# Manos, HUD, campaña y restauración del menú — 15/11/2026

Estado: implementación aplicada; validación final en curso. Este informe sustituye el diagnóstico inicial que quedó bloqueado temporalmente por CS0246. Aquel bloqueo se resolvió en la sesión del menú y ya no describe el estado actual.

## Autorización y conservación

Se ejecutaron las partes A–D del texto adjunto, adaptado de Claude. La petición posterior «pon el animado de nuevo y ese menú feo bórralo» autoriza restaurar la entrada. La pantalla antigua de campaña se quitó de la escena mediante el Editor y se guardó fuera de Assets en `Tools/IronRequiem/Quarantine/20261115-legacy-menu/IndustrialShell.prefab`, con su meta. Pausa y muerte se conservaron.

Respaldos: `Tools/IronRequiem/Backups/20261115-hands-hud-campaign/` (804 archivos + hashes) y `20261115-animated-menu-entry/`. No hubo commits, compras, paquetes nuevos ni créditos Meshy. Unity, URP e Input System se conservan. No se ejecutó Build Hangar Intro ni se sobrescribió un FBX fuente. Las escenas se modificaron con APIs del Editor y guardas contra escenas ya sucias.

El protocolo de revisión de la skill code-standards solicitada se realizó con revisores de solo lectura: manos, UI/flujo y cierre. No editaron assets ni manejaron Unity. La skill anthropic-skills:unity-3d-character-animation-vfx no estaba disponible; no se inventó su contenido.

## Causas raíz y cambios

### Puños y brazos

El solver antiguo capturaba rotaciones posiblemente animadas como reposo, mezclaba con la pose corriente y compartía una bisagra entre dedos. El bake custom serializado prevalecía sobre los ángulos por defecto. Además, no existía una restricción final de muñeca respecto al antebrazo/cámara. Los pesos mezclados de la mano mecánica FPS deformaban la superficie incluso con flexión calibrada.

`HandPoseLibrary` conserva bind auténtica y poses absolutas. `HandPoseController` aplica los dedos al final de LateUpdate y calibra la orientación del puño con la dirección de los dedos y normal de la palma. Los blends duran 0,05–0,2 s. `ProtagonistHandsPresentation` delega a esta autoridad en la campaña, conservando sockets y lógica de inventario; no se comparte el rig FPS con el de cinemática.

Se adoptó el Plan B autorizado para la superficie de puño cerrado: mallas rígidas L/R hechas en Blender local. El script y el archivo blend permiten reautoría. Métricas de superficie BVH: pulgar exterior a índice/medio a 2,000004 mm; distal-palma de cada dedo a 9,5 mm; escalas de piezas uno; 2.484 triángulos. No se presentan esas distancias como medidas del skin deformable antiguo.

| Dedo | MCP objetivo | PIP | DIP | Distal-palma del puño rígido |
|---|---:|---:|---:|---:|
| Índice | 85° | 102° | 65° | 9,5 mm |
| Medio | 90° | 102° | 65° | 9,5 mm |
| Anular | 92° | 102° | 65° | 9,5 mm |
| Meñique | 95° | 102° | 65° | 9,5 mm |
| Pulgar | 40° | 37,5° | 27,5° | 2,0 mm a segundas falanges índice/medio |

La prueba geométrica +15° se repite después de invertir un signo, si hace falta. `pose-bake.txt` registra ejes, flexión bind, signo y escala de cada falange L/R; `audit.txt` registra jerarquía/matrices. El pulgar tiene un diseño rígido externo y no se certifica mediante la prueba de signo de los otros cuatro dedos.

El corte del antebrazo surgía al ocultar la pieza sustituida junto con su sleeve y no tener segmentos superiores. `FirstPersonUpperArms` conserva hombro, brazo superior y codo del CapsuleRobotRig como copias de malla, con escala horneada y tangentes; sigue el codo de las poses FPS y permanece con el cañón. F armado usa el antebrazo derecho; F desarmado la izquierda; M1 desarmado la derecha. El FX de carga sigue el miembro activo.

### HUD

Se adapta el Canvas existente con una barra industrial inferior: energía/calor, salud, nueve slots, retrato, carga y OVERDRIVE. Números bitmap por geometría 5x7, visor ligero por geometría; daño direccional, grietas, parpadeo, chispas, extracción/ejecución, OD y muerte. Se conservan los prompts y banners previos.

Se corrigió una trampa de la jerarquía: PlayerHealthBar está en el controlador raíz GameHUD; desactivar su GameObject ocultaba todo el HUD. Ahora se desactiva el componente/panel viejo aislado y se mantiene la raíz activa. El layout usa referencia 1920x1080 y anclajes de ancho completo. Las capturas offscreen sin interfaz no son evidencia válida; la captura natural `Bridge/gameplay-hud-after-skip.png` sí muestra números, slots, visor y controles.

### Transiciones

El barrido Resources.UnloadUnusedAssets podía introducir un bloqueo tras la activación. Se eliminó del flujo de juego. Se inicia antes la carga del sector siguiente, mientras se recorre el actual, con activación retenida y prioridad baja; la esclusa adopta esa carga. Se conserva la ventana actual/siguiente y el teardown repartido, sin afirmar que todo el mapa esté residente.

`DrainPendingLoadsForShutdown` libera y espera cargas retenidas antes de descargar escenas en las pruebas; evita que la cola de AsyncOperation quede bloqueada. El flujo de reinicio también libera su operación huérfana. ShaderVariantCollection propia calentada tras la cubierta negra y seis vistas de precalentamiento existentes.

Decisión: conservar la ventana para limitar el alcance y la memoria. No existe una medición nueva con Memory Profiler que demuestre que la residencia completa excede memoria; no se atribuye esa decisión a un dato inexistente. No se horneó oclusión ni se inventaron LOD nuevos.

### Cinemática

Tras mirar la cinta, la cámara sigue el giro corporal desde detrás del hombro y revela a los Carroñeros antes del vuelo a la cabeza. SphereCast prueba tres alternativas y conserva el plano autorado si ninguna es segura. Los dedos se muestrean con tiempo absoluto (Inspect/Grip/Relaxed y ventana suave de puño), evitando que un salto dependa del frame anterior. Se reutiliza el giro corporal existente; no se reconstruye el Timeline ni se cambia el asset fuente.

### Menú animado restaurado

La entrada directa a RequiemCampaign todavía llamaba a ShowMain y exponía su IndustrialShell antiguo. Ahora la campaña comienza gameplay y volver carga IntroMenu. `playModeStartScene` apunta a IntroMenu; preview/pruebas guardan y restauran esa preferencia. El build de verificación respeta el orden habilitado existente, con IntroMenu primero, sin modificar EditorBuildSettings.

Cambios de esta reparación: `Scripts/Levels/CombatLabMenu.cs`, `Scenes/RequiemCampaign.unity`, herramienta nueva `Editor/CampaignMenuEntryRepair.cs` y preferencia local de inicio de Play. IntroMenu, su arte, prefabs y animaciones no se reconstruyeron. La pantalla antigua está fuera de Assets; no puede volver a cargarse en la campaña actual. `animated-menu-motion.txt` mide desplazamiento de personajes y cámara entre dos fotogramas; `Bridge/animated-menu-restored-a.png` y b muestran el menú recuperado.

## Evidencia y límites

- EditMode poses: **3/3 superadas**, XML y status en `Tools/IronRequiem/Reports/HandsHudCampaign/`.
- PlayMode: se están ejecutando las siete CampaignTests y una prueba de cinemática completa/saltos. No confundir capturas creadas por un test con inspección visual aprobada.
- Baseline Player real: `tour-before.txt`, RTX4050/i5-13420H, **1600x900**. Picos de transición 25,6/21,0/36,3/18,2/19,0/19,4 ms. Room02 incumplía 33 ms. Promedios y p99 por sala están en ese mismo archivo. No se etiqueta como 1080p.
- La medición posterior y el objetivo de 33 ms siguen pendientes hasta ejecutar el Player reparado. El benchmark actual exige 1920x1080 reales para -irFullHD y falla si la ventana no los proporciona.
- Los errores históricos permanecen en Logs/Editor.log. La prueba abre una observación nueva y registra sus errores en test-errors.txt. Los fallos de autenticación de servicios AI/entitlements del Editor no son errores de scripts del juego; tampoco se ocultan como una consola global sin errores.
- El estado final de verificación se añadirá aquí tras revisar resultados, capturas y hashes. No se declara terminada todavía la validación A–D.

## Verificación posterior de la presentación

El cierre visual detectó que el validador de URP devolvía _ZWrite a cero en los materiales transparentes serializados. Eso hacía visibles las falanges de la cara opuesta a través de la placa dorsal, aunque la orientación fuese correcta. HandPoseController ahora crea seis copias de material únicamente al inicializar, mantiene depth write y alpha blend coherentes, y las libera al destruirse. El skin abierto deja de renderizar por completo al alcanzar el cierre total, conservando enabled como solicitud de visibilidad. No se modifica el material fuente en ejecución.

Las capturas actuales right-fist-fps/left-fist-fps y vistas aisladas muestran la placa dorsal opaca, sin esas superficies atravesadas. Tras esta corrección pasaron otra vez las tres pruebas de poses y la prueba focal de presentación/cañón/extracción/OD. El HUD tiene capturas reales de layout 720/1080/1440 con salud completa, salud 20 % y OVERDRIVE; el capturador reutiliza la herramienta existente y evita aplicar postprocesado a la UI, igual que el overlay real.

El primer build reparado finalizó sin errores. El segundo build con la corrección de materiales también: errores 0, 44,74 s. La primera medición posterior está en performance-comparison.md dentro de Tools/IronRequiem/Reports/HandsHudCampaign: Room02 baja de 36,3 a 16,4 ms, pero el primer cambio registró 41,4 ms. El perfilado adicional conserva un pico de unos 36 ms junto a trabajo de activación/descarga; se repartió el teardown por GameObject, incluyendo subjerarquías grandes, y se separó el frame de desactivación. Se repite la validación completa tras ese ajuste.

Conservación: los GUID originales de escenas y scripts comparados permanecen iguales. IntroMenu y EditorBuildSettings no cambiaron respecto al respaldo de la reparación del menú. Respecto a la instantánea inicial hay cambios concurrentes en IntroMenu, MainMenuPanelPresentation.cs, Editor/Menu/MainMenuRestore.cs y Tests/PlayMode/MainMenuRestoreTests.cs; este chat no escribió esos archivos ni revirtió su trabajo. Véanse protected-differences-final.json y animated-menu-preservation.json.

## Archivos de esta intervención

Existentes modificados: Scripts/Diagnostics/CampaignTourBenchmark.cs; Scripts/HangarIntro/HangarIntroCinematic.cs y WakeUpRobotPose.cs; Scripts/Levels/CampaignFlow.cs y CombatLabMenu.cs; Scripts/Player/FistChargePresentation.cs, ProtagonistHandsPresentation.cs y ProtagonistMelee.cs; Scripts/UI/MechanicHud.cs; Tests/PlayMode/CampaignTests.cs; Scenes/RequiemCampaign.unity y Scenes/Campaign/Room00_Hangar.unity. Todos esos paths cuelgan de Assets/IronRequiem/.

Nuevos: Scripts/Player/HandPoseController.cs, HandPoseLibrary.cs y FirstPersonUpperArms.cs; Scripts/UI/RetroStatusBar.cs, RetroDigitsGraphic.cs y RobotPortraitGraphic.cs; Editor/HandPoseAuthoring.cs, PlayerVisualRepairAuthoring.cs, HandsHudCampaignTools.cs, HandsHudCampaignVerification.cs y CampaignMenuEntryRepair.cs; Tests/EditMode/HandPoseControllerTests.cs y Tests/PlayMode/HangarIntroPresentationTests.cs; assets de poses/mallas/materiales/variantes en Art/Player/HandPoses/, con sus meta. Herramienta Blender y exportaciones bajo Tools/IronRequiem/. Documentación: este informe, Contexto_Animaciones.md y apéndice v36 de CHECKLIST_TRABAJO.md. Preferencia local de entrada Play: UserSettings/EditorUserSettings.asset.

No se modificaron por este chat los scripts base de Assets/FPS, el FBX fuente T-07, el FBX fuente CapsuleRobotRig, Packages/manifest.json, packages-lock.json ni ProjectSettings. No hay commit automático. El Player anterior se archivó en Tools/IronRequiem/Quarantine/20261115-legacy-menu/baseline-player para que el build de diagnóstico con entrada antigua no se confunda con el actual.
