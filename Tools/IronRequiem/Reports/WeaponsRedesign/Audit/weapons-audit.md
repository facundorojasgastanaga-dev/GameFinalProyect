# Auditoría de armas — IRON REQUIEM

16/11/2026. Trabajo **solo de lectura**: no se modificó ningún asset, prefab, escena, script ni `.meta`. Blender 5.2.3 LTS siempre con `--background --factory-startup --python-exit-code 1` (la sesión GUI del coordinador no se tocó). Los cuatro FBX conservan su hash SHA-256 antes y después de cada inspección (`source_unchanged: true` en `probe-*.json`).

Datos legibles por máquina: `weapons-audit.json`. Evidencia: `inspect-*.json` (blender_asset_tool), `probe-*.json` (medición y emisivos), `guid-closure-raw.json` / `guid-closure-tree.txt` / `transitive-consumers.json` (consumidores por GUID), `prefab-dumps/*.txt`, `renders/*.png`. Scripts propios en `scripts/`.

Convención de ejes de estos FBX (bakeAxisConversion 0, exportados con `axis_forward=-Z, axis_up=Y`): **Unity = (−Xb, Zb, −Yb)**. Disparo por Blender −Y = Unity +Z.

## 1. Inventario por arma

| | PistolModule_1000 | IR_PrimaryModule_v02 | T07_CannonClean | T07_Cannon_PivotFixed |
|---|---|---|---|---|
| GUID | `60c06924…ca5f` | `899ab1d0…bc3e` | `48ac3df5…45d0` | `3167e5df…bf4` (sin seguimiento git) |
| Tris LOD0/1/2 | 532 / – / – (1.000 vért.) | 696 / – / – (384 vért.) | 1.702 / 906 / 412 (Unity importa 1.694/807/401) | 1.702 / 906 / 412 (triangulado) |
| Materiales FBX | 1: PistolModule_Atlas | 1: PistolModule_Atlas | 2: OriginalCannonSurface (1.494), CutInterior (208) | idénticos al Clean |
| Texturas | BaseColor + Emission 768² | **las de la pistola** (ruta a BasicPistolModule) | ninguna (asigna Unity) | ninguna |
| Material Unity | PistolModule_URP (Lit) | PrimaryModule_URP (Lit) | T07_URP_FPS + ExposedMechanism_FPS | T07_URP_FPS + ExposedMechanism_FPS |
| Emisivos | ranuras cian laterales x=±0,058, z 0,17–0,23 m; **`_EMISSION` ausente en el .mat** → probablemente apagado | ranuras cian x=±0,054, z 0,125–0,19 m; `_EMISSION` activo | ninguno; ExposedMechanism tiene color naranja pero sin keyword | igual |
| Tamaño Unity (X×Y×Z) | 0,117 × 0,138 × 0,379 m | 0,109 × 0,113 × 0,338 m | 0,159 × 0,605 × 0,428 m (en marco del robot) | 0,314 × 0,195 × 0,671 m |
| Pivote | centro del acople trasero | acople trasero | origen del robot (−0,300, −0,027, 1,092) B; 7,7 cm fuera del eje | acople trasero |
| Eje de disparo | +Z | +Z | inclinado ~31°; normal boca B (−0,074, −0,513, −0,855) | +Z |
| Sockets FBX | Attachment (0,0,0), Muzzle (0,0,0,379) | Attachment (0,0,0), Muzzle (0,0,0,333) | ninguno | Attachment (0,0,0), Muzzle (0,0,0,6644) bajo LOD0 |
| Sockets prefab | Visual: igual; Weapon_IR_Pistol: Attachment (0, 0,124, 0,077), Muzzle (0, 0,124, 0,456) | Attachment (0, 0,094, 0,077), GunMuzzle (0, 0,094, 0,41) | Weapon_T07_Temporary_Optimized: Attachment (0,0584, 0,0558, −0,1742); muzzle base (0,015, 0,065, 0,48) | WeaponAttachment (0,0,0), WeaponMuzzle (0,0,0,66442) en la raíz |
| Geometría tras el acople | 0 mm | **4,5 mm** | n/a | 3,2 mm |
| Topología | 1.000 bordes abiertos: cada cara es una isla | cerrada, 20 islas | cerrada, 1 isla | cerrada, 1 isla |
| Import (.meta) | globalScale 1, useFileScale 1, useFileUnits 1, bakeAxisConversion 0, materialImportMode 2, Generic, importAnimation 1 | igual con materialImportMode 0, sin animación | igual, materialImportMode 0, Generic, LOD 0,25/0,125/0,01 | igual, sin animación, LOD 0,25/0,125/0,01; LODGroup del prefab 0,18/0,06/0,003 |
| Fuente editable | `create_pistol_module.py` + `Working/BasicPistolModule/PistolModule_1000.blend` | `primary_module_v02.py` + `.blend` (FBX de Working idéntico) | derivado del T-07 | `Tools/IronRequiem/Exports/T07_Cannon_PivotFixed/` (copia idéntica) |

