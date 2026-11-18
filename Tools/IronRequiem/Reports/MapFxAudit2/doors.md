# Puertas: inventario para el rediseño (MapFxAudit2), 17/11/2026

Auditoría de solo lectura. Fuentes: código C#, YAML de `Assets/IronRequiem/Scenes/RequiemCampaign.unity` y de `Scenes/Campaign/*.unity`, leído con un parser propio (las posiciones son mundiales y compuestas desde la jerarquía), y el volcado de Play Mode `Tools/IronRequiem/Reports/MapFxAudit/doors/doors.txt` con sus 64 capturas `a{n}-{rol}-{puerta}-{fase}.png`, que generó `Tests/PlayMode/MapFxAudit.cs:362-394`.

Aviso de estado: `git status` marca **`RequiemCampaign.unity` y `CampaignFlow.cs` como modificados (M)**. Según AGENTS.md, no se debe guardar encima una escena que ya estaba modificada sin coordinarlo antes.

---

## 1. Contrato actual (código)

### Actores
| Pieza | Archivo | Papel |
|---|---|---|
| `CampaignFlow` | `Scripts/Levels/CampaignFlow.cs` (en `CampaignSession`, fid 1644691800) | **Es el único que manda**. Decide cuándo se abre y se cierra cada puerta de esclusa. |
| `CampaignAirlock` | `Scripts/Levels/CampaignAirlock.cs` (guid f81fdd603961ac04eb15a6ec5071f108) | Es un volumen trigger con un Rigidbody cinemático. Solo guarda las referencias `sourceDoor`, `destinationDoor`, `extraDestinationDoors[]` y `cycle`. En `OnTriggerEnter/Stay` llama a `flow.TryTransition(sourceStage)` (l.28) y registra `lastContact`. `PlayerInside` es `Time.time-lastContact<=.25f` (l.23). |
| `AirlockDoorMotion` | `Scripts/VisualPass/AirlockDoorMotion.cs` (clase estática, guid 977635b3753cbae4385a727f2d55a753) | Mueve la puerta y gestiona `SetActive`. No es un componente. Las corrutinas se ejecutan en el host (`CampaignFlow`) porque la puerta se desactiva. |
| `AirlockCycle` | `Scripts/Levels/AirlockCycle.cs` (guid 4347cad4130852746a9c200a82c036f1) | Es solo presentación: luces, tiras, vapor y audio. **Solo existe en la esclusa 0.** |
| `CorridorAlarm` | `Scripts/Levels/CorridorAlarm.cs` (en Room00b) | **No toca ninguna puerta.** Observa `CombatTwoRoomsLab.WaveIndex/Completed`. Con la primera oleada activa la sirena, las balizas y las luces rojas y muestra "ALERTA · INTRUSIÓN DETECTADA". Al completar la sala apaga todo y las pantallas muestran "ZONA DESPEJADA · ESCLUSA ABIERTA" en verde (l.19, l.70-76). Ese mismo frame `CampaignRoom.Completed` pasa a true y `CampaignFlow.Update` abre `LobbyRear`. |

### Significado del estado
- **Cerrada** = `activeSelf==true` + pose cerrada + BoxCollider sólido (no trigger).
- **Abierta** = `activeSelf==false`, devuelta a la pose cerrada (`Run`, l.155: `door.SetActive(false); t.position = shut;`). Una puerta abierta no tiene colisión ni render y tampoco tiene hijos activos.
- **Cerrándose** = activa y con colisión **desde el primer frame**. `Set` llama a `StartCoroutine(Run)`, que ejecuta hasta el primer `yield` de forma síncrona: si la puerta estaba inactiva hace `t.position=open; SetActive(true)` (l.146).
- **Abriéndose** = activa y con colisión mientras sube. Solo se desactiva al terminar.
- `IsClosed(door)` (l.128-133) es true si `!door` **o** si no hay corrutina en curso, `activeSelf`, y la posición está a menos de 1 mm de `closedWorld[door]`. Si la puerta nunca pasó por `Set` y está activa, también devuelve true.
- `closedWorld[door]` se captura de forma perezosa en el primer `Set` (l.115). Por eso **la puerta debe estar en su pose cerrada la primera vez que se llama a `Set`**.
- Los diccionarios estáticos `closedWorld/running` se limpian en `SubsystemRegistration` (l.110).

