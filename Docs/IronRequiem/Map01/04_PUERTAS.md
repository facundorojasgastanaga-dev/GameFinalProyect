# MAPA 1 · 04 PUERTAS (R4, conceptos) · Plan F1

18/11/2026. Fase F1, plan en papel. Rol R4 (opus). Este archivo es lo único que se ha escrito. No se abrieron Unity ni Blender, no hay renders ni modelos, no se tocó `Concepts/Doors/Redesign_v01/` ni ningún código. Todo dato que no sale de una fuente del proyecto lleva la marca **[SUPUESTO]**.

**Fuentes y abreviaturas.**

| Abreviatura | Fuente |
|---|---|
| [AIR] | `Assets/IronRequiem/Scripts/VisualPass/AirlockDoorMotion.cs` |
| [CF] | `Scripts/Levels/CampaignFlow.cs` |
| [CA] | `Scripts/Levels/CampaignAirlock.cs` |
| [CY] | `Scripts/Levels/AirlockCycle.cs` |
| [CT] | `Scripts/Levels/CombatTwoRoomsLab.cs` |
| [CAL] | `Scripts/Levels/CorridorAlarm.cs` |
| [DCI] | `Scripts/Player/DistanceCameraImpulse.cs` |
| [MD] | `Docs/IronRequiem/Map01/MAP01_DESIGN.md` |
| [INV] | `Docs/IronRequiem/Map01/00_INVENTARIO.md` |
| [DM] | `Tools/IronRequiem/Reports/MapFxAudit2/doors.md` |
| [DC] | `Docs/IronRequiem/Concepts/Doors/Redesign_v01/DOORS_CONCEPTS.md` |
| [PZ] | `Tools/IronRequiem/Reports/ProtectedZone/before-20261117.txt` y `guardian.md` |
| [MIX] | `Assets/FPS/Audio/MainAudioMixer.mixer` |
| [WAV] | Duración leída de la cabecera de cada `.wav` (18/11, solo lectura) |

---

## PARTE A

### P0. Resumen y decisiones para el usuario

1. Las seis clases comparten una arquitectura. El **root** de la puerta, que es el GameObject que referencian `CampaignAirlock` o un controlador nuevo, solo cumple el contrato: un BoxCollider que cubre el vano, un medidor invisible y, en las puertas interiores, un NavMeshObstacle. **Toda la parte visual** (hojas, marco, cerrojos, luces, sonido) está en un **rig hermano siempre activo**, que lleva `DoorPresentation`.
2. Direcciones por clase:
   - Z: D-A Mandíbula, refinada. En F3a se propone además una alternativa nueva de cuñas diagonales.
   - A: D-B V1.
   - P: D-B V3.
   - F: variantes dañadas de A.
   - E y B: direcciones nuevas. E toma de D-C la bisagra, el brazo y los cerrojos. B combina los dientes de D-A, la junta diagonal de D-C y un sello central.
   - D-C completa sigue descartada para puertas de combate.
3. Tamaños del contrato v0: se confirman Z, A, F y B. Cambian E (Ø 3,6 m truncada y barrido de 4 m), P (fondo de casete de 1,2 m si H > 5 m, más 0,6 m en los laterales) y la antecámara de B (14 m de alto y dentro de la escena del jefe).
4. Tiempos: se mantienen 0,7 s / 0,45 s [AIR:12]. La sensación de peso sale de tres recursos, sin alargar el contrato:
   - un pre-roll antes de `Set`, con la colisión todavía activa;
   - la fase ≥ 1 de `Prepare`, que garantiza al menos 1,5 s [CF:224,256], para las puertas de destino;
   - una «cola» visual cuando la hoja ya ha despejado el vano.
5. El estado se lee sin color. La referencia es mecánica: cerrojos dentro = bloqueada. A eso se suman iconos, el ritmo de las tiras y las balizas. Los destellos se limitan a ≤ 3 Hz.
6. Anti-atrapamiento:
   - En las esclusas, el hueco entre la hoja y el trigger es ≥ 1,0 m y el trigger ocupa toda la cámara.
   - Las puertas interiores avisan ≥ 0,8 s antes de cerrar y comprueban el solape.
   - NavMesh: las esclusas no tienen NavMesh; las puertas interiores llevan un NavMeshObstacle con carve.
7. Todo el código nuevo va en `Scripts/Doors/` y solo observa propiedades públicas. El único cambio en un sistema existente es el enganche opcional en `AirlockDoorMotion`, que requiere aprobación.
8. Riesgo de diseño principal: si una persiana P separa los spawns del jugador, la ronda queda «EN ESPERA» para siempre [CT:115,123].

| # | [DECISIÓN USUARIO] | Recomendación de R4 | Si se rechaza |
|---|---|---|---|
| DU-1 | Enganche `IDoorPresentation` en `AirlockDoorMotion.Run`: unas 30 líneas, el root no se mueve, el `k` es el mismo y el contrato no cambia. Sin una presentación registrada, todo se comporta como hoy (§P3.3) | **Aprobar** | Plan B (§P3.4). El root invisible sigue subiendo como hoy. La colisión al cerrar solo es nominal, igual que ahora, y el carve llega tarde. Todas las clases siguen siendo posibles |
| DU-2 | ¿Alargar la apertura más allá de 0,7 s? | **No, de forma global.** Como opción dentro de DU-1, solo para Z: hasta 1,1 s al abrir y 0,6 s al cerrar, si tras F3a la Z a 0,7 s no transmite peso | Z con fases comprimidas (deslizamiento de 0,49 s) |
| DU-3 | Esclusa a0 del hangar y todo lo que encuadra la cinemática | **Fuera del rediseño.** Si Map01 empieza en el hangar, C00 replica a0 con copias de material y se verifica `Shot_C_Exit` en Play antes de cambiar nada que se vea desde el hangar ([DM] §5) | — |
| DU-4 | Color del estado ABIERTA: verde o color de la zona | **Verde** en marco y tiras. El color de la zona solo va en la banda pintada de sector | Color de zona: hay que comprobar que no choque con el rojo y el ámbar |
| DU-5 | Rótulos de estado en atlas emisivo o en TMP | **Atlas emisivo**: sin material extra ni glifos dinámicos ([DC] §2) | TMP, que suma glifos a `WarmFonts` [CF:73-85] |
| DU-6 | Persiana P de entrada en cada arena, que baja con la ronda 1 | **Sí, en las arenas que marque R3.** Sin ella, el jugador puede retirarse a la cámara de esclusa, que no tiene NavMesh y donde los enemigos no entran ([DM] §2 NavMesh) | Se acepta la retirada |
| DU-7 | Excepción al ancho mínimo de 4 m [MD §5.1] para E (2,5 m a ras de suelo) | **Aprobar solo para pasos secundarios de ≤ 1 m de largo** (el espesor del marco) | E-2 rodante con vano rectangular, o E de Ø 4,8 m (4 m a ras de suelo) |
| DU-8 | F-2 (destruir el mecanismo) en la ruta crítica | **No: solo en rutas opcionales** | Ruta crítica con rotura automática a los 20 s de proximidad [SUPUESTO] |

---

### P1. Catálogo de clases Z / A / E / P / F / B

#### P1.0 Kit común (vale para las seis clases)

**Root (contrato).** Nombre `<ID>_Door`, con `<ID>` = `C<NN>_Source`, `C<NN>_Destination` o `R<NN>_<Clase><n>`.

- Se evitan los nombres exactos `SourceDoor`/`DestinationDoor`. `VisualPassF2` D1 y `LevelAudit.cs:141` regeneran umbrales por nombre ([DM] §5).
- Lleva un único **BoxCollider sólido** que cubre el vano por el espesor del paquete de hojas. Capa Default, como hoy ([DM] §2), sin Rigidbody y sin scripts.
- Lleva un **medidor**: MeshRenderer de una caja de 12 triángulos del tamaño del vano, con `forceRenderingOff` y sin sombras. Sirve para que `Height()` [AIR:40-44] siga siendo exacto en el Plan B y en el modo de respaldo. [SUPUESTO: verificar en F2 que `Renderer.bounds` es válido con `forceRenderingOff`.]
- En puertas interiores lleva además un NavMeshObstacle (§P3.6).
- Se crea en su pose cerrada. Las puertas interiores que empiezan abiertas se crean **inactivas** y en la pose cerrada. Así la primera llamada a `Set` captura bien `closedWorld` [AIR:20].

**Rig.** `<ID>_Rig`, hermano del root dentro de la capa `[Doors]`, siempre activo. Contiene:

- `Frame_Static`: marco, casetes, jambas y dintel. Con flags estáticos y colliders de caja simples.
- Piezas móviles: hojas, cerrojos, levas, rueda, balizas y pistones. **Ninguna lleva collider.**
- `Status`: renderers de tiras, labio e icono.
- Una `Light` Point sin sombras, apagada en reposo.
- Un `AudioSource` 3D.
- Sistemas de partículas en sockets.
- `DoorPresentation` y el controlador de la clase.

**Por qué las hojas van en el rig y no en el root.** Es un cambio respecto a [DC] §1, donde las hojas son hijas del root. Las clases que baten (E, B) o que se quedan a la vista (F) necesitarían un «proxy de hojas aparcadas», como D-C. Con todo en el rig no hace falta proxy en ninguna clase. Cuando la puerta está abierta y las hojas están dentro del casete, sus renderers se desactivan.

**Materiales de toda la familia** (≈ 10 nuevos; B suma 1):

| Material | Cantidad | Detalle |
|---|---|---|
| Atlas de puerta | 1 | HardSurface [INV A4.1], 2048². Incluye chapa pintada, franjas de peligro, acero desnudo con `_EDGEWEAR`, glifos de plantilla e iconos de estado |
| Presets de estado | 6 | Apagado, Rojo, RojoLatente, Ámbar, ÁmbarParpadeo y Verde, más un Blanco para la alarma. Usan `_PANELS` con `_EmissionColor`/`_PulseSpeed` [INV A4.1]. Se intercambian con `sharedMaterial` y no con MaterialPropertyBlock, para no sacar los renderers del SRP Batcher (iron-requiem-performance) |
| Conos de baliza | 1 | Aditivo |
| Vapor y chispas | 2 | **Copias** de `HG_FxSmoke`/`HG_FxSpark`, que están en la lista roja [PZ §1b] |

**Paleta y forma** (create-game-assets):

- Chapa pintada oscura, acero desnudo en las aristas y franjas de peligro solo en los bordes que se mueven.
- **Rojo, ámbar y verde emisivos se reservan al estado.** Ninguna otra parte de la puerta lleva emisivo de esos colores.
- Lenguaje de forma por clase: Z, dientes; A, bandas gruesas horizontales; P, líneas finas; E, círculo y radios; F, torsión y rotura; B, diagonal más círculo.

**LOD** (criterio de [DC] §6): LOD0 hasta 25 m, LOD1 al 40 % hasta 60 m, LOD2 como cajas.

**Reposo.** Las puertas lejanas (más de una etapa de distancia) pasan a **DORMIDA**: sin `Update` y sin `Light`.

#### P1.1 Tamaños: contrato v0 y propuesta de R4

| Clase | v0 [MD §5.2] | Propuesta de R4 | Motivo |
|---|---|---|---|
| **Z** | Vano 8 × 6; marco 1,5; reserva de 1,6 arriba y 3,0 a cada lado; cámara 12 × 10 × 8 | **Se confirma.** Se añade lo siguiente: los casetes van dentro del muro, en la **cara de la sala**, lo que exige una banda de muro de 14 × 7,6 × 1,5 m. El volumen libre del lado de la sala pasa a 6 m de fondo. El techo de la sala, en esa franja, debe ser ≥ 8,0 m | Los 3,0 m salen de W/4 + diente + 0,45 = 2,85 m ([DC] §3). Dentro de la cámara solo quedan (10 − 8)/2 = 1 m por lado, así que el bolsillo no cabe allí |
| **A** | 5 × 4,5; 1,0; 1,3 arriba y 1,0 a cada lado; cámara 8 × 7 × 5,5 | **Se confirma.** El casete va en la **cara de la sala**: el techo de la sala, a ≤ 4 m de la puerta, debe ser ≥ 6,0 m (4,5 + 1,3 + 0,2). Dentro de la cámara no sube nada, así que la cámara podría bajar a 5,0 m; se mantienen 5,5 | El casete de D-B mide una lama más 0,5 m: 0,8 + 0,5 = 1,3 m ([DC] §4) |
| **E** | Ø 3,2; marco 0,8; barrido de 3,5 m; como esclusa, 8 × 5 × 5 | **Ø 3,6 truncada** a ras de suelo, con el centro a 1,3 m: ancho a ras de 2,5 m y alto de 3,1 m. Marco de 0,8 m. **Barrido de 4,0 m** y cuna de aparcamiento de 4,0 m. **No se usa como esclusa**. **[cambio B]** En Map01 bate 90°, sin cuna (§P1.4) | Con Ø 3,2 y suelo plano quedan ≤ 2,3 m a ras de suelo y 2,7 m de alto, demasiado justo a 13–19,5 m/s [INV §2]. El radio de batido es Ø + 0,3 de desplazamiento de la bisagra |
| **P** | 4–12 × 4–6; 0,6; casete de 1,2 de alto y 1,0 de fondo | **Se confirma**, con **fondo de 1,2 m si H está entre 5 y 6 m**, **0,6 m laterales** y el casete en la **cara exterior a la arena** | El fondo es n · grosor + 0,2. Con H = 6 m hacen falta n = ⌈6 / 0,45⌉ = 14 lamas: 14 × 0,07 + 0,2 = 1,18 m |
| **F** | Como A | **Se confirma.** F-2 suma 0,8 m en la jamba del mecanismo. F-1 tiene un hueco fijo de 1,3 m de alto | Cápsula agachada de 0,9 m [INV §2] |
| **B** | 14 × 10; 3; 4 arriba y 4 a cada lado; antecámara 20 × 16 × 12 | **Se confirma el vano.** La **antecámara pasa a 20 × 16 × 14 m** y forma parte de la **escena del jefe**, no de la sesión | 10 + 4 m de corona = 14 m de muro, que con 12 m de techo no se vería. Si R1 no puede, la corona visible baja a 2 m |

#### P1.2 Z · Compuerta de zona (blast door)

**Función.** Conecta dos zonas. Es la `CampaignAirlock` C<NN> cuando R<NN> y R<NN+1> pertenecen a zonas distintas. La puerta de origen y la de destino son Z, y la cámara de 12 × 10 × 8 m usa `AirlockCycle` [CY], sin cambios, para el ciclo. Es el hito visual del cambio de zona.

**Dirección.**

- **Z-1 «Mandíbula pesada»** reutiliza D-A ([DC] §3): junta dentada de 4 dientes (con H ≥ 6 m), cubo central partido de Ø 1,6 m [SUPUESTO] y 8 dogs (4 por lado; D-A tenía 6). Añade **2 pistones hidráulicos por lado**, visibles en la cara de la sala, que empujan los segmentos exteriores.
- **Z-2 «Cuñas diagonales»** es nueva y se valorará en F3a. Combina la junta diagonal de D-C (inclinación de ±7 % de W) con el recorrido lateral de D-A. El recorrido sube a W/2 + desfase ≈ 4,6 m, pero cabe en 3,0 m de casete con 2 segmentos.

**Geometría.**

