# IRON REQUIEM — checklist de trabajo

Actualizada: 31 de octubre de 2026, America/La_Paz.

Esta lista ordena las solicitudes del usuario. No autoriza a implementar todos los bloques a la vez. **Ejecutar solo el bloque asignado**, cerrar su verificación y registrar el resultado antes de pasar a otro. Crear esta lista no modifica el juego.

`[x]` significa entregado y respaldado por informes. `[ ]` significa pendiente, fallo reportado o revisión por completar. Una prueba automatizada anterior no cierra un fallo que el usuario continúa observando. Una referencia visual adaptada no implica aprobación artística final.

## Control de alcance y autorización

**Solicitud activa del 31/10 en este chat: actualizar esta checklist. No ejecutar cambios de gameplay, configuración, builds, instalaciones ni pruebas del juego como consecuencia de esa solicitud.**

Pedir un prompt, una explicación o una checklist autoriza a preparar ese contenido; no autoriza a ejecutar las tareas descritas. Estar anotado como pendiente tampoco activa un trabajo. Una autorización directa previa del usuario conserva su validez dentro de su alcance; no pedirla otra vez para cada paso rutinario.

Antes de actuar:

- [ ] Leer la última instrucción directa del usuario e identificar si pide información, planificación o ejecución.
- [ ] Registrar el ID y el resultado concreto solicitado. Si el usuario pidió varios IDs explícitamente, registrar ese conjunto.
- [ ] Revisar informes y cambios recientes para no repetir tareas ya entregadas.
- [ ] Delimitar los sistemas/archivos que será necesario modificar y los que deben conservarse.
- [ ] Definir una comprobación proporcional al cambio y preparar el respaldo necesario.

Durante la ejecución de una tarea autorizada:

- [ ] Hacer únicamente los cambios y pasos técnicos necesarios para completar el alcance solicitado.
- [ ] Conservar decisiones de diseño, controles, balance y apariencia ajenos a ese alcance.
- [ ] Registrar problemas adicionales como pendientes; si su solución amplía el alcance, consultar antes de modificarla.
- [ ] No comenzar otro ID porque el actual terminó ni porque se detectó una oportunidad de mejora.
- [ ] No instalar herramientas, rehacer sistemas o actualizar dependencias por iniciativa ajena a la tarea.

Al cerrar:

- [ ] Anotar fecha, petición directa que autorizó el trabajo, archivos modificados y resultado.
- [ ] Enlazar evidencia y distinguir compilación, pruebas automatizadas, prueba manual y medición de rendimiento.
- [ ] Marcar completado solo lo comprobado; dejar visibles las limitaciones y fallos pendientes.
- [ ] Informar qué se terminó y cuál sería el siguiente ID, sin iniciarlo automáticamente fuera del alcance autorizado.

## Registro de tareas e IDs estables

Los IDs permiten pedir, por ejemplo, «ejecuta únicamente IR-02». Esta tabla es un registro de planificación: **no inicia trabajos ni declara autorizada su ejecución por sí sola**. Las subtareas y criterios de cierre están en los bloques correspondientes más abajo.

| ID | Tarea | Estado registrado | Criterio de cierre |
|---|---|---|---|
| IR-01 | Reproducir problemas y guardar referencia | Pendiente de comprobar estado actual | Pasos de reproducción y capturas/medidas guardados |
| IR-02 | Corregir recogida del cañón | Fallo reportado; no cerrado por pruebas históricas | Interacción mediante E y ciclo completo comprobados |
| IR-03 | Reducir blur | Pendiente de comprobar estado actual | Causa identificada y comparación equivalente antes/después |
| IR-04 | Rendimiento: objetivo 120 FPS a 1080p | Objetivo planificado; sin certificación en esta checklist | Ejecutable PC, carga/calidad definidas, comparación y prueba sostenida |
| IR-05 | Destrucción progresiva por vida | Pendiente de comprobar implementación actual | Umbrales, piezas, animaciones y LOD compatibles |
| IR-06 | Explosión del cuerpo al morir | Pendiente de comprobar implementación actual | Muerte y efectos únicos; armas desprendidas conservadas |
| IR-07 | Pulido visual según referencias | Entregas existentes; ajustes concretos por definir | Diferencias solicitadas corregidas y comparadas |
| IR-08 | Regresión y rendimiento final | Pendiente de los cambios que se autoricen | Mecánicas verificadas y mediciones finales documentadas |
| IR-09 | Protagonista definitivo | Integración v02, melee y slide asimétrico verificados en laboratorio | Variante y rig compartido integrados conservando el controlador FPS |
| IR-10 | Mapa 1 «Búnker abandonado» (Prompt 9): 17 cuartos + jefe | F0 y F1 en papel; **PARADA F1**, esperando aprobación | Recorrido completo sin soft-locks, 120 FPS a 1080p en los 3 cuartos más cargados y zona protegida intacta (PROMPTS_CHATS §9) |
| IR-11 | IA del jefe del Mapa 1 | Pendiente; solo conceptos (Map01/03_ENCUENTROS §A7) | Jefe elegido por el usuario, con fases, ataques telegrafiados y armas robables verificados en Play |

Estados que puede adoptar una tarea: **pendiente / autorizada / en curso / por verificar / completada / bloqueada por un impedimento documentado**. «Por verificar» no equivale a «completada». Antes de iniciar un ID, contrastar esta tabla con las entregas de otros chats y con la petición directa que corresponda.

Ficha para cada nueva ejecución:

```text
ID:
Fecha:
Petición directa del usuario que la autoriza:
Resultado solicitado:
Alcance y archivos previstos:
Estado:
Cambios realmente aplicados:
Verificación realizada y evidencia:
Limitaciones / pendientes:
```

## Entregado: conservar y no rehacer

- [x] Mantener Unity 6000.5.5f1, URP e Input System y reutilizar la arquitectura FPS.
- [x] Jugador provisional con controlador, cámara y armas de la plantilla.
- [x] Dash aéreo y slide con Shift + C, con partículas de contacto.
- [x] Pistola modular básica integrada; escopeta conservada fuera del inventario inicial.
- [x] T-07 optimizado: 12.066 / 6.032 / 2.511 triángulos totales por nivel.
- [x] Enemigo común optimizado integrado en CharacterLab y T07OptimizationLab, conservando originales y GUID.
- [x] Rig provisional real de 17 huesos, animaciones y compatibilidad con LOD, navegación y cañón independiente.
- [x] Sistemas de desprendimiento, arma temporal, carga, energía, humo crítico, lanzamiento y explosión implementados. La recogida tiene un fallo reportado que se trata abajo.
- [x] Presentación de explosión del cañón implementada con partículas, audio y sacudida limitada por distancia.
- [x] Tamaño y boca del cañón equipado corregidos; carga magenta implementada con referencia al último disparo del video.

Fuentes del estado: `DESIGN_V02_UPDATE.md`, `WEAPON_FEEDBACK.md`, `T07_OPTIMIZATION.md`, `T07_ANIMATION_VFX.md` y `CANNON_CHARGE.md`. El último informe registra 45 pruebas aprobadas; son resultados de esa revisión, no una nueva validación de esta checklist. Los informes históricos anteriores pueden describir funciones que ya fueron añadidas después.

## 1. Reproducir el estado actual y establecer la referencia

- [ ] Respaldar los archivos que vaya a tocar el bloque activo; comprobar escenas con cambios pendientes.
- [ ] Identificar la escena y el prefab concretos donde falla la recogida; anotar pasos de reproducción.
- [ ] Capturar el aspecto borroso actual con resolución y cámara registradas.
- [ ] Registrar una carga reproducible con varios T-07, disparos, armas sueltas y explosiones: equipo, resolución, calidad, cantidad de objetos, tiempos CPU/GPU y GC. Distinguir Editor de Player PC.

**Cierre:** problema de recogida reproducible y referencia visual/de rendimiento guardada. Si no se reproduce, registrar qué se probó y qué dato falta, sin inventar la causa.

## 2. Corregir la recogida del cañón — prioridad funcional

- [ ] Diagnosticar entrada E, distancia, línea de visión, raycast, capas, colliders, demora y estado del módulo.
- [ ] Comprobar referencia a Weapon_T07_Temporary_Optimized, espacio en inventario y activación del arma.
- [ ] Corregir la causa dentro de StolenWeaponCarrier/StolenCannon y los sistemas existentes.
- [ ] Dar una indicación mediante la UI existente cuando falte alcance, haya inventario lleno o el módulo no esté disponible.
- [ ] Verificar recogida desde distintos ángulos y junto al cadáver, varias armas cercanas y pulsaciones repetidas.
- [ ] Verificar equipamiento único, carga/disparo, energía persistente, cambio de arma, lanzamiento y nueva recogida, humo y explosión.

**Cierre:** el caso reportado funciona en Play Mode y no reaparecen duplicados, energía regenerada o LOD visibles del módulo equipado. No basta con compilar o llamar directamente a Equip: probar la interacción real a través del sistema de entrada.

## 3. Reducir el blur y conservar legibilidad

- [ ] Identificar qué cámaras, Volumes o ajustes producen el desenfoque real.
- [ ] Revisar Motion Blur, profundidad de campo, antialiasing/ghosting y resolución interna solo donde estén presentes.
- [ ] Aplicar un ajuste moderado de nitidez, preservando el estilo y un antialiasing razonable.
- [ ] Comparar imágenes equivalentes, giros rápidos y movimiento; comprobar arma, enemigos y efectos de carga.

**Cierre:** comparación antes/después registrada, con ajustes exactos y sin introducir sharpening excesivo o defectos nuevos evidentes. No desactivar todos los efectos por defecto.

## 4. Optimizar el rendimiento medido — primera pasada

- [ ] Usar la referencia del bloque 1 para identificar los cuellos de botella principales.
- [ ] Comprobar uso del enemigo común optimizado, LOD y sombras con varios enemigos.
- [ ] Revisar coste de luces, transparencias, partículas, fragmentos y limpieza de armas/efectos.
- [ ] Revisar CPU, navegación, físicas y GC donde las medidas señalen un problema.
- [ ] Aplicar únicamente las mejoras justificadas; no añadir pooling, otro perfilador o una arquitectura nueva por rutina.
- [ ] Si se usa pooling, verificar el reinicio completo de estado, eventos, salud, energía, piezas y VFX.
- [ ] Comparar antes/después con la misma carga y documentar límites de la medición.

**Cierre:** medidas comparables y mecánicas conservadas. El objetivo de planificación solicitado es **120 FPS a 1920×1080 en RTX 4050 Laptop**, aproximadamente **8,33 ms por frame**. Calidad y carga de combate deben quedar definidas antes de evaluar el resultado. Medir un ejecutable fuera del Editor, usar calentamiento y una prueba de al menos cinco minutos, registrar tiempos CPU/GPU, mediana, p95/p99, frames por encima de 8,33 ms y tirones. Comparar bajo las mismas condiciones; validar el resultado final en un build sin instrumentación de desarrollo. Registrar 1440p por separado. Reducir triángulos, limitar el contador a 120 o medir solo el promedio no cierra este objetivo. No se garantiza alcanzarlo sin evidencia. Pedir el prompt de este trabajo no activó su ejecución.

## 5. Destrucción progresiva del robot según vida

- [ ] Preparar una variante/copia del T-07 animado y optimizado actual.
- [ ] Usar Health como única fuente de vida y umbrales configurables, inicialmente propuestos en 75 %, 50 % y 25 %.
- [ ] Añadir daños visuales leves, desprendimiento de placas secundarias y deterioro crítico progresivo.
- [ ] Separar geometría y preparar interiores/tapas donde sea necesario, conservando rig, UV y presupuestos de LOD.
- [ ] Mantener el daño visible al cambiar de LOD; las piezas perdidas no deben reaparecer.
- [ ] Conservar el arma y su unión como sistema independiente; no romper extremidades esenciales ni cambiar IA o velocidad incidentalmente.
- [ ] Manejar golpes que crucen varios umbrales y daño simultáneo sin repetir efectos o desprendimientos.

**Cierre:** deterioro gradual visible y coherente con la vida, compatible con locomoción, ataque, desarme y todos los LOD.

## 6. Explosión final del cuerpo del robot

- [ ] Integrar la explosión con la muerte/colapso existentes; detener navegación y ataques inmediatamente al morir.
- [ ] Reutilizar y adaptar los VFX disponibles para el cuerpo, con destello, chispas, restos, humo y audio.
- [ ] Ejecutar muerte, objetivos, loot y desprendimiento del arma una sola vez.
- [ ] Mantener como presentación visual por defecto; no añadir daño al jugador o cadenas de explosiones sin un alcance explícito.
- [ ] Conservar cualquier cañón que ya esté desprendido, recogido o equipado.
- [ ] Probar golpe letal desde vida completa, muerte durante ataque, cañón robado y varias muertes simultáneas.

**Cierre:** el robot muere y explota sin duplicar daño/eventos, borrar armas ajenas o dejar efectos persistentes. Esta es la explosión del cuerpo; no rehacer la explosión del arma ya entregada.

## 7. Revisión visual de entregas existentes

- [ ] Revisar en juego el tamaño/posición del cañón y su carga magenta frente al último disparo del video.
- [ ] Revisar la explosión del arma frente al video y anotar diferencias concretas que aún se quieran ajustar.
- [ ] Revisar peso de las animaciones, poses, pies y uniones del T-07; hay margen de pulido manual documentado.
- [ ] Aplicar únicamente los ajustes concretos derivados de esa revisión, manteniendo la lógica de combate.

**Cierre:** cambios artísticos pedidos y revisados; no reconstruir por completo sistemas ya funcionales porque haya una referencia nueva.

## 8. Regresión y rendimiento final

- [ ] Comprobar movimiento/cámara, dash, slide y pistola.
- [ ] Comprobar IA, animación, daño progresivo, muerte y LOD.
- [ ] Comprobar el ciclo completo del arma robada después de todos los cambios.
- [ ] Repetir la carga representativa con las nuevas piezas y explosiones, preferentemente en Player PC.
- [ ] Corregir las regresiones o costes nuevos comprobados, conservando los resultados anteriores para comparar.
- [ ] Entregar pruebas, comparaciones, cambios de configuración, archivos afectados y pendientes; dejar el Editor fuera de Play Mode sin guardar cambios de ejecución.

## IR-09 — protagonista optimizado v01: integración inicial en laboratorio

Fecha: 31 de octubre de 2026, America/La_Paz. Autorización directa: integrar únicamente IR_Protagonist_Optimized_v01.zip conforme a su PROMPT_INTEGRACION.txt. Esta petición posterior autoriza este bloque; la ficha documental anterior no inicia ni impide otras tareas.

- [x] Leer AGENTS.md, README, PROMPT_INTEGRACION y Verification. Verificar manifiesto del ZIP y carpeta de entrega: cero diferencias. Confirmar mallas estáticas de origen, sin rig, pesos ni clips.
- [x] Importar copias bajo Assets/IronRequiem/Art/Player/ProtagonistV01, conservando fuentes externas/ZIP y sus UV, geometría, texturas, escala y uniones. Material URP/Lit compartido: BaseColor sRGB, Normal como normal map y MetalSmooth lineal, metallic R/smoothness A.
- [x] Preparar en Blender un rig Generic inicial de 50 huesos, incluidos dedos, y pesos para los tres LOD sobre el mismo esqueleto. Mantener las cinco piezas por LOD y sus 21.108 / 11.628 / 6.122 triángulos. Las cuatro piezas FPS proceden expresamente de IR_Protagonist_FPS_Parts (6.070 triángulos), verificadas antes de transferir los pesos. Las guías originales del cuerpo permanecen; los sockets FPS derivados se sitúan en las muñecas y el derecho se orienta con el módulo activo.
- [x] Preparar clips iniciales de reposo, caminar, mano normal, puño, agarre y una pose de golpe para previsualización. El golpe de previsualización no se conecta a controles, daño ni combos. Rig editable: Tools/IronRequiem/Blender/Working/Protagonist/IR_Protagonist_Rigged.blend.
- [x] Crear las variantes Protagonist_Body, Protagonist_FPS y Assets/IronRequiem/Prefabs/Player/Player_IR_Protagonist_v01.prefab. Reutilizar PlayerCharacterController, PlayerInputHandler, PlayerWeaponsManager, StolenWeaponCarrier y PlayerBodyPresentation; el cuerpo conserva sombras y las manos FPS usan la cámara/capa de armas, sin superposición del cuerpo completo.
- [x] Mostrar mano normal cuando no hay arma activa; ocultar únicamente la mano derecha sustituida al equipar pistola o cañón, conservando antebrazo y apoyo izquierdo. Derivar la presentación del arma activa real y del módulo del portador; no crear inventario ni controles nuevos. Conservar muzzle, proyectiles, carga, energía, humo crítico, lanzamiento y explosión existentes.
- [x] Crear Assets/IronRequiem/Scenes/ProtagonistIntegrationLab.unity con tres figuras de inspección de LOD y jugador integrado. Conservar en sus overrides los ajustes de cámaras/URP y transformaciones de cámara/sockets de la MainScene guardada, leída mediante escena de preview. MainScene y jugador anterior no se sustituyen ni se guardan.
- [x] Verificar compilación y referencias: cero errores de compilación capturados y cero scripts ausentes. En Blender, 48 comprobaciones de uniones (codo/muñeca, ambos lados, tres LOD, cuatro poses), separación máxima aproximada 0,00000012 m. UV y pesos sin vértices huérfanos; poses evaluadas finitas. Evidencia interna: Tools/IronRequiem/Reports/Protagonist/rig.json, seams.json y deformation-probe.json.
- [x] Seis pruebas finales aprobadas en el Test Runner de Unity con Game View: rig/LOD/material/cámaras; movimiento, slide, dash con su binding existente Ctrl, ADS y disparo; modo normal y restauración de pistola; F para recoger, carga completa/disparo, 1/2 para cambiar, G para lanzar, nueva recogida con energía conservada, humo y explosión; agotamiento equipado y retorno a pistola; Tab para pausa y muerte sin manos duplicadas ni pérdida del módulo. El estado de inventario vacío/restauración y el agotamiento se preparan con APIs existentes para verificar presentación; no se presentan como controles melee implementados. XML final: Tools/IronRequiem/Reports/Protagonist/playmode-gui.xml, 6/6. Capturas internas fps-pistol, fps-normal-fist, fps-slide-aim, fps-cannon y fps-cannon-charge.
- [x] Conservar ejecuciones fallidas de diagnóstico separadas del XML final: batch sin captura de cursor y correcciones de preparación/sincronización de las pruebas. No se cambió el controlador ni la UI para acomodar el test. Respaldos y hashes: Tools/IronRequiem/Backups/20261031-protagonist. Restaurar la reordenación incidental de símbolos de compilación realizada por Unity; su conjunto no cambia.
- [ ] Pulir manualmente dedos, pulgar, contacto del agarre y pesos cercanos a nudillos sobre la topología generada. Las poses y pesos entregados son una primera versión verificable; no se presentan como animación final de producción. Pulir locomoción corporal a velocidades altas si se solicita una pasada de animación.
- [x] Implementar el primer golpe melee por la autorización posterior del 31/10 (véase v02 abajo). La entrega v01 no lo implementaba. No hay combos ni acción nueva de desarmar/holster; el modo normal sigue el inventario real.

Resultado de v01: integración inicial funcional en variantes y laboratorio; Editor fuera de Play Mode. En esa entrega no se ejecutaron builds ni tareas de rendimiento, blur, enemigos o VFX. No se instalaron paquetes ni se consumieron créditos. Herramientas: Blender local, APIs del Editor y Test Framework de Unity; skills FPS y armas modulares. Las reducciones de geometría del paquete no se convierten en afirmaciones de mejora de FPS.

### IR-09 v02 — montaje, golpe izquierdo y presentación del slide

Autorización directa del usuario: los cinco ajustes del protagonista; aclaraciones posteriores **E para recoger, F para golpear**, y slide menos sentado, con una rodilla flexionada. Sustituye la propuesta inicial de compartir F contextualmente. Alcance limitado a este bloque y a sus dependencias de entrada/verificación.

- [x] Crear Player_IR_Protagonist_Actions_v02.prefab y ProtagonistActionsLab.unity como variantes/copia separada. Conservar MainScene, el laboratorio y jugador v01, modelos fuente, LOD corporales, materiales, escalas físicas del módulo y del enemigo. Respaldos en Tools/IronRequiem/Backups/20261031-protagonist-actions.
- [x] Corregir montaje FPS por la guía Attachment real del modelo PistolModule_1000: mide 0,379 m desde la boca hasta la unión. La aproximación anterior terminaba unos 0,15 m dentro de la carcasa. Ajustar el antebrazo a esa guía con 3 mm de solape de tapa y eje paralelo a WeaponMuzzle, conservando la geometría/escala del arma, muzzle, ADS, disparo y retroceso. El cañón robado conserva su origen y su representación independiente.
- [x] Ocultar mano y antebrazo izquierdos en reposo, movimiento, ADS, disparo y uso del cañón. Mantener su rig/mesh y las sombras del cuerpo; mostrarlos únicamente durante el golpe y retirarlos al recuperar o interrumpirlo.
- [x] Añadir un único golpe izquierdo con F sobre ProtagonistMelee: preparación 0,12 s, extensión 0,10 s, impacto 0,04 s y recuperación 0,30 s, alcance inicial 1,55 m, radio 0,18 m y daño 25. Parámetros expuestos en el componente. Animación nativa LeftPunch_Articulated.anim con articulaciones de dedos, cierre de puño y pulgar adaptado mediante IK en Blender; objetivos animados de muñeca/codo y FK del antebrazo independientes del arma derecha. Sin combos ni movimiento adicional del jugador.
- [x] Consultar colliders con PhysX, excluir al jugador, comprobar oclusión por paredes y agrupar por Actor/Health para aplicar daño una sola vez por objetivo y ataque mediante Damageable. Mantener la pulsación sostenida sin repetición automática y bloquear nuevos golpes durante preparación/recuperación. Cancelar por pausa, muerte o desactivación.
- [x] Cambiar únicamente la recogida CollectWeapon a E y añadir Melee en F mediante APIs del Input System. Resolver el conflicto comprobado con NextWeapon en E quitando ese binding, anunciado al usuario; conservar Q, rueda, 1/2 y demás bindings. Mantener Interact genérico preexistente. Actualizar aviso HUD a [E] RECOGER CAÑÓN y referencias de recogida en pruebas/autoría. Si se pulsan E y F simultáneamente, dar prioridad a la recogida; una sola tecla no recoge y golpea a la vez.
- [x] Preparar copia real de la geometría inferior, cortada/cerrada a la altura de la cadera en Blender, con 7.080 triángulos, UV y pesos conservados. Usar su rig real en una representación FPS separada: derecha extendida e izquierda con rodilla flexionada, entrada/salida de 0,09 s, retirada al terminar, saltar, pausar o morir. Sin cabeza/torso en la cámara y sin duplicar las piernas corporales. Mantener AdvancedPlayerMovement y sus velocidades, duración, colisiones y trayectoria intactos; limitar las correcciones de suelo a la pose visual.
- [x] Reutilizar SlideContactEffects con dos contactos por raycast, ajustados a los puntos de rodilla/bota y normales reales. Chispas amarillas/naranjas en Particle System, material URP compartido existente, simulación mundial, vida de 0,08–0,18 s, máximo 24 partículas por contacto (48 total), sin luces ni fragmentos físicos. Detener y limpiar al perder contacto, saltar, quedar en el aire, pausar, morir o salir del slide. Mantenerlas independientes del humo crítico del cañón.
- [x] Nueve pruebas Play Mode del Test Runner aprobadas con entrada nativa virtual: montaje durante movimiento/ADS/retroceso/disparo; fases y articulación de puño/impacto único; pared/alcance/tecla sostenida/recuperación; E/F y prioridad simultánea; slide asimétrico/contactos/salto; pendiente de 10° y salida; pausa/muerte sin residuos; cañón robado con carga, cambio de arma, lanzamiento, nueva recogida, energía y explosión. Validar flexión de rodilla, emisión y limpieza reales, y ocultación de piernas por encuadre extremo (>55° hacia abajo) sin cambiar el slide. Separar la pierna derecha del volumen del arma. No se declaran pruebas manuales de teclado físico ni mediciones de FPS.
- [x] Guardar secuencia real de capturas en Tools/IronRequiem/Reports/ProtagonistActions/Secuencia.html y PNG 01–14; la vista 14 usa una cámara auxiliar de prueba para inspeccionar las chispas. El objetivo rojo de las capturas es un objeto de prueba que cambia de color con Health.OnDamaged, no un VFX nuevo del juego. XML final: playmode.xml. Las pasadas anteriores/diagnóstico se conservan separadas; ausencia de tests no se considera aprobación.

Corrección posterior solicitada: únicamente slide. Se elimina la ocultación por mirar más de 55° hacia abajo y el uso de los ejes inclinados de cámara para colocar las piernas. La pose queda anclada al jugador, orientada a SlideDirection al inicio; mirar cambia el encuadre, sin acercar la geometría a la cámara. El controlador aplica una única restricción horizontal durante el slide, configurable en AdvancedPlayerMovement (80° por lado); al terminar recupera el giro libre. No cambia velocidad, duración ni trayectoria. Cuatro pruebas específicas Play Mode aprobadas: pose al mirar abajo, dirección fija/límites a ambos lados/liberación de giro, salto/limpieza y pendiente/salida. Resultado: Tools/IronRequiem/Reports/ProtagonistActions/SlideFacing/playmode.xml. La ocultación extrema documentada en la entrega inicial queda sustituida por esta corrección. A 89° los pies extendidos pueden salir naturalmente por arriba del encuadre; las piernas permanecen activas y sus raíces visibles. Escenas, prefab, entrada, ProjectSettings y paquetes sin cambios.
Archivos de código nuevos: ProtagonistMelee, ProtagonistSlidePresentation y autoría/verificación limitada de estas variantes. Ajustes acotados a ProtagonistHandsPresentation, SlideContactEffects, MechanicInput, MechanicHud y bindings; las pruebas previas de recogida se actualizan de F a E. Herramientas: Blender local, APIs del Editor, PhysX, AnimationClip, Particle System, Input System y Test Framework existentes. No instalar paquetes, consumir créditos ni ejecutar tareas adicionales de rendimiento, enemigos o explosiones. El laboratorio nuevo queda listo fuera de Play Mode; MainScene no se promociona ni se guarda.

## Límites para evitar trabajo adicional

- Modificar solo lo necesario para el bloque activo y registrar dependencias concretas si aparece un impedimento.
- No rehacer controlador, inventario, IA, rig, carga o explosión que ya funcionen sin una causa comprobada.
- No añadir enemigos, armas, niveles, jefes, multijugador, UI nueva completa ni cambios de balance fuera de la tarea.
- No cambiar Editor, URP o Input System, actualizar dependencias incidentalmente ni instalar paquetes experimentales.
- No comprar assets, consumir créditos o activar cuentas. Usar herramientas pertinentes ya disponibles.
- Conservar fuentes, GUID y copias de respaldo; usar APIs del Editor para escenas/prefabs y no guardar escenas con cambios del usuario.
- Antes de marcar una tarea: anotar qué se cambió, cómo se verificó y dónde está la evidencia. Si una comprobación no se ejecutó, dejarla abierta.

## Corrección de MainScene — slide y pistola (30 de octubre de 2026)

- [x] Corregir la integración del slide: MainScene carecía de AdvancedPlayerMovement y SlideContactEffects. Se conectaron los componentes existentes mediante overrides de escena, sin cambiar el controlador ni sus velocidades (13 m/s, sprint ×1,5).
- [x] Restaurar Weapon_IR_Pistol con PistolModule_1000 como arma inicial. MainScene apuntaba a Weapon_Blaster; se conservan ambos prefabs y sus fuentes.
- [x] Comprobar en Play Mode por teclado/ratón virtual del Input System: modelo/muzzle y disparo de la pistola, W+Shift+C con desplazamiento/partículas/salida de slide, agacharse en reposo y cancelación del slide con salto. Tres verificaciones aprobadas en Tools/IronRequiem/Reports/SlidePistol/play-smoke.json. Se ejecutaron como comprobaciones dentro de Play, sin Test Runner, para preservar los ajustes pendientes del HUD de MainScene.

No se cierra el objetivo pendiente de 120 FPS con esta corrección. La escena de comprobación separada es Assets/IronRequiem/Scenes/SlidePistolLab.unity; los cambios de ejecución no se guardaron.

## Corrección de rebaba del cañón equipado — 30 de octubre de 2026

- [x] Revisar el video y la malla: recortes abiertos e irregulares en el módulo original. Preparar copia en Blender, recortar las puntas por planos y cerrar los cortes con tapas e interior compartido; conservar UV de la superficie y vértices de la boca (error 0). Derivada T07_CannonClean.fbx, tres niveles cerrados; solo LOD0 se asigna a la vista FPS existente.
- [x] Actualizar Weapon_T07_Temporary_Optimized mediante APIs del Editor, conservando GUID, sockets y todas las transformaciones. Fuentes, enemigo y módulo del mundo con sus LOD permanecen intactos. Respaldo en Tools/IronRequiem/Backups/20261030-cannon-burr.
- [x] Dos comprobaciones dentro de Play Mode con entrada nativa virtual: recogida/modelo limpio/alineación durante movimiento y ADS; disparos consecutivos cargados/energía/sin impacto inmediato en el jugador. Tools/IronRequiem/Reports/CannonBurr/play-smoke.json (no ejecución de Test Runner). Verificada imagen equipada en el laboratorio. Sin guardar MainScene ni sus ajustes pendientes.

## Pistola sin partículas heredadas y slide estable — 31 de octubre de 2026

- [x] Desactivar en Weapon_IR_Pistol los cuatro VFX_GunCylinder de la plantilla (autoplay/emisión activos); conservar sus fuentes y el efecto independiente de boca. Mantener VFX_OverheatPrimary desactivado y conservar el humo crítico del cañón. Aplicación mediante APIs del Editor, sin guardar escenas.
- [x] Exponer SuppressWeaponBob en el contrato de movimiento existente y suprimir/reiniciar el balanceo en PlayerWeaponsManager durante el slide; conservar ADS, rotación de cámara, retroceso, cambio de arma y balanceo al caminar.
- [x] Dos verificaciones aprobadas dentro de Play Mode con teclado/ratón virtual del Input System: reposo y tres disparos de pistola sin partículas heredadas; slide con socket estable, desplazamiento, ADS, giro, disparo y recuperación del balanceo al caminar. Evidencia interna: Tools/IronRequiem/Reports/SlideAim/play-smoke.json. No fue una ejecución del Test Runner. Editor devuelto a MainScene fuera de Play, preservando los cambios pendientes del usuario.

## Registro documental — 31 de octubre de 2026

- [x] Por petición directa de crear una constancia de tareas y evitar trabajo no solicitado, actualizar esta checklist existente con IDs, estados, control de alcance y una ficha de ejecución.
- [x] Incorporar al plan el objetivo de 120 FPS a 1080p y conservar los registros anteriores sin volver a ejecutar sus tareas.
- [x] Limitar esta actualización a `Docs/IronRequiem/CHECKLIST_TRABAJO.md`. No se modificaron assets, código, escenas, configuración ni se ejecutaron pruebas o builds del juego.

## Nuevo robot — optimización y separación de piezas (31/10/2026)

- [x] Petición directa: «optimiza y separa por piezas este nuevo modelo», fuentes 9e4ba63501b568bf7eff159488112f21.fbx y cd151c50f4f37a89f763f89018003056.glb. Alcance exclusivo de geometría; no integración de gameplay.
- [x] Inspeccionar ambas fuentes: sin rig, pesos o animaciones; FBX 50.060 triángulos, 2 islas reales, mapas 4K. Usar la copia del FBX como base, conservar hashes de ambos originales.
- [x] Entregar 15 piezas con pivotes y tapas cerradas, UV exteriores conservadas, un atlas/material compartido y tres LOD de 24.104 / 12.376 / 5.494 triángulos. Escala de origen conservada. Texturas derivadas 2K y MetalSmooth para URP.
- [x] Reimportar y verificar 22 archivos en Blender: tres LOD FBX/GLB, ensamblaje de piezas y 15 FBX individuales. Evidencia: Tools/IronRequiem/Reports/NewRobotOptimization/roundtrip.json. Archivo editable y vistas reales en Tools/IronRequiem/Exports/IR_NewRobot_Optimized_v01.
- [ ] Rig/animaciones e integración/prefab Unity: fuera del alcance solicitado, no ejecutados. No se declaran mejoras de FPS ni pruebas de gameplay. Escenas, modelos existentes, configuración y mecánicas del juego conservados.
## Nuevo robot — animación de presentación (01/11/2026)

- [x] Autorización: prompt adjunto y dos videos del 01/11; alcance exclusivo de este robot. Articulación rígida de 16 huesos, un peso por vértice y 45 piezas entre los tres LOD, con geometría/UV conservadas. Copia editable en Tools/IronRequiem/Exports/IR_NewRobot_Animated_v01; originales intactos.
- [x] Prefab independiente Assets/IronRequiem/Prefabs/Robots/NewRobot_Animated_v01.prefab y escena Assets/IronRequiem/Scenes/NewRobotAnimationLab.unity. Altura inicial 3 m sobre raíz de presentación, material URP compartido, siete clips: reposo, caminar, giro izquierdo/derecho, preparación, elevación/retroceso bilateral suave y recuperación. Marcha adaptada al video y a las proporciones del modelo; no extracción directa de animaciones del video.
- [x] Animator con locomoción y capa superior enmascarada. Attack activa la secuencia, CancelAttack la interrumpe y ShotSpeed permite ajustar su ritmo visual. Evento de presentación único y socket del módulo frontal derecho; sin crear IA, daño, proyectiles, VFX ni controles.
- [x] Cuatro pruebas específicas Play Mode aprobadas; ninguna suite ajena ejecutada. Comprobados escala/referencias/topología, apoyo de pie a velocidad de referencia, giros sin root motion, elevación real del cañón, locomoción durante disparo, repetición/cancelación y 15 renderers de un único LOD visible. También reimportación del nuevo FBX en Blender (16 huesos/45 piezas/7 clips). Resultado final: Tools/IronRequiem/Reports/NewRobotAnimation/playmode.xml. Correcciones de altura, máscaras y bounds validadas; las pasadas de diagnóstico quedan separadas.
- [ ] Conexión a combate real: fuera del alcance actual; el paquete estático no tenía un sistema de disparo conectado. Los videos se usan como referencia de poses/ritmo, sin presentar una adaptación manual entre geometrías distintas como una copia exacta.

Uso: abrir NewRobotAnimationLab y pulsar Play para la secuencia automática; para disparo manual, desactivar AnimationPreview_Only y usar el trigger Attack en Animator o Preview Shot en NewRobotAnimationPresentation. MainScene y personajes anteriores no sustituidos; se preservaron los cambios pendientes del usuario antes de las pruebas.
## Big Bot — IA y disparos de combate (01/11/2026)

Alcance exclusivo: Big Bot. Fuentes identificadas como NewRobot_Animated_v01, independiente del T-07; altura 3 m, 16 huesos rígidos, 45 piezas en tres LOD y siete clips. La fuente no tenía Health, colliders, IA, proyectiles ni sistema de armas desmontables.

- [x] Crear la variante Assets/IronRequiem/Prefabs/Enemies/BigBot_Combat_v01.prefab, el proyectil derivado Projectile_BigBot y Assets/IronRequiem/Scenes/BigBotCombatLab.unity mediante APIs del Editor. Conservar modelos, animaciones fuente, GUID, escenas anteriores, URP, Input System y controles.
- [x] Reutilizar EnemyController, EnemyMobile (Patrol/Follow/Attack), DetectionModule, NavigationModule, PatrolPath, Health y Damageable. Añadir un contrato opcional de política en EnemyMobile para este actor; los enemigos sin política conservan su flujo. Afiliación enemiga 0, frente al jugador 1. Distancia y LOS física; memorizar únicamente la última posición visible, cancelar preparación al perder visión y buscar ruta/posición de tiro con NavMeshAgent.
- [x] Ajustar cuerpo y navegación a su tamaño: collider derivado de la geometría, radio de agente 2,25 m, altura 3,05 m, velocidad 1,4 m/s. Bake propio del laboratorio con ese espacio libre, sin modificar tipos de agente globales ni rebakear MainScene. Animator sin root motion, ciclo de marcha según velocidad real y huesos de antebrazo orientados conservando el retroceso existente.
- [x] Medir por separado en Blender las bocas de los dos tubos superiores y ajustar sus sockets solo en la variante. Alternar los dos WeaponController; preparación visual de 0,7 s sincronizada al evento de animación, seguida de proyectil recto a una posición capturada, sin homing. Reutilizar ProjectileStandard, VFX, impactos, sonido y exclusión de colliders del propietario. Comprobar volumen de salida y trayectoria antes del disparo.
- [x] Exponer en BigBotCombat daño (12), velocidad (18 m/s), alcance (35 m), intervalo tras disparo (2,2 s), preparación (0,7 s), distancia de ataque (12 m) y detección (26 m). Vida inicial 240 en Health. Mantener el procesamiento único de muerte/objetivos/loot de EnemyController; detener inmediatamente navegación, ataques, Animator y efectos de preparación.
- [x] Compilación correcta y siete pruebas específicas aprobadas en Play Mode del Editor: referencias/escala/LOD y detección bloqueada; adquisición, persecución, cuerpo sin penetrar la cobertura y parada; pérdida de visión/cancelación/última posición; alternancia, origen/dirección y daño real al jugador sin autoimpacto; esquiva y salida obstruida; muerte durante preparación sin nuevos disparos ni doble muerte; daño al robot con el proyectil real del jugador. XML final: Tools/IronRequiem/Reports/BigBotCombat/playmode.xml, 7/7. Las pasadas fallidas de diagnóstico no se presentan como resultados finales. No se ejecutaron suites de otros enemigos, protagonista, rendimiento o animaciones de presentación.
- [x] Verificar 57 hashes protegidos contra el respaldo: cero diferencias en escenas originales, modelo/prefab fuente, scripts protegidos, ProjectSettings, manifest y packages-lock. EnemyMobile y esta sección son las modificaciones intencionales de archivos existentes respaldados. Respaldo: Tools/IronRequiem/Backups/20261101-bigbot-combat. Sin paquetes instalados, compras ni créditos.
- [ ] Limitaciones: no existe clip de muerte; la solución actual detiene/congela el cuerpo y utiliza la retirada a los 2,5 s del controlador existente. La fuente no tenía cañones desmontables: no se añadió esa mecánica. Patrulla reutilizada y revisada en código; las pruebas controladas de combate anulan su ruta. Antes de integrarlo en otro nivel, preparar un NavMesh con espacio libre equivalente para el robot grande; la escena principal no se actualizó ni se midió rendimiento.

Herramientas: skills iron-requiem-enemy-ai, consulta de iron-requiem-modular-weapons y guía oficial de AI Navigation; Blender local para inspección de bocas; APIs del Editor y Test Framework instalados. Editor devuelto al laboratorio fuera de Play, sin guardar cambios de ejecución.
### Big Bot — correcciones de combate y presentación (01/11/2026)

- [x] Solicitud exclusiva del Big Bot: aumentar Health.MaxHealth de 240 a 1200 y reutilizar WorldspaceHealthBar y su jerarquía uGUI de la plantilla. Barra compartida con la vida real, visible desde vida completa, orientada a cámara y retirada al morir. Sin un sistema de salud/UI paralelo.
- [x] Sustituir el giro exponencial del Big Bot por giro amortiguado limitado a 60 grados/s mediante su política opcional de EnemyMobile. Activar los clips articulados TurnLeft/TurnRight y ajustar su ciclo según velocidad angular. Torso limitado a 20 grados y brazos con offsets acotados/suavizados; restaurar la pose del Animator antes de cada evaluación para no acumular rotaciones. Recentrar la mirada de las articulaciones cuando el objetivo queda detrás; el cuerpo completa el giro.
- [x] Preparar ambos cañones y lanzar dos proyectiles en el mismo frame del evento de disparo, manteniendo una sola secuencia/ataque de IA. El temporizador de cambio de EnemyController bloqueaba una segunda llamada TryAtack en el mismo frame: después del primer ataque aceptado, el segundo cañón usa su WeaponController existente. Ambos recorridos deben estar libres; se conservan daño, velocidad, impactos, exclusión del propietario y cancelación al perder visión o morir. El daño configurado sigue siendo por proyectil.
- [x] Comprobar primero la colisión estática con entrada W real del Input System: el CharacterController se detenía delante del BoxCollider, sin capas ignoradas ni trigger. Elevar a 3 m la separación mínima de NavMeshAgent para impedir la invasión del jugador durante seguimiento cercano. Confirmar collider sólido habilitado y detección del Rigidbody; no modificar controlador del jugador ni matriz global de físicas.
- [x] Compilación y 10/10 pruebas específicas aprobadas en Play Mode, incluidas vida/barra al 100 % y 75 %, contacto quieto y en movimiento, seguimiento al rodearlo con velocidad angular acotada, articulación real de pies y ausencia de acumulación del brazo, dos proyectiles simultáneos desde bocas distintas, daño y esquiva, cobertura/obstrucción y muerte. XML final: Tools/IronRequiem/Reports/BigBotCombat/playmode.xml (01/11/2026, 11:20–11:21 America/La_Paz). Barra verificada también en captura interna de Game View. La captura de órbita tiene la cámara fuera del robot y no se utiliza como evidencia visual del giro; ese comportamiento se verificó en ejecución por estados, posiciones de pies y rotaciones.
- [x] Conservar el mismo prefab BigBot_Combat_v01 y BigBotCombatLab. Modelos fuente, MainScene y 57 archivos protegidos/configuración conservan sus hashes anteriores. Respaldo previo en Tools/IronRequiem/Backups/20261101-bigbot-corrections. Referencia a UnityEngine.UI añadida solo al ensamblado de pruebas para comprobar el fillAmount real. Sin paquetes, compras, créditos ni tareas ajenas.

Se mantienen los pendientes anteriores de clip específico de muerte y navegación al integrar en otros niveles. Editor fuera de Play, devuelto a BigBotCombatLab. Herramientas: skill enemy-ai y guías oficiales de físicas 3D y uGUI, APIs locales del Editor y Test Framework existentes.
### Big Bot — destrucción progresiva, emergencia y muerte (01/11/2026)

- [x] Alcance exclusivo autorizado: nueva variante Assets/IronRequiem/Prefabs/Enemies/BigBot_Destructible_v01.prefab y escena Assets/IronRequiem/Scenes/BigBotDestructionLab.unity. Conservar BigBot_Combat_v01, BigBotCombatLab, MainScene, FBX original, rig, siete clips anteriores y GUID. El laboratorio tiene una copia propia del NavMesh existente para el mismo robot grande; no se rebakearon escenas de juego.
- [x] Inspeccionar las 45 mallas rígidas, 16 huesos y tres LOD reales del Big Bot, junto al informe geométrico de sus tapas. Reutilizar piezas ya separadas/cerradas; no cortar polígonos, confundirlo con T-07 ni modificar el FBX. Extraer copias estáticas normalizando la escala heredada del FBX y conservando UV, normales y silueta. Verificar por vértices que la pose/escala del cañón desprendido coincide con la unida.
- [x] Usar Health (1200) como fuente. Umbrales Inspector iniciales 75/50/25 %, máscara de transiciones idempotente y materiales URP derivados compartidos para deterioro. Chispas localizadas desde 75 %, humo ligero desde 50 % y más intenso a 25 %. Desprender mochila y cubierta del hombro izquierdo a 50 %, cubierta derecha a 25 %. Mantener todos los huesos, piernas, navegación y las piezas estructurales restantes. Ocultar cada pieza completa en sus tres LOD para impedir reapariciones a distancia.
- [x] Desprender el cañón derecho completo (Forearm_R + Pincer_R) a 25 %, con chispas y caída física. Copiar la pose actual de ambos pivotes, conservar tres LOD estáticos y no llevarse el rig ni componentes de arma/daño. Marcar la pérdida antes del efecto, bloquear ese WeaponController, vaciar su ammo, impedir recarga y cancelar su preparación. Reconocer pérdidas anticipadas notificadas por BigBotCombat.LoseCannon sin otro desprendimiento al cruzar el umbral. La fuente no tenía daño localizado ni recogida de este cañón: no se añadieron hitboxes de unión ni un sistema paralelo de recogida.
- [x] Emergencia basada en el número real de cañones disponibles, multiplicador Inspector inicial 1,5, sin acumulación por daño repetido. Conservar preparación de 0,7 s, pausas, trayectoria recta, distancia, LOS y comprobación de salida/obstáculos. Medición final del Editor en esta prueba: intervalo normal entre descargas 2,931 s y emergencia 1,961 s. Añadir una sobrecarga explícita en EnemyController para seleccionar el WeaponController superviviente sin depender del arma cacheada; el flujo original de disparo de BigBot_Combat_v01 y la plantilla se conserva.
- [x] Crear HeavyCollapse.anim nativa, 1,8 s, sobre el rig real: inestabilidad, flexión de piernas, descenso de pelvis, fallo de energía y desplome hacia delante. Desactivar IA, navegación y ataques inmediatamente; después reproducir la presentación. Sin root motion ni rigidbody compitiendo con Animator. Validar 55 poses con las 45 mallas y usar corrección visual de suelo y apoyos de suela tomados de vértices reales para la transición desde locomoción/ataque. No hornear mallas por frame en producción. Conservar la limpieza de EnemyController a 2,5 s y el procesamiento único de muerte/objetivos/loot. Sin explosión ni daño añadido al morir.
- [x] Reutilizar RobotDestructionPool para restos cosméticos (máximo existente 16) y materiales de chispas/humo existentes. Pool propio del cañón gráfico/físico con máximo 8 objetos y retorno a los 8 s; restos secundarios a los 3 s. Reiniciar velocidades, disponibilidad, temporizador y colisiones ignoradas; ningún slot contiene salud, energía, inventario, Animator o armas. Sin luces nuevas ni materiales instanciados por enemigo. Máximos por robot: 16 chispas eléctricas, 12 partículas de humo y 24 de rotura, además de su preparación existente; efectos transitorios y humo limitados.
- [x] Compilación y 7/7 pruebas específicas finales aprobadas en Play Mode del Editor (16:54:30 UTC, 12:54:30 America/La_Paz): daño gradual y persistencia en cada LOD; daño masivo; letal desde vida completa y muerte única sin explosión dañina; pérdida anticipada con salud completa, ausencia de disparos desde el cañón perdido y cadencia medida; LOS en emergencia y muerte durante preparación; desplome sin movimiento de la raíz de navegación, geometría sobre el suelo y limpieza; seis muertes adicionales simultáneas, límites de restos y reutilización del pool físico. La pérdida anticipada se provocó mediante el contrato real de pérdida, no mediante un nuevo sistema de daño localizado. XML final: Tools/IronRequiem/Reports/BigBotDestruction/playmode.xml. Revisar también las capturas internas de muerte; las pasadas fallidas quedan separadas en el respaldo.
- [x] Verificar 36 archivos protegidos/configuración: cero diferencias contra el respaldo de inicio, incluidos MainScene, prefab/laboratorio de combate previos, modelo, animaciones de presentación y Packages/ProjectSettings. Cambios de código existentes intencionales y respaldados: BigBotCombat y la sobrecarga de EnemyController. Checklist limitada a esta tarea. Respaldo en Tools/IronRequiem/Backups/20261101-bigbot-destruction. No instalar paquetes, comprar herramientas ni consumir créditos.
- [ ] Trabajo posterior: pulido artístico adicional y validación del desplome sobre pendientes/terreno irregular. El laboratorio probado es plano; no se afirma validación general de desniveles. Recogida y daño localizado del nuevo cañón requieren una tarea propia si se solicitan. No integrar automáticamente en MainScene ni afirmar mejoras de FPS: se verificaron límites de objetos, no un benchmark de rendimiento.

Herramientas: skills iron-requiem-enemy-ai e iron-requiem-modular-weapons, inspección geométrica previa y actual mediante APIs del Editor, Particle System/URP y Test Framework existentes. Editor devuelto a BigBotDestructionLab fuera de Play sin guardar cambios de ejecución.
### Big Bot — soportes, visibilidad cercana, giro y chispazos (01/11/2026)

- [x] Corregir el primer desprendimiento: conservar ambos hombros, que sostienen visualmente los cañones. La pieza secundaria que cae al 50 % es la mochila; al 25 % sigue desprendiéndose el conjunto real del cañón derecho. Conservar sus huesos, la unión del cañón superviviente y los tres LOD. Corregir la configuración de la variante mediante APIs del Editor, sin editar el FBX.
- [x] Corregir la causa del culling cercano: los bounds del trabajo anterior estaban expresados en el espacio del renderer y SkinnedMeshRenderer los interpreta respecto a rootBone en este rig. Reconstruirlos en ese espacio, cubriendo locomoción, disparos, giro y muerte con margen para apuntado. Ajustar el volumen del LODGroup; conservar sus tres niveles y updateWhenOffscreen=false, sin forzar LOD0 ni renderizarlo siempre.
- [x] Crear dos clips nativos StepTurn sobre las articulaciones existentes: apoyo con arco de pies, flexión de rodillas, paso alterno y elevación breve. TurnPhase avanza según grados reales de giro (45 grados por ciclo), con histéresis para evitar cambios nerviosos al detenerse. Aplicar únicamente en la variante destructible; conservar el giro y los clips fuente del prefab de combate anterior. Deslizamiento medio observado del hueso del pie izquierdo durante apoyo: 0,00464 m/s en 1,847 s medidos; no equivale a una validación sobre pendientes.
- [x] Hacer visibles los chispazos críticos: situar el emisor frontal fuera de la geometría del torso, reutilizar el material URP existente, usar estelas breves y añadir pulsos de 10 partículas más 5 en la unión rota cada 0,75 s. Máximos de 32 partículas eléctricas frontales y 24 de rotura, sin luces nuevas. Conservar humo, preparación, cadencia y muerte existentes; cancelar la emisión crítica al morir.
- [x] Cuatro pruebas específicas Play Mode aprobadas: soportes y descarga crítica; render cercano; nuevos pasos y apoyo; regresión del desplome/suelo/limpieza. Comprobar realmente 99 renders aislados, con tres estados de vida, distancias de 18/7/3/2,2 m y contacto frontal a 1,45 m, desde ocho ángulos cuando corresponde; ninguna imagen quedó sin robot (mínimo 125 píxeles del modelo a 192×192). Evidencia: presentation-four-tests.xml y visibility-pixels.csv en Tools/IronRequiem/Reports/BigBotDestruction. Repetir únicamente la prueba crítica para capturar el efecto vivo sin proyectiles; pasó 1/1 y se revisó su imagen, junto a la vista cercana y el giro. El XML playmode.xml corresponde a esa repetición final.
- [x] Incorporar las correcciones al flujo de autoría para que una futura regeneración no restaure hombros desmontables ni bounds incorrectos. Checklist limitada a este bloque; sin nuevas mecánicas, paquetes, compras o créditos. Conservar 37 archivos protegidos/modelos/configuración, incluidos MainScene, ambos laboratorios previos, prefab de combate original y HeavyCollapse.anim. Respaldo previo: Tools/IronRequiem/Backups/20261101-bigbot-presentation-fixes.

Se mantiene BigBot_Destructible_v01 en BigBotDestructionLab. La revisión se realizó en el Editor, no en un Player compilado; no se midieron FPS. Continúan los pendientes anteriores de validación en terreno irregular y las mecánicas de recogida/daño localizado ausentes de la fuente. Herramientas: skills enemy-ai/modular-weapons, APIs del Editor, Animator, Particle System y Test Framework instalados.
### Correcciones de combate y presentación — variantes CombatPolish (02/11/2026)

- [x] Big Bot: reproducir el conflicto de rotación de NavMeshAgent con EnemyMobile; desactivar updateRotation en ejecución, conservar root motion desactivado y añadir apoyos articulados durante giros/reversiones. En la prueba controlada, salto máximo del pie por frame de 0,506 m a 0,0565 m y deslizamiento medio de apoyo de 0,00734 m/s. No son medidas de FPS ni validación sobre pendientes.
- [x] Melee: corregir el rechazo de contactos interiores contra el collider grande al usar su centro; sincronizar físicas en el impacto, mantener distancia, daño y deduplicación existentes. Probar ocho ángulos, contacto interior, impacto único, espera de impacto, pared y fuera de alcance.
- [x] Brazo FPS: copia LeftPunch_FPSFramed del clip articulado, con trayectoria y encuadre corregidos. Mantener dedos, proporciones y alcance físico. Prueba de toda la animación: 32 frames, 225 vértices del corte y cero muestras visibles dentro de WeaponCamera; mano oculta después.
- [x] Pistola: variante Weapon_IR_Pistol_CombatPolish con carcasa de la plantilla y unión robótica al antebrazo. Conservar referencias de proyectil, boca, cadencia, daño, retroceso y controles. Probar reposo, movimiento, ADS y disparo; verificar montaje con el inventario real.
- [x] Visibilidad: variantes BigBot/T07/HoverBot/Turret con materiales emisivos compartidos, ojos existentes o indicadores sobre superficies reales y unidos a sus articulaciones. Mantener LOD; sin luces nuevas ni cambio de iluminación global. Prueba aislada sin luces, UI ni partículas: los cuatro modelos muestran emisión visible.
- [x] Muerte: variante RobotBodyBurst_CombatPolish, visual y sin DamageArea/CannonExplosion/luces; usar RobotDestructionPool existente, tamaños por enemigo y limpieza con escala restaurada. Big Bot prepara a 0,97 s y detona a 1,25 s, junto al desplome. T07 conserva su único propietario de muerte; HoverBot/torreta reutilizan el contrato IEnemyDeathPresentation. Probar cuatro muertes simultáneas, daño letal repetido, ausencia de disparos posteriores, cañón previamente desprendido, limpieza y reutilización del pool.
- [x] Diez pruebas Play Mode específicas aprobadas en ejecuciones por bloque, incluida revisión visual de muerte y regresión aislada de movimiento/cámara, recogida con E, carga alineada, energía, cambio de armas, lanzamiento, nueva recogida y agotamiento. La primera pasada de inventario se encontró con el HitBox de HoverBot delante del módulo: contacto a 0,524 m y cañón a 0,904 m, entrada activa y 70 de energía. La prueba adicional confirma el bloqueo correcto por línea de visión y completa el ciclo con combate activo tras despejarla; también pasó la versión aislada. No se modificó recogida para atravesar enemigos.
- [x] Compilación sin errores y siete variantes sin scripts ni referencias serializadas ausentes. Conservar los 35 archivos protegidos del respaldo sin diferencias, incluida MainScene, prefabs fuente, Input System, Packages y ProjectSettings. Escena separada: Assets/IronRequiem/Scenes/CombatPolishLab.unity. Originales conservados; ninguna integración en MainScene, instalación o consumo de créditos. Herramientas: skills enemy-ai/fps/modular-weapons y VFX instalada, APIs del Editor, Animator, Particle System/URP y Test Framework.
- [ ] Validación adicional no realizada: apoyos de giro sobre terreno irregular. Las pruebas de esta tarea cubren el suelo plano del laboratorio y se ejecutaron en el Editor; no se generó Player ni se midió rendimiento.
### Arma principal — cadencia y acople PrimaryModule v02 (02/11/2026)

- [x] Revisar respaldos de la pistola: valores históricos 0,5 y 0,4 s; no se encontró una cadencia anterior más rápida que la actual de 0,4 s. Configurar la alternativa autorizada de 0,2 s (5 disparos/s nominales), con daño 11 frente a 22: conservar 55 DPS nominales. Mantener disparo semiautomático y los controles actuales.
- [x] Ajustar únicamente la reserva/recuperación de esta variante: 16 a 32 unidades, regeneración 16 a 32 unidades/s y demora de 1 a 0,6 s. Conservar 352 de daño por reserva completa y evitar que la reducción de daño duplique el combate prolongado por la recarga. Proyectil en variante del existente: cambiar solo Damage, conservando velocidad, radio, impactos, corrección de trayectoria y código.
- [x] Comparar impactos reales y TTK antes/después a 5 m, blancos inmóviles y pasos simulados de 1/60 s, incluyendo recarga y pulsaciones semiautomáticas. T07 de 160 HP: 8 disparos/3,05 s frente a 15/3,2833 s. Big Bot de 1200 HP: 55 disparos/48,6167 s frente a 110/50,65 s. Tiempos del primer disparo a la muerte; no son una medición de DPS de combate contra objetivos móviles ni de rendimiento.
- [x] Revisar en Blender una copia del módulo local, usando la referencia conceptual recta existente y la descripción del usuario: sin empuñadura, transición desde la muñeca medida de 7,4 cm a una unión de 7,6 cm, solape interno aproximado de 4 mm, longitud de 33,3 cm entre sockets y anchura máxima de 10,87 cm. Modelo cerrado con normales verificadas, 696 triángulos, cero bordes abiertos/no manifold y un material URP compartido que reutiliza el atlas. Conservar fuentes y pivotes; no modificar rig, huesos, manos ni modelos originales.
- [x] Preparar Weapon_IR_PrimaryModule_v02, Projectile_IR_PrimaryModule_v02 y Player_PrimaryModule_v02, con inventario y presentación existentes. Ocultar solo la mano sustituida; conservarla para otros estados. Mantener el antebrazo de 0,27712 m, unión y boca alineadas durante reposo, movimiento, ADS, disparo y retroceso; ajustar 3 cm el encuadre equipado. La revisión final muestreó 208 vértices del corte durante 185 frames: cero muestras visibles. Confirmar que mantener el botón dispara una sola vez y que retirar/reponer el módulo restaura/oculta la mano mediante PlayerWeaponsManager.
- [x] Aprobar las tres pruebas Play Mode específicas (TTK común, TTK Big Bot y montaje), con pasadas antes/después y repetición del montaje tras corregir normales. Revisar capturas reales de reposo, ADS y detalle; sin errores de compilación ni referencias ausentes en las tres variantes. Escena separada Assets/IronRequiem/Scenes/PrimaryModuleLab.unity; respaldos en Tools/IronRequiem/Backups/20261102-primary-module. Fuera de la checklist, 55 archivos protegidos sin diferencias, incluidos MainScene, controles, código FPS, armas robadas, configuración y modelos fuente. Sin paquetes, servicios de generación ni créditos.
- [ ] Validación adicional pendiente: balance con puntería humana y enemigos móviles, fuera de la prueba controlada de TTK. Esta entrega conserva MainScene; las variantes están preparadas y verificadas en el laboratorio. No se ejecutaron tareas de enemigos, melee, slide, rendimiento o efectos.
### Arma principal — cápsulas y fuego automático de plantilla v03 (02/11/2026)

- [x] Reutilizar las cuatro mallas Primary_Weapon_Container de Weapon_Blaster, con sus materiales compartidos, al 48 % de su tamaño equipado original y montadas sobre el módulo v02. Conservar la unión estrecha, rig, mano oculta y boca; no reactivar VFX_GunCylinder ni añadir partículas, luces o colliders a las cápsulas.
- [x] Copiar la cadencia real de la plantilla: 0,1 s / 10 disparos por segundo nominales. Activar disparo automático al mantener el botón, según confirmación explícita del usuario. Ajustar únicamente la nueva variante a daño 5,5, reserva 64, regeneración 64/s y demora 0,28 s, conservando 55 DPS nominales y 352 de daño por reserva completa. Conservar los proyectiles existentes mediante variante de Damage, sin cambiar su código o física.
- [x] Tres pruebas Play Mode específicas aprobadas: montaje/movimiento/ADS/retroceso/fuego sostenido/restauración de mano y los dos TTK de blancos inmóviles a 5 m con pasos simulados de 1/60 s. T07 160 HP: 30 impactos, 3,2167 s; Big Bot 1200 HP: 219 impactos, 51,75 s, incluyendo recuperación. Revisar capturas de reposo, ADS y unión. Ningún corte proximal visible en 185 frames, 208 vértices muestreados. Las cápsulas no tienen emisores sueltos ni luces.
- [x] Preparar Weapon_IR_PrimaryCapsules_v03, Projectile_IR_PrimaryCapsules_v03 y Player_PrimaryCapsules_v03 en Assets/IronRequiem/Prefabs; escena separada Assets/IronRequiem/Scenes/PrimaryCapsulesLab.unity. Compilación limpia y variantes sin scripts ausentes. Conservar v02, MainScene y armas robadas; actualizar únicamente la prueba de esta arma y la checklist. Respaldo Tools/IronRequiem/Backups/20261102-primary-capsules. Ninguna instalación o consumo de créditos.

Se mantiene pendiente la valoración de balance con puntería humana y enemigos móviles indicada en la tarea anterior; las pruebas de TTK son controladas en el Editor.
### Respuesta al daño y arma integrada v04 — bloques 3 y 4 (02/11/2026)

- [x] Inspeccionar la plantilla real: EnemyController usa un gradiente de emisión blanco y MaterialPropertyBlock; EnemyMobile/DetectionModule lanzan OnDamaged y chispas locales. Adaptar esos recursos a BigBot_HitFeedback_v04 y T07_HitFeedback_v04: destello de 0,09 s, pausa mínima de 0,14 s, reacción aditiva leve de torso de 0,18 s con intervalo de 0,3 s, sin trasladar el root ni tomar autoridad del NavMeshAgent. Ejecutar la pose antes del apuntado y de los sockets para mantener la unión del cañón.
- [x] Mantener Health/Damageable como fuente de daño. Añadir notificación del punto real para bala/melee; el proyectil procesa su impacto una sola vez y omite su VFX genérico cuando el robot ya lo presenta. Conservar la deduplicación por objetivo del puño. Reutilizar un Particle System de la plantilla por robot, sin instanciar efectos por golpe, con simulación mundial, 32/48 partículas máximas, vida breve y sin luces nuevas. No añadir daño ni estados de IA.
- [x] Copiar los materiales necesarios del enemigo y usar bloques por renderer/material para el destello, restaurándolos al terminar, desactivar o morir. Mantener las apariencias de deterioro y no alterar el material compartido de otros enemigos. Evitar el registro de ranuras nulas en EnemyController. En estas variantes, la presentación breve sustituye solo los disparadores de daño de EnemyMobile/DetectionModule, conservando detección, apuntado y ataques.
- [x] Rediseñar el arma sobre copias de las mallas Primary_Weapon y Front_Piece de Weapon_Blaster: eliminar empuñadura, adaptar el extremo posterior y conservar sockets/rig/retroceso/ADS. Obtener cerámica, metal, naranja y cian de regiones verificadas de IR_Protagonist_BaseColor; usar copias del material y los mapas de superficie reales del jugador. Adaptar el mapa de normales a la nueva geometría desactivando su intensidad en esta copia. Conservar las cuatro cápsulas, con copias de materiales y tono cian del protagonista. Conservar automático 0,1 s, daño 5,5, DPS nominal 55 y los parámetros de recuperación v03.
- [x] Cinco pruebas Play Mode específicas aprobadas en las pasadas correspondientes: bala y melee contra colliders superpuestos, deterioro/restauración/aislamiento con impactos repetidos, ataques continuos y limpieza al morir, presentación FPS en reposo/movimiento/ADS/disparo/retroceso y cambio real al cañón robado y vuelta. Repetir la reacción después del ajuste de orden y el montaje después del ajuste visual: aprobados. Capturas reales revisadas. Muestreo del brazo: 187 frames, 208 vértices del corte, cero muestras visibles; longitud de antebrazo 0,27712 m.
- [x] Escena separada Assets/IronRequiem/Scenes/HitWeaponLab.unity; prefabs BigBot_HitFeedback_v04, T07_HitFeedback_v04, Player_Integrated_v04 y Weapon_IR_Integrated_v04. Compilación sin errores y escena sin scripts ausentes. Conservar MainScene, modelos/materiales originales, versiones anteriores, paquetes y configuración. Cambios acotados y respaldados en Damageable, ProjectileStandard, ProtagonistMelee, EnemyController, EnemyMobile y DetectionModule; fuentes previas en Tools/IronRequiem/Backups/20261102-hit-weapon. No instalar paquetes ni consumir créditos.

Limitación: pruebas realizadas en el Editor y en el laboratorio; no se generó un Player compilado ni se integraron estas variantes en MainScene.
### Restaurar animaciones y aumentar ligeramente el daño (02/11/2026)

- [x] Corregir la pérdida de movimiento del arma: WeaponFuelCellHandler seguía referenciando las cápsulas antiguas ocultas. Mantener ese componente original y enlazar cuatro copias visibles mediante sockets con la escala/orientación correctas, conservando los parámetros de desplazamiento de la plantilla. No reparentar ni sobrescribir piezas heredadas; conservar las anteriores ocultas. Verificar movimiento por consumo y vuelta a reposo tras recargar.
- [x] Subir daño principal de 5,5 a 6,5 (+18,18 %), mediante Projectile_IR_Integrated_v05, variante del proyectil existente. Conservar automático de 0,1 s, munición, recuperación, retroceso, controles y diseño. DPS nominal: 55 a 65. Conservar la definición anterior del proyectil y las armas robadas.
- [x] Hacer visible la reacción en cada impacto de T07 y Big Bot. Medir la evolución de pose del clip Hurt real de HoverBot (0,4667 s) y adaptar su envolvente a sus torsos nativos en 0,24 s. Superponer contribuciones acotadas por impacto en lugar del bloqueo anterior de 0,3 s, sin reiniciar Animator, desplazar el root ni cancelar ataques. Mantener limpieza al morir/desactivar y las chispas/destello existentes.
- [x] Tres pruebas Play Mode específicas aprobadas: daño real 6,5 y animación/restauración de cápsulas; 50 impactos/50 contribuciones en Big Bot con dos ataques completos durante la prueba y sin desplazamiento artificial; ocho impactos/ocho reacciones en T07 y limpieza al morir. Pico de reacción observado de 9,02 grados durante el fuego repetido. Compilación sin errores. Conservar HitWeaponLab y las variantes v04 como punto de prueba, sin tocar MainScene ni los modelos/materiales.

Respaldo: Tools/IronRequiem/Backups/20261102-animation-restore. Cambios limitados al enlace de animación, daño solicitado y reacción de los robots; sin paquetes, créditos ni otras tareas. Pruebas en el Editor, sin generar Player.
### Arma principal — presentación nativa v06 (02/11/2026)

- [x] Crear Weapon_IR_NativeVisual_v06 como variante directa de Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab. La fuente no tiene Animator ni clips: WeaponFuelCellHandler mueve las cápsulas; PlayerWeaponsManager ejecuta bob, ADS, retroceso y equipamiento. Conservar estos scripts, jerarquía y pivotes originales. Recuperar RecoilForce visual original de 1; mantener la cámara y los parámetros actuales del manager.
- [x] Ajustar copias de geometría a los nodos nativos y reutilizar el diseño del módulo y los mapas reales del protagonista mediante materiales propios. Mantener mano derecha oculta, acople y boca actuales. Añadir soportes telescópicos con dos grupos de pesos sobre los transforms existentes, sin otra autoridad de animación. Corregir el soporte rígido que sobresalía y adaptar la emisión al cian/naranja del protagonista.
- [x] Conservar los campos públicos actuales de WeaponController salvo referencias internas de presentación y retroceso nativo solicitado. Mismo proyectil Projectile_IR_Integrated_v05, daño 6,5, automático cada 0,1 s, munición y recuperación. Sin cambios en código runtime, controles, cámara, enemigos, melee o armas robadas.
- [x] Verificar dos pruebas específicas en Play Mode del Editor: 44 poses de cápsulas en 11 fracciones de munición comparadas con el prefab original; reposo, caminar, ADS, fuego automático, retroceso, movimiento disparando, retirar y volver a equipar. Comprobar alineación en 243 frames y cero vértices visibles del corte proximal del antebrazo. Revisar capturas reales de reposo, ADS/disparo, detalle del acople y cápsulas extendidas. Compilación sin errores; laboratorio sin scripts ausentes. No se ejecutaron pruebas de enemigos, melee o rendimiento ni un build de Player.
- [x] Preparar Player_NativeVisual_v06 y Assets/IronRequiem/Scenes/NativeWeaponVisualLab.unity; dejar este laboratorio abierto, limpio y fuera de Play. MainScene y los archivos protegidos sin diferencias antes de actualizar esta checklist. Respaldo Tools/IronRequiem/Backups/20261102-native-weapon-visual. Sin instalaciones ni créditos.

### Reparar diseño nativo y quitar desenfoque (02/11/2026)

- [x] Restaurar las siete mallas originales de Weapon_Blaster en la variante v06, con UV/normales nativas y cápsulas sobre sus nodos originales. Desactivar la carcasa deformada y soportes anteriores conservándolos. Ajustar la unión posterior y mantener alineada la boca. Usar materiales propios y atlas nativo recoloreado con la paleta del protagonista; conservar mapas y máscaras emisivas, con cian/naranja según calentamiento. Conservar movimientos por código, parámetros de disparo, proyectil y activación previa de efectos.
- [x] Desactivar Motion Blur en los tres perfiles que lo contienen, y DOF donde existe. Eliminar FXAA/TAA y resolución dinámica de ambas cámaras de Player_NativeVisual_v06; verificar render scale 1 y conservar bloom/iluminación/MSAA del pipeline.
- [x] Verificar 2/2 pruebas Play Mode con entrada virtual: mallas originales, configuración de desenfoque, 44 poses nativas de cápsulas, caminar, ADS, disparo, retroceso, retirada y equipamiento. Prueba final: 240 frames alineados, 18 disparos y cero vértices visibles del corte proximal del brazo. Revisar capturas reales. Auditoría final sin errores de compilación capturados ni scripts ausentes; NativeWeaponVisualLab limpio y fuera de Play.
- [x] Comparar 1.366 archivos protegidos: solo los dos prefabs v06 y los tres perfiles previstos cambiaron. Escenas completas, plantilla Assets/FPS, Packages y ProjectSettings intactos. Respaldo 20261102-native-weapon-clarity; informe NATIVE_WEAPON_REPAIR.md y evidencias Reports/NativeWeaponRepair. Sin integración nueva en MainScene, paquetes, créditos ni pruebas de rendimiento/enemigos/melee.

### Recogida, antebrazo modular y calentamiento v07 (02/11/2026)

- [x] Corregir el cañón caído del Big Bot: era un resto sin componentes de recogida. Crear una variante recogible con las mallas y tres LOD reales, más representación equipada del mismo modelo. Reutilizar StolenCannon, energía, carga, OVERDRIVE, humo, explosión y PlayerWeaponsManager. Preservar el pool decorativo como alternativa. No reciclar módulos equipados; limitar ocho instancias y 120 segundos en el suelo.
- [x] Crear Weapon_IR_ArmModule_v07 al 62 % del tamaño anterior y sin empuñadura, conservando los movimientos por código, daño 6,5, cadencia 0,1 s, munición y proyectil. Mantener el material y las UV de la carcasa en copias.
- [x] Crear Player_ModularArm_v07 con receptor próximo al codo, conservando 14 cm del antebrazo. Sustituir la parte distal y mano al equipar; restituir originales al retirar el arma. Mantener el puñetazo izquierdo. Preparar socket del cuerpo para futuras animaciones y usar la misma interfaz trasera en principal, T-07 y Big Bot. Cubrir la unión y mantener el corte proximal fuera de cámara.
- [x] Restaurar calor progresivo cian/naranja/rojo en ambas ranuras de las cápsulas, con MaterialPropertyBlock, vapor máximo de 20 partículas y enfriamiento nativo. No modificar balance ni añadir daño por calor.
- [x] Cinco pruebas Play Mode aprobadas: movimiento/ADS/disparo y restauración del brazo; calentamiento/enfriamiento; recogida/disparo/lanzamiento/explosión del Big Bot; recogida del T-07 con paredes/rango/cambio de arma; liberación por muerte del jugador. 169 frames alineados, 14 disparos, cero vértices visibles del corte proximal. Entrada virtual sin activar ventanas; E recoge, F golpea, G lanza.
- [x] Preparar ModularArmLab separado. Comparar 2.114 archivos: cinco cambios previstos de integración; escenas anteriores, v06, plantilla, modelos y configuración intactos. Auditoría sin errores de compilación capturados ni scripts ausentes. Respaldo 20261102-modular-arm-pickup; informe MODULAR_ARM_AND_PICKUP.md. Sin paquetes, créditos o medición de FPS.

### Big Bot y bazooka robada v08 (02/11/2026)

- [x] Crear BigBot_Explosive_v08, Weapon_BigBot_Bazooka_v08 y BigBot_BazookaPickup_v08 como variantes. Preparar BigBotBazookaLab a partir del laboratorio guardado por el usuario; sustituir únicamente Big Bot y su referencia al módulo recogible. Conservar MainScene, escenas anteriores, T-07, arma principal, fuentes, GUID, URP, controles y paquetes.
- [x] Aumentar 65 % la representación equipada alrededor de su interfaz Attachment, manteniendo la interfaz trasera y recolocando WeaponMuzzle en la boca real del modelo. Conservar escala física del módulo y enemigo, materiales, recogida con E, carga, energía por instancia, lanzamiento con G y OVERDRIVE crítico automático. La acción X existe en la entrada, pero el portador actual no la procesa; esta tarea no añadió otra activación ni cambió controles.
- [x] Reutilizar ProjectileStandard y DamageArea para ambos proyectiles explosivos. Añadir cobertura, exclusión del propietario y afiliación como opciones desactivadas por defecto en DamageArea; activarlas solo en las dos variantes nuevas de proyectil. Pasar la normal del impacto para iniciar la comprobación fuera de la pared. Mantener una única ruta de daño de área y deduplicación por Health. BigBotProjectileSettings conserva el área únicamente en la variante marcada como explosiva; los prefabs anteriores conservan su funcionamiento.
- [x] Valores Big Bot antes/después: daño por proyectil 12/10; radio 0/1,8 m; velocidad 18/22 m/s; recorrido máximo 35/48 m; detección 26/36 m; distancia de ataque 12/22 m; descanso normal 2,2/1,8 s, con preparación de 0,7 s conservada. Periodo normal anterior teórico 2,9 s y nuevo medido 2,516666 s; siguen disparando ambos cañones. Presión máxima nominal aproximada: 8,28 a 8 HP/s antes de esquiva y caída de daño por distancia.
- [x] Revisar emergencia existente de 1,5x y conservarla como alternativa para variantes anteriores. En v08, pérdida real de cualquier cañón habilita ráfagas de tres tiros, intervalo Inspector de 0,26 s y pausa de 2,1 s tras el último tiro. Nueva advertencia de 0,7 s antes de la siguiente ráfaga. Reutilizar Fire para el retroceso entre tiros. Sin acumular multiplicadores, disparar desde un cañón perdido ni conservar tiros pendientes al morir o perder visión. Presión nominal teórica de emergencia: aproximadamente 6,21 a 9,04 HP/s; intervalo observado entre tiros 0,266667 s.
- [x] Valores robada: daño de carga 25–65 conservado, radio de explosión nuevo de 2,6 m, velocidad anterior/final de 300 m/s, intervalo mínimo de 0,5 s y carga máxima de 1,2 s conservados. Mantener costes de energía. Crear dos variantes de CannonExplosionPresentation con partículas y sonido existentes, duración 1,8 s, sin luces ni impulso de cámara adicionales. Limitar cada sistema a 20 partículas y cada burst a ocho; sin daño en el prefab visual ni otro sistema de pooling.
- [x] Aprobar 4/4 pruebas Play Mode específicas: ataque lejano con EnemyMobile y dos bocas; bloqueo de disparos por cobertura; impactos reales contra pared/suelo/jugador, daño de área y colliders duplicados; pérdida durante preparación, ráfaga, pausa, pérdida de visión y muerte durante ráfaga; recogida/carga/energía/lanzamiento/nueva recogida/OVERDRIVE y detonación única. Repetición ampliada del ciclo robado aprobada 1/1: proyectil cargado real produce 49,72221 de daño de área al blanco directo y 41,90799 al cercano, con un evento por blanco y sin sumar daño directo. Cobertura recibió cero daño; impacto enemigo directo al jugador produjo 10, una sola vez.
- [x] Verificar 148 frames de acople, boca y carga en reposo y carga normal/ADS; cero vértices de la bazooka cruzando el near plane en esas muestras, distancia mínima observada 0,477898 m. Revisar por separado capturas reales de disparo/recuperación, impacto y daño a dos blancos. Auditoría final: compilación sin errores capturados, cero scripts ausentes, laboratorio limpio y fuera de Play. Entrada virtual sin activar ventanas ni controlar escritorio. Pruebas del Editor; no build de Player ni medición de FPS.

Respaldo: Tools/IronRequiem/Backups/20261102-bigbot-bazooka. Evidencias breves y XML: Tools/IronRequiem/Reports/BigBotBazooka. Cuatro archivos runtime existentes modificados para las opciones anteriores; los assets previos permanecen intactos. Sin instalaciones ni créditos.

### Carga visual del cañón robado del Big Bot — referencia v09 (02/11/2026)

- [x] Inspeccionar antes de modificar assets el video 20261102-2202-46.5756978.mp4: 364×236, 30 FPS, 219 frames y 7,3 s. Una sola carga: inicio con motas azules y destello, filamentos curvos azul/blanco concentrados en la boca, anillo azul transitorio, arcos magenta segmentados con marcas orbitales delante, mantenimiento y salida alrededor de 5,27 s. Extraer muestras generales y detalladas con Blender en background; conservar el archivo fuente.
- [x] Crear BigBot_ReferenceCharge_v09 como variante del efecto y Weapon_BigBot_ReferenceCharge_v09 como variante de la bazooka v08. Reutilizar CannonChargePresentation y CannonChargeShot mediante presentación opcional, desactivada por defecto para el T-07 y prefabs anteriores. Preparar filamentos curvos, núcleo blanco/azul, dos anillos abiertos magenta, marcas y motas locales con materiales URP propios. Máximo 24 partículas, pico final observado de 14; sin luces nuevas ni materiales creados por instancia. Conservar el efecto anterior oculto y sus fuentes. Validar coordenadas finitas de las mallas generadas.
- [x] Anclar el origen bajo WeaponMuzzle con posición/rotación locales cero y escala uno. Mantener las capas visibles delante del borde del cañón. Comprimir las fases a la carga real de 1,2 s; conservar el destello de salida de 0,085 s y limpiar después. El anillo inicial se oculta durante la carga sostenida. Conservar el proyectil magenta, daño, energía, cadencia, sonidos, OVERDRIVE y controles existentes. La preparación y conducta de IA permanecen intactas; la variante de Big Bot solo referencia la nueva arma mediante su módulo recogible.
- [x] Preparar BigBotChargeReferenceLab, con variantes nuevas del enemigo y módulo, sin guardar MainScene ni escenas anteriores. Dos pruebas Play Mode finales aprobadas: carga parcial, completa y dos disparos consecutivos con cuatro disparos aceptados; cancelación, movimiento, mouselook/ADS, cambio de arma, lanzamiento y muerte del portador durante carga. Verificar 138 frames de alineación, simulación local y limpieza; las cancelaciones no disparan ni consumen energía. Comparar campos de WeaponController y referencia exacta al proyectil con v08. Revisar capturas de fases equivalentes y vista FPS. Auditoría final sin errores de compilación capturados ni scripts ausentes; laboratorio limpio y fuera de Play. Entrada virtual y render a textura, sin activar ventanas ni controlar escritorio.
- [x] Conservar originales, materiales previos, MainScene, configuración y paquetes mediante comparación de hashes. Único runtime existente modificado: CannonChargePresentation, con rama visual opcional. Checklist limitada a esta tarea. Respaldo Tools/IronRequiem/Backups/20261102-bigbot-charge-reference; evidencia breve y capturas Tools/IronRequiem/Reports/BigBotChargeReference. Sin instalaciones ni créditos.

Diferencias respecto al video: filamentos y destellos procedurales más definidos, sin reproducir exactamente su ruido/brillo fino; cámara, modelo e iluminación distintos. La fuente es de baja resolución. Se conserva la carga jugable de 1,2 s y se abrevia la disipación para limpiar al disparar; el haz azul del video no sustituye al proyectil existente. Pruebas en el Editor, sin build de Player ni medición de FPS.

### Big Bot y arma robada en ráfagas v10 (02/11/2026)

- [x] Crear BigBot_Burst_v10, Weapon_BigBot_Burst_v10, BigBot_BurstPickup_v10, Projectile_BigBot_Burst_v10 y BigBotBurstLab separados. Reutilizar EnemyController/EnemyMobile/BigBotCombat, WeaponController, proyectiles explosivos, DamageArea y recursos por instancia. Conservar MainScene, escenas previas, modelos, arma principal, paquetes y assets del T-07.
- [x] Big Bot: tres proyectiles totales por ráfaga, alternando ambos cañones 0/1/0 y 1/0/1. Intervalo configurable 0,18 s y recuperación 1,5 s. Emergencia: solo cañón superviviente, intervalo 0,12 s y recuperación 0,9 s. Conservar daño de 10 por proyectil, radio 1,8 m, velocidad 22 m/s, recorrido 48 m, detección 36 m y ataque 22 m. Conservar anticipación de 0,7 s como aviso sin energía; se añade después de la recuperación. Apuntar al punto anunciado durante toda la ráfaga, manteniendo visibilidad y salida libre antes de cada proyectil. Total máximo de 30 de daño por ráfaga, no seis proyectiles/60 de daño. Presión máxima teórica con anticipación: 11,72 HP/s normal y 16,30 HP/s emergencia; requiere ajuste de dificultad en partidas.
- [x] Reutilizar preparación, Fire y recuperación del Animator. Interrumpir recuperación visual cuando vence el temporizador configurado para evitar una pausa de emergencia alargada por el clip. Pérdida durante ráfaga conserva únicamente su presupuesto pendiente y usa el cañón restante; no reinicia otros tres disparos ni acumula multiplicadores. Pérdida durante preparación conserva la cancelación existente. Muerte, objetivo muerto o pared cancelan pendientes.
- [x] Arma robada: variante directa de Weapon_Blaster con geometría, materiales, acople y parámetros de presentación de la bazooka v08. Sin CannonChargeShot, CannonChargePresentation, jerarquía de carga ni adaptador ChargedProjectileDamage. CannonBurstShot compone entradas sobre WeaponController; no dispara mediante corrutinas o temporizadores fuera de la puerta de entrada del inventario. M1 inicia tres tiros; soltar completa los pendientes, mantener repite después de pausa. Intervalo 0,18 s, pausa 1,5 s, daño fijo 25, radio 2,6 m, velocidad 300 m/s, energía 12 por proyectil realmente aceptado. Ráfaga completa: 75 de daño y 36 de energía. Conservar capacidad de 100, recogida E, lanzamiento G y OVERDRIVE crítico automático. Su regla crítica existente puede cortar una ráfaga. CannonAmmo enlaza el adaptador correcto y el HUD muestra RÁFAGA solo para esta variante; T-07 continúa con su carga de 1,2 s y sus costes anteriores.
- [x] Reutilizar fogonazo azul de plantilla en una variante local de corta duración, máximo 12 partículas, sin luces. Cada tiro aceptado usa daño/impacto/sonido y retroceso del flujo nativo. Limpiar fogonazos al cancelar; retirar solos al terminar. Conservar las explosiones existentes y exclusión/cobertura del área; no sumar daño directo.
- [x] Aprobar seis pruebas Play Mode finales: dos ráfagas normales con alternancia continua; pared y pérdida del objetivo entre tiros; desprendimiento durante ráfaga, emergencia y muerte del Big Bot; pulsación breve y energía insuficiente; mantener/soltar durante segunda ráfaga; cambio de arma, lanzamiento, nueva recogida y muerte del jugador entre tiros. Valores observados a paso de 60 Hz: intervalos normal/robada 0,18333 s, emergencia 0,13333 s; separación IA entre último tiro y siguiente ráfaga 2,21667/1,61667 s incluyendo aviso; pausa robada 1,5 s. Cien de energía producen tres tiros y quedan 64; dos ráfagas producen seis tiros y quedan 28. Con 35 restantes se aceptan solo dos, gastando 24, y OVERDRIVE cancela el tercero. Interrupciones tras primer tiro: sin tiros tardíos ni cobros extra. Verificar ausencia de componentes/referencias activas de carga y conservación de configuración T-07; no se ejecutó una partida nueva del T-07 ni de otras armas.
- [x] Auditoría final: sin errores de compilación capturados ni scripts ausentes; BigBotBurstLab limpio y fuera de Play. Input virtual sin activar ventanas o controlar escritorio. Comparación de hashes: cambios existentes limitados a BigBotCombat, CannonAmmo y MechanicHud, además de esta checklist; nuevos assets/scripts de esta tarea. Respaldo Tools/IronRequiem/Backups/20261102-bigbot-burst. Evidencias breves/XML en Tools/IronRequiem/Reports/BigBotBurst. Sin instalaciones, créditos, build de Player ni pruebas de rendimiento.

### Laboratorio de combate de dos cuartos (03/11/2026)

- [x] Crear Assets/IronRequiem/Scenes/CombatTwoRoomsLab.unity con geometría básica URP: dos arenas amplias, corredor central de 12 m, columnas, coberturas bajas/altas, rutas laterales y luz clara. Conservar jugador e inventario actuales. Reutilizar T07_HitFeedback_v04, BigBot_Burst_v10, HoverBot_CombatPolish y Turret_CombatPolish; comprobar Health, EnemyController, referencias y agentes en ejecución. No se encontró un coordinador de oleadas existente; añadir únicamente CombatTwoRoomsLab con cantidades, prefabs, puntos, distancias de seguridad y volúmenes de entrada configurables.
- [x] Primera arena: entrada inicia 6 T-07; después 3 T-07 y 1 Big Bot; finalmente 3 drones y 2 Big Bots. Contar Health raíz de los enemigos registrados mediante OnDie, sin incluir armas, restos o cadáveres. Esperar al último superviviente antes de continuar. Al completar, desactivar puerta y obstáculo de navegación permanentemente. Entrada al segundo cuarto inicia una sola ronda de 4 Big Bots y torreta central; terminar con PRUEBA COMPLETADA. Desactivar objetivos/flujo automático de plantilla únicamente en esta escena para impedir una victoria prematura entre oleadas.
- [x] Hornear NavMesh propio usando AI Navigation instalado y radio conservador de 2,25 m. Seleccionar puntos libres configurados lejos del jugador y sin repetir posición en una oleada. Mantener escala, armas, IA, estadísticas, destrucción y recogida de cada prefab. Spawn al final del frame para respetar inicialización de Health antes del siguiente Update de combate. Cuatro pickups HealthPickup existentes de 25 HP, dos por cuarto, en posiciones fijas editables; sin reaparición. Impedir solo los drops de vida que pudiera contener el prefab instanciado, sin sustituir los módulos desprendibles.
- [x] Dos pruebas Play Mode finales aprobadas: cantidades/tipos exactos de las cuatro rondas; superviviente pendiente; transiciones por muertes reales de Health, con cadáveres/módulos aún presentes; apertura permanente, corredor navegable, entrada única a ronda final y finalización. Segunda prueba: trigger real del CharacterController sobre pickup, cura 10 de los 25 configurados al faltar solo 10, tope máximo y destrucción única; rutas laterales completas en ambos cuartos. Compilación sin errores capturados, cero scripts ausentes; escena limpia y fuera de Play. No se controló escritorio ni se activaron ventanas.
- [x] MainScene, manifest y packages-lock sin diferencias frente al respaldo. Prefabs originales usados mediante instancias; ningún script previo modificado. Checklist limitada a esta tarea. Sin paquetes, créditos ni mejoras adicionales. Evidencias breves/XML en Tools/IronRequiem/Reports/TwoRooms; respaldo en Tools/IronRequiem/Backups/20261103-two-rooms.

Pendiente de revisión manual: recorrido con dash/slide, lectura visual del combate y dificultad de las cantidades solicitadas. Las pruebas automáticas validan lógica y navegación, no una partida manual completa ni rendimiento medido.

### Persecución y menú temporal del laboratorio (03/11/2026)

- [x] En CombatTwoRoomsLab, asignar a los enemigos móviles una ruta PatrolPath existente cuyo nodo es el jugador; avanzar hacia él antes de detectarlo y conservar Follow/Attack, obstáculos y línea de visión nativos. Torreta permanece fija. Sin cambios globales de IA o estadísticas.
- [x] Añadir menú uGUI temporal con Jugar y panel de muerte con Reiniciar · cuarto 1 / Volver al menú. Pausar tiempo/audio y liberar cursor. Recargar la escena completa para restituir vida, cuatro curaciones, oleadas y puerta. Desactivar el antiguo gestor de pausa solo en este laboratorio. Añadir la escena al final de Build Settings para permitir la recarga; conservar las entradas previas.
- [x] Tres pruebas Play Mode aprobadas: oleadas/puerta/final; curación/navegación; aproximación medida, muerte real, pausa/cursor y eventos persistentes de los botones de reinicio/menú/jugar. Reinicio comprobado con jugador sano, oleada sin iniciar, puerta cerrada y cuatro pickups. Sin errores de compilación capturados ni scripts ausentes; laboratorio limpio y fuera de Play. Botones invocados programáticamente: pendiente revisión manual de clics/encuadre y partida completa.
- [x] MainScene, manifest y packages-lock conservados por hash. Respaldo en Tools/IronRequiem/Backups/20261103-rooms-menu. Sin control del escritorio, paquetes, créditos ni otras mejoras.
### Corrección del panel negro persistente (03/11/2026)

- [x] Causa confirmada por captura y jerarquía en ejecución: InGameMenuManager desactivado no ejecutaba Start, dejando activo su MenuRoot de opciones. Ocultar ambos MenuRoot heredados mediante APIs del Editor y overrides de las instancias solo en CombatTwoRoomsLab; conservar prefabs y MainScene.
- [x] Cuatro pruebas Play Mode aprobadas, incluida regresión del menú: inicio, Jugar, muerte, reinicio y regreso al menú sin panel heredado. Raycast de EventSystem identifica el botón visible; eventos simulados de puntero accionan los tres botones. No se usó control del escritorio ni clic físico. Compilación sin errores capturados. Respaldo: Tools/IronRequiem/Backups/20261103-menu-black.
### Combate ágil y ejecución para extraer armas (03/11/2026)

- [x] Crear variantes propias en Assets/IronRequiem/Prefabs/FastCombat y Assets/IronRequiem/Scenes/FastCombatExecutionLab.unity a partir del laboratorio de dos cuartos. Conservar MainScene, escenas previas, prefabs originales, modelos, GUID, URP e Input System. Agregar solo el laboratorio a Build Settings para su reinicio. Parámetros iniciales guardados en FastCombatTuning.asset; valores efectivos editables en los componentes de las variantes y la instancia del jugador. No se aplican multiplicadores en Awake ni al reiniciar.
- [x] Jugador: suelo 13 a 14,95 m/s; sprint 19,5 a 22,425; aire 10 a 11,5; dash 24 a 27,6. Escalar la curva de velocidad del slide al 115 %, conservando sus duraciones y controles. Sin cambios en vida, daño, salto, cámara ni cadencias. Enemigos: movimiento al 120 %, vida y daño al 80 %. T-07: 128 HP, 4,2 m/s, daño 8; Big Bot: 960 HP, 1,68 m/s, daño 8; dron: 80 HP, agente 4,2 m/s, daño 8; torreta: 320 HP, fija, daño 2,4. Mantener percepción, preparación y cadencias. T-07 y Big Bot conservan sincronización de locomoción por distancia/velocidad real.
- [x] Reutilizar E, StolenWeaponCarrier, MechanicHud, Health y el inventario existente. Mostrar «[E] Ejecutar y tomar arma» solo con un módulo compatible disponible y vida estrictamente inferior al 75 %. Revalidar proximidad de 2,5 m a la superficie, objetivo apuntado y pared antes de reservar inventario. Entregar una instancia de arma antes de matar; impedir entregas repetidas. T-07 y Big Bot compatibles; drones y torreta no tienen módulo compatible y no reciben uno inventado. Conservar recogida de armas lanzadas, carga T-07, ráfaga Big Bot, energía, humo, OVERDRIVE y explosión.
- [x] Desactivar desprendimientos recogibles automáticos solo mediante campos optativos de las variantes. Mantener restos, daño visual y pérdida/emergencia de un cañón del Big Bot con restos decorativos. Añadir receptor opcional de salud a Damageable, configurado únicamente en el hitbox de unión del T-07 nuevo, para que el antiguo depósito de integridad no absorba disparos sin reducir vida principal. Conservar el comportamiento de los assets previos. Reutilizar el HUD modular del laboratorio de destrucción y enlazarlo al jugador nuevo; no crear otra UI de recogida.
- [x] Big Bot robado: daño de proyectil explosivo 25 a 30 y reserva 100 a 120, con valores redondeados para evitar mostrar 121 o retrasar el umbral crítico por precisión flotante. Conservar 12 de energía por proyectil, tres por ráfaga, intervalo 0,18 s, pausa 1,5 s, radio 2,6 m y OVERDRIVE al 20 % (24 de energía), con gracia de 5 s. La explosión propia del módulo conserva sus reglas anteriores.
- [x] Cinco pruebas Play Mode finales aprobadas: comparación de valores de las cuatro variantes y movimiento mediante W virtual (14,9498 m/s); umbral exacto del 75 %, daño de unión a Health raíz, distancia, pared, inventario lleno, prompt, entrega con E y repetición; muertes normales y pérdida crítica del Big Bot sin módulos recogibles; ejecución del Big Bot, tres tiros con M1, energía 120 a 84, cambio de arma, lanzamiento con G, humo crítico a 24 y detonación; proyectil real del Big Bot con daño 8 y aplicación de 8 al jugador por Damageable/Health. Sin control del escritorio. Compilación sin errores capturados y escena sin scripts ausentes. No se midieron FPS ni se realizó una partida manual completa.
- [x] Soundtrack excluido por petición posterior del usuario: no se añadieron pistas, fuentes de música ni cambios de mixer. Sin instalaciones, compras ni créditos. Respaldo: Tools/IronRequiem/Backups/20261103-fast-execution. Evidencias breves y XML: Tools/IronRequiem/Reports/FastCombat.

Pendiente: revisión manual de dificultad, claridad del HUD y combate completo con las cuatro rondas. No se integraron estas variantes en MainScene ni en el laboratorio anterior.
### Alcance enemigo y seis proyectiles del Big Bot (04/11/2026)

- [x] Crear variantes a partir de FastCombat en Assets/IronRequiem/Prefabs/EnemyRange y EnemyRangeBurstLab.unity, conservando MainScene, escenas anteriores, modelos y prefabs originales. Valores serializados desde la base actual una sola vez; no hay multiplicadores en ejecución. Conservar salud, daño, movimiento, proyectiles y referencias de módulos robados. Solicitar registro del laboratorio mediante EditorBuildSettings; no se probó reinicio ni ejecutable de este laboratorio.
- [x] Alcance de ataque: T-07 10 a 12,5 m; dron 10 a 12,5 m; torreta 35 a 43,75 m; Big Bot 22 a 28,6 m, sin sumar el incremento general. Detección T-07 16, dron 20 y Big Bot 36 m conservadas; torreta 30 a 45,75 m. Trayectoria sin gravedad: proyectiles comunes/torreta 100 m/s durante 5 s (500 m nominales); Big Bot 22 m/s y 48 m de recorrido. No fue necesario ampliar velocidad ni vida de proyectiles.
- [x] Big Bot: seis proyectiles totales por ráfaga, tres de cada cañón por alternancia; con uno restante, seis de ese cañón. Intervalos 0,18 a 0,15 s normal y 0,12 s de emergencia conservado. Pausa entre último tiro y siguiente primer tiro: 1 s normal y 0,75 s de emergencia, incluyendo el aviso existente de 0,7 s. Antes las pausas configuradas de 1,5/0,9 s sumaban el aviso. Campo optativo pauseIncludesPreparation permite conservar las variantes anteriores. Daño 8 por proyectil, 48 máximo por ráfaga frente a 24 antes; explosiones y radio originales. Mayor presión solicitada, pendiente valorar dificultad manualmente.
- [x] EnemyController conserva la arquitectura existente y añade guardia optativa de visibilidad y trayecto boca/objetivo antes de cada tiro, activada solo en las nuevas variantes. Impedir disparos durante la memoria de un objetivo oculto; las balas ya emitidas siguen colisionando normalmente. BigBotCombat conserva su comprobación propia, objetivo anunciado y presupuesto pendiente al perder un cañón. Muerte y pérdida de visión/objetivo cancelan la cola. Sin carga ni cambios al arma robada del jugador.
- [x] Cinco pruebas Play Mode finales aprobadas: configuración/referencias y balance conservados; dos ráfagas reales alternadas de seis; pérdida tras el primer tiro, cinco pendientes desde el superviviente y siguiente ráfaga de seis, más muerte; pared entre tiros y muerte del objetivo; impactos reales más allá del alcance anterior para los cuatro tipos y bloqueo por pared tras vencer el cooldown. Medidos a 60 Hz: intervalo normal 0,150 s, emergencia 0,133 s; pausas 1,017 y 0,767 s. Impactos en jugador a 12 m T-07/dron, 42 m torreta y 27,456 m Big Bot. La escena usa navegación existente; las pruebas fijan enemigos para aislar alcance y cadencia, no verifican otra vez persecución completa. Primer intento interrumpido porque el menú de muerte pausó el test; corregir únicamente el arnés y repetir cinco pruebas aprobadas.
- [x] Compilación sin errores capturados y laboratorio sin scripts ausentes. Sin escritorio, paquetes ni créditos. Respaldo Tools/IronRequiem/Backups/20261104-enemy-range; resultados XML y valores en Tools/IronRequiem/Reports/EnemyRange. Pendiente: revisión manual de dificultad y combate móvil completo; no se midieron FPS ni se compiló Player.
Comparación final de hashes protegidos: MainScene, escenas previas, prefabs, Packages y demás configuración conservados; excepción ProjectSettings.asset reserializado durante Test Runner/recompilación. Sin cambios intencionales de PlayerSettings; no sobrescribir configuración con un respaldo antiguo.

### Protección al ejecutar, carga T-07 y cohete robado del Big Bot (04/11/2026)

- [x] Crear variantes CombatFollowup y CombatFollowupLab.unity a partir del último laboratorio. Conservar modelos, MainScene, escenas previas, daño/vida/movimiento y módulos originales. Registro propio en Build Settings para el menú existente. No instalar paquetes ni usar escritorio.
- [x] Health acepta protección temporal independiente de Invincible. ExecutionProtection protege desde la entrega validada del arma, durante toda la animación de manos o salto/arranque/disparo/descenso del Big Bot, y 2 segundos adicionales al terminar. Duración editable en Inspector; no convertir la protección en invencibilidad permanente ni sobrescribir el modo invencible previo. Solo ejecuciones aceptadas conceden protección; no cambia el filtro de inventario/vida/proximidad.
- [x] T-07: EnemyController conserva detección, navegación y disparo existentes; campo optativo EnemyChargeDuration de 1,2 s solo en la nueva variante. Reutilizar geometría/materiales/partículas magenta de la carga del cañón, anclados a WeaponMuzzle, y un proyectil cargado propio. Daño 8, velocidad 40 m/s, alcance de ataque 12,5 m. No dispara antes de cargar; pared, muerte o pérdida de alcance/visión cancelan el progreso y limpian la presentación. Variantes anteriores mantienen duración cero y comportamiento previo.
- [x] Arma robada del Big Bot: proyectil variante del usado por el enemigo, conservando sus mallas, materiales, estela y trayectoria de 22 m/s con 48 m de recorrido. El jugador conserva daño 30, radio 2,6 m, reserva 120, coste 12 por proyectil y tres disparos por ráfaga a 0,18 s / pausa 1,5 s. Big Bot enemigo conserva seis disparos y balance anterior. No reintroducir carga en el Big Bot.
- [x] Primera verificación Play Mode: protección frente a daño repetido durante ambas ejecuciones, expiración y conservación de Invincible; ejecución completa del Big Bot y tres disparos del arma robada con gasto de 36; carga real del T-07 sin tiro anticipado y cancelación por pared/muerte. Audio reutilizado ensayado y descartado después de comparar tiempos. Las pruebas finales validan el audio nativo con caché del mixer.
- [x] Rendimiento: medir 12 enemigos inmóviles disparando (4 Big Bots, 8 T-07), calentamiento 3 s y captura 20 s en Editor, RTX 4050 Laptop, Game View 1001x508, calidad Default, VSync 0, sin límite. Baseline: mediana 18,73 ms, p95 29,86, p99 37,87, máximo 48,96, 2,11 % sobre 33,33 ms, 150 disparos. Conservar caché de grupos del AudioMixer y eliminar el array temporal de colliders por proyectil. Consultas al mixer: 1.000 pasan de 13,11 ms a 0,25–0,63 ms; mismo grupo de salida. No interpretar GC.GetAllocatedBytesForCurrentThread=0 como prueba de cero asignaciones en este runtime.
- [x] Descartar pool de audio al empeorar frente a audio nativo con las mismas optimizaciones: p99 37,86 frente a 33,57 ms, mediana 20,09 frente a 18,42; 150 frente a 152 disparos. Código candidato archivado únicamente en el respaldo, retirado del runtime. Una captura intermedia presentó pico de 546 ms, no repetido en la siguiente (máximo 48,81). No atribuirlo a una causa no perfilada ni presentar los tirones como resueltos. Cero colecciones GC observadas durante estas capturas; asignaciones del Editor alrededor de 20 KB/frame incluyen herramientas y arnés.

Pendiente de rendimiento: captura de Player actual a 1080p con CPU/GPU y cargas de aparición/destrucción/armas; las capturas cortas del Editor no validan 120 FPS ni una mejora global consistente. No se redujo calidad ni se cambiaron mecánicas para inflar FPS. Evidencias y XML en Tools/IronRequiem/Reports/CombatFollowup; respaldo Tools/IronRequiem/Backups/20261104-combat-followup.
Verificación final tras retirar el pool: 5/5 pruebas Play Mode aprobadas, incluida caché de mixer/ruta de audio y clip nulo. Captura final de 20 s: mediana 17,72 ms, p95 28,85, p99 35,95, máximo 47,52; 1,78 % sobre 33,33 ms. Variación moderada frente al baseline, sin acreditar una mejora estable en Player. PlayerLoop medio 12,48 ms; no hay muestras GPU y EditorLoop=0 no identifica su coste real. Auditoría final sin errores de compilación ni scripts ausentes; CombatFollowupLab limpio y fuera de Play. Hashes de escenas previas, MainScene, modelos/prefabs originales, Packages y PlayerSettings conservados; cambio intencional en EditorBuildSettings para laboratorio.

### Escopeta secundaria, cooldown del slide, umbral E y rendimiento (04/11/2026)

- [x] Escopeta de plantilla como secundaria (ranura 2) mediante variantes `Prefabs/SecondaryShotgun`. Valores 168→90 por disparo (10×9), 6°, 0,85 s, 4 cargas, 32 m, caída 100 %→30 % entre 6 y 18 m. Medido sobre T-07: 90/26,2/2,7/0 a 3/12/22/40 m. Arma robada en la ranura 3 sin conflictos; lanzamiento con G libera solo esa ranura.
- [x] Cooldown del slide de 1,5 s desde el final del slide, con obligación de soltar C (campos opcionales; por defecto 0 = comportamiento previo). Shift + C, requisitos, piernas y chispas conservados.
- [x] Umbral E estrictamente < 75 % ya implementado; verificado en T-07 y Big Bot (75 % exacto rechazado, revalidación al pulsar E, entrega única).
- [x] Animaciones: escopeta montada en el antebrazo modular (antes la mano la atravesaba) y cambio de arma con suavizado opcional en el laboratorio. Capturas revisadas.
- [ ] Rendimiento: baseline Release 1080p exclusivo de 5 min registrado (media 6,90 ms, p95 10,60, p99 11,99, 26,6 % > 8,33 ms, limitado por CPU). Throttling de NavMesh en variantes; after medido: media 6,97, p95 10,43, p99 11,67, 26,4 % > 8,33 ms, 0,05 % > 16,67 ms, 15 frames > 33 ms. Sin mejora medible. 120 FPS no alcanzados; 60 FPS casi estables (persisten picos de GC/aparición).
- [x] 7/7 Play Mode + 6/6 regresión FastCombat. MainScene, CombatFollowupLab, prefabs originales, Packages y ProjectSettings.asset conservados. Informe: `Docs/IronRequiem/SECONDARY_SHOTGUN_SLIDE_EXECUTION.md`. Respaldo: `Tools/IronRequiem/Backups/20261104-shotgun-slide-exec`.

### Brazo, ejecuciones, recarga y búnker (06/11/2026)

- [x] Brazo: mangas hasta el hombro (BunkerLab). Frames con el corte visible en las ejecuciones del T-07 / Big Bot: 43/70 y 47/70 → 0.
- [x] Umbral de ejecución < 75 % fijo para todos los enemigos ejecutables. 75 % exacto rechazado; 74,99, 51, 50 y 49,9 % aceptados; las barras coinciden con Health.
- [x] T-07: el arma se entrega al pulsar E; el primer golpe impacta a 0,35 s y la muerte llega con el puñetazo (0,733 s). 2 impactos y 1 muerte.
- [x] Escopeta: sin regeneración; recarga de 1,25 s tras 4 disparos con celdas que bajan, silbido y vapor; disparo bloqueado mientras dura; pausa al cambiar de arma; fogonazo ×1,8.
- [x] Búnker cerrado con texturas, techo, 23 luces, niebla y NavMesh rehorneado en un asset nuevo.
- [x] Pico de aparición: `EnemySpawnQueue` (precarga inactiva + 1 activación por frame) en BunkerLab y BunkerBenchmark. A/B 1080p: frames > 50 ms de 14 a 0; máximo de 80,7 a 27,3 ms. Con cola, los Big Bot disparan más (efecto de la rejilla del benchmark; ver informe §7).
- [ ] Repetir el A/B con el portátil a plena potencia (en esta medición la GPU estaba limitada a 16–20 W).
- [x] 8/8 Play Mode en BunkerPolishTests y 7/7 en SecondaryShotgunTests. Informe: `Docs/IronRequiem/BUNKER_POLISH.md`.

### SawBot: optimización y reparación de sierras (06/11/2026)

- [x] Inspeccionar sawbot.fbx y sawbot2.fbx: misma geometría, topología, UV y cuatro texturas 4K; una malla cerrada de 49.948 triángulos, sin rig, pesos ni animaciones. Usar el primero como base, conservando ambos hashes originales.
- [x] Reconstruir las sierras en copias: cortar las regiones distales identificadas en las bocas de las carcasas, cerrar uniones y preparar guías, cadena continua, enlaces uniformes, cortadores alternados y remaches. Conservar carcasas y proporciones. Rehacer UV de las sierras sobre muestras del atlas original para evitar estiramiento; conservar UV restantes. Pivotes independientes en el FBX por piezas y Blender.
- [x] Exportar LOD0/1/2 de 22.184 / 12.384 / 8.200 triángulos; una malla y material en cada exportación ensamblada. Usar reducción menos agresiva en LOD2 para conservar topología cerrada. Mantener altura de 0,778153 m y contacto con el suelo igual entre LOD. Texturas 2K, normales renormalizadas y Metallic/Smoothness empaquetado para URP.
- [x] Verificar reimportación de seis FBX/GLB: conteos exactos, UV finitas, un material, ausencia de huesos/animaciones y cero bordes no manifold tras soldar divisiones de formato. Verificar cinco piezas y sus pivotes; revisar renders frontal, lateral y tres cuartos. FBX fuentes intactos; escenas, Packages y ProjectSettings sin cambios por hash. No se ejecutó Unity/Play Mode ni se midieron FPS: esta es una entrega de geometría, no de IA o rendimiento del juego.
- [x] Entrega: Tools/IronRequiem/Exports/IR_SawBot_Optimized_v01.zip, con FBX, Blender editable, texturas, vista y verificación. GLB disponibles fuera del ZIP en la carpeta homónima. Checklist limitada a esta tarea; sin instalaciones, créditos ni cambios de gameplay.
### SawBot jugable: animación e IA (06/11/2026)

- [x] Preparar copias articuladas de los tres LOD con separación geométrica real y tapas en ruedas, hombros y antebrazos. Conservar las fuentes FBX y el paquete optimizado anterior. Crear un esqueleto compartido de once uniones en Unity y pesos rígidos unitarios; una malla skinned/material por LOD. Geometría con interiores: 25.656 / 14.866 / 10.100 triángulos. Prefab visual a escala uniforme 2 (altura aproximada 1,56 m); assets fuente sin normalización.
- [x] Animator con reposo, rodadura, ataque, reacción aditiva al daño y muerte con desplome del chasis/fallo de energía. Sin root motion. Ruedas sincronizadas con desplazamiento y giro reales del NavMeshAgent; cadenas con vibración de motor. Límites de skin conservadores para no desaparecer cerca de cámara. No se retargetearon clips del T-07 ni se asumió un rig en el FBX estático.
- [x] Reutilizar EnemyController, EnemyMobile (Patrol/Follow/Attack), DetectionModule, NavigationModule, Health y Damageable. Añadir política específica de ataque cuerpo a cuerpo, con línea de visión/rango/ángulo revalidados en el evento de impacto. Adaptación optativa AllowWeaponlessPolicy en EnemyController, desactivada en enemigos previos; SawBot no contiene arma de proyectiles ficticia.
- [x] Parámetros iniciales: 140 HP, 5,2 m/s, detección 22 m, alcance 1,55 m, daño 12, preparación 0,45 s y recuperación 0,75 s. Impacto único por ataque; paredes, pérdida de objetivo y muerte cancelan pendientes. Reutilizar respuesta blanca/chispas y explosión corporal cosmética del pool existente. Parar IA, navegación, audio y colisiones al morir; ocultar barra/núcleo y retirar cadáver a los 4 s mediante EnemyController. Sin daño explosivo nuevo ni drops inventados.
- [x] Prefab Assets/IronRequiem/Prefabs/Enemies/SawBot_AI_v01.prefab. Laboratorio Assets/IronRequiem/Scenes/SawBotLab.unity con oleadas propias. Integrar un SawBot adicional en la primera oleada de BunkerLab: conservar seis T-07, resto de grupos/oleadas y sus cantidades. Usar EnemySpawnQueue existente, navegación ya horneada, barra de salud y materiales URP compartidos. Añadir solo SawBotLab a Build Settings; MainScene conservada.
- [x] 6/6 pruebas Play Mode finales aprobadas: esqueleto/pesos/LOD y deformación de ataque; persecución/giro/ruedas por navegación real; anticipación, daño 12 una vez y muerte durante el segundo ataque; rodeo de obstáculo con carving y pared interpuesta durante preparación; proyectil real del jugador, destello restaurado, golpe F único entre varios hitboxes, muerte masiva/repetida, pose sobre el suelo y limpieza; primera oleada de Bunker con seis T-07 y un SawBot y descuento único al morir. Entrada virtual/API sin controlar escritorio. Compilación sin errores capturados; escena de pruebas limpia y fuera de Play.
- [x] Respaldo Tools/IronRequiem/Backups/20261106-sawbot-gameplay; resultados y XML en Tools/IronRequiem/Reports/SawBotGameplay. Restaurar reserializaciones incidentales de TimeManager, QualitySettings y UnityConnectSettings a sus bytes iniciales verificados. Cambios protegidos intencionales: BunkerLab y entrada nueva de Build Settings. Sin paquetes, créditos ni cambios a controles/armas del jugador.

Pendiente: revisión manual del ritmo/sonido y dificultad. Las cadenas vibran, pero los eslabones no circulan individualmente. SawBot aún no tiene un módulo de sierra compatible con el inventario del jugador; no se añadió una recogida paralela ni se le asignó un cañón de otro robot. No se midieron FPS ni se compiló un Player para esta integración.### Calor de armas y menús V1 (06/11/2026)

- [x] Analizar ambos videos locales: canal curvo de calor con marco agrietado y paneles tecnológicos oscuros con bordes cian. Crear una interpretación industrial en mallas uGUI y colores de vértice, sin servicios de generación, compras, paquetes ni texturas externas.
- [x] WeaponHeat por instancia, opcional en Inspector: máximo 100, +8 por disparo aceptado, retardo 0,6 s, enfriamiento 20/s, recuperación a 35. Bloqueo con histéresis; enfriamiento detenido durante pausa y activo en armas ocultas. Conservar estado al cambiar. Gate en WeaponController y evento OnShootProcessed; una suma por disparo, independientemente de proyectiles, pellets o cuadros de entrada.
- [x] Variante Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_Pistol_Thermal.prefab conectada al inventario de MainScene. Mantener valores originales: 16 de munición y 0,4 s entre disparos; no modificar daño, proyectil ni recarga. No activar calor en módulos robados. HUD independiente de munición, energía y OVERDRIVE; porcentaje, estado escrito, relleno amarillo/naranja/rojo y pulso moderado sin reconstruir la malla para pulsar. Diseño adaptable para separar el indicador del arma en 1080p y 4:3.
- [x] Reutilizar IntroMenu, StartButton, LoadSceneButton, MenuNavigation, EventSystem e InGameMenuManager. JUGAR carga MainScene; OPCIONES, CRÉDITOS editables y SALIR. Pausa con Escape y menú anterior reutilizado como autoridad de tiempo/cursor. Opciones de volumen mediante MainAudioMixer, sensibilidad y pantalla completa persistentes en PlayerPrefs propios. Restaurar tiempo y audio al regresar al menú; volumen elegido conservado al reanudar. Música existente del menú conservada y encaminada al grupo Master existente.
- [x] Crear ThermalMenuLab y ThermalIntroMenuLab mediante APIs del Editor; comprobar allí antes de aplicar a MainScene e IntroMenu. Respaldos en Tools/IronRequiem/Backups/20261106-thermal-menu. Conservar escena de trabajo SawBotLab limpia y fuera de Play; originales de armas/prefabs preservados. Añadir solo ambos laboratorios a Build Settings; restantes ProjectSettings, manifest y packages-lock iguales al respaldo por hash.
- [x] 5/5 pruebas Play Mode finales aprobadas: 13 disparos a cadencia/munición originales alcanzan bloqueo; tiro rechazado no suma calor ni consume munición; retardo, enfriamiento y recuperación; carga nativa/pellets/entrada automática virtual sin duplicados; inventario con dos estados térmicos y HUD por arma; movimiento y cámara con entrada virtual; Escape, bloqueo de entrada, opciones y restauración del volumen. Navegación con teclado, Enter para JUGAR, flujo real IntroMenu → MainScene → IntroMenu, persistencia de sensibilidad/volumen/pantalla completa y SALIR seguro en Editor. Los parámetros alterados para cubrir carga/pellets/automático en la matriz de prueba son únicamente instancias de ejecución.
- [x] Compilación sin errores capturados; MainScene sin scripts ausentes. Capturas de menú, opciones, créditos, pausa y calor 0/32/72/100 en Tools/IronRequiem/Reports/ThermalMenu; también 1440×1080 (4:3). Resultados reales en play.xml. Pruebas mediante API/entrada virtual, sin controlar escritorio ni guardar cambios de ejecución.
- [ ] Validar cambio físico de pantalla completa y cierre de aplicación en un Player de Windows. No se generó un ejecutable para esta tarea.

Archivos propios principales: WeaponHeat.cs, WeaponHeatHud.cs, ThermalGaugeGraphic.cs, IndustrialPanelGraphic.cs, RequiemMenuView.cs, RequiemSettings.cs e IWeaponShotGate.cs; autoría/pruebas ThermalMenuAuthoring.cs, ThermalMenuVerification.cs y ThermalMenuTests.cs. Cambios acotados en WeaponController.cs, PlayerInputHandler.cs, InGameMenuManager.cs y MenuNavigation.cs; ambas escenas existentes, variante térmica y dos laboratorios. Créditos configurables en RequiemMenuView; posición amplia/estrecha del HUD configurable en WeaponHeatHud.

### Saw Bot sostenido e interfaz en dos cuartos (06/11/2026)

- [x] Inspeccionar prefab real SawBot_AI_v01: tres SkinnedMeshRenderer con el mismo rig rígido de once uniones, Health/Damageable, EnemyController/EnemyMobile, DetectionModule y NavMeshAgent. Mantener fuentes, prefab original y MainScene. Reproducir en Play Mode el defecto lateral: al rotar las ruedas 90°, fragmentos del chasis/antebrazos orbitaban y parte del neumático permanecía en el cuerpo; no eran cubos de depuración ni partículas mesh.
- [x] Separar nuevamente ruedas en Blender sobre copias con volúmenes cilíndricos centrados en el eje real; conservar el resto en el chasis. Corregir pivotes antes de exportar los tres LOD; conservar UV/materiales, rehacer bind poses y ajustar hitboxes de ruedas en la variante. Nuevas mallas de 24.874 / 14.434 / 9.740 triángulos; original de 25.656 / 14.866 / 10.100 conservado. No interpretar esta reducción como mejora de FPS medida.
- [x] Variante Assets/IronRequiem/Prefabs/Enemies/SawBot_Sustained_v02.prefab. Reutilizar SawBotCombat como política de EnemyMobile; modo sostenido optativo, original con golpe conservado. Velocidad 5,2 → 7,2 m/s, aceleración 18 → 24 m/s², giro 180 → 300°/s y giro durante contacto 140°/s. Entrada de ataque horizontal 1,1 m, salida 1,4 m, anticipación 0,25 s y radio de contacto 0,16 m, configurables. SawBotCombat configura NavigationModule/Agent al iniciar; ajustar allí velocidad/aceleración de esta variante. El agente mueve el root; no hay root motion ni traslación artificial para perseguir.
- [x] Crear clips de preparación y bucle sostenido con hombros/codos articulados. Conservar MoveSpeed, Attack, Alerted y OnDamaged; DriveSpeed sigue velocidad real. Detener el agente durante ataque y permitir giro controlado; cancelar al perder objetivo, visión o alcance. Salud/muerte conservadas y desplome con transición breve. Los eventos antiguos SawImpact/SawRecover quedan sin efecto únicamente en modo sostenido.
- [x] Daño 20/s en pasos de 0,1 s (2 por paso). Cápsulas en las hojas reales, filtro por Actor/Health y raycasts de obstrucción; un único daño por objetivo/paso entre ambos brazos y múltiples colliders. Tiempo escalado independiente del clip y del framerate. Chispas existentes y PlayOneShot sobre AudioSource existente, sin crear fuentes por cada tick.
- [x] Ruedas calculadas por desplazamiento mundial de cada eje y radio 0,28 m; diferencia izquierda/derecha en curvas y giro sobre el sitio. Los clips no escriben ruedas. Paradas sin giro residual. Originales conservados; assets revisados bajo Assets/IronRequiem/Art/Enemies/SawBotSustain y copia editable Tools/IronRequiem/Exports/IR_SawBot_Articulated_v02/IR_SawBot_Articulated.blend.
- [x] Reemplazar referencias existentes de Saw Bot en BunkerLab y SawBotLab por la variante, conservando cantidades y sistemas de oleadas. Crear Assets/IronRequiem/Scenes/SawBotSustainLab.unity como copia de pruebas con navegación existente; añadir solo este laboratorio a Build Settings.
- [x] Interfaz industrial, opciones, pausa con Escape y HUD térmico en CombatTwoRoomsLab y BunkerLab. Reutilizar CombatLabMenu para inicio/muerte/reinicio y RequiemMenuView/InGameMenuManager para presentación/pausa. Contrato mínimo ICombatLabMenuFlow evita otra autoridad de pausa o carga; el botón JUGAR inicia el propio laboratorio. Conservar muerte y reinicio desde cuarto 1. Añadir variantes térmicas de las armas iniciales y retargetear también sus referencias de presentación del brazo, sin cambiar estadísticas ni módulos robados.
- [x] 8/8 pruebas Play Mode finales: reproducción visual original; ruedas reparadas, cuarto de circunferencia/90°, diferencial, parada y tres LOD sin fragmentos orbitales; persecución/giro; rodeo de pared con carving y recuperación del contacto; daño a 30 y 144 Hz simulados; interrupción por pared/muerte; interfaz real de CombatTwoRoomsLab, arma/representación del brazo, calor, Escape/opciones, muerte y reinicio. Medidas: 40 de daño en aproximadamente 2 s de contacto a ambas frecuencias; desplazamiento del cuerpo durante contacto 0 m. Pico de velocidad del agente 7,2 m/s; giro máximo muestreado 5° por frame a 60 Hz; 60/64 muestras en movimiento alineadas; rodeo lateral 5,7 m. Son medidas de simulación/IA, no FPS del Player.
- [x] Capturas reales de Game View: gameview-two-rooms-menu.png y gameview-saw-contact.png. Vistas Play Mode de giro, contacto, ruedas antes/después y muerte en Tools/IronRequiem/Reports/SawBotSustain. XML test.xml, resultados de daño dps-30.txt/dps-144.txt. Cámara de captura etiquetada MainCamera para que WorldspaceHealthBar siga una cámara válida; no se modificó ese sistema del juego.
- [x] Respaldo Tools/IronRequiem/Backups/20261106-sawbot-sustain. Hashes de MainScene, prefab original, manifest y packages-lock conservados; otros ProjectSettings sin cambios salvo la entrada del laboratorio en Build Settings. Sin paquetes, servicios de generación, compras, créditos ni control del escritorio.

Limitación visual: rig de piezas rígidas con once articulaciones; las cadenas vibran, sus eslabones todavía no circulan individualmente. No se creó un módulo de sierra robable. Revisión manual de sensaciones y audio pendiente; no se compiló Player ni se midió rendimiento gráfico para esta tarea.

### Saw Bot: muerte pesada, motosierra robable y copia LMMS (06/11/2026)

- [x] Variante SawBot_Arsenal_v03.prefab, conservando SawBot_Sustained_v02 y fuentes. Muerte de 2,1 s con desequilibrio, caída completa del rig y brazos plegados; versiones Death, DeathContact y DeathDisarmed. Ajustar altura de la animación contra las mallas de los tres LOD en 127 muestras, manteniendo inmóvil el root de gameplay y deteniendo navegación/ataque inmediatamente. Reutilizar explosión corporal cosmética a 1,35 s y limpieza existente a 4 s. Prueba: 126 frames de los tres LOD, mínimo local 0,0025 m sobre suelo plano, inclinación mayor de 65°.
- [x] Separar visualmente el módulo derecho por sus tres piezas reales ya articuladas: Forearm_R, GuideBar_R y Chain_R. Conservar pesos, UV y material; cuerpo y módulo comparten esqueleto/LOD. EnemyExecutionSource existente entrega la motosierra con E por debajo de 75 % de vida. Reserva congela ataque y oculta únicamente el módulo derecho; forceRenderingOff impide que reaparezca al cambiar LOD. Entrega única, sin drops nuevos al morir normalmente.
- [x] Weapon_SawBot_Stolen.prefab y SawBot_StolenModule.prefab reutilizan WeaponController, StolenWeaponCarrier, CannonAmmo, CannonOverdrive, CannonExplosion y Damageable. M1 mantenido: 45 daño/s, alcance 1,65 m, radio 0,2 m, pasos de 0,1 s y 12 energía/s; reserva 100. Sin proyectil ni carga. Sustituir visualmente la mano mediante presentación FPS actual, con Attachment propio; ajustar encuadre, vibración y chispas de contacto. Conservar cambio de arma, G, recogida posterior, humo crítico, cuenta regresiva y explosión. Interfaz IStolenWeaponBinding comparte el enlace de energía con los adaptadores existentes de carga T-07 y ráfaga Big Bot; sus comportamientos no cambian.
- [x] Actualizar referencias existentes de Saw Bot en BunkerLab, SawBotLab y SawBotSustainLab sin cambiar cantidades. Laboratorio nuevo Assets/IronRequiem/Scenes/SawBotWeaponDeathLab.unity. MainScene y configuración de paquetes preservadas; solo añadir ese laboratorio a Build Settings. Respaldos en Tools/IronRequiem/Backups/20261106-sawbot-weapon-death.
- [x] 6/6 pruebas Play Mode: muerte desde reposo/contacto y limpieza; extracción real con E, entrega única y módulo oculto en los tres LOD; 45 daño y 12 energía por segundo de M1 sin daño múltiple por hitboxes, sin proyectiles, pared y liberación; cambio de arma, lanzamiento/recogida sin recargar energía, pausa y detonación crítica única; carga T-07 y ráfaga Big Bot siguen enlazando y consumiendo recurso. La prueba de pared usa un objetivo inmóvil fuera de la zona donde el NavMesh recolocaba el spawn y solapaba pared/enemigo. XML y capturas en Tools/IronRequiem/Reports/SawBotWeaponDeath.
- [x] Abrir LMMS 1.2.2 con Tools/IronRequiem/Music/ManualEdits/20261106_SawBotSession/IR_Combat01_Loop_Manual.mmp. Copiar también Full, Explore y Menu a esa carpeta. Proyectos nativos editables con instrumentos/FX; SoundFont existente verificado. Ventana abierta y respondiendo. No modificar MMP/WAV originales ni renderizar encima de la música del juego.

Pendiente: icono y grabación de motor específicos de motosierra; se reutilizan recursos de la plantilla. La cadena conserva vibración mecánica, sin circulación individual de eslabones. Caída verificada sobre suelo plano; adaptación física a pendientes/obstáculos no incluida. No se instaló software ni se usaron servicios de pago/créditos.

### Saw Bot: sierras alineadas, cadena móvil, humo y motor (07/11/2026)

- [x] Inspeccionar rig y geometría reales: guías y cadenas coincidían entre sí, pero desviaban 16,7° respecto al eje muñeca/antebrazo. Corregir ambos lados en SawBot_SawPolish_v04.prefab, conservando posiciones de unión, UV, materiales, rig, ataque y tres LOD; originales v01/v02/v03 intactos.
- [x] Cadena que circula alrededor de una guía fija. Compartir 24 mallas de fase entre ambos brazos, enemigos y arma robada; sin crear mallas o materiales por frame. Incluir los renderers de cadena en LOD, feedback y ocultación por extracción, sin reaparición del módulo derecho.
- [x] Humo breve desde puntos exteriores medidos de la carcasa. Material URP compartido existente, 10 partículas/s, duración 0,45–0,7 s, máximo 12 por sierra, sin luces. Conservar aparte el humo crítico del módulo. Sonido original de motor sintetizado, mono, bucle de 2 s; salida al mixer existente, sin servicios de pago ni grabaciones descargadas.
- [x] Variantes Weapon_SawBot_Animated_v02.prefab y SawBot_AnimatedModule_v02.prefab; conservar 45 daño/s, alcance 1,65 m, reserva 100 y consumo 12/s. Mismo sistema E, inventario, cambio y lanzamiento. Referencias de Saw Bot actualizadas en los laboratorios existentes; nuevo SawBotSawPolishLab.unity. MainScene, prefabs originales, scripts de combate y configuración/paquetes sin cambios por hash.
- [x] Reajustar únicamente la altura visual de copias de Death/DeathContact/DeathDisarmed para la nueva orientación de hojas. 127 muestras, tres LOD; prueba de caída desde ataque: mínimo medido 0,00941 m sobre suelo plano, root de gameplay fijo. Restaurar el dato de navegación existente que SaveScene(saveAsCopy) retiró del laboratorio fuente y asignar una copia del mismo al nuevo laboratorio, sin rebake.
- [x] Compilación sin errores y 3/3 pruebas Play Mode finales: alineación y circulación con tres LOD, guía estacionaria, emisión y AudioSource reproduciendo, pausa/salida de alcance; extracción real con E, cadena retirada oculta, sierra equipada con M1, energía, liberación/cambio/lanzamiento; muerte durante ataque y limpieza inmediata de humo/audio. Capturas inspeccionadas en Tools/IronRequiem/Reports/SawBotSawPolish. No se realizó escucha manual del sonido ni medición de FPS.

Pendiente opcional: sustituir el motor sintetizado por una grabación específica con licencia. La circulación de cadena y el sonido provisional de hoverbot indicados como pendientes en el registro anterior quedan resueltos por esta variante.

### Menú industrial y spawn variado (07/11/2026)

- [x] Aplicar el menú industrial existente a SawBotLab, SawBotSustainLab, SawBotWeaponDeathLab y SawBotSawPolishLab. Reutilizar RequiemMenuView, CombatLabMenu e InGameMenuManager para jugar, opciones, pausa con Escape, muerte, reinicio y vuelta al menú. Conservar inventario, controles y HUD actuales.
- [x] Ampliar CombatTwoRoomsLab y EnemySpawnQueue existentes: tres composiciones configurables por oleada, semilla reproducible opcional (0 elige una nueva al recargar), orden y posiciones aleatorios, preferencia por puntos diferentes a la oleada anterior y entrada escalonada de 0,2–0,4 s. Precalentar solamente la composición seleccionada, una instancia por frame. No reciclar enemigos muertos ni añadir otro coordinador/pooling de salud.
- [x] Activar variedad en SawBotLab, BunkerLab y CombatTwoRoomsLab; usar los prefabs actuales de cada escena y el SawBot_SawPolish_v04. Conservar los grupos originales como configuración fija opcional, cantidades totales por oleada y torreta central fija. Totales: SawBotLab 2/3/4/5; BunkerLab 7/4/5/5; CombatTwoRoomsLab 6/4/5/5. No cambiar vida, daño, armas, velocidad o IA de los prefabs.
- [x] Revalidar la posición justo antes de activar cada enemigo: distancia al jugador (6 m más radio), NavMesh, ruta completa, límites del cuarto, cápsula física libre y separación entre apariciones. Si todos los puntos están bloqueados, mantener la instancia inactiva, indicar espera en el HUD y reintentar cada 0,25 s; no avanzar la oleada mientras haya apariciones pendientes. Pausa y muerte bloquean nuevas activaciones.
- [x] Cada laboratorio usa su propia copia del NavMesh ya horneado. Causa comprobada de referencias perdidas: NavMeshSurface.OnValidate.UnshareNavMeshAsset elimina datos compartidos al abrir copias de escena juntas. Restaurar los laboratorios afectados sin rebake ni cambios de geometría; MainScene intacta. Laboratorio separado Assets/IronRequiem/Scenes/CombatVarietyLab.unity, sin modificar Build Settings.
- [x] Compilación sin errores y 4/4 pruebas Play Mode. Botones reales mediante EventSystem: jugar, opciones/volver, Escape, muerte/reinicio y volver al menú industrial. Semillas11/27/43 producen mezclas y puntos distintos; jugador movido después de encolar respeta distancia al activarse. Todos los puntos bloqueados esperan y se recuperan; cantidades7/4/5/5, superviviente que retiene oleada, cuerpos/restos sin contar, puerta tras la tercera, torreta y final correctos. Configuración fija original6/4/5/5 sigue pasando su prueba de compatibilidad.

Pruebas de spawn congelan la IA después de aparecer para medir posiciones; no constituyen una nueva prueba de combate o rendimiento. Captura industrial-menu.png y resultados en Tools/IronRequiem/Reports/CombatVariety. Respaldo Tools/IronRequiem/Backups/20261107-menu-spawn-variety. Sin paquetes nuevos ni control del escritorio.

### Regresiones de música, pausa, sensibilidad y lanzamiento Big Bot (07/11/2026)

- [x] Reproducir antes de corregir: CombatVarietyLab no tenía MenuMusic; sensibilidad guardada y real3,2, pero pausa mostraba0,8392188; frame de pausa1451,45x1013,51px en pantalla1001x470px. Módulo Big Bot creado con escala3,48 aunque su prefab y articulación FBX requieren348,18: cien veces menor, con todos los LOD fuera del umbral de visibilidad. Captura de caracterización baseline.xml/baseline.txt y PNG; la prueba inicial de recogida se ajustó para sincronizar físicas antes de apuntar al collider.
- [x] Reconectar IR_Menu01_Loop.wav existente al grupo Music existente en siete laboratorios. MenuMusic se desvanece/detiene al jugar; CombatMusic conserva sus capas sincronizadas y entra progresivamente al salir del menú, sin superposición permanente. No modificar WAV, mixer, música editable LMMS ni clips originales. Componentes sin vínculo a CombatLabMenu conservan el comportamiento anterior.
- [x] RequiemMenuView refresca controles sin notificar callbacks al abrir pausa/opciones. Mantener RequiemSettings como fuente persistente y PlayerInputHandler actual. Ajustar marco a viewport real, responder a cambios de dimensiones, respetar escala automática de Canvas y retirar únicamente el fondo básico sustituido. Guardar CanvasScaler1920x1080 con SerializedObject/overrides en vez de perder valores del prefab. Sin modificar controles o cámara.
- [x] EnemyExecutionSource usa la articulación real CannonPivot/CannonFrontPivot de BigBotDestruction al crear el módulo. Conserva conversión FBX, tamaño físico, pivotes y LOD; no forzar LOD0 ni desactivar culling. Prefab/modelos originales, daño, energía, ráfagas y reglas de explosión sin cambios.
- [x] Registrar solamente SawBotSawPolishLab y CombatVarietyLab, que no estaban en la lista compartida de escenas y no podían reiniciarse con SceneManager.LoadScene. Preservar el orden y entradas anteriores. MainScene sin modificar.
- [x] Compilación sin errores y2/2 pruebas Play Mode finales: música reproduciendo con listener pausado, transición al jugar y retorno tras recargar; sensibilidad3,2 sincronizada con pausa/input y1,8 conservada al reabrir/reiniciar; marco dentro de1001x470px; módulo real Big Bot, E, lanzamiento, visibilidad automática a2/8/18m y recogida de la misma instancia con energía107/120 conservada. Capturas fixed-pause-options.png y fixed-throw-*.png en Tools/IronRequiem/Reports/MenuWeaponRegression. Preferencias originales restauradas después de pruebas; no se realizó escucha manual ni medición FPS.

Respaldo Tools/IronRequiem/Backups/20261107-menu-throw-fixes. Sin paquetes nuevos, servicios de pago ni control del escritorio.

Verificación visual adicional: repetir solo lanzamiento/LOD/recogida con el cadáver del donante retirado en la prueba para evitar oclusión de la cámara; 1/1 aprobada. Capturas del módulo aislado a 2, 8 y 18 m. Ningún cambio adicional de gameplay.

## HUD térmico ausente en laboratorios (07/11/2026)
- [x] Corregir SawBotLab, CombatVarietyLab, SawBotSustainLab, SawBotWeaponDeathLab y SawBotSawPolishLab: faltaban WeaponHeatHUD y la referencia a la variante térmica existente del módulo principal. Reutilizados ThermalMenuAuthoring.AddHeatHud y Weapon_IR_ArmModule_v07_Thermal; referencias de presentación del antebrazo sincronizadas. Secundarias y armas robadas sin cambios.
- [x] Play Mode: dos pruebas aprobadas. Un HUD visible y conectado en siete laboratorios, incluidos CombatTwoRoomsLab y BunkerLab; fuego automático real con M1, 13 disparos hasta 100 %, bloqueo térmico, enfriamiento, ocultación al cambiar a la secundaria y recuperación al volver. Captura revisada en Tools/IronRequiem/Reports/ThermalHudRepair/heat-visible.png. Compilación sin errores capturados.
- [x] Respaldos en Tools/IronRequiem/Backups/20261107-thermal-hud-repair. MainScene, WeaponHeatHud.cs, paquetes y Build Settings conservados. Verificación en Editor; no se generó Player ni se midió rendimiento.

## Autonomía térmica y escopeta sin cartuchos (07/11/2026)
- [x] Principal: 8 a 2,5 de calor por disparo, máximo100; 13 a40 disparos consecutivos antes del bloqueo. Escopeta: variante con12,5 por perdigonada, ocho disparos hasta el bloqueo; conserva10 perdigones, cadencia0,85s, proyectil y daño. Enfriamiento20/s y recuperación35; retraso0,6s principal y1s escopeta. Parámetros editables en WeaponHeat.
- [x] Dos variantes nuevas: Weapon_IR_ArmModule_v07_ExtendedHeat y Weapon_Shotgun_HeatOnly. WeaponController dispone de una opción explícita, desactivada por defecto, para usar calor sin un segundo límite de munición. Ratio nativo de cápsulas, vapor y HUD sincronizado con calor. Recarga CylinderBankReload desactivada solo en la nueva escopeta; sin cartuchos ni aviso de recarga. Inventario y referencias de presentación actualizados en los siete laboratorios existentes; sin añadir escopeta donde no existía. Fuentes y armas robadas conservadas.
- [x] Primero WeaponHeatBalanceLab separado:1/1 prueba Play Mode. Después integración:3/3 aprobadas. M1 automático,40 disparos principal,8 perdigonadas/80 proyectiles escopeta, bloqueo, enfriamiento, disparo sin munición incluso fijada en cero, pausa sin enfriar, cambio de arma, acople Mounted y HUD visible/conectado en siete escenas. Captura de escopeta revisada. Conteos medidos con tiempo simulado a60Hz; no son medidas de FPS.
- [x] Compilación sin errores capturados. Respaldos: Tools/IronRequiem/Backups/20261107-heat-budget-shotgun. MainScene, manifest y packages-lock iguales por hash. No se probaron builds Player ni se cambiaron estadísticas de daño, cadencias o armas robadas.

## Nivel de dos plantas y grieta recuperable (07/11/2026)
- [x] Nueva escena CombatTwoFloorsLab: copia independiente de CombatTwoRoomsLab, dos plantas superpuestas a0/8m, escalera lateral de40 peldaños físicos (0,2m alto/0,8m fondo;8m ancho), rellanos amplios, columnas y coberturas. Conserva jugador, armas térmicas, menú, música, curación y oleadas del laboratorio fuente. Tres oleadas inferiores abren el acceso a la escalera; la ronda superior empieza después de cruzar la grieta. Escena añadida a Build Settings para conservar reinicio y vuelta al menú.
- [x] Grieta de4m a todo el ancho del piso superior. GapFallRecovery:20 de daño configurable mediante Health, regreso al banco desde el que se inició el salto, cinco destinos por lado comprobados con cápsula para evitar obstáculos. Cooldown0,75s contra múltiples colliders. No cura ni recrea el jugador/inventario. Respeta pausa, muerte e invulnerabilidad existentes; a cero vida funciona la muerte normal.
- [x] APIs mínimas de recuperación en PlayerCharacterController y AdvancedPlayerMovement: conservan cámara/inventario y eliminan velocidad de caída, impacto anterior, dash, slide y movimiento dirigido. Sin otro controlador ni cambios de velocidad o salto. Efectos de caída únicamente por Health; sin daño duplicado de aterrizaje.
- [x] NavMesh propio, radio2,25m, altura3,05m, escalón0,25m. Prueba inicial detectó entrada inferior demasiado estrecha para el diámetro del robot: ampliados rellano y giro, rebake con el mismo GUID. Ruta completa desde planta baja hasta la superior, manteniendo la grieta sin enlace automático. UV de copias de cubos adaptadas a los materiales compartidos del búnker; fuentes intactas.
- [x] Play Mode final4/4: movimiento real W por acceso y40 peldaños, salida superior, rutas NavMesh; Space+W cruza sin daño; caminar/correr sin saltar cae, resta exactamente20 por caída y recupera en el banco correcto desde ambos lados; inventario intacto, sin impulso residual ni daño extra; pausa bloquea recuperación. Oleadas inferiores6/4/5, apertura de puerta, espera antes del salto, cinco enemigos arriba y finalización. IA congelada en la prueba de flujo para aislar cantidades; no se afirma una partida completa manual ni FPS medidos.
- [x] Capturas revisadas y resultados en Tools/IronRequiem/Reports/TwoFloorGap. Respaldos en Tools/IronRequiem/Backups/20261107-two-floor-gap. MainScene, CombatTwoRoomsLab, manifest y packages-lock iguales por hash; no se instalaron paquetes. Compilación sin errores capturados. Escena nueva abierta fuera de Play para probarla.

## Sector03: persecución por pasillo (07/11/2026)
- [x] Escena independiente Assets/IronRequiem/Scenes/ChaseCorridorLab.unity, registrada en Build Settings. Pasillo de218m total y16m ancho, vestíbulo, puerta que se cierra al entrar, tres grietas de4m, coberturas laterales y salida segura. Tres sierras de6,2m, malla compartida de24 dientes,240rpm, avance8m/s tras aviso de2s. Sin collider complejo ni luz por sierra; daño de contacto mediante Health (100, intervalo0,35s), pausa/muerte/final detienen la persecución y motor. No esperar a matar enemigos para avanzar.
- [x] Reutilizados jugador, armas térmicas, interfaz, menú, música y cuatro pickups de vida de uso único. Cuatro T07 actuales y un SawBot en tres emboscadas, prewarm/activación mediante EnemySpawnQueue existente; posiciones validadas en NavMesh, distancia y cápsula. Omite emboscadas ya sobrepasadas en lugar de aparecer sobre el jugador. IA, armas, animaciones y estadísticas de los enemigos sin cambios.
- [x] Grietas mediante GapFallRecovery existente:20 de vida, retorno al banco desde el que se saltó, inventario preservado durante la caída; sierras siguen avanzando. No introducir otro controlador ni salud. NavMesh propio con bancos separados; no enlaces automáticos que eviten los saltos.
- [x] Puerta Sector03 en el cuarto superior de CombatTwoFloorsLab, accesible únicamente tras completar su ronda final; carga ChaseCorridorLab directamente en juego. Desplazado un punto general de aparición que coincidía exactamente con el de la torreta, de(0,8,14) a(-7,8,20), para evitar bloquear la finalización; se mantienen cantidades y composiciones. Cambios en escena mediante APIs del Editor, con respaldo.
- [x] Play Mode final5/5: recorrido real con W y tres saltos Space; cinco enemigos aparecen una vez; cierre de entrada, anticipación, avance de sierras, pausa, salida y parada del motor; captura del jugador quieto y menú de muerte; reinicio limpio y vuelta al menú; caída con20 de daño e inventario intacto; completar los dos cuartos y transición efectiva a persecución; tres semillas de aparición11/27/43 conservan disponible la torreta. IA congelada tras aparecer para aislar geometría/flujo; no se afirma una partida manual de combate ni rendimiento medido.
- [x] Capturas revisadas en Tools/IronRequiem/Reports/ChaseCorridor. Compilación sin errores capturados. MainScene, CombatTwoRoomsLab, manifest y packages-lock iguales por hash. Respaldos en Tools/IronRequiem/Backups/20261107-chase-corridor. Sin paquetes ni servicios de pago. La transición usa SceneManager.LoadScene y el jugador/inventario inicial del nuevo laboratorio; persistencia entre escenas no implementada en esta tarea. Reinicio de persecución conserva el flujo existente de CombatLabMenu.

## Trituradora y reparación visual/carga del T07 (07/11/2026)
- [x] ChaseCorridorLab usa dos rodillos horizontales sobre ejeX,19 anillos dentados intercalados y giro opuesto hacia la zona de trituración. Conserva malla/material compartidos y las tres sierras anteriores inactivas. Prefab de presentación GiantShredder. Frente de contacto ajustado a2,1m para coincidir con dientes de1,8m y cápsula del jugador; mismos8m/s,2s de aviso y daño. Sin colliders complejos ni luces añadidas.
- [x] Reproducción del T07 real: la variante usada por los tres niveles tenía EnemyChargeDuration=0; la variante CombatFollowup ya tenía1,2s pero no era la referenciada. Inspección de17 huesos, pesos y tres LOD; las piernas existían. Comparaciones de IK, bindposes y renderizado no justificaron sustituir animaciones ni rig. Blender confirmó bordes abiertos/superficies sin interior en las piernas.
- [x] Copia T07_LegInteriors.fbx y editable propio: tapas mecánicas de2mm desde los bordes medidos, con pesos interpolados normalizados, manteniendo UV y superficies exteriores. +69/+44/+23 triángulos por LOD; ninguna eliminación arbitraria de polígonos. Conservados fuente original, rig, animaciones, materiales y brazo desprendible. Variante T07_LegRepair_Charged_v05 conserva160HP, daño, proyectil, movimiento y cadencia actuales; restaura carga nativa1,2s y presentación magenta existente en la boca real.
- [x] Referencias de T07 actualizadas mediante APIs del Editor en ChaseCorridorLab, CombatTwoFloorsLab y CombatTwoRoomsLab, incluidas variaciones de oleadas. Los demás laboratorios inspeccionados no referencian esta variante antigua y conservan sus assets. MainScene y prefabs fuente sin cambios.
- [x] Play Mode3/3 sobre variantes: ejes horizontales, giro opuesto y pausa; tres LOD y estados70/45/20%HP, pérdida del brazo sin ocultar piernas; carga sin disparo prematuro, origen en boca, un proyectil, cancelación ante pared y muerte. Capturas abiertas y revisadas. Regresión integrada3/3: recorrido real con tres saltos y cinco enemigos, caída con20 de daño e inventario conservado, captura por trituradora, muerte, reinicio y menú. IA congelada en pruebas de recorrido; no se afirma una partida manual ni FPS medidos.
- [x] Compilación sin errores capturados. Respaldos: Tools/IronRequiem/Backups/20261107-shredder-t07. Originales T07_HitFeedback_v04 y CombatFollowup, MainScene, Build Settings, manifest y packages-lock iguales por hash. Herramientas: skills enemy-ai/modular-weapons, Blender local, APIs del Editor/TestRunner y sistemas URP/Animation Rigging existentes; sin paquetes ni créditos. Resultados y comparaciones en Tools/IronRequiem/Reports/ShredderT07Repair. El primer export diagnóstico sin caras añadidas se retiró de Assets tras respaldarlo; la entrega verificada incluye caras reales.

## Conexión de fases, visibilidad por cuarto y ejecución (07/11/2026)
- [x] CombatTwoFloorsLab y CombatTwoRoomsLab conectan sus dos arenas con ChaseCorridorLab. Puerta intermedia: espera mínima1,5s y prewarm terminado. Salida al corredor: carga asíncrona real, activación retenida y puerta cerrada al menos1,5s hasta progreso listo; aviso de preparación/listo. Menú y reinicio liberan la carga pendiente sin bloquear Unity.
- [x] LabRoomVisibility oculta renderers y luces del cuarto inactivo; conserva estructura compartida, escalera/pasaje, colliders, NavMesh, flags LOD, destrucción y jugador FPS. Cache al inicio/cambio de cuarto y registro en la cola existente; seguimiento de enemigos que crucen entre cuartos, sin búsquedas globales por frame. No se declara mejora de FPS medida; los contadores internos incluyen renderers precalentados/inactivos.
- [x] Fallo de E reproducido en Play: los T07 v05 y BigBot v10 referenciados no tenían EnemyExecutionSource. Variantes en Prefabs/PhaseFlow con los módulos ya existentes; referencias sustituidas en las oleadas/variaciones/emboscadas de los tres laboratorios. Sin modificar estadísticas, modelos originales ni entrada. Uniones del T07 enrutan daño a Health real; suprimidos drops automáticos recogibles en estas variantes. SawBot conserva su sistema; dron/torreta no tienen módulo compatible.
- [x] Play Mode final5/5, además de reproducción previa1/1: ambos recorridos completos con conteos de oleadas, puertas y transición real al corredor; E al70%, bloqueo al75% exacto, distancia/pared/arma ya sostenida, entrega y muerte únicas ante E repetida, energía completa y muerte normal sin drop; visibilidad de planta superior y enemigo que cambia de cuarto; salto de ejecución no cambia de planta; menú/reinicio durante precarga. IA congelada en pruebas de flujo/interacción para aislarlas; no partida manual ni benchmark de FPS. Compilación y referencias revisadas. Resultados en Tools/IronRequiem/Reports/PhaseFlow; respaldos en Tools/IronRequiem/Backups/20261107-phase-execution.
- [x] MainScene, Build Settings, manifest y packages-lock iguales por hash. Originales conservados; sin paquetes, créditos ni control del escritorio. La transición mantiene la creación del jugador/inventario inicial del corredor; persistencia de inventario entre escenas sigue pendiente y no se añadió en esta corrección.

## Campaña principal por sectores (08/11/2026)
- [x] IntroMenu/JUGAR dirige a RequiemCampaign. Cuatro cuartos con tres rondas cada uno: fundición, planta superior con grieta, montaje y reactor; después, persecución original adaptada como sector05. Escenas propias en Scenes/Campaign, sin sobrescribir MainScene ni los laboratorios fuente.
- [x] Carga aditiva real: esclusas de dos puertas, cierre tras entrar, descarga completa del cuarto anterior antes de cargar/precalentar el siguiente y espera mínima1,5s. Solo una escena de combate cargada, más la sesión/esclusas persistentes. Mismo jugador, salud, inventario y energía; restos y drops del cuarto anterior se retiran al descargarlo. Reinicio vuelve al primer cuarto.
- [x] Variantes actuales en Prefabs/Campaign: T07 NavThrottle/VO con piernas reparadas en3LOD; BigBot NavThrottle/VO con ráfaga de6; dron y torreta con IA/alcance actuales y materiales VO; SawBot SawPolish v04. Compartidos los materiales HardSurface del trabajo visual existente. Sin cambios a estadísticas globales, controles ni paquetes.
- [x] Luz ambiental.50/.55/.60, luminarias4,4 sin sombras adicionales, montadas contra techos; retiradas las tiras flotantes heredadas de la persecución. Una reflexión horneada128 por sector. Ojos/indicadores emisivos compartidos, sin luz dinámica por robot; torreta usa su ranura de ojos sin ocultar todo el cuerpo al morir.
- [x] Armas obtenidas por E únicamente con vida estrictamente menor al75% en T07/BigBot/SawBot. Sin armas recogibles automáticas por daño crítico/muerte; conserva roturas decorativas y armas ya ganadas/lanzadas. Dron y torreta no tienen módulo compatible. Corregida limpieza de inventario equipado en escenas aditivas.
- [x] Cuatro casos Play Mode aprobados: doce rondas y conteos, cuatro transiciones con una sola escena/nativos anteriores destruidos, inventario/energía persistentes, recorrido real por40 escalones, salto de grieta superior y tres saltos en persecución con5 enemigos; entrada desde menú/reinicio/vuelta durante carga; cinco familias con ojos/apagado/umbral75% y sin drops; agotamiento equipado con detonación única e inventario limpio. IA congelada en pruebas de flujo para aislar el nivel. Capturas1280x720 revisadas; sin benchmark nuevo de FPS/Player. Una aserción intentaba detonar de nuevo tras agotamiento síncrono: corregida y repetida con éxito.
- [x] Compilación sin errores; hashes de MainScene, tres laboratorios fuente, manifest y packages-lock conservados. Build Settings incluye las seis escenas nuevas. Respaldos en Tools/IronRequiem/Backups/20261108-campaign; resultados en Reports/Campaign. Sin instalaciones, créditos ni control del escritorio.

## Luces, armas vigentes y limpieza visual (08/11/2026)
- [x] Indicadores manuales BigBot conservan posiciones, rotaciones y escalas locales exactas; portadores vinculados al torso. Superficies biseladas con máscara y material emisivo compartido; sin luces dinámicas nuevas. Ojo T07 transferido a la rama de campaña: esfera heredaba escala100 y tenía diámetro100m, collider50m y material sin emisión. Malla derivada normalizada a65mm, collider auxiliar retirado; transformaciones del marcador intactas.
- [x] Campaña conserva luminarias y sombras; ambiente.55/.60/.65, reflexión1.1; copia del Volume existente con exposición+.30EV, contraste3, bloom.08/umbral1.1 y grano desactivado. MainScene original y paquetes intactos por hash.
- [x] Restauradas Weapon_IR_ArmCannon_VO y Weapon_Shotgun_ArmModule_VO en StartingWeapons y referencias de ProtagonistHandsPresentation. La campaña solo equipaba el arma anterior. Escopeta derivada con un cañón, tubo inferior, receptor y guardamanos biselados; conserva cápsulas, animaciones, boca y parámetros mecánicos verificados iguales por comparación de serialización.
- [x] T07 cuerpo y brazo, tres LOD: derivados con pesos rígidos por cara, superficies originales y tapas en articulaciones. Muestreo de todos los clips en LOD0: cuerpo máximo1.007x y brazo1.001x frente a27.407x/343.407x anteriores. Corregida escritura de buffers al actualizar Mesh manteniendo GUID; captura posterior sin picos. BigBot y Saw mantienen rig rígido; dron y torreta conservan clips originales.
- [x] Inventario de referencias serializadas/código/Resources/herramientas/pruebas y comprobación nativa AssetDatabase.GetDependencies. Eliminados20 assets mediante AssetDatabase.DeleteAsset:19 materiales y1 textura. Respaldo exacto con .meta/GUID/SHA y Restore.ps1 en Tools/IronRequiem/Backups/20261108-visual-repair/DeletedAssets. Lista: Tools/IronRequiem/Reports/VisualRepair/deleted-assets.txt. Conservadas19 texturas por dependencias nativas; ningún modelo/prefab/escena confirmado obsoleto. Malla original Assets/IronRequiem/Art/VisualOverhaul/Meshes/VO_ArmShotgun_Body.asset conservada como fuente del rediseño.
- [x] Play base y primera revisión final: campaña real1280x720, ambas armas, entradaM1, ADS, cambio, ocultación de mano y apagado de ojos al morir. IA congelada para capturas; clips muestreados manualmente, no prueba completa de combate. Capturas antes/después en Reports/VisualRepair/Shots. Primera prueba bloqueada por playModeStartScene: TestRunner ahora lo limpia temporalmente y lo restaura.
- [ ] Revisión manual de cuatro contornos complejos del brazo derecho LOD0; no afirmar cierre completo de todas las uniones. Sin benchmark nuevo de FPS. Resultado de repetición final con buffers corregidos en Reports/VisualOverhaul/req-vr-play-mesh-fixed.txt.
- [x] Repetición final con mallas corregidas aprobada1/1 en Play Mode (16s): ambas armas, M1, ADS, cambio, mano oculta, clips y apagado al morir. Sin guardar estado de ejecución.

## T07: corrección de lente demasiado luminosa (08/11/2026)
- [x] Lente propia URP Unlit sin HDR, diámetro45mm calculado desde esfera base nueva; conserva posición y hueso. Colores de estado limitados a0.9. Prefabs T07_LegRepair_Charged_v05 y Campaign/T07_Current_Repaired; respaldo Tools/IronRequiem/Backups/20261108-t07-eye.
- [x] Play1/1 T07EyeRegressionTests con EnemyController/IA activos: aparición, tamaño de lente menor a0.1m, gradiente corporal apagado en reposo y restauración tras daño. Captura t07-spawn-fixed.png. No se reprodujo aura gigante en la campaña con esta variante; el ojo anterior medía65mm, no100m. No atribuir el fallo actual a escala gigante sin otra evidencia. Sin cambios de combate/animaciones/escenas.

## T07 resplandor excesivo y ojos rojos T07/Saw (08/11/2026)
- [x] Inspección real: flash embebido T07 de campaña escala3.69 (frente a.75 en la rama fuente), playOnAwake activo; DamageSparks/BodyDamageSmoke/CriticalSmoke heredaban100 del rig. La revisión anterior del ojo no cubría estos efectos.
- [x] Prefab propio T07_MuzzleFlash_Bounded: escala.22, vida.08s, máximo8 partículas, tamaño de pantalla.12; fuente embebida desactivada y referencia externa disparada mediante WeaponController existente. Tres emisores del rig normalizados a escala mundial1 conservando sus anclajes y reglas. Generador VO ya no multiplica repetidamente el flash del T07.
- [x] Material compartido rojo URP Unlit sin HDR para esfera guardada del T07 y CoreIndicator existente del SawBot; estados siguen rojos y muerte los apaga. Retirado solo render del indicador auxiliar cian de frente T07. IA, cadencias, daño, carga y escenas intactos. Respaldo Tools/IronRequiem/Backups/20261108-t07-glare-red-eyes.
- [x] Play1/1 aprobado (17s): IA activa, cargas y disparos reales confirmados por OnShootProcessed; daño55% y22% cruza tres umbrales, VFX del rig permanecen a escala1; esfera T07 y CoreIndicator Saw usan rojo. Capturadas9 fases de combate; revisadas aparición, carga y estado crítico, sin el resplandor gigante. Resultado Reports/VisualOverhaul/req-t07-glare-runtime.txt.

## Pase visual F0 auditoría y F1 C1/C2 (08/11/2026)
- [x] F0: auditoría sin cambios en Docs/IronRequiem/VISUAL_AUDIT_F0.md. Capturas en Tools/IronRequiem/Reports/VisualPass/F0. Play Mode real en Room01 (Editor, no Player): 58-60 FPS en calma y picos de 20 FPS (50 ms de CPU) en la aparición de la ronda. GPU por debajo de 2 ms, así que el límite está en la CPU. Benchmark Player no ejecutado.
- [x] C1, resplandor blanco gigante: reproducido en la campaña con enemigos (T-07, Saw Bot). Diagnóstico con GlareWatcher y Bloom desactivado en runtime: sin Bloom desaparece, así que es un píxel NaN/HDR extremo propagado por el Bloom (intensidad .1, umbral .9, clamp 65472). Arreglo: StopNaN en Main Camera y WeaponCamera de RequiemCampaign; perfil copiado a Assets/IronRequiem/Art/VisualPass/Campaign_PostProfile.asset con clamp de Bloom 12, asignado solo al Volume de campaña. Assets/Rendering/PostProcessingProfile.asset (compartido con MainScene) queda intacto. Además: maxParticleSize de chispas .5→.04 y startSize ≤.12 en los cinco prefabs Campaign/*_Current (Saw HitSparks se emitía delante de la cámara).
- [x] C2, pantalla roja completa: FlashImage del GameHUD no tenía sprite (rojo sólido a pantalla completa con alfa .7). Override solo en la instancia de RequiemCampaign: sprite de viñeta de la plantilla (Transparent_Center), alfa .32, color (.6,.015,.01), duración .18, viñeta crítica .5 y pulso 4 Hz. GameHUD.prefab de la plantilla sin cambios.
- [x] Verificación: Play Mode con IA activa, jugador invulnerable solo en runtime para observar. Antes: f1-before-glare.jpg. Después: f1-after-glare-fixed.jpg y f1-after-sawbot-close.jpg (T-07, Saw Bot y dron legibles a corta distancia). Diagnóstico: f1-diag-bloom-off.jpg. Capturas en Tools/IronRequiem/Reports/VisualPass/F1. Sin errores nuevos en la consola. No se midió FPS Player.
- [x] Herramientas: Assets/IronRequiem/Editor/VisualPass/VisualPassF1.cs (menú Tools > Iron Requiem > Visual Pass). Los Diag son solo de runtime y no guardan nada. Respaldos en Tools/IronRequiem/Backups/20261108-visualpass-f1 (escena, metas y prefabs de enemigos de campaña). Informes en Tools/IronRequiem/Reports/VisualPass/F1/*.txt.
- [ ] C3 pendiente (jugabilidad, sin tocar): los enemigos aparecen encima del jugador (el Saw Bot entra en contacto al iniciar la ronda) y la muerte llega en unos 3 s. Requiere autorización.
- [x] F1 luz por sector (08/11, 23:00): las cinco escenas de Campaign pasan de ambiente Flat .55/.60/.65 a Trilight por sector (cielo, ecuador y suelo; suelo = ecuador para que se lean los techos interiores), con niebla Exp2 (.015-.022). Las CeilingLight existentes cambian solo color e intensidad (6-6,5); sin luces ni sombras nuevas. Grade propio por sala con un Volume global VisualPass_SectorGrade (prioridad 10) y perfiles Art/VisualPass/Sector_<escena>.asset (ColorAdjustments + WhiteBalance). Identidad: Fundición cálida, Brecha acero frío, Montaje ámbar neutro, Reactor verde azulado, Persecución alarma roja.
- [x] Verificación en Play Mode con salto de sector diagnóstico (runtime, nada guardado): los cinco sectores capturados en Reports/VisualPass/F1/l1-*.jpg; comparación en f1-lighting-sectors.jpg (arriba a la izquierda, el antes). GPU sin cambios (1,6-1,8 ms) y FPS del Editor iguales: el coste es nulo frente al límite de CPU. Respaldos en Backups/20261108-visualpass-f1-lighting. Herramienta: Editor/VisualPass/VisualPassF1Lighting.cs. L2 (vistas previas en Edit Mode) no es fiable porque las paredes solo aparecen en runtime; no usarla para aprobar.
- [ ] Pendientes F1: las reflection probes horneadas (128 px) siguen con la luz anterior y habría que rehornearlas; el arma VO todavía se ve muy clara (revisar en F5); la aparición de enemigos encima del jugador (C3) sigue sin tocar.

## Pase visual F2 materiales y entorno, primera parte (09/11/2026)
- [x] Texturas nuevas generadas por código (tileables de 1024 px, sin assets externos) en Art/VisualPass/Textures:
  - CP_WallPanels: paneles de acero con remaches, nervio con acento naranja, óxido y suciedad en la parte baja.
  - CP_FloorPlate: chapa de rombos con placas, pernos y zonas pulidas.
  - CP_CeilingRibs: chapa corrugada con vigas oxidadas.
  - CP_Lava: costra con grietas emisivas.
  - Cada una con albedo, normal y máscara (G desgaste, B oclusión).
- [x] Materiales CP_Wall, CP_Floor y CP_Ceiling (shader IronRequiem/HardSurface en triplanar de mundo a 3, 2 y 3 m, con normal, suciedad y desgaste por máscara). Se reemplazaron 58 materiales VO_Env_Wall/Floor/Ceiling y Bunker_Ceiling, solo en las cinco escenas Campaign; VO_Env_*, los labs y la plantilla siguen intactos. Herramienta: Editor/VisualPass/VisualPassF2.cs (M1). Respaldos en Backups/20261109-visualpass-f2.
- [x] Pedido del usuario, vacíos hacia fuera del mapa: las escenas de sala ya no tienen skybox, la Main Camera de RequiemCampaign limpia a casi negro (.012,.012,.016) y cada sala tiene VisualPass_VoidShell, una carcasa interior de acero oscuro (CP_VoidShell) a 8 m de su geometría (6 quads, sin colisión ni sombras, estáticos).
- [x] Pedido del usuario, lava en las caídas: VisualPass_LavaPools con CP_Lava emisiva (HDR 3/.8/.12, pulso .35 Hz), un quad a media altura de cada GapFallRecovery (Room02 en y=4,8; ChaseGap_48/98/148 en y=4,0). Sin luces dinámicas. Comprobado en Scene view durante Play; no capturé la lava desde la caída en primera persona porque la recuperación te devuelve antes.
- [x] Verificación: Play Mode en Room01 y Room02 (f2-room01-before-after.jpg, f2-floor-closeup.jpg, f2-room02-materials.jpg). Sin errores de consola. GPU sin cambio apreciable.
- [ ] Pendiente F2: el techo sigue casi negro (subir el tinte de CP_Ceiling o añadir luminarias emisivas). Faltan dressing (tuberías, cables, señalética, decals) y trituradoras más amenazantes en Pursuit05 (chispas y luz de aviso emisiva). Verificar las carcasas en vivo mirando por las aberturas.
- [x] F2 D1, pedido del usuario (09/11, 01:15):
  - Umbral de acero de 18 cm (VisualPass_DoorSill, VO_Env_RustMetal, con colisión) bajo las 9 puertas de esclusa de PersistentAirlocks. Cierra la separación entre el piso y la puerta (Room02EntryDoor y las demás).
  - Puertas animadas: nuevo Scripts/VisualPass/AirlockDoorMotion.cs. Suben al techo en 0,7 s y bajan en 0,45 s; al terminar de abrirse la puerta queda inactiva y al cerrarse tiene colisión desde el primer frame.
  - CampaignFlow.cs: se cambiaron 3 líneas de SetActive por AirlockDoorMotion.Set. El original está en Backups/20261109-visualpass-f2/CampaignFlow.cs.bak.
- [x] Luz rara al cambiar de sala: durante la carga, la escena activa pasaba a la sesión (Flat .55 brillante). Ahora la sesión usa Trilight neutro tenue con niebla, CampaignSession tiene SectorLightingBlend (interpola ambiente y niebla durante 1,4 s al cambiar la escena activa) y cada VisualPass_SectorGrade tiene VolumeFadeIn (sube el peso del grade de 0 a 1 en 1,2 s; usa reflexión porque el asmdef runtime no referencia SRP Core).
- [x] CP_Ceiling con tinte 2,2: los techos ya se leen.
- [x] Verificación en Play: sin errores ni warnings, la puerta inicial se abre, y el salto de sector carga sin destello. No capturé un frame a mitad de la animación de la puerta ni el umbral de Room02 desde el juego.
- [ ] Riesgo: las pruebas EditMode/PlayMode antiguas que comprueban activeSelf de una puerta justo después de la transición pueden necesitar esperar 0,7 s.
- [x] F2 S1, decorado y trituradoras (09/11, 01:50):
  - VisualPass_Dressing en Room01-04 (45-52 objetos por sala, estáticos para batching, sin colisión ni sombras, sin luces): dos tuberías por pared larga con abrazaderas cada 4 m, bandeja de cables, vigas transversales en el techo cada 6 m, bajantes en las esquinas y letrero TMP «SECTOR 0N» + nombre en la pared oeste. Los tramos de la pared este se cortan en las puertas (Room01 z 17-33, Room02 z -16/-4).
  - Pursuit05: chispas (ParticleSystem, 45/s, maxParticleSize .04, material Spark de la plantilla) en las 3 GiantSaw y balizas emisivas rojas (CP_WarningStrobe con pulso de 2,4 Hz) sobre ShredderSideFrame y MachineFrame.
  - CP_Ceiling ajustado (metálico .1, suavidad .08, tinte 1,7) para quitar el brillo del reflejo horneado antiguo; CP_VoidShell más oscuro y mate.
- [x] Verificación en Play: f2-room01-f1-vs-f2.jpg (vigas y tuberías, techo legible) y f2-s1-pursuit.jpg. Sin errores. Nota: el diagnóstico de salto deja al jugador cerca del techo en Room04 (sube por algo bajo las lámparas); es solo un artefacto del diagnóstico, no del juego.
- [ ] No verificado de cerca: las chispas y balizas de las trituradoras en movimiento; el letrero puede necesitar otra fuente si faltan glifos (Ó) en «LiberationSans SDF - Fallback».

## Pase visual F3 VFX de combate, primera parte (09/11/2026)
- [x] A0, auditoría de solo lectura: Reports/VisualPass/F1/f3-a0-vfx-audit.txt recorre las referencias de VFX de las dos armas VO y los cinco enemigos de campaña. Hallazgo: los disparos del jugador usaban el fogonazo verde de la plantilla y chispas verdes (principal) o azules (escopeta), contra la regla de paleta (jugador = naranja).
- [x] V1, disparos naranjas: nuevas copias en Prefabs/VisualPass/VFX:
  - VP_Muzzle_Primary/Shotgun, VP_Impact_Primary/Shotgun y VP_Bolt_Primary/Shotgun.
  - Color HDR de blanco a naranja con colorOverLifetime de fuego; maxParticleSize limitado (.12/.16 fogonazos, .05/.045 impactos).
  - Los materiales Lazer_Green/Lazer_Blue pasan a Spark neutro.
  - Los proyectiles apuntan a los nuevos impactos.
  - Weapon_IR_ArmCannon_VO y Weapon_Shotgun_ArmModule_VO usan los nuevos fogonazos y proyectiles; respaldo en Backups/20261109-visualpass-f3.
  - La plantilla FPS no se modifica.
- [x] Verificación en Play: f3-impact-before-after.jpg (impacto verde frente a blanco-naranja). Sin errores.
- [ ] Pendiente F3: explosiones (onda y debris), vapor y brillo del cañón al enfriar (ya existen VFX_OverheatPrimary y VO_PortSteam, falta revisarlos), arco eléctrico al ejecutar y extraer, OVERDRIVE en pantalla, y comprobar el fogonazo naranja en primera persona.

## Visual Pass F6 (parcial) — HUD industrial (2026-11-09)
- Herramienta: `Assets/IronRequiem/Editor/VisualPass/VisualPassF6.cs` (menú Tools/Iron Requiem/Visual Pass/F6 …). Runtime nuevo: `Scripts/VisualPass/HudBannerPlate.cs`, `HudHealthTint.cs` (cero alloc, sin luces).
- Prefabs nuevos (plantilla FPS intacta): `Prefabs/VisualPass/UI/VP_AmmoCounter.prefab`, `VP_NotificationToast.prefab`.
- RequiemCampaign (ambos GameHUD: GameManager/GameHUD y GameHUD): WeaponHUDManager.AmmoCounterPrefab y NotificationHUDManager.NotificationPrefab → copias VP (override de instancia).
- Salud: marco IndustrialPanelGraphic, etiqueta INTEGRIDAD, relleno naranja → rojo; pulso suave <25 %.
- Cartel sector/ronda (ArenaMessages/Message): bajo la brújula (y −112), placa industrial, punch al cambiar texto. Antes: y −25, tamaño 1100×90, tapaba brújula.
- Brújula fondo oscuro + línea naranja; pista pausa "PAUSA / OPCIONES".
- `FPS/Scripts/UI/NotificationHUDManager.cs`: textos ES ("ARMA OBTENIDA · ", "JETPACK DESBLOQUEADO"). Backup `Tools/IronRequiem/Backups/20261109-visualpass-f6/`.
- Verificado Play Mode Room01: sin solapes, toast estilizado, consola sin errores nuevos. Pendiente: verificar pulso vida baja, menú pausa, benchmark.

## Visual Pass F3 (cierre) + F6 menú/pausa (2026-11-09)
- Herramienta: `Editor/VisualPass/VisualPassF3b.cs` (F3 V2, V2b, Diag heat fx). Runtime nuevo en `Scripts/VisualPass/`: `ExtractionArcFx.cs`, `HeatVentFx.cs`, `OverdriveScreenFx.cs`, `MenuShellMotion.cs`. Todo sin luces, rigs construidos una vez, cero alloc por frame.
- Ejecución/extracción: `ExtractionArcFx` en Player_ModularArm_v07 (RequiemCampaign). Escucha `StolenWeaponCarrier.Executed`; en el tirón dispara arco eléctrico (2 LineRenderer), chispas y trozos metálicos. Pool 2 rigs (DontDestroyOnLoad).
- Calor cañón: `HeatVentFx` en Weapon_IR_ArmCannon_VO y Weapon_Shotgun_ArmModule_VO. Vapor desde las celdas al enfriar o sobrecalentar (visible en vista). El brillo lo dan las celdas rojas existentes (PrimaryModuleHeatPresentation). El glow en boca queda solo si el arma no tiene celdas (boca tapada por celdas en vista).
- OVERDRIVE: `VP_OverdriveEdge` (sprite vignette de bordes) en ambos FeedbackFlashCanvas; pulso naranja más rápido cuanto menos queda; centro de vista libre.
- Explosión de módulo: copias `Prefabs/VisualPass/VFX/VP_CannonExplosion_IR.prefab` y `VP_VFX_HoverBot_Explosion.prefab` con debris metálico (mesh particles, colisión baja). `CannonExplosion.impactEffect` repuntado en 15 prefabs IR (Campaign + labs). Backups en `Tools/IronRequiem/Backups/20261109-visualpass-f3b/`. La onda, el destello acotado (máx. 2 luces, 0.12 s) y el shake ya existían en `CannonExplosionPresentation`.
- Menú intro + pausa + CombatLabMenu: `MenuShellMotion` en IndustrialShell; entrada con fade y escala, vetas del casco en movimiento, fade/slide al cambiar de subpanel. Barrido de escaneo probado y retirado (lavaba el panel).
- Verificado en Play: vapor al enfriar, pausa y Opciones con transición, consola sin errores nuevos.
- Sin verificar en Play: arco de ejecución, borde OVERDRIVE y debris de explosión (requieren combate real).
- Hallazgo: el WeaponController de la plantilla instancia/destruye muzzle flash por disparo (~20 vivos a la vez). No es pooling. Pendiente para F4 o rendimiento.

## Visual Pass F4 — Game feel (2026-11-09)
- Herramienta: `Editor/VisualPass/VisualPassF4.cs` (Game feel, Damage flash on all HUDs, Diag damage arcs). Runtime nuevo en `Scripts/VisualPass/`: `GameFeel.cs` (escala de temblor persistida + hitstop seguro con pausa), `HeavyDeathFeel.cs`, `ShakeSliderBinding.cs`, `DamageDirectionHud.cs`, `DamageWedgeGraphic.cs`.
- Temblor opcional: fila "TEMBLOR DE PANTALLA" (0–100 %, PlayerPrefs `IR_ScreenShake`) en Opciones de intro, pausa y CombatLabMenu. `DistanceCameraImpulse` escala temblor y kicks con esa opción.
- Hitstop: ejecución 0,06 s (ExtractionArcFx). Muertes Big Bot / Saw Bot (`HeavyDeathFeel` en `BigBot_Current` y `SawBot_Current`): hitstop 0,09 / 0,07 s, temblor y kick según distancia. No actúa si el juego está en pausa o en cámara lenta.
- Daño direccional: 4 arcos pooled alrededor de la mira (radio 140 px a 1080p) que siguen al atacante y se desvanecen en 0,85 s. Están en ambos GameHUD/HUD.
- Flash de daño: el segundo GameHUD (raíz `GameHUD`) tenía flash plano al 70 % y viñeta crítica al 100 %, que saturaban la pantalla de rojo. Ahora ambos HUD usan sprite de viñeta, alpha 0,32, crítico 0,5 y color (0,6; 0,015; 0,01).
- Restos: armadura y brazos persisten hasta descargar el sector (`RobotArmorDebris.PersistSeconds` = 600; `RobotDestructionPool` recicla el más viejo y limpia al `sceneUnloaded`).
- Fogonazos: `WeaponController` (plantilla FPS) usa un pool de 6 por arma en vez de Instantiate/Destroy por disparo.
- Ya existían: hit flash de enemigos (`RobotHitFeedback`) y aviso de vida baja (`HudHealthTint` + viñeta crítica).
- Backups: `Tools/IronRequiem/Backups/20261109-visualpass-f4/` (DistanceCameraImpulse, RobotArmorDebris, RobotDestructionPool, WeaponController, prefabs BigBot/SawBot).
- Verificado en Play (Room01): slider de temblor, arco de daño, sin pantallazo rojo, consola sin errores nuevos.
- Sin verificar en Play: hitstop de muertes pesadas y de ejecución, y persistencia de restos. Benchmark pendiente.

## Visual Pass F5 (parcial) — Enemigos y viewmodel (2026-11-09)
- Herramienta: `Editor/VisualPass/VisualPassF5.cs` (Audit viewmodel materials, Viewmodel arm material).
- Estados de daño 75/50/25 % (`RobotDamageProgression.cs`, backup en `Backups/20261109-visualpass-f5/`):
  - ráfaga de chispas al cruzar cada umbral (10/18/28) y humo inicial;
  - chispas continuas 4/9/20 por segundo (antes 2/5/12);
  - humo más grande y oscuro en 50 % y 25 %.
- Viewmodel: `IR_Protagonist_URP` estaba metálico 1 / suavidad 1 (espejo que quemaba a blanco con las luces de sector). Copia `Art/VisualPass/IR_Protagonist_VP_FPS.mat` (suavidad 0,55 con mapa metálico, base 0,8). Se aplica solo a los renderers FirstPersonWeapon del jugador en RequiemCampaign: antebrazos y manos FPS, Forearm_R_Receiver y SlideLegs_LOD0. El cuerpo en tercera persona queda igual.
- Revisado: el cuerpo tiene LODGroup de 3 niveles, así que LOD0/1/2 no se dibujan a la vez.
- Pendiente (necesita decidir con el usuario o trabajo de animación/modelo): los 4 contornos del brazo T-07 LOD0, la unión antebrazo-arma, el puñetazo izquierdo, el slide, las piernas y el clipping con cámara (near 0,1 en ambas cámaras).
- Sin verificar en Play: las ráfagas de daño, porque morí antes de dañar enemigos.

## Visual Pass F5b — Feedback del usuario: piernas, cambio de arma, golpe (2026-11-09)
- Piernas del slide (`ProtagonistSlidePresentation.cs`): patada de entrada de la pierna delantera (0,18 s), vibración por fricción según velocidad, bombeo de la rodilla recogida y balanceo de cadera. Backup en `Backups/20261109-visualpass-f5/`.
- Cambio de arma (`Scripts/VisualPass/WeaponDeployMotion.cs` en Weapon_IR_ArmCannon_VO y Weapon_Shotgun_ArmModule_VO): todo el módulo (celdas, cuerpo y cañón juntos) sale retraído (escala 0,72/0,72/0,12), se expande con overshoot en 0,28 s y con un giro corto. Así los tubos ya no aparecen antes que el resto. Backups de los prefabs.
- Golpe (`ProtagonistHandsPresentation.cs`, `straightJab`): trayectoria recta guardia → recarga → extensión al centro de la vista (antes subía como uppercut). Los kicks de cámara de `ProtagonistMelee` en la escena pasan a windUp (-0,3; -0,5; 0,5), impacto (1,3; 0,25; -0,7) y fallo (0,5; 0,15; -0,3); antes eran (-1,1; -0,6; 1,4), (2,6; 0,9; -2,4) y (0,9; 0,5; -1,1).
- Diag nuevo: F5 "Diag slow motion toggle (play)".
- Verificado en Play: compila, el cambio de arma y el golpe funcionan sin errores. Las transiciones son demasiado rápidas para capturarlas por pantalla, así que falta la revisión visual del usuario.
- Pendiente: "el brazo queda raro con el cañón". Falta definir qué parte (unión, ángulo o largo del antebrazo).

## Música de campaña + armas robadas + puente de órdenes (2026-11-09)
- Música: `RequiemCampaign` nunca tuvo música de juego; `CombatMusicAuthoring` solo la añadía a MainScene y al laboratorio. Nuevo objeto `CampaignMusic` (CombatMusic con IR_Combat01_Explore / IR_Combat01_Loop, grupo Music, `m_LabMenu` = CombatLabMenu). Verificado con F5 Diag audio: exploración sonando a 0,8 en Room01.
- Armas robadas: los materiales de los robots eran metal espejo (metálico 1 / suavidad 1) y en primera persona se veían negros.
  - Copias FPS en `Art/VisualPass/StolenFPS/` (T07_URP, SawBot_URP, BigBot_Surface, WeaponPistol: metálico 0,35, suavidad 0,42, sin mapa metálico).
  - `ExposedMechanism_FPS` (gris trabajado con brillo naranja), solo en Weapon_T07_Temporary_Optimized.
  - Aplicado solo a los prefabs de arma robada: Weapon_T07_Temporary_Optimized, Weapon_SawBot_Animated_v02 y BigBot_PlayerWeapon. Los enemigos no cambian. Backups en `Backups/20261109-visualpass-f5/`.
- Pendiente: el visual del cañón T-07 robado (`StolenCannonVisual`, 0,63 × 0,59 × 0,75 m) es la malla del brazo del enemigo, demasiado grande y vista desde el extremo arrancado. Hay que escalarla y reposicionarla. Saw Bot y Big Bot sin capturas en mano todavía.
- Puente de órdenes `Editor/VisualPass/IRCommandBridge.cs`: lee órdenes de `Tools/IronRequiem/Bridge/command.txt` y solo acepta una lista fija: refresh, menús de Tools/Iron Requiem, play, stop, wait, click de botón, timescale, screenshot y clearlog. Escribe los resultados en `result.txt` y `console.txt`.
- Diags nuevos F5: audio, armas robadas (prefabs y materiales), robo desde enemigo (tipo en `Bridge/steal_type.txt`), renderers del arma en mano.
- Armas robadas (opción 2 del usuario): F5 "Stolen cannons use VO shell (robot tint)". En Weapon_T07_Temporary_Optimized y BigBot_PlayerWeapon se ocultan las mallas Lit del brazo enemigo. Se añade `VP_StolenShell`: copia solo de mallas del cañón VO, alineada al Attachment o muzzle, con materiales tintados en `StolenFPS/*_{T07,BigBot}Stolen.mat` (T-07 gris-lila con emisión magenta, Big Bot bronce con emisión naranja). La sierra del Saw Bot conserva su modelo con materiales mates. Backups `*.shell.bak`. T-07 verificado con robo real en Play (captura `Bridge/shell_t07.png`); Big Bot pendiente de ver en Play.
- Ajuste al antebrazo (feedback: "algunas armas quedan por encima"): `Scripts/VisualPass/StolenShellFit.cs` en cada `VP_StolenShell`.
  - En LateUpdate (orden 200, después de ProtagonistHandsPresentation) coloca la carcasa en `RightModuleSocket`, el mismo punto donde se encaja el cañón VO.
  - Offset calculado desde la boca del VO + `mountedWristOffset` (0; -0,03; -0,42).
  - Solo actúa cuando el antebrazo está reemplazado.
  - Aplicado a Weapon_T07_Temporary_Optimized y BigBot_PlayerWeapon (F5 "Stolen shells fit forearm socket").
  - Falta verificarlo visualmente en juego: la captura automática salió durante la animación de ejecución.

## Fusión de armas robadas + tubos del arma principal + balance (2026-11-10)
- Armas robadas: `StolenShellFit` coloca la carcasa por geometría (cara trasera sobre la punta del antebrazo, centrada, 2 cm montada). Animación de fusión de 0,42 s al equipar: flota delante → absorción → encaje con chispas, kick de cámara y hitstop de 0,035 s. El usuario confirmó que se ve bien.
- Tubos celestes (celdas de combustible) del arma principal: estaban fuera de GunRoot y aparecían enteros antes que el resto. `WeaponDeployMotion` ahora escala el arma completa (raíz del prefab), así que las celdas salen metidas junto con las demás piezas.
- Balance (`Editor/VisualPass/VisualPassBalance.cs`, F7 Apply; backups en `Tools/IronRequiem/Backups/20261110-balance/`):
  - Drones (HoverBot_Current): disparo cada 1,5 s (antes 0,8), daño 4 (antes 8), proyectil a 70 (antes 100). Proyectil propio `Prefabs/VisualPass/Balance/EnemyProjectile_Drone`.
  - T-07 (T07_Current_Repaired): francotirador. Daño 22 (cargado 18–26; antes 8), proyectil a 90 (antes 40), detección 45 m (antes 16), ataque 38 m (antes 12,5), memoria del objetivo 6 s (antes 2). Mantiene el aviso de carga de 1,2 s. Proyectil propio `T07_SniperProjectile`.
  - Saw Bot (SawBot_Current): velocidad 9,5 (antes 7,2), aceleración 34 (antes 24), preparación 0,2 (antes 0,25), recuperación 0,55 (antes 0,75).
  - Big Bot sin cambios.
- Verificado con F7 Audit: valores aplicados en los prefabs. Pendiente de prueba jugando.
- Tubos al cambiar de arma (causa real, vista en cámara lenta con F5 "Diag switch weapon" + timescale 0,05):
  - El PlayerWeaponsManager de la plantilla baja el arma fuera de pantalla (DownWeaponPosition) y sube la nueva desde abajo. Los tubos, que van arriba del arma, eran lo primero en asomar y lo último en irse.
  - Fix (F7 "Weapon swap in place"): en RequiemCampaign `DownWeaponPosition` = `DefaultWeaponPosition` y `WeaponSwitchDelay` 0,2 → 0,12. El arma cambia en su sitio y la nueva sale compacta y se expande completa (`WeaponDeployMotion`, ahora sobre la raíz del arma y relanzado cada vez que se muestra).
  - Verificado con capturas en cámara lenta (`Bridge/swap_*.png`).
- Pendiente informado por el usuario: el T-07 a veces cancela el disparo cargado. Sospecha: la carga de 1,2 s se cancela si pierde línea de vista o si el objetivo sale del rango durante la carga. Sin investigar.

### F7 · Tubos de armas principales (v16, 10/11/2026)
- [x] Causa real (video del usuario): `ArmModuleVisual` (cambio de arma propio) solo movía `VO_Module`; los tubos (fuel cells), el bloque pistola del template, soportes y cañones son hermanos de `VO_Module`, así que se quedaban en el antebrazo mientras el módulo salía/entraba.
- [x] `ArmModuleVisual.cs`: los hermanos con malla (salvo Attachment, muzzle y vapor) siguen la pose del módulo; los tubos se hunden dentro del cuerpo (`cellSink` (0,-0.2,0)) al soltar y salen al acoplar (62-92 % del acople). Sin alloc por frame; respeta posiciones que escriben `WeaponFuelCellHandler` y `CylinderBankReload`. Backup: `Tools/IronRequiem/Backups/ArmModuleVisual_preF7tubes.cs.bak`.
- [x] Escopeta: los 4 tubos del template sobresalían ~7 cm sobre el cuerpo delgado. `F7 Fit shotgun tubes` baja `WeaponMesh_Pistol` 0.065 m en `Weapon_Shotgun_ArmModule_VO` (tubos al ras, siguen subiendo por disparo). Backup: `Tools/IronRequiem/Backups/Weapon_Shotgun_ArmModule_VO_preF7tubes.prefab.bak`.
- [x] Nuevo editor: `Assets/IronRequiem/Editor/VisualPass/VisualPassTubes.cs` (Audit arm tubes, Fit shotgun tubes).
- [x] Verificado en cámara lenta (tc_a*/tc_b*): todo el módulo sale y entra como una pieza; tubos ocultos durante el viaje.

### F7 · T-07 cancelaba el disparo cargado (v17, 10/11/2026)
- [x] Causa: `EnemyController` cancelaba la carga en cuanto, durante un solo frame, perdía de vista al objetivo, el objetivo salía del rango, o el rayo boca→objetivo tocaba cualquier collider (otro enemigo, borde de cobertura).
- [x] Fix en `Assets/FPS/Scripts/AI/EnemyController.cs`: nuevo `EnemyChargeGrace` (0,45 s). Con la carga empezada, un bloqueo breve ya no la cancela; si la carga termina bloqueada, se mantiene y dispara apenas haya tiro limpio. Solo se cancela si sigue bloqueada más de 0,45 s o el robot muere. Afecta a todo enemigo con `EnemyChargeDuration` > 0. Finales de línea originales conservados.
- [x] Backup: `Tools/IronRequiem/Backups/EnemyController_preT07grace.cs.bak`. Compila sin errores.
- [ ] Probar jugando (no probado en partida).

### F8 · Nuevo modelo del T-07 (mech Tripo con rig) — escena de prueba (v18, 10/11/2026)
- [x] Los dos zips son el mismo mech (uno con rig, otro sin rig). Se usa el que tiene rig: `Art/Enemies/T07Mech/T07Mech_Rigged.fbx` + 99 texturas (`.fbm`).
- [x] Optimizado (`F8 Mech 2 build visual`): de 99 mallas skinned + 99 materiales a 1 malla skinned (12.5k vértices, 20 huesos, 1 hueso por vértice: placas rígidas) + cañón rígido aparte en el antebrazo derecho + 1 atlas 4096 + 1 material URP. Corrige el giro de ~45° del export. Prefab: `Prefabs/VisualPass/Mech/T07Mech_Visual.prefab`.
- [x] Animaciones nuevas (`F8 Mech 3`), con IK de piernas y pies plantados (sin resbalar, el paso va sincronizado por distancia): Mech_Walk, DisarmedWalk, Idle, CombatIdle, Turn, CannonAttack, UpperBodyNeutral, HitReaction, Death. Controlador `T07Mech_Locomotion` = copia de T07_Locomotion con los clips y la máscara nuevos (mismos parámetros).
- [x] Prefab de prueba `T07_Mech_Test` (copia desempacada de T07_Current_Repaired): arte viejo oculto, mech de 2,6 m, cañón del mech como módulo desmontable, flash de impacto y reacción del torso en el cuerpo nuevo. Paso 1,68 m. Velocidad 4,2 → 3 solo en la prueba.
- [x] Escena alternativa: `Scenes/RequiemCampaign_MechTest.unity` con copias `Room01..04_MechTest` y `Pursuit05_MechTest` (añadidas a Build Settings); los T-07 de la campaña se cambiaron por `T07_Mech_Test`. Originales intactos.
- [ ] Verificación en partida: la captura automática no llegó a enfocar al T-07 (los SawBots atacaban la cámara). Pendiente que el usuario lo juegue.
- [ ] Pendientes del nuevo T-07: piezas de blindaje que se desprenden y texturas de daño (siguen ligadas al modelo viejo y quedaron ocultas), desmontaje del cañón y robo con el modelo nuevo, IK de pies en rampas.
- [x] Muerte del T-07 nuevo (v19): sobrecarga + explosión compartida (RobotDestructionPool) en el pecho nuevo y el mech se parte en 15 piezas (cabeza, torso, pelvis, hombros, brazos, antebrazo, muslos, piernas, pies, cañón) que salen disparadas en todas direcciones (7–13 m/s, 4–9 m/s hacia arriba, giro 220–720°/s), rebotan con fricción y quedan como chatarra (600 s, como el resto de restos). Sin Rigidbody ni instanciado al morir (piezas pre-armadas inactivas en los huesos). Scripts: `Scripts/VisualPass/MechDeathExplosion.cs`, `MechDebrisPiece.cs`. Ya no se usan las placas ni el brazo del modelo viejo. Verificado en juego (`Bridge/boom_*.png`).

### Tarea 3 · Carga del arma robada del T-07 lejos del cañón (v20, 11/11/2026)
- [x] Causa real: `StolenShellFit` mueve la carcasa (VP_StolenShell) cada LateUpdate para encajarla en el antebrazo, pero `WeaponMuzzle` (T07_Muzzle, padre del efecto de carga y origen del disparo y del fogonazo) seguía en su posición fija del prefab. Carga, disparo y fogonazo nacían lejos de la boca.
- [x] Fix: nuevo `Scripts/VisualPass/StolenChargeAnchor.cs` (orden 210, después de StolenShellFit): mide una vez el frente de la carcasa y cada frame pone el muzzle en la boca. Solo mueve la posición; la rotación de puntería queda igual. Sin alloc por frame. Sigue la carcasa en reposo, al moverse, en slide y en dash, porque se recalcula cada frame.
- [x] Carga mejorada: el módulo vibra y se hincha poco a poco con la carga (5 % máx.), se echa un poco hacia atrás y da un golpe de retroceso al soltar. El magenta original no cambia. Partículas de carga en espacio Local (pegadas a la boca).
- [x] Aplicado con `T3 Fix stolen muzzle + charge anchor` en Weapon_T07_Temporary_Optimized, Weapon_BigBot_Temporary, Weapon_BigBot_Bazooka_v08, Weapon_BigBot_ReferenceCharge_v09 y BigBot_PlayerWeapon. Respaldos en `Tools/IronRequiem/Backups/T3/`.
- [x] Corregido un bug propio: los offsets se acumulaban cuando StolenShellFit no reescribía la carcasa, y el arma se iba detrás de la cámara.
- [x] Verificado en juego (`Bridge/t3_*.png`): con el arma del T-07, la carga aparece en la boca del cañón y crece; muzzle = efecto = frente de la carcasa.
- [x] Diag: `Scripts/Diagnostics/ChargeHoldProbe.cs` (lo añade el menú `T3 Diag hold charge`, solo en Play).
- [ ] No probado en movimiento real, slide ni dash (solo con la cámara quieta).
- [ ] T-07 enemigo (carga de 1,2 s y cancelación): sin cambios de código; falta revisarlo en partida.

### Tarea 1 · Muerte unificada en piezas para todos los enemigos (v21, 11/11/2026)
- [x] Referencia (elegida por el usuario): explosión en piezas del T-07 mech nuevo (`MechDeathExplosion`).
- [x] `MechDeathExplosion` ahora es reutilizable: `ConfigureGeneric` con piezas, renderers fuente, renderers a ocultar, retardo de sobrecarga, fuerza (tamaño), offset del centro, segundos de chatarra, color de chispas y material de chispas. Chispas propias (un sistema por enemigo creado en Awake, sin alloc por frame, sin luces dinámicas). Piezas pre-armadas inactivas; al morir se sueltan del enemigo y siguen volando aunque el enemigo se destruya.
- [x] Seguridad: guarda al morir qué partes siguen existiendo (placas o brazo ya desprendidos no se repiten); las piezas cercanas no salen hacia la cámara (radio de 16 m); las piezas grandes vuelan menos (la cabeza de la torreta cae, no cruza la sala).
- [x] Herramienta `T1 Apply unified death to campaign enemies` (`Editor/VisualPass/VisualPassDeathFx.cs`): parte cada malla skinned por hueso dominante (mallas en `Art/VisualPass/DeathPieces/<enemigo>/`), usa solo LOD0, excluye módulos de arma (cañón robable), escala correcta en todos (piezas hijas del hueso o renderer, sin el bug de 100×). Respaldos en `Tools/IronRequiem/Backups/T1/`.
  - T07_Current_Repaired: 20 piezas, retardo 0,28 s (sobrecarga existente), chispas magenta.
  - BigBot_Current: 21 piezas, fuerza 1,7 (más grande), retardo 0,2 s.
  - SawBot_Current: 11 piezas, retardo 0,22 s.
  - HoverBot_Current: 6 piezas, sin retardo, cae desde el aire, chispas azules.
  - Turret_Current: 3 piezas, la base queda fija en el suelo.
- [x] Se mantienen las bolas de fuego, muertes pesadas, destrucción progresiva y desprendimientos existentes. Ninguna muerte normal suelta armas recogibles (el dron sigue soltando su botiquín, como antes).
- [x] T07_Mech_Test reconstruido sin piezas duplicadas.
- [x] Verificado en juego con los 5 a la vez (`Bridge/d_*.png`).
- [ ] Swarm Bot: se le pone en la Tarea 4.

### T-07 enemigo dispara desde su cañón (v21)
- [x] Nuevo `Scripts/VisualPass/EnemyMuzzleAnchor.cs` (orden 220, después de T07CannonSocket): mide una vez la boca del módulo del cañón (punta del eje largo) y pone ahí el muzzle del arma cada frame; la rotación de puntería queda igual. El efecto de carga del T-07 ya colgaba del muzzle.
- [x] Aplicado con `T1 T-07 enemy fires from cannon` en T07_Current_Repaired.
- [x] Verificado en juego (`EnemyShotProbe`): 6 disparos, todos nacen en el muzzle, a 0,39 m del centro del módulo (módulo de 0,69 m), o sea en la boca. La carga magenta se ve en el brazo del cañón (`Bridge/t07_s*.png`).

### Tarea 2 · Efectos de disparo de todas las armas del jugador (v22, 11/11/2026)
- [x] Pool común `Assets/FPS/Scripts/Game/Shared/FxPool.cs` (`FxPool` + `PooledFx`): reusa instancias inactivas, reinicia partículas y estelas, y limpia referencias muertas al descargar escenas.
- [x] `WeaponController.HandleShoot`: proyectiles del pool (antes había un Instantiate por bala). Aplica también a enemigos que disparan con WeaponController.
- [x] `ProjectileStandard`: vuelve al pool al impactar o al vencer la vida (antes Destroy). Lo que no viene del pool se destruye como antes. Ya no suscribe dos veces OnShoot al reusar, reutiliza la lista de colliders ignorados y usa `SphereCastNonAlloc` (antes `SphereCastAll` reservaba memoria cada frame por bala). Impacto con pool.
- [x] Impacto según superficie (`Scripts/VisualPass/ShotFxLibrary.cs`, asset `Resources/IronRequiem/ShotFxLibrary.asset`): robots → impacto propio del arma; metal (nombre de material/objeto: metal, rust, steel, pipe, door, crate…) → `VP_Impact_Metal` (chispas que caen + destello); resto → `VP_Impact_Dust` (polvo + esquirlas). La clasificación se cachea por collider.
- [x] Humo leve en la boca (`Scripts/VisualPass/MuzzleShotFx.cs` + `VP_MuzzleSmoke`, con pool) en el mismo frame que el fogonazo, el retroceso y el sonido (`OnShootProcessed`). Más humo en la escopeta y las robadas. Aplicado a Weapon_IR_ArmCannon_VO, Weapon_Shotgun_ArmModule_VO, Weapon_T07_Temporary_Optimized, Weapon_BigBot_Temporary, Weapon_BigBot_Bazooka_v08, Weapon_BigBot_ReferenceCharge_v09 y BigBot_PlayerWeapon.
- [x] Fogonazo corto: partículas ≤ 60 ms en VP_Muzzle_Primary, VP_Muzzle_Shotgun y BigBot_BurstMuzzle_v10 (este también lo usa el Big Bot enemigo).
- [x] Estelas: VP_Bolt_Primary y VP_Bolt_Shotgun ahora tienen estela naranja corta (60 ms, afinada); T-07 magenta y explosivo robado: estela acortada y afinada.
- [x] Todo sale del muzzle de cada arma (robadas ancladas a la boca por T3).
- [x] Verificado en juego con el tiempo casi congelado (`Bridge/sh_*.png`): fogonazo en la boca, perdigones con estela, bala magenta del T-07 robado; impacto de polvo registrado en pared a 62 m.
- [x] Respaldos en `Tools/IronRequiem/Backups/T2/` (ProjectileStandard, WeaponController, prefabs).
- [ ] Sierra robada (Saw Bot): sin disparo; no se tocó su efecto de contacto.
- [ ] Profiler: se mide en la Tarea 4 junto con la horda.

## v23 - Mech T-07 dispara desde el cañón (2026-11-11)
- [x] Bug: el mech T-07 cargaba con el brazo colgando; el muzzle miraba al suelo (fwd y=-0.75) y el proyectil moría en 0.07 s contra el piso.
- [x] `T07CannonSocket`: aim opcional del brazo (`aimBone` = Right_UpperArm) encima de la animación, con blend in/out mientras ve al objetivo.
- [x] `EnemyMuzzleAnchor`: `aimAtTarget` (cono 60°) orienta el muzzle al objetivo; `MouthWorld()` público.
- [x] Menú `T1 Mech T-07 aims cannon`; `F8 Mech 5 test prefab` lo re-aplica al reconstruir.
- [x] Play: 8/8 disparos, muzzle fwd ≈ dirección a cámara, proyectiles recorren ~12 m hasta el jugador. Carga magenta en la boca del cañón. 0 errores.
- Backups: `Tools/IronRequiem/Backups/AimFix/`.

## v24 - Nuevos enemigos en la campaña (agregados, no reemplazan) (2026-11-11)
- [x] Prefab de campaña `Prefabs/Campaign/T07_Mech.prefab` (copia del mech probado; el de test queda para la escena de prueba).
- [x] Menús `T4 Audit campaign spawns` / `T4 Add mech T-07 to campaign rounds` (idempotente, solo agrega grupos, backup de escenas en `Tools/IronRequiem/Backups/Spawns/`).
- [x] +1 T07_Mech en: Room01 ronda 3 (base y las 3 variaciones); Room02/03/04 rondas 2 y 3. Los T-07 viejos y demás enemigos siguen igual.
- [x] Play: campaña arranca, prewarm sin errores.
- [ ] Swarm Bot (T4): también se agrega junto a las oleadas existentes (confirmado por el usuario: no es reemplazo).

## v25 - Carroñero (enemigo de horda, Tarea 4) (2026-11-11)
- [x] Fuente Tripo copiada a `Art/Enemies/Carronero/Source` (FBX original intacto; metal+rugosidad empacados en `Carronero_MetalSmooth.png`).
- [x] C2: rig colapsado 65 -> 22 huesos, piel dura por hueso dominante, sierra separada (`SawSpin`, gira sola, borde naranja emisivo), 4 LODs (v 16381 / 3317 / 1563 / 473; t 10653 / 7175 / 3873 / 1361), LODGroup 25% / 10% / 4% / 1.2%.
- [x] Bug: guardar LODs con `CopySerialized` dejaba mallas mezcladas (picos). Ahora se reemplazan los datos manteniendo el asset.
- [x] C3: `Prefabs/Campaign/Carronero.prefab` sobre la base de SawBot (EnemyController, EnemyMobile, Health, SawBotCombat sierra sostenida, NavMeshAgent). Vida 26, velocidad 7.2, daño 4.5/s. Sin ejecución ni robo. Sin barra de vida flotante.
- [x] `CarroneroMotion`: paso procedural, brazo de sierra al atacar, sierra girando; no usa Animator; no posa si está fuera de cámara.
- [x] C4: muerte unificada (22 piezas, explota al instante, piezas 4 s).
- [x] Rendimiento (24 en editor): pitch infinito en `EnemyMobile` (aviso cada frame) corregido; `DetectionModule.DetectionInterval` (0.1 s en el Carroñero, default 0 = igual que antes); destino NavMesh con umbral 0.6 m / 0.3 s; evasión LowQuality; audio con prioridad 200; quitados RobotHitFeedback, EnemyStateLights, flash de cuerpo, cadenas y humo heredados del arte oculto. LateUpdate 3.7 -> 1.25 ms, scripts totales +2.4 ms para 24 (editor).
- [x] Oleadas (junto a lo existente): Room02 ronda 2 +8, Room03 ronda 3 +14, Room04 ronda 3 +20. Menú `T4 Add Carronero hordes to campaign rounds`.
- [ ] Medir en build de desarrollo (el editor suma overhead) y probar las rondas con horda en partida real.
- Backups: `Tools/IronRequiem/Backups/Perf/` (EnemyMobile, DetectionModule), `Backups/Spawns/` (escenas de cuartos, estado previo a las hordas).

## v26 - Menú principal "Hangar C-04" (IntroMenu) (2026-11-11/12)
- [x] Fondo 3D animado en `Assets/FPS/Scenes/IntroMenu.unity` generado por `Assets/IronRequiem/Editor/Menu/HangarMenuAuthoring.cs` (menú `Tools/Iron Requiem/Menu/Build Hangar Intro (IntroMenu)`, idempotente: todo bajo `HangarSet` o con prefijo `HG_`). Se reutilizan RequiemMenuView, LoadSceneButton, opciones y créditos; solo se reestiliza la UI.
- [x] Runtime en `Assets/IronRequiem/Scripts/Menu/` (MenuFlicker, MenuCameraRig, CapsuleRobotIdle, BridgeWalker, BridgePatrol, ConveyorBelt, MenuExplosionFx, HangarLaunchSequence, HangarButtonFx, HangarStatusTag, MenuSpin) y puente `Bridges/Game/MenuLaunchSequence.cs`. RequiemMenuView.Primary reproduce la secuencia de JUGAR si hay una registrada.
- [x] Protagonista (modelo Tripo `Art/Protagonist/CapsuleRobot`) en la cápsula C-04. Empieza apagado. Con JUGAR: la cámara se acerca a su cara, el visor y las líneas cian se encienden con tartamudeo, aparece EN LÍNEA y la pantalla funde a negro antes de cargar la campaña.
- [x] Cinta transportadora al fondo: copias del T07_Mech y del Carroñero sin gameplay (`Prefabs/Menu/MenuWalker_*`) viajan apagadas. Explotan al hacer clic (contador HOSTILES NEUTRALIZADOS).

### Corrección 12/11: esfera blanca sobre la cinta
- [x] Causa verificada con `Debug report emissive`. El LODGroup de las copias volvía a encender renderers ocultos en el prefab fuente, porque los scripts que los apagaban se quitan al copiar. Uno de ellos, `BasicRobot/EyeBall` del T07 (material HoverBot_Eye, emisión HDR 24), con bloom sin clamp (65472) y Stop NaN apagado, producía la esfera que quemaba el centro. Lo mismo pasaba con la armadura vieja del T07, la piel vieja del SawBot y `CoreIndicator` (RobotEye_Red, Unlit) del Carroñero (rayas y destellos sueltos).
- [x] `CleanWalkerVisuals`: conserva solo los renderers visibles en el prefab fuente con material iluminado. Elimina los ocultos, los Unlit/Particles/Additive y los nombres de FX, reconstruye los LODGroup con lo que queda y asigna copias sin emisión (`Prefabs/Menu/Materials/MW_*`), así que ojos e indicadores quedan apagados. Las copias ya no tienen ParticleSystem, Light, LensFlare ni Trail (verificado en el informe).
- [x] Constantes al inicio de HangarMenuAuthoring: luz de exhibición `WashIntensity/Range/Angle` = 5.5 / 7 / 54 (tres spots compartidos por tramo, antes 11 / 8 / 62); bloom threshold 1.1, intensity 0.6, scatter 0.65, **clamp 10**; Stop NaN activo en la cámara; tope HDR de las piezas Unlit (`GlowCap`) = 2.0, salvo las tiras cian sin cambio; niebla 0.022. Tonemapping Neutral confirmado. Sin luces dinámicas nuevas.
- [x] Verificado en Play: más de 30 s de cinta con capturas 1080p y de Game view antes y después (`Tools/IronRequiem/Bridge/hgc_25-29` y `glow_gameview_*` antes; `hgc_30-36` y `glow_after_*` / `glow_final_*` después). Sin esfera ni halos. Explosión y secuencia de JUGAR correctas. Los informes `emissive.txt`, `walkers.txt` y `flicker.txt` están en `Tools/IronRequiem/Reports/HangarMenu/`.
- [x] Respaldos: `Tools/IronRequiem/Backups/20261112-hangar-glow/` (script y checklist previos); cada build guarda escena, volumen y prefabs de menú en `Tools/IronRequiem/Backups/<fecha-hora>-hangar-build/`. Escena original del menú en `Backups/20261111-hangar-menu/`.
- [ ] Consola: sin errores de los scripts del menú. Quedan errores internos de Unity ajenos (`Library/StateCache ... Sharing violation` y `Library/EditorToolsSettings.asset` vacío/corrupto, ambos por bloqueo de archivos en Library; `Failed to create domain-reload token` del paquete AI Assistant).
- [ ] Ojo rojo del T07_Mech en la cinta: es un píxel pintado en el albedo del atlas compartido `T07Mech_Atlas`, no emisión (no genera bloom). Para borrarlo habría que usar una copia del atlas solo para el menú; no se tocó el atlas de campaña.
- [x] `JointIndicator`, `PowerIndicator`, `Sphere`/`EyeBall` y `CoreIndicator` eliminados de las copias (filtro por nombre ampliado: Indicator, EyeBall, Eye_, Glow, Orb, VFX). 86 renderers eliminados en total.
- [ ] Probar un clic real con el ratón y la carga completa tras JUGAR (en la verificación se usaron menús de depuración).


## v27 - Sector 0 "Room00_Hangar": despertar del protagonista y Carroñeros carroñeando (2026-11-12)
- [x] Timeline 1.8.13 instalado con aprobación (`Tools/Iron Requiem/Packages/Add Timeline`, respaldo de manifest/lock en `Backups/20261112-timeline/`). Cinemachine 3.1.7 ya estaba. Sin paquetes preview.
- [x] Modelo riggeado del protagonista (Tripo, huesos estilo Mixamo con dedos) en `Art/Protagonist/CapsuleRobotRig/` (import Generic, sin materiales ni clips). Lo usan la cápsula del menú y la cinemática (materiales `HG_ProtagonistRig` / `HG_ProtagonistRigGlow`; overlay de líneas cian como segundo SkinnedMeshRenderer sobre los mismos huesos).
- [x] Escena `Assets/IronRequiem/Scenes/Campaign/Room00_Hangar.unity` generada por `Editor/HangarIntro/HangarSectorAuthoring.cs` (menú `Tools/Iron Requiem/Hangar Intro/Build Room00_Hangar + campaign`, idempotente, conserva GUID de escena, NavMesh y Timeline). Reutiliza los constructores del menú (`HangarMenuAuthoring`: entorno, cápsula C-04, banners, cinta, luces medio encendidas, niebla, volumen `HG_MenuVolume`); no duplica texturas. Raíz `HangarSector` en (0,0,-60) girada 180° para que el frente abra al lobby persistente.
- [x] Gameplay del hangar: colisores en muros, columnas, cajas, tuberías, cápsula, pilares; muro frontal con esclusa (12 x 7 m) y cartel "ESCLUSA · SECTOR 1"; bloqueos invisibles atrás y techo (jetpack). 7 pilas de chatarra (placas, brazos arrancados, restos de Carroñero/T07 sin scripts), chispas intermitentes, vapor, 3 lámparas de trabajo y 2 luces cálidas de zona. NavMesh propio (radio 0.6, las pilas fuera del bake para que los carroñeros lleguen al borde). 10 puntos de chatarra, todos sobre el NavMesh.
- [x] Campaña (`RequiemCampaign`): `stageScenes` = Room00_Hangar, Room01..Pursuit05. El lobby inicial es la esclusa 0 (`HangarAirlock`: LobbyRear -> InitialLoadingDoor); las 4 esclusas existentes se desplazaron +1. `CampaignFlow.prologueStages = 1`: la numeración "SECTOR n" no cambia (Foundry sigue siendo SECTOR 1). Portada negra `CampaignStartCover` mientras carga el primer sector. Jugador, UI e inventario siguen en la sesión.
- [x] Cambios mínimos a sistemas existentes: `CampaignRoom` (prólogo de exploración: `Release()` completa la sala), `CampaignFlow` (prólogo y portada), `CombatLabMenu.ResumedAfterRestart`, `PlayerInputHandler` (`PlayerInputLock`), `PlayerCharacterController.SetViewAngles`, `EnemyMobile` (estado `Scavenging` vía `IEnemyScavenging`), `DetectionModule` (`Suppressed`, `ForceKnownTarget`), `SawBotCombat.SpeedScale`, `CarroneroMotion` (agacharse/hurgar/alcanzar), `CapsuleRobotIdle` (`PoseDriven`, `GlowScale`, `PowerDown`). Respaldos en `Tools/IronRequiem/Backups/20261112-hangar-sector/` y por build en `Backups/<fecha-hora>-room00-hangar/`.
- [x] Cinemática 13.4 s (`HangarIntroCinematic` + Timeline `Art/Levels/Campaign/Room00_WakeUp.playable`: pista Cinemachine con 6 planos y pista de audio con 12 cues). Planos: A visor parpadea y se estabiliza (mismo encuadre que el último frame del menú) · B servos: dedos se cierran, antebrazo-cañón se arma con chispas y brillo · C sale de la cápsula con peso, mano izquierda en el riel, pie al suelo con polvo y chispas · D erguido, las líneas cian terminan de encenderse · E plano revelando el hangar con los Carroñeros hurgando sin notarlo · F la cámara vuela a la cabeza y funde a primera persona.
- [x] Pose procedural sobre el rig del cuerpo (`WakeUpRobotPose`): función pura del tiempo del director (scrub/skip/pausa dan la misma pose), trayectoria del cuerpo cápsula -> borde -> cuclillas -> de pie, IK de dos huesos para pies plantados (sin deslizar ni flotar), mano en el riel y brazo-cañón, dedos, brazos relajados al salir, cabeza que mira a los Carroñeros.
- [x] Audio procedural propio (sin assets externos) en `Audio/SFX/HangarIntro/`: hidráulica, servos, arranque eléctrico, pisada metálica, cañón, hurgar x3, arranque de pieza, chatarra, chispa. Grupos del `MainAudioMixer`: Player (cinemática), EnemyMovement (carroñeros), General. La música de exploración la sigue llevando `CombatMusic`.
- [x] Saltar manteniendo E / Espacio (o botón sur) 0.9 s con indicador radial. Durante la cinemática: input bloqueado, HUD oculto, letterbox. Al terminar (completa, saltada o reinicio tras muerte) el estado final es el mismo: robot oculto (el jugador *es* el robot), jugador en el punto de pie mirando a la esclusa, pitch 0, HUD visible, control devuelto, sala liberada, fundido de 0.7 s.
- [x] Solo se reproduce al iniciar la campaña (JUGAR o inicio directo); `RestartFromFirstRoom` tras morir salta directo al estado final.
- [x] 5 Carroñeros reales (`Prefabs/Campaign/Carronero.prefab`, no las copias del menú) con `ScavengerBehaviour` + `ScavengerGroup`: caminan entre puntos, se agachan y hurgan (chispas + metal), a veces arrancan una pieza, la cargan y la lanzan en otra pila, pausas y miradas. Ignoran al jugador hasta recibir daño; entonces él y los que estén a 14 m pasan a Follow/Attack normales y la música entra en combate.
- [x] Verificado en Play (puente de archivos, `Tools/Iron Requiem/Hangar Intro/Debug/*`): capturas por plano `Tools/IronRequiem/Bridge/hi_*.png`, hand-off, provocación de grupo, transición lobby -> Room01 ("SECTOR 1 LISTO · TRES RONDAS"), sin errores de consola de estos scripts. Informe `Tools/IronRequiem/Reports/HangarIntro/`.
- [x] `CampaignTests` adaptado: pasa el prólogo (salta la cinemática, entra a la esclusa) y desplaza los índices de esclusas por `PrologueStages`.
- [x] Skip comprobado: estado final idéntico al de la reproducción completa (jugador (-0.68, 0, -58.76), yaw 30, pitch 0, HUD visible, input libre, sala completada). Reinicio tras muerte: `ResumedAfterRestart = true`, la cinemática no se reproduce y se llega al mismo estado.
- [x] Menú reconstruido con el robot riggeado; el último frame de JUGAR (`hgc_43`) coincide con el plano A de la cinemática.
- [ ] Pasar `CampaignTests` completo en el Test Runner (no se ejecutó en esta sesión).
- [ ] Probar en partida real con teclado/ratón: mantener E/Espacio para saltar y recorrer el hangar hasta la esclusa.
- [ ] El pedido original llegó cortado tras "Ignoran al jugador": si había más reglas para los Carroñeros neutrales (p. ej. reaccionar si el jugador se acerca mucho), faltan.
- [ ] Rendimiento medido solo en editor (sin asignaciones por frame en los scripts nuevos); falta medir en build.


## v28 - Sector 0 v2: puerta, rodillas, vidrio, actuación, chatarra, cámara de conexión, pasillo de alarma y consola de opciones (2026-11-12)
Todo se genera desde los scripts de authoring (idempotentes, conservan GUID): `Tools/Iron Requiem/Hangar Intro/Build Room00_Hangar + campaign` y `Tools/Iron Requiem/Menu/Build Industrial Options (menu + pause)`. Respaldos: `Tools/IronRequiem/Backups/20261112-hangar-v2/` (scripts previos), `Backups/<fecha-hora>-room00-hangar/` y `Backups/<fecha-hora>-industrial-options/` (escenas antes de cada build).

### 1. Bug: el hangar desaparecía al salir
- [x] `CampaignFlow.Prepare` (todas las esclusas): cierra la puerta de origen y espera a que esté **cerrada del todo** (`AirlockDoorMotion.IsClosed`) **y** el jugador siga dentro del volumen (`CampaignAirlock.PlayerInside`). Si sale antes, la puerta se reabre y se cancela el ciclo. Solo entonces descarga el sector anterior.
- [x] Lo que se ve desde la esclusa vive en la zona persistente: cámara de conexión (z -84..-76), vestíbulo (z -76..-70) con persiana `VestibuleShutter`. Ya no hay vacío visible.
- [x] Verificado: al entrar a la cámara el hangar sigue cargado mientras cierra la puerta; luego carga el pasillo (`busy=True` -> `stage=1`).

### 3. Bug: rodillas invertidas
- [x] `WakeUpRobotPose` v2: IK analítico de dos huesos con la rodilla en el plano del polo; polo delante de la rodilla que sigue al cuerpo (`mid + bodyFwd·0.6·pierna`). Objetivo del pie limitado por debajo de la cadera (`hip.y − 0.25·pierna`) y a la longitud de la pierna: sin estirar malla. Pies plantados y re-plantados al girar.
- [x] Verificado con capturas laterales `Tools/IronRequiem/Bridge/hi_side_*.png` (6.8 s bajada, 7.9 y 9.0 s cuclillas, 13.8 s de pie): rodillas siempre hacia delante.

### 2 y 4. Cinemática: vidrio y actuación (19.6 s, 8 planos, 20 cues)
- [x] `CapsuleGlassBreak`: golpe 1 (4.35 s) grieta con sonido seco; golpe 2 (5.15 s) estalla. 22 fragmentos físicos (4 mallas procedurales) del pool, se asientan y se apagan a los 6 s o al descargar. Polvo, chispas, sonido fuerte, sacudida leve de cámara. Ignora el colisor de la cápsula.
- [x] Skip: idéntico estado final con el vidrio roto (`glass.Broken()`). Cápsula del menú sin cambios (vidrio intacto).
- [x] Actuación tras bajar: mira la mano derecha, gira palma/dorso, abre y cierra dedos, inclina la cabeza, pulso del visor; gira cabeza y luego torso hacia la cinta (105°), pausa; vuelve a los Carroñeros y enlaza con el plano existente. +6.2 s. E/Espacio siguen saltando.
- [x] Planos: A_Visor 0–2.2 · B_Servos 2.2–4.0 · B2_Glass 4.0–5.7 · C_Exit 5.7–9.8 · D_Hand 9.8–12.7 · D2_Belt 12.7–15.0 · E_Reveal 15.0–17.8 · F_ToHead 17.4–19.6. Capturas `hi_*_t*.png`.

### 6. Chatarra con los modelos adjuntos
- [x] `HangarScrapAuthoring`: importa `Scrap_RustedWreckage`, `Scrap_MachineWreck`, `Scrap_MilitaryMech` (`Art/Environment/Scrap/`), material `IronRequiem/HardSurface` (suciedad + desgaste, GPU instancing), escala normalizada (1 m), pivote al suelo, LODGroup, prefabs en `Prefabs/Scrap/`. ~9.3–9.7 k tris cada uno.
- [x] 7 pilas procedurales con semilla fija (2–4 restos + 3–6 piezas sueltas), estáticas (batching), colisor de caja simple por pila. Los Carroñeros lanzan piezas de este set. NavMesh rehorneado: 10/10 puntos sobre la malla.

### 7. Cámara de conexión + pasillo de alarma
- [x] Flujo: Room00_Hangar -> CÁMARA (persistente) -> pasillo `Room00b_AlarmCorridor` -> esclusa del lobby -> Room01 (SECTOR 1). `prologueStages = 2`: la numeración SECTOR n no cambia.
- [x] Cámara: dos puertas, ojo de buey blindado que ve el vestíbulo con luces rojas apagadas, paneles, cables, cartel "TRÁNSITO C-04 → PUENTE NORTE". Secuencia: entrar -> se cierra la puerta del hangar -> ~1.5 s de descontaminación (vapor, ámbar -> verde, hidráulica; `AirlockCycle`) -> descarga hangar y carga pasillo -> abre la puerta del pasillo.
- [x] Pasillo: estrecho, coberturas (cajas, barricada), plataforma elevada con rampa, tubos que parpadean, 4 ductos. Al entrar, `CorridorAlarm`: sirena (grupo Ambient), klaxon, 4 balizas giratorias, 8 luces rojas, 6 pantallas, mensaje "ALERTA · INTRUSIÓN DETECTADA". Al limpiar: "ZONA DESPEJADA · ESCLUSA ABIERTA".
- [x] Composición: **ronda 1** = 2 T-07 (`T07_Current_Repaired`, uno tras cobertura y otro en la plataforma, cañón cargado) + 2 Carroñeros de ductos; **ronda 2** = 3 Carroñeros de ductos. Total 2 T-07 + 5 Carroñeros. Sistema de rondas existente (`CombatTwoRoomsLab`), ejecución con E bajo 75 % intacta.
- [x] La esclusa al lobby solo abre al morir todos. Una sola escena de combate cargada a la vez.
- [x] Reinicio tras morir (`RestartFromFirstRoom`) desde el pasillo: vuelve al hangar, `restart=True`, sin cinemática, vidrio roto, control libre.

### 5. Opciones como consola industrial
- [x] `IndustrialOptionsAuthoring` reestiliza los 3 paneles existentes (IntroMenu, pausa `InGameMenu` y `CombatLabMenu`) sin tocar la lógica: mismos Slider/Toggle/Button, listeners y `RequiemSettings`. Placa remachada, franja de peligro, 3 lámparas de estado, faders verticales (volumen, sensibilidad y **temblor de pantalla**) con LCD, palanca para pantalla completa, botón VOLVER iluminado, navegación explícita teclado/mando. Sprites propios en `Art/Menu/UI/`.
- [x] Monitor CCTV: `CctvFeed` (cámara desactivada, sin post/sombras/AA) + `CctvMonitor` (RT 256x144 a 15 fps con `Camera.Render`, solo mientras el panel está activo; se libera al cerrar) + shader `IronRequiem/UI/CCTV` (fósforo verde, scanlines, ruido, glitches, ganancia nocturna). Rótulo "CAM 04 · HANGAR C-04", reloj, ● REC.
- [x] Menú: misma vista que la cámara inicial del menú, con un Carroñero (capa 30, solo lo ve el CCTV; `CctvActor`) hurgando junto a la cápsula C-04. Pausa: cámara equivalente dentro del hangar (se descarga con el sector; fuera del hangar muestra "SIN SEÑAL").
- [x] Funciona en pausa (`timeScale=0`, tiempo no escalado). Persistencia verificada entre dos sesiones de Play: volumen 0.37/0.80 y sensibilidad 3.04 se leen al reabrir.

### Verificación
- [x] Flujo completo por puente de archivos: JUGAR/BeginGame -> cinemática (o skip) -> cámara -> pasillo (2 rondas) -> lobby -> Room01 "SECTOR 1 LISTO · TRES RONDAS". Consola: 0 errores (solo avisos internos ADTM de audio de Unity).
- [x] Capturas: vidrio `hi_2_t4.4`/`hi_3_t5.3`, piernas `hi_side_*`, mano `hi_7_t11.2`, giro a la cinta `hi_8_t13.8`, chatarra `hi_9_t16.5`/`hi_fp_*`, pasillo `hi_corr_*`, opciones `opt_menu_3`/`opt_pause_2`, CCTV `cctv_*`.
- [x] Medición en editor (Game view 1001x335, indicativo): hangar 55 fps (18 ms), opciones con monitor 67 fps (15 ms), cámara de conexión 68 fps con un pico de 752 ms al cargar/descargar escenas (ocurre con la puerta cerrada, tapado por el ciclo), pasillo 85 fps. El monitor no baja el rendimiento.
- [ ] Objetivo 120 FPS a 1080p en RTX 4050 Laptop: **no verificable en editor**; falta Profiler en build de desarrollo. El pico de carga del pasillo es candidato a optimizar (precarga o `allowSceneActivation`).
- [ ] GC en editor ~13 KB/frame (incluye asignaciones del propio editor); confirmar en build que los scripts nuevos asignan 0 por frame.
- [x] `CampaignTests` ejecutado completo desde `Tools/Iron Requiem/Tests/Run CampaignTests (PlayMode)` (`Editor/Tests/CampaignTestRunner.cs`, informe `Tools/IronRequiem/Reports/Campaign/testrun.txt`): **3 de 4 pasan** (`CurrentRobotsEyesAndExecutionOnlyWeapons`, `EquippedExplosionCleansInventoryInAdditiveRoom`, `IntroMenuStartsCampaignAndRestartReturnsToRoomOne`). El prólogo (hangar -> cámara -> pasillo 2 rondas -> lobby) pasa en todas.
- [x] Adaptaciones del test: espera a que la puerta del hangar esté abierta antes de entrar a la cámara; pasillo limpiado como un jugador (sin `Freeze`, que apaga la muerte de los Carroñeros); el pasillo no usa la variedad de oleadas de los tests; puertas animadas con espera; intensidad de lámparas igual entre sí (4.4 -> 6.0 por el pase visual); conteo de enemigos leído del propio plan de oleadas (cambió en v24); re-bind tras la secuencia de JUGAR del menú.
- [ ] `TwelveRoundsAirlocksInventoryAndPursuit`: Room01 completo (3 rondas, esclusa, escaleras) pasa; falla en **Room02 ronda 2** (plan de 14 enemigos que nunca están vivos a la vez con la IA congelada). Es contenido previo (v24), no del sector 0; revisar el tope de vivos de esa ronda o el test.
- [ ] Nota: con `ConfigureVariety(true)` las oleadas del pasillo se atascan (los puntos fijos no pasan `SafeSpawn`). El juego no usa variedad ahí; no activarla sin revisar los puntos de ducto.
- [ ] Probar con teclado/ratón real: E/Espacio para saltar, faders con ratón y palanca, navegación con flechas en la consola.


## v29 - Sector 0 v3: brazo rígido, cinemática pesada, depósito de chatarra, pasillo amplio y transiciones limpias (2026-11-12)
Mismos scripts de authoring idempotentes (`Build Room00_Hangar + campaign`). Respaldos: `Tools/IronRequiem/Backups/20261112-hangar-v3/` (scripts, `CombatTwoRoomsLab`, asmdef y checklist previos).

### 1. El brazo se deformaba al girar la mano
- [x] Causa: el auto-rig (Tripo) reparte pesos del hueso de la mano por todo el antebrazo, y la mano giraba 165°: el antebrazo se retorcía como goma (capturas `pre_hand_*`).
- [x] `RobotRigidSkin` (editor): copia de la malla del robot de la cinemática con **pesos duros por vértice** en las cadenas de brazos (Arm, ForeArm, Hand, dedos): 6349 de 24676 vértices siguen a un solo hueso. Cada segmento se mueve como pieza rígida, como el T-07. Asset: `Art/Protagonist/CapsuleRobotRig/Rigid/`. Torso, piernas y cabeza no cambian.
- [x] `WakeUpRobotPose`: el giro va solo en la muñeca (`maxWristRoll` 80°, palma → pausa → dorso → pausa → neutro, con suavizado). Hombro y codo quedan donde los deja el IK.
- [x] Primera versión con triángulos duplicados: dejaba picos en codo y muñeca y se descartó. Cerca, antes/después: `pre_hand_*` → `hi_hand_*`.
- [x] Se genera antes de abrir la escena (`Prepare`), porque reimportar el FBX con la escena abierta borraba los overrides de material. Menú `Rebuild rigid arm skin (next build)` para regenerarla.

### 2. Cinemática más lenta y pesada
- [x] 19,6 s → **30,4 s (+55 %)**. Tiempos nuevos por acción, no escalado: visor 0–3,6 s (despierta a 0,8 s), dedos/cañón 3,6–6,2, golpes 7,2 y 8,8 (preparación 0,3 s, retroceso 0,75 s), salida 9,8–16,2, mano 16,6–20,6, pausa ante la cinta 23,1–24,6, giro a los Carroñeros 24,6–26,6.
- [x] Curvas más lentas: asentamiento de rodillas, peso en cada paso, entradas y salidas suaves (rail, cañón, cabeza y mirada). Planos y 20 cues de audio calculados desde `WakeUpRobotPose.Timing`. Skip con E/Espacio: mismo estado final.

### 3. Cubos negros → depósito de chatarra
- [x] Quitados los `CrateA/B/C` y los bloques `Heap/Plate`. Búsqueda en el build: **placeholder cubes left = 0**.
- [x] 19 montones de 3 capas (base, medio, cima) con los 3 modelos, rotación y escala variadas, de 3,2 a 5,5 m de alto. 12 apoyados en paredes y columnas (piezas inclinadas hacia el soporte), 5 trabajados por los Carroñeros, 1 en el lugar de las cajas. 210 piezas + 45 restos sueltos en el suelo, siempre fuera del pasillo cápsula → esclusa.
- [x] Colisores de 2 cajas por montón; restos sueltos sin colisor ni sombra; solo la base proyecta sombra. Mesh LOD de Unity 6 activado en los 3 FBX; static batching. NavMesh rehorneado: 10/10 puntos de chatarra sobre la malla.

### 4. Transiciones
- [x] `CampaignFlow`: el siguiente sector se precarga (`allowSceneActivation=false`, prioridad de carga baja) en cuanto la puerta queda cerrada. Se activa con la puerta cerrada y luego se descarga el sector viejo (dos escenas solo esos frames, tras la puerta).
- [x] `ZoneEnvironment`: niebla, luz ambiental y reflejos se funden con los valores de la esclusa al cerrar (1,6 s) y luego con los de la zona nueva. Los Volumes globales y los audios de ambiente en loop de la zona vieja bajan a 0 y los de la nueva suben de 0. La música es de la sesión: no se corta.
- [x] `Resources.UnloadUnusedAssets` ya no corre en las transiciones del prólogo (era un frame largo); solo entre sectores de combate, tapado por "PREPARANDO".
- [x] Medido con un log por transición (`[Campaign] transition … peak frame`, por fases). Valores del editor:

| Transición | v2 (descargar → cargar) | v3, primera carga | v3, assets ya en memoria |
|---|---|---|---|
| Cámara → pasillo | 547 ms | 615–706 ms | **31–51 ms** |
| Lobby → Room01 | 968 ms | 979–1145 ms | **81–96 ms** |

  En el editor la primera carga de una escena se hace en el hilo principal, así que el pico frío no baja. Ahora ocurre siempre con la puerta cerrada y el ambiente ya fundido. La activación sola cuesta 30–40 ms; en build el resto se carga en segundo plano durante el ciclo.
- [ ] Confirmar en un build de desarrollo (Profiler conectado) que el pico frío baja a la cifra de "assets en memoria".

### 5. Pasillo más grande y despejado
- [x] 11×6 m → **18×9 m**. Pilastras al ras de la pared (0,2 m), vigas en el techo. Quitadas las cajas, columnas y barricadas sueltas.
- [x] Coberturas: 3 bloques grandes de 3,6×1,5×1,6 m con franja de peligro, más el puesto de guardia elevado (5×5 m, rampa de 3,4 m a 14°). Carriles libres a los dos lados para sprint, slide y dash.
- [x] T-07: uno tras el bloque C (−5, −42,2) y otro en el puesto (6,2, 1,45, −39,6). Ductos en z −64, −57, −51 y −66, alternando lados. Luces más altas y potentes. NavMesh rehorneado.

### Extra encontrado
- [x] `CombatTwoRoomsLab` (oleadas variadas): una ronda con más enemigos que puntos de spawn se quedaba atascada para siempre (Room02 ronda 2: 14 enemigos). Ahora las reservas caducan a los 2,5 s y una 3.ª pasada reutiliza los puntos; el chequeo físico sigue impidiendo aparecer encima de alguien.

### Verificación
- [x] Recorrido completo hangar → cámara → pasillo (2 rondas) → lobby → Room01 "SECTOR 1 LISTO · TRES RONDAS". Consola: 0 errores.
- [x] **CampaignTests 4/4 en verde** (158 s). Adaptado: dos escenas permitidas solo mientras `Busy`, rondas horda por tandas, conteos desde el plan.
- [x] Capturas: mano `pre_hand_*`/`hi_hand_*`, depósito `v3_yard_0..3`, pasillo `v3_corridor_0..1`, cinemática `hi_*`/`hi_side_*`.
- [x] Rendimiento en el editor (Game view 1001×586, indicativo): hangar 85–95 fps en las mejores tomas y 50 fps en una toma ruidosa del editor. Pasillo ~50 fps.
- [ ] 120 FPS a 1080p en RTX 4050: falta el build de desarrollo.
- [ ] Probar con teclado y ratón reales.


## v30 - Menú Hangar C-04 restaurado + cinemática Room00: corte al golpe, interior de cápsula, salida con peso, pisadas por contacto, cabeza y manos rígidas (2026-11-12)
Respaldos: `Tools/IronRequiem/Backups/20261112-1845-v30-menu-cinematic/` (scripts, IntroMenu, EditorBuildSettings, CampaignTests y checklist previos) y los de cada build (`<fecha-hora>-room00-hangar`, `<fecha-hora>-hangar-build`). Authoring idempotente, GUID conservados, FBX del rig sin tocar.

### 1. Menú animado "Hangar C-04"
- [x] **Causa:** la escena IntroMenu estaba intacta (HangarSet activo, HangarLaunchSequence registrado, primera en Build Settings), pero `Build Room00_Hangar + campaign` terminaba con `RequiemCampaign` abierta. Al pulsar Play en el Editor arrancaba la campaña con su menú de inicio simple (`CombatLabMenu`).
- [x] `HangarSectorAuthoring.Build` vuelve a abrir IntroMenu al terminar. Menú nuevo `Tools/Iron Requiem/Menu/Open IntroMenu`.
- [x] Reconstruido con `Build Hangar Intro (IntroMenu)`; convive con la consola industrial y el CCTV. Verificado en Play: explosión (kills=1), OPCIONES → panel industrial con monitor CCTV, VOLVER, JUGAR → secuencia de la cápsula → Room00 (`Tools/IronRequiem/Bridge/v30/menu_*.png`).
- [x] Guardia en `CampaignTests.IntroMenuStartsCampaign...`: HangarSet activo, HangarLaunchSequence activo e IntroMenu como escena 0.

### 2. Corte del plano raro de la mano
- [x] Se quitó el beat servos/cañón (plano B_Servos). Del visor (0–3,6 s) se corta a B2_Glass con el puño ya en recorrido (arranca 3,35 s, acelera con k², golpe 4,0 s; segundo golpe 5,6 s). Sin preparación visible.
- [x] Cinemática 30,4 → **27,8 s**. Planos: A_Visor · B2_Glass · B3_Release (interior) · C_Exit · D_Hand · D2_Belt · E_Reveal · F_ToHead. 15 cues de audio desde `WakeUpRobotPose.Timing`.

### 3. Interior de la cápsula (solo Room00; la del menú no cambia)
- [x] `CapsuleRestraints` (nuevo): 8 sujeciones telescópicas (hombros, pecho, muslos, espinillas) colocadas 5,5 cm delante de la superficie del robot; tras el vidrio se sueltan (pop de 2 cm), pausa y se retraen al riel con vapor + hidráulica. 4 cables de la espalda saltan con chispas leves y cuelgan con péndulo amortiguado. La cuna (cama + reposapiés) se inclina 16° y avanza 10 cm.
- [x] Función pura del tiempo (`WakeUpRobotPose.CradleTransform` compartido): scrub y salto dan el mismo estado. Fin con salto en t=7,5 s: cuna 344°, sujeciones al 12 %, cables colgando; idéntico al de la reproducción completa.

### 4. Salida con peso y sin atravesar la cápsula
- [x] Secuencia: mano izquierda en el riel → pierna derecha sale con arco por encima del borde (suela 8 cm sobre el canto) y se planta → transferencia de peso → pierna izquierda por encima del borde → se endereza. Pies plantados (reposapiés o suelo), cadera que baja y sube, polo de rodilla delante (y alto mientras el pie sigue dentro). La cadera baja sola si una pierna se fuera a estirar de más.
- [x] Medido cada 0,25 s (manos, rodillas, pies, hombros contra cajas orientadas de la cápsula): antes hasta **−22 cm** dentro de la base (`LeftToeBase/FootCap`); ahora **0 cruces**, mínimo 5,3 cm. Capturas laterales y frontales `v30a_exit_*` / `v30b_exit_*`.

### 5. Pisadas por contacto real
- [x] `FootContactFx` (nuevo): la ventana de tiempo solo arma; dispara cuando la suela está a ≤1,5 cm del suelo (raycast, solo el suelo), el pie no sube y antes se levantó >5 cm. Un disparo por pisada; polvo y chispas bajo la suela orientados a la normal, sonido metálico y sacudida pequeña (`HangarIntroCinematic.Kick`). Se quitaron las pisadas por tiempo del Timeline. Mismo sistema para los 4 re-apoyos del giro.
- [x] Antes: polvo del pie derecho con la suela a **28 cm**. Ahora: R 10,04 s (1,1 cm), L 12,04 s (0,3 cm), giros 1,0–1,5 cm. Capturas del frame exacto `v30b_contact_*`.

### 6. Cabeza, cuello y manos rígidos
- [x] `RobotRigidSkin` v30: cabeza y cuello cortados como piezas rígidas por triángulo (Head 1210, Neck 208 triángulos; 101 vértices de costura duplicados), 100 % a su hueso; el resto del cuerpo pierde su influencia. Manos y dedos rígidos por vértice (como los segmentos del brazo). Pesos ordenados (sin asserts). Malla derivada con el mismo GUID; aplicada en Room00 y en la cápsula del menú.
- [x] Aim de cabeza repartido Head 70 % / Neck 20 % / Spine2 10 %, límites: yaw ±70°, pitch −40/+30°, roll ±20°. Giro de muñeca ±50° (antes ±80°).
- [x] Pedido del usuario tras la primera versión: quitar "las cosas negras" de muñeca y cuello. Eran collares de unión; quedan desactivados (`Apply(..., withCollars:true)` los repone). Probado también cortar las manos por triángulo: dejaba el borde interior del puño con picos al girar, así que se descartó.
- [x] Capturas de cerca de cuello y muñeca en `Bridge/v30/v30b_hand_*` (antes `v30a_hand_*`).

### Verificación
- [x] JUGAR desde el menú → cinemática completa → control. Salto a mitad: mismo estado final. **CampaignTests 4/4 en verde (152 s).**
- [x] Consola: 0 errores de scripts. Quedan avisos internos de Unity (ADTM de audio, `Library/StateCache ... Sharing violation`).
- [ ] Revisar en partida real con teclado y ratón; medir en build de desarrollo.

## v31 - Combate v4: vacío bajo la cinta, slide invulnerable, Big Bot sin ejecución y más explosivo, puño cargable, inicio sin armas + estación de montaje, cinemática (2026-11-12/13)
Respaldos: `Tools/IronRequiem/Backups/20261112-fist-v4/` (los 22 scripts modificados, `BigBot_Current.prefab` + meta y este checklist, previos) y los de cada build (`<fecha-hora>-room00-hangar`: RequiemCampaign, Room00_Hangar, timeline, NavMesh, EditorBuildSettings). Nada se borró; los GUID se conservan (prefabs y materiales nuevos se crean una vez y luego se actualizan en su sitio).
Autoría idempotente: todo sale de `Tools/Iron Requiem/Hangar Intro/Build Room00_Hangar + campaign` (llama a `HangarPitAuthoring` y `CombatV4Authoring`); este último también tiene menú propio `Tools/Iron Requiem/Combat v4/Apply assets + campaign session`.

### 1. Vacío bajo y delante de la cinta (solo Room00_Hangar)
- [x] `HangarPitAuthoring` (nuevo): el suelo y su collider terminan en z local 13,2; pozo de 42 m hasta la pared trasera (z 30,5), ancho de pared a pared. Paredes con costillas, pilares del puente y columnas que siguen hacia abajo, 7 vigas (2 partidas), 4 vigas longitudinales, pasarela bajo la cinta, 18 cables colgando, 7 luces lejanas (4 puntuales naranjas sin sombras) y dos capas de niebla.
- [x] Borde claro: escalón (12 cm), franja de peligro y baranda rota (15 postes, 10 tramos; hueco abierto en el centro, un tramo doblado y otro arrancado).
- [x] Caída: `GapFallRecovery` existente (trigger a −5,5 m) → 20 de daño y vuelta a uno de 3 puntos seguros del lado del depósito. Sin orilla lejana (no se puede "ganar" cruzando). Probado: 100→80 HP, posición (0, 0, −118), en el suelo.
- [x] Chatarra: 5 pilas que caían sobre el vacío se omiten (14 quedan) y los restos sueltos no se colocan allí. NavMesh rehorneado: 10/10 puntos de chatarra en la malla; el vacío no tiene malla.
- [x] Menú (IntroMenu) no cambia: allí los escombros de las explosiones caen al piso bajo la cinta. La transición JUGAR → Room00 pasa por negro.

### 2. Slide (C) con invulnerabilidad corta
- [x] `AdvancedPlayerMovement.slideInvulnerability` = 0,42 s desde el inicio del slide → `Health.GrantInvulnerability` (misma fuente que `ExecutionProtection`; usa el máximo, nunca acorta otra). Cooldown 1,5 s + soltar C sin cambios.
- [x] `Health.OnDamageIgnored` (nuevo evento): golpes bloqueados → `SlideGuardFeedback` (chispas cian + ping, solo durante la ventana) y `SlideGuardScreenFx` (borde cian en el HUD). Sonido suave al empezar el slide.
- [x] Test con teclado real (sprint + C): golpe dentro de la ventana ignorado y reportado; al terminar la ventana el daño vuelve.

### 3. Big Bot
- [x] No ejecutable: `EnemyExecutionSource.executionEnabled=false` en `BigBot_Current` (sin [E], sin reserva, sin módulo, sin entrega de arma). Assets del arma robada intactos.
- [x] Daño por proyectil **8 → 12** (+50 %), radio de explosión **1,8 → 2,88 m**. Copia `Projectile_BigBot_Blast_v4` (el proyectil compartido no se toca) con `BigBotBlastFx`: `FX_BigBot_Blast_v4` (bola de fuego, humo, onda de choque, 60–80 chispas, escombros con colisión, quemadura 5,5 s, explosión grave 3D) y sacudida de cámara que cae con la distancia (radio 18 m).
- [x] Patrón sin cambios (ráfagas de 6, cañones alternos, emergencia a un cañón). CampaignTests comprueba que el Big Bot nunca ofrece [E].

### 4. Puño cargable (slot propio)
- [x] `Weapon_IR_Fist` (nuevo prefab): `WeaponController.PermanentMelee` (no se puede quitar ni tirar; `RemoveWeapon` lo rechaza; sin contador de munición/calor) + `FistWeaponInput` (`IWeaponInputExtension`).
- [x] M1 corto = golpe rápido (mismo que F). Mantener = carga hasta 1,2 s; soltar = golpe pesado: 95 de daño, alcance 2,1 m, empuje hasta 3 m (`ExecutionShove`), embestida 1,1 m (`AdvancedPlayerMovement.BeginLunge`), aturdimiento 0,75 s (`EnemyController.Stagger`: no dispara ni se mueve), hitstop 0,07 s. Carga completa: destello + cámara lenta leve 0,12 s al 35 % (`GameFeel.HitStopThenSlow`, se puede desactivar).
- [x] Carga: el puño izquierdo entra en cuadro y vibra cada vez más, energía cian, luz, zumbido que sube de tono, anillo de carga bajo la mira (`FistChargeHud`). Sonido metálico grave al impactar + chispas (`FX_Fist_HeavyImpact_v4`).
- [x] Reutiliza el rig del puñetazo izquierdo; la mano izquierda sigue oculta fuera del golpe. F sigue funcionando.
- [x] Probado con ratón real: carga completa → golpe pesado conecta (Carroñero 26→0).

### 5. Inicio sin armas + estación de montaje
- [x] `StartingWeapons`: [Cañón, Escopeta] → [Puño]. Los prefabs de cañón y escopeta no se borran.
- [x] `WeaponMountStation` en la esclusa del lobby (z −33,9): pórtico con pinzas, chispas, sonido y mensaje en el HUD; monta `Weapon_IR_ArmCannon_VO` una sola vez y lo selecciona. Probado: [Puño] → [Puño, Cañón].
- [x] Reinicio: recarga la campaña desde el hangar → solo puño; la estación vuelve a dar el cañón.
- [x] Dificultad del pasillo: oleada 1 = 2 T-07 + 1 Carroñero (antes 2+2), oleada 2 = 2 Carroñeros (antes 3). Los T-07 siguen siendo ejecutables.
- [x] HUD limpio con el puño (sin barra de calor ni contador).

### 6. Cinemática
- [x] Final: E_Reveal/F_ToHead ya no se alejan 2–2,6 m hacia atrás (atravesaban la cápsula); ahora sobre el hombro derecho, delante de la cápsula, y cada punto se empuja fuera del collider de la cápsula (+35 cm). Medición: **0/42** muestras tocando la cápsula. Paso a primera persona igual.
- [x] JUGAR: `HangarLaunchSequence` ya no despierta al robot a 1,8 s; `CapsuleRobotIdle.HoldDark()` lo deja apagado (sin parpadeos por explosiones) todo el acercamiento. Medido: robotDark=True de 0,3 a 2,9 s. Room00 lo enciende en el plano del visor.

### Verificación
- [x] Flujo en Play: JUGAR (visor apagado) → cinemática → final sin atravesar → hangar solo con puño → vacío (caída y vuelta) → golpe cargado → explosión del Big Bot → estación → puño + cañón. Capturas `Bridge/v4jugar_*`, `v4end_*`, `v4pit_*`, `v4punch_*`, `v4blast_*`, `v4station_*`; registro `Reports/HangarIntro/v4_play.txt`.
- [x] Perfil en Play del Editor (sin capturas en la ventana): golpe pesado peor frame 99,9 ms con la muerte del Carroñero incluida (sin muerte 35 ms); explosión del Big Bot (pool caliente) 40,8 ms; ~24 KB de GC por frame en ambos = base del Editor. La primera explosión de la sesión crea el pool (pico de varios segundos en el Editor por compilación de shaders/instancias): pendiente prewarm.
- [x] CampaignTests: **5/5 en verde (245 s)**: los 4 previos (prólogo ahora comprueba solo-puño al inicio, estación que da el cañón una sola vez, reinicio con solo puño, Big Bot nunca ejecutable) + nuevo `V4FistChargeSlideGuardAndBigBotBlast` (puño irremovible, datos del Big Bot, slide real con golpe ignorado, carga completa con ratón → golpe pesado 128→33 HP, tiempo restaurado, tap = golpe rápido).
- [x] Consola: 0 errores de scripts. Quedan avisos internos de Unity (ADTM de audio, `Library/StateCache ... Sharing violation`, token refresh).
- [ ] Probar en partida real con teclado y ratón el slide contra un ataque y el ritmo del pasillo solo con puño; medir en build de desarrollo; precalentar `FX_BigBot_Blast_v4` en la carga del sector del Big Bot.

## v32 - Golpe con la mano derecha (F), animación de la estación de montaje, opciones grandes y centradas, un solo estilo de interfaz (2026-11-13)
Respaldos: `Tools/IronRequiem/Backups/20261113-v5-melee-station-ui/` (los scripts modificados, previos) y los de cada pase (`<fecha-hora>-room00-hangar`, `<fecha-hora>-ui-unify`: RequiemCampaign, Room00_Hangar, Room00b_AlarmCorridor, IntroMenu y el tema). Nada se borró; los GUID se conservan. Autoría idempotente: `Build Room00_Hangar + campaign` (combate y estación) y `Tools/Iron Requiem/Menu/Unify UI style (all screens)` (interfaz; también lo llama `Build Industrial Options`).

### 1. Golpe con la mano derecha en F
- [x] F corto = golpe rápido; mantener F = carga (vibración, energía cian, anillo bajo la mira, golpe pesado con empuje, aturdimiento y hitstop: todo lo de v4). La carga se suelta al soltar F (`MechanicInput.MeleeHeld`).
- [x] Desarmado: la mano derecha está visible y M1 también lanza el golpe rápido. La mano izquierda vuelve a ocultarse (solo aparece al desarmar enemigos).
- [x] Armado (cañón o módulo robado en el brazo derecho): F hace el mismo golpe cargable como golpe mecánico con el antebrazo/arma (`ProtagonistMelee.ArmedStrike`, pose del socket del arma). No desequipa ni toca retroceso ni calor.
- [x] Slot del puño quitado del inventario (`StartingWeapons` vacío; `Weapon_IR_Fist` queda como asset). Q, rueda y números solo eligen slots ocupados.
- [x] HUD contextual: `GOLPE [F] / [M1] · MANTENER [F] CARGA` (o `GOLPE [F] MECÁNICO` con arma). Opciones: placa CONTROLES con F, mantener F, M1, E, Esc.

### 2. Estación de montaje animada (~5,6 s)
- [x] `WeaponMountStation` (reescrito) + `StationArmRig` (nuevo, IK analítico de 2 huesos con el codo arriba) en el prefab `MountStation_C04` (lobby, z −33,9).
- [x] E → bloqueo de input, primera persona con encuadre controlado. El antebrazo derecho sube (desde abajo, fuera de cámara), las trabas giran con vapor, chispas y sonido hidráulico; el brazo A se lleva la mano. El brazo B trae `Weapon_IR_ArmCannon_VO`: alineación, encaje, giro de traba, cápsulas encendidas, destello cian. El cañón se calienta y se enfría una vez (`WeaponHeat.PreviewHeat`) y aparece "MÓDULO DE CAÑÓN ACOPLADO". Vuelve el control.
- [x] Se salta manteniendo E o Espacio (0,6 s): mismo estado final. El arma real se añade solo en el cambio y sube al instante (`PlayerWeaponsManager.SnapActiveWeaponUp`).
- [x] Sin interpenetración con la cámara: la mano entra por un camino bajo (v5b) en vez de mezclarse desde la pose de reposo; pinzas y piezas en la capa de manos.
- [x] Reiniciar después de la estación: el cañón ya está y la secuencia no se repite (`CombatLabMenu.ResumedAfterRestart`).

### 3. Opciones grandes y centradas con VOLVER
- [x] El panel de opciones sale del marco y pasa a ser una capa de pantalla completa (`OptionsPanel` → fondo oscuro + `OptionsConsole` 1060×520) con `ScreenFitPanel`: 76 % del alto (máx. 92 % del ancho), centrado. Igual en menú principal y pausa.
- [x] VOLVER más grande (380×72, texto 30) y Escape: en el menú principal vuelve al inicio desde opciones y créditos; en pausa vuelve al inicio de la pausa sin cerrarla (`InGameMenuManager.BackHandler`). Transición suave: la de `MenuShellMotion` (fundido + deslizamiento).
- [x] Escala por `CanvasScaler` 1920×1080 (match 0,5); capturas a 1280×720, 1920×1080 y 2560×1440.

### 4. Un solo estilo de interfaz
- [x] `IronRequiemUiTheme` (nuevo ScriptableObject, `Assets/IronRequiem/Settings/UI/IR_UiTheme.asset`): paleta (cian, naranja, acero, texto, peligro, fondos), fuentes (títulos Roboto-Black, cuerpo LiberationSans), placas y sonidos de interfaz (hover, clic, volver; suenan en pausa).
- [x] Botón base: `HangarButtonFx` (el del hangar, ampliado) en todos los botones: placa biselada, barra de acento, hover/clic iguales, pequeño "squash" al pulsar, sonidos del tema. Panel base: `IndustrialPanelGraphic`. Títulos con la fuente de título.
- [x] Pantallas: menú principal, opciones, créditos, pausa, muerte (consola roja con franja de peligro y "SEÑAL VITAL PERDIDA"), banners de ronda y alertas ("ALERTA · INTRUSIÓN DETECTADA"), avisos contextuales ([E] ACOPLAR…), aviso de saltar cinemática (placa), HUD de vida, calor, módulo robado y anillo de carga del golpe (sprite de anillo nuevo `IR_UI_Ring`).
- [x] El menú de pausa de la plantilla FPS (no industrial, nunca se abre) se deja igual. La lógica de menús y el guardado de sensibilidad/opciones no cambian.

### Verificación
- [x] Flujo en Play (puente): inicio sin armas → golpe derecho con M1 y con F (carga completa: Carroñero 26→0) → pasillo → estación completa (capturas `Bridge/v5station_*`, la mano sube desde abajo sin cruzar la cámara) y saltada a ~1,5 s (`Bridge/v5skip_*`: mismo estado, cañón arriba, control devuelto) → con el cañón, F = golpe mecánico (128→33 HP, sin quitar el arma ni sumar calor).
- [x] Menú: OPCIONES grande y centrada → VOLVER → inicio; Escape desde opciones y desde créditos → inicio. Pausa → opciones → Escape vuelve al inicio de la pausa (sigue en pausa) → VOLVER igual. Registro `Reports/HangarIntro/v5_ui.txt`.
- [x] Capturas de todas las pantallas a 1080p y 1440p (opciones también a 720p), antes y después: `Tools/IronRequiem/Bridge/v5ui/before/` y `v5ui/after/` (menú, opciones, créditos, pausa, opciones de pausa, muerte, banners de ronda y alerta, aviso [E], secuencia de la estación, HUD sin arma / con cañón / con carga, aviso de saltar cinemática). Se renderizan fuera de pantalla pasando los canvas overlay por la cámara del juego (`HangarIntroDebugV5Ui`).
- [x] CampaignTests: **5/5 en verde (247,6 s)**. Cambios: el prólogo comprueba inicio sin armas, M1 desarmado con la mano derecha (izquierda oculta), E inicia la estación (input bloqueado), Espacio la salta (mismo estado, solo el cañón, sin repetir); el test del menú comprueba la consola de opciones (ajuste al 76 %/92 %, centrada, VOLVER) y que un reinicio sin estación queda sin armas; el test v4 carga con F (golpe mecánico pesado) y termina con un reinicio que conserva el cañón sin repetir la secuencia.
- [x] Consola: 0 errores de scripts. Solo mensajes internos de Unity (`Library/EditorToolsSettings.asset`, token de recarga de dominio, ADTM de audio, `StateCache ... Sharing violation`).
- [ ] Probar en partida real con teclado y ratón (golpe con F/M1, estación, menús con ratón y Escape) y medir en build de desarrollo.
- [ ] Notas: la escena de campaña tiene dos HUD activos (el de `GameManager` y uno suelto de la plantilla, con "Tab" en vez de "Esc"); se superponen exactos y no se tocaron. Las pinzas de la estación son cajas oscuras simples.

## v33 - Puño mecánico cerrado, muñeca con collar, estación con abrazaderas y botones de la pantalla de muerte (2026-11-13)
Respaldos: `Tools/IronRequiem/Backups/20261113-v6-fist-wrist/` (ProtagonistHandsPresentation, WeaponMountStation, UiUnifyAuthoring previos), `<fecha-hora>-ui-unify` (escenas antes de reaplicar el estilo) y `CHECKLIST_TRABAJO` previo. Nada se borró; los GUID se conservan. Hecho por el puente, sin terminal: falta el commit.

### 1. Puño cerrado (F y M1 desarmado)
- [x] Causa de la garra: durante el golpe el Animator pasaba a `HandPose=1` (agarre) y el clip `IR_HandFist` es solo media garra. Ahora `HandPose=0` mientras la mano es normal.
- [x] `ProtagonistHandsPresentation.ApplyFist`: curva por código las 15 falanges derechas (proximal 92°, media 104°, distal 78°, pulgar 58° sobre índice y medio), dedos juntos, muñeca recta. Cierra en 0,06 s, abre en 0,24 s. Sin asignaciones por frame. `FistClosure` público.
- [x] Tap F: puño 1,00 a los 0,1 s y golpe. Mantener F: cerrado desde el inicio de la carga, vibra con energía cian, golpe y reapertura suave. M1 desarmado igual.
- [x] Pose de carga reajustada (`ChargeW`/`ChargeE`): el puño entero queda en cuadro, abajo a la derecha de la mira, sin taparla.
- [x] Con cañón o módulo robado, F sigue siendo el golpe mecánico con el antebrazo (sin cambios).

### 2. Muñeca y WristMount
- [x] Collar `WristCollar_R_FPS` (gris oscuro) en la unión mano-antebrazo; `WristMount` público, alineado al eje del antebrazo. Con antebrazo reemplazado se coloca en `rightPort`.
- [x] El cilindro blanco (receptor) ya no aparece con la mano solo desacoplada; con antebrazo reemplazado se tiñe oscuro.
- [x] Disparo del cañón tras la estación: boca y efectos alineados (capturas `wrist_aim`, `wrist_fire_a/b`).

### 3. Estación de montaje
- [x] Antebrazo centrado en cámara (`inspectWrist`/`inspectElbow` nuevos; sube desde abajo).
- [x] Abrazaderas: cierran 0,72–0,95 s (sonido al cerrar), abren 4,6–4,85 s; se esconden al terminar o saltar.
- [x] La mano sale 0,32 s recta por el eje del antebrazo antes de que el brazo A se la lleve.
- [x] Resto igual a v32: cañón alineado, encaje lineal, giro de traba, cápsulas, calor/enfriado, mensaje, vuelta suave. Skip con E/Espacio: mismo estado.

### 4. Pantalla de muerte
- [x] REINICIAR y VOLVER AL MENÚ no respondían: el `Image` propio del botón (el que recibe el raycast) quedó desactivado y la placa hija no recibe raycast. `UiUnifyAuthoring.StyleButton` ahora lo activa (transparente, sin cull) en todos los botones. Clic real con ratón: reinicia.

### 5. Brazos del CapsuleRobotRig
- [ ] No adoptado: el rig es una sola malla rígida de todo el cuerpo; usar sus brazos rompía sockets, `rightPort` y escala del cañón. Las piernas del slide tampoco se cambiaron.

### Verificación
- [x] Capturas `Tools/IronRequiem/Bridge/v6/after/`: puño (reposo, tap a–d, carga inicio/media/completa, impacto, retorno, fin), armado (mismo set con cañón), estación (`station_t0.42`…`t4.13`, `station_done`), muñeca (reposo, apuntando, disparo, slide). Menús de depuración `HangarIntroDebugV6`, informe `Reports/HangarIntro/v6.txt`.
- [x] CampaignTests: **6/6 en verde (235,9 s)**. Nuevos: `DeathScreenButtonsReceiveClicks` (cada botón es el primer impacto del raycast) y en el prólogo `FistClosure > 0,95` a 0,1 s del golpe.
- [x] Consola: 0 errores.
- [ ] Probar en partida real con teclado y ratón. Pinzas de la estación siguen siendo cubos.

## v34 - Puño con forma real (dedos en la palma) y F otra vez con la mano izquierda (2026-11-13)
Respaldos: `Tools/IronRequiem/Backups/20261113-v7-fist-left/` (ProtagonistHandsPresentation, ProtagonistMelee, WeaponMountStation, CampaignTests, HangarIntroDebugV6, MechanicHud previos) y el CHECKLIST previo. GUID conservados. Hecho por el puente, sin terminal: falta el commit.

- [x] Puño v33 descartado: giraba cada falange con el eje del clip de garra y los dedos quedaban como garra o se doblaban hacia atrás.
- [x] `HandFistRig` (nuevo, `Scripts/Player/HandFistRig.cs`): bisagra única en la línea de nudillos, dedos juntos (0,85), nudillo 92° / medio 106° / punta 72°, pulgar por CCD sobre índice y medio. Pose cerrada calculada una vez; por frame solo mezcla, sin GC. Lado de la palma del rig: derecha +1, izquierda −1 (comprobado en primeros planos).
- [x] F (tap y carga) = mano izquierda, con o sin cañón (`ProtagonistMelee.LeftStrike`). M1 desarmado = golpe rápido con la derecha. Golpe mecánico con el antebrazo desactivado.
- [x] HUD `GOLPE IZQ. [F] · DER. [M1] · MANTENER [F] CARGA`; mensaje de la estación `[F] GOLPE (MANO IZQUIERDA)`.
- [x] Capturas `Bridge/v6/after/v7_left_*`, `v7_right_*` (frente, lado, arriba, interior, fondo liso) y `v7_left_charge_game`, `fist_*` (vista de juego). Menú `V7 fist closeups (play)`.
- [x] CampaignTests **6/6 en verde (270,7 s)**; consola 0 errores.
- [ ] Pulgar: en algunos ángulos queda algo levantado. Probar en partida real.

## v35 - Transiciones más ligeras, suelos coherentes, salas abiertas y decoradas por sector (2026-11-13/14, modo autónomo)
Respaldos: `Tools/IronRequiem/Backups/20261113-v35-levels-perf/` (CampaignFlow, CombatTwoRoomsLab, escenas de Room01–04, pasillo y RequiemCampaign, CHECKLIST previo). GUID conservados (NavMesh y sondas reescritos en sus mismos assets). Piezas sustituidas en la raíz inactiva `Quarantine_v35` de cada escena; nada borrado. Sin Codex ni commits.

Herramientas nuevas (menús `Tools/Iron Requiem/…`):
- `Perf/…`: tour automático en Editor o en un Player Development (Autoconnect Profiler). Mide por transición pico, frames >33/>50 ms, GC, tiempos de puerta/carga/activación/descarga/asentado y los tres frames más largos con su fase; por sala, la media, el p99 y el máximo en combate. `-irMarkers` añade los marcadores del profiler que más tiempo gastan (media y en el pico). Informe en `Builds/IR_Tour/ir_tour.txt`.
- `Levels/Audit + captures (read only)`, `Levels/Dump geometry (read only)`.
- `Levels/v35 Open + dress rooms (writes scenes)` (idempotente) y `Levels/v35 Rebuild floor guide`.

Rendimiento de transiciones (causas medidas y arregladas):
- [x] Glifos TMP dinámicos: un nombre de sector o "PREPARANDO…" nuevo rasterizaba glifos, subía el atlas y reconstruía el canvas en el frame de activación (≈40 ms en Room01). Ahora se precargan todos los glifos del español en las fuentes de la sesión, detrás de la portada (solo en Player).
- [x] La sala vieja se destruye por tramos de unos 350 transforms por frame, siempre con la puerta cerrada. Después `UnloadSceneAsync` libera una escena vacía.
- [x] La sala nueva se dibuja una vez, con la puerta cerrada, desde una cámara oculta de 320x180 (un sexto por frame). Así se preparan sus shaders y texturas antes de que el jugador la vea.
- [x] Subidas asíncronas: búfer de 32 MB persistente y 3 ms por frame.
- [x] Se mantienen las reglas del airlock: doble puerta, y descarga solo con la puerta cerrada y el jugador dentro.

| Transición (Player dev, 1600x900) | Pico antes (ms) | Pico después (ms) |
|---|---|---|
| Hangar → pasillo | 69,9 | 32,4 |
| Pasillo → Room01 | 50,6 | 35,8 (31,0 en la siguiente pasada) |
| Room01 → Room02 | 21,1 | 31,2 |
| Room02 → Room03 | 19,8 | 29,8 |
| Room03 → Room04 | 25,2 | 29,1 |
| Room04 → Pursuit | 39,5 | 25,9 |

Combate (sala | media ms | p99 ms) en la última pasada: Room00b 13,7 | 21,8 · Room01 14,8 | 20,6 · Room02 13,3 | 18,8 · Room03 14,8 | 19,5 · Room04 14,3 | 18,8. Sin combate: Hangar 14,6 · Pursuit 10,6. Antes: media 6,7–7,6 y p99 10,4–11,3.

- [ ] **Base de frame duplicada en todo el recorrido**, incluso en el hangar y en Pursuit, que no se tocaron. Por eso parece una causa del equipo (energía o temperatura del portátil, o el Editor abierto) y no de las salas. El hilo principal llena el frame y la GPU va casi ociosa (≈1,1–1,6 ms). Las subidas de Room02–04 (de 21–25 a 29–31 ms) siguen esa base, porque el sobrecoste del pico sobre la base bajó. Repetir antes y después con el portátil enchufado, en modo Alto rendimiento y con el Editor cerrado.
- [ ] Los picos que quedan son `Resources.UnloadUnusedAssets` (≈17–23 ms, con la puerta cerrada) y el JIT de Mono la primera vez que se ejecuta un código. IL2CPP quitaría el JIT; no se cambió el backend.
- [ ] El Player sigue en 1600x900: el juego restaura su tamaño de ventana. No se midió a 1080p y no se alcanza el objetivo de 120 FPS con la base actual.

Suelos:
- [x] Auditoría de juntas: no hay escalones accidentales. Los escalones de 20 cm de la escalera Room01→Room02 quedan cubiertos por la rampa invisible `V35_StairRamp` (14°) en `PersistentAirlocks`.
- [x] Galones de guía emisivos cada 5 m a lo largo de la ruta, apuntando a la salida, más un marco emisivo en cada salida. Bordes de las pasarelas con franjas de peligro. Los materiales solo cambian en los umbrales.

Salas (techo 7–8 m → 12–13 m, pasarelas a 3,8 m con rampas de ≈19°, coberturas agrandadas, decoración sin colisionador):
- [x] Room01 Fundición: crisoles con metal fundido, bocas de horno y regueros naranjas. 104 piezas, 12 galones.
- [x] Room02 Brecha: vigas caídas, grieta en el techo, placas colgantes y varillas sobre el vacío. 98 piezas y 3 pasarelas.
- [x] Room03 Ensamblaje: brazos robot en el techo, cinta aérea con chasis y racks de cabezas. 180 piezas; 4 de 12 spawns movidos fuera de las pasarelas.
- [x] Room04 Reactor: columna de energía cian, anillos, colector y conductos. 90 piezas.
- [x] Comunes: pilastras, rieles de grúa, ventanas nocturnas, ventiladores, pórtico con cadena y gancho, cables. Luces teñidas por sector (mismo número e intensidad, sin sombras, como exigen los tests). NavMesh y sondas recocinados en sus assets. Botiquines movidos a las pasarelas.
- [x] Capturas antes/después de cada sala, desde la entrada y desde un punto alto: `Tools/IronRequiem/Bridge/v35/before_play/` y `after_play/`.
- [ ] Hangar sin abrir; el pasillo solo tiene galones. No hay luz horneada ni oclusión. Faltan ShaderVariantCollection y LOD dedicados para la decoración.

Verificación: tour en Editor y en Player completo en todas las salas (todos los slots alcanzables), CampaignTests **6/6 en verde** y consola sin errores.

## v36 — Poses absolutas, brazos completos, HUD clásico y entrada animada (2026-11-15)

Respaldos: `Tools/IronRequiem/Backups/20261115-hands-hud-campaign/` y `20261115-animated-menu-entry/`. Detalle y límites: `HANDS_HUD_CAMPAIGN_DIAGNOSTIC_20261115.md`. Sin commits, paquetes nuevos ni Meshy.

- [x] HandPoseController como autoridad final de dedos en campaña y cinemática; bibliotecas Open/Relaxed/Fist/Grip/Inspect de matrices bind por rig. Muestreo de timeline independiente del frame anterior.
- [x] Plan B autorizado: puños rígidos L/R hechos en Blender, collar; geometría 2.484 tris, pulgar externo a 2 mm y distal-palma a 9,5 mm. Huesos/piezas sin escala animada.
- [x] Hombro/brazo superior/codo de CapsuleRobotRig en copias FPS, siguiendo el brazo y conservados con cañón. F armado vuelve al golpe de antebrazo derecho; izquierda oculta sin acción.
- [x] Barra inferior uGUI industrial, números bitmap y visor reactivo; energía/salud/slots/carga/OVERDRIVE, prompts/retícula/banners conservados. Raíz GameHUD activa.
- [x] Carga anticipada del siguiente sector, activación retenida, teardown repartido y precalentamiento; sin UnloadUnusedAssets manual durante gameplay. Drain antes de descarga de pruebas/reinicio. Se conserva ventana; no se declara residencia completa ni Memory Profiler medido.
- [x] Plano tras el hombro siguiendo el giro y comprobación de obstáculos; poses Inspect/Grip/Relaxed con blend. E/Espacio convergen al mismo estado.
- [x] EditMode poses **3/3** y PlayMode **8/8** (siete CampaignTests + cinemática completa/saltos), sin errores registrados por la ejecución. Incluye inputs reales M1/F/E, cañón, extracción, OD, estación, reinicio y recorrido Room00–Pursuit05.
- [x] Petición posterior del usuario: IntroMenu animado restaurado como inicio de Play. Pantalla antigua retirada de campaña y conservada en cuarentena fuera de Assets; pausa/muerte conservadas. IntroMenu y EditorBuildSettings idénticos al respaldo de la reparación. Movimiento de personajes/cámara medido; flujo JUGAR/reinicio/volver probado.
- [ ] Cierre visual final de las capturas de puños y HUD 720/1080/1440 tras ajustar la herramienta de captura.
- [ ] Player reparado medido a 1080p y comparación de picos/promedio/p99 con baseline real 1600x900. Objetivo 33 ms no certificado todavía.

Las anotaciones de versiones anteriores sobre HandFistRig y el HUD describen estados históricos. Para esta campaña prevalece Contexto_Animaciones.md actualizado.

## v37 — Estación de montaje (cambio de mano por el cañón) y HUD que no tape las armas (16/11/2026)

Petición directa: «mejora la animación de cambio de mano al arma principal del robot que está después del pasillo en el hangar» + prompt adjunto con dos cambios autorizados (estación de montaje y HUD que tapa las armas). Respaldos y hashes: `Tools/IronRequiem/Backups/20261116-station-hud/`. Sin commits, paquetes, escenas ni prefabs guardados; GUID conservados. `UiUnifyAuthoring.cs`, `MechanicHud.cs` y `MountStation_C04.prefab` idénticos al respaldo.

El «rifle» es `Weapon_IR_ArmCannon_VO`, que monta `WeaponMountStation` (`MountStation_C04`, lobby de `RequiemCampaign`, tras el pasillo del hangar). No es el cambio de arma 1/2 de `PlayerWeaponsManager`.

### 1. Estación de montaje
Reproducción (Play, E real, tiempo ×0,1, telemetría por frame `Reports/StationHud/<before|after>/station-telemetry.csv`, capturas `station/station_<t>_*.png`):
- [x] Causa principal: la estación hacía `LateUpdate` (orden 0) antes del IK de `ProtagonistHandsPresentation` (110) y colocaba abrazaderas, anillo de traba, pinza del brazo A y el proxy del cañón con la pose de la mano **sin IK**. Quedaban ~1,6 m por encima de la cámara: entre 1,0 y 4,6 s solo se veía el antebrazo desnudo y el cañón **aparecía de golpe** en el swap.
- [x] El cañón real reproducía al equiparse su `WeaponDeployMotion` (largo 12 %→100 %, giro −18°, rebote): deriva de 11,2 cm tras el swap.
- [x] La toma de control inicial pasaba la mano por el centro del encuadre (16 frames a ×0,1).

Corrección (`Scripts/Levels/WeaponMountStation.cs`):
- [x] `[DefaultExecutionOrder(1100)]`; el override del brazo y el encuadre se calculan en `Update` (`DriveArm`, l. 162) antes del IK; los objetos que siguen al antebrazo se colocan en `LateUpdate` con la mano ya resuelta. Sin segundo escritor de dedos (`HandPoseController` sigue siendo el único).
- [x] El cañón se acopla como manga: su `Attachment` queda `MountedHandLead` detrás de la mano (l. 231), igual que tras el montaje. El descenso termina en la pose exacta que calcula la presentación con el arma montada (`PortPose`, l. 298) y el proxy se asienta en el transform donde nace el arma real (`DefaultWeaponPosition` + rotación del socket). Swap: proxy 0 mm del arma real, mano inmóvil durante el fundido del override.
- [x] El arma montada salta su despliegue genérico (`SkipNextDeploy`, l. 343). La recogida/cambio normal conserva el despliegue.
- [x] Asentamiento: muelle amortiguado al encajar (TSlide, 45 %) y al bloquear (TLock), 1,6 cm hacia atrás y 0,5 cm arriba, ~0,35 s (`Kick`, l. 188). Vibración de las trabas con envolvente (sin escalón).
- [x] Toma de control por debajo/detrás de la cámara (0,16 s): 0 frames con la mano dentro del encuadre.
- [x] Duración total sin cambios (5,6 s); salto con E/Espacio y reinicio iguales.
- Cambios mínimos de soporte: `ProtagonistHandsPresentation.MountedHandLead` (propiedad de solo lectura, l. 108) y `WeaponDeployMotion.SkipNextDeploy` (l. 36).

### 2. HUD que tapaba las armas
Medición (`Reports/StationHud/<before|after>/hud/measure.txt`, composición juego + overlay FPS + UI y máscara de solape `*_overlap.png`; 1920×1080, 2560×1440, 1920×1200 16:10, 2560×1080 21:9):
- [x] Antes, la barra de ancho completo (180 px ref.) tapaba el 75 % del cañón (83 % a 21:9), 73–80 % del puño cargando, 52–66 % del cañón robado y 42–55 % de la pistola. El viewmodel ocupa siempre x ≥ 55 % del ancho y hasta ~37 % del alto.
- [x] Solución menos invasiva: misma barra, mismos datos y estilo, convertida en bloque izquierdo (53 % del ancho; CARGA PUÑO y OVERDRIVE apilados en la última columna). Layout en código (`RetroStatusBar.cs`, l. 20 y 37), sin guardar escenas. Sin cambios de cámara, FOV ni viewmodel.
- [x] Después: 0 % de la barra sobre cañón, pistola, puño y cañón robado en las cuatro resoluciones. Retícula, prompts, banners, números 5×7 y retrato conservados.
- [ ] Queda 1–6 % de solape con el medidor térmico preexistente (`WeaponHeatHud`, esquina inferior derecha, antes oculto bajo la barra). No se tocó.

### Verificación
- [x] Compilación: 0 errores (solo avisos CS0618 preexistentes).
- [x] EditMode poses **3/3** (`Reports/HandsHudCampaign/poses-tests.xml`, 16/11 13:02).
- [ ] PlayMode CampaignTests + HangarIntroPresentationTests: en la ejecución de las 13:02 pasaron los 7 CampaignTests (estación con E real y salto con Espacio incluidos); se abortó al empezar HangarIntroPresentationTests porque se detuvo el Play. Las reejecuciones chocaron con otra sesión que usaba el mismo Editor. Falta una ejecución completa limpia (XML nuevo).
  - Revisión de la sesión «Sprites / Models», 13:33–14:04, sobre `Bridge/console.txt`:
    - Los fallos de TwelveRounds (13:38:05 y 13:52:46) y de Presentation (13:52:06) coinciden en el mismo frame con la IOException de `VisualOverhaulDriver.cs:45` (heartbeat.txt).
    - Presentation pasó a las 14:01:12. En cada pasada la estación registró «Weapon_IR_ArmCannon_VO mounted». No hay ninguna aserción sobre WeaponMountStation ni sobre RetroStatusBar.
    - NullReferenceException en `MenuFlicker.cs:73` (13:33:50), ajena a esta tarea.
    - Skills registradas en `SETUP_REPORT.md`.
  - Reejecución desde un segundo chat, con el puente inactivo 6 min antes (16/11, 13:45 y 13:50): intento 1 **7/8**, intento 2 **6/8** (`Reports/HandsHudCampaign/campaign-tests-attempt1.xml`, `campaign-tests.xml`). Los fallos (`TwelveRoundsAirlocksInventoryAndPursuit` en ambos; `PresentationHandsMountedArmsAndOverdriveHud` en el 2) no son aserciones del juego: `VisualOverhaulDriver.cs:45` escribe `Reports/VisualOverhaul/heartbeat.txt` cada segundo sin try/catch, Windows devuelve IOException 1224 (archivo abierto por otro proceso) y el Test Framework lo cuenta como log no esperado. La cinemática completa con saltos (HangarIntroPresentationTests) pasó en ambos. No se marca como superado: ninguna ejecución dio 8/8.
- [ ] Cambio de armas 1/2 por entrada real: pistola→cañón con `1` verificado (antebrazo sustituido, socket animándose); el resto de la prueba quedó invalidado por la colisión con la otra sesión.
- Skills: plugin oficial de Unity ya instalado (ui, ui-ugui, unity-cli usadas; craft-game-floors disponible para el suelo del hangar). Catálogo sin skills oficiales nuevas de animación FPS. game-dev-combat-animation (local) solo aporta método (línea base, medir antes de mirar); sus scripts son de otro proyecto web y no se ejecutaron. caveman (tercero) no instalado.
- [x] Play Mode manual por el puente: estación con E real completa ×3 (antes/después), cañón, puño cargando, cañón robado y pistola medidos; salida de Play sin guardar.
- [ ] Prueba con teclado y ratón físicos por el usuario.

Heartbeat (16/11, 13:55, autorizado por el usuario «Sí, arréglalo»; respaldo `Backups/20261116-station-hud/VisualOverhaulDriver.cs`): la escritura de `VisualOverhaulDriver.cs:46` queda dentro de `try { } catch (IOException) { }`. Compila y escribe con normalidad. La reejecución de las 13:58 no dio veredicto: a las 14:01–14:03 otra sesión creó scripts en `Scripts/UI` (FistChargeHud, FistChargeRadialGraphic, OvalFrameGraphic, RoundedFrameGraphic, IronRequiemUiThemeLink), Unity recompiló en Play y el runner abortó (NullReference tras la recarga). Revisión independiente (solo lectura): el orden 1100 de la estación respeta la autoridad de poses y no hay escritores nuevos de dedos; `UiUnifyAuthoring.cs`, `MechanicHud.cs` y `MountStation_C04.prefab` idénticos al respaldo. Hallazgos abiertos: (D1) las medidas/capturas del HUD (12:46) no corresponden a `RetroStatusBar.cs` actual (modificado 13:31 y 14:04; «[F] MANTENER»; la comprobación de ajuste de texto registró «no RetroStatusBar»); (D2) el puño izquierdo (F sin arma, `ChargeW` en `ProtagonistHandsPresentation.cs:331`) no se midió y probablemente cae bajo el bloque izquierdo; (D3) el catch no cubre `UnauthorizedAccessException` ni `Directory.CreateDirectory` (l.43); (D4) `WeaponDeployMotion.SkipNextDeploy` (l.36) puede dejar `skipNext` activo si se llama con el arma ya desplegada (hoy ningún llamante lo hace); (D5) `PortPose` asume la rama `actionsEnabled && nativeAttachment`. Faltan tests de: secuencia completa sin saltar, despliegue saltado vs. conservado, muerte/pausa durante la secuencia y layout del HUD.

#### Cierre de verificación (16/11, 14:09–14:22, una sola sesión en el Editor)
Por petición del usuario («Esta, para la otra») se pausaron las sesiones «UI/ UX» y «Skills, animación de la estación…». UI/ UX había reescrito `RetroStatusBar.cs` en dos columnas laterales (ARMAS a la izquierda, SALUD/ESTADO DEL CUERPO/CARGA PUÑO a la derecha, retrato abajo al centro) y añadido `FistChargeRadialGraphic`, `RoundedFrameGraphic`, `OvalFrameGraphic`, `IronRequiemUiThemeLink`, `Editor/HudRedesignTools.cs` y `Resources/IronRequiem/HudThemeLink.asset` (respaldos `Backups/20261116-hud-text/` y `20261116-hud-redesign/`). Lo que sigue verifica ESE HUD, no el bloque izquierdo descrito arriba en el punto 2.
- [x] `VisualOverhaulDriver.cs:43-48`: `CreateDirectory` y la escritura del heartbeat dentro de `try`, con `catch (IOException)` y `catch (UnauthorizedAccessException)` (D3 cerrado).
- [x] Compilación: 0 `error CS`.
- [x] PlayMode CampaignTests (7) + HangarIntroPresentationTests (1): **8/8 superados** (14:18, sin errores capturados). `Reports/HandsHudCampaign/campaign-tests-1418-8of8.xml`. Incluye estación con E real y salto, cinemática completa/saltos, puño cargado, menú, muerte y recorrido completo.
- [x] Play Mode por el puente (RequiemCampaign, cinemática saltada): medición viewmodel vs. UI opaca en 1920×1080 / 2560×1440 / 1920×1200 / 2560×1080 (`Reports/StationHud/after-text/hud/measure.txt` y PNG con `_overlap`): pistola **0 %** en las cuatro; cañón montado 0,9 / 0,9 / **5,0** / 0 %; antebrazo armado cargando (F) 8,4 / 8,5 / **12,1** / 0 %; puño izquierdo cargando sin arma (D2) 5,1 / 5,1 / 4,5 / **9,2** % (retrato central). Entrada real 1 → pistola y 2 → cañón con antebrazo sustituido. Salida de Play sin guardar.
- [ ] Solapes restantes: el medidor térmico (`WeaponHeatHud`, esquina inferior derecha) y la parte baja de la columna derecha tocan el cañón y el antebrazo cargando, sobre todo a 16:10; el retrato central toca el puño izquierdo. En 16:10 la notificación «ARMA OBTENIDA · …» se superpone a «FRÍA / ESTABLE» en la columna izquierda.
- [ ] Ajuste de texto: 4 etiquetas por resolución desbordan su rect en ≤1 px de ancho («T07 TEMPORARY OPTIMIZED» ×2, «BRAZO DER.», «BRAZO IZQ.»; `after-text/hud/text-fit.txt`).
- [ ] El estado desarmado en reposo mide 0 px de viewmodel: la herramienta no captura la mano derecha en reposo; sin dato.
- [ ] Capturas antes/después de la secuencia de la estación con el código final: siguen siendo las de 12:40 (`Reports/StationHud/<before|after>/station/`).
- Editor: fuera de Play, con RequiemCampaign abierta (sin cambios). No se restauró IntroMenu porque la escena previa registrada era la temporal del Test Runner.

#### Corrección de solapes del HUD (16/11, 14:22–14:45; petición «Sí, corrígelos»)
Respaldo y hashes: `Tools/IronRequiem/Backups/20261116-hud-overlap-1610/` (RetroStatusBar.cs, WeaponHeatHud.cs y sus .meta). Solo código en `Scripts/UI`; sin escenas, prefabs, cámara, FOV ni viewmodel. El retrato central (diseño de UI/ UX) no se movió.
- [x] `RetroStatusBar.BuildBodyColumn`: el marco de la columna derecha rodea solo `BodyContent`; la franja inferior queda sin ningún píxel de UI.
- [x] `RetroStatusBar.PlaceMechanicTexts`: el panel de munición de la plantilla (`WeaponHUDManager`, esquina inferior derecha sobre el arma) pasa a la banda vacía de ARMAS bajo los 9 slots (`AmmoTop` 290, `AmmoHeight` 130); `NotificationsRect` («ARMA OBTENIDA · …») pasa junto a la columna izquierda, encima de MechanicStatus (`NotificationPosition`). Se localizan por nombre porque `IronRequiem.Runtime` no referencia `fps.UI` (no se tocó el asmdef).
- [x] `WeaponHeatHud.Dock(...)` (nuevo, solo en ejecución) y llamada desde `RetroStatusBar.Start`: el medidor térmico, si existe, se acopla bajo `BodyContent`. En RequiemCampaign no hay WeaponHeatHud: esa ruta no se ejercitó en Play.
- [x] Compilación 0 `error CS`. Medición en Play (RequiemCampaign, `Reports/StationHud/after-text/hud/`; antes: `hud-before-1610/`, intermedia: `hud-fix1/`), 1920×1080 / 2560×1440 / 1920×1200 / 2560×1080: pistola 0/0/0/0 % (antes 0/0/0/0); cañón montado **0/0/0/0 %** (antes 0,9/0,9/5,0/0); antebrazo armado cargando **0/0/0/0 %** (antes 8,4/8,5/12,1/0); puño izquierdo cargando 3,1/3,1/2,7/5,6 % (antes 5,1/5,1/4,5/9,2; lo que queda es el retrato central). Notificación ya no pisa «FRÍA / ESTABLE».
- [x] Repetición 17:57–18:02 (sin cambios de código desde 14:40, Editor libre): `TwelveRoundsAirlocksInventoryAndPursuit` aislado **superado** (168 s) y `ThermalHudRepairTests` **3/3** (incluye HUD térmico visible y enlazado en 7 laboratorios). `Reports/VisualOverhaul/tests-pursuit1758.xml` y `tests-thermal1758.xml`. El fallo de 14:44 fue intermitente (espera de movimiento en la persecución), no del HUD.
- [ ] PlayMode campaña tras el cambio (14:44, primera ejecución): **7/8** (`campaign-tests-hud1610.xml`). Falla `TwelveRoundsAirlocksInventoryAndPursuit` en la espera de movimiento «Approach shifted pursuit gap stage=6» (persecución final), no en una aserción de HUD; ese test pasó a las 14:18 con el HUD anterior. La repetición aislada no llegó a ejecutarse: se canceló porque el Editor quedó en uso (Play en Room00_Hangar, fuera de tests, ~16:47).
- [x] (sustituido por la repetición 3/3 de 18:02) `ThermalHudRepairTests` (16:41 → watchdog 16:45 tras ~2 h): ningún test terminó, resultado inconcluso; el Editor probablemente quedó sin foco o suspendido y el tiempo de juego no avanzó.
- [ ] Texto: siguen los 4 desbordes ≤1 px por resolución de la barra; además el informe ahora incluye «R»/«Reload» del contador de la plantilla, que pasa a colgar de la barra (desborde preexistente de ese prefab).

Pendientes anotados (no corregidos): medidor térmico en la esquina; al soltar las abrazaderas el antebrazo blanco cambia a la manga negra del módulo en el mismo frame del swap; el cuerpo del brazo B tapa el centro del encuadre ~0,4 s al bloquear; mejora del suelo/mapa del hangar pedida en el chat (Room00_Hangar tiene cambios sin commit: no se guardó).

## v38 — Menú: etiquetas Dymo sobre chapa, interruptor de pantalla completa y botón VOLVER (16/11/2026)

Petición directa: arreglar la copia fantasma de la palanca de PANTALLA COMPLETA, cambiarla por un interruptor, investigar VOLVER y rediseñar los botones del menú como etiquetas de etiquetadora (concepto E). Respaldos: `Tools/IronRequiem/Backups/20261116-hud-text/` y `20261116-hud-redesign/`. Sin commits, escenas ni prefabs guardados.

- [x] Palanca: era un Toggle con la imagen "palanca abajo" siempre visible y "palanca arriba" encima. `OptionsSwitchVisual` la sustituye en tiempo de ejecución por un interruptor (`OptionsSwitchInstaller` lo añade al cargar escena; `RequiemMenuView` vive en `fps.UI` y no puede ver `IronRequiem.Runtime`).
- [x] Botones: `IndustrialPanelGraphic` gana los estilos Label (cinta Dymo) y Sheet (chapa con remaches y franja de peligro); `HangarButtonFx` los usa (`HangarButtonFx.DymoStyle = false` devuelve el aspecto anterior) y oculta `MenuOnlyMetal`/`MenuOnlyLamp`.
- [x] `HangarButtonFx.OnDisable` limpia el estado de pulsado (VOLVER se ocultaba con el efecto activo).
- [x] Verificado en Play (IntroMenu, capturas `Tools/IronRequiem/Bridge/dymo_home2.png` y `dymo_options2.png`): compila, interruptor por ratón virtual, 4 de 4 ciclos OPCIONES→VOLVER.
- [ ] VOLVER roto: no se reproduce en el Editor; pendiente de confirmar en el build y tras activar pantalla completa.
- [ ] Sin revisar: menú de pausa, pantalla de muerte/Win/Lose con el nuevo estilo y hover con ratón físico.

## IR-09 v03 — protagonista v02 refinado y rediseño de armas v01 (16–17/11/2026)

Autorización directa del usuario: A) refinar el protagonista (auditoría, refinado, manos modulares y estándar de acople) y B) rediseñar las armas, con pausa de aprobación tras B1. Elegidas: P-A, PR-C y C-A, sobre los prefabs de campaña. Para el puño, acortar las falanges 2 y 3. Respaldo en `Tools/IronRequiem/Backups/20261116-protagonist-v02/`. Informes: `PROTAGONIST_V02_REFINE.md`, `WEAPONS_REDESIGN_V01.md` y `Tools/IronRequiem/Reports/IR09_V02_Review/review.md`.

- [x] Auditoría A1 en solo lectura: 15 errores priorizados y 54 renders previos (`Reports/ProtagonistV02/Audit/`). Inventario de armas y contrato de acople (`Reports/WeaponsRedesign/Audit/`).
- [x] Estándar IR-ACOPLE-01: socket `ModuleSocket_R/L` con +Z distal, brida Ø 0,078, espiga Ø 0,064 con 3 mm de solape, interfaz del módulo Ø 0,066 en z = 0 y un único `Attachment`.
- [x] Protagonista v02 en tres iteraciones, visibles en la sesión de Blender:
  - Manos rígidas por piezas con bisagras y un hueso por vértice.
  - Muñeca según el estándar, codo entero y sockets únicos.
  - LOD regenerados: FPS 8.768 de 9.000; LOD0 23.480 de 26.000.
- [x] Exportación v02 con cinco clips de pose.
- [x] En Unity:
  - Variantes `Protagonist_FPS_v02` y `Protagonist_Body_v02`.
  - Bibliotecas `HandPoses/V02` horneadas desde los clips.
  - `Player_ModularArm_ProtagonistV02` y `ProtagonistV02Lab`.
- [x] B1: nueve direcciones; el usuario eligió tres.
- [x] B2:
  - Tres armas con script, tres LOD, texturas 1024 y emisivos de calor y carga, dentro de presupuesto.
  - Variantes de `Weapon_IR_ArmCannon_VO`, `Weapon_Shotgun_ArmModule_VO` y `Weapon_T07_Temporary_Optimized`.
  - `WeaponsRedesignLab`.
- [x] Código: socket opcional en `StolenShellFit`/`StolenChargeAnchor`; sin cambio con el campo vacío.
- [x] PlayMode, entrada virtual: **3/3 aprobadas** (`WeaponsRedesignTests` ×2 y `ProtagonistV02Tests`).
  - Acople a 1 mm y asiento del cañón a 0,00 mm.
  - Disparo y disparo cargado.
  - Cambio de arma.
  - Golpes F y M1 con puño ≥ 0,9.
- [x] Hashes protegidos 28/28 OK. La fuente del T-07 está intacta. Ningún prefab ni escena existente cambió; no se instalaron paquetes ni se hicieron commits.
- [ ] Prueba manual en Play: vista FPS, estación mano → arma, puño cargado, cinemática del hangar, recogida y lanzamiento.
- [x] Receptor `Forearm_R_Receiver` v02 para el jugador v07 (17/11, iteración 4).
- [ ] Puño de medio y anular hasta PIP 95°.
- [x] Costuras de codo, palma y muñeca, y espejo L/R por LOD (17/11, iteración 4; FPS 8.964).
- [ ] Texel de las manos y del cañón.
- [ ] PR-C: calor por aleta (requiere shader) y encuadre bajo (requiere autorizar un cambio de viewmodel).
- [x] Estación de los laboratorios con el arma rediseñada.
- [x] Ventana de carga del cañón animada (`ChargeWindowGlow`, verificada en PlayMode).
- [ ] Integración en campaña tras la aprobación del usuario.
- [ ] Limpieza manual de `Assets/InitTestScene*.unity` y de `C:\renders`.


## IR-03 / IR-07 — Auditoría de mapa y efectos, y lote M1+E1+E2+B1 (17/11/2026)

Informe completo y tabla de propuestas: [MAP_FX_AUDIT_20261117.md](MAP_FX_AUDIT_20261117.md). Respaldo y hashes: `Tools/IronRequiem/Backups/20261117-mapfx-phase2/`. Capturas: antes en `Tools/IronRequiem/Reports/MapFxAudit_before_20261117/`, después en `Tools/IronRequiem/Reports/MapFxAudit/` (mismos nombres).

- [x] **M1. Grado de color por sala.** El Volume `VisualPass_SectorGrade` se quedaba en weight 0 en todas las salas. `VolumeFadeIn` guarda ahora el peso autorado en `Awake` y `ZoneEnvironment.Bind` lo reclama.
  - Medido en Play: el peso pasa de 0 a 1,00 en unos 1,6 s en Room01 y Room02, y en Room02-05 es 1,00 al entrar.
  - Archivos: `Scripts/VisualPass/VolumeFadeIn.cs` y `Scripts/Levels/ZoneEnvironment.cs`.
- [x] **E1. Explosión de muerte.** La cadena `RobotBodyBurst_CombatPolish_VO_VO_VO` iba a ×36,6 del original (ImpactFlare a 220 m, ImpactRing a 110 m), y su partícula raíz opaca `TrailDeath` producía los cuadrados negros.
  - Nuevo prefab `Prefabs/VisualOverhaul/VFX/RobotBodyBurst_CombatPolish_IR.prefab`: variante del original con tamaño ×1,35 y velocidad ×1,15 una sola vez, con topes.
  - HoverBot_Current, Turret_Current, T07_Mech y T07_Current_Repaired apuntan ahora a `_IR`. Los `_VO*` se conservan sin cambios.
  - `VisualOverhaulEnemies.cs` ya no acumula multiplicadores al reejecutarse. Herramienta nueva: `Editor/MapFx/IronCampaignMapFxDeathFx.cs`.
  - El `DeathVfx` de plantilla no se anula, porque nunca se instancia: la presentación devuelve true.
- [x] **E2. Hangar en juego.** Nuevo perfil `Resources/IronRequiem/HG_GameplayVolume.asset`: CA 0, grano 0,1, viñeta 0,2, bloom 0,25.
  - `ZoneEnvironment` lo sustituye en runtime solo en las etapas de CampaignFlow.
  - IntroMenu y la escena no cambian. Herramienta: `Editor/MapFx/IronCampaignMapFxHangar.cs`.
- [x] **B1. Resolución nativa** (causa probable de IR-03).
  - `PlayerSettings.defaultIsNativeResolution` pasa de 0 a 1.
  - En build, `RequiemSettings` aplica `SetResolution(nativa, FullScreenWindow)` al activar la pantalla completa y al cargar el menú con esa preferencia. En el Editor no cambia nada.
  - Herramienta: `Editor/MapFx/IronCampaignMapFxDisplay.cs`.
- [x] **Compilación** sin errores.
- [x] **PlayMode.** CampaignTests 7/7, MapFxAudit 4/4, MenuWeaponRegressionTests 3/3 y ThermalMenuTests: 4 de 6 aprobadas.
  - Los 2 fallos de ThermalMenuTests (`EscapePauseInputAndSettings` e `IntroNavigationPersistenceAndPlayFlow`) también ocurren con el `RequiemSettings` original, así que son previos. El test espera el flujo antiguo IntroMenu → CombatTwoFloorsLab.
- [x] **Hashes.** Ninguna escena cambió. Solo cambiaron los 4 prefabs de campaña previstos (más campos por defecto que añade la reserialización, iguales a los inicializadores en C#), el prefab nuevo `_IR` y una línea de `ProjectSettings.asset`.
- [x] **Revisión independiente:** sin hallazgos bloqueantes.
- [ ] **Recorrido manual completo** de la campaña con teclado y ratón. Solo se recorrió con entrada virtual del Test Runner.
- [ ] **Medición Release a 1080p (R1).** En el Editor no hay cambio medible: main thread p50 de 18-24 ms antes y después, dentro del ruido de una pelea no determinista.
- [x] **B1, arranque a pantalla completa** (aprobado por el usuario el 17/11/2026). `IronCampaignMapFxDisplay.Apply fullscreen` cambió `fullScreenMode` de Windowed a FullScreenWindow (`ProjectSettings.asset` l.115, `fullscreenMode: 3 → 1`; es el único cambio frente al respaldo `ProjectSettings.before-fullscreen.asset`). El ejecutable arranca sin bordes a la resolución nativa. Si en el registro quedó `IronRequiem.Fullscreen=0` o claves `Screenmanager*` de builds anteriores, mandan esas: borrarlas antes de comprobar. Pendiente: verificarlo en un build.
- [ ] Comprobar en build que el blur desaparece, borrando antes las claves `Screenmanager*` del registro.

### Verificación en build Release (17/11/2026)
- [x] Build Release `Tools/IronRequiem/Builds/VisualOverhaul/mapfx-release/` (`VisualOverhaulBench.BuildGame`): Succeeded, 0 errores, 33 escenas, 275 s.
- [x] **R1 medido a 1080p** (`-irTour -irFullHD`, Release, exclusiva 1920×1080, vsync 0, RTX 4050 Laptop): Hangar 174 FPS (p99 11,6 ms) · Pasillo 188 · Foundry 179 · Breach 183 · Assembly 182 · Reactor 192 · Pursuit 258. Media de salas por encima de 120 FPS; sin fotogramas >33 ms en combate. Informe: `Tools/IronRequiem/Reports/Perf/tour_player_mapfx_release_1080p.txt`. El tour cubre solo la primera ronda de cada sala con 3–6 enemigos.
- [x] **B1: se mantiene sin bordes (decisión del usuario, 17/11/2026).** Con las claves `Screenmanager*` borradas, el ejecutable arranca en FullScreenWindow pero a **1600×900**: Windows escala el panel físico de 1920×1080 al 120 % y Unity recibe el escritorio lógico de 1600×900, que se estira a 1920×1080 (sigue el blur de IR-03). Solo la pantalla completa exclusiva da 1920×1080 reales. El usuario eligió no cambiar a exclusiva: con escalado de Windows al 120 % el blur de IR-03 persiste y queda como limitación conocida. Informe: `tour_player_mapfx_release_default.txt`.
- Registro de Windows: exportado antes de las pruebas en `Backups/20261117-mapfx-phase2/registry-ProyectoGameFinal-before-build-test.reg` y restaurado al terminar.

## IR-03 / IR-07 / PUERTAS — Auditoría r2 con zona protegida (17/11/2026), FASE 1
Informe: `Docs/IronRequiem/MAP_FX_AUDIT_20261117_r2.md` (anexos en `Tools/IronRequiem/Reports/MapFxAudit2/` y `ProtectedZone/guardian.md`). Solo lectura y Play Mode; sin escenas guardadas ni commits. Archivos nuevos: `Editor/MapFx/IronCampaignProtectedZone.cs`, `Tests/PlayMode/ProtectedMenuReference.cs`, tests E/F en `MapFxAudit.cs` y `Tools/IronRequiem/ProtectedZone/compare_menu.ps1`.
- [x] Zona protegida: hashes de 443 archivos iguales antes y después (solo cambia el atlas dinámico de TMP). Las capturas del menú no pueden ser idénticas píxel a píxel porque el menú usa tiempo real.
- [ ] **E2 afecta a la zona protegida en runtime:** cambia el post de Room00 antes de la cinemática. Propuesta P-E2a, pendiente de decisión.
- [ ] **E1 incompleto (F1):** `RobotDestructionPool` usa un solo prefab por sala. Propuesta E7.
- [ ] Colisión confirmada en Play: hueco sin suelo en Room02 (M7), SouthWall de Room01 sobre la estación (M8), columnas en las rampas de Room04 (M9). La campaña termina en un muro ciego (M13).
- [ ] **Corrección a la verificación B1 de arriba:** la build es DPI-aware (PerMonitorV2), así que el escalado al 120 % no está demostrado como causa del arranque a 1600×900. Diagnóstico B4 pendiente.
- [x] Inventario de puertas: 6 esclusas y 13 puertas; contrato comprobado en Play (puerta abierta inactiva, colisión desde el primer frame al cerrar). Sin estado rojo ni sonido de puerta. A la espera de la decisión sobre la FASE P1.

## T-07 Mech: prueba de animación (Tripo/Composio y clips propios de robot) (17/11/2026)
Informe: `Docs/IronRequiem/TRIPO_RETARGET_TEST_20261117.md`. Solo variante y laboratorios; sin commits; producción intacta (19 hashes protegidos iguales).
- [x] Tripo por Composio: subida y comprobación de rig gratis → `riggable: false`; saldo 0; **0 créditos gastados**. Vía descartada por API para este robot.
- [x] Clips propios R_Idle, R_CombatIdle, R_PatrolWalk, R_CombatAdvance, R_CannonAttack y R_HitReaction, con el controller copiado `T07MechRobot_Locomotion`, la variante `T07_Mech_RobotAnim_Test` y los laboratorios Before/After.
- [x] Compilación sin errores. Play Mode automático en los dos laboratorios: patrulla → persecución → disparo → golpe, 0 errores de consola. Revisor ciego: mejora en disparo, golpe y legibilidad patrulla/combate; idle peor.
- [x] Limpieza (aprobada): el idle mantiene el cañón bajo y lo gira con la cabeza; el golpe gira y ladea el torso (distinto del disparo); la interpenetración se revisó en vistas cercanas y el golpe se separó del muslo; recaptura en Play con 0 errores.
- [ ] Prueba manual con teclado y ratón y coste del Animator.
- [x] Disparador del golpe: `MechHitReactionTrigger` (nuevo, solo en la variante) dispara OnDamaged con límite de 0,7 s y cede el brazo en el golpe y tras cada disparo. `T07CannonSocket.YieldAim` añadido; no hace nada si nadie lo llama (respaldo en Backups/20261117-t07mech-robot-anim). En Play, Before (el juego hoy) no reproduce nunca el HitReaction y After sí, con el daño real; 0 errores.
- [ ] No hay tests que cubran `T07CannonSocket`; su equivalencia sin el disparador se verificó solo en Play.
- [ ] Integración en `T07_Mech.prefab`: pendiente de aprobación.
- [x] Escena vitrina `Assets/IronRequiem/Scenes/T07MechRobotAnimShowcase.unity`: 12 mechs visuales sin IA (delante NUEVO, detrás ANTES), 6 columnas, cada uno en bucle con `AnimationClipShowcase` (nuevo, PlayableGraph). Comprobación en Play: 11/12 se mueven más de 1 cm; `R_CombatIdle` se queda en 0,6 cm por diseño (quieto con zumbido). 0 errores; sale de Play sola y restaura la escena. Menú: `Tools/Iron Requiem/Visual Pass/T07 Mech robot anim (test) - Play SHOWCASE (all clips)`.

## T-07 de campaña: animación de robot (18/11/2026)
Informe: `Docs/IronRequiem/T07_ROBOT_ANIM_20261118.md`. Solo variante y escenas de prueba; sin commits; 20 hashes protegidos del T-07 y 19 del Mech iguales.
- [x] Inventario de enemigos (BigBot, Carroñero, HoverBot, SawBot, T-07, torreta). Se empezó por el T-07: el más frecuente y con el mismo driver que el Mech.
- [x] Rig medido: lados `_L`/`_R` nombrados desde el espectador, cañón perpendicular al antebrazo, hueso `Head` duplicado en guías. Corregido en la herramienta (`RCannonArm`/`rCannonPerp`); el Mech no cambia.
- [x] 6 clips `R_*`, controller `T07Robot_Locomotion`, variante `T07_RobotAnim_Test`, laboratorios Before/After y vitrina `T07RobotAnimShowcase`.
- [x] Play automático antes/después con 0 errores. El HitReaction ahora se ve (antes nunca). Vitrina con 12/12 robots en movimiento; Unity vuelve a IntroMenu limpio.
- [ ] Prueba manual con teclado y ratón; revisor ciego A/B; coste del Animator.
- [ ] Integración en `T07_Current_Repaired.prefab`: pendiente de aprobación.
- [ ] Siguientes enemigos: SawBot (clips de rotación limpios; ruedas y cadena por código), HoverBot (controller de plantilla: copiar), BigBot (apuntado e IK de giro por código), torreta (pivote por código) y Carroñero (todo procedural: los clips no se verían sin cambiar `CarroneroMotion`).

## SawBot: animación de robot (18/11/2026)
Informe: `Docs/IronRequiem/SAWBOT_ROBOT_ANIM_20261118.md`. Solo variante y escenas de prueba; sin commits; 19 hashes protegidos del SawBot iguales.
- [x] 5 clips `R_Saw*` (reposo, marcha, preparar 0,25 s exactos, serrar, golpe aditivo) con las poses de contacto originales como clave; muertes, ruedas y cadena sin tocar.
- [x] Controller `SawBotRobot`, variante `SawBot_RobotAnim_Test`, laboratorios Before/After y vitrina `SawBotRobotAnimShowcase`.
- [x] Play automático antes/después: 0 errores y la misma secuencia de IA. Vitrina: 9/10 en movimiento (el que no, es el SawHold antiguo, casi estático).
- [ ] Prueba manual; integración en `SawBot_Current.prefab` pendiente de aprobación.

## IR-10 — Mapa 1 «Búnker abandonado», F0 y F1 (18/11/2026)
Petición: Prompt 9 de `PROMPTS_CHATS.md` §9, ejecutado por el coordinador R0 con un equipo de subagentes (F0 con Haiku; R1, R3 y R4 con Opus; R2 y R5 con Sonnet). Alcance de esta ejecución: F0 y F1, y PARADA. Solo documentos en `Docs/IronRequiem/Map01/`.
- No se abrió ni se controló Unity, no se guardaron escenas y no hubo commits.
- Build Settings, zona protegida y sistemas existentes, sin cambios.
- Respaldo de esta checklist: `Tools/IronRequiem/Backups/20261118-map01-f1/`.
- [x] **F0 inventario:** `Map01/00_INVENTARIO.md`. Lo que condiciona el diseño, verificado contra el código:
  - las armas se obtienen solo por ejecución (`executionOnly`);
  - recorrido lineal sin teletransporte;
  - las rondas ≥ 4 necesitan `secondRoom`;
  - morir recarga toda la sesión;
  - no hay soporte de jefe ni fin de campaña.
- [x] **F1 diseño en papel:** `Map01/MAP01_DESIGN.md` más 01_ESTRUCTURA … 05_ILUMINACION.
  - 17 cuartos + jefe en 12 zonas.
  - Plano sin solapes, comprobado por script.
  - Curva de ritmo, plan de encuentros (238 enemigos) y 59 puertas en 6 clases.
  - Iluminación por zona y 22 props nuevos.
  - 3 conceptos de jefe.
  - Contratos v1 entre roles.
- [ ] **PARADA F1:** decisiones U1–U16 de `MAP01_DESIGN.md` §7 (jefe, sesiones y checkpoints, obtención de armas, Build Settings…).
- [ ] F2 greybox jugable, F3a conceptos de puertas, F3–F5 (ver `MAP01_DESIGN.md` §8).
- [ ] Verificar en el Editor: si el Carroñero es ejecutable, las estadísticas del T-07 Mech, si la torreta es fija y el escalón de NavMesh (1,0 frente a 0,4).
- Pendiente aparte: **IR-11**, IA del jefe (otra tarea).
- [x] **F2 greybox (18/11/2026), construido y en Play.** El usuario aprobó U1–U16. Plan: `Map01/F2_PLAN.md`.
  - **Escenas:** 4 sesiones `Map01_Campaign_S1…S4`, copias de `RequiemCampaign`, que no se tocó. 18 cuartos `Map01_R<NN>_*`, en `Scenes/Map01_Bunker/`, añadidos al final de Build Settings (22 escenas; no se quitó ni reordenó ninguna).
  - **Autoría por datos:** Structure, Lighting, Doors, Encounters y Sessions, en `Editor/Map01/`.
  - **Componentes de runtime nuevos:** `ArenaShutter`, `Map01SessionHandoff`, `Map01StageRelease`, `NavAreaMaskOnAwake`.
  - **Compilación:** sin errores.
  - **Validadores:** Structure OK (NavMesh y caminos de slots), Doors OK, Encounters OK. Lighting da 10 avisos de más de 4 luces por objeto (techos), que quedan para afinar en F3.
- [x] **PlayMode automático `Map01F2Tests`.** Jugador invencible y teletransportado: comprueba el flujo, no la geometría a pie.
  - S1 y S3 aprobadas.
  - S2 aprobada en la segunda pasada. En la primera, R08 se quedó «en espera» con otra variación aleatoria y otra posición del jugador: **vigilar en la prueba manual**.
  - S4: el jefe provisional por rondas se completa. La carga de WinScene no se comprobó después de corregir el test.
- [ ] **PARADA F2:** recorrido manual del usuario (`Tools/Iron Requiem/Map01/Play/Play from S1`), revisor independiente, medición Release de R07/R11/R16/R18 y capturas por cuarto.
- [x] **Hangar y menú (18/11/2026, por petición del usuario).**
  - **Menú:** JUGAR de IntroMenu carga `Map01_Campaign_S1`, con permiso explícito del usuario. Solo cambia `SceneName`, más 10 líneas `style: 0` que Unity reserializa con el valor por defecto de `IndustrialPanelGraphic`. Hay respaldo en `Backups/20261118-map01-hangar/` y el menú `Sessions/5` lo revierte.
  - **S1 empieza en Room00_Hangar:** cinemática intacta (hash igual) y esclusa a0 hacia S00/R01.
  - **Chatarrería `HG_ScrapRoute`:** el tramo del hangar hacia la puerta, construido por `HangarRouteAuthoring`. Pasillo de ≥ 4,2 m, NavMesh del hangar rehorneado y 6 montones viejos desactivados. Las cintas y la pasarela no se tocan; el contador y las explosiones del menú ya existían y no cambian.
  - **Verificado en Play:** menú → JUGAR → S1 → cinemática → jugador de pie en el hangar.
  - [ ] Pendiente: el paso hangar → R01 a pie y el coste de la chatarrería (+515 k triángulos, sin LOD).
- [x] **Sonido de la cinemática de inicio (18/11/2026, petición del usuario).**
  - **Qué se añadió:** capas nuevas sin tocar la timeline ni los scripts de HangarIntro (hashes iguales):
    - `HangarIntroSoundscape` (Scripts/Audio), con 30 cues por tiempo;
    - 18 WAV sintetizados de forma reproducible en `Audio/SFX/HangarIntro_v2` (`Tools/IronRequiem/Audio/synth_hangar_intro.py`);
    - raíz `HG_CinematicSoundscape` en Room00_Hangar, creada por `HangarIntroAudioAuthoring`.
  - **Diseño de cues:** `Map01/AUDIO_CINEMATICA_INICIO.md`. Respaldo: `Backups/20261118-hangar-audio/`.
  - **Verificado en Play:** menú → JUGAR → cinemática completa sin errores.
  - [ ] Pendiente: la escucha del usuario. El acento de la revelación (23,2 s) está estimado. Durante la cinemática, la música de exploración baja a −9 dB (se puede desactivar).

## Estación de montaje, cambio de arma y ejecución del T-07 (19/11/2026)
Informe: `Docs/IronRequiem/ARMS_FIX_20261119.md`. Respaldo `Tools/IronRequiem/Backups/20261119-arms-fix/`. Evidencias `Tools/IronRequiem/Reports/ArmsFixPass/` (before/after, `station_compare_*.png`, `weapons_*_sheet.png`, `tests/`). Sin commits; ninguna escena guardada; zona protegida con 39/39 hashes iguales.
- [x] A. Estación en 4 tiempos (aproximación, sujeción, bloqueo con golpe seco a 3,80 s, retirada) con la duración de 5,6 s. IK con el codo hacia arriba y hacia dentro, más comprobación por cápsula: los brazos ya no entran en el muro de Map01. Cobertura máxima 19,6 % en el vestíbulo y 17,6 % en Map01 (antes 24,2 % y 22,1 %). Ninguna pieza flotando al terminar. Una luz sin sombras. Solo clips existentes.
- [x] B. Rig sin parpadeo en 1↔2, sin despliegue duplicado ni absorción repetida, G con un solo cambio, cambio forzado que se invierte, arma robada en el brazo tras el arranque. 1/2 y G bloqueados hasta el impacto (~0,83 s); melee bloqueada hasta el final de la recuperación de la mano.
- [x] C. Ejecución con exactamente 2 golpes (arranque 0,34 s; puñetazo y muerte 0,732 s). El alcance es un agarre de 0,55 m como máximo, sin puño. YankTime y StrikeTime sin cambios (Big Bot y ExtractionArcFx intactos).
- [x] Chirrido robótico (petición del usuario): quitados los 9 `IR_Hangar_Servo` de `Room00_WakeUp.playable` (se conservan los otros 6 sonidos); la estación tampoco usa el servo.
- [x] Compilación: 0 errores. Tests nuevos `ArmsFixPassTests`: 4/4. Regresión final: 72/83, con 9 fallos previos confirmados por línea base con el código original y 2 inestables o de entorno, verificados aislados.
- [ ] Prueba manual del usuario con teclado y ratón.
- [ ] Map01: el marco fijo de la estación sigue dentro del muro lateral (S1, S3, S4). Hay que recolocarlo en `Map01SessionAuthoring` (chat 9); requiere aprobación.
- [ ] Pendientes menores de la revisión: «[G] SOLTAR» visible durante el bloqueo (MechanicHud), sierra del SawBot ausente hasta el arranque, agarre corto frente al arco de extracción, excepción de `T07CannonSocket` al instanciar desde una corrutina, piezas de la estación aún con aspecto de cubos.

## Estación de montaje con el modelo segmentado (19/11/2026, modelo entregado por el usuario)
Informe: `Docs/IronRequiem/MOUNT_STATION_ART_20261119.md`. Respaldo en `Tools/IronRequiem/Backups/20261119-mount-station-art/` (prefab, rig, estación, arnés, checklist, FBX fuente y hashes). Evidencias en `Tools/IronRequiem/Reports/MountStationArt/`. Sin commits; ninguna escena guardada; zona protegida 39/39. Aplicado al prefab de producción con aprobación del usuario (respaldo previo en `.../apply-production/`).
- [x] **Preparado del modelo:** FBX de Tripo (256 piezas) partido en copia por `Tools/IronRequiem/Blender/mount_station_prep.py`. Separa pórtico estático, 2 brazos de trabajo (anilla, eslabón con hombro y codo, herramienta, pinza partida en dos mitades) y 2 brazos traseros aparcados. Añade UV cúbicas y 6 materiales `IR_MS_*`. Fuente intacta (SHA-256 igual).
- [x] **Variante de prueba `MountStation_C04_Segmented.prefab`** (retirada al aplicar; copia en el respaldo): creada por `MountStationArtBuilder`. La animación de `WeaponMountStation` no cambia; el rig se coloca sobre las articulaciones del modelo y los cubos se ocultan, conservando los colisionadores reajustados a los postes.
- [x] **`StationArmRig`:** `hingeAligned` y `gripLength`, ambos desactivados por defecto. La estación actual no cambia.
- [x] **Encaje:** el pórtico cabe sin tocar muros en RequiemCampaign y Map01 S1–S4. Los cubos actuales se meten en `SideW` (S1) y `SideE` (S3, S4); con este arte desaparece el pendiente de recolocar la estación.
- [x] **Cobertura máxima (solo brazos):** 23,0 % en el vestíbulo y 19,7 % en Map01 S1, sin choques en ninguna de las dos. Con la mano soltada: 26,1 % y 22,2 %.
- [x] **Tests:** EditMode `StationArmRigSegmentedTests` 4/4. Regresión PlayMode (campaña, hangar y armas) 11/12; el único fallo ya existía: IntroMenu arranca Map01.
- [x] **Aplicado a `MountStation_C04.prefab`** (19/11, «Sí, aplícalo»): mismo `.meta`/GUID; escenas sin guardar y con el mismo hash. Encaje libre en las 5 escenas. En Play con la estación real: vestíbulo 23,0 % / 26,1 %, Map01 S1 19,6 % / 22,2 %, sin choques y montaje concedido. EditMode 4/4 (la prueba de producción fallaba antes de aplicar). Regresión PlayMode 11/12, con el mismo fallo previo de IntroMenu.
- [x] **Refinamiento de la garra (19/11, tarde):** la garra ya no se ve suelta del brazo.
  - Muñeca del modelo en `StationArmRig`: cabeza en la punta de la herramienta, codo hacia el del modelo y giro hacia la herramienta. El giro respecto a la herramienta pasa de 132-175° a 0°.
  - Pinzas que giran sobre su pasador (tenazas) en vez de deslizarse.
  - Reposo hacia fuera y atrás: el brazo ya no se pliega del todo.
  - Los bloques de rayas del anillo de la muñeca pasan a ser pernos de acero con tapón naranja.
  - Cobertura del brazo: vestíbulo 22,0 %, S1 18,4 %, sin choques y montaje concedido.
  - EditMode 6/6; regresión 11/12 (fallo previo de IntroMenu); escenas sin guardar.
- [x] **Puertas blindadas (19/11, aprobado):** 86 puertas de rayas pasan al material `IR_BlastDoor` en RequiemCampaign y Map01 S1–S4. Se generó por código: acero, dos hojas, refuerzos, remaches, cerradura y marco naranja.
  - Escenas guardadas con respaldo en `Backups/20261119-doors/`; solo cambia el material.
  - Siguen con rayas las marcas de la escalera y la cámara de tránsito del hangar.
  - En Play: vestíbulo 22,0 %, S1 18,4 %, sin choques y montaje concedido. EditMode 6/6; regresión 11/12; zona protegida 39/39.
- [x] **Puerta del tránsito del hangar** («TRÁNSITO C-04 · PUENTE NORTE», aprobado): cambiada en las cinco escenas, con respaldo en `Backups/20261119-transit-door/`.
  - `ChamberHangarDoor` pasa a `IR_BlastDoor`; la puerta con ojo de buey (4 piezas), a `IR_DoorPlate` (acero remachado).
  - Solo cambia el material; IntroMenu y los generadores, sin tocar.
  - Play, EditMode 6/6 y regresión 11/12 (fallo previo).
- [x] **Controles v0.1, F2 (20/11, esquema Ágil elegido):** bindings nuevos en `InputSystem_Actions.inputactions` (respaldo en `Backups/20261119-controls/`).
  - Dash en Espacio (aire) / M5 / LB; slide en C / M4 / R3; Interactuar contextual E / X; golpe F / RB; expulsar G / B; `WeaponNext`, `WeaponPrevious` y `SelectWeapon1..9`; Esc entra en `UI/Menu` y Tab sale.
  - Eliminadas `Attack`, `Interact`, `Previous`, `Next`, `NextWeapon`; `Overdrive` queda sin tecla (no se borra). Recargar queda solo en teclado (X es Interactuar).
  - `PlayerInputHandler` lee acciones (sin `Keyboard.current`), sprint alterna en mando por defecto. `WeaponMountStation` e `InGameMenuManager` migrados. `ControlHints` da el nombre real de la tecla a `MechanicHud`, `RetroStatusBar` y la estación.
  - EditMode 9/9 (`ControlsSchemeTests`, `MechanicsAssetTests`). PlayMode: regresión C 24/27 y B 42/48; los fallos ya estaban en la línea base de ARMS_FIX (solo `ModularArmTests` falló por un archivo bloqueado).
  - Sin tocar: zona protegida, escenas, stick sin `deltaTime`, F12. Pendiente: panel de reasignación (F3/F4), textos horneados de escenas y prefab.
- [x] **Controles v0.1, F4 (20/11):** menú de reasignación (lista A) con captura, conflictos con intercambio, restablecer y guardado en PlayerPrefs; botón CONTROLES en las opciones de la pausa (IntroMenu sin tocar).
  - Código nuevo: `ControlsRebinder`, `ControlsPanel`, `ControlsPanelInstaller`, `BakedHintFixer`, `PauseMenuBackHook`; ajustes de sprint e invertir Y en `RequiemSettings`.
  - EditMode 18/18 (`ControlsSchemeTests`, `ControlsRebinderTests`, `MechanicsAssetTests`); PlayMode 4/4 con teclado virtual (Golpe F→V: V golpea, F no, el HUD muestra V, persiste, el bloqueo de entrada sigue activo, Esc cancela la captura).
  - Capturas y estado en `Docs/IronRequiem/CONTROLS_V01.md` §10. Pendiente: botón en IntroMenu (aprobación), textos horneados restantes y prueba manual con mando.
- [ ] Prueba manual con teclado y ratón; ejecución en Play en Map01 S2–S4; medida de rendimiento (≈ 31 k vértices más).

- 2026-11-20 · Controles: botón CONTROLES añadido al menú principal por instalador en ejecución (IntroMenu sin editar). Probado en Play con captura.

## Chat 11 — Linterna y lectura de enemigos en la oscuridad: F0 + F1 (20/11/2026)

Petición: prompt 11 de `PROMPTS_CHATS.md` (F0 inventario, F1 propuestas, **PARADA**). Detalle: `VISION_V01.md`. Respaldo de esta checklist: `Tools/IronRequiem/Backups/20261120-vision-f0f1/`. Sin cambios en escenas, materiales, luces, entrada, ajustes ni paquetes. Sin commits.

- [x] F0 inventario con 3 subagentes Haiku de solo lectura (luces y zonas oscuras; materiales, emisivos y rim; entrada, cámara, HUD y ajustes). Las afirmaciones clave se comprobaron en el proyecto: URP sin light layers (`Default_PipelineAsset.asset:76`); `_RIM` como keyword de material; nadie escribe el rim en runtime; `EnemyController` reescribe su MaterialPropertyBlock cada frame.
- [x] Herramienta de medición de solo lectura `Assets/IronRequiem/Editor/Vision/VisionF0Probe.cs` (menú `Tools/Iron Requiem/Vision`). Usa una escena vacía sin guardar y salas aditivas en Play, y reabre la escena previa. Validada contra la cámara del jugador en Map01 S1: medias 10,54 y 10,61.
- [x] Medición: 561 casos en 24 salas (`Reports/Vision/F0`). 161 ilegibles, 134 débiles y 266 legibles. Peores: R07, R11 y R02; por enemigo, el T07 (35 %). HoverBot siempre legible.
- [x] F1: 3 opciones más una combinación, probadas en los mismos 6 puntos oscuros a 5, 15 y 25 m (`Reports/Vision/F1`). A (linterna) 8/18, B (pulso) 17/18, C (pasivo) 17/18, A+C 17/18. Recomendada: C.
- [ ] **PARADA F1:** elección del usuario (A, B, C o combinación). F2 a F4 sin empezar.
- [ ] Pendientes para otros chats:
  - Map01 oscuro de forma general (fondo 2-13 frente a ≥ 45): chat 9 y F3, sin subir el ambiente.
  - R07 con 10 luces y R18 con 9.
  - Prueba manual del chat 10; no hay botón de mando libre.
  - `maxBatches=0` en MapFxAudit.

## Chat 11 — F2 a F4: lectura pasiva de enemigos en la oscuridad (opción C) (20/11/2026)

Petición: el usuario eligió **«C: lectura pasiva»** y después dijo «continua». Detalle completo en `VISION_V01.md` §6-§8. Respaldos: `Tools/IronRequiem/Backups/20261120-vision-f2/`. Sin cambios en escenas, luces, entrada, pipeline, paquetes ni materiales originales (hash igual). Sin commits.

- [x] F2:
  - `Scripts/Player/Vision/EnemyDarkReadability.cs`, `VisionMaterialRules.cs` y `VisionMaterialMap.cs`.
  - 4 variantes `_Vision` nuevas en `Art/Vision/`.
  - Ajustes «Resaltar enemigos en la oscuridad» y «Contornos siempre activos» (`RequiemSettings` y `ControlsPanel`).
  - Herramientas en `Editor/Vision/`.
  - Sin acción de entrada nueva, sin `Light`, sin recurso.
- [x] Calibración con 561 puntos medidos: oscuro ≤ 0,5 (fondo ≈ 15) e iluminado ≥ 1,6 (fondo ≈ 45).
- [x] F3:
  - En los 6 puntos más oscuros, 0 de 18 legibles antes y 16 de 18 después. El contraste pasa de 1,0-1,5 a 2,2-3,3.
  - Esclusa Transfer y captura disparando sin deslumbramiento.
  - Room03 en Editor: 0 luces añadidas; tiempo de frame y GC sin cambio apreciable; **SetPass +18-25 %** (coste real, pendiente de medir en build).
- [x] F4: revisor Opus independiente. Hallazgos 1 y 3-9 corregidos (2 descartado con evidencia).
  - Fallo propio corregido: la liberación oscurecía al enemigo y el rim superaba 4 HDR por la conversión de espacio de color.
- [x] Tests: EditMode **3/3** y PlayMode **2/2**, con XML en `Tools/IronRequiem/Reports/Vision/F2/`. Compilación sin errores.
- [ ] Pendientes:
  - Medición en build (SetPass y GPU) y, si procede, cuantizar el rim en niveles.
  - Medir a 25 m reales.
  - Prueba manual con teclado, ratón y menú de ajustes.
  - Comprobar en juego BigBot, HoverBot, Turret y Carroñero.
  - Oscuridad general de Map01 (chat 9).

- 2026-11-20 · Menú: estilo unificado y luces (pedido por el usuario; zona de menú aprobada para este cambio).
  - Hecho, todo en ejecución, sin editar ni guardar escenas (`Scripts/Menu/MenuStyleInstaller.cs`):
    consola de opciones con la misma chapa remachada y placas de cinta del menú principal, cabecera «AJUSTES»; créditos dentro del marco del menú (antes flotaba sin panel); lecturas de volumen/sensibilidad visibles (quedaban tapadas por su pantallita); lámpara suelta de VOLVER retirada.
  - Luces: `FaderGlowFx` (brillo según altura de la palanca), `MenuLampFx` (halo y fallos en PWR/SRV/NET y en el interruptor), `MenuButtonLightFx` (resplandor, barrido e indicador en los botones), `TitleWornFx` + `TitleWearGraphic` + `MenuSparksGraphic` (título desgastado, letras que fallan, chispas y línea de escaneo).
  - Tocados: `MainMenuPanelPresentation` (el marco sigue visible en créditos), `MenuPolishButtonFx` (getter `IsPrimary`), `OptionsSwitchVisual` (placa de cinta + halo), `BakedHintFixer` (también en el menú principal). Respaldo: `Tools/IronRequiem/Backups/20261120-menu-style/`.
  - Verificado en Play con capturas (`Docs/IronRequiem/Concepts/UI/MenuStyle/`): inicio, opciones y créditos del menú principal, y opciones de la pausa en Map01. Sin excepciones en consola.
  - No verificado: interacción con ratón/mando real, secuencia de JUGAR con el título nuevo, otras resoluciones, compilación del juego. Sin tests automáticos nuevos.

- 2026-11-20 · Cinemática de despertar (Room00) y pausa (pedido por el usuario; zona protegida aprobada para estos puntos).
  - Luces: el visor arranca al final del primer acercamiento (`WakeAt` = primer golpe − 0,9 s; antes 0,8 s). El sonido de arranque `R01_arranque` se mueve con él (`HangarIntroSoundscape.RetimeCue`). La franja del visor sigue viéndose celeste pálido porque es el color de su textura, no una luz (0 materiales emisivos en el modelo).
  - Golpes al cristal (`WakeUpRobotPose`): puño armado junto al pecho, pausa, golpe seco, queda apoyado y retrocede; el segundo se arma más y atraviesa el cristal. Torso que gira con cada golpe y codo detrás del puño.
  - Mirada a la mano: la cabeza se orientaba antes de subir el brazo y apuntaba a donde la mano colgaba. Ahora `LookAtHand` corre después del brazo y apunta a la palma.
  - Cristal (`CapsuleGlassBreak`): la grieta deja de ser una imagen plana; se genera una red de fracturas finas que sale del puño y sigue creciendo a saltos hasta el segundo golpe. La rotura añade esquirlas pequeñas.
  - Pausa (`MenuStyleInstaller.Pause`): consola compacta con el estilo del menú principal (título desgastado, regla naranja, botones numerados con luz, lámparas, ayuda de teclas). `MenuButtonLightFx` funciona también sin `MenuPolishButtonFx`.
  - Todo en ejecución, sin guardar escenas. Respaldo: `Tools/IronRequiem/Backups/20261120-intro-pausa/`.
  - Verificado en Play con capturas fijas (`Docs/IronRequiem/Concepts/UI/IntroPausa/`). No verificado: la cinemática vista en movimiento, el sonido de arranque retrasado, `HangarIntroPresentationTests` y `CampaignTests` (no ejecutados), compilación del juego.

- 2026-11-20 (corrección) · Luces del robot: lo anterior estaba al revés.
  - Acercamiento de JUGAR en el menú (`HangarLaunchSequence`): se quitó el encendido a 1,8 s; el robot sigue apagado hasta el fundido.
  - Cinemática de Room00: el visor vuelve a encenderse a 0,8 s, antes de moverse (`WakeAt` restaurado; se retiró `RetimeCue` y el sonido de arranque queda en su tiempo original).
  - `TitleWornFx`: corregido el error «graphic rebuild while we are already inside a graphic rebuild loop» (el desgaste se marcaba desde el callback de TMP; ahora se marca en `Update`).
  - `MainMenuRestoreTests`: la comprobación `robot.IsAwake` tras JUGAR pasa a esperar `false`. Test no ejecutado.
  - Verificado en Play con capturas: `jugar_acercamiento_apagado.png` e `intro_1_visor_encendido.png`; sin el error en consola en esa pasada.

## Chat 7 — Lote P-E2a + E7 + M7–M9 + P1 de la auditoría r2 (aplicado el 17/11/2026, registrado el 20/11/2026)
Aprobado por el usuario el 17/11. El código y las copias entraron en el commit `6dbe3be` (18/11). **La verificación quedó a medias** (ver abajo). Desde el 18/11 el chat 9 (Mapa 1) sustituye a este chat en mapa y puertas.
- [x] **P-E2a:** `ZoneEnvironment.Bind` ya no cambia el perfil de `HG_PostVolume`. `Scripts/VisualPass/HangarGameplayPost.cs` (nuevo) añade en runtime un Volume aparte (prioridad 21, HG_GameplayVolume) solo cuando `HangarIntroCinematic.Finished` es true. Test `MapFxAudit.H_HangarPostAfterCinematic` aprobado el 17/11: peso 0 durante la cinemática y 1,00 a los 1,5 s de terminar; `HG_PostVolume` sigue con HG_MenuVolume.
- [x] **E7:** `RobotDestructionPool` guarda un juego de bursts por prefab; BigBotDestruction, SawBotCombat, PooledEnemyDeathBurst y RobotDamageProgression piden su propio prefab. Test `MapFxAudit.I_BurstPoolPerPrefab` aprobado el 17/11.
- [ ] **E7, regresión sin diagnosticar:** el 17/11 fallaron `BigBotDestructionTests.LethalFromFullLifeProcessesDeathAndDropOnceWithoutDamageExplosion` y `SimultaneousDeathsRespectBudgetsAndCannonPoolResetsOnReuse`, y `CombatPolishTests` se colgó tras `BigBotDeathVisualSequence`. No se comprobó si también fallan con el código anterior. No se ejecutaron `T07DestructionBehaviorTests`, `SawBotTests`, `HangarIntroPresentationTests` ni `CampaignTests`.
- [x] **M7–M9 en copias** (`Editor/MapFx/IronCampaignMapFxRooms.cs`): `Room01_Foundry_v2`, `Room02_Breach_v2` y `Room04_Reactor_v2`, cada una con su `*_v2_NavMesh.asset`. Los originales no se tocaron (hashes iguales el 17/11). Test `MapFxAudit.G_RoomFixesBeforeAfter` (`Reports/MapFxAudit/fixes/`): en Room02 el jugador ya no cae al vacío, en Room01 ningún collider de la sala toca la estación y en Room04 la rampa llega a la pasarela. Las copias **no están en Build Settings** ni en `stageScenes`; con Map01 como campaña activa quedan como referencia.
- [x] **P1 conceptos de puertas:** `Docs/IronRequiem/Concepts/Doors/Redesign_v01/` (D-A Mandíbula, D-B Persiana telescópica, D-C Bóveda batiente). Sin elección del usuario; el chat 9 definió después su propia familia en `Map01/04_PUERTAS.md`.
- [ ] No hechos: medición Release tras el lote, comparación final de la zona protegida y recorrido manual.

## Chat 12 — Pase visual con Fable: F0 diagnóstico y F1 dirección de arte (21/11/2026)

Petición: prompt 12 de `PROMPTS_CHATS.md` más el «Contexto del mundo» enviado por el usuario. Alcance de esta ejecución: F0 y F1, y **PARADA**. Entrega: `Docs/IronRequiem/VisualPassFable/` (`00_DIAGNOSTICO.md`, `01_DIRECCION.md`, `01d_DECORACION.md`, `01e_CARRONERO.md`, capturas en `F0/` y `F1/`, láminas en `concepts/`). Sin cambios en escenas, materiales, luces, ajustes, entrada ni paquetes. Sin commits. Respaldo de esta checklist: `Tools/IronRequiem/Backups/20261120-chat7-cierre/`.

- [x] **Arnés de solo lectura** `Assets/IronRequiem/Tests/PlayMode/VisualPassF0.cs` (único archivo nuevo en Assets). Carga la sesión real `Map01_Campaign_S1`, detiene el flujo y carga cada sala de forma aditiva; nada se guarda.
- [x] **F0 zonas:** 25 salas (18 de Map01 y 7 de la campaña antigua), 2 puntos de vista fijos, vacío y T-07 a 5, 15 y 25 m, a 1920×1080: 200 capturas en `Tools/IronRequiem/Reports/VisualPassFable/F0/zones/`, con `zones.csv` y `rooms.csv`. Fondo medio de 3 a 19 sobre 255 en todas; entre el 50 % y el 98 % de los píxeles por debajo de 10.
- [x] **F0 luces de robots:** 7 enemigos, estados de `EnemyStateLights`, frente, espalda y 15 m (61 capturas y `robots.txt`). Ojos de 3–5 cm sin HDR; solo el HoverBot cambia de color con el estado; el BigBot escribe en una emisión apagada; el punto rojo del Carroñero flota; el contorno blanco de `EnemyDarkReadability` es lo más llamativo.
- [x] **F0 Carroñero:** 8 tiras de frames (`carronero/`). Pie de apoyo a 6,2–6,6 m/s con el cuerpo a 5,6 m/s; sin andar (la IA fija 7,2 m/s); sin anticipación, reacción al daño ni pose de muerte.
- [x] **F0 decoración:** Map01 es greybox, `[Decor]` vacío en los 18 cuartos; no existe ninguno de los 22 props previstos.
- [x] **F1 maquetas en motor (solo en ejecución):** 5 niveles de luz en 7 salas (`F1/lum.csv`, 70 capturas) y 2 opciones de ojos rojos en 6 enemigos (144 capturas).
- [x] **F1 d y e** (subagente director de arte, Fable): re-tematización de los 18 cuartos, esquema de la ruta, 3 láminas de tramo y 3 de antes/después con scripts de Blender; 12 mejoras de animación del Carroñero en dos niveles de alcance.
- [x] **Zona protegida:** instantáneas `ProtectedZone/vp-before-20261120.txt` y `vp-after-20261121.txt`, 508 archivos. Única diferencia: el atlas dinámico `LiberationSans SDF - Fallback.asset`, que Unity reescribe en cada Play Mode.
- [x] **Subagentes:** 4 inventarios Haiku de solo lectura (`Reports/VisualPassFable/inventory/`; el B no escribió su archivo y su contenido está en `robots.txt` y en el diagnóstico) y 1 director de arte Fable.
- [ ] **PARADA F1:** elección del usuario en a) nivel de luminosidad y linterna, b) correcciones de luces, c) ojos rojos A o B, d) dirección del mapa y concepto de decoración, e) alcance del Carroñero. F2 a F6 sin empezar.
- [ ] **Contradicciones con el contexto del mundo, sin tocar** (`00_DIAGNOSTICO.md` §6): Map01 baja de 0 a −37 m y el contexto dice que se sube; el orden temático de zonas; no hay laboratorios; el Habitáculo (Dormitorios, Comedor, Enfermería); el final en la Bóveda sin salida. Decisión del usuario y trabajo del chat 9.
- [ ] **Solape con el chat 11:** la opción C de la linterna ya está implementada; este pase propone corregir su contorno blanco.
- [ ] **Hallazgo para el chat 9, sin verificar en partida real:** `EnemyController.SelfDestructYHeight` vale −20 en todos los prefabs y los suelos de R08 a R18 están entre −21,95 y −36,95. En el arnés, un T-07 creado en esas salas se destruía en su primer frame.
- [ ] No hecho en esta fase: medición en build, prueba manual, revisor independiente (son de F6) y tests EditMode/PlayMode de la entrega final. `VisualPassF0` son capturas, no comprobaciones.

## Chat 12 — Pase visual: F2 luminosidad, F3 ojos rojos y F5 Carroñero (21/11/2026)

El usuario aprobó «lo recomendado» de `VisualPassFable/01_DIRECCION.md`. Detalle, medidas y capturas: `Docs/IronRequiem/VisualPassFable/02_RESULTADO.md`. Respaldo: `Tools/IronRequiem/Backups/20261121-visualpass-f2f5/`. Sin commits.

- [x] **F2 luminosidad por tramos** (sin linterna, sin luces nuevas): 18 JSON de `Levels/Map01/Data/Lighting`, Volume de exposición por cuarto (`Art/VisualPass12/Map01_Exposure_T1/T2/T3`), 18 escenas `Map01_R*` reconstruidas solo en `[Lighting]` y sondas rehorneadas. Fondo medio de 3–19 a 17–66 sobre 255. El validador sigue en los 10 cuartos con más de 4 luces por objeto que ya había.
- [x] **Contorno del resaltado** (chat 11): azul acero y mitad de intensidad (`VisionMaterialRules.cs`).
- [x] **F3 ojos rojos, opción A:** `Scripts/VisualOverhaul/EnemyEyeGlow.cs` y `EnemyEyeStyle.cs`; un solo sistema de partículas, sin `Light`, sin tocar prefabs ni materiales.
- [x] **F5 Carroñero de combate, nivel mínimo:** `Scripts/Enemies/CarroneroCombatMotion.cs`, solo en `Prefabs/Map01/Enemies/Carronero_Map01.prefab`. Coste en Editor para 24: 0,31 ms frente a 0,33–0,48 ms.
- [x] **Tests:** EditMode 18 de 18 (`VisualPassLightingTests`, `VisualPassEyesEditTests`, `VisionReadabilityEditTests`); PlayMode `VisualPassEyesTests` 26 de 26, `VisionReadabilityTests` 2 de 2, `VisualPassCarroneroTests` 11 de 11. `Map01F2Tests`: S2–S4 pasan; S1 falla en una comprobación del hangar anterior a estos cambios (sin confirmar con el código previo).
- [x] **Zona protegida:** 508 archivos iguales salvo el atlas dinámico de TMP.
- [ ] **Regresión medida:** a 25 m solo 6 de 25 casos son «legibles» con la métrica de luminancia (antes 25 de 25); a 5 y 15 m, todos. Decisión del usuario (`02_RESULTADO.md` §6).
- [ ] Pendientes: ojo del BigBot descentrado; el rojo tira a rosa con el bloom; material de ojos creado con `Shader.Find` (comprobar en build); campaña antigua con menos contorno.
- [ ] No hecho: F4 decoración (depende de O1 en el chat 9 y del kit de props), medición en build, prueba manual, `CampaignTests`/`HangarIntroPresentationTests`/`MainMenuRestoreTests`. El revisor independiente quedó en curso (`VisualPassFable/03_REVISION.md`, sin leer).

## Chat 12 — Pase visual: ronda tras la revisión independiente (21/11/2026)

- [x] Revisión independiente leída ([03_REVISION.md](VisualPassFable/03_REVISION.md)): espacio 3 → 7, enemigo 6 → 5, ambiente 4 → 6, ojos 4 → 5, Carroñero 3 → 5.
- [x] Ojos en rojo puro, estados por tamaño, intensidad y pulso; anclajes de T-07, BigBot y torreta rehechos; indicadores del BigBot en rojo tenue.
- [x] Contorno del resaltado más suave (`VisionMaterialRules`: potencia 2.8, 0.16 con luz, 0.6 a oscuras).
- [x] Tests: EditMode 19 de 19; PlayMode 28 de 28 y 11 de 11. Zona protegida sin cambios (solo atlas TMP).
- [x] Capturas finales en `Docs/IronRequiem/VisualPassFable/F2/`; detalle en la §8 de `02_RESULTADO.md`.
- [ ] Decisión del usuario: lectura del enemigo a 25 m (4 legibles de 25 con la métrica de luminancia).
- [ ] Segunda revisión independiente con los cambios.
- [ ] Prueba manual con teclado y ratón, build Release (peor cuarto y horda de 24), vídeo.
- [ ] `CampaignTests`, `HangarIntroPresentationTests`, `MainMenuRestoreTests`; confirmar si `SessionTraversal(0)` ya fallaba.
- [ ] F4 decoración: espera a la inversión de cotas (O1) del chat 9 y al kit de props.
- Sin commits.

## Chat 13 — Ejecuciones de robots v02: F0–F4 (22/11/2026)

Petición: prompt «Ejecuciones de robots más brutales y legibles»; tras F1 el usuario aprobó «Todo, Big Bot incluido». Detalle, archivo:línea y pendientes: `Docs/IronRequiem/Executions/EXECUTIONS_V02.md` (F0 y F1 en la misma carpeta). Respaldo: `Tools/IronRequiem/Backups/20261122-executions-v02/`. Sin commits, paquetes ni escenas guardadas; zona protegida intacta (hashes).

- [x] F0: grabación (jugador + lateral) y telemetría de T-07, SawBot y Big Bot. Hallazgo: el Big Bot de campaña no era ejecutable (`executionEnabled=false` desde v31).
- [x] F1: guion por beats y storyboards; aprobado todo, incluidos Big Bot ejecutable, Recover 0,24, micro-paso y destello.
- [x] F2 robot: capa de reacción por beats, arrastre a la mano, arma oculta en el arranque, pulso local, audio por beat, barra de vida oculta, restos desviados de la cámara. Prefabs de campaña configurados por herramienta del Editor.
- [x] F3 jugador: director de ejecución, alcance por bandas, arma continua (proxy), hit-stop con presupuesto, kicks con tope de 90°/s, Big Bot rehecho (1,52 s reales sin control; antes 2,30).
- [x] Compilación 0 errores. Tests: `ExecutionsV02Tests` 8/9, `ArmsFixPassTests` 4/4, FastCombat Big Bot 1/1. Regresión 70/81: los 10 fallos restantes son previos y ajenos (6 comprobados con el código original restaurado).
- [x] Revisor ciego, 2 rondas: en la final prefiere la versión nueva en los tres robots (notas 4–9, ninguna ≥8 en todo).
- [x] Big Bot, ronda 2 (aprobada por el usuario): perchero delante del cañón, calculado sobre un proxy del cuerpo visible (`ExecutionBodyProxy`, pose renderizada). El jugador cuelga del cañón; el robot mantiene la puntería que tenía al pulsar E; tirones que levantan el cañón. `ExecutionsV02Tests` 9/9, medido contra los triángulos reales: giro 1,26°/frame, cámara a 0,97 m del cuerpo, 0 frames fuera de cuadro, 2,09 m a 1,2 s, mano a ≤0,05 m del cañón (1 frame a 0,13). Regresión sin fallos nuevos atribuibles. `BigBotBazooka.StolenBazooka…` falla; último pase el 08/11 y no se verificó con un swap. Respaldo: `Tools/IronRequiem/Backups/20261122-executions-v02-bigbot-r2/`.
- [x] Revisor ciego, ronda 3: prefiere la ronda 2 del Big Bot (5/5/5/4/7 frente a 5/4/5/4/4).
- [x] Big Bot, ronda 3 (aprobada): el cañón arrancado se encoge a tamaño de mano (antes 2,7 m a 0,33 m de la lente); el cañón robado es el arma activa desde 0,80 s; la muerte ya no lanza un segundo cañón; tambaleo en el arranque. `ExecutionsV02Tests` 9/9 con 3 comprobaciones nuevas; la regresión no tiene fallos nuevos. Respaldo: `Tools/IronRequiem/Backups/20261122-executions-v02-bigbot-r3/`.
- [x] Revisor ciego, ronda 4: prefiere la ronda 3 (5/5/5/4/8 frente a 6/4/4/3/5). El mareo llega a 8.
- [ ] Big Bot, todavía <8 en peso, legibilidad, brazos y reacción: el cañón montado no se encuadra en el disparo, el tambaleo no se lee de cerca y el cañón encogido parece un palo. Los rayos blancos de la muerte son del sistema existente.
- [ ] T-07: el brazo arrancado tapa el centro a 0,47 s; transferencia de mano poco legible. SawBot: remate sin fogonazo (muerte existente).
- [ ] Prueba manual del usuario con teclado y ratón. Nota: sus PlayerPrefs reasignan Melee a clic derecho y Apuntar a F.

## Chat 13 — Sensación de input lag al moverse (23/11/2026)

El usuario notaba «una enorme sensación de input lag» y la situó en el movimiento. Respaldo: `Tools/IronRequiem/Backups/20261123-movement-sharpness/`.

- [x] Medición (`Tests/PlayMode/InputLatencyProbe.cs`, informe `Tools/IronRequiem/Reports/Controls/input-latency.txt`), en la campaña real (Map01 S1, Editor, tiempo real):
  - ratón → cámara: 1 frame (12–16 ms), sin suavizado;
  - W → movimiento: 10–25 ms, pero ~140 ms hasta el 90 % de la velocidad;
  - Editor: 68 FPS de media, con picos de 32–91 ms.
- [x] Causa: `MovementSharpnessOnGround = 15` (valor de la plantilla en `Assets/FPS/Prefabs/Player.prefab`).
- [x] Cambio: `AdvancedPlayerMovement.groundSharpness = 35` (Inspector, sección «Ground response»), aplicado en Awake. El prefab de plantilla no se toca. Resultado: 90 % de la velocidad en 47–61 ms. También frena antes.
- [x] Tests: `ExecutionsV02Tests` 9/9, `CharacterBehaviorTests` 3/3. Los fallos de `MechanicsBehaviorTests` ×2, `SlidePistolMainTests` ×1 y `FastCombatTests` ×1 son previos (el de SlidePistol falla igual con el ajuste desactivado).
- [ ] Prueba del usuario con ratón y teclado reales. Si sigue pesado: subir `groundSharpness` (50 ≈ casi instantáneo) o bajar `QualitySettings.maxQueuedFrames` de 2 a 1 (ProjectSettings, pendiente de su permiso).

## Chat 13 — Sin sprint: velocidad única (23/11/2026)

El usuario notaba que «de repente» el movimiento se ponía «pesadísimo» tras el golpe cargado o Shift. En sus PlayerPrefs tenía el sprint en modo alternar (`IronRequiem.SprintToggleKeyboard = 1`), Melee en clic derecho y Apuntar en F. Pidió «quita la función del Shift y que tengamos siempre la misma velocidad». Respaldo: `Tools/IronRequiem/Backups/20261123-no-sprint/`.

- [x] `PlayerInputHandler.GetSprintInputHeld()` devuelve siempre `false`: Shift no hace nada y se camina siempre a `MaxSpeedOnGround` (13 m/s). La acción Sprint sigue en el asset, sin uso.
- [x] `AdvancedPlayerMovement`: el slide ya no exige sprint. C con la velocidad de marcha (≥11 m/s) lo inicia.
- [x] Menús: se quitan los interruptores «Sprint con teclado / mando», su nota y la fila «Sprint» de reasignar teclas.
- [x] Verificado con `Tests/PlayMode/HeavinessRepro.cs` (campaña real, con los ajustes y teclas del usuario; informe en `Tools/IronRequiem/Reports/Controls/heaviness-repro.txt`):
  - con W se va a 13,0 m/s y con W+Shift también a 13,0;
  - tras golpe cargado completo o corto, Shift durante la carga, slide o apuntar con F, desde el mismo punto: cámara igual (59,8°), escala de tiempo 1, ningún estado pegado, y 90 % de la velocidad en 52–58 ms.
- [x] Causa más probable del «pesado» repentino: el sprint en modo alternar. Se apagaba solo al soltar el movimiento o pulsar C y la velocidad caía de 26 a 13 m/s. Sin sprint ya no puede pasar.
- [x] Tests:
  - `ControlsRebindPlayTests` 4/4;
  - `MechanicsBehaviorTests` 15/18; incluye el slide sin sprint (`SlideRequiresSprint…` pasa con C a velocidad normal);
  - fallos previos sin relación: FullCharge y SlideContact (fallaban antes) y ChargedEnergy (no pudo guardar una captura por archivo bloqueado);
  - `SlidePistolMainTests` igual que antes.
- [ ] `TwoFloorGapTests`: el Editor se bloqueó cargando `ChaseCorridorLab` (326 s de espera del hilo de carga) y el vigilante cortó la ejecución. Pendiente de repetir.
- [ ] El nombre `SlideRequiresSprint…` del test de mecánicas ya no describe el comportamiento (el slide no requiere sprint); el test pasa igual.

## Chat 13 — «Pesado» tras el primer módulo: era apuntar con F (23/11/2026)

- [x] `Tests/PlayMode/ModuleHeavinessProbe.cs` (campaña real, informe en `Tools/IronRequiem/Reports/Controls/module-heaviness.txt`):
  - antes y después del primer módulo y en Room01 y la sala 2: 76–94 FPS, nada pegado, arranque al 90 % en 40–63 ms;
  - con las teclas del usuario (Apuntar = F, Melee = clic derecho), mantener F con el módulo activa el apuntado: la cámara gira a 0,4× (`PlayerCharacterController.AimingRotationMultiplier`);
  - sin arma, F no hace nada: por eso empieza justo tras el primer módulo;
  - al soltar F vuelve a 1,0× (también tras carga, golpe o cambio de arma).
- [x] El usuario eligió «quitar freno al apuntar»: `AdvancedPlayerMovement.aimLookMultiplier = 1` sustituye en Awake el 0,4 de la plantilla (el prefab no se toca). Verificado: apuntando con el módulo, la cámara gira a 1,00×. Respaldo: `Tools/IronRequiem/Backups/20261123-no-sprint/AdvancedPlayerMovement.before-aim.cs`.
