using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    public sealed class SawBotSawPolishTests
    {
        const string Enemy = "Assets/IronRequiem/Prefabs/Enemies/SawBot_SawPolish_v04.prefab";
        const string Report = "Tools/IronRequiem/Reports/SawBotSawPolish";
        PlayerCharacterController player; PlayerWeaponsManager weapons; StolenWeaponCarrier carrier; SawBotCombat bot;
        Keyboard keys; Mouse mouse; float capture; bool background;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
        [UnitySetUp] public IEnumerator Setup()
        {
            capture = Time.captureDeltaTime; background = Application.runInBackground; Application.runInBackground = true; Time.captureDeltaTime = 1f / 60; Time.timeScale = 1; AudioListener.pause = false;
            devices = InputSystem.actions.devices; keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keys.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keys, mouse };
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/SawBotSawPolishLab.unity", new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            var menu = Object.FindAnyObjectByType<CombatLabMenu>(); menu.BeginGame(); menu.enabled = false; var waves = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); waves.enabled = false; if (waves.SpawnQueue) waves.SpawnQueue.enabled = false;
            Object.FindAnyObjectByType<GameFlowManager>().enabled = false; Object.FindAnyObjectByType<ObjectiveManager>().enabled = false;
            player = Object.FindAnyObjectByType<PlayerCharacterController>(); player.enabled = false; player.GetComponent<Health>().Invincible = true; weapons = player.GetComponent<PlayerWeaponsManager>(); carrier = player.GetComponent<StolenWeaponCarrier>();
            foreach (var collider in Object.FindObjectsByType<Collider>()) if (!collider.transform.IsChildOf(player.transform) && !(collider.bounds.size.y < 2 && collider.bounds.size.x > 20 && collider.bounds.size.z > 20)) collider.enabled = false;
            MovePlayer(new Vector3(-12, .03f, -10)); Cursor.lockState = CursorLockMode.Locked; yield return new WaitForSeconds(.7f);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; AudioListener.pause = false; Time.captureDeltaTime = capture; InputSystem.actions.devices = devices; InputSystem.RemoveDevice(keys); InputSystem.RemoveDevice(mouse); Cursor.lockState = CursorLockMode.None;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("SawPolishCleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene); Application.runInBackground = background;
        }
        void MovePlayer(Vector3 position) { var collider = player.GetComponent<CharacterController>(); collider.enabled = false; player.transform.SetPositionAndRotation(position, Quaternion.identity); collider.enabled = true; player.CharacterVelocity = Vector3.zero; Physics.SyncTransforms(); }
        IEnumerator Spawn(bool moving = true)
        {
            Assert.That(NavMesh.SamplePosition(new Vector3(-12, 0, -7), out var hit, 3, NavMesh.AllAreas), Is.True);
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Enemy), hit.position, Quaternion.Euler(0, 180, 0)); bot = go.GetComponent<SawBotCombat>(); var patrol = new GameObject("SawPolishPatrol").AddComponent<PatrolPath>(); patrol.PathNodes.Add(player.transform); go.GetComponent<EnemyController>().PatrolPath = patrol; yield return null; yield return null;
            if (!moving) { go.GetComponent<EnemyController>().enabled = false; go.GetComponent<EnemyMobile>().enabled = false; if (go.GetComponent<NavMeshAgent>().isOnNavMesh) go.GetComponent<NavMeshAgent>().isStopped = true; }
        }
        IEnumerator Until(Func<bool> condition, float seconds, string label) { float end = Time.time + seconds, real = Time.realtimeSinceStartup + 50; while (!condition() && Time.time < end && Time.realtimeSinceStartup < real) yield return null; Assert.That(condition(), Is.True, label); }
        void Aim() { player.PlayerCamera.transform.LookAt(bot.GetComponent<Actor>().AimPoint.position); Cursor.lockState = CursorLockMode.Locked; Physics.SyncTransforms(); }
        IEnumerator TapE() { InputSystem.QueueStateEvent(keys, new KeyboardState(Key.E)); yield return null; yield return null; InputSystem.QueueStateEvent(keys, new KeyboardState()); yield return null; }
        void Capture(string name)
        {
            var camera = new GameObject("SawPolishProof").AddComponent<Camera>(); camera.transform.position = bot.transform.position + new Vector3(2.2f, 1.15f, -2.2f); camera.transform.LookAt(bot.transform.position + Vector3.up * .65f); camera.fieldOfView = 35;
            var light = new GameObject("SawPolishProofLight").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 3; light.transform.rotation = Quaternion.Euler(30, 140, 0);
            SaveCamera(camera, name); Object.Destroy(camera.gameObject); Object.Destroy(light.gameObject);
        }
        static void SaveCamera(Camera camera, string name)
        {
            var previous = camera.targetTexture; var active = RenderTexture.active; var rt = new RenderTexture(1280, 720, 24); var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); File.WriteAllBytes(Report + "/" + name + ".png", image.EncodeToPNG()); }
            finally { camera.targetTexture = previous; RenderTexture.active = active; Object.Destroy(rt); Object.Destroy(image); }
        }
        [UnityTest] public IEnumerator AlignedChainsCirculateAndStopOnRangeAndPause()
        {
            yield return Spawn(); var presentation = bot.GetComponent<SawMotorPresentation>(); var chains = bot.GetComponentsInChildren<SawChainMotion>(); Assert.That(chains.Length, Is.EqualTo(2));
            foreach (var chain in chains) Assert.That(Vector3.Angle(chain.transform.forward, chain.transform.parent.position - chain.transform.parent.parent.position), Is.LessThan(.1f), "Blade must continue wrist axis");
            yield return Until(() => bot.IsAttacking, 6, "Existing AI reaches sustained attack"); yield return new WaitForSeconds(.3f);
            Assert.That(presentation.IsRunning, Is.True); Assert.That(presentation.Motor.isPlaying, Is.True); Assert.That(presentation.Smoke.All(p => p.isPlaying && p.particleCount > 0), Is.True);
            float before = chains[0].Travel; var guide = bot.GetComponentsInChildren<Transform>().First(t => t.name == "GuideBar_L"); var rotation = guide.localRotation;
            for (int lod = 0; lod < 3; lod++) { bot.GetComponent<LODGroup>().ForceLOD(lod); yield return new WaitForSeconds(.12f); Assert.That(chains.All(c => c.Views[lod].enabled && c.Views[lod].GetComponent<MeshFilter>().sharedMesh), Is.True); }
            Assert.That(chains[0].Travel, Is.Not.EqualTo(before)); Assert.That(guide.localRotation, Is.EqualTo(rotation), "Only chain cutters circulate, not guide bar"); Capture("enemy-sawing");
            Time.timeScale = 0; yield return null; yield return null; before = chains[0].Travel; yield return new WaitForSecondsRealtime(.15f);
            Assert.That(chains[0].Travel, Is.EqualTo(before)); Assert.That(presentation.Motor.isPlaying, Is.False); Assert.That(presentation.Smoke.All(p => p.particleCount == 0), Is.True); Time.timeScale = 1;
            MovePlayer(new Vector3(-12, .03f, -17)); yield return Until(() => !bot.IsAttacking, 2, "Leaves saw reach"); yield return null;
            Assert.That(presentation.IsRunning, Is.False); Assert.That(presentation.Motor.isPlaying, Is.False); Assert.That(presentation.Smoke.All(p => p.particleCount == 0), Is.True);
        }
        [UnityTest] public IEnumerator ExtractionAndEquippedSawUseSameAnimatedModule()
        {
            yield return Spawn(false); bot.transform.position = new Vector3(-12, .03f, -8.2f); bot.GetComponent<Health>().TakeDamage(bot.GetComponent<Health>().MaxHealth * .3f, player.gameObject); Aim(); yield return null; yield return null;
            Assert.That(carrier.ExecutionCandidate, Is.SameAs(bot.GetComponent<EnemyExecutionSource>())); yield return TapE(); yield return Until(() => bot.IsDead, 2, "Execution"); yield return new WaitForSeconds(.7f);
            var module = carrier.Held; Assert.That(module, Is.Not.Null); var weapon = module.EquippedWeapon; var presentation = weapon.GetComponent<SawMotorPresentation>(); var chain = weapon.GetComponentInChildren<SawChainMotion>();
            var enemyRight = bot.GetComponentsInChildren<SawChainMotion>().Single(c => c.transform.parent.name == "Chain_R"); Assert.That(enemyRight.Views.All(r => r.forceRenderingOff), Is.True, "Extracted chain absent in all LODs");
            float energy = module.Ammo.Remaining, before = chain.Travel; InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 }); yield return new WaitForSeconds(.45f);
            Assert.That(presentation.IsRunning, Is.True); Assert.That(chain.Travel, Is.Not.EqualTo(before)); Assert.That(presentation.Smoke.All(p => p.particleCount > 0), Is.True); Assert.That(weapon.GetComponent<AudioSource>().isPlaying, Is.True);
            Assert.That(module.Ammo.Remaining, Is.EqualTo(energy - 4.8f).Within(1.21f), "Existing12energy/s remains"); SaveCamera(player.PlayerCamera, "equipped-running"); InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null; yield return null;
            Assert.That(presentation.IsRunning, Is.False); Assert.That(presentation.Smoke.All(p => p.particleCount == 0), Is.True); Assert.That(weapon.GetComponent<AudioSource>().isPlaying, Is.False);
            weapons.SwitchWeapon(true); yield return new WaitForSeconds(.6f); Assert.That(presentation.IsRunning, Is.False);
            int slot = Enumerable.Range(0, 9).Single(i => weapons.GetWeaponAtSlotIndex(i) == weapon); weapons.SwitchToWeaponIndex(slot, true); yield return new WaitForSeconds(.7f);
            Assert.That(carrier.ThrowHeld(), Is.True); yield return null; Assert.That(module.GetComponentsInChildren<SawChainMotion>().Length, Is.EqualTo(1));
            Assert.That(module.GetComponentsInChildren<SawMotorPresentation>().Length, Is.EqualTo(0), "Ground module unpowered, critical smoke remains separate");
        }
        [UnityTest] public IEnumerator DeathClearsEffectsAndCollapseStaysAboveFloor()
        {
            yield return Spawn(); yield return Until(() => bot.IsAttacking, 6, "Attacking before death"); yield return new WaitForSeconds(.3f); var presentation = bot.GetComponent<SawMotorPresentation>(); var chain = bot.GetComponentsInChildren<SawChainMotion>()[0];
            bot.GetComponent<Health>().Kill(); yield return null; yield return null; float travel = chain.Travel; Assert.That(bot.IsAttacking, Is.False); Assert.That(presentation.IsRunning, Is.False); Assert.That(presentation.Motor.isPlaying, Is.False); Assert.That(presentation.Smoke.All(p => p.particleCount == 0), Is.True);
            float minimum = float.PositiveInfinity;
            for (int frame = 0; frame < 126; frame++)
            {
                yield return null;
                foreach (var skin in bot.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = skin.sharedMesh; var vertices = mesh.vertices; var weights = mesh.boneWeights; var bind = mesh.bindposes;
                    var matrices = skin.bones.Select(b => bot.transform.worldToLocalMatrix * b.localToWorldMatrix).ToArray();
                    for (int v = 0; v < vertices.Length; v++) { int bone = weights[v].boneIndex0; minimum = Mathf.Min(minimum, matrices[bone].MultiplyPoint3x4(bind[bone].MultiplyPoint3x4(vertices[v])).y); }
                }
                foreach (var filter in bot.GetComponentsInChildren<SawChainMotion>().SelectMany(c => c.GetComponentsInChildren<MeshFilter>())) foreach (var v in filter.sharedMesh.vertices) minimum = Mathf.Min(minimum, bot.transform.InverseTransformPoint(filter.transform.TransformPoint(v)).y);
            }
            Assert.That(chain.Travel, Is.EqualTo(travel)); Assert.That(minimum, Is.GreaterThan(-.035f), "Corrected saws do not penetrate flat floor during death"); Capture("death-corrected");
            File.WriteAllText(Report + "/playmode.txt", "Runtime: alignment, shared chain phases across3LODs, smoke/audio during attack, range/pause/release/death stop, real E extraction, module energy/switch/throw. Death minimum=" + minimum);
        }
    }
}
