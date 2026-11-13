# Brazo, ejecuciones, recarga de escopeta y búnker abandonado (06/11/2026)

Laboratorio nuevo: `Assets/IronRequiem/Scenes/BunkerLab.unity` (copia de SecondaryShotgunLab) y `BunkerBenchmark.unity` para la medición. MainScene, laboratorios anteriores, modelos y prefabs originales sin tocar. Respaldo: `Tools/IronRequiem/Backups/20261106-bunker-polish`. Evidencias (`before-*` / `after-*`): `Tools/IronRequiem/Reports/BunkerPolish`.

## 1. Brazo cortado

Causa: el antebrazo FPS es una malla corta (solo antebrazo y mano, sin brazo). En ejecuciones y algunos golpes, su extremo del codo cae dentro del encuadre y se ve el corte abierto (`before-exec-T07_Hi-018.png`).

Corrección: `ProtagonistHandsPresentation` añade una manga opcional (`armSleeves`) desde el codo de cada antebrazo, con 6 cm de solape, hasta un punto de hombro fuera de cámara. Sigue al rig ya ajustado en cada frame: no cambia huesos ni movimientos. Material de traje oscuro, radio 3,8 cm, sin sombras, activa solo en BunkerLab.

Medido, en frames con el codo visible y sin cubrir:

| Acción | Antes | Después |
|---|---|---|
| Ejecución del T-07 | 43 / 70 | 0 / 70 |
| Ejecución del Big Bot | 47 / 70 | 0 / 70 |
| Golpe con F | 0 / 64 | 0 / 64 |

Probado con la principal y la escopeta. Capturas en `after-exec-*`, `after-punch-*`.

## 2. Umbral de ejecución: menos del 75 % para todos

`EnemyExecutionSource.HealthThreshold = 0.75` es ahora una regla fija. El valor antiguo por prefab solo puede hacerla más estricta: probado con 0,95 en un enemigo al 80 %, que sigue sin poder ejecutarse.

Resultados en T-07 y Big Bot:
- **No ejecutables:** 90 %, 76 % y 75 % exacto.
- **Ejecutables:** 74,99 %, 51 %, 50 % y 49,9 %.

Las barras de vida coinciden con Health, con un error de 0,002 o menos. Los drones y la torreta no tienen arma robable, así que no pueden ejecutarse.

Que el Big Bot se pueda ejecutar con más de la mitad de la vida (entre 50 y 75 %) es la regla pedida, no un fallo.

## 3. Ejecución del T-07

Antes, el robot moría al pulsar E y los dos golpes caían en el vacío. Ahora:
- El arma se entrega al pulsar E y el robot queda quieto: IA y navegación detenidas.
- **Primer golpe** (arrancar el arma, 0,35 s): impacto, chispas y reacción.
- **Segundo golpe** (puñetazo, 0,733 s): impacto, empuje y muerte, en el mismo frame que el impacto (0,732 s).

Comprobado: 2 impactos, 1 muerte y 1 sola entrega del arma. El cambio está en `StolenWeaponCarrier`, que comparten todas las escenas.

## 4. Escopeta

En `Weapon_Shotgun_Secondary`:
- Sin regeneración: con 1 disparo y 3 s de espera sigue en 3 cargas.
- Al gastar la cuarta carga, las 4 celdas se hunden en el cuerpo, ventilan con un silbido (`Weapon_Cooling.wav` del proyecto) y vapor (`VFX_AngrySteam`), y vuelven a asentarse.
- **Duración de la recarga: 1,25 s** (0,15 s de espera + 1,1 s de recorrido; medido 1,31 s a frame variable).
- No dispara mientras recarga.
- Si cambias de arma a mitad de recarga, se pausa y continúa al volver a equiparla.
- El fogonazo es 1,8 veces mayor (variante `MuzzleFlash_Shotgun_Secondary`).
- Daño, caída de daño y alcance sin cambios.

Componente nuevo: `CylinderBankReload`.

## 5. Búnker

- **Texturas:** cinco procedurales de 512 px con normal map (hormigón de suelo con juntas, paneles de pared con chorreones, metal oxidado, franjas de peligro y rejilla) en `Assets/IronRequiem/Art/Environment/Bunker`.
- **Materiales:** 9 compartidos (URP/Lit). Las UV están a escala real, así que no hace falta un material por objeto.
- **Teselas:** la geometría se divide en piezas de 10 m o menos (210 teselas), para que cada una reciba sus luces dentro del límite de 4 luces por objeto del renderer Forward. Los colliders originales no cambian.
- **Cierre:** techo a 7,25 m, sin collider y sin sombras, sin cielo visible; el fondo de cámara es color de niebla.
- **Detalles:** rejilla en el pasillo y franjas de peligro en el suelo, la puerta, las coberturas bajas y las bases de las columnas.
- **Luz:** 15 lámparas frías de techo con emisión (2 con sombra), 4 rojas y 4 ámbar de emergencia. Sol tenue de relleno (0,55), ambiente 0,36 y niebla exp² 0,008.
- **Sin luz horneada:** los enemigos son dinámicos y necesitan luz directa en tiempo real. Hornear habría requerido sondas de luz y no se hizo.
- **NavMesh:** rehorneado en un asset nuevo (`BunkerNavMesh.asset`) con los mismos ajustes; el del laboratorio original queda intacto. Probada una ruta completa entre el cuarto 1 y el 2 con la puerta abierta.

## Pruebas

