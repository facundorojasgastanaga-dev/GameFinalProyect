# MapFxAudit2 — RENDER Y RENDIMIENTO (solo lectura)

Fecha: 17/11/2026. Subagente de render y rendimiento. **No se modificó nada del proyecto**: no se usó el Editor ni git add/commit. Único archivo escrito: este informe.

Fuentes leídas:
- `Docs/IronRequiem/MAP_FX_AUDIT_20261117.md` (v1).
- `Docs/IronRequiem/CHECKLIST_TRABAJO.md`, sección final de IR-03/IR-07 y verificación Release.
- `Reports/Perf/tour_player_mapfx_release_{1080p,default}.txt`.
- `Reports/MapFxAudit/{fx-perf,horde,volume-weight}.txt`.
- `Reports/ProtectedZone/guardian.md`.
- YAML de ProjectSettings, del pipeline, de las escenas y de los prefabs, y código C# citado por línea.
- El manifiesto embebido en `Builds/VisualOverhaul/mapfx-release/IronRequiem.exe`, `Player.log` y la consulta de pantalla de Windows (solo lectura).

---

## 1. URP en uso

| Elemento | Valor | Evidencia |
|---|---|---|
| Niveles de calidad | **1 solo**, `Default`, para Standalone. pixelLightCount 4, `anisotropicTextures: 1` (por textura, no forzado), `antiAliasing: 0`, vSync 0, lodBias 2, textureQuality 0 (completa), `realtimeReflectionProbes: 0`. | `ProjectSettings/QualitySettings.asset` |
| Pipeline asset | `Assets/Rendering/Default_PipelineAsset.asset` (GUID 6cbb3341…), el mismo en Quality y en Graphics. No hay otro pipeline asset. | `QualitySettings.asset` customRenderPipeline; `GraphicsSettings.asset` m_CustomRenderPipeline |
| HDR | **On**, `m_HDRColorBufferPrecision: 0` (32 bits). | `Default_PipelineAsset.asset` |
| MSAA | `m_MSAA: 1` (desactivado). Las cámaras tienen `m_AllowMSAA: 0`. | ídem; `FPS/Prefabs/Player.prefab:168,877` |
| Render scale / upscaling | `m_RenderScale: 1`. `m_UpscalingFilter: 0` (Auto) no actúa con escala 1. Ningún script cambia `renderScale`, `upscalingFilter` ni la resolución dinámica (grep en `Assets/**/*.cs`). `m_AllowDynamicResolution: 0` en las cámaras. | ídem |
| Luces | Principal por píxel con sombras soportadas y shadowmap de 1024. Adicionales por píxel, **límite de 4 por objeto**, sombras adicionales soportadas (atlas de 1024). Soft shadows en calidad 2 (Medium). Distancia de sombras 50, 1 cascada. | ídem |
| Otros | SRP Batcher on, dynamic batching off. GPU Resident Drawer off. LOD crossfade on. `m_UseAdaptivePerformance: 1`, con el módulo `com.unity.modules.adaptiveperformance` en el manifest; sin proveedor en PC, sin efecto esperado. `m_VolumeFrameworkUpdateMode: 0` (cada frame). Grading LDR de 32. Depth texture y opaque texture off en el asset. | ídem; `Packages/manifest.json:19` |
| Renderer | 1: `Default_PipelineAsset_ForwardRenderer.asset`. **Forward** (`m_RenderingMode: 0`). Depth priming off. Native Render Pass off. Intermediate texture Auto. | `Assets/Rendering/Default_PipelineAsset_ForwardRenderer.asset` |
| Renderer features | **Solo SSAO** (activa). No hay decals ni render features propias. Ajustes: `AOMethod 1` (Interleaved Gradient), **`Source 1` = DepthNormals** (fuerza un prepass DepthNormals de todos los opacos), **`Downsample 0`** (resolución completa), `Samples 2` (Low), `BlurQuality 0` (High), **Intensity 0,3** y **Radius 0,035**, `AfterOpaque 0`. | ídem, bloque `&8514492229712036805` |

### Cámaras de la campaña

La cadena del jugador es: `RequiemCampaign` → `Player_ModularArm_v07` → `Player_NativeVisual_v06` → `Player_Integrated_v04` → `FPS/Prefabs/Player.prefab`.

| Cámara | Post | AA | Otros | Evidencia |
|---|---|---|---|---|
| Main Camera (jugador, Base) | on | **None** en runtime. La base es FXAA (`Player.prefab:916`), pero `Player_NativeVisual_v06.prefab:80` la anula a 0. MapFxAudit lo confirmó: `camera AA=None quality=High postFX=True stack=1 fov=60.0`. | HDR on, far 300, FOV 60. Volume mask = Default. Volume local del prefab con prioridad 0 y perfil `PostProcessingProfile`, que `RequiemCampaign` anula a `Campaign_PostProfile` (`RequiemCampaign.unity`, override `sharedProfile` → 8b1176be…). | `Player.prefab:900-924`; `fx-perf.txt` |
| Weapon camera (Overlay, en el stack) | off | None | Sombras on, far 10. | `Player.prefab:183-206` |
| `Room00_Hangar/WakeUpCinematic/CinematicCamera` (**PROTEGIDA**) | on | SMAA High | `stopNaN` 1. Volume mask = Default, así que **ve los mismos Volumes globales** que el jugador. | `Room00_Hangar.unity:45939-45941` |
| `Room00_Hangar/CctvFeed` | off | None | Sin sombras. | `Room00_Hangar.unity:183507` |

---

## 2. Volumes de la campaña

### Perfiles (valores con override; `*` = activo)

| Perfil | Bloom thr / int / scatter / clamp (HQ, downscale) | Tonemap | Exposure / contraste / saturación / filtro | WB | CA | Grano (tipo, int., resp.) | Viñeta | MB / DoF / Lens / Panini |
|---|---|---|---|---|---|---|---|---|
| `Art/VisualPass/Campaign_PostProfile` (Volume del jugador, prioridad 0) | 0,9 / 0,1 / 0,7 / 12 (HQ off, half) | ACES | — | — | — | Thin1, **0,3**, 0,8 | 0,084, smooth 0,2, color morado (0,18; 0,05; 0,32) | MB `active 0`; los demás no tienen override |
| `Resources/IronRequiem/HG_GameplayVolume` (sustituto en runtime en Room00) | 1,1 / 0,25 / 0,65 / 10 | Neutral | +0,1 / 14 / −14 / (0,93; 0,98; 1) | — | 0 | Medium2, 0,1, 0,7 | 0,2, negro, smooth 0,5 | — (LiftGammaGain igual que el de menú) |
| `Art/Menu/HG_MenuVolume` (**PROTEGIDO**, `HG_PostVolume`, prioridad 20) | 1,1 / **0,6** / 0,65 / 10 | Neutral | +0,1 / 14 / −14 / (0,93; 0,98; 1) | — | **0,12** | Medium2, **0,28**, 0,7 | **0,38**, negro | — |
| `Sector_Room01_Foundry` (prioridad 10) | — | — | +0,1 / 12 / −4 / (1; 0,95; 0,88) | T +12 | — | — | — | — |
| `Sector_Room02_Breach` | — | — | +0,1 / 14 / −12 / (0,94; 0,98; 1) | T −10 | — | — | — | — |
| `Sector_Room03_Assembly` | — | — | +0,1 / 12 / −6 / (1; 0,97; 0,92) | T +5 | — | — | — | — |
| `Sector_Room04_Reactor` | — | — | +0,1 / 15 / −2 / (0,92; 1; 0,98) | T −8, tint −4 | — | — | — | — |
| `Sector_Pursuit05` | — | — | +0,05 / 16 / 0 / (1; 0,9; 0,86) | T +8 | — | — | — | — |
| `CombatClarity` (MainScene y labs; **no está en campaña**) | 0,9 / 0,1 / 0,7 / 65472 | ACES | — | — | — | Thin1, 0,3 | 0,084 | MB `active 0` |
| `VisualRepair/Campaign_Readability` (no está en las escenas de campaña) | 1,1 / 0,08 / 0,7 / 65472 | ACES | — | — | — | grano `active 0` | 0,084 | — |
| `Assets/DefaultVolumeProfile` (perfil por defecto de URP, global settings) | int. 0 | None | 0 / 0 / 0 | 0 | 0 | 0 | 0 | MB y DoF con `active 1` pero modo Off o intensidad 0 (cambio sin commit de 2 líneas). Lens 0, Panini 0. |

### Pila efectiva por sala (escenas de campaña)

Volumes globales encontrados en el YAML:
- Room00b: no tiene ningún Volume.
- Room01–04 y P05: `VisualPass_SectorGrade`, prioridad 10, weight 1, con el perfil `Sector_*`.
- Room00: `HG_PostVolume`, prioridad 20.

El peso del sector llega a 1,00 en ~1,6 s (M1 ya corregido; `MapFxAudit/volume-weight.txt` lo muestra con exposure 0,1, contraste 12/14 y saturación −4/−12 aplicados).

| Sala | Pila (prioridad) | Bloom int. | Tonemap | Exp / contraste / sat. | CA | Grano | Viñeta | MB / DoF / Lens |
|---|---|---|---|---|---|---|---|---|
| Room00_Hangar en campaña | Campaign_Post (0) + HG_PostVolume (20) con **HG_GameplayVolume** sustituido en `Bind` | 0,25 (thr 1,1) | Neutral | +0,1 / 14 / −14 | 0 | Medium2 0,1 | 0,2 negra | 0 / Off / 0 |
| Room00_Hangar fuera de campaña e IntroMenu | … + HG_MenuVolume | 0,6 | Neutral | +0,1 / 14 / −14 | 0,12 | 0,28 | 0,38 | 0 |
| Room00b_AlarmCorridor | solo Campaign_Post | 0,1 (thr 0,9) | ACES | 0 / 0 / 0 | 0 | **Thin1 0,3** | 0,084 morada | 0 |
| Room01_Foundry | Campaign_Post + Sector (10) | 0,1 | ACES | +0,1 / 12 / −4, WB +12 | 0 | 0,3 | 0,084 | 0 |
| Room02_Breach | ídem | 0,1 | ACES | +0,1 / 14 / −12, WB −10 | 0 | 0,3 | 0,084 | 0 |
| Room03_Assembly | ídem | 0,1 | ACES | +0,1 / 12 / −6, WB +5 | 0 | 0,3 | 0,084 | 0 |
| Room04_Reactor | ídem | 0,1 | ACES | +0,1 / 15 / −2, WB −8 / −4 | 0 | 0,3 | 0,084 | 0 |
| Pursuit05 | ídem | 0,1 | ACES | +0,05 / 16 / 0, WB +8 | 0 | 0,3 | 0,084 | 0 |

Nota: el tonemapping es un enum y no se interpola. En Room00, con el Volume de prioridad 20 a weight > 0, manda Neutral. Al salir hacia Room00b (fadeOut), cambia a ACES en cuanto el peso llega a 0.

### E2 / ZONA PROTEGIDA: `ZoneEnvironment` cambia el aspecto de la cinemática

**Qué hace hoy.** `ZoneEnvironment.SceneFade.Bind` (`Scripts/Levels/ZoneEnvironment.cs:78-80`) sustituye `sharedProfile` de cualquier Volume global cuyo perfil se llame `"HG_MenuVolume"` por `Resources.Load("IronRequiem/HG_GameplayVolume")`. Dónde se llama a `Bind`:
- `CampaignFlow.cs:247`: `fadeIn.Bind(scene)` al cargar la etapa 0 = Room00_Hangar.
- `CampaignFlow.cs:204`: `fadeOut.Bind(oldScene)` al salir.

La sustitución ocurre **antes** de que empiece `HangarIntroCinematic`, y `CinematicCamera` usa post con la máscara Default (`Room00_Hangar.unity:45939`). Por eso **la cinemática de despertar, la cinta y la pasarela se ven con el perfil de juego** (CA 0,12→0, grano 0,28→0,1, viñeta 0,38→0,2, bloom 0,6→0,25) siempre que se juegue la campaña. Con Room00 abierta suelta o en IntroMenu no cambia nada: el asset protegido está intacto, como confirmó el guardián.

Riesgos adicionales:
- **a)** La coincidencia es por nombre: cualquier Volume futuro con ese perfil cambiaría.
- **b)** `Editor/HangarIntro/HangarSectorAuthoring.cs:46` (protegido) reasigna HG_MenuVolume si se ejecuta. Es coherente, porque el cambio es solo en runtime.

**Cómo acotarlo sin tocar la zona protegida** (no aplicado; solo código no protegido):

| Opción | Qué | Toca zona protegida | Pros / contras |
|---|---|---|---|
| **P-E2a (recomendada)** | Quitar la sustitución de `Bind`. En su lugar, una vez terminada la cinemática, añadir en runtime **un Volume global nuevo** en la escena Room00 con `HG_GameplayVolume`, **prioridad 21** y weight 0→1 en ~1,5 s (SmoothStep). Así `HG_PostVolume` y su `sharedProfile` no se tocan nunca. Para esperar el final de la cinemática basta su API pública de solo lectura: `HangarIntroCinematic.Finished` / `IsPlaying` (`HangarIntro/HangarIntroCinematic.cs:67-68`, sin editarla). Si no existe cinemática o se salta (el tour usa `RequestSkip`), el cambio se hace al entrar. El Volume nuevo es raíz de la escena Room00, así que el `fadeOut.Bind` de la salida lo recoge y se desvanece con el resto. Archivos: `Scripts/Levels/ZoneEnvironment.cs` (quitar las líneas 61-66 y 80) y un MonoBehaviour nuevo, p. ej. `Scripts/VisualPass/HangarGameplayPost.cs`, que crea `CampaignFlow` tras el BlendIn de la etapa 0 (`CampaignFlow.cs:~282`). | **No** en archivos. Visualmente, la cinemática recupera su aspecto de menú. La sala jugable del hangar (donde también se ven la cinta y la pasarela) sigue con el perfil de juego, y esto **necesita tu decisión**: ¿la protección cubre solo la cinemática o también el aspecto en juego? | Sin "pop", porque CA, grano y viñeta se interpolan por weight. Tonemap Neutral en ambos perfiles, así que no hay salto. Esfuerzo B, riesgo B. Hay que verificar en Play que el Volume temporal no queda tras descargar Room00. |
| P-E2b | Igual, pero cambiando `sharedProfile` de golpe al terminar la cinemática. | No en archivos | Más simple, pero se ve un salto justo cuando la cámara "entra en la cabeza" (el plano `endAtHead`). |
| P-E2c | Revertir E2: perfil de menú también en juego. | No | Vuelve el hallazgo 4 de v1 (CA y grano en juego) y su contribución a IR-03 en el hangar. |

En cualquier opción, sustituir la coincidencia por nombre por otra más estricta: escena `Room00_Hangar` + objeto `HG_PostVolume`, o una referencia serializada al asset.

---

## 3. IR-03 (blur): causas candidatas

