# Puertas: Redesign v01 (fase P1), tres direcciones

17/11/2026. Son bocetos 3D propios (blockouts procedurales en Blender 5.2.3 LTS, Eevee, 1280×720) para que el usuario **elija** una dirección. No se eligió ninguna ni se tocó nada fuera de esta carpeta: ni `Assets/`, ni escenas, ni prefabs, ni `ProjectSettings`, ni el Editor. Tampoco se usó Meshy ni se hicieron compras o descargas. El audio se cita por ruta y no se copió.

Fuentes leídas: `Tools/IronRequiem/Reports/MapFxAudit2/doors.md` (inventario de las 13 puertas y contrato), `Docs/IronRequiem/MAP_FX_AUDIT_20261117_r2.md` §6 y las capturas `Tools/IronRequiem/Reports/MapFxAudit/doors/*.png`. Se siguió el patrón de `Docs/IronRequiem/Concepts/Weapons/Redesign_v01/`.

## 0. Resumen y recomendación

| Dir. | Idea en una frase | Apertura / cierre | Lámina |
|---|---|---|---|
| **D-A Mandíbula telescópica** | Dos hojas con junta dentada. Los cerrojos laterales salen, el cubo central gira, las hojas se separan 8 cm y se telescopan hacia casetes laterales. | 0,80 s / 0,45 s | `D-A/D-A_Mandibula_lamina.png` |
| **D-B Persiana telescópica** | Lamas horizontales. Las levas laterales giran y los pasadores de suelo suben; la lama inferior sube, recoge a las demás y todas acaban en un casete superior corto. | **0,70 s / 0,45 s** (los tiempos actuales) | `D-B/D-B_PersianaTelescopica_lamina.png` |
| **D-C Bóveda batiente** | Hojas de bóveda con junta diagonal. Los cerrojos superiores suben, el sello cede y las hojas giran 90° hacia la sala con brazos hidráulicos (en V3 y V4, plegables de 4 paneles). | 1,00 s / 0,45 s | `D-C/D-C_BovedaBatiente_lamina.png` |

**Recomendación: D-B como familia base para las 13 puertas, y D-A solo para la esclusa principal si el jugador debe sentir esa puerta como especial.** D-C queda descartada para combate. El razonamiento está en el §7.

## 1. Restricciones comunes que respetan las tres direcciones

Salen del contrato de `AirlockDoorMotion` y `CampaignFlow` (doors.md §1 y §5):

- **El root de la puerta no se mueve.** `DoorPresentation.Evaluate(k, closing)` anima las hojas, que son hijas del root, con el mismo `k` que hoy desplaza el root. De ese modo `IsClosed` sigue siendo exacto: root en `closedWorld`, sin corrutina y activo.
- **El root lleva un único BoxCollider sólido que cubre el vano.** Está activo durante toda la apertura y desde el primer frame del cierre. Al terminar de abrir, el root queda **inactivo** y sin colisión. Las hojas no llevan colliders propios en D-A ni en D-B; en D-C se discute en su sección.
- **El marco o alojamiento es un hermano siempre activo** (`DoorRig`), no un hijo del root. Contiene casetes, jambas, dintel, cerrojos y levas (que se retraen hacia el marco y siguen visibles con la puerta abierta), tiras de estado, balizas, la luz y el componente que escucha los eventos. En los renders, el estado `ABIERTA` muestra exactamente eso: el root ya no existe y solo queda el marco.
- **Al desactivarse el root, ninguna hoja puede "desaparecer a la vista".** En la pose final (k=1), las hojas de D-A y D-B están dentro de su casete. D-C no puede esconderlas y necesita un **proxy de hojas aparcadas** en el marco (ver D-C).
- **Nada atraviesa techos ni paredes.** Los renders incluyen el túnel de la esclusa con su techo real sobre el vano: 0,5 m en la cámara (V1 y V3) y 0,95 m en Transfer y escalera (V2 y V4).
- Escala de las variantes (la misma en las tres direcciones):

