# Pase visual (chat 12) · F1 Dirección de arte → PARADA

Fecha: 21/11/2026. Es una propuesta: no se ha implementado nada. Todas las imágenes de las secciones a, b y c son **maquetas hechas dentro del motor en Play Mode**, con cambios solo en ejecución (nada guardado). Parten de los mismos puntos de vista de F0 ([00_DIAGNOSTICO.md](00_DIAGNOSTICO.md)).

Necesito tu elección en cinco puntos:

| Punto | Qué eliges | Mi recomendación |
|---|---|---|
| a) Luminosidad | Nivel 1 "+ legible" o nivel 2 "+ luminoso"; y si quieres linterna además | Nivel 1 como base, subiendo hacia el nivel 2 en los tramos altos; sin linterna con `Light` |
| b) Luces de robots | Qué correcciones entran | Las seis de la lista |
| c) Ojos rojos | Opción A (estado por intensidad y pulso) u opción B (ojos rojos + indicador corporal) | Opción A |
| d) Decoración | Cómo resolver la contradicción subida/bajada y el concepto por zona | Ver [01d_DECORACION.md](01d_DECORACION.md) |
| e) Carroñero | Alcance "mínimo que se nota" o "completo" | Ver [01e_CARRONERO.md](01e_CARRONERO.md) |

Skills usadas en a, b y c: `unity:urp-postprocessing` (exposición por Volume con `overrideState`, bloom y HDR), `gamedev:shader-programming` (el rim de Fresnel alcanza su máximo en la silueta: por eso un rim blanco se lee como contorno de neón), `gamedev:fps-shooter` (los enemigos necesitan telegrafía y un retardo de reacción legible; la lectura no debe depender de gestionar un interruptor), `iron-requiem-performance` (presupuesto de luces y medir antes de afirmar) e `iron-requiem-enemy-ai` (no se toca la IA: solo se lee su estado). No cargué `gamedev:performance-optimization`, `gamedev:unity-input-system`, `gamedev:input-systems` ni `tune-enemy-ai`: harán falta en F2 y F3, no para esta propuesta.

---

## a) Luminosidad

### Los dos niveles

Los dos usan las mismas palancas, que ya están dirigidas por datos (`Levels/Map01/Data/Lighting/R*.json` y `LightingAuthoring`), así que se reaplican sin tocar escenas a mano. **No añaden ninguna luz:** suben las que hay, de modo que el presupuesto de 8 por cuarto no cambia.

| Palanca | Hoy | Nivel 1 "+ legible" | Nivel 2 "+ luminoso" | Dónde vive |
|---|---|---|---|---|
| Ambiente Trilight (cielo, ecuador y suelo) | ×1 | **×2,2** | **×3,2** | JSON de cada cuarto, `environment` |
| Intensidad de las luces del cuarto (relleno) | ×1 (I 12 típica) | **×1,5** | **×2,0** | JSON, `lights[].intensity` |
| Alcance de las luces | ×1 (12 m típico) | **×1,25** | **×1,4** | JSON, `lights[].range` |
| Exposición | 0 EV (no hay) | **+0,6 EV** | **+1,0 EV** | Un Volume nuevo de Map01, prioridad 5, con `ColorAdjustments.postExposure`. **No se edita `Campaign_PostProfile`**, que también forma parte de la pila del hangar protegido |
| Densidad de niebla | ×1 (0,007–0,016) | ×0,8 | ×0,65 | JSON, `environment.fog` |
| Tonemapping, bloom, grano, viñeta | — | sin cambio | sin cambio | — |

### Resultado medido (fondo medio de la imagen, 0–255; objetivo de `05_ILUMINACION` ≥ 45 en carriles)

