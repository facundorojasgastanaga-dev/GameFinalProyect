using UnityEngine;

namespace IronRequiem
{
    /// Soft light sprites for the menu, generated once in memory (no art assets, nothing saved to disk):
    /// Soft = round halo; Ring = glow that hugs the edge of a rectangle (9-sliced, the rectangle sits 20 px inside the
    /// sprite); Band = a soft vertical stripe used as a moving sheen.
    public static class MenuGlowSprites
    {
        public const float RingInset = 20;
        static Sprite soft, ring, band;

        public static Sprite Soft => soft ? soft : soft = Make(64, 64, Vector4.zero, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y) / 32f; float a = Mathf.Clamp01(1 - d); return a * a * (3 - 2 * a) * a;
        });

        public static Sprite Ring => ring ? ring : ring = Make(64, 64, new Vector4(28, 28, 28, 28), (x, y) =>
        {
            float qx = Mathf.Abs(x) - 12, qy = Mathf.Abs(y) - 12;
            float sd = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0);
            float k = sd >= 0 ? sd / 8f : sd / 3f; return Mathf.Exp(-k * k);
        });

        public static Sprite Band => band ? band : band = Make(64, 4, Vector4.zero, (x, y) =>
        {
            float a = Mathf.Clamp01(1 - Mathf.Abs(x) / 32f); return a * a;
        });

        static Sprite Make(int w, int h, Vector4 border, System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, name = "IR_MenuGlow" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x + .5f - w * .5f, y + .5f - h * .5f)) * 255));
            tex.SetPixels32(px); tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.HideAndDontSave; return sprite;
        }

        /// Non-interactive image child, centred, used by every light effect of the menu.
        public static UnityEngine.UI.Image Child(string name, Transform parent, Sprite sprite, Color color, bool sliced = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)); go.layer = parent.gameObject.layer;
            var r = (RectTransform)go.transform; r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            var img = go.AddComponent<UnityEngine.UI.Image>(); img.sprite = sprite; img.color = color; img.raycastTarget = false;
            if (sliced) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }
    }
}
