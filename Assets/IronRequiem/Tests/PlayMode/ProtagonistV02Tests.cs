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
    // IR-09 v02 in ProtagonistV02Lab (copy of WeaponsRedesignLab with Player_ModularArm_ProtagonistV02). Virtual devices only.
    public sealed class ProtagonistV02Tests
    {
        const string Reports = "Tools/IronRequiem/Reports/ProtagonistV02/Play";
        const string Lab = "Assets/IronRequiem/Scenes/ProtagonistV02Lab.unity";
        PlayerCharacterController player; PlayerWeaponsManager manager; ProtagonistHandsPresentation hands; HandPoseController poses;
        Keyboard keyboard; Mouse mouse; UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        float previousCapture; bool previousBackground;

        [UnitySetUp] public IEnumerator Setup()
        {
            Directory.CreateDirectory(Reports); previousCapture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1;
            previousBackground = Application.runInBackground; Application.runInBackground = true;
            previousDevices = InputSystem.actions.devices; keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.actions.devices = new InputDevice[] { keyboard, mouse }; keyboard.MakeCurrent(); mouse.MakeCurrent();
            EditorSceneManager.LoadSceneInPlayMode(Lab, new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); manager = player.GetComponent<PlayerWeaponsManager>();
            hands = player.GetComponent<ProtagonistHandsPresentation>(); poses = player.GetComponent<HandPoseController>();
            player.GetComponent<Health>().Invincible = true;
            var flow = Object.FindAnyObjectByType<GameFlowManager>(); if (flow) flow.enabled = false;
            var objectives = Object.FindAnyObjectByType<ObjectiveManager>(); if (objectives) objectives.enabled = false;
            foreach (var enemy in Object.FindObjectsByType<EnemyController>())
            {
                enemy.enabled = false; if (enemy.NavMeshAgent && enemy.NavMeshAgent.isOnNavMesh) { enemy.NavMeshAgent.ResetPath(); enemy.NavMeshAgent.isStopped = true; }
                foreach (var mobile in enemy.GetComponents<EnemyMobile>()) mobile.enabled = false;
            }
            Cursor.lockState = CursorLockMode.Locked; yield return new WaitForSeconds(1f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; Time.captureDeltaTime = previousCapture;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("ProtagonistV02Cleanup" + Time.frameCount));
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            InputSystem.actions.devices = previousDevices; if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
            Cursor.lockState = CursorLockMode.None; Application.runInBackground = previousBackground; yield return null;
        }

        IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        static Transform Named(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);

        void CaptureFps(string name)
        {
            var rt = new RenderTexture(1280, 720, 24); var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var cams = new[] { player.PlayerCamera, manager.WeaponCamera }; var targets = cams.Select(c => c.targetTexture).ToArray(); var active = RenderTexture.active;
            try { foreach (var c in cams) { c.targetTexture = rt; c.Render(); } RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply(); File.WriteAllBytes(Reports + "/" + name + ".png", tex.EncodeToPNG()); }
            finally { for (int i = 0; i < cams.Length; i++) cams[i].targetTexture = targets[i]; RenderTexture.active = active; Object.Destroy(rt); Object.Destroy(tex); }
        }
        void CloseUp(Transform target, string name, Vector3 offset)
        {
            var cam = new GameObject("V02InspectionCamera").AddComponent<Camera>(); cam.cullingMask = manager.WeaponCamera.cullingMask | player.PlayerCamera.cullingMask;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.08f, .10f, .13f); cam.nearClipPlane = .005f; cam.fieldOfView = 35;
            cam.transform.position = target.position + player.PlayerCamera.transform.TransformDirection(offset); cam.transform.LookAt(target.position);
            var rt = new RenderTexture(1024, 1024, 24); var tex = new Texture2D(1024, 1024, TextureFormat.RGB24, false); var active = RenderTexture.active;
            try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); tex.Apply(); File.WriteAllBytes(Reports + "/" + name + ".png", tex.EncodeToPNG()); }
            finally { RenderTexture.active = active; Object.Destroy(cam.gameObject); Object.Destroy(rt); Object.Destroy(tex); }
        }

        [UnityTest] public IEnumerator V02HandsUseRigidMeshesPoseLibrariesAndPunch()
        {
            Assert.That(poses, Is.Not.Null, "HandPoseController on the v02 player");
            var skins = hands.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var n in new[] { "Hand_R_FPS", "Hand_L_FPS", "Forearm_R_FPS", "Forearm_L_FPS" })
            {
                var s = skins.Single(x => x.name == n);
                Assert.That(UnityEditor.AssetDatabase.GetAssetPath(s.sharedMesh), Does.Contain("ProtagonistV02"), n + " uses the v02 mesh");
                var bpv = s.sharedMesh.GetBonesPerVertex(); int max = 0; for (int i = 0; i < bpv.Length; i++) max = Mathf.Max(max, bpv[i]);
                Assert.That(max, Is.EqualTo(1), n + " rigid: one bone per vertex");
            }
            // Armed rest: module on the forearm.
            yield return new WaitForEndOfFrame(); CaptureFps("v02-armed-rest");
            // Unarmed: remove the active weapons to reach the Normal representation (closed fist at rest).
            foreach (var w in Enumerable.Range(0, 9).Select(i => manager.GetWeaponAtSlotIndex(i)).Where(w => w).ToArray()) manager.RemoveWeapon(w);
            yield return new WaitForSeconds(1.2f); yield return new WaitForEndOfFrame();
            Assert.That(hands.CurrentRepresentation, Is.EqualTo(ProtagonistHandsPresentation.Representation.Normal));
            Assert.That(hands.RightHandVisible, Is.True, "Right v02 hand visible unarmed");
            CaptureFps("v02-unarmed-relaxed"); CloseUp(hands.RightHand, "v02-right-relaxed-closeup", new Vector3(.18f, .12f, .22f));
            // Left strike with F (melee): left hand appears, then hides again.
            var melee = player.GetComponent<ProtagonistMelee>(); bool sawLeft = false, sawAttack = false; float leftClosure = 0, rightClosure = 0; bool shotL = false, shotR = false;
            yield return Tap(Key.F);
            for (int i = 0; i < 40; i++)
            {
                yield return null; sawLeft |= hands.LeftHandVisible; sawAttack |= melee && melee.IsAttacking; leftClosure = Mathf.Max(leftClosure, poses.LeftClosure);
                if (!shotL && poses.LeftClosure > .95f) { shotL = true; yield return new WaitForEndOfFrame(); CaptureFps("v02-left-punch"); CloseUp(hands.LeftHandBone, "v02-left-fist-closeup", new Vector3(-.18f, .12f, .22f)); }
            }
            Assert.That(sawAttack, Is.True, "F starts the melee strike"); Assert.That(sawLeft, Is.True, "Left v02 hand shown during the strike");
            Assert.That(leftClosure, Is.GreaterThan(.9f), "Left hand closes into the v02 fist during the strike");
            yield return new WaitForSeconds(.8f);
            Assert.That(hands.LeftHandVisible, Is.False, "Left hand hidden again after recovery");
            // Unarmed M1: right jab with the v02 fist.
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState()); 
            for (int i = 0; i < 40; i++)
            {
                yield return null; rightClosure = Mathf.Max(rightClosure, poses.RightClosure);
                if (!shotR && poses.RightClosure > .95f) { shotR = true; yield return new WaitForEndOfFrame(); CaptureFps("v02-right-jab"); CloseUp(hands.RightHand, "v02-right-fist-closeup", new Vector3(.18f, .12f, .22f)); }
            }
            Assert.That(rightClosure, Is.GreaterThan(.9f), "Right hand closes into the v02 fist on the unarmed jab");
            File.WriteAllText(Reports + "/v02-hands.txt", "v02 meshes on 4 FPS renderers, 1 bone per vertex; HandPoseController present; unarmed Normal fist visible; F left strike shows and hides the left hand. Captures: armed rest, unarmed fist, left punch, close-ups.");
        }
    }
}
