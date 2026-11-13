using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // Visual pass F1 (08/11/2026). C2: damage flash as edge vignette instead of a solid full-screen red.
    // C1: Play Mode watcher that logs large visible renderers / strong lights to find the white glare source.
    // Scene changes go through SerializedObject on the RequiemCampaign GameHUD instance (prefab overrides only;
    // the template GameHUD.prefab is not modified).
    public static class VisualPassF1
    {
        const string Scene = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        const string VignetteSprite = "61e9c8b64d003e047ab7cfc7f5d74555";
        const string Backup = "Tools/IronRequiem/Backups/20261108-visualpass-f1";
        public const string Report = "Tools/IronRequiem/Reports/VisualPass/F1";

        [MenuItem("Tools/Iron Requiem/Visual Pass/F1 C2 - Damage vignette")]
        public static void ApplyDamageVignette()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("IRON REQUIEM VisualPass: stop Play Mode first."); return; }
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != Scene)
            {
                if (EditorSceneManager.GetSceneManagerSetup().Any(s => EditorSceneManager.GetSceneByPath(s.path).isDirty)) { Debug.LogError("IRON REQUIEM VisualPass: an open scene has unsaved user changes; not switching."); return; }
                active = EditorSceneManager.OpenScene(Scene);
            }
            else if (active.isDirty) { Debug.LogError("IRON REQUIEM VisualPass: RequiemCampaign already has unsaved changes; not saving over them."); return; }

            Directory.CreateDirectory(Backup); Directory.CreateDirectory(Report);
            foreach (var f in new[] { Scene, Scene + ".meta" }) File.Copy(f, Path.Combine(Backup, Path.GetFileName(f)), true);

            var log = new StringBuilder();
            var hud = active.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).FirstOrDefault(c => c && c.GetType().Name == "FeedbackFlashHUD");
            if (!hud) { Debug.LogError("IRON REQUIEM VisualPass: FeedbackFlashHUD not found in RequiemCampaign."); return; }
            var data = new SerializedObject(hud);
            var image = data.FindProperty("FlashImage").objectReferenceValue as Image;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(VignetteSprite));
            if (!image || !sprite) { Debug.LogError("IRON REQUIEM VisualPass: flash image or vignette sprite missing."); return; }
            log.AppendLine("before alpha=" + data.FindProperty("DamageFlashMaxAlpha").floatValue + " critical=" + data.FindProperty("CriticaHealthVignetteMaxAlpha").floatValue + " sprite=" + (image.sprite ? image.sprite.name : "none"));

            Undo.RecordObject(image, "Damage vignette");
            image.sprite = sprite; image.type = Image.Type.Simple; image.preserveAspect = false; image.raycastTarget = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);

            data.FindProperty("DamageFlashColor").colorValue = new Color(.78f, .02f, .01f, 1f);
            data.FindProperty("DamageFlashDuration").floatValue = .18f;
            data.FindProperty("DamageFlashMaxAlpha").floatValue = .55f;
            data.FindProperty("CriticaHealthVignetteMaxAlpha").floatValue = .6f;
            data.FindProperty("PulsatingVignetteFrequency").floatValue = 4f;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud);

            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            log.AppendLine("after alpha=.55 duration=.18 critical=.6 freq=4 sprite=" + sprite.name + " hud=" + hud.name);
            File.WriteAllText(Report + "/c2-damage-vignette.txt", log.ToString());
            Debug.Log("IRON REQUIEM VisualPass C2 applied. " + log);
        }

        static readonly string[] CampaignEnemies = {
            "Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab","Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab",
            "Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab","Assets/IronRequiem/Prefabs/Campaign/HoverBot_Current.prefab",
            "Assets/IronRequiem/Prefabs/Campaign/Turret_Current.prefab" };

        // C1: the glare watcher caught SawBot_Current/HitSparks (Unlit additive, 10.9m bounds) emitted at the contact
        // point on the player, i.e. right in front of the camera. ParticleSystemRenderer.maxParticleSize defaults to .5
        // (half the screen), so each spark near the lens covered the view. Clamp spark screen size and start size on the
        // campaign enemy variants only; emission counts, timings, damage and positions stay as they are.
        [MenuItem("Tools/Iron Requiem/Visual Pass/F1 C1 - Clamp spark glare")]
        public static void ClampSparkGlare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("IRON REQUIEM VisualPass: stop Play Mode first."); return; }
            Directory.CreateDirectory(Backup); Directory.CreateDirectory(Report);
            var log = new StringBuilder();
            foreach (var path in CampaignEnemies)
            {
                foreach (var f in new[] { path, path + ".meta" }) File.Copy(f, Path.Combine(Backup, Path.GetFileName(f)), true);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        if (ps.name.IndexOf("Spark", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var r = ps.GetComponent<ParticleSystemRenderer>(); if (!r) continue;
                        var main = ps.main; var size = main.startSize;
                        string before = "maxScreen=" + r.maxParticleSize + " startSize=" + size.constantMin + ".." + size.constantMax + " mode=" + size.mode;
                        if (r.maxParticleSize > .04f) { r.maxParticleSize = .04f; changed = true; }
                        float cap = .12f;
                        if (size.mode == ParticleSystemCurveMode.Constant && size.constant > cap) { main.startSize = cap; changed = true; }
                        else if (size.mode == ParticleSystemCurveMode.TwoConstants && size.constantMax > cap) { main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Min(size.constantMin, cap * .4f), cap); changed = true; }
                        log.AppendLine(path + " :: " + AnimationUtility.CalculateTransformPath(ps.transform, root.transform) + " " + before + " -> maxScreen=" + r.maxParticleSize + " startSize<=" + cap);
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Report + "/c1-spark-clamp.txt", log.ToString());
            Debug.Log("IRON REQUIEM VisualPass C1 spark clamp:\n" + log);
        }

        // C2 follow-up: sustained saw damage resets the flash every 0.1s, so the vignette sat near max alpha. Lower it.
        [MenuItem("Tools/Iron Requiem/Visual Pass/F1 C2b - Softer damage vignette")]
        public static void SofterVignette()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != Scene || active.isDirty || EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("IRON REQUIEM VisualPass: open a clean RequiemCampaign in Edit Mode first."); return; }
            var hud = active.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).First(c => c && c.GetType().Name == "FeedbackFlashHUD");
            var data = new SerializedObject(hud);
            data.FindProperty("DamageFlashMaxAlpha").floatValue = .32f;
            data.FindProperty("DamageFlashColor").colorValue = new Color(.6f, .015f, .01f, 1f);
            data.FindProperty("CriticaHealthVignetteMaxAlpha").floatValue = .5f;
            data.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
            EditorSceneManager.MarkSceneDirty(active); EditorSceneManager.SaveScene(active);
            File.AppendAllText(Report + "/c2-damage-vignette.txt", "C2b alpha=.32 color=(.6,.015,.01) critical=.5\n");
            Debug.Log("IRON REQUIEM VisualPass C2b applied.");
        }

        // C1 real fix. Diagnosis (08/11 Play): the white disc follows the T-07 and disappears with Bloom off, while the
        // profile bloom is only .08 / threshold 1.1 -> a NaN/Inf or extreme HDR pixel is being spread by Bloom.
        // Fix: StopNaN on the campaign cameras + Bloom clamp on the campaign volume profile(s). Both are cheap
        // (one fullscreen pass / a shader constant); intensity, threshold and the rest of the grade are kept.
        [MenuItem("Tools/Iron Requiem/Visual Pass/F1 C1 - Fix bloom glare (StopNaN + clamp)")]
        public static void FixBloomGlare()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != Scene || active.isDirty || EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("IRON REQUIEM VisualPass: open a clean RequiemCampaign in Edit Mode first."); return; }
            Directory.CreateDirectory(Backup); Directory.CreateDirectory(Report);
            File.Copy(Scene, Path.Combine(Backup, "RequiemCampaign.before-c1.unity"), true);
            var log = new StringBuilder();
            foreach (var cam in active.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)))
            {
                var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); if (!data) continue;
                log.AppendLine("CAMERA " + cam.name + " stopNaN " + data.stopNaN + " -> True post=" + data.renderPostProcessing);
                Undo.RecordObject(data, "StopNaN"); data.stopNaN = true; PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            }
            var profiles = new HashSet<UnityEngine.Rendering.VolumeProfile>();
            foreach (var v in active.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UnityEngine.Rendering.Volume>(true))) if (v.sharedProfile) profiles.Add(v.sharedProfile);
            foreach (var p in profiles)
            {
                string path = AssetDatabase.GetAssetPath(p);
                if (!path.StartsWith("Assets/IronRequiem/")) { log.AppendLine("SKIP non-IronRequiem profile " + path); continue; }
                File.Copy(path, Path.Combine(Backup, Path.GetFileName(path)), true);
                if (p.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom))
                {
                    log.AppendLine("BLOOM " + path + " intensity=" + bloom.intensity.value + " threshold=" + bloom.threshold.value + " clamp=" + bloom.clamp.value + "(" + bloom.clamp.overrideState + ") -> clamp=12");
                    bloom.clamp.overrideState = true; bloom.clamp.value = 12f; EditorUtility.SetDirty(p);
                }
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(active); EditorSceneManager.SaveScene(active);
            File.WriteAllText(Report + "/c1-bloom-glare-fix.txt", log.ToString());
            Debug.Log("IRON REQUIEM VisualPass C1 bloom glare fix:\n" + log);
        }

        // The campaign volume uses the shared Assets/Rendering/PostProcessingProfile.asset (also used by MainScene).
        // Do not touch it: copy it under Assets/IronRequiem/Art/VisualPass and point only the campaign volume at the copy.
        public const string CampaignProfile = "Assets/IronRequiem/Art/VisualPass/Campaign_PostProfile.asset";
        [MenuItem("Tools/Iron Requiem/Visual Pass/F1 C1b - Campaign post profile copy + bloom clamp")]
        public static void CampaignProfileCopy()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != Scene || active.isDirty || EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("IRON REQUIEM VisualPass: open a clean RequiemCampaign in Edit Mode first."); return; }
            Directory.CreateDirectory(Report); Directory.CreateDirectory("Assets/IronRequiem/Art/VisualPass");
            var log = new StringBuilder();
            var volumes = active.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UnityEngine.Rendering.Volume>(true)).ToArray();
            foreach (var v in volumes)
            {
                string src = v.sharedProfile ? AssetDatabase.GetAssetPath(v.sharedProfile) : "";
                if (src == CampaignProfile) { log.AppendLine("ALREADY " + v.name); continue; }
                if (src != "Assets/Rendering/PostProcessingProfile.asset") { log.AppendLine("SKIP " + v.name + " " + src); continue; }
                if (!File.Exists(CampaignProfile)) { AssetDatabase.CopyAsset(src, CampaignProfile); AssetDatabase.ImportAsset(CampaignProfile); }
                var copy = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(CampaignProfile);
                if (copy.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom))
                {
                    log.AppendLine("BLOOM intensity=" + bloom.intensity.value + "(" + bloom.intensity.overrideState + ") threshold=" + bloom.threshold.value + " clamp=" + bloom.clamp.value + " -> clamp=12");
                    bloom.clamp.overrideState = true; bloom.clamp.value = 12f; EditorUtility.SetDirty(copy);
                }
                foreach (var c in copy.components) log.AppendLine("  override " + c.GetType().Name + " active=" + c.active);
                Undo.RecordObject(v, "Campaign profile"); v.sharedProfile = copy; PrefabUtility.RecordPrefabInstancePropertyModifications(v);
                log.AppendLine("VOLUME " + v.name + " " + src + " -> " + CampaignProfile);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(active); EditorSceneManager.SaveScene(active);
            File.AppendAllText(Report + "/c1-bloom-glare-fix.txt", log.ToString());
            Debug.Log("IRON REQUIEM VisualPass C1b:\n" + log);
        }

        // Play Mode diagnostics only (runtime instances, nothing saved).
        [MenuItem("Tools/Iron Requiem/Visual Pass/Diag - Camera StopNaN on (play)")]
        public static void DiagStopNaN()
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
            {
                var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (data) { data.stopNaN = true; Debug.Log("IRON REQUIEM diag stopNaN on " + cam.name + " post=" + data.renderPostProcessing); }
            }
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/Diag - Bloom off (play)")]
        public static void DiagBloomOff()
        {
            foreach (var v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Exclude))
                if (v.profile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom)) { bloom.active = false; Debug.Log("IRON REQUIEM diag bloom off on volume instance " + v.name); }
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/Diag - Player invulnerable (play)")]
        public static void DiagInvulnerable()
        {
            var pc = Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerCharacterController>();
            var h = pc ? pc.GetComponent<Unity.FPS.Game.Health>() : null;
            if (h) { h.Invincible = true; Debug.Log("IRON REQUIEM diag player invulnerable (runtime only)"); }
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/F1 C1 - Glare watcher on")]
        public static void WatcherOn() { SessionState.SetBool(GlareWatcher.Key, true); GlareWatcher.Reset(); Debug.Log("IRON REQUIEM glare watcher ON; report: " + Report + "/c1-glare-watch.txt"); }

        [MenuItem("Tools/Iron Requiem/Visual Pass/F1 C1 - Glare watcher off")]
        public static void WatcherOff() { SessionState.SetBool(GlareWatcher.Key, false); GlareWatcher.Flush(); Debug.Log("IRON REQUIEM glare watcher OFF"); }
    }

    [InitializeOnLoad]
    public static class GlareWatcher
    {
        public const string Key = "IronRequiem.VisualPass.GlareWatch";
        static readonly Dictionary<string, string> seen = new Dictionary<string, string>();
        static int frame;
        static GlareWatcher()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingPlayMode) Flush(); };
        }
        public static void Reset() { seen.Clear(); }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || !SessionState.GetBool(Key, false)) return;
            if (++frame % 6 != 0) return;
            var cam = Camera.main; if (!cam) return;
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (!r.enabled || r is SkinnedMeshRenderer) continue;
                var b = r.bounds; float size = b.size.magnitude;
                bool particle = r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer;
                bool emissive = r.sharedMaterials.Any(m => m && (m.shader.name.Contains("Unlit") || m.shader.name.Contains("Particle") || (m.IsKeywordEnabled("_EMISSION"))));
                if (!(particle || emissive)) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (!m) continue; float peak = 0;
                    foreach (var prop in new[] { "_EmissionColor", "_BaseColor", "_Color", "_TintColor" })
                        if (m.HasProperty(prop)) { var c = m.GetColor(prop); peak = Mathf.Max(peak, c.maxColorComponent); if (float.IsNaN(c.r + c.g + c.b)) peak = float.NaN; }
                    if (float.IsNaN(peak) || peak > 8f) Note(r.transform, "HDR peak=" + peak.ToString("F1") + " mat=" + m.name + ":" + m.shader.name + " visible=" + r.isVisible);
                }
                if (size < (particle ? 2.5f : 1.5f)) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, b)) continue;
                if (Vector3.Distance(cam.transform.position, b.center) > 60f) continue;
                Note(r.transform, (particle ? "PARTICLE" : "EMISSIVE") + " size=" + size.ToString("F1") + " lossy=" + r.transform.lossyScale.ToString("F2") + " mats=" + string.Join("|", r.sharedMaterials.Where(m => m).Select(m => m.name + ":" + m.shader.name)));
            }
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.enabled && l.intensity > 3f && l.type != LightType.Directional && Vector3.Distance(cam.transform.position, l.transform.position) < 40f)
                    Note(l.transform, "LIGHT " + l.type + " intensity=" + l.intensity.ToString("F1") + " range=" + l.range.ToString("F1"));
        }

        static void Note(Transform t, string what)
        {
            string path = Path(t);
            string key = path + "|" + what.Split(' ')[0];
            if (seen.ContainsKey(key)) return;
            seen[key] = Time.time.ToString("F2") + "s " + what + " root=" + t.root.name + " path=" + path;
        }

        static string Path(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }

        public static void Flush()
        {
            if (seen.Count == 0) return;
            Directory.CreateDirectory(VisualPassF1.Report);
            File.WriteAllLines(VisualPassF1.Report + "/c1-glare-watch.txt", seen.Values.OrderBy(v => v));
        }
    }
}
