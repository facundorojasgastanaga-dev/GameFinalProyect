# Pase visual con Fable · F1 · d) Decoración

Fecha: 21/11/2026. Rol: Director de arte (rol 2 del prompt 12). **Fase F1: propuesta → PARADA. Nada de esto se implementa hasta que elijas.**

Solo he escrito dentro de `Docs/IronRequiem/VisualPassFable/`. No he tocado `Assets/`, `ProjectSettings` ni otros documentos, no he abierto Unity y no he usado git, Meshy, Tripo, créditos ni IA generativa de imágenes. Las imágenes salen de scripts de Blender 5.2 reproducibles (`concepts/src/`).

Marcas: **[COMPROBADO]** lo he leído yo en archivos o datos; **[F0]** viene de los inventarios o capturas de F0; **[PROPUESTA]** es dirección de arte nueva; **sin comprobar** lo que no he podido verificar leyendo.

Las secciones a) luminosidad y linterna, b) luces de robots y c) ojos rojos las escribe el coordinador. Aquí solo doy un objetivo de luz **indicativo** para que la decoración y la luz cuenten lo mismo.

---

## 0. Qué tienes que decidir

| # | Decisión | Mi recomendación | Dónde |
|---|---|---|---|
| **D1** | Dirección del mapa: O1 invertir cotas, O2 solo re-tematizar, O3 adaptar el contexto | **O1**, hecha por el chat 9 antes de decorar los 6 cuartos con desnivel. Si el chat 9 no puede: **O2+** (O2 más R02 invertido). Nunca O3 | §1 |
| **D2** | Nombres y temática nuevos de los 18 cuartos | Aprobar la tabla de §2 (vale igual para O1 y O2) | §2 |
| **D3** | Qué hacer con los rastros de vida humana (camas, comida, camillas, restos humanos en el horno) | Quitarlos; dejar solo estarcidos, equipo y registros | §2.2 |
| **D4** | Kit de props: 19 de los 22 previstos, 3 descartados y 10 nuevos | Aprobar la lista y el orden de §5 | §5 |
| **D5** | La «compuerta de salida» de R18 es decoración (landmark), no una puerta del sistema de Puertas | Sí, coordinada con el chat 9 y con el jefe | §3.3, §8 |
| **D6** | Haces de luz natural del tramo 3: la luz real la pone Iluminación; Decoración solo pone la brecha, el polvo y una malla de haz | Sí | §3.3, §6 |

Vista de conjunto:

![Esquema de la ruta de subida](concepts/ruta_subida.png)

---

## 1. La contradicción de dirección: el mapa baja y la historia sube

### 1.1 El hecho

- **Contexto (lo fijas tú):** el jugador despierta en las profundidades y cada zona está más cerca del exterior que la anterior. Abajo laboratorios, en medio infraestructura, arriba señales del exterior hasta la salida.
- **Documentos y datos actuales:** el búnker **baja** de 0 a −37 m (`Map01/MAP01_DESIGN.md` l.20). El hangar del prólogo figura como «Superficie» (`Map01/01_ESTRUCTURA.md` §3, fila 0). La ruta termina en la Bóveda a −37 m, sin salida («fin de mapa», `01_ESTRUCTURA.md` §6). [COMPROBADO]
- Es la contradicción C1 del inventario C, y arrastra C2 (orden temático), C3 (no hay laboratorios), C5 (vida humana), C6 (final sin salida) y C7 (ninguna señal del exterior).

### 1.2 Cómo están expresadas las cotas en los datos [COMPROBADO]

Leído en `Assets/IronRequiem/Levels/Map01/Data/Structure/` (solo lectura):

- **Cada cuarto** (`R01.json` … `R18.json`) tiene `worldOrigin: [x, y, z]`. La `y` es la cota del suelo principal: 0, −12, −12, −12, −12, −18, −18, −22, −22, −22, −28 (×5), −34, −37, −37. Todo lo demás del cuarto (bloques, puertas, rampas, spawns, volúmenes libres, anclajes de luz) va en coordenadas **locales**.
- **Cada esclusa** (`C01.json` … `C17.json`) y `S00.json` van en coordenadas **de mundo**: `worldCenter`, `floorY`, centros de puerta, trigger, anclajes y salidas de vapor.
- Dos peligros llevan una cota de mundo: `hazards[].upperFloorY` en R10 (−22) y R12 (−28).
- Los datos de `Lighting/`, `Encounters/` y `Doors/` son locales al cuarto (comprobado en R02).
- `yaw` vale 0 en todos, y el validador **rechaza** cualquier otro valor (`Editor/Map01/StructureValidation.cs:85`). `Map01Core.cs:187` asume yaw 0 al pasar de local a mundo.
- **En planta no hay ningún solape**: he cruzado las 35 cajas (18 cuartos y 17 esclusas) y salen 0 pares solapados en XZ. Cambiar solo la `y` no puede crear solapes.
- **Doce cuartos son planos** (entrada y salida a la misma cota): R01, R03, R04, R05, R07, R09, R10, R12, R13, R14, R15 y R18.
- **Seis cuartos concentran todo el desnivel**, y en los seis se entra por arriba y se sale por el suelo:

| Cuarto | Entrada (y local) | Salida (y local) | Desnivel | Cómo se baja hoy (bloques del JSON) |
|---|---|---|---|---|
| R02 Pozo | +12 | 0 | 12 m | `galleryRamp`, `rampE`, `rampW` (más caídas de 6 m) |
| R06 Armería | +6 | 0 | 6 m | `rampSW`, `nestRamp` |
| R08 Almacenes | +4 | 0 | 4 m | `rampNE`, `rampWS` (más caída de 4 m) |
| R11 Decantadores | +6 | 0 | 6 m | `rampW`, `rampE` |
| R16 Reactor | +6 | 0 | 6 m | `rampRW`, `rampRE` |
| R17 Centro de mando | +3 | 0 | 3 m | tres escaleras `aisle0/1/2` |
| **Total** | | | **37 m** | |

Consecuencia: invertir el mapa **no** es solo cambiar el signo de 18 números. En doce cuartos sí lo es. En los otros seis hay que entrar por abajo y salir por arriba. Las rampas para subir ya existen en los seis, así que la geometría de cada sala sirve; lo que cambia es qué puerta es la entrada.

No he encontrado en el repositorio el script `plan2.py` que generó la tabla de mundo de `01_ESTRUCTURA.md` §5.2 (búsqueda sin resultados): el plano habría que regenerarlo con un script nuevo o a mano.

### 1.3 Las tres opciones

#### O1 · Invertir las cotas (el hangar pasa a ser el nivel más profundo)

El hangar se queda donde está (cota 0, zona protegida, sin tocar) y pasa a ser el fondo. Los 18 cuartos suben de 0 a +37 m. Mismo orden de cuartos y de encuentros. La Bóveda pasa a ser la compuerta de salida a superficie, con el jefe guardándola.

Cotas resultantes (suelo principal → cota de salida):

| R01 | R02 | R03–R05 | R06 | R07 | R08 | R09–R10 | R11 | R12–R15 | R16 | R17 | R18 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 0 → +12 | +12 | +12 → +18 | +18 | +18 → +22 | +22 | +22 → +28 | +28 | +28 → +34 | +34 → +37 | +37 |

**Qué datos cambian:**

