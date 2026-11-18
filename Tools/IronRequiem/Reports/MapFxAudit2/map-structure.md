# MAPA · Estructura de la campaña (MapFxAudit 2, 17/11/2026)

Subagente MAPA ESTRUCTURA. **Solo lectura**: no se abrió el Editor ni se modificó nada; único archivo escrito: este.
Complementa `Docs/IronRequiem/MAP_FX_AUDIT_20261117.md` (v1) sin repetirlo. Lo que v1 ya cubre (probes sin datos, grado de sector, materiales repetidos, esclusas oscuras en %, flotantes del hangar, rendimiento) solo se cita.

**Método.**
- Parser propio del YAML de escena (transform jerárquico con rotación y escala, prefab instances, MonoBehaviours por GUID de script). Las posiciones son **mundo, en metros**.
- Lectura de `Scripts/Levels/*.cs` y `MapFxAudit/map.txt`.
- 12 capturas PNG revisadas: `r01-pano-0/180`, `r01-route-exit`, `r02-gap-before-jump`, `r02-overview-a`, `r02-pano-180`, `r03-overview-a`, `r03-pano-0`, `r04-route-exit`, `r05-gap1`, `r05-exit`, `r00b-route-exit` y `r00c-lobby-station`.

**Límites.**
- No se reprodujo nada en Play Mode. Los hallazgos marcados como **estático** salen de la geometría y no se han probado jugando.
- Los `*_NavMesh.asset` son binarios; no se leyó su contenido.

---

## 0. Correcciones a v1

1. **Las capturas `*-overview-a/b` están tomadas desde fuera de las salas.** `MapFxAudit.cs:259-261` coloca la cámara a 1,5 m de las esquinas de los bounds de *todos* los renderers, y esos bounds incluyen `VisualPass_VoidShell` (71 m de ancho frente a los 54 m de la sala). La cámara queda en x≈±34, al otro lado de los muros (±27).
   - `r02-breach-overview-a.png` y `r03-assembly-overview-a.png` muestran la cara exterior de los muros.
   - Las cifras de "64–72 % oscuro" en las vistas altas (Room01-04 y Pursuit) son un **artefacto del método**, no oscuridad de la sala.
   - Hay que retirar la frase de v1 "Room02: el techo a 21 m queda negro" o volver a medir con la cámara dentro.
2. **Las panorámicas son desde el punto de entrada**, no desde el centro. La visibilidad de la salida (`route … visible`) es un linecast entre la entrada y la puerta.

---

## 1. Escenas

### 1.1 Disposición en el mundo (todas comparten coordenadas; carga aditiva)

```
z:  -183 ……… -85 | -84 … -70 | -70 … -35.5 | -35 … -26 | -34 ……… 31 (y=0) | escalera x=27..35 | -34 … 27 (y=8) | 27…39 | 39 … 91 | 91…103 | 103 … 155 | 155…167 | 167 ……… 385.5
    Room00_Hangar  | Chamber   | Room00b     | StartLobby| Room01_Foundry    | y 0→8, z 24→-8    | Room02_Breach    | T_27  | Room03  | T_91   | Room04    | T_155   | Pursuit05
    (prólogo)      | (persist.)| (pasillo)   | (persist.)| (planta baja)     | (persistente)     | (ENCIMA de R01)  |(pers.)|         | (pers.)|           | (pers.) | (fin: muro)
```

- **Room01 y Room02 ocupan el mismo volumen XZ** (x ±27, z ≈ -34…31). Room01 va de y 0 a 12,45 (techo v35) y Room02 de y 8 a 21. Solo funciona porque nunca están las dos visibles: Room02 se activa con Room01 aún cargada, y Room01 se desmonta mientras la puerta sigue cerrada (`CampaignFlow.cs:237-249`).
- **Persistentes en `RequiemCampaign.unity`** (`PersistentAirlocks`): ConnectionChamber, StartLobby, la escalera (40 escalones de 0,2×0,8 m, de 8 m de ancho, z 24 → -8), UpperLanding, TransferAirlock_27/91/155 y todas las puertas.

### 1.2 Tabla por escena

