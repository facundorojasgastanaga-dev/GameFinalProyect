using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    /// File command bridge: lets an assistant drive the open Editor without screen control.
    /// Write commands (one per line) to Tools/IronRequiem/Bridge/command.txt; results go to result.txt.
    /// Whitelisted commands only (no code execution): refresh | menu <Menu/Path> | play | stop | wait <seconds> | click <button name or label>
    ///           | screenshot <file.png> | timescale <x> | clearlog
    /// Each file is processed once (renamed to command.done). Nothing runs unless that file is written.
    [InitializeOnLoad]
    public static class IRCommandBridge
    {
        const string Dir = "Tools/IronRequiem/Bridge";
        static readonly string CommandFile = Dir + "/command.txt";
        static readonly string ResultFile = Dir + "/result.txt";
        static readonly string LogFile = Dir + "/console.txt";
        static readonly Queue<string> pending = new Queue<string>();
        static double nextPoll, waitUntil;

        static IRCommandBridge()
        {
            Directory.CreateDirectory(Dir);
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
            Restore();
            Append("bridge ready " + DateTime.Now.ToString("HH:mm:ss") + " playing=" + EditorApplication.isPlaying);
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log && !message.StartsWith("[")) return;
            try { File.AppendAllText(LogFile, $"{DateTime.Now:HH:mm:ss} {type}: {message}\n{(type == LogType.Exception || type == LogType.Error ? stack + "\n" : "")}"); } catch { }
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextPoll) return;
            nextPoll = now + .5;
            if (pending.Count == 0 && File.Exists(CommandFile))
            {
                var lines = File.ReadAllLines(CommandFile);
                File.Copy(CommandFile, Dir + "/command.done", true); File.Delete(CommandFile);
                foreach (var l in lines) if (!string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#")) pending.Enqueue(l.Trim());
                Append("---- batch " + DateTime.Now.ToString("HH:mm:ss") + " (" + pending.Count + " commands)");
                Persist();
            }
            if (pending.Count == 0 || now < waitUntil || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var cmd = pending.Dequeue(); Persist();
            try { Run(cmd); } catch (Exception e) { Append("ERROR " + cmd + ": " + e.Message); }
            if (pending.Count == 0) Append("---- done");
        }

        static void Run(string line)
        {
            int sp = line.IndexOf(' ');
            string op = (sp < 0 ? line : line.Substring(0, sp)).ToLowerInvariant(), arg = sp < 0 ? "" : line.Substring(sp + 1).Trim();
            switch (op)
            {
                case "refresh": AssetDatabase.Refresh(); Append("refresh"); waitUntil = EditorApplication.timeSinceStartup + 2; break;
                case "menu":
                    // Only this project's own tools; never arbitrary editor menus.
                    if (!arg.StartsWith("Tools/Iron Requiem/")) { Append("menu refused (not Tools/Iron Requiem/): " + arg); break; }
                    Append($"menu {arg} -> {EditorApplication.ExecuteMenuItem(arg)}"); break;
                case "play": EditorApplication.isPlaying = true; Append("play"); waitUntil = EditorApplication.timeSinceStartup + 3; break;
                case "stop": EditorApplication.isPlaying = false; Append("stop"); waitUntil = EditorApplication.timeSinceStartup + 3; break;
                case "wait": waitUntil = EditorApplication.timeSinceStartup + float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture); Append("wait " + arg); break;
                case "timescale": Time.timeScale = float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture); Append("timescale " + Time.timeScale); break;
                case "screenshot":
                    var file = Path.Combine(Dir, string.IsNullOrEmpty(arg) ? "shot.png" : arg);
                    ScreenCapture.CaptureScreenshot(file); Append("screenshot " + file + " (written next frame)"); waitUntil = EditorApplication.timeSinceStartup + 1; break;
                case "click": Append(Click(arg)); break;
                case "clearlog": File.WriteAllText(LogFile, ""); Append("clearlog"); break;
                default: Append("unknown " + line); break;
            }
        }

        static string Click(string target)
        {
            foreach (var b in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                if (!b.isActiveAndEnabled || !b.interactable) continue;
                var label = b.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (string.Equals(b.name, target, StringComparison.OrdinalIgnoreCase) || (label && string.Equals(label.text.Trim(), target, StringComparison.OrdinalIgnoreCase)))
                { b.onClick.Invoke(); return "click " + target + " -> " + b.name; }
            }
            return "click " + target + " -> NOT FOUND";
        }

        static void Append(string s) { try { File.AppendAllText(ResultFile, s + "\n"); } catch { } }
        // Survive domain reloads (play mode / compile) without losing queued commands.
        static void Persist() => SessionState.SetString("IRBridgeQueue", string.Join("\n", pending));
        static void Restore()
        {
            pending.Clear();
            foreach (var l in SessionState.GetString("IRBridgeQueue", "").Split('\n')) if (!string.IsNullOrWhiteSpace(l)) pending.Enqueue(l);
            if (pending.Count > 0) Append("restored " + pending.Count + " queued commands after reload");
        }
    }
}
