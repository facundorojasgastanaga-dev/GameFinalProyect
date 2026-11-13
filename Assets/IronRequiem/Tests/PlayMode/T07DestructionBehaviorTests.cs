using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
using Unity.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    public sealed class T07DestructionBehaviorTests
    {
        const string Lab = "Assets/IronRequiem/Scenes/T07DestructionLab.unity";
        const string Variant = "Assets/IronRequiem/Prefabs/Enemies/T07_Common_Destructible.prefab";
        const string Original = "Assets/IronRequiem/Prefabs/Enemies/T07_Common_Optimized.prefab";
        const string Reports = "Tools/IronRequiem/Reports/T07Destruction";
        EnemyController enemy;
        RobotDamageProgression damage;
        Health health;
        PlayerCharacterController player;
        Keyboard keyboard;
        Mouse mouse;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? oldDevices;
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1; Application.runInBackground = true;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(Lab, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keyboard.MakeCurrent(); mouse.MakeCurrent();
            oldDevices = InputSystem.actions.devices; InputSystem.actions.devices = new InputDevice[] { keyboard, mouse };
            yield return null; yield return null;
            enemy = Object.FindAnyObjectByType<RobotDamageProgression>().GetComponent<EnemyController>(); damage = enemy.GetComponent<RobotDamageProgression>(); health = enemy.GetComponent<Health>();
            health.MaxHealth = 100; health.CurrentHealth = 100; Freeze(enemy);
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); player.GetComponent<Health>().Invincible = true;
#if UNITY_EDITOR
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
#endif
            Cursor.lockState = CursorLockMode.Locked; yield return new WaitForSeconds(.3f);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("DestructionCleanup_" + Time.frameCount));
            if (old.IsValid() && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
            InputSystem.actions.devices = oldDevices; if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
            Cursor.lockState = CursorLockMode.None; yield return null;
        }
        static void Freeze(EnemyController target)
        {
            target.GetComponent<EnemyMobile>().enabled = false;
            var nav = target.NavMeshAgent; if (nav && nav.enabled && nav.isOnNavMesh) { nav.ResetPath(); nav.isStopped = true; }
        }
        IEnumerator Tap(Key key) { Cursor.lockState = CursorLockMode.Locked; InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return new WaitForSecondsRealtime(.04f); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; }
        void SetRatio(float ratio) => health.TakeDamage(Mathf.Max(0, health.CurrentHealth - health.MaxHealth * ratio), null);
        static Camera CameraAt(Vector3 position, Vector3 target)
        {
            var camera = new GameObject("DestructionEvidenceCamera").AddComponent<Camera>(); camera.transform.position = position; camera.transform.LookAt(target); camera.fieldOfView = 45;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.045f, .055f, .065f); return camera;
        }
        static void Capture(Camera camera, string name)
        {
            var target = new RenderTexture(1100, 740, 24); camera.targetTexture = target; camera.Render();
            var old = RenderTexture.active; RenderTexture.active = target; var image = new Texture2D(1100, 740, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1100, 740), 0, 0); image.Apply(); File.WriteAllBytes(Reports + "/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null; target.Release(); Object.Destroy(image); Object.Destroy(target);
        }
        [UnityTest] public IEnumerator GradualDamageHasStrictThresholdsStableLodsAndPermanentArmorLoss()
        {
            var camera = CameraAt(enemy.transform.position + enemy.transform.forward * 4 + enemy.transform.right * 2 + Vector3.up * 2.2f, enemy.transform.position + Vector3.up);
            Capture(camera, "stage-100"); SetRatio(.75f); Assert.That(damage.TransitionCount, Is.Zero);
            SetRatio(.74f); yield return new WaitForSeconds(.3f); Assert.That(damage.Stage, Is.EqualTo(1)); Assert.That(damage.Armor.Count(p => p.IsDetached), Is.Zero); Capture(camera, "stage-74");
            SetRatio(.49f); yield return new WaitForSeconds(.2f); Assert.That(damage.Stage, Is.EqualTo(2)); Assert.That(damage.Armor.Count(p => p.IsDetached), Is.EqualTo(2)); Assert.That(damage.Smoke.isEmitting, Is.True); Capture(camera, "stage-49");
            Assert.That(enemy.TryAtack(player.transform.position), Is.True, "Secondary armor loss must preserve ranged attacks");
            SetRatio(.24f); yield return new WaitForSeconds(.3f); Assert.That(damage.Stage, Is.EqualTo(3)); Assert.That(damage.TransitionCount, Is.EqualTo(3)); Assert.That(damage.Armor.All(p => p.IsDetached), Is.True); Capture(camera, "stage-24");
            var group = enemy.GetComponentsInChildren<LODGroup>().Single(g => g.name == "Body");
            for (int i = 0; i < 3; i++)
            {
                group.ForceLOD(i); yield return null; yield return null;
                foreach (var plate in damage.Armor) Assert.That(plate.GetComponentsInChildren<Renderer>(true).All(r => !r.gameObject.activeInHierarchy), Is.True);
                Capture(camera, "damaged-lod-" + i);
            }
            health.Heal(60); health.TakeDamage(1, null); Assert.That(damage.TransitionCount, Is.EqualTo(3)); Assert.That(damage.Armor.All(p => p.IsDetached), Is.True);
            Assert.That(enemy.GetComponentInChildren<DetachableCannon>().IsDetached, Is.False, "Armor loss must not steal the cannon automatically");
            Assert.That(RobotDestructionPool.ArmorInUse, Is.LessThanOrEqualTo(16)); Object.Destroy(camera.gameObject);
        }
        [UnityTest] public IEnumerator FullHealthLethalAndRepeatedDamageProcessDeathLootAndCannonExactlyOnce()
        {
            var loot = new GameObject("DestructionLootPrototype"); loot.SetActive(false); enemy.LootPrefab = loot; enemy.DropRate = 1;
            int deaths = 0; health.OnDie += () => deaths++;
            int remaining = Object.FindAnyObjectByType<EnemyManager>().NumberOfEnemiesRemaining;
            var cannon = enemy.GetComponentInChildren<StolenCannon>(); player.GetComponent<Health>().Invincible = false; float playerBefore = player.GetComponent<Health>().CurrentHealth;
            health.TakeDamage(1000, null); health.TakeDamage(1000, null); health.Kill();
            Assert.That(deaths, Is.EqualTo(1)); Assert.That(damage.TransitionCount, Is.EqualTo(3)); Assert.That(damage.DeathPresentationCount, Is.EqualTo(1));
            Assert.That(Object.FindAnyObjectByType<EnemyManager>().NumberOfEnemiesRemaining, Is.EqualTo(remaining - 1));
            Assert.That(enemy.enabled, Is.False); Assert.That(enemy.NavMeshAgent.enabled, Is.False); Assert.That(enemy.TryAtack(player.transform.position), Is.False);
            Assert.That(cannon.State, Is.EqualTo(StolenCannonState.Available)); Assert.That(cannon.transform.IsChildOf(enemy.transform), Is.False);
            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Count(g => g.scene == enemy.gameObject.scene && g.name == "DestructionLootPrototype(Clone)"), Is.EqualTo(1));
            yield return new WaitForSeconds(.45f); Assert.That(player.GetComponent<Health>().CurrentHealth, Is.EqualTo(playerBefore));
            Assert.That(enemy.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.name.StartsWith("Body_LOD") || s.name.StartsWith("Armor_")).All(s => !s.gameObject.activeInHierarchy), Is.True);
            yield return new WaitForSeconds(1.4f); Assert.That(enemy == null, Is.True); Assert.That(cannon, Is.Not.Null); Assert.That(cannon.GetComponent<LODGroup>().GetLODs().Length, Is.EqualTo(3));
            yield return new WaitForSeconds(1.5f); Assert.That(RobotDestructionPool.BurstsInUse, Is.Zero); Assert.That(RobotDestructionPool.ArmorInUse, Is.Zero);
        }
        [UnityTest] public IEnumerator SimultaneousDamagePreservesPreviouslyStolenEquippedCannon()
        {
#if UNITY_EDITOR
            var spare = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Original), new Vector3(8, .05f, 8), Quaternion.identity).GetComponent<EnemyController>();
