# Menú inicial — presentación e interacción

Trabajo del 20 de noviembre de 2026. Alcance: el menú animado de inicio de IRON REQUIEM.

## Cambios

Se conserva el Canvas y el flujo de RequiemMenuView, la cápsula, el protagonista, los robots de fondo, la interacción de explosiones, la cámara y HangarLaunchSequence. La entrada y la salida mantienen sus animaciones existentes, sin añadir una espera al comenzar la partida.

La presentación usa grafito, marfil, naranja y cian, siguiendo la armadura y el visor del protagonista. El título mantiene dos líneas con jerarquía entre IRON y REQUIEM. JUGAR recibe mayor altura y tratamiento naranja; las acciones secundarias usan placas oscuras. Se ajustan tipografía, márgenes, alineación, separación y detalles luminosos.

MenuPolishButtonFx distingue reposo, selección, hover, pulsación y deshabilitado. Solo desplaza el texto hasta cuatro unidades; el rectángulo interactivo conserva posición, tamaño y escala. La interpolación dura aproximadamente 0,11 s. Los sonidos existentes se reproducen por eventos, con límites de frecuencia; conservan el mixer y el volumen actuales.

MainMenuPolish recupera foco válido antes del procesamiento de UI y restaura el botón OPCIONES al volver de ajustes. UI/Cancel permite volver con mando. La autoría usa un UnityEvent para llamar ShowHome, sin cambiar el ensamblado de UI ni su código compartido con pausa y muerte.

No se cambia HangarButtonFx, su estilo global, RequiemMenuView, gameplay, HUD, pausa, muerte, materiales del escenario, paquetes ni ProjectSettings. Los efectos antiguos se desactivan solo en los botones de esta escena, para que no compitan con la presentación nueva.

## Archivos

- `Assets/IronRequiem/Scripts/Menu/MainMenuPolish.cs`
- `Assets/IronRequiem/Scripts/Menu/MenuPolishButtonFx.cs`
- `Assets/IronRequiem/Editor/Menu/MenuPolishAuthoring.cs`
- `Assets/IronRequiem/Editor/Tests/MenuPolishTestRunner.cs`
- `Assets/IronRequiem/Tests/PlayMode/MenuPolishTests.cs`
- Copia de trabajo: `Assets/IronRequiem/Scenes/IntroMenuPolishLab.unity`.
- Destino tras verificar la copia: `Assets/FPS/Scenes/IntroMenu.unity`.

Respaldo y hashes previos: `Tools/IronRequiem/Backups/20261120-MenuPolish/`. Evidencias: `Tools/IronRequiem/Reports/MenuPolish/`.

## Pruebas ejecutadas

Unity Editor 6000.5.5f1. Menú original: **1/1**, capturas previas completas finalizadas a las 11:03:21. Copia final: **5/5**, a las 11:13:17. Integración en IntroMenu: **5/5**, a las 11:18:12, zona America/La_Paz. Resultados NUnit en `before.xml`, `after-lab.xml` y `after.xml`, dentro de la carpeta de evidencias.

- [x] Capturas nativas de GameView, antes/después, de inicio y opciones a 1280×720, 1920×1080, 2560×1440 y 3440×1440. Las dimensiones reales de Screen y PNG se comprueban; no se reescala ni recompone la imagen.
- [x] Límites de botones y desbordamiento de texto comprobados en las cuatro resoluciones. Revisión visual de inicio a 1080p y ultrapanorámico, y de opciones a 720p. Se corrigió el margen heredado que hacía demasiado pequeños VOLVER y CONTROLES.
- [x] Movimiento real de un robot de fondo y de la cámara; protagonista inicialmente apagado y secuencia existente presente.
- [x] Teclado virtual: flecha abajo, Enter y Escape. Mando virtual: cruceta, A y B. Se comprueban foco inicial, apertura de opciones y retorno contextual a OPCIONES.
- [x] Ratón virtual: opciones, volver y salir. Los clics sobre UI no detonaron robots de fondo. El volumen conserva el cambio al reabrir ajustes y la prueba restaura su valor previo.
- [x] JUGAR invocado dos veces: carga real de Map01_Campaign_S1 exactamente una vez mediante la secuencia existente.
- [x] Reposo, selección, hover, pulsación y deshabilitado. Posición, escala y tamaño del rectángulo de clic permanecen iguales.
- [x] 1.000 llamadas de Update tras calentamiento, para un botón estabilizado y MainMenuPolish: **0 bytes administrados**. Se excluyen Canvas, renderizado, otros componentes del menú y asignaciones del frame completo; no es una medición de FPS ni de Player compilado.
- [x] GameView restaurado: selección 0 y siete tamaños originales; tamaños temporales retirados. Editor fuera de Play Mode, escena vacía limpia y configuración de escena inicial restauradas al estado previo. Sin ventanas nuevas ni llamadas Show/Focus.
- [x] Integridad: de 1.784 archivos preexistentes protegidos, solo cambió IntroMenu.unity. Los otros 1.783, incluidos scripts, prefabs, materiales, ProjectSettings y paquetes, conservan sus hashes. En IntroMenu, **1.860 objetos serializados fuera del Canvas principal permanecen idénticos**; no se añadieron objetos fuera de ese Canvas. GUID del menú conservado; laboratorio fuera de Build Settings.

La captura final usa la petición asíncrona ScreenCapture.CaptureScreenshot y espera un PNG completo. Los intentos iniciales con RenderTexture y cámara UI se descartaron por no reproducir correctamente el Overlay. También se corrigieron la propiedad de URP de solo lectura y los índices de tamaños temporales del capturador. El historial está en `progress.txt`; los resultados y PNG finales corresponden al método nativo validado.

## Pendientes y límites

- [ ] Cierre real de una aplicación compilada: SALIR ejecutó y verificó la rama de Editor, que registra el cierre sin cerrar Unity.
- [ ] Prueba con teclado, ratón y mando físicos. Los dispositivos usados por la suite fueron virtuales y atravesaron el Input System y EventSystem reales.
- [ ] Escucha subjetiva de sonidos y comparación de niveles en altavoces/auriculares. Se conservan clips y mixer del proyecto; no se afirma una evaluación auditiva.
- [ ] Perfilado completo CPU/GPU en Player. La medida de asignaciones corresponde solo a los callbacks especificados.
- [ ] Recorrido completo de gameplay y pruebas completas de pausa/muerte. No se modificaron esos sistemas; las pruebas de esta tarea solo cargaron la escena de destino.

Los PNG finales se encuentran en `before/`, `after-lab/` y `after/`. `capture-diagnostics.txt`, `gameview-restoration.txt`, `editor-restoration.txt`, `integrity.txt` y `scene-preservation.json` documentan dimensiones, restauración y alcance.

Auditoría final del Editor, 11:20:18 (America/La_Paz): fuera de Play Mode, sin errores de compilación capturados. La comparación de hashes se repitió después de las pruebas integradas y mantuvo el mismo resultado: únicamente IntroMenu.unity cambió entre los archivos preexistentes protegidos.
