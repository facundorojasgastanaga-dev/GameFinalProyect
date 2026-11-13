using UnityEngine;

namespace IronRequiem
{
    // Visual overhaul (07/11/2026): one brief point light per pooled death burst (RobotDestructionPool slot), lit only
    // for `duration` seconds when the burst detonates. At most `MaxLit` flashes are lit at once across all slots, so a
    // multi-kill never adds more than that many realtime lights. No allocation; the light is disabled when idle.
    [RequireComponent(typeof(RobotBodyBurst))]
    public sealed class BurstFlashLight : MonoBehaviour
    {
        public const int MaxLit = 2;
        [SerializeField] Light flash;
        [SerializeField, Min(.02f)] float duration = .16f;
        [SerializeField, Min(0)] float peakIntensity = 9f;
        static int lit;
        RobotBodyBurst burst; float age = -1; bool counted;
        public static int Lit => lit;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetBudget() => lit = 0;
        public void Configure(Light light) => flash = light;
        void Awake() { burst = GetComponent<RobotBodyBurst>(); if (flash) flash.enabled = false; }
        void Update()
        {
            if (!flash) return;
            if (age < 0)
            {
                if (!burst.HasBurst) return;
                age = 0; if (lit < MaxLit) { lit++; counted = true; flash.enabled = true; }
            }
            age += Time.deltaTime;
            if (counted) { float k = 1 - Mathf.Clamp01(age / duration); flash.intensity = peakIntensity * k * k; }
            if (age >= duration) Release();
            if (!burst.HasBurst && age >= duration) age = -1;
        }
        void Release() { if (counted) { lit = Mathf.Max(0, lit - 1); counted = false; } if (flash) flash.enabled = false; }
        void OnDisable() { Release(); age = -1; }
    }
}