### Movimiento
- `OpenSeconds=.7f` con smoothstep `k*k*(3-2k)` y `CloseSeconds=.45f` con `k*k`, una curva ease-in que acelera hacia el suelo (efecto de golpe) (l.107, 148, 154).
- La traslación es solo vertical: `open = shut + up * Height`. `Height = GetComponent<Renderer>().bounds.size.y + .1f` del **root**, y si el root no tiene Renderer vale **4 m** (l.135-139).
- Reversión a mitad de recorrido: `StopCoroutine` y arranque desde la posición actual (l.116-121). Si ya va en esa dirección no hace nada. Si `host` está inactivo o no se está en Play, hace un snap inmediato (l.123).
- Comprobado en Play (doors.txt): ChamberHangarDoor, base y=2.40 → 3.86 a 0.25 s → 6.39 a 0.5 s → abierta inactiva con y=2.40. Cerrándose a 0.2 s está en y=6.26. En las 13 puertas: `contract: open ends inactive=True closing collider on first frame=True`.

### Cuándo se abre o se cierra cada puerta
1. **Apertura de salida** (`CampaignFlow.Update`, l.93-97): cada frame, si `!Busy`, la sala actual está `Completed`, el menú está en juego y el jugador vivo, se llama a `Set(airlocks[CurrentStage].SourceDoor, false)`. Es idempotente. La etapa 6 (Pursuit05) no tiene esclusa.
2. **Entrada en la esclusa** (`TryTransition`, l.98-102): exige `!Busy`, `source==CurrentStage`, sala `Completed`, jugando, vivo y `timeScale>0`. Entonces arranca `Prepare(source+1)`.
3. **`Prepare`** (l.179-276):
   - l.202: `Set(SourceDoor, true)` (cierra detrás del jugador) y `Cycle.Begin()` si existe.
   - l.206: espera **mientras** `!IsClosed(SourceDoor) || !PlayerInside || load.progress<.9 || t<environmentBlendSeconds(1.6 s)`. Así **nunca se activa la etapa siguiente ni se descarga la anterior sin la puerta de origen exactamente cerrada** y con el jugador dentro del volumen.
   - **Abort** (l.211-216): con la puerta ya cerrada y el jugador **fuera del trigger más de 1 s**, la reabre (`Set(SourceDoor,false)`), llama a `Cycle.Abort()` (verde, sin sonido), restaura fog y post, deja que la carga en curso termine y descarga esa escena, y luego `Busy=false`. Después el jugador puede volver a entrar.
   - Después: activa la etapa, desmonta la antigua por tramos, `WarmRoom` y como mínimo 1.5 s (`minimumPreparationSeconds`) desde el fin de la carga.
   - l.266-272: `target==0` abre `initialDoor`, que es **null** en la escena (fid 0). La reasignó `HangarTransitAuthoring.cs:243` y por eso no hace nada. En otro caso abre `airlocks[target-1].DestinationDoor`, todas sus `ExtraDestinationDoors` a la vez y `Cycle.Complete()`.
4. **Después de la transición** la puerta de origen queda **cerrada para siempre**: no hay vuelta atrás y la escena anterior está descargada. La de destino queda abierta (inactiva) y nunca se vuelve a cerrar.
5. Tiempo mínimo con las dos puertas cerradas: 0.45 s de cierre, luego un máximo entre carga ≥0.9 y 1.6 s de mezcla, luego activación, desmontaje, warm y 1.5 s. En total unos 3 s o más antes de los 0.7 s de apertura.

