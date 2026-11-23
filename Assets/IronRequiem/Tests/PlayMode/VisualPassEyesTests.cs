using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    /// Visual pass F3: enemy eyes (EnemyEyeGlow). Same session loading pattern as VisualPassF0: the real Map01 session,
    /// flow disabled, GameFlowManager disabled, time frozen. Enemies are instantiated near the main camera with the
    /// equivalent of VisualPassF0.Prepare (SelfDestructYHeight very low, agents off). Nothing is saved.
    public sealed class VisualPassEyesTests
    {
        const string Session = "Assets/IronRequiem/Scenes/Map01_Bunker/Map01_Campaign_S1.unity";
        const string Prefabs = "Assets/IronRequiem/Prefabs/Campaign/";
        static readonly string[] Enemies = { "T07_Current_Repaired", "T07_Mech", "SawBot_Current", "BigBot_Current", "HoverBot_Current", "Turret_Current", "Carronero" };
        static string[] EnemyNames() => Enemies;

        bool background; Scene sessionScene; Camera cam; EnemyEyeGlow glow;

        [UnitySetUp] public IEnumerator Setup()
        {
            LogAssert.ignoreFailingMessages = true;
            background = Application.runInBackground; Application.runInBackground = true; Time.timeScale = 1;
            EditorSceneManager.LoadSceneInPlayMode(Session, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            var flow = Object.FindAnyObjectByType<CampaignFlow>(); Assert.That(flow, Is.Not.Null);
            float end = Time.realtimeSinceStartup + 90; while ((flow.Busy || flow.CurrentStage != 0) && Time.realtimeSinceStartup < end) yield return null;
            flow.Menu.BeginGame(); flow.Player.GetComponent<Health>().Invincible = true; yield return null;
            var intro = Object.FindAnyObjectByType<HangarIntroCinematic>();
            if (intro) { end = Time.realtimeSinceStartup + 10; while (!intro.IsPlaying && !intro.Finished && Time.realtimeSinceStartup < end) yield return null; intro.RequestSkip(); }
            end = Time.realtimeSinceStartup + 15; while (PlayerInputLock.Locked && Time.realtimeSinceStartup < end) yield return null;
            yield return flow.DrainPendingLoadsForShutdown(); flow.enabled = false;
            foreach (var m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) if (m && m.GetType().Name == "GameFlowManager") m.enabled = false; // a kill must not end the session
            Time.timeScale = 0;
            sessionScene = flow.gameObject.scene;
            foreach (var s in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s != sessionScene && s.isLoaded && s.name != "DontDestroyOnLoad" && !s.name.StartsWith("InitTestScene")).ToArray())
                yield return SceneManager.UnloadSceneAsync(s);
            flow.Player.gameObject.SetActive(true);
            yield return null;
            cam = Camera.main; glow = EnemyEyeGlow.Ensure(); EnemyEyeGlow.Enabled = true;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; Application.runInBackground = background; EnemyEyeGlow.Enabled = true;
            var clean = SceneManager.CreateScene("VisualPassEyesCleanup" + Time.frameCount); SceneManager.SetActiveScene(clean);
            var session = Object.FindAnyObjectByType<CampaignFlow>(); if (session) { session.enabled = true; yield return session.DrainPendingLoadsForShutdown(); }
            foreach (var queue in Object.FindObjectsByType<EnemySpawnQueue>()) { queue.CancelPending(); queue.enabled = false; }
            var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s != clean && s.isLoaded).OrderBy(s => s == sessionScene ? 1 : 0).ToArray();
            foreach (var s in loaded) if (s.isLoaded) yield return SceneManager.UnloadSceneAsync(s);
            Cursor.lockState = CursorLockMode.None; LogAssert.ignoreFailingMessages = false;
        }

        /// Equivalent of VisualPassF0.Prepare: agents off (nothing bakes a NavMesh here), no self-destruction below y -20
        /// and no health bar facing a camera that does not exist in the test.
        static void Prepare(GameObject enemy)
        {
            foreach (var agent in enemy.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
            foreach (var ec in enemy.GetComponentsInChildren<EnemyController>(true)) ec.SelfDestructYHeight = -100000;
            foreach (var bar in enemy.GetComponentsInChildren<Unity.FPS.UI.WorldspaceHealthBar>(true)) { if (bar.HealthBarPivot) bar.HealthBarPivot.gameObject.SetActive(false); bar.enabled = false; }
        }

        GameObject Spawn(string name, float distance, float sideways = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab");
            Assume.That(prefab, Is.Not.Null, "prefab " + name);
            Assert.That(cam, Is.Not.Null, "Camera.main of the session");
            var t = cam.transform; var p = t.position + Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized * distance + t.right * sideways - Vector3.up * 1.2f;
            var go = Object.Instantiate(prefab, p, Quaternion.LookRotation(-Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized, Vector3.up));
            Prepare(go); return go;
        }

        IEnumerator Settle() { yield return null; yield return null; glow.ApplyNow(); yield return null; }

        static bool IsRed(Color c) => c.r > .01f && c.r > c.g * 4f && c.r > c.b * 4f;

        [UnityTest, Timeout(120000)] public IEnumerator Eyes_RedInThreeStates_IntensityGrows([ValueSource(nameof(EnemyNames))] string name)
        {
            LogAssert.ignoreFailingMessages = true;
            var go = Spawn(name, 6f); yield return Settle();
            var enemy = go.GetComponent<EnemyController>();
            Assert.That(glow.TryGetEye(enemy, out _), Is.True, name + ": the eye must be managed");
            float last = 0; var order = new[] { EnemyEyeState.Patrol, EnemyEyeState.Alert, EnemyEyeState.Attack };
            foreach (var state in order)
            {
                glow.ForceState(enemy, state); glow.ApplyNow(); yield return null;
                Assert.That(glow.TryGetEye(enemy, out var eye), Is.True);
                Assert.That(IsRed(eye.Color), Is.True, name + " " + state + " colour " + eye.Color);
                Assert.That(eye.Hdr, Is.GreaterThan(last), name + " " + state + " must be brighter than the previous state");
                Assert.That(eye.Hdr, Is.LessThanOrEqualTo(EnemyEyeStyle.MaxHdr + 1e-4f));
                Assert.That(eye.Drawn, Is.True);
                last = eye.Hdr;
            }
            Object.Destroy(go);
        }

        [UnityTest, Timeout(120000)] public IEnumerator Eyes_NaturalStateIsPatrolWithoutTarget()
        {
            LogAssert.ignoreFailingMessages = true;
            var go = Spawn("T07_Current_Repaired", 400f); yield return Settle();
            var enemy = go.GetComponent<EnemyController>();
            Assert.That(glow.TryGetEye(enemy, out var eye), Is.True);
            Assert.That(eye.State, Is.EqualTo(EnemyEyeState.Patrol));
            Assert.That(eye.Hdr, Is.EqualTo(EnemyEyeStyle.PatrolHdr).Within(1e-3f));
            Object.Destroy(go);
        }

        [UnityTest, Timeout(120000)] public IEnumerator Eyes_OffWhenTheEnemyDies()
        {
            LogAssert.ignoreFailingMessages = true;
            var go = Spawn("T07_Current_Repaired", 6f); yield return Settle();
            var enemy = go.GetComponent<EnemyController>(); var health = go.GetComponent<Health>();
            Assert.That(glow.TryGetEye(enemy, out _), Is.True);
            int before = glow.DrawnParticles; Assert.That(before, Is.GreaterThan(0));
            health.Kill(); yield return null;
            Assert.That(glow.TryGetEye(enemy, out _), Is.False, "no eye after death");
            Assert.That(glow.DrawnParticles, Is.LessThan(before), "the eye particles are gone in the next frame");
        }

        [UnityTest, Timeout(120000)] public IEnumerator Eyes_SizesNeverExceedTheCaps([ValueSource(nameof(EnemyNames))] string name)
        {
            LogAssert.ignoreFailingMessages = true;
            foreach (float d in new[] { .5f, 1.5f, 5f, 25f, 120f })
            {
                var go = Spawn(name, d); yield return Settle();
                var enemy = go.GetComponent<EnemyController>();
                foreach (var state in new[] { EnemyEyeState.Patrol, EnemyEyeState.Alert, EnemyEyeState.Attack, EnemyEyeState.Warning })
                {
                    glow.ForceState(enemy, state); glow.ApplyNow(); yield return null;
                    Assert.That(glow.TryGetEye(enemy, out var eye), Is.True, name + " at " + d + " m");
                    Assert.That(float.IsNaN(eye.CoreSize) || float.IsNaN(eye.HaloSize) || float.IsInfinity(eye.CoreSize) || float.IsInfinity(eye.HaloSize), Is.False);
                    Assert.That(eye.CoreSize, Is.LessThanOrEqualTo(EnemyEyeStyle.CoreCap(eye.Size) + 1e-4f), name + " core at " + d + " m " + state);
                    Assert.That(eye.HaloSize, Is.LessThanOrEqualTo(EnemyEyeStyle.HaloCap(eye.Size) + 1e-4f), name + " halo at " + d + " m " + state);
                    Assert.That(EnemyEyeStyle.IsFinite(eye.Position), Is.True);
                    Assert.That(Mathf.Max(eye.Color.r, eye.Color.g, eye.Color.b), Is.LessThanOrEqualTo(EnemyEyeStyle.MaxHdr + 1e-4f));
                }
                Object.Destroy(go); yield return null;
            }
        }

        [UnityTest, Timeout(120000)] public IEnumerator Eyes_AnchoredOnTheBody([ValueSource(nameof(EnemyNames))] string name)
        {
            LogAssert.ignoreFailingMessages = true;
            var go = Spawn(name, 6f); yield return Settle();
            var enemy = go.GetComponent<EnemyController>();
            Assert.That(glow.TryGetEye(enemy, out var eye), Is.True);
            var rs = go.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();
            Assume.That(rs.Length, Is.GreaterThan(0));
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            b.Expand(.3f); // the Carronero eye used to float about 0.75 m above its body
            Assert.That(b.Contains(eye.Position), Is.True, name + " eye " + eye.Position + " outside " + b);
            Object.Destroy(go);
        }

        [UnityTest, Timeout(120000)] public IEnumerator Eyes_ProtectedScavengerGetsNoEyes()
        {
            LogAssert.ignoreFailingMessages = true;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Carronero.prefab"); Assume.That(prefab, Is.Not.Null);
            var holder = new GameObject("EyesTest_InactiveHolder"); holder.SetActive(false); // Awake of the added component must not run
            var go = Object.Instantiate(prefab, holder.transform);
            go.AddComponent<ScavengerBehaviour>().enabled = false;
            go.transform.SetParent(null); Prepare(go);
            var t = cam.transform; go.transform.position = t.position + Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized * 6f - Vector3.up * 1.2f;
            yield return Settle();
            var enemy = go.GetComponent<EnemyController>();
            Assert.That(glow.IsExcluded(enemy), Is.True);
            Assert.That(glow.TryGetEye(enemy, out _), Is.False, "an enemy with ScavengerBehaviour must not receive eyes");
            Object.Destroy(go); Object.Destroy(holder);
        }

        [UnityTest, Timeout(120000)] public IEnumerator Eyes_OneRendererForManyEnemies()
        {
            LogAssert.ignoreFailingMessages = true;
            const int n = 8; var list = new GameObject[n]; var counts = new int[n];
            for (int i = 0; i < n; i++) { list[i] = Spawn(i % 2 == 0 ? "HoverBot_Current" : "Carronero", 6f + i, (i - n / 2) * .8f); counts[i] = list[i].GetComponentsInChildren<Renderer>(true).Length; }
            // other systems add their own renderers in Awake/Start (death sparks, health bar): count once those exist, with the eyes switched off
            EnemyEyeGlow.Enabled = false; yield return null; yield return null; yield return null;
            for (int i = 0; i < n; i++) counts[i] = list[i].GetComponentsInChildren<Renderer>(true).Length;
            EnemyEyeGlow.Enabled = true;
            yield return Settle();
            Assert.That(glow.GlowingCount, Is.GreaterThanOrEqualTo(n));
            Assert.That(glow.RendererCount, Is.EqualTo(1));
            Assert.That(glow.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(1), "the manager owns exactly one renderer");
            Assert.That(glow.GetComponentsInChildren<Light>(true).Length, Is.EqualTo(0), "no Light by default");
            Assert.That(glow.DrawnParticles, Is.GreaterThanOrEqualTo(n).And.LessThanOrEqualTo(3 * glow.GlowingCount));
            for (int i = 0; i < n; i++)
            {
                // Carronero: its floating CoreIndicator is hidden, nothing is added: the renderer count of every enemy is unchanged
                Assert.That(list[i].GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(counts[i]), "no renderer added to enemy " + i);
                Assert.That(list[i].GetComponentsInChildren<Light>(true).Count(l => l.name.Contains("Eye")), Is.EqualTo(0));
            }
            foreach (var g in list) Object.Destroy(g);
        }

        [UnityTest, Timeout(120000)] public IEnumerator Eyes_StateLightsAreSuspendedAndRestored()
        {
            LogAssert.ignoreFailingMessages = true;
            var go = Spawn("T07_Current_Repaired", 6f); yield return Settle();
            var esl = go.GetComponent<EnemyStateLights>(); Assume.That(esl, Is.Not.Null);
            var enemy = go.GetComponent<EnemyController>();
            Assert.That(glow.IsManaged(enemy), Is.True); Assert.That(esl.enabled, Is.False, "EnemyStateLights is suspended while the eye is managed");
            EnemyEyeGlow.Enabled = false; glow.ApplyNow(); yield return null;
            Assert.That(glow.IsManaged(enemy), Is.False); Assert.That(esl.enabled, Is.True, "and restored when the system is switched off");
            EnemyEyeGlow.Enabled = true; Object.Destroy(go);
        }
    }
}
