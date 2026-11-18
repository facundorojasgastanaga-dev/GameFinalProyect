# Guardián de la zona protegida — 2026-11-17

Solo lectura. Base: `before-20261117.txt` (443 filas: 2 escenas + 441 dependencias). Uso por salas = cierre transitivo de GUID leído del YAML (escena → prefab/mat/asset/controller → …) de Room00b_AlarmCorridor, Room01_Foundry, Room02_Breach, Room03_Assembly, Room04_Reactor, Pursuit05 y RequiemCampaign. No se leen los `.fbx.meta` (remapeos de materiales) ni las cargas por `Resources.Load`/código: un asset que solo se carga en runtime no aparece aquí.

Nota sobre las etiquetas: la instantánea usa 3 etiquetas compartidas, no 1: `shared:IntroMenu`, `shared:Room00_Hangar` y `shared:IntroMenu+Room00_Hangar` (más `protected`).

Integridad (re-hash ahora contra la instantánea): 396 de 397 entradas de `Assets/` son idénticas (archivo y meta). **Cambió 1:** `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` (atlas dinámico de TMP; suele reescribirse al renderizar texto en el Editor, no está en la lista del lote). Las 46 entradas `Packages/…` no se pueden verificar leyendo archivos del proyecto (viven en la caché de paquetes).

## 1. Dependencias compartidas por tipo

| Tipo | Total | protected | solo IntroMenu | solo Room00 | Intro+Room00 | **usadas por salas** |
|---|---|---|---|---|---|---|
| MonoScript | 119 (35 de paquetes) | 35 | 22 | 42 | 20 | 70 |
| Mesh/FBX (Mesh + modelos .fbx) | 97 | 0 | 10 | 78 | 9 | 88 |
| Texture2D | 61 (5 de paquetes) | 0 | 20 | 15 | 26 | 46 |
| Material | 57 (1 de paquete) | 1 | 8 | 19 | 29 | 39 |
| AnimationClip | 31 | 0 | 18 | 5 | 8 | 31 |
| AudioClip | 23 | 0 | 5 | 16 | 2 | 14 |
| Prefab | 22 | 2 | 1 | 19 | 0 | 17 |
| Shader | 8 (4 URP/paquete) | 0 | 3 | 0 | 5 | 8 |
| VolumeProfile | 1 | 0 | 0 | 0 | 1 | **0** |
| Otros (controllers, masks, avatar, mixer, fonts TMP, timeline, navmesh, tema UI, hand poses) | 22 | 0 | 10 | 7 | 5 | 17 |
| **Total** | **441** | 38 | | | | **330** |

Las 35 entradas `protected` incluyen 16 scripts Editor (`Editor/HangarIntro/*`, `Editor/Menu/HangarMenuAuthoring.cs`), que no aparecen en ninguna escena.

## 1b. LISTA ROJA: compartidos con la zona protegida Y usados por las salas → COPIAR, no editar

Abreviaturas: 00b, 01–04, P05, RC = Room00b…Room04, Pursuit05, RequiemCampaign. "d" = referencia directa en la escena.

**Entorno / render**

