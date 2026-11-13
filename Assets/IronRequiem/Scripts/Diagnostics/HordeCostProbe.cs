using System.Collections;
using UnityEngine;

namespace IronRequiem
{
    /// Diagnostics only: measures what a horde costs by switching parts off step by step (3 s per step) on the
    /// enemies whose name starts with a prefix. Logs avg ms and p99 per step.
    public sealed class HordeCostProbe : MonoBehaviour
    {
        string prefix; readonly float[] buf = new float[2048];

        public static void Run(string namePrefix) { var p = new GameObject("HordeCostProbe").AddComponent<HordeCostProbe>(); p.prefix = namePrefix; }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2);
            var bots = new System.Collections.Generic.List<GameObject>();
            foreach (var h in FindObjectsByType<Unity.FPS.Game.Health>(FindObjectsSortMode.None)) if (h.name.StartsWith(prefix)) bots.Add(h.gameObject);
            var log = new System.Text.StringBuilder($"[horde cost] bots={bots.Count}\n");
            yield return Sample("all on", log);
            foreach (var b in bots) { var m = b.GetComponent<CarroneroMotion>(); if (m) m.enabled = false; }
            yield return Sample("- motion script", log);
            foreach (var b in bots) foreach (var a in b.GetComponentsInChildren<AudioSource>()) a.enabled = false;
            yield return Sample("- audio", log);
            foreach (var b in bots) foreach (var a in b.GetComponentsInChildren<Animator>()) a.enabled = false;
            yield return Sample("- hidden animator", log);
            foreach (var b in bots) foreach (var l in b.GetComponentsInChildren<LODGroup>()) l.ForceLOD(3);
            yield return Sample("forced LOD3", log);
            foreach (var b in bots) foreach (var r in b.GetComponentsInChildren<Renderer>()) r.enabled = false;
            yield return Sample("- all rendering", log);
            foreach (var b in bots) foreach (var mb in b.GetComponents<MonoBehaviour>()) if (mb && !(mb is Unity.FPS.Game.Health)) mb.enabled = false;
            foreach (var b in bots) { var ag = b.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag) ag.enabled = false; }
            yield return Sample("- AI + agents (nothing left)", log);
            Debug.Log(log.ToString());
            Destroy(gameObject);
        }

        IEnumerator Sample(string label, System.Text.StringBuilder log)
        {
            yield return null; int n = 0; float end = Time.unscaledTime + 3;
            while (Time.unscaledTime < end) { if (n < buf.Length) buf[n++] = Time.unscaledDeltaTime * 1000; yield return null; }
            System.Array.Sort(buf, 0, n); float sum = 0; for (int i = 0; i < n; i++) sum += buf[i];
            log.AppendLine($"  {label,-30} avg={sum / Mathf.Max(1, n):0.00}ms p99={buf[Mathf.Min(n - 1, (int)(n * .99f))]:0.00}ms");
        }
    }
}
