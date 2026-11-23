# MAPA 1 · 02 · Decoración (R2)

**Estado:** F1, guía y plan en papel. Autor: R2 (sonnet), 18/11/2026. No se ha construido ni modelado nada.

**Alcance:** solo lectura del proyecto; único archivo escrito: este. No se abrió Unity ni Blender, no se usó la CLI ni git que escriba, ni Tripo, Meshy, Composio o la red.

**Fuentes** (abreviaturas usadas en las tablas):

| Clave | Archivo |
|---|---|
| [MD] | `Docs/IronRequiem/Map01/MAP01_DESIGN.md` §3–§5 |
| [ES] | `01_ESTRUCTURA.md` (§2 métricas, §3 zonas, §6 tabla, §7 fichas, §12 datos) |
| [INV] | `00_INVENTARIO.md` (§4 kit, §5 presupuesto, anexo A) |
| [ENC] | `03_ENCUENTROS.md` (S6 volumen de slot) |
| [PU] | `04_PUERTAS.md` (§P2.3 señalización, §P4 reglas) |
| [VO] | `VISUAL_OVERHAUL.md` §3 |
| [AUD] | `VISUAL_AUDIT_F0.md` |
| [BP] | `BUNKER_POLISH.md` §5 |
| [AG] | `AGENTS.md` |

**Marcas:**

| Marca | Significado |
|---|---|
| **[DECISIÓN USUARIO]** | Lo decide el usuario en la PARADA de F1, con mi recomendación |
| **[SUPUESTO]** | Cifra o hecho no verificado en el proyecto |
| **[VALIDAR F2/F3]** | Se comprueba con el validador, el Frame Debugger o una prueba en Play |
| **[PARA R1]** `[PARA R4]` `[PARA R5]` `[PARA R3]` `[PARA R0]` | Petición o coordinación con ese rol |

---

## D0. Resumen y decisiones

1. **Principio rector:** *suelo despejado, perímetro y altura vestidos.* En las arenas el centro del suelo queda limpio (armas caídas, slots, carriles); el relato vive en los muros, las alcobas, las entreplantas, los techos (≥ +4,5 m) y los respiros. Los respiros (R05, R09, R13, R17) llevan la densidad narrativa más alta porque no hay combate.
2. **Lenguaje visual:** antiguos ocupantes (pintura plana militar, estarcido blanco, objetos de uso, reparaciones improvisadas) frente a máquinas (chapa soldada sin pintar, cables negros anclados, injertos de restos de robot). Cuanto más abajo, más «tomado por las máquinas». Cuatro niveles de destrucción D0–D3 (D1).
3. **Reutilizo primero el kit** libre: `Bunker_*.mat`, `VO_PipeRun2m`, `VO_CableBundle4m`, `VO_WallVent`, `VO_Prop_*`, `VO_Decal_*` y los `BunkerBox_N` ([INV] §4). La lista roja solo se usa o se copia. Los Scrap (solo Room00) **no se usan**: se sustituyen por restos modelados propios.
4. **Props nuevos:** **16 familias modulares + 6 héroes (landmarks) = 22 imprescindibles** (≈ 45 mallas LOD0), por scripts de Blender reproducibles. 10 deseables. Ninguna pieza exige Tripo (D5.4).
5. **Materiales:** **1 material de props por zona** (13 copias de un mismo atlas, con `_DirtFloorY` propio) más 1 atlas de señalética y los decales del kit. Pocos materiales = pocos SetPass.
6. **Orientación:** código de sector pintado (`HAB-03`), nivel y profundidad, flechas hacia la salida con el color de la zona destino, rótulo colgante sobre cada salida y plano «ESTÁ AQUÍ» en los respiros (D2).
7. **Rendimiento:** decoración ≤ 60 draw calls y ≤ 10 SetPass por arena (≤ 70 y ≤ 12 en respiros), ≤ 80 k triángulos visibles, ≤ 24 decales, partículas ambientales ≤ 5 sistemas (D6).
8. **Ni una luz:** solo superficies emisivas bajas (≤ 1,5 HDR) en pantallas y LED. Los emisivos de decoración no usan rojo, verde ni ámbar cerca de las puertas, para no confundirse con su estado (D1.8).

### [DECISIÓN USUARIO]

| # | Decisión | Opciones | Recomendación de R2 |
|---|---|---|---|
| **D-D1** | Marcas planas de orientación (≤ 1 cm, sin collider) **dentro** de los volúmenes libres de ruta crítica y aproximación a puertas. El contrato [MD] §5.5 dice «no se decora» en ellos | (a) Permitirlas solo como tipo `routeMark` (flechas y líneas guía). (b) Prohibirlas y poner solo flechas en muros | **(a).** No afectan a colisión, NavMesh ni spawns, y son la ayuda de orientación más barata. Requiere que [PARA R0] amplíe §5.5 con esa excepción |
| **D-D2** | Piezas de la lista roja: ¿usar los Scrap de Room00 como restos de robot? | (a) No: restos propios (`IR_Wreck_*`). (b) Copiar los FBX | **(a).** Los Scrap no tienen colisión, sus triángulos están sin verificar y llevan material HardSurface 4096². Los restos propios ocupan menos y se parecen a los robots del juego |
| **D-D3** | Tripo para alguna pieza | (a) Ninguna. (b) 1 hulk de Big Bot destruido (R08/R16) | **(a).** Todo lo del plan se modela con primitivas y lathe. Si se elige (b), el coste lo indicaré antes de cada generación y no se genera sin tu OK |
| **D-D4** | Material de props con shader `IronRequiem/HardSurface` (suciedad y desgaste de aristas) frente a URP Lit | (a) HardSurface en copias propias. (b) URP Lit con suciedad horneada | **(a).** El shader no se modifica (lista roja), tiene instancing y da `_DIRT` y `_EDGEWEAR`. Falta verificar compatibilidad con SRP Batcher [VALIDAR F3] |
| **D-D5** | Narrativa ambiental (D3): usar el relato propuesto («Unidad Centinela», «limpieza T-9»…) | Sí / No / ajustar | **Sí.** Solo se apoya en R09 «trituradora de limpieza T-9» y en el título de R17 que ya fijan [ES]. El resto es mío [SUPUESTO] y se cambia sin tocar la estructura |

---

## D1. Reglas comunes de vestuario

### D1.1 Lenguaje visual: ocupantes frente a máquinas

| Rasgo | Antiguos ocupantes (humanos) | Máquinas |
|---|---|---|
| Superficie | Pintura plana militar, mate, desconchada; hormigón con estarcido | Chapa sin pintar, grafito, soldaduras vistas, placas con perforaciones |
| Marcas | Estarcidos blancos, rótulos a brocha, flechas pintadas, chapas de identificación, cinta americana | Sin texto; cuadrículas de remaches, líneas finas de emisión baja (cian) solo en zonas profundas |
| Objetos | Literas, taquillas, mesas, camillas, cajas de suministros, sacos de arena | Haces de cable negro con abrazaderas, conductos añadidos, restos de robot «injertados» en muros, rieles |
| Reparaciones | Tablones, barricadas, parches de chapa, cintas de peligro | Soldaduras sobre lo humano; las máquinas reutilizan lo humano |
| Color | Oliva, crema, azul-gris, blanco clínico, amarillo de seguridad | Grafito, negro hollín, óxido, acentos cian |
| Símbolo de ocupación | Emblema de la unidad (R03) | Ninguno: las máquinas no firman |

Regla de lectura de combate (defecto 7 de [AUD]: enemigos poco legibles): la decoración es **mate y poco saturada a la altura de los ojos de la arena** (0,5–2,5 m). Los acentos saturados y los emisivos se reservan para landmarks, pantallas y señalética. Los restos de robot son siempre oscuros, sin emisivo ni rim, y yacen tumbados: no se confunden con un enemigo vivo.

### D1.2 Niveles de destrucción

| Nivel | Nombre | Qué se ve | Decales (de 24) | Zonas |
|---|---|---|---|---|
| **D0** | Intacto | Pintura entera, sin escombro, sin goteras. No se usa en el mapa salvo el vestíbulo S00 | ≤ 6 | S00 |
| **D1** | Abandono | Polvo, óxido leve, papeles, objetos en su sitio, algún cable suelto | 12–18 | ACC (R01), HAB (R03, R05), MNT (R09), MAN (R17) |
| **D2** | Daño | Barricadas, agujeros, mobiliario volcado, charcos, cables colgando, fuego antiguo | 18–22 | ACC (R02), HAB (R04), ARM, COM, ALM, AGU (R11, R13), FUN, ENS, BOV |
| **D3** | Tomado | Estructura invadida: cables gruesos, injertos, derrumbes, restos de robot por todas partes | 14–22 | MNT (R10), AGU (R12), REA |

Eje secundario, «presencia de máquinas» M0–M3 (crece con la profundidad): M0 en R01–R05, M1 en R06–R09, M2 en R10–R14, M3 en R15–R18. Gobierna cuántos cables negros, injertos y líneas cian aparecen.

### D1.3 Paleta de materiales del kit (libres)

