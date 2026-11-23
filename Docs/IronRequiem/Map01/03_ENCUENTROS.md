# MAPA 1 · 03 · Encuentros (R3) — Parte A

**Estado:** F1, Parte A: todo lo que no depende de la lista de cuartos. La Parte B (asignación por cuarto) espera a `01_ESTRUCTURA.md`. Autor: R3 (opus), 18/11/2026.

**Alcance:** diseño en papel. No se abrió Unity, no se usó la CLI ni git que escriba. Solo se leyeron documentos, código y prefabs. Único archivo escrito: este.

## Convenciones de este documento

- **Fuentes** como `ruta:línea`. Abreviaturas:
  - `CHK` = `Docs/IronRequiem/CHECKLIST_TRABAJO.md`
  - `INV` = `Docs/IronRequiem/Map01/00_INVENTARIO.md`
  - `MD` = `Docs/IronRequiem/Map01/MAP01_DESIGN.md`
  - `DV02` = `Docs/IronRequiem/DESIGN_V02_UPDATE.md`
  - `MAPK` = `Docs/IronRequiem/MODULAR_ARM_AND_PICKUP.md`
  - `BP` = `Docs/IronRequiem/BUNKER_POLISH.md`
  - `S/` = `Assets/IronRequiem/Scripts/` · `P/` = `Assets/IronRequiem/Prefabs/`
- **Marcas:**
  - **[SUPUESTO]**: cifra o comportamiento propuesto, sin dato en el proyecto.
  - **[LECTURA]**: deducido de código o prefabs, sin medir en Play.
  - **[DECISIÓN USUARIO]**: lo decide el usuario en la PARADA de F1.
  - **[R0]**, **[R1]**, **[R4]**, **[R5]**: requisito o aviso para ese rol.
- **Códigos de enemigo** (variantes vigentes, INV §3):

| Código | Enemigo | Prefab |
|---|---|---|
| T07 | T-07 común | `P/Campaign/T07_Current_Repaired.prefab` |
| T07M | T-07 Mech | `P/Campaign/T07_Mech.prefab` |
| SAW | SawBot | `P/Campaign/SawBot_Current.prefab` |
| BIG | Big Bot | `P/Campaign/BigBot_Current.prefab` |
| HOV | HoverBot (dron) | `P/Campaign/HoverBot_Current.prefab` |
| TUR | Torreta | `P/Campaign/Turret_Current.prefab` |
| CAR | Carroñero | `P/Campaign/Carronero.prefab` |

- **«Ejecutable»** = enemigo que entrega un arma al ejecutarlo con vida < 75 %: hoy T07, T07M y SAW. El CAR no cuenta como fuente hasta resolver D-E3.

---

## A0. Resumen

1. Hoy las armas solo se consiguen **ejecutando**: T07 y T07M dan el cañón; SAW da la motosierra. El jugador **solo puede llevar un arma robada a la vez** (`S/Player/StolenWeaponCarrier.cs:88,160`). Por eso cada combate se diseña como un ciclo: debilitar → ejecutar (2,5 m, ≈ 2,7 s de protección) → gastar → OVERDRIVE (el arma se bloquea 5 s) → lanzarla como granada (100 de daño, radio 4,5 m) → siguiente ejecución.
2. **Rendimiento de cada arma** (detalle en A3):
   - Cañón: ≈ 195 de daño directo (3 cargas completas) más una granada de hasta 100.
   - Motosierra: ≤ 300 (6,7 s de contacto) más una granada de hasta 100.
   - Arma principal: ≈ 65 DPS en ráfaga y ≈ 26 DPS sostenidos [LECTURA].
   - Un BIG (960 PV) cuesta ≈ 30 s solo con la principal y 10–15 s con 2 armas robadas. **Cada BIG exige ≥ 2 ejecutables en su ronda.**
3. **Orden de introducción:** CAR → T07 (tutorial de ejecución) → HOV → SAW → respiro → TUR → alarma → BIG → T07M → persecución → horda grande → combinaciones → jefe (A2).
4. **Diez arquetipos de encuentro** con requisitos de arena para R1 (A4). Los más restrictivos:
   - Espacio del BIG: anillo libre ≥ 6 m; slot de Ø 5 m × 4,6 m de alto; aparece a ≥ 8,25 m del jugador.
   - Nido de torreta conectado al NavMesh.
   - Persecución alineada con +Z del mundo y ≤ 16 m de ancho.
   - Toda superficie pisable por el jugador a ≤ 3 m de NavMesh conectado.
   - Bounds de la arena sin incluir la cámara de esclusa.
5. **Rondas** (A5):
   - ≤ 8 unidades y ≤ 12 cuerpos.
   - `variedSpawns = true` siempre: el camino no variado lanza una excepción si no hay slot (`S/Levels/CombatTwoRoomsLab.cs:80`).
   - Rondas ≥ 4: `secondRoom` = copia de la arena.
   - Slots ≥ 1,5 × cuerpos, en ≥ 3 grupos separados ≥ 15 m.
6. **Respiros:** un cuarto sin rondas solo abre la salida si alguien llama a `Release()` (`S/Levels/CampaignRoom.cs:20,23`). Hace falta un componente nuevo; sin él hay soft-lock (A6).
7. **Jefe:** tres conceptos (A7). Recomiendo **A, «El Custodio»**: un Big Bot pesado cuyos cañones se arrancan destruyendo sus uniones. Es la recompensa por los ocho Big Bots del mapa, que nunca dan arma (B6) [cambio B].
8. **Datos F2:** esquema `Data/Encounters/R<NN>.json`, su correspondencia campo a campo y las reglas del validador de `EncounterAuthoring` (A8).

### Decisiones para el usuario

| # | Decisión | Opciones | Recomendación de R3 |
|---|---|---|---|
| D-E1 | **Obtención de armas** | (a) Solo ejecución (estado actual, D1). (b) Volver al pilar original: destruir la unión desprende el arma, en todos. (c) Híbrido: ejecución en todo el mapa; además, desprendimiento por unión **solo** en T07M (unión de 35 PV, `T07_Mech.prefab:47642` `executionOnly: 1`) y en el jefe | **(c).** Conserva el flujo probado (ejecución con protección) y añade una forma de conseguir arma **a distancia**: disparar la unión desarma al Mech, que deja de disparar y se retira (`ENEMY_V01.md:24`). El pilar original aparece donde más luce. Coste: `executionOnly = 0` en `T07_Mech.prefab`, que es un cambio de balance. Antes hay que verificar que el módulo desprendido sea recogible en esa variante: los desprendimientos recogibles se apagaron por campos opcionales (CHK:432, CHK:620). Con (a), el mapa funciona igual; con (b), la economía de A3 se duplica y hay que reducir fuentes |
| D-E2 | **Checkpoints por zona** | (a) Ninguno: morir recarga toda la sesión (`S/Levels/CombatLabMenu.cs:38-40`). (b) Checkpoint en cada compuerta de zona (clase Z) | **(b).** Con ≥ 15 cuartos, una partida de 45–60 min [SUPUESTO] repetida entera desde cada muerte castiga demasiado. Al reaparecer: estado de la entrada de zona, vida = máx(vida al entrar, 50) [SUPUESTO] y sin arma robada. Exige código que toca `CombatLabMenu`/`CampaignFlow`, así que necesita aprobación. Si se elige (a), R3 rebaja ≈ 15 % las unidades del tercer tercio |
| D-E3 | **¿El Carroñero es ejecutable?** | Hoy no está claro: el checklist dice que no (CHK:866), pero el prefab hereda `EnemyExecutionSource` activo (INV §B, hallazgo 4) | **No ejecutable** (`executionEnabled = 0` en `Carronero.prefab`). Si lo fuera, un enemigo de 26 PV daría una motosierra completa (100 de energía, módulo heredado del SawBot [LECTURA]). Eso trivializa las hordas y llena la pantalla de avisos [E]. Mientras no se verifique en el Editor, este diseño **no cuenta al CAR como fuente** |
| D-E4 | **Jefe** | A «El Custodio» · B «La Matriarca» · C «El Panóptico» (A7) | **A.** C es el plan B si el prototipo de F4 falla por escala o NavMesh |
| D-E5 | Pausa entre rondas (adicional) | Hoy 0 s: la siguiente ronda entra en el mismo frame (`CombatTwoRoomsLab.cs:66`). Añadir 1–2 s exige cambiar ese código | **Mantener 0 s en F2** y decidir tras jugar. El diseño compensa con slots lejanos (≥ 10 m) y con «líderes lentos» (BIG lejos) |
| D-E6 | NavMesh del BIG y del jefe (adicional) | Hoy hay un solo tipo de agente, de radio 0,5 (`ProjectSettings/NavMeshAreas.asset:76`). El BIG mide 2,25 de radio en su componente (`P/Enemies/BigBot_Combat_v01.prefab:1404`) y planifica sobre un NavMesh que no lo considera. Un segundo tipo de agente cambia ProjectSettings | **En F2, regla de geometría** (rutas de BIG ≥ 5 m, sin pasos estrechos que conecten con su zona) **y medición.** El tipo de agente «Grande» solo si en F2 se atascan |

### Avisos de coordinación (no son decisiones del usuario)

- **[R0] Arma inicial.** En la campaña actual el jugador empieza solo con el puño, y `WeaponMountStation` monta el arma principal una vez por dominio (`S/Levels/WeaponMountStation.cs:52`; CHK:1079-1081). Map01 debe garantizar el arma principal antes del primer combate con T07: una estación única en R01 o en C01, o empezar después del hangar.
- **[R0] Texto fijo.** Toda sala necesita `prologueTitle`. Sin él, el HUD dice «TRES RONDAS» (`S/Levels/CampaignFlow.cs:273`).
- **[R5] Luces de `CorridorAlarm`.** El componente referencia luces que son de Iluminación. Encuentros las enlaza por ID de anclaje (A8).

---

## A1. Roster

### A1.1 Enemigos vigentes

| Código | Rol táctico | PV | Detección / ataque | Velocidad | Daño (fuente) | Telegrafía | Arma que da y cómo | Unidades | Coste de aparición | Geometría: fuerte / débil |
|---|---|---|---|---|---|---|---|---|---|---|
| **T07** | Francotirador; **fuente de cañón** | 128 | 45 / 38 m | 4,2 | 22 por disparo (18–26 cargado), proyectil a 90 m/s, memoria 6 s (CHK:769). Tolera 0,45 s de bloqueo durante la carga (CHK:788). Cota de presión ≤ 18 PV/s por T07 [LECTURA: 22 / 1,2 s] | Carga magenta de 1,2 s en la boca (CHK:769, 831) | Cañón por ejecución < 75 %: hace falta ≥ 33 de daño (6 impactos de la principal o 1 puñetazo cargado) | 1 | **Alto:** los T07 suman el 75–85 % del coste de aparición en el laboratorio (BP:95). La primera activación de la sesión, ≈ 140 ms para 8 sin cola (BP:131) | **Fuerte** en líneas largas (≥ 25 m) sin cortes y desde balcones. **Débil** si la línea de visión se corta cada ≤ 10 m o hay coberturas altas y esquinas: el jugador acorta distancia para ejecutar |
| **T07M** | Francotirador blindado; fuente de cañón (misma arma) | 128 (unión 35) | Sin verificar [SUPUESTO: = T07] | 3 (agente) / 4,2 (`NavigationModule`) | Sin verificar; dispara desde el cañón con el brazo apuntando (CHK:847-852) | Carga en la boca (CHK:852) | Cañón por ejecución. Con D-E1(c), también al destruir la unión de 35 PV | 1 | Sin medir; Animator con IK de pies (CHK:795) [SUPUESTO ≥ T07] | Como T07. Más lento: buen blanco en distancias medias |
| **SAW** | Hostigador cuerpo a cuerpo rápido; **fuente de motosierra** | 140 | 22 / 3 m (entra a 1,1 m, sale a 1,4 m: CHK:515) | 9,5; aceleración 34 (CHK:770) | 20 PV/s en contacto (CHK:517) | Preparación de 0,2 s, muy corta (CHK:770); motor audible (CHK:542) | Motosierra por ejecución < 75 %: hace falta ≥ 36 de daño | 1 | Sin medir | **Fuerte** en pasillos estrechos, recodos y esquinas ciegas. **Débil** en bucles alrededor de un obstáculo: el jugador va a 13 m/s, el SAW a 9,5 |
| **BIG** | Tanque de artillería; niega zonas | 960 | 36 / 28,6 m | 1,68; giro 60°/s (CHK:273) | Ráfaga de 6 proyectiles explosivos de 12 PV con radio 2,88 m (CHK:1068). 0,15 s entre tiros y 1 s de pausa con el aviso incluido (CHK:442). Proyectil a 22 m/s con 48 m de recorrido (CHK:441). **Hasta 72 por ráfaga** [cálculo]. Con un solo cañón, ráfaga de emergencia cada 0,12 s (CHK:442). Cualquier collider le corta la línea de tiro, también otros enemigos (BP:123) | Aviso de 0,7 s | **Ninguna** (no ejecutable: CHK:1067) | 2 | 1–1,5 ms cada uno (BP:95). La primera explosión crea el pool y provoca un pico (CHK:1091, 1094) | **Fuerte** en espacios abiertos y planos de ≥ 30 m y en pasillos sin salida lateral. **Débil** ante columnas altas y anchas (cortan la línea de tiro y la salpicadura), anillos para rodearlo y alturas |
| **HOV** | Hostigador ligero; presión vertical | 80 | 20 / 12,5 m | 4,2 | 4 cada 1,5 s; proyectil a 70 m/s (CHK:768) | No consta | Ninguna. Su botiquín se anula en las rondas (`CombatTwoRoomsLab.cs:132`) | 1 | Sin medir | **Fuerte** en salas altas con coberturas bajas. **Débil** en techos bajos y pasillos |
| **TUR** | Negación de área a larga distancia; francotirador fijo | 320 | 45,75 / 43,75 m | Fija (CHK:419, 430; sin reverificar en `Turret_Current`) | 2,4 por disparo (CHK:430); cadencia sin dato | No consta | Ninguna | 1 | Sin medir | **Fuerte** en posiciones elevadas que ven la planta. **Débil** ante flanqueo por detrás, coberturas altas en la aproximación y, [SUPUESTO], ángulo muerto bajo su plataforma |
| **CAR** | Horda; presión de posición | 26 | 22 / 3 m; detección cada 0,1 s | 7,2 | 4,5 PV/s (CHK:866) | No consta; sierra girando (CHK:867) | Ninguna en este diseño (D-E3) | **0,5** | **Bajo:** optimizado (CHK:869) | **Fuerte** en espacios abiertos donde rodea. **Débil** en embudos (puertas de 4–6 m) ante la motosierra; cae con 4 impactos de la principal |

### A1.2 Recursos del jugador (base de los cálculos)

| Recurso | Valor | Fuente |
|---|---|---|
| Vida | 100, sin regeneración | `Assets/FPS/Prefabs/Player.prefab:473`; CHK:1057 |
| Botiquín | 25 PV. El prefab trae **40**; la campaña fija 25 en cada instancia. Solo se consume si falta vida | `Assets/FPS/Prefabs/Pickups/Pickup_Health.prefab:86`; `Assets/IronRequiem/Editor/CampaignAuthoring.cs:176`; INV §C3 |
| Arma principal `Weapon_IR_ArmCannon_VO` | 6,5 por proyectil, intervalo de 0,1 s y 2,5 de calor por disparo (máximo 100, retardo 0,6 s, enfriamiento 20/s, recuperación a 35) | `P/VisualPass/VFX/VP_Bolt_Primary.prefab:73-74`; MAPK:7; `P/Weapons/Weapon_IR_ArmModule_v07_ExtendedHeat.prefab:23`; `S/Weapons/WeaponHeat.cs:10-14` |
| → rendimiento de la principal | **65 DPS durante 4 s** (40 disparos = 260) hasta el bloqueo; después, ciclos de ≈ 6,45 s con 169 de daño: **≈ 26 DPS sostenidos** | [LECTURA, sin medir] |
| Puño | Golpe rápido. Cargado: 95 de daño, 1,2 s de carga, aturde 0,75 s | CHK:1073; `S/Player/ProtagonistMelee.cs:35` |
| Ejecución | Vida < 75 %, a 2,5 m. Protección desde la entrega del arma, durante la animación y 2 s más. El T07 muere a los 0,733 s → **≈ 2,7 s de protección** | BP:21-38; `StolenWeaponCarrier.cs:12`; CHK:451 |
| Límite de armas robadas | **Una a la vez**; con un arma robada, el aviso de ejecución no aparece | `StolenWeaponCarrier.cs:88,160` |
| Slide | 0,42 s de invulnerabilidad; 1,5 s de enfriamiento | CHK:1062 |
| Persistencia | El arma robada viaja entre cuartos. Lo lanzado o caído en un cuarto desaparece al descargarlo | CHK:617; `S/Levels/CampaignFlow.cs:227-228` |
| Módulos en el mundo | Recogibles 120 s, máximo 8 a la vez | MAPK:19 |

### A1.3 Tiempo para matar (blancos inmóviles, 100 % de acierto) [LECTURA]

