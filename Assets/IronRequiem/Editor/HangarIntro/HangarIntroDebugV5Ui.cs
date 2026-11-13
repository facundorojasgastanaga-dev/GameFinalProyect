using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// v5 UI verification through the file bridge: a runtime report of every live canvas, and captures of each screen
    /// (main menu, options, credits, pause, pause options, death, HUD states, banners, prompts) rendered off-screen at
    /// real resolutions (1280x720 / 1920x1080 / 2560x1440) by temporarily drawing the overlay canvases through the
    /// game camera into a render texture. Also drives VOLVER and Escape on the options screens. Play mode only.
    public static class HangarIntroDebugV5Ui
    {
        const string Menu = "Tools/Iron Requiem/Hangar Intro/Debug/";
        static string Dir => "Tools/IronRequiem/Bridge/v5ui/" + EditorPrefs.GetString("IR_V5UiTag", "after");
        [MenuItem(Menu + "V5 UI capture tag = before")] static void TagBefore() => EditorPrefs.SetString("IR_V5UiTag", "before");
        [MenuItem(Menu + "V5 UI capture tag = after")] static void TagAfter() => EditorPrefs.SetString("IR_V5UiTag", "after");
        const string ReportFile = "Tools/IronRequiem/Reports/HangarIntro/v5_ui.txt";
        static readonly Vector2Int P720 = new Vector2Int(1280, 720), P1080 = new Vector2Int(1920, 1080), P1440 = new Vector2Int(2560, 1440);
        static readonly List<(double at, Action act)> queue = new List<(double, Action)>();
        static readonly MethodInfo ScalerHandle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);

        static void Log(string line) { Directory.CreateDirectory(Path.GetDirectoryName(ReportFile)); File.AppendAllText(ReportFile, DateTime.Now.ToString("HH:mm:ss ") + line + "\n"); Debug.Log("[V5UI] " + line); }
        static void Schedule(double delay, Action act) { queue.Add((EditorApplication.timeSinceStartup + delay, act)); EditorApplication.update -= Tick; EditorApplication.update += Tick; }
        static void Tick()
        {
            if (!EditorApplication.isPlaying) { queue.Clear(); EditorApplication.update -= Tick; return; }
            queue.Sort((a, b) => a.at.CompareTo(b.at));
            // one due step per editor update, so a key press and its release never land in the same frame
            if (queue.Count > 0 && EditorApplication.timeSinceStartup >= queue[0].at)
            {
                var a = queue[0].act; queue.RemoveAt(0); try { a(); } catch (Exception e) { Log("ERROR " + e); }
                double now = EditorApplication.timeSinceStartup; // keep the remaining spacing after a slow capture
                if (queue.Count > 0 && queue[0].at < now) { double shift = now - queue[0].at + .05; for (int i = 0; i < queue.Count; i++) queue[i] = (queue[i].at + shift, queue[i].act); }
            }
            if (queue.Count == 0) EditorApplication.update -= Tick;
        }
        static void Press(params Key[] keys) { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys)); }

        // ---------------------------------------------------------------- report
        [MenuItem(Menu + "V5 UI runtime report (play)")]
        static void Report()
        {
            var sb = new StringBuilder("runtime UI " + DateTime.Now + "\n");
            foreach (var m in Object.FindObjectsByType<InGameMenuManager>(FindObjectsInactive.Include))
                sb.AppendLine($"InGameMenuManager {PathOf(m.transform)} scene={m.gameObject.scene.name} active={m.isActiveAndEnabled} industrial={m.UseIndustrialMenu} root={(m.MenuRoot ? PathOf(m.MenuRoot.transform) : "-")}");
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(c => c.isRootCanvas).OrderBy(c => c.sortingOrder))
            {
                var s = c.GetComponent<CanvasScaler>();
                sb.AppendLine($"=== {PathOf(c.transform)} scene={c.gameObject.scene.name} mode={c.renderMode} sort={c.sortingOrder} enabled={c.enabled}" + (s ? $" scaler={s.uiScaleMode} ref={s.referenceResolution}" : ""));
                Dump(c.transform, 1, sb);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportFile));
            File.WriteAllText("Tools/IronRequiem/Reports/HangarIntro/ui_runtime.txt", sb.ToString());
            Log("runtime report written (" + sb.Length + " chars)");
        }
        static string PathOf(Transform t) { var p = t.name; while (t.parent) { t = t.parent; p = t.name + "/" + p; } return p; }
        static void Dump(Transform t, int depth, StringBuilder sb)
        {
            if (!t.gameObject.activeInHierarchy || depth > 8) return;
            var line = new StringBuilder(new string(' ', depth * 2) + t.name);
            var tx = t.GetComponent<TMP_Text>(); if (tx) line.Append($" [TMP '{tx.text.Replace("\n", "\\n")}' {(tx.font ? tx.font.name : "-")} {tx.fontSize:0} #{ColorUtility.ToHtmlStringRGBA(tx.color)}]");
            if (t.GetComponent<Button>()) line.Append(" [Button]");
            if (t.GetComponent<HangarButtonFx>()) line.Append(" [Fx]");
            var g = t.GetComponent<CanvasGroup>(); if (g) line.Append($" [alpha {g.alpha:0.00}]");
            sb.AppendLine(line.ToString());
            for (int i = 0; i < t.childCount; i++) Dump(t.GetChild(i), depth + 1, sb);
        }

        // ---------------------------------------------------------------- capture
        static Camera GameCamera()
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude).Where(c => c.enabled && !c.targetTexture && c.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Base).ToArray();
            var main = Camera.main; if (main && cams.Contains(main)) return main;
            return cams.OrderByDescending(c => c.depth).FirstOrDefault();
        }
        /// Renders the game view and every live overlay canvas at each requested size into Dir/name_WxH.png.
        public static void Capture(string name, params Vector2Int[] sizes)
        {
            var cam = GameCamera(); if (!cam) { Log("capture " + name + ": no camera"); return; }
            var data = cam.GetUniversalAdditionalCameraData();
            // the UI is drawn by the base camera; the first-person overlay (hands / weapon) is left out of UI captures
            // (screen-space-camera canvases on the overlay camera did not render through Camera.Render)
            var top = cam; var stackOff = data.cameraStack != null ? data.cameraStack.Where(c => c && c.enabled).ToArray() : new Camera[0];
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(c => c.isRootCanvas && c.enabled && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            int mask = top.cullingMask; var target = cam.targetTexture; float plane = Mathf.Min(Mathf.Max(top.nearClipPlane + .05f, .3f), top.farClipPlane * .5f); // TMP SDF needs a sane canvas scale
            Directory.CreateDirectory(Dir);
            try
            {
                int uiLayers = 1 << 5; foreach (var c in canvases) foreach (var g in c.GetComponentsInChildren<Graphic>()) uiLayers |= 1 << g.gameObject.layer;
                top.cullingMask = mask | uiLayers; foreach (var o in stackOff) o.enabled = false;
                foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = top; c.planeDistance = plane; }
                foreach (var size in sizes)
                {
                    var rt = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32); var tex = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    var prev = RenderTexture.active;
                    try
                    {
                        cam.targetTexture = rt;
                        for (int pass = 0; pass < 2; pass++)
                        {
                            foreach (var c in canvases) { var s = c.GetComponent<CanvasScaler>(); if (s && ScalerHandle != null) ScalerHandle.Invoke(s, null); }
                            Canvas.ForceUpdateCanvases();
                            foreach (var f in Object.FindObjectsByType<ScreenFitPanel>(FindObjectsInactive.Exclude)) f.Refit();
                            foreach (var c in canvases) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)c.transform);
                        }
                        cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); tex.Apply();
                        File.WriteAllBytes(Path.Combine(Dir, $"{name}_{size.x}x{size.y}.png"), tex.EncodeToPNG());
                    }
                    finally { RenderTexture.active = prev; cam.targetTexture = target; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(tex); }
                }
            }
            finally
            {
                top.cullingMask = mask; foreach (var o in stackOff) if (o) o.enabled = true;
                foreach (var c in canvases) if (c) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; var s = c.GetComponent<CanvasScaler>(); if (s && ScalerHandle != null) ScalerHandle.Invoke(s, null); }
                Canvas.ForceUpdateCanvases();
            }
            Log("captured " + name + " (" + string.Join(", ", sizes.Select(s => s.y + "p")) + ") cam=" + cam.name + " top=" + top.name + " canvases=" + canvases.Length + " layers=" + string.Join(",", canvases.Select(c => LayerMask.LayerToName(c.gameObject.layer)).Distinct()));
        }

        static RequiemMenuView View(Func<RequiemMenuView, bool> pick) => Object.FindObjectsByType<RequiemMenuView>(FindObjectsInactive.Include).FirstOrDefault(pick);
        static T Field<T>(Object o, string name) where T : class => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(o) as T;
        static string OptionsInfo(RequiemMenuView v)
        {
            var options = Field<GameObject>(v, "options"); if (!options) return "options=-";
            var console = options.transform.Find("OptionsConsole") as RectTransform; var canvas = options.GetComponentInParent<Canvas>().rootCanvas;
            if (!console) return "options console=- (not wrapped)";
            var corners = new Vector3[4]; console.GetWorldCorners(corners);
            var cr = (RectTransform)canvas.transform; float h = (corners[1].y - corners[0].y) / cr.lossyScale.y, w = (corners[2].x - corners[1].x) / cr.lossyScale.x;
            return $"options console {w:0}x{h:0} of canvas {cr.rect.width:0}x{cr.rect.height:0} = {h / cr.rect.height:P0} height, centre=({(corners[0].x + corners[2].x) / 2 / cr.lossyScale.x - cr.rect.width / 2:0},{(corners[0].y + corners[2].y) / 2 / cr.lossyScale.y - cr.rect.height / 2:0})";
        }

        // ---------------------------------------------------------------- main menu (IntroMenu)
        [MenuItem(Menu + "V5 UI capture main menu (play IntroMenu)")]
        static void MainMenu()
        {
            var v = View(x => x.gameObject.scene.name == "IntroMenu"); if (!v) { Log("main menu: no RequiemMenuView in IntroMenu"); return; }
            var options = Field<GameObject>(v, "options"); var home = Field<GameObject>(v, "home"); var credits = Field<GameObject>(v, "credits");
            var back = Field<Button>(v, "optionsBack");
            Schedule(.2, () => Capture("menu_home", P1080, P1440));
            Schedule(.6, () => { v.OpenOptions(); });
            Schedule(1.2, () => { Log("main menu options open=" + options.activeSelf + " · " + OptionsInfo(v)); Capture("menu_options", P720, P1080, P1440); });
            Schedule(1.6, () => { back.onClick.Invoke(); });
            Schedule(2.0, () => Log($"VOLVER -> home={home.activeSelf} options={options.activeSelf}"));
            Schedule(2.2, () => v.OpenOptions());
            Schedule(2.6, () => Press(UnityEngine.InputSystem.Key.Escape));
            Schedule(2.95, () => Press());
            Schedule(3.1, () => Log($"Escape -> home={home.activeSelf} options={options.activeSelf}"));
            Schedule(3.3, () => { if (credits) v.OpenCredits(); });
            Schedule(3.8, () => Capture("menu_credits", P1080, P1440));
            Schedule(4.0, () => Press(UnityEngine.InputSystem.Key.Escape));
            Schedule(4.35, () => Press());
            Schedule(4.5, () => Log($"Escape on credits -> home={home.activeSelf} credits={(credits ? credits.activeSelf : false)}"));
        }

        // ---------------------------------------------------------------- campaign screens
        [MenuItem(Menu + "V5 UI capture campaign (play)")]
        static void Campaign()
        {
            var player = Object.FindAnyObjectByType<PlayerCharacterController>(); if (!player) { Log("campaign: no player"); return; }
            var flow = Object.FindAnyObjectByType<CampaignFlow>(); var melee = player.GetComponent<ProtagonistMelee>();
            var pause = Object.FindObjectsByType<InGameMenuManager>(FindObjectsInactive.Exclude).FirstOrDefault(m => m.isActiveAndEnabled && m.UseIndustrialMenu);
            var station = Object.FindAnyObjectByType<WeaponMountStation>();
            double t = .2;
            Schedule(t, () => Capture("hud_unarmed", P1080, P1440)); t += .5;
            if (station && !station.Granted)
            {
                Schedule(t, () => { Place(player, station.transform.position - station.transform.forward * .5f + Vector3.up * .03f, station.transform.eulerAngles.y); }); t += .8;
                Schedule(t, () => { Log("prompt: '" + (flow && flow.Message ? flow.Message.text : "-") + "'"); Capture("prompt_station", P1080, P1440); }); t += .3;
                Schedule(t, () => station.Begin()); t += 1.4;
                Schedule(t, () => { Log("station banner: '" + (flow && flow.Message ? flow.Message.text : "-") + "'"); Capture("station_sequence", P1080); }); t += .2;
                Schedule(t, () => station.Skip()); t += .8;
                Schedule(t, () => { Log("after mount: '" + (flow && flow.Message ? flow.Message.text : "-") + "'"); Capture("hud_cannon_mounted", P1080, P1440); }); t += .4;
            }
            Schedule(t, () => Press(UnityEngine.InputSystem.Key.F)); t += .9;
            Schedule(t, () => { Log($"fist charge charging={melee && melee.IsCharging} level={(melee ? melee.ChargeLevel : 0):0.00}"); Capture("hud_fist_charge", P1080, P1440); }); t += .2;
            Schedule(t, () => Press()); t += .8;
            if (pause)
            {
                var view = View(x => Field<InGameMenuManager>(x, "pauseOwner") == pause);
                Schedule(t, () => pause.SetPauseMenuActivation(true)); t += .7;
                Schedule(t, () => Capture("pause_home", P1080, P1440)); t += .3;
                if (view)
                {
                    var options = Field<GameObject>(view, "options"); var back = Field<Button>(view, "optionsBack");
                    Schedule(t, () => view.OpenOptions()); t += .6;
                    Schedule(t, () => { Log("pause options open=" + options.activeSelf + " · " + OptionsInfo(view)); Capture("pause_options", P720, P1080, P1440); }); t += .3;
                    Schedule(t, () => Press(UnityEngine.InputSystem.Key.Escape)); t += .35;
                    Schedule(t, () => Press()); t += .4;
                    Schedule(t, () => Log($"pause Escape on options -> options={options.activeSelf} paused={pause.IsPaused}")); t += .2;
                    Schedule(t, () => view.OpenOptions()); t += .4;
                    Schedule(t, () => back.onClick.Invoke()); t += .4;
                    Schedule(t, () => Log($"pause VOLVER -> options={options.activeSelf} paused={pause.IsPaused}")); t += .2;
                }
                else Log("pause: no RequiemMenuView bound to the pause manager");
                Schedule(t, () => pause.SetPauseMenuActivation(false)); t += .5;
            }
            else Log("pause: no industrial InGameMenuManager active");
            if (flow && flow.Message)
            {
                string keep = null;
                Schedule(t, () => { keep = flow.Message.text; flow.Message.text = "SECTOR 1 LISTO · TRES RONDAS"; }); t += .5;
                Schedule(t, () => Capture("banner_round", P1080, P1440)); t += .2;
                Schedule(t, () => flow.Message.text = "ALERTA · INTRUSIÓN DETECTADA"); t += .5;
                Schedule(t, () => Capture("banner_alert", P1080, P1440)); t += .2;
                Schedule(t, () => flow.Message.text = keep ?? ""); t += .3;
            }
            Schedule(t, () => { var h = player.GetComponent<Health>(); if (h) h.Kill(); }); t += 1.2;
            Schedule(t, () => Capture("death", P1080, P1440)); t += .3;
            Schedule(t, () => Log("campaign UI capture done"));
        }

        [MenuItem(Menu + "V5 UI capture cinematic skip prompt (play)")]
        static void SkipPrompt()
        {
            var cine = Object.FindAnyObjectByType<HangarIntroCinematic>(); if (!cine) { Log("skip prompt: no cinematic"); return; }
            var prompt = Field<CanvasGroup>(cine, "skipPrompt"); if (!prompt) { Log("skip prompt: none"); return; }
            prompt.alpha = 1;
            foreach (var t in prompt.GetComponentsInChildren<TMP_Text>(true)) Log($"skip prompt text '{t.name}' active={t.isActiveAndEnabled} text='{t.text}' font={(t.font ? t.font.name : "-")} size={t.fontSize} color={t.color} rect={t.rectTransform.rect}");
            Capture("prompt_skip_cinematic", P1080, P1440);
        }

        static void Place(PlayerCharacterController player, Vector3 p, float yaw)
        {
            var move = player.GetComponent<AdvancedPlayerMovement>(); if (move) move.ResetForCheckpoint();
            player.TeleportToCheckpoint(p); player.transform.rotation = Quaternion.Euler(0, yaw, 0); player.PlayerCamera.transform.localRotation = Quaternion.identity; Physics.SyncTransforms();
        }
    }
}
