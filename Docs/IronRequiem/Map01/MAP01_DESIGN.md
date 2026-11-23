# MAPA 1 · BÚNKER ABANDONADO · Especificación maestra

**Estado: F1 cerrada en papel → PARADA. Hace falta la aprobación del usuario para pasar a F2.** 18/11/2026.

Esta especificación la mantiene el coordinador (R0). Aquí van el resumen, los contratos, las decisiones y el estado. El detalle está en el archivo de cada rol, y cada dato tiene un solo sitio: si dos documentos no coinciden, manda el del rol dueño del dato.

| Documento | Dueño | Contenido |
|---|---|---|
| [00_INVENTARIO.md](00_INVENTARIO.md) | R6 + R0 | F0: sistemas, métricas, enemigos, kit, rendimiento y salas actuales, verificado contra el código |
| [01_ESTRUCTURA.md](01_ESTRUCTURA.md) | R1 | Zonas, flujo, plano de mundo sin solapes, tabla y fichas por cuarto, curva de ritmo, arena del jefe, datos F2 |
| [02_DECORACION.md](02_DECORACION.md) | R2 | Lenguaje visual, orientación, guía por zona y por cuarto, 22 props nuevos, rendimiento |
| [03_ENCUENTROS.md](03_ENCUENTROS.md) | R3 | Roster, introducción progresiva, economía de armas, arquetipos, encuentros por cuarto, **3 conceptos de jefe** |
| [04_PUERTAS.md](04_PUERTAS.md) | R4 | Familia de clases Z/A/E/P/F/B, lenguaje de estado, contrato técnico, asignación por conexión |
| [05_ILUMINACION.md](05_ILUMINACION.md) | R5 | Paletas por zona, plan por cuarto, estados, técnica URP e IR-03, presupuesto |

Fuente de requisitos: `Docs/IronRequiem/PROMPTS_CHATS.md` §9.

## 1. El mapa en una página

- **17 cuartos y la arena del jefe = 18 etapas (R01–R18)**, en 12 zonas. Hay 17 esclusas (C01–C17) y un vestíbulo de arranque (S00). El búnker baja de 0 a −37 m en siete niveles; todo el desnivel se gana dentro de los cuartos.
- **Recorrido**: Acceso → Habitáculo → Armería → Comunicaciones → Almacenes → Túneles → Tratamiento de agua → Fundición → Ensamblaje → Reactor → Centro de mando → Bóveda.
- **Duración estimada**: ≈ 52 min.
- **Funciones**:
  - 11 arenas de combate;
  - 1 evento de alarma (R07 Comunicaciones, 4–5 rondas con persianas);
  - 1 persecución con trituradora (R10, 200 m en +Z);
  - 4 respiros (R05, R09, R13, R17);
  - el jefe (R18, Bóveda de 72 × 72 m con galería a +6).
- **Ritmo** en diente de sierra: nunca más de dos cuartos seguidos con tensión ≥ 7, y un respiro cada ≤ 4 combates. Gráfico en [01 §8](01_ESTRUCTURA.md).
- **Enemigos**: 238, más el jefe y 10–13 refuerzos. Por tipo: 100 Carroñeros, 59 T-07, 33 HoverBots, 28 SawBots, 8 T-07 Mech, 8 Big Bots y 2 torretas.
  - Máximo simultáneo del mapa: 8 unidades / 10 cuerpos.
  - Hay 95 fuentes de arma y 61 botiquines. Detalle en [03 §B6](03_ENCUENTROS.md).
- **Introducción progresiva**: Carroñero → T-07 con tutorial de ejecución → HoverBot → SawBot → *respiro* → torreta → alarma → Big Bot y T-07 Mech → *respiro* → persecución → horda → combinaciones → *respiro* → jefe.
- **Puertas**: 59, en 6 clases.
  - 23 Z (compuerta de zona), 11 A (esclusa), 1 F-A (dañada).
  - 20 P (persianas de combate) y 2 P-c (persecución).
  - 1 E opcional (escotilla) y 1 B (jefe).