| Sala | Hoy | Nivel 1 | Nivel 2 | Hoy + linterna | Nivel 1 + linterna |
|---|---|---|---|---|---|
| R01 Control de acceso | 12 | **41** | **79** | 19 | 46 |
| R03 Dormitorios | 10 | 38 | 77 | 19 | 48 |
| R07 Comunicaciones | 12 | 37 | 70 | 17 | 42 |
| R11 Decantadores | 8 | 28 | 60 | 39 | 62 |
| R14 Fundición | 9 | 27 | 54 | 17 | 36 |
| R17 Centro de mando | 6 | 21 | 45 | 13 | 30 |
| R18 Bóveda | 3 | 29 | 68 | 8 | 33 |

Píxeles casi negros (< 10/255): hoy 56–98 %; nivel 1, 2–37 %; nivel 2, 0–3 %. Datos completos en [F1/lum.csv](F1/lum.csv).

| | Hoy | Nivel 1 "+ legible" | Nivel 2 "+ luminoso" |
|---|---|---|---|
| **R01** | ![](F1/lum/Map01_R01_ControlAcceso_0_actual_T07.jpg) | ![](F1/lum/Map01_R01_ControlAcceso_1_legible_T07.jpg) | ![](F1/lum/Map01_R01_ControlAcceso_2_luminoso_T07.jpg) |
| **R11** | ![](F1/lum/Map01_R11_Decantadores_0_actual_T07.jpg) | ![](F1/lum/Map01_R11_Decantadores_1_legible_T07.jpg) | ![](F1/lum/Map01_R11_Decantadores_2_luminoso_T07.jpg) |
| **R18** | ![](F1/lum/Map01_R18_Boveda_0_actual_T07.jpg) | ![](F1/lum/Map01_R18_Boveda_1_legible_T07.jpg) | ![](F1/lum/Map01_R18_Boveda_2_luminoso_T07.jpg) |

Lectura:
- **Nivel 1** deja ver suelo, paredes y coberturas y conserva la identidad de cada zona (cálido en Acceso, verde azulado en Agua, frío en la Bóveda). Se queda algo por debajo del objetivo de 45 en las salas más oscuras (R17: 21).
- **Nivel 2** cumple el objetivo en todas, pero las salas cálidas quedan muy claras (R01: 79) y se pierde la sensación de búnker.
- Los niveles son uniformes en esta maqueta. En F2 se afinan por cuarto.

### Progresión de la subida (enlaza con d)

Propongo no usar un único nivel, sino una rampa que cuente la subida, con un mínimo que ninguna zona incumple:

| Tramo | Fondo objetivo en carriles | Cómo | Carácter de la luz |
|---|---|---|---|
| 1. Profundidades (laboratorio) | 30–35 | Nivel 1 | Artificial de emergencia: fría, puntual, con zonas de sombra |
| 2. Infraestructura | 38–45 | Nivel 1 con relleno ×1,7 | Industrial: sodio y fluorescente averiado |
| 3. Cerca de la superficie | 48–58 | Entre nivel 1 y nivel 2 | Aparece luz natural fría que entra por grietas, además de la artificial |
| Mínimo absoluto | 28 en oscuridad intencional, 45 en spawns | — | Como ya pide `05_ILUMINACION` §L1.3 |

Dos avisos sobre estas cifras:
- Mis números son la **media de toda la imagen**, que incluye techos y rincones; el objetivo de 45 de `05_ILUMINACION` se refiere a carriles y spawns. No son la misma medida, y la de carriles está por hacer.
- El esquema [concepts/ruta_subida.png](concepts/ruta_subida.png) del director de arte dibuja unos objetivos de 46 a 72 que son indicativos y más altos que los de esta tabla. Los definitivos se fijan por cuarto en F2, según el nivel que elijas.

### Linterna

