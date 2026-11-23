# MAPA 1 · F0 Inventario (solo lectura)

18/11/2026. Fase F0 del Prompt 9 (`Docs/IronRequiem/PROMPTS_CHATS.md` §9). Lo hicieron cinco subagentes de inventario (modelo Haiku), en paralelo y solo con lectura; cada uno escribió únicamente su anexo. El coordinador (R0) comprobó contra el código y los prefabs los datos que condicionan el diseño; las correcciones están en §0.

- No se abrió ni se controló Unity. El Editor estaba abierto en otra sesión con `SawBotRobotAnimShowcase`.
- No se guardó ninguna escena, no hubo commits y no se tocaron archivos del proyecto fuera de esta carpeta.
- Rama `respaldo/pre-mapa1-2026-11-18`, punto de restauración `6dbe3be`.

Los anexos A–E (al final) son la fuente detallada, con `ruta:línea`. Esta síntesis solo recoge lo que condiciona F1.

## 0. Verificación del coordinador

| Dato | Anexo | Comprobado en | Resultado |
|---|---|---|---|
| Movimiento del jugador en campaña: suelo 13, sprint ×1,5, aire 10, salto 9, gravedad 20, cámara 0,8 | D | `Assets/FPS/Prefabs/Player.prefab:431-447` | **Correcto** |
| Cápsula 1,8 × 0,35 m, pendiente 45°, escalón 0,3 m | D | `Player.prefab:543-546` | **Correcto** |
| Dash 24 m/s × 0,18 s; slide 0,7 s | D | `Scripts/Player/AdvancedPlayerMovement.cs:10-13` | **Correcto** |
| Agente NavMesh tipo 0: radio 0,5, altura 2, pendiente 45, escalón 1 | D | `ProjectSettings/NavMeshAreas.asset:76-79` | **Correcto** |
| Big Bot con radio de agente 2,25 m | D | `Prefabs/Enemies/BigBot_Combat_v01.prefab:1404` | Valor presente en la cadena; **no se descarta** un override posterior |
| URP: 4 luces adicionales por objeto, HDR, MSAA off, render scale 1 | E | `Assets/Rendering/Default_PipelineAsset.asset:27-30,48` | **Correcto** |
| `EnemySpawnQueue` asignada «solo en Room01 y Room03» | E | grep `spawnQueue:` en las escenas de campaña | **Corrección:** está asignada en **todas** las salas de combate (Room00b, 01, 02, 03 y 04 y sus `_v2`) |
| Uniones del T-07 en `executionOnly` | B | `Scripts/Enemies/DetachableCannon.cs:43` y overrides `executionOnly: 1` en `FastCombat/T07_HitFeedback_v04_Fast` y `Campaign/T07_Mech` | **Correcto:** con `executionOnly`, `Detach()` retorna sin desprender |
| Arma robada del SawBot «no verificada» | B | `Prefabs/Weapons/SawBot_AnimatedModule_v02.prefab:514` → GUID `7035500b…` | **Resuelto:** `Prefabs/Weapons/Weapon_SawBot_Animated_v02.prefab` (motosierra) |
| «Room01 16 enemigos por ronda» | B | Tabla B3 del mismo anexo | **Errata del anexo:** son 16 **por sala** (6 + 4 + 6) |
| Con más de 3 rondas, las de índice ≥ 3 usan `secondRoom*` | C | `Scripts/Levels/CombatTwoRoomsLab.cs:66,76,103,121` | **Correcto.** En `singleArena` las rondas avanzan solas cuando no queda nadie vivo ni pendiente |
| «TRES RONDAS» fijo en el texto de llegada | C | `Scripts/Levels/CampaignFlow.cs:273` | **Correcto.** Se evita con `prologueTitle` |
| `CombatLabMenu` reconoce la campaña por el nombre `RequiemCampaign`; la muerte recarga toda la sesión | C | `Scripts/Levels/CombatLabMenu.cs:13,38-40` | **Correcto** |

## 1. Reglas del sistema de campaña que condicionan el mapa

Fuente: anexo C, verificado en el código citado.

1. **Una etapa = una escena de sala aditiva**, con un solo `CampaignRoom`. `CampaignFlow.stageScenes` guarda nombres sin ruta, que deben estar en Build Settings. Cambiar Build Settings necesita la aprobación del usuario.
2. **Las esclusas viven en la escena de sesión** (`Map01_Campaign.unity`): el trigger `CampaignAirlock` y sus puertas `sourceDoor`, `destinationDoor` y `extraDestinationDoors`. Están todas presentes durante toda la partida. `airlocks[i].sourceStage == i` y `airlocks.Length == stageScenes.Length − 1`.
3. **No hay teletransporte.** Las salas comparten coordenadas de mundo con la sesión: el mapa tiene que ser un plano 3D coherente. La sala N+1 se activa antes de descargar la N, así que **dos salas consecutivas no pueden solaparse**. Las esclusas, que siempre existen, no pueden solapar ninguna sala.
4. **El recorrido es lineal y sin vuelta atrás.** Tras cada transición, la puerta de origen queda cerrada para siempre y la sala anterior se descarga. Un atajo entre etapas es imposible sin código nuevo. Dentro de una etapa sí puede haber bucles y pasos secundarios.
5. **Rondas** (`CombatTwoRoomsLab`, `singleArena = true`):
   - La ronda 1 empieza al entrar en `firstRoom` (Bounds).
   - Cada ronda siguiente entra **en el mismo frame** en que muere el último enemigo de la anterior. No hay temporizador entre rondas.
   - El número de enemigos simultáneos es igual al tamaño de la ronda; no hay tope.
   - Hay variaciones aleatorias por ronda (`variedSpawns`) y aparición segura: NavMesh, ≥ 6 m del jugador, sin solape y con camino completo hasta el jugador.
   - Con 4 o más rondas hay que rellenar `secondRoom` y `secondRoomSpawns`. Si no, la ronda se queda «EN ESPERA» para siempre (soft-lock por lectura de código).
6. **Aparición**: `EnemySpawnQueue` precalienta 1 enemigo por frame y activa 1 por frame (`Configure(1,1)`). Todos los enemigos de todas las rondas de la sala se instancian inactivos al cargarla.
7. **Puertas** (`AirlockDoorMotion`):
   - Movimiento solo vertical: 0,7 s para abrir y 0,45 s para cerrar.
   - Una puerta abierta queda **inactiva y sin colisión**; la que cierra tiene colisión desde el primer frame.
   - `IsClosed` depende de que el root no se mueva fuera de `Set`.
   - Sin NavMeshObstacle, sin comprobación de obstrucción y sin estado BLOQUEADA.
   - `DoorPresentation` **no existe**: es la propuesta de enganche visual de `doors.md` §5.
8. **Lo que falta en el código actual** y obliga a decidir:
   - No hay checkpoints: morir reinicia la campaña desde la etapa 0.
   - No hay evento de fin de campaña ni de WinScene.
   - No hay soporte de jefe: ni fases ni HUD de jefe.
   - Las persianas o puertas internas dentro de una sala tampoco existen; en campaña `gate` está vacío.

   Para todo esto hace falta código nuevo. Si es un componente nuevo que observa propiedades públicas, como hace `CorridorAlarm`, no cambia sistemas existentes. Si toca `CampaignFlow`, `CombatLabMenu` o `AirlockDoorMotion`, necesita aprobación.
9. **Vida**: `Pickup_Health` cura 25 y en campaña se coloca a mano. Las rondas ponen `DropRate = 0` al loot de vida, así que el HoverBot no la suelta en las salas.
10. **Entrada**: IntroMenu (protegida) carga `RequiemCampaign`. Map01 se probará abriendo `Map01_Campaign.unity` en el Editor, con `directGameplayEntry = true`, hasta que el usuario apruebe cambiar la entrada.

## 2. Métricas del jugador (efectivas en campaña)

Fuente: anexo D, verificado en §0.

| Métrica | Valor | Cálculo |
|---|---|---|
| Velocidad en suelo / sprint | 13 / 19,5 m/s | MaxSpeedOnGround × SprintSpeedModifier 1,5 |
| Tope horizontal en el aire | 10 / 15 m/s | MaxSpeedInAir × 1,5 al esprintar |
| Altura del salto | 2,03 m | 9² / (2 · 20) |
| Tiempo en el aire | 0,90 s | 2 · 9 / 20 |
| Salto horizontal | 9,0 m andando · 13,5 m esprintando | tope en el aire × 0,9 s |
| Dash | 4,32 m | 24 m/s × 0,18 s; una carga hasta aterrizar |
| Salto + dash | ≈ 11,5 m andando · ≈ 14,6 m esprintando | anexo D §D4 |
| Slide | ≈ 12 m desde sprint | 0,7 s, deceleración 18 m/s²; invulnerable 0,42 s |
| Cápsula | 1,8 m de pie · 0,9 m agachado · radio 0,35 m | |
| Ojos | 1,44 m de pie · 0,72 m agachado | CameraHeightRatio 0,8 |
| Escalón / pendiente | 0,3 m / 45° | CharacterController |
| Caída sin daño | ≈ 10 m | anexo D (daño desde 20 m/s); **sin medir en Play** |
| Alcance de recogida / ejecución | 2,5 m (`StolenWeaponCarrier`) | DESIGN_V02_UPDATE |

**Agentes NavMesh:** tipo 0 con radio 0,5, altura 2, pendiente 45° y escalón 1 (la escena Room01 hornea con 0,4: discrepancia anotada en el anexo D). Big Bot ≈ 2,25 m de radio y 3,05 m de alto. Carroñero 0,32 / 1,15 m. SawBot 0,8 / 1,6 m.

## 3. Enemigos vigentes y economía de armas

Fuente: anexo B, con las correcciones de §0. Las variantes vigentes son `Assets/IronRequiem/Prefabs/Campaign/*`.

| Tipo | Prefab | Salud | Detección / ataque | Velocidad | Arma para el jugador |
|---|---|---|---|---|---|
| T-07 común | `T07_Current_Repaired` | 128 | 45 / 38 m | 4,2 | Cañón `Weapon_T07_Temporary_Optimized` (energía 100, OVERDRIVE al 20 % con 5 s de margen), **solo por ejecución** (< 75 %) |
| T-07 Mech | `T07_Mech` | 128 (unión 35) | sin verificar | 3 / 4,2 | El mismo cañón, solo por ejecución |
| SawBot | `SawBot_Current` | 140 | 22 / 3 m | 9,5 | Motosierra `Weapon_SawBot_Animated_v02`, por ejecución |
| Big Bot | `BigBot_Current` | 960 | 36 / 28,6 m | 1,68 | **Ninguna en campaña** (sin ejecución y cañón no recogible) |
| HoverBot | `HoverBot_Current` | 80 | 20 / 12,5 m | 4,2 | Ninguna; su loot de vida se anula en las rondas |
| Torreta | `Turret_Current` | 320 | 45,75 / 43,75 m | ¿fija? | Ninguna |
| Carroñero | `Carronero` | 26 | 22 / 3 m | 7,2 | **No concluyente**: hereda `EnemyExecutionSource` activo, aunque la checklist dice que no es ejecutable |

**Hallazgo de diseño principal:** el pilar «destruir la unión → desprender → recoger» está **desactivado en la campaña actual**. Hoy las armas solo se consiguen por ejecución, de T-07 y SawBot. Las fuentes de arma de cada sala son por tanto T-07 (cañón) y SawBot (motosierra). F1 diseña la economía con las reglas actuales y deja la alternativa como decisión del usuario.

Campaña actual: 125 enemigos en total. Las rondas de horda llegan a 14, 22 y 27 enemigos simultáneos, con 8, 14 y 20 Carroñeros.

## 4. Kit y herramientas

Fuente: anexo A.

- **Libre** (fuera de la lista roja): `Art/Environment/Bunker/Bunker_*.mat` (URP/Lit; hormigón, pared, techo, rejilla, peligro, óxido y lámparas ámbar, fría y roja); mallas `Art/VisualOverhaul/Meshes` (tubería 244 tris, cable 600, rejilla 144, luces 24–108; sin colliders); los 226 `BunkerBox_N`; los decales `VO_Decal_*`; `CombatClarity.asset`.
- **Lista roja** (usar o copiar, nunca modificar): el shader `IronRequiem/HardSurface` (se puede usar en materiales nuevos, no editar), `VO_Env_RustMetal` y sus texturas, `VO_Noise`, los Scrap (solo Room00), `HG_*` y `T07Mech_Atlas`.
- **Faltan** props de búnker: literas, taquillas, camillas, terminales, válvulas, barricadas, bidones, cajas, agua y escombros de hormigón. Son el mayor hueco del kit. Deben salir de scripts de Blender reproducibles (R2), sin compras; Tripo, solo con aprobación por pieza.
- **Patrones de autoría:** `VisualOverhaulAssets` (mallas y materiales procedurales), `VisualOverhaulDressing` (colocación por raycast, `CheckLightBudget`) y `LevelOpenPass` (cuarentena en lugar de borrado). **Ninguno usa Undo** y `Dress()` no limpia su raíz previa. Los scripts de autoría de Map01 deben añadir Undo y una limpieza por capa.

## 5. Rendimiento y presupuesto provisional por cuarto

Fuente: anexo E.

- El juego está **limitado por CPU** (hilo principal); la GPU va sobrada (~5 ms de p99 con 12 robots).
- Release a 1080p, solo la primera ronda (3–6 enemigos): media de 5,2–5,8 ms en todas las salas. p99 de 7,0–8,4 ms en las de combate y de 9,1–11,6 ms en Pasillo y Hangar.
- Con 12 robots por oleada (búnker, Release): media de 10,1–12,6 ms. **No cumple 8,33 ms.**
- No hay medidas de las rondas de horda (22–27) ni draw calls de Release posteriores a v35.

**Presupuesto F0 [PROPUESTA del coordinador]:** se usa para diseñar en F1 y se mide en F2 con la ronda más cargada de cada cuarto. El criterio es IR-04: media ≤ 8,33 ms, p99 ≤ 11 ms y 0 frames > 33 ms, a 1920×1080 exclusiva.

| Concepto | Límite |
|---|---|
| Enemigos vivos a la vez (= tamaño de ronda) | **≤ 8 «unidades»**: Carroñero = 0,5, Big Bot = 2, resto = 1. **Techo de 12 cuerpos** |
| Activación | 1 enemigo por frame (`EnemySpawnQueue`) |
| Draw calls por vista | ≤ 350 sin combate · ≤ 400 en combate |
| SetPass por vista | ≤ 200 (techo de 260 hasta medir en Release) |
| Triángulos visibles en combate | ≤ 400 k |
| Luces en tiempo real visibles por cuarto | ≤ 8 estáticas · ≤ 2 con sombra · ≤ 2 transitorias de efectos |
| Luces por objeto | ≤ 4 (límite de URP): teselas de geometría de ≤ 10 m |
| Reflection probes | ≤ 2 horneadas por cuarto; light probes solo para dinámicos |
| Decales | ≤ 24 por cuarto |
| Partículas vivas en combate | ≤ 500 y ≤ 60 sistemas |
| Transición entre cuartos | pico ≤ 33 ms y 0 frames > 50 ms |

## 6. Salas actuales (para decidir su reutilización)

Fuente: anexo E §E5.

| Sala | Planta / techo | Estado |
|---|---|---|
| Room00_Hangar | 149×84×114 m | **Prólogo; se mantiene.** Contiene la zona protegida |
| Room00b_AlarmCorridor | 18×34 m / 8,95 m | Pasillo lineal con alarma. M14 y R9 pendientes |
| Room01_Foundry | 54×65 m / 12,45 m | Falsa salida (M11). M8 corregido en `_v2`, sin probar en Play |
| Room02_Breach | 54×65 m / 20,95 m | Grieta. M7 corregido en `_v2`, sin probar; horda de 14 |
| Room03_Assembly | 54×52 m / 20,95 m | Mismo casco que Room04; horda de 22 |
| Room04_Reactor | 54×52 m / 20,95 m | M9 corregido en `_v2`, sin probar; horda de 27 |
| Pursuit05 | 16×218 m / 16 m | Persecución con sierra; termina en muro ciego (M13) |

## 7. Pendientes de F0 (fuera de alcance, no se tocan)

- Verificar en el Editor si el Carroñero es ejecutable, la salud de la unión del T-07 de campaña y si la torreta se mueve.
- Discrepancia del escalón de NavMesh: 1,0 m en ProjectSettings frente a 0,4 m en Room01.
- Medir las rondas de horda en Release (R6/R7 de `render-perf.md`).

---

# Anexos (informes F0 completos)

Se incluyen tal como los entregaron los subagentes. Las correcciones de §0 prevalecen.


---

<!-- Anexo A_kit_props.md -->

## Anexo · F0-A: Inventario del kit de entorno, props y herramientas de autoría (Mapa 1: Búnker abandonado)