| Dato | Archivos | Tipo de cambio |
|---|---|---|
| `worldOrigin.y` | 18 `Structure/R*.json` | Mecánico |
| `worldCenter.y`, `floorY`, puertas, trigger, anclajes, vapor | 17 `Structure/C*.json` (S00 no cambia) | Mecánico, pero hay que regenerar el plano |
| `upperFloorY` | R10 y R12 | Mecánico |
| Papeles de entrada y salida, clase de puerta en cada extremo (A o Z), `portal`, `arena`, `spawnZones`, `freeVolumes`, anclajes | 6 `Structure` (R02, R06, R08, R11, R16, R17) | **Rediseño**: se recorren al revés. Con yaw 0 obligatorio, la vía práctica es un espejo en Z de los datos del cuarto |
| Posición en planta de todo lo que va detrás de cada cuarto invertido | Los 35 archivos de Structure | Regenerar el plano de mundo (hoy 0 solapes) |
| Puntos fijos de spawn, persianas y volúmenes sellados | `Encounters/` y `Doors/` de R02, R06, R08, R11 y R16 | Revalidar y, donde falle, reautorizar |
| Luces | `Lighting/` de los 6 cuartos | Siguen a los anclajes por ID; revisar la luz de salida |
| Escenas | 18 de cuarto y 4 de sesión | Regenerar con los scripts de autoría; rehacer el NavMesh de los 18; rehacer las reflection probes |

**Qué documentos cambian:** `MAP01_DESIGN.md` §1 (l.20–21) y tabla de estado; `01_ESTRUCTURA.md` §3 (fila del hangar y columna «Posición»), §4, §5.1–5.2, §6, las fichas de los 6 cuartos y §8; `02_DECORACION.md` D0.2, D1.2, D2.1 (niveles y profundidades), D2.2 W5, D3 completo y D5; `03_ENCUENTROS.md` en los 5 combates invertidos; `04_PUERTAS.md` (clases en las puertas que cambian de papel y rótulos); `05_ILUMINACION.md` §L2.2 y las fichas L3 de los cuartos re-tematizados.

**Trabajo para el chat 9 (Mapa 1):** un script de espejo y de cotas, el plano de mundo nuevo con su comprobación de solapes, la revalidación de estructura, encuentros y puertas, la regeneración de las 22 escenas con NavMesh, y un recorrido completo. Es trabajo de estructura, no de arte.

**Riesgos:**

- **Cambia cómo se entra a 5 arenas** (R02, R06, R08, R11, R16). Hoy llegas a una galería alta y ves la sala desde arriba; con O1 llegas abajo y los enemigos pueden tener la altura. El orden y la composición de los encuentros no cambian, pero la lectura de la llegada sí. Hay que jugarlo antes de darlo por bueno.
- Las reglas «slots a ≥ 10 m de la entrada» y «bounds sin la plataforma de llegada» se diseñaron para la entrada actual: pueden fallar en esos 5 cuartos.
- Las capturas F0 usan puntos de vista en coordenadas de mundo (`F0/rooms.csv`). Con O1 hay que trasladarlos para que el «antes y después» sea comparable.
- Coordinación: si el chat 9 está activo en Map01, O1 es suyo. Este pase no debe tocar Structure.
- **Hangar protegido:** no se mueve ni cambia de aspecto. Solo cambia su etiqueta en los documentos («Superficie» → «fondo»). En la captura F0 `Room00_Hangar_vp1_vacio.jpg` hay una franja azul en la parte alta; no sé si es cielo abierto o techo iluminado (**sin comprobar**). Si la cinemática enseña cielo, chocaría con «despierta en las profundidades» y eso no se arregla sin tocar zona protegida: te lo pregunto, no lo cambio.

**A favor:** es la única opción en la que lo que se anda coincide con lo que se cuenta. Además, con O1 la contradicción C4 desaparece sola: la tabla «presencia de máquinas» de `02_DECORACION.md` (M0 en R01–R05 … M3 en R15–R18) se queda igual por cuarto; solo cambia su explicación, de «cuanto más abajo, más tomado» a «cuanto más cerca de la superficie, más tomado» (los robots entraron por arriba).

#### O2 · Mantener la geometría que baja y solo re-tematizar

No se toca Structure. Se aplican los nombres, la decoración y la luz de §2 y §3 sobre el mapa tal como está.

**Qué cambia:** los mismos documentos de decoración, luz y rótulos que en O1; ningún dato de Structure, Encounters ni Doors. Trabajo para el chat 9: ninguno de estructura; solo rótulos de puerta cuando exista `DoorPresentation`.

**Qué no cuadra, dicho con honestidad:**

- En seis cuartos **bajas rampas mientras la historia dice que subes**: 37 m en total. El peor es R02, justo al principio: un pozo de 12 m que se desciende. Es el primer cuarto vertical y fija la dirección en la cabeza del jugador.
- La decoración dirá «más cerca de la superficie» en salas cuyo suelo está cada vez más abajo. Cualquier rótulo de profundidad con número mentiría: en O2 solo se podrían pintar códigos de nivel, no metros.
- El hangar sigue estando, físicamente, por encima de todo.
- La luz y las señales del exterior sí funcionarían (son decoración y datos de luz), pero el cuerpo del jugador notará la bajada.

**Variante O2+ [PROPUESTA]:** O2, más dos apoyos baratos:

1. Invertir **solo R02** (un cuarto, el más evidente), para que el primer gran desnivel sea una subida.
2. Vestir las 17 esclusas como montacargas que suben (sonido, luces que pasan hacia abajo por rendijas, indicador de nivel). Es ficción: la cota real no cambia y no se mueve al jugador. Depende de la presentación de puertas del chat 9, que aún no existe (`DoorPresentation` no está en `Assets/`, inventario E). **Sin comprobar** que el contrato de `CampaignAirlock` admita esa presentación.

Con O2+ quedan cinco bajadas locales de 3 a 6 m, que se leen como relieve de la sala y no como dirección de la ruta.

#### O3 · Mantener el descenso y adaptar el contexto

Se cambia la historia para que encaje con el mapa: se despierta arriba y se baja.

**Qué cambia:** el bloque «Contexto del mundo» de `PROMPTS_CHATS.md` §12 y tu premisa. Ningún dato del proyecto. Trabajo para el chat 9: ninguno.

**Riesgos:**

- **Contradice lo que has fijado** («despierta en las profundidades», «el mapa es una subida»). Solo tú puedes cambiarlo.
- La luz iría al revés de tu queja principal: más luz real al principio y la zona más oscura al final. Hoy R18 es ya lo más oscuro del mapa (media de cuadro 3 sobre 255 en F0).
- No hay salida al exterior ni final de «escapar».
- Los laboratorios irían en R15–R18 y las señales del exterior en R01–R02, así que la re-tematización de §2 habría que escribirla invertida.

### 1.4 Comparación y recomendación

| | O1 invertir | O2 re-tematizar | O2+ | O3 adaptar contexto |
|---|---|---|---|---|
| Lo que andas coincide con la historia | Sí | No (37 m de bajada) | A medias (25 m de bajada local) | Sí, con otra historia |
| Respeta tu contexto | Sí | Sí en arte, no en el cuerpo | Sí en arte | **No** |
| Datos de Structure que cambian | Los 35 | 0 | 1 cuarto + plano | 0 |
| Encuentros a revalidar | 5 | 0 | 1 | 0 |
| Trabajo del chat 9 | Alto | Ninguno | Bajo | Ninguno |
| Riesgo para el combate | Medio (llegadas desde abajo) | Ninguno | Bajo | Ninguno |
| Luz que crece hacia el final | Sí | Sí | Sí | No |

**Recomiendo O1**, con una condición de orden: que el chat 9 invierta los seis cuartos **antes** de decorarlos. Decorar primero y mover después es pagar dos veces (criterio de `gamedev:level-design`: vestir al final y repetir las comprobaciones tras cualquier cambio de geometría).

Lo práctico es que **la re-tematización de §2 vale igual para O1 y para O2**. Los doce cuartos planos se pueden decorar ya con cualquiera de las dos; lo único que depende de la elección son los números de cota pintados y los seis cuartos con desnivel (R02, R06, R08, R11, R16, R17), que dejaría para el final.

Si el chat 9 no puede asumir O1, mi segunda opción es **O2+**. No recomiendo O3.

---

## 2. Re-tematización de las 12 zonas y los 18 cuartos

