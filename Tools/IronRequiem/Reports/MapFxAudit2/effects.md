# IRON REQUIEM: inventario de efectos de juego (MapFxAudit2)

Fecha: 17/11/2026. Solo lectura: código, YAML de prefabs/escenas y datos ya medidos. No se abrió el Editor ni Play Mode.
Este documento **actualiza** `Docs/IronRequiem/MAP_FX_AUDIT_20261117.md` (v1). E1 y E2 ya están aplicados. No repite sus fichas: las corrige o amplía.

## 0. Base de datos usada y qué cambió desde v1

- **Capturas y datos actuales** (`Tools/IronRequiem/Reports/MapFxAudit/`, 03:37–03:48): son **posteriores** a E1 (`VisualOverhaul/deathfx-apply.txt`, 03:29).
- **Datos anteriores:** están en `MapFxAudit_before_20261117/` (03:16).
- **Comparación Editor antes → después** (solo tendencia; ver los límites en v1):

| Muestra | Partículas máx. | Sistemas vivos | SetPass máx. | Main p50/p95 (ms) |
|---|---|---|---|---|
| Room01, pelea | 259 → 222 | 32 → 34 | 227 → 241 | 17,9/26,3 → 19,3/27,1 |
| Room03 R2 | 852 → 862 | 96 → 89 | 337 → 306 | 22,4/34,3 → 22,0/30,7 |
| Room03 R3 | 826 → **1373** | 90 → **133** | 262 → 309 | 22,9/31,5 → 24,0/34,3 |

  Las rondas no son deterministas: R3 tenía 18 vivos al cerrar la muestra. **No se puede atribuir el +66 % de partículas de R3 a E1** sin semilla fija (ver P-FX1).
- **Volumen de sector:** `volume-weight.txt` ya muestra w=1,00 a los 1,6 s. M1 también está aplicado: `VolumeFadeIn.cs:7-10` y `Claim()`.
- **Capturas posteriores a E1** (`fx-death-0..5`, `seq-room03-round2-live-1`): ya no hay tinte naranja a pantalla completa ni cuadrados negros gigantes. Pero **ninguna captura coincide con el instante de la detonación**: solo se ven restos, humo y el destello rojo de golpe del T-07 (`fx-death-0.png`). La legibilidad de la explosión _IR sigue **sin evaluar** (pasa a E6/P-FX1).

### Correcciones a la v1
1. **"Assets huérfanos" de v1: falso.**
   - `VFX/CannonCharge/EnergyStripe.asset` y `MagentaCore.asset` son texturas de `EnergyFrame.mat` y `EnergyCore.mat`.
   - `VFX/CannonExplosion/PressureEdge.asset` y `SmokeLobe.asset` son texturas de `PressureWave.mat` y `ResidualSmoke.mat`.
   - Esos 4 materiales están en uso (CannonCharge_Magenta, CannonExplosion_IR y T07_Mech). **No borrar.**
2. **"BigBot_ReferenceCharge_v09 solo labs": falso.**
   - `Prefabs/Campaign/BigBot_Current.prefab` es la cadena de variantes `BigBot_Burst_v10_NavThrottle_VO` → `BigBot_Burst_v10_NavThrottle` → … → `Prefabs/Enemies/BigBot_Burst_v10.prefab` (guid d1ca4ab0…), que anida `BigBot_ReferenceCharge_v09` (18 referencias).
   - Por tanto, el vórtice azul/violeta de `VFX/BigBotChargeV09/*` **está en campaña** (Room01-04).
3. **"`VFX_HoverBot_Explosion` duplicado en T07_Mech": no ocurre.**
   - `EnemyController.OnDie` (`FPS/Scripts/AI/EnemyController.cs:423-428`) solo instancia `DeathVfx` si **no** hay un `IEnemyDeathPresentation` o si este devuelve false.
   - T07_Mech tiene `RobotDamageProgression` con `preserveLegacyDeathEffect: 0` (`T07_Mech.prefab:15187`). Lo confirma `VisualOverhaul/deathfx-analyze.txt`: "not spawned" en los 7 prefabs de campaña.
   - El duplicado real es otro: hay dos rutas de burst con prefabs distintos (hallazgo F1).
4. **HeatVentFx, "brillo invisible con celdas":** es intencional (`HeatVentFx.cs:29-34`). Con `WeaponFuelCellHandler` el vapor sale de la celda 0 y el "glow" lo hacen las celdas rojas de `PrimaryModuleHeatPresentation`. No está roto. Queda pendiente verlo en movimiento.

---

## 1. Hallazgos nuevos (ordenados por peso)