| Escena | Propósito | Raíces principales (objetos) | Interior útil | NavMeshSurface (Children) → asset | Probe |
|---|---|---|---|---|---|
| `RequiemCampaign` | Sesión persistente: jugador, HUD, menú, música, esclusas, flujo | `PersistentAirlocks` (206), `GameManager` (prefab), `CampaignSession` (CampaignFlow), `CombatLabMenu`, `ArenaMessages`, `CampaignStartCover`, `LabSun` | — | Ninguno: cada sala trae el suyo | No |
| `Room00_Hangar` | Prólogo: cinemática y salida por la esclusa. Título "HANGAR C-04 · SAL POR LA ESCLUSA" | `HangarSector` (961), `WakeUpCinematic`🔒, `Scavengers`🔒, `RoomEncounter` (CampaignRoom, prólogo) | Bounds 149×84×114, z -183…-85 | En el GO 715812677 → `Room00_Hangar_NavMesh.asset` | No |
| `Room00b_AlarmCorridor` | Pasillo de alarma: 2 rondas, sector sin numerar ("PUENTE NORTE") | `AlarmCorridor` (114: Geometry, Cover, Lights, Siren, RoomEncounter, DuctSpawn0-3, GuardSpawn0-1), `V35_FloorGuide`, `Quarantine_v35` (inactiva) | 18×34 m (x ±9, z -69,7…-35,6), techo 8,95 | `AlarmCorridor` → `Room00b_AlarmCorridor_NavMesh` | No |
| `Room01_Foundry` | SECTOR 1, 3 rondas | `TwoFloorsGeometry` (56), `V35_Dressing` (105), `VisualPass_Dressing` (53), `V35_FloorGuide`, `RoomEncounter` (WaveSlots ×12), `VisualPass_VoidShell`, `SectorReflection`, `VisualPass_SectorGrade`, `Quarantine_v35` (inactiva) | 54×65 m (z -33,5…31), techo 11,95. Pasarela oeste a 3,8 m (26 m de largo) con rampa | `TwoFloorsGeometry` → `Room01_Foundry_NavMesh` (niveles 0 / 0,5 / 4 / **12,5** = isla sobre el techo, inalcanzable) | (0,3,0), caja 54×14×56 |
| `Room02_Breach` | SECTOR 2, 3 rondas, grieta de 4 m | Igual que Room01 más `GapReturnPoints` (10), `GapFallRecovery`, `VisualPass_LavaPools`, `GapSign` | Banco cercano z -26…-2, grieta z -2…2, banco lejano z 2…26; suelo a y 8, techo 20,95 | `TwoFloorsGeometry` → `Room02_Breach_NavMesh` (8 / 8,5 / 12 / **21,5** = isla sobre el tejado) | (0,12,10), caja 54×15×56 |
| `Room03_Assembly` | SECTOR 3, 3 rondas | `AssemblyGeometry` (68), `V35_Dressing` (181), `VisualPass_Dressing`, `V35_FloorGuide`, `RoomEncounter`, `VoidShell`, probe y grade | 54×52 m (z 39…91), techo 20,95. Pasarelas a ambos lados a 11,6–11,8 | `AssemblyGeometry` → `Room03_Assembly_NavMesh` | (0,12,65), caja 54×15×56 |
| `Room04_Reactor` | SECTOR 4, 3 rondas | `ReactorGeometry` (65, con `ReactorCore` 7×2,75×7), `V35_Dressing` (91), resto igual que Room03; `Quarantine_v35` (inactiva) | 54×52 m (z 103…155), mismo casco que Room03 | `ReactorGeometry` → `Room04_Reactor_NavMesh` | (0,12,129), caja 54×15×56 |
| `Pursuit05` | SECTOR 5: persecución de sierras | `ChaseCorridorGeometry` (46), `GiantSawCarriage` (40), `ReturnPoints_48/98/148`, `AmbushPoints` (5), `ChaseEncounter`, `ChaseGap_48/98/148`, `ChaseEntranceGate` (inactiva), `ChaseExitGate` | 16 m de ancho y 218 m de largo (z 167…385), techo 16 | `ChaseCorridorGeometry` → `Pursuit05_NavMesh` | (0,12,276), caja 20×9×224 |

🔒 = zona protegida.

- Todas las escenas tienen `m_LightingDataAsset` por defecto, sin LightingData (confirma v1).
- No hay `OffMeshLink` ni `NavMeshLink` en ninguna sala (0 en las 7).
- Todos los NavMeshSurface usan agente 0 (Humanoid): radio 0,5, altura 2, escalón 1 y pendiente 45 según `NavMeshAreas.asset`. Las 3 configuraciones distintas por asset que cita v1 no se han verificado aquí, porque los assets son binarios.

### 1.3 Conexiones (`CampaignFlow.airlocks`, `RequiemCampaign.unity`)

