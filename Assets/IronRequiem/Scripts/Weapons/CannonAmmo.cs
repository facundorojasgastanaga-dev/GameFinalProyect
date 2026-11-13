using System;
using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    // The component/GUID is retained; its resource is now per-instance energy.
    public sealed class CannonAmmo : MonoBehaviour
    {
        [SerializeField, Min(1)] float capacity = 100;
        WeaponController equippedWeapon;
        public float Capacity => capacity;
        public float Remaining { get; private set; }
        public event Action EnergyChanged;
        void Awake() => Remaining = capacity;
        public void Bind(WeaponController weapon)
        {
            Unbind(); equippedWeapon = weapon;
            weapon.MaxAmmo = Mathf.CeilToInt(capacity);
            weapon.AutomaticReload = false; weapon.HasPhysicalBullets = false;
            weapon.GetComponent<IStolenWeaponBinding>()?.Bind(GetComponent<StolenCannon>());
            SynchronizeWeapon();
        }
        public bool TryConsume(float amount)
        {
            if (amount <= 0 || amount > Remaining + .001f) return false;
            Drain(amount); return true;
        }
        public void Drain(float amount)
        {
            if (amount <= 0 || Remaining <= 0) return;
            Remaining = Mathf.Max(0, Remaining - amount);
            SynchronizeWeapon(); EnergyChanged?.Invoke();
        }
        void SynchronizeWeapon() { if (equippedWeapon) equippedWeapon.SetAmmo(Remaining); }
        public void Unbind()
        {
            if (equippedWeapon)
            {equippedWeapon.GetComponent<IStolenWeaponBinding>()?.Bind(null);}
            equippedWeapon = null;
        }
        void OnDestroy() => Unbind();
    }
}