| # | Hallazgo | Tipo | Evidencia |
|---|---|---|---|
| F1 | **El pool de muerte usa un único prefab para toda la sala, y lo decide quien hace `Awake` primero.** Muertes del mismo tipo pueden verse distintas según el orden de aparición, y la mitad de E1 (destello `VO_Flash`) puede no verse nunca en una sala. | **ROTO (parcial de E1)** | Ver detalle bajo la tabla |
| F2 | **DamageDirectionHud está 2 veces en RequiemCampaign** (igual que `VP_OverdriveEdge`, E5). | sospecha de **duplicado** | Ver detalle bajo la tabla |
| F3 | **Presupuestos de luz separados.** Los dos destellos pueden encender hasta 4 point lights transitorias, además de las 4 de la sala y las de alarma o pasillo. | DÉBIL | Ver detalle bajo la tabla |
| F4 | **Restos persistentes con `Update` permanente.** | COSTOSO | Ver detalle bajo la tabla |
| F5 | **Coste de spawn atribuible a efectos.** Se suma al spawn sin pool de actores (R3 de v1). Hipótesis para los p99 de 212–224 ms en Room03 R1/R2 (`horde.txt`); no está medido por separado. | sospecha (medir) | Ver detalle bajo la tabla |
| F6 | **Sin soft particles posibles:** ninguna textura de profundidad. Los billboards grandes cortan el suelo y las paredes con aristas duras. | DÉBIL | Ver detalle bajo la tabla |
| F7 | **`MaterialPropertyBlock` en renderers de efecto** saca a esos renderers del SRP Batcher. | sospecha (medir) | Ver detalle bajo la tabla |
| F8 | **Riesgos de `ShotFxLibrary`.** | DÉBIL / riesgo | Ver detalle bajo la tabla |
| F9 | **Hit-stop sin enfriamiento en muertes encadenadas.** | sospecha de diseño | Ver detalle bajo la tabla |
| F10 | **Cadenas `_VO_VO…` vivas en prefabs de laboratorio.** Las de proyectiles y fogonazos son variantes vacías (solo nombre y trail `m_Time`), sin escalado acumulado. | deuda | Ver detalle bajo la tabla |

**F1. Un único prefab de pool por sala.**
- `RobotDestructionPool` guarda un solo `burstPrefab` estático y, si ya existe `root`, ignora las llamadas siguientes (`RobotDestructionPool.cs:16,25`).
- Hacen `Prepare` cuatro sitios con prefabs distintos:
  - `BigBotDestruction`, con el **original** `RobotBodyBurst_CombatPolish` sin luz y a escala 1,55 (`AI/BigBotDestruction.cs:71`).
  - `SawBotCombat`, con el **original** (`AI/SawBotCombat.cs:139`; afecta a SawBot_Current y Carronero).
  - `PooledEnemyDeathBurst` y `RobotDamageProgression`, con **`_IR`** (HoverBot, Turret, T07_Current, T07_Mech: `Enemies/PooledEnemyDeathBurst.cs:14`, `Enemies/RobotDamageProgression.cs:41`).
- En Room01-04 conviven BigBot, SawBot, HoverBot y T07.
- Fuentes: código citado y `deathfx-analyze.txt`.

**F2. DamageDirectionHud y `VP_OverdriveEdge` duplicados.**
- Las dos instancias de `VP_DamageDirection` están en `RequiemCampaign.unity:48389` (bajo la instancia de `GameHUD.prefab`) y `:48865` (bajo la instancia de `GameManager.prefab`). Las dos tienen `m_IsActive 1` y `m_Enabled 1`.
- Si ambos canvas están activos, cada golpe pinta 2 cuñas con alfa 0,85 superpuestas.
- Mismo patrón para `VP_OverdriveEdge` (`:16214` bajo GameManager y `:36381` bajo GameHUD).
- Que el canvas padre esté activo en runtime solo se confirma en Play Mode.
- Código: `VisualPass/DamageDirectionHud.cs:32-36`.

**F3. Presupuestos de luz separados.**
- `BurstFlashLight.MaxLit = 2` (rango 9, I=9, 0,16 s, sin sombras; `VisualOverhaul/BurstFlashLight.cs:11`).
- `CannonExplosionPresentation` lleva otro contador `liveLights<2` (rango 5, I=7, 0,12 s; `Weapons/CannonExplosionPresentation.cs:217` y `CannonExplosion_IR.prefab`).
- URP Forward tiene `m_AdditionalLightsPerObjectLimit: 4` (`Assets/Rendering/Default_PipelineAsset.asset:48`). Por eso, en una muerte múltiple cerca de una explosión de módulo, los objetos pueden perder luces de la sala durante 0,1–0,2 s (parpadeo de iluminación).

**F4. Restos persistentes con `Update` permanente.**
- `MechDebrisPiece.Update` sigue corriendo en reposo (`MechDebrisPiece.cs:27-36`): suma `age` y compara cada frame durante 22–60 s.
- Piezas por prefab: BigBot 21, Carronero 22, T07_Current 20, T07_Mech 15, SawBot 11, HoverBot 6, Turret 3 (conteo de componentes en `Prefabs/Campaign/*.prefab`).
- `pieceSeconds` (YAML): BigBot, SawBot, T07, Turret 60; HoverBot 45; Carronero 22.
- Room03 R3 (22 enemigos) → **≈250–350 renderers de restos y otros tantos `Update`** al final de la ronda. No hay tope global (E3 de v1 sigue vigente).
- `RobotArmorDebris.Update` (`Enemies/RobotArmorDebris.cs:39-55`) reescribe `localScale` cada frame aunque esté en reposo (24 slots).

**F5. Coste de spawn atribuible a efectos.**
- **Cada enemigo crea un ParticleSystem en `Awake`:** `MechDeathExplosion` usa `new GameObject` + `AddComponent<ParticleSystem>` (`MechDeathExplosion.cs:49-60`).
- **El primer `Prepare` del pool instancia de golpe:** 16 bursts de unos 10 sistemas cada uno, más 24 objetos de restos (`RobotDestructionPool.cs:29-42`). Ocurre en el `Awake` del primer enemigo, que puede ser durante el spawn de la ronda.

