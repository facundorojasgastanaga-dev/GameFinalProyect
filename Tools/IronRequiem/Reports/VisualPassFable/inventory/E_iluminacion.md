# Inventario E: iluminación, visibilidad y colores reservados

Fecha: 20/11/2026. Solo lectura. Sin Unity abierto, sin git, sin cambios en el proyecto. Único archivo escrito: este.

**Fuentes leídas:** `Docs/IronRequiem/Map01/05_ILUMINACION.md` (completo), `04_PUERTAS.md` (§P1.0, §P2.1, tabla de estados), `VISION_V01.md` (completo), `CONTROLS_V01.md` (solo teclas y rebinding), `DESIGN_V02_UPDATE.md`, `VISUAL_AUDIT_F0.md`, `CHECKLIST_TRABAJO.md` (líneas 312, 628, 640–700, 1541), `MAP_FX_AUDIT_20261117_r2.md` (§5, §7), `render-perf.md` (§2, §4), `PROMPTS_CHATS.md` (§11 y §12), código `LightingAuthoring.cs`, `ZoneEnvironment.cs`, `EnemyStateLights.cs`, `CorridorAlarm.cs`, `EnemyDarkReadability.cs`, `VisionMaterialRules.cs`, `Campaign_PostProfile.asset`, `Sector_*.asset`, `Default_PipelineAsset.asset`, los 18 JSON `Levels/Map01/Data/Lighting/R*.json`, las escenas `Scenes/Map01_Bunker/*` y `Scenes/Campaign/*` (lectura YAML) y el informe `Tools/IronRequiem/Reports/Map01/F2/lighting_validate.txt`.

**Método de los datos de cuarto:** los valores de ambiente, niebla, luces y probes salen de los JSON de datos, que son lo que aplica `LightingAuthoring`. Los recuentos de componentes `Light` salen de las escenas. Cada dato indica su fuente.

---

## 0. Resumen de lo que hay construido frente al documento 05

| Elemento | Documento 05 | Estado en el proyecto | Fuente |
|---|---|---|---|
| Ambiente Trilight y niebla exp² por cuarto | Sí, valores de §L2.2 | **Aplicado.** Coincide con los JSON y las escenas | `Data/Lighting/R*.json`, `LightingAuthoring.cs:257-265` |
| Luces por cuarto (anclajes de Structure) | §L3.x | **Aplicado.** 151 `Light` previstas; ver tabla 2 | `Map01_Bunker/*.unity` |
| Reflection probes horneadas (≤ 2 por cuarto) | §L5.4 | **Creadas**: 1 por cuarto (caja del AABB) y 2 en R10, R12 y R18 según el JSON. **Horneado sin comprobar** | `LightingAuthoring.cs:269-298` |
| Volumes de zona `Sector_M01_*` | §L2.2, §L5.6 | **No existen** (grep sin resultados). En Map01 solo se usa `Campaign_PostProfile` | `Assets/IronRequiem/Art` |
| Componentes de estado (`Map01RoomLighting`, `Map01LampFlicker`, `LuminaireSpin`, `Map01BossLighting`, `Map01LightingAblation`) | §L4.1 | **No existen** en `Assets/`. No hay estados de combate, alarma, apagones ni fases | grep de clases |
| Volumes de estado `M01_State_*` | §L4.1 | **No existen** | — |
| Luces de estado de puerta de R4 y `DoorPresentation` | 04 §P1, §P2 | **No existe** la clase en `Assets/`. Los colores de puerta son solo documento | grep de clases |
| Relleno de esclusas (`C01–C17`) | §L3.20 | **Aplicado** en escenas de sesión (`FillColor`, `LightingAuthoring.cs:37-46`). Sin JSON propio | `LightingAuthoring.cs:37-46` |
| Validador §L8.3 | ≤ 8 luces, ≤ 2 sombras, luces por objeto, color reservado a 6 m | **Implementado en parte.** Reglas en `ValidateRoom` (`LightingAuthoring.cs:367-445`) | informe F2 |
| `SectorLightingBlend` fuera de `Map01_Campaign` (D-L5) | Recomendado quitarlo | **Sigue presente** en `Map01_Campaign_S1.unity` y en `RequiemCampaign.unity` | grep de escenas |
| Fondo de niebla de esclusa (0,09; 0,095; 0,105) | «DECISIÓN en F2» | **No aplicada.** Escenas de sesión: (0,06; 0,065; 0,075) | `Map01_Campaign_S1.unity` |
| Grano 0,18 y viñeta neutra por zona (D-L4) | §L2.2 | **No aplicado.** `Campaign_PostProfile` mantiene grano 0,3 y viñeta morada | `Campaign_PostProfile.asset` |
| Verde de terminales en R07 (0,15; 1,0; 0,45) | §L2.2, zona Comunicaciones | **No está en el JSON de R07** (sí en R13). Discrepancia | `R07.json` vs `R13.json` |

**Discrepancia de recuento R07 y R18 (no documentada):** `LightingAuthoring.BuildRoom` crea una luz neutra por defecto (intensidad 12, alcance 12, color 0,85; 0,92; 1,0) para cada anclaje de Structure que no tiene entrada en el JSON (`LightingAuthoring.cs:174-183`). Afecta a R07 (`R07.A9`, `R07.A10`) y a R18 (`R18.A8`, `R18.A9`). Por eso la escena tiene 10 y 9 componentes `Light`, frente a 8 y 7 en los JSON. El informe F2 confirma 10 y 7 encendidas en R07, y 9 y 7 en R18. El documento 05 y el chat 11 lo señalan como exceso, pero no explican la causa.

