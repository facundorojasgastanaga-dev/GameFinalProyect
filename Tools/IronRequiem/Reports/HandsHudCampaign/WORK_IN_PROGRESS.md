# Work checkpoint — hands / HUD / campaign

This is an unfinished implementation, not a delivery report. User authorized the complete pasted A–D request and clarified that the prohibition on Codex was copied from a Claude prompt. Caveman chat, normal code/docs. Never edit menu paths or Build Settings. No commits, packages, Meshy credits or source model replacement.

## Environment and execution

- Unity 6000.5.5f1 remains open. Official Pipeline package is absent; do not install experimental packages or launch another Editor/test CLI on this open project.
- Existing local `Assets/IronRequiem/Editor/VisualPass/IRCommandBridge.cs` accepts commands in `Tools/IronRequiem/Bridge/command.txt`; reads/executes once, writes `result.txt`. Check file is absent before writing a batch. Never overwrite another batch. Polling is slow around domain reloads.
- Bridge menu calls returning True do NOT prove callback success; inspect fresh errors, output timestamps and DLL timestamps. Compiler errors leave the old Editor DLL loaded and old menu callbacks runnable. Wait until `Library/ScriptAssemblies/IronRequiem.Editor.dll` is newer than modified Editor source before issuing new commands.
- Source logs: `Logs/Editor.log`, bridge `console.txt`. Historical compile/runtime errors remain; current code compiled at ~20:21 on 15 Nov. Initial menu `RequiemMenuView` CS0246 was fixed by another session. This session never edited it.
- CUA found Unity open on IntroMenu but screenshots returned Codex; native focus attempt aborted. Use the existing bridge. A completion summary was sent for the initial inventory.

## Backup and historical docs

- 804-file backup: `Tools/IronRequiem/Backups/20261115-hands-hud-campaign/`; original scripts, Editor tools, tests, Docs, Packages, ProjectSettings and campaign/Room00 scenes/metas.
- `protected-hashes.json` records 156 menu/BuildSettings files. Other session changed menu/test files during work. Never restore those changes blindly. `preservation.json` is an early snapshot, not final.
- `Docs/IronRequiem/HANDS_HUD_CAMPAIGN_DIAGNOSTIC_20261115.md` and `Contexto_Animaciones.md` are EARLY drafts incorrectly still describing the initial compiler block. Rewrite before delivery; update CHECKLIST_TRABAJO.md too.

## Implemented, awaiting full validation

1. `HandPoseLibrary` SO has absolute Open/Relaxed/Fist/Grip/Inspect rotations. `HandPoseController` (LateUpdate 1000) owns finger results from these immutable targets, with blends. FPS presentation requests poses instead of calling legacy HandFistRig on migrated campaign instances. Legacy lab fallback remains.
2. New `HandPoseAuthoring.Apply` recovers actual skin bind matrices, calibrates per-joint flexion/sign, and writes FPS L/R assets in `Assets/IronRequiem/Art/Player/HandPoses/`. It now repeats the +15 sign test after inversion. Targets MCP 85/90/92/95, PIP102, DIP65, thumb40/37.5/27.5. FPS log must be freshly regenerated if needed; earlier log was from before actual retest wording.
3. Original FPS skin is visibly distorted. Authorized Plan B: Blender `Tools/IronRequiem/Blender/build_closed_fist.py` creates rigid mechanical fists, exports JSON and blend under `Tools/IronRequiem/Exports/PlayerRigidFist/`. Current BVH metrics: thumb-to-index/middle second link 2.000004 mm; all four distal-to-palm 9.5 mm; piece scales all one; 2,484 triangles. JSON mesh is imported into Unity `.asset`, mirrored for L. Controller crossfades old hand and rigid visual. Closed transparent materials now use ZWrite=1 to stop ghosting; new capture still required.
4. `FirstPersonUpperArms` poses rigid upper-arm/shoulder segments extracted from CapsuleRobotRig, scaled in mesh data (2.2), plus elbow joints, following FPS elbow/shoulder guide. Existing source FBX unchanged. Generated normal-map meshes now have tangents.
5. Armed F now sets ArmedStrike and uses existing right-forearm BashPose; unarmed F remains left and M1 right. FistChargePresentation now follows active fist or mounted forearm.
6. `RetroStatusBar`, `RetroDigitsGraphic`, `RobotPortraitGraphic` implement bottom classic HUD. Static industrial panels, bitmap geometry numerals, slots, reactive visor. Existing MechanicHud restored if missing and compact mode avoids per-frame status string construction. Prompts/reticle/round banners retained.
7. IMPORTANT UI bug fixed in authoring but NEEDS fresh visual proof: `PlayerHealthBar` lives on GameHUD CONTROLLER ROOT, not its isolated visual. Earlier tool disabled that root and hid all HUD. Current tool reactivates the root containing ClassicStatusBar, disables the old health component, and only hides its child health panel. Heat widget only disabled if not ancestor of new bar. Existing MechanicsHUD container is always stretched; scaler is now ScaleWithScreenSize1920x1080/.5. Custom graphics now require CanvasRenderer, use non-legacy mesh generation and standard UI winding. Earlier captures either lack bitmap/portrait or lack entire HUD; none are valid delivery evidence.
8. CampaignFlow preloads next room at completion of current preparation (activation held), adopts that load in airlock, uses Low background priority through campaign, removes gameplay UnloadUnusedAssets. Existing sliced teardown/window retained, no all-map residency. `DrainPendingLoadsForShutdown` releases/awaits held load BEFORE async unload and blocks new loads/transitions. CampaignTests.Cleanup uses it before enumerating loaded scenes.
9. Startup ShaderVariantCollection is generated from new player materials, referenced by CampaignFlow and warmed behind initial cover. Real six-view room warmup remains.
10. Cinematic over-shoulder reveal follows existing planted-foot body turn; capsule/world sphere casts try 3 positions and otherwise retain authored shot. No minimum distance pushing through close obstacles. WakeUpRobotPose exposes facing/center. Cinematic fingers now sampled deterministically from timeline weights (Inspect/Grip and smooth fist window) via HandPoseController.Sample; timeline mode reapplies same absolute sample in LateUpdate. `Apply cinematic poses` MUST run after compile to save timelineDriven=true into Room00; queued now.