| Material | Uso en decoración | Fuente |
|---|---|---|
| `Bunker_Wall` (3 m/tile) | Pantallas de hormigón, relieves de muro, pieles de volumen | [INV] A1 |
| `Bunker_Floor` (4 m/tile) | Escombro y parches de suelo | [INV] A1 |
| `Bunker_Ceiling` | Vigas y cerchas del techo si no llevan atlas | [INV] A1 |
| `Bunker_RustMetal` (2 m/tile) | Chapa, contenedores, cerchas. Se referencia el `.mat`; sus texturas están en lista roja y no se editan | [INV] A1, A6.1 |
| `Bunker_Grating` | Pasarelas pequeñas, tapas de desagüe | [INV] A1 |
| `Bunker_Hazard` | Franjas en bordes de caída, bases de pilar, cajas de armería | [INV] A1, [BP] §5 |
| `Bunker_Lamp*` | **No se usan** (son luminarias de R5; emisión HDR ≈ 6) | [INV] A1 |
| `VO_Prop_Pipe`, `VO_Prop_Housing`, `VO_Prop_Cable` | Sobre las mallas `VO_PipeRun2m`, `VO_WallVent`, `VO_CableBundle4m` | [INV] A1/A2.2 |
| `VO_Decal_Burn / Hazard / Rust / Stain` | Decales de malla (suciedad) | [INV] A1, [VO] §3 |
| `VO_Env_*` | Son de Estructura; no se usan en props | — |
| Señalética de plantilla (`Sign_*`) | **Descartada**: no es estilo búnker ([INV] A3) | — |

**Materiales nuevos propuestos (F3; no se crean ahora):**

| Material | Qué es | Cantidad |
|---|---|---|
| `Decor_Props_<ZONA>` | HardSurface con el atlas de props (4 cuadrantes de paleta, D5.1). Copias por zona: cambian el desplazamiento de `_BaseMap`, el tinte y `_DirtFloorY` (cota de suelo real, [INV] A4.1) | 13: ACC1 (0), ACC2 (−12), HAB (−12), ARM (−18), COM (−18), ALM (−22), MNT (−22), AGU (−28), FUN (−28), ENS (−28), REA (−34), MAN (−37), BOV (−37) |
| `Decor_Signage` | URP Lit, atlas 1024² con recorte alfa (letras, flechas, placas). Un único tipo de glifo para todo el mapa | 1 |
| `Decor_Puddle` | Copia de `VO_Decal_Stain` con smoothness alto (≈ 0,9), para charcos que toman el reflection probe | 1 |
| `Decor_Screen_<color>` | Emisivo de pantallas, ≤ 1,5 HDR: **no** hace falta material propio si se usa `_PANELS` del atlas | 0–2 |

[SUPUESTO] La copia por zona usa el desplazamiento de `_BaseMap` para elegir paleta; hay que confirmar que `IronRequiem/HardSurface` respeta `_BaseMap_ST` [VALIDAR F3]. Si no, el atlas lleva las 4 paletas en mallas con UV distinto y se duplican las mallas (coste de memoria bajo).

### D1.4 Clases de colisión

| Clase | Qué es | Collider | NavMesh | Flags estáticos |
|---|---|---|---|---|
| **S0 Marca plana** | Decal de malla o estarcido, ≤ 0,02 m de grosor | Ninguno | No interviene | `BatchingStatic` |
| **S1 Pequeña** | Alto ≤ 0,3 m **o** todas las dimensiones ≤ 0,6 m, fuera de volúmenes libres | Ninguno | `NavMeshModifier.ignoreFromBuild` | `BatchingStatic`, `OccludeeStatic` |
| **S2 Relieve de muro** | Tubería, conducto, placa: sobresale ≤ 0,15 m dentro de un volumen libre, ≤ 0,4 m fuera | Ninguno | `ignoreFromBuild` | ídem |
| **S3 Colgante** | Cables, cerchas, rótulos: **borde inferior ≥ +4,5 m** sobre cualquier suelo pisable | Ninguno | `ignoreFromBuild` | ídem |
| **S4 Sólida** | Alto ≥ 1,1 m (taquilla, rack, contenedor, mesa de cobertura) | **1 `BoxCollider`** por pieza | `NavMeshModifier` con área *Not Walkable* | ídem |
| **S5 Piel** | Revestimiento de un volumen de R1 (garita, bloque de contenedores, núcleo, bombas) | Ninguno: el collider es el del volumen de R1 | Ninguno | ídem |
| **S6 Animada** | Puente del clarificador, plato de radar, ventilador | Ninguno | `ignoreFromBuild` | **No** estática (≤ 8 por cuarto) |

Fuentes del patrón: `NavMeshModifier.ignoreFromBuild` en `VisualOverhaulDressing.cs:172` y flags estáticos en `:75` ([INV] A5). **Comprobar los namespaces de `NavMeshModifier`** (paquete frente a `Assets/NavMeshComponents`) antes de F3 ([AG], [ES] D-U6) [VALIDAR F2]. El NavMesh se hornea **después** de colocar la capa [Decor].

### D1.5 Reglas de colocación (se convierten en reglas del validador, D7.3)

| # | Regla | Motivo / fuente |
|---|---|---|
| **K-1** | Nada de [Decor] con volumen dentro de `freeVolumes` (`criticalPath`, `combatLane`, `slideLane`, `doorApproach`, `spawnClearance`, `ring`, `funnel`) ni de los volúmenes libres de puerta. Única excepción: tipo `routeMark` (D-D1) | [MD] §5.3, §5.5; [ES] §7 |
| **K-2** | Dentro de un volumen libre, lo adosado a un muro entre y = 0,1 y 4,5 m sobresale ≤ 0,15 m y sin collider | Slide de 12 m y dash lateral; engancha al jugador ([ES] E-1) |
| **K-3** | Nada dentro del volumen de un slot: Ø 1,6 × 2,2 m (normal) y Ø 5 × 4,6 m (Big Bot) **ni mallas ni colliders**, salvo marcas planas. Además, **sin collider a ≤ 2,5 m** del centro de un slot (alcance de recogida/ejecución) | [ENC] S6, [ES] E-10, [ES] §2.1 |
| **K-4** | Holgura entre colliders de decoración y obstáculos o muros: **0 m (adosado) o ≥ 4 m** (≥ 5 m con Big Bot). Nada de ranuras de 1–4 m | [ES] E-1 |
| **K-5** | Alturas de colliders: ≤ 0,3 sin collider; sólidos **1,1–1,2** (cobertura baja) o **≥ 2,4** (alta, ≥ 3 m de ancho si está en ruta). Un sólido entre 1,2 y 2,4 solo si va adosado a muro y su collider se prolonga hasta 2,5 m (no se puede pisar) | Cobertura baja/alta [ES] §2.2; salto de 2,03 m [ES] §2.1 |
| **K-6** | Sin escaleras accidentales: ningún apilado de colliders de decoración permite subir con saltos ≤ 1,7 m a una cornisa, una barandilla, un hueco o un borde de bounds. Pila máxima 1,2 m dentro de 3 m de un borde de caída | Saltable ≤ 1,7 m |
| **K-7** | Colgantes: nada entre y = 0,3 y +4,5 sobre pasos, rampas y pasarelas. Las pasarelas a +5 tienen su techo libre: sin colgantes bajos | Cabeza en el apex a 3,83 m ([ES] §2.2) |
| **K-8** | Sobre pasarelas y bordes de caída: barandillas son de R1; la decoración no las sustituye ni las tapa | [ES] §7 |
| **K-9** | Superficies pisables a ≤ 3 m de NavMesh conectado: un sólido pisable de decoración (≥ 1,1 m) no deja zonas aisladas | [ES] E-2 |
| **K-10** | Las cámaras de esclusa (`C<NN>`) y el AABB de las bandas de puerta **no llevan decoración** | [PU] §P4 regla 1; [ES] §2.4 |
| **K-11** | Alrededor de los botiquines y armas: 2 m libres de colliders y de mallas de suelo | Recogida a 2,5 m; legibilidad |
| **K-12** | En arenas, el suelo fuera del perímetro no lleva escombro de más de 0,05 m (armas caídas y módulos con collider deben verse) | [ENC] S6 (los módulos de arma sí tienen collider) |
| **K-13** | Una decoración con collider **no** sustituye una cobertura que R1 o R3 piden. Si R1 da un volumen (garita, bloque, bombas), la decoración es **piel (S5)** y no añade collider | Evita colliders dobles y holguras falsas |
| **K-14** | Las líneas de visión LV-1/LV-2 de [ES] §2.5 se comprueban **incluyendo** los colliders de decoración (nunca se les resta visibilidad al nido de torreta sin pasar el validador) | Visión del nido ≤ 60 % |

### D1.6 Alturas de cobertura que la decoración viste

R1 y R3 fijan las coberturas; R2 solo las viste (S5) y propone las **pocas** que sean decoración con collider.

| Tipo | Altura | Ancho / fondo | Viste con |
|---|---|---|---|
| Baja | 1,1–1,2 m | ≥ 1,2 m | `IR_Crate` L (1,2³), mesa volcada, saco de arena, taquilla tumbada |
| Alta | ≥ 2,4 m (≥ 3 m de ancho en ruta) | — | Contenedores, racks, tabique de chapa, pantalla antiexplosión |
| Saltable (decoración) | ≤ 1,7 m | — | Solo adosada; nunca delante de un carril |

### D1.7 Historia visible: reglas

- Cada cuarto cuenta **una** historia principal legible desde el pórtico (el landmark) y **dos** secundarias en el perímetro. Una pieza narrativa por 10 m de muro.
- Las marcas de las máquinas aumentan hacia la salida del cuarto (el jugador «lee» el avance).
- Los restos de robot solo en perímetro, entreplantas y alcobas; en el suelo de arena como mucho 2 piezas por cuarto, fuera de los volúmenes libres, con collider ≥ 1,1 m o ninguno.

### D1.8 Emisivos y puertas

- Emisivo máximo **1,5 HDR** (pantallas, LED, líneas cian) frente a los ≈ 6 HDR de `Bunker_Lamp*`. Motivo: el bloom ya tiene umbral 0,9 ([INV] A4.2) y C1 de [AUD] (resplandor blanco) obliga a no sumar emisivos fuertes.
- **A ≤ 12 m del plano de una puerta no hay emisivos rojos, verdes ni ámbar** en decoración: son los colores de estado de [PU] (rojo = bloqueada, ámbar = abriéndose, verde = pasa). El rótulo de salida es **blanco hueso** y de baja emisión.
- Luces: ninguna. Las luminarias, balizas rotativas y focos de landmark son de R5 y se anclan en los anclajes de R1.

