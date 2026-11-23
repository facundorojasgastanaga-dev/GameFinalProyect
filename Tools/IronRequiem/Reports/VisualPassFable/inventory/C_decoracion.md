# Inventario C · Decoración y presupuesto por zona

Fecha: 20/11/2026. Subagente INVENTARIO C del Pase visual (Map01 y campaña antigua).
**Solo lectura.** Único archivo escrito: este. No se abrió Unity, Blender ni git que escriba; no se usó red ni Tripo/Meshy.

Marcas: **[VERIFICADO]** comprobado en archivos o escenas; **[DOC]** afirmado por los documentos, no comprobado en código; **sin comprobar** lo que no pude leer o contar.

Abreviaturas de fuentes (rutas bajo `Docs/IronRequiem/`): `MD` = `Map01/MAP01_DESIGN.md`, `ES` = `Map01/01_ESTRUCTURA.md`, `DEC` = `Map01/02_DECORACION.md`, `INV` = `Map01/00_INVENTARIO.md`, `ILU` = `Map01/05_ILUMINACION.md`, `F2` = `Map01/F2_PLAN.md`, `FX` = `MAP_FX_AUDIT_20261117_r2.md`, `CTX` = `PROMPTS_CHATS.md` (bloque "Contexto del mundo", líneas 572–586).

---

## 0. Estado en una frase

**Map01 no tiene decoración construida.** Los 18 cuartos existen como greybox: cada escena tiene un nodo `[Decor]` **vacío** (0 hijos, verificado en las 18 escenas `Assets/IronRequiem/Scenes/Map01_Bunker/Map01_R*.unity`). No existe ninguno de los 22 props nuevos, ni `DecorAuthoring.cs`, ni `Levels/Map01/Data/Decor/`, ni `Art/Environment/Map01/`, ni los scripts Blender `decor_*.py`. Las escenas de campaña antigua tampoco tienen decoración de kit.

---

## 1. Ruta de Map01 en orden de juego

Cotas según `ES` §6 (columna «Cotas»; N0 = 0, N1 = −12, N2 = −18, N3 = −22, N4 = −28, N5 = −34, N6 = −37 m, `ES` l.334). **Todo el recorrido baja**: la cota nunca sube entre cuartos (`ES` l.70). Tema decorativo según `DEC` D3/D4. «Estado» vale para todos los cuartos salvo que se indique: **greybox, `[Decor]` vacío [VERIFICADO]**.

