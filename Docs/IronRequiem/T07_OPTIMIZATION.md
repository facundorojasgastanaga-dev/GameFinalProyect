# T-07 común — geometría optimizada y LOD

30 de octubre de 2026, America/La_Paz. Blender 5.2.3 LTS y Unity 6000.5.5f1.

Actualización posterior: [T07_ANIMATION_VFX.md](T07_ANIMATION_VFX.md) documenta la integración del enemigo común, el rig compartido de sus tres LOD y la explosión del cañón. Las afirmaciones de ausencia de rig y las pruebas de este informe corresponden a la entrega estática de geometría; los FBX optimizados originales se conservan como fuentes estáticas.

## Resultado

Se inspeccionaron de nuevo los **dos archivos adjuntos**, no el Rustborn Sentinel anterior. FBX y GLB contienen 50.000 triángulos, una malla y un material; ninguno trae huesos, pesos o clips. Sus hashes coinciden con los archivos usados para preparar el T-07 nuevo. Se reutilizó la copia con cuerpo y antebrazo/cañón separados y altura de 2,05 m, conservando sus pivotes y coordenadas locales.

| Nivel | Cuerpo | Cañón | Total de triángulos | Reducción frente a los 50.000 originales |
|---|---:|---:|---:|---:|
| LOD0, cerca | 10.225 | 1.841 | **12.066** | **75,9 %** |
| LOD1, distancia media | 5.111 | 921 | **6.032** | **87,9 %** |
| LOD2, lejos | 2.128 | 383 | **2.511** | **95,0 %** |

Los vértices de Blender son 6.053 / 3.021 / 1.238. Las divisiones de normales y UV aumentan los vértices exportados/importados; no confundir estos recuentos con triángulos. Los seis archivos portátiles FBX/GLB fueron reimportados y conservaron exactamente los triángulos, dos piezas, UV y escala. Se limpiaron caras duplicadas detectadas durante la validación del borrador.

El método es reducción de superficie mediante Decimate, con conservación de atributos UV. No es retopología manual para deformación. No se inventó un rig. Se revisaron comparaciones frontal, lateral y posterior con iluminación equivalente. LOD0 mantiene bien la silueta; LOD2 deforma detalles de cabeza, antena y arma al observarlo de cerca y se reserva para lejos. El corte y las tapas del cañón siguen siendo los provisionales del prototipo anterior.

## Assets para usar en este proyecto

- Enemigo: `Assets/IronRequiem/Prefabs/Enemies/T07_Common_Optimized.prefab`.
- Cañón equipado: `Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab`.
- Prueba: `Assets/IronRequiem/Scenes/T07OptimizationLab.unity`.
- Mallas: `Assets/IronRequiem/Art/Enemies/T07Optimized/T07_LOD0.fbx`, `T07_LOD1.fbx`, `T07_LOD2.fbx`.

Los dos prefabs son variantes de los existentes. La variante enemiga conserva IA, navegación, percepción, salud, daño de unión, desprendimiento, energía y efectos. La escena nueva es una copia de CharacterLab con el enemigo optimizado y la referencia de patrulla actualizada; reutiliza su NavMesh. MainScene, CharacterLab y los prefabs originales no se sustituyeron.

Hay **dos LODGroup independientes**, uno para el cuerpo y otro en la propia raíz desmontable del cañón. El cañón conserva su grupo al desprenderse. Al equiparlo, sus tres representaciones del mundo se ocultan; al lanzarlo recuperan visibilidad. El arma FPS usa la malla LOD0 del cañón y mantiene el controlador, carga, proyectil y referencias existentes. No se añadió un controlador o un sistema de armas paralelo.

Los umbrales iniciales de altura relativa en pantalla son cuerpo **0,35 / 0,12 / 0,006** y cañón **0,18 / 0,06 / 0,003**. El último valor es el umbral de ocultación visual; no desactiva IA, colliders o daño. La selección también depende de FOV y `QualitySettings.lodBias`. Son valores iniciales de revisión, no distancias universales en metros. Se evita el fundido entre LOD para no dibujar dos niveles durante la transición.