| Enemigo | PV | Umbral de ejecución | Principal (impactos / tiempo) | Cañón (cargas de 65) | Motosierra (s de contacto) |
|---|---|---|---|---|---|
| T07 / T07M | 128 | ≥ 33 de daño | 20 / 1,9 s | 2 | 2,9 s |
| SAW | 140 | ≥ 36 de daño | 22 / 2,1 s | 3 (dos cargas = 130) | 3,2 s |
| HOV | 80 | — | 13 / 1,2 s | 2 | 1,8 s |
| TUR | 320 | — | 50: más que una barra de calor (40) → ≈ 8–9 s | 5: **más de un cañón** | 7,2 s: **más que los 6,7 s útiles** |
| CAR | 26 | — | 4 / 0,3 s | 1 con carga ≥ 3 %. La carga mínima hace 25 y lo deja a 1 PV | 0,6 s |
| BIG | 960 | — | 148 / **≈ 30 s** con el calor | 15 = **5 cañones** | 21 s = 3,2 motosierras |

---

## A2. Introducción progresiva

**Principios aplicados** (design-game-encounters, tune-enemy-ai):

- Una fuente de presión nueva cada vez.
- Cada tipo nuevo aparece **solo o en ventaja**: lejos, lento o con una fuente de arma a mano.
- Cada arquetipo pide una respuesta distinta.
- Tope de atacantes comprometidos a la vez (A5.1).

Las zonas son orientativas: R1 decide la asignación real.

### A2.1 Orden de introducción

| # | Tipo / hito | Posición | Zona orientativa | Primera aparición: composición y condiciones | Lección |
|---|---|---|---|---|---|
| 1 | **CAR** | Primer tercio, inicio | Esclusas de acceso | 2–3 CAR solos en una arena E1. El jugador ya tiene la principal | Disparar y moverse; la horda no dispara |
| 2 | **T07** y **tutorial de ejecución** | Primer tercio | Esclusas de acceso → Dormitorios | Ronda 1: **un T07 solo** a 15–25 m, con coberturas para acercarse. Ronda 2: 1 T07 + 2 CAR, con el slot del T07 al final de una ruta cubierta. El aviso «[E] Ejecutar y tomar arma» sale solo (CHK:431) | Debilitar a < 75 %, acercarse a 2,5 m, ejecutar, cargar el cañón (1,2 s), llegar a OVERDRIVE y **lanzarlo** |
| 3 | **HOV** | Primer tercio | Dormitorios / Comedor | 2 HOV + 1 T07 como fuente | No todos los enemigos dan arma; apuntar en vertical |
| 4 | **SAW** | Final del primer tercio | Comedor | Ronda 1: **un SAW solo** en una arena E2 con bucle alrededor de las mesas, para escapar dando vueltas. Ronda 2: 1 SAW + 4 CAR | La motosierra contra la horda pequeña; el SAW llega primero porque es el más rápido |
| — | **Respiro** | Fin del primer tercio | Enfermería | 0 enemigos; 2–3 botiquines | Recuperar el aliento; anticipar lo que viene (decoración: torretas desmontadas) |
| 5 | **TUR** | Segundo tercio | Armería | Arena E4. La TUR en su nido + **un T07 en la ruta de aproximación** + 2 CAR | Avanzar por cobertura; cañón cargado a distancia o granada al nido |
| 6 | **Evento de alarma** | Segundo tercio | Comunicaciones | Arena E8 con 4–5 rondas: todos los tipos conocidos en escalada (A6.3) | Gestionar varias armas robadas seguidas; persianas |
| 7 | **BIG** | Segundo tercio | Almacenes | Arena E3. **El BIG en un slot a ≥ 30 m de la entrada** (1,68 m/s: unos 15 s hasta ponerse a 5 m) y **2 T07 en el lado del jugador** | El tanque pide el cañón robado: unos 30 s sin él y 10–15 s con 2 |
| 8 | **T07M** | Segundo tercio | Almacenes / Túneles | Mezclado con T07, con la misma arma. Con D-E1(c): **un T07M solo** a distancia media, con su unión visible | Silueta nueva; con (c), desarmarlo a distancia disparando a la unión |
| 9 | **Persecución** | Segundo tercio, al final | Túneles de mantenimiento | ChaseCorridorEncounter con la trituradora (A6.2) | Moverse sin parar; las emboscadas son obstáculos |
| 10 | **Horda grande** | Tercer tercio, inicio | Tratamiento de agua | Arena E6: 1 SAW + 8–10 CAR + 1 T07 | Motosierra y embudos |
| 11 | Combinaciones (A2.2) | Tercer tercio | Taller/Fundición, Ensamblaje, Reactor | — | Encadenar armas robadas |
| 12 | Examen final y jefe | Tercer tercio, final | Centro de mando → Bóveda | Arena E8 (segundo cierre, opcional) → antecámara → E9 | Todo |

### A2.2 Combinaciones que exigen la mecánica

| Nombre | Composición (unidades) | Qué exige y por qué (cifras de A1/A3) | Posición / arquetipo |
|---|---|---|---|
| **Yunque** | BIG + 2 T07 + 2 CAR = **5 u** | Usar el cañón robado contra el tanque: 30 s solo con la principal, 10–15 s con 2 cañones | Segundo tercio / E3 |
| **Torre** | TUR (nido) + T07 en la aproximación + 2 HOV = **4 u** | Cañón cargado a distancia y granada al nido: 320 PV = un cañón (195) + granada (≤ 100) + 4 impactos | Segundo tercio / E4 |
| **Marea** | SAW + 10 CAR + T07 = **7 u, 12 cuerpos** | Motosierra contra la horda: el SAW llega primero (9,5 m/s) y una motosierra despacha 6–11 CAR | Segundo y tercer tercio / E6 |
| **Martillo y yunque** | BIG + SAW + 6 CAR + T07 = **7 u, 9 cuerpos** | **Cambiar de arma robada** obligado por la regla de una sola: motosierra para la horda, lanzarla y ejecutar el T07 para el tanque | Tercer tercio / E3 |
| **Doble tanque** | 2 BIG + 3 T07 + SAW = **8 u** | ≥ 3 fuentes (1920 PV de BIG); granadas OVERDRIVE cuando los dos BIG se acercan (radio 4,5 m) | Tercer tercio / E3 grande |
| **Fuego cruzado** | TUR + BIG + 2 T07 + SAW = **6 u** | Priorizar la TUR con el primer cañón y el BIG con el segundo; flanqueo vertical | Tercer tercio / E4 + E3 (Reactor) |
| **Blindados** | 2 T07M + 2 T07 + 2 HOV = **6 u** | Con D-E1(c): desarmar a distancia disparando a las uniones; con (a), ejecuciones bajo fuego cruzado | Segundo y tercer tercio / E2 |
| **Relevo** | 3 SAW + 2 T07 = **5 u** | Ciclar armas deprisa: 5 fuentes, y cada ejecución da ≈ 2,7 s de protección | Tercer tercio / E5 |

### A2.3 Curva de dificultad por tramo

| Tramo | Unidades por ronda | Cuerpos máx. | Pesados por ronda | Fuentes mínimas por ronda |
|---|---|---|---|---|
| Primer tercio | 2–5 | 8 | 0 | ≥ 1 desde el tutorial |
| Segundo tercio | 4–7 | 10 | ≤ 1 BIG o 1 TUR | ≥ 1; ≥ 2 con BIG |
| Tercer tercio | 6–8 | 12 | ≤ 2 BIG; o 1 TUR + 1 BIG | ≥ 2; ≥ 3 con 2 BIG |

---

## A3. Economía de armas

### A3.1 Cañón robado del T07 (`Weapon_T07_Temporary_Optimized`, módulo `P/FastCombat/T07_Module.prefab`)

| Dato | Valor | Fuente |
|---|---|---|
| Energía | 100 | `T07_Module.prefab:5131` |
| Coste por disparo | 12–30, lineal con la carga | `P/Weapons/Weapon_T07_Temporary.prefab:5315-5316` (la variante `_Optimized` no lo cambia) |
| Daño | 25–65 | `S/Weapons/ChargedProjectileDamage.cs:10-11`; DV02:77 |
| Carga | Máximo 1,2 s; mínimo 20 % (0,24 s). Intervalo mínimo entre disparos: 0,5 s | `Weapon_T07_Temporary.prefab:5313-5314`; DV02:75, 78 |
| OVERDRIVE | Con energía ≤ 20 % entra una vez en estado crítico: **cancela y bloquea el disparo** y drena hasta 0 en 5 s | `T07_Module.prefab:5241-5242`; `S/Weapons/CannonOverdrive.cs:37-40`; DV02:41, 47 |
| Explosión | 100 de daño, radio 4,5 m, caída al 25 % en el borde, fusible de respaldo de 6 s. Lanzado en crítico, detona al tocar algo sólido. Si no se lanza, explota en la mano y daña al jugador | `T07_Module.prefab:5258,5302-5303`; DV02:53, 55, 80-81 |

**Cálculo** [LECTURA]:

- 3 cargas completas: 100 → 70 → 40 → 10. Al bajar de 20 entra en crítico: **195 de daño directo**.
- Con cargas mínimas: 7 disparos (100 → 16) = 175. Gestionando la energía al límite: ≈ 215.
- Daño por unidad de energía: 2,08 (carga mínima) a 2,17 (completa). **La carga completa es la jugada natural**; no hace falta enseñar a dosificar.
- Con la granada (≤ 100): **≈ 295 por cañón**.
- Vida útil: ≈ 1,2–1,7 s por disparo → 3 disparos en 4–5 s, más el tiempo de apuntar; en total 6–10 s [SUPUESTO], más los 5 s de crítico.

### A3.2 Motosierra robada del SAW (`P/Weapons/Weapon_SawBot_Animated_v02.prefab`)

| Dato | Valor | Fuente |
|---|---|---|
| Ataque | 45 de daño por segundo, alcance 1,65 m, radio 0,2 m, golpes cada 0,1 s, 12 de energía por segundo | `P/Weapons/Weapon_SawBot_Stolen.prefab:325-329` (la variante v02 solo cambia `motorLoop`, l.5251) |
| Módulo | Capacidad 100; crítico al 20 % con 5 s; explosión de 100, radio 4,5 m, fusible de 6 s | `P/Weapons/SawBot_StolenModule.prefab:236,347-348,364,408-409` |
| Consumo | Gasta energía **en cada golpe con M1 pulsado, toque o no a un enemigo**. En crítico se para | `S/Weapons/StolenSawAttack.cs:24,33-35` |

**Cálculo:**

- Energía útil: 80 → 6,7 s de motor → **≤ 300 de daño**. Con una eficiencia real del 50–70 % [SUPUESTO]: 150–210.
- Con la granada: **≈ 250–400 por motosierra**.
- Contra la horda: 0,6 s por CAR → **6–11 CAR por motosierra**.

### A3.3 Bucle del jugador y ritmo de OVERDRIVE

Ciclo: debilitar → ejecutar (≈ 2,7 s protegido) → gastar (6–10 s) → crítico (5 s, el arma no dispara) → **lanzarla a un grupo o a un pesado** → buscar la siguiente fuente.

- Duración del ciclo: ≈ 12–20 s [SUPUESTO].
- **Ritmo objetivo** [SUPUESTO; validar en F2]: 1 expulsión cada 15–25 s en rondas normales y cada 10–15 s en rondas con BIG.
- **Por cuarto de 3 rondas: 2–4 ciclos.**
- Una ronda de más de 45 s sin ninguna fuente es un defecto de diseño.
- La ejecución también **defiende**: en rondas de mucha presión, las fuentes funcionan como respiros tácticos (≈ 2,7 s de protección cada una).

### A3.4 Reglas mínimas de fuentes por ronda

| Regla | Contenido |
|---|---|
| R-E1 | Toda ronda de ≥ 4 unidades lleva ≥ 1 ejecutable (T07, T07M o SAW). En el primer tercio, desde el tutorial, toda ronda lleva ≥ 1 |
| R-E2 | **Por cada BIG, ≥ 2 ejecutables en la misma ronda**, y al menos uno es T07 o T07M |
| R-E3 | **Con TUR, ≥ 1 T07 o T07M en la misma ronda**, con su slot en la ruta de aproximación al nido |
| R-E4 | **En una horda (≥ 6 CAR), ≥ 1 SAW en la misma ronda** |
| R-E5 | **Cada ronda se basta sola**: nunca depender de un arma traída de la ronda o del cuarto anterior |
| R-E6 | ≤ 4 T07/T07M por ronda (coste de aparición, BP:95; presión de francotirador) |
| R-E7 | Las variaciones de una ronda conservan el **mismo número de ejecutables y de pesados** y la misma suma de unidades (± 0,5) |
| R-E8 | La primera ronda de **cada cuarto** lleva ≥ 1 ejecutable cerca del jugador (a ≤ 20 m de los slots de la ronda 1), para que el bucle empiece pronto |

### A3.5 Armas robadas que hacen falta contra los pesados

| Pesado | Soluciones (daño aproximado) | Fuentes en la ronda | TTK objetivo [SUPUESTO] |
|---|---|---|---|
| **BIG (960)** | 2 cañones (≈ 590) + principal (≈ 370, unos 57 impactos) · 2 cañones + 1 motosierra (≈ 990) · 3 cañones con granadas (≈ 885) + 12 impactos | ≥ 2; ≥ 3 si hay 2 BIG | ≤ 15 s por BIG |
| **TUR (320)** | 1 cañón (195) + granada (≤ 100) + ≥ 4 impactos · 1 motosierra si se llega al nido (300 + granada) · 50 impactos de la principal a ≤ 43 m (≈ 8–9 s expuesto) | ≥ 1 T07 | ≤ 8 s |
| **2 BIG** | Una granada que alcance a los dos (radio 4,5 m) vale el doble. La geometría debe juntarlos: el anillo del arquetipo E3 los hace converger | ≥ 3 | ≤ 25 s los dos |

### A3.6 Botiquines (25 PV)

| Regla | Contenido |
|---|---|
| R-V1 | `HealAmount = 25` **explícito** en cada instancia: el prefab trae 40 (`Pickup_Health.prefab:86`) |
| R-V2 | Arena de combate: **2 botiquines** (50 PV), +1 por cada BIG o TUR de la sala, con un **máximo de 4**. **[cambio B]** +1 en las arenas de horda (R11: daño por contacto de muchos CAR) |
| R-V3 | Colocación: en rutas de flanqueo y bucles, para premiar el movimiento (estilo DOOM). Siempre visibles desde la ruta principal. Nunca en callejones ni sobre slots de spawn (una cápsula sobre el slot no bloquea, pero el jugador allí sí: A5.3). Al menos uno a ≤ 10 m de una cobertura alta frente a cada pesado |
| R-V4 | Respiro: 2–3. Persecución: 2, sobre la ruta y sin desvío, más 1 en la bahía previa al trigger **[cambio B]**. Arena de alarma: 3, más los de recompensa tras terminar (R07: 2 en las alas). **Antecámara del jefe: 4 (100 PV)**; arena del jefe: 4 en la galería |
| R-V5 | Las rondas anulan la vida que suelta el HOV (`CombatTwoRoomsLab.cs:132`): **los botiquines colocados son la única curación** |
| R-V6 | Lo que no se recoge se pierde al salir del cuarto. Sin checkpoints (D-E2a), sumar +1 botiquín por cuarto en el tercer tercio |

---

## A4. Arquetipos de encuentro y requisitos de arena (para R1)

### A4.1 Reglas comunes a todas las arenas de combate

Todas salen del código de aparición (`CombatTwoRoomsLab.cs:99-128`, INV §C4).

| # | Regla | Por qué |
|---|---|---|
| S1 | **Bounds de la arena** (`firstRoom`): **la ruta a la salida los atraviesa**, contienen todos los slots y **no incluyen la cámara de esclusa**. Son AABB en coordenadas de mundo: si el cuarto gira, R1 comprueba que la caja envolvente no tape la esclusa. **[cambio B]** Pueden dejar fuera la plataforma o galería de llegada si se cumplen cuatro condiciones (B2.2): conectada al NavMesh de la arena (S2), sin slots, la salida solo se alcanza cruzando los bounds y el pórtico P cierra solo con el jugador ≥ 4 m dentro | La ronda 1 empieza al entrar en los bounds (`:63`). Si el jugador puede llegar a la salida sin entrar, nunca termina: **soft-lock** |
| S2 | **Toda superficie pisable por el jugador** (plataformas, tarimas, cajas grandes) está sobre NavMesh conectado o a ≤ 3 m de él | Cada spawn exige un camino completo hasta el NavMesh a ≤ 3 m del jugador (`:123`). Si el jugador sube a un sitio desconectado, la ronda se queda en **«OLEADA EN ESPERA»** |
| S3 | **Todo slot sobre NavMesh conectado** con la planta, también el nido de la TUR (rampa, escalera o NavMeshLink) | El mismo control de camino completo afecta a enemigos fijos |
| S4 | **Slots ≥ 1,5 × cuerpos** de la ronda más grande, en **≥ 3 grupos** (≥ 4 desde 8 cuerpos), con ≥ 2 slots por grupo y los grupos a **≥ 15 m** entre sí | La distancia libre (6 m + radio) solo puede anular un grupo a la vez |
| S5 | Slots de la ronda 1 a **≥ 10 m de la puerta de entrada** | El jugador está en el borde de los bounds cuando empieza |
| S6 | **Volumen libre alrededor del slot**: normal, Ø 1,6 m y 2,2 m de alto (radio del SAW 0,8); **BIG, Ø 5 m y 4,6 m de alto**. Separación entre slots: 2 m normales y 5,5 m entre slots de BIG. Ningún collider por encima del suelo (+ 0,08 m) dentro del volumen: **[R2] Decoración no invade** | Prueba de cápsula y reservas (`:117-128`); radio del BIG 2,25 |
| S7 | **Bucle obligatorio**: ninguna arena de combate tiene callejones sin salida (MD §5.1) | Movimiento rápido; kiting del SAW |
| S8 | Altura libre ≥ 8 m; con plataformas, cota de la plataforma + 4,5 m (MD §5.1) | — |
| S9 | **Atacantes comprometidos:** desde ningún punto de la planta principal debe haber línea de visión con más de 3 grupos de slots a la vez. Coberturas altas (≥ 2,4 m de alto y ≥ 3 m de ancho) cada ≤ 12 m en las rutas | Tope de francotiradores; ancho mayor que la salpicadura del BIG (2,88 m) |