| Etapa → siguiente | CampaignAirlock (trigger) | Puerta origen → destino (+ extras) | AirlockCycle |
|---|---|---|---|
| 0 Hangar → 1 Pasillo | `HangarTransit/ConnectionChamber/ChamberAirlock` (0;2;-79,7), caja 7×4×5,8 | `ChamberHangarDoor` (0;2,4;-84) → `ChamberCorridorDoor` (-2,3;2,4;-76) + `VestibuleShutter` (z -70,2) | **Sí** (la única) |
| 1 Pasillo → 2 Room01 | `StartLobby/HangarAirlock` (0;2;-30,2), 10×4×5 | `LobbyRear` (z -35) → `InitialLoadingDoor` (z -26) | No |
| 2 Room01 → 3 Room02 | `StairAirlock` (31;2;25), 5×4×6, al pie de la escalera | `Room01ExitDoor` (27;3,8;25) → `Room02EntryDoor` (27;11,8;-10) | No |
| 3 Room02 → 4 Room03 | `TransferAirlock_27/Transfer` (0;10;33), 8×4×6 | `SourceDoor` z 27 → `DestinationDoor` z 39 | No |
| 4 Room03 → 5 Room04 | `TransferAirlock_91/Transfer` (0;10;97) | z 91 → z 103 | No |
| 5 Room04 → 6 Pursuit | `TransferAirlock_155/Transfer` (0;10;161) | z 155 → z 167 | No |
| 6 Pursuit → (nada) | — | `ChaseExitGate` (z 373) se desactiva y el muro `ExitWall` (z 385,5) cierra el paso | — |

- **La campaña termina en un muro ciego.** No encontré código que reaccione a `Pursuit.Completed`: ni WinScene, ni créditos, ni volver al menú (estático; `r05-pursuit-exit.png` muestra el muro).
- **Los Transfer no tienen AirlockCycle** (`cycle: {fileID: 0}`), ni ningún sistema de luz o vapor durante la espera.
  - Su única luz es un **punto de I=4,4 a y=15,7**, a 7,7 m del suelo (y=8), con alcance de 12 m.
  - Por atenuación cuadrática, en el suelo llega como mucho alrededor de 4,4/7,7² ≈ 0,07. Eso explica el 89-97 % de píxeles oscuros que midió v1.

### 1.4 Escenas `*_MechTest`

- Son copias de F8 (`Editor/VisualPass/VisualPassMechScene.cs`) en las que los T-07 se sustituyen por `T07_Mech_Test`.
- `RequiemCampaign_MechTest.stageScenes` empieza en `Room01_Foundry_MechTest` (sin hangar ni pasillo), con 5 etapas y 4 esclusas.
- Usan geometría **anterior a v35** sobre los **mismos NavMesh v35**: tienen el mismo GUID de `m_NavMeshData`.
  - Ejemplo en Room01_MechTest: el techo está a y=7,2, no tiene pasarela ni rampa y las coberturas son las pequeñas. El NavMesh compartido incluye superficie a y=4 (la pasarela v35) que no existe en esa escena.
  - Los agentes podrían planificar sobre aire. Es estático y solo afecta a las escenas de prueba.
- **Las 6 están en Build Settings** (índices 25-30, `enabled: 1`; 33/33 activas). No encontré referencias en runtime: solo se abren desde el Editor. `Pursuit05_MechTest` solo difiere de `Pursuit05` en referencias de prefab.

---

## 2. Flujo

### 2.1 Máquina de etapas (`CampaignFlow.Prepare`)

1. **Start** → `Prepare(0)`.
   - La primera etapa se carga en aditivo con la activación retenida, detrás de `CampaignStartCover` (activo porque `prologueStages=2`).
   - Al activarla se aplican su entorno (`ZoneEnvironment.Capture` y `BlendIn`, 1,6 s) y sus volúmenes y audio (`SceneFade`).
   - Se abre `initialDoor`, que es **nulo** en la campaña: el hangar no tiene puerta inicial.
   - Por último se precarga la etapa siguiente (`PreloadFollowingStage`, retenida al 90 %).
2. **En la sala.** `Update` abre la `SourceDoor` de la esclusa actual en cuanto `CurrentRoom.Completed` es verdadero. Qué significa "completado":
   - **Rondas:** `CombatTwoRoomsLab.Completed`.
   - **Persecución:** `ChaseCorridorEncounter.Completed`.
   - **Prólogo:** `Release()`, que llama `HangarIntroCinematic`.
3. **Transición.** El trigger `CampaignAirlock` llama a `TryTransition(i)`.
   - Se cierra la puerta de origen y arranca `Cycle.Begin` (solo en el hangar).
   - Durante el cierre se funden el post y el audio de la sala saliente hacia el entorno neutro de la sesión.
   - Se espera a que se cumplan tres condiciones: puerta cerrada, jugador dentro y carga ≥ 0,9.
   - **Abortar:** si el jugador queda fuera del trigger con la puerta cerrada durante más de 1 s, se reabre la puerta, se descarta la precarga y la transición se cancela.
4. **Activar** la escena precargada y convertirla en la activa.
   - Se busca su `CampaignRoom`.
   - Al frame siguiente, `Teardown` destruye la sala vieja en tramos de 120 transforms por frame y después llama a `UnloadSceneAsync`.