Se comparte el material URP/Lit existente, con sus mapas y límite de importación 2048. Las piezas siguen separadas para permitir la mecánica modular: **dos renderers/submeshes del robot por nivel, un material compartido**, además de los indicadores y VFX del prototipo. Esta entrega reduce geometría; no afirma reducir los draw calls. Las variantes conservan dependencias de los assets base, de modo que tampoco se afirma reducir automáticamente el tamaño del build o toda la memoria cargada.

## Archivos portátiles

- Paquete nativo: `Tools/IronRequiem/Exports/T07_Common_Optimized.unitypackage`.
- FBX/GLB y mapas 2048: `Tools/IronRequiem/Exports/T07Optimized/`.
- Editable con texturas empaquetadas: `Tools/IronRequiem/Blender/Working/T07Optimized/T07_Optimized.blend`.
- ZIP de modelos, mapas, editable, comparación e informe: `Tools/IronRequiem/Exports/T07_Optimized_Models.zip`.

Para usar el enemigo aquí, abrir T07OptimizationLab y pulsar Play. Para incorporarlo a otro nivel, usar la variante T07_Common_Optimized. Para llevar el modelo a otro proyecto, importar el paquete en Unity con URP o usar el FBX con sus PNG adyacentes. El GLB contiene sus propias texturas. Los tres GLB son niveles estáticos separados: el cambio automático de LOD está configurado en el prefab de Unity. No importar a la vez el paquete y duplicados de los mismos assets innecesariamente.

## Verificación y límites

**Tres pruebas aprobadas en el Editor real: dos EditMode y una PlayMode.** La prueba de ejecución avanzó 150 frames y comprobó navegación sobre NavMesh, llamadas de selección de los tres niveles, desprendimiento repetido sin duplicar módulos, conservación del LODGroup, equipamiento mediante el contrato del módulo, ocultación de todos los LOD del mundo, sincronización de energía, humo crítico, lanzamiento con recuperación de visibilidad y explosión una sola vez. No fue una prueba con teclado físico ni una repetición completa de la campaña o del sistema de carga de disparos.

Los informes están en `Tools/IronRequiem/Reports/T07Optimization/`: `inspection.json`, `geometry.json`, `roundtrip.json`, `unity-final.json`, `play.xml`, `edit.xml`, `runtime.json` y `preservation.json`. Las imágenes `comparison-front.png`, `comparison-side.png` y `comparison-back.png` muestran geometría real, no imágenes generadas por IA. El informe inicial `unity.json` corresponde al borrador anterior a limpiar una cara duplicada por nivel; consultar **unity-final.json** para la entrega final. La copia preparada tenía 50.313 triángulos en Blender y 50.161 según su importación previa en Unity; las tapas y caras duplicadas explican que no se use ese borrador como equivalente exacto del original de 50.000.

Se registraron hashes de **1.879 archivos protegidos**, incluidos ambos adjuntos externos, Assets, Packages y ProjectSettings. Al guardar assets, Unity reserializó incidentalmente el material previo ChargeEnergyCore; se recuperaron sus bytes exactos comprobando el SHA-256 de la instantánea. El respaldo del estado serializado queda bajo `Tools/IronRequiem/Backups/20261029-t07-optimization/`. La comprobación final informa cualquier diferencia; no se aceptan cambios silenciosos a originales.

**No hay un benchmark de FPS antes/después con oleadas.** El descenso medido es de geometría. Como cálculo de presupuesto, 20 robots de 50.000 triángulos suman 1.000.000; 20 al mismo LOD0 suman 241.320, sin incluir VFX ni pasadas extra. Esto no predice por sí solo el frame time. Falta medir una carga representativa en un Player de PC, incluyendo sombras, IA, navegación, partículas y armas sueltas, y ajustar los umbrales con esas medidas. Tampoco se añadieron rig, skinning ni ciclos de caminar.

Herramientas: skills `iron-requiem-performance`, `iron-requiem-modular-weapons`, `iron-requiem-enemy-ai` y consulta de Unity CLI; Blender local; APIs nativas del Editor, LODGroup, URP y Test Framework existentes. La CLI evitó lanzar otro Editor al detectar el proceso abierto. No se instaló Pipeline experimental ni otra herramienta, no hubo compras o créditos Meshy y no se hicieron commits.
