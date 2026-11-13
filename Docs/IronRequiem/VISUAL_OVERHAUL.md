# IRON REQUIEM: mejora visual, shader propio y limpieza (07-08/11/2026)

Unity 6000.5.5f1, URP 17.5, Input System 1.19. Todo se hizo con variantes y copias: **MainScene, prefabs, modelos y materiales originales no se modificaron** (hash de MainScene, `Packages/manifest.json`, `packages-lock.json` y `ProjectSettings/*.asset` iguales al inicio y al final, ver §8). Sin paquetes nuevos, sin créditos, sin commits, sin control del escritorio.

Respaldos: `Tools/IronRequiem/Backups/20261107-visual-overhaul/` (hashes iniciales, `PlayerWeaponsManager.cs` original, `Default_PipelineAsset.asset` original, instantáneas de ProjectSettings de cada build).
Evidencias: `Tools/IronRequiem/Reports/VisualOverhaul/` (capturas en `Shots/`, resultados de tests, inventario, variantes) y `Tools/IronRequiem/Reports/CombatLoad/vo-*` (benchmark).

> **Estado (08/11/2026, tras pasar a decales de malla):** todo lo pedido está hecho en variantes y verificado, **salvo el criterio de rendimiento**: el build B con todo el trabajo es **+1,0 ms de media y +1,5 ms de p99** más lento que A (antes de este cambio: +1,2 y +1,7) en el benchmark de combate (§6). Queda para tu decisión (§9). MainScene sin aplicar; cuarentena sin borrar.

## 1. Limpieza del proyecto

- Inventario de los 1.756 archivos de `Assets` (modelos, prefabs, materiales, texturas, escenas, audio, animación...) con `Tools/IronRequiem/visual_overhaul_inventory.js` (solo lectura).
- **En uso** = alcanzable por GUID desde un root: escenas de Build Settings, carpetas `Resources`, GUIDs de `ProjectSettings`, y rutas o nombres de assets escritos en C# (juego, herramientas de Editor y tests, que cargan los laboratorios por ruta). Se siguen todas las referencias serializadas (escenas, prefabs, materiales, controladores, remapeos de materiales en `.meta`, shader graphs). Addressables no está instalado.
- **Conservado** (no candidato aunque nada lo use en juego): código y shaders, terceros (`Plugins`, `TextMesh Pro`, `NavMeshComponents`), tutoriales IET (el paquete los carga por tipo), configuración de render, archivos citados en `Docs/` o en scripts de `Tools/` (modelos fuente), carpetas `Source/` y todo lo que esos archivos necesitan.
- **Sin uso**: 252 assets (110,7 MB), movidos con sus `.meta` a `Tools/IronRequiem/Quarantine/20261107/` (fuera de Assets, misma ruta relativa, `manifest.json` con SHA-256 de cada archivo). **Nada se borró.** Restaurar: `node Tools/IronRequiem/visual_overhaul_quarantine.js restore 20261107`.
- Comprobaciones tras la cuarentena: compila sin errores; referencias rotas en las 19 escenas de Build Settings y 188 prefabs = 14, todas el mismo `RawImage` de vista previa de captura del `InGameMenu`, cuyo GUID no existe ni en Assets ni en la cuarentena (**ya estaba roto antes**; no lo causó la cuarentena); EditMode y PlayMode con los mismos resultados que antes de la cuarentena (§7); build completo del juego (19 escenas de Build Settings, Release) correcto, con 0 errores.

Tabla completa (ruta, tamaño, estado y prueba): [VisualOverhaul/CLEANUP_TABLE.md](VisualOverhaul/CLEANUP_TABLE.md) y CSV con las 1.756 filas en `Tools/IronRequiem/Reports/VisualOverhaul/inventory-before-quarantine.csv`. Lo más grande en cuarentena: `Assets/FPS/Scenes/SecondaryScene.unity` (37 MB, escena de plantilla sin uso), 32 texturas de `Assets/FPS/Art/Textures` (57 MB), 61 prefabs de nivel de plantilla y los paquetes de props/fuentes de `Assets/ModAssets`.

**Borrado definitivo: pendiente de tu confirmación explícita.**

## 2. Shader `IronRequiem/HardSurface`

`Assets/IronRequiem/Art/Shaders/HardSurface/IronRequiemHardSurface.shader` (HLSL, URP Forward). Un solo shader para entorno, módulos del jugador y robots.

- **SRP Batcher**: todas las propiedades en `UnityPerMaterial`. Instancing activado en los materiales.
- **Variantes mínimas**: funciones con `shader_feature_local` (`_NORMALMAP`, `_TRIPLANAR`, `_DIRT`, `_EDGEWEAR`, `_RIM`, `_PANELS`); solo los `multi_compile` de iluminación que usa este proyecto (sombra principal, luces adicionales y sus sombras, sombras suaves, SSAO, niebla, instancing). Sin lightmaps, Forward+, light layers, cookies, DBuffer ni debug.
- Suciedad en espacio mundo (ruido triplanar + franja de suelo + cavidades), desgaste de bordes por curvatura en pantalla (derivadas de la normal) o por máscara (canal G), rim/fresnel emisivo opcional, paneles emisivos con pulso por tiempo y `_PanelBoost` (encendido de luces y anillos), triplanar opcional para mallas sin UV a escala.
- **Daño** `_Damage` 0-1 (rama uniforme, sin keyword): oscurece, quema por ruido con borde incandescente y pulsa en rojo. `RobotDamageProgression` y `BigBotDestruction` lo usan sin código nuevo: sus materiales de etapa en las variantes son materiales HardSurface con `_Damage` 0,35 / 0,65 / 0,95 (materiales compartidos: sin instancias y sin romper el SRP Batcher).
- Pases: ForwardLit, ShadowCaster, DepthOnly, DepthNormals (SSAO) y Meta (horneado).
- Runtime: solo `MaterialPropertyBlock` o materiales compartidos; ningún `new Material` en juego.