5. **Settle.**
   - `WarmRoom` renderiza la sala nueva 6 veces a 320×180 desde una cámara oculta.
   - Se espera a `CurrentRoom.Ready`, que exige que no queden instancias por precalentar en la cola.
   - Si la etapa no es de prólogo, se espera además un mínimo de 1,5 s. Mientras tanto se muestra "PREPARANDO SECTOR n…".
6. **Abrir.** Se abren `DestinationDoor` y las extras, y se llama a `Cycle.Complete`.
   - Se muestra el título: `prologueTitle`, "PERSECUCIÓN · ATRAVIESA LA PUERTA" en la última etapa o "SECTOR n LISTO · TRES RONDAS".
   - Se precarga la etapa siguiente.

**Notas sobre el flujo:**
- `ChaseCorridorEncounter.Start` escribe "SECTOR 03 · ATRAVIESA LA PUERTA", que es un literal antiguo. CampaignFlow lo sobrescribe al terminar la preparación; aun así, puede verse un instante (estático).
- `LabRoomVisibility` **no se usa en la campaña** (solo en CombatTwoFloorsLab y CombatTwoRoomsLab).
- `AirlockCycle` solo está en ConnectionChamber.
- `SectorLightingBlend` está en RequiemCampaign; su solapamiento con ZoneEnvironment lo trata v1.

### 2.2 Rondas por sala (descripción, sin propuesta de cambio)

En todas las salas `CombatTwoRoomsLab` funciona en modo `singleArena=1`: cada ronda empieza cuando muere la anterior y no hay compuerta intermedia. Spawns con `EnemySpawnQueue` (1 activación por frame).

| Sala | Disparo (bounds `firstRoom`) | Slots | Ronda 1 | Ronda 2 | Ronda 3 | Varied / variaciones |
|---|---|---|---|---|---|---|
| Room00b | z -64…-37, y -1…10 | 6 fijos: GuardSpawn0/1 en la plataforma norte y DuctSpawn0/2/3 en los conductos | T07r ×2 (guardias) + Carronero ×1 (conducto) = 3 | Carronero ×2 (conductos) = 2 | — | No; puntos fijos. `playerClearance` 3 |
| Room01 | z -24…24, y -0,5…4,5 | 12 en rejilla x {-18,0,18} × z {-18,-6,6,18} | Base T07r ×6; variación 6 | Base T07r ×3 + Big ×1; variación 4 | Base Hover ×3 + Big ×2 + T07M ×1; variación 6 | Sí, 3 variaciones por ronda ("Presión mixta", "Flancos móviles", "Apoyo y asalto"); semilla 0 = aleatoria |
| Room02 | **Solo el banco lejano**: z 3…25, y 7,5…12,5 | 9 en z 6…20 + `CentralTurret` fijo (0;8;14) | T07r ×4 + Saw ×2 = 6 | Big ×2 + Hover ×3 + T07M ×1 + Carronero ×8 = **14** | Big ×4 + Turret ×1 + T07M ×1 = 6 | Varied=1, sin variaciones; orden y slots barajados |
| Room03 | z 42…88, y 7…13 | 12: x {-18,0,18} × z {47,57,73,83} | Saw ×4 + T07r ×2 = 6 | Big ×2 + T07r ×3 + T07M ×1 = 6 | Saw ×3 + Big ×2 + Hover ×2 + T07M ×1 + Carronero ×14 = **22** | Igual que Room02 |
| Room04 | z 106…152, y 7…13 | 12: x {-18,0,18} × z {111,121,137,147} + `TurretSlot` fijo (0;8;141) | T07r ×3 + Hover ×3 = 6 | Big ×2 + Saw ×2 + Turret ×1 + T07M ×1 = 6 | Big ×3 + Saw ×3 + T07M ×1 + Carronero ×20 = **27** | Igual que Room02 |
| Pursuit05 | Trigger `ChaseEncounter` en z 195 | 5 en `AmbushPoints` | z ≥ 208: T07r ×2 en (±4,5; 219) | z ≥ 260: Saw ×1 en (-4; 271) | z ≥ 310: T07r ×2 en (±4,5; 321) | Sierra a 8 m/s tras 2 s de aviso; 100 de daño cada 0,35 s; meta en z 369 |

Abreviaturas: T07r = T07_Current_Repaired, T07M = T07_Mech, Big = BigBot_Current, Hover = HoverBot_Current, Saw = SawBot_Current.

- **Spawn seguro** (`SafeSpawn`): sobre el NavMesh (0,75), dentro de los bounds y a más de 6 m + radio del jugador; con hueco en la reserva, camino completo hasta el jugador y una cápsula libre.
- **No comprueba si el jugador ve el punto:** los enemigos aparecen a la vista en el suelo abierto. Solo el pasillo tiene fuentes diegéticas (conductos y plataforma).

