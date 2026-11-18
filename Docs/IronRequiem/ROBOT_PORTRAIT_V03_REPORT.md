# UNIDAD 01 — daño visual v03

16 de noviembre de 2026. Cambio limitado al retrato del HUD.

## Arte

23 PNG en `Concepts/Player/HUD_Reactions_v03/`, copiados byte por byte a `Assets/IronRequiem/Art/UI/RobotPortrait/v03/`. Se conservan las versiones anteriores.

- d0, salud >=75%: conserva las cinco poses originales.
- d1, salud >=50% y <75%: pierde una placa completa de la mejilla y una esquina de la frente; circuitos, cables y mecanismos expuestos.
- d2, salud >=20% y <50%: pierde ambas mejillas, la carcasa de la mandíbula y gran parte de la frente; quedan placas superiores parciales.
- d3, salud >0% y <20%: pierde toda la carcasa exterior; cráneo mecánico, cables, engranajes y visor expuestos.
- Muerte: el mismo endoesqueleto sin luces. No reaparece la armadura al morir.

Cada nivel dañado incluye frente, izquierda, derecha, impacto y carga. Sin reflejar imágenes. Ataque y OVERDRIVE siguen conservando el daño vigente. Curarse restaura el nivel que corresponde a la salud real. El límite cambia solo para el retrato; otros indicadores del HUD mantienen su configuración.

Se editaron 16 imágenes con la herramienta integrada `image_gen`: quince poses dañadas y apagado. Referencias, prompts y salidas originales quedan documentados en `Concepts/Player/HUD_Reactions_v03/generation-manifest.json`. No se generaron modelos ni se instalaron paquetes.

## Integración

`RobotPortraitGraphic` precarga el banco `IronRequiem/RobotPortraitV03`. Se reutilizan RetroStatusBar, selección de poses, eventos de salud/armas/puño, mirada y overlay existentes. El único cambio de lógica de presentación es el umbral de d3, ahora estrictamente inferior a 20%.

La herramienta de autoría importa Sprite Single, Full Rect, pivote central, transparencia, sin mipmaps, máximo 512 y sin Read/Write. Crea un banco y laboratorio v03 separados, sin guardar escenas existentes.

Archivos de código modificados: RobotPortraitGraphic.cs, RobotPortraitAuthoring.cs, RobotPortraitTestRunner.cs y RobotPortraitTests.cs. Las pruebas incluyen 20%, 19,9% y curación de vuelta a 20%.

## Verificación

Segunda ejecución completa en Unity Editor: **9/9 PlayMode superadas**, finalizada a las 11:21:04 (Bolivia). Resultado en `Tools/IronRequiem/Reports/RobotPortraitV03/playmode.xml`.

Se ejecutaron importación/transparencia/pivotes de los 23 sprites; límites y curación (incluidos 20% y 19,9%); impactos; giros e histéresis; pausa/cambio de cámara; chispas; carga real con F, cancelación y golpe confirmado; disparos, señal de ejecución, OVERDRIVE, muerte y Bind de reaparición; suscripciones; ratón virtual invertido y gamepad virtual. Se guardaron 156 capturas reales del laboratorio a 720p, 1080p y 1440p. Se inspeccionaron las capturas de los dos lados del límite de 20%.

La primera ejecución tuvo 7/9: una excepción externa al retrato por bloqueo de escritura de VisualOverhaul/heartbeat.txt y una carga que regresó a cero. Se conserva `playmode-attempt1.xml`. Se repitió la suite sin cambiar gameplay ni relajar aserciones; la segunda pasó completa. No se atribuye una causa definitiva a la cancelación transitoria de carga.

Medición limitada al componente: 1.000 llamadas de LateUpdate tras calentamiento, 0 bytes asignados tanto en reposo como con carga/chispas. Excluye Canvas y GC del frame completo. Las 23 texturas reportaron 48.255.840 bytes de memoria runtime en Editor. No es un benchmark de FPS ni una prueba de Player compilado.

Verificación final: 847 archivos protegidos sin cambios respecto al respaldo (escenas/prefabs preexistentes, scripts base/gameplay, ProjectSettings y paquetes). 23 copias PNG idénticas a las fuentes v03. Editor restaurado a IntroMenu, limpio, fuera de Play Mode; misma escena de inicio. Solo se creó el laboratorio v03, sin añadirlo a Build Settings. No se probó una partida completa ni el recorrido físico de ejecución/extracción de enemigos: las comprobaciones respectivas usan los eventos/componentes reales en el laboratorio.
