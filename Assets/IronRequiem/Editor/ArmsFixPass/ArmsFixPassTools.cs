using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// 19/11/2026 arms fix pass (station, weapon switch, T-07 execution): measurement and capture tools only.
    /// Play Mode only for runs; nothing is saved. Play entry helpers set the Play start scene and restore it
    /// (and the open scenes) when Play stops. Results: Tools/IronRequiem/Reports/ArmsFixPass/<tag>/.
    public static partial class ArmsFixPassTools
    {
        const string Menu = "Tools/Iron Requiem/Arms Fix Pass/";
        const string Root = "Tools/IronRequiem/Reports/ArmsFixPass";
        const string Key = "IRON.ArmsFixPass";
        internal static string Tag => EditorPrefs.GetString("IR_ArmsFixTag", "before");
        internal static string Dir => Root + "/" + Tag;
        [MenuItem(Menu + "Tag = before")] static void TagBefore() => EditorPrefs.SetString("IR_ArmsFixTag", "before");
        [MenuItem(Menu + "Tag = after")] static void TagAfter() => EditorPrefs.SetString("IR_ArmsFixTag", "after");

        static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        internal static T Field<T>(object o, string name) { var f = o?.GetType().GetField(name, Any); return f != null && f.GetValue(o) is T v ? v : default; }
        internal static void Log(string line) { Directory.CreateDirectory(Dir); File.AppendAllText(Dir + "/log.txt", DateTime.Now.ToString("HH:mm:ss ") + line + "\n"); Debug.Log("[ArmsFix] " + line); }
        internal static PlayerCharacterController Player => Object.FindAnyObjectByType<PlayerCharacterController>();
        internal static void Keys(params UnityEngine.InputSystem.Key[] keys) { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys)); }
        static void RequirePlay() { if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required."); }
        internal static string F(float v) => v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ play entry (restores start scene + setup)
        const string Campaign = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        const string Map01S1 = "Assets/IronRequiem/Scenes/Map01_Bunker/Map01_Campaign_S1.unity";
        [MenuItem(Menu + "Play RequiemCampaign (lobby station)")] static void PlayCampaign() => PlayScene(Campaign);
        [MenuItem(Menu + "Play Map01 S1 (vestibule station)")] static void PlayMap01() => PlayScene(Map01S1);

        internal static void PlayScene(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Already in Play Mode");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var s = EditorSceneManager.GetSceneAt(i);
                if (s.isDirty || string.IsNullOrEmpty(s.path)) throw new InvalidOperationException("Open scene has unsaved changes: " + s.path);
            }
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path) ?? throw new InvalidOperationException("Missing " + path);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            SessionState.SetString(Key + ".restore", string.Join("\n", setup.Select(s => $"{s.path}\t{(s.isLoaded ? 1 : 0)}\t{(s.isActive ? 1 : 0)}")));
            var start = EditorSceneManager.playModeStartScene;
            SessionState.SetString(Key + ".start", start ? AssetDatabase.GetAssetPath(start) : "");
            SessionState.SetBool(Key + ".pending", true); SessionState.SetBool(Key + ".entered", false);
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            EditorSceneManager.playModeStartScene = asset;
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Hook() { EditorApplication.update -= RestoreTick; EditorApplication.update += RestoreTick; }
        static void RestoreTick()
        {
            if (!SessionState.GetBool(Key + ".pending", false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.SetBool(Key + ".entered", true); return; }
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || !SessionState.GetBool(Key + ".entered", false)) return;
            var startPath = SessionState.GetString(Key + ".start", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(startPath) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(startPath);
            var setup = SessionState.GetString(Key + ".restore", "").Split('\n').Where(l => l.Contains('\t')).Select(l => l.Split('\t'))
                .Select(p => new SceneSetup { path = p[0], isLoaded = p[1] == "1", isActive = p[2] == "1" }).ToArray();
            try { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); SessionState.SetBool(Key + ".pending", false); }
            catch (Exception e) { Debug.LogWarning("[ArmsFix] restore deferred: " + e.Message); }
        }

        // ------------------------------------------------------------------ rendering helpers
        internal static Camera TempCamera(string name, Camera from)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; var c = go.AddComponent<Camera>(); c.CopyFrom(from); c.enabled = false;
            var data = go.AddComponent<UniversalAdditionalCameraData>(); data.renderType = CameraRenderType.Base; data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None;
            c.clearFlags = CameraClearFlags.SolidColor; return c;
        }
        internal static Texture2D RenderCam(Camera cam, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24); var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var prev = RenderTexture.active; var target = cam.targetTexture;
            try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); }
            finally { cam.targetTexture = target; RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); }
            return tex;
        }
        static Color[] RenderTwice(Camera c, int w, int h, out Color[] white)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32); var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); var prev = RenderTexture.active;
            try
            {
                c.targetTexture = rt;
                c.backgroundColor = Color.black; c.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); var black = tex.GetPixels();
                c.backgroundColor = Color.white; c.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); white = tex.GetPixels();
                return black;
            }
            finally { c.targetTexture = null; RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(tex); }
        }
        static Color[] Overlay(Camera from, int mask, int w, int h)
        {
            var cam = TempCamera("ArmsFixOverlay", from); cam.cullingMask = mask;
            try
            {
                var black = RenderTwice(cam, w, h, out var white); var px = new Color[black.Length];
                for (int i = 0; i < px.Length; i++)
                {
                    float a = Mathf.Clamp01(1 - ((white[i].r - black[i].r) + (white[i].g - black[i].g) + (white[i].b - black[i].b)) / 3f);
                    px[i] = a > .002f ? new Color(black[i].r / a, black[i].g / a, black[i].b / a, a) : new Color(0, 0, 0, 0);
                }
                return px;
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }
        /// What the player sees: world camera + first-person weapon overlay.
        internal static Texture2D FpsShot(int w = 1280, int h = 720)
        {
            var p = Player; var cam = p.PlayerCamera; var weapons = p.GetComponent<PlayerWeaponsManager>();
            var tex = RenderCam(cam, w, h); var g = tex.GetPixels();
            var src = weapons && weapons.WeaponCamera ? weapons.WeaponCamera : cam;
            var vm = Overlay(src, weapons && weapons.WeaponCamera ? weapons.WeaponCamera.cullingMask : (weapons ? (int)weapons.FpsWeaponLayer : 0), w, h);
            for (int i = 0; i < g.Length; i++) g[i] = Color.Lerp(g[i], new Color(vm[i].r, vm[i].g, vm[i].b, 1), vm[i].a);
            tex.SetPixels(g); tex.Apply(); return tex;
        }
        internal static void Save(Texture2D tex, string sub, string name)
        {
            Directory.CreateDirectory(Dir + "/" + sub); File.WriteAllBytes($"{Dir}/{sub}/{name}.png", tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        }
        /// Fraction of the player's frame covered by the given renderers (rendered alone on layer 31 with the FPS camera).
        internal static float Coverage(Camera fps, IEnumerable<Renderer> renderers, int w = 320, int h = 180)
        {
            var list = renderers.Where(r => r && r.enabled && r.gameObject.activeInHierarchy).ToList(); if (list.Count == 0) return 0;
            var moved = list.Select(r => (r.gameObject, r.gameObject.layer)).ToList();
            foreach (var m in moved) m.Item1.layer = 31;
            try
            {
                var px = Overlay(fps, 1 << 31, w, h); int n = 0; foreach (var c in px) if (c.a > .5f) n++;
                return n / (float)px.Length;
            }
            finally { foreach (var m in moved) if (m.Item1) m.Item1.layer = m.Item2; }
        }
        /// Oriented-box test of each renderer's mesh bounds against solid scene colliders that are not part of `owner`/player.
        internal static List<string> WallHits(IEnumerable<Renderer> renderers, Transform owner, Transform player)
        {
            var hits = new List<string>(); var buf = new Collider[16];
            foreach (var r in renderers)
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                Bounds lb = r is SkinnedMeshRenderer smr ? smr.localBounds : r.GetComponent<MeshFilter>() is MeshFilter mf && mf.sharedMesh ? mf.sharedMesh.bounds : new Bounds();
                if (lb.size == Vector3.zero) continue;
                var t = r.transform; var half = Vector3.Scale(lb.extents, t.lossyScale); half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z)) * .9f;
                int n = Physics.OverlapBoxNonAlloc(t.TransformPoint(lb.center), half, buf, t.rotation, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var c = buf[i]; if (!c || c.transform.IsChildOf(owner) || (player && c.transform.IsChildOf(player))) continue;
                    hits.Add($"{r.name}->{c.name}");
                }
            }
            return hits;
        }
    }
}