**Otros hechos del código que contradicen el documento:**
- Tonemapping: el perfil usa ACES (`mode 2`), igual que el documento. Pero `Campaign_PostProfile` no tiene `ColorAdjustments` ni `WhiteBalance`. Por tanto **postExposure = 0 en Map01** (`Campaign_PostProfile.asset`, componentes: Bloom, Vignette, Tonemapping, FilmGrain, MotionBlur).
- Post de Map01: la escena `Map01_Campaign_S1.unity` tiene un Volume con `sharedProfile = Campaign_PostProfile` (1 referencia). Las escenas de cuarto no tienen Volume propio. **Sin comprobar** que ese Volume sea el único de la pila en runtime.
- Los apagones de §L4.5 (R07, R10, R16, R18) **no están implementados**.

---

## 1. Tabla por zona y cuarto (Map01)

### 1a. Ambiente, niebla y post

Ambiente en RGB 0–1 (sky / equator / ground). Intensidad 1,0 y `reflectionIntensity` 1,1 en todos los cuartos. Modo Trilight (1) en todos. Niebla exp² (modo 3) en todos. Volume de zona: ninguno en Map01. Post aplicado: `Campaign_PostProfile` (ACES; bloom umbral 0,9, intensidad 0,1, scatter 0,7, clamp 12; viñeta morada 0,084; grano Thin1 0,3). Esa pila se aplica a través de la sesión.

| Cuarto | Zona | Sky | Equator | Ground | Niebla color | Densidad | Probes (JSON; 0 = 1 caja por defecto) |
|---|---|---|---|---|---|---|---|
| R01 | Acceso | 0,34; 0,28; 0,22 | 0,22; 0,20; 0,19 | 0,16; 0,12; 0,09 | 0,20; 0,15; 0,10 | 0,012 | 0 (doc: 1) |
| R02 | Acceso | 0,34; 0,28; 0,22 | 0,22; 0,20; 0,19 | 0,16; 0,12; 0,09 | 0,20; 0,15; 0,10 | 0,014 | 0 |
| R03 | Habitáculo | 0,28; 0,32; 0,38 | 0,20; 0,22; 0,25 | 0,14; 0,13; 0,12 | 0,12; 0,14; 0,17 | 0,010 | 0 |
| R04 | Habitáculo | 0,28; 0,32; 0,38 | 0,20; 0,22; 0,25 | 0,14; 0,13; 0,12 | 0,12; 0,14; 0,17 | 0,010 | 0 |
| R05 | Habitáculo | 0,28; 0,32; 0,38 | 0,20; 0,22; 0,25 | 0,14; 0,13; 0,12 | 0,12; 0,14; 0,17 | 0,010 | 0 |
| R06 | Armería | 0,30; 0,24; 0,18 | 0,20; 0,17; 0,15 | 0,14; 0,10; 0,07 | 0,18; 0,13; 0,08 | 0,011 | 0 |
| R07 | Comunicaciones | 0,22; 0,28; 0,30 | 0,16; 0,20; 0,20 | 0,10; 0,12; 0,11 | 0,09; 0,14; 0,13 | 0,010 | 0 |
| R08 | Almacenes | 0,24; 0,20; 0,16 | 0,17; 0,15; 0,13 | 0,11; 0,09; 0,07 | 0,15; 0,12; 0,09 | 0,013 | 0 |
| R09 | Túneles | 0,22; 0,17; 0,17 | 0,17; 0,14; 0,14 | 0,12; 0,09; 0,09 | 0,16; 0,08; 0,07 | 0,012 | 0 |
| R10 | Túneles | 0,22; 0,17; 0,17 | 0,17; 0,14; 0,14 | 0,12; 0,09; 0,09 | 0,16; 0,08; 0,07 | 0,014 | **2** |
| R11 | Agua | 0,20; 0,30; 0,30 | 0,14; 0,21; 0,21 | 0,09; 0,13; 0,12 | 0,08; 0,17; 0,16 | 0,014 | 0 |
| R12 | Agua | 0,20; 0,30; 0,30 | 0,14; 0,21; 0,21 | 0,09; 0,13; 0,12 | 0,08; 0,17; 0,16 | 0,014 | **2** |
| R13 | Agua | 0,20; 0,30; 0,30 | 0,14; 0,21; 0,21 | 0,09; 0,13; 0,12 | 0,08; 0,17; 0,16 | 0,014 | 0 |
| R14 | Fundición | 0,32; 0,22; 0,16 | 0,21; 0,17; 0,14 | 0,20; 0,10; 0,05 | 0,20; 0,11; 0,06 | 0,012 | 0 |
| R15 | Ensamblaje | 0,30; 0,33; 0,37 | 0,22; 0,24; 0,27 | 0,13; 0,13; 0,13 | 0,13; 0,15; 0,17 | 0,009 | 0 |
| R16 | Reactor | 0,18; 0,28; 0,34 | 0,13; 0,19; 0,23 | 0,12; 0,10; 0,08 | 0,07; 0,15; 0,19 | 0,013 | 0 |
| R17 | Mando | 0,15; 0,20; 0,24 | 0,11; 0,14; 0,17 | 0,08; 0,09; 0,10 | 0,06; 0,10; 0,12 | 0,010 | 0 |
| R18 | Bóveda | 0,26; 0,28; 0,32 | 0,18; 0,19; 0,22 | 0,11; 0,10; 0,10 | 0,12; 0,13; 0,16 | 0,007 | **2** |
| S00 y C01–C17 (sesión) | Esclusas | 0,27; 0,28; 0,31 | 0,19; 0,20; 0,22 | 0,19; 0,20; 0,22 | 0,06; 0,065; 0,075 | 0,016 | 0 |

Notas:
- Lo que aplica el código coincide con el documento 05 §L2.2 en ambiente y niebla. Por ejemplo, R01 y R03 coinciden exactamente. La diferencia es el color de niebla de las esclusas, que sigue en la versión anterior a la decisión F2.
- `CampaignFlow` mezcla ambiente y niebla en 1,6 s (`environmentBlendSeconds`, `CampaignFlow.cs:105-110, 197-220`). Lo mezcla a través del neutro de la sesión.
- Las probes: si el JSON no lista cajas, `BuildProbes` crea una por cuarto sobre el AABB (`LightingAuthoring.cs:269-298`). Dos cajas en R10, R12 y R18. El total de probes horneadas (21 en el documento) **no comprobado**.

