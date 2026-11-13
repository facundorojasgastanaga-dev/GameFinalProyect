# IR Protagonist — optimizado modular v01

Entrega del 31 de octubre de 2026. **Archivos preparados, sin importar en Unity ni modificar el jugador.** Toda esta carpeta está fuera de Assets. Para continuar, enviar el ZIP y el texto PROMPT_INTEGRACION.txt al chat de trabajo.

## Archivos principales

| Archivo | Uso | Triángulos |
|---|---|---:|
| IR_Protagonist_LOD0.fbx / .glb | Cuerpo completo cercano, manos con detalle conservado | 21.108 |
| IR_Protagonist_LOD1.fbx / .glb | Cuerpo completo a distancia media | 11.628 |
| IR_Protagonist_LOD2.fbx / .glb | Cuerpo completo lejano | 6.122 |
| IR_Protagonist_FPS_Parts.fbx / .glb | Solo antebrazos y manos LOD0 para vista FPS | 6.070 |
| IR_Protagonist_Editable.blend | Fuente editable de todas las piezas, con texturas empaquetadas | Varias versiones |

Las fuentes FBX y GLB suministradas contienen la misma superficie de 50.000 triángulos y dimensiones coincidentes. Se eligió el FBX para trabajar porque tiene vértices más unidos; el GLB fuente divide índices en las costuras. Los FBX y GLB entregados son exportaciones de la misma copia optimizada, no dos personajes diferentes. Frente a los 50.000 originales, la reducción total es aproximadamente 57,8 %, 76,7 % y 87,8 %. Las manos se conservan con más detalle en LOD0 deliberadamente; reducirlas como el torso dañaba su forma para la primera persona.

Los totales incluyen tapas y cortes de separación. Las cantidades de vértices pueden variar entre Blender, FBX, GLB y Unity por normales/UV divididas; no confundirlas con triángulos. Esta entrega no acredita una mejora de FPS ni el objetivo de 120 FPS del juego.

## Preparación modular

Cada cuerpo completo contiene cinco objetos:

- Body_LOD#: torso, cabeza, piernas y brazos superiores.
- Forearm_L_LOD# y Forearm_R_LOD#: antebrazos separados, origen en el codo.
- Hand_L_LOD# y Hand_R_LOD#: manos separadas, origen en la muñeca.

Los cortes se realizaron en planos medidos de codo y muñeca, antes de separar las caras. Ambos lados de cada unión comparten la sección geométrica de ese nivel. Se cerraron los cortes y las tapas usan una muestra oscura del mismo atlas: no requieren un material adicional. Hay cinco objetos/submeshes por cuerpo completo, con un material compartido; el kit FPS tiene cuatro. La modularidad no se presenta como una reducción de draw calls.

Se incluyen seis guías vacías: Elbow_L, Elbow_R, Wrist_L, Wrist_R, ModuleSocket_L y ModuleSocket_R. No son un esqueleto. El +Z local de cada guía apunta desde el codo hacia la muñeca. Los sockets de módulo coinciden con la muñeca; su ajuste a cada arma real corresponde a la integración, no se probó aquí. En el .blend las guías llevan sufijo _LOD# para distinguir las tres versiones; en cada exportación FBX/GLB sus nombres son los seis indicados.

Los antebrazos y manos del kit FPS usan las mismas posiciones y pivotes que LOD0. El archivo FPS no contiene el torso, una pistola ni un arma enemiga. No importar y superponer ambas representaciones como si fueran personajes distintos. El arma básica ya existente debe acoplarse como módulo separado durante la integración.

## Rig, melee y agarres: estado real

**No hay huesos, pesos ni animaciones en las fuentes ni en las salidas.** Se conservan mallas estáticas en pose A. La entrega permite separar/mostrar/ocultar piezas y añadir un rig posteriormente; no incluye ya implementados el cambio de modo, un golpe melee, un puño cerrado o un agarre animado. Ningún código de combate se modificó.

La geometría de manos procede del modelo generado original. Su estructura puede necesitar limpieza/retopología para flexionar dedos sin intersecciones; la reducción de polígonos no repara anatomía ni añade articulaciones. Las guías de muñeca/codo son puntos iniciales medidos, sujetos a revisión durante el rig. Los cortes y las superficies interiores también requieren revisión artística de cerca para producción.

## Escala, orientación y texturas

La fuente medía aproximadamente 1,140539 m. La copia se normalizó a aproximadamente **1,80 m de altura**, centrada en X/Y con los pies en Z=0. Blender: Z arriba, frente -Y. FBX: opciones estándar de exportación -Z forward / Y up. GLB: conversión de ejes del exportador glTF. Reimportaciones en Blender comprobaron escala y posiciones; **no se verificó todavía el importador de Unity**, por instrucción del usuario.

Textures contiene PNG de 2048×2048:

- IR_Protagonist_BaseColor.png: color, sRGB.
- IR_Protagonist_Normal.png: normal map, datos lineales.
- IR_Protagonist_Metallic.png y IR_Protagonist_Roughness.png: mapas originales reducidos, lineales.
- IR_Protagonist_MetalSmooth.png: conversión para URP, metallic en R y smoothness = 1 − roughness en A; importar lineal.

Mantener la carpeta Textures junto a los FBX para resolver sus rutas relativas. Cada GLB es autocontenido y contiene texturas 2K; el .blend tiene los mapas empaquetados. Compartir un material URP en la integración. Los LOD son archivos independientes: todavía no existe un LODGroup o prefab nuevo dentro del juego.

## Evidencia

Previews muestra comparaciones frontal/lateral/posterior del original y los LOD, piezas separadas y una mano de cerca. Son renders de las mallas reales en Blender. Verification contiene inspección de ambas fuentes, recuentos, parámetros de separación y verificación de ocho archivos reimportados (cuatro variantes por dos formatos): triángulos, piezas, UV, material, escala, pivotes, guías y mapas 2K.

Se tomó una instantánea de 2.119 archivos protegidos de Assets, Packages, ProjectSettings y las dos fuentes externas. La verificación de preservación informa cualquier diferencia. No se abrió ni controló Unity, no se guardaron escenas/prefabs ni se generaron builds o pruebas Play Mode. Herramientas: Blender 5.2.3 LTS, Python local y criterios de las skills de rendimiento, FPS y armas modulares; sin compras, créditos o instalaciones.