Fecha: 2026-11-18. Solo lectura. No se abrió Unity, no se ejecutó la CLI, no se escribió nada en el proyecto. Rutas relativas a `C:\Users\gehbd\ProyectoGameFinal\`. Si algo no se pudo comprobar aparece como **NO VERIFICADO**.

Convención de la lista roja: "sí" = aparece en `Tools/IronRequiem/Reports/ProtectedZone/before-20261117.txt` (dependencias de IntroMenu / Room00 o de las salas) y debe copiarse, no editarse. "sí (solo Room00)" = aparece en el snapshot protegido, pero solo como dependencia de Room00_Hangar (no está en la lista 1b de salas). Fuente de la lista: `Tools/IronRequiem/Reports/ProtectedZone/guardian.md` §1b y §2.

Shaders, por GUID (todos los `.mat` del kit):
- `933532a4fcc9baf4fa0491de14d08ed7` = URP Lit. **Inferido, NO VERIFICADO en el .meta local**: la búsqueda en `Packages/` se agotó. Fuente de la inferencia: `Assets/IronRequiem/Editor/BunkerPolishAuthoring.cs:96,106` usa `Shader.Find("Universal Render Pipeline/Lit")` y los `.mat` de Bunker referencian ese GUID.
- `20aba464369adfd4e800688c83c02a4a` = `Assets/IronRequiem/Art/Shaders/HardSurface/IronRequiemHardSurface.shader` (verificado, `.meta` local).
- `d0353a89b1f911e48b9e16bdc9f2e058` = `m_Script` de los `.mat` (script de Unity). NO VERIFICADO.

---

### A1 Materiales del kit Bunker/Scrap

Todas las rutas empiezan por `Assets/IronRequiem/Art/Environment/`. Tamaños de textura: PNG 512x512 (cabecera IHDR leída); JPG 4096x4096 (marcador SOF leído). Escala = `m_Scale` de `_BaseMap` (1 unidad = 1 tile; el tile en metros sale del comentario de `BunkerPolishAuthoring.cs` `Mat(..., tilingMetres, ...)`).

| Ruta (material) | Shader | Texturas (GUID → archivo) | Uso sugerido | Lista roja |
|---|---|---|---|---|
| `Bunker/Bunker_Floor.mat` | URP Lit (`933532a4`) | Albedo `486065da` → `Bunker/Textures/Bunker_ConcreteFloor_Albedo.png` 512²; Normal `2cfa7a80` → `Bunker_ConcreteFloor_Normal.png` 512². Escala 0.25 (4 m/tile). Smooth 0.25, metal 0 | suelo (hormigón) | no |
| `Bunker/Bunker_Wall.mat` | URP Lit | Albedo `64f3d848` → `Bunker_ConcreteWall_Albedo.png` 512²; Normal `d9c80360` → `Bunker_ConcreteWall_Normal.png` 512². Escala 0.333 (3 m). Smooth 0.15 | pared (hormigón) | no |
| `Bunker/Bunker_Ceiling.mat` | URP Lit | Mismas texturas que Wall (`64f3d848`, `d9c80360`). Escala 0.25 (4 m). Smooth 0.1 | techo | no |
| `Bunker/Bunker_RustMetal.mat` | URP Lit | Albedo `4b431a38` → `Bunker_RustMetal_Albedo.png` 512²; Normal `d4d2cfde` → `Bunker_RustMetal_Normal.png` 512². Escala 0.5 (2 m). Metal 0.55, smooth 0.35 | óxido, metal, paneles | **sí (solo Room00)** (el `.mat` no; sus dos texturas sí, vía `VO_Env_RustMetal.mat`, ver A6) |
| `Bunker/Bunker_Grating.mat` | URP Lit | Albedo `11e3ff70` → `Bunker_Grating_Albedo.png` 512²; Normal `1a5d2da9` → `Bunker_Grating_Normal.png` 512². Escala 0.5 (2 m). Metal 0.6, smooth 0.4 | rejilla (suelo o pasarela) | no |
| `Bunker/Bunker_Hazard.mat` | URP Lit | Albedo `f1305f35` → `Bunker_Hazard_Albedo.png` 512²; Normal `d61c6fe5` → `Bunker_Hazard_Normal.png` 512². Escala 0.667 (1.5 m). Metal 0.1, smooth 0.3 | peligro (franjas) | no |
| `Bunker/Bunker_LampCold.mat` | URP Lit | Sin textura. Emisión (4.68, 5.4, 6) HDR; base (0.468, 0.54, 0.6) | lámpara fría de techo | no |
| `Bunker/Bunker_LampRed.mat` | URP Lit | Sin textura. Emisión (8, 0.96, 0.4) HDR | emergencia roja | no |
| `Bunker/Bunker_LampAmber.mat` | URP Lit | Sin textura. Emisión (7, 3.85, 0.7) HDR | emergencia ámbar / aviso | no |
| `Bunker/Bunker_SuitSleeve.mat` | URP Lit | Sin textura. Color (0.16, 0.17, 0.18), metal 0.2 | manga del protagonista (**no es entorno**) | no |
| `Scrap/Scrap_MachineWreck/Scrap_MachineWreck_HS.mat` | IronRequiem/HardSurface (`20aba464`) | BaseMap `1e6c9216` → `Scrap_MachineWreck_BaseColor.jpg` 4096². Keywords `_DIRT`, `_EDGEWEAR` | chatarra grande (wreck) | sí (solo Room00) |
| `Scrap/Scrap_MilitaryMech/Scrap_MilitaryMech_HS.mat` | HardSurface | BaseMap `75ca8dae` → `Scrap_MilitaryMech_BaseColor.jpg` 4096². `_DIRT`, `_EDGEWEAR` | chatarra de mech | sí (solo Room00) |
| `Scrap/Scrap_RustedWreckage/Scrap_RustedWreckage_HS.mat` | HardSurface | BaseMap `9dc0f853` → `Scrap_RustedWreckage_BaseColor.jpg` 4096². `_DIRT`, `_EDGEWEAR` | chatarra oxidada | sí (solo Room00) |

Notas de A1:
- Los `.mat` de Bunker no tienen keywords de HardSurface: son URP Lit puros. Los de Scrap sí usan HardSurface (valores: `_Metallic` 0.45, `_Smoothness` 0.32, `_DirtAmount` 0.5, `_WearAmount` 0.45, `_Damage` 0, `_UseDirt` 1, `_UseEdgeWear` 1; fuente: `Scrap/*/…_HS.mat` líneas 54-82).
- Los `.mat` de Scrap no importan materiales desde el FBX (`materialImportMode: 0`, `materialLocation: 1`, `Scrap_*.fbx.meta`), así que el enlace es el `.mat` externo.
- Quién usa estos materiales (búsqueda por GUID en `.unity`/`.prefab`, verificado): los `Bunker_*.mat` aparecen en `BunkerLab`, `BunkerBenchmark`, `VisualBench_A`, y en los labs `ChaseCorridorLab`, `CombatTwoFloorsLab`, `CombatVarietyLab`, `SawBot*Lab`, `WeaponHeatBalanceLab`. **No aparecen en `Scenes/Campaign/*` (Room00…Room04, Pursuit05)**. `Bunker_Hazard`, `Bunker_LampRed` y `Bunker_RustMetal` también se usan en `GiantShredder.prefab`. Los `Scrap_*_HS.mat` solo los usa su prefab `Prefabs/Scrap/*.prefab`.
- `Bunker_LampAmber` tiene `_Surface` 0 (opaco) aunque su color base tenga alpha 0.6: el alpha no tiene efecto en opaco (lectura del `.mat`, no probado en Editor).
- Autor de los Bunker: `BunkerPolishAuthoring.Build()` (`Editor/BunkerPolishAuthoring.cs:119-155`). `Mat()` solo crea el material si no existe (`:93-96`), pero `Save()` sobrescribe las texturas PNG (`:85-88`).

### Materiales del kit en Art/VisualOverhaul/Materials (shader HardSurface salvo que se indique)

Ruta base: `Assets/IronRequiem/Art/VisualOverhaul/Materials/`. Los `.mat` se crean en `VisualOverhaulAssets.Env()` / `Panel()` / `ConvertLit()` (`Editor/VisualOverhaul/VisualOverhaulAssets.cs:113-200`). Las texturas de cada uno se leen de `m_TexEnvs` (GUIDs). Los nombres de las texturas de ruido/suelo se resuelven por GUID cuando hay `.meta` local.

| Ruta (material) | Shader | Texturas (GUID → nombre) | Uso sugerido | Lista roja |
|---|---|---|---|---|
| `VO_Env_Floor.mat` | HardSurface | Suelo: `486065da`(Bunker_ConcreteFloor_Albedo) + `2cfa7a80`(…_Normal) + `3305035890b4b3a4088e21c1d0c03c6a` (VO_Noise.png) | suelo (triplanar en `_FloorTri`) | no (VO_Noise sí, ver A6) |
| `VO_Env_FloorTri.mat` | HardSurface | Mismas que `VO_Env_Floor` | suelo en mallas sin UV fiables | no |
| `VO_Env_Wall.mat` | HardSurface | `64f3d848` + `d9c80360` (ConcreteWall) + VO_Noise | pared | no |
| `VO_Env_WallTri.mat` | HardSurface | Mismas que `VO_Env_Wall` | pared en mallas sin UV fiables | no |
| `VO_Env_Ceiling.mat` | HardSurface | `64f3d848` + `d9c80360` + VO_Noise | techo | no |
| `VO_Env_RustMetal.mat` | HardSurface | `4b431a38` + `d4d2cfde` (RustMetal) + VO_Noise | óxido | **sí** (`b296f58c…`, zona protegida en IntroMenu y Room00) |
| `VO_Env_MetalTri.mat` | HardSurface | RustMetal + VO_Noise | óxido triplanar (rampas) | no |
| `VO_Env_Grating.mat` | HardSurface | `11e3ff70` + `1a5d2da9` (Grating) + VO_Noise | rejilla | no |
| `VO_Env_Hazard.mat` | HardSurface | `f1305f35` + `d61c6fe5` (Hazard) + VO_Noise | peligro | no |
| `VO_Light_Cold.mat` / `_Amber` / `_Red` / `_Dead` | HardSurface, panel emisivo | Textura `0db3afa6` (NO VERIFICADO, probablemente máscara de panel) | lámparas; `_Dead` = fallo del `LightFlicker` | no |
| `VO_Prop_Pipe.mat` | HardSurface | RustMetal (`4b431a38`, `d4d2cfde`) + VO_Noise | tubería | no (VO_Noise sí) |
| `VO_Prop_Housing.mat` | HardSurface | RustMetal + VO_Noise | carcasa de luz / rejilla | no |
| `VO_Prop_Cable.mat` | HardSurface | Solo VO_Noise | cable | no |
| `VO_Suit_*` (Accent, Joint, Light, Plate, Ring) | HardSurface | `VO_Suit_Albedo/Mask/Normal` (`39c45343`, `455dee19`, `cc38d1a6` probable, NO VERIFICADO el mapeo exacto) | **traje del protagonista, no entorno** | no |
| `VO_Decal_Burn` / `_Hazard` / `_Rust` / `_Stain` | **URP Lit (`933532a4`)**, transparente | `2fcd327d`, `c2bb5074`, `76c6dd99`, `aaae55cf` (NO VERIFICADO nombre exacto; existen `Art/VisualOverhaul/Textures/VO_Decal_*.png`) | manchas, óxido en goteo, señal de peligro, quemadura (decales de malla) | no |
| `VO_Trail_Amber` / `_Magenta` / `_Red` | shader `0406db5a` (NO VERIFICADO, probablemente partículas) | — | estelas, no entorno | no |

Notas de A1 (VO):
- `VisualOverhaulAssets.Env(m, tex, metres, smooth, metal, triplanar, wear, dirt)` (`VisualOverhaulAssets.cs:128`) es la fábrica de los `VO_Env_*`. Los valores concretos de triplanar/desgaste/suciedad por material **NO VERIFICADOS** (no leí `m_Floats` de cada `.mat`).
- `VO_Env_*` solo usa la textura de ruido `VO_Noise.png` como máscara (GUID `3305035890b4b3a4088e21c1d0c03c6a`).

---

### A2 Mallas y piezas modulares

### A2.1 Bunker/Meshes (cubos generados)
| Ruta | Qué es | Tris | Colisión | Uso sugerido |
|---|---|---|---|---|
| `Art/Environment/Bunker/Meshes/BunkerBox_0.asset` … `BunkerBox_225.asset` (226 archivos) | Cubo unitario-escalado (24 vértices, 36 índices = 12 tris) generado por `BunkerPolishAuthoring.Box()` con caché por tamaño (`BunkerPolishAuthoring.cs:292`). Nombre `BunkerBox_<n>` | 12 cada uno; **2.712 en total** (suma de `indexCount`/3) | NO VERIFICADO por malla: la malla no tiene collider. El collider depende del GameObject que la use | bloque de muro, columna, cajas, suelos de planta. Son el "ladrillo" del kit Bunker |
| `Art/Environment/Bunker/BunkerNavMesh.asset` | NavMesh del búnker de BunkerLab | — | — | solo referencia; no copiar a un mapa nuevo sin rehornear |

### A2.2 Art/VisualOverhaul/Meshes (piezas modulares; generadas en código por `VisualOverhaulAssets.BuildPropMeshes()`, `VisualOverhaulAssets.cs:322-363`)
| Ruta | Qué es | Tris (verificado, suma de índices) | Colisión | Uso sugerido |
|---|---|---|---|---|
| `VO_PipeRun2m.asset` | Tramo de tubería 2 m (tubo sub0, soportes y bridas sub1) | 244 | no (solo MeshFilter/MeshRenderer en `Piece()`, `VisualOverhaulDressing.cs:70-76`) | tuberías de techo o pared |
| `VO_CableBundle4m.asset` | Haz de 3 cables colgantes, 4 m, con anclajes | 600 | no | cables colgantes entre vigas |
| `VO_WallVent.asset` | Rejilla de ventilación 1.2 x 0.8 m: marco + 7 lamas (sub0) + placa oscura (sub1) | 144 | no | rejilla de ventilación en pared |
| `VO_CeilingLight.asset` | Panel de techo 2.5 x 0.55 m: carcasa (sub0) + difusor emisivo (sub1) | 24 | no | luz de techo |
| `VO_CeilingLightHousing.asset` | Carcasa sola | 12 | no | luz de techo, versión sin emisivo (para variantes) |
| `VO_CeilingLightLens.asset` | Difusor solo | 12 | no | sustituto emisivo del anterior |
| `VO_EmergencyLight.asset` | Luz de emergencia de pared: placa + lente enjaulada (sub1) | 108 | no | emergencia enjaulada |
| `VO_JointPlate_L/_R.asset` | Placas de articulación, 12 tris | 12 cada una | no | **probable traje/brazo, NO VERIFICADO**: no usar en entorno sin comprobar |
| `VO_StatusSlit.asset`, `VO_StatusSlitTall.asset` | Ranuras de estado, 12 tris | 12 cada una | no | indicador en paneles (NO VERIFICADO el uso) |

Excluidas por ser del protagonista: `VO_ArmCannon_Body` (484), `VO_ArmCannon_Rings` (640), `VO_ArmShotgun_Body` (1180).

Notas de A2.2:
- No hay prefabs de estas mallas en `Prefabs/VisualOverhaul` (búsqueda de `BoxCollider|MeshCollider`: 0 prefabs con colliders). Se instancian por código en `Dress()`: `Options` por defecto: `Pipes=18`, `Cables=10`, `Vents=8`, `Emergency=8`, `Flicker=3`, `SteamVents=3`, `Decals=60` (`VisualOverhaulDressing.cs:155-162`).
- El cuadro de piezas usa `StaticEditorFlags.BatchingStatic | OccludeeStatic | ReflectionProbeStatic` (`VisualOverhaulDressing.cs:75`). Esto encaja con la skill de rendimiento (batching estático).

### A2.3 Scrap (FBX)
| Ruta | Qué es | Tris | Colisión | Uso sugerido |
|---|---|---|---|---|
| `Art/Environment/Scrap/Scrap_MachineWreck/Scrap_MachineWreck.fbx` | Chatarra grande | NO VERIFICADO (FBX binario, no leído) | `addColliders: 0`; el prefab no declara collider (NO VERIFICADO con certeza) | obstáculo grande, zonas derrumbadas |
| `Art/Environment/Scrap/Scrap_MilitaryMech/Scrap_MilitaryMech.fbx` | Restos de mech militar | NO VERIFICADO | `addColliders: 0` | derrumbe, zona de combate |
| `Art/Environment/Scrap/Scrap_RustedWreckage/Scrap_RustedWreckage.fbx` | Restos oxidados | NO VERIFICADO | `addColliders: 0` | relleno de sala |

Importación (verificado en los `.fbx.meta`): `globalScale 1`, `useFileScale 1`, `meshCompression 1`, `isReadable 0`, `generateSecondaryUV 0`, `importAnimation 0`, `importBlendShapes 0`. Son estáticos.

### A2.4 Mallas de campaña (cubos con dimensiones en el nombre)
- `Art/Levels/Campaign/V35/V35Cube_<ancho>x<alto>x<largo>.asset`: **73** mallas de cubo generadas por `LevelOpenPass`/`CampaignAuthoring` (`Box()` con caché). Sirven de "ladrillos" dimensionados para las salas v35 (Room01–04). Tris: 12 cada uno (no verificado por archivo, mismo patrón que BunkerBox).
- `Art/Levels/TwoRooms/Cover.mat` (color (0.2, 0.36, 0.43)): material de cobertura de la plantilla de salas. Lista roja NO VERIFICADO.

---

### A3 Props y pickups

Fuentes: búsqueda por nombre en `Assets/` (excluyendo `Enemies/` y `Weapons/`), verificación de snapshot con `before-20261117.txt` (0 coincidencias para los nombres `Pickup_Health`, `Loot_Health`, `Wall_1m`, `Basic_Floor`, `Sign_Basic`, `Dun_`, `Wall_Door`).

| Ruta | Tipo | Qué es | Uso sugerido | Lista roja |
|---|---|---|---|---|
| `Art/VisualOverhaul/Meshes/*` | malla | Pipes, cables, rejillas, luces (A2.2) | decoración modular | no |
| `Art/VisualOverhaul/Materials/VO_Decal_*.mat` + `Textures/VO_Decal_*.png` | material/textura | Manchas, óxido, quemaduras, peligro | decales de malla | no |
| `VisualOverhaulDressing.MeshDecal()` (`VisualOverhaulDressing.cs:107-128`) | herramienta | Quads de decal pegados a superficie por raycast, con comprobación de esquinas | manchas en paredes y suelo sin URP Decal | — |
| `Prefabs/Scrap/Scrap_MachineWreck.prefab`, `Scrap_MilitaryMech.prefab`, `Scrap_RustedWreckage.prefab` | prefab | Un GameObject cada uno, con su FBX y su `_HS.mat` (guids verificados) | chatarra | sí (solo Room00) |
| `Art/Environment/Bunker/Textures/Bunker_Hazard_*` + `Bunker_Hazard.mat` | material | Franjas de peligro | bordes de pasarelas, bases de columna (`BunkerPolish` añade `ColumnHazard`) | no |
| `FPS/Prefabs/Level/Signs/Sign_Basic_Floor.prefab`, `Sign_Diamond.prefab`, `Sign_Triangle.prefab` | prefab | Señalética de plantilla (triángulo, rombo, piso) | señalización | no (no figuran en snapshot) |
| `FPS/Art/Materials/Level/Sign_Blue_Transparent.mat`, `Sign_Green.mat`, `Sign_Green_Transparent.mat`, `Sign_Orange.mat`, `Sign_Orange_Transparent.mat` | material | Colores de señal, versión transparente y opaca | señalética de color | no |
| `FPS/Prefabs/Pickups/Pickup_Health.prefab`, `Loot_Health.prefab` + `FPS/Scripts/Gameplay/HealthPickup.cs` | prefab + script | Botiquín de plantilla (modelo `FPS/Art/Models/Mesh_Health_Pickup.fbx`) | botiquín | no (0 coincidencias en snapshot) |
| `FPS/Prefabs/VFX/VFX_HealthPickup.prefab`, `VFX_PickupSparkles.prefab` | VFX | Efecto del botiquín | pickup | no |
| `FPS/Art/Materials/VFX/M_HealthPickup.mat`, `HealthPickups.mat` | material | Material del botiquín | pickup | no |
| `FPS/Prefabs/Level/Walls/Wall_1m.prefab`, `Wall_4m.prefab`, `Wall_Corner.prefab`, `Wall_Door.prefab`; `FPS/Prefabs/Level/Floors/Floor_*.prefab` | prefab | Piezas modulares de plantilla (muro, suelo, puerta) | muros/suelos base | no |
| `FPS/Prefabs/Level/Dungeon/Dun_BigDoor.prefab` | prefab | Puerta grande de plantilla | puertas | no |
| `FPS/Art/Materials/Level/Wall_Grey.mat`, `Wall_DarkGrey.mat`, `Floor_Grey.mat`, `Floor_DarkGrey.mat` | material | Materiales grises de plantilla, sin estilo búnker | NO recomendado | no |
| `FPS/Prefabs/PBObjects/` y `RootObjects/*.obj` | modelos OBJ | Mallas de plantilla de ProBuilder | NO recomendado | no |
| `Art/Levels/Campaign/V35/IR_V35_FloorGuide.mat`, `IR_V35_FoundryGlow.mat`, `IR_V35_NightWindow.mat`, `IR_V35_ReactorCyan.mat` | material | Galones de guía emisivos, luz de fundición, ventana nocturna, cian de reactor | guía de ruta y ambiente por sector | no |
| Decoración v35 (`Room01`…`Room04`, dentro de las escenas) | objetos de escena | Crisoles, vigas caídas, brazos robot, columna de energía, rieles, ventiladores, cables (`v35` en CHECKLIST) | ambientación por sala | las salas no están en la lista; las referencias de escena sí (no verificado en detalle) |

No encontré en `Assets/` (fuera de Enemies y Weapons): cajas, barriles, cajas de munición, barricadas, coberturas como prefab, camillas ni terminales. Los `V35Cube` y `BunkerBox` cubren "bloques"; no hay prefabs de objetos.

---

### A4 Shader HardSurface y CombatClarity

### A4.1 `Art/Shaders/HardSurface/IronRequiemHardSurface.shader` (466 líneas, nombre `IronRequiem/HardSurface`)
Keywords y su uso (líneas 4-11 del shader):
- `_TRIPLANAR`: base y normal en espacio mundo. Útil para mallas sin UV compatibles con la escala de la textura. Parámetros: `_TriplanarScale` (metros por tile, rango libre, por defecto 3), `_TriplanarSharpness` (1-16, por defecto 6).
- `_DIRT`: suciedad de espacio mundo, con ruido triplanar y más fuerte cerca del suelo y en cavidades (AO). Parámetros: `_DirtColor` (0.12, 0.10, 0.08), `_DirtAmount` (0-1, por defecto 0.45), `_DirtScale` (metros por tile, 2.5), `_DirtFloorHeight` (1.2 m por encima de `_DirtFloorY`), `_DirtFloorY` (altura del suelo).
- `_EDGEWEAR`: metal desgastado en aristas. Por curvatura en espacio pantalla (derivadas de normal) × ruido, o por máscara (canal G de `_MaskMap`). Parámetros: `_WearColor` (0.62, 0.60, 0.56), `_WearAmount` (0-1, por defecto 0.6), `_WearCurvature` (0-20, por defecto 6), `_WearFromMask` (0 curvatura, 1 máscara).
- `_RIM`: borde fresnel emisivo, para leer siluetas en la oscuridad. Parámetro: `_RimPower` (0.5-8, por defecto 3.5).
- `_PANELS`: paneles emisivos (máscara R de `_MaskMap` o `_EmissionMap` opcional) con pulso temporal. Parámetros: `_EmissionColor` (HDR, por defecto (0, 0.8, 1.2)), `_PulseSpeed` (Hz, 0.6), `_PulseAmount` (0.35), `_PanelBoost` (0-8, en runtime, p. ej. al disparar).
- Daño (`_Damage` 0-1): no es keyword, es una rama uniforme. Oscurece el albedo (×0.45 al máximo), quema por ruido y pulsa en rojo. Parámetros: `_DamageColor` (2.4, 0.12, 0.04 HDR), `_DamagePulseSpeed` (2.2 Hz).

Otros parámetros: `_Metallic`, `_Smoothness`, `_BumpScale` (0-2), `_UseNormalMap` (keyword `_NORMALMAP`).

Pases: Forward (URP Lit), ShadowCaster, DepthOnly, DepthNormals, Meta. Instancing en todos (`#pragma multi_compile_instancing`). Variantes compiladas solo para las keywords usadas (`shader_feature_local`).

Ajustes ya existentes para zonas del mapa (verificado en materiales Scrap; no en los VO_Env):
- Scrap: `_UseDirt 1`, `_UseEdgeWear 1`, `_UseTriplanar 0`, `_UsePanels 0`, `_UseRim 0`, `_DirtAmount 0.5`, `_WearAmount 0.45`, `_Damage 0`.
- VO_Env_*: valores **NO VERIFICADOS** en `m_Floats`; el autor (`Env()`) acepta triplanar, wear y dirt como flags.

### A4.2 `Art/Rendering/CombatClarity.asset` (GUID `c42f1c04cb15a184d83431794d4ecfdd`)
Es un **VolumeProfile** con overrides de postproceso del combate. Componentes (verificado en el YAML):
- `Bloom` (threshold 0.9, intensity 0.1, scatter 0.7, skipIterations 1, maxIterations 6, clamp 65472, tint blanco, `highQualityFiltering` 0; `dirtIntensity` 0).
- `Vignette` (color (0.18, 0.047, 0.32), intensidad 0.084, suavidad 0.2, centro 0.5/0.5, no redondeada).
- `CombatClarity` (componente propio, `m_Script` `d7fd9488…`, NO VERIFICADO: contenido no leído).
- `Tonemapping`, `FilmGrain`, `MotionBlur` (overrides activos; valores no leídos).

Lista roja: **libre** (`guardian.md`: "Art/Rendering/CombatClarity.asset: no lo usa IntroMenu ni Room00 ni ninguna sala; solo MainScene y labs").

Uso para variar zonas: el perfil es global de cámara. Para variar por zona conviene un volumen local o un perfil propio, no modificar éste (lo usa la escena de combate de referencia).

---

### A5 Scripts de autoría existentes (Editor)

Ruta base: `Assets/IronRequiem/Editor/`. Undo: busca `Undo.RegisterCreatedObjectUndo|AddComponent|RecordObject|SetTransformParent` en `*.cs`. Los generadores de entorno **no** lo usan.

| Ruta | Qué genera | Idempotente | Undo | Patrón reutilizable |
|---|---|---|---|---|
| `VisualOverhaul/VisualOverhaulAssets.cs` | Texturas PNG (`BuildTextures`, `Save` línea 35), materiales `VO_*` (`BuildMaterials`, `Mat` línea 113, `Env` línea 128, `Panel` línea 137), mallas de props `BuildPropMeshes` (líneas 322-363). Ruido `Fbm`/`Hash` determinista | Parcial: `Mat()` solo crea si no existe (línea 118); las mallas se sobrescriben con `CreateAsset`/`SetDirty` (línea 313); texturas se sobrescriben | no | `MeshKit` (Box, Lathe, Tube) + `Save()` = mallas procedurales guardadas como `.asset` |
| `VisualOverhaul/VisualOverhaulDressing.cs` | `Dress(Options)` (línea 166): crea `VO_Dressing` con hijos `VO_Lights`, `VO_Props`, `VO_Decals`, `VO_Ambience`, `VO_LightProbes`, reflection probes; `Piece()` (línea 70) con static flags; `MeshDecal()` (107); `Survey()` (53) raycast de techo y muros; `SplitRenderer()` (337) parte mallas en tiles | **No**: crea siempre `new GameObject("VO_Dressing")` (línea 174) sin borrar el anterior; la idempotencia depende del llamador | no | Colocación con semilla fija (`Seed`), raycast de superficies, `NavMeshModifier.ignoreFromBuild` (línea 172), `CheckLightBudget()` (323) para el límite de 4 luces por objeto |
| `VisualOverhaul/VisualOverhaulScenes.cs` | `BuildLab()` (línea 140): copia MainScene a la variante, sustituye jugador/enemigos/materiales, llama `Dress()` (línea 163). `RebuildLab()` (201) borra solo la variante generada y reconstruye. `BuildBench()` (238) hace A/B. `Bake()`/`FinishBake()` para luces | Sí por negativa: `BuildLab` lanza excepción si el destino existe (línea 143). `RebuildLab` borra y reconstruye | no (usa `EditorSceneManager.SaveScene` directo) | `RemapPrefabs` (34) + `PrefabUtility.ReplacePrefabAssetOfPrefabInstance` (line 64) para cambiar enemigos a variantes `_VO` |
| `BunkerPolishAuthoring.cs` | `Build()` (119): texturas `Bunker_*` (`Save` línea 85, sobrescribe), materiales (`Mat` 93, idempotente), geometría de `BunkerLab` (`Box` cache → `BunkerBox_N`, línea 292), `BunkerLights` + `LightFixture`, NavMesh `BunkerNavMesh.asset` (222). `ConfigureShotgun()` (28). `Rebuild()` (262) | Parcial: `Mat()` idempotente; `Rebuild()` **destructivo** (`AssetDatabase.DeleteAsset(Lab)`) | no | Mallas por caché de tamaño + `Quad()` (264) + `Emissive()` (103) |
| `LevelOpenPass.cs` | "v35 Open + dress rooms (writes scenes)" (`Run`, línea 66) y "v35 Rebuild floor guide" (`RebuildGuide`, 94). Respaldo en `Tools/IronRequiem/Backups/20261113-v35-levels-perf/scenes` (`BackupScene` línea 115, copia solo si no existe). `Root()` (171) cuarentena la raíz previa como `Quarantine_v35` (no borra); `Box()`/`Cyl()` (154/164) | Declarado idempotente en CHECKLIST v35; **en el código `Root()` cuarentena una raíz existente en cada pasada, así que se acumulan `_q<n>`: NO VERIFICADO** | no | **Patrón de cuarentena**: nunca borrar, mover a un nodo inactivo; útil para rehacer salas sin perder lo anterior |
| `CampaignAuthoring.cs` | Cola por archivo en `EditorApplication.update` (`Tick`, línea 29): operaciones `open`, `inspect`, `nav`, `finish`, `anchors`, `promote`, `upgrade`. `Copy()` (39) lanza si el destino existe. `Box()` (42) crea `CampaignCube_N` | Sí por negativa (`Copy`) | no | Cola de comandos por archivo para ejecutar operaciones del Editor sin UI |
| `CampaignCurrentAssets.cs` | Menciona `BunkerBox`/`Bunker_*`. Detalle NO VERIFICADO | NO VERIFICADO | no | — |
| `ChaseCorridorAuthoring.cs`, `TwoFloorGapAuthoring.cs`, `PhaseFlowAuthoring.cs` | Mencionan mallas o materiales del búnker (grep). Detalle NO VERIFICADO | NO VERIFICADO | no | — |
| `VisualPass/VisualPassF2.cs`, `VisualPassF3b.cs` | Mencionan mallas o materiales del búnker (grep). Detalle NO VERIFICADO | NO VERIFICADO | Algunos scripts de VisualPass sí usan Undo (grep) | — |
| `MapFx/IronCampaignMapFxRooms.cs` | Usa Undo (grep). Efectos por sala | NO VERIFICADO | sí | — |
| `Tests/CampaignTestRunner.cs`, `CampaignVerification.cs` | Verificación de campaña (no genera geometría) | — | — | Tests de referencia |
| `LevelAudit.cs` | Auditoría de niveles de solo lectura (menú `Levels/Audit + captures (read only)`) | Sí (solo lectura) | no | Auditoría previa a cambios |

Fuera de alcance por zona protegida (solo nombrados, no abiertos): `Editor/HangarIntro/*` (incluye `HangarSectorAuthoring.cs` y `HangarScrapAuthoring.cs`, que tocan Room00 y el Scrap de Room00) y `Editor/Menu/*` (incluye `HangarMenuAuthoring.cs`).

Tests relevantes ya existentes (PlayMode, verificados por nombre en VISUAL_OVERHAUL.md): `Assets/IronRequiem/Tests/PlayMode/VisualOverhaulTests.cs` (8 tests, "8/8 en la última pasada completa" según el doc, NO VERIFICADO en esta sesión), y `CampaignTests`.

---

### A6 Lista roja relevante y cuarentena

### A6.1 Lista roja cruzada con el kit
Fuente: `ProtectedZone/guardian.md` §1b y `before-20261117.txt`.

| Asset del kit | Lista roja | Motivo / consecuencia |
|---|---|---|
| `Art/Shaders/HardSurface/IronRequiemHardSurface.shader` (`20aba464`) | **sí** | Lo usan Room00 e IntroMenu (y todas las salas). **No modificar el shader**: cualquier variante nueva se hace en otro archivo |
| `Art/VisualOverhaul/Materials/VO_Env_RustMetal.mat` (`b296f58c`) | **sí** | Protegido. Copiar a `VO_Env_RustMetal_Copia.mat` para variar |
| `Art/Environment/Bunker/Textures/Bunker_RustMetal_Albedo.png` (`4b431a38`) y `_Normal.png` (`d4d2cfde`) | **sí** | Protegidas (no editar la textura). El `.mat` `Bunker_RustMetal.mat` **no** está en la lista |
| `Art/VisualOverhaul/Textures/VO_Noise.png` (`3305035890b4b3a4088e21c1d0c03c6a`) | **sí** | Compartida por todos los `VO_Env_*` y `VO_Prop_*`. No editar |
| `Art/Environment/Scrap/*/…BaseColor.jpg` (`1e6c9216`, `75ca8dae`, `9dc0f853`), `…fbx` (`0c98fa8f`, `6be3a608`, `02cbeb22`), `…_HS.mat` (`351ae00a`, `54d5f7e1`, `184bf16e`) | **sí (solo Room00)** | Solo Room00 los usa. Copiar antes de variar |
| `Prefabs/Scrap/Scrap_*.prefab` (`1df2d713`, `68562b04`, `12a422c5`) | **sí (solo Room00)** | Copiar |
| `Art/Rendering/CombatClarity.asset` | no (libre) | Ver A4.2 |
| `Art/Environment/Bunker/Bunker_*.mat` (excepto RustMetal textures) | no (libres) | Editables; los labs los usan |
| `Art/VisualOverhaul/Materials/VO_Decal_*.mat`, `VO_Prop_*.mat`, `VO_Light_*.mat`, `VO_Env_Floor/Wall/Ceiling/Grating/Hazard/Metal*.mat` | no (libres) | No aparecen en el snapshot |
| `Art/Menu/Textures/Hangar_Hazard.png`, `Art/Menu/Materials/HG_Hazard.mat` | **sí** | Son el peligro del hangar, **no** usar. Usar `Bunker_Hazard` (libre) |
| `Art/Menu/Materials/HG_*` (Plate, Trim, Hose, Floor, City, Glass, Fx*) | **sí** | No usar. Son del menú/hangar |
| `Assets/IronRequiem/Art/Menu/Materials/HG_MenuVolume` (VolumeProfile) | **sí** | No usar en el búnker. Ojo: `ZoneEnvironment` cambia de perfil por nombre, no por GUID (`guardian.md` §2) |
| `Prefabs/VFX/RobotBodyBurst_CombatPolish.prefab` (`6a412bf6`), `VisualPass/VFX/VP_VFX_HoverBot_Explosion.prefab` (`9da5a859`), `VFX/CannonExplosion/*`, `VFX/Mechanics/*` | **sí** | VFX de enemigos y menú. No son del kit de entorno |
| FPS template: `Pickup_Health`, `Wall_*`, `Floor_*`, `Sign_*`, `Dun_*` | no (0 coincidencias en `before-20261117.txt`) | Libres, pero visualmente no son del búnker |

Cruce de "escenas": el lab `BunkerLab` (y `BunkerBenchmark`, `VisualBench_A`) usa `Bunker_*` y las salas de Campaign **no** (A1). Las salas de campaña no dependen del kit búnker: un cambio en el kit no toca Room01–04 salvo que se cambie `HardSurface`, `VO_Env_RustMetal` o los `Scrap` (que están en la lista roja).

### A6.2 Cuarentena (`Tools/IronRequiem/Quarantine`)
- Carpetas presentes (verificado): `20261107/` (930 archivos, `Assets/` + `manifest.json`), `20261108-mesh-decals/` (solo `VO_ForwardRenderer.asset`), `20261115-hands-hud/` (vacía), `20261115-legacy-menu/` (`IndustrialShell.prefab`, `baseline-player`).
- Búsqueda por nombre dentro de la cuarentena con los términos bunker, scrap, VO_, VisualOverhaul, HardSurface, Light, Decal, Prop, Env: **ningún asset del kit**. Lo que hay son props de plantilla (`FPS/Art/Materials/Props/Jetpack.mat`, `StatueBig_*`), `ModAssets` (Archway, Obelisk, Pillar, Pyramid, FoodProps), `SecondaryScene`.
- Cuarentena interna de escenas: `Quarantine_v35` (nodo inactivo dentro de cada sala v35, creado por `LevelOpenPass.Root()`). **No es la carpeta de cuarentena**; no borrar.
- `Docs/IronRequiem/VisualOverhaul/CLEANUP_TABLE.md`: solo consultada como referencia. No tiene filas del kit (búsqueda por Bunker, Scrap, VisualOverhaul, CombatClarity, HardSurface: solo aparece la línea 3, que es la mención al CSV). No la usé para decidir nada.

---

### A7 Huecos: qué falta en el kit para un búnker

Búsqueda por nombre en `Assets/` (excluyendo Enemies y Weapons) de: locker, taquilla, bunk, litera, stretcher, camilla, terminal, valve, válvula, water, agua, console, monitor, desk, chair, table, shelf, rack, medkit, barrel, crate, pallet, barricada. Resultado: **sin assets** (los únicos hits son `Bunker*` por el patrón `bunk`, falsos positivos; y dos scripts de plantilla `Destructable.cs` y `UITable.cs`).

Faltan para un búnker (solo lista, sin proponer compras):
- Literas o camas (dormitorio de tropa).
- Taquillas o armarios metálicos.
- Camillas o mesas de enfermería.
- Terminales y consolas de control (con pantalla emisiva).
- Válvulas, volantes y manómetros para tuberías (solo hay tubo y brida en `VO_PipeRun2m`).
- Barricadas y sacos de arena, o cajas de munición apiladas.
- Barriles, bidones y cajas (no hay prefab de objeto; solo bloques).
- Elementos de agua: charcos, drenaje, grifos (la zona inundada necesita un plano de agua o decal de charco; no hay shader de agua en el kit).
- Puertas herméticas o compuertas (solo `Dun_BigDoor` de plantilla y `Wall_Door` de plantilla).
- Grifería, extintores, cajas eléctricas y paneles de distribución (solo hay `VO_StatusSlit`, sin prefab de panel completo).
- Escombros y bloques caídos (solo chatarra grande de Scrap, no hay fragmentos de hormigón ni varillas separadas).
- Vegetación o moho: no aplica pero sí suciedad de agua (`_DIRT` ya existe; no hay máscara de humedad específica).
- Cableado en cajas: solo haces de cable colgantes.
- Luces de emergencia rotas o parpadeantes como prefab (existe `LightFlicker` y tres tubos en el lab, no un prefab reutilizable documentado).

---

### Uso de la skill iron-requiem-performance

La leí completa al empezar (`.agents/skills/iron-requiem-performance/SKILL.md`). Criterios aplicados al valorar el kit:
- **Materiales compartidos e instancing**: el kit ya comparte materiales (`VO_Env_*` y `Bunker_*` son pocos `.mat` reutilizados por muchas mallas), y los `VO_*` tienen `instancing` según `Env()`/`Panel()` (verificado en el autor, no en `m_EnableInstancing`). El `LightFlicker` cambia entre dos materiales compartidos sin crear instancias (`VisualOverhaulDressing.SetFlickerMats`, línea 314).
- **Batching**: las piezas de `Dress()` se crean con `BatchingStatic | OccludeeStatic | ReflectionProbeStatic` (`VisualOverhaulDressing.cs:75`). El kit de BunkerBox (cubos) también es estático por su autor (`BunkerPolish`).
- **Presupuesto de luces**: el límite de 4 luces adicionales por objeto de URP se comprueba por renderer (`CheckLightBudget`, `VisualOverhaulDressing.cs:323`). Lo documenta `VISUAL_OVERHAUL.md` §3 (17 de 1.973 renderers del lab lo superan).
- **Medidas y triángulos**: no ejecuté ninguna medida (solo lectura). Los triángulos de los BunkerBox y VO_ se contaron desde `indexCount` del YAML. Los del FBX de Scrap son NO VERIFICADOS. No abrí el informe T-07 (no era parte de la tarea).
- **Coste conocido del entorno**: `VISUAL_OVERHAUL.md` §6 mide que el entorno HardSurface + decales + piezas cuesta ~1,0 ms de media en el benchmark de combate. Es un dato del equipo, no una medida mía.

---

### Conclusiones

1. El kit útil y libre de la lista roja es el **Bunker** (`Bunker_*.mat` con texturas de hormigón, óxido, rejilla, peligro y lámparas) más las mallas **VisualOverhaul** (tubería 244 tris, cable 600, rejilla 144, luces 24-108) y los **BunkerBox_N** (226 cubos de 12 tris). Los decales `VO_Decal_*` también son libres.
- Lo bloqueado es el **HardSurface** (ni se modifica), **VO_Env_RustMetal** y sus texturas, **VO_Noise**, y los **Scrap** (solo Room00). Para variar, copiar primero.
- Sin props de objeto: no hay cajas, barriles, coberturas, camillas, terminales ni válvulas. Son la mayor carencia para un búnker (A7).
- Las herramientas de autoría que sirven de patrón son `VisualOverhaulAssets` (mallas y materiales procedurales), `VisualOverhaulDressing` (colocación por raycast) y `LevelOpenPass` (cuarentena en lugar de borrado). Ninguna usa Undo y `Dress()` no limpia la raíz previa, así que para un mapa nuevo conviene añadir Undo y limpieza explícita.


---

<!-- Anexo B_enemigos.md -->

## Anexo · F0-B · Inventario de enemigos vigentes (campaña) — solo lectura

Proyecto: `C:\Users\gehbd\ProyectoGameFinal`. No se abrió Unity, no se escribió nada en el proyecto, no se usó git que escriba ni red.
Método: los `*_Current` se leyeron con `Assets/IronRequiem/Editor/CampaignCurrentAssets.cs` (Folder `Assets/IronRequiem/Prefabs/Campaign`). Las referencias de escena se leyeron de `Assets/IronRequiem/Scenes/Campaign/*.unity`. Los valores efectivos se resolvieron recorriendo la cadena de variantes (`m_SourcePrefab` del PrefabInstance raíz, `m_TransformParent: {fileID: 0}`) con un script auxiliar de YAML (`scratchpad/f0/resolve.py`). Las cifras de cadena son el primer override encontrado de abajo arriba; cuando no aparece override se usa el valor propio o base. Donde el resolutor no llegó, se marca NO VERIFICADO.

Skills leídas: `iron-requiem-enemy-ai` y `iron-requiem-modular-weapons` (SKILL.md). Aplicado: IA (estados y pérdida de capacidad por evento, no por destruir controlador), y armas (separar desprendimiento, integridad de unión y arma temporal; no tocar el FBX ni inventar pivotes).

---

### Hallazgos que cambian el diseño (leer primero)

1. **En toda la campaña, las uniones (DetachableCannon) están en `executionOnly = 1`** (Campaign T07_Current, T07_Mech; BigBot). `DetachableCannon.Detach()` retorna sin hacer nada si `executionOnly` (`Assets/IronRequiem/Scripts/Enemies/DetachableCannon.cs:43`). Es decir: hoy un T-07 de campaña **no** se desprende al destruir su unión de disparo; solo se desprende por ejecución (<75 % de vida). Si el diseño exige "destruir la unión para desprender", hay que decidirlo explícitamente (ver B4).
2. **El T-07 de campaña es ejecutable y su arma temporal es `Weapon_T07_Temporary_Optimized`** (vía `FastCombat/T07_Module.prefab`).
3. **Big Bot no es ejecutable en campaña** (`executionEnabled = 0`) y no suelta cañón coleccionable (`BigBotDestruction.executionOnly = 1`, `BigBotDestruction.cs:105`).
4. **Contradicción a verificar en Editor: Carroñero.** El checklist dice "Sin ejecución ni robo" (`Docs/IronRequiem/CHECKLIST_TRABAJO.md:866`), pero el componente `EnemyExecutionSource` está heredado de SawBot_Arsenal_v03 con `m_Enabled: 1` y `healthThreshold 0.75` (`Assets/IronRequiem/Prefabs/Enemies/SawBot_Arsenal_v03.prefab:621`), y `Campaign/Carronero.prefab` no sobrescribe `executionEnabled`. Resultado: NO CONCLUYENTE; el prefab sugiere que sí es ejecutable.
5. **SawBot de campaña es ejecutable** y su módulo robable es `SawBot_AnimatedModule_v02` (override en `SawBot_SawPolish_v04.prefab:10676-10678`).

---

### B1 Variantes vigentes

Convención: "Cadena" = variantes desde la base hasta el prefab vigente (la primera fila de cada cadena es la más cercana). Salud, detección, ataque y velocidad se resolvieron por cadena; "own" = el valor está en el propio archivo o en su base directa.

| Tipo | Prefab vigente (ruta · GUID) | Deriva de (cadena resumida) | Salud | Detección / ataque | Velocidad (NavMeshAgent / NavigationModule.MoveSpeed) | Agente radio / altura · agentTypeID | Arma desprendible → arma temporal del jugador | Ejecutable (<75 %) | Fuente |
|---|---|---|---|---|---|---|---|---|---|
| **T-07 común con cañón** | `Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab` · `28eb30e852d76494b903d8b75b5f5654` | VO `VisualOverhaul/T07_HitFeedback_v04_NavThrottle_VO` ← `SecondaryShotgun/T07_HitFeedback_v04_NavThrottle` ← `CombatFollowup/T07_HitFeedback_v04` ← `FastCombat/T07_HitFeedback_v04_Fast` ← `Enemies/T07_CombatPolish` ← `Enemies/T07_Common_Destructible` ← `Enemies/T07_Common_Optimized` | **128** (override `FastCombat/T07_HitFeedback_v04_Fast.prefab`) | **Detección 45 m** y **ataque 38 m** (override propio en `Campaign/T07_Current_Repaired.prefab`) | **4,2** / **4,2** (`FastCombat/T07_HitFeedback_v04_Fast.prefab`) | **0,4 / 2,05** (heredado de `Enemies/T07_IR_v01.prefab`; coincide con `Campaign/T07_Mech.prefab` own) · agentTypeID 0 (solo confirmado en T07_Mech) | **Sí**: `DetachableCannon` en `CannonJoint`, **`executionOnly = 1`** (campaña) → solo se desprende por ejecución. Módulo `EnemyExecutionSource.modulePrefab` (own) = `FastCombat/T07_Module.prefab` → `weaponPrefab` = `Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab`. Energía: `CannonAmmo.capacity = 100` (`FastCombat/T07_Module.prefab:5131`). OVERDRIVE: `CannonOverdrive.criticalThreshold 0,2`, `criticalGraceDuration 5 s` (`T07_Module.prefab:5241-5242`) | **Sí**, `healthThreshold 0,75` (asignado por `CampaignCurrentAssets.cs:33`; `Enemies` ya usa 0,75 en `Campaign/T07_Mech.prefab`) | Resolución por cadena; `CampaignCurrentAssets.cs:76-92`; `Campaign/T07_Current_Repaired.prefab` |
| **T-07 Mech (campaña)** | `Assets/IronRequiem/Prefabs/Campaign/T07_Mech.prefab` · `7df4469d35d854b4c9a7f612c8568239` | Archivo propio (`own`); copia del mech probado (checklist v24, `CHECKLIST_TRABAJO.md:855-860`). No pasa por `CampaignCurrentAssets` | **128** (own, `T07_Mech.prefab:14814`) | Detección/ataque **NO VERIFICADO** en el archivo (sin valor propio resoluble) | **3** (`NavMeshAgent m_Speed`, `T07_Mech.prefab:14842`) · **4,2** (`NavigationModule.MoveSpeed`, `:15031`) | **0,4 / 2,05** (`:14841`, `:14850`) · agentTypeID 0 (`:14843` aprox.) | **Sí**: `DetachableCannon` en `CannonJoint` (**salud propia 35**, `T07_Mech.prefab` `Health` de `CannonJoint`), **`executionOnly: 1`** (`T07_Mech.prefab:47642`). Módulo `EnemyExecutionSource.modulePrefab` → `FastCombat/T07_Module.prefab` (guid `2550b58e12816c44b8f7fa38b086704f`) → `Weapon_T07_Temporary_Optimized.prefab` | **Sí**, `healthThreshold 0,75` (own, `T07_Mech.prefab`) y `executionEnabled 1` | `Campaign/T07_Mech.prefab` |
| **Big Bot** | `Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab` · `e78fe7bc0d09a4c429a26845fc72bbd3` | VO `BigBot_Burst_v10_NavThrottle_VO` ← `SecondaryShotgun/BigBot_Burst_v10_NavThrottle` ← `CombatFollowup/BigBot_Burst_v10` ← `EnemyRange/BigBot_Burst_v10_Fast_Range` ← `FastCombat/BigBot_Burst_v10_Fast` ← `Enemies/BigBot_Burst_v10` ← `Enemies/BigBot_ReferenceCharge_v09` ← `Enemies/BigBot_Explosive_v08` ← `Enemies/BigBot_HitFeedback_v04` | **960** (override `FastCombat/BigBot_Burst_v10_Fast.prefab`) | **Detección 36 m** (override `Enemies/BigBot_Explosive_v08.prefab`) · **ataque 28,6 m** (override `EnemyRange/BigBot_Burst_v10_Fast_Range.prefab`) | **1,68** (NavMeshAgent y MoveSpeed, `FastCombat/BigBot_Burst_v10_Fast.prefab`) | **NO VERIFICADO** (no hay bloque NavMeshAgent resoluble en la cadena) | **No en campaña**: `executionEnabled = 0` (`Campaign/BigBot_Current.prefab`) y `BigBotDestruction.executionOnly = 1` (override `FastCombat/BigBot_Burst_v10_Fast.prefab`); el cañón no se vuelve coleccionable (`BigBotDestruction.cs:105`). Módulo `Prefabs/Weapons`/`CombatFollowup/BigBot_Module.prefab` existe pero no se usa | **No** | `Campaign/BigBot_Current.prefab`; `Editor/CampaignCurrentAssets.cs:32,94` |
| **SawBot** | `Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab` · `b33f8cb85dbefc347b495aadf2aa5cd6` | `Enemies/SawBot_SawPolish_v04` ← `Enemies/SawBot_Arsenal_v03` ← `Enemies/SawBot_Sustained_v02` ← `Enemies/SawBot_AI_v01` (base) | **140** (own en `Enemies/SawBot_AI_v01.prefab:5356`) | **Detección 22 m** (`SawBot_AI_v01.prefab:6021`) · **ataque 3 m** (override `SawBot_Sustained_v02.prefab:332`) | **9,5** (NavMeshAgent, override `Campaign/SawBot_Current.prefab:1221`) · **7,2** (MoveSpeed, `SawBot_Sustained_v02`) | **0,8 / 1,6** (own, `SawBot_AI_v01.prefab:5477,5486`) · agentTypeID 0 (`:5476`) | **No hay unión desprendible** (sin `DetachableCannon` en la cadena). Robable por ejecución: módulo `SawBot_AnimatedModule_v02.prefab` (override en `SawBot_SawPolish_v04.prefab:10676-10678`). Arma final: **NO VERIFICADO** (el `weaponPrefab` de ese módulo no se leyó; el nombre de la sierra temporal no consta). Sin pickup automático de drop (`CampaignCurrentAssets.cs:95`) | **Sí**, `healthThreshold 0,75` (asignado por `CampaignCurrentAssets.cs:33,95`) | `Campaign/SawBot_Current.prefab` |
| **Carroñero** (horda, hangar y rondas) | `Assets/IronRequiem/Prefabs/Campaign/Carronero.prefab` · `f6b6432993420e5409d267ac7782cdd0` | `Enemies/SawBot_SawPolish_v04` ← `Enemies/SawBot_Arsenal_v03` ← `Enemies/SawBot_Sustained_v02` ← `Enemies/SawBot_AI_v01` (sobre la base SawBot, `CHECKLIST_TRABAJO.md:866`) | **26** (override `Campaign/Carronero.prefab:4295`) | **Detección 22 m** (heredado de `SawBot_AI_v01:6021`, sin override) · **DetectionInterval 0,1 s** (override `Carronero.prefab:4075`) · **ataque 3 m** (override de `SawBot_Sustained_v02:332`) | **7,2** (NavMeshAgent override `Carronero.prefab:4095`) · **7,2** (MoveSpeed, `SawBot_Sustained_v02:347`) | **0,32 / 1,15** (overrides `Carronero.prefab`) · agentTypeID 0 | **No hay unión desprendible** (sin `DetachableCannon`). Ejecución: **NO CONCLUYENTE** (ver hallazgo 4): `EnemyExecutionSource` habilitado y heredado, sin `executionEnabled = 0` | **NO CONCLUYENTE** (el checklist dice que no; el prefab no lo desactiva) | `Campaign/Carronero.prefab`; `CHECKLIST_TRABAJO.md:866-871` |
| **HoverBot** (dron) | `Assets/IronRequiem/Prefabs/Campaign/HoverBot_Current.prefab` · `2dbdd51a8c5a3b447ad64f8d210b6b8f` | `SecondaryShotgun/HoverBot_CombatPolish_NavThrottle` ← `CombatFollowup/HoverBot_CombatPolish` ← `EnemyRange/HoverBot_CombatPolish_Fast_Range` ← `FastCombat/HoverBot_CombatPolish_Fast` ← `Enemies/HoverBot_CombatPolish` ← `FPS/Enemies/Enemy_HoverBot` | **80** (override `FastCombat/HoverBot_CombatPolish_Fast.prefab:13-14`); base 100 (`Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab:201`) | **Detección 20 m** (`Enemy_HoverBot.prefab:475`) · **ataque 12,5 m** (override `EnemyRange/HoverBot_CombatPolish_Fast_Range.prefab:18-19`; base 10 m `:476`) | **4,2** (NavMeshAgent, `FastCombat/HoverBot_CombatPolish_Fast.prefab:18-19`). Sin MoveSpeed | **0,5 / 2** (own, `Enemy_HoverBot.prefab:228,237`) · agentTypeID 0 | No (sin `DetachableCannon` ni `EnemyExecutionSource` en la cadena) | **No** | `Campaign/HoverBot_Current.prefab` |
| **Torreta** | `Assets/IronRequiem/Prefabs/Campaign/Turret_Current.prefab` · `7923a148bef66f34e82fb58b7ed67749` | `CombatFollowup/Turret_CombatPolish` ← `EnemyRange/Turret_CombatPolish_Fast_Range` ← `FastCombat/Turret_CombatPolish_Fast` ← `Enemies/Turret_CombatPolish` ← `FPS/Enemies/Enemy_Turret` | **320** (override `FastCombat/Turret_CombatPolish_Fast.prefab`); base 400 (`Enemy_Turret.prefab:283`) | **Detección 45,75 m** y **ataque 43,75 m** (override `EnemyRange/Turret_CombatPolish_Fast_Range.prefab`); base 30/35 m (`Enemy_Turret.prefab:456-457`) | **4,2** (NavMeshAgent, `FastCombat/Turret_CombatPolish_Fast.prefab`). Movimiento de la torreta: NO VERIFICADO | **0,5 / 2** (own, `Enemy_Turret.prefab:366,375`) · agentTypeID 0 | No | **No** (`LootPrefab: {fileID: 0}`, `Enemy_Turret.prefab:266`) | `Campaign/Turret_Current.prefab` |

Notas de la tabla:
- "Cadena" usa los `m_SourcePrefab` del PrefabInstance raíz. Algunas variantes tienen PrefabInstance anidados (no contados, p. ej. `CannonCharge_Magenta` en la cadena del T-07).
- Dos T-07 de campaña coexisten. `T07_Current_Repaired` es el prefab que `CampaignCurrentAssets.Apply` regenera; `T07_Mech` (v24) se añadió a las rondas aparte. Ambos son ejecutables y ambos tienen `executionOnly = 1`.
- `DropRate`: en el Carroñero quedó 0 (checklist, `Campaign` `Policy`); en el resto no se resolvió. `Policy()` (`CampaignCurrentAssets.cs:34`) solo pone DropRate 0 si el loot es arma; el HoverBot suelta `Pickups/Loot_Health` (`LootPrefab` guid `9973c412...` → `Assets/FPS/Prefabs/Pickups/Loot_Health.prefab`, `DropRate 1` en la base).
- Salud de la unión del T-07 de campaña (`T07_Current_Repaired`): **NO VERIFICADO** (T07_IR_v01 y T07_Mech la tienen a 35).

---

### B2 Otras variantes existentes y estado

| Estado | Prefab (ruta) | Qué es / por qué no es vigente |
|---|---|---|
| **Vigente, base de cadena** | `Assets/IronRequiem/Prefabs/Enemies/{T07_IR_v01, T07_Common_Optimized, T07_CombatPolish, BigBot_Burst_v10, SawBot_SawPolish_v04, HoverBot_CombatPolish, Turret_CombatPolish}.prefab` | Bases de las cadenas de arriba. No se cargan en las rondas directamente. |
| **Vigente en VO (visual)** | `Assets/IronRequiem/Prefabs/VisualOverhaul/{T07_HitFeedback_v04_NavThrottle_VO, BigBot_Burst_v10_NavThrottle_VO, HoverBot_CombatPolish_VO, Turret_CombatPolish_VO, Enemy_Turret_VO, T07_LegRepair_Charged_v05_Execution_VO, BigBot_Burst_v10_Execution_VO}.prefab` | Capa visual usada como base de los `*_Current`. Los de `Execution_VO` los consume solo la `Policy` antigua (mapa de reemplazo en `CampaignCurrentAssets.cs:98`). |
| Obsoleto para campaña | `Assets/IronRequiem/Prefabs/PhaseFlow/{T07_LegRepair_Charged_v05_Execution, BigBot_Burst_v10_Execution}.prefab` | Fases anteriores; el mapa de `CampaignCurrentAssets.cs:98` los sustituye por los `*_Current`. |
| Laboratorio / pruebas | `Assets/IronRequiem/Prefabs/FastCombat/*`, `EnemyRange/*`, `CombatFollowup/*`, `SecondaryShotgun/*` | Variantes de rendimiento o de laboratorio; son los ancestros de los `*_Current`. No editar como si fueran vigentes. |
| Pendiente de integrar (anim) | `Assets/IronRequiem/Prefabs/VisualPass/T07Robot/T07_RobotAnim_Test.prefab` | Clips `R_*` del T-07 (`Docs/IronRequiem/T07_ROBOT_ANIM_20261118.md`). Integración en `T07_Current_Repaired` pendiente de aprobación (`CHECKLIST_TRABAJO.md:1421-1423`). |
| Pendiente de integrar (anim) | `Assets/IronRequiem/Prefabs/VisualPass/SawBotRobot/SawBot_RobotAnim_Test.prefab` | Clips `R_Saw*` del SawBot (`Docs/IronRequiem/SAWBOT_ROBOT_ANIM_20261118.md`). Pendiente en `SawBot_Current` (`CHECKLIST_TRABAJO.md:1430`). |
| Pendiente de integrar (anim) | `Assets/IronRequiem/Prefabs/VisualPass/Mech/T07_Mech_RobotAnim_Test.prefab` | Clips del Mech con disparador de golpe `MechHitReactionTrigger` (solo en la variante). Integración en `T07_Mech.prefab` pendiente (`CHECKLIST_TRABAJO.md:1410-1411`). |
| Test (no campaña) | `Assets/IronRequiem/Prefabs/VisualPass/Mech/T07_Mech_Test.prefab`, `Scenes/RequiemCampaign_MechTest.unity`, `Room0*_MechTest.unity` | Escenas de prueba del mech. No consumen para la campaña. |
| Menú | `Assets/IronRequiem/Prefabs/Menu/MenuWalker_T07.prefab`, `MenuWalker_Carronero.prefab` | Copias decorativas del menú (`CHECKLIST_TRABAJO.md` v27: "no las copias del menú"). |
| Prototipo (no campaña) | `ENEMY_V01` (`Docs/IronRequiem/ENEMY_V01.md:24`) | Prueba con salud de cuerpo 160, unión 35, detección 16 m, ataque 10 m, navegación 3,5 m/s. No es el T-07 vigente. |
| Bunker / labs | `Scenes/BunkerLab.unity`, `BunkerBenchmark.unity` | Cola de aparición `EnemySpawnQueue` (`BUNKER_POLISH.md:106`). |

---

### B3 Enemigos por sala en la campaña actual

Fuente: `CombatTwoRoomsLab` en `Assets/IronRequiem/Scenes/Campaign/Room0*.unity` (waves y variations), `ChaseCorridorEncounter` en `Pursuit05.unity` (ambushes), y `Tools/IronRequiem/Reports/MapFxAudit2/map-structure.md` (líneas 128-132, mismos totales). Las variaciones de Room01 se eligen al azar por ronda (`CombatTwoRoomsLab.cs:58`, seed `RunSeed`). `CampaignAuthoring` activa `ConfigureVariety(true)` en las salas de `Names` (lo ejecuta el editor de campaña; ver `Assets/IronRequiem/Editor/CampaignAuthoring*.cs`, línea 24 de `Names`). Room02-04 no tienen variaciones: se juega siempre la lista base.

| Sala | Olas | Composición (cuenta por ola) | Total |
|---|---|---|---|
| **Room01_Foundry** (variaciones: "Presión mixta", "Flancos móviles", "Apoyo y asalto") | W0 | 6 — variación: T-07 común 2-3, SawBot 2, HoverBot 1-2 | 6 |
| | W1 | 4 — variación: BigBot 1, T-07 común 1-3, SawBot 1, HoverBot 1 | 4 |
| | W2 | 6 — variación: BigBot 2, HoverBot 1, SawBot 1, T-07 común 1, **T-07 Mech 1** | 6 |
| | | | **16 por ronda** |
| **Room02_Breach** (sin variaciones) | W0 | T-07 común 4, SawBot 2 | 6 |
| | W1 | BigBot 2, HoverBot 3, **T-07 Mech 1**, **Carroñero 8** | 14 |
| | W2 | BigBot 4, Torreta 1, **T-07 Mech 1** | 6 |
| | | | **26** |
| **Room03_Assembly** (sin variaciones) | W0 | SawBot 4, T-07 común 2 | 6 |
| | W1 | BigBot 2, T-07 común 3, **T-07 Mech 1** | 6 |
| | W2 | SawBot 3, BigBot 2, HoverBot 2, **T-07 Mech 1**, **Carroñero 14** | 22 |
| | | | **34** |
| **Room04_Reactor** (sin variaciones) | W0 | T-07 común 3, HoverBot 3 | 6 |
| | W1 | BigBot 2, SawBot 2, Torreta 1, **T-07 Mech 1** | 6 |
| | W2 | BigBot 3, SawBot 3, **T-07 Mech 1**, **Carroñero 20** | 27 |
| | | | **39** |
| **Pursuit05** (persecución, `ChaseCorridorEncounter.ambushes`) | 3 emboscadas | T-07 común 4 + SawBot 1 | 5 |
| **Room00b_AlarmCorridor** (en la ruta del hangar según `HangarSectorAuthoring.cs:52`; **no** está en `CampaignAuthoring.Names`) | W0 | T-07 común 2, Carroñero 1 | 3 |
| | W1 | Carroñero 2 | 2 |
| | | | **5** |
| **Room00_Hangar** (sector 0, exploración) | — | 5 Carroñeros reales (`Carronero.prefab`, `ScavengerBehaviour`), pasivos hasta recibir daño (`CHECKLIST_TRABAJO.md` v27) | 5 pasivos |

Totales de una partida (salas de combate + Pursuit05 + Room00b): **125** enemigos (16 + 26 + 34 + 39 + 5 + 5). Conteo por tipo, sumando todas las olas (Room01 con una variación cualquiera; cada composición da 3 Big Bot en Room01): **T-07 Mech 7** (1 en Room01, 2 en cada una de Room02-04), **Big Bot 18** (R01 3, R02 6, R03 4, R04 5), **SawBot ~19** (R01 4, R02 2, R03 7, R04 5, Pursuit05 1), **Carroñero 45** en combate (R02 8, R03 14, R04 20, Room00b 3) más 5 en hangar. El T-07 común y el HoverBot y la torreta se reparten en el resto de la tabla (no sumados aquí).

---

### B4 Notas de rendimiento y de diseño

Rendimiento (lo que consta):
- El T-07 pesa 75-85 % del coste de aparición en el laboratorio (`Docs/IronRequiem/BUNKER_POLISH.md:95`, con el bunker). Se corrigió con `EnemySpawnQueue` (sección 7) pero la cola se activó solo en BunkerLab/BunkerBenchmark (`BUNKER_POLISH.md:106`). Las salas de campaña sí referencian `spawnQueue` (`Room01_Foundry.unity:8003`), así que el coste en campaña **no está medido** (NO VERIFICADO).
- Carroñero: `DetectionInterval 0,1 s`, sin `RobotHitFeedback`, sin `EnemyStateLights`, evasión LowQuality, audio prioridad 200 (`CHECKLIST_TRABAJO.md:869`). LateUpdate pasó de 3,7 a 1,25 ms para 24 (solo editor).
- Big Bot: 960 HP y 1-1,5 ms de aparición cada uno (`BUNKER_POLISH.md:95`).
- Torreta: detección 45,75 m / ataque 43,75 m. Es el enemigo de mayor alcance; con `NavMeshAgent` a 4,2 m/s pero es torreta (no se ha confirmado que se mueva).

Diseño (lo que la configuración permite o impide):
- **Ejecución vs. unión.** Hoy la mecánica "destruir unión → arma" no se ejecuta en campaña para T-07 ni Big Bot (`executionOnly = 1`). Solo SawBot, T-07 y, si se confirma, Carroñero, sueltan un módulo al ejecutarse (<75 % de vida). Es el punto que más conviene decidir antes de colocar enemigos.
- **Energía de la arma del T-07.** 100 de capacidad con OVERDRIVE en el 20 % y 5 s de gracia (`T07_Module.prefab:5131,5241-5242`). Es el dato de referencia para dimensionar cuántos T-07 hacen falta por sala para llegar a OVERDRIVE.
- **Enemigos que tapan la línea de tiro de otros.** No consta en los datos. Requiere geometría de sala (no inspeccionada). En Room02 hay una torreta fija (`CentralTurret` en 0;8;14, `map-structure.md:130`) que, con 43,75 m de ataque, cubre casi toda la sala: NO VERIFICADO el cruce con los BigBot de W2.
- **Alcance de combate.** T-07 38 m de ataque con 45 m de detección; torreta 43,75 m; Big Bot 28,6 m; HoverBot 12,5 m; SawBot/Carroñero 3 m (cuerpo a cuerpo). Un arma de 100 de energía en T-07 se agota en pocos disparos de cañón; las armas de cuerpo a cuerpo (sierra) se consumen con el uso.
- **Variación aleatoria en Room01.** Cada ronda elige una de tres composiciones por ola, así que la economía de armas varía por partida. Cualquier cálculo de "armas por sala" debe hacerse para las tres variantes (16 enemigos por ronda en las tres).
- **Límite de cuerpo a cuerpo en la horda.** 42 Carroñeros en Room02-04 (+5 en hangar). Son los enemigos con mayor número y menor alcance; los SawBot (3 m) quedan en la misma ola.

---

### Conclusiones

- Las variantes vigentes son los `*_Current` de `Assets/IronRequiem/Prefabs/Campaign/`; sus cadenas terminan en los `Enemies/*` y en los `FastCombat/*`, `EnemyRange/*` (cifras de salud, detección y ataque de B1).
- En campaña las uniones del T-07 y del Big Bot son `executionOnly`: el arma se obtiene por ejecución (<75 %), no al destruir la unión. Cualquier diseño de "desprender por daño" requiere cambiar esa política.
- Ejecutables y con arma temporal: T-07 (`Weapon_T07_Temporary_Optimized`, energía 100, OVERDRIVE 20 %/5 s) y SawBot (`SawBot_AnimatedModule_v02`, arma no verificada). Big Bot no. Carroñero NO CONCLUYENTE.
- Campaña: 125 enemigos por partida en salas (Room01 16 con variación aleatoria, Room02 26, Room03 34, Room04 39, Pursuit05 5, Room00b 5) más 5 Carroñeros en el hangar.
- Antes de decidir la economía: verificar en Editor el Carroñero, la salud de la unión del T-07 de campaña y el arma del SawBot.

### Pendientes y limitaciones

- Verificar en Editor (sin tocarlo, solo lectura de la vista del Inspector): `executionEnabled` y `m_Enabled` del `EnemyExecutionSource` del Carroñero; salud de la unión del T-07 de campaña; `weaponPrefab` de `SawBot_AnimatedModule_v02` (arma temporal del SawBot).
- Radio/altura/agentTypeID del Big Bot y de la torreta; si la torreta se mueve.
- Verificar DropRate del T-07 y de Big Bot en campaña (no resuelto).
- Medir en build (el coste de 75-85 % del T-07 viene de bunker, no de campaña).

Skills aplicadas: `iron-requiem-enemy-ai` (estados, pérdida de capacidad por evento, sin tocar el controlador de navegación) e `iron-requiem-modular-weapons` (separación de unión, módulo y arma; no se usó geometría ni pivotes inventados). Ningún otro archivo se modificó.


---

<!-- Anexo C_sistemas_campana.md -->

## Anexo · F0-C · Inventario de sistemas de campaña reutilizables (solo lectura)

Fecha: 18/11/2026. Proyecto: `C:\Users\gehbd\ProyectoGameFinal` (Unity 6000.5.5f1, ensamblado `IronRequiem.Runtime`).
Rutas relativas a la raíz del proyecto. Las líneas son del archivo citado (verificadas con lectura o grep; "~" = aproximada).

**Alcance y método.** Lectura directa de `Assets/IronRequiem/Scripts/Levels/*.cs` citados, grep de referencias en `Assets/`, `Assets/IronRequiem/Editor/CampaignAuthoring.cs`, `Assets/IronRequiem/Editor/Campaign/`, `Tools/IronRequiem/Reports/MapFxAudit2/doors.md` y `map-structure.md`, `Tools/IronRequiem/Reports/ProtectedZone/guardian.md`, y grep puntual de `Assets/IronRequiem/Scenes/RequiemCampaign.unity` (bloque de `CampaignFlow`, líneas 51142-51169). No se abrió Unity, no se ejecutó Play, no se leyeron escenas completas ni binarios NavMesh. Lo que dependa de Play queda como **NO VERIFICADO**.

**Estado.** Solo se escribió este archivo. No se modificó ningún otro archivo ni se usó git que escriba.

---

### C1 Mapa de sistemas

| Sistema | Ruta | Responsabilidad | Quién lo usa / de quién depende |
|---|---|---|---|
| CampaignFlow | `Assets/IronRequiem/Scripts/Levels/CampaignFlow.cs` | Máquina de estados: carga aditiva de etapas, esclusas, descarga, entorno, textos | Objeto `CampaignSession` en la escena de sesión (`RequiemCampaign.unity`). Usa CampaignAirlock, CampaignRoom, CombatLabMenu, AirlockDoorMotion, AirlockCycle, ZoneEnvironment |
| CampaignRoom | `.../Levels/CampaignRoom.cs` | Adaptador de sala: `Completed`, `Ready`, título, prólogo | Raíz de cada escena de sala. Lo busca CampaignFlow (CampaignFlow.cs:243) |
| CampaignAirlock | `.../Levels/CampaignAirlock.cs` | Trigger de paso entre etapas y referencias a puertas | Escena de sesión. Llama a `CampaignFlow.TryTransition` |
| AirlockDoorMotion | `Assets/IronRequiem/Scripts/VisualPass/AirlockDoorMotion.cs` | Animación vertical de puertas y `IsClosed` | Clase estática. La llama CampaignFlow |
| AirlockCycle | `.../Levels/AirlockCycle.cs` | Presentación del ciclo (luces, vapor, audio) | Opcional. Según doors.md solo existe en ConnectionChamber |
| CombatTwoRoomsLab | `.../Levels/CombatTwoRoomsLab.cs` | Rondas/oleadas, slots, spawn seguro, `Completed` | Componente de la escena de sala. Enlazado en `CampaignRoom.rounds` |
| EnemySpawnQueue | `.../Levels/EnemySpawnQueue.cs` | Precalentado y activación escalonada de enemigos | CombatTwoRoomsLab, ChaseCorridorEncounter |
| ChaseCorridorEncounter | `.../Levels/ChaseCorridorEncounter.cs` | Persecución con sierra y emboscadas | `CampaignRoom.pursuit` (Pursuit05) |
| ChaseSceneEntrance | `.../Levels/ChaseSceneEntrance.cs` | Carga `ChaseCorridorLab` desde el laboratorio | Solo laboratorio. CampaignAuthoring.cs:201 lo destruye en la sesión |
| GapFallRecovery | `.../Levels/GapFallRecovery.cs` | Devuelve al jugador al caer en una grieta (daño 20) | Escenas de sala. CampaignRoom.Awake la enlaza (CampaignRoom.cs:33) |
| CorridorAlarm | `.../Levels/CorridorAlarm.cs` | Alarma visual y sonora sobre CombatTwoRoomsLab | Room00b (solo presentación) |
| LabRoomVisibility | `.../Levels/LabRoomVisibility.cs` | Ocultación por pisos o zonas | Solo laboratorios. map-structure.md 2.1 confirma que no se usa en campaña |
| WeaponMountStation | `.../Levels/WeaponMountStation.cs` | Montaje del cañón robado en el lobby (5,6 s) | Escena de sesión. Usa `CampaignFlow.Message` (~l.506) y PlayerWeaponsManager |
| CombatLabMenu | `.../Levels/CombatLabMenu.cs` | Pausa, muerte, reinicio, `IsPlaying` | Sesión. Lo consultan rondas, persecución y música |
| ZoneEnvironment | `.../Levels/ZoneEnvironment.cs` | Captura y mezcla de RenderSettings, Volumes y AudioSources | CampaignFlow |
| VolumeFadeIn | `Assets/IronRequiem/Scripts/VisualPass/VolumeFadeIn.cs` | Fade del volumen de sector | Lo reclama `ZoneEnvironment.SceneFade.Bind` |
| SectorLightingBlend | `.../VisualPass/SectorLightingBlend.cs` | Suaviza ambiente y niebla al cambiar la escena activa | Sesión (según map-structure.md) |
| CombatMusic | `Assets/IronRequiem/Scripts/Audio/CombatMusic.cs` | Música de exploración y combate | Sesión. Busca `EnemyManager` (CombatMusic.cs:69) |
| HealthPickup | `Assets/FPS/Scripts/Gameplay/HealthPickup.cs` | Curación (`HealAmount`) | Prefab `Pickup_Health` (CampaignAuthoring.cs:176) |
| CombatTuningProfile | `.../Levels/CombatTuningProfile.cs` | ScriptableObject con multiplicadores | Solo `Editor/FastCombatAuthoring.cs:44` (lab). **No lo usa la campaña** |
| CampaignAuthoring (Editor) | `Assets/IronRequiem/Editor/CampaignAuthoring.cs` | Construcción de la campaña actual | Solo Editor. Operación `build` no reejecutable (l.105, lanza si la campaña ya existe) |
| IronCampaignProbe (Editor) | `Assets/IronRequiem/Editor/Campaign/IronCampaignProbe.cs` | Volcados de sólo lectura | Solo Editor |

Dependencias base de todos: `PlayerCharacterController` (`Assets/FPS/Scripts/Gameplay/PlayerCharacterController.cs`, `IsDead` l.104, `TeleportToCheckpoint` l.110), `Health` (`Assets/FPS/Scripts/Game/Shared/Health.cs`, `OnDamaged` l.13, `OnDie` l.15, `Heal` l.37) y `EnemyController`/`EnemyMobile` (`Assets/FPS/Scripts/AI`).

---

### C2 Flujo de campaña paso a paso

Todo se refiere a `CampaignFlow.cs` salvo indicación. La campaña actual tiene 7 etapas: `Room00_Hangar`, `Room00b_AlarmCorridor`, `Room01_Foundry`, `Room02_Breach`, `Room03_Assembly`, `Room04_Reactor`, `Pursuit05` (RequiemCampaign.unity:51148-51155). `prologueStages: 2`.

**Arranque**
1. `Awake` (l.44-50): sube la prioridad de carga, ajusta el upload de streaming, muestra `startCover` si `prologueStages>0` (l.49) y ejecuta `startupVariants.WarmUp()`.
2. `Start` (l.92): lanza `WarmFonts` (glifos en el charset español, l.73-85; no se ejecuta en Editor, l.78) y espera `Prepare(0)`.
3. `Prepare(0)`:
   - Captura el entorno neutro (l.183) y valida `CanStreamedLevelBeLoaded(stageScenes[target])` (l.185). Si falla, muestra "ERROR · FASE NO DISPONIBLE" y aborta con `Busy` ya puesto.
   - Como `CurrentStage<0`, carga la etapa 0 en aditivo con activación retenida (l.195, `StartLoad(true)`).
   - No hay puertas que cerrar (l.198-225 solo corren con `CurrentStage>=0`).
   - Activa la escena (l.237), la marca como activa (l.246), busca `CampaignRoom` entre las raíces (l.241-244), fija `CurrentRoom` (l.251), hace `WarmRoom` (l.255) y espera `Ready` y el mínimo de preparación `minimumPreparationSeconds` (l.256; en prólogo el mínimo es 0).
   - `CurrentStage=target` (l.262). `target==0` llama a `AirlockDoorMotion.Set(initialDoor,false)` (l.266), que en la campaña es no-op porque `initialDoor` es null (RequiemCampaign.unity:51163, `initialDoor: {fileID: 0}`).
   - Muestra el texto (l.273), libera `startCover` si no es prólogo (l.274) y precarga la etapa siguiente (l.275).

**Bucle de sala**
4. `Update` (l.93-97): si no hay `Busy`, hay sala, el menú está jugando y el jugador vive, y `CurrentRoom.Completed` es true, llama a `AirlockDoorMotion.Set(SourceDoor, false)` (l.96). Es idempotente: la puerta de salida se abre en cuanto la sala termina.

**Transición**
5. `CampaignAirlock.Contact` (CampaignAirlock.cs:28): si el collider pertenece al `PlayerCharacterController` de `flow.Player`, guarda `lastContact` y llama a `flow.TryTransition(sourceStage)`.
6. `TryTransition` (l.98-102): exige `!Busy`, `source==CurrentStage`, `CurrentRoom.Completed`, menú jugando, jugador vivo y `timeScale>0`. Después lanza `Prepare(source+1)`.
7. `Prepare(target)` con `target = CurrentStage+1`:
   - a. Cierra `SourceDoor` (l.202) y llama a `AirlockCycle.Begin()` si existe (l.203). Esto ocurre dentro de la esclusa, detrás del jugador.
   - b. Espera en bucle (l.206) mientras **no** se cumpla todo esto: `IsClosed(SourceDoor)`, `PlayerInside` (contacto en los últimos 0,25 s), carga `progress>=0.9` y `Time.time-closing >= environmentBlendSeconds` (1,6 s). Durante la espera funde el post y el audio de la sala saliente (l.209).
   - c. **Abort** (l.211-216): si la puerta está cerrada y el jugador lleva más de 1 s fuera del trigger, reabre la puerta, llama a `Cycle.Abort()`, espera a que termine la carga en curso, descarga esa escena y pone `Busy=false`.
   - d. Activa la escena precargada (l.237) y la marca como activa (l.246, con el entorno de la nueva sala como destino de la mezcla).
   - e. Descarga la sala vieja (l.249): `Teardown` desactiva raíces y destruye por tandas de `TeardownBudget`=120 transforms por frame (l.136-162) y después `UnloadSceneAsync`.
   - f. `WarmRoom` (l.163-175): 6 renders a 320×180 desde una cámara oculta, con la cámara del jugador como plantilla.
   - g. Espera `CampaignRoom.Ready` (`initialized && queue.PrewarmPending==0`, CampaignRoom.cs:24) y, si no es prólogo, 1,5 s desde la descarga (l.256).
   - h. Abre `DestinationDoor` y todas las `ExtraDestinationDoors` (l.266-270) y llama a `Cycle.Complete()` (l.271). Muestra el título (l.273) y precarga la siguiente etapa (l.275).
   - Orden alternativo (`preloadNextStage=false`, l.230-235, "v2"): descarga la vieja antes de cargar la nueva.

**Fin de partida**
8. La última etapa no tiene esclusa. `ChaseCorridorEncounter.Update` pone `Completed=true` al llegar a `finishZ` (ChaseCorridorEncounter.cs:84-85) y muestra "PERSECUCIÓN COMPLETADA · ZONA SEGURA". No hay evento ni carga de `WinScene`: `GameFlowManager.WinSceneName` (`Assets/FPS/Scripts/Game/Managers/GameFlowManager.cs:15`) solo aparece en herramientas y benchmarks (grep sin usos en `Levels/`). **Cierre de campaña: no existe** (map-structure.md 1.3 lo confirma por lectura).

**Muerte y reinicio**
9. `CombatLabMenu.LateUpdate` (l.34): con vida ≤0 activa `deathPanel` y pausa. "Reiniciar" llama a `RestartFromFirstRoom` (l.38) → `Reload(true)` (l.40) → `SceneManager.LoadScene(gameObject.scene.name, Single)`. **La campaña entera se recarga y vuelve a la etapa 0.** No hay checkpoints entre salas. Los tests lo confirman (CampaignTests.cs:263 y :345).

**Dónde vive el jugador y qué pasa con el cañón**
10. El jugador vive en la escena de sesión (CampaignAuthoring.cs:218: `All<PlayerCharacterController>(scene).Single()`). `CampaignFlow` no lo teletransporta al cambiar de sala (grep sin resultados en CampaignFlow.cs). Por eso **la llegada es la posición que el jugador tiene al cruzar la puerta destino, y las salas deben compartir coordenadas de mundo con la sesión**.
11. El cañón robado (`StolenWeaponCarrier`) se reubica a la escena de sesión antes de descargar la vieja (CampaignFlow.cs:227-228; `CampaignFlow.PlaceWorldModule` en `StolenWeaponCarrier.cs:179`).

**Precalentamiento de enemigos**
12. `CombatTwoRoomsLab.Start` (l.56-58): elige la composición de cada ronda y llama `EnemySpawnQueue.Prewarm(prefab, count)` por cada grupo. `EnemySpawnQueue.Update` (l.52-61) instancia **un** enemigo inactivo por frame bajo la carpeta `EnemySpawnQueue_Prewarmed`. `CampaignRoom.Ready` bloquea la apertura de la puerta de llegada hasta que `PrewarmPending==0`.
13. `ChaseCorridorEncounter.Start` (~l.64-66): precalienta un enemigo por prefab de emboscada.

---

### C3 Ficha por sistema

Formato: campo `[SerializeField]` o público (tipo, valor por defecto, línea). Las propiedades sin `[SerializeField]` se asignan solo por código.

### CampaignFlow (`Levels/CampaignFlow.cs`)
- Serializados: `player` PlayerCharacterController (null, l.12); `menu` CombatLabMenu (l.13); `message` TMP_Text (l.14); `stageScenes` string[] (l.15, **nombres de escena sin ruta**, deben estar en Build Settings); `airlocks` CampaignAirlock[] (l.16); `initialDoor` GameObject (null, l.17); `minimumPreparationSeconds` float 1.5, min 0 (l.18); `prologueStages` int 0, min 0 (l.20); `startCover` CanvasGroup (l.22); `startupVariants` ShaderVariantCollection (l.23); `environmentBlendSeconds` float 1.6, min 0 (l.108); `preloadNextStage` bool true (l.112).
- Estáticos (no serializados): `TeardownBudget` int 120 (l.136); `SkipWarm` bool false (l.135); `Owns(owner)` (l.29); `PlaceWorldModule(go)` (l.30-33).
- Propiedades: `Player`, `Menu`, `Message` (l.34-36), `CurrentRoom`, `CurrentStage` (-1), `Busy` (l.37-39), `StageScenes`, `Airlocks`, `LoadedStageCount` (l.40-42), `PrologueStages` (l.86), `Phase` (l.176), `LastTransitionPeakMs` (l.128), `LastReport` (`TransitionReport`, l.130-131), `TransitionSerial` (l.132).
- Métodos: `Configure(owner, lab, label, scenes, doors, first)` (l.91), `ConfigurePrologue(stages, cover)` (l.87), `ReleaseStartCover()` (l.89), `TryTransition(source)` (l.98), `DrainPendingLoadsForShutdown()` (l.58).
- Eventos C#: **ninguno**. El estado se consulta por propiedades.
- Texto: `Sector(t) = "SECTOR " + (t+1-prologueStages)` (l.90).
- Límites: una transición cada vez (`Busy`); precarga de una etapa; descarga en tandas; `Teardown` a 120 transforms/frame; mínimo de transición ≈ 0,45 s de cierre + carga ≥0,9 + 1,6 s de mezcla + 1,5 s de preparación (C5).

### CampaignRoom (`Levels/CampaignRoom.cs`)
- Serializados: `rounds` CombatTwoRoomsLab (null, l.9); `pursuit` ChaseCorridorEncounter (null, l.10); `queue` EnemySpawnQueue (null, l.11); `prologueTitle` string "" (l.13).
- Propiedades: `IsPrologue` = `!rounds && !pursuit` (l.15); `PrologueTitle` (l.16); `HasTitle` = título no vacío (l.18); `Completed` = `rounds?rounds.Completed : pursuit?pursuit.Completed : released` (l.23); `Ready` (l.24); `Rounds`, `Pursuit` (l.21-22).
- Métodos: `Release()` (l.20), `Configure(waves, chase, spawns)` (l.25), `ConfigureTitle(t)` (l.19), `ConfigurePrologue(t)` (l.26).
- `[DefaultExecutionOrder(-200)]` (l.6). `Awake` (l.27-34) busca `CampaignFlow`. Si no lo encuentra, desactiva sus componentes. Si lo encuentra, llama `rounds.BindSession(player, message)`, `pursuit.BindSession(player, menu, message)` y `GapFallRecovery.BindSession(player)` para cada grieta de la escena.
- Límite: **un único CampaignRoom por escena**; CampaignFlow toma el primero (l.243).

### CampaignAirlock (`Levels/CampaignAirlock.cs`, RequireComponent BoxCollider)
- Serializados: `flow` CampaignFlow (l.9); `sourceStage` int 0 (l.10); `sourceDoor`, `destinationDoor` GameObject (l.11); `extraDestinationDoors` GameObject[] vacío (l.13); `cycle` AirlockCycle (l.15).
- Propiedades: `SourceDoor`, `DestinationDoor`, `ExtraDestinationDoors`, `Cycle`, `SourceStage`, `PlayerInside` (= contacto en los últimos 0,25 s, l.23).
- Métodos: `Configure(session, index, entrance, exit)` (l.25), `ConfigureExtras(extras, cycle)` (l.24).
- Trigger: `OnTriggerEnter/Stay` (l.26-27) → `Contact` (l.28).
- Límite: `sourceStage` debe coincidir con el índice de `airlocks[]` (ver C6).

### AirlockDoorMotion (`VisualPass/AirlockDoorMotion.cs`, estática)
- Constantes: `OpenSeconds=0.7`, `CloseSeconds=0.45` (l.12).
- `Set(host, door, closed)` (l.17-30): registra la pose cerrada la primera vez (l.20); si ya está en ese estado no hace nada (l.23, l.27); si no puede correr corrutina, snap (l.28); si no, lanza `Run` en `host`.
- `IsClosed(door)` (l.33-38): true si `null`, o si está activa, sin corrutina y a <1 mm de la pose cerrada.
- `Run` (l.46-63): cerrar = activa desde el primer frame (l.51), `k²`; abrir = `smoothstep`, y al final `SetActive(false)` (l.60).
- `Height(door)` (l.40-44): `Renderer.bounds.size.y + 0.1` del **root**, o 4 m si no hay Renderer.
- Límite: sin Rigidbody (colisionador estático movido por transform), sin NavMeshObstacle, sin obstrucción (doors.md §4).

### AirlockCycle (`Levels/AirlockCycle.cs`)
- Serializados: `lights` Light[] (l.11); `lamps` Renderer[] (l.12); `steam` ParticleSystem[] (l.13); `audioSource` (l.14); `cycleClip`, `doneClip` (l.15); `amber` (1,.55,.12) y `green` (.25,1,.45) (l.16); `amberHdr` (2,1,.2) y `greenHdr` (.4,2,.7) (l.17); `lightIntensity` 2.5 y `blinkHz` 2.2, min 0 (l.18).
- API: `Begin()` (l.30), `Complete()` (l.38), `Abort()` (l.47), `Cycling`.
- No tiene estado rojo ni de bloqueo (doors.md §3).

### CombatTwoRoomsLab (`Levels/CombatTwoRoomsLab.cs`)
- Serializados: `player` (l.16); `waves` Wave[] (l.17); `firstRoomSpawns`, `secondRoomSpawns` Transform[] (l.18); `firstRoom` Bounds (0,3,0) 48×14×46 (l.19); `secondRoom` Bounds (0,3,58) 48×14×46 (l.19); `playerClearance` 6, min 1 (l.20); `gate` GameObject (l.21, null en campaña); `message` TMP_Text (l.22); `singleArena` bool false (l.24); `arenaTitle` "CUARTO" (l.25); `gatePreparationSeconds` 0 (l.26); `spawnQueue` (l.29); `variedSpawns` false (l.31); `scenarioSeed` 0 (l.33; 0 = aleatorio por partida); `minimumSpawnInterval` 0.2 (l.34); `maximumSpawnInterval` 0.4 (l.35); `separationPadding` 0.5 (l.36).
- Constante: `ReservationSeconds` 2.5 (l.39).
- Tipos anidados: `Group { Prefab, Count≥1 (def 1), FixedPoint }` (l.13); `Composition { Name, Groups }` (l.14); `Wave { Groups, Variations }` (l.15).
- API: `Completed`, `WaveIndex`, `LivingEnemies`, `PendingSpawns`, `Living`, `SpawnQueue`, `WaitingForSpawnSpace`, `CurrentComposition`, `RunSeed`, `UsesVariedSpawns` (l.42-49); `BindSession` (l.51); `ConfigureVariety(enabled, seed)` (l.44, lanza excepción si ya empezó); `Configure(p, seq, a, b, door, text)` (l.55); `UseSpawnQueue` (l.50); `ShowExitStatus` (l.52); `FirstRoomBounds`, `FirstRoomSpawns` (l.53-54).
- Eventos: ninguno propio. Se suscribe a `Health.OnDie` (l.133).

### EnemySpawnQueue (`Levels/EnemySpawnQueue.cs`)
- Serializados: `prewarmPerFrame` int 1, min 1 (l.16); `activationsPerFrame` int 1, min 1 (l.18).
- API: `Configure(prewarmFrame, activateFrame)` (l.30); `Prewarm(prefab, count)` (l.39); `Spawn(prefab, pos, rot, callback, resolvePos?, resolveRot?)` (l.41-42); `Pending` (l.27), `PrewarmPending` (l.28), `Ready(prefab)` (l.29); `CancelPending()` (l.93).
- Límites: 1 instancia precalentada por frame y 1 activación por frame en campaña (`Configure(1,1)` en CampaignAuthoring.cs:144 y :177). Sin tope de enemigos vivos. Mientras hay activaciones pendientes no precalienta (l.54). Una sola construcción por frame (l.73). **Si `resolvePosition` devuelve null, la cabeza de la cola espera** (l.82): bloquea las peticiones posteriores.

### ChaseCorridorEncounter (`Levels/ChaseCorridorEncounter.cs`, RequireComponent BoxCollider)
- Tipo `Ambush { ActivationZ float; Prefabs GameObject[]; Points Transform[]; Triggered (runtime) }` (l.16-21).
- Serializados: `player` (l.23), `menu` (l.24), `queue` (l.25), `entranceDoor`, `exitDoor` (l.26), `carriage` (l.27), `blades` Transform[] (l.28), `motor` (l.29), `message` (l.30), `ambushes` (l.31), `sawSpeed` 8 (l.32), `warningSeconds` 2 (l.33), `bladeRpm` 240 (l.34), `counterRotateBlades` false (l.35), `sawContactOffset` 0.8 (l.36), `sawDamage` 100, min 1 (l.37), `damageInterval` 0.35 (l.38), `finishZ` 184 (l.39; en Pursuit05 es 369 por `+185`, CampaignAuthoring.cs:218), `halfWidth` 8 (l.40), `courseOffset` Vector3 (l.41).
- API: `Running`, `Completed`, `SpawnedCount`, `Spawned`, `Ambushes`, `SawSpeed`, `SawCarriage`, `EntranceDoor`, `ExitDoor`, `Motor` (l.46-55); `EnterFromPreviousScene()` estática (l.57); `BindSession` (l.58); `Configure` (l.59).
- Reglas de emboscada (`ProcessAmbushes`, ~l.97-111): una zona se activa al pasar `ActivationZ`. Si el jugador ya supera `última z de la zona - 4`, se marca y se omite. Si avanza sobre esa línea mientras hay pendientes, se cancela la cola (`CancelPending`). Cada punto se valida en `SafePosition` (~l.113-123): debe estar ≥6 m por delante del jugador, sobre NavMesh (0,75 m) y sin cápsula sólida.
- Sierra: daño `sawDamage` cada `damageInterval` si `|x-courseOffset.x|<halfWidth` y la altura está en el rango (~l.91-92).

### GapFallRecovery (`Levels/GapFallRecovery.cs`)
- Serializados: `player` (l.10), `nearReturns`, `farReturns` Transform[] (l.11), `damage` 20 (l.12), `repeatDelay` 0.75 (l.13), `upperFloorHeight` 8, `gapNearZ` -2, `gapFarZ` 2 (l.14), `halfWidth` 26 (l.15).
- API: `TryRecover()` (l.43): elige el punto de retorno libre más cercano del lado en que se tomó impulso; llama `movement.ResetForCheckpoint()`, `player.TeleportToCheckpoint(...)` y `Health.TakeDamage(damage)`. `RecoveryCount`, `ReturnsToFarSide`, `Damage`, `Configure` (l.22), `BindSession` (l.24).
- **No es un checkpoint**. Es recuperación local con daño. (AdvancedPlayerMovement `ResetForCheckpoint`, `Assets/IronRequiem/Scripts/Player/AdvancedPlayerMovement.cs:94`.)

### CorridorAlarm (`Levels/CorridorAlarm.cs`)
- Serializados: `rounds` (l.11); `alarmLights` Light[] (l.12); `beacons` Transform[] (l.13); `beaconLights` (l.14); `beaconHeads` Renderer[] (l.15); `siren`, `klaxon` (l.16-17); `screens` TMP_Text[] (l.18); `alertText`, `clearText` (l.19); `alarmIntensity` 3,5, `beaconIntensity` 14, `beaconDegreesPerSecond` 240, `bannerSeconds` 2,8 (l.20).
- Dispara con `WaveIndex>=0` (l.93) y limpia con `Completed` (l.103). Lo usa solo Room00b.

### CombatLabMenu (`Levels/CombatLabMenu.cs`)
- Serializados: `player` (l.9), `mainPanel`, `deathPanel` (l.10), `pauseManager` (l.11), `directGameplayEntry` bool **false** (l.12).
- `IsCampaignEntry` (l.13) = `directGameplayEntry || scene.name=="RequiemCampaign"` (**nombre a fuego**).
- `Start` (l.33): `BeginGame` si `IsCampaignEntry || startAfterReload || fromMainMenu`; si no, `ShowMain` (l.36), que llama `BeginGame` directamente si no hay `mainPanel`.
- `BeginGame` (l.37) desreferencia `deathPanel` sin comprobar nulo: **`deathPanel` es obligatorio**.
- API: `IsPlaying` (l.17), `IsDeathMenu` (l.18), `IsMainMenu`, `LaunchedFromMainMenu`, `BeginGame`, `RestartFromFirstRoom`, `ReturnToMainMenu` (l.39, vuelve a `IntroMenu` por `MainMenuLaunch.MainMenuScene`, `Assets/IronRequiem/Bridges/Game/MainMenuLaunch.cs:8`).

### ZoneEnvironment (`Levels/ZoneEnvironment.cs`, estática)
- `Settings` y `Capture()`, `Apply(a, b, t)` (l.14-48): blend de niebla, ambiente y reflexión.
- `SceneFade` (l.53): `Bind(scene)` (l.73) reclama volúmenes globales y AudioSources en bucle; `Set(k)` (l.94); `Volumes`, `Sources`.
- Constantes específicas del hangar: `HangarScene = "Room00_Hangar"`, `HangarVolume = "HG_PostVolume"`, `MenuProfile = "HG_MenuVolume"` (l.63) y `Resources.Load("IronRequiem/HG_GameplayVolume")` (l.67). **No afectan a otras escenas**.

### VolumeFadeIn / SectorLightingBlend (`VisualPass/`)
- `VolumeFadeIn`: `seconds` 1,2 min 0,05 (l.13); `AuthoredWeight` (l.15); `Claim()` (l.18), que detiene su propio fade si lo reclama `SceneFade`.
- `SectorLightingBlend`: `seconds` 1,4 min 0,05 (l.13); escucha `SceneManager.activeSceneChanged` (l.17). `DisallowMultipleComponent`.

### WeaponMountStation (`Levels/WeaponMountStation.cs`)
- RequireComponent BoxCollider, trigger (l.77). Serializados: `weaponPrefab` (l.25), `handArm`/`cannonArm` StationArmRig (l.26), `cannonRack`, `lockRing` (l.27), `statusLamp` (l.28), colores (l.29), FX (l.30), `flashLight` (l.31), `sfx` (l.32), clips (l.33), `prompt` (l.34), `message` (l.35), `messageSeconds` 4 (l.36), `skipHoldSeconds` 0,6 (l.37).
- `Duration` = 5,6 s (l.47). `mountedThisRun` static (l.52), reset solo en `SubsystemRegistration` (l.53): **una vez por dominio**, no por reinicio de partida.
- `Configure(...)` (l.65) solo por código. Busca `CampaignFlow` (l.84) para el texto.

### CombatMusic (`Audio/CombatMusic.cs`)
- Serializados: `m_ExploreClip` (l.13), `m_CombatClip` (l.14) (sin ambos se desactiva, l.62-66); `m_OutputGroup` (l.15); `m_Volume` 0,8 (l.16); `m_FadeToCombatSeconds` 0,75 (l.17); `m_FadeToExploreSeconds` 3 (l.18); `m_CombatHoldSeconds` 6 (l.20); `m_PollInterval` 0,25 (l.21); `m_PlayWhilePaused` true (l.23); `m_LabMenu` (l.24).
- `Start` (l.60-75): `FindAnyObjectByType<EnemyManager>()` **una vez** (l.69). Sin `EnemyManager` en la escena, la música nunca pasa a combate.
- `EnemyManager` viene del prefab `Assets/FPS/Prefabs/GameManager.prefab` (grep). Que la instancia esté en RequiemCampaign: **NO VERIFICADO** (solo se confirmó la cadena de texto "GameManager" en el YAML).

### HealthPickup (`Assets/FPS/Scripts/Gameplay/HealthPickup.cs`)
- `HealAmount` float, por defecto 0 (l.9). `OnPicked` cura solo si `Health.CanPickup()` y destruye el objeto. Base: `Pickup` (`Assets/FPS/Scripts/Gameplay/Pickup.cs`, `PickupSfx` l.17, `PickupVfxPrefab` l.18, trigger l.35).
- Campaña: `Pickup_Health.prefab` con `HealAmount=25` solo en Room03/Room04 (CampaignAuthoring.cs:176). Room01/02 conservan los pickups del laboratorio que caen en su piso (CampaignAuthoring.cs:132).
- Las rondas **no sueltan vida** de enemigos cuyo loot contenga HealthPickup (CombatTwoRoomsLab.cs:132, `DropRate=0`).

### CombatTuningProfile (`Levels/CombatTuningProfile.cs`, ScriptableObject)
- `PlayerSpeed` 1,15; `EnemySpeed` 1,2; `EnemyDamage` 0,8; `EnemyHealth` 0,8; `StolenBigBotDamage` 1,2; `StolenBigBotEnergy` 1,2 (l.7-12; todos `Min(.1f)`).
- **Solo lo usa** `Editor/FastCombatAuthoring.cs:44` (lab). La campaña no lo referencia.

### ChaseSceneEntrance (`Levels/ChaseSceneEntrance.cs`)
- Serializados: `prerequisite` CombatTwoRoomsLab (l.10), `sealedDoor` (l.11), `destination` "ChaseCorridorLab" (l.12, **a fuego por defecto**), `minimumPreparationSeconds` 1,5 (l.13).
- Solo laboratorio. CampaignAuthoring.cs:201 lo destruye al construir la sesión.

### LabRoomVisibility (`Levels/LabRoomVisibility.cs`)
- Serializados: `player` (l.10), `stacked` (l.11), `boundary` 29 (l.12), `upperFloorEntryHeight` 6,8 (l.13). Usa "Sector03", "Stair", "Step" por nombre (l.60). Solo laboratorios.

---

### C4 Datos que configura un encuentro (rondas, spawns, fin de ronda, puertas)

### Esquema (lo que se rellena en la escena de sala)

```
Escena de sala  (una por etapa, p. ej. Map01_R03_Nombre.unity)
│
├─ CampaignRoom                          [1 por escena; CampaignFlow toma el primero]
│    rounds      : CombatTwoRoomsLab     o null
│    pursuit     : ChaseCorridorEncounter o null
│    queue       : EnemySpawnQueue       (obligatoria si hay rondas con enemigos)
│    prologueTitle : string               si no vacío: reemplaza el "SECTOR n" del HUD
│                                         y el texto de llegada (CampaignFlow.cs:273)
│
├─ CombatTwoRoomsLab
│    singleArena     : true               (campaña; ver C7 para más de 3 rondas)
│    arenaTitle      : string              "CUARTO n" -> HUD "<título> · RONDA i / N"
│    firstRoom       : Bounds              entrar aquí dispara la ronda 1 (una vez)
│    firstRoomSpawns : Transform[]         slots de las rondas 0..2 (índice < 3)
│    secondRoom      : Bounds              rondas con índice >= 3
│    secondRoomSpawns: Transform[]         slots de las rondas con índice >= 3
│    variedSpawns    : bool                 true = barajar y elegir variación
│    scenarioSeed    : int                  0 = aleatoria por partida; !=0 reproducible
│    playerClearance : 6 m (min 1)          distancia mínima jugador-spawn
│    minimumSpawnInterval / maximumSpawnInterval : 0.2 / 0.4 s
│    separationPadding : 0.5 m
│    spawnQueue      : EnemySpawnQueue      (obligatoria con variedSpawns)
│    waves           : Wave[]
│       Wave {
│         Groups     : Group[]             composición base (sin variación)
│         Variations : Composition[]       variaciones (elige una al azar por ronda)
│       }
│       Composition { Name : string, Groups : Group[] }
│       Group {
│         Prefab     : GameObject          con EnemyController y Health
│         Count      : int >= 1 (def 1)
│         FixedPoint : Transform           opcional: fuerza el slot de todas sus instancias
│       }
│
└─ EnemySpawnQueue
     prewarmPerFrame = 1, activationsPerFrame = 1   (campaña: Configure(1,1))
```

Otras claves: `Group.FixedPoint` solo se usa en el camino variado si está asignado (CombatTwoRoomsLab.cs:105). `Configure` (l.55) y `ConfigureVariety` (l.44) existen para asignar por código.

### Procedimiento de una ronda (camino variado, `singleArena`)
1. `Start` (l.56-58): por cada ronda elige `Variations[random]` si hay variaciones y `variedSpawns`; si no, usa `Groups`. Precalienta `Count` instancias de cada grupo (l.58).
2. `LateUpdate` (l.63): al entrar en `firstRoom`, `Spawn(0)`, una sola vez.
3. `SpawnVaried` (l.88-97): crea una lista con cada `Group`×`Count`, la **baraja** (l.92-93) y encola cada una con `spawnQueue.Spawn(...)`. La ronda pasa a `active`.
4. `ResolveSpawn` (l.99-116), en cada intento:
   - Respeta `nextSpawnAt` (intervalo) y `RetryAt` (0,25 s tras fallo).
   - Recorre los slots de la ronda en orden aleatorio, con tres pasadas: (1) libre y no usado recientemente, (2) no usado en la ronda, (3) cualquiera.
   - Si ninguno vale, muestra "OLEADA EN ESPERA · BUSCANDO ESPACIO SEGURO" (l.115) y reintenta.
5. `SafeSpawn` (l.117-128): NavMesh a ≤0,75 m; dentro de los bounds de la ronda; a ≥ `playerClearance + radio`; sin solapar reservas de 2,5 s; camino NavMesh completo al jugador (≤3 m de tolerancia); cápsula libre.
6. `Register` (l.129-134): añade el `Health` a `alive`, suscribe `OnDie`, fija `PatrolPath` a una ruta con el propio jugador para `EnemyMobile` (l.132), y pone `DropRate=0` si el loot lleva HealthPickup.

### Fin de ronda y fin de sala
- **Fin de ronda**: `alive` vacío (los `Health` destruidos se quitan, l.66) **y** `pendingSpawns==0`.
- Si quedan rondas: `Spawn(index+1)` en el mismo frame, sin tiempo de calma (l.66). Si no: `Completed=true` (l.66-67).
- **Fin de sala**: `CampaignRoom.Completed` = `rounds.Completed` (CampaignRoom.cs:23).
- **Apertura de la salida**: `CampaignFlow.Update` (l.96) abre la `SourceDoor` de la etapa actual cuando `Completed` es true.

### Qué no hay
- No hay temporizador entre rondas.
- No hay tope de enemigos vivos ni de spawns por ronda.
- No hay pesos en las variaciones (selección uniforme, l.58).
- No hay eventos C#. La ronda se consulta por propiedades (`WaveIndex`, `LivingEnemies`, `PendingSpawns`, `WaitingForSpawnSpace`).

### Ejemplo real (CampaignAuthoring.cs:155-157 y RequiemCampaign)
- `ConfigureRounds(node, waves, slots, bounds, index)`: `Configure(null, waves, slots, empty, null, null)`, `UseSpawnQueue(queue)`, `ConfigureVariety(true)`, `singleArena=true`, `arenaTitle="CUARTO n"`, `firstRoom=bounds`.
- Salas: 3 rondas cada una. Room01 usa `FirstWaves` del laboratorio original (CampaignAuthoring.cs:114-120). Room02 a Room04 usan rondas construidas en código (l.149 y l.178). Pursuit05 usa ChaseCorridorEncounter.

---

### C5 Puertas: contrato actual y enganche propuesto

### Contrato actual (código)
- **Abrir**: `AirlockDoorMotion.Set(flow, door, false)`. La puerta termina **inactiva** (`SetActive(false)`, AirlockDoorMotion.cs:60) y en su pose cerrada.
- **Cerrar**: `Set(flow, door, true)`. La puerta queda **activa y con colisión desde el primer frame** (l.51).
- **IsClosed(door)** (l.33-38): `true` si es null; o si activa, sin corrutina y a <1 mm de la pose cerrada.
- **Pose cerrada**: se captura en la **primera** llamada a `Set` (l.20). La puerta debe estar en su sitio en ese momento.
- **Movimiento**: solo vertical, `Height = Renderer.bounds.size.y + 0.1` del root (l.40-44), o 4 m si el root no tiene Renderer. Abrir 0,7 s smoothstep; cerrar 0,45 s `k²`.
- **Quién las llama**:
  - `CampaignFlow.Update` (l.96): abre la salida de la etapa actual.
  - `Prepare` (l.202): cierra la de origen.
  - `Prepare` (l.266-270): abre la de destino y todas las `ExtraDestinationDoors` a la vez.
  - La puerta de destino **no se vuelve a cerrar** en la campaña.
- **Ciclo**: `AirlockCycle.Begin()` al cerrar (l.203), `Abort()` en el abort (l.213), `Complete()` al abrir (l.271). Solo ConnectionChamber tiene ciclo (doors.md).
- **Contratos que comprueban los tests** (doors.md §5, no revisado aquí): `Tests/PlayMode/CampaignTests.cs:57,62,78,190-193`, `MapFxAudit.cs:387-394`, `CampaignTourBenchmark.cs:175`. Exigen `activeSelf==false` al estar abierta y un collider sólido activo desde el primer frame de cierre.

### Hechos que limitan el diseño
- Las puertas son **colliders estáticos movidos por transform** (sin Rigidbody, sin NavMeshObstacle). No empujan al CharacterController.
- No hay comprobación de obstrucción. Un jugador dentro del hueco puede quedar bajo la hoja.
- El Abort solo se evalúa con la puerta cerrada y el jugador fuera del trigger más de 1 s (CampaignFlow.cs:211).
- Mover el root de una puerta fuera de `AirlockDoorMotion` después del primer `Set` deja `IsClosed` sin volver a true (`closedWorld` es estático, AirlockDoorMotion.cs:13).
- Ni `AirlockCycle` ni ningún otro componente tiene evento de "bloqueada" o "alarma".

### Enganche propuesto (doors.md §5) — **NO IMPLEMENTADO**
- `grep "DoorPresentation" Assets` devuelve **0 resultados**. Es una propuesta.
- Propuesta: en `AirlockDoorMotion.Run`, si existe un componente `DoorPresentation` registrado, delegar la pose visual con el mismo `k` (`Evaluate(k, closing)`), y llamar a `OnBegin(closing)` y `OnEnd(closing)`. El root **no** se mueve (queda en `shut`), así `IsClosed` sigue siendo exacto. Si no hay presentación, se conserva el comportamiento actual.
- Restricción: `DoorPresentation` no puede ser **hijo** del root, porque el root se desactiva al abrirse. Debe ser un hermano siempre activo, o vivir en `CampaignAirlock`. Las hojas sí pueden ser hijas del root.
- Restricción: `Height()` necesita un Renderer en el root, o la presentación debe aportar el recorrido.
- Señales de estado derivables sin tocar CampaignFlow: observar `CurrentStage`, `CurrentRoom.Completed`, `Busy` e `IsClosed`, o añadir eventos en los puntos l.96, l.202, l.213, l.269-271.
- Para obstrucción: una comprobación de solape con el jugador antes de cerrar del todo cabe en `Run` sin cambiar el contrato, si la puerta sigue activa y con collider desde el primer frame.

---

### C6 Cadenas escritas a fuego y suposiciones que afectan a escenas nuevas `Map01_*`

Cada entrada indica si se puede resolver **solo con datos** o requiere código.

| # | Ruta:línea | Qué está fijo | Efecto en Map01 | Solo datos |
|---|---|---|---|---|
| 1 | `Levels/CombatLabMenu.cs:13` | `scene.name=="RequiemCampaign"` | Si la sesión no se llama así, sin `directGameplayEntry` no entra directamente. Con `mainPanel` vacío, `ShowMain` arranca igual (l.36) | **Sí**: poner `directGameplayEntry=true` en el CombatLabMenu de la sesión |
| 2 | `Assets/FPS/Scenes/IntroMenu.unity:3051` | `SceneName: RequiemCampaign` del LoadSceneButton | Entrada desde el menú principal | No: IntroMenu está en la lista protegida (guardian.md, sección 3). Probar en Editor abriendo la escena de sesión |
| 3 | `Levels/CampaignFlow.cs:15` y RequiemCampaign.unity:51148 | `stageScenes` = nombres sin ruta | Nombres exactos, presentes en Build Settings (`CanStreamedLevelBeLoaded`, l.121, l.185) | **Sí** |
| 4 | `Levels/CampaignFlow.cs:273` | `Sector(target)+" LISTO · TRES RONDAS"` | Texto fijo "TRES RONDAS" para toda sala sin título. Con otro número de rondas miente | **Parcial**: poner `prologueTitle` en cada sala (CampaignRoom.cs:18 lo prioriza). Eliminar el texto requiere código |
| 5 | `Levels/CampaignFlow.cs:273` | `target==stageScenes.Length-1` → "PERSECUCIÓN · ATRAVIESA LA PUERTA" | La última etapa muestra ese texto salvo que tenga título | **Sí**, con `prologueTitle` |
| 6 | `Levels/CampaignFlow.cs:90, 238, 256` | Numeración `SECTOR n` y "PREPARANDO SECTOR n…" | Con `prologueStages=0` la etapa 0 es SECTOR 1 | **Sí** |
| 7 | `Levels/CampaignFlow.cs:266-271` | El destino de la etapa `t` es `airlocks[t-1]` | `airlocks.Length` debe ser `stageScenes.Length-1`, y `airlocks[i].sourceStage == i` | **Sí** (y comprobar a mano) |
| 8 | `Levels/CampaignFlow.cs:243` | Primer `CampaignRoom` entre raíces | Una sola sala por escena | **Sí** |
| 9 | `Levels/CombatTwoRoomsLab.cs:76, 103, 121, 131` | `index<3` decide slots, bounds y LabRoomVisibility | Rondas 0-2 usan `firstRoom*`; **rondas ≥3 usan `secondRoom*`** | **Sí**, pero hay que rellenar `secondRoomSpawns` y `secondRoom` (ver C7) |
| 10 | `Levels/CombatTwoRoomsLab.cs:69-71` | Camino no `singleArena`: 3+3 rondas (`Spawn(3)`, puerta `gate`) | No usar `singleArena=false` | **Sí** (usar singleArena) |
| 11 | `Levels/CombatTwoRoomsLab.cs:56` | "ENTRA EN LA PRIMERA ARENA" en Start | Se ve un instante antes de la preparación | No hay problema de datos: CampaignFlow lo sobrescribe |
| 12 | `Levels/ChaseCorridorEncounter.cs:64` | "SECTOR 03 · ATRAVIESA LA PUERTA" en Start | Lo sobrescribe CampaignFlow tras la preparación | No |
| 13 | `Levels/ChaseCorridorEncounter.cs:85` | "PERSECUCIÓN COMPLETADA · ZONA SEGURA" | Texto fijo al terminar | No |
| 14 | `Levels/ZoneEnvironment.cs:63, 67` | `Room00_Hangar`, `HG_PostVolume`, `HG_MenuVolume`, `Resources` "IronRequiem/HG_GameplayVolume" | Solo se activa si la escena se llama `Room00_Hangar`. No afecta a Map01 | Evitar ese nombre |
| 15 | `Levels/LabRoomVisibility.cs:60` | "Sector03", "Stair", "Step" por nombre | Solo lab. En campaña no hay ocultación: sin culling por piso | No |
| 16 | `Levels/ChaseSceneEntrance.cs:12` | `"ChaseCorridorLab"` | Solo lab | No |
| 17 | `Levels/CampaignFlow.cs:136, 135` | `TeardownBudget=120`, `SkipWarm` estáticos | No serializados | No (código) |
| 18 | `Levels/CampaignFlow.cs:175` | 6 direcciones de precalentamiento visual | Fijo | No |
| 19 | `Levels/WeaponMountStation.cs:52` | `mountedThisRun` estático | Solo una vez por dominio; un reinicio no vuelve a montar | No (código) |
| 20 | `Audio/CombatMusic.cs:69` | `FindAnyObjectByType<EnemyManager>()` | Sin EnemyManager en sesión, nunca hay música de combate | **Sí**: incluir el prefab GameManager en la sesión (NO VERIFICADO en la escena actual) |
| 21 | `Editor/CampaignAuthoring.cs:22-24, 105, 107-108` | Rutas `Scenes/Campaign`, `Names` fijos, prefabs de enemigos, `build` lanza excepción si ya existe | Solo Editor. **No reejecutar** para Map01 | — |
| 22 | `Editor/HangarIntro/HangarTransitAuthoring.cs:208-244` | Cablea `stageScenes`, `airlocks`, `initialDoor=null` (protegido) | No tocar | — |
| 23 | `Diagnostics/CampaignTourBenchmark.cs:150` | `GetSceneByName("RequiemCampaign")` | Solo benchmark | No |
| 24 | `Tests/PlayMode/CampaignTests.cs` y otros | Escenas y nombres `RequiemCampaign` | Solo tests | No |

**Suposiciones duras (sin texto escrito en código)**
- **Etapas aditivas**: cada sala es una escena aditiva cargada con `LoadSceneAsync(name, Additive)` (CampaignFlow.cs:123, 191). Cada escena se descarga entera (`Teardown` + `UnloadSceneAsync`).
- **Esclusas y puertas viven en la escena de sesión**. `Teardown` destruye las raíces de la escena vieja (CampaignFlow.cs:139-162). Si una puerta o un trigger estuviera en la sala, desaparecería al salir. No hay comprobación en código.
- **El jugador y el CampaignFlow viven en la sesión**. `Prepare` usa `gameObject.scene` como `sessionScene` (l.182).
- **Coordenadas de mundo compartidas** (ver C2 punto 10 y map-structure.md 1.1). No hay teletransporte.
- **Una escena de sala = un NavMesh propio** (CampaignAuthoring.cs:58-63, `Bake` con `NavMeshSurface`). Los enemigos no cruzan entre salas.
- **Build Settings**: los nombres se resuelven con `CanStreamedLevelBeLoaded`. Sin entrada, la transición termina en "ERROR · FASE NO DISPONIBLE" y deja `Busy` activo (CampaignFlow.cs:185; doors.md §4.2: jugador atrapado).

---

### C7 Riesgos para el mapa nuevo

1. **Más de 3 rondas por sala en `singleArena` (alto).** Las rondas con índice ≥3 usan `secondRoomSpawns` y `secondRoom` (CombatTwoRoomsLab.cs:76, :103, :121). Si `secondRoomSpawns` está vacío, `ResolveSpawn` no encuentra slot y reintenta cada 0,25 s con "OLEADA EN ESPERA" (l.115). **Por lectura de código no hay salida: soft-lock.** NO VERIFICADO en Play. Mitigación con datos: rellenar `secondRoomSpawns` y `secondRoom`. Mitigación alternativa: encadenar salas en lugar de rondas.
2. **Texto "TRES RONDAS" fijo (medio).** Toda sala sin `prologueTitle` dice "TRES RONDAS" (CampaignFlow.cs:273). Requiere código para ser genérico.
3. **Muerte reinicia toda la campaña (alto, decisión de diseño).** `RestartFromFirstRoom` recarga la sesión y vuelve a la etapa 0 (CombatLabMenu.cs:38-40). Con 15+ salas, cada muerte repite todo. Para checkpoints por sala hace falta código nuevo (cargar la etapa actual sin recargar la sesión, y reponer el estado del jugador).
4. **Sin cierre de campaña (alto).** No hay evento de fin ni carga de WinScene (C2 punto 8). El boss de Map01 necesita un componente nuevo de fin (idea M13 de map-structure.md).
5. **Boss arena: no existe soporte (alto).** `grep -i boss` en `Assets/IronRequiem/Scripts` solo devuelve `IndustrialPanelGraphic.cs` (nombre de clase de UI, no relacionado). No hay barra de vida de jefe, fases ni evento de jefe. Con datos puede usarse una ronda con un prefab grande (p. ej. BigBot) como encuentro, pero sin fases ni HUD propios.
6. **Precarga y memoria (medio).** Todos los enemigos de todas las rondas de una sala se instancian desactivados al empezar (CombatTwoRoomsLab.cs:58, EnemySpawnQueue.cs:54-60). `EnemySpawnQueue` nunca recicla. Room04 tiene 27 enemigos en la ronda 3 según map-structure.md 2.2 (NO VERIFICADO aquí). Medir antes de dimensionar.
7. **Tiempo por transición (medio).** Mínimo ≈ cierre 0,45 s + espera de carga ≥0,9 + mezcla 1,6 s + preparación 1,5 s (no prólogo) + activación/descarga en tandas. Con 15 transiciones son ≥ 3 s cada una. Medirlo con `LastReport` (CampaignFlow.cs:131) antes de fijar el ritmo.
8. **Precarga con una sola etapa (bajo).** `PreloadFollowingStage` carga solo la siguiente. Dos salas a la vez en memoria durante la transición.
9. **Puertas sin obstrucción ni presentación (medio).** Ver C5. Una hoja puede caer sobre el jugador en el hueco. No hay sonido ni estado de bloqueo en puertas 1-5.
10. **Desbordamiento de índices (medio).** `airlocks[t-1]` (CampaignFlow.cs:269) y `airlocks[CurrentStage]` (l.96, protegido por `<airlocks.Length`). Un `airlocks` corto rompe la transición final. Comprobar con una lista explícita.
11. **Nombres de escena copiados (bajo).** `Map01_R<NN>_<Nombre>` sin espacios ni caracteres especiales, y con el mismo nombre en Build Settings y en `stageScenes`.
12. **Protegido (alto).** Están en la lista "nadie toca" de guardian.md: `IntroMenu.unity`, `Room00_Hangar.unity` y los materiales `HG_*` y `VO_Env_RustMetal`. `HangarTransitAuthoring.cs` (cablea el hangar) también. `ConnectionChamber` y su AirlockCycle pertenecen a esa zona (doors.md §5).
13. **Sesión modificada (alto).** `RequiemCampaign.unity` y `CampaignFlow.cs` figuran modificados en git (doors.md, aviso de estado). Map01 debe nacer en una escena nueva; no guardar sobre `RequiemCampaign.unity`.
14. **Regenerar (alto).** Volver a ejecutar `CampaignAuthoring` (op `build`), `HangarTransitAuthoring.IntegrateCampaign` o `VisualPassF2 D1` borra o recoloca puertas y umbrales (doors.md §5).
15. **Lab-only sin efecto (bajo).** `LabRoomVisibility`, `ChaseSceneEntrance` y `CombatTuningProfile` no se usan en campaña. No contar con ellos (culling por piso, paso lab→persecución, multiplicadores globales).
16. **Entorno y volúmenes (bajo).** `ZoneEnvironment.SceneFade` reclama los `VolumeFadeIn` de la sala. Sin volúmenes globales no hay blend de post, pero la transición sigue funcionando.
17. **Armas recogidas (bajo).** `WeaponMountStation` se monta una vez por dominio; si Map01 tiene una estación, debe ser la única (`mountedThisRun`).

---

### Respuesta a la pregunta central: ¿puede cargar `Map01_R<NN>_<Nombre>` sin cambiar código?

**Sí, con datos**, salvo cuatro puntos que requieren decisión o código:

- **Datos necesarios** en `Map01_Campaign.unity`:
  - `CampaignFlow` (objeto `CampaignSession`): `player`, `menu` (CombatLabMenu con `deathPanel` y `directGameplayEntry=true`), `message` (TMP_Text), `stageScenes` = lista de nombres exactos, `airlocks` = un `CampaignAirlock` por transición (`sourceStage` = índice, `sourceDoor`, `destinationDoor`, `extraDestinationDoors`), `initialDoor` = null, `prologueStages` (0 o más), `minimumPreparationSeconds`, `environmentBlendSeconds`, `preloadNextStage`.
  - Un `CampaignRoom` en cada escena de sala, con `rounds` (CombatTwoRoomsLab y EnemySpawnQueue) o `pursuit`, y `prologueTitle` en cada sala para evitar los textos fijos (C6 #4 y #5).
  - En cada CombatTwoRoomsLab: `singleArena=true`, `firstRoom`, `firstRoomSpawns`, `secondRoom`, `secondRoomSpawns` (si hay más de 3 rondas), `waves`, `variedSpawns`, `spawnQueue`.
  - Build Settings: todas las escenas de sala y la de sesión, habilitadas, con nombre exacto.
- **Cuatro puntos que no son solo datos**:
  1. Texto "TRES RONDAS" fijo (CampaignFlow.cs:273) si una sala no tiene título.
  2. Reinicio que recarga la campaña entera (CombatLabMenu.cs:38-40), sin checkpoints.
  3. Sin evento de fin de campaña (C2 punto 8).
  4. Sin soporte de jefe (C7 punto 5).
- **Entrada desde menú**: IntroMenu.unity:3051 apunta a RequiemCampaign. Para probar, abrir la escena de sesión en el Editor. Cambiar el menú toca una escena protegida.

---

### Skills aplicadas y límites de esta tarea

- **`iron-requiem-fps`** (leída): se usó para ubicar el movimiento del jugador y el uso de `TeleportToCheckpoint` y `ResetForCheckpoint` de GapFallRecovery. No se modificó nada de movimiento.
- **`iron-requiem-performance`** (leída): se usó para señalar las perillas de rendimiento de la campaña (prewarm 1/frame, activación 1/frame, `TeardownBudget` 120, `WarmRoom` 6 renders, `WarmGlyphs`) y para marcar lo que requiere medición. **No se midió nada**: sin Profiler ni Play.
- Tarea de solo lectura. Único archivo escrito: este.


---

<!-- Anexo D_metricas_jugador.md -->

## Anexo · D - Métricas reales del jugador y de los agentes (campaña actual)

Raíz del proyecto: `C:\Users\gehbd\ProyectoGameFinal`. Rutas relativas a esa raíz. Solo lectura: no se abrió Unity ni se escribió nada salvo este archivo.

### D1 Prefab del jugador en campaña y cadena de variantes

Escena: `Assets/IronRequiem/Scenes/RequiemCampaign.unity`. Instancia del jugador: bloque `PrefabInstance &922907303` (línea 20758), `m_SourcePrefab` guid `a1f836fee3b45174c93971440a5a9c6d`, nombre `Player_ModularArm_v07`.

Cadena verificada (cada salto leído en la línea raíz `PrefabInstance` de cada archivo):

| Nivel | Prefab | Raíz PrefabInstance (línea) | Base |
|---|---|---|---|
| 0 | `Assets/IronRequiem/Prefabs/Player/Player_ModularArm_v07.prefab` | 267 | Player_NativeVisual_v06 |
| 1 | `Assets/IronRequiem/Prefabs/Player/Player_NativeVisual_v06.prefab` | 3 | Player_Integrated_v04 |
| 2 | `Assets/IronRequiem/Prefabs/Player/Player_Integrated_v04.prefab` | 3 | Player_PrimaryCapsules_v03 |
| 3 | `Assets/IronRequiem/Prefabs/Player/Player_PrimaryCapsules_v03.prefab` | 3 | Player_PrimaryModule_v02 |
| 4 | `Assets/IronRequiem/Prefabs/Player/Player_PrimaryModule_v02.prefab` | 3 | Player_CombatPolish_v01 |
| 5 | `Assets/IronRequiem/Prefabs/Player/Player_CombatPolish_v01.prefab` | 3 | Player_IR_Protagonist_Actions_v02 |
| 6 | `Assets/IronRequiem/Prefabs/Player/Player_IR_Protagonist_Actions_v02.prefab` | 9737 | Player_IR_Protagonist_v01 |
| 7 | `Assets/IronRequiem/Prefabs/Player/Player_IR_Protagonist_v01.prefab` | 543 | Player_IR_v01 |
| 8 | `Assets/IronRequiem/Prefabs/Player/Player_IR_v01.prefab` | 4913 | Assets/FPS/Prefabs/Player.prefab |
| 9 (base) | `Assets/FPS/Prefabs/Player.prefab` | sin raíz de variante | - |

Nota de método: el nivel 0 contiene una instancia anidada de Player_NativeVisual_v06 (línea 267) que es la raíz del archivo. Ningún nivel de la cadena aporta overrides de movimiento: se revisaron los `propertyPath` de los 10 archivos y solo aparecen transformaciones (`m_LocalPosition/Rotation`, `m_Layer`, `m_Name`), referencias de armas (`pistolPrefab`, `StartingWeapons`, `rightArmReceiverViews`), `m_Enabled`, `m_CastShadows` y similares. En `Player_IR_v01.prefab` los únicos overrides de componentes de jugador son los valores de `AdvancedPlayerMovement` (5079-5085) y `StolenWeaponCarrier` (5098-5099), que coinciden con los valores por defecto del código.

Conclusión D1: los valores efectivos de movimiento de la campaña son los serializados en `Assets/FPS/Prefabs/Player.prefab` más los de `AdvancedPlayerMovement` y `StolenWeaponCarrier` de `Player_IR_v01.prefab`. Los prefabs `FastCombat` (14,95 m/s) no forman parte de esta cadena.

### D2 Tabla de parámetros

Convención: "código" = valor por defecto en el `.cs`; "override" = valor serializado en el prefab de la cadena (fuente de verdad en runtime, porque un valor serializado sustituye al de código).

### D2.a PlayerCharacterController (`Assets/FPS/Scripts/Gameplay/PlayerCharacterController.cs`)

| Parámetro | Valor efectivo | Por defecto en código | Override (dónde) | Fuente |
|---|---|---|---|---|
| GravityDownForce | 20 m/s² | 20 (PCC:17) | sí, igual (`Player.prefab:431`) | override = código |
| GroundCheckDistance | **1** m | 0,05 (PCC:23) | **sí** (`Player.prefab:435`) | override distinto |
| MaxSpeedOnGround | **13** m/s | 10 (PCC:26) | **sí** (`Player.prefab:436`) | override distinto |
| MovementSharpnessOnGround | 15 | 15 (PCC:30) | sí, igual (`Player.prefab:437`) | igual |
| MaxSpeedCrouchedRatio | 0,5 | 0,5 (PCC:33) | sí, igual (`Player.prefab:438`) | igual |
| MaxSpeedInAir | 10 m/s | 10 (PCC:36) | sí, igual (`Player.prefab:439`) | igual |
| AccelerationSpeedInAir | 25 m/s² | 25 (PCC:39) | sí, igual (`Player.prefab:440`) | igual |
| SprintSpeedModifier | **1,5** | 2 (PCC:42) | **sí** (`Player.prefab:441`) | override distinto |
| KillHeight | -50 m | -50 (PCC:45) | sí, igual (`Player.prefab:442`) | igual |
| RotationSpeed | 200 | 200 (PCC:48) | sí, igual (`Player.prefab:443`) | igual |
| JumpForce | 9 m/s | 9 (PCC:54) | sí, igual (`Player.prefab:445`) | igual |
| CameraHeightRatio | **0,8** | 0,9 (PCC:57) | **sí** (`Player.prefab:446`) | override distinto |
| CapsuleHeightStanding | 1,8 m | 1,8 (PCC:60) | sí, igual (`Player.prefab:447`) | igual |
| CapsuleHeightCrouching | 0,9 m | 0,9 (PCC:63) | sí, igual (`Player.prefab:448`) | igual |
| CrouchingSharpness | 15 | 10 (PCC:66) | **sí** (`Player.prefab:449`) | override distinto |
| RecievesFallDamage | sí (1) | false (PCC:85) | **sí** (`Player.prefab:456`) | override distinto |
| MinSpeedForFallDamage | 20 m/s | 10 (PCC:88) | **sí** (`Player.prefab:457`) | override distinto |
| MaxSpeedForFallDamage | 40 m/s | 30 (PCC:91) | **sí** (`Player.prefab:458`) | override distinto |
| FallDamageAtMinSpeed | 10 | 10 (PCC:94) | sí, igual (`Player.prefab:459`) | igual |
| FallDamageAtMaxSpeed | 30 | 50 (PCC:97) | **sí** (`Player.prefab:460`) | override distinto |

Verificado en código: la velocidad de sprint es `MaxSpeedOnGround * SprintSpeedModifier` (PCC:335 y 326); en aire el tope horizontal es `MaxSpeedInAir * speedModifier` (PCC:392), es decir el sprint también eleva el tope aéreo; la gravedad se aplica como `CharacterVelocity += Vector3.down * GravityDownForce * dt` (PCC:396); el salto fija `CharacterVelocity.y = JumpForce` (PCC:356).

### D2.b CharacterController (`Assets/FPS/Prefabs/Player.prefab`, bloque ~l.533-549)

| Parámetro | Valor efectivo | Por defecto en código | Override | Fuente |
|---|---|---|---|---|
| m_Height | 1,8 m (cambia en runtime a 0,9 agachado, PCC:448/475) | no aplica | valor en prefab | `Player.prefab:543` |
| m_Radius | 0,35 m | no aplica | valor en prefab | `Player.prefab:544` |
| m_SlopeLimit | 45° | no aplica | valor en prefab | `Player.prefab:545` |
| m_StepOffset | 0,3 m | no aplica | valor en prefab | `Player.prefab:546` |
| m_SkinWidth | 0,001 m | no aplica | valor en prefab | `Player.prefab:547` |
| m_Center | (0,1,0) inicial; el código lo fija a up*height*0,5 (PCC:452/462) | no aplica | | `Player.prefab:549` |

### D2.c AdvancedPlayerMovement (`Assets/IronRequiem/Scripts/Player/AdvancedPlayerMovement.cs`), componente en `Player_IR_v01.prefab`

| Parámetro | Valor efectivo | Por defecto en código | Override | Fuente |
|---|---|---|---|---|
| dashSpeed | 24 m/s | 24 (cs:10) | sí, igual (`Player_IR_v01.prefab:5079`) | igual |
| dashDuration | 0,18 s | 0,18 (cs:11) | sí, igual (:5080) | igual |
| slideDuration | 0,7 s | 0,7 (cs:13) | sí, igual (:5081) | igual |
| slideMinimumSpeed | 11 m/s | 11 (cs:14) | sí, igual (:5082) | igual |
| slideImpulse | 4 m/s | 4 (cs:15) | sí, igual (:5083) | igual |
| slideDeceleration | 18 m/s² | 18 (cs:16) | sí, igual (:5084) | igual |
| slideMaximumSpeed | 28 m/s | 28 (cs:17) | sí, igual (:5085) | igual |
| slideCooldown | 0 s | 0 (cs:20) | no | código; campaña = 0 |
| slideRequiresRelease | no | false (cs:22) | no | código |
| slideInvulnerability | 0,42 s | 0,42 (cs:25) | no | código (invulnerable a daño durante el slide) |
| slideLookAngle / slideMaxPitch | 80° / 38° | 80 (cs:27) / 38 (cs:29) | no | código |
| Cargas de dash | 1 por vuelo (`DashAvailable` se repone al aterrizar, cs:158) | true inicial (cs:49) | no | código |

Nota: el cooldown de slide de 1,5 s citado en `Docs/IronRequiem/CHECKLIST_TRABAJO.md:464` NO está en la campaña: el componente de `Player_IR_v01` no lo serializa y no encontré llamadas a `ConfigureSlideCooldown` fuera de su definición. Valor efectivo en campaña: 0 s.

### D2.d Armas, interacción, ejecución y golpe

| Parámetro | Valor efectivo | Por defecto en código | Override | Fuente |
|---|---|---|---|---|
| StolenWeaponCarrier.interactionRange | 2,5 m | 2,5 (cs:12) | sí, igual (`Player_IR_v01.prefab:5098`) | igual |
| StolenWeaponCarrier.throwSpeed | 14 m/s | 14 (cs:13) | sí, igual (:5099) | igual |
| StolenWeaponCarrier.shoveDelay | 0,26 s | 0,26 (cs:15) | no | código |
| EnemyExecutionSource.healthThreshold | 0,75 (umbral de ejecución) | 0,75 (EnemyExecutionSource.cs:11) | no | código; no es distancia |
| ProtagonistMelee.reach (golpe F ligero) | 1,55 m | 1,55 (ProtagonistMelee.cs:10) | sí, igual (`Player_IR_Protagonist_Actions_v02.prefab:9924`) | igual |
| ProtagonistMelee.radius (golpe F ligero) | 0,18 m | 0,18 (cs:11) | sí, igual (:9925) | igual |
| ProtagonistMelee.heavyReach (carga) | 2,1 m | 2,1 (cs:36) | no | código |
| ProtagonistMelee.heavyRadius (carga) | 0,32 m | 0,32 (cs:37) | no | código |
| Pausa de la carga máx. (maxChargeSeconds) | 1,2 s | 1,2 (cs:32) | no | código |

Distancia de ejecución "E": `CHECKLIST_TRABAJO.md:431` declara "proximidad de 2,5 m a la superficie", que coincide con interactionRange. NO VERIFICADO en el código de ejecución (no leí la lógica de comprobación de distancia de `StolenWeaponCarrier`, solo sus campos).

### D2.e Pantalla y altura de ojos

- Altura de ojos de pie: 1,8 × 0,8 = **1,44 m** (PCC:446-447 y :453 `PlayerCamera.localPosition = up * height * CameraHeightRatio`).
- Altura de ojos agachado: 0,9 × 0,8 = **0,72 m**.
- Con el valor por defecto del código (0,9): 1,62 m y 0,81 m (no aplica en campaña).

### D2.f Vida y caída

- MaxHealth del jugador: 100 (`Player.prefab:473`). NO VERIFICADO el uso de la vida en campaña más allá de esta línea.
- Daño por caída: `CHECKLIST_TRABAJO.md:586` documenta "GapFallRecovery:20" (es daño de un sistema de recuperación de grieta, no la caída normal de PCC).

### D3 NavMesh: agent types y ajustes de bake

### D3.a Agent types del proyecto (`ProjectSettings/NavMeshAreas.asset`)

Único agent type definido: `agentTypeID: 0` ("Humanoid", `m_SettingNames`).

| Campo | Valor | Línea |
|---|---|---|
| agentRadius | 0,5 m | 76 |
| agentHeight | 2,0 m | 77 |
| agentSlope | 45° | 78 |
| agentClimb (escalón) | **1,0 m** | 79 |
| ledgeDropHeight | 0 | 80 |
| cellSize | 0,1667 m | (bloque m_Settings) |
| tileSize | 256 | (bloque m_Settings) |
| minRegionArea | 2 | (bloque m_Settings) |

### D3.b Ajustes de bake de la escena de sala `Assets/IronRequiem/Scenes/Campaign/Room01_Foundry.unity`

Bloque `NavMeshSettings` (`--- !u!196 &4`), serialización de `m_BuildSettings`:

| Campo | Valor | Línea |
|---|---|---|
| agentTypeID | 0 | 104 |
| agentRadius | 0,5 | 105 |
| agentHeight | 2 | 106 |
| agentSlope | 45 | 107 |
| agentClimb | **0,4** | 108 |
| cellSize | 0,16666667 | 113 |
| tileSize | 256 | 115 |
| minRegionArea | 2 | (bloque) |

Discrepancia: `agentClimb` 1,0 en ProjectSettings frente a 0,4 en la escena. NO VERIFICADO cuál se usa al hornear con AI Navigation 2.0.14 (`Packages/manifest.json`, línea 5). Si el bake usa 0,4, el escalón automático es menor que el del proyecto.

Componente `NavMeshSurface` (`Room01_Foundry.unity`, bloque ~l.17208): `m_AgentTypeID 0` (17210); `m_CollectObjects 2` (17211) - significado del enum NO VERIFICADO (no localicé la fuente del paquete; la búsqueda en Library fue demasiado lenta y se canceló); `m_Size` 10×10×10 y `m_Center` (0,2,0) (17212-17213); `m_UseGeometry 1` (17217); `m_VoxelSize 0,16666667` (17225); `m_TileSize 256` (17223); `m_BuildHeightMesh 0` (17228); `m_IgnoreNavMeshAgent 1`, `m_IgnoreNavMeshObstacle 1`.

Nota: `RequiemCampaign.unity` no contiene componentes `NavMeshSurface` (grep: 0 coincidencias). Los hornean las escenas de sala.

### D3.c Agentes de enemigos (campos `NavMeshAgent` en prefabs)

| Prefab | m_Radius | m_Height | m_Speed | m_StoppingDistance | Fuente |
|---|---|---|---|---|---|
| BigBot_Combat_v01 | **2,25** | **3,05** | 1,4 m/s | 3 | `Assets/IronRequiem/Prefabs/Enemies/BigBot_Combat_v01.prefab:1404,1413,1405,1409` |
| Enemy_HoverBot | 0,5 | 2 | 3,5 | 0,2 | `Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab:228,237,229,233` |
| Enemy_Turret | 0,5 | 2 | 3,5 | 0 | `Assets/FPS/Prefabs/Enemies/Enemy_Turret.prefab:366,375,367,371` |

Contraste con docs: `CHECKLIST_TRABAJO.md:411` ("radio conservador de 2,25 m") y `:588` ("NavMesh propio, radio 2,25 m, altura 3,05 m, escalón 0,25 m"). En el 588 se registra que "entrada inferior demasiado estrecha para el diámetro del robot" y que se ampliaron rellano y giro. NO VERIFICADO dónde está el NavMesh "propio" de radio 2,25: en ProjectSettings solo existe el agent type 0 (radio 0,5). Por tanto el radio 2,25 del Big Bot es de su componente de agente, y el horneado no lo tiene en cuenta: los pasillos deben dimensionarse para su diámetro (4,5 m), como indica la experiencia del propio proyecto.

### D4 Derivados calculados

Gravedad g = 20 m/s² (D2.a). Salto v0 = 9 m/s. Todos los valores asumen integración continua; la integración discreta de Unity por frame difiere en fracciones de frame (NO VERIFICADO en Play Mode).

| # | Magnitud | Fórmula | Resultado |
|---|---|---|---|
| D4.1 | Altura de salto (apex sobre el suelo) | h = v0² / (2g) = 81 / 40 | **2,025 m** |
| D4.2 | Tiempo total en el aire (suelo a suelo) | t = 2·v0 / g = 18 / 20 | **0,90 s** |
| D4.3 | Tiempo hasta el apex | t_apex = v0 / g | 0,45 s |
| D4.4 | Velocidad horizontal en aire andando | min(v_suelo 13, tope aéreo 10) (PCC:392, aplicado desde el primer frame en aire) | **10 m/s** |
| D4.5 | Distancia de salto andando | 10 × 0,90 | **9,0 m** |
| D4.6 | Velocidad horizontal en aire esprintando | tope 10 × 1,5 | **15 m/s** (la velocidad de suelo 19,5 se recorta a 15) |
| D4.7 | Distancia de salto esprintando | 15 × 0,90 | **13,5 m** |
| D4.8 | Distancia del dash | 24 × 0,18 | **4,32 m** (la velocidad 24 no se limita por el tope aéreo: `AdvancedPlayerMovement.cs:218`) |
| D4.9 | Tiempo restante tras un dash en el apex | 0,90 - 0,45 - 0,18 | 0,27 s |
| D4.10 | Salto + dash en el apex, andando | 10×0,45 + 4,32 + 10×0,27 (tras EndDash la velocidad queda en 10: `:235` usa MaxSpeedInAir sin modificador) | **≈11,5 m** |
| D4.11 | Salto + dash en el apex, esprintando | 15×0,45 + 4,32 + [10→15 a 25 m/s² en 0,2 s = 2,5 m] + 15×0,07 = 6,75 + 4,32 + 2,5 + 1,05 | **≈14,6 m** |
| D4.12 | Dash durante el ascenso | `AdvancedPlayerMovement.cs:179` pone la velocidad vertical en min(0, vy): anula la subida | el dash en el inicio del salto pierde la altura |
| D4.13 | Velocidad de inicio del slide | min(28, v_suelo_sprint + 4) = min(28, 13×1,5 + 4) = min(28, 23,5) | **23,5 m/s** (requiere ≥ 11 m/s y sprint, `:188-192`) |
| D4.14 | Velocidad de corte por velocidad | 13 × 0,5 = 6,5 m/s; tiempo a 6,5 = (23,5-6,5)/18 | 0,94 s (no alcanzado: corta antes el tiempo de 0,7 s) |
| D4.15 | Distancia de slide a velocidad de sprint | 23,5×0,7 - ½·18·0,7² = 16,45 - 4,41 | **≈12,0 m** |
| D4.16 | Distancia de slide desde 11 m/s (mínimo) | v0 = 15; corte a 6,5 en t = 8,5/18 = 0,47 s; 15×0,47 - 9×0,47² | ≈5,1 m |
| D4.17 | Duración de invulnerabilidad del slide | código | 0,42 s |
| D4.18 | Altura de ojos de pie | 1,8 × 0,8 | **1,44 m** |
| D4.19 | Altura de ojos agachado / slide | 0,9 × 0,8 | **0,72 m** |
| D4.20 | Altura libre para estar de pie | altura de cápsula | 1,8 m (más skinWidth 0,001) |
| D4.21 | Altura libre para agacharse o deslizar | altura de cápsula agachada | 0,9 m |
| D4.22 | Ancho del jugador (diámetro de cápsula) | 2 × 0,35 | 0,70 m |
| D4.23 | Altura libre que bloquea el apex de salto de pie | 1,8 + 2,025 | un techo entre 1,8 y 3,8 m sobre el suelo impide el salto (cabeza a 3,8 m en el apex) |
| D4.24 | Ascenso máximo automático de escalón | m_StepOffset | 0,3 m |
| D4.25 | Pendiente caminable del jugador | m_SlopeLimit | 45° |
| D4.26 | Rampa de 19° para subir 1 m | 1 / tan(19°) | 2,9 m horizontales por metro |
| D4.27 | Longitud de rampa de 19° para 3,8 m | 3,8 / sin(19°) | ≈11,7 m |
| D4.28 | Caída sin daño | impacto v = √(2·g·h) = 20 m/s | h = 400 / 40 = **10 m** (daño 10 a 20 m/s, 30 a 40 m/s: h = 40 m) |
| D4.29 | Distancia de detección de suelo al estar en suelo | skinWidth + GroundCheckDistance = 0,001 + 1 | **1,001 m** (PCC:260-261) |
| D4.30 | Radio de cruce del Big Bot | diámetro = 2 × 2,25 | **4,5 m** de paso libre mínimo |

Notas sobre los derivados:
- D4.4 y D4.6: `PlayerCharacterController.cs:392` limita solo el componente horizontal en aire, con el modificador de sprint. No hay cap por tiempo.
- D4.10 y D4.11: son estimaciones de tramo a tramo. Asumen que el jugador entra en el aire a velocidad de suelo y que el dash se activa a la altura del apex. NO VERIFICADO en Play Mode.
- D4.13 y D4.15: asumen que la velocidad de suelo ya alcanzó 19,5 m/s al pulsar C (la rampa de aceleración usa `MovementSharpnessOnGround` 15). NO VERIFICADO.
- D4.29: `PlayerCharacterController.cs:259-261` usa 1,001 m al estar en suelo. Con este valor un borde de hasta ~1 m puede no soltar el estado "grounded". NO VERIFICADO el efecto exacto (no simulé el salto de borde; comprobar en Play Mode).
- D4.28: la lógica de daño usa `m_LatestImpactSpeed` y `fallSpeed` (PCC:216-221). La cifra 10 m es el umbral teórico por caída libre sin rozamiento; NO VERIFICADO con la caída real.

### D5 Reglas de construcción sugeridas

Todas las reglas están derivadas de los números anteriores. Marcadas [SUGERENCIA] porque son recomendaciones de diseño, no hechos del proyecto.

1. [SUGERENCIA] **Pasillo de jugador**: mínimo 1,0 m libres (cápsula 0,70 m + margen para strafe y esquiva). Pasillos de combate de 2,5 m o más, para que el slide (D4.15: ≈12 m de recorrido) y el strafe no se corten contra las paredes.
2. [SUGERENCIA] **Pasillo de Big Bot**: mínimo 4,5 m libres (D4.30). Entradas, giros y rellanos por donde pasen Big Bots: al menos 5 m de ancho y espacio para girar, como ya se corrigió en `CHECKLIST_TRABAJO.md:588`.
3. [SUGERENCIA] **Altura de techo de paso de pie**: al menos 2,0 m (margen sobre 1,8 m). Para zonas donde el jugador deba saltar, usar techos de 4,0 m o más: un techo entre 1,8 y 3,8 m bloquea el apex (D4.23).
4. [SUGERENCIA] **Pasos bajos**: un hueco de 0,9 a 1,8 m obliga a agacharse o deslizar (D4.21). Usarlo para pasos de "slide obligatorio" con holgura de 1,0 m.
5. [SUGERENCIA] **Sala de combate**: techos de 12-13 m, la dirección ya adoptada en `CHECKLIST_TRABAJO.md:1211` (la versión anterior era 7-8 m). Cualquier techo entre 1,8 y 3,8 m en zonas de salto debe evitarse (D4.23).
6. [SUGERENCIA] **Pasarelas**: como el jugador salta 2,025 m (D4.1), una pasarela a 3,8 m no se alcanza con un salto simple. Usar rampa de 19° (D4.27: ≈11,7 m) o escalón de 0,3 m o menos (D4.24). La rampa de 14° de `CHECKLIST_TRABAJO.md:1208` (V35_StairRamp) es más suave y cómoda: 3,8 m de desnivel exigen unos 16 m de rampa (3,8 / sin 14°).
7. [SUGERENCIA] **Huecos horizontales entre plataformas** a la misma altura: hasta 9,0 m andando (D4.5), hasta 13,5 m esprintando (D4.7), hasta ~14,6 m con dash en el apex (D4.11). Como umbral de diseño propongo un hueco "sin dash" ≤ 9 m (andando) o ≤ 12 m (sprint), y un hueco "con dash" de 12 a 14 m. Gaps mayores de 14,6 m no son franqueables.
8. [SUGERENCIA] **Coberturas**: a 0,9 a 1,0 m de altura cubren al jugador agachado (ojos a 0,72 m, D4.19) y dejan asomar al jugador de pie (ojos a 1,44 m, D4.18). Coberturas de 1,5 m o más cubren también al jugador de pie. Ajustar la altura según el tipo de duelo que se quiera.
9. [SUGERENCIA] **Escalones**: 0,2 m (los de `CHECKLIST_TRABAJO.md:1208`) están dentro del límite de 0,3 m del controlador (D4.24). Escalones de 0,3 m a 0,5 m necesitan rampa.
10. [SUGERENCIA] **Pozos y grietas**: caídas de hasta 10 m no hacen daño por caída (D4.28). Un pozo más profundo de 10 m es un riesgo real: 10 de daño a 20 m/s y 30 a 40 m/s. El sistema de recuperación de grieta de `CHECKLIST_TRABAJO.md:586` aplica 20 de daño por caída, así que una grieta de 4 m puede cruzarse por la recuperación sin pasar por caída normal.
11. [SUGERENCIA] **Arenas**: una arena de 12 m de lado permite un slide completo (D4.15) de extremo a extremo. Arenas de 20 m o más permiten que el Big Bot (1,4 m/s, parada a 3 m) y las torretas tengan espacio para moverse sin bloquearse.
12. [SUGERENCIA] **Bake de NavMesh**: fijar `agentClimb` en un solo valor (0,4 o 1,0) y documentarlo, porque las fuentes del proyecto difieren (D3.a frente a D3.b). Usar `cellSize` 0,1667 m (ya configurado), y dejar un margen de 0,5 m entre obstáculos y paredes para el agente de radio 0,5.

### Lo que no se verificó

- No se ejecutó Play Mode: todas las distancias y alturas son derivadas de valores serializados y de fórmulas, no medidas.
- La integración discreta de Unity por frame, la rampa de aceleración del sprint y el efecto de GroundCheckDistance = 1 requieren una prueba en Play Mode.
- El enum `CollectObjects` del `NavMeshSurface` (valor 2) no está confirmado.
- No localicé el NavMesh "propio" de radio 2,25 citado en `CHECKLIST_TRABAJO.md:411` y `:588`.
- No se revisó la lógica de comprobación de distancia de ejecución (E) más allá de su campo `interactionRange`.

### Skill aplicada

`.agents/skills/iron-requiem-fps/SKILL.md`: se usaron sus reglas de solo lectura de movimiento (el controlador CharacterController es la única autoridad de velocidad y gravedad; no se añadió Rigidbody ni otro controlador), y se leyeron los componentes existentes `PlayerCharacterController`, `AdvancedPlayerMovement` y `StolenWeaponCarrier` sin proponer cambios de código.


---

<!-- Anexo E_rendimiento_salas.md -->

## Anexo · F0-E · Rendimiento previo y salas actuales (inventario, solo lectura)

Fecha de corte: 18/11/2026. Proyecto: `C:\Users\gehbd\ProyectoGameFinal`. Sin Editor, sin builds, sin `unity` CLI, sin git que escriba, sin red. Único archivo escrito: este.

Convenciones: **[Editor]** = Unity Editor (incluye overhead, solo tendencia). **[Dev]** = Player Development. **[Rel]** = Player Release. Las cifras de Editor no son comparables con Release. Todas las cifras llevan su fuente entre paréntesis.

Fuentes principales: `Docs/IronRequiem/MAP_FX_AUDIT_20261117_r2.md` (§1-§10), `MAP_FX_AUDIT_20261117.md` (§Rendimiento), `Tools/IronRequiem/Reports/MapFxAudit2/{render-perf,map-structure,effects}.md`, `Tools/IronRequiem/Reports/Perf/*.txt`, `Tools/IronRequiem/Reports/MapFxAudit/{fx-perf,horde,volume-weight}.txt`, `Docs/IronRequiem/BUNKER_POLISH.md` §5-§7, `VISUAL_OVERHAUL.md` §6, `VISUAL_AUDIT_F0.md`, `CHECKLIST_TRABAJO.md` (v35 y "Verificación en build Release (17/11/2026)"), `Assets/Rendering/Default_PipelineAsset.asset` y `Default_PipelineAsset_ForwardRenderer.asset` (solo lectura, grep).

**Criterios de la skill `iron-requiem-performance` aplicados:** captura con resolución, calidad, enemigos y pasada registrados; Editor y Player separados; ninguna mejora de FPS atribuida sin control de vsync y de condiciones; presupuestos derivados de medidas y no universales; pooling y spawn revisados (reinicio de estado, cola de aparición); URP, materiales y mecánicas principales (velocidad, extracción de armas, OVERDRIVE) no se proponen para cambio.

---

### E1 Configuración de render relevante

| Parámetro | Valor | Fuente |
|---|---|---|
| Renderer | Forward (`m_RenderingMode: 0`), depth priming off, Native Render Pass off | `Default_PipelineAsset_ForwardRenderer.asset:50-51`; render-perf §1 |
| Pipeline | Un solo `Default_PipelineAsset`, el mismo en Quality y Graphics. Quality con un solo nivel `Default` | render-perf §1 |
| HDR | On, precisión 32 bits (`m_HDRColorBufferPrecision: 0`) | `Default_PipelineAsset.asset:27` |
| MSAA | Off (`m_MSAA: 1`). Cámara del jugador **AA None** en runtime (`Player_NativeVisual_v06.prefab:80` anula el FXAA base) | `Default_PipelineAsset.asset:28`; render-perf §1 |
| Render scale | 1. Upscaling Auto sin efecto. Sin resolución dinámica | `Default_PipelineAsset.asset:29-30`; render-perf §1 |
| Luces adicionales por objeto | **4** (`m_AdditionalLightsPerObjectLimit: 4`) | `Default_PipelineAsset.asset:48` |
| Sombra luz principal | Soportada, mapa 1024 | `:45-46` |
| Sombras de luces adicionales | Soportadas, mapa 1024. `m_ShadowAtlasResolution: 256` (`:92`, significado exacto no verificado) | `:49-50`, `:92` |
| Distancia y cascadas | 50 m, 1 cascada. Soft shadows soportadas | `:57-58`, `:66` |
| Reflection probes | Blending y box projection on. `realtimeReflectionProbes: 0` | `:54-55`; render-perf §1 |
| SSAO (única feature) | `Source` = DepthNormals (prepass DepthNormals de todos los opacos), `Downsample 0` (resolución completa), Samples Low (2), BlurQuality High, **Intensity 0,3, Radius 0,035** | Bloque de features del renderer; render-perf §1 |
| Batching | SRP Batcher on; dynamic batching off; GPU Resident Drawer off | render-perf §1 |
| Texturas y calidad | `pixelLightCount 4`, `anisotropicTextures: 1` (por textura), `textureQuality 0`, `antiAliasing 0`, `vSync 0`, `lodBias 2` | `QualitySettings.asset` vía render-perf §1 |
| Otros | Depth texture y opaque texture off. Adaptive Performance on, sin proveedor en PC (sin efecto esperado). Volume framework cada frame | `Default_PipelineAsset.asset`; `Packages/manifest.json:19`; render-perf §1 |
| Timing de GPU | `enableFrameTimingStats: 0` en el build de tour: **no hay ms de GPU en Release** | `ProjectSettings.asset:159`; render-perf §5 |

**Luces de escena (todas en tiempo real):** `m_Lightmapping: 4` en todas las escenas de campaña; sin LightingData, sin occlusion culling, sin LODGroups (render-perf §4; map-structure §1.2). Sombras en campaña: solo `KeySpot` de Room00 (zona protegida) renderiza sombra; la direccional `LabSun` no tiene sombra; Room01-04 y Pursuit05 no tienen luces con sombra (render-perf §4).

**Restricción práctica:** con Forward, un objeto ve como máximo 4 luces adicionales; las demás se descartan. Es el origen de las teselas de 10 m del búnker (BUNKER_POLISH §5) y del riesgo de "pop" en Room00b (AlarmRed×8, render-perf §4).

**Fuera de alcance por regla (render-perf §6):** Forward+, Deferred, GPU Resident Drawer, STP y cualquier cambio de pipeline o paquetes. Esta inventario no propone ninguno.

---

### E2 Mediciones previas

Leyenda de columnas: **media / p99** en ms de frame. "n.m." = no medido en esa fuente. Draw calls y SetPass solo existen en Editor; no hay ninguna medición de draw calls ni SetPass en Release para la campaña.

| fecha | escena | build / Editor | resolución | media | p99 | draw calls / SetPass | notas |
|---|---|---|---|---|---|---|---|
| 17/11 10:36 | Room00_Hangar (sin combate) | **Rel** `mapfx-release`, `-irTour -irFullHD`, vsync 0 | 1920×1080 exclusiva | 5,76 (174 FPS) | **11,62** | n.m. | 31 luces, 753 renderers (v1). Único "p99 > 8,33" con Pasillo y Assembly (Perf/tour_player_mapfx_release_1080p.txt) |
| 17/11 10:36 | Room00b_AlarmCorridor (3 enem., 6 s) | Rel | 1920×1080 excl. | 5,33 (188) | 9,09 | n.m. | Pico de frame 22,8 ms en llegada (sin >33 ms) |
| 17/11 10:36 | Room01_Foundry (6 enem., 6 s) | Rel | 1920×1080 excl. | 5,57 (179) | 7,85 | n.m. | Max 12,9 ms |
| 17/11 10:36 | Room02_Breach (6 enem., 6 s) | Rel | 1920×1080 excl. | 5,46 (183) | 7,33 | n.m. | Pico 26,4 ms en llegada |
| 17/11 10:36 | Room03_Assembly (6 enem., 6 s) | Rel | 1920×1080 excl. | 5,49 (182) | 8,43 | n.m. | Max 12,3 ms. Solo primera ronda (R2 y R3 no medidas) |
| 17/11 10:36 | Room04_Reactor (6 enem., 6 s) | Rel | 1920×1080 excl. | 5,22 (192) | 6,98 | n.m. | Pico 28,0 ms en llegada |
| 17/11 10:36 | Pursuit05 (sin combate) | Rel | 1920×1080 excl. | 3,88 (258) | 6,01 | n.m. | — |
| 17/11 10:36 | Transiciones (6 saltos) | Rel | 1920×1080 excl. | — | — | — | Pico 12,0–20,3 ms; >33 ms: 0; >50 ms: 0; total 1,8–4,5 s; puerta 1,60 s fija; descarga pico 11–19 ms |
| 17/11 10:31 | Campaña completa (tour) | Rel | **1600×900** (arranque por defecto, B1) | 5,06 / 4,55 / 5,13 / 4,57 / 4,81 / 4,76 / 3,77 (Hangar…Pursuit) | 9,27 / 6,51 / 7,04 / 6,17 / 6,22 / 6,37 / 6,30 | n.m. | Tour_player_mapfx_release_default.txt. Transiciones pico 11,6–33,6 ms |
| 14/11 01:30 | Campaña completa (tour) | **Dev** con profiler on, vsync 0 | 1600×900 | 14,55 / 13,69 / 14,81 / 13,29 / 14,76 / 14,27 / 10,58 | 22,28 / 21,77 / 20,60 / 18,83 / 19,45 / 18,79 / 14,18 | n.m. | Tour_player_after_final.txt. Pico de transición 25,9–35,8 ms. Base de frame alta incluso en Hangar y Pursuit (CHECKLIST v35: causa de entorno no resuelta) |
| 14/11 00:34 | Campaña completa (tour) | **Editor** | 769×532 | 24,13 (Hangar), 30,21, 30,35, 31,19, 19,28, 17,54, 10,89 (Pursuit) | 53,59 / 52,18 / 50,77 / 48,03 / 32,08 / 26,33 / 16,77 | n.m. | Tendencia. Pico de transición Room00b 2,40 s y Room01 3,66 s (Editor) |
| 17/11 (fx-perf) | Room01, pelea con fuego (637 frames) | **Editor** | 1030×556 | 22,02 (p50 19,37) | 34,59 | **SetPass máx. 241**; tris máx. 337 k; partículas máx. 222 / sistemas 34; luces activas 15; GC 98,5 KB/frame | Fx-perf.txt. Mismo día antes de E1: SetPass 227, 259 partículas |
| 17/11 (horde) | Room03 R1 / R2 / R3 | **Editor** | 1030×556 | 23,19 / 25,75 / 26,94 | **212,3 / 223,9 / 75,8** | SetPass 190 / 306 / 309; tris 400 k / 467 k / **603 k**; partículas 166 / 862 / **1373**; sistemas 22 / 89 / 133; luces 14 / 16 / 16 | Horde.txt. Los p99 de 212–224 ms coinciden con la aparición. **Discrepancia:** v1 y r2 citan 1,2 M tris para R3 (ver §Consistencia) |
| 08/11 (VISUAL_AUDIT_F0) | Room01, antes de ronda | **Editor** | 1080 (Game view con Stats) | 17 ms CPU | n.m. | **342 draw calls**, 11,7 k tris | Antes de v35 (dressing y pilastras añadidos después). Menú: 9 draw calls. Texto del informe: "muy limitado por CPU" (hilo principal 75–92 %) |
| 08/11 (VISUAL_AUDIT_F0) | Room01, ronda 1 y pico | **Editor** | 1080 | 22 ms / pico 50,3 ms CPU | n.m. | 341–346 / ≈300 draw calls; 96–181 k tris | GPU 1,7–1,9 ms (GPU sobrada) |
| 08/11 (VO §6, serie final) | BunkerBenchmark, 12 robots por oleada, 15 oleadas, 5 min | **Rel**, `CombatLoadBenchmark` + `RunCombatLoad.ps1`, vsync 0 | 1920×1080 excl. | A: 11,59 (media de 2 pasadas); **B: 12,61** | A: 17,63; **B: 19,12** | n.m. | Frames > 16,67 ms: A 2,5 % → B 8,1 %. GPU p99 4,8–5,6 ms; hilo de render 3,0–3,7 ms; CPU = frame (cuello de botella CPU). Mismo fixture A/B |
| 08/11 (VO §6, ablación) | BunkerBenchmark, B sin decales | **Rel** (5 oleadas) | 1920×1080 excl. | B sin decales 12,41; B sin decales ni probes ≈12,21 | 18,2–18,7 | n.m. | Quitar todo lo nuevo deja B ≈12,2 ms frente a A 11,0–11,7 en las mismas pasadas cortas. Decales ≈0,4–0,8 ms; cada grupo restante 0,2–0,3 ms |
| 06/11 (BUNKER §6) | Laboratorio anterior vs Búnker, misma carga, 5 min | **Rel** | 1920×1080 excl. | 6,97 vs 7,09 | 11,67 vs 12,90 | n.m. | Frames > 8,33 ms: 26,4 % → 14,4 %. Frames > 50 ms: 3 → 15 (52–98 ms, uno por oleada, en el frame de aparición de 12 enemigos). Hilo de render 1,50 → 1,88 ms |
| 06/11 (BUNKER §7, A/B) | Búnker sin / con `EnemySpawnQueue`, 15 oleadas, pasadas seguidas | **Rel** (portátil limitado a 16–20 W: solo comparable entre ellas) | 1920×1080 excl. | 10,96 → 10,14 | 16,69 → 18,40 | n.m. | Frames > 50 ms: **14 → 0**; > 33 ms: 16 → 0; máximo 80,7 → 27,3 ms. **Carga no idéntica:** disparos enemigos 834 → 1589 (ver BUNKER §7) |

**Notas de medición que condicionan cualquier comparación:**
- Las pasadas de Release 17/11 usan `-irTour`: solo la primera ronda (3–6 enemigos, jugador invencible y quieto, sin disparar). **No cubren R2-R3, muertes en cadena, disparo, OVERDRIVE ni carga sostenida** (render-perf §5, "Qué NO cubre").
- El tour de Release 1080p (17/11) fue la primera medida de campaña en Release; el informe de 1600×900 es el arranque por defecto y no un objetivo.
- Ningún dato de Release de campaña tiene draw calls, SetPass ni tiempo de GPU. Para el presupuesto de §E4 esos valores hay que medirlos (R6/R7 de render-perf).
- Los informes de Release que usan `teardown 120` y los de Dev `teardown 350` son parámetros distintos del benchmark, no de la escena.

---

### E3 Causas de coste conocidas y mitigaciones existentes

| # | Causa | Evidencia | Mitigación ya existente | Estado |
|---|---|---|---|---|
| 1 | **CPU-bound** (hilo principal). En el búnker, frame CPU = frame total; GPU p99 ≈ 5 ms | VISUAL_AUDIT_F0 (Editor, 75–92 % hilo principal); VO §6 (A/B Rel) | VO §6: reflection probes por sala (6 búnker, 16 lab; antes 10 y 33), light probes solo en enemigos (entorno: solo rebote), un solo keyword set para entorno y robots, decales 49→12 (bench) y 69→24 (lab), polvo 180→70, vapor 3→1 | Parcial: ≈0,5–1 ms de coste restante en efectos de robot y HardSurface (VO §6). **En campaña no aplicado** |
| 2 | **Pico de aparición de oleada** (12 enemigos en un frame) | BUNKER §6-§7 (52–98 ms en Rel); horde Editor p99 212–224 ms | `EnemySpawnQueue`: precarga en calma, 1 activación por frame. Medido en búnker: frames > 50 ms 14 → 0 | Activa en Room01 y Room03 (`spawnQueue:` en YAML, `Room01_Foundry.unity:8003`, `Room03_Assembly.unity:21197`). **Contradice** BUNKER §7 ("solo BunkerLab"). Pendiente: primera activación de la sesión ~140 ms con 8 T-07 (BUNKER §7) |
| 3 | **Restos y muertes** (3–22 piezas por enemigo, 22–60 s, `Update` en reposo, sin tope global; `MechDeathExplosion` crea un ParticleSystem en cada `Awake`; pool de 16 bursts en spawn) | effects.md F4–F5; render-perf §5 (hipótesis de los picos de Room03) | Ninguna en campaña. Hay un límite de piezas en el informe de v1 (E3) sin implementar | **Pendiente.** Propuestas E3+ (tope global 80 piezas, cero `Update` en reposo) y P-FX2/P-FX3 (precalentar pool, chispas horneadas) |
| 4 | **Pool de destrucción de un prefab por sala** (F1): lo que aporta E1 (escala y destello) puede no verse según el primer enemigo | effects.md F1 (`RobotDestructionPool.cs:16,25`, verificado) | E1 (`_IR`), parcial | **Roto en parte.** Propuesta E7 (un pool por prefab) |
| 5 | **Luces**: 31 en Hangar (p99 11,6 ms, zona protegida); 17 en Room00b (AlarmRed×8, riesgo de superar 4 por objeto); 4 por sala de combate; 14–16 activas en combate (Editor) | render-perf §4; map-structure §3 | Teselas de 10 m en búnker (210 piezas) para no pasar de 4 luces por objeto (BUNKER §5); luces de efectos con 1 point desactivada por defecto | Pendientes: R8 (Hangar, zona protegida), R9 (Room00b). F3: presupuestos de luz de efectos (BurstFlash + CannonExplosion) suman hasta 4 transitorias. E8 propone máx. 2 |
| 6 | **Reflection probes y light probes**: cada reflection probe por objeto cuesta ≈0,30 ms (VO §5); interpolar light probes ≈0,13 ms | VO §5 (perfil de CPU B−A) | Reducción por sala en búnker y lab; entorno sin light probes | Campaña: una caja de probe por sala (Room01 (0,3,0) 54×14×56; Room02-04 (0,12,y) 54×15×56; render-perf / map-structure §1.2). **Sin medir su coste en campaña** |
| 7 | **Partículas y sistemas vivos**: 862 (R2) y 1373 (R3) partículas en Editor; 89 y 133 sistemas | horde.txt, effects.md | Polvo 180→70 y vapor 3→1 en VO; estela más corta | Sin tope global. No hay soft particles (F6, E12 requiere OK de pipeline) |
| 8 | **Transiciones**: picos de 25–36 ms (Dev) y 12–20 ms (Rel); descarga `Resources.UnloadUnusedAssets` 17–23 ms con la puerta cerrada; JIT de Mono la primera vez | v35 (CHECKLIST); Perf/tour_player_mapfx_release_1080p.txt | v35: precarga de glifos TMP del español, teardown por tramos (sala vieja), subidas asíncronas 3 ms/frame, sala nueva pre-dibujada desde cámara oculta 320×180 (WarmRoom, 6 veces). v36: sin `UnloadUnusedAssets` manual durante gameplay | Parcial. IL2CPP no cambiado (decisión de backend, no medida) |
| 9 | **Hit-stop sin enfriamiento** (F9 / E9) y **SSAO a resolución completa con prepass DepthNormals** | effects.md; render-perf R4′ | — | Propuestas E9 y R4′ (Depth o media resolución). R4′ requiere OK por ser asset de render |
| 10 | **Resolución de la build**: en Rel por defecto 1600×900 (escritorio sin bordes), no 1920×1080 | render-perf §3 (IR-03, B1, B4) | `-irFullHD` obliga a exclusiva 1920×1080 para el tour | Diagnóstico B4 pendiente. **Toda medida de campaña a 1600×900 no es comparable con 1080p** |
| 11 | **Sombras** | render-perf §4: "prácticamente nulo" en salas | Sin sombras en Room01-04 y Pursuit05 | No medido en Rel. Coste real pendiente |

---

### E4 Propuesta de presupuesto por cuarto [PROPUESTA]

Todos los valores son propuestas razonadas con E2 y E3. **Ninguno está medido en Release para campaña**; se fijan para cumplirse en la prueba de §E4.0 antes de diseñar salas. Una sala de combate se mide en su ronda más cargada (R3), no en la primera.

### E4.0 Criterio de tiempo (Release, 1920×1080 exclusiva, vsync 0)

- Pasada de la ronda más cargada, al menos 60 s, A/B/A/B, con enemigos, disparo y muertes en cadena registrados (`-irAllRounds`, `-irBurst`, `-irFire`, `-irSustain` de render-perf §5).
- **Media ≤ 8,33 ms, p99 ≤ 11 ms, 0 frames > 33 ms en combate** (criterio IR-04 propuesto en render-perf §5).
- Base: la primera ronda ya cumple la media (5,2–5,8 ms) pero no el p99 en Hangar, Pasillo y Assembly. La carga de 12 robots por oleada (VO y BUNKER) no cumple la media: 10,1–12,6 ms.

### E4.1 Tabla de presupuestos por cuarto

| Concepto | Presupuesto propuesto | Base de datos | Nota |
|---|---|---|---|
| **Draw calls** por vista (sala idle) | **≤ 350** | 342 en Room01 antes de ronda (Editor, 08/11) | Dato **anterior a v35**, que añadió 90–180 piezas por sala. Hay que volver a medir. Medir con Frame Debugger en build Dev o Rel |
| **Draw calls** por vista (combate) | **≤ 400** | ≈300 en combate (Editor, 08/11) | Igual que arriba |
| **SetPass** por vista (combate) | **≤ 200 objetivo; techo 260** hasta medir Rel | 129 en idle y 227–241 en pelea Room01; 306–309 en Room03 R2-R3 (Editor, 17/11) | Editor no es Release. Usar el valor como orden de magnitud |
| **Triángulos** visibles (combate) | **≤ 400 k** | 181 k (08/11), 337 k (Room01 pelea, 17/11), 400–603 k (Room03 R1-R3, Editor) | El dato de 1,2 M de Room03 R3 no está confirmado (ver §Consistencia) |
| **Luces en tiempo real** estáticas por sala visibles | **≤ 8** | 4 por sala de combate (Room01-04); 17 en Room00b; 31 en Hangar (p99 11,6 ms) | Correlación, no causa: el Hangar tiene 753 renderers y 2,05 M tris. Medir R8 con `-irMarkers` antes de dar por bueno el límite |
| **Luces transitorias** (efectos) | **≤ 2** | 14–16 activas en combate (Editor); F3 suma hasta 4 | Propuesta E8 |
| **Luces por objeto** | **≤ 4** (límite URP) | Límite actual de `Default_PipelineAsset` | Teselar por ≤ 10 m (BUNKER §5); revisar AlarmRed×8 en Room00b (R9) |
| **Luces con sombra** | **≤ 2** por sala (una principal y una de acento) | Hoy 0 en Room01-04 y Pursuit05; 1 en Room00 (protegida) | VISUAL_AUDIT_F0 (F1) recomienda 1–2 luces principales con sombra; coste no medido en Rel |
| **Reflection probes** | **≤ 2 baked por sala** (sin realtime) | Una caja por sala en campaña; búnker usó 6 y el lab 16 (coste ≈0,30 ms por objeto muestreado, VO §5) | Light probes solo para enemigos |
| **Decales** | **≤ 24 por sala** | Lab VO: 24; bench: 12 | Coste de grupo 0,4–0,8 ms (VO §6) |
| **Partículas vivas** (combate) | **≤ 500 partículas, ≤ 60 sistemas** | Editor: 862 / 89 (R2) y 1373 / 133 (R3) | Polvo recortado a 70 en VO. Propuesta de tope global (E3+) |
| **Enemigos activos a la vez** | **≤ 8**; techo absoluto **12** hasta medir | Rondas 6 / 14 / 22 / 27 (campaña, map-structure §2.2). Medido: 12 robots/oleada = 10,1–12,6 ms de media en Rel | **Ninguna medición a 8 enemigos.** No se puede asegurar que 8 cumpla 8,33 ms sin medir. Las rondas de 22–27 exigen llegada escalonada o pool |
| **Aparición** | **1 enemigo activo por frame** | `EnemySpawnQueue` (búnker: frames > 50 ms 14 → 0) | Ya usado en Room01 y Room03 |
| **Restos** | **≤ 80 piezas vivas** globales; **cero** `Update` en reposo | F4–F5 (3–22 piezas por enemigo, 22–60 s) | Propuesta E3+ |
| **Transiciones** (Rel 1080p) | **Pico ≤ 33 ms; 0 frames > 50 ms; total ≤ 4,5 s** | Hoy: pico 12,0–20,3 ms; total 1,8–4,5 s; puerta 1,60 s fija | Ya cumplido en Rel 1080p; no en Dev (25–36 ms) |

### E4.2 Lectura de la propuesta

- **Límite más restrictivo:** los enemigos simultáneos. Una sala con 22–27 robots (Room03 R3, Room04 R3) no tiene un dato de frame en Release. Hay que elegir entre llegada escalonada, límite de enemigos activos o aceptar que la ronda 3 no cumple IR-04.
- **Luces:** el presupuesto de 8 estáticas por sala deja margen frente a las 17–31 de Room00b y Hangar, y no cambia las 4 actuales de Room01-04.
- **Lo que no se propone:** cambiar Forward, quitar SSAO (R4′ requiere OK), quitar efectos principales, o reducir la velocidad o la extracción de armas.

### E4.3 Medición previa obligatoria (antes de fijar cifras)

1. R6 y R7 (render-perf §5): ampliar el tour a rondas 2–3, muertes, disparo y carga sostenida, con frame timing de CPU y GPU en el build de medición (sin tocar ProjectSettings).
2. Frame Debugger de una sala de combate en Dev y Rel: draw calls y SetPass reales por sala tras v35.
3. A/B/A/B de cada sala con la misma semilla (P-FX1). Con 3 pasadas por variante, informar medianas.
4. Medir a 1920×1080 exclusiva y registrar plan de energía, nvidia-smi y `Screen.*`. El 1600×900 invalida la comparación (render-perf §3).

---

### E5 Salas actuales

Dimensiones y techos de `map-structure.md` §1.2 y §3 salvo indicación. "Zona" = zona protegida (IntroMenu completa; en Room00: pasarela, cinta, walkers, cinemática, `CinematicCamera`, Scavengers; `Scripts/Menu`, `Scripts/HangarIntro`, `Editor/Menu`, `Editor/HangarIntro`, `Prefabs/Menu/MenuWalker_*`).

| Sala | Dimensiones (planta) | Techo | Niveles / pasarelas | Landmark | Rondas y enemigos | Luces (YAML) | Defectos confirmados (M7-M19) | Zona protegida |
|---|---|---|---|---|---|---|---|---|
| **Room00_Hangar** (prólogo) | Bounds 149×84×114 m, z -183…-85 | n.d. | Pasarela y cinta (protegidas) | "HANGAR C-04 · SAL POR LA ESCLUSA", cinta | Sin rondas (`Release()` de cinemática). Scavengers y RoomEncounter de prólogo | 31 (17 spot, 13 point, 1 directional); 1 con sombra (KeySpot) | E2 cambia el post en runtime antes de la cinemática (P-E2a). Flotantes del hangar (v1). Puerta de la esclusa a0 en el plano de la cinemática | **Sí** |
| **Room00b_AlarmCorridor** | 18×34 m (x ±9, z -69,7…-35,6) | 8,95 m | Plataforma de guardia 5×1,4×5 con rampa; 3 bloques de cobertura 3,6×1,5×1,6 | `LobbyRear` visible a 27 m; `AlertScreen` (texto cortado) | R1: T07r×2 (guardias) + Carronero×1 (conducto) = 3. R2: Carronero×2 = 2. 6 slots fijos | 17 (9 spot, 8 point): AlarmRed×8, Down×5, BeaconSpot×4 | **M14**: pilastras en z -50 tapan `AlertScreen` (z -49,5). **R9**: riesgo de más de 4 luces por objeto (AlarmRed×8). Sin Volume propio | No (fuera de la zona; sus `HG_*` están en la lista roja) |
| **Room01_Foundry** (sector 1) | 54×65 m (z -33,5…31) | 11,95 m en map-structure; **12,45 m** tras v35 (dato más reciente) | Pasarela oeste 5,5 m de ancho a 3,8 m, 26 m, con rampa; **sin alto en el este**. Isla de 12,5 m inalcanzable en NavMesh | **Falsa salida** (4 `MoltenRun` de 11 m y panel de peligro en el muro norte). Salida real al este (z 19…31) | R1: T07r×6 (variación 6). R2: T07r×3 + Big×1 (variación 4). R3: Hover×3 + Big×2 + T07M×1 (variación 6). 12 slots en rejilla x {-18,0,18} z {-18,-6,6,18} | 4 point (`CeilingLight`, I 4,4), sin sombra | **M8 confirmado en Play**: `SouthWall` atraviesa la estación de montaje. **M11**: falsa salida (pendiente). Sin coberturas en z < -12 | No |
| **Room02_Breach** (sector 2) | Mismo volumen XZ que Room01 (x ±27, z ≈ -34…31); suelo y 8 | 20,95 m | 3 pasarelas a 11,6 m (oeste a ambos lados de la grieta, este solo lejano). Rampas z 8,3 y -9,8 | **Grieta** de 4 m en z -2…2 y `GapSign` | R1: T07r×4 + Saw×2 = 6. R2: Big×2 + Hover×3 + T07M×1 + Carronero×8 = **14** en 48×22 m con 9 slots. R3: Big×4 + Turret×1 + T07M×1 = 6. Combate solo en banco lejano (z 3…25) | 4 point, sin sombra | **M7 confirmado en Play**: franja sur sin suelo (z -33…-26,5). **M17**: baranda pendiente. Banco cercano sin combate | No |
| **Room03_Assembly** (sector 3) | 54×52 m (z 39…91) | 20,95 m | Pasarelas a ambos lados (z 67–86), rampas z 55,8–67,4. Bancos 4×1,6×10 en x ±10 | Marco amarillo de salida a 45 m, visible | R1: Saw×4 + T07r×2 = 6. R2: Big×2 + T07r×3 + T07M×1 = 6. R3: Saw×3 + Big×2 + Hover×2 + T07M×1 + Carronero×14 = **22** | 4 point, sin sombra | **Slot pegado a `CeilingSupport`** (x ±18, z 57/73): riesgo de "oleada en espera" en R3. Picos Editor 212–224 ms en aparición. Sin defecto de geometría confirmado | No |
| **Room04_Reactor** (sector 4) | 54×52 m (z 103…155), mismo casco que Room03 +64 m | 20,95 m | Pasarelas y rampas laterales; **`CeilingSupport` (±23; 129) atraviesa `Ramp_V35`** | `ReactorCore` 7 m de diámetro (z 125,5…132,5) y `EnergyColumn`; salida detrás del núcleo | R1: T07r×3 + Hover×3 = 6. R2: Big×2 + Saw×2 + Turret×1 + T07M×1 = 6. R3: Big×3 + Saw×3 + T07M×1 + Carronero×20 = **27** | 4 point, sin sombra | **M9 confirmado (bounds)**: rampa partida en 1,05 y 2,55 m. **M12**: guía de salida pendiente | No |
| **Pursuit05** (sector 5, persecución) | 16 m de ancho, 218 m de largo (z 167…385) | 16 m | 3 grietas de 4 m (z 233/283/333) | Sierra `GiantSawCarriage` a 8 m/s | 3 disparos: T07r×2 (z ≥ 208), Saw×1 (z ≥ 260), T07r×2 (z ≥ 310). Meta z 369 | 6 point | **Muro ciego** `ExitWall` (z 385,5), sin evento de fin: **M13**. **M19**: `SideCover` deja ranuras de 1 m. `ChaseEntranceGate` con separación de 3,98 m sin explicar | No |
| Esclusas Transfer 27/91/155 (persistentes) | Cajas 12×12×8 m | — | — | — | — | Única luz: punto I 4,4 a y 15,7 (7,7 m del suelo) | Casi negras (lum 0,01–0,04). **M10**: sin AirlockCycle | No (en `RequiemCampaign`, no protegida) |

**Nota:** en el pasillo, `AlertScreen` están en z -49,5, detrás de las pilastras; la propuesta M14 es mover los transforms (no toca los materiales `HG_*`).

---

### E6 Estado de las copias `_v2` y de los `*_MechTest`

**Copias `_v2` (creadas el 17/11 11:58, committed en `6dbe3be`; `git log --all` lo confirma):**

| Copia | Cambio presente frente al original (comparación de nombres de objeto) | Verificado en Play | Estado |
|---|---|---|---|
| `Room01_Foundry_v2.unity` | Añade `SouthWall_West_M8` y `SouthWall_East_M8` (M8 aplicado) | No (CHECKLIST: casilla de colisión pendiente) | M8 en copia. El `SouthWall` original sigue en la escena; no comprobado si quedó desactivado o movido. NavMesh `Room01_Foundry_v2_NavMesh.asset` presente |
| `Room02_Breach_v2.unity` | Añade `UpperSouthFill_M7` (M7 aplicado) | No | Igual. NavMesh `Room02_Breach_v2_NavMesh.asset` presente |
| `Room04_Reactor_v2.unity` | Añade `CeilingSupport_West_M9`, `CeilingSupport_East_M9`, `CeilingSupport_V35Ext_West_M9`, `CeilingSupport_V35Ext_East_M9` (M9 aplicado) | No | Igual. NavMesh `Room04_Reactor_v2_NavMesh.asset` presente |
| `Room00b_AlarmCorridor_v2`, `Pursuit05_v2`, `Room00_Hangar_v2` | **No existen** | — | M13, M14, M19, R8 y R9 sin copia |
| `RequiemCampaign_v2` | No existe (`stageScenes` del `RequiemCampaign` original) | — | Para probar `_v2` desde el menú haría falta tocar `IntroMenu` (zona protegida, `SceneName` de línea 3051) |

**`*_MechTest` (6 escenas, `RequiemCampaign_MechTest` incluido):** `Pursuit05_MechTest`, `Room01_Foundry_MechTest`, `Room02_Breach_MechTest`, `Room03_Assembly_MechTest`, `Room04_Reactor_MechTest` en `Assets/IronRequiem/Scenes/Campaign/`, y `Assets/IronRequiem/Scenes/RequiemCampaign_MechTest.unity`. Son copias de F8 con T-07 sustituidos por `T07_Mech_Test`, con geometría **anterior a v35** sobre los **NavMesh v35** (mismo GUID de `m_NavMeshData`). Están en Build Settings (índices 25-30). No tienen referencia en runtime; solo se abren desde el Editor (map-structure §1.4). Ninguna de las seis tiene uso en campaña: no se usan para medir.

**Estado de git (solo lectura, 18/11):** `git status` sobre `Assets/IronRequiem/Scenes/Campaign/` no muestra cambios. El informe r2 (17/11, §10) hablaba de cambios sin commit de `RequiemCampaign.unity` y de las salas; probablemente estén incluidos en `6dbe3be`. No verificado.

---

### §Consistencia: discrepancias entre fuentes

1. **Triángulos de Room03 R3:** `horde.txt` da 603 k; v1 (MAP_FX_AUDIT §Rendimiento) y r2 (§4 y §7 de `map-structure`) dan 1,20 M. Puede ser otra medida o un recorte. Sin resolver.
2. **Aparición con `EnemySpawnQueue`:** BUNKER §7 (06/11) dice que la cola solo está en `BunkerLab` y `BunkerBenchmark`. El YAML de campaña de Room01 y Room03 la asigna (`spawnQueue:`). Puede ser posterior a BUNKER §7. Room02 y Room04 no comprobadas.
3. **Techo de Room01:** 11,95 m en map-structure (§1.2 y §3) y 12,45 m tras v35 (map-structure §1.1, CHECKLIST v35: "techo 7–8 m → 12–13 m"). Usado 12,45.
4. **Parámetro de teardown:** v35 habla de tramos de ~350 transforms por frame; map-structure §2.1 dice 120. Los informes Rel tienen `teardown 120` y los Dev `teardown 350` en la cabecera. Parece un parámetro de medición, no un valor fijo de la sala.
5. **Cambios sin commit en la campaña:** r2 y map-structure hablan de cambios del usuario sin commit; `git status` del 18/11 no los muestra (ver §E6).
6. **Draw calls de Room01 (342):** de 08/11, antes de v35 y de las capturas de 17/11. Cualquier presupuesto de draw calls debe medirse de nuevo.
7. **Frame base alta en Dev (14/11):** en Hangar y Pursuit (sin combate) la media es 10–15 ms, lo que la CHECKLIST v35 atribuye a energía, temperatura o Editor abierto, no a las salas. No resuelto. Los datos de Dev no se usan para presupuestos.

---

### Verificaciones que no se han hecho

- No se abrió el Editor ni Play Mode. No se midió Release nueva.
- Los `*_NavMesh.asset` son binarios: no se verificaron agentes ni conectividad.
- Coste de sombras, probes y decales en campaña: sin medir en Release.
- Room02 y Room04 no se comprobaron en YAML por `spawnQueue` (solo Room01 y Room03).