La geometría, las coberturas, los spawns y los encuentros no cambian: **solo nombre, decoración y luz**. La tabla supone O1; lo que varía con O2 y O3 está en §2.3.

Los tres tramos coinciden con las sesiones: tramo 1 = S1, tramo 2 = S2, tramo 3 = S3 + S4.

### 2.1 Tabla por cuarto [PROPUESTA]

Cota O1 = metros sobre el suelo del hangar. Niveles: D deterioro, M ocupación robot, E señal del exterior, de 0 a 3.

| ID | Nombre actual | Nombre propuesto (código) | Tramo | Cota O1 | Función narrativa | D / M / E |
|---|---|---|---|---|---|---|
| — | Hangar C-04 (prólogo) | Sin cambios (protegido) | 1 | 0 | Donde despiertas. El fondo del búnker | — |
| R01 | Control de acceso | **Control de cuarentena** (CUA-01) | 1 | 0 | La esclusa que sellaba el ala experimental. Primer combate | 1 / 0 / 0 |
| R02 | Pozo del montacargas | **Pozo de izado** (CUA-02) | 1 | 0 → +12 | Por aquí subían los prototipos. Primera gran subida: miras hacia arriba | 2 / 0 / 0 |
| R03 | Dormitorios | **Criostasis de serie** (LAB-03) | 1 | +12 | Las filas de cápsulas de los demás sujetos. Entiendes que no eras el único | 1 / 0 / 0 |
| R04 | Comedor | **Bancos de pruebas** (LAB-04) | 1 | +12 | Donde se ensayaba a los sujetos. Primera marca de los robots | 1 / 1 / 0 |
| R05 | Enfermería | **Quirófano de ensamblaje** (LAB-05) | 1 | +12 | Respiro. Donde te montaron; registros del proyecto | 1 / 1 / 0 |
| R06 | Armería | **Armería 7** (ARM-06) | 2 | +12 → +18 | Sales del laboratorio a la parte militar. Jaulas abiertas desde fuera | 2 / 1 / 0 |
| R07 | Comunicaciones | **Nodo de comunicación** (COM-07) | 2 | +18 | Los robots lo usan como red propia. Alarma. Primer polvo que cae del exterior | 2 / 1 / 1 |
| R08 | Almacenes | **Almacén tomado** (ALM-08) | 2 | +18 → +22 | Primer nido de reparación grande y primer Big Bot | 2 / 2 / 1 |
| R09 | Taquillas | **Depósito de mantenimiento** (MNT-09) | 2 | +22 | Respiro. Herramienta y cunas de carga vacías | 1 / 1 / 1 |
| R10 | Túnel de la trituradora | **Túnel de servicio T-9** (MNT-10) | 2 | +22 | Persecución. La estructura ya está agrietada | 3 / 2 / 1 |
| R11 | Decantadores | **Decantadores** (AGU-11) | 2 | +22 → +28 | Horda. Agua que se filtra desde arriba | 2 / 2 / 1 |
| R12 | Colector derrumbado | **Colector derrumbado** (AGU-12) | 2 | +28 | El derrumbe trae el primer barro del exterior | 3 / 2 / 2 |
| R13 | Sala de bombas | **Bombas y generadores** (AGU-13) | 2 | +28 | Respiro. La energía que los robots mantienen viva | 2 / 2 / 1 |
| R14 | Fundición | **Forja de reciclaje** (FOR-14) | 3 | +28 | Los robots funden restos de guerra traídos de fuera | 2 / 3 / 2 |
| R15 | Ensamblaje | **Línea de re-ensamblaje** (ENS-15) | 3 | +28 | Se reconstruyen con lo que funden. Máxima ocupación | 2 / 3 / 2 |
| R16 | Reactor | **Toma de aire y filtrado** (AIR-16) | 3 | +28 → +34 | Pico 2. Primer haz de luz de día, por el pozo de ventilación | 3 / 3 / 3 |
| R17 | Centro de mando | **Puesto de defensa** (DEF-17) | 3 | +34 → +37 | Respiro. La última orden: sellar la compuerta | 2 / 2 / 3 |
| R18 | Bóveda | **Compuerta Cero** (SAL-18) | 3 | +37 | Jefe. El Custodio guarda la salida | 3 / 3 / 3 |

Notas:

- Los códigos de zona son pintura (estarcidos). Las clases de puerta (Z entre zonas, A dentro) **no cambian**, porque son geometría: las 12 zonas conservan sus límites y solo cambian de nombre. ACC → CUA, HAB → LAB, FUN → FOR, REA → AIR, MAN → DEF, BOV → SAL; ARM, COM, ALM, MNT, AGU y ENS se quedan.
- **R16 es el cambio más fuerte.** El contexto sitúa la energía en el tramo medio, y un reactor junto a la superficie no encaja. La geometría (un cilindro de Ø 10 con galería en U y dos torres adosadas) se lee igual de bien como toma de aire principal con su tambor de filtros. Y es literalmente «aire y luz natural fría que se filtran». La energía pasa a R13, como grupo generador junto a las bombas.
- Niveles M: mantengo la tabla de `02_DECORACION.md` D1.2 con cuatro ajustes: R04 y R05 de M0 a M1 (primeras marcas), R08 de M1 a M2 (nido), R14 de M2 a M3, R17 de M3 a M2 (es un respiro).
- Niveles D: R04 baja de D2 a D1 (el laboratorio está intacto), R17 sube de D1 a D2 y R18 de D2 a D3 (daño de guerra junto a la superficie).

### 2.2 Qué pasa con cada espacio «humano»

Regla: **ningún objeto de uso personal en uso**. Nada de camas hechas, comida, ropa, tazas ni objetos colocados como si alguien fuera a volver. Los humanos aparecen por lo que construyeron y escribieron.

| Elemento actual (`02_DECORACION.md`) | Problema | Se convierte en |
|---|---|---|
| R03 Dormitorios: literas, «una unidad que vivió allí», camas a medias | Refugio habitado | **Cápsulas de estasis en serie.** Las galerías de literas (+3,5) pasan a ser bastidores de cápsulas. Mismo volumen y misma cobertura |
| R03 mural de la unidad y muro de chapas | Rastro válido | **Se queda**, como emblema del proyecto y panel de placas de serie de los sujetos. Es un registro, no una vivienda |
| R04 Comedor: mesas, comida seca, cocina a +4,5 | Refugio habitado | **Bancos de ensayo** con extremidades sujetas en mordazas. La cocina pasa a ser la cabina de observación elevada |
| R04 barricada «NO PASARÁN» con T-07 empalados | Cuenta una resistencia humana viva | **Barrera de contención de ensayo** (pantallas antiexplosión sobre raíles) reventada. Rastro humano: el estarcido «CONTENCIÓN». Los restos de T-07 se quedan |
| R05 Enfermería: camillas, biombos, quirófano | Cuidado de personas | **Quirófano de ensamblaje**: mesa de montaje con brazos bajo la gran lámpara y mesas de despiece. Las torretas desmontadas que ya preveía el documento encajan tal cual |
| R09 Taquillas: hoja de turno, casco | Objetos personales | **Depósito de mantenimiento**: armarios de herramienta y recambios. Se queda el esquema pintado de la T-9 y un tablero de partes (registro) |
| R07: hojas de turno, auriculares, silla caída | Puesto recién abandonado | Consolas muertas y racks. Se queda el cableado negro nuevo de los robots |
| R14 horno: restos de T-07 «y humanos» | Sensible, y no aporta al contexto | **Solo máquinas y restos de guerra**: cascos de blindado, vainas, carcasas de robot. Ningún resto humano |
| R15: herramientas humanas en los bancos | Rastro válido | Se queda, y se añade un **tablero de siluetas de herramientas** pintado en el muro |
| R17: nota «SELLAR BÓVEDA», taza, abrigos | Taza y abrigos = habitado | Se queda la orden, como texto en pantalla y en el registro: **«SELLAR COMPUERTA»**. Fuera taza y abrigos |
| R18: «contiene lo que el búnker protegía» | Ya no es una bóveda | La compuerta de salida. Lo que el búnker protegía era **el exterior de lo que hay dentro**: tú |