**F6. Sin soft particles posibles.**
- `m_RequireDepthTexture: 0` (`Default_PipelineAsset.asset:22`) y todos los materiales de `VFX/*` tienen `_SoftParticlesEnabled: 0`.
- Ejemplos: `ResidualSmoke` (alpha, 16 partículas máx.) y `VFX_ImpactRing` (disco de 4,05 m en `_IR`).

**F7. `MaterialPropertyBlock` en renderers de efecto.**
- Por frame mientras actúan:
  - `EnemyStateLights` en Warning (`EnemyStateLights.cs:78-82`).
  - `RobotHitFeedback` durante el destello (`RobotHitFeedback.cs:34`, con `SetColor("_EmissionColor")` por string).
  - `CannonChargePresentation.UpdateReference`, con `GetColor`/`SetColor("_BaseColor")` por string y por renderer cada frame (`CannonChargePresentation.cs:145-153`; afecta al vórtice v09 del BigBot).
  - El anillo de `CannonExplosionPresentation` (`:237`).
- `PrimaryModuleHeatPresentation` solo escribe cuando cambia el ratio.
- No genera GC, pero puede subir SetPass y Batches.

**F8. Riesgos de `ShotFxLibrary`.**
- `Cache` estático `Dictionary<Collider,int>` (`ShotFxLibrary.cs:19-26`) se limpia solo en `BeforeSceneLoad`, una vez por sesión. Acumula los colliders de todas las salas cargadas y descargadas (claves destruidas retenidas).
- `Classify` concatena strings y hace `ToLowerInvariant` la primera vez que se golpea cada collider (`:42-43`). Son asignaciones puntuales en combate.
- `c.GetComponent<Renderer>() ?? c.GetComponentInParent<Renderer>()` (`:41`): `??` sobre `UnityEngine.Object` se salta la comparación con null de Unity. Lo mismo en `FxPool.cs:26` (`GetComponent<PooledFx>() ?? AddComponent`).
- Funciona hoy, pero debería ser `TryGetComponent`.

**F9. Hit-stop sin enfriamiento en muertes encadenadas.**
- `HeavyDeathFeel` aplica 0,04–0,08 s de hit-stop a escala 0,06 por muerte pesada (BigBot, SawBot, Carronero y T07_Mech; `HeavyDeathFeel.cs:24-32`).
- `ExtractionArcFx` suma 0,06 s (`ExtractionArcFx.cs:56`).
- `GameFeel.HitStop` solo encadena con `max()` (`GameFeel.cs:31`). Matar en serie cada 0,3 s puede dejar más del 15 % del tiempo congelado.
- No está medido: solo se ve jugando (P-FX1 lo registra).

**F10. Cadenas `_VO_VO…` vivas en prefabs de laboratorio.**
- La cadena de burst sigue referenciada por prefabs de laboratorio: `RobotBodyBurst_CombatPolish_VO_VO_VO` (×36,6 de tamaño, `ImpactFlare` de 219,9 m, `deathfx-analyze.txt`) lo usan `HoverBot_CombatPolish_VO`, `T07_HitFeedback_v04_NavThrottle_VO`, `T07_LegRepair_Charged_v05_Execution_VO`, `Turret_CombatPolish_VO` y `VisualPass/Mech/T07_Mech_Test`.
- `T07_HitFeedback_v04_NavThrottle_VO` es la base de `T07_Current_Repaired`, pero esta lo sobrescribe con `_IR` (`T07_Current_Repaired.prefab:2454`).
- `T07_BodyExplosion_VO_VO_VO` (startSize hasta 30,4) lo usa `T07_Common_Destructible_VO` (labs: ProtagonistV02Lab, MainScene_VisualLab, WeaponsRedesignLab).
- `m_MaxParticleSize 0,3` limita cada partícula al 30 % de la pantalla, pero no el número de capas.

---

## 2. Inventario efecto por efecto

Leyenda:
- **Pool:** S = sí, N = no.
- **Asignaciones por frame:** solo lo visible en el código.
- **Mat:** A = aditivo (`_Blend 2`, URP Particles/Unlit `0406db5a…`), α = alpha blend, O = opaco.
- maxParticles según el YAML del prefab o el código.

### 2.1 Scripts/VisualPass