### 1b. Luces, sombras y medidas

«Comp.» son componentes `Light` en la escena. «Enc.» son los que tienen intensidad > 0 en reposo (criterio del validador, `LightingAuthoring.cs:392-394`). Intensidad y alcance son valores del JSON, sin contar las luces por defecto. Las medidas del chat 11 son mediana del fondo y mediana del enemigo en escala 0–255 (`VISION_V01.md` §1.2). En R05, R09, R13 y R17 no hubo medida.

| Cuarto | Comp. / Enc. | I (mín–máx) | Alcance (m) | Sombras | Colores de luz (RGB 0–1) | Fondo / enemigo |
|---|---|---|---|---|---|---|
| R01 | 6 / 6 | 14–18 | 8–14 | 0 | (1,00; 0,62; 0,24) ámbar sodio; (1,00; 0,55; 0,10) baliza; (1,00; 0,92; 0,80) salida | 8,8 / 3,5 |
| R02 | 7 / 7 | 10–55 | 8–20 | 0 (doc: 0–1) | (1,00; 0,62; 0,24); (1,00; 0,80; 0,50); (1,00; 0,92; 0,80); (1,00; 0,95; 0,85) | 7,0 / 8,8 |
| R03 | 8 / 8 | 11–18 | 8–13 | 0 | (0,78; 0,88; 1,00) fría; (1,00; 0,10; 0,06) mural rojo; (1,00; 0,92; 0,80) salida | 11,9 / 52,3 (HoverBot) |
| R04 | 7 / 7 | 14–24 | 8–14 | 0 | (0,78; 0,90; 1,00); (1,00; 0,78; 0,50) cocina; (1,00; 0,90; 0,75); (1,00; 0,92; 0,80) | 12,9 / 8,0 |
| R05 | 6 / 6 | 12–40 | 8–14 | 0 | (0,92; 1,00; 0,96) clínica; (1,00; 0,92; 0,80); (1,00; 1,00; 0,98) | sin medir |
| R06 | 8 / 8 | 10–36 | 8–20 | 0 (doc: 0–1) | (1,00; 0,08; 0,04) rotativas rojas; (1,00; 0,58; 0,20) sodio; (1,00; 0,92; 0,80); (1,00; 0,95; 0,85) | 8,8 / 11,0 |
| R07 | **10** / 7 (8 JSON + 2 defecto) | 0–18 | 8–22 | 0 | (0,78; 0,90; 1,00) techo; (1,00; 0,08; 0,04) alarma (I = 0 en reposo); (1,00; 0,92; 0,80) salida; defecto (0,85; 0,92; 1,00) I 12 | 8,9 / 9,7 |
| R08 | 8 / 7 (1 reserva apagada) | 0–45 | 8–24 | **1** | (1,00; 0,66; 0,28); (1,00; 0,78; 0,45); (1,00; 0,92; 0,80); (1,00; 0,95; 0,85) | 10,9 / 8,2 |
| R09 | 5 / 5 | 10–18 | 8–12 | 0 | (0,78; 0,90; 1,00); (1,00; 0,75; 0,45) oficina; (1,00; 0,92; 0,80) | sin medir |
| R10 | 8 / 8 | 16–30 | 8–14 | 0 | (0,78; 0,90; 1,00) bahía; (1,00; 0,10; 0,05) grietas; (1,00; 0,60; 0,15) emboscadas; (1,00; 0,92; 0,80) | 11,8 / 7,1 |
| R11 | 8 / 8 | 12–30 | 8–18 | 0 | (0,20; 0,90; 0,70); (0,40; 0,95; 0,80); (0,80; 1,00; 0,95); (1,00; 0,92; 0,80) | 10,5 / 8,4 |
| R12 | 8 / 8 | 14–30 | 8–18 | 0 | (0,60; 0,90; 0,90); (1,00; 0,55; 0,15) foso; (1,00; 0,92; 0,80); (1,00; 0,95; 0,85) | 7,6 / 2,9 |
| R13 | 7 / 7 | 4–18 | 4–14 | 0 | (0,15; 1,00; 0,45) **verde** bombas; (0,75; 1,00; 0,95); (1,00; 0,92; 0,80) | sin medir |
| R14 | 8 / 8 | 18–40 | 8–20 | **1** | (1,00; 0,40; 0,10) lava; (1,00; 0,62; 0,25) horno; (1,00; 0,80; 0,55); (1,00; 0,92; 0,80) | 8,1 / 7,6 |
| R15 | 8 / 8 | 18–32 | 8–20 | 0 | (0,30; 0,85; 1,00) línea; (0,70; 0,85; 1,00); (0,92; 0,96; 1,00) soldadura; (1,00; 0,92; 0,80) | 9,5 / 6,1 |
| R16 | 8 / 8 | 18–60 | 8–26 | 0 (doc: 0–1) | (0,20; 0,80; 1,00) núcleo; (0,70; 0,90; 1,00); (1,00; 0,60; 0,20) torres; (1,00; 0,92; 0,80) | 6,5 / 13,4 |
| R17 | 8 / 8 | 8–30 | 8–14 | 0 | (0,20; 0,90; 0,70); (0,25; 0,75; 0,85); (0,60; 0,85; 0,95); (1,00; 0,92; 0,80) | sin medir |
| R18 | **9** / 7 (7 JSON + 2 defecto) | 0–95 | 18–36 | **2** | (0,75; 0,82; 1,00); (0,75; 0,85; 1,00); (1,00; 0,08; 0,04) fases rojo | 2,8 / 7,5 |

Cuartos sin dato de sombra en JSON pero con luces «de sombra» en el documento: R02, R06 y R16 (opcionales en el documento, todos en 0 en el JSON).