`BunkerPolishTests` en Play Mode, en BunkerLab: 6/6 (brazo, barras, umbral, ejecución del T-07, recarga y búnker). Se usan dispositivos virtuales, sin dar foco a ninguna ventana.

## 6. Rendimiento (ejecutable Release, 1080p exclusivo, 5 min, misma carga)

| | Laboratorio anterior | Búnker |
|---|---|---|
| Frame medio / mediana | 6,97 / 6,89 ms | 7,09 / 6,99 ms |
| p95 / p99 | 10,43 / 11,67 ms | 9,90 / 12,90 ms |
| Frames > 8,33 ms | 26,4 % | 14,4 % |
| Frames > 16,67 ms | 0,05 % | 0,10 % |
| Frames > 33 / > 50 ms | 15 / 3 | 15 / 15 |
| CPU / GPU media | 6,97 / 3,11 ms | 7,09 / 3,07 ms |
| Hilo de render | 1,50 ms | 1,88 ms |

- La media sube muy poco (0,12 ms).
- La GPU no cambia.
- La proporción de frames por encima de 8,33 ms baja a casi la mitad.
- El hilo de render sube 0,38 ms por las teselas y las luces.

Los 15 frames de más de 50 ms (52–98 ms, uno por oleada) caen todos en el frame en que aparecen los 12 enemigos (t ≈ 0,07 s de cada oleada). Antes ese mismo frame costaba entre 33 y 50 ms. Lo más probable es que el coste extra venga de crear 12 enemigos de golpe con luces y 2 sombras puntuales activas, pero no está perfilado.

## 7. Corrección del pico de aparición (06/11/2026)

**Diagnóstico** (cronómetro en el ejecutable y perfil del frame de aparición). Todo el coste está en el hilo principal, en un solo frame:
- Deserializar los 12 enemigos: 15–40 ms.
- Activarlos (Awake/OnEnable): 15–40 ms. Destacan `RigBuilder.OnEnable` y `EnemyController.Start`.
- Hay recolecciones de GC en ese mismo frame.
- Los 8 T-07 suman entre el 75 y el 85 % del coste; cada Big Bot cuesta 1–1,5 ms.

**Corrección.** Nuevo `EnemySpawnQueue` (`Scripts/Levels`):
- **Precarga:** crea las instancias inactivas (sin Awake) bajo un contenedor desactivado, 1 por frame, en tiempo de calma: el menú, o el final de una oleada en el benchmark.
- **Aparición:** activa 1 enemigo por frame. Una oleada de 12 tarda unos 0,1 s en entrar.
- **Sin precarga disponible:** si no queda instancia precargada, la crea en un frame y la activa en el siguiente.
- **Sin reciclaje:** los enemigos siguen destruyéndose al morir, como antes.

**Integración:**
- `CombatTwoRoomsLab` tiene un campo opcional `spawnQueue`. Vacío, se comporta como antes.
- La oleada no cuenta como despejada mientras queden apariciones en cola.
- La cola está activada solo en BunkerLab y BunkerBenchmark (op `bunker-spawn-queue`). MainScene, ProjectSettings y manifest no cambian (hash comprobado).

**Test nuevo** `WaveEnemiesArriveOnePerFrameFromPrewarm`: la oleada 1 real llega de la precarga a 1 enemigo por frame, todos en NavMesh, y el índice de oleada no avanza. BunkerPolishTests 8/8 (más el diagnóstico de disparos) y SecondaryShotgunTests 7/7.

**Medición A/B** (ejecutable Release, 1080p exclusivo, 15 oleadas, pasadas seguidas). Ese día el portátil estaba limitado a 16–20 W de GPU y 1,3–1,4 GHz, frente a 30 W y 2,1 GHz de la medición del apartado 6. Por eso las dos pasadas son más lentas que esa medición; solo valen comparadas entre sí:

| | Sin cola | Con cola |
|---|---|---|
| Frames > 50 ms | 14 | **0** |
| Frames > 33 ms | 16 | **0** |
| Frame máximo | 80,7 ms | **27,3 ms** |
| Media / mediana | 10,96 / 11,30 ms | 10,14 / 9,84 ms |
| p95 / p99 | 15,08 / 16,69 ms | 16,40 / 18,40 ms |
| Frames > 16,67 ms | 1,03 % | 4,10 % |
| Disparos enemigos | 834 | 1589 |

**Advertencia sobre la carga: no es la misma.**
- La línea de tiro de los Big Bot (`BigBotCombat.ClearSegment`) la bloquea cualquier collider, incluidos los T-07.
- En la rejilla fija del benchmark, con la aparición simultánea los T-07 de delante tapan a los Big Bot, que casi no disparan (2–7 disparos por oleada).
- Con llegada escalonada sí encuentran hueco: 25–32 disparos con los T-07 primero, 33–48 con los Big Bot primero.
- Activando los 12 a la vez desde la precarga vuelven a 1–5.
- Por tanto la pasada con cola lleva casi el doble de fuego enemigo. Probablemente por eso empeoran el p95, el p99 y los frames > 16,67 ms, pero no está separado de forma aislada.
- En las arenas reales los puntos de aparición son otros, así que este efecto de la rejilla no tiene por qué repetirse igual.

**Pendiente:**
- La primera activación de la sesión es mucho más cara: sin cola, unos 140 ms para los 8 T-07, por inicialización única. En el benchmark cae en el calentamiento, que no se mide. En el juego cae en la primera oleada, y no está medido cuánto la reparte la cola.
- Repetir el A/B con el portátil a plena potencia para comparar con el apartado 6.
- 120 FPS sostenidos siguen sin alcanzarse.