- **Luz**: todo en tiempo real, con 151 `Light` en el mapa y ≤ 8 visibles por cuarto. Reflection probes horneadas (≤ 2 por cuarto). Sombras solo en spots y ≤ 8 en todo el mapa.
- **Decoración**: el kit Bunker libre más 22 props nuevos hechos con scripts de Blender reproducibles. Sin Tripo y sin compras.

### Estado por cuarto

| ID | Nombre | Zona | Función | Estructura | Decoración | Encuentros | Puertas | Luz | Probado |
|---|---|---|---|---|---|---|---|---|---|
| R01 | Control de acceso | Acceso | Combate (intro) | F1 | F1 | F1 | F1 | F1 | — |
| R02 | Pozo del montacargas | Acceso | Combate vertical | F1 | F1 | F1 | F1 | F1 | — |
| R03 | Dormitorios | Habitáculo | Combate (+HoverBot) | F1 | F1 | F1 | F1 | F1 | — |
| R04 | Comedor | Habitáculo | Combate (+SawBot) | F1 | F1 | F1 | F1 | F1 | — |
| R05 | Enfermería | Habitáculo | Respiro | F1 | F1 | — | F1 | F1 | — |
| R06 | Armería | Armería | Combate (+torreta) | F1 | F1 | F1 | F1 | F1 | — |
| R07 | Comunicaciones | Comunicaciones | Alarma | F1 | F1 | F1 | F1 | F1 | — |
| R08 | Almacenes | Almacenes | Pico 1 (+Big Bot) | F1 | F1 | F1 | F1 | F1 | — |
| R09 | Taquillas | Túneles | Respiro | F1 | F1 | — | F1 | F1 | — |
| R10 | Túnel de la trituradora | Túneles | Persecución | F1 | F1 | F1 | F1 | F1 | — |
| R11 | Decantadores | Tratamiento de agua | Horda | F1 | F1 | F1 | F1 | F1 | — |
| R12 | Colector derrumbado | Tratamiento de agua | Combate con grieta | F1 | F1 | F1 | F1 | F1 | — |
| R13 | Sala de bombas | Tratamiento de agua | Respiro | F1 | F1 | — | F1 | F1 | — |
| R14 | Fundición | Taller | Combinaciones | F1 | F1 | F1 | F1 | F1 | — |
| R15 | Ensamblaje | Ensamblaje | Combinaciones | F1 | F1 | F1 | F1 | F1 | — |
| R16 | Reactor | Reactor | Pico 2 | F1 | F1 | F1 | F1 | F1 | — |
| R17 | Centro de mando | Mando | Respiro | F1 | F1 | — | F1 | F1 | — |
| R18 | Bóveda | Bóveda | Jefe | F1* | F1* | F1* | F1 | F1* | — |

`F1` = diseñado en papel. `*` = la versión final depende del jefe elegido. «Probado» se rellena en F2 con el recorrido manual.

## 2. Restricciones duras (de F0; ningún rol las cambia)

1. **Una etapa = una escena de cuarto**: `Assets/IronRequiem/Scenes/Map01_Bunker/Map01_R<NN>_<Nombre>.unity`, con un `CampaignRoom` por escena. Cada sesión tiene su `CampaignFlow`.
2. **Recorrido lineal entre etapas.** Las esclusas viven en la escena de sesión y no hay vuelta atrás. Los bucles, atajos y caídas existen solo **dentro** de un cuarto.
3. **Plano físico coherente.** No hay teletransporte. Ningún cuarto ni esclusa se solapa con otro: R1 lo comprobó por script (0 solapes y 0 cercanías de menos de 4 m). La extensión es x −17…294, z −70…783 m.
4. **Rondas** con `CombatTwoRoomsLab` en `singleArena` y `variedSpawns = true`. Con 4 o más rondas, `secondRoom` es una copia de la arena. Cada ronda entra sin pausa cuando no queda nadie vivo de la anterior.
5. **Puertas**: manda el contrato de `AirlockDoorMotion`. Abierta = inactiva y sin colisión; al cerrar, colisión desde el primer frame. Nunca atrapa al jugador.
6. **No se cambian sistemas existentes sin aprobación.** Lo nuevo va en componentes nuevos que observan propiedades públicas (§5).
7. **Zona protegida intacta**, y la lista roja solo como copia.
8. **Sin Meshy, sin compras.** Tripo solo con aprobación y el coste dicho antes de cada generación (el plan actual no lo usa).

