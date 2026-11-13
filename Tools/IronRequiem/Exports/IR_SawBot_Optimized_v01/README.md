# IR SawBot optimizado v01

Base: sawbot.fbx. sawbot2.fbx contiene exactamente la misma geometría, UV y texturas; ambos originales permanecen intactos.

- Original: 49.948 triángulos. LOD0: 22.184; LOD1: 12.384; LOD2: 8.200.
- Los FBX ensamblados tienen una malla y un material por LOD. No incluyen rig, pesos, animaciones, colisiones ni IA.
- Escala original conservada: altura de 0,778153 m. Los tres LOD apoyan las ruedas a la misma altura.
- Sierras reconstruidas: guías cerradas, cadenas continuas, enlaces regulares, cortadores alternados y remaches. Las piezas originales distales se reemplazan en copias; las carcasas se conservan y sus cortes quedan cerrados.
- IR_SawBot_Parts.fbx contiene cuerpo, dos guías y dos cadenas, con pivotes de las sierras en sus uniones. El Blender editable contiene colecciones de los tres LOD y una referencia oculta; no exportar la colección Source_Reference_DoNotExport.
- Texturas originales reducidas de 4K a 2K, con normales renormalizadas. UV originales conservadas fuera de las sierras; las piezas nuevas usan proyección planar sobre muestras metálicas del atlas original, evitando estiramientos entre islas.

## Unity URP

Importar los FBX junto a Textures. Usar URP/Lit: BaseColor en Base Map; Normal como Normal Map; MetalSmooth en Metallic Map, con suavidad en el canal alfa. Importar MetalSmooth sin sRGB. Roughness y Metallic independientes se conservan para Blender y otros motores. Conectar los tres FBX a un LODGroup según el tamaño y cámara del juego.

El paquete ZIP contiene FBX, Blender, texturas, vista y verificación. Las versiones GLB autocontenidas están disponibles aparte en la misma carpeta de exportación para no duplicar las texturas tres veces en el ZIP.

Verificación real: reimportación de los seis FBX/GLB de LOD en Blender; conteos, escala, UV finitas, un material y superficies cerradas después de soldar las divisiones propias del formato. Se inspeccionaron renders frontal, lateral y tres cuartos. No se probó en Play Mode ni se midieron FPS. Esta entrega prepara el modelo; no lo convierte en un enemigo jugable ni modifica escenas.