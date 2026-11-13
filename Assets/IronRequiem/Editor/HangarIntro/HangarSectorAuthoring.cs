using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Cinemachine;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// 12/11/2026 — Sector 0 "Room00_Hangar": the main-menu hangar (capsule C-04) as the first campaign stage.
    /// Reuses the menu builders (HangarMenuAuthoring: HG_* materials/textures, environment, capsule) under a root rotated
    /// so the hangar's front opens onto the persistent lobby of RequiemCampaign, adds gameplay colliders, scrap zones,
    /// five neutral scavenging Carroñeros (the real campaign prefab), the NavMesh, and the wake-up cinematic
    /// (Timeline + Cinemachine + WakeUpRobotPose). Then wires the stage into CampaignFlow:
    /// Room00_Hangar -> lobby airlock -> Room01_Foundry. Idempotent; scene and asset GUIDs are kept.
    /// Run: Tools/Iron Requiem/Hangar Intro/Build Room00_Hangar + campaign
    public static class HangarSectorAuthoring
    {
        const string Menu = "Tools/Iron Requiem/Hangar Intro/";
        public const string SectorName = "Room00_Hangar";
        public const string SectorScene = "Assets/IronRequiem/Scenes/Campaign/" + SectorName + ".unity";
        const string Session = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        const string LevelArt = "Assets/IronRequiem/Art/Levels/Campaign";
        const string AudioDir = "Assets/IronRequiem/Audio/SFX/HangarIntro";
        const string TimelinePath = LevelArt + "/Room00_WakeUp.playable";
        const string NavPath = LevelArt + "/Room00_Hangar_NavMesh.asset";
        const string CarroneroPrefab = "Assets/IronRequiem/Prefabs/Campaign/Carronero.prefab";
        const string WalkerCarronero = "Assets/IronRequiem/Prefabs/Menu/MenuWalker_Carronero.prefab";
        const string WalkerT07 = "Assets/IronRequiem/Prefabs/Menu/MenuWalker_T07.prefab";
        const string MenuVolume = "Assets/IronRequiem/Art/Menu/HG_MenuVolume.asset";
        const string Report = "Tools/IronRequiem/Reports/HangarIntro";
        const string PrologueTitle = "HANGAR C-04 · SAL POR LA ESCLUSA";

        // Layout (hangar-local metres; the menu hangar spans x ±17, z -25..45, the front opens at z -24)
        // 12/11/2026 v2: the hangar front (local z -24.5) lands on the persistent connection chamber (world z -84.5):
        // hangar -> chamber (persistent) -> vestibule (persistent) -> alarm corridor (Room00b) -> lobby airlock -> Room01.
        static readonly Vector3 Origin = new Vector3(0, 0, -109);
        const float Yaw = 180f;
        const float FrontZ = -24f, DoorHalf = 4f, DoorHeight = 4.6f, HallHalf = 17f, HallHeight = 18f;
        const float CinematicSeconds = 27.8f; // v30: servo/cannon beat cut (v3 was 30.4 s); shot and cue times follow WakeUpRobotPose.Timing
        const float NavRadius = .6f, NavHeight = 1.6f;
        // Scrap piles: x, z, radius. The first five have scrap points; the last two are set dressing.
        // v3 scrap yard (hangar-local x, z, radius, height, lean towards a wall/column). The first WorkedPiles are the
        // scavengers' piles (scrap points on their hall side); the rest lean on the walls and columns or replace the crates.
        static readonly (float x, float z, float r, float h, float lx, float lz)[] Piles =
        {
            (-9.2f, -10f, 2.0f, 4.1f, 0, 0), (-6.6f, -15f, 1.8f, 3.8f, 0, 0), (-12.8f, -19.5f, 2.1f, 4.9f, -1, 0), (9.4f, -11.5f, 2.0f, 4.2f, 0, 0), (7.4f, -18f, 1.9f, 3.9f, 0, 0),
            (-13.6f, 15f, 2.0f, 5.2f, -1, 0), (13.4f, 6f, 1.9f, 4.9f, 1, 0),
            (-13.8f, -22f, 1.7f, 4.9f, -1, -.6f), (13.8f, -22f, 1.7f, 5.2f, 1, -.6f), (-13.6f, -3.8f, 1.7f, 5.5f, -1, .5f), (13.6f, -4.6f, 1.8f, 5.5f, 1, .4f),
            (-13.8f, 8.5f, 1.6f, 4.6f, -1, 0), (13.6f, 13.5f, 1.8f, 5.5f, 1, 0), (-13.4f, 23f, 1.7f, 4.9f, -1, 0), (13.2f, 26f, 1.6f, 4.6f, 1, 0),
            (-8.2f, -22.6f, 1.4f, 3.8f, 0, -1), (8.6f, -22.6f, 1.4f, 3.6f, 0, -1), (-4.4f, 3.2f, 1.25f, 3.2f, 0, 0), (-7.5f, 26.5f, 1.6f, 4.3f, 0, 1),
        };
        const int WorkedPiles = 5, Scavengers = 5;

        static StringBuilder log;

        [MenuItem(Menu + "Build Room00_Hangar + campaign")]
        public static void Build()
        {
            log = new StringBuilder("Room00_Hangar build " + DateTime.Now + "\n");
            Directory.CreateDirectory(Report);
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene open: " + SceneManager.GetSceneAt(i).path + ". Save or discard it first; nothing was changed.");
                var backup = "Tools/IronRequiem/Backups/" + DateTime.Now.ToString("yyyyMMdd-HHmm") + "-room00-hangar";
                Directory.CreateDirectory(backup);
                foreach (var f in new[] { Session, SectorScene, TimelinePath, NavPath, "ProjectSettings/EditorBuildSettings.asset" })
                    if (File.Exists(f)) { File.Copy(f, Path.Combine(backup, Path.GetFileName(f)), true); if (File.Exists(f + ".meta")) File.Copy(f + ".meta", Path.Combine(backup, Path.GetFileName(f) + ".meta"), true); }
                log.AppendLine("backup " + backup);

                HangarMenuAuthoring.PrepareShared(log, TMP_Settings.defaultFontAsset);
                HangarMenuAuthoring.ConfigureImports();
                ConfigureAudio();
                var m = HangarMenuAuthoring.BuildMaterials();
                var scrap = HangarScrapAuthoring.Prefabs(log);
                RobotRigidSkin.Prepare(log);   // before the sector scene holds a robot instance (may reimport the rig)
                CombatV4Authoring.PrepareAssets(log, m);   // v4: fist weapon, Big Bot blast/damage/no execution, feedback FX
                BuildSector(m, scrap);
                HangarTransitAuthoring.BuildCorridor(log, m);
                HangarTransitAuthoring.IntegrateCampaign(log, m);
                AssetDatabase.SaveAssets();
                // v30: leave the Editor on the main menu. Before, the build ended with RequiemCampaign open, so pressing
                // Play started the campaign's own simple start menu (CombatLabMenu) instead of the Hangar C-04 menu.
                EditorSceneManager.OpenScene(HangarMenuAuthoring.ScenePath, OpenSceneMode.Single);
                log.AppendLine("reopened " + HangarMenuAuthoring.ScenePath);
                File.WriteAllText(Report + "/build.txt", log.ToString());
                Debug.Log("[HangarIntro] build ok\n" + log);
            }
            catch (Exception e)
            {
                log.AppendLine("ERROR " + e);
                File.WriteAllText(Report + "/build.txt", log.ToString());
                Debug.LogError("[HangarIntro] " + e);
            }
        }

        // ------------------------------------------------------------------ imports
        static void ConfigureAudio()
        {
            AssetDatabase.Refresh();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ai = AssetImporter.GetAtPath(path) as AudioImporter; if (!ai) continue;
                var s = ai.defaultSampleSettings; bool dirty = false;
                if (!ai.forceToMono) { ai.forceToMono = true; dirty = true; }
                if (s.loadType != AudioClipLoadType.DecompressOnLoad || s.compressionFormat != AudioCompressionFormat.ADPCM)
                { s.loadType = AudioClipLoadType.DecompressOnLoad; s.compressionFormat = AudioCompressionFormat.ADPCM; ai.defaultSampleSettings = s; dirty = true; }
                if (dirty) ai.SaveAndReimport();
            }
        }

        static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/" + name + ".wav");

        static AudioMixerGroup MixerGroup(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets("MainAudioMixer"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid); if (!path.EndsWith(".mixer")) continue;
                var g = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AudioMixerGroup>().FirstOrDefault(x => x.name == name);
                if (g) return g;
            }
            log.AppendLine("mixer group missing: " + name);
            return null;
        }

        // ------------------------------------------------------------------ sector scene
        sealed class Built { public Transform set, pod, stand, grip, look, scav, belt, impact; public CapsuleGlassBreak glass; public HangarMenuAuthoring.PodFx podFx; public List<Transform> points = new List<Transform>(); public CampaignRoom room; }

        static void BuildSector(HangarMenuAuthoring.Mats m, GameObject[] scrap)
        {
            Scene scene;
            if (File.Exists(SectorScene)) scene = EditorSceneManager.OpenScene(SectorScene, OpenSceneMode.Single);
            else { scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorSceneManager.SaveScene(scene, SectorScene); }
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root); // the scene is owned by this tool
            var b = new Built();
            b.set = new GameObject("HangarSector").transform; b.set.SetPositionAndRotation(Origin, Quaternion.Euler(0, Yaw, 0));
            var lighting = HangarMenuAuthoring.BuildEnvironment(b.set, m);
            b.pod = HangarMenuAuthoring.BuildCapsule(b.set, m, out b.podFx);
            // v3: rigid arm segments, so the wrist roll turns the hand like a mechanism instead of twisting the arm skin
            RobotRigidSkin.Apply(b.podFx.model, log, m.trim);   // v30: + cut head/neck/hands and collars
            // v2: the canopy stays closed; the protagonist breaks it from the inside during the cinematic
            BuildShellAndColliders(b, m);
            HangarPitAuthoring.Build(b.set, m, log);   // v4: the void under the conveyor (before scrap and NavMesh)
            BuildLamps(b, m);
            BuildScrap(b, m, scrap);
            BuildGlass(b, m);
            BuildAmbientFx(b, m);
            var volume = new GameObject("HG_PostVolume").AddComponent<Volume>(); volume.transform.SetParent(b.set, false);
            volume.isGlobal = true; volume.priority = 20; volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(MenuVolume);
            HangarMenuAuthoring.ConfigureRenderSettings();
            Bake(b);
            BuildScavengers(b, m, scrap);
            var roomGo = new GameObject("RoomEncounter"); b.room = roomGo.AddComponent<CampaignRoom>(); b.room.ConfigurePrologue(PrologueTitle);
            BuildCinematic(b, m);
            // v2: the options/pause CCTV monitor shows the hangar from the menu's first camera view (hangar-local pose)
            IndustrialOptionsAuthoring.AddFeed(b.set, b.set.TransformPoint(new Vector3(0f, 1.45f, -3.1f)), b.set.rotation * Quaternion.Euler(-4f, -5f, 0f), 50f, Color.black);
            // the sector's own audio sources go to the template mixer
            var general = MixerGroup("General");
            foreach (var s in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AudioSource>(true))) if (!s.outputAudioMixerGroup) s.outputAudioMixerGroup = general;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var entries = EditorBuildSettings.scenes.ToList();
            if (!entries.Any(e => e.path == SectorScene)) { entries.Add(new EditorBuildSettingsScene(SectorScene, true)); EditorBuildSettings.scenes = entries.ToArray(); log.AppendLine("build settings + " + SectorScene); }
            log.AppendLine("saved " + SectorScene);
        }

        static GameObject Solid(GameObject g)
        {
            if (!g.GetComponent<Collider>())
            {
                var mf = g.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh && mf.sharedMesh.name.StartsWith("Cylinder")) g.AddComponent<CapsuleCollider>(); else g.AddComponent<BoxCollider>();
            }
            g.layer = 0; return g;
        }

        static GameObject Blocker(Transform parent, string name, Vector3 localPos, Vector3 size)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = localPos;
            g.AddComponent<BoxCollider>().size = size; return g;
        }

        static void BuildShellAndColliders(Built b, HangarMenuAuthoring.Mats m)
        {
            var env = b.set.Find("Environment");
            string[] solids = { "WallLeft", "WallRight", "CrateA", "CrateB", "CrateC", "Catwalk", "PipeRun", "Pipe0", "Pipe1", "Pipe2", "Pipe3" };
            foreach (var t in b.set.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                if (solids.Contains(n) || n.StartsWith("ColumnL") || n.StartsWith("ColumnR") || n == "PodBase" || n == "PodStrut" || n == "Pillar") Solid(t.gameObject);
            }
            var floor = env.Find("FloorCollider"); floor.gameObject.layer = 0;
            floor.localPosition = new Vector3(0, -.25f, 10); floor.GetComponent<BoxCollider>().size = new Vector3(HallHalf * 2, .5f, 70);
            var deck = b.set.Find("Bridge/DeckCollider"); if (deck) deck.gameObject.layer = 0;
            // capsule body (tilted with the pod)
            var podCol = b.pod.gameObject.AddComponent<BoxCollider>(); podCol.center = new Vector3(0, 0, .25f); podCol.size = new Vector3(1.95f, 3.5f, .9f);
            // front wall with the airlock opening onto the lobby
            var shell = HangarMenuAuthoring.Group("SectorShell", b.set);
            float side = HallHalf - DoorHalf;
            Solid(HangarMenuAuthoring.Box("FrontWallL", shell, new Vector3(-(DoorHalf + side * .5f), HallHeight * .5f, FrontZ), new Vector3(side, HallHeight, 1), m.plateDark, null, false));
            Solid(HangarMenuAuthoring.Box("FrontWallR", shell, new Vector3(DoorHalf + side * .5f, HallHeight * .5f, FrontZ), new Vector3(side, HallHeight, 1), m.plateDark, null, false));
            Solid(HangarMenuAuthoring.Box("FrontWallTop", shell, new Vector3(0, (HallHeight + DoorHeight) * .5f, FrontZ), new Vector3(DoorHalf * 2, HallHeight - DoorHeight, 1), m.plateDark, null, false));
            foreach (var s in new[] { -1, 1 }) HangarMenuAuthoring.Box("ExitPost", shell, new Vector3(s * (DoorHalf + .15f), DoorHeight * .5f, FrontZ + .55f), new Vector3(.3f, DoorHeight, .3f), m.hazard, null, false);
            HangarMenuAuthoring.Box("ExitLintel", shell, new Vector3(0, DoorHeight + .15f, FrontZ + .55f), new Vector3(DoorHalf * 2 + .6f, .3f, .3f), m.hazard, null, false);
            var sign = new GameObject("ExitSign"); sign.transform.SetParent(shell, false); sign.transform.localPosition = new Vector3(0, DoorHeight + 1.0f, FrontZ + .56f);
            var tmp = sign.AddComponent<TextMeshPro>(); tmp.font = TMP_Settings.defaultFontAsset; tmp.text = "TRÁNSITO C-04  ·  PUENTE NORTE"; tmp.fontSize = 6; tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, .62f, .25f); tmp.rectTransform.sizeDelta = new Vector2(10, 1.2f);
            // text faces the hangar interior (+Z local)
            sign.transform.localRotation = Quaternion.Euler(0, 180, 0);
            // invisible bounds: no ceiling in the menu art, an open back towards the city and a reachable bridge deck
            Blocker(shell, "BackBlocker", new Vector3(0, HallHeight * .5f, 30), new Vector3(HallHalf * 2, HallHeight, 1));
            Blocker(shell, "CeilingBlocker", new Vector3(0, 16.8f, 10), new Vector3(HallHalf * 2, .5f, 70));
            log.AppendLine("colliders: " + b.set.GetComponentsInChildren<Collider>(true).Length);
        }

        static void BuildLamps(Built b, HangarMenuAuthoring.Mats m)
        {
            var root = HangarMenuAuthoring.Group("SectorLamps", b.set);
            var spots = new[] { new Vector3(-8.5f, 6.4f, -13f), new Vector3(8f, 6.6f, -14.5f), new Vector3(-.5f, 6.8f, -6f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var lamp = HangarMenuAuthoring.Group("WorkLamp" + i, root); lamp.localPosition = spots[i];
                HangarMenuAuthoring.Box("Housing", lamp, new Vector3(0, .08f, 0), new Vector3(2.2f, .14f, .4f), m.trim, null, false);
                var tube = HangarMenuAuthoring.Box("Tube", lamp, Vector3.zero, new Vector3(2f, .06f, .1f), m.lampWarm, null, false).GetComponent<Renderer>();
                HangarMenuAuthoring.Box("Cable", lamp, new Vector3(-.9f, 4.9f, 0), new Vector3(.02f, 9.8f, .02f), m.trim, null, false);
                HangarMenuAuthoring.Box("CableB", lamp, new Vector3(.9f, 4.9f, 0), new Vector3(.02f, 9.8f, .02f), m.trim, null, false);
                var spot = HangarMenuAuthoring.MakeLight("Down", lamp, LightType.Spot, new Vector3(0, -.05f, 0), new Vector3(90, 0, 0), new Color(1f, .82f, .62f), i == 2 ? 10f : 20f, 15f, 120f);
                HangarMenuAuthoring.Flicker(lamp.gameObject, new[] { spot }, new[] { tube }, new Color(2f, 1.7f, 1.25f), i == 1 ? MenuFlicker.Mode.Failing : MenuFlicker.Mode.Hum, .35f, 70 + i);
            }
            var exitLamp = HangarMenuAuthoring.MakeLight("ExitGlow", root, LightType.Point, new Vector3(0, DoorHeight + 1.2f, FrontZ + 1.4f), Vector3.zero, new Color(1f, .5f, .2f), 3f, 9f);
            HangarMenuAuthoring.Flicker(exitLamp.gameObject, new[] { exitLamp }, new Renderer[0], Color.black, MenuFlicker.Mode.Hum, 0, 81);
        }

        static void BuildScrap(Built b, HangarMenuAuthoring.Mats m, GameObject[] scrap)
        {
            var root = HangarMenuAuthoring.Group("ScrapZones", b.set);
            var rnd = new System.Random(1204);
            float R(float a, float c) => a + (float)rnd.NextDouble() * (c - a);
            var pointsRoot = HangarMenuAuthoring.Group("ScrapPoints", b.set);
            // v3: the menu's placeholder crates (black cubes) are replaced by a scrap pile (index 17)
            foreach (var t in b.set.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Crate")).ToArray()) Object.DestroyImmediate(t.gameObject);
            int pieces = 0, skippedOverPit = 0;
            for (int i = 0; i < Piles.Length; i++)
            {
                var d = Piles[i]; var c = new Vector3(d.x, 0, d.z); float r = d.r; var lean = new Vector3(d.lx, 0, d.lz);
                if (HangarPitAuthoring.OverPit(c, r * 1.3f)) { skippedOverPit++; continue; }   // v4: nothing floats over the void
                var pile = HangarMenuAuthoring.Group("ScrapPile" + i, root); pile.localPosition = c;
                pile.localRotation = lean == Vector3.zero ? Quaternion.Euler(0, R(0, 360), 0) : Quaternion.identity;
                pieces += HangarScrapAuthoring.BuildPile(pile, r, d.h, lean, rnd, scrap, i);
                // simple colliders: a wide base and a narrower upper block shifted towards the support
                var baseCol = pile.gameObject.AddComponent<BoxCollider>(); baseCol.center = new Vector3(0, d.h * .25f, 0); baseCol.size = new Vector3(r * 2.6f, d.h * .5f, r * 2.6f);
                var upper = pile.gameObject.AddComponent<BoxCollider>(); var ln = lean == Vector3.zero ? Vector3.zero : lean.normalized;
                upper.center = ln * r * .35f + Vector3.up * d.h * .65f; upper.size = new Vector3(r * 1.7f, d.h * .5f, r * 1.7f);
                // intermittent sparks from torn cables
                HangarMenuAuthoring.PS("CableSparks", pile, m.fxSpark, ps =>
                {
                    ps.transform.localPosition = new Vector3(0, d.h * .45f, 0);
                    var main = ps.main; main.loop = true; main.playOnAwake = true; main.duration = R(2.5f, 4.5f); main.startLifetime = new ParticleSystem.MinMaxCurve(.2f, .6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f); main.startSize = new ParticleSystem.MinMaxCurve(.015f, .04f); main.gravityModifier = 1.4f; main.maxParticles = 40;
                    main.simulationSpace = ParticleSystemSimulationSpace.World; main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .85f, .5f), new Color(1f, .5f, .15f));
                    var e = ps.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(R(.2f, 1.5f), (short)6, (short)14, 2, .07f) { probability = .7f } });
                    var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = .1f; sh.rotation = new Vector3(-90, 0, 0);
                    var rr = ps.GetComponent<ParticleSystemRenderer>(); rr.renderMode = ParticleSystemRenderMode.Stretch; rr.velocityScale = .05f; rr.lengthScale = 1.1f;
                    var cl = ps.colorOverLifetime; cl.enabled = true; cl.color = HangarMenuAuthoring.Fade(Color.white);
                });
                if (i == 0 || i == 3 || i == 5)
                    HangarMenuAuthoring.PS("Steam", pile, m.fxSmoke, ps =>
                    {
                        ps.transform.localPosition = new Vector3(.3f, .6f, .2f); ps.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                        var main = ps.main; main.loop = true; main.playOnAwake = true; main.prewarm = true; main.duration = 5; main.startLifetime = new ParticleSystem.MinMaxCurve(3, 5);
                        main.startSpeed = new ParticleSystem.MinMaxCurve(.3f, .6f); main.startSize = new ParticleSystem.MinMaxCurve(.5f, 1.1f); main.maxParticles = 40;
                        main.startColor = new Color(.6f, .66f, .72f, .12f); main.simulationSpace = ParticleSystemSimulationSpace.World;
                        var e = ps.emission; e.rateOverTime = 4;
                        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14; sh.radius = .1f;
                        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .5f, 1, 2.4f));
                        var cl = ps.colorOverLifetime; cl.enabled = true; cl.color = HangarMenuAuthoring.Fade(Color.white, .8f);
                    });
                if (i >= WorkedPiles) continue;
                // two scrap points per worked pile, facing the pile, on the side towards the hall centre (snapped after the bake)
                float away = Mathf.Sign(c.x) * -1;
                foreach (var ang in new[] { -35f, 40f })
                {
                    var dir = Quaternion.Euler(0, ang, 0) * new Vector3(away, 0, 0);
                    var p = new GameObject("ScrapPoint" + b.points.Count).transform; p.SetParent(pointsRoot, false);
                    p.localPosition = c + dir * (r * 1.4f + .7f); p.localRotation = Quaternion.LookRotation(-dir);
                    b.points.Add(p);
                }
            }
            // loose debris on the floor between the piles, off the walking routes (main aisle, capsule, door, scrap points)
            var debrisRoot = HangarMenuAuthoring.Group("FloorDebris", root); int debris = 0;
            for (int tries = 0; tries < 600 && debris < 45; tries++)
            {
                var p = new Vector3(R(-15.5f, 15.5f), 0, R(-23.5f, 30f));
                if (HangarPitAuthoring.OverPit(p, .9f)) continue;                                   // v4: the void
                if (Mathf.Abs(p.x) < 4.8f && p.z < 2f) continue;                                   // aisle capsule -> airlock
                if (Mathf.Abs(p.x) < 6.5f && p.z < -20.5f) continue;                               // in front of the airlock
                if (Vector3.Distance(p, new Vector3(1.75f, 0, .6f)) < 3.6f) continue;              // capsule and stand point
                if (Piles.Any(q => !HangarPitAuthoring.OverPit(new Vector3(q.x, 0, q.z), q.r * 1.3f) && Vector3.Distance(p, new Vector3(q.x, 0, q.z)) < q.r * 1.7f)) continue;
                if (b.points.Any(q => Vector3.Distance(p, q.localPosition) < 1.1f)) continue;
                HangarScrapAuthoring.Debris(debrisRoot, scrap, rnd, p, debris++);
            }
            // there must be no placeholder cube left in the yard
            int placeholders = b.set.GetComponentsInChildren<MeshFilter>(true).Count(f => f.sharedMesh && f.sharedMesh.name == "Cube" && (f.name.StartsWith("Crate") || f.name == "Heap" || f.name == "Plate" || f.transform.IsChildOf(root)));
            log.AppendLine($"scrap yard: piles={Piles.Length - skippedOverPit} (skipped over pit {skippedOverPit}) wreck pieces={pieces} floor debris={debris} placeholder cubes left={placeholders}");
            foreach (var z in new[] { new Vector3(-8.6f, 1.4f, -13.8f), new Vector3(7.8f, 1.3f, -14.6f) })
            {
                var l = HangarMenuAuthoring.MakeLight("ScrapGlow", root, LightType.Point, z, Vector3.zero, new Color(1f, .55f, .25f), 4f, 9f);
                HangarMenuAuthoring.Flicker(l.gameObject, new[] { l }, new Renderer[0], Color.black, MenuFlicker.Mode.Failing, .25f, (int)(z.x * 10));
            }
            var lookAt = new GameObject("CinematicLookTarget").transform; lookAt.SetParent(b.set, false); lookAt.localPosition = new Vector3(-8.5f, 1.2f, -13.5f); b.look = lookAt;
            var belt = new GameObject("CinematicBeltTarget").transform; belt.SetParent(b.set, false); belt.localPosition = new Vector3(2f, 6.2f, 19f); b.belt = belt;
            log.AppendLine("scrap piles=" + Piles.Length + " points=" + b.points.Count + " models=" + scrap.Length);
        }

        // ------------------------------------------------------------------ capsule glass (broken in the cinematic)
        static void BuildGlass(Built b, HangarMenuAuthoring.Mats m)
        {
            var pod = b.pod; var canopy = b.podFx.canopyRenderer;
            if (!canopy) { log.AppendLine("WARN no canopy"); return; }
            canopy.gameObject.SetActive(true); canopy.enabled = true;
            var root = HangarMenuAuthoring.Group("CanopyBreak", pod);
            var impact = new GameObject("GlassImpact").transform; impact.SetParent(pod, false); impact.localPosition = new Vector3(.08f, .45f, -.46f); b.impact = impact;
            // crack decal on the outer face of the glass
            var crackPath = LevelArt + "/Textures/Room00_GlassCrack.png";
            if (AssetImporter.GetAtPath(crackPath) is TextureImporter cti && (!cti.alphaIsTransparency || cti.wrapMode != TextureWrapMode.Clamp))
            { cti.alphaIsTransparency = true; cti.wrapMode = TextureWrapMode.Clamp; cti.maxTextureSize = 512; cti.SaveAndReimport(); }
            var crackTex = AssetDatabase.LoadAssetAtPath<Texture2D>(crackPath);
            var crackMat = HangarMenuAuthoring.ParticleMat("HG_GlassCrack", crackTex, new Color(.85f, .95f, 1f, .95f), false);
            var crack = HangarMenuAuthoring.Prim(PrimitiveType.Quad, "Crack", pod, new Vector3(.08f, .45f, -.495f), new Vector3(.75f, .75f, 1), crackMat, Vector3.zero, false);
            // shards: four procedural triangle-prism meshes, 22 pieces spread over the front of the canopy
            var shardMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Menu/Materials/HG_GlassShard.mat");
            if (!shardMat) { shardMat = HangarMenuAuthoring.Lit("HG_GlassShard", new Color(.6f, .82f, .9f, .32f), 0, .92f); HangarMenuAuthoring.Transparent(shardMat); }
            var meshes = new Mesh[4];
            for (int k = 0; k < meshes.Length; k++) meshes[k] = ShardMesh(k);
            var rnd = new System.Random(77);
            float R(float a, float c) => a + (float)rnd.NextDouble() * (c - a);
            var shards = new List<Rigidbody>(); var podCol = pod.GetComponent<Collider>();
            for (int i = 0; i < 22; i++)
            {
                float x = R(-.55f, .55f), y = R(-1.0f, 1.15f);
                float z = -.06f - .42f * Mathf.Sqrt(Mathf.Max(0, 1 - (x / .725f) * (x / .725f)));
                var go = new GameObject("Shard" + i, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(Rigidbody));
                go.transform.SetParent(root, false); go.transform.localPosition = new Vector3(x, y, z); go.transform.localRotation = Quaternion.Euler(R(-20, 20), R(-30, 30), R(0, 360));
                float sc = R(.6f, 1.25f); go.transform.localScale = Vector3.one * sc; go.layer = 2;
                var mesh = meshes[i % meshes.Length]; go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = shardMat; mr.shadowCastingMode = ShadowCastingMode.Off;
                var bc = go.GetComponent<BoxCollider>(); bc.center = mesh.bounds.center; bc.size = Vector3.Max(mesh.bounds.size, new Vector3(.02f, .02f, .02f));
                var rb = go.GetComponent<Rigidbody>(); rb.mass = .15f; rb.linearDamping = .1f; rb.angularDamping = .4f; rb.interpolation = RigidbodyInterpolation.Interpolate; rb.isKinematic = true;
                shards.Add(rb);
            }
            ParticleSystem Fx(string name, Material mat, Action<ParticleSystem> setup) => HangarMenuAuthoring.PS(name, root, mat, setup);
            var dust = Fx("GlassDust", m.fxDust, ps =>
            {
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.8f, 1.8f); main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 2.2f); main.startSize = new ParticleSystem.MinMaxCurve(.12f, .4f);
                main.startColor = new Color(.75f, .8f, .85f, .4f); main.gravityModifier = -.02f; main.maxParticles = 80; main.simulationSpace = ParticleSystemSimulationSpace.World;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .25f;
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, 2.2f));
                var cl = ps.colorOverLifetime; cl.enabled = true; cl.color = HangarMenuAuthoring.Fade(Color.white, .8f);
            });
            var sparks = Fx("GlassSparks", m.fxSpark, ps =>
            {
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.2f, .55f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f); main.startSize = new ParticleSystem.MinMaxCurve(.012f, .03f);
                main.gravityModifier = 1.4f; main.maxParticles = 60; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startColor = new ParticleSystem.MinMaxGradient(new Color(.7f, .95f, 1f), new Color(1f, .8f, .5f));
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .08f;
                var rr = ps.GetComponent<ParticleSystemRenderer>(); rr.renderMode = ParticleSystemRenderMode.Stretch; rr.velocityScale = .05f; rr.lengthScale = 1.1f;
                var cl = ps.colorOverLifetime; cl.enabled = true; cl.color = HangarMenuAuthoring.Fade(Color.white);
            });
            var audioGo = new GameObject("GlassAudio"); audioGo.transform.SetParent(root, false); audioGo.transform.position = impact.position;
            var audio = audioGo.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = .5f; audio.minDistance = 3; audio.maxDistance = 30; audio.outputAudioMixerGroup = MixerGroup("Impact");
            var glass = root.gameObject.AddComponent<CapsuleGlassBreak>();
            glass.Configure(canopy, crack.GetComponent<Renderer>(), shards.ToArray(), impact, dust, sparks, audio, Clip("IR_Hangar_GlassCrack"), Clip("IR_Hangar_GlassShatter"));
            glass.ConfigureIgnore(podCol ? new[] { podCol } : new Collider[0]);
            b.glass = glass;
            log.AppendLine("glass shards=" + shards.Count);
        }

        static Mesh ShardMesh(int k)
        {
            var path = LevelArt + "/Room00_GlassShard" + k + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.name = "Room00_GlassShard" + k;
            var r = new System.Random(300 + k); float R(float a, float c) => a + (float)r.NextDouble() * (c - a);
            // a thin irregular triangle (sometimes a quad) prism, 1 cm thick, 8-20 cm wide
            int n = k % 2 == 0 ? 3 : 4; var outline = new Vector2[n];
            for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2 + R(-.4f, .4f); float rad = R(.05f, .11f); outline[i] = new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad); }
            var v = new List<Vector3>(); var tris = new List<int>(); float h = .005f;
            for (int i = 0; i < n; i++) v.Add(new Vector3(outline[i].x, outline[i].y, -h));
            for (int i = 0; i < n; i++) v.Add(new Vector3(outline[i].x, outline[i].y, h));
            for (int i = 1; i < n - 1; i++) { tris.Add(0); tris.Add(i); tris.Add(i + 1); tris.Add(n); tris.Add(n + i + 1); tris.Add(n + i); }
            for (int i = 0; i < n; i++) { int j = (i + 1) % n; tris.Add(i); tris.Add(n + i); tris.Add(j); tris.Add(j); tris.Add(n + i); tris.Add(n + j); }
            mesh.SetVertices(v); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static void BuildAmbientFx(Built b, HangarMenuAuthoring.Mats m)
        {
            var root = HangarMenuAuthoring.Group("SectorFx", b.set);
            HangarMenuAuthoring.PS("Dust", root, m.fxDust, ps =>
            {
                ps.transform.localPosition = new Vector3(0, 2.4f, -6);
                var main = ps.main; main.loop = true; main.playOnAwake = true; main.prewarm = true; main.duration = 10; main.startLifetime = 14; main.startSpeed = .03f;
                main.startSize = new ParticleSystem.MinMaxCurve(.01f, .035f); main.maxParticles = 500; main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startColor = new Color(.85f, .9f, 1f, .45f);
                var e = ps.emission; e.rateOverTime = 34;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(26, 4.5f, 30);
                var n = ps.noise; n.enabled = true; n.strength = .08f; n.frequency = .2f; n.scrollSpeed = .05f;
                var c = ps.colorOverLifetime; c.enabled = true; c.color = HangarMenuAuthoring.Fade(Color.white, .9f);
            });
        }

        static void Bake(Built b)
        {
            Physics.SyncTransforms();
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = NavRadius; settings.agentHeight = NavHeight; settings.agentClimb = .3f; settings.agentSlope = 35; settings.overrideVoxelSize = true; settings.voxelSize = .1f;
            var sources = new List<NavMeshBuildSource>();
            // scrap piles stay out of the bake so scavengers can stand right at their edge (the player still collides)
            // v2: scrap piles carve the NavMesh with their single box collider (the wreck models carry no colliders)
            var markups = new List<NavMeshBuildMarkup>();
            var shell = b.set.Find("SectorShell"); markups.Add(new NavMeshBuildMarkup { root = shell.Find("CeilingBlocker"), ignoreFromBuild = true, applyToChildren = true });
            NavMeshBuilder.CollectSources(b.set, -1, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            var bounds = new Bounds(new Vector3(0, 1.5f, 3), new Vector3(HallHalf * 2, 5, 56)); // local: floor + crates, below the bridge deck
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, b.set.position, b.set.rotation);
            if (!data) throw new Exception("NavMesh bake failed");
            data.name = SectorName + "_NavMesh";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
            if (existing) { EditorUtility.CopySerialized(data, existing); data = existing; EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(data, NavPath);
            var surface = b.set.gameObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.agentTypeID = 0; surface.collectObjects = Unity.AI.Navigation.CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.navMeshData = data; surface.AddData();
            int ok = 0; foreach (var p in b.points) if (NavMesh.SamplePosition(p.position, out var hit, 1.5f, NavMesh.AllAreas)) { p.position = hit.position; ok++; } else log.AppendLine("WARN scrap point off the NavMesh: " + p.name + " local " + b.set.InverseTransformPoint(p.position));
            log.AppendLine($"navmesh sources={sources.Count} points on mesh {ok}/{b.points.Count}");
        }

        // ------------------------------------------------------------------ scavengers
        static void BuildScavengers(Built b, HangarMenuAuthoring.Mats m, GameObject[] scrap)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarroneroPrefab);
            if (!prefab) throw new Exception("Missing " + CarroneroPrefab);
            var root = new GameObject("Scavengers").transform; b.scav = root;
            var pieces = HangarMenuAuthoring.Group("ScrapPieces", root);
            var group = root.gameObject.AddComponent<ScavengerGroup>();
            var bots = new List<ScavengerBehaviour>();
            var rummage = new[] { Clip("IR_Hangar_Rummage1"), Clip("IR_Hangar_Rummage2"), Clip("IR_Hangar_Rummage3") }.Where(c => c).ToArray();
            var voiceGroup = MixerGroup("EnemyMovement");
            for (int i = 0; i < Scavengers; i++)
            {
                var point = b.points[(i * 2) % b.points.Count];
                var bot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                bot.name = "Carronero_Scavenger" + i; bot.transform.SetParent(root, true);
                var start = point.position - point.forward * (.2f + .15f * i);
                bot.transform.SetPositionAndRotation(start, Quaternion.LookRotation(point.forward) * Quaternion.Euler(0, (i % 2 == 0 ? 1 : -1) * 15f * i, 0));
                var motion = bot.GetComponent<CarroneroMotion>();
                var arm = motion ? new SerializedObject(motion).FindProperty("otherArm").objectReferenceValue as Transform : null;
                var hand = arm; while (hand && hand.childCount > 0) hand = hand.GetChild(0);
                var socket = new GameObject("CarrySocket").transform; socket.SetParent(hand ? hand : bot.transform, false); socket.localPosition = Vector3.zero;
                var voiceGo = new GameObject("ScavengerVoice"); voiceGo.transform.SetParent(bot.transform, false); voiceGo.transform.localPosition = new Vector3(0, .8f, .3f);
                var voice = voiceGo.AddComponent<AudioSource>(); voice.playOnAwake = false; voice.spatialBlend = 1; voice.rolloffMode = AudioRolloffMode.Linear; voice.minDistance = 2; voice.maxDistance = 26; voice.dopplerLevel = 0; voice.outputAudioMixerGroup = voiceGroup;
                var sparks = HangarMenuAuthoring.PS("RummageSparks", socket, m.fxSpark, ps =>
                {
                    var main = ps.main; main.loop = false; main.startLifetime = new ParticleSystem.MinMaxCurve(.15f, .45f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(.012f, .03f); main.gravityModifier = 1.5f; main.maxParticles = 50; main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .85f, .5f), new Color(1f, .5f, .15f)); main.scalingMode = ParticleSystemScalingMode.Shape;
                    var rr = ps.GetComponent<ParticleSystemRenderer>(); rr.renderMode = ParticleSystemRenderMode.Stretch; rr.velocityScale = .05f; rr.lengthScale = 1.1f;
                    var cl = ps.colorOverLifetime; cl.enabled = true; cl.color = HangarMenuAuthoring.Fade(Color.white);
                });
                var rb = HangarScrapAuthoring.ThrowPiece(pieces, scrap, i);
                var sc = bot.AddComponent<ScavengerBehaviour>();
                sc.Configure(group, motion, socket, rb, sparks, voice, rummage, Clip("IR_Hangar_Tear"), Clip("IR_Hangar_Clatter"), 17 + i * 31);
                bots.Add(sc);
                if (!NavMesh.SamplePosition(start, out _, .8f, NavMesh.AllAreas)) log.AppendLine("WARN scavenger off mesh " + start);
            }
            group.Configure(b.points.ToArray(), bots.ToArray(), 14f);
            log.AppendLine("scavengers=" + bots.Count + " (prefab " + CarroneroPrefab + ")");
        }

        // ------------------------------------------------------------------ cinematic
        static Transform Anchor(Transform parent, string name, Vector3 pos, Vector3 lookAt)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false);
            t.SetPositionAndRotation(pos, Quaternion.LookRotation(lookAt - pos, Vector3.up)); return t;
        }

        static RectTransform UiRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; return r;
        }

        static void BuildCinematic(Built b, HangarMenuAuthoring.Mats m)
        {
            var pod = b.pod; var up = Vector3.up;
            var face = Vector3.ProjectOnPlane(-pod.forward, up).normalized; var right = Vector3.Cross(up, face);
            var foot = pod.TransformPoint(0, -1.66f, -.5f);
            var S = foot + face * .55f; S.y = 0;   // v30: one long step down from the capsule lip (was .75)
            var rig = new GameObject("WakeUpCinematic").transform;
            var stand = new GameObject("StandPoint").transform; stand.SetParent(rig, false); stand.SetPositionAndRotation(S, Quaternion.LookRotation(face));
            var grip = new GameObject("RailGrip").transform; grip.SetParent(pod, false); grip.localPosition = new Vector3(.84f, -.55f, -.42f);   // v30: lower on the rail, within reach while stepping out
            b.stand = stand; b.grip = grip;
            // pose driver: the robot leaves the capsule hierarchy at runtime
            var pose = rig.gameObject.AddComponent<WakeUpRobotPose>();
            pose.Configure(b.podFx.model, pod, grip, stand, b.look);
            pose.ConfigureActing(b.belt, b.impact);
            // visor light rides on the head bone; a cyan glow on the cannon forearm
            Transform Find(string bone) => b.podFx.model ? b.podFx.model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith(bone)) : null;
            var head = Find(":Head") ?? Find("Head"); var fore = Find(":RightForeArm") ?? Find("RightForeArm");
            if (b.podFx.visor && head) b.podFx.visor.transform.SetParent(head, true);
            Light cannon = null;
            if (fore) { cannon = HangarMenuAuthoring.MakeLight("CannonGlow", fore, LightType.Point, Vector3.zero, Vector3.zero, new Color(.3f, .9f, 1f), 0, 1.6f); }
            log.AppendLine("rig bones head=" + (head ? head.name : "none") + " forearm=" + (fore ? fore.name : "none"));
            // FX
            var fxRoot = HangarMenuAuthoring.Group("CinematicFx", rig);
            var dust = HangarMenuAuthoring.PS("FootDust", fxRoot, m.fxDust, ps =>
            {
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.8f, 1.6f); main.startSpeed = new ParticleSystem.MinMaxCurve(.4f, 1.6f); main.startSize = new ParticleSystem.MinMaxCurve(.15f, .45f);
                main.startColor = new Color(.7f, .72f, .75f, .35f); main.gravityModifier = -.02f; main.maxParticles = 80; main.simulationSpace = ParticleSystemSimulationSpace.World;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = .25f; sh.rotation = new Vector3(-90, 0, 0);
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, 2f));
                var cl = ps.colorOverLifetime; cl.enabled = true; cl.color = HangarMenuAuthoring.Fade(Color.white, .8f);
            });
            ParticleSystem Sparks(string name, Transform parent) => HangarMenuAuthoring.PS(name, parent, m.fxSpark, ps =>
            {
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.2f, .6f); main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f); main.startSize = new ParticleSystem.MinMaxCurve(.015f, .04f);
                main.gravityModifier = 1.5f; main.maxParticles = 60; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .85f, .5f), new Color(.5f, .9f, 1f));
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = .08f; sh.rotation = new Vector3(-90, 0, 0);
                var rr = ps.GetComponent<ParticleSystemRenderer>(); rr.renderMode = ParticleSystemRenderMode.Stretch; rr.velocityScale = .05f; rr.lengthScale = 1.1f;
                var cl = ps.colorOverLifetime; cl.enabled = true; cl.color = HangarMenuAuthoring.Fade(Color.white);
            });
            var footSparks = Sparks("FootSparks", fxRoot); var cannonSparks = Sparks("CannonSparks", fxRoot);

            // camera + shots
            var camGo = new GameObject("CinematicCamera", typeof(Camera), typeof(AudioListener)); camGo.transform.SetParent(rig, false);
            var cam = camGo.GetComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.1f, .13f, .17f, 1); cam.fieldOfView = 40; cam.nearClipPlane = .03f; cam.farClipPlane = 260; cam.depth = 5;
            var data = cam.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true; data.stopNaN = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; data.antialiasingQuality = AntialiasingQuality.High;
            cam.enabled = false; var listener = camGo.GetComponent<AudioListener>(); listener.enabled = false;
            var brain = camGo.AddComponent<CinemachineBrain>(); brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
            var shotsRoot = HangarMenuAuthoring.Group("Shots", rig);
            var chest = pod.TransformPoint(0, .55f, -.2f) + face * .42f + right * .1f;
            var lookA = pod.TransformPoint(0, .9f, -.12f);
            // v2 shot list: glass punches, the hand study and the conveyor look were added; times match WakeUpRobotPose.Timing
            var podFront = -pod.forward; var hit = b.impact ? b.impact.position : pod.TransformPoint(.08f, .45f, -.46f);
            var headPos = S + up * 1.95f;
            var beltPoint = b.belt ? b.belt.position : S - face * 20 + up * 6;
            var T = pose.Times;
            float revealEnd = T.turnBackEnd + 1.6f, headStart = revealEnd - .6f;
            // v30: the servo close-up is cut; the visor shot goes straight to the fist driving into the glass, then the
            // capsule interior opens (restraints, cables, cradle) and the side shot shows the leg going over the lip
            float glassCut = T.punch1 - .4f;
            var podMid = pod.TransformPoint(0, -.1f, -.25f);
            // v4: no shot position may sit inside the capsule shell: the pod's body collider (tilted box) + a camera
            // margin; a point too close is pushed out toward the hangar.
            var podShell = pod.GetComponent<BoxCollider>(); bool podAny = podShell; Physics.SyncTransforms();
            const float CamMargin = .35f;
            bool NearPod(Vector3 p) => podAny && Vector3.Distance(podShell.ClosestPoint(p), p) < CamMargin;
            Vector3 OutOfPod(Vector3 p) { for (int i = 0; i < 30 && NearPod(p); i++) p += face * .1f; return p; }
            var defs = new (string name, float start, float end, float fov, Vector3 from, Vector3 fromLook, Vector3 to, Vector3 toLook, bool head)[]
            {
                ("A_Visor", 0f, glassCut, 38f, pod.TransformPoint(0, .98f, -1.05f), lookA, pod.TransformPoint(0, .96f, -.86f), lookA, false),
                ("B2_Glass", glassCut, T.releaseAt, 42f, hit + podFront * 1.75f - right * .5f - up * .2f, hit + up * .08f, hit + podFront * 1.5f - right * .35f - up * .15f, hit + up * .1f, false),
                ("B3_Release", T.releaseAt, T.cradleEnd + .1f, 44f, podMid + face * 2.5f + right * 1.0f + up * .45f, podMid + up * .1f, podMid + face * 2.2f + right * .8f + up * .4f, podMid + up * .12f, false),
                ("C_Exit", T.cradleEnd + .1f, T.stand + .4f, 46f, S + face * 2.3f + right * 2.7f + up * 1.2f, S + up * .75f - face * .55f, S + face * 2.1f + right * 2.3f + up * 1.2f, S + up * .95f - face * .35f, false),
                ("D_Hand", T.stand + .4f, T.handEnd, 38f, S + face * 1.7f + right * .75f + up * 1.85f, headPos + right * .1f - up * .1f, S + face * 1.45f + right * .6f + up * 1.9f, headPos + right * .08f - up * .12f, false),
                ("D2_Belt", T.handEnd, T.turnBackStart, 46f, S + face * 3.2f - right * 1.3f + up * 1.4f, Vector3.Lerp(S + up * 1.6f, beltPoint, .3f), S + face * 3.0f - right * 1.8f + up * 1.5f, Vector3.Lerp(S + up * 1.6f, beltPoint, .35f), false),
                // v4: short pull-back over the right shoulder, kept in front of the capsule (was 2.0-2.6 m back, through the pod)
                ("E_Reveal", T.turnBackStart, revealEnd, 36f, OutOfPod(S - face * .15f + right * 1.0f + up * 2.05f), S + face * 13f + up * .6f, OutOfPod(S - face * .4f + right * 1.05f + up * 2.15f), S + face * 13.5f + up * .5f, false),
                ("F_ToHead", headStart, CinematicSeconds, 60f, OutOfPod(S - face * .4f + right * .95f + up * 2.15f), S + face * 4f + up * 1.4f, Vector3.zero, Vector3.zero, true),
            };
            var paths = new List<HangarIntroCinematic.ShotPath>(); var vcams = new List<CinemachineCamera>();
            foreach (var d in defs)
            {
                var vgo = new GameObject("Shot_" + d.name); vgo.transform.SetParent(shotsRoot, false);
                var vcam = vgo.AddComponent<CinemachineCamera>(); var lens = vcam.Lens; lens.FieldOfView = d.fov; lens.NearClipPlane = .03f; lens.FarClipPlane = 260; vcam.Lens = lens;
                var from = Anchor(shotsRoot, d.name + "_From", d.from, d.fromLook);
                var to = d.head ? null : Anchor(shotsRoot, d.name + "_To", d.to, d.toLook);
                vgo.transform.SetPositionAndRotation(from.position, from.rotation);
                paths.Add(new HangarIntroCinematic.ShotPath { camera = vgo.transform, from = from, to = to, start = d.start, end = d.end, endAtHead = d.head });
                vcams.Add(vcam);
            }
            {
                // v4 clearance report: sample the reveal and hand-off paths against the capsule bounds.
                int inside = 0; var eye = headPos + face * .12f;
                foreach (var d in defs)
                {
                    if (d.name != "E_Reveal" && d.name != "F_ToHead") continue;
                    var end = d.head ? eye : d.to;
                    for (int i = 0; i <= 20; i++) { var q = Vector3.Lerp(d.from, end, i / 20f); if (podAny && Vector3.Distance(podShell.ClosestPoint(q), q) < .12f) inside++; }
                }
                log.AppendLine("v4 end camera samples inside/touching the capsule shell=" + inside + " (E_Reveal + F_ToHead, 42 samples)");
            }
            // timeline: Cinemachine shots + sound cues
            var director = rig.gameObject.AddComponent<PlayableDirector>(); director.playOnAwake = false; director.timeUpdateMode = DirectorUpdateMode.GameTime; director.extrapolationMode = DirectorWrapMode.Hold;
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (!timeline) { timeline = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(timeline, TimelinePath); }
            foreach (var track in timeline.GetRootTracks().ToArray()) timeline.DeleteTrack(track);
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration = CinematicSeconds;
            var camTrack = timeline.CreateTrack<CinemachineTrack>(null, "Shots");
            for (int i = 0; i < defs.Length; i++)
            {
                var clip = camTrack.CreateClip<CinemachineShot>(); clip.start = defs[i].start; clip.duration = defs[i].end - defs[i].start; clip.displayName = defs[i].name;
                var shot = (CinemachineShot)clip.asset; shot.VirtualCamera.exposedName = UnityEngine.GUID.Generate().ToString();
                director.SetReferenceValue(shot.VirtualCamera.exposedName, vcams[i]);
            }
            var audioTrack = timeline.CreateTrack<AudioTrack>(null, "Sfx");
            // cues follow the pose beats (v3 timing): boot, arming, wind-ups, steps, crouch, hand study, turn re-plants
            // v30 cues: boot, punch drives, restraints, cables, cradle, steps (servo whine only: the metal footstep is
            // played by FootContactFx at the real contact), stand, hand study, conveyor look
            var cues = new (string clip, float at, float volume)[]
            {
                ("IR_Hangar_PowerUp", .7f, .9f), ("IR_Hangar_Servo", T.punch1 - .7f, .55f), ("IR_Hangar_Servo", T.punch2 - .45f, .5f),
                ("IR_Hangar_Hydraulic", T.releaseAt, .85f), ("IR_Hangar_Spark", T.cablePop, .45f), ("IR_Hangar_Servo", T.cradleStart, .6f), ("IR_Hangar_Hydraulic", T.cradleEnd - .15f, .6f),
                ("IR_Hangar_Hydraulic", T.leave - .1f, .7f), ("IR_Hangar_Servo", T.rightStepStart + .1f, .5f), ("IR_Hangar_Servo", T.leftStepStart + .1f, .45f), ("IR_Hangar_Hydraulic", T.stand - .3f, .75f),
                ("IR_Hangar_Servo", T.handStart + .3f, .45f), ("IR_Hangar_Servo", T.handStart + (T.handEnd - T.handStart) * .45f, .4f), ("IR_Hangar_Servo", T.beltLook + .4f, .5f), ("IR_Hangar_Servo", T.turnBackStart, .45f),
            };
            int placed = 0;
            foreach (var c in cues)
            {
                var ac = Clip(c.clip); if (!ac) { log.AppendLine("missing clip " + c.clip); continue; }
                var tc = audioTrack.CreateClip(ac); tc.start = c.at; tc.displayName = c.clip;
                if (tc.asset is AudioPlayableAsset apa) apa.loop = false;
                placed++;
            }
            EditorUtility.SetDirty(timeline);
            director.playableAsset = timeline;
            director.SetGenericBinding(camTrack, brain);
            var audioGo = new GameObject("CinematicAudio"); audioGo.transform.SetParent(rig, false);
            var audio = audioGo.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0; audio.outputAudioMixerGroup = MixerGroup("Player");
            director.SetGenericBinding(audioTrack, audio);
            log.AppendLine("timeline shots=" + defs.Length + " sounds=" + placed + " duration=" + CinematicSeconds);

            // screen: fade, letterbox, skip prompt
            var canvasGo = new GameObject("CinematicUI", typeof(Canvas), typeof(CanvasScaler)); canvasGo.transform.SetParent(rig, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 140;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            Image Black(RectTransform r) { var img = r.gameObject.AddComponent<Image>(); img.color = Color.black; img.raycastTarget = false; return img; }
            var top = UiRect("LetterboxTop", canvasGo.transform); Black(top);
            var bottom = UiRect("LetterboxBottom", canvasGo.transform); Black(bottom);
            var fadeRect = UiRect("Fade", canvasGo.transform); Black(fadeRect); var fade = fadeRect.gameObject.AddComponent<CanvasGroup>(); fade.blocksRaycasts = false; fade.alpha = 1;
            var prompt = UiRect("SkipPrompt", canvasGo.transform); prompt.anchorMin = prompt.anchorMax = prompt.pivot = new Vector2(1, 0); prompt.sizeDelta = new Vector2(520, 70); prompt.anchoredPosition = new Vector2(-48, 34);
            var promptGroup = prompt.gameObject.AddComponent<CanvasGroup>(); promptGroup.alpha = 0; promptGroup.blocksRaycasts = false;
            var label = UiRect("Label", prompt).gameObject.AddComponent<TextMeshProUGUI>(); label.text = "MANTÉN  E / ESPACIO  PARA SALTAR"; label.fontSize = 22; label.alignment = TextAlignmentOptions.MidlineRight; label.color = new Color(.85f, .9f, .92f, .9f); label.characterSpacing = 4; label.raycastTarget = false;
            ((RectTransform)label.transform).offsetMax = new Vector2(-70, 0);
            var ring = new GameObject("Ring", typeof(RectTransform)); ring.transform.SetParent(prompt, false);
            var ringRect = (RectTransform)ring.transform; ringRect.anchorMin = ringRect.anchorMax = ringRect.pivot = new Vector2(1, .5f); ringRect.sizeDelta = new Vector2(44, 44); ringRect.anchoredPosition = new Vector2(-6, 0);
            var ringBg = ring.AddComponent<Image>(); ringBg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); ringBg.color = new Color(1, 1, 1, .15f); ringBg.raycastTarget = false;
            var fillGo = new GameObject("Fill", typeof(RectTransform)); fillGo.transform.SetParent(ring.transform, false); var fr = (RectTransform)fillGo.transform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
            var fill = fillGo.AddComponent<Image>(); fill.sprite = ringBg.sprite; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Radial360; fill.fillOrigin = 2; fill.fillAmount = 0; fill.color = new Color(.3f, .9f, 1f, .95f); fill.raycastTarget = false;

            var hero = HangarMenuAuthoring.MakeLight("HeroKey", rig, LightType.Spot, S + face * 2.2f - right * 1.6f + up * 3.2f, Quaternion.LookRotation(S + up * 1.3f - (S + face * 2.2f - right * 1.6f + up * 3.2f)).eulerAngles, new Color(.85f, .92f, 1f), 0, 9f, 50f);
            var cine = rig.gameObject.AddComponent<HangarIntroCinematic>();
            cine.ConfigureHeroLight(hero);
            cine.ConfigureGlass(b.glass);
            cine.Configure(director, cam, listener, paths.ToArray(), pose, b.podFx.idle, b.room, stand, b.podFx.vent, cannonSparks, dust, footSparks, cannon, fade, top, bottom, promptGroup, fill);
            // v30: capsule interior (restraints, back cables, tilting cradle) and contact-driven footsteps
            cine.ConfigureRestraints(BuildRestraints(b, m, pose));
            var stepGo = new GameObject("FootstepAudio"); stepGo.transform.SetParent(fxRoot, false);
            var stepAudio = stepGo.AddComponent<AudioSource>(); stepAudio.playOnAwake = false; stepAudio.spatialBlend = .6f; stepAudio.minDistance = 2; stepAudio.maxDistance = 25; stepAudio.outputAudioMixerGroup = MixerGroup("Player");
            var windows = new List<FootContactFx.Window>();
            foreach (var r in new[] { true, false })
            {
                pose.StepWindows(r, out var ea, out var ed, out var ga, out var gd, out var ba, out var bd);
                windows.Add(new FootContactFx.Window { right = r, armAt = ea, disarmAt = ed, strength = 1f });
                windows.Add(new FootContactFx.Window { right = r, armAt = ga, disarmAt = gd, strength = .35f });
                windows.Add(new FootContactFx.Window { right = r, armAt = ba, disarmAt = bd, strength = .35f });
            }
            var contact = rig.gameObject.AddComponent<FootContactFx>();
            contact.Configure(pose, director, cine, windows.ToArray(), dust, footSparks, stepAudio, Clip("IR_Hangar_FootMetal"));
            log.AppendLine("foot contact windows=" + windows.Count);
            log.AppendLine($"stand={S} facing={face} grip={grip.position}");
        }

        /// v30: the inside of capsule C-04 in Room00 (the menu capsule is not touched): a cradle (bed + footrest) that tilts
        /// forward, eight telescoping restraints (shoulders, chest, thighs, shins) placed just in front of the robot's
        /// surface, and four back cables plugged into the robot's sides. Driven by CapsuleRestraints from the cinematic time.
        static CapsuleRestraints BuildRestraints(Built b, HangarMenuAuthoring.Mats m, WakeUpRobotPose pose)
        {
            var pod = b.pod;
            var cradle = pod.Find("Cradle"); if (!cradle) cradle = HangarMenuAuthoring.Group("Cradle", pod);
            var bed = pod.Find("Bed"); if (bed) bed.SetParent(cradle, true);
            // robot surface in pod space (bind pose, as authored) to place the restraints just in front of it
            var smr = b.podFx.model ? b.podFx.model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh && r.gameObject.name != "GlowOverlay").OrderByDescending(r => r.sharedMesh.vertexCount).FirstOrDefault() : null;
            var pts = new Vector3[0];
            if (smr) pts = RobotRigidSkin.SkinnedWorld(smr).Select(v => pod.InverseTransformPoint(v)).Where(v => !float.IsNaN(v.x)).ToArray();
            log.AppendLine("restraints: robot surface samples=" + pts.Length);
            float Front(float x0, float x1, float y0, float y1) { float z = -.16f; foreach (var p in pts) if (p.x >= x0 && p.x <= x1 && p.y >= y0 && p.y <= y1 && p.z < z) z = p.z; return z; }
            float sole = pts.Length > 0 ? pts.Min(p => p.y) : -1.175f;
            HangarMenuAuthoring.Box("Footrest", cradle, new Vector3(0, sole - .028f, -.03f), new Vector3(.95f, .05f, .55f), m.trim);
            var defs = new (string name, float y, float inner)[] { ("Shoulder", .62f, .16f), ("Chest", .30f, .0f), ("Thigh", -.35f, .03f), ("Shin", -.85f, .03f) };
            const float outer = .70f;
            var clamps = new List<CapsuleRestraints.Clamp>(); int i = 0;
            foreach (var d in defs)
                foreach (var s in new[] { -1, 1 })
                {
                    float x0 = Mathf.Min(s * d.inner, s * outer), x1 = Mathf.Max(s * d.inner, s * outer);
                    float z = Front(x0 - .03f, x1 + .03f, d.y - .09f, d.y + .09f) - .055f;
                    var root = new GameObject("Restraint_" + d.name + (s > 0 ? "L" : "R")).transform; root.SetParent(cradle, false); root.localPosition = new Vector3(s * outer, d.y, z);
                    float len = outer - d.inner;
                    HangarMenuAuthoring.Box("Bar", root, new Vector3(-s * len * .5f, 0, 0), new Vector3(len, .075f, .065f), m.trim);
                    HangarMenuAuthoring.Box("Pad", root, new Vector3(-s * (len - .05f), 0, .035f), new Vector3(.09f, .09f, .02f), m.rubber, null, false);
                    HangarMenuAuthoring.Box("Housing_" + d.name + (s > 0 ? "L" : "R"), cradle, new Vector3(s * (outer + .045f), d.y, z), new Vector3(.09f, .13f, .11f), m.plateDark);
                    clamps.Add(new CapsuleRestraints.Clamp { bar = root, closedLocal = root.localPosition, retract = new Vector3(0, 0, .02f), delay = i * .05f, keep = .12f });
                    i++;
                }
            // back cables: anchored on the rails, plugged into the robot's sides at chest and waist height
            var cables = new List<CapsuleRestraints.Cable>();
            foreach (var s in new[] { -1, 1 })
                foreach (var y in new[] { .38f, -.05f })
                {
                    var anchorLocal = new Vector3(s * .72f, y, .16f);
                    var side = pts.Where(p => Mathf.Abs(p.y - y) < .08f && p.z > -.02f && p.x * s > 0).OrderBy(p => (p - anchorLocal).sqrMagnitude).Take(1).ToArray();
                    var plugLocal = side.Length > 0 ? side[0] : new Vector3(s * .3f, y, .1f);
                    var anchor = new GameObject("CableAnchor").transform; anchor.SetParent(pod, false); anchor.localPosition = anchorLocal;
                    var hose = HangarMenuAuthoring.Prim(PrimitiveType.Cylinder, "BackCable", pod, Vector3.zero, new Vector3(.045f, .2f, .045f), m.rubber, null, false).transform;
                    var plug = HangarMenuAuthoring.Box("CablePlug", pod, Vector3.zero, new Vector3(.07f, .06f, .07f), m.trim, null, false).transform;
                    var plugged = pod.TransformPoint(plugLocal);
                    cables.Add(new CapsuleRestraints.Cable { anchor = anchor, hose = hose, plug = plug, plugged = plugged, hang = (Vector3.down * .9f + pod.right * (s * .35f) - pod.forward * .15f).normalized });
                }
            var fxRoot = HangarMenuAuthoring.Group("RestraintFx", pod);
            var steam = HangarMenuAuthoring.PS("ClampSteam", fxRoot, m.fxSmoke, ps =>
            {
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1.3f); main.startSpeed = new ParticleSystem.MinMaxCurve(.4f, 1.2f); main.startSize = new ParticleSystem.MinMaxCurve(.08f, .22f);
                main.startColor = new Color(.82f, .86f, .9f, .35f); main.gravityModifier = -.06f; main.maxParticles = 80; main.simulationSpace = ParticleSystemSimulationSpace.World;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .04f;
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, 2.4f));
                var cl = ps.colorOverLifetime; cl.enabled = true; cl.color = HangarMenuAuthoring.Fade(Color.white, .8f);
            });
            var sparks = HangarMenuAuthoring.PS("CableSparks", fxRoot, m.fxSpark, ps =>
            {
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.15f, .4f); main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 2.4f); main.startSize = new ParticleSystem.MinMaxCurve(.01f, .025f);
                main.gravityModifier = 1.3f; main.maxParticles = 40; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startColor = new ParticleSystem.MinMaxGradient(new Color(.6f, .95f, 1f), new Color(1f, .85f, .55f));
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .02f;
                var rr = ps.GetComponent<ParticleSystemRenderer>(); rr.renderMode = ParticleSystemRenderMode.Stretch; rr.velocityScale = .05f; rr.lengthScale = 1f;
                var cl = ps.colorOverLifetime; cl.enabled = true; cl.color = HangarMenuAuthoring.Fade(Color.white);
            });
            var inside = pod.gameObject.AddComponent<CapsuleRestraints>();
            inside.Configure(pose, cradle, clamps.ToArray(), cables.ToArray(), steam, sparks);
            log.AppendLine($"restraints={clamps.Count} cables={cables.Count} footrest y={sole - .028f:0.000} (pod-local)");
            return inside;
        }

        // Campaign integration (connection chamber, alarm corridor, airlock order) lives in HangarTransitAuthoring.
        internal static AudioClip SfxClip(string name) => Clip(name);
        internal static AudioMixerGroup Group(string name) => MixerGroup(name);
    }
}