### Otras puertas (fuera de AirlockDoorMotion)
- `Pursuit05.unity` → `ChaseCorridorEncounter` (`Scripts/Levels/ChaseCorridorEncounter.cs:63,75,85`). `ChaseEntranceGate` (fid 2019520163, (0,12,185), 16×8×0.6, `activeSelf=0`) **aparece de golpe** con `SetActive(true)` al empezar la persecución. `ChaseExitGate` (fid 1710111611, (0,12,373)) **desaparece de golpe** al llegar a `finishZ`. Material VO_Env_Hazard, malla `Art/Levels/ChaseCorridor/CorridorCube_8.asset`. No tienen animación ni sonido.
- `ChaseSceneEntrance.sealedDoor` y `CombatTwoRoomsLab.gate` (`SetActive(false)` de golpe): en las salas de campaña `gate: {fileID: 0}` (Room00b, Room01-04). Solo se usan en los labs (CombatTwoFloorsLab, etc.), **no en la campaña**.
- Room01-04 no tienen persianas propias. Solo tienen marcos estáticos: `DoorHeader_V35` (CP_Wall, sobre el hueco, p. ej. Room03 (0,18.5,91) 10×5×0.94) y `ExitFrameGlow` (IR_V35_FloorGuide, 0.55 m delante del plano de la puerta), generados por `Editor/LevelOpenPass.cs:205,450-455`. **Siempre están encendidos**, también con la puerta bloqueada.

---

## 2. Instancias (RequiemCampaign.unity → `PersistentAirlocks`)

Etapas: 0 Room00_Hangar, 1 Room00b_AlarmCorridor, 2 Room01_Foundry, 3 Room02_Breach, 4 Room03_Assembly, 5 Room04_Reactor, 6 Pursuit05. `prologueStages: 2`, así que Room01 se rotula "SECTOR 1".

Todas las puertas: `activeSelf=1` en YAML, capa Default, no estáticas, **sin Rigidbody** (son colliders estáticos movidos por transform), **sin NavMeshObstacle** (hay 0 componentes 208 en todas las escenas de campaña), **sin AudioSource propia** y sin scripts.

| # | Esclusa (trigger) | Rol | GameObject (fid, línea YAML) | Pos. mundo / escala | Malla | Material | Collider |
|---|---|---|---|---|---|---|---|
| 0 | `HangarTransit/ConnectionChamber/ChamberAirlock` (0,2,-79.7), trigger 7×4×5.8. Room00 → Room00b | source | `.../ChamberHangarDoor` (153797872, l.8248) | (0,2.4,-84) 8.4×4.8×0.6 | builtin Cube | **HG_Hazard** 478be5ff… (URP Lit) | Box 1³ sólido |
| 0 | | destination | `.../ChamberCorridorDoor` (1191339563, l.37332) + hijos DoorRight, DoorBelow, DoorAbove, Porthole (HG_Glass), 4×PortholeBar y PortholeCross (HG_Trim, sin collider) | root (-2.3,2.4,-76) 3.8×4.8×0.6, DoorRight en x=+2.3 | builtin Cube | HG_Hazard, HG_Glass ce30fe24…, HG_Trim 8c726fd0… | 5 Box sólidos (root, Right, Below, Above y Porthole) |
| 0 | | extra | `.../VestibuleShutter` (1144660081, l.36531) + `ShutterHazard` | (0,2.4,-70.25) 8.4×4.8×0.5 | builtin Cube | **HG_PlateDark** 98ee78a2… + franja HG_Hazard | Box sólido |
| 1 | `StartLobby/HangarAirlock` (0,2,-30.2), trigger 10×4×5. Room00b → Room01 | source | `StartLobby/LobbyRear` (950688888, l.23762) | (0,3.5,-35) 14×7×1 | `Art/Levels/Campaign/CampaignCube_23.asset` eaa5e4ad… | **VO_Env_Wall** a4e1dc73… (IronRequiem/HardSurface). **Es una pared lisa, no parece una puerta.** | Box sólido |
| 1 | | destination | `StartLobby/InitialLoadingDoor` (1127723920, l.36235) | (0,3.5,-26) 12×7×0.6 | CampaignCube_24 ee3717de… | VO_Env_Hazard aa53d937… | Box sólido |
| 2 | `StairAirlock` (31,2,25), trigger 5×4×6. Room01 → Room02 | source | `Room01ExitDoor` (1363093716, l.39934) | (27,3.75,25) 1×7.5×12, plano YZ | CampaignCube_14 3e85e73f… | VO_Env_Hazard | Box sólido |
| 2 | | destination | `Room02EntryDoor` (129616278, l.7506) | (27,11.75,-10) 1×7.5×8 | CampaignCube_15 05b27fbb… | VO_Env_Hazard | Box sólido |
| 3 | `TransferAirlock_27/Transfer` (0,10,33), trigger 8×4×6. Room02 → Room03 | source / destination | `SourceDoor` (65374779) / `DestinationDoor` (791764267) | (0,12,27) / (0,12,39) 10×8×0.6 | CampaignCube_19 b6ba2c3e… | VO_Env_Hazard | Box sólido |
| 4 | `TransferAirlock_91/Transfer` (0,10,97). Room03 → Room04 | source / destination | `SourceDoor` (110839621) / `DestinationDoor` (76178156) | z=91 / 103, misma forma | CampaignCube_19 | VO_Env_Hazard | Box |
| 5 | `TransferAirlock_155/Transfer` (0,10,161). Room04 → Pursuit05 | source / destination | `SourceDoor` (144796037) / `DestinationDoor` (713148219) | z=155 / 167 | CampaignCube_19 | VO_Env_Hazard | Box |

