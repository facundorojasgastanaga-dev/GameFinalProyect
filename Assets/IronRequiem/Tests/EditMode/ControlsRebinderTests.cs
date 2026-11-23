using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronRequiem.Tests
{
    /// Conflict detection, swap, empty slots, persistence and reset of the rebinding logic (no UI, no devices).
    public sealed class ControlsRebinderTests
    {
        const string Path = "Assets/FPS/InputSystem_Actions.inputactions";
        string savedPrefs; bool hadPrefs;

        [SetUp] public void SetUp() { hadPrefs = PlayerPrefs.HasKey(ControlsRebinder.PrefsKey); savedPrefs = PlayerPrefs.GetString(ControlsRebinder.PrefsKey, ""); PlayerPrefs.DeleteKey(ControlsRebinder.PrefsKey); }
        [TearDown] public void TearDown() { if (hadPrefs) PlayerPrefs.SetString(ControlsRebinder.PrefsKey, savedPrefs); else PlayerPrefs.DeleteKey(ControlsRebinder.PrefsKey); }

        static InputActionAsset Clone() => InputActionAsset.FromJson(AssetDatabase.LoadAssetAtPath<InputActionAsset>(Path).ToJson());

        [Test] public void EveryRowHasTwoSlotsPerDeviceFamilyExceptMovementParts()
        {
            var asset = Clone();
            foreach (var row in ControlsRebinder.Rows)
            {
                var action = asset.FindAction("Player/" + row.Action, true);
                foreach (var group in row.Part != null ? new[] { "Keyboard&Mouse" } : new[] { "Keyboard&Mouse", "Gamepad" })
                    for (int slot = 0; slot < ControlsRebinder.Slots; slot++)
                        Assert.That(ControlsRebinder.SlotIndex(action, group, row.Part, slot), Is.GreaterThanOrEqualTo(0), row.Action + " " + group + " slot " + slot);
            }
        }

        [Test] public void FreeControlIsAssignedAndSurvivesSaveLoad()
        {
            var a = Clone(); var melee = a.FindAction("Player/Melee", true);
            int slot0 = ControlsRebinder.SlotIndex(melee, "Keyboard&Mouse", null, 0);
            Assert.That(ControlsRebinder.FindConflict(a, "Keyboard&Mouse", melee, slot0, "<Keyboard>/v", out _, out _), Is.False);
            ControlsRebinder.Assign(a, melee, slot0, "<Keyboard>/v");
            var b = Clone(); ControlsRebinder.Load(b);
            var loaded = b.FindAction("Player/Melee");
            Assert.That(loaded.bindings[slot0].effectivePath, Is.EqualTo("<Keyboard>/v"));
            Assert.That(ControlsRebinder.Display(loaded, slot0), Is.EqualTo("V"));
        }

        [Test] public void EmptySlotCanBeFilled()
        {
            var a = Clone(); var melee = a.FindAction("Player/Melee", true);
            int slot1 = ControlsRebinder.SlotIndex(melee, "Keyboard&Mouse", null, 1);
            Assert.That(ControlsRebinder.Display(melee, slot1), Is.EqualTo("—"));
            ControlsRebinder.Assign(a, melee, slot1, "<Keyboard>/v");
            Assert.That(melee.bindings[slot1].effectivePath, Is.EqualTo("<Keyboard>/v"));
        }

        [Test] public void UsedControlIsReportedAndSwapExchangesBoth()
        {
            var a = Clone(); var melee = a.FindAction("Player/Melee", true); var crouch = a.FindAction("Player/Crouch", true);
            int mi = ControlsRebinder.SlotIndex(melee, "Keyboard&Mouse", null, 0), ci = ControlsRebinder.SlotIndex(crouch, "Keyboard&Mouse", null, 0);
            Assert.That(crouch.bindings[ci].effectivePath, Is.EqualTo("<Keyboard>/c"));
            Assert.That(ControlsRebinder.FindConflict(a, "Keyboard&Mouse", melee, mi, "<Keyboard>/c", out var other, out int oi), Is.True);
            Assert.That(other.name, Is.EqualTo("Crouch")); Assert.That(oi, Is.EqualTo(ci));
            ControlsRebinder.Swap(a, melee, mi, "<Keyboard>/c", other, oi);
            Assert.That(melee.bindings[mi].effectivePath, Is.EqualTo("<Keyboard>/c"));
            Assert.That(crouch.bindings[ci].effectivePath, Is.EqualTo("<Keyboard>/f"));
        }

        [Test] public void JumpAndDashMayShareSpaceButNothingElseMay()
        {
            var a = Clone(); var dash = a.FindAction("Player/Dash", true); var melee = a.FindAction("Player/Melee", true);
            Assert.That(ControlsRebinder.FindConflict(a, "Keyboard&Mouse", dash, 0, "<Keyboard>/space", out _, out _), Is.False);
            Assert.That(ControlsRebinder.FindConflict(a, "Keyboard&Mouse", melee, 0, "<Keyboard>/space", out var other, out _), Is.True);
            Assert.That(new[] { "Jump", "Dash" }, Does.Contain(other.name));
        }

        [Test] public void GamepadAndKeyboardGroupsDoNotConflictWithEachOther()
        {
            var a = Clone(); var melee = a.FindAction("Player/Melee", true);
            Assert.That(ControlsRebinder.FindConflict(a, "Gamepad", melee, 0, "<Keyboard>/c", out _, out _), Is.False);
        }

        [Test] public void ResetAllRestoresDefaultsAndClearsSavedData()
        {
            var a = Clone(); var melee = a.FindAction("Player/Melee", true);
            int mi = ControlsRebinder.SlotIndex(melee, "Keyboard&Mouse", null, 0);
            ControlsRebinder.Assign(a, melee, mi, "<Keyboard>/v");
            Assert.That(PlayerPrefs.HasKey(ControlsRebinder.PrefsKey), Is.True);
            ControlsRebinder.ResetAll(a);
            Assert.That(melee.bindings[mi].effectivePath, Is.EqualTo("<Keyboard>/f"));
            Assert.That(PlayerPrefs.HasKey(ControlsRebinder.PrefsKey), Is.False);
        }

        [Test] public void CompositePartRowsFindWasdAndArrows()
        {
            var move = Clone().FindAction("Player/Move", true);
            Assert.That(move.bindings[ControlsRebinder.SlotIndex(move, "Keyboard&Mouse", "up", 0)].path, Is.EqualTo("<Keyboard>/w"));
            Assert.That(move.bindings[ControlsRebinder.SlotIndex(move, "Keyboard&Mouse", "up", 1)].path, Is.EqualTo("<Keyboard>/upArrow"));
        }

        [Test] public void HintsFollowRebinding()
        {
            var a = Clone(); var melee = a.FindAction("Player/Melee", true);
            int mi = ControlsRebinder.SlotIndex(melee, "Keyboard&Mouse", null, 0);
            Assert.That(ControlsRebinder.Display(melee, mi), Is.EqualTo("F"));
            melee.ApplyBindingOverride(mi, "<Keyboard>/v");
            Assert.That(ControlsRebinder.Display(melee, mi), Is.EqualTo("V"));
        }
    }
}