---

## D2. Orientación (wayfinding)

### D2.1 Códigos de zona y color

El color es **de pintura** (no luz). `S-NN` coincide con el «SECTOR n» del HUD según [PU] §P2.3 [SUPUESTO: S-NN = R<NN>; lo confirma R4/R0 cuando se fije `prologueStages`].

| Zona ([ES] §3) | Código | Cuartos | Nivel y profundidad | Color de pintura (sRGB) | Acento de señal |
|---|---|---|---|---|---|
| Acceso | **ACC** | R01–R02 | N0 (0) → N1 (−12) | Verde grisáceo `#5E6558` | Amarillo seguridad `#C9A227` |
| Habitáculo | **HAB** | R03–R05 | N1 (−12) | Oliva desvaído `#6B7050` | Crema `#BDB59A` |
| Armería | **ARM** | R06 | N1 → N2 (−18) | Gris acero `#3E4246` | Rojo `#A0281E` |
| Comunicaciones | **COM** | R07 | N2 (−18) | Gris azulado `#4B5560` | Verde terminal `#3FD07A` (solo en pantallas) |
| Almacenes | **ALM** | R08 | N2 → N3 (−22) | Ocre/kraft `#8A6B43` | Verde militar `#4D5B45` |
| Mantenimiento | **MNT** | R09–R10 | N3 (−22) | Hormigón gris `#6A6A66` | Rojo/blanco `#B3261E` |
| Agua | **AGU** | R11–R13 | N3 → N4 (−28) | Azulejo sucio `#9DB0A8` | Azul-verde `#2F6F73` |
| Fundición | **FUN** | R14 | N4 (−28) | Hollín `#1B1A19` | Naranja `#C2571A` |
| Ensamblaje | **ENS** | R15 | N4 (−28) | Blanco industrial `#CFCFC8` | Cian `#27B5C9` |
| Reactor | **REA** | R16 | N4 → N5 (−34) | Amarillo peligro sobre gris | Ámbar `#D8861B` |
| Mando | **MAN** | R17 | N5 → N6 (−37) | Azul-gris `#2E3E4A` | Azul-verde pantalla |
| Bóveda | **BOV** | R18 | N6 (−37) | Gris casi negro | Rojo de fase |

Cotas de nivel tomadas de [ES] §3 y §6.

### D2.2 Piezas de orientación

| Id | Pieza | Dónde | Medidas | Tipo | Notas |
|---|---|---|---|---|---|
| **W1** | **Código de sector**: `HAB-03` + nombre + `NIVEL 1 · −12 m` | Muro frente al pórtico de entrada, y = 3,5–5,0; repetido sobre la salida | Letra de 0,8–1,0 m (legible a 40 m) | Estarcido (S0) | Imprescindible en todos los cuartos |
| **W2** | **Flechas y chevrones** `▸▸▸` | Muros del recorrido a y = 1,6–2,4 cada ≤ 12 m; en el suelo solo en respiros y R10 | 1,2 × 0,6 m | S0 (`routeMark` en suelo, D-D1) | Blanco hueso sobre banda del color de la **zona siguiente** |
| **W3** | **Rótulo de salida colgante** `C<NN> ▸ <ZONA>` | Sobre cada salida, a ≥ +4,5 m, visible desde el pórtico (LV-5 de [ES]) | 2,0 × 0,6 m | Pieza `IR_SignBlade` (S3), emisión ≤ 1,0 blanca | Complementa, no sustituye, la señal del marco de R4 |
| **W4** | **Plano «ESTÁ AQUÍ»** | R05, R09, R13 (cabina, pedido de [ES]) y R17 | 4 × 2,5 m | Quad con celda del atlas + marca de «aquí» | Esquema de los 7 niveles; 4 variantes |
| **W5** | **Profundidad**: regla vertical con marcas cada 1 m en R02 | Muro W del pozo, de −12 a 0 | 6 estarcidos | S0 | Hace legible el descenso |
| **W6** | **Rodapié de color de zona** | Muros principales, y = 1,0–1,3 | Franja de 0,25 m, tesela ≤ 10 m, 1 malla combinada por cuarto | Malla plana de 2 cm | Cuenta como **1** marca |
| **W7** | **Marca de caída/hueco** | Borde de cada caída voluntaria y grieta: franja `Bunker_Hazard` y chevrones | — | S0 / skin | Telegrafía lo irreversible (skill de nivel) |

### D2.3 Puertas: reparto con R4

| Elemento | Quién | Contenido |
|---|---|---|
| Banda del dintel de la cara de sala | **R4** | `<ZONA> · S-<NN>` + flecha ([PU] §P2.3) |
| Cara de cámara | **R4** | `ESCLUSA C<NN> → <ZONA siguiente>` |
| Franjas de peligro en jambas, línea de cierre de P | **R4** | — |
| Rótulo colgante W3, código de sector W1, flechas W2 | **R2** | Mismo texto y misma tipografía que R4: **un solo juego de glifos** [PARA R4: compartir celdas del atlas o duplicarlas exactamente] |
| Numeración | Común | C<NN> = conexión R<NN> → R<NN+1> ([MD] §4). No hay otro número de puerta en el entorno |

Reglas de colocación: W1 y W3 quedan **fuera** de la banda de puerta (14 × 7,6 para Z; 7 × 5,8 para A) y de los volúmenes libres. Las marcas del suelo hacia la puerta (W2, `routeMark`) acaban a 2 m del plano del vano y no tocan la línea de cierre de la P.

### D2.4 Criterios de nivel aplicados

- **Landmark visible desde el pórtico** en cada cuarto con silueta de alto contraste de valor (D3).
- **Guiar sin muros:** el color de zona y las flechas marcan la ruta; nunca se cierra un camino con decoración.
- **Respiros ricos, arenas legibles:** los respiros se usan para enseñar el lugar (plano, esquema).
- **No se confía en la luz** para marcar la salida (decisión de R5): la señal funciona apagada.

---

## D3. Guía por zona

Cada zona: materiales, props dominantes, colores, destrucción, historia, landmark y **qué NO poner**. «Mat» = materiales visibles de decoración.

### Z1 · Acceso (ACC) · R01–R02 · D1→D2 · M0

- **Materiales:** `Decor_Props_ACC1` (R01) y `ACC2` (R02), `Bunker_Hazard` en controles, `Bunker_Wall` en pieles, `VO_Prop_Pipe`.
- **Props dominantes:** barreras de control (brazo alzado), sacos, cajas L, taquillas de guardia, terminales de control, conductos, cadenas y guías del pozo.
- **Colores:** verde grisáceo con amarillo seguridad; metal desnudo en el pozo.
- **Historia:** puesto de control cerrado a toda prisa («ACCESO C-04»): papeles esparcidos, taquillas abiertas, barrera levantada. El montacargas cayó: cables, cadenas y contrapeso, marcas de impacto en las guías.
- **Landmark R01:** garita 6 × 6 × 3 vestida con marco de ventana, equipo de techo (aire, antena), bandeja de cables y estarcido `ACCESO C-04`. La baliza es de R5.
- **Landmark R02:** plataforma del montacargas estrellada: guías dobladas, barandilla caída, hazard en el borde, cables y cadenas colgando sin collider por encima de +11.
- **NO poner:** objetos en la rampa del pórtico; nada en el pozo bajo la plataforma inclinada.

### Z2 · Habitáculo (HAB) · R03–R05 · D1–D2 · M0

- **Materiales:** `Decor_Props_HAB`, `Bunker_Wall` pintado, poco óxido; `VO_Decal_Stain`, `VO_Decal_Burn`.
- **Props dominantes:** literas, taquillas, baúles, mesas, bancos, camillas, biombos, cajas de suministros.
- **Colores:** oliva y crema; azul-gris en telas; blanco clínico en R05; rojo desvaído del emblema.
- **Historia:** una unidad que vivió allí. R03: mural de la unidad (emblema, lista de nombres, chapas de identificación en el muro), camas hechas a medias. R04: barricada «NO PASARÁN» contra algo que entraba por el sur; restos de T-07 empalados en ella; comida seca en mesas. R05: enfermería de campaña; torretas desmontadas sobre camillas como anticipo de R06 ([ES] §A6.1 vía R05).
- **Landmarks:** R03 mural en x −6…6, z 20…24; R04 barricada x 6…14; R05 mesa de operaciones bajo la lámpara (de R5).
- **NO poner:** sangre exagerada; nada en los vanos de 6 m de los tabiques de R03; nada en la rampa E de R04.

### Z3 · Armería (ARM) · R06 · D2 · M1

- **Materiales:** `Decor_Props_ARM`, `Bunker_RustMetal`, `Bunker_Hazard` en jaulas.
- **Props:** jaulas (`IR_CageGrate`), cajas de munición, taquillas tumbadas, estantes de armas **enjauladas**, pantallas antiexplosión, vigas.
- **Colores:** gris acero y amarillo; rojo en el estarcido.
- **Historia:** la armería fue vaciada en el último momento y las máquinas abrieron las jaulas de fuera hacia dentro. Hay armas viejas tras rejas.
- **Landmark:** cámara acorazada «ARMERÍA 7» (x −4…12, z −8…8, y 0…8): reja, estantes, puerta de cámara entreabierta. Las rotativas rojas son de R5.
- **NO poner:** armas sueltas, brillantes o a la altura del jugador, para no confundirlas con *pickups*. Las armas decorativas son siluetas distintas de las del jugador, grises y siempre tras barras.

