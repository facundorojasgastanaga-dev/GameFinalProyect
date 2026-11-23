using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    /// Visual pass 12 · F5 · Carroñero combat animation (CarroneroCombatMotion on Carronero_Map01.prefab), one case per
    /// criterion of 01e_CARRONERO.md, level "Mínimo". Same session pattern as VisualPassF0: the real Map01 session, flow
    /// and GameFlowManager disabled, Map01 R01 loaded additively, fixed 1/60 s step. Nothing is saved.
    /// Run VisualOverhaulCarronero.Apply first. Measured numbers go to Tools/IronRequiem/Reports/VisualPassFable/F5/.
    /// Where it makes sense the untouched base prefab runs through the same measurement as a control: a check that
    /// cannot tell the old animation from the new one proves nothing.
    public sealed class VisualPassCarroneroTests
    {
        const string Session = "Assets/IronRequiem/Scenes/Map01_Bunker/Map01_Campaign_S1.unity";
        const string Room = "Assets/IronRequiem/Scenes/Map01_Bunker/Map01_R01_ControlAcceso.unity";
        const string Variant = "Assets/IronRequiem/Prefabs/Map01/Enemies/Carronero_Map01.prefab";
        const string Base = "Assets/IronRequiem/Prefabs/Campaign/Carronero.prefab";
        const string LegacyScript = "Assets/IronRequiem/Scripts/Enemies/CarroneroMotion.cs";
        const string NewScript = "Assets/IronRequiem/Scripts/Enemies/CarroneroCombatMotion.cs";
        const string Backup = "Tools/IronRequiem/Backups/20261121-visualpass-f2f5/";
        const string Out = "Tools/IronRequiem/Reports/VisualPassFable/F5/";
        static readonly string[] ProtectedScenes = { "Assets/FPS/Scenes/IntroMenu.unity", "Assets/IronRequiem/Scenes/Campaign/Room00_Hangar.unity" };
        static readonly string[] PosedBones = { "Hips", "Spine1", "Spine2", "Neck", "Head", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot", "LeftArm", "LeftForeArm", "RightArm" };
        const float Dt = 1f / 60;

        bool background, hasRun; float capture, runLength; Scene sessionScene; Camera cam; RenderTexture target; PlayerCharacterController player;
        Vector3 runA, runB, runDir; StringBuilder notes; readonly List<GameObject> spawned = new List<GameObject>();

        // ------------------------------------------------------------------ session
        [UnitySetUp] public IEnumerator Setup()
        {
            LogAssert.ignoreFailingMessages = true;
            background = Application.runInBackground; Application.runInBackground = true; Time.timeScale = 1; capture = Time.captureDeltaTime;
            notes = new StringBuilder();
            EditorSceneManager.LoadSceneInPlayMode(Session, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            var flow = Object.FindAnyObjectByType<CampaignFlow>(); Assert.That(flow, Is.Not.Null);
            float end = Time.realtimeSinceStartup + 90; while ((flow.Busy || flow.CurrentStage != 0) && Time.realtimeSinceStartup < end) yield return null;
            flow.Menu.BeginGame(); flow.Player.GetComponent<Health>().Invincible = true; yield return null;
            var intro = Object.FindAnyObjectByType<HangarIntroCinematic>();
            if (intro) { end = Time.realtimeSinceStartup + 10; while (!intro.IsPlaying && !intro.Finished && Time.realtimeSinceStartup < end) yield return null; intro.RequestSkip(); }
            end = Time.realtimeSinceStartup + 15; while (PlayerInputLock.Locked && Time.realtimeSinceStartup < end) yield return null;
            yield return flow.DrainPendingLoadsForShutdown(); flow.enabled = false;
            foreach (var m in Object.FindObjectsByType<MonoBehaviour>()) if (m && m.GetType().Name == "GameFlowManager") m.enabled = false; // a kill must not end the session
            Time.timeScale = 0;
            sessionScene = flow.gameObject.scene;
            foreach (var s in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s != sessionScene && s.isLoaded && s.name != "DontDestroyOnLoad" && !s.name.StartsWith("InitTestScene")).ToArray())
                yield return SceneManager.UnloadSceneAsync(s);
            flow.Player.gameObject.SetActive(true); player = flow.Player;
            yield return null;

            // Map01 R01: floor, NavMesh and the place where F0 filmed the baseline
            var op = EditorSceneManager.LoadSceneAsyncInPlayMode(Room, new LoadSceneParameters(LoadSceneMode.Additive));
            end = Time.realtimeSinceStartup + 60; while (op != null && !op.isDone && Time.realtimeSinceStartup < end) yield return null;
            var room = SceneManager.GetSceneByPath(Room); Assert.That(room.IsValid() && room.isLoaded, Is.True, "load " + Room);
            SceneManager.SetActiveScene(room);
            for (int i = 0; i < 5; i++) yield return null;
            foreach (var queue in Object.FindObjectsByType<EnemySpawnQueue>()) { queue.CancelPending(); queue.enabled = false; } // the room must not add its own wave
            Physics.SyncTransforms();
            hasRun = FindRun(room);
            if (!hasRun) { runA = player.transform.position; runDir = Vector3.forward; runLength = 12; runB = runA + runDir * runLength; }
            PlacePlayer(runA);

            // own camera into a small texture: it decides what is "on camera" without touching the game view
            var go = new GameObject("VPCarronero_Camera"); cam = go.AddComponent<Camera>();
            target = new RenderTexture(320, 180, 16); cam.targetTexture = target; cam.fieldOfView = 38; cam.nearClipPlane = .05f; cam.farClipPlane = 300;
            Time.captureDeltaTime = Dt; Time.timeScale = 1;
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = capture; Time.timeScale = 1; Application.runInBackground = background;
            foreach (var g in spawned) if (g) Object.Destroy(g);
            spawned.Clear();
            if (cam) { cam.targetTexture = null; Object.Destroy(cam.gameObject); }
            if (target) { target.Release(); Object.Destroy(target); }
            if (notes != null && notes.Length > 0)
            {
                try { Directory.CreateDirectory(Out); File.AppendAllText(Out + "carronero_tests.txt", DateTime.Now.ToString("s") + " " + TestContext.CurrentContext.Test.Name + "\n" + notes + "\n"); }
                catch (IOException) { }
            }
            var clean = SceneManager.CreateScene("VisualPassCarroneroCleanup" + Time.frameCount); SceneManager.SetActiveScene(clean);
            var session = Object.FindAnyObjectByType<CampaignFlow>(); if (session) { session.enabled = true; yield return session.DrainPendingLoadsForShutdown(); }
            foreach (var queue in Object.FindObjectsByType<EnemySpawnQueue>()) { queue.CancelPending(); queue.enabled = false; }
            var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s != clean && s.isLoaded).OrderBy(s => s == sessionScene ? 1 : 0).ToArray();
            foreach (var s in loaded) if (s.isLoaded) yield return SceneManager.UnloadSceneAsync(s);
            Cursor.lockState = CursorLockMode.None; LogAssert.ignoreFailingMessages = false;
        }

        /// A straight, level, unobstructed NavMesh segment: the player stands at runA and bots start at runB.
        bool FindRun(Scene scene)
        {
            var cols = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(false)).Where(c => c.enabled && !c.isTrigger).ToArray();
            if (cols.Length == 0) return false;
            var bounds = cols[0].bounds; foreach (var c in cols) bounds.Encapsulate(c.bounds);
            var seeds = new[] { new Vector3(bounds.center.x, bounds.min.y + .5f, bounds.center.z), bounds.center };
            foreach (float length in new[] { 16f, 14f, 12f, 10f })
                foreach (var seed in seeds)
                {
                    if (!NavMesh.SamplePosition(seed, out var centre, 20f, NavMesh.AllAreas)) continue;
                    foreach (float offset in new[] { 0f, 3f, -3f, 6f, -6f })
                        for (int k = 0; k < 12; k++)
                        {
                            var dir = Quaternion.Euler(0, k * 15f, 0) * Vector3.forward; var across = Vector3.Cross(Vector3.up, dir) * offset;
                            if (!NavMesh.SamplePosition(centre.position + across - dir * (length * .5f), out var a, 1.5f, NavMesh.AllAreas)) continue;
                            if (!NavMesh.SamplePosition(centre.position + across + dir * (length * .5f), out var b, 1.5f, NavMesh.AllAreas)) continue;
                            var line = b.position - a.position;
                            if (Mathf.Abs(line.y) > .3f || line.magnitude < length - 2f) continue;
                            if (NavMesh.Raycast(a.position, b.position, out _, NavMesh.AllAreas)) continue;
                            if (Physics.Linecast(a.position + Vector3.up * 1.2f, b.position + Vector3.up * 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                            runA = a.position; runB = b.position; runLength = line.magnitude; runDir = Vector3.ProjectOnPlane(line, Vector3.up).normalized;
                            return true;
                        }
                }
            return false;
        }

        // ------------------------------------------------------------------ helpers
        /// Where puppets start: on the run, clear of the player who stands at runA.
        Vector3 Lane => runA + runDir * 1.5f;
        float LaneLength => Mathf.Max(4f, runLength - 2.5f);

        void PlacePlayer(Vector3 floor)
        {
            var advanced = player.GetComponent<AdvancedPlayerMovement>(); if (advanced) advanced.ResetForCheckpoint();
            player.TeleportToCheckpoint(floor + Vector3.up * .05f); Physics.SyncTransforms();
        }

        GameObject Spawn(string prefabPath, Vector3 position, Quaternion rotation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); Assert.That(prefab, Is.Not.Null, prefabPath);
            var go = Object.Instantiate(prefab, position, rotation); go.name = "VPC_" + prefab.name; spawned.Add(go);
            foreach (var ec in go.GetComponentsInChildren<EnemyController>(true)) ec.SelfDestructYHeight = -100000;
            return go;
        }

        /// Presentation only: AI and agent off, the test moves the transform. Call two frames after Spawn (Start has run).
        static void Puppet(GameObject go)
        {
            foreach (var mb in go.GetComponents<MonoBehaviour>()) if (mb is EnemyController || mb is EnemyMobile || mb is SawBotCombat) mb.enabled = false;
            var agent = go.GetComponent<NavMeshAgent>(); if (agent) agent.enabled = false;
        }

        static void RequireApplied()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Variant); Assert.That(asset, Is.Not.Null, Variant);
            Assert.That(asset.GetComponent<CarroneroCombatMotion>(), Is.Not.Null, "Carronero_Map01.prefab has no CarroneroCombatMotion: run VisualOverhaulCarronero.Apply first");
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static CarroneroRigInfo Rig(GameObject go) => go.GetComponentInChildren<CarroneroRigInfo>(true);
        static Quaternion Rel(Transform root, Transform bone) => Quaternion.Inverse(root.rotation) * bone.rotation;
        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        void Note(string line) { notes.AppendLine("  " + line); Debug.Log("[VisualPassCarronero] " + line); }

        /// Side view that follows the bot, as in F0: close enough for LOD0.
        void Aim(Transform bot, float distance = 3.6f)
        {
            var c = bot.position + Vector3.up * .75f; var side = Vector3.Cross(Vector3.up, bot.forward); var p = c + side * distance + Vector3.up * .15f;
            cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(c - p, Vector3.up));
        }
        void AimAt(Vector3 point, Vector3 from) => cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(point - from, Vector3.up));

        /// Foot bookkeeping shared by the chase and the fixed-speed run. The lower ankle is F0's "support foot"; the
        /// slower ankle is an independent check that does not read the component's own flags.
        sealed class Gait
        {
            public int frames, plantedFrames, stillFrames, stances, touchdowns, maxTier = -1, minStance = int.MaxValue;
            public float maxPlantedSlip, f0Sum, speedSum, firstTouch, lastTouch;
            Vector3 lastL, lastR; bool have, lastLp, lastRp, openL, openR; int runL, runR;
            public float F0Mean => frames > 0 ? f0Sum / frames : 0;
            public float MeanSpeed => frames > 0 ? speedSum / frames : 0;
            public float PlantedFraction => frames > 0 ? plantedFrames / (float)frames : 0;
            public float StillFraction => frames > 0 ? stillFrames / (float)frames : 0;
            public float CadenceHz => touchdowns > 1 && lastTouch > firstTouch ? (touchdowns - 1) / (lastTouch - firstTouch) : 0;
            public void Break() { have = false; openL = openR = false; }
            public void Add(Vector3 l, Vector3 r, bool lp, bool rp, float speed, float time, int tier)
            {
                if (have)
                {
                    float dl = Flat(l - lastL), dr = Flat(r - lastR);
                    frames++; speedSum += speed; if (tier > maxTier) maxTier = tier;
                    f0Sum += (l.y <= r.y ? dl : dr) / Dt; // same rule as VisualPassF0.Slide
                    if (Mathf.Min(dl, dr) <= .02f) stillFrames++;
                    if (lp && lastLp) maxPlantedSlip = Mathf.Max(maxPlantedSlip, dl);
                    if (rp && lastRp) maxPlantedSlip = Mathf.Max(maxPlantedSlip, dr);
                    if (lp || rp) plantedFrames++;
                    if (lp && !lastLp) { if (touchdowns == 0) firstTouch = time; lastTouch = time; touchdowns++; }
                    Stance(lp, lastLp, ref runL, ref openL); Stance(rp, lastRp, ref runR, ref openR);
                }
                lastL = l; lastR = r; lastLp = lp; lastRp = rp; have = true;
            }
            void Stance(bool now, bool before, ref int run, ref bool open)
            {
                if (now && !before) { open = true; run = 1; }
                else if (now && open) run++;
                else if (!now && before && open) { if (run < minStance) minStance = run; stances++; open = false; }
            }
            public string Line(string tag) => tag + ": frames=" + frames + " speed=" + F(MeanSpeed) + " m/s | F0 support-foot speed=" + F(F0Mean) + " m/s | a foot still (<=2 cm) in " + F(StillFraction * 100) + " % of frames"
                + " | flagged planted " + F(PlantedFraction * 100) + " % | max planted slip=" + F(maxPlantedSlip * 100) + " cm/frame | stances=" + stances + " shortest=" + (stances > 0 ? minStance.ToString() : "-") + " frames | cadence=" + F(CadenceHz) + " Hz | worst LOD=" + maxTier;
        }

        /// The real AI chasing the player from runB to runA, measured while the agent is at chase speed (F0 method).
        IEnumerator Chase(string prefabPath, Gait gait)
        {
            PlacePlayer(runA);
            var bot = Spawn(prefabPath, runB, Quaternion.LookRotation(-runDir, Vector3.up));
            var agent = bot.GetComponent<NavMeshAgent>(); var motion = bot.GetComponent<CarroneroCombatMotion>();
            Transform footL = Rig(bot).Bone("LeftFoot"), footR = Rig(bot).Bone("RightFoot");
            Assert.That(footL && footR, Is.True, "foot bones");
            for (int i = 0; i < 480 && bot; i++)
            {
                Aim(bot.transform); yield return null; if (!bot) break;
                if (Vector3.Distance(bot.transform.position, player.transform.position) < 2.5f) break;
                float speed = agent.velocity.magnitude;
                if (speed < 4.5f || (motion && motion.BlendWeight < 1f)) { gait.Break(); continue; }
                gait.Add(footL.position, footR.position, motion && motion.LeftFootPlanted, motion && motion.RightFootPlanted, speed, Time.time, motion ? motion.LodTier : 0);
            }
            if (bot) Object.Destroy(bot);
            yield return null;
        }

        // ------------------------------------------------------------------ (a) A1
        [UnityTest, Timeout(120000)] public IEnumerator A_PlantedFoot_DoesNotSlide_AtRealChaseSpeed()
        {
            LogAssert.ignoreFailingMessages = true; RequireApplied();
            Assume.That(hasRun, Is.True, "no straight NavMesh run in " + Room);
            var control = new Gait(); var now = new Gait();
            yield return Chase(Base, control);
            yield return Chase(Variant, now);
            Note(control.Line("control (Carronero.prefab, CarroneroMotion)")); Note(now.Line("Carronero_Map01 (CarroneroCombatMotion)"));
            Assume.That(control.frames, Is.GreaterThanOrEqualTo(30), "the control chase was too short to measure");
            Assume.That(now.frames, Is.GreaterThanOrEqualTo(30), "the chase was too short to measure");
            Assume.That(now.maxTier, Is.LessThanOrEqualTo(1), "the bot must be filmed at LOD0/1, where the foot IK runs");
            Assert.That(control.F0Mean, Is.GreaterThan(3f), "control: the F0 metric must still see the legacy gait skating (6.2 to 6.6 m/s in F0)");
            Assert.That(now.maxPlantedSlip, Is.LessThanOrEqualTo(.02f), "planted foot: horizontal displacement per frame (limit 2 cm, R1)");
            Assert.That(now.PlantedFraction, Is.GreaterThanOrEqualTo(.5f), "a foot must be planted most of the time, or the limit above is met by never planting");
            Assert.That(now.StillFraction, Is.GreaterThanOrEqualTo(.4f), "independent of the flags: one ankle must really be still on the ground");
            Assert.That(now.StillFraction, Is.GreaterThan(control.StillFraction), "and more often than with the legacy gait");
            Assert.That(now.stances, Is.GreaterThan(0)); Assert.That(now.minStance, Is.GreaterThanOrEqualTo(3), "every step keeps the foot planted at least 3 frames");
            Assert.That(now.CadenceHz, Is.GreaterThan(0).And.LessThanOrEqualTo(5f), "cadence (R2)");
            Assert.That(now.F0Mean, Is.LessThan(control.F0Mean), "F0 support-foot speed must drop");
        }

        // ------------------------------------------------------------------ A1 at the top speed, fixed step
        [UnityTest, Timeout(120000)] public IEnumerator G_TopSpeed_CadenceUnder5Hz_AndThreePlantedFramesPerStep()
        {
            LogAssert.ignoreFailingMessages = true; RequireApplied();
            const float speed = 7.2f;
            var rot = Quaternion.LookRotation(runDir, Vector3.up);
            var bot = Spawn(Variant, Lane, rot); yield return Frames(2); Puppet(bot);
            var motion = bot.GetComponent<CarroneroCombatMotion>(); Transform footL = Rig(bot).Bone("LeftFoot"), footR = Rig(bot).Bone("RightFoot");
            var gait = new Gait(); int steps = Mathf.Max(90, Mathf.FloorToInt(LaneLength / (speed * Dt)));
            for (int i = 0; i < steps; i++)
            {
                bot.transform.SetPositionAndRotation(Lane + runDir * (speed * Dt * i), rot); Aim(bot.transform); yield return null;
                if (i < 30 || motion.BlendWeight < 1f) continue; // blend in and speed settled
                gait.Add(footL.position, footR.position, motion.LeftFootPlanted, motion.RightFootPlanted, motion.Speed, Time.time, motion.LodTier);
            }
            Note(gait.Line("7.2 m/s fixed") + " | component cadence=" + F(motion.CadenceHz) + " Hz cycle=" + F(motion.CycleMeters) + " m");
            Assume.That(gait.maxTier, Is.LessThanOrEqualTo(1));
            Assert.That(gait.MeanSpeed, Is.EqualTo(speed).Within(.3f), "the gait must read the real speed of the transform");
            Assert.That(gait.maxPlantedSlip, Is.LessThanOrEqualTo(.02f));
            Assert.That(gait.stances, Is.GreaterThanOrEqualTo(4)); Assert.That(gait.minStance, Is.GreaterThanOrEqualTo(3));
            Assert.That(gait.CadenceHz, Is.GreaterThan(3f).And.LessThanOrEqualTo(5f), "cadence at 7.2 m/s (12.5 Hz before)");
            Assert.That(gait.StillFraction, Is.GreaterThanOrEqualTo(.4f));
        }

        // ------------------------------------------------------------------ (b) A3 / A4
        [UnityTest, Timeout(120000)] public IEnumerator B_Attack_AnticipationReadsBeforeContact_AndRecovers()
        {
            LogAssert.ignoreFailingMessages = true; RequireApplied();
            Assume.That(hasRun, Is.True, "no straight NavMesh run in " + Room);
            PlacePlayer(runA);
            var bot = Spawn(Variant, runA + runDir * 6f, Quaternion.LookRotation(-runDir, Vector3.up));
            var combat = bot.GetComponent<SawBotCombat>(); var motion = bot.GetComponent<CarroneroCombatMotion>(); var rig = Rig(bot);
            Transform arm = rig.Bone("LeftArm"), tip = rig.bladeEnd, root = bot.transform;
            var armBind = Rel(root, arm); var runPose = armBind; bool attacking = false;
            for (int i = 0; i < 600 && !attacking; i++) { runPose = Rel(root, arm); Aim(root); yield return null; attacking = combat.IsAttacking; }
            Assume.That(attacking, Is.True, "the bot never started its attack");
            float prep = combat.Preparation, contactBefore = combat.ContactSeconds;
            // sample k reads the pose written k-1 frames after the frame in which IsAttacking rose
            int n = Mathf.CeilToInt((prep + .6f) / Dt);
            float armedAt = -1, frontAt = -1, angleAtTenth = 0, contactAt = -1, sustainTravel = 0; Vector3 lastTip = tip.position; bool cancelled = false;
            for (int k = 1; k <= n; k++)
            {
                Aim(root); yield return null;
                float shown = (k - 1) * Dt, z = (Quaternion.Inverse(root.rotation) * (tip.position - arm.position)).z;
                if (!combat.IsAttacking) { cancelled = true; break; }
                if (armedAt < 0 && z < 0) armedAt = shown;
                if (armedAt >= 0 && frontAt < 0 && z >= 0) frontAt = shown;
                if (Mathf.Abs(shown - .1f) < Dt * .5f) angleAtTenth = Quaternion.Angle(runPose, Rel(root, arm));
                if (contactAt < 0 && combat.ContactSeconds > contactBefore) contactAt = k * Dt;
                if (shown >= prep + .05f && shown < prep + .41f) sustainTravel += Vector3.Distance(tip.position, lastTip);
                lastTip = tip.position;
            }
            Note("attack: preparation=" + F(prep) + " s | saw behind the shoulder from " + F(armedAt) + " s to " + F(frontAt) + " s | arm at 0.10 s differs " + F(angleAtTenth) + " deg from the run pose | first contact tick at "
                + (contactAt < 0 ? "(none: distance " + F(Vector3.Distance(root.position, player.transform.position)) + " m)" : F(contactAt) + " s") + " | saw tip travel in 0.36 s of sustain=" + F(sustainTravel * 100) + " cm | stage=" + motion.Stage);
            Assume.That(cancelled, Is.False, "the attack was cancelled before it could be measured");
            Assert.That(armedAt, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(.12f), "the saw must be cocked behind the shoulder within the wind-up");
            Assert.That(frontAt, Is.GreaterThanOrEqualTo(.25f), "anticipation: the saw tip must not come forward of the shoulder before 0.25 s (R3)");
            Assert.That(frontAt, Is.LessThanOrEqualTo(prep + Dt * .5f), "and it must be forward by the end of the preparation (R3)");
            Assert.That(angleAtTenth, Is.GreaterThanOrEqualTo(25f), "at 0.10 s the arm must be clearly off its run pose");
            if (contactAt >= 0)
            {
                Assert.That(contactAt, Is.GreaterThanOrEqualTo(prep - 1e-3f), "SawBotCombat cannot hurt before its preparation");
                Assert.That(contactAt, Is.LessThanOrEqualTo(prep + 3 * Dt + 1e-3f), "the blade must be on the target when the preparation ends (R4): contact may not start late"); // 1 ms of float slack: 0.35 + 3/60 measured as 0.40000004
            }
            Assert.That(sustainTravel, Is.GreaterThanOrEqualTo(.08f), "the sustained saw must keep moving (R5)");
            Assert.That(motion.Stage, Is.EqualTo(CarroneroCombatMotion.AttackStage.Sustain));

            // recovery: the player leaves, SawBotCombat cancels, the arm is back on its run pose 0.25 s later
            PlacePlayer(runB);
            bool released = false; for (int i = 0; i < 60 && !released; i++) { Aim(root); yield return null; released = !combat.IsAttacking; }
            Assume.That(released, Is.True, "the attack did not end after the player left");
            int settle = Mathf.CeilToInt(.25f / Dt) + 2; for (int k = 0; k < settle; k++) { Aim(root); yield return null; }
            float back = Quaternion.Angle(armBind, Rel(root, arm));
            Note("recovery: arm " + F(back) + " deg from its bind pose 0.25 s after the attack ended (run swing is +-6 deg) | stage=" + motion.Stage);
            Assert.That(motion.Stage, Is.EqualTo(CarroneroCombatMotion.AttackStage.None));
            Assert.That(back, Is.LessThanOrEqualTo(12f), "5 deg of tolerance plus the 6 deg run swing");
        }

        /// The blade capsule SawBotCombat tests hangs from the saw forearm, so the pose decides when damage lands. From
        /// the end of the preparation on, the blade of the new animation must sweep the same volume as the old one.
        [UnityTest, Timeout(120000)] public IEnumerator B_ContactVolume_MatchesLegacyFromTheEndOfPreparation()
        {
            LogAssert.ignoreFailingMessages = true; RequireApplied();
            var mid = runA + runDir * (runLength * .5f); var side = Vector3.Cross(Vector3.up, runDir); var rot = Quaternion.LookRotation(runDir, Vector3.up);
            var legacyBot = Spawn(Base, mid - side * 1.2f, rot); var newBot = Spawn(Variant, mid + side * 1.2f, rot);
            yield return Frames(2); Puppet(legacyBot); Puppet(newBot);
            Vector3 eye = mid + runDir * 5.5f + Vector3.up * 1.1f, look = mid + Vector3.up * .7f;
            for (int i = 0; i < 40; i++) { AimAt(look, eye); yield return null; }
            var field = typeof(SawBotCombat).GetField("attacking", BindingFlags.Instance | BindingFlags.NonPublic);
            Assume.That(field, Is.Not.Null, "SawBotCombat.attacking");
            var combats = new[] { legacyBot.GetComponent<SawBotCombat>(), newBot.GetComponent<SawBotCombat>() };
            foreach (var c in combats) field.SetValue(c, true); // both attack in the same frame, with their AI off
            float prep = combats[1].Preparation; int first = Mathf.CeilToInt(prep / Dt) + 1, count = 44; // 44 frames: two sawing periods
            var bots = new[] { legacyBot, newBot }; var min = new Vector3[2, 2]; var max = new Vector3[2, 2];
            for (int b = 0; b < 2; b++) for (int e = 0; e < 2; e++) { min[b, e] = Vector3.positiveInfinity; max[b, e] = Vector3.negativeInfinity; }
            for (int k = 1; k <= first + count; k++)
            {
                AimAt(look, eye); yield return null;
                if (k <= first) continue;
                for (int b = 0; b < 2; b++)
                {
                    var rig = Rig(bots[b]); var inverse = Quaternion.Inverse(bots[b].transform.rotation);
                    for (int e = 0; e < 2; e++)
                    {
                        var p = inverse * ((e == 0 ? rig.bladeStart : rig.bladeEnd).position - bots[b].transform.position);
                        min[b, e] = Vector3.Min(min[b, e], p); max[b, e] = Vector3.Max(max[b, e], p);
                    }
                }
            }
            foreach (var c in combats) field.SetValue(c, false);
            float worst = 0;
            for (int e = 0; e < 2; e++)
            {
                Note((e == 0 ? "bladeStart" : "bladeEnd") + " swept box, root space: legacy " + min[0, e].ToString("F3") + " .. " + max[0, e].ToString("F3") + " | new " + min[1, e].ToString("F3") + " .. " + max[1, e].ToString("F3"));
                var dMin = min[0, e] - min[1, e]; var dMax = max[0, e] - max[1, e];
                worst = Mathf.Max(worst, Mathf.Max(Mathf.Max(Mathf.Abs(dMin.x), Mathf.Abs(dMin.y), Mathf.Abs(dMin.z)), Mathf.Max(Mathf.Abs(dMax.x), Mathf.Abs(dMax.y), Mathf.Abs(dMax.z))));
            }
            Assert.That(max[0, 1].y - min[0, 1].y, Is.GreaterThan(.1f), "control: the legacy saw is sawing during the sampled window");
            Assert.That(worst, Is.LessThanOrEqualTo(.02f), "blade volume while sawing: largest difference with the legacy animation (2 cm; contact radius is 24 cm)");
        }

        // ------------------------------------------------------------------ (c) A5
        [UnityTest, Timeout(120000)] public IEnumerator C_HitReaction_ShowsInThreeFrames_AndReturns()
        {
            LogAssert.ignoreFailingMessages = true; RequireApplied();
            var mid = runA + runDir * Mathf.Min(6f, runLength * .5f); var rot = Quaternion.LookRotation(-runDir, Vector3.up);
            float[] peak = new float[2], late = new float[2], headPeak = new float[2];
            for (int b = 0; b < 2; b++)
            {
                var bot = Spawn(b == 0 ? Base : Variant, mid, rot); yield return Frames(2); Puppet(bot);
                var health = bot.GetComponent<Health>(); Transform root = bot.transform, chest = Rig(bot).Bone("Spine2"), head = Rig(bot).Bone("Head");
                for (int i = 0; i < 40; i++) { Aim(root); yield return null; }
                Quaternion chest0 = Rel(root, chest), head0 = Rel(root, head); Vector3 at = root.position; float hp = health.CurrentHealth;
                health.TakeDamage(4, player.gameObject);
                for (int k = 1; k <= 18; k++)
                {
                    Aim(root); yield return null;
                    float angle = Quaternion.Angle(chest0, Rel(root, chest));
                    if (k <= 3) { peak[b] = Mathf.Max(peak[b], angle); headPeak[b] = Mathf.Max(headPeak[b], Quaternion.Angle(head0, Rel(root, head))); }
                    if (k == 17) late[b] = angle; // pose written 16 frames (0.27 s) after the hit
                }
                Assert.That(health.CurrentHealth, Is.EqualTo(hp - 4).Within(1e-3f), "damage itself is untouched");
                Assert.That(Vector3.Distance(root.position, at), Is.LessThan(1e-4f), "pose only: the root does not move");
                Object.Destroy(bot); yield return null;
            }
            Note("hit: Spine2 peak in 3 frames: legacy " + F(peak[0]) + " deg, new " + F(peak[1]) + " deg | head peak new " + F(headPeak[1]) + " deg | Spine2 0.27 s later: new " + F(late[1]) + " deg");
            Assert.That(peak[0], Is.LessThan(1f), "control: the legacy animation has no hit reaction, so this measure can fail");
            Assert.That(peak[1], Is.GreaterThanOrEqualTo(8f), "Spine2 must flinch at least 8 deg within 3 frames (R6)");
            Assert.That(headPeak[1], Is.GreaterThanOrEqualTo(8f), "and the head with it");
            Assert.That(late[1], Is.LessThan(1f), "and be back within 0.25 s (R6)");
        }

        // ------------------------------------------------------------------ (d) A6 and the camera gate
        [UnityTest, Timeout(120000)] public IEnumerator D_OffCamera_NothingIsPosed_AndTheReturnDoesNotPop()
        {
            LogAssert.ignoreFailingMessages = true; RequireApplied();
            const float speed = 5f; var rot = Quaternion.LookRotation(runDir, Vector3.up);
            var bot = Spawn(Variant, Lane, rot); yield return Frames(2); Puppet(bot);
            var motion = bot.GetComponent<CarroneroCombatMotion>(); var rig = Rig(bot); Transform root = bot.transform;
            var bones = PosedBones.Select(rig.Bone).Where(t => t).ToArray(); Assert.That(bones.Length, Is.EqualTo(PosedBones.Length), "rig bones");
            int step = 0;
            void Move() { root.SetPositionAndRotation(Lane + runDir * Mathf.PingPong(speed * Dt * step++, LaneLength), rot); }
            for (int i = 0; i < 40; i++) { Move(); Aim(root); yield return null; }
            Assert.That(motion.LodTier, Is.GreaterThanOrEqualTo(0), "visible: the bot is being posed");
            Assert.That(motion.LastPosedFrame, Is.GreaterThanOrEqualTo(Time.frameCount - 2));

            // hide: no camera draws it. If something else still does (an open Scene view), switch its skinned renderers off:
            // the gate is "no SkinnedMeshRenderer enabled and visible", the same as CarroneroMotion.
            var cameras = Object.FindObjectsByType<Camera>().Where(c => c.enabled).ToArray();
            foreach (var c in cameras) c.enabled = false;
            for (int i = 0; i < 4; i++) { Move(); yield return null; }
            var skins = bot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.enabled).ToArray(); bool renderersOff = false;
            if (skins.Any(r => r.isVisible)) { renderersOff = true; foreach (var r in skins) r.enabled = false; for (int i = 0; i < 2; i++) { Move(); yield return null; } }
            Note("hidden by " + (renderersOff ? "switching the skinned renderers off (another camera, probably the Scene view, still drew the bot)" : "disabling every camera"));
            Assert.That(motion.LodTier, Is.EqualTo(-1), "off camera");
            var sentinel = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) { bones[i].localRotation *= Quaternion.Euler(25, 10, 0); sentinel[i] = bones[i].localRotation; }
            float phase = motion.Phase; int posed = motion.LastPosedFrame;
            for (int i = 0; i < 20; i++) { Move(); yield return null; }
            float written = 0; for (int i = 0; i < bones.Length; i++) written = Mathf.Max(written, Quaternion.Angle(sentinel[i], bones[i].localRotation));
            Assert.That(written, Is.LessThan(.01f), "no bone rotation may be written while off camera (R8)");
            Assert.That(motion.LastPosedFrame, Is.EqualTo(posed)); Assert.That(motion.LodTier, Is.EqualTo(-1));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(phase * 360f, motion.Phase * 360f)), Is.GreaterThan(1f), "the gait clock keeps running off camera");

            // back on camera: the first posed frame may not jump (R7), then the pose takes over
            foreach (var r in skins) r.enabled = true;
            foreach (var c in cameras) if (c) c.enabled = true;
            float firstJump = -1;
            for (int i = 0; i < 40 && firstJump < 0; i++)
            {
                Move(); Aim(root); yield return null;
                if (motion.LastPosedFrame == posed) continue;
                firstJump = 0; for (int j = 0; j < bones.Length; j++) firstJump = Mathf.Max(firstJump, Quaternion.Angle(sentinel[j], bones[j].localRotation));
            }
            Assert.That(firstJump, Is.GreaterThanOrEqualTo(0), "the bot was never posed again after coming back");
            for (int i = 0; i < 20; i++) { Move(); Aim(root); yield return null; }
            float moved = 0; for (int i = 0; i < bones.Length; i++) moved = Mathf.Max(moved, Quaternion.Angle(sentinel[i], bones[i].localRotation));
            Note("first frame back on camera: largest bone jump " + F(firstJump) + " deg (limit 8) | 0.33 s later the pose is " + F(moved) + " deg away from the stale one | blend=" + F(motion.BlendWeight));
            Assert.That(firstJump, Is.LessThanOrEqualTo(8f), "first visible frame: largest change of any bone (R7)");
            Assert.That(motion.BlendWeight, Is.EqualTo(1f).Within(1e-4f), "the 0.2 s blend is over");
            Assert.That(moved, Is.GreaterThan(5f), "and the live pose replaced the stale one");
        }

        // ------------------------------------------------------------------ (e) protected zone
        [UnityTest, Timeout(120000)] public IEnumerator E_BasePrefab_KeepsCarroneroMotion_AndTheVariantChangesNoGameplay()
        {
            LogAssert.ignoreFailingMessages = true; RequireApplied();
            var baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Base); var variantAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Variant);
            Assert.That(baseAsset.GetComponentsInChildren<CarroneroCombatMotion>(true), Is.Empty, "Carronero.prefab must not carry the new component");
            Assert.That(baseAsset.GetComponent<CarroneroMotion>().enabled, Is.True, "Carronero.prefab keeps CarroneroMotion enabled");
            Assert.That(PrefabUtility.GetPrefabAssetType(variantAsset), Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(variantAsset), Is.SameAs(baseAsset), "Carronero_Map01 is a direct variant of Carronero");
            Assert.That(variantAsset.GetComponents<CarroneroCombatMotion>().Length, Is.EqualTo(1));
            Assert.That(variantAsset.GetComponent<CarroneroCombatMotion>().enabled, Is.True);
            Assert.That(variantAsset.GetComponent<CarroneroMotion>().enabled, Is.False, "in the variant CarroneroMotion is disabled: only one component writes the pose");

            var mid = runA + runDir * Mathf.Min(6f, runLength * .5f); var side = Vector3.Cross(Vector3.up, runDir); var rot = Quaternion.LookRotation(-runDir, Vector3.up);
            var legacyBot = Spawn(Base, mid - side * 1.5f, rot); var newBot = Spawn(Variant, mid + side * 1.5f, rot);
            for (int i = 0; i < 6; i++) { AimAt(mid + Vector3.up * .7f, mid - runDir * 5f + Vector3.up * 1.1f); yield return null; }
            Assert.That(legacyBot.GetComponentsInChildren<CarroneroCombatMotion>(true), Is.Empty);
            Assert.That(legacyBot.GetComponent<CarroneroMotion>().enabled, Is.True, "protected zone: the hangar and menu Carroñero is animated by CarroneroMotion, as before");
            Assert.That(newBot.GetComponent<CarroneroMotion>().enabled, Is.False);
            Assert.That(newBot.GetComponent<CarroneroCombatMotion>().YieldingToLegacy, Is.False);
            int priority = newBot.GetComponent<NavMeshAgent>().avoidancePriority;
            Assert.That(priority, Is.InRange(40, 59), "CarroneroMotion.Awake still spreads the avoidance priority although the component is disabled");

            // gameplay numbers of the variant are those of the base prefab (read on the assets: the AI rewrites agent.speed every frame)
            SawBotCombat c0 = baseAsset.GetComponent<SawBotCombat>(), c1 = variantAsset.GetComponent<SawBotCombat>(); NavMeshAgent a0 = baseAsset.GetComponent<NavMeshAgent>(), a1 = variantAsset.GetComponent<NavMeshAgent>();
            Assert.That(new[] { c1.Preparation, c1.Reach, c1.DamagePerSecond, c1.DamageInterval, c1.MovementSpeed, a1.speed, a1.acceleration, a1.angularSpeed, a1.radius, variantAsset.GetComponent<Health>().MaxHealth },
                Is.EqualTo(new[] { c0.Preparation, c0.Reach, c0.DamagePerSecond, c0.DamageInterval, c0.MovementSpeed, a0.speed, a0.acceleration, a0.angularSpeed, a0.radius, baseAsset.GetComponent<Health>().MaxHealth }));

            // coexistence: something drives the scavenging API of CarroneroMotion on a combat bot -> the old component takes the pose
            var legacy = newBot.GetComponent<CarroneroMotion>(); var motion = newBot.GetComponent<CarroneroCombatMotion>();
            legacy.CrouchTarget = 1; yield return Frames(3);
            Assert.That(legacy.enabled, Is.True, "CarroneroMotion is handed the pose while its targets are driven"); Assert.That(motion.YieldingToLegacy, Is.True);
            legacy.CrouchTarget = 0; yield return Frames(Mathf.CeilToInt(.8f / Dt));
            Assert.That(legacy.enabled, Is.False, "and gives it back"); Assert.That(motion.YieldingToLegacy, Is.False);

            // files: byte-identical to the backup taken before this phase, and the protected scenes know nothing of the new component
            foreach (var file in new[] { Base, LegacyScript })
            {
                if (!File.Exists(Backup + file)) { Note("no backup copy of " + file + ": hash not compared"); continue; }
                Assert.That(Sha(file), Is.EqualTo(Sha(Backup + file)), file + " differs from the backup of 21/11");
            }
            string script = AssetDatabase.AssetPathToGUID(NewScript), variant = AssetDatabase.AssetPathToGUID(Variant);
            Assert.That(script, Is.Not.Empty);
            foreach (var scene in ProtectedScenes.Where(File.Exists))
            {
                string text = File.ReadAllText(scene);
                Assert.That(text.Contains(script) || text.Contains(variant), Is.False, scene + " must not reference CarroneroCombatMotion or the Map01 variant");
            }
        }

        // ------------------------------------------------------------------ (f) A2
        [UnityTest, Timeout(120000)] public IEnumerator F_TwoIndividuals_NeverShareThePhase()
        {
            LogAssert.ignoreFailingMessages = true; RequireApplied();
            var rot = Quaternion.LookRotation(-runDir, Vector3.up); var side = Vector3.Cross(Vector3.up, runDir);
            var bots = new List<CarroneroCombatMotion>();
            for (int i = 0; i < 24; i++) bots.Add(Spawn(Variant, runB - runDir * (i / 6) * 1.2f + side * ((i % 6 - 2.5f) * 1.1f), rot).GetComponent<CarroneroCombatMotion>());
            var phases = bots.Select(b => b.Phase).ToArray(); // read in the frame they were created
            float Apart(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a * 360f, b * 360f)) / 360f;
            float pair = Apart(phases[0], phases[1]), closest = 1;
            for (int i = 0; i < phases.Length; i++) for (int j = i + 1; j < phases.Length; j++) closest = Mathf.Min(closest, Apart(phases[i], phases[j]));
            yield return Frames(3);
            var cycles = bots.Select(b => b.CycleMeters).ToArray();
            Note("phase: two created together are " + F(pair) + " of a cycle apart | closest pair of 24: " + F(closest) + " | stride per cycle at rest " + F(cycles.Min()) + " .. " + F(cycles.Max()) + " m");
            Assert.That(pair, Is.GreaterThanOrEqualTo(.1f), "two bots created in the same frame (R9)");
            // 24 random phases will sometimes land within a thousandth of each other (measured 0.001 and 0.006 on 21/11/2026); what the horde
            // must not do is march in step: no exact duplicates and the phases spread over the cycle (at least 8 of 12 twelfths occupied).
            Assert.That(closest, Is.GreaterThan(0f), "no two of a horde of 24 share exactly the same phase");
            int twelfths = phases.Select(ph => Mathf.FloorToInt(Mathf.Repeat(ph, 1f) * 12f)).Distinct().Count();
            Assert.That(twelfths, Is.GreaterThanOrEqualTo(8), "the 24 phases spread over the cycle");
            Assert.That(cycles.Max() - cycles.Min(), Is.GreaterThan(.01f), "cadence also differs per individual");
        }

        // ------------------------------------------------------------------ A7
        [UnityTest, Timeout(120000)] public IEnumerator H_Turns_BankInwards_AndStraightRunsStayUpright()
        {
            LogAssert.ignoreFailingMessages = true; RequireApplied();
            const float speed = 4f, yawRate = 220f; // deg/s to the right: the criterion is >= 200 deg/s at >= 3 m/s
            var centre = runA + runDir * Mathf.Min(6f, runLength * .5f);
            var bot = Spawn(Variant, centre, Quaternion.LookRotation(runDir, Vector3.up)); yield return Frames(2); Puppet(bot);
            var motion = bot.GetComponent<CarroneroCombatMotion>(); Transform root = bot.transform, hips = Rig(bot).Bone("Hips"), head = Rig(bot).Bone("Head");
            for (int i = 0; i < 40; i++) { Aim(root); yield return null; }
            Quaternion hips0 = Rel(root, hips), head0 = Rel(root, head);
            // + = the top of the hips leans to the bot's right
            float Lean() { var up = (Rel(root, hips) * Quaternion.Inverse(hips0)) * Vector3.up; return Mathf.Asin(Mathf.Clamp(up.x, -1f, 1f)) * Mathf.Rad2Deg; }

            float yaw = root.eulerAngles.y; Vector3 p = centre; float turnMin = 99, turnMax = -99, headTurn = 0;
            for (int i = 0; i < 75; i++)
            {
                yaw += yawRate * Dt; var r = Quaternion.Euler(0, yaw, 0); p += r * Vector3.forward * (speed * Dt);
                root.SetPositionAndRotation(p, r); Aim(root); yield return null;
                if (i < 30) continue;
                float lean = Lean(); turnMin = Mathf.Min(turnMin, lean); turnMax = Mathf.Max(turnMax, lean);
                headTurn = Mathf.Max(headTurn, Quaternion.Angle(head0, Rel(root, head)));
            }
            float bank = motion.BankDegrees; var heading = Quaternion.Euler(0, yaw, 0); float straight = 0;
            for (int i = 0; i < 50; i++)
            {
                p += heading * Vector3.forward * (speed * Dt); root.SetPositionAndRotation(p, heading); Aim(root); yield return null;
                if (i >= 25) straight = Mathf.Max(straight, Mathf.Abs(Lean()));
            }
            Note("turn right at " + F(yawRate) + " deg/s and " + F(speed) + " m/s: hips lean inwards " + F(turnMin) + " .. " + F(turnMax) + " deg (component " + F(bank) + ") | head leads by up to " + F(headTurn) + " deg | straight afterwards: " + F(straight) + " deg");
            Assert.That(turnMin, Is.GreaterThanOrEqualTo(6f), "bank into the turn, towards the inside (right)");
            Assert.That(turnMax, Is.LessThanOrEqualTo(12.5f), "bank is capped at 12 deg");
            Assert.That(headTurn, Is.GreaterThanOrEqualTo(8f), "the head leads the turn");
            Assert.That(straight, Is.LessThanOrEqualTo(1f), "upright on a straight run");
        }

        // ------------------------------------------------------------------ LOD tiers
        [UnityTest, Timeout(120000)] public IEnumerator I_LodTier_FollowsTheLodGroup_AndFarLevelsSkipTheFootIk()
        {
            LogAssert.ignoreFailingMessages = true; RequireApplied();
            const float speed = 5.6f; var rot = Quaternion.LookRotation(runDir, Vector3.up);
            var bot = Spawn(Variant, Lane, rot); yield return Frames(2); Puppet(bot);
            var motion = bot.GetComponent<CarroneroCombatMotion>(); var group = Rig(bot).GetComponent<LODGroup>(); Assume.That(group, Is.Not.Null, "LODGroup on the Carroñero visual");
            int step = 0; var seen = new int[4]; var planted = new int[4];
            for (int lod = 0; lod < 4; lod++)
            {
                group.ForceLOD(lod);
                for (int i = 0; i < 50; i++)
                {
                    bot.transform.SetPositionAndRotation(Lane + runDir * Mathf.PingPong(speed * Dt * step++, LaneLength), rot); Aim(bot.transform); yield return null;
                    if (i < 25) continue;
                    seen[lod] = motion.LodTier; if (motion.LeftFootPlanted || motion.RightFootPlanted) planted[lod]++;
                }
            }
            group.ForceLOD(-1);
            Note("forced LOD -> tier read by the component: " + string.Join(", ", seen) + " | frames with a planted foot (of 25): " + string.Join(", ", planted));
            for (int lod = 0; lod < 4; lod++) Assert.That(seen[lod], Is.EqualTo(lod), "the tier must be the LOD level the LODGroup draws (forced LOD" + lod + ")");
            // The foot IK runs up to the tuned level (ikMaxLod; 0 since the cost measurement of 21/11/2026): levels beyond it use the cheap legs.
            int ikMax = new UnityEditor.SerializedObject(motion).FindProperty("ikMaxLod").intValue;
            for (int lod = 0; lod < 4; lod++)
            {
                if (lod <= ikMax) Assert.That(planted[lod], Is.GreaterThan(5), "LOD" + lod + " solves the foot IK (ikMaxLod=" + ikMax + ")");
                else Assert.That(planted[lod], Is.EqualTo(0), "LOD" + lod + " uses the cheap legs (ikMaxLod=" + ikMax + ")");
            }
        }

        // ------------------------------------------------------------------ cost, same load, old against new
        /// 24 Carroñeros running a circle in view, AI off (its cost is not animation). Three states on the very same
        /// bots: new animation, none, legacy animation. "LateBehaviourUpdate" is the Editor marker the 1.25 ms figure
        /// of the checklist comes from. Editor numbers, not a build.
        [UnityTest, Timeout(120000)] public IEnumerator Z_Cost_HordeOf24_NewAnimationAgainstLegacy()
        {
            LogAssert.ignoreFailingMessages = true; RequireApplied();
            const int count = 24; const float radius = 5f, speed = 5.6f;
            var centre = runA + runDir * (runLength * .5f); var bots = new GameObject[count];
            for (int i = 0; i < count; i++) bots[i] = Spawn(Variant, centre + Quaternion.Euler(0, i * 360f / count, 0) * Vector3.forward * radius, Quaternion.identity);
            yield return Frames(2);
            foreach (var b in bots) Puppet(b);
            var motions = bots.Select(b => b.GetComponent<CarroneroCombatMotion>()).ToArray(); var legacies = bots.Select(b => b.GetComponent<CarroneroMotion>()).ToArray();
            Vector3 eye = centre - runDir * 9f + Vector3.up * 9f; cam.fieldOfView = 60; float angle = 0;
            void Move()
            {
                angle += speed / radius * Mathf.Rad2Deg * Dt;
                for (int i = 0; i < count; i++)
                {
                    var r = Quaternion.Euler(0, angle + i * 360f / count, 0);
                    bots[i].transform.SetPositionAndRotation(centre + r * Vector3.forward * radius, r * Quaternion.Euler(0, 90, 0));
                }
                AimAt(centre, eye);
            }
            var marker = new ProfilerRecorder("LateBehaviourUpdate"); marker.Start();
            var ms = new double[3]; var frame = new double[3]; var samples = new int[3]; var tiers = new int[5];
            string[] labels = { "new (CarroneroCombatMotion)", "none", "legacy (CarroneroMotion)" };
            for (int round = 0; round < 2; round++)
                for (int state = 0; state < 3; state++)
                {
                    for (int i = 0; i < count; i++) { legacies[i].enabled = state == 2; motions[i].enabled = state == 0; }
                    for (int i = 0; i < 150; i++)
                    {
                        Move(); yield return null;
                        if (i < 30) continue;
                        if (marker.Valid) ms[state] += marker.LastValue * 1e-6; frame[state] += Time.unscaledDeltaTime * 1000.0; samples[state]++;
                        if (state == 0 && i == 149) foreach (var m in motions) tiers[Mathf.Clamp(m.LodTier + 1, 0, 4)]++;
                    }
                }
            bool valid = marker.Valid; marker.Dispose();
            for (int i = 0; i < count; i++) { legacies[i].enabled = false; motions[i].enabled = true; }
            for (int s = 0; s < 3; s++) { ms[s] /= Mathf.Max(1, samples[s]); frame[s] /= Mathf.Max(1, samples[s]); }
            double costNew = ms[0] - ms[1], costOld = ms[2] - ms[1];
            Note("24 bots at 5.6 m/s, " + samples[0] + " frames per state, Editor. LOD tiers of the new animation (off camera, 0, 1, 2, 3): " + string.Join(", ", tiers));
            for (int s = 0; s < 3; s++) Note(labels[s].PadRight(30) + " LateBehaviourUpdate=" + (valid ? ms[s].ToString("0.000", CultureInfo.InvariantCulture) + " ms" : "n/a") + " | frame=" + frame[s].ToString("0.00", CultureInfo.InvariantCulture) + " ms");
            Note("animation cost for 24: new " + costNew.ToString("0.000", CultureInfo.InvariantCulture) + " ms, legacy " + costOld.ToString("0.000", CultureInfo.InvariantCulture) + " ms (per bot " + (costNew / count * 1000).ToString("0.0", CultureInfo.InvariantCulture) + " vs " + (costOld / count * 1000).ToString("0.0", CultureInfo.InvariantCulture) + " us). If the new one is higher: VisualOverhaulCarronero.Tune ikMaxLod=0, then halfRateFromLod=1");
            Assume.That(valid, Is.True, "the LateBehaviourUpdate marker is not available: only frame times were recorded (see the report file)");
            Assume.That(tiers[0], Is.LessThan(count / 2), "most of the horde was off camera: the comparison is not valid");
            Assert.That(costNew, Is.LessThanOrEqualTo(costOld * 1.1 + .03), "the new animation must cost the same as the legacy one or less (10 % and 0.03 ms of measurement noise allowed)");
        }

        static string Sha(string path)
        {
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(File.ReadAllBytes(path)).Select(b => b.ToString("x2")));
        }
    }
}
