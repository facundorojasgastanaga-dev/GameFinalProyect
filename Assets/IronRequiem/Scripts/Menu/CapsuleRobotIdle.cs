using UnityEngine;

namespace IronRequiem
{
    /// The protagonist in capsule C-04 starts powered OFF: no visor light, no glow, no movement.
    /// Wake() (called by the launch sequence once the camera is on its face) runs a power-up stutter, then the
    /// visor and the cyan lines of the armour stay lit and the servos take the weight.
    /// Explosions on the bridge only make the dead visor flicker faintly ("something is stirring").
    public sealed class CapsuleRobotIdle : MonoBehaviour
    {
        [SerializeField] Transform model;
        [SerializeField] Light visor;
        [SerializeField] Renderer[] glow = new Renderer[0];
        [SerializeField, ColorUsage(false, true)] Color glowColor = new Color(.6f, 2.4f, 3.2f);
        [SerializeField] float breathMeters = .005f, breathSeconds = 4.6f;
        Vector3 basePos; Quaternion baseRot; float visorBase, pulse, wakeT = -1, power; MaterialPropertyBlock block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public void Configure(Transform robot, Light visorLight, Renderer[] glowOverlay) { model = robot; visor = visorLight; glow = glowOverlay ?? new Renderer[0]; }
        public void Pulse(float amount = 1) { if (!holdDark) pulse = Mathf.Max(pulse, amount); }
        public void Wake() { if (wakeT < 0 && !holdDark) wakeT = 0; }
        // v4 JUGAR approach: no wake, and no blast twitch on the visor either, until the scene changes.
        bool holdDark;
        public bool IsDark => wakeT < 0 && (holdDark || pulse <= 0);
        public void HoldDark() { holdDark = true; pulse = 0; wakeT = -1; Apply(0); }
        public bool IsAwake => wakeT >= 0;
        // Hangar intro (12/11/2026): the cinematic poses the rig itself and dims/sweeps the armour lines.
        public bool PoseDriven { get; set; }
        public float GlowScale { get; set; } = 1;
        /// Restarts the power-up stutter (visor flicker that stabilises), e.g. at the start of the wake-up cinematic.
        public void Reboot() { wakeT = 0; }
        /// Visor and glow off and the component stops (the cinematic hands the body over to the player).
        public void PowerDown() { Apply(0); enabled = false; }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (model) { basePos = model.localPosition; baseRot = model.localRotation; }
            if (visor) { visorBase = visor.intensity; visor.intensity = 0; }
            Apply(0);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f), t = Time.unscaledTime;
            pulse = Mathf.MoveTowards(pulse, 0, dt * 1.6f);
            if (wakeT < 0)
            {
                // Powered off: perfectly still; a nearby blast makes the visor twitch for a moment.
                float flick = pulse > 0 && Mathf.Repeat(t * 23f, 1) < .4f ? pulse * .18f : 0;
                Apply(flick);
                return;
            }
            wakeT += dt;
            // Power-up: catch, drop, catch, drop, then ramp and hold.
            float w = wakeT;
            power = w < .12f ? .7f : w < .26f ? .03f : w < .33f ? .9f : w < .5f ? .08f : Mathf.Clamp01((w - .5f) / .35f) * (1 + .15f * Mathf.Exp(-(w - .85f) * 6f));
            Apply(power);
            float lift = Mathf.SmoothStep(0, 1, Mathf.Clamp01((wakeT - .4f) / 1.2f));
            float breath = Mathf.Sin(t * Mathf.PI * 2 / breathSeconds) * lift;
            if (model && !PoseDriven)
            {
                model.localPosition = basePos + Vector3.up * (lift * .045f) + Vector3.back * (breath * breathMeters);
                model.localRotation = baseRot * Quaternion.Euler(breath * .3f - lift * 1.5f, 0, 0);
            }
        }

        void Apply(float level)
        {
            if (visor) visor.intensity = visorBase * level;
            if (glow.Length == 0) return;
            var c = glowColor * (level * GlowScale); c.a = 1;
            block.SetColor(BaseColorId, c);
            foreach (var r in glow) if (r) { r.SetPropertyBlock(block); r.enabled = level > .001f; }
        }
    }
}