| Ses. | Cuarto | Zona (código) | Función (`ES` §6) | Cota local / nivel | Baja o sube | Tema decorativo (`DEC`) | Landmark (`ES` §6) | Nota de estado |
|---|---|---|---|---|---|---|---|---|
| S1 | R01 Control de acceso | Acceso ACC (N0) | Combate E1 | 0 · entreplanta +3,5 / N0 | Baja (entrada) | Puesto cerrado a toda prisa «ACCESO C-04», papeles, taquillas abiertas, barrera levantada (`DEC` l.230) | Garita con baliza ámbar | Estructura con `booth` tag landmark (`Data/Structure/R01.json`) |
| S1 | R02 Pozo del montacargas | ACC (N0→N1) | Combate vertical E2 | 0 · galería +12→+6 / N1 (−12) | Baja 12 m | Montacargas caído, guías dobladas, cables y cadenas sin collider (`DEC` l.232) | Plataforma estrellada; contrapeso | Regla de profundidad W5 prevista (`DEC` l.385) |
| S1 | R03 Dormitorios | HAB (N1) | Combate E2 + HoverBot | 0 · galerías +3,5 / N1 (−12) | Baja (desde R02) | «Una unidad que vivió allí»: mural de la unidad, camas a medias (`DEC` l.240) | Mural + muro de chapas | Señalado como **riesgo de contexto** (§4 C5) |
| S1 | R04 Comedor | HAB (N1) | Combate E2 + SawBot | 0 · cocina +4,5 / N1 (−12) | Baja | Barricada «NO PASARÁN» con restos de T-07 empalados; comida seca (`DEC` l.240–241) | Barricada 1,2 m | Mismo riesgo C5 |
| S1 | R05 Enfermería | HAB (N1) | **Respiro** E0 | 0 · quirófano +1,2 / N1 | Baja | Enfermería de campaña, torretas desmontadas sobre camillas (`DEC` l.240) | Mesa de operaciones bajo la lámpara de R5 | Sin slots (`ES` §6) |
| S2 | R06 Armería | ARM (N1→N2) | Combate E4 + TUR | 0 · entreplanta +6 (−12) / N2 (−18) | Baja | Armería vaciada; máquinas abrieron las jaulas de fuera a dentro (`DEC` l.249) | Cámara «ARMERÍA 7» | — |
| S2 | R07 Comunicaciones | COM (N2) | **Alarma** E8 | 0 / N2 (−18) | Baja | Último mensaje enviado desde aquí; hojas de turno, auriculares; cables negros nuevos entre racks (`DEC` l.258) | Mástil con radar rojo | 4 P de ala (`ES` §6) |
| S2 | R08 Almacenes | ALM (N2→N3) | Combate E3 + Big Bot | 0 · cubierta +4 / N3 (−22) | Baja | Inventario numerado derrumbado; grúa parada sobre la pila (`DEC` l.267) | Estantería de 8 m volcada | Slots BS1–BS4 |
| S2 | R09 Taquillas | MNT (N3) | **Respiro** E0 | 0 / N3 (−22) | Baja | Taquillas de operarios; esquema pintado de la trituradora «T-9»; hoja de turno y casco (`DEC` l.276) | Esquema pintado 6×3 m | Sin slots |
| S2 | R10 Túnel de la trituradora | MNT (N3) | **Persecución** E7 | 0 / N3 (−22); grietas a −10 | Baja | Rastros de arrastre, marcas de limpieza, «el túnel se limpia solo» (`DEC` l.276) | Trituradora (prefab propio) | Recta de 200 m en +Z (`ES` §6) |
| S2 | R11 Decantadores | AGU (N3→N4) | Combate E6 horda | 0 (−28) · pasarela +6 (−22) / N4 | Baja | Depósitos clarificadores; puente giratorio aún en movimiento (`DEC` l.285) | `IR_ClarifierBridge` (héroe) | — |
| S2 | R12 Colector derrumbado | AGU (N3→N4) | Combate E2/E3 con grieta | 0 · plataforma +5 · foso −12 / N4 (−28) | Baja | Tubo colector caído y cascada (`DEC` l.285) | Tubo B1 + cascada | Solo grieta cubierta por `GapFallRecovery` (`FX` §3) |
| S2 | R13 Sala de bombas | AGU (N4) | **Respiro** E0 | 0 · cabina +3 / N4 (−28) | Baja | Bombas aún en marcha; plano «ESTÁ AQUÍ» en la cabina (`DEC` l.285, 466) | Tres bombas gigantes | Sin slots |
| S3 | R14 Fundición | FUN (N4) | Combate E3 combinaciones | 0 · pasarelas +5 / N4 (−28) | Baja | Horno reabierto por las máquinas para fundir restos de T-07 y humanos (`DEC` l.294) | Horno + 4 coladas | 4 B (Big Bot) |
| S3 | R15 Ensamblaje | ENS (N4) | Combate E2 combinaciones | 0 / N4 (−28) | Baja | Máquinas producen T-07 con herramientas humanas (`DEC` l.303) | Cadena de torsos colgando | Hueco central 6 m (cinta) |
| S3 | R16 Reactor | REA (N4→N5) | **Pico 2** E3+E4 | 0 · galería en U +6 / N5 (−34) | Baja | Máquinas alimentan su producción con el reactor; estructura humana deformada (`DEC` l.312) | Núcleo cian Ø10 | Uno de los 3 cuartos de Release (`DEC` l.486) |
| S3 | R17 Centro de mando | MAN (N5→N6) | **Respiro** E0 | 0 · gradas +1/+2/+3 / N6 (−37) | Baja | Último mando «SELLAR BÓVEDA»; silencio, taza, abrigos (`DEC` l.321) | Mesa táctica con holograma | Sin slots (o 8/12 opcional) |
| S4 | R18 Bóveda | BOV (N6) | **Jefe** E9 | 0 · galería +6 / N6 (−37) | Baja (fin de mapa) | «Contiene lo que el búnker protegía» (relato abierto) (`DEC` l.329) | Puerta B; estructura central [DEPENDE DEL JEFE] | **Salida: «—» (fin de mapa)**, `ES` l.503 |

Sesiones: S1 = R01–R05, S2 = R06–R13, S3 = R14–R17, S4 = R18 (`F2` l.8, `MD` U2 aprobado). Las escenas de sesión `Map01_Campaign_S1..S4.unity` **no** tienen nodo `[Decor]` [VERIFICADO].

**Lectura de la tabla:** la cota decrece en todo el mapa y no hay ninguna zona de superficie; la salida del mapa es la Bóveda a −37 m.

---

## 2. Kits y props: qué existe como archivo

