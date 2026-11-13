using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    /// 12/11/2026: user-approved install of Timeline (stable, Unity) for the wake-up cinematic.
    /// Uses the Package Manager Client API in the open Editor (no manual manifest edit). Result written
    /// to Tools/IronRequiem/Reports/Packages/timeline.txt.
    public static class IronRequiemPackageInstall
    {
        const string Report = "Tools/IronRequiem/Reports/Packages";
        static AddRequest request;

        [MenuItem("Tools/Iron Requiem/Packages/Add Timeline (approved 12-11)")]
        static void AddTimeline()
        {
            if (request != null && !request.IsCompleted) return;
            Directory.CreateDirectory(Report);
            File.WriteAllText(Report + "/timeline.txt", "requested " + System.DateTime.Now + "\n");
            request = Client.Add("com.unity.timeline");
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (request == null || !request.IsCompleted) return;
            EditorApplication.update -= Poll;
            var line = request.Status == StatusCode.Success
                ? $"ok {request.Result.name}@{request.Result.version} source={request.Result.source}"
                : $"FAILED {request.Error?.message}";
            File.AppendAllText(Report + "/timeline.txt", line + "\n");
            Debug.Log("[Packages] Timeline " + line);
        }
    }
}
