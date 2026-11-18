using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// 16/11/2026 station + HUD pass: read-only measurement and capture tools (Play Mode only, nothing saved).
    /// - Station: starts the montage station through the real E key, slows time, logs per-frame telemetry of the
    ///   forearm, the cannon proxy and the real cannon (camera space) and captures the key moments.
    /// - HUD: renders the game (with the first-person overlay), the UI and a viewmodel-only mask at several
    ///   resolutions and measures how much of the viewmodel the bottom status bar covers.
    public static class StationHudPassTools
    {
        const string Menu = "Tools/Iron Requiem/Station HUD Pass/";
        const string Root = "Tools/IronRequiem/Reports/StationHud";
        static string Tag => EditorPrefs.GetString("IR_StationHudTag", "before");
        static string Dir => Root + "/" + Tag;
        [MenuItem(Menu + "Tag = before")] static void TagBefore() => EditorPrefs.SetString("IR_StationHudTag", "before");
        [MenuItem(Menu + "Tag = after")] static void TagAfter() => EditorPrefs.SetString("IR_StationHudTag", "after");
        [MenuItem(Menu + "Tag = after-text")] static void TagAfterText() => EditorPrefs.SetString("IR_StationHudTag", "after-text");

        static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static T Field<T>(object o, string name) { var f = o?.GetType().GetField(name, Any); return f != null ? (T)f.GetValue(o) : default; }
        static void Log(string line) { Directory.CreateDirectory(Dir); File.AppendAllText(Dir + "/log.txt", DateTime.Now.ToString("HH:mm:ss ") + line + "\n"); Debug.Log("[StationHud] " + line); }
        static PlayerCharacterController Player => Object.FindAnyObjectByType<PlayerCharacterController>();
        static void Keys(params Key[] keys) { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys)); }
        static void RequirePlay() { if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required."); }

        // ------------------------------------------------------------------ station
        static WeaponMountStation station; static StringBuilder csv; static readonly List<float> shots = new List<float>(); static int shotIndex;
        static float lastT = -1; static float slow = .1f;

        [MenuItem(Menu + "Station sequence: telemetry + captures (play)")]
        static void StationRun()
        {
            RequirePlay();
            station = Object.FindAnyObjectByType<WeaponMountStation>(); var player = Player;
            if (!station || !player) { Log("station or player missing"); return; }
            if (station.Granted) { Log("station already granted; restart the campaign first"); return; }
            var move = player.GetComponent<AdvancedPlayerMovement>(); if (move) move.ResetForCheckpoint();
            player.TeleportToCheckpoint(station.transform.position - station.transform.forward * .5f + Vector3.up * .03f);
            player.transform.rotation = Quaternion.Euler(0, station.transform.eulerAngles.y, 0); player.PlayerCamera.transform.localRotation = Quaternion.identity; Physics.SyncTransforms();
            Directory.CreateDirectory(Dir);
            csv = new StringBuilder("t,weight,detached,handX,handY,handZ,forearmOn,proxyOn,proxyAX,proxyAY,proxyAZ,proxyFX,proxyFY,proxyFZ,realOn,realAX,realAY,realAZ,realFX,realFY,realFZ,camYaw,camPitch,fov,wfov\n");
            shots.Clear(); shots.AddRange(new[] { .3f, .75f, 1.2f, 1.55f, 1.75f, 2.1f, 2.45f, 2.8f, 3.05f, 3.3f, 3.5f, 3.65f, 3.8f, 3.9f, 4.1f, 4.3f, 4.5f, 4.58f, 4.62f, 4.7f, 4.8f, 4.9f, 5.1f, 5.4f });
            shotIndex = 0; lastT = -1;
            Time.timeScale = slow;
            Log("station run: pressing E (real input) at slow time " + slow);
            double pressAt = EditorApplication.timeSinceStartup + .4; double releaseAt = pressAt + .15;
            EditorApplication.CallbackFunction tick = null; bool pressed = false, released = false; double timeout = EditorApplication.timeSinceStartup + 120;
            tick = () =>
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > timeout) { EditorApplication.update -= tick; Time.timeScale = 1; Flush("aborted"); return; }
                double now = EditorApplication.timeSinceStartup;
                if (!pressed && now >= pressAt) { Keys(Key.E); pressed = true; }
                if (pressed && !released && now >= releaseAt) { Keys(); released = true; }
                // the Game view may not have keyboard focus while driven by the bridge: fall back to the same entry point E calls
                if (released && !station.Busy && lastT < 0 && now >= releaseAt + 1.5) { station.Begin(); Log("E not received (no Game view focus): WeaponMountStation.Begin() called directly"); releaseAt = double.MaxValue; }
                if (!station) { EditorApplication.update -= tick; Time.timeScale = 1; Flush("station lost"); return; }
                if (station.Busy) Sample();
                else if (lastT >= 0)
                {
                    EditorApplication.update -= tick; Time.timeScale = 1;
                    Shot("station_done_" + Tag);
                    Flush("done granted=" + station.Granted + " skipped=" + station.WasSkipped);
                }
            };
            EditorApplication.update += tick;
        }
        static void Flush(string why) { File.WriteAllText(Dir + "/station-telemetry.csv", csv.ToString()); Log("station " + why + "; telemetry rows=" + csv.ToString().Count(c => c == '\n')); }
        static void Sample()
        {
            float t = station.SequenceTime; if (t == lastT) return;
            var player = Player; var cam = player.PlayerCamera.transform; var hands = Object.FindAnyObjectByType<ProtagonistHandsPresentation>();
            var weapons = player.GetComponent<PlayerWeaponsManager>();
            float weight = Field<float>(hands, "overrideWeight"); bool armOverride = Field<bool>(hands, "armOverride"); bool hidden = Field<bool>(hands, "overrideHandHidden");
            var hand = cam.InverseTransformPoint(hands.RightHand.position);
            var forearm = hands.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Forearm_R_FPS");
            var proxy = Field<GameObject>(station, "cannonProxy"); var pAttach = Field<Transform>(station, "proxyAttachment"); var pMuzzle = Field<Transform>(station, "proxyMuzzle");
            var active = weapons.GetActiveWeapon(); Transform rAttach = null;
            if (active && active.WeaponRoot) foreach (var tr in active.WeaponRoot.GetComponentsInChildren<Transform>(true)) if (tr.name == "Attachment") { rAttach = tr; break; }
            Vector3 pa = proxy && pAttach ? cam.InverseTransformPoint(pAttach.position) : Vector3.zero, pf = proxy && pMuzzle ? cam.InverseTransformDirection(pMuzzle.forward) : Vector3.zero;
            Vector3 ra = rAttach ? cam.InverseTransformPoint(rAttach.position) : Vector3.zero, rf = active && active.WeaponMuzzle ? cam.InverseTransformDirection(active.WeaponMuzzle.forward) : Vector3.zero;
            bool realOn = active && active.WeaponRoot && active.WeaponRoot.activeInHierarchy;
            csv.AppendLine(string.Join(",", new object[] { t.ToString("0.0000"), (armOverride ? weight : 0).ToString("0.000"), hidden ? 1 : 0, F(hand.x), F(hand.y), F(hand.z), forearm && forearm.enabled && forearm.gameObject.activeInHierarchy ? 1 : 0,
                proxy && proxy.activeInHierarchy ? 1 : 0, F(pa.x), F(pa.y), F(pa.z), F(pf.x), F(pf.y), F(pf.z), realOn ? 1 : 0, F(ra.x), F(ra.y), F(ra.z), F(rf.x), F(rf.y), F(rf.z),
                F(player.transform.eulerAngles.y), F(cam.localEulerAngles.x), F(player.PlayerCamera.fieldOfView), F(weapons.WeaponCamera ? weapons.WeaponCamera.fieldOfView : 0) }));
            while (shotIndex < shots.Count && t >= shots[shotIndex]) { Shot($"station_{shots[shotIndex]:0.00}_{Tag}"); shotIndex++; }
            lastT = t;
        }
        static string F(float v) => v.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
        static void Shot(string name)
        {
            var p = Player; if (!p) return; Directory.CreateDirectory(Dir + "/station");
            var tex = RenderGame(p.PlayerCamera, 1280, 720); var g = tex.GetPixels();
            var vm = RenderViewmodel(p.GetComponent<PlayerWeaponsManager>(), p.PlayerCamera, 1280, 720);
            for (int i = 0; i < g.Length; i++) g[i] = Color.Lerp(g[i], new Color(vm[i].r, vm[i].g, vm[i].b, 1), vm[i].a);
            tex.SetPixels(g); tex.Apply();
            File.WriteAllBytes(Dir + "/station/" + name + ".png", tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        }

        // ------------------------------------------------------------------ HUD vs viewmodel
        static readonly MethodInfo ScalerHandle = typeof(CanvasScaler).GetMethod("Handle", Any);
        static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1920, 1200), new Vector2Int(2560, 1080) };

        [MenuItem(Menu + "Give cannon VO (play)")]
        static void GiveCannon()
        {
            RequirePlay(); var st = Object.FindAnyObjectByType<WeaponMountStation>(); var w = Player.GetComponent<PlayerWeaponsManager>();
            if (st && st.WeaponPrefab && !w.HasWeapon(st.WeaponPrefab)) w.AddWeapon(st.WeaponPrefab);
            for (int i = 0; i < 9; i++) { var s = w.GetWeaponAtSlotIndex(i); if (s && st && s.SourcePrefab == st.WeaponPrefab.gameObject) { w.SwitchToWeaponIndex(i, true); w.SnapActiveWeaponUp(); } }
            Log("gave cannon: active=" + (w.GetActiveWeapon() ? w.GetActiveWeapon().name : "none"));
        }
        [MenuItem(Menu + "Give pistol (play)")]
        static void GivePistol()
        {
            RequirePlay(); var w = Player.GetComponent<PlayerWeaponsManager>();
            var guid = AssetDatabase.FindAssets("Weapon_IR_Pistol t:Prefab").FirstOrDefault(); if (guid == null) { Log("pistol prefab not found"); return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)).GetComponent<WeaponController>();
            if (!w.HasWeapon(prefab)) w.AddWeapon(prefab);
            for (int i = 0; i < 9; i++) { var s = w.GetWeaponAtSlotIndex(i); if (s && s.SourcePrefab == prefab.gameObject) { w.SwitchToWeaponIndex(i, true); w.SnapActiveWeaponUp(); } }
            Log("gave pistol " + AssetDatabase.GUIDToAssetPath(guid));
        }
        [MenuItem(Menu + "Hold F (play)")] static void HoldF() { RequirePlay(); Keys(Key.F); }
        // weapon selection through the real Input System bindings (1 / 2)
        [MenuItem(Menu + "Press 1 (play)")] static void Press1() { RequirePlay(); Keys(Key.Digit1); }
        [MenuItem(Menu + "Press 2 (play)")] static void Press2() { RequirePlay(); Keys(Key.Digit2); }
        static int switchShot;
        [MenuItem(Menu + "Log weapons + capture (play)")]
        static void LogWeapons()
        {
            RequirePlay(); var p = Player; var w = p.GetComponent<PlayerWeaponsManager>(); var a = w.GetActiveWeapon();
            var slots = string.Join(",", Enumerable.Range(0, 9).Select(i => w.GetWeaponAtSlotIndex(i)).Where(x => x).Select(x => Clean(x.name)));
            var hands = Object.FindAnyObjectByType<ProtagonistHandsPresentation>();
            Log($"weapons: active={(a ? Clean(a.name) : "none")} index={w.ActiveWeaponIndex} rootActive={(a && a.WeaponRoot && a.WeaponRoot.activeInHierarchy)} socket={w.WeaponParentSocket.localPosition} slots=[{slots}] forearmReplaced={(hands && hands.RightForearmReplaced)}");
            Shot($"switch_{switchShot++:00}_{Tag}");
        }
        [MenuItem(Menu + "Release keys (play)")] static void Release() { RequirePlay(); Keys(); }

        [MenuItem(Menu + "Measure HUD vs viewmodel (play)")]
        static void MeasureHud()
        {
            RequirePlay();
            var player = Player; var weapons = player.GetComponent<PlayerWeaponsManager>(); var melee = player.GetComponent<ProtagonistMelee>();
            var carrier = player.GetComponent<StolenWeaponCarrier>(); var active = weapons.GetActiveWeapon();
            string state = melee && melee.IsCharging ? "fist-charge" : carrier && carrier.Held && carrier.Held.EquippedWeapon == active && active ? "stolen-" + Clean(active.name) : active ? Clean(active.name) : "unarmed";
            Directory.CreateDirectory(Dir + "/hud");
            var report = new StringBuilder();
            var bar = Object.FindAnyObjectByType<RetroStatusBar>();
            report.AppendLine($"state={state} bar={(bar ? ((RectTransform)bar.transform).rect.ToString() : "none")}");
            foreach (var size in Sizes)
            {
                var game = RenderGame(player.PlayerCamera, size.x, size.y);
                var ui = RenderUiAlpha(player.PlayerCamera, size.x, size.y, out string blocks);
                var view = RenderViewmodel(weapons, player.PlayerCamera, size.x, size.y);
                var g = game.GetPixels32(); var u = ui.GetPixels(); var v = view.Select(c => c.a > .5f).ToArray();
                int vm = 0, covered = 0, minX = size.x, minY = size.y, maxX = -1, maxY = -1;
                var outPx = new Color32[g.Length]; var overlap = new Color32[g.Length];
                for (int i = 0; i < g.Length; i++)
                {
                    float a = u[i].a; var gc = Color.Lerp((Color)g[i], new Color(view[i].r, view[i].g, view[i].b, 1), view[i].a);
                    outPx[i] = Color.Lerp(gc, new Color(u[i].r, u[i].g, u[i].b, 1), a);
                    bool isVm = v[i]; bool isHud = a > .5f; int x = i % size.x, y = i / size.x;
                    overlap[i] = isVm && isHud ? new Color32(255, 40, 40, 255) : isVm ? new Color32(90, 200, 230, 255) : isHud ? new Color32(70, 70, 70, 255) : new Color32(12, 12, 14, 255);
                    if (!isVm) continue; vm++; if (isHud) covered++;
                    if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
                var composite = new Texture2D(size.x, size.y, TextureFormat.RGB24, false); composite.SetPixels32(outPx); composite.Apply();
                var mask = new Texture2D(size.x, size.y, TextureFormat.RGB24, false); mask.SetPixels32(overlap); mask.Apply();
                string tag = $"{state}_{size.x}x{size.y}_{Tag}";
                File.WriteAllBytes($"{Dir}/hud/{tag}.png", composite.EncodeToPNG());
                File.WriteAllBytes($"{Dir}/hud/{tag}_overlap.png", mask.EncodeToPNG());
                // column layout: the viewmodel coverage by ANY opaque UI pixel is the metric; block rects are listed for reference
                report.AppendLine($"{size.x}x{size.y} aspect={(float)size.x / size.y:0.000} viewmodelPx={vm} coveredByUi={covered} ({(vm > 0 ? covered * 100f / vm : 0):0.0}%) viewmodelRect=x[{minX},{maxX}] y[{minY},{maxY}] (y from bottom)");
                report.AppendLine("  blocks (px, y from bottom):" + blocks);
                Object.DestroyImmediate(game); Object.DestroyImmediate(ui); Object.DestroyImmediate(composite); Object.DestroyImmediate(mask);
            }
            File.AppendAllText(Dir + "/hud/measure.txt", report.ToString());
            Log("HUD measured: " + state);
        }
        // Every dynamic label of the bar is tried with each string it can show; a label fails if its rendered text
        // is wider/taller than its rect or if it overlaps a sibling label. The real strings are restored afterwards.
        // Column layout (16/11): dynamic labels are identified by their GameObject name, so every string each one can show is tried.
        static readonly Dictionary<string, string[]> Variants = new Dictionary<string, string[]>
        {
            { "OverdriveState", RetroStatusBar.OverdriveLabels },
            { "EnergyState", new[] { "SIN MÓDULO", "ENERGÍA DEL MÓDULO", "FRÍA / ESTABLE", "CALOR ALTO", "SOBRECALENTADA" } },
            { "WeaponName", new[] { RetroStatusBar.NoWeaponName, "PISTOL", "ARMCANNON VO", "T07 TEMPORARY OPTIMIZED", RetroStatusBar.CleanName("Weapon_IR_T07_Temporary_Optimized(Clone)") } },
        };
        static readonly string[][] RowVariants = { RetroStatusBar.CoreLabels, RetroStatusBar.RightArmLabels, RetroStatusBar.LeftArmLabels, RetroStatusBar.LegsLabels };
        static string[] OptionsFor(TMPro.TMP_Text label, string original)
        {
            if (Variants.TryGetValue(label.name, out var options)) return options;
            // BodyRow_<n>/Value
            var row = label.transform.parent;
            if (label.name == "Value" && row && row.name.StartsWith("BodyRow_", StringComparison.Ordinal) && int.TryParse(row.name.Substring(8), out int index) && index >= 0 && index < RowVariants.Length)
                return RowVariants[index];
            return new[] { original };
        }
        [MenuItem(Menu + "Check bar text fit (play)")]
        static void CheckBarTextFit()
        {
            RequirePlay();
            var bar = Object.FindAnyObjectByType<RetroStatusBar>(); if (!bar) { Log("text fit: no RetroStatusBar"); return; }
            var canvas = bar.GetComponentInParent<Canvas>().rootCanvas; var scaler = canvas.GetComponent<CanvasScaler>();
            var labels = bar.GetComponentsInChildren<TMPro.TMP_Text>(true); var original = labels.Select(l => l.text).ToArray();
            var cam = TempCamera("TextFitCamera", Player.PlayerCamera); var report = new StringBuilder(); int failures = 0;
            try
            {
                foreach (var size in Sizes)
                {
                    var rt = RenderTexture.GetTemporary(size.x, size.y, 24); cam.targetTexture = rt;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam;
                    if (scaler && ScalerHandle != null) ScalerHandle.Invoke(scaler, null); Canvas.ForceUpdateCanvases();
                    var line = new StringBuilder($"{size.x}x{size.y}:"); int bad = 0;
                    for (int i = 0; i < labels.Length; i++)
                    {
                        var options = OptionsFor(labels[i], original[i]);
                        foreach (var text in options)
                        {
                            var l = labels[i]; l.text = text; l.ForceMeshUpdate(true, true);
                            var r = l.rectTransform.rect; var b = l.textBounds.size;
                            bool fits = b.x <= r.width + .5f && b.y <= r.height + .5f && !l.isTextOverflowing;
                            // sibling overlap in the same panel (e.g. OVERDRIVE state vs [G] EXPULSAR)
                            foreach (var other in labels) if (other != l && other.transform.parent == l.transform.parent && other.text.Length > 0)
                            {
                                other.ForceMeshUpdate(true, true);
                                if (WorldTextRect(l).Overlaps(WorldTextRect(other))) { fits = false; line.Append($" [overlap '{text}'/'{other.text}']"); }
                            }
                            if (!fits) { bad++; line.Append($" FAIL '{text}' text={b.x:0.0}x{b.y:0.0} rect={r.width:0.0}x{r.height:0.0} pt={l.fontSize:0.0}"); }
                            else if (l.enableAutoSizing && l.fontSize < l.fontSizeMax - .01f) line.Append($" '{text}'={l.fontSize:0.0}pt");
                        }
                        labels[i].text = original[i];
                    }
                    failures += bad; report.AppendLine(line.Append(bad == 0 ? " -> all labels fit" : $" -> {bad} failures").ToString());
                    cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
                }
            }
            finally
            {
                for (int i = 0; i < labels.Length; i++) if (labels[i]) labels[i].text = original[i];
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
                if (scaler && ScalerHandle != null) ScalerHandle.Invoke(scaler, null); Canvas.ForceUpdateCanvases();
                Object.DestroyImmediate(cam.gameObject);
            }
            Directory.CreateDirectory(Dir + "/hud"); File.AppendAllText(Dir + "/hud/text-fit.txt", report.ToString());
            Log($"text fit: {failures} failures (labels={labels.Length})");
        }
        static Rect WorldTextRect(TMPro.TMP_Text t)
        {
            var b = t.textBounds; var tr = t.rectTransform;
            Vector3 a = tr.TransformPoint(b.min), c = tr.TransformPoint(b.max);
            return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
        }
        static string Clean(string n) => n.Replace("(Clone)", "").Trim();

        static Texture2D RenderGame(Camera cam, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24); var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var prev = RenderTexture.active; var target = cam.targetTexture;
            try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); }
            finally { cam.targetTexture = target; RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); }
            return tex;
        }
        static Camera TempCamera(string name, Camera from)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; var c = go.AddComponent<Camera>(); c.CopyFrom(from); c.enabled = false;
            var data = go.AddComponent<UniversalAdditionalCameraData>(); data.renderType = CameraRenderType.Base; data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None;
            c.clearFlags = CameraClearFlags.SolidColor; return c;
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
        /// UI composited by rendering it over black and over white: alpha = 1 - (white - black).
        static readonly string[] BlockNames = { "WeaponsColumn", "BodyColumn", "BodyContent", "FistChargeRadial", "AvatarFrame", "RobotPortrait" };
        static Texture2D RenderUiAlpha(Camera game, int w, int h, out string blocks)
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(c => c.isRootCanvas && c.enabled && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var cam = TempCamera("HudCaptureCamera", game);
            // isolate the UI on an otherwise unused layer while capturing (some HUD graphics live on Default)
            const int CaptureLayer = 31;
            var moved = canvases.SelectMany(cv => cv.GetComponentsInChildren<Transform>(true)).Distinct().Select(tr => (tr.gameObject, tr.gameObject.layer)).ToArray();
            foreach (var m in moved) m.Item1.layer = CaptureLayer;
            cam.cullingMask = 1 << CaptureLayer; blocks = "";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                cam.targetTexture = RenderTexture.GetTemporary(w, h, 24); // canvas sizing follows the camera target
                foreach (var cv in canvases) { cv.renderMode = RenderMode.ScreenSpaceCamera; cv.worldCamera = cam; cv.planeDistance = Mathf.Max(cam.nearClipPlane + .05f, .3f); }
                for (int pass = 0; pass < 2; pass++)
                {
                    foreach (var cv in canvases) { var s = cv.GetComponent<CanvasScaler>(); if (s && ScalerHandle != null) ScalerHandle.Invoke(s, null); }
                    Canvas.ForceUpdateCanvases();
                    foreach (var f in Object.FindObjectsByType<ScreenFitPanel>(FindObjectsInactive.Exclude)) f.Refit();
                    foreach (var cv in canvases) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)cv.transform);
                }
                var bar = Object.FindAnyObjectByType<RetroStatusBar>();
                var rects = new List<RectTransform>();
                if (bar) foreach (var n in BlockNames) { var r = bar.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == n); if (r) rects.Add(r); }
                var mechanic = Object.FindAnyObjectByType<MechanicHud>();
                if (mechanic) rects.AddRange(mechanic.GetComponentsInChildren<RectTransform>(true).Where(t => t.name == "MechanicStatus" || t.name == "InteractionPrompt"));
                var sb = new StringBuilder(); var corners = new Vector3[4];
                foreach (var r in rects)
                {
                    r.GetWorldCorners(corners); Vector3 a = cam.WorldToScreenPoint(corners[0]), c = cam.WorldToScreenPoint(corners[2]);
                    sb.Append($" {r.name}=x[{a.x:0},{c.x:0}] y[{a.y:0},{c.y:0}] ({a.x / w * 100:0.0}%-{c.x / w * 100:0.0}% w, {a.y / h * 100:0.0}%-{c.y / h * 100:0.0}% h)");
                }
                blocks = sb.ToString();
                RenderTexture.ReleaseTemporary(cam.targetTexture); cam.targetTexture = null;
                var black = RenderTwice(cam, w, h, out var white); var px = new Color[black.Length];
                for (int i = 0; i < px.Length; i++)
                {
                    float a = Mathf.Clamp01(1 - ((white[i].r - black[i].r) + (white[i].g - black[i].g) + (white[i].b - black[i].b)) / 3f);
                    px[i] = a > .002f ? new Color(black[i].r / a, black[i].g / a, black[i].b / a, a) : new Color(0, 0, 0, 0);
                }
                tex.SetPixels(px); tex.Apply();
            }
            finally
            {
                foreach (var cv in canvases) if (cv) { cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.worldCamera = null; var s = cv.GetComponent<CanvasScaler>(); if (s && ScalerHandle != null) ScalerHandle.Invoke(s, null); }
                foreach (var m in moved) if (m.Item1) m.Item1.layer = m.Item2;
                Canvas.ForceUpdateCanvases(); Object.DestroyImmediate(cam.gameObject);
            }
            return tex;
        }
        /// The first-person overlay (weapon camera layers) rendered over black and white: colour + coverage alpha.
        static Color[] RenderViewmodel(PlayerWeaponsManager weapons, Camera game, int w, int h)
        {
            var source = weapons.WeaponCamera ? weapons.WeaponCamera : game;
            var cam = TempCamera("ViewmodelCaptureCamera", source);
            cam.cullingMask = weapons.WeaponCamera ? weapons.WeaponCamera.cullingMask : (int)weapons.FpsWeaponLayer;
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
    }
}