| Kit | Ruta | Contenido verificado | Uso en decoración según `DEC` |
|---|---|---|---|
| Materiales Bunker | `Assets/IronRequiem/Art/Environment/Bunker/*.mat` | 10 materiales: `Bunker_Ceiling`, `_Floor`, `_Grating`, `_Hazard`, `_RustMetal`, `_Wall`, `_SuitSleeve`, `_LampAmber`, `_LampCold`, `_LampRed`; más `BunkerNavMesh.asset` [VERIFICADO] | Usables: Floor, Wall, Ceiling, RustMetal, Grating, Hazard. **Lamp\*: no** (`DEC` D1.3) |
| Texturas Bunker | `Art/Environment/Bunker/Textures/` | 5 juegos Albedo+Normal 512 px: hormigón suelo/pared, rejilla, peligro, óxido [VERIFICADO; `BUNKER_POLISH.md` §5] | Base del kit |
| Mallas Bunker | `Art/Environment/Bunker/Meshes/BunkerBox_0..225.asset` | 226 cubos (12 tris c/u) [VERIFICADO por conteo; `INV` A1] | Bloques, no props |
| Scrap | `Art/Environment/Scrap/` (3 FBX + HS) y `Assets/IronRequiem/Prefabs/Scrap/*.prefab` (3) | Existen [VERIFICADO] | **No se usan** (`DEC` D-D2). Ya los usa `HG_ScrapRoute` del hangar (zona protegida; 92 instancias, `Reports/Map01/Hangar/route_build.txt` l.47) |
| Kit VisualOverhaul (mallas) | `Art/VisualOverhaul/Meshes/` | `VO_PipeRun2m`, `VO_CableBundle4m`, `VO_WallVent`, `VO_CeilingLight(+Housing,+Lens)`, `VO_EmergencyLight`, `VO_StatusSlit(Tall)`, `VO_JointPlate_L/R` [VERIFICADO] | `VO_PipeRun2m`, `VO_CableBundle4m`, `VO_WallVent` sí (`DEC` D1.3). Luminarias: no (son de R5) |
| Kit VisualOverhaul (materiales) | `Art/VisualOverhaul/Materials/` | `VO_Prop_Pipe`, `VO_Prop_Housing`, `VO_Prop_Cable`, `VO_Decal_Burn/Hazard/Rust/Stain` (+ texturas) [VERIFICADO]; `VO_Env_*` son de Estructura; `VO_Light_*` no | Decales (`DEC` D1.3) |
| Props IR_* de decoración (22 imprescindibles + 10 deseables de `DEC` D5) | Buscado en `Assets/` por nombre | **Ninguno existe**: no hay `IR_Crate`, `IR_Locker`, `IR_Bunk`, `IR_Terminal`, `IR_RackServer`, `IR_PipeKit`, `IR_Duct`, `IR_Barrier`, `IR_Rubble`, `IR_CeilingBeam`, `IR_Wreck*`, `IR_Container`, `IR_Table`, `IR_SignQuad/Blade`, `IR_ClarifierBridge`, `IR_AntennaMast`, `IR_PumpHuge`, `IR_TacticalTable`, `IR_ReactorCoreShell`, `IR_FurnaceFront` [VERIFICADO] | Solo en papel (`DEC` D5). Los `IR_*` que sí existen son de enemigos, jugador, menú, puertas y MountStation, sin relación |
| Materiales de props `Decor_*` | — | `Decor_Props_<ZONA>` (13), `Decor_Signage`, `Decor_Puddle`: **no existen** [VERIFICADO] | Solo en papel (`DEC` D1.3) |
| Carpeta de salida | `Assets/IronRequiem/Art/Environment/Map01/` | **No existe** [VERIFICADO] | — |
| Prefabs Map01 | `Assets/IronRequiem/Prefabs/Map01/Enemies/` | Solo `BigBot_Map01`, `Carronero_Map01`, `T07_Mech_Map01` (enemigos) [VERIFICADO] | Sin decoración |
| Datos Map01 | `Assets/IronRequiem/Levels/Map01/Data/` | `Doors`, `Encounters`, `Hangar`, `Lighting`, `Structure` (72 archivos). **Sin** `Decor/` [VERIFICADO] | `DEC` D7.1 lo propone |
| Autoría | `Assets/IronRequiem/Editor/Map01/` | `StructureAuthoring`, `Map01Core` (define la capa `[Decor]`), `HangarRouteAuthoring`, etc. **Sin** `DecorAuthoring.cs` [VERIFICADO] | `DEC` D7.2 lo propone |
| Blender | `Tools/IronRequiem/Blender/` | Sin `decor_lib.py`, `decor_atlas.py`, `decor_build_*.py` ni `decor_verify.py` [VERIFICADO] | `DEC` D5.1 lo propone |

**Huecos del kit** (`INV` A7, l.396–404): literas, taquillas, camillas, terminales, válvulas, barricadas, barriles, cajas, agua y charcos. `DEC` cubre casi todos con los 22 props, pero ninguno existe.

