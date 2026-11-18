# IRON REQUIEM — informe de configuración

Fecha: **28 de octubre de 2026**, zona America/La_Paz. Trabajo realizado en el proyecto existente `C:/Users/gehbd/ProyectoGameFinal`.

**Configuración verificada:** DOTween **1.3.045** está instalado, configurado y cargado, con `DOTween.Modules` compilado y ajustes guardados en `Assets/Resources/DOTweenSettings.asset`. La verificación final del **28 de octubre a las 12:31, America/La_Paz**, aprobó **13/13 comprobaciones** (`all_checks_pass: true`). Se retiró la solicitud temporal completada para que no se repita al abrir Unity. Permanecen las decisiones documentadas de no instalar Pipeline experimental, posponer VFX Graph y dejar Meshy sin autenticar por elección del usuario.

## Estado de la base

Unity Editor **6000.5.5f1** confirmado en ProjectVersion y en la auditoría del Editor en ejecución. URP **17.5.0**, Input System **1.19.0**, entrada activa Input System, escena activa **MainScene**. La auditoría final posterior a DOTween encontró **0 scripts ausentes**, escena sin cambios pendientes y sin errores de compilación capturados.

La plantilla mantiene `PlayerCharacterController`, `PlayerInputHandler`, `PlayerWeaponsManager`, `WeaponController`, Health/Damageable, EnemyController/EnemyMobile/EnemyTurret y sus sistemas de UI, audio y objetivos. Las nueve acciones consultadas por PlayerInputHandler existen en el asset de entrada.

No había AGENTS.md ni IRON_REQUIEM_GDD_v0.1.md en el proyecto. Se creó AGENTS.md sin sustituir instrucciones anteriores.

## Plugins y herramientas

| Elemento | Estado real | Evidencia / limitación |
|---|---|---|
| Plugin oficial Unity para Codex | Instalado y habilitado, **0.1.8-beta** | `codex plugin list --json`, manifiesto y caché oficial comprobados; es la versión del plugin, no del Editor ni de un paquete UPM |
| Unity CLI | Ya instalada, **1.0.0-beta.14**, verificada | Comprobación oficial: versión más reciente en su canal instalado; no se cambió el Editor |
| Skills oficiales Unity | **32 archivos de skill instalados** | Caché `C:/Users/gehbd/.codex/plugins/cache/unity-agent-plugin/unity/0.1.8-beta/skills` |
| Conexión MCP con Unity | **Pendiente; no conectada** | Requiere com.unity.pipeline. Todas las versiones publicadas consultadas son experimentales, hasta 0.9.1-exp.1; no se instaló por la regla de paquetes estables |
| Meshy CLI | Instalado y ejecutado, **0.4.3** | `meshy --version`; Node **24.20.0**, npm **11.19.0** cumplen requisitos |
| Skill oficial Meshy | Instalada: **meshy-3d-generation** | `C:/Users/gehbd/.codex/skills/meshy-3d-generation`; fuente oficial meshy-dev/meshy-3d-agent |
| Plugin Meshy del directorio | No instalado como plugin de catálogo | La búsqueda disponible no devolvió una entrada; se instalaron la skill oficial y su CLI por el procedimiento compatible |
| Sesión Meshy | **Pendiente por elección del usuario** | Auth status indicó no autenticado. La revisión automática bloqueó iniciar el acceso y el usuario eligió dejarlo pendiente. No se generaron modelos ni se consumieron créditos |
| Blender | Ya instalado, **5.2.3 LTS**, ejecutado | Inspección, exportación de copia y reimportación verificadas |

Skills oficiales de interés para este proyecto: unity-cli, unity-package-management, physics-3d-collision, initialize-ai-navigation, shader-graph-create-custom-node, validate-urp-render-graph-renderer-feature, urp-postprocessing, audio-setup-mixers, optimize-audio, asset-transformer-toolkit y generate-editor-search-query. La lista completa está en `Tools/IronRequiem/Reports/tooling-installation.json`. No se añadieron herramientas de monetización ni servicios de pago al proyecto.

La clonación Git del catálogo oficial falló con una interrupción RPC/EOF. Se descargó el ZIP directamente del repositorio oficial de Unity Technologies y se registró como catálogo local compatible; el plugin se instaló desde él. La copia fuente permanece en `Tools/IronRequiem/Downloads/unity-agent-plugin-main` y está ignorada por Git: conservarla mientras sea la fuente registrada del catálogo. La instalación en caché está separada. Actualizar este catálogo local no equivale a un `git pull` automático.

