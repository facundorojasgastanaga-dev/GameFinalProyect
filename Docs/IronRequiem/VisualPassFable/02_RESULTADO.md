# Pase visual (chat 12) · Resultado de F2, F3 y F5

Fecha: 21/11/2026. Aprobado por el usuario: "lo recomendado" de [01_DIRECCION.md](01_DIRECCION.md). Sin commits. La zona protegida no cambió.

**F4 (decoración) no está hecha.** Depende de que el chat 9 invierta las cotas de Map01 (opción O1) y de construir el kit de props, que no existe.

> **Actualización tras la revisión independiente:** los puntos 3 y 5 de §1, los valores de ojos de §3 y las cifras de legibilidad a 15 y 25 m quedaron sustituidos por la **§8**. Las capturas de `F2/` son las finales, posteriores a esa ronda.

## 1. Lo que hay que saber

1. **El mapa ya no está a oscuras.** El fondo medio de los 18 cuartos de Map01 pasa de 3–19 a 17–66 sobre 255, y los píxeles casi negros bajan de 39–98 % a 2–44 %.
2. **Los ojos de los robots ahora brillan en rojo** en los siete enemigos, con el estado por intensidad y pulso (opción A). No se añadió ninguna luz.
3. **A 25 m la lectura del enemigo empeora con la métrica del proyecto.** A 5 y 15 m siguen siendo legibles todos los casos (36 de 36 y 29 de 29). A 25 m pasan de 25 de 25 a 6 de 25. Motivo: la métrica mide luminancia, antes la sala estaba negra y el contorno era blanco; ahora la sala está iluminada, el contorno es azul acero y un ojo rojo no cuenta como "brillante". Es una consecuencia de lo aprobado y necesita tu decisión (§6).
4. **El Carroñero de combate ya no patina** y avisa antes de atacar. Solo cambia en Map01; el del hangar y el del menú siguen igual.
5. **El ojo del BigBot no queda centrado** en la cabeza y el rojo de los ojos tira a rosa con el bloom (§6).

## 2. F2 · Luminosidad

Sin linterna. Ninguna luz nueva: los recuentos por cuarto son los mismos.

| Tramo | Cuartos | Ambiente | Intensidad de luces | Alcance | Exposición | Niebla |
|---|---|---|---|---|---|---|
| 1 | R01–R05 | ×2,2 | ×1,6 | ×1 | +0,6 EV | ×0,8 |
| 2 | R06–R13 | ×2,2 | ×1,8 | ×1 | +0,6 EV | ×0,8 |
| 3 | R14–R18 | ×2,7 | ×1,9 | ×1 | +0,8 EV | ×0,7 |

- **Cambio respecto a lo propuesto:** el alcance de las luces se queda como estaba. Ampliarlo subía de 10 a 13 los cuartos con más de 4 luces por objeto; lo compensé con algo más de intensidad. El validador vuelve a dar los mismos 10 cuartos que ya daba (R01, R02, R05, R06, R08, R11, R14, R15, R16 y R18).
- **Exposición:** un Volume global por cuarto (prioridad 5) con un perfil por tramo en `Assets/IronRequiem/Art/VisualPass12/Map01_Exposure_T1/T2/T3.asset`. `Campaign_PostProfile` no se tocó.
- **Datos:** `Assets/IronRequiem/Levels/Map01/Data/Lighting/R01…R18.json`, generados por `Tools/IronRequiem/VisualPass12/apply_luminosity.py` a partir del respaldo (se puede repetir).
- **Código:** `Editor/Map01/LightingAuthoring.cs` (`BuildRoomsOnly` :62, `BuildExposureVolume` :285) y `Editor/VisualPass12/VisualOverhaulLuminosity.cs` (crear perfiles, aplicar, hornear sondas, informe).
- **Escenas:** las 18 `Map01_R*.unity` se reconstruyeron solo en su capa `[Lighting]` y se rehornearon sus sondas de reflexión. Las 4 escenas de sesión no cambiaron.

### Antes y después (mismos puntos de vista de F0)

