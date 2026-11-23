using UnityEngine;

namespace IronRequiem
{
    public enum EnemyEyeState { Patrol, Alert, Attack, Warning, Dead }
    public enum EnemyEyeSize { Standard, Large }

    /// Visual pass F3 (21/11/2026), pure data and maths for the enemy eyes (no Unity objects, EditMode testable).
    /// Direction approved (01_DIRECCION b2-b6 + option A): eyes are ALWAYS red; the state is carried by intensity and pulse.
    ///   Patrol  dim and steady          HDR 1.6, small halo
    ///   Alert   medium, slow 1.5 Hz     HDR 2.6 (x 0.75..1.25), medium halo
    ///   Attack  maximum, steady         HDR 4, large halo
    ///   Warning two flashes toward white-hot in the last 0.35 s before a shot (replaces the old 14 Hz strobe)
    ///   Dead    off
    /// Caps: HDR never above MaxHdr (the campaign bloom has threshold 1 and clamp 12); sizes are world metres with explicit
    /// caps and are never derived from lossyScale.
    public static class EnemyEyeStyle
    {
        /// Pure red base. It is close to the door "BLOQUEADA" red (1; 0.03; 0.02, 04_PUERTAS P2.1) on purpose (21/11/2026): the eye is told apart by shape, not hue (small round point with a halo, head height, moves with the robot, never a bar or a rotating beacon).
        public static readonly Color BaseColor = new Color(1f, .04f, .03f, 1f); // no blue: (1; .05; .12) read pink / magenta with additive blend + bloom
        /// Hue the eye is pushed toward during the shot warning flashes (lighter red, still capped at MaxHdr).
        public static readonly Color WarningHue = new Color(1f, .22f, .14f, 1f); // red-orange, never white or pink

        public const float MaxHdr = 4f;
        public const float PatrolHdr = 1.25f, AlertHdr = 2.3f, AttackHdr = 2.8f; // 22/11/2026: a little dimmer on request (were 1.8 / 3.4 / 4) // states differ by SIZE and RHYTHM as well as intensity
        public const float AlertPulseHz = 1.5f, AlertPulseDepth = .45f; // alert intensity oscillates between x0.55 and x1
        public const float WarningLead = .35f;

        public const float CoreCapStandard = .20f, HaloCapStandard = .9f; // core cap .16 -> .20 (21/11/2026): 6 px at 25 m was under the visible-eye criterion
        public const float CoreCapLarge = .30f, HaloCapLarge = 1.6f;
        public const float LargeScale = 1.8f;

        const float CoreMinPixels = 6f, HaloMinPixels = 11f, ReferenceHeight = 1080f; // far eye slightly larger than real so it reads at 25 m, but red

        public static float CoreCap(EnemyEyeSize size) => size == EnemyEyeSize.Large ? CoreCapLarge : CoreCapStandard;
        public static float HaloCap(EnemyEyeSize size) => size == EnemyEyeSize.Large ? HaloCapLarge : HaloCapStandard;

        /// Emissive multiplier of the core (the particle material carries MaxHdr, the per-particle alpha carries hdr / MaxHdr).
        public static float Hdr(EnemyEyeState state, float time)
        {
            switch (state)
            {
                case EnemyEyeState.Patrol: return PatrolHdr;
                case EnemyEyeState.Alert: return Mathf.Min(MaxHdr, AlertHdr * PulseMultiplier(state, time));
                case EnemyEyeState.Attack: return AttackHdr;
                case EnemyEyeState.Warning: return AttackHdr;
                default: return 0f;
            }
        }

        public static float PulseMultiplier(EnemyEyeState state, float time) =>
            state == EnemyEyeState.Alert ? 1f - AlertPulseDepth * (.5f - .5f * Mathf.Sin(2f * Mathf.PI * AlertPulseHz * Sane(time, 0f, -1e6f, 1e6f))) : 1f;

        /// Halo strength, as in the approved mock-up (E_F1Eyes): 0.55 / 0.8 / 1.15.
        public static float HaloStrength(EnemyEyeState state)
        {
            switch (state)
            {
                case EnemyEyeState.Patrol: return .25f; // almost no halo
                case EnemyEyeState.Alert: return .6f;
                case EnemyEyeState.Attack: case EnemyEyeState.Warning: return 1.15f;
                default: return 0f;
            }
        }

        /// Halo emissive multiplier: 1.1 x strength, follows the alert pulse. Never above MaxHdr.
        public static float HaloHdr(EnemyEyeState state, float time) => Mathf.Min(MaxHdr, .78f * HaloStrength(state) * PulseMultiplier(state, time));

        /// Core diameter per state (metres): small dot / medium / full size (= CoreCapStandard).
        static float BaseCore(EnemyEyeState state) => state == EnemyEyeState.Patrol ? .09f : state == EnemyEyeState.Alert ? .14f : state == EnemyEyeState.Dead ? 0f : CoreCapStandard;

        public static float CoreSize(EnemyEyeState state, EnemyEyeSize size)
        {
            float s = BaseCore(state);
            return Mathf.Min(CoreCap(size), s * (size == EnemyEyeSize.Large ? LargeScale : 1f));
        }