### 2.3 Qué cambia con O2 y con O3

- **Con O2:** la tabla es la misma. Se quitan los números de cota de los rótulos (solo código de zona y nivel). La regla vertical del pozo de R02 (W5) se pinta sin metros o se suprime. El «primer haz de luz de día» de R16 y la compuerta de R18 se mantienen, aunque estén físicamente a −34 y −37 m.
- **Con O3:** la tabla se invierte. Laboratorios en R15–R18 (ensayo, núcleo experimental, registro del proyecto y cámara del experimento), señales del exterior en R01–R02 y luz decreciente. Habría que reescribir §2 y §3; no lo he desarrollado porque contradice el contexto fijado.

---

## 3. Dirección de arte por tramo y por zona

Biblia de arte en tres líneas (criterio de `unity:craft-game-floors`: fijar estilo y paleta antes del primer material):

1. **Estilo:** búnker militar de bloques grandes y cantos vivos, mate y sucio. Lo humano es pintura y texto; lo robot es chatarra soldada y cable negro. Nada brilla salvo lo que importa.
2. **Paleta:** un color dominante por tramo, que pasa de frío artificial a cálido sucio y a frío natural, con el valor subiendo siempre. Rojo, ámbar y verde no existen en la decoración a ≤ 12 m de una puerta.
3. **Lectura:** suelo despejado, perímetro y altura vestidos. La decoración es siempre más oscura que un enemigo a la altura de los ojos.

Materiales comunes (todos existen, inventario C): `Bunker_Wall`, `Bunker_Floor`, `Bunker_Ceiling`, `Bunker_RustMetal`, `Bunker_Grating` y `Bunker_Hazard`; mallas `VO_PipeRun2m`, `VO_CableBundle4m` y `VO_WallVent` con `VO_Prop_Pipe`, `VO_Prop_Cable` y `VO_Prop_Housing`; decales `VO_Decal_Burn`, `_Hazard`, `_Rust` y `_Stain`. Los props nuevos llevan el shader `IronRequiem/HardSurface` en materiales propios, un material por zona (`Decor_Props_<ZONA>`, propuesta D1.3 de `02_DECORACION.md`), sin modificar el shader.

### 3.1 Tramo 1 · Profundidades (R01–R05)

![Tramo 1](concepts/tramo_1_profundidades.png)

| | |
|---|---|
| **Formas** | Ortogonal y limpio. Módulos repetidos en fila (cápsulas, bancos, monitores). Líneas horizontales largas a 1,2 m (zócalo). Cilindros verticales como único contrapunto |
| **Paleta** | Grafito `#232A31`, acero azulado `#414B55`, cerámica de laboratorio `#AEB8BD`, azul hielo `#9FD4FF` (único emisivo), amarillo y negro de peligro solo como pintura |
| **Luz (indicativa)** | Emergencia fría: tiras de zócalo y pocas luminarias débiles. Fondo objetivo 46–50. Es el tramo más oscuro, pero por encima del mínimo de 45 |
| **Materiales** | `Bunker_Wall` y `Bunker_Floor` casi sin óxido; `Bunker_Hazard` en bases; `Decor_Props_CUA` y `_LAB` en el cuadrante blanco clínico y cian del atlas; `VO_Decal_Stain` y `_Hazard`. Sin `VO_Decal_Burn` |
| **Se quita** | Literas, comida, camillas con uso, biombos, papeles personales, la luz ámbar de sodio de R01–R02 como dominante (pasa a fría) |
| **Se añade** | Cápsulas, bancos y pórticos de ensayo, muros de monitores, estarcidos de proyecto, umbilicales al techo |
| **Marca robot** | Mínima y concreta: una cápsula abierta desde fuera, un haz de cables arrancado, un montón pequeño de chatarra en un rincón. Crece de R01 a R05 |
| **Rastro humano** | Estarcidos («SUJETOS 01-12», «CONTENCIÓN»), placas de serie, registros en pantalla, el emblema del proyecto |
| **Señal del exterior** | **Ninguna.** Ni polvo ni grietas: aislamiento total |

Landmarks por zona (ocupan los volúmenes `landmark` que ya define Structure):

| Zona | Landmark 1 | Landmark 2 | Landmark 3 |
|---|---|---|---|
| **CUA** (R01–R02) | Cabina de descontaminación (la garita de R01), estarcido «CUARENTENA C-04» | Plataforma de izado estrellada en el fondo del pozo (R02) | Contrapeso colgando y regla de cota pintada, que ahora se lee hacia arriba (R02) |
| **LAB** (R03–R05) | Muro del proyecto: emblema y placas de sujetos (R03) | Barrera de contención reventada (R04) | Mesa de ensamblaje bajo la gran lámpara (R05) |

### 3.2 Tramo 2 · Infraestructura (R06–R13)

![Tramo 2](concepts/tramo_2_infraestructura.png)

| | |
|---|---|
| **Formas** | Naves altas con masas apiladas e irregulares contra los muros. Diagonales rotas (tuberías dobladas, estanterías volcadas, cables en catenaria). El orden militar se ve debajo del desorden robot |
| **Paleta** | Hollín `#2F261E`, hormigón cálido `#5A4D3F`, kraft `#8A6B43`, óxido `#4A2E20`, verde azulado de agua `#5E9C98` (solo AGU), blanco de soldadura `#CFE6FF` como único acento frío |
| **Luz (indicativa)** | Incandescente sucia (blanco cálido, no el ámbar de las puertas), con chispas de soldadura en los nidos. Fondo objetivo 50–56 |
| **Materiales** | `Bunker_RustMetal` y `Bunker_Grating` dominan; `Bunker_Floor` húmedo en AGU; `VO_PipeRun2m` y `VO_CableBundle4m` en cantidad; `VO_Decal_Rust`, `_Stain` y `_Burn` |
| **Se quita** | Objetos personales de R07 y R09; el verde de terminales de COM como color de zona (se queda solo en pantallas, lejos de puertas) |
| **Se añade** | Montañas de chatarra, nidos de reparación, cables arrancados, tubería reventada, contenedores abiertos, grietas y polvo |
| **Marca robot** | Alta y creciente: chatarra apilada, piezas canibalizadas, nidos con brazos colgantes, cableado negro nuevo que cruza lo humano |
| **Rastro humano** | Estarcidos de inventario y de bahía («B-12»), numeración de contenedores, esquema pintado de la T-9, plano «ESTÁ AQUÍ» en los respiros |
| **Señal del exterior** | **Primeras grietas y polvo.** Grietas finas en muros y techos, conos de polvo en el suelo, agua que gotea desde arriba y, en R12, el primer barro. Ninguna luz natural todavía |

| Zona | Landmark 1 | Landmark 2 | Landmark 3 |
|---|---|---|---|
| **ARM** (R06) | Cámara acorazada «ARMERÍA 7» forzada | Jaulas abiertas de fuera a dentro | Nido de torreta con alimentación tendida por los robots |
| **COM** (R07) | Mástil de antenas: por su lucernario cae el primer polvo | Anillo de consolas muertas bajo una telaraña de cable negro | Alas de racks |
| **ALM** (R08) | Estantería de 8 m volcada, convertida en montaña de chatarra | Nido de reparación | Grúa parada sobre la pila |
| **MNT** (R09–R10) | Esquema pintado de la T-9 (R09) | Cunas de carga vacías (R09) | La trituradora y las grietas del túnel (R10) |
| **AGU** (R11–R13) | Puente giratorio del clarificador (R11) | Tubo colector caído, cascada y lengua de barro (R12) | Tres bombas y el grupo generador (R13) |