| Variante | Vano (an × al × grosor) | Puertas actuales que sustituiría |
|---|---|---|
| V1 Esclusa principal | 8,4 × 4,8 × 0,6 m | ChamberHangarDoor, ChamberCorridorDoor (esclusa a0, **zona protegida**: ver §7) |
| V2 Paso entre salas | 10 × 8 × 0,6 m | SourceDoor y DestinationDoor de Transfer_27, _91 y _155 (6 puertas) e InitialLoadingDoor (12×7) |
| V3 Persiana interior | 8,4 × 4,8 × 0,5 m | VestibuleShutter; ChaseEntranceGate y ChaseExitGate (Pursuit05, hoy aparecen de golpe) |
| V4 Salida de sala | 12 × 7,5 × 1,0 m | Room01ExitDoor, Room02EntryDoor (plano YZ, 8–12 m de ancho) y LobbyRear (14×7, que habría que reducir a ≤12 m porque sobresale del techo del lobby) |

## 2. Lenguaje de estado (común a las tres direcciones)

Todas las señales están en el **marco** (siempre activo), salvo la rendija o labio emisivo de las hojas, que solo refuerza la señal mientras la puerta está cerrada. Los colores son emisivos por máscara del material HardSurface, recoloreados con un MaterialPropertyBlock (`_EmissionColor`), igual que hace hoy `AirlockCycle` con `_BaseColor`.

| Estado | Cuándo (derivable sin tocar `CampaignFlow`) | Color | Forma y movimiento (lectura sin color) | Rótulo | Luz dinámica | Render V1 |
|---|---|---|---|---|---|---|
| **BLOQUEADA** | Sala actual sin `Completed`. Puerta de origen ya cerrada tras el ciclo. | Rojo (1, 0,03, 0,02) ×9 | Tiras **continuas y fijas** en jambas, dintel y hoja. Balizas encendidas sin giro. | BLOQUEADA | 1 punto rojo, sin sombras | `*_V1_blocked.png` |
| **ABRIÉNDOSE** | `Set(door,false)` hasta que el root se desactiva. | Ámbar (1, 0,36, 0,02) ×9 | **Balizas giratorias** (2 conos aditivos por baliza, ~1 vuelta cada 0,6 s). Las tiras laten si dura más de 0,5 s. | ABRIENDO | La misma luz en ámbar | `*_V1_unlock.png`, `*_V1_half.png` |
| **ABIERTA / PASA** | Root inactivo. | Verde (0,1, 1, 0,22) ×7 **o el color temático de la sala** (p. ej. cian de los rótulos de sector) | Tiras fijas. Se podrían añadir chevrones que apunten hacia dentro (P2). | PASO | La misma luz en verde, o apagada para ahorrar | `*_V1_open.png` |
| **ALARMA** | `CorridorAlarm` activo (Room00b, primera oleada → sala completa). La puerta sigue cerrada. | Rojo y blanco alternos | **Estroboscopio** a 4 Hz en franjas rojas y blancas y balizas rojas girando. Se distingue de BLOQUEADA por el parpadeo y el giro. | ALERTA | Luz roja pulsante | `*_V1_alarm.png` |
| Ciclo de esclusa (sin render) | `Prepare`: la puerta se cierra detrás y se espera la carga. | Ámbar parpadeante a 2,2 Hz (los valores actuales de `AirlockCycle`) | Igual que ABRIÉNDOSE, pero en el lado interior. | CICLO | — | — |
| Abort (sin render) | El jugador sale del trigger más de 1 s. | Ámbar → verde | Como la apertura normal, pero añadiendo sonido (hoy es silencioso). | PASO | — | — |

Notas:
- **Se corrige la contradicción actual**: hoy a0 está en verde estando bloqueada y `ExitFrameGlow` está siempre encendido. Con esta propuesta, el verde solo aparece con el root inactivo.
- El rótulo duplica el color con texto, pensando en los daltónicos. Se puede hacer con TMP (ya se usa en `TransitSign`) o, mejor, con un atlas de 4 palabras o iconos en la máscara emisiva, lo que evita un material extra.
- `CorridorAlarm` no toca puertas. Para ALARMA, el `DoorRig` de LobbyRear observaría `CombatTwoRoomsLab.WaveIndex/Completed`, igual que hace hoy `CorridorAlarm`.

## 3. D-A Mandíbula telescópica

![](D-A/D-A_Mandibula_lamina.png)