### Z4 · Comunicaciones (COM) · R07 · D2 · M1

- **Materiales:** `Decor_Props_COM` (LED y pantallas verdes bajas por `_PANELS`), `Bunker_Grating` en suelo técnico.
- **Props:** consolas del anillo, racks en las alas, conductos `d`, escotillas de suelo, cables en suelo técnico (sin collider ≤ 0,05 m, solo en alas).
- **Colores:** gris azulado; verde terminal solo en pantallas.
- **Historia:** el último mensaje se envió desde aquí; hojas con turnos, auriculares, una silla caída. Las máquinas desviaron las líneas: cables negros nuevos entre racks.
- **Landmark:** mástil de antenas (3 × 3, hasta el lucernario) con plato de radar rotativo a +8 (`IR_AntennaMast`, `IR_RadarDish`). La luz roja es de R5.
- **NO poner:** nada dentro de los vanos de las 4 P de ala ((6 + 2) × 4 por cada cara) ni en el anillo de rampas. Las alas se tratan como sub-espacios con su propio grupo desactivable.

### Z5 · Almacenes (ALM) · R08 · D2 · M1

- **Materiales:** `Decor_Props_ALM`, `Bunker_RustMetal` en contenedores y estanterías, `Bunker_Floor` en palés.
- **Props:** contenedores de 6 m, cajas, palés, estantes, barriles, cinta de peligro.
- **Colores:** kraft, ocre, verde militar, rojo óxido.
- **Historia:** nada salió de aquí: inventario completo y numerado, ahora derrumbado. La grúa quedó parada sobre la pila.
- **Landmark:** pila de contenedores + estantería de 8 m volcada, con la grúa puente parada encima (decoración a > +10). Hueco: bloque central + 1 m. Contenedores y estantería son **piel (S5)** del bloque de R1.
- **NO poner:** nada en el anillo libre de 6 m ni en las rampas de refugio de 5 m.

### Z6 · Mantenimiento (MNT) · R09–R10 · D1 / D3 · M1–M2

- **Materiales:** `Decor_Props_MNT`, `Bunker_Wall`, `Bunker_Grating`, tubería `VO_Prop_Pipe`, franjas rojo/blanco.
- **Props:** taquillas de operarios (R09), esquema pintado de la trituradora «T-9», tuberías de techo, cajas eléctricas; en R10, pilastras con hazard, cables, restos de robots de limpieza.
- **Colores:** hormigón gris con rojo/blanco; rojo de emergencia en estarcidos.
- **Historia:** R09 explica la persecución antes de que empiece: el esquema de la «trituradora de limpieza T-9», una hoja de turno y un casco. R10: rastros de arrastre y marcas de limpieza; el túnel «se limpia solo».
- **Landmark R09:** esquema pintado (6 × 3 m). **R10:** trituradora (prefab existente `GiantShredder`, no es de R2) y el brillo rojo de las grietas (R5).
- **NO poner:** nada en el túnel de 16 m salvo pilastras adosadas de R1; el salto de 4 m entre grietas no se estrecha. Marcadores de distancia cada 20 m (D2).

### Z7 · Tratamiento de agua (AGU) · R11–R13 · D2–D3 · M2

- **Materiales:** `Decor_Props_AGU` con `_Smoothness` mayor (humedad), `Decor_Puddle`, `Bunker_Grating`, `Bunker_RustMetal`.
- **Props:** válvulas con volante, tuberías gruesas, compuertas, bombas pequeñas, pasarelas de mantenimiento, carriles, cuadros de control.
- **Colores:** azulejo sucio, azul-verde de tuberías, verde de moho en estarcidos de desagüe.
- **Historia:** el colector se derrumbó y la planta se inundó. R11: depósitos clarificadores, puente giratorio aún moviéndose. R12: tubo caído y cascada. R13: bombas aún en marcha («ESTÁ AQUÍ»).
- **Landmarks:** R11 puente giratorio del clarificador (+3…+5, `IR_ClarifierBridge`); R12 tubo colector caído con cascada (partícula ≤ 1); R13 tres bombas 6 × 6 × 5 (`IR_PumpHuge`, piel S5).
- **NO poner:** nada en los embudos de R11; sin fosos ni agua con colisión (lámina de 0,3 m solo visual); en R12 nada en el borde de salto.

### Z8 · Fundición (FUN) · R14 · D2 · M2

- **Materiales:** `Decor_Props_FUN`, `Bunker_RustMetal`, `Bunker_Hazard`, `VO_Decal_Burn`.
- **Props:** crisoles, rieles de grúa, cadenas, moldes, yunques, barriles, bandejas de escoria; escudos de chapa.
- **Colores:** hollín, naranja quemado, amarillo.
- **Historia:** las máquinas reabrieron el horno para fundir los restos de los T-07 y de los humanos.
- **Landmark:** horno con 4 coladas detrás de rejas (x −10…10, z 26…32, y 0…12): rejas, boquillas, emisivo de colada ≤ 1,5 en un quad; los resplandores con luz son de R5. Crisol mayor 10 × 10 × 6 como piel S5.
- **NO poner:** franjas de peligro ni marco en el horno (corrige M11, [ES] R14): no puede parecer una salida. La salida NE lleva W1 y W3.

### Z9 · Ensamblaje (ENS) · R15 · D2 · M3

- **Materiales:** `Decor_Props_ENS`, `Bunker_Grating`, `Bunker_Wall`, `Bunker_Hazard`.
- **Props:** bancos de trabajo con herramientas, plantillas de robot, cinta, cajas de piezas, cables, riel con ganchos.
- **Colores:** blanco industrial, cian de línea (≤ 1,5 HDR).
- **Historia:** las máquinas continúan produciendo T-07 con herramientas humanas.
- **Landmark:** cadena de torsos colgando (x −3…3, z −18…18, y +6…+12): ~20 `IR_Wreck_Torso` con instancing, riel y ganchos. Bajos ≥ +6 (no hay colisión).
- **NO poner:** nada en el hueco central de 6 m de la cinta; los torsos no bajan de +6; jaula opcional x −26…−20, z −25…−19 sin slots.

### Z10 · Reactor (REA) · R16 · D3 · M3

- **Materiales:** `Decor_Props_REA`, `Bunker_Hazard`, `Bunker_RustMetal`.
- **Props:** tuberías gruesas, conductos de refrigeración, cuadros de control, escombro de pared, soportes de galería.
- **Colores:** amarillo peligro, naranja ámbar, cian del núcleo (solo líneas ≤ 1,5).
- **Historia:** las máquinas alimentan su producción con el reactor; la estructura humana se deforma.
- **Landmark:** núcleo cian Ø 10 a techo (piel `IR_ReactorCoreShell`: anillos, puntales, hazard en la base). La luz es de R5.
- **NO poner:** columnas en rampas (M9); nada en el anillo de 6 m.

### Z11 · Centro de mando (MAN) · R17 · D1 · M1

- **Materiales:** `Decor_Props_MAN`, pantallas por `_PANELS`.
- **Props:** consolas de las gradas, sillas, pantallas con la silueta de la Bóveda, mesas, plano.
- **Colores:** azul-gris, azul-verde tenue.
- **Historia:** el último mando ordenó «SELLAR BÓVEDA». Silencio, taza, abrigos.
- **Landmark:** mesa táctica con holograma de la Bóveda en x −5…5, z 6,5…11,5 (`IR_TacticalTable`, `IR_VaultHologram`, emisivo ≤ 1,5, sin transparencia).
- **NO poner:** nada en los pasillos de escalera de 4 m; silueta del jefe [DEPENDE DEL JEFE].

### Z12 · Bóveda (BOV) · R18 · D2 · M3 [DEPENDE DEL JEFE]

- **Materiales:** `Decor_Props_BOV`, `Bunker_Wall`, `Bunker_RustMetal`, `Bunker_Hazard`.
- **Props:** galería perimetral con cajas, cables gruesos, estructura central hasta que el jefe fije la arena.
- **Historia:** contiene lo que el búnker protegía (relato abierto).
- **NO poner:** nada en la antecámara de 20 × 16 × 14 (sin spawns) más allá de los marcos; la plataforma de compromiso 6 × 6 y la zona de 16 × 8 × 10 delante de B quedan libres.

### S00 y esclusas

S00 (vestíbulo de sesión) lleva D0: PlayerStart y `WeaponMountStation` hueco 4 × 4 en (−4; 0; −64) quedan libres; solo estarcido de zona y terminales en los muros. Las cámaras de esclusa no llevan decoración (K-10). Hangar y Room00: **zona protegida, no se toca**.

---

## D4. Plan por cuarto

### D4.1 Tabla resumen

Los cuartos y sus datos vienen de [ES] §6. «Dest.» es el nivel D0–D3 (D1.2). Todas las cifras de coste son estimaciones de papel [SUPUESTO / VALIDAR F3].

