using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    // Executions v02 (F2): every sound of an execution comes from the robot's side (the player side plays no audio).
    // One cue table per ExecutionKind (F1 section 3, "Sonido" column), each cue tied to a beat plus an offset. The clips are
    // assigned by ExecutionsAuthoring.Apply() on the three robot prefabs; with no clips the component simply stays silent.
    // One-shots go through the existing mixer groups (AudioUtility.GetAudioGroup) on a short-lived AudioSource that
    // self-destructs (like AudioUtility.CreateSFX, but with a volume). Hand cues are 2D at the player's hand; the loops
    // (creak, motor, hydraulic) are 3D on the robot, fade out in 0.03-0.05 s and are always stopped on Cancel / End / death /
    // disable. The mixer has no EnemyMovement/PlayerMovement/WeaponShoot-free equivalents beyond AudioUtility.AudioGroups, so
    // Impact, Pickup, WeaponShoot and EnemyAttack are used.
    [DefaultExecutionOrder(170)]
    [RequireComponent(typeof(EnemyExecutionSource))]
    public sealed class ExecutionRobotAudio : MonoBehaviour
    {
        public enum Id { Clamp, Lock, FistImpact, Deflect, Blast, Tear, Spark, FootMetal, Hydraulic, CannonArm, Creak, SubBoom, MotorLoop, Jump, Land, Count }

        [Header("Clips (assigned by ExecutionsAuthoring)")]
        [SerializeField] AudioClip clamp, mountLock, fistImpact, deflect, blast, tear, spark, footMetal, hydraulic, cannonArm, creak, subBoom, motorLoop, jump, land;
        [SerializeField, Range(0, 1)] float masterVolume = 1;
        [SerializeField, Min(1)] float robotMinDistance = 3;

        sealed class Cue { public ExecutionBeat Beat; public float Offset; public Id Clip; public float Volume; public AudioUtility.AudioGroups Group; public bool Fired; }
        EnemyExecutionSource source; Health health; ExecutionKind kind; bool subscribed, running, dieHooked;
        Cue[] cues = new Cue[0];
        readonly float[] beatTime = new float[11];
        Vector3 hand;
        // Loop state
        AudioSource loop; Id loopId; ExecutionBeat loopStart, loopStop; float loopOffset, loopVolume, loopPitch0, loopPitch1, loopPitchSeconds, loopFade, loopStartedAt = -1, fadeFrom, fadeAt = -1, fadeSeconds;
        bool loopArmed, loopPlaying;

        void Awake()
        {
            source = GetComponent<EnemyExecutionSource>(); health = GetComponent<Health>(); kind = source.Kind;
            var go = new GameObject("ExecutionLoop"); go.transform.SetParent(transform, false); go.transform.localPosition = Vector3.up;
            loop = go.AddComponent<AudioSource>(); loop.playOnAwake = false; loop.loop = true; loop.spatialBlend = 1; loop.minDistance = robotMinDistance; loop.volume = 0;
            Build();
        }
        void OnEnable() { if (source) { source.BeatReached += OnBeat; subscribed = true; } }
        void Start() { if (health && !dieHooked) { health.OnDie += OnDie; dieHooked = true; } }
        void OnDisable()
        {
            if (subscribed && source) source.BeatReached -= OnBeat; subscribed = false;
            if (dieHooked && health) health.OnDie -= OnDie; dieHooked = false;
            StopLoopNow(); running = false;
        }

        AudioClip ClipOf(Id id)
        {
            switch (id)
            {
                case Id.Clamp: return clamp; case Id.Lock: return mountLock; case Id.FistImpact: return fistImpact; case Id.Deflect: return deflect;
                case Id.Blast: return blast; case Id.Tear: return tear; case Id.Spark: return spark; case Id.FootMetal: return footMetal;
                case Id.Hydraulic: return hydraulic; case Id.CannonArm: return cannonArm; case Id.Creak: return creak; case Id.SubBoom: return subBoom;
                case Id.MotorLoop: return motorLoop; case Id.Jump: return jump; case Id.Land: return land;
            }
            return null;
        }

        static Cue C(ExecutionBeat beat, float offset, Id clip, float volume, AudioUtility.AudioGroups group) => new Cue { Beat = beat, Offset = offset, Clip = clip, Volume = volume, Group = group };

        void Build()
        {
            var impact = AudioUtility.AudioGroups.Impact; var pickup = AudioUtility.AudioGroups.Pickup;
            switch (kind)
            {
                case ExecutionKind.SawBot:
                    cues = new[] { C(ExecutionBeat.Begin, .17f, Id.Clamp, .7f, impact), C(ExecutionBeat.Rip, 0, Id.Tear, 1, impact), C(ExecutionBeat.Rip, 0, Id.Deflect, .7f, impact),
                        C(ExecutionBeat.WindUp, 0, Id.Lock, .6f, pickup), C(ExecutionBeat.Strike, 0, Id.FistImpact, 1, impact) };
                    SetLoop(Id.MotorLoop, ExecutionBeat.Struggle, 0, .5f, 1, 1.25f, .16f, ExecutionBeat.Rip, .03f);
                    break;
                case ExecutionKind.BigBot:
                    cues = new[] { C(ExecutionBeat.Begin, 0, Id.Jump, 1, impact), C(ExecutionBeat.Grab, 0, Id.FootMetal, .9f, impact), C(ExecutionBeat.Struggle, 0, Id.Clamp, .8f, impact),
                        C(ExecutionBeat.Rip, 0, Id.Tear, 1, impact), C(ExecutionBeat.Rip, 0, Id.CannonArm, .8f, impact), C(ExecutionBeat.Handoff, 0, Id.Lock, .6f, pickup),
                        C(ExecutionBeat.Strike, 0, Id.Blast, 1, AudioUtility.AudioGroups.WeaponShoot), C(ExecutionBeat.Strike, 0, Id.SubBoom, .5f, impact), C(ExecutionBeat.End, 0, Id.Land, 1, impact) };
                    SetLoop(Id.Hydraulic, ExecutionBeat.Struggle, 0, .5f, 1, 1, 0, ExecutionBeat.Rip, .05f);
                    break;
                default:
                    cues = new[] { C(ExecutionBeat.Begin, .17f, Id.Clamp, .7f, impact), C(ExecutionBeat.Rip, 0, Id.Tear, 1, impact), C(ExecutionBeat.Rip, 0, Id.Spark, .6f, impact),
                        C(ExecutionBeat.WindUp, 0, Id.Lock, .6f, pickup), C(ExecutionBeat.Strike, 0, Id.FistImpact, 1, impact) };
                    SetLoop(Id.Creak, ExecutionBeat.Struggle, 0, .45f, 1, 1, 0, ExecutionBeat.Rip, .05f);
                    break;
            }
        }

        void SetLoop(Id id, ExecutionBeat start, float offset, float volume, float pitch0, float pitch1, float pitchSeconds, ExecutionBeat stop, float fade)
        { loopId = id; loopStart = start; loopOffset = offset; loopVolume = volume; loopPitch0 = pitch0; loopPitch1 = pitch1; loopPitchSeconds = pitchSeconds; loopStop = stop; loopFade = fade; loopArmed = true; }

        void OnBeat(ExecutionBeatInfo info)
        {
            if (info.Beat == ExecutionBeat.Cancel) { running = false; StopLoopNow(); return; }
            if (info.Beat == ExecutionBeat.Begin)
            {
                running = true; StopLoopNow(); loopArmed = ClipOf(loopId) != null;
                for (int i = 0; i < beatTime.Length; i++) beatTime[i] = -1;
                foreach (var cue in cues) cue.Fired = false;
            }
            if (!running) return;
            hand = info.HandPosition; beatTime[(int)info.Beat] = Time.time;
            if (info.Beat == loopStop) FadeLoop(loopFade);
            if (info.Beat == ExecutionBeat.End) { StopLoopNow(); }
        }

        void OnDie() { if (loopPlaying) FadeLoop(.05f); }

        void Update()
        {
            if (!running) { StepLoop(); return; }
            for (int i = 0; i < cues.Length; i++)
            {
                var cue = cues[i];
                if (cue.Fired) continue;
                float at = beatTime[(int)cue.Beat];
                if (at < 0 || Time.time - at < cue.Offset) continue;
                cue.Fired = true; Play(cue);
            }
            if (loopArmed && !loopPlaying)
            {
                float at = beatTime[(int)loopStart];
                if (at >= 0 && Time.time - at >= loopOffset && beatTime[(int)loopStop] < 0) StartLoop();
            }
            StepLoop();
        }

        void Play(Cue cue)
        {
            var clip = ClipOf(cue.Clip); if (!clip) return;
            var go = new GameObject("ExecutionSfx"); go.transform.position = hand;
            var audio = go.AddComponent<AudioSource>();
            audio.clip = clip; audio.volume = Mathf.Clamp01(cue.Volume * masterVolume); audio.spatialBlend = 0; audio.playOnAwake = false;
            audio.outputAudioMixerGroup = AudioUtility.GetAudioGroup(cue.Group);
            audio.Play();
            go.AddComponent<TimedSelfDestruct>().LifeTime = clip.length + .1f;
        }

        void StartLoop()
        {
            var clip = ClipOf(loopId); if (!clip) return;
            loop.clip = clip; loop.pitch = loopPitch0; loop.volume = loopVolume * masterVolume; loop.outputAudioMixerGroup = AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.EnemyAttack);
            loop.Play(); loopPlaying = true; loopStartedAt = Time.time; fadeAt = -1;
        }

        void FadeLoop(float seconds)
        {
            if (!loopPlaying) { loopArmed = false; return; }
            fadeAt = Time.unscaledTime; fadeSeconds = Mathf.Max(.01f, seconds); fadeFrom = loop.volume; loopArmed = false;
        }

        void StepLoop()
        {
            if (!loopPlaying) return;
            if (fadeAt >= 0)
            {
                float k = (Time.unscaledTime - fadeAt) / fadeSeconds;
                loop.volume = Mathf.Lerp(fadeFrom, 0, Mathf.Clamp01(k));
                if (k >= 1) StopLoopNow();
            }
            else if (loopPitchSeconds > 0) loop.pitch = Mathf.Lerp(loopPitch0, loopPitch1, Mathf.Clamp01((Time.time - loopStartedAt) / loopPitchSeconds));
        }

        // Guaranteed stop: Cancel, End, disable, destruction.
        void StopLoopNow()
        {
            if (loop) { loop.Stop(); loop.volume = 0; }
            loopPlaying = false; fadeAt = -1; loopArmed = false;
        }
    }
}
