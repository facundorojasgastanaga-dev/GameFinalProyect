using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools
{
    public static class HandPoseAuthoring
    {
        const string Campaign = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        const string Folder = "Assets/IronRequiem/Art/Player/HandPoses";
        const string Report = "Tools/IronRequiem/Reports/HandsHudCampaign/pose-bake.txt";
        static readonly string[] FingerNames = { "Index", "Middle", "Ring", "Little", "Thumb" };
        static readonly float[] Knuckles = { 85, 90, 92, 95 };

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Apply hand poses")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserving dirty scene.");
            var scene = SceneManager.GetSceneByPath(Campaign); bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(Campaign, OpenSceneMode.Additive);
            try
            {
                var hands = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ProtagonistHandsPresentation>(true)).Single();
                var log = new StringBuilder("Absolute hand poses from mesh bind matrices\n");
                var left = Bake(hands, true, log); var right = Bake(hands, false, log);
                var controller = hands.GetComponent<HandPoseController>();
                if (!controller) controller = Undo.AddComponent<HandPoseController>(hands.gameObject);
                var player = hands.GetComponent<Unity.FPS.Gameplay.PlayerCharacterController>();
                controller.Configure(hands.LeftHandBone, left, hands.RightHand, right, player.PlayerCamera.transform);
                Undo.RecordObject(hands, "Use absolute hand poses"); hands.ConfigureHandPoses(controller);
                EditorUtility.SetDirty(controller); EditorUtility.SetDirty(hands);
                PrefabUtility.RecordPrefabInstancePropertyModifications(hands);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Directory.CreateDirectory(Path.GetDirectoryName(Report)); File.WriteAllText(Report, log.ToString());
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        static HandPoseLibrary Bake(ProtagonistHandsPresentation owner, bool left, StringBuilder log)
        {
            string side = left ? "L" : "R";
            var hand = left ? owner.LeftHandBone : owner.RightHand;
            var skin = owner.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.name == "Hand_" + side + "_FPS");
            return BakeRig(hand,skin,left,"FPS_",log);
        }

        internal static HandPoseLibrary BakeRig(Transform hand,SkinnedMeshRenderer skin,bool left,string prefix,StringBuilder log)
        {
            string side=left?"L":"R";
            bool mixamo=hand.name.IndexOf("mixamorig",StringComparison.OrdinalIgnoreCase)>=0;
            var all = hand.GetComponentsInChildren<Transform>(true);
            var originals = all.Select(t => t.localRotation).ToArray();
            var bones = skin.bones; var matrices = skin.sharedMesh.bindposes;
            var index = new Dictionary<Transform, int>();
            for (int i = 0; i < bones.Length; i++) if (bones[i]) index[bones[i]] = i;
            var data = new List<HandPoseLibrary.BonePose>(15);
            try
            {
                // Recover local bind rotations; never treat the scene's already curled fingers as bind.
                foreach (var node in all)
                    if (node != hand && node.parent && index.TryGetValue(node, out int child) && index.TryGetValue(node.parent, out int parent))
                        node.localRotation = Quaternion.Inverse(matrices[parent].inverse.rotation) * matrices[child].inverse.rotation;
                var chains = new Transform[5][];
                for (int f = 0; f < 5; f++)
                    chains[f] = Enumerable.Range(1, 3).Select(j => all.Single(t => t.name == (mixamo?("mixamorig:"+(left?"Left":"Right")+"Hand"+(f==3?"Pinky":FingerNames[f])+j):FingerNames[f]+j+"_"+side))).ToArray();
                Vector3 direction = (chains[1][0].position - hand.position).normalized;
                Vector3 across = (chains[0][0].position - chains[3][0].position).normalized;
                Vector3 normal = Vector3.Cross(across, direction).normalized;
                Vector3 bend = Vector3.zero;
                for (int f = 0; f < 4; f++)
                    bend += (chains[f][1].position - chains[f][0].position).normalized - direction;
                if (Vector3.Dot(normal, bend) < 0) normal = -normal;
                var localDirection = hand.InverseTransformDirection(direction);
                var localNormal = hand.InverseTransformDirection(normal);
                var center = hand.position + direction * .045f + normal * .012f;
                log.AppendLine($"Hand {side}: palm={localNormal:F4}; forward={localDirection:F4}");

                for (int f = 0; f < 4; f++)
                {
                    var chain = chains[f]; var axes = new Vector3[3]; var bindFlex = new float[3];
                    var bind = chain.Select(t => t.localRotation).ToArray();
                    for (int j = 0; j < 3; j++)
                    {
                        var node = chain[j]; var next = j < 2 ? chain[j + 1] : null;
                        Vector3 boneDirection = next ? (next.position - node.position).normalized : node.TransformDirection(Vector3.up);
                        var axis = Vector3.Cross(boneDirection, normal).normalized;
                        var tip = node.position + boneDirection * .03f;
                        bool signPass = (node.position + Quaternion.AngleAxis(15, axis) * (tip - node.position) - center).sqrMagnitude < (tip - center).sqrMagnitude;
                        if (!signPass) axis = -axis;
                        bool verified=(node.position + Quaternion.AngleAxis(15, axis) * (tip - node.position) - center).sqrMagnitude < (tip - center).sqrMagnitude;
                        axes[j] = node.InverseTransformDirection(axis);
                        var before = j == 0 ? direction : (node.position - chain[j - 1].position).normalized;
                        bindFlex[j] = Vector3.SignedAngle(before, boneDirection, axis);
                        log.AppendLine($"{node.name}: target={(j == 0 ? Knuckles[f] : j == 1 ? 102 : 65):F1}; bindFlex={bindFlex[j]:F2}; axis={axes[j]:F4}; +15 verified={verified}; signInverted={!signPass}; scale={node.localScale}");
                    }
                    for (int j = 0; j < 3; j++)
                    {
                        float target = j == 0 ? Knuckles[f] : j == 1 ? 102 : 65;
                        var open = bind[j] * Quaternion.AngleAxis(-bindFlex[j], axes[j]);
                        var closed = bind[j] * Quaternion.AngleAxis(target - bindFlex[j], axes[j]);
                        chain[j].localRotation = closed;
                        data.Add(new HandPoseLibrary.BonePose { name = chain[j].name, bind = bind[j], open = open,
                            relaxed = Quaternion.Slerp(open, closed, .16f), fist = closed,
                            grip = Quaternion.Slerp(open, closed, .65f), inspect = Quaternion.Slerp(open, closed, .06f) });
                    }
                }
                // The thumb stays outside the curled fingers, toward the middle phalanges, with bounded articulation.
                var thumb = chains[4];
                Vector3 targetThumb = (chains[0][1].position + chains[1][1].position) * .5f + normal * .018f;
                float[] thumbAngles = { 40, 37.5f, 27.5f };
                for (int j = 0; j < 3; j++)
                {
                    var node = thumb[j]; var bind = node.localRotation;
                    Vector3 own = j < 2 ? thumb[j + 1].position - node.position : node.TransformDirection(Vector3.up) * .025f;
                    var axis = Vector3.Cross(own, targetThumb - node.position).normalized;
                    if (axis.sqrMagnitude < .5f) axis = Vector3.Cross(own.normalized, normal).normalized;
                    var closed = bind * Quaternion.AngleAxis(thumbAngles[j], node.InverseTransformDirection(axis));
                    node.localRotation = closed;
                    data.Add(new HandPoseLibrary.BonePose { name = node.name, bind = bind, open = bind, relaxed = bind,
                        fist = closed, grip = Quaternion.Slerp(bind, closed, .7f), inspect = bind });
                    log.AppendLine($"{node.name}: flex={thumbAngles[j]:F1}; scale={node.localScale}");
                }
                Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
                string path = Folder + "/"+prefix+side+".asset";
                var library = AssetDatabase.LoadAssetAtPath<HandPoseLibrary>(path);
                if (!library) { library = ScriptableObject.CreateInstance<HandPoseLibrary>(); AssetDatabase.CreateAsset(library, path); }
                library.Configure(data.ToArray(), localDirection, localNormal); EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
                return library;
            }
            finally { for (int i = 0; i < all.Length; i++) all[i].localRotation = originals[i]; }
        }
    }
}
