# Herramienta local de Blender

Ejecutar exclusivamente en un proceso nuevo con `--background --factory-startup`. El script rechaza una sesión interactiva y nunca sobrescribe un FBX de salida existente. La escena temporal se descarta al salir; no se guarda un `.blend` ni se toca el Editor de Blender del usuario.

Desde PowerShell en la raíz del proyecto:

```powershell
$blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
$tool = 'Tools/IronRequiem/Blender/blender_asset_tool.py'
$model = 'Assets/IronRequiem/Art/Enemies/Exterminador/Models/Meshy_AI_Rustborn_Sentinel_20261028_texture.fbx'
& $blender --background --factory-startup --python-exit-code 1 --python $tool -- inspect --input $model --report Tools/IronRequiem/Reports/t07-blender.json
```

El informe enumera mallas, armatures/huesos, pesos, modificadores, acciones, materiales, imágenes, escala, bounds, triángulos, bordes abiertos e islas. No sustituye una revisión visual de deformaciones o dirección del frente.

Preparar una copia exportada, seleccionando nombres reales del informe:

```powershell
& $blender --background --factory-startup --python-exit-code 1 --python $tool -- prepare-copy --input $model --objects Mesh_0 --output Tools/IronRequiem/Downloads/t07-working-copy.fbx --report Tools/IronRequiem/Reports/t07-working-copy.json
```

Opciones para esa copia:

- `--separate-loose`: separar islas conectadas; no selecciona semánticamente un arma fusionada.
- `--pivots-json <archivo>`: mapa de nombres exactos a coordenadas del pivote en espacio mundial de Blender, por ejemplo `{"Mesh_0": [0, 0, 0]}`. Solo tras inspeccionar y decidir los pivotes.
- `--frames-json <archivo>`: mapa de nombres exactos a `{"origin": [x,y,z], "forward": [x,y,z], "up": [x,y,z]}` en espacio mundial de Blender. Hornea ese marco en la malla: origen a (0,0,0), `forward` a -Y (Unity +Z) y `up` a +Z (Unity +Y); el objeto queda en el origen sin rotación ni escala. Solo mallas sin padre ni datos compartidos.
- `--triangulate`: triangula las mallas seleccionadas antes de exportar, para que Unity no retriangule n-gonos cóncavos como las tapas de corte.
- `--empties-json <archivo>`: crea vacíos nuevos (por ejemplo `Attachment` y `Muzzle`) como `{"Nombre": {"parent": "<objeto>", "location": [x,y,z]}}` en espacio local del padre, después de aplicar marcos y pivotes, y los exporta.
- `--export-animation`: incluir animaciones existentes en la copia. No crea clips ni rigging. Seleccionar también el armature y jerarquía necesarios si el modelo los tiene.

La exportación usa FBX con ejes -Z/Y, unidades y sin leaf bones añadidos. Se probó exportación y reimportación del T-07 en una carpeta temporal ignorada: mismo número de triángulos, dimensiones y hash del original. Reimportar y verificar la copia en Unity antes de adoptarla como nuevo asset.