#endif
            yield return null; yield return null; Freeze(spare);
            var module = enemy.GetComponentInChildren<StolenCannon>(); enemy.GetComponentInChildren<DetachableCannon>().Detach(); module.Body.isKinematic = true;
            var point = player.PlayerCamera.transform.position + player.PlayerCamera.transform.forward * 1.5f; module.transform.position = point; Physics.SyncTransforms(); module.transform.position += point - module.SolidCollider.bounds.center; Physics.SyncTransforms();
            yield return new WaitForSeconds(.4f);
#if UNITY_EDITOR
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
#endif
            Cursor.lockState = CursorLockMode.Locked; yield return null;
            var carrier = player.GetComponent<StolenWeaponCarrier>();
            File.WriteAllText(Reports + "/pickup-diagnostic.txt", "candidate=" + carrier.Candidate + " state=" + module.State + " canAct=" + player.GetComponent<MechanicInput>().CanAct + " bounds=" + module.SolidCollider.bounds + " camera=" + player.PlayerCamera.transform.position);
            Assert.That(carrier.Candidate, Is.SameAs(module), "Native pickup ray must actually see the detached module");
            // This case verifies body death with a PREVIOUSLY stolen weapon. Prepare that
            // state through the native pickup API; keyboard pickup is covered by the FPS suite.
            Assert.That(carrier.TryPickup(module), Is.True, "Native pickup must accept the visible module; existing slot=" + player.GetComponent<PlayerWeaponsManager>().HasWeapon(module.WeaponPrefab));
            Assert.That(carrier.Held, Is.SameAs(module)); yield return new WaitForSeconds(2.1f);
            var equipped = module.EquippedWeapon;
            int deaths = 0; health.OnDie += () => deaths++; health.TakeDamage(60, null); health.TakeDamage(60, null); health.TakeDamage(60, null);
            Assert.That(deaths, Is.EqualTo(1)); Assert.That(damage.TransitionCount, Is.EqualTo(3)); Assert.That(damage.DeathPresentationCount, Is.EqualTo(1));
            yield return new WaitForSeconds(2); Assert.That(enemy == null, Is.True); Assert.That(carrier.Held, Is.SameAs(module)); Assert.That(module.EquippedWeapon, Is.SameAs(equipped));
            Assert.That(module.Ammo.Remaining, Is.EqualTo(100)); Assert.That(module.State, Is.EqualTo(StolenCannonState.Equipped));
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return new WaitForSeconds(.4f); InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null; yield return null;
            Assert.That(module.Ammo.Remaining, Is.LessThan(100), "The stolen weapon must still shoot after its source body exploded");
        }
        [UnityTest] public IEnumerator DeathInterruptsAttackWhileArmorSocketsFollowAnimatedBones()
        {
            var driver = enemy.GetComponent<T07AnimationDriver>(); var socket = damage.Armor[3].DebrisSocket; Vector3 start = socket.position;
            Assert.That(enemy.TryAtack(player.transform.position), Is.True); driver.Animator.SetTrigger("Attack"); yield return new WaitForSeconds(.16f);
            Assert.That(Vector3.Distance(start, socket.position), Is.GreaterThan(.001f));
            health.Kill(); Assert.That(driver.IsDead, Is.True); Assert.That(enemy.NavMeshAgent.enabled, Is.False); Assert.That(enemy.GetComponent<EnemyMobile>().enabled, Is.False);
            Assert.That(enemy.TryAtack(player.transform.position), Is.False); Assert.That(damage.DeathPresentationCount, Is.EqualTo(1));
            var camera = CameraAt(enemy.transform.position + new Vector3(3, 2, 4), enemy.transform.position + Vector3.up);
            yield return new WaitForSeconds(.1f); Capture(camera, "death-overload"); yield return new WaitForSeconds(.25f); Capture(camera, "death-burst");
            yield return new WaitForSeconds(.65f); Capture(camera, "death-residual"); Object.Destroy(camera.gameObject);
        }
        [UnityTest] public IEnumerator ReusedDebrisAndEffectsResetAndNewEnemiesStartComplete()
        {
            health.Kill(); yield return new WaitForSeconds(3.5f); Assert.That(RobotDestructionPool.BurstsInUse, Is.Zero); Assert.That(RobotDestructionPool.ArmorInUse, Is.Zero); Assert.That(RobotBodyBurst.AudioVoices, Is.Zero);
#if UNITY_EDITOR
            var fresh = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Variant), new Vector3(3, .05f, 3), Quaternion.identity);
