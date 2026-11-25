using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace IronRequiem.Editor
{
    /// 23/11 measurement only: records the Profiler while the campaign launches and stops it right after the hangar
    /// activation log line, then reports the markers of the longest frames. Nothing in the project changes.
    [InitializeOnLoad]
    public static class VisualOverhaulLoadProfiler
    {
        const string Key = "IR.LoadProfiler.Armed";
        const string Out = "Tools/IronRequiem/Reports/VisualOverhaul/legacy-variants/profile.txt";
        static VisualOverhaulLoadProfiler() { if (SessionState.GetBool(Key, false)) { Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog; } }

        public static string Arm()
        {
            ProfilerDriver.ClearAllFrames(); ProfilerDriver.profileEditor = true; ProfilerDriver.enabled = true;
            SessionState.SetBool(Key, true); Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
            return "armed";
        }
        static void OnLog(string message, string stack, LogType type)
        {
            if (!message.Contains("transition to Room00_Hangar")) return;
            ProfilerDriver.enabled = false; SessionState.SetBool(Key, false); Application.logMessageReceived -= OnLog;
        }

        public static string Report()
        {
            ProfilerDriver.enabled = false; SessionState.SetBool(Key, false);
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
            if (first < 0 || last < first) return "no frames";
            var frames = new List<(float ms, int frame)>();
            for (int f = first; f <= last; f++)
                using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
                    if (v != null && v.valid) frames.Add((v.frameTimeMs, f));
            var sb = new StringBuilder($"{DateTime.Now:s} frames {first}-{last} ({frames.Count} valid)\n");
            foreach (var (ms, frame) in frames.OrderByDescending(x => x.ms).Take(3))
            {
                sb.AppendLine($"== frame {frame}: {ms:0} ms");
                using (var v = ProfilerDriver.GetHierarchyFrameDataView(frame, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
                {
                    var rows = new List<(float ms, int depth, string name)>();
                    Walk(v, v.GetRootItemID(), 0, rows);
                    foreach (var r in rows.Where(r => r.ms >= 50).OrderByDescending(r => r.ms).Take(45)) sb.AppendLine($"  {r.ms,8:0} ms  d{r.depth}  {r.name}");
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Out)); File.WriteAllText(Out, sb.ToString());
            return sb.ToString();
        }
        static void Walk(HierarchyFrameDataView v, int id, int depth, List<(float, int, string)> rows)
        {
            if (depth > 14) return;
            var children = new List<int>(); v.GetItemChildren(id, children);
            foreach (var c in children)
            {
                float ms = v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime);
                if (ms < 50) continue;
                rows.Add((ms, depth, v.GetItemName(c)));
                Walk(v, c, depth + 1, rows);
            }
        }
    }
}
