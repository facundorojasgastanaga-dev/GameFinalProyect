# IRON REQUIEM: auditoría de mapa y efectos (Fase 1)

Fecha: 17/11/2026. Solo lectura más Play Mode sin guardar. **No se modificó ninguna escena, prefab, material, ajuste ni paquete.**

## Cómo se hizo

- **Recorrido en Play Mode** con un test PlayMode nuevo y aislado: `Assets/IronRequiem/Tests/PlayMode/MapFxAudit.cs`. Es el único archivo añadido al proyecto y se puede borrar sin efectos.
  - Usa el mismo arnés que `CampaignTests`: entrada virtual del Input System, salas cargadas en aditivo dentro de la escena temporal del Test Runner y teardown sin guardar.
  - Se lanzó con la cola `Tools/IronRequiem/Requests/visual-overhaul.txt` (VisualOverhaulDriver).
  - Pruebas y resultado:
    - `A_RouteCapturesAndMapChecks`: superada, 151 s.
    - `B_FightEffectsAndProfiler`: superada.
    - `C_HordeProfilerRoom03`: superada.
    - `D_SectorVolumeWeightOverTime`: superada, 41 s.
  - Tras cada ejecución, el Editor volvió a IntroMenu sin Play Mode. El Test Runner reescribió `EditorBuildSettings.asset` y dos assets de tutoriales, con el mismo contenido: `git diff` vacío.
- **Salidas** en `Tools/IronRequiem/Reports/MapFxAudit/`:
  - 115 capturas a 1920×1080 renderizadas desde la cámara del jugador con su post real. Las capturas `blur-*` son de la Game view.
  - Volcados: `map.txt` (por sala), `fx-perf.txt`, `horde.txt` y `volume-weight.txt`.
- **Cuatro subagentes de solo lectura** aportaron el análisis estático: estructura del mapa, inventario de efectos, render y postprocesado, y protocolo de rendimiento.
- **Límites de esta fase.**
  - Las cifras de rendimiento son del **Editor**, con la Game view a 1030×556: sirven para ver tendencias y **no certifican IR-04**. No se hizo build Release (ver propuesta R1).
  - Mis capturas PNG asignan unos 8 MB cada una, así que **los picos de GC de ~10 MB son míos**. El GC de fondo sí es válido.
  - El recorder "Batches Count" devolvió 0 en esta versión; SetPass y Tris son válidos.
  - No se capturaron la carga del cañón ni OVERDRIVE en combate. Tras la pelea con fuego automático, el módulo robado ya se había agotado y expulsado, y la búsqueda de `CannonChargePresentation` en el cañón montado no lo encontró. Quedan pendientes para una captura manual.
  - Frame Debugger: no se usó.

---

## Resumen ejecutivo

