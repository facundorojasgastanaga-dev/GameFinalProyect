using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace IronRequiem.Tests
{
    public sealed class T07OptimizationBehaviorTests
    {
        [UnityTest]
        public IEnumerator ReducedVisualsSurviveDetachEquipEnergyWarningThrowAndExplosion()
        {
            Time.timeScale = 1; Application.runInBackground = true;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/T07OptimizationLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null; yield return null;
            var player = Object.FindAnyObjectByType<PlayerCharacterController>();
            var enemy = Object.FindAnyObjectByType<EnemyController>();
            player.GetComponent<Health>().Invincible = true;
            enemy.GetComponent<EnemyMobile>().enabled = false;
            Assert.That(enemy.NavMeshAgent.isOnNavMesh, Is.True);
            enemy.NavMeshAgent.ResetPath();
            var module = enemy.GetComponentInChildren<StolenCannon>();
            var joint = enemy.GetComponentInChildren<DetachableCannon>();
            var body = enemy.GetComponentsInChildren<LODGroup>().Single(g => g.name == "Body");
            var cannon = module.GetComponent<LODGroup>();
            int frame = Time.frameCount;
            for (int i = 0; i < 60; i++) yield return null;
            Assert.That(Time.frameCount, Is.GreaterThan(frame + 55), "The simulation must actually advance.");
            for (int i = 0; i < 3; i++) { body.ForceLOD(i); cannon.ForceLOD(i); yield return null; }
            body.ForceLOD(-1); cannon.ForceLOD(-1);
            var position = module.transform.position;
            joint.Detach(); joint.Detach();
            Assert.That(joint.IsDetached, Is.True);
            Assert.That(module.transform.parent, Is.Null);
            Assert.That(module.GetComponent<LODGroup>(), Is.SameAs(cannon));
            Assert.That(module.Body, Is.Not.Null); Assert.That(module.SolidCollider, Is.Not.Null);
            Assert.That(Vector3.Distance(position, module.transform.position), Is.LessThan(.001f));
            Assert.That(Object.FindObjectsByType<StolenCannon>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(enemy.GetCurrentWeapon().GetCurrentAmmo(), Is.Zero);
            module.Body.isKinematic = true;
            yield return new WaitForSeconds(.4f);

            // Exercise the module contract directly, without pretending physical keyboard input was tested.
            var manager = player.GetComponent<PlayerWeaponsManager>();
            Assert.That(manager.AddWeapon(module.WeaponPrefab), Is.True);
            var weapon = manager.HasWeapon(module.WeaponPrefab);
            Assert.That(module.Equip(player.GetComponent<StolenWeaponCarrier>(), weapon), Is.True);
            var renderers = cannon.GetLODs().SelectMany(l => l.renderers).ToArray();
            for (int i = 0; i < 3; i++)
            {
                cannon.ForceLOD(i); yield return null;
                Assert.That(renderers.All(r => !r.enabled), Is.True, "Equipping must hide every world LOD.");
            }
            cannon.ForceLOD(-1);
            Assert.That(module.Ammo.TryConsume(12), Is.True);
            Assert.That(module.Ammo.Remaining, Is.EqualTo(88));
            Assert.That(weapon.GetCurrentAmmo(), Is.EqualTo(88));
            module.Ammo.Drain(73);
            Assert.That(module.Overdrive.IsArmed, Is.True);
            yield return new WaitForSeconds(.2f);
            Assert.That(module.Overdrive.Smoke.isPlaying, Is.True);
            Assert.That(module.Release(player.transform.position + Vector3.forward * 3 + Vector3.up,
                Vector3.up, 1, player.gameObject), Is.True);
            Assert.That(module.State, Is.EqualTo(StolenCannonState.Thrown));
            Assert.That(renderers.All(r => r.enabled), Is.True);
            Assert.That(body, Is.Not.Null); Assert.That(body.enabled, Is.True);
            Assert.That(module.Explosion.TryDetonate(), Is.True);
            Assert.That(module.Explosion.TryDetonate(), Is.False);
            Directory.CreateDirectory("Tools/IronRequiem/Reports/T07Optimization");
            File.WriteAllText("Tools/IronRequiem/Reports/T07Optimization/runtime.json",
                "{\"success\":true,\"advancedFrames\":" + (Time.frameCount-frame) +
                ",\"navMesh\":true,\"lodForceCalls\":true,\"detachIdempotent\":true,\"equippedAllWorldLodsHidden\":true," +
                "\"energyBinding\":true,\"criticalSmoke\":true,\"throwRestoresVisibility\":true,\"explosionIdempotent\":true," +
                "\"physicalInputTested\":false,\"fpsBenchmark\":false}");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("T07OptimizationCleanup"));
            if (old.IsValid() && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
