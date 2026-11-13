using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools
{
    // Read-only probe for the hangar intro work: dumps the campaign player rig, HUD, audio and mixer state to a report.
    // Opens RequiemCampaign additively when it is not already open and closes it without saving.
    public static class HangarIntroProbe
    {
        const string Campaign = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        const string ReportDir = "Tools/IronRequiem/Reports/HangarIntro";

        [MenuItem("Tools/Iron Requiem/Hangar Intro/Debug/Probe Campaign Setup")]
        static void Probe()
        {
            var log = new StringBuilder();
            var scene = SceneManager.GetSceneByPath(Campaign);
            bool opened = false;
            if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(Campaign, OpenSceneMode.Additive); opened = true; }
            try
            {
                var player = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Unity.FPS.Gameplay.PlayerCharacterController>(true)).FirstOrDefault();
                log.AppendLine("player=" + (player ? player.name + " pos=" + player.transform.position : "none"));
                if (player)
                {
                    foreach (var t in player.GetComponentsInChildren<Transform>(true))
                    {
                        int depth = 0; for (var p = t; p != player.transform; p = p.parent) depth++;
                        if (depth > 4) continue;
                        var comps = string.Join(",", t.GetComponents<Component>().Where(c => c && !(c is Transform)).Select(c => c.GetType().Name));
                        string extra = "";
                        var cam = t.GetComponent<Camera>(); if (cam) extra += $" fov={cam.fieldOfView} near={cam.nearClipPlane} depth={cam.depth} mask={cam.cullingMask}";
                        var anim = t.GetComponent<Animator>(); if (anim) extra += $" avatar={(anim.avatar ? AssetDatabase.GetAssetPath(anim.avatar) : "-")} human={(anim.avatar && anim.avatar.isHuman)} ctrl={(anim.runtimeAnimatorController ? AssetDatabase.GetAssetPath(anim.runtimeAnimatorController) : "-")}";
                        var smr = t.GetComponent<SkinnedMeshRenderer>(); if (smr) extra += $" mesh={AssetDatabase.GetAssetPath(smr.sharedMesh)}";
                        log.AppendLine(new string(' ', depth * 2) + t.name + (t.gameObject.activeSelf ? "" : " (off)") + " [" + comps + "]" + extra + " lp=" + t.localPosition);
                    }
                    var pcc = player;
                    var so = new SerializedObject(pcc);
                    foreach (var n in new[] { "PlayerCamera", "RotationSpeed", "CameraHeightRatio", "CapsuleHeightStanding" })
                    { var p = so.FindProperty(n); if (p != null) log.AppendLine($"pcc.{n}={(p.propertyType == SerializedPropertyType.ObjectReference ? (p.objectReferenceValue ? p.objectReferenceValue.name : "null") : p.propertyType == SerializedPropertyType.Float ? p.floatValue.ToString() : p.propertyType.ToString())}"); }
                }
                log.AppendLine("-- roots");
                foreach (var g in scene.GetRootGameObjects())
                {
                    var comps = string.Join(",", g.GetComponents<Component>().Where(c => c && !(c is Transform)).Select(c => c.GetType().Name));
                    log.AppendLine(g.name + " [" + comps + "] children=" + g.transform.childCount);
                }
                log.AppendLine("-- canvases");
                foreach (var c in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).Where(c => c.isRootCanvas))
                    log.AppendLine(NodePath(c.transform) + " mode=" + c.renderMode + " order=" + c.sortingOrder + " group=" + (c.GetComponent<CanvasGroup>() != null) + " active=" + c.gameObject.activeInHierarchy);
                log.AppendLine("-- audio sources");
                foreach (var a in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AudioSource>(true)))
                    log.AppendLine(NodePath(a.transform) + " clip=" + (a.clip ? a.clip.name : "-") + " group=" + (a.outputAudioMixerGroup ? a.outputAudioMixerGroup.name : "-"));
                log.AppendLine("-- music/fx managers");
                foreach (var m in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(m => m && (m.GetType().Name.Contains("Music") || m.GetType().Name.Contains("Audio") || m.GetType().Name.Contains("Hud") || m.GetType().Name.Contains("HUD"))))
                    log.AppendLine(NodePath(m.transform) + " " + m.GetType().FullName);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }

            log.AppendLine("-- mixers");
            foreach (var guid in AssetDatabase.FindAssets("t:AudioMixer"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var groups = AssetDatabase.LoadAllAssetsAtPath(path).OfType<UnityEngine.Audio.AudioMixerGroup>().Select(g => g.name);
                log.AppendLine(path + " groups=" + string.Join(",", groups));
            }
            log.AppendLine("-- clips");
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                log.AppendLine(path + " " + (clip ? clip.length.ToString("0.00") + "s" : ""));
            }
            log.AppendLine("-- models with Humanoid avatars");
            foreach (var guid in AssetDatabase.FindAssets("t:Avatar"))
                log.AppendLine(AssetDatabase.GUIDToAssetPath(guid));
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/probe.txt", log.ToString());
            Debug.Log("[HangarIntro] probe written");
        }

        static string NodePath(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }
    }
}
