using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronRequiem
{
    /// Rebinding logic without any UI: finds the two binding "slots" of an action for a device family, captures a new
    /// control, detects and swaps conflicts, and saves/loads the overrides of the project-wide asset (the same asset that
    /// PlayerInputHandler and every other consumer read). Overrides live in PlayerPrefs and are loaded before the first scene.
    public static class ControlsRebinder
    {
        public const string PrefsKey = "IronRequiem.InputOverrides";
        public const int Slots = 2;

        public readonly struct Row
        {
            public readonly string Action, Label, Hint, Category, Part;
            public Row(string action, string label, string category, string hint = null, string part = null) { Action = action; Label = label; Category = category; Hint = hint; Part = part; }
        }

        public const string Movement = "MOVIMIENTO", Combat = "COMBATE", Weapons = "SELECCIÓN DE ARMA";

        public static readonly Row[] Rows =
        {
            new Row("Move", "Avanzar", Movement, null, "up"), new Row("Move", "Retroceder", Movement, null, "down"),
            new Row("Move", "Izquierda", Movement, null, "left"), new Row("Move", "Derecha", Movement, null, "right"),
            new Row("Jump", "Saltar", Movement), new Row("Dash", "Dash", Movement, "en el aire; comparte tecla con Saltar"),
            new Row("Crouch", "Agacharse / Slide", Movement),
            new Row("Fire", "Disparar", Combat), new Row("Aim", "Apuntar", Combat), new Row("Melee", "Golpe / carga", Combat),
            new Row("CollectWeapon", "Interactuar", Combat, "ejecutar · recoger · estación"), new Row("ThrowWeapon", "Expulsar arma", Combat),
            new Row("WeaponNext", "Arma siguiente", Combat), new Row("WeaponPrevious", "Arma anterior", Combat), new Row("Reload", "Recargar", Combat),
            new Row("SelectWeapon1", "Arma 1", Weapons), new Row("SelectWeapon2", "Arma 2", Weapons), new Row("SelectWeapon3", "Arma 3", Weapons),
            new Row("SelectWeapon4", "Arma 4", Weapons), new Row("SelectWeapon5", "Arma 5", Weapons), new Row("SelectWeapon6", "Arma 6", Weapons),
            new Row("SelectWeapon7", "Arma 7", Weapons), new Row("SelectWeapon8", "Arma 8", Weapons), new Row("SelectWeapon9", "Arma 9", Weapons),
        };

        /// Controls that may share a binding on purpose: Jump on the ground and Dash in the air never apply at once.
        public static bool MayShare(string a, string b) =>
            (a == "Jump" && b == "Dash") || (a == "Dash" && b == "Jump");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadAtStartup() => Load(InputSystem.actions);

        public static void Load(InputActionAsset asset)
        {
            if (asset == null) return;
            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(json)) return;
            try { asset.LoadBindingOverridesFromJson(json); } catch (Exception e) { Debug.LogWarning("Controls: saved bindings ignored (" + e.Message + ")"); }
            ControlHints.NotifyChanged();
        }

        public static void Save(InputActionAsset asset)
        {
            PlayerPrefs.SetString(PrefsKey, asset.SaveBindingOverridesAsJson()); PlayerPrefs.Save();
            ControlHints.NotifyChanged();
        }

        public static void ResetAll(InputActionAsset asset)
        {
            asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefsKey); PlayerPrefs.Save();
            ControlHints.NotifyChanged();
        }

        static bool InGroup(InputBinding b, string group)
        {
            if (string.IsNullOrEmpty(b.groups)) return false;
            foreach (var g in b.groups.Split(';')) if (g.Trim() == group) return true;
            return false;
        }

        /// Binding index of slot 0..Slots-1 of the row for the device family, or -1 (composite part rows pick parts by name).
        public static int SlotIndex(InputAction action, string group, string part, int slot)
        {
            int seen = 0; var list = action.bindings;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b.isComposite) continue;
                if (part != null ? !(b.isPartOfComposite && b.name == part && InGroup(b, group)) : (b.isPartOfComposite || !InGroup(b, group))) continue;
                if (seen++ == slot) return i;
            }
            return -1;
        }

        public static string Display(InputAction action, int index)
        {
            return index < 0 ? "—" : ControlHints.Name(action, index);
        }

        public static string Label(string actionName, string part = null)
        {
            foreach (var r in Rows) if (r.Action == actionName && r.Part == part) return r.Label;
            return actionName;
        }

        /// Another binding of the same device family that already uses newPath (null when free or an allowed pair).
        public static bool FindConflict(InputActionAsset asset, string group, InputAction action, int index, string newPath, out InputAction other, out int otherIndex)
        {
            other = null; otherIndex = -1;
            if (string.IsNullOrEmpty(newPath)) return false;
            var map = asset.FindActionMap("Player", false); if (map == null) return false;
            foreach (var a in map.actions)
            {
                var list = a.bindings;
                for (int i = 0; i < list.Count; i++)
                {
                    if (a == action && i == index) continue;
                    var b = list[i];
                    if (b.isComposite || !InGroup(b, group) || b.effectivePath != newPath) continue;
                    if (a == action ? false : MayShare(a.name, action.name)) continue;
                    other = a; otherIndex = i; return true;
                }
            }
            return false;
        }

        public static void Assign(InputActionAsset asset, InputAction action, int index, string path)
        {
            action.ApplyBindingOverride(index, path); Save(asset);
        }

        /// Gives the other binding the control this slot had, then assigns the new control here.
        public static bool CanSwap(InputAction action, int index) => !string.IsNullOrEmpty(action.bindings[index].effectivePath);

        public static void Swap(InputActionAsset asset, InputAction action, int index, string newPath, InputAction other, int otherIndex)
        {
            string previous = action.bindings[index].effectivePath;
            if (string.IsNullOrEmpty(previous)) return; // never leave the other action without a control; the UI offers no swap here
            other.ApplyBindingOverride(otherIndex, previous);
            action.ApplyBindingOverride(index, newPath);
            Save(asset);
        }

        /// Interactive capture of the next control. Esc (keyboard) or Start (gamepad) cancels; Esc and P are never bindable.
        public static InputActionRebindingExtensions.RebindingOperation Begin(InputAction action, int index, string group, Action<string> captured, Action cancelled)
        {
            bool pad = group == ControlHints.GamepadGroup;
            string result = null;
            action.Disable();
            var op = action.PerformInteractiveRebinding(index)
                .OnMatchWaitForAnother(0.1f)
                .OnApplyBinding((o, path) => result = path)
                .OnComplete(o => { o.Dispose(); action.Enable(); captured?.Invoke(result); })
                .OnCancel(o => { o.Dispose(); action.Enable(); cancelled?.Invoke(); });
            if (pad)
            {
                op.WithControlsHavingToMatchPath("<Gamepad>").WithCancelingThrough("<Gamepad>/start")
                  .WithControlsExcluding("<Gamepad>/leftStick").WithControlsExcluding("<Gamepad>/rightStick")
                  .WithControlsExcluding("<Gamepad>/leftStick/*").WithControlsExcluding("<Gamepad>/rightStick/*")
                  .WithControlsExcluding("<Gamepad>/select").WithControlsExcluding("<Gamepad>/start");
            }
            else
            {
                op.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>").WithCancelingThrough("<Keyboard>/escape")
                  .WithControlsExcluding("<Keyboard>/p").WithControlsExcluding("<Keyboard>/anyKey")
                  .WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta").WithControlsExcluding("<Mouse>/scroll")
                  .WithControlsExcluding("<Mouse>/scroll/*").WithControlsExcluding("<Mouse>/clickCount");
            }
            return op.Start();
        }
    }
}