Coste en variantes compiladas en el Player (contadas tras el stripping de URP):

| Shader | Build A (antes) | Build B (después) |
|---|---|---|
| URP/Lit (todos los pases) | 886 | 886 (incluye los decales de malla, Lit transparente) |
| IronRequiem/HardSurface | 0 | 158 (128 fragment ForwardLit) |
| Shader Graphs/Decal | 0 | 0 (antes 88, con decales URP) |
| **Total** | **886** | **1.044** (antes 1.068) |

Coste en GPU: ver §6 (medido dentro del benchmark A/B, no aislado).

## 3. Mapa: `Assets/IronRequiem/Scenes/VisualOverhaul/MainScene_VisualLab.unity`

Copia de MainScene. Generado por `VisualOverhaulScenes.BuildLab` (reproducible, semilla fija).

- **Jugador**: el rig del protagonista de BunkerLab (antebrazo modular, mangas, puño, ejecución, HUD térmico) sustituye al jugador de plantilla, en la misma posición. Inventario: cañón continuo + escopeta (§4). MainScene tenía solo la pistola térmica; el cambio es necesario para probar los módulos y las mangas.
- **Enemigos**: las 3 instancias de MainScene (2 T-07, 1 torreta) reemplazadas por sus variantes `_VO` (misma IA, vida y armas). Ninguno eliminado.
- **Materiales**: 1.973 renderers del nivel con HardSurface triplanar (hormigón de suelo, paneles de pared, metal oxidado) con suciedad y desgaste. Texturas del búnker existentes reutilizadas.
- **Cierre**: techo solo visual (sin collider, sin sombras, excluido del NavMesh) a la altura máxima de los muros (8,05 m); fondo de cámara sólido color niebla.
- **Decales de malla** (22 en el lab, 12 en el benchmark; 08/11/2026, sustituyen a los decales URP por rendimiento): quads estáticos pegados a la superficie (+2 cm) con URP Lit transparente (alpha blend, sin sombra, sin escritura de profundidad), con static batching y sin renderer feature ni renderer extra. Se colocan por raycast en los mismos puntos que los decales URP; cada esquina se comprueba contra la superficie y el quad se reduce o se descarta si sobresale de un borde (2 de 24 descartados en el lab). Manchas, quemaduras, chorreones de óxido y señales de peligro originales. Herramienta: `VisualOverhaulDressing.MeshDecal` / `ConvertProjectorsToMesh`. Capturas iguales a las de antes: `Shots/map-meshdecals-1..4.png`.
- **Piezas modulares** (mallas propias reutilizadas, estáticas para static batching, materiales con instancing): tramos de tubería con bridas y soportes, haces de cables colgantes, rejillas de ventilación, lámparas de techo y luces de emergencia enjauladas.
- **Iluminación**: lámparas frías de techo con emisión y luz puntual (2 con sombra), emergencia roja y ámbar en muros, 3 tubos que fallan (`LightFlicker`: ruido determinista a 20 Hz, sin GC, cambia entre dos materiales compartidos). Sol de relleno tenue y frío.
- **Sondas**: `LightProbeGroup` en rejilla sobre el NavMesh (0,6 / 2,2 / 4 m) y reflection probes con proyección de caja por zona de ~36 m (16 en el lab, 6 en el benchmark). **Horneado**: luces Mixed en modo *Baked Indirect*; el entorno estático aporta rebote pero **no se generan lightmaps** (decisión: los modelos de plantilla no garantizan UV2 y los lightmaps añadían variantes `LIGHTMAP_ON`); el rebote llega a enemigos y jugador por las light probes. El estático no muestrea probes (coste de CPU medido, §6).
- **Ambiente**: niebla exponencial², un emisor de polvo que sigue a la cámara (≤70 partículas, sin colisión) y vapor en 2 rejillas (≤16 partículas por sistema, sin colisión).
- **Límite de 4 luces adicionales por objeto**: comprobado por renderer (esfera de luz contra bounds). En el lab, 17 de los 1.973 renderers del nivel (0,9 %) tienen 5-6 luces candidatas (muros junto a una luz de emergencia y dos lámparas); dividirlos en piezas de 6-8 m no bajó la cifra, así que quedan enteros: URP aplica las 4 más importantes. Lista en `Reports/VisualOverhaul/lab-light-budget-over.txt`. En el benchmark, las luces son las de BunkerLab (iguales en A y B): ya había 86 renderers con más de 4 luces candidatas; no se añadieron luces.
- **NavMesh**: rehorneado solo en la variante, en un asset nuevo `MainScene_VisualLab_NavMesh.asset`; el de MainScene sigue intacto. La decoración está excluida (NavMeshModifier). El NavMesh resultante tiene 407 vértices frente a 363 del asset original (el original es más antiguo que la geometría actual del nivel); los enemigos navegan en el test de luces por estado.
- **Bloom**: no se añadió ni se subió. El perfil existente de la escena ya tenía Bloom activo y se dejó igual (motion blur y profundidad de campo siguen desactivados). **Pregunta pendiente: ¿quieres el bloom existente apagado en la variante?**

