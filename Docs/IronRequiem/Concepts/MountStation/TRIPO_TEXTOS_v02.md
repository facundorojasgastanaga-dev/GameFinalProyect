# Estación de montaje C-04 v02: textos para Tripo

19/11/2026. El usuario genera los modelos en Tripo con su cuenta y entrega los FBX. Aquí no se usó Composio, no se gastaron créditos ni se hicieron compras.

## Por qué va por piezas

La estación actual (`Prefabs/Levels/MountStation_C04.prefab`, construida con cubos por `Editor/HangarIntro/CombatV4Authoring.cs`) tiene dos brazos que se mueven por código: `StationArmRig` gira `Yaw`, `Upper`, `Fore` y `Gripper`, y abre y cierra `JawA` y `JawB`. Tripo entrega una malla fusionada; con un solo modelo, los brazos no se podrían mover. Por eso cada pieza que se mueve se genera aparte. Los dos brazos usan las mismas mallas.

| ID | Pieza | Se mueve | Hueso / nodo en Unity | Medida actual (referencia) |
|---|---|---|---|---|
| P1 | Pórtico (marco fijo) | No | raíz | 5,0 × 3,9 m. **Objetivo v02: ≤ 3,8 m de ancho** (en Map01 S1 hay un hueco de 4 m y hoy el marco entra en el muro) |
| P2 | Torreta de hombro | Gira en vertical | `Yaw` | 0,32 × 0,22 × 0,32 m |
| P3 | Brazo superior | Bisagra del hombro | `Upper` | 1,3 m entre ejes (`upperLength`) |
| P4 | Antebrazo | Bisagra del codo | `Fore` | 1,2 m entre ejes (`foreLength`) |
| P5 | Pinza (palma y 2 mordazas) | Orientación libre; las mordazas se deslizan | `Gripper`, `JawA`, `JawB` | Palma de 0,12 m; carrera de mordaza de 0,07 a 0,025 m desde el centro |
| P6 | Anillo de bloqueo de muñeca | Gira 60° | `WristLocks` | Rodea el collar de la mano (Ø 0,080 m, IR-ACOPLE-01): **hueco interior ≥ Ø 0,10 m** |
| P7 | Soporte del cañón (opcional) | No | `CannonRack` | Repisa de 0,7 × 0,5 m |

## Ajustes en Tripo

- **Text to 3D**, con la versión de modelo más reciente.
- **Textura y PBR activados.**
- **Low poly / Smart Low Poly**, si aparece, con límite de caras: P1 8.000; P2 a P5 2.000 cada una; P6 y P7 1.500. Si no aparece, genera normal: se reduce después en Blender.
- Si Tripo admite **prompt negativo**, usa siempre el mismo (abajo).
- Genera 2–4 variantes por pieza y quédate con la que cumpla:
  - silueta limpia;
  - bisagras redondas en los extremos (P3 y P4);
  - agujero central abierto (P6);
  - sin piezas sueltas ni bultos derretidos.
- **Exporta en FBX**. Si puede, descarga también el GLB, que lleva las texturas dentro.

## Dónde dejar los archivos

`Tools/IronRequiem/Generated/Tripo_MountStation_20261119/raw/`, con estos nombres: `P1_Portico.fbx`, `P2_Hombro.fbx`, `P3_BrazoSuperior.fbx`, `P4_Antebrazo.fbx`, `P5_Pinza.fbx`, `P6_AnilloBloqueo.fbx`, `P7_Soporte.fbx` (más los `.glb` o texturas si las hay).

No los pongas dentro de `Assets/`. La integración los limpia primero en Blender.

## Textos (pegar tal cual; en inglés porque Tripo responde mejor)

Bloque de estilo, ya incluido al final de cada texto: *Game-ready hard-surface prop, worn military bunker machinery. Dark gunmetal steel and rusted iron, bolted armor plates, chipped yellow-black hazard stripes, grime and oil stains, small stencil markings. Heavy, mechanical, clean readable silhouette, beveled edges. Single isolated object, centered, no floor, no background.*

**Prompt negativo:**
```
person, character, robot body, legs, floor, ground plane, background, text, letters, logo, cartoon, toy, plastic, organic shapes, melted, blobby, floating pieces, thin wires
```

**P1 Pórtico**
```
Heavy industrial gantry frame for a robotic weapon-mounting station inside an abandoned military bunker. Two thick vertical steel pillars joined at the top by a massive horizontal crossbeam; overall about as wide as it is tall, shallow depth. Under the crossbeam, a recessed ceiling rail where two robot arms will hang (arms NOT included). Amber status light strip across the front of the crossbeam, a blank metal sign plate on top, hydraulic hoses and cable bundles running down the pillars, hazard stripes on the inner pillar edges, bolted feet. Open empty passage between the pillars. Game-ready hard-surface prop, worn military bunker machinery. Dark gunmetal steel and rusted iron, bolted armor plates, chipped yellow-black hazard stripes, grime and oil stains, small stencil markings. Heavy, mechanical, clean readable silhouette, beveled edges. Single isolated object, centered, no floor, no background.
```

**P2 Torreta de hombro**
```
Compact rotating shoulder turret of an industrial robot arm, hanging upside down from a ceiling rail: a short cylindrical slewing ring on top, a boxy motor housing below it, and at the bottom a hinge clevis (two side plates with a thick horizontal pin) where the upper arm attaches. Arm NOT included. Cables entering the housing. Game-ready hard-surface prop, worn military bunker machinery. Dark gunmetal steel and rusted iron, bolted armor plates, chipped yellow-black hazard stripes, grime and oil stains, small stencil markings. Heavy, mechanical, clean readable silhouette, beveled edges. Single isolated object, centered, no floor, no background.
```

