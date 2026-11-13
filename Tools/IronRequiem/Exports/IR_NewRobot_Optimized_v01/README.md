# IR_NewRobot_Optimized_v01

Copia optimizada y modular del robot adjunto. No sustituye modelos ni personajes de IRON REQUIEM.

- Original FBX: 50.060 triángulos.
- LOD0: 24.104 triángulos (51,8 % menos).
- LOD1: 12.376 triángulos (75,3 % menos).
- LOD2: 5.494 triángulos (89,0 % menos).
- 15 piezas en cada nivel: torso con cabina, pelvis, módulo trasero, dos conjuntos de hombro/brazo superior, dos antebrazos, dos pinzas, dos muslos, dos pantorrillas y dos pies.
- Cortes geométricos cerrados con tapas; mismo atlas/material. UV originales conservadas en la superficie exterior. Las tapas usan una muestra grafito del atlas.
- Texturas PBR de 2.048 × 2.048, derivadas de las originales de 4K. Se conserva la escala de la fuente: aproximadamente 0,862 m de alto; no se convirtió a escala humana.

## Archivos

- `IR_NewRobot_Editable.blend`: copia editable, LOD0 visible, referencia y otros LOD ocultos. Los originales FBX/GLB quedan conservados fuera del paquete.
- `IR_NewRobot_LOD0/1/2.fbx` y `.glb`: ensamblajes completos con piezas independientes y pivotes iguales entre niveles.
- `IR_NewRobot_Parts.fbx`: ensamblaje modular LOD0.
- `Parts/`: las 15 piezas LOD0 en FBX individuales, manteniendo sus pivotes y posiciones de montaje. Poner a cero la transformación de una pieza tras importarla la centra por su pivote, no por su caja envolvente.
- `Textures/`: BaseColor, Normal, Metallic, Roughness y MetalSmooth para URP. En el último mapa, RGB contiene metallic y alfa contiene 1 − roughness.
- `Previews/`: renders reales del modelo ensamblado, separado y comparado en los tres LOD.
- `Verification/`: recuentos y verificación de reimportación.

## Uso en Unity / URP

Importar el FBX deseado junto con `Textures`. Reutilizar un solo material URP/Lit compartido: BaseColor en Base Map; Normal como textura Normal Map; MetalSmooth con sRGB desactivado en Metallic Map y smoothness desde su alfa. Metallic y Smoothness a 1 para que manden los mapas. Metallic/Roughness separados se incluyen para otros programas; no son necesarios además de MetalSmooth en URP.

Los tres LOD están exportados, pero este paquete no crea un prefab ni configura LODGroup en el juego. Conectar los ensamblajes a un LODGroup al integrarlos; no mostrar los tres simultáneamente. Las piezas conservan sus nombres y puntos de articulación correspondientes. Separar piezas aumenta el número potencial de renderers/draw calls: no se afirma una mejora de FPS sin medir el caso integrado.

## Estado y límites

Mallas estáticas: no hay rig, pesos ni animaciones, igual que en las fuentes recibidas. Los pivotes permiten preparar articulación rígida posteriormente; no equivalen a un esqueleto ni a articulaciones de PhysX. La cabina permanece integrada en el torso; las pinzas son conjuntos completos, sin dedos articulados individualmente.

Se verificaron 22 archivos exportados por reimportación en Blender: recuentos de triángulos, 15 piezas por nivel, tapas cerradas tras soldar duplicados de costuras, UV finitas, materiales/texturas 2K, pivotes y ausencia real de rig. FBX y GLB fuentes representan prácticamente la misma superficie (máxima distancia muestreada de 0,027 mm), aunque sus triángulos y divisiones por costura difieren. Los hashes de ambos originales se conservaron.

No se importó ni se probó en Unity, no se midieron FPS y no se modificaron escenas, gameplay, armas, enemigos ni el protagonista actual. Se usó Blender local; no hubo generación ni consumo de créditos.
