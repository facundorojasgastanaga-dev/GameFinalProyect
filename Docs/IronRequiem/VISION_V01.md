# VISION_V01 — Linterna y lectura de enemigos en la oscuridad (chat 11)

Fecha: 20/11/2026. Estado: **elegida la C (lectura pasiva). F2 implementado, F3 medido y F4 revisado y verificado** (§6-§8). Escenas, luces, entrada, pipeline y materiales originales sin cambios.

Skills usadas:
- `gamedev:shader-programming`: el rim de Fresnel `pow(1−N·V, p)` ya existe en `IronRequiemHardSurface.shader:279-280`; se mueve por color, sin editar el shader.
- `gamedev:fps-shooter`: en un FPS rápido la lectura depende de la telegrafía, no de gestionar un interruptor.
- `unity:ui-ugui`: el indicador de HUD solo si hace falta.
- Locales `iron-requiem-performance`, `iron-requiem-enemy-ai` e `iron-requiem-fps` (inventarios): presupuesto, IA solo propuesta, cámara FPS.

## 1. F0 — Inventario medido

### 1.1 Método

La herramienta de solo lectura `Assets/IronRequiem/Editor/Vision/VisionF0Probe.cs` hace lo siguiente:
- Abre una escena vacía que no se guarda.
- En Play Mode carga cada sala con sus luces y su ambiente.
- Coloca congelado el enemigo real de cada spawn (el prefab de su ronda).
- Lo renderiza desde 5, 15 y 25 m, a la altura de los ojos y mirando al centro de la arena, con `Campaign_PostProfile`.

Una máscara del enemigo da tres valores en la escala 0-255 de §L1.3: el valor del enemigo, el del fondo inmediato y la relación entre ambos. Criterio de §L1.2: legible si el contraste es ≥ 1,5 o si se ven los ojos (≥ 6 px brillantes). «Débil» es 1,25-1,5 e «ilegible» < 1,25. Al terminar reabre la escena anterior.

**Validación:** en una partida real (Map01 S1), la cámara del jugador y la de la herramienta en la misma pose dan una media de 10,54 y 10,61. La Game view coincide (`Tools/IronRequiem/Reports/Vision/F0/validate.txt`). Los números de abajo son los del juego.

### 1.2 Resultado: 561 medidas, 24 salas

| Sala | Fondo (mediana) | Enemigo (mediana) | % ilegible a 5 / 15 / 25 m |
|---|---|---|---|
| R01 Control | 8,8 | 3,5 | 20 / 20 / 20 |
| R02 Pozo | 7,0 | 8,8 | 50 / 50 / **80** |
| R03 Dormitorios | 11,9 | 52,3 | 0 / 0 / 0 (HoverBot: ojo brillante) |
| R04 Comedor | 12,9 | 8,0 | 50 / 20 / 0 |
| R06 Armería | 8,8 | 11,0 | 20 / 30 / 40 |
| **R07 Comunicaciones** | 8,9 | 9,7 | **100 / 80 / 50** |
| R08 Almacenes | 10,9 | 8,2 | 70 / 30 / 30 |
| **R10 Túnel (emboscadas)** | 11,8 | 7,1 | 33 / 33 / 33 |
| **R11 Decantadores** | 10,5 | 8,4 | **80** / 40 / 60 |
| R12 Colector | 7,6 | 2,9 | 10 / 0 / 0 |
| R14 Fundición | 8,1 | 7,6 | 60 / 10 / 30 |
| R15 Ensamblaje | 9,5 | 6,1 | 10 / 20 / 40 |
| R16 Reactor | 6,5 | 13,4 | 20 / 40 / 40 |
| R18 Bóveda | 2,8 | 7,5 | 0 / 0 / 0 |
| Room00b Pasillo de alarma | 13,0 | 20,1 | 33 / 17 / 33 |
| Room01-04 (campaña actual) | 2,2-6,0 | 1,1-8,5 | Room03: 50 / 50 / 50; resto ≤ 20 |
| Pursuit05 | 2,1 | 6,1 | 0 / 0 / 20 |

R05, R09, R13 y R17 no tienen spawns de rondas (R17 usa otro sistema de encuentro); no se midieron.

Por enemigo (% de medidas ilegibles):