**Qué hay de decoración en las escenas Map01 hoy:** solo bloques de `[Structure]` con colisión. Nombres que parecen props son **bloques estructurales** con tipo y material (`Data/Structure/R01.json`): `booth` (sólido 6×3×6, `Bunker_Wall`, tag landmark), `sandbags` (`coverHigh` 4×2,4×2, `Bunker_RustMetal`), `lockersW`/`lockersE` (sólido 8×9×6). Son cobertura de R1, no decoración [VERIFICADO en JSON]. Los materiales Structure se asignan por nombre (`StructureAuthoring.cs` l.535–540); en las escenas se referencian por GUID, así que **no pude confirmar por nombre** qué material lleva cada bloque (sin comprobar).

---

## 3. Presupuesto por arena y datos medidos

### 3.1 Límites de `DEC` D6.1 y valores de `DEC` D4.1 (papel, no medido)

| Cuarto | Tipo | Props | DC decor | SetPass | Tris decor (k) | Límite del tipo (DC / SetPass / k tris) | ¿Cumple en papel? | Partículas (sist./vivas) |
|---|---|---|---|---|---|---|---|---|
| R01 | Arena | 70 | 40 | 9 | 45 | ≤60 / ≤10 / ≤80 | Sí | 1 / 40 |
| R02 | Arena | 85 | 50 | 10 | 60 | ≤60 / ≤10 / ≤80 | Sí (SetPass al límite) | 2 / 60 |
| R03 | Arena | 140 | 55 | 9 | 70 | ≤60 / ≤10 / ≤80 | Sí | 1 / 30 |
| R04 | Arena | 130 | 60 | 10 | 75 | ≤60 / ≤10 / ≤80 | Sí (al límite) | 2 / 50 |
| R05 | Respiro | 110 | 55 | 9 | 70 | ≤70 / ≤12 / ≤100 | Sí | 1 / 30 |
| R06 | Arena | 120 | 60 | 10 | 75 | ≤60 / ≤10 / ≤80 | Sí (al límite) | 1 / 30 |
| R07 | Arena | 120 | 60 | 10 | 75 | ≤60 / ≤10 / ≤80 | Sí (al límite) | 2 / 50 |
| R08 | Arena | 110 | 55 | 9 | 70 | ≤60 / ≤10 / ≤80 | Sí | 1 / 30 |
| R09 | Respiro | 90 | 45 | 9 | 50 | ≤70 / ≤12 / ≤100 | Sí | 1 / 30 |
| R10 | Persecución | 120 | 40 | 8 | 60 | ≤45 / ≤9 / ≤60 | Sí (al límite de tris) | 3 / 60 |
| R11 | Arena | 90 | 50 | 9 | 65 | ≤60 / ≤10 / ≤80 | Sí | 3 / 60 |
| R12 | Arena | 100 | 55 | 10 | 65 | ≤60 / ≤10 / ≤80 | Sí | 3 / 70 |
| R13 | Respiro | 100 | 55 | 10 | 70 | ≤70 / ≤12 / ≤100 | Sí | 2 / 40 |
| R14 | Arena | 110 | 60 | 10 | 80 | ≤60 / ≤10 / ≤80 | Sí (al límite) | 2 / 60 |
| R15 | Excepción | 120 | 60 | 10 | 90 | ≤60 / ≤10 / ≤100 | Sí | 1 / 30 |
| R16 | Arena | 100 | 55 | 10 | 70 | ≤60 / ≤10 / ≤80 | Sí | 2 / 50 |
| R17 | Respiro | 100 | 55 | 9 | 70 | ≤70 / ≤12 / ≤100 | Sí | 1 / 20 |
| R18 | Jefe | 60 | 45 | 8 | 55 | ≤50 / ≤9 / ≤60 | Sí | 1 / 30 |

Fuente de los valores: `DEC` l.344–363 (papel). Límites: `DEC` l.571–577 y D6.1 (l.569).

**Puntos a vigilar (sin comprobar):**
- R03 tiene **140 props con 55 DC**: solo cuadra si la mayoría se combina por material y se hornea estático. Sin medir.
- `DEC` D4.1 indica 8–10 «materiales visibles» por cuarto, mientras D6.2 habla de ≤ 2 materiales de props visibles. Son magnitudes distintas (incluyen kit y decales) y no están aclaradas.
- Las cifras de `DEC` son estimaciones de papel [SUPUESTO] (`DEC` l.342).

### 3.2 Datos medidos disponibles

