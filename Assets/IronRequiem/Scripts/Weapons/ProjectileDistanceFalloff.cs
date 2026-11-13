using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    // Damage falloff by distance from the shot origin to the hit point. Full damage up to
    // fullDamageDistance, linear down to minimumMultiplier at zeroFalloffDistance and beyond.
    // Range itself is limited by ProjectileStandard.MaxLifeTime and Speed on the same prefab.
    [RequireComponent(typeof(ProjectileStandard))]
    public sealed class ProjectileDistanceFalloff : MonoBehaviour, IProjectileHitDamageModifier
    {
        [SerializeField, Min(0)] float fullDamageDistance = 6;
        [SerializeField, Min(0)] float minimumDamageDistance = 18;
        [SerializeField, Range(0, 1)] float minimumMultiplier = .3f;
        [Tooltip("Hits farther than this from the shot origin deal no damage (0 disables). Keeps the range exact at any frame rate.")]
        [SerializeField, Min(0)] float maximumDistance;
        ProjectileBase projectile;
        public float FullDamageDistance => fullDamageDistance;
        public float MinimumDamageDistance => minimumDamageDistance;
        public float MinimumMultiplier => minimumMultiplier;
        public float MaximumDistance => maximumDistance;

        void Awake() => projectile = GetComponent<ProjectileBase>();

        public void Configure(float full, float minimum, float multiplier, float maximum = 0)
        {
            maximumDistance = Mathf.Max(0, maximum);
            fullDamageDistance = Mathf.Max(0, full);
            minimumDamageDistance = Mathf.Max(fullDamageDistance, minimum);
            minimumMultiplier = Mathf.Clamp01(multiplier);
        }

        public float Multiplier(float distance)
        {
            if (maximumDistance > 0 && distance > maximumDistance) return 0;
            if (distance <= fullDamageDistance) return 1;
            if (minimumDamageDistance <= fullDamageDistance) return minimumMultiplier;
            float t = Mathf.Clamp01((distance - fullDamageDistance) / (minimumDamageDistance - fullDamageDistance));
            return Mathf.Lerp(1, minimumMultiplier, t);
        }

        public float ModifyHitDamage(float damage, Vector3 hitPoint)
        {
            if (!projectile) projectile = GetComponent<ProjectileBase>();
            return damage * Multiplier(Vector3.Distance(projectile.InitialPosition, hitPoint));
        }
    }
}
