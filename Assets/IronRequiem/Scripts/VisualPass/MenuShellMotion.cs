using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Industrial menu shell: open transition (fade + scale), drifting hull seams and a short
    /// fade/slide when the visible sub-panel changes. Unscaled time (works while paused). Zero alloc per frame.
    [RequireComponent(typeof(RectTransform))]
    public sealed class MenuShellMotion : MonoBehaviour
    {
        [SerializeField] RectTransform frame;
        [SerializeField] RectTransform[] panels;
        [SerializeField] RectTransform[] seams;
        [SerializeField, Min(.05f)] float openSeconds = .24f;
        [SerializeField, Min(.05f)] float panelSeconds = .16f;
        [SerializeField] float seamDrift = 40f;
        CanvasGroup group; CanvasGroup[] panelGroups; Vector2[] panelBase; Vector2[] seamBase;
        int shown = -1; float open, panelT = 1;

        public void Configure(RectTransform consoleFrame, RectTransform[] subPanels, RectTransform[] hullSeams)
        { frame = consoleFrame; panels = subPanels; seams = hullSeams; }

        void Awake()
        {
            group = frame ? frame.GetComponent<CanvasGroup>() : null;
            if (frame && !group) group = frame.gameObject.AddComponent<CanvasGroup>();
            panelGroups = new CanvasGroup[panels.Length]; panelBase = new Vector2[panels.Length];
            for (int i = 0; i < panels.Length; i++)
            {
                if (!panels[i]) continue;
                panelGroups[i] = panels[i].GetComponent<CanvasGroup>(); if (!panelGroups[i]) panelGroups[i] = panels[i].gameObject.AddComponent<CanvasGroup>();
                panelBase[i] = panels[i].anchoredPosition;
            }
            seamBase = new Vector2[seams.Length]; for (int i = 0; i < seams.Length; i++) if (seams[i]) seamBase[i] = seams[i].anchoredPosition;
        }

        void OnEnable() { open = 0; shown = -1; Apply(0); }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f), t = Time.unscaledTime;
            if (open < 1) { open = Mathf.Min(1, open + dt / openSeconds); Apply(open); }
            for (int i = 0; i < seams.Length; i++)
                if (seams[i]) seams[i].anchoredPosition = seamBase[i] + new Vector2(Mathf.Repeat(t * seamDrift, 310f), 0);
            int active = -1;
            for (int i = 0; i < panels.Length; i++) if (panels[i] && panels[i].gameObject.activeSelf) { active = i; break; }
            if (active != shown) { shown = active; panelT = shown >= 0 && open >= 1 ? 0 : 1; }
            if (panelT < 1 && shown >= 0)
            {
                panelT = Mathf.Min(1, panelT + dt / panelSeconds);
                float k = 1 - (1 - panelT) * (1 - panelT);
                panelGroups[shown].alpha = k;
                panels[shown].anchoredPosition = panelBase[shown] + new Vector2(28 * (1 - k), 0);
            }
        }

        void Apply(float x)
        {
            float k = 1 - (1 - x) * (1 - x) * (1 - x);
            if (group) group.alpha = k;
            if (frame) frame.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, k);
            if (x >= 1) for (int i = 0; i < panels.Length; i++) if (panelGroups != null && panelGroups[i] && panels[i].gameObject.activeSelf && panelT >= 1) panelGroups[i].alpha = 1;
        }
    }
}
