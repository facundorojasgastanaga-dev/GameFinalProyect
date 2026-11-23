# IRON REQUIEM — prompts por chat y skills por tarea

Actualizado: 17/11/2026. Cada chat empieza sin memoria: pega su prompt completo. Todos los prompts piden leer este archivo primero, así que las reglas comunes y la política de skills viven aquí una sola vez.

Un chat por tarea. No ejecutes a la vez dos chats que toquen los mismos archivos o escenas (ver "Conflictos" al final).

---

## Reglas comunes (todos los chats)

1. Lee `AGENTS.md`, `Docs/IronRequiem/SETUP_REPORT.md` y `Docs/IronRequiem/CHECKLIST_TRABAJO.md`. Identifica si el usuario pide información, plan o ejecución; ejecuta solo lo autorizado.
2. Unity 6000.5.5f1, URP 17.5.0, Input System 1.19.0. No cambiar Editor, pipeline, sistema de entrada ni paquetes; nada preview/experimental. No Meshy, no créditos, no compras.
3. Respaldo en `Tools/IronRequiem/Backups/<fecha>/` antes de modificar. APIs del Editor para escenas y prefabs; nunca YAML a mano. No guardar escenas que el usuario ya tiene modificadas en git: trabajar en copias. No commits. Conservar GUID y `.meta`. No sobrescribir FBX/GLB fuente.
4. Código nuevo en `Assets/IronRequiem/` con namespace `IronRequiem`.
5. Problemas fuera del alcance: anotarlos como pendientes, no arreglarlos.
6. Cierre: verificación separada en compilación / tests EditMode-PlayMode / prueba manual en Play Mode (salir sin guardar cambios de ejecución). Registrar en `CHECKLIST_TRABAJO.md`.

### Equipo de subagentes (todos los chats)

- El chat es el COORDINADOR y el único que maneja el Editor de Unity (compilar, Play Mode, Profiler, guardar) y la escritura final en Blender.
- Subagentes de solo lectura van en paralelo; los que escriben tienen archivos exclusivos declarados antes de empezar. Sin worktrees (duplicarían `Library`).
- Cada brief de subagente incluye: objetivo, archivos a leer, archivos que puede tocar, lo prohibido, **las skills que debe cargar** (de la tabla de su tarea) y formato de entrega. El coordinador verifica cada resultado.

### Política de skills

- **Antes de cada tarea, carga las skills que su prompt indica para esa tarea** (herramienta Skill para `gamedev:*` y `unity:*`; leer el `SKILL.md` para `.agents/skills/iron-requiem-*` y las de `~/.claude/skills`). Indica en el informe qué skill usaste en cada tarea y qué aplicaste de ella.
- Prioridad si se contradicen: `AGENTS.md` > skills locales `iron-requiem-*` > `unity:*` (oficiales) > `gamedev:*` > skills genéricas.
- Las `gamedev:unity-*` están escritas para Unity 6.3 LTS; este proyecto es 6000.5: verifica cada API en el proyecto antes de usarla.
- Las skills marcadas **(solo principios)** están orientadas a three.js/web: usa sus criterios de diseño, nunca su código ni sus herramientas.
- Ninguna skill autoriza instalar paquetes UPM, cambiar pipeline/entrada ni ejecutar `unity test` contra el proyecto abierto. `unity:unity-package-management` y `unity:asset-transformer-toolkit` (Pixyz, licencia) **no se usan**.
- Si falta una skill: buscar solo en catálogo de Claude, Anthropic o Unity Technologies; terceros solo con aprobación del usuario.

### Skills disponibles útiles para este proyecto

| Grupo | Skills |
|---|---|
| Locales | `iron-requiem-fps`, `iron-requiem-enemy-ai`, `iron-requiem-modular-weapons`, `iron-requiem-performance` |
| Unity oficiales | `unity:ui`, `unity:ui-ugui`, `unity:unity-cli`, `unity:urp-postprocessing`, `unity:initialize-ai-navigation`, `unity:physics-3d-collision`, `unity:sprite-editor`, `unity:manage-sprite-atlas`, `unity:optimize-text-mesh-pro`, `unity:audio-setup-mixers`, `unity:optimize-audio`, `unity:craft-game-floors`, `unity:shader-graph-create-custom-node`, `unity:validate-urp-render-graph-renderer-feature`, `unity:generate-editor-search-query` |
| Plugin gamedev | `gamedev:router`, `gamedev:unity-csharp-scripting`, `gamedev:unity-input-system`, `gamedev:unity-physics`, `gamedev:unity-animation`, `gamedev:unity-scriptableobjects`, `gamedev:unity-navmesh`, `gamedev:unity-build-pipeline`, `gamedev:fps-shooter`, `gamedev:game-feel`, `gamedev:game-ui-ux`, `gamedev:camera-systems`, `gamedev:input-systems`, `gamedev:physics-tuning`, `gamedev:level-design`, `gamedev:game-ai`, `gamedev:ai-behavior-trees-utility-ai`, `gamedev:shader-programming`, `gamedev:performance-optimization`, `gamedev:create-game-assets`, `gamedev:audio-design`, `gamedev:game-docs` |
| Genéricas | `game-dev-combat-animation`, `tune-enemy-ai`; **(solo principios)**: `design-action-combat`, `design-game-encounters`, `create-game-vfx`, `build-rigged-game-assets` |

---

## 1. Animaciones (general)

```
Lee primero Docs/IronRequiem/PROMPTS_CHATS.md (reglas comunes, subagentes y política de skills) y aplícalo.
Área: animaciones del protagonista (manos/brazos FPS, cinemática) y de enemigos.
Lee Contexto_Animaciones.md, T07_ANIMATION_VFX.md, HANDS_HUD_CAMPAIGN_DIAGNOSTIC_20261115.md y PLAYER_V01.md.

Autoridad de poses que se respeta: HandPoseController (1000, único escritor de dedos), ProtagonistHandsPresentation (110), ProtagonistMelee (100), FirstPersonUpperArms (120), WakeUpRobotPose (200). No mezclar ejes ProtagonistV01 FPS / CapsuleRobotRig. Enemigos: Animator MoveSpeed, Attack, Alerted, OnDamaged; scripts en Assets/IronRequiem/Scripts/Animation.

Tarea: [describe la animación o fallo].

Skills por tarea:
- Diagnóstico de poses y rig FPS: iron-requiem-fps, gamedev:unity-animation.
- Animator de enemigos (estados, transiciones, capas, IK): gamedev:unity-animation, iron-requiem-enemy-ai.
- Peso, anticipación, impacto y recuperación de golpes/ataques: game-dev-combat-animation, gamedev:game-feel.
- Scripts C#: gamedev:unity-csharp-scripting.
- Rendimiento (Animator, GC): iron-requiem-performance, gamedev:performance-optimization.

Entrega: causa con archivo:línea, cambio mínimo, capturas y verificación en Play Mode.
```

## 2. Animación de cambio mano → rifle (estación de montaje)

```
Lee primero Docs/IronRequiem/PROMPTS_CHATS.md y aplícalo.
Lee Contexto_Animaciones.md y HANDS_HUD_CAMPAIGN_DIAGNOSTIC_20261115.md.
Autorizado: mejorar la animación donde la mano se cambia por el rifle/arma montada. La visibilidad de armas frente al HUD la resuelve el chat 4 (HUD); aquí no se toca la UI.

- Assets/IronRequiem/Scripts/Levels/WeaponMountStation.cs: línea de tiempo TArmB0, TApproach, TSlide, TLock, TLower1, TSwap=4.6, TRelease1=4.85; override del brazo FPS; Swap()/SelectMounted(). StationArmRig mueve el brazo de la estación.
- Identifica qué arma es "el rifle" y su escena. Si es el cambio normal en PlayerWeaponsManager y no la estación, avisa antes de seguir.
- Objetivo: el arma entra acoplada al socket sin pop, asentamiento/recoil al bloquear, ease continuos, sin clipping con cámara. Duración total igual salvo justificación.
- Respeta la autoridad de poses (HandPoseController único escritor de dedos). Si el chat 6 ya definió un ESTÁNDAR DE ACOPLE nuevo, úsalo.

Skills por tarea:
- Análisis de la línea de tiempo y rig: iron-requiem-fps, iron-requiem-modular-weapons, gamedev:unity-animation.
- Easing, asentamiento, recoil y sensación de peso: gamedev:game-feel, game-dev-combat-animation.
- Encuadre y clipping de cámara FPS: gamedev:camera-systems.
- Código: gamedev:unity-csharp-scripting.

Subagentes: DIAGNÓSTICO (solo lectura) → IMPLEMENTADOR (solo WeaponMountStation.cs y StationArmRig.cs) → REVISOR (regresiones de melee, carga de puño, cinemática y cambio 1/2).
Entrega: capturas antes/después de aproximación, separación, swap y release; archivos con archivo:línea; verificación separada.
```

## 3. Sprites

```
Lee primero Docs/IronRequiem/PROMPTS_CHATS.md y aplícalo.
Área: sprites 2D (retrato del robot en el visor, iconos de slots, feeds CCTV, siluetas del panel de cuerpo, elementos 2D de VFX).
Lee ROBOT_PORTRAIT_V01_REPORT.md, ROBOT_PORTRAIT_V02_REPORT.md, VISUAL_OVERHAUL.md; revisa Art/UI/RobotPortrait/v01-v02, RobotPortraitSprites.cs, RobotPortraitGraphic.cs, RobotPortraitOverlay.cs, CctvFeed.cs y Docs/IronRequiem/Concepts.
Los números del HUD son geometría 5x7 (RetroDigitsGraphic): no convertirlos a sprites.

Tarea: [sprite o estado].

Skills por tarea:
- Guía de estilo, producción y control de calidad del arte (transparencia, consistencia, licencias): gamedev:create-game-assets.
- Slicing, pivotes, bordes 9-slice: unity:sprite-editor.
- Atlas y memoria: unity:manage-sprite-atlas, iron-requiem-performance.
- Integración en el Canvas: unity:ui-ugui.

Entrega: assets con Import Settings explícitos (tipo, filtro, compresión, PPU) y captura en Play Mode de cada estado.
```

## 4. Interfaz: HUD nuevo (armas izquierda, cuerpo derecha, avatar abajo)

