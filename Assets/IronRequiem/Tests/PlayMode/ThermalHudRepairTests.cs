using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IronRequiem.Tests
{
    public sealed class ThermalHudRepairTests
    {
        const string Report = "Tools/IronRequiem/Reports/ThermalHudRepair";
        bool background; float capture; Keyboard keyboard; Mouse mouse;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
        [UnitySetUp] public IEnumerator Setup()
        {
            background = Application.runInBackground; capture = Time.captureDeltaTime; Application.runInBackground = true; Time.captureDeltaTime = 1f / 60;
            devices = InputSystem.actions.devices; keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); keyboard.MakeCurrent(); mouse.MakeCurrent(); InputSystem.actions.devices = new InputDevice[] { keyboard, mouse };
            Time.timeScale = 1; AudioListener.pause = false; Directory.CreateDirectory(Report); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; AudioListener.pause = false; Time.captureDeltaTime = capture; InputSystem.actions.devices = devices; InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); Cursor.lockState = CursorLockMode.None;
            var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("HeatCleanup" + Time.frameCount)); yield return SceneManager.UnloadSceneAsync(scene); Application.runInBackground = background;
        }
        IEnumerator Load(string name)
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/" + name + ".unity", new LoadSceneParameters(LoadSceneMode.Single)); yield return null; yield return null;
            var player = Object.FindAnyObjectByType<PlayerCharacterController>(); player.GetComponent<Health>().Invincible = true;
            var wave = Object.FindAnyObjectByType<CombatTwoRoomsLab>(); if (wave) { wave.enabled = false; if (wave.SpawnQueue) { wave.SpawnQueue.CancelPending(); wave.SpawnQueue.enabled = false; } }
            Object.FindAnyObjectByType<CombatLabMenu>().BeginGame(); yield return new WaitForSeconds(.7f);
        }
        static void Visible(WeaponHeatHud hud)
        {
            var settings = new SerializedObject(hud); var content = (GameObject)settings.FindProperty("content").objectReferenceValue; Assert.That(content.activeInHierarchy, Is.True);
            var graphic = (ThermalGaugeGraphic)settings.FindProperty("gauge").objectReferenceValue; Assert.That(graphic.canvasRenderer.GetInheritedAlpha(), Is.GreaterThan(.9f)); Assert.That(graphic.rectTransform.rect.width, Is.GreaterThan(0));
            var corners = new Vector3[4]; graphic.rectTransform.GetWorldCorners(corners);
            foreach (var point in corners) { var screen = RectTransformUtility.WorldToScreenPoint(null, point); Assert.That(screen.x, Is.InRange(-1, Screen.width + 1)); Assert.That(screen.y, Is.InRange(-1, Screen.height + 1)); }
        }
        [UnityTest] public IEnumerator AllCombatLabsHaveVisibleBoundHud()
        {
            foreach (string name in new[] { "SawBotLab", "CombatVarietyLab", "CombatTwoRoomsLab", "BunkerLab", "SawBotSustainLab", "SawBotWeaponDeathLab", "SawBotSawPolishLab" })
            {
                yield return Load(name); var inventory = Object.FindAnyObjectByType<PlayerWeaponsManager>(); var huds = Object.FindObjectsByType<WeaponHeatHud>(); Assert.That(huds.Length, Is.EqualTo(1), name);
                Assert.That(huds[0].BoundHeat, Is.SameAs(inventory.GetActiveWeapon().GetComponent<WeaponHeat>()), name); Assert.That(huds[0].BoundHeat, Is.Not.Null, name); Visible(huds[0]);
            }
        }
        [UnityTest] public IEnumerator AutomaticFireHeatCoolingAndWeaponSwitch()
        {
            yield return Load("SawBotLab"); var inventory = Object.FindAnyObjectByType<PlayerWeaponsManager>(); var hud = Object.FindAnyObjectByType<WeaponHeatHud>(); var weapon = inventory.GetActiveWeapon(); var heat = weapon.GetComponent<WeaponHeat>();
            Assert.That(weapon.DelayBetweenShots, Is.EqualTo(.1f).Within(.001f)); int shots = 0; weapon.OnShootProcessed += () => shots++;
            int expected = Mathf.CeilToInt(heat.MaximumHeat / new SerializedObject(heat).FindProperty("heatPerShot").floatValue);
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 }); float deadline = Time.time + 8;
            while (!heat.IsOverheated && Time.time < deadline) yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            Assert.That(shots, Is.EqualTo(expected)); Assert.That(heat.IsOverheated, Is.True); Visible(hud);
            var text = hud.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "HeatState"); Assert.That(text.text, Is.EqualTo("SOBRECALENTADA"));
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Report + "/heat-visible.png")); yield return null;
            inventory.SwitchWeapon(true); yield return new WaitForSeconds(.7f); Assert.That(inventory.GetActiveWeapon(), Is.Not.SameAs(weapon)); Assert.That(hud.BoundHeat, Is.SameAs(inventory.GetActiveWeapon().GetComponent<WeaponHeat>()));
            var content = (GameObject)new SerializedObject(hud).FindProperty("content").objectReferenceValue; Assert.That(content.activeSelf, Is.EqualTo(hud.BoundHeat != null));
            inventory.SwitchWeapon(false); yield return new WaitForSeconds(.7f); Assert.That(hud.BoundHeat, Is.SameAs(heat)); Visible(hud);
            yield return new WaitForSeconds(4); Assert.That(heat.IsOverheated, Is.False); Assert.That(heat.CurrentHeat, Is.LessThan(35)); Visible(hud);
        }
        [UnityTest] public IEnumerator ExtendedHeatAndShotgunWithoutAmmo()
        {
            yield return Load("WeaponHeatBalanceLab"); var inventory = Object.FindAnyObjectByType<PlayerWeaponsManager>(); var hud = Object.FindAnyObjectByType<WeaponHeatHud>(); var primary = inventory.GetActiveWeapon(); var heat = primary.GetComponent<WeaponHeat>();
            Assert.That(primary.UsesHeatInsteadOfAmmo, Is.True); Assert.That(primary.DelayBetweenShots, Is.EqualTo(.1f).Within(.001f));
            int shots = 0; primary.OnShootProcessed += () => shots++; primary.SetAmmo(0); float start = Time.time;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 }); float deadline = Time.time + 8;
            while (!heat.IsOverheated && Time.time < deadline) yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            Assert.That(shots, Is.EqualTo(40)); Assert.That(heat.IsOverheated, Is.True); Assert.That(primary.GetCurrentAmmo(), Is.Zero, "Firing uses heat, even with zero ammunition"); Visible(hud);
            string measured = "Primary40 shots to lock in " + (Time.time - start) + " simulated seconds.\n";
            inventory.SwitchToWeaponIndex(1); yield return new WaitForSeconds(.7f); var shotgun = inventory.GetActiveWeapon(); var shotgunHeat = shotgun.GetComponent<WeaponHeat>();
            Assert.That(shotgun.UsesHeatInsteadOfAmmo, Is.True); Assert.That(shotgun.BulletsPerShot, Is.EqualTo(10)); Assert.That(shotgun.DelayBetweenShots, Is.EqualTo(.85f).Within(.001f)); Assert.That(shotgun.GetComponent<CylinderBankReload>().enabled, Is.False); Assert.That(shotgun.HasPhysicalBullets, Is.False); Assert.That(shotgun.AutomaticReload, Is.False); Assert.That(hud.BoundHeat, Is.SameAs(shotgunHeat));
            shotgun.SetAmmo(0); int blasts = 0; shotgun.OnShootProcessed += () => blasts++; start = Time.time;
            for (int i = 0; i < 8; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 }); yield return null; yield return null; InputSystem.QueueStateEvent(mouse, new MouseState()); yield return new WaitForSeconds(.84f);
                Assert.That(blasts, Is.EqualTo(i + 1));
            }
            Assert.That(shotgunHeat.IsOverheated, Is.True); Assert.That(blasts, Is.EqualTo(8)); Assert.That(shotgun.GetCurrentAmmo(), Is.Zero); Visible(hud);
            Assert.That(shotgun.TryShootSingleWithCharge(1), Is.False); Assert.That(shotgun.GetComponent<CylinderBankReload>().IsReloading, Is.False);
            measured += "Shotgun8 discharges/80 pellets to lock in " + (Time.time - start) + " simulated seconds; no ammo consumed or refilled.\n";
            Time.timeScale = 0; float pausedHeat = shotgunHeat.CurrentHeat; yield return new WaitForSecondsRealtime(.15f); Assert.That(shotgunHeat.CurrentHeat, Is.EqualTo(pausedHeat)); Time.timeScale = 1;
            yield return new WaitForSeconds(4.5f); Assert.That(shotgunHeat.IsOverheated, Is.False); Assert.That(shotgun.TryShootSingleWithCharge(1), Is.True); Assert.That(blasts, Is.EqualTo(9)); Assert.That(shotgun.GetCurrentAmmo(), Is.Zero); Visible(hud);
            var hands = Object.FindAnyObjectByType<ProtagonistHandsPresentation>(); Assert.That(hands.CurrentRepresentation, Is.EqualTo(ProtagonistHandsPresentation.Representation.Mounted));
            File.WriteAllText("Tools/IronRequiem/Reports/WeaponHeatBalance/measured.txt", measured); ScreenCapture.CaptureScreenshot(Path.GetFullPath("Tools/IronRequiem/Reports/WeaponHeatBalance/shotgun-thermal.png")); yield return null;
        }
    }
}
