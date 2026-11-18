using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools
{
    /// <summary>Scoped, explicit tools for the hands/HUD/campaign repair. Never writes menu assets.</summary>
    public static class HandsHudCampaignTools
    {
        const string Report = "Tools/IronRequiem/Reports/HandsHudCampaign";
        const string Campaign = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Preview campaign")]
        public static void PreviewCampaign()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Editor already playing.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserving dirty scene.");
            SessionState.SetString("IR.HandsPreview.Previous", SceneManager.GetActiveScene().path);
            SessionState.SetString("IR.HandsPreview.StartScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene(Campaign);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Begin preview")]
        public static void BeginPreview()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
            var flow = UnityEngine.Object.FindAnyObjectByType<CampaignFlow>();
            Application.runInBackground=true;
            if (flow && flow.Menu) flow.Menu.BeginGame();
            var intro = UnityEngine.Object.FindAnyObjectByType<HangarIntroCinematic>();
            if (intro) intro.RequestSkip();
        }

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Preview left fist")]
        public static void LeftFist() => PreviewFist(true);
        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Preview right fist")]
        public static void RightFist() => PreviewFist(false);
        static void PreviewFist(bool left)
        {
            var hands = UnityEngine.Object.FindAnyObjectByType<ProtagonistHandsPresentation>();
            if (!EditorApplication.isPlaying || !hands) throw new InvalidOperationException("Campaign Play Mode required.");
            var serialized = new SerializedObject(hands);
            serialized.FindProperty("previewLeftFist").boolValue = left;
            serialized.FindProperty("previewRightFist").boolValue = !left;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Capture fists")]
        public static void CaptureFists()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
            var hands = UnityEngine.Object.FindAnyObjectByType<ProtagonistHandsPresentation>();
            var player = hands.GetComponent<Unity.FPS.Gameplay.PlayerCharacterController>();
            Directory.CreateDirectory(Report);
            string tag=hands.RightFistClosure>.95f?"right-fist":hands.LeftFistClosure>.95f?"left-fist":"rest";
            File.WriteAllText(Report+"/"+tag+"-state.txt",$"left={hands.LeftFistClosure:F4}; right={hands.RightFistClosure:F4}\n");
            Capture(player.PlayerCamera, tag+"-fps", 1920, 1080);
            var views = hands.FpsRoot.GetComponentsInChildren<Renderer>(true);
            var saved = views.Select(r => r.enabled).ToArray();
            var go = new GameObject("HandVerificationCamera"); var camera = go.AddComponent<Camera>();
            camera.CopyFrom(player.PlayerCamera); camera.enabled = false; camera.fieldOfView = 36; camera.nearClipPlane = .005f;
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f, .15f, .18f);
            try
            {
                foreach (bool left in new[] { true, false })
                {
                    string side = left ? "L" : "R"; var hand = left ? hands.LeftHandBone : hands.RightHand;
                    for (int i = 0; i < views.Length; i++) views[i].enabled = views[i].name == "Hand_" + side + "_FPS" || views[i].name == "RigidClosedFist" && views[i].transform.IsChildOf(hand);
                    var renderer = views.First(r => r.name == "Hand_" + side + "_FPS"); camera.cullingMask = 1 << renderer.gameObject.layer;
                    var center = renderer.bounds.center;
                    var view = player.PlayerCamera.transform;
                    var directions = new[] { view.forward, -view.forward, view.right, view.up };
                    for (int i = 0; i < directions.Length; i++)
                    {
                        var direction = directions[i]; camera.transform.position = center + direction * .42f;
                        camera.transform.rotation = Quaternion.LookRotation(-direction, i == 3 ? view.forward : view.up);
                        Capture(camera, tag+"-hand-" + side + "-" + i, 1920, 1080);
                    }
                }
            }
            finally { for (int i = 0; i < views.Length; i++) if (views[i]) views[i].enabled = saved[i]; UnityEngine.Object.DestroyImmediate(go); }
        }

        static void Capture(Camera camera, string name, int width, int height)
        {
            var target = RenderTexture.GetTemporary(width, height, 24);
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                File.WriteAllBytes(Report + "/" + name + ".png", texture.EncodeToPNG());
            }
            finally { camera.targetTexture = previousTarget; RenderTexture.active = previousActive; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(texture); }
        }

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Capture gameplay state")]
        public static void CaptureGameplayState()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play Mode required.");
            var player=UnityEngine.Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerCharacterController>();
            string tag=SessionState.GetString("IR.HandsHud.CaptureTag","gameplay");Directory.CreateDirectory(Report);
            var camera=player.PlayerCamera;var intro=UnityEngine.Object.FindAnyObjectByType<HangarIntroCinematic>();
            if(intro&&intro.IsPlaying)
            {
                var cinematic=new SerializedObject(intro).FindProperty("cinematicCamera").objectReferenceValue as Camera;
                if(cinematic)camera=cinematic;
            }
            Capture(camera,tag,1920,1080);
            var hands=player.GetComponent<ProtagonistHandsPresentation>();var melee=player.GetComponent<ProtagonistMelee>();
            File.WriteAllText(Report+"/"+tag+"-state.txt",$"attacking={melee.IsAttacking}; charging={melee.IsCharging}; armed={melee.ArmedStrike}; charge={melee.ChargeLevel}; poseTime={melee.PoseTime}; leftClosure={hands.LeftFistClosure}; rightClosure={hands.RightFistClosure}; cinematicTime={(intro?intro.Time01*intro.Duration:0)}; cameraPosition={camera.transform.position}; cameraForward={camera.transform.forward}\n");
        }

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Restore preview scene")]
        public static void RestorePreviewScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserving dirty scene.");
            string path = SessionState.GetString("IR.HandsPreview.Previous", "");
            string start=SessionState.GetString("IR.HandsPreview.StartScene","");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(start)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(start);
            if (!string.IsNullOrEmpty(path)) EditorSceneManager.OpenScene(path);
        }

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Build baseline")]
        public static void BuildBaseline() => Build("before");

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Build repaired")]
        public static void BuildRepaired() => Build("after");

        static void Build(string tag)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserving dirty scene.");
            Directory.CreateDirectory(Report);
            // Match the user's enabled entry scene and ordering, including the animated IntroMenu.
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var result = UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Builds/HandsHudCampaign/" + tag + "/IronRequiem.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            File.WriteAllText(Report + "/build-" + tag + ".txt", $"{result.summary.result}\nerrors={result.summary.totalErrors}\nduration={result.summary.totalTime}\n");
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Development build failed: " + tag);
        }

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Audit")]
        public static void Audit()
        {
            Directory.CreateDirectory(Report);
            var text = new StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
            text.AppendLine($"playing={EditorApplication.isPlaying} compiling={EditorApplication.isCompiling}");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                text.AppendLine($"scene={scene.path} dirty={scene.isDirty}");
            }
            var preview = EditorSceneManager.OpenPreviewScene(Campaign);
            try
            {
                foreach (var root in preview.GetRootGameObjects())
                {
                    foreach (var hands in root.GetComponentsInChildren<ProtagonistHandsPresentation>(true))
                    {
                        text.AppendLine("FPS=" + hands.name);
                        text.AppendLine(EditorJsonUtility.ToJson(hands, true));
                        foreach (var animator in hands.GetComponentsInChildren<Animator>(true))
                            text.AppendLine($"animator={animator.name} controller={AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)} enabled={animator.enabled}");
                        foreach (var skin in hands.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        {
                            text.AppendLine($"skin={skin.name} mesh={AssetDatabase.GetAssetPath(skin.sharedMesh)} scale={skin.transform.lossyScale}");
                            var bones = skin.bones; var bind = skin.sharedMesh.bindposes;
                            for (int b = 0; b < bones.Length && b < bind.Length; b++)
                            {
                                var bone = bones[b]; if (!bone) continue;
                                text.AppendLine($"  {bone.name} parent={bone.parent?.name} pos={bone.localPosition:F5} rot={bone.localEulerAngles:F3} scale={bone.localScale} bind={bind[b]}");
                            }
                        }
                    }
                    foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                        text.AppendLine($"canvas={canvas.name} active={canvas.gameObject.activeSelf} children={string.Join(",",canvas.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t=>t.name))}");
                }
                var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Art/Protagonist/CapsuleRobotRig/CapsuleRobotRig.fbx");
                if (model) foreach (var node in model.GetComponentsInChildren<Transform>(true))
                    text.AppendLine($"capsule={node.name} parent={node.parent?.name} pos={node.localPosition:F5} rot={node.localEulerAngles:F3} scale={node.localScale}");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            File.WriteAllText(Report + "/audit.txt", text.ToString());
            Debug.Log("[HandsHudCampaign] Audit complete");
        }
    }
}