```
Lee primero Docs/IronRequiem/PROMPTS_CHATS.md y aplícalo.
Lee Contexto_Animaciones.md ("HUD y entrada"), HANDS_HUD_CAMPAIGN_DIAGNOSTIC_20261115.md, ROBOT_PORTRAIT_V02_REPORT.md y WEAPON_FEEDBACK.md.
Referencias: Docs/IronRequiem/Concepts/UI/hud_layout_boceto_20261116.png y carga_punio_referencia_20261116.mp4 (si no reproduce, ábrelo en el navegador integrado y extrae fotogramas con video.currentTime).

Autorizado: rediseño del HUD de juego.
A. Layout: columna IZQUIERDA alta = armas (actual, slots, calor/energía, OVERDRIVE). Columna DERECHA alta redondeada = estado del cuerpo (tarea C) + vida + carga del puño. ABAJO AL CENTRO = avatar animado (reutiliza RobotPortraitGraphic/Overlay/Sprites). Centro libre: retícula y viewmodel siempre visibles (resuelve que la barra de vida tapa las armas). Reemplaza la presentación de RetroStatusBar sin perder datos ni lógica.
B. Medidor radial de carga del puño (según el video): ~6 segmentos trapezoidales, contorno cian neón con glow, relleno semitransparente, centro hueco; se llena por segmentos en sentido horario con la carga real (FistChargePresentation/ProtagonistMelee/FistChargeHud); lleno = segmentos se separan hacia afuera (diafragma/flor); al soltar se recogen/vacían. Tiempo unscaled, sin GC por frame.
C. Panel de estado del cuerpo: silueta frontal del ROBOT protagonista (propia, no copiar imágenes) con etiquetas a ambos lados unidas por líneas/flechas y detalles de engranajes: CABEZA/ÓPTICA, CUELLO, HOMBRO I/D, BRAZO I, BRAZO D (icono del arma montada), TORSO/NÚCLEO, CADERA, PIERNA I/D, PIE I/D. Colores por integridad: sana → ámbar → rojo parpadeante → destruida gris. Golpe = destello de la parte. OVERDRIVE = pulso de toda la silueta. Vida numérica con RetroDigitsGraphic.
   ALCANCE: el jugador no tiene daño por partes. Implementa OPCIÓN A (visual derivada de vida global + dirección del último impacto; documenta el mapeo). OPCIÓN B (integridad real con efectos de gameplay) solo se anota como propuesta en la checklist.
D. Estilo y técnica: Canvas uGUI e IronRequiemUiTheme; conserva retícula, prompts, banners y dígitos 5x7. Probar 1920x1080, 2560x1440, 16:10 y 21:9. UiUnifyAuthoring.cs tiene cambios del usuario: solo extender.

Skills por tarea:
- Enrutado y elección de framework UI: unity:ui.
- Jerarquía Canvas, RectTransform, anchors, layouts, prefabs: unity:ui-ugui.
- Reglas de HUD (anclaje a bordes, escalado, ultrawide, HUD por eventos y no por sondeo, accesibilidad mínima): gamedev:game-ui-ux.
- Animación del medidor y del panel (easing, pulsos, destellos): gamedev:game-feel.
- Silueta y sprites del panel: gamedev:create-game-assets, unity:sprite-editor, unity:manage-sprite-atlas.
- Textos TMP si los hay: unity:optimize-text-mesh-pro.
- Integración con datos del juego: iron-requiem-fps, iron-requiem-modular-weapons, gamedev:unity-csharp-scripting.
- Coste por frame: iron-requiem-performance, gamedev:performance-optimization.

Subagentes: REFERENCIAS (lectura: video y boceto) ‖ INVENTARIO HUD (lectura) → MEDIDOR PUÑO (FistChargeRadialGraphic.cs) ‖ PANEL CUERPO (BodyIntegrityPanel.cs, BodySilhouetteGraphic.cs, Art/UI/BodyStatus/) ‖ LAYOUT (RetroStatusBar/MechanicHud) → REVISOR (regresiones y GC).
Entrega: capturas por resolución; secuencia del medidor vacío→cargando→lleno→disparo; panel sano/daño por zona/crítico/OVERDRIVE; tabla de skills usadas por tarea; verificación separada.
```

## 5. Mecánicas

```
Lee primero Docs/IronRequiem/PROMPTS_CHATS.md y aplícalo.
Área: movimiento (dash, slide), armas modulares (desprender, recoger, calor, OVERDRIVE automático, lanzar/expulsar), melee y daño.
Lee DESIGN_V02_UPDATE.md (vigente; MECHANICS_V02.md es histórico), MODULAR_ARM_AND_PICKUP.md, PISTOL_MODULE_1000.md, CANNON_CHARGE.md, SECONDARY_SHOTGUN_SLIDE_EXECUTION.md y NATIVE_WEAPON_REPAIR.md.
Controles vigentes: WASD/Espacio/Shift, Ctrl dash aéreo, C slide/agachado, M1 disparo (mantener = carga del cañón), F recoger/melee izquierdo, G lanzar, 1/2 selección; OVERDRIVE automático.
Extiende PlayerCharacterController mediante IPlayerMotionExtension. No reasignes bindings ni GUID del .inputactions.
Indica qué ID de la checklist corresponde (p. ej. IR-02, IR-05) y ejecuta solo ese.

Tarea: [mecánica o fallo].

Skills por tarea:
- Diseño de combate FPS (hitscan/proyectil, TTK, recoil, daño): gamedev:fps-shooter, iron-requiem-modular-weapons.
- Movimiento, dash, slide y controlador: iron-requiem-fps, gamedev:physics-tuning, gamedev:unity-physics.
- Colisiones, triggers y raycasts que fallan: unity:physics-3d-collision.
- Entrada (acciones, buffering): gamedev:unity-input-system, gamedev:input-systems.
- Sensación de impacto (hit-stop, shake, knockback): gamedev:game-feel; design-action-combat (solo principios).
- Datos de tuning: gamedev:unity-scriptableobjects (solo si encaja con CombatTuningProfile existente).
- Código y tests: gamedev:unity-csharp-scripting.

Entrega: pasos de reproducción, fix, tests EditMode/PlayMode en IronRequiem.PlayModeTests y prueba manual del ciclo completo.
```

## 6. Modelos 3D: protagonista refinado + rediseño de armas

```
Lee primero Docs/IronRequiem/PROMPTS_CHATS.md y aplícalo.
Lee CHECKLIST (IR-09), PLAYER_V01.md, CHARACTER_PREPARATION_REPORT.md, Contexto_Animaciones.md, MODULAR_ARM_AND_PICKUP.md, PISTOL_MODULE_1000.md, WEAPON_FEEDBACK.md, VISUAL_AUDIT_F0.md, VISUAL_OVERHAUL.md y Tools/IronRequiem/Blender/USAGE.md.

A. PROTAGONISTA (autorizado). Fuentes intocables: Art/Player/ProtagonistV01/Source (LOD0-2, FPS_Parts), Rigged, Art/Protagonist/CapsuleRobot(Rig), Exports/PlayerRigidFist + build_closed_fist.py; conceptos en Concepts/Player/Protagonist_Multiview_v02.
 A1 Auditoría (blender_asset_tool.py, inspect_protagonist.py, verify_protagonist_seams.py, measure_protagonist_joints.py): normales, caras duplicadas/internas, no-manifold, piezas flotantes, costuras, UVs, texel density, materiales, pesos mezclados en piezas rígidas, huesos sin uso, escala y pivotes, diferencias entre LOD y FPS/cuerpo. Renders antes.
 A2 Refinado sin cambiar silueta: corregir todo, biselar bordes vistos en FPS, normales ponderadas, piezas rígidas a un solo hueso con peso 1.0, LOD regenerados, presupuesto para 120 FPS (IR-04).
 A3 Manos separadas: mano I/D independientes cortadas en la muñeca con collar mecánico sin huecos; socket propio por mano (verifica nombres existentes); ESTÁNDAR DE ACOPLE documentado (posición, rotación, escala, diámetro del collar, eje de inserción) válido para mano, pistola, cañón y módulo primario. Regenerar HandPoses desde las nuevas bind poses; puño rígido unificado con la mano nueva.
 A4 Exportar a Tools/IronRequiem/Exports/IR_Protagonist_Refined_v01/; en Unity, Art/Player/ProtagonistV02/ y prefabs VARIANTES de Protagonist_FPS y Protagonist_Body. No reemplazar v01 sin aprobación.
B. ARMAS (PistolModule_1000, IR_PrimaryModule_v02, T07 Cannon).
 B1 Conceptos (DETENTE): 2-3 direcciones por arma, silueta legible, zona emisiva de calor/OVERDRIVE, boca clara; el cañón sigue pareciendo arrancado del T-07. En Concepts/Weapons/Redesign_v01/ con pros/contras.
 B2 Tras aprobación: modelado con scripts Blender reproducibles, cumpliendo el ESTÁNDAR DE ACOPLE; pivote en el acople, +Z disparo, sockets muzzle, emisivos, LOD; prefabs variantes; verificar WeaponController, recogida/desprendimiento, lanzamiento, VFX de carga y WeaponMountStation.

Skills por tarea:
- Auditoría y refinado de mallas, UVs, texturas y QA: gamedev:create-game-assets, iron-requiem-performance.
- Rig rígido, sockets y separación de manos: iron-requiem-fps, iron-requiem-modular-weapons; build-rigged-game-assets (solo principios: separación de piezas y pivotes).
- Regeneración de poses e integración con Animator: gamedev:unity-animation.
- Materiales y shader HardSurface: gamedev:shader-programming.
- Conceptos de armas: gamedev:create-game-assets (guía de estilo), gamedev:fps-shooter (legibilidad de armas).
- Presupuesto de polígonos/LOD y medición: gamedev:performance-optimization, iron-requiem-performance.
- Búsqueda de referencias a prefabs/GUID en el Editor: unity:generate-editor-search-query.

Subagentes: AUDITOR PROTAGONISTA ‖ AUDITOR ARMAS (lectura) → MODELADOR PROTAGONISTA (Blender/Working/protagonist_v02, refine_protagonist_v02.py) ‖ CONCEPTOS ARMAS (Concepts/Weapons/Redesign_v01) → RIG Y POSES → [aprobación] MODELADOR ARMAS → REVISOR.
Entrega: PROTAGONIST_V02_REFINE.md (auditoría, correcciones, estándar de acople, métricas, renders), conceptos de armas para elegir y luego WEAPONS_REDESIGN_V01.md; verificación separada; IR-09 en la checklist.
```

## 7. Mapa, efectos y rediseño de puertas (zona de la cinta protegida)