### A4.2 Tabla de arquetipos

| Código | Arquetipo | Planta mín. (m) | Techo | Líneas de visión | Flanqueo y verticalidad | Coberturas | BIG | Slots | Rondas × unidades |
|---|---|---|---|---|---|---|---|---|---|
| **E0** | Respiro (sin combate) | Libre | ≥ 4,5 | — | — | — | — | 0 | 0; componente de liberación (A6.1) |
| **E1** | Introducción pequeña | 24 × 28 | ≥ 8 | Máx. 20–30 m | 1 bucle alrededor de un bloque central (≥ 6 × 6, cobertura alta); circulación ≥ 6 m; tarima opcional a +1,5 m | 4–6 bajas, 1–2 altas | No | 6 en 3 grupos | 1–2 × 2–4 u (≤ 6 cuerpos) |
| **E2** | Media mixta | 36 × 40 | ≥ 10 (entreplanta + 4,5) | Un eje de ≥ 38 m (alcance del T07) cortado por 2–3 coberturas altas | 2 bucles en «8». Entreplanta a +3,5–4,5 m sobre ≤ 30 % de la planta, con 2 accesos en extremos opuestos (rampa ≤ 30° y escalera). **[cambio B]** También +5…+6 si es pasarela con paso inferior (CC-3 de R1) o si el cuarto desciende (R02). Las galerías sobre pilares no cuentan en el 30 % porque no quitan suelo (B2.2) | 6–8 bajas, 3–4 altas | Opcional, con las reglas de E3 | ≥ 11 en 4 grupos (≥ 1 en la entreplanta) | 3 × 5–7 u |
| **E3** | Grande con BIG | **44 × 48** | ≥ 10 (≥ 12 con plataformas) | Máx. 30–45 m: más que los 28,6 m del BIG, para poder salir de su alcance | **Anillo de ≥ 6 m libres** (mínimo de contrato 5) alrededor de una estructura central de ≥ 10 × 10 m que corta su línea de tiro, con perímetro ≥ 90 m. Balcón o anillo a +4 m en 2 lados, con rampas. **Todo paso hacia la zona del BIG ≥ 5 m** | Pilares altos de Ø o lado ≥ 3 m cada ≤ 12 m del anillo; bajas para los duelos con T07 | **2 slots grandes por cada BIG de la ronda**, en mitades opuestas y a ≥ 20 m de la entrada | ≥ 12 normales + grandes; 4 grupos | 3 (o 4) × 6–8 u; ≤ 2 BIG por ronda |
| **E4** | Vertical con TUR | 36 × 44 | ≥ nido + 4,5 (≥ 12; 14 recomendado) | Desde el nido la TUR ve **≤ 60 % de la planta**; alcance 43,75 m | **Nido de ≥ 6 × 6 m a +5…+8 m**, conectado al NavMesh. **≥ 2 rutas de aproximación** con cortes de visión cada ≤ 8 m (≈ 0,4 s expuesto esprintando). **Una ruta de flanqueo a la cota del nido**, por detrás o de lado, de ≥ 4 m de ancho. Zona bajo el nido utilizable | Altas en las rutas; bajas en la planta | Opcional (Fuego cruzado) | ≥ 9 normales en 3 grupos + **1 slot fijo por TUR** | 3 × 5–7 u; 1 TUR por ronda (2 solo en E8/E9, con nidos distintos) |
| **E5** | Pasillo o emboscada | 6–8 de ancho × 40–70 de largo | ≥ 6 (≥ 8 con pasarela) | Tramos ≤ 38 m cortados por recodos o columnas | **Ruta paralela obligatoria** (galería, pasarela o sala lateral) que cierre el bucle. Nichos de 4 × 4 m cada 10–15 m a los dos lados | Columnas y nichos | Solo con ≥ 10 m de ancho | ≥ 8 en ≥ 3 grupos de nichos: delante, centro y **detrás** (emboscada a ≥ 6 m) | 1–2 × 3–5 u |
| **E6** | Horda | 40 × 40 | ≥ 8 | Abiertas | Anillo de circulación de ≥ 8 m alrededor de 1–2 islas; **2–3 embudos de 5–6 m** entre subzonas para encauzar la horda hacia la motosierra; plataformas bajas con rampa; sin fosos | Pocas, bajas (contra los T07) | No | ≥ 12 en ≥ 4 grupos, ≥ 2 en el lado lejano | 2–3; ronda de horda de 6–8 u (10–12 cuerpos) alternada con rondas mixtas |
| **E7** | Persecución | 6–16 de ancho × 150–220 de largo | ≥ 8 | Larga, hacia delante | **Recta en +Z del mundo**; banda vertical de −7 a +6 m sobre `courseOffset.y`; grietas de 4 m (GapFallRecovery) | Laterales | No | 2 por emboscada, ≥ 6 m por delante | 2–3 emboscadas × 1–2 enemigos (A6.2) |
| **E8** | Evento de alarma | Como E2 o E3 | Como E2 o E3 | Como E2 o E3 | Como E2 o E3, **más 2–4 persianas (clase P)** que sellan alas laterales; **con las persianas bajadas sigue habiendo bucle** | Como E2 o E3 | Según la base | Como la base, **solo en el volumen que no se sella** | 4–5 × 4 → 8 u, en escalada (rondas ≥ 4: A5.4) |
| **E9** | Jefe | Ver A7.4 | | | | | | | |

### A4.3 Notas por arquetipo para R1

- **E1:** sirve para presentar un tipo. El bloque central cumple dos funciones: bucle para escapar del SAW y corte de visión frente al T07.
- **E2:** la entreplanta da ventaja de altura, pero sus dos accesos opuestos impiden que el jugador acampe. El nivel superior necesita NavMesh conectado (S2).
- **E3:** el BIG gira a 60°/s (CHK:273); rodearlo por el anillo es la táctica. Los pilares cortan su línea de tiro y la salpicadura, siempre que tengan ≥ 3 m de ancho.
  - Rampas al balcón de ≤ 5 m de ancho: el BIG no sube. Es el refugio del jugador; da igual si el BIG llega o no.
  - La primera explosión del BIG en la sesión provoca un pico: **[R0]** precalentar `FX_BigBot_Blast_v4` en el cuarto del primer BIG (pendiente en CHK:1094).
- **E4:** con 320 PV, la TUR obliga a acercarse o a gastar el cañón. El flanqueo a la cota del nido convierte la amenaza a distancia en un duelo cercano.
  - **La TUR va siempre con un `FixedPoint` propio y `Count = 1`.** Dos torretas en el mismo punto fijo nunca caben: la segunda espera para siempre (A5.3).
- **E5:** los slots de detrás generan tensión, pero la regla de 6 m impide que aparezcan pegados. El T07 en un pasillo largo es letal: tramos ≤ 38 m.
- **E6:** los embudos son el sitio de la motosierra. La reserva de slots permite reutilizarlos (tercera pasada, `:106-108`), pero cada slot debe quedar libre antes: 12 cuerpos con 8 slots tardan más en entrar.
- **E7 y E8:** ver A6.

### A4.4 Requisitos más restrictivos (resumen para R1)

1. **BIG:** anillo libre ≥ 6 m, todo paso hacia su zona ≥ 5 m, slots de Ø 5 m × 4,6 m de alto a ≥ 8,25 m del jugador (6 + 2,25), planta ≥ 44 × 48 m.
2. **TUR:** nido de ≥ 6 × 6 m a +5…+8 m, **conectado al NavMesh**, que ve ≤ 60 % de la planta. Ruta de flanqueo a su cota.
3. **Persecución:** recta en **+Z del mundo**, ≤ 16 m de ancho, 150–220 m de largo, sin giros.
4. **NavMesh:** toda superficie pisable a ≤ 3 m de NavMesh conectado. Ningún slot ni nido en una isla.
5. **Bounds:** la caja envolvente de la arena no incluye la cámara de esclusa y cubre toda la ruta desde la entrada.
6. **Arena del jefe** (A7.4): Ø ≥ 56 m, techo ≥ 16 m, galería a +6 m, anillo del jefe ≥ 8 m.

---

## A5. Reglas de rondas y límites

### A5.1 Presupuesto por ronda (MD §3.8; INV §5)

| Límite | Valor | Notas |
|---|---|---|
| Unidades por ronda | **≤ 8** (CAR 0,5; BIG 2; resto 1) | El tamaño de la ronda es el número de enemigos simultáneos (`:66`) |
| Cuerpos por ronda | **≤ 12** | — |
| BIG por ronda | ≤ 2 | 4 unidades; coste de aparición (BP:95) |
| T07 + T07M por ronda | ≤ 4; ≤ 3 en la primera ronda con T07 de la sesión | Coste de aparición (BP:95) y de la primera activación (BP:131) |
| TUR por ronda | ≤ 1 (2 solo en E8 o E9) | Un slot fijo por TUR |
| Atacantes a distancia con línea de visión a la vez | ≤ 3 T07 y ≤ 1 BIG en ráfaga hacia el jugador | Presión teórica: 3 T07 (≤ 54 PV/s) o 1 BIG (≤ 72 por ráfaga) frente a 100 PV [LECTURA] |
| Cuerpos totales por cuarto (precalentados al cargar) | **≤ 36** [SUPUESTO] | Todo se instancia inactivo al cargar, a 1 por frame (`EnemySpawnQueue.cs:16`; INV §1.6). Room04 tiene hoy 39 (INV §B3) |
| Activación | 1 por frame (`Configure(1,1)`), y 0,2–0,4 s entre apariciones (`CombatTwoRoomsLab.cs:34-35`) | Una ronda de 8 cuerpos entra en 1,6–3,2 s; una de 12, en 2,4–4,8 s |

### A5.2 Rondas por función de cuarto

| Función | Rondas | Variaciones aleatorias | Notas |
|---|---|---|---|
| Introducción (E1) | 1–2 | **No** | Composición fija para enseñar |
| Combate normal (E2, E4, E6) | 3 | Sí, 2–3 por ronda, desde el segundo tercio | — |
| Pieza grande de zona (E3) | 3–4 | Sí; la ronda 1 fija | Con 4: datos de `secondRoom` (A5.4) |
| Pasillo (E5) | 1–2 | Opcional | — |
| Alarma (E8) | 4–5 | Solo en las rondas 2–4 | `secondRoom` obligatorio |
| Persecución (E7) | 0 (emboscadas) | No | ChaseCorridorEncounter |
| Respiro (E0) | 0 | — | Componente de liberación |
| Jefe (E9) | Encuentro propio | No | A7 |

**Variaciones:**

- Siempre `variedSpawns = true`. Una ronda sin variaciones deja `Variations` vacío y usa `Groups`.
- Todas las variaciones de una ronda cumplen R-E7.
- **Semilla:** en F2, `scenarioSeed` distinto de 0 por cuarto, para poder reproducir pruebas (tune-enemy-ai, design-action-combat). Para la entrega, 0 (aleatoria en cada partida).
- `Groups` siempre relleno con la composición base. Si una variación falla, queda un respaldo válido.

### A5.3 Cómo evitar «OLEADA EN ESPERA» y otros soft-locks

| Causa (código) | Regla de prevención |
|---|---|
| Todos los slots a < 6 m + radio del jugador (`:121`) | S4 (grupos separados ≥ 15 m), S5 |
| Jugador en una superficie sin NavMesh conectado a ≤ 3 m (`:123`) | S2 |
| Slot o nido en una isla de NavMesh (`:123`) | S3 |
| Slot ocupado por la decoración o por un módulo de arma caído (prueba de cápsula, `:124-126`) | S6. Los restos de muerte no tienen collider (`S/VisualPass/MechDebrisPiece.cs:5`); los módulos de arma sí. Con S4 sobran slots |
| `FixedPoint` compartido por enemigos fijos, o TUR con `Count > 1` en un punto fijo (`:105`) | Un grupo por TUR, con `Count = 1` y su propio `FixedPoint` |
| BIG sin slot grande libre | ≥ 2 slots grandes por BIG; sin `FixedPoint` (encuentra solo el grande gracias a la prueba de cápsula) |
| Camino no variado: lanza una excepción si no hay slot (`:80`) | `variedSpawns = true` siempre |
| **[cambio B]** Segunda TUR en el mismo slot fijo de un cuarto: los restos de la primera podrían ocupar la cápsula (la base «queda fija en el suelo», CHK:822; sin verificar si conserva collider) | No se reutiliza el slot fijo de una TUR dentro del mismo cuarto (R06 y R16 tienen una sola TUR) |
| `variedSpawns` sin cola: excepción (`:94`) | `spawnQueue` asignado siempre |
| Rondas ≥ 4 sin `secondRoomSpawns` (`:103`; INV §C7.1) | A5.4 |
| Bounds que no cubren la ruta (`:63`) | S1 |
| Cuarto sin rondas ni persecución: no se libera (`CampaignRoom.cs:23`) | Componente de liberación (A6.1) |

### A5.4 Rondas ≥ 4 (soft-lock por lectura de código)

Las rondas con índice ≥ 3 usan `secondRoomSpawns` y los bounds `secondRoom` (`:103, 121`). En una arena única:

- `secondRoom` = **los mismos bounds** que `firstRoom`.
- `secondRoomSpawns` = **las mismas referencias de Transform** que `firstRoomSpawns` (o un subconjunto válido).
- El validador de F2 rechaza cualquier cuarto con más de 3 rondas y `secondRoomSpawns` vacío (A8.4, V5).

---

## A6. Respiros, persecución y alarma

### A6.1 Ritmo y respiros

**Patrón por zona:** entrada (E1 o E5) → combate (E2, E4 o E6) → pico (E3 o E8) → respiro (E0) o esclusa de zona.

| Regla | Contenido |
|---|---|
| Frecuencia | ≥ 1 respiro (E0) cada 3–4 cuartos de combate; ≥ 1 por tercio, más la antecámara del jefe. Nunca dos picos (E3 o E8) seguidos sin respiro o compuerta de zona entre ellos |
| Esclusas | Cada transición dura ≥ 3 s (INV §C7.7): ya es un microrespiro. Los respiros largos son cuartos E0 |
| Persecución | Precedida por un respiro con 2–3 botiquines y seguida de una esclusa |

**Contenido de un respiro E0:**

- 2–3 botiquines.
- Landmark y narrativa (R2).
- Anticipo visual del enemigo siguiente: chatarra de TUR en la Enfermería antes de la Armería, por ejemplo.
- **Ningún enemigo:** `CampaignRoom` no admite enemigos sin rondas.

**[R0] Componente nuevo `Map01StageRelease`** (`S/Levels/Map01/`, observa sin cambiar nada):

- Campos:
  - `room` (CampaignRoom).
  - `mode` (`OnStart` u `OnEnterVolume`).
  - `volume` (Bounds locales).
  - `delaySeconds`.
- Llama a `room.Release()` una sola vez.
- La sala queda con `rounds = null`, `pursuit = null` y `prologueTitle` = «ENFERMERÍA · ZONA SEGURA».
- Efecto secundario [LECTURA]: `IsPrologue = true` hace que `CampaignFlow` no llame a `ReleaseStartCover` (`CampaignFlow.cs:274`). Es inocuo después de la primera etapa, porque la cubierta ya se liberó. **Comprobar en F2.**

### A6.2 Persecución (`S/Levels/ChaseCorridorEncounter.cs`)

**Comportamiento real** [LECTURA]:

- La sierra avanza en `Vector3.forward` del **mundo** (`:88`).
- La persecución termina cuando la `z` de mundo del jugador supera `finishZ` (`:84`).
- El daño solo se aplica si |x − `courseOffset.x`| < `halfWidth` y la altura está entre −7 y +6 m de `courseOffset.y` (`:91`).
- 100 de daño cada 0,35 s: **la sierra mata** (vida 100).
- Las emboscadas exigen que sus puntos estén ≥ 6 m por delante (`:115`). La persecución no espera a matar a nadie (CHK:593).
- Las puertas internas `entranceDoor` y `exitDoor` se activan y desactivan con `SetActive`; no son esclusas.

**Requisitos [R1]:**