- Vano de 8 × 6 m.
- Cada hoja tiene un segmento interior dentado y uno exterior, de 0,35 m cada uno y escalonados 0,25 m, lo que da un paquete de ≈ 0,95 m [SUPUESTO]. El marco mide 1,5 m.
- Casetes laterales de 3,0 m, dentro del muro, en la cara de la sala.
- 1,6 m sobre el dintel para las balizas, el rótulo y el colector de los pistones.

**Secuencia** (contrato de 0,7 s / 0,45 s; `k` = t / duración, como en `Run` [AIR:53,59]):

| Fase | t (s) | k | Movimiento (pivote) | Audio candidato [WAV] | Efectos |
|---|---|---|---|---|---|
| Z1 Cerrojos | 0,00–0,14 | 0–0,20 | 8 dogs se trasladan ±X 0,65 m (centro del dog). Los medios cubos giran 30° sobre la normal de la puerta (centro del cubo) | `Audio/SFX/V4/IR_Mount_Lock.wav` (0,80 s), ×2 con 30 ms de desfase | Chispas en los dogs |
| Z2 Sello | 0,14–0,21 | 0,20–0,30 | Las hojas se separan 8 cm en ±X | `Audio/SFX/HangarIntro/IR_Hangar_Hydraulic.wav` (1,60 s, fundido a los 0,6 s) | Chorro de vapor en la junta |
| Z3 Deslizamiento | 0,21–0,70 | 0,30–1 | Segmento interior: 4,65 m. Exterior: 2,25 m. Vástagos de pistón (pivote en el canto exterior inferior de cada segmento). Curva de aceleración tardía: el gálibo del jugador (0,8 × 1,9 m) no se despeja antes de k ≈ 0,6 | `HangarIntro/IR_Hangar_Servo.wav` (1,00 s, pitch 0,75) + `HangarIntro/IR_Hangar_CannonArm.wav` (1,20 s) | — |
| Fin | 0,70 | 1 | El root se desactiva; las hojas ya están en el casete y sus renderers se apagan | `HangarIntro/IR_Airlock_Done.wav` (1,00 s), **solo si la esclusa no tiene AirlockCycle**, porque el ciclo ya lo reproduce [CY:43] | Tiras en verde |
| Cierre C1 | 0,00–0,33 | 0–0,73 | Deslizamiento inverso en k² | `IR_Hangar_Servo.wav` (pitch 0,85) | — |
| Cierre C2 | 0,33–0,37 | — | Los dientes encajan | `Audio/SFX/V4/IR_Fist_HeavyImpact.wav` (0,90 s) | `DistanceCameraImpulse.Emit(pos, 18, 0,35, 0,3)` [DCI:31] + bocanada de vapor |
| Cierre C3 | 0,37–0,45 | — | Los dogs entran, con un rebote corto | `Audio/SFX/V4/IR_Mount_Clamp.wav` (0,50 s) | — |

- **Pre-roll en la puerta de destino.** Empieza en cuanto `flow.Busy && flow.Phase >= 1` [CF:176] y dispone de ≥ 1,5 s antes de `Set(open)` [CF:224,256]: balizas ámbar girando, sirena `HangarIntro/IR_Corridor_Siren.wav` (2,00 s, en bucle) y el icono CICLO. **Los dogs siguen dentro**, porque mientras esté cerrada la mecánica debe decir que está bloqueada.
- **Con DU-2** (opcional): apertura de 1,10 s (Z1 0–0,25, Z2 0,25–0,38, Z3 0,38–1,10) y cierre de 0,60 s (C1 0–0,45, C2 0,45–0,50, C3 0,50–0,60).

**Piezas y pivotes.** `Leaf_L_Inner`, `Leaf_L_Outer`, `Leaf_R_Inner` y `Leaf_R_Outer` (traslación X). `Hub_L` y `Hub_R` (rotación sobre la normal). `Dogs_L` y `Dogs_R` (4 dogs fusionados por lado, traslación X). `Ram_L1`, `L2`, `R1` y `R2` (traslación del vástago). `Beacon_L` y `Beacon_R` (rotación Y).

Sockets: `socket_steam_joint_top/bot`, `socket_spark_dogs_L/R`, `socket_light_room` y `socket_audio`.

**Colliders.** Root con una Box de 8 × 6 × 0,95. Los casetes y pistones que sobresalgan llevan Box estáticos en `Frame_Static`. No hay NavMeshObstacle, porque la sesión no tiene NavMesh ([DM] §2).

**Luces de estado.** Por cada cara de sala: 2 balizas aditivas sin `Light`, 1 Point sin sombras de rango 8 m ([DC] §3) que solo se enciende en transitorios, y tiras en jambas, dintel y labio de los dientes. En la cámara, `AirlockCycle` con 2 lámparas como en a0 (intensidad 2,5 y rango 7, [DM] §2).

**Presupuesto.**

- Hojas: LOD0 de 2.500–3.500 triángulos, LOD1 al ≈ 40 %, LOD2 ≤ 150.
- Rig: 2.000–3.000. Para comparar, los bocetos de D-A daban 1.432–1.888 en el root y 1.759–1.999 en el marco ([DC] §6).
- ≤ 18 draw calls por puerta y 1 `Light`.

**Coste relativo:** 2,0 (A = 1).

#### P1.3 A · Esclusa entre cuartos (airlock)

**Función.** Conecta cuartos de la misma zona. Es una `CampaignAirlock` con puerta de origen A, puerta de destino A y una cámara de 8 × 7 × 5,5 m:

- El ciclo de presurización lo hace **`AirlockCycle` existente**, configurado con `Configure`/`ConfigureExtras` [CY:25] [CA:24]: ámbar parpadeando a 2,2 Hz con un 55 % encendido [CY:18,52], vapor y `IR_Airlock_Cycle`/`Done`.
- Las **luces giratorias** de la cámara son las balizas de la cara de cámara de la puerta de destino. Giran mientras `Cycle.Cycling` [CY:23] sea true.

**Dirección.** **A-1** reutiliza D-B V1 ([DC] §4, lamas gruesas escalonadas). **[cambio B]** Con 01_ESTRUCTURA hay 12 puertas A frente a 23 Z. A pasa a ser el **piloto técnico**: es la más barata y valida el contrato y el rig de lamas que comparte con P. Z pasa a ser el **objetivo visual** que se aprueba primero (create-game-assets, paso 5).

**Geometría.**

- Vano de 5 × 4,5 m.
- 6 lamas de 0,8 m (0,75 visibles más 0,05 de solape) y 0,14 m de grosor.
- La lama 0 lleva franjas de peligro, el labio emisivo y 3 pasadores que entran 0,32 m en el umbral.
- 4 levas, 2 por jamba.
- Casete de 1,3 m sobre el dintel, en la cara de la sala y dentro del muro de 1,0 m.

**Secuencia** (exacta en 0,7 s / 0,45 s; [DC] §4 ya la planteaba sin cambiar los tiempos):

| Fase | t (s) | k | Movimiento (pivote) | Audio candidato [WAV] |
|---|---|---|---|---|
| A1 Levas y pasadores | 0,00–0,15 | 0–0,21 | 4 levas giran 90° (eje en la jamba). Los 3 pasadores suben 0,32 m | `V4/IR_Mount_Clamp.wav` (0,50 s) + `HangarIntro/IR_Corridor_Klaxon.wav` (0,90 s, volumen 0,5) como sirena breve de ABRIÉNDOSE |
| A2 Elevación | 0,15–0,70 | 0,21–1 | La lama 0 sube H + 0,15 = 4,65 m (canto inferior). La lama i se mueve max(0, d − i·h). Aceleración tardía: el labio no pasa de 1,9 m antes de t ≈ 0,5 s | `HangarIntro/IR_Hangar_Hydraulic.wav` (silbido al despegar) + `HangarIntro/IR_Hangar_Clatter.wav` (1,10 s), o un «clac» por lama con `V4/IR_Fist_QuickImpact.wav` (0,35 s, bajo) |
| Fin | 0,70 | 1 | El root se desactiva y las lamas quedan en el casete | — (lo reproduce `AirlockCycle.Complete`) |
| C1 Caída | 0,00–0,36 | — | Caída escalonada en k²: la lama de arriba suelta primero | `IR_Hangar_Clatter.wav` |
| C2 Golpe | 0,36–0,39 | — | Impacto contra el umbral | `V4/IR_Fist_HeavyImpact.wav` + `Emit(pos, 10, 0,2, 0,25)` |
| C3 Cierre | 0,39–0,45 | — | Bajan los pasadores y se cierran las levas | `V4/IR_Mount_Lock.wav` (0,80 s) |

**Piezas y pivotes.** `Slat_00`–`Slat_05` (traslación Y; canto inferior). `Pins` (3 fusionados, hijo de `Slat_00`). `Cams_L` y `Cams_R` (2 fusionadas por lado si giran juntas; si no, 4). `Beacon_*`.

**Colliders.** Root con una Box de 5 × 4,5 × 0,84. Sin NavMeshObstacle.

**Luces de estado.** 1 Point transitoria por cara de sala; 0 si R5 ya tiene una luminaria a menos de 4 m del marco. 2 balizas por cara de sala y 2 en la cara de cámara de la puerta de destino.

**Presupuesto.** Lamas: 1.200–2.000 triángulos. Rig: 1.200–1.800 (boceto D-B V1: 852 / 1.467; propuesta de [DC] §6: root 1.200–2.500, marco 1.200–2.000). ≤ 12 draw calls por puerta.

**Coste relativo:** 1,0.

#### P1.4 E · Escotilla redonda de bóveda

**Función.** Pasos secundarios y atajos **dentro** de un cuarto, que es la única forma de atajo que permite la campaña lineal ([MD] §6 D2, [INV §1.4]).

- Se abre **una sola vez y no se vuelve a cerrar**: un atajo desbloqueado.
- Se desbloquea **por proximidad** desde el lado U, el lado de desbloqueo.
- No se usa la acción `Interact` (tecla E). Comparte la tecla con `CollectWeapon`, que ya usa `WeaponMountStation.cs:82` (bindings en `Assets/FPS/InputSystem_Actions.inputactions`), y el sistema de entrada no se toca.
- Hace de **filtro de enemigos**. El Big Bot no cabe (radio de 2,25 m y altura de 3,05 m [INV §2]). Los agentes de tipo 0 (radio 0,5, altura 2) sí pasan.

**Dirección.** Las dos son nuevas.

- **E-1 «Bóveda de rueda»**: rueda central de Ø 1,2 m, anillo de bayoneta de 8 orejetas y bisagra pesada externa desplazada 0,6 m del muro. Reutiliza de D-C ([DC] §5) la bisagra de nudillos, el brazo hidráulico y el sello.
- **E-2 «Bóveda rodante»** se valorará en F3a. El tapón se retira y rueda por un carril hasta un bolsillo lateral de 3,8 m. No hay barrido, pero sí más muro.

**Geometría (E-1).**

- Vano circular de Ø 3,6 m, truncado a ras de suelo, con el centro a 1,3 m: ancho a ras de 2,49 m, alto de 3,1 m y 3,07 m de alto en el borde de la cápsula (x = ±0,35).
- Marco de 0,8 m. La hoja mide 0,45 m más el labio del tapón.
- Bate 180° hacia la **cara opuesta al lado U**, para no barrer al jugador que la abre, y queda plana contra el muro sobre una **cuna estática** de 4,0 × 3,9 × 0,8 m.
- **[cambio B]** La única E del mapa está en R05, en un depósito de 8 × 6 m. Vano, marcos y cuna necesitan 9,2 m de muro, así que no cabe. Por eso la E de Map01 **bate 90°**: queda como aleta perpendicular al muro, contra un tope de suelo, y barre un cuarto de círculo de r 4,0 m junto a la bisagra. El batido de 180° con cuna queda como variante para muros de ≥ 9,2 m.

**Secuencia.** Todo lo largo ocurre antes de `Set` o en la cola posterior, así que no hace falta alargar el contrato:

| Fase | t (s) | Quién | Movimiento (pivote) | Audio [WAV] |
|---|---|---|---|---|
| E0 Pre-roll: rueda | 0,00–0,60 | `HatchDoor`, con el root sólido | La rueda gira 540° sobre la normal (centro) | `HangarIntro/IR_Hangar_Servo.wav` (pitch 0,6) |
| E0 Pre-roll: bayoneta | 0,60–0,85 | `HatchDoor` | El anillo gira 22,5° y suelta las 8 orejetas | `V4/IR_Mount_Lock.wav` |
| E1 Tapón | `Set(open)` k 0–0,15 | `AirlockDoorMotion` + presentación | El tapón avanza 0,15 m por la normal | `HangarIntro/IR_Hangar_Hydraulic.wav` |
| E2 Batido | k 0,15–1 (hasta 0,7 s) | idem | 0 → 80° sobre el eje de la bisagra (vertical). A 80° el vano ya está libre | `HangarIntro/IR_Hangar_CannonArm.wav` (1,20 s) |
| E3 Cola | 0,7–1,3 s tras `Set` | presentación (root inactivo) | 80 → 180° con frenado y apoyo en la cuna | `V4/IR_Fist_HeavyImpact.wav` (volumen 0,6) + `Emit(pos, 8, 0,15, 0,2)` |

**[cambio B]** Con el batido de 90°, E2 llega a 70° a los 0,7 s (el vano queda libre salvo ≈ 1,2 m junto a la bisagra) y la cola E3 va de 70° a 90° entre 0,7 y 1,1 s, frenando contra el tope. No hay cuna.

**Piezas y pivotes.** `Wheel` (normal). `BayonetRing` (normal; un solo transform en lugar de 8 cerrojos). `HingeArm` (eje vertical de la bisagra). `Leaf`, hija del brazo, con traslación por la normal para el tapón. `Damper_Body` y `Damper_Rod`, orientados entre dos anclas.

**Colliders.**

- Root con una Box de 3,6 × 3,1 × 0,6, que cubre el círculo truncado.
- El anillo del marco usa un **MeshCollider estático no convexo** de 64–128 triángulos, que es válido en geometría estática según `unity:physics-3d-collision`. Tapa las esquinas del hueco rectangular del muro.
- La cuna lleva una Box estática permanente.
- La hoja no tiene collider mientras bate.
- NavMeshObstacle en el root (§P3.6).

**Luces de estado.** Sin `Light`: solo emisivos.

- Lado bloqueado: rojo, con el icono «abrir desde el otro lado».
- Lado U: la rueda brilla en ámbar.
- Abierta: verde en el anillo.

**Presupuesto.** Hoja, rueda y anillo: 1.500–2.500 triángulos. Marco, bisagra y cuna: 1.200–2.000. ≤ 9 draw calls.

**Coste relativo:** 1,6.

#### P1.5 P · Persiana de seguridad o cierre de emergencia

**Función.** Va dentro de un cuarto, en la capa `[Doors]` de la escena. La mueve el componente nuevo `ArenaShutter`, que observa `CombatTwoRoomsLab`. Usos:

1. **Sello de arena.** Baja al empezar la ronda 1 (`WaveIndex >= 0` [CT:48]) y sube con `Completed`.
2. **Expansión.** Sube cuando empieza una ronda k. Encaja con las rondas ≥ 4, que usan `secondRoom` [CT:103,121] [INV §1.5].
3. **Entrada anti-retirada** (DU-6).
4. **Cierre narrativo** en una alarma.

Bajar «durante el combate» y subir «al terminar la ronda» se lee en `WaveIndex` y `Completed`. Entre rondas no hay pausa: la siguiente empieza en el mismo frame [CT:66].