```
Lee primero Docs/IronRequiem/PROMPTS_CHATS.md y aplícalo.
Lee CHECKLIST (IR-03, IR-04, IR-07 y la sección del menú del hangar/cinta), VISUAL_OVERHAUL.md, VISUAL_AUDIT_F0.md, BUNKER_POLISH.md, VisualOverhaul/CLEANUP_TABLE.md, WEAPON_FEEDBACK.md, CANNON_CHARGE.md y T07_ANIMATION_VFX.md.

ZONA PROTEGIDA (no se toca): Assets/FPS/Scenes/IntroMenu.unity completa; en Room00_Hangar la pasarela, la cinta, sus walkers y lo que la cinemática mira; Scripts/Menu/*, Scripts/HangarIntro/*, Editor/Menu/HangarMenuAuthoring.cs, Editor/HangarIntro/*, Prefabs/Menu/MenuWalker_*. Assets compartidos (p. ej. T07Mech_Atlas, materiales, Volume): si los referencia la zona protegida, usar copia/variante. Prueba: hashes y capturas del menú (cinta, explosión al clic, secuencia JUGAR) idénticos antes y después.

FASE 1 auditoría (DETENTE al terminar): MAP_FX_AUDIT_<fecha>.md con, por sala (Room00 fuera de zona protegida, Room00b, 01-04, Pursuit05): legibilidad del recorrido, espacio para dash/slide y flanqueo, coberturas, atascos y huecos de colisión, NavMesh, z-fighting, materiales, variedad entre salas, iluminación y probes. Por efecto (VFX/*, Scripts/VisualPass, Scripts/VisualOverhaul): qué hace, captura, estado bien/débil/roto/excesivo, legibilidad, coherencia y coste (Profiler). Postprocesado y causa del blur (IR-03). Inventario de puertas (CampaignAirlock, AirlockCycle, AirlockDoorMotion). Tabla priorizada de propuestas.
FASE 2 mejoras aprobadas: escenas en COPIAS si no hay commit; reutilizar Bunker, Scrap y HardSurface; rehornear NavMesh/probes solo en copias; efectos con estilo común, pooling, sin GC; efectos reemplazados desactivados; medir 120 FPS 1080p antes/después.
FASE P puertas: P1 conceptos (DETENTE): familia de puertas (esclusa principal, paso entre salas, persiana interior, salida) con mecanismo visible (cerrojos, pistones, hojas segmentadas o iris) y estados BLOQUEADA rojo / ABRIÉNDOSE ámbar / ABIERTA / ALARMA. P2 tras aprobación: modelo con scripts Blender, piezas móviles con pivotes, LOD, HardSurface con emisivos de estado; prefabs en Prefabs/Doors con DoorPresentation (cerrojos → separación → apertura, shake leve, chispas/vapor, luces, sonido). CONTRATO QUE NO CAMBIA: puerta abierta termina inactiva sin colisión; puerta que se cierra con colisión desde el primer frame; sin atrapar al jugador; NavMesh respeta estado. Puertas antiguas desactivadas, no borradas.

Skills por tarea:
- Auditoría y mejora de diseño de salas (recorrido, ritmo, métricas del jugador, bloqueos): gamedev:level-design; design-game-encounters (solo principios: composición de arenas y ritmo).
- NavMesh y agentes: unity:initialize-ai-navigation, gamedev:unity-navmesh.
- Colisiones y huecos: unity:physics-3d-collision.
- Postprocesado, Volume y blur IR-03: unity:urp-postprocessing; renderer features: unity:validate-urp-render-graph-renderer-feature.
- Efectos (lectura, impacto, coherencia): gamedev:game-feel, gamedev:shader-programming; create-game-vfx (solo principios: legibilidad, telegraphs, niveles de calidad); unity:shader-graph-create-custom-node si hace falta un nodo.
- Pulido general de aspecto y luz: unity:craft-game-floors (solo diagnóstico y criterios; no reestructurar el proyecto).
- Rendimiento: iron-requiem-performance, gamedev:performance-optimization.
- Puertas: modelo gamedev:create-game-assets; movimiento y sensación gamedev:game-feel; sonido gamedev:audio-design, unity:audio-setup-mixers; código gamedev:unity-csharp-scripting.
- Zona protegida y referencias GUID: unity:generate-editor-search-query.

Subagentes: GUARDIÁN ZONA PROTEGIDA (primero) → MAPA ‖ EFECTOS ‖ RENDER/RENDIMIENTO ‖ PUERTAS INVENTARIO (lectura) → CONCEPTOS PUERTAS → [aprobación] por sala/familia de efectos, MODELADOR PUERTAS, PRESENTACIÓN PUERTAS → REVISOR.
Entrega: auditoría, conceptos, capturas antes/después, secuencia de cada puerta, mediciones, prueba de zona protegida intacta, checklist IR-03/IR-07 + entrada de puertas.
```

## 8. IA enemiga

```
Lee primero Docs/IronRequiem/PROMPTS_CHATS.md y aplícalo.
Área: comportamiento de enemigos (T-07, Carroñero, SawBot, Big Bot, torretas).
Lee ENEMY_V01.md, T07_INSPECTION.md, T07_ANIMATION_VFX.md y BUNKER_POLISH.md. Base: EnemyController, EnemyMobile (Patrol/Follow/Attack), EnemyTurret, DetectionModule, NavigationModule, PatrolPath (Assets/FPS/Scripts/AI) y Assets/IronRequiem/Scripts/AI y Enemies. No migrar NavMeshComponents antiguos sin comprobar referencias.

Tarea: [comportamiento o fallo].

Skills por tarea:
- Arquitectura de decisión (FSM/BT/utilidad), percepción y steering: iron-requiem-enemy-ai, gamedev:game-ai, gamedev:ai-behavior-trees-utility-ai.
- Ritmo de combate, agresividad, espaciado, telegraphs: tune-enemy-ai, gamedev:fps-shooter; design-action-combat (solo principios).
- Navegación: unity:initialize-ai-navigation, gamedev:unity-navmesh.
- Animator de enemigos: gamedev:unity-animation.
- Rendimiento con muchos enemigos: iron-requiem-performance, gamedev:performance-optimization.

Entrega: reproducción, cambio, tests PlayMode y prueba manual en sala de campaña.
```

## 9. Mapa 1 completo: Búnker abandonado (equipo de 6 roles)

Sustituye al chat 7 para mapa y puertas. Seleccionar Opus 5.5 (o el modelo más capaz del selector) para este chat: actúa como coordinador.