Capturas (mismas posiciones de cámara, render del Editor sin jugador):

| Antes (MainScene) | Después (VisualLab) |
|---|---|
| ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/map-before-1.png) | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/map-after-1.png) |
| ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/map-before-2.png) | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/map-after-2.png) |
| ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/map-before-3.png) | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/map-after-3.png) |

## 4. Armas del protagonista como módulos del antebrazo

Variantes (originales intactos), generadas por `VisualOverhaulWeapons`:
- `Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_IR_ArmCannon_VO.prefab` (de `Weapon_IR_ArmModule_v07_ExtendedHeat`): collar de acople, carcasa de placas con las 4 cápsulas de calor encajadas encima, cañón largo único con 4 anillos emisivos que se encienden mientras dispara (suben con cada disparo y se apagan al soltar).
- `Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_Shotgun_ArmModule_VO.prefab` (de `Weapon_Shotgun_Secondary_Thermal`): carcasa con las **4 celdas de energía visibles** en una cuna, 4 tubos agrupados 2x2 con ligera apertura y abrazaderas. Conserva las 4 cargas y la recarga: las celdas se hunden con el silbido (`Weapon_Cooling.wav`) y el vapor existentes.
- Diseño original: placas cerámicas hueso, juntas grafito, banda naranja y ranuras cian, la misma paleta y lenguaje de placas que el traje del protagonista (materiales `VO_Suit_*`). No copia siluetas ni colores de personajes existentes.
- Daño, cadencia, munición, calor, proyectil, boca y punto `Attachment` sin cambios.

**Nota de diseño**: la escopeta actual de BunkerLab es `Weapon_Shotgun_HeatOnly` (solo calor, sin cartuchos). Como pediste "conservar 4 cargas y su recarga", el módulo se basa en `Weapon_Shotgun_Secondary_Thermal` (4 cargas + recarga + calor). Si quieres la versión solo calor, es la misma operación sobre ese prefab.

**Animación de cambio** (`ArmModuleVisual`, sustituye a `SmoothWeaponSwitch` solo para estas dos armas mediante la interfaz `IWeaponSwitchMotion`; el resto de armas conserva el movimiento de plantilla):
1. Bloqueo (30 % de la fase de salida): las placas de las juntas se abren, "clac" (`Notification_Snap.wav`), silbido y vapor desde el puerto del antebrazo, las luces se apagan en orden inverso.
2. El módulo se separa del antebrazo y sale fuera de cámara (el antebrazo, la boca y el `Attachment` no se mueven).
3. Entra el nuevo, se acopla con un golpe seco (`Land.wav` + pequeño impulso de cámara), las juntas se cierran y las luces se encienden en secuencia.

Medido en Play Mode (60 Hz simulados): **0,62-0,65 s**, **0 disparos** durante el cambio con el gatillo pulsado, funciona con ADS mantenido (apunta al terminar) y durante un deslizamiento; vuelve a disparar al terminar. Cambio en `PlayerWeaponsManager.cs` (plantilla): solo añade el hook opcional (`SwitchState`, `SwitchPhaseProgress`, `IWeaponSwitchMotion`); sin el componente, el comportamiento es idéntico al anterior.

Secuencia del cambio (cañón → escopeta, cada 4 frames):

| | | |
|---|---|---|
| ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/swap-04.png) | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/swap-12.png) | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/swap-16.png) |
| ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/swap-20.png) | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/swap-28.png) | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/swap-36.png) |

Módulos aislados, antes y después:

| | Antes | Después |
|---|---|---|
| Cañón | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/cannon-before.png) | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/cannon-side.png) |
| Escopeta | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/shotgun-before.png) | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/shotgun-side.png) |

**Brazo cortado** (mangas de BunkerLab): sonda por frame del extremo del codo del antebrazo FPS dentro de la vista sin manga que lo cubra, igual que `BunkerPolishTests`.

| Acción | Antes (BunkerLab, módulos v07) | Después (VisualLab, módulos nuevos) |
|---|---|---|
| Puño con cañón | 0 / 32 frames con corte | 0 / 32 |
| Puño con escopeta | 0 / 32 | 0 / 32 |
| Ejecución T-07 | 0 / 70 | 0 / 70 |
| Ejecución Big Bot | 0 / 70 | 0 / 70 |

Capturas: `Shots/arms-before-*` y `Shots/arms-after-*` (puño y ejecuciones, cada 8-12 frames).

## 5. Enemigos (T-07, Big Bot, HoverBot, torreta)

Variantes `_VO` en `Assets/IronRequiem/Prefabs/VisualOverhaul/` de los prefabs que usan MainScene, el benchmark y los laboratorios de fase (T07_Common_Destructible, Enemy_Turret, T07_HitFeedback_v04_NavThrottle, BigBot_Burst_v10_NavThrottle, T07_LegRepair_Charged_v05_Execution, BigBot_Burst_v10_Execution, HoverBot_CombatPolish, Turret_CombatPolish). Comportamiento, daño e IA sin cambios.