| # | Causa | Estado | Evidencia |
|---|---|---|---|
| 1 | Arranque a 960×540 en ventana, estirado | **Confirmada y corregida** (B1). | Antes `defaultScreenWidth/Height 960×540` con `defaultIsNativeResolution 0`. Ahora `defaultIsNativeResolution: 1` (`ProjectSettings.asset:88`) y `fullscreenMode: 1` (FullScreenWindow, l.115). |
| 2 | **Sin bordes a 1600×900 estirado a 1920×1080** (escalado de Windows al 120 %) | **Confirmada como síntoma; causa raíz PENDIENTE.** | Medido: `tour_player_mapfx_release_default.txt` registra `1600x900` (Screen.width/height al inicio del tour). Pantalla: el controlador del panel (Intel UHD, Optimus) da **1920×1080 a 144 Hz** (`Win32_VideoController`), y un proceso sin DPI awareness (PowerShell) ve `Bounds 1600×900` (1920/1,2). **Contradicción**: el manifiesto embebido en `mapfx-release/IronRequiem.exe` declara `<dpiAware>True/PM</dpiAware>` y `<dpiAwareness>PerMonitorV2</dpiAwareness>`, así que Windows **no debería** virtualizar el proceso. No hay entrada de compatibilidad DPI para IronRequiem.exe en `HKCU\…\AppCompatFlags\Layers`. Por eso "Windows le da a Unity el escritorio lógico" no está demostrado: puede ser la lógica de resolución por defecto de Unity en FullScreenWindow o un estado de PlayerPrefs. `Player.log` no registra la resolución. |
| 3 | `RequiemSettings.ApplyDisplayMode` no corrige el primer arranque | **Confirmada (lógica).** | `RequiemSettings.Apply()` solo llama a `ApplyDisplayMode` **si existe la clave** `IronRequiem.Fullscreen` (`Bridges/Game/RequiemSettings.cs:30`). Con las claves borradas (condición de la prueba), nada llama a `SetResolution`. Además, `NativeResolution()` prioriza `Screen.mainWindowDisplayInfo` (l.52); **no está verificado** si devuelve 1920×1080 o 1600×900 con escalado al 120 %. Si devuelve el lógico, el interruptor del menú tampoco arregla nada. |
| 4 | Ajuste de DPI awareness en Player Settings | **No encontrado.** | `ProjectSettings.asset` no tiene ningún campo DPI para Windows. Solo están `macRetinaSupport`, `resolutionScalingMode: 0` (móvil, Fixed DPI) y `resizableWindow: 0`. La awareness ya viene en el manifiesto del exe (PMv2). Un ajuste de Unity 6 para esto: **a investigar** en la documentación de la versión instalada, sin suponerlo. |
| 5 | Argumentos de línea de comandos (solución sin exclusiva) | **Pendiente de probar** (no cambia el modo por defecto). | Unity documenta para el Standalone Player `-screen-width`, `-screen-height`, `-screen-fullscreen` y `-window-mode borderless\|exclusive\|windowed`. Prueba de discriminación sin código: `IronRequiem.exe -screen-width 1920 -screen-height 1080 -window-mode borderless`, registrando Screen.width/height. Si da 1920×1080 sin bordes, la solución es pedir **explícitamente** 1920×1080 en FullScreenWindow (ver B4). Comprobar que esta versión 6000.5 acepta `-window-mode` antes de documentarlo al usuario. |
| 6 | Render scale ≠ 1 o upscaling (FSR, STP, bilinear) | **Descartada.** | `m_RenderScale: 1` y ningún script lo cambia. Upscaling Auto sin efecto a escala 1. |
| 7 | Resolución del RT de cámara o intermedio | **Descartada.** | Sin `m_TargetTexture` y sin resolución dinámica. El intermedio de URP va a tamaño de cámara × renderScale (1). Overlay del arma en el mismo destino. |
| 8 | FXAA o TAA | **Descartada.** | AA None en runtime (override de v06). La base FXAA no se usa. Sin TAA ni STP. El A/B de v1 (`blur-B/C`) dio la misma imagen. |
| 9 | Motion blur / DoF / Lens / Panini | **Descartada.** | Intensidad 0 u Off en todos los perfiles (tabla de la sección 2), y A/B de v1. |
| 10 | Grano | **Contribuye** (percepción de suavidad o ruido, no blur óptico). | Thin1 0,3 en todas las salas salvo Room00 (0,1 con E2). |
| 11 | CA | **Descartada en salas** (0). En Room00 solo con el perfil de menú (0,12): la cinemática en IntroMenu o Room00 suelta. | Sección 2 |
| 12 | Viñeta / bloom | **Descartada como blur.** | Viñeta 0,084; bloom 0,1 (thr 0,9); en Room00 de juego, 0,25. |
| 13 | Filtrado anisotrópico | **Contribuye** (suelos borrosos en ángulo rasante). | Quality `anisotropicTextures: 1` (por textura). Metas de textura en `Assets/IronRequiem` + `Assets/FPS`: **140 con `aniso: 1`**, 53 en −1, 27 en 0, 32 en 4 y 7 en 16. |
| 14 | Calidad o mip de textura | **Descartada.** | `textureQuality: 0` (completa) y sin mip streaming. 106 texturas sin mipmaps, que producen aliasing y no blur. |
| 15 | Aliasing confundido con blur (sin AA) | **Contribuye** a la mala nitidez percibida (escaleras y parpadeo). | AA None (fila 8). |

**Conclusión IR-03:** la única causa de blur real que queda es la resolución de presentación (1600×900 estirada) en el modo sin bordes elegido. Grano y aniso suman. Lo siguiente sería **diagnosticar** (filas 2, 3 y 5) antes de dar la limitación por inevitable: el manifiesto PMv2 indica que un sin bordes a 1920×1080 reales debería ser posible.

---

## 4. Luces

Recuento estático del YAML. En todas las escenas, todas las luces son tiempo real (`m_Lightmapping: 4`). Las escenas no tienen LightingData (`fileID 20201` por defecto), occlusion culling ni LODGroups.

| Escena | Luces | Tipos | Con sombra | Comentario |
|---|---|---|---|---|
| Room00_Hangar (**protegida**) | **31** | 17 spot, 13 point y 1 directional | 1: `KeySpot` (spot, soft, r 11, I 16), que pasa al atlas de sombras adicionales | Down×6, PitGlow×4, Spot×4, RobotWash×3, ScrapGlow×2, … Con 753 renderers y 2,05 M tris (v1) es la sala con el peor p99 Release: 11,6 ms. |
| Room00b_AlarmCorridor | **17** | 9 spot y 8 point | 0 | AlarmRed×8, Down×5, BeaconSpot×4. Con el **límite de 4 por objeto en Forward**, los objetos iluminados por más de 4 descartan luces (posible "pop" al moverse). |
| Room01–04 | 4 cada una | 4 point (`CeilingLight`) | 0 | Iguales en las 4 salas (v1, hallazgo 6). |
| Pursuit05 | 6 | 6 point | 0 | — |
| RequiemCampaign (persistente) | 12 | 11 point y 1 directional (`LabSun`) | 0 | CeilingLight×7, CycleLamp×2 (airlock), Glow y VestibuleDim. Hay 1 componente desactivado. |

