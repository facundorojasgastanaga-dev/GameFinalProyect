# Pase visual con Fable · F1 · e) Animación del Carroñero

Fecha: 21/11/2026. Rol: Director de arte (rol 2 del prompt 12). **Fase F1: propuesta → PARADA. Nada se implementa hasta que elijas el nivel de alcance.**

No se cambian daño, alcance, cadencia, velocidad, IA ni tiempos de `SawBotCombat`. Todo lo de aquí es pose: lee el estado del juego y no lo modifica.

Marcas: **[COMPROBADO]** lo he leído yo en código, prefabs o tiras; **[F0]** viene del inventario D o de `F0/carronero.txt`; **[PROPUESTA]** es diseño nuevo; **sin comprobar** lo que no he podido verificar leyendo.

---

## 0. Qué tienes que decidir

| # | Decisión | Mi recomendación |
|---|---|---|
| **E1** | Alcance: **Mínimo que se nota** (7 mejoras) o **Completo** (12) | Mínimo primero, medir, y Completo si el coste lo permite (§3) |
| **E2** | Muerte legible: hoy el estallido es instantáneo (retardo 0). Leer una pose antes exige un retardo de 0,16 s **solo en la variante de combate** | Sí a 0,16 s, tras comprobar que el cuerpo muerto no bloquea disparos (§5) |
| **E3** | Dónde se activa: solo Map01 (`Carronero_Map01.prefab`) o también en las salas de la campaña antigua | Solo Map01 (§6) |

---

## 1. Lo que hay hoy

### 1.1 Código y prefabs [COMPROBADO]

- `Assets/IronRequiem/Scripts/Enemies/CarroneroMotion.cs`: 105 líneas, `LateUpdate` con orden 150, sin Animator propio. Posa 10 huesos y la sierra: Hips, Spine1, las dos piernas (UpLeg y Leg), LeftArm y LeftForeArm (sierra), RightArm y Head.
- Paso: la fase avanza por distancia recorrida dividida entre `stride` (l.60; 0,575 m en el prefab, l.4696). La amplitud satura a 3 m/s (l.69). No hay anclaje de pie ni IK.
- Ataque: `attackBlend` va a 1 a 6 por segundo (l.61), es decir, en 0,17 s.
- La fase de paso empieza en 0 en todos (l.27); la semilla (l.36) solo afecta a respiración, cabeza y serrado.
- Filtro de cámara (l.66–68): si ningún `SkinnedMeshRenderer` es visible, no escribe la pose. No hay mezcla al volver.
- Muerte: los objetivos van a 0 y la sierra deja de girar en seco (l.56, l.91).
- `SawBotCombat` expone `IsAttacking` y `Preparation` (l.28–29). `Health.OnDamaged(float, GameObject)` existe (`Health.cs:13`). `EnemyMobile.AiState` es de lectura pública (`EnemyMobile.cs:33`).
- `CarroneroRigInfo.bones` guarda los 22 huesos y los busca por nombre (`Bone(name)`).
- **`Prefabs/Map01/Enemies/Carronero_Map01.prefab` es una variante de `Carronero.prefab`** (GUID `f6b6…cdd0`) con 0 componentes añadidos: solo sobrescribe nombre y transform. La usan `EncounterAuthoring` y `EncounterValidation`.
- `Carronero.prefab` lo referencian directamente estas escenas: `Room00_Hangar` (protegida), `Room00b_AlarmCorridor`, `Room02_Breach` (y `_v2`), `Room03_Assembly` y `Room04_Reactor` (y `_v2`).
- **El estallido de muerte del Carroñero no tiene retardo.** `Carronero.prefab:4659` dice `delay: 0`, y `Editor/VisualPass/VisualPassDeathFx.cs:69` lo fija a propósito: «horde: instant pop». Los 0,28 s son el valor por defecto de la clase y el del T-07, no el del Carroñero.

Valores del prefab que cito del inventario D sin haberlos releído: preparación 0,35 s, alcance 1,05 m, alcance de salida 1,35 m, intervalo de daño 0,12 s, velocidad 7,2 m/s, aceleración 26 m/s², velocidad de giro 300 °/s (140 °/s atacando), vida 26.

### 1.2 Lo que se ve en las tiras de F0 [COMPROBADO mirándolas]