| Enemigo | % ilegible | Lectura |
|---|---|---|
| **T07** (`T07_Current_Repaired`) | **35 %** (297 medidas) | El más usado |
| BigBot | 40 % | — |
| SawBot | 35 % | — |
| Turret | 30 % | — |
| Carroñero | 15 % | — |
| T07 Mech | 12 % | — |
| HoverBot | 0 % | Su ojo basta |

Total: 266 legibles, 134 débiles y 161 ilegibles.

**Hallazgo principal (se anota, no se corrige aquí):** Map01 está oscuro en todas partes, no solo en las zonas de oscuridad intencional. El fondo tiene una mediana de 2-13 cuando §L1.3 pide ≥ 45 en carriles y spawns. Es iluminación del chat 9 (05_ILUMINACION, «nada construido» en las luces finales). La regla prohíbe arreglarlo subiendo el ambiente. Va como propuesta a F3.

Capturas con el enemigo contorneado en magenta: `Tools/IronRequiem/Reports/Vision/F0/captures/` (450). Datos: `legibility.csv` y `lights.csv`.

### 1.3 Luces por cuarto (medidas en ejecución)

- **Al límite:** 8/8 luces activas en R03, R06, R10, R11, R12, R14, R15, R16 y R17.
- **Por encima del presupuesto:**
  - R07: 10 (tope 8).
  - R18: 9.
  - Anotado para el chat 9; no se toca.
- **Sombras:** solo en spots (R08, R14 y R18 ×2). Ninguna puntual tiene sombra.
- **URP** (`Assets/Rendering/Default_PipelineAsset.asset`):
  - Luces adicionales por píxel, **4 por objeto** (l.48).
  - Sombras adicionales activas (l.49).
  - **Light Layers desactivadas** (`m_SupportsLightLayers: 0`, l.76).
- **Consecuencia para una linterna con `Light`:**
  - Pasa a 9/8 en nueve cuartos.
  - Con el límite de 4 luces por objeto, algunas superficies perderían una luz de la sala al entrar en el cono.
  - Sin light layers, la luz también ilumina las manos y el arma (WeaponCamera es una cámara overlay que solo filtra capas de dibujo, no luces). **[VALIDAR F2]** si se elige A.

### 1.4 Materiales de los enemigos

- **Shader `IronRequiem/HardSurface`:** `_RIM` es un **keyword de material** (`shader_feature_local`). Un MaterialPropertyBlock solo puede cambiar `_RimColor` y `_RimPower`, no encender el keyword.
- **T07 actual** (medido en ejecución): material `Hit_T07_URP_f8f0f1_HS`, HardSurface con **`_RIM` ya activo** y color muy tenue (0,1; 0,18; 0,26). Para el enemigo más ilegible basta un MaterialPropertyBlock: ningún material cambia.
- **Otros enemigos:** HoverBot y Turret tienen variantes `_HS` con rim. SawBot, Carroñero y BigBot parecen URP Lit sin rim. Esto es inventario estático, **por confirmar en F2** con el registro en ejecución. Los que no tengan rim necesitan una **variante de material nueva** (copia con `_RIM`); los originales no se tocan.
- **Restos:**
  - `Scrap_*` no comparte ningún material con enemigos y tiene `_UseRim 0`: no reciben resaltado.
  - Las piezas destructibles y los cadáveres sí comparten materiales con rim. El resaltado debe ir por renderer del enemigo vivo y retirarse al morir, nunca por material.
- **Código existente:**
  - `EnemyController` ya usa MaterialPropertyBlock para el ojo y el destello de daño, y **reescribe cada frame** el bloque del cuerpo por índice de material (`Assets/FPS/Scripts/AI/EnemyController.cs:264-268`; ojo en `:241-242` y `:291-292`). El resaltado debe aplicarse en `LateUpdate` con `GetPropertyBlock` → añadir `_RimColor` → `SetPropertyBlock` para fusionarse con su bloque sin pisar el destello.
  - No hay código de resaltado, escáner ni linterna.

### 1.5 Entrada, cámara, HUD, audio y ajustes

- **Teclas libres:**
  - Teclado: T (recomendada), V, X, Z, H, B, L…
  - Mando: **ningún botón libre** (todos asignados; cruceta izq./der. = armas 1/2).