Dos avisos para el coordinador (son de luz, no de decoración):

- El mástil de R07 lleva un radar **rojo** y la nota de Structure dice que «sube por un lucernario». Está a más de 12 m de las puertas, así que cumple la regla, pero conviene decidir si ese rojo se mantiene ahora que los ojos de los robots van a ser rojos.
- Las bombas de R13 llevan una luz verde (0,15; 1,00; 0,45) casi igual al verde de puerta abierta (inventario E §5b).

### 3.3 Tramo 3 · Cerca de la superficie (R14–R18)

![Tramo 3](concepts/tramo_3_superficie.png)

| | |
|---|---|
| **Formas** | Monumental y roto. Grandes planos de hormigón partidos, losas colgando, conos de tierra y escombro. Una sola vertical de luz por sala |
| **Paleta** | Polvo `#8A867D`, tierra `#5A4838`, oliva militar `#5E6548`, acero `#45474A`, luz de día fría `#D5E4F5`. En FOR se añade el naranja de forja `#C07A45`, lejos de las puertas |
| **Luz (indicativa)** | Natural fría que entra por brechas, lucernarios y la compuerta. Fondo objetivo 58–72: el tramo más luminoso. La luz real la coloca Iluminación |
| **Materiales** | `Bunker_Wall` y `Bunker_Floor` con mucha suciedad; `Bunker_Hazard` en la compuerta; material de tierra y escombro sobre `Bunker_Floor` teñido; `VO_Decal_Stain` y `_Rust`. El polvo en suspensión es de partículas ambientales |
| **Se quita** | El cian del reactor como luz dominante de R16; el holograma de «la Bóveda» de R17; los restos humanos del horno |
| **Se añade** | Brechas de techo, lenguas de tierra, restos de guerra (blindado, erizos, sacos), haces de luz, la compuerta de salida |
| **Marca robot** | Máxima en R14–R16 (forja, línea, chatarra por todas partes); en R18, la del jefe |
| **Rastro humano** | Defensa de la entrada: erizos, sacos, vehículo, estarcidos de «SALIDA» y la orden «SELLAR COMPUERTA» |
| **Señal del exterior** | **Luz natural fría, tierra y restos de la guerra.** Empieza indirecta en R14–R15, es un haz en R16 y llena la sala en R18 |

| Zona | Landmark 1 | Landmark 2 | Landmark 3 |
|---|---|---|---|
| **FOR** (R14) | Frente de horno con cuatro bocas | Casco de blindado colgado del puente grúa (≥ +6) | Montón de restos de guerra con barro junto a la entrada de carga |
| **ENS** (R15) | Cadena de torsos T-07 colgando | Tablero de siluetas de herramientas | Lucernarios agrietados con polvo en el haz |
| **AIR** (R16) | Tambor de toma de aire Ø 10 con el haz de luz que baja | Dos torres de filtrado | Válvula antiexplosión reventada y lengua de tierra en la galería |
| **DEF** (R17) | Mesa táctica con la maqueta de la compuerta | Troneras: una banda horizontal de luz de día | La orden final en pantalla |
| **SAL** (R18) | Compuerta Cero entreabierta, con rendija de luz | Brecha del techo con haz | Restos de guerra; estructura central según el jefe |

Dos límites que hay que respetar:

- **La compuerta de R18 es decoración, no una puerta.** R18 no tiene salida en los datos («fin de mapa»). La compuerta es un landmark en el muro norte y no debe usar el lenguaje de estado de las puertas: ni rojo, ni ámbar, ni verde. Su única luz es el día que entra por la rendija. La puerta B del jefe sigue siendo del chat 9.
- **Decoración no pone luces** (regla D1.8 de `02_DECORACION.md`). Los haces del tramo 3 son: brecha o grieta (malla), polvo (partículas), una malla de haz aditiva de bajo valor y la `Light` que decida Iluminación dentro de su presupuesto de 8 por cuarto.

### 3.4 Antes y después

Captura F0 actual a la izquierda y lámina del tramo a la derecha. No son la misma sala ni la misma cámara: sirven para comparar ambiente, luz y densidad.

![Antes y después, tramo 1](concepts/antes_despues_tramo_1.png)
![Antes y después, tramo 2](concepts/antes_despues_tramo_2.png)
![Antes y después, tramo 3](concepts/antes_despues_tramo_3.png)

Lo que he visto en las capturas F0 [F0]: los cuartos son bloques con dos o tres luminarias y nada más. En R11 a 15 m el T-07 solo se distingue por su contorno. En R18 casi todo el cuadro es negro (97,9 % de píxeles oscuros en `zones.csv`). No hay ningún elemento que diga dónde estás ni hacia dónde vas.

---

## 4. La ruta completa

El esquema del principio (`concepts/ruta_subida.png`) reúne, cuarto a cuarto: la cota con O1 y la cota actual, el nombre propuesto, el color dominante, la luz (objetivo indicativo en color, medido hoy en blanco, mínimo legible en amarillo) y los tres niveles D, M y E.

Lectura rápida:

- **Color:** frío artificial → cálido sucio → frío natural. El jugador vuelve al frío al final, pero con mucha más luz: no es el mismo azul.
- **Luz:** sube siempre, salvo un pequeño valle en R10 (persecución), que no baja de 50.
- **Ocupación robot:** crece hacia la superficie. Los robots entraron por arriba.
- **Señal del exterior:** nada hasta R06; polvo y grietas de R07 a R13; tierra y luz indirecta en R14–R15; luz directa de R16 a R18.
- **Deterioro:** no es monótono a propósito. Los respiros (R05, R09, R13, R17) bajan un punto para que se lean como descanso.

Los valores medidos hoy van de 3 a 16 sobre 255 (media de cuadro, tomas vacías). El mínimo legible que cita `05_ILUMINACION.md` §L1.3 es 45.

---

## 5. Kit de props mínimo

Parto de los 22 «imprescindibles» de `02_DECORACION.md` D5.2. **Ninguno existe todavía** (inventario C), así que cambiar la lista no desperdicia trabajo hecho.

Todos se harían con scripts de Blender reproducibles, con el patrón de `door_lib.py` y lo que ya propone D5.1: 1 unidad = 1 m, pivote en el centro de la base, mallas `_LOD0/_LOD1/_LOD2`, sin colliders en el FBX y nunca sobrescribiendo un FBX. Sin Tripo, sin Meshy y sin IA generativa: no hay nada que declarar en Steam por esta parte.

Clases de colisión S0–S6 según `02_DECORACION.md` D1.4. Triángulos = LOD0 / LOD1.

### 5.1 Prioridad 0 · Sin esto no se lee el contexto

