# T-07: integración, animación y explosión

30 de octubre de 2026, America/La_Paz. Integración aplicada y regresión final aprobada.

## Fuentes e inspección

Se parte de T07_Common_Optimized, sus tres FBX optimizados y la separación de cuerpo/cañón documentada en T07_OPTIMIZATION.md. Los FBX optimizados originales siguen sin huesos, pesos o clips y permanecen intactos. No se emplea el Rustborn Sentinel de Exterminador.

La inspección nueva de Blender detectó cuatro componentes conectados en el cuerpo LOD0, con 5.088 de sus 5.139 vértices de Blender en el componente principal. El cañón es una pieza separada de 914 vértices. Los recuentos del importador Unity incluyen costuras UV y normales divididas. Separar por islas no habría producido articulaciones útiles para todo el cuerpo. Se optó por un skin híbrido con placas mayormente rígidas y mezclas cortas de dos pesos en los codos, rodillas, tobillos, cuello y hombros.

La copia nueva T07_SharedRig.fbx contiene un esqueleto real de **17 huesos**, compartido por los tres skins del cuerpo. Conserva los triángulos **10.225 / 5.111 / 2.128**, UV y material original. El cañón mantiene sus mallas estáticas y LODGroup independiente. La fuente editable está en Tools/IronRequiem/Blender/Working/T07Animated/T07_SharedRig.blend; el autor reproducible es Tools/IronRequiem/Blender/rig_t07.py. Ninguna fuente original se sobrescribe.

## Referencias de video

Se decodificaron localmente ambos MP4 con el secuenciador de Blender. No se extrajeron clips reutilizables ni assets comerciales de los videos. El primer archivo dura 56,5 s a 30 fps: muestra una demostración de animaciones Robot Police, con apoyos alternados, locomoción armada, giros, paradas y reacciones. Se revisaron muestras del principio, centro y final, incluidas capturas entre 30 y 55 s. Se adapta su idea de peso y estabilidad del torso a la silueta y cañón del T-07.

El segundo dura 13,03 s a 30 fps. El primer plano ya comienza con una explosión en curso: núcleo amarillo/blanco, expansión naranja, chispas rojas y humo oscuro, que se disipa durante unos segundos. Después aparecen logos y otra vista. No permite medir con precisión el inicio del destello. La versión local prioriza lectura rápida y presupuestos de PC: destello de 0,085 s, luz de 0,12 s, núcleo de aproximadamente 0,55 s, onda de 0,38 s, fragmentos/chispas de menos de 1 s y humo hasta 2,7 s; raíz destruida a los 3,2 s.

Metadatos, hashes de los videos, hojas de contacto y geometría están en Tools/IronRequiem/Reports/T07Animation. El audio de referencia no se ha usado como asset; se reutiliza el sonido existente del efecto de explosión.

## Integración técnica

T07AnimationDriver conserva NavMeshAgent como autoridad del desplazamiento. La fase de locomoción sigue la distancia recorrida y usa los parámetros reales MoveSpeed, Attack, Alerted y OnDamaged, más Cycle, Turn, Disarmed y Dead. Animator no aplica root motion. Se crean Idle, CombatIdle, Walk, Turn, CannonAttack, HitReaction, DisarmedWalk y Death, más una pose neutra de capa superior para limpiar interrupciones de ataque.

Animation Rigging 1.4.1, ya instalado, resuelve dos cadenas TwoBoneIK. T07FootPlacement mantiene objetivos de apoyo en mundo, eleva los pies durante el avance y consulta el suelo con una máscara limitada. El skin es provisional, con retopología optimizada para superficie, no una retopología manual de deformación.

T07CannonSocket sigue el antebrazo mediante el pivote existente, sin reparentar objetos heredados del prefab ni incluir huesos en el módulo desprendible. Conserva el apuntado de EnemyController durante combate. Al desprenderse, el módulo deja de seguir el socket, conserva sus tres LOD, pasa a la física y sigue usando recogida, carga, energía, humo crítico, lanzamiento y daño existentes. La muerte interrumpe las capas de ataque, cancela IA y detiene el agente antes de reproducir el colapso; EnemyController sigue registrando la muerte y destruyendo el cuerpo tras 1,65 s.