| Sala | Fondo vacío antes (v1 / v2) | Después | Negro < 10 antes | Después | T-07 a 15 m antes (enemigo : fondo, contraste) | Después |
|---|---|---|---|---|---|---|
| R01 ControlAcceso | 12,46 / 11,54 | **38,44 / 35,08** | 55,94 % | 2,68 % | 34,8 : 15,04 (1,99) | 36,38 : 40,5 (1,1) |
| R02 PozoMontacargas | 13,67 / 9,55 | **37,41 / 29,68** | 38,8 % | 5,22 % | 33,69 : 6,31 (3,42) | 34,05 : 21,22 (1,49) |
| R03 Dormitorios | 10 / 9,74 | **36,38 / 34,4** | 59,88 % | 3,9 % | 34,08 : 15,55 (1,9) | 36,71 : 44,14 (1,18) |
| R04 Comedor | 14,09 / 16,51 | **42,8 / 47,95** | 52,21 % | 1,96 % | 33,89 : 15,45 (1,9) | 36,15 : 45,64 (1,23) |
| R05 Enfermeria | 14,73 / 13,88 | **42,14 / 41,67** | 57,87 % | 5,17 % | 42,11 : 12,51 (2,69) | 40,54 : 39,87 (1,01) |
| R06 Armeria | 9,41 / 11,23 | **28,32 / 31,72** | 62,7 % | 20,18 % | 24,25 : 2,92 (3,69) | 31,23 : 16,19 (1,71) |
| R07 Comunicaciones | 12 / 11,31 | **34,63 / 33,97** | 59,05 % | 5,56 % | 33,61 : 9,13 (2,73) | 34,56 : 29,72 (1,14) |
| R08 Almacenes | 6,28 / 19 | **18,7 / 34,85** | 75,3 % | 43,73 % | tapado | tapado |
| R09 Taquillas | 11,68 / 14,95 | **28,29 / 34,18** | 58,43 % | 16,41 % | 32,84 : 19,67 (1,53) | 30,29 : 44,51 (1,4) |
| R10 TunelTrituradora | 4,63 / 7,53 | **16,69 / 22,09** | 86,41 % | 34,83 % | 32,91 : 10,01 (2,52) | 28,69 : 21,95 (1,25) |
| R11 Decantadores | 8,04 / 10,24 | **26,22 / 30,03** | 74,38 % | 39,62 % | 32,94 : 13,6 (2,04) | 33,84 : 35,67 (1,05) |
| R12 Colector | 11,04 / 9,88 | **35,79 / 32,41** | 67,77 % | 7,24 % | 34,12 : 24,41 (1,33) | 38,31 : 45,15 (1,16) |
| R13 SalaBombas | 14,98 / 17,23 | **39,75 / 43,26** | 49,76 % | 6,24 % | 32,5 : 15,11 (1,86) | 33,46 : 38,19 (1,12) |
| R14 Fundicion | 8,99 / 10,14 | **37,55 / 36,96** | 69,39 % | 14,13 % | 32,87 : 10 (2,52) | 39,68 : 35,82 (1,09) |
| R15 Ensamblaje | 15,73 / 10,88 | **65,9 / 59,37** | 49,83 % | 1,75 % | 36,77 : 15,86 (2) | 54,38 : 64,1 (1,16) |
| R16 Reactor | 13,41 / 14,27 | **45,25 / 45,77** | 59,9 % | 14,3 % | tapado | tapado |
| R17 CentroMando | 5,94 / 6,72 | **28,44 / 28,96** | 87,2 % | 2,25 % | 33,2 : 4,54 (4) | 36,46 : 26,06 (1,33) |
| R18 Boveda | 2,99 / 2,85 | **39,56 / 35,13** | 97,91 % | 3,71 % | 32,99 : 2,56 (5,03) | 42,48 : 38,5 (1,09) |
| Room00 Hangar | 9,52 / 16,21 | **9,53 / 16,41** | 75,31 % | 75,35 % | 28,14 : 16,17 (1,57) | 28,11 : 16,15 (1,57) |
| Room00b AlarmCorridor | 15,3 / 22,71 | **15,98 / 22,7** | 58,13 % | 54,16 % | tapado | tapado |
| Room01 Foundry | 4,35 / 4,29 | **4,36 / 4,3** | 91,21 % | 91,2 % | 32,62 : 12,53 (2,15) | 18,68 : 12,36 (1,36) |
| Room02 Breach | 4,11 / 4,1 | **4,12 / 4,11** | 90,98 % | 90,96 % | 33,42 : 8,39 (2,87) | 18,85 : 8,14 (1,81) |
| Room03 Assembly | 4,1 / 4,05 | **4,11 / 4,05** | 87,71 % | 87,7 % | 32,82 : 1,36 (5,95) | 18,59 : 1,17 (3,82) |
| Room04 Reactor | 12,54 / 11,92 | **12,54 / 11,92** | 81,97 % | 81,97 % | tapado | tapado |
| Pursuit05 | 0,61 / 0,32 | **0,61 / 0,32** | 99,95 % | 99,94 % | 29,01 : 1,6 (5,15) | 15,53 : 1,42 (3,2) |