| Dato | Fuente | Valor | Relevancia |
|---|---|---|---|
| Decoración Map01 medida | `MD` l.251 | «Ninguna cifra de rendimiento de Map01 está medida» | No hay base para decoración |
| Navmesh por cuarto (triángulos, no render) | `Reports/Map01/F2/navmesh/index.txt` | R01 112 … R18 384 | No sirve como coste de render |
| Hangar `HG_ScrapRoute` | `Reports/Map01/Hangar/route_build.txt` l.29, 47 | 336 renderers, 92 instancias de chatarra, 0 errores de validación; tris por grupo en l.49–60 y 98–101 | Zona protegida |
| +515 k triángulos sin LOD | `CHECKLIST_TRABAJO.md` l.1472 | Cita el documento, **no** aparece sumada en `route_build.txt` | **Sin comprobar** (no sumé los grupos) |
| Campaña, Room01, Editor (08/11) | `VISUAL_AUDIT_F0.md` tabla Play | Antes de ronda 342 DC y 11,7 k tris; ronda 1: 341–346 DC, 96–164 k tris; pico 300 DC, 181 k tris, CPU 50,3 ms, GPU < 2 ms | Es de escena completa, no de decoración. Indica que el juego va **limitado por CPU**, como ya señala `DEC` D6 |

---

## 4. Contradicciones entre los documentos y el CONTEXTO DEL MUNDO

Enumeradas para decisión del usuario. No propongo cambios; para cada una: qué cambiaría si mandara el contexto y qué opción mínima existe.

| # | Tema | Documento (cita) | Contexto (cita) | Qué cambiaría si mandara el contexto | Opción mínima |
|---|---|---|---|---|---|
| **C1** | **Dirección** | `MD` l.20: «El búnker baja de 0 a −37 m»; `ES` l.70 y l.334: N0 = 0 … N6 = −37; `ES` l.203: hangar en «Superficie, z −205…−70» | `CTX` l.575–576: «despierta en las profundidades» y «el mapa es una subida» | Invertir cotas, orden de sesiones y esclusas: mueve geometría de todo el mapa y el hangar (zona protegida) | **Ninguna sin mover geometría.** La cota se mantiene en las 18 escenas y datos. Solo cabe decoración de contraste en el tramo final |
| **C2** | **Orden temático de zonas** | `MD` l.21 y `ES` §3 (filas 1–12): Acceso → Habitáculo → Armería → Comunicaciones → Almacenes → Túneles → Agua → Fundición → Ensamblaje → Reactor → Mando → Bóveda | `CTX` l.578–580: abajo laboratorios → medio infraestructura → arriba exterior | Reasignar la temática de cada zona a su cota. El orden de cotas no cambia | **Re-tematizar** las zonas de cota baja (R16–R18) como laboratorio y las de cota alta (R01–R02) con señales exteriores. No mueve geometría, pero invierte la lectura |
| **C3** | **Zona de laboratorios** | No existe zona de laboratorio en `MD`, `ES` ni `DEC`. Lo más cercano es la cápsula de la zona protegida (`Room00_Hangar`) | `CTX` l.578: «abajo, laboratorios y zonas de experimentación (cápsulas, bancos de pruebas, registros)» | Añadir o cambiar cuartos de cota baja | Re-tematizar R17 (respiro) con cápsulas y registros. **Requiere props nuevos** no listados en `DEC` D5 (cápsula, banco de pruebas) |
| **C4** | **Densidad de máquinas y de destrucción** | `DEC` l.35: «Cuanto más abajo, más tomado por las máquinas»; l.79: M0 en R01–R05, M3 en R15–R18; `DEC` l.74–77: D3 «tomado» en R10, R12, R16 (profundo) y D0/D1 arriba | `CTX` l.580: abajo tecnología intacta pero muerta; en medio infraestructura «cada vez más dañada y tomada por los robots» | Invertir M0–M3 y D0–D3 con la profundidad | **Solo tablas de niveles** (`DEC` D1.2 y l.79): cambia qué se pinta, no la geometría. Coste: re-dressing de los 18 cuartos |
| **C5** | **Zonas de vida humana** | `DEC` l.240–241: R03 «una unidad que vivió allí», «camas hechas a medias», R04 «comida seca en mesas», R05 enfermería con camillas; `ES` l.760: barricada «NO PASARÁN» de «antiguos ocupantes»; nombres Dormitorios, Comedor, Enfermería también en `PROMPTS_CHATS.md` l.304 (propuesta del usuario, «ajustable») | `CTX` l.583: «la decoración nunca debe parecer un refugio humano habitado. Los humanos solo aparecen como rastro» | Quitar la idea de habitabilidad: camas desordenadas, sin comida, sin uso | **Re-tematizar sin mover geometría:** conservar nombres y el mural/barricada como rastro, quitar camas hechas y comida. El emblema ya es rastro (`DEC` l.66) |
| **C6** | **Final en la Bóveda con jefe** | `MD` l.28 (R18 con jefe), `ES` l.503 «Salida: — (fin de mapa)», `MD` l.248 «No hay evento de fin de campaña». Jefe «El Custodio» aprobado (`F2` l.8) | `CTX` l.579–580 y l.577: arriba «hasta la salida», «más luz real a medida que subimos» | Añadir un tramo de ascenso y salida al exterior (cambia estructura) | **Sin geometría:** re-tematizar la galería de R18 con grieta de luz natural en el techo y rótulo de salida. Requiere a R5 para la luz (la decoración no pone luces, `DEC` D1.8) y no cambia el hecho de que la salida está a −37 m |
| **C7** | **Luz natural y señales del exterior** | `DEC` no contiene «luz natural», «exterior» ni «cielo» (grep = 0). `ILU` sin comprobar en su totalidad. `ES` §3 no menciona luz natural (lectura de la tabla) | `CTX` l.579–580: «señales del exterior», «luz natural fría que se filtra», «más luz real a medida que subimos» | Diseñar la progresión de luz y señales hacia arriba | **Re-tematizar** R01–R02 (cota baja del tramo ascendente en el contexto) con señales de exterior. Luz: decisión de R5. Ver C2 |
| **C8** | **Colores reservados** | `DEC` l.160 y `ILU` l.169: rojo, ámbar y verde reservados a ≤ 12 m de puertas (estado de `PU`). Pero las paletas de zona usan rojo (`DEC` l.175 ARM, l.178 MNT), ámbar (`DEC` l.182 REA, `ES` l.204 «Ámbar de sodio» en Acceso), verde (`DEC` l.176 COM, solo pantallas) | `CTX` l.589: «Quiero luz roja en los ojos de los robots» | No hay contradicción directa: la regla es por distancia a puerta. Sí hay tensión de lectura | **Mantener la regla de distancia** (ya en `DEC` D1.8 y V-11). Sin cambio de documento: solo confirmar con el usuario si el rojo de ojos (enemigos) puede coincidir en color con el estado de puerta |
| **C9** | **Emisivos y luz de decoración** | `DEC` l.41, l.161: «Ni una luz»; emisivo ≤ 1,5 HDR | `CTX` l.581: «un poco más de luz real a medida que subimos» | Permitir más emisivo o luces hacia arriba | **Decisión de R5**; decoración no pone luces (`DEC` D1.8) |
| **C10** | **Jefe y arena al final** | `MD` l.28 y l.248; `F2` l.8 | `CTX`: no menciona jefe | Sin cambio si el jefe no es contradicción del contexto; sí condiciona C6 | Decisión del usuario, no cambio de decoración |

