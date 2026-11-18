# Rediseño de armas v01 (B1 y B2)

16–17/11/2026. Blender 5.2 y Unity 6000.5.5f1. Skills: `iron-requiem-modular-weapons` e `iron-requiem-performance`. Todo se hizo con scripts reproducibles, sin Meshy, generación con créditos ni compras.

## B1: conceptos

`Docs/IronRequiem/Concepts/Weapons/Redesign_v01/` contiene tres láminas (3/4, perfil y vista FPS), una por arma, con tres direcciones cada una, y `README.md` con pros y contras. El usuario eligió **P-A «Monolito»** (pistola), **PR-C «Espina radiadora»** (principal) y **C-A «Arrancado crudo»** (cañón del T-07).

## B2: modelado (Blender)

Scripts: `Tools/IronRequiem/Blender/weapons_redesign_v01_lib.py` y `weapons_redesign_v01_{pistol,primary,t07cannon}.py`. Exportaciones en `Tools/IronRequiem/Exports/{Pistol,Primary,T07Cannon}_Redesign_v01/`: FBX con tres LOD, BaseColor, MetalSmooth (R metálico, A suavidad) y Emission a 1024², renders, `metrics.json` y `README.md`.

| Arma | Tris LOD0 / LOD1 / LOD2 | Presupuesto | Acople → boca | Slots de material | Texel |
|---|---|---|---|---|---|
| PistolMonolito | 1.256 / 628 / 314 | 1.300 | 0,379 m | Surface y Heat_Emissive | 1.350 px/m |
| PrimaryEspina | 1.460 / 730 / 364 | 1.500 | 0,333 m | Surface y Heat_Emissive | 1.350 px/m |
| T07CannonCrudo | 2.420 / 1.210 / 604 | 2.600 | 0,664 m | Surface, Heat_Emissive y Charge_Emissive | 737 px/m |

Las tres cumplen IR-ACOPLE-01, verificado al reimportar en Blender:
- Origen en `Attachment` y disparo por −Y en Blender (+Z en Unity).
- Alojamiento de Ø 0,066 y 8 mm de profundidad, collar de Ø 0,089–0,091 y 32 mm, y nada por detrás del origen.
- Vacíos `Attachment` y `Muzzle` únicos.
- Malla triangulada (Unity retriangula mal los n-gons cóncavos), Weighted Normal y 0 caras duplicadas.

## Unity

`Assets/IronRequiem/Editor/WeaponsRedesignAuthoring.cs` se ejecuta desde el menú `Tools > Iron Requiem > Weapons > Redesign v01` o con la cola `Tools/IronRequiem/Requests/weapons-redesign.txt` (`build`, `lab`, `test`, `verify`).

**Arte** en `Assets/IronRequiem/Art/Weapons/Redesign_v01/<Arma>/`:
- FBX importado con la convención del estándar: `useFileScale`, sin bake de ejes y sin materiales.
- Materiales URP/Lit `_Surface`, `_Heat` y `_Charge`, con mapas y `_EMISSION` activo.

**Visuales:** `Prefabs/Weapons/Visuals/<Arma>_Visual.prefab` es una variante del modelo con LODGroup (pistola y principal 0,25 / 0,1 / 0,01; cañón 0,18 / 0,06 / 0,003).

**Variantes de arma** en `Prefabs/Weapons/Redesign_v01/`. Los prefabs actuales quedan como respaldo, sin cambios.

| Variante | Base (arma de campaña) | Montaje |
|---|---|---|
| `Weapon_IR_ArmCannon_Redesign_v01` | `Weapon_IR_ArmCannon_VO` (pistolPrefab) | Visual dentro de `VO_Module`, que es el moduleRoot de ArmModuleVisual y se mueve en el cambio de arma. Su `ModuleAttachment` se coloca sobre el `Attachment` estático; `WeaponMuzzle` va a la boca nueva. OverheatBehavior tiñe `_Heat` |
| `Weapon_Shotgun_ArmModule_Redesign_v01` | `Weapon_Shotgun_ArmModule_VO` (montada y slot 2) | Igual. Esta arma no tiene OverheatBehavior |
| `Weapon_T07_Redesign_v01` | `Weapon_T07_Temporary_Optimized` (cañón robado) | Visual dentro de `VP_StolenShell`. `StolenShellFit.rearSocket` = `ModuleAttachment` con 3 mm de solape (contrato), y `StolenChargeAnchor.mouthSocket` = `ModuleMuzzle` |