| | Antes | Después |
|---|---|---|
| R01, T-07 a 15 m | ![](F0/zonas/Map01_R01_ControlAcceso_vp1_T07_15m.jpg) | ![](F2/zonas/Map01_R01_ControlAcceso_vp1_T07_15m.jpg) |
| R11, T-07 a 15 m | ![](F0/zonas/Map01_R11_Decantadores_vp1_T07_15m.jpg) | ![](F2/zonas/Map01_R11_Decantadores_vp1_T07_15m.jpg) |
| R14, T-07 a 15 m | ![](F0/zonas/Map01_R14_Fundicion_vp1_T07_15m.jpg) | ![](F2/zonas/Map01_R14_Fundicion_vp1_T07_15m.jpg) |
| R18, T-07 a 15 m | ![](F0/zonas/Map01_R18_Boveda_vp1_T07_15m.jpg) | ![](F2/zonas/Map01_R18_Boveda_vp1_T07_15m.jpg) |

Lectura:
- Cumplen el objetivo de fondo ≥ 45 R15 y R16, y se acercan R04, R05 y R13. El resto queda entre 17 y 43: es el nivel 1 aprobado, no el 2.
- Los más oscuros siguen siendo R08 (19 en la vista 1), R10 (17–22) y R11 (26–30).
- El hangar y la campaña antigua no cambian (son el control).

### Contorno del resaltado (corrección b1)

- `Scripts/Player/Vision/VisionMaterialRules.cs:10,14`: tinte azul acero (0,42; 0,62; 1) en vez de blanco, e intensidad a la mitad (1,6 → 0,8).
- Los umbrales de `EnemyDarkReadability` se quedan en los que calibró el chat 11 (0,5 y 1,6). Probé a bajarlos y la lectura a 25 m empeoraba más.
- En salas que siguen oscuras el contorno todavía se ve claro y azulado (por ejemplo en el Carroñero a 5 m en R01).
- **Efecto secundario:** en la campaña antigua, que sigue oscura, el robot pierde la mitad de su contorno. Allí los casos legibles pasan de 30 a 25 de 34.

## 3. F3 · Ojos rojos y luces de robots

Archivos nuevos: `Scripts/VisualOverhaul/EnemyEyeGlow.cs` (494 líneas) y `EnemyEyeStyle.cs` (156). No se editó ningún prefab, material ni escena.

- **Cómo funciona:** un gestor creado en ejecución dibuja todos los ojos con un único sistema de partículas (1 draw call para todos los robots; 3 partículas por ojo: halo, núcleo y centro). Sin `Light` ni objetos por robot. Reescanea enemigos cada 0,5 s.
- **Color:** rojo carmesí (1; 0,05; 0,12), con el centro en rojo más claro (1; 0,38; 0,32). Tope de HDR 4.
- **Estados (opción A):** patrulla HDR 2 fijo; alerta HDR 3 con pulso de 1,5 Hz; ataque HDR 4 fijo; aviso de disparo con dos destellos en los últimos 0,35 s (sustituye al estrobo de 14 Hz); al morir se apaga.
- **Anclajes:** T-07 y T-07 Mech en la esfera de la cabeza; SawBot en su indicador; HoverBot en el ojo; torreta en el iris; BigBot con un solo ojo en la cabina; Carroñero delante de la lente frontal, con su punto rojo flotante oculto.
- **`EnemyStateLights`** se desactiva en cada robot gestionado para que no siga escribiendo cian y ámbar, y se restaura si se suelta.
- **Exclusiones:** todo lo que tenga `ScavengerBehaviour`, `BridgeWalker` o `CctvActor`, la escena IntroMenu, y los enemigos del hangar hasta que termina la cinemática.
- **Interruptor:** `EnemyEyeGlow.Enabled` y el grupo de ablación `eyes`.

