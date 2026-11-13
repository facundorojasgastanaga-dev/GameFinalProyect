using System;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    [RequireComponent(typeof(Health))]
    public sealed class DetachableCannon : MonoBehaviour
    {
        [SerializeField] Transform moduleVisual;
        [SerializeField] bool executionOnly;
        [SerializeField] EnemyController enemy;
        [SerializeField] EnemyMobile mobile;
        [SerializeField] Collider jointCollider;
        Health integrity;
        Health carrierHealth;
        public bool IsDetached { get; private set; }
        public event Action Detached;

        public void Configure(Transform visual, EnemyController controller, EnemyMobile behavior, Collider hitbox)
        {
            moduleVisual = visual; enemy = controller; mobile = behavior; jointCollider = hitbox;
        }

        void OnEnable()
        {
            integrity = GetComponent<Health>();
            carrierHealth = enemy ? enemy.GetComponent<Health>() : null;
            integrity.OnDie += Detach;
            if (carrierHealth) carrierHealth.OnDie += Detach;
        }

        void OnDisable()
        {
            if (integrity) integrity.OnDie -= Detach;
            if (carrierHealth) carrierHealth.OnDie -= Detach;
        }

        public void Detach()
        {
            if (executionOnly || IsDetached || !moduleVisual) return;
            IsDetached = true;
            if (jointCollider) jointCollider.enabled = false;
            // Explicit loss of ranged capability. Keep navigation/detection/health alive.
            // EnemyMobile alone calls TryAtack; suspend it so it cannot fire a cached weapon.
            if (mobile) mobile.enabled = false;
            if (enemy)
            {
                var lostWeapon = enemy.GetCurrentWeapon();
                lostWeapon.UseAmmo(lostWeapon.MaxAmmo);
                lostWeapon.AutomaticReload = false;
            }
            var agent = enemy ? enemy.NavMeshAgent : null;
            if (agent && agent.isOnNavMesh) agent.ResetPath();
            moduleVisual.SetParent(null, true);
            var box = moduleVisual.gameObject.AddComponent<BoxCollider>();
            var module = moduleVisual.GetComponent<StolenCannon>();
            if(module)module.FitPickupCollider(box);
            var rigidbody = moduleVisual.gameObject.AddComponent<Rigidbody>();
            rigidbody.mass = 12;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // Do not collide with the carrier immediately after detachment.
            if (enemy)
                foreach (var collider in enemy.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(box, collider);
            rigidbody.AddForce((transform.right + Vector3.up) * 1.5f, ForceMode.VelocityChange);
            if (module) module.MakeAvailable(rigidbody, box);
            else Destroy(moduleVisual.gameObject, 30);
            Detached?.Invoke();
        }
    }
}
