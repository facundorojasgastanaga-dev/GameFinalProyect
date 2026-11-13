using UnityEngine;

namespace IronRequiem
{
    /// Scales a fixed-size panel so it fills a fraction of its parent's height (and never more than a fraction of
    /// its width), centred. Used by the options console so it reads at the same size at 1280x720, 1920x1080 and
    /// 2560x1440 and in the narrow main-menu frame as well as in the pause menu. Presentation only.
    [RequireComponent(typeof(RectTransform))]
    public sealed class ScreenFitPanel : MonoBehaviour
    {
        [SerializeField, Range(.2f, 1f)] float heightFraction = .76f;
        [SerializeField, Range(.2f, 1f)] float widthFraction = .92f;
        [SerializeField, Min(.1f)] float maxScale = 3f;
        RectTransform self, parent; Vector2 lastParent, lastSelf;

        public void Configure(float height, float width) { heightFraction = height; widthFraction = width; Fit(); }
        public float CurrentScale => self ? self.localScale.x : 1;
        /// Fits again now (e.g. right after the canvas changed resolution, before the next LateUpdate).
        public void Refit() => Fit();

        void OnEnable() { self = (RectTransform)transform; parent = transform.parent as RectTransform; lastParent = Vector2.zero; Fit(); }
        void OnRectTransformDimensionsChange() => Fit();
        void LateUpdate()
        {
            if (!parent) return;
            if (parent.rect.size != lastParent || self.rect.size != lastSelf) Fit();
        }

        void Fit()
        {
            if (!self) self = (RectTransform)transform;
            if (!parent) parent = transform.parent as RectTransform;
            if (!parent) return;
            var p = parent.rect.size; var s = self.rect.size;
            if (p.x <= 0 || p.y <= 0 || s.x <= 0 || s.y <= 0) return;
            float scale = Mathf.Min(maxScale, Mathf.Min(p.y * heightFraction / s.y, p.x * widthFraction / s.x));
            self.anchorMin = self.anchorMax = self.pivot = new Vector2(.5f, .5f);
            self.anchoredPosition = Vector2.zero;
            self.localScale = new Vector3(scale, scale, 1);
            lastParent = p; lastSelf = s;
        }
    }
}