- Materiales HardSurface: desgaste de bordes, paneles emisivos (mapa de emisión existente), rim suave frío para leer la silueta en la oscuridad; un mismo conjunto de keywords para todos los robots. Etapas de daño con `_Damage`.
- `EnemyStateLights`: color por estado leído de la IA existente (patrulla turquesa, alerta ámbar, ataque rojo), **aviso parpadeante blanco antes del disparo** (carga real del T-07 o el final de `DelayBetweenShots` en armas sin carga) y apagado al morir (además de `EnemyPowerIndicators`). Solo `MaterialPropertyBlock`, escrito al cambiar.
- Efectos: fogonazo 1,7x; proyectiles con estela emisiva corta (TrailRenderer en el prefab del proyectil, sin luces); chispas de impacto x2 en `RobotHitFeedback`; explosión de muerte con partículas 1,35x y **una luz breve (0,16 s) por explosión del pool**, con un máximo global de 2 encendidas a la vez (`BurstFlashLight`). Todo dentro de `RobotDestructionPool` / `PooledEnemyDeathBurst` existentes; sin luces por proyectil.

Medido en Play Mode (T-07 del VisualLab): patrulla → ataque al acercarse → aviso 0,5 s antes del disparo → muerto (luces apagadas). La alerta (ámbar) no se observó en el test porque el T-07 pasó directo a ataque al detectar a 16 m.

| Antes | Después |
|---|---|
| ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/enemy-t07-before.png) | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/enemy-t07-after.png) |
| ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/enemy-bigbot-before.png) | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/enemy-bigbot-after.png) |
| ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/enemy-hoverbot-before.png) | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/enemy-hoverbot-after.png) |
| ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/enemy-turret-before.png) | ![](../../Tools/IronRequiem/Reports/VisualOverhaul/Shots/enemy-turret-after.png) |

Mismo set de luces de previsualización para antes y después. En juego: `Shots/enemy-t07-attack.png`, `Shots/enemy-t07-warning.png`.

## 6. Rendimiento (ejecutable Release, 1080p pantalla completa exclusiva, 5 min de combate)

Protocolo: `CombatLoadBenchmark` + `RunCombatLoad.ps1` (12 robots por oleada, 15 oleadas de 20 s, disparo y cambio de arma por dispositivos virtuales, VSync off, D3D11). Mismo fixture (BunkerBenchmark) en dos copias:
- **A** `VisualBench_A.unity`: estado actual (armas actuales ExtendedHeat + Secondary_Thermal, enemigos y búnker actuales).
- **B** `VisualBench_B.unity`: la misma escena con todo lo de este trabajo (HardSurface, decales, piezas, sondas horneadas, módulos con animación de cambio, enemigos `_VO` con efectos).
Pasadas seguidas, alternando A y B, con potencia, reloj, temperatura y P-state de la GPU (nvidia-smi cada 5 s).

### Serie final con decales de malla (08/11/2026; builds `vo-a-release-md` / `vo-b-release-md`; pasadas seguidas A, B, A, B)

| Pasada | Muestras | Media | Mediana | p95 | p99 | Máx | >8,33 ms | >16,67 ms | >33 ms | >50 ms | CPU | GPU (p99) | Hilo principal | Hilo de render | GPU W (σ) | Reloj MHz (σ) | Temp | P-state | Disparos jug./enem. | Muertes | GC | Errores |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| vo-a7 | 25852 | 11.61 | 11.92 | 15.77 | 17.56 | 33.7 | 85.1 % | 2.26 % | 1 | 0 | 11.61 | 4.83 (7.45) | 11.03 | 3.05 | 15.6 (4.9) | 1275 (447) | 42 | P8/P0/P4/P3 | 870/1591 | 180 | 33 | 0 |
| vo-b7 | 23984 | 12.51 | 12.77 | 17.18 | 18.95 | 39.9 | 90.2 % | 7.71 % | 1 | 0 | 12.51 | 5.48 (8.28) | 11.92 | 3.68 | 16.1 (4.8) | 1362 (495) | 44 | P8/P0/P5 | 828/1599 | 180 | 36 | 0 |
| vo-a8 | 25968 | 11.56 | 11.81 | 15.98 | 17.70 | 29.2 | 85.1 % | 2.76 % | 0 | 0 | 11.56 | 4.84 (7.41) | 10.98 | 3.10 | 16.2 (3.2) | 1350 (354) | 44 | P0/P4 | 870/1562 | 180 | 33 | 0 |
| vo-b8 | 23615 | 12.71 | 12.94 | 17.39 | 19.28 | 31.8 | 92.4 % | 8.46 % | 0 | 0 | 12.71 | 5.60 (8.56) | 12.12 | 3.73 | 16.5 (3.4) | 1384 (388) | 44 | P8/P0 | 829/1569 | 180 | 36 | 0 |

**Media de las dos pasadas de cada lado (B − A):** frame medio 11,59 → 12,61 ms (**+1,03 ms**), mediana 11,87 → 12,86 (+0,99), p95 15,88 → 17,29 (+1,41), **p99 17,63 → 19,12 ms (+1,49)**, frames > 8,33 ms 85,1 → 91,3 %, frames > 16,67 ms 2,5 → 8,1 %, frames > 33 ms 1 → 1 en total (0,5 por pasada en los dos lados), > 50 ms 0 → 0. CPU (frame) +1,03 ms, hilo principal 11,01 → 12,02 (+1,01), hilo de render 3,08 → 3,71 (+0,63), GPU 4,84 → 5,54 (+0,70). Potencia de GPU 15,9 / 16,3 W, reloj 1.313 / 1.373 MHz y 43-44 °C: condiciones equivalentes (B incluso con algo más de reloj). Las cuatro pasadas a 1920x1080 en pantalla completa exclusiva (`vo-*-display.txt`), igual que la serie anterior.

