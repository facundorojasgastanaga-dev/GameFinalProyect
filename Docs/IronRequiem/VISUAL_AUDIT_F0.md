# IRON REQUIEM — Auditoría visual F0

Fecha: 08/11/2026. Fase 0 del plan visual (director de arte técnico). **Sin cambios en el proyecto.**

## Alcance y límites de esta auditoría

- Fuentes: AGENTS.md, CHECKLIST_TRABAJO.md, skills locales `.agents/skills/*`, escenas `RequiemCampaign.unity` y `Campaign/Room01_Foundry.unity` (lectura YAML) y capturas de campaña `Tools/IronRequiem/Reports/Campaign/room1-4.png` y `pursuit.png`.
- Play Mode: ejecutado con control del escritorio, solo Room01. El combate a través de capturas es demasiado lento para recorrer las doce rondas (C3). Room02-04 y Pursuit05 se auditan con las capturas del 08/11 y el YAML.
- Benchmark Player: **no ejecutado**. Hace falta un build y lanzar el .exe; está pendiente de tu OK porque el build tarda varios minutos.
- Las capturas son del 08/11, **anteriores** a la restauración de las armas VO; el arma en primera persona que aparece no es la vigente.
- Rendimiento: solo referencia histórica (≈12,61 ms de frame medio y 19,12 ms de p99, benchmark visual previo). **No es una base válida de la campaña actual.**

## Play Mode real (08/11, 21:10, control del escritorio)

Flujo IntroMenu → JUGAR → Room01_Foundry, Game view 1920×1080 con Stats. **Medidas del Editor, no del Player**: sirven para la tendencia, no certifican los FPS.

| Momento | FPS Editor | CPU (frame) | GPU | Triángulos | Draw calls |
|---|---|---|---|---|---|
| Menú | 102 | 9,8 ms | 0,5 ms | 2 k | 9 |
| Room01 antes de la ronda | 58-60 | 17 ms | 1,7 ms | 11,7 k | 342 |
| Ronda 1, aparición de enemigos | 44-45 | 22 ms | 1,7-1,9 ms | 96-164 k | 341-346 |
| Pico en combate | **19,9** | **50,3 ms** | 1,8 ms | 181 k | 300 |

Lectura: el juego está **muy limitado por CPU** (hilo principal entre 75 y 92 %). La GPU va sobrada (menos de 2 ms). Por eso las mejoras visuales del lado GPU (post, luz baked, materiales) son baratas; lo que hay que vigilar es la CPU: partículas, instanciación, IA y scripts. El pico de 50 ms coincide con la aparición de la ronda.

Capturas en `Tools/IronRequiem/Reports/VisualPass/F0/`.

### Hallazgos críticos nuevos (Play)

- **C1. Resplandor blanco gigante** en la aparición de enemigos (`f0-room1-glare-enemy-spawn.jpg`): una esfera blanca ocupa un tercio de la pantalla y tapa a los enemigos, más un segundo halo a la derecha. Se reprodujo en la campaña actual pese a la corrección T07 de hoy. Probable origen: VFX de spawn, flash o explosión con escala heredada, o bloom sobre un emisivo HDR. **Prioridad 1.**
- **C2. El overlay de daño tiñe toda la pantalla de rojo saturado** (`f0-room1-damage-overlay.jpg`) y ciega al jugador. Hay que reemplazarlo por viñeta direccional. **Prioridad 1.**
- **C3. Muerte en unos 3 s** tras empezar la ronda 1: la vida cae del 100 % al 0 % casi sin ver al enemigo. Es de balance o de daño de área, no visual, pero bloquea las pruebas visuales en combate. Queda registrado como pendiente; no lo toco sin tu OK.
- **C4. HUD sin terminar**: ranuras de arma como rectángulos blancos planos, barra de vida roja sin marco, la brújula se cruza con el texto del cartel de sector y el mensaje "Picked up weapon : Shotgun" sale en inglés con el estilo de la plantilla.
- **C5. El viewmodel VO** sale casi blanco y sobreexpuesto (exposición +0,30 EV y bloom) y la lectura del arma se pierde.

## Diagnóstico general

