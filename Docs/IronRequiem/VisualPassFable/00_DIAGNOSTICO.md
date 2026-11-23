# Pase visual (chat 12) · F0 Diagnóstico

Fecha: 21/11/2026. Solo lectura más Play Mode sin guardar. No se cambió ninguna escena, material, luz, ajuste ni paquete. Sin commits.

**Lo que hay que saber antes de leer el resto:**

1. **Map01 no tiene decoración.** Los 18 cuartos son greybox: el nodo `[Decor]` de cada escena está vacío y ninguno de los 22 props de `02_DECORACION.md` existe todavía. La queja "la decoración no transmite un búnker tomado por robots" es literal: no hay nada que rediseñar, hay que hacerla.
2. **Los documentos de Map01 contradicen el contexto del mundo.** Map01 **baja** de 0 a −37 m (Acceso → … → Bóveda) y el contexto dice que **subimos** desde laboratorios profundos hasta la salida. Detalle y preguntas en §6. No he cambiado nada.
3. **El juego está muy oscuro en todas partes**, no solo en las zonas de oscuridad intencional: el fondo medio de la imagen es de 3 a 19 sobre 255 en las 25 salas (§2).
4. **La linterna ya se decidió en el chat 11 el 20/11** (opción C, lectura pasiva, implementada). El contorno blanco que añade es hoy lo más raro de las luces de los robots (§3). Hay que decidir si se mantiene, se ajusta o se sustituye (§7).
5. **Hallazgo aparte, sin tocar:** los enemigos se autodestruyen por debajo de y = −20 m y Map01 baja hasta −37 m (§8).

## 1. Cómo se midió

- Arnés nuevo de solo lectura: `Assets/IronRequiem/Tests/PlayMode/VisualPassF0.cs`. Carga la sesión real `Map01_Campaign_S1` (jugador, gestores y `Campaign_PostProfile`), detiene el flujo, descarga el hangar y carga cada sala de forma aditiva con el tiempo congelado. Así los enemigos se ven como en el juego, con sus luces de estado y con el resaltado del chat 11.
- **Puntos de vista fijos y reproducibles:** 2 por sala, sobre el NavMesh, a un tercio de cada extremo del eje largo, a 1,65 m de altura y mirando al centro de juego. Las coordenadas están en `F0/rooms.csv` (columnas `vp1`, `vp2`).
- **Capturas a 1920×1080** con la cámara de la sonda (misma métrica que `VisionF0Probe` del chat 11, que la validó contra la cámara del jugador): vacío y con un T-07 a 5, 15 y 25 m. 200 capturas en `Tools/IronRequiem/Reports/VisualPassFable/F0/zones/`; una selección está en [F0/zonas/](F0/zonas/).
- **Escala:** valor de pantalla 0–255 (luma Rec.709), la misma de `05_ILUMINACION.md` §L1.3. "Enemigo" es la media dentro de su silueta; "fondo" es el anillo que lo rodea; el contraste es (mayor + 5) / (menor + 5).
- **Límites de la medida:**
  - Editor, no build. Las cifras de SetPass y triángulos incluyen al jugador y su arma, así que no sirven como presupuesto de decoración (y además no hay decoración que medir).
  - Las salas se miden sin la esclusa ni las puertas de sesión.
  - Los casos marcados "tapado" son puntos donde un bloque oculta al enemigo: no cuentan como ilegibles.

Skills usadas: `unity:urp-postprocessing` (pila de Volume, exposición y bloom), `iron-requiem-performance` (medir en condiciones controladas; distinguir Editor de build) e `iron-requiem-enemy-ai` (no tocar la IA, solo leer su estado). Los inventarios de solo lectura (Haiku) están en `Tools/IronRequiem/Reports/VisualPassFable/inventory/`.

## 2. Luminosidad por zona

Objetivo que ya existe en `05_ILUMINACION.md` §L1.3: **≥ 45** en carriles y spawns. Medido: **3 a 19**.

