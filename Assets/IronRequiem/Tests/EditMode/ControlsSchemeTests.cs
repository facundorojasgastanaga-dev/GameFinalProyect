using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace IronRequiem.Tests
{
    /// Audits the default "Ágil" control scheme (CONTROLS_V01.md) and the override save/load round trip.
    public sealed class ControlsSchemeTests
    {
        const string Path = "Assets/FPS/InputSystem_Actions.inputactions";
        // Intentional shared controls: Space / South are Jump on the ground and Dash in the air (mutually exclusive by state).
        static readonly HashSet<string> AllowedPairs = new HashSet<string> { "Dash|Jump" };

        static InputActionAsset Clone() => InputActionAsset.FromJson(AssetDatabase.LoadAssetAtPath<InputActionAsset>(Path).ToJson());

        [Test] public void NoTwoPlayerActionsShareAControlExceptAllowedPairs()
        {
            var asset = Clone(); var map = asset.FindActionMap("Player", true);
            var owners = new Dictionary<string, List<string>>();
            foreach (var action in map.actions)
                foreach (var b in action.bindings)
                {
                    if (b.isComposite || string.IsNullOrEmpty(b.path)) continue;
                    string key = (b.groups ?? "").Trim(';') + "|" + b.path;
                    if (!owners.TryGetValue(key, out var list)) owners[key] = list = new List<string>();
                    if (!list.Contains(action.name)) list.Add(action.name);
                }
            var conflicts = owners.Where(o => o.Value.Count > 1 && !AllowedPairs.Contains(string.Join("|", o.Value.OrderBy(n => n)))).Select(o => o.Key + " -> " + string.Join(",", o.Value)).ToList();
            Assert.That(conflicts, Is.Empty, string.Join("\n", conflicts));
        }

        [Test] public void LegacyActionsAreGoneAndNewOnesExist()
        {
            var map = Clone().FindActionMap("Player", true);
            foreach (var gone in new[] { "Attack", "Interact", "Previous", "Next", "NextWeapon" }) Assert.That(map.FindAction(gone), Is.Null, gone);
            var needed = new List<string> { "WeaponNext", "WeaponPrevious", "Dash", "Melee", "ThrowWeapon", "CollectWeapon", "Crouch", "Sprint", "Jump", "Overdrive" };
            for (int i = 1; i <= 9; i++) needed.Add("SelectWeapon" + i);
            foreach (var name in needed) Assert.That(map.FindAction(name), Is.Not.Null, name);
            Assert.That(map.FindAction("Overdrive").bindings.Count, Is.Zero, "Overdrive is automatic: kept, unbound");
        }

        [Test] public void EveryCombatActionIsReachableOnKeyboardAndGamepad()
        {
            var map = Clone().FindActionMap("Player", true);
            string[] both = { "Move", "Look", "Jump", "Sprint", "Crouch", "Fire", "Aim", "Dash", "CollectWeapon", "Melee", "ThrowWeapon", "WeaponNext", "WeaponPrevious", "SelectWeapon1", "SelectWeapon2" };
            foreach (var name in both)
            {
                var a = map.FindAction(name, true);
                Assert.That(a.bindings.Any(b => (b.groups ?? "").Contains("Keyboard&Mouse")), Is.True, name + " keyboard");
                Assert.That(a.bindings.Any(b => (b.groups ?? "").Contains("Gamepad")), Is.True, name + " gamepad");
            }
        }

        [Test] public void MenuUsesEscapeAndNoLongerTab()
        {
            var menu = Clone().FindAction("UI/Menu", true);
            Assert.That(menu.bindings.Any(b => b.path == "<Keyboard>/escape"), Is.True);
            Assert.That(menu.bindings.Any(b => b.path == "<Keyboard>/tab"), Is.False);
        }

        [Test] public void OverridesSurviveSaveLoadAndResetRestoresDefaults()
        {
            var a = Clone(); var melee = a.FindAction("Player/Melee", true);
            int index = melee.bindings.IndexOf(b => b.path == "<Keyboard>/f");
            melee.ApplyBindingOverride(index, "<Keyboard>/v");
            string json = a.SaveBindingOverridesAsJson();
            var b2 = Clone(); b2.LoadBindingOverridesFromJson(json);
            Assert.That(b2.FindAction("Player/Melee").bindings[index].effectivePath, Is.EqualTo("<Keyboard>/v"));
            b2.RemoveAllBindingOverrides();
            Assert.That(b2.FindAction("Player/Melee").bindings[index].effectivePath, Is.EqualTo("<Keyboard>/f"));
        }
    }
}