| # | Pieza | Origen | Función | Tamaño (m) | Tris | Material | Col. | Blender |
|---|---|---|---|---|---|---|---|---|
| 1 | `IR_Capsule` (cerrada, abierta, reventada) | **Nueva** | Firma del tramo 1; sustituye a las literas | 1,4 × 1,4 × 3,0 | 450 / 150 | Atlas + visor emisivo ≤ 1,5 HDR azul hielo | S4 | Sí (lathe) |
| 2 | `IR_ScrapPile` S / M / L | **Nueva** | Firma de la ocupación robot | Ø 1,5 × 1,2 · Ø 3 × 2,4 · Ø 6 × 5 (adosado) | 300 · 600 · 1.200 | Atlas, grafito y óxido, sin emisivo | S1 / S4 / S5 | Sí (dispersión con semilla) |
| 3 | `IR_Wreck` (torso, brazo, pierna, cabeza, chasis) | Previsto n.º 13 | Piezas canibalizadas | 0,9 × 0,5 × 0,7 | 900 / 250 · 300 · 400 · 250 · 500 | Atlas, grafito, sin emisivo | S1 / S3 | Sí |
| 4 | `IR_CableTorn` | Deseable `IR_CableDrop`, **sube a imprescindible** | Cable arrancado colgante | 0,1 × 2–4 | 150 | `VO_Prop_Cable` | S3 | Sí (curva) |
| 5 | `IR_SignQuad` y `IR_SignBlade` | Previsto n.º 16 | Estarcidos y rótulos: el rastro humano | 0,02 de grosor | 2–48 | `Decor_Signage` | S0 / S3 | Sí |
| 6 | `IR_CrackDecal` e `IR_DustCone` | **Nueva** | Primera señal del exterior | Grieta 2–4 de largo · cono Ø 2 × 0,3 | 8 · 120 | `VO_Decal_Stain` (copia) · atlas | S0 / S1 | Sí |
| 7 | `IR_PipeKit` | Previsto n.º 8 | Completa `VO_PipeRun2m`; tubería reventada | Ø 0,2–0,6 | 60–250 | `VO_Prop_Pipe` y atlas | S2 | Sí |
| 8 | `IR_CeilingBeam` | Previsto n.º 12 | Techos (hoy vacíos) | Viga 4 · cercha 8 | 80 · 280 | `Bunker_Ceiling` | S3 | Sí |
| 9 | `IR_Terminal` | Previsto n.º 6 | Consolas y muros de registros | 0,9 × 0,6 × 1,1 | 250 / 80 | Atlas (`_PANELS`) | S4 / S1 | Sí |
| 10 | `IR_Crate` S / M / L | Previsto n.º 1 | Cobertura baja y relleno | 0,6³ · 1,0 × 0,6 × 0,6 · 1,2³ | 120 / 36 | Atlas | S1 / S4 | Sí |

### 5.2 Prioridad 1 · Completan los tres tramos

| # | Pieza | Origen | Función | Tamaño (m) | Tris | Material | Col. | Blender |
|---|---|---|---|---|---|---|---|---|
| 11 | `IR_RepairNest` | **Nueva** (héroe modular) | Nido de reparación: aro, pórtico de tres patas, brazos y cuna | Ø 6 × 5,6 | 1.400 / 450 | Atlas + chispa emisiva ≤ 1,5 HDR | S5 / S4 | Sí |
| 12 | `IR_TestRig` e `IR_TestBench` | **Nueva** (el banco reconvierte `IR_Table`, n.º 15) | Banco de pruebas con pórtico; mesa de despiece | 6,4 × 5,2 × 5,6 · 3,0 × 1,0 × 1,1 | 500 / 160 · 150 / 40 | Atlas + `Bunker_Hazard` | S5 / S4 | Sí |
| 13 | `IR_EarthSpill` 3 m / 6 m | **Nueva** | Tierra y escombro que entran | 3 × 2 × 1 · 6 × 4 × 2,4 (adosado) | 250 · 500 | `Bunker_Floor` teñido | S4 / S5 | Sí |
| 14 | `IR_RoofBreach` e `IR_LightShaft` | **Nueva** | Borde de brecha con varillas; malla de haz sin `Light` | Ø 8 · tronco 8 × 8 | 300 · 12 | `Bunker_Ceiling` · aditivo ≤ 1,0 HDR | S3 | Sí |
| 15 | `IR_WarWreck` | **Nueva** | Casco de blindado medio enterrado | 5,6 × 2,6 × 2,5 | 800 / 250 | Atlas, oliva | S5 / S4 | Sí |
| 16 | `IR_Rubble` | Previsto n.º 11 | Escombro con varilla | 0,3–1,5 · montón 3 | 60–250 · 500 | `Bunker_Floor` y atlas | S1 / S4 | Sí |
| 17 | `IR_Barrier` (sacos, erizo, reja de jaula, placa) | Previsto n.º 10 | Jaulas de ARM; defensa de la entrada | 2,0 × 0,6 × 1,2 | 400 / 120 · 100 · 160 · 80 | Atlas + `Bunker_RustMetal` | S4 | Sí |
| 18 | `IR_Container` 6 m / 3 m | Previsto n.º 14 | ALM y FOR | 6,0 × 2,4 × 2,6 | 220 / 40 | `Bunker_RustMetal` | S5 / S4 | Sí |
| 19 | `IR_Barrel` e `IR_Pallet` | Previsto n.º 2 | Relleno de ALM, FOR, MNT y AGU | Ø 0,6 × 0,9 | 180 / 60 · 90 | Atlas | S1 | Sí |
| 20 | `IR_Locker` | Previsto n.º 3, **re-tematizado** | Armarios de muestras (tramo 1) y de herramienta (R09). Nunca taquilla personal abierta con ropa | 0,5 × 0,5 × 1,9 | 220 / 60 | Atlas | S4 | Sí |
| 21 | `IR_RackServer` | Previsto n.º 7 | COM y registros | 0,8 × 0,9 × 2,2 | 300 / 40 | Atlas | S4 | Sí |
| 22 | `IR_Duct` | Previsto n.º 9 | Conductos, algunos reventados | 1,0 × 0,8 × 4 | 96 · 40 | Atlas | S3 / S2 | Sí |

### 5.3 Prioridad 2 · Héroes, en orden de recorrido

| # | Pieza | Origen | Cuarto | Tris |
|---|---|---|---|---|
| 23 | `IR_AntennaMast` e `IR_RadarDish` | Previsto n.º 18 | R07 | 700 / 250 · 400 |
| 24 | `IR_ClarifierBridge` | Previsto n.º 17 | R11 | 900 / 300 |
| 25 | `IR_PumpHuge` (dos bombas y una variante generador) | Previsto n.º 19 | R13 | 1.500 / 500 |
| 26 | `IR_FurnaceFront` | Previsto n.º 22 | R14 | 1.200 / 400 |
| 27 | `IR_IntakeDrum` | Previsto n.º 21 `IR_ReactorCoreShell`, **re-tematizado** | R16 | 1.800 / 600 |
| 28 | `IR_TacticalTable` y maqueta de la compuerta | Previsto n.º 20, el holograma cambia de contenido | R17 | 600 · 800 |
| 29 | `IR_SurfaceGate` | **Nueva** (depende de D5 y del jefe) | R18 | 1.500 / 500 |

### 5.4 Descartados

| Pieza prevista | Motivo |
|---|---|
| `IR_Bunk` (n.º 4) | Litera: es refugio habitado. La sustituye `IR_Capsule` |
| `IR_Cot` (n.º 5) | Camilla: cuidado de personas. La sustituye `IR_TestBench` |
| `IR_MedScreen` (n.º 5) | Biombo médico. Sin sustituto; si hace falta una pantalla alta, sirve `IR_BarricadePlate` |
| `IR_Curtain` (deseable) | Cortina de enfermería |

Resultado: de los 22 previstos se reutilizan 19 (3 de ellos re-tematizados), se descartan 3 y se añaden 10 nuevos. Total 29 piezas; 10 bastan para empezar.

### 5.5 Reparto tipo por arena

Presupuesto de la capa de decoración por arena (`02_DECORACION.md` D6.1): **≤ 60 draw calls, ≤ 10 SetPass, ≤ 80 k triángulos**.

Modelo de papel: un draw call por cada grupo estático de piezas del mismo material dentro de una tesela de ≤ 10 m, más uno por cada pieza animada o suelta; SetPass = materiales distintos a la vista. **Son estimaciones: nada de esto está medido.** Se mide en F4 con MapFxAudit, cuarto a cuarto.

Triángulos de las mallas del kit VO: `VO_CableBundle4m` cuesta 600 según `02_DECORACION.md` D5.3; `VO_PipeRun2m` y `VO_WallVent` están **sin comprobar** y los supongo ≤ 200 y ≤ 300.

**Tramo 1 · R03 Criostasis de serie (36 × 48 m)**