La campaña se lee hoy como un **graybox iluminado de forma plana**. Los sistemas (enemigos, VFX y destrucción) están más pulidos que el escenario que los contiene. La mayor ganancia visual por coste está en la luz, los materiales del entorno y el dressing, no en los enemigos.

Datos de escena (Room01 y RequiemCampaign):
- Ambiente en modo Flat (`m_AmbientMode: 3`), color gris azulado de 0,55/0,60/0,65 e intensidad 1: ilumina todo por igual y borra el volumen.
- Niebla desactivada.
- Cuatro luces puntuales `CeilingLight` de intensidad 4,4 por sala, sin sombras.
- Misma configuración en todas las salas: no hay identidad por sector.

## Defectos priorizados

| # | Defecto | Dónde | Impacto | Coste | Fase |
|---|---|---|---|---|---|
| 1 | Luz ambiental plana y alta: sin contraste ni zonas oscuras, enemigos sin silueta | Todas las salas | Alto | Bajo | F1 |
| 2 | Todas las superficies usan la misma textura de ruido "nublado" (suelo, paredes, techo): aspecto de prototipo y tiling muy visible | Todas | Alto | Medio | F2 |
| 3 | Sin identidad por sector: fundición, reactor y montaje se ven iguales | Room01-04 | Alto | Bajo-medio | F1/F2 |
| 4 | Salas vacías: cajas oxidadas y barriles como única cobertura, sin tuberías, maquinaria, cables ni señalética | Room01-04 | Alto | Medio-alto | F2 |
| 5 | Techo plano y oscuro que ocupa el 40 % del encuadre, sin vigas ni estructura | Todas | Medio | Medio | F2 |
| 6 | Puertas de esclusa como plano amarillo/negro sin marco ni luz de estado | Room02/03, pasillo | Medio | Bajo | F2/F6 |
| 7 | Enemigos diminutos y poco legibles a media distancia (solo barra roja visible) | Room01, Room04 | Alto | Bajo (luz/rim) | F1/F5 |
| 8 | Sin niebla ni profundidad atmosférica: el pasillo de persecución se lee como un túnel infinito sin ambiente | Pursuit05 | Medio | Bajo | F1 |
| 9 | Luminarias sin halo ni contribución visible; lámparas que no explican la luz del suelo | Todas | Medio | Bajo | F1 |
| 10 | Sin decals (óxido, aceite, quemaduras, marcas de impacto persistentes) | Todas | Medio | Bajo-medio | F2/F3 |
| 11 | Zonas de caída y grietas sin lectura de peligro desde lejos (por verificar en Room02) | Room02 | Medio | Bajo | F2 |
| 12 | El arma en primera persona de las capturas es negra y sin detalle; verificar el aspecto de las VO vigentes en una captura nueva | Viewmodel | Alto | — | F5 |
| 13 | Pendiente heredado: 4 contornos del brazo T-07 en LOD0 | T-07 | Bajo-medio | Medio | F5 |

## Recomendación de orden

1. **F1 primero**: es lo más barato y lo que más cambia. Ambiente de Flat a Gradient/Skybox con intensidad baja, luces clave cálidas y de relleno frías, sombras en 1-2 luces principales por sala, niebla lineal suave y grading por sector. Coste de GPU bajo si las sombras se limitan.
2. **F2** sustituye el ruido nublado por variantes HardSurface (concreto, chapa industrial, rejilla) con trim sheets, añade estructura de techo y dressing modular.
3. F3 a F6 sobre esa base.

## Siguiente paso (espera OK)

Propuesta para F1: primero corregir C1 (resplandor) y C2 (overlay rojo), porque invalidan cualquier evaluación visual en combate. Después, el pase de luz y postprocesado por sector. Método: control del escritorio sobre Unity y scripts de Editor en `Assets/IronRequiem/Editor/VisualPass/`, con respaldo previo en `Tools/IronRequiem/Backups/<fecha>-visualpass-f1`.

Decisiones abiertas:
- ¿Hago el build Player para el benchmark base antes de F1? Tarda varios minutos.
- ¿Ajusto C3 (muerte en 3 s), aunque sea gameplay, para poder auditar el combate?
