# IRON REQUIEM

FPS 3D rápido para PC inspirado en DOOM y ULTRAKILL, en un mundo postapocalíptico dominado por robots. Su mecánica principal es destruir puntos de unión, desprender armas enemigas, recogerlas y usarlas temporalmente con energía o munición, activar OVERDRIVE y expulsarlas al agotarse. Es una dirección de diseño; los sistemas modulares todavía no están implementados en esta configuración.

## Base que debe conservarse

- Proyecto existente: Unity **6000.5.5f1**, URP **17.5.0**, Input System **1.19.0**, entrada activa `1` (Input System).
- Escena de juego: `Assets/FPS/Scenes/MainScene.unity`. Build Settings también contiene IntroMenu, WinScene y LoseScene. Conservar jugador, enemigos, cámara y prefabs de plantilla.
- No cambiar Editor, render pipeline ni sistema de entrada. No añadir paquetes preview, beta o experimentales. La CLI instalada es beta; no confundir su versión con permiso para instalar paquetes experimentales.
- Fuente T-07: `Assets/IronRequiem/Art/Enemies/Exterminador/Models/Meshy_AI_Rustborn_Sentinel_20261028_texture.fbx`. Conservar FBX y meta originales. Generic no demuestra un esqueleto funcional.
- No comprar herramientas, generar modelos ni consumir créditos de Meshy por una tarea de configuración.

## Arquitectura existente

La plantilla vive en `Assets/FPS`, con ensamblados `fps.Game`, `fps.Gameplay`, `fps.AI` y `fps.UI`.

- Movimiento: `Scripts/Gameplay/PlayerCharacterController.cs` con CharacterController. Entrada: `Scripts/Gameplay/Managers/PlayerInputHandler.cs`, mediante InputSystem.actions y `Assets/FPS/InputSystem_Actions.inputactions`.
- Armas: `Scripts/Game/Shared/WeaponController.cs`, `Scripts/Gameplay/Managers/PlayerWeaponsManager.cs`, WeaponPickup, ProjectileBase y ProjectileStandard. Salud e impactos: Health, Damageable y DamageArea.
- IA: EnemyController, EnemyMobile, EnemyTurret, DetectionModule, NavigationModule y PatrolPath en `Scripts/AI`. EnemyMobile usa Patrol/Follow/Attack y los parámetros Animator MoveSpeed, Attack, Alerted y OnDamaged.
- Navegación: AI Navigation instalado y componentes antiguos en `Assets/NavMeshComponents`. No eliminar ni migrar uno por aparente duplicación: comprobar namespaces, superficies y referencias primero.
- UI, audio, objetivos y flujo de partida existen en la plantilla; reutilizarlos.

## Código y assets

Usar C# compatible con Unity 6, clase y archivo con el mismo nombre y un MonoBehaviour por archivo. Para código nuevo preferir namespace `IronRequiem`, campos privados `[SerializeField]`, APIs explícitas y componentes por responsabilidad. Mantener convenciones existentes al editar la plantilla; evitar refactors de estilo ajenos a la tarea.

Colocar sistemas propios bajo `Assets/IronRequiem/` por dominio: Scripts, Prefabs, Art, Audio, VFX, Editor y Tests cuando sean necesarios. Crear carpetas con contenido real. Conservar GUID y `.meta`; usar APIs del Editor para escenas y prefabs, variantes y Undo en herramientas interactivas. No editar YAML de escenas a mano si el Editor está disponible. No guardar una escena que ya estaba modificada por el usuario.

Antes de modificar configuración, respaldar archivos afectados en `Tools/IronRequiem/Backups/<fecha>/`. Esta carpeta está ignorada porque puede contener configuración privada. Git tiene raíz en la carpeta de usuario; limitar status/diff/add a este proyecto. No añadir el perfil completo ni hacer commits automáticos de cambios existentes.

## Skills y herramientas

Skills locales en `.agents/skills/`: `iron-requiem-fps`, `iron-requiem-enemy-ai`, `iron-requiem-modular-weapons` y `iron-requiem-performance`. Elegir según tarea y leer su SKILL.md. Usar skills oficiales cuando estén realmente disponibles, comprobando instalación.

Consultar `Docs/IronRequiem/SETUP_REPORT.md` para estados verificados. Usar Unity CLI con `--project-path` a este proyecto. El MCP oficial requiere `com.unity.pipeline`; mientras solo tenga versiones experimentales queda pendiente por la regla de paquetes estables. No instalarlo silenciosamente ni sustituirlo por un servidor arbitrario.

`Assets/IronRequiem/Editor/IronRequiemSetupTools.cs` genera una auditoría desde `Tools > Iron Requiem`. Su cola local en `Tools/IronRequiem/Requests` admite auditoría y las instalaciones concretas autorizadas para esta configuración; no expone un servidor ni modifica escenas.

Blender: `Tools/IronRequiem/Blender/blender_asset_tool.py` inspecciona FBX, mallas, huesos, materiales, dimensiones y topología; prepara exportaciones en copias con selección explícita y pivotes. No guardar encima del modelo fuente. Antes de cambiar rigging leer `Docs/IronRequiem/T07_INSPECTION.md`. Separar islas no garantiza separar correctamente un arma.

## Verificación

Tras cada instalación, esperar resolución UPM, importación y compilación antes de instalar otro paquete. Diagnosticar fallos primero. No actualizar dependencias preexistentes incidentalmente.

1. Revisar Console y `Tools/IronRequiem/Reports/unity-audit.json`: versión, paquetes, URP, escena activa, scripts ausentes y errores capturados.
2. Usar `Window > General > Test Runner` para pruebas relevantes EditMode/PlayMode. Ausencia de tests no equivale a tests superados. No ejecutar `unity test` contra un proyecto ya abierto en otro Editor.
3. Para gameplay autorizado comprobar movimiento, cámara, disparo, cambio de arma, enemigos y UI en Play Mode; salir sin guardar cambios de ejecución. No declarar controles probados si solo se compiló.
4. Comparar hashes de escenas, prefabs, scripts base, modelo y ProjectSettings con el respaldo. Verificar manifest y packages-lock, no solo descargas. Registrar cambios y pendientes.

`IRON_REQUIEM_GDD_v0.1.md` no estaba presente durante la configuración inicial. Si aparece, leerlo sin inventar contenido.