---

## 5. Por zona: repetición, huecos, cobertura y marcas

Datos de cobertura y slots de `ES` §6 (Cap./slots) y `DEC` D4.2. «Marcas robot» y «rastros humanos» son las previstas en `DEC` D3; **ninguna está construida**.

| Zona (cuartos) | Qué se repite (previsto) | Vacío o poco dressed | Qué puede tapar el combate | Marcas de ocupación robot (previstas) | Rastros humanos (previstos) |
|---|---|---|---|---|---|
| **ACC** (R01–R02) | `IR_Crate` bajas, `IR_Locker`; cables y cadenas | Centro de R01 (suelo limpio); pozo de R02 bajo la plataforma | R01: saco alto x −12…−8 (2 m, piel); cajas L 1,2 m; slots G1–G3 (6 cap./10 slots). R02: 8/12 slots; carriles W/E; salida Z libre | Cables sobre +11 m sin collider; contrapeso caído (`DEC` l.382) | Papeles, taquillas abiertas, barrera levantada (`DEC` l.230) |
| **HAB** (R03–R05) | `IR_Bunk` (naves), `IR_Locker` row, camillas, biombos | R05 (respiro, sin slots); vanos de 6 m de R03 | R03: taquillas H1/H2 3×6×2,4 (piel); baúles 1,2 m. R04: mesas 1,1 m; barricada 1,2 m en dos tramos con vano de 6 m. Slots 8/12 (R03, R04) | Restos de T-07 empalados en la barricada (`DEC` l.240, 399) | Mural de la unidad, chapas, camas a medias, comida seca, camillas (riesgo C5) |
| **ARM** (R06) | `IR_CageGrate`, cajas de munición, taquillas tumbadas | Suelo de la nave (densidad baja) | H1–H3 (piel), cajas 1,2 m; nido TUR (20; 23); cámara acorazada (alta densidad); 8/12 slots + 1 TUR | **Jaulas abiertas de fuera a dentro** (`DEC` l.249) | Estarcido rojo, «ARMERÍA 7», armas viejas tras rejas (`DEC` l.249–251) |
| **COM** (R07) | `IR_RackServer` (4 racks de 6 m en las alas), consolas del anillo | Sala central (densidad baja); vanos de las P de ala | Racks 6×1,2×2,4 (cobertura alta); consolas 1,1 m; 12/18 slots; 4 P de ala (grupos desactivables) | Cables negros nuevos entre racks (`DEC` l.258) | Hojas de turno, auriculares, silla caída, último mensaje |
| **ALM** (R08) | `IR_Container` 6 m, palés, barriles, `IR_Shelf` (deseable) | Anillo ≥ 6 m y rampas de 5 m (`DEC` l.269) | Pilares 3×3×8 de R1 vestidos; palés 1,2 m; 12/18 slots + 4 B (Big Bot Ø5) | Sin marca robot específica (solo la grúa parada y la pila) | Inventario numerado derrumbado (`DEC` l.267) |
| **MNT** (R09–R10) | Pilastras cada 10 m en R10, tuberías, cajas eléctricas | R10 entero salvo pilastras; R09 sin slots | R10: 3 emboscadas × 2; grietas; sin cobertura decorativa | Restos de robots de limpieza y cables en R10 (`DEC` l.274) | Esquema T-9, hoja de turno, casco (R09) |
| **AGU** (R11–R13) | Bombas, tuberías, válvulas de volante | Embudos de R11 (nada); borde de salto de R12 | R11: bombas 1,2 m en (±14; ±14) (deseable), anillo ≥ 8 m, 12/18 slots. R12: casetas H 6×6×3, 4×4 como piel; 12/18 + 2 B. R13: 3 bombas (piel) | Puente giratorio y bombas en marcha (`DEC` l.285–286) | Plano «ESTÁ AQUÍ» en R13 (`DEC` l.466), válvulas con volante |
| **FUN** (R14) | Crisoles, rieles, cadenas, moldes, barriles | Anillo ≥ 6 m | Crisol 10×10×6 (piel); pilares 3×3; 12/18 + 4 B | Horno reabierto para fundir restos de T-07 (`DEC` l.294) | Restos de humanos fundidos (`DEC` l.294): **sensible, revisar con el usuario** |
| **ENS** (R15) | `IR_Wreck_Torso` ~20 en instancing (torsos colgando ≥ +6) | Hueco central de 6 m (cinta) | Bancos 4×1,6×10 (piel); plantillas 3×3×3; cinta 1,1 m; 12/18 slots | Torsos de T-07 colgando; producción automática (`DEC` l.303–304) | Herramientas humanas en bancos (`DEC` l.303) |
| **REA** (R16) | `RingCover` (4), torres de refrigeración (2), tuberías gruesas | Anillo ≥ 6 m | RingCover (piel hazard); torres adosadas; 12/18 + 4 B + 1 TUR | Máquinas alimentan producción con el reactor (`DEC` l.312) | Estructura humana deformada (`DEC` l.312) |
| **MAN** (R17) | Consolas de gradas, sillas, pantallas | Pasillos de escalera x −14, 0, 14 | Mesa táctica (cobertura); gradas. Sin slots (o 8/12 opcional) | Pantallas con silueta de la Bóveda (`DEC` l.319) | Nota «SELLAR BÓVEDA», taza, abrigos (`DEC` l.321) |
| **BOV** (R18) | Cajas, cables gruesos en la galería | Centro de la arena (no se decora); antecámara sin decoración más allá de marcos | Plataforma de compromiso 6×6; volumen B; slots: jefe + 4 grupos × 3 | Estructura central, pendiente del jefe [DEPENDE DEL JEFE] | «Contiene lo que el búnker protegía» (sin rastro definido) |
| **S00** (vestíbulo) | Estarcido de zona y terminales en muros | PlayerStart y `WeaponMountStation` 4×4 | Ninguna | — | Estarcido de zona |