| Pieza | Inst. | Tris | Material | DC |
|---|---|---|---|---|
| `IR_Capsule` | 28 | 12,6 k | `Decor_Props_LAB` | 8 |
| `IR_Terminal` | 8 | 2,0 k | `Decor_Props_LAB` | 3 |
| `IR_Locker` en fila de tres | 6 | 4,0 k | `Decor_Props_LAB` | 3 |
| `IR_TestBench` | 4 | 0,6 k | `Decor_Props_LAB` | 2 |
| `IR_PipeKit` y `VO_PipeRun2m` | 40 | 6,0 k | `VO_Prop_Pipe` | 6 |
| `IR_Duct` | 14 | 1,3 k | `Decor_Props_LAB` | 4 |
| `IR_CeilingBeam` (16 vigas, 4 cerchas) | 20 | 2,4 k | `Bunker_Ceiling` | 4 |
| `VO_CableBundle4m` e `IR_CableTorn` | 8 + 6 | 5,7 k | `VO_Prop_Cable` | 5 |
| `VO_WallVent` | 6 | 1,8 k | `VO_Prop_Housing` | 2 |
| `IR_Wreck` e `IR_ScrapPile` S | 3 + 1 | 1,8 k | `Decor_Props_LAB` | 2 |
| Estarcidos | 14 | 0,3 k | `Decor_Signage` | 3 |
| Decales (dos tipos) | 16 | 0,1 k | 2 `VO_Decal_*` | 4 |
| **Total** | **≈ 174** | **≈ 39 k** | **8 materiales** | **≈ 46** |

**Tramo 2 · R08 Almacén tomado (48 × 56 m)**

| Pieza | Inst. | Tris | Material | DC |
|---|---|---|---|---|
| `IR_ScrapPile` (3 L, 6 M, 10 S) | 19 | 10,2 k | `Decor_Props_ALM` | 8 |
| `IR_RepairNest` | 1 | 1,4 k | `Decor_Props_ALM` | 2 |
| `IR_Wreck` (6 torsos, 8 brazos, 6 piernas, 2 chasis) | 22 | 11,2 k | `Decor_Props_ALM` | 5 |
| `IR_Crate` (10 L, 16 M, 20 S) | 46 | 5,5 k | `Decor_Props_ALM` | 6 |
| `IR_Barrel` e `IR_Pallet` | 18 + 10 | 4,1 k | `Decor_Props_ALM` | 4 |
| `IR_Container` 6 m | 8 | 1,8 k | `Bunker_RustMetal` | 4 |
| `IR_PipeKit` y `VO_PipeRun2m` | 24 | 3,6 k | `VO_Prop_Pipe` | 5 |
| `VO_CableBundle4m` e `IR_CableTorn` | 6 + 14 | 5,7 k | `VO_Prop_Cable` | 5 |
| `IR_Duct` | 10 | 1,0 k | `Decor_Props_ALM` | 3 |
| `IR_CeilingBeam` | 20 | 2,4 k | `Bunker_Ceiling` | 4 |
| `IR_CrackDecal` e `IR_DustCone` | 6 + 4 | 0,5 k | `VO_Decal_Stain` · `Decor_Props_ALM` | 3 |
| Estarcidos | 10 | 0,2 k | `Decor_Signage` | 3 |
| Decales (tres tipos) | 18 | 0,1 k | 3 `VO_Decal_*` | 6 |
| **Total** | **≈ 236** | **≈ 48 k** | **10 materiales** | **≈ 58** |

**Tramo 3 · R16 Toma de aire y filtrado (52 × 50 m)**

| Pieza | Inst. | Tris | Material | DC |
|---|---|---|---|---|
| `IR_IntakeDrum` | 1 | 1,8 k | `Decor_Props_AIR` | 2 |
| `IR_RoofBreach` e `IR_LightShaft` | 2 + 3 | 0,6 k | `Bunker_Ceiling` · aditivo | 4 |
| `IR_EarthSpill` (3 de 6 m, 5 de 3 m) | 8 | 2,8 k | `Bunker_Floor` teñido | 5 |
| `IR_Rubble` (4 montones, 24 trozos) | 28 | 5,6 k | `Bunker_Floor` teñido | 6 |
| `IR_WarWreck` | 2 | 1,6 k | `Decor_Props_AIR` | 2 |
| `IR_Barrier` (6 erizos, 6 muros de sacos) | 12 | 3,4 k | `Decor_Props_AIR` | 4 |
| `IR_ScrapPile` (4 M, 6 S) | 10 | 4,2 k | `Decor_Props_AIR` | 5 |
| `IR_Wreck` | 8 | 4,0 k | `Decor_Props_AIR` | 3 |
| `IR_PipeKit` y `VO_PipeRun2m` | 20 | 3,0 k | `VO_Prop_Pipe` | 4 |
| `IR_CableTorn` | 10 | 1,5 k | `VO_Prop_Cable` | 3 |
| `IR_Duct` (algunos reventados) | 12 | 1,2 k | `Decor_Props_AIR` | 3 |
| `IR_CeilingBeam` | 18 | 2,6 k | `Bunker_Ceiling` | 4 |
| `IR_CrackDecal` e `IR_DustCone` | 10 + 6 | 0,8 k | `VO_Decal_Stain` · `Decor_Props_AIR` | 3 |
| Estarcidos | 10 | 0,2 k | `Decor_Signage` | 3 |
| Decales (dos tipos) | 16 | 0,1 k | 2 `VO_Decal_*` | 4 |
| **Total** | **≈ 176** | **≈ 33 k** | **10 materiales** | **≈ 55** |

En R08 y R16 cuento el material de la grieta como uno más (copia de `VO_Decal_Stain`); si comparte material con un decal ya presente, son 9.

Los tres caben en papel. Los puntos justos son los draw calls de R08 (58 de 60) y el SetPass de R08 y R16 (10 de 10). Si al medir se pasan, la primera palanca es reducir a dos los tipos de decal por sala, y la segunda fundir montones de chatarra vecinos en una sola malla por tesela.

El cuello de botella medido del juego es la CPU, no la GPU (`VISUAL_AUDIT_F0.md`, inventario C §3.2). Por eso importa más el número de materiales y de objetos que el de triángulos.

---

## 6. Reglas para no tapar el combate

Se suman a las reglas K-1 a K-14 de `02_DECORACION.md` D1.5. Donde dos reglas se pisan, vale la más estricta.

| # | Regla | Cómo se comprueba |
|---|---|---|
| C-1 | **Nada en los carriles.** Ninguna pieza con volumen dentro de los `freeVolumes` de Structure (ruta crítica, carril de combate, carril de slide, aproximación a puerta, despeje de spawn, anillo, embudo) | Validador contra `Structure/R*.json` |
| C-2 | **Nada a < 1,5 m de un spawn ni de una puerta**, y ningún collider a ≤ 2,5 m del centro de un slot (K-3, más estricta). Los volúmenes libres de puerta (Z 10 × 6 × 6, A 7 × 4 × 4,5) quedan vacíos | Validador |
| C-3 | **Las coberturas son las de Structure.** La decoración las viste como piel sin collider; no añade ni quita coberturas ni cambia su altura (1,1–1,2 m o ≥ 2,4 m) | Revisión de datos: 0 colliders nuevos dentro de los bounds de arena |
| C-4 | **La decoración es más oscura que un enemigo.** Entre 0,5 y 2,5 m de altura: mate, poco saturada, sin emisivo ni borde de luz. En la captura de control a 15 m, la silueta de decoración más clara debe medir ≤ 0,7 × el valor del enemigo (misma medida que F0, `enemy_val`) | Captura con el método de F0 |
| C-5 | **Ningún resto de robot de pie.** Los restos van tumbados, colgados a ≥ +4,5 m o dentro de un montón. Nada con silueta de bípedo entre 1,6 y 2,4 m de alto sobre el suelo | Revisión visual por cuarto |
| C-6 | **Sin piezas flotantes.** Todo apoya en suelo, muro o techo, o cuelga de algo que se ve. Los cables arrancados salen de un panel o de una bandeja | Revisión visual; el validador comprueba el contacto de la caja con una superficie |
| C-7 | **Colgantes a ≥ +4,5 m** sobre cualquier suelo pisable (K-7), y nada bajo las pasarelas | Validador |
| C-8 | **Sin rojo, ámbar ni verde** en emisivos o luz de decoración a ≤ 12 m de una puerta. Emisivo máximo 1,5 HDR | Test EditMode ya previsto en el prompt 12 |
| C-9 | **El landmark se ve desde la entrada** y la salida se lee sin luz de estado (rótulo y forma) | Captura desde el pórtico |
| C-10 | **El suelo de arena queda limpio**: escombro ≤ 0,05 m fuera del perímetro (K-12), para que se vean armas y módulos caídos | Validador |
| C-11 | **La tierra y el escombro del tramo 3 van adosados** a muros o bajo las brechas, nunca en mitad de un carril; holguras de 0 o ≥ 4 m (≥ 5 m con Big Bot) | Validador |
| C-12 | El NavMesh se hornea **después** de colocar la decoración y se repite el test de camino de cada slot al jugador | Tests de Map01 |