---

## 3. Revisión de diseño por sala

Los hallazgos **nuevos** van en negrita. "est." = estático (geometría o código), sin reproducir en Play.

### Transición hangar → pasillo → lobby (fuera de la zona protegida)
- La salida del hangar es `ChamberHangarDoor`, que vive en RequiemCampaign y no en el hangar. Por eso una guía de luz puede ir en `ConnectionChamber` sin tocar `Room00_Hangar.unity`.
- **Pasillo (Room00b), recorrido.** Es lineal de sur a norte, con la puerta `LobbyRear` visible a 27 m.
  - Coberturas: 3 bloques de 3,6×1,5×1,6, alternados a x ±4,6/5. Plataforma de guardia de 5×1,4×5 con rampa.
  - Carriles libres de unos 4 m entre bloques y muros: el dash y el slide funcionan.
- **Pasillo, pantallas tapadas (est., visible en captura).** Las dos `AlertScreen` y sus `ScreenFrame` de 2,4 m de ancho en z -49,5 (x ±9) quedan detrás de las `Pilaster` en z -50 (0,9 de fondo, sobresalen 0,3 m). La pilastra tapa el centro del texto: en `r00b-corridor-route-exit.png` se lee "ZO… EJADA / …BIERTA".
- **El muro sur de Room01 invade el lobby persistente (est. + captura).**
  - `Room01_Foundry/TwoFloorsGeometry/SouthWall` mide 54×7×1 en z -34 (ocupa z -34,5…-33,5) y `SouthWall_V35Ext` sube hasta y 12. El lobby (`StartLobby`) va de z -35 a -26.
  - Al activarse Room01, que ocurre con el jugador dentro del lobby, aparece un muro a 1 m por delante de `LobbyRear`, y **`V5_MountStation` (z -33,9) queda dentro de ese muro**.
  - `r01-foundry-pano-180.png` lo confirma: el fondo del lobby muestra los paneles azules de Room01 con solo la barra y los soportes de la estación asomando, y `LobbyRear` queda tapada.
  - **Riesgo (sin reproducir):** si el jugador está en z < -33 cuando se activa Room01 (pegado a la estación, que se usa con la tecla E y dura 5,6 s), el muro aparece sobre su cápsula.
  - Además `GroundFloor` de Room01 (cara superior en y 0,00) se superpone al `LobbyFloor` (y -0,02) en 12×8 m. Hay 2 cm de separación, posible z-fighting lejano; no se aprecia en las capturas.

### Room01_Foundry
- **Salida.**
  - **Falsa salida:** desde la entrada (z -26, mirando al norte, `r01-pano-0`), los 4 `MoltenRun` de 11 m de alto en x ±6 y ±18 enmarcan el panel de rayas de peligro del horno, en el centro del muro norte. Se lee como una puerta de 12 m, y los chevrones van recto hacia allí hasta z 9 antes de girar al este.
  - La salida real (`Room01ExitDoor`, 12 m, en el muro este, z 19…31) solo tiene `ExitFrameGlow` en un lateral y queda detrás de la `HighCover` (9;7).
- **Movilidad.** Carril central x -5…5 (10 m) y carriles exteriores de 11 m. 4 columnas de 2×2 en (±14; ±12).
  - Pasarela oeste de 5,5 m de ancho a 3,8 m, con baranda solo en el borde interior.
  - **No hay alto en el lado este:** la asimetría deja la mitad este sin juego vertical.
- **Coberturas.** Bajas de 6×1,4×2,5 en (±8; -6) y altas de 4,5×3,6×2,5 en (±9; 7), simétricas.
  - La zona sur (z < -12) no tiene coberturas, y es donde aparecen los slots de z -18.
  - Al entrar, el jugador se encuentra 3 slots a 8 m (z -18) en campo abierto.
- **Atascos.** No hay obstáculos en la rampa (x -26,5…-21; columnas en x ±13…15). El hueco sur pasarela–muro es de 0,5 m (pasarela hasta x -26,55, muro interior en -26,5); no hay atasco.

### Escalera Room01 → Room02 (persistente)
- 40 escalones de 0,2 m (el escalón del CharacterController es 0,3: correcto). 8 m de ancho y 32 m de largo, entre muros.
- Las `StepMark_*` quedan **5 mm por encima** de cada escalón (y = cara superior + 0,005 ± 0,005), con riesgo de z-fighting a distancia (est.).