| Tira | Qué muestra | Defecto |
|---|---|---|
| `01_correr.jpg` (24 frames, 1 por celda) | Torso y brazos con la misma silueta en las 24 celdas; las piernas son un borrón bajo el cuerpo | El pie de apoyo se mueve a 6,56 m/s con el cuerpo a 5,62 m/s [F0]: no se planta nunca. No se lee zancada, rebote ni inclinación |
| `06_andar.jpg` (32 celdas de 2 frames) | Velocidad forzada a 1,8 m/s; la IA la devuelve a 7,2 [F0]. Las piernas cambian de celda a celda sin ciclo reconocible | **No existe andar.** Pie de apoyo a 17,9 m/s [F0] |
| `02_atacar.jpg` (32 celdas de 2 frames) | Las celdas 3 a 8 quedan tapadas por un cuerpo que cruza delante de la cámara. De la 9 a la 32 la pose es prácticamente la misma: brazo de sierra extendido y quieto | No se lee anticipación ni recuperación. El brazo sostenido no se mueve durante al menos 0,8 s |
| `03_dano.jpg` (16 frames) | `TakeDamage(4)` en la celda 4. Las 16 celdas son iguales | Ninguna reacción visible al impacto |
| `04_girar.jpg` (24 celdas de 2 frames) | Giro de 180° en 0,8 s [F0]. El cuerpo gira rígido y vertical | Sin inclinación, sin que la cabeza guíe, sin pasos de giro |
| `05_morir.jpg` (32 celdas de 2 frames) | `Kill()` en la celda 5: el cuerpo se apaga; chispas en las celdas 6 a 8; fragmentos desde la 9; nada desde la 12 | Ninguna pose de muerte: se pasa de vivo a estallido |
| `07_reposo.jpg` (16 celdas de 4 frames) | Las 16 celdas son iguales | Parece una estatua. La respiración actual (6 mm) no se ve |
| `08_hurgar.jpg` (24 celdas de 4 frames) | Se agacha en las primeras celdas y queda con el brazo adelantado | Es el comportamiento del hangar (zona protegida): **no se toca** |

Nota sobre la tira del ataque: como las celdas 3 a 8 están tapadas, la subida del brazo no se puede juzgar en imagen; lo que afirmo de ese tramo sale del código (0,17 s). Conviene repetir esa captura con la cámara de lado antes de F5.

Por qué correr parece una vibración: con zancada de 0,575 m por ciclo, a 5,62 m/s salen 9,8 ciclos por segundo, unos 6 frames por ciclo a 60 fps. A 7,2 m/s son 12,5 ciclos por segundo, menos de 5 frames por ciclo. No hay ojo que lea eso como pasos.

---

## 2. Lista de animaciones, por prioridad

Tiempos en segundos y en frames a 60 fps. «M» = entra en el nivel Mínimo; «C» = solo en el Completo. Los huesos que se añaden a los 10 actuales van en negrita.

### A1 · Paso sincronizado con la velocidad real y pie plantado (M)

- **Problema F0:** `01_correr` y `06_andar`: el pie de apoyo patina a 6,56 y 17,9 m/s; la cadencia es ilegible.
- **Diseño [PROPUESTA]:** carrera con fase aérea. La zancada crece con la velocidad, de modo que la cadencia nunca supera lo que se puede leer:

| Velocidad real | Zancada por ciclo | Cadencia | Frames por ciclo |
|---|---|---|---|
| 1,8 m/s (si algún día anda) | 0,85 m | 2,1 Hz | 28 |
| 5,6 m/s (persecución medida) | 1,38 m | 4,1 Hz | 15 |
| 7,2 m/s (máxima) | 1,60 m | 4,5 Hz | 13 |

- **Fases de cada pierna, a 7,2 m/s (ciclo de 0,22 s, 13 frames):**
  - Apoyo, 0,07 s (4 frames): el pie queda **fijo en el mundo**; la pierna barre hacia atrás para alcanzarlo (IK analítico de dos huesos, sin raycast); la cadera baja 3 cm a mitad de apoyo.
  - Despegue, 0,03 s (2 frames): el pie rota de talón a punta 20–35°.
  - Vuelo, 0,12 s (7 frames): la rodilla se recoge, el pie adelanta en arco y la cadera sube 3–5 cm.
