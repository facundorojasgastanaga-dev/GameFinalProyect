using UnityEngine;

namespace Unity.FPS.Game
{
    public class Damageable : MonoBehaviour
    {
        [Tooltip("Multiplier to apply to the received damage")]
        public float DamageMultiplier = 1f;

        [Range(0, 1)] [Tooltip("Multiplier to apply to self damage")]
        public float SensibilityToSelfdamage = 0.5f;

        [SerializeField, Tooltip("Optional receiver for hitboxes whose separate integrity is disabled by a variant.")]
        Health healthOverride;
        public Health Health { get; private set; }

        public event System.Action<Vector3, Vector3, GameObject> Impact;
        public bool InflictDamageAtPoint(float damage, bool explosion, GameObject source, Vector3 point, Vector3 normal)
        { float before = Health ? Health.CurrentHealth : 0; InflictDamage(damage, explosion, source); if (Health && Health.CurrentHealth < before && Impact != null) { Impact.Invoke(point, normal, source); return true; } return false; }
        void Awake()
        {
            // find the health component either at the same level, or higher in the hierarchy
            Health = healthOverride ? healthOverride : GetComponent<Health>();
            if (!Health)
            {
                Health = GetComponentInParent<Health>();
            }
        }

        public void InflictDamage(float damage, bool isExplosionDamage, GameObject damageSource)
        {
            if (Health)
            {
                var totalDamage = damage;

                // skip the crit multiplier if it's from an explosion
                if (!isExplosionDamage)
                {
                    totalDamage *= DamageMultiplier;
                }

                // potentially reduce damages if inflicted by self
                if (Health.gameObject == damageSource)
                {
                    totalDamage *= SensibilityToSelfdamage;
                }

                // apply the damages
                Health.TakeDamage(totalDamage, damageSource);
            }
        }
    }
}