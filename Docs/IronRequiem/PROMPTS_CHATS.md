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

---

## Conflictos entre chats

| Chats | Comparten | Regla |
|---|---|---|
| 2 y 6 | Rig, HandPoses, prefabs de armas, WeaponMountStation | Primero 6 (define el estándar de acople), después 2 |
| 2 y 1 | Animaciones del protagonista | No a la vez |
| 3 y 4 | Sprites de UI y retrato | 4 coordina; 3 solo si 4 no está tocando Art/UI |
| 7 y 5/8 | Escenas de campaña, NavMesh | No a la vez |
| 9 y 7 | Mapa y puertas | 9 sustituye a 7 para mapa y puertas; usar 7 solo para auditar efectos |
| 9 y 5/8 | Encuentros, NavMesh, sistemas de campaña | No a la vez; 8 y 5 después de F2 de 9 |
| Cualquiera | Editor de Unity | Un solo chat con Play Mode/guardado a la vez |