**Dirección.** **P-1** reutiliza D-B V3 ([DC] §4): lamas finas de 0,48–0,5 m y grosor de 0,07 m, con n = ⌈H / 0,45⌉. Para vanos de más de 8 m se usan lamas V2, con bandas de estado cada 3 lamas.

**Geometría.**

- Vano de 4–12 × 4–6 m y marco de 0,6 m.
- Casete de 1,2 m de alto y 1,0 m de fondo (1,2 m si H > 5 m), en la **cara exterior a la arena**. La señal va en la cara de la arena.
- 0,6 m laterales.
- Una línea de cierre pintada en el suelo de la arena.

**Secuencia.**

| Fase | t (s) | Quién | Movimiento | Audio [WAV] |
|---|---|---|---|---|
| P0 Aviso | 0,0–1,0 | `ArenaShutter`, con el root inactivo y sin colisión | Balizas ámbar girando; las lamas tiemblan ±2 cm [SUPUESTO] | `HangarIntro/IR_Corridor_Klaxon.wav` |
| P0′ Espera | hasta que el vano esté libre | `ArenaShutter` | Comprueba el solape en cada frame (§P3.5) | — |
| PC1 Caída | 0,00–0,36 tras `Set(true)` | contrato | Caída escalonada | `HangarIntro/IR_Hangar_Clatter.wav` |
| PC2 Golpe | 0,36–0,39 | contrato | Golpe | `V4/IR_Fist_HeavyImpact.wav` + `Emit(pos, 10, 0,2, 0,25)` |
| PC3 Cierre | 0,39–0,45 | contrato | Levas | `V4/IR_Mount_Lock.wav` |
| Apertura | 0,70 | contrato | Igual que A1 y A2 | `V4/IR_Mount_Clamp.wav` + `IR_Hangar_Clatter.wav` (pitch 1,1) |

**Piezas.** `Slat_00`–`Slat_n` (traslación Y), `Lip` (en `Slat_00`), `Cams_L` y `Cams_R`, y `Beacon_*`.

**[cambio B] Variante P-c, compuertas de persecución de R10.** Dos compuertas: 16 × 8 visibles más una cabecera de 2 m para el casete, en un túnel de 10 m de alto. Solo son presentación sobre `entranceDoor`/`exitDoor` de `ChaseCorridorEncounter`, que sigue sin cambios y las activa con `SetActive` (§Q2).

**Colliders.** Root con una Box de W × H × 0,3 y un NavMeshObstacle con carve (§P3.6). El precedente es `ArenaGate` en `Editor/CombatTwoRoomsAuthoring.cs:42` (Box con carving).

**Luces de estado.** Sin `Light`, o 1 transitoria durante el aviso. Emisivos en el labio y las bandas, y 2 balizas en la cara de la arena.

**Presupuesto.** Lamas: 800–1.500 triángulos. Marco y casete: 800–1.500 (boceto D-B V3: 788 / 1.467 [DC §6]). ≤ 10 draw calls.

**Coste relativo:** 0,7.

#### P1.6 F · Puerta forzada o dañada

**Función y variantes.** Las tres se basan en A-1, con daños.

| Variante | Dónde | Comportamiento | Contrato |
|---|---|---|---|
| **F-A** Esclusa dañada | Sesión, como esclusa | Igual que A, pero la elevación se **atasca** entre k 0,45 y 0,52, con chispas y `HangarIntro/IR_Hangar_Tear.wav` (0,80 s), y después recupera de golpe para terminar en 0,7 s. El cierre rasca | Solo presentación: idéntico a A |
| **F-1** Atascada a medias | Cuarto, estática | Las lamas se quedan atascadas con un hueco fijo de **1,3 m** de alto: se pasa agachado (0,9 m) o deslizándose [INV §2]. Ningún agente de altura 2 cabe. De vez en cuando suelta chispas (`HangarIntro/IR_Hangar_Spark.wav`, 0,60 s, cada 4–9 s [SUPUESTO]) | No es una puerta con estado: la geometría y los colliders son estáticos y forman parte del horneado del NavMesh |
| **F-2** Mecanismo destruible | Cuarto, **solo en rutas opcionales** (DU-8) | La persiana está trabada por un actuador expuesto con un **punto de unión** brillante, el mismo lenguaje que las uniones de los enemigos. Al destruirlo, la persiana sale disparada hacia arriba por el contrapeso. Sirve para enseñar el pilar «destruir uniones» ([MD] §6 D1) | `ForcedDoor` observa `Health.OnDie` (`Assets/FPS/Scripts/Game/Shared/Health.cs`) y llama a `Set(open)`. Se abre una sola vez |

**Secuencia de F-2.**

| Fase | t (s) | Detalle | Audio |
|---|---|---|---|
| Pre-roll | 0,0–0,3 | Ráfaga de chispas; el vástago del pistón sale despedido (visual) | `IR_Hangar_Spark.wav` + `IR_Hangar_Tear.wav`, con `Emit(pos, 12, 0,3, 0,3)` |
| `Set(open)` | 0,7 | Elevación rápida con rebote | `IR_Hangar_Clatter.wav` |
| Cola | 0,4 | La lama 0 queda doblada en la boca del casete, ≤ 0,2 m bajo el dintel | — |

**Colliders.** F-A como A. F-1 con Box estáticos en el rig. F-2: root con Box y NavMeshObstacle; el mecanismo lleva una Box con `Damageable` y `Health` en el rig, y esa Box se desactiva al romperse. Salud del mecanismo: 60 [SUPUESTO].

**Presupuesto.** Igual que A más 500 triángulos de daño y 300–600 del mecanismo.

**Coste relativo:** 1,3 por las tres variantes, porque comparten las piezas de A-1.

#### P1.7 B · Puerta del jefe

**Función.** Es única: separa la antecámara de la arena del jefe **dentro de la escena del jefe**. Así la puede manejar un componente nuevo (`BossGateSequence`) con su secuencia larga y sin tocar `CampaignFlow`. La esclusa que lleva a esa escena es una Z normal.

**Dirección.** Las dos son nuevas.

- **B-1 «Sello de mandíbulas»**: sello central de Ø 6 m [SUPUESTO] con 8 pernos radiales y el emblema. Junta diagonal con dientes grandes (D-C + D-A). 2 hojas trapezoidales de **3 segmentos telescópicos** que entran en bolsillos laterales de 4 m: (W/2 + desfase de 1 m) / 3 + diente + 0,45 ≈ 3,7 m. 4 pistones visibles. **[cambio B]** La antecámara de R1 mide 16 m de ancho, así que solo queda 1 m a cada lado del vano: los 4 pistones van en la **corona** de la cara de antecámara (4 m sobre el dintel) y no a los lados.
- **B-2 «Fauces verticales»** se valorará en F3a: el sello gira, la hoja superior sube a una corona de 5 m y la inferior baja a un **foso** de 5 m. Exige foso, que tiene coste de estructura.

**Secuencia de B-1** (`BossGateSequence`; el root está sólido hasta el final de S5):

| Fase | t (s) | Qué pasa | Audio [WAV] | Efectos |
|---|---|---|---|---|
| S0 Compromiso | — | El jugador permanece 1,0 s en la plataforma; el emblema se enciende poco a poco | — | — |
| S1 Encendido | 0,0–0,8 | Emblema en rojo. `Stage = 1` es la señal para que R5 atenúe la antecámara | `HangarIntro/IR_Hangar_PowerUp.wav` (2,60 s) | — |
| S2 Sello | 0,8–2,4 | El sello gira 90° en 3 golpes | `V4/IR_Fist_HeavyImpact.wav` (pitch 0,7) ×3 | `Emit(pos, 30, 0,15, 0,2)` en cada golpe |
| S3 Pernos | 2,4–3,4 | Los 8 pernos se retiran uno a uno, cada 0,12 s | `IR_Mount_Lock` y `IR_Mount_Clamp` alternos | — |
| S4 Vapor | 3,4–3,9 | Vapor a lo largo de la junta diagonal (2 sistemas de ≤ 40 partículas) y chispas | `IR_Hangar_Hydraulic.wav` (pitch 0,8) | — |
| S5 Apertura | 3,9–6,4 | Las hojas se separan durante 2,5 s; `Set(open)` se llama a los 5,7 s y su fin coincide con el de las hojas | `IR_Hangar_Servo.wav` (pitch 0,6) + `IR_Hangar_CannonArm.wav` | — |
| S6 Revelación | 6,4 | `Stage = 6`: R5 enciende la arena y R3 hace la presentación del jefe | — | — |
| Cierre detrás del jugador | 0,45 | Con el jugador dentro de `firstRoom` y el vano libre | `IR_Fist_HeavyImpact.wav` (pitch 0,6) + `V4/IR_BigBot_Blast.wav` (2,60 s) [SUPUESTO: confirmar que suena a golpe y no a disparo] | `Emit(pos, 40, 0,5, 0,5)` |
| Reapertura | 2,0 | Con `Completed`: S3 y S5 acelerados | — | — |

**Colliders.** Root con una Box de 14 × 10 × 2,0 y un NavMeshObstacle con carve. Pistones y carcasas con Box estáticos.

**Luces de estado.** 2 Point transitorias, el emblema emisivo y 4 balizas. Las luces de la arena son de R5. **[cambio B]** R1 reserva un solo anclaje para la puerta B (01_ESTRUCTURA R18): queda **1 Point transitoria**, y el resto es emisivo.

**Presupuesto.** Hojas y sello: 10.000–14.000 triángulos. Marco y pistones: 6.000–9.000 [SUPUESTO]. LOD1 al 50 % y LOD2 en torno a 2.000. Solo se ve una vez, y es ≤ 6 % del techo de 400 k [INV §5]. Suma un material «hero».

**Coste relativo:** 4,0.

#### P1.8 Comparativa

| Clase | Dirección | Triángulos LOD0 (hojas + rig) | Draw calls por puerta | `Light` | Materiales | Tiempos | Coste |
|---|---|---|---|---|---|---|---|
| Z | Z-1 = D-A (+ Z-2 nueva) | 4,5–6,5 k | ≤ 18 | 1 transitoria | kit común | 0,7/0,45 (o 1,1/0,6 con DU-2) | 2,0 |
| A | D-B V1 | 2,4–3,8 k | ≤ 12 | 0–1 transitoria (+2 de `AirlockCycle`) | kit común | 0,7/0,45 | 1,0 |
| E | nueva (de D-C) | 2,7–4,5 k | ≤ 9 | 0 | kit común | pre-roll 0,85 + 0,7 + cola 0,6 | 1,6 |
| P | D-B V3 | 1,6–3,0 k | ≤ 10 | 0–1 | kit común | aviso 1,0 + 0,45 / 0,7 | 0,7 |
| F | A-1 dañada | 2,9–4,9 k | ≤ 14 | 0 | kit común | como A; F-2 con pre-roll 0,3 | 1,3 |
| B | nueva (D-A + D-C + sello) | 16–23 k | ≤ 40 | 2 transitorias | kit + 1 hero | secuencia de ≈ 6,4 s + 0,45 | 4,0 |

**Por vista.** Normalmente se ven ≤ 2–3 puertas, lo que supone ≤ 15 k triángulos y ≤ 36 draw calls, frente a un techo de 400 k triángulos y 350–400 draw calls [INV §5]. Se mide en F2.

**Número de instancias. [cambio B]** El recuento real está en la Parte B, §Q4: 23 Z, 11 A, 1 F-A, 1 E, 20 P más 2 P-c y 1 B. Las puertas de esclusa siempre cargadas son 35 en una sola sesión, o 11/18/10 con D-U4 (c).

---

### P2. Lenguaje de estado común

#### P2.1 Estados

Colores HDR de [DC] §2. El ritmo de ámbar es el de `AirlockCycle` [CY:18,52].

| Estado | Color | Lectura sin color (forma y movimiento) | Icono (atlas) | Mecánica visible | Sonido |
|---|---|---|---|---|---|
| **BLOQUEADA** | Rojo (1; 0,03; 0,02) ×9, fijo | Tiras continuas y quietas, balizas paradas | Barra horizontal «—» | **Cerrojos, levas y pasadores dentro** | — |
| **ALARMA** (combate en el cuarto) | Rojo y blanco alternos a ≤ 2,5 Hz durante 3 s; después rojo latente a 0,5 Hz | Segmentos alternos que se intercambian, balizas rojas girando 3 s y después un latido lento | Triángulo «!» | Cerrojos dentro | `IR_Corridor_Klaxon.wav` una vez. La sirena continua del cuarto es de R3 y `CorridorAlarm` |
| **PREPARANDO** (pre-roll) | Ámbar fijo | Balizas girando; las tiras avanzan hacia la hoja | Flechas circulares | Cerrojos **todavía dentro** | Z/B: `IR_Corridor_Siren.wav` en bucle |
| **ABRIÉNDOSE** | Ámbar (1; 0,36; 0,02) ×9 | Balizas a 1 vuelta cada 0,6 s ([DC] §2); chevrones que avanzan en el sentido de paso | Engranaje | **Cerrojos saliendo** (k ≤ 0,2–0,3) | Capas de la clase (P1), con sirena breve |
| **ABIERTA / PASA** | Verde (0,1; 1; 0,22) ×7 | Chevrones «»» fijos, marco vacío | Chevrones | Hojas recogidas | Al terminar, `IR_Airlock_Done` si no lo pone el ciclo |
| **CERRÁNDOSE / CICLO** | Ámbar parpadeando a 2,2 Hz con un 55 % encendido | Balizas de la cámara girando, vapor | Flechas circulares | Hojas bajando; cerrojos entrando al final | Golpe y cierre; `IR_Airlock_Cycle` lo pone `AirlockCycle.Begin` [CY:35] |
| **ABORT** | Ámbar → verde | Como abrir, pero los chevrones apuntan a la sala de origen | Flecha de vuelta | Cerrojos saliendo | `IR_Hangar_Hydraulic` (descompresión), hoy silencioso ([DM] §3) |
| **SELLADA** | Rojo tenue, sin `Light` ni pulso | Una «X» fija | «X» | Cerrojos dentro | — |
| **DORMIDA** | El estado lógico de arriba, sin `Light` | Sin `Update` | — | — | — |

**Reglas de legibilidad.**

- **La mecánica manda.** Cerrojos dentro ⇔ root activo y sin apertura en curso. El verde solo aparece en `End(open)`, así que el verde es la señal de «ahora sí se pasa». Esto corrige el verde en reposo de a0 y el `ExitFrameGlow` siempre encendido ([DM] §3).
- **Tamaño.** Tiras de ≥ 0,15 m de ancho y panel de icono de 1,2 × 0,6 m en el dintel. A 40 m son ≈ 4–5 px a 1080p [SUPUESTO: FOV horizontal de ≈ 90°].
- **Fotosensibilidad** (game-feel, WCAG 2.3.1). Los destellos grandes se mantienen ≤ 3 Hz, por eso la alarma baja de 4 Hz ([DC] §2) a 2,5 Hz. No existe una opción de «reducir destellos»: se anota como pendiente. El temblor de cámara ya respeta la opción del jugador `GameFeel.ShakeScale` [DCI:82].
- **Sin hit-stop en ninguna puerta.** La sensación de peso viene de la anticipación (cerrojos), de la inercia de la hoja y del golpe final, más vapor y sonido.
- **Colores reservados.** R5 no coloca luminarias rojas, ámbar ni verdes saturadas a menos de 6 m de un marco. R2 no pone emisivos en el marco ni en el volumen libre. En zonas iluminadas en rojo, la puerta se lee por el patrón de las tiras y las balizas.

