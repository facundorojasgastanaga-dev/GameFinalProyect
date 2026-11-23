using UnityEngine;

namespace IronRequiem
{
    /// Chat 11 · shared rules of the passive dark readability (runtime, editor tools and tests use the same ones).
    public static class VisionMaterialRules
    {
        /// Steel blue (hue ~0.61). Not white (pase visual 12, correccion b1: the white rim read as a neon negative), not the red / amber / green
        /// reserved for door states (04_PUERTAS), not the patrol-eye cyan (hue 0.5).
        public static readonly Color Tint = new Color(.42f, .62f, 1f);
        public const float RimPower = 2.8f; // 4.5 -> 2.8 (pase visual 12, revision independiente): the narrow fresnel read as a jagged neon outline; a wider, weaker one reads as a cool fill light // narrow fresnel: an outline, not a white body (F4 review)
        /// Linear HDR multipliers of Tint (written with SetVector: what the shader receives): lit → dark surroundings.
        /// Lit surroundings = no extra rim. Pase visual 12 b1: half the previous intensity (1.6 → 0.8); max with the 150 % setting = 1.2 (≤ 4.0 HDR, 05_ILUMINACION §L1.3).
        public const float RimLit = 0.16f, RimDark = 0.6f; // a faint floor in lit rooms: with the rooms brightened the robot matched its background (contrast ~1.1 at 15 m)
        public static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        public static readonly int RimPowerId = Shader.PropertyToID("_RimPower");

        /// Body surfaces only: never eyes, status lamps, glass, lasers or other emissive / transparent parts.
        public static bool IsBodyMaterial(Material m)
        {
            if (!m || !m.shader) return false;
            if (m.renderQueue >= 2450) return false; // transparent / alpha-tested overlays
            string s = m.shader.name;
            if (s.IndexOf("Particle", System.StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("Unlit", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (m.IsKeywordEnabled("_EMISSION") && m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > .4f) return false;
            string n = m.name;
            foreach (var skip in Skip)
                if (n.IndexOf(skip, System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return m.HasProperty("_BaseMap") || m.HasProperty("_BaseColor");
        }

        static readonly string[] Skip = { "Eye", "Status", "Glass", "Lazer", "Laser", "Muzzle", "Flash", "Shadow", "Trail", "Spark", "Smoke", "Health", "Lens", "Indicator", "RescueOrange", "_Saw" };
        /// Renderers that show state (eyes, indicators, lenses) keep their own look.
        public static bool IsBodyRenderer(Renderer r)
        {
            if (!r || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) return false;
            foreach (var skip in Skip) if (r.name.IndexOf(skip, System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return true;
        }

        /// True for the hues reserved to doors (red, amber, green) at a saturation that reads as a colour.
        public static bool IsReservedColor(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (s < .35f || v < .05f) return false;
            return h < .05f || h > .95f || (h >= .05f && h < .15f) || (h >= .2f && h <= .45f);
        }

        /// Far reading (pase visual 12): beyond FarStart the rim widens and strengthens so a small, distant robot reads as a
        /// cool lit figure against the brightened rooms. Max = (0.6 * 2.6 + 0.3) * 1.5 = 2.8 (≤ 4.0 HDR).
        public const float FarStart = 12f, FarEnd = 26f, FarGain = 2.6f, FarFloor = 0.3f, FarPower = 1.7f;
        public static float FarFactor(float distance) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(FarStart, FarEnd, distance));
        public static Color RimFor(float darkness, float intensity, float distance)
        {
            float t = FarFactor(distance), user = Mathf.Clamp(intensity, 0, 1.5f);
            return Tint * ((Mathf.Lerp(RimLit, RimDark, Mathf.Clamp01(darkness)) * Mathf.Lerp(1, FarGain, t) + FarFloor * t) * user);
        }
        public static float RimPowerFor(float distance) => Mathf.Lerp(RimPower, FarPower, FarFactor(distance));

        /// Rim colour for a darkness factor (0 lit … 1 dark) and the user intensity (0 … 1.5).
        public static Color RimFor(float darkness, float intensity) =>
            Tint * (Mathf.Lerp(RimLit, RimDark, Mathf.Clamp01(darkness)) * Mathf.Clamp(intensity, 0, 1.5f));
    }
}
