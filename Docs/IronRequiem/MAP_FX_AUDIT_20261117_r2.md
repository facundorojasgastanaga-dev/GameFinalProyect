# MAP / FX AUDIT, revisión 2: zona protegida e inventario de puertas (17/11/2026)

FASE 1 del brief "zona protegida + puertas". Solo lectura más Play Mode sin guardar. Esta revisión **amplía y corrige** la auditoría del mismo día ([MAP_FX_AUDIT_20261117.md](MAP_FX_AUDIT_20261117.md), "v1"), que se conserva. Desde la v1 ya se aplicó el lote aprobado M1+E1+E2+B1, así que las capturas de las salas son posteriores a ese lote.

**Anexos detallados** (una fuente por dominio, todos de solo lectura):

| Anexo | Contenido |
|---|---|
| [guardian.md](../../Tools/IronRequiem/Reports/ProtectedZone/guardian.md) | Zona protegida: dependencias, lista roja, impacto del lote ya aplicado |
| [map-structure.md](../../Tools/IronRequiem/Reports/MapFxAudit2/map-structure.md) | Escenas, flujo, rondas, NavMesh, revisión sala a sala, M7–M19 |
| [effects.md](../../Tools/IronRequiem/Reports/MapFxAudit2/effects.md) | Ficha por efecto, F1–F10, E7–E14, P-FX1–6 |
| [render-perf.md](../../Tools/IronRequiem/Reports/MapFxAudit2/render-perf.md) | URP, Volumes por sala, IR-03, luces, protocolo IR-04, R6–R10, B4–B6, P-E2a |
| [doors.md](../../Tools/IronRequiem/Reports/MapFxAudit2/doors.md) | Contrato de puertas, 13 instancias, señales y problemas, enganche del rediseño |

## 0. Cómo se hizo

- **Guardián primero.** `Editor/MapFx/IronCampaignProtectedZone.cs` (nuevo, solo lectura) guarda el SHA-256 de los archivos protegidos y de todas las dependencias recursivas (`AssetDatabase.GetDependencies`) de IntroMenu y Room00_Hangar. Base: `Tools/IronRequiem/Reports/ProtectedZone/before-20261117.txt`, con 443 filas.
- **Capturas de referencia del menú:** `Tests/PlayMode/ProtectedMenuReference.cs` (nuevo). Recoge la cinta en movimiento (0 / +0,5 / +1 s), la explosión por **clic real** sobre un walker (+4/+16/+46 frames) y la secuencia de JUGAR (0,5/1,5/2,5/4 s). Salida en `ProtectedZone/menu-<etiqueta>/`.
- **Mapa y efectos:** se reutilizan las capturas y mediciones de la v1, tomadas hoy tras el lote, en `Tools/IronRequiem/Reports/MapFxAudit/`. Hay dos tests nuevos en `MapFxAudit.cs`:
  - `E_DoorStatesAndInventory`: 64 capturas, cada puerta en sus 4 estados, con el contrato comprobado.
  - `F_CollisionSuspects`: verifica físicamente con raycasts y bounds tres sospechas que salieron de la revisión estática.
- **Rendimiento:** build Release `Tools/IronRequiem/Builds/VisualOverhaul/mapfx-release/` medida con `CampaignTourBenchmark` (`-irTour -irFullHD`).
- **Subagentes:** 4 de solo lectura (mapa, efectos, render, puertas), que solo escribieron sus anexos. El Editor lo manejó solo el coordinador. No se guardó ninguna escena y no hay commits.

## 1. Resultado más importante: la zona protegida ya tiene un efecto en runtime del lote anterior