Las cuatro skills locales pasaron `quick_validate.py` y **ya aparecen en el catálogo activo de Codex**, junto con meshy-3d-generation, en el turno de continuación. Los 32 archivos del plugin Unity están instalados; si su catálogo completo no aparece en la selección, recargar Codex. No se reinició Codex ni Unity automáticamente.

## Paquetes de Unity

| Paquete | Versión | Estado |
|---|---|---|
| Animation Rigging — com.unity.animation.rigging | **1.4.1** | Instalado mediante UnityEditor.PackageManager.Client.Add; registrado y compilado |
| Cinemachine — com.unity.cinemachine | **3.1.7** | Instalado mediante UPM; registrado y compilado. No se añadió CinemachineBrain ni se cambió la cámara FPS |
| Splines — com.unity.splines | **2.9.0** | Dependencia estable añadida por Cinemachine |
| ProBuilder — com.unity.probuilder | **6.1.2** | Ya instalado; conservado |
| AI Navigation — com.unity.ai.navigation | **2.0.14** | Ya instalado; conservado junto con NavMeshComponents de la plantilla |
| URP / Core / Shader Graph | **17.5.0** | Ya instalados; conservados |
| Input System | **1.19.0** | Ya instalado; conservado |
| Test Framework | **1.7.0** | Ya instalado; conservado |
| Profile Analyzer | **1.4.0** | Ya instalado; conservado |
| DOTween Free | **1.3.045** | Instalado, configurado y verificado: biblioteca cargada, ajustes persistidos y DOTween.Modules compilado |
| Visual Effect Graph | No instalado | Preparado para uso posterior; no existe un efecto inmediato que lo necesite |

No cambiaron las versiones de ninguna dependencia directa ni de las dependencias bloqueadas preexistentes. `com.unity.ai.assistant` **2.21.0-pre.2** ya estaba en el proyecto: se conservó; no fue una instalación preview realizada por esta tarea.

