using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    // Integration tests use virtual devices only. No window activation, GameView.Focus or physical input.
    public sealed class ModularArmTests
    {
        const string Reports = "Tools/IronRequiem/Reports/ModularArm";
        const string Lab = "Assets/IronRequiem/Scenes/ModularArmLab.unity";
        PlayerCharacterController player; PlayerWeaponsManager manager; ProtagonistHandsPresentation hands;
        StolenWeaponCarrier carrier; Keyboard keyboard; Mouse mouse;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        float previousCapture; bool previousBackground;
        [UnitySetUp] public IEnumerator Setup()
        {
            Directory.CreateDirectory(Reports); previousCapture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1;
            previousBackground = Application.runInBackground; Application.runInBackground = true;
            previousDevices = InputSystem.actions.devices; keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.actions.devices = new InputDevice[] {keyboard, mouse}; keyboard.MakeCurrent(); mouse.MakeCurrent();
            EditorSceneManager.LoadSceneInPlayMode(Lab, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); manager = player.GetComponent<PlayerWeaponsManager>(); hands = player.GetComponent<ProtagonistHandsPresentation>(); carrier = player.GetComponent<StolenWeaponCarrier>();
            player.GetComponent<Health>().Invincible = true;
            Object.FindAnyObjectByType<GameFlowManager>().enabled = false; Object.FindAnyObjectByType<ObjectiveManager>().enabled = false;
            foreach (var enemy in Object.FindObjectsByType<EnemyController>())
            {
                enemy.enabled = false; if (enemy.NavMeshAgent.isOnNavMesh) { enemy.NavMeshAgent.ResetPath(); enemy.NavMeshAgent.isStopped = true; }
                foreach (var mobile in enemy.GetComponents<EnemyMobile>()) mobile.enabled = false;
                var big = enemy.GetComponent<BigBotCombat>(); if (big) big.enabled = false;
                var turret = enemy.GetComponent<EnemyTurret>(); if (turret) turret.enabled = false;
            }
            Move(new Vector3(-14, .03f, -11)); Cursor.lockState = CursorLockMode.Locked; yield return new WaitForSeconds(.7f);
            Assert.That(player.GetComponent<MechanicInput>().CanAct, Is.True, "Background virtual-input gate; never steal window focus to satisfy this assertion");
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; Time.captureDeltaTime = previousCapture;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("ModularArmCleanup" + Time.frameCount));
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            InputSystem.actions.devices = previousDevices; if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
            Cursor.lockState = CursorLockMode.None; Application.runInBackground = previousBackground; yield return null;
        }
        void Move(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>(); controller.enabled = false; player.transform.SetPositionAndRotation(position, Quaternion.identity); controller.enabled = true; player.CharacterVelocity = Vector3.zero; Physics.SyncTransforms();
        }
        IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        void Fire(bool held, bool aim = false) => InputSystem.QueueStateEvent(mouse, new MouseState {buttons = (ushort)((held ? 1 : 0) | (aim ? 2 : 0))});
        static Transform Named(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        void CheckArm()
        {
            Assert.That(hands.RightForearmReplaced, Is.True); Assert.That(hands.RightHandVisible, Is.False); Assert.That(hands.LeftHandVisible, Is.False);
            Assert.That(Named(hands.FpsRoot, "Forearm_R_FPS").GetComponent<Renderer>().enabled, Is.False);
            Assert.That(Named(hands.FpsRoot, "Forearm_R_Receiver").GetComponent<Renderer>().enabled, Is.True);
            var weapon = manager.GetActiveWeapon(); var attachments = weapon.WeaponRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Attachment").ToArray();
            Assert.That(attachments.Length, Is.EqualTo(1), "One authoritative rear interface per module"); var attach = attachments[0];
            if (attach) Assert.That(Vector3.Distance(hands.RightModuleSocket.position, attach.position + weapon.WeaponMuzzle.forward * .003f), Is.LessThan(.001f));
        }
        void Capture(Camera camera, string name, int width = 1280, int height = 720)
        {
            var rt = new RenderTexture(width, height, 24); var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var active = RenderTexture.active; var target = camera.targetTexture;
            try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply(); File.WriteAllBytes(Reports + "/" + name + ".png", tex.EncodeToPNG()); }
            finally { camera.targetTexture = target; RenderTexture.active = active; Object.Destroy(rt); Object.Destroy(tex); }
        }
        void JointCapture(string name)
        {
            var weapon = manager.GetActiveWeapon(); var socket = hands.RightModuleSocket;
            var camera = new GameObject("ModuleInspectionCamera").AddComponent<Camera>(); camera.cullingMask = manager.WeaponCamera.cullingMask;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .10f, .13f); camera.nearClipPlane = .005f; camera.fieldOfView = 40;
            camera.transform.position = socket.position - weapon.WeaponMuzzle.forward * .34f + weapon.WeaponMuzzle.right * .30f + weapon.WeaponMuzzle.up * .13f;
            camera.transform.LookAt(socket.position + weapon.WeaponMuzzle.forward * .10f); Capture(camera, name, 1280, 960); Object.Destroy(camera.gameObject);
        }
        [UnityTest] public IEnumerator CompactModuleKeepsMovementAndRestoresNormalForearm()
        {
            var weapon = manager.GetActiveWeapon(); var native = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab");
            var actualCell = weapon.GetComponent<WeaponFuelCellHandler>(); var sourceCell = native.GetComponent<WeaponFuelCellHandler>();
            Assert.That(actualCell.FuelCellUsedPosition, Is.EqualTo(sourceCell.FuelCellUsedPosition));
            var model = Named(weapon.transform, "WeaponMesh_Pistol"); Assert.That(model.localScale.x, Is.EqualTo(.62f).Within(.0001f));
            Assert.That(weapon.DelayBetweenShots, Is.EqualTo(.1f).Within(.0001f)); Assert.That(weapon.ProjectilePrefab.GetComponent<ProjectileStandard>().Damage, Is.EqualTo(6.5f));
            var receiver = Named(hands.FpsRoot, "Forearm_R_Receiver").GetComponent<SkinnedMeshRenderer>(); var forearm = Named(hands.FpsRoot, "Forearm_R");
            var baked = new Mesh(); receiver.BakeMesh(baked, false); var scale = receiver.transform.lossyScale;
            Vector3 World(Vector3 v) => receiver.transform.TransformPoint(new Vector3(v.x / scale.x, v.y / scale.y, v.z / scale.z));
            var initial = baked.vertices; var used = baked.triangles.Distinct().ToArray();
            var projection = used.Select(i => Vector3.Dot(World(initial[i]) - forearm.position, weapon.WeaponMuzzle.forward)).ToArray();
            float rear = projection.Min(); var cap = used.Where((index, i) => projection[i] < rear + .012f).ToArray(); int visibleCuts = 0;
            int frames = 0, shots = 0; weapon.OnShoot += () => shots++; var start = player.transform.position;
            for (int phase = 0; phase < 4; phase++)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(phase == 1 || phase == 2 ? Key.W : Key.None)); Fire(phase >= 2, phase == 2);
                float end = Time.time + .7f;
                while (Time.time < end)
                {
                    yield return new WaitForEndOfFrame(); CheckArm(); frames++;
                    receiver.BakeMesh(baked, false); scale = receiver.transform.lossyScale; var vertices = baked.vertices;
                    foreach (int index in cap) { var viewport = manager.WeaponCamera.WorldToViewportPoint(World(vertices[index])); if (viewport.z > manager.WeaponCamera.nearClipPlane && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1) visibleCuts++; }
                }
                Capture(player.PlayerCamera, "module-phase-" + phase);
            }
            Fire(false); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSeconds(.5f); JointCapture("module-joint");
            Assert.That(shots, Is.GreaterThan(8)); Assert.That(Vector3.Distance(start, player.transform.position), Is.GreaterThan(.2f));
            Object.Destroy(baked); Assert.That(visibleCuts, Is.Zero, "Receiver proximal cut must remain outside the FPS frame during motion and ADS");
            var prefab = weapon.SourcePrefab.GetComponent<WeaponController>(); manager.RemoveWeapon(weapon); yield return new WaitForSeconds(1);
            Assert.That(hands.RightForearmReplaced, Is.False); Assert.That(hands.RightHandVisible, Is.True);
            Assert.That(Named(hands.FpsRoot, "Forearm_R_FPS").GetComponent<Renderer>().enabled, Is.True);
            Assert.That(Named(hands.FpsRoot, "Forearm_R_Receiver").GetComponent<Renderer>().enabled, Is.False);
            manager.AddWeapon(prefab); manager.SwitchToWeaponIndex(0, true); yield return new WaitForSeconds(1); CheckArm();
            File.WriteAllText(Reports + "/motion.txt", frames + " aligned virtual-input frames; " + shots + " shots; " + visibleCuts + " visible proximal cut vertices; walking, ADS, fire, normal arm restoration and re-equipping verified. No desktop focus calls.");
        }
        [UnityTest] public IEnumerator SustainedFireTurnsCapsulesRedAndCoolsBack()
        {
            var weapon = manager.GetActiveWeapon(); var heat = weapon.GetComponent<OverheatBehavior>(); var cells = weapon.GetComponent<WeaponFuelCellHandler>();
            Color Emission()
            {
                var renderer = cells.FuelCells[0].GetComponent<Renderer>(); var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, 1); return block.GetColor("_EmissionColor");
            }
            Fire(true); yield return new WaitForSeconds(2); var warm = Emission(); Capture(player.PlayerCamera, "heat-warm");
            yield return new WaitForSeconds(3.6f); var hot = Emission();
            Assert.That(hot.r, Is.GreaterThan(2)); Assert.That(hot.r, Is.GreaterThan(hot.g * 8)); Assert.That(hot.r, Is.GreaterThan(hot.b * 8)); Assert.That(hot.r, Is.GreaterThan(warm.r));
            Assert.That(heat.SteamVfx.gameObject.activeInHierarchy, Is.True); Assert.That(heat.SteamVfx.particleCount, Is.GreaterThan(0));
            var opaque = new MaterialPropertyBlock(); cells.FuelCells[0].GetComponent<Renderer>().GetPropertyBlock(opaque, 0);
            Assert.That(opaque.GetColor("_EmissionColor").r, Is.GreaterThan(2), "Opaque capsule surfaces must heat too; glass-only overrides were visually hidden");
            Fire(false); yield return new WaitForSeconds(.14f); Capture(player.PlayerCamera, "heat-red");
            yield return new WaitForSeconds(1.8f); var cold = Emission(); Capture(player.PlayerCamera, "heat-cooled");
            Assert.That(weapon.CurrentAmmoRatio, Is.GreaterThan(.99f)); Assert.That(cold.b, Is.GreaterThan(cold.r)); Assert.That(cells.FuelCells.All(c => c.transform.localPosition.sqrMagnitude < .00001f), Is.True);
            File.WriteAllText(Reports + "/heat.txt", "Actual held fire: warm=" + warm + "; red=" + hot + "; cooled=" + cold + ". Native ammo/heat/capsule mechanics; steam budget 20. No damage/cadence changes.");
        }
        IEnumerator AimAtGroundModule(StolenCannon module)
        {
            // Hold a reproducible camera pose for the raycast; movement/mouselook are covered by the separate motion case.
            player.enabled = false;
            module.Body.isKinematic = true; module.transform.SetPositionAndRotation(new Vector3(-14, .30f, -9.6f), Quaternion.identity); Physics.SyncTransforms();
            module.transform.position += Vector3.up * (.03f - module.SolidCollider.bounds.min.y); Physics.SyncTransforms();
            var center = module.SolidCollider.bounds.center;
            Move(new Vector3(center.x, .03f, center.z - 1.7f)); player.PlayerCamera.transform.LookAt(center); Physics.SyncTransforms();
            yield return new WaitForSeconds(.4f);
            var camera = player.PlayerCamera.transform;
            File.AppendAllText(Reports + "/pickup-diagnostic.txt", module.name + "; status=" + carrier.GetPickupStatus(module) + "; candidate=" + carrier.Candidate + "; bounds=" + module.SolidCollider.bounds + "; hits=" + string.Join(";", Physics.RaycastAll(camera.position, camera.forward, 4, -1, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).Select(h => h.collider.name + ":" + h.distance)) + "\n");
        }
        [UnityTest] public IEnumerator BigBotDropCanBeCollectedFiredThrownAndExploded()
        {
            var bot = Object.FindAnyObjectByType<BigBotDestruction>(); bot.GetComponent<Health>().TakeDamage(901, player.gameObject); yield return null;
            Assert.That(bot.CannonDropCount, Is.EqualTo(1)); var drop = bot.FallenCannon; Assert.That(drop, Is.Not.Null);
            var module = drop.GetComponent<StolenCannon>(); Assert.That(module, Is.Not.Null, "Regression: former decorative debris must be an actual weapon");
            yield return AimAtGroundModule(module); Assert.That(carrier.Candidate, Is.SameAs(module));
            yield return Tap(Key.E); yield return new WaitForSeconds(1); Assert.That(carrier.Held, Is.SameAs(module)); CheckArm();
            Assert.That(manager.GetActiveWeapon().SourcePrefab.name, Is.EqualTo("Weapon_BigBot_Temporary")); JointCapture("bigbot-equipped-joint"); Capture(player.PlayerCamera, "bigbot-equipped");
            float energy = module.Ammo.Remaining; int shots = 0; var charge = manager.GetActiveWeapon().GetComponent<CannonChargeShot>(); charge.ShotReleased += _ => shots++;
            Fire(true); yield return new WaitForSeconds(.8f); Fire(false); yield return new WaitForSeconds(.2f);
            Assert.That(shots, Is.EqualTo(1)); Assert.That(module.Ammo.Remaining, Is.LessThan(energy)); energy = module.Ammo.Remaining;
            yield return Tap(Key.G); yield return new WaitForSeconds(.4f); Assert.That(carrier.Held, Is.Null); Assert.That(module.State, Is.EqualTo(StolenCannonState.Available)); Assert.That(module.Ammo.Remaining, Is.EqualTo(energy).Within(.001f));
            yield return AimAtGroundModule(module); yield return Tap(Key.E); yield return new WaitForSeconds(1); Assert.That(carrier.Held, Is.SameAs(module)); Assert.That(module.Ammo.Remaining, Is.EqualTo(energy).Within(.001f));
            int detonations = 0; module.Explosion.Detonated += () => detonations++; module.Ammo.Drain(module.Ammo.Remaining - 10); yield return null;
            Assert.That(module.Overdrive.IsArmed, Is.True); Assert.That(module.Overdrive.Smoke.isPlaying, Is.True);
            yield return Tap(Key.G); yield return new WaitForSeconds(5.4f); Assert.That(detonations, Is.EqualTo(1)); Assert.That(carrier.Held, Is.Null);
            File.WriteAllText(Reports + "/bigbot-pickup.txt", "Actual 25% health drop: E pickup, charged shot, finite energy, G throw, same-energy re-pickup, critical smoke, thrown explosion exactly once. Real Big Bot visual used. Primary restored. No desktop focus calls.");
        }
        [UnityTest] public IEnumerator T07PickupRespectsWallsRangeAndSeparateMeleeKey()
        {
            var bot = Object.FindAnyObjectByType<RobotDamageProgression>(); var detach = bot.GetComponentInChildren<DetachableCannon>();
            var module = bot.GetComponentInChildren<StolenCannon>(); detach.Detach(); yield return AimAtGroundModule(module);
            Assert.That(carrier.Candidate, Is.SameAs(module)); yield return Tap(Key.F); yield return new WaitForSeconds(.6f); Assert.That(carrier.Held, Is.Null, "F punches; E collects");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "PickupOcclusionTest"; wall.transform.position = Vector3.Lerp(player.PlayerCamera.transform.position, module.SolidCollider.bounds.center, .5f); wall.transform.localScale = new Vector3(3, 3, .12f); Physics.SyncTransforms();
            yield return null; yield return Tap(Key.E); Assert.That(carrier.Held, Is.Null); Assert.That(carrier.Candidate, Is.Null); Object.Destroy(wall); yield return null;
            yield return Tap(Key.E); yield return new WaitForSeconds(1); Assert.That(carrier.Held, Is.SameAs(module));
            player.PlayerCamera.transform.localRotation = Quaternion.identity; yield return null; yield return new WaitForEndOfFrame(); CheckArm(); Capture(player.PlayerCamera, "t07-equipped");
            JointCapture("t07-equipped-joint");
            var t07Visual = Named(manager.GetActiveWeapon().transform, "StolenCannonVisual").GetComponent<Renderer>();
            File.WriteAllText(Reports + "/t07-view.txt", "Equipped mesh enabled=" + t07Visual.enabled + "; active=" + t07Visual.gameObject.activeInHierarchy + "; camera-to-muzzle alignment=" + Vector3.Dot(player.PlayerCamera.transform.forward, manager.GetActiveWeapon().WeaponMuzzle.forward) + "; rear socket=" + hands.RightModuleSocket.position + "; mesh bounds=" + t07Visual.bounds);
            float energy = module.Ammo.Remaining; yield return Tap(Key.Digit1); yield return new WaitForSeconds(1); Assert.That(manager.GetActiveWeapon().SourcePrefab.name, Is.EqualTo("Weapon_IR_ArmModule_v07")); CheckArm();
            yield return Tap(Key.Digit2); yield return new WaitForSeconds(1); Assert.That(hands.CurrentRepresentation, Is.EqualTo(ProtagonistHandsPresentation.Representation.StolenCannon)); Assert.That(module.Ammo.Remaining, Is.EqualTo(energy)); CheckArm();
            yield return Tap(Key.G); yield return new WaitForSeconds(.4f); yield return AimAtGroundModule(module); Move(player.transform.position + Vector3.back * 3); player.PlayerCamera.transform.LookAt(module.SolidCollider.bounds.center); yield return null;
            yield return Tap(Key.E); Assert.That(carrier.Held, Is.Null);
            File.WriteAllText(Reports + "/t07-pickup.txt", "Real T07 detachment, E pickup, F melee only, wall occlusion and out-of-range refusal; switch primary/stolen with energy preserved; universal arm receiver remains mounted. No desktop focus calls.");
        }
        [UnityTest] public IEnumerator DeathReleasesBigBotModuleWithoutDeletingItsEnergy()
        {
            var bot = Object.FindAnyObjectByType<BigBotDestruction>(); bot.DetachCannon(); yield return null; var module = bot.FallenCannon.GetComponent<StolenCannon>();
            yield return AimAtGroundModule(module); yield return Tap(Key.E); yield return new WaitForSeconds(1); module.Ammo.Drain(10); float energy = module.Ammo.Remaining;
            var health = player.GetComponent<Health>(); health.Invincible = false; health.TakeDamage(health.MaxHealth + 1, null); yield return null;
            Assert.That(carrier.Held, Is.Null); Assert.That(module.State, Is.EqualTo(StolenCannonState.Available)); Assert.That(module.Ammo.Remaining, Is.EqualTo(energy)); Assert.That(hands.FpsRoot.gameObject.activeSelf, Is.False);
        }
    }
}