| ID | Cuarto | Zona | Dest. | Props | Decales | Mats visibles | DC decor | SetPass | Tris decor (k) | Partículas (sist. / vivas) |
|---|---|---|---|---|---|---|---|---|---|---|
| R01 | Control de acceso | ACC | 1 | 70 | 18 | 9 | 40 | 9 | 45 | 1 / 40 |
| R02 | Pozo del montacargas | ACC | 2 | 85 | 20 | 10 | 50 | 10 | 60 | 2 / 60 |
| R03 | Dormitorios | HAB | 1 | 140 | 20 | 9 | 55 | 9 | 70 | 1 / 30 |
| R04 | Comedor | HAB | 2 | 130 | 22 | 10 | 60 | 10 | 75 | 2 / 50 |
| R05 | Enfermería (respiro) | HAB | 1 | 110 | 16 | 9 | 55 | 9 | 70 | 1 / 30 |
| R06 | Armería | ARM | 2 | 120 | 20 | 10 | 60 | 10 | 75 | 1 / 30 |
| R07 | Comunicaciones | COM | 2 | 120 | 20 | 10 | 60 | 10 | 75 | 2 / 50 |
| R08 | Almacenes | ALM | 2 | 110 | 18 | 9 | 55 | 9 | 70 | 1 / 30 |
| R09 | Taquillas (respiro) | MNT | 1 | 90 | 18 | 9 | 45 | 9 | 50 | 1 / 30 |
| R10 | Túnel de la trituradora | MNT | 3 | 120 | 14 | 8 | 40 | 8 | 60 | 3 / 60 |
| R11 | Decantadores | AGU | 2 | 90 | 22 | 9 | 50 | 9 | 65 | 3 / 60 |
| R12 | Colector derrumbado | AGU | 3 | 100 | 22 | 10 | 55 | 10 | 65 | 3 / 70 |
| R13 | Sala de bombas (respiro) | AGU | 2 | 100 | 20 | 10 | 55 | 10 | 70 | 2 / 40 |
| R14 | Fundición | FUN | 2 | 110 | 20 | 10 | 60 | 10 | 80 | 2 / 60 |
| R15 | Ensamblaje | ENS | 2 | 120 | 18 | 10 | 60 | 10 | 90 | 1 / 30 |
| R16 | Reactor | REA | 3 | 100 | 20 | 10 | 55 | 10 | 70 | 2 / 50 |
| R17 | Centro de mando (respiro) | MAN | 1 | 100 | 16 | 9 | 55 | 9 | 70 | 1 / 20 |
| R18 | Bóveda (jefe) | BOV | 2 | 60 | 12 | 8 | 45 | 8 | 55 | 1 / 30 |

Los «Props» incluyen colgantes y relieves. Límites en D6.

### D4.2 Notas por cuarto

Notación: **VL** = volumen libre de [ES] §7; **Piel** = S5; coordenadas locales como en [ES].

**R01 Control de acceso** (32 × 40, techo 9)
- **Sub-espacios:** garita central; entreplanta +3,5 (x 10…16, z −6…14); bloques de taquillas de guardia macizos (x −16…−8 y 8…16, z −20…−14); perímetro.
- **Densidad:** baja en suelo, media en la entreplanta y el perímetro.
- **Landmark:** piel de garita (marco de ventana, aire, bandeja, estarcido `ACCESO C-04`) en x −5…5, z −1…8.
- **Cobertura = decoración:** saco alto 4 × 2 × 2,4 (x −12…−8, z 10…12) como piel S5 de `IR_SandbagWall`; bajas 1,2 (3) como `IR_Crate` L sobre el volumen de R1.
- **Libres:** ruta x 3…10; carriles W (x −16…−10, z −14…8) y E; puerta Z (x −5…5, z −20…−14); salida A (x 2,5…9,5, z 16…20); slots G1–G3.
- **Orientación:** W1 en el muro N sobre la salida; flechas en el muro E; W3 sobre la A.

**R02 Pozo del montacargas** (40 × 44, techo 21)
- **Sub-espacios:** galería de entrada +12; terrazas +6; suelo; pozo (aire).
- **Densidad:** baja abajo, media en las terrazas, alta en el pozo y la pared (cables y cadenas).
- **Landmark:** plataforma inclinada (x −6…6, z 0…12): guías de 1,5 × 1,5 en (±7; 0) y (±7; 12) vestidas como piel; contrapeso caído (x −8…−4, z 14…18) piel. Cables y cadenas sin collider sobre +11 (hueco x −7…7, z −1…13, y 0…21).
- **Cobertura:** solo las de R1.
- **Libres:** rellano, rampa de galería, terraza E, salida Z (x −5…5, z 16…22), carriles.
- **Orientación:** regla de profundidad W5 en el muro W; W1 en el rellano; flechas en las terrazas. Barandillas de 2,4 m son de R1.

**R03 Dormitorios** (36 × 48, techo 8)
- **Sub-espacios:** nave W, nave E, pasillo central, galerías de literas +3,5.
- **Densidad:** alta en las naves, media en el pasillo.
- **Landmark:** mural de la unidad (decal grande) + chapas en x −6…6, z 20…24.
- **Cobertura:** taquillas H1/H2 (3 × 6 × 2,4) como piel `IR_Locker` row; baúles bajos 1,2 como `IR_Crate` L.
- **Props:** literas `IR_Bunk` (galerías y naves, adosadas a tabiques), taquillas, cajas.
- **Libres:** vanos de 6 m de los tabiques, naves x −14…−8 y 8…14, vestíbulo, salida A (x 9,5…16,5, z 20…24).
- **Orientación:** W1 sobre la salida; flecha en el muro N.

**R04 Comedor** (40 × 40, techo 9)
- **Sub-espacios:** isla de servicio, filas de mesas, cocina elevada +4,5, barricada.
- **Densidad:** media en suelo (mesas), alta en la cocina.
- **Landmark:** barricada «NO PASARÁN» (x 6…14, z −4…16, y 0…4): `IR_SandbagWall`, `IR_BarricadePlate` con estarcido y restos de T-07 empalados (`IR_Wreck_*`) tumbados y oscuros.
- **Cobertura:** mesas de 1,1 m (`IR_Table` cover); barricada 1,2 m en dos tramos (vano de 6 m libre frente a la salida).
- **Libres:** isla con 9 m alrededor, rampa E, salida A (x 16…20, z 4,5…11,5), entrada A.
- **Orientación:** flechas desde la entrada hacia el vano de la barricada.

**R05 Enfermería (respiro)** (36 × 30, techo 8)
- **Densidad:** alta (sin combate).
- **Landmark:** mesa de operaciones y quirófano +1,2 (x −5…7, z −7…5); la lámpara es de R5.
- **Props:** camillas, biombos (colisión simple), torretas desmontadas sobre camillas (anticipo de R06), 3 botiquines (de R3) con cruz pintada.
- **Libres:** ruta entrada → salida Z (x 12…18, z −11…−1), `Map01StageRelease` (x 8…16, z −12…0), semicírculo de r 4 m de la escotilla E opcional.
- **Orientación:** plano W4; línea guía de color de la zona siguiente (ARM) al suelo.

**R06 Armería** (48 × 56, techo 14)
- **Sub-espacios:** nave, entreplanta W +6, pasarela N, nido de TUR, cámara acorazada.
- **Densidad:** baja en suelo, alta en la cámara y bajo la pasarela.
- **Landmark:** «ARMERÍA 7» (x −4…12, z −8…8, y 0…8): `IR_CageGrate`, estantes, puerta entreabierta.
- **Cobertura:** H1–H3 piel de cajones y contenedores; bajas 1,2 como cajas de munición L.
- **Libres:** rellano +6, rampas, pasarela N, nido (`TUR` a (20; 23)), salida Z, slide x −17…−12.
- **Nota LV:** los colliders decorativos no reducen la visión del nido (K-14).

**R07 Comunicaciones** (52 × 52, techo 10)
- **Sub-espacios:** sala central (arena), 2 alas de racks, 2 vestíbulos. Las alas son grupos desactivables.
- **Densidad:** baja en sala (consolas del anillo), alta en las alas.
- **Landmark:** mástil (x −2…2, z −2…2, y 0…10) + plato giratorio `IR_RadarDish` (S6). La luz roja es de R5.
- **Cobertura:** racks altos 6 × 1,2 × 2,4 (4) como `IR_RackServer` row; consolas bajas 1,1.
- **Fuentes diegéticas:** conductos `d` en tabiques (z 0) y escotillas de suelo junto a racks ([ES]).
- **Libres:** vanos de las P de ala, anillo de rampas, vestíbulos Z, slots G1–G4.

**R08 Almacenes** (48 × 56, techo 14)
- **Sub-espacios:** nave, cubierta N +4, balcón W +4, bloque central.
- **Densidad:** baja en suelo, media en la cubierta y el balcón.
- **Landmark:** bloque de 12 × 14 × 6 (x −4…8, z −12…2): piel de `IR_Container` y estantería volcada (+ grúa a > +10).
- **Cobertura:** bajas 1,2 (palés); pilares 3 × 3 × 8 de R1 vestidos como montantes.
- **Libres:** anillo ≥ 6 m, rampas 5 m, salida Z, slots grandes (Ø 5 × 4,6) BS1–BS4.

**R09 Taquillas (respiro)** (28 × 28, techo 8)
- **Densidad:** alta.
- **Landmark:** esquema de la trituradora T-9 pintado (6 × 3 m).
- **Props:** taquillas de operarios, bancos, botiquines pintados, tuberías, cajas.
- **Libres:** diagonal de 6 m, entrada Z (x −14…−8, z −11…−1), salida A (x 2,5…9,5, z 10…14), volumen de `StageRelease` (x 0…12, z 2…10).
- **Orientación:** W4; hacia R10, flechas rojo/blanco.

**R10 Túnel de la trituradora** (16 × 200, techo 10)
- **Densidad:** baja, repetida cada 10 m (pilastras de R1 vestidas).
- **Elementos:** tuberías de techo (≥ +8), cables, cajas eléctricas adosadas, marcadores de distancia W2, chevrones de suelo `routeMark`.
- **Libres:** el túnel completo (y 0…4,5) excepto las pilastras; grietas G1–G3; bahía de entrada.
- **Landmark:** trituradora (prefab propio, no es de R2).
- **Rendimiento:** 200 m visibles: repetición de 3 módulos, LOD agresivo y corte de partículas.

