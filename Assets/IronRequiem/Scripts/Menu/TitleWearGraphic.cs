using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Wear drawn over the menu title in the colour of the plate behind it, so it reads as missing paint: one stencil
    /// slit per line (the "machine lettering" cut), diagonal scratches and small chips. Deterministic (hash, no Random),
    /// built once from the title's text geometry. Child of the title, so it shares its local space.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TitleWearGraphic : MaskableGraphic
    {
        TMP_Text title;
        public void Configure(TMP_Text text, Color plate) { title = text; color = plate; raycastTarget = false; SetVerticesDirty(); }

        static float Hash(int x, int s) { unchecked { uint h = (uint)(x * 374761393 + s * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!title || title.textInfo == null) return;
            var info = title.textInfo; Color c = color;
            for (int li = 0; li < info.lineCount; li++)
            {
                var line = info.lineInfo[li]; if (line.visibleCharacterCount == 0) continue;
                var first = info.characterInfo[line.firstVisibleCharacterIndex]; var last = info.characterInfo[line.lastVisibleCharacterIndex];
                float x0 = first.bottomLeft.x, x1 = last.topRight.x, y0 = first.bottomLeft.y, h = first.topRight.y - first.bottomLeft.y, w = x1 - x0;
                if (h < 4 || w < 4) continue;
                // stencil slit across the whole word, low on the letters
                float sy = y0 + h * .27f, st = Mathf.Max(2f, h * .034f);
                ThermalGaugeGraphic.Quad(vh, new Vector2(x0 - 4, sy + st), new Vector2(x0 - 4, sy), new Vector2(x1 + 4, sy), new Vector2(x1 + 4, sy + st), c);
                // scratches
                for (int i = 0; i < 11; i++)
                {
                    int k = li * 97 + i;
                    var a = new Vector2(x0 + w * Hash(k, 1), y0 + h * Hash(k, 2));
                    float ang = (Hash(k, 3) > .25f ? 38f : -52f) + (Hash(k, 4) - .5f) * 34f, len = h * Mathf.Lerp(.12f, .5f, Hash(k, 5)), th = Mathf.Lerp(.9f, 2.3f, Hash(k, 6));
                    var d = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)); var n = new Vector2(-d.y, d.x) * th * .5f; var b = a + d * len;
                    var faint = c; faint.a = c.a * Mathf.Lerp(.55f, 1f, Hash(k, 7));
                    ThermalGaugeGraphic.Quad(vh, a + n, a - n, b - n * .3f, b + n * .3f, faint);
                }
                // chips along the top and bottom edges of the letters
                for (int i = 0; i < 9; i++)
                {
                    int k = li * 131 + i + 500;
                    float cx = x0 + w * Hash(k, 1), cy = Hash(k, 2) > .5f ? y0 + h - Hash(k, 3) * h * .08f : y0 + Hash(k, 3) * h * .08f, s = Mathf.Lerp(2.5f, 7.5f, Hash(k, 4));
                    ThermalGaugeGraphic.Quad(vh, new Vector2(cx - s, cy + s * Hash(k, 5)), new Vector2(cx - s * Hash(k, 6), cy - s), new Vector2(cx + s, cy - s * Hash(k, 7)), new Vector2(cx + s * Hash(k, 8), cy + s), c);
                }
            }
        }
    }
}