| Sala | Luces | Fondo de la imagen, vacío (vista 1 / vista 2) | Píxeles < 10/255 | T-07 a 5 m (enemigo : fondo) | a 15 m | a 25 m | Notas |
|---|---|---|---|---|---|---|---|
| R01 ControlAcceso | 6 | 12,46 / 11,54 | 55,94 % | 35,98 : 13,95 | 34,8 : 15,04 | 28,24 : 10,82 |  |
| R02 PozoMontacargas | 7 | 13,67 / 9,55 | 38,8 % | 33,9 : 11,91 | 33,69 : 6,31 | tapado | algún punto tapado |
| R03 Dormitorios | 8 | 10 / 9,74 | 59,88 % | 35,59 : 15,4 | 34,08 : 15,55 | 29,71 : 14,94 |  |
| R04 Comedor | 7 | 14,09 / 16,51 | 52,21 % | 37,18 : 15,62 | 33,89 : 15,45 | 24,63 : 11,03 |  |
| R05 Enfermeria | 6 | 14,73 / 13,88 | 57,87 % | 37,69 : 17,65 | 42,11 : 12,51 | 32,18 : 17,75 |  |
| R06 Armeria | 8 | 9,41 / 11,23 | 62,7 % | 34,44 : 7,75 | 24,25 : 2,92 | tapado | algún punto tapado |
| R07 Comunicaciones | 10 | 12 / 11,31 | 59,05 % | 34,91 : 14,73 | 33,61 : 9,13 | 21,46 : 9,08 |  |
| R08 Almacenes | 7 | 6,28 / 19 | 75,3 % | 33,84 : 5,87 | tapado | tapado | suelo por debajo de −20 m; algún punto tapado |
| R09 Taquillas | 5 | 11,68 / 14,95 | 58,43 % | 34,25 : 14,51 | 32,84 : 19,67 | 36,26 : 18,92 | suelo por debajo de −20 m |
| R10 TunelTrituradora | 8 | 4,63 / 7,53 | 86,41 % | 33,32 : 7,62 | 32,91 : 10,01 | 27,87 : 9,85 | suelo por debajo de −20 m |
| R11 Decantadores | 8 | 8,04 / 10,24 | 74,38 % | 34,18 : 15,28 | 32,94 : 13,6 | 27,23 : 11,45 | suelo por debajo de −20 m |
| R12 Colector | 8 | 11,04 / 9,88 | 67,77 % | 34,61 : 19,64 | 34,12 : 24,41 | 33,71 : 40,79 | suelo por debajo de −20 m |
| R13 SalaBombas | 7 | 14,98 / 17,23 | 49,76 % | 36,19 : 28,54 | 32,5 : 15,11 | 29,99 : 13,27 | suelo por debajo de −20 m |
| R14 Fundicion | 8 | 8,99 / 10,14 | 69,39 % | 33,61 : 10,36 | 32,87 : 10 | tapado | suelo por debajo de −20 m; algún punto tapado |
| R15 Ensamblaje | 8 | 15,73 / 10,88 | 49,83 % | 35,9 : 18,68 | 36,77 : 15,86 | 21,69 : 8,26 | suelo por debajo de −20 m |
| R16 Reactor | 8 | 13,41 / 14,27 | 59,9 % | 34,13 : 14,99 | tapado | tapado | suelo por debajo de −20 m; algún punto tapado |
| R17 CentroMando | 8 | 5,94 / 6,72 | 87,2 % | 33,39 : 3,83 | 33,2 : 4,54 | 27,87 : 4,01 | suelo por debajo de −20 m |
| R18 Boveda | 9 | 2,99 / 2,85 | 97,91 % | 33,87 : 2,49 | 32,99 : 2,56 | 29,6 : 2,42 | suelo por debajo de −20 m; algún punto tapado |
| Room00 Hangar | 35 | 9,52 / 16,21 | 75,31 % | 29,47 : 12,62 | 28,14 : 16,17 | 26,49 : 19,1 |  |
| Room00b AlarmCorridor | 17 | 15,3 / 22,71 | 58,13 % | 44 : 20 | tapado | tapado | algún punto tapado |
| Room01 Foundry | 4 | 4,35 / 4,29 | 91,21 % | 34,07 : 13,71 | 32,62 : 12,53 | 26,27 : 14,7 |  |
| Room02 Breach | 4 | 4,11 / 4,1 | 90,98 % | 34,48 : 4,08 | 33,42 : 8,39 | 26,49 : 7,17 |  |
| Room03 Assembly | 4 | 4,1 / 4,05 | 87,71 % | 34,68 : 3,17 | 32,82 : 1,36 | 27,14 : 1,34 |  |
| Room04 Reactor | 4 | 12,54 / 11,92 | 81,97 % | 34,75 : 3,85 | tapado | tapado | algún punto tapado |
| Pursuit05 | 6 | 0,61 / 0,32 | 99,95 % | 31,88 : 2,06 | 29,01 : 1,6 | 22,47 : 1,7 |  |