- Cuarto **recto en +Z del mundo**, de ≤ 16 m de ancho (`halfWidth` 8) y 150–220 m de recorrido.
- Desnivel dentro de la banda −7…+6 m.
- 2–3 grietas de 4 m con GapFallRecovery (20 de daño y vuelta a la orilla de salida: CHK:595).
- El trigger de inicio, ≥ 10 m después de la puerta interna de entrada.
- La salida termina en la puerta del cuarto alineada con la esclusa siguiente, **no en un muro ciego** (INV §6, M13).

**Holgura de tiempo** [cálculo]. Sea L el recorrido, v la velocidad del jugador y d₀ la ventaja inicial de la sierra (10 m [SUPUESTO]):

`H = L/8 + 2 + d₀/8 − L/v`

Con L = 185 m: **H ≈ 12 s andando (13 m/s)** y ≈ 17 s esprintando.

- Regla: la suma de retrasos previstos (emboscadas + grietas fallidas, ≈ 1–2 s cada una [SUPUESTO]) ≤ **50 % de H andando ≈ 6 s**.
- Por tanto: ≤ 3 emboscadas, cada una de ≤ 2 enemigos de tipo rápido de abatir o esquivar (T07 a distancia, HOV, CAR).
- **Sin BIG**: ocupa 4,5 m de los 16. **Sin TUR**: su slot fijo bloquearía la ruta.

**Economía:** no se espera ejecutar durante la persecución (0,733 s parado). El arma robada que el jugador traiga es un extra.

**Datos** (de Pursuit05 y del laboratorio): `sawSpeed` 8, `warningSeconds` 2, `bladeRpm` 240, `sawDamage` 100, `damageInterval` 0,35, `sawContactOffset` 2,1 con la trituradora `GiantShredder` (CHK:601), `halfWidth` 8. `finishZ` y `courseOffset` en coordenadas de mundo (A8).

### A6.3 Evento de alarma

**Base reutilizable: `CorridorAlarm`** (`S/Levels/CorridorAlarm.cs`):

- Se dispara con `rounds.WaveIndex >= 0` (`:46`) y se apaga con `Completed` (`:56`).
- Es solo presentación: luces, balizas, sirena, pantallas y textos (`:11-21`).

**Propuesta: «CIERRE DE COMUNICACIONES»** (arquetipo E8, segundo tercio).

**Fases:**

- **Calma:** el jugador explora un ala.
- **Disparo:** al entrar en los bounds empieza la ronda 1, salta `CorridorAlarm` y bajan las persianas de las alas laterales.
- **Escalada:** 4–5 rondas de 4 → 8 unidades.
- **Ronda final:** apagón parcial (estado de R5) durante la última ronda [SUPUESTO].
- **Desenlace:** con `Completed`, suben las persianas y aparece «ZONA DESPEJADA · ESCLUSA ABIERTA».

**[R0] Componente nuevo `Map01ShutterController`** (observa `WaveIndex`, `LivingEnemies`, `PendingSpawns` y `Completed`, que son públicas: `CombatTwoRoomsLab.cs:48-49`):

- Datos por persiana:
  - `door` (pieza clase P de R4).
  - `closeOnRound`.
  - `openOnRound` u `openOnComplete`.
  - `sweepVolume`.
- **No cierra mientras el jugador esté dentro del volumen de barrido**: reintenta cada 0,25 s.
- Colisión desde el primer frame al cerrar; abierta, inactiva (contrato MD §3.5).
- Con un `NavMeshObstacle` con carving cuando está cerrada [SUPUESTO; acordar con R4], ningún slot queda detrás de una persiana: S6 y la regla de E8 (slots solo en el volumen que no se sella).

**Segundo cierre (opcional):** en el Centro de mando, antes del jefe, como examen final.

---

## A7. Jefe: tres conceptos

Solo concepto: la IA completa del jefe es otra tarea (Prompt 9, F4). Las cifras del jefe son [SUPUESTO] salvo las que citan una fuente.

**Común a los tres** [R0]:

- Componente nuevo **`Map01BossEncounter`**. Fases por umbrales de `Health`; refuerzos con `EnemySpawnQueue.Spawn(prefab, pos, rot, callback, resolve)` (`S/Levels/EnemySpawnQueue.cs:41-42`); barra de jefe en el HUD.
- Al morir el jefe, llama a `CampaignRoom.Release()`. La sala del jefe va con `rounds = null` y `prologueTitle` propio.
- El fin de mapa (WinScene o mensaje) lo resuelve R0: hoy no existe (INV §C2.8).
- Presupuesto: jefe = 3 unidades [SUPUESTO] + refuerzos ≤ 5 unidades → ≤ 8.

### A7.1 Concepto A — «EL CUSTODIO» (Goliat-0), Big Bot de bóveda

**Silueta:**

- Big Bot vigente a escala ×1,6 [SUPUESTO]: ≈ 4,9 m de alto (3,05 × 1,6) y unos 5 m de hombros.
- Placas de compuerta soldadas al frente, pintadas con la plantilla «B-0».
- Mochila-reactor con tubos incandescentes.
- Dos cañones de hombro sobredimensionados con **anillas naranjas en la unión**, el mismo color de unión que el T-07 (`ENEMY_V01.md:24`).
- En la fase 1, cables gruesos del techo enchufados a la mochila: está anclado a la Bóveda.
- Lectura en una sola imagen: **el robot que guarda las mejores armas del búnker**.

**Vida** [SUPUESTO]: 3600 (≈ 3,75 BIG) en tres tramos de 1200. Cada unión de cañón: 300.

**Armas robables (ligadas a las fases):**

- Al romper la unión o pasar el umbral, cae el cañón de ráfaga del Big Bot: `P/Weapons/BigBot_CollectibleCannon.prefab`, que da `Weapon_BigBot_Temporary`. Se recoge con E, **sin ejecución**, coherente con v31.
- Para el jugador: 3 proyectiles por ráfaga de 30 de daño explosivo con radio 2,6 m, energía 120 y 12 por proyectil; OVERDRIVE a 24 (MAPK:15; CHK:402, 433, 453).
- El desprendimiento real y la recogida ya se probaron al 25 % de vida (MAPK:33).
- Por cañón: 96 de energía útil → 8 proyectiles ≈ 240 + granada ≤ 100 ≈ **340**.
- Los refuerzos T07 dan cañones de T-07 (≈ 295 cada uno).

**Fases:**

| Fase | PV | Conducta | Refuerzos | Transición |
|---|---|---|---|---|
| 1 «Enchufado» | 100–67 % | Anclado a ≤ 10 m del centro; ráfagas dobles (patrón vigente) y mortero | 2 T07 (fuentes) | Al 67 % o al romper la unión izquierda: arranca los cables (2 s, invulnerable, onda que empuja); **cae el cañón izquierdo** |
| 2 «Suelto» | 67–34 % | Camina a 1,68 m/s × 1,2; con un cañón, ráfaga de emergencia (ya existe); embestida | 1 SAW + 4 CAR | Al 34 % o al romper la unión derecha: **cae el cañón derecho** |
| 3 «Núcleo» | 34–0 % | Sin cañones: barrido de brazo y pisotón. La mochila se abre y deja el núcleo expuesto en la espalda | 2 T07 + 1 HOV | Muerte: `HeavyCollapse` (1,8 s, CHK:287) y explosión |

**Ataques telegrafiados:**

| Ataque | Señal (tell) | Ventana activa | Respuesta del jugador |
|---|---|---|---|
| Ráfaga doble (vigente) | 0,7 s de brillo y sonido de preparación (CHK:400, 442) | 6 proyectiles en 0,9 s, a 22 m/s | Cobertura alta, salir del eje o slide (0,42 s invulnerable) |
| Mortero (nuevo, fases 1–2) | 1,5 s: círculos rojos en el suelo y silbido | 3 impactos de radio 3 m, 20 cada uno [SUPUESTO] | Salir del círculo; la rejilla de la galería protege |
| Embestida (nuevo, fase 2) | 1,0 s: agacha el torso, raspa el suelo, visor rojo | Carrera recta de 2 s a 10 m/s [SUPUESTO] | Dash lateral (4,32 m). **Si choca con un pilar, queda aturdido 3 s:** ventana para la unión o el núcleo |
| Pisotón (nuevo, fase 3) | 0,8 s: levanta la pierna, chispas | Onda en el suelo de radio 8 m, 25 de daño | Saltar (apex 2,03 m) o estar en la galería |
| Barrido de brazo (fase 3) | 0,6 s | Cono frontal de 120° a 4 m | Alejarse. El castigo: deja el núcleo trasero abierto 2 s |

**Debilidades ligadas a la mecánica:**

- Uniones naranjas: destruirlas adelanta la fase y suelta el arma.
- Núcleo trasero en la fase 3: daño explosivo ×2 [SUPUESTO] con la ráfaga robada y las granadas OVERDRIVE.
- Ejecutar refuerzos: cañón y ≈ 2,7 s de protección.
- El jefe no se puede ejecutar.

**Arena** (requisitos para R1):

- Circular, **Ø ≥ 56 m**: el doble del alcance de 28,6 m, para poder salir de él.
- Techo ≥ 16 m.
- **Galería perimetral a +6 m** de ≥ 6 m de ancho, con 4 rampas de 4–5 m: el jefe no sube; es el refugio.
- 6 pilares de Ø ≥ 3,5 m en anillo a 14 m del centro: cobertura, y le dan contra qué estrellarse en la embestida.
- Centro libre de Ø 20 m (zona de anclaje de la fase 1).
- **Anillo del jefe ≥ 8 m libres** (radio de agente ≈ 3,6 [SUPUESTO]).
- 4 alcobas de refuerzo de 3 slots bajo la galería, con NavMesh conectado.
- **Suelo plano y sin fosos en todo su radio de acción**: los cañones caen con física junto a él (CHK:285).
- 4 botiquines en la galería.

**Base técnica:**

- Reutiliza: cadena de `BigBot_Current` (ráfagas, emergencia, destrucción al 75/50/25 %, desprendimiento del cañón y `HeavyCollapse`); `BigBot_CollectibleCannon` y `Weapon_BigBot_Temporary`; contrato de pérdida anticipada `BigBotCombat.LoseCannon` (CHK:285); receptor de salud de `Damageable` (CHK:432); contrato de política de `EnemyMobile` (CHK:261); `WorldspaceHealthBar`; `EnemySpawnQueue`.
- Nuevo:
  - Variante `Boss_Custodio`: escala, vida, `executionOnly = 0` **solo aquí** e hitboxes de unión.
  - `Map01BossEncounter` y barra de jefe.
  - Política con 3 ataques nuevos, más los clips de embestida y pisotón sobre el rig de 16 huesos.
  - Decal del mortero.

**Riesgos:**

- Escalar el rig: los bounds y LOD ya dieron culling cercano (CHK:297).
- Agente de ≈ 3,6 m sobre un NavMesh de 0,5 (D-E6) → arena abierta sin pasos de < 8 m.
- Silueta ya vista ocho veces en el mapa (B6) [cambio B]: compensar con la silueta y las fases.
- 72 por ráfaga más refuerzos puede ser excesivo: bajar el daño en la variante.

**Coste: medio.**

### A7.2 Concepto B — «LA MATRIARCA», fundidora de chatarra

**Silueta:**

- Máquina-horno empotrada en la pared del fondo, de 14 × 10 m [SUPUESTO].
- Boca de crisol central incandescente.
- Dos brazos-grúa de 3 segmentos con **motosierras de 4 m**.
- Dos tolvas laterales que escupen Carroñeros.
- Lomo con 2 torretas.
- Lectura: **la fábrica que crea a los Carroñeros**.

**Vida** [SUPUESTO]: núcleo 2400, brazos 600 cada uno, torretas 320 (las de la TUR).

**Armas robables:**

- Fase 1: los SAW de cada camada dan motosierras.
- Fase 2: al romper la unión de un brazo (codo naranja) cae una «sierra matriarca». Reutiliza el módulo y el arma de la motosierra existente: **sin código de arma nuevo**. Una variante de 60 de daño por segundo es opcional [SUPUESTO].

**Fases:**

1. **«Producción»:** las torretas del lomo disparan. Cada 20 s [SUPUESTO] las tolvas sueltan una camada de 4–6 CAR y 1 SAW. Destruir las 2 torretas (no dan arma) libera los brazos.
2. **«Brazos»:** barridos alto y bajo. Romper los codos tira las sierras.
3. **«Crisol»:** el núcleo solo se expone cuando la boca se abre para verter (3 s cada 10 s [SUPUESTO]). Fuera de esa ventana recibe ×0,25. **Una granada OVERDRIVE dentro de la boca abierta hace ×3** [SUPUESTO]: le devuelves su chatarra; la expulsión es el golpe final.

**Ataques telegrafiados:**

| Ataque | Señal (tell) | Ventana activa | Respuesta del jugador |
|---|---|---|---|
| Barrido alto | 1,0 s: el brazo sube, chispas y sirena corta | Hoja a 1,2–1,8 m del suelo | **Slide por debajo** (cápsula agachada de 0,9 m; 0,42 s invulnerable) |
| Barrido bajo | 1,0 s: el brazo raspa el suelo | Hoja a 0–0,6 m | **Saltar** (apex 2,03 m) |
| Vertido | 1,5 s: la boca brilla y el suelo se marca en rojo | Charco de 4 s, 15 PV/s [SUPUESTO] | Subir a las pasarelas |
| Camada | 2 s: luz ámbar en la tolva y sirena | Llegada de CAR en masa | Motosierra en los embudos |

**Debilidades:** uniones de los brazos; la boca abierta; las sierras robadas contra el núcleo; las torretas destruidas abren el flanco.

**Refuerzos:** CAR continuos, con un techo de 8 CAR (4 u) + 1 SAW por camada; en la fase 3, 1 T07.

**Arena:**

- Semicírculo de 50 m de frente × 40 m de fondo, ante la máquina.
- 3 niveles: suelo, galería a +5 m y pasarelas a +9 m sobre las tolvas.
- Los brazos alcanzan 14 m desde la pared (zona marcada).
- Una zona plana de 12 × 12 m delante de cada brazo para recoger la sierra.
- Embudos de 5 m entre las tolvas y la planta.
- Techo ≥ 18 m.

**Base técnica:**

- Reutiliza: TUR, CAR, SAW, la motosierra robada, `DamageArea`, `CannonExplosion` y los rodillos de la trituradora `GiantShredder` (CHK:601) para la boca.
- Es estática: el jefe no tiene problema de NavMesh.
- Nuevo:
  - Malla grande y brazos rígidos con scripts de Blender reproducibles.
  - Animación de los brazos e hitboxes.
  - `Map01BossEncounter` con generador de camadas.
  - Peligro de vertido y trigger de la boca que multiplica el daño de las granadas.

**Riesgos:** **arte de pieza única grande**; un jefe estático puede leerse como «torreta grande»; horda constante + jefe en el techo de 12 cuerpos; sincronizar los barridos con la colisión.

**Coste: alto.**

### A7.3 Concepto C — «EL PANÓPTICO», columna de defensa del Centro de mando

**Silueta:**

- Columna central de 6 m de diámetro × 20 m de alto que atraviesa 3 pisos de una bóveda cilíndrica.
- 3 anillos giratorios, cada uno con 2 vainas de cañón T-07 y un ojo-sensor rojo.
- Corona de antenas.
- Lectura: **la arquitectura es el enemigo**.

**Vida** [SUPUESTO]: uniones de las vainas, 150 cada una; núcleo, 2400 en 3 tramos.

**Armas robables:**

- Cada vaina es un módulo T-07: `P/FastCombat/T07_Module.prefab`, que da `Weapon_T07_Temporary_Optimized`.
- Al romper su unión (`DetachableCannon` con `executionOnly = 0` **solo en la variante del jefe**), cae al piso de su nivel y se recoge con E.
- 6 vainas = 6 cañones ≈ 1770 de daño. **Aquí vive el pilar original del juego.**

**Fases (de abajo arriba):**

1. Anillo inferior (2 vainas) + 2 TUR en el suelo.
2. Anillo medio + suelo electrificado.
3. Corona: núcleo superior, vulnerable solo a disparos cargados ≥ 80 % del cañón robado [SUPUESTO] y a granadas lanzadas a las rejillas de ventilación.

**Ataques telegrafiados:**

| Ataque | Señal (tell) | Ventana activa | Respuesta del jugador |
|---|---|---|---|
| Barrido láser | 2 s: línea roja que recorre el suelo | Láser a 1 m o a 2 m de altura | A 1 m, saltar; a 2 m, agacharse o slide |
| Salva de vainas | Carga magenta de 1,2 s (existente, CHK:769) | Disparos cargados | Cobertura alta |
| Suelo electrificado por sectores | 1,5 s: las rejillas parpadean en ámbar | Sector activo | Cambiar de nivel |

**Escudo giratorio:** blinda la cara que mira al jugador. Hay que flanquear alrededor de la columna.

**Debilidades:** las uniones de las vainas, el lado ciego del escudo y sus propios cañones. Los refuerzos T07 ejecutables llegan en ascensores.

**Refuerzos:** T07 por los ascensores (fuentes); en la fase 2, HOV.

**Arena:**

- Cilindro de Ø ≥ 50 m.
- 3 niveles (0, +6 y +12 m) en anillos de ≥ 8 m de ancho alrededor del hueco central.
- Rampas en espiral ≤ 30°, 2 por nivel y opuestas.
- Techo ≥ 22 m.
- Caída entre niveles ≤ 6 m.
- **Repisas de recogida bajo cada anillo**, para que ninguna vaina caiga al hueco.
- 3 alcobas de refuerzo por nivel.
- Coberturas altas cada 8 m: las vainas alcanzan 43,75 m.