**Luces de efectos (prefab).** Cada una tiene 1 point light **desactivada por defecto** y sin sombra, que encienden los scripts del efecto:
- `RobotBodyBurst_CombatPolish_IR` y `_VO`
- `CannonExplosion_IR` y `VP_CannonExplosion_IR`
- `T07_BodyExplosion_VO`
- `MountStation_C04`

Ningún script crea luces en runtime (no hay `AddComponent<Light>` en `Scripts/`).

**Medido en el Editor** (`fx-perf.txt`): 14–16 point/spot activas en combate.

**Sombras.** El coste de mapas de sombras en salas es prácticamente nulo: la directional no tiene sombra y no hay otras luces con sombra. Solo Room00 renderiza el atlas adicional por `KeySpot`.

---

## 5. Protocolo de medición IR-04

**Estado actual.** Release a 1080p exclusiva: medias de 5,2–5,8 ms (174–192 FPS) en combate y 3,9 ms en Pursuit. **p99** por sala:

| Sala | p99 |
|---|---|
| Hangar | **11,6 ms** |
| Pasillo | **9,1 ms** |
| Assembly | **8,4 ms** |
| Foundry | 7,85 ms |
| Breach | 7,3 ms |
| Reactor | 7,0 ms |

Las tres primeras **superan los 8,33 ms (120 FPS)**. Ir de 1600×900 a 1920×1080 (+44 % de píxeles) cuesta +0,3–0,8 ms por sala (Hangar 5,06→5,76; Foundry 5,13→5,57), así que hay un coste dependiente de resolución aunque el informe marque "espera present/GPU 0.0".

### Build
- `VisualOverhaulBench.BuildGame("mapfx-release")` (`Editor/VisualOverhaul/VisualOverhaulBench.cs:69-77`) mediante la cola `Tools/IronRequiem/Requests/visual-overhaul.txt`.
- Hace un build Release (`BuildOptions.None`) de las escenas activas de Build Settings, **sin tocar ProjectSettings**.
- Por eso `enableFrameTimingStats: 0` (`ProjectSettings.asset:159`): **no hay tiempo de GPU** (FrameTimingManager). La bench de CombatLoad (l.33-53) sí lo activa temporalmente. Proponer una variante `BuildGameTimed` que haga lo mismo (snapshot y restauración).

### Ejecución
- `Tools/IronRequiem/Builds/VisualOverhaul/mapfx-release/IronRequiem.exe -irTour -irFullHD`.
- `-irFullHD` pide exclusiva 1920×1080 y **aborta con Quit(3)** si no la obtiene (`CampaignTourBenchmark.cs:102-112`).
- Opcionales: `-irNoWarm`, `-irNoTeardown` e `-irMarkers`. Este último registra hasta 3000 marcadores: solo para diagnóstico, porque añade coste y no sirve para certificar.
- Salida: `ir_tour.txt` junto al exe. Copiarlo a `Reports/Perf/tour_player_<tag>.txt`.
- Condiciones que hay que registrar:
  - Portátil enchufado, plan "Alto rendimiento" y GPU NVIDIA (el panel lo atiende la Intel por Optimus).
  - `nvidia-smi` antes y después.
  - Sin otras apps.
  - 3 pasadas por variante en orden A/B/A/B, informando la mediana.

### Qué cubre el tour
- 6 transiciones: pico, número de frames >33/50 ms, GC y fases de CampaignFlow.
- 3 s de idle en Hangar y Pursuit.
- **6 s de la primera ronda** de cada sala de combate (3–6 enemigos), con el jugador **invencible, quieto y sin disparar**.
- La cinemática se salta con `RequestSkip`.

### Qué NO cubre
- Rondas 2–3. En el Editor, Room03 R3 tiene 22 enemigos, 1,2 M tris, ~830–1370 partículas y 90–133 sistemas (`horde.txt`). Room04 R3 tiene 27 enemigos.
- **Muertes en cadena.** El tour mata un enemigo por frame tras la muestra (`:215-218`), pero **ese tramo no se mide**.
- Disparo del jugador (boca, impactos, proyectiles), OVERDRIVE, carga del cañón, ejecución y extracción.
- Carga sostenida de 60 s o más (térmica y GC).
- Tiempo de GPU.
- Menú y cinemática.

