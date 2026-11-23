using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace IronRequiem
{
    /// Presentation and focus repair scoped exclusively to the animated start menu.
    [DefaultExecutionOrder(-50)]
    public sealed class MainMenuPolish : MonoBehaviour
    {
        [SerializeField] MonoBehaviour view;
        [SerializeField] UnityEngine.Events.UnityEvent returnHome = new UnityEngine.Events.UnityEvent();
        public UnityEngine.Events.UnityEvent ReturnHome => returnHome;
        [SerializeField] GameObject home;
        [SerializeField] UnityEngine.UI.Button primary, options, credits;
        [SerializeField] UnityEngine.UI.Selectable optionsFirst, creditsFirst;
        [SerializeField] GameObject optionsPanel, creditsPanel;
        [SerializeField] HangarLaunchSequence launch;
        InputAction cancel;
        float elapsed;
        bool wasHome, wasOptions, wasCredits;
        public bool IsReady => elapsed >= .32f;
        public bool IsLaunching => launch && launch.IsPlaying;

        public void Configure(MonoBehaviour owner, GameObject homePanel, UnityEngine.UI.Button play,
            UnityEngine.UI.Button settings, UnityEngine.UI.Button about, UnityEngine.UI.Selectable firstOptions,
            UnityEngine.UI.Selectable firstCredits, GameObject settingsPanel, GameObject aboutPanel,
            HangarLaunchSequence sequence)
        {
            view = owner; home = homePanel; primary = play; options = settings; credits = about;
            optionsFirst = firstOptions; creditsFirst = firstCredits; optionsPanel = settingsPanel;
            creditsPanel = aboutPanel; launch = sequence;
        }
        void Start()
        {
            cancel = InputSystem.actions.FindAction("UI/Cancel", false);
            wasHome = home.activeInHierarchy;
        }
        void Update()
        {
            if (!view || !home) return;
            if (IsLaunching) return;
            elapsed += Time.unscaledDeltaTime;
            if (cancel != null && cancel.WasPressedThisFrame() &&
                ((optionsPanel && optionsPanel.activeInHierarchy) || (creditsPanel && creditsPanel.activeInHierarchy))) returnHome.Invoke();
            bool onHome = home.activeInHierarchy;
            bool onOptions = optionsPanel && optionsPanel.activeInHierarchy;
            bool onCredits = creditsPanel && creditsPanel.activeInHierarchy;
            if (!wasHome && onHome)
            {
                var restore = wasOptions ? options : wasCredits ? credits : primary;
                if (restore && restore.IsInteractable()) restore.Select();
            }
            var system = EventSystem.current;
            if (system)
            {
                var current = system.currentSelectedGameObject;
                bool valid = current && current.activeInHierarchy;
                if (valid)
                {
                    var selectable = current.GetComponent<UnityEngine.UI.Selectable>();
                    valid = selectable && selectable.IsInteractable();
                }
                if (!valid)
                {
                    UnityEngine.UI.Selectable fallback = onHome ? primary : onOptions ? optionsFirst : onCredits ? creditsFirst : null;
                    if (fallback && fallback.IsInteractable()) fallback.Select();
                }
            }
            wasHome = onHome; wasOptions = onOptions; wasCredits = onCredits;
        }
    }
}