**Descripción.** Dos hojas cuya junta central es una **dentadura** en zigzag de 3 dientes (4 si H ≥ 6 m), con un **cubo de cierre** circular (franjas de peligro y 4 orejetas) partido entre las dos hojas. Cada hoja tiene dos segmentos: el interior, con los dientes y por delante, y el exterior, retranqueado 0,25 m. Al abrir, el interior pasa por delante del exterior y los dos acaban dentro de un **casete lateral** fijo de `W/4 + diente + 0,45 m` de ancho (2,9 m en V1 y 3,4 m en V2). Seis **dogs** (pestillos de acero) del marco abrazan las hojas y se retraen 0,65 m hacia los casetes.

**Variantes.** V1 es la doble hoja de referencia. V2 tiene 4 dientes y es más alta. **V3** es una persiana de una sola hoja en dos segmentos que se recogen hacia un único casete derecho de `W/2 + 0,5 m` (4,7 m), con ranuras emisivas horizontales. V4 es la doble hoja de 12 m y 1 m de grosor.

**Secuencia por fases** (`D-A/D-A_Mandibula_cronograma.png`):

| Fase | Tiempo | k | Qué se mueve (pivote) | Audio candidato (existente) |
|---|---|---|---|---|
| A1 Cerrojos | 0,00–0,17 s | 0–0,21 | Los 6 dogs del marco se trasladan ±X 0,65 m (pivote: su centro). Los 2 medios cubos giran 30° sobre Y (pivote: centro del cubo, en la hoja). | `Assets/IronRequiem/Audio/SFX/V4/IR_Mount_Lock.wav` (0,8 s) |
| A2 Sello | 0,17–0,25 s | 0,21–0,31 | Las hojas se separan 8 cm en ±X. Vapor en la junta (podría reutilizar el `CycleSteam` de a0). | `Assets/IronRequiem/Audio/SFX/HangarIntro/IR_Hangar_Hydraulic.wav` (1,6 s, recortado) |
| A3 Deslizamiento | 0,25–0,80 s | 0,31–1 | El segmento interior recorre `W/2 + diente + 0,25` y el exterior `W/4 + 0,25`, con smoothstep (pivote: canto exterior inferior de cada segmento). | `Assets/IronRequiem/Audio/SFX/HangarIntro/IR_Hangar_Servo.wav` (1,0 s, pitch 0,8) |
| Fin | 0,80 s | 1 | El root se desactiva; las hojas ya están dentro de los casetes. | `Assets/IronRequiem/Audio/SFX/HangarIntro/IR_Airlock_Done.wav` (1,0 s) |
| Cierre C1 | 0,00–0,33 s | — | Deslizamiento inverso con ease-in (k², como ahora). | `IR_Hangar_Servo.wav` |
| Cierre C2 | 0,33–0,37 s | — | Los dientes encajan. | `Assets/IronRequiem/Audio/SFX/V4/IR_Fist_HeavyImpact.wav` (0,9 s) |
| Cierre C3 | 0,37–0,45 s | — | Dogs y cubo vuelven a su sitio. `IsClosed` solo se cumple al terminar esta fase. | `Assets/IronRequiem/Audio/SFX/V4/IR_Mount_Clamp.wav` (0,5 s) |

**Alargamiento: +0,10 s al abrir** (0,80 en lugar de 0,70). Las fases A1 y A2 suman 0,25 s y el deslizamiento necesita al menos 0,5 s para leerse como pesado. Si hay que quedarse en 0,70 s, se recorta A3 a 0,45 s. El cierre se queda en 0,45 s.

