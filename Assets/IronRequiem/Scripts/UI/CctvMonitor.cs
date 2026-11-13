using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Options-console CCTV monitor (12/11/2026). While this object is active it renders the active CctvFeed into a
    /// 256x144 render texture at ~15 fps (manual Camera.Render, no post-processing), shows the camera label and a clock,
    /// and adds occasional glitches. Without a feed it shows static and "SIN SEÑAL". Unscaled time, so it also runs in pause.
    public sealed class CctvMonitor : MonoBehaviour
    {
        [SerializeField] RawImage screen;
        [SerializeField] TMP_Text label, clock;
        [SerializeField, Min(1)] float framesPerSecond = 15f;
        [SerializeField] Vector2Int resolution = new Vector2Int(256, 144);
        static readonly int GlitchId = Shader.PropertyToID("_Glitch"), NoSignalId = Shader.PropertyToID("_NoSignal");
        RenderTexture target; Material material; float nextFrame, glitchUntil, nextGlitch; int lastSecond = -1;
        CctvFeed feed;

        public bool HasSignal => feed;
        public RenderTexture Target => target;

        public void Configure(RawImage image, TMP_Text title, TMP_Text time) { screen = image; label = title; clock = time; }

        void OnEnable()
        {
            if (!screen) return;
            material = screen.material ? new Material(screen.material) : null; // instance: the glitch value is per monitor
            if (material) screen.material = material;
            target = new RenderTexture(resolution.x, resolution.y, 16, RenderTextureFormat.ARGB32) { name = "CCTV", filterMode = FilterMode.Point };
            target.Create(); screen.texture = target;
            nextFrame = 0; nextGlitch = Time.unscaledTime + Random.Range(1.5f, 4f); lastSecond = -1;
            Bind();
        }

        void OnDisable()
        {
            if (feed && feed.Camera && feed.Camera.targetTexture == target) feed.Camera.targetTexture = null;
            feed = null;
            if (target) { target.Release(); Destroy(target); target = null; }
            if (material) { Destroy(material); material = null; }
        }

        void Bind()
        {
            feed = CctvFeed.Active;
            if (label) label.text = feed ? feed.Label : "CAM 04 · SIN SEÑAL";
            if (material) material.SetFloat(NoSignalId, feed ? 0 : 1);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (feed != CctvFeed.Active) Bind();
            if (feed && now >= nextFrame)
            {
                nextFrame = now + 1f / framesPerSecond;
                var cam = feed.Camera; var previous = cam.targetTexture;
                cam.targetTexture = target; cam.Render(); cam.targetTexture = previous;
            }
            // occasional glitches: short tearing bursts
            if (now >= nextGlitch) { glitchUntil = now + Random.Range(.08f, .3f); nextGlitch = now + Random.Range(2.5f, 6f); }
            if (material) material.SetFloat(GlitchId, now < glitchUntil ? Random.Range(.4f, 1f) : 0f);
            if (clock)
            {
                var t = System.DateTime.Now; int s = t.Hour * 3600 + t.Minute * 60 + t.Second;
                if (s != lastSecond) { lastSecond = s; clock.SetText("{0:00}:{1:00}:{2:00}", t.Hour, t.Minute, t.Second); }
            }
        }
    }
}