```
Lee primero Docs/IronRequiem/PROMPTS_CHATS.md (reglas comunes, subagentes y política de skills) y aplícalo.
Lee también: CHECKLIST_TRABAJO.md, DESIGN_V02_UPDATE.md, MODULAR_ARM_AND_PICKUP.md, ENEMY_V01.md, BUNKER_POLISH.md, VISUAL_OVERHAUL.md, VISUAL_AUDIT_F0.md, VisualOverhaul/CLEANUP_TABLE.md, CANNON_CHARGE.md y, si existe, cualquier MAP_FX_AUDIT_*.md.

# MAPA 1 — BÚNKER ABANDONADO

## Contexto
IRON REQUIEM es un FPS rápido estilo DOOM/ULTRAKILL en un mundo postapocalíptico dominado por robots. Mecánica central: destruir uniones de los enemigos para desprender sus armas, recogerlas, usarlas con calor/energía, llegar a OVERDRIVE y expulsarlas. Por eso los enemigos son también la fuente de armas del jugador: la distribución de enemigos es la economía de armas del mapa.
El primer mapa es un BÚNKER MILITAR ABANDONADO tomado por las máquinas: hormigón, acero oxidado, rejillas, tuberías, señalética de sectores pintada con plantilla, iluminación de emergencia, zonas inundadas o derrumbadas, barricadas de los antiguos ocupantes.

## Objetivo
Diseñar y construir un mapa completo de AL MENOS 15 cuartos, agrupados en zonas del búnker, que termine en una arena de BOSS (boss por definir: el equipo propone conceptos y yo elijo). Con estructura, decoración, encuentros por rondas, puertas rediseñadas tipo compuerta de nave/búnker e iluminación propia por zona.

## Alcance y paradas obligatorias
Trabajo por fases. DETENTE y espera mi aprobación al final de F1, F2 y de los conceptos de puertas (F3a). Lo que no apruebe no se construye.
- F0 Inventario (solo lectura).
- F1 Diseño en papel del mapa completo + conceptos de boss. → PARADA.
- F2 Greybox jugable (bloques, puertas y enemigos provisionales). → PARADA: lo juego yo.
- F3 Producción en paralelo: puertas (conceptos → PARADA → modelado), decoración, iluminación, encuentros.
- F4 Arena del boss: espacio, puerta, iluminación y encuentro provisional. La IA completa del boss es OTRA tarea: deja su diseño documentado y una entrada en la checklist.
- F5 Integración, recorrido completo, rendimiento y revisión independiente.

## Lo que ya existe (reutilizar, no reinventar)
- Flujo: CampaignFlow (carga escenas de sala en stageScenes y conecta CampaignAirlock), CampaignRoom (rondas con CombatTwoRoomsLab, persecución con ChaseCorridorEncounter, EnemySpawnQueue), CombatTuningProfile, ZoneEnvironment, SectorLightingBlend, CorridorAlarm, AirlockCycle, AirlockDoorMotion, GapFallRecovery, LabRoomVisibility.
- Salas actuales: Scenes/Campaign/Room00_Hangar, Room00b_AlarmCorridor, Room01_Foundry, Room02_Breach, Room03_Assembly, Room04_Reactor, Pursuit05 (+ *_MechTest) y Scenes/RequiemCampaign.unity. Room00_Hangar es el prólogo y se mantiene. En F1 el arquitecto propone cuáles de las demás se reutilizan (como copias rediseñadas) y cuáles se reemplazan.
- Kit: Art/Environment/Bunker (materiales Floor, Wall, Ceiling, Grating, Hazard, RustMetal, LampAmber/Cold/Red, Meshes, Textures), Art/Environment/Scrap (MachineWreck, MilitaryMech, RustedWreckage), shader IronRequiem/HardSurface, Art/Rendering/CombatClarity.asset.
- Enemigos (Prefabs/Enemies): T07 (CombatPolish, Common_Destructible, LegRepair_Charged…), BigBot (Combat, Destructible, Explosive, Burst…), SawBot (AI, Arsenal, SawPolish, Sustained), HoverBot_CombatPolish, Turret_CombatPolish; Carroñeros en el hangar. F0 determina cuál es la variante vigente de cada uno.

## ZONA PROTEGIDA (no se toca)
Assets/FPS/Scenes/IntroMenu.unity completa; en Room00_Hangar la pasarela, la cinta transportadora, sus walkers y lo que la cinemática mira; Scripts/Menu/*, Scripts/HangarIntro/*, Editor/Menu/HangarMenuAuthoring.cs, Editor/HangarIntro/*, Prefabs/Menu/MenuWalker_*. Assets compartidos con esa zona (p. ej. T07Mech_Atlas, materiales, perfiles de Volume) solo se usan como copia/variante. Prueba al final: hashes y capturas del menú idénticos.

## Escenas y git
Las escenas de campaña actuales tienen cambios míos sin commit: NO se guardan. Todo el mapa nuevo vive en escenas nuevas: Assets/IronRequiem/Scenes/Map01_Bunker/Map01_R<NN>_<Nombre>.unity y una escena de campaña nueva Map01_Campaign.unity con su propio CampaignFlow. Build Settings no se cambia hasta que lo apruebe.

## MODELOS POR ROL
Lanza cada subagente con la herramienta Agent fijando el parámetro model:
- Tú (coordinador): el modelo de este chat. Si no es el más capaz disponible, avísame antes de empezar.
- Diseño y criterio (arquitecto, encuentros, conceptos de puertas, revisor): model "opus".
- Producción con mucho volumen de trabajo repetitivo pero cuidadoso (decoración, iluminación, modelado de puertas en Blender, scripts de autoría): model "sonnet". Si una tarea de producción requiere decisiones de diseño difíciles, súbela a "opus" y explícame por qué.
- Inventarios y búsquedas de solo lectura: model "haiku".
- Si el selector ofrece un modelo superior a Opus para razonamiento, úsalo en los roles de "opus".

## ARQUITECTURA DE TRABAJO (para que nadie se pise)
1. Una ESPECIFICACIÓN MAESTRA en Docs/IronRequiem/Map01/: MAP01_DESIGN.md (la escribe el coordinador a partir de las de cada rol) + un archivo por rol (01_ESTRUCTURA.md, 02_DECORACION.md, 03_ENCUENTROS.md, 04_PUERTAS.md, 05_ILUMINACION.md). Cada rol solo escribe su archivo.
2. Datos por rol en Assets/IronRequiem/Levels/Map01/Data/<Rol>/ (ScriptableObjects o JSON), una entrada por cuarto con el mismo ID (R01…R15+).
3. Cada cuarto tiene raíces por capa, y cada rol solo crea/borra hijos dentro de la suya: [Structure], [Decor], [Encounters], [Doors], [Lighting].
4. Construcción por SCRIPTS DE AUTORÍA de Editor idempotentes (como HangarSectorAuthoring o LevelOpenPass), uno por rol, en Assets/IronRequiem/Editor/Map01/: StructureAuthoring.cs, DecorAuthoring.cs, EncounterAuthoring.cs, DoorAuthoring.cs, LightingAuthoring.cs. Leen los datos de su rol y reconstruyen su capa con Undo. Los subagentes escriben scripts y datos; SOLO TÚ compilas, ejecutas la autoría, entras en Play Mode, horneas y guardas.
5. Código de runtime nuevo en Assets/IronRequiem/Scripts/Levels/Map01/ y Assets/IronRequiem/Scripts/Doors/ (namespace IronRequiem). Cada archivo tiene un único dueño.
6. Contratos entre roles (los fija el coordinador en F1):
   - Estructura → todos: límites del cuarto, entradas/salidas con su CLASE de puerta, carriles de combate y volúmenes LIBRES (prohibido decorar), zonas candidatas de spawn, huecos para landmark, alturas de techo, anclajes de luz.
   - Encuentros → Estructura: requisitos de arena (espacio para BigBot, líneas de visión de torretas, rutas de flanqueo, coberturas, verticalidad).
   - Puertas ↔ Estructura: tamaños de vano por clase de puerta y profundidad del marco.
   - Iluminación es dueña de luminarias (malla + Light); Decoración no coloca luces. Las luces de estado de cada puerta son de Puertas.
   - Decoración no invade volúmenes libres, rutas de NavMesh ni spawns.

## ROLES

### R0 — COORDINADOR (tú)
Misión: dirigir el equipo, fijar contratos, integrar, resolver conflictos, mantener la especificación maestra y responder ante mí.
- Escribe un brief completo a cada subagente: objetivo, documentos a leer, archivos que puede tocar (exclusivos), lo prohibido, skills a cargar, contratos que debe cumplir y formato de entrega.
- Verifica cada entrega (diff, datos, capturas) antes de aceptarla; ningún informe de subagente se da por bueno sin comprobarlo.
- Ejecuta en Unity: compilar, autoría, NavMesh, horneados de luz y reflection probes, Play Mode, Profiler, capturas, guardado.
- Lleva una tabla de estado por cuarto (estructura / decoración / encuentros / puertas / luz / probado).
Skills: gamedev:router, unity:unity-cli, unity:generate-editor-search-query, gamedev:game-docs, iron-requiem-fps.

### R1 — ARQUITECTO DE ESTRUCTURA (model "opus")
Misión: estructura de cada zona y de cada cuarto del mapa completo (≥15 cuartos + arena del boss).
F1 entrega 01_ESTRUCTURA.md con:
- Zonas del búnker (propuesta, ajustable): p. ej. Hangar (prólogo), Esclusas de acceso, Dormitorios, Comedor, Enfermería, Armería, Comunicaciones, Almacenes, Túneles de mantenimiento, Tratamiento de agua (inundado), Taller/Fundición, Ensamblaje, Reactor, Centro de mando, Bóveda/arena del boss. Cada zona con identidad propia (forma, material dominante, landmark).
- Diagrama de flujo (mermaid) con cuartos, conexiones, clase de puerta en cada conexión, atajos que se desbloquean, y el recorrido crítico.
- Tabla por cuarto: ID, zona, función (arena de combate / respiro / persecución / transición / exploración / boss), dimensiones, niveles de altura, conexiones, landmark, tiempo estimado de recorrido.
- Curva de ritmo de todo el mapa: tensión, respiros, picos, introducción de enemigos.
- MÉTRICAS reales del jugador medidas en el proyecto (velocidad al caminar y sprint, altura de salto, distancia de dash, longitud de slide, alcance de recogida) y reglas derivadas: ancho mínimo de pasillos, altura de techos, tamaño de arenas, altura de coberturas, separación de plataformas.
- Diseño para movimiento rápido: arenas con bucles y circulación (sin callejones sin salida en combate), verticalidad, espacio para dash/slide, rutas de flanqueo, sin geometría que enganche al jugador.
- Requisitos de la arena del boss (en coordinación con R3).
F2: StructureAuthoring.cs + datos que generan el greybox de todos los cuartos con el kit Bunker (sin decoración), colisiones correctas, NavMesh horneable, vanos de puerta según clase y marcadores de spawn provisionales.
Skills: gamedev:level-design, gamedev:fps-shooter, gamedev:camera-systems (alturas y encuadre), unity:initialize-ai-navigation, gamedev:unity-navmesh, unity:physics-3d-collision, design-game-encounters (solo principios), iron-requiem-fps.

### R2 — DECORACIÓN (model "sonnet")
Misión: vestir cada cuarto como búnker abandonado, con narrativa ambiental y sin estorbar el combate.
- Guía de vestuario por zona: materiales, props dominantes, colores, nivel de destrucción, historia visible (barricadas, cajas de suministros, literas, camillas, taquillas, terminales, cables colgando, tuberías y válvulas, conductos, rejillas, charcos, hollín, restos de robots destruidos, señalización de sector y flechas pintadas que ayudan a orientarse).
- Reutiliza primero el kit Bunker y Scrap. Props nuevos: con scripts de Blender reproducibles en Tools/IronRequiem/Blender (modelado propio). Generar con Tripo vía Composio solo con mi aprobación por cada generación, con el costo indicado antes. Nada de Meshy ni compras.
- Respeta los volúmenes libres, el NavMesh y los spawns. Decoración pequeña sin colisión; la grande con colisión simple.
- Rendimiento: static batching, GPU instancing donde aplique, LOD, occlusion culling, materiales compartidos; presupuesto de draw calls por cuarto definido en F0.
- DecorAuthoring.cs + datos por cuarto.
Skills: gamedev:create-game-assets, unity:craft-game-floors (solo criterios de acabado), gamedev:shader-programming (HardSurface, suciedad, humedad), gamedev:level-design (orientación con señalética y landmarks), iron-requiem-performance, gamedev:performance-optimization.

### R3 — ENCUENTROS (model "opus")
Misión: qué enemigos van en qué cuarto, cuántas rondas, cuántos por ronda, desde dónde aparecen y en qué orden.
- Tabla por cuarto: tipos de enemigo (variante de prefab vigente), número de rondas, composición de cada ronda, máximo simultáneo, puntos y volúmenes de spawn, condición de fin de ronda, condición de apertura de puerta, armas desprendibles disponibles.
- Introducción progresiva: cada tipo nuevo se presenta solo o en condiciones favorables antes de combinarse con otros. Combinaciones que exijan usar la mecánica de desprender armas.
- ECONOMÍA DE ARMAS: en cada combate el jugador debe poder conseguir armas desprendibles útiles; curva de calor/OVERDRIVE; recuperación de salud si existe en el proyecto.
- Respiros entre combates y una persecución o evento de alarma en el recorrido (CorridorAlarm, ChaseCorridorEncounter).
- Límite de enemigos simultáneos y de activaciones por frame compatibles con EnemySpawnQueue y con 120 FPS.
- BOSS: 2-3 conceptos (silueta, armas desprendibles por fases, fases, ataques telegrafiados, debilidades ligadas a la mecánica, requisitos de arena). Yo elijo uno en la PARADA de F1.
- EncounterAuthoring.cs + datos que configuran CampaignRoom / CombatTwoRoomsLab / EnemySpawnQueue / CombatTuningProfile en las escenas nuevas, sin cambiar el código de esos sistemas salvo aprobación.
Skills: iron-requiem-enemy-ai, iron-requiem-modular-weapons, gamedev:fps-shooter, gamedev:game-ai, tune-enemy-ai, design-game-encounters y design-action-combat (solo principios), gamedev:unity-navmesh, iron-requiem-performance.

### R4 — PUERTAS (conceptos model "opus"; modelado e implementación model "sonnet")
Misión: rediseño completo de las puertas entre zonas y cuartos. Nada de paneles simples: compuertas de búnker y de nave, pesadas y mecánicas.
- Familia por CLASE (tamaños acordados con R1):
  · Compuerta de zona (blast door grande): hojas gruesas que se separan en vertical/horizontal o en diagonal, pistones hidráulicos, cerrojos que se retraen primero, vapor y chispas.
  · Esclusa entre cuartos (airlock): doble puerta con cámara intermedia, ciclo de presurización, luces giratorias.
  · Escotilla redonda tipo bóveda: rueda de cierre que gira, bisagra pesada, para pasos secundarios o atajos.
  · Persiana de seguridad / cierre de emergencia: baja durante los combates y se levanta al terminar la ronda.
  · Puerta forzada/dañada: atascada, se abre a medias o hay que destruir su mecanismo.
  · Puerta del BOSS: única, monumental, con secuencia de apertura especial.
- Lenguaje de estado legible a distancia: BLOQUEADA (rojo), ABRIÉNDOSE (ámbar, luces giratorias, sirena), ABIERTA/PASA, ALARMA, y señalización pintada de sector sobre el marco.
- F3a conceptos (PARADA): 2-3 direcciones con bocetos o renders en Docs/IronRequiem/Concepts/Doors/Map01/, tabla de pros y contras y estimación de coste.
- F3b tras aprobación: modelos con scripts de Blender reproducibles (piezas móviles separadas con pivotes, LOD, HardSurface con emisivos de estado), exportación a Tools/IronRequiem/Exports/Doors_Map01/, prefabs en Assets/IronRequiem/Prefabs/Doors/ y un componente DoorPresentation (secuencia cerrojos → separación → apertura con easing pesado, shake leve cercano, partículas, luces de estado, sonido).
- CONTRATO DE JUEGO QUE NO CAMBIA: CampaignAirlock (sourceDoor, destinationDoor, extraDestinationDoors), AirlockCycle y AirlockDoorMotion mandan. Una puerta abierta termina inactiva sin colisión; una puerta que se cierra tiene colisión desde el primer frame; nunca atrapa al jugador ni deja pasar antes de tiempo; NavMesh respeta su estado. Si hay que adaptar AirlockDoorMotion, solo delegando la parte visual.
- DoorAuthoring.cs coloca las puertas según la clase de cada conexión.
Skills: gamedev:create-game-assets, gamedev:game-feel, gamedev:audio-design, unity:audio-setup-mixers, gamedev:shader-programming, gamedev:unity-csharp-scripting, gamedev:unity-animation (si se usa Animator), unity:physics-3d-collision, build-rigged-game-assets (solo principios: piezas y pivotes), iron-requiem-performance.

### R5 — ILUMINACIÓN (model "sonnet")
Misión: iluminación de cada cuarto y de cada zona, atmosférica y a la vez legible para el combate.
- Paleta por zona (fría fluorescente parpadeante, ámbar de sodio, rojo de emergencia, verde de terminales, etc.) integrada con ZoneEnvironment y SectorLightingBlend.
- Guía del jugador con la luz: salidas y puertas siempre destacadas, landmarks iluminados, enemigos legibles contra el fondo (sin siluetas perdidas en negro), sin zonas oscuras que no sean intencionales.
- Estados: normal, combate/alarma (rojo), ronda terminada, apagón dramático en momentos puntuales, transición a la arena del boss.
- Técnica URP: luces horneadas vs tiempo real, límite de luces adicionales y sombras por cuarto, light probes, reflection probes por cuarto, emisivos, Volume por zona (bloom controlado, exposición, color grading). Sin paquetes nuevos ni cambios de pipeline. Revisar que no reaparezca el blur (IR-03).
- Fixtures: es dueño de las luminarias (malla + Light) en los anclajes de R1.
- LightingAuthoring.cs + datos; el coordinador ejecuta los horneados.
Skills: unity:urp-postprocessing, unity:validate-urp-render-graph-renderer-feature (solo si hace falta un renderer feature), gamedev:shader-programming, gamedev:level-design (guiar con luz), unity:craft-game-floors (solo criterios de luz y acabado), iron-requiem-performance, gamedev:performance-optimization.

### R6 — APOYO
- INVENTARIO (model "haiku", F0, varios en paralelo): kit y prefabs disponibles, variantes vigentes de enemigos, sistemas de campaña y sus campos, mediciones de rendimiento previas, métricas del jugador. Entrega Docs/IronRequiem/Map01/00_INVENTARIO.md.
- REVISOR INDEPENDIENTE (model "opus", al final de F2 y de F5): no recibe tus conclusiones, solo el código, los datos y las capturas. Revisa contra AGENTS.md, los contratos, la zona protegida, legibilidad, ritmo, atascos, soft-locks, puertas que atrapan, NavMesh roto y rendimiento.

## Criterios de calidad globales
- Al menos 15 cuartos más la arena del boss, recorrido completo de principio a fin sin soft-locks.
- Cada cuarto se reconoce: un landmark, una paleta y una forma propias; el jugador siempre sabe dónde está la salida.
- Combate rápido sin enganches: probado con sprint, dash, slide y salto en cada cuarto.
- Ninguna puerta atrapa ni deja pasar antes de tiempo; ningún enemigo queda fuera del NavMesh.
- 120 FPS a 1080p (IR-04) medido en build Release en los 3 cuartos más cargados, con la misma metodología de VISUAL_OVERHAUL.md.
- Zona protegida intacta (hashes y capturas).

## Reglas
- Respaldo en Tools/IronRequiem/Backups/<fecha>/ antes de modificar. APIs del Editor y Undo; nada de YAML a mano. No commits.
- No cambiar controles, balance de armas, pipeline ni paquetes. Cambios en sistemas existentes (CampaignFlow, CampaignRoom, EnemySpawnQueue, AirlockDoorMotion…) solo con mi aprobación y sin romper las salas actuales.
- Problemas fuera de alcance: anotarlos como pendientes.
- Si una decisión de diseño importante no está cubierta aquí (p. ej. número final de zonas, reutilizar o no una sala), propónla con tu recomendación en la siguiente PARADA en lugar de decidir sola.

## Entregas
- F0: 00_INVENTARIO.md.
- F1: MAP01_DESIGN.md + archivos por rol, diagrama de flujo, tabla de cuartos, curva de ritmo, plan de encuentros, plan de iluminación, plan de puertas y 2-3 conceptos de boss. → PARADA.
- F2: greybox jugable de todos los cuartos en Map01_Campaign.unity, capturas por cuarto, tiempos de recorrido, informe del revisor. → PARADA.
- F3a: conceptos de puertas. → PARADA.
- F3-F5: capturas antes/después por cuarto (greybox → final), secuencia de cada clase de puerta, tabla de encuentros final, mediciones de rendimiento, prueba de zona protegida, informe del revisor y Docs/IronRequiem/MAP01_REPORT.md con qué hizo cada rol, qué modelo usó, qué skills cargó en cada tarea y qué quedó pendiente.
- Verificación separada en compilación, tests PlayMode y recorrido manual completo. Registrar en CHECKLIST_TRABAJO.md (nuevo ID para Map01 y otro para la IA del boss).
```

## 10. Controles cómodos + menú de reasignación

Seleccionar Opus en este chat: actúa como coordinador.

```
Lee primero Docs/IronRequiem/PROMPTS_CHATS.md (reglas comunes, subagentes y política de skills) y aplícalo.
Lee también DESIGN_V02_UPDATE.md (controles vigentes; OVERDRIVE es automático), MECHANICS_V02.md (solo histórico), MODULAR_ARM_AND_PICKUP.md, ARMS_FIX_20261119.md (bloqueos de entrada durante ejecución y estación), CHECKLIST_TRABAJO.md y Map01/MAP01_DESIGN.md (por si algún cuarto depende de un control).

# CONTROLES MÁS CÓMODOS Y MENÚ PARA REASIGNARLOS

Objetivo: (A) un esquema de controles por defecto más cómodo para un FPS rápido (teclado+ratón y mando), sin conflictos, y (B) una interfaz en el juego para cambiar cada control, guardarla y restaurarla. No cambies balance, daño, tiempos de armas ni el Input System (1.19.0, entrada activa = Input System). No instales paquetes.

## Lo que hay hoy (verifícalo, no lo des por cierto)
Acciones en Assets/FPS/InputSystem_Actions.inputactions (mapa Player), leídas por PlayerInputHandler vía InputSystem.actions:
Move WASD/flechas · Look ratón · Fire M1 · Aim M2 · Jump Espacio · Sprint Shift izq. · Crouch C · Dash Ctrl izq. · Reload R · Previous 1 · Next 2 · NextWeapon Q/rueda · Interact E · CollectWeapon E · ThrowWeapon G · Melee F · Overdrive X (sin uso: OVERDRIVE es automático) · Attack M1 y Enter. Mapa UI: Menu = P, Tab, Start, Select.
Problemas ya vistos que debes confirmar:
- E está en Interact y en CollectWeapon a la vez; además la estación de montaje y la ejecución del T-07 usan E.
- En mando, buttonWest está en Attack y Reload, buttonNorth en Interact y NextWeapon, y el dpad arriba y abajo en NextWeapon.
- Dash en Ctrl izquierdo (estira el meñique) junto a Shift (sprint) y C (agacharse).
- Hay teclas leídas a mano con Keyboard.current/Mouse.current, que no responderán a la reasignación: WeaponMountStation.cs (mantener E/Espacio para saltar), HangarIntroCinematic.cs y Scripts/Menu (zona protegida: anótalo, no lo toques).
- Los textos en pantalla llevan la tecla escrita a mano ("[E] ACOPLAR…", "[F] GOLPE…", ayudas del HUD), así que quedarán mal al reasignar.
- No hay código de reasignación. RequiemSettings (Bridges/Game) ya guarda volumen, sensibilidad y pantalla completa en PlayerPrefs. InGameMenuManager (plantilla) es el menú de pausa.

## Fases y paradas
- F0 Inventario (solo lectura): todas las acciones y bindings por dispositivo; cada lectura directa de teclas (grep de Keyboard.current, Mouse.current, Gamepad.current, wasPressedThisFrame, GetKey); cada texto en pantalla que nombra una tecla (prefabs, escenas, TMP y strings en código); quién consume cada acción y en qué contexto (gameplay, estación, ejecución, cinemática, menús). Tabla de conflictos por contexto.
- F1 Propuesta de esquema → PARADA. Entre 2 y 3 esquemas por defecto (por ejemplo "Clásico" y "Ágil") para teclado+ratón y mando:
  - tabla de acción → tecla, con la razón de cada cambio;
  - mapa de calor de la mano izquierda (qué dedo pulsa qué);
  - conflictos resueltos;
  - qué acciones se unifican o desaparecen (por ejemplo, Interact y CollectWeapon como una E contextual con prioridad clara);
  - opciones de mantener o alternar (toggle) para sprint, agacharse y apuntar.
  Criterios:
  - lo frecuente bajo los dedos en reposo (WASD, Espacio, Shift, Q, E, F, botones del ratón);
  - nada que obligue a soltar el movimiento para una acción de combate;
  - mismas teclas para el mismo verbo en toda la partida;
  - mando jugable de principio a fin.
  Espero mi elección antes de tocar el .inputactions.
- F2 Esquema elegido: cambia los bindings por defecto en el asset (con respaldo), elimina duplicados, migra las lecturas directas de tecla fuera de la zona protegida a acciones, y haz que todos los textos de ayuda muestren la tecla real con GetBindingDisplayString (un único helper reutilizable, con iconos o texto para teclado y mando).
- F3 Diseño de la interfaz → PARADA. 2 maquetas (capturas o imágenes) del panel "CONTROLES" con el estilo de IronRequiemUiTheme y del HUD nuevo. Contenido:
  - lista por categorías (Movimiento, Combate, Armas, Interacción, Sistema);
  - columnas Principal, Secundaria y Mando;
  - estado "Pulsa una tecla… (Esc cancela)";
  - aviso de conflicto con opción de intercambiar;
  - restablecer la acción y restablecer todo;
  - sensibilidad del ratón y del mando, invertir Y, mantener o alternar;
  - navegable entera con ratón, teclado y mando.
- F4 Implementación tras aprobar la maqueta:
  - reasignación interactiva con PerformInteractiveRebinding (excluir Esc, ratón X/Y y lo que el inventario marque; soportar las partes del compuesto WASD);
  - guardado con SaveBindingOverridesAsJson/LoadBindingOverridesFromJson sobre el MISMO asset que usa PlayerInputHandler, cargado al arrancar antes del primer frame de juego;
  - integrado en RequiemSettings, sin crear un segundo sistema de ajustes;
  - el panel se abre desde la pausa (InGameMenuManager);
  - añadir la entrada al menú principal (IntroMenu) es zona protegida: propónlo y hazlo solo si lo apruebo, con un cambio mínimo y hashes del resto iguales;
  - durante la reasignación, el gameplay no recibe la tecla.
- F5 Verificación y revisión independiente.

## Skills por tarea
- Acciones, bindings, reasignación y guardado: gamedev:unity-input-system, gamedev:input-systems (verifica cada API en Input System 1.19.0).
- Esquema cómodo para FPS rápido y mando: gamedev:fps-shooter, gamedev:game-feel, iron-requiem-fps.
- Diseño del panel y navegación (ratón, teclado y mando): gamedev:game-ui-ux, unity:ui → unity:ui-ugui.
- Persistencia de ajustes: gamedev:save-systems.
- Código: gamedev:unity-csharp-scripting.
- Textos y fuentes TMP: unity:optimize-text-mesh-pro.
- Búsqueda de textos y referencias en prefabs/escenas: unity:generate-editor-search-query.

## Subagentes (model por rol)
1. INVENTARIO (haiku, solo lectura, en paralelo): A acciones y bindings; B lecturas directas de teclas; C textos con teclas en prefabs, escenas y código.
2. DISEÑADOR DE ESQUEMA (opus): F1. Escribe solo Docs/IronRequiem/CONTROLS_V01.md.
3. DISEÑADOR DE UI (opus): maquetas de F3 en Docs/IronRequiem/Concepts/UI/Controls/.
4. IMPLEMENTADOR INPUT (sonnet): .inputactions, migración de lecturas directas, helper de textos y servicio de reasignación (Assets/IronRequiem/Scripts/Input/). Archivos exclusivos.
5. IMPLEMENTADOR UI (sonnet): panel, prefab y su script de autoría (Assets/IronRequiem/Scripts/UI/Controls/, Prefabs/UI/, Editor/Controls/). Archivos exclusivos.
6. REVISOR (opus, independiente): juega con el esquema nuevo y busca conflictos, textos desactualizados y regresiones.
Solo tú controlas el Editor (compilar, Play Mode, guardar).

## Reglas
- Respaldo en Tools/IronRequiem/Backups/<fecha>/ (.inputactions, scripts y prefabs tocados).
- No guardes escenas de campaña ni de Map01 con cambios de Play.
- Zona protegida intacta salvo lo que yo apruebe: IntroMenu, Scripts/Menu, Scripts/HangarIntro, Editor/HangarIntro, cinta y pasarela.
- Los bloqueos de entrada de ARMS_FIX (ejecución, estación y cambio de arma) deben seguir funcionando con cualquier tecla asignada.
- La acción Overdrive (sin uso) no se borra sin mi aprobación: proponlo.

## Entrega
- CONTROLS_V01.md con esquema final, razones, tabla teclado/ratón y mando, y la lista de cambios con archivo:línea.
- Capturas del panel (lista, esperando tecla, conflicto y restablecer) y de las ayudas del HUD con una tecla reasignada.
- Tests:
  - EditMode: conflictos e intercambio; guardar y cargar overrides; restablecer.
  - PlayMode con dispositivos virtuales: reasignar Melee a V, que V golpee y que F ya no lo haga; que el texto de ayuda muestre V; que persista tras reiniciar la escena; que los bloqueos de ejecución y de estación sigan activos.
- Verificación separada en compilación / tests / prueba manual con teclado, ratón y mando.
- Registro en CHECKLIST_TRABAJO.md.
```