Rutas completas: `PersistentAirlocks/...`. VO_Env_Hazard usa el shader `IronRequiemHardSurface.shader` (20aba464…). Las mallas `CampaignCube_N` son cubos con UV escaladas por tamaño (`Editor/CampaignAuthoring.cs:47`) y solo las usan `RequiemCampaign.unity` y **`RequiemCampaign_MechTest.unity`**, una copia con las mismas puertas que el rediseño debe tener en cuenta o excluir de forma explícita.

**Umbrales**: `VisualPass_DoorSill` (VO_Env_RustMetal, 0.18 m, BoxCollider, sin sombras) bajo InitialLoadingDoor, Room01ExitDoor ((27,-0.1,25) 3×0.18×12.4), Room02EntryDoor ((27,7.9,-10)) y los 6 Source/DestinationDoor de las Transfer. **No hay umbral en LobbyRear ni en las puertas de la cámara.**

**Trigger y puerta de origen (hueco)**: cámara 1.1 m (cara de la puerta en -83.7, trigger desde -82.6), lobby 1.8 m, escalera 1.0 m (puerta x 26.5-27.5, trigger desde x 28.5) y Transfer 2.7 m.

**NavMesh**: la sesión no tiene NavMeshSurface (`CampaignAuthoring.RemoveNav`, l.40). Cada sala hornea su propio NavMesh solo con los colliders de su escena (`CampaignAuthoring.Bake`, l.58-63: agentRadius 2.25, voxel 0.12). Por eso **el estado de la puerta no afecta al NavMesh** y no hay carving. Los enemigos no entran en la esclusa porque no hay NavMesh dentro, no porque la puerta los bloquee: los NavMeshAgent no colisionan con BoxColliders. `map.txt` informa `offNavMesh=0` y `navWithoutFloorCollider=0` en todas las salas. No pude verificar en el YAML si algún polígono del NavMesh de una sala llega al plano de la puerta (los datos son binarios).