| # | Hallazgo | Tipo | Evidencia |
|---|---|---|---|
| 1 | **El grado de color por sector nunca se aplica.** `VisualPass_SectorGrade` se queda en **weight 0,00** al menos 6 s después de entrar (Room01 y Room02), y en 0,00–0,01 en todas las salas. `VolumeFadeIn.OnEnable` pone 0 y `CampaignFlow`/`ZoneEnvironment.Bind` guarda ese 0 como peso "original". | ROTO | `volume-weight.txt`; `VolumeFadeIn.cs:17`, `ZoneEnvironment.cs:67`, `CampaignFlow.cs:246-282` |
| 2 | **Las reflection probes no tienen datos en runtime** (`baked=none` en Room01-04 y Pursuit05). Las escenas no tienen LightingData; los `.exr` no los referencia nada. Por eso las superficies metálicas HardSurface salen planas o negras (coberturas negras en Room03). | ROTO | `map.txt` (líneas `probe`); `r03-assembly-pano-0.png` |
| 3 | **Las explosiones de muerte tapan la pantalla.** Tiñen todo de naranja y lanzan cuadrados negros gigantes delante de la cámara. Es el C1 de F0, sigue vivo: la cadena `RobotBodyBurst_CombatPolish_VO_VO_VO` aplica startSize ×1,35⁸ ≈ ×11 por reejecución acumulada del autor de VO. | EXCESIVO | `seq-room03-round2-live-1.png`; `VisualOverhaulEnemies.cs:92-105` |
| 4 | **El Volume del menú del hangar se aplica jugando en Room00_Hangar**: prioridad 20, CA 0,12, viñeta 0,38, grano 0,28 y bloom 0,6. Se ven flecos cromáticos en la chatarra y la salida está muy oscura (lum 0,062, 59 % de píxeles negros). | EXCESIVO / IR-03 | `r00-hangar-route-exit.png`; `map.txt` |
| 5 | **IR-03 (blur): no hay ningún efecto de blur activo.** Motion blur, DoF, lens y Panini están en 0; TAA/STP no se usan; render scale es 1. El A/B con todo eso forzado a 0 y AA None da la misma imagen. La causa probable es la **resolución**: el ejecutable arranca a 960×540 en ventana y el interruptor de pantalla completa no cambia la resolución (se estira). En el Editor, la Game view estaba a 1030×556. Contribuyen el grano 0,3 global y la falta de AA y aniso. | CAUSA | `blur-A/B/C-*.png`; `ProjectSettings.asset:44-45,88`; `RequiemSettings.cs:25` |
| 6 | **Room03 y Room04 son la misma caja** (54×52, mismas paredes, suelo de "ruido nublado", 4 luces sin sombra), y Room01/02 repiten coberturas en las mismas x/z. Con el grado de sector a 0 (hallazgo 1), solo cambia el tinte de 4 luces. | DÉBIL | `r03-assembly-pano-0.png` frente a `r04-reactor-pano-0.png` |
| 7 | **Esclusas casi negras por dentro** (89–97 % de píxeles oscuros en Transfer 2→3, 3→4 y 4→5) y **salida de Room04 oculta por `ReactorCore`** desde la entrada (linecast bloqueado). | DÉBIL | `r02/03/04-*-airlock-inside.png`, `r04-reactor-route-exit.png` |
| 8 | **Rendimiento (Editor): limitado por CPU, como en todas las medidas previas.** Main thread p50 de 18–23 ms y p95 de 26–34 ms. Room03 R3: 1,2 M triángulos, 826 partículas y 90 sistemas vivos. 14–16 luces puntuales activas. Picos de 230–250 ms (p99) al aparecer rondas. GC de fondo de 14 KB/frame sin disparar. | — | `fx-perf.txt`, `horde.txt` |

Sin problemas encontrados: materiales con error o rosas (0 en todas las salas), scripts ausentes (0), NavMesh sin suelo debajo (0 puntos en una rejilla de 1,5 m) y caminos de enemigos (ronda 1 de Room01-04: 6/6 completos, 0 fuera del NavMesh). Las caídas tienen recuperación (Room02, Pursuit ×3 y el pozo del hangar).

---

## MAPA, sala por sala

En cada sala hay 4 panorámicas (`<sala>-pano-0/90/180/270.png`), 2 vistas altas desde esquinas (`-overview-a/b`), la vista hacia la salida (`-route-exit`) y la esclusa por dentro y a la salida. "Lum" es la luminancia media en sRGB 0–1; "oscuro" es el % de píxeles con lum < 0,04.

### Room00_Hangar (prólogo)
- **Capturas:** `r00-hangar-intro-cinematic`, `r00-hangar-pano-*`, `-overview-*`, `-route-exit` y `r00-airlock-cycle`.
- **Recorrido:** la puerta "TRÁNSITO C-04 · PUENTE NORTE" se lee por su rótulo y sus franjas. El entorno está muy oscuro (lum 0,062) y la chatarra se funde con el fondo. No hay luz que guíe hasta la puerta.
- **Combate:** no hay. Tiene 753 renderers y 2,05 M triángulos (dato previo), y es la sala más pesada de cargar: pico de activación de 3,6–4,4 s en el Editor.
- **Defectos:**
  - No tiene reflection probe.
  - 8 piezas `ConveyorPit/BrokenRailing/Rail` flotan 1,0 m sobre el suelo. Probablemente es intencional (barandilla rota), pero conviene verificarlo.
  - Se le aplica el Volume del menú (hallazgo 4).
- **Iluminación:** 31 luces (16 spot, 13 point y 1 direccional) con una sola sombra. Ambiente Trilight 45474F, niebla exp 0,016.