- **Ya está implementada la opción C** del chat 11 (lectura pasiva). El prompt 12 decía que lo sustituía, pero el 11 se ejecutó el 20/11.
- Maqueta de linterna con `Light` (foco de 64°, alcance 30 m, intensidad 90, sin sombras, en el hombro): sube poco la escena salvo donde hay superficies cerca (R11: de 8 a 39). Con el nivel 1 aporta entre 2 y 8 puntos más en la mayoría de salas.
- Costes que midió el chat 11 y siguen vigentes: nueve cuartos pasarían de 8 a 9 luces; URP limita a 4 luces por objeto, así que algunas superficies perderían una luz de sala dentro del cono; y con las Light Layers apagadas ilumina también las manos y el arma (activarlas es tocar el pipeline).

| Hoy + linterna (R11) | Nivel 1 + linterna (R11) |
|---|---|
| ![](F1/lum/Map01_R11_Decantadores_3_actual_linterna_T07.jpg) | ![](F1/lum/Map01_R11_Decantadores_4_legible_linterna_T07.jpg) |

Opciones:
1. **Sin linterna; nivel 1 + resaltado C corregido** (recomendada). La luz de sala hace el trabajo y el resaltado queda como red de seguridad en los rincones.
2. Nivel 1 + linterna de hombro con tecla (T libre en teclado; en mando no hay botón libre). Añade un interruptor que gestionar en un FPS rápido y cuesta presupuesto de luces.
3. Nivel 1 + pulso (opción B del chat 11): un barrido corto que marca enemigos, sin `Light`.

---

## b) Luces de robots: qué se corrige

Lo "raro" está catalogado en F0 §3. Propuesta de corrección, por orden de impacto:

| # | Problema hoy | Corrección | Cómo (sin tocar materiales compartidos) |
|---|---|---|---|
| 1 | Contorno **blanco** del resaltado: el robot parece un negativo | Bajar su intensidad a la mitad, teñirlo de azul acero en vez de blanco y activarlo solo donde el fondo esté por debajo del umbral. Con el nivel 1 se apaga solo en casi todo el mapa | Parámetros de `VisionMaterialRules` y umbrales de `EnemyDarkReadability` |
| 2 | Ojos de 3–5 cm sin brillo | Núcleo emisivo de 10–14 cm con halo suave y tope de HDR 4 (el bloom ya tiene clamp 12 y StopNaN) | Un quad con textura de núcleo + halo por ojo, instanciado, o variante de material nueva |
| 3 | El punto rojo del Carroñero flota fuera de la malla | Anclarlo al hueso de la cabeza, donde va el ojo | Solo en `Carronero_Map01` (combate); el del hangar no cambia |
| 4 | El BigBot escribe los estados en una emisión apagada | Variante nueva de `SharedIndicatorLens` con `_EMISSION` activo, solo para el BigBot | Material nuevo; el compartido no se toca |
| 5 | Cada robot habla un idioma distinto | Un único lenguaje de estado para todos (el que elijas en c), incluidos torreta y Carroñero | `EnemyStateLights` o componente equivalente ligero para hordas |
| 6 | Aviso de disparo a 14 Hz | Dos destellos claros en los 0,35 s previos al disparo, en lugar de un estrobo | Parámetro `blinkHz` y forma del pulso |

Antes y después (nivel 1, opción A en ataque):

| | Hoy | Después |
|---|---|---|
| **T-07** | ![](F1/ojos/T07_Current_Repaired_1_legible_0_actual_5m.jpg) | ![](F1/ojos/T07_Current_Repaired_1_legible_A_ataque_5m.jpg) |
| **Carroñero** | ![](F1/ojos/Carronero_1_legible_0_actual_5m.jpg) | ![](F1/ojos/Carronero_1_legible_A_ataque_5m.jpg) |
| **BigBot** | ![](F1/ojos/BigBot_Current_1_legible_0_actual_5m.jpg) | ![](F1/ojos/BigBot_Current_1_legible_A_ataque_5m.jpg) |
| **SawBot** | ![](F1/ojos/SawBot_Current_1_legible_0_actual_5m.jpg) | ![](F1/ojos/SawBot_Current_1_legible_A_ataque_5m.jpg) |