**Efecto de los decales de malla:** la diferencia B − A baja de +1,20 a **+1,03 ms** de media y de +1,74 a **+1,49 ms** de p99, y los frames > 16,67 ms de B bajan de 11,0 % a 8,1 %. Ahorro real ≈ 0,2 ms de media, menos que los 0,4-0,8 ms de la ablación (que quitaba los decales del todo, no los cambiaba por quads). **Sigue sin cumplir tu criterio** (+0,3 ms de media, p99 sin subir).

### Serie anterior con decales URP (build B optimizado; pasadas seguidas A, B, A, B)

| Pasada | Muestras | Media | Mediana | p95 | p99 | Máx | >8,33 ms | >16,67 ms | >33 ms | >50 ms | CPU | GPU (p99) | Hilo principal | Hilo de render | GPU W (σ) | Reloj MHz (σ) | Temp | P-state | Disparos jug./enem. | Muertes | GC | Errores |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| vo-a5 | 25216 | 11.90 | 12.16 | 16.20 | 17.97 | 32.6 | 88.1 % | 3.29 % | 0 | 0 | 11.90 | 5.01 (7.53) | 11.32 | 3.13 | 15.6 (3.2) | 1267 (347) | 44 | P0/P4 | 870/1567 | 180 | 33 | 0 |
| vo-b5 | 23245 | 12.91 | 13.12 | 17.70 | 19.55 | 33.7 | 93.4 % | 10.57 % | 1 | 0 | 12.91 | 5.50 (8.36) | 12.34 | 3.56 | 16.1 (3.4) | 1352 (355) | 44 | P0/P4 | 827/1613 | 180 | 33 | 0 |
| vo-a6 | 25394 | 11.82 | 12.10 | 16.07 | 17.96 | 31.6 | 87.6 % | 2.91 % | 0 | 0 | 11.82 | 4.95 (7.58) | 11.24 | 3.11 | 16.0 (3.3) | 1319 (392) | 44 | P8/P0/P4 | 870/1602 | 180 | 33 | 0 |
| vo-b6 | 22706 | 13.22 | 13.42 | 17.88 | 19.87 | 33.8 | 95.5 % | 11.47 % | 1 | 0 | 13.22 | 5.82 (9.00) | 12.63 | 3.68 | 15.8 (3.3) | 1313 (368) | 44 | P0/P5/P4 | 828/1604 | 180 | 33 | 0 |

**Media de las dos pasadas de cada lado (B − A):** frame medio 11,86 → 13,06 ms (**+1,20 ms**), mediana +1,14, p95 +1,66, **p99 17,97 → 19,71 ms (+1,74)**, frames > 8,33 ms 87,8 → 94,5 %, frames > 16,67 ms 3,1 → 11,0 %, frames > 33 ms 0 → 1 por pasada, > 50 ms 0 → 0. CPU (frame) +1,20 ms, hilo principal 11,28 → 12,49 (+1,21), hilo de render 3,12 → 3,62 (+0,50), GPU 4,98 → 5,66 (+0,68). Potencia de GPU 15,8 / 16,0 W y reloj 1.293 / 1.332 MHz de media, mismos P-state (P0/P4) y 44 °C en ambas: las condiciones fueron equivalentes, no hizo falta repetir por potencia o reloj. El cuello de botella es la **CPU** (frame CPU = frame total; la GPU va al 40-45 %). B dispara un 5 % menos (827 frente a 870 disparos del jugador) porque el cambio de módulo dura 0,62 s en lugar de 2 × 0,2 s: B no tiene más carga de combate que A.

**Resultado (con decales URP): B empeora más de 0,3 ms de media y sube el p99. No cumple tu criterio.** Abajo, qué se optimizó, de dónde sale el coste y qué opciones quedan (§9).

### Optimización hecha (medida)

1. Primera versión B (series 1 y 2): 13,0-13,2 ms frente a 9,5-11,7 ms de A. Perfil de CPU (build Development, oleada 3) B − A por frame: muestreo de reflection probes por objeto +0,30 ms, interpolación de light probes +0,13, envío de draws y cambios de shader (SRPBatcher.Flush +0,41, ApplyShader +0,48 entre principal y render), decales +0,08.
2. Cambios aplicados: reflection probes por sala (6 en el búnker, 16 en el lab, antes 10 y 33); el entorno estático deja de muestrear light probes (solo aporta rebote; los enemigos sí las usan); proyectiles, fogonazos y explosiones sin probes; un único conjunto de keywords para todo el entorno y otro para todos los robots (más tramos agrupados por el SRP Batcher); decales 49 → 12 en el benchmark y 69 → 24 en el lab; polvo 180 → 70 partículas; vapor 3 → 1 rejilla; estela más corta.
3. Ablación en el mismo ejecutable (argumento `--vo-off`, componente `VisualOverhaulAblation`, inactivo sin el argumento; pasadas de 5 oleadas, aceptan otra resolución, pero todas salieron a 1080p exclusivo):