#### P2.2 Derivación de cada estado sin tocar `CampaignFlow`

Solo se leen propiedades públicas:

- `CampaignFlow`: `CurrentStage`, `Busy`, `Phase`, `CurrentRoom` [CF:37-39,176].
- `CampaignRoom`: `Completed`, `Rounds` ([INV C3]).
- `CombatTwoRoomsLab`: `WaveIndex`, `Completed`, `LivingEnemies`, `PendingSpawns` [CT:48-49].
- `CampaignAirlock`: `SourceStage`, `PlayerInside`, `Cycle` [CA:17-23].
- `AirlockCycle`: `Cycling` [CY:23].
- Del root: `activeSelf`.
- `Begin`/`End` del enganche (o, en el Plan B, el movimiento observado del root).

Para la esclusa i, con `s = flow.CurrentStage`:

| Puerta | Condición | Estado |
|---|---|---|
| Origen de i (salida de R_i), cara de sala | `s < i` | BLOQUEADA (DORMIDA si `s < i − 1`) |
| | `s == i` y `Rounds.WaveIndex < 0` | BLOQUEADA |
| | `s == i`, `WaveIndex >= 0` y `!Completed` | ALARMA (estroboscopio en los 3 primeros segundos, como el banner de 2,8 s de [CAL:20]) |
| | `Begin(open)` con `!Busy` | ABRIÉNDOSE → ABIERTA en `End` |
| | `Begin(close)`, es decir `Busy` con `s == i` [CF:202] | CERRÁNDOSE / CICLO |
| | `Begin(open)` con `Busy` y `s == i` (solo puede ser el Abort [CF:211-216]) | ABORT |
| Origen de i, cara de cámara | `s > i` | SELLADA |
| Destino de i (entrada a R_{i+1}), cara de cámara | `s <= i` y sin pre-roll | BLOQUEADA |
| | `Busy`, `s == i` y `Phase >= 1` (sin vuelta atrás, ≥ 1,5 s disponibles [CF:224,256]) | PREPARANDO |
| | `Begin(open)` [CF:269] | ABRIÉNDOSE → ABIERTA |
| Extra de i | Igual que el destino | — |
| Balizas de la cámara de i | `Cycle.Cycling` | CICLO; si no, el estado de la puerta de origen |
| P / E / F / B | Lo publica su controlador (§P3.7) | — |

En una sala sin rondas (prólogo o respiro), ALARMA no se produce y vale BLOQUEADA hasta `Completed`.

**[cambio B]** Casos que añade 01_ESTRUCTURA:

- **Persecución** (R10, origen de C10): ALARMA mientras `CurrentRoom.Pursuit.Running` ([INV C3]); ABRIÉNDOSE con `Completed`. Esa puerta se ve desde 200 m.
- **Rol Initial** (`initialDoor`: S00 y, con D-U4 (c), los vestíbulos de llegada de S2 y S3):
  - BLOQUEADA → PREPARANDO con `Busy && Phase >= 1` durante `Prepare(0)`, sin cubierta negra porque `prologueStages = 0` [CF:49] → ABRIÉNDOSE ([CF:266]) → ABIERTA.
  - `AirlockDoorState` llama a `AirlockCycle.Begin()` del vestíbulo en `Start` y a `Complete()` cuando la puerta queda inactiva.
- **Salida de fin de sesión** (C05 y C13 con D-U4 (c)): sin `CampaignAirlock`. El estado sale de `Map01SessionHandoff` (§Q1.1).

#### P2.3 Señalización pintada de sector

Va pintada con plantilla en la banda del dintel de cada cara de sala.

- **[cambio B]** Formato: `<CÓDIGO DE ZONA>-<NN> · <NOMBRE>`, donde NN es el número del cuarto en el mapa, más una flecha de paso. Ejemplo: `CO-07 · COMUNICACIONES`.
- **No se pinta «SECTOR n».** El HUD numera con `Sector(target) = target + 1 − prologueStages` [CF:90], y con D-U4 (c) cada sesión vuelve a empezar en «SECTOR 1» (`PREPARANDO SECTOR n…` [CF:238,256]). El número pintado ya no coincidiría. La tabla de códigos está en §Q1.
- En la cara de cámara: `ESCLUSA C<NN> → <ZONA siguiente>`.
- Franjas de peligro a 45° en las jambas y en los cantos móviles.
- En las P, una línea de cierre en el suelo.
- Se hace con **quads con UV del atlas de puerta**, sin decales (el límite es de 24 por cuarto [INV §5]) y sin TMP.
- Nombres y colores de zona: R1 y R5.

---

### P3. Contrato técnico

#### P3.1 Invariantes (código actual, que no cambia)

- **Abierta** = root inactivo, en la pose cerrada y sin colisión [AIR:60].
- **Cerrando** = activo desde el primer frame [AIR:51].
- `IsClosed` = activo, sin corrutina y a < 1 mm de `closedWorld` [AIR:33-38]. `closedWorld` se captura en el primer `Set` [AIR:20].
- La única que manda en las puertas de esclusa es `CampaignFlow`:
  - abre la de origen con `Completed` [CF:96];
  - la cierra con el contacto [CF:202];
  - espera `IsClosed && PlayerInside && carga ≥ 0,9 && 1,6 s` [CF:206];
  - el Abort ocurre cuando el jugador lleva más de 1 s fuera [CF:211];
  - abre el destino y los extra [CF:269-270].
- Los tests exigen `!activeSelf` al abrir y un collider sólido activo en el frame de `Set(…, true)` (`Tests/PlayMode/CampaignTests.cs:57,62,78,190-193`; `MapFxAudit.cs:387-392`).

#### P3.2 Arquitectura: root y rig

```
C<NN>/[Doors]/
  C<NN>_Source_Door   (root: BoxCollider + medidor invisible)   ← CampaignAirlock.sourceDoor
  C<NN>_Source_Rig    (siempre activo: Frame_Static, piezas, Status, Light, Audio, FX,
                       DoorPresentation, AirlockDoorState)
  C<NN>_Destination_Door / _Rig
R<NN>/[Doors]/
  R<NN>_P1_Door  (root + NavMeshObstacle, creado inactivo)  /  R<NN>_P1_Rig (… + ArenaShutter)
```

En los prefabs de F3b, un contenedor `Door_<Clase>` siempre activo agrupa el root y el rig como hermanos.

#### P3.3 Enganche propuesto (DU-1): **modifica un sistema existente y requiere aprobación**

La interfaz es código nuevo en el mismo ensamblado, `Scripts/IronRequiem.Runtime.asmdef`:

```csharp
namespace IronRequiem {
  public interface IDoorPresentation {
    float Openness { get; }          // 0 cerrada … 1 abierta (pose visual actual; para invertir a mitad)
    float OpenSeconds { get; }       // <= 0 → 0,7 [AIR:12]. Solo Z con DU-2: tope 1,1
    float CloseSeconds { get; }      // <= 0 → 0,45. Tope 0,6
    void Begin(bool closing);        // mismo frame que Set (y que SetActive(true) al cerrar)
    void Evaluate(float k, float openness, bool closing); // k lineal de Run; openness ya con la curva
    void End(bool closing);          // antes de SetActive(false) (abrir) y antes de running.Remove
  }
}
```

**Cambios en `AirlockDoorMotion`** (unas 30 líneas):

- Se añaden `Register(GameObject door, IDoorPresentation p)` y `Unregister(door, p)`, con un diccionario estático que se limpia en `SubsystemRegistration`, igual que `Reset` [AIR:15]. `Unregister` purga también `closedWorld` y `running` de esa puerta: hoy las puertas de los cuartos descargados se quedan en los diccionarios estáticos.
- En `Run`, si la puerta tiene una presentación registrada:
  - **el root no se mueve**;
  - `from = p.Openness`;
  - al abrir, `openness = from + (1 − from) · smoothstep(k)`;
  - al cerrar, `openness = from · (1 − k²)`, con las mismas curvas que hoy [AIR:53,59];
  - `SetActive` funciona exactamente igual;
  - se llama a `End` antes de `running.Remove`, de modo que `IsClosed` [AIR:36] no es true hasta que termina la parte visual.
- Camino rápido (`!host`, o fuera de Play) [AIR:28]: `Begin`, después `Evaluate(1, cerrada ? 0 : 1)` y después `End`.
- **Sin presentación registrada, el código se ejecuta tal como hoy.**

**Qué mejora.**

- La colisión cubre **todo el vano desde el primer frame** del cierre. Hoy, la losa empieza en el techo y baja.
- No hay colliders estáticos movidos por transform.
- `IsClosed` es trivialmente exacto.
- El carve es inmediato, porque el obstáculo está quieto.
- Desaparece el problema de las losas que atraviesan techos ([DM] §4).

**Contrapartida.** Al abrir, la colisión dura hasta `End`, así que un jugador que corre hacia un hueco ya visible puede chocar contra una pared invisible durante ≤ 0,3 s. Mitigación:

- curvas que aceleran tarde, de modo que el gálibo no se despeja antes de k ≈ 0,6–0,7;
- el verde y los chevrones solo aparecen en `End`.

Nunca deja pasar antes de tiempo.

**Tests que hay que añadir** (F3b), todos en PlayMode:

- Con presentación: la posición del root es constante durante todo el recorrido; hay collider en el frame de `Set(true)`; `!activeSelf` tras la apertura; `IsClosed` es false hasta `End`; la inversión a mitad de recorrido funciona.
- Sin presentación: regresión idéntica a hoy.
- Volver a ejecutar las partes de puertas de `CampaignTests` y `MapFxAudit`.

#### P3.4 Plan B (sin aprobación): presentación que observa el root

- `AirlockDoorMotion` no cambia. El root sigue subiendo `Height = alto del medidor + 0,1` en 0,7 s / 0,45 s [AIR:43,48], ahora **invisible**.
- En `LateUpdate`, `DoorPresentation` calcula el grado de apertura:
  - `openness = (root.y − y_cerrada) / Height` mientras el root está activo;
  - `openness = 1` cuando está inactivo.
  
  La dirección sale de la variación entre frames. `Begin` y `End` se detectan por las transiciones.
- La regla visual es que el borde inferior de la obstrucción visible coincida con el borde inferior del collider. En A, P y F (lamas, que suben) coincide exactamente. En Z y B (laterales) se cumple de forma aproximada: hueco central ≈ fracción abierta. En E y B, el pre-roll y la cola siguen funcionando.
- Costes:
  - el collider invisible atraviesa el techo, lo que es inocuo;
  - el cierre «con colisión desde el primer frame» es solo nominal, como hoy, y la seguridad depende por completo de las reglas AT;
  - en las P, el carve llega después de que el root se detenga, unos 0,45 s más el tiempo hasta considerarse estacionario [SUPUESTO].

#### P3.5 Anti-atrapamiento

| Regla | Qué exige | Por qué |
|---|---|---|
| **AT-1** Esclusas | Distancia de ≥ 1,0 m entre la cara interior del collider del root y el borde del trigger, en ambos extremos | `Set(true)` solo se dispara por contacto [CA:28]. Una cápsula de radio 0,35 m [INV §2] que toca el trigger se extiende como mucho 0,70 m más allá de su borde. Quedan 0,30 m de margen. Hoy hay entre 1,0 y 2,7 m ([DM] §2) |
| **AT-2** | El trigger cubre todo el suelo de la cámara salvo esas franjas, y **toda su altura** | Con 2,7 m de hueco se provoca un Abort con el jugador dentro ([DM] §4, Juego 1). Un salto dura 0,9 s y llega a 3,83 m de cabeza; `PlayerInside` mira 0,25 s [CA:23] y el Abort necesita > 1 s fuera [CF:211] |
| **AT-3** | Nada pisable dentro del espesor del marco. Umbrales enrasados de ≤ 0,18 m, como `VisualPass_DoorSill` ([DM] §2). Sin repisas en la boca del casete | — |
| **AT-4** Interiores (P, B) | Antes de `Set(true)`: `Physics.OverlapBox(centro del vano, semiejes del vano + 0,35 m, rotación, máscara Player \| Enemigos, QueryTriggerInteraction.Ignore) == 0`, comprobado en cada frame y con búfer `NonAlloc`. Aviso de ≥ 0,8 s (P: 1,0 s). Si el vano sigue ocupado, se espera y no se fuerza nunca (aviso en el log a los 5 s). **[cambio B]** Además, el jugador no puede estar dentro del **volumen sellado** (`sealedVolume`: el vestíbulo, el rellano, el ala o la jaula que queda detrás) ni puede haber un enemigo de `rounds.Living` [CT:49] dentro; si no, espera. El aviso de los pórticos baja a 0,5 s | Las consultas de solape funcionan con cualquier `timeScale` (physics-3d-collision §8). El `CharacterController` es un collider no trigger. **[cambio B]** Si se cerrara con el jugador o un enemigo en el lado sellado, la ronda no podría terminar: soft-lock |
| **AT-5** | La puerta de destino nunca se cierra ([DM] §1.4). E y F-2 tampoco: son de un solo sentido | — |
| **AT-6** | B se cierra solo cuando el jugador está en `firstRoom` y el vano libre. `firstRoom` empieza ≥ 4 m detrás del plano del vano | La ronda 1 empieza al entrar en `firstRoom` [CT:63] |
| **AT-7** | El collider del root no excede vano + marco | No invade espacio transitable |
| **AT-8** | Las hojas móviles no llevan collider | Los colliders movidos por transform no empujan al `CharacterController` ([DM] §4) |

#### P3.6 NavMesh

**Esclusas** (Z, A, F-A): sin NavMesh ni NavMeshObstacle.

- La sesión no tiene `NavMeshSurface` (`CampaignAuthoring.RemoveNav`, l.40; [DM] §2).
- El suelo de la cámara, los umbrales y el espesor del marco pertenecen a la **escena de sesión**.
- El NavMesh del cuarto termina en el plano del muro, más la erosión del radio del agente.
- Prueba en F2: ningún polígono del NavMesh del cuarto a < 0,5 m del plano de una puerta (`NavMesh.FindClosestEdge` o `SamplePosition`).

**Interiores** (P, E, F-2, B): `NavMeshObstacle` (`UnityEngine.AI`, del motor) en el root.

- Box del tamaño del vano por el paquete de hojas.
- `carving = true` y `carveOnlyStationary = true`. `carvingTimeToStationary = 0` [SUPUESTO: verificar].
- El cuarto se **hornea con el root inactivo** (puerta abierta), así que la malla cruza el vano y el carve solo recorta mientras está cerrada.
- No se usa `NavMeshModifier`: hay dos clases con ese nombre, la del paquete AI Navigation (`Unity.AI.Navigation`) y `Assets/NavMeshComponents/Scripts/NavMeshModifier.cs:3,8` (`UnityEngine.AI`). AGENTS.md pide comprobar los namespaces. [SUPUESTO: verificar que `NavMeshSurface` ignora los GameObjects inactivos al recoger fuentes.]
- [SUPUESTO] Confirmar en F2 que el carve recorta también la superficie del Big Bot.

**Regla de spawns.**

- `SafeSpawn` exige un camino completo hasta el jugador [CT:123]. Con una P cerrada, los slots de las rondas activas tienen que estar en la región del jugador; si no, la ronda queda «OLEADA EN ESPERA» en bucle [CT:115].
- Ninguna P cierra con enemigos vivos al otro lado.
- R3 lo valida en F2 con un test: camino desde cada slot de ronda hasta el punto de referencia de la arena, con todas las P de esa ronda cerradas.

**F-1** es geometría estática horneada: no hay obstáculo.