- La velocidad sigue saliendo del desplazamiento real del transform, como hoy. El suelo se toma de la altura de la raíz del agente; no hace falta raycast por pie en un NavMesh.
- **Huesos:** UpLeg y Leg de las dos piernas, Hips, **LeftFoot**, **RightFoot**. En Completo, también **LeftToeBase** y **RightToeBase**.
- **Test por frames** (paso fijo de 1/60, como en F0):
  - desplazamiento horizontal del pie marcado como plantado ≤ **2 cm por frame** (1,2 m/s; hoy 10,9 cm por frame);
  - en cada paso a 7,2 m/s hay ≥ 3 frames seguidos con el pie plantado;
  - cadencia ≤ 5 Hz a 7,2 m/s (hoy 12,5);
  - repetir la medida de F0 «velocidad horizontal media del pie de apoyo»: objetivo ≤ 0,6 m/s.
- **Riesgo:** la longitud real de la pierna está **sin comprobar**. Si es menor de unos 0,45 m, el barrido de apoyo no cabe y hay que subir la cadencia tope hasta 6 Hz (10 frames por ciclo), que todavía se lee.
- **Coste:** es la mejora más cara: dos resoluciones de IK por robot. Solo en LOD0 y LOD1 (§4).

### A2 · Fase aleatoria por individuo (M)

- **Problema F0:** en `04_girar` y `05_morir` aparecen dos Carroñeros juntos con poses muy parecidas (a tamaño de celda no puedo asegurar que sean idénticas). En el código la fase empieza en 0 para todos, así que los que arrancan a la vez van sincronizados.
- **Diseño:** fase inicial aleatoria, más ±8 % de cadencia y ±10 % de amplitud por semilla.
- **Huesos:** ninguno nuevo.
- **Test:** dos instancias creadas en el mismo frame tienen fases que difieren en ≥ 0,1 de ciclo; en una horda de 24, la desviación de fase no es 0.
- **Coste:** nulo.

### A3 · Anticipación del ataque (M)

- **Problema F0:** `02_atacar`: de la celda 9 en adelante el brazo ya está fuera y quieto. En el código el brazo sube en 0,17 s, la mitad de los 0,35 s de preparación sin daño.
- **Diseño:** usar los 0,35 s completos (se lee `combat.Preparation`, no se escribe un número fijo). La cadena va de cadera a pecho y de pecho a brazo:

| Fase | Tiempo | Frames | Qué pasa |
|---|---|---|---|
| Recogida | 0,00–0,10 s | 0–6 | La cadera baja 4 cm y retrocede; el pecho gira 12° alejando el hombro de la sierra; el brazo sube y se arma por encima del hombro; la cabeza se clava en el objetivo |
| Carga | 0,10–0,28 s | 6–17 | Se sostiene la pose armada con un temblor corto; la sierra acelera de 720 a 2.200 °/s. **Esta es la pose que el jugador tiene que leer** |
| Descarga | 0,28–0,35 s | 17–21 | La cadera empuja 6 cm hacia delante, el pecho desenrolla y el brazo cae al contacto |
| Contacto | 0,35 s | 21 | Coincide con el primer frame en que `SawBotCombat` puede hacer daño |

- La anticipación legible (recogida más carga) dura **0,28 s**, por encima del mínimo de 0,25 s y dentro de la preparación. El daño no se retrasa ni se adelanta: sigue saliendo de `SawBotCombat`.
- **Huesos:** Hips, Spine1, **Spine2**, LeftArm, LeftForeArm, Head, **Neck**. En Completo, **LeftShoulder**.
- **Test:**
  - desde el flanco de subida de `IsAttacking`, la punta de la sierra (`bladeEnd`) no cruza por delante del plano del hombro antes de 0,25 s ni después de 0,35 s;
  - a los 0,10 s el brazo difiere ≥ 25° de su pose de carrera;
  - el primer tick de daño no cambia de frame respecto a hoy.
- **Coste:** nulo (son las mismas rotaciones con otra curva, más dos huesos).

### A4 · Ataque sostenido que se mueve, y recuperación (M)

- **Problema F0:** `02_atacar`: el brazo extendido no cambia en al menos 24 celdas (0,8 s). El criterio de la skill es claro: nunca sostener un brazo estirado hacia el enemigo con la hoja quieta.
- **Diseño del sostenido:** ciclo de empuje y retirada de 0,36 s (22 frames), que equivale a tres ticks de daño de 0,12 s. El cuerpo se inclina 10° hacia el contacto y el codo trabaja entre 20° y 60°.
- **Diseño de la recuperación**, al caer `IsAttacking`:

| Fase | Tiempo | Frames | Qué pasa |
|---|---|---|---|
| Rebote | 0,00–0,08 s | 0–5 | El brazo sale hacia arriba y atrás con 10° de exceso; el pecho retrocede |
| Asentado | 0,08–0,25 s | 5–15 | Vuelve a la pose de carrera; la sierra baja de 2.200 a 720 °/s |

- Los 0,25 s coinciden con la espera que ya aplica la IA tras cancelar [F0], así que el siguiente ataque arranca siempre desde neutro.
- **Huesos:** los de A3.
- **Test:** durante el sostenido, la punta de la sierra recorre ≥ 8 cm en cada ventana de 0,36 s; tras caer `IsAttacking`, el brazo está a ≤ 5° de la pose de carrera a los 0,25 s.
- **Coste:** nulo.

### A5 · Reacción al impacto (M)

- **Problema F0:** `03_dano`: 16 celdas iguales.
- **Diseño:** capa aditiva que no interrumpe nada (no es un aturdimiento, la IA no se entera):

| Fase | Tiempo | Frames | Qué pasa |
|---|---|---|---|
| Golpe | 0,00–0,03 s | 0–2 | Pecho y cabeza saltan 10–14° en la dirección del impacto; la cadera cede 3 cm |
| Sostén | 0,03–0,07 s | 2–4 | Se mantiene |
| Vuelta | 0,07–0,18 s | 4–11 | Regresa con un rebote pequeño |

- La dirección sale del `source` de `Health.OnDamaged`. Los 0,18 s coinciden con el intervalo mínimo entre reacciones que ya usa `SawBotCombat`.
- Con 26 de vida y golpes de varios puntos, el Carroñero muere en pocos impactos: la reacción tiene que verse en los dos primeros frames.
- **Huesos:** Spine1, **Spine2**, **Neck**, Head, Hips.
- **Test:** en los 3 frames siguientes a `TakeDamage`, Spine2 gira ≥ 8° respecto a su tendencia; a los 0,25 s la diferencia es < 1°. Posición y velocidad del agente idénticas a una ejecución sin reacción.
- **Coste:** nulo cuando no hay impacto.

### A6 · Mezcla al volver a cámara (M)

- **Problema:** en el código, al volver a ser visible la pose salta de golpe. No hay tira de F0 para esto: habría que capturarla.
- **Diseño:** al pasar de no visible a visible, mezcla de 0,15 s (9 frames) desde la pose que tienen los huesos hasta la pose objetivo.
- **Huesos:** todos los que posa el componente.
- **Test:** en el primer frame visible, el cambio angular máximo de cualquier hueso es ≤ 8° (hoy puede llegar a unos 26–30°). Fuera de cámara, ninguna rotación escrita (igual que hoy).
- **Coste:** una interpolación por hueso durante 9 frames por reaparición.

### A7 · Inclinación en giros (M)

- **Problema F0:** `04_girar`: 180° en 0,8 s con el cuerpo vertical.
- **Diseño:** balanceo lateral hacia el interior del giro, proporcional a la velocidad angular y a la velocidad de avance, con tope de 12°. La cabeza gira primero (hasta 20° de adelanto) y el pecho contragira 6°. Entrada y salida en 0,12 s (7 frames).
- **Huesos:** Hips, Spine1, **Spine2**, **Neck**, Head.
- **Test:** girando a ≥ 200 °/s y ≥ 3 m/s, el balanceo de la cadera está entre 6° y 12° hacia el interior; en recta es ≤ 1°.
- **Coste:** nulo.

### A8 · Cambio de peso (C)

- **Problema F0:** `01_correr` y `07_reposo`: silueta plana, la cadera solo se inclina hacia delante.
- **Diseño:** la cadera se desplaza 2–3 cm hacia la pierna de apoyo y balancea 4°; Spine2 compensa; los hombros contragiran ±6° respecto a la cadera; el codo derecho acompaña; el cuello absorbe la mitad del cabeceo para que la cabeza, y con ella el ojo, se mantenga estable.
- **Huesos:** Hips, **Spine**, Spine1, **Spine2**, **LeftShoulder**, **RightShoulder**, RightArm, **RightForeArm**, **Neck**, Head.
- **Test:** el desplazamiento lateral de la cadera correlaciona con el pie plantado; la cabeza se mueve en vertical ≤ 40 % de lo que se mueve la cadera.
- **Coste:** medio (seis huesos más).

