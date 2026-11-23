using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IronRequiem.Tests
{
    /// Chat 11 · option C (EnemyDarkReadability) with real enemy prefabs in a dark scene made by the test.
    /// Enemies run without a player (their own AI logs errors about the missing player / compass); those are not this
    /// feature's, so only errors raised from the vision code fail the test.
    public sealed class VisionReadabilityTests
    {
        const string T07 = "Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab";
        const string SawBot = "Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab";
        readonly List<string> visionErrors = new List<string>();
        void Capture(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception) && (stack + message).IndexOf("EnemyDarkReadability", System.StringComparison.Ordinal) >= 0) visionErrors.Add(message);
        }
        static GameObject Load(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
        }
        static float RimOf(EnemyController enemy, out bool found)
        {
            var block = new MaterialPropertyBlock(); found = false;
            foreach (var r in enemy.GetComponentsInChildren<Renderer>())
            {
                if (!VisionMaterialRules.IsBodyRenderer(r)) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (VisionMaterialRules.IsBodyMaterial(mats[i]) && mats[i].IsKeywordEnabled("_RIM"))
                    {
                        r.GetPropertyBlock(block, i); found = true;
                        if (block.isEmpty) return mats[i].GetColor(VisionMaterialRules.RimColorId).maxColorComponent;
                        var v = block.GetVector(VisionMaterialRules.RimColorId); return Mathf.Max(v.x, Mathf.Max(v.y, v.z));
                    }
            }
            return 0;
        }

        static float RenderMean(Camera cam)
        {
            var rt = RenderTexture.GetTemporary(320, 180, 24); var tex = new Texture2D(320, 180, TextureFormat.RGB24, false); var prev = RenderTexture.active;
            try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 320, 180), 0, 0); tex.Apply(); return tex.GetPixels32().Average(c => (c.r + c.g + c.b) / 3f); }
            finally { cam.targetTexture = null; RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); Object.Destroy(tex); }
        }

        /// Turning the highlight off (setting off, death, release) must give back exactly the original look.
        [UnityTest]
        public IEnumerator ReleaseRestoresTheOriginalLook()
        {
            LogAssert.ignoreFailingMessages = true; visionErrors.Clear(); Application.logMessageReceived += Capture;
            float intensityBefore = RequiemSettings.VisionIntensity; bool alwaysBefore = RequiemSettings.VisionOutlinesAlways; var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene("VisionReleaseTest");
            try
            {
                // sun-lit test scene: force full outlines so the on/off comparison really exercises the highlight
                SceneManager.SetActiveScene(scene); RequiemSettings.SetVisionIntensity(0); RequiemSettings.SetVisionOutlinesAlways(true);
                var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.2f; sun.transform.rotation = Quaternion.Euler(40, 150, 0);
                var t07 = Object.Instantiate(Load(T07), Vector3.zero, Quaternion.Euler(0, 180, 0)).GetComponent<EnemyController>();
                var saw = Object.Instantiate(Load(SawBot), new Vector3(2.2f, 0, 0), Quaternion.Euler(0, 180, 0)).GetComponent<EnemyController>();
                var cam = new GameObject("Cam").AddComponent<Camera>(); cam.enabled = false; cam.transform.position = new Vector3(1.1f, 1.3f, -5f); cam.transform.LookAt(new Vector3(1.1f, 1.1f, 0));
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
                if (!EnemyDarkReadability.Instance) new GameObject("EnemyDarkReadability").AddComponent<EnemyDarkReadability>();
                var vision = EnemyDarkReadability.Instance;
                for (int i = 0; i < 4; i++) yield return null;
                float original = RenderMean(cam);
                RequiemSettings.SetVisionIntensity(1); vision.Rescan(); for (int i = 0; i < 4; i++) yield return null;
                Assert.That(vision.IsHighlighted(t07) && vision.IsHighlighted(saw), Is.True, "both highlighted");
                float lit = RenderMean(cam);
                RequiemSettings.SetVisionIntensity(0); for (int i = 0; i < 3; i++) yield return null;
                Assert.That(vision.IsHighlighted(t07), Is.False, "released");
                float released = RenderMean(cam);
                Debug.Log($"[VisionTest] release look: original={original:0.00} highlighted={lit:0.00} released={released:0.00}");
                Assert.That(lit, Is.Not.EqualTo(original).Within(.05f), "the highlight changes the image");
                Assert.That(released, Is.EqualTo(original).Within(Mathf.Max(.15f, original * .01f)), "released image equals the original");
                // F4 review: a damage stage that swaps a material while highlighted must survive the release
                RequiemSettings.SetVisionIntensity(1); vision.Rescan(); for (int i = 0; i < 4; i++) yield return null;
                var sawBody = saw.GetComponentsInChildren<Renderer>().First(r => r.sharedMaterials.Any(m => m && m.name.EndsWith("_Vision")));
                var stage = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "TestDamageStage" };
                var mats = sawBody.sharedMaterials; int stageIndex = System.Array.FindIndex(mats, m => m && m.name.EndsWith("_Vision")); mats[stageIndex] = stage; sawBody.sharedMaterials = mats;
                // F4 review: a part that leaves the enemy (detached cannon → pickup) is released and no longer driven
                var part = t07.GetComponentsInChildren<Renderer>().First(r => VisionMaterialRules.IsBodyRenderer(r) && r.sharedMaterials.Any(m => VisionMaterialRules.IsBodyMaterial(m) && m.IsKeywordEnabled("_RIM")) && r.transform != t07.transform);
                int partIndex = System.Array.FindIndex(part.sharedMaterials, m => VisionMaterialRules.IsBodyMaterial(m) && m.IsKeywordEnabled("_RIM"));
                part.transform.SetParent(null, true); yield return null; yield return null;
                var pb = new MaterialPropertyBlock(); part.GetPropertyBlock(pb, partIndex);
                Assert.That(pb.isEmpty || pb.GetVector(VisionMaterialRules.RimColorId).x < .01f, Is.True, "detached part released");
                RequiemSettings.SetVisionIntensity(0); for (int i = 0; i < 3; i++) yield return null;
                Assert.That(sawBody.sharedMaterials[stageIndex], Is.SameAs(stage), "damage-stage material kept on release");
                Object.Destroy(part.gameObject); Object.Destroy(stage);
                mats = sawBody.sharedMaterials; mats[stageIndex] = Load(SawBot).GetComponentsInChildren<Renderer>(true).First(r => r.name == sawBody.name).sharedMaterials[stageIndex]; sawBody.sharedMaterials = mats;
                for (int i = 0; i < 2; i++) yield return null;
                original = RenderMean(cam); // the scene changed (part removed): new reference for the on/off cycle below
                // twice, as in a real fight (on/off/on/off)
                RequiemSettings.SetVisionIntensity(1); vision.Rescan(); for (int i = 0; i < 4; i++) yield return null;
                RequiemSettings.SetVisionIntensity(0); for (int i = 0; i < 3; i++) yield return null;
                Assert.That(RenderMean(cam), Is.EqualTo(original).Within(Mathf.Max(.15f, original * .01f)), "second release also exact");
            }
            finally { RequiemSettings.SetVisionIntensity(intensityBefore); RequiemSettings.SetVisionOutlinesAlways(alwaysBefore); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
            yield return SceneManager.UnloadSceneAsync(scene);
            Application.logMessageReceived -= Capture; LogAssert.ignoreFailingMessages = false;
            Assert.That(visionErrors, Is.Empty, "errors from EnemyDarkReadability: " + string.Join(" | ", visionErrors));
        }

        [UnityTest]
        public IEnumerator PassiveRimFollowsDarknessClearsOnDeathAndAddsNoLights()
        {
            LogAssert.ignoreFailingMessages = true; visionErrors.Clear(); Application.logMessageReceived += Capture;
            float intensityBefore = RequiemSettings.VisionIntensity; bool alwaysBefore = RequiemSettings.VisionOutlinesAlways;
            var ambientMode = RenderSettings.ambientMode; var ambient = RenderSettings.ambientLight; var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene("VisionReadabilityTest");
            try
            {
                RequiemSettings.SetVisionIntensity(1); RequiemSettings.SetVisionOutlinesAlways(false);
                SceneManager.SetActiveScene(scene);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.05f, .05f, .06f);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.localScale = Vector3.one * 6;
                var t07Prefab = Load(T07); var sawPrefab = Load(SawBot);
                Assert.That(t07Prefab && sawPrefab, Is.True, "campaign enemy prefabs");
                int lightsBefore = Object.FindObjectsByType<Light>().Count(l => l.isActiveAndEnabled);
                var t07 = Object.Instantiate(t07Prefab, new Vector3(0, 0, 0), Quaternion.identity).GetComponent<EnemyController>();
                var saw = Object.Instantiate(sawPrefab, new Vector3(8, 0, 0), Quaternion.identity).GetComponent<EnemyController>();
                if (!EnemyDarkReadability.Instance) new GameObject("EnemyDarkReadability").AddComponent<EnemyDarkReadability>();
                var vision = EnemyDarkReadability.Instance;
                for (int i = 0; i < 3; i++) yield return null;
                vision.Rescan();
                for (int i = 0; i < 6; i++) yield return null;

                // 1. dark surroundings → strong rim on the T07 (rim already in its material: MaterialPropertyBlock only)
                Assert.That(vision.IsHighlighted(t07), Is.True, "T07 tracked and highlighted");
                Assert.That(vision.DarknessOf(t07), Is.GreaterThan(.9f), "dark scene reads as dark");
                float darkRim = RimOf(t07, out bool found);
                Assert.That(found, Is.True, "T07 has a rim body slot");
                Assert.That(darkRim, Is.EqualTo(VisionMaterialRules.RimFor(1, 1).maxColorComponent).Within(.15f), "dark rim at full strength");
                // SawBot has no rim keyword: the live instance uses the _Vision variant
                Assert.That(saw.GetComponentsInChildren<Renderer>().Any(r => r.sharedMaterials.Any(m => m && m.name.EndsWith("_Vision"))), Is.True, "SawBot swapped to its vision variant");
                Assert.That(RimOf(saw, out _), Is.GreaterThan(VisionMaterialRules.RimFor(1, 1).maxColorComponent * .5f), "SawBot rim driven"); // pase visual 12 b1: rim at half intensity (was > 1)
                // the feature adds no Light: the room's light count / budget is unchanged
                Assert.That(Object.FindObjectsByType<Light>().Count(l => l.isActiveAndEnabled), Is.EqualTo(lightsBefore), "no lights added");

                // 2. lit surroundings → weaker rim (smoothed, no pop)
                var lamp = new GameObject("TestLamp").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 10; lamp.intensity = 60; lamp.transform.position = t07.transform.position + Vector3.up * 2.5f;
                vision.Rescan();
                float t = Time.time; while (Time.time - t < 1.2f) yield return null;
                float litRim = RimOf(t07, out _);
                Assert.That(vision.DarknessOf(t07), Is.LessThan(.3f), "lit enemy reads as lit");
                Assert.That(litRim, Is.LessThan(darkRim * .6f), "rim drops when the enemy is lit");
                Object.Destroy(lamp.gameObject);

                // 3. intensity 0 (setting off) → released: original materials and rim back
                RequiemSettings.SetVisionIntensity(0); yield return null; yield return null;
                Assert.That(saw.GetComponentsInChildren<Renderer>().Any(r => r.sharedMaterials.Any(m => m && m.name.EndsWith("_Vision"))), Is.False, "SawBot back to its own materials");
                Assert.That(vision.IsHighlighted(t07), Is.False, "off releases the highlight");
                RequiemSettings.SetVisionIntensity(1); vision.Rescan(); for (int i = 0; i < 4; i++) yield return null;
                Assert.That(vision.IsHighlighted(t07), Is.True, "on again");

                // 4. death → highlight removed immediately (pieces / corpse never keep it)
                var health = t07.GetComponent<Health>(); health.Invincible = false; health.Kill();
                yield return null; yield return null;
                Assert.That(vision.IsHighlighted(t07), Is.False, "dead enemy loses the highlight");
                float afterDeath = t07 ? RimOf(t07, out _) : 0;
                if (t07) Assert.That(afterDeath, Is.LessThan(.5f), "rim back to the material's faint value");
                Debug.Log($"[VisionTest] darkRim={darkRim:0.00} litRim={litRim:0.00} sawRim>1 ok, lights {lightsBefore}→{lightsBefore}, afterDeath={afterDeath:0.00}");
            }
            finally
            {
                RequiemSettings.SetVisionIntensity(intensityBefore); RequiemSettings.SetVisionOutlinesAlways(alwaysBefore);
                RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambient;
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
            // 5. scene change: unloading the scene with tracked enemies raises no vision errors
            yield return SceneManager.UnloadSceneAsync(scene);
            for (int i = 0; i < 4; i++) yield return null;
            Application.logMessageReceived -= Capture; LogAssert.ignoreFailingMessages = false;
            Assert.That(visionErrors, Is.Empty, "errors from EnemyDarkReadability: " + string.Join(" | ", visionErrors));
        }
    }
}
