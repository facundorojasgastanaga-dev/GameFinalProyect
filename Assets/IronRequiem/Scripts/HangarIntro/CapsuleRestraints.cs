using System;
using UnityEngine;

namespace IronRequiem
{
    /// v30 (12/11/2026) inside of the Room00 capsule C-04: after the glass breaks the restraints unlock and slide into
    /// the rails (steam + hydraulic hiss on the timeline), the back cables pop out of the robot with a few weak sparks and
    /// swing down, and the cradle (bed, footrest, restraints) tilts forward to help the robot out. Evaluate(t) is a pure
    /// function of the cinematic time (the cradle motion comes from WakeUpRobotPose.CradleTransform), so scrubbing and
    /// skipping give the same state; Evaluate(duration) is the final state (restraints open, cradle forward). One-shot
    /// particle bursts fire only while the cinematic actually plays forward. The menu capsule does not use this.
    public sealed class CapsuleRestraints : MonoBehaviour
    {
        [Serializable]
        public struct Clamp
        {
            public Transform bar;
            public Vector3 closedLocal;     // local position in the cradle while locked
            public Vector3 retract;         // local offset when fully retracted (into the rail)
            public float delay;             // seconds after releaseAt
            [Range(.05f, 1)] public float keep;   // telescoping: fraction of the bar length left out of the rail housing
        }

        [Serializable]
        public struct Cable
        {
            public Transform anchor;        // fixed end on the capsule rail
            public Transform hose;          // unit cylinder (Y axis), stretched between the ends
            public Transform plug;          // connector at the robot end
            public Vector3 plugged;         // world position of the connector at bind (on the robot's back/side)
            public Vector3 hang;            // world direction the free cable swings to
        }

        [SerializeField] WakeUpRobotPose pose;
        [SerializeField] Transform cradle;
        [SerializeField] Clamp[] clamps = new Clamp[0];
        [SerializeField] Cable[] cables = new Cable[0];
        [SerializeField] ParticleSystem steam, sparks;
        [SerializeField, Min(.1f)] float clampSeconds = .75f;
        Vector3 cradleBindPos; Quaternion cradleBindRot; bool cached; float lastT = -1; bool released, popped;

        public bool Open => released;

        public void Configure(WakeUpRobotPose robotPose, Transform cradleRoot, Clamp[] restraints, Cable[] backCables, ParticleSystem steamFx, ParticleSystem sparkFx)
        { pose = robotPose; cradle = cradleRoot; clamps = restraints ?? new Clamp[0]; cables = backCables ?? new Cable[0]; steam = steamFx; sparks = sparkFx; }

        void Cache()
        {
            if (cached || !cradle) return;
            cradleBindPos = cradle.position; cradleBindRot = cradle.rotation; cached = true;
        }

        void Awake() { Cache(); Evaluate(0, false); }   // locked restraints and plugged cables before the cinematic starts

        public void Evaluate(float t, bool playing)
        {
            if (!pose) return;
            Cache();
            var T = pose.Times;
            // one-shot FX only when playing forward through the cue (never on scrub/skip)
            if (t < lastT) { released = t >= T.releaseAt; popped = t >= T.cablePop; }
            if (playing && !released && t >= T.releaseAt && lastT < T.releaseAt) { released = true; Puffs(); }
            if (playing && !popped && t >= T.cablePop && lastT < T.cablePop) { popped = true; Pops(); }
            if (t >= T.releaseAt) released = true;
            if (t >= T.cablePop) popped = true;
            lastT = t;

            if (cradle)
            {
                pose.CradleTransform(t, out var q, out var pivot, out var off);
                cradle.SetPositionAndRotation(pivot + q * (cradleBindPos - pivot) + off, q * cradleBindRot);
            }
            for (int i = 0; i < clamps.Length; i++)
            {
                var c = clamps[i]; if (!c.bar) continue;
                float a = T.releaseAt + c.delay;
                // unlock pop (2 cm out), short pause, then a hydraulic retract into the rail with ease-out
                float pop = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, a + .08f, t));
                float k = Mathf.InverseLerp(a + .15f, a + .15f + clampSeconds, t); k = 1 - (1 - k) * (1 - k);
                c.bar.localPosition = c.closedLocal + new Vector3(0, 0, -.02f) * pop * (1 - k) + c.retract * k;
                c.bar.localScale = new Vector3(Mathf.Lerp(1, c.keep <= 0 ? 1 : c.keep, k), 1, 1);
            }
            for (int i = 0; i < cables.Length; i++)
            {
                var c = cables[i]; if (!c.anchor || !c.hose || !c.plug) continue;
                var a = c.anchor.position; var toPlug = c.plugged - a; float len = toPlug.magnitude;
                Vector3 end;
                if (t < T.cablePop) end = c.plugged;
                else
                {
                    // springs out of the socket, then swings down on a damped pendulum (pure function of the time since pop)
                    float s = t - T.cablePop;
                    float swing = 1 - Mathf.Exp(-s * 3.2f) * Mathf.Cos(s * 7.5f);
                    var dir = Vector3.Slerp(toPlug / Mathf.Max(len, 1e-4f), c.hang.normalized, Mathf.Clamp(swing, 0, 1.15f));
                    end = a + dir * len + (c.plugged - a).normalized * (.05f * Mathf.Exp(-s * 8f));
                }
                var mid = (a + end) * .5f; var span = end - a;
                c.hose.SetPositionAndRotation(mid, Quaternion.FromToRotation(Vector3.up, span.sqrMagnitude > 1e-6f ? span.normalized : Vector3.up));
                var sc = c.hose.localScale; sc.y = span.magnitude * .5f / Mathf.Max(1e-4f, c.hose.parent ? c.hose.parent.lossyScale.y : 1); c.hose.localScale = sc;
                c.plug.SetPositionAndRotation(end, c.hose.rotation);
            }
        }

        void Puffs()
        {
            if (!steam) return;
            foreach (var c in clamps) if (c.bar) { steam.transform.position = c.bar.position; steam.Emit(6); }
        }

        void Pops()
        {
            if (!sparks) return;
            foreach (var c in cables) { sparks.transform.position = c.plugged; sparks.Emit(5); }
        }
    }
}
