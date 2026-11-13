using UnityEngine;

namespace IronRequiem
{
    /// Hangar menu (11/11/2026): drives lights and emissive renderers of one fixture together.
    /// Modes: Steady, Hum (slight buzz), Failing (half-dead tube: unstable windows with drop-outs), Blink (indicator).
    /// Deterministic hash noise, unscaled time, MaterialPropertyBlock emission (shared materials untouched). No allocation per frame.
    public sealed class MenuFlicker : MonoBehaviour
    {
        public enum Mode { Steady, Hum, Failing, Blink }
        [SerializeField] Light[] lights = new Light[0];
        [SerializeField] Renderer[] emissive = new Renderer[0];
        [SerializeField, ColorUsage(false, true)] Color emission = new Color(.2f, 1.6f, 2f);
        [SerializeField] Mode mode = Mode.Hum;
        [SerializeField, Range(0, 1)] float outage = .25f;
        [SerializeField, Min(.05f)] float blinkHz = 1.2f;
        [SerializeField, Range(0, 1)] float floor = .04f;
        [SerializeField] int seed = 1;
        float[] baseIntensity; MaterialPropertyBlock block; float level = 1, boost, stable, stableTarget, clock, smoothed = 1; int step;
        bool hold; float holdLevel;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor"), BaseColorId = Shader.PropertyToID("_BaseColor");

        public float Level => level;
        public Mode CurrentMode => mode;

        public void Configure(Light[] fixtureLights, Renderer[] views, Color hdrEmission, Mode flickerMode, float outageChance, int noiseSeed, float blinkRate = 1.2f)
        { lights = fixtureLights ?? new Light[0]; emissive = views ?? new Renderer[0]; emission = hdrEmission; mode = flickerMode; outage = outageChance; seed = noiseSeed; blinkHz = blinkRate; }

        /// Launch sequence: blend toward a steady full output (true) or back to the authored behaviour (false).
        public void SetStable(bool on) => stableTarget = on ? 1 : 0;
        /// Momentary surge (explosion nearby, robot reaction).
        public void Pulse(float amount) => boost = Mathf.Max(boost, amount);
        /// Hard override used by the power-on stutter of the intro (level < 0 releases it).
        public void Hold(float value) { hold = value >= 0; holdLevel = value; }
        public void SetEmission(Color hdr) => emission = hdr;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            baseIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) if (lights[i]) baseIntensity[i] = lights[i].intensity;
            clock = Hash(seed, 91) * 3;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f), t = Time.unscaledTime + seed * 13.7f;
            float raw;
            switch (mode)
            {
                case Mode.Steady: raw = 1; break;
                case Mode.Hum: raw = .9f + .1f * Mathf.PerlinNoise(t * 6f, seed); if (Hash((int)(t * 9), seed) > .985f) raw = .55f; break;
                case Mode.Blink: raw = Mathf.Repeat(t * blinkHz, 1) < .55f ? 1 : floor; break;
                default:
                    clock += dt;
                    while (clock >= .05f) { clock -= .05f; step++; }
                    // ~2 s windows; inside an unstable one the tube stutters on/off at 20 Hz with partial levels.
                    bool unstable = Hash(step / 40, seed) < outage * 2.2f;
                    bool lit = !unstable || Hash(step, seed * 7 + 3) > .45f;
                    raw = lit ? (unstable ? .5f + .5f * Hash(step, seed + 11) : .92f + .08f * Mathf.PerlinNoise(t * 5, seed)) : floor;
                    break;
            }
            stable = Mathf.MoveTowards(stable, stableTarget, dt * 2.5f);
            raw = Mathf.Lerp(raw, 1.15f, stable);
            if (hold) raw = holdLevel;
            // Tubes don't snap: a few ms of glow decay keeps it believable without blurring the stutter.
            smoothed = raw > smoothed ? raw : Mathf.Lerp(smoothed, raw, 1 - Mathf.Exp(-dt * 38));
            boost = Mathf.MoveTowards(boost, 0, dt * 1.8f);
            level = smoothed * (1 + boost);
            for (int i = 0; i < lights.Length; i++) if (lights[i]) lights[i].intensity = baseIntensity[i] * level;
            if (emissive.Length > 0)
            {
                var c = emission * level; c.a = 1; block.SetColor(EmissionId, c); block.SetColor(BaseColorId, c); // Unlit glow uses _BaseColor
                for (int i = 0; i < emissive.Length; i++) if (emissive[i]) emissive[i].SetPropertyBlock(block);
            }
        }

        static float Hash(int x, int s) { unchecked { uint h = (uint)(x * 374761393 + s * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
    }
}
