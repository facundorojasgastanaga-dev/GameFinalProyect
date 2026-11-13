using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // Visual pass F1 lighting (08/11/2026). Per-sector identity on the campaign room scenes:
    //  - Environment: Flat .55/.60/.65 (same everywhere, kills volume) -> Trilight sky/equator/ground per sector.
    //  - Exp2 fog per sector for depth (no volumetrics: cost is a few ALU per pixel).
    //  - Existing CeilingLight points: colour + intensity only (no new lights, no shadows: the game is CPU-bound).
    //  - A global Volume per room scene (priority 10, layer Default, same as the camera volume) with
    //    ColorAdjustments + WhiteBalance in its own profile under Art/VisualPass. The shared campaign profile
    //    (bloom/tonemap/vignette) keeps working underneath.
    // Room scenes are the active scene while loaded (RenderSettings come from them).
    public static class VisualPassF1Lighting
    {
        const string Folder = "Assets/IronRequiem/Scenes/Campaign/";
        const string Art = "Assets/IronRequiem/Art/VisualPass";
        const string Backup = "Tools/IronRequiem/Backups/20261108-visualpass-f1-lighting";
        // Global brightness knob for iteration (ambient only).
        const float AmbientScale = 1.35f;

        struct Sector
        {
            public string Scene; public Color Sky, Equator, Ground, Fog, Light; public float FogDensity, LightIntensity;
            public float Exposure, Contrast, Saturation, Temperature, Tint; public Color Filter;
        }

        static readonly Sector[] Sectors =
        {
            new Sector { Scene = "Room01_Foundry",  Sky = C(.30f,.24f,.18f), Equator = C(.20f,.15f,.11f), Ground = C(.20f,.15f,.11f), Fog = C(.17f,.11f,.07f), FogDensity = .016f,
                         Light = C(1f,.74f,.48f), LightIntensity = 6f, Exposure = .1f, Contrast = 12, Saturation = -4, Temperature = 12, Tint = 0, Filter = C(1f,.95f,.88f) },
            new Sector { Scene = "Room02_Breach",   Sky = C(.21f,.26f,.33f), Equator = C(.14f,.17f,.21f), Ground = C(.14f,.17f,.21f), Fog = C(.08f,.10f,.13f), FogDensity = .018f,
                         Light = C(.82f,.92f,1f), LightIntensity = 6f, Exposure = .1f, Contrast = 14, Saturation = -12, Temperature = -10, Tint = 0, Filter = C(.94f,.98f,1f) },
            new Sector { Scene = "Room03_Assembly", Sky = C(.28f,.26f,.22f), Equator = C(.18f,.17f,.14f), Ground = C(.18f,.17f,.14f), Fog = C(.12f,.11f,.08f), FogDensity = .015f,
                         Light = C(1f,.86f,.62f), LightIntensity = 6f, Exposure = .1f, Contrast = 12, Saturation = -6, Temperature = 5, Tint = 0, Filter = C(1f,.97f,.92f) },
            new Sector { Scene = "Room04_Reactor",  Sky = C(.15f,.27f,.27f), Equator = C(.10f,.17f,.17f), Ground = C(.10f,.17f,.17f), Fog = C(.05f,.13f,.13f), FogDensity = .02f,
                         Light = C(.58f,1f,.9f), LightIntensity = 6.5f, Exposure = .1f, Contrast = 15, Saturation = -2, Temperature = -8, Tint = -4, Filter = C(.92f,1f,.98f) },
            new Sector { Scene = "Pursuit05",       Sky = C(.27f,.15f,.13f), Equator = C(.17f,.10f,.08f), Ground = C(.17f,.10f,.08f), Fog = C(.15f,.05f,.04f), FogDensity = .022f,
                         Light = C(1f,.48f,.36f), LightIntensity = 6f, Exposure = .05f, Contrast = 16, Saturation = 0, Temperature = 8, Tint = 0, Filter = C(1f,.9f,.86f) },
        };
        static Color C(float r, float g, float b) => new Color(r, g, b, 1);

        [MenuItem("Tools/Iron Requiem/Visual Pass/F1 L1 - Sector lighting + grade")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("IRON REQUIEM VisualPass: stop Play Mode first."); return; }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => EditorSceneManager.GetSceneByPath(s.path).isDirty)) { Debug.LogError("IRON REQUIEM VisualPass: save or discard open scene changes first."); return; }
            Directory.CreateDirectory(Backup); Directory.CreateDirectory(Art); Directory.CreateDirectory(VisualPassF1.Report);
            var log = new StringBuilder();
            try
            {
                foreach (var s in Sectors)
                {
                    string path = Folder + s.Scene + ".unity";
                    if (!File.Exists(Path.Combine(Backup, s.Scene + ".unity"))) foreach (var f in new[] { path, path + ".meta" }) File.Copy(f, Path.Combine(Backup, Path.GetFileName(f)), true);
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                    log.AppendLine("SCENE " + s.Scene + " before ambient=" + RenderSettings.ambientMode + " " + RenderSettings.ambientSkyColor + " fog=" + RenderSettings.fog);
                    RenderSettings.ambientMode = AmbientMode.Trilight;
                    RenderSettings.ambientSkyColor = s.Sky * AmbientScale; RenderSettings.ambientEquatorColor = s.Equator * AmbientScale; RenderSettings.ambientGroundColor = s.Ground * AmbientScale;
                    RenderSettings.ambientIntensity = 1f;
                    RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = s.Fog; RenderSettings.fogDensity = s.FogDensity;

                    int lights = 0;
                    foreach (var l in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)).Where(l => l.type == LightType.Point || l.type == LightType.Spot))
                    {
                        if (!l.name.StartsWith("CeilingLight")) { log.AppendLine("  keep light " + l.name); continue; }
                        Undo.RecordObject(l, "Sector light"); l.color = s.Light; l.intensity = s.LightIntensity; lights++;
                    }

                    string profilePath = Art + "/Sector_" + s.Scene + ".asset";
                    var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
                    if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
                    var ca = profile.TryGet(out ColorAdjustments existingCa) ? existingCa : profile.Add<ColorAdjustments>(true);
                    ca.postExposure.Override(s.Exposure); ca.contrast.Override(s.Contrast); ca.saturation.Override(s.Saturation); ca.colorFilter.Override(s.Filter);
                    var wb = profile.TryGet(out WhiteBalance existingWb) ? existingWb : profile.Add<WhiteBalance>(true);
                    wb.temperature.Override(s.Temperature); wb.tint.Override(s.Tint);
                    foreach (var c in profile.components) { if (!AssetDatabase.Contains(c)) AssetDatabase.AddObjectToAsset(c, profile); EditorUtility.SetDirty(c); }
                    EditorUtility.SetDirty(profile);

                    var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "VisualPass_SectorGrade");
                    if (!go) { go = new GameObject("VisualPass_SectorGrade"); SceneManager_Move(go, scene); }
                    go.layer = 0;
                    var vol = go.GetComponent<Volume>() ? go.GetComponent<Volume>() : go.AddComponent<Volume>();
                    vol.isGlobal = true; vol.priority = 10; vol.weight = 1; vol.sharedProfile = profile;

                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    log.AppendLine("  ambient Trilight x" + AmbientScale + " fog exp2 " + s.FogDensity + " ceiling lights=" + lights + " -> " + s.Light + " i=" + s.LightIntensity + " grade=" + profilePath);
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            File.WriteAllText(VisualPassF1.Report + "/l1-sector-lighting.txt", log.ToString());
            Debug.Log("IRON REQUIEM VisualPass L1:\n" + log);
        }


        // Renders one preview per sector (eye height, from the entry side looking across the room) with post and fog.
        // Temporary camera only; scenes are not modified or saved.
        [MenuItem("Tools/Iron Requiem/Visual Pass/F1 L2 - Render sector previews")]
        public static void Previews()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => EditorSceneManager.GetSceneByPath(s.path).isDirty)) { Debug.LogError("IRON REQUIEM VisualPass: save open scenes first."); return; }
            string dir = VisualPassF1.Report + "/Previews"; Directory.CreateDirectory(dir);
            try
            {
                foreach (var s in Sectors)
                {
                    var scene = EditorSceneManager.OpenScene(Folder + s.Scene + ".unity", OpenSceneMode.Single);
                    var lamps = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)).Where(l => l.name.StartsWith("CeilingLight")).Select(l => l.transform.position).ToArray();
                    if (lamps.Length == 0) continue;
                    Physics.SyncTransforms();
                    var center = lamps.Aggregate(Vector3.zero, (a, v) => a + v) / lamps.Length;
                    float floorY = Physics.Raycast(center + Vector3.down * .3f, Vector3.down, out var down, 30f) ? down.point.y : center.y - 6f;
                    float sx = lamps.Max(v => v.x) - lamps.Min(v => v.x), sz = lamps.Max(v => v.z) - lamps.Min(v => v.z);
                    var look = sz >= sx ? Vector3.forward : Vector3.right;
                    var eye = new Vector3(center.x, floorY + 1.7f, center.z);
                    var pos = Physics.Raycast(eye, -look, out var wall, 80f) ? wall.point + look * 1.2f : eye - look * 8f;
                    bool longZ = look == Vector3.forward;
                    var go = new GameObject("VP_PreviewCam"); go.hideFlags = HideFlags.DontSave;
                    var cam = go.AddComponent<Camera>(); cam.fieldOfView = 70; cam.nearClipPlane = .05f; cam.farClipPlane = 300;
                    go.transform.position = pos; go.transform.rotation = Quaternion.LookRotation(longZ ? Vector3.forward : Vector3.right) * Quaternion.Euler(4, 0, 0);
                    var data = go.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true; data.stopNaN = true; data.volumeLayerMask = 1;
                    var campaignProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VisualPassF1.CampaignProfile);
                    var baseVol = go.AddComponent<Volume>(); baseVol.isGlobal = true; baseVol.priority = 0; baseVol.sharedProfile = campaignProfile;
                    var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
                    for (int i = 0; i < 3; i++) cam.Render();
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                    RenderTexture.active = prev;
                    File.WriteAllBytes(dir + "/" + s.Scene + ".png", tex.EncodeToPNG());
                    Object.DestroyImmediate(tex); cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
                }
            }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("IRON REQUIEM VisualPass previews -> " + dir);
        }


        // Play Mode diagnostic: jump the running campaign to a sector (runtime only, nothing saved) and drop the
        // player under the sector lamps, to review lighting without clearing twelve rounds.
        [MenuItem("Tools/Iron Requiem/Visual Pass/Diag - Jump to next sector (play)")]
        public static void DiagNextSector()
        {
            if (!EditorApplication.isPlaying) return;
            var flow = Object.FindAnyObjectByType<CampaignFlow>(); if (!flow || flow.Busy) return;
            int target = Mathf.Min(flow.CurrentStage + 1, flow.StageScenes.Length - 1);
            var m = typeof(CampaignFlow).GetMethod("Prepare", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            flow.StartCoroutine((System.Collections.IEnumerator)m.Invoke(flow, new object[] { target }));
            EditorApplication.CallbackFunction wait = null; float t0 = Time.realtimeSinceStartup;
            wait = () =>
            {
                if (!EditorApplication.isPlaying || Time.realtimeSinceStartup - t0 > 30) { EditorApplication.update -= wait; return; }
                if (flow.Busy || flow.CurrentStage != target || !flow.CurrentRoom) return;
                EditorApplication.update -= wait;
                var scene = flow.CurrentRoom.gameObject.scene;
                var lamps = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)).Where(l => l.name.StartsWith("CeilingLight")).Select(l => l.transform.position).ToArray();
                if (lamps.Length == 0) return;
                var c = lamps.Aggregate(Vector3.zero, (a, v) => a + v) / lamps.Length;
                if (!Physics.Raycast(c + Vector3.down * .3f, Vector3.down, out var hit, 30f)) return;
                var player = flow.Player; var cc = player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false; player.transform.position = hit.point + Vector3.up * .2f; if (cc) cc.enabled = true;
                Debug.Log("IRON REQUIEM diag jumped to sector " + (target + 1) + " " + scene.name);
            };
            EditorApplication.update += wait;
        }


        [MenuItem("Tools/Iron Requiem/Visual Pass/Diag - What is at screen center (play)")]
        public static void DiagCenter()
        {
            var cam = Camera.main; if (!cam) return;
            var ray = cam.ViewportPointToRay(new Vector3(.5f, .62f, 0));
            var hits = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude).Where(r => r.enabled && r.bounds.IntersectRay(ray, out _))
                .Select(r => { r.bounds.IntersectRay(ray, out float d); return (d, r); }).OrderBy(x => x.d).Take(10);
            var sb = new StringBuilder("IRON REQUIEM diag center ray (viewport .5,.62):\n");
            foreach (var (d, r) in hits) sb.AppendLine(d.ToString("F1") + "m " + r.name + " scene=" + r.gameObject.scene.name + " root=" + r.transform.root.name + " mat=" + (r.sharedMaterial ? r.sharedMaterial.name : "-") + " bounds=" + r.bounds.size);
            Debug.Log(sb); File.WriteAllText(VisualPassF1.Report + "/diag-center.txt", sb.ToString());
        }

        static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }
}