**Restricciones técnicas para P2**
- Piezas móviles (hijas del root): `Inner_L/R` y `Outer_L/R` (traslación pura en X), `Hub_L/R` (rotación Y local). En V1 son 4 + 2 objetos, o 2 mallas por lado si el cubo va en un hueso. Piezas del marco que se mueven: 6 `Dog` con traslación X.
- **Espacio.** Esta es la restricción dura de D-A: cada lado necesita un bolsillo de 2,9 m (V1), 3,4 m (V2) o 3,8 m (V4) **más allá del vano y dentro del grosor del muro**. La cámara a0 tiene paredes laterales en ±4,5 m para un vano de 8,4 m, así que el bolsillo cae dentro del muro lateral. La Transfer tiene paredes en ±6,5 m para 10 m. Por tanto: (a) hay que **montar los casetes en la cara de la sala** (la hoja avanza 0,3 m hacia la sala antes de deslizarse, al estilo de una puerta de enchufe), o (b) entallar los muros laterales, o (c) estrechar los vanos. Los renders muestran el túnel empezando detrás del muro de la puerta. En P2 hay que comprobarlo puerta por puerta.
- El techo no influye: el movimiento es horizontal.
- Triángulos: ver §6.
- Materiales: 1 HardSurface (atlas de chapa pintada, marco, franjas de peligro y acero) con máscara emisiva. 1 material aditivo compartido para los haces. Rótulo con atlas emisivo o TMP.
- Luces: 1 punto por puerta (color de estado, sin sombras, rango ~8 m). Las balizas son conos aditivos sin luz.
- Colliders: solo el BoxCollider del root. Los casetes y dogs llevan colliders estáticos del marco, que pertenecen al muro.

**Riesgos**
- **Atrapar al jugador**: las hojas cierran desde los lados hacia el centro. Como hoy, el root es sólido desde el primer frame. La pinza dentada es una zona de aplastamiento visual: sumar la comprobación de solape propuesta en doors.md §5 antes de C2.
- **Lectura**: muy buena. La silueta dentada y el cubo giratorio hacen la puerta inconfundible desde lejos, y el estado se lee en dientes, rendijas y jambas.
- **Coste**: medio-alto, por los casetes grandes, 6 dogs y el espacio que obligan a reservar en el nivel.
- **Encaje con las variantes**: excelente en V1 (puerta de prestigio) y bueno en V4, si la sala tiene muro ancho. V2 y V3 son donde peor encaja: en la Transfer no hay muro lateral, y una persiana de una hoja necesita un casete de 4,7 m.

## 4. D-B Persiana telescópica

![](D-B/D-B_PersianaTelescopica_lamina.png)

**Descripción.** Lamas horizontales de 0,8–0,9 m (0,5 m en V3), escalonadas en profundidad para formar una coraza laminar. La lama 0 (inferior) lleva franjas de peligro, un **labio emisivo** de estado y **3 pasadores** que entran en alojamientos del umbral. Cada tercera lama tiene una banda emisiva para que el estado se lea en toda la altura. En las jambas, **4 levas** (2 por lado) giran 90° para soltar las lamas. Al abrir, la lama 0 sube y **recoge** a cada lama cuando llega a ella (efecto telescópico, con un "clac" por lama). Al final todas quedan apiladas dentro de un **casete superior corto**: alto = una lama + 0,5 m (1,3–1,4 m) y fondo = n·grosor (0,7–1,1 m), montado en la **cara de la sala**.

**Variantes.** V1 tiene 6 lamas gruesas (0,14 m), con franjas de peligro en las dos inferiores. V2 tiene 9 lamas con bandas de estado. **V3** tiene 10 lamas finas (0,07 m), con aspecto de persiana enrollable de blindaje; también sirve para las puertas de persecución, que hoy aparecen de golpe y se podrían bajar en 0,45 s. V4 tiene 9 lamas de 12 m. Las mismas piezas sirven en todas: solo cambian n, el ancho y el grosor.

**Secuencia por fases** (`D-B/D-B_PersianaTelescopica_cronograma.png`):