**R11 Decantadores** (48 × 52, techo 14)
- **Sub-espacios:** depósitos T1/T2, pasarela S +6, pasarela baja de válvulas +1,2.
- **Densidad:** media en pasarela de válvulas, baja en el suelo.
- **Landmark:** puente giratorio sobre T1 (+3…+5) con `IR_ClarifierBridge` (S6).
- **Cobertura:** bombas bajas 1,2 en (−14; −14) y (14; 14) como `IR_Pump` small (deseable, o `IR_Crate` L si no existe); muros de depósito de R1 vestidos con tapa y válvulas.
- **Libres:** anillo ≥ 8 m, 3 embudos, rellano de entrada, salida A.

**R12 Colector derrumbado** (44 × 56, techo 14)
- **Sub-espacios:** orilla cercana, orilla lejana, plataforma +5, foso.
- **Densidad:** media en las orillas, alta en el foso (visual).
- **Landmark:** tubo B1 + cascada (partícula ≤ 1 + decales húmedos) (hueco B1 + 2 m, hasta +8).
- **Cobertura:** casetas de válvulas H (6 × 6 × 3) y 4 × 4 como piel; bajas 1,2.
- **Libres:** puentes B1/B2 completos, bordes de salto 3 m, salida A.
- **Orientación:** W7 en todos los bordes de grieta.

**R13 Sala de bombas (respiro)** (32 × 40, techo 10)
- **Densidad:** alta.
- **Landmark:** 3 bombas 6 × 6 × 5 (x −12…−6, z −10, 0, 10): piel `IR_PumpHuge` ×3. Los indicadores verdes de bomba son de R5.
- **Props:** mapa de sectores «ESTÁ AQUÍ» en la cabina +3 (W4), válvulas, botiquines.
- **Libres:** diagonal de 8 m, salida Z (x −11…−1, z 14…20), entrada A.

**R14 Fundición** (52 × 64, techo 13)
- **Sub-espacios:** nave, pasarelas +5, puente grúa +5, horno N.
- **Densidad:** baja en suelo, media en pasarelas, alta bajo ellas.
- **Landmark:** horno + 4 coladas (D3 Z8).
- **Cobertura:** crisol mayor 10 × 10 × 6 como piel; pilares 3 × 3.
- **Libres:** anillo ≥ 6 m, rampas, salida NE x 13…23, z 26…32.

**R15 Ensamblaje** (52 × 50, techo 13)
- **Landmark:** ~20 torsos colgantes.
- **Cobertura:** bancos al tresbolillo (4 × 1,6 × 10), plantillas 3 × 3 × 3, cinta 1,1 m, pilares del riel: pieles `IR_Locker`/`IR_Crate`/rieles.
- **Libres:** hueco central 6 m, salida Z (x −5…5, z 19…25), entrada Z, jaula opcional.
- **Nota:** presupuesto de triángulos 90 k (techo 100 k, D6).

**R16 Reactor** (52 × 50, techo 14)
- **Landmark:** núcleo Ø 10 (x −6…6, z −3…9, y 0…14).
- **Cobertura:** RingCover (4) y torres de refrigeración adosadas (2): pieles con hazard.
- **Libres:** anillo ≥ 6 m, rampas R-W/R-E, salida Z (x −19…−9, z 19…25), rellano de entrada.
- **Medición:** es uno de los 3 cuartos de Release ([ES] R16).

**R17 Centro de mando (respiro)** (40 × 36, techo 12)
- **Landmark:** mesa táctica (x −5…5, z 6,5…11,5) con holograma.
- **Densidad:** media en gradas, baja en suelo.
- **Libres:** salida Z (x −5…5, z 12…18), entrada Z, 3 pasillos de escalera x −14, 0, 14, volumen de `StageRelease`.

**R18 Bóveda** (antecámara 16 × 20 + arena 72 × 72)
- **Densidad:** mínima hasta que se fije el jefe [DEPENDE DEL JEFE]: galería perimetral con cajas y cables; el centro no se decora.
- **Libres:** plataforma de compromiso 6 × 6, volumen de B (16 × 8 × 10 arena), bounds de arena.

---

## D5. Catálogo de props nuevos (Blender, reproducible)

### D5.1 Método

- **Scripts** en `Tools/IronRequiem/Blender/` con el patrón de `door_lib.py` (`reset_scene`, materiales con nombre, funciones por pieza) y la ejecución de `USAGE.md` (`--background --factory-startup`, sin sesión interactiva):
  - `decor_lib.py`: ayudas comunes (cajas con bisel, lathe, array, generación de LOD con decimate, nombres, exportación).
  - `decor_atlas.py`: genera el atlas de paleta (albedo, normal, máscara R = paneles, G = desgaste) y el de señalética.
  - `decor_build_stock.py` (crate, barrel, pallet, container, shelf), `decor_build_dorm_med.py` (locker, bunk, cot, screen, table), `decor_build_tech.py` (terminal, rack, pipe kit, duct, cable), `decor_build_combat.py` (sandbag, hedgehog, cage, rubble, wreck), `decor_build_heroes.py` (los 6 héroes), `decor_verify.py`.
- **Salidas:** `Assets/IronRequiem/Art/Environment/Map01/Props/<Familia>/IR_<Nombre>_v01.fbx` (nunca se sobreescribe un FBX; versión nueva = `_v02`). Informe en `Tools/IronRequiem/Reports/Map01/Decor/`.
- **Convenciones:** 1 unidad = 1 m; pivote en el centro de la base; Y arriba en Unity (ejes -Z/Y como `blender_asset_tool.py`); mallas `_LOD0/_LOD1/_LOD2` (Unity crea el `LODGroup` desde el FBX); sin rig; colliders **no** van en el FBX (`addColliders` apagado, como los Scrap): los añade `DecorAuthoring` según el catálogo.
- **Atlas de paleta:** 1024², 4 cuadrantes de paleta (A oliva/militar, B acero/amarillo, C kraft/ocre, D blanco clínico/cian). Todas las UV de las mallas están en el cuadrante A; las copias de material por zona desplazan `_BaseMap` [VALIDAR F3].
- **Verificación (`decor_verify.py`):** triángulos por LOD, dimensiones, pivote, nombres, UV dentro del atlas, sin n-gonos, sin bordes abiertos en LOD0. Sin esto, la pieza no entra en F3.
- Tripo: no se usa (D5.4).

### D5.2 Imprescindibles (22: 16 familias + 6 héroes)

Tris = LOD0 / LOD1 / LOD2 (una sola cifra = sin LOD). «Col.» = clase de D1.4.

| # | Familia / pieza | Mallas | Medidas | Tris | Col. | Material | Zonas | Reutilización |
|---|---|---|---|---|---|---|---|---|
| 1 | `IR_Crate` | S 0,6³ · M 1,0×0,6×0,6 · L 1,2³ | — | 120 / 36 | S1 / S1 / **S4** (L es cobertura baja) | Atlas | ACC HAB ARM ALM MNT FUN ENS | Muy alta; apilables |
| 2 | `IR_Barrel` + `IR_Pallet` | barril de pie / tumbado / con fuga · palé | Ø 0,6 × 0,9 | 180 / 60 · 90 | S1 (S4 si agrupados ≥ 1,1) | Atlas | ALM FUN MNT AGU | Alta |
| 3 | `IR_Locker` | simple · fila ×3 · abierta · abollada | 0,5×0,5×1,9 | 220 / 60 | **S4** (collider hasta 2,5, K-5) | Atlas | ACC HAB MNT ARM ENS | Muy alta |
| 4 | `IR_Bunk` | litera doble con y sin colchón | 2,0×0,9×1,8 | 380 / 120 | S4 | Atlas | HAB (R03) | Media (también ACC) |
| 5 | `IR_Cot` + `IR_MedScreen` | camilla · biombo de 3 paneles | 0,9×2,0×0,55 · 1,6×1,7 | 160 · 120 | S1 / S4 (biombo 1,7 no pisable, K-5) | Atlas | HAB (R05, R03) | Media |
| 6 | `IR_Terminal` | consola simple · doble · inclinada; pantalla = submalla con máscara R | 0,9×0,6×1,1 | 250 / 80 | S4 (≥ 1,1) o S1 | Atlas (`_PANELS`) | ACC COM MAN ENS HAB | Muy alta |
| 7 | `IR_RackServer` | rack · fila ×3 | 0,8×0,9×2,2 | 300 / 40 | S4 | Atlas | COM ENS MAN | Media |
| 8 | `IR_PipeKit` | codo 90° · T · tubo vertical Ø 0,5 · cuerpo de válvula + volante · brida · manómetro | Ø 0,2–0,6 | 60–250 | S2 | `VO_Prop_Pipe` y atlas | MNT AGU FUN REA ENS | Muy alta; completa `VO_PipeRun2m` |
| 9 | `IR_Duct` | recto 4 m · codo · rejilla de suelo · reja de pared | 1,0×0,8×4 | 96 · 40 | S3 / S2 | Atlas | COM ALM MNT todas | Muy alta (techos) |
| 10 | `IR_Barrier` | saco · muro de sacos 2 m (1,2 y 2,4) · erizo antitanque · `IR_CageGrate_2m` · `IR_BarricadePlate_3m` | 2,0×0,6×1,2 | 400 / 120 · 100 · 160 · 80 | S4 | Atlas + `Bunker_RustMetal` | ACC HAB ARM FUN | Alta |
| 11 | `IR_Rubble` | trozo S/M/L con varilla · montón 3 m · tubo roto | 0,3–1,5 | 60–250 · 500 | S1 / S4 (montón) | `Bunker_Floor` y atlas | AGU MNT ACC HAB REA | Alta |
| 12 | `IR_CeilingBeam` | viga 4 m · cercha 8 m · escuadra | 0,4×4 | 80 · 280 · 40 | S3 | `Bunker_Ceiling` / atlas | Todas (defecto 5 de [AUD]) | Muy alta |
| 13 | `IR_Wreck` | torso · brazo · pierna · cabeza · chasis HoverBot | 0,9×0,5×0,7 | torso 900 / 250 / 60 · brazo 300 · pierna 400 · cabeza 250 · chasis 500 | S1 / S3 (colgado) | Atlas (grafito, sin emisivo) | ARM HAB MNT FUN ENS REA | Alta; R15 ×20 con instancing |
| 14 | `IR_Container` | 6 m · 3 m | 6,0×2,4×2,6 | 220 / 40 | S5 (piel) o S4 | `Bunker_RustMetal` | ALM FUN | Alta |
| 15 | `IR_Table` | mesa cobertura 3,0×1,0×1,1 · mesa pequeña · banco | — | 150 / 40 | S4 | Atlas | HAB MAN | Media |
| 16 | `IR_SignQuad` | quad plano · placa 0,02 · `IR_SignBlade` colgante · panel de plano 4×2,5 | — | 2–48 | S0 / S3 | `Decor_Signage` | Todas | Muy alta |
| 17 | **Héroe** `IR_ClarifierBridge` | brazo radial 7 m + rascador | Ø 14 | 900 / 300 | S6 | Atlas | R11 | Única |
| 18 | **Héroe** `IR_AntennaMast` + `IR_RadarDish` | mástil 3×3×10 · plato Ø 3 | — | 700 / 250 · 400 | S5 / S6 | Atlas | R07 | Única |
| 19 | **Héroe** `IR_PumpHuge` | bomba 6×6×5 | — | 1.500 / 500 | S5 | Atlas | R13 (×3) | Instancing |
| 20 | **Héroe** `IR_TacticalTable` + `IR_VaultHologram` | mesa + hologram sólido emisivo | 10×5 | 600 · 800 | S4 / S1 | Atlas (`_PANELS`) | R17 | Única |
| 21 | **Héroe** `IR_ReactorCoreShell` | anillos, puntales, base hazard | Ø 10 | 1.800 / 600 | S5 | Atlas + `Bunker_Hazard` | R16 | Única |
| 22 | **Héroe** `IR_FurnaceFront` | frente de horno con 4 bocas y reja | 20×12 | 1.200 / 400 | S5 | Atlas + emisivo ≤ 1,5 | R14 | Única |