---

## 6. Campaña antigua (escenas `Assets/IronRequiem/Scenes/Campaign/`)

Datos de `FX` §3 (l.51–69) y verificación en YAML: **ninguna de las seis escenas tiene `VO_Dressing`, `VO_Props` ni `VO_Decals`, ni referencias a `Scrap_` o `Bunker_` por nombre** [VERIFICADO por grep]. La decoración de campaña es greybox con elementos de peligro.

| Sala | Lectura y recorrido (FX §3) | Combate | Defectos confirmados (FX §3) | Decoración real | Destino en Map01 (`ES` §10) |
|---|---|---|---|---|---|
| Room00b_AlarmCorridor | Lineal; pantallas `AlertScreen` (z −49,5) tapadas por pilastras de z −50 | Pasillo estrecho, 3+2 enemigos | Texto de pantallas cortado; riesgo de superar 4 luces por objeto (AlarmRed ×8) | Ninguna del kit | Se **reemplaza** (`MD` U6) |
| Room01_Foundry | **Falsa salida**: 4 `MoltenRun` enmarcan un panel de peligro de 12 m; los chevrones apuntan ahí | Amplia, con coberturas en rejilla | `SouthWall` atraviesa la estación de montaje (confirmado en Play) | Ninguna; panel de peligro como señal falsa | Deriva a **R14** (sin repetir la falsa salida; `ES` LV-5) |
| Room02_Breach | Salida visible sobre la grieta; banco cercano seguro | 14 enemigos en 48×22 m | Franja sur sin collider en 100 de 135 puntos; `GapFallRecovery` solo cubre la grieta | Ninguna | Se **reemplaza**; la grieta pasa a **R12** |
| Room03_Assembly | Marco luminoso de salida (bien) | Rampas; p99 212–224 ms en horda (Editor) | — | Ninguna | Deriva a **R15** |
| Room04_Reactor | Salida poco guiada detrás del núcleo | Rampas laterales | `CeilingSupport` cruza `Ramp_V35` y parte la rampa (1,05 y 2,55 m) | Ninguna | Deriva a **R16** |
| Pursuit05 | Persecución lineal | `SideCover` deja ranuras estrechas | Muro ciego al final (`ExitWall`, z 385,5); sin trigger de fin | Ninguna | Deriva a **R10** |