- **Chat 10 (controles):**
  - Hecho: `ControlsRebinder` (overrides en PlayerPrefs) y `ControlHints.Name()` con `GetBindingDisplayString`.
  - Pendiente: su **prueba manual** (`CHECKLIST_TRABAJO.md:1527`).
  - Según la tabla de conflictos, 11 va después de 10 o reservando la acción con él.
- **Viewmodel:** capa 10 `FirstPersonWeapon`, cámara overlay. El jugador de campaña es `Player_ModularArm_v07`.
- **Audio:** un solo mixer, `Assets/FPS/Audio/MainAudioMixer.mixer`; grupo `Player` para el jugador.
- **Ajustes:** `RequiemSettings` en PlayerPrefs; las filas nuevas van en `ControlsPanel.BuildSettings`.
- **Tema:** `IronRequiemUiTheme` (Cyan, Steel, Text…).

## 2. F1 — Propuestas probadas en los mismos 6 puntos oscuros

**Puntos** (los peores de F0): R10 emboscada P1, R07 G3_2, R11 G1_2, R02 G1_1, R14 G1_1 y Room00b DuctSpawn2. Todos con el T07 actual, a 5, 15 y 25 m. La distancia real se recorta si hay un muro en medio.

**Captura de prueba de cada opción:** `Tools/IronRequiem/Reports/Vision/F1/captures/<sala>_<spawn>_<variante>_<d>m.jpg` (90, sin contorno). Datos: `F1/prototypes.csv`. Los prototipos se hacen en ejecución con clones de material y una luz temporal; no se guardó nada.

**Color de visión:** blanco frío (0,72; 0,86; 1,0). No usa rojo, ámbar ni verde (puertas) ni el cian del ojo en patrulla.

| | Base | **A** Linterna de hombro | **B** Pulso de escáner | **C** Lectura pasiva | **A + C** |
|---|---|---|---|---|---|
| Qué es | — | Spot de 55° (núcleo 32°), alcance 30 m, intensidad 35, sin sombra, se alterna con una tecla | Durante ~3 s, el rim de los enemigos sube a 3,2 HDR; recarga | El rim de cada enemigo vivo sube solo según la oscuridad local: k = clamp((45 − fondo)/35), rim 0,6-2,0 HDR | Ambas |
| Legibles (de 18) | 0 | 8 | **17** | **17** | **17** |
| Contraste 5 / 15 / 25 m | 1,03 / 1,15 / 1,06 | 1,66 / 1,20 / 1,11 | 6,95 / 5,93 / 5,95 | 4,77 / 4,03 / 4,05 | 3,28 / 3,64 / 3,62 |
| Por qué | — | Ilumina igual enemigo y fondo; a 15-25 m apenas llega (1/d²) | Silueta muy marcada | Silueta clara sin tocar el entorno | La linterna sube el fondo y reduce el contraste de C |
| Coste en luces | — | **+1 luz en tiempo real.** Rompe el presupuesto en 9 cuartos (8/8) salvo que apague o atenúe un anclaje mientras está encendida | 0 | 0 | +1 (igual que A) |
| Coste GPU/SetPass | — | +1 luz por píxel en el cono; estimado < 0,3 ms a 1080p **[medir en F2]** | Solo durante el pulso: los renderers del enemigo salen del SRP Batcher (≈ +1 draw por material de enemigo vivo) | Igual que B pero permanente: ≈ 10-25 draws de enemigos fuera del SRP Batcher. CPU ≈ 0; la oscuridad local se estima cada 0,25 s | Suma de A y C |
| Viewmodel | — | Probablemente lo ilumina o quema (sin light layers). Evitarlo exige activar Light Layers en el URP (necesita tu permiso) o mover la luz | — | — | Igual que A |
| Encaje FPS rápido | — | Medio: gestión de interruptor, ambiente de survival | Medio-alto: decisión táctica breve; cuidado con que no se convierta en un wallhack (el rim respeta la oclusión, no se ve a través de muros) | **Alto:** sin entrada nueva, nunca interrumpe el combate, el ambiente se mantiene | Medio |
| Recurso nuevo | — | Batería opcional (no propuesta) | Recarga de ~8 s (no es un recurso) | No | — |
| Entrada | — | Tecla T; en mando **no hay botón libre** (chat 10) | Tecla nueva; mismo problema en mando | **Ninguna** (sin conflicto con el chat 10) | Tecla T |
| Reacción de la IA (solo propuesta, chat 8) | — | Los enemigos dentro del cono podrían alertarse antes. No recomendado al ritmo DOOM | El pulso podría alertar en un radio (riesgo/recompensa) | No aplica | — |