### Cómo añadir carga comparable (antes/después)
Propuesta: solo código de `Scripts/Diagnostics/CampaignTourBenchmark.cs`, que no es zona protegida.
1. **Medir el tramo de limpieza que ya existe**: `Begin()` antes del bucle de kills y `End()` al completar las rondas, más 3 s de asentamiento. Línea "deaths" por sala con avg, p99 y pico. Da el coste de las muertes en cadena **sin cambiar la carga**.
2. `-irAllRounds`: por cada ronda, esperar a `PendingSpawns==0`, tomar 6 s de muestra (con el recuento de enemigos) y matar. Una línea por ronda (R1/R2/R3).
3. `-irBurst N`: matar N enemigos en el mismo frame. Es el peor caso de la cadena `RobotBodyBurst_IR` + debris + luz.
4. `-irFire`: mantener el fuego primario con un `Mouse` virtual del Input System, como el arnés de `MapFxAudit`/`CampaignTests`, durante la muestra.
5. `-irSustain 60`: mantener viva la R3 de Room03/04 durante 60 s.
6. Fijar `UnityEngine.Random.InitState(semilla)` al inicio y registrar la semilla. La IA sigue sin ser determinista por el tiempo, así que el criterio es A/B/A/B con 3 repeticiones y comparar medianas de avg y p99.
7. Contar partículas y sistemas vivos cada 0,5 s fuera de la ventana de muestra, o como pico al final del tramo, para no contaminar el tiempo de frame.

**Criterio IR-04** (propuesta): avg ≤ 8,33 ms **y** p99 ≤ 11 ms en todas las líneas (R1–R3, deaths y fire), con 0 frames >33 ms en combate.

---

## 6. Propuestas

Impacto, esfuerzo y riesgo: A = alto, M = medio, B = bajo. La nomenclatura sigue la de v1: R1–R5 y B1–B3 ya existen; las nuevas continúan la numeración.

### Rendimiento

| ID | Propuesta | Impacto | Esfuerzo | Riesgo | Archivos | ¿Zona protegida? |
|---|---|---|---|---|---|---|
| R1 | Línea base Release a 1080p | — | — | — | **Hecha** (primera ronda) | No |
| **R6** | Ampliar el tour: medir el tramo de muertes y añadir `-irAllRounds`, `-irBurst`, `-irFire`, `-irSustain` y semilla (sección 5) | **A** (habilita IR-04) | M | B | `Scripts/Diagnostics/CampaignTourBenchmark.cs` | No |
| **R7** | Build de bench con `enableFrameTimingStats` temporal, para tener ms de GPU y CPU separados en Release | M | B | B (snapshot y restauración de ProjectSettings) | `Editor/VisualOverhaul/VisualOverhaulBench.cs` | No |
| R4′ | SSAO: `Source` DepthNormals→**Depth** (elimina el prepass DepthNormals, que duplica las draw calls de opacos) y/o `Downsample` on. Con intensidad 0,3 y radio 0,035 el efecto visual es mínimo. Alternativa: desactivar la feature. Medir con R7. | M (CPU de render y GPU) | B | B (visual) | `Assets/Rendering/Default_PipelineAsset_ForwardRenderer.asset` (respaldo; **requiere tu OK**: es un asset del renderer, no un cambio de pipeline) | No; afecta a todo, IntroMenu incluido, así que comparar la captura del menú |
| **R8** | Hangar, peor p99 (11,6 ms): 31 luces en tiempo real, 2,05 M tris y 753 renderers. Opciones: (a) medir con `-irMarkers` qué domina; (b) desactivar en runtime, al terminar la cinemática, luces **no protegidas** fuera de vista (`City` y `SectorLamps` sin MenuFlicker), desde un script de campaña; (c) LODs/culling en una copia | M | M | M | Script nuevo en `Scripts/Levels/`; o copia `Room00_Hangar_v2` | **Sí** (aspecto de la sala). Solo (a) es libre; (b) y (c) requieren tu decisión |
| **R9** | Pasillo (17 luces, p99 9,1 ms): reducir solapes por encima de 4 luces por objeto (AlarmRed×8) o ajustar rangos | B–M | B | B | `Room00b_AlarmCorridor.unity` (copia `_v2` o edición con Editor) | No |
| R2 | Visibilidad SawBot/BigBot cacheada por frame y detección escalonada (v1) | A en hordas | M | M | `AI/SawBotCombat.cs`, `AI/BigBotCombat.cs` | No |
| R3 | Pool de actores y de los sistemas de muerte (v1) | A en hordas | A | M | `Levels/EnemySpawnQueue.cs`, `EnemyController` | No (`Carronero.prefab` es compartido: usar variantes) |
| E3 (v1) | Límite global de debris, reposo a los 3-5 s y pool | M | M | B | `MechDebrisPiece.cs`, `MechDeathExplosion.cs` | No |
| R5 | HUD sin strings por frame (v1) | B | B | B | `UI/MechanicHud.cs` | No |
| **R10** | Volume framework en "ViaScripting" para la cámara overlay del arma (post off) | B (0,1–0,3 ms estimado, **no medido**) | B | B | Variante del prefab del jugador | No |

