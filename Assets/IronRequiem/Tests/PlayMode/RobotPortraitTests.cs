using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
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
using Pose = IronRequiem.RobotPortraitSprites.Pose;
using Reaction = IronRequiem.RobotPortraitGraphic.Reaction;

namespace IronRequiem.Tests
{
    public sealed class RobotPortraitTests
    {
        const string Reports = "Tools/IronRequiem/Reports/RobotPortraitV03";
        RobotPortraitGraphic portrait;
        RobotPortraitSprites sprites;
        Health health;
        PlayerCharacterController player;
        PlayerWeaponsManager inventory;
        StolenWeaponCarrier carrier;
        ProtagonistMelee melee;
        Camera renderCamera;
        Keyboard keyboard; Mouse mouse;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
        bool background; float captureDelta;

        [UnitySetUp] public IEnumerator Setup()
        {
            background = Application.runInBackground; captureDelta = Time.captureDeltaTime;
            Application.runInBackground = true; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1;
            devices = InputSystem.actions.devices;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.actions.devices = new InputDevice[] { keyboard, mouse };
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/RobotPortraitV03Lab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            portrait = Object.FindAnyObjectByType<RobotPortraitGraphic>();
            player = Object.FindAnyObjectByType<PlayerCharacterController>();
            health = player.GetComponent<Health>(); inventory = player.GetComponent<PlayerWeaponsManager>();
            carrier = player.GetComponent<StolenWeaponCarrier>(); melee = player.GetComponent<ProtagonistMelee>();
            renderCamera = GameObject.Find("PortraitLabCamera").GetComponent<Camera>();
            sprites = Resources.Load<RobotPortraitSprites>("IronRequiem/RobotPortraitV03");
            Assert.That(portrait, Is.Not.Null); Assert.That(sprites && sprites.HasDamageLevels, Is.True);
            yield return new WaitForSeconds(.9f);
            health.Invincible = false; portrait.ResetReactions(); Directory.CreateDirectory(Reports);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; PlayerInputLock.Set(false); Time.captureDeltaTime = captureDelta;
            InputSystem.actions.devices = devices;
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("PortraitV03Cleanup" + Time.frameCount));
            yield return SceneManager.UnloadSceneAsync(old);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
            Application.runInBackground = background;
        }
        IEnumerator Frames(int count = 2) { for (int i = 0; i < count; i++) yield return null; }
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        void SetRatio(float ratio)
        {
            float difference = ratio * health.MaxHealth - health.CurrentHealth;
            if (difference > 0) health.Heal(difference); else if (difference < 0) health.TakeDamage(-difference, null);
        }
        IEnumerator Level(int level)
        {
            SetRatio(level == 0 ? .9f : level == 1 ? .6f : level == 2 ? .35f : .15f);
            yield return new WaitForSeconds(.24f); portrait.ResetReactions(); yield return Frames();
            Assert.That(portrait.DamageLevel, Is.EqualTo(level));
        }
        IEnumerator Turn(float degreesPerSecond, int frames = 8)
        {
            for (int i = 0; i < frames; i++)
            {
                player.SetViewAngles(player.transform.eulerAngles.y + degreesPerSecond / 60, 0);
                yield return null;
            }
        }
        void Face(int level, Pose pose)
        {
            Assert.That(portrait.DamageLevel, Is.EqualTo(level)); Assert.That(portrait.CurrentPose, Is.EqualTo(pose));
            Assert.That(portrait.sprite, Is.SameAs(sprites.Get(level, pose)));
        }