## 3. Recomendación

**C, lectura pasiva**, con dos ajustes en `RequiemSettings`:
1. Intensidad del resaltado (0-150 %).
2. Accesibilidad «contornos siempre activos», que fija k = 1.

Motivos:
- Resuelve 17 de 18 puntos.
- No añade luces, entrada, recursos ni carga al chat 10.
- Encaja con DOOM/ULTRAKILL.
- En el T07, el enemigo más ilegible, se hace solo con MaterialPropertyBlock.

Si quieres además una linterna por ambiente o por estilo, **A + C** funciona igual de bien en legibilidad. Pero cuesta una luz que hoy no cabe en 9 cuartos y necesita resolver el viewmodel; no lo recomiendo hasta que el chat 9 cierre la iluminación.

## 4. Plan de F2 propuesto en F1 (histórico; lo implementado está en §6)

- **`Assets/IronRequiem/Scripts/Player/Vision/EnemyDarkReadability.cs`** (un MonoBehaviour por enemigo vivo, lo añade un gestor):
  - Estima la oscuridad local con el ambiente más las 4 luces más cercanas de la sala, cada 0,25 s.
  - Aplica `_RimColor` y `_RimPower` con MaterialPropertyBlock en `LateUpdate`, fusionado (Get → Set) con el bloque que `EnemyController` reescribe cada frame (ojo y daño).
  - Se retira en `OnDie` y al desactivarse. Nunca toca restos ni decoración.
- **`EnemyVisionRegistry.cs`:** alta y baja de enemigos por evento, sin `Find` por frame.
- **`Editor/Vision/VisionMaterialVariants.cs`:** crea copias `_Vision` con `_RIM` solo para los materiales de enemigo que hoy no lo tienen. Las aplica en variantes de prefab con autorización; no en los materiales compartidos.
- **`RequiemSettings` y `ControlsPanel`:** las dos filas de ajustes.
- **Sonido e indicador de HUD:** no hacen falta en C. Solo con B o A.
- **Tests:**
  - EditMode: color sin rojo, ámbar ni verde; restos sin resaltado; con A o B, la acción tiene binding de teclado y de mando.
  - PlayMode: resaltado con y sin oscuridad; se pierde al morir; luces del cuarto ≤ presupuesto; cambio de escena sin errores.
- **Rendimiento:** MapFxAudit en el peor cuarto, con y sin C.

## 5. Pendientes anotados (fuera del alcance)

- Map01 oscuro de forma general, con un fondo de 2-13 frente al mínimo de 45. Propuesta para F3 y el chat 9: completar las luces de §L3 y la banda de lectura `LUM-07`. No subir el ambiente.
- R07 (10 luces) y R18 (9) superan el presupuesto de 8.
- Prueba manual del chat 10 sin hacer; no hay botón de mando libre para una acción nueva.
- `maxBatches=0` en los informes de MapFxAudit: el dato no es fiable.

## 6. F2 — Implementado (opción C)

Elección del usuario: **C, lectura pasiva**. No añade acción de entrada (no hay binding nuevo ni conflicto con el chat 10), ni `Light`, ni recurso, ni sonido, ni indicador de HUD: no hacían falta.