| | Antes | Después |
|---|---|---|
| T-07, ataque | ![](F0/robots/T07_Current_Repaired_spawn_frente.jpg) | ![](F2/robots/T07_Current_Repaired_ataque_frente.jpg) |
| Carroñero | ![](F0/robots/Carronero_spawn_frente.jpg) | ![](F2/robots/Carronero_spawn_frente.jpg) |
| BigBot, ataque | ![](F0/robots/BigBot_Current_ataque_frente.jpg) | ![](F2/robots/BigBot_Current_ataque_frente.jpg) |
| T-07 a 15 m | ![](F0/robots/T07_Current_Repaired_spawn_15m.jpg) | ![](F2/robots/T07_Current_Repaired_spawn_15m.jpg) |

Las capturas de "antes" se hicieron en la sala a oscuras y las de "después" con la luz nueva; por eso cambia también el fondo.

Cambios respecto a la maqueta aprobada:
- Patrulla y alerta suben de HDR 1,6 y 2,6 a 2 y 3: sobre el fondo iluminado el rojo de patrulla casi no se veía.
- El ojo lejano se dibuja algo mayor que el real (mínimo de 4 píxeles) para que se lea a distancia.
- Probé un centro casi blanco para cumplir la métrica de "ojo visible" a 25 m y lo descarté: el ojo se veía blanco, no rojo.

## 4. F5 · Carroñero de combate (nivel mínimo, A1–A7)

Archivos nuevos: `Scripts/Enemies/CarroneroCombatMotion.cs` (592 líneas) y `Editor/VisualPass12/VisualOverhaulCarronero.cs`. El componente se añadió solo a `Prefabs/Map01/Enemies/Carronero_Map01.prefab`, donde `CarroneroMotion` queda desactivado. `Carronero.prefab` y `CarroneroMotion.cs` no cambiaron.

| Medida | Antes (`Carronero.prefab`) | Después (`Carronero_Map01`) |
|---|---|---|
| Deslizamiento del pie plantado | Sin pie plantado (0 % de frames) | 0,72 cm por frame como máximo; pie plantado el 70 % de los frames |
| Velocidad del pie de apoyo (métrica de F0) | 6,5 m/s | 2,1–2,4 m/s (incluye la fase de vuelo) |
| Cadencia de paso a 7,2 m/s | ≈ 12,5 pasos/s | 4,5 pasos/s |
| Anticipación del ataque | Brazo sube en 0,17 s | Sierra detrás del hombro de 0,03 s a 0,30 s (0,27 s de aviso) |
| Primer daño | Tras 0,35 s de preparación | Igual: 0,35 s |
| Reacción al impacto | 0° | 12° en el torso y 14° en la cabeza en 3 frames; vuelve en 0,27 s |
| Giro | Bloque rígido | Inclinación de 8,8° hacia el interior; la cabeza adelanta hasta 19° |
| Vuelta a cámara | Salto de pose | Mezcla de 0,2 s; salto de 0° en el primer frame |
| Fase entre individuos | Todos en fase | Dos creados juntos quedan a 0,36–0,39 de ciclo |

- **Coste (Editor, 24 Carroñeros corriendo a la vista):** 0,31 ms la animación nueva frente a 0,33–0,48 ms la antigua, en tres pasadas. Con los ajustes por defecto costaba 0,41 ms (+40 %), así que el pie plantado se limita al LOD 0 y a partir del LOD 1 (unos 10 m) la animación se actualiza un frame de cada dos. Esos ajustes están guardados en el prefab (`ikMaxLod: 0`, `halfRateFromLod: 1`).
- **No cambia:** daño, alcance, cadencia, velocidad, vida, IA ni la muerte (sigue siendo el estallido inmediato).
- **Tiras de frames:** [antes](F0/carronero/) y [después](F2/carronero/). En las de después se forzó el LOD 0.

![](F2/carronero/01_correr.jpg)
![](F2/carronero/02_atacar.jpg)

## 5. Verificación

**Compilación:** sin errores.