Lectura:
- **Entre el 50 % y el 98 % de los píxeles de cada captura está por debajo de 10/255.** Las luminarias se ven encendidas pero no iluminan el suelo ni las paredes.
- El T-07 marca siempre ≈ 34 a 5 y 15 m, sea cual sea la sala: ese valor lo pone el contorno del resaltado del chat 11, no la luz de la sala. Por eso casi todo sale "legible" aunque la sala esté negra.
- Las peores: R18 Bóveda (3), R10 Túnel (5–8), R17 Centro de mando (6) y toda la campaña antigua (Room01–03 ≈ 4; Pursuit05 < 1).
- No existe ningún Volume de zona en Map01: solo actúa `Campaign_PostProfile`, que no tiene exposición. Los valores de ambiente y niebla aplicados coinciden con `05_ILUMINACION` (están en `Levels/Map01/Data/Lighting/R*.json`).

| Control de acceso, vacío | Control de acceso, T-07 a 15 m |
|---|---|
| ![](F0/zonas/Map01_R01_ControlAcceso_vp1_vacio.jpg) | ![](F0/zonas/Map01_R01_ControlAcceso_vp1_T07_15m.jpg) |
| **Decantadores, T-07 a 15 m** | **Bóveda, T-07 a 15 m** |
| ![](F0/zonas/Map01_R11_Decantadores_vp1_T07_15m.jpg) | ![](F0/zonas/Map01_R18_Boveda_vp1_T07_15m.jpg) |
| **Fundición, T-07 a 15 m** | **Campaña antigua, Foundry, T-07 a 15 m** |
| ![](F0/zonas/Map01_R14_Fundicion_vp1_T07_15m.jpg) | ![](F0/zonas/Room01_Foundry_vp1_T07_15m.jpg) |

## 3. Catálogo de luces de robots

Escenario: `Map01_R01_ControlAcceso`, punto de vista 1, cada robot a 5 m (plano de 3/4, frente y espalda) y a 15 m, en cada estado de `EnemyStateLights`. 61 capturas en `Reports/VisualPassFable/F0/robots/` y volcado completo en [F0/robots.txt](F0/robots.txt).

| Robot | Qué es su "luz" | Material y color | Tamaño | Estados (patrulla / alerta / ataque / aviso) | Halo o bloom | ¿Flota? | Parpadeo |
|---|---|---|---|---|---|---|---|
| **T-07** | Esfera `Head/Sphere` | `RobotEye_Red`, URP Unlit, rojo (0,85; 0,01; 0), sin HDR | **5 cm** | Los cuatro escriben el mismo rojo (0,8; 0,01; 0): **no cambia nada entre estados** | Ninguno (no supera el umbral de bloom) | No | No |
| **T-07 Mech** | La misma esfera | Igual | 5 cm | Igual: sin cambio | Ninguno | No | No |
| **SawBot** | `CoreIndicator` | `RobotEye_Red` | **3 cm** | Sin cambio | Ninguno | No | No |
| **Carroñero** | `CoreIndicator` | `RobotEye_Red`; sin `EnemyStateLights` | 3 cm | No tiene estados | Ninguno | **Sí: el punto rojo queda ≈ 0,75 m por encima del cuerpo, separado de la malla** | No |
| **BigBot** | 6 `PowerIndicator` en el torso | `SharedIndicatorLens`, URP Lit con **`_EMISSION` apagado** | 12–16 cm | El script escribe cian / ámbar / rojo en `_EmissionColor`, pero con la emisión apagada **solo cambia el color base, que depende de la luz de la sala** | Ninguno | No | El aviso parpadea a 14 Hz |
| **HoverBot** | `EyeBall` (3 materiales) | `SharedRobotEyes` Unlit cian (0,15; 2,3; 2,8) + `HoverEye` emisivo | **34 cm** | Cian → ámbar → rojo → blanco: **el único robot en el que el estado se ve** | Leve | No | Aviso a 14 Hz entre rojo y blanco |
| **Torreta** | Iris `TurretTop` (ranura 2) | `SharedRobotEyeSlots`, emisivo cian (0,15; 2,3; 2,8); sin `EnemyStateLights` | Iris de ≈ 0,6 m | No cambia | Sí, halo cian (único con bloom medible) | No | No |