| Fase | Tiempo | k | Qué se mueve (pivote) | Audio candidato |
|---|---|---|---|---|
| B1 Levas y pasadores | 0,00–0,15 s | 0–0,21 | 4 levas del marco giran 90° (pivote: eje en la jamba). Los 3 pasadores suben 0,32 m (hijos de la lama 0). | `Assets/IronRequiem/Audio/SFX/V4/IR_Mount_Clamp.wav` (0,5 s) |
| B2 Elevación | 0,15–0,70 s | 0,21–1 | La lama 0 sube `H + 0,15` con smoothstep. La lama i se mueve `max(0, d − i·h)` (pivote: canto inferior de cada lama, traslación Z pura). | `IR_Hangar_Hydraulic.wav` (silbido al despegar) + `Assets/IronRequiem/Audio/SFX/HangarIntro/IR_Hangar_Clatter.wav` (1,1 s; o un "clac" por lama con `IR_Fist_QuickImpact.wav`, 0,35 s, a volumen bajo) |
| Fin | 0,70 s | 1 | Root inactivo; las lamas ya están dentro del casete. | `IR_Airlock_Done.wav` |
| Cierre C1 | 0,00–0,36 s | — | Caída escalonada con ease-in: la lama superior suelta primero y la 0 llega la última. | `IR_Hangar_Clatter.wav` |
| Cierre C2 | 0,36–0,39 s | — | Golpe contra el umbral. | `IR_Fist_HeavyImpact.wav` |
| Cierre C3 | 0,39–0,45 s | — | Pasadores abajo y levas cerradas. | `Assets/IronRequiem/Audio/SFX/V4/IR_Mount_Lock.wav` |

**Duración: igual que ahora** (0,70 s al abrir y 0,45 s al cerrar). No hace falta tocar `OpenSeconds` ni `CloseSeconds`.

**Restricciones técnicas para P2**
- Piezas móviles (hijas del root): n lamas (traslación Z) y 3 pasadores (traslación Z local, hijos de la lama 0). Se pueden fusionar las n lamas en una malla con n huesos o hacer n MeshRenderers con el mismo material, que el SRP Batcher agrupa. Piezas del marco que se mueven: 4 levas en rotación.
- **Espacio**: no hace falta bolsillo lateral (las lamas entran 0,3 m en canales de las jambas). Sí hace falta **h + 0,5 m de muro por encima del vano en la cara de la sala**, y nada en el lado de la esclusa: 1,3 m en V1, 1,39 m en V2 y V4 y 0,98 m en V3.
  - Cámara a0: el muro del hangar sobre la puerta es alto (captura `a0-source-…-0.5s`), así que cabe.
  - Transfer: en la sala, `DoorHeader_V35` ocupa y 16–21 sobre una puerta que llega a y 16, de modo que el casete cabe en la cara de la sala. **Dentro de la esclusa no sube nada**, así que se respeta el techo de 0,95 m.
  - `Room01ExitDoor` y `Room02EntryDoor`: el casete va en la cara de la sala, nunca en el lado de la escalera, donde el techo está a 0,95 m. Además se elimina el deslizamiento coplanario con `UpperInnerStairGuard` (hoy produce z-fighting), porque las lamas ya no recorren ese plano.
  - Los renders `*_V1_open.png` y `*_conjunto_half.png` muestran que no hay ninguna pieza por encima del casete.
- Triángulos: ver §6. Es la dirección más barata.
- Materiales: 1 HardSurface con máscara emisiva. Labio y bandas comparten el canal emisivo.
- Luces: 0–1. Basta con 1 punto; en la Transfer, que ya tiene `CeilingLight`, puede valer 0 y dejar solo los emisivos.
- Colliders: solo el BoxCollider del root.

**Riesgos**
- **Atrapar al jugador**: es el mismo patrón vertical de hoy, pero la lama 0 cae la última y el golpe llega a 0,36 s. Hace falta la misma comprobación de solape que en D-A. Ventaja: el hueco se cierra de arriba abajo, así que el jugador ve venir el cierre.
- **Lectura**: buena. El labio y las bandas emisivas se ven a cualquier altura y las balizas giran. Es menos icónica que D-A: una persiana se lee como "garaje industrial" salvo que las lamas sean gruesas y estén escalonadas, como en V1.
- **Coste**: bajo. Solo hay traslaciones, el rig es simple y la misma pieza sirve para las 4 variantes y para las 2 puertas de persecución.
- **Encaje**: el mejor de las tres direcciones en V2, V3 y V4. En V1 es correcta, pero menos espectacular que D-A.

## 5. D-C Bóveda batiente

![](D-C/D-C_BovedaBatiente_lamina.png)