**Límite por cuarto (`MaxLit` = 8, `MaxShadow` = 2):**
- Al límite en reposo (8 encendidas): R03, R06, R10, R11, R12, R14, R15, R16, R17.
- Por debajo: R01 (6), R02 (7), R04 (7), R05 (6), R08 (7), R09 (5), R13 (7), R07 (7, con defecto), R18 (7, con defecto).
- Validador F2 (`Tools/IronRequiem/Reports/Map01/F2/lighting_validate.txt`): **10 errores** de «más de 4 luces efectivas por objeto» en R01, R02, R05, R06, R08, R11, R14, R15, R16 y R18. Con 11 objetos en R14 y 14 en R18. Es el límite que más pesa, no el de 8 luces.
- Reflection probes: 21 previstas en el documento. En el JSON hay 2 en R10, R12 y R18. El resto usa 1. **Total no comprobado.**

**Luminaria de sesión (S00 y C01–C17):** las escenas `Map01_Campaign_S1..S4` tienen 19, 21, 17 y 13 componentes `Light` (70 en total). El documento cuenta 2 + 17 = 19. La diferencia **no está explicada** (pueden ser luces de puerta de R4 u otras). Sin comprobar.

**Cuartos sin fuentes de luz de estado:** R07 alarma, R10 persecución, R16 y R18 fases: sin implementar. Sus luces rojas y pulsos no existen en código, solo en JSON con intensidad 0.

---

## 2. Objetivos numéricos y estado frente a ellos

Fuente de los objetivos: `05_ILUMINACION.md` §L1.1 y §L1.3 (escala sRGB 0–255 de una tarjeta gris del 18 % tras ACES, misma escala que la medida del chat 11).

| Objetivo | Valor en el documento | Origen |
|---|---|---|
| Nivel 1, salida | 150–190 | §L1.1 |
| Nivel 2, landmark | 120–170 | §L1.1 |
| Nivel 3, ruta crítica y carriles | 70–110 | §L1.1 |
| Nivel 4, ambiente y rincones | ≥ 40 % de la ruta (≈ 45–60); bajo hasta 28 en oscuridad intencional | §L1.1, §L1.4 |
| Carriles, spawns, recogida de armas, botiquines | **mín. 45**, máx. 130 | §L1.3 |
| Bajo una luminaria | máx. 170 | §L1.3 |
| Contraste charco / sombra | 2,5:1 a 4:1 (lineal) | §L1.3 |

**Medido (chat 11) frente a 45 en carriles y spawns:**

| Cuarto | Fondo medido | Factor hasta 45 | Diferencia aprox. en EV lineal |
|---|---|---|---|
| R18 Bóveda | 2,8 | ×16 | +4,9 |
| Room01–04 (campaña antigua) | 2,2–6,0 | ×7,5–20 | +3,8 a +5,3 |
| Pursuit05 | 2,1 | ×21 | +5,4 |
| R16 Reactor | 6,5 | ×6,9 | +3,7 |
| R12 Colector | 7,6 | ×5,9 | +3,5 |
| R14 Fundición | 8,1 | ×5,6 | +3,4 |
| R02 Pozo | 7,0 | ×6,4 | +3,6 |
| R01 Control | 8,8 | ×5,1 | +3,3 |
| R06 Armería | 8,8 | ×5,1 | +3,3 |
| R07 Comunicaciones | 8,9 | ×5,1 | +3,3 |
| R15 Ensamblaje | 9,5 | ×4,7 | +3,2 |
| R11 Decantadores | 10,5 | ×4,3 | +3,0 |
| R08 Almacenes | 10,9 | ×4,1 | +3,0 |
| R10 Túnel | 11,8 | ×3,8 | +2,9 |
| R03 Dormitorios | 11,9 | ×3,8 | +2,8 |
| R04 Comedor | 12,9 | ×3,5 | +2,7 |
| Room00b Pasillo de alarma (campaña) | 13,0 | ×3,5 | +2,7 |

**Cálculo:** la diferencia en EV se estima decodificando sRGB a lineal (sRGB 13 → 0,0040; sRGB 45 → 0,0262; sRGB 2,8 → 0,00085). Es **aproximado**, porque el tonemapping ACES no es lineal en sombras. Tómalo como orden de magnitud, no como dato de diseño.

**Lectura:** en Map01 todos los cuartos medidos están entre 2,8 y 13 frente a un mínimo de 45. La brecha es de ×3,5 a ×16. Con una subida de EV al estilo de la plantilla se pasa de tonos oscuros a tonos muy quemados en emisivos, así que hay que repartir la subida entre ambiente, luces y exposición (ver §4).

---

## 3. Métodos de medición y restricciones ya fijadas

- **Chat 11** (`VISION_V01.md` §1.1): `VisionF0Probe.cs` renderiza al enemigo desde 5, 15 y 25 m con `Campaign_PostProfile`. Valida que la cámara del jugador y la de la herramienta dan 10,54 y 10,61 en la misma pose.
- **Restricción explícita en el checklist:** «Map01 oscuro de forma general (fondo 2–13 frente a ≥ 45): chat 9 y F3, **sin subir el ambiente**» (`CHECKLIST_TRABAJO.md:1541`). El chat 11 la repite (`VISION_V01.md` §1.2): «La regla prohíbe arreglarlo subiendo el ambiente». **Conflicto directo** con el objetivo de este pase. Requiere decisión del usuario.
- **Restricción de la tarea de visibilidad** (`CHECKLIST_TRABAJO.md:312`): «sin luces nuevas ni cambio de iluminación global». Aplica a esa tarea, pero el pase visual actual la está revisando.
- **Presupuesto de CPU:** `VISUAL_AUDIT_F0.md` mide CPU (50 ms en el pico) con GPU < 2 ms. El cuello de botella es la CPU. Las luces cuestan en la CPU por objeto, por lo que subir la luminosidad con más luces penaliza más que subirla con ambiente o exposición.

---

## 4. Palancas para subir la luminosidad

