using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    // Executions v02 (22/11/2026). Real E / M1 / F / 1 / 2 / G through virtual Input System devices, 60 Hz fixed, sampled at
    // the end of every frame (after LateUpdate, so reaction layers, camera kicks and hand IK are what is measured).
    // Same lab and placement as ArmsFixPassTests / ExecutionsF0Capture. Guion: Docs/IronRequiem/Executions/F1_PROPUESTA.md.
    public sealed class ExecutionsV02Tests
    {
        const string Lab = "Assets/IronRequiem/Scenes/VisualOverhaul/MainScene_VisualLab.unity";
        const string Reports = "Tools/IronRequiem/Reports/Executions/tests";
        const string T07 = "Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab";
        const string T07Mech = "Assets/IronRequiem/Prefabs/Campaign/T07_Mech.prefab";
        const string Saw = "Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab";
        const string Big = "Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab";
        const float Frame = 1f / 60;
        // guion F1 (execution clock, seconds since E)
        const float Yank = .34f, Strike = .732f, BigShot = .95f, Tolerance = .05f;
        const float ShortLimit = 1.2f, BigLimit = 2.0f, MaxGripDepth = .6f;

        PlayerCharacterController player; PlayerWeaponsManager weapons; StolenWeaponCarrier carrier; ProtagonistHandsPresentation hands; ProtagonistMelee melee;
        AdvancedPlayerMovement movement; BigBotExecutionSequence bigSeq; Health playerHealth;
        Keyboard keys; Mouse mouse; UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices; string savedOverrides;
        bool background; float capture;
        GameObject target; EnemyExecutionSource source; Health targetHealth; Bounds bounds;
        readonly List<string> log = new List<string>();

        [UnitySetUp] public IEnumerator Setup()
        {
            log.Clear(); weaponViews = null; pieces = null; Directory.CreateDirectory(Reports);
            capture = Time.captureDeltaTime; background = Application.runInBackground; Application.runInBackground = true; Time.captureDeltaTime = Frame; Time.timeScale = 1; AudioListener.pause = false;
            EditorSceneManager.LoadSceneInPlayMode(Lab, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            var menu = Object.FindAnyObjectByType<CombatLabMenu>(); if (menu) { menu.BeginGame(); menu.enabled = false; }
            var waves = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); if (waves) waves.enabled = false;
            var flow = Object.FindAnyObjectByType<GameFlowManager>(); if (flow) flow.enabled = false;
            var objectives = Object.FindAnyObjectByType<ObjectiveManager>(); if (objectives) objectives.enabled = false;
            Time.timeScale = 1;
            foreach (var e in Object.FindObjectsByType<EnemyController>()) e.gameObject.SetActive(false);
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); weapons = player.GetComponent<PlayerWeaponsManager>(); carrier = player.GetComponent<StolenWeaponCarrier>();
            hands = player.GetComponent<ProtagonistHandsPresentation>(); melee = player.GetComponent<ProtagonistMelee>(); movement = player.GetComponent<AdvancedPlayerMovement>();
            bigSeq = player.GetComponent<BigBotExecutionSequence>(); playerHealth = player.GetComponent<Health>(); playerHealth.Invincible = true;
            // default bindings for the test (the user's saved remaps, e.g. Melee on the right mouse button, are restored in Cleanup)
            savedOverrides = InputSystem.actions.SaveBindingOverridesAsJson(); InputSystem.actions.RemoveAllBindingOverrides();
            previousDevices = InputSystem.actions.devices; keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse };
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "ExecV02Floor"; floor.transform.position = new Vector3(0, 29.5f, 0); floor.transform.localScale = new Vector3(80, 1, 80);
            Move(new Vector3(0, 30.05f, -12)); Cursor.lockState = CursorLockMode.Locked;
            yield return new WaitForSeconds(.6f);
            weapons.SwitchToWeaponIndex(0);
            yield return new WaitForSeconds(1f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (savedOverrides != null) { InputSystem.actions.RemoveAllBindingOverrides(); if (savedOverrides.Length > 0) InputSystem.actions.LoadBindingOverridesFromJson(savedOverrides); savedOverrides = null; }
            InputSystem.actions.devices = previousDevices; if (keys != null) InputSystem.RemoveDevice(keys); if (mouse != null) InputSystem.RemoveDevice(mouse);
            Time.timeScale = 1; Time.captureDeltaTime = capture; AudioListener.pause = false; Cursor.lockState = CursorLockMode.None;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("ExecV02Cleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene); Application.runInBackground = background;
        }

        void Move(Vector3 p) { var c = player.GetComponent<CharacterController>(); c.enabled = false; player.transform.SetPositionAndRotation(p, Quaternion.identity); c.enabled = true; player.CharacterVelocity = Vector3.zero; Physics.SyncTransforms(); }
        void Keys(params Key[] pressed) { Cursor.lockState = CursorLockMode.Locked; InputSystem.QueueStateEvent(keys, new KeyboardState(pressed)); }
        void Fire(bool held) { var s = new MouseState(); if (held) s = s.WithButton(MouseButton.Left); InputSystem.QueueStateEvent(mouse, s); }
        void Write(string name) => File.WriteAllText(Reports + "/" + name + ".txt", string.Join("\n", log));

        // Spawns a campaign robot on the test platform at 50 % health, AI off (as the execution itself does), and stands the
        // player `standOff` metres in front of it looking at it. wallBehind: a wall 0.5 m behind the robot.
        IEnumerator Spawn(string prefabPath, float standOff, bool big, bool wallBehind = false)
        {
            LogAssert.ignoreFailingMessages = true; // harness: EnemyController without EnemyMobile logs on detection
            yield return new WaitForEndOfFrame();
            target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), new Vector3(0, 30.05f, -6), Quaternion.Euler(0, 180, 0));
            foreach (var b in new Behaviour[] { target.GetComponent<EnemyMobile>(), target.GetComponent<UnityEngine.AI.NavMeshAgent>() }) if (b) b.enabled = false;
            yield return null;
            var controller = target.GetComponent<EnemyController>(); if (controller) controller.enabled = false;
            yield return null; yield return new WaitForSeconds(.5f);
            source = target.GetComponentInChildren<EnemyExecutionSource>(); Assert.That(source, Is.Not.Null, prefabPath);
            targetHealth = source.GetComponent<Health>(); targetHealth.TakeDamage(targetHealth.MaxHealth * .5f, player.gameObject); yield return new WaitForSeconds(1.2f); // real damage event: stages applied before E
            var cols = target.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();
            bounds = cols[0].bounds; foreach (var c in cols) bounds.Encapsulate(c.bounds);
            if (wallBehind) { var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "ExecV02Wall"; wall.transform.position = new Vector3(bounds.center.x, 31.5f, bounds.max.z + .5f + .1f); wall.transform.localScale = new Vector3(6, 3, .2f); }
            // the controller stays enabled (as in the game: grounded, band C micro-step possible); the view is set through it
            player.enabled = true;
            Move(new Vector3(bounds.center.x, bounds.min.y + .03f, bounds.min.z - standOff));
            var look = (big ? bounds.center + Vector3.up * .3f : bounds.center) - player.PlayerCamera.transform.position;
            player.SetViewAngles(Quaternion.LookRotation(look).eulerAngles.y, -Mathf.Asin(look.normalized.y) * Mathf.Rad2Deg); Physics.SyncTransforms(); Cursor.lockState = CursorLockMode.Locked;
            yield return null; yield return null;
            Assert.That(source.ExecutionEnabled, Is.True, prefabPath + ": execution enabled in the prefab");
            Assert.That(source.Eligible, Is.True, prefabPath + " below 75 %");
            Assert.That(carrier.ExecutionCandidate, Is.SameAs(source), "E would execute it (status " + carrier.ExecutionStatus + ")");
        }

        sealed class Run
        {
            public float startTime; public int frames; public readonly List<float> impacts = new List<float>(); public int deaths; public float deathAt = -1;
            public float handsHiddenAt = -1, busyEndsAt = -1, controlBackAt = -1, maxGripDepth = float.MinValue, maxStruggleGap, maxCamDeg; public int camFramesOver1, frozenAlive, hitStops, maxBoneJumpFrames;
            public readonly List<float> armGaps = new List<float>();
            public int shotChecked; public bool shotWithStolen, proxyAtShot; public float maxProxySize;
            public int ArmFramesOff => armGaps.Count(x => x > .08f);
            public float minMeshGap = 9, minMeshGapAt = -1, minCannonGap = 9, minCannonGapAt = -1;
            public float maxBoneStep, maxWeaponGap, maxArmGap, minCameraGap = 9, distanceAt12 = -1, minCameraGapAt = -1; public int robotOutOfFrame; public bool disarmSeen; public readonly List<float> frozenAt = new List<float>(); public int m1Shots; public int serial0; public int switchEvents; public bool timeScaleOk = true;
        }

        // Presses E (real key), then samples until the execution and its presentation are over. Keys pressed while busy:
        // the ARMS_FIX schedule (all must be ignored).
        IEnumerator Execute(Run run, bool big, float seconds, System.Func<float, IEnumerator> midAction = null, float midAt = -1)
        {
            // skin bones plus the transforms that carry rigid pieces (the T-07 Mech body is made of MeshRenderers parented to its rig)
            var bones = target.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s => s.enabled).SelectMany(s => s.bones)
                .Concat(target.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && r.transform.parent).Select(r => r.transform.parent))
                .Where(b => b && !b.name.Contains("Wheel") && !b.name.Contains("Chain")).Distinct().ToArray();
            log.Add($"motion probe: {bones.Length} bones ({target.GetComponentsInChildren<SkinnedMeshRenderer>().Count(x => x.enabled)} enabled skins, {target.GetComponentsInChildren<MeshRenderer>().Count(x => x.enabled)} mesh renderers)");
            var prev = bones.Select(b => Quaternion.Inverse(target.transform.rotation) * b.rotation).ToArray();
            foreach (var d in target.GetComponentsInChildren<Damageable>(true)) d.Impact += (p, n, s) => run.impacts.Add(Time.time - run.startTime);
            targetHealth.OnDie += () => { run.deaths++; run.deathAt = Time.time - run.startTime; };
            var active0 = weapons.GetActiveWeapon(); void Shot() => run.m1Shots++; if (active0) active0.OnShootProcessed += Shot;
            void Switched(WeaponController w) => run.switchEvents++; weapons.OnSwitchedToWeapon += Switched;
            run.serial0 = melee ? melee.AttackSerial : 0;
            var camT = player.PlayerCamera.transform; var prevCam = camT.rotation;
            var schedule = new List<(float at, string what)> { (.05f, "M1 down"), (.15f, "F"), (.25f, "2"), (.30f, "M1 up"), (.55f, "F"), (.72f, "G"), (.78f, "1") };
            int next = 0; bool keyDown = false;
            run.startTime = Time.time; Keys(Key.E); yield return null; Keys();
            Assert.That(carrier.Held, Is.Not.Null, "E starts the execution (weapon delivered)");
            bool midDone = false;
            for (int f = 0; f < Mathf.CeilToInt(seconds / Frame); f++)
            {
                yield return new WaitForEndOfFrame();
                float clock = Time.time - run.startTime, real = (f + 1) * Frame;
                if (Time.timeScale < .9f && Time.timeScale > 0) run.hitStops++;
                if (keyDown) { Keys(); keyDown = false; }
                else if (next < schedule.Count && clock >= schedule[next].at && carrier.ExecutionBusy)
                {
                    var what = schedule[next++].what;
                    if (what == "M1 down") Fire(true); else if (what == "M1 up") Fire(false);
                    else { Keys(what == "F" ? Key.F : what == "G" ? Key.G : what == "1" ? Key.Digit1 : Key.Digit2); keyDown = true; }
                }
                if (!midDone && midAction != null && clock >= midAt) { midDone = true; yield return midAction(clock); }
                // camera rotation not caused by the mouse (no mouse input is sent)
                float camDeg = Quaternion.Angle(prevCam, camT.rotation); prevCam = camT.rotation;
                run.maxCamDeg = Mathf.Max(run.maxCamDeg, camDeg); if (camDeg > 1) run.camFramesOver1++; if (big && camDeg > 1.2f) log.Add($"  cam turn {camDeg:F2} deg at {clock:F3}");
                // the grabbing hand never reaches past the arm envelope; it closes on the real joint while struggling
                if (hands.LeftHandVisible && hands.LeftHandBone)
                {
                    var local = camT.InverseTransformPoint(hands.LeftHandBone.position);
                    if (clock < (big ? .80f : Yank)) run.maxGripDepth = Mathf.Max(run.maxGripDepth, local.z);
                    var joint = source ? source.WeaponJoint : null;
                    bool struggling = big ? clock >= .50f && clock < .62f : clock >= .22f && clock < Yank;
                    if (struggling && joint) run.maxStruggleGap = Mathf.Max(run.maxStruggleGap, Vector3.Distance(hands.LeftHandBone.position, joint.position));
                    if (struggling) run.maxWeaponGap = Mathf.Max(run.maxWeaponGap, WeaponGap(hands.LeftHandBone.position));
                    // Big Bot: its skinned cannon bounds span the robot, so the hand is also measured to the cannon arm bones (Forearm_R -> Pincer_R)
                    // Big Bot: the hand to the cannon's real triangles (bind-pose placed), not to its oversized skinned bounds
                    if (struggling && big) { MeshGaps(hands.LeftHandBone.position, out _, out float handCannon); run.maxArmGap = Mathf.Max(run.maxArmGap, handCannon); run.armGaps.Add(handCannon); }
                    if (joint && f % 3 == 0 && clock < (big ? 1f : .5f)) log.Add($"  t {clock:F3} hand {local.ToString("F2")} joint {camT.InverseTransformPoint(joint.position).ToString("F2")} ({joint.name}) robot dz {(target ? target.transform.position.z + 6 : 0):F2}");
                }
                // camera never inside the robot body (reviewer finding): distance to its colliders, the grabbed weapon excluded
                if (target && targetHealth && targetHealth.CurrentHealth > 0) { float g = CameraBodyGap(camT.position); if (g < run.minCameraGap) { run.minCameraGap = g; run.minCameraGapAt = clock; } }
                if (big)
                {
                    var vp = player.PlayerCamera.WorldToViewportPoint(bounds.center);
                    if (clock >= .4f && clock <= 1f && (vp.z <= 0 || vp.x < .15f || vp.x > .85f || vp.y < .15f || vp.y > .85f)) run.robotOutOfFrame++;
                    if (run.distanceAt12 < 0 && clock >= 1.2f) run.distanceAt12 = Vector3.Distance(camT.position, bounds.center);
                    { var carryNow = player.GetComponent<ExecutionDirector>().Carry; if (carryNow && carryNow.Visible && carryNow.Module) { var pb = new Bounds(); bool first = true; foreach (var r in carryNow.Module.GetComponentsInChildren<Renderer>()) if (r.enabled && !(r is ParticleSystemRenderer)) { if (first) { pb = r.bounds; first = false; } else pb.Encapsulate(r.bounds); } if (!first && player.PlayerCamera.WorldToViewportPoint(pb.center).z < .6f) run.maxProxySize = Mathf.Max(run.maxProxySize, pb.size.magnitude); } } // near the lens only: at birth it stands where the robot's cannon was
                    if (run.shotChecked == 0 && clock >= BigShot - .02f) { run.shotChecked = 1; var aw = weapons.GetActiveWeapon(); var carry = player.GetComponent<ExecutionDirector>().Carry; run.shotWithStolen = carrier.Held && aw == carrier.Held.EquippedWeapon && aw.IsWeaponActive; run.proxyAtShot = carry && carry.Visible; log.Add($"  max proxy size so far {run.maxProxySize:F2} m; at the shot ({clock:F3}): active {(aw ? aw.name : "none")} stolen-active {run.shotWithStolen} proxy visible {run.proxyAtShot}"); }
                    if (f % 2 == 0 && target && targetHealth.CurrentHealth > 0)
                    {
                        MeshGaps(camT.position, out float bodyGap, out float cannonGap);
                        if (bodyGap < run.minMeshGap) { run.minMeshGap = bodyGap; run.minMeshGapAt = clock; }
                        if (cannonGap < run.minCannonGap) { run.minCannonGap = cannonGap; run.minCannonGapAt = clock; }
                    }
                    if ((f % 3 == 0 || clock > .48f && clock < .66f) && clock < 1.6f) log.Add($"  big t {clock:F3} cam-centre {(camT.position - bounds.center).ToString("F2")} vp {vp.ToString("F2")} gap {(target && targetHealth.CurrentHealth > 0 ? CameraBodyGap(camT.position) : -1):F2} yaw {camT.eulerAngles.y:F1} pitch {camT.eulerAngles.x:F1}{BigTrace(camT.position)}");
                }
                // robot alive: not frozen, no pose jumps
                if (target)
                {
                    float maxStep = 0; var rootRot = Quaternion.Inverse(target.transform.rotation);
                    for (int i = 0; i < bones.Length; i++) { if (!bones[i]) continue; var r = rootRot * bones[i].rotation; maxStep = Mathf.Max(maxStep, Quaternion.Angle(prev[i], r)); prev[i] = r; }
                    bool alive = targetHealth && targetHealth.CurrentHealth > 0;
                    if (alive && f > 0 && maxStep < .1f) { run.frozenAlive++; run.frozenAt.Add(clock); }
                    if (alive) run.maxBoneStep = Mathf.Max(run.maxBoneStep, maxStep);
                }
                if (run.busyEndsAt < 0 && f > 2 && !carrier.ExecutionBusy) run.busyEndsAt = real;
                if (hands.IsDisarming) run.disarmSeen = true;
                if (run.handsHiddenAt < 0 && run.disarmSeen && !hands.IsDisarming && !hands.LeftHandVisible) run.handsHiddenAt = real;
                if (run.controlBackAt < 0 && f > 2 && !(bigSeq && bigSeq.IsRunning) && !(movement && movement.IsScripted)) run.controlBackAt = real;
                run.frames = f + 1;
            }
            if (keyDown) Keys(); Fire(false); if (active0) active0.OnShootProcessed -= Shot; weapons.OnSwitchedToWeapon -= Switched;
            yield return null;
            run.timeScaleOk = Mathf.Approximately(Time.timeScale, 1);
            log.Add($"impacts [{string.Join(", ", run.impacts.Select(x => x.ToString("F3")))}] deaths {run.deaths} at {run.deathAt:F3}; hands hidden {run.handsHiddenAt:F3} s real, busy ends {run.busyEndsAt:F3}, control back {run.controlBackAt:F3}");
            log.Add($"grip depth max {run.maxGripDepth:F3} m, struggle hand-joint bone max {run.maxStruggleGap:F3} m, hand-weapon surface max {run.maxWeaponGap:F3} m; camera max {run.maxCamDeg:F2} deg/frame, frames >1 deg {run.camFramesOver1}; hit-stop frames {run.hitStops}; camera-body gap min {run.minCameraGap:F2} m at {run.minCameraGapAt:F2} s; robot frozen alive frames {run.frozenAlive} at [{string.Join(" ", run.frozenAt.Select(x => x.ToString("F2")))}], max bone step {run.maxBoneStep:F1} deg");
            log.Add($"Big Bot framing: frames with the robot out of the 15-85 % frame (0.4-1.0 s) {run.robotOutOfFrame}, camera-robot distance at 1.2 s {run.distanceAt12:F2} m, hand-cannon surface max {run.maxArmGap:F3} m frames > 0.08 m {run.ArmFramesOff}/{run.armGaps.Count} [{string.Join(" ", run.armGaps.Select(x => x.ToString("F2")))}]; camera to the visible mesh ({MeshInfo}): body min {run.minMeshGap:F2} m at {run.minMeshGapAt:F2} s, cannon min {run.minCannonGap:F2} m at {run.minCannonGapAt:F2} s");
            log.Add($"M1 shots of the outgoing weapon {run.m1Shots}, melee serial {run.serial0}->{(melee ? melee.AttackSerial : 0)}, switch events {run.switchEvents}, held {(carrier.Held ? carrier.Held.name : "none")}, timeScale {Time.timeScale}");
        }


        // Distance from the hand to the robot's mounted weapon (closest point of its renderer bounds), same renderers the
        // robot hides at the Rip (ExecutionWeaponDetach): saw views, Big Bot cannon views, T-07 attached cannon.
        List<Renderer> weaponViews;
        float WeaponGap(Vector3 hand)
        {
            if (weaponViews == null)
            {
                weaponViews = new List<Renderer>();
                var saw = target.GetComponent<SawBotWeaponSource>(); var bigD = target.GetComponent<BigBotDestruction>();
                if (saw) weaponViews.AddRange(saw.RightSawRenderers.Where(r => r));
                else if (bigD) weaponViews.AddRange(bigD.CannonViews.Where(r => r));
                else
                {
                    Transform mounted = target.GetComponentsInChildren<StolenCannon>(true).Where(c => c.State == StolenCannonState.Attached).Select(c => c.transform).FirstOrDefault();
                    if (!mounted) mounted = target.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CannonModule" || t.name == "VPD_CannonModule" || t.name == "T07Mech_Cannon");
                    if (mounted) weaponViews.AddRange(mounted.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)));
                }
            }
            float best = float.MaxValue;
            foreach (var r in weaponViews) if (r && r.gameObject.activeInHierarchy && r.enabled) best = Mathf.Min(best, Vector3.Distance(hand, r.bounds.ClosestPoint(hand)));
            return best == float.MaxValue ? 9 : best;
        }


        // Big Bot: the camera's distance to the triangles of the robot's LOD0 meshes, baked in their live pose (the real armour, not
        // the gameplay box nor the skinned bounds); body pieces and cannon pieces (BigBotDestruction.CannonViews) apart.
        // BakeMesh returns degenerate vertices for these non-readable, 348x-scaled skins, so each rigid piece ("Torso_LOD0" rides bone
        // "Torso") is placed through its bind pose: world = bone.localToWorld * bindpose * vertex.
        sealed class Piece { public Transform bone; public Matrix4x4 bind; public Vector3[] verts; public int[] tris; public bool cannon; }
        List<Piece> pieces; public string MeshInfo = "-";
        void MeshGaps(Vector3 cam, out float body, out float cannon)
        {
            body = cannon = 9;
            if (pieces == null)
            {
                pieces = new List<Piece>(); var cannonViews = new HashSet<Renderer>(ExecutionReachResolver.WeaponViews(source)); int empty = 0;
                foreach (var skin in target.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    int lod = skin.name.IndexOf("_LOD0"); if (lod <= 0 || !skin.sharedMesh) continue;
                    var bones = skin.bones; int bi = System.Array.FindIndex(bones, b => b && b.name == skin.name.Substring(0, lod)); if (bi < 0) continue;
                    var mesh = skin.sharedMesh; var v = new List<Vector3>(); mesh.GetVertices(v); if (v.Count == 0) { empty++; continue; }
                    pieces.Add(new Piece { bone = bones[bi], bind = mesh.bindposes[bi], verts = v.ToArray(), tris = mesh.triangles, cannon = cannonViews.Contains(skin) });
                }
                MeshInfo = $"{pieces.Count} pieces ({pieces.Sum(p => p.tris.Length / 3)} triangles), {empty} unreadable";
            }
            foreach (var piece in pieces)
            {
                if (!piece.bone) continue;
                var m = piece.bone.localToWorldMatrix * piece.bind; float best = 9;
                var w = new Vector3[piece.verts.Length]; for (int i = 0; i < w.Length; i++) w[i] = m.MultiplyPoint3x4(piece.verts[i]);
                for (int i = 0; i < piece.tris.Length; i += 3) best = Mathf.Min(best, Vector3.Distance(cam, ClosestOnTriangle(cam, w[piece.tris[i]], w[piece.tris[i + 1]], w[piece.tris[i + 2]])));
                if (piece.cannon) cannon = Mathf.Min(cannon, best); else body = Mathf.Min(body, best);
            }
        }
        // Ericson, Real-Time Collision Detection 5.1.5
        static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a; float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap); if (d1 <= 0 && d2 <= 0) return a;
            Vector3 bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp); if (d3 >= 0 && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2; if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + d1 / (d1 - d3) * ab;
            Vector3 cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp); if (d6 >= 0 && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6; if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + d2 / (d2 - d6) * ac;
            float va = d3 * d6 - d5 * d4; if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (d4 - d3) / ((d4 - d3) + (d5 - d6)) * (c - b);
            float denom = 1 / (va + vb + vc); return a + ab * (vb * denom) + ac * (vc * denom);
        }

        string BigTrace(Vector3 cam)
        {
            var dir = player.GetComponent<ExecutionDirector>(); var carry = dir ? dir.Carry : null; var aw0 = weapons.GetActiveWeapon();
            string carryInfo = "";
            if (carry && carry.Visible && carry.Module) { var pb = new Bounds(); bool first = true; foreach (var r in carry.Module.GetComponentsInChildren<Renderer>()) if (r.enabled && !(r is ParticleSystemRenderer)) { if (first) { pb = r.bounds; first = false; } else pb.Encapsulate(r.bounds); } var pvp = player.PlayerCamera.WorldToViewportPoint(pb.center); carryInfo = $" proxy size {pb.size.magnitude:F2} depth {pvp.z:F2} vp ({pvp.x:F2},{pvp.y:F2})"; }
            string w = $" | active {(aw0 ? aw0.name.Replace("(Clone)", "") + (aw0.IsWeaponActive ? "+" : "-") : "none")} carry {(carry ? carry.StageName : "-")}{carryInfo}";
            if (!target || targetHealth.CurrentHealth <= 0) return w;
            var j = source.WeaponJoint; var tip = j ? j.Find("Pincer_R") : null; var px = target.GetComponent<ExecutionBodyProxy>();
            Vector3 wp = cam; bool has = px && px.WeaponPoint(cam, out wp); MeshGaps(cam, out float b, out float c);
            var anim = target.GetComponentInChildren<Animator>();
            var aw = bigSeq.AnchorWorld; MeshGaps(aw, out _, out float anchorMesh);
            float handMesh = -1; if (hands.LeftHandVisible && hands.LeftHandBone) MeshGaps(hands.LeftHandBone.position, out _, out handMesh); var hl = hands.LeftHandBone ? player.PlayerCamera.transform.InverseTransformPoint(hands.LeftHandBone.position) : Vector3.zero; var al = player.PlayerCamera.transform.InverseTransformPoint(aw);
            return w + $" | hand-cannon {handMesh:F2} hand {hl.ToString("F2")} anchor {al.ToString("F2")} anchor-cam {Vector3.Distance(aw, cam):F2} anchor-mesh {anchorMesh:F2} root {player.transform.position.ToString("F2")} tip {(tip ? tip.position.ToString("F2") : "-")} proxyCannon {(has ? Vector3.Distance(cam, wp).ToString("F2") : "-")} meshCannon {c:F2} meshBody {b:F2} anim {(anim ? anim.enabled + " " + anim.GetCurrentAnimatorStateInfo(0).shortNameHash : "-")}";
        }

        float CameraBodyGap(Vector3 cam)
        {
            WeaponGap(cam); // fills weaponViews
            float best = 9;
            foreach (var c in target.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c.isTrigger || weaponViews.Any(v => v && (c.transform.IsChildOf(v.transform) || v.transform.IsChildOf(c.transform) && c.transform != target.transform && c.GetType() != typeof(CharacterController)))) continue;
                if (c.attachedRigidbody && !c.attachedRigidbody.isKinematic) continue; // loose debris
                best = Mathf.Min(best, Vector3.Distance(cam, c.ClosestPoint(cam)));
            }
            return best;
        }

        void AssertRobotLegal(string label)
        {
            if (!target) return; // destroyed by its own death flow
            var r = target.transform.position;
            Assert.That(Mathf.Abs(r.y - 30.05f), Is.LessThanOrEqualTo(.05f), label + ": robot root height unchanged (not floating / sunk)");
            var overlaps = Physics.OverlapCapsule(r + Vector3.up * .5f, r + Vector3.up * 1.2f, .3f, -1, QueryTriggerInteraction.Ignore)
                .Where(c => !c.transform.IsChildOf(target.transform) && !c.transform.IsChildOf(player.transform) && c.name != "ExecV02Floor" && !(c.attachedRigidbody && !c.attachedRigidbody.isKinematic)).Select(c => c.name).ToArray();
            Assert.That(overlaps, Is.Empty, label + ": robot root inside geometry");
        }

        void AssertShort(Run run, string label)
        {
            Assert.That(run.impacts.Count, Is.EqualTo(2), label + ": exactly two blows");
            Assert.That(run.impacts[0], Is.EqualTo(Yank).Within(Tolerance), label + ": blow 1 = rip");
            Assert.That(run.deaths, Is.EqualTo(1), label + ": one death");
            Assert.That(run.deathAt, Is.EqualTo(Strike).Within(Tolerance), label + ": death with the punch");
            Assert.That(run.impacts.All(t => t <= run.deathAt + .001f), Is.True, label + ": no blow after death");
            Assert.That(run.handsHiddenAt, Is.GreaterThan(0f).And.LessThanOrEqualTo(ShortLimit), label + ": execution <= 1.2 s real");
            Assert.That(run.busyEndsAt, Is.GreaterThan(0f).And.LessThanOrEqualTo(.97f), label + ": actions free again after the impact beat");
            Assert.That(run.maxGripDepth, Is.LessThanOrEqualTo(MaxGripDepth), label + ": the hand never reaches past the arm envelope");
            Assert.That(run.maxWeaponGap, Is.LessThanOrEqualTo(.08f), label + ": the hand closes on the robot weapon at its mount while struggling");
            Assert.That(run.frozenAlive, Is.LessThanOrEqualTo(6), label + ": robot reacts (frozen frames while alive)");
            Assert.That(run.maxBoneStep, Is.LessThanOrEqualTo(25f), label + ": no pose jump while alive");
            Assert.That(run.maxCamDeg, Is.LessThanOrEqualTo(1.5f), label + ": camera never turns by itself > 1.5 deg/frame");
            Assert.That(run.minCameraGap, Is.GreaterThanOrEqualTo(.30f), label + ": camera stays outside the robot body");
            Assert.That(run.hitStops, Is.InRange(1, 8), label + ": hit-stops present and short");
            Assert.That(run.timeScaleOk, Is.True, label + ": time scale restored");
            Assert.That(run.m1Shots, Is.Zero, label + ": M1 ignored"); Assert.That(melee.AttackSerial, Is.EqualTo(run.serial0), label + ": F ignored");
            Assert.That(carrier.Held, Is.Not.Null, label + ": G ignored"); Assert.That(weapons.GetActiveWeapon(), Is.SameAs(carrier.Held.EquippedWeapon), label + ": 1 / 2 ignored, stolen weapon in hand");
            Assert.That(run.switchEvents, Is.EqualTo(1), label + ": one weapon switch");
            Assert.That(source.LastBeat, Is.EqualTo(ExecutionBeat.End), label + ": execution ended cleanly");
        }

        // Baseline for the F checks: does F start a melee in this lab before any execution?
        [UnityTest] public IEnumerator Baseline_FStartsMeleeWithoutExecution()
        {
            yield return new WaitForSeconds(.3f); var mi = player.GetComponent<MechanicInput>(); int s0 = melee.AttackSerial;
            Keys(Key.F); yield return null; log.Add($"F frame 0: f {keys.fKey.isPressed} MeleeHeld {mi.MeleeHeld} MeleePressed {mi.MeleePressed} canAct {mi.CanAct} busy {carrier.MeleeBusy}"); yield return null; Keys(); yield return null;
            var action = InputSystem.actions.FindAction("Player/Melee", false);
            log.Add($"serial {s0}->{melee.AttackSerial}; Melee action enabled {action?.enabled}, bindings [{(action == null ? "" : string.Join(", ", action.bindings.Select(x => x.effectivePath)))}], controls [{(action == null ? "" : string.Join(", ", action.controls.Select(c => c.path)))}]");
            Write("baseline-F");
            Assert.That(melee.AttackSerial, Is.EqualTo(s0 + 1), "F starts a melee in the lab before any execution");
        }

        [UnityTest] public IEnumerator T07_TwoBlowsTimingReachRobotReactionAndLocks() { yield return ShortCase("T07", T07); }
        [UnityTest] public IEnumerator T07Mech_TwoBlowsTimingReachRobotReactionAndLocks() { yield return ShortCase("T07Mech", T07Mech); }
        [UnityTest] public IEnumerator SawBot_TwoBlowsTimingReachSawContinuityAndLocks() { yield return ShortCase("SawBot", Saw); }

        IEnumerator ShortCase(string label, string prefab)
        {
            yield return Spawn(prefab, 1.2f, false);
            var saw = target.GetComponent<SawBotWeaponSource>(); bool sawVisibleBeforeRip = true;
            var run = new Run();
            yield return Execute(run, false, 1.6f, saw ? (System.Func<float, IEnumerator>)(c => { sawVisibleBeforeRip = !saw.WeaponHiddenByRip; return Nothing(); }) : null, .25f);
            Write(label);
            AssertShort(run, label);
            if (saw) Assert.That(sawVisibleBeforeRip, Is.True, label + ": the saw stays on the robot until the rip");
            AssertRobotLegal(label);
            yield return new WaitForSeconds(.3f);
            var mi = player.GetComponent<MechanicInput>();
            log.Add($"before F: f pressed {keys.fKey.isPressed}, meleeHeld {mi.MeleeHeld}, charging {melee.IsCharging}, attacking {melee.IsAttacking}, melee enabled {melee.enabled}, serial {melee.AttackSerial}");
            Keys(Key.F);
            for (int i = 0; i < 3; i++) { yield return null; log.Add($"F frame {i}: f pressed {keys.fKey.isPressed} wasPressed {keys.fKey.wasPressedThisFrame}, MeleePressed {mi.MeleePressed}, InteractPressed {mi.InteractPressed}, MeleeHeld {mi.MeleeHeld}, canAct {mi.CanAct}, meleeBusy {carrier.MeleeBusy}, charging {melee.IsCharging}, attacking {melee.IsAttacking}, serial {melee.AttackSerial}"); if (i == 1) Keys(); }
            Write(label);
            Assert.That(melee.AttackSerial, Is.EqualTo(run.serial0 + 1), $"{label}: F works once the execution is over (canAct {player.GetComponent<MechanicInput>().CanAct}, meleeBusy {carrier.MeleeBusy}, execBusy {carrier.ExecutionBusy}, disarming {hands.IsDisarming}, bigRunning {(bigSeq && bigSeq.IsRunning)}, attacking {melee.IsAttacking}, timeScale {Time.timeScale})");
        }
        static IEnumerator Nothing() { yield break; }

        [UnityTest] public IEnumerator T07_WallBehindRobot_PullAndShoveStopAtTheWall()
        {
            yield return Spawn(T07, 1.2f, false, wallBehind: true);
            var run = new Run(); yield return Execute(run, false, 1.6f); Write("T07-wall");
            Assert.That(run.deaths, Is.EqualTo(1)); AssertRobotLegal("T07 wall");
        }

        [UnityTest] public IEnumerator BigBot_RipAndShotWithinTwoSecondsNoForcedCameraTurn()
        {
            yield return Spawn(Big, 1.3f, true);
            var cannon = ExecutionReachResolver.WeaponViews(source).Where(r => r).Select(r => r.bounds).ToArray(); var cb = cannon.Length > 0 ? cannon[0] : new Bounds(); foreach (var b in cannon) cb.Encapsulate(b);
            foreach (var c in target.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger)) log.Add($"big collider {c.name} ({c.GetType().Name}) parent {(c.transform.parent ? c.transform.parent.name : "-")} min {c.bounds.min.ToString("F2")} max {c.bounds.max.ToString("F2")} weapon {ExecutionReachResolver.IsWeaponCollider(c, source.WeaponJoint, ExecutionReachResolver.WeaponViews(source))}");
            foreach (var r in target.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)))
            {
                var s = r as SkinnedMeshRenderer; var mesh = s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                log.Add($"big renderer {r.name} {r.GetType().Name} root {(s && s.rootBone ? s.rootBone.name : "-")} bones {(s ? s.bones.Length : 0)} mesh {(mesh ? mesh.name + " v" + mesh.vertexCount + " readable " + mesh.isReadable + " local " + mesh.bounds.size.ToString("F2") : "-")} world {r.bounds.size.ToString("F2")} cannon {cannon.Length > 0 && ExecutionReachResolver.WeaponViews(source).Contains(r)}");
            }
            log.Add($"big cannon views {cannon.Length} bounds min {cb.min.ToString("F2")} max {cb.max.ToString("F2")}");
            log.Add($"big bounds centre {bounds.center.ToString("F2")} size {bounds.size.ToString("F2")}, joint {(source.WeaponJoint ? source.WeaponJoint.position.ToString("F2") : "-")}, player {player.transform.position.ToString("F2")}, cam {player.PlayerCamera.transform.position.ToString("F2")}, fov {player.PlayerCamera.fieldOfView:F0}");
            var destruction = target.GetComponent<BigBotDestruction>();
            var run = new Run(); yield return Execute(run, true, 2.6f); log.Add($"perch {bigSeq.Perch.ToString("F2")} ({bigSeq.PerchInfo}) grab {bigSeq.GrabPoint.ToString("F2")} pincer {(source.WeaponJoint && source.WeaponJoint.Find("Pincer_R") ? source.WeaponJoint.Find("Pincer_R").position.ToString("F2") : "-")}"); Write("BigBot");
            Assert.That(run.deaths, Is.EqualTo(1), "one death"); Assert.That(run.deathAt, Is.EqualTo(BigShot).Within(Tolerance), "death with the shot");
            Assert.That(run.impacts.All(t => t <= run.deathAt + .001f), Is.True, "no blow after death");
            Assert.That(run.controlBackAt, Is.GreaterThan(0f).And.LessThanOrEqualTo(BigLimit), "control back <= 2.0 s");
            Assert.That(run.maxCamDeg, Is.LessThanOrEqualTo(1.5f), "no forced camera turn");
            // round 2: measured against the real armour (baked LOD0 triangles); its only collider is a box much larger than the silhouette
            Assert.That(run.minMeshGap, Is.GreaterThanOrEqualTo(.30f), "camera stays 0.30 m outside the Big Bot's visible body");
            Assert.That(run.minCannonGap, Is.GreaterThanOrEqualTo(.12f), "camera never inside the cannon it grabs");
            Assert.That(run.robotOutOfFrame, Is.LessThanOrEqualTo(3), "the Big Bot stays framed while grabbing and shooting");
            Assert.That(run.distanceAt12, Is.GreaterThanOrEqualTo(2.0f), "the player is away from the death burst at 1.2 s");
            Assert.That(run.maxGripDepth, Is.LessThanOrEqualTo(MaxGripDepth), "hand within the arm envelope");
            Assert.That(run.maxWeaponGap, Is.LessThanOrEqualTo(.08f), "the hand closes on the Big Bot cannon");
            // the hand reads the last rendered pose (one frame behind): a tug snap may open a gap for one frame, never more
            Assert.That(run.armGaps.Count, Is.GreaterThanOrEqualTo(5), "struggle frames sampled");
            Assert.That(run.ArmFramesOff, Is.LessThanOrEqualTo(1), "the hand closes on the cannon armour itself (real triangles, <= 0.08 m), not on its oversized skinned bounds");
            Assert.That(run.maxArmGap, Is.GreaterThan(0f).And.LessThanOrEqualTo(.20f), "no frame with the hand off the cannon by more than a one-frame tug lag");
            Assert.That(run.timeScaleOk, Is.True);
            Assert.That(run.m1Shots, Is.Zero, "M1 ignored"); Assert.That(melee.AttackSerial, Is.EqualTo(run.serial0), "F ignored");
            Assert.That(carrier.Held, Is.Not.Null, "G ignored");
            Assert.That(run.switchEvents, Is.EqualTo(1), "one (deferred) weapon switch");
            Assert.That(run.shotWithStolen, Is.True, "round 3: the stolen cannon is the active, visible weapon when the shot fires");
            Assert.That(run.maxProxySize, Is.LessThanOrEqualTo(.5f), "round 3: within 0.6 m of the lens the torn cannon is hand-sized (<= 0.5 m), not a 2.7 m slab");
            // the stolen module the player holds is itself a collectible cannon (BigBotCannonDebris on a StolenCannon): not loose
            var debris = Object.FindObjectsByType<BigBotCannonDebris>(FindObjectsInactive.Exclude);
            int looseCannons = debris.Count(d => !(carrier.Held && d.GetComponentInParent<StolenCannon>() == carrier.Held));
            log.Add("cannon debris: " + string.Join(", ", debris.Select(d => d.name + (carrier.Held && d.GetComponentInParent<StolenCannon>() == carrier.Held ? " (held)" : " (loose)"))));
            log.Add($"after the death: fallen cannon {(destruction && destruction.FallenCannon ? destruction.FallenCannon.name : "none")}, active cannon debris {looseCannons}"); Write("BigBot");
            Assert.That(destruction && destruction.FallenCannon, Is.False, "round 3: the dead Big Bot does not throw a second cannon (the player holds it)");
            Assert.That(looseCannons, Is.Zero, "round 3: no loose cannon debris after the execution");
            Assert.That(run.proxyAtShot, Is.False, "round 3: the tossed proxy is gone (not parked at the lens) at the shot");
            var body = player.GetComponent<CharacterController>();
            var overlap = Physics.OverlapCapsule(body.transform.position + Vector3.up * body.radius, body.transform.position + Vector3.up * (body.height - body.radius), body.radius * .95f, -1, QueryTriggerInteraction.Ignore)
                .Where(c => target && c.transform.IsChildOf(target.transform)).ToArray();
            Assert.That(overlap, Is.Empty, "player not inside the Big Bot after the jump back");
            AssertRobotLegal("BigBot");
        }

        [UnityTest] public IEnumerator T07_PlayerDiesMidExecution_NoResidue()
        {
            yield return Spawn(T07, 1.2f, false);
            var run = new Run();
            yield return Execute(run, false, 1.4f, c => KillPlayer(), .45f); Write("T07-player-death");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "time scale restored");
            Assert.That(hands.IsDisarming, Is.False, "hands released");
            Assert.That(carrier.ExecutionBusy, Is.False, "no pending execution");
            Assert.That(movement && movement.IsScripted, Is.False, "no scripted move left");
            Assert.That(run.deaths, Is.LessThanOrEqualTo(1), "robot dies at most once");
            Assert.That(source.LastBeat == ExecutionBeat.Cancel || source.LastBeat == ExecutionBeat.End, Is.True, "robot told the execution is over");
            Assert.That(LoopingAudioOn(target), Is.Empty, "no execution loop left playing on the robot");
        }
        IEnumerator KillPlayer() { playerHealth.Invincible = false; playerHealth.Kill(); yield return null; }

        [UnityTest] public IEnumerator BigBot_PlayerDiesMidSequence_NoResidue()
        {
            yield return Spawn(Big, 1.3f, true);
            var run = new Run();
            yield return Execute(run, true, 1.6f, c => KillPlayer(), .7f); Write("BigBot-player-death");
            Assert.That(Time.timeScale, Is.EqualTo(1f)); Assert.That(bigSeq.IsRunning, Is.False); Assert.That(movement.IsScripted, Is.False);
            Assert.That(run.deaths, Is.LessThanOrEqualTo(1));
            Assert.That(LoopingAudioOn(target), Is.Empty);
        }

        [UnityTest] public IEnumerator T07_PauseMidExecution_ResumesWithoutResidue()
        {
            yield return Spawn(T07, 1.2f, false);
            var run = new Run();
            yield return Execute(run, false, 1.8f, c => Pause(), .5f); Write("T07-pause");
            Assert.That(run.impacts.Count, Is.EqualTo(2)); Assert.That(run.deaths, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.EqualTo(1f)); Assert.That(hands.IsDisarming, Is.False); Assert.That(source.LastBeat, Is.EqualTo(ExecutionBeat.End));
        }
        IEnumerator Pause() { float saved = Time.timeScale; Time.timeScale = 0; for (int i = 0; i < 20; i++) yield return null; Time.timeScale = saved > 0 ? saved : 1; }

        static string[] LoopingAudioOn(GameObject robot) => robot ? robot.GetComponentsInChildren<AudioSource>().Where(a => a.isPlaying && a.loop && a.clip && (a.clip.name.Contains("Creak") || a.clip.name.Contains("Motor") || a.clip.name.Contains("Hydraulic"))).Select(a => a.clip.name).ToArray() : new string[0];
    }
}