La maqueta no incluye la corrección 1 (el contorno sigue blanco en estas imágenes) ni la 3: son los quads de ojo añadidos en ejecución sobre el robot actual.

---

## c) Ojos rojos sin perder la lectura del estado

Hoy el color indica el estado (cian patrulla, ámbar alerta, rojo ataque), aunque solo se ve en el HoverBot. Dos opciones:

| | **Opción A: ojos siempre rojos; el estado va por intensidad y pulso** | **Opción B: ojos rojos + indicador corporal con el color de estado** |
|---|---|---|
| Patrulla | Rojo tenue y fijo (HDR 1,6; halo pequeño) | Ojo rojo fijo (HDR 2,6) + barra en el pecho **cian** |
| Alerta | Rojo medio con pulso lento, 1,5 Hz (HDR 2,6) | Ojo igual + barra **blanca** |
| Ataque | Rojo máximo y fijo (HDR 4; halo grande) | Ojo igual + barra **magenta** |
| Aviso de disparo | Dos destellos a blanco en 0,35 s | La barra parpadea dos veces |
| Muerte | Se apaga | Se apagan los dos |
| A favor | Cumple "luz roja" sin añadir colores ni piezas. Un solo elemento por robot: barato en hordas. La amenaza se lee como "más rojo = más peligro" | El estado se distingue por color, también de lejos y sin fijarse en la intensidad |
| En contra | Distinguir tenue de medio a 25 m es más difícil; depende del pulso | Más elementos y un color nuevo por robot; hay que encontrar sitio en cada malla (en el HoverBot y la torreta casi no lo hay); más draw calls en hordas |

| | Patrulla | Alerta | Ataque |
|---|---|---|---|
| **A, T-07** | ![](F1/ojos/T07_Current_Repaired_1_legible_A_patrulla_5m.jpg) | — | ![](F1/ojos/T07_Current_Repaired_1_legible_A_ataque_5m.jpg) |
| **B, T-07** | ![](F1/ojos/T07_Current_Repaired_1_legible_B_patrulla_5m.jpg) | — | ![](F1/ojos/T07_Current_Repaired_1_legible_B_ataque_5m.jpg) |
| **A, BigBot** | ![](F1/ojos/BigBot_Current_1_legible_A_patrulla_5m.jpg) | — | ![](F1/ojos/BigBot_Current_1_legible_A_ataque_5m.jpg) |
| **B, BigBot** | ![](F1/ojos/BigBot_Current_1_legible_B_patrulla_5m.jpg) | — | ![](F1/ojos/BigBot_Current_1_legible_B_ataque_5m.jpg) |
| **A, HoverBot** | ![](F1/ojos/HoverBot_Current_1_legible_A_patrulla_5m.jpg) | — | ![](F1/ojos/HoverBot_Current_1_legible_A_ataque_5m.jpg) |
| **B, HoverBot** | ![](F1/ojos/HoverBot_Current_1_legible_B_patrulla_5m.jpg) | — | ![](F1/ojos/HoverBot_Current_1_legible_B_ataque_5m.jpg) |

A 15 m, sala actual y nivel 1:

| | Hoy | Opción A, ataque | Opción B, ataque |
|---|---|---|---|
| Sala actual | ![](F1/ojos/T07_Current_Repaired_0_actual_0_actual_15m.jpg) | ![](F1/ojos/T07_Current_Repaired_0_actual_A_ataque_15m.jpg) | ![](F1/ojos/T07_Current_Repaired_0_actual_B_ataque_15m.jpg) |
| Nivel 1 | ![](F1/ojos/T07_Current_Repaired_1_legible_0_actual_15m.jpg) | ![](F1/ojos/T07_Current_Repaired_1_legible_A_ataque_15m.jpg) | ![](F1/ojos/T07_Current_Repaired_1_legible_B_ataque_15m.jpg) |