**Luces y sonido de la esclusa 0** (`ConnectionChamber`, AirlockCycle fid 649514720):
- `lights` = 2 `CycleLamp` (Point, (0,4.4,-82) y (0,4.4,-78), intensidad 2.5, rango 7).
- `lamps` = 2 `CycleStrip` (HG_CyanGlow 1aca80bd…, recoloreadas con un MPB `_BaseColor`).
- `steam` = 2 `CycleSteam` (HG_FxSmoke).
- `audioSource` = `CycleAudio` (0,2,-80) con spatial 0.6 y mixer General.
- `cycleClip` = `Audio/SFX/HangarIntro/IR_Airlock_Cycle.wav` (6c97ddd2…, `PlayOneShot` .9 en Begin) y `doneClip` = `IR_Airlock_Done.wav` (9c990d16…, .8 en Complete).
- Colores: ámbar (1,.55,.12) con HDR (2,1,.2), parpadeo de 2.2 Hz con un 55 % encendido (k 1 / .15); verde (.25,1,.45) con HDR (.4,2,.7). **No hay estado rojo.**
- En el vestíbulo hay 4 `DeadAlarmLamp` (HG_RedGlow 98da280c…, con MPB atenuado (.18,.02,.02)). Son decorado estático y nadie las controla.
- Además: `VestibuleDim` (0.8, azulada) y los textos TMP `TransitSign` "TRÁNSITO C-04 → PUENTE NORTE" y `TransitSignBack` "HANGAR C-04".

**Esclusas 1-5**: no tienen AirlockCycle (`cycle: {fileID: 0}`). Solo un `MountedCeilingFixture/CeilingLight` frío ((.8,.9,1), 4.4) en el techo de la Transfer y del lobby y 3 en la escalera. **No tienen ningún sonido.**

### Herramientas que las generaron (reproducibilidad)
| Objetos | Generador | Notas |
|---|---|---|
| PersistentAirlocks, StairAirlock, Room01Exit/Room02Entry, TransferAirlock_27/91/155, StartLobby (LobbyRear, InitialLoadingDoor) | `Editor/CampaignAuthoring.cs` `BuildSession()` l.197-221. No tiene MenuItem: se lanza con la cola `Tools/IronRequiem/Requests/campaign.txt` (Tick, l.29-35) | **Reconstruye la sesión entera copiándola desde CombatTwoFloorsLab.** Pisaría todo. |
| Bunker_* → VO_Env_* | `Editor/CampaignCurrentAssets.cs:100-110` (op `upgrade`) | Remapeo de materiales. |
| VisualPass_DoorSill | `Editor/VisualPass/VisualPassF2.cs:203-229`, menú *Tools/Iron Requiem/Visual Pass/F2 D1 - Door sills + transition lighting* | Borra y recrea los umbrales **según el nombre de la puerta y los bounds del Renderer del root**. |
| HangarTransit/ConnectionChamber (3 puertas, cycle, trigger), recableado de `airlocks[]`, `stageScenes`, `initialDoor=null` y HangarAirlock | `Editor/HangarIntro/HangarTransitAuthoring.cs` `IntegrateCampaign` l.208-252, `BuildChamber` l.255-318 y `PortholeDoor` l.323-337 | **Es un script PROTEGIDO** (lista "nadie toca" de guardian.md §3). Si se vuelve a ejecutar destruye `HangarTransit` y recablea el flow. `CombatV4Authoring.ApplyToSession` (también protegido) toca `StartLobby`. |
| DoorHeader_V35 y ExitFrameGlow (en las salas) | `Editor/LevelOpenPass.cs`, menú *Levels/v35 Open + dress rooms* | Escribe escenas de sala. |

---

## 3. Señales de estado actuales (lo que ve el jugador)

