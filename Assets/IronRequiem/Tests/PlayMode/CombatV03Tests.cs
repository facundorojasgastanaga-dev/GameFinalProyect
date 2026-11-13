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
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace IronRequiem.Tests
{
    public sealed partial class CombatV03Tests
    {
        const string Output = "Tools/IronRequiem/Reports/CombatV03";
        [UnityTest] public IEnumerator MainScenePickupIntegration()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/FPS/Scenes/MainScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
            Application.runInBackground = true; Time.timeScale = 1;
            yield return null; yield return null;
            var player = Object.FindAnyObjectByType<PlayerCharacterController>();
            player.GetComponent<Health>().Invincible = true;
            Object.FindAnyObjectByType<GameFlowManager>().enabled = false;
            Object.FindAnyObjectByType<ObjectiveManager>().enabled = false;
            var enemy = Object.FindAnyObjectByType<RobotDamageProgression>().GetComponent<EnemyController>();
            var module = enemy.GetComponentInChildren<StolenCannon>(true);
            enemy.GetComponent<Health>().Kill(); yield return new WaitForSeconds(.45f);
            Assert.That(module && module.CanPickup && module.transform.parent == null, Is.True, "Death must leave an available independent cannon.");
            var carrier = player.GetComponent<StolenWeaponCarrier>();
            var character = player.GetComponent<CharacterController>(); character.enabled = false;
            player.enabled = false; player.transform.position = module.transform.position + new Vector3(0, 0, -1.4f);
            module.Body.isKinematic = true;
            player.PlayerCamera.transform.LookAt(module.SolidCollider.bounds.center);
            var previous = InputSystem.actions.devices;
            var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.actions.devices = new InputDevice[] { keyboard, mouse }; keyboard.MakeCurrent(); mouse.MakeCurrent();
            Cursor.lockState = CursorLockMode.Locked;
#if UNITY_EDITOR
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
#endif
            yield return null; InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Directory.CreateDirectory(Output);
            if (!carrier)
            {
                Assert.That(player.GetComponent<MechanicInput>(), Is.Null);
                Assert.That(module.State, Is.EqualTo(StolenCannonState.Available));
                File.WriteAllText(Output + "/pickup-reproduction.txt", "REPRODUCED MainScene: cannon detached, available and solid; CollectWeapon F binding=" + InputSystem.actions.FindAction("Player/CollectWeapon").bindings[0].effectivePath + "; no MechanicInput/StolenWeaponCarrier on player; pressing F leaves Available.");
            }
            else
            {
                Assert.That(carrier.Held, Is.SameAs(module), "F should pick up the cannon dropped by a dead T07 in MainScene. canAct="+player.GetComponent<MechanicInput>().CanAct+" cursor="+Cursor.lockState+" ending="+Object.FindAnyObjectByType<GameFlowManager>().GameIsEnding+" status="+carrier.GetPickupStatus(module)+" candidate="+carrier.Candidate);
                Assert.That(module.State, Is.EqualTo(StolenCannonState.Equipped));
                yield return new WaitForSeconds(.3f);
                Assert.That(module.EquippedWeapon, Is.Not.Null); Assert.That(module.EquippedWeapon.IsWeaponActive, Is.True);
                File.WriteAllText(Output + "/pickup-main-fixed.txt", "PASS MainScene F picks up and equips death-dropped T07 cannon.");
            }
            InputSystem.actions.devices = previous; InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); Cursor.lockState = CursorLockMode.None;
        }
    }
}