---

## 11. Linterna y lectura de enemigos en zonas oscuras

```text
Lee primero Docs/IronRequiem/PROMPTS_CHATS.md (reglas comunes, subagentes y política de skills) y aplícalo.
Lee también DESIGN_V02_UPDATE.md, Map01/MAP01_DESIGN.md, Map01/05_ILUMINACION.md (presupuesto de luces, ambiente mínimo y colores reservados), Map01/02_DECORACION.md (D1.8 emisivos y restos de robot), Map01/04_PUERTAS.md (rojo/ámbar/verde reservados al estado de puertas), Map01/00_INVENTARIO.md (shader HardSurface: _RIM, _PANELS, _PanelBoost), CHECKLIST_TRABAJO.md (entrada "Visibilidad" de BigBot/T07/HoverBot/Turret) y, si existe, Docs/IronRequiem/CONTROLS_V01.md.

# LINTERNA Y FORMA DE VER A LOS ENEMIGOS EN LA OSCURIDAD

Objetivo: que en las zonas oscuras el jugador siempre pueda localizar y leer a los enemigos (dónde están, hacia dónde miran, si van a atacar) sin que el mapa pierda su ambiente y sin romper el rendimiento. No cambies el render pipeline ni el asset URP, ni el balance, la IA o los tiempos de armas sin mi aprobación. No instales paquetes.

## Lo que hay hoy (verifícalo, no lo des por cierto)
- No existe linterna del jugador ni modo de visión.
- Los enemigos (BigBot, T07, HoverBot, Turret) ya tienen emisivos compartidos y ojos/indicadores visibles sin luces.
- El shader IronRequiemHardSurface tiene _RIM (borde fresnel para siluetas en la oscuridad, _RimPower) y _PANELS con _PanelBoost en runtime.
- Map01: URP Forward, ≤ 8 anclajes de luz por cuarto y ≤ 4 por objeto; sombras solo en Spot; bloom con umbral 1,0 y emisivos ≤ 4 HDR; el ambiente nunca baja de ≈ 40 % de la ruta.
- Zonas oscuras conocidas: túneles de mantenimiento (R10, rojo de emergencia y franjas), esclusas Transfer antiguas (lum 0,011–0,040) y lo que marque 05_ILUMINACION.
- Rojo, ámbar y verde están reservados a las puertas. Los restos de robot no llevan emisivo ni rim para no confundirse con enemigos vivos.
- MapFxAudit ya mide activePointSpotLights, batches y SetPass por cuarto.

## Fases y paradas
- F0 Inventario (solo lectura):
  - medir la luminancia en el lugar donde aparece cada enemigo, cuarto por cuarto en Map01 y en la campaña actual, y listar los puntos donde un enemigo a 10–25 m no se distingue del fondo (captura + valor);
  - registrar los materiales y emisivos de cada enemigo y si usan _RIM, y anotar cómo se aplican: material compartido o MaterialPropertyBlock;
  - anotar cuántas luces activas hay por cuarto contra el presupuesto, y si las luces adicionales del URP proyectan sombras;
  - ver cómo se renderizan la WeaponCamera y las manos/armas, y si la luz de una linterna las quemaría;
  - revisar las acciones libres en InputSystem_Actions y el estado del chat 10 (controles).
- F1 Propuestas → PARADA. Entre 2 y 3 soluciones, con captura de prueba de cada una en el mismo punto oscuro. Por ejemplo:
  - A, linterna de hombro o casco: Spot con cookie de lente, alternar con una tecla, alcance y ángulo de FPS rápido, y sombra solo si el presupuesto lo permite;
  - B, pulso de escáner: un eco que recorre el cuarto y resalta los contornos de los enemigos unos segundos (_RIM o _PanelBoost por MaterialPropertyBlock), con recarga;
  - C, lectura pasiva: el rim y los ojos del enemigo suben de forma automática según la oscuridad local, sin acción del jugador;
  - o una combinación, como A + C.
  Para cada una, compara:
  - qué tan bien se lee un enemigo a 5, 15 y 25 m;
  - el coste en luces, SetPass y ms de GPU;
  - si encaja con un FPS rápido tipo DOOM/ULTRAKILL;
  - si necesita un recurso nuevo (batería o energía): solo con mi aprobación;
  - si los enemigos deberían notar la linterna: solo como propuesta, la IA es del chat 8.
  Espero mi elección antes de implementar.
- F2 Implementación de lo elegido:
  - componente propio bajo Assets/IronRequiem/Scripts/Player/Vision/ (o el dominio que encaje);
  - acción nueva en el Input System integrada en el sistema de reasignación del chat 10 si ya existe, y su ayuda en pantalla con la tecla real (helper de GetBindingDisplayString);
  - sonido de encendido/apagado o del pulso por el mixer existente;
  - indicador mínimo en el HUD solo si hace falta, con el estilo de IronRequiemUiTheme;
  - la luz o el resaltado no usan rojo, ámbar ni verde; los restos de robot y la decoración nunca se resaltan;
  - opción en RequiemSettings si procede (por ejemplo, intensidad del resaltado o accesibilidad "contornos siempre activos").
- F3 Ajuste por zona: en los cuartos donde F0 detectó enemigos ilegibles, corrige con lo elegido y, solo si no basta, propón (no apliques) ajustes de luz en 05_ILUMINACION respetando el presupuesto.
- F4 Verificación y revisión independiente.

## Skills por tarea
- Diseño de visibilidad en FPS rápido y sensación: gamedev:fps-shooter, gamedev:game-feel, iron-requiem-fps.
- Lectura de enemigos, telegrafía y (si se aprueba) reacción a la luz: iron-requiem-enemy-ai, gamedev:game-ai.
- Luz, cookie, rim y pulso sin editar el shader: gamedev:shader-programming; post-proceso solo si la opción lo usa: unity:urp-postprocessing.
- Zonas oscuras y presupuesto de luces por cuarto: gamedev:level-design, iron-requiem-performance, gamedev:performance-optimization.
- Entrada nueva y su texto: gamedev:unity-input-system, gamedev:input-systems.
- Indicador de HUD: gamedev:game-ui-ux, unity:ui → unity:ui-ugui.
- Sonido del encendido o pulso: gamedev:audio-design, unity:audio-setup-mixers.
- Código: gamedev:unity-csharp-scripting.

## Subagentes (model por rol)
1. INVENTARIO (haiku, solo lectura, en paralelo): A luminancia y luces por cuarto; B materiales, emisivos y rim de enemigos; C entrada, WeaponCamera y HUD.
2. DISEÑADOR (opus): F1. Escribe solo Docs/IronRequiem/VISION_V01.md con propuestas, capturas y números.
3. ARTISTA DE LUZ (sonnet): prefab de la linterna o del pulso, cookie, VFX y sonido (Assets/IronRequiem/Prefabs/Player/Vision/, Art/Vision/, Audio/SFX/Vision/). Archivos exclusivos.
4. IMPLEMENTADOR (sonnet): scripts, acción de entrada y opción de ajustes (Assets/IronRequiem/Scripts/Player/Vision/, Editor/Vision/). Archivos exclusivos.
5. REVISOR (opus, independiente): juega las zonas oscuras con y sin la mecánica y busca enemigos ilegibles, deslumbramiento, falsos resaltados y caídas de rendimiento.
Solo tú controlas el Editor (compilar, Play Mode, guardar).

## Reglas
- Respaldo en Tools/IronRequiem/Backups/<fecha>/ de todo lo que toques.
- No modifiques materiales compartidos de enemigos: usa MaterialPropertyBlock o variantes nuevas.
- No guardes escenas de campaña ni de Map01 con cambios de Play.
- Zona protegida intacta: IntroMenu, Scripts/Menu, Scripts/HangarIntro, Editor/HangarIntro, cinta y pasarela.
- La linterna cuenta en el presupuesto de luces: con ella encendida, ningún cuarto supera su límite (verifícalo con MapFxAudit).
- No subas el ambiente global para "arreglar" la oscuridad.

## Entrega
- VISION_V01.md con la opción elegida, parámetros finales, coste medido y la lista de cambios con archivo:línea.
- Capturas antes/después en al menos 4 puntos oscuros (incluidos R10 y una esclusa), con enemigos a 5, 15 y 25 m, y una con el arma disparando para comprobar que no deslumbra.
- Tests:
  - EditMode: la acción existe y tiene binding en teclado y mando; los colores no usan rojo/ámbar/verde reservados; los restos de robot no reciben resaltado.
  - PlayMode con entrada virtual: encender y apagar (o lanzar el pulso) con la tecla; el enemigo recibe el resaltado y lo pierde al terminar o al morir; las luces activas del cuarto no superan el presupuesto; sin errores al cambiar de escena.
- Rendimiento medido con MapFxAudit en el peor cuarto, con la mecánica activa y sin ella.
- Verificación separada en compilación / tests / prueba manual.
- Registro en CHECKLIST_TRABAJO.md.
```

## 12. Pase visual con Fable: decoración, luminosidad, luces de robots y animación del Carroñero

Para ejecutar con **Fable** (claude-fable-5-2). Sustituye al prompt 11 en lo de la linterna: si el 11 no se ha corrido, no lo corras.