Orden de F3: primero 1, 3, 6, 8, 9, 10, 12, 16 (modulares que aparecen en > 10 cuartos), luego 2, 4, 5, 7, 11, 13, 14, 15 y, por último, los héroes por orden de recorrido.

### D5.3 Deseables

| Pieza | Para qué | Tris | Zonas |
|---|---|---|---|
| `IR_Shelf` estantería 2 m | R08 montantes y estantes (alternativa: vigas) | 200 | ALM |
| `IR_CableDrop` | Cable colgante de bajo coste (el kit cuesta 600 tris) | 150 | Todas |
| `IR_Chain` | Cadenas en R02, R14 | 60 por tramo | ACC FUN |
| `IR_PumpSmall` | Bombas bajas de R11 | 400 | AGU |
| `IR_Fan` | Ventilador en techo (S6) | 240 | COM MNT |
| `IR_WallBox` | Extintor, caja de fusibles, botiquín | 80–140 | Todas |
| `IR_GuideRail_6m` | Guías del pozo R02 | 120 | ACC |
| `IR_Conveyor_3m` | Cinta de R15 | 260 | ENS |
| `IR_Window` | Ventana de garita R01 | 60 | ACC |
| `IR_Curtain` | Cortina en R05 | 100 | HAB |

### D5.4 Tripo

**Ninguna pieza lo justifica.** Todo lo planificado son formas duras que Blender resuelve con primitivas y lathe. Única candidata posible: un hulk de Big Bot destruido (R08/R16) por su silueta orgánica, solo como **propuesta** [DECISIÓN USUARIO D-D3]. No he podido consultar el coste de Tripo (sin red): se indicaría antes de cada generación y no se genera sin tu aprobación.

---

## D6. Rendimiento

### D6.1 Presupuesto de la capa [Decor] dentro de [INV] §5

Límite del cuarto: ≤ 350 DC sin combate, ≤ 400 con combate, SetPass ≤ 200 (techo 260), ≤ 400 k triángulos en combate, ≤ 24 decales.

| Tipo de cuarto | DC decor | SetPass decor | Tris decor | Decales | Partículas ambientales |
|---|---|---|---|---|---|
| Arena de combate | ≤ 60 | ≤ 10 | ≤ 80 k | ≤ 24 | ≤ 5 sist. / ≤ 150 vivas |
| Respiro | ≤ 70 | ≤ 12 | ≤ 100 k | ≤ 24 | ≤ 5 sist. / ≤ 100 |
| R10 (túnel) | ≤ 45 | ≤ 9 | ≤ 60 k | ≤ 24 | ≤ 3 sist. / ≤ 80 |
| R15 (excepción) | ≤ 60 | ≤ 10 | ≤ 100 k | ≤ 24 | ≤ 2 sist. |
| R18 | ≤ 50 | ≤ 9 | ≤ 60 k | ≤ 24 | ≤ 3 sist. |

Reparto: la decoración ocupa ≈ 15 % de los DC, ≈ 5 % del SetPass y ≤ 20 % de los triángulos de [INV] §5. Las partículas de combate (≤ 500 vivas, ≤ 60 sistemas globales) tienen prioridad: la decoración cede sus sistemas si el combate los necesita [PARA R5].

### D6.2 Técnicas

| Técnica | Regla |
|---|---|
| **Static batching** | Todo lo no animado con `BatchingStatic`, `OccludeeStatic`, `ReflectionProbeStatic` (patrón de `VisualOverhaulDressing.cs:75`). Se fusiona por material, así que el número de materiales distintos por vista importa más que el de objetos |
| **Materiales compartidos** | 1 material de props por zona (13 en el mapa, ≤ 2 visibles a la vez), 1 de señalética, 4 decales del kit. Cero materiales por prop. Sin `MaterialPropertyBlock` en decoración |
| **SRP Batcher / instancing** | HardSurface declara instancing ([INV] A4.1). Se usa instancing para repetidos **no estáticos** (torsos de R15 si se animan, ventiladores). SRP Batcher: comprobar compatibilidad del shader en el Frame Debugger [VALIDAR F3] |
| **LOD** | `LODGroup` solo en piezas > 1.500 tris o repetidas ≥ 20 veces. LOD1 a ≈ 30 % de pantalla, culled a 5–8 % |
| **Oclusión** | Sub-espacios como grupos (alas de R07, jaula de R15). Occlusion culling horneado: **la caché es por escena y los cuartos se cargan aditivos** [VALIDAR F2]. Apoyarse en `LabRoomVisibility` y en las pieles de volumen de R1 como oclusores |
| **Teselas** | La decoración se coloca en piezas ≤ 10 m (límite de 4 luces por objeto, [BP] §5). Nada de mallas combinadas > 10 m |
| **Decales** | Mallas de decal (`MeshDecal` de `VisualOverhaulDressing`), alpha blend, sin sombras. Cap 24: estarcidos W1/W2/W5 + manchas + rodapié (cuenta 1) |
| **No duplicar** | Antes de combinar mallas, inspeccionar `Assets/FPS/Scripts/Game/MeshCombiner` ([iron-requiem-performance]) |
| **Animadas** | ≤ 8 por cuarto, sin física, `Update` mínimo, desactivadas fuera de vista (S6) |
| **Texturas** | 1 atlas de props 1024² (albedo + normal + máscara) y 1 de señalética 1024²: ≈ 3–4 MB comprimidos [SUPUESTO] |

### D6.3 Partículas ambientales

- **Polvo:** el emisor global que sigue a la cámara (≤ 70 partículas) de [VO] §3.
- **Vapor:** ≤ 16 partículas por sistema, ≤ 3 rejillas por cuarto.
- **Goteo de agua** (AGU, MNT): ≤ 8 partículas por sistema, ≤ 4 por cuarto; sin colisión.
- **Cascada de R12:** ≤ 1 sistema ([ES] R12).
- Si una luz las ilumina (vapor rojo en R06, brillo del horno), el tinte lo decide **R5** [PARA R5]. R2 no pone luces.

---

## D7. Datos de F2/F3 y `DecorAuthoring`

### D7.1 `Assets/IronRequiem/Levels/Map01/Data/Decor/R<NN>.json` (esquema v0)

Coordenadas locales al cuarto, como en [ES] §12. Lee `Data/Structure/R<NN>.json` para `worldOrigin`, `freeVolumes`, `spawnZones`, `doors` y `landmark`.

| Campo | Tipo | Contenido |
|---|---|---|
| `id`, `zone`, `seed` | string, string, int | Zona (`ACC`…); semilla fija (se usa un generador propio, no el estado global) |
| `destruction`, `machine` | int | D0–D3 y M0–M3 |
| `materials` | {props, signage, puddle, decals{}} | Nombre de material |
| `budget` | {drawCalls, setPass, tris, decals, particleSystems, particles} | Caps del cuarto (D6.1) |
| `subspaces` | [{id, min, max, density, group}] | `density`: `low` · `mid` · `high`; `group`: nombre del grupo activable |
| `props` | [{id, prefab, pos, yaw, scale, kind, collider, nav, attach, lod, group, tag}] | `kind`: `S1`…`S6`; `collider`: `none` o `{type: box, center, size}`; `nav`: `ignore` · `notWalkable`; `attach`: `floor` · `wall:±X/±Z` · `ceiling` · `volume:<blockId>` |
| `decals` | [{id, type, material, pos, normal, size, rotY, atlasCell?, routeMark?}] | `type`: `stain` · `rust` · `burn` · `hazard` · `puddle` · `stencil` · `trim` |
| `signage` | [{id, kind, text, pos, normal, size}] | `kind`: `W1`…`W7`; `text` es un literal del atlas |
| `skins` | [{id, target, prefab, inflate}] | `target`: `block:<id>` del JSON de Estructura; `inflate` ≤ 0,1 m |
| `animated` | [{id, prefab, pos, kind, axis, rpm}] | S6 |
| `particles` | [{id, kind, pos, maxParticles}] | `dust` · `steam` · `drip` · `fall` |
| `notes` | string | — |