| Archivo | Qué hace |
|---|---|
| `Assets/IronRequiem/Scripts/Player/Vision/EnemyDarkReadability.cs` (nuevo) | Componente único que se crea solo y persiste entre escenas (`Bootstrap`, l.49). Funciones: <ul><li>Busca enemigos vivos (`EnemyController`) cada 1 s y no reinserta muertos (`Rescan`, l.62).</li><li>En `LateUpdate` (l.79) estima la oscuridad local cada 0,25 s (`DarknessAt` / `Illuminance`, l.174-216). Usa las sondas de luz si la escena las tiene (y entonces solo suma luces en tiempo real, sin doble conteo); si no, el ambiente. Suma las luces de punto, foco y direccionales con caída, cono y **oclusión por muros** (`Blocked`, l.218; ignora triggers, cuerpos con Rigidbody, el jugador y otros actores). El primer muestreo fija el valor y después se suaviza a 3/s, sin saltos.</li><li>Aplica el rim por renderer e índice de material con MaterialPropertyBlock, fusionado con el que `EnemyController` reescribe cada frame. Los valores van en lineal con `SetVector` (`Write`, l.167-170).</li><li>Una pieza que deja de ser hija del enemigo, como el cañón desprendido convertido en pickup, se libera al momento (`Apply`, l.156).</li><li>Al morir, desactivarse, destruirse o apagar el ajuste: `SetPropertyBlock(null, índice)` y se devuelve el material original solo donde sigue la variante, sin pisar las etapas de daño (`Release` / `ReleaseSlot`, l.136-155).</li><li>Excluye `IntroMenu` y `Room00_Hangar` (zona protegida, l.29).</li><li>Ajustes releídos solo cuando cambian.</li><li>`ApplyNow` (l.99) es solo para herramientas y tests.</li></ul> |
| `…/Vision/VisionMaterialRules.cs` (nuevo) | <ul><li>Color blanco frío (0,72; 0,86; 1,0) (l.9).</li><li>Fresnel estrecho, potencia 4,5 (l.10): un contorno, no un cuerpo blanco.</li><li>Rim lineal de **0 con entorno iluminado a 1,6 a oscuras** (l.13); 2,4 con el ajuste al 150 % (≤ 4 HDR).</li><li>Filtros de cuerpo: fuera ojos, indicadores, lentes, cristal y sierra emisiva (l.18-39).</li><li>`IsReservedColor` (l.41) para los tests.</li></ul> |
| `…/Vision/VisionMaterialMap.cs` (nuevo) + `Assets/IronRequiem/Art/Vision/Resources/Vision/VisionMaterialMap.asset` | Pares material original → variante `_Vision`. |
| `Assets/IronRequiem/Art/Vision/Materials/*_Vision.mat` (4 nuevos) | `SawBot_URP`, `Carronero_Body`, `T07Mech_Atlas` (URP Lit sin rim) y `VO_Suit_Joint` (HardSurface sin rim). Son copias HardSurface con `_RIM` y rim **negro** por defecto: el aspecto base es el del original y solo se ilumina cuando el sistema lo pide. Los originales no cambian (mismo hash). El T07 actual, BigBot, HoverBot y Turret ya tenían `_RIM`, así que en ellos basta el MaterialPropertyBlock. |
| `Assets/IronRequiem/Editor/Vision/VisionMaterialVariants.cs` (nuevo) | Auditoría de materiales de enemigos (solo lectura) y creación de las variantes. |
| `Assets/IronRequiem/Bridges/Game/RequiemSettings.cs` l.19-27 | `VisionIntensity` (0-1,5; 1 por defecto), `VisionOutlinesAlways` (accesibilidad) y `VisionVersion` (contador de cambios). |
| `Assets/IronRequiem/Scripts/UI/Controls/ControlsPanel.cs` l.337-339 y l.344 | Pestaña AJUSTES, sección VISIÓN: «Resaltar enemigos en la oscuridad» (SÍ/NO) y «Contornos siempre activos». Más una nota explicativa. |
| `Assets/IronRequiem/Editor/Vision/VisionF0Probe.cs`, `VisionF3Session.cs`, `VisionTestRunner.cs` (nuevos) | Medición, capturas, tests y perfilado (menú `Tools/Iron Requiem/Vision`). No guardan nada del juego. |
| Tests nuevos | `Tests/EditMode/VisionReadabilityEditTests.cs` (3) y `Tests/PlayMode/VisionReadabilityTests.cs` (2). |

Respaldos: `Tools/IronRequiem/Backups/20261120-vision-f2/` (RequiemSettings, ControlsPanel y este documento antes de F2) y `20261120-vision-f0f1/` (checklist).

**Calibración** (561 puntos de F0 con el valor del componente junto al brillo de fondo medido en la imagen):

