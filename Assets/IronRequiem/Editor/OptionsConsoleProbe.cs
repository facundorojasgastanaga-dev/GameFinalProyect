using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IronRequiem.EditorTools
{
    /// 16/11/2026 options console diagnostics (Play Mode only, read-only apart from simulated pointer events):
    /// opens the options panel, raycasts the real GraphicRaycaster at the centre of VOLVER and logs what is hit,
    /// then sends pointerEnter/down/up/click through the EventSystem like a mouse would and reports whether the panel closed.
    public static class OptionsConsoleProbe
    {
        const string Menu = "Tools/Iron Requiem/Options Probe/";
        const string Dir = "Tools/IronRequiem/Reports/OptionsConsole";
        static void Log(string s) { Directory.CreateDirectory(Dir); File.AppendAllText(Dir + "/log.txt", System.DateTime.Now.ToString("HH:mm:ss.fff ") + s + "\n"); Debug.Log("[OptionsProbe] " + s); }
        static RequiemMenuView View => Object.FindAnyObjectByType<RequiemMenuView>();

        [MenuItem(Menu + "Open options (play)")]
        static void Open()
        {
            // Play Mode only: in Edit Mode this would activate/deactivate objects of the open scene and mark it dirty.
            if (!EditorApplication.isPlaying) { Log("open options: Play Mode required (skipped)"); return; }
            var v = View; if (!v) { Log("no RequiemMenuView"); return; } v.OpenOptions(); Log("opened options");
        }

        [MenuItem(Menu + "Click VOLVER with pointer events (play)")]
        static void ClickBack()
        {
            if (!EditorApplication.isPlaying) { Log("click VOLVER: Play Mode required (skipped)"); return; }
            var v = View; if (!v) { Log("no RequiemMenuView"); return; }
            var so = new SerializedObject(v); var back = so.FindProperty("optionsBack").objectReferenceValue as Button;
            if (!back) { Log("no back button"); return; }
            var rt = (RectTransform)back.transform; var c = new Vector3[4]; rt.GetWorldCorners(c);
            var canvas = back.GetComponentInParent<Canvas>().rootCanvas; var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, (c[0] + c[2]) * .5f);
            var es = EventSystem.current; var data = new PointerEventData(es) { position = screen, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); es.RaycastAll(data, hits);
            var sb = new StringBuilder($"VOLVER at screen {screen} active={back.gameObject.activeInHierarchy} interactable={back.interactable} scale={rt.lossyScale.x:0.000} raycast hits:");
            foreach (var h in hits) sb.Append($" [{h.gameObject.name} <{(h.gameObject.transform.parent ? h.gameObject.transform.parent.name : "-")}>]");
            Log(sb.ToString());
            var target = hits.Count > 0 ? hits[0].gameObject : null;
            bool topIsBack = target && (target == back.gameObject || target.transform.IsChildOf(back.transform));
            Log("top hit belongs to VOLVER: " + topIsBack + " ; selected=" + (es.currentSelectedGameObject ? es.currentSelectedGameObject.name : "none"));
            if (!target) return;
            data.pointerCurrentRaycast = hits[0]; data.pointerPressRaycast = hits[0];
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerEnterHandler);
            data.pointerPress = ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerClickHandler);
            Log("after click: options active=" + v.IsOptionsOpen + " ; selected=" + (es.currentSelectedGameObject ? es.currentSelectedGameObject.name : "none") + " ; back scale=" + rt.localScale.x.ToString("0.000"));
        }

        // ------------------------------------------------------------------ virtual mouse cycles (real InputSystemUIInputModule path)
        static double nextAt; static int step, cycle; static bool running;
        static Button OptionsButton() { var so = new SerializedObject(View); return so.FindProperty("optionsButton").objectReferenceValue as Button; }
        static Button BackButton() { var so = new SerializedObject(View); return so.FindProperty("optionsBack").objectReferenceValue as Button; }
        static Vector2 ScreenCentre(Button b)
        {
            var rt = (RectTransform)b.transform; var c = new Vector3[4]; rt.GetWorldCorners(c); var canvas = b.GetComponentInParent<Canvas>().rootCanvas;
            return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, (c[0] + c[2]) * .5f);
        }
        static void MouseTo(Vector2 p, bool down) => UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, new UnityEngine.InputSystem.LowLevel.MouseState { position = p, buttons = (ushort)(down ? 1 : 0) });
        static string Hit(Vector2 p)
        {
            var es = EventSystem.current; var hits = new List<RaycastResult>(); es.RaycastAll(new PointerEventData(es) { position = p }, hits);
            return hits.Count > 0 ? hits[0].gameObject.name : "none";
        }

        [MenuItem(Menu + "Mouse cycles OPCIONES -> VOLVER x4 (play)")]
        static void StartCycles()
        {
            if (!EditorApplication.isPlaying || !View) { Log("cycles: need Play Mode with menu"); return; }
            if (UnityEngine.InputSystem.Mouse.current == null) UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            running = true; step = 0; cycle = 0; nextAt = EditorApplication.timeSinceStartup; EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Log("cycles: started (virtual mouse), options open=" + View.IsOptionsOpen);
        }
        static void Tick()
        {
            if (!running || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextAt) return;
            var v = View; if (!v) { running = false; return; }
            var es = EventSystem.current;
            switch (step++)
            {
                case 0: { var b = OptionsButton(); var p = ScreenCentre(b); MouseTo(p, false); Log($"#{cycle} move to OPCIONES {p} top hit={Hit(p)} selected={es.currentSelectedGameObject?.name}"); nextAt = EditorApplication.timeSinceStartup + .3; break; }
                case 1: { var p = ScreenCentre(OptionsButton()); MouseTo(p, true); nextAt = EditorApplication.timeSinceStartup + .12; break; }
                case 2: { var p = ScreenCentre(OptionsButton()); MouseTo(p, false); nextAt = EditorApplication.timeSinceStartup + .6; break; }
                case 3: { Log($"#{cycle} after OPCIONES click: options open={v.IsOptionsOpen}"); var b = BackButton(); if (!v.IsOptionsOpen) { step = 8; break; } var p = ScreenCentre(b); MouseTo(p, false); Log($"#{cycle} move to VOLVER {p} top hit={Hit(p)} selected={es.currentSelectedGameObject?.name} scale={b.transform.localScale.x:0.000}"); nextAt = EditorApplication.timeSinceStartup + .4; break; }
                case 4: { MouseTo(ScreenCentre(BackButton()), true); nextAt = EditorApplication.timeSinceStartup + .12; break; }
                case 5: { MouseTo(ScreenCentre(BackButton()), false); nextAt = EditorApplication.timeSinceStartup + .6; break; }
                case 6: { var b = BackButton(); Log($"#{cycle} after VOLVER click: options open={v.IsOptionsOpen} selected={es.currentSelectedGameObject?.name} backScale={b.transform.localScale.x:0.000} backInteractable={b.interactable}"); nextAt = EditorApplication.timeSinceStartup + .2; break; }
                default: cycle++; step = 0; if (cycle >= 4) { running = false; EditorApplication.update -= Tick; MouseTo(Vector2.zero, false); Log("cycles: done"); } break;
            }
        }

        // virtual-mouse press on the fullscreen switch (real raycast + Toggle.OnPointerClick); logs the value before/after
        static int swStep; static double swAt;
        [MenuItem(Menu + "Click fullscreen switch with virtual mouse (play)")]
        static void ClickSwitch()
        {
            var v = View; if (!v || !EditorApplication.isPlaying) { Log("switch: need Play Mode with menu"); return; }
            if (UnityEngine.InputSystem.Mouse.current == null) UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            swStep = 0; swAt = EditorApplication.timeSinceStartup; EditorApplication.update -= SwitchTick; EditorApplication.update += SwitchTick;
        }
        static void SwitchTick()
        {
            if (EditorApplication.timeSinceStartup < swAt) return; var v = View; if (!v) { EditorApplication.update -= SwitchTick; return; }
            var t = new SerializedObject(v).FindProperty("fullscreen").objectReferenceValue as Toggle; var g = (RectTransform)t.targetGraphic.transform; var c = new Vector3[4]; g.GetWorldCorners(c);
            var p = RectTransformUtility.WorldToScreenPoint(null, (c[0] + c[2]) * .5f);
            switch (swStep++)
            {
                case 0: MouseTo(p, false); Log($"switch: before isOn={t.isOn} top hit={Hit(p)}"); swAt = EditorApplication.timeSinceStartup + .3; break;
                case 1: MouseTo(p, true); swAt = EditorApplication.timeSinceStartup + .1; break;
                case 2: MouseTo(p, false); swAt = EditorApplication.timeSinceStartup + .5; break;
                default: Log($"switch: after click isOn={t.isOn}"); MouseTo(Vector2.zero, false); EditorApplication.update -= SwitchTick; break;
            }
        }

        // Renders the security camera by hand for each "x z" offset listed in Reports/OptionsConsole/offsets.txt (one per line)
        // and saves cctv_try_<i>.png (640x400). Uses the real CctvActorFitter so what is rendered is what the game will do.
        [MenuItem(Menu + "Render CCTV for listed offsets (play)")]
        static void RenderOffsets()
        {
            if (!EditorApplication.isPlaying) { Log("cctv render: Play Mode required"); return; }
            var feed = CctvFeed.Active; if (!feed) { Log("cctv render: no feed"); return; }
            var file = Dir + "/offsets.txt"; if (!File.Exists(file)) { Log("cctv render: offsets.txt missing"); return; }
            var original = CctvActorFitter.Offset; int i = 0;
            foreach (var line in File.ReadAllLines(file))
            {
                var p = line.Split(new[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries); if (p.Length < 2) continue;
                CctvActorFitter.Offset = new Vector3(float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), 0, float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture));
                CctvActorFitter.Apply();
                var rt = RenderTexture.GetTemporary(640, 400, 24); var tex = new Texture2D(640, 400, TextureFormat.RGB24, false); var prev = RenderTexture.active;
                feed.Camera.targetTexture = rt; feed.Camera.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 640, 400), 0, 0); tex.Apply();
                feed.Camera.targetTexture = null; RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                File.WriteAllBytes($"{Dir}/cctv_try_{i}.png", tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                Log($"cctv render {i}: offset {CctvActorFitter.Offset}"); i++;
            }
            CctvActorFitter.Offset = original; CctvActorFitter.Apply();
        }

        [MenuItem(Menu + "Dump CCTV layout (play)")]
        static void DumpCctv()
        {
            if (!EditorApplication.isPlaying) { Log("cctv: Play Mode required"); return; }
            var sb = new StringBuilder();
            var feed = CctvFeed.Active; sb.AppendLine("feed=" + (feed ? feed.name + " pos=" + feed.transform.position + " euler=" + feed.transform.eulerAngles + " fov=" + feed.Camera.fieldOfView : "none"));
            Transform pod = null; foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)) if (t.name == "Capsule_C04") { pod = t; break; }
            if (pod)
            {
                sb.AppendLine($"pod pos={pod.position} euler={pod.eulerAngles} scale={pod.lossyScale}");
                foreach (var r in pod.GetComponentsInChildren<Renderer>(true)) sb.AppendLine($"   pod renderer {r.name} layer={r.gameObject.layer} center={r.bounds.center:0.00} size={r.bounds.size:0.00}");
            }
            foreach (var a in Object.FindObjectsByType<CctvActor>(FindObjectsInactive.Include))
            {
                sb.AppendLine($"actor {a.name} pos={a.transform.position} euler={a.transform.eulerAngles} forward={a.transform.forward}");
                foreach (var r in a.GetComponentsInChildren<Renderer>(true)) sb.AppendLine($"   actor renderer {r.name} type={r.GetType().Name} center={r.bounds.center:0.00} size={r.bounds.size:0.00}");
            }
            Directory.CreateDirectory(Dir); File.WriteAllText(Dir + "/cctv.txt", sb.ToString()); Log("cctv dump written");
        }

        [MenuItem(Menu + "Dump button graphics (play)")]
        static void DumpButtons()
        {
            if (!EditorApplication.isPlaying) { Log("dump: Play Mode required"); return; }
            var sb = new StringBuilder();
            foreach (var fx in Object.FindObjectsByType<HangarButtonFx>(FindObjectsInactive.Exclude))
            {
                sb.AppendLine("BUTTON " + PathOf(fx.transform) + " size=" + ((RectTransform)fx.transform).rect.size);
                foreach (var g in fx.GetComponentsInChildren<Graphic>(true))
                {
                    var img = g as Image;
                    sb.AppendLine($"   {PathOf(g.transform).Replace(PathOf(fx.transform), "")} <{g.GetType().Name}> enabled={g.enabled} active={g.gameObject.activeInHierarchy} color={g.color} sprite={(img && img.sprite ? img.sprite.name : "-")} style={(g is IndustrialPanelGraphic ip ? ip.Style.ToString() : "-")}");
                }
            }
            Directory.CreateDirectory(Dir); File.WriteAllText(Dir + "/buttons.txt", sb.ToString()); Log("dump written (" + sb.Length + " chars)");
        }
        static string PathOf(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }

        [MenuItem(Menu + "Toggle fullscreen lever (play)")]
        static void ToggleLever()
        {
            if (!EditorApplication.isPlaying) { Log("toggle: Play Mode required (skipped)"); return; }
            var v = View; if (!v) return; var so = new SerializedObject(v); var t = so.FindProperty("fullscreen").objectReferenceValue as Toggle; if (!t) { Log("no toggle"); return; }
            t.SetIsOnWithoutNotify(!t.isOn); t.graphic?.CrossFadeAlpha(t.isOn ? 1 : 0, 0, true);
            Log("lever isOn=" + t.isOn + " targetGraphic=" + (t.targetGraphic ? t.targetGraphic.name : "-") + " graphic=" + (t.graphic ? t.graphic.name : "-"));
        }
    }
}
