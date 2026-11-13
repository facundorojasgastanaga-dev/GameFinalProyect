using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    [DisallowMultipleComponent, RequireComponent(typeof(WeaponController))]
    public sealed class WeaponHeat : MonoBehaviour, IWeaponShotGate
    {
        [SerializeField] bool heatEnabled = true;
        [SerializeField, Min(1)] float maximumHeat = 100;
        [SerializeField, Min(0)] float heatPerShot = 8;
        [SerializeField, Min(0)] float coolingDelay = .6f;
        [SerializeField, Min(0)] float coolingPerSecond = 20;
        [SerializeField, Min(0)] float recoveryHeat = 35;
        WeaponController weapon;
        float heat, lastShot = float.NegativeInfinity, lastTick;
        bool overheated;
        public bool HeatEnabled => heatEnabled && enabled;
        public float CurrentHeat { get { Tick(); return heat; } }
        public float MaximumHeat => maximumHeat;
        public float NormalizedHeat => CurrentHeat / maximumHeat;
        public bool IsOverheated { get { Tick(); return overheated && HeatEnabled; } }
        public bool CanFire => !IsOverheated;
        public string StateLabel => IsOverheated ? "SOBRECALENTADA" : NormalizedHeat >= .7f ? "CALOR ALTO" : "FRÍA / ESTABLE";
        /// v5 montage station test: sets the heat (0..1) as if just fired; it then cools normally.
        public void PreviewHeat(float normalized)
        {
            if (!HeatEnabled) return;
            Tick(); heat = Mathf.Clamp01(normalized) * maximumHeat * .999f; lastShot = lastTick = Time.time;
            if (weapon) weapon.SetHeatDisplay(heat / maximumHeat, false);
        }
        void Awake() { weapon = GetComponent<WeaponController>(); lastTick = Time.time; }
        void OnEnable() { if (!weapon) weapon = GetComponent<WeaponController>(); weapon.OnShootProcessed += AcceptedShot; }
        void OnDisable() { if (weapon) weapon.OnShootProcessed -= AcceptedShot; }
        void Update() => Tick();
        void Tick()
        {
            float now = Time.time;
            float elapsed = Mathf.Max(0, now - Mathf.Max(lastTick, lastShot + coolingDelay));
            lastTick = now;
            if (!HeatEnabled) return;
            heat = Mathf.Max(0, heat - elapsed * coolingPerSecond);
            if (overheated && heat <= Mathf.Min(recoveryHeat, maximumHeat - .001f)) overheated = false;
            weapon.SetHeatDisplay(heat / maximumHeat, heat > 0 && now > lastShot + coolingDelay);
        }
        void AcceptedShot()
        {
            if (!HeatEnabled) return;
            Tick();
            heat = Mathf.Min(maximumHeat, heat + heatPerShot);
            lastShot = lastTick = Time.time;
            if (heat >= maximumHeat) overheated = true;
            weapon.SetHeatDisplay(heat / maximumHeat, false);
        }
        void OnValidate()
        {
            maximumHeat = Mathf.Max(1, maximumHeat);
            recoveryHeat = Mathf.Clamp(recoveryHeat, 0, maximumHeat - .001f);
        }
    }
}