        [UnityTest] public IEnumerator A_ImportsAll23AndChecksAlphaResolutionAndAlignment()
        {
            long memory = 0; int count = 0;
            for (int d = 0; d < 4; d++) foreach (Pose pose in Enum.GetValues(typeof(Pose)))
            { yield return CheckImport(sprites.Get(d, pose)); memory += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(sprites.Get(d, pose).texture); count++; }
            foreach (var sprite in new[] { sprites.Attack, sprites.Overdrive, sprites.Offline })
            { yield return CheckImport(sprite); memory += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(sprite.texture); count++; }
            Assert.That(count, Is.EqualTo(23)); Assert.That(portrait.raycastTarget, Is.False); Assert.That(portrait.Overlay.raycastTarget, Is.False);
            File.WriteAllText(Reports + "/texture-memory.txt", "23 textures, Editor runtime memory bytes=" + memory + "; 512 max, RGBA, no mipmaps, non-readable.\n");
            Face(0, Pose.Front); Capture("d0-front");
        }
        IEnumerator CheckImport(Sprite sprite)
        {
            Assert.That(sprite, Is.Not.Null); var path = AssetDatabase.GetAssetPath(sprite);
            Assert.That(path, Does.Contain("/v03/"));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite)); Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.alphaIsTransparency, Is.True); Assert.That(importer.mipmapEnabled || importer.isReadable, Is.False);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect)); Assert.That(importer.maxTextureSize, Is.EqualTo(512));
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(512, 512))); Assert.That(sprite.pivot, Is.EqualTo(sprite.rect.size * .5f));
            var png = new Texture2D(2, 2); png.LoadImage(File.ReadAllBytes(path));
            Assert.That(png.GetPixel(0, 0).a, Is.Zero); Assert.That(png.GetPixel(png.width / 2, png.height / 2).a, Is.GreaterThan(.9f));
            Object.Destroy(png); yield return null;
        }

        [UnityTest] public IEnumerator B_HealthThresholdsImpactsAndHealingUseResultingDamageTier()
        {
            float[] ratios = { 1, .75f, .749f, .5f, .499f, .25f, .249f, .20f, .199f, .01f, .20f, .5f, .75f, 1 };
            int[] expected = { 0, 0, 1, 1, 2, 2, 2, 2, 3, 3, 2, 1, 0, 0 };
            for (int i = 0; i < ratios.Length; i++)
            {
                SetRatio(ratios[i]); yield return new WaitForSeconds(.24f); Face(expected[i], Pose.Front);
                if (ratios[i] == .20f) Capture("health-20-percent");
                if (ratios[i] == .199f) Capture("health-19-9-percent");
            }
            for (int d = 0; d < 4; d++)
            {
                health.Heal(health.MaxHealth); yield return Frames();
                SetRatio(d == 0 ? .9f : d == 1 ? .6f : d == 2 ? .35f : .15f); yield return Frames();
                Assert.That(portrait.CurrentReaction, Is.EqualTo(Reaction.Impact)); Face(d, Pose.Impact); Capture("d" + d + "-impact");
                yield return new WaitForSeconds(.24f); Face(d, Pose.Front); Capture("d" + d + "-front");
            }
        }

        [UnityTest] public IEnumerator C_GameplayYawDirectionsWrapCenterHysteresisAndRecoil()
        {
            for (int d = 0; d < 4; d++)
            {
                yield return Level(d); player.SetViewAngles(350, 0); portrait.ResetLookSample(); yield return Frames();
                yield return Turn(120); Assert.That(portrait.GazeDirection, Is.EqualTo(1)); Face(d, Pose.Right); Capture("d" + d + "-right");
                yield return Turn(-120, 12); Assert.That(portrait.GazeDirection, Is.EqualTo(-1)); Face(d, Pose.Left); Capture("d" + d + "-left");
                yield return new WaitForSeconds(.24f); Face(d, Pose.Front);
                Assert.That(Mathf.Abs(portrait.SmoothedYawSpeed), Is.LessThan(20));
            }
            float yaw = player.transform.eulerAngles.y;
            for (int i = 0; i < 24; i++) { player.SetViewAngles(yaw + (i % 2 == 0 ? .03f : -.03f), 0); yield return null; Assert.That(portrait.GazeDirection, Is.Zero); }
            Keys(Key.A); yield return new WaitForSeconds(.2f); Keys(); yield return Frames(); Assert.That(portrait.GazeDirection, Is.Zero, "Strafing is not look input");
            DistanceCameraImpulse.Kick(player.PlayerCamera, new Vector3(2, 4, 1), Vector3.zero);
            yield return new WaitForSeconds(.12f); Assert.That(portrait.GazeDirection, Is.Zero, "Local camera recoil must not turn the portrait");
            Time.timeScale = .2f; portrait.ResetLookSample(); yield return Frames();
            yield return Turn(360, 8); Face(3, Pose.Right);
            yield return new WaitForSecondsRealtime(.3f); Face(3, Pose.Front); Time.timeScale = 1;
        }

        [UnityTest] public IEnumerator D_PauseTeleportCameraSwapAndCinematicResetLookSample()
        {
            yield return Level(2); yield return Turn(120); Face(2, Pose.Right);
            Time.timeScale = 0; float stopped = portrait.ReactionTime; Sprite before = portrait.sprite;
            player.SetViewAngles(player.transform.eulerAngles.y + 30, 0); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(portrait.ReactionTime, Is.EqualTo(stopped)); Assert.That(portrait.sprite, Is.SameAs(before)); Assert.That(portrait.Overlay.AliveCount, Is.Zero);
            Time.timeScale = 1; yield return Frames(); Face(2, Pose.Front);
            yield return Turn(-120); Face(2, Pose.Left);
            player.transform.position += Vector3.right * 8; player.SetViewAngles(player.transform.eulerAngles.y + 25, 0);
            yield return Frames(); Face(2, Pose.Front);
            var original = player.PlayerCamera; var replacement = new GameObject("PortraitTestCamera").AddComponent<Camera>(); replacement.enabled = false;
            player.PlayerCamera = replacement; player.SetViewAngles(player.transform.eulerAngles.y + 20, 0); yield return Frames(); Face(2, Pose.Front);
            player.PlayerCamera = original; Object.Destroy(replacement.gameObject); yield return Frames();
            PlayerInputLock.Set(true); player.SetViewAngles(player.transform.eulerAngles.y - 40, 0); yield return Frames(); Face(2, Pose.Front);
            PlayerInputLock.Set(false); yield return Frames(); Face(2, Pose.Front);
            player.SetViewAngles(player.transform.eulerAngles.y + 100, 0); yield return Frames(); Face(2, Pose.Front);
            yield return Turn(120); Face(2, Pose.Right);
        }

        [UnityTest] public IEnumerator E_SparksAreAnimatedBoundedPoseAlignedAndStopWhenHealedOrPaused()
        {
            for (int d = 0; d < 4; d++)
            {
                yield return Level(d); yield return Turn(120, 3);
                Assert.That(portrait.GazeDirection, Is.EqualTo(1));
                Assert.That(portrait.Overlay.BurstCount, Is.EqualTo(d == 0 ? 0 : 1));
                if (d == 0) { Assert.That(portrait.Overlay.AliveCount, Is.Zero); continue; }
                Assert.That(portrait.Overlay.LastBurstSize, Is.EqualTo(d == 1 ? 1 : d == 2 ? 4 : 8));
                Assert.That(portrait.Overlay.CrackAnchor.x, Is.GreaterThan(.5f)); Assert.That(portrait.rectTransform.localScale.x, Is.GreaterThan(0));
                Capture("d" + d + "-sparks-frame1");
                var firstMesh = portrait.Overlay.canvasRenderer.GetMesh();
                Vector3 firstPoint = firstMesh.vertices[0]; byte firstAlpha = firstMesh.colors32[0].a;
                yield return Frames(3); Capture("d" + d + "-sparks-frame2");
                var secondMesh = portrait.Overlay.canvasRenderer.GetMesh();
                Assert.That(Vector3.Distance(firstPoint, secondMesh.vertices[0]), Is.GreaterThan(.05f), "Animated spark vertices move");
                Assert.That(secondMesh.colors32[0].a, Is.LessThan(firstAlpha), "Animated sparks fade");
                Assert.That(portrait.Overlay.BurstCount, Is.EqualTo(1), "No burst each frame");
                Time.timeScale = 0; yield return Frames(); Assert.That(portrait.Overlay.AliveCount, Is.Zero);
                Time.timeScale = 1; yield return Frames(); SetRatio(1); yield return Frames(); Assert.That(portrait.Overlay.AliveCount, Is.Zero);
            }
        }

        [UnityTest] public IEnumerator F_RealFistChargeTurnsCancellationAndConfirmedHeavyImpactPreserveDamage()
        {
            for (int d = 0; d < 4; d++)
            {
                yield return Level(d); Keys(Key.F); yield return Frames(4);
                Assert.That(melee.IsCharging, Is.True, "F action must start the real melee component");
                yield return new WaitForSeconds(.18f); Face(d, Pose.Charge); Assert.That(portrait.CurrentReaction, Is.EqualTo(Reaction.Charging));
                float low = portrait.Overlay.Energy; Capture("d" + d + "-charge-low");
                yield return new WaitForSeconds(.65f); Assert.That(melee.ChargeLevel, Is.GreaterThan(.6f)); Assert.That(portrait.Overlay.Energy, Is.GreaterThan(low));
                yield return Turn(120, 5); Face(d, Pose.Right); Capture("d" + d + "-charge-right");
                yield return Turn(-120, 8); Face(d, Pose.Left); Capture("d" + d + "-charge-left");
                Assert.That(portrait.Overlay.CrackAnchor.x, Is.GreaterThan(.5f), "The physical fracture never mirrors");
                melee.CancelCharge(); Keys(); yield return new WaitForSeconds(.25f); Face(d, Pose.Front);
                Assert.That(portrait.CurrentReaction, Is.Not.EqualTo(Reaction.Charging));
            }
            yield return Level(1); Keys(Key.F); yield return new WaitForSeconds(.25f);
            SetRatio(.15f); yield return Frames(); Assert.That(portrait.CurrentReaction, Is.EqualTo(Reaction.Impact)); Face(3, Pose.Impact);
            yield return new WaitForSeconds(.24f); Face(3, Pose.Charge); Capture("damage-during-charge");
            health.Heal(health.MaxHealth * .2f); yield return Frames(); Face(2, Pose.Charge);
            SetRatio(.15f); yield return new WaitForSeconds(.24f); Face(3, Pose.Charge);
            yield return new WaitForSeconds(1); Assert.That(melee.FullyCharged, Is.True);
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule); target.name = "PortraitHeavyTarget";
            target.transform.position = player.PlayerCamera.transform.position + player.PlayerCamera.transform.forward * 1.1f; target.transform.localScale = Vector3.one * .4f;
            var targetHealth = target.AddComponent<Health>(); targetHealth.MaxHealth = 200; target.AddComponent<Damageable>(); yield return Frames();
            Keys(); float deadline = Time.realtimeSinceStartup + 5;
            while (melee.LastHitCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(melee.LastHitCount, Is.GreaterThan(0)); Assert.That(targetHealth.CurrentHealth, Is.LessThan(200));
            yield return Frames(); Assert.That(portrait.CurrentReaction, Is.EqualTo(Reaction.Attack));
            Assert.That(portrait.sprite, Is.Not.SameAs(sprites.Attack)); Assert.That(portrait.DamageLevel, Is.EqualTo(3)); Capture("heavy-hit-d3");
        }

        [UnityTest] public IEnumerator G_ShotsExecutionOverdriveDeathAndRespawnNeverRepairDamage()
        {
            var weapon = inventory.GetActiveWeapon();
            if (!weapon)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<WeaponController>("Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_IR_ArmCannon_VO.prefab");
                Assert.That(inventory.AddWeapon(prefab), Is.True); inventory.SwitchToWeaponIndex(0, true);
                yield return new WaitForSeconds(1.2f); weapon = inventory.GetActiveWeapon();
            }
            Assert.That(weapon, Is.Not.Null);
            for (int d = 0; d < 4; d++)
            {
                yield return Level(d); weapon.SetAmmo(weapon.MaxAmmo);
                Assert.That(weapon.HandleShootInputs(true, true, false), Is.True); yield return Frames();
                Assert.That(portrait.CurrentReaction, Is.EqualTo(Reaction.Attack));
                Assert.That(portrait.sprite, d == 0 ? Is.SameAs(sprites.Attack) : Is.SameAs(sprites.Get(d, Pose.Front))); Capture("d" + d + "-attack");
                int rests = 0;
                for (int i = 0; i < 36; i++) { weapon.OnShoot?.Invoke(); yield return null; if (portrait.CurrentReaction != Reaction.Attack) rests++; }
                Assert.That(rests, Is.GreaterThan(0)); yield return new WaitForSeconds(.3f);
                carrier.NotifyRip(null, Vector3.zero); yield return new WaitForSeconds(.4f); Assert.That(portrait.CurrentReaction, Is.EqualTo(Reaction.Attack));
                yield return new WaitForSeconds(.14f); Assert.That(portrait.CurrentReaction, Is.Not.EqualTo(Reaction.Attack));
            }
            inventory.SwitchToWeaponIndex(-1, true); yield return new WaitForSeconds(.3f);
            Face(3, Pose.Front); Assert.That(portrait.CurrentReaction, Is.Not.EqualTo(Reaction.Attack));
            inventory.SwitchToWeaponIndex(0, true); yield return new WaitForSeconds(1.2f); Face(3, Pose.Front);
            var module = Object.Instantiate(AssetDatabase.LoadAssetAtPath<StolenCannon>("Assets/IronRequiem/Prefabs/FastCombat/T07_Module.prefab"));
            var body = module.GetComponent<Rigidbody>(); if (!body) body = module.gameObject.AddComponent<Rigidbody>(); body.isKinematic = true;
            var collider = module.GetComponent<Collider>(); if (!collider) collider = module.gameObject.AddComponent<BoxCollider>();
            module.MakeAvailable(body, collider, true); Assert.That(module.Equip(carrier, weapon), Is.True);
            typeof(StolenWeaponCarrier).GetField("<Held>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(carrier, module);
            module.Ammo.Drain(module.Ammo.Remaining - module.Ammo.Capacity * .19f); yield return Frames();
            Assert.That(portrait.OverdriveActive, Is.True); Face(3, Pose.Front); Assert.That(portrait.Overlay.Energy, Is.GreaterThan(0)); Capture("d3-overdrive");
            health.Heal(health.MaxHealth); yield return Frames(); Assert.That(portrait.sprite, Is.SameAs(sprites.Overdrive)); Capture("d0-overdrive");
            health.Kill(); yield return Frames(); Assert.That(portrait.sprite, Is.SameAs(sprites.Offline)); Assert.That(portrait.Overlay.AliveCount, Is.Zero); Assert.That(portrait.Overlay.Energy, Is.Zero); Capture("offline");
            health.Heal(health.MaxHealth); yield return Frames(); Assert.That(portrait.sprite, Is.SameAs(sprites.Offline));
            var replacement = new GameObject("PortraitRespawnHealth").AddComponent<Health>(); yield return Frames();
            portrait.Bind(replacement); yield return Frames(); Face(0, Pose.Front); Capture("respawn");
        }

        [UnityTest] public IEnumerator I_RealLookInputSupportsInversionSensitivityAndVirtualGamepad()
        {
            var input = player.GetComponent<PlayerInputHandler>(); bool inverted = input.InvertXAxis; float sensitivity = input.LookSensitivity;
            var pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.actions.devices = new InputDevice[] { keyboard, mouse, pad };
            try
            {
                foreach (bool invert in new[] { false, true }) foreach (float factor in new[] { .75f, 1.5f })
                {
                    input.InvertXAxis = invert; input.LookSensitivity = factor; portrait.ResetLookSample(); yield return Frames();
                    float beforeYaw = player.transform.eulerAngles.y;
                    for (int i = 0; i < 10; i++)
                    {
                        InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(10, 0) }); yield return null;
                    }
                    Assert.That(Mathf.Sign(Mathf.DeltaAngle(beforeYaw, player.transform.eulerAngles.y)), Is.EqualTo(invert ? -1 : 1));
                    Assert.That(portrait.GazeDirection, Is.EqualTo(invert ? -1 : 1)); Capture("mouse-invert-" + invert + "-sensitivity-" + factor);
                    InputSystem.QueueStateEvent(mouse, new MouseState()); yield return new WaitForSeconds(.25f); Face(0, Pose.Front);
                }
                input.InvertXAxis = false; input.LookSensitivity = 1;
                InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(.7f, 0) });
                yield return Frames(8); Face(0, Pose.Right); Capture("gamepad-right");
                InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(-.7f, 0) });
                yield return Frames(8); Face(0, Pose.Left); Capture("gamepad-left");
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return new WaitForSeconds(.25f); Face(0, Pose.Front);
            }
            finally
            {
                input.InvertXAxis = inverted; input.LookSensitivity = sensitivity;
                InputSystem.actions.devices = new InputDevice[] { keyboard, mouse }; InputSystem.RemoveDevice(pad);
            }
        }

        [UnityTest] public IEnumerator H_SubscriptionsOverlayReuseAndManagedAllocations()
        {
            for (int i = 0; i < 4; i++) portrait.Bind(health, inventory, carrier, melee);
            int damageHandlers = 0;
            foreach (var handler in health.OnDamaged.GetInvocationList()) if (handler.Target == portrait) damageHandlers++;
            Assert.That(damageHandlers, Is.EqualTo(1));
            Assert.That(portrait.GetComponentsInChildren<RobotPortraitOverlay>(true).Length, Is.EqualTo(1));
            var update = (Action)Delegate.CreateDelegate(typeof(Action), portrait,
                typeof(RobotPortraitGraphic).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic));
            for (int i = 0; i < 40; i++) update();
            long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 1000; i++) update();
            long idle = GC.GetAllocatedBytesForCurrentThread() - before;
            yield return Level(2); Keys(Key.F); yield return new WaitForSeconds(.4f);
            Assert.That(melee.IsCharging, Is.True); for (int i = 0; i < 40; i++) update();
            before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 1000; i++) update();
            long active = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(portrait.Overlay.BurstCount, Is.LessThan(30), "Cooldown bounds bursts, not every frame");
            File.WriteAllText(Reports + "/allocation.txt", "1000 warmed portrait LateUpdate calls: idle=" + idle + " bytes; charging + sparks=" + active +
                " bytes. Includes reused overlay simulation, excludes Canvas rendering and full-frame GC. Editor, not Player FPS.\n");
            Assert.That(idle, Is.Zero); Assert.That(active, Is.Zero);
            portrait.enabled = false;
            if (health.OnDamaged != null)
                foreach (var handler in health.OnDamaged.GetInvocationList()) Assert.That(handler.Target, Is.Not.SameAs(portrait));
            Assert.That(portrait.Overlay.AliveCount, Is.Zero);
            melee.CancelCharge(); Keys();
        }

        void Capture(string name)
        {
            CaptureResolution(name + "-720p", 1280, 720); CaptureResolution(name + "-1080p", 1920, 1080); CaptureResolution(name + "-1440p", 2560, 1440);
        }
        void CaptureResolution(string name, int width, int height)
        {
            var target = RenderTexture.GetTemporary(width, height, 24); var previous = renderCamera.targetTexture; var active = RenderTexture.active;
            try
            {
                renderCamera.targetTexture = target; Canvas.ForceUpdateCanvases(); renderCamera.Render(); RenderTexture.active = target;
                var corners = new Vector3[4]; portrait.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var pixel = renderCamera.WorldToScreenPoint(corner);
                    Assert.That(pixel.x, Is.InRange(0, width)); Assert.That(pixel.y, Is.InRange(0, height));
                }
                var png = new Texture2D(width, height, TextureFormat.RGBA32, false);
                png.ReadPixels(new Rect(0, 0, width, height), 0, 0); png.Apply(); File.WriteAllBytes(Reports + "/" + name + ".png", png.EncodeToPNG()); Object.Destroy(png);
            }
            finally { renderCamera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); }
        }
    }
}