## 3. Convenciones

- **IDs**: `R01`…`R18` siguen el recorrido. `C<NN>` es la esclusa R<NN> → R<NN+1>. `S00` es el vestíbulo de arranque.
- Metros, Y hacia arriba, **yaw 0 en todos los cuartos** (CC-8). Cada cuarto tiene un origen de mundo propio ([01 §5.2](01_ESTRUCTURA.md)).
- **Capas por cuarto**: `[Structure]`, `[Decor]`, `[Encounters]`, `[Doors]` (puertas internas) y `[Lighting]`. Cada rol solo crea o borra hijos en la suya.
- **Capas por esclusa**, en la sesión: `C<NN>/[Structure]`, `C<NN>/[Doors]` y `C<NN>/[Lighting]`.
- **Datos de F2**: `Assets/IronRequiem/Levels/Map01/Data/<Rol>/R<NN>.json` (y `C<NN>.json`), en coordenadas locales al cuarto. Esquema v0 de cada rol:
  - Estructura: [01 §12](01_ESTRUCTURA.md).
  - Decoración: [02 §D7](02_DECORACION.md).
  - Encuentros: [03 §A8](03_ENCUENTROS.md).
  - Puertas: [04 §P3/§Q2](04_PUERTAS.md).
  - Iluminación: [05 §L8](05_ILUMINACION.md).

  R0 los congela al empezar F2: un archivo de esquema por rol y validadores en cada `*Authoring`.
- **Rótulos pintados**: código de zona + número de cuarto (p. ej. `HAB-03`), sin «SECTOR n». El HUD vuelve a contar desde 1 en cada sesión ([04 [cambio B]](04_PUERTAS.md)).

## 4. Contratos entre roles (v1)

v1 = v0 + los cambios CC-1…CC-15 de [01 §13](01_ESTRUCTURA.md) (CC-13 a CC-15 salieron de la reconciliación final: antecámara de B, IDs de anclaje `R<NN>.A<n>` y la regla de «≤ 8 luces encendidas a la vez») + los tamaños de puerta ajustados por R4 ([04 §P4](04_PUERTAS.md)) + las decisiones de R0 de §6.

**Métricas y reglas de construcción** (detalle en [01 §2](01_ESTRUCTURA.md)):

| Regla | Valor |
|---|---|
| Paso del jugador | ≥ 4 m |
| Pasillo de combate | ≥ 6 m |
| Ruta del Big Bot | ≥ 5 m (anillo ≥ 6 m en sus arenas) |
| Altura libre | ≥ 4,5 m; arenas ≥ 8 m; pasarelas con paso inferior a ≥ +5 m |
| Coberturas | Bajas 1,1–1,2 m · altas ≥ 2,4 m |
| Saltable | ≤ 1,7 m |
| Huecos franqueables | ≤ 7,5 m andando · ≤ 11 m esprintando · ≤ 12 m con dash |
| Carril de slide | ≥ 12 m |
| Rampas / caídas voluntarias | Rampas ≤ 30° · caídas ≤ 8 m |
| Holguras entre obstáculos | 0 o ≥ 4 m (≥ 5 m con Big Bot) |
| Superficies pisables | A ≤ 3 m de NavMesh conectado |
| Slots de spawn | A ≥ 10 m de la entrada |
| Teselas de geometría | ≤ 10 m |

Origen de las métricas: suelo 13 m/s, sprint 19,5 m/s, salto 2,03 m / 0,9 s, dash 4,32 m, slide ≈ 12 m y cápsula 1,8 × 0,35 m ([00 §2](00_INVENTARIO.md)).