| Efecto (archivo) | Qué hace | Disparador | Prefab o asset | Pool | Asignaciones por frame | Partículas / luces | Mat | Estado y lectura en combate |
|---|---|---|---|---|---|---|---|---|
| `MuzzleShotFx.cs` | Bocanada de humo en la boca | `WeaponController.OnShootProcessed` (por disparo) | `Prefabs/VisualPass/VFX/VP_MuzzleSmoke.prefab` | S (`FxPool.Spawn`, vida 0,9 s) | 0 por frame. Por disparo, `GetComponent<PooledFx>` en `FxPool.cs:31` | 3, sin luz | α (`VP_ShotSmoke`) | **bien**: barato y no tapa (`seq-room01-fight-live-*`) |
| `ShotFxLibrary.cs` (SO en `Resources/IronRequiem/ShotFxLibrary.asset`) | Impacto de superficie: metal o polvo; los robots conservan el impacto propio del arma | `ImpactSurface.Resolve` desde ProjectileStandard al impactar | `VP_Impact_Metal` (Sparks 16 + Flash 1) y `VP_Impact_Dust` (Puff 6 + Chips 8), vida 1,6 s | S (FxPool) | Primer impacto por collider: concatenación, `ToLower` y `GetComponentInParent` ×2 (F8) | 17 / 14; sin luz | Metal A; Polvo α | **débil**: pierde la identidad del arma en el entorno (v1). Caché sin limpiar (F8) |
| `HeatVentFx.cs` | Brillo en la boca según calor, más vapor al enfriar o sobrecalentar | `LateUpdate` leyendo `WeaponHeat` | 2 PS creados en `Awake` (`:64-85`) | N/A (persistente) | 0 (array `one[1]` reutilizado) | Vapor 40 (máx. en pantalla 0,12); brillo 1 | Por material asignado | **bien** por diseño (corrección 4). Visual pendiente (E6) |
| `ExtractionArcFx.cs` | 2 arcos LineRenderer de 9 puntos, chispas y trozos en el tirón | `StolenWeaponCarrier.Executed`; dispara tras `DisarmYankTime` + 0,02 s; también hit-stop de 0,06 | 2 rigs `DontDestroyOnLoad` creados en `Awake` | S (2 rigs en anillo) | 0 (buffer reutilizado) | Chispas 26 (Stretch) y trozos 6 (malla); sin luz | A / O | **bien** en lectura (0,26 s, `fx-execution-extraction-0..5`). **Paleta**: azul (1,6; 1,8; 3) y gradiente a (0,4; 0,6; 1); choca con el resto (E4) |
| `MechDeathExplosion.cs` | Esconde el cuerpo y lanza las piezas pre-horneadas, con chispas | `Health.OnDie`, retardo de 0,28 s (`delay`) | Piezas hijas inactivas (`MechDebrisPiece`); PS de chispas creado en `Awake` | Piezas: sí (pre-horneadas, sin `Instantiate`). Chispas: N (`Destroy(…,1,2)` en `:112`) | Por muerte: `p.name.Contains` (asigna string por pieza, `:96`), `GetComponent<Renderer>` y `bounds` por pieza (`:105`), `Camera.main` | Chispas 40 por enemigo (`sparkCount: 40` en todos los prefabs); sin luz | `sparkMaterial` (Stretch) | **bien** como lectura de "se rompe". **costoso** en spawn (F5) y restos (F4) |
| `MechDebrisPiece.cs` | Balística barata con SphereCast, 4 rebotes, reposo y fundido al final | `MechDeathExplosion.Explode` | — | Pre-horneado | `SphereCast` por frame en vuelo; `Update` también en reposo | — | Material del robot | **costoso** (F4). Visualmente bien: restos en el suelo en `seq-room03-round2-live-1.png`, `fx-death-*` |
| `HeavyDeathFeel.cs` | Hit-stop de 0,08, sacudida en radio de 22 m y patada de cámara | `Health.OnDie` (BigBot, SawBot, Carronero, T07_Mech) | — | — | `Camera.main` por muerte | — | — | **bien** suelto; riesgo en cadena (F9) |
| `GameFeel.cs` | Hub de hit-stop y escala de sacudida (PlayerPrefs) | `HitStop` / `HitStopThenSlow` | Runner oculto `DontDestroyOnLoad` | — | 0 | — | — | **bien**: respeta la pausa (`:27`). Sin enfriamiento (F9) |
| `OverdriveScreenFx.cs` | Borde naranja que pulsa de 2,2 a 7 Hz según peligro | `LateUpdate` sobre `carrier.Held.Overdrive.IsArmed` | `Image` en `VP_OverdriveEdge` | — | 0 | — | UI | **duplicado** (E5; F2: `RequiemCampaign.unity:16214` y `:36381`). Alfa 0,10–0,32 ×1,15: con dos instancias puede llegar a ≈0,6 |
| `SlideGuardScreenFx.cs` | Borde cian proporcional a `SlideGuardFeedback.EdgeIntensity` | `LateUpdate` | `Image` (1 instancia en RequiemCampaign) | — | 0 | — | UI | **débil / pendiente**: alfa máx. 0,55, el más alto de los bordes; no sale en las capturas de cámara (v1) |
| `DamageDirectionHud.cs` (+ `DamageWedgeGraphic.cs`) | 4 cuñas rojas alrededor de la mira que siguen al atacante 0,85 s | `Health.OnDamaged` del jugador | Creadas en `Awake` | S (4) | 0 | — | UI | **sospecha de duplicado** (F2). Pendiente en la Game view |
| `HudHealthTint.cs` | Color de la barra de vida y pulso bajo el 25 % | `LateUpdate` | — | — | 0 (pero escribe `Graphic.color` cada frame y rehace la malla de UI aunque no cambie) | — | UI | **bien**; coste menor de UI (P-FX6) |
| `SectorLightingBlend.cs` | Interpola ambiente y niebla 1,4 s al cambiar la escena activa | `SceneManager.activeSceneChanged` | Componente en RequiemCampaign | — | `DynamicGI.UpdateEnvironment()` **cada frame** durante el blend (`:49`) | — | — | **bien** visualmente; doble escritor con ZoneEnvironment (v1). Coste del `UpdateEnvironment` sin medir (P-FX5) |
| `VolumeFadeIn.cs` | Fundido del peso del volumen de sector (corregido con M1 y `Claim`) | `OnEnable` | En Room01-04, Pursuit05 y `*_MechTest` | — | 0 | — | — | **bien** (`volume-weight.txt`: w=1,00 a 1,6 s) |
| `EnemyMuzzleAnchor.cs` / `StolenChargeAnchor.cs` | Colocan la boca (y la carga) en la boca medida del cañón; vibración, hinchado y retroceso al cargar | `LateUpdate` (orden 220/210) | — | — | 0 (medición única) | — | — | **bien**: ancla de efectos, no es un efecto |
| `WeaponDeployMotion`, `AirlockDoorMotion`, `HudBannerPlate`, `ShakeSliderBinding`, `StolenShellFit`, `MenuShellMotion` | Movimiento o UI, no VFX | — | — | — | — | — | — | Fuera de alcance. `MenuShellMotion` puede rozar el menú: **no tocar** |

