using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;
namespace IronRequiem.Tests
{
    public sealed class NativeWeaponVisualTests
    {
        const string Reports = "Tools/IronRequiem/Reports/NativeWeaponRepair";
        PlayerCharacterController player; PlayerWeaponsManager manager; Keyboard keyboard; Mouse mouse;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previous; float oldCapture;
        [UnitySetUp] public IEnumerator Setup()
        {
            oldCapture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1; Application.runInBackground = true;
            previous = InputSystem.actions.devices; keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); InputSystem.actions.devices = new InputDevice[] { keyboard, mouse }; keyboard.MakeCurrent(); mouse.MakeCurrent();
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/NativeWeaponVisualLab.unity", new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); manager = player.GetComponent<PlayerWeaponsManager>(); player.GetComponent<Health>().Invincible = true;
            // Isolate weapon presentation; enemies and their assets are not tested or modified by this task.
            foreach (var e in Object.FindObjectsByType<EnemyController>()) { e.enabled = false; var mobile = e.GetComponent<EnemyMobile>(); if (mobile) mobile.enabled = false; var bot = e.GetComponent<BigBotCombat>(); if (bot) bot.enabled = false; var turret = e.GetComponent<EnemyTurret>(); if (turret) turret.enabled = false; e.NavMeshAgent.isStopped = true; }
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.SetPositionAndRotation(new Vector3(-14, .03f, -11), Quaternion.identity); cc.enabled = true; player.CharacterVelocity = Vector3.zero; Physics.SyncTransforms();
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus(); Cursor.lockState = CursorLockMode.Locked; yield return new WaitForSeconds(.6f);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = oldCapture; Time.timeScale = 1; var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("NativeVisualCleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene); InputSystem.actions.devices = previous; InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); Cursor.lockState = CursorLockMode.None; yield return null;
        }
        static string PathOf(Transform t, Transform root) => t == root ? "" : PathOf(t.parent, root) + "/" + t.name;
        [UnityTest] public IEnumerator NativeCapsulesAndUnchangedGameplay()
        {
            var w = manager.GetActiveWeapon(); var current = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_Integrated_v04.prefab").GetComponent<WeaponController>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab"); var nativeSource = source.GetComponent<WeaponController>();
            var sourceMeshes = source.GetComponentsInChildren<MeshFilter>(true);
            foreach (var filter in sourceMeshes)
            {
                var actualFilter = w.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == filter.name);
                Assert.That(actualFilter.sharedMesh, Is.SameAs(filter.sharedMesh), "Exact native mesh, UVs and normals: " + filter.name);
                Assert.That(actualFilter.GetComponent<Renderer>().enabled, Is.True);
                Assert.That(actualFilter.GetComponent<Renderer>().sharedMaterials.All(m => AssetDatabase.GetAssetPath(m).StartsWith("Assets/IronRequiem/Art/Weapons/NativeRepair/")), Is.True);
            }
            Assert.That(sourceMeshes.Length, Is.EqualTo(7));
            foreach (var profileGuid in AssetDatabase.FindAssets("t:VolumeProfile"))
            {
                var path = AssetDatabase.GUIDToAssetPath(profileGuid); if (!path.StartsWith("Assets/")) continue;
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile.TryGet<MotionBlur>(out var blur)) { Assert.That(blur.active, Is.False, path); Assert.That(blur.intensity.value, Is.Zero, path); }
                if (profile.TryGet<DepthOfField>(out var depth)) { Assert.That(depth.active, Is.False, path); Assert.That(depth.mode.value, Is.EqualTo(DepthOfFieldMode.Off), path); }
            }
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.That(pipeline.renderScale, Is.EqualTo(1f));
            Assert.That(player.PlayerCamera.GetUniversalAdditionalCameraData().antialiasing, Is.EqualTo(AntialiasingMode.None));
            Assert.That(manager.WeaponCamera.GetUniversalAdditionalCameraData().antialiasing, Is.EqualTo(AntialiasingMode.None));
            Assert.That(player.PlayerCamera.allowDynamicResolution, Is.False);
            System.IO.File.WriteAllText(Reports + "/rendering.txt", "Exact seven native meshes with unchanged UVs/normals. All project MotionBlur disabled/intensity zero and DOF disabled/off; renderScale=" + pipeline.renderScale + "; main AA=" + player.PlayerCamera.GetUniversalAdditionalCameraData().antialiasing + "; weapon AA=" + manager.WeaponCamera.GetUniversalAdditionalCameraData().antialiasing);
            foreach (var field in typeof(WeaponController).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (new[] { "WeaponRoot", "WeaponMuzzle", "WeaponAnimator", "RecoilForce", "OnShoot" }.Contains(field.Name)) continue;
                Assert.That(field.GetValue(w), Is.EqualTo(field.GetValue(current)), "Preserve current field " + field.Name);
            }
            Assert.That(w.RecoilForce, Is.EqualTo(nativeSource.RecoilForce)); Assert.That(w.WeaponAnimator, Is.Null); Assert.That(w.GetComponentsInChildren<Animator>(true), Is.Empty);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_NativeVisual_v06.prefab"); Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(asset)), Is.EqualTo(AssetDatabase.GetAssetPath(source)), "Direct original prefab variant");
            var reference = Object.Instantiate(source, new Vector3(1000, -1000, 1000), Quaternion.identity); var rw = reference.GetComponent<WeaponController>(); rw.Owner = player.gameObject; rw.AmmoReloadRate = 0; reference.GetComponent<OverheatBehavior>().CoolingCellsSound = null; rw.ShowWeapon(false); yield return null;
            var actual = w.GetComponent<WeaponFuelCellHandler>(); var expected = reference.GetComponent<WeaponFuelCellHandler>(); float reload = w.AmmoReloadRate; w.AmmoReloadRate = 0;
            Assert.That(actual.SimultaneousFuelCellsUsage, Is.EqualTo(expected.SimultaneousFuelCellsUsage)); Assert.That(actual.FuelCellUsedPosition, Is.EqualTo(expected.FuelCellUsedPosition)); Assert.That(actual.FuelCellUnusedPosition, Is.EqualTo(expected.FuelCellUnusedPosition));
            float peak = 0;
            foreach (float ratio in new[] { 1f, .95f, .8f, .7f, .55f, .4f, .2f, 0f, .35f, .8f, 1f })
            {
                w.SetAmmo(ratio * w.MaxAmmo); rw.SetAmmo(ratio * rw.MaxAmmo); yield return null; yield return new WaitForEndOfFrame();
                for (int i = 0; i < 4; i++)
                {
                    var a = actual.FuelCells[i].transform; var b = expected.FuelCells[i].transform;
                    Assert.That(a.gameObject.activeInHierarchy && a.GetComponent<Renderer>().enabled, Is.True);
                    Assert.That(PathOf(a, w.transform), Is.EqualTo(PathOf(b, reference.transform)));
                    Assert.That(Vector3.Distance(a.localPosition, b.localPosition), Is.LessThan(.000001f)); Assert.That(Quaternion.Angle(a.localRotation, b.localRotation), Is.LessThan(.001f)); Assert.That(a.localScale, Is.EqualTo(b.localScale));
                    Assert.That(a.parent.localPosition, Is.EqualTo(b.parent.localPosition)); Assert.That(a.parent.localRotation, Is.EqualTo(b.parent.localRotation)); Assert.That(a.parent.localScale, Is.EqualTo(b.parent.localScale));
                    peak = Mathf.Max(peak, a.localPosition.y);
                }
                if (ratio == 0) { Capture(player.PlayerCamera, "capsules-extended"); JointCapture(w, "joint-extended"); }
            }
            Assert.That(peak, Is.EqualTo(.1f).Within(.000001f)); w.AmmoReloadRate = reload; Object.Destroy(reference); w.SetAmmo(w.MaxAmmo);
            System.IO.File.WriteAllText(Reports + "/native-parity.txt", "11 ammo fractions, 44 capsule poses matched original transforms/controller at <0.000001 m. Visible cells remain native FuelCells. No Animator in original. Original visual recoil=" + w.RecoilForce + "; current public gameplay fields unchanged, same projectile reference.");
        }
        void JointCapture(WeaponController w, string name)
        {
            var attachment = w.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Attachment");
            var close = new GameObject("JointCapture").AddComponent<Camera>(); close.cullingMask = manager.WeaponCamera.cullingMask; close.clearFlags = CameraClearFlags.SolidColor; close.backgroundColor = new Color(.08f, .1f, .13f); close.nearClipPlane = .005f; close.fieldOfView = 40; close.transform.position = attachment.position - w.WeaponMuzzle.forward * .32f + w.WeaponMuzzle.right * .32f + w.WeaponMuzzle.up * .12f; close.transform.LookAt(attachment.position + w.WeaponMuzzle.forward * .10f); Capture(close, name, 1280, 960); Object.Destroy(close.gameObject);
        }
        void Capture(Camera camera, string name, int width = 1280, int height = 720)
        {
            var rt = new RenderTexture(width, height, 24); var tex = new Texture2D(width, height, TextureFormat.RGB24, false); var old = RenderTexture.active; camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply(); System.IO.File.WriteAllBytes(Reports + "/" + name + ".png", tex.EncodeToPNG()); camera.targetTexture = null; RenderTexture.active = old; Object.Destroy(rt); Object.Destroy(tex);
        }
        [UnityTest] public IEnumerator OriginalMotionAndFraming()
        {
            var w = manager.GetActiveWeapon(); var hands = player.GetComponent<ProtagonistHandsPresentation>(); var visual = w.GetComponentsInChildren<Transform>(true).Single(t => t.name == "PrimaryModuleVisual"); var muzzle = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "Muzzle"); var attachment = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "Attachment"); var wrist = hands.FpsRoot.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Hand_R");
            var arm = hands.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name == "Forearm_R_FPS"); var forearm = hands.FpsRoot.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Forearm_R"); float length = Vector3.Distance(wrist.position, forearm.position); var mesh = new Mesh(); arm.BakeMesh(mesh, false); var scale = arm.transform.lossyScale;
            var projection = mesh.vertices.Select(v => Vector3.Dot(arm.transform.TransformPoint(new Vector3(v.x / scale.x, v.y / scale.y, v.z / scale.z)) - forearm.position, w.WeaponMuzzle.forward)).ToArray(); float min = projection.Min(); var cap = Enumerable.Range(0, projection.Length).Where(i => projection[i] < min + .014f).ToArray(); int cuts = 0, frames = 0, shots = 0; float recoil = 0; w.OnShoot += () => shots++; var start = player.transform.position;
            var recoilField = typeof(PlayerWeaponsManager).GetField("m_WeaponRecoilLocalPosition", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int phase = 0; phase < 6; phase++)
            {
                bool moving = phase == 1 || phase == 2 || phase == 5, aiming = phase == 2 || phase == 4, firing = phase >= 3;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(moving ? Key.W : Key.None)); InputSystem.QueueStateEvent(mouse, new MouseState { buttons = (ushort)((aiming ? 2 : 0) | (firing ? 1 : 0)) });
                float end = Time.time + .65f; int before = shots;
                while (Time.time < end)
                {
                    yield return new WaitForEndOfFrame(); frames++; Assert.That(Vector3.Distance(muzzle.position, w.WeaponMuzzle.position), Is.LessThan(.0001f)); Assert.That(Vector3.Angle(muzzle.forward, w.WeaponMuzzle.forward), Is.LessThan(.1f)); Assert.That(Vector3.Distance(wrist.position, attachment.position + w.WeaponMuzzle.forward * .003f), Is.LessThan(.002f)); Assert.That(Vector3.Distance(forearm.position, wrist.position), Is.EqualTo(length).Within(.001f)); Assert.That(hands.RightHandVisible, Is.False); Assert.That(hands.LeftHandVisible, Is.False);
                    recoil = Mathf.Max(recoil, -((Vector3)recoilField.GetValue(manager)).z);
                    arm.BakeMesh(mesh, false); scale = arm.transform.lossyScale; var vertices = mesh.vertices;
                    foreach (int i in cap) { var v = manager.WeaponCamera.WorldToViewportPoint(arm.transform.TransformPoint(new Vector3(vertices[i].x / scale.x, vertices[i].y / scale.y, vertices[i].z / scale.z))); if (v.z > manager.WeaponCamera.nearClipPlane && v.x >= 0 && v.x <= 1 && v.y >= 0 && v.y <= 1) cuts++; }
                }
                if (aiming) Assert.That(manager.IsAiming, Is.True); if (firing) Assert.That(shots - before, Is.GreaterThan(3)); Capture(player.PlayerCamera, "phase-" + phase); InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(mouse, new MouseState()); yield return new WaitForSeconds(.8f);
            Assert.That(Vector3.Distance(start, player.transform.position), Is.GreaterThan(.2f)); Assert.That(recoil, Is.GreaterThan(.05f)); Assert.That(cuts, Is.Zero, "No arm proximal cut in FPS camera"); Assert.That(w.GetComponent<WeaponFuelCellHandler>().FuelCells.All(c => c.transform.localPosition.sqrMagnitude < .00001f), Is.True, "Native recharge returns capsules");
            var close = new GameObject("NativeJointCapture").AddComponent<Camera>(); close.cullingMask = manager.WeaponCamera.cullingMask; close.clearFlags = CameraClearFlags.SolidColor; close.backgroundColor = new Color(.08f, .1f, .13f); close.nearClipPlane = .005f; close.fieldOfView = 40; close.transform.position = attachment.position - w.WeaponMuzzle.forward * .32f + w.WeaponMuzzle.right * .32f + w.WeaponMuzzle.up * .12f; close.transform.LookAt(attachment.position + w.WeaponMuzzle.forward * .10f); Capture(close, "joint-detail", 1280, 960); Object.Destroy(close.gameObject); Object.Destroy(mesh);
            var prefab = w.SourcePrefab.GetComponent<WeaponController>(); manager.RemoveWeapon(w); yield return new WaitForSeconds(.5f); Assert.That(hands.RightHandVisible, Is.True); Assert.That(manager.AddWeapon(prefab), Is.True); manager.SwitchToWeaponIndex(0, true); yield return new WaitForSeconds(.6f); Assert.That(hands.RightHandVisible, Is.False); Assert.That(manager.GetActiveWeapon().IsWeaponActive, Is.True);
            System.IO.File.WriteAllText(Reports + "/motion.txt", "Actual keyboard/mouse: idle, walking, walking ADS, automatic fire/recoil, ADS fire, walking fire, unequip and re-equip; " + frames + " aligned frames; " + cuts + " visible proximal cut vertices; " + shots + " shots; peak native recoil=" + recoil + "; arm length=" + length + ". Separate lab only. No melee/enemy/performance tests.");
        }
    }
}
