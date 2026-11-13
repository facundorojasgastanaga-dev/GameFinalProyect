using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class DamageArea : MonoBehaviour
    {
        [Tooltip("Area of damage when the projectile hits something")]
        public float AreaOfEffectDistance = 5f;

        [Tooltip("Damage multiplier over distance for area of effect")]
        public AnimationCurve DamageRatioOverDistance;

        [Header("Optional cover checks (existing assets keep their original rules)")]
        public bool RespectObstacles;
        public bool IgnoreOwner;
        public bool IgnoreSameAffiliation;
        public LayerMask ObstructionLayers = -1;

        [Header("Debug")] [Tooltip("Color of the area of effect radius")]
        public Color AreaOfEffectColor = Color.red * 0.5f;

        public void InflictDamageInArea(float damage, Vector3 center, LayerMask layers,
            QueryTriggerInteraction interaction, GameObject owner, Vector3 surfaceNormal = default)
        {
            Dictionary<Health, Damageable> uniqueDamagedHealths = new Dictionary<Health, Damageable>();
            var closestColliders = RespectObstacles ? new Dictionary<Health, Collider>() : null;
            var ownerHealth = owner ? owner.GetComponentInParent<Health>() : null;
            var ownerActor = owner ? owner.GetComponentInParent<Actor>() : null;

            // Create a collection of unique health components that would be damaged in the area of effect (in order to avoid damaging a same entity multiple times)
            Collider[] affectedColliders = Physics.OverlapSphere(center, AreaOfEffectDistance, layers, interaction);
            foreach (var coll in affectedColliders)
            {
                Damageable damageable = coll.GetComponent<Damageable>();
                if (damageable)
                {
                    Health health = damageable.GetComponentInParent<Health>();
                    if (!health || (IgnoreOwner && health == ownerHealth)) continue;
                    var actor = health.GetComponent<Actor>();
                    if (IgnoreSameAffiliation && ownerActor && actor && ownerActor.Affiliation == actor.Affiliation) continue;
                    if (RespectObstacles)
                    {
                        Vector3 origin = center + surfaceNormal.normalized * .025f;
                        Vector3 point = coll.ClosestPoint(origin);
                        if (!Visible(origin, point, health, owner)) continue;
                        if (closestColliders.TryGetValue(health, out var old) &&
                            (old.ClosestPoint(center) - center).sqrMagnitude <= (coll.ClosestPoint(center) - center).sqrMagnitude) continue;
                        closestColliders[health] = coll;
                        uniqueDamagedHealths[health] = damageable;
                    }
                    else if (!uniqueDamagedHealths.ContainsKey(health))
                    {
                        uniqueDamagedHealths.Add(health, damageable);
                    }
                }
            }

            // Apply damages with distance falloff
            foreach (var pair in uniqueDamagedHealths)
            {
                var uniqueDamageable = pair.Value;
                float distance = RespectObstacles
                    ? Vector3.Distance(center, closestColliders[pair.Key].ClosestPoint(center))
                    : Vector3.Distance(uniqueDamageable.transform.position, transform.position);
                uniqueDamageable.InflictDamage(
                    damage * DamageRatioOverDistance.Evaluate(distance / Mathf.Max(.001f, AreaOfEffectDistance)), true, owner);
            }
        }

        bool Visible(Vector3 origin, Vector3 point, Health target, GameObject owner)
        {
            var delta = point - origin;
            if (delta.sqrMagnitude < .000001f) return true;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ObstructionLayers, QueryTriggerInteraction.Ignore))
            {
                var collider = hit.collider;
                if (collider.GetComponent<IgnoreHitDetection>() || collider.GetComponentInParent<Health>() == target ||
                    (owner && collider.transform.IsChildOf(owner.transform))) continue;
                return false;
            }
            return true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = AreaOfEffectColor;
            Gizmos.DrawSphere(transform.position, AreaOfEffectDistance);
        }
    }
}