**Tests nuevos (todos pasan):**

| Suite | Tipo | Casos |
|---|---|---|
| `VisualPassLightingTests` | EditMode | 4 de 4 |
| `VisualPassEyesEditTests` | EditMode | 11 de 11 |
| `VisionReadabilityEditTests` (chat 11) | EditMode | 3 de 3 |
| `VisualPassEyesTests` | PlayMode | 26 de 26 |
| `VisionReadabilityTests` (chat 11) | PlayMode | 2 de 2 |
| `VisualPassCarroneroTests` | PlayMode | 11 de 11, dos pasadas seguidas |

Ajustes que hice en tests al ejecutarlos:
- `VisualPassLightingTests`: añadí R13.A5 y R13.A6 (verde de las bombas, dato del chat 9) a las excepciones conocidas de colores de puerta.
- `VisualPassCarroneroTests`: 1 ms de holgura en el instante del primer contacto (medía 0,40000004 frente a 0,4) y el criterio de fase de la horda pasa de "ningún par a menos de 0,002" a "sin duplicados exactos y fases repartidas", porque 24 fases aleatorias caen a veces a 0,001. El test de LOD lee ahora el ajuste guardado.
- `VisionReadabilityTests`: un umbral fijaba el valor antiguo del contorno.

**Regresión:**
- `Map01F2Tests`: sesiones 2, 3 y 4 pasan. La sesión 1 falla en "Room00_Hangar: respite without Map01StageRelease", una comprobación anterior a que el chat 9 hiciera empezar S1 en el hangar. No la ejecuté con el código anterior para confirmarlo.
- No ejecutados: `CampaignTests`, `HangarIntroPresentationTests`, `MainMenuRestoreTests` ni el resto de suites de enemigos.

**Zona protegida:** instantáneas `vp-before-20261120` y `vp-after-f2f5-20261121`, 508 archivos. Única diferencia: el atlas dinámico de TextMesh Pro, que Unity reescribe en cada Play Mode.

**Archivos cambiados frente al respaldo** (`Tools/IronRequiem/Backups/20261121-visualpass-f2f5/`): 18 JSON de luz, 18 escenas de cuarto, `LightingAuthoring.cs`, `Carronero_Map01.prefab` y `VisionMaterialRules.cs` (`EnemyDarkReadability.cs` quedó idéntico al original).

**No hecho:**
- Medición en build Release (peor cuarto y horda). Las cifras de coste son del Editor.
- Prueba manual con teclado y ratón.
- Captura en vídeo; solo hay tiras de frames.
- F4 decoración.

## 6. Problemas conocidos y decisiones pendientes

| # | Problema | Opciones |
|---|---|---|
| 1 | A 25 m solo 6 de 25 casos son "legibles" con la métrica de luminancia (antes 25 de 25) | (a) Aceptarlo: en la imagen el robot se ve como figura oscura con ojo rojo. (b) Dar al contorno un mínimo azul acero también en salas iluminadas. (c) Volver a un centro de ojo más claro, que se lee menos rojo |
| 2 | El rojo de los ojos tira a rosa con el bloom | Bajar el azul del carmesí; se acercaría al rojo de puerta bloqueada (1; 0,03; 0,02) |
| 3 | El ojo del BigBot queda en la esquina de la cabeza, no centrado | Ajustar `bigBotDown` y `bigBotForward` en `EnemyEyeGlow` con capturas |
| 4 | En la campaña antigua, sin luz nueva, el contorno a la mitad resta lectura (30 → 25 de 34) | Aplicarle también la luminosidad o dejarla como está, ya que JUGAR lleva a Map01 |
| 5 | El material de los ojos se crea en ejecución con `Shader.Find`; en un build la variante aditiva podría quedar fuera | Comprobarlo en el primer build; si falla, crear el material como asset |
| 6 | Los enemigos se autodestruyen por debajo de y = −20 m (R08–R18) | Hallazgo de F0 para el chat 9; mis tests lo evitan solo en ejecución |

## 7. Cómo deshacerlo