| Valor del componente | Brillo de fondo (mediana) |
|---|---|
| 0-0,3 | 8 |
| 0,3-0,5 | 12 |
| 0,8-1,2 | 25 |

Umbrales: «oscuro» ≤ 0,5 (fondo ≈ 15) e «iluminado» ≥ 1,6 (fondo ≈ 45, el mínimo de §L1.3). Con el Map01 actual (0,17-0,30 en todas las salas) el rim va al máximo; cuando el chat 9 lleve las salas a ≥ 45, dejará de añadirse solo.

## 7. F3 — Medición antes/después con el sistema final

Mismas posiciones de F1. «Antes» con el ajuste a 0, «después» con el sistema real, en el mismo frame. Datos: `Tools/IronRequiem/Reports/Vision/F3/before-after.csv`. Capturas: `F3/captures/<sala>_<spawn>_antes|despues_<d>m.jpg`.

| Punto (T07) | Antes 5 / 15 / 25 m | Después 5 / 15 / 25 m |
|---|---|---|
| R02 Pozo G1_1 | 1,02 / 1,20 / 1,20 | **2,72 / 2,25 / 2,24** |
| R07 Comunicaciones G3_2 | 1,00 / 1,01 / 1,01 | **2,70 / 2,72 / 2,72** |
| R10 Túnel, emboscada P1 | 1,04 / 1,10 / 1,11 | **2,36 / 2,29 / 2,20** |
| R11 Decantadores G1_2 | 1,19 / 1,00 / 1,00 | **3,34 / 2,84 / 2,84** |
| R14 Fundición G1_1 | 1,01 / 1,25 / 1,19 | **2,81 / 2,21 / 2,29** |
| Room00b Pasillo de alarma | 1,46 / 1,33 / 1,00* | **2,53** / 1,34 / 1,00* |
| **Legibles** | **0 de 18** | **16 de 18** |

\* A 25 m la cámara queda dentro del conducto (valor 0,07): es un artefacto de la medición (igual en F0). A 15 m en el pasillo el fondo ya está a 25 y el contorno suma menos; queda «débil».

- **El enemigo pasa de un valor ≈ 8-21 a ≈ 31-41**, con el cuerpo oscuro y el ojo intactos. Ya no es la silueta blanca de la primera versión.
- **El fondo** sube 0,3-0,5 puntos: el ambiente no cambia.
- **Distancias:** la columna `dist_real` recoge el recorte por muros. Varias tomas de «25 m» quedaron a 5-17 m. No se ha medido a 25 m reales en todas las salas.

**Esclusa Transfer** (campaña actual, `PersistentAirlocks/TransferAirlock_27`, 10,2 m): `F3/captures/Esclusa_Transfer_antes_10m.jpg` / `_despues_10m.jpg`. Antes el T07 se pierde contra el muro oscuro; después su silueta se recorta.

**Disparando:** `Tools/IronRequiem/Bridge/vision_f3_firing_a.png`. El jugador dispara con el cañón al T07 resaltado. Se ven el haz, el arma y el HUD, sin deslumbramiento: la mecánica no añade luz.

**Rendimiento** (`MapFxAudit.C_HordeProfilerRoom03`, el peor cuarto; Editor en Play Mode; `F3/horde-off.txt` y `horde-on.txt`). Ronda 3, con 18-19 enemigos vivos:

| | OFF | ON |
|---|---|---|
| Frame p50 / p95 | 15,62 / 27,11 ms | 15,78 / 25,01 ms |
| Hilo principal p50 | 15,65 ms | 15,67 ms |
| GC por frame | 87,3 KB | 86,7 KB |
| Luces punto/foco activas | 16 | 16 |
| SetPass máx. | 238 | **309** |

- **Coste real en SetPass: +18 % a +25 %** (+33 en la ronda 2 y +71 en la 3). Los MaterialPropertyBlock sacan esos renderers del SRP Batcher.
- **El tiempo de frame del Editor no lo refleja:** está dominado por otros costes. Falta medirlo en build.
- **0 luces añadidas:** el presupuesto por cuarto no cambia.
- **Mitigación propuesta (no aplicada):** cuantizar el rim en 3-4 niveles con materiales compartidos por nivel, para volver al SRP Batcher.
- La pasada ON es de las 09:12, con el rim anterior. El coste en SetPass depende de que existan los bloques, no de su valor. La repetición de las 09:31 falló por un error de E/S de Windows al guardar una captura de MapFxAudit (`Win32 IO 1224`), ajeno a la mecánica.