### Room02_Breach
- **Hueco sin suelo junto al muro sur (est., grave si se confirma).**
  - `UpperNearBank` (54×1×24) va de z -26 a -2, pero el muro sur tiene su cara interior en z -33,5. Queda una **franja de 54×7,5 m sin suelo** a y 8.
  - No hay ningún objeto con collider entre y 3 y 11 en z < -24.
  - `VisualPass_VoidShell` no tiene colliders. `GapFallRecovery` solo cubre z -2…2 e y 3,2…6,4.
  - Consecuencia: caída hasta `KillHeight` -50, es decir, muerte. Ni el chequeo "NavMesh sin suelo" de v1 ni el "Reach" lo detectan, porque ahí no hay NavMesh.
  - En `r02-breach-pano-180.png` se ve la banda negra entre el borde del suelo y la base del muro sur.
  - Origen probable: en el laboratorio de dos plantas, el techo de Room01 (y 7,45) quedaba debajo; con las salas en streaming, Room01 ya no está.
- Grieta norte: entre el banco lejano (hasta z 26) y `NorthExitWall` (cara en z 26,5) hay una ranura de 0,5 m, salvo bajo la puerta, donde la cubre `VisualPass_DoorSill`. Con una cápsula de r 0,35 el jugador se hunde unos 10 cm: tropiezo, no caída.
- **Zona segura en el banco cercano.**
  - El combate solo usa el banco lejano (bounds z 3…25) y no hay NavMeshLink a través de la grieta.
  - Un jugador que vuelva de un salto al banco cercano no puede ser perseguido; solo le alcanza el fuego a distancia.
  - La ronda 2 (14 enemigos) ocurre en una franja de 48×22 m con 9 slots.
  - Al aterrizar en (0; 9,7; 5,1), los slots válidos rodean al jugador a 6–18 m en todo el banco.
- **Verticalidad.** Hay 3 pasarelas a 11,6 m: oeste a ambos lados de la grieta, este solo en el lado lejano. Rampas en z 8,3 y -9,8.
- **Repetición.** Las coberturas y columnas están en las **mismas x/z que en Room01**, desplazadas solo en y (+8).

### Room03_Assembly
- **Salida.** Recta: puerta a 45 m con marco amarillo, visible (`r03-pano-0`).
- **Coberturas y carriles.** Bancos de 4×1,6×10 en x ±10 (8…12), en dos tramos (z 52…62 y 68…78). Forman 2 carriles largos a cada lado (x 0…8 y 12…17).
  - Los soportes de 2×2 en (±18; 58/72) cortan el carril exterior.
  - **Pasarelas** a ambos lados (z 67…86) con rampas (z 55,8…67,4).
- **Slot pegado a columna.** Los slots (±18; 57/73) caen justo en la cara de `CeilingSupport` (x 17…19, z 57…59 / 71…73). v1 ya lo citó; `Reach` fue correcto. Es el caso más probable de "OLEADA EN ESPERA" en la ronda 3 (22 enemigos).

### Room04_Reactor
- **Columna dentro de la rampa (est., probable atasco).**
  - `CeilingSupport` (2×8×2) en (±23; 12; 129) ocupa x 22…24 y z 128…130.
  - `Ramp_V35` va de x -26,55 a -21,05 y de z 119,8 a 131,4: **la columna atraviesa la rampa** cerca de su extremo alto, a unos 10,9 m de altura, y la divide en pasos de 1,05 m y 2,55 m. Pasa lo mismo en el lado este.
  - En Room03 los soportes están en x ±18 y no tocan la rampa: el problema es exclusivo de Room04.
- **Salida.** El `ReactorCore` (7 m de diámetro, en z 125,5…132,5) y la `EnergyColumn` están en el eje entrada–salida. Los chevrones rodean el núcleo por la izquierda (`r04-route-exit.png`); el marco de salida no se ve. Detrás del núcleo está el `TurretSlot` (0;8;141).
- **Coberturas.** 4 `RingCover_V35` de 6×1,4×3,5 en (±12; 121/137), con franjas de peligro, bien legibles. El núcleo hace de gran cobertura central: buen flanqueo en anillo.
- **Mismo casco que Room03:** suelo de 54×52, `PortalWall`, pasarelas y rampas en posiciones relativas idénticas (+64 m en z).

### Pursuit05
- **Recorrido.** Pasillo de 16 m de ancho y 218 m de largo, con 3 grietas de 4 m (z 233/283/333) con bordes de peligro y `PitGlow`.
  - Las coberturas laterales de 2×2 en x ±6 dejan una ranura de 1 m contra el muro: posible enganche al correr con la sierra detrás (est.).
- **`ChaseEntranceGate`** (16×8×0,6, centro en y 12) está inactiva en la escena y se activa al empezar. Su base geométrica queda en y 8 = suelo. Los 3,98 m de separación que midió v1 no salen de la escala del transform: habría que revisar la malla o el pivote; no se resolvió.
- **Final.** Muro ciego en z 385,5, 12 m después de `ChaseExitGate`, sin salida ni evento (sección 1.3).

