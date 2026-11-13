using Unity.Profiling;
using UnityEngine;

namespace IronRequiem
{
    /// Diagnostics: records frame times for a few seconds and logs average ms, p99 and max, plus the average
    /// main-thread cost of the engine systems that scale with enemy count. Allocates once.
    public sealed class FrameTimeSampler : MonoBehaviour
    {
        static readonly (ProfilerCategory cat, string name)[] Markers =
        {
            (ProfilerCategory.Scripts, "BehaviourUpdate"), (ProfilerCategory.Scripts, "LateBehaviourUpdate"), (ProfilerCategory.Scripts, "FixedBehaviourUpdate"),
            (ProfilerCategory.Ai, "NavMeshManager"), (ProfilerCategory.Physics, "Physics.Simulate"), (ProfilerCategory.Animation, "PreLateUpdate.DirectorUpdateAnimationBegin"),
            (ProfilerCategory.Render, "MeshSkinning.Update"), (ProfilerCategory.Render, "Inl_RenderCameraStack"), (ProfilerCategory.Audio, "PreLateUpdate.UpdateAudio"),
            (ProfilerCategory.Scripts, "IRON.AI.TargetDetection"),
        };
        readonly float[] samples = new float[4096]; int count; float until, startAt; string label;
        ProfilerRecorder[] rec; double[] sums; int recFrames;

        public static void Run(string label, float seconds, float delay = 0)
        {
            var s = new GameObject("FrameTimeSampler").AddComponent<FrameTimeSampler>();
            s.label = label; s.startAt = Time.unscaledTime + delay; s.until = s.startAt + seconds;
        }

        void OnEnable()
        {
            rec = new ProfilerRecorder[Markers.Length]; sums = new double[Markers.Length];
            for (int i = 0; i < Markers.Length; i++) rec[i] = ProfilerRecorder.StartNew(Markers[i].cat, Markers[i].name);
        }
        void OnDisable() { if (rec != null) foreach (var r in rec) r.Dispose(); }

        void Update()
        {
            if (Time.unscaledTime < startAt) return;
            if (count < samples.Length) samples[count++] = Time.unscaledDeltaTime * 1000f;
            for (int i = 0; i < rec.Length; i++) if (rec[i].Valid) sums[i] += rec[i].LastValue * 1e-6; // ns -> ms
            recFrames++;
            if (Time.unscaledTime < until) return;
            System.Array.Sort(samples, 0, count);
            float sum = 0; for (int i = 0; i < count; i++) sum += samples[i];
            float avg = count > 0 ? sum / count : 0, p99 = count > 0 ? samples[Mathf.Min(count - 1, (int)(count * .99f))] : 0;
            int enemies = FindObjectsByType<Unity.FPS.AI.EnemyController>(FindObjectsSortMode.None).Length;
            var sb = new System.Text.StringBuilder($"[frametime] {label}: frames={count} avg={avg:0.00}ms ({(avg > 0 ? 1000 / avg : 0):0} fps) p99={p99:0.00}ms max={samples[Mathf.Max(0, count - 1)]:0.00}ms enemies={enemies} res={Screen.width}x{Screen.height}\n");
            for (int i = 0; i < rec.Length; i++) sb.AppendLine($"    {Markers[i].name,-45} {(rec[i].Valid ? (sums[i] / Mathf.Max(1, recFrames)).ToString("0.000") + "ms" : "n/a")}");
            Debug.Log(sb.ToString());
            Destroy(gameObject);
        }
    }
}