Reglas comunes a las dos opciones:
- **Sin `Light` dinámica por robot.** Emisivo con tope de HDR 4 y halo en un quad. Como mucho, una luz real en el BigBot y en el jefe, si el presupuesto del cuarto lo permite (hoy nueve cuartos están en 8 de 8, así que habría que quitar una luz de sala).
- **Que no se confunda con las puertas.** El rojo de BLOQUEADA es (1; 0,03; 0,02) en barras y balizas fijas. Los ojos serían un punto pequeño y redondo, a la altura de la cabeza, que se mueve con el robot, con un rojo algo más carmesí (1; 0,05; 0,12) y sin el patrón de baliza giratoria.
- **Dato a tener en cuenta:** el rojo tiene poca luminancia. En la maqueta, cambiar el ojo cian del HoverBot por rojo baja su contraste de 1,98 a 1,01–1,64. Por eso el ojo rojo necesita núcleo claro y halo, no solo cambiar el color.
- **El ojo se apaga al morir** y no hay resplandor gigante: en las capturas de la maqueta a 15 m, el brillo fuera de la silueta fue como mucho de 11 píxeles.
- En el BigBot el rojo ya domina la armadura: sus seis indicadores rojos se pierden. En él conviene un único "ojo" central más grande, o la opción B.

Mi recomendación es la **A**, con una excepción a valorar: indicador corporal solo en el BigBot.

---

## d) Decoración y e) Carroñero

Los desarrolló el director de arte (subagente) en documentos aparte:

- [01d_DECORACION.md](01d_DECORACION.md): la contradicción subida/bajada con sus opciones, la re-tematización de los 18 cuartos, landmarks, paleta, señales del exterior por tramo, kit de props y presupuesto. Imágenes en [concepts/](concepts/).
- [01e_CARRONERO.md](01e_CARRONERO.md): lista priorizada de animaciones con tiempos, huesos, cómo se mide cada una y dos niveles de alcance.

---

## Preguntas abiertas que salen de d y e

1. **Dirección del mapa:** ¿O1 (invertir cotas: 12 cuartos solo cambian de signo, 6 hay que recorrerlos al revés y revalidar), O2+ (re-tematizar e invertir solo R02) u O3 (mantener el descenso)? Es trabajo del chat 9.
2. **¿El hangar es el fondo del búnker?** Es donde despierta el jugador y es zona protegida; hoy los documentos lo sitúan en superficie.
3. **¿R16 deja de ser Reactor** y pasa a toma de aire y filtrado, para que el tramo alto tenga señales del exterior?
4. **¿La compuerta de salida de R18 puede ser solo decoración?** R18 no tiene salida en los datos y la campaña termina en el jefe.
5. **Muerte del Carroñero:** hoy estalla sin retardo. Para que se lea una pose antes hace falta un retardo de ≈ 0,16 s solo en la variante de combate. ¿Lo quieres?
6. **¿La animación nueva del Carroñero va solo en Map01** (`Carronero_Map01.prefab`) o también en las salas antiguas, que usan `Carronero.prefab` directamente (el mismo del hangar protegido)?
7. Colores a revisar si los ojos pasan a rojo: el radar rojo de R07 y la luz verde de las bombas de R13, casi igual al verde de puerta abierta.

## Qué pasa después de tu elección

| Fase | Entra | Archivos |
|---|---|---|
| F2 | El nivel elegido en a y, si la quieres, la linterna | `Levels/Map01/Data/Lighting/*.json`, `Editor/Map01/LightingAuthoring.cs`, Volume nuevo de Map01, `Scripts/Player/Vision/` |
| F3 | b y c | `Scripts/VisualOverhaul/`, variantes nuevas de materiales de enemigos |
| F4 | d, una zona cada vez | `Editor/Map01/` y `Prefabs/Map01/`. **Depende de la decisión sobre la dirección del mapa y de coordinar con el chat 9** |
| F5 | e | `Scripts/Enemies/` del Carroñero y sus tests, solo en prefabs de combate |
| F6 | Verificación y revisor independiente | — |