- **Luminosidad:** restaurar los 18 JSON y `LightingAuthoring.cs` del respaldo y ejecutar `VisualOverhaulLuminosity.Apply` y `.BakeProbes`; o restaurar directamente las 18 escenas del respaldo.
- **Ojos:** `EnemyEyeGlow.Enabled = false`, o borrar los dos scripts.
- **Carroñero:** `VisualOverhaulCarronero.Revert` (quita el componente y reactiva `CarroneroMotion` en la variante).
- **Contorno:** restaurar `VisionMaterialRules.cs` del respaldo.

## 8. Ronda tras la revisión independiente (21/11/2026)

El revisor independiente ([03_REVISION.md](03_REVISION.md)) puntuó el primer resultado así (antes → después del pase): espacio 3 → 7, enemigo 6 → 5, ambiente 4 → 6, ojos 4 → 5, Carroñero 3 → 5. Las notas son de **antes** de esta ronda; no se ha repetido la revisión con los cambios de abajo.

### Qué se corrigió

| Defecto del revisor | Cambio |
|---|---|
| Ojos rosas o magenta | Color base sin azul: (1, 0.04, 0.03). Centro caliente rojo (1, 0.22, 0.14), no blanco. |
| Estados que no se distinguen | Patrulla, alerta y ataque cambian tamaño (núcleo 0.09 / 0.14 / 0.20 m) e intensidad (1.8 / 3.4 / 4); alerta pulsa a 1.5 Hz. |
| Ojo del T-07 en el abdomen | Anclado a la parte alta de la cabeza. Comprobado en captura. |
| Ojo del BigBot y de la torreta descolocados | Anclajes rehechos. El del BigBot queda en la cabina, aproximado. |
| Indicadores del BigBot de otro color | Rojo tenue y fijo (55 % del color base). |
| Ojo que se pierde de lejos | Refuerzo de intensidad entre 10 y 22 m (hasta ×2.2). |
| Contorno azul como chispas flotantes | Borde más ancho y suave (potencia 2.8); 0.16 con luz y 0.6 a oscuras. |

### Legibilidad del enemigo con la métrica del proyecto (Map01)

| Distancia | F0 | Tras F2/F3 | Tras esta ronda |
|---|---|---|---|
| 5 m | 36 de 36 | 36 de 36 | 36 de 36 |
| 15 m | 29 de 29 | 29 de 29 | 26 de 29 (27 contando píxeles de ojo rojo) |
| 25 m | 25 de 25 | 6 de 25 | 4 legibles, 5 débiles, 16 ilegibles |

La métrica mide luminancia. Un rojo puro no puede superar luminancia 54 de 255, así que el ojo nunca cuenta como "punto brillante" (umbral 170), y el contorno más suave resta contraste medido. En los recortes a 15 y 25 m el robot se ve como figura oscura con un ojo rojo y un borde azulado, pero eso es lectura visual mía, no una medida.

### Verificación de esta ronda

- **Compilación:** sin errores.
- **Tests EditMode:** 19 de 19 (`VisualPassEyesEditTests`, `VisionReadabilityEditTests`, `VisualPassLightingTests`).
- **Tests PlayMode:** 28 de 28 (`VisualPassEyesTests`, `VisionReadabilityTests`) y 11 de 11 (`VisualPassCarroneroTests`).
- **Zona protegida:** 508 filas comparadas contra la instantánea previa al pase; la única diferencia es el atlas dinámico de TextMesh Pro, que cambia solo.
- **Manual:** no hecho. Nadie ha jugado esto con teclado y ratón.

### Sin hacer o sin comprobar

- El efecto del contorno más suave en la campaña antigua no se volvió a medir.
- Build Release, peor cuarto y horda de 24: sin medir en este pase.
- `CampaignTests`, `HangarIntroPresentationTests` y `MainMenuRestoreTests` no se ejecutaron.
- `Map01F2Tests.SessionTraversal(0)` falla ("Room00_Hangar: respite without Map01StageRelease"); creo que ya fallaba antes, sin confirmar.
- Puntos del revisor no tocados: luz de relleno plana, muerte y giro del Carroñero, anticipación floja.
- Los enemigos se autodestruyen por debajo de y = -20, y los cuartos R08–R18 de Map01 están por debajo. Es del chat 9.

### Decisión pendiente

Lectura del enemigo a 25 m: aceptar el estado actual, reforzar el contorno o añadir acentos emisivos al cuerpo, o aclarar el centro del ojo (vuelve a tirar a rosa).
