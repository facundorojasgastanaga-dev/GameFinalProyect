using UnityEngine;
namespace Unity.FPS.AI
{
    // Optional actor-specific policy. EnemyMobile retains Patrol/Follow/Attack and navigation ownership.
    public interface IEnemyMobilePolicy
    {
        bool CanAttack {get;}
        Vector3 PursuitDestination {get;}
        Vector3 AimPosition {get;}
        void OrientTowards(Vector3 target);
        void RequestAttack();
        void UpdateMotion(float speed);
    }
}