### 2.2 Scripts/VisualOverhaul

| Efecto | Qué hace | Disparador | Pool | Asignaciones por frame | Luces | Estado |
|---|---|---|---|---|---|---|
| `BurstFlashLight.cs` | Point light breve por burst del pool | `Update` que observa `RobotBodyBurst.HasBurst` | Va en los 16–32 slots del pool | 0 | Máx. 2 encendidas (estático), rango 9, I 9→0 en 0,16 s, sin sombras (`RobotBodyBurst_CombatPolish_IR.prefab`, `VO_Flash`) | **bien** en sí mismo. **Roto en la práctica** cuando el pool lo prepara un BigBot o SawBot (F1: el original no lleva `VO_Flash`). Presupuesto separado del de CannonExplosion (F3) |
| `EnemyStateLights.cs` | Emisivo por estado: patrulla teal, alerta ámbar, ataque rojo, aviso blanco intermitente a 14 Hz | `Update` por enemigo; MPB solo al cambiar (cada frame en Warning) | — | 0 GC; MPB (F7) | Ninguna (emisivo) | **débil en T-07**: los 4 estados en rojo por `VisualOverhaulT07Glare` (v1). Bien en HoverBot y SawBot |
| `LightFlicker.cs` | Parpadeo de fluorescente a 20 Hz con intercambio de material compartido | `Update` | — | 0 | Modula una luz existente | **muerto en campaña**: 0 instancias en RequiemCampaign, Room0x y Pursuit05 (grep por guid). Solo labs |
| `VisualOverhaulAblation.cs` | Interruptores de benchmark (`--vo-off`) | `Start`/`LateUpdate`, cada 30 frames con flag | — | `FindObjectsByType` con flag | — | Herramienta; 0 instancias en campaña. `EnemyStateLights` consulta `Has("states")` al habilitarse |
| `FollowMainCamera.cs` | Centra el polvo ambiental en la cámara | `LateUpdate` | — | 0 | — | 0 instancias en campaña (polvo `VO_Ambience` solo en labs) |
| `FrameGrab.cs` | Captura para tests (asigna) | Tests | — | Sí, a propósito | — | Herramienta, no es efecto |
| `ArmModuleVisual.cs` | Presentación del módulo de brazo | — | — | No revisado en detalle (230 líneas, visual de arma) | — | Fuera del inventario de VFX |

### 2.3 Efectos de combate fuera de esas carpetas (los dispara la campaña)

| Efecto | Disparador | Prefab | Pool | Partículas / luces | Mat | Estado |
|---|---|---|---|---|---|---|
| **Burst de muerte** `RobotBodyBurst` + `RobotDestructionPool` | `IEnemyDeathPresentation` (PooledEnemyDeathBurst, RobotDamageProgression, BigBotDestruction, SawBotCombat) | `_IR` (6933cfa3…) o el original (6a412bf6…, **lista roja**) según F1 | S: 16 precreados, hasta 32; 3,1 s de vida | Cadena: original → `T07_BodyExplosion` → `CannonExplosion_IR` → `VFX_HoverBot_Explosion` (plantilla). 10 sistemas en `_IR` (`deathfx-apply.txt`). Máximos: ring, lines y flare 64 cada uno, root 64, core 4, sparks 12, fragments 4, smoke 8, overload 12; `m_MaxParticleSize 0,3` | `WarmEnergy`, `HotFlash` y `PressureWave` A; `ResidualSmoke` α; `TrailDeath` **opaco** billboard (0,1–0,27 m) | **E1 aplicado** en 4 de 7 prefabs. Tamaños de `_IR`: flare 8,1, ring 4,05, core 1,84 m. Inconsistente (F1). La legibilidad en el instante de la detonación no está capturada |
| Explosión del módulo robado `CannonExplosion` + `CannonExplosionPresentation` | Detonación de OVERDRIVE (impacto o mecha de 6 s) | `VP_CannonExplosion_IR` / `VP_VFX_HoverBot_Explosion` (**lista roja ambos**) según el módulo | **N**: `Destroy(Instantiate(...),5)` (`CannonExplosion.cs:283`) y `Destroy(gameObject,3.2)` (`Presentation:223`) | `VP_CannonExplosion_IR`: 6 sistemas (máx. 10/30/8/16/1/14) más 1 luz (I 7, rango 5, máx. 2 vivas, `<18 m`) | A + α | **bien** en diseño (`T07_ANIMATION_VFX.md`: 0,085 s de destello, 0,38 s de onda, humo de 2,7 s). Sin pool, pero es poco frecuente |
| Carga del cañón del jugador y del T-07 `CannonChargePresentation` (modo marcos) | `CannonChargeShot` (`ChargeFraction`) | `Prefabs/VFX/CannonCharge_Magenta.prefab`: 6 renderers, 30 triángulos, 32 partículas, sin luz (`CANNON_CHARGE.md:14`) | Persistente | 32 | A (`EnergyCore`, `EnergyFrame`, `EnergySparks`) | **pendiente** (E6): no capturado |
| Carga del BigBot (modo `referenceVortex`) | Igual | `Prefabs/Enemies/BigBot_ReferenceCharge_v09.prefab` (anidado en BigBot_Current, corrección 2) | Persistente | Emisión de 9→28/s | A (`Blue/White/VioletArcs`) | **pendiente**. Coste: `GetColor`/`SetColor` por string y MPB por renderer cada frame mientras carga (F7) |
| `RobotHitFeedback` | `Health.OnDamaged` y `Damageable.Impact` (T07_Mech) | PS de chispas propio (`sparkCount: 12`) y destello emisivo | Persistente | 12 por impacto; máx. 48 | — | **bien** como "acierto"; el destello rojo plano de cuerpo entero (`fx-death-0.png`) es tosco |
| `RobotDamageProgression` (fallback) | Si el pool está lleno: `sparks.Emit(8)` y `smoke.Emit(3)` (`:98-102`) | Propio | — | — | — | Bien |
| `RobotArmorDebris` | Placas y brazos arrancados | Pool de 16 + 8; recicla el más antiguo | S | — | Material del robot | **costoso** en reposo (F4) |
| `PrimaryModuleHeatPresentation` | Celdas rojas por munición | `LateUpdate`, escribe solo al cambiar | — | — | MPB | **bien** |
| `CorridorAlarm` | Luces de alarma en Room00b | — | — | Luces de escena | — | **bien** (v1) |