Además, sobre **todos** ellos actúa desde el 20/11 el resaltado `EnemyDarkReadability` del chat 11: un contorno de Fresnel blanco azulado sobre el cuerpo.

**Qué puede ser "lo raro" (por orden de peso):**

1. **El contorno blanco del resaltado.** En salas oscuras el robot se ve como una silueta negra con filo blanco, casi un negativo. El pico de brillo del robot pasa a 210/255 y en el Carroñero, el SawBot y el BigBot el brillo se sale de la silueta. Se nota más cuanto más oscura está la sala, es decir, hoy siempre.
2. **Los ojos son diminutos y no brillan.** Un punto de 3–5 cm sin HDR no se distingue a 15 m. Los ojos ya son rojos en el T-07, el SawBot y el Carroñero, pero no se ven: por eso se pide "luz roja en los ojos" aunque el color ya esté.
3. **El lenguaje de estado es incoherente.** Solo el HoverBot cambia de color. El T-07 y el SawBot tienen el componente pero con los cuatro estados en el mismo rojo. El BigBot escribe los colores en una emisión apagada. La torreta y el Carroñero no tienen estados.
4. **El punto rojo del Carroñero flota** sobre la antena, fuera de la malla.
5. **Mezcla de colores entre robots:** cian en HoverBot, BigBot y torreta; rojo en T-07, SawBot y Carroñero. No hay una regla.
6. **El aviso a 14 Hz** del HoverBot y el BigBot es un parpadeo muy rápido entre rojo y blanco.
7. **La barra de vida** en el mundo es un rectángulo rojo plano sobre el robot. En mis capturas sale torcida porque mira a la cámara del jugador, no a la de la sonda: eso es artefacto de la captura, no del juego; pero en juego sigue siendo un rojo plano grande cerca de los ojos.
8. En el BigBot, el rojo ya domina su armadura: un ojo rojo ahí no destaca.

| T-07, 5 m | Carroñero, 5 m (contorno blanco y punto rojo flotando) |
|---|---|
| ![](F0/robots/T07_Current_Repaired_spawn_frente.jpg) | ![](F0/robots/Carronero_spawn_frente.jpg) |
| **BigBot, 5 m** | **Torreta, 5 m** |
| ![](F0/robots/BigBot_Current_spawn_frente.jpg) | ![](F0/robots/Turret_Current_spawn_frente.jpg) |
| **HoverBot en patrulla** | **HoverBot en ataque** |
| ![](F0/robots/HoverBot_Current_patrulla_frente.jpg) | ![](F0/robots/HoverBot_Current_ataque_frente.jpg) |
| **T-07 a 15 m** | **Carroñero a 15 m** |
| ![](F0/robots/T07_Current_Repaired_spawn_15m.jpg) | ![](F0/robots/Carronero_spawn_15m.jpg) |

Historial que no hay que reintroducir (CHECKLIST, 08/11): resplandor gigante por NaN y HDR sin tope en el bloom, escalas heredadas del rig en el destello del T-07 y chispas de tamaño 0,5 delante de la cámara. Se arregló con StopNaN, bloom con clamp 12 y ojos Unlit sin HDR; de ahí que hoy los ojos no brillen.

## 4. Carroñero: secuencias de frames

Carroñero de combate real persiguiendo al jugador invencible en `Map01_R01`, filmado de lado con una luz neutra añadida solo para leer la animación. Paso fijo de 1/60 s. Tiras en [F0/carronero/](F0/carronero/) y datos en [F0/carronero.txt](F0/carronero.txt).

