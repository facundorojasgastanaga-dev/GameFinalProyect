# MAPA 1 · F2 Greybox jugable: plan común (dueño: R0)

18/11/2026. El usuario aprobó F1 con todas las recomendaciones de `MAP01_DESIGN.md` §7 (U1–U16):

| Decisión | Valor |
|---|---|
| Jefe | **A «El Custodio»** |
| Sesiones | **4** (S1 R01–R05 · S2 R06–R13 · S3 R14–R17 · S4 R18) |
| Armas | **Híbrido** |
| Cuartos | 17 + jefe |
| Hangar | Reservado, sin tocar |
| Salas actuales | Se derivan por datos |
| Build Settings | Se **añaden** las escenas Map01 al final |
| Componentes nuevos (§5) | Aprobados |
| Persiana P de entrada | Sí |
| `IDoorPresentation` | Aprobado para F3; **en F2 no se usa** |
| Carroñero | No ejecutable en Map01 |
| Big Bot | Área Jump + variante |
| Luz | Todo en tiempo real |
| Puertas | 0,7 / 0,45 s |
| Decoración | Restos propios, sin Tripo |
| Pausa entre rondas | 0 s |

F2 = **bloques, puertas y enemigos provisionales**, jugable de punta a punta. Sin decoración (F3). Luz greybox mínima.

## 1. Reglas para todos los subagentes de F2

1. **Solo R0 usa Unity**: compila, ejecuta la autoría, hornea, entra en Play y guarda. Tú escribes C# y JSON. **No puedes compilar**: comprueba cada API contra el código del proyecto (grep) y contra Unity 6000.5 / URP 17.5 / AI Navigation 2.0.14. Escribe código sencillo y defensivo, con mensajes de error claros. R0 te devolverá los errores de compilación.
2. **Contrato en código:** `Assets/IronRequiem/Editor/Map01/Map01Core.cs`. Contiene rutas, sesiones, capas, convención de marcadores, helpers de escena (`Map01Scenes.OpenRoom/OpenSession`, que se niegan a tocar escenas ajenas con cambios), jerarquía (`EnsureRoomRoot`, `EnsureChamberRoot`, `ClearLayer`), JSON con Newtonsoft (`Map01Json`) y el **modelo de datos de Estructura**. Es de R0: **no lo edites**. Si te falta algo, dilo en tu entrega y R0 lo añade.
3. **Autoría** (Editor, `namespace IronRequiem.Editor.Map01`, carpeta `Assets/IronRequiem/Editor/Map01/`):
   - Menús bajo `Tools/Iron Requiem/Map01/<Rol>/…`: el puente de R0 solo ejecuta menús que empiezan por `Tools/Iron Requiem/`.
   - Cada menú procesa todos los cuartos o esclusas de su rol. Es **idempotente**: abre la escena, `ClearLayer` de **su** capa, reconstruye, guarda y escribe un informe con `Map01Report.Write`.
   - Usa Undo (`Undo.RegisterCreatedObjectUndo`, `Undo.DestroyObjectImmediate`). Nunca toca otra capa.
   - Valida los datos **antes** de construir y lanza una excepción con el ID y el campo que fallan.
4. **Runtime** (`namespace IronRequiem`, ensamblado `IronRequiem.Runtime`):
   - Carpetas: `Assets/IronRequiem/Scripts/Levels/Map01/` y `Assets/IronRequiem/Scripts/Doors/`.
   - Un MonoBehaviour por archivo, con el mismo nombre. Campos privados `[SerializeField]`.
   - Sin GC por frame. Sin `Find` por nombre en `Update`.
   - **Solo observan** propiedades públicas de sistemas existentes.
5. **Prohibido:**
   - modificar cualquier archivo existente fuera de tu lista de archivos exclusivos, en especial: `CampaignFlow`, `CampaignRoom`, `CampaignAirlock`, `CombatTwoRoomsLab`, `EnemySpawnQueue`, `AirlockDoorMotion`, `AirlockCycle`, `CorridorAlarm`, `ChaseCorridorEncounter`, `GapFallRecovery`, `WeaponMountStation`, `CombatLabMenu`, `ZoneEnvironment` y los prefabs o escenas existentes (solo se leen o se copian por API);
   - la zona protegida;
   - ProjectSettings y paquetes (salvo el menú de Build Settings delegado a R4, §4);
   - git que escriba; Meshy, Tripo o compras; red; `seguimiento-proyectos/`.
6. **Escenas** solo en `Assets/IronRequiem/Scenes/Map01_Bunker/`. **Datos** en `Assets/IronRequiem/Levels/Map01/Data/<Rol>/`. **Generados** (NavMesh, prefabs derivados) en `Assets/IronRequiem/Levels/Map01/Generated/` o `Assets/IronRequiem/Prefabs/Map01/`.
7. **Unidades:** metros, yaw 0. Los datos de cuarto van en coordenadas **locales al cuarto**. El root `Map01_R<NN>` está en `worldOrigin`, y sus capas en el origen local. Los datos de esclusa (`S00`, `C<NN>`) van en coordenadas **de mundo**. El root de la esclusa está en `worldCenter`, así que sus hijos usan `posición de mundo − worldCenter`.
8. **Entrega:**
   - archivos creados (con ruta);
   - menús y orden de ejecución;
   - qué valida tu código;
   - qué no pudiste comprobar sin compilar;
   - riesgos;
   - skills usadas.