### 2.4 Carpetas VFX: uso real

| Carpeta | Contenido | Usado en campaña por | Notas |
|---|---|---|---|
| `VFX/CannonCharge` | 3 mat A, 4 mallas, 2 texturas | `CannonCharge_Magenta.prefab` (T07_Mech, armas T-07) | Corrección 1: `EnergyStripe` y `MagentaCore` son texturas en uso |
| `VFX/CannonExplosion` | 4 mat (3 A, 1 α), 3 assets | `CannonExplosion_IR`, `VP_CannonExplosion_IR`, `T07_BodyExplosion` → todos los bursts | **Lista roja** (guardian §1b): editar solo en copia |
| `VFX/BigBotChargeV09` | 4 mat A, 7 mallas | `BigBot_ReferenceCharge_v09` → BigBot_Current | Corrección 2. Azul y violeta: rompe la paleta (E4) |
| `VFX/Mechanics` | `MechanicParticles` (α), `ChargeEnergyParticles` (A), `ChargeEnergyCore` (opaco Lit), `ParticleSoftCircle` | Jugador (Player_IR_*), `Weapon_T07_Temporary`, `Projectile_T07_Charged`, `BigBot_CollectibleCannon` | **Lista roja** (`MechanicParticles` y `ParticleSoftCircle`) |
| `Prefabs/VisualOverhaul/VFX` | 33 prefabs: cadenas `_VO` de 5 proyectiles, BurstMuzzle, ChargedProjectile, BodyExplosion y RobotBodyBurst, más `_IR` | Campaña: `_IR` (directo); `BigBot_BurstMuzzle_v10_VO_VO_VO_VO`, `EnemyProjectile_1_VO_VO_VO_VO` (BigBot_Current, vía `BigBot_Burst_v10_NavThrottle_VO`); `Projectile_Hoverbot_VO_VO_VO` (T07_Current, vía base) | Las cadenas de proyectiles son variantes vacías: inofensivas, pero ilegibles (F10). Ver la tabla siguiente |

Cadenas de `Prefabs/VisualOverhaul/VFX` que solo usan prefabs de laboratorio (F10):

| Prefab | Escala acumulada | Usado por |
|---|---|---|
| `RobotBodyBurst_CombatPolish_VO_VO_VO` | ×36,6 | Labs (F10) |
| `T07_BodyExplosion_VO_VO_VO` | — | Labs |

**Duplicados o muertos** (el CLEANUP_TABLE del 07/11 marca las 4 carpetas `VFX/*` "en uso — citado en C#", líneas 358-361, y no evalúa duplicados):
- **Muertos en campaña:** `LightFlicker`, `FollowMainCamera`, `VisualOverhaulAblation` y la cadena `RobotBodyBurst_*_VO*` / `T07_BodyExplosion_VO*` (solo labs).
- **Duplicados de escena:** `VP_OverdriveEdge` ×2 y `VP_DamageDirection` ×2.
- **Duplicado funcional:** dos prefabs de burst en el mismo pool (F1).

### 2.5 Coherencia de estilo (estado actual)

| Efecto | Color o paleta |
|---|---|
| Muerte | Ámbar y naranja (`sparkColor` 2,4/1,1/0,35; `VO_Flash` 1/0,62/0,3) |
| Carga del T-07 | Magenta |
| Carga del BigBot | Azul, blanco y violeta |
| Extracción | Azul eléctrico |
| SlideGuard | Cian |
| OVERDRIVE | Naranja (igual que la muerte: ambigüedad "¿me va a explotar el módulo o murió algo?") |
| Daño recibido | Rojo |
| Golpe al T-07 | Rojo plano de cuerpo entero |
| Ojos de T-07 | Siempre rojos |

Sobran tres familias de "energía" (magenta, azul y cian) sin regla. Duraciones: casi todo dura 0,1–0,5 s; los restos, 22–60 s.

---

## 3. Propuestas

Continúan tras E6 de v1. E3 y E5 se **amplían** (se marca "act."). Impacto, esfuerzo y riesgo: A = alto, M = medio, B = bajo.

### Efectos

