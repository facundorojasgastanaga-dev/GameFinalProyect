using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace IronRequiem.Tests
{
    public sealed class CannonChargeBehaviorTests
    {
        const string Reports = "Tools/IronRequiem/Reports/CannonCharge";
        PlayerCharacterController player;
        PlayerWeaponsManager weapons;
        StolenWeaponCarrier carrier;
        StolenCannon module;
        WeaponController weapon;
        CannonChargeShot charge;
        CannonChargePresentation feedback;
        Keyboard keyboard;
        Mouse mouse;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        readonly List<string> measurements = new List<string>();

        [UnitySetUp] public IEnumerator Setup()
        {
            measurements.Clear(); Time.timeScale = 1; Application.runInBackground = true;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/T07OptimizationLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keyboard.MakeCurrent(); mouse.MakeCurrent();
            previousDevices = InputSystem.actions.devices; InputSystem.actions.devices = new InputDevice[] { keyboard, mouse };
            yield return null; yield return null;
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); weapons = player.GetComponent<PlayerWeaponsManager>(); carrier = player.GetComponent<StolenWeaponCarrier>();
            player.GetComponent<Health>().Invincible = true;
            var enemy = Object.FindAnyObjectByType<EnemyController>(); enemy.GetComponent<EnemyMobile>().enabled = false; enemy.NavMeshAgent.ResetPath();
#if UNITY_EDITOR
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
#endif
            Cursor.lockState = CursorLockMode.Locked; yield return new WaitForSeconds(.5f);
            enemy.GetComponentInChildren<DetachableCannon>().Detach(); module = Object.FindAnyObjectByType<StolenCannon>();
            module.Body.isKinematic = true;
            var target = player.PlayerCamera.transform.position + player.PlayerCamera.transform.forward * 1.5f;
            module.transform.position = target; Physics.SyncTransforms(); module.transform.position += target - module.SolidCollider.bounds.center; Physics.SyncTransforms();
            yield return new WaitForSeconds(.4f); Assert.That(carrier.Candidate, Is.EqualTo(module));
            Cursor.lockState = CursorLockMode.Locked;
            Assert.That(player.GetComponent<MechanicInput>().CanAct, Is.True, "Native input gate before pickup");
            yield return Tap(Key.E); Assert.That(carrier.Held, Is.EqualTo(module), "E must actually pick up the module");
            yield return new WaitForSeconds(2.1f); Cursor.lockState = CursorLockMode.Locked;
            Assert.That(carrier.CanUseHeldWeapon, Is.True, "Held=" + carrier.Held + " active=" + weapons.GetActiveWeapon() + " cursor=" + Cursor.lockState);
            weapon = module.EquippedWeapon; charge = weapon.GetComponent<CannonChargeShot>(); feedback = weapon.GetComponent<CannonChargePresentation>();
            Assert.That(feedback, Is.Not.Null);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("CannonChargeCleanup_" + Time.frameCount));
            if (old.IsValid() && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
            InputSystem.actions.devices = previousDevices;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
            Cursor.lockState = CursorLockMode.None; yield return null;
        }
        IEnumerator Tap(Key key) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; }
        void Mouse(bool down, bool aim = false, Vector2 delta = default)
        {
            var state = new MouseState { delta = delta }; if (down) state = state.WithButton(MouseButton.Left); if (aim) state = state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse, state);
        }
        void CheckAlignment()
        {
            var visual = weapon.GetComponentsInChildren<Transform>(true).Single(t => t.name == "StolenCannonVisual");
            // Independently measured rim centre in the original imported mesh, not a bounds centre.
            Vector3 mouth = visual.TransformPoint(new Vector3(.00009319516f, -.002265465f, -.005319517f));
            float error = Vector3.Distance(mouth, weapon.WeaponMuzzle.position);
            Assert.That(error, Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(feedback.EffectOrigin.position, mouth), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(feedback.EnergyParticles.transform.position, mouth), Is.LessThan(.0001f));
            Assert.That(Vector3.Dot(visual.TransformDirection(new Vector3(.07086702f, -.5761503f, -.8142658f).normalized), weapon.WeaponMuzzle.forward), Is.GreaterThan(.9999f));
            Assert.That(Vector3.Dot(weapon.WeaponMuzzle.forward, player.PlayerCamera.transform.forward), Is.GreaterThan(.999f));
            // Conservative local bounds proof, without enabling Read/Write on production meshes.
            var bounds = visual.GetComponent<MeshFilter>().sharedMesh.bounds; float closest = float.PositiveInfinity;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                closest = Mathf.Min(closest, weapons.WeaponCamera.transform.InverseTransformPoint(visual.TransformPoint(point)).z);
            }
            Assert.That(closest, Is.GreaterThan(weapons.WeaponCamera.nearClipPlane + .005f), "Equipped geometry clips the weapon camera");
            var coreViewport = weapons.WeaponCamera.WorldToViewportPoint(feedback.EnergyCore.TransformPoint(Vector3.forward));
            Assert.That(coreViewport.x, Is.InRange(.05f, .95f), "Charge core leaves horizontal framing");
            Assert.That(coreViewport.y, Is.InRange(.15f, .95f), "Charge core leaves vertical framing while aiming");
            measurements.Add("frame=" + Time.frameCount + " rim_error_m=" + error.ToString("G9") + " closest_camera_z=" + closest.ToString("G9") + " fraction=" + charge.ChargeFraction);
        }
        void CheckSilent()
        {
            if (!feedback) return; // A depleted weapon may already have been destroyed by its owner.
            Assert.That(feedback.IsVisible, Is.False);
            foreach (var ps in feedback.EffectOrigin.GetComponentsInChildren<ParticleSystem>(true)) { Assert.That(ps.particleCount, Is.Zero); Assert.That(ps.isEmitting, Is.False); }
            Assert.That(feedback.EffectOrigin.GetComponentsInChildren<Light>(true).Any(l => l.enabled && l.gameObject.activeInHierarchy), Is.False);
            Assert.That(feedback.EffectOrigin.GetComponentsInChildren<AudioSource>(true).Any(a => a.isPlaying), Is.False);
        }
        void Capture(string name)
        {
            var camera = player.PlayerCamera; var target = new RenderTexture(1280, 720, 24); var oldTarget = camera.targetTexture;
            camera.targetTexture = target; camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            Directory.CreateDirectory(Reports); File.WriteAllBytes(Reports + "/" + name + ".png", texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = oldTarget; target.Release(); Object.Destroy(texture); Object.Destroy(target);
        }
        [UnityTest] public IEnumerator RimChargeAndAimRemainAlignedThroughMotionAndAds()
        {
            Capture("equipped-idle"); CheckAlignment(); Mouse(true); yield return new WaitForSeconds(.3f); CheckAlignment(); Capture("charge-short");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
            for (int i = 0; i < 20; i++) { Mouse(true, false, new Vector2(2, -.5f)); yield return null; CheckAlignment(); }
            Mouse(true, true); yield return new WaitForSeconds(1.1f); CheckAlignment(); Capture("charge-full-aim");
            Mouse(true); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSeconds(.4f); CheckAlignment();
            for (int i = 0; i < 6; i++) { Capture("charge-full-" + i); yield return new WaitForSeconds(.065f); }
            Assert.That(charge.ChargeFraction, Is.EqualTo(1)); Assert.That(feedback.EnergyParticles.particleCount, Is.InRange(1, 32));
            charge.CancelPendingShot(); yield return null; CheckSilent();
            File.WriteAllLines(Reports + "/alignment-measurements.txt", measurements);
        }
        [UnityTest] public IEnumerator ConsecutiveShotsStartAtMouthWhileMovingAndIgnorePlayer()
        {
            player.GetComponent<Health>().Invincible = false; float before = player.GetComponent<Health>().CurrentHealth; int hits = 0, shots = 0;
            player.GetComponent<Health>().OnDamaged += (damage, source) => hits++;
            weapon.OnShootProcessed += () =>
            {
                shots++; var p = Object.FindObjectsByType<ProjectileBase>().Where(x => x.Owner == player.gameObject).OrderBy(x => Vector3.Distance(x.InitialPosition, weapon.WeaponMuzzle.position)).First();
                float spawnError = Vector3.Distance(p.InitialPosition, weapon.WeaponMuzzle.position), visualError = Vector3.Distance(p.transform.position, weapon.WeaponMuzzle.position);
                measurements.Add("shot=" + shots + " initial_error=" + spawnError + " visual_error=" + visualError + " muzzle_velocity=" + weapon.MuzzleWorldVelocity);
                Assert.That(spawnError, Is.LessThan(.0001f)); Assert.That(visualError, Is.LessThan(.0001f)); Assert.That(Vector3.Dot(p.InitialDirection, weapon.WeaponMuzzle.forward), Is.GreaterThan(.999f));
            };
            Mouse(true); yield return new WaitForSeconds(.07f); Mouse(false); yield return null; Assert.That(shots, Is.Zero); CheckSilent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            for (int i = 0; i < 2; i++)
            {
                Mouse(true); yield return new WaitForSeconds(i == 0 ? .4f : 1.3f); Mouse(false); yield return null; yield return null;
                if (i == 0)
                {
                    Capture("charge-release"); Mouse(true);
                    for (int sample = 0; sample < 12; sample++)
                    {
                        yield return null;
                        var ps = feedback.EnergyParticles;
                        measurements.Add("overlap frame=" + Time.frameCount + " charge=" + charge.IsCharging + " visible=" + feedback.IsVisible + " playing=" + ps.isPlaying + " emitting=" + ps.isEmitting + " paused=" + ps.isPaused + " stopped=" + ps.isStopped + " time=" + ps.time + " count=" + ps.particleCount + " delay=" + ps.main.startDelay.constant + " culling=" + ps.main.cullingMode + " emissionEnabled=" + ps.emission.enabled);
                    }
                    File.WriteAllLines(Reports + "/overlap-measurements.txt", measurements);
                    Assert.That(charge.IsCharging, Is.True); Assert.That(feedback.EnergyParticles.isEmitting, Is.True, "A new press during the release tail restarts the charge emitter");
                    charge.CancelPendingShot(); Mouse(false); yield return null;
                }
                yield return new WaitForSeconds(.15f); CheckSilent(); yield return new WaitForSeconds(.4f);
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(shots, Is.EqualTo(2)); Assert.That(charge.LastShotPower, Is.EqualTo(1)); Assert.That(hits, Is.Zero); Assert.That(player.GetComponent<Health>().CurrentHealth, Is.EqualTo(before));
            Assert.That(module.Ammo.Remaining, Is.InRange(50, 58)); File.WriteAllLines(Reports + "/shot-measurements.txt", measurements);
        }
        [UnityTest] public IEnumerator SwitchingCancellingAndExhaustionLeaveNoChargeEffects()
        {
            Mouse(true); yield return new WaitForSeconds(.4f); Assert.That(feedback.IsVisible, Is.True); yield return Tap(Key.Digit1); CheckSilent();
            Mouse(false); yield return new WaitForSeconds(2.1f); yield return Tap(Key.Digit2); yield return new WaitForSeconds(2.1f); CheckSilent();
            Mouse(true); yield return new WaitForSeconds(.4f); Time.timeScale = 0; yield return null; CheckSilent(); Mouse(false); Time.timeScale = 1; yield return null;
            Mouse(true); yield return new WaitForSeconds(.4f); charge.CancelPendingShot(); yield return null; CheckSilent();
            Mouse(false); yield return null; Mouse(true); yield return new WaitForSeconds(.4f); module.Ammo.TryConsume(90); yield return null; yield return null;
            Assert.That(charge.IsCharging, Is.False); CheckSilent(); Assert.That(module.Overdrive.IsArmed, Is.True); Assert.That(module.Overdrive.Smoke.isEmitting, Is.True);
            bool detonated = false; module.Explosion.Detonated += () => detonated = true;
            module.Ammo.TryConsume(module.Ammo.Remaining); yield return null; yield return null; CheckSilent(); Assert.That(detonated, Is.True);
        }
    }
}