#endif
            yield return null; yield return null; var p = fresh.GetComponent<RobotDamageProgression>(); Freeze(fresh.GetComponent<EnemyController>());
            Assert.That(p.Stage, Is.Zero); Assert.That(p.TransitionMask, Is.Zero); Assert.That(p.Armor.All(a => !a.IsDetached && a.gameObject.activeInHierarchy), Is.True);
            Assert.That(p.Sparks.particleCount, Is.Zero); Assert.That(p.Smoke.particleCount, Is.Zero);
            int created = RobotDestructionPool.CreatedBursts; fresh.GetComponent<Health>().Kill(); yield return new WaitForSeconds(.4f);
            Assert.That(RobotDestructionPool.CreatedBursts, Is.EqualTo(created)); Assert.That(RobotDestructionPool.BurstsInUse, Is.EqualTo(1));
            yield return new WaitForSeconds(3); Assert.That(RobotDestructionPool.BurstsInUse, Is.Zero); Assert.That(RobotDestructionPool.ArmorInUse, Is.Zero);
        }

        [Serializable] class Metrics { public string label; public int enemies, frames, maxArmor, maxBursts, maxParticles; public double meanMs, p95Ms, mainThreadMs, gcBytes; }
        readonly List<Metrics> metrics = new List<Metrics>();
        IEnumerator Measure(string label, int count, int frames, int warmup)
        {
            for (int i = 0; i < warmup; i++) yield return null;
            var particleSystems = Object.FindObjectsByType<ParticleSystem>();
            var samples = new double[frames]; var result = new Metrics { label = label, enemies = count, frames = frames };
            using (var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1))
            using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
            {
                for (int i = 0; i < frames; i++)
                {
                    yield return null; samples[i] = Time.unscaledDeltaTime * 1000;
                    result.mainThreadMs += cpu.Valid ? cpu.LastValue / 1000000d : -1; result.gcBytes += gc.Valid ? gc.LastValue : -1;
                    result.maxArmor = Mathf.Max(result.maxArmor, RobotDestructionPool.ArmorInUse); result.maxBursts = Mathf.Max(result.maxBursts, RobotDestructionPool.BurstsInUse);
                    int particles = 0; foreach (var ps in particleSystems) if (ps) particles += ps.particleCount; result.maxParticles = Mathf.Max(result.maxParticles, particles);
                }
            }
            result.mainThreadMs /= frames; result.gcBytes /= frames; result.meanMs = samples.Average(); Array.Sort(samples); result.p95Ms = samples[(int)(frames * .95)]; metrics.Add(result);
        }
        [UnityTest] public IEnumerator ZZ_MeasureSixteenRobotsAndSimultaneousDeathsWithBoundedPools()
        {
            metrics.Clear(); Object.FindAnyObjectByType<GameFlowManager>().enabled = false; player.PlayerCamera.enabled = false;
            enemy.transform.position = new Vector3(0, 0, 40); enemy.enabled = false;
            var camera = CameraAt(new Vector3(12, 9, -16), new Vector3(0, 1, 6)); camera.fieldOfView = 48;
            player.PlayerCamera.tag = "Untagged"; camera.tag = "MainCamera";
            var bots = new List<EnemyController>();
            foreach (string path in new[] { Original, Variant })
            {
                for (int i = 0; i < 16; i++)
                {
#if UNITY_EDITOR
                    var bot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), new Vector3((i % 4 - 1.5f) * 3, .05f, (i / 4) * 3), Quaternion.Euler(0, 180, 0)).GetComponent<EnemyController>(); bots.Add(bot);
#endif
                }
                yield return null; yield return null; foreach (var bot in bots) { Freeze(bot); bot.enabled = false; }
                yield return Measure(path == Original ? "original_16_intact" : "variant_16_intact", 16, 180, 60);
                if (path == Variant) break;
                var manager = Object.FindAnyObjectByType<EnemyManager>(); foreach (var bot in bots) { manager.UnregisterEnemy(bot); Object.Destroy(bot.gameObject); } bots.Clear(); yield return null;
            }
            foreach (var bot in bots) bot.GetComponent<Health>().TakeDamage(bot.GetComponent<Health>().MaxHealth * .51f, null);
            yield return Measure("variant_16_49percent_transient", 16, 180, 0); Capture(camera, "crowd-49");
            foreach (var bot in bots) bot.GetComponent<Health>().TakeDamage(bot.GetComponent<Health>().MaxHealth * .25f, null);
            yield return Measure("variant_16_24percent", 16, 180, 0); Capture(camera, "crowd-24");
            int deaths = 0; foreach (var bot in bots) { bot.GetComponent<Health>().OnDie += () => deaths++; bot.GetComponent<Health>().Kill(); }
            yield return new WaitForSeconds(.35f); Capture(camera, "crowd-death"); Assert.That(deaths, Is.EqualTo(16));
            Assert.That(RobotDestructionPool.BurstsInUse, Is.EqualTo(16)); Assert.That(RobotBodyBurst.AudioVoices, Is.LessThanOrEqualTo(4));
            Assert.That(RobotDestructionPool.ArmorInUse, Is.LessThanOrEqualTo(16));
            yield return Measure("variant_16_death_tail", 16, 180, 0); yield return new WaitForSeconds(1.5f);
            Assert.That(RobotDestructionPool.BurstsInUse, Is.Zero); Assert.That(RobotDestructionPool.ArmorInUse, Is.Zero); Assert.That(RobotBodyBurst.AudioVoices, Is.Zero);
            var lines = new List<string> { "Editor only; AI disabled equally; animations/foot IK active; no controlled Player/GPU benchmark.", "GPU=" + SystemInfo.graphicsDeviceName + "; screen=" + Screen.width + "x" + Screen.height + "; quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "; VSync=" + QualitySettings.vSyncCount, "label,enemies,frames,mean_ms,p95_ms,main_thread_ms,gc_bytes,max_armor,max_bursts,max_particles" };
            foreach (var m in metrics) lines.Add(string.Join(",", m.label, m.enemies, m.frames, m.meanMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), m.p95Ms.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), m.mainThreadMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), m.gcBytes.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), m.maxArmor, m.maxBursts, m.maxParticles));
            File.WriteAllLines(Reports + "/performance.csv", lines); Object.Destroy(camera.gameObject);
        }
    }
}