| # | Palanca | Dónde vive | Efecto esperado | Riesgo | ¿Reaplicable por datos? |
|---|---|---|---|---|---|
| 1 | **Ambiente Trilight** (sky / equator / ground) por cuarto | `Data/Lighting/R*.json` → `environment`; aplicado en `LightingAuthoring.cs:257-265` | Sube el fondo en todas las superficies sin tocar luces ni coste de CPU por objeto | Aplana el cuarto y baja el contraste (objetivo de 2,5:1 a 4:1). Si el ambiente supera los valores de referencia, la luz de los emisivos gana protagonismo | **Sí**: `Lighting/Build greybox all` reescribe `[Lighting]` y guarda escena (con respaldo previo) |
| 2 | **`ambientIntensity`** (1,0 en todos) | `RenderSettings.ambientIntensity` (mismo archivo, línea 262) | Según la documentación de Unity, el multiplicador de ambiente solo actúa con la fuente Skybox. **Con Trilight probablemente no tiene efecto. Sin comprobar** | Si no actúa, subirlo no sirve | Sí, por datos, pero comprobar primero |
| 3 | **Intensidad y alcance de luces existentes** | `lights[].intensity` y `range` en JSON; recorte por `maxRange` (`LightingAuthoring.cs:219-222`) | Sube charcos y acentos en lugar de ambiente. Cuartos con margen: R01, R02, R04, R05, R08, R09, R13 | Pop al cruzar el límite de 4 luces por objeto (ya hay 10 errores). Más bloom en emisivos (umbral 0,9 y clamp 12) | Sí |
| 4 | **Luces de relleno nuevas** | JSON + anclajes de `Data/Structure` (`lightAnchors`) | Mucha subida con poco ambiente. Ver margen por cuarto en tabla 1b | Límite de 8 encendidas y 4 por objeto. Cuartos ya al límite: R03, R06, R10, R11, R12, R14, R15, R16, R17. CPU | Sí, si hay anclaje libre. Sin comprobar cuántos anclajes libres hay |
| 5 | **`postExposure`** (EV) del perfil | `Campaign_PostProfile.asset`: **no hay override de `ColorAdjustments`** (es 0) | Sube todo en la misma proporción: 1 EV = ×2 en lineal. El rango de subida que sugiere §2 es de +2,7 a +5,4 EV | Quema emisivos HDR (≤ 4 según §L1.3), sube bloom y ruido. **Afecta a toda la campaña**: también a RequiemCampaign, Room00b–04, Pursuit05 y a la pila de Room00 (`render-perf.md` §2, prioridad 0). Puede cambiar la cinemática protegida | No por JSON: asset (vía Editor API) |
| 6 | **Volume de zona** (`Sector_M01_*`) | No creado. Valores previstos en `05` §L2.2 (postExposure +0,05 a +0,16; contraste; WB) | Subida por zona sin afectar a las demás. Mantiene la identidad | Requiere código nuevo en `LightingAuthoring` y crear assets. Riesgo de mezcla con `VolumeFadeIn` y `SceneFade` (`ZoneEnvironment.cs:53-102`) | Sí, si se añade a `LightingAuthoring` |
| 7 | **Color y densidad de niebla** | JSON `environment.fogColor` y `fogDensity`; `ApplyEnvironment` (`LightingAuthoring.cs:264`) | Sube el fondo lejano, el «telón» de la silueta y reduce el aspecto oscuro del espacio | Baja el contraste de los enemigos lejanos: el documento pide fondo ≥ 1,5× el enemigo (§L1.2). Con densidad mayor a 0,016 se ensucian las torretas a 43,75 m | Sí (JSON) |
| 8 | **Albedo de materiales** | Shader `IronRequiem/HardSurface`, `VO_Env_RustMetal.mat`, `Bunker_RustMetal_Albedo`, `VO_Noise` | Sube el rebote de la luz en superficies sin tocar la iluminación | Son **materiales compartidos** con la zona protegida (lista roja de `MAP_FX_AUDIT_20261117_r2.md` §1). Hay que copiarlos. No se ha medido el coste ni el efecto | No |
| 9 | **Tonemapping** | `Campaign_PostProfile` → Tonemapping `mode 2` (ACES) | Cambiar a Neutral da más brillo en sombras y menos contraste | El enum no se interpola (`render-perf.md` §2). Cambia la identidad de toda la campaña | No |
| 10 | **Viñeta morada** (0,084; color 0,18; 0,047; 0,32) | `Campaign_PostProfile.asset` | Quitar o neutralizar la viñeta aclara los bordes de pantalla | Mínimo. Es un tinte morado que el documento quiere cambiar | No |
| 11 | **Grano** (0,3 hoy; 0,18 propuesto, D-L4) | `Campaign_PostProfile.asset` → FilmGrain | Sin efecto directo en brillo; reduce el ruido sobre las sombras | Relacionado con IR-03: la nitidez y el ruido sobre los tonos oscuros | No |
| 12 | **Sombras** | `shadow` en JSON (R08, R14, R18 ×2) | **Oscurecen.** No son palanca de brillo | — | — |
| 13 | **Reflection probes** (`reflectionIntensity` 1,1) | `LightingAuthoring.cs:294` y `262` | Solo afecta a reflejos, no al difuso | Coste de horneado | Parcial |
| 14 | **Rim de enemigos** (`VisionIntensity` 0–1,5) | `RequiemSettings` (ya implementado) | Hace legibles a los enemigos sin subir el fondo | Ya hecho en chat 11 | Ya implementado |

**Combinación que el documento plantea implícitamente:** ambiente y niebla para el «fondo» (palancas 1, 7), luces de relleno solo donde falte (palanca 3 y 4) y exposición moderada si todavía no basta (palanca 5, con cuidado de HDR). **Sin comprobar** cuánto de la brecha de +3 EV cubre el ambiente por sí solo.

---

## 5. Colores reservados y restricciones

### 5a. Puertas (reservados: rojo, ámbar y verde)