### Variedad y legibilidad general (complemento a v1 §6)
- **Simetría.** Todas las salas son simétricas en x salvo la pasarela de Room01 y la pasarela este de Room02. Combinadas con la rejilla de spawns de 3×4, los encuentros se leen igual en todas las salas.
- **Las únicas "piezas firma" con colisión** son la grieta (Room02), los bancos (Room03) y el núcleo con anillos (Room04). Room01 no tiene ninguna; el horno es decorado sin colisión de juego.
- **Esclusas.** Las tres Transfer son cajas idénticas de 12×12×8 m sin AirlockCycle; aportan un respiro, pero ninguna orientación.

---

## 4. Propuestas de MAPA

Continúan la numeración de v1. M1 ya está hecho; M2–M6 de v1 siguen vigentes y no se repiten. Impacto, esfuerzo y riesgo: A / M / B.

**Regla de trabajo para todas las propuestas.**
- `RequiemCampaign.unity` y todas las salas tienen **cambios del usuario sin commit** (`git status`). AGENTS.md prohíbe guardarlas, así que todo se hace sobre copias `*_v2`.
- Las copias necesitan una `RequiemCampaign_v2` con `stageScenes` apuntando a ellas, y añadirlas a Build Settings, lo que exige respaldar `EditorBuildSettings.asset`.
- **Para jugar las `_v2` desde el menú habría que cambiar el `LoadSceneButton` de IntroMenu (`SceneName: RequiemCampaign`, línea 3051): zona protegida.** Alternativa: probarlas abriendo `RequiemCampaign_v2` en el Editor, o promoverlas sobre los originales después de que el usuario haga commit.

