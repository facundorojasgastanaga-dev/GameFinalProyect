using UnityEngine;

namespace IronRequiem
{
    /// Presentation of a pressurisation / decontamination cycle in a campaign airlock (12/11/2026).
    /// CampaignFlow calls Begin() when the transfer starts (door behind the player closing) and Complete() when the next
    /// sector is ready and the far door opens. Lights blink amber while cycling and settle on green; steam vents run and
    /// a hydraulic hiss plays. No gameplay logic lives here.
    public sealed class AirlockCycle : MonoBehaviour
    {
        [SerializeField] Light[] lights = new Light[0];
        [SerializeField] Renderer[] lamps = new Renderer[0];
        [SerializeField] ParticleSystem[] steam = new ParticleSystem[0];
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip cycleClip, doneClip;
        [SerializeField] Color amber = new Color(1f, .55f, .12f), green = new Color(.25f, 1f, .45f);
        [SerializeField, ColorUsage(false, true)] Color amberHdr = new Color(2f, 1.0f, .2f), greenHdr = new Color(.4f, 2f, .7f);
        [SerializeField, Min(0)] float lightIntensity = 2.5f, blinkHz = 2.2f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;
        bool cycling;

        public bool Cycling => cycling;

        public void Configure(Light[] lamps0, Renderer[] lampViews, ParticleSystem[] vents, AudioSource source, AudioClip cycle, AudioClip done)
        { lights = lamps0; lamps = lampViews; steam = vents; audioSource = source; cycleClip = cycle; doneClip = done; }

        void Awake() { block = new MaterialPropertyBlock(); Apply(green, greenHdr, 1); }

        public void Begin()
        {
            if (cycling) return;
            cycling = true;
            foreach (var s in steam) if (s) s.Play(true);
            if (audioSource && cycleClip) audioSource.PlayOneShot(cycleClip, .9f);
        }

        public void Complete()
        {
            if (!cycling) return;
            cycling = false;
            foreach (var s in steam) if (s) s.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (audioSource && doneClip) audioSource.PlayOneShot(doneClip, .8f);
            Apply(green, greenHdr, 1);
        }

        public void Abort() { cycling = false; foreach (var s in steam) if (s) s.Stop(true, ParticleSystemStopBehavior.StopEmitting); Apply(green, greenHdr, 1); }

        void Update()
        {
            if (!cycling) return;
            float k = Mathf.Repeat(Time.time * blinkHz, 1) < .55f ? 1 : .15f;
            Apply(amber, amberHdr, k);
        }

        void Apply(Color c, Color hdr, float k)
        {
            foreach (var l in lights) if (l) { l.color = c; l.intensity = lightIntensity * k; }
            if (lamps.Length == 0) return;
            if (block == null) block = new MaterialPropertyBlock();
            var e = hdr * k; e.a = 1; block.SetColor(BaseColorId, e);
            foreach (var r in lamps) if (r) r.SetPropertyBlock(block);
        }
    }
}