Preguntas para el recorrido de prueba de cada cuarto en F4 (criterio de `gamedev:level-design`, «playtest por informe»): ¿dudaste en algún momento de hacia dónde ir?, ¿confundiste alguna pieza con un enemigo?, ¿te enganchaste en algo al hacer slide o dash?, ¿algún enemigo quedó tapado por decoración?

---

## 7. Orden propuesto para F4

1. Tú eliges D1 a D6.
2. Piezas de prioridad 0 (10 piezas) y un cuarto piloto por tramo, de los planos: **R03**, **R09** y **R15**. Captura antes y después y presupuesto medido en cada uno.
3. Con los tres pilotos aprobados, el resto de cuartos planos.
4. Los seis cuartos con desnivel, cuando el chat 9 haya resuelto O1 (o directamente, si eliges O2).
5. Héroes, en orden de recorrido.

Si el chat 9 está activo en Map01, paro y te pregunto antes de tocar `Editor/Map01/` o `Prefabs/Map01/`.

---

## 8. Contradicciones y dudas que señalo (no cambio nada)

| # | Asunto | Qué necesito |
|---|---|---|
| 1 | El mapa baja y la historia sube (C1) | Tu elección en §1 |
| 2 | El hangar figura como «Superficie». Puede que su cinemática enseñe cielo (**sin comprobar**) | Que confirmes que el hangar es el fondo del búnker, o que me digas qué se ve arriba |
| 3 | R16 es un reactor y el contexto pone la energía en el tramo medio | Aprobar el cambio a toma de aire, o decirme que se queda como reactor |
| 4 | R18 no tiene salida en los datos. La «salida» sería decoración y un fin de mapa que aún no existe (`Map01BossEncounter`, F4 del chat 9) | Aprobar D5 |
| 5 | El checklist dice «Map01 oscuro: sin subir el ambiente» (`CHECKLIST_TRABAJO.md:1541`, inventario E) y este pase pide más luz | Es del coordinador, pero afecta a mis objetivos indicativos |
| 6 | Rojo del radar de R07 y verde de las bombas de R13, frente a ojos rojos y puertas | Decisión de las secciones a) y c) |
| 7 | Los nombres Dormitorios, Comedor y Enfermería los propusiste tú como «ajustables» (`PROMPTS_CHATS.md` l.304, inventario C) | Confirmar que los cambio |
| 8 | Las escenas de la campaña antigua (Room00b, Room01–04, Pursuit05) tampoco tienen decoración, pero Map01 las reemplaza o deriva (decisión U6) | Propongo no decorarlas. Dime si quieres lo contrario |

---

## 9. Imágenes y cómo regenerarlas

| Archivo | Qué es |
|---|---|
| `concepts/ruta_subida.png` | Esquema de la ruta en sección, 2880 × 1620 |
| `concepts/tramo_1_profundidades.png` | Lámina del tramo 1, 1280 × 720, Eevee |
| `concepts/tramo_2_infraestructura.png` | Lámina del tramo 2 |
| `concepts/tramo_3_superficie.png` | Lámina del tramo 3 |
| `concepts/antes_despues_tramo_1.png`, `_2.png`, `_3.png` | Captura F0 junto a la lámina, 1928 × 540 |
| `concepts/src/fable_lib.py` | Librería común |
| `concepts/src/tramos_concepts.py` | Las tres láminas |
| `concepts/src/ruta_subida.py` | El esquema; lee `F0/zones.csv` |
| `concepts/src/antes_despues.py` | Las composiciones |
| `concepts/src/tramos_report.json` | Media y porcentaje de píxeles oscuros de cada lámina |

```
cd Docs\IronRequiem\VisualPassFable\concepts\src
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --factory-startup --python tramos_concepts.py
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --factory-startup --python ruta_subida.py
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --factory-startup --python antes_despues.py
```

Las he mirado todas antes de entregar. Medias de cuadro: 56, 67 y 74 sobre 255 (tramos 1, 2 y 3, con la franja de leyenda incluida); ninguna sale negra. Corregí cuatro rondas: leyenda que se pisaba, cerchas que tapaban la maqueta, estarcidos ocultos tras las pilastras y el tramo 3 quemado.

Límites de las láminas: son maquetas de bloques para elegir dirección, no assets ni capturas de motor. La luz es de Eevee, no de URP: el ambiente es orientativo. En la del tramo 3, el estarcido del muro izquierdo queda tapado por la losa del techo.

---

## 10. Skills usadas

| Skill | Qué he aplicado | Qué no |
|---|---|---|
| `unity:craft-game-floors` | Biblia de arte de tres líneas antes de cualquier material (§3). «Un suelo rellena un valor por defecto, nunca una decisión del desarrollador»: conservo kit, shader y paleta existentes. Alcance de solo evaluación: propuesta sin cambios en el proyecto | No abrí sus referencias ni las páginas de Unity que enlaza; no he puntuado capturas con su tabla (eso es F4 y F6). Ignoré todo lo que toca el proyecto |
| `gamedev:create-game-assets` | Inspeccionar antes de inventar (§1.2, kits existentes). Nombrar el sistema visual: formas, valores, papeles de la paleta, materiales, densidad (§3). Manifiesto de piezas con tamaño, tris, material, colisión y origen (§5). Aprobar un objetivo visual antes de producir la familia (láminas y cuartos piloto). Familias, no piezas sueltas. Registro de procedencia: todo procedural, sin IA generativa | Sus scripts de control de imagen necesitan Pillow, que no está instalado, y no instalo nada: medí las láminas con numpy dentro de Blender |
| `gamedev:level-design` | Vestir al final y repetir comprobaciones tras cambiar geometría (orden de O1, §1.4 y §7). Guiar con luz, líneas y landmarks, no con muros (§3, C-9). Ritmo en diente de sierra: los respiros llevan la carga narrativa (§4). Telegrafiar lo irreversible. Preguntas concretas de recorrido (§6) | No he medido métricas del jugador: uso las de `MAP01_DESIGN.md` §4. Sus ejemplos son de Godot |
| `game-dev-combat-animation` | Se aplica en `01e_CARRONERO.md` | — |
| `iron-requiem-fps` (local) | No tocar jugador, cámara ni armas; la decoración respeta las métricas de movimiento ya fijadas (slide de 12 m, dash, salto de 2,03 m) | — |
| `iron-requiem-performance` (local) | Los presupuestos salen de medidas, no de límites universales: los repartos de §5.5 se marcan como papel. Evitar instancias de material accidentales (un material por zona, sin MaterialPropertyBlock). Misma carga antes y después | No hay ninguna medida nueva: no abro Unity |