Fuente: `04_PUERTAS.md` §P2.1 (tabla de estados, líneas 377–386), §P1.0 (presets, línea 93), `Concepts/Doors/Redesign_v01/DOORS_CONCEPTS.md` §2 (HDR), §P2.1 (línea 395) y `05_ILUMINACION.md` §L1.5, §L5.3, §L8.3.

| Estado de puerta | Color HDR | Patrón de baliza y tiras |
|---|---|---|
| BLOQUEADA | Rojo (1,00; 0,03; 0,02) ×9, fijo | Tiras continuas y quietas, balizas paradas |
| ALARMA (combate en el cuarto) | Rojo y blanco alternos a ≤ 2,5 Hz durante 3 s, después rojo latente a 0,5 Hz | Segmentos alternos, balizas giratorias 3 s, luego latido lento |
| PREPARANDO (pre-roll) | Ámbar fijo | Balizas giratorias, tiras avanzan hacia la hoja |
| ABRIÉNDOSE | Ámbar (1,00; 0,36; 0,02) ×9 | Balizas 1 vuelta cada 0,6 s |
| ABIERTA / PASA | Verde (0,10; 1,00; 0,22) ×7 | Chevrones fijos, al terminar `IR_Airlock_Done` |
| CERRÁNDOSE / CICLO | Ámbar parpadeando 2,2 Hz con 55 % encendido | Balizas de cámara giratorias |
| ABORT | Ámbar → verde | Chevrones apuntan atrás |
| SELLADA | Rojo tenue, sin `Light` ni pulso | «X» fija |

- Presets de emisivos (`04` §P1.0, línea 93): Apagado, Rojo, RojoLatente, Ámbar, ÁmbarParpadeo, Verde, Blanco.
- Regla de `05`: ninguna luz roja, ámbar o verde saturada a menos de 6 m de un marco (§L1.5, §L5.3, §L8.3). En código está como **aviso** en `LightingAuthoring.cs` (línea ~421: «rojo/ámbar/verde saturado a < 6 m de la puerta»). El informe F2 **no contiene ningún aviso de este tipo**, pero no sé si el chequeo recibió las posiciones de puerta reales. **Sin comprobar.**
- Las puertas no tienen código: `DoorPresentation` no existe en `Assets/`. Los colores de puerta son solo de documento.
- **Ritmo de destellos:** ≤ 3 Hz (`04` §P2.1, línea 43).

### 5b. Colores de luz por zona que tocan a los reservados

| Zona | Colores de luz de JSON | Conflicto previsible |
|---|---|---|
| Acceso (R01, R02) | Ámbar sodio (1; 0,62; 0,24); baliza (1; 0,55; 0,10) | Ámbar de ABRIÉNDOSE y CICLO. Solo es problema a < 6 m de un marco |
| Armería (R06) | Rotativas **rojas** (1; 0,08; 0,04); sodio (1; 0,58; 0,20) | Rojo de BLOQUEADA y de ALARMA |
| Túneles (R10) | Rojo de grietas (1; 0,10; 0,05); ámbar emboscada (1; 0,60; 0,15) | Rojo de BLOQUEADA |
| Fundición (R14) | Lava (1; 0,40; 0,10); horno (1; 0,62; 0,25) | Ámbar de ABRIÉNDOSE |
| Agua (R13) | **Verde** bombas (0,15; 1,00; 0,45) | Verde de ABIERTA (0,10; 1,00; 0,22). Son casi iguales |
| Comunicaciones (R07) | Alarma **roja** (1; 0,08; 0,04); documento prevé verde de terminales que **no está en el JSON** | Rojo de alarma y de BLOQUEADA |
| Reactor (R16) | Torres ámbar (1; 0,60; 0,20) | Ámbar de cuenta atrás |
| Bóveda (R18) | Fases **rojas** (1; 0,08; 0,04) | Rojo de BLOQUEADA y de ALARMA |
| Esclusas (C01–C17) | Relleno cromático; lámparas de ciclo ámbar/verde de R4 | Ámbar y verde |

### 5c. Colores de estado de enemigos hoy

Fuente: `Assets/IronRequiem/Scripts/VisualOverhaul/EnemyStateLights.cs:20-21, 58-59`.

| Estado | HDR (R; G; B) | Nota |
|---|---|---|
| Patrulla | cian (0,1; 1,4; 1,2) | Coincide con el cian de zona de Agua, Reactor y Ensamblaje |
| Alerta | ámbar (2,2; 1,1; 0,08) | |
| Ataque | rojo (2,8; 0,12; 0,05) | Mismo tono que BLOQUEADA |
| Aviso | blanco caliente (5; 3,6; 3), parpadea contra Ataque a **14 Hz** (`blinkHz = 14`) | 14 Hz supera el límite de 3 Hz que el documento de puertas aplica. Solo señalo el dato |
| Muerte | negro | |

En `CorridorAlarm.cs:21`, la alarma de R07 usa `headOn` (2,2; 0,15; 0,08) y `headOff` (0,12; 0,01; 0,01). Es un rojo distinto del rojo de Ataque, pero muy parecido.

### 5d. Conflictos previsibles si los ojos de los robots pasan a ser rojos

1. **Ataque ya es rojo** (2,8; 0,12; 0,05). Si el ojo es rojo siempre, el estado Patrulla / Alerta / Ataque pierde su lectura de color y solo queda la intensidad o el pulso.
2. **Rojo de BLOQUEADA** (1; 0,03; 0,02). Un robot con ojo rojo cerca de una puerta cerrada se confunde con ella.
3. **Rojo de alarma** en R07, R10 y R18. Un ojo rojo se pierde en ese ambiente.
4. **Ya hay rojo en robots:** `CHECKLIST_TRABAJO.md:640-645` documenta que T07 y Saw ya tienen esfera roja y CoreIndicator rojo. El cambio sería extender lo que ya existe, no empezar de cero.
5. **Ojo y halo por emisivo:** el ojo rojo con bloom puede crear un halo que supere el «≤ 4 HDR» de `05` §L1.3. Con Bloom umbral 0,9 y clamp 12 (`Campaign_PostProfile`), el halo podría tapar al robot.
6. **Sin luz dinámica por robot:** el prompt 12 pide no usar `Light` por robot en hordas. Consistente con el presupuesto de 4 luces por objeto.