La variante CannonExplosion_IR deriva de VFX_HoverBot_Explosion. CannonExplosion conserva el único punto de aplicación de DamageArea; CannonExplosionPresentation solo controla presentación. Los fragmentos son partículas de malla sin colliders ni rigidbodies. Hay materiales URP compartidos y texturas locales pequeñas, sin instancias de material por partícula. El destello admite como máximo dos luces simultáneas y no usa sombras. DistanceCameraImpulse añade una perturbación breve según distancia y restaura la base antes de que el controlador FPS procese el siguiente frame.

## Escenas y preservación

La búsqueda inicial encontró el T-07 anterior en CharacterLab. T07OptimizationLab ya usaba la variante optimizada. La referencia del prefab optimizado a T07_IR_v01 es su herencia y se conserva; no se sustituye por sí misma. No se encontraron referencias directas del T-07 en MainScene ni en un sistema de aparición de runtime.

Se preparó primero T07_Common_Animated_Working, una copia independiente para el laboratorio. El cambio de escenas usa PrefabUtility.ReplacePrefabAssetOfPrefabInstance con coincidencia por jerarquía y conservación de overrides; comprueba transforms y referencias de PatrolPath. Tras aprobar el laboratorio, se actualizó T07_Common_Optimized conservando su GUID y condición de variante, y se sustituyó una instancia en CharacterLab. T07OptimizationLab volvió a usar directamente el prefab común validado. MainScene no tenía ese T-07 y no se modifica. El registro detallado está en scene-migration.txt.

Respaldo inicial: Tools/IronRequiem/Backups/20261030-t07-animation-vfx, con **1.900 hashes protegidos**, incluidos ambos T-07 externos. La primera creación de objetos temporales marcó CharacterLab como dirty: el generador se detuvo sin guardar. Una copia de memoria creada mediante SaveScene(saveAsCopy) resultó byte idéntica al archivo original; solo entonces se reabrió la escena intacta. Las creaciones posteriores se aíslan en una escena temporal propia. El registro queda en transient-scenes.txt.

## Verificación y pendientes

El laboratorio final aprobó **6/6 PlayMode y 5/5 EditMode**, con captura visual de los tres LOD, reposo, caminar, ataque, reacción/desarme, muerte y tres fases de la explosión. Se probaron recogida con F y carga/soltado con el ratón virtual del Input System, consumo de 30 unidades para el tiro completo, humo crítico, lanzamiento con G, LOD del cañón liberado y detonación idempotente. La explosión equipada dañó al jugador una sola vez; sus partículas no añadieron daño. Ocho efectos simultáneos respetaron el límite de dos luces, terminaron y devolvieron posición/rotación de cámara a su base. Un efecto a 150 m terminó sin luz o sacudida. Se comprobó inicio de audio y sacudida cercana; no fue una escucha manual ni una prueba de teclado físico.

La primera ejecución había aprobado 4/6 PlayMode y detectó deriva de 1 cm al morir y error de apoyo de 19,2 cm. Se detiene y desactiva el agente al morir y se corrigió la altura de la pelvis. Las muestras posteriores registraron objetivos de apoyo inmóviles y errores máximos reales de aproximadamente 8–10,5 cm; no se afirma contacto perfecto de las suelas. Los diagnósticos previos se conservan, incluidos un intento de etapa con ensamblado anterior y una llamada de Editor no disponible corregida antes de la verificación final. La regresión conserva los escenarios existentes; su expectativa de destrucción ahora espera DeathDuration en lugar de exigir muerte instantánea incompatible con la animación.

**Regresión final a las 01:57 America/La_Paz: 40/40 pruebas aprobadas**, 28 PlayMode y 12 EditMode. Los resultados están en regression-play.xml y regression-edit.xml. Incluyen movimiento y cámara FPS mediante Input System, pistola, cambio de arma, patrulla/detección/ataque/pérdida de objetivo, retirada desarmada, daño, carga, energía, humo, lanzamiento, fusible, muerte y explosión. La muestra final de locomoción registró 35 frames de apoyo, deriva del objetivo 0 m y error máximo del pie de 0,0817 m. Las hojas de contacto y capturas se abrieron y revisaron; son renders del proyecto, no imágenes generadas.

