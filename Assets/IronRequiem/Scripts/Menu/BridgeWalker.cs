using UnityEngine;

namespace IronRequiem
{
    /// Presentation-only enemy crossing the bridge behind the hangar menu. Built from the campaign prefab with all
    /// gameplay stripped (no AI, NavMesh, Health). Drives the T-07 locomotion parameters itself; procedural rigs
    /// (Carroñero) animate from the transform movement. One trigger collider for the click.
    public sealed class BridgeWalker : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] float speed = 1.7f, stride = 1.5f;
        [SerializeField] string label = "T-07";
        [SerializeField, Min(.1f)] float blastScale = 1;
        Collider hitbox; Renderer[] views; Vector3 from, to; float travelled, length, phase, runSpeed; bool alive, carried; CarroneroMotion procedural;
        int cycleId = -1, moveId = -1, turnId = -1;

        public bool Alive => alive;
        public float Travelled => travelled;
        public string Label => label;
        public float BlastScale => blastScale;
        public Vector3 Center { get { var b = hitbox ? hitbox.bounds : new Bounds(transform.position, Vector3.one); return b.center; } }

        public void Configure(Animator rig, float walkSpeed, float strideLength, string displayName, float explosionScale)
        { animator = rig; speed = walkSpeed; stride = strideLength; label = displayName; blastScale = explosionScale; }

        void Awake()
        {
            hitbox = GetComponent<Collider>(); views = GetComponentsInChildren<Renderer>(false); procedural = GetComponent<CarroneroMotion>();
            if (animator)
            {
                animator.applyRootMotion = false;
                foreach (var p in animator.parameters)
                {
                    if (p.type != AnimatorControllerParameterType.Float) continue;
                    if (p.name == "Cycle") cycleId = p.nameHash;
                    else if (p.name == "MoveSpeed" || p.name == "Speed") moveId = p.nameHash;
                    else if (p.name == "Turn") turnId = p.nameHash;
                }
            }
        }

        /// Starts a crossing. startFraction lets the menu open with robots already mid-bridge.
        /// onBelt: the robot stands still (powered down) facing facingYaw and is carried at speedOverride.
        public void Begin(Vector3 start, Vector3 end, float startFraction = 0, float speedOverride = -1, bool onBelt = false, float facingYaw = 180)
        {
            carried = onBelt; runSpeed = speedOverride > 0 ? speedOverride : speed;
            from = start; to = end; length = Mathf.Max(.1f, Vector3.Distance(start, end));
            travelled = length * Mathf.Clamp01(startFraction); phase = Random.value;
            var dir = Vector3.ProjectOnPlane(end - start, Vector3.up);
            transform.SetPositionAndRotation(Vector3.Lerp(from, to, travelled / length), carried ? Quaternion.Euler(0, facingYaw, 0) : Quaternion.LookRotation(dir.sqrMagnitude > 0 ? dir : Vector3.forward));
            gameObject.SetActive(true);
            if (!procedural) procedural = GetComponent<CarroneroMotion>();
            if (procedural) procedural.enabled = !carried;
            foreach (var v in views) if (v) v.enabled = true;
            if (hitbox) hitbox.enabled = true;
            alive = true;
        }

        public void Kill()
        {
            alive = false;
            if (hitbox) hitbox.enabled = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!alive) return;
            float step = runSpeed * Time.deltaTime; travelled += step;
            if (travelled >= length) { alive = false; gameObject.SetActive(false); return; }
            transform.position = Vector3.Lerp(from, to, travelled / length);
            if (!animator) return;
            if (carried) { if (moveId != -1) animator.SetFloat(moveId, 0); if (turnId != -1) animator.SetFloat(turnId, 0); return; }
            phase = Mathf.Repeat(phase + step / stride, 1);
            if (cycleId != -1) animator.SetFloat(cycleId, phase);
            if (moveId != -1) animator.SetFloat(moveId, runSpeed);
            if (turnId != -1) animator.SetFloat(turnId, 0);
        }
    }
}