| Estado | Esclusa 0 (cámara) | Esclusas 1-5 |
|---|---|---|
| **Bloqueada** (sala sin completar) | Losa HG_Hazard naranja y negra con el rótulo "TRÁNSITO C-04 · PUENTE NORTE" (captura `a0-source-...-1-closed`). Las tiras CycleStrip y las luces están en **verde** desde `Awake` (`AirlockCycle.cs:58`), así que **la luz dice "adelante" con la puerta bloqueada**. Sin sonido. | Losa amarilla y negra plana (VO_Env_Hazard). **LobbyRear es una pared de hormigón sin ninguna señal.** En la sala, ExitFrameGlow encendido igual. No hay luz de estado, icono ni sonido. |
| **Desbloqueada / abriéndose** | La losa sube en 0.7 s a través del techo (captura `-2-opening-0.5s`: se ve el borde inferior en el dintel). Sin sonido. HUD: "… COMPLETADO · ACCESO ABIERTO" (`CombatTwoRoomsLab.cs:66`) o el título del prólogo. | Igual: sube y **desaparece**. Sin sonido. En Room00b, la pantalla de CorridorAlarm cambia a "ZONA DESPEJADA · ESCLUSA ABIERTA" en verde. |
| **Abierta** | No hay puerta. El hueco queda libre y no hay hoja visible en ningún alojamiento. | Igual. |
| **Ciclo (cerrándose detrás + carga)** | Las luces parpadean en ámbar, sale vapor y suena `IR_Airlock_Cycle`. La captura `a0-...-1b-cycle-amber`, tomada desde fuera, no muestra el ámbar porque las luces están dentro de la cámara. | Solo la losa que baja en 0.45 s. **No hay luz, vapor ni sonido.** HUD "PREPARANDO SECTOR N…" durante la activación. Las capturas `r02/r03/r04-*-airlock-inside` miden lum 0.011-0.040 (89-97 % oscuro): **dentro casi negro.** |
| **Llegada** | `IR_Airlock_Done` y luz verde fija. Se abren a la vez ChamberCorridorDoor y VestibuleShutter. | La puerta de destino sube y el HUD muestra "SECTOR N LISTO · TRES RONDAS". |
| **Alarma** | No hay estado de alarma de puerta. Las DeadAlarmLamp rojas están siempre apagadas. | La alarma es de sala (CorridorAlarm en Room00b: balizas, sirena y texto) y no tiene relación con la puerta. |
| **Abort** | Ámbar → verde sin sonido y la puerta vuelve a subir. | La puerta vuelve a subir en silencio. |

---

## 4. Problemas

**Visuales**
1. Todas las puertas son **un cubo plano que sube en vertical y atraviesa el techo**: no hay alojamiento, guía, marco en el lado de la esclusa, cerrojos ni juntas. Una puerta abierta simplemente **desaparece** (`SetActive(false)`).
2. Intersecciones calculadas a partir de las posiciones:
   - Transfer: la losa (y 8-16) sube 8.1 m, hasta y 16.1-24.1, a través de `AirlockCeiling` (y 15.95-16.45). En la sala, el `DoorHeader_V35` de Room03/04 cubre y 16-21, así que **de y 21 a 24.1 la losa asoma por encima del dintel**.
   - `Room01ExitDoor` (x 26.5-27.5) se desliza **coplanaria** con `UpperInnerStairGuard` (x 26.5-27.5, y 8-16), con riesgo de z-fighting durante el recorrido.
   - `Room02EntryDoor` sube hasta y 23.1 y supera `StairRoof` (techo en 16.45).
   - `LobbyRear` mide 14 m de ancho bajo un `LobbyRoof` de 12 m y sobresale por los lados.
   - Las capturas a1, a2 y a3 (salas sin cargar) muestran las losas flotando por encima del tejado.
3. En la cámara, las HG_* usan el Cube builtin con escala por objeto, así que las franjas se estiran de forma distinta en cada hoja. En `ChamberCorridorDoor`, DoorBelow y DoorAbove (0.8 m) tienen las franjas comprimidas: se ve en `r00-airlock-cycle.png` como una columna central de rayas densas.
4. `LobbyRear` es una pared (VO_Env_Wall) que funciona como puerta y no da ninguna pista de que lo sea. No tiene umbral.
5. La señal de estado se contradice: verde en reposo con la puerta bloqueada, y `ExitFrameGlow` siempre encendido.
6. Las Transfer están casi a oscuras por dentro durante la espera de unos 3 s.

