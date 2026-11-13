using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // Secondary shotgun, slide cooldown and execution threshold lab (04/11/2026).
    // Builds variants only; originals, MainScene and previous labs are preserved.
    [InitializeOnLoad]
    public static class SecondaryShotgunAuthoring
    {
        public const string Folder = "Assets/IronRequiem/Prefabs/SecondaryShotgun";
        public const string Lab = "Assets/IronRequiem/Scenes/SecondaryShotgunLab.unity";
        public const string Reports = "Tools/IronRequiem/Reports/SecondaryShotgun";
        public const string Weapon = Folder + "/Weapon_Shotgun_Secondary.prefab";
        public const string Projectile = Folder + "/Projectile_Shotgun_Secondary.prefab";
        const string SourceWeapon = "Assets/FPS/Prefabs/Weapons/Weapon_Shotgun.prefab";
        const string SourceProjectile = "Assets/FPS/Prefabs/Projectiles/Projectile_Shotgun.prefab";
        const string Queue = "Tools/IronRequiem/Requests/secondary-shotgun.txt", Key = "IRON.SecondaryShotgun";
        static readonly DateTime Loaded = DateTime.UtcNow;
        static TestRunnerApi api;

        // Final balance (previous template values in the report).
        public const int Pellets = 10;
        public const float PelletDamage = 9, Spread = 6, Delay = .85f, Range = 32, Speed = 150;
        public const float FullDamageDistance = 6, MinimumDamageDistance = 18, MinimumMultiplier = .3f;
        public const int MaxAmmo = 4;
        public const float ReloadRate = 1, ReloadDelay = 1;
        public const float SlideCooldown = 1.5f;

        static SecondaryShotgunAuthoring()
        {
            EditorApplication.update += Tick;
            if (SessionState.GetBool(Key, false)) EditorApplication.delayCall += Register;
        }

        static void Register()
        {
            if (!api) { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results()); }
        }

        static bool Dirty() => Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty);

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(Key + ".restore", false))
            {
                SessionState.EraseBool(Key + ".restore");
                if (!Dirty()) EditorSceneManager.OpenScene(File.Exists(Lab) ? Lab : CombatFollowupAuthoring.Lab);
                IronRequiemSetupTools.WriteAudit();
            }
            if (!File.Exists(Queue) || SessionState.GetBool(Key, false)) return;
            // Wait for the domain that contains the latest edit of this tool and its tests.
            if (new[] { "Assets/IronRequiem/Editor/SecondaryShotgunAuthoring.cs", "Assets/IronRequiem/Tests/PlayMode/SecondaryShotgunTests.cs" }
                .Any(p => File.Exists(p) && File.GetLastWriteTimeUtc(p) > Loaded)) return;
            string op = File.ReadAllText(Queue).Trim(); File.Delete(Queue); Directory.CreateDirectory(Reports);
            try
            {
                if (Dirty()) throw new Exception("Preserving dirty user scene");
                if (op == "bunker-shotgun") { BunkerPolishAuthoring.ConfigureShotgun(); File.WriteAllText(Reports + "/status.txt", "bunker-shotgun: done"); return; }
                if (op == "bunker-bench-prepare") { BenchPrepare(BunkerPolishAuthoring.Lab, BunkerBench, "Assets/IronRequiem/Prefabs/SecondaryShotgun/T07_HitFeedback_v04_NavThrottle.prefab", "Assets/IronRequiem/Prefabs/SecondaryShotgun/BigBot_Burst_v10_NavThrottle.prefab"); File.WriteAllText(Reports + "/status.txt", "bunker-bench-prepare: done"); return; }
                if (op == "bunker-spawn-queue") { BunkerPolishAuthoring.AddSpawnQueue(BunkerPolishAuthoring.Lab, BunkerBench); File.WriteAllText(Reports + "/status.txt", "bunker-spawn-queue: done"); return; }
                if (op == "bunker-rebuild") { BunkerPolishAuthoring.Rebuild(); File.WriteAllText(Reports + "/status.txt", "bunker-rebuild: done"); return; }
                if (op == "bunker-build") { BunkerPolishAuthoring.Build(); File.WriteAllText(Reports + "/status.txt", "bunker-build: done"); return; }
                if (op == "optimize") { Optimize(); File.WriteAllText(Reports + "/status.txt", "optimize: done"); return; }
                if (op == "bench-prepare") { BenchPrepare(); File.WriteAllText(Reports + "/status.txt", "bench-prepare: done"); return; }
                if (op.StartsWith("bench-build-")) { BenchBuild(op.Substring(12)); File.WriteAllText(Reports + "/status.txt", op + ": done"); return; }
                if (op.StartsWith("bench-analyse-")) { BenchAnalyse(op.Substring(14)); File.WriteAllText(Reports + "/status.txt", op + ": done"); return; }
                if (op == "mount") { Mount(); File.WriteAllText(Reports + "/status.txt", "mount: done"); return; }
                if (op == "build") { Build(); File.WriteAllText(Reports + "/status.txt", "build: done"); return; }
                if (op == "audit") { IronRequiemSetupTools.WriteAudit(); File.WriteAllText(Reports + "/status.txt", "audit: done"); return; }
                File.WriteAllText(Reports + "/phase.txt", op); Register(); SessionState.SetBool(Key, true);
                string filter = op.StartsWith("class:") ? "IronRequiem.Tests." + op.Substring(6) : op.StartsWith("only:") ? "IronRequiem.Tests.SecondaryShotgunTests." + op.Substring(5) : "IronRequiem.Tests.SecondaryShotgunTests";
                api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = new[] { filter } }));
            }
            catch (Exception e) { File.WriteAllText(Reports + "/error.txt", e.ToString()); File.WriteAllText(Reports + "/status.txt", op + ": FAILED"); Debug.LogException(e); }
        }

        static void Variant(string source, string dest)
        {
            if (File.Exists(dest)) throw new Exception("Preserve existing " + dest);
            var preview = EditorSceneManager.NewPreviewScene();
            try { var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source), preview); PrefabUtility.SaveAsPrefabAsset(go, dest); }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        static void Build()
        {
            if (File.Exists(Lab)) throw new Exception("Preserve existing lab");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(SourceWeapon).GetComponent<WeaponController>();
            var templateProjectile = AssetDatabase.LoadAssetAtPath<GameObject>(SourceProjectile).GetComponent<ProjectileStandard>();
            File.WriteAllText(Reports + "/previous-values.txt",
                $"Template Weapon_Shotgun: pellets={template.BulletsPerShot} damagePerPellet={templateProjectile.Damage} perShot={template.BulletsPerShot * templateProjectile.Damage} spread={template.BulletSpreadAngle} delay={template.DelayBetweenShots} maxAmmo={template.MaxAmmo} reloadRate={template.AmmoReloadRate}/s reloadDelay={template.AmmoReloadDelay} speed={templateProjectile.Speed} gravity={templateProjectile.GravityDownAcceleration} lifetime={templateProjectile.MaxLifeTime} nominalRange={templateProjectile.Speed * templateProjectile.MaxLifeTime} falloff=none");

            Variant(SourceProjectile, Projectile);
            var go = PrefabUtility.LoadPrefabContents(Projectile);
            try
            {
                var p = go.GetComponent<ProjectileStandard>(); p.Damage = PelletDamage; p.Speed = Speed; p.MaxLifeTime = Range / Speed;
                var falloff = go.GetComponent<ProjectileDistanceFalloff>(); if (!falloff) falloff = go.AddComponent<ProjectileDistanceFalloff>();
                falloff.Configure(FullDamageDistance, MinimumDamageDistance, MinimumMultiplier, Range);
                PrefabUtility.SaveAsPrefabAsset(go, Projectile);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }

            Variant(SourceWeapon, Weapon);
            go = PrefabUtility.LoadPrefabContents(Weapon);
            try
            {
                var w = go.GetComponent<WeaponController>();
                w.ProjectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Projectile).GetComponent<ProjectileBase>();
                w.BulletsPerShot = Pellets; w.BulletSpreadAngle = Spread; w.DelayBetweenShots = Delay;
                w.MaxAmmo = MaxAmmo; w.AmmoReloadRate = ReloadRate; w.AmmoReloadDelay = ReloadDelay;
                PrefabUtility.SaveAsPrefabAsset(go, Weapon);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }

            if (!AssetDatabase.CopyAsset(CombatFollowupAuthoring.Lab, Lab)) throw new Exception("Cannot copy lab");
            var scene = EditorSceneManager.OpenScene(Lab);
            var player = Object.FindAnyObjectByType<PlayerCharacterController>();
            var manager = new SerializedObject(player.GetComponent<PlayerWeaponsManager>());
            var list = manager.FindProperty("StartingWeapons");
            if (list.arraySize < 1) throw new Exception("Lab player has no primary weapon");
            list.arraySize = 2; list.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Weapon).GetComponent<WeaponController>();
            manager.ApplyModifiedPropertiesWithoutUndo();
            var motion = new SerializedObject(player.GetComponent<AdvancedPlayerMovement>());
            motion.FindProperty("slideCooldown").floatValue = SlideCooldown; motion.FindProperty("slideRequiresRelease").boolValue = true;
            motion.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Cannot save lab");
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == Lab)) { scenes.Add(new EditorBuildSettingsScene(Lab, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            AssetDatabase.SaveAssets(); IronRequiemSetupTools.WriteAudit();
            File.WriteAllText(Reports + "/settings.txt",
                $"Secondary shotgun: pellets={Pellets} damagePerPellet={PelletDamage} perShot={Pellets * PelletDamage} spread={Spread} delay={Delay} maxAmmo={MaxAmmo} reloadRate={ReloadRate}/s reloadDelay={ReloadDelay} speed={Speed} range={Range}m falloff full<={FullDamageDistance}m -> x{MinimumMultiplier} at>={MinimumDamageDistance}m. Slide cooldown={SlideCooldown}s from slide end, release C required.");
        }

        // Plugs the shotgun into the modular right forearm like the primary module: rear Attachment on the bore
        // axis, visual scaled relative to the primary. Edits only the shotgun variant and the lab player instance.
        static Bounds LocalBounds(Transform root)
        {
            bool any = false; var b = new Bounds();
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!f.sharedMesh || !f.GetComponent<Renderer>()) continue; var m = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = m.center + Vector3.Scale(m.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = root.InverseTransformPoint(f.transform.TransformPoint(c)); if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }
        public const float LengthRatio = 1.2f;
        public static readonly Vector3 AimOffset = new Vector3(0, .055f, .1f);
        static void Mount()
        {
            var primaryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Weapons/Visuals/Weapon_IR_ArmModule_v07.prefab").GetComponent<WeaponController>();
            var primaryBounds = LocalBounds(primaryPrefab.WeaponRoot.transform); float primaryLength = primaryBounds.size.z * primaryPrefab.WeaponRoot.transform.lossyScale.z;
            var pellet = PrefabUtility.LoadPrefabContents(Projectile);
            try { pellet.GetComponent<ProjectileDistanceFalloff>().Configure(FullDamageDistance, MinimumDamageDistance, MinimumMultiplier, Range); PrefabUtility.SaveAsPrefabAsset(pellet, Projectile); }
            finally { PrefabUtility.UnloadPrefabContents(pellet); }
            var go = PrefabUtility.LoadPrefabContents(Weapon); string info;
            try
            {
                var w = go.GetComponent<WeaponController>(); var root = w.WeaponRoot.transform;
                var existing = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Attachment"); if (existing) Object.DestroyImmediate(existing.gameObject);
                root.localScale = Vector3.one; var bounds = LocalBounds(root); float length = bounds.size.z;
                float scale = Mathf.Clamp(primaryLength * LengthRatio / Mathf.Max(.01f, length), .4f, 1); root.localScale = Vector3.one * scale;
                var muzzle = root.InverseTransformPoint(w.WeaponMuzzle.position);
                var attachment = new GameObject("Attachment").transform; attachment.SetParent(root, false);
                attachment.localPosition = new Vector3(muzzle.x, muzzle.y, bounds.min.z); attachment.localRotation = Quaternion.Inverse(root.rotation) * w.WeaponMuzzle.rotation;
                w.AimOffset = AimOffset;
                PrefabUtility.SaveAsPrefabAsset(go, Weapon);
                info = "aimOffset=" + AimOffset + " " + $"primaryLength={primaryLength:F3}m shotgunLength={length:F3}m scale={scale:F3} attachmentLocal={attachment.localPosition} muzzleLocal={muzzle}";
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
            var scene = EditorSceneManager.OpenScene(Lab);
            var hands = Object.FindAnyObjectByType<ProtagonistHandsPresentation>();
            var so = new SerializedObject(hands); var list = so.FindProperty("mountedWeaponPrefabs");
            list.arraySize = 1; list.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Weapon).GetComponent<WeaponController>();
            so.ApplyModifiedPropertiesWithoutUndo();
            var manager = new SerializedObject(Object.FindAnyObjectByType<PlayerWeaponsManager>()); manager.FindProperty("SmoothWeaponSwitch").boolValue = true; manager.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Cannot save lab");
            AssetDatabase.SaveAssets(); File.WriteAllText(Reports + "/mount.txt", info);
        }

        // Measured optimisation: enemy variants that stop re-requesting an unchanged NavMesh destination every frame.
        // Stats, AI, weapons and destruction are inherited unchanged from the CombatFollowup prefabs.
        public const float NavThreshold = .3f, NavRefresh = .25f;
        static void Optimize()
        {
            var map = new System.Collections.Generic.Dictionary<GameObject, GameObject>();
            foreach (string name in new[] { "T07_HitFeedback_v04", "BigBot_Burst_v10", "HoverBot_CombatPolish" })
            {
                string source = "Assets/IronRequiem/Prefabs/CombatFollowup/" + name + ".prefab", dest = Folder + "/" + name + "_NavThrottle.prefab";
                if (!File.Exists(dest)) Variant(source, dest);
                var go = PrefabUtility.LoadPrefabContents(dest);
                try { var e = go.GetComponent<Unity.FPS.AI.EnemyController>(); e.DestinationUpdateThreshold = NavThreshold; e.DestinationRefreshInterval = NavRefresh; PrefabUtility.SaveAsPrefabAsset(go, dest); }
                finally { PrefabUtility.UnloadPrefabContents(go); }
                map[AssetDatabase.LoadAssetAtPath<GameObject>(source)] = AssetDatabase.LoadAssetAtPath<GameObject>(dest);
            }
            foreach (string path in new[] { Lab, Bench })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var lab = Object.FindAnyObjectByType<CombatTwoRoomsLab>();
                if (lab)
                {
                    var data = new SerializedObject(lab); var waves = data.FindProperty("waves");
                    for (int i = 0; i < waves.arraySize; i++) { var groups = waves.GetArrayElementAtIndex(i).FindPropertyRelative("Groups"); for (int j = 0; j < groups.arraySize; j++) { var p = groups.GetArrayElementAtIndex(j).FindPropertyRelative("Prefab"); if (p.objectReferenceValue is GameObject g && map.TryGetValue(g, out var v)) p.objectReferenceValue = v; } }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                var runner = Object.FindAnyObjectByType<CombatLoadBenchmark>();
                if (runner) runner.Configure(map.Values.First(g => g.name.StartsWith("T07")), map.Values.First(g => g.name.StartsWith("BigBot")));
                EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Cannot save " + path);
            }
            EditorSceneManager.OpenScene(Lab); AssetDatabase.SaveAssets(); IronRequiemSetupTools.WriteAudit();
            File.WriteAllText(Reports + "/optimize.txt", "Enemy variants *_NavThrottle: DestinationUpdateThreshold=" + NavThreshold + " m, refresh=" + NavRefresh + " s. Lab waves and benchmark use them.");
        }

        public const string Bench ="Assets/IronRequiem/Scenes/SecondaryShotgunBenchmark.unity", BenchReports = "Tools/IronRequiem/Reports/CombatLoad";
        public static bool Building { get; private set; }
        public static int StrippedVariants;
        public const string BunkerBench = "Assets/IronRequiem/Scenes/BunkerBenchmark.unity";
        static void BenchPrepare() => BenchPrepare(Lab, Bench, "Assets/IronRequiem/Prefabs/CombatFollowup/T07_HitFeedback_v04.prefab", "Assets/IronRequiem/Prefabs/CombatFollowup/BigBot_Burst_v10.prefab");
        static void BenchPrepare(string source, string dest, string t07, string big)
        {
            if (File.Exists(dest)) throw new Exception("Preserve existing benchmark scene");
            if (!AssetDatabase.CopyAsset(source, dest)) throw new Exception("Cannot copy benchmark scene");
            var scene = EditorSceneManager.OpenScene(dest);
            var runner = new GameObject("CombatLoadBenchmark").AddComponent<CombatLoadBenchmark>();
            runner.Configure(AssetDatabase.LoadAssetAtPath<GameObject>(t07), AssetDatabase.LoadAssetAtPath<GameObject>(big));
            EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Cannot save benchmark");
            EditorSceneManager.OpenScene(source); IronRequiemSetupTools.WriteAudit();
        }

        // Explicit benchmark Player build. PlayerSettings are switched temporarily (frame timing, D3D11, Mono) and
        // restored together with byte snapshots of ProjectSettings and render assets, as in the Performance120 protocol.
        static void BenchBuild(string label)
        {
            bool development = label.Contains("development"); string root = "Tools/IronRequiem/Builds/CombatLoad/" + label;
            string snapshot = "Tools/IronRequiem/Backups/20261104-shotgun-slide-exec/build-" + label;
            var protectedFiles = Directory.GetFiles("ProjectSettings", "*", SearchOption.AllDirectories).Concat(Directory.GetFiles("Assets/Rendering", "*", SearchOption.AllDirectories))
                .Concat(new[] { "Assets/DefaultVolumeProfile.asset", "Assets/UniversalRenderPipelineGlobalSettings.asset" }).Where(File.Exists).ToArray();
            foreach (string path in protectedFiles) { string d = snapshot + "/" + path; Directory.CreateDirectory(Path.GetDirectoryName(d)); File.Copy(path, d, true); }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            bool timing = PlayerSettings.enableFrameTimingStats, defaults = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
            var graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64); var preloaded = PlayerSettings.GetPreloadedAssets();
            var backend = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone);
            try
            {
                PlayerSettings.enableFrameTimingStats = true; PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                Building = true; StrippedVariants = 0; Directory.CreateDirectory(root); Directory.CreateDirectory(BenchReports);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { label.Contains("bunker") ? BunkerBench : Bench }, locationPathName = root + "/IronRequiemCombatLoad.exe", target = BuildTarget.StandaloneWindows64, options = development ? BuildOptions.Development : BuildOptions.None });
                File.WriteAllText(BenchReports + "/build-" + label + ".txt", report.summary.result + " errors=" + report.summary.totalErrors + " warnings=" + report.summary.totalWarnings + " bytes=" + report.summary.totalSize + " seconds=" + report.summary.totalTime.TotalSeconds.ToString("F0") + " inferenceComputeVariantsExcluded=" + StrippedVariants);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Benchmark build " + report.summary.result);
            }
            finally
            {
                Building = false; PlayerSettings.enableFrameTimingStats = timing; PlayerSettings.SetPreloadedAssets(preloaded);
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, backend);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, graphics); PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, defaults);
                foreach (string path in protectedFiles) File.Copy(snapshot + "/" + path, path, true);
                AssetDatabase.Refresh(); EditorSceneManager.RestoreSceneManagerSetup(setup); IronRequiemSetupTools.WriteAudit();
            }
        }

        [Serializable] sealed class Marker { public string name; public double selfMs, totalMs, maxSelfMs; public int calls; }
        [Serializable] sealed class ProfileReport { public int frames; public Marker[] markers; public string note = "Development Player CPU profiler, wave 3, 6-11 s (staged damage + shooting). Ranked by self time; divide by frames for per-frame cost."; }
        static void BenchAnalyse(string label)
        {
            string path = Path.GetFullPath(BenchReports + "/" + label + "-profile.raw"); if (!File.Exists(path)) throw new Exception("Profile missing " + path);
            UnityEngine.Profiling.Profiler.enabled = false; UnityEditorInternal.ProfilerDriver.LoadProfile(path, false);
            var report = new ProfileReport(); var totals = new System.Collections.Generic.Dictionary<string, Marker>();
            for (int frame = UnityEditorInternal.ProfilerDriver.firstFrameIndex; frame <= UnityEditorInternal.ProfilerDriver.lastFrameIndex; frame++)
                for (int thread = 0; thread < 2; thread++)
                    using (var raw = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, thread))
                    {
                        if (!raw.valid) continue; if (thread == 0) report.frames++;
                        for (int i = 0; i < raw.sampleCount; i++)
                        {
                            double total = raw.GetSampleTimeMs(i), self = total; int child = i + 1;
                            for (int c = 0; c < raw.GetSampleChildrenCount(i); c++) { self -= raw.GetSampleTimeMs(child); child += 1 + raw.GetSampleChildrenCountRecursive(child); }
                            string name = (thread == 0 ? "" : "[render] ") + raw.GetSampleName(i);
                            if (!totals.TryGetValue(name, out var m)) totals[name] = m = new Marker { name = name };
                            m.selfMs += Math.Max(0, self); m.totalMs += total; m.maxSelfMs = Math.Max(m.maxSelfMs, self); m.calls++;
                        }
                    }
            report.markers = totals.Values.OrderByDescending(m => m.selfMs).Take(80).ToArray();
            File.WriteAllText(BenchReports + "/" + label + "-profile.json", JsonUtility.ToJson(report, true));
            int frames = Math.Max(1, report.frames);
            File.WriteAllLines(BenchReports + "/" + label + "-profile-inclusive.txt", totals.Values.OrderByDescending(m => m.totalMs).Take(90)
                .Select(m => (m.totalMs / frames).ToString("F3") + " incl  " + (m.selfMs / frames).ToString("F3") + " self  calls/frame " + (m.calls / (double)frames).ToString("F1") + "  " + m.name));
        }

        sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor t) { }
            public void TestStarted(ITestAdaptor t) { }
            public void TestFinished(ITestResultAdaptor t) { }
            public void OnError(string m) { SessionState.EraseBool(Key); File.WriteAllText(Reports + "/runner-error.txt", m); }
            public void RunFinished(ITestResultAdaptor r)
            {
                if (!SessionState.GetBool(Key, false)) return;
                SessionState.EraseBool(Key);
                var phase = File.ReadAllText(Reports + "/phase.txt").Replace(':', '-');
                TestRunnerApi.SaveResultToFile(r, Path.GetFullPath(Reports + "/" + phase + ".xml"));
                File.WriteAllText(Reports + "/status.txt", phase + ": " + r.ResultState + " passed=" + r.PassCount + " failed=" + r.FailCount);
                SessionState.SetBool(Key + ".restore", true);
            }
        }
    }
}