| Asset | GUID | Zona | Salas (vía) |
|---|---|---|---|
| Art/Shaders/HardSurface/IronRequiemHardSurface.shader | 20aba464369adfd4e800688c83c02a4a | Intro+R00 | todas (VO_Env_RustMetal.mat, VO_Env_Hazard.mat, CP_Cable.mat, IR_V35_FloorGuide.mat, T07_Current_Repaired) |
| Art/VisualOverhaul/Materials/VO_Env_RustMetal.mat | b296f58c4ab28b1439a38024412900e9 | Intro+R00 | todas (d) |
| Art/Environment/Bunker/Textures/Bunker_RustMetal_Albedo.png | 4b431a3837e308948854f2dc1c1693f0 | Intro+R00 | todas (vía VO_Env_RustMetal.mat) |
| Art/Environment/Bunker/Textures/Bunker_RustMetal_Normal.png | d4d2cfdeb857af14da433705b6159169 | Intro+R00 | todas |
| Art/VisualOverhaul/Textures/VO_Noise.png | 3305035890b4b3a4088e21c1d0c03c6a | Intro+R00 | todas |
| Art/Menu/Materials/HG_Plate, HG_PlateDark, HG_Trim, HG_Hazard, HG_Hose, HG_RedGlow.mat | (ver instantánea) | Intro+R00 | 00b, RC (d) |
| Art/Menu/Materials/HG_City.mat, HG_LampWarm.mat | 7a68816b…, 90d5e69a… | Intro+R00 | 00b (d) |
| Art/Menu/Materials/HG_Floor.mat | 8de28fe75c3ee474f84732225794d7c1 | Intro | 00b, RC (d) |
| Art/Menu/Materials/HG_Glass.mat, HG_CyanGlow.mat, HG_OrangeGlow.mat | ce30fe24…, 1aca80bd…, e86def43… | Intro+R00 | RC |
| Art/Menu/Textures/Hangar_Floor_*, Hangar_Plate_*, Hangar_Hazard.png | — | Intro+R00 | 00b, RC |
| Art/Menu/Materials/HG_FxSmoke, HG_FxSpark, HG_FxFire, HG_FxDebris.mat + Hangar_Smoke/SoftDot.png | — | Intro (+R00) | 01–04, RC (BigBot_Current.prefab, FX_Fist_HeavyImpact_v4) |
| Art/Menu/UI/HG_UI_CCTV.mat, IR_UI_*.png, IronRequiemCctv.shader, Audio/Music/IR_Menu01_Loop.wav | — | Intro | RC (d) |

**Enemigos**

| Asset | GUID | Zona | Salas (vía) |
|---|---|---|---|
| Art/Enemies/T07Mech/Built/T07Mech_Atlas.mat | 039efce46b32f3944a8186954a6e561e | Intro (MenuWalker_T07) | 01–04 (T07_Mech.prefab) |
| Art/Enemies/T07Mech/Built/T07Mech_Atlas.png | 9673d4367a26a99478af8debb151e447 | Intro | 01–04 |
| T07Mech_Rigged.fbx, T07Mech_Locomotion.controller, T07Mech_UpperBody.mask, clips Mech_*.anim | — | Intro | 01–04 (T07_Mech.prefab) |
| T07Animated: T07_GenericAvatar, T07_Locomotion.controller, T07_UpperBody.mask | — | Intro | 00b–04, P05 (T07_Current_Repaired) |
| Carronero_Body.mat + Carronero_Albedo/MetalSmooth/Normal | 06a82d6e… | Intro+R00 | 00b, 02, 03, 04 (Carronero.prefab) |
| Prefabs/Campaign/Carronero.prefab | f6b6432993420e5409d267ac7782cdd0 | R00 (5 Scavengers) | 00b, 02, 03, 04 (d) |
| SawBot_URP.mat, SawBot_Eye.mat, SawBot_URP_FPS.mat, texturas IR_SawBot_*, 4 controllers SawBot*, prefabs SawBot_AI_v01/Arsenal_v03/SawPolish_v04/Sustained_v02, Weapon_SawBot_*, SawBot_*Module | — | R00 (SawPolish.controller también Intro) | 00b–04, P05 (Carronero, SawBot_Current) |
| HoverBot_Body/Eye/Glass.mat, texturas HoverBot_*, HoverBot.fbx + clips, HoverBot_AnimationController | — | Intro | 00b–04, P05 (HoverBot_Current, T07_Current_Repaired) |
| VisualRepair/RobotEye_Red.mat | d3add111f0a04394284bb4b33bed8fec | R00 | 00b–04, P05 |

**VFX**