#### P3.7 Componentes nuevos

Todos son **código nuevo** en `Assets/IronRequiem/Scripts/Doors/`, namespace `IronRequiem`, un MonoBehaviour por archivo y `[SerializeField] private`.

| Componente | Vive en | Entradas | Salidas | Observa / llama | Aprueba |
|---|---|---|---|---|---|
| `IDoorPresentation` | — | — | — | Lo llama `AirlockDoorMotion` si se aprueba DU-1 | Usuario (con DU-1) |
| `DoorClassProfile` (ScriptableObject) | Un asset por clase y variante | Pistas por pieza (transform, tipo, eje, distancia o ángulo, ventana de k al abrir y al cerrar, curva); cues de audio, partículas y temblor por fase; pre-roll y cola | — | — | R0 |
| `DoorPresentation` | Rig | `door` (root), perfil, piezas, `statusRenderers` y presets, balizas, `Light`, `AudioSource`, sockets de partículas, `observeRootMotion` (Plan B) | `State`, `Openness`, evento C# `PhaseChanged` | `AirlockDoorMotion.Register` (DU-1) o el root (Plan B); `DistanceCameraImpulse.Emit` [DCI:31] | R0 |
| `AirlockDoorState` | Rig de las puertas de esclusa (sesión) | `CampaignFlow`, `CampaignAirlock`, rol (origen, destino o extra), cara | Fija el estado en `DoorPresentation` | La tabla de §P2.2. DORMIDA si \|s − i\| > 1 | R0 |
| `ArenaShutter` (P) | Rig, escena del cuarto | `rounds` (de la misma escena), `door`, `closeOnWave` (0; −1 = nunca), `openOnWave` (−1 = con `Completed`), `warningSeconds` 1,0, volumen del vano, máscara de ocupantes | `Sealed`, `Warning`; `AirlockDoorMotion.Set(this, door, cerrada)` | `WaveIndex`, `Completed` [CT:48]. **No escribe en `message`**, que es de las rondas y del flujo [CT:56,66,98] | R0; reglas por sala de R3 |
| `HatchDoor` (E) | Rig | `door`, `unlockTrigger` en el lado U (trigger con Rigidbody cinemático, como `CampaignAirlock` en [DM] §1), `requireRoomCompleted`, `rounds` opcional | `Unlocked`; `Set(open)` | El contacto del jugador | R0 y R1 |
| `ForcedDoor` (F-2) | Rig | `door`, `Health` del mecanismo con `Damageable` y collider | `Breached`; `Set(open)` | `Health.OnDie` | R0 y R3 |
| `BossGateSequence` (B) | Rig, escena del jefe | `door`, trigger de compromiso, `commitSeconds` 1,0, `rounds` | `Stage` (0–6, lo leen R5 y R3), `Revealed`, `Sealed` | Trigger, `WaveIndex`, `Completed`, `FirstRoomBounds` [CT:53] | R0 y usuario (concepto de jefe) |
| `DoorAuthoring` (Editor) | `Editor/Map01/DoorAuthoring.cs` | `Levels/Map01/Data/Doors/C<NN>.json` y `R<NN>.json`, prefabs | Capas `[Doors]` reconstruidas con Undo y cuarentena de lo anterior (patrón de `LevelOpenPass`). Cablea `CampaignAirlock` por `SerializedObject` y `Configure`/`ConfigureExtras` [CA:24-25], y `AirlockCycle.Configure` [CY:25] | — | R0 lo ejecuta |

**[cambio B] Ajustes tras 01_ESTRUCTURA y 03_ENCUENTROS:**

- `ArenaShutter` suma la entrada `sealedVolume` (Bounds locales) y `warningSeconds` por puerta: 0,5 s en los pórticos y 1,0 s en las alas y la jaula.
- `ArenaShutter` es **el mismo componente** que `Map01ShutterController` de R3 (03_ENCUENTROS §A6.3). Hay un solo componente, en `Scripts/Doors/`. R3 aporta los datos de cada persiana (§Q5, R3-1).
- `AirlockDoorState` suma dos roles:
  - `Initial` (S00 y vestíbulos de llegada), con `AirlockCycle` opcional;
  - `HandoffDeparture`, que lee `Map01SessionHandoff` en lugar de `CampaignAirlock` (§Q1.1).
- Componente nuevo **`ChaseGateState`** (P-c de R10). Observa `ChaseCorridorEncounter.EntranceDoor`/`ExitDoor` (`activeSelf`), `Running` y la z del jugador, y se lo pasa a `DoorPresentation` con un pre-roll por distancia. No cambia `ChaseCorridorEncounter`. Lo aprueba R0.

**Notas de scripting** (gamedev:unity-csharp-scripting):

- Las corrutinas de `Set` corren en el *host*. Para P, E, F y B el host es el controlador del rig, que está siempre activo, porque un GameObject desactivado mata sus corrutinas.
- Nada de `?.` sobre objetos de Unity.
- `FindAnyObjectByType` solo una vez, en `Start`, como hace `CorridorAlarm` [CAL:35].

#### P3.8 Qué toca sistemas existentes y qué es solo código nuevo

| Elemento | Cambio | Aprobación |
|---|---|---|
| `AirlockDoorMotion.cs` | Enganche y purga (DU-1). La duración por presentación solo con DU-2 | **Usuario** |
| `CampaignFlow`, `CampaignAirlock`, `CampaignRoom`, `CombatTwoRoomsLab`, `AirlockCycle`, `CorridorAlarm`, `EnemySpawnQueue` | **Ninguno**: solo se leen propiedades públicas y se usan `Configure`/`ConfigureExtras` existentes | — |
| `MainAudioMixer.mixer`, shader HardSurface, `HG_*`, WAV compartidos | Ninguno: solo se referencian. Para recortar un clip se copia (§P1, audio) | — |
| `InputSystem_Actions` | Ninguno: E se abre por proximidad | — |
| Tests existentes | Ninguno; se añaden tests nuevos | R0 |
| Componentes de §P3.7 | Código nuevo | R0 (y lo que indique la tabla) |

**Audio, común a todas las clases** (gamedev:audio-design):

- Un `AudioSource` 3D por rig: `spatialBlend` 1, atenuación logarítmica, `maxDistance` de 30 m (70 m en B) [SUPUESTO].
- Salida al grupo **General** [MIX:516], que es el precedente de `CycleAudio` ([DM] §2).
- Los golpes, opcionalmente, a **Impact** [MIX:539] [SUPUESTO: comprobar que no hay ducking pensado para armas].
- No se añaden grupos al mezclador, que es global y compartido con la zona protegida [PZ §1b].
- Variación: pitch de ±4 % por instancia.
- Una sola fuente en bucle, la sirena de Z y B, que se detiene en `End`.
- Los WAV de `HangarIntro` (`Clatter`, `Hydraulic`, `PowerUp`, `Servo`, `Spark`, `Tear`) son compartidos con Room00 [PZ: before-20261117.txt:282-293]. Se **referencian sin cambiar su importación**. Cualquier recorte se hace en una copia en `Audio/SFX/Doors/`.
- No aparecen en esa instantánea: `IR_Airlock_*`, `IR_Corridor_*`, `IR_Hangar_CannonArm` y `V4/*`.
- Huecos de audio para F3b: un deslizamiento metálico grave y un «clac» de lama. Son pendientes y no se generan ahora.

---

### P4. Requisitos para Estructura (tabla para R1)

**Convenciones de la tabla.**

- «Marco» es el espesor mínimo del muro en la banda de la puerta.
- «Cara de sala» es el lado del cuarto contiguo.
- El volumen libre se mide (ancho del vano + 2 m) × fondo × alto del vano, como en [MD] §5.2, salvo donde se indica.
- En el volumen libre no hay decoración, spawns ni coberturas.

| Clase | Vano (an × al) | Marco | Sobre el dintel | Laterales (cada lado) | Cara del mecanismo o casete | Volumen libre | Cámara o antecámara (interior) | Techo |
|---|---|---|---|---|---|---|---|---|
| **Z** | 8 × 6 | 1,5 | 1,6 (balizas, rótulo y colector) | 3,0 dentro del muro (casetes) → **banda de 14 × 7,6 × 1,5 m** | Casetes y pistones en la cara de sala | Sala: 10 × **6** × 6. Cámara: 10 × 4 × 6 | 12 × 10 × 8 | Sala ≥ 8,0 m en la franja de 6 m; cámara 8 m |
| **A** | 5 × 4,5 | 1,0 | 1,3 en la cara de sala (casete dentro del muro) + 0,2 de cubierta | 1,0 (canales, levas y tiras) | Casete en la cara de sala; **nada sube dentro de la cámara** | Sala: 7 × 4 × 4,5. Cámara: la propia cámara | 8 × 7 × 5,5 | Sala ≥ 6,0 m en la franja de 4 m; cámara ≥ 5,0 m |
| **E** | Ø 3,6 truncada (suelo plano, 2,5 a ras, 3,1 de alto, centro a 1,3). En el muro, un hueco rectangular de 3,6 × 3,1 cuyas esquinas cierra el anillo del rig | 0,8 | 0,8 sobre el arco | 0,8 en el lado sin bisagra; 0,8 + **cuna de 4,0 m** en el lado de la bisagra. **[cambio B]** Con 90°, 0,8 a cada lado, sin cuna | Bisagra y cuna en la **cara opuesta al lado U** | Cara de batido: **semicírculo de r 4,0 m** + cuna (4,0 × 3,9 × 0,8). **[cambio B]** Con 90°, un cuarto de círculo de r 4,0 junto a la bisagra. Cara U: 4,5 × 4 × 3,1 | No se usa como esclusa | ≥ 4,5 m (regla general) |
| **P** | 4–12 × 4–6 | 0,6 | Casete de 1,2 de alto; fondo de 1,0 (H ≤ 5) o 1,2 (H 5–6) | 0,6 | Casete en la **cara exterior a la arena**; señal en la cara de la arena | (W + 2) × 4 × H en ambas caras + línea de cierre de 2 m en el suelo de la arena | — | Cara del casete ≥ H + 1,5 |
| **F** | Como A. F-1: hueco de 1,3 m de alto | 1,0 | Como A | Como A. F-2: **1,8** en la jamba del mecanismo | Mecanismo en la cara por la que se llega | Como A | F-A: como A | Como A |
| **B** | 14 × 10 | 3,0 | **4,0** (corona visible) | 4,0 dentro del muro → **banda de 22 × 14 × 3 m** | Sello, pistones y corona en la cara de antecámara | Antecámara: **plataforma de compromiso de 6 × 6 m a 8–12 m** del vano. Arena: 16 × 8 × 10 | **20 × 16 × 14** dentro de la **escena del jefe** | Antecámara ≥ 14 m (o 12 m con una corona de 2 m). B-2 exigiría un foso de 5 m |

**Reglas adicionales.**

1. **Esclusas.**
   - El trigger cumple AT-1 y AT-2.
   - El suelo de la cámara, los umbrales y el espesor del marco son de la sesión (`C<NN>/[Structure]`).
   - Anclajes de la cámara:
     - 2 lámparas de `AirlockCycle` en el techo, a ¼ y ¾ del largo, como en a0 ((0; 4,4; −82) y (0; 4,4; −78), [DM] §2). Son de Puertas.
     - 2 salidas de vapor bajas en los muros laterales.
     - 1 relleno de R5: las Transfer actuales están a oscuras por dentro, con lum de 0,011–0,040 ([DM] §3).
     - 2 balizas por puerta en las esquinas superiores de la cara de cámara.
   
   La cámara no lleva otra decoración.
2. **Persianas P.**
   - No cortan el bucle de una arena: están prohibidos los callejones sin salida en combate [MD §5.1].
   - No separan spawns del jugador (§P3.6).
   - En una ruta de Big Bot miden ≥ 5 m de ancho y ≥ 4 m de alto ([MD] §5.1; Big Bot de 3,05 m [INV §2]).
3. **E.** El trigger U está fuera del semicírculo de batido. Las coberturas y los spawns quedan fuera de ese semicírculo y de la cuna.
4. **B.** `firstRoom` (los límites de `CombatTwoRoomsLab`) empieza ≥ 4 m detrás del plano del vano (AT-6). La antecámara no lleva spawns.
5. **Teselas e iluminación.** Los marcos y los casetes son renderers propios. Las teselas de muro alrededor del vano miden ≤ 10 m, por el límite de 4 luces por objeto [INV §5].
6. **Exclusiones.** No se toca la esclusa a0 ni ninguna puerta visible desde la cinemática del hangar (DU-3).

---

### P5. Plan de F3a (conceptos) y esbozo de F3b

#### F3a: conceptos con bocetos, después de aprobar F1, con su propia PARADA

| Clase | Direcciones | Base |
|---|---|---|
| Z | **Z-1** Mandíbula pesada · **Z-2** Cuñas diagonales | D-A refinada · nueva (D-C + D-A) |
| A | **A-1** Persiana telescópica de esclusa | D-B V1 + cámara con ciclo |
| E | **E-1** Bóveda de rueda batiente · **E-2** Bóveda rodante | Nuevas (bisagra y brazo de D-C) |
| P | **P-1** Lamas finas, con dos anchos (6 m y 12 m) | D-B V3 |
| F | **F-1** Familia dañada (F-A, F-1 y F-2) | A-1 |
| B | **B-1** Sello de mandíbulas · **B-2** Fauces verticales | Nuevas |

En total, 9 direcciones.

**Vistas por dirección** (unas 10; ≈ 95 imágenes en total):

- 5 estados: BLOQUEADA, ABRIÉNDOSE con k = 0,35 y con k = 0,7, ABIERTA y ALARMA.
- CICLO visto desde el interior de la cámara (Z y A).
- Montaje **sin color** (escala de grises de los 4 estados), para comprobar la lectura con daltonismo.
- Conjunto a escala, con siluetas del jugador (1,8 m), del Big Bot (3,05 m) y del Carroñero (1,15 m).
- **Corte y planta con las reservas de P4**.
- Cronograma de fases y audio.
- Lámina resumen.

Extras: B suma una tira de 6 fotogramas S1–S6, E suma la planta del barrido y F suma sus 3 variantes.

**Dónde y con qué.**

- Destino: `Docs/IronRequiem/Concepts/Doors/Map01/<Clase>-<n>_<Nombre>/`, con `DOORS_MAP01_CONCEPTS.md` (pros y contras, coste, triángulos) y `report.json`.
- Blender 5.2 por script, igual que Redesign_v01 ([DC] §8: `"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --factory-startup --python-exit-code 1 --python …`), con Eevee a 1280×720.
- Se **copia** `door_lib.py` a la carpeta de Map01, sin modificar el original.

**Coste estimado.**

- Render: 5–12 min en total. Redesign_v01 hizo 3 direcciones en 1–3 min.
- Trabajo: una sesión de agente opus para los conceptos.
- 0 créditos, sin red y sin Meshy ni Tripo.
- Al terminar, **PARADA**: el usuario elige una dirección por clase y, si aún no lo ha hecho, decide DU-1 y DU-2.

#### F3b: esbozo, después de aprobar F3a