En Unity el nodo raíz de los FBX conserva el habitual −90° X y escala 100 (CombatPolish desplaza el Attachment 0,00039 en unidades del FBX = 3,9 cm). Los prefabs compensan con +90° X en los hijos Attachment/Muzzle o con sockets propios en la raíz.

Renders (Workbench, verde = Attachment, rojo = Muzzle, amarillo = eje): `renders/<arma>_{front,side,three_quarter,top}.png`. El Clean se renderiza en su marco original inclinado.

## 2. Consumidores

### Directos por GUID del FBX (`git grep` sobre 269 prefabs, escenas y assets)

| FBX | Consumidor directo | Relación |
|---|---|---|
| PistolModule_1000 | `Prefabs/Weapons/Visuals/PistolModule_1000_Visual.prefab` | anidado |
| IR_PrimaryModule_v02 | `Prefabs/Weapons/Weapon_IR_PrimaryModule_v02.prefab` | anidado (PrimaryModuleVisual) |
| T07_CannonClean | `Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab` | solo `m_Mesh` del renderer, **desactivado** |
| T07_Cannon_PivotFixed | `Prefabs/Weapons/Visuals/T07_Cannon_PivotFixed_Visual.prefab` | variante; **sin otros consumidores** |

Transitivos (variantes, anidados y referencias de campo; potenciales, porque algunos campos se sobrescriben en variantes): pistola 16 prefabs / 43 escenas (incluye MainScene vía `Weapon_IR_Pistol_Thermal`); principal 8 / 30; Clean 42 / 47 (todos los T-07 y Big Bot, incluidas las salas de campaña Room01–04 vía `T07_Mech`); PivotFixed 1 / 0. Árbol completo en `guid-closure-tree.txt`.

### Cadena de prefabs relevante

- **Pistola**: Visual → `Weapon_IR_Pistol` (variante de Weapon_Blaster, `WeaponMuzzle` = Muzzle del FBX) → `_Thermal` (inicial de MainScene) y `_CombatPolish` (renderers del módulo apagados, Attachment movido a z 0,038, RoboticWristCoupler) → `Weapon_IR_PrimaryModule_v02` (pistola desactivada, sockets renombrados `LegacyAttachment_1000`/`LegacyMuzzle_1000`) → `_PrimaryCapsules_v03` → `_Integrated_v04`.
- **Principal**: `Weapon_IR_PrimaryModule_v02` → `_PrimaryCapsules_v03` (cápsulas colocadas respecto al Attachment al crear el prefab) → `_Integrated_v04`; jugadores `Player_PrimaryModule_v02`…`_Integrated_v04`.
- **T-07**: `Weapon_T07_Temporary_Optimized` (variante de Weapon_T07_Temporary) es el `weaponPrefab` de `StolenCannon` en `T07_Module` y lo referencian T07_Common_Optimized, T07_Mech, T07_Mech_Test, T07_Common_Animated_Working; `Weapon_BigBot_Temporary` es variante suya y de ella cuelgan todas las armas robadas del Big Bot.
- **Jugador actual de campaña**: `Player_ModularArm_v07` con `Weapon_IR_ArmCannon_VO` (pistolPrefab) y `Weapon_Shotgun_ArmModule_VO` (montada). Esas armas **no** usan ninguno de los cuatro FBX; descienden de `Weapon_IR_NativeVisual_v06` (variante de Weapon_Blaster). En campaña, de los cuatro FBX solo llega el Clean (y desactivado).

### Scripts C# (archivo:línea)

Runtime:

| Archivo:línea | Qué hace | Cómo calcula el acople |
|---|---|---|
| `Scripts/Player/ProtagonistHandsPresentation.cs:204` | Representación: StolenCannon / Pistol (SourcePrefab == pistolPrefab) / Mounted / Normal | — |
| `…ProtagonistHandsPresentation.cs:234` | Busca el primer Transform llamado `Attachment` bajo WeaponRoot | por nombre |
| `…:246` | Sin Attachment: muñeca = muzzle + rot·offset fijo (pistol/cannon/mounted, líneas 25-33) | offset fijo desde la boca |
| `…:256` | `rightWrist = Attachment.position + muzzle.forward*0.003` | **Attachment + 3 mm** |
| `…:259` | Antebrazo modular: mano avanza `rightLength − 0.14` | eje = WeaponMuzzle.forward |
| `…:298-300` | Codo = muñeca − muzzle.forward; FitArm alinea el antebrazo con ese eje | eje de la boca |
| `…:323` | `RightModuleSocket` = (puerto, rotación de WeaponMuzzle) | puerto |
| `Scripts/VisualPass/StolenShellFit.cs:60,74` | Asienta VP_StolenShell sobre RightModuleSocket, solape 0,02 m, animación de absorción | **bounds** (centro x/y, min z de la AABB) |
| `Scripts/VisualPass/StolenChargeAnchor.cs:49` | Lleva WeaponMuzzle a la boca de la carcasa (+0,015), retroceso y pulsos de carga | **bounds** (max z) |
| `Scripts/VisualPass/EnemyMuzzleAnchor.cs:60` | Boca del cañón enemigo: eje más largo de la AABB, extremo lejano al padre | **bounds** (módulo del enemigo, mallas T07_LOD*, no CannonClean) |
| `Scripts/Weapons/CannonChargePresentation.cs:8` | Carga magenta; cuelga de la boca (ChargeEnergy bajo WeaponMuzzle) | hijo de WeaponMuzzle |
| `Scripts/Weapons/StolenCannon.cs:60` | Recogida/desprendimiento/lanzamiento del módulo de mundo; collider desde bounds (mín. 6 cm mundo, compensa escala 100) | bounds |
| `Scripts/Player/StolenWeaponCarrier.cs:38,74,176` | E recoge (raycast), G lanza desde WeaponMuzzle | WeaponMuzzle |
| `Scripts/Levels/WeaponMountStation.cs:232,302-319,401-419` | Montaje en estación: proxy del arma, `Attachment` del proxy a `MountedHandLead` de la mano; agarre por bounds | Attachment + bounds; arma de campaña = ArmCannon_VO |
| `Scripts/VisualOverhaul/ArmModuleVisual.cs:87` | Animación de cambio: Attachment, muzzle y vapor no se mueven | por nombre |

Editor / pruebas (se rompen o reconstruyen al cambiar geometría o nombres):

- `Editor/PistolModuleAssetBuilder.cs:50-80` (crea el Visual alineando Attachment→origen, Muzzle→+Z; rechaza sobrescribir).
- `Editor/WeaponFeedbackUpgrade.cs:58-66` (coloca el Visual por **Muzzle contra GunMuzzle** heredado; `WeaponMuzzle` = Muzzle del FBX).
- `Editor/CombatPolishAuthoring.cs:144,147` (apaga renderers; Attachment fijo a (0, 0,124, 0,038)).
- `Editor/PrimaryModuleAuthoring.cs:28,34-37` (importa FBX, renombra sockets de la pistola, GunMuzzle (0, 0,094, 0,41), coloca por Muzzle).
- `Editor/PrimaryCapsulesAuthoring.cs:24-25` (cápsulas respecto al Attachment).
- `Editor/ModularArmAuthoring.cs:145,216,250,300` (Attachment = (0, muzzle.y, bounds.min.z + 0,002), crea `RightArm_ModuleSocket`).
- `Editor/NativeWeaponRepairTools.cs:170` (Attachment z fijo −0,047), `Editor/ArmModuleFitTools.cs:28,170`.
- `Editor/WeaponPivotAuthoring.cs:23-29,96,153,190` (regenera el PivotFixed y su Visual; sockets `WeaponAttachment`/`WeaponMuzzle`).
- `Editor/CannonBurrRepairTools.cs:11-12,46` (lee `CannonClean_LOD0` por nombre), `Editor/T07OptimizationBuilder.cs:21,91`, `Editor/VisualPass/VisualPassF5.cs:387-470` (crea VP_StolenShell y configura StolenShellFit alineando Attachment).
- Pruebas: `Tests/EditMode/MechanicsAssetTests.cs:47-50`, `Tests/PlayMode/SlidePistolMainTests.cs:53,73` (nombre `PistolModule_1000_Equipped`, muzzle llamado `Muzzle`), `Tests/PlayMode/ModularArmTests.cs:72` (un único Attachment), `Tests/PlayMode/CannonBurrSmokeRelay.cs:41` (malla `CannonClean_LOD0`), `Tests/EditMode/WeaponPivotAssetTests.cs:10-25`, `Tests/PlayMode/PrimaryModuleTests.cs:45-48`, `CombatPolishTests.cs:73-76`, `ProtagonistActionsTests.cs:55`, `NativeWeaponVisualTests.cs:98`, `BigBotBurstTests.cs:58`.