Extracto (R03, ilustrativo):

```json
{
  "id": "R03", "zone": "HAB", "seed": 30301, "destruction": 1, "machine": 0,
  "materials": { "props": "Decor_Props_HAB", "signage": "Decor_Signage", "puddle": "Decor_Puddle" },
  "budget": { "drawCalls": 55, "setPass": 9, "tris": 70000, "decals": 24, "particleSystems": 1, "particles": 30 },
  "subspaces": [ { "id": "dormW", "min": [-18, 0, -18], "max": [-8, 8, 24], "density": "high", "group": "dormW" } ],
  "props": [
    { "id": "D001", "prefab": "IR_Bunk_Double_v01", "pos": [-17.2, 0, 4], "yaw": 90, "kind": "S4",
      "collider": { "type": "box", "center": [0, 0.9, 0], "size": [2.0, 1.8, 0.9] },
      "nav": "notWalkable", "attach": "wall:-X", "lod": true, "group": "dormW" }
  ],
  "decals": [ { "id": "M01", "type": "stencil", "material": "Decor_Signage", "atlasCell": "EMBLEM_UNIT",
                "pos": [0, 3.2, 23.95], "normal": [0, 0, -1], "size": [5, 3] } ],
  "signage": [ { "id": "W01", "kind": "W1", "text": "HAB-03", "pos": [0, 4, 23.95], "normal": [0, 0, -1], "size": [4, 1] } ]
}
```

### D7.2 `DecorAuthoring.cs` (propuesta; no se crea ahora)

- **Ruta:** `Assets/IronRequiem/Editor/Map01/DecorAuthoring.cs` (namespace `IronRequiem`; clase y archivo con el mismo nombre). Menú `Tools > Iron Requiem > Map01 > Decor`.
- **Capa:** solo escribe y borra hijos del raíz `[Decor]` de la escena del cuarto. Nunca toca `[Structure]`, `[Encounters]`, `[Doors]`, `[Lighting]` ([MD] §4).
- **Idempotente:** un mismo JSON produce siempre la misma capa (semilla fija; orden de piezas por `id`). Antes de generar, vacía los hijos de `[Decor]` creados por ella.
- **Undo:** un solo grupo (`Undo.SetCurrentGroupName("Decor R03")`), `Undo.RegisterCreatedObjectUndo` y `Undo.DestroyObjectImmediate`. Rompe con el patrón sin Undo de `VisualOverhaulDressing` ([INV] A5).
- **Prefabs:** `PrefabUtility.InstantiatePrefab`; colliders y `NavMeshModifier` desde el JSON; flags estáticos según la clase.
- **Escena:** no guarda una escena que el usuario ya tuviera modificada ([AG]); respalda las escenas afectadas en `Tools/IronRequiem/Backups/<fecha>/` antes de escribir.
- **Informe:** `Tools/IronRequiem/Reports/Map01/decor-R<NN>.json` con piezas, triángulos, decales y materiales distintos.
- **No** crea luces, no hornea el NavMesh (lo hace R1/R0 después) y no usa el Editor sin el coordinador.

### D7.3 Validador (rechaza la capa si falla)

| # | Regla | Cómo |
|---|---|---|
| V-01 | Ninguna pieza (salvo `routeMark`) solapa `freeVolumes` de `Data/Structure` ni los volúmenes de puerta | AABB por pieza |
| V-02 | Ninguna pieza ni collider en el volumen de un slot (Ø 1,6 × 2,2 y Ø 5 × 4,6) y ningún collider a ≤ 2,5 m del centro | Cápsula |
| V-03 | Holgura de colliders de decoración: 0 o ≥ 4 m (≥ 5 m con Big Bot) | Distancias entre AABB |
| V-04 | Altura de collider: ≤ 0,3 sin collider; sólido 1,1–1,2 o ≥ 2,4; 1,2–2,4 solo adosado y con collider a 2,5 | K-5 |
| V-05 | Relieves en volúmenes libres ≤ 0,15 m sin collider (K-2) | Distancia al plano de muro |
| V-06 | Colgantes: borde inferior ≥ +4,5 m sobre suelo pisable | Raycast hacia abajo |
| V-07 | Sin escaleras accidentales (K-6) | Grafo de saltos ≤ 1,7 m y gap ≤ 1,0 |
| V-08 | Nada en el AABB de esclusas ni en las bandas de puerta | K-10 |
| V-09 | Decales ≤ 24, partículas y sistemas ≤ caps, animadas ≤ 8, luces = 0 | Contadores |
| V-10 | Estimación de DC, SetPass y tris ≤ presupuesto | Suma de mallas y materiales distintos |
| V-11 | Emisivos rojos, verdes o ámbar a ≤ 12 m del plano de una puerta | D1.8 |
| V-12 | LV-1/LV-2: la capa no reduce la visión del nido; incluye colliders de decoración | Rejilla de 2 m, raycast a 1 m |
| V-13 | Cada pieza apuntada a `volume:<id>` queda dentro de su AABB + 0,1 m y sin collider | S5 |

---

## D8. Riesgos

| Riesgo | Efecto | Cómo se evita |
|---|---|---|
| Decoración que engancha al jugador (relieves de muro en carriles, colgantes bajos) | Se corta el slide o el dash | K-2, K-7, V-05, V-06 |
| Escaleras accidentales por cajas apiladas | El jugador sale de bounds | K-6, V-07 |
| Z-fighting de decales y rodapié | Parpadeo | Offset de 2 cm, un decal por superficie, sin solape de dos decales, `Decor_Signage` con recorte alfa |
| Piezas flotantes o hundidas | Se rompe la lectura | `attach` en el JSON; raycast a la superficie y tolerancia ≤ 0,02 m en V-01..13 |
| Slot ocupado por decoración («OLEADA EN ESPERA») | Ronda atascada | K-3, V-02 |
| NavMesh con islas por sólidos pisables | Spawn fallido | K-9, `NavMeshModifier` *Not Walkable*, rehorneo después de decorar |
| Armas decorativas confundidas con pickups | Jugador pierde tiempo | Siluetas distintas, grises, siempre tras barras, ninguna en suelo (D3 Z3) |
| Restos de robot confundidos con enemigos | Falsos blancos | Grafito sin emisivo ni rim, siempre tumbados |
| Emisivos que parecen luz de puerta | Mala lectura de esclusa | D1.8, V-11 |
| Resplandor por emisivo HDR + bloom (C1 de [AUD]) | Pantalla blanca | Emisivo ≤ 1,5; sin superficies > 2 m² |
| Coste de draw calls por materiales proliferantes | Pasa de 400 DC | 1 material por zona, atlas, `V-10` |
| SRP Batcher incompatible con HardSurface o con el offset de `_BaseMap` | SetPass altos | Medir en Frame Debugger en F3; plan B: URP Lit con suciedad horneada |
| Colliders dobles con los volúmenes de R1 | Holguras falsas | S5 sin collider (K-13) |
| Oclusión no funciona con escenas aditivas | Todo el cuarto vecino se dibuja | Grupos y `LabRoomVisibility`; medir en F2 |
| Zona protegida | Rotura de la cinemática | Nada en Room00_Hangar, IntroMenu ni sus scripts; no se copian `HG_*` |
| Narrativa mía chocando con la de R3/R4 | Textos contradictorios | Textos literales del atlas; revisión al cierre de F1 |

---

## D9. Skills usadas

| Skill | ¿Cargada? | Qué se aplicó |
|---|---|---|
| `iron-requiem-performance` (SKILL.md leído) | Sí (archivo) | Presupuestos derivados del proyecto y de la visibilidad real; revisar `MeshCombiner` antes de duplicar; evitar instancias de material; no `MaterialPropertyBlock`; medir antes y después |
| `gamedev:create-game-assets` | Sí | Manifest por pieza (nombre, tris por LOD, colisión, material, prioridad, procedencia); un objetivo visual antes de producir (atlas de paleta); normalización determinista; validar en contexto; registrar procedencia; 3D: pivote, escala, LOD y colisión comprobados, no inferidos. Sin generación por IA: guion de Blender |
| `unity:craft-game-floors` | Sí (solo criterios de acabado) | Paleta fija y declaración de intención visual antes del primer material; props como «placeholder» solo si faltan; no se reestructura el proyecto ni se cambian materiales ajenos |
| `gamedev:shader-programming` | Sí | `_DIRT` (suciedad de espacio mundo con `_DirtFloorY` por zona), `_EDGEWEAR` (desgaste), humedad por smoothness y charcos reflectantes, `_PANELS` para pantallas, emisivo con tope; sin tocar el shader (lista roja) |
| `gamedev:level-design` | Sí | Landmarks visibles desde el pórtico, guiar con color y flechas, no con muros; respiros como enseñanza; telegrafiar lo irreversible (caídas); pacing de densidad (suelo limpio en arenas, denso en respiros); dressing al final sobre bloqueo validado |
| `gamedev:performance-optimization` | Sí | Presupuesto por cuarto, split CPU/GPU (el juego está limitado por CPU: menos objetos, no menos triángulos), batching estático, instancing solo para repetidos no estáticos, LOD y oclusión, medir en build y no en Editor |

**No se usaron:** Unity, Blender, la CLI, Tripo, Meshy, Composio ni la red.