1. **Blender reproducible** en `Tools/IronRequiem/Blender/Doors_Map01/`, con una librería común y un `build_<clase>.py` por clase.
   - El `.blend` fuente se guarda aparte de las exportaciones.
   - Piezas separadas y con nombre: los pivotes de la sección P1 en la junta, la bisagra o el canto.
   - Sockets como empties `socket_*`; LOD0, LOD1 y LOD2; atlas compartido de 2048²; escala 1, Y hacia arriba, y aplicar escala antes de exportar (physics-3d-collision, paso 8).
   - Sin triángulos sueltos.
   - Exportación FBX a `Tools/IronRequiem/Exports/Doors_Map01/<Clase>/`.
   - Un **manifiesto JSON por clase**: fuente, piezas, pivotes, sockets, acciones (`open`, `close`, `preroll`, `tail`, `alarm`, `abort`, `break`, `reveal`) con duración y eventos, colliders, triángulos, materiales y estado. Principios de build-rigged-game-assets.
2. **Importación** en `Assets/IronRequiem/Art/Doors/Map01/`, con los materiales del §P1.0.
3. **Prefabs** `Assets/IronRequiem/Prefabs/Doors/Door_<Clase>[_<Variante>].prefab`, con contenedor, root y rig. Variantes de prefab para cada tamaño de P.
4. **Scripts** de §P3.7 y, si se aprueba DU-1, el enganche. Un `DoorClassProfile` por clase.
5. **`DoorAuthoring.cs`**, idempotente por capa `[Doors]` y con Undo. Lo ejecuta R0.
6. **Tests.**
   - EditMode, estructura del prefab: el root tiene exactamente 1 BoxCollider no trigger, ni MonoBehaviour ni Rigidbody. El rig referencia al root. Las piezas móviles no tienen collider. Los triángulos por LOD están dentro del presupuesto. Los materiales son compartidos y no instanciados.
   - PlayMode, contrato por clase (§P3.3), `ArenaShutter` con el jugador en el vano, carve con la puerta cerrada, secuencias de E, F-2 y B, y Abort.
   - Recorrido manual del coordinador.
7. **Modelos de agente** ([MD] §2): modelado, presentación y autoría con sonnet; revisión con opus.

---

### P6. Riesgos

| # | Riesgo | Impacto | Mitigación | Dueño |
|---|---|---|---|---|
| 1 | No se aprueba DU-1 | Medio | Plan B (§P3.4): mismas clases, pero con colisión de cierre nominal y carve tardío. Hay que reforzar AT-1, AT-2 y AT-4 | Usuario / R4 |
| 2 | Una P separa spawns o enemigos del jugador | **Alto (soft-lock)** [CT:115,123] | Regla de §P3.6 + test de caminos en F2 + nunca cerrar con enemigos al otro lado | R3 / R1 |
| 3 | El jugador se queda bajo una puerta que cierra | Alto | AT-1 a AT-8; aviso previo; comprobación de solape | R4 / R1 |
| 4 | Retirada a la cámara de esclusa, sin NavMesh, durante el combate | Medio | P de entrada (DU-6) | Usuario / R3 |
| 5 | Pared invisible ≤ 0,3 s al abrir (con DU-1) | Bajo | Curvas que aceleran tarde y verde solo en `End` (§P3.3) | R4 |
| 6 | Las 28–32 puertas de esclusa están siempre cargadas | Medio (CPU limitada [INV §5]) | Estado DORMIDA sin `Update` ni `Light`, LOD2, renderers aparcados desactivados, intercambio de `sharedMaterial` y no de MaterialPropertyBlock | R4 |
| 7 | Luces de estado frente al límite de 8 luces visibles y 4 por objeto [INV §5] | Medio | `Light` solo en transitorios, sin sombras, rango ≤ 8 m; R5 reserva una plaza transitoria por cuarto | R4 / R5 |
| 8 | Fotosensibilidad | Medio | ≤ 3 Hz. No existe la opción «reducir destellos»: queda como **pendiente** | R4 / R0 |
| 9 | Zona protegida (a0, `HG_*`, shader, WAV compartidos) | Alto | DU-3, materiales nuevos o copias, WAV solo por referencia; verificar `Shot_C_Exit` si se toca algo visible desde el hangar | R0 |
| 10 | Que se vuelvan a ejecutar `CampaignAuthoring`, `HangarTransitAuthoring` o `VisualPassF2` D1 sobre Map01 | Alto ([DM] §5) | No ejecutarlos y usar nombres distintos de `SourceDoor`/`DestinationDoor` | R0 |
| 11 | Bandas de muro grandes en Z (14 m) y B (22 m) | Medio | Si R1 no tiene espacio, la Z se convierte en una «Z-lite» con D-B V2 vertical (casete de 1,4 m) | R1 |
| 12 | La hoja de E barre a quien esté delante | Bajo | Bate hacia el lado contrario a U; trigger fuera del barrido; la hoja no tiene collider | R1 |
| 13 | El fuego enemigo rompe el mecanismo de F-2 | Bajo | Aceptado: es una ruta opcional [SUPUESTO: afiliación de los proyectiles] | R3 |
| 14 | Entradas obsoletas en los diccionarios estáticos de `AirlockDoorMotion` cuando se descarga un cuarto | Bajo | Purga en `Unregister` (DU-1) | R4 |
| 15 | Ambigüedad de `NavMeshModifier` entre el paquete y `Assets/NavMeshComponents` | Medio | Hornear con el root inactivo y comprobar namespaces (AGENTS.md) | R0 |
| 16 | El pre-roll de destino depende de `Phase` [CF:176] | Bajo (solo afecta a lo visual) | Si cambia `CampaignFlow`, el pre-roll deja de verse, pero el contrato sigue intacto | R4 |
| 17 | `Prepare` termina con error y deja `Busy` siempre a true con ambas puertas cerradas ([DM] §4, Juego 2) | Alto, pero ya existe | Fuera de alcance: requiere un cambio de `CampaignFlow`. Anotado como pendiente | R0 |
| 18 | `Renderer.bounds` con `forceRenderingOff`, y `carvingTimeToStationary` | Bajo | Se verifica en F2, antes de producir | R0 |

---

### P7. Skills usadas

| Skill | Cargada | Qué se aplicó |
|---|---|---|
| `iron-requiem-performance` (leído el SKILL.md) | Sí | Presupuestos derivados de [INV §5] y marcados para medir en F2. Intercambio de `sharedMaterial` en lugar de MaterialPropertyBlock para no salir del SRP Batcher. LOD y colliders creados en copias. Estado DORMIDA para las puertas siempre cargadas |
| `gamedev:create-game-assets` | Sí | La familia como sistema: una paleta con roles (emisivo reservado al estado), lenguaje de forma por clase, un hero asset que se aprueba primero (A-1), manifiesto por clase, LOD y presupuesto de triángulos, validación a escala de juego y procedencia (scripts propios, sin generación) |
| `gamedev:game-feel` | Sí | Niveles de temblor llevados a `DistanceCameraImpulse.Emit` con caída por distancia (0,15–0,5). Sin hit-stop. Anticipación (cerrojos), acción y continuación (cola y apoyo). Destellos ≤ 3 Hz (WCAG 2.3.1). Respeto de la opción `GameFeel.ShakeScale` |
| `gamedev:audio-design` | Sí | Salida a grupos existentes del mezclador sin modificarlo. Capas por fase. Variación de pitch. Un único bucle (sirena) con parada garantizada. No duplicar `IR_Airlock_Done` con `AirlockCycle`. Copias para recortes |
| `unity:physics-3d-collision` | Sí | Un BoxCollider estático en el root, sin Rigidbody. Piezas móviles sin collider (no hay colliders estáticos movidos). `OverlapBox` con `QueryTriggerInteraction.Ignore` (funciona con cualquier `timeScale`). MeshCollider no convexo solo en el anillo estático de E. Triggers con Rigidbody cinemático, como `CampaignAirlock`. Escala aplicada en el DCC |
| `gamedev:unity-csharp-scripting` | Sí | Un MonoBehaviour por archivo, `[SerializeField] private`, el host de las corrutinas siempre activo (se detienen al desactivar el GameObject), sin `?.` sobre objetos de Unity, `Find*` en caché. APIs que hay que verificar en 6000.5 (`forceRenderingOff`, carve) |
| `build-rigged-game-assets` (solo principios) | Sí | Fuente preservada aparte del runtime, pivotes y sockets con nombre, biblioteca de acciones con id, rol, duración y eventos, volúmenes de colisión que mandan y van separados de lo visual, manifiesto validable. No se usó nada de su código ni de three.js |

No se cargaron `gamedev:shader-programming`, `gamedev:unity-animation` ni `unity:audio-setup-mixers`, que son para F3b, como indica el brief.

---

## PARTE B — Asignación por conexión

18/11/2026. Base: `01_ESTRUCTURA.md` de R1 ([EST]) y `03_ENCUENTROS.md` Parte A de R3 ([ENC]). Coordenadas **locales** del cuarto, como en las fichas de [EST] §7 (yaw 0: mundo = origen + local). «Techo» es la altura libre sobre la cota de la puerta.

**Cómo encaja la banda con R1.** La banda del marco está **dentro del AABB de la esclusa**, y el muro del cuarto lleva un hueco del tamaño de su cara ([EST] §2.4, CC-9).

- Los casetes de A y los bolsillos de Z quedan en la banda.
- Los pistones, las balizas y los rótulos de la cara de sala ocupan el nicho de 1 m que forma el hueco.
- Todo lo de la Parte A (§P3.2 y §P4) sigue valiendo: el «muro» de §P4 es esa banda.

### Q1. Tabla por conexión

**Códigos de zona para la señalización pintada** ([cambio B] en §P2.3):

| Código | Zona | Código | Zona |
|---|---|---|---|
| AC | Acceso | TU | Túneles de mantenimiento |
| HB | Habitáculo | AG | Tratamiento de agua |
| AR | Armería | FU | Taller/Fundición |
| CO | Comunicaciones | EN | Ensamblaje |
| AL | Almacenes | RE | Reactor |
| — | — | CM | Centro de mando |
| — | — | BV | Bóveda |

**Qué se pinta en cada cara:**

- Cara de sala de la puerta de origen: código y nombre del cuarto siguiente, más una flecha de paso.
- Cara de sala de la puerta de destino: el mismo rótulo, como cartel de llegada.
- Caras de cámara: `ESCLUSA C<NN> › <código>`.
- En las Z se añade una banda superior `ZONA <NOMBRE>` en el color de la zona (DU-4).
- El atlas de glifos lleva Á, É, Í, Ó, Ú, Ñ, «·», «›» y flechas.

**Secuencias de estado** (§P2.1 y §P2.2):

| Secuencia | Estados |
|---|---|
| **O** (origen) | BLOQUEADA → ALARMA (con rondas activas) → ABRIÉNDOSE → ABIERTA → CICLO → SELLADA, con ABORT posible durante el CICLO |
| **O-r** (origen en un respiro) | Igual que O, pero sin ALARMA: abre cuando `Map01StageRelease` libera el cuarto |
| **O-p** (origen en la persecución) | ALARMA mientras dura la persecución |
| **D** (destino) | BLOQUEADA → PREPARANDO → ABRIÉNDOSE → ABIERTA |
| **I** (inicial) | Como D, durante `Prepare(0)` |

| Conexión | Recorrido (eje) | Clase · dirección | Vano | Casete o bolsillos | Techo origen / destino (exige) | Rótulo (cara de sala) | Estados | Comportamiento especial |
|---|---|---|---|---|---|---|---|---|
| **S00** (`initialDoor`) | S00 → R01 S, x 0 (+Z) | Z · Z-1 | 8 × 6 | Bolsillos en la banda (z −56,5…−55). Pistones y balizas en la cara de R01 y en la del vestíbulo | S00 8 / R01 9 (≥ 8) ✓ | `AC-01 · CONTROL DE ACCESO` + `ZONA ACCESO`. Vestíbulo: `ACCESO › AC-01` | I | La abre `CampaignFlow` con target 0 [CF:266]; no tiene `CampaignAirlock`. `AirlockDoorState` (rol Initial) pone el `AirlockCycle` del vestíbulo en ámbar desde `Start` y lo completa al abrir. Con `prologueStages = 0` no hay cubierta negra [CF:49]: el jugador ve ≥ 1,5 s de PREPARANDO [CF:256] |
| **C01** | R01 N, x +6 → R02 S, x −14, cota +12 (+Z) | A · A-1 | 5 × 4,5 | Casete en la cara de R01 (origen) y de R02 (destino) | R01 9 / R02 9 (≥ 6) ✓ | `AC-02 · POZO MONTACARGAS` | O / D | El anclaje de R1 en (6; 6,6; 19,4) queda sobre la banda: la cara de R01 no lleva Point de puerta (§P1.3) |
| **C02** | R02 N, x 0 → R03 S, x 0 (+Z) | Z · Z-1 | 8 × 6 | Bolsillos en la banda | R02 21 / R03 **8** (≥ 8) ✓ justo | `HB-03 · DORMITORIOS` + `ZONA HABITÁCULO` | O / D | En R03 quedan 0,4 m de muro sobre la banda: no cabe ningún anclaje encima. R02: G1 y el contrapeso invaden el volumen libre (R1-1) |
| **C03** | R03 N, x +13 → R04 S, x −10 (+Z) | A | 5 × 4,5 | Cara de sala | R03 8 / R04 9 (≥ 6) ✓ | `HB-04 · COMEDOR` | O / D | Luz de R1 en (13; 6,5; 23,5), sobre la banda ✓ |
| **C04** | R04 E, z +8 → R05 W, z +8 (+X) | A | 5 × 4,5 | Cara de sala | R04 9 / R05 8 (≥ 6) ✓ | `HB-05 · ENFERMERÍA` | O / D | Luz de R1 en (19,5; 6; 8) ✓. La destino da a un respiro |
| **C05** | R05 E, z −6 → R06 W, z −10, cota +6 (+X) | Z | 8 × 6 | Bolsillos en la banda | R05 **8** / R06 **8** (14 − 6) (≥ 8) ✓ justos | `AR-06 · ARMERÍA 7` + `ZONA ARMERÍA` | O-r / D | **Corte de sesión con D-U4 (c)** (§Q1.1); sin D-U4 (c), esclusa normal. La luz de salida de R05 va al lado de la banda (R1-8) |
| **C06** | R06 S, x +14 → R07 N, x 0 (−Z) | Z | 8 × 6 | Bolsillos en la banda | R06 14 / R07 10 ✓ | `CO-07 · COMUNICACIONES` + `ZONA COMUNICACIONES` | O / D | R06 tiene los 8 anclajes ocupados: la Point de puerta solo se enciende en transitorios. G1 de R06 invade el volumen libre (R1-2) |
| **C07** | R07 S, x 0 → R08 N, x −10, cota +4 (−Z) | Z | 8 × 6 | Bolsillos en la banda | R07 10 / R08 10 (14 − 4) ✓ | `AL-08 · ALMACENES` + `ZONA ALMACENES` | O / D | La ALARMA de la puerta coincide con `CorridorAlarm` [CAL:46,56]. La puerta no reproduce klaxon porque ya suena el de la alarma [CAL:64] |
| **C08** | R08 E, z −20 → R09 W, z −6 (+X) | Z | 8 × 6 | Bolsillos en la banda | R08 14 / R09 **8** ✓ justo | `TU-09 · TAQUILLAS` + `ZONA TÚNELES` | O / D | BS2 de R08 está dentro del volumen libre de salida (R1-3). Las taquillas de R09, a validar (R1-5) |
| **C09** | R09 N, x +6 → R10 S, x 0 (+Z) | A | 5 × 4,5 | Cara de sala | R09 8 / R10 10 ✓ | `TU-10 · TÚNEL T-9` | O-r / D | La destino da a la bahía de llegada. La P-c de entrada está 14 m más allá (§Q2) |
| **C10** | R10 N, x 0 → R11 S, x 0, cota +6 (+Z) | Z | 8 × 6 | Bolsillos en la banda; 1 m de muro a cada lado en el túnel de 16 m | R10 10 / R11 **8** ✓ justo | `AG-11 · DECANTADORES` + `ZONA TRATAMIENTO DE AGUA` | **O-p** / D | ALARMA mientras `Pursuit.Running` ([cambio B] §P2.2). Es la meta que se ve a 200 m durante toda la persecución; se pone en verde al terminar ([EST] R10) |
| **C11** | R11 N, x −12 → R12 S, x −10 (+Z) | A (origen) + **F-A (destino)** | 5 × 4,5 | Cara de sala | R11 14 / R12 14 ✓ | `AG-12 · COLECTOR`. Cámara: `ESCLUSA C11 › AG-12 · DERRUMBE` | O / D con atasco | **F-A en la puerta de destino**: es la que se ve de cerca desde la cámara y da al colector derrumbado. Se atasca entre k 0,45 y 0,52, con chispas y `IR_Hangar_Tear` (§P1.6), y cumple el mismo contrato que A. Opcional: una lámpara de ciclo apagada |
| **C12** | R12 N, x +10 → R13 S, x +4 (+Z) | A | 5 × 4,5 | Cara de sala | R12 14 / R13 10 ✓ | `AG-13 · SALA DE BOMBAS` | O / D | — |
| **C13** | R13 N, x −6 → R14 S, x 0 (+Z) | Z | 8 × 6 | Bolsillos en la banda | R13 10 / R14 13 ✓ | `FU-14 · FUNDICIÓN` + `ZONA TALLER` | O-r / D | **Corte de sesión con D-U4 (c)** (§Q1.1) |
| **C14** | R14 N, x +18 → R15 S, x −10 (+Z) | Z | 8 × 6 | Banda en x 11…25, a 1 m de la esquina E y del hueco del horno | R14 13 / R15 13 ✓ | `EN-15 · ENSAMBLAJE` + `ZONA ENSAMBLAJE` | O / D | Es la «salida real con marco completo» de R14 (M11): la señal son las tiras y los chevrones de esta puerta |
| **C15** | R15 N, x 0 → R16 S, x 0, cota +6 (+Z) | Z | 8 × 6 | Bolsillos en la banda | R15 13 / R16 **8** ✓ justo | `RE-16 · REACTOR` + `ZONA REACTOR` | O / D | El pilar del riel de R15 en (0; 19) invade el volumen libre (R1-4) |
| **C16** | R16 N, x −14 → R17 S, x −8, cota +3 (+Z) | Z | 8 × 6 | Bolsillos en la banda | R16 14 / R17 9 (12 − 3) ✓ | `CM-17 · CENTRO DE MANDO` + `ZONA MANDO` | O / D | El «marco cian» de M12 es la banda de zona. El cian no es un color de estado (§P2.1) |
| **C17** | R17 N, x 0 → R18, antecámara S, x 0 (+Z) | Z | 8 × 6 | Bolsillos en la banda; en la antecámara de 16 m queda 1 m a cada lado | R17 12 / R18 14 ✓ | `BV-18 · BÓVEDA` + `ZONA BÓVEDA · ÚLTIMA ESCLUSA` | O-r / D | Pre-roll largo (sirena y emblema) desde `Phase >= 1`. La destino da a la antecámara de R18, sin enemigos |

