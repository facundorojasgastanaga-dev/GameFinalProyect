using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IronRequiem
{
    /// Single source of key/button names for every on-screen help text. Reads the live (possibly rebound) bindings of
    /// the project-wide actions, so a text never shows a key the player has moved. Texts use {Action} tokens
    /// (for example "{Dash} DASH LISTO"); Version changes whenever a binding or the last-used device changes so
    /// HUDs that cache their strings know to rebuild them.
    public static class ControlHints
    {
        public const string KeyboardGroup = "Keyboard&Mouse", GamepadGroup = "Gamepad";
        static readonly Regex Token = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);
        static readonly Regex Legacy = new Regex(@"\[(E|F|G|M1|CTRL)\]", RegexOptions.Compiled);
        static bool hooked;

        public static int Version { get; private set; }
        public static bool UsingGamepad { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            if (hooked) { InputSystem.onEvent -= OnEvent; InputSystem.onActionChange -= OnActionChange; }
            hooked = true; Version = 0; UsingGamepad = false;
            InputSystem.onEvent += OnEvent; InputSystem.onActionChange += OnActionChange;
        }

        static void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            bool pad = device is Gamepad, kbm = device is Keyboard || device is Mouse;
            if (!pad && !kbm) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            if (pad == UsingGamepad) return;
            foreach (var _ in eventPtr.EnumerateChangedControls(device, magnitudeThreshold: .4f)) { UsingGamepad = pad; Version++; return; }
        }

        static void OnActionChange(object obj, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged) Version++;
        }

        /// Call after changing or resetting overrides so cached texts rebuild.
        public static void NotifyChanged() => Version++;

        /// Display name of the primary binding of Player/<action> for the active device family ("E", "LMB", "LEFT SHOULDER").
        /// Returns "—" when the action has no binding for that family.
        public static string Key(string action, bool? gamepad = null)
        {
            var map = InputSystem.actions;
            var a = map != null ? map.FindAction("Player/" + action, false) : null;
            if (a == null) return "?";
            string group = (gamepad ?? UsingGamepad) ? GamepadGroup : KeyboardGroup;
            var bindings = a.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b.isComposite || b.isPartOfComposite || string.IsNullOrEmpty(b.effectivePath) || !InGroup(b.groups, group)) continue;
                return Name(a, i);
            }
            return "—";
        }

        static readonly System.Collections.Generic.Dictionary<string, string> Friendly = new System.Collections.Generic.Dictionary<string, string>
        {
            { "<Keyboard>/space", "ESPACIO" }, { "<Keyboard>/leftShift", "SHIFT" }, { "<Keyboard>/rightShift", "SHIFT DER." }, { "<Keyboard>/leftCtrl", "CTRL" },
            { "<Keyboard>/rightCtrl", "CTRL DER." }, { "<Keyboard>/leftAlt", "ALT" }, { "<Keyboard>/tab", "TAB" }, { "<Keyboard>/capsLock", "BLOQ MAYÚS" },
            { "<Keyboard>/upArrow", "↑" }, { "<Keyboard>/downArrow", "↓" }, { "<Keyboard>/leftArrow", "←" }, { "<Keyboard>/rightArrow", "→" },
            { "<Mouse>/leftButton", "CLIC IZQ." }, { "<Mouse>/rightButton", "CLIC DER." }, { "<Mouse>/middleButton", "CLIC RUEDA" },
            { "<Mouse>/backButton", "M4" }, { "<Mouse>/forwardButton", "M5" }, { "<Mouse>/scroll/up", "RUEDA ↑" }, { "<Mouse>/scroll/down", "RUEDA ↓" },
            { "<Gamepad>/buttonSouth", "A" }, { "<Gamepad>/buttonEast", "B" }, { "<Gamepad>/buttonWest", "X" }, { "<Gamepad>/buttonNorth", "Y" },
            { "<Gamepad>/leftShoulder", "LB" }, { "<Gamepad>/rightShoulder", "RB" }, { "<Gamepad>/leftTrigger", "LT" }, { "<Gamepad>/rightTrigger", "RT" },
            { "<Gamepad>/leftStickPress", "L3" }, { "<Gamepad>/rightStickPress", "R3" }, { "<Gamepad>/dpad/up", "CRUZ ↑" }, { "<Gamepad>/dpad/down", "CRUZ ↓" },
            { "<Gamepad>/dpad/left", "CRUZ ←" }, { "<Gamepad>/dpad/right", "CRUZ →" }, { "<Gamepad>/start", "START" }, { "<Gamepad>/select", "SELECT" },
        };

        /// Short Spanish name of one binding (path based, so it does not depend on the OS keyboard language); unknown controls
        /// fall back to Input System's own display string.
        public static string Name(InputAction action, int bindingIndex)
        {
            string path = action.bindings[bindingIndex].effectivePath;
            if (string.IsNullOrEmpty(path)) return "—";
            if (Friendly.TryGetValue(path, out var name)) return name;
            return action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions).ToUpperInvariant();
        }

        /// Replaces {Action} tokens with the live key name.
        public static string Resolve(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            return Token.Replace(text, m => Key(m.Groups[1].Value));
        }

        /// For texts baked into scenes and prefabs with a literal key ("[E] ACOPLAR"): rewrites the old default keys.
        public static string ResolveLegacy(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // one pass: chained Replace calls would rewrite a key that another token just produced (E and F swapped)
            return Legacy.Replace(text, m =>
            {
                switch (m.Groups[1].Value)
                {
                    case "E": return "[" + Key("CollectWeapon") + "]";
                    case "F": return "[" + Key("Melee") + "]";
                    case "G": return "[" + Key("ThrowWeapon") + "]";
                    case "M1": return "[" + Key("Fire") + "]";
                    default: return "[" + Key("Dash") + "]";
                }
            });
        }

        static bool InGroup(string groups, string group)
        {
            if (string.IsNullOrEmpty(groups)) return false;
            foreach (var g in groups.Split(';')) if (g.Trim() == group) return true;
            return false;
        }
    }
}