**Ajustes de luz por zona:** no se propone tocar luces para leer a los enemigos, porque la C lo resuelve sin luz. La oscuridad general de Map01 (§5) sigue siendo trabajo del chat 9.

## 8. F4 — Revisión independiente y verificación

**Revisor:** subagente Opus independiente, de solo lectura, que juzgó las capturas por sí mismo. Hallazgos y estado:

| # | Hallazgo | Estado |
|---|---|---|
| 1 | El cálculo de oscuridad ignoraba luces horneadas, direccionales y oclusión: «oscuro» casi siempre. | Corregido y recalibrado (§6). Con sondas, solo luces en tiempo real (sin doble conteo). La oclusión ignora cuerpos en movimiento (sin parpadeo cuando alguien cruza). |
| 2 | Se pierde el destello de daño en las variantes. | **No es una regresión:** los Lit originales no tienen el keyword `_EMISSION`, así que el destello ya no se veía. Queda anotado que la variante usa el modelo de iluminación HardSurface. |
| 3 | Robot blanco «porcelana» en lugar de contorno. | Corregido: fresnel 4,5 y rim de 0 a 1,6. |
| 4 | El cañón desprendido seguía resaltado. | Corregido y cubierto por test. |
| 5 | Al liberar se restauraban materiales por encima de las etapas de daño. | Corregido y cubierto por test. |
| 6 | Bloque vacío en lugar de `null`. | Corregido (`SetPropertyBlock(null, índice)`). |
| 7 | Entradas sin limpiar; destello al aparecer; muertos reinsertados. | Corregido. |
| 8 | Coste en SetPass. | Registrado como coste real; falta medir en build. |
| 9 | Huecos de test; `highlighted=False` en la esclusa. | Tests ampliados. La causa de la esclusa estaba en la herramienta: el enemigo de prueba nacía en la escena del hangar, que está excluida, y se capturaba antes de su `Start`. Corregido. |

**Fallo propio encontrado por los tests:** al liberar, el enemigo quedaba más oscuro (0,90 frente a 1,07). Causa: un color reescrito por MaterialPropertyBlock se convierte de nuevo de espacio de color. Corregido. El rim aplicado también superaba 4 HDR por la misma conversión; ahora se escribe en lineal.

**Verificación:**
- **Compilación:** 0 errores (solo avisos CS0618 preexistentes).
- **EditMode 3/3** (`Reports/Vision/F2/editmode-tests.xml`, 09:48):
  - el color no es reservado y respeta HDR ≤ 4;
  - los restos de robot (`Scrap_*`) no son enemigos, no tienen rim ni son materiales de visión;
  - las variantes conservan albedo y rim negro, y las fuentes siguen sin rim.
  - La C no tiene acción de entrada, así que no hay test de binding.
- **PlayMode 2/2** (`playmode-tests.xml`, 09:47), con T07 y SawBot reales:
  - el rim sigue la oscuridad (1,6 a oscuras, 0 con luz);
  - el SawBot usa su variante;
  - **0 luces añadidas**;
  - el apagado devuelve los materiales;
  - al morir se quita al momento;
  - la pieza desprendida se libera;
  - el material de una etapa de daño se conserva;
  - la imagen tras liberar es igual a la original (1,07 / 1,07, dos ciclos);
  - sin errores de la mecánica al descargar la escena.
- **Prueba en Play Mode:**
  - partida real de la campaña con la esclusa y el disparo;
  - salas de Map01 cargadas en Play con enemigos reales.
  - Siempre se salió de Play sin guardar; las escenas no se tocaron.
  - **No** se ha probado con teclado y ratón físicos ni el menú de ajustes con clics reales.

**Pendientes:**
- Medición en build (SetPass y GPU) y, si hace falta, la mitigación por niveles.
- Medir a 25 m reales en salas largas.
- Revisión visual por el usuario en partida.
- Comprobar en juego el resto de enemigos (BigBot, HoverBot, Turret, Carroñero): ya tienen rim o variante, pero solo T07 y SawBot están probados en PlayMode.