No se recomienda: Forward+, Deferred, GPU Resident Drawer, STP ni cambios de pipeline o paquetes (fuera de alcance por regla).

### Post y blur

| ID | Propuesta | Impacto | Esfuerzo | Riesgo | Archivos | ¿Zona protegida? |
|---|---|---|---|---|---|---|
| **B4** | **Diagnóstico de resolución sin exclusiva.** (1) Probar `-screen-width 1920 -screen-height 1080 -window-mode borderless`. (2) Registrar en `Player.log` al arrancar `Screen.width/height`, `Screen.currentResolution`, `Screen.mainWindowDisplayInfo`, `Display.main.systemWidth/Height` y `Screen.resolutions` máx. Si hay 1920×1080 físicos disponibles, `NativeResolution()` debe usar el máximo de `Screen.resolutions` o `Display.main.systemWidth`. Aplicarlo **también en el primer arranque** (sin clave en PlayerPrefs) con un `RuntimeInitializeOnLoadMethod` en un archivo nuevo, para no tocar `Scripts/Menu/RequiemMenuView`. | **A** (IR-03) | B | B | `Bridges/Game/RequiemSettings.cs`, archivo nuevo `Scripts/Display/DisplayBoot.cs` | No en archivos. Cambia la resolución del menú en build (global, como B1) |
| B2 | Grano de 0,3 a ≤0,1 en `Campaign_PostProfile` y SMAA Low/Medium en la cámara del jugador, en una variante v07/v08, no en `Player.prefab` | M | B | B | `Art/VisualPass/Campaign_PostProfile.asset`, variante del jugador | No (Campaign_PostProfile no está en la lista roja) |
| B3 | Aniso: subir a 4–8 las texturas de suelo y pared de salas, o `anisotropicTextures: 2` (Force) en Quality (respaldo) | M | B | B | `.meta` de texturas no compartidas o `QualitySettings.asset` | El Force global afecta a IntroMenu y Room00, y las texturas `Bunker_RustMetal_*` y `Hangar_*` están en la lista roja. Para no tocar la zona, solo .meta de texturas no compartidas |
| **P-E2a** | Acotar el cambio de perfil del hangar a después de la cinemática con un Volume adicional (prioridad 21, rampa) y matching estricto (sección 2) | A (protege la cinemática) | B | B | `Scripts/Levels/ZoneEnvironment.cs`, `Scripts/Levels/CampaignFlow.cs`, nuevo `Scripts/VisualPass/HangarGameplayPost.cs` | **Sí, para corregir**: devuelve a la cinemática su aspecto original. Queda por decidir el aspecto de Room00 jugable |
| B5 | Bloom clamp: `CombatClarity` y `Campaign_Readability` tienen clamp 65472 (fireflies sin límite). No afecta a la campaña, solo a labs y MainScene | B | B | B | `Art/Rendering/CombatClarity.asset` | No |
| B6 | Recomprometer o revertir `Assets/DefaultVolumeProfile.asset` (MB y DoF `active 1`, sin efecto visual): limpieza de git | B | B | B | — | No |

**Orden sugerido:**
1. **B4** (diagnóstico, barato; puede cerrar IR-03 sin exclusiva).
2. **P-E2a** (devuelve la zona protegida a su estado).
3. **R6 + R7** (instrumentar la carga real).
4. Optimizar con datos: R4′, R2, R3/E3 y R8 según lo que muestren los marcadores.