```text
Lee primero Docs/IronRequiem/PROMPTS_CHATS.md (reglas comunes, subagentes y política de skills) y aplícalo. El prompt 11 de ese archivo es la especificación de referencia para la linterna; este chat lo sustituye.
Lee también AGENTS.md, DESIGN_V02_UPDATE.md, CHECKLIST_TRABAJO.md (entradas "Visibilidad", "T07 resplandor excesivo y ojos rojos", "Pase visual F0/F1", "v25 Carroñero"), VISUAL_AUDIT_F0.md, Map01/MAP01_DESIGN.md, Map01/02_DECORACION.md, Map01/05_ILUMINACION.md, Map01/04_PUERTAS.md y, si existe, CONTROLS_V01.md.

# PASE VISUAL: MAPA MÁS LEGIBLE, DECORACIÓN NUEVA, LUCES DE ROBOTS Y CARROÑERO

## Contexto del mundo (guía toda la dirección de arte)
- El jugador es un robot: un experimento militar que fue abandonado. Despierta en las profundidades de un búnker militar abandonado y lo recorre luchando contra los robots que ahora lo dominan.
- El mapa es una subida: poco a poco salimos hacia la superficie. Cada zona está algo más cerca del exterior que la anterior.
- La decoración y la luz deben contar esa subida:
  - abajo, laboratorios y zonas de experimentación (cápsulas, bancos de pruebas, registros del proyecto), aislamiento, luz artificial de emergencia y tecnología militar intacta pero muerta;
  - en medio, infraestructura del búnker (armerías, almacenes, mantenimiento, agua, energía), cada vez más dañada y tomada por los robots;
  - arriba, señales del exterior (polvo y escombros que entran, grietas, aire y luz natural fría que se filtran, tierra y restos de la guerra en la superficie) hasta la salida.
- La luminosidad puede seguir esa misma progresión: un poco más de luz real a medida que subimos, sin que ninguna zona baje del mínimo legible.
- Los robots enemigos han ocupado el búnker: marcas de su presencia (chatarra apilada, cables arrancados, piezas canibalizadas, nidos de reparación) en lugar de decoración militar ordenada.
- El protagonista es un robot entre robots: la decoración nunca debe parecer un refugio humano habitado. Los humanos solo aparecen como rastro del pasado: carteles, estarcidos, equipo militar, registros.
- Si un documento existente contradice este contexto (zonas, orden o temática), señálalo en F0 y pregúntame; no lo cambies por tu cuenta.

Quejas reales del jugador (yo), en orden de prioridad:
1. El juego sigue demasiado oscuro: quiero más luminosidad o una linterna (o las dos).
2. Las luces de los robots se ven raras.
3. Quiero luz roja en los ojos de los robots.
4. La decoración del mapa necesita un rediseño: hoy no transmite un búnker militar abandonado, tomado por los robots, del que vamos subiendo hacia la superficie.
5. Las animaciones del Carroñero son pobres.
No cambies balance, daño, IA, tiempos de armas, render pipeline, versión de URP ni Input System. No instales paquetes, no compres ni generes modelos con créditos.

## Lo que hay hoy (verifícalo, no lo des por cierto)
- Iluminación de Map01 (05_ILUMINACION): ambiente Trilight por zona, niebla exp², Volume por zona sobre Campaign_PostProfile (Bloom umbral 1,0 y clamp 12, StopNaN en las cámaras), ≤ 8 luces por cuarto y ≤ 4 por objeto, URP Forward, ambiente ≥ 40 % de la ruta (28 en oscuridad intencional) y 4 apagones dramáticos. Rojo, ámbar y verde están reservados al estado de las puertas.
- No hay linterna.
- Luces de robots:
  - EnemyStateLights (Scripts/VisualOverhaul) cambia el color de ojos e indicadores según el estado de la IA: Patrulla cian, Alerta ámbar, Ataque rojo, Aviso blanco parpadeante a 14 Hz, y se apagan al morir;
  - EnemyPowerIndicators apaga los renderers al morir;
  - el T-07 y el SawBot ya llevan un material rojo URP Unlit sin HDR en el ojo y el CoreIndicator;
  - BigBot, HoverBot y Turret usan emisivos compartidos;
  - al Carroñero se le quitaron EnemyStateLights y otros extras por rendimiento;
  - hubo fallos de resplandor gigante por NaN, HDR y escalas heredadas: no los reintroduzcas.
- Carroñero: Prefabs/Campaign/Carronero.prefab, malla Tripo de 22 huesos con 4 LOD, todo procedural en Scripts/Enemies/CarroneroMotion.cs (105 líneas, sin Animator, no posa fuera de cámara): paso, brazo de sierra, agacharse y hurgar. Va en hordas de hasta 20–24, con LateUpdate de ≈ 1,25 ms para 24 en el Editor.
  - CarroneroMotion también lo usan ScavengerBehaviour (Scripts/HangarIntro), BridgeWalker (Scripts/Menu) y CctvActor: son zona protegida y no deben cambiar de aspecto sin mi aprobación.
- Decoración de Map01: lo definen 02_DECORACION (kits IR_*, presupuesto por arena: ≤ 60 DC, ≤ 10 SetPass y ≤ 80 k triángulos; restos de robot oscuros y tumbados) y los scripts de autoría de Editor/Map01. El chat 9 (Mapa 1) puede seguir activo.

## Fases y paradas
- F0 Diagnóstico (solo lectura; Play Mode sin guardar):
  - capturas a 1920×1080 desde puntos fijos y reproducibles (≥ 2 por zona de Map01 y de la campaña actual), con enemigos a 5, 15 y 25 m;
  - luminancia media y de los enemigos frente al fondo en cada captura;
  - catálogo de cada luz de robot (renderer, material, color e intensidad por estado, si tiene halo o bloom, si flota separada de la malla, si parpadea) con una captura por estado; marca qué puede ser lo "raro";
  - el Carroñero grabado en secuencias de frames: andar, correr, girar, atacar, recibir daño, hurgar y morir, con los defectos (patinaje de pies, rigidez, falta de anticipación, brazo que atraviesa la malla, giros bruscos…);
  - la decoración por zona: qué se repite, qué está vacío, qué tapa el combate.
  Entrega Docs/IronRequiem/VisualPassFable/00_DIAGNOSTICO.md con las capturas.
- F1 Dirección de arte → PARADA. Un documento con imágenes (paintovers o mockups sobre las capturas de F0) que proponga:
  - a) Luminosidad: 2 niveles (por ejemplo "+ legible" y "+ luminoso") con valores concretos de ambiente, relleno, exposición y Volume por zona, sin perder la identidad de cada zona; y si va con linterna, la opción de linterna (prompt 11: linterna de hombro, pulso o lectura pasiva) y cómo convive con el nuevo nivel.
  - b) Luces de robots: qué se corrige de lo "raro", con antes y después.
  - c) Ojos rojos: cómo quedan los ojos rojos sin perder la lectura del estado (hoy el color indica el estado). Propón 2 opciones, por ejemplo: ojos siempre rojos y el estado por intensidad y pulso; o ojos rojos e indicador corporal con el color de estado. El rojo de los ojos no debe confundirse con el de las puertas cerradas, y sin Light dinámica por robot en las hordas (emisivo y halo o flare con tope de HDR); como mucho, una luz real solo en el BigBot o los jefes, si el presupuesto lo permite.
  - d) Decoración: concepto por zona que muestre la subida hacia la superficie (moodboard sobre capturas, lenguaje de formas, paleta, 3 landmarks clave, qué se quita y qué se añade, y qué señal del exterior aparece en esa zona), respetando el presupuesto de 02_DECORACION. Incluye un esquema de toda la ruta, de las profundidades a la salida, con la evolución de luz, color y deterioro.
  - e) Carroñero: lista de animaciones nuevas o mejoradas con referencia de timing (por ejemplo, anticipación del ataque ≥ 0,25 s para que se lea).
  Espero mi elección de cada punto antes de implementar.
- F2 Luminosidad y linterna (lo elegido en a). Cambios por zona en los datos de 05_ILUMINACION y en sus scripts de autoría, no a mano en el YAML; la linterna según el prompt 11 (acción nueva integrada con la reasignación del chat 10 si existe).
- F3 Luces de robots y ojos rojos (b y c): por MaterialPropertyBlock o con variantes nuevas de material, sin tocar los materiales compartidos que usan otros. Que el ojo se apague al morir y que no haya resplandor gigante (comprobar con Bloom activo).
- F4 Decoración (d), una zona cada vez: captura antes/después y presupuesto medido antes de pasar a la siguiente. Usar los kits y scripts de autoría existentes, o ampliarlos. Sin piezas flotantes ni que tapen spawns, puertas o la ruta.
- F5 Carroñero (e):
  - mejora con un componente nuevo o una ampliación compatible de CarroneroMotion: si cambias la API o el aspecto que ven HangarIntro, Menu o CctvActor, para y pide aprobación; si no, que la mejora solo se active en los prefabs de combate;
  - incluye: pies que no patinen (paso sincronizado con la velocidad real del NavMeshAgent, plantado con raycast si cabe en el presupuesto), cambio de peso, inclinación en giros y aceleración, anticipación y recuperación del ataque, reacción al impacto, arranque y frenada, respiración en reposo y mejor muerte si cabe;
  - mismo coste o menor: medir 24 Carroñeros con HordeCostProbe; no posa fuera de cámara; LOD bajo con una animación más simple.
- F6 Verificación y revisión independiente.

## Skills por tarea
- Dirección de arte, look final y puntuación de capturas: unity:craft-game-floors, gamedev:create-game-assets.
- Decoración y composición de zonas: gamedev:level-design, iron-requiem-fps.
- Luminosidad, Volume y bloom: unity:urp-postprocessing; presupuesto de luces y rendimiento: iron-requiem-performance, gamedev:performance-optimization.
- Linterna, entrada y texto de ayuda: gamedev:fps-shooter, gamedev:unity-input-system, gamedev:input-systems.
- Ojos, halos, emisivos y pulso sin editar el shader: gamedev:shader-programming.
- Lectura de estados del enemigo y telegrafía: iron-requiem-enemy-ai, tune-enemy-ai.
- Animación procedural del Carroñero: game-dev-combat-animation (estándar, pruebas por frame y críticos ciegos), gamedev:unity-animation, gamedev:game-feel, build-rigged-game-assets (huesos y sockets).
- Código: gamedev:unity-csharp-scripting.

## Subagentes (model por rol)
1. INVENTARIO (haiku, solo lectura, en paralelo): A capturas y luminancia por zona; B catálogo de luces de robots; C decoración y presupuesto por zona; D secuencias del Carroñero.
2. DIRECTOR DE ARTE (fable): F1, el documento y los mockups en Docs/IronRequiem/VisualPassFable/. No toca Assets.
3. ILUMINACIÓN Y LINTERNA (sonnet): F2, datos y autoría de luz y Scripts/Player/Vision/. Archivos exclusivos.
4. LUCES DE ROBOTS (sonnet): F3, Scripts/VisualOverhaul/ y variantes de materiales de enemigos. Archivos exclusivos.
5. DECORADOR (sonnet): F4, scripts y prefabs de decoración de Editor/Map01/ y Prefabs/Map01/. Archivos exclusivos.
6. ANIMADOR (fable): F5, Scripts/Enemies/ del Carroñero y sus tests. Archivos exclusivos.
7. REVISOR (opus, independiente; no vio la propuesta al hacerse): juega las zonas con capturas antes/después y puntúa legibilidad, ambiente, ojos, decoración y Carroñero del 1 al 10, con defectos concretos. Lo que baje de 8 vuelve a su rol.
Solo tú controlas el Editor (compilar, Play Mode, guardar). Un rol cada vez en Play Mode.

## Reglas
- Respaldo en Tools/IronRequiem/Backups/<fecha>/ de todo lo que toques.
- Escenas: usa las APIs del Editor y los scripts de autoría; no guardes escenas con cambios de Play ni escenas que ya estaban modificadas por mí sin avisar. Coordina con el chat 9: si está activo en Map01, para y pregúntame.
- Zona protegida intacta: IntroMenu, Scripts/Menu, Scripts/HangarIntro, Editor/HangarIntro, cinta y pasarela, ScavengerBehaviour, BridgeWalker y CctvActor.
- Rojo, ámbar y verde siguen reservados a las puertas en la decoración y las luces del mapa; los ojos de los enemigos son la única excepción aprobada, y no usan el mismo patrón que las balizas de puerta.
- Presupuestos: luces por cuarto (con la linterna encendida), DC, SetPass y triángulos de 02_DECORACION, y coste del Carroñero. Mide antes y después con MapFxAudit y HordeCostProbe.
- Si algo es IA generativa (texturas o modelos), anótalo para la declaración de Steam; no se gastan créditos.

## Entrega
- Docs/IronRequiem/VisualPassFable/ con 00_DIAGNOSTICO, 01_DIRECCION (lo aprobado) y 02_RESULTADO (parámetros finales y lista de cambios con archivo:línea).
- Capturas antes/después desde los mismos puntos de F0, con enemigos a 5, 15 y 25 m, y un vídeo o secuencia de frames del Carroñero (andar, girar, atacar, impacto, muerte) antes/después.
- Tests:
  - EditMode: luces por cuarto ≤ presupuesto; sin rojo, ámbar ni verde en la decoración a ≤ 12 m de una puerta; los ojos usan el color aprobado y HDR ≤ al tope; la acción de la linterna tiene binding.
  - PlayMode: los ojos cambian según la solución elegida y se apagan al morir; sin resplandor con Bloom; la linterna enciende y apaga; el Carroñero no patina (desplazamiento del pie plantado ≤ tolerancia), ataca con anticipación medida y sigue sin posar fuera de cámara; la intro y el menú siguen idénticos (hash o captura).
- Rendimiento: peor cuarto y horda de 24 Carroñeros, antes/después.
- Verificación separada en compilación / tests / prueba manual, y registro en CHECKLIST_TRABAJO.md.
```