## 3. Contrato de acople actual (antebrazo ↔ arma)

1. **El arma manda.** PlayerWeaponsManager coloca el prefab del arma en `WeaponParentSocket` (posición por defecto/ADS, bob y retroceso). `ProtagonistHandsPresentation` ajusta después el antebrazo al arma en LateUpdate.
2. **Interfaz del arma**: un Transform llamado exactamente `Attachment` bajo `WeaponRoot` (se toma el primero; las pruebas exigen uno solo) y `WeaponController.WeaponMuzzle`, cuyo `forward` es el eje de disparo **y** el eje del antebrazo. La rotación del Attachment no se usa.
3. **Puerto**: `Attachment.position + WeaponMuzzle.forward × 0,003` — el antebrazo penetra **3 mm** en el arma (línea 256). `MountedHandLead = (rightLength − 0,14) + 0,003`.
4. **Transforms del jugador**: `rightModuleSocket` (propiedad `RightModuleSocket`) = `ModuleSocket_R` en `Player_IR_Protagonist_v01`; en `Player_ModularArm_v07` existen además `RightArm_ModuleSocket` (cuerpo, junto al codo), `Forearm_R_Receiver` y `RightArm_UniversalCoupling`. En cada frame con módulo se escribe (puerto, rotación de WeaponMuzzle). El antebrazo modular conserva 0,14 m junto al codo; collar de muñeca r 0,046 / largo 0,032 m en el puerto.
5. **Sin Attachment** se usan offsets fijos desde la boca: pistol (0, −0,015, −0,23), cannon (0,015, −0,04, −0,38), mounted (0, −0,03, −0,42).
6. **Armas robadas**: la carcasa visible (`VP_StolenShell`) se asienta por AABB sobre RightModuleSocket con 20 mm de solape. La boca se recoloca en el frente de la AABB + 15 mm.
7. **Posición en WeaponRoot**: hoy el Attachment cae en z = 0,077 en pistola y principal porque **ambas visuales se colocaron haciendo coincidir el Muzzle del FBX con un GunMuzzle heredado** (0,456 y 0,41). La altura cambia (y 0,124 frente a 0,094).

### Qué haría falta para que un arma nueva entre «sin ajustes manuales»

- FBX con origen en el centro de la cara trasera de acople, disparo por Blender −Y / arriba +Z, escala 1 m, `axis_forward=-Z, axis_up=Y`. Vacíos `Attachment` (0,0,0) y `Muzzle` (0,−L,0), como ya hacen la pistola, el principal y el PivotFixed.
- Importación igual a la actual: useFileScale 1, globalScale 1, **bakeAxisConversion 0** (activarlo invirtió el cañón), sin materiales importados. Prefab visual con raíz identidad y sockets con rotación identidad (+Z/+Y).
- **Un solo nombre de socket.** Hoy conviven `Attachment`/`Muzzle` (pistola, principal, receptor) y `WeaponAttachment`/`WeaponMuzzle` (PivotFixed). El receptor solo reconoce `Attachment`.
- Sin geometría detrás de z = 0 salvo el solape reservado (3 mm del puerto, 20 mm en robadas), y la sección trasera centrada en x = y = 0 y compatible con el collar de 46 mm.
- Muzzle en el extremo +Z y AABB alineada con +Z, porque StolenShellFit y StolenChargeAnchor miden por AABB.
- Una regla de colocación por **Attachment** y no por Muzzle: posición canónica del Attachment en WeaponRoot, p. ej. (0, y, 0,077), y Muzzle en z = 0,077 + L. Hoy cada autoría la resuelve a mano.
- Al reemplazar un FBX existente, conservar nombres de nodos y de malla (`PistolModule_1000`, `Attachment`, `Muzzle`, `CannonClean_LOD0`…). Unity deriva de ellos los fileID que usan los overrides.

## 4. Riesgos de cambiar la geometría