**Totales:** 11 esclusas Z, 6 esclusas A y S00. Todos los techos cumplen. Seis puertas Z están **justas a 8 m**, sin margen: en R03, R05, R06, R09, R11 y R16. Por eso R1 no puede bajar esos techos en F2.

#### Q1.1 Cortes de sesión en C05 y C13 (D-U4 c, `Map01SessionHandoff`)

Con tres sesiones, R05 es la última etapa de S1 y R13 la de S2. `CampaignFlow` solo abre la salida si `CurrentStage < airlocks.Length` [CF:96]. Como `airlocks.Length == stageScenes.Length − 1`, **nadie abre la puerta de origen de C05 en S1 ni la de C13 en S2**. `Map01SessionHandoff` (componente nuevo de R0) asume las funciones de `CampaignFlow` con el mismo contrato de puerta.

**Salida (en S1, C05; en S2, C13).** La lleva `Map01SessionHandoff`, en la sesión y siempre activo, que también es el host de las corrutinas.

1. **Apertura.** Cuando se cumplen a la vez `flow.CurrentStage == flow.StageScenes.Length − 1`, `flow.CurrentRoom.Completed` (el respiro liberado por `Map01StageRelease`), `!flow.Busy`, `flow.Menu.IsPlaying` y `!flow.Player.IsDead`, llama a `AirlockDoorMotion.Set(this, sourceDoor, false)`. Es idempotente, como [CF:95-96].
2. **Trigger propio en la cámara.** BoxCollider trigger con Rigidbody cinemático, igual que `CampaignAirlock`, que cumple AT-1 y AT-2: 10 × 10 × 8 m en una Z. `PlayerInside` es un contacto en los últimos 0,25 s, como [CA:23].
3. **Entrada del jugador.** Cuando la salida está abierta y el jugador hace contacto:
   - `Set(this, sourceDoor, true)`;
   - `cycle.Begin()`;
   - `Departing = true`.
4. **Espera.** `IsClosed(sourceDoor) && PlayerInside && t ≥ 1,6 s` (copia de [CF:206]). Después carga la sesión siguiente.
5. **Abort.** Copia de [CF:211-216]: si la puerta está cerrada y el jugador lleva más de 1 s fuera, `Set(this, sourceDoor, false)`, `cycle.Abort()` y `Departing = false`. Una vez empezada la carga ya no hay Abort.
6. **La puerta lejana de esa cámara** no se mueve en la sesión de salida: su root sigue activo y no pasa por `AirlockDoorMotion`. `AirlockDoorState` (rol `HandoffDeparture`) la muestra BLOQUEADA y pasa a PREPARANDO cuando `Departing` es true.
7. **Propiedades públicas** que necesita `AirlockDoorState`: `Armed` (salida abierta), `Departing` y `PlayerInside`.

**Llegada (vestíbulo de S2 = copia de C05; de S3 = copia de C13).**

- Misma geometría, mismos prefabs y mismas coordenadas. `DoorAuthoring` las construye desde el mismo `C05.json`/`C13.json` en las dos sesiones.
- La puerta de origen tiene el root activo y nunca se mueve: se ve SELLADA por la cara de cámara desde el primer frame.
- La lejana es el `initialDoor` de la sesión nueva (rol Initial).
- El `AirlockCycle` del vestíbulo arranca en ámbar (`Begin` en `Start`), así que **el primer frame de la sesión nueva coincide con el último de la anterior**: origen cerrada, ciclo en ámbar y lejana en PREPARANDO. Pasa a verde cuando se abre el `initialDoor`.
- `prologueStages = 0` en las tres sesiones.

**Sin D-U4 (c):** C05 y C13 son esclusas Z normales, con `CampaignAirlock`.

### Q2. Puertas internas

**Reglas comunes de `ArenaShutter`** (R-P), que concretan AT-4 [cambio B]:

| Regla | Contenido |
|---|---|
| R-P1, cierre | Solo cierra si se cumplen las tres condiciones: (a) el vano, ampliado 0,35 m, está libre de jugador y enemigos; (b) el jugador está fuera del `sealedVolume`; (c) ningún `rounds.Living` está dentro. Si no, reintenta cada 0,25 s, como propone R3 ([ENC] §A6.3). Aviso: 0,5 s en los pórticos y 1,0 s en las alas |
| R-P2, apertura | Abre con `Completed` o con `openOnWave`. Tras `Completed` no vuelve a cerrar |
| R-P3, NavMesh | El cuarto se hornea con el root **inactivo** (abierta). El root lleva NavMeshObstacle Box con carve (§P3.6) |
| R-P4, slots | El `sealedVolume` no contiene slots ni `FixedPoint`. Como el jugador nunca queda dentro (R-P1), **el camino de cualquier slot al jugador no atraviesa la zona sellada**: `SafeSpawn` [CT:123] no puede quedarse «OLEADA EN ESPERA» por una persiana |
| R-P5, mensajes | No escribe en `message` [CT:56,66,98] |

| ID | Posición (local) | Vano | Casete | Componente (dueño) | Cierra / abre | Volumen sellado | NavMesh y spawns |
|---|---|---|---|---|---|---|---|
| R01-P1 | z −14, entre los bloques de taquillas | 10 × 6 | Cara del vestíbulo; 1,2 × 1,2 | `ArenaShutter` (Puertas; datos de R3) | Ronda 0 / `Completed` | Vestíbulo x −8…8, z −20…−14 | Vestíbulo sin slots (los grupos están a ≥ 18 m) ✓ |
| R02-P1 | Plano x −8 del rellano (+12) | 6 × 4,5 | Cara del rellano; 1,2 × 1,0 | `ArenaShutter` | Ronda 0 / `Completed` | Rellano x −20…−8, z −22…−12, a +12 | Sin slots; la barandilla N de 2,4 m lo cierra ✓. **El cerramiento del resto del plano está por definir** (R1-6) |
| R03-P1 | z −18 | 12 × 6 | Vestíbulo; 1,2 × 1,2 (techo 8 ≥ 7,5) | `ArenaShutter` | Ronda 0 / `Completed` | Vestíbulo z −24…−18 | ✓ |
| R04-P1 | z −16, fondo del vestíbulo x −14…−6 | 6 × 4,5 (jambas de 1 m) | Vestíbulo; 1,2 × 1,0 | `ArenaShutter` | Ronda 0 / `Completed` | Vestíbulo x −14…−6, z −20…−16 | ✓ |
| R06-P1, P2 | Entreplanta (+6), planos z −17 y z −3, desde x −24 hasta el borde | 6 × 6 + jamba exenta de 0,6 en el borde (x −17,6…−17, hasta +13,2) | En la cara **opuesta** al rellano, para no tapar la cara de sala de la banda Z (z −17…−3) | `ArenaShutter` ×2, con el mismo `sealedVolume` | Ronda 0 / `Completed` | Rellano x −24…−17, z −17…−3, a +6 | Sin slots ✓. **Parten la entreplanta** en combate; el bucle grande sigue por las caídas (R1-7) |
| R07-P0 | z 20 (pórtico N) | 10 × 6 | Vestíbulo N; 1,2 × 1,2 | `ArenaShutter` | Ronda 0 / `Completed` | Vestíbulo N, z 20…26 | ✓ |
| R07-PW1, PW2, PE1, PE2 | Tabiques x ±19; z −14…−8 y 8…14 | 6 × 4,5 | Cara del ala; 1,2 × 1,0 | `ArenaShutter` ×4 | Ronda 0 / `Completed`. **Sin klaxon** (suena `CorridorAlarm`) | Cada ala, x ±19…26 | Alas sin slots ([EST] R07) ✓. Con las alas cerradas sigue habiendo bucle ✓. **R-P1 (b) es esencial aquí**: un jugador que entra en un ala durante el aviso dejaría la ronda sin poder terminar |
| R08-P1 | Cubierta (+4), plano z 22, entre muretes de 3 m | 10 × 6 | Cara del rellano | `ArenaShutter` | Ronda 0 / `Completed` | Rellano x −15…−5, z 22…28, a +4 | ✓. El paso de la cubierta (z 18…22) queda libre: el bucle no se corta. Fuera de la ruta del BIG |
| R11-P1, P2 | Pasarela S (+6), planos x ±7 | 6 × 6 (techo 8 ≥ 7,5) | Caras exteriores (\|x\| > 7), para no tapar la banda Z | `ArenaShutter` ×2 | Ronda 0 / `Completed` | Rellano x −7…7, z −26…−20, a +6 | Sin slots ✓. Parten la pasarela S; cada mitad sigue conectada por su rampa (R1-7) |
| R12-P1 | z −24 | 6 × 4,5 | Vestíbulo | `ArenaShutter` | Ronda 0 / `Completed` | Vestíbulo x −13,6…−6,4, z −28…−24 | ✓, **cuando existan los muros** (R1-6) |
| R14-P1 | z −26 | 10 × 6 | Vestíbulo | `ArenaShutter` | Ronda 0 / `Completed` | Vestíbulo x −5,6…5,6, z −32…−26 | ✓, con muros que no corten la rampa W (R1-6) |
| R15-P1 | z −19 | 10 × 6 | Vestíbulo | `ArenaShutter` | Ronda 0 / `Completed` | Vestíbulo x −15,6…−4,4, z −25…−19 | ✓ (R1-6). Deja un bolsillo de 4,4 m (x −20…−15,6) delante de la jaula |
| R15-PJ (jaula, opcional) | Cara E de la jaula, plano x −20, z −24,5…−19,5 | 5 × 4,5 | Interior de la jaula; 1,2 × 1,0 | `ArenaShutter` en modo «solo abre» | Empieza cerrada (root activo) / se abre con `WaveIndex >= 2`: al empezar la tercera ronda, que es cuando termina la segunda | — (nunca se cierra) | Interior sin slots ✓. Carve mientras está cerrada (isla sin spawns) |
| R16-P1, P2 | Pasarela S (+6), planos x ±7 | 6 × 6 (techo 8 ≥ 7,5) | Caras exteriores | `ArenaShutter` ×2 | Ronda 0 / `Completed` | Rellano x −7…7, z −25…−19, a +6 | ✓. Parten la U de galerías; G5 (galería W) sigue conectada por R-W (R1-7) |
| R17-P1 (solo con E8) | Grada 3 (+3), plano z −12 [propuesta] | 10 × 6 (techo 9 ≥ 7,5) | Cara de la entrada | `ArenaShutter` | Ronda 0 / `Completed` | x −13…−3, z −18…−12, a +3 | Pendiente (R1-12, R3-5) |
| R05-E1 (opcional) | Cara N del depósito, centro (**−12,6**; 0; −9) [propuesta; R1 lo tiene en −14], bisagra en el lado E | Ø 3,6 truncada | — | `HatchDoor` (Puertas) | Abre por proximidad desde la sala (lado U), con el trigger a 2–6 m. **No se cierra** | — | Nadie puede estar dentro antes de abrir. Bate 90° hacia el S, lejos del jugador ([cambio B] §P1.4). Sin NavMeshObstacle: R05 no tiene enemigos ni NavMesh obligatorio. Botiquín fuera del cuarto de círculo (r 4,0) |
| R10-PC1 | z −86 (bahía) | 16 × 8 + cabecera de 2 m; collider de 16 × 10 ([EST] R10) | En la cabecera | `ChaseGateState` + `DoorPresentation` (solo presentación) | Cuando `ChaseCorridorEncounter` la activa: cierre **visual** de 0,35 s, con golpe y `Emit`. La colisión ya es completa | — | El trigger está a ≥ 10 m ([EST] R10) y el jugador ya lo ha cruzado ✓. Sin obstáculo: las emboscadas van por delante |
| R10-PC2 | z +86 | Igual | En la cabecera | Igual | Pre-roll visual (ámbar y sirena) desde z +70 [SUPUESTO]; se abre del todo cuando queda inactiva en `finishZ` (+82) | — | No se cierra |
| R18-B | Banda z −27,5…−24,5 | 14 × 10 | Pistones en la corona de la cara de antecámara ([cambio B] §P1.7) | `BossGateSequence` (Puertas) | **Abre** tras 1,0 s en la plataforma de compromiso (x −3…3, z −40,5…−34,5) → S1–S5 (≈ 6,4 s, §P1.7). **Cierra** con el encuentro empezado, el jugador en los bounds (z ≥ −20,5) y el vano y la antecámara libres. No se reabre [SUPUESTO: el fin de mapa lo da `Map01BossEncounter`] | Antecámara x −8…8, z −47,5…−27,5 | Se hornea con el root inactivo; carve mientras está cerrada (lo está al cargar). La antecámara no tiene slots. El jefe aparece al empezar el encuentro, después de que el jugador cruce |

**Conclusión sobre el camino de NavMesh.**

- Todos los volúmenes sellados son **callejones**: vestíbulos, rellanos, alas, jaula o antecámara.
- Ninguno contiene slots. R-P1 impide cerrar con el jugador o un enemigo dentro.
- Por tanto, **ninguna persiana corta el camino entre los spawns de la ronda y el jugador**.
- Lo único que se corta son recorridos de bucle a +6 (R06, R11 y R16), y solo mientras dura el combate (R1-7).

### Q3. Comprobación de encaje, cuarto a cuarto

| Cuarto | Bandas en el muro | Techo junto a las puertas | Volúmenes libres de puerta | Puertas internas | Resultado |
|---|---|---|---|---|---|
| S00 | Z de 14 en un AABB de 14 (12 interiores) ✓ | 8 ✓ | ✓ (estación en z −66…−62) | — | ✓ |
| R01 | Z S ✓ · A N, x 2,5…9,5 ✓ | 9 / 9 ✓ | ✓ | P de 10 × 6 entre las taquillas ✓ | ✓ |
| R02 | A S a +12 ✓ · Z N ✓ | 9 / 21 ✓ | ✗ G1 y contrapeso (R1-1) | Plano x −8 por cerrar (R1-6) | 2 ajustes |
| R03 | Z S ✓ · A N ✓ | 8 justo / 8 ✓ | ✓ | P de 12 × 6 ✓ | ✓ |
| R04 | A S ✓ · A E ✓ | 9 ✓ | ✓ | P de 6 × 4,5 en un vestíbulo de 8 m ✓ | ✓ |
| R05 | A W ✓ · Z E (z −13…1) ✓ | 8 / 8 justo ✓ | ✓ | La E de 180° no cabe en el depósito de 8 m → batido de 90° ([cambio B]) + R1-9 | R1-8 y R1-9 |
| R06 | Z W a +6 ✓ · Z S ✓ | 8 justo / 14 ✓ | ✗ G1 (R1-2) | 2 P de rellano: jamba exenta y casetes por fuera; parten la entreplanta (R1-7) | 2 ajustes |
| R07 | Z N ✓ · Z S ✓ | 10 ✓ | ✓ | Pórtico y 4 alas ✓ | ✓ |
| R08 | Z N a +4 ✓ · Z E (z −27…−13) ✓ | 10 / 14 ✓ | ✗ BS2 (R1-3) | P del rellano ✓ | 1 ajuste |
| R09 | Z W ✓ · A N ✓ | 8 justo / 8 ✓ | Taquillas a validar (R1-5) | — | Validar |
| R10 | A S ✓ · Z N (túnel de 16 m) ✓ | 10 ✓ | ✓ | 2 P-c: cabecera y tuberías (R1-10) | 1 ajuste |
| R11 | Z S a +6 ✓ · A N ✓ | 8 justo / 14 ✓ | ✓ | 2 P de rellano (R1-7) | Nota |
| R12 | A S ✓ · A N ✓ | 14 ✓ | ✓ | Pórtico sin muros (R1-6) | 1 ajuste |
| R13 | A S ✓ · Z N ✓ | 10 ✓ | ✓ (bombas a 1 m del volumen libre) | — | ✓ |
| R14 | Z S ✓ · Z N (x 11…25) ✓ | 13 ✓ | ✓ | Cerramiento del pórtico por definir (R1-6) | 1 ajuste |
| R15 | Z S ✓ · Z N ✓ | 13 ✓ | ✗ pilar del riel (R1-4) | Pórtico (R1-6); jaula ✓ | 2 ajustes |
| R16 | Z S a +6 ✓ · Z N (x −21…−7) ✓ | 8 justo / 14 ✓ | ✓ | 2 P de rellano (R1-7) | Nota |
| R17 | Z S a +3 ✓ · Z N ✓ | 9 / 12 ✓ | ✓ (mesa a 0,5 m del volumen libre) | Pórtico solo con E8 (R1-12) | ✓ |
| R18 | Z S en una antecámara de 16 m (1 m por lado) ✓ · B de 22 × 14 × 3 ✓ (la galería S empieza en x ±11) | 14 ✓ | ✓ (plataforma a 7–13 m del vano; delantal de 16 × 8) | B con pistones en la corona ([cambio B]) o R1-13 | ✓ |
| Esclusas Z (11) | AABB de 14 × 9 × 15 con la banda de 14 × 7,6 × 1,5 dentro ✓ | Cámara de 8 ≥ 7,6 ✓ | AT-1 y AT-2 ✓ (trigger de 10 × 10) | — | Faltan anclajes (R1-11) |
| Esclusas A (6) | AABB de 8 × 6,5 × 10 con la banda de 7 × 5,8 × 1,0 dentro ✓. El casete llega a 5,8, dentro de la losa (5,5–6,0) ✓ | Cámara de 5,5 ✓ | AT-1 ✓ (C01: trigger de 6 m) | — | Faltan anclajes (R1-11) |

**Lo que no cabe tal como está:**

- La E de 180° en R05. Se resuelve en la Parte A ([cambio B]) más R1-9.
- Cuatro intrusiones en volúmenes libres: R02, R06, R08 y R15.
- Cuatro pórticos sin cerramiento definido: R02, R12, R14 y R15.
- La luz de salida de R05.
- La cabecera de las P-c de R10.

**Ninguna reserva de banda, techo ni cámara falla.**

### Q4. Recuento, coste de F3b y orden de producción

| Clase | Instancias | Dónde | Variantes de prefab |
|---|---|---|---|
| **Z** | **23** (+4 copias con D-U4 c) | S00 + 11 esclusas × 2 | 1 (8 × 6), con dos caras de rótulo |
| **A** | **11** | C01, C03, C04, C09 y C12 (2 por esclusa) y el origen de C11 | 1 (5 × 4,5) |
| **F-A** | **1** | Destino de C11 | Variante de A |
| F-1 / F-2 | 0 | — | **No se producen** |
| **E** | **1** (opcional) | R05 | 1 (90°) |
| **P** | **20** (+1 en R17 con E8): 15 pórticos, 4 alas y 1 jaula opcional | §Q2 | 5 tamaños: 12 × 6 (×1) · 10 × 6 (×5) · 6 × 6 (×6) · 6 × 4,5 (×7) · 5 × 4,5 (×1) |
| **P-c** | **2** | R10 | 1 (16 × 8 + cabecera) |
| **B** | **1** | R18 | 1 |
| **Total** | **59 puertas** | | 10 prefabs y variantes |

**Puertas de esclusa siempre cargadas.** En una sola sesión son 35. Con D-U4 (c) son 11 en S1, 18 en S2 y 10 en S3, lo que alivia el riesgo 6 de §P6.

**Coste de F3b** en unidades relativas (A = 1, §P1.8) [SUPUESTO: 1 unidad ≈ media jornada de un agente sonnet]:

| Paquete | Unidades | Nota |
|---|---|---|
| Kit común: atlas, presets, glifos, partículas y copias de audio | 1,0 | — |
| Código: enganche o Plan B, `DoorPresentation`, `DoorClassProfile`, `AirlockDoorState`, `ArenaShutter`, `HatchDoor`, `BossGateSequence` y `ChaseGateState` | 2,5 | — |
| `DoorAuthoring` y `Data/Doors` | 1,0 | — |
| Tests EditMode y PlayMode | 1,0 | — |
| A (piloto) | 1,0 | — |
| P con 5 tamaños, más P-c | 1,1 | — |
| Z | 2,0 | — |
| F-A | 0,3 | — |
| E | 1,2 | Opcional |
| B | 4,0 | Depende del jefe |
| **Total** | **≈ 15** | ≈ 7–8 jornadas de agente, más la verificación del coordinador. Producir F-1 y F-2 sumaría ≈ 1,0 |

**Orden recomendado.**

0. **En F2, antes del arte:** la lógica de §Q1.1 y §Q2 (`ArenaShutter`, `AirlockDoorState`, `HatchDoor`, `BossGateSequence` y `ChaseGateState` mínimos) con puertas greybox que cumplan el contrato. Así F2 puede probar los soft-locks: pórticos, alas, cortes de sesión y Abort. DU-1 se decide antes de F3b; F2 puede ir con el Plan B.
1. **A** como piloto técnico: contrato, rig de lamas, `DoorAuthoring` y tests (12 instancias contando F-A).
2. **P y P-c**: mismo rig de lamas, 22 instancias.
3. **Z**: el objetivo visual aprobado en F3a, 23 instancias.
4. **F-A**: variante de A.
5. **B**: cuando el usuario haya elegido el jefe.
6. **E**: la última, porque es opcional.

### Q5. Conflictos y peticiones

**Para R1**

1. **R1-1 · R02.** G1 (x −10…−4) y el contrapeso H (x −8…−4, z 14…18) invaden 1 m el volumen libre de salida de la Z (x −5…5, z 16…22). Hay que llevarlos a x ≤ −5.
2. **R1-2 · R06.** G1 (x 14…20, z −24…−18) invade el volumen libre de salida (x 9…19, z −28…−22). Debe quedar en z ≥ −22.
3. **R1-3 · R08.** BS2 (19; −24), de Ø 5, está dentro del volumen libre de salida (x 18…24, z −25…−15). Debe salir a x ≤ 15,5 o z ≥ −12,5, manteniendo ≥ 30 m hasta la entrada y la mitad opuesta.
4. **R1-4 · R15.** El pilar del riel en (0; 19), de 3 × 3, invade el volumen libre de salida (z 19…25). Debe quedar en z ≤ 17,5; por ejemplo, centro en (0; 16).
5. **R1-5 · R09.** Las filas de taquillas del croquis (x −10…0) cruzan el volumen libre de entrada (x −14…−8). Deben empezar en x ≥ −8. [VALIDAR: el croquis es orientativo.]
6. **R1-6 · Cerramiento de pórticos.** El muro del vestíbulo es el vano de la P más 2 × 0,6 m de jambas.
   - R12: no tiene muros; propuesta x −13,6…−6,4.
   - R14: muros en x ±5,6 que no corten la rampa W (z −30…−14).
   - R15: muros en x −15,6 y −4,4; dejan 4,4 m de bolsillo delante de la jaula.
   - R02: la rampa de la galería ocupa 10 m de fondo en el plano x −8. Opciones: P de 6 m más 4 m de muro, o una P de 9 × 4,5 con jambas.
7. **R1-7 · R06, R11 y R16.** Las P de rellano parten el recorrido a +6 durante el combate. Los bucles siguen existiendo por caídas y rampas, y ningún grupo de slots queda aislado (G5 de R16 sigue unido por R-W). Hay que confirmarlo o elegir la alternativa: una **P de nicho** en el hueco del muro, delante de la puerta de destino. Esa alternativa exige ≥ 1,8 m de fondo y tapa los herrajes de la Z por la cara de sala.
8. **R1-8 · R05.** El anclaje «1 en la salida» no puede ir sobre la banda Z: con techo de 8 m solo quedan 0,4 m. Debe ir al lado, en z < −13 o z > 1.
9. **R1-9 · R05.** Mover la E a (−12,6; 0; −9), con la bisagra al E. Así la aleta de 90° queda adosada al muro E del depósito y no deja una ranura de 1,9 m (regla E-1).
10. **R1-10 · R10.** Reservar la cabecera de 2 m (y 8…10) en z −86 y z +86, e interrumpir allí las tuberías del techo (≥ +8).
11. **R1-11 · `C<NN>.json`.** Añadir por cámara 1 anclaje de relleno para R5 y 2 salidas de vapor bajas (§P4, regla 1). C01 solo trae CY1 y CY2.
12. **R1-12 · R17.** Si R3 activa la E8 opcional, reservar un pórtico. Propuesta: P de 10 × 6 en la grada 3, en z −12.
13. **R1-13 · R18** (opcional). Ensanchar la antecámara a 22 m (x −11…11) para que se vea toda la banda B. Si no, los pistones van en la corona, como ya recoge [cambio B].
14. **R1-14 · Vestíbulos de llegada con D-U4 (c).** La `WeaponMountStation` de 4 × 4 va en la franja central de 4 m de la cámara de 12 m, fuera de los volúmenes libres de 4 m de cada puerta, adosada a un muro lateral. Deja 6 m de paso.

**Para R3**

15. **R3-1.** `Map01ShutterController` ([ENC] §A6.3) y `ArenaShutter` son el mismo componente. Debe haber uno solo, en `Scripts/Doors/`, de Puertas. R3 aporta `closeOnWave`/`openOnWave` y el `sealedVolume` de cada persiana en **un solo sitio**. Propuesta: `Data/Doors/R<NN>.json`, con el campo `internalDoors.behaviour` de [EST] §12.1.
16. **R3-2.** Regla del validador (amplía V6 de [ENC] §A8.4): ningún slot ni `FixedPoint` dentro del `sealedVolume` de una P que esté cerrada en esa ronda.
17. **R3-3.** Jaula de R15: «al terminar la ronda 2» equivale a `openOnWave = 2`. Exige que R15 tenga ≥ 3 rondas.
18. **R3-4 · R18.** Si `Map01BossEncounter` no usa `CombatTwoRoomsLab`, tiene que exponer `Started`, `Completed` y los bounds de la arena, porque `BossGateSequence` los lee para cerrar B.
19. **R3-5 · R17.** Con la E8 opcional hace falta un pórtico (R1-12).
20. **R3-6 · R10.** `anticipateZ` de la P-c de salida en `finishZ` − 12 m [SUPUESTO]. Confirmarlo con la holgura de ≈ 11 s.
21. **R3-7 · R07.** Las persianas de las alas no reproducen klaxon: ya lo hace `CorridorAlarm`.

**Para R0**

22. **R0-1.** Decidir DU-1 antes de F3b. F2 puede ir con el Plan B y puertas greybox.
23. **R0-2.** `Map01SessionHandoff` (D-U4 c) debe cumplir el contrato de puerta de §Q1.1 y exponer `Armed`, `Departing` y `PlayerInside`.
24. **R0-3.** HUD: «PREPARANDO SECTOR n» [CF:238,256] vuelve a 1 en cada sesión con D-U4 (c). Por eso los rótulos usan código de zona ([cambio B]). Queda **pendiente**: unificar la numeración exige tocar `CampaignFlow`.
25. **R0-4.** F2 necesita ya la lógica de las puertas con piezas greybox (§Q4, paso 0). F3b solo sustituye la parte visual.
26. **R0-5.** Tests de F2:
    - camino de cada slot al jugador con las P de la ronda cerradas;
    - `ArenaShutter` no cierra con el jugador o un enemigo en el `sealedVolume`;
    - Abort y corte de sesión.
27. **R0-6.** Las Point de puerta cuentan como **transitorias**: ≤ 2 por cuarto (`lightBudget`, [EST] §12.2). En R06, que tiene 8 de 8, y en R18, con un anclaje, la puerta usa emisivos fuera de los transitorios.
28. **R0-7.** F-1 y F-2 no aparecen en Map01: no se producen en F3b.