**Descripción.** Dos hojas de bóveda de grosor completo con una **junta diagonal** (inclinación de ±7 % del ancho) marcada por una rendija emisiva. Tienen bisagras de tres nudillos en las jambas, **brazos hidráulicos** (tubo y vástago) que van de la jamba a la hoja y **cerrojos verticales** que bajan del dintel a asientos sobre la cara de la hoja. Al abrir, los cerrojos suben, la hoja se despega 6 cm hacia la sala y **pivota 90°** sobre la bisagra vertical, hasta quedar perpendicular al muro como una aleta. En el suelo, unas marcas ámbar tenues señalan el arco de barrido.

**Variantes.** V1 y V2 tienen 2 hojas con un barrido de W/2 (4,2 m y 5 m). **V3 y V4 son plegables de 4 paneles** (dos por lado, en acordeón): el barrido baja a W/4 (2,1 m y 3 m) y el paquete abierto mide 2T de grosor.

**Secuencia por fases** (`D-C/D-C_BovedaBatiente_cronograma.png`):

| Fase | Tiempo | k | Qué se mueve (pivote) | Audio candidato |
|---|---|---|---|---|
| C1 Cerrojos | 0,00–0,20 s | 0–0,20 | Los 2 cerrojos del marco suben 0,7 m hacia el dintel. | `IR_Mount_Lock.wav` |
| C2 Sello | 0,20–0,28 s | 0,20–0,28 | Las hojas avanzan 6 cm hacia la sala. | `IR_Hangar_Hydraulic.wav` |
| C3 Giro | 0,28–1,00 s | 0,28–1 | Giro de 0–90° sobre Z (pivote: eje de la bisagra, en la arista de la cara de sala junto a la jamba). Los brazos del marco se reorientan con LookAt. En la plegable, el panel 2 gira −2θ respecto al panel 1 (pivote: arista común en la cara de esclusa). | `Assets/IronRequiem/Audio/SFX/HangarIntro/IR_Hangar_CannonArm.wav` (1,2 s) + `IR_Hangar_Servo.wav` |
| Fin | 1,00 s | 1 | El root se desactiva y **en el mismo frame se activa el proxy** de hojas aparcadas en el marco. | `IR_Airlock_Done.wav` |
| Cierre C1 | 0,00–0,35 s | — | El proxy se apaga en el mismo frame en que el root se activa. Giro inverso con ease-in. | `IR_Hangar_Servo.wav` |
| Cierre C2 | 0,35–0,38 s | — | Choque. | `IR_Fist_HeavyImpact.wav` |
| Cierre C3 | 0,38–0,45 s | — | Cerrojos abajo. | `IR_Mount_Clamp.wav` |

**Alargamiento: +0,30 s al abrir** (1,00 s). Un giro de 90° de una hoja de 4–5 m en menos de 0,7 s parece de cartón; para que pese necesita ~0,7 s de giro más 0,28 s de cerrojos y sello. Si se recorta a 0,7 s, el giro se queda en 0,42 s y parece un portazo.

**Restricciones técnicas para P2**
- Piezas móviles (hijas del root): 2 hojas en rotación Z, o 4 paneles en V3 y V4 (panel 1 θ, panel 2 −2θ relativo). Piezas del marco que se mueven: 2 cerrojos (traslación Z) y 2 brazos (orientación entre un ancla fija y un punto de la hoja).
- **Proxy obligatorio**: con la puerta abierta las hojas quedan a la vista, perpendiculares al muro, pero el root está inactivo. Hace falta una copia de las hojas en la pose abierta como hija del marco, activada y desactivada en el mismo frame que el root. Esto **duplica la malla** de la hoja y añade un estado más que sincronizar.
- **Espacio**: no sube nada al techo ni entra nada en los muros, pero hace falta un **arco libre en el suelo de la sala** de radio W/2 (W/4 en la plegable). Las aletas abiertas invaden la sala entre 2,1 y 5 m.
- **Colisión con la puerta abierta**: las aletas deberían ser sólidas (el proxy necesita collider), lo que **cambia la geometría de combate de la sala**. Como el NavMesh de cada sala se hornea solo con su escena (doors.md, "NavMesh"), los proxies de la sesión no estarían horneados: harían falta `NavMeshObstacle` con carving o volver a hornear.
- Triángulos: ver §6, más el proxy.
- Materiales y luces: como D-A.