**De juego**
1. Las puertas son **colliders estáticos movidos por transform** (sin Rigidbody cinemático). Al cerrarse no empujan al CharacterController. Si el jugador retrocede al hueco (de 1.0 a 2.7 m entre la cara y el trigger), la losa puede bajar **sobre él**. No hay comprobación de obstrucción. El Abort solo se evalúa con la puerta ya cerrada y el jugador fuera del trigger durante 1 s. Con la holgura de 2.7 m de las Transfer, un jugador que está **dentro de la esclusa pero fuera del trigger** provoca un Abort.
2. **Jugador atrapado**: si `Prepare` sale por `yield break` con error ("ERROR · FASE NO DISPONIBLE/INCOMPLETA", l.185 y l.244), `Busy` queda en true para siempre con las dos puertas cerradas. `Prepare` tampoco comprueba la muerte del jugador durante la espera.
3. NavMesh: no hay obstáculo ni carving. La contención depende de que no haya NavMesh en la sesión y de la erosión de 2.25 m en las salas. Los proyectiles atraviesan una puerta abierta (es lo esperado) y una cerrada los bloquea con el BoxCollider.
4. Pursuit05: las puertas aparecen y desaparecen de golpe (`ChaseEntranceGate`/`ChaseExitGate`). Al activarse, la de entrada puede aparecer encima del jugador.

**De sonido**
1. Ninguna puerta tiene sonido de movimiento, golpe de cierre, cerrojo o "bloqueada". Solo la esclusa 0 tiene sonido, y es del ciclo (`IR_Airlock_Cycle/Done` en Begin y Complete), no de la puerta.
2. Las esclusas 1-5 y las puertas de la persecución están en silencio total. El Abort también es silencioso.

---

## 5. Riesgos y puntos de enganche para el rediseño

**Contrato que no se puede romper** (lo comprueban `Tests/PlayMode/CampaignTests.cs:57,62,78,190-193` y `MapFxAudit.cs:387-394`):
- `SourceDoor/DestinationDoor.activeSelf == false` ⇔ abierta. Los tests y `CampaignTourBenchmark.cs:175` esperan a `!activeSelf`.
- Al cerrar, debe haber un collider sólido activo desde el frame de `Set(...,true)`. El test usa `GetComponentsInChildren<Collider>()` con `enabled && !isTrigger`.
- `IsClosed` debe ser exacto: sin corrutina, activa y con el root en `closedWorld`. **Toda animación visual tiene que haber terminado cuando `IsClosed` devuelve true.** Si no, la sala se activa o descarga con una hoja todavía en movimiento y se puede ver el vacío.

**Enganche propuesto (sin cambiar el contrato)**
- En `AirlockDoorMotion.Run`, si la puerta tiene un `DoorPresentation` registrado, delegar en él la pose visual con el mismo `k` (`Evaluate(k, closing)`), además de `OnBegin(closing)` y `OnEnd(closing)` (sonido de golpe o cerrojo). En ese caso **no se mueve el root**: queda en `shut`, de modo que `IsClosed` sigue siendo trivialmente exacto, y la desactivación final se mantiene igual. Si no hay presentación, se conserva el comportamiento actual.
- **El root se desactiva al abrirse**, así que todo lo que deba verse con la puerta abierta (marco, alojamiento, luces de estado, cerrojos que se retraen hacia el marco) y el propio componente que haga `Update` u oiga eventos **no puede ser hijo de la puerta**. Debe ser un hermano siempre activo (p. ej. `DoorRig` junto a la puerta, con referencia a ella) o vivir en el `CampaignAirlock`. Las hojas sí pueden ser hijas del root.
- `Height()` usa el Renderer del root. Si el root nuevo es un vacío, el recorrido cae a **4 m**. Hay que mantener un Renderer en el root o hacer que la presentación aporte el recorrido.
- Las señales de estado (bloqueada, desbloqueada, ciclo, llegada, abort) se pueden derivar sin tocar `CampaignFlow` observando `CurrentStage`, `CurrentRoom.Completed`, `Busy` e `IsClosed`, o con eventos opcionales añadidos en los mismos puntos (l.96, 202, 213, 269-271). Generalizar `AirlockCycle` a las 6 esclusas es el camino natural: ya tiene `Begin/Complete/Abort`. Falta un estado "bloqueado" (no existe el rojo en `AirlockCycle`).
- Para obstrucciones: una comprobación de solape con el jugador antes de cerrar del todo encaja dentro de `Run` y no altera el contrato, siempre que la puerta siga activa y con collider desde el primer frame.