---

## 6. Linterna: decisión del chat 11, solape con el prompt 12

**Qué decidió el chat 11** (`VISION_V01.md` §2, §3, §6):
- Evaluó A (linterna de hombro, spot de 55°, alcance 30 m, intensidad 35), B (pulso de escáner de 3 s con recarga de 8 s) y C (lectura pasiva).
- **Eligió C.** Medido en 18 puntos: A → 8 legibles, B → 17, C → 17. C es la opción sin entrada nueva y sin `Light`.
- **Implementado y verificado** (§6, §8): `EnemyDarkReadability.cs` (`Assets/IronRequiem/Scripts/Player/Vision/`), `VisionMaterialRules.cs` (tinte (0,72; 0,86; 1,0), fresnel 4,5, rim 0 con luz a 1,6 a oscuras), `VisionMaterialMap.cs`, cuatro variantes `*_Vision.mat`, ajustes `VisionIntensity` 0–1,5 y `VisionOutlinesAlways` en `RequiemSettings`. Tests: EditMode 3/3, PlayMode 2/2. **0 luces añadidas.**
- Medido tras la implementación (`VISION_V01.md` §7): de 0 a 16 de 18 legibles. Coste en SetPass: +18 a +25 % por romper el SRP Batcher. No medido en build.

**Por qué descartó la linterna con `Light`** (`VISION_V01.md` §1.3):
- Pasa de 8 a 9 componentes en nueve cuartos (los que ya están al límite).
- Con el límite de 4 luces por objeto, algunas superficies perderían una luz de la sala al entrar en el cono.
- **Light Layers están desactivadas** (`Default_PipelineAsset.asset:76`, `m_SupportsLightLayers: 0`). La luz de la linterna iluminaría también el viewmodel y el arma. Activarlas es un cambio de render pipeline, que `AGENTS.md` no permite sin autorización.
- El chat 11 hace un matiz que el prompt 12 no recoge: **A y C se anulan en parte**. La linterna sube el fondo local y reduce el contraste de C (A + C: 3,28 / 3,64 / 3,62 frente a C: 4,77 / 4,03 / 4,05).

**Solape con el prompt 12** (`PROMPTS_CHATS.md:566-569`, `§12` F1a y F2):
- El prompt 12 dice «sustituye al prompt 11 en lo de la linterna» y pide elegir entre «linterna de hombro, pulso o lectura pasiva».
- **La lectura pasiva ya está hecha** (chat 11). El prompt 12 la trata como una opción pendiente. Hay que decidir si cuenta como elegida.
- La tabla de conflictos (`PROMPTS_CHATS.md:670+`) dice «12 y 11: no correr los dos». Si el usuario elige A o B, el chat 11 queda como base y la linterna se añade encima.
- El prompt 12 también pide que la linterna se integre con la reasignación del chat 10 (`ControlsRebinder` ya existe). En la tabla de acciones no hay tecla T asignada (`InputSystem_Actions.inputactions`), así que T sigue libre. **Ningún botón de mando libre** (todos están asignados).

**Qué quedaría por hacer si el usuario quiere linterna además de más luz:**
1. Elegir A o B sobre la base de C (decisión del usuario).
2. Si es A, hay que liberar una luz de la sala o atenuar un anclaje mientras la linterna está encendida (VISION §1.3).
3. Decidir el viewmodel: activar Light Layers (cambio de pipeline, pide permiso) o mover la luz.
4. Asignar tecla (T recomendada) y mando (no hay botón libre).
5. Medir la carga en Release. No está medida.
6. **Alternativa sin `Light`:** una proyección tipo cono (Decal Projector o emisivo). No está probada y su coste es **sin comprobar**.

---

## 7. Campaña antigua (Room00b, Room01–04, Pursuit05)

Fuentes: escenas `Scenes/Campaign/*.unity` (YAML), `Art/VisualPass/Sector_*.asset`, `render-perf.md` §2 y §4, `CHECKLIST_TRABAJO.md:653` y `:628`.

| Escena | Ambiente | Niebla (color; densidad) | Volume de sector | Luces |
|---|---|---|---|---|
| Room00b_AlarmCorridor | **Flat** (modo 3). Sky (0,16; 0,17; 0,20); equator (0,114; 0,125; 0,133); ground (0,047; 0,043; 0,035) | Exponencial (modo 2). (0,06; 0,07; 0,09); 0,030 | **Ninguno** (solo `Campaign_PostProfile`) | 17 (9 spot + 8 point). AlarmRed ×8. Probes: sin comprobar |
| Room01_Foundry | Trilight. Sky (0,405; 0,324; 0,243); equator (0,27; 0,2025; 0,1485) | exp². (0,17; 0,11; 0,07); 0,016 | `VisualPass_SectorGrade` prioridad 10: exp +0,10, contraste 12, sat −4, filtro (1; 0,95; 0,88), WB +12 | 4 CeilingLight, I 6–6,5 |
| Room02_Breach | Trilight. Sky (0,2835; 0,351; 0,4455); equator (0,189; 0,2295; 0,2835) | exp². (0,08; 0,10; 0,13); 0,018 | exp +0,10, contraste 14, sat −12, filtro (0,94; 0,98; 1), WB −10 | 4 CeilingLight |
| Room03_Assembly | Trilight. Sky (0,378; 0,351; 0,297); equator (0,243; 0,2295; 0,189) | exp². (0,12; 0,11; 0,08); 0,015 | exp +0,10, contraste 12, sat −6, filtro (1; 0,97; 0,92), WB +5 | 4 CeilingLight |
| Room04_Reactor | Trilight. Sky (0,2025; 0,3645; 0,3645); equator (0,135; 0,2295; 0,2295) | exp². (0,05; 0,13; 0,13); 0,020 | exp +0,10, contraste 15, sat −2, filtro (0,92; 1; 0,98), WB −8, tinte −4 | 4 CeilingLight |
| Pursuit05 | Trilight. Sky (0,3645; 0,2025; 0,1755); equator/ground (0,2295; 0,135; 0,108) | exp². (0,15; 0,05; 0,04); 0,022 | exp +0,05, contraste 16, sat 0, filtro (1; 0,9; 0,86), WB +8 | 6 point |