**Riesgos**
- **Atrapar o empujar**: la puerta de origen se abre **hacia el jugador** en la sala recién completada y la de destino barre la sala nueva, donde aparecen los enemigos. Los colliders movidos por transform no empujan al CharacterController, así que las hojas lo atravesarían o lo atascarían. Es el riesgo más alto de las tres direcciones.
- **Lectura**: buena de frente (la diagonal emisiva es muy visible). Pero las aletas abiertas, iluminadas en verde, tapan el vano visto desde ángulos laterales.
- **Coste**: alto. El proxy, el NavMesh, la apertura más larga y los brazos con LookAt suben tanto la complejidad como el coste.
- **Encaje**: aceptable solo en V3 y V4 plegables y en espacios sin combate. Para V1 y V2 no es recomendable.

## 6. Estimaciones (triángulos, materiales, luces)

Recuento de la malla evaluada del boceto (`report.json`, sin muro, túnel, haces ni marcas de suelo). Incluye bevels de 1 segmento y remaches como cubos. Es orientativo: una malla final, con remaches en el normal map y piezas fusionadas, debería quedar en el mismo orden.

| Variante | D-A root / marco | D-B root / marco | D-C root / marco (el proxy suma lo mismo que el root) |
|---|---|---|---|
| V1 Esclusa principal | 1.432 / 1.759 | 852 / 1.467 | 520 / 1.671 |
| V2 Paso entre salas | 1.888 / 1.999 | 1.152 / 1.467 | 664 / 1.671 |
| V3 Persiana interior | 340 / 1.519 | 788 / 1.467 | 928 / 1.791 |
| V4 Salida de sala | 1.816 / 1.951 | 1.200 / 1.467 | 1.168 / 1.791 |

El marco incluye el rótulo de texto (~300 tris en Blender), que en juego sería un quad con atlas. El root de D-C sale bajo porque el boceto tiene hojas lisas; con el detalle de bóveda real subiría al orden de D-A.

Presupuesto propuesto por puerta para P2:

| | D-A | D-B | D-C |
|---|---|---|---|
| Root (hojas) LOD0 | 2.000–3.500 | 1.200–2.500 | 1.500–3.000 (+ igual en el proxy) |
| Root LOD1 (>25 m) | ~40 % (sin dientes biselados, cubo de 8 lados) | ~35 % (lamas planas sin nervio) | ~40 % |
| Root LOD2 (>60 m) | 2 cajas con dientes (≤150) | 1 caja (12) | 2 prismas (24) |
| Marco | 1.500–2.500 | 1.200–2.000 | 1.500–2.500 |
| Materiales | 1 HardSurface + emisivo por máscara; 1 aditivo de haces compartido por todas las puertas | igual | igual |
| Luces dinámicas | 1 (sin sombras) | 0–1 | 1 |
| Colliders | root Box + estáticos del marco | root Box + estáticos del marco | root Box + **proxy** (2 Box) + obstáculo NavMesh |
| Objetos que se animan | 4–6 hijos + 6 dogs | n lamas + 3 pasadores + 4 levas | 2–4 hijos + 2 cerrojos + 2 brazos + proxy |

Con 13 puertas en la sesión, de las que solo se ven 2–4 a la vez, cualquiera de las tres direcciones cabe en el presupuesto. El p99 de Hangar, Pasillo y Assembly ya supera 8,33 ms (auditoría §7), así que conviene no añadir sombras dinámicas ni luces giratorias reales.

## 7. Tabla comparativa de pros y contras y recomendación

| Criterio | D-A Mandíbula | D-B Persiana | D-C Bóveda |
|---|---|---|---|
| Identidad visual y fantasía robótica | **Muy alta**: dientes y cubo giratorio, al estilo de DOOM | Media-alta (con lamas gruesas y escalonadas) | Alta (cámara acorazada) |
| Cabe en 0,70 / 0,45 s | +0,10 s al abrir (o 0,7 s recortando el deslizamiento) | **Sí, exacto** | +0,30 s al abrir |
| Espacio que exige | Bolsillos laterales de 2,9–3,8 m por lado | **Solo h+0,5 m de muro sobre el vano, en la cara de la sala** | Arco libre en el suelo de 2,1–5 m |
| Respeta el techo de la esclusa | Sí | Sí (el casete está en la cara de la sala) | Sí |
| Pose final oculta (root inactivo sin saltos) | Sí (dentro de los casetes) | Sí (dentro del casete) | **No**: necesita proxy |
| Riesgo de atrapar o empujar | Medio (cierre lateral) | Medio (cierre vertical, igual que hoy) | **Alto** (barre la sala) |
| Impacto en NavMesh y combate | Ninguno | Ninguno | Aletas sólidas: carving u horneado |
| Lectura del estado a distancia | Muy buena | Buena (labio y bandas a toda altura) | Buena de frente y peor de lado |
| Coste de producción y rig | Medio-alto | **Bajo** | Alto |
| Reutilización en 13 puertas + persecución | Media (V2 y V3 no encajan bien) | **Total** (cambian n, ancho y grosor) | Baja |
| Tris del root (boceto V1) | 1.432 | 852 | 520 (+520 de proxy) |

**Recomendación razonada.**
1. **D-B Persiana telescópica para la familia completa** (V2 Transfer ×6, InitialLoadingDoor, V4 salidas de escalera y lobby, V3 vestíbulo y persecución). Es la única que cabe en los tiempos actuales sin tocar `OpenSeconds` ni `CloseSeconds`, no necesita bolsillos laterales que la Transfer no tiene, guarda sus piezas fuera de la esclusa (y así respeta los 0,95 m de techo), elimina el z-fighting de la escalera, no toca el NavMesh y es la más barata de construir y animar.
2. **D-A Mandíbula solo como puerta de prestigio** de la esclusa principal (V1), y solo si P2 confirma que caben casetes de 2,9 m en la cara de la sala. Esa esclusa está en zona protegida (`HangarTransitAuthoring.cs`, materiales HG_*, cinemática de Room00): como dice la auditoría, se haría con materiales copiados, sin tocar Room00 y tras verificar en Play el plano `Shot_C_Exit`. Si no se aprueba tocar a0, D-B cubre también V1.
3. **D-C queda descartada para puertas de combate.** Su versión plegable se podría reservar para algún decorado sin combate.

Pendiente de decisión del usuario: la dirección (o la mezcla B + A en V1); si a0 entra o no en el rediseño; si ABIERTA usa verde o el color temático de la sala; y si el rótulo va en atlas emisivo o en TMP.

## 8. Archivos y regeneración

| Archivo | Contenido |
|---|---|
| `door_lib.py` | Primitivas (cajas biseladas, prismas con pivote, cilindros, remaches), materiales (chapa con óxido, franjas de peligro procedurales, emisivos y alarma), marco común con estados, túnel, cámaras, rótulos, cronograma, recuento de tris y composición de láminas con numpy. |
| `doors_concepts.py` | Los 3 constructores (`build_A/B/C`), con fases parametrizadas por k, las 4 variantes y el runner. |
| `D-X/D-X_<Nombre>_V1_{blocked,unlock,half,open,alarm}.png` | Estados de la variante principal. |
| `D-X/D-X_<Nombre>_conjunto_{blocked,half}.png` | Las 4 variantes a escala, cerradas y a mitad de apertura. |
| `D-X/D-X_<Nombre>_cronograma.png` | Fases, tiempos y audio candidato. |
| `D-X/D-X_<Nombre>_lamina.png` | Lámina resumen de la dirección (8 vistas). |
| `report.json` | Tris y objetos por variante, separados en root y marco; fases; motor; post. |

```
cd Docs\IronRequiem\Concepts\Doors\Redesign_v01
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --factory-startup --python-exit-code 1 --python doors_concepts.py -- D-B
```

Sin IDs se regeneran las tres direcciones (~1–3 min en total). `_title.png` es un rótulo intermedio.

**Límites del boceto.** Son blockouts sin UV ni normal maps. El óxido y las franjas son procedurales y no reflejan el atlas final. El bloom es el Glare del compositor. Los haces de las balizas son conos semitransparentes congelados en un ángulo. La silueta de 1,8 m es solo una referencia de escala. No se probó nada en Unity: las restricciones de espacio citadas salen de las posiciones de doors.md y hay que verificarlas en el Editor durante P2.
