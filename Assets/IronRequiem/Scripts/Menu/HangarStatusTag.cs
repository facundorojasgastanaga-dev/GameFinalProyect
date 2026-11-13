using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Bottom-right capsule tag ("EN ESPERA"), with a blinking indicator. After a while idle it hints at the
    /// hostiles on the bridge; the patrol reports kills so the line reacts.
    public sealed class HangarStatusTag : MonoBehaviour
    {
        [SerializeField] TMP_Text state, detail;
        [SerializeField] Graphic icon;
        [SerializeField] IndustrialPanelGraphic plate;
        [SerializeField] float hintAfter = 9;
        Color color = new Color(1f, .55f, .12f); float blinkHz = .8f, since; int kills; bool locked; string detailIdle = "CÁPSULA C-04 · SOPORTE VITAL ESTABLE";
        static readonly string[] quips = { "AMENAZA ELIMINADA", "OBJETIVO NEUTRALIZADO", "SEÑAL HOSTIL PERDIDA", "PUENTE NORTE · DESPEJADO" };

        public void Configure(TMP_Text stateText, TMP_Text detailText, Graphic indicator, IndustrialPanelGraphic panel) { state = stateText; detail = detailText; icon = indicator; plate = panel; }

        public void Set(string text, Color tint, float hz)
        {
            color = tint; blinkHz = hz;
            if (state) { state.text = text; state.color = Color.Lerp(Color.white, tint, .25f); }
            if (plate) plate.Configure(new Color(.03f, .03f, .035f, .82f), new Color(tint.r, tint.g, tint.b, .9f), 14);
        }

        public void Lock(string text, Color tint, float hz, string detailText) { locked = true; Set(text, tint, hz); if (detail) { detail.text = detailText; detail.alpha = 1; } }

        public void ReportKill(int total)
        {
            kills = total; if (locked || !detail) return;
            detail.text = quips[(total - 1) % quips.Length] + "  ·  " + total.ToString("00");
            detail.color = new Color(1f, .6f, .2f); since = -2.5f;
        }

        void Start() { Set("EN ESPERA", color, blinkHz); if (detail) { detail.text = detailIdle; detail.color = new Color(.55f, .63f, .66f); } }

        void Update()
        {
            float t = Time.unscaledTime;
            if (icon)
            {
                float on = Mathf.Repeat(t * blinkHz, 1) < .5f ? 1 : .15f;
                icon.color = new Color(color.r, color.g, color.b, on);
            }
            if (locked || !detail) return;
            since += Time.unscaledDeltaTime;
            if (since > 0 && kills > 0 && since < .1f) { detail.text = detailIdle; detail.color = new Color(.55f, .63f, .66f); }
            if (kills == 0 && since > hintAfter)
            {
                detail.text = "SEÑALES HOSTILES · PUENTE NORTE";
                detail.color = new Color(1f, .45f, .15f, Mathf.Repeat(t * 1.4f, 1) < .6f ? 1 : .35f);
            }
        }
    }
}