**Base técnica:**

- Reutiliza: `T07_Module` y el cañón (verificados), `DetachableCannon` (la unión tiene salud propia: 35 en el Mech), `CannonCharge_Magenta`, la torreta fija (`EnemyTurret`) como base de las vainas, y geometría procedural (`MeshKit` Lathe/Tube de `VisualOverhaulAssets`): **arte barato**.
- Estática: sin agente.
- Nuevo: controlador de anillos y escudo, láser, suelo electrificado (`DamageArea` por sector), `Map01BossEncounter` y HUD.

**Riesgos:** puede leerse como «muchas torretas» en vez de como una criatura; verticalidad de 3 niveles con NavMesh en espiral para los refuerzos (rampas ≥ 5 m); cámara mirando hacia arriba; vainas que caen al hueco (por eso las repisas).

**Coste: medio.**

### A7.4 Comparación y recomendación

| Criterio | A · Custodio | B · Matriarca | C · Panóptico |
|---|---|---|---|
| Mecánica central (robar, OVERDRIVE, expulsar) | Alta: las armas más fuertes del juego, del propio jefe | Alta: la granada en la boca es el remate | Muy alta: el pilar original en estado puro |
| Lectura y espectáculo | Alta: criatura móvil, embestida y desplome | Alta: escala y horda | Media: arquitectura |
| Reutilización verificada | **Alta** (`BigBot_CollectibleCannon` probado: MAPK:33) | Media (SAW, CAR, TUR) | Alta (`T07_Module`, `DetachableCannon`) |
| Código nuevo | Medio (3 ataques y fases) | Medio-alto (brazos, vertido, camadas) | Medio (anillos, escudo, láser) |
| Arte nuevo | Bajo-medio (variante escalada, 2 clips) | **Alto** (máquina grande) | Bajo (procedural) |
| Riesgo de NavMesh e IA | **Medio-alto** (agente grande) | Bajo (estática) | Bajo (estática) |
| Rendimiento | Como un BIG + refuerzos | Horda constante: el más cargado | Bajo |
| **Coste relativo** | **Medio** | **Alto** | **Medio** |

**Recomendación: A, «El Custodio».**

- Es el clímax natural del mapa: el jugador ha visto ocho BIG (B6) [cambio B] que nunca dieron arma, y el jefe por fin se la da.
- Usa las piezas ya probadas del BIG robado y es un jefe móvil, al estilo DOOM.
- **Plan B: C.** Mismo coste y sin riesgo de agente grande; si el prototipo de F4 falla por la escala o el NavMesh, se cambia a C.
- B solo si hay presupuesto de arte para la máquina.

**Requisitos comunes de la arena del jefe [R1]**, valen para cualquier concepto:

- Ø ≥ 50–56 m y techo ≥ 16–22 m.
- ≥ 2 niveles.
- Puerta clase B de 14 × 10 m, con antecámara de 20 × 16 × 12 m (MD §5.2) que funcione como respiro con 4 botiquines.
- 4 grupos de slots de refuerzo de ≥ 3 cada uno, con NavMesh conectado.
- Suelo plano donde caen las armas; sin fosos.
- Circulación en anillo de ≥ 8 m.

**[R0]:** entrada en el checklist para la IA del jefe como tarea aparte.

---

## A8. Datos F2: `Assets/IronRequiem/Levels/Map01/Data/Encounters/R<NN>.json`

### A8.1 Esquema v0

Coordenadas **locales al cuarto**. `EncounterAuthoring` convierte a mundo con el origen y la rotación de `Data/Structure/R<NN>.json`; si el cuarto gira, `bounds` pasa a ser la caja envolvente.

| Campo JSON | Tipo | Destino en Unity | Fuente del campo |
|---|---|---|---|
| `schema` | `"map01-encounters/v0"` | — | — |
| `id` | `"R05"` | Escena `Map01_R05_*`, raíz `[Encounters]` | MD §4 |
| `kind` | `rounds` · `pursuit` · `respite` · `boss` | `CampaignRoom.rounds` / `pursuit` / ninguno + `Map01StageRelease` / `Map01BossEncounter` | `S/Levels/CampaignRoom.cs:9-10,23` |
| `archetype` | `E0`…`E9` | Documentación y validador | A4 |
| `title` | string | `CampaignRoom.prologueTitle` | `CampaignRoom.cs:13`; `CampaignFlow.cs:273` |
| `rounds.arenaTitle` | string | `CombatTwoRoomsLab.arenaTitle` → HUD «título · RONDA i / N» | `CombatTwoRoomsLab.cs:25,98` |
| `rounds.bounds` | `{center, size}` local | `firstRoom` (Bounds de mundo) | `:19,63` |
| `rounds.bounds2` | `null` o `{…}` | `secondRoom`; si es `null` y hay > 3 rondas, copia de `bounds` | `:19,121` |
| `rounds.slots[]` | `{id, pos, cluster, class: normal\|big\|static}` | `firstRoomSpawns`: Transforms `Slot_<id>` en `[Encounters]/Slots` | `:18` |
| `rounds.slots2` | `null` o lista de ids | `secondRoomSpawns`; si es `null`, las mismas referencias | `:18,103` |
| `rounds.waves[].groups[]` | `{enemy, count, fixedSlot?}` | `Wave.Groups[]` → `Group{Prefab, Count, FixedPoint}` | `:13,15` |
| `rounds.waves[].variations[]` | `{name, groups[]}` | `Wave.Variations[]` → `Composition{Name, Groups}` | `:14` |
| (fijo) | — | `singleArena = true`, `variedSpawns = true`, `gate = null`, `gatePreparationSeconds = 0` | `:21,24,26,31` |
| `rounds.seed` | int | `scenarioSeed` (F2: distinto de 0; entrega: 0) | `:33` |
| `rounds.playerClearance` | 6 | `playerClearance` (no bajar) | `:20` |
| `rounds.spawnInterval` | `[0.2, 0.4]` | `minimumSpawnInterval` / `maximumSpawnInterval` | `:34-35` |
| `rounds.separationPadding` | 0,5 | `separationPadding` | `:36` |
| `queue` | `{prewarmPerFrame: 1, activationsPerFrame: 1}` | `EnemySpawnQueue`, asignada a `CombatTwoRoomsLab.spawnQueue` y a `CampaignRoom.queue` | `S/Levels/EnemySpawnQueue.cs:16,18`; `CombatTwoRoomsLab.cs:29`; `CampaignRoom.cs:11` |
| `health[]` | `{id, pos, heal: 25}` | `Pickup_Health` con `HealAmount` explícito | `Pickup_Health.prefab:86`; `CampaignAuthoring.cs:176` |
| `release` | `{mode, volume?, delay}` | `Map01StageRelease` (nuevo) | A6.1 |
| `pursuit.trigger` | `{center, size}` | BoxCollider (trigger) del `ChaseCorridorEncounter` | `ChaseCorridorEncounter.cs:70-72` |
| `pursuit.sawSpeed`, `warningSeconds`, `bladeRpm`, `sawDamage`, `damageInterval`, `sawContactOffset`, `halfWidth` | números | Campos homónimos | `ChaseCorridorEncounter.cs:32-40` |
| `pursuit.finishLocalZ`, `courseCenterLocal` | locales | `finishZ` y `courseOffset` **en mundo** | `:39,41,84,91` |
| `pursuit.ambushes[]` | `{activationLocalZ, spawns[{enemy, pos}]}` | `Ambush{ActivationZ (mundo), Prefabs[], Points[]}` | `:16-21` |
| `pursuit.entranceDoorRef`, `exitDoorRef`, `carriageRef`, `bladesRef`, `motorRef` | ID en `[Doors]` / `[Encounters]` | `entranceDoor`, `exitDoor`, `carriage`, `blades`, `motor` | `:26-29` |
| `alarm` | `{lightAnchors[], beaconAnchors[], beaconLightAnchors[], beaconHeadAnchors[], sirenAnchor, klaxon, screenAnchors[], alertText, clearText, alarmIntensity, beaconIntensity, beaconDegreesPerSecond, bannerSeconds}` | `CorridorAlarm` (referencias por ID de anclaje de R5 y R4) | `S/Levels/CorridorAlarm.cs:11-21` |
| `shutters[]` | `{doorRef, closeOnRound, openOnRound?, openOnComplete, sweepVolume}` | `Map01ShutterController` (nuevo) | A6.3 |
| `boss` | `{concept, prefab, spawn, phases[{untilHealth, reinforcements[]}]}` | `Map01BossEncounter` (nuevo) | A7 |
| — | — | `CombatTuningProfile`: la campaña no lo usa; sin datos | INV §C3 |

**Alias de enemigo → prefab:**

| Alias | Prefab |
|---|---|
| `T07` | `Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab` |
| `T07M` | `.../Campaign/T07_Mech.prefab` |
| `SAW` | `.../Campaign/SawBot_Current.prefab` |
| `BIG` | `.../Campaign/BigBot_Current.prefab` |
| `HOV` | `.../Campaign/HoverBot_Current.prefab` |
| `TUR` | `.../Campaign/Turret_Current.prefab` |
| `CAR` | `.../Campaign/Carronero.prefab` |

`unitsOf(alias)`: CAR = 0,5; BIG = 2; resto = 1.

### A8.2 Ejemplo (cuarto E2 hipotético de 36 × 40 m, con la entrada en z = −20)

```json
{
  "schema": "map01-encounters/v0",
  "id": "RXX",
  "kind": "rounds",
  "archetype": "E2",
  "title": "COMEDOR · TRES RONDAS",
  "rounds": {
    "arenaTitle": "COMEDOR",
    "bounds": { "center": [0, 5, 0], "size": [36, 12, 40] },
    "bounds2": null,
    "playerClearance": 6,
    "spawnInterval": [0.2, 0.4],
    "separationPadding": 0.5,
    "seed": 0,
    "slots": [
      { "id": "A1", "pos": [-14, 0, 14], "cluster": "A", "class": "normal" },
      { "id": "A2", "pos": [-11, 0, 17], "cluster": "A", "class": "normal" },
      { "id": "A3", "pos": [-15, 0, 9], "cluster": "A", "class": "normal" },
      { "id": "B1", "pos": [14, 0, 14], "cluster": "B", "class": "normal" },
      { "id": "B2", "pos": [11, 0, 17], "cluster": "B", "class": "normal" },
      { "id": "B3", "pos": [15, 0, 9], "cluster": "B", "class": "normal" },
      { "id": "C1", "pos": [-15, 0, -6], "cluster": "C", "class": "normal" },
      { "id": "C2", "pos": [-12, 0, -9], "cluster": "C", "class": "normal" },
      { "id": "D1", "pos": [15, 0, -6], "cluster": "D", "class": "normal" },
      { "id": "D2", "pos": [12, 0, -9], "cluster": "D", "class": "normal" },
      { "id": "E1", "pos": [0, 4, 17], "cluster": "E", "class": "normal" }
    ],
    "slots2": null,
    "waves": [
      { "groups": [ { "enemy": "SAW", "count": 1 } ], "variations": [] },
      { "groups": [ { "enemy": "SAW", "count": 1 }, { "enemy": "CAR", "count": 4 } ], "variations": [] },
      {
        "groups": [ { "enemy": "T07", "count": 2 }, { "enemy": "SAW", "count": 1 }, { "enemy": "CAR", "count": 4 } ],
        "variations": [
          { "name": "Presión", "groups": [ { "enemy": "T07", "count": 2 }, { "enemy": "SAW", "count": 1 }, { "enemy": "CAR", "count": 4 } ] },
          { "name": "Flancos", "groups": [ { "enemy": "T07", "count": 2 }, { "enemy": "HOV", "count": 1 }, { "enemy": "SAW", "count": 1 }, { "enemy": "CAR", "count": 2 } ] }
        ]
      }
    ]
  },
  "queue": { "prewarmPerFrame": 1, "activationsPerFrame": 1 },
  "health": [
    { "id": "H1", "pos": [17, 0, 2], "heal": 25 },
    { "id": "H2", "pos": [-17, 4, 18], "heal": 25 }
  ],
  "release": null,
  "pursuit": null,
  "alarm": null,
  "shutters": [],
  "boss": null
}
```

**Lectura del ejemplo:**

- Ronda 3, «Presión»: 2 + 1 + 2 = 5 unidades y 7 cuerpos.
- Ronda 3, «Flancos»: 2 + 1 + 1 + 1 = 5 unidades y 6 cuerpos. Las dos tienen 3 ejecutables (R-E7).
- 11 slots ≥ 1,5 × 7 cuerpos, en 5 grupos. Los de la ronda 1 están a ≥ 10 m de la entrada.

### A8.3 Ejemplos de bloques especiales

```json
{ "release": { "mode": "OnEnterVolume", "volume": { "center": [0, 2, 10], "size": [12, 4, 6] }, "delay": 0 } }
```

```json
{ "pursuit": {
    "trigger": { "center": [0, 2, 12], "size": [16, 4, 2] },
    "courseCenterLocal": [0, 0, 0], "halfWidth": 8, "finishLocalZ": 195,
    "sawSpeed": 8, "warningSeconds": 2, "bladeRpm": 240, "sawDamage": 100, "damageInterval": 0.35, "sawContactOffset": 2.1,
    "carriageRef": "Shredder", "bladesRef": ["Shredder/RollerA", "Shredder/RollerB"], "motorRef": "Shredder/Motor",
    "entranceDoorRef": "ChaseEntrance", "exitDoorRef": "ChaseExit",
    "ambushes": [
      { "activationLocalZ": 40,  "spawns": [ { "enemy": "T07", "pos": [5, 0, 62] } ] },
      { "activationLocalZ": 95,  "spawns": [ { "enemy": "CAR", "pos": [-4, 0, 112] }, { "enemy": "CAR", "pos": [4, 0, 114] } ] },
      { "activationLocalZ": 140, "spawns": [ { "enemy": "HOV", "pos": [0, 0, 160] } ] }
    ] } }
```

### A8.4 Reglas del validador de `EncounterAuthoring` (bloquean la autoría)

| # | Regla |
|---|---|
| V1 | `Σ unitsOf ≤ 8` y `Σ count ≤ 12` por composición |
| V2 | Fuentes por composición: R-E1 a R-E4 y R-E6. Las variaciones de una ronda conservan ejecutables, pesados y unidades ± 0,5 (R-E7) |
| V3 | `slots.length ≥ ceil(1,5 × máx. de cuerpos)`, ≥ 3 grupos y grupos a ≥ 15 m entre sus centros |
| V4 | Un slot `big` por cada BIG de la ronda × 2. Los `static` solo los usan grupos TUR con `count = 1` y `fixedSlot` único |
| V5 | Más de 3 rondas ⇒ `secondRoom` y `secondRoomSpawns` no vacíos tras resolver los `null` |
| V6 | Ningún slot fuera de `bounds`; slots de la ronda 1 a ≥ 10 m de la puerta de entrada (dato de R1) |
| V7 | `variedSpawns = true` y cola asignada; `title` no vacío |
| V8 | Cada `health.heal` = 25 (R-V1) y número dentro de R-V2 y R-V4 |
| V9 | `kind = respite` ⇒ `release` no nulo; `kind = rounds` ⇒ `waves.length ≥ 1` |
| V10 | `kind = pursuit` ⇒ eje del cuarto en +Z del mundo (dato de R1), puntos de emboscada con `z > activationZ + 6` y ningún BIG ni TUR |
| V11 | Cuerpos totales del cuarto (todas las rondas) ≤ 36 [SUPUESTO] |
| V12 | Tras la autoría (en el Editor, lo ejecuta R0): `NavMesh.SamplePosition` ≤ 0,75 m y `CalculatePath` completo desde cada slot hasta un punto de referencia de cada superficie pisable (gamedev:unity-navmesh) |

---

## A9. Skills usadas

| Skill | Cargada | Qué apliqué |
|---|---|---|
| `gamedev:fps-shooter` | Sí | Tabla de tiempo para matar por arma y por vida (A1.3); enemigos con aviso previo y sin «aimbot» (ventanas de A1.1 y A7); el TTK se ajusta en tabla, no número a número |
| `gamedev:game-ai` | Sí | Separar percepción, decisión y movimiento en las fases del jefe; guardas contra atascos (S2, S3, A5.3) y contra la omnisciencia (memoria de 6 s del T07) |
| `tune-enemy-ai` | Sí | Estados con señal, ventana y recuperación (A7); escenarios deterministas: semilla en F2 (A5.2) |
| `design-game-encounters` | Sí, **solo principios** (es de three.js) | Encuentro como decisión: una presión nueva cada vez (A2), tope de atacantes comprometidos (S9, A5.1), recompensas y recuperación (A3.6) |
| `design-action-combat` | Sí, **solo principios** | Cada ataque con señal, ventana activa, recuperación y respuesta propia (slide, salto, dash, cobertura); las fases cambian la presión sin invalidar lo aprendido (A7) |
| `gamedev:unity-navmesh` | Sí | Camino completo frente a parcial, islas y `NavMeshLink` (S2, S3); tamaño de agente frente al horneado (D-E6); verificación con `CalculatePath` (V12) |
| `iron-requiem-enemy-ai` (local) | Leída | Reutilizar EnemyController, EnemyMobile y EnemyTurret; perder un cañón cambia capacidades sin destruir la navegación (fases A y C) |
| `iron-requiem-modular-weapons` (local) | Leída | Estados Attached → Available → Equipped → Thrown/Detonated, energía por instancia e inventario lleno: base de A3 y de las reglas de una sola arma robada |
| `iron-requiem-performance` (local) | Leída | Presupuesto por medida (D3: se mide en F2), coste de aparición del T07 y del BIG, precarga y picos de FX (A5.1, E3) |