| Elemento del lote ya aplicado | Veredicto |
|---|---|
| VolumeFadeIn (M1), RequiemSettings (B1), 4 prefabs de campaña y `RobotBodyBurst_CombatPolish_IR` (E1), ProjectSettings | **Sin efecto** en IntroMenu ni en el contenido de Room00. GUID con 0 apariciones; MenuWalker_* y MenuExplosionFx no usan RobotBodyBurst. B1 solo cambia el modo de la ventana en la build. |
| **ZoneEnvironment (E2)** | **Efecto en runtime en Room00 dentro de la campaña.** Al cargar la etapa 0, `ZoneEnvironment.Bind` (l.78-80) cambia `HangarSector/HG_PostVolume` de HG_MenuVolume a HG_GameplayVolume **antes** de la cinemática: CA 0,12→0, viñeta 0,38→0,2, grano 0,28→0,1 y bloom 0,6→0,25. Cambia el aspecto de la cinta, la pasarela y la cinemática, no su comportamiento. No se guarda en ningún asset. IntroMenu no se ve afectada. |

**Propuesta P-E2a (prioridad 1, necesita tu OK):** quitar ese cambio de `Bind` y, solo cuando `HangarIntroCinematic.Finished` sea true (se lee sin modificar la clase protegida), añadir en runtime un Volume nuevo con HG_GameplayVolume, prioridad 21 y peso 0→1 en unos 1,5 s. `HG_PostVolume` no se toca nunca. Además, cambiar la coincidencia por nombre ("HG_MenuVolume") por escena + objeto. Así la cinemática vuelve a verse como el original y el hangar jugable conserva E2.

Decide tú si la protección cubre también el aspecto del hangar **jugable** después de la cinemática. Si lo cubre, la opción es P-E2c: revertir E2 en Room00.

## 2. Prueba de la zona protegida (ahora, tras la fase 1)

- **Hashes** (`ProtectedZone/compare-before-20261117-after-20261117.txt`): **1 diferencia de 443**, `TextMesh Pro/.../LiberationSans SDF - Fallback.asset`. Es el atlas **dinámico** de TMP, que Unity reescribe al renderizar texto en Play Mode; ya estaba modificado en git al empezar la sesión. Todo lo demás coincide: escenas, scripts de Menu y HangarIntro, MenuWalker_*, materiales, atlas T07Mech y perfiles.
- **Capturas del menú** (`menu-base-a`, `menu-base-b`, `menu-after`, `menu-after-2`): no se puede demostrar igualdad de píxeles con este arnés. `HangarLaunchSequence` y `MenuFlicker` avanzan con `Time.unscaledDeltaTime`, es decir, tiempo real, así que dos ejecuciones **sin ningún cambio** ya difieren:

  | Comparación (diferencia media de luminancia, 0–255) | Menú | Cinta | Explosión | JUGAR 1,5 s |
  |---|---|---|---|---|
  | base-a frente a base-b | 1,0 | 1,0–1,9 | 0,8–1,4 | 7,5 |
  | after frente a after-2 (los dos con el proyecto sin cambios) | 4,7 | 5,2 | 1,7–3,8 | 18,9 |
  | base-a frente a after | 6,1 | 1,3–1,8 | 3,3–5,3 | 25,0 |

  La diferencia antes/después está dentro de la variación entre dos ejecuciones idénticas: luz de la cápsula que parpadea y avance de la cámara de JUGAR según la duración real de los frames. Las imágenes coinciden en todo lo demás: composición, cinta, walkers, explosión y UI. Hacerlas deterministas exigiría tocar scripts protegidos, así que **la prueba válida son los hashes**, con las capturas como referencia visual. `Tools/IronRequiem/ProtectedZone/compare_menu.ps1` repite la comparación.