| ID | Propuesta | Impacto | Esfuerzo | Riesgo | Archivos (siempre copias) | ¿Toca zona protegida? |
|---|---|---|---|---|---|---|
| M7 | **Cerrar el hueco sur de Room02**: alargar `UpperNearBank` hasta z -33,5 (54×1×31,5) o añadir una losa de 54×1×7,5 en z -29,75; rehornear el NavMesh de la copia. Antes, comprobarlo en Play: caminar hacia el sur desde la entrada (27;8;-10) | **A** | B | B | `Room02_Breach_v2.unity`, `Art/Levels/Campaign/Room02_Breach_v2_NavMesh.asset` (nuevo) | No. No es una escena ni un asset de la lista roja; la losa puede usar `VO_Env_RustMetal.mat` por referencia, sin editarlo |
| M8 | **Desplazar la estación y el lobby fuera del muro de Room01**: mover `SouthWall` y `SouthWall_V35Ext` de Room01 a z -35,5 (pegados a `LobbyRear`) y recortar `GroundFloor` para que empiece en z -26, en lugar de mover el lobby persistente. Alternativa: mover `V5_MountStation` a z -31,5. Verificar en Play la secuencia de la estación durante la activación | **A** | B-M | M (encaje lobby–sala y NavMesh de Room01) | `Room01_Foundry_v2.unity` (+ NavMesh v2); o `RequiemCampaign_v2.unity` si se mueve la estación | No. La estación es una instancia de `Prefabs/Levels/MountStation_C04.prefab`, fuera de `Prefabs/Menu`. Confirmar que no comparte assets de la lista roja si se toca su prefab (mejor no tocarlo: mover solo la instancia) |
| M9 | **Sacar las columnas de las rampas de Room04**: mover `CeilingSupport` y `_V35Ext` de (±23; 129) a (±18,5; 129) o desplazar la rampa a z 113…125; rehornear el NavMesh | M | B | B | `Room04_Reactor_v2.unity` (+ NavMesh v2) | No |
| M10 | **Iluminar y orientar los Transfer** (concreta el M4 de v1): bajar la luz a y 13 o añadir 2 luces de pared de I ~3, alcance 8, a 3 m del suelo junto a la `DestinationDoor`; enlazar un `AirlockCycle` (ya existe el componente) en `Transfer_27/91/155` | M | B | B | `RequiemCampaign_v2.unity` | No (RequiemCampaign no está protegida). El ciclo podría reutilizar los clips del hangar solo por referencia |
| M11 | **Quitar la falsa salida de Room01**: apagar o cambiar de tono el panel de peligro del horno o los `MoltenRun` centrales (x ±6), y poner en el muro este un marco luminoso completo, como el de Room03, más un rótulo SALIDA; acercar el giro de los chevrones al este | M | B | B | `Room01_Foundry_v2.unity` | No. Si se cambia el material de `MoltenRun` o del panel, hacerlo con un material nuevo, no editando `IR_V35_*` compartidos |
| M12 | **Guía de salida en Room04**: un segundo trazo de chevrones por la derecha y una luz cian en el marco de salida (z 154,4) visible por encima del núcleo (y ≥ 14) | M | B | B | `Room04_Reactor_v2.unity` | No |
| M13 | **Final de campaña**: convertir el `ExitWall` de Pursuit05 en una puerta o esclusa con trigger de fin (WinScene ya está en Build Settings o volver al menú con un texto de cierre). Requiere un componente nuevo y no toca encuentros | A (cierre del bucle) | M | B | `Pursuit05_v2.unity`, nuevo `Scripts/Levels/CampaignEnd.cs` | No, salvo si "volver al menú" implica editar IntroMenu (no hace falta: basta con `SceneManager.LoadScene`) |
| M14 | **Pantallas del pasillo**: mover las `AlertScreen` y `ScreenFrame` de z -49,5 a -46 (entre las pilastras -50 y -42) | B | B | B | `Room00b_AlarmCorridor_v2.unity` | No. El pasillo usa materiales `HG_*` de la lista roja, pero mover transforms no los modifica |
| M15 | **Identidad por geometría** (complemento al M3 de v1, que trata materiales y luz): romper la simetría y la repetición. Ideas:<br>• Room01: pasarela este o puente sobre los hornos.<br>• Room02: mover coberturas y columnas fuera de las x/z de Room01, añadir vigas caídas con colisión sobre la grieta como segunda ruta.<br>• Room04: plataformas elevadas en el anillo. <br>No modificar slots ni composiciones; si una cobertura cae sobre un slot, mover la cobertura, no el slot | A | A | M (NavMesh, reachability de spawns: repetir `Reach` de MapFxAudit) | Copias `_v2` de Room01-04 + NavMesh v2 | No, salvo materiales compartidos (usar variantes o copias) |
| M16 | **Fuentes de spawn diegéticas** (presentación, no cambia encuentros): compuertas de suelo o montacargas ligados a los slots existentes de Room01-04, como los `DuctSpawn` del pasillo, para justificar las apariciones a la vista | M | M | M (roza el sistema de spawns: solo visual, sin tocar `SafeSpawn` ni las listas de slots) | Copias `_v2`; prefab nuevo `Prefabs/Levels/SpawnHatch.prefab` | No. Tiene un borde con "no cambiar encuentros", solo de presentación |
| M17 | **Borde del banco cercano de Room02**: añadir baranda o `GapWarningEdge` en z -26 si M7 no se aplica (mitigación mínima) | M | B | B | `Room02_Breach_v2.unity` | No |
| M18 | **Limpieza estructural**:<br>a) Sacar las 6 `*_MechTest` de Build Settings (no tienen punto de entrada en runtime; NavMesh incoherente). <br>b) Corregir la cámara overview de `MapFxAudit.cs` para que use los bounds de la geometría sin `VisualPass_VoidShell` (sección 0). <br>c) Documentar `Quarantine_v35` (inactiva y superpuesta a las coberturas v35: z-fighting inmediato si alguien la reactiva) | B | B | B (a toca ProjectSettings: respaldo previo) | `ProjectSettings/EditorBuildSettings.asset`, `Tests/PlayMode/MapFxAudit.cs` | No |
| M19 | **Pursuit**: ensanchar la ranura de las `SideCover` (moverlas a x ±5,5, dejando 1,5 m) o quitar la de z 317, que está junto a la última grieta | B | B | B | `Pursuit05_v2.unity` | No |

Orden sugerido: **M7 → M8 → M9** (errores de colisión: verificar primero en Play y luego corregir en la copia), después **M10 + M11 + M12** (legibilidad, baratos) y **M13** (cierre). M15 y M16 son de diseño mayor y conviene hacerlos después de M3 de v1.

---

## 5. Pendiente o no verificable desde aquí
- Reproducir en Play los hallazgos de colisión de M7, M8 y M9 (caída en el sur de Room02, muro sobre el jugador en la estación, columna en la rampa de Room04). Son deducciones de transforms y BoxCollider 1×1×1 escalados; no se probaron.
- Contenido de los `*_NavMesh.asset` (binarios): agentes por asset y conectividad real.
- `ChaseEntranceGate` con una separación de 3,98 m: no se explica por el transform.
- Hangar: solo se revisaron las raíces y los objetos fuera de la zona. Sus raíces no protegidas son `HangarSector/{CctvFeed, City, Lights, OverheadLamp, PodBase, PodRim, PodStrut, ScrapPoints, ScrapZones, SectorFx, SectorLamps, SectorShell}`. No se propone nada dentro de `Room00_Hangar.unity`; la guía hacia la salida va en `ConnectionChamber` (M10 o M4 de v1).
