using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// v6 checks through the file bridge: first-person captures of the closed right fist (tap, charge, release),
    /// the wrist joint and the montage station, plus a probe of the death-screen buttons (what the pointer hits and
    /// whether a real click restarts). Play mode only; nothing is saved.
    public static class HangarIntroDebugV6
    {
        const string Menu = "Tools/Iron Requiem/Hangar Intro/Debug/";
        static string Dir => "Tools/IronRequiem/Bridge/v6/" + EditorPrefs.GetString("IR_V6Tag", "after");
        const string ReportFile = "Tools/IronRequiem/Reports/HangarIntro/v6.txt";
        [MenuItem(Menu + "V6 capture tag = before")] static void TagBefore() => EditorPrefs.SetString("IR_V6Tag", "before");
        [MenuItem(Menu + "V6 capture tag = after")] static void TagAfter() => EditorPrefs.SetString("IR_V6Tag", "after");

        static readonly List<(double at, Action act)> queue = new List<(double, Action)>();
        static void Log(string line) { Directory.CreateDirectory(Path.GetDirectoryName(ReportFile)); File.AppendAllText(ReportFile, DateTime.Now.ToString("HH:mm:ss ") + line + "\n"); Debug.Log("[V6] " + line); }
        static void Schedule(double delay, Action act) { queue.Add((EditorApplication.timeSinceStartup + delay, act)); EditorApplication.update -= Tick; EditorApplication.update += Tick; }
        static void Tick()
        {
            if (!EditorApplication.isPlaying) { queue.Clear(); EditorApplication.update -= Tick; return; }
            queue.Sort((a, b) => a.at.CompareTo(b.at));
            if (queue.Count > 0 && EditorApplication.timeSinceStartup >= queue[0].at)
            {
                var a = queue[0].act; queue.RemoveAt(0); try { a(); } catch (Exception e) { Log("ERROR " + e); }
                double now = EditorApplication.timeSinceStartup;
                if (queue.Count > 0 && queue[0].at < now) { double shift = now - queue[0].at + .03; for (int i = 0; i < queue.Count; i++) queue[i] = (queue[i].at + shift, queue[i].act); }
            }
            if (queue.Count == 0) EditorApplication.update -= Tick;
        }
        // chained steps: each delay counts from the previous step
        static double cursor; static void Begin() => cursor = 0; static void Step(double d, Action a) { cursor += d; Schedule(cursor, a); }
        static PlayerCharacterController Player => Object.FindAnyObjectByType<PlayerCharacterController>();
        static void Keys(params Key[] keys) { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys)); }
        static void Shot(string name)
        {
            var p = Player; if (!p) return; var cam = p.PlayerCamera; Directory.CreateDirectory(Dir);
            var rt = RenderTexture.GetTemporary(1280, 720, 24); var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var prev = RenderTexture.active; var target = cam.targetTexture;
            try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply(); File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG()); }
            finally { cam.targetTexture = target; RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(tex); }
        }
        static T Field<T>(object o, string name) where T : class => o?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(o) as T;
        static string HandState()
        {
            var hands = Object.FindAnyObjectByType<ProtagonistHandsPresentation>(); var melee = Player ? Player.GetComponent<ProtagonistMelee>() : null;
            var anim = Field<Animator>(hands, "poseAnimator");
            string state = anim ? (anim.GetCurrentAnimatorStateInfo(0).IsName("NormalFist") ? "NormalFist" : anim.GetCurrentAnimatorStateInfo(0).IsName("ModuleSupport") ? "ModuleSupport" : "?") + " HandPose=" + anim.GetInteger("HandPose") : "no animator";
            var hp = Object.FindAnyObjectByType<ProtagonistHandsPresentation>(); return $"{state} fist={(hp ? hp.FistClosure : 0):0.00} attacking={(melee && melee.IsAttacking)} charging={(melee && melee.IsCharging)} level={(melee ? melee.ChargeLevel : 0):0.00} armed={(melee && !melee.Unarmed)}";
        }
        static void Place(Vector3 p, float yaw)
        {
            var player = Player; if (!player) return;
            var move = player.GetComponent<AdvancedPlayerMovement>(); if (move) move.ResetForCheckpoint();
            player.TeleportToCheckpoint(p); player.transform.rotation = Quaternion.Euler(0, yaw, 0); player.PlayerCamera.transform.localRotation = Quaternion.identity; Physics.SyncTransforms();
        }

        // ------------------------------------------------------------------ fist
        [MenuItem(Menu + "V6 fist sequence (play)")]
        static void Fist() => FistRun("fist");
        static void FistRun(string prefix)
        {
            Begin();
            Step(.05, () => { var hands = Object.FindAnyObjectByType<ProtagonistHandsPresentation>(); var bones = Field<Transform[]>(hands, "fistBones"); Log("fist bones: " + (bones == null ? "none" : string.Join(",", bones.Select(b => b.name)))); });
            Step(.1, () => { Shot(prefix + "_00_rest"); Log(prefix + " rest: " + HandState()); });
            // quick tap
            Step(.4, () => Keys(Key.F));
            Step(.03, () => { Shot(prefix + "_tap_a"); Log(prefix + " tap a: " + HandState()); });
            Step(.03, () => Keys());
            Step(.03, () => { Shot(prefix + "_tap_b"); Log(prefix + " tap b: " + HandState()); });
            Step(.08, () => { Shot(prefix + "_tap_c"); Log(prefix + " tap c: " + HandState()); });
            Step(.5, () => { Shot(prefix + "_tap_d"); Log(prefix + " tap d: " + HandState()); });
            // hold to full charge, release
            Step(.6, () => Keys(Key.F));
            Step(.12, () => { Shot(prefix + "_charge_start"); Log(prefix + " charge start: " + HandState()); });
            Step(.5, () => { Shot(prefix + "_charge_mid"); Log(prefix + " charge mid: " + HandState()); });
            Step(.8, () => { Shot(prefix + "_charge_full"); Log(prefix + " charge full: " + HandState()); });
            Step(.05, () => Keys());
            Step(.03, () => { Shot(prefix + "_release_a"); Log(prefix + " release a: " + HandState()); });
            Step(.06, () => { Shot(prefix + "_release_b"); Log(prefix + " release b: " + HandState()); });
            Step(.15, () => { Shot(prefix + "_return"); Log(prefix + " return: " + HandState()); });
            Step(.5, () => { Shot(prefix + "_end"); Log(prefix + " end: " + HandState()); });
        }
        [MenuItem(Menu + "V6 fist sequence armed (play)")]
        static void FistArmed() => FistRun("armed");

        // ------------------------------------------------------------------ station + wrist
        [MenuItem(Menu + "V6 station steps (play)")]
        static void Station()
        {
            var station = Object.FindAnyObjectByType<WeaponMountStation>(); if (!station) { Log("station: MISSING"); return; }
            Place(station.transform.position - station.transform.forward * .5f + Vector3.up * .03f, station.transform.eulerAngles.y);
            Begin(); Step(.5, () => { station.Begin(); Log("station begin"); });
            double last = 0;
            foreach (var m in new[] { .4, .8, 1.2, 1.6, 2.0, 2.5, 3.0, 3.4, 3.7, 4.0, 4.4, 4.8, 5.2, 5.8 })
            { double at = m; Step(Math.Max(.01, at - last), () => { Shot($"station_t{station.SequenceTime:0.00}"); Log($"station t={station.SequenceTime:0.00} busy={station.Busy}"); }); last = at; }
            Step(.8, () => { Shot("station_done"); Log("station done granted=" + station.Granted); });
        }
        [MenuItem(Menu + "V6 wrist closeups (play)")]
        static void Wrist()
        {
            var player = Player; if (!player) return; Begin();
            Step(.1, () => Shot("wrist_rest"));
            Step(.2, () => { if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Right, true)); });
            Step(.6, () => Shot("wrist_aim"));
            Step(.05, () => { if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Right, true).WithButton(MouseButton.Left, true)); });
            Step(.06, () => Shot("wrist_fire_a"));
            Step(.06, () => Shot("wrist_fire_b"));
            Step(.05, () => { if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState()); });
            Step(.5, () => { Keys(Key.C, Key.W, Key.LeftShift); });
            Step(.15, () => Shot("wrist_slide"));
            Step(.2, () => Keys());
            Step(.1, () => Log("wrist closeups done"));
        }

        // ------------------------------------------------------------------ death buttons
        // ------------------------------------------------------------------ v7 fist close-ups
        // Holds F (left charge) and throws an M1 (right strike, unarmed) and renders each hand close up from the front,
        // the side, the top and the thumb side, plus the game view. Uses the player camera's culling mask.
        [MenuItem(Menu + "V7 fist closeups (play)")]
        static void FistCloseups()
        {
            Begin();
            Step(.05, () => { var h = Object.FindAnyObjectByType<ProtagonistHandsPresentation>(); Log("v7 fist rigs: " + (h ? h.FistDebug : "none")); });
            Step(.2, () => Keys(Key.F));
            Step(.6, () => { Shot("v7_left_charge_game"); Close("v7_left", false); Log("v7 left charge: " + HandState() + " L=" + Hands.LeftFistClosure.ToString("0.00")); });
            Step(.9, () => Keys());
            Step(.12, () => { Shot("v7_left_release_game"); });
            Step(.2, () => { Shot("v7_left_impact_game"); });
            Step(1.0, () => { Shot("v7_left_open_game"); var h = Hands; Log("v7 rigs after: " + h.FistDebug + " L=" + h.LeftFistClosure.ToString("0.00")); });
            Step(.3, () => { if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Left, true)); });
            Step(.05, () => { if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState()); });
            Step(.1, () => { Shot("v7_right_strike_game"); Close("v7_right", true); Log("v7 right strike: " + HandState() + " R=" + Hands.RightFistClosure.ToString("0.00")); });
        }
        static ProtagonistHandsPresentation Hands => Object.FindAnyObjectByType<ProtagonistHandsPresentation>();
        static void Close(string prefix, bool right)
        {
            var h = Hands; var p = Player; if (!h || !p) return;
            var hand = Field<Transform>(h, right ? "rightHand" : "leftHand"); if (!hand) return;
            var src = p.PlayerCamera; var go = new GameObject("V7CloseCam"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(src); cam.enabled = false;
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cam.fieldOfView = 35; cam.nearClipPlane = .01f; var handRenderer = hand.GetComponentInChildren<Renderer>(); var rends = h.RightHandRenderers; if (rends != null && rends.Length > 0 && rends[0]) handRenderer = rends[0];
            cam.cullingMask = handRenderer ? 1 << handRenderer.gameObject.layer : ~0; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.32f, .34f, .38f); // only the FPS arms, plain backdrop
            var fwd = src.transform.forward; var up = src.transform.up; var side = src.transform.right * (right ? 1 : -1);
            Vector3 center = hand.position + fwd * .06f;
            (string n, Vector3 dir)[] views = { ("front", fwd), ("side", side), ("top", up), ("inner", -side), ("below", -up) };
            try
            {
                foreach (var v in views)
                {
                    cam.transform.position = center + v.dir * .32f; cam.transform.rotation = Quaternion.LookRotation(center - cam.transform.position, Mathf.Abs(Vector3.Dot(v.dir, up)) > .9f ? fwd : up);
                    Render(cam, prefix + "_" + v.n);
                }
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }
        static void Render(Camera cam, string name)
        {
            Directory.CreateDirectory(Dir); var rt = RenderTexture.GetTemporary(640, 640, 24); var tex = new Texture2D(640, 640, TextureFormat.RGB24, false); var prev = RenderTexture.active;
            try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); tex.Apply(); File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG()); }
            finally { cam.targetTexture = null; RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(tex); }
        }

        [MenuItem(Menu + "V6 death buttons probe (play)")]
        static void Death()
        {
            var player = Player; if (!player) return; var lab = Object.FindAnyObjectByType<CombatLabMenu>(); Begin();
            Step(.1, () => { var h = player.GetComponent<Health>(); if (h) h.Kill(); });
            Step(1.2, () =>
            {
                var death = Field<GameObject>(lab, "deathPanel"); Log($"death panel active={(death && death.activeInHierarchy)} cursor={Cursor.lockState}/{Cursor.visible} timeScale={Time.timeScale} eventSystem={(EventSystem.current ? EventSystem.current.name : "NONE")}");
                foreach (var m in Object.FindObjectsByType<Unity.FPS.UI.InGameMenuManager>(FindObjectsInactive.Include)) Log($"  InGameMenuManager {m.name} enabled={m.isActiveAndEnabled} industrial={m.UseIndustrialMenu}");
                if (!death) return;
                foreach (var b in death.GetComponentsInChildren<Button>(true))
                {
                    var canvas = b.GetComponentInParent<Canvas>().rootCanvas; var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, ((RectTransform)b.transform).position);
                    var data = new PointerEventData(EventSystem.current) { position = screen }; var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
                    foreach (var g in b.GetComponentsInChildren<Graphic>(true)) Log($"    graphic {g.name} {g.GetType().Name} rt={g.raycastTarget} active={g.isActiveAndEnabled} cull={g.canvasRenderer.cull} cullT={g.canvasRenderer.cullTransparentMesh} depth={g.depth} a={g.color.a:0.000} contains={RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform, screen, cam)}");
                    foreach (var cg in b.GetComponentsInParent<CanvasGroup>(true)) Log($"    group {cg.name} blocks={cg.blocksRaycasts} interactable={cg.interactable} alpha={cg.alpha}");
                    Log($"  button {b.name} interactable={b.IsInteractable()} listeners={b.onClick.GetPersistentEventCount()} screen={screen} hits: " + string.Join(" | ", hits.Take(5).Select(h => h.gameObject.name + "@" + h.module.GetType().Name + "/" + h.sortingOrder)));
                }
            });
            // a real mouse click on REINICIAR
            Step(.3, () =>
            {
                var death = Field<GameObject>(lab, "deathPanel"); var b = death ? death.GetComponentsInChildren<Button>(true).FirstOrDefault(x => x.name.Contains("Restart")) : null; if (!b || Mouse.current == null) return;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)b.transform).position);
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = screen });
            });
            Step(.2, () => { var death = Field<GameObject>(lab, "deathPanel"); var b = death.GetComponentsInChildren<Button>(true).First(x => x.name.Contains("Restart")); Vector2 s = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)b.transform).position); InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = s }.WithButton(MouseButton.Left, true)); Log("click down at " + s + " hovered=" + (EventSystem.current.currentSelectedGameObject ? EventSystem.current.currentSelectedGameObject.name : "-")); });
            Step(.12, () => { var death = Field<GameObject>(lab, "deathPanel"); var b = death.GetComponentsInChildren<Button>(true).First(x => x.name.Contains("Restart")); Vector2 s = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)b.transform).position); InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = s }); });
            Step(1.5, () => Log($"after click: cursor={Cursor.lockState} restarted={CombatLabMenu.ResumedAfterRestart} deathMenu={(Object.FindAnyObjectByType<CombatLabMenu>()?.IsDeathMenu)} timeScale={Time.timeScale}"));
        }
    }
}
