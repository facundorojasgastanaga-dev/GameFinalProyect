# MAPA 1 · 05 · Iluminación (R5)

**Estado:** F1, plan en papel. Autor: R5 (sonnet), 18/11/2026. Nada se ha construido, horneado ni medido.

**Alcance:** solo lectura del proyecto; único archivo escrito: este. No se abrió Unity, no se usó la CLI, ni git que escriba, ni red, ni Meshy/Tripo/Composio.

**Fuentes y abreviaturas.** Cada dato del proyecto lleva su fuente; lo supuesto lleva **[SUPUESTO]**; lo que decide el usuario, **[DECISIÓN USUARIO]** con recomendación.

| Sigla | Documento o código |
|---|---|
| [MD] | `MAP01_DESIGN.md` (§3 restricciones, §4 capas, §5.3 y §5.5 contratos) |
| [E] | `01_ESTRUCTURA.md` (§3 zonas, §6 tabla, §7 fichas, §9 jefe, §12 datos) |
| [INV] | `00_INVENTARIO.md` (§5 presupuesto; anexos A4, C, E) |
| [R2A] | `MAP_FX_AUDIT_20261117_r2.md` (§5 post e IR-03) |
| [RP] | `Tools/IronRequiem/Reports/MapFxAudit2/render-perf.md` |
| [VA] | `VISUAL_AUDIT_F0.md` |
| [VO] | `VISUAL_OVERHAUL.md` (§3 mapa, §6 rendimiento) |
| [BP] | `BUNKER_POLISH.md` §5 |
| [ENC] | `03_ENCUENTROS.md` (A6.3 alarma, A7 jefe) |
| [PU] | `04_PUERTAS.md` (P1.0, P2, P4) |
| [ZE] `[SLB]` `[VFI]` `[CA]` `[AC]` `[LF]` `[ESL]` `[BFL]` `[CF]` | Código: `ZoneEnvironment.cs`, `SectorLightingBlend.cs`, `VolumeFadeIn.cs`, `CorridorAlarm.cs`, `AirlockCycle.cs`, `LightFlicker.cs`, `EnemyStateLights.cs`, `BurstFlashLight.cs`, `CampaignFlow.cs` |

---

## L0. Resumen y decisiones

1. **Estrategia técnica: todo en tiempo real, como hoy** (`m_Lightmapping: 4` en todas las escenas de campaña; sin LightingData [INV E1]). Sin lightmaps ni light probes en F2: los enemigos son dinámicos, los modelos no garantizan UV2 y [VO §3] ya decidió no generarlos. Lo único horneado son las **reflection probes** (≤ 2 por cuarto).
2. **Parque de luces:** 134 luminarias con `Light` en los cuartos y S00 (7–8 por cuarto como máximo, 5 como mínimo) y 17 rellenos de esclusa: **151 en todo el mapa, nunca más de 8 del cuarto visibles a la vez** (+1 relleno de esclusa y, aparte, las luces de estado de R4).
3. **Sombras:** solo luces **spot** (un punto de luz con sombra gasta 6 caras del atlas). 8 luces con sombra en todo el mapa (R02, R06, R07 solo en alarma, R08, R14, R16 y 2 en R18), **≤ 1 por cuarto salvo R18 (2)**. Empiezan activas solo R08, R14 y R18; el resto se activa si la medición de F2 lo permite.
4. **Identidad por zona:** 12 paletas (§L2) con ambiente trilight, niebla exp² y un Volume de zona (solo ColorAdjustments + WhiteBalance, más Bloom y grano contenidos) sobre `Campaign_PostProfile`. La esclusa de sesión es el «gris neutro» de transición (así lo hace `CampaignFlow`), con un relleno cromático por esclusa.
5. **Legibilidad:** jerarquía salida > landmark > ruta > ambiente; el ambiente nunca baja de ≈ 40 % de la ruta; toda zona de spawn, de caída de armas y de botiquín está iluminada; **las luces de salida son blancas** (R4 reserva rojo, ámbar y verde en los marcos).
6. **Estados** (§L4): normal, combate, alarma (R07 con `CorridorAlarm` sin cambios), ronda terminada, 4 apagones con suelo de legibilidad, secuencia de encendido de la Bóveda y fases del jefe para A, B y C. Hacen falta **5 componentes nuevos** de solo observación.
7. **IR-03:** no se añade desenfoque, profundidad de campo, aberración, lens flare ni halos. Bloom con umbral ≥ 1,0, intensidad ≤ 0,2 y clamp ≤ 12; grano ≤ 0,2; niebla ≤ 0,016 (≤ 0,010 en arenas grandes). Se verifica en 1920×1080 exclusiva, no a 1600×900.
8. **Conflictos con [E] (§L9.2):** 3 anclajes caen dentro o rozan el volumen libre de una puerta (R02, R06, R08), las alas de R07 no tienen anclajes, los anclajes carecen de ID, el «núcleo con sombra» de R16 es una luz puntual y la luz de la persecución (R10) depende de emisivos.

### [DECISIÓN USUARIO]