## Verification tools and status

- New Editor tools: HandsHudCampaignTools, HandPoseAuthoring, PlayerVisualRepairAuthoring, HandsHudCampaignVerification. All menus under `Tools/Iron Requiem/Hands HUD Campaign/`.
- Pose tests: previously 2/2 passed. Added third deterministic timeline sample test; latest run queued, await `poses-tests-status.txt`.
- CampaignTests now adds PresentationHandsMountedArmsAndOverdriveHud (real input, rest/quick/full unarmed, mounted charge/fire, stolen charge, actual overdrive UI snapshots) and station snapshot in existing setup. Updated old armed-left assertion to required forearm bash.
- New HangarIntroPresentationTests replays full intro, captures inspect and shoulder frames, then verifies E and Space skips match final player position/rotation and finger pose. Full runner selects BOTH classes (8 PlayMode tests total: original6 + presentation1 + cinematic1).
- Run menus: Run pose tests / Run campaign tests. Runner writes XML/status/test-errors under this report folder, resumes callbacks across reload and clears Console at run start while preserving historical Editor.log.
- CaptureFists now prefixes right-fist/left-fist/rest so one preview cannot overwrite the other hand's closure evidence. File state records closure. CaptureGameplayState reads SessionState `IR.HandsHud.CaptureTag`, chooses cinematic camera if intro playing, writes1920x1080 PNG + state.
- Capture HUD resolutions now converts/restores overlay canvases temporarily, adds a UI OVERLAY camera to actual URP base stack (old separate base UI camera cleared world). Supports1280/1920/2560 widths; healthy/damaged/overdrive tag. Needs actual image inspection.
- Inspect runtime visuals menu writes custom graphics mesh counts, cull/depth/alpha, canvas sizes into runtime-visuals.txt. Previous file only lists CinematicUI/ArenaMessages/CombatLabMenu because GameHUD root was disabled.
- Preview campaign / Begin preview (call twice after room load to request skip), Preview left fist/right fist, Capture fists, Capture HUD resolutions, Damage portrait preview, stop, Restore preview scene. All reversible Play changes; no save runtime state.

## Performance baseline

`Build baseline` succeeded, errors0, 2m59s. Build path `Builds/HandsHudCampaign/before/IronRequiem.exe`; build-local scene list starts campaign and excludes IntroMenu, never modifies Build Settings. Ran actual Player with -irTour -irFullHD via Start-Process; require_escalated launch was needed for real desktop runtime. Baseline report `tour-before.txt`: actual1600x900 (old FullScreenWindow clamped to desktop), transition peaks25.6/21.0/36.3/18.2/19.0/19.4 ms; Room02 exceeds33. Do NOT label this FullHD.

CampaignTourBenchmark now forces windowed1920x1080 + Display.SetParams and quits with error if actual resolution differs. Need Build repaired, run Player -irTour -irFullHD, inspect actual dimensions, transition peaks and per-room mean/p99. Also run without -irFullHD at1600x900 for honest like-resolution comparison if useful. No after measurement yet. No memory-profiler claim yet.

## Current queue / next work

Latest queued batch: Apply rigid arms and HUD; Apply cinematic poses; Run pose tests. Check bridge result and fresh 3/3 pose status. Then preview and inspect runtime/HUD/fist captures. Do not proceed on missing custom graphics or invisible HUD. Apply failures may return True in bridge; check fresh scene mtime/errors.

Once visuals are correct: run full 8-test PlayMode suite (do NOT compile during it), inspect failures/logs and screenshots; fix only concrete issues and rerun relevant failures. Then build repaired and run Player tour; fix measured transition issues until target or document real blocker. Finally verify hashes/changed-file ownership, update reports/Contexto/checklist, exit Play, deliver concise truthful result with evidence link. Do not call task complete just because compilation passed.

Review agents (skill code-standards explicitly authorized delegation): review_hands found capture overwrite, false sign-certification, wrong charge FX anchor, missing tangents — fixed; it later hit usage limit. review_ui_flow found prompt container, camera near obstacle, HUD capture issue and preload priority — fixed. final_review found drain deadlock/flicker/scrub/jump — fixed, integration pending. No agents modified files or operated Unity.