- Todas usan `Campaign_PostProfile` (prioridad 0): ACES, bloom 0,1, grano 0,3, viñeta 0,084 morada.
- En Room01–04 la campaña ya pasó de Flat 0,55 a Trilight (checklist:653, 08/11). Las luces pasaron a I 6–6,5 solo en color e intensidad.
- Ambos valores de sky del Trilight de las escenas antiguas traen un campo alfa de 1,35 en el YAML. No sé si ese campo cambia el brillo. **Sin comprobar.**
- **Room00 (Hangar) y su pila de post** (`HG_PostVolume`, prioridad 20) están en zona protegida. No se propone tocarlos.

---

## 8. Sin comprobar (no he podido verificarlo leyendo)

1. **Si `ambientIntensity` tiene efecto en Trilight.** Creo que no, pero falta comprobarlo en el Editor.
2. **El valor de `m_AdditionalLightsRenderingMode`** (`Default_PipelineAsset.asset:47`, valor 1). Según mi conocimiento del enum de URP 17, 1 podría ser *PerVertex* y no *PerPixel*, como dice el documento (`VISION_V01.md` §1.3). Verificar en el Inspector.
3. **Qué Volume aplica en runtime** en los cuartos de Map01 (solo se ha visto la referencia en `Map01_Campaign_S1.unity`).
4. **Si `Campaign_PostProfile` altera la pila del Hangar protegido** (`render-perf.md` §2 lo indica: Campaign prioridad 0 más HG prioridad 20). Un cambio en el perfil podría alterar la cinemática.
5. **Distancia de cada luz roja, ámbar o verde a los marcos de puerta.** Requiere cruzar `Data/Doors/*.json` con `Data/Lighting/*.json`. El informe F2 no marca avisos.
6. **Número de anclajes libres** en Structure para luces de relleno (margen real por cuarto).
7. **Coste en CPU y GPU** de cada palanca. Nada está medido en Release (`05` §L6.2, `VISUAL_AUDIT_F0.md`).
8. **Reflection probes horneadas**: cuántas existen en disco y si están al día. El documento habla de 21.
9. **Luces de sesión S00 y C01–C17** (70 componentes en S1–S4 frente a 19 en el documento).
10. **Verde de terminales de R07** (documento) frente a su ausencia en JSON.
11. **Efecto de `SectorLightingBlend`** en la transición si se quita (D-L5).

---

## 9. Resumen (12 a 18 líneas)

1. Los datos que aplica el juego están en `Assets/IronRequiem/Levels/Map01/Data/Lighting/R01–R18.json`. Los aplica `LightingAuthoring` a las escenas de cuarto y de sesión. Coinciden con el documento 05 en ambiente Trilight y niebla exp² (densidad 0,007–0,016).
2. Map01 medido: fondo 2,8 a 13 frente a un mínimo de 45. La brecha va de ×3,5 (R03, R04, Room00b) a ×16 (R18). En EV lineal: de +2,7 a +5,4 aprox.
3. Ambiente, niebla y luces ya son datos reaplicables con `Lighting/Build greybox all`. La exposición (`postExposure`) no tiene override en `Campaign_PostProfile` y afecta a toda la campaña, incluida la pila del Hangar protegido.
4. Límites de luces: 8 encendidas por cuarto y 4 por objeto. Al límite en reposo: R03, R06, R10, R11, R12, R14, R15, R16, R17. El validador F2 da 10 errores de «más de 4 luces efectivas por objeto».
5. R07 (10 componentes) y R18 (9) tienen dos luces neutras de defecto (I 12, alcance 12) que el código crea para anclajes sin entrada en el JSON. No están en el documento.
6. Lo que no existe: Volumes de zona `Sector_M01_*`, estados de combate/alarma/apagones (componentes L4), luces de puerta de R4 y `DoorPresentation`.
7. Restricción vigente: «sin subir el ambiente» (`CHECKLIST_TRABAJO.md:1541`). Choca con este pase. Requiere decisión del usuario.
8. Colores reservados: rojo BLOQUEADA (1; 0,03; 0,02), ámbar ABRIÉNDOSE (1; 0,36; 0,02), verde ABIERTA (0,10; 1; 0,22). Patrones ≤ 3 Hz en 04 §P2.1.
9. Conflictos ya presentes: el verde de R13 es casi el verde ABIERTA; el rojo de alarma (R07, R10, R18) y de Armería se parece al rojo BLOQUEADA; el Ataque ya es rojo.
10. Ojos rojos para todos los robots pierden el estado por color (Ataque y ojo serían el mismo tono). El aviso de enemigo parpadea a 14 Hz, por encima del límite de 3 Hz de puertas.
11. Linterna: el chat 11 eligió C (lectura pasiva, implementada, 16 de 18 legibles, 0 luces añadidas). A y B quedan como opción sobre C.
12. Una linterna con `Light` (opción A) necesita luz libre por cuarto, decidir el viewmodel (Light Layers desactivadas, cambio de pipeline) y reduce el contraste de C.
13. Campaña antigua: Room01–04 y Pursuit05 usan Trilight con `Campaign_PostProfile` y un Volume de sector (exp +0,05/+0,10). Room00b sigue en Flat (0,16; 0,17; 0,20) sin Volume.
14. Puntos sin comprobar que cambian la decisión: `ambientIntensity` en Trilight, el enum de luces adicionales, distancia de luces rojas a marcos, y el efecto de `Campaign_PostProfile` sobre el Hangar.
15. Ningún cambio en el proyecto. Solo este archivo.