| # | Decisión | Opciones | Recomendación de R5 |
|---|---|---|---|
| **D-L1** | Estrategia de luz | (a) Todo en tiempo real, reflection probes horneadas, sin light probes. (b) Luces Mixed en *Baked Indirect* + light probes para el rebote en enemigos, sin lightmaps (como el laboratorio VO). (c) Lightmaps de los estáticos: exige UV2 y variantes `LIGHTMAP_ON` en los 226 cubos y los kits | **(a).** El juego está limitado por CPU y la GPU va sobrada [INV §5]; las light probes cuestan ≈ 0,13 ms por objeto muestreado y las reflection probes ≈ 0,30 ms [INV E3 #6]. El rebote se imita con el ambiente trilight (suelo cálido). (b) solo como prueba A/B en R14, R16 y R18 en F5. (c) descartada: coste de F2 alto y los enemigos ni lo usarían |
| **D-L2** | Sombras | (a) Ninguna salvo R18. (b) 1 spot en R08 y R14 y 2 en R18 desde F2, y 1 más en R02, R06, R07 (solo en alarma) y R16 si F2 mide < 0,5 ms por cuarto. (c) 2 por cuarto en todos | **(b), condicionada a la medición.** Hoy ninguna sala de campaña tiene sombras en luces adicionales y su coste en Release no está medido [INV E3 #11] |
| **D-L3** | Apagones dramáticos | (a) Ninguno. (b) 4: R07 última ronda, R10 al arrancar la persecución, R16 en la ronda 3 y R18 en la fase 3 del jefe, con ambiente ≥ 50 % y guías emisivas. (c) Más (R12, R13, R17) | **(b).** Cada uno tiene una razón de ritmo (§L4.5). Ninguno dura más de 20 s ni empieza en la primera ronda de un cuarto |
| **D-L4** | Nitidez (IR-03): grano de 0,3 a 0,18 y viñeta sin tinte morado en los Volumes de zona de Map01 | Sí / No | **Sí.** No toca `Campaign_PostProfile`: es un override de zona. Se valida con A/B en 1080 exclusiva (§L5.7). Si el usuario prefiere el grano de hoy, no cambia nada más |
| **D-L5** | `Map01_Campaign` sin `SectorLightingBlend` y sin `LabSun` | Sí / No | **Sí.** `CampaignFlow` ya mezcla ambiente y niebla con `ZoneEnvironment` (1,6 s); el segundo mezclador y un sol global que ilumina todas las salas por igual reabren el defecto de ambiente plano [VA 1] (§L9, riesgo 1) |
| **D-L6** | Luces de estado de puerta (R4) fuera de las 8 del cuarto | Sí, hasta 2 visibles con alcance ≤ 5 m y apagadas en reposo / No, restan de las 8 | **Sí** y coordinado con R0 y R4: [E §7] ya cuenta «1 en la salida» como anclaje de R5, y las `Light` de `Status` de R4 [PU P1.0] son otra cosa |
| **D-L7** | Concepto de jefe | A, B o C (§L4.8) | Mi plan vale para los tres; la luz de la Bóveda se cierra con el elegido |

---

## L1. Principios de legibilidad

### L1.1 Jerarquía de la luz

La luz decide a dónde mira el jugador. Niveles, de más a menos, medidos como **valor sRGB (0–255) de una tarjeta gris del 18 %** tras el tonemapping ACES, vista de frente en la cota de combate. Son objetivos de partida **[SUPUESTO]** que F2 calibra con capturas; el validador no puede medirlos.

| Nivel | Qué es | Objetivo | Cómo se logra |
|---|---|---|---|
| 1 | **Salida y puerta del avance** | 150–190, el punto más luminoso de la sala una vez cumplida la ronda | Foco de umbral **blanco** (spot de 80° sobre el dintel, anclaje «salida» de [E]); en combate baja al 25–35 % y al cumplirse la ronda sube al 100 % en 0,8 s. El verde de la puerta es de R4 |
| 2 | **Landmark** | 120–170 en su cara visible, ≈ 2× la ruta | Un foco propio (spot o puntual de acento), color de zona saturado y distinto del ambiente; visible desde el pórtico de entrada (LV-5 de [E]) |
| 3 | **Ruta crítica y carriles de combate** | 70–110 | Charcos de las luminarias de techo, con alcance tal que cada carril tiene ≥ 2 luces |
| 4 | **Ambiente, rincones** | ≥ 40 % de la ruta (≈ 45–60); la oscuridad intencional baja hasta 28 | Ambiente trilight; solo se baja de 45 en las zonas de §L1.4 |

### L1.2 Legibilidad de los enemigos

Datos: los ojos de `EnemyStateLights` son emisivos HDR por estado: Patrulla cian (0,1; 1,4; 1,2), Alerta ámbar (2,2; 1,1; 0,08), Ataque rojo (2,8; 0,12; 0,05), Aviso blanco caliente (5; 3,6; 3) [ESL]. No son `Light`, no cuentan para el límite. [VA 7] documenta enemigos «poco legibles a media distancia» sobre fondo plano.

1. **Fondo más claro que el robot.** El enemigo es metal oscuro; se lee como silueta contra un fondo de valor ≥ 1,5× el suyo. Por eso: niebla de color medio (no casi negra; §L2), suelos y tramo bajo de muro con valor 60–110, y una **banda de lectura** (§L7, `LUM-07` en tenue) a 2,5–3,5 m en los muros de fondo de cada arena.
2. **Contraluz y rim.** Luz cálida desde arriba y relleno **frío** lateral (equator del trilight): el contorno de los robots queda frío frente a una luz de techo cálida y al revés. `HardSurface` tiene el keyword `_RIM` (borde fresnel, `_RimPower` 0,5–8) [INV A4.1]; **no se verificó** si los materiales de los enemigos lo activan. **[VALIDAR F2]**: si están apagados, es un cambio de material de enemigos (fuera de mi archivo): se propone al usuario como mejora aparte y no se asume.
3. **Ojos contra el color de la zona.** Conflictos previsibles y qué se hace:

| Zona | Ojo que se pierde | Mitigación |
|---|---|---|
| Acceso, Armería, Almacenes, Fundición (ámbar) | Alerta (ámbar) | Relleno y ambiente neutros-fríos (la luz clave es ámbar pero el equator no); saturación −4 a −6 en el Volume. El ámbar HDR de 2,2 supera con holgura al ámbar de lámpara (≤ 1,0) |
| Agua, Reactor, Ensamblaje (cian) | Patrulla (cian) | Aceptado: la patrulla es el estado menos urgente. Alerta y Ataque se mantienen. El cian de ambiente se mantiene a saturación baja (≤ 0,45) |
| R07 en alarma, R10, R18 con fases (rojo) | Ataque (rojo) | La luz roja de alarma es de **saturación media y pulsada** (no fija) y va en los tabiques y el mástil, no a la altura de los ojos de los robots. El rojo de los ojos tiene 2,8 de HDR, ≥ 2,5× el de la lámpara. **[VALIDAR F2]** con una captura de R07 en alarma |

4. **Sin siluetas en negro.** Regla: ningún punto de la zona de combate (carriles, rangos de spawn, volúmenes de recogida de armas) por debajo de 45. Se valida colocando tarjetas grises en F2 (§L8, validador de cobertura de spawn).
5. **Dos efectos más ya existentes que no se tocan:** el viewmodel y las armas robadas brillan en HDR; el límite de 2 luces de efectos transitorias (`BurstFlashLight.MaxLit = 2`) se respeta [BFL].

### L1.3 Niveles mínimo y máximo de luminancia

| Magnitud | Mínimo | Máximo | Nota |
|---|---|---|---|
| Carril de combate, rango de spawn, recogida de armas, botiquín | 45 | 130 | Medido con tarjeta gris del 18 % |
| Bajo una luminaria de nave | — | 170 | Más alto quema el bloom del emisivo |
| Emisivos en pantalla | — | HDR ≤ 4,0 (lava ≤ 5,0) | Con umbral de bloom 1,0 y clamp 12 el halo no tapa a los enemigos (corrige C1 de [VA]) |
| Fogonazos de armas y explosiones | — | Los de hoy | No se tocan |
| Contraste entre charco y sombra | 2,5 : 1 (lineal) | 4 : 1 | Fuera de ese rango el cuarto se lee plano o con huecos negros |

### L1.4 Oscuridad intencional (catálogo cerrado)

Toda oscuridad que no esté en esta tabla es un defecto. Ninguna contiene spawn, botiquín, punto de recogida ni ruta crítica.

| Dónde | Qué | Por qué | Valor mínimo |
|---|---|---|---|
| R03, naves laterales | 2 tubos que fallan (parpadeo, nunca apagados > 0,4 s seguidos) | Identidad del Habitáculo | 45 |
| R10, más allá de 60 m de la sierra | Fondo del túnel | Tensión de la persecución; la niebla y las franjas emisivas la guían | 28 |
| R12, borde del foso | Brillo ámbar de abajo y cascada | Peligro: la caída se lee por luz | 28 (el brillo da la lectura) |
| R17 | Ambiente tenue (respiro) | Calma antes del jefe; las pantallas y la mesa son la luz | 45 |
| R18, antecámara hasta que abre B | Arena apagada y antecámara tenue | Revelado teatral (§L4.7) | 45 en la antecámara |
| Apagones de §L4.5 | Ambiente al 50 % | Evento dramático | 45 en el carril, con guías |

### L1.5 Peligros y caídas

Las caídas ≥ 3 m (R02, R06, R08, R10, R12, R16) llevan en el borde una **tira emisiva ámbar tenue** (`LUM-07`) y las grietas de R10 y el foso de R12 un brillo de abajo (anclajes de [E]). Se suma al lenguaje de R4: rojo, ámbar y verde saturados quedan **reservados a estado de puerta** a menos de 6 m de un marco [PU P2.1]; a menos de 6 m solo se usa blanco, blanco cálido o el tono de zona desaturado.

---

## L2. Paleta por zona

### L2.1 Reglas comunes

- **Color de luz** en el selector de Unity (sRGB/gamma). Kelvin como guía.
- **Ambiente:** `AmbientMode.Trilight` en todos los cuartos (modo 1, como hoy en `Room01_Foundry` y `RequiemCampaign`). `SectorLightingBlend` también fuerza Trilight al mezclar [SLB]. Sky = tono de zona tenue; equator = neutro-frío; ground = rebote cálido oscuro. Intensidad 1,0; `reflectionIntensity` 1,1.
- **Niebla:** `FogMode.ExponentialSquared` (modo 3, como hoy). `ZoneEnvironment.Apply` toma el modo del destino y no interpola el modo [ZE]; mantener el mismo modo en todo Map01 evita saltos. Densidad ≤ 0,016. Transmitancia = exp(−(d·ρ)²): con ρ = 0,012 queda 0,79 a 30 m, 0,43 a 60 m y 0,24 a 100 m.
- **Color de niebla:** de valor medio (12–20 %), no casi negro como hoy (`RequiemCampaign`: 0,06; 0,065; 0,075): sirve de telón en el que los robots oscuros se recortan (§L1.2).
- **Volume de zona** (un asset por zona, `Sector_M01_<Zona>`, prioridad 10 como los `Sector_*` actuales [R2A §5]): `ColorAdjustments` (postExposure, contrast, saturation, colorFilter), `WhiteBalance` (temperature, tint), `Bloom` (umbral, intensidad, scatter, clamp), `FilmGrain` e `Vignette` (color neutro). **Tonemapping no se toca** (ACES de `Campaign_PostProfile`; es un enum y no se interpola [R2A §5]). Sin DoF, motion blur, aberración, lens distortion ni Panini.
- **Transición** con `CampaignFlow`: sale la zona vieja hasta el ambiente neutro de la esclusa de sesión y entra la nueva en `environmentBlendSeconds` = 1,6 s (`ZoneEnvironment.Apply` + `SceneFade.Set`) [CF:105-110, 197-220, 278-282]. Los Volumes globales de cada escena entran por `SceneFade.Bind`, que recoge los `Volume` con `isGlobal`; un `VolumeFadeIn` en el Volume de zona evita el salto de carga y `SceneFade` lo «reclama» [VFI]. Cada cuarto de la misma zona usa el **mismo asset** de perfil: la mezcla vuelve a pasar por el neutro de la esclusa.
- **Esclusa = gris neutro de sesión.** El ambiente de la sesión (hoy sky 0,27; 0,28; 0,31, equator 0,19; 0,20; 0,22, niebla 0,06; 0,065; 0,075 de densidad 0,016) es el punto intermedio de todo cambio de zona [CF:183, 197]. Se conserva, salvo subir el color de niebla a (0,09; 0,095; 0,105) para que el paso no sea más oscuro que las salas **[DECISIÓN en F2, no cambia nada por defecto]**.
- **Valores = punto de partida [SUPUESTO]**: calibrar en F2 con capturas del Editor y luego en Release.
- **Intensidades.** Regla de cálculo: `I ≈ 0,20·h²` para luces de nave (h = altura de la luz sobre el suelo de combate), `0,15·h²` en respiros y `0,07·h²` en R17. Referencias del proyecto: lámpara fría del laboratorio I = 9,5, alcance 12,5 [VO §3, 9,5/12,5 en `MainScene_VisualLab`]; ámbar de `Room01_Foundry` de campaña I = 6, alcance 32, color (1; 0,66; 0,42) (más débil que la regla; de ahí el ambiente plano de [VA]). Alcance `r ≈ 0,8–1,0 ×` separación entre luminarias, para que ningún punto de la sala reciba más de 4 (§L3.0).

### L2.2 Tabla de zonas

Ambiente: sky / equator / ground. Volume: `postExposure` (EV) / contraste / saturación / filtro de color / WB temperatura, tinte / Bloom umbral · intensidad · scatter. Todos con clamp de bloom 12.

| # | Zona | Luz clave | Luz de relleno y acentos | Ambiente (sky / equator / ground) | Niebla (color · densidad) | Volume `Sector_M01_*` |
|---|---|---|---|---|---|---|
| 1 | **Acceso** (R01, R02) | Sodio 2200 K (1,00; 0,62; 0,24) | Baliza ámbar giratoria (1,0; 0,55; 0,10); relleno neutro-frío por el equator | (0,34; 0,28; 0,22) / (0,22; 0,20; 0,19) / (0,16; 0,12; 0,09) | (0,20; 0,15; 0,10) · 0,012 (R01), 0,014 (R02, pozo) | `Acceso`: +0,10 / 14 / −6 / (1; 0,97; 0,92) / +10, 0 / 1,0 · 0,12 · 0,55 |
| 2 | **Habitáculo** (R03, R04, R05) | Fluorescente fría 6500 K (0,80; 0,90; 1,00), parpadeante | R04: trabajo cálida 3000 K (1,0; 0,78; 0,50); R05: clínica 5000 K (0,92; 1,0; 0,96); mural rojo en R03 | (0,28; 0,32; 0,38) / (0,20; 0,22; 0,25) / (0,14; 0,13; 0,12) | (0,12; 0,14; 0,17) · 0,010 | `Habitaculo`: +0,05 / 10 / −10 / (0,95; 0,98; 1) / −10, 0 / 1,0 · 0,12 · 0,55 |
| 3 | **Armería** (R06) | Sodio 2000 K (1,00; 0,58; 0,20) en nave alta | 2 rotativas rojas (1,0; 0,06; 0,03) en la cámara; relleno frío | (0,30; 0,24; 0,18) / (0,20; 0,17; 0,15) / (0,14; 0,10; 0,07) | (0,18; 0,13; 0,08) · 0,011 | `Armeria`: +0,05 / 16 / −4 / (1; 0,95; 0,88) / +12, 0 / 1,0 · 0,12 · 0,55 |
| 4 | **Comunicaciones** (R07) | Fría 6000 K (0,78; 0,90; 1,00) | Verde de terminales (0,15; 1,0; 0,45) a nivel de suelo; rojo de alarma (1,0; 0,06; 0,03) | (0,22; 0,28; 0,30) / (0,16; 0,20; 0,20) / (0,10; 0,12; 0,11) | (0,09; 0,14; 0,13) · 0,010 | `Comunicaciones`: +0,05 / 12 / −8 / (0,94; 1; 0,97) / −6, −6 / 1,0 · 0,14 · 0,55. Estado `Alarm`: ver §L4 |
| 5 | **Almacenes** (R08) | Sodio tenue 2400 K (1,00; 0,66; 0,28) | Haces entre estanterías (conos aditivos); luz de cubierta blanca cálida | (0,24; 0,20; 0,16) / (0,17; 0,15; 0,13) / (0,11; 0,09; 0,07) | (0,15; 0,12; 0,09) · 0,013 (polvo) | `Almacenes`: 0 / 14 / −6 / (1; 0,96; 0,90) / +8, 0 / 1,0 · 0,12 · 0,55 |
| 6 | **Túneles** (R09, R10) | R09: fría 6500 K; R10: rojo de emergencia (1,0; 0,10; 0,05) | Franjas emisivas rojas y ámbar cada 10 m; ámbar de emboscada (1,0; 0,60; 0,15) | (0,22; 0,17; 0,17) / (0,17; 0,14; 0,14) / (0,12; 0,09; 0,09) | (0,16; 0,08; 0,07) · 0,012 (R09), 0,014 (R10) | `Tuneles`: 0 / 18 / 0 / (1; 0,90; 0,90) / +4, 0 / 1,0 · 0,14 · 0,55 |
| 7 | **Tratamiento de agua** (R11, R12, R13) | Verde-cian 5500 K (0,40; 0,95; 0,80) | Resplandores bajo el agua; ámbar del foso en R12 (contraste complementario); verde de bombas en R13 | (0,20; 0,30; 0,30) / (0,14; 0,21; 0,21) / (0,09; 0,13; 0,12) | (0,08; 0,17; 0,16) · 0,014 | `Agua`: +0,10 / 12 / −2 / (0,95; 1; 1) / −14, −8 / 1,0 · 0,12 · 0,55 |
| 8 | **Fundición** (R14) | Ámbar de nave 2300 K (1,00; 0,62; 0,25) | Naranja de lava (1,0; 0,40; 0,10), parpadeo lento | (0,32; 0,22; 0,16) / (0,21; 0,17; 0,14) / (0,20; 0,10; 0,05) | (0,20; 0,11; 0,06) · 0,012 | `Fundicion`: +0,05 / 18 / +4 / (1; 0,93; 0,84) / +16, 0 / 1,0 · 0,16 · 0,60 |
| 9 | **Ensamblaje** (R15) | Blanca fría 5600 K (0,92; 0,96; 1,00) | Cian de la línea (0,30; 0,85; 1,0); arco de soldadura (0,7; 0,85; 1,0) | (0,30; 0,33; 0,37) / (0,22; 0,24; 0,27) / (0,13; 0,13; 0,13) | (0,13; 0,15; 0,17) · 0,009 | `Ensamblaje`: +0,10 / 12 / −6 / (0,97; 0,99; 1) / −4, 0 / 1,0 · 0,12 · 0,55 |
| 10 | **Reactor** (R16) | Cian del núcleo (0,20; 0,80; 1,00) | Ámbar de refrigeración (1,0; 0,60; 0,20); techo frío tenue | (0,18; 0,28; 0,34) / (0,13; 0,19; 0,23) / (0,12; 0,10; 0,08) | (0,07; 0,15; 0,19) · 0,013 | `Reactor`: 0 / 16 / +2 / (0,92; 1; 1) / −8, −4 / 1,0 · 0,18 · 0,60 |
| 11 | **Centro de mando** (R17) | Azul-verde de pantallas (0,25; 0,75; 0,85), tenue | Tiras de consola (0,20; 0,90; 0,70); resplandor del holograma | (0,15; 0,20; 0,24) / (0,11; 0,14; 0,17) / (0,08; 0,09; 0,10) | (0,06; 0,10; 0,12) · 0,010 | `Mando`: −0,15 / 12 / −8 / (0,95; 1; 1) / −12, 0 / 1,0 · 0,12 · 0,55 |
| 12 | **Bóveda** (R18) | Blanca dura 7000 K (0,75; 0,82; 1,00) | Rojo de fases (1,0; 0,06; 0,03); acento de la estructura central | (0,26; 0,28; 0,32) / (0,18; 0,19; 0,22) / (0,11; 0,10; 0,10) | (0,12; 0,13; 0,16) · 0,007 (arena de 72 m) | `Boveda`: +0,10 / 18 / −8 / (0,96; 0,98; 1) / −2, 0 / 1,0 · 0,12 · 0,55 |
| — | **Esclusas C01–C17 y S00** (sesión) | Neutra 4500 K | Relleno cromático por esclusa (§L3.20); lámparas de ciclo ámbar/verde de R4 | Los de la sesión (arriba) | (0,09; 0,095; 0,105) · 0,016 [DECISIÓN F2] | Solo `Campaign_PostProfile` |
| 0 | **Hangar** (reservado) | — | — | **Zona protegida: no se toca** | — | — |

Todos los Volumes de zona llevan además: `FilmGrain` Thin1 intensidad **0,18** (D-L4; hoy 0,3), respuesta 0,8, e `Vignette` con color casi negro cálido (0,05; 0,04; 0,04) en lugar del morado de `Campaign_PostProfile` (0,18; 0,047; 0,32), con intensidad 0,084. Son overrides de zona: no se edita `Campaign_PostProfile` ni `CombatClarity` en F1.

### L2.3 Cómo se integra con ZoneEnvironment, SectorLightingBlend y VolumeFadeIn

| Pieza | Qué hace hoy | Plan para Map01 |
|---|---|---|
| `ZoneEnvironment.Capture/Apply` [ZE] | Captura y mezcla niebla, ambiente y reflexión (modo del ambiente cambia una vez a t = 0,5) | **Sin cambios.** Todos los cuartos usan Trilight y niebla exp²: no hay saltos de modo |
| `ZoneEnvironment.SceneFade` [ZE] | `Bind` recoge los Volumes con `isGlobal` y los AudioSource en bucle de la escena; `Set(k)` escala los pesos | El Volume de zona es un hijo de `[Lighting]` con `isGlobal`. Los **Volumes de estado** (§L4) también serán globales: ver el protocolo de peso en §L4.1 |
| `SectorLightingBlend` [SLB] | En la sesión: mezcla ambiente y niebla al cambiar la escena activa; fuerza Trilight | **No se incluye en `Map01_Campaign`** (D-L5). Hoy `RequiemCampaign` tiene los dos mezcladores a la vez; al activarse `activeSceneChanged`, `SectorLightingBlend` restituye el ambiente viejo antes de que `CampaignFlow` capture el nuevo y puede anular la mezcla. **[VALIDAR F2]** con una captura de la transición |
| `VolumeFadeIn` [VFI] | Sube el peso del Volume de sector al cargar; `Claim()` lo cede a `SceneFade` | Se añade al Volume de zona de cada cuarto (comportamiento idéntico al de `Room01–04`) |
| `CampaignFlow` [CF] | `environmentBlendSeconds` = 1,6 s; ambiente neutro durante el ciclo | Sin cambios |

---

## L3. Plan por cuarto

### L3.0 Reglas comunes y lectura de las tablas

**Convenciones.**

- **Anclaje:** coordenadas locales de [E §7]. [E] no numera los anclajes. Se usa el ID provisional `R<NN>.L<n>` en el orden de la ficha y **se pide a R1/R0 que los fijen** (R3 «enlaza las luces de la alarma por ID de anclaje», [E §7 R07]). Un anclaje de R1 es un máximo: R5 puede dejar alguno sin usar.
- **Modo:** todas las luces son `Realtime` (`lightmapBakeType = Realtime`); ninguna luminaria es `ContributeGI` ni Lightmap Static. Las mallas llevan `BatchingStatic | ReflectionProbeStatic | OccludeeStatic` (como las piezas de [VO] y [INV A2.2]). Las luminarias que giran o parpadean con cambio de material no son estáticas.
- **Luz:** `P` = puntual, `S nº°` = spot con ese ángulo. `I` = `Light.intensity` en las unidades del proyecto; `r` = `range` en metros. Calibración: ver §L2.1. **[SUPUESTO]** hasta F2.
- **Sombra:** solo en spots. «S» = sombra suave (`LightShadows.Soft`, resolución media del atlas de luces adicionales, 1024 [INV E1]); «–» = ninguna.
- **Estado (§L4):** `norm` = siempre igual; el resto nombra el grupo de luces que cambia con la ronda.
- **Salida:** toda luz de «salida» es un **spot blanco cálido** (1,0; 0,92; 0,80), 80°, `I` = 18, `r` = 8, porque queda a ≤ 6 m del marco de la puerta y R4 reserva rojo, ámbar y verde a esa distancia [PU P2.1]. Baja al 25–35 % en combate y sube al 100 % al cumplirse la ronda.

**Alcance, solapes y el límite de 4 luces por objeto.**

URP aplica por objeto las 4 luces adicionales más importantes; las demás se descartan. Lo que se percibe como «pop» ocurre al descartar o admitir una luz **con aporte visible**.

1. La atenuación de URP llega a cero en `range` con una ventana suave: una luz en el 10 % externo de su alcance aporta < 3 %. Si el 5.º candidato de un objeto está en ese borde, no hay salto visible. **[SUPUESTO: forma exacta de la ventana; se comprueba en F2 con una captura de enemigo cruzando un solape]**
2. Los objetos **estáticos** conservan su conjunto de luces (no se mueven): no hay pop. El riesgo es de los **dinámicos**: enemigos, jugador, restos de muerte, armas soltadas y proyectiles.
3. Regla de diseño: `r ≈ 0,8–1,0 ×` separación entre luminarias contiguas (rejilla). Con eso, un punto cualquiera ve ≤ 4 centros dentro de su alcance, y la 5.ª luz está en el borde. Los alcances de los acentos (landmark, baliza) son ≤ 10 m para no sumar más.
4. Con `CheckLightBudget` (esfera de luz contra *bounds*, como en [VO §3]) el recuento es más pesimista que el real, porque el *bounds* de una tesela de 10 m añade hasta 7 m. El validador (§L8) marca como **aviso** los objetos con 5–6 luces candidatas y como **error** los de ≥ 7; los avisos se revisan por aporte real (distancia / alcance > 0,85 → se ignora).
5. Las **luces de estado de puerta** (R4, `Status`, apagadas en reposo, ≤ 2 visibles) y las 2 lámparas de ciclo de cada esclusa (R4) son aparte (D-L6). Su alcance máximo acordado: ≤ 5 m.

**Reflection probes y light probes.**

- **Reflection probes horneadas** (las hornea el coordinador): `Box Projection` activo, importancia 1, resolución 128 **[SUPUESTO]**, un solo probe que cubre el AABB interior del cuarto salvo R10, R12 y R18 (2 cada uno). La campaña actual ya usa una caja por sala (54×14×56 en `Room01`) [INV E3 #6]. Total del mapa: 21.
- **Light probes: ninguno** (D-L1). Los enemigos reciben el ambiente trilight y las luces en tiempo real. Es consistente con [VO §3]: «el estático no muestrea probes» por coste de CPU.

**Spawns, recogidas y botiquines.** Cada grupo de spawn de [E §7] tiene su centro a ≤ 0,6 × `r` de una luz (o en un tramo con ≥ 45 de luminancia). Los volúmenes de recogida de armas (Ø 6 m de R18 y los de cada cuarto) y los botiquines llevan una luz a ≤ 8 m y nunca en la sombra de un spot con sombra.

### L3.1 S00 · Vestíbulo de sesión (en `Map01_Campaign`)

Paleta: zona Acceso, atenuada. AABB 14×9×15 [E §7].

| ID | Anclaje de R1 | Luminaria | Luz | Sombra | Rol |
|---|---|---|---|---|---|
| S00.L1 | Pared, a un lado de la estación | LUM-02 plafón enjaulado (sodio) | P (1,0; 0,65; 0,28) · I 10 · r 8 | – | Ambiente |
| S00.L2 | Pared, al otro lado | LUM-06 foco spot sobre la `WeaponMountStation` | S 55° blanco cálido (1,0; 0,85; 0,65) · I 16 · r 9 | – | **Landmark: el arma obligatoria antes del primer T-07** |

- Total 2 (de 2). La estación ya trae una luz propia `MountStation_C04`, apagada por defecto [R2A §4]; cuenta como transitoria.
- Probes: 0 (usa el reflejo por defecto de la sesión). [E] no da coordenadas de estos dos anclajes.

### L3.2 R01 · Control de acceso (Acceso; combate E1; techo 9)

Arena 32×40; garita central 6×6×3 con baliza ámbar; entreplanta +3,5.

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R01.L1–L4 | Techo (±8; 8,6; −4) y (±8; 8,6; 10) | LUM-02 plafón enjaulado, sodio | P (1,0; 0,62; 0,24) · I 15 · r 14 | – | `techo` | Ambiente y ruta: 4 charcos solapados |
| R01.L5 | Garita (0; 3,6; 3) | LUM-05 baliza giratoria ámbar, `LuminaireSpin` 120 °/s | P (1,0; 0,55; 0,10) · I 14 · r 9 | – | norm | **Landmark** («ACCESO C-04») |
| R01.L6 | Pared salida (6; 6,6; 19,4) | LUM-15 foco de umbral | Spot blanco cálido (§L3.0) | – | `salida` | **Salida** (ruta N, x +6) |

- Total 6 (de 6); sombras 0; probes 1 (32×11×42).
- Spawns: G1 (−15…−11; 14…19), G2 entreplanta (11…15; 4…12), G3 (−15…−11; −8…−2): L1–L4 a ≤ 8 m de G1 y G3; G2 queda cubierto por el solape de L2.
- El pórtico de entrada (P a z −14) y la puerta de salida quedan libres de luces; el anclaje de salida está por encima de los 4,5 m del volumen libre.

### L3.3 R02 · Pozo del montacargas (Acceso; combate vertical E2; techo 21)

| ID | Anclaje | Luminaria | Luz | Sombra | Rol |
|---|---|---|---|---|---|
| R02.L1–L4 | Colgadas a y +15 en (±10; 0) y (±10; 16) | LUM-03 campana de sodio colgante (cadena de 3 m) | P (1,0; 0,62; 0,24) · I 40 · r 18 | – | Ambiente en vertical (h = 15) |
| R02.L5 | Jaula del pozo (0; 16; 6) | LUM-06 foco spot, mira abajo | S 45° (1,0; 0,80; 0,50) · I 55 · r 20 | **S** (opcional, D-L2) | **Landmark:** plataforma estrellada y contrapeso. La sombra recorta los cables sobre la pared |
| R02.L6 | Pared de la galería (−14; 15; −21) | LUM-04 emergencia enjaulada, blanca | P (1,0; 0,95; 0,85) · I 10 · r 8 | – | Ruta de entrada |
| R02.L7 | Salida (0; 8; 21,4) | LUM-15 | Spot blanco cálido | – | **Salida** |

- Total 7 (de 7); sombras 0–1; probes 1 (42×23×46).
- **Conflicto con [E]:** R02.L6 cae dentro del volumen libre de la puerta A de entrada (x −17,5…−10,5; z −22…−18; y 12…16,5, con los valores de [E §7] para A: 7 × 4 × 4,5). Se pide a R1 subirlo a y ≥ 17,5 o desplazarlo a x −20 **[VALIDAR F2]**. Hasta entonces R02 queda en 6 luces.
- Riesgo de pop: 4 campanas con r 18 y separación 20 × 16 dan hasta 4 solapes en el centro; los dinámicos que caen por el pozo atraviesan 3 capas (galería, terrazas, suelo). El ground cálido del ambiente mantiene alto el fondo para que la 5.ª luz no se note.
- Peligro: caídas de 6 m con tira ámbar tenue (`LUM-07`) en el borde de las terrazas.

### L3.4 R03 · Dormitorios (Habitáculo; combate E2, presentación del HOV; techo 8)

Tres naves separadas por tabiques de 2 m (los tabiques ocultan ≈ 2/3 del cuarto desde cada nave [E §7]).

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R03.L1–L6 | 2 por nave, en (0 / ±13; 7,6; ±10) | LUM-02 plafón enjaulado, fría | P (0,78; 0,88; 1,0) · I 11 · r 13 | – | `techo`; **2 con `Map01LampFlicker`** (nave W, z ±10) | Ambiente y ruta |
| R03.L7 | Mural (0; 4; 23,5) | LUM-04 emergencia enjaulada | S 70° rojo (1,0; 0,10; 0,06) · I 18 · r 10, mira al mural | – | norm | **Landmark** (mural de la unidad, visible desde el pórtico) |
| R03.L8 | Salida (13; 6,5; 23,5) | LUM-15 | Spot blanco cálido | – | `salida` | **Salida** |

- Total 8 (de 8); sombras 0; probes 1 (38×10×50).
- Oscuridad intencional: tubos que fallan en la nave W; nunca por debajo de 45 en los slots de spawn (§L1.4). Un tubo apagado dura ≤ 0,4 s seguidos [LF: `outageChance` 0,18, ráfagas de ≈ 2 s].
- Riesgo de pop: bajo. [E] lo califica como «el cuarto más barato de iluminar».

### L3.5 R04 · Comedor (Habitáculo; combate E2, presentación del SAW; techo 9)

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R04.L1–L4 | Techo (±10; 8,6; −8 / 6); 2 parpadeantes | LUM-02 fría | P (0,80; 0,90; 1,0) · I 15 · r 14 | – | `techo`; 2 con `Map01LampFlicker` | Ambiente y ruta |
| R04.L5 | Cocina (−8; 8,6; 16) | LUM-02 neutra cálida | P (1,0; 0,90; 0,75) · I 14 · r 10 | – | norm | Distingue la cocina (+4,5) del salón |
| R04.L6 | Barricada (12; 3; 7) | LUM-09 lámpara de trabajo portátil | P (1,0; 0,78; 0,50) · I 24 · r 10 | – | norm | **Landmark** («NO PASARÁN»); avisa de la salida E |
| R04.L7 | Salida (19,5; 6; 8) | LUM-15 | Spot blanco cálido | – | `salida` | **Salida** |

- Total 7 (de 7); sombras 0; probes 1 (42×11×42).
- La lámpara de trabajo es cálida (3000 K), no ámbar saturado, y está a ≈ 8 m del marco E (x 20), más de 6 m [PU P2.1].

### L3.6 R05 · Enfermería (Habitáculo; respiro; techo 8)

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R05.L1–L4 | Techo, blanca clínica (sin coordenadas en [E]) | LUM-02 | P (0,92; 1,0; 0,96) · I 12 · r 14 | – | `techo`; 2 con `Map01LampFlicker` suave (`outageChance` 0,10) | Ambiente |
| R05.L5 | Lámpara quirúrgica (sobre el quirófano, hasta +7) | LUM-10 lámpara quirúrgica | S 60° blanco (1,0; 1,0; 0,98) · I 40 · r 8 | – | norm | **Landmark** y charco duro |
| R05.L6 | Salida | LUM-15 | Spot blanco cálido | – | `salida`: **encendido desde el principio** (no hay ronda) | **Salida** |

- Total 6 (de 6); sombras 0; probes 1 (38×10×32).
- Sin rondas: no hay estado de combate. Es el momento de ver la salida iluminada y los botiquines.

### L3.7 R06 · Armería (Armería; combate E4, presentación de la TUR; techo 14)

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R06.L1–L4 | Nave alta (±10; 13,5; ±12) | LUM-03 campana de sodio | P (1,0; 0,58; 0,20) · I 36 · r 20 (**L2 como spot 90°** si D-L2 activa su sombra) | S opcional en L2 | `techo` | Ambiente |
| R06.L5–L6 | Cara W de la cámara (−2,5; 5,5; ±4) | LUM-05 baliza giratoria roja, 2 uds., `LuminaireSpin` en sentidos opuestos | P (1,0; 0,06; 0,03) · I 14 · r 8 | – | norm | **Landmark** («ARMERÍA 7») |
| R06.L7 | Entreplanta (−20; 11; −10) | LUM-04 emergencia enjaulada blanca | P (1,0; 0,95; 0,85) · I 10 · r 9 | – | norm | Ruta de entrada |
| R06.L8 | Salida (14; 9; −27,5) | LUM-15 | Spot blanco cálido | – | `salida` | **Salida** |

- Total 8 (de 8); sombras 0–1; probes 1 (50×16×58).
- **Conflicto con [E]:** R06.L7 está dentro del volumen libre de la puerta Z de entrada (W, z −10, y +6: x −24…−18, z −15…−5, y 6…12, con 10 × 6 × 6 de [E §7]). Se pide moverlo a x ≥ −17 o a y ≥ 12,5. [E] ya avisa «R5 no debe usar los anclajes de puerta»; hasta resolverlo R06 queda en 7.
- Las rotativas rojas están a ≥ 19 m de las dos puertas Z (W y S): cumple la distancia de 6 m.
- Con L2 en sombra la nave gana profundidad (la cámara y la pasarela ya parten la vista).

### L3.8 R07 · Comunicaciones (Comunicaciones; ALARMA E8; techo 10)

Cuarto con alarma `CorridorAlarm`, 4 alas laterales cerradas por persianas y estado de apagón en la última ronda (§L4).

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R07.L1–L4 | Techo (±10; 9,6; ±10) | LUM-02 enjaulada fría | P (0,78; 0,90; 1,0) · I 18 · r 16 | – | `techo` (§L4.3) | Ambiente y ruta |
| R07.L5 | Plato del mástil (0; 8; 0) | LUM-05 baliza giratoria roja en el plato del radar (se reutiliza `CorridorAlarm.beacons`) | S 55° rojo (1,0; 0,06; 0,03) · I 30 · r 22, **apagada en reposo** | **S** en alarma (D-L2) | `alarma` | **Landmark** |
| R07.L6–L7 | Tabiques (±18,5; 5; 0) | LUM-04 emergencia roja, 2 uds. | P (1,0; 0,06; 0,03) · I 12 · r 10 | – | `alarmLights` de `CorridorAlarm` (intensidad fijada por el componente: `alarmIntensity` 3,5 por defecto; calibrar) | Alarma |
| R07.L8 | Salida (sin coordenadas) | LUM-15 | Spot blanco cálido | – | `salida` | **Salida** |

- Total 8 (de 8); sombras 0 (1 en alarma con D-L2); probes 1 (54×12×54).
- **Verde de terminales** a nivel de suelo: 8 tiras emisivas `LUM-07` sin `Light` (no cuentan), en el anillo de consolas, con el material `M01_Lum_Green`. Son el landmark del suelo y el contrapunto cromático a la alarma.
- **Alas (conflicto con [E]):** los 8 anclajes de [E] están en la sala central; las 4 alas con racks no tienen ninguno, pero la fase «Calma» de [ENC A6.3] exige explorar un ala. Propuesta: cada ala se ilumina con 2 tiras emisivas verdes (`LUM-07`, sin `Light`) más el ambiente, sin spawn dentro (las alas no tienen slots [E]). Si no basta, R1 añade 1 anclaje por ala y R07 baja a 4 luces de techo.
- **Sub-espacio:** las 4 alas se agrupan bajo `[Lighting]/Wings/<N|S|E|W>` para poder apagarlas con la persiana cerrada (el render de la sala central lo pide [E]); `Map01RoomLighting` lo hace leyendo el estado de la persiana (`ArenaShutter`, componente de R4) **[VALIDAR F2]**.
- Alarma: `CorridorAlarm` se usa **sin cambios**: `Configure(rounds, alarmLights, beacons, beaconLights, beaconHeads, siren, klaxon, screens)` con `alarmLights` = R07.L6–L7, `beaconLights` = R07.L5 y la cabeza giratoria del plato [CA]. Sus valores por defecto (`alarmIntensity` 3,5, `beaconIntensity` 14) calibran la escala de las demás luces rojas del mapa.

### L3.9 R08 · Almacenes (Almacenes; combate E3, presentación del BIG; techo 14)

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R08.L1–L4 | Nave alta (±12; 13,5; −16 / 8) | LUM-03 campana de sodio tenue | P (1,0; 0,66; 0,28) · I 30 · r 20 | – | `techo`; 1 con `Map01LampFlicker` leve | Ambiente |
| R08.L5 | Foco sobre el landmark (2; 12; −5) | LUM-06 foco spot, apunta al bloque central | S 50° (1,0; 0,78; 0,45) · I 45 · r 24 | **S** (activa desde F2) | norm | **Landmark:** estantería volcada. La sombra de la pila de contenedores da el efecto de «haces entre estanterías» |
| R08.L6 | Cubierta (−10; 10; 26) | LUM-04 enjaulada blanca cálida | P (1,0; 0,92; 0,80) · I 14 · r 10 | – | norm | Rellano de llegada y ruta |
| R08.L7 | Salida (E, z −20) | LUM-15 | Spot blanco cálido | – | `salida` | **Salida** |
| (L8) | Libre | — | — | — | — | **Reserva de R5** [E «1 libre para R5»]; no se usa en F1 |

- Total 7 (de 8); sombras 1; probes 1 (50×16×58).
- Los **haces** son 2 conos aditivos `LUM-14` sin `Light` desde L1 y L3, de alfa baja, con la niebla de 0,013 como polvo. Sin lens flare.
- **Aviso:** R08.L6 (−10; 10; 26) roza el límite superior del volumen libre de la puerta Z de entrada (y 4…10, x −15…−5, z 22…28). Se pide y ≥ 10,5 a R1.
- Es el primer BIG de la sesión: el BIG y su explosión (`FX_BigBot_Blast_v4`) suman luces transitorias; no se cuentan aquí [E §7].

### L3.10 R09 · Taquillas de mantenimiento (Túneles; respiro; techo 8)

| ID | Anclaje | Luminaria | Luz | Sombra | Rol |
|---|---|---|---|---|---|
| R09.L1–L3 | Techo | LUM-02 fría | P (0,80; 0,90; 1,0) · I 12 · r 12 | – | Ambiente |
| R09.L4 | Oficina | LUM-09 lámpara de trabajo | P (1,0; 0,75; 0,45) · I 10 · r 8 | – | Detalle y botiquines |
| R09.L5 | Salida | LUM-15 | Spot blanco cálido | – | **Salida** |

- Total 5 (de 5); sombras 0; probes 1 (30×10×30).
- El esquema pintado de la trituradora (landmark) se lee con L1 más una tira emisiva roja `LUM-07` en la cabecera. Si R09 se recorta (D-U3), L1–L3 pasan a la bahía S de R10.

### L3.11 R10 · Túnel de la trituradora (Túneles; PERSECUCIÓN; 16×200; techo 10)

El túnel se ve entero (200 m): las luces son pocas y se resuelve con **emisivos sin `Light`**.

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R10.L1 | Bahía (z −100…−86) | LUM-02 fría | P (0,80; 0,90; 1,0) · I 16 · r 14 | – | `apagon` (cae al arrancar la persecución) | Bahía, antes del trigger |
| R10.L2–L4 | Brillo de las 3 grietas (desde abajo) | LUM-17 luz sumergida, bajo cada grieta | P rojo (1,0; 0,10; 0,05) · I 30 · r 14 | – | norm | **Peligro y landmark:** la grieta se lee desde lejos |
| R10.L5–L7 | Las 3 emboscadas (a `ActivationZ`) | LUM-04 emergencia | P ámbar (1,0; 0,60; 0,15) · I 22 · r 14 | – | grupo `emboscadas`, se enciende al llegar a su `ActivationZ` | Aviso de emboscada |
| R10.L8 | Salida (puerta Z al N) | LUM-15 | Spot blanco cálido | – | `salida` | **Salida** («puerta Z iluminada» [E]) |

- Total 8 (de 8); sombras 0; probes **2** (18×22×100 cada uno).
- **Franjas emisivas** cada 10 m (el «resto» que [E] reserva a R5): 20 por muro = 40 quads `LUM-07` en 2 mallas combinadas de 100 m (≈ 240 tris cada una) con 2 materiales compartidos: rojo tenue (lado de la sierra) y ámbar tenue (lado opuesto). Dan 28–45 de valor en el fondo del túnel.
- **Teselas:** 10 m (20 por muro) [E]; cada tesela ve ≤ 2 luces (r 14, separación ≥ 25 m): sin riesgo de 4 por objeto.
- La persecución avanza en +Z: los brillos de foso y las franjas van por delante del jugador; la sierra viene por detrás. **[SUPUESTO]** (no leí el carro de `ChaseCorridorEncounter`; solo `Running`, `Completed` y `Ambushes[].ActivationZ` son públicos).

### L3.12 R11 · Decantadores (Agua; horda E6; techo 14)

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R11.L1–L4 | Techo (±12; 13,5; ±10) | LUM-02 enjaulada verde-cian | P (0,40; 0,95; 0,80) · I 30 · r 18 | – | `techo` | Ambiente |
| R11.L5 | Puente del clarificador | LUM-06 foco spot sobre T1 | S 55° (0,80; 1,0; 0,95) · I 25 · r 14 | – | norm | **Landmark** (puente giratorio) |
| R11.L6–L7 | Bajo el agua de T1 y T2 | LUM-17 luz sumergida | P (0,20; 0,90; 0,70) · I 12 · r 9 | – | norm | Resplandor del agua |
| R11.L8 | Salida (N, x −12) | LUM-15 | Spot blanco cálido | – | `salida` | **Salida** |

- Total 8 (de 8); sombras 0; probes 1 (50×16×54). El agua no refleja en tiempo real (ni se propone) [E].
- Hasta 12 cuerpos (CAR): es una de las rondas más cargadas del mapa [E]; las luces no cambian en combate salvo la salida.

### L3.13 R12 · Colector derrumbado (Agua; combate con grieta; techo 14)

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R12.L1–L4 | Techo (sin coordenadas); **2 rotas parpadeando** | LUM-02 fría-cian | P (0,60; 0,90; 0,90) · I 30 · r 18 | – | `techo`; 2 con `Map01LampFlicker` | Ambiente |
| R12.L5–L6 | Foso (±8; −6; 0) | LUM-17 luz sumergida | P ámbar (1,0; 0,55; 0,15) · I 25 · r 14 | – | norm | **Peligro y landmark:** brillo bajo el tubo colector |
| R12.L7 | Plataforma | LUM-04 enjaulada blanca | P (1,0; 0,95; 0,85) · I 14 · r 10 | – | norm | Ruta |
| R12.L8 | Salida (N, x +10) | LUM-15 | Spot blanco cálido | – | `salida` | **Salida** |

- Total 8 (de 8); sombras 0; probes **2** (una caja por orilla del foso; [E] da 2 sub-espacios visibles a la vez).
- Contraste complementario: ámbar del foso frente al cian del resto. La cascada (1 sistema de partículas, R2) no emite luz.
- Las luces R12.L5–L6 salen por debajo de y −0,5 y solo iluminan los objetos del lado cercano del foso.

### L3.14 R13 · Sala de bombas (Agua; respiro; techo 10)

| ID | Anclaje | Luminaria | Luz | Sombra | Rol |
|---|---|---|---|---|---|
| R13.L1–L3 | Techo | LUM-02 cian blanca | P (0,75; 1,0; 0,95) · I 15 · r 14 | – | Ambiente |
| R13.L4–L6 | Estado de las 3 bombas (alcance ≤ 4) | LUM-11 luz de estado | P verde (0,15; 1,0; 0,45) · I 4 · r 4 | – | **Landmark** («tres bombas gigantes») |
| R13.L7 | Salida (N, x −6) | LUM-15 | Spot blanco cálido | – | **Salida** |

- Total 7 (de 7); sombras 0; probes 1 (34×12×42). Sin ronda: la salida está iluminada desde el principio.
- **Verde saturado a ≥ 6 m de los marcos:** las bombas están en el centro; la entrada S (x +4) y la salida N (x −6) quedan a más de 6 m **[VALIDAR F2 con las coordenadas de R2]**.

### L3.15 R14 · Fundición (Fundición; combate E3 combinaciones; techo 13)

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R14.L1–L4 | Nave alta ámbar (±12; 12,5; −4) y (±12; 12,5; 22) | LUM-03 campana de sodio | P (1,0; 0,62; 0,25) · I 32 · r 20 | – | `techo` | Ambiente |
| R14.L5–L6 | Brillo del horno (±5; 4; 30) | Sin malla; usa el emisivo de las coladas de R2 (`M01_Lum_Lava`) | P naranja (1,0; 0,40; 0,10) · I 40 · r 14 | – | norm, con `Map01LampFlicker` lento (modo «fuego») | **Landmark:** horno con 4 coladas |
| R14.L7 | Puente grúa (0; 9; 16) | LUM-06 foco spot, mira abajo | S 70° (1,0; 0,80; 0,55) · I 40 · r 16 | **S** (activa desde F2) | norm | Ruta y sombra del puente y las pasarelas sobre la nave |
| R14.L8 | Salida (18; 9; 31,5) | LUM-15 | Spot blanco cálido | – | `salida` | **Salida** |

- Total 8 (de 8); sombras 1; probes 1 (54×15×66).
- Ambiente con ground cálido (0,20; 0,10; 0,05) como rebote de la lava. La lava es decoración inaccesible tras barandilla [E]; ningún emisivo de lava a menos de 6 m de un marco.
- Peligro: caídas de 5 m: tira ámbar tenue en el borde de las pasarelas.

### L3.16 R15 · Ensamblaje (Ensamblaje; combate E2 combinaciones; techo 13)

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R15.L1–L4 | Techo frías (sin coordenadas; **[SUPUESTO]** (±13; 12,5; ±12)) | LUM-02 enjaulada blanca fría | P (0,92; 0,96; 1,0) · I 32 · r 20 | – | `techo` | Ambiente |
| R15.L5–L6 | Línea (0; 11; ±10) | LUM-06 foco spot cian, mira a la cadena | S 60° (0,30; 0,85; 1,0) · I 28 · r 16 | – | norm | **Landmark:** cadena de torsos T-07, iluminada en cian |
| R15.L7 | Soldadura (12; 3; 0) | LUM-12 luz de soldadura | P (0,70; 0,85; 1,0) · I 30 · r 8 | – | `Map01LampFlicker` modo «arco» (cambios grandes ≤ 3 Hz) | Cuenta como estática |
| R15.L8 | Salida (N, x 0) | LUM-15 | Spot blanco cálido | – | `salida` | **Salida** |

- Total 8 (de 8); sombras 0; probes 1 (54×15×52).
- **Jaula opcional (x −26…−20, z −25…−19):** 2 botiquines; se ilumina con R15.L1 y una tira emisiva blanca `LUM-07` en la cara de la persiana. No tiene luz propia.
- La línea son ≈ 20 mallas estáticas de T-07 [E]: el spot cian las tiñe sin necesidad de materiales nuevos.

### L3.17 R16 · Reactor (Reactor; PICO 2 fuego cruzado; techo 14)

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R16.L1 | Núcleo, centro en (0; 7; 3), Ø 10 hasta el techo | Sin malla; emisivo del núcleo `M01_Lum_Cyan` | **P** cian (0,20; 0,80; 1,0) · I 60 · r 26 | **–** (conflicto con [E], ver abajo) | `reactor` (pulso 0,25 Hz; sobrecarga en la ronda 3) | **Landmark** |
| R16.L2–L5 | Techo (sin coordenadas; **[SUPUESTO]** (±14; 13; ±12)) | LUM-02 enjaulada fría | P (0,70; 0,90; 1,0) · I 22 · r 14; **L3 como spot 90° con sombra** si D-L2 lo activa | S opcional en L3 | `techo` | Ambiente |
| R16.L6–L7 | Torres de refrigeración | LUM-04 emergencia enjaulada | P ámbar (1,0; 0,60; 0,20) · I 30 · r 14 | – | norm | Acento y orientación |
| R16.L8 | Salida (N, x −14) | LUM-15 | Spot blanco cálido | – | `salida` | **Salida** |

- Total 8 (de 8); sombras 0–1; probes 1 (54×16×52).
- **Conflicto con [E]:** [E] propone «la luz del núcleo, candidata a sombra». Una luz **puntual** con sombra consume 6 caras del atlas de 1024 y deja teselas de ≈ 256 px; la sombra del núcleo (sin sentido visual desde dentro) no compensa. Se recomienda **una sola sombra con un spot de techo** (R16.L3).
- **Riesgo de 4 luces por objeto:** el núcleo (r 26) alcanza casi todo el cuarto. Para que los objetos del anillo de ≥ 6 m no pasen de 4, las luces de techo cercanas llevan r 14. **Es el cuarto con mayor riesgo del mapa;** F2 lo mide con el validador (§L8).
- Es uno de los 3 cuartos que se miden en Release [E].
- Caídas de 6 m en las galerías: tira ámbar tenue.

### L3.18 R17 · Centro de mando (Mando; respiro, E8 opcional; techo 12)

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R17.L1–L4 | Techo, tenues | LUM-02 | P (0,60; 0,85; 0,95) · I 10 · r 14 | – | `techo` | Ambiente |
| R17.L5 | Resplandor de la mesa | Sin malla; el holograma de la mesa (R2) usa `M01_Lum_Blue` | P (0,25; 0,75; 0,85) · I 30 · r 12, pulso 0,5 Hz ±15 % | – | norm | **Landmark:** mesa táctica con el holograma de la Bóveda |
| R17.L6–L7 | Tiras de consola (2) | LUM-07 + `Light` | P (0,20; 0,90; 0,70) · I 8 · r 8 | – | norm | Ruta de gradas |
| R17.L8 | Salida (N) | LUM-15 | Spot blanco cálido | – | `salida`: **encendida desde el principio** | **Salida** hacia el jefe |

- Total 8 (de 8); sombras 0; probes 1 (42×14×38).
- Si se usa el E8 opcional («examen final»), reutiliza `CorridorAlarm` y el apagón de §L4.5 (D-L3 c).
- Es el cuarto más oscuro de la campaña a propósito (−0,15 EV); el suelo de las gradas conserva ≥ 45 (§L1.4).

### L3.19 R18 · Bóveda (Bóveda; JEFE; antecámara 14, arena 24)

Arena 72×72, galería +6, antecámara 16×20×14 [E §9].

| ID | Anclaje | Luminaria | Luz | Sombra | Estado | Rol |
|---|---|---|---|---|---|---|
| R18.L1–L2 | Altas en (±18; 22; 11,5) | LUM-16 panel alto de arena | P (0,75; 0,82; 1,0) · I 90 · r 32 | – | `arena` (§L4.7) | Ambiente |
| R18.L3–L4 | Altas en (0; 22; −6) y (0; 22; 29) | LUM-16 panel alto, **spots** de 80° | S (0,75; 0,82; 1,0) · I 95 · r 36 | **S** (2, el máximo del cuarto) | `arena` | Ambiente y **sombras de los 6 pilares y del jefe** (en A) |
| R18.L5–L6 | Acentos de fase (esquinas de la galería, sin coordenadas en [E]; **[SUPUESTO]** (±34; 6,5; 40)) | LUM-05 baliza roja | P (1,0; 0,06; 0,03) · I 0 → 35 · r 18 | – | `fase` (§L4.8) | Fases del jefe |
| R18.L7 | Estructura central [DEPENDE DEL JEFE] | LUM-06 | Ver §L4.8 | – | `jefe` | **Landmark** |
| (R18.L8) | Puerta B | **R4** | — | — | — | No cuenta aquí |

- Total 7 (de 8, el 8.º es de R4); sombras 2; probes **2** (antecámara 16×14×20; arena 72×24×72).
- **Antecámara:** tiene 4 botiquines [E] y la puerta B cerrada la separa de las luces de la arena. [E] no le da anclajes. Se pide **1 anclaje más** de R1 en la antecámara (blanco frío tenue (0,70; 0,80; 1,0) · I 14 · r 14). Sin él, la antecámara se lee negra hasta que abre B (§L4.7).
- **Recogida de armas:** Ø 6 m alrededor de los puntos de caída posibles, suelo plano [E §9]: lo iluminan L1–L4 (charcos de r 32 y 36) con un mínimo de 70 en todo el suelo.
- Luz móvil del jefe: una luz puntual hija del prefab del jefe (núcleo o ojo) es **transitoria de efectos** [BFL: tope de 2 simultáneas]. En la arena habrá además las explosiones de los refuerzos: se pide a R0 subir a **3** el tope en R18 (§L9).

### L3.20 C01–C17 · Esclusas (sesión; `C<NN>/[Lighting]`)

Cada cámara tiene 1 relleno de R5 [PU P4 regla 1]. R4 pone las 2 lámparas de `AirlockCycle` (ámbar/verde, `lightIntensity` 2,5 [AC]), las balizas y las luces de estado de las 2 puertas. Las cámaras Transfer de hoy son oscuras por dentro (luminancia 0,011–0,040 [PU P4]); el relleno les da una lectura mínima de 45.

| Parte | Valor |
|---|---|
| Luz | **Puntual** sin sombra, 1 por cámara, en el techo al centro de la cámara |
| Intensidad y alcance | I 6 · r 7 (cámara A, 8×7×5,5) · I 9 · r 9 (cámara Z, 12×10×8) |
| Color | **Puente cromático:** media entre el color clave de la zona de salida y el de la entrada, desaturada al 70 % hacia el blanco |
| Estado | Fijo. Las lámparas de ciclo (R4) parpadean ámbar durante el cruce y no se tocan |

| Esclusa | Clase | De → A | Color del relleno |
|---|---|---|---|
| C01 | A | Acceso → Acceso | (1,0; 0,86; 0,70) |
| C02 | Z | Acceso → Habitáculo | (0,90; 0,90; 0,96) |
| C03, C04 | A | Habitáculo → Habitáculo | (0,86; 0,92; 1,0) |
| C05 | Z | Habitáculo → Armería | (0,95; 0,90; 0,86) |
| C06 | Z | Armería → Comunicaciones | (0,86; 0,94; 0,92) |
| C07 | Z | Comunicaciones → Almacenes | (0,95; 0,93; 0,88) |
| C08 | Z | Almacenes → Túneles | (1,0; 0,86; 0,80) |
| C09 | A | Túneles → Túneles | (0,95; 0,85; 0,82) |
| C10 | Z | Túneles → Agua | (0,86; 0,96; 0,94) |
| C11, C12 | A | Agua → Agua | (0,84; 0,98; 0,96) |
| C13 | Z | Agua → Fundición | (0,95; 0,92; 0,86) |
| C14 | Z | Fundición → Ensamblaje | (0,94; 0,92; 0,92) |
| C15 | Z | Ensamblaje → Reactor | (0,84; 0,94; 1,0) |
| C16 | Z | Reactor → Mando | (0,80; 0,94; 1,0) |
| C17 | Z | Mando → Bóveda | (0,90; 0,94; 1,0) |

- Clases tomadas de [E §5.2]. 17 luces en todo el mapa; probes 0. La luz del relleno es lo único de R5 en la esclusa: los marcos y las balizas son de R4.
- **Límite de 4 por objeto en la cámara:** relleno (1) + 2 lámparas de ciclo + 2 luces de estado de puerta = 5 candidatas para los objetos centrales. Las luces de estado tienen r ≤ 5 y están en los extremos: los objetos centrales ven ≤ 4 con aporte. **[VALIDAR F2]** con el validador en una cámara Z.

### L3.21 Resumen de recuento

| Cuarto | Anclajes de R1 | Luces R5 usadas | Sombra | Probes |
|---|---|---|---|---|
| S00 | 2 | 2 | 0 | 0 |
| R01 | 6 | 6 | 0 | 1 |
| R02 | 7 | 7 (6 hasta mover L6) | 0–1 | 1 |
| R03 | 8 | 8 | 0 | 1 |
| R04 | 7 | 7 | 0 | 1 |
| R05 | 6 | 6 | 0 | 1 |
| R06 | 8 | 8 (7 hasta mover L7) | 0–1 | 1 |
| R07 | 8 | 8 | 0–1 (alarma) | 1 |
| R08 | 8 | 7 (1 de reserva) | **1** | 1 |
| R09 | 5 | 5 | 0 | 1 |
| R10 | 8 | 8 | 0 | 2 |
| R11 | 8 | 8 | 0 | 1 |
| R12 | 8 | 8 | 0 | 2 |
| R13 | 7 | 7 | 0 | 1 |
| R14 | 8 | 8 | **1** | 1 |
| R15 | 8 | 8 | 0 | 1 |
| R16 | 8 | 8 | 0–1 | 1 |
| R17 | 8 | 8 | 0 | 1 |
| R18 | 8 (1 es de R4) | 7 | **2** | 2 |
| C01–C17 | 17 | 17 | 0 | 0 |

- **Totales:** 132 luces en R01–R18 + 2 en S00 + 17 en esclusas = **151 `Light` fijas**. Máximo por cuarto = **8** (R03, R06, R07, R10, R11, R12, R14, R15, R16, R17); mínimo 5 (R09).
- **Con sombra:** 8 como tope (R02 1, R06 1, R07 1 en alarma, R08 1, R14 1, R16 1, R18 2); activas por defecto 4 luces en 3 cuartos (R08, R14, R18 ×2).
- **Reflection probes:** 21 (R10, R12 y R18 con 2).
- **Visibles a la vez en el peor fotograma:** el cuarto (≤ 8) + el relleno de la esclusa vecina (1) + las luces de R4 (≤ 4 si se abre una puerta con su cámara).

---

## L4. Estados de iluminación

### L4.1 Modelo y componentes nuevos (solo observadores)

Ningún sistema existente se modifica ([MD §3.6]). Todo lo nuevo vive en `Assets/IronRequiem/Scripts/Levels/Map01/` con namespace `IronRequiem` y observa propiedades públicas, como `CorridorAlarm`.

| Componente | Dónde | Qué observa | Qué escribe | Notas |
|---|---|---|---|---|
| **`Map01RoomLighting`** | 1 por cuarto, en `[Lighting]` | `CombatTwoRoomsLab`: `WaveIndex`, `Completed`, `LivingEnemies`, `PendingSpawns` [INV C3]. `CampaignRoom`: `Completed`, `Rounds`, `Pursuit`. `ChaseCorridorEncounter`: `Running`, `Completed`, `Ambushes[].Triggered`. `CorridorAlarm`: `Alarmed`, `Cleared`. `CampaignFlow`: `Busy` | Por **grupo de luces**: multiplicador de intensidad, color, `enabled`, cambio de `sharedMaterial` emisivo entre 3–4 materiales compartidos. Peso de los Volumes de estado. Multiplicador de `RenderSettings.ambientIntensity` | Máquina de estados con curvas por grupo (§L8). Sin `Update` si no hay cambio. No escribe en `RenderSettings` mientras `CampaignFlow.Busy` (la mezcla de 1,6 s manda) y restaura al desactivarse |
| **`Map01LampFlicker`** | Luminarias con fallo | Su propio reloj (mismo algoritmo y semilla que `LightFlicker`) y la escala del grupo | `Light.intensity` y el material emisivo (par encendido/muerto) | `LightFlicker` fija `target.intensity = baseIntensity × nivel` cada 0,05 s [LF] y anularía cualquier escala de estado. Sin tocarlo, esta copia añade la escala externa y los modos «fuego» (lento), «arco» (rápido, ≤ 3 Hz en cambios grandes) y «tubo». Respeta `VisualOverhaulAblation.Has("flicker")` |
| **`LuminaireSpin`** | Balizas giratorias | — | `transform.Rotate` | Giro continuo con `Renderer.isVisible`: sin coste fuera de vista. `CorridorAlarm` solo gira las balizas en alarma [CA], y las balizas de R01 y R06 giran siempre |
| **`Map01BossLighting`** | R18 | `Map01BossEncounter` (R3): se pide que exponga `Phase` (int), `PhaseChanged` (evento), `Staggered` y `CoreOpen`. Si no existe, cae a la razón `Health` del jefe | Grupos `arena`, `fase`, `jefe`, Volume `M01_State_Boss`, ambiente | **Aviso:** `Map01BossEncounter` aún no existe; el contrato es una petición a R3 y R0 |
| **`Map01LightingAblation`** | Raíz de la sesión | Argumento `--m01-off a,b,c` (mismo patrón que `VisualOverhaulAblation`) | Desactiva grupos | `shadows`, `states`, `flicker`, `fixtures`, `probes`. Permite medir cada coste en el mismo build (§L6). Solo se activa con el argumento |

**Protocolo de peso de los Volumes de estado.**

- `SceneFade.Bind` recoge **todos** los `Volume` con `isGlobal` de la escena y guarda su peso; `Set(k)` lo multiplica [ZE]. Si el Volume de estado está a 0 al hacer `Bind`, `Set(1)` lo deja a 0: no interfiere.
- Cada Volume de estado es **global, prioridad 15** (entre el sector, 10, y los de Room00, 20/21), sin `VolumeFadeIn`.
- `Map01RoomLighting` escribe su peso en `LateUpdate` solo si `!CampaignFlow.Busy`. Durante las mezclas (1,6 s) las escribe `SceneFade`, que lo deja en 0 si estaba a 0 al hacer `Bind`; los estados se deshacen antes de salir (§L4.4).
- Alternativa descartada: un Volume local con `BoxCollider` del tamaño del cuarto no pasa por `SceneFade`, pero obligaría a poner un trigger del tamaño de la sala en la capa Default (la máscara del Volume de la cámara) y las consultas físicas con `Physics.queriesHitTriggers` por defecto lo verían **[VALIDAR F2: grep de `Physics.Raycast` sin `QueryTriggerInteraction`]**.

**Perfiles de estado** (4, en `Assets/IronRequiem/Art/Map01/Lighting/Volumes/`): `M01_State_Combat`, `M01_State_Alarm`, `M01_State_Blackout`, `M01_State_Boss`. Cada uno solo sobreescribe `ColorAdjustments` (postExposure, contraste, saturación, filtro) y `Vignette` (intensidad y color). Sin Bloom, sin grano.

### L4.2 Estados generales de un cuarto con rondas

| Estado | Condición (propiedades públicas) | Grupo `techo` | Grupo `salida` | Volume `M01_State_Combat` | Ambiente (`ambientIntensity`) |
|---|---|---|---|---|---|
| **Normal** (antes de la ronda 1) | `WaveIndex < 0` | 100 % | 100 % hasta la ronda; luego 30 % | 0 | 1,0 |
| **Combate** (todas las rondas, sin pausa entre ellas) | `WaveIndex ≥ 0` y `!Completed` | 92 % | 30 % (la salida se apaga: aún no se puede salir) | → 0,6 en 1,0 s: contraste +4, viñeta 0,084 → 0,124 con color casi negro rojizo (0,06; 0,02; 0,02), saturación 0 | 1,0 |
| **Alarma** (solo R07; opcional R16 y R18) | `CorridorAlarm.Alarmed` | 55 % | 30 % | `M01_State_Alarm` → 0,7 | 0,9 |
| **Ronda terminada** | `Completed` | → 100 % en 1,2 s, encendido en cascada hacia la salida | **30 → 100 % en 0,8 s** | → 0 en 1,2 s | → 1,0 |

- **Entre rondas del mismo cuarto no cambia nada** (las rondas son seguidas, sin pausa [MD §3.4]). Los estados siguen a `WaveIndex` solo en R07 y R16.
- Las curvas son **suavizadas** (SmoothStep) y de ≥ 0,4 s. Ningún cambio grande de área supera 3 Hz [PU P2.1, WCAG 2.3.1].
- `PendingSpawns` > 0 con `LivingEnemies` = 0 (cola de aparición) cuenta como combate: se evita que el estado «ronda terminada» salte entre la muerte del último enemigo y la aparición del siguiente.

### L4.3 Alarma de R07

Reutiliza `CorridorAlarm` **sin cambios** para sirena, balizas y pantallas (se dispara con `WaveIndex ≥ 0` y se limpia con `Completed` [CA]). `Map01RoomLighting` añade lo que `CorridorAlarm` no hace: baja la luz fría, sube el Volume y gestiona el apagón.

| Ronda de R07 | Luces | Post y ambiente | Legibilidad |
|---|---|---|---|
| 1 (disparo) | `CorridorAlarm` enciende rojas (L6, L7) y baliza (L5); `techo` 100 → 55 % en 0,8 s | Alarma 0,7; ambiente 0,9 | Rojas en tabiques y mástil, no a la altura de los ojos; verde de terminales intacto |
| 2–3 | Igual | Igual | `techo` ≥ 45 de luminancia en el anillo |
| última (4.ª o 5.ª) | **Apagón parcial** (§L4.5-1): L1 y L3 a 0 %, L2 y L4 a 15 % con parpadeo ≤ 3 Hz, rojas y baliza igual | Alarma 0,85; ambiente 0,60 | Anillo de consolas verdes, baliza roja barriendo y L5 con sombra suave; mín. 45 en el carril de combate |
| `Completed` | `CorridorAlarm` apaga rojas; reconexión en cascada L1→L4 (0,15 s entre luces), salida 30 → 100 % | Alarma → 0 en 1,2 s | La pantalla «ZONA DESPEJADA» (de `CorridorAlarm`) usa el verde de `clearText` |

- **Alas:** al bajar una persiana, la luz del ala (2 tiras verdes) se apaga por `Wings/<N|S|E|W>` para que la sala central no pague su render.
- La sirena, el klaxon y las pantallas son de `CorridorAlarm`; R5 no los cambia.

### L4.4 Ronda terminada y salida de un cuarto

1. `Completed` = true (propiedad pública de `CampaignRoom`/`CombatTwoRoomsLab`).
2. Se deshacen todos los estados: apagones, alarma, Volumes de estado (→ 0 en 1,2 s), `ambientIntensity` → 1.
3. Cascada de luz hacia la salida (`techo` y luces de ruta) y foco de umbral al 100 %.
4. Luego R4 pasa la puerta a ABRIÉNDOSE → ABIERTA (verde) [PU P2.2].
5. Cuando `CampaignFlow.Busy`, la máquina se detiene y no escribe en `RenderSettings`; la mezcla de ambiente y Volumes la hace `CampaignFlow`.

### L4.5 Apagones dramáticos

**Reglas comunes:** nunca en la primera ronda de un cuarto; nunca mientras `CampaignFlow.Busy`; ≤ 45 s; `ambientIntensity` ≥ 0,5; el carril de combate, los spawns y los puntos de recogida conservan ≥ 45 de luminancia; ≥ 2 grupos de guía encendidos (emisivos del suelo, balizas, tiras); la salida conserva el 30 %. Los cambios de luz se hacen por curvas, no por saltos.

| # | Dónde | Disparador | Qué cae | Qué queda (legibilidad) | Duración | Cómo termina |
|---|---|---|---|---|---|---|
| 1 | **R07**, última ronda | `WaveIndex` = última ronda de R3 (campo `blackoutFromWave` en los datos) | L1 y L3 a 0 %; L2 y L4 al 15 % con parpadeo | Tiras verdes de consola, baliza roja de L5 con sombra, rojas de L6–L7, ojos de robots, ambiente 0,60 | La ronda, con tope de 45 s | `Completed`: reconexión en cascada |
| 2 | **R10**, al empezar la persecución | `ChaseCorridorEncounter.Running` pasa a true | L1 (bahía) parpadea 3 veces y cae; ambiente 0,70 | Franjas rojas y ámbar cada 10 m (HDR ≤ 2,0), brillos de grieta y ámbar de emboscadas | La persecución | `Completed` o fin del cuarto |
| 3 | **R16**, ronda 3 | `WaveIndex` = 2 | `techo` al 25 % con 3 tropiezos de ≤ 1,5 Hz en 2 s | **El núcleo sube a I 90 y pulso 1 Hz** (sobrecarga), ámbar de torres, ojos | La ronda, tope 25 s | Fin de ronda |
| 4 | **R18**, fase final del jefe | Fase 3 (§L4.8) | `arena` al 55–60 %, ambiente 0,65–0,70 | El **núcleo del jefe** (A), la boca del crisol (B) o los anillos (C) son el punto más luminoso; acentos rojos latiendo | La fase | Muerte del jefe |

**Fuera de plan** (D-L3 c): R12 (ya con 2 tubos rotos), R13 y R17 (respiros). R17 puede usar el apagón 1 si R3 activa el E8 opcional.

### L4.6 Cuartos sin ronda

- **R05, R09, R13, R17:** luces constantes y salida encendida desde el principio. El `Map01StageRelease` (volumen junto a la salida) no cambia la luz.
- **R10 (persecución):** estados `normal` (bahía fría) → `apagon` (arranca la persecución, §L4.5-2) → `completado`: la puerta Z «iluminada en verde al terminar» [E] es de R4; R5 sube las franjas del último tramo a 100 %.
- **Esclusas:** el relleno es fijo. El ciclo ámbar/verde es de R4 (`AirlockCycle`).

### L4.7 Transición a la arena del jefe (R17 → C17 → R18)

| Tiempo | Ocurre | Luz |
|---|---|---|
| Salida de R17 | Última sala respiro | Salida blanca al 100 %; ambiente tenue (−0,15 EV) |
| C17 (Z) | Cruce | Relleno (0,90; 0,94; 1,0), I 9, y ciclo de R4 |
| Entrada a la antecámara (escena R18 cargada; el ambiente se mezcla 1,6 s) | La escena R18 se **autora con ambiente 0,45 y las luces de `arena` apagadas** (`enabled = false`: no cuentan para el presupuesto) | La antecámara la ilumina su anclaje propio (0,70; 0,80; 1,0), I 14, y los 4 botiquines |
| Puerta B empieza a abrir (R4: ABRIÉNDOSE) | `Map01BossLighting` lanza el **encendido en cascada** | t + 0,0 s: se enciende la estructura central (L7) · t + 0,4 s: L1 y L2 · t + 0,8 s: L3 y L4 (con sombra) · t + 1,6 s: completo. Ambiente 0,45 → 1,0 en 2 s |
| El jugador cruza B (≥ 4 m detrás del plano, R4 AT-6) | `BossGateSequence` cierra B | Los acentos de fase siguen a 0 |
| Pelea | Empieza en los *bounds* de `Map01BossEncounter` | Fase 1 de §L4.8 |

El encendido escalonado cumple tres fines: revela primero el landmark (jerarquía de §L1.1), oculta el *pop* de las luces nuevas y respeta el límite de 3 Hz. El corte del ambiente se autora (no se anima desde fuera) para que `CampaignFlow` lo mezcle sin saltos.

### L4.8 Fases del jefe (los tres conceptos de [ENC A7])

Común: grupos `arena` (L1–L4), `fase` (L5–L6, acentos rojos, I 0 → 35, r 18) y `jefe` (L7). El Volume `M01_State_Boss` sube con las fases: fase 2 → 0,4; fase 3 → 0,8 (exposición −0,20; contraste +6; saturación +4; viñeta 0,20 casi negra rojiza). Luz móvil del jefe = **transitoria #1**. Con refuerzos y explosiones se superaría el tope de 2 transitorias [BFL] y [INV §5]: se pide a R0 un tope de 3 en R18 y se mide (§L6).

**A · «El Custodio» (recomendado por R3)**

| Fase | `arena` | `fase` (rojo) | L7 (estructura central) | Ambiente | Volume boss | Transición |
|---|---|---|---|---|---|---|
| 1 «Enchufado» (100–67 %) | 100 % | 0 | Spot blanco-azul (0,75; 0,85; 1,0), I 40, r 22, sobre el anclaje de los cables; chispas con modo «arco» | 1,0 | 0 | — |
| 1 → 2 (arranca los cables, 2 s, invulnerable, onda) | Cae al 30 % en 0,15 s y vuelve al 85 % en 1,0 s | Sube a 35 en 0,6 s | Chispazo (I 120 durante 0,12 s: **transitoria**); luego se apaga | 0,9 | → 0,4 | Una sola caída: sin parpadeo repetido |
| 2 «Suelto» (67–34 %) | 85 % | 35, pulso 0,5 Hz | Apagada | 0,9 | 0,4 | 1 → 2 cañones |
| 2 → 3 (cae el cañón derecho) | → 60 % | → 60 | — | → 0,70 | → 0,8 | — |
| 3 «Núcleo» (34–0 %) | 60 % | 60, latido 1 Hz | Luz propia del jefe (naranja-blanco (1,0; 0,70; 0,40), I 40, r 12, **transitoria #1**) sobre el núcleo expuesto: punto más luminoso | 0,65–0,70 | 0,8 | Apagón dramático #4 |
| Muerte (`HeavyCollapse`, 1,8 s) | Tiembla ±10 % | 100 → 0 | Explosión | → 1,0 | → 0 en 3 s | Vuelta al 100 %: suelo y armas caídas visibles |

Pilares de Ø 3,5 (6): las 2 sombras de R18.L3–L4 las recortan y el jefe proyecta su sombra: legibilidad de la embestida.

**B · «La Matriarca»**

| Fase | `arena` | `fase` | L7 = **boca del crisol** (anclaje en la pared N) | Volume boss |
|---|---|---|---|---|
| 1 «Producción» | 90 % | 0 | Naranja (1,0; 0,40; 0,10), I 25, r 26, brillo constante | 0 |
| 2 «Brazos» | 85 % | 30 | I 40; chispas de sierras (transitorias) | 0,4 |
| 3 «Crisol» | 70 % | 40, latido 1 Hz | **Pulso sincronizado con la ventana de vulnerabilidad** (3 s cada 10 s [SUPUESTO de R3]): abierta → I 110, r 30, blanco-naranja y el color de `arena` se tiñe un 25 % (1,0; 0,85; 0,70); cerrada → I 25 | 0,8 |

- Las tolvas son emisivas sin `Light` (parpadean 0,5 s en ámbar al soltar una camada): ni cuentan, ni se pasan del presupuesto.
- La luz «avisa» cuándo hacer daño: color y brillo de la boca son el *tell* principal. No hay otro rojo en la sala durante la ventana.

**C · «El Panóptico»**

| Fase | `arena` | `fase` | L7 = luz del anillo activo (se desplaza en Y entre 4, 10 y 16 m) | Suelo (emisivo, sin `Light`) |
|---|---|---|---|---|
| 1 anillo inferior | 100 % | 0 | Roja (1,0; 0,20; 0,15), I 50, r 22, a y 4 | Frío |
| 2 anillo medio + suelo electrificado | 80 % | 20 | Misma luz a y 10 | **Rejilla azul-blanca pulsando a 1 Hz** (`M01_Lum_Blue`): telegrafía el suelo electrificado |
| 3 corona (núcleo superior) | 55 % | 50 | Blanca (1,0; 0,95; 0,90), I 70, r 24, a y 16 | Frío |

- Los niveles inferiores quedan oscuros adrede en la fase 3 (oscuridad intencional) con ≥ 45 en las repisas de recogida.
- El «ojo-sensor» de cada anillo es un emisivo rojo (`M01_Lum_Red`) cuya apertura (blanco) marca la ventana.

---

## L5. Técnica URP

### L5.1 Horneado, mixto o tiempo real

| Opción | Qué es | Coste | Riesgo | Veredicto |
|---|---|---|---|---|
| **A. Tiempo real total + reflection probes horneadas** | Hoy, más probes por cuarto | 0 horneado de luz; 21 probes (≈ 0,30 ms por objeto muestreado [INV E3 #6]); luces: CPU de culling y de cálculo por objeto | Límite de 4 luces por objeto; cada luz tiene coste de CPU por frame | **Recomendada** |
| B. Mixed (*Baked Indirect*) + light probes | Como el laboratorio de VO [VO §3] | Light probes ≈ 0,13 ms por objeto interpolado; hornear 18 escenas; las luces siguen en tiempo real | Sin lightmaps, el beneficio es solo el rebote en enemigos; Mixed y probes obligan a LightingData por cuarto y a escenas de cuarto cargadas aditivamente | Prueba A/B en R14, R16 y R18 en F5 |
| C. Lightmaps de estáticos | UV2 + variantes `LIGHTMAP_ON` + Mixed | Alto: los 226 cubos de `BunkerBox` y los kits no garantizan UV2; añade variantes y memoria por cuarto; el cuarto se carga/descarga 18 veces | Alto: cambia el aspecto de las cargas (CPU-bound en la transición [INV E3 #8]) | **Descartada** |

Razones de A:

1. El cuello de botella es la CPU y la GPU va sobrada [INV §5] (p99 de GPU 4,8–5,6 ms [INV E2]). Pagar luz por píxel es barato; pagar sombras y muestreos por objeto, no.
2. Los enemigos, el jugador y las armas soltadas son dinámicos: necesitan luz directa en tiempo real [BP §5] (`Sin luz horneada`).
3. [VO §3] ya decidió no generar lightmaps (los modelos de plantilla no garantizan UV2 y los lightmaps añadían variantes `LIGHTMAP_ON`).
4. El rebote se aproxima con el ambiente trilight (ground cálido).

### L5.2 Sombras

- Solo en **spots** (1 tile del atlas). Un punto con sombra son 6 caras.
- Atlas de luces adicionales 1024 [INV E1]; con 2 spots cada uno queda a 512 px (R18). `m_ShadowAtlasResolution: 256` de [INV E1] **tiene un significado no verificado**: comprobar en F2 qué tamaño de tile recibe un spot.
- Distancia de sombras 50 m, 1 cascada, soft shadows [INV E1]: la arena de 72 m queda cubierta desde el centro, pero no desde la antecámara. Las sombras de R18 empiezan en la fase de encendido, ya dentro.
- Sin sombra de luz principal (no hay directional en las salas) y sin sombras en emergencias, balizas, relleno ni luces de efectos.
- Cada luz con sombra se apaga con `enabled = false` cuando el estado no la necesita (R07.L5 en reposo): una luz apagada no cuesta.
- `shadowStrength` 0,85, `shadowBias` y `shadowNormalBias` de URP por defecto; `shadowNearPlane` 0,5; se ajustan por captura en F2.

### L5.3 Emisivos

- Todos los emisivos son del shader `IronRequiem/HardSurface` con `_PANELS` (patrón de `VO_Light_*`) y **materiales compartidos**; los cambios de estado son **cambio de `sharedMaterial`** y no `MaterialPropertyBlock`, para conservar el SRP Batcher (mismo criterio que [PU P1.0]).
- HDR ≤ 4,0 (lava ≤ 5,0): con bloom de umbral 1,0 el halo no llega a ocupar un tercio de la pantalla (C1 de [VA]).
- Un emisivo no ilumina: cada luminaria con `Light` lleva un emisivo que la explica (corrige «lámparas que no explican la luz» [VA 9]). Las luminarias con solo emisivo (franjas, tiras, pantallas, ojos) son fuente de lectura, no de iluminación.
- Colores reservados al estado de puerta (rojo, ámbar, verde de R4): ningún emisivo de R5 con esos colores a menos de 6 m de un marco.

### L5.4 Probes y reflejos

- **Reflection probes:** ≤ 2 por cuarto, horneadas, `Box Projection` (ya activo en el pipeline [INV E1]); `realtimeReflectionProbes: 0`. 21 en el mapa (§L3.21).
- **Light probes:** ninguno en F2 (D-L1). Medición en F5: `--vo-off lprobes` ya existe para el A/B [VO].
- **Reflejo por defecto** de las escenas: 1,1 [INV E1]. Sin cielo visible (techos y niebla de color); el reflejo por defecto de la escena es el del Skybox de proyecto: **[VALIDAR F2]** que no refleje un cielo luminoso en los cuartos sin probe (S00 y las esclusas).

### L5.5 Niebla y ambiente

- Exp² (modo 3), densidad ≤ 0,016, color de valor medio (§L2.2). Más densa que 0,016 «ensucia» los carriles de visión de 43,75 m de las torretas [E]; en las arenas anchas (R11, R18) ≤ 0,010.
- El color de niebla funciona como telón para el contraluz de los robots (§L1.2).
- Todos los cuartos usan `AmbientMode.Trilight` (valores §L2.2).

### L5.6 Volumes de zona sobre `Campaign_PostProfile`

| Capa | Prioridad | Contenido |
|---|---|---|
| `Campaign_PostProfile` (Volume del jugador) | 0 | ACES; bloom 0,9/0,1/0,7/12; grano Thin1 0,3 · 0,8; viñeta 0,084 morada; MB `active 0` [R2A §5] |
| `Sector_M01_<Zona>` (global, `VolumeFadeIn`) | 10 | ColorAdjustments + WhiteBalance (§L2.2) + bloom (umbral 1,0, intensidad 0,12–0,18, scatter 0,55–0,60, clamp 12) + **grano 0,18** + **viñeta** con color neutro |
| `M01_State_*` (global, escrito por `Map01RoomLighting`) | 15 | Solo ColorAdjustments y Vignette (§L4.1) |
| `HG_*` (zona protegida, solo Room00) | 20 / 21 | No se usan en Map01 |

- Las copias se hacen desde `Sector_Room0*` (libres); ningún perfil `HG_*` se copia ni se referencia.
- **Tonemapping no se toca** (ACES): el enum no se interpola [R2A].
- Nunca se añaden DoF, motion blur, aberración cromática, lens distortion ni Panini a un Volume de zona.

### L5.7 Qué evita reabrir IR-03 (nitidez)

[R2A §5] concluye que la causa de blur real que queda es la resolución de presentación (1600×900 estirada) y que el grano y el aniso «suman». Plan de R5:

| Regla | Valor |
|---|---|
| Sin DoF, MB, aberración, Lens, Panini, halos ni lens flare en los Volumes y en las luminarias | Siempre |
| Bloom | Umbral ≥ 1,0; intensidad ≤ 0,2; scatter ≤ 0,6; clamp ≤ 12; sin `dirt` |
| Grano | Thin1 ≤ 0,2 (D-L4, hoy 0,3) |
| Niebla | ≤ 0,016 (≤ 0,010 en arenas anchas) |
| Haces y conos | Aditivos, alfa baja, sin soft particles ni desenfoque; ≤ 2 por cuarto |
| Cámara, AA y render scale | No se tocan (AA None en runtime; render scale 1) [RP §1] |
| Dónde se juzga | **Solo en 1920×1080 exclusiva** (`-irFullHD`); el 1600×900 invalida cualquier comparación [RP §3, E3 #10] |
| Verificación de F2 | Captura 1080 del mismo encuadre con y sin Volumes de zona: **varianza del laplaciano ≥ 90 %** de la de `Campaign_PostProfile` sola [SUPUESTO: umbral por calibrar] y comparación visual por el usuario |

SSAO (R4′ de [RP]) y el prepass DepthNormals no son de R5, pero el AO se suma al ambiente de los rincones (§L1.3): cualquier cambio de SSAO obliga a recomprobar los mínimos de §L1.3.

---

## L6. Rendimiento

### L6.1 Presupuesto

| Concepto | Límite de [INV §5] | Plan de R5 por cuarto | Global |
|---|---|---|---|
| Luces estáticas visibles | ≤ 8 | 5–8 (R03, R06, R07, R10, R11, R12, R14, R15, R16 y R17 = 8) | 151 en el mapa; nunca más de un cuarto cargado a la vez |
| Luces con sombra | ≤ 2 | 0–1 (R18: 2) | 8 como tope; 4 activas por defecto |
| Luces transitorias de efectos | ≤ 2 | 2 (`BurstFlashLight.MaxLit` = 2) [BFL]; R18 pide 3 | — |
| Luces por objeto | ≤ 4 | Rejilla con `r ≈ 0,8–1,0 ×` separación (§L3.0); R16, R02 y R10 revisados | Validador de §L8 |
| Reflection probes | ≤ 2 | 1 (2 en R10, R12 y R18) | 21 |
| Light probes | solo dinámicos | 0 | 0 |
| Emisivos distintos por cuarto (SetPass) | ≤ 200 SetPass [INV §5] | ≤ 6 materiales de R5 por cuarto | Compartidos entre cuartos |
| Triángulos de luminarias | ≤ 400 k visibles [INV §5] | ≤ 4 k por cuarto (§L7) | < 70 k en todo el mapa |
| Decales | ≤ 24 | — (R2) | — |

### L6.2 Coste estimado

Nada está medido en Release para la campaña [INV E2]. Cifras **[SUPUESTO]** por comparación con lo medido en [VO §6] y [INV E3]:

| Fuente de coste | Estimación por cuarto | Por qué |
|---|---|---|
| 8 luces sin sombra, tiempo real | +0,15–0,30 ms de CPU (selección y datos de luz por objeto); GPU +0,1–0,3 ms a 1080p | Hoy 4 por sala; [VO §6] mide 0,2–0,3 ms por grupo de efectos |
| 1 spot con sombra | +0,3–0,8 ms de CPU (pase de sombras con los *casters* dentro del frustum) y +20–60 draws | Hoy ninguna sala de campaña tiene sombra de luz adicional [RP §4] |
| `Map01LampFlicker` ×2–4 | < 0,02 ms | `LightFlicker` ya a 20 Hz sin GC [LF] |
| `Map01RoomLighting` | < 0,01 ms | Máquina por eventos, sin `Update` en reposo |
| Reflection probes | +0,3 ms por objeto muestreado, según [INV E3 #6] | Una caja por cuarto, igual que hoy |
| Bloom con umbral 1,0 | ≈ 0 respecto a hoy | Mismo `Bloom` que `Campaign_PostProfile` |

Total de luz por cuarto **≤ 0,8 ms de p99** en combate y **≤ 0,3 ms** sin sombras. Criterio IR-04: media ≤ 8,33 ms, p99 ≤ 11 ms, 0 frames > 33 ms [INV §5]. R16, R18 y R14 (los más cargados) son los 3 cuartos de la prueba en Release [E].

### L6.3 Qué se mide en F2

1. **Protocolo** (A/B/A/B, 3 pasadas, medianas) con la ronda más cargada de cada cuarto: `-irAllRounds`, `-irBurst`, `-irFire`, `-irSustain` en 1920×1080 exclusiva [INV E4.0, RP §5]; plan de energía y GPU registrados.
2. **Variantes con `--m01-off`:** (a) todo; (b) `shadows`; (c) `fixtures` (solo ambiente y emisivos); (d) `flicker`; (e) `probes`. La diferencia (a) − (c) da el coste de las luminarias; (a) − (b), el de las sombras.
3. **Frame Debugger** en un cuarto de 8 luces y en R16: draws, SetPass, y el *shadow pass*.
4. **Validador** (§L8): recuento de luces por objeto (esfera contra *bounds*) y lista de objetos con ≥ 5.
5. **Capturas** de luminancia con tarjetas grises del 18 % en los slots de spawn, carriles, puntos de recogida y botiquines (§L1.1).
6. **Tiempo de GPU:** `enableFrameTimingStats` está a 0 en el build de tour; se necesita la variante `BuildGameTimed` propuesta en [RP §5].
7. **Transiciones:** el relleno de esclusa y la entrada de luces del cuarto nuevo no deben subir el pico (≤ 33 ms, 0 frames > 50 ms [INV §5]).

---

## L7. Luminarias nuevas para F3

**Reutilizar primero** (A2.2 de [INV]): `VO_CeilingLight` (24 tris, panel 2,5×0,55), `VO_CeilingLightHousing` (12), `VO_CeilingLightLens` (12), `VO_EmergencyLight` (108), materiales `VO_Light_Cold`, `_Amber`, `_Red` y `_Dead` (HardSurface con panel emisivo; no están en la lista roja). Las mallas son procedurales de `VisualOverhaulAssets.BuildPropMeshes()`: no hay prefabs, así que F3 crea prefabs nuevos en `Assets/IronRequiem/Prefabs/Map01/Lighting/`.

| ID | Luminaria | Base | Tris | Luz | Material emisivo | Se usa en |
|---|---|---|---|---|---|---|
| LUM-01 | Panel fluorescente simple | `VO_CeilingLight` | 24 | P | `VO_Light_Cold` (par muerto: `VO_Light_Dead`) | Apoyo en cualquier cuarto |
| **LUM-02** | **Plafón fluorescente enjaulado** | `VO_CeilingLight` + jaula de barrotes nueva (72 tris) | 96 | P | `VO_Light_Cold`, o `M01_Lum_Sodium` / `M01_Lum_Cyan` según zona | R01, R03–R05, R07, R09–R13, R15–R17, S00 |
| **LUM-03** | **Campana de sodio colgante** (cono, cadena, vidrio) | Nueva | 180 | P | `M01_Lum_Sodium` | R02, R06, R08, R14 |
| LUM-04 | Luz de emergencia enjaulada de pared | `VO_EmergencyLight` | 108 | P o S | `VO_Light_Red`, `VO_Light_Amber` o `M01_Lum_White` | Mural R03, tabiques R07, ruta en muchos cuartos |
| **LUM-05** | **Baliza giratoria** (base, domo y cabeza rotatoria de 60 tris) | Nueva | 220 | P o S | `VO_Light_Amber` / `VO_Light_Red` | R01, R06, R07 (plato del radar), R18 |
| **LUM-06** | **Foco de landmark** (cuerpo cilíndrico, visera, soporte) | Nueva | 140 | S | `M01_Lum_White` o de zona | S00, R02, R08, R11, R14, R15, R18 |
| LUM-07 | **Tira emisiva** de 10 m (quad) y de borde de caída | Nueva (12 tris por tira; 40 unidades por malla combinada) | 12 | Sin `Light` (o con una en R17) | `M01_Lum_Red`, `_Amber`, `_Green`, `_Cyan`, `_White` (versión «tenue» y «viva») | R07 (anillo), R10 (franjas), todas las caídas, R17 |
| LUM-08 | **Luz de terminal** (pantalla) | Nueva (el prop es de R2; R5 aporta los materiales) | 24 | Sin `Light` | `M01_Lum_Green`, `M01_Lum_Blue` | R07, R17 |
| LUM-09 | **Lámpara de trabajo portátil** (trípode y foco) | Nueva | 150 | P | `M01_Lum_White` cálido | R04, R09 |
| LUM-10 | **Lámpara quirúrgica** (anillo y 6 lentes) | Nueva | 420 | S | `M01_Lum_White` | R05 |
| LUM-11 | **Luz de estado de bomba** (lente pequeña) | Nueva | 36 | P r 4 | `M01_Lum_Green` | R13 |
| LUM-12 | **Luz de soldadura / arco** | Nueva (casquillo, sin malla en la luz) | 24 | P | `M01_Lum_White` azulado | R15 |
| LUM-14 | **Cono de haz** aditivo | Nueva | 24 | Sin `Light` | Aditivo `M01_Beam` | R08 (2) |
| **LUM-15** | **Foco de umbral** (spot blanco sobre el dintel) | `LUM-06` en pequeño | 96 | S 80° | `M01_Lum_White` | Salida de **todos** los cuartos |
| **LUM-16** | **Panel alto de arena** (3×3 m, jaula, cadena) | Nueva | 260 | P o S | `M01_Lum_White` | R18 |
| LUM-17 | **Luz sumergida** (disco emisivo bajo el agua o en foso) | Sin malla o disco de 24 tris | 24 | P | `M01_Lum_Cyan`, `M01_Lum_Red` o ámbar | R10, R11, R12 |

**Materiales nuevos (6, compartidos entre cuartos; HardSurface con `_PANELS`):** `M01_Lum_Sodium` (HDR 3,0), `M01_Lum_White` (3,0), `M01_Lum_Green` (2,5), `M01_Lum_Cyan` (3,0), `M01_Lum_Blue` (2,5) y `M01_Lum_Lava` (HDR 5,0); más 1 aditivo `M01_Beam` para los haces. Cada uno tiene una variante «tenue» (emisión × 0,35, sufijo `_Dim`) para franjas y estados apagados: 12 materiales en total. Rojo y ámbar usan `VO_Light_Red` y `VO_Light_Amber`.

**Presupuesto de triángulos:** ≤ 4 k por cuarto (p. ej. R14: 4 campanas × 180 + 2 horno (sin malla) + 1 foco 140 + 1 umbral 96 + tiras 480 = ≈ 1,4 k; R10: 2 mallas de franjas × 240 + 3 emergencias × 108 + bahía 96 + umbral 96 = ≈ 1,1 k). **≈ 60 k en todo el mapa**, frente a los 400 k visibles por vista de [INV §5] (por vista, nunca > 4 k). Los LOD no se prevén: las luminarias son < 400 tris.

**Reglas de construcción de prefabs (F3):**

- Cada prefab es un hijo de `[Lighting]` con: malla (estática salvo giratoria y parpadeante), `Light` configurada y, si hace falta, `LuminaireSpin` o `Map01LampFlicker`.
- Pivote en el punto de anclaje (techo = cara superior del soporte; pared = cara trasera; suelo = centro).
- Sin colliders. Capa Default. Las mallas estáticas llevan `BatchingStatic`; las giratorias, no.
- Materiales con instancing donde el shader lo admita y **sin instancias de material en runtime** (`sharedMaterial` siempre).
- Los emisivos y los prefabs se **crean con APIs del Editor** (`PrefabUtility`), con `Undo`, y no se edita el YAML de escenas a mano [AGENTS.md].

---

## L8. Datos de F2/F3 y `LightingAuthoring`

### L8.1 Esquema `Assets/IronRequiem/Levels/Map01/Data/Lighting/R<NN>.json` (y `C<NN>.json`, `S00.json`)

Una entrada por ID, coordenadas **locales** al cuarto (el origen de mundo está en los datos de Estructura [E §12]). Esquema v0; el coordinador lo fija al cerrar F1.

| Campo | Tipo | Descripción |
|---|---|---|
| `room`, `zone`, `version` | texto, texto, int | Ej.: `R07`, `Comunicaciones`, 0 |
| `environment` | objeto | `ambientMode` (`Trilight`), `sky`, `equator`, `ground` (RGB 0–1), `ambientIntensity`, `reflectionIntensity`, `fog` {`mode` `ExponentialSquared`, `color`, `density`} |
| `volumes` | objeto | `zone` (perfil), `states[]` (perfiles de estado), `priority` de cada uno |
| `fixtures[]` | lista | `id` (`R07.L1`), `anchor` (ID de R1), `lum` (`LUM-02`), `prefab`, `pos` [x,y,z], `rotEuler`, `light` {`kind` P/S, `color` [r,g,b], `kelvin`?, `intensity`, `range`, `spotAngle`, `shadow` none/soft, `mode` `Realtime`}, `emissive` (material), `group`, `role` (`ambient`, `route`, `landmark`, `exit`, `alarm`, `accent`, `status`, `reserve`), `flicker` {`mode`, `seed`, `outageChance`}?, `spin` {`degPerSec`}?, `subspace`?, `enabledAtStart` |
| `emissiveStrips[]` | lista | `id`, `mesh`, `material`, `pos`, `length`, `count`, `spacing`, `group` |
| `groups[]` | lista | `name`, `fixtures[]`, `states` {`normal`, `combat`, `alarm`, `cleared`, `blackout`, `arena`, `fase1..3`: {`scale`, `color`?, `enabled`, `emissive`}}, `fadeSeconds` |
| `events[]` | lista | `id`, `trigger` {`kind` `wave`/`chase`/`phase`/`enter`/`gate`, `index`, `volume`?}, `type` (`blackout`/`ignite`/`pulse`), `duration`, `ambientFactor`, `groupsOff[]`, `groupsOn[]`, `stagger`, `maxSeconds` |
| `reflectionProbes[]` | lista | `center`, `size`, `resolution`, `importance`, `boxProjection` |
| `budget` | objeto | `maxLights` (8), `maxShadowLights` (2), `maxPerObject` (4) |
| `refs` | objeto | `corridorAlarm` (R07), `rounds`, `chase` (R10): nombres de GameObject de [Encounters] |

Los grupos y eventos de cada cuarto están en las §L3 y §L4; el JSON los materializa.

### L8.2 Reglas de `LightingAuthoring.cs` (Editor, `Assets/IronRequiem/Editor/Map01/`)

1. **Capa propia:** solo crea, modifica y borra hijos de la raíz `[Lighting]` del cuarto (`C<NN>/[Lighting]` en esclusas). No toca `[Structure]`, `[Decor]`, `[Encounters]` ni `[Doors]`.
2. **Idempotente:** reconstruye por ID (`R07.L1`); ejecutarlo dos veces no duplica ni cambia nada si no hay cambios en los datos. Los objetos se buscan por nombre estable, no por índice.
3. **Undo:** `Undo.RegisterCreatedObjectUndo`, `Undo.RecordObject` y `Undo.DestroyObjectImmediate` en cada cambio; un único grupo de `Undo` por ejecución.
4. **Sin YAML a mano:** `PrefabUtility.InstantiatePrefab`, `EditorSceneManager` y las APIs del Editor; no guarda una escena que ya estuviera modificada por el usuario [AGENTS.md]. Escribe `RenderSettings` (ambiente, niebla) del cuarto como datos de entorno.
5. **Respeta la zona protegida:** no abre ni referencia `Room00_Hangar`, `IntroMenu`, `HG_*` ni `MenuWalker_*`.
6. **Anclajes de R1:** solo coloca en posiciones de `Data/Structure`; tolerancia 0,25 m; error si una luz cae en un volumen libre de puerta.
7. **Flags:** mallas `BatchingStatic | ReflectionProbeStatic | OccludeeStatic` (no `ContributeGI`); luces `lightmapBakeType = Realtime`.
8. **Respaldo:** antes de cambiar configuración, copia los archivos afectados a `Tools/IronRequiem/Backups/<fecha>/` (carpeta ignorada) [AGENTS.md].
9. **El horneado lo ejecuta el coordinador** (R0): reflection probes de cada cuarto (`Lightmapping.BakeReflectionProbe`); `LightingAuthoring` solo las crea y configura.

### L8.3 Validador (se ejecuta tras `LightingAuthoring` y en el validador de la sesión de R0)

| Regla | Severidad |
|---|---|
| ≤ 8 luces de `[Lighting]` por cuarto; ≤ 2 con sombra; sombras solo en spots | Error |
| ≤ 2 reflection probes por cuarto; light probes = 0 | Error |
| Luces por objeto: para cada `Renderer` (excepto UI y partículas), contar luces cuya esfera (posición, `range`) corta sus *bounds*: ≥ 7 → error; 5–6 → aviso con distancia/alcance de cada una | Error / aviso |
| Ninguna luz dentro de un volumen libre de puerta (VL de Estructura) | Error |
| Ninguna luz roja, ámbar ni verde con saturación > 0,5 a < 6 m de un marco de puerta (R4); la luz de salida es blanca | Error |
| Toda luz de «salida» existe y mira al marco de salida | Error |
| Cada centro de spawn, cada punto de recogida de armas y cada botiquín tiene una luz a ≤ 0,6 × `range` | Aviso |
| Ninguna luz con `lightmapBakeType` ≠ Realtime, ni `Light.cookie`, ni `renderMode` Important, salvo salida | Error |
| Emisivo HDR ≤ 4,0 (lava ≤ 5,0); materiales de R5 compartidos (sin instancias) | Error |
| Presupuesto de triángulos de luminarias ≤ 4 k por cuarto | Aviso |
| Cada cuarto con `CombatTwoRoomsLab` tiene `Map01RoomLighting` con grupos `techo` y `salida` | Error |
| R07: `CorridorAlarm` configurado con las luces L5–L7 | Error |
| Volumes de zona: sin DoF, MB, CA, Lens ni Panini; bloom dentro de §L5.7 | Error |

---

## L9. Riesgos

### L9.1 Riesgos

| # | Riesgo | Probabilidad / impacto | Mitigación |
|---|---|---|---|
| 1 | **Dos mezcladores de ambiente** (`SectorLightingBlend` y `CampaignFlow`) pelean al cambiar de escena y borran la mezcla | Media / medio | D-L5: no se incluye `SectorLightingBlend` en `Map01_Campaign`; captura de la transición en F2 |
| 2 | **Más de 4 luces por objeto** en los cuartos con núcleo (R16) o pozo (R02) y pop en dinámicos | Media / medio | `r ≈ 0,8–1,0 ×` separación; ventana de atenuación; validador; ambiente trilight alto (ground cálido) |
| 3 | **Ambiente plano de nuevo:** ambiente alto o `LabSun` global | Media / alto | D-L5: sin directional; ambientes de §L2.2 bajos; medición con tarjeta gris |
| 4 | **Enemigos que se pierden** en cuartos monocromos (alarma roja, cian) | Media / alto | §L1.2: fondo más claro, banda de lectura, niebla de valor medio; captura de R07 en alarma |
| 5 | **Coste de las sombras** no medido en Release | Media / medio | Solo 4 luces activas en F2; interruptor `--m01-off shadows`; D-L2 |
| 6 | **Parpadeos y apagones** con fotosensibilidad | Baja / medio | Cambios grandes ≤ 3 Hz, rampas ≥ 0,4 s, tope de 45 s; no hay opción «reducir destellos» (pendiente global [PU P2.1]) |
| 7 | **Interferencia entre `LightFlicker` y las escalas de estado** | Alta si se reutiliza `LightFlicker` | `Map01LampFlicker` nuevo; `LightFlicker` no se toca |
| 8 | **Peso de los Volumes de estado** pisado por `SceneFade` | Media / bajo | Protocolo de §L4.1: global, prioridad 15, escrito en `LateUpdate` solo si `!Busy` |
| 9 | **IR-03:** Volumes que añaden niebla o grano percibidos como blur | Baja / alto | §L5.7; juicio solo en 1080 exclusiva; D-L4 |
| 10 | **Colores de estado de puerta** (R4) enmascarados por luces rojas de R5 | Media / medio | Regla de 6 m y salida blanca; las puertas se leen por patrón en zonas rojas [PU P2.1] |
| 11 | **Rendimiento de R16, R18 y R14** (los 3 cuartos de la prueba) | Media / alto | Estimación en §L6.2, medición en F2, `--m01-off` para aislar |
| 12 | **El jefe y los refuerzos superan el tope de luces transitorias** (2) | Alta en R18 / medio | Pedir 3 a R0; la luz del jefe es una; la explosión usa `BurstFlashLight` (máx. 2) |
| 13 | **Dependencia de componentes que no existen** (`Map01BossEncounter`, `ArenaShutter`, `Map01StageRelease`) | Media / medio | Contrato por propiedades públicas con *fallbacks* (§L4.1); se cierra en F4 |
| 14 | **Reflejo por defecto** de cielo en cuartos sin probe | Baja / bajo | Verificar en F2 (§L5.4) |
| 15 | **Calibración:** todas las intensidades son de papel | Alta / medio | Primer paso de F2: capturas con tarjeta gris y ajuste global por factor |

### L9.2 Conflictos con `01_ESTRUCTURA.md`

| # | Dónde | Conflicto | Petición |
|---|---|---|---|
| 1 | Todas las fichas | Los anclajes no tienen ID; R3 los enlaza «por ID de anclaje» (R07) | R1/R0: `R<NN>.A<n>` estables, a los que R5 mapea `R<NN>.L<n>` |
| 2 | R02, «pared galería (−14; 15; −21)» | Dentro del volumen libre de la puerta A de entrada (7×4×4,5 desde y 12) | Subir a y ≥ 17,5 o mover a x ≤ −18 |
| 3 | R06, «entreplanta (−20; 11; −10)» | Dentro del volumen libre de la puerta Z de entrada (10×6×6 desde y 6) | Mover a x ≥ −17 o y ≥ 12,5 |
| 4 | R08, «cubierta (−10; 10; 26)» | Roza el techo del volumen libre de la puerta Z de entrada (y 4…10) | y ≥ 10,5 |
| 5 | R07 | Las 4 alas no tienen anclajes de luz | O dos tiras emisivas por ala (propuesta) o 1 anclaje por ala |
| 6 | R16, «núcleo candidata a sombra» | Es una luz puntual: 6 caras del atlas | Sombra en un spot de techo, no en el núcleo |
| 7 | R18 | Antecámara sin anclajes: con B cerrada queda a oscuras; los 8 anclajes incluyen el de la puerta B (R4) | +1 anclaje de antecámara (blanco frío tenue) |
| 8 | R10 | 8 anclajes en 200 m; el resto «franjas emisivas» queda a cargo de R5 | Aceptado: 40 quads en 2 mallas (§L3.11); R2 no coloca emisivos en ese túnel |
| 9 | Varios (R05, R12, R13, R15, R16, R17, S00, R18) | Faltan coordenadas de los anclajes | R1 las fija en F2 (`Data/Structure`) |
| 10 | R08 | 1 anclaje libre para R5 | Se deja de reserva |
| 11 | Presupuesto [INV §5] y R4 | Las luces de estado de puerta y de ciclo no figuran en las 8 | D-L6: ≤ 2 visibles fuera de las 8 |

### L9.3 Coordinación con otros roles

- **R2 (Decoración):** usa los materiales `M01_Lum_*` para sus emisivos (pantallas, coladas, núcleo, ojos del jefe); no coloca `Light`. Los emisivos de lava, núcleo y holograma son materiales de R5, objetos de R2. Los landmarks (volumen de R1) se iluminan con luminarias de R5 a ≥ 0,5 m de la decoración.
- **R3 (Encuentros):** datos `blackoutFromWave` (R07, R16), `lastRound`; ventana del crisol (B) y tiempos de fase; el contrato `Map01BossEncounter` (`Phase`, `PhaseChanged`, `CoreOpen`).
- **R4 (Puertas):** colores reservados; luces de estado ≤ 5 m; `ArenaShutter` estado público; la luz de salida es de R5 y la de la puerta B es de R4.
- **R0:** tope de luces transitorias en R18; aprobación de los 5 componentes; sesión sin `SectorLightingBlend` ni `LabSun`.

---

## L10. Skills usadas

| Skill | ¿Cargada? | Qué se aplicó |
|---|---|---|
| `unity:urp-postprocessing` | Sí | Pila de Volumes (global, prioridad, `overrideState`, `VolumeProfile` por zona y por estado); `sharedProfile` frente a `profile`; ACES y HDR; bloom sobre HDR; comprobación de que la cámara tiene post-proceso y la máscara incluye la capa Default. No se ejecutó nada (la skill exige un Editor con CLI, prohibido en esta fase) |
| `gamedev:level-design` | Sí | Guiar con luz y landmarks en lugar de muros; jerarquía salida > landmark > ruta > ambiente; revelado escalonado de la Bóveda; comprobación de que ninguna luz «gatea» o bloquea un paso |
| `gamedev:shader-programming` | Sí | Rim por fresnel (`_RIM` de HardSurface), emisivos HDR sin ramas, rampas con `smoothstep` en las curvas de estado, intercambio de materiales en lugar de ramas por píxel |
| `unity:craft-game-floors` | Sí, **solo criterios de luz y acabado** (`scene-lighting`): luz clave cálida desde arriba con relleno frío, ambiente en gradiente (aquí trilight), volumen global con bloom controlado, niebla del color de la sombra, comprobación de HDR y de la cámara. No se aplicó el flujo de creación, la paleta fija ni el scorecard |
| `gamedev:performance-optimization` | Sí | Presupuesto antes de optimizar; separar CPU y GPU (CPU-bound); medir A/B/A/B en un build y sin vsync, con p99; interruptores de ablación; no confiar en cifras de Editor |
| `iron-requiem-performance` (local) | Sí | Capturas con resolución y condiciones registradas, Editor y Player separados, presupuestos derivados de medidas y no universales, materiales compartidos sin instancias, sin cambiar URP |
| `unity:validate-urp-render-graph-renderer-feature` | **No** | No se propone ninguna renderer feature ni cambio de pipeline |

Prioridad aplicada: AGENTS.md > `iron-requiem-*` > `unity:*` > `gamedev:*` > genéricas. Donde `craft-game-floors` propone un único sol direccional cálido, AGENTS.md y [VO §3] mandan (sin lightmaps ni directional global) y se descartó.
