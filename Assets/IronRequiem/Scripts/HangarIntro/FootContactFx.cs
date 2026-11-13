using System;
using UnityEngine;
using UnityEngine.Playables;

namespace IronRequiem
{
    /// v30 (12/11/2026) footstep effects of the Room00 wake-up cinematic, driven by real contact instead of fixed times.
    /// A window of cinematic time only ARMS a foot. The effect (dust, sparks, metal sound, small camera kick) fires once,
    /// on the first frame in which the sole is within a few millimetres of the ground (raycast under the foot) while the
    /// foot is no longer rising, and only if that foot was lifted above the release height since it was armed. The effect
    /// is placed under the sole and aligned with the ground normal. No allocations per frame.
    [DefaultExecutionOrder(300)]
    public sealed class FootContactFx : MonoBehaviour
    {
        [Serializable]
        public struct Window
        {
            public bool right;
            public float armAt, disarmAt;   // cinematic seconds
            [Range(0, 1)] public float strength;
        }

        public delegate void ContactHandler(FootContactFx source, bool right, Vector3 point, float time, float soleHeight);
        /// Raised on every fired contact (debug captures and tests listen to it).
        public static event ContactHandler Contact;

        [SerializeField] WakeUpRobotPose pose;
        [SerializeField] PlayableDirector director;
        [SerializeField] HangarIntroCinematic cinematic;
        [SerializeField] Window[] windows = new Window[0];
        [SerializeField] LayerMask ground = 1;   // Default layer: the hangar floor collider (glass shards are on Ignore Raycast)
        [SerializeField, Min(.001f)] float contactThreshold = .015f;  // sole to ground (m)
        [SerializeField, Min(.01f)] float releaseHeight = .05f;       // the foot must have been this high since arming
        [SerializeField, Min(0)] float maxRiseSpeed = .05f;           // m/s: "not rising" (near zero or going down)
        [Header("Effects")]
        [SerializeField] ParticleSystem dust;
        [SerializeField] ParticleSystem sparks;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip stepClip;
        [SerializeField, Range(0, 1)] float volume = 1f;

        struct FootState { public int window; public bool primed, fired; public float prevSole; public bool hasPrev; }
        FootState left, right;
        RaycastHit hit;

        public int Fired { get; private set; }

        public void Configure(WakeUpRobotPose robotPose, PlayableDirector timeline, HangarIntroCinematic owner, Window[] armWindows, ParticleSystem dustFx, ParticleSystem sparkFx, AudioSource source, AudioClip clip)
        { pose = robotPose; director = timeline; cinematic = owner; windows = armWindows ?? new Window[0]; dust = dustFx; sparks = sparkFx; audioSource = source; stepClip = clip; }

        void OnEnable() { left = new FootState { window = -1 }; right = new FootState { window = -1 }; }

        void LateUpdate()
        {
            if (!pose || !director || !pose.IsBound || !cinematic || !cinematic.IsPlaying) return;
            if (!pose.Model || !pose.Model.gameObject.activeInHierarchy) return;
            float t = (float)director.time, dt = Time.deltaTime;
            Track(ref right, true, t, dt);
            Track(ref left, false, t, dt);
        }

        void Track(ref FootState s, bool isRight, float t, float dt)
        {
            int w = ActiveWindow(isRight, t);
            if (w < 0) { s.window = -1; s.hasPrev = false; return; }
            if (w != s.window) { s.window = w; s.primed = false; s.fired = false; s.hasPrev = false; }
            if (s.fired) return;
            var foot = isRight ? pose.RightFoot : pose.LeftFoot; var toe = isRight ? pose.RightToe : pose.LeftToe;
            if (!foot) return;
            // sole: the lower of the heel (foot bone minus ankle height) and the ball (toe bone minus toe height)
            float heelY = foot.position.y - pose.AnkleHeight, ballY = toe ? toe.position.y - pose.ToeHeight : heelY;
            var under = toe ? Vector3.Lerp(foot.position, toe.position, .5f) : foot.position;
            float groundY = pose.FloorY; var normal = Vector3.up;
            // only the floor counts as ground (not the capsule body the foot steps over)
            if (Physics.Raycast(new Vector3(under.x, Mathf.Max(heelY, ballY) + .5f, under.z), Vector3.down, out hit, 2f, ground, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y - pose.FloorY) < .15f)
            { groundY = hit.point.y; normal = hit.normal; }
            float sole = Mathf.Min(heelY, ballY) - groundY;
            float vy = s.hasPrev && dt > 1e-5f ? (sole - s.prevSole) / dt : 0;
            s.prevSole = sole; s.hasPrev = true;
            if (sole > releaseHeight) s.primed = true;
            if (!s.primed || sole > contactThreshold || vy > maxRiseSpeed) return;
            s.fired = true; Fired++;
            Fire(new Vector3(under.x, groundY, under.z), normal, windows[w].strength);
            Contact?.Invoke(this, isRight, new Vector3(under.x, groundY, under.z), t, sole);
        }

        int ActiveWindow(bool isRight, float t)
        {
            for (int i = 0; i < windows.Length; i++)
                if (windows[i].right == isRight && t >= windows[i].armAt && t <= windows[i].disarmAt) return i;
            return -1;
        }

        void Fire(Vector3 point, Vector3 normal, float strength)
        {
            var rot = Quaternion.FromToRotation(Vector3.up, normal);
            if (dust) { dust.transform.SetPositionAndRotation(point + normal * .02f, rot); dust.Emit(Mathf.RoundToInt(8 + 18 * strength)); }
            if (sparks && strength > .4f) { sparks.transform.SetPositionAndRotation(point + normal * .04f, rot); sparks.Emit(Mathf.RoundToInt(4 + 10 * strength)); }
            if (audioSource && stepClip)
            {
                audioSource.transform.position = point;
                audioSource.pitch = .92f + .12f * Mathf.Repeat(point.x * 7.3f + point.z * 3.1f, 1f);
                audioSource.PlayOneShot(stepClip, volume * Mathf.Lerp(.45f, 1f, strength));
            }
            if (cinematic) cinematic.Kick(.012f + .03f * strength);
        }
    }
}