Prioridad aplicada: AGENTS.md > iron-requiem-* > unity:* > gamedev:* > genéricas. No se cargó `unity:initialize-ai-navigation`: no hornea nada en F1.

**Sin verificar** (necesita el Editor; queda para R0 o F2):

- Si el Carroñero es ejecutable.
- Si `Turret_Current` es fija.
- Detección, ataque y daño del T07M.
- Daño y cadencia reales de la TUR.
- DPS sostenido real de la principal.
- Que `Map01StageRelease` sea inocuo con `IsPrologue`.

---

## PARTE B — Encuentros por cuarto

**Base:** `01_ESTRUCTURA.md` de R1 (18/11): §1 (D-U1…D-U6 y componentes nuevos), §5.2, §6, §7, §8, §9 y §13.

- Coordenadas **locales** de las fichas de R1 como (x; y; z). Yaw 0 en todo el mapa: mundo = origen de §5.2 + local.
- Notación de ronda: **u / c · f** = unidades / cuerpos · fuentes (ejecutables).
- Los cambios hechos a la Parte A por esta parte llevan **[cambio B]**: S1, la fila E2, R-V2, R-V4 y A5.3, más las menciones a «diez» Big Bot.

### B0. Reglas comunes de la Parte B

**Fin de ronda** (todos los cuartos con rondas):

- No queda vivo ningún enemigo registrado en la ronda y `pendingSpawns == 0` (`CombatTwoRoomsLab.cs:66`).
- La TUR cuenta: hay que destruirla.
- La ronda siguiente entra en el mismo frame (D-E5).

**Apertura de la salida:**

| Tipo de cuarto | Condición | Componente |
|---|---|---|
| Combate y alarma | `CampaignRoom.Completed` = `rounds.Completed` → `CampaignFlow` abre la `SourceDoor` | `CampaignFlow.cs:96` (existente) |
| Respiro | `Map01StageRelease` (`OnEnterVolume`) → `CampaignRoom.Release()` | Nuevo (A6.1) |
| Último cuarto de una sesión (D-U4 c: R05 y R13) | `Release()` + `Map01SessionHandoff` abre la salida y carga la sesión siguiente | Nuevos (R1 §1) |
| Persecución (R10) | `pursuit.Completed` con z ≥ `finishZ` (195); `exitDoor` interna con `SetActive(false)` | `ChaseCorridorEncounter.cs:84-85` |
| Jefe (R18) | `Map01BossEncounter` → `Release()` y fin de mapa ([R0]) | Nuevo (A7) |

**Slots:**

- Con `variedSpawns`, **cada ronda baraja todos los slots** de `firstRoomSpawns` (rondas 1–3) o de `secondRoomSpawns` (rondas ≥ 4) (`CombatTwoRoomsLab.cs:103-104`). No se puede asignar un grupo a una ronda: solo forzar un slot con `FixedPoint`.
- **Lista de cada cuarto** = todos los slots normales y grandes de la ficha de R1, **menos** el slot fijo de la TUR y el slot grande de presentación de R08 (BS1). Esos dos solo se usan como `FixedPoint`, para que ningún otro enemigo los ocupe antes.
- `secondRoom` = `firstRoom` y `secondRoomSpawns` = la misma lista en **todos** los cuartos de combate (R1 E-5; obligatorio en R07).

**Configuración y textos:**

- `variedSpawns = true` y `spawnQueue` asignada siempre.
- Semilla distinta de 0 en F2; 0 en la entrega (A5.2).
- **Variaciones:** solo donde la tabla dice «Sí», y siempre con el mismo número de fuentes, de pesados y de unidades (R-E7).
- **Títulos:** `prologueTitle` en todas las etapas (B1.1). `arenaTitle` es el nombre corto del cuarto; el HUD muestra «NOMBRE · RONDA i / N».

**Pórtico P de entrada (D-U5):** baja **solo cuando el jugador está dentro de los bounds y a ≥ 4 m del plano de la persiana**. Si el jugador vuelve hacia fuera antes de que baje, queda abierta. Así nunca deja al jugador fuera de la arena (B7, petición 9).

### B1. Tabla por cuarto

#### B1.1 Resumen

| ID | Función | Rondas | Máx. simultáneo (u / c) | Cuerpos totales | Variaciones | Apertura | Fuentes (T07 · T07M · SAW) | Botiquines | `prologueTitle` |
|---|---|---|---|---|---|---|---|---|---|
| R01 | E1, introducción y tutorial de ejecución | 2 | 1,5 / 3 | 4 | No | Completed | 1 (1·0·0) | 2 | CONTROL DE ACCESO · 2 RONDAS |
| R02 | E2 vertical | 3 | 5 / 7 | 17 | No | Completed | 7 (7·0·0) | 2 | POZO DEL MONTACARGAS · 3 RONDAS |
| R03 | E2, presentación del HOV | 3 | 5 / 6 | 14 | No | Completed | 4 (4·0·0) | 2 | DORMITORIOS · 3 RONDAS |
| R04 | E2, presentación del SAW | 3 | 5 / 6 | 12 | No | Completed | 4 (1·0·3) | 2 | COMEDOR · 3 RONDAS |
| R05 | E0 respiro (fin de S1) | 0 | — | 0 | — | Release + SessionHandoff | — | 3 (+1 opcional) | ENFERMERÍA · ZONA SEGURA |
| R06 | E4, presentación de la TUR | 3 | 5,5 / 7 | 17 | Ronda 2 | Completed | 6 (5·0·1) | 3 | ARMERÍA · 3 RONDAS |
| R07 | E8 alarma | **5** | 7 / 10 | 33 | Ronda 3 | Completed (+ P de ala) | 14 (9·0·5) | 3 + 2 en las alas | COMUNICACIONES · ACCESO RESTRINGIDO |
| R08 | E3, presentación del BIG y del T07M | 3 | 6 / 6 | 17 | Ronda 3 | Completed | 7 (4·2·1) | 4 | ALMACENES · 3 RONDAS |
| R09 | E0 respiro | 0 | — | 0 | — | Release | — | 3 | TÚNEL DE SERVICIO · LIMPIEZA EN CURSO |
| R10 | E7 persecución | 3 emboscadas | 2 por emboscada | 6 | No | `pursuit.Completed` | 2 (2·0·0), opcionales | 3 | TÚNEL DE LA TRITURADORA · CORRE |
| R11 | E6 horda | 3 | 7 / 10 | 26 | Rondas 1 y 3 | Completed | 7 (4·0·3) | 3 | DECANTADORES · 3 RONDAS |
| R12 | E2/E3 con grieta | 3 | 7 / 9 | 23 | Ronda 3 | Completed | 8 (4·2·2) | 3 | COLECTOR DERRUMBADO · 3 RONDAS |
| R13 | E0 respiro (fin de S2) | 0 | — | 0 | — | Release + SessionHandoff | — | 3 | SALA DE BOMBAS · ZONA SEGURA |
| R14 | E3, combinaciones | 3 | 7 / 9 | 22 | Ronda 3 | Completed | 11 (6·1·4) | 4 | FUNDICIÓN · 3 RONDAS |
| R15 | E2, combinaciones (jaula P) | 3 | 8 / 10 | 26 | Ronda 2 | Completed (+ jaula) | 13 (5·3·5) | 2 + 2 en la jaula | ENSAMBLAJE · 3 RONDAS |
| R16 | E3 + E4, pico 2 | 3 | 8 / 10 | 21 | Ronda 2 | Completed | 11 (7·0·4) | 4 | REACTOR · 3 RONDAS |
| R17 | E0 respiro (E8 opcional: **no** recomendado) | 0 | — | 0 | — | Release | — | 3 | CENTRO DE MANDO · BÓVEDA AL NORTE |
| R18 | E9 jefe | Fases | Jefe + ≤ 5 u | Jefe + 10 (≤ 13) | No | BossEncounter | 5 (+ 2 cañones del jefe en A) | 4 + 4 | BÓVEDA |

#### B1.2 Rondas por cuarto

**R01 · Control de acceso** (E1). Slots: G1 4, G2 3 (entreplanta +3,5), G3 3 = 10.

| Ronda | Composición | u / c · f | `FixedPoint` | Variación |
|---|---|---|---|---|
| 1 | CAR ×3 | 1,5 / 3 · 0 | — | No |
| 2 | **T07 ×1** (tutorial de ejecución) | 1 / 1 · 1 | — | No |

- La ronda 1 no tiene fuente: es la única excepción a R-E8, antes del tutorial. El cañón ganado pasa a R02, donde se gasta y se lanza.
- Botiquines: (−13; 0; 6) en el carril W y (13; 3,5; −4) en la entreplanta.

**R02 · Pozo del montacargas** (E2 vertical). Slots: G1 3, G2 3, G3 3 (cornisa W +6), G4 3 (cornisa E +6) = 12.

| Ronda | Composición | u / c · f | `FixedPoint` | Variación |
|---|---|---|---|---|
| 1 | T07 ×1 + T07 ×1 + CAR ×2 | 3 / 4 · 2 | Un T07 en el slot de G3 en z ≈ 12 (francotirador al otro lado del pozo, a ≈ 40 m de la llegada) y el otro en G1, en (−7; 0; 19) | No |
| 2 | T07 ×2 + CAR ×4 | 4 / 6 · 2 | — | No |
| 3 | T07 ×3 + CAR ×4 | 5 / 7 · 3 | — | No |

- Primer OVERDRIVE y primer lanzamiento (R1 §8).
- Botiquines: (0; 0; −8) en el suelo, en el carril de slide, y (−18; 6; 20) en el extremo N de la cornisa W.

**R03 · Dormitorios** (E2). Slots: G1 3 (galería W +3,5), G2 3, G3 3, G4 3 = 12.

| Ronda | Composición | u / c · f | Variación |
|---|---|---|---|
| 1 | HOV ×2 + T07 ×1 | 3 / 3 · 1 | No |
| 2 | HOV ×2 + T07 ×1 + CAR ×2 | 4 / 5 · 1 | No |
| 3 | T07 ×2 + HOV ×2 + CAR ×2 | 5 / 6 · 2 | No |

- Botiquines: (−16; 3,5; −6) en la galería W y (16; 3,5; 6) en la galería E.

**R04 · Comedor** (E2). Slots: G1 3 (cocina +4,5), G2 3, G3 3, G4 3 = 12.

| Ronda | Composición | u / c · f | Variación |
|---|---|---|---|
| 1 | **SAW ×1** solo (bucle alrededor de la isla) | 1 / 1 · 1 | No |
| 2 | SAW ×1 + CAR ×4 | 3 / 5 · 1 | No |
| 3 | SAW ×1 + T07 ×1 + HOV ×2 + CAR ×2 | 5 / 6 · 2 | No |

- Botiquines: (−8; 0; −12) en el carril S y (−18; 4,5; 18) en el extremo W de la cocina.

**R05 · Enfermería** (respiro, fin de S1):

- `Map01StageRelease` `OnEnterVolume` en x 8…16, z −12…0 (R1). Después, `Map01SessionHandoff` en C05.
- Botiquines: 3 (el quirófano y 2 en la sala, según el croquis de R1), +1 opcional en el depósito de la escotilla E.

**R06 · Armería** (E4). Slots: G1 3, G2 3, G3 3 (bajo la pasarela), G4 3 = 12. TUR fija en (20; 6; 23), fuera de la lista.

| Ronda | Composición | u / c · f | `FixedPoint` | Variación |
|---|---|---|---|---|
| 1 «Torre» | **TUR ×1** + T07 ×1 + CAR ×2 | 3 / 4 · 1 | TUR en su slot (`Count` 1). T07 en G4 (12…16; 0…6), al pie de la rampa del nido | No |
| 2 | V-A: T07 ×2 + HOV ×2 + CAR ×2 · V-B: T07 ×1 + SAW ×1 + HOV ×2 + CAR ×2 | 5 / 6 · 2 | — | Sí |
| 3 | SAW ×1 + T07 ×2 + HOV ×1 + CAR ×3 | 5,5 / 7 · 3 | — | No |

- Sin segunda TUR: el slot fijo no se reutiliza (A5.3 [cambio B]).
- Botiquines: (−20; 6; 10) en la entreplanta W, (0; 0; −20) en el carril S y (−4; 6; 25) en la pasarela N.

**R07 · Comunicaciones:** ver B3.

**R08 · Almacenes** (E3). Slots: G1 4 (balcón W +4), G2 5, G3 5, G4 4, BS2, BS3 y BS4 = 21. BS1, solo como `FixedPoint`.

| Ronda | Composición | u / c · f | `FixedPoint` | Variación |
|---|---|---|---|---|
| 1 «Yunque» | **BIG ×1** + T07 ×1 + T07 ×1 + CAR ×2 | 5 / 5 · 2 | BIG en BS1 (−10; −24), a 46 m de la llegada. T07 en el slot de G1 más al S (z ≈ −5) y en G4 (14; 0; 14), del lado del jugador | No |
| 2 (presentación del T07M) | **T07M ×2** + HOV ×2 + CAR ×2 | 5 / 6 · 2 | — | No |
| 3 | V-A: BIG + T07 ×2 + SAW + CAR ×2 · V-B: BIG + T07 + T07M + SAW + HOV | 6 / 6 · 3 y 6 / 5 · 3 | — (BIG en BS2–BS4) | Sí |

- Botiquines: (5; 4; 24) en la cubierta N, (−2; 0; 8) en el anillo N, (−12; 0; −14) en el anillo SW y (20; 0; −10) en el anillo E.
- **[R0]** Precalentar `FX_BigBot_Blast_v4`: es el primer BIG de S2.

**R09 · Taquillas** (respiro): `Map01StageRelease` en x 0…12, z 2…10 (R1); 3 botiquines (R1).

**R10 · Túnel de la trituradora:** ver B4.

**R11 · Decantadores** (E6). Slots: G1 5, G2 5, G3 4, G4 4 = 18.

| Ronda | Composición | u / c · f | Variación |
|---|---|---|---|
| 1 | V-A: T07 ×2 + HOV + CAR ×4 · V-B: T07 + SAW + HOV + CAR ×4 | 5 / 7 · 2 | Sí |
| 2 «Marea» | **SAW ×1** + T07 ×1 + **CAR ×8** | 6 / 10 · 2 | No |
| 3 | V-A: SAW ×2 + T07 + HOV ×2 + CAR ×4 · V-B: SAW + T07 ×2 + HOV ×2 + CAR ×4 | 7 / 9 · 3 | Sí |

- Botiquines (3, R-V2 [cambio B]): (−22; 1,2; 8) en la pasarela de válvulas, (12; 0; −14) y (−6; 0; 22).
- Ninguno en el rellano de llegada: queda cerrado por las P al empezar.

**R12 · Colector derrumbado** (E2/E3). Slots: G1 5, G2 3 (plataforma +5), G3 5, G4 5, BS1 y BS2 = 20.

| Ronda | Composición | u / c · f | Variación |
|---|---|---|---|
| 1 «Blindados» | T07M ×2 + T07 ×1 + HOV ×2 | 5 / 5 · 3 | No |
| 2 «Martillo y yunque» | **BIG ×1** + SAW ×1 + T07 ×1 + CAR ×6 | 7 / 9 · 2 | No |
| 3 | V-A: T07 ×2 + SAW + HOV ×2 + CAR ×4 · V-B: T07M ×2 + SAW + HOV ×2 + CAR ×4 | 7 / 9 · 3 | Sí |

- El BIG sale en BS1 o BS2, los dos en la orilla lejana: la grieta hace de bloque central.
- Botiquines: (−18; 0; −12) en la orilla cercana W, (18; 0; −8) junto al puente B2 y (8; 0; 8) en la orilla lejana.

**R13 · Sala de bombas** (respiro, fin de S2):

- `Map01StageRelease` en x −10…0, z 10…16 (R1). Después, `Map01SessionHandoff` en C13.
- 3 botiquines (R1): 2 en la cabina y 1 en el suelo.

**R14 · Fundición** (E3; primer cuarto de S3). Slots: G1 4, G2 4 (+5), G3 4 (+5), G4 3, G5 3 y BS1–BS4 = 22.

| Ronda | Composición | u / c · f | Variación |
|---|---|---|---|
| 1 «Yunque» | BIG ×1 + T07 ×2 + SAW ×1 + CAR ×2 | 6 / 6 · 3 | No |
| 2 «Relevo» | **SAW ×3** + T07 ×2 + CAR ×4 | 7 / 9 · 5 | No |
| 3 | V-A: BIG + T07 ×2 + T07M + HOV + CAR ×2 · V-B: BIG + T07 + T07M + SAW + HOV ×2 | 7 / 7 · 3 y 7 / 6 · 3 | Sí |