## 2. Orden de ejecución (lo ejecuta R0)

| Paso | Menú | Dueño | Qué hace |
|---|---|---|---|
| 1 | `Map01/Sessions/1 Create session scenes` | R4 | Copia `RequiemCampaign.unity` → `Map01_Campaign_S1…S4` (si no existen) y las limpia: desactiva `PersistentAirlocks` y `LabSun`, apaga `SectorLightingBlend` y conserva Player, HUD, menús, música, GameManager y `CampaignSession` |
| 2 | `Map01/Structure/Build all` | R1 | Crea o abre las 18 escenas de cuarto; reconstruye `[Structure]` (geometría, colliders, marcadores) y la geometría de las esclusas en sus sesiones |
| 3 | `Map01/Structure/Bake NavMesh all` | R1 | Un NavMesh por cuarto (área Jump en los volúmenes de D-U6) y validación de slots |
| 4 | `Map01/Lighting/Build greybox all` | R5 | Luz neutra por zona en los anclajes, RenderSettings por cuarto, relleno de esclusas y reflection probe |
| 5 | `Map01/Doors/Build all` | R4 | Puertas greybox de esclusa y triggers `CampaignAirlock` en las sesiones; persianas P (`ArenaShutter`) y B en los cuartos |
| 6 | `Map01/Encounters/Build all` | R3 | `CampaignRoom`, rondas, colas, botiquines, persecución, grietas, alarma, `Map01StageRelease`, el jefe provisional por rondas y las variantes de enemigo Map01 |
| 7 | `Map01/Sessions/2 Wire sessions` | R4 | `CampaignFlow` (stageScenes, airlocks, initialDoor, prologueStages = 0), Player en PlayerStart, `WeaponMountStation`, `Map01SessionHandoff` y `directGameplayEntry = true` |
| 8 | `Map01/Sessions/3 Add to Build Settings` | R4 | Añade al **final** las 4 sesiones y los 18 cuartos, sin reordenar ni quitar nada |
| 9 | `Map01/Validate all` + capturas + tests | R0 | Validadores de cada rol, PlayMode y recorrido |

## 3. Convenciones entre capas (para encontrarse sin referencias cruzadas frágiles)

- **Marcadores de Estructura** (`Map01Core.Map01Markers`): `[Structure]/Markers/...`
  - `Slots/<G>/<G>_<n>`
  - `Anchors/<R<NN>.A<n>>`
  - `Openings/<doorId>`
  - `Arena` (BoxCollider desactivado = `firstRoom` en local)
  - `Landmark`
  - `Release` (BoxCollider trigger; solo en respiros)

  En las esclusas: `C<NN>/[Structure]/Markers/Anchors/<C<NN>.CY1|CY2|FILL>`, `Openings/SourceDoor|DestinationDoor`, `Trigger`, `PlayerStart` y `Station`.
- **Luces** (R5): cada `Light` se llama como su anclaje (`R07.A7`) y vive en `[Lighting]`. Encuentros (`CorridorAlarm`) las busca por ese nombre.
- **Puertas** (R4):
  - En las sesiones: `C<NN>/[Doors]/SourceDoor`, `DestinationDoor` y `Airlock` (el trigger `CampaignAirlock`).
  - En S00: `S00/[Doors]/DestinationDoor`, que es el `initialDoor`.
  - En las esclusas de arranque de sesión, la cámara existe en **las dos** sesiones (`Map01Paths.SessionsOfChamber`).
  - En los cuartos: `[Doors]/<internalDoorId>` con `ArenaShutter`.
- **Componentes de runtime que se buscan entre sí:** `ArenaShutter` y `Map01StageRelease` encuentran el `CampaignRoom` de **su propia escena** en `Start` (`gameObject.scene.GetRootGameObjects()` → `GetComponentInChildren<CampaignRoom>(true)`). No hay referencias serializadas entre capas de roles distintos.
- **NavMesh (R0-D8):** NavMeshBuilder con el agente 0 y estos ajustes forzados en el bake:

  | Ajuste | Valor |
  |---|---|
  | `agentRadius` | 0,75 |
  | `agentHeight` | 2,0 |
  | `agentClimb` | 0,45 |
  | `agentSlope` | 40 |
  | `voxelSize` | 0,15 |

  Los volúmenes `nav.jumpAreaVolumes` se marcan con el área 2 (Jump) mediante `NavMeshBuildSource` ModifierBox. La variante Map01 del Big Bot excluye Jump de su `areaMask`. Hay **un NavMeshData por cuarto**, en `Generated/NavMesh/R<NN>_NavMesh.asset`, con `NavMeshSurface` en `[Structure]`. Se medirá en el recorrido de F2.

## 4. Delegación especial

- **R4** escribe el único menú que cambia `EditorBuildSettings` (paso 8). Solo añade, nunca quita ni reordena. R0 tiene respaldo en `Tools/IronRequiem/Backups/20261118-map01-f2/`.
- **R3** crea, por `PrefabUtility` y como **variantes nuevas** en `Assets/IronRequiem/Prefabs/Map01/Enemies/`:
  - `T07_Mech_Map01` (`executionOnly = 0`, U3 híbrido);
  - `BigBot_Map01` (`areaMask` sin Jump, U12);
  - `Carronero_Map01` (`EnemyExecutionSource` desactivado, U11).

  Nunca modifica los originales.