| ID | Propuesta | Impacto | Esfuerzo | Riesgo |
|---|---|---|---|---|
| **E7** | **Un pool de bursts por prefab**: `Dictionary<GameObject, RobotBodyBurst[]>` en `RobotDestructionPool`. Así cada enemigo usa el suyo y el `_IR` (con `VO_Flash`) sale siempre que se pide. Alternativa sin código: asignar `_IR` en BigBot_Current (`BigBotDestruction.sharedPoolTemplate`) y en SawBot_Current (`SawBotCombat.deathEffect`) como overrides de prefab. | **A** | B (código) / B (overrides) | B–M |
| **E5 act.** | Además de `VP_OverdriveEdge`, desactivar el **`VP_DamageDirection`** duplicado. Antes, contar en Play Mode las instancias activas con `FindObjectsByType<DamageDirectionHud>` y `<OverdriveScreenFx>`. | M | B | B |
| **E3 act.** | Restos: tope global de 80 piezas (`MechDebrisPiece` + armadura), reciclando la más antigua. `enabled=false` en reposo, con fundido final gestionado por un único temporizador estático (cero `Update` en reposo). `RobotArmorDebris`: no reescribir `localScale` en reposo. | M (CPU en hordas) | M | B |
| **E8** | **Presupuesto de luz único** para efectos (máx. 2 entre `BurstFlashLight` y `CannonExplosionPresentation`; prioridad a la más cercana) para no superar el límite de 4 luces por objeto. | B–M | B | B |
| **E9** | **Enfriamiento de hit-stop** en `GameFeel`: tras un hit-stop, ignorar nuevos durante 0,25 s, o limitar a 0,12 s de congelado por segundo. Primero medirlo (P-FX1). | M (flujo DOOM/ULTRAKILL) | B | B |
| **E4 act.** | Paleta: muerte y explosión **ámbar**; energía robada y OVERDRIVE **magenta** (separada del ámbar de muerte); carga enemiga (T-07 y BigBot) **magenta–violeta** compartida; defensa y slide **cian**; extracción **cian-blanco** (en vez del azul 0,4/0,6/1). Golpe al T-07: destello blanco cálido breve en lugar de rojo plano. | M | M | B |
| **E10** | `TrailDeath` (opaco, billboard) en el burst → material aditivo o quitar ese sistema en la variante `_IR` (son "cuadrados" a 0,27 m). | B | B | B |
| **E11** | `ShotFxLibrary`: limpiar `Cache` en `sceneUnloaded` y `TryGetComponent` en lugar de `??` (también `FxPool.cs:26`); clasificar sin concatenar (comprobar las palabras clave en cada nombre). | B | B | B |
| **E12** | Soft particles: activar Depth Texture y `_SoftParticlesEnabled` en humo y anillo, en **copias** de los materiales. Si no, al menos bajar el humo y alejarlo del suelo. | B–M | B | M (requiere tu OK, toca el render pipeline) |
| **E13** | Limpieza de cadenas `_VO_VO…` (F10): repuntar los labs a `_IR` y mover las cadenas a cuarentena con la herramienta del CLEANUP_TABLE (conservando GUID y meta), sin borrar. | B (higiene) | M | B |
| **E14** | Pool para la explosión del módulo robado (`FxPool.Spawn` con vida de 3,2 s en lugar de `Instantiate`/`Destroy`); `CannonExplosionPresentation` debería reiniciarse en `OnEnable` sin `Destroy`. | B | M | M |

Archivos de cada propuesta y si toca la zona protegida:

**E7**
- Archivos: `Scripts/Enemies/RobotDestructionPool.cs`, o `Prefabs/Campaign/BigBot_Current.prefab` y `SawBot_Current.prefab`.
- Zona protegida: **roza por código**. `SawBotCombat` (Carronero) también prepara el pool en **Room00** (`AI/SawBotCombat.cs:139`). Con un pool por prefab, Room00 sigue usando el original: sin cambio visual. Verificarlo con un test del hangar.
- **No** tocar `Carronero.prefab` (lista roja) ni el original `RobotBodyBurst_CombatPolish.prefab` (lista roja).

**E5 act.**
- Archivo: `RequiemCampaign.unity`.
- Zona protegida: no. Necesita tu commit previo o una copia, porque la escena tiene cambios sin commit.

**E3 act.**
- Archivos: `MechDebrisPiece.cs`, `MechDeathExplosion.cs`, `RobotArmorDebris.cs`, `RobotDestructionPool.cs`.
- Zona protegida: no, salvo el pool, que corre en Room00 (Carronero). Verificar con un test.

**E8**
- Archivos: `BurstFlashLight.cs`, `CannonExplosionPresentation.cs` y una clase estática nueva `FxLightBudget`.
- Zona protegida: **roza**. `VP_CannonExplosion_IR` y `CannonExplosionPresentation` se usan en R00 según el guardian (§1b). Es solo código; no cambia el asset.

**E9**
- Archivo: `Scripts/VisualPass/GameFeel.cs`.
- Zona protegida: comprobar si el menú o la cinemática llaman a `GameFeel` (`grep` antes de tocar).

**E4 act.**
- Archivos: **copias** de los materiales `VFX/CannonExplosion/*`, `HG_Fx*` y `MechanicParticles`; `ExtractionArcFx.cs` (gradientes en código); `VisualOverhaulT07Glare.cs`.
- Zona protegida: **sí** para los materiales de la lista roja → solo copias o variantes. No editar `HG_Fx*` ni `VFX/CannonExplosion/*`.