- La sesión empieza sin arma robada: la ronda 1 lleva 3 fuentes.
- Botiquines: (0; 5; 16) en el puente grúa, (0; 0; −16) en el anillo S, (−9; 0; 12) en el anillo N y (15; 0; 0) en el anillo E.
- **[R0]** Precalentar el FX del BIG: la sesión es nueva.

**R15 · Ensamblaje** (E2). Slots: G1 4, G2 4, G3 4 (+5), G4 3 (+5), G5 3 = 18.

| Ronda | Composición | u / c · f | Variación |
|---|---|---|---|
| 1 «Blindados» | T07M ×2 + T07 ×2 + HOV ×2 | 6 / 6 · 4 | No |
| 2 | V-A: SAW ×2 + T07 + HOV + CAR ×6 · V-B: SAW ×2 + T07M + HOV + CAR ×6 | 7 / 10 · 3 | Sí |
| 3 «Relevo» | SAW ×3 + T07 ×2 + T07M ×1 + CAR ×4 | 8 / 10 · 6 | No |

- **Jaula de suministros:** su P sube al **empezar la ronda 3** (`WaveIndex == 2`). Dentro no hay slots.
- Botiquines: (−16; 0; 0) en la nave W, (16; 0; −2) en la nave E y 2 en la jaula, en (−23; 0; −22).

**R16 · Reactor** (E3 + E4). Slots: G1 4, G2 3 (galería NE +6), G3 4, G4 4, G5 3 (galería W +6) y BS1–BS4 = 22. TUR fija en (11; 6; 22), fuera de la lista.

| Ronda | Composición | u / c · f | `FixedPoint` | Variación |
|---|---|---|---|---|
| 1 «Fuego cruzado» | **TUR ×1** + **BIG ×1** + T07 ×2 + SAW ×1 | 6 / 5 · 3 | TUR en su slot; BIG libre | No |
| 2 | V-A: SAW ×2 + T07 ×2 + HOV ×2 + CAR ×4 · V-B: SAW + T07 ×2 + T07M + HOV ×2 + CAR ×4 | 8 / 10 · 4 | — | Sí |
| 3 «Doble tanque» | **BIG ×2** + T07 ×3 + SAW ×1 | 8 / 6 · 4 | — | No |

- Botiquines: (−23; 6; −14) en la galería W, (23; 6; −14) en la galería E, (−6; 0; −6) en el anillo S y (20; 0; 8) bajo la galería E.

**R17 · Centro de mando:**

- Respiro: `Map01StageRelease` en x −6…6, z 2…6 (R1).
- Botiquines: (−14; 0; 12), (14; 0; 12) y (0; 3; −16).
- **E8 opcional, no recomendado:** con D-U4 (c), un cierre más justo antes del jefe se repetiría en cada intento contra él (B6).
- Si el usuario lo quiere, en los 4 grupos de 3 de R1:
  - Ronda 1: T07 ×2 + HOV ×2 + CAR ×2 (5 / 6 · 2).
  - Ronda 2: SAW ×2 + T07 ×2 + CAR ×4 (6 / 8 · 4).
  - Ronda 3: T07M ×2 + T07 + SAW + HOV ×2 (6 / 6 · 4).

**R18 · Bóveda:** ver B5.

### B2. Comprobación cuarto a cuarto

#### B2.1 Capacidad, slots, Big Bot, torreta y bounds

| ID | Máx. cuerpos | Slots necesarios (1,5×) | Slots en la lista | Grupos / distancia (R1) | BIG | TUR | Bounds | Veredicto |
|---|---|---|---|---|---|---|---|---|
| R01 | 3 | 5 | 10 | 3 / 21–29 m ✓ | — | — | Incluyen la ruta ✓ | ✓ |
| R02 | 7 | 11 | 12 | 4 / ≥ 15,2 m ✓ | — | — | Sin galería de llegada (B2.2) | ✓ con condiciones |
| R03 | 6 | 9 | 12 | 4 / ≥ 17 m ✓ | — | — | ✓ | ✓ |
| R04 | 6 | 9 | 12 | 4 / 19–38 m ✓ | — | — | ✓ | ✓ |
| R06 | 7 | 11 | 12 + TUR fija | 4 / 20–48 m ✓ | — | Nido de 8 × 10 a +6 con rampa ✓; flanqueo por la pasarela N ✓; visión ≤ 60 % [VALIDAR F2] con las pantallas de reserva de R1 | Sin entreplanta W (B2.2) | ✓ con condiciones |
| R07 | 10 | 15 | 18 | 4 / 28–30 m ✓ | — | — | ✓; `secondRoom` = copia | ✓ (B3) |
| R08 | 6 | 9 | 21 + BS1 fijo | 4 / 28–45 m ✓ | Ronda 1: BS1 fijo. Ronda 3: 3 grandes libres (≥ 2) ✓. Anillo de 14–16 m, perímetro ≈ 100 m, 7 pilares de 3 × 3 ✓ | — | Sin cubierta N (B2.2) | ✓ con condiciones |
| R10 | 2 por emboscada | — | 6 puntos | — | Sin BIG ✓ | Sin TUR ✓ | `finishZ` 195 ✓ | ✓ (B4) |
| R11 | 10 | 15 | 18 | 4 / 15,3–41 m ✓ | — | — | Sin pasarela S (B2.2) | ✓ con condiciones |
| R12 | 9 | 14 | 20 | 4 / 16,6–57 m ✓ | 1 BIG con 2 grandes ✓. Matiz en B2.3 | — | Foso fuera ✓ | ✓ con matiz |
| R14 | 9 | 14 | 22 | 5 / 24–48 m ✓ | 1 BIG por ronda con 4 grandes ✓. Anillo de 6 m, 4 pilares de 3 × 3 ✓ | — | ✓ | ✓ |
| R15 | 10 | 15 | 18 | 5 / 18–46 m ✓ | — | — | ✓ | ✓ |
| R16 | 10 | 15 | 22 + TUR fija | 5 / 16,6–50 m ✓ | Ronda 3: 2 BIG con 4 grandes ✓. Anillo ≥ 6 m alrededor del núcleo de Ø 10 ✓ | Nido de 6 × 6 a +6 ✓; flanqueo por la galería E → NE ✓; visión [VALIDAR F2] | Sin pasarela S (B2.2) | ✓ con condiciones (BS4: petición 7) |
| R17 (E8 opc.) | 8 | 12 | 12 | 4 ✓ | — | — | Habría que definirlos | ✓ si se activa |
| R18 | Refuerzos ≤ 5 u | — | 12 en alcobas + 4 en la galería | 4 alcobas | Stand-in de A: petición 1 | B y C: petición 1 | Arena de R1 ✓ | Pendiente de la petición 1 |

Distancias del jugador al entrar en los bounds frente a los slots de la ronda 1, comprobadas en papel con las fichas: todas ≥ 6,4 m (normales) y ≥ 8,25 m (BIG). Las más justas: R14 G4/G5 a ≈ 8,2 m y R08 G4 a ≈ 8,2 m si se entra por la rampa NE. Las dos son válidas.

#### B2.2 Las dos diferencias que señala R1

**1. Plataformas a +6 donde E2 pedía +3,5–4,5. Aceptable.** Se cambia la fila E2 de A4.2 [cambio B].

| Cuarto | Caso | Veredicto y motivo |
|---|---|---|
| R02 | Terrazas macizas a +6 (≈ 35 % de la planta), porque el cuarto desciende 12 m | **Aceptable.** Las caídas son de 6 m (≤ 8, voluntarias), hay 2 rampas opuestas y un bucle en «8», y el techo de 21 m supera la cota + 4,5. Las cornisas con slots (G3 al W y G4 al E) quedan en lados opuestos: no se suman sobre la misma zona del suelo |
| R06 | Entreplanta W, pasarela N y nido a +6 | **Aceptable:** es E4, que pide el nido a +5…+8. Techo 14 ≥ 10,5. Ocupan ≈ 25 % de la planta |
| R16 | Galería en U y nido a +6 | **Aceptable:** E3 + E4. Las galerías W, E y NE van sobre pilares con 5,5 m libres debajo: no quitan suelo. Solo la pasarela S es maciza (≈ 12 %) |
| R15, R14 | Pasarelas a +5 | **Aceptable** (CC-3: paso inferior de 4,5 m) |

**2. Bounds que no incluyen la entreplanta o pasarela de llegada (R02, R06, R08, R11 y R16). Aceptable** con cuatro condiciones. Se cambia S1 [cambio B].

| # | Condición | R02 | R06 | R08 | R11 | R16 |
|---|---|---|---|---|---|---|
| C1 | La plataforma excluida está conectada al NavMesh de la arena (S2 / E-2): si el jugador se refugia en ella, los enemigos llegan y las apariciones siguen | ✓ rampa de la galería | ✓ rampa SW y pasarela N | ✓ rampa NE y balcón W | ✓ rampas E y W | ✓ galerías W y E |
| C2 | Ningún slot en la parte excluida | ✓ | ✓ | ✓ | ✓ | ✓ |
| C3 | La salida (en el suelo) solo se alcanza cruzando los bounds | ✓ | ✓ | ✓ | ✓ | ✓ |
| C4 | El pórtico P cierra el rellano solo con el jugador ≥ 4 m dentro de los bounds (B0) | Petición 9 | Ídem | Ídem | Ídem | Ídem |

**Efecto en el juego.** La ronda 1 empieza cuando el jugador baja o cruza el borde, no al llegar. Gana unos segundos para leer la arena desde arriba, lo que viene bien en E3 y E4.

**R06 y R08 dejan un refugio fuera de bounds** (el resto de la entreplanta W y la cubierta N) al que el BIG no sube (Jump, D-U6) pero el resto sí. No es un atasco: el BIG dispara a 28,6 m y los demás llegan.

#### B2.3 Matiz de R12 (BIG con grieta)

- Cada orilla mide 44 × 24,5 m: menos que la planta E3 (44 × 48) y sin bloque central.
- **Acepto un BIG en la ronda 2**, que aparece en la orilla lejana (BS1 o BS2):
  - la grieta de 7 m hace de bloque central;
  - el bucle orilla → puente → orilla rodea la grieta;
  - los puentes miden 7 y 8 m (≥ 5) ✓;
  - LV-3: la caseta H de la orilla cercana (5; −17) está a 33,6 m de BS1 (> 28,6) ✓.
- **No más de un BIG por ronda en R12.**

### B3. R07 · Alarma

**Rondas** (las 4 y 5 usan `secondRoom` = `firstRoom` y `secondRoomSpawns` = los mismos 18 slots: A5.4 y V5):

| Ronda | Composición | u / c · f | Variación | Nota |
|---|---|---|---|---|
| 1 (dispara la alarma) | T07 ×2 + CAR ×2 | 3 / 4 · 2 | No | Bajan las P |
| 2 | HOV ×2 + SAW ×1 + CAR ×2 | 4 / 5 · 1 | No | — |
| 3 | V-A: T07 ×2 + SAW + HOV ×2 + CAR ×2 · V-B: T07 + SAW ×2 + HOV ×2 + CAR ×2 | 6 / 7 · 3 | Sí | — |
| 4 (índice 3) | T07 ×3 + SAW ×1 + CAR ×6 | 7 / 10 · 4 | No | Primera ronda con `secondRoom` |
| 5 (índice 4) | SAW ×2 + T07 ×2 + HOV ×2 + CAR ×1 | 6,5 / 7 · 4 | No | Apagón parcial opcional [R5] |

- Total: 33 cuerpos (≤ 36 ✓) y 14 fuentes.
- Sin BIG (se presenta en R08) y sin TUR (no hay nido).
- Escalada: 3 → 4 → 6 → 7 → 6,5 unidades. La última ronda baja de cuerpos y sube de tensión con la luz.

**`CorridorAlarm`** (existente; se dispara con `WaveIndex >= 0`, `CorridorAlarm.cs:46`; se apaga con `Completed`, `:56`):

| Campo | Referencia |
|---|---|
| `rounds` | El `CombatTwoRoomsLab` de R07 |
| `alarmLights` | Las 2 rojas de los tabiques (±18,5; 5; 0), de R5 |
| `beacons`, `beaconLights`, `beaconHeads` | El plato de radar del mástil (0; 8; 0), de R5 y R2 |
| `siren` y `klaxon` | AudioSource en el mástil (de R5 o R0) y el clip existente de Room00b |
| `screens` | Consolas del anillo (R2) |
| `alertText` | «ALERTA · INTRUSIÓN EN COMUNICACIONES» |
| `clearText` | «ZONA DESPEJADA · ESCLUSA ABIERTA» |

Todas las referencias se enlazan por ID de anclaje (A8).

**Persianas (`ArenaShutter`, de R4):**

| Persiana | Baja | Sube | Condición extra |
|---|---|---|---|
| 4 P de ala (6 × 4,5) en x ±18,5, z −14…−8 y 8…14 | Al empezar la ronda 1 | Con `Completed` | Solo con el volumen de barrido libre y el jugador dentro de los bounds. **No suben entre rondas** |
| P del pórtico (10 × 6) en z 20 | Al empezar la ronda 1 | No hace falta (la salida está al S) | Jugador ≥ 4 m dentro de los bounds, es decir, en z ≤ 16 (B0) |

**Comprobación: ninguna persiana corta el camino NavMesh entre spawns y jugador.**

- **Todos los slots están en la sala central**, dentro de los bounds (x −18…18, z −20…18). Las alas no tienen slots (R1).
- Las alas solo se pueden alcanzar desde la sala central (**petición 2 a R1**). Por tanto, cuando empieza la ronda 1 el jugador está en la sala, y la carga de cerrar las alas solo quita NavMesh donde no hay ni jugador ni slots.
- El vestíbulo S (fuera de los bounds, delante de la salida) no tiene persiana: sigue conectado. Si el jugador se refugia allí, los enemigos llegan.
- El pórtico N solo cierra con el jugador dentro: nunca lo deja fuera con la ronda en marcha. Si cerrase con el jugador en el vestíbulo N, ningún spawn tendría camino («OLEADA EN ESPERA») y el jugador quedaría encerrado entre la puerta de la esclusa (que no vuelve a ser trigger) y la persiana. **Sería un soft-lock**; por eso la condición de cierre.
- Lo que se comprueba en F2 (V12): `CalculatePath` desde los 18 slots hasta 4 puntos del jugador (los cuatro cuadrantes y el anillo) con las 5 persianas cerradas → `PathComplete`.

**Datos (extracto):**

```json
{ "id": "R07", "kind": "rounds", "archetype": "E8", "title": "COMUNICACIONES · ACCESO RESTRINGIDO",
  "rounds": { "arenaTitle": "COMUNICACIONES",
    "bounds": { "center": [0, 4.75, -1], "size": [36, 10.5, 38] }, "bounds2": null, "slots2": null,
    "waves": [
      { "groups": [ { "enemy": "T07", "count": 2 }, { "enemy": "CAR", "count": 2 } ], "variations": [] },
      { "groups": [ { "enemy": "HOV", "count": 2 }, { "enemy": "SAW", "count": 1 }, { "enemy": "CAR", "count": 2 } ], "variations": [] },
      { "groups": [ { "enemy": "T07", "count": 2 }, { "enemy": "SAW", "count": 1 }, { "enemy": "HOV", "count": 2 }, { "enemy": "CAR", "count": 2 } ],
        "variations": [
          { "name": "Francotiradores", "groups": [ { "enemy": "T07", "count": 2 }, { "enemy": "SAW", "count": 1 }, { "enemy": "HOV", "count": 2 }, { "enemy": "CAR", "count": 2 } ] },
          { "name": "Sierras", "groups": [ { "enemy": "T07", "count": 1 }, { "enemy": "SAW", "count": 2 }, { "enemy": "HOV", "count": 2 }, { "enemy": "CAR", "count": 2 } ] } ] },
      { "groups": [ { "enemy": "T07", "count": 3 }, { "enemy": "SAW", "count": 1 }, { "enemy": "CAR", "count": 6 } ], "variations": [] },
      { "groups": [ { "enemy": "SAW", "count": 2 }, { "enemy": "T07", "count": 2 }, { "enemy": "HOV", "count": 2 }, { "enemy": "CAR", "count": 1 } ], "variations": [] } ] },
  "shutters": [
    { "doorRef": "R07/[Doors]/P_WingW_S", "closeOnRound": 1, "openOnComplete": true },
    { "doorRef": "R07/[Doors]/P_WingW_N", "closeOnRound": 1, "openOnComplete": true },
    { "doorRef": "R07/[Doors]/P_WingE_S", "closeOnRound": 1, "openOnComplete": true },
    { "doorRef": "R07/[Doors]/P_WingE_N", "closeOnRound": 1, "openOnComplete": true },
    { "doorRef": "R07/[Doors]/P_Portal",  "closeOnRound": 1, "openOnComplete": false, "minInsideMeters": 4 } ],
  "health": [ { "id": "H1", "pos": [0, 1.2, 6], "heal": 25 }, { "id": "H2", "pos": [-12, 0, -2], "heal": 25 }, { "id": "H3", "pos": [12, 0, -2], "heal": 25 },
              { "id": "H4", "pos": [-22, 0, 0], "heal": 25, "note": "ala W, recompensa" }, { "id": "H5", "pos": [22, 0, 0], "heal": 25, "note": "ala E, recompensa" } ] }
```

Los IDs de las persianas son provisionales y los fija R4.