DOTween se obtuvo exclusivamente de [Demigiant](https://dotween.demigiant.com/download.php), que declara compatibilidad con Unity 6. Está bajo `Assets/Plugins/Demigiant/DOTween`. En esta instalación nueva no se copiaron los tres archivos DOTweenUpgradeManager del actualizador de versiones antiguas; se conservan en el ZIP y carpeta de descarga. No se borró ningún asset existente.

Su configuración usa las operaciones del proveedor: inicialización de DOTweenSettings si falta, aplicación de módulos y creación de `Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef`. Se verificaron tanto el archivo de ajustes como la carga real del ensamblado en Unity; la mera existencia de archivos descargados no se tomó como instalación completa.

VFX Graph 17.5 es compatible con la versión de gráficos del Editor. La auditoría confirma una **NVIDIA RTX 4050 Laptop GPU** con compute shaders y el proyecto usa color lineal. Aun así, la [documentación de VFX Graph 17.5](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.5/manual/System-Requirements.html) mantiene su soporte URP en preview con limitaciones de plataforma. Se dejó para una tarea de VFX concreta, preservando la configuración actual.

## Skills propias y utilidades

| Skill | Archivo |
|---|---|
| iron-requiem-fps | `.agents/skills/iron-requiem-fps/SKILL.md` |
| iron-requiem-enemy-ai | `.agents/skills/iron-requiem-enemy-ai/SKILL.md` |
| iron-requiem-modular-weapons | `.agents/skills/iron-requiem-modular-weapons/SKILL.md` |
| iron-requiem-performance | `.agents/skills/iron-requiem-performance/SKILL.md` |

Se crearon `AGENTS.md`, `Assets/IronRequiem/Editor/IronRequiemSetupTools.cs`, `Tools/IronRequiem/Blender/blender_asset_tool.py`, su guía USAGE.md y `Tools/IronRequiem/verify_setup.py`. Son instrucciones y herramientas de configuración; no añaden mecánicas de gameplay.

La utilidad del Editor usa una cola de solicitudes locales con operaciones limitadas y versiones permitidas, sin servidor ni ejecución arbitraria de código. Instala por UPM, audita y configura el paquete gratuito. Su menú `Tools > Iron Requiem > Audit Project and T-07 (Read Only)` genera el informe sin modificar escenas. Tras cada instalación se esperó resolución y compilación antes de continuar con el siguiente paquete.

## Protección y verificación

Git tiene raíz en **C:/Users/gehbd** y este proyecto figuraba como no seguido. No se inicializó otro repositorio, se hicieron commits ni se alteraron cambios anteriores.

Respaldo: `Tools/IronRequiem/Backups/20261028-111809/`, con Packages, ProjectSettings, configuración original de Codex y hashes de **1.651 archivos originales**, incluidos **1.617 archivos de Assets**. Backups y Downloads están ignorados para evitar subir configuración privada y copias temporales. No publicar el respaldo de Codex.

La comparación final confirmó intactos los **1.617 archivos originales de Assets**: escenas, prefabs, materiales, scripts, FBX y metadatos. De los 1.651 archivos originales de la instantánea, solo manifest y packages-lock cambiaron; ProjectSettings se conservó íntegro. Se añadieron archivos nuevos de configuración y herramientas. Las **13 comprobaciones aprobadas** están en `Tools/IronRequiem/Reports/setup-verification.json`.

Se verificaron compilación del Editor, UPM y registro de paquetes, referencias de scripts de la escena activa, las nueve acciones de entrada, hashes de originales, topología y estructura del FBX, exportación y reimportación de una copia. La herramienta Blender también rechazó intentar sobrescribir la fuente y mantuvo su hash.

No hay pruebas de proyecto `*Test*.cs` / `*Tests.asmdef` bajo Assets. No se ejecutó una suite ni una prueba manual de controles: compilación y preservación de archivos no equivalen a jugar. Para desarrollo posterior usar `Window > General > Test Runner` y la guía en AGENTS.md. No lanzar otro Editor de pruebas sobre el proyecto abierto.

## Incidencias

- Se corrigió **CS0104** en la nueva herramienta: conflicto entre UnityEditor.PackageInfo y UnityEditor.PackageManager.PackageInfo, resuelto con alias explícito. Unity confirmó compilación correcta después.
- La primera configuración automática de DOTween encontró una NullReferenceException al crear el asmdef sin un DOTweenSettings inicial. Se corrigió la secuencia usando la inicialización del proveedor con `createIfMissing=true` y `fullSetup=false`, antes de configurar módulos. El segundo intento quedó `configured` y la auditoría final confirmó ajustes, biblioteca y ensamblado cargados. La escena no se modificó.
- El Python incluido no tenía PyYAML. Se instaló **PyYAML 6.0.3** solo en la carpeta local ignorada de validación; el validador oficial aprobó las cuatro skills.
- La automatización de Windows falló al capturar, activar y enviar una tecla a Unity. No se supuso que esos pasos funcionaran. Se recurrió al Refresh realizado por el usuario y a la herramienta local una vez compilada.
- El log real de Unity 6.5 se redirige a `Logs/Editor.log`; el log del perfil solo contiene el arranque. Se comprobó el log del proyecto para validar compilación.
- La importación de DOTween provocó una recompilación amplia: el compilador confirmó `Tundra build success` en **537,71 segundos**, sin nuevos errores C#. La fase completa de refresh/recarga se prolongó más de veinte minutos. No se forzó el cierre de Unity ni se canceló la compilación. Tras el Refresh y la restauración de ventana realizados por el usuario, se completaron configuración, recompilación de módulos y verificación final.
- Hay mensajes del servicio/licencias de Unity AI sobre permisos y solicitudes cloud en el log, anteriores a estas instalaciones. No son errores de compilación y no se intentó habilitar servicios de pago ni cambiar cuentas para eliminarlos.

## T-07 y siguiente paso

T-07 es una sola malla de **10.370 triángulos**, sin rig, animaciones ni cañón separado. La escala efectiva es próxima a **1,90 m de alto**; Unity mantiene un giro y escala de importación. El shader es URP/Lit, pero las texturas incrustadas del FBX no están asignadas en el material de Unity. El análisis completo está en `T07_INSPECTION.md`.

Siguiente paso recomendado: **preparar una copia del T-07 en Blender, separar geométricamente el cañón y las piezas móviles, ubicar pivotes y crear un rig verificable**. Después producir el primer ciclo de locomoción y una variante de prefab que reutilice la IA y armas de la plantilla. No empezar generación automática de animaciones sobre el FBX estático original.

## Evaluación de skills para estación de montaje y HUD — 16/11/2026

Petición: preparar skills para animación FPS de brazos/cambio de arma y layout de HUD uGUI. **No se instaló nada nuevo.** Respaldo de este informe: `Tools/IronRequiem/Backups/20261116-skills-eval/`.

| Skill | Fuente | Versión | Estado | Uso en esta tarea |
|---|---|---|---|---|
| iron-requiem-fps | Local `.agents/skills/` | — | Ya presente | Aplica: arma vía PlayerWeaponsManager/WeaponController, verificación de cambio de arma y pausa |
| iron-requiem-modular-weapons | Local `.agents/skills/` | — | Ya presente | Aplica: sockets/pivotes del módulo montado, Blender solo sobre copias |
| iron-requiem-enemy-ai / -performance | Local | — | Ya presentes | No aplican a estas dos partes |
| unity:ui-ugui, unity:ui, unity:unity-cli | Plugin oficial Unity (Anthropic Directory, `github.com/unity-technologies/unity-agent-plugin`) | 0.1.9-beta, habilitado en Claude Code | Ya instalado | ui-ugui: anclajes/pivotes, ediciones dirigidas sin reconstruir. unity-cli: no lanzar otro Editor sobre el proyecto abierto |
| game-dev-combat-animation, game-dev-combat-skill-polish, design-action-combat | Skills de usuario ya cargadas | — | Ya presentes | Orientadas a Three.js/web; solo criterios generales de anticipación/asentamiento. No sustituyen las reglas del proyecto |
| animation-studio | Anthropic Directory, comunidad | 1.1.0 | Rechazada | Vídeo 2D generado por código; no aplica a Unity |

Búsqueda en el catálogo (SearchPlugins/SearchSkills: "unity animation", "first person viewmodel", "weapon swap animation", "game hud layout"): sin otra skill oficial de Anthropic o Unity para animación FPS. El plugin Unity no incluye skill de animación. No se buscaron terceros porque ninguna laguna lo justificaba.

## Fuentes oficiales

- [Plugin de Unity Technologies](https://github.com/Unity-Technologies/unity-agent-plugin).
- [Formato de plugins y catálogos de Codex](https://developers.openai.com/plugins/build/plugins).
- [Meshy para Codex](https://docs.meshy.ai/es/webapp/plugins/codex/introduction) y [procedimiento de instalación Meshy](https://github.com/meshy-dev/meshy-3d-agent/blob/main/INSTALL.md).
- Versiones y dependencias consultadas directamente en el registro oficial `packages.unity.com`.

Para autenticar Meshy en otra ocasión: `meshy auth login --device --format json --no-update-check`, completar el acceso personalmente en el navegador y comprobar `meshy auth status --format json --no-update-check`. No es necesario autenticarse para inspeccionar el FBX local con Blender.

## Skills para estación de montaje y HUD (16/11/2026)

Revisión para la tarea v37 (animación de la estación de montaje y HUD que tapaba las armas). **No se instaló nada**: las skills ya disponibles cubren la tarea. La búsqueda en el catálogo de Claude (SearchSkills: unity animation, fps viewmodel, weapon swap animation, ugui hud layout, game feel) no devolvió resultados, así que no hubo candidatas de Anthropic, Unity ni terceros que evaluar.

| Skill | Fuente | Aporta a esta tarea | Scripts | Estado |
|---|---|---|---|---|
| iron-requiem-fps | local `.agents/skills/` | Una sola autoridad de movimiento y cámara; armas de la plantilla | No | Instalada; manda sobre las genéricas |
| iron-requiem-modular-weapons | local `.agents/skills/` | Montaje de módulos y paso por PlayerWeaponsManager/WeaponController | No | Instalada; manda |
| iron-requiem-enemy-ai / -performance | local | No aplica (IA y rendimiento) | No | Instaladas; no usadas |
| unity:ui, unity:ui-ugui | plugin oficial Unity Technologies | Anclajes y layout del Canvas uGUI existente | No | Disponibles |
| unity:unity-cli | plugin oficial Unity Technologies | Control del Editor; no conecta sin `com.unity.pipeline`, que sigue pendiente | No | Disponible, no utilizable |
| game-dev-combat-animation, design-action-combat | skills de usuario ya instaladas | Referencias de game feel (asentamiento y retroceso) | No | Solo como referencia |
| animate, animation-systems, improve-animations | skills de usuario | Movimiento web (CSS/JS); no aplican a Unity | — | No aplican |

Las skills genéricas no sustituyen AGENTS.md ni las iron-requiem-*. Ninguna autoriza a instalar paquetes ni a cambiar pipeline, entrada o Editor.