## 13. Animaciones de ejecución de los robots

```text
Lee primero Docs/IronRequiem/PROMPTS_CHATS.md (reglas comunes, subagentes y política de skills) y aplícalo.
Lee también ARMS_FIX_20261119.md (reglas vigentes de la ejecución del T-07: 2 golpes, bloqueos de entrada), SECONDARY_SHOTGUN_SLIDE_EXECUTION.md, T07_ROBOT_ANIM_20261118.md, SAWBOT_ROBOT_ANIM_20261118.md, MODULAR_ARM_AND_PICKUP.md, DESIGN_V02_UPDATE.md, CONTROLS_V01.md y CHECKLIST_TRABAJO.md.

# EJECUCIONES DE ROBOTS MÁS BRUTALES Y LEGIBLES

Contexto: somos un robot, experimento militar abandonado, que arranca las armas a los robots que dominan el búnker. La ejecución es el momento estrella del juego (estilo DOOM glory kill, pero rápida, sin cortar el ritmo de ULTRAKILL). Hoy se siente pobre: el robot casi no reacciona y la secuencia no tiene peso.
Objetivo: mejorar la animación de TODAS las ejecuciones (lo que hace el robot ejecutado, los brazos del protagonista, la cámara, el impacto, el sonido y los efectos) sin cambiar la regla de ejecución, el daño, el balance, la IA ni los controles.

## Lo que hay hoy (verifícalo, no lo des por cierto)
- Regla: un robot es ejecutable con vida < 75 % (EnemyExecutionSource). Se inicia con la acción de interactuar (E por defecto; mira CONTROLS_V01).
- T-07: DisarmMotion (Scripts/Player, guiado por ProtagonistHandsPresentation): agarre, arranque del cañón (YankTime) y puñetazo (StrikeTime). Exactamente 2 golpes; la muerte llega con el segundo. ExecutionShove empuja al robot hacia atrás tras el golpe.
- SawBot: SawBotWeaponSource (arrancar la sierra) sobre la misma reserva de ejecución.
- Big Bot: BigBotExecutionSequence: salto encima, arranca el cañón, le dispara con él y salto atrás, con el movimiento por AdvancedPlayerMovement.
- Carroñero: sin ejecución (no se añade sin mi aprobación).
- Durante la ejecución el robot se congela: se desactivan EnemyMobile y la IA y se para el NavMeshAgent (StolenWeaponCarrier, BigBotExecutionSequence). Casi no hay animación de reacción del robot.
- Ya existen: RobotHitFeedback, RobotMotionAccents, RobotDamageProgression, T07AnimationDriver, T07FootPlacement, BigBotTurnFootPlant, DetachableCannon, ExecutionProtection (invulnerabilidad), StolenWeaponCarrier y StolenShellFit (absorción del arma, sin un tercer golpe durante la ejecución), y el sacudido de cámara y el hit-stop existentes.
- Bloqueos de entrada de ARMS_FIX: M1, F, 1, 2 y G se ignoran durante la ejecución. Deben seguir funcionando.

## Fases y paradas
- F0 Diagnóstico (solo lectura; Play Mode sin guardar): graba cada ejecución (T-07, SawBot y Big Bot) desde la cámara del jugador y desde una cámara lateral de prueba, como secuencia de frames con tiempos. Anota los defectos: robot rígido o congelado, poses que atraviesan la malla, brazo estirado o roto, mano que no coincide con la unión, saltos de pose, falta de anticipación, golpe sin peso, cámara que marea, tiempos muertos y duración total. Mide la duración de cada ejecución y cuánto tiempo el jugador no controla nada.
- F1 Propuesta → PARADA. Para cada robot, un guion por beats con tiempos (por ejemplo: entrada 0–0,15 s, agarre, forcejeo, arranque, golpe, remate y salida) que describa:
  - qué hace el robot en cada beat (retroceso, intento de soltarse, chispas en la unión, la pieza que cede, caída o derrumbe al morir, sin flotar);
  - qué hacen los brazos y la cámara del jugador;
  - el hit-stop, la sacudida, los efectos y el sonido en cada impacto.
  Muéstralo con storyboard (capturas anotadas o dibujos simples). Criterios:
  - ejecución corta: ≤ 1,2 s en T-07 y SawBot y ≤ 2,0 s en Big Bot, salvo que yo apruebe más;
  - el jugador recupera el control enseguida;
  - legible a 60 FPS y con FOV alto;
  - sin mareo: nada de giros de cámara bruscos fuera del jugador.
  Si propones cambiar el número de golpes o la duración actual, dilo aparte: necesita mi aprobación. Opcional: 1 o 2 variantes por robot según el ángulo de entrada (frente, espalda, lado), solo como propuesta.
  Espero mi elección antes de implementar.
- F2 Implementación del robot ejecutado:
  - capa de reacción por código en los huesos existentes (sin cambiar el Animator de base ni los clips importados): forcejeo, arranque, impacto y derrumbe;
  - la muerte reutiliza el flujo y la destrucción existentes (RobotDamageProgression, BigBotDestruction, la muerte por piezas);
  - el robot sigue sin moverse por su cuenta y el NavMeshAgent no se descontrola; nada termina dentro de muros ni flotando.
- F3 Jugador: brazos (DisarmMotion, ProtagonistHandsPresentation y BigBotExecutionSequence), cámara, hit-stop y sonido:
  - la mano se cierra sobre la unión real y el arma arrancada sigue a la mano sin saltos;
  - mantener el estándar de brazos de ARMS_FIX: sin estirar el brazo, sin corte del hombro visible y el agarre no pasa del alcance máximo;
  - sonido: los clips del proyecto por el mixer existente, sin descargas.
- F4 Verificación y revisión independiente.

## Skills por tarea
- Estándar de animación de combate, pruebas por frame y críticos ciegos: game-dev-combat-animation.
- Huesos, Animator y capas por código: gamedev:unity-animation, build-rigged-game-assets.
- Peso del impacto, hit-stop, sacudida y tiempos: gamedev:game-feel, design-action-combat, gamedev:fps-shooter.
- Reglas del juego y de los enemigos: iron-requiem-fps, iron-requiem-enemy-ai, iron-requiem-modular-weapons.
- Efectos de chispas y piezas: create-game-vfx; sonido: gamedev:audio-design, unity:audio-setup-mixers.
- Código: gamedev:unity-csharp-scripting. Rendimiento: iron-requiem-performance.

## Subagentes (model por rol)
1. INVENTARIO (haiku, solo lectura, en paralelo): A T-07; B SawBot; C Big Bot. Grabación de frames y lista de defectos.
2. DIRECTOR DE ANIMACIÓN (opus): F1, guiones y storyboard en Docs/IronRequiem/Executions/. No toca Assets.
3. ANIMADOR DE ROBOTS (sonnet): F2, reacciones del robot en Assets/IronRequiem/Scripts/Enemies/Execution/. Archivos exclusivos.
4. ANIMADOR DEL JUGADOR (sonnet): F3, brazos, cámara y sonido (DisarmMotion, ProtagonistHandsPresentation, BigBotExecutionSequence y lo nuevo en Scripts/Player/Execution/). Archivos exclusivos.
5. REVISOR (opus, independiente): mira las secuencias antes/después sin conocer el guion y puntúa del 1 al 10 el peso, la legibilidad, los brazos, la reacción del robot y el mareo, con defectos concretos. Lo que baje de 8 vuelve a su rol.
Solo tú controlas el Editor (compilar, Play Mode, guardar).

## Reglas
- Respaldo en Tools/IronRequiem/Backups/<fecha>/ de todo lo que toques.
- No cambies la regla de < 75 %, el daño, la vida, la IA, el inventario ni los controles.
- No guardes escenas con cambios de Play.
- Zona protegida intacta: IntroMenu, Scripts/Menu, Scripts/HangarIntro, Editor/HangarIntro, cinta y pasarela.
- Bloqueos de ARMS_FIX y la invulnerabilidad (ExecutionProtection) intactos; cancelar la ejecución (muerte del jugador, pausa, cambio de escena) no deja residuos.
- No edites los FBX fuente ni los clips importados: usa copias o capas por código.
- Coste: mismo presupuesto de partículas y luces que hoy (sin Light nueva por ejecución, salvo un flash breve aprobado).

## Entrega
- Docs/IronRequiem/Executions/EXECUTIONS_V02.md: guion final por robot, tiempos, parámetros y lista de cambios con archivo:línea.
- Secuencias de frames antes/después de cada ejecución (vista del jugador y lateral).
- Tests PlayMode con entrada real del Input System:
  - en cada robot: la ejecución empieza con la tecla, número de golpes y muerte en el tiempo del guion (±0,05 s), una sola muerte y ningún golpe extra;
  - duración ≤ al límite aprobado y el jugador recupera el control;
  - el robot no termina dentro de un muro ni flotando, y la mano no pasa del alcance máximo;
  - M1, F, 1, 2 y G siguen ignorados durante la ejecución;
  - cancelar a mitad (muerte o pausa) no deja residuos.
- Además, los tests existentes de ARMS_FIX y de ejecución siguen en verde.
- Verificación separada en compilación / tests / prueba manual, y registro en CHECKLIST_TRABAJO.md.
```

## Conflictos entre chats

| Chats | Comparten | Regla |
|---|---|---|
| 2 y 6 | Rig, HandPoses, prefabs de armas, WeaponMountStation | Primero 6 (define el estándar de acople), después 2 |
| 2 y 1 | Animaciones del protagonista | No a la vez |
| 3 y 4 | Sprites de UI y retrato | 4 coordina; 3 solo si 4 no está tocando Art/UI |
| 7 y 5/8 | Escenas de campaña, NavMesh | No a la vez |
| 9 y 7 | Mapa y puertas | 9 sustituye a 7 para mapa y puertas; usar 7 solo para auditar efectos |
| 9 y 5/8 | Encuentros, NavMesh, sistemas de campaña | No a la vez; 8 y 5 después de F2 de 9 |
| 10 y 4/5 | Textos de ayuda del HUD, InputSystem_Actions, PlayerInputHandler | No a la vez; 10 después de que 4 cierre el HUD nuevo |
| 11 y 10 | InputSystem_Actions, ayudas con tecla, RequiemSettings | 11 después de que 10 cierre (o reservar la acción con 10) |
| 11 y 9/8 | Luces de Map01, materiales de enemigos | 11 no cambia la iluminación de 9 ni la IA de 8: solo propone |
| 12 y 11 | Linterna | 12 sustituye a 11: no correr los dos |
| 12 y 9 | Decoración e iluminación de Map01 | No a la vez; 12 después de que 9 cierre su fase de decoración e iluminación |
| 12 y 8/2 | Carroñero, luces de estado de enemigos | 12 no toca la IA; no a la vez que 8 |
| 13 y 2/1/12 | Brazos del protagonista, DisarmMotion, animación de robots | No a la vez |
| Cualquiera | Editor de Unity | Un solo chat con Play Mode/guardado a la vez |
