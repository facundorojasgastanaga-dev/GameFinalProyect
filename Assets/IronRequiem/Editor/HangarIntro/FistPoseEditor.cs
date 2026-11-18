using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    /// Pose the FPS fist by hand: opens the campaign scene, shows the FPS arms, selects the finger bones of one hand and
    /// lets you rotate them with the Rotate tool (E). "Guardar" stores those rotations in the hand's HandFistRig
    /// (useCustomPose) and puts the bones back to rest; the game then closes the hand into that pose.
    public static class FistPoseEditor
    {
        const string Menu = "Tools/Iron Requiem/Fist Pose/";
        const string Campaign = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        static ProtagonistHandsPresentation hands; static bool left; static List<Transform> bones; static Quaternion[] rest; static bool rootWasActive; static Quaternion handRest; static Renderer[] views; static bool[] viewsEnabled;
        static bool Editing => hands && bones != null;

        [MenuItem(Menu + "1a. Editar puño IZQUIERDO")] static void EditLeft() => Begin(true);
        [MenuItem(Menu + "1b. Editar puño DERECHO")] static void EditRight() => Begin(false);
        [MenuItem(Menu + "2. Guardar pose")] static void Save() => End(true);
        [MenuItem(Menu + "3. Cancelar")] static void Cancel() => End(false);
        [MenuItem(Menu + "Seleccionar dedo siguiente %#]")] static void Next() => Cycle(1);
        [MenuItem(Menu + "Seleccionar dedo anterior %#[")] static void Prev() => Cycle(-1);
        [MenuItem(Menu + "Volver al puño automático (izquierdo)")] static void AutoLeft() => Auto(true);
        [MenuItem(Menu + "Volver al puño automático (derecho)")] static void AutoRight() => Auto(false);
        [MenuItem(Menu + "1a. Editar puño IZQUIERDO", true)][MenuItem(Menu + "1b. Editar puño DERECHO", true)]
        static bool CanBegin() => !EditorApplication.isPlayingOrWillChangePlaymode && !Editing;
        [MenuItem(Menu + "2. Guardar pose", true)][MenuItem(Menu + "3. Cancelar", true)][MenuItem(Menu + "Seleccionar dedo siguiente %#]", true)][MenuItem(Menu + "Seleccionar dedo anterior %#[", true)]
        static bool CanEnd() => Editing;

        static ProtagonistHandsPresentation Find()
        {
            var found = Object.FindObjectsByType<ProtagonistHandsPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (found) return found;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
            EditorSceneManager.OpenScene(Campaign, OpenSceneMode.Single);
            return Object.FindObjectsByType<ProtagonistHandsPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        }
        static void Begin(bool leftHand)
        {
            hands = Find(); if (!hands) { EditorUtility.DisplayDialog("Fist Pose", "No encontré ProtagonistHandsPresentation (escena RequiemCampaign).", "OK"); return; }
            left = leftHand; var hand = left ? hands.LeftHandBone : hands.RightHand;
            bones = HandFistRig.FingerBones(hand, hands.IsFingerBoneOf); rest = bones.Select(b => b.localRotation).ToArray(); handRest = hand.localRotation;
            var root = hands.FpsRoot; rootWasActive = root.gameObject.activeSelf; root.gameObject.SetActive(true);
            views = root.GetComponentsInChildren<Renderer>(true); viewsEnabled = views.Select(r => r.enabled).ToArray();
            foreach (var r in views) r.enabled = true; // edit-time view only (restored on save / cancel); the game drives visibility
            // start from the saved hand-made pose, if any
            var rig = left ? hands.LeftFistRig : hands.RightFistRig;
            if (rig.UsesCustomPose) foreach (var b in bones) if (rig.TryGetCustom(b.name, out var q)) b.localRotation = q;
            Selection.activeTransform = bones.FirstOrDefault(); Tools.current = Tool.Rotate; Tools.pivotRotation = PivotRotation.Local;
            var view = SceneView.lastActiveSceneView; if (!view) view = SceneView.GetWindow<SceneView>();
            if (view) { view.Frame(new Bounds(hand.position, Vector3.one * .25f), false); view.ShowNotification(new GUIContent("Gira los dedos con E (rotar)\nCtrl+Shift+] / [ = dedo siguiente / anterior\nTools > Iron Requiem > Fist Pose > 2. Guardar pose"), 6); }
            Debug.Log("[FistPose] editando puño " + (left ? "IZQUIERDO" : "DERECHO") + ": " + string.Join(", ", bones.Select(b => b.name)));
        }
        static void Cycle(int step)
        {
            if (!Editing) return; int i = bones.IndexOf(Selection.activeTransform); i = ((i < 0 ? 0 : i + step) % bones.Count + bones.Count) % bones.Count;
            Selection.activeTransform = bones[i]; EditorGUIUtility.PingObject(bones[i]);
        }
        static void End(bool save)
        {
            if (!Editing) return;
            var hand = left ? hands.LeftHandBone : hands.RightHand;
            if (save)
            {
                Undo.RecordObject(hands, "Guardar pose de puño");
                (left ? hands.LeftFistRig : hands.RightFistRig).StoreCustom(bones);
                EditorUtility.SetDirty(hands); PrefabUtility.RecordPrefabInstancePropertyModifications(hands);
            }
            // bones back to rest; the scene keeps only the saved values on the component
            for (int i = 0; i < bones.Count; i++) if (bones[i]) bones[i].localRotation = rest[i];
            hand.localRotation = handRest;
            for (int i = 0; i < views.Length; i++) if (views[i]) views[i].enabled = viewsEnabled[i];
            hands.FpsRoot.gameObject.SetActive(rootWasActive);
            if (save) { EditorSceneManager.MarkSceneDirty(hands.gameObject.scene); EditorSceneManager.SaveScene(hands.gameObject.scene); }
            Debug.Log("[FistPose] " + (save ? "pose guardada en el puño " + (left ? "IZQUIERDO" : "DERECHO") + " (escena guardada)" : "edición cancelada"));
            bones = null; hands = null;
        }
        static void Auto(bool leftHand)
        {
            var h = Find(); if (!h) return; Undo.RecordObject(h, "Puño automático");
            (leftHand ? h.LeftFistRig : h.RightFistRig).UseCustomPose = false; EditorUtility.SetDirty(h); PrefabUtility.RecordPrefabInstancePropertyModifications(h);
            EditorSceneManager.MarkSceneDirty(h.gameObject.scene); EditorSceneManager.SaveScene(h.gameObject.scene);
            Debug.Log("[FistPose] puño " + (leftHand ? "izquierdo" : "derecho") + " vuelve al automático");
        }
    }
}
