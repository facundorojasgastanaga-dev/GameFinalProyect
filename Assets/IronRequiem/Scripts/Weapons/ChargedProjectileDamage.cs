using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    [RequireComponent(typeof(ProjectileStandard))]
    public sealed class ChargedProjectileDamage : MonoBehaviour, IProjectileChargeReceiver
    {
        [SerializeField, Min(0)] float minimumDamage = 25;
        [SerializeField, Min(0)] float maximumDamage = 65;
        [SerializeField] Transform energyVisual;
        [SerializeField] TrailRenderer energyTrail;
        [SerializeField, Min(.001f)] float minimumRadius = .04f;
        [SerializeField, Min(.001f)] float maximumRadius = .14f;
        public float MinimumDamage => minimumDamage;
        public float MaximumDamage => maximumDamage;
        public Transform EnergyVisual => energyVisual;
        public float MinimumRadius => minimumRadius;
        public float MaximumRadius => maximumRadius;

        public void ConfigureVisual(Transform visual, TrailRenderer trail)
        { energyVisual = visual; energyTrail = trail; }

        public void ApplyCharge(float normalizedPower)
        {
            float power = Mathf.Clamp01(normalizedPower);
            var projectile = GetComponent<ProjectileStandard>();
            projectile.Damage = Mathf.Lerp(minimumDamage, Mathf.Max(minimumDamage, maximumDamage), power);
            // ProjectileStandard uses a world-space sweep radius, independently of mesh scale.
            projectile.Radius = Mathf.Lerp(minimumRadius, Mathf.Max(minimumRadius, maximumRadius), power);
            float diameter = projectile.Radius * 2;
            if (energyVisual) energyVisual.localScale = Vector3.one * diameter;
            if (energyTrail) energyTrail.widthMultiplier = diameter;
        }
    }
}