**Clases de puerta (Puertas ↔ Estructura).** Detalle en [04 §P1/§P4](04_PUERTAS.md):

| Clase | Vano | Uso | Notas de estructura |
|---|---|---|---|
| Z (zona) | 8 × 6 m | Esclusa entre zonas | Banda de muro de 14 × 7,6 × 1,5 m en la cara de la sala, 6 m libres y techo ≥ 8 m junto a la puerta. Esclusa de 14 × 9 × 15 (AABB) |
| A (esclusa) | 5 × 4,5 m | Esclusa dentro de una zona | Techo ≥ 6 m junto a la puerta; esclusa de 8 × 6,5 × 10 (AABB) |
| E (escotilla) | Ø 3,6 m truncada (2,5 m a ras de suelo) | Solo paso interno (R05) | Barrido de 90° |
| P (persiana) | 4–12 × 4–6 m | Pórtico de cada arena, alas de R07, jaula de R15 | Casete en la cara exterior a la arena |
| F-A (dañada) | Como A | Destino de C11 | Solo visual |
| B (jefe) | 14 × 10 m | Dentro de R18 | Antecámara de 20 × 22 × 14 dentro de la escena del jefe (CC-13) |

En las cámaras de esclusa hay ≥ 1 m entre la hoja y el trigger, y el trigger ocupa toda la cámara.

**Responsabilidades:**

- **Estructura → todos.** Por cuarto: límites, cotas, techos, puertas con su clase, volúmenes libres, grupos de slots, hueco del landmark, anclajes de luz con ID, bounds de la arena y peligros ([01 §7](01_ESTRUCTURA.md)).
- **Encuentros → Estructura.** Arquetipos y requisitos de arena ([03 §A4](03_ENCUENTROS.md)), cumplidos con los matices aceptados en [03 §B2](03_ENCUENTROS.md):
  - plataformas a +6;
  - bounds sin la plataforma de llegada, con 4 condiciones.
- **Iluminación** es dueña de las luminarias. **Decoración** no coloca luces y no usa emisivos rojos, verdes ni ámbar a ≤ 12 m de una puerta. Las luces de estado son de **Puertas**.
- **Decoración** no invade volúmenes libres, NavMesh ni spawns. La única excepción son las `routeMark` (§6, decisión R0-D1).
- **Ninguna persiana corta el camino NavMesh entre los spawns de la ronda y el jugador.** Nunca cierra con el jugador o un enemigo vivo en el lado que se sella. El pórtico baja solo con el jugador ≥ 4 m dentro de los bounds ([03 §B7](03_ENCUENTROS.md), [04 §Q2](04_PUERTAS.md)).

**Presupuesto por cuarto** ([00 §5](00_INVENTARIO.md), provisional hasta medir en F2):

| Concepto | Límite |
|---|---|
| Ronda | ≤ 8 unidades y ≤ 12 cuerpos |
| Luces en tiempo real | ≤ 8 visibles · ≤ 2 con sombra · ≤ 2 transitorias de efectos (3 en R18) · ≤ 4 por objeto |
| Draw calls | ≤ 350 sin combate / ≤ 400 en combate; la decoración ≤ 60 por arena |
| SetPass | ≤ 200 |
| Reflection probes | ≤ 2 |
| Decales | ≤ 24 |
| Partículas | ≤ 500 |

**Criterio IR-04**: media ≤ 8,33 ms, p99 ≤ 11 ms y 0 frames > 33 ms en la ronda más cargada, a 1920×1080 exclusiva.

## 5. Componentes nuevos (solo código nuevo; observan propiedades públicas)

Ninguno modifica un sistema existente. **El único cambio a un sistema existente que se propone es DU-1** (§7).

