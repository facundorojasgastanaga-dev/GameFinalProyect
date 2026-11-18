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
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    // Weapons Redesign v01 (P-A, PR-C, C-A) in WeaponsRedesignLab (copy of MainScene_VisualLab). Virtual devices only.
    public sealed class WeaponsRedesignTests
    {
        const string Reports = "Tools/IronRequiem/Reports/WeaponsRedesign/Play";
        const string Lab = "Assets/IronRequiem/Scenes/WeaponsRedesignLab.unity";
        PlayerCharacterController player; PlayerWeaponsManager manager; ProtagonistHandsPresentation hands;
        StolenWeaponCarrier carrier; Keyboard keyboard; Mouse mouse;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        float previousCapture; bool previousBackground;

        [UnitySetUp] public IEnumerator Setup()
        {
            Directory.CreateDirectory(Reports); previousCapture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1;
            previousBackground = Application.runInBackground; Application.runInBackground = true;
            previousDevices = InputSystem.actions.devices; keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.actions.devices = new InputDevice[] { keyboard, mouse }; keyboard.MakeCurrent(); mouse.MakeCurrent();
            EditorSceneManager.LoadSceneInPlayMode(Lab, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); manager = player.GetComponent<PlayerWeaponsManager>();
            hands = player.GetComponent<ProtagonistHandsPresentation>(); carrier = player.GetComponent<StolenWeaponCarrier>();
            player.GetComponent<Health>().Invincible = true;
            var flow = Object.FindAnyObjectByType<GameFlowManager>(); if (flow) flow.enabled = false;
            var objectives = Object.FindAnyObjectByType<ObjectiveManager>(); if (objectives) objectives.enabled = false;
            foreach (var enemy in Object.FindObjectsByType<EnemyController>())
            {
                enemy.enabled = false; if (enemy.NavMeshAgent && enemy.NavMeshAgent.isOnNavMesh) { enemy.NavMeshAgent.ResetPath(); enemy.NavMeshAgent.isStopped = true; }
                foreach (var mobile in enemy.GetComponents<EnemyMobile>()) mobile.enabled = false;
                var turret = enemy.GetComponent<EnemyTurret>(); if (turret) turret.enabled = false;
            }
            Cursor.lockState = CursorLockMode.Locked; yield return new WaitForSeconds(1f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; Time.captureDeltaTime = previousCapture;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("WeaponsRedesignCleanup" + Time.frameCount));
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            InputSystem.actions.devices = previousDevices; if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
            Cursor.lockState = CursorLockMode.None; Application.runInBackground = previousBackground; yield return null;
        }

        IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        void Fire(bool held) => InputSystem.QueueStateEvent(mouse, new MouseState { buttons = (ushort)(held ? 1 : 0) });
        static Transform Named(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);

        void CheckModule(string variant, string visual)
        {
            var weapon = manager.GetActiveWeapon();
            Assert.That(weapon.SourcePrefab.name, Is.EqualTo(variant));
            var attachments = weapon.WeaponRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Attachment").ToArray();
            Assert.That(attachments.Length, Is.EqualTo(1), "One authoritative rear interface per weapon");
            Assert.That(Vector3.Distance(hands.RightModuleSocket.position, attachments[0].position + weapon.WeaponMuzzle.forward * .003f), Is.LessThan(.001f), "IR-ACOPLE-01 port");
            var vis = Named(weapon.transform, visual);
            var lod0 = vis.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name.EndsWith("_LOD0"));
            Assert.That(lod0.enabled && lod0.gameObject.activeInHierarchy, Is.True);
            var rear = Named(vis, "ModuleAttachment"); var mouth = Named(vis, "ModuleMuzzle");
            Assert.That(Vector3.Angle(mouth.position - rear.position, weapon.WeaponMuzzle.forward), Is.LessThan(1f));
            var view = manager.WeaponCamera.WorldToViewportPoint(lod0.bounds.center);
            Assert.That(view.z > 0 && view.x > -.2f && view.x < 1.2f && view.y > -.2f && view.y < 1.2f, Is.True, "Module visible to the weapon camera");
            Assert.That(weapon.WeaponRoot.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.enabled && !r.transform.IsChildOf(vis)), Is.Zero, "Old VO visual hidden");
        }

        void Capture(Camera camera, string name, int width = 1280, int height = 720)
        {
            var rt = new RenderTexture(width, height, 24); var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var active = RenderTexture.active; var target = camera.targetTexture;
            try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply(); File.WriteAllBytes(Reports + "/" + name + ".png", tex.EncodeToPNG()); }
            finally { camera.targetTexture = target; RenderTexture.active = active; Object.Destroy(rt); Object.Destroy(tex); }
        }
        void CaptureFps(string name)
        {
            // Main camera + weapon camera composited the same way the game does: render both into one target.
            var rt = new RenderTexture(1280, 720, 24); var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var cams = new[] { player.PlayerCamera, manager.WeaponCamera }; var targets = cams.Select(c => c.targetTexture).ToArray(); var active = RenderTexture.active;
            try { foreach (var c in cams) { c.targetTexture = rt; c.Render(); } RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply(); File.WriteAllBytes(Reports + "/" + name + ".png", tex.EncodeToPNG()); }
            finally { for (int i = 0; i < cams.Length; i++) cams[i].targetTexture = targets[i]; RenderTexture.active = active; Object.Destroy(rt); Object.Destroy(tex); }
        }
        void JointCapture(string name)
        {
            var weapon = manager.GetActiveWeapon(); var socket = hands.RightModuleSocket;
            var camera = new GameObject("RedesignInspectionCamera").AddComponent<Camera>(); camera.cullingMask = manager.WeaponCamera.cullingMask;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .10f, .13f); camera.nearClipPlane = .005f; camera.fieldOfView = 40;
            camera.transform.position = socket.position - weapon.WeaponMuzzle.forward * .34f + weapon.WeaponMuzzle.right * .30f + weapon.WeaponMuzzle.up * .13f;
            camera.transform.LookAt(socket.position + weapon.WeaponMuzzle.forward * .12f); Capture(camera, name, 1280, 960); Object.Destroy(camera.gameObject);
        }

        [UnityTest] public IEnumerator ArmCannonAndPrimaryRedesignMountByAttachment()
        {
            yield return new WaitForEndOfFrame();
            CheckModule("Weapon_IR_ArmCannon_Redesign_v01", "PistolMonolito_Visual"); CaptureFps("pistol-rest"); JointCapture("pistol-joint");
            var weapon = manager.GetActiveWeapon(); int shots = 0; weapon.OnShoot += () => shots++;
            Fire(true); yield return new WaitForSeconds(.8f); Fire(false); yield return new WaitForSeconds(.1f); CaptureFps("pistol-firing");
            Assert.That(shots, Is.GreaterThan(0), "Pistol fires from the new muzzle");
            int slot = manager.ActiveWeaponIndex == 0 ? 1 : 0;
            yield return Tap(slot == 1 ? Key.Digit2 : Key.Digit1); yield return new WaitForSeconds(1.4f); yield return new WaitForEndOfFrame();
            CheckModule("Weapon_Shotgun_ArmModule_Redesign_v01", "PrimaryEspina_Visual"); CaptureFps("primary-rest"); JointCapture("primary-joint");
            var primary = manager.GetActiveWeapon(); int pshots = 0; primary.OnShoot += () => pshots++;
            Fire(true); yield return new WaitForSeconds(.6f); Fire(false); yield return new WaitForSeconds(.3f);
            Assert.That(pshots, Is.GreaterThan(0), "Primary fires from the new muzzle");
            File.WriteAllText(Reports + "/modules.txt", $"pistol shots {shots}; primary shots {pshots}; single Attachment, port within 1 mm, axis < 1 deg, visible to weapon camera, old VO renderers hidden; switch via virtual key.");
        }

        [UnityTest] public IEnumerator T07RedesignCannonSeatsOnSocketAndCharges()
        {
            // Pair each detachable joint with the module it owns (serialized moduleVisual), active enemies only.
            StolenCannon module = null; DetachableCannon detach = null;
            foreach (var d in Object.FindObjectsByType<DetachableCannon>())
            {
                var visual = new UnityEditor.SerializedObject(d).FindProperty("moduleVisual").objectReferenceValue as Transform;
                var m = visual ? visual.GetComponent<StolenCannon>() : null;
                if (m && m.WeaponPrefab && m.WeaponPrefab.name == "Weapon_T07_Redesign_v01") { module = m; detach = d; break; }
            }
            Assert.That(module, Is.Not.Null, "Lab T-07 points to the redesign variant");
            detach.Detach(); yield return null; yield return null;
            Assert.That(module.Body, Is.Not.Null, "Detached module became an available world pickup");
            // Equip through the existing contract (pickup ray/occlusion is covered by ModularArmTests).
            player.enabled = false;
            module.Body.isKinematic = true;
            var cam = player.PlayerCamera.transform; module.transform.SetPositionAndRotation(cam.position + Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized * 1.4f + Vector3.down * 1.2f, Quaternion.identity); Physics.SyncTransforms();
            cam.LookAt(module.SolidCollider.bounds.center); yield return new WaitForSeconds(.4f);
            if (carrier.Candidate != module) Debug.LogWarning("Pickup candidate differs; status=" + carrier.GetPickupStatus(module));
            yield return Tap(Key.E); yield return new WaitForSeconds(1.2f);
            Assert.That(carrier.Held, Is.SameAs(module), "E pickup of the redesigned module");
            player.enabled = true; cam.localRotation = Quaternion.identity; yield return new WaitForSeconds(.6f); yield return new WaitForEndOfFrame();
            var weapon = manager.GetActiveWeapon(); Assert.That(weapon.SourcePrefab.name, Is.EqualTo("Weapon_T07_Redesign_v01"));
            var vis = Named(weapon.transform, "T07CannonCrudo_Visual"); var rear = Named(vis, "ModuleAttachment"); var mouth = Named(vis, "ModuleMuzzle");
            var socket = hands.RightModuleSocket;
            float seat = Vector3.Distance(rear.position, socket.position - socket.forward * .003f);
            Assert.That(seat, Is.LessThan(.003f), "Shell seated by its Attachment (IR-ACOPLE-01), not by its bounds");
            Assert.That(Vector3.Distance(weapon.WeaponMuzzle.position, mouth.position + vis.forward * .015f), Is.LessThan(.01f), "Muzzle on the module mouth");
            CaptureFps("cannon-rest"); JointCapture("cannon-joint");
            int shots = 0; var charge = weapon.GetComponent<CannonChargeShot>(); charge.ShotReleased += _ => shots++;
            var window = vis.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name.EndsWith("_LOD0")); var mpb = new MaterialPropertyBlock();
            Fire(true); yield return new WaitForSeconds(.9f); CaptureFps("cannon-charging");
            window.GetPropertyBlock(mpb, 2); var charged = mpb.GetColor("_EmissionColor");
            Assert.That(weapon.GetComponent<ChargeWindowGlow>(), Is.Not.Null, "Charge window glow present");
            Assert.That(charged.maxColorComponent, Is.GreaterThan(1f), "Charge window brightens while charging");
            Fire(false); yield return new WaitForSeconds(.3f);
            Assert.That(shots, Is.EqualTo(1), "Charged shot released once");
            File.WriteAllText(Reports + "/cannon.txt", $"seat error {seat * 1000:F2} mm; charged shots {shots}.");
        }
    }
}