### A9 · Arranque y frenada (C)

- **Problema:** el agente pasa de 0 a 7,2 m/s en 0,28 s [F0] y la pose solo sigue la velocidad suavizada: no hay signo de aceleración. No hay tira de F0.
- **Diseño:** inclinación según la aceleración longitudinal medida.

| Momento | Tiempo | Frames | Qué pasa |
|---|---|---|---|
| Arranque | 0,00–0,08 s | 0–5 | La cadera baja 3 cm y el cuerpo se echa 14° hacia delante |
| Arranque, salida | 0,08–0,28 s | 5–17 | Se endereza hasta la inclinación de carrera |
| Frenada | 0,00–0,12 s | 0–7 | Cuerpo 10° hacia atrás, cadera 4 cm abajo, brazos adelante, los dos pies plantados |
| Frenada, salida | 0,12–0,32 s | 7–19 | Asienta en reposo |

- La raíz no se retrasa: la mueve la IA.
- **Huesos:** Hips, Spine1, **Spine2**, piernas, brazos.
- **Test:** con aceleración ≥ 15 m/s², cabeceo de la cadera ≥ 10° en ≤ 0,10 s; al frenar, cabeceo negativo ≥ 6°.
- **Coste:** nulo.

### A10 · Reposo y alerta (C)

- **Problema F0:** `07_reposo`: estático.
- **Diseño del reposo:** el peso pasa de un pie a otro cada 2–3 s; la cabeza barre a saltos de 15–25° cada 1,2–2,5 s, distinto por individuo; los hombros suben 2°; la sierra da tirones cortos.
- **Diseño de la alerta**, cuando `EnemyMobile.AiState` pasa a seguir:

| Fase | Tiempo | Frames | Qué pasa |
|---|---|---|---|
| Sobresalto | 0,00–0,10 s | 0–6 | La cabeza salta hacia el objetivo y el cuerpo se agacha 6 cm |
| Arranque | 0,10–0,25 s | 6–15 | Enlaza con A9 |

- No retrasa la persecución: es pose encima del movimiento.
- **Huesos:** Hips, Spine1, **Spine2**, **Neck**, Head, **LeftShoulder**, **RightShoulder**, **LeftHand**.
- **Test:** en 2 s de reposo, la cabeza cambia ≥ 15° al menos una vez y la cadera se desplaza ≥ 1,5 cm lateralmente.
- **Coste:** bajo.

### A11 · Giro en el sitio con pasos (C)

- **Problema F0:** `04_girar`: gira sobre sí mismo sin mover los pies.
- **Diseño:** por debajo de 0,5 m/s, los pies se replantan cada 45° de giro, alternando, con 0,10 s (6 frames) por paso.
- **Huesos:** piernas, **LeftFoot**, **RightFoot**, Hips.
- **Test:** girando en el sitio, el pie plantado no deriva más de 2 cm por frame ni gira más de 45° respecto a su orientación de plantado.
- **Coste:** usa el IK de A1.

### A12 · Muerte que se lea (C, depende de E2)

Ver §5.

### Resumen de prioridad

| Prio. | Animación | Tira de F0 | Nivel | Huesos nuevos | Coste |
|---|---|---|---|---|---|
| 1 | A1 Paso y pie plantado | 01, 06 | M | LeftFoot, RightFoot | Alto (IK) |
| 2 | A3 Anticipación del ataque | 02 | M | Spine2, Neck | Nulo |
| 3 | A4 Sostenido y recuperación | 02 | M | — | Nulo |
| 4 | A5 Reacción al impacto | 03 | M | — | Nulo |
| 5 | A2 Fase aleatoria | 04, 05 | M | — | Nulo |
| 6 | A6 Mezcla al volver a cámara | sin tira | M | — | Bajo |
| 7 | A7 Inclinación en giros | 04 | M | — | Nulo |
| 8 | A8 Cambio de peso | 01, 07 | C | Spine, hombros, RightForeArm | Medio |
| 9 | A9 Arranque y frenada | sin tira | C | — | Nulo |
| 10 | A10 Reposo y alerta | 07 | C | LeftHand | Bajo |
| 11 | A11 Giro en el sitio | 04 | C | — | Bajo |
| 12 | A12 Muerte legible | 05 | C | RightHand, dedos del pie | Bajo |