**E10**
- Archivo: `RobotBodyBurst_CombatPolish_IR.prefab` (override en la variante).
- Zona protegida: no. `TrailDeath.mat` es lista roja: no editarlo; cambiar el material **en la variante**.

**E11**
- Archivos: `ShotFxLibrary.cs` y `FPS/Scripts/Game/Shared/FxPool.cs` (plantilla: cambio mínimo).
- Zona protegida: no.

**E12**
- Archivos: `Default_PipelineAsset.asset` y copias de `ResidualSmoke.mat` y `DiskExplosion.mat`.
- Zona protegida: **sí**, indirectamente. El pipeline es global, incluidos IntroMenu y Room00. Necesita respaldo y tu OK.

**E13**
- Archivos: `Prefabs/VisualOverhaul/VFX/*_VO_VO*`.
- Zona protegida: no. Comprobar que ningún prefab de `Prefabs/Menu/*` las use: en el grep de GUID no aparece ninguno.

**E14**
- Archivos: `CannonExplosion.cs` y `CannonExplosionPresentation.cs`.
- Zona protegida: **roza** (R00 usa estos prefabs). Es solo código: los assets de la lista roja no cambian.

### Rendimiento ligado a efectos

| ID | Propuesta | Impacto | Esfuerzo | Riesgo |
|---|---|---|---|---|
| **P-FX1** | **Medición determinista de efectos** (requisito para E7, E9 y E3): extender `Tests/PlayMode/MapFxAudit.cs` (ya existe) con semilla fija y muerte forzada de N enemigos ante la cámara. Registrar por frame: prefab real del pool (`RobotDestructionPool` vía reflexión del campo estático), `BurstsInUse`, `BurstFlashLight.Lit`, `CannonExplosionPresentation.ActiveFlashLights`, `MechDebrisPiece` activos y en reposo, hit-stop acumulado por segundo, partículas y sistemas vivos, y capturas a +0,05/+0,15/+0,3 s de la detonación. | **A** (desbloquea decisiones) | M | B |
| **P-FX2** | **Precalentar el pool en la carga de la sala** (con `CampaignFlow` al activar la sala) en lugar del `Awake` del primer enemigo. Medir antes y después el pico de spawn (p99 de 212–224 ms en `horde.txt`) con el Profiler (Instantiate y Awake). | M | B | B |
| **P-FX3** | Las chispas de `MechDeathExplosion`: un `ParticleSystem` horneado en el prefab o un pool compartido de 8 emisores, en lugar de `AddComponent` en cada `Awake` y `Destroy` en cada muerte. | M (spawn de rondas de 22–27) | M | B |
| **P-FX4** | Frame Debugger en Room03 R2 tras E7: cuántos draws de efectos salen del SRP Batcher por MPB (F7), y overdraw de los billboards aditivos `_IR` (flare de 8,1 m y ring de 4,05 m a ≤3 m de la cámara). | M | B | B |
| **P-FX5** | Profiler de `SectorLightingBlend.Update` (`DynamicGI.UpdateEnvironment` cada frame, 1,4 s). Si cuesta más de 0,2 ms, actualizar cada 4 frames. | B | B | B |
| **P-FX6** | `HudHealthTint`: escribir `color` solo al cambiar. Hoy `Graphic.color` marca la malla de UI como sucia cada frame. | B | B | B |

Archivos de cada propuesta y si toca la zona protegida:

**P-FX1**
- Archivo: `Assets/IronRequiem/Tests/PlayMode/MapFxAudit.cs`.
- Zona protegida: no (es un test con teardown sin guardar).

**P-FX2**
- Archivos: `RobotDestructionPool.cs` y `Scripts/Levels/CampaignFlow.cs`.
- Zona protegida: **roza**: `CampaignFlow` hace `Bind` sobre Room00 (guardian, línea 90). No precalentar en Room00, o verificarlo con un test.

**P-FX3**
- Archivos: `MechDeathExplosion.cs` y una herramienta de Editor para hornear en variantes.
- Zona protegida: `Carronero.prefab` es lista roja: **no hornear en él**. Usar código con fallback para no tocarlo.

**P-FX4**
- Archivos: ninguno (solo medición).
- Zona protegida: no.

**P-FX5**
- Archivo: `SectorLightingBlend.cs`.
- Zona protegida: corre en RequiemCampaign, incluida la transición desde Room00: **roza**. Verificar la cinemática.

**P-FX6**
- Archivo: `HudHealthTint.cs`.
- Zona protegida: no.

**Lote recomendado:**
1. **P-FX1** primero, sin cambios de juego.
2. Después **E7 + E5 act. + E3 act.**: código pequeño y fuera de los assets de la lista roja.
3. Volver a capturar.
4. Con imágenes reales de la detonación decidir **E4 act.** y **E10**.
5. **E12** solo con tu OK por tocar el pipeline.

## 4. Lo que solo se puede medir en Play Mode (pendiente)

- Qué prefab gana el pool en cada sala (F1).
- Si los dos `VP_DamageDirection` y `VP_OverdriveEdge` están activos a la vez (F2).
- Lectura de la detonación `_IR`, la carga magenta, el vórtice v09, OVERDRIVE, HeatVent y SlideGuard en la Game view (E6 de v1).
- Hit-stop acumulado en cadena (F9).
- Coste de spawn de efectos (F5).
- Draws fuera del SRP Batcher (F7).
- Ninguna cifra de este documento es de Player Release (ver R1 de v1).
