using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace IronRequiem
{
    // Executions v02 (F2): when an executed robot dies right in front of the player, its detached plates and wreckage must not
    // fly into the camera. For ~0.45 s after the Strike beat, pieces thrown by the existing death systems (RobotArmorDebris,
    // MechDebrisPiece: cosmetic ballistics; BigBotCannonDebris: Rigidbody) lose their velocity component toward the player's
    // camera, get a small push outward and down, and a piece born closer than minimumDistance is moved out to it.
    // RobotDamageProgression / BigBotDestruction are untouched. The cosmetic pieces keep their velocity private, so it is
    // read and written once per piece through cached reflection (one boxed Vector3 per piece, never per frame). No particles, no lights.
    [RequireComponent(typeof(EnemyExecutionSource))]
    public sealed class ExecutionDebrisGuard : MonoBehaviour
    {
        [SerializeField, Min(.1f)] float windowSeconds = .45f;
        [SerializeField, Min(1)] float searchRadius = 8;
        [SerializeField, Min(0)] float minimumDistance = 1.2f;
        [SerializeField, Min(0)] float outwardPush = 1.2f, downwardPush = .5f;
        static readonly float[] ScanTimes = { 0, .05f, .1f, .18f, .28f, .4f };
        static readonly FieldInfo ArmorVelocity = typeof(RobotArmorDebris).GetField("velocity", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo MechVelocity = typeof(MechDebrisPiece).GetField("velocity", BindingFlags.NonPublic | BindingFlags.Instance);
        EnemyExecutionSource source; bool subscribed;
        float strikeAt = -10; int nextScan = ScanTimes.Length;
        readonly HashSet<Object> done = new HashSet<Object>();

        void Awake() => source = GetComponent<EnemyExecutionSource>();
        void OnEnable() { if (source) { source.BeatReached += OnBeat; subscribed = true; } }
        void OnDisable() { if (subscribed && source) source.BeatReached -= OnBeat; subscribed = false; nextScan = ScanTimes.Length; }

        void OnBeat(ExecutionBeatInfo info)
        {
            if (info.Beat == ExecutionBeat.Strike) { strikeAt = Time.time; nextScan = 0; done.Clear(); }
            else if (info.Beat == ExecutionBeat.Cancel) nextScan = ScanTimes.Length;
        }

        void Update()
        {
            if (nextScan >= ScanTimes.Length) return;
            float t = Time.time - strikeAt;
            if (t > windowSeconds + .1f) { nextScan = ScanTimes.Length; return; }
            if (t < ScanTimes[nextScan]) return;
            nextScan++;
            var cam = Camera.main; if (!cam) return;
            Vector3 eye = cam.transform.position, centre = transform.position; float r2 = searchRadius * searchRadius;
            foreach (var piece in FindObjectsByType<RobotArmorDebris>())
                if (piece && piece.InUse && !done.Contains(piece) && (piece.transform.position - centre).sqrMagnitude < r2) { done.Add(piece); Redirect(piece.transform, eye, ArmorVelocity, piece, null); }
            foreach (var piece in FindObjectsByType<MechDebrisPiece>())
                if (piece && piece.Flying && !done.Contains(piece) && (piece.transform.position - centre).sqrMagnitude < r2) { done.Add(piece); Redirect(piece.transform, eye, MechVelocity, piece, null); }
            foreach (var piece in FindObjectsByType<BigBotCannonDebris>())
            {
                var body = piece ? piece.GetComponent<Rigidbody>() : null;
                if (body && !body.isKinematic && !done.Contains(piece) && (piece.transform.position - centre).sqrMagnitude < r2) { done.Add(piece); Redirect(piece.transform, eye, null, null, body); }
            }
        }

        void Redirect(Transform piece, Vector3 eye, FieldInfo field, object owner, Rigidbody body)
        {
            Vector3 toEye = eye - piece.position; float distance = toEye.magnitude;
            if (distance < 1e-3f) return;
            toEye /= distance;
            if (distance < minimumDistance) piece.position -= toEye * (minimumDistance - distance);
            Vector3 velocity = body ? body.linearVelocity : (field != null ? (Vector3)field.GetValue(owner) : Vector3.zero);
            float toward = Vector3.Dot(velocity, toEye);
            if (toward > 0) velocity -= toEye * toward;
            velocity += -toEye * outwardPush + Vector3.down * downwardPush;
            if (body) body.linearVelocity = velocity; else if (field != null) field.SetValue(owner, velocity);
        }
    }
}