---

## 3. Dos niveles de alcance

| | **Mínimo que se nota** | **Completo** |
|---|---|---|
| Incluye | A1, A2, A3, A4, A5, A6 y A7 | Todo: A1 a A12 |
| Huesos posados | 14 de 22 (los 10 de hoy más LeftFoot, RightFoot, Spine2 y Neck) | 20 a 22 de 22 |
| Qué arregla | El patinaje, la cadencia, el ataque sin aviso, el impacto sin respuesta, la horda sincronizada, el salto al volver a cámara y el giro rígido | Además: peso, arranque y frenada, reposo vivo, giro con pasos y muerte |
| Qué queda sin tocar | Reposo estático, arranque y frenada, muerte instantánea | Nada de la lista |
| Coste estimado frente a 1,25 ms | Mismo orden si el IK se limita a LOD0–1; hay que medirlo | Mayor: probablemente exige limitar el nivel completo a LOD0 |
| Riesgo | Bajo | Medio: más huesos en una malla de piel dura, más casos de piezas que se separan o se atraviesan |
| Decisiones tuyas | Ninguna más | E2 (retardo de muerte) |

Recomiendo implementar el Mínimo, medirlo con 24 Carroñeros y, si cabe, seguir con A8, A9 y A10 en ese orden. A12 solo si apruebas E2.

---

## 4. Coste

- **Presupuesto:** `LateUpdate` ≈ 1,25 ms para 24 Carroñeros en el Editor [F0, `CHECKLIST_TRABAJO.md:871`], unos 52 µs por robot visible. No hay cifra de build.
- **Hoy, por robot visible:** unas 12 rotaciones escritas, unos 25 productos de cuaterniones y una docena de senos y cosenos.
- **Mínimo, por robot en LOD0–1:** 4 rotaciones más y dos IK de dos huesos (cada uno, dos raíces, dos arcocosenos y unos 6 productos). Estimo entre un 50 y un 70 % más de aritmética que hoy en esos robots. **Es una estimación sin medir.**
- **Para quedar en el mismo coste o menor**, animación por niveles de detalle. El filtro de cámara ya recorre los cuatro `SkinnedMeshRenderer` y el `LODGroup` solo deja uno activo, así que saber el LOD no cuesta nada:

| Situación | Altura en pantalla | Animación |
|---|---|---|
| LOD0 y LOD1 | ≥ 10 % | La del nivel elegido, con IK |
| LOD2 | 4–10 % | Paso con la cadencia nueva y fase aleatoria, sin IK ni huesos extra; brazo con anticipación |
| LOD3 | < 4 % | Solo cadera y piernas, actualizadas un frame de cada dos, alternando por individuo |
| Fuera de cámara | — | No posa (igual que hoy) |

- En una horda de 24, lo normal es que solo unos pocos estén en LOD0–1 a la vez. Eso es lo que hay que comprobar midiendo, no suponer.
- **Cómo se mide:** `HordeCostProbe` (menú `Tools > Iron Requiem > Visual Pass > C6`), misma escena y misma carga antes y después, anotando el reparto de LOD. El probe mide el frame completo, así que conviene añadirle un paso que apague solo el componente nuevo.
- **Si supera 1,25 ms:** primero, IK solo en LOD0; después, cadencia de actualización a la mitad en LOD1.
- **Posible ahorro, sin proponerlo todavía:** el inventario D señala que el Animator heredado y las ruedas del SawBot siguen ejecutándose en cada Carroñero. El probe ya tiene un paso «sin Animator». Si sale una cifra relevante, sería otra tarea, porque `SawBotCombat` escribe en ese Animator.
- Sin asignaciones de memoria por frame, sin `Light` y sin raycasts.

---

## 5. La muerte: lo que el retardo actual permite

El encargo decía «si cabe en el retardo actual (0,28 s)». **Ese no es el retardo del Carroñero: es 0** (§1.1). En `05_morir` no hay pose de muerte porque el cuerpo se oculta en el mismo frame en que muere.

Opciones:

| Opción | Qué se hace | Qué se ve | Qué necesita |
|---|---|---|---|
| **M-a · No tocar el retardo** | Nada en la pose | Igual que hoy. La «lectura» de la muerte queda en el último impacto (A5) y en el estallido | Nada |
| **M-b · Retardo de 0,16 s solo en la variante de combate** | Sobrescribir `delay` de `MechDeathExplosion` en `Carronero_Map01.prefab` y posar un colapso | 10 frames de colapso y después el estallido de siempre | Tu aprobación (E2) y una comprobación previa |