Riesgo para el pase visual: `VISUAL_AUDIT_F0.md` (defectos 4 y 5) ya indica que la campaña se lee como «graybox iluminado de forma plana» y sin decoración.

---

## 7. Sin comprobar

- Material por nombre de cada bloque de `[Structure]` en las escenas Map01 (las referencias son por GUID). Sin comprobar.
- `ILU` (`05_ILUMINACION.md`) solo leído en cabeceras, paleta parcial y reglas de puerta; la tabla completa de §L2 no se leyó.
- Coste real de Tripo: sin red, no comprobable. `DEC` D5.4 dice que no se usa.
- Suma de triángulos de `HG_ScrapRoute` (+515 k según `CHECKLIST_TRABAJO.md` l.1472): no verificada.
- Si R03 cabe en 55 DC con 140 props: depende del batching que no está escrito.
- `DEC` cita `VisualOverhaulDressing.cs:172` para `ignoreFromBuild`. El cambio está en la **línea 176** (`Assets/IronRequiem/Editor/VisualOverhaul/VisualOverhaulDressing.cs:176`); `:75` para las flags de estáticos sí coincide. El namespace `Unity.AI.Navigation.NavMeshModifier` **sí** se usa en ese archivo, así que la duda de namespace de `DEC` D1.4 queda resuelta por la misma referencia del proyecto [VERIFICADO].
- No se han visto las escenas de decoración de las salas de campaña en Play. Todo lo de estado visual es de documentos.

---

## 8. Resumen

1. Map01 no tiene decoración construida: los 18 cuartos son greybox y el `[Decor]` de cada escena está vacío (verificado).
2. No existe ninguno de los 22 props imprescindibles ni los 10 deseables, ni `DecorAuthoring.cs`, ni `Data/Decor/`, ni `Art/Environment/Map01/`, ni los scripts Blender de decoración.
3. Kits que sí existen: 10 materiales `Bunker_*` (sin los Lamp*), 5 juegos de textura, 226 `BunkerBox`, `VO_PipeRun2m`, `VO_CableBundle4m`, `VO_WallVent`, decales `VO_Decal_*` y 3 Scrap (estos últimos excluidos).
4. La ruta baja de N0 (0 m) a N6 (−37 m) en 18 etapas; la salida del mapa es la Bóveda R18 (fin de mapa), no hay ninguna zona de superficie.
5. Presupuesto de papel por arena: ≤ 60 DC, ≤ 10 SetPass, ≤ 80 k tris; los 18 cuartos cumplen en papel. Ningún dato de decoración de Map01 está medido.
6. El único número medido cercano es de Editor en la campaña antigua (pico de 300 DC y 181 k tris en Room01); el juego está limitado por CPU.
7. Contradicciones con el contexto: dirección (C1, la más grave, afecta a geometría y al hangar protegido), orden temático de zonas (C2), falta de zona de laboratorio (C3), densidad de máquinas invertida (C4).
8. Vida humana: Dormitorios, Comedor y Enfermería con camas, comida y camillas chocan con «nunca un refugio habitado» (C5), aunque el propio usuario propuso esos nombres.
9. Final: la Bóveda con jefe y sin salida al exterior choca con «hasta la salida» (C6); la opción mínima es re-tematizar sin mover geometría.
10. Colores reservados (C8): la regla rojo/ámbar/verde solo aplica a ≤ 12 m de puertas, no es contradicción directa con el contexto.
11. Por zona: los huecos de combate y las coberturas están definidos (ES §6, DEC D4); las marcas robot y los rastros humanos están previstos pero no construidos.
12. Campaña antigua: sin decoración de kit en las seis escenas; Room01 tiene una falsa salida (panel de peligro) que no debe repetirse.
13. Sin comprobar: materiales por nombre en Structure, la tabla completa de ILU §L2, el total de +515 k de la chatarrería y el batching de R03.
14. Decisiones que necesito del usuario: C1 (dirección), C6 (final), C5 (vida humana) y el tramo de sensibilidad de FUN (restos humanos fundidos).