**Riesgos**
- **Zona protegida / lista roja**:
  - La esclusa 0 entera la generó `HangarTransitAuthoring.cs` (**script protegido**): no hay que volver a ejecutarlo ni editarlo. Hace falta una herramienta nueva e idempotente.
  - Sus materiales **HG_Hazard, HG_PlateDark, HG_Glass, HG_Trim, HG_CyanGlow, HG_RedGlow, HG_Floor** y `VO_Env_RustMetal.mat` (umbrales) están en la **lista roja** (guardian.md §1b): copiar, no editar.
  - `VO_Env_Hazard.mat` no está en la lista roja, pero tiene 49 usos en la sesión (escalones, etc.) y aparece en Room01-04 y Pursuit05. Su shader `IronRequiemHardSurface` sí está en la lista roja. Por tanto: material nuevo, sin editar ninguno de los dos.
  - `VO_Env_Wall` e `IR_Airlock_*.wav` no aparecen en la instantánea protegida.
  - El marco del lado del hangar (`HangarSector/SectorShell/ExitPost`, `ExitLintel` y `SectorLamps/ExitGlow`) está en **Room00_Hangar.unity**, una escena protegida. SectorShell queda "fuera de zona", pero solo se puede tocar en una copia, nunca en el original. ExitGlow está en `SectorLamps` y puede llevar MenuFlicker.
  - `ChamberHangarDoor` se ve desde el hangar. Room00 tiene `Shot_C_Exit/C_Exit_From/To` en `WakeUpCinematic`, así que **cambiar su aspecto probablemente cambie un plano de la cinemática protegida**. El YAML no permite determinar si ese plano la encuadra: hay que verificarlo en Play.
- **Pisado por regeneración**: volver a ejecutar `CampaignAuthoring` (op build), `HangarTransitAuthoring.IntegrateCampaign` o `VisualPassF2 D1` borra o recoloca puertas y umbrales. Si se sustituyen las puertas, hay que recablear `sourceDoor`, `destinationDoor` y `extraDestinationDoors` por SerializedObject. `VisualPassF2` y `LevelAudit.cs:141` usan los nombres `SourceDoor`/`DestinationDoor`/`Room01ExitDoor`/`Room02EntryDoor`/`InitialLoadingDoor`.
- `closedWorld` es estático y por GameObject. Si el rediseño mueve el root en el Editor o en runtime fuera de `AirlockDoorMotion` después del primer `Set`, `IsClosed` no vuelve a ser true nunca y el jugador queda atrapado.
- `RequiemCampaign.unity` ya está modificada (git M). `RequiemCampaign_MechTest.unity` contiene puertas duplicadas.
- Geometría con poco margen: la Transfer tiene 0.95 m entre la parte superior de la losa y el techo; la cámara tiene un techo de 0.5 m sobre el hueco de 4.8 m. Las hojas laterales o partidas necesitan alojamiento en las paredes (paredes de 1 m en x ±6.5 en la Transfer, ±4.5 en la cámara).
- Perf: las puertas no son estáticas y mover colliders estáticos tiene coste en PhysX. Con 13 puertas es despreciable, pero si las hojas nuevas añaden muchos colliders conviene un único collider de bloqueo en el root.