### B4. R10 · Persecución: emboscadas

Origen de R10: (237; −22; 113). Mundo: x = 237 + x local; z = 113 + z local; y = −22.

| Emboscada | `ActivationZ` (local → mundo) | `Prefabs[j]` | `Points[j]` local (x; y; z) | `Points[j]` mundo |
|---|---|---|---|---|
| A1 (tras G1) | −55 → **58** | CAR | (−4,5; 0; −26) | (232,5; −22; 87) |
| | | CAR | (4,5; 0; −20) | (241,5; −22; 93) |
| A2 (tras G2) | −10 → **103** | T07 | (4,5; 0; 24) | (241,5; −22; 137) |
| | | HOV | (−4,5; 0; 18) | (232,5; −22; 131) |
| A3 (tras G3) | 30 → **143** | T07 | (−4,5; 0; 64) | (232,5; −22; 177) |
| | | CAR | (4,5; 0; 58) | (241,5; −22; 171) |

- **Reglas cumplidas:**
  - Todos los puntos están ≥ 28 m por delante de su `ActivationZ` (≥ 6 m: `ChaseCorridorEncounter.cs:115`).
  - Sin BIG ni TUR.
  - ≤ 2 enemigos por emboscada; 6 en total.
- **Retraso previsto** [SUPUESTO]: A1 ≈ 1 s, A2 ≈ 1,5 s y A3 ≈ 1,5 s; las grietas de 4 m se saltan andando (alcance de 9 m). Unos 4 s, frente a los ≤ 5,5 s (50 % de la holgura de ≈ 11 s que calcula R1) ✓.
- Los dos T07 se pueden ejecutar, pero no se cuenta con ello: ejecutar detiene al jugador 0,73 s con la sierra detrás.
- **Campos** (R1; de mundo donde el componente los usa así):
  - `courseOffset` (237; −22; 0), `halfWidth` 8, `finishZ` 195.
  - Trigger: local z −75…−71 (mundo 38…42), 16 × 10 × 4.
  - Trituradora en local z −84,5 (mundo 28,5); `entranceDoor` en z −86 (mundo 27); `exitDoor` en z +86 (mundo 199).
  - `sawSpeed` 8, `warningSeconds` 2, `bladeRpm` 240, `sawDamage` 100, `damageInterval` 0,35, `sawContactOffset` 2,1 (`GiantShredder`, CHK:601).
- **Botiquines** (R-V4 [cambio B]): 1 en la bahía (0; 0; −93) y 2 sobre el eje de la ruta, en (0; 0; −15) y (0; 0; 35).
  - Se sustituyen los 3 de la bahía del croquis de R1: después de R09 (3 botiquines) el jugador llega con la vida llena y solo sirven los de la ruta (petición 3).
- **[R1, VALIDAR F2]** Los puntos en x ±4,5 no pueden caer en una pilastra adosada: las pilastras ocupan x ±7…8.

### B5. R18 · Jefe: encuentro provisional (F2) y de producción (F4)

**Slots de R1** (§9):

- Alcobas de refuerzo **AW, AE, ANW y ANE**, de 3 slots cada una, bajo la galería.
- **4 slots en la galería**: GW, GE, GN y GS.
- Coordenadas exactas: petición 1 a R1.
- El jugador entra por el S (puerta B, en z −24,5). Las alcobas W/E quedan a ≈ 36 m de la entrada y las NW/NE a ≈ 64 m.

**Reglas comunes de los refuerzos (F4):**

- `Map01BossEncounter` usa `EnemySpawnQueue.Spawn` con una validación propia equivalente a `SafeSpawn`: NavMesh, ≥ 6 m + radio del jugador y cápsula libre.
- Tope: jefe (3 u [SUPUESTO]) + refuerzos ≤ 5 u.
- **Relevo:** si no queda ningún ejecutable vivo durante 20 s, entra un T07 por la alcoba más lejana al jugador. Máximo 1 relevo por fase [SUPUESTO].

#### Concepto A · El Custodio

| | F2 provisional (solo sistemas existentes: `CombatTwoRoomsLab` en R18) | F4 (`Map01BossEncounter` + variante `Boss_Custodio`) |
|---|---|---|
| Jefe | Stand-in: `BigBot_Current` sin escalar, con `FixedPoint` en el centro (0; 0; 11,5) | `Boss_Custodio` en el centro; 3 fases (A7.1) |
| Fase / ronda 1 | BIG ×1 (centro) + T07 ×2: 4 / 3 · 2 | **F1 (100–67 %):** T07 ×2 en ANW y ANE |
| Fase / ronda 2 | BIG ×1 + SAW + T07 + CAR ×2: 5 / 5 · 2 | **F2 (67–34 %):** SAW ×1 en AW y CAR ×4 (2 en AE y 2 en ANW). Cae el cañón izquierdo del jefe |
| Fase / ronda 3 | BIG ×2 + T07 ×2 + SAW ×2: 8 / 6 · 4 | **F3 (34–0 %):** T07 ×2 en AW y AE + HOV ×1 en GN. Cae el cañón derecho |
| Armas | Solo las de los T07 y del SAW: el stand-in no suelta cañones (`executionOnly`) | Los 2 cañones de ráfaga del jefe (≈ 340 cada uno) y los T07 y el SAW de los refuerzos |
| Qué valida | Espacio, NavMesh del anillo, alcobas, rampas Jump y rendimiento con 2 BIG | Fases, uniones, recogida y fin de mapa |
| Necesita | 1 slot fijo en el centro + **4 slots grandes** en el suelo (petición 1) | La variante del jefe y el componente nuevo |

**Por qué un stand-in por rondas:** permite jugar R18 en F2 sin código nuevo. Muere el último enemigo → `Completed`; sin salida, el fin de mapa lo resuelve R0. `prologueTitle` = «BÓVEDA · PROVISIONAL».

#### Concepto B · La Matriarca

| | F2 provisional | F4 |
|---|---|---|
| Jefe | Stand-in: TUR ×2 en dos slots fijos de la galería N (+6, conectada por rampas), donde iría la máquina | Máquina estática empotrada en el N; sus 2 TUR del lomo las crea el encuentro en puntos fijos sobre la máquina (sin camino NavMesh, porque no pasan por `CombatTwoRoomsLab`) |
| Fase / ronda 1 | TUR ×2 + T07 + SAW + CAR ×2: 5 / 6 · 2 | **Producción:** cada 20 s, camada de CAR ×4 + SAW ×1 alternando ANW y ANE (las tolvas). Tope: 8 CAR y 2 SAW vivos |
| Fase / ronda 2 | SAW ×2 + T07 + CAR ×6: 6 / 9 · 3 | **Brazos:** camadas cada 25 s (CAR ×4 + SAW ×1); los brazos sueltan sierras |
| Fase / ronda 3 | T07 ×2 + SAW + HOV + CAR ×6: 7 / 10 · 3 | **Crisol:** camada de CAR ×2 + T07 ×1 (AW o AE) cada 20 s |
| Necesita | 2 slots fijos de TUR en la galería N | Puntos de las tolvas = ANW y ANE |

#### Concepto C · El Panóptico

| | F2 provisional | F4 |
|---|---|---|
| Jefe | Stand-in: TUR ×2 en dos puntos fijos del suelo a r 12 del centro, más una tercera en la galería (sin anillo a +12 en F2) | Columna estática con vainas desprendibles |
| Fase / ronda 1 | TUR ×2 + T07 ×2: 4 / 4 · 2 | **F1:** T07 ×2 en AW y AE + 2 TUR de suelo (las crea el encuentro) |
| Fase / ronda 2 | T07 ×2 + HOV ×2 + CAR ×2: 5 / 6 · 2 | **F2:** T07 ×1 en ANW + HOV ×2 en GW y GE |
| Fase / ronda 3 | TUR ×1 (galería, slot distinto) + T07M ×2 + T07: 4 / 4 · 3 | **F3:** T07 ×2 en AW y AE + T07M ×1 en ANE |
| Necesita | 3 slots fijos de TUR conectados al NavMesh | El anillo a +12 de §9 [DEPENDE DEL JEFE] y ascensores = alcobas |

**Botiquines de R18:** 4 en la antecámara (R1) y 4 en la galería, en (−32; 6; 11,5), (32; 6; 11,5), (0; 6; 43,5) y (−24; 6; −20).

### B6. Economía total del mapa

#### B6.1 Por cuarto

OVERDRIVE esperados = 50–80 % de las fuentes [SUPUESTO], porque no todo ejecutable se ejecuta.

| ID | Sesión (D-U4 c) | Enemigos | BIG | TUR | Fuentes | Fuentes por BIG (ronda) | OVERDRIVE esperados | Botiquines (PV) |
|---|---|---|---|---|---|---|---|---|
| R01 | S1 | 4 | — | — | 1 | — | 1 | 2 (50) |
| R02 | S1 | 17 | — | — | 7 | — | 4–6 | 2 (50) |
| R03 | S1 | 14 | — | — | 4 | — | 2–3 | 2 (50) |
| R04 | S1 | 12 | — | — | 4 | — | 2–3 | 2 (50) |
| R05 | S1 | — | — | — | — | — | — | 3 + 1 opc. (75–100) |
| R06 | S2 | 17 | — | 1 | 6 | — (TUR: 1 T07 ✓) | 3–5 | 3 (75) |
| R07 | S2 | 33 | — | — | 14 | — | 7–11 | 5 (125) |
| R08 | S2 | 17 | 2 | — | 7 | R1: 2 · R3: 3 ✓ | 4–6 | 4 (100) |
| R09 | S2 | — | — | — | — | — | — | 3 (75) |
| R10 | S2 | 6 | — | — | 2 (opcionales) | — | 0–1 | 3 (75) |
| R11 | S2 | 26 | — | — | 7 | — | 4–6 | 3 (75) |
| R12 | S2 | 23 | 1 | — | 8 | R2: 2 ✓ | 4–6 | 3 (75) |
| R13 | S2 | — | — | — | — | — | — | 3 (75) |
| R14 | S3 | 22 | 2 | — | 11 | R1: 3 · R3: 3 ✓ | 6–9 | 4 (100) |
| R15 | S3 | 26 | — | — | 13 | — | 7–10 | 4 (100) |
| R16 | S3 | 21 | 3 | 1 | 11 | R1: 3 (BIG + TUR) · R3: 4 para 2 BIG ✓ | 6–9 | 4 (100) |
| R17 | S3 | — | — | — | — | — | — | 3 (75) |
| R18 (A) | S3 | Jefe + 10 (≤ 13) | Jefe | — | 5 + 2 cañones del jefe | — | 4–6 | 8 (200) |

#### B6.2 Totales

| Sesión | Enemigos | Fuentes | OVERDRIVE esperados | Botiquines (PV) |
|---|---|---|---|---|
| S1 (R01–R05) | 47 | 16 | 9–13 | 11 (275) + 1 opc. |
| S2 (R06–R13) | 122 | 44 | 22–35 | 27 (675) |
| S3 (R14–R18, con A) | 69 + jefe + 10–13 | 35 + 7 | 23–34 | 23 (575) |
| **Mapa** | **238 + jefe + 10–13 refuerzos** | **95 + 7 en el jefe** | **≈ 54–82** | **61 (1525 PV) + 1 opcional** |

**Por tipo** (sin R18, con la variación A de cada ronda):

| CAR | T07 | HOV | SAW | T07M | BIG | TUR | Total |
|---|---|---|---|---|---|---|---|
| 100 | 59 | 33 | 28 | 8 | 8 | 2 | **238** |

- Máximo simultáneo del mapa: **8 unidades / 10 cuerpos** (R15 y R16). Ninguna ronda pasa de 10 cuerpos: el techo de 12 queda como margen.
- Todos los cuartos ≤ 36 cuerpos (V11). El mayor es R07, con 33.
- **[R0] Medir en Release:** R16 (8 u, 2 BIG), R11 ronda 2 (10 cuerpos de horda) y R07 ronda 4 (10 cuerpos), además de R18 (R1).

#### B6.3 Efecto de D-U4 (c) en el ajuste

- Cada sesión empieza con **100 PV, sin arma robada y con la estación de montaje**. Por eso la primera ronda de R06 y de R14 lleva fuentes (1 y 3). Los botiquines se reparten por sesión (B6.2).
- **Sí cambia el tercer tercio.** S3 = R14–R18: cada muerte contra el jefe repite R14–R17 (≈ 12,5 min según R1).
  - **Petición 8:** un cuarto corte en **C17** (es Z): S4 = R18 con su antecámara. Así cada intento contra el jefe empieza en la antecámara.
  - **Con ese corte se mantiene el ajuste de esta tabla.**
  - **Sin ese corte**, R3 rebaja:
    - R15 ronda 3 a 7 u / 8 c (CAR ×2 en vez de ×4);
    - R16 ronda 2 a 7 u / 8 c (CAR ×2);
    - R16 ronda 3 a 7 u / 5 c (T07 ×2: quedan 3 fuentes para 2 BIG; se aceptaría como excepción a R-E2).
- **S2 es larga** (≈ 22 min, 122 enemigos): morir en R12 repite R06–R11 (≈ 18 min).
  - **Opción** [DECISIÓN USUARIO, ampliación de D-U4]: corte también en **C10** (Z, tras la persecución) → S2a R06–R10 y S2b R11–R13.
  - Coste: una sesión y una estación más. Recomendación de R3: **sí**, si las sesiones son baratas de montar.

### B7. Conflictos y peticiones

**A R1:**

1. **R18:**
   - Fijar las coordenadas de las alcobas AW, AE, ANW y ANE (3 slots cada una) y de los slots de la galería GW, GE, GN y GS.
   - Para el stand-in de F2 del concepto A: un **slot fijo en el centro** (0; 0; 11,5) y **4 slots grandes** a r ≈ 22 del centro, fuera del delantal S: (−22; 0; 11,5), (22; 0; 11,5), (−15,5; 0; 27) y (15,5; 0; 27).
   - Si se eligen B o C: 2–3 slots fijos de TUR conectados al NavMesh (galería N para B; suelo a r 12 y galería para C).
2. **R07:** confirmar que las alas **solo** se alcanzan desde la sala central (sin acceso desde los vestíbulos N o S). Si no, el jugador podría estar en un ala al empezar la ronda 1 (B3).
3. **R10:** botiquines: 1 en la bahía y 2 en la ruta (B4), en vez de los 3 de la bahía. Verificar en F2 que los puntos de emboscada en x ±4,5 no coinciden con pilastras.
4. **R12:** E-1 ≥ 5 m entre los extremos de los puentes B1/B2 y cualquier obstáculo, porque el BIG cruza por ellos. No más de un BIG por ronda (B2.3).
5. **Plataformas excluidas de los bounds** (R02, R06, R08, R11 y R16): aceptadas con C1–C4 (B2.2). Ningún cambio de geometría.
6. **R06:** la lista tiene 12 slots para un máximo de 7 cuerpos. Correcto, pero sin margen para añadir cuerpos: cualquier cambio de R06 pasa por R3.
7. **R16 BS4** (bajo la galería W, 5,5 m libres): confirmar E-1 ≥ 5 m entre el slot y los pilares de la galería. La cápsula del BIG mide 4,58 m: cabe en altura.
8. **Sesiones:** corte en C17 (S4 = R18) y, opcional, en C10 (B6.3). También para R0.

**A R4:**

9. **`ArenaShutter`:**
   - Pórtico: baja solo con el jugador dentro de los bounds y a ≥ 4 m del plano; nunca con el jugador fuera.
   - Alas de R07: bajan con `WaveIndex == 0` si el barrido está libre y el jugador está dentro de los bounds; no suben entre rondas; suben con `Completed`.
   - Jaula de R15: sube con `WaveIndex == 2`.
   - Cerrada, con `NavMeshObstacle` y carving. Colisión desde el primer frame; abierta, inactiva.
10. Persianas con el mismo contrato de «nunca atrapar» que las puertas (MD §3.5): si el barrido está ocupado, esperar y reintentar cada 0,25 s.

**A R0:**

11. **Componentes nuevos de esta parte:**
    - `Map01StageRelease` (R05, R09, R13 y R17).
    - `Map01SessionHandoff` (R05 y R13; también R17 si se aprueba el corte de C17).
    - `ArenaShutter` (de R4).
    - `Map01BossEncounter` (F4).
    - Opcional: un observador de `WaveIndex` para el apagón de la ronda 5 de R07 [R5].
12. **R18 en F2 con stand-in por rondas** (B5): no requiere código nuevo. El fin de mapa sigue pendiente de R0.
13. Precalentar `FX_BigBot_Blast_v4` en **R08 y R14**: el primer BIG de cada sesión (CHK:1091, 1094).
14. **Verificar en el Editor**, antes de cerrar la economía:
    - Si el CAR es ejecutable (D-E3).
    - Detección, ataque y daño del T07M.
    - Si la TUR es fija.
    - Si los restos de la TUR conservan collider (A5.3).
15. **Decisiones que afectan a esta parte:**
    - **D-E1 (c)** cambia R08 ronda 2, R12 ronda 1 y R15 ronda 1: los T07M pasan a ser «desarmables a distancia».
    - **D-E3:** si el CAR resultara ejecutable, R11 y R07 tendrían decenas de fuentes; habría que reajustar.
    - **D-E4** (jefe) fija B5.
