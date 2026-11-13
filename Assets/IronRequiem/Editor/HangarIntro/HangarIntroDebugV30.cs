using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// v30 (12/11/2026) evidence tool for the Room00 cinematic review: captures the same moments before and after the
    /// changes (cut to the glass punch, the exit from the side and the front, each foot contact, the neck and the wrist
    /// while the robot studies its hand) and measures how close hands, knees, feet and shoulders get to the capsule
    /// parts (oriented boxes). Editor-only; nothing here runs in the game.
    public static class HangarIntroDebugV30
    {
        const string Menu = "Tools/Iron Requiem/Hangar Intro/Debug/";
        const string Dir = "Tools/IronRequiem/Bridge/v30";
        const string ReportFile = "Tools/IronRequiem/Reports/HangarIntro/v30.txt";
        const int W = 960, H = 540;
        static string prefix = "v30a";
        static readonly List<float> marks = new List<float>(); static readonly List<string> kinds = new List<string>();
        static int next; static bool armed; static float lastT = -1;
        static readonly string[] PodParts = { "FootCap", "RailLip-1", "RailLip1", "Rail-1", "Rail1", "CanopyFrameTop", "HeadCap", "Bed", "HeadVisor" };

        static void Log(string line)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportFile));
            File.AppendAllText(ReportFile, System.DateTime.Now.ToString("HH:mm:ss ") + line + "\n");
            Debug.Log("[HangarIntro] v30 " + line);
        }

        [MenuItem("Tools/Iron Requiem/Menu/Open IntroMenu")]
        static void OpenIntroMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[HangarMenu] stop Play Mode first"); return; }
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogWarning("[HangarMenu] unsaved scene open, IntroMenu not opened: " + SceneManager.GetSceneAt(i).path); return; }
            EditorSceneManager.OpenScene("Assets/FPS/Scenes/IntroMenu.unity", OpenSceneMode.Single);
            Debug.Log("[HangarMenu] IntroMenu open");
        }

        [MenuItem(Menu + "V30 skip cinematic (play)")]
        static void Skip()
        {
            var cine = Object.FindAnyObjectByType<HangarIntroCinematic>(); if (!cine) { Log("skip: no cinematic"); return; }
            var d = cine.GetComponent<PlayableDirector>(); Log($"skip requested at t={(d ? d.time : -1):0.00} playing={cine.IsPlaying}");
            cine.RequestSkip();
        }

        /// End state after the cinematic (played or skipped): restraints retracted, cradle forward, cables hanging, glass broken.
        [MenuItem(Menu + "V30 report end state (play)")]
        static void EndState()
        {
            var cine = Object.FindAnyObjectByType<HangarIntroCinematic>(); var sb = new StringBuilder();
            if (cine) sb.Append($"finished={cine.Finished} skipped={cine.WasSkipped} ");
            var pod = GameObject.Find("Capsule_C04");
            if (pod)
            {
                var cradle = pod.transform.Find("Cradle"); if (cradle) sb.Append($"cradle localEuler={cradle.localEulerAngles} localPos={cradle.localPosition} ");
                foreach (var t in pod.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Restraint_"))) sb.Append($"{t.name}.scaleX={t.localScale.x:0.00} ");
                foreach (var t in pod.GetComponentsInChildren<Transform>(true).Where(t => t.name == "CablePlug")) sb.Append($"plug.y={t.position.y:0.00} ");
            }
            var glass = Object.FindAnyObjectByType<CapsuleGlassBreak>(); if (glass) sb.Append($"glass shattered={glass.IsShattered} ");
            var p = Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerCharacterController>(); if (p) sb.Append($"player={p.transform.position} yaw={p.transform.eulerAngles.y:0.0} lock={PlayerInputLock.Locked}");
            Log("end state " + sb);
        }

        [MenuItem(Menu + "V30 capture BEFORE (play)")] static void Before() => Arm("v30a");
        [MenuItem(Menu + "V30 capture AFTER (play)")] static void After() => Arm("v30b");

        static void Arm(string p)
        {
            prefix = p; marks.Clear(); kinds.Clear(); next = 0; armed = true; lastT = -1;
            Directory.CreateDirectory(Dir);
            File.AppendAllText(ReportFile, "");
            Log("armed " + prefix);
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            FootContactFx.Contact -= OnContact; FootContactFx.Contact += OnContact;
        }

        static void Plan(WakeUpRobotPose.Timing T, float duration)
        {
            void Add(float t, string k) { marks.Add(t); kinds.Add(k); }
            // 1. the cut from the visor to the glass: every 0.2 s around the first punch
            float cutFrom = Mathf.Max(0, T.punch1 - 3.0f);
            for (float t = cutFrom; t <= T.punch2 + .5f; t += .2f) Add(t, "cine");
            // 2. the exit: cinematic camera + side + front + capsule clearance every 0.25 s
            for (float t = T.leave - .6f; t <= T.stand + .6f; t += .25f) Add(t, "exit");
            // 3. head and wrist while studying the hand
            for (float t = T.handStart + .4f; t <= T.handEnd; t += .5f) Add(t, "hand");
            // 4. head turns to the conveyor and back (neck)
            Add((T.turnStart + T.turnEnd) * .5f, "hand"); Add(T.turnEnd + .3f, "hand"); Add((T.turnBackStart + T.turnBackEnd) * .5f, "hand");
            // 5. old fixed-time foot FX moments (the BEFORE evidence of the early dust)
            Add(T.rightStepEnd, "footR"); Add(T.leftStepEnd, "footL");
            var order = Enumerable.Range(0, marks.Count).OrderBy(i => marks[i]).ToArray();
            var m = order.Select(i => marks[i]).ToList(); var k = order.Select(i => kinds[i]).ToList();
            marks.Clear(); marks.AddRange(m); kinds.Clear(); kinds.AddRange(k);
            Log($"plan {marks.Count} captures, duration {duration:0.0}, punch1 {T.punch1:0.0} leave {T.leave:0.0} stand {T.stand:0.0} hand {T.handStart:0.0}-{T.handEnd:0.0}");
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; FootContactFx.Contact -= OnContact; armed = false; return; }
            if (!armed) return;
            var cine = Object.FindAnyObjectByType<HangarIntroCinematic>(); if (!cine) return;
            var director = cine.GetComponent<PlayableDirector>(); var pose = Object.FindAnyObjectByType<WakeUpRobotPose>();
            if (!director || !pose || !pose.IsBound) return;
            if (marks.Count == 0) Plan(pose.Times, (float)director.duration);
            float t = (float)director.time;
            if (!cine.IsPlaying) { if (cine.Finished) { Log("done " + prefix); armed = false; } return; }
            while (next < marks.Count && t >= marks[next])
            {
                string name = $"{prefix}_{kinds[next]}_t{marks[next]:00.00}";
                switch (kinds[next])
                {
                    case "cine": Capture(cine.GetComponentInChildren<Camera>(true), name); break;
                    case "exit":
                        Capture(cine.GetComponentInChildren<Camera>(true), name + "_cam");
                        Side(pose, name + "_side"); Front(pose, name + "_front");
                        Log($"t={t:0.00} clearance {Clearance(pose)}");
                        break;
                    case "hand": Neck(pose, name + "_neck"); Wrist(pose, name + "_wrist"); break;
                    case "footR": FootView(pose, true, name); Log($"t={t:0.00} fixed-time R foot sole height {SoleHeight(pose, true):0.000} m"); break;
                    case "footL": FootView(pose, false, name); Log($"t={t:0.00} fixed-time L foot sole height {SoleHeight(pose, false):0.000} m"); break;
                }
                next++;
            }
            lastT = t;
        }

        static void OnContact(FootContactFx fx, bool right, Vector3 point, float time, float sole)
        {
            if (!armed) return;
            var pose = Object.FindAnyObjectByType<WakeUpRobotPose>(); if (!pose) return;
            string name = $"{prefix}_contact_{(right ? "R" : "L")}_t{time:00.00}";
            FootView(pose, right, name);
            Log($"contact {(right ? "R" : "L")} t={time:0.000} sole={sole:0.000} m point={point}");
        }

        // ------------------------------------------------------------------ measurements
        static Transform Bone(WakeUpRobotPose pose, string n) => pose.Model ? pose.Model.GetComponentsInChildren<Transform>(true).FirstOrDefault(b => b.name == n || b.name.EndsWith(":" + n)) : null;

        static float SoleHeight(WakeUpRobotPose pose, bool right)
        {
            var foot = Bone(pose, right ? "RightFoot" : "LeftFoot"); var toe = Bone(pose, right ? "RightToeBase" : "LeftToeBase");
            if (!foot) return -1;
            float y = Mathf.Min(foot.position.y - pose.AnkleHeight, toe ? toe.position.y - pose.ToeHeight : 99);
            return y - (pose.FloorY);
        }

        /// Smallest signed distance (m) from robot probe points to the capsule's box parts; negative = inside a part.
        static string Clearance(WakeUpRobotPose pose)
        {
            var pod = GameObject.Find("Capsule_C04"); if (!pod) return "no pod";
            var parts = pod.GetComponentsInChildren<Transform>(true).Where(t => PodParts.Contains(t.name) && t.GetComponent<MeshRenderer>() && t.GetComponent<MeshRenderer>().enabled).ToArray();
            var probes = new (string bone, float radius)[] { ("LeftHand", .05f), ("RightHand", .05f), ("LeftLeg", .07f), ("RightLeg", .07f), ("LeftFoot", .06f), ("RightFoot", .06f), ("LeftToeBase", .04f), ("RightToeBase", .04f), ("LeftArm", .08f), ("RightArm", .08f), ("LeftForeArm", .06f), ("RightForeArm", .06f) };
            var sb = new StringBuilder(); float worst = 99; string worstName = "";
            foreach (var p in probes)
            {
                var b = Bone(pose, p.bone); if (!b) continue;
                foreach (var part in parts)
                {
                    // the cradle carries the robot: its back may touch the bed until it leaves
                    if (part.name == "Bed") continue;
                    float d = BoxDistance(part, b.position) - p.radius;
                    if (d < worst) { worst = d; worstName = p.bone + "/" + part.name; }
                    if (d < 0) sb.Append($" HIT {p.bone}->{part.name} {d:0.000}");
                }
            }
            return $"min {worst:0.000} m ({worstName}){sb}";
        }

        static float BoxDistance(Transform box, Vector3 p)
        {
            var l = box.InverseTransformPoint(p); var s = box.lossyScale;
            var q = new Vector3((Mathf.Abs(l.x) - .5f) * Mathf.Abs(s.x), (Mathf.Abs(l.y) - .5f) * Mathf.Abs(s.y), (Mathf.Abs(l.z) - .5f) * Mathf.Abs(s.z));
            var outside = Vector3.Max(q, Vector3.zero).magnitude; var inside = Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0);
            return outside + inside;
        }

        // ------------------------------------------------------------------ cameras
        static void With(System.Action<Camera> place, string name, float fov = 40)
        {
            var go = new GameObject("V30CaptureCam"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = fov; cam.nearClipPlane = .02f; cam.enabled = false;
            try { place(cam); Capture(cam, name); } finally { Object.DestroyImmediate(go); }
        }

        static void Side(WakeUpRobotPose pose, string name)
        {
            var stand = pose.StandPoint; if (!stand) return;
            var right = Vector3.Cross(Vector3.up, stand.forward);
            var focus = stand.position - stand.forward * .55f + Vector3.up * .8f;
            With(c => { c.transform.position = focus + right * 3.4f + Vector3.up * .25f; c.transform.LookAt(focus); }, name, 42);
        }

        static void Front(WakeUpRobotPose pose, string name)
        {
            var stand = pose.StandPoint; if (!stand) return;
            var focus = stand.position - stand.forward * .45f + Vector3.up * .9f;
            With(c => { c.transform.position = focus + stand.forward * 3.6f + Vector3.up * .35f + Vector3.Cross(Vector3.up, stand.forward) * .4f; c.transform.LookAt(focus); }, name, 42);
        }

        static void FootView(WakeUpRobotPose pose, bool right, string name)
        {
            var foot = Bone(pose, right ? "RightFoot" : "LeftFoot"); var stand = pose.StandPoint; if (!foot || !stand) return;
            var side = Vector3.Cross(Vector3.up, stand.forward) * (right ? 1 : -1);
            var focus = new Vector3(foot.position.x, pose.FloorY + .12f, foot.position.z);
            With(c => { c.transform.position = focus + side * 1.3f + Vector3.up * .05f + stand.forward * .2f; c.transform.LookAt(focus); }, name, 34);
        }

        static void Neck(WakeUpRobotPose pose, string name)
        {
            var head = pose.Head; if (!head) return; var neck = Bone(pose, "Neck");
            var focus = neck ? Vector3.Lerp(neck.position, head.position, .5f) : head.position;
            var fwd = Vector3.ProjectOnPlane(pose.Model.forward, Vector3.up).normalized; if (fwd.sqrMagnitude < .1f) fwd = Vector3.forward;
            var side = Vector3.Cross(Vector3.up, fwd);
            With(c => { c.transform.position = focus + (side * .8f + fwd * .45f + Vector3.up * .1f).normalized * .9f; c.transform.LookAt(focus); }, name, 30);
        }

        static void Wrist(WakeUpRobotPose pose, string name)
        {
            var hand = pose.RightHand; var fore = pose.RightForearm; if (!hand || !fore) return;
            var mid = Vector3.Lerp(fore.position, hand.position, .65f); var axis = (hand.position - fore.position).normalized;
            var side = Vector3.Cross(Vector3.up, axis); if (side.sqrMagnitude < 1e-4f) side = Vector3.right; side.Normalize();
            With(c => { c.transform.position = mid + side * .7f + Vector3.up * .15f; c.transform.LookAt(mid); }, name, 32);
        }

        static void Capture(Camera cam, string name)
        {
            if (!cam) return;
            Directory.CreateDirectory(Dir);
            var rt = RenderTexture.GetTemporary(W, H, 24); var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var prev = RenderTexture.active; var target = cam.targetTexture; bool was = cam.enabled;
            try { cam.targetTexture = rt; cam.enabled = true; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); File.WriteAllBytes(Path.Combine(Dir, name + ".jpg"), tex.EncodeToJPG(88)); }
            finally { cam.targetTexture = target; cam.enabled = was; RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(tex); }
        }
    }
}
