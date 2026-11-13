using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronRequiem.Tests
{
    // The user can keep Codex in front while virtual-device integration tests run.
    // Clone settings in memory: never edit the project's InputSettings asset.
    [SetUpFixture]
    public sealed class InputFocusTestScope
    {
        InputSettings previous, temporary;
        [OneTimeSetUp] public void Begin()
        {
            previous = InputSystem.settings; temporary = Object.Instantiate(previous); temporary.hideFlags = HideFlags.DontSave;
            temporary.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            temporary.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = temporary;
        }
        [OneTimeTearDown] public void End()
        {
            if (previous) InputSystem.settings = previous;
            if (temporary) Object.DestroyImmediate(temporary);
        }
    }
}