| Tira | Qué muestra | Defecto observado |
|---|---|---|
| [01_correr](F0/carronero/01_correr.jpg) | Persecución a 5,6 m/s (velocidad del agente 7,2) | **Patina siempre:** el pie de apoyo se mueve a 6,2–6,6 m/s (dos pasadas), más que el cuerpo. El ciclo de piernas va a unos 12 pasos por segundo: se ve como un temblor, no como un paso. El torso mantiene una pose fija. |
| [02_atacar](F0/carronero/02_atacar.jpg) | Vista lateral, desde 4,5 m hasta estar serrando (la figura de la derecha es el jugador) | `IsAttacking` llega a los 0,5 s de la tira y el brazo sube en ≈ 0,17 s (4–5 celdas). **No hay anticipación que se lea**: llega corriendo y ya está serrando. Durante el ataque las celdas son casi iguales (solo vibra la sierra). Sin recuperación. |
| [03_dano](F0/carronero/03_dano.jpg) | `TakeDamage(4)` en la celda 4 | **Ninguna reacción** en la pose. |
| [04_girar](F0/carronero/04_girar.jpg) | El jugador aparece 7 m detrás | Gira 180° en 0,8 s como un bloque rígido: sin inclinación ni pasos de giro. |
| [05_morir](F0/carronero/05_morir.jpg) | `Kill()` en la celda 5 | El cuerpo desaparece en ≈ 0,15 s en un estallido de **barras blancas grandes** (chispas) y trozos oscuros. No hay pose de muerte. |
| [06_andar](F0/carronero/06_andar.jpg) | Intento de andar a 1,8 m/s | **No existe andar:** la IA vuelve a fijar 7,2 m/s en cada frame. Solo corre o está parado. |
| [07_reposo](F0/carronero/07_reposo.jpg) | Parado, IA apagada | Solo un balanceo mínimo de cabeza; se lee como estatua. |
| [08_hurgar](F0/carronero/08_hurgar.jpg) | Pose de hurgar (lo que usa el hangar) | Funciona. Es **zona protegida**: no debe cambiar. |

![](F0/carronero/01_correr.jpg)
![](F0/carronero/02_atacar.jpg)
![](F0/carronero/05_morir.jpg)

Del código (`CarroneroMotion.cs`, inventario D): mueve 10 de los 22 huesos; el paso avanza con la distancia real pero con zancada fija de 0,575 m y sin plantar el pie; todos los individuos comparten fase de paso; al volver a entrar en cámara la pose salta sin mezcla. El mismo `Carronero.prefab` es el de los scavengers del hangar; para combate existe `Prefabs/Map01/Enemies/Carronero_Map01.prefab`.

## 5. Decoración por zona

- **Map01 (18 cuartos):** greybox. Solo hay estructura (muros, plataformas, coberturas, que son bloques con colisión), luminarias y cruces de botiquín. Todos los cuartos comparten los mismos dos materiales de pared y suelo, así que **se repite todo y está todo vacío**. Nada tapa el combate porque no hay nada.
- **Kits que sí existen:** 10 materiales `Bunker_*`, `VO_PipeRun2m`, `VO_CableBundle4m`, `VO_WallVent` y decales `VO_Decal_*`. Los `Scrap_*` existen pero `02_DECORACION` los descarta y el hangar ya los usa (zona protegida).
- **Presupuesto de papel** (`02_DECORACION` §D6): ≤ 60 draw calls, ≤ 10 SetPass y ≤ 80 k triángulos por arena. No hay nada medido porque no hay nada construido.
- **Hangar (Room00):** su tramo jugable ya tiene la chatarrería `HG_ScrapRoute` (+515 k triángulos sin LOD según CHECKLIST; mi segunda vista del hangar midió 4,6 M de triángulos en pantalla). Es coste, no decoración que falte.
- **Campaña antigua (Room01–04, Pursuit05):** tampoco tiene kit Bunker ni Scrap; son las salas V35 con chevrones y marcos. Ya no es la campaña a la que lleva JUGAR.

Detalle en `Reports/VisualPassFable/inventory/C_decoracion.md`.

## 6. Contradicciones con el contexto del mundo (para que decidas)

No he cambiado ningún documento. Cada punto necesita tu decisión antes de F4.

| # | El contexto dice | Los documentos dicen | Dónde |
|---|---|---|---|
| C1 | El mapa es una **subida** desde las profundidades | "El búnker **baja** de 0 a −37 m en siete niveles"; el hangar donde despierta el jugador está en superficie | `MAP01_DESIGN.md:20`, `01_ESTRUCTURA.md:203` |
| C2 | Abajo laboratorios → en medio infraestructura → arriba exterior | Acceso → Habitáculo → Armería → Comunicaciones → Almacenes → Túneles → Agua → Fundición → Ensamblaje → Reactor → Mando → Bóveda | `MAP01_DESIGN.md:21` |
| C3 | Abajo hay laboratorios y zonas de experimentación | No hay ninguna zona de laboratorio | `MAP01_DESIGN.md` §1 |
| C4 | Nunca un refugio humano habitado | Zona "Habitáculo": Dormitorios, Comedor y Enfermería, con camas, mural de la unidad y barricada | `MAP01_DESIGN.md:47-49`, `02_DECORACION.md` |
| C5 | La ruta termina en la **salida** a la superficie | Termina en la Bóveda con el jefe, marcada "fin de mapa"; no hay zona de superficie | `01_ESTRUCTURA.md` |
| C6 | Más luz real al subir | La luz de `05_ILUMINACION` no sigue una progresión de altura | `05_ILUMINACION.md` §L2 |
| C7 | Rojo en los ojos de los robots | Rojo, ámbar y verde reservados a puertas; el ataque ya usa rojo y la alerta ámbar | `04_PUERTAS.md` §P2.1, `EnemyStateLights.cs:20` |

Las opciones para C1 (invertir cotas, re-tematizar o mantener el descenso) están desarrolladas en `01d_DECORACION.md`.

## 7. Solape con el chat 11 (linterna)

- El prompt 12 dice que sustituye al 11 y que no se corra si no se había corrido. **Sí se corrió:** el 20/11 elegiste la opción C (lectura pasiva) y quedó implementada (`Scripts/Player/Vision/`, 4 materiales `_Vision`, dos ajustes en el menú).
- El chat 11 midió también la linterna con `Light`: 8 de 18 puntos legibles, frente a 17 de 18 de C. Y anotó tres límites: nueve cuartos ya tienen 8 luces de 8, URP limita a 4 luces por objeto y las Light Layers están apagadas (la linterna iluminaría también las manos y el arma; activarlas es tocar el pipeline).
- El chat 11 tenía prohibido subir el ambiente; este pase lo pide. Con más luz en la sala, el resaltado C pierde sentido en casi todo el mapa y su contorno blanco deja de hacer falta.

## 8. Hallazgo fuera de alcance: autodestrucción por debajo de −20 m

`EnemyController.EnsureIsWithinLevelBounds` (`Assets/FPS/Scripts/AI/EnemyController.cs:277`) destruye a cualquier enemigo cuya altura sea menor que `SelfDestructYHeight`, que vale **−20** en la clase y en todos los prefabs, y que nadie cambia en Map01.

- Suelos medidos: R08 y R09 a −21,95; R10 a −21,98; R11–R15 a −27,95; R16 a −33,95; R17 a ≈ −36; R18 a −36,95.
- En mi arnés, un T-07 recién creado en esas salas desaparecía en su primer frame. Para poder fotografiar tuve que subir ese límite solo en ejecución.
- **No he comprobado qué pasa en una partida real** (cómo cuentan las rondas a un enemigo que se destruye sin morir). Si se confirma, de R08 en adelante los enemigos no llegan a existir. Es del chat 9; lo dejo anotado, sin tocar.

## 9. Otros pendientes anotados

- R07 tiene 10 luces y R18 tiene 9, por encima del tope de 8. El validador de F2 da 10 avisos de más de 4 luces por objeto.
- El contador "Draw Calls Count" del Editor devuelve 0 (igual que "Batches Count" en `MapFxAudit`): para F4 habrá que medir con el Frame Debugger o en build.
- La explosión de muerte del Carroñero son barras blancas grandes; pertenece a efectos, no a animación.
- El chat 7 dejó sin diagnosticar dos tests de `BigBotDestructionTests` tras el cambio E7 (ver CHECKLIST).