- **Lista roja** (assets compartidos con IntroMenu/Room00 que las salas también usan; para modificarlos hay que hacer una copia): el shader IronRequiem/HardSurface, VO_Env_RustMetal.mat, Bunker_RustMetal_Albedo/Normal, VO_Noise, los materiales HG_* (Plate, PlateDark, Trim, Hazard, Hose, RedGlow, Floor, City, Glass, Fx*), T07Mech_Atlas (.mat y .png), Carronero.prefab y la cadena SawBot/HoverBot, RobotBodyBurst_CombatPolish (base de `_IR`), VP_VFX_HoverBot_Explosion (extraVfx del menú), CannonExplosion_IR, T07_BodyExplosion, VFX/CannonExplosion/* y VFX/Mechanics/*. **Libres:** CombatClarity.asset, Bunker_*.mat y Campaign_PostProfile.
- **Riesgo anotado:** volver a ejecutar `HangarSectorAuthoring.cs` o `HangarTransitAuthoring.cs` (Editor/HangarIntro, protegidos) regenera partes de Room00 o de la esclusa 0. Ninguna propuesta los ejecuta.

## 3. MAPA, sala por sala (resumen; detalle y capturas en v1 y map-structure.md)

Capturas: `Tools/IronRequiem/Reports/MapFxAudit/r0x-<sala>-{pano-0/90/180/270, route, airlock-*}.png`.

**Corrección a la v1:** las vistas `*-overview-a/b` se tomaron **desde fuera de las salas**. La caja de la cámara incluía `VisualPass_VoidShell` (71 m de ancho frente a 54 m). Por eso no valen los porcentajes de píxeles oscuros de esas vistas ni la frase de la v1 sobre el "techo negro" de Room02. Las panorámicas y las rutas sí son válidas.

| Sala | Legibilidad y recorrido | Combate (dash/slide, flanqueo, coberturas, altura) | Defectos verificados | Luz y variedad |
|---|---|---|---|---|
| Room00_Hangar (fuera de la zona) | Salida por la esclusa de la cámara de conexión, rotulada "TRÁNSITO C-04" | Prólogo sin combate | — | 31 luces en tiempo real; p99 Release de 11,6 ms (el peor). Las luces protegidas (MenuFlicker/MenuSpin) no se tocan. |
| Room00b_AlarmCorridor | Lineal; las `AlertScreen` (z -49,5) quedan tapadas por las pilastras de z -50 | Pasillo estrecho, 3+2 enemigos | Texto de las pantallas cortado | 17 luces; riesgo de superar 4 luces por objeto (AlarmRed×8) |
| Room01_Foundry | **Falsa salida:** 4 `MoltenRun` enmarcan un panel de peligro de 12 m en el muro norte y los chevrones apuntan ahí; la salida real está al este | Amplia, con coberturas en rejilla | **CONFIRMADO en Play:** `SouthWall` (z -34,5…-33,5) atraviesa la estación de montaje (z -34,9…-33,3) del lobby persistente | Misma rejilla de coberturas y columnas que Room02 |
| Room02_Breach | Salida visible por encima de la grieta | Solo se usa el banco lejano; sin NavMeshLink sobre la grieta, el banco cercano es zona segura. La ronda 2 mete 14 enemigos en 48×22 m | **CONFIRMADO en Play:** franja sur x -26…26, z -33…-26,5. En 100 de 135 puntos no hay ningún collider desde y 12 hasta y -78 (los 35 restantes caen sobre `LobbyRoof`, en y 7,5). `GapFallRecovery` solo cubre la grieta: caer ahí lleva a `KillHeight` | Ocupa en planta el mismo volumen que Room01, a otra altura |
| Room03_Assembly | Marco luminoso de salida (bien) | Rampas y altura; picos p99 de 212–224 ms en Editor durante la horda (spawn) | — | Mismo casco que Room04, desplazado 64 m |
| Room04_Reactor | Salida poco guiada detrás del núcleo | Rampas laterales | **CONFIRMADO (bounds):** `CeilingSupport` (±23; 129) se cruza con `Ramp_V35`; la rampa queda partida en tramos de 1,05 y 2,55 m, probable atasco | Igual que Room03 |
| Pursuit05 | Persecución lineal | `SideCover` deja ranuras estrechas | **La campaña termina en un muro ciego** (`ExitWall`, z 385,5); no hay trigger de fin ni vuelta al menú | — |
| Esclusas Transfer (27/91/155) | Casi negras por dentro (lum 0,01–0,04) | — | Única luz: un punto de intensidad 4,4 a 7,7 m del suelo | Sin `AirlockCycle` |

Lo que la v1 ya cubría (NavMesh sin colisionador, objetos flotantes, materiales de error, reflection probes y exposición por sala) sigue igual: no hay materiales de error ni scripts ausentes. `*_MechTest` (6 escenas) está en Build Settings sin punto de entrada y con NavMesh incoherente (anotado en M18).

## 4. EFECTOS (resumen; ficha por efecto en effects.md y en la v1)

Capturas: `MapFxAudit/fx-{death,execution-extraction,slide-guard,damage-overlay}-*.png`. Coste en Editor (solo tendencia): pelea en Room01, p99 34,6 ms, 98,5 KB de GC por frame de media, 241 SetPass como máximo y 337 k triángulos (`fx-perf.txt`).

| Id | Hallazgo | Estado |
|---|---|---|
| **F1** | `RobotDestructionPool` guarda **un único prefab estático** y lo decide el primer `Prepare` (`RobotDestructionPool.cs:16,25`, verificado). BigBot y SawBot preparan el original; HoverBot, Turret y T07 preparan `_IR`. **Lo que aporta E1 (escala y destello) puede no verse nunca en una sala**, según qué enemigo despierte primero. | **Roto en parte (defecto de E1)** |
| F2 | `VP_DamageDirection` está duplicado en RequiemCampaign (l.48389 y l.48865), igual que `VP_OverdriveEdge`. Si los dos están activos, cada golpe pinta dos cuñas. | Sospecha de duplicado |
| F3 | Dos presupuestos de luz separados (BurstFlash y CannonExplosion) que suman hasta 4 luces transitorias; con el límite de 4 por objeto, la luz de la sala puede parpadear. | Débil |
| F4–F5 | Los restos (3–22 piezas por enemigo, 22–60 s) siguen con `Update` en reposo y sin tope global. `MechDeathExplosion` crea un ParticleSystem en cada `Awake` y el pool instancia 16 bursts en el spawn. Hipótesis para los picos p99 de Room03 (sin medir por separado). | Costoso |
| F6 | No hay soft particles (sin textura de profundidad). | Débil |
| F7–F10 | MaterialPropertyBlock fuera del SRP Batcher; caché y `??` en ShotFxLibrary/FxPool; hit-stop sin enfriamiento; cadenas `_VO_VO…` en labs. | Riesgo o deuda |
| v1 | Explosión `_IR` sin pantalla tapada ni cuadrados (E1); CA y grano del hangar en juego corregidos (E2); OverdriveEdge duplicado (E5); restos (E3). | Ver v1 |

**Corrección a la v1:** EnergyStripe, MagentaCore, PressureEdge y SmokeLobe **no** son huérfanos (son texturas en uso). `BigBot_ReferenceCharge_v09` **sí** está en campaña, anidado en BigBot_Burst_v10.

Ninguna captura actual coincide con el instante de la detonación, así que la legibilidad de `_IR` en combate sigue sin evaluar. P-FX1 la resuelve.

## 5. POSTPROCESADO e IR-03

- **Pila por sala:** `Campaign_PostProfile` (prioridad 0: ACES, grano Thin1 0,3, viñeta 0,084, bloom 0,1) más `Sector_*` (prioridad 10, balance de blancos). En Room00 manda `HG_PostVolume` (prioridad 20). Room00b no tiene Volume propio. Tabla completa en render-perf.md §2.
- **URP:** un solo nivel de calidad. HDR, MSAA off, render scale 1, Forward, 4 luces adicionales por objeto, SSAO (DepthNormals a resolución completa, intensidad 0,3 y radio 0,035: coste alto para poco efecto visual). La cámara del jugador va sin AA en runtime.
- **IR-03, corrección importante a lo que te dije antes:** la build declara `dpiAware True/PM` y `PerMonitorV2` en su manifiesto, y no hay entrada de compatibilidad DPI. La explicación "Unity recibe el escritorio lógico por el escalado al 120 %" **no está demostrada**: solo un proceso sin DPI awareness, como PowerShell, ve 1600×900. Además, en el primer arranque `RequiemSettings.Apply()` no fija la resolución si no existe la clave `IronRequiem.Fullscreen` (`RequiemSettings.cs:30`). La causa del arranque a 1600×900 está **pendiente** y la decisión "sin bordes = blur inevitable" debería revisarse con **B4**: un diagnóstico con log de `Screen.*` en `Player.log` y la prueba `-screen-width 1920 -screen-height 1080`. Las demás causas posibles:
  - **Descartadas:** render scale, upscaling, tamaño del RT, TAA/FXAA, MB, DoF, lens y CA en las salas.
  - **Contribuyen:** grano 0,3, aniso 1 en 140 texturas y aliasing por no tener AA.

## 6. INVENTARIO DE PUERTAS (detalle en doors.md; capturas en `MapFxAudit/doors/`)

**Contrato actual (comprobado en Play Mode en las 13 puertas):**
- Solo manda `CampaignFlow`. Abre la `SourceDoor` cuando la sala está completada, la cierra al entrar en la esclusa y espera `IsClosed && PlayerInside && carga ≥ 0,9 && 1,6 s`. Si el jugador sale más de 1 s, la reabre (Abort). Al final abre la `DestinationDoor` y las extra.
- `AirlockDoorMotion` solo mueve en vertical (`bounds.y+0,1`): abrir 0,7 s con smoothstep y cerrar 0,45 s con k², un golpe. Una puerta abierta termina **inactiva**; una que se cierra tiene **colisión desde el primer frame** (`closing collider on first frame=True` en todas).

| Esclusa | Puerta | Tipo | Malla y material | Colisión | Señal | Sonido |
|---|---|---|---|---|---|---|
| a0 ConnectionChamber (Hangar→Pasillo) | ChamberHangarDoor 8,4×4,8×0,6 | origen | cubo, **HG_Hazard (lista roja)** | Box sólido | AirlockCycle: **verde en reposo aunque esté bloqueada**, ámbar parpadeante en el ciclo, vapor | IR_Airlock_Cycle/Done (ciclo; la puerta no tiene sonido) |
| a0 | ChamberCorridorDoor (2 hojas, dintel, umbral y ojo de buey) | destino | HG_Hazard + HG_Glass (lista roja) | 5 Box | idem | idem |
| a0 | VestibuleShutter 8,4×4,8×0,5 | persiana extra | HG_PlateDark + franja HG_Hazard (lista roja) | Box | idem | idem |
| a1 StartLobby (Pasillo→Room01) | LobbyRear 14×7×1 | origen | **una pared** (VO_Env_Wall) que hace de puerta | Box | ninguna | ninguno |
| a1 | InitialLoadingDoor 12×7×0,6 | destino | VO_Env_Hazard | Box | ninguna | ninguno |
| a2 StairAirlock (Room01→Room02) | Room01ExitDoor 1×7,5×12 / Room02EntryDoor 1×7,5×8 | origen / destino | VO_Env_Hazard; **se desliza coplanaria con `UpperInnerStairGuard` (franjas en z-fighting, captura `a2-source-…-opening-0.25s.png`)** | Box | ninguna | ninguno |
| a3, a4, a5 Transfer_27/91/155 | SourceDoor / DestinationDoor 10×8×0,6 | origen / destino | VO_Env_Hazard; **en Room03/04 asoman por encima del techo al abrirse** (y 21–24,1) | Box | ninguna; interior casi negro | ninguno |
| Pursuit05 | ChaseEntranceGate / Exit | persecución | aparece y desaparece de golpe con `SetActive`, fuera de AirlockDoorMotion | Box | ninguna | ninguno |

**Problemas comunes:**
- Bloques planos que suben al techo y lo atraviesan, sin marco que los aloje, sin cerrojos y sin sonido.
- No existe un estado **rojo/BLOQUEADA**: a0 está en verde estando cerrada y a1–a5 no tienen ninguna señal.
- No hay NavMeshObstacle; el NavMesh de cada sala está horneado aparte y no depende del estado de la puerta.
- Una puerta que se cierra es un collider movido por transform y **puede bajar encima del jugador** (hueco puerta-trigger de 1,0 a 2,7 m).
- `CorridorAlarm` no controla ninguna puerta.

**Enganche para el rediseño (P2):** un `DoorPresentation` llamado desde `AirlockDoorMotion.Run` con el mismo k, **sin mover el root**. Así `IsClosed` sigue siendo exacto y la desactivación final no cambia. El marco, las luces y el componente deben ser **hermanos siempre activos**, no hijos de la puerta, porque el root se desactiva al abrirse. Hay que mantener un Renderer en el root o declarar el recorrido.

**Para P2 en zona protegida o lista roja:** la esclusa a0 la generó `HangarTransitAuthoring.cs` (protegido) y usa materiales HG_*. Su marco del lado del hangar está en Room00_Hangar.unity, y `ChamberHangarDoor` puede salir en el plano final de la cinemática. **Propuesta: dejar a0 fuera del rediseño o hacerlo solo con materiales copiados y sin tocar Room00** (necesita tu decisión). Además, `RequiemCampaign.unity` tiene cambios tuyos sin commit, así que las puertas nuevas irían en una copia `RequiemCampaign_v2.unity`.

## 7. RENDIMIENTO (IR-04)

- **Release 1080p** (exclusiva, vsync 0, RTX 4050 Laptop; `Perf/tour_player_mapfx_release_1080p.txt`):

  | Sala | FPS | p99 |
  |---|---|---|
  | Hangar | 174 | 11,6 ms |
  | Pasillo | 188 | 9,1 ms |
  | Foundry | 179 | 7,9 ms |
  | Breach | 183 | 7,3 ms |
  | Assembly | 182 | 8,4 ms |
  | Reactor | 192 | 7,0 ms |
  | Pursuit | 258 | 6,0 ms |

  Las medias cumplen los 120 FPS. **El p99 supera 8,33 ms en Hangar, Pasillo y Assembly.**
- **Lo que el tour no cubre:** rondas 2–3, hordas de 22–27 enemigos, muertes en cadena, disparo, OVERDRIVE y carga sostenida. La build de medición tampoco da tiempos de GPU (`enableFrameTimingStats` off). Por eso las medidas antes/después de la fase 2 necesitan R6 y R7.

## 8. Tabla priorizada de propuestas (esperan tu elección)

Impacto (Imp.), esfuerzo (Esf.) y riesgo: A alto, M medio, B bajo. **ZP** indica si toca la zona protegida. Todas las de escena van sobre **copias `_v2`**.

### Zona protegida y post
| Id | Propuesta | Imp. | Esf. | Riesgo | Archivos | ZP |
|---|---|---|---|---|---|---|
| **P-E2a** | Acotar E2 a después de la cinemática con un Volume runtime de prioridad 21 y rampa; HG_PostVolume intacto | **A** | B | B | `Scripts/Levels/ZoneEnvironment.cs`, `CampaignFlow.cs`, nuevo `Scripts/VisualPass/HangarGameplayPost.cs` | **Sí, la restaura.** Lee `HangarIntroCinematic.Finished` sin modificarla. |
| P-E2c | Alternativa: revertir E2 en Room00 | M | B | B | `ZoneEnvironment.cs` | La restaura |
| **B4** | Diagnóstico de resolución sin exclusiva (log de `Screen.*`, prueba con `-screen-width/height`) y, si hay 1920×1080 físicos, aplicarlo en el primer arranque desde un archivo nuevo | **A (IR-03)** | B | B | nuevo `Scripts/...`, sin tocar `Scripts/Menu` | No (solo la ventana de la build) |
| B2 | Grano 0,3→≤0,1 en Campaign_PostProfile y SMAA Low/Medium en una variante del jugador | M | B | B | `Campaign_PostProfile.asset`, variante del jugador | No |
| B3 | Aniso 4–8 solo en las texturas de sala no compartidas | M | B | B | `.meta` de texturas libres | No si se excluye la lista roja |

### MAPA
| Id | Propuesta | Imp. | Esf. | Riesgo | Archivos | ZP |
|---|---|---|---|---|---|---|
| **M7** | Cerrar el hueco sur de Room02 (losa de 54×1×7,5 o alargar `UpperNearBank`) y rehornear el NavMesh de la copia | **A** | B | B | `Room02_Breach_v2.unity` y su NavMesh | No |
| **M8** | Sacar la estación de montaje del `SouthWall` de Room01 (mover el muro a z -35,5 o la estación a z -31,5) | **A** | B | B | `Room01_Foundry_v2.unity` | No |
| **M9** | Sacar `CeilingSupport` de las rampas de Room04 y rehornear | M | B | B | `Room04_Reactor_v2.unity` | No |
| M13 | Final de campaña: `ExitWall` de Pursuit05 convertido en esclusa con trigger de fin (WinScene o menú) | A (cierra el bucle) | M | B | `Pursuit05_v2.unity`, componente nuevo | No |
| M10 | Luz y AirlockCycle en las esclusas Transfer (lo cubre el rediseño de puertas si se aprueba) | M | B | B | `RequiemCampaign_v2.unity` | No |
| M11 | Quitar la falsa salida de Room01 y marcar la salida este | M | B | B | `Room01_Foundry_v2.unity` | No (con materiales copiados) |
| M12 | Guía de salida en Room04 | M | B | B | `Room04_Reactor_v2.unity` | No |
| M14 | Mover las pantallas del pasillo a z -46 | B | B | B | `Room00b_AlarmCorridor_v2.unity` | No (solo transforms; los HG_* no se modifican) |
| M15 / M16 | Identidad geométrica por sala y spawns diegéticos (solo presentación) | M–A | A | M | copias de Room01–04 | No |
| M17 / M19 / M18 | Baranda en Room02 si no se aplica M7 / ranuras de cobertura en Pursuit / limpieza de MechTest y del overview de la auditoría | B | B | B | copias, Build Settings, test | No |
| v1 M2–M6 | Siguen vigentes (ver v1); M5 (luz del hangar) **sí toca la zona protegida**, ver R8 | | | | | |

### EFECTOS
| Id | Propuesta | Imp. | Esf. | Riesgo | Archivos | ZP |
|---|---|---|---|---|---|---|
| **E7** | Un pool de bursts por prefab en `RobotDestructionPool` (corrige F1, que deja E1 a medias) | **A** | B | B | `Scripts/Enemies/RobotDestructionPool.cs` | No en archivos; Room00 usa Carroñeros, así que hace falta un test del hangar |
| **P-FX1** | Medición determinista de efectos (semilla, N muertes ante la cámara, prefab del pool, luces, partículas, GC) | A (habilita el resto) | M | B | `Tests/PlayMode/MapFxAudit.cs` | No |
| E5+ | Desactivar los duplicados `VP_OverdriveEdge` y `VP_DamageDirection` (contarlos primero en Play) | M | B | B | `RequiemCampaign_v2.unity` | No |
| E3+ | Restos: tope global de 80 piezas, cero `Update` en reposo | M | M | B | `MechDebrisPiece.cs`, `RobotArmorDebris.cs` | No |
| E8 | Presupuesto de luz único para efectos (máx. 2) | B–M | B | B | `BurstFlashLight`, `CannonExplosionPresentation` | No |
| E9 | Enfriamiento del hit-stop | M | B | B | `GameFeel.cs` | No |
| E4+ | Paleta común: muerte ámbar, energía y OVERDRIVE magenta, defensa cian; en copias de material | M | M | B | copias de material | No si se usan copias (VFX/Mechanics está en la lista roja) |
| E10, E11, E13, E14 | `TrailDeath` aditivo en `_IR`, limpieza de ShotFxLibrary, cuarentena `_VO_VO`, pool del módulo robado | B | B–M | B–M | ver effects.md | No |
| E12 | Soft particles (Depth Texture) | B–M | B | M | asset del renderer | **Toca el pipeline: necesita OK aparte** |

### RENDIMIENTO
| Id | Propuesta | Imp. | Esf. | Riesgo | Archivos | ZP |
|---|---|---|---|---|---|---|
| **R6** | Ampliar el tour: muertes, todas las rondas, disparo, carga sostenida y semilla (misma carga antes y después) | **A** | M | B | `Scripts/Diagnostics/CampaignTourBenchmark.cs` | No |
| R7 | Build de medición con frame timing (CPU/GPU) y ProjectSettings restaurado | M | B | B | `Editor/VisualOverhaul/VisualOverhaulBench.cs` | No |
| P-FX2 / P-FX3 | Precalentar el pool al cargar la sala; chispas horneadas en lugar de `AddComponent` en cada muerte | M | M | B | `CampaignFlow.cs`, `MechDeathExplosion.cs` | No |
| R4′ | SSAO con fuente Depth o a media resolución | M | B | B | asset del renderer | No, pero es un asset de render: necesita tu OK |
| R9 | Pasillo: reducir solapes de más de 4 luces por objeto | B–M | B | B | `Room00b_AlarmCorridor_v2.unity` | No |
| R8 | Hangar (p99 de 11,6 ms, 31 luces): medir con `-irMarkers` y desactivar en runtime luces **no protegidas** fuera de vista tras la cinemática | M | M | M | script de campaña nuevo | **Roza la zona**: solo luces fuera de ella y solo tras la cinemática; necesita decisión |
| R2, R3, R5, R10, P-FX4–6 | Los de la v1 más: Volume de la cámara overlay, HudHealthTint, SectorLightingBlend | B–A | B–A | B–M | ver anexos | No |

### PUERTAS (FASE P)
| Id | Propuesta | Notas |
|---|---|---|
| **P1** | Conceptos de familia de puertas (2–3 direcciones) | Siguiente paso si lo apruebas; el subagente 6 escribiría solo en `Docs/IronRequiem/Concepts/Doors/Redesign_v01/`. |
| P2 | Implementación con `DoorPresentation` delegado desde `AirlockDoorMotion` (contrato intacto) en `RequiemCampaign_v2` | **Excluir la esclusa a0 o hacerla con materiales copiados y sin tocar Room00/HangarTransitAuthoring.** |

## 9. Orden recomendado

1. **P-E2a** (devuelve la cinemática a su aspecto original) y **E7** (sin él, E1 queda a medias).
2. **M7 + M8 + M9**: los tres errores de colisión confirmados.
3. **B4**: diagnóstico del blur antes de dar por cerrada la decisión sobre la pantalla sin bordes.
4. **R6 + P-FX1**: medición comparable, requisito para medir la fase 2.
5. **P1**: conceptos de puertas.

## 10. Pendientes anotados (fuera de alcance de esta fase)

- `RequiemCampaign.unity`, todas las salas y `CampaignFlow.cs` tienen cambios sin commit: la fase 2 trabajará en copias `_v2`. Para probarlas desde el menú habría que editar IntroMenu (protegida); la alternativa es abrir la escena en el Editor o promoverlas después de tu commit.
- El atlas dinámico de TMP (`LiberationSans SDF - Fallback.asset`) cambia en cada Play Mode: conviene revisarlo antes de tu commit o desactivar su modo dinámico (no lo he tocado).
- Los `*_NavMesh.asset` son binarios y no se revisaron por dentro; no se reprodujo en Play la separación de 3,98 m de `ChaseEntranceGate`.
- Recorrido manual con teclado y ratón: sigue sin hacerse en esta fase.
