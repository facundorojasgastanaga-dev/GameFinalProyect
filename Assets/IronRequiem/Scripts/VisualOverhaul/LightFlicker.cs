using UnityEngine;

namespace IronRequiem
{
    // Visual overhaul (07/11/2026): cheap failing-fixture flicker. Deterministic hash noise, no allocation.
    // Swaps the fixture between two shared materials (lit / dead) so the SRP Batcher path is kept; the light's
    // intensity follows. Runs at a fixed 20 Hz step and only touches the renderer when the on/off state changes.
    public sealed class LightFlicker : MonoBehaviour
    {
        [SerializeField] Light target;
        [SerializeField] Renderer fixture;
        [SerializeField] Material litMaterial, deadMaterial;
        [SerializeField, Range(0, 1)] float outageChance = .18f;
        [SerializeField] int seed = 1;
        float baseIntensity, clock; int step; bool on = true;
        public bool IsOn => on;
        public void Configure(Light light, Renderer view, Material lit, Material dead, int noiseSeed) { target = light; fixture = view; litMaterial = lit; deadMaterial = dead; seed = noiseSeed; }
        void Awake() { if (target) baseIntensity = target.intensity; }
        void Update()
        {
            clock += Time.deltaTime; if (clock < .05f) return; clock = 0; step++;
            // Bursts: a slow hash picks "unstable" windows (~2 s); inside them a fast hash toggles the tube.
            bool unstable = Hash(step / 40, seed) < outageChance * 2.2f;
            bool next = !unstable || Hash(step, seed * 7 + 3) > .45f;
            float level = next ? (unstable ? .55f + .45f * Hash(step, seed + 11) : 1f) : .04f;
            if (target) target.intensity = baseIntensity * level;
            if (next != on) { on = next; if (fixture) fixture.sharedMaterial = on ? litMaterial : deadMaterial; }
        }
        static float Hash(int x, int s) { unchecked { uint h = (uint)(x * 374761393 + s * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
    }
}
