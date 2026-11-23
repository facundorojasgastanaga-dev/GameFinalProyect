using TMPro;
using UnityEngine;

namespace IronRequiem
{
    /// Worn machine lettering for the menu title (IRON REQUIEM). Each letter gets a metal gradient with its own wear and
    /// some rust; a stencil slit, scratches and chips are drawn over it (TitleWearGraphic); two faulty letters stutter
    /// like a dying sign now and then and throw sparks (MenuSparksGraphic); a scan line crosses the title from time to
    /// time. Only vertex colours change: the text, its font, size and layout are untouched. Unscaled time.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TitleWornFx : MonoBehaviour
    {
        static readonly Color Top = new Color(.97f, .95f, .87f), Bottom = new Color(.60f, .60f, .56f), Rust = new Color(.50f, .23f, .07f);
        static readonly Color Ember = new Color(.20f, .07f, .02f), Surge = new Color(1f, .97f, .88f);
        TMP_Text title; MenuSparksGraphic sparks; TitleWearGraphic wear;
        float[] level = new float[0]; int faultA = -1, faultB = -1, active = -1;
        float nextEvent, eventEnd, nextStep, dip = 1, dipEnd, nextScan; bool dirty = true, painting;

        public void Configure(Color plate)
        {
            title = GetComponent<TMP_Text>();
            wear = Make<TitleWearGraphic>("IR_TitleWear"); wear.Configure(title, plate);
            sparks = Make<MenuSparksGraphic>("IR_TitleSparks"); sparks.raycastTarget = false;
        }

        T Make<T>(string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)); go.layer = gameObject.layer;
            var r = (RectTransform)go.transform; r.SetParent(transform, false); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            return go.AddComponent<T>();
        }

        void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            nextEvent = Time.unscaledTime + Random.Range(1.2f, 3f); nextScan = Time.unscaledTime + Random.Range(3f, 6f); dirty = true;
        }
        void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

        // TMP rebuilt the mesh (plain colours again): repaint at once so the plain title never shows for a frame
        // (the wear overlay is only flagged here: dirtying another graphic inside the canvas rebuild loop is not allowed)
        void OnTextChanged(Object changed) { if (changed == title && !painting) { Layout(); Paint(); wearDirty = true; } }
        bool wearDirty;

        void Layout()
        {
            var info = title.textInfo; int n = info.characterCount;
            if (level.Length != n) { level = new float[n]; for (int i = 0; i < n; i++) level[i] = 1; }
            faultA = faultB = -1; int visible = 0, total = 0;
            for (int i = 0; i < n; i++) if (info.characterInfo[i].isVisible) total++;
            for (int i = 0; i < n; i++)
            {
                if (!info.characterInfo[i].isVisible) continue;
                if (visible == 2) faultA = i; if (visible == total - 4) faultB = i;
                visible++;
            }
        }

        static float Hash(int x, int s) { unchecked { uint h = (uint)(x * 374761393 + s * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }

        void Paint()
        {
            if (!title || title.textInfo == null) return;
            painting = true;
            var info = title.textInfo; int n = Mathf.Min(info.characterCount, level.Length); float alpha = title.color.a;
            for (int i = 0; i < n; i++)
            {
                var ch = info.characterInfo[i]; if (!ch.isVisible) continue;
                var cols = info.meshInfo[ch.materialReferenceIndex].colors32; int v = ch.vertexIndex; if (cols == null || v + 3 >= cols.Length) continue;
                float worn = Mathf.Lerp(.80f, 1f, Hash(i, 1));
                Color top = Top * worn, bottom = Hash(i, 3) > .62f ? Color.Lerp(Bottom, Rust, .72f) : Bottom * worn;
                float l = level[i] * dip;
                if (l < 1) { float k = Mathf.Clamp01(l); top = Color.Lerp(Ember, top, k); bottom = Color.Lerp(Ember * .6f, bottom, k); }
                else if (l > 1) { float k = Mathf.Clamp01((l - 1) / .3f); top = Color.Lerp(top, Surge, k); bottom = Color.Lerp(bottom, Surge, k * .7f); }
                top.a = bottom.a = alpha;
                Color32 t32 = top, b32 = bottom;
                cols[v] = b32; cols[v + 1] = t32; cols[v + 2] = t32; cols[v + 3] = b32;
            }
            title.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            painting = false; dirty = false;
        }

        void SparkFrom(int index, int count, float power)
        {
            var ch = title.textInfo.characterInfo[index];
            var at = new Vector2(Random.Range(ch.bottomLeft.x, ch.topRight.x), Mathf.Lerp(ch.bottomLeft.y, ch.topRight.y, Random.Range(.1f, .7f)));
            sparks.Emit(at, count, power);
        }

        void Update()
        {
            if (!title) return;
            bool shown = title.enabled && title.gameObject.activeInHierarchy;
            if (wear && wear.enabled != shown) { wear.enabled = shown; sparks.enabled = shown; }
            if (!shown) return;
            if (wearDirty) { wearDirty = false; if (wear) wear.SetVerticesDirty(); }
            if (level.Length != title.textInfo.characterCount || faultA < 0) { title.ForceMeshUpdate(); Layout(); dirty = true; if (wear) wear.SetVerticesDirty(); }
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;

            // a faulty letter stutters for a moment
            if (active < 0 && now >= nextEvent && faultA >= 0)
            {
                active = Random.value > .5f && faultB >= 0 ? faultB : faultA; eventEnd = now + Random.Range(.25f, .75f); nextStep = 0;
                SparkFrom(active, Random.Range(7, 14), 1);
                if (Random.value < .2f) { dip = .5f; dipEnd = now + .09f; dirty = true; }
            }
            if (active >= 0 && active < level.Length)
            {
                if (now >= eventEnd) { level[active] = 1.3f; SparkFrom(active, Random.Range(3, 7), .8f); active = -1; nextEvent = now + Random.Range(2.5f, 7.5f); dirty = true; }
                else if (now >= nextStep)
                {
                    bool on = Random.value > .45f; float before = level[active];
                    level[active] = on ? Random.Range(.6f, 1.25f) : 0; nextStep = now + Random.Range(.035f, .09f); dirty = true;
                    if (on && before < .1f && Random.value < .6f) SparkFrom(active, Random.Range(2, 5), .7f);
                }
            }
            // the surge after a fault settles back to normal
            for (int i = 0; i < level.Length; i++) if (i != active && level[i] > 1) { level[i] = Mathf.MoveTowards(level[i], 1, dt * 1.6f); dirty = true; }
            if (dip < 1 && now >= dipEnd) { dip = 1; dirty = true; }
            if (now >= nextScan) { nextScan = now + Random.Range(7f, 13f); var b = title.textBounds; sparks.Sweep(new Rect(b.min.x - 6, b.min.y - 4, b.size.x + 12, b.size.y + 8)); }
            if (dirty) Paint();
        }
    }
}