**PistolModule_1000 — alto.**
- `WeaponMuzzle` de Weapon_IR_Pistol apunta al nodo FBX `Muzzle`: moverlo cambia el origen del disparo en toda la cadena, incluida la pistola térmica de MainScene. Renombrarlo deja la referencia nula.
- Los overrides de CombatPolish (Attachment a z 0,038) y de PrimaryModule_v02 (renombrar a `LegacyAttachment_1000`) apuntan a fileID del FBX. Si cambia la jerarquía o un nombre quedan huérfanos y reaparece un segundo `Attachment` en v02/v03/v04. Eso rompe ModularArmTests:72 y puede asentar la mano en el socket equivocado.
- Pruebas por nombre: MechanicsAssetTests, SlidePistolMainTests.
- El atlas es compartido con el principal, así que cambiar las texturas cambia las dos armas.
- La malla de caras sueltas no admite bisel ni normales suavizadas sin rehacerla desde el .blend.
- Emisión probablemente apagada por falta de `_EMISSION`: confirmar en el Editor antes de evaluar el arte.

**IR_PrimaryModule_v02 — medio-alto.**
- La visual se cuadró por Muzzle: alargarla o acortarla desplaza el Attachment y con él el antebrazo, salvo que se recalcule el GunMuzzle.
- Las cápsulas de v03 y v04 son objetos colocados una vez respecto al Attachment, sin reajuste en runtime: quedarían desalineadas.
- La malla ya sobresale 4,5 mm por detrás del acople y choca con el solape de 3 mm.
- Las texturas son las de la pistola.
- Los jugadores v02–v04 la usan como pistolPrefab o inicial. v06/v07 lo sobrescriben, así que no afecta a campaña.

**T07_CannonClean — medio (bajo visual, alto de herramientas).**
- En runtime el renderer está desactivado y lo visible es VP_StolenShell, así que un cambio de malla apenas se vería.
- Pero el nodo tiene escala 100, posición y rotación compuestas ajustadas a mano al origen del robot. Cualquier cambio de pivote invalida esa alineación oculta y la de `Weapon_BigBot_Temporary` (variante).
- CannonBurrRepairTools, WeaponPivotAuthoring y CannonBurrSmokeRelay buscan `CannonClean_LOD0` por nombre.
- Lo heredan 42 prefabs y 47 escenas (todos los T-07 y Big Bot de campaña).
- Es fuente del PivotFixed: si se cambia, el PivotFixed deja de ser equivalente.

**T07_Cannon_PivotFixed — bajo.**
- Sin consumidores fuera de su Visual, la herramienta y la prueba.
- `WeaponPivotAuthoring` lo copia desde `Tools/IronRequiem/Exports/…`: editar la copia de Assets se pierde al reconstruir, así que hay que editar la exportación.
- Sockets `WeaponAttachment`/`WeaponMuzzle` incompatibles por nombre con el receptor. Si se monta en un arma, el receptor encontraría el vacío FBX `Attachment` bajo LOD0 (misma posición, rotación −90° X). Funciona por coincidencia y puede duplicarse con un Attachment del arma.
- Los LOD1/LOD2 desvían la boca 1–4 cm.
- No está bajo control de versiones (`??`).

## 5. Relación con los conceptos

- `Docs/IronRequiem/Concepts/Weapons/pistol-module-v0{1,2}*.prompt.txt` (y sus PNG) son el origen de la pistola actual: bloque recto, anillo de acople trasero en línea, pestaña naranja y tira cian.
- `Concepts/Weapons/Redesign_v01/README.md` (fase B1) ya adopta la convención de este contrato: origen en el acople, −Y Blender / +Z Unity, largos ±2 % de 0,379 / 0,333 / 0,664 m, emisivos de calor naranja-rojo y carga magenta en el T-07.
- Su collar de **Ø 0,09 m** casi coincide con el collar del receptor (r 0,046 m = Ø 0,092 m). Conviene fijar ese diámetro como parte del contrato.

## 6. Límites

- Sin Editor de Unity: los recuentos de Unity, el estado de `_EMISSION` en tiempo real y la jerarquía importada salen de los `.meta` y del YAML de los prefabs, no de una carga en el Editor. Las cifras de Unity del Clean y del PivotFixed proceden de WEAPON_PIVOT_T07_CANNON.md.
- Los emisivos se detectaron muestreando la textura de emisión en el centroide UV de cada cara (umbral 0,2). Es aproximado.
- Las relaciones transitivas son referencias potenciales. Las variantes pueden sobrescribir campos, como StartingWeapons o pistolPrefab en v06/v07.
- `IRON_REQUIEM_GDD_v0.1.md` no se consultó (no forma parte del encargo).
