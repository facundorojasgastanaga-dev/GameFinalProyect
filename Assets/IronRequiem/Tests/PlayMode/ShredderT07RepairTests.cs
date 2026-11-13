using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    public sealed class ShredderT07RepairTests
    {
        const string Report = "Tools/IronRequiem/Reports/ShredderT07Repair";
        GameObject actor; Camera camera; PlayerCharacterController player; bool background; float capture;
        readonly List<Object> scratch = new List<Object>();
        [UnitySetUp] public IEnumerator Setup()
        {
            background = Application.runInBackground; capture = Time.captureDeltaTime; Application.runInBackground = true; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1; AudioListener.pause = false;
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/ChaseCorridorLab.unity", new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); player.GetComponent<Health>().Invincible = true; Object.FindAnyObjectByType<CombatLabMenu>().BeginGame(); Object.FindAnyObjectByType<ChaseCorridorEncounter>().enabled = false;
            yield return new WaitForSeconds(.3f); player.TeleportToCheckpoint(new Vector3(0, .03f, 20)); player.enabled = false; Physics.SyncTransforms();
            camera = new GameObject("RobotInspectionCamera").AddComponent<Camera>(); camera.enabled = false; camera.fieldOfView = 35; camera.nearClipPlane = .05f;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; AudioListener.pause = false; Time.captureDeltaTime = capture; Cursor.lockState = CursorLockMode.None;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("T07RepairCleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene); foreach (var item in scratch) if (item) Object.Destroy(item); scratch.Clear(); Application.runInBackground = background;
        }
        void Capture(string name, Vector3? position = null, Vector3? target = null)
        {
            camera.transform.position = position ?? actor.transform.position + new Vector3(2.6f, 1.4f, -5.5f); camera.transform.LookAt(target ?? actor.transform.position + Vector3.up);
            var rt = new RenderTexture(640, 720, 24); var image = new Texture2D(640, 720, TextureFormat.RGB24, false); var old = RenderTexture.active;
            try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 640, 720), 0, 0); image.Apply(); File.WriteAllBytes(Report + "/" + name + ".png", image.EncodeToPNG()); }
            finally { camera.targetTexture = null; RenderTexture.active = old; Object.Destroy(rt); Object.Destroy(image); }
        }
        void Describe(StringBuilder log)
        {
            log.AppendLine("time=" + Time.time + " root=" + actor.transform.position);
            foreach (var t in actor.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("Thigh") || t.name.Contains("Shin") || t.name.Contains("Foot"))) log.AppendLine(t.name + " p=" + actor.transform.InverseTransformPoint(t.position) + " rot=" + t.eulerAngles + " scale=" + t.lossyScale);
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.name.StartsWith("Body_LOD")))
            {
                var mesh = new Mesh(); skin.BakeMesh(mesh, false); log.AppendLine(skin.name + " visible=" + skin.isVisible + " enabled=" + skin.enabled + " baked=" + mesh.bounds + " source=" + skin.sharedMesh.bounds); Object.Destroy(mesh);
            }
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) log.AppendLine("collider=" + collider.name + " layer=" + collider.gameObject.layer + " trigger=" + collider.isTrigger + " bounds=" + collider.bounds);
        }
        [UnityTest] public IEnumerator BaselineRobotLegAndCharge()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Enemies/T07_HitFeedback_v04.prefab"); actor = Object.Instantiate(prefab, new Vector3(0, .06f, 34), Quaternion.Euler(0, 180, 0)); yield return null; yield return null;
            var enemy = actor.GetComponent<EnemyController>(); int shots = 0; enemy.GetCurrentWeapon().OnShootProcessed += () => shots++;
            var log = new StringBuilder(); log.AppendLine("source=" + AssetDatabase.GetAssetPath(prefab) + " charge=" + enemy.EnemyChargeDuration); Describe(log);
            var body = actor.GetComponentsInChildren<LODGroup>().Single(g => g.name == "Body");
            for (int i = 0; i < 3; i++) { body.ForceLOD(i); yield return new WaitForSeconds(.3f); Capture("baseline-lod" + i); Describe(log); }
            yield return new WaitForSeconds(1); Describe(log); log.AppendLine("shots=" + shots); File.WriteAllText(Report + "/baseline-runtime.txt", log.ToString());
            var rig = actor.GetComponentsInChildren<MonoBehaviour>().Single(r => r.GetType().Name == "Rig" && r.name == "FootPlacementRig"); var rigData = new SerializedObject(rig); rigData.FindProperty("m_Weight").floatValue = 0; rigData.ApplyModifiedPropertiesWithoutUndo(); yield return new WaitForSeconds(.3f); body.ForceLOD(0); yield return null; Capture("diagnostic-no-foot-ik");
            rigData.Update(); rigData.FindProperty("m_Weight").floatValue = 1; rigData.ApplyModifiedPropertiesWithoutUndo(); yield return new WaitForSeconds(.3f);
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.name.StartsWith("Body_LOD")))
            {
                var original = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.name == skin.name); var copy = Object.Instantiate(skin.sharedMesh); scratch.Add(copy); copy.bindposes = original.bones.Select(b => b.worldToLocalMatrix * original.transform.localToWorldMatrix).ToArray(); skin.sharedMesh = copy;
            }
            yield return new WaitForSeconds(.3f); Capture("diagnostic-rebound");
            Object.Destroy(actor); yield return null;
            var holder = new GameObject("DiagnosticInactive"); holder.SetActive(false); actor = Object.Instantiate(prefab, holder.transform); actor.transform.SetPositionAndRotation(new Vector3(0, .06f, 34), Quaternion.Euler(0, 180, 0));
            var animator = actor.GetComponent<T07AnimationDriver>().Animator; var overrides = new AnimatorOverrideController(animator.runtimeAnimatorController); scratch.Add(overrides); var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); overrides.GetOverrides(pairs);
            int removed = 0;
            for (int i = 0; i < pairs.Count; i++)
            {
                var original = pairs[i].Key; if (original.name.Contains("Death")) continue; var copy = Object.Instantiate(original); scratch.Add(copy);
                foreach (var b in AnimationUtility.GetCurveBindings(copy)) if ((b.path.Contains("/Thigh_") || b.path.Contains("/Shin_") || b.path.Contains("/Foot_")) && b.propertyName.Contains("Rotation")) { AnimationUtility.SetEditorCurve(copy, b, null); removed++; }
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, copy);
            }
            overrides.ApplyOverrides(pairs); animator.runtimeAnimatorController = overrides; actor.transform.SetParent(null, true); Object.Destroy(holder); yield return new WaitForSeconds(.4f); Capture("diagnostic-ik-owns-legs"); yield return new WaitForSeconds(.7f); Capture("diagnostic-ik-owns-legs-walk"); File.WriteAllText(Report + "/leg-stream-probe.txt", "Removed conflicting local leg rotation curves=" + removed);
            var materials = new Dictionary<Material, Material>(); foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.sharedMaterials = skin.sharedMaterials.Select(m => { if (!materials.TryGetValue(m, out var copy)) { copy = new Material(m); if (copy.HasProperty("_Cull")) copy.SetFloat("_Cull", 0); materials[m] = copy; scratch.Add(copy); } return copy; }).ToArray(); yield return new WaitForSeconds(.2f); Capture("diagnostic-two-sided");
        }
        const string Fixed = "Assets/IronRequiem/Prefabs/Enemies/T07_LegRepair_Charged_v05.prefab";
        IEnumerator SpawnFixed(float z)
        {
            actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Fixed), new Vector3(0, .06f, z), Quaternion.Euler(0, 180, 0)); yield return null; yield return null;
        }
        [UnityTest] public IEnumerator RepairedLegInteriorsPreserveAllLodsAndDamageStages()
        {
            yield return SpawnFixed(34); var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Enemies/T07_HitFeedback_v04.prefab"); var body = actor.GetComponentsInChildren<LODGroup>().Single(g => g.name == "Body"); int[] added = { 69, 44, 23 };
            Assert.That(actor.GetComponent<Health>().MaxHealth, Is.EqualTo(original.GetComponent<Health>().MaxHealth));
            for (int level = 0; level < 3; level++)
            {
                var skin = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.name == "Body_LOD" + level); var before = original.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.name == skin.name);
                Assert.That(skin.sharedMesh.triangles.Length / 3, Is.EqualTo(before.sharedMesh.triangles.Length / 3 + added[level])); Assert.That(skin.bones.Length, Is.EqualTo(17)); Assert.That(skin.bones.All(b => b), Is.True); Assert.That(skin.sharedMesh.bindposes.Length, Is.EqualTo(17)); Assert.That(skin.sharedMesh.uv.Length, Is.EqualTo(skin.sharedMesh.vertexCount));
                var weights = skin.sharedMesh.boneWeights; foreach (string side in new[] { "L", "R" }) { int index = System.Array.FindIndex(skin.bones, b => b.name == "Foot_" + side); Assert.That(weights.Count(w => w.boneIndex0 == index && w.weight0 > .1f), Is.GreaterThan(50)); }
                body.ForceLOD(level); yield return new WaitForSeconds(.35f); Capture("fixed-leg-lod" + level); Assert.That(skin.gameObject.activeInHierarchy, Is.True);
            }
            var hp = actor.GetComponent<Health>(); var progression = actor.GetComponent<RobotDamageProgression>(); body.ForceLOD(0);
            foreach (float ratio in new[] { .7f, .45f, .2f }) { hp.TakeDamage(hp.CurrentHealth - hp.MaxHealth * ratio, player.gameObject); yield return new WaitForSeconds(.2f); Capture("fixed-leg-health-" + Mathf.RoundToInt(ratio * 100)); foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.name.StartsWith("Body_LOD"))) Assert.That(skin.gameObject.activeInHierarchy, Is.True); }
            Assert.That(progression.CriticalArm.IsDetached, Is.True); Assert.That(progression.TransitionCount, Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator T07ReallyChargesThenCancelsAtWallsAndDeath()
        {
            yield return SpawnFixed(26); actor.GetComponent<EnemyMobile>().enabled = false; var enemy = actor.GetComponent<EnemyController>(); var agent = enemy.NavMeshAgent; if (agent.isOnNavMesh) agent.isStopped = true; yield return new WaitForSeconds(1);
            var gun = enemy.GetCurrentWeapon(); var visual = actor.GetComponent<EnemyCannonChargePresentation>(); int shots = 0; gun.OnShootProcessed += () => shots++;
            Assert.That(enemy.IsSeeingTarget, Is.True); Assert.That(enemy.EnemyChargeDuration, Is.EqualTo(1.2f)); var old = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Enemies/T07_HitFeedback_v04.prefab").GetComponentsInChildren<WeaponController>(true).First(); Assert.That(gun.ProjectilePrefab, Is.SameAs(old.ProjectilePrefab)); Assert.That(gun.DelayBetweenShots, Is.EqualTo(old.DelayBetweenShots));
            enemy.CancelEnemyCharge(); enemy.TryAtack(player.GetComponent<Actor>().AimPoint.position); float began = Time.time;
            while (Time.time - began < 1.05f) { enemy.TryAtack(player.GetComponent<Actor>().AimPoint.position); yield return null; }
            Assert.That(shots, Is.Zero); Assert.That(visual.IsVisible, Is.True); var effect = (GameObject)new SerializedObject(visual).FindProperty("effect").objectReferenceValue; Assert.That(Vector3.Distance(effect.transform.position, gun.WeaponMuzzle.position), Is.LessThan(.001f)); Capture("fixed-charge");
            while (shots == 0 && Time.time - began < 1.5f) { enemy.TryAtack(player.GetComponent<Actor>().AimPoint.position); yield return null; } Assert.That(shots, Is.EqualTo(1));
            yield return new WaitForSeconds(gun.DelayBetweenShots + .1f); for (int i = 0; i < 15; i++) { enemy.TryAtack(player.GetComponent<Actor>().AimPoint.position); yield return null; } Assert.That(visual.IsVisible, Is.True);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(0, 2, 23); wall.transform.localScale = new Vector3(12, 6, .5f); Physics.SyncTransforms(); for (int i = 0; i < 80; i++) { enemy.TryAtack(player.GetComponent<Actor>().AimPoint.position); yield return null; } Assert.That(shots, Is.EqualTo(1)); Assert.That(visual.IsVisible, Is.False);
            Object.Destroy(wall); yield return new WaitForSeconds(.3f); for (int i = 0; i < 15; i++) { enemy.TryAtack(player.GetComponent<Actor>().AimPoint.position); yield return null; } Assert.That(visual.IsVisible, Is.True); actor.GetComponent<Health>().Kill(); yield return null; yield return null; Assert.That(visual.IsVisible, Is.False); Assert.That(shots, Is.EqualTo(1));
            File.WriteAllText(Report + "/charge-proof.txt", "Native1.2s charge: no early shot; muzzle VFX; one accepted projectile. Wall blocks/cancels next charge; death cancels reacquired charge. Existing damage/projectile and shot cooldown unchanged.");
        }
        [UnityTest] public IEnumerator CrusherRollersAreHorizontalCounterRotatingAndPauseCleanly()
        {
            var chase = Object.FindAnyObjectByType<ChaseCorridorEncounter>(); chase.enabled = true; player.TeleportToCheckpoint(new Vector3(0, .03f, 10)); Physics.SyncTransforms(); yield return new WaitForSeconds(.2f); Assert.That(chase.Running, Is.True);
            var data = new SerializedObject(chase); var blades = data.FindProperty("blades"); Assert.That(blades.arraySize, Is.EqualTo(2)); Assert.That(data.FindProperty("counterRotateBlades").boolValue, Is.True); var a = (Transform)blades.GetArrayElementAtIndex(0).objectReferenceValue; var b = (Transform)blades.GetArrayElementAtIndex(1).objectReferenceValue;
            Assert.That(Mathf.Abs(Vector3.Dot(a.forward, Vector3.right)), Is.GreaterThan(.99f)); Assert.That(Mathf.Abs(Vector3.Dot(b.forward, Vector3.right)), Is.GreaterThan(.99f)); var upA = a.up; var upB = b.up; yield return new WaitForSeconds(.05f); Assert.That(Vector3.SignedAngle(upA, a.up, a.forward) * Vector3.SignedAngle(upB, b.up, b.forward), Is.LessThan(0));
            var qa = a.rotation; var qb = b.rotation; Time.timeScale = 0; yield return new WaitForSecondsRealtime(.1f); Assert.That(Quaternion.Angle(a.rotation, qa), Is.LessThan(.001f)); Assert.That(Quaternion.Angle(b.rotation, qb), Is.LessThan(.001f)); Time.timeScale = 1;
            actor = chase.SawCarriage.gameObject; camera.fieldOfView = 55; Capture("crusher", chase.SawCarriage.position + new Vector3(5, 3.4f, 13), chase.SawCarriage.position + Vector3.up * 3.4f);
        }
    }
}