| Asset | GUID | Zona | Salas (vía) |
|---|---|---|---|
| Prefabs/VFX/RobotBodyBurst_CombatPolish.prefab | 6a412bf6662de62419069b9689dbd81a | R00 (Carronero→SawBot_AI_v01) | 00b–04, P05; **es la base del nuevo `_IR`** |
| Prefabs/VFX/CannonExplosion_IR.prefab, T07_BodyExplosion.prefab | 51c9870a…, 3cf1d09b… | R00 | 00b–04, P05 |
| Prefabs/VisualPass/VFX/VP_CannonExplosion_IR.prefab | 00ee1e78… | R00 | 00b–04, P05 |
| Prefabs/VisualPass/VFX/VP_VFX_HoverBot_Explosion.prefab | 9da5a859757a3a0468bf3efe65065109 | **Intro (extraVfx de MenuExplosionFx)** | 00b–04, P05 (T07_Current_Repaired) |
| VFX/CannonExplosion/*.mat + PressureEdge/SmokeLobe/PressureRing | — | R00 | 00b–04, P05 |
| VFX/Mechanics/MechanicParticles.mat, ParticleSoftCircle.asset | aec7ad1d…, 3b114a6c… | R00 | todas |
| FPS: VFX_HoverBot_Explosion, VFX_LazerSparksGreen, Projectile_Blaster, DiskExplosion/ImpactFlare/Spark/Lazer_Green/SpeedLine/TrailDeath.mat, ExplosionRing/Flare/Speedlines texturas, Mesh_ExplosionDisk/Projectile.fbx | — | Intro y/o R00 | 00b–04, P05 (+RC algunos) |

**Global / UI / audio**: MainAudioMixer.mixer, LiberationSans SDF(+Fallback)/TMP_SDF-Mobile, Roboto-Black SDF, TEX_White/Black_16x16, Crosshair_Hexa, HUD_Weapon03, Pickup_Weapon_Small/Weapon_Cooling/Hoverbot_*.wav — compartidos con todas o casi todas las salas.

**Fuera de la lista roja (verificado):**
- `Art/Rendering/CombatClarity.asset` (c42f1c04cb15a184d83431794d4ecfdd): no lo usa IntroMenu ni Room00 ni ninguna sala; solo MainScene y labs. Libre.
- `Art/Environment/Bunker/*.mat` (Bunker_Wall, Floor, RustMetal, …): ninguno está en la zona protegida; solo sus texturas RustMetal (vía VO_Env_RustMetal.mat). Las .mat se pueden editar; las texturas no.
- `Art/Environment/Scrap/*` y `Prefabs/Scrap/*`: solo Room00 (zona protegida), ninguna sala los usa.
- VolumeProfiles: el único protegido es `HG_MenuVolume` y **ninguna sala lo referencia**. `Resources/IronRequiem/HG_GameplayVolume.asset` no está en ninguna escena (se carga por código).
- Puertas/airlock: no hay prefabs con Door/Airlock en `Prefabs/`; los airlocks son objetos de escena en RequiemCampaign. No se cruzan con la zona protegida.

## 2. Lote 20261117-mapfx-phase2: impacto en la zona protegida

GUIDs: VolumeFadeIn.cs `f743e9c773c581b4db8e437ba1709e16`, ZoneEnvironment.cs `2b6b04472531c2f47813002e87efea0b`, RequiemSettings.cs `50a049d56152f064ca943b47d824be23`, RobotBodyBurst_CombatPolish_IR.prefab `6933cfa37e0725e4b8e1726ce3d45670`, HG_MenuVolume `94a637b6240e2134a8363cff84ff101b`, HG_GameplayVolume `7d12127910af08f42b63bc53c04ab3ab`.

| Elemento | ¿Lo usa IntroMenu / Room00? | Veredicto | Evidencia |
|---|---|---|---|
| VolumeFadeIn.cs | No: 0 apariciones del GUID en IntroMenu.unity, Room00_Hangar.unity y MenuWalker_*. Lo llevan Room01–04 y P05 (y sus _MechTest). | **SIN EFECTO** | grep del GUID f743e9c7… |
| ZoneEnvironment.cs (cambio de perfil) | No es componente (clase estática, 0 apariciones del GUID). Se ejecuta desde `CampaignFlow.fadeIn/fadeOut.Bind(scene)` (CampaignFlow.cs:204, :247) sobre las escenas de `stageScenes`, que empiezan por **Room00_Hangar** (RequiemCampaign.unity:51148-51149). IntroMenu **no** está en la lista y no se hace Bind sobre ella. | **EFECTO EN RUNTIME EN ROOM00**: dentro de la campaña, el Volume global `HangarSector/HG_PostVolume` (Room00_Hangar.unity:242685, sharedProfile = HG_MenuVolume) pasa a `HG_GameplayVolume`: CA 0.12→0, viñeta 0.38→0.20, grano 0.28→0.10, intensidad de bloom 0.6→0.25 (el resto es igual). Afecta al aspecto de toda la sala, incluida la cinta, la pasarela y la cinemática de despertar. No cambia el comportamiento. No se guarda: es la escena en Play, y HG_MenuVolume.asset es idéntico al respaldo. Si Room00 se abre sola, sin CampaignFlow, no hay cambio. | ZoneEnvironment.cs:61,65,80 |
| HG_MenuVolume en IntroMenu | Sí, `HangarSet/HG_PostVolume` (IntroMenu.unity:61316). | **SIN EFECTO EN MENÚ**: el asset no cambió (cmp con el respaldo idéntico, hash igual a la instantánea) y en IntroMenu nunca se ejecuta el cambio de perfil. | — |
| ¿IntroMenu se carga como etapa? | No. `stageScenes` = Room00_Hangar, Room00b, Room01–04, Pursuit05; `prologueStages: 2`. | Correcto | RequiemCampaign.unity:51148-51165 |
| RequiemSettings.cs (modo de pantalla) | Se ejecuta en IntroMenu: RequiemMenuView (componente del Canvas) llama a `RequiemSettings.Apply()` (RequiemMenuView.cs:35), que ahora llama a `ApplyDisplayMode` (RequiemSettings.cs:30,34-49) si hay PlayerPrefs. | **EFECTO EN MENÚ, solo en build y solo de ventana**: resolución nativa en pantalla completa y 1600×900/1280×720 en ventana. En el Editor conserva `Screen.fullScreen` (línea 36). No toca la cinta, la pasarela, los walkers, MenuExplosionFx ni HangarLaunchSequence. | — |
| ProjectSettings (defaultIsNativeResolution 0→1, fullscreenMode 3→1) | Global, afecta a todo el arranque de la build (también al menú). | **EFECTO EN MENÚ (global de ventana, build)**: el juego arranca en Fullscreen Window a resolución nativa en vez de en ventana. No cambia el contenido. EditorBuildSettings sin cambios. | diff ProjectSettings.asset líneas 88 y 115 |
| HoverBot_Current, Turret_Current, T07_Mech, T07_Current_Repaired → `_IR` | Ninguno está en IntroMenu ni en Room00. Room00 solo instancia Scrap_* y Carronero (5); IntroMenu solo instancia MenuWalker_T07/Carronero y CapsuleRobotRig. Carronero.prefab no cambió. T07_Mech también recibió `executionEnabled: 1` y `PermanentMelee: 0` (no están en la zona). | **SIN EFECTO** | m_SourcePrefab de las dos escenas |
| RobotBodyBurst_CombatPolish_IR.prefab (nuevo) | 0 apariciones en IntroMenu, Room00 y MenuWalker_*. Es una variante del original `6a412bf6…` (33 overrides), que es compartido con Room00 y cuyo hash no cambió. | **SIN EFECTO** | — |
| MenuWalker_* / MenuExplosionFx → RobotBodyBurst_* | No. MenuExplosionFx no tiene campo de prefab de burst. Su `extraVfx` apunta a VP_VFX_HoverBot_Explosion (`9da5a859…`, IntroMenu.unity:22932 y 59033), que no cambió. | **SIN EFECTO** | — |
| HangarLaunchSequence (JUGAR) | No referencia ningún archivo del lote. Scripts/Menu y Scripts/HangarIntro no usan ZoneEnvironment, VolumeFadeIn, RequiemSettings ni Resources. | **SIN EFECTO** | grep |
| VisualOverhaulEnemies.cs (Editor, en el respaldo) | Herramienta Editor de variantes _VO; no la usa la zona. | SIN EFECTO | — |

Riesgos detectados:
- `Editor/HangarIntro/HangarSectorAuthoring.cs:46` (protegido) vuelve a asignar HG_MenuVolume a Room00 si se ejecuta. Es coherente con el diseño del lote, porque el cambio es solo en runtime.
- El cambio de perfil busca el nombre `"HG_MenuVolume"`, no el GUID. Cualquier etapa futura con un Volume global de ese nombre también cambiará.
- La cinemática de Room00 dentro de la campaña ya no se ve igual que en la versión con el perfil de menú. Si eso cuenta como "aspecto protegido", es el único efecto real del lote.

## 3. Lista "nadie toca"

**Archivos `protected` de la instantánea (38):**
- Escenas: `Assets/FPS/Scenes/IntroMenu.unity`, `Assets/IronRequiem/Scenes/Campaign/Room00_Hangar.unity`
- `Assets/IronRequiem/Scripts/Menu/`: BridgePatrol, BridgeWalker, CapsuleRobotIdle, ConveyorBelt, HangarButtonFx, HangarLaunchSequence, HangarStatusTag, MainMenuPanelPresentation, MenuCameraRig, MenuExplosionFx, MenuFlicker, MenuSpin (.cs)
- `Assets/IronRequiem/Scripts/HangarIntro/`: CapsuleGlassBreak, CapsuleRestraints, FootContactFx, HangarIntroCinematic, ScavengerBehaviour, ScavengerGroup, WakeUpRobotPose (.cs)
- `Assets/IronRequiem/Editor/Menu/HangarMenuAuthoring.cs`
- `Assets/IronRequiem/Editor/HangarIntro/`: CombatV4Authoring, FistInventory, FistPoseBake, FistPoseEditor, HangarIntroDebug, HangarIntroDebugV30, HangarIntroDebugV4, HangarIntroDebugV5Ui, HangarIntroDebugV6, HangarIntroProbe, HangarPitAuthoring, HangarScrapAuthoring, HangarSectorAuthoring, HangarTransitAuthoring, RobotRigidSkin (.cs)
- `Assets/IronRequiem/Prefabs/Menu/MenuWalker_Carronero.prefab`, `MenuWalker_T07.prefab`, `Prefabs/Menu/Materials/MW_Carronero_Saw.mat`
- Además, por regla: todo lo etiquetado `shared:*` (los 441 de arriba) solo se toca en copia o variante.

**Room00_Hangar.unity — jerarquía de la zona** (raíces: WakeUpCinematic 43 objetos, HangarSector 692, RoomEncounter 1, Scavengers 7)

| Objeto | Motivo |
|---|---|
| `WakeUpCinematic` (completo: CinematicCamera, Shots/* incl. Shot_D2_Belt y D2_Belt_From/To, HeroKey, StandPoint, CinematicFx, CinematicUI, CinematicAudio) | HangarIntroCinematic + WakeUpRobotPose + FootContactFx |
| `HangarSector/Bridge` (198: BeltBody, BeltSurface, ChevronStrip, BeltGuard±1.25, BridgeLamp×4, Roller×44) | ConveyorBelt, MenuSpin, MenuFlicker: cinta y pasarela |
| `HangarSector/ConveyorPit` (177, incl. UnderBeltGantry) | foso bajo la cinta |
| `HangarSector/Capsule_C04` (112: Protagonist, CanopyBreak, Canopy, Crack, GlassImpact, Cradle/Restraint_*, RailGrip, VentSteam, RestraintFx, cables, strips) | CapsuleRestraints, CapsuleGlassBreak, CapsuleRobotIdle; referenciados por la cinemática |
| `HangarSector/CinematicBeltTarget`, `CinematicLookTarget` | objetivos de WakeUpRobotPose |
| `HangarSector/Environment/Clutter/Catwalk` | pasarela |
| `Scavengers` (5 instancias de Carronero con ScavengerBehaviour + ScrapPieces) y `HangarSector/ScrapPoints/ScrapPoint0-9` | ScavengerGroup. Son los "walkers" de Room00; **en Room00 no hay MenuWalker_* ni BridgePatrol**, que solo existen en IntroMenu (`HangarSet/BridgePatrol`) |
| `RoomEncounter` | lo referencia HangarIntroCinematic |
| `HangarSector/HG_PostVolume` | usa HG_MenuVolume (asset compartido) |
| Luces con MenuFlicker o MenuSpin: OverheadLamp, Lights/Beacon, BeaconHead, HallTube0-2, SectorLamps/ExitGlow, WorkLamp0-2, ScrapZones/ScrapGlow | scripts protegidos de Scripts/Menu |

**Fuera de la zona** (se podría tocar en una copia de Room00, nunca en el original): `HangarSector/City` (58), `SectorShell` (10), `Environment` salvo Catwalk (≈31), `ScrapZones` (34, pilas de Scrap_*, salvo ScrapGlow), `SectorFx` (2), `CctvFeed`, `PodRim`/`PodStrut`/`PodBase`, y las luces de `Lights`/`SectorLamps` que no tienen MenuFlicker o MenuSpin. Advertencia: los planos de la cinemática (Shot_E_Reveal, C_Exit, D2_Belt…) encuadran el hangar. Cambiar algo visible ahí cambia el aspecto de la cinemática, aunque el objeto no tenga scripts protegidos. Que un plano concreto vea o no un objeto no se puede determinar leyendo el YAML.