| Componente | Carpeta | Dueño | Para qué | Cuándo |
|---|---|---|---|---|
| `Map01StageRelease` | Scripts/Levels/Map01 | R3/R1 | Llama a `CampaignRoom.Release()` en los respiros (R05, R09, R13, R17). Sin él, soft-lock | F2 |
| `ArenaShutter` (= `Map01ShutterController`) | Scripts/Doors | R4 | Persianas P: pórticos, alas de R07, jaula de R15. Datos en `Data/Doors` | F2 (lógica greybox) |
| `Map01SessionHandoff` | Scripts/Levels/Map01 | R0 | Corte de sesión: abre la salida de la última etapa y carga la sesión siguiente | F2 |
| `ChaseGateState` | Scripts/Doors | R4 | Estados de las compuertas de la persecución | F2/F3 |
| `DoorPresentation` + rig hermano | Scripts/Doors | R4 | Parte visual de todas las puertas (hojas, cerrojos, luces, sonido) | F3b (requiere DU-1 para el enganche) |
| `HatchDoor` | Scripts/Doors | R4 | Escotilla E opcional de R05 | F3 |
| `Map01BossEncounter` + barra de jefe | Scripts/Levels/Map01 | R3 | Fases, refuerzos y fin de mapa (`Release()`) | F4 (IA completa = otra tarea) |
| `BossGateSequence` | Scripts/Doors | R4 | Puerta B: cierra detrás del jugador | F4 |
| `Map01RoomLighting`, `Map01LampFlicker`, `LuminaireSpin`, `Map01BossLighting` | Scripts/Levels/Map01 | R5 | Estados de luz, parpadeo y balizas | F3/F4 |
| `Map01LightingAblation` | Scripts/Diagnostics | R5 | Solo medición | F2/F5 |

**Scripts de autoría** (Editor, idempotentes, con Undo, cada uno limpia solo su capa): `Assets/IronRequiem/Editor/Map01/StructureAuthoring.cs`, `DecorAuthoring.cs`, `EncounterAuthoring.cs`, `DoorAuthoring.cs` y `LightingAuthoring.cs`. Solo R0 los ejecuta, compila, hornea y guarda.

**Componentes existentes que se reutilizan sin cambios**: `CampaignFlow`, `CampaignAirlock`, `CampaignRoom`, `CombatTwoRoomsLab`, `EnemySpawnQueue`, `CorridorAlarm` (R07), `ChaseCorridorEncounter` (R10), `GapFallRecovery` (R10, R12), `WeaponMountStation` (vestíbulo de cada sesión), `ZoneEnvironment` y `VolumeFadeIn`.

## 6. Decisiones que ha tomado el coordinador en F1

| # | Decisión | Motivo |
|---|---|---|
| R0-D1 | **Se acepta la excepción `routeMark`** (D-D1 de R2): flechas y líneas guía planas de ≤ 1 cm, sin collider ni efecto en el NavMesh, dentro de los volúmenes libres de ruta y de puerta. **Nunca** en volúmenes de spawn ni en el umbral de la puerta (1 m) | Es la ayuda de orientación más barata y no afecta a juego ni colisiones |
| R0-D2 | `ArenaShutter` y `Map01ShutterController` son **un solo componente**, de Puertas, en `Scripts/Doors/`. R3 aporta `closeOnWave`/`openOnWave` en `Data/Doors` | Un dueño por archivo |
| R0-D3 | Las luces de estado de puerta van **fuera** de las 8 de Iluminación: ≤ 2 visibles por cuarto, alcance ≤ 5 m, apagadas en reposo (D-L6) | Reparto del presupuesto entre roles |
| R0-D4 | R18 puede tener **3 luces transitorias** de efectos, pendiente de medición | Fases del jefe |
| R0-D5 | Contratos v1 (§4) | Integración de los roles |
| R0-D6 | Recomendación de **cuatro sesiones** (S1 R01–R05, S2 R06–R13, S3 R14–R17, S4 R18) en lugar de tres. **C10 queda como corte opcional** | Cada intento contra el jefe ya no repite R14–R17, ≈ 12,5 min (R3 B6.3) |
| R0-D7 | Las puertas tienen **lógica greybox desde F2**; F3b solo sustituye la parte visual | Permite probar el contrato antes del arte (R4 R0-4) |
| D1 (F1) | Encuentros diseñados con la obtención de armas actual (solo ejecución); la alternativa se pregunta al usuario | Cambiarla es cambiar el balance |

## 7. Decisiones que esperan al usuario (PARADA F1)

Si no se indica otra cosa, **en F2 se aplica la recomendación**.

### Necesarias para empezar F2

| # | Decisión | Recomendación | Alternativas | Fuente |
|---|---|---|---|---|
| **U1** | **Jefe** | **A «El Custodio»**: un Big Bot ×1,6 cuyos cañones se arrancan rompiendo sus uniones. Coste medio. Plan B: C | B «La Matriarca»: fundidora que fabrica Carroñeros; coste alto de arte. C «El Panóptico»: columna de tres anillos con vainas de cañón; coste medio y sin riesgo de NavMesh | [03 §A7](03_ENCUENTROS.md) |
| **U2** | **Muerte y checkpoints** | **Cuatro sesiones encadenadas** (S1 R01–R05 · S2 R06–R13 · S3 R14–R17 · S4 R18) con `Map01SessionHandoff`. La muerte reinicia solo la sesión en curso, con vida llena y la estación de montaje. No toca sistemas existentes | (b) Checkpoint en cada compuerta Z: toca `CampaignFlow`/`CombatLabMenu`. (c) Sin cambios: cada muerte, desde R01 (> 50 min). (d) Añadir el corte opcional en C10 | [01 D-U4](01_ESTRUCTURA.md), [03 D-E2/B6.3](03_ENCUENTROS.md) |
| **U3** | **Cómo se consiguen las armas** | **Híbrido**: ejecución (< 75 %) en todo el mapa, como hoy, más desprendimiento por unión **solo** en el T-07 Mech y en el jefe | (a) Solo ejecución, como hoy. (b) Volver al pilar original (destruir la unión desprende el arma) en todos: duplica la economía | [03 D-E1](03_ENCUENTROS.md) |
| **U4** | **Número de cuartos** | **17 + jefe** | 15 + jefe, recortando R09 y R13 (sigue el orden de [01 D-U3](01_ESTRUCTURA.md)) | [01 D-U3](01_ESTRUCTURA.md) |
| **U5** | **Hangar** | Map01 empieza en S00/R01. El hangar queda reservado sin tocar, con S00 donde termina su esclusa, para anteponerlo después | El hangar como etapa 0 ya en F2, con una copia de la esclusa a0 | [01 D-U1](01_ESTRUCTURA.md) |
| **U6** | **Salas actuales** | No copiar escenas: R10, R12, R14, R15 y R16 **derivan por datos** de Pursuit05, Room02, Room01, Room03 y Room04, con los defectos M7–M19 corregidos. Room00b y Room02 se reemplazan | Copias de escena desplazadas | [01 §10](01_ESTRUCTURA.md) |
| **U7** | **Build Settings** | **F2 necesita añadir las escenas de Map01** (sesiones y cuartos) **al final** de Build Settings, sin tocar las existentes. `CampaignFlow` solo carga escenas que estén ahí (`CanStreamedLevelBeLoaded`). IntroMenu no se cambia: Map01 se prueba abriendo su sesión en el Editor | No añadirlas: el greybox no se puede jugar de punta a punta | [00 §1](00_INVENTARIO.md) |
| **U8** | Lista de **componentes nuevos** de §5 | Aprobarla | — | §5 |
| **U9** | **Persiana P de entrada** en las arenas, para que el jugador no se refugie en la esclusa | **Sí** | No: el pórtico queda abierto | [01 D-U5](01_ESTRUCTURA.md), [04 DU-6](04_PUERTAS.md) |

### Necesarias antes de F3 (F2 puede avanzar sin ellas)

