using System.Collections.Generic;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    // Executions v02 (F2): a local emissive pulse (0.09 s, HDR intensity 2.5) on the renderers nearest to the weapon joint on
    // the Rip and Strike beats, through MaterialPropertyBlocks per renderer + material index (shared materials never change).
    // It ADDS to whatever emission the slot already shows (damage-phase red of RobotDamageProgression included) and puts the
    // original block back when the pulse ends. It never rewrites the body emission outside the pulse: the whole-body tint that
    // F1 suspected is left to the existing damage systems (rewriting it erased the damage phase and made the robot blink).
    [DefaultExecutionOrder(165)]
    [RequireComponent(typeof(EnemyExecutionSource))]
    public sealed class ExecutionLocalFlash : MonoBehaviour
    {
        [SerializeField, Min(.03f)] float pulseSeconds = .09f;
        [SerializeField, Min(0)] float intensity = 2.5f;
        [SerializeField] Color pulseColor = new Color(1f, .62f, .25f, 1f);
        [SerializeField, Min(.05f)] float radius = .45f;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        sealed class Slot
        {
            public Renderer View; public int Index; public bool Near;
            public MaterialPropertyBlock Original = new MaterialPropertyBlock(), Work = new MaterialPropertyBlock(); public bool OriginalEmpty, Saved;
        }
        EnemyExecutionSource source; EnemyController enemy; Health health;
        readonly List<Slot> slots = new List<Slot>(32);
        bool subscribed, running, built, hadNear; float pulseAt = -10;

        public bool Suppressing => running;
        public bool PulseActive => Time.time - pulseAt < pulseSeconds;

        void Awake() { source = GetComponent<EnemyExecutionSource>(); enemy = GetComponent<EnemyController>(); health = GetComponent<Health>(); }
        void OnEnable() { if (source) { source.BeatReached += OnBeat; subscribed = true; } }
        void OnDisable() { if (subscribed && source) source.BeatReached -= OnBeat; subscribed = false; Finish(); }

        void Build()
        {
            built = true; slots.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    slots.Add(new Slot { View = r, Index = i });
            }
        }

        void OnBeat(ExecutionBeatInfo info)
        {
            switch (info.Beat)
            {
                case ExecutionBeat.Begin:
                    if (!built) Build();
                    running = true; ClearPulse(); break;
                case ExecutionBeat.Rip: case ExecutionBeat.Strike:
                    if (running) StartPulse(); break;
                case ExecutionBeat.End: case ExecutionBeat.Cancel:
                    Finish(); break;
            }
        }

        void StartPulse()
        {
            var joint = source.WeaponJoint; Vector3 point = joint ? joint.position : transform.position + Vector3.up;
            ClearPulse();
            pulseAt = Time.time; hadNear = true;
            float r2 = radius * radius;
            foreach (var s in slots)
            {
                if (!s.View || !s.View.enabled || s.View.forceRenderingOff) continue;
                if (s.View.bounds.SqrDistance(point) > r2) continue;
                s.Near = true;
                s.View.GetPropertyBlock(s.Original, s.Index); s.OriginalEmpty = s.Original.isEmpty; s.Saved = true;
            }
        }

        void ClearPulse()
        {
            foreach (var s in slots)
            {
                if (s.Near && s.Saved && s.View) s.View.SetPropertyBlock(s.OriginalEmpty ? null : s.Original, s.Index);
                s.Near = false; s.Saved = false;
            }
        }

        void LateUpdate()
        {
            if (!running) return;
            if (health && health.CurrentHealth <= 0 && !PulseActive) { Finish(); return; }
            float t = (Time.time - pulseAt) / pulseSeconds;
            bool pulsing = t >= 0 && t < 1;
            Color pulse = pulsing ? pulseColor * (intensity * (1 - t * t)) : Color.black;
            if (!pulsing) { if (hadNear) { ClearPulse(); hadNear = false; } return; }
            foreach (var s in slots)
            {
                if (!s.View || !s.Near) continue;
                s.View.GetPropertyBlock(s.Work, s.Index);
                Color existing;
                if (s.Original.HasColor(EmissionId)) existing = s.Original.GetColor(EmissionId); // base = block at pulse start, never last frame's sum
                else { var materials = s.View.sharedMaterials; existing = s.Index < materials.Length && materials[s.Index] && materials[s.Index].HasProperty(EmissionId) ? materials[s.Index].GetColor(EmissionId) : Color.black; }
                s.Work.SetColor(EmissionId, existing + pulse);
                s.View.SetPropertyBlock(s.Work, s.Index);
            }
        }

        void Finish()
        {
            if (!running && !built) return;
            ClearPulse(); running = false; pulseAt = -10;
        }
    }
}
