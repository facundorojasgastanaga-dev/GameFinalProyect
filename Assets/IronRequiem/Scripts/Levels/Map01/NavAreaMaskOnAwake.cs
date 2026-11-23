using UnityEngine;
using UnityEngine.AI;

namespace IronRequiem
{
    /// R0-D9 (Map01): the prefab keeps every NavMesh area so CombatTwoRoomsLab validates spawns on the same NavMesh as the
    /// other enemies; once the instance wakes up, its agent stops using the Jump area (refuge ramps, D-U6).
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NavAreaMaskOnAwake : MonoBehaviour
    {
        [SerializeField] string excludedArea = "Jump";
        [SerializeField] int fallbackAreaIndex = 2;

        public int AppliedMask { get; private set; } = NavMesh.AllAreas;

        void Awake()
        {
            var agent = GetComponent<NavMeshAgent>();
            if (!agent) return;
            int area = NavMesh.GetAreaFromName(excludedArea);
            if (area < 0) area = fallbackAreaIndex;
            if (area < 0 || area > 31) return;
            agent.areaMask &= ~(1 << area);
            AppliedMask = agent.areaMask;
        }
    }
}
