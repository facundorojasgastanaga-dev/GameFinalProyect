# Entrada para Tripo3D — T-07 Mech (prueba de retarget, 17/11/2026)

- **Origen:** `Assets/IronRequiem/Art/Enemies/T07Mech/T07Mech_Rigged.fbx` (export original de Tripo con rig, F8). No se modificó: SHA-256 `57dbb0aa65f6f55f89ea10c6cc3cdfe80ce3ef90f30b1e80af3b89d7fc819aea` antes y después.
- **Herramienta:** `Tools/IronRequiem/Blender/export_tripo_input.py`, Blender 5.2.3 LTS en segundo plano con `--factory-startup`.
- **Archivo:** `T07Mech_TripoInput.glb`, 3.052.752 bytes, SHA-256 `3b4344ab3552a2a0fcd14abb64532ff2a1bbbeddb679e90553924d67c1132e7e`.

## Qué se hizo a la copia

1. Armature en pose de reposo (bind pose). Se quitaron los modificadores Armature, los grupos de vértices, el armature y los vacíos. La geometría queda tal cual estaba en la bind pose.
2. Se unieron las 99 piezas `tripo_part_*` en una sola malla, conservando los 99 materiales, sus texturas y el UVMap.
3. Se giró sobre el eje vertical 33,5° para que mire a -Y en Blender (+Z en glTF/Unity). El frente se midió con los huesos de hombros y caderas y se comprobó con los pies (coseno 0,957). Pies en z = 0, centrado en XY. **Sin cambio de escala:** mide 0,51 × 0,22 × 1,00 m.
4. Exportado como GLB con texturas, sin skin ni animaciones.

Métricas: 5.114 vértices y 9.227 triángulos, igual que la fuente. Hay vistas frontal, lateral y trasera en `../reports/input_*.png`.

## Armas y uniones

El FBX no contiene ningún arma ni unión separable. El cañón es el antebrazo y la mano derecha de la malla (huesos `Right_LowerArm` y `Right_Hand`), así que sube con el cuerpo para no romper la silueta bípeda. `CannonModule`, `CannonMount`, `CannonJoint`, `Muzzle` y el punto naranja `JointIndicator` son objetos del prefab. Los acopla `T07CannonSocket` al hueso del antebrazo derecho, y se vuelven a acoplar igual en la variante de prueba.