Colapso de M-b:

| Fase | Tiempo | Frames | Qué pasa |
|---|---|---|---|
| Corte | 0,00–0,03 s | 0–2 | El ojo y los indicadores se apagan (ya ocurre); la cabeza cae; el pecho se arquea en la dirección del último impacto |
| Colapso | 0,03–0,13 s | 2–8 | Las rodillas ceden (cadera −10 cm); el brazo de sierra cae; la sierra frena de 2.200 °/s a 0 |
| Estallido | 0,16 s | 10 | `MechDeathExplosion`, sin cambios |

Por qué 0,16 s y no 0,28: el retardo 0 se eligió a propósito para las hordas («instant pop»). Con 0,16 s la muerte se lee y el ritmo de horda casi no cambia.

Comprobación previa (**sin comprobar hoy**): durante esos 0,16 s el cuerpo muerto no debe parar disparos dirigidos a los robots de detrás ni contar como vivo para la ronda. Si lo hace, M-b queda descartada, porque cambiaría el combate.

Test de M-b: entre `Kill()` y el estallido pasan 0,16 s ± 1 frame; en ese tramo la cadera baja ≥ 6 cm y la sierra pierde ≥ 80 % de su velocidad; la ronda termina en el mismo frame que hoy.

---

## 6. Cómo se limita a los prefabs de combate

Regla del encargo: `ScavengerBehaviour` (HangarIntro), `BridgeWalker` (Menu) y `CctvActor` usan el mismo `CarroneroMotion` y son zona protegida. No pueden cambiar de aspecto.

### Opción recomendada: componente nuevo solo en la variante

1. Componente nuevo `CarroneroCombatMotion` en `Assets/IronRequiem/Scripts/Enemies/`, namespace `IronRequiem`, con orden de ejecución posterior a 150.
2. Se añade **solo** a `Prefabs/Map01/Enemies/Carronero_Map01.prefab`, que hoy no tiene ningún componente añadido.
3. En esa misma variante se desactiva `CarroneroMotion` (sobrescritura de `enabled`), y el componente nuevo posa todo, incluida la sierra, leyendo los huesos de `CarroneroRigInfo.bones`.
4. **`CarroneroMotion.cs` y `Carronero.prefab` no se editan.** Hangar, menú y CCTV siguen byte a byte como están.

Detalles a tener en cuenta:

- `CarroneroMotion.Awake` reparte la prioridad de evasión de la horda (l.37). `Awake` se ejecuta aunque el componente esté desactivado, así que eso se conserva. Conviene confirmarlo con un test.
- `HordeCostProbe` apaga `CarroneroMotion` en uno de sus pasos: hay que añadirle el componente nuevo para que la medida siga siendo válida.
- Las salas de la campaña antigua referencian `Carronero.prefab` directamente, así que conservarían la animación vieja. Map01 las reemplaza (decisión U6 de `MAP01_DESIGN.md`). Si las quieres con la animación nueva, habría que redirigir sus spawns a la variante, y eso toca escenas que no son de este pase: es la decisión E3.

### Opción descartada: un interruptor dentro de `CarroneroMotion`

Añadir un campo que por defecto deje el comportamiento actual. Funciona, pero edita el archivo que comparten las tres zonas protegidas. El encargo dice que en ese caso hay que parar y pedir permiso. No la recomiendo.

### Guardas de regresión

- EditMode: el hash de `CarroneroMotion.cs`, `Carronero.prefab` y `Prefabs/Menu/MenuWalker_Carronero.prefab` coincide con el respaldo.
- EditMode: ninguna escena protegida (`IntroMenu`, `Room00_Hangar`) contiene `CarroneroCombatMotion`.
- PlayMode: la intro y el menú siguen idénticos (hash o captura), como ya pide el prompt 12.
- PlayMode: el Carroñero de combate sigue sin posar fuera de cámara.

---

## 7. Método de trabajo para F5

Adaptado de la skill `game-dev-combat-animation`:

1. **Estándar antes de tocar poses.** Un documento corto con reglas numeradas, cada una marcada como «test» (medida) o «revisada» (puntuada en frames). Borrador de reglas:

| # | Regla | Tipo |
|---|---|---|
| R1 | Pie plantado: ≤ 2 cm por frame de deriva horizontal | test |
| R2 | Cadencia ≤ 5 Hz a 7,2 m/s | test |
| R3 | Anticipación del ataque entre 0,25 y 0,35 s, medida en la punta de la sierra | test |
| R4 | El primer tick de daño no cambia de frame | test |
| R5 | El brazo sostenido nunca queda quieto más de 0,36 s | test |
| R6 | Reacción al impacto visible en ≤ 3 frames y terminada en ≤ 0,25 s | test |
| R7 | Primer frame visible: ≤ 8° de salto por hueso | test |
| R8 | Fuera de cámara no se escribe ninguna rotación | test |
| R9 | Dos individuos nunca comparten fase | test |
| R10 | Rangos articulares: codo 10–150°, rodilla sin hiperextensión, ningún segmento separado más de 1 cm de su vecino | test |
| R11 | La cadena va de cadera a pecho y de pecho a brazo | revisada |
| R12 | La silueta de la carga del ataque se distingue de la de carrera a 15 m | revisada |
| R13 | La cabeza se mantiene estable al correr | revisada |
| R14 | Posición, velocidad y daño idénticos a una ejecución sin el componente | test |

2. **Línea base primero.** Las ocho tiras de F0 son el «antes». Faltan tres: ataque con la cámara de lado, arranque y frenada, y vuelta a cámara.
3. **Medir antes de mirar.** Rangos articulares y deriva del pie con el rig real, antes de gastar una captura.
4. **Cada test se prueba rompiéndolo.** Al añadir un test, se restaura el defecto un momento para ver que falla.
5. **Dos cámaras.** La del jugador a 5 y a 15 m, y la vista de horda con 24. Además, las del hangar y el menú para la regresión.
6. **Juicio.** Dos críticos a ciegas con una rúbrica fija y un A/B a ciegas contra las tiras de F0, con los mismos títulos. Cada afirmación de un crítico se comprueba contra los frames a resolución completa antes de actuar. Encaja con el rol REVISOR del prompt 12.

Lo que no se aplica de esa skill: las reglas de estiramiento de piel (la malla es de piel dura por hueso [F0], así que el riesgo es que las piezas se separen o se atraviesen, no que la piel se retuerza), los presets de captura de movimiento de Tripo (no se gastan créditos) y los scripts de su proyecto de origen.

---

## 8. Dudas y cosas sin comprobar

| # | Asunto |
|---|---|
| 1 | El retardo de muerte del Carroñero es 0, no 0,28 s. Decisión E2 |
| 2 | La longitud real de la pierna, que fija la zancada y la cadencia tope de A1 |
| 3 | Si el cuerpo muerto bloquea disparos o cuenta para la ronda durante un retardo (§5) |
| 4 | Cómo define la herramienta de F0 «pie de apoyo». Hay que reutilizar la misma medida |
| 5 | Si el orden de `views` coincide con el orden de los LOD; se resuelve una vez al iniciar |
| 6 | Si el clip de impacto del Animator heredado llega a algún hueso (el inventario D cree que no). Si llegara, se sumaría a A5 |
| 7 | Las celdas 3 a 8 de `02_atacar` están tapadas: la anticipación actual no está capturada en imagen |
| 8 | Ninguna cifra de coste es de build; todo es del Editor |

---

## 9. Skills usadas

| Skill | Qué he aplicado |
|---|---|
| `game-dev-combat-animation` | El bucle (estándar antes de las poses, línea base, medir antes de mirar, tests que fallan al restaurar el defecto, dos cámaras, críticos a ciegas y A/B, verificar cada afirmación). Fases sobre el reloj del juego con el contacto en su frame. Cadena cinética. Mantener en movimiento lo que sigue al golpe y no sostener un brazo estirado con la hoja quieta. Rangos articulares como regla medida |
| `iron-requiem-performance` (local) | Presupuesto derivado de medida; misma carga antes y después; distinguir Editor y build; LOD con animación más simple; no quitar una mecánica para ahorrar |
| `iron-requiem-fps` (local) | No tocar jugador ni armas; separar prueba visual de prueba real |
| `gamedev:level-design`, `gamedev:create-game-assets`, `unity:craft-game-floors` | Se aplican en `01d_DECORACION.md` |
