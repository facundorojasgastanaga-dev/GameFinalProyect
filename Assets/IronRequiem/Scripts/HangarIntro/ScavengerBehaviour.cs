using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace IronRequiem
{
    /// Neutral "Scavenging" activity for a regular campaign Carroñero (EnemyController + EnemyMobile + SawBotCombat):
    /// walks between scrap points, crouches and rummages (sparks + metal), sometimes tears a piece off and carries it
    /// to the next pile and throws it. It ignores the player until it is damaged; then it and nearby members
    /// (ScavengerGroup) switch to the normal Follow/Attack states. No per-frame allocations.
    [RequireComponent(typeof(EnemyController))]
    public sealed class ScavengerBehaviour : MonoBehaviour, IEnemyScavenging
    {
        enum Phase { Walk, Rummage, Tear, Throw, Pause }

        [SerializeField] ScavengerGroup group;
        [SerializeField] CarroneroMotion motion;
        [SerializeField] Transform carrySocket;
        [SerializeField] Rigidbody scrapPiece;
        [SerializeField] ParticleSystem rummageSparks;
        [SerializeField] AudioSource voice;
        [SerializeField] AudioClip[] rummageClips = new AudioClip[0];
        [SerializeField] AudioClip tearClip, throwClip;
        [SerializeField, Min(.2f)] float walkSpeed = 1.5f;
        [SerializeField] Vector2 rummageSeconds = new Vector2(3f, 6.5f), pauseSeconds = new Vector2(.8f, 2.4f);
        [SerializeField, Range(0, 1)] float carryChance = .55f;
        [SerializeField, Min(0)] float turnSpeed = 160f;
        [SerializeField] int seed;

        EnemyController enemy; NavMeshAgent agent; Health health; SawBotCombat saw;
        Phase phase; int point = -1; float timer, nextHit, originalSpeed, pieceSince = -1; bool carrying, scavenging = true, arrivedSet;
        Collider pieceCollider; Transform pieceHome;

        public bool IsScavenging => scavenging;
        public bool Carrying => carrying;
        public int RummageHits { get; private set; }
        public int Throws { get; private set; }
        /// Debug/report text (allocates; not for per-frame use).
        public string DebugState => $"phase={phase} point={point} timer={timer:0.0}" + (agent ? $" onMesh={agent.isOnNavMesh} stopped={agent.isOnNavMesh && agent.isStopped} path={agent.hasPath}/{agent.pathStatus} rem={(agent.hasPath ? agent.remainingDistance : -1):0.0} speed={agent.speed:0.0} vel={agent.velocity.magnitude:0.0}" : "");

        public void Configure(ScavengerGroup owner, CarroneroMotion pose, Transform socket, Rigidbody piece, ParticleSystem sparks, AudioSource source,
            AudioClip[] rummage, AudioClip tear, AudioClip toss, int randomSeed)
        {
            group = owner; motion = pose; carrySocket = socket; scrapPiece = piece; rummageSparks = sparks; voice = source;
            rummageClips = rummage; tearClip = tear; throwClip = toss; seed = randomSeed;
        }

        void Awake()
        {
            enemy = GetComponent<EnemyController>(); agent = GetComponent<NavMeshAgent>(); health = GetComponent<Health>(); saw = GetComponent<SawBotCombat>();
            if (scrapPiece) { pieceCollider = scrapPiece.GetComponent<Collider>(); pieceHome = scrapPiece.transform.parent; scrapPiece.gameObject.SetActive(false); }
        }

        void OnEnable() { if (health) health.OnDamaged += Damaged; }
        void OnDisable() { if (health) health.OnDamaged -= Damaged; if (group) group.Release(this); }

        void Start()
        {
            // EnemyController creates the DetectionModule reference in Awake/Start; suppress sight until provoked.
            var detection = GetComponentInChildren<DetectionModule>();
            if (detection) detection.Suppressed = true;
            if (agent) { originalSpeed = agent.speed; agent.speed = walkSpeed; }
            // the saw policy rewrites agent.speed every frame from its drive speed: scale that instead
            if (saw) saw.SpeedScale = walkSpeed / Mathf.Max(.1f, saw.MovementSpeed);
            point = group ? group.Reserve(this, -1, seed) : -1;
            phase = Phase.Pause; timer = .3f + (Mathf.Abs(seed) % 7) * .25f; // staggered starts
        }

        void Damaged(float amount, GameObject source)
        {
            if (!scavenging) return;
            if (group) group.Alert(this, source); else Provoke(source);
        }

        /// Leaves the neutral activity and fights the attacker with the regular EnemyMobile states.
        public void Provoke(GameObject attacker)
        {
            if (!scavenging) return;
            scavenging = false;
            if (group) group.Release(this);
            DropPiece(Vector3.up * .5f);
            if (motion) { motion.CrouchTarget = 0; motion.DigTarget = 0; motion.ReachTarget = 0; }
            if (agent) agent.speed = originalSpeed;
            if (saw) saw.SpeedScale = 1;
            var detection = enemy ? enemy.DetectionModule : GetComponentInChildren<DetectionModule>();
            if (detection)
            {
                detection.Suppressed = false;
                var actor = attacker ? attacker.GetComponentInParent<Actor>() : null;
                if (!actor) { var player = FindAnyObjectByType<Unity.FPS.Gameplay.PlayerCharacterController>(); if (player) actor = player.GetComponent<Actor>(); }
                if (actor && actor.AimPoint) detection.ForceKnownTarget(actor.AimPoint.gameObject);
            }
        }

        public void UpdateScavenging()
        {
            if (!scavenging) return;
            if (health && health.CurrentHealth <= 0) { scavenging = false; DropPiece(Vector3.zero); return; }
            float dt = Time.deltaTime;
            var target = group ? group.Point(point) : null;
            switch (phase)
            {
                case Phase.Walk:
                    if (!target) { Begin(Phase.Pause, 1); break; }
                    if (!arrivedSet) { enemy.SetNavDestination(target.position); arrivedSet = true; }
                    FaceMovement(dt);
                    if (agent && !agent.pathPending && agent.remainingDistance <= Mathf.Max(.45f, agent.stoppingDistance + .1f))
                    {
                        if (carrying) Begin(Phase.Throw, .75f);
                        else Begin(Phase.Rummage, Random.Range(rummageSeconds.x, rummageSeconds.y));
                    }
                    break;
                case Phase.Rummage:
                    Face(target ? target.forward : transform.forward, dt);
                    if (Time.time >= nextHit)
                    {
                        nextHit = Time.time + Random.Range(.32f, .85f);
                        RummageHits++;
                        if (rummageSparks) rummageSparks.Emit(Random.Range(3, 9));
                        Play(rummageClips.Length > 0 ? rummageClips[Random.Range(0, rummageClips.Length)] : null, Random.Range(.35f, .7f));
                    }
                    if ((timer -= dt) <= 0) Begin(Random.value < carryChance && scrapPiece ? Phase.Tear : Phase.Pause, 0);
                    break;
                case Phase.Tear:
                    Face(target ? target.forward : transform.forward, dt);
                    if (!carrying && timer < .45f) PickPiece();
                    if ((timer -= dt) <= 0) NextPoint();
                    break;
                case Phase.Throw:
                    Face(target ? target.forward : transform.forward, dt);
                    if (carrying && timer < .4f) { DropPiece(transform.forward * 2.6f + Vector3.up * 2.4f + transform.right * Random.Range(-.8f, .8f)); Throws++; Play(throwClip, .8f); }
                    if ((timer -= dt) <= 0) Begin(Phase.Rummage, Random.Range(rummageSeconds.x, rummageSeconds.y));
                    break;
                case Phase.Pause:
                    // look around: slow yaw sway
                    transform.Rotate(0, Mathf.Sin(Time.time * 1.3f + seed) * 25f * dt, 0, Space.World);
                    if ((timer -= dt) <= 0) NextPoint();
                    break;
            }
            if (pieceSince >= 0 && !carrying && Time.time - pieceSince > 9f) ParkPiece();
        }

        void Begin(Phase next, float seconds)
        {
            phase = next;
            if (next == Phase.Tear) seconds = .9f;
            if (next == Phase.Pause) seconds = seconds > 0 ? seconds : Random.Range(pauseSeconds.x, pauseSeconds.y);
            timer = seconds; arrivedSet = false;
            if (!motion) return;
            motion.CrouchTarget = next == Phase.Rummage || next == Phase.Tear ? 1 : next == Phase.Pause ? .15f : 0;
            motion.DigTarget = next == Phase.Rummage ? 1 : 0;
            motion.ReachTarget = next == Phase.Tear ? 1 : next == Phase.Throw ? .8f : carrying ? .45f : 0;
            if (next == Phase.Tear) Play(tearClip, .7f);
            if (next == Phase.Rummage) nextHit = Time.time + .4f;
        }

        void NextPoint()
        {
            point = group ? group.Reserve(this, point, seed + RummageHits + Throws * 3) : -1;
            Begin(Phase.Walk, 0);
            if (motion) motion.ReachTarget = carrying ? .45f : 0;
        }

        void PickPiece()
        {
            if (!scrapPiece || !carrySocket) return;
            var t = scrapPiece.transform;
            scrapPiece.isKinematic = true; if (pieceCollider) pieceCollider.enabled = false;
            t.SetParent(carrySocket, false); t.localPosition = Vector3.zero; t.localRotation = Quaternion.Euler(20, 35, 10);
            scrapPiece.gameObject.SetActive(true); carrying = true; pieceSince = -1;
            if (rummageSparks) rummageSparks.Emit(10);
        }

        void DropPiece(Vector3 velocity)
        {
            if (!carrying || !scrapPiece) return;
            carrying = false; pieceSince = Time.time;
            var t = scrapPiece.transform; var p = t.position; var r = t.rotation;
            t.SetParent(pieceHome, false); t.SetPositionAndRotation(p, r);
            if (pieceCollider) pieceCollider.enabled = true;
            scrapPiece.isKinematic = false; scrapPiece.linearVelocity = velocity; scrapPiece.angularVelocity = new Vector3(Random.Range(-6f, 6f), Random.Range(-4f, 4f), Random.Range(-6f, 6f));
        }

        void ParkPiece()
        {
            pieceSince = -1;
            if (!scrapPiece) return;
            scrapPiece.isKinematic = true; scrapPiece.gameObject.SetActive(false);
        }

        void FaceMovement(float dt)
        {
            // turn towards the path corner: the saw drive only moves forward when facing it
            if (!agent || !agent.hasPath) return;
            Face(agent.steeringTarget - transform.position, dt);
        }

        void Face(Vector3 direction, float dt)
        {
            direction.y = 0; if (direction.sqrMagnitude < .0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * dt);
        }

        void Play(AudioClip clip, float volume)
        {
            if (!clip || !voice) return;
            voice.pitch = Random.Range(.88f, 1.12f);
            voice.PlayOneShot(clip, volume);
        }
    }
}