| Pasada | Media | Mediana | p99 | CPU | GPU (p99) | Hilo principal | Hilo de render | GPU W (σ) | Reloj MHz (σ) | Temp 
|---|---|---|---|---|---|---|---|---|---|---
| ab-b-1 | 13.19 | 13.32 | 19.85 | 13.19 | 5.63 (8.23) | 12.61 | 3.71 | 15.7 (3.7) | 1323 (447) | 43 
| ab-b-nodecals | 12.41 | 12.74 | 18.72 | 12.41 | 5.45 (8.62) | 11.84 | 3.52 | 17.2 (5.6) | 1445 (497) | 44 
| ab-b-noprops | 12.99 | 13.24 | 19.08 | 12.99 | 5.55 (8.12) | 12.40 | 3.55 | 16.4 (5.3) | 1349 (484) | 44 
| ab-b-noamb | 12.91 | 13.17 | 18.97 | 12.91 | 5.51 (8.04) | 12.34 | 3.57 | 15.9 (3.7) | 1352 (446) | 44 
| ab-b-norefl | 12.92 | 13.29 | 19.22 | 12.92 | 5.58 (8.55) | 12.34 | 3.68 | 15.8 (3.8) | 1349 (441) | 44 
| ab-b-nolprobes | 12.93 | 13.23 | 19.02 | 12.93 | 5.51 (8.14) | 12.36 | 3.63 | 15.9 (4.1) | 1340 (457) | 44 
| ab-b-nostates | 13.26 | 13.49 | 19.77 | 13.26 | 5.59 (8.51) | 12.67 | 3.65 | 16.2 (4.1) | 1344 (457) | 44 
| ab-b-notrails | 12.92 | 13.22 | 18.99 | 12.92 | 5.53 (8.14) | 12.35 | 3.62 | 15.9 (4.3) | 1352 (455) | 44 
| c-b-probes | 12.60 | 12.83 | 18.65 | 12.61 | 5.45 (8.53) | 12.04 | 3.55 | 16.1 (4.2) | 1391 (473) | 44 
| c-b-probesdecals | 12.46 | 12.67 | 18.15 | 12.47 | 5.33 (8.05) | 11.88 | 3.49 | 18.7 (6.9) | 1550 (584) | 46 
| c-b-noall | 12.21 | 12.59 | 18.22 | 12.21 | 5.02 (7.91) | 11.62 | 3.22 | 16.9 (4.5) | 1463 (491) | 45 
| q-a-1 | 10.98 | 11.30 | 16.82 | 10.98 | 4.57 (7.11) | 10.44 | 2.90 | 16.9 (4.3) | 1382 (429) | 43 
| q-b-1 | 12.80 | 13.15 | 19.14 | 12.80 | 5.62 (9.03) | 12.23 | 3.56 | 16.1 (4.5) | 1355 (517) | 44 
| q-a-2 | 11.31 | 11.46 | 17.37 | 11.31 | 4.78 (7.43) | 10.75 | 3.00 | 16.8 (4.7) | 1401 (512) | 44 
| q-b-2 | 12.79 | 12.95 | 18.66 | 12.79 | 5.45 (8.24) | 12.22 | 3.57 | 16.3 (4.0) | 1389 (458) | 44 
| c-a-1 | 11.74 | 12.16 | 17.27 | 11.74 | 4.94 (7.23) | 11.16 | 3.09 | 16.0 (4.3) | 1303 (501) | 44

Lectura: cada grupo nuevo cuesta poco por separado (decales ≈ 0,4-0,8 ms, el resto 0,2-0,3 ms cada uno: piezas, partículas, reflection probes, light probes, estelas; luces de estado ≈ 0) y quitarlos todos deja B ≈ 12,2 ms frente a ≈ 11,0-11,7 de A en esas mismas pasadas cortas. El resto del coste (≈ 0,5-1 ms) está en lo que no se puede desactivar en caliente: los materiales y efectos de los robots (chispas x2, fogonazos 1,7x, explosión con más partículas) y el entorno en HardSurface. Una pasada aislada de "todo desactivado" dio 9,48 ms, pero no se reprodujo (12,21 ms al repetirla): la descarto como atípica.

### Series anteriores (antes de la optimización, para referencia)