**Integridad final a las 01:59: all_checks_pass=true**, frente a 1.900 archivos protegidos. Solo cambiaron los siete archivos preexistentes previstos: dos asmdef propios para referenciar Animation Rigging instalado, dos escenas, el prefab común, el getter de presentación de CannonExplosion y la espera de muerte del test de personaje. Modelos, GUID/meta anteriores, prefab T07_IR_v01 de respaldo, MainScene, scripts FPS de producción, Packages, ProjectSettings, URP e Input System conservaron sus hashes. Se restauraron los bytes de ChargeEnergyCore.mat tras su reserialización incidental por URP. El Editor quedó en CharacterLab, fuera de Play Mode, escena limpia, cero scripts ausentes y sin errores de compilación capturados. delivery-verification.json y verify_t07_animation_delivery.py permiten revisar la evidencia.

La prueba existente de 120 frames dejó una muestra de Editor a 2560 × 1440, calidad Default, VSync 0, un enemigo y RTX 4050 Laptop: media 7,81 ms, p95 9,63 ms, último main thread 6,87 ms y última asignación registrada de 9.163 bytes. Incluye el Editor y el arnés de pruebas. No es un antes/después controlado, un perfil de ocho explosiones ni una medición de Player PC; no acredita una ganancia de FPS.

## Entrega y ajuste

- Enemigo común aplicado: Assets/IronRequiem/Prefabs/Enemies/T07_Common_Optimized.prefab.
- Laboratorio inicial y reproducción: Assets/IronRequiem/Scenes/T07OptimizationLab.unity. CharacterLab usa ahora el mismo enemigo común.
- Rig, Avatar, controlador, máscara y clips: Assets/IronRequiem/Art/Enemies/T07Animated/.
- Explosión reutilizable: Assets/IronRequiem/Prefabs/VFX/CannonExplosion_IR.prefab, variante del efecto de plantilla.
- Copia de trabajo conservada: T07_Common_Animated_Working.prefab. Los prefabs y modelos previos permanecen como respaldo.

Abrir cualquiera de los laboratorios y pulsar Play. Los controles F, M1 y G del cañón se conservan. El inspector de T07AnimationDriver expone Stride Distance; T07FootPlacement expone altura de paso, proporción de apoyo y máscara de suelo. El Animator contiene clips y transiciones editables. En CannonExplosionPresentation se ajustan duración, flash, onda y sacudida; cada Particle System expone sus bursts, curvas y tiempos. El núcleo admite hasta ocho partículas, con máximo total configurado de 65 en las capas nuevas. La lógica de daño y energía sigue separada.

La geometría sigue teniendo los presupuestos optimizados previos: esta tarea añade skinning, animación y VFX. **No se ha medido una mejora de FPS**. Falta un benchmark representativo en Player PC con oleadas, y ajuste manual de pesos/poses y contacto de suelas, audio y efectos. La versión usa partículas y máscaras procedurales; igualar la densidad, iluminación volumétrica y detalle del video requeriría mejores texturas animadas de fuego/humo y una revisión artística adicional. No se compraron assets, consumieron créditos ni instalaron paquetes.

Herramientas: skills locales de IA enemiga, armas modulares y rendimiento; consulta de VFX, Unity CLI y fuentes de Animation Rigging instaladas; Blender 5.2.3 LTS para inspección, rigging, pesos y decodificación de referencias; APIs de Editor/AnimatorController/PrefabUtility, Particle System, URP y Test Framework. Computer Use no conectó tras su recuperación; se continuó con las solicitudes locales específicas y las APIs del Editor abierto, sin lanzar otro Editor ni instalar Pipeline o un servidor alternativo. Las utilidades específicas no ejecutan código arbitrario recibido por archivos.