En todas las variantes, los renderers del visual anterior quedan desactivados (los scripts, sockets, vapor y audio se conservan). Los sockets del FBX se renombran a `ModuleAttachment` y `ModuleMuzzle`, de modo que cada arma conserva **un único `Attachment`**: el estático que lee el receptor del antebrazo. Así se evita un bucle entre el receptor y el asiento de la carcasa.

**Código tocado** (respaldo en `Tools/IronRequiem/Backups/20261116-protagonist-v02/scripts/`): `StolenShellFit.cs` y `StolenChargeAnchor.cs` reciben un socket **opcional** (`rearSocket`/`socketOverlap` y `mouthSocket`). Con el campo vacío, el comportamiento por caja envolvente es idéntico al anterior. Con él, el asiento usa el `Attachment` real, porque los cables de C-A desplazaban la caja 7 cm.

**Laboratorio:** `Assets/IronRequiem/Scenes/WeaponsRedesignLab.unity` es una copia de `MainScene_VisualLab` con 6 referencias reasignadas a las variantes nuevas y una `MountStation_C04` delante del jugador. Las escenas originales no se tocan.

## Verificación

- **Blender:** reimportación con las 12 comprobaciones del estándar por arma.
- **Unity, importación y compilación:** `Tools/IronRequiem/Reports/WeaponsRedesign/Unity/verify.json`. Un solo `Attachment` por arma, error de acople y de boca de 0 mm, eje alineado 0°, escala visual 1, triángulos iguales a Blender, renderers antiguos apagados y materiales con sus tres mapas y emisión.
- **PlayMode** (`Assets/IronRequiem/Tests/PlayMode/WeaponsRedesignTests.cs`, entrada virtual, informe `play.xml`): **2/2 aprobadas**.
  - Pistola y principal: puerto a 1 mm del socket, visibles para la cámara de armas, disparan (8 y 1 disparos) y cambian de arma con la tecla.
  - Cañón T-07: desprendimiento real, recogida con E, asiento por `Attachment` con **0,00 mm** de error, boca sobre el ánima y un disparo cargado.
  - Capturas en `Tools/IronRequiem/Reports/WeaponsRedesign/Play/`.

## Pendientes

- Prueba manual en Play de la estación de montaje. En ambos laboratorios, `MountStation_C04` entrega ahora `Weapon_IR_ArmCannon_Redesign_v01`, pero la prueba automática no recorre el intercambio mano → arma.
- **PR-C** heredó la posición en pantalla del módulo VO y queda baja, en la esquina inferior derecha. Ajustar el encuadre sería cambiar el viewmodel o la cámara, y se pide autorización antes de hacerlo.
- **Calor por aleta** de PR-C: el color de vértice `HeatStep` existe, pero URP/Lit lo ignora. Encenderlas una a una requiere un Shader Graph o un componente nuevo, que no se hicieron.
- **Cañón:** el texel es de 737 px/m, por debajo del objetivo (hay un modo de 2048 disponible). El `StolenCannon` del mundo sigue usando las mallas del T-07 y su collider de recogida no se ha rediseñado.
- **Integración en campaña:** no se ha hecho. Requiere aprobar las variantes y cambiar las referencias en `RequiemCampaign`.

## Actualización (17/11/2026)

- **Ventana de carga del cañón:** `Assets/IronRequiem/Scripts/Weapons/ChargeWindowGlow.cs`, nuevo, está en `Weapon_T07_Redesign_v01`. El slot `Charge_Emissive` de las tres LOD sigue `ChargeFraction` (curva cuadrática), pulsa por encima del 85 % de carga y destella al disparar. Usa MaterialPropertyBlock por submesh, sin instanciar materiales. La prueba del cañón exige ahora una emisión mayor que 1 durante la carga y está **aprobada**.
- **Laboratorios:** la estación se coloca antes de la reasignación de referencias, así que en `WeaponsRedesignLab` y `ProtagonistV02Lab` entrega el arma rediseñada.
- **PlayMode 3/3** a las 02:25. `verify.json` confirma tres renderers con ventana de carga y la emisión activa.
