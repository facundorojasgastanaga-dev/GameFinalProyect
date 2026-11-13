using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GapFallRecovery : MonoBehaviour
    {
        [SerializeField] PlayerCharacterController player;
        [SerializeField] Transform[] nearReturns, farReturns;
        [SerializeField, Min(0)] float damage = 20;
        [SerializeField, Min(.1f)] float repeatDelay = .75f;
        [SerializeField] float upperFloorHeight = 8, gapNearZ = -2, gapFarZ = 2;
        [SerializeField, Min(1)] float halfWidth = 26;
        Health health; CharacterController controller; AdvancedPlayerMovement movement;
        readonly Collider[] blockers = new Collider[32];
        bool farSide; float nextRecovery;
        public int RecoveryCount { get; private set; }
        public float Damage => damage;
        public bool ReturnsToFarSide => farSide;
        public void Configure(PlayerCharacterController owner, Transform[] near, Transform[] far)
        { player = owner; nearReturns = near; farReturns = far; }
        public void BindSession(PlayerCharacterController owner)=>player=owner;
        void Start()
        {
            health = player.GetComponent<Health>(); controller = player.GetComponent<CharacterController>(); movement = player.GetComponent<AdvancedPlayerMovement>();
        }
        void Update()
        {
            if (Time.timeScale <= 0 || !player || player.IsDead) return;
            var p = player.transform.position;
            // Remember the take-off bank, never grant progress from falling beyond the centre of the gap.
            if (player.IsGrounded && Mathf.Abs(p.y - upperFloorHeight) < .35f && Mathf.Abs(p.x) < halfWidth)
            { if (p.z < gapNearZ - .1f) farSide = false; else if (p.z > gapFarZ + .1f) farSide = true; }
        }
        void OnTriggerEnter(Collider other) => Contact(other);
        void OnTriggerStay(Collider other) => Contact(other);
        void Contact(Collider other)
        {
            if (other.GetComponentInParent<PlayerCharacterController>() == player) TryRecover();
        }
        public bool TryRecover()
        {
            if (!health || player.IsDead || health.CurrentHealth <= 0 || Time.timeScale <= 0 || Time.time < nextRecovery) return false;
            Transform destination = null; float closest = float.PositiveInfinity;
            foreach (var point in farSide ? farReturns : nearReturns)
            {
                if (!point) continue; var centre = point.position + controller.center;
                float radius = controller.radius, extent = Mathf.Max(0, controller.height * .5f - radius);
                int count = Physics.OverlapCapsuleNonAlloc(centre - Vector3.up * extent, centre + Vector3.up * extent, radius, blockers, -1, QueryTriggerInteraction.Ignore);
                bool clear = count < blockers.Length;
                for (int i = 0; i < count; i++) if (!blockers[i].transform.IsChildOf(player.transform)) { clear = false; break; }
                float distance = (point.position - player.transform.position).sqrMagnitude;
                if (clear && distance < closest) { closest = distance; destination = point; }
            }
            if (!destination) return false;
            nextRecovery = Time.time + repeatDelay;
            if (movement) movement.ResetForCheckpoint();
            player.TeleportToCheckpoint(destination.position);
            Physics.SyncTransforms(); RecoveryCount++;
            health.TakeDamage(damage, gameObject);
            return true;
        }
    }
}