| Pasada | Muestras | Media | Mediana | p95 | p99 | Máx | >8,33 ms | >16,67 ms | >33 ms | >50 ms | CPU | GPU (p99) | Hilo principal | Hilo de render | GPU W (σ) | Reloj MHz (σ) | Temp | P-state | Disparos jug./enem. | Muertes | GC | Errores |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| vo-a1 | 27577 | 10.88 | 11.20 | 15.58 | 17.56 | 2196.8 | 72.4 % | 1.99 % | 12 | 5 | 10.88 | 4.52 (7.36) | 10.38 | 2.83 | 16.0 (5.6) | 1271 (486) | 43 | P8/P0/P4/P5 | 854/1540 | 180 | 33 | 0 |
| vo-a2 | 25957 | 11.56 | 11.86 | 15.70 | 17.66 | 32.7 | 86.1 % | 2.18 % | 0 | 0 | 11.56 | 4.82 (7.33) | 10.99 | 3.02 | 15.5 (4.6) | 1304 (442) | 43 | P8/P0/P4 | 870/1596 | 180 | 33 | 0 |
| vo-b2 | 22667 | 13.24 | 13.59 | 17.86 | 19.67 | 36.9 | 94.0 % | 11.85 % | 1 | 0 | 13.24 | 5.73 (8.53) | 12.67 | 3.79 | 15.7 (3.4) | 1331 (380) | 44 | P0 | 827/1539 | 180 | 30 | 0 |
| vo-a3 | 31620 | 9.49 | 8.66 | 14.70 | 16.64 | 2184.5 | 53.9 % | 0.98 % | 11 | 6 | 9.49 | 3.93 (7.08) | 9.12 | 2.47 | 15.8 (5.0) | 1308 (472) | 44 | P8/P0/P5 | 861/1543 | 180 | 36 | 0 |
| vo-b3 | 22987 | 13.05 | 13.20 | 18.06 | 19.93 | 32.6 | 93.6 % | 13.09 % | 0 | 0 | 13.05 | 5.70 (8.88) | 12.48 | 3.69 | 16.5 (6.1) | 1361 (534) | 45 | P8/P0/P3/P5 | 828/1562 | 180 | 30 | 0 |
| vo-a4 | 25669 | 11.69 | 11.89 | 16.10 | 18.02 | 28.8 | 85.9 % | 3.25 % | 0 | 0 | 11.69 | 4.94 (7.57) | 11.11 | 3.08 | 16.2 (4.5) | 1288 (441) | 44 | P8/P0/P4/P3 | 870/1600 | 180 | 33 | 0 |
| vo-b4 | 23123 | 12.98 | 13.33 | 17.51 | 19.34 | 38.8 | 93.3 % | 9.66 % | 1 | 0 | 12.98 | 5.73 (8.91) | 12.41 | 3.65 | 15.9 (3.6) | 1302 (390) | 44 | P8/P0 | 828/1568 | 180 | 33 | 0 |

Pasadas fallidas por el modo de pantalla (Windows devolvió 1600x900 en lugar de 1080p exclusivo: `vo-b1`, `vo-a-prof`) se descartaron y se repitieron; no son errores del juego. La variación de A entre pasadas (9,5-11,9 ms) es grande en este portátil; por eso se comparan pasadas alternas y varias por lado.

## 7. Pruebas

**Tests propios** `Assets/IronRequiem/Tests/PlayMode/VisualOverhaulTests.cs`: **8/8** en la última pasada completa (repetida el 08/11 tras los decales de malla: 8/8) (dispositivos de entrada virtuales, 60 Hz simulados, sin foco ni control del escritorio):

| Test | Qué comprueba | Resultado |
|---|---|---|
| LabLoadsCleanAndIsDressed | VisualLab carga sin errores en consola; 22 decales de malla y 0 `DecalProjector`, 16 reflection probes, 1.737 light probes horneadas, 2 módulos, 3 enemigos (ninguno quitado) con luces de estado, 3 tubos que parpadean | OK |
| ModuleSwitchAnimatesBlocksFireAndWorksWithAdsAndSlide | Cambio normal 0,650 s, con ADS 0,633 s, durante deslizamiento 0,633 s; 0 disparos con el gatillo pulsado durante el cambio; un desbloqueo y un acople por cambio; ADS activo al terminar; vuelve a disparar | OK |
| ShotgunKeepsFourChargesAndBankReload | 4 celdas visibles, 4 disparos (3-2-1-0), recarga del banco con ventilación, vuelve a 4 | OK |
| EnemyLightsFollowStatesWarnAndGoOutOnDeath | Patrulla → ataque → aviso 0,57 s antes del disparo real → muerto con luces apagadas; colores distintos | OK (8/8 repeticiones tras la corrección) |
| ArmsBefore / ArmsAfter | Brazo cortado en puño y ejecuciones (§4): 0 frames con corte, antes y después | OK |
| PatrolSurvivalMain / PatrolSurvivalLab | Con el NavMesh rehorneado los 3 robots siguen vivos tras 15 s, igual que en MainScene | OK |

Lo que encontraron los tests y se corrigió: (1) el aviso se calculaba con la cadencia de otro `WeaponController` del robot (los T-07 llevan varios); ahora usa `EnemyController.GetCurrentWeapon()`; (2) `EnemyStateLights` accedía al `EnemyController` ya destruido tras la muerte (excepción); ahora lo comprueba; (3) el resto de intermitencias venían del propio test (jugador colocado sobre el foso, torreta activa debajo) y están corregidas.

**Suite completa del proyecto** después de la cuarentena y de todo el trabajo, comparada con la pasada inicial (antes de tocar nada):

- EditMode: 14 de 17, **los mismos 3 fallos que antes** (MechanicsAssetTests.NewActionsAreUnique…, T07DestructionAssetTests.Variant…, T07OptimizationAssetTests.Variant…).
- PlayMode, por clases (42 clases): **los mismos fallos que en la pasada inicial**: BigBotDestruction ×2, BunkerPolish.ShotgunReloadsBankOfFourWithVent, CombatFollowup.ExecutionProtects…, CombatPolish.MeleeContactAngles, CombatTwoRooms ×2, FastCombat.T07Threshold…, MechanicsBehavior.SlideContactParticles…, PrimaryModule.IntegratedMount, RobotImpactFeedback ×3 y SawBot.BunkerWaveContainsOneSawBot…. Ninguno usa escenas o prefabs de este trabajo.
- CombatPolish.**TurnTransitions** falló en la suite y en 1 de 2 repeticiones (salto de pie del Big Bot de 0,115 m frente al límite de 0,11 m; mide con tiempo real del Editor). Pasaba en la pasada inicial. Usa el Big Bot original, no las variantes. Lo considero intermitente, pero no puedo demostrar que no tenga relación.
- **TwoFloorGapTests se cuelga** (espera mientras el `timeScale` está a 0): ya se colgaba en la pasada inicial, y también cuando se ejecuta sola. El vigilante del runner lo corta a los 6 min.
- Pasada inicial: `Tools/IronRequiem/Reports/VisualOverhaul/baseline-playmode-results.txt`. Pasada final: `tests-fp-*-progress.txt` y `.xml`.

**Builds**: ejecutables Release A y B (D3D11, 0 errores) y **build completo del juego con las 19 escenas de Build Settings tras la cuarentena: correcto, 0 errores** (`Tools/IronRequiem/Builds/VisualOverhaul/quarantine-check`).

## 8. Archivos protegidos y cambios fuera de las variantes

- `Assets/FPS/Scenes/MainScene.unity`, `Packages/manifest.json`, `Packages/packages-lock.json` y los 24 `ProjectSettings/*.asset`: **el mismo SHA-256 que al empezar** (27 de 27, comprobado al final). Ver la nota sobre el build del juego más abajo.
- BunkerLab y BunkerBenchmark: mismo hash (solo se copiaron).
- Cambios en archivos existentes (con respaldo):
  - `Assets/FPS/Scripts/Gameplay/Managers/PlayerWeaponsManager.cs`: hook opcional del cambio de arma (sin el componente, el comportamiento es el mismo). Nuevo `Assets/FPS/Scripts/Game/Shared/IWeaponSwitchMotion.cs`.
  - `Assets/Rendering/Default_PipelineAsset.asset`: tuvo un segundo renderer con decales URP mientras se usaron; con los decales de malla **se restauró byte a byte desde el original** (`Backups/.../02-rendering`, comprobado) y las cámaras de las variantes vuelven al renderer por defecto. El renderer sobrante `VO_ForwardRenderer.asset` se movió a `Tools/IronRequiem/Quarantine/20261108-mesh-decals/` (fuera de Assets, sin borrar).
- Builds de benchmark: el protocolo existente cambia D3D11, Mono y las estadísticas de frame solo durante el build, y restaura ProjectSettings y los assets de render byte a byte. Hash idéntico tras cada build.
- **Nota**: el build completo del juego (para validar la cuarentena) no usaba ese protocolo, y Unity volcó al disco el estado que tenía en memoria: `preloadedAssets` en ProjectSettings, `UnityConnectSettings.m_Enabled`, campos nuevos de GraphicsSettings y el `active` de MotionBlur/DoF en `DefaultVolumeProfile` (intensidad 0 y modo Off: no se aplica ningún desenfoque). **Restaurados desde la instantánea original** y comprobados por hash. Si el Editor vuelve a guardarlos más tarde, la instantánea está en `Tools/IronRequiem/Backups/20261107-visual-overhaul/build-vo-a-release/`.
- El paquete de tutoriales (IET) reserializa al entrar en Play `Assets/FPS/Tutorials/2_AddARoom/13-Spawn-Room.asset` y `4_AddEnemyBot/015.asset`. Ya ocurría antes con cualquier test de Play Mode; no lo cambié.
- Build Settings sin cambios: las escenas nuevas no se añadieron.

## 9. Pendiente

1. **Rendimiento (decisión tuya)**: hecho el cambio a decales de malla; B queda **+1,03 ms de media y +1,49 ms de p99**. Opciones que quedan, de más a menos impacto en el aspecto:
   - Quitar reflection probes y light probes a los robots (solo ambiente + luces): ~0,5 ms.
   - Quitar polvo, vapor y estelas: ~0,5 ms.
   - Aun sin todo eso, en pasadas cortas B quedaba ≈ 0,5-1 ms por encima de A: los efectos de los enemigos (chispas x2, fogonazos 1,7x, explosión) y los materiales HardSurface. Para llegar a +0,3 ms habría que rebajar también esos efectos (p. ej. chispas x1,3 y fogonazos x1,3) o simplificar el shader en los robots.
   Dime qué conservar y repito la serie A/B.
2. **Bloom**: no se añadió ni se tocó; el perfil existente ya lo tenía activo. ¿Lo quieres apagado en la variante?
3. **Aplicar a MainScene**: no hecho; pendiente de tu aprobación.
4. **Borrado definitivo de la cuarentena** (`Tools/IronRequiem/Quarantine/20261107`, 252 assets, 111 MB): pendiente de tu confirmación.
5. Escopeta: el módulo usa la versión de 4 cargas con recarga (la que pediste); BunkerLab usa ahora la de solo calor. Confirma cuál quieres.
6. La alerta ámbar está en el código, pero no se observó en Play Mode (el T-07 pasa de patrulla a ataque al detectar).
7. En el lab, 17 renderers (0,9 %) tienen más de 4 luces candidatas (URP usa las 4 más importantes).
8. No se generaron lightmaps (el rebote solo va en sondas). Si quieres lightmaps para lo estático, hay que revisar las UV2 de las piezas de plantilla y añadir la variante `LIGHTMAP_ON` al shader.
9. Tests intermitentes previos: CombatPolish.TurnTransitions y el cuelgue de TwoFloorGap.
10. `VisualBench_B` contiene `VO_Ablation` (inactivo sin `--vo-off`), solo para medir.