| # | Decisión | Recomendación | Fuente |
|---|---|---|---|
| U10 | **Enganche `IDoorPresentation` en `AirlockDoorMotion`** (≈ 30 líneas): el root no se mueve, el `k` es el mismo y el contrato no cambia. Es el único cambio en un sistema existente | Aprobar. F2 va sin él, con puertas greybox | [04 DU-1](04_PUERTAS.md) |
| U11 | Carroñero **no ejecutable** (variante Map01 con `executionEnabled = 0`), después de verificarlo en el Editor | Sí | [03 D-E3](03_ENCUENTROS.md) |
| U12 | Big Bot en Map01: rampas de refugio con el área Jump y una variante que la excluye. Sin cambios en ProjectSettings | Sí | [01 D-U6](01_ESTRUCTURA.md), [03 D-E6](03_ENCUENTROS.md) |
| U13 | Luz: todo en tiempo real (D-L1); sombras según medición (D-L2); 4 apagones (D-L3); grano de 0,3 a 0,18 en los Volumes de zona (D-L4, IR-03); sesión sin `SectorLightingBlend` ni `LabSun` (D-L5) | Sí a todas | [05 L0](05_ILUMINACION.md) |
| U14 | Puertas: 0,7 s / 0,45 s sin alargar (DU-2); ABIERTA en verde (DU-4); rótulos en atlas emisivo (DU-5); excepción de 2,5 m para E (DU-7); F-2 solo en rutas opcionales (DU-8) | Sí a todas | [04 P0](04_PUERTAS.md) |
| U15 | Decoración: restos de robot propios, sin los Scrap del hangar (D-D2); sin Tripo (D-D3); HardSurface en copias propias (D-D4); narrativa propuesta (D-D5) | Sí a todas | [02 D0](02_DECORACION.md) |
| U16 | Pausa entre rondas: 0 s, como hoy. Se revisa tras jugar F2 | Sí | [03 D-E5](03_ENCUENTROS.md) |

## 8. Plan de F2 (tras la aprobación)

1. **Preparación**:
   - Respaldo en `Tools/IronRequiem/Backups/<fecha>-map01-f2/`.
   - Hashes de la zona protegida con `IronCampaignProtectedZone`.
   - Comprobar que ninguna otra sesión usa el Editor; es uno solo para todos.
2. **Esquemas de datos y validadores**: congelar el esquema v0 de cada rol.
3. **Subagentes** (cada uno con archivos exclusivos):
   - R1 (opus): `StructureAuthoring.cs` y los datos de estructura.
   - R3 (opus): `EncounterAuthoring.cs` y los datos de encuentros.
   - R4 (sonnet): lógica greybox de puertas, `ArenaShutter`, `Map01SessionHandoff` y `ChaseGateState`.
   - `Map01StageRelease`: R3.
4. **R0 en el Editor**: compilar, ejecutar la autoría y crear las escenas de sesión y cuarto (nunca guardar sobre las existentes); hornear un NavMesh por cuarto; asignar el jefe provisional por rondas en R18; ejecutar los tests de F2.
5. **Tests de F2**:
   - camino de cada slot al jugador con las persianas de la ronda cerradas;
   - las persianas no cierran con alguien en el volumen sellado;
   - Abort y corte de sesión;
   - recorrido completo sin soft-locks.
6. **Recorrido manual** con teclado y ratón, capturas por cuarto y tiempos.
7. **Medición en Release** de R07, R11, R16 y R18.
8. **Revisor independiente** (opus): recibe el código, los datos y las capturas, sin las conclusiones de R0.
9. → **PARADA F2**: lo juega el usuario.

## 9. Pendientes conocidos (fuera de alcance de F1)

- Verificar en el Editor:
  - si el Carroñero es ejecutable;
  - detección, ataque y daño del T-07 Mech;
  - si la torreta es fija y si sus restos conservan el collider;
  - el escalón de NavMesh (1,0 en ProjectSettings frente a 0,4 en Room01).
- No hay evento de fin de campaña: el fin de mapa irá en `Map01BossEncounter` (F4).
- La IA completa del jefe es otra tarea: **IR-11** en la checklist.
- Precalentar el FX de explosión del Big Bot en R08 y R14, que es el primer Big Bot de su sesión.
- Ninguna cifra de rendimiento de Map01 está medida: todo el presupuesto se valida en F2.