### Room00b_AlarmCorridor
- **Capturas:** `r00b-corridor-*` (incluye `-wave-alarm` y `-deaths`).
- **Recorrido:** el pasillo es claro (18 m de ancho y salida a 27 m, visible). Es oscuro (43 % de píxeles oscuros con la alarma activa).
- **Combate:** coberturas de 3,6×1,5 y plataforma de guardia. Hay espacio para dash y slide.
- **Defectos:** no tiene probe ni Volume propio, así que usa el post del jugador (ACES, grano 0,3).
- **Lobby / estación** (`r00c-lobby-station*`): se lee bien. El empalme de suelos lobby (y=-0,02) y Room01 (y=0) es un posible z-fighting; en las capturas no se aprecia.

### Room01_Foundry
- **Capturas:** `r01-foundry-*`.
- **Recorrido:** la salida está a 52 m en el muro este y es visible, pero tiene poco contraste (lum 0,14 y 28 % oscuro). Las flechas amarillas del suelo (V35 FloorGuide) son la única guía.
- **Combate:** 54×65 m, techo a 12 m y una pasarela. Hay buen espacio para movimiento rápido. Las coberturas bajas y altas son simétricas.
- **Defectos:**
  - El suelo y las paredes usan la misma textura de ruido nublado (F0 #2, sin resolver).
  - El probe no tiene datos.
  - El grado de sector está a 0.
- **Iluminación:** 4 puntuales cálidas (1, .66, .42) de I=6, sin sombras. Ambiente Trilight 5F5042 y niebla exp² ocre.

### Room02_Breach
- **Capturas:** `r02-breach-*` y `r02-breach-gap-before-jump`.
- **Recorrido:**
  - La grieta de 4 m se lee y el salto funciona: se aterriza en (0; 9,6; 5,2).
  - La salida norte, a 20 m, es visible.
  - Las vistas altas están muy oscuras (64 % oscuro): el techo a 21 m queda negro y no hay probe.
- **Combate:** los spawns solo están en el banco lejano y la arena solo cubre ese banco (dato estático).
- **Defectos:**
  - La esclusa de salida está negra por dentro (lum 0,015, 97 % oscuro).
  - El ambiente y la niebla son idénticos a los de Room03, Room04 y Pursuit (45474F / 0F1113), aunque ZoneEnvironment debería diferenciarlos.

### Room03_Assembly
- **Capturas:** `r03-assembly-*`.
- **Recorrido:** la salida está a 45 m, en línea recta tras los bancos.
- **Combate:**
  - Bancos de 4×1,6×10 y soportes de 2×2.
  - La caja es la misma que la de Room04.
  - Las coberturas metálicas de la derecha se ven completamente negras, por la falta de probe y de luz de relleno.
- **Defectos:**
  - En la ronda 1 todos los enemigos llegaron al jugador.
  - Los slots (±18; 57/73) junto a `CeilingSupport` son un riesgo estático no reproducido: `Reach` dio 6/6 completos.
  - Esclusa negra (89 % oscuro).

### Room04_Reactor
- **Capturas:** `r04-reactor-*`.
- **Recorrido:** el `ReactorCore` es un buen punto de referencia, pero **tapa la salida** desde la entrada (`visible=False`). El jugador tiene que rodearlo sin ninguna guía de luz.
- **Combate:** coberturas de anillo con franjas de peligro y pasarelas. Hay espacio adecuado.
- **Variedad:** el cian del núcleo es el único rasgo propio. Paredes, suelo, techo y luces son los de Room03.

### Pursuit05
- **Capturas:** `r05-pursuit-*` (inicio, gap1-3 y salida).
- **Recorrido:**
  - Es lineal y claro.
  - Es oscuro (36–40 % de píxeles oscuros) y las grietas se leen poco desde 6 m antes.
  - Las 3 recuperaciones de caída están presentes y el salto superó las 3 grietas.
- **Defectos:**
  - `ChaseEntranceGate` aparece 3,98 m sobre el suelo: probablemente es la compuerta abierta, a confirmar.
  - El probe no tiene datos.

### Variedad visual entre salas
No se distinguen Foundry, Breach, Assembly y Reactor más allá del tinte de 4 luces y de un elemento clave (crisoles o núcleo cian). Causas, por orden de peso:
1. El grado de sector está a 0.
2. Mismos materiales en las 4 salas (CP_Cable, VO_Env_RustMetal, CP_Wall e IR_V35_*).
3. Mismo ambiente y misma niebla en Room02-05.
4. Las probes no tienen datos.

### Iluminación
- **Exposición:** lum media de 0,13–0,22 en panorámicas. Es oscura pero coherente; los negros sin intención están en techos, esclusas y metales sin reflejo.
- **SectorLightingBlend frente a ZoneEnvironment:** los dos escriben `RenderSettings` en la transición. El modo final siempre es Trilight y no se observaron saltos en las capturas, pero queda pendiente un registro fotograma a fotograma.
- **Reflection probes:** hallazgo 2. Además, sus cajas no cubren el techo ni los extremos de Room01 y Room02 (dato estático).

---

## EFECTOS, uno por uno

Estado: **bien** / **débil** / **roto** / **excesivo**. La capturas están en `Tools/IronRequiem/Reports/MapFxAudit/`.

| Efecto | Qué hace y cuándo | Captura | Estado | Notas |
|---|---|---|---|---|
| Explosión de muerte (`RobotBodyBurst_CombatPolish_VO_VO_VO` + `MechDeathExplosion` + `BurstFlashLight`, y `VFX_HoverBot_Explosion` en T07_Mech) | Al morir un enemigo se disparan 3-4 sistemas a la vez | `seq-room03-round2-live-1`, `fx-death-0..5` | **EXCESIVO** | Tiñe la pantalla y deja cuadrados negros gigantes delante del jugador. Tamaño ×11 acumulado. Duplica sistemas. |
| `MechDebrisPiece` | Trozos que siguen vivos 22–60 s con SphereCast y Update | `blur-*-turning` (trozos en el suelo) | débil / costoso | Hasta unos 150 Updates y SphereCasts activos en una sala grande. |
| `ExtractionArcFx` | Arcos y chispas azules en la ejecución | `fx-execution-extraction-0..5` | bien / débil | Breve (0,26 s) y legible. El azul choca con el ámbar y el magenta del resto. |
| `MuzzleShotFx` | Humo de boca, con pool | `seq-room01-fight-live-*` | bien | Bajo coste. |
| `ShotFxLibrary` (impactos Metal/Dust) | Sustituye el impacto de energía al dar en el entorno | `seq-room01-fight-live-*` | débil | Se pierde la identidad del arma. La caché estática no se limpia entre escenas. |
| `HeatVentFx` | Brillo y vapor por calor | — (no capturado) | pendiente | Sospecha de brillo invisible con celdas de combustible (estático). |
| `CannonCharge_Magenta` | Carga del cañón del T-07 | — (no capturado) | pendiente | Captura manual. |
| `OverdriveScreenFx` | Borde naranja en OVERDRIVE | — (no capturado) | **sospecha de duplicado** | 2 instancias `VP_OverdriveEdge` en RequiemCampaign; si coinciden, el alfa se suma. |
| `SlideGuardScreenFx` | Borde cian durante la guardia del slide | `fx-slide-guard-0..3` | débil | No se aprecia en las capturas de cámara (es UI de pantalla). Alfa máximo 0,55, el más alto de los bordes. |
| Overlay de daño (C2 de F0) | Al recibir daño | `fx-damage-overlay-0..2` | pendiente | Las capturas de cámara no incluyen UI. Ver en Game view. |
| `HeavyDeathFeel` / `GameFeel.HitStop` | Congelado y temblor | — | bien | No toca el Volume. Los congelados se encadenan con `max()`. |
| `EnemyStateLights` | Emisivo por estado con MaterialPropertyBlock | `r0x-*-round1` | débil en T-07 | En T-07 los 4 estados son rojos (VisualOverhaulT07Glare). |
| `CorridorAlarm` | Luces rojas de alarma | `r00b-corridor-wave-alarm` | bien | — |
| `AirlockDoorMotion` / `AirlockCycle` | Puertas y ciclo de la esclusa | `r00-airlock-cycle`, `*-airlock-*` | bien | El interior de los Transfer está negro (mapa). |
| `SectorLightingBlend` + `ZoneEnvironment` + `VolumeFadeIn` | Mezcla de ambiente, niebla y grado | `volume-weight.txt` | **ROTO** | Hallazgo 1. Además son dos sistemas para el ambiente. |
| `LightFlicker`, `VisualOverhaulAblation`, `BigBot_ReferenceCharge_v09` | — | — | sin uso en campaña | Solo labs. |
| Assets huérfanos: `VFX/CannonCharge/{EnergyStripe,MagentaCore}.asset`, `VFX/CannonExplosion/{PressureEdge,SmokeLobe}.asset` | — | — | sin uso | Solo los citan generadores de Editor. |

**Coherencia de estilo.** No hay paleta común:
- Ámbar (2,4; 1,1; 0,35) en las muertes.
- Magenta en la carga y en las chispas de T-07.
- Azul en la extracción y en HoverBot.
- Naranja en OVERDRIVE.
- Cian en SlideGuard.

Las duraciones son razonables (0,1–0,5 s), salvo los trozos persistentes de 22–60 s. El efecto más intenso (la muerte) es justo el que tapa la vista.

**Coste medido en el Editor, pelea de Room01 con fuego continuo** (`fx-perf.txt`):
- Partículas: máximo 259, con 32 sistemas vivos.
- Luces puntuales o spot activas: 16.
- SetPass: máximo 227.

**Horda de Room03 R2-R3:** 826–852 partículas, 90–96 sistemas y 1,2 M triángulos.

**CLEANUP_TABLE.md** es un inventario de assets del 07/11, anterior a VisualPass y a la cadena `_VO_VO_VO`. No marca ningún efecto como duplicado; los duplicados de esta tabla son nuevos.

---

## POSTPROCESADO (IR-03)

| Perfil (dónde) | Bloom thr/int/scatter/clamp | Tonemap | Grano | Viñeta | Otros |
|---|---|---|---|---|---|
| `Campaign_PostProfile` (Volume del jugador, campaña) | 0,9 / 0,1 / 0,7 / 12 | ACES | 0,3 | 0,084 | MB, DoF, CA y Lens a 0 |
| `HG_MenuVolume` (Room00_Hangar, prioridad 20) | 1,1 / **0,6** / 0,65 / 10 | Neutral | **0,28** | **0,38** | **CA 0,12**; exposición +0,1, contraste 14, saturación −14 |
| `Sector_Room0x` (prioridad 10) | — | — | — | — | WB y ColorAdjustments; **weight real 0** |
| `CombatClarity` (MainScene y labs) | 0,9 / 0,1 / 0,7 / 65472 | ACES | 0,3 | 0,084 | El clamp no limita los fireflies |

- **Motion blur y DoF: no causan IR-03.** Están apagados en todos los perfiles. El A/B `blur-A` (actual), `blur-B` (MB/DoF/CA/Lens/Panini a 0) y `blur-C` (además sin AA) produce imágenes equivalentes.
- **AA:** la cámara de campaña va con AA None. Hay dientes de sierra y no hay blur.
- **Bloom:** en la campaña (0,1, clamp 12) no quema; los emisivos HDR de 3–8 producen un halo moderado. En el hangar (0,6) sí hay riesgo de quemado.
- **Pendientes de git:** `DefaultVolumeProfile.asset` tiene `active: 1` en MotionBlur y DoF. No tiene efecto visual (intensidad 0 y modo Off), pero es un volcado del Editor que VISUAL_OVERHAUL dio por restaurado.

---

## RENDIMIENTO (Editor, solo tendencia)

| Muestra | Frame p50 / p95 / p99 | Main p50 / p95 | GC de fondo | SetPass máx. | Tris máx. | Partículas / sistemas | Luces |
|---|---|---|---|---|---|---|---|
| Room01 R1, IA viva, sin disparar (3 s) | 18,2 / 24,0 / 26,9 ms | 18,1 / 23,8 | 14 KB/frame | 129 | 187 k | 89 / 13 | 14 |
| Room01, pelea con fuego (14 s) | 17,7 / 26,3 / 39,7 | 17,9 / 26,3 | (contaminado) | 227 | 309 k | 259 / 32 | 16 |
| Room03 R1 | 19,4 / 27,4 / 232 | 19,4 / 27,3 | — | 191 | 400 k | 139 / 19 | 14 |
| Room03 R2 | 22,4 / 34,5 / 247 | 22,4 / 34,3 | — | 337 | 541 k | 852 / 96 | 16 |
| Room03 R3 (22 enemigos, 14 s) | 22,9 / 31,0 / 66 | 22,9 / 31,5 | — | 262 | **1,20 M** | 826 / 90 | 16 |

Lectura:
- El hilo principal es el cuello de botella (coincide con todas las medidas previas: GPU de 1–5,5 ms en builds).
- Los p99 de 230–250 ms coinciden con la aparición de rondas.
- Las transiciones en el Editor tienen picos de 65–245 ms y de 3,6–4,4 s al activar el hangar.

Línea base previa y carencias, según el subagente de rendimiento:
- **No existe ninguna medición Release de la campaña a 1080p.** Los tours de campaña son Development a 1600×900. Los Release (Bunker y VisualBench) son de fixtures antiguos.
- Las rondas cargadas (Room04 R3, 27 enemigos; Room03 R3, 22) nunca se han medido.

Protocolo para Fase 2: build Release Mono D3D11 con `enableFrameTimingStats` solo durante el build, 1080p exclusivo, vSync off, semilla fija, Room04 R3 y Room03 R3 durante al menos 60 s por pasada, A/B/A/B y al menos 300 s medidos. Métricas de `CombatFrameCapture` más Batches, SetPass, partículas y luces. Condiciones del portátil registradas (plan de energía, nvidia-smi).

Sospechas de coste en CPU, de análisis estático:
1. Detección y raycasts por enemigo y frame: `EnemyController.cs:256`, `SawBotCombat.Visible` (:33, varias veces por frame), `BigBotCombat.CanSeeSource` (:270).
2. Spawn y muerte sin pool de actores: `EnemySpawnQueue.cs:46`, `EnemyController.cs:279/427-441`.
3. Muerte con 3-4 sistemas y trozos persistentes: `MechDeathExplosion.cs:90-112`, `MechDebrisPiece.cs:27-36`.
4. Render en CPU: ScheduleDraw/DrawSRPBatcher de 2–5 ms en el tour, SSAO a resolución completa y hasta 4 luces adicionales por objeto en Forward.
5. HUD: strings y `GetComponent` por frame en `MechanicHud.cs:30-47`.

---

## Propuestas priorizadas (esperan tu elección)

Impacto y riesgo: A = alto, M = medio, B = bajo. El esfuerzo es aproximado.

### MAPA

| ID | Propuesta | Impacto | Esfuerzo | Riesgo | Archivos |
|---|---|---|---|---|---|
| M1 | Arreglar el peso del grado de sector: que `ZoneEnvironment.Bind` guarde 1 (o el peso serializado) en lugar del 0 de `VolumeFadeIn`, o quitar `VolumeFadeIn` de las salas | **A** | B | B | `Scripts/Levels/ZoneEnvironment.cs`, `Scripts/VisualPass/VolumeFadeIn.cs` (código, no escenas) |
| M2 | Hornear de verdad las reflection probes en copias `_v2` (LightingData por sala) o pasarlas a Realtime OnAwake, una sola vez; ajustar las cajas al volumen de la sala | **A** | M | M | Copias `Room0x_v2.unity` y `Art/Levels/Campaign/*` |
| M3 | Identidad por sala: materiales de suelo y pared propios (variantes HardSurface), ambiente y niebla distintos en ZoneEnvironment de Room02-05, y 1-2 luces clave con sombra por sala | A | M-A | M | Copias `_v2`, `Art/Environment/Bunker` y `Scrap` |
| M4 | Legibilidad de la ruta: luz de salida o baliza encima de cada puerta, iluminar el interior de los Transfer y una guía alrededor de `ReactorCore` en Room04 | M | B | B | Copias `_v2`, RequiemCampaign (Transfers) |
| M5 | Hangar: más luz de relleno hacia la salida y sustituir el Volume de menú en juego (ver E2) | M | B | B | Copia `Room00_Hangar_v2` |
| M6 | Verificar las piezas flotantes (barandilla del hangar y `ChaseEntranceGate`) y el empalme lobby/Room01 | B | B | B | Inspección |

### EFECTOS

| ID | Propuesta | Impacto | Esfuerzo | Riesgo | Archivos |
|---|---|---|---|---|---|
| E1 | Explosión de muerte: volver a la escala prevista (×1,35 una vez) sin acumulación, y que el autor de VO sea idempotente. Una sola explosión por muerte (quitar `DeathVfx` de plantilla en T07_Mech o el burst duplicado) y límite de tamaño en pantalla | **A** | M | M | `Editor/VisualOverhaul/VisualOverhaulEnemies.cs` y los prefabs `RobotBodyBurst_*_VO*` (variantes nuevas; los actuales quedan desactivados, no borrados) |
| E2 | Hangar en juego: quitar CA y grano y bajar viñeta y bloom (desactivar `HG_PostVolume` al empezar a jugar o crear un perfil de juego) | **A** | B | B | Copia `Room00_Hangar_v2` o script de cambio de perfil |
| E3 | Trozos de muerte: límite global (p. ej. 60 activos), pasar a reposo tras 3-5 s y pool | M | M | B | `MechDebrisPiece.cs`, `MechDeathExplosion.cs` |
| E4 | Paleta común de feedback (daño y muerte ámbar, energía robada magenta, defensa cian) con intensidades y duraciones unificadas | M | M | B | Materiales y prefabs VFX (variantes) |
| E5 | Quitar el `VP_OverdriveEdge` duplicado (desactivarlo) | B | B | B | RequiemCampaign: requiere tu commit previo o una copia |
| E6 | Capturar de forma manual la carga del cañón, OVERDRIVE, HeatVent y el overlay de daño en Game view, y evaluarlos | M | B | B | — |

### RENDIMIENTO

| ID | Propuesta | Impacto | Esfuerzo | Riesgo | Archivos |
|---|---|---|---|---|---|
| R1 | Línea base Release de la campaña a 1080p (Room04 R3, Room03 R3) con el protocolo anterior. Hace falta una operación de build nueva y medir antes de optimizar | **A** | M | B (snapshot y restauración de ProjectSettings) | `Editor/VisualOverhaul/VisualOverhaulBench.cs` (nueva operación), `CampaignTourBenchmark.cs` |
| R2 | Raycasts de visibilidad: cachear `Visible` por frame en SawBot y BigBot, y escalonar la detección | A | M | M (IA) | `AI/SawBotCombat.cs`, `AI/BigBotCombat.cs` |
| R3 | Pool de actores en spawn y muerte (EnemySpawnQueue) y de los sistemas de muerte | A | A | M | `Levels/EnemySpawnQueue.cs`, `EnemyController` (plantilla, con cuidado) |
| R4 | SSAO a media resolución o apagado (efecto mínimo) | M (GPU) | B | B | `Default_PipelineAsset_ForwardRenderer.asset` (render pipeline: requiere tu OK explícito) |
| R5 | HUD sin strings por frame | B | B | B | `UI/MechanicHud.cs` |

### IR-03 (blur)

| ID | Propuesta | Impacto | Esfuerzo | Riesgo | Archivos |
|---|---|---|---|---|---|
| B1 | Resolución nativa en el ejecutable (`defaultIsNativeResolution`) y `Screen.SetResolution(nativa)` al pasar a pantalla completa | **A** | B | B | `ProjectSettings.asset` (respaldo), `Bridges/Game/RequiemSettings.cs` |
| B2 | Bajar el grano de 0,3 a 0,1 o menos en Campaign_PostProfile, y SMAA Low en la cámara de campaña (ahora None) | M | B | B | `Art/VisualPass/Campaign_PostProfile.asset`, cámara del jugador (variante o copia) |
| B3 | Forzar el filtrado anisotrópico en suelos (137 texturas con aniso 1) | M | B | B | `.meta` de texturas o QualitySettings |

Mi recomendación de primer lote: **M1 + E1 + E2 + B1**. Son baratos o medios, de alto impacto, y desbloquean evaluar M3 y E4 con el aspecto real de las salas. **R1** debería ir antes de cualquier optimización de rendimiento.

## Pendientes anotados (fuera de alcance)

- `DefaultVolumeProfile.asset` modificado sin commit (MB y DoF con `active: 1`, sin efecto).
- Las escenas `*_MechTest` usan geometría anterior a v35 con el NavMesh v35 compartido.
- `Art/Levels/Campaign/V35/` y `Editor/LevelOpenPass.cs` están sin versionar.
- Hay 3 configuraciones de agente para `agentTypeID 0` (hangar 0,6 m, corredor 0,9 m, salas 2,25 m).
- Archivo de esta auditoría añadido: `Assets/IronRequiem/Tests/PlayMode/MapFxAudit.cs`. Bórralo si no quieres conservarlo; no lo referencia nada.