        public static float HaloSize(EnemyEyeState state, EnemyEyeSize size)
        {
            float core = BaseCore(state);
            float s = core * (2.6f + 2.6f * HaloStrength(state)) * (size == EnemyEyeSize.Large ? LargeScale : 1f);
            return Mathf.Min(HaloCap(size), s);
        }

        /// World size of one pixel at that distance (reference 1080p).
        public static float PixelWorldSize(float distance, float fovDegrees) =>
            2f * Mathf.Max(0f, Sane(distance, 0f, 0f, 1e5f)) * Mathf.Tan(Mathf.Deg2Rad * Sane(fovDegrees, 60f, 5f, 170f) * .5f) / ReferenceHeight;

        /// State size raised to the angular minimum (about 2.5 px core / 5 px halo at 1080p), then capped. Beyond roughly 60 m
        /// the cap wins: a far eye is a sub-3 px dot, never a giant glow.
        public static float FinalCoreSize(EnemyEyeState state, EnemyEyeSize size, float distance, float fovDegrees)
        {
            float basis = CoreSize(state, size); if (basis <= 0f) return 0f;
            return Mathf.Clamp(Mathf.Max(basis, CoreMinPixels * PixelWorldSize(distance, fovDegrees)), .02f, CoreCap(size));
        }
        public static float FinalHaloSize(EnemyEyeState state, EnemyEyeSize size, float distance, float fovDegrees)
        {
            float basis = HaloSize(state, size); if (basis <= 0f) return 0f;
            return Mathf.Clamp(Mathf.Max(basis, HaloMinPixels * PixelWorldSize(distance, fovDegrees)), .04f, HaloCap(size));
        }

        /// Halo visibility with distance: fades in over the first 2.5 m (a face-to-face halo must not wash the screen) and
        /// loses at most half of its strength between 30 and 60 m (it must not disappear at 25 m).
        public static float HaloDistanceFade(float distance)
        {
            float d = Sane(distance, 0f, 0f, 1e5f);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.6f, 2.5f, d)) * (1f - .5f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 60f, d)));
        }

        // ---- hot centre: small warm-white point in the middle of the red core (an incandescent LED). Gives the eye the luma that
        // pure red cannot (Rec.709 luma of red <= 54) while the red core and halo still dominate the area.
        public static readonly Color HotHue = new Color(1f, .22f, .14f, 1f); // red-orange centre (HSV hue ~0.016, saturation 0.86): never pink, never white
        public const float HotSizeRatio = .28f, HotCapRatio = .35f, HotMinPixels = 2.5f;

        /// Hot centre emissive multiplier, capped at MaxHdr. Patrol is dim but present, Attack/Warning maximum.
        public static float HotHdr(EnemyEyeState state, float time)
        {
            switch (state)
            {
                case EnemyEyeState.Patrol: return 1.15f;
                case EnemyEyeState.Alert: return 1.7f;
                case EnemyEyeState.Attack: case EnemyEyeState.Warning: return 2.1f;
                default: return 0f;
            }
        }
        public static float HotCap(EnemyEyeSize size) => CoreCap(size) * HotCapRatio;
        public static float HotSize(EnemyEyeState state, EnemyEyeSize size) => Mathf.Min(HotCap(size), CoreSize(state, size) * HotSizeRatio);

        /// At least 3 px at 1080p (up to its cap), never larger than the red core it sits in. Beyond ~45 m the cap wins and it shrinks.
        public static float FinalHotSize(EnemyEyeState state, EnemyEyeSize size, float distance, float fovDegrees)
        {
            float basis = HotSize(state, size); if (basis <= 0f) return 0f;
            float hot = Mathf.Clamp(Mathf.Max(basis, HotMinPixels * PixelWorldSize(distance, fovDegrees)), .01f, HotCap(size));
            return Mathf.Min(hot, FinalCoreSize(state, size, distance, fovDegrees));
        }

        /// Two humps over the lead window: t is the time since the window opened (0..lead).
        public static float ShotFlash(float t, float lead)
        {
            if (t < 0f || lead <= 0f || t >= lead) return 0f;
            return Mathf.Sin(Mathf.PI * Mathf.Repeat(t / lead * 2f, 1f));
        }

        /// Hue of the eye (colour without intensity).
        public static Color Hue(EnemyEyeState state, float flash) =>
            state == EnemyEyeState.Warning ? Color.Lerp(BaseColor, WarningHue, Mathf.Clamp01(flash) * .85f) : BaseColor;

        /// Final linear HDR colour of the core (hue x hdr). Every channel is at most MaxHdr.
        public static Color EyeColor(EnemyEyeState state, float time, float flash)
        {
            var hue = Hue(state, flash); float hdr = Hdr(state, time);
            return new Color(Mathf.Min(MaxHdr, hue.r * hdr), Mathf.Min(MaxHdr, hue.g * hdr), Mathf.Min(MaxHdr, hue.b * hdr), 1f);
        }

        /// NaN / Infinity safe clamp.
        public static float Sane(float v, float fallback, float min, float max) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp(v, min, max);
        public static bool IsFinite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
}