**P3 Brazo superior**
```
Straight upper arm segment of a heavy industrial robot arm, isolated part lying horizontal. A rectangular steel box beam about eight times longer than its width, with a round hinge knuckle (horizontal cylindrical joint) at each end, a hydraulic piston running along one side, a cable bundle clipped along the top. Nothing else attached. Game-ready hard-surface prop, worn military bunker machinery. Dark gunmetal steel and rusted iron, bolted armor plates, chipped yellow-black hazard stripes, grime and oil stains, small stencil markings. Heavy, mechanical, clean readable silhouette, beveled edges. Single isolated object, centered, no floor, no background.
```

**P4 Antebrazo**
```
Straight forearm segment of a heavy industrial robot arm, isolated part lying horizontal, a bit thinner than an upper arm. Rectangular steel beam about ten times longer than its width, a large round elbow knuckle (horizontal cylinder) at one end and a round wrist flange (flat disc) at the other end, a small actuator housing near the elbow, a cable bundle along the top. Nothing else attached. Game-ready hard-surface prop, worn military bunker machinery. Dark gunmetal steel and rusted iron, bolted armor plates, chipped yellow-black hazard stripes, grime and oil stains, small stencil markings. Heavy, mechanical, clean readable silhouette, beveled edges. Single isolated object, centered, no floor, no background.
```

**P5 Pinza**
```
Two-finger parallel gripper end effector of an industrial robot arm, isolated part. A compact block palm with a round mounting flange on the back, two flat parallel jaw fingers on short rails in front, wide open, with rubber-padded hazard-striped tips, a small piston between the jaws. No arm attached. Game-ready hard-surface prop, worn military bunker machinery. Dark gunmetal steel and rusted iron, bolted armor plates, chipped yellow-black hazard stripes, grime and oil stains, small stencil markings. Heavy, mechanical, clean readable silhouette, beveled edges. Single isolated object, centered, no floor, no background.
```

**P6 Anillo de bloqueo de muñeca**
```
Mechanical locking collar that attaches a module to a robotic wrist, isolated part: a thick steel ring like a donut with a large empty hole straight through the center, three radial clamp jaws evenly spaced at 120 degrees pointing inward into the hole, a small hydraulic cylinder behind each clamp, small glowing cyan indicator lights between the clamps. Game-ready hard-surface prop, worn military bunker machinery. Dark gunmetal steel and rusted iron, bolted armor plates, chipped yellow-black hazard stripes, grime and oil stains, small stencil markings. Heavy, mechanical, clean readable silhouette, beveled edges. Single isolated object, centered, no floor, no background.
```

**P7 Soporte del cañón (opcional)**
```
Weapon cradle rack for a single arm-cannon module, isolated part: a short steel shelf on one vertical support leg, a U-shaped padded cradle on top, a locking latch, hazard stripes on the front edge, a cable running to the side. Empty, no weapon on it. Game-ready hard-surface prop, worn military bunker machinery. Dark gunmetal steel and rusted iron, bolted armor plates, chipped yellow-black hazard stripes, grime and oil stains, small stencil markings. Heavy, mechanical, clean readable silhouette, beveled edges. Single isolated object, centered, no floor, no background.
```

## Contrato para la integración (cuando lleguen los FBX)

- **Origen y licencia:** generado con IA (Tripo) por el usuario. Anotar esto aquí y en la checklist; Steam pide declararlo.
- **Blender:**
  - trabajar en copias en `Tools/IronRequiem/Blender/Working/MountStation_v02/`;
  - escala en metros con las medidas de la tabla;
  - Y arriba y +Z al frente en Unity;
  - pivotes en los ejes de las bisagras: P2 en el eje vertical, P3 en la bisagra del hombro con el segmento a lo largo de +Z, P4 en el codo, P5 en el centro de la palma mirando a +Z;
  - P5 separada en palma, `JawA` y `JawB`;
  - sin caras duplicadas ni internas ni no-manifold; triangulada, con normales ponderadas y LOD.
- **Presupuesto:** estación completa con dos brazos ≤ 18.000 triángulos en LOD0; ≤ 3 materiales URP/Lit o HardSurface; texturas ≤ 2048.
- **Unity:** prefab nuevo `MountStation_C04_v02` con la misma jerarquía (`ArmA_Hand`/`ArmB_Cannon` → `Yaw`/`Upper`/`Fore`/`Gripper`/`JawA`/`JawB`, `WristLocks`, `CannonRack`, FX, `DockLight` y audio). Se configura con `StationArmRig.Configure` y `WeaponMountStation.Configure`, usando las longitudes medidas.
- **No sobrescribir `MountStation_C04`:** `CombatV4Authoring` (zona protegida `Editor/HangarIntro/`) la regenera, y `WeaponsRedesignAuthoring` y `Map01SessionAuthoring` la referencian.
- **Línea de tiempo:** no se cambia (5,6 s).
- **Al terminar, comprobar en el vestíbulo y en Map01 S1:**
  - cobertura máxima ≤ 25 %;
  - ni brazos ni marco dentro de muros;
  - nada flotando al terminar;
  - una sola luz (`DockLight`);
  - las mismas pruebas de `ARMS_FIX_20261119.md`.
