# Pistola modular — modelo de 1.000 vértices

Versión de baja poligonización modelada localmente en Blender 5.2.3 LTS a partir de `pistol-module-v02-straight.png`. Conserva la silueta recta, acople posterior, placas claras, estructura oscura, pestañas naranja y luces cian. Es una reconstrucción simplificada para el presupuesto solicitado, no una conversión exacta de cada detalle de la imagen. No se usó generación remota ni créditos.

## Geometría y materiales

- **1.000 vértices** en el modelo de Blender, el GLB exportado y la malla importada en Unity.
- **532 triángulos**, un submesh y un material. El recuento incluye los vértices separados por normales planas y costuras UV que realmente necesita renderizar el modelo; no hay vértices de relleno sin caras.
- Atlas de color y emisión de **768 × 768**; GLB y Blender incluyen las imágenes incrustadas. El FBX usa las imágenes PNG adyacentes.
- Dimensiones del prefab de Unity: aproximadamente **11,7 cm de ancho × 13,8 cm de alto × 37,9 cm de largo**.
- Origen situado en el acople posterior. El prefab visual apunta hacia **+Z**, con los transforms `Attachment` y `Muzzle` preparados. No requiere rig para usarse como módulo rígido.

## Archivos

- `Assets/IronRequiem/Art/Weapons/BasicPistolModule/PistolModule_1000.fbx`: modelo para el importador nativo de Unity.
- `Assets/IronRequiem/Art/Weapons/BasicPistolModule/PistolModule_1000.glb`: modelo portátil con texturas incrustadas.
- `Assets/IronRequiem/Art/Weapons/BasicPistolModule/PistolModule_URP.mat`: material nativo URP/Lit con color y emisión asignados.
- `Assets/IronRequiem/Prefabs/Weapons/Visuals/PistolModule_1000_Visual.prefab`: representación visual lista para colocar en el proyecto.
- `Tools/IronRequiem/Blender/Working/BasicPistolModule/PistolModule_1000.blend`: fuente editable, sin cámaras o luces de la captura y con texturas empaquetadas.
- `Tools/IronRequiem/Blender/create_pistol_module.py`: generador local reproducible. Rechaza sobrescribir una entrega salvo que se indique explícitamente la opción de revisión de borrador.

## Uso

El modelo se integra ahora en Weapon_IR_Pistol mediante la actualización descrita en [WEAPON_FEEDBACK.md](WEAPON_FEEDBACK.md). La fuente visual sigue siendo independiente y sus archivos originales se conservan.

En este proyecto el prefab visual ya está disponible. Se puede colocar como representación gráfica de la pistola existente, conservando WeaponController, su lógica de disparo, sus colliders y las referencias de juego. El prefab entregado contiene la malla, material y sockets; no cambia automáticamente el arma equipada ni añade otra implementación de combate.

Para trasladarlo a otro proyecto con URP, importar el paquete `Tools/IronRequiem/Exports/PistolModule_1000.unitypackage`. Para Blender u otras herramientas se puede usar el GLB o abrir el .blend. Al mover el FBX solo, incluir ambos PNG junto a él.

## Verificación

Se exportó y reimportó el FBX en Blender, se inspeccionó el recuento del GLB y se comprobó la malla importada en Unity. Unity creó el material y prefab mediante APIs del Editor en una escena de previsualización independiente, sin guardar ni modificar la escena activa.

Evidencias: `Tools/IronRequiem/Reports/pistol-module-model.json`, `pistol-module-fbx-roundtrip.json`, `pistol-module-unity.json` y `pistol-module-preview.png`. La creación del material/prefab se reproduce desde `Tools > Iron Requiem > Weapons > Build 1000 Vertex Pistol Visual`, que rechaza sobrescribir assets ya entregados. La herramienta no abre un servidor ni ejecuta instrucciones arbitrarias desde archivos.
