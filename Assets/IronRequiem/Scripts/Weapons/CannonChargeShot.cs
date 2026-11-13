using System;
using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    [RequireComponent(typeof(WeaponController))]
    public sealed class CannonChargeShot : MonoBehaviour, IWeaponInputExtension, IStolenWeaponBinding
    {
        [SerializeField, Min(.1f)] float maximumChargeDuration = 1.2f;
        [SerializeField, Range(.01f, .95f)] float minimumChargeFraction = .2f;
        [SerializeField, Min(1)] float minimumEnergyCost = 12;
        [SerializeField, Min(1)] float maximumEnergyCost = 30;
        WeaponController weapon;
        StolenCannon module;
        float elapsed;
        bool charging;
        public bool IsCharging => charging;
        public float ChargeFraction => charging ? Mathf.Clamp01(elapsed / maximumChargeDuration) : 0;
        public float MaximumChargeDuration => maximumChargeDuration;
        public float MinimumChargeFraction => minimumChargeFraction;
        public float MinimumEnergyCost => minimumEnergyCost;
        public float MaximumEnergyCost => maximumEnergyCost;
        public float LastShotPower { get; private set; }
        public float LastEnergyCost { get; private set; }
        public event Action ChargeCancelled;
        public event Action<float> ShotReleased;
        bool CanCharge => module && module.Carrier && module.Carrier.CanUseHeldWeapon &&
            weapon.IsWeaponActive && !module.Overdrive.IsArmed && module.Ammo.Remaining >= minimumEnergyCost;

        void Awake() => weapon = GetComponent<WeaponController>();
        public void Bind(StolenCannon source) { CancelPendingShot(); module = source; }
        public void CancelPendingShot() { charging = false; elapsed = 0; ChargeCancelled?.Invoke(); }
        void LateUpdate() { if (charging && !CanCharge) CancelPendingShot(); }
        void OnDisable() => CancelPendingShot();

        public bool HandleShootInputs(bool down, bool held, bool up)
        {
            if (!CanCharge) { CancelPendingShot(); return false; }
            // Returning to this slot with the button held cannot start a new charge.
            if (down) { charging = true; elapsed = 0; }
            if (charging && held) elapsed = Mathf.Min(maximumChargeDuration, elapsed + Time.deltaTime);
            if (!up || !charging) return false;
            float fraction = ChargeFraction;
            CancelPendingShot();
            if (fraction < minimumChargeFraction) return false;
            float affordablePower = Mathf.InverseLerp(minimumEnergyCost, Mathf.Max(minimumEnergyCost, maximumEnergyCost), module.Ammo.Remaining);
            float power = Mathf.Min(Mathf.InverseLerp(minimumChargeFraction, 1, fraction), affordablePower);
            float cost = Mathf.Lerp(minimumEnergyCost, Mathf.Max(minimumEnergyCost, maximumEnergyCost), power);
            if (!weapon.TryShootSingleWithCharge(power)) return false;
            LastShotPower = power; LastEnergyCost = cost;
            ShotReleased?.Invoke(power);
            module.Ammo.TryConsume(cost);
            return true;
        }
    }
}
