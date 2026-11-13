using Unity.FPS.AI;
using UnityEngine;
using UnityEngine.AI;

namespace IronRequiem
{
    // Ranged capability is lost, but the existing agent and detection remain authoritative.
    public sealed class DisarmedEnemyRetreat : MonoBehaviour
    {
        [SerializeField] DetachableCannon cannon;
        [SerializeField] EnemyController enemy;
        float nextPathTime;

        public void Configure(DetachableCannon module, EnemyController controller)
        { cannon = module; enemy = controller; }

        void Update()
        {
            if (!cannon || !cannon.IsDetached || !enemy || !enemy.NavMeshAgent ||
                !enemy.NavMeshAgent.isOnNavMesh || Time.time < nextPathTime) return;
            nextPathTime = Time.time + 0.5f;
            var target = enemy.KnownDetectedTarget;
            if (!target) return;
            Vector3 away = Vector3.ProjectOnPlane(transform.position - target.transform.position, Vector3.up).normalized;
            if (NavMesh.SamplePosition(transform.position + away * 4, out var hit, 3, enemy.NavMeshAgent.areaMask))
                enemy.SetNavDestination(hit.position);
        }
    }
}
