using System;
using System.IO;
using System.Linq;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools
{
    /// <summary>Imports portrait references and builds an isolated lab, without saving any existing scene.</summary>
    public static class RobotPortraitAuthoring
    {
        const string Art = "Assets/IronRequiem/Art/UI/RobotPortrait/v03/";
        const string Bank = "Assets/IronRequiem/Resources/IronRequiem/RobotPortraitV03.asset";
        public const string Lab = "Assets/IronRequiem/Scenes/RobotPortraitV03Lab.unity";
        static readonly string[] Names = {
            "d0_frente", "d0_izquierda", "d0_derecha", "d0_impacto", "d0_punio_carga",
            "d1_frente", "d1_izquierda", "d1_derecha", "d1_impacto", "d1_punio_carga",
            "d2_frente", "d2_izquierda", "d2_derecha", "d2_impacto", "d2_punio_carga",
            "d3_frente", "d3_izquierda", "d3_derecha", "d3_impacto", "d3_punio_carga", "ataque", "overdrive", "apagado" };

        [MenuItem("Tools/Iron Requiem/Robot Portrait/Audit editor restoration")]
        public static void Audit()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("playing=" + EditorApplication.isPlaying + " compiling=" + EditorApplication.isCompiling);
            report.AppendLine("playModeStartScene=" + AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                report.AppendLine("scene=" + scene.path + " dirty=" + scene.isDirty);
            }
            Directory.CreateDirectory("Tools/IronRequiem/Reports/RobotPortraitV03");
            File.WriteAllText("Tools/IronRequiem/Reports/RobotPortraitV03/editor-restoration.txt", report.ToString());
        }

        [MenuItem("Tools/Iron Requiem/Robot Portrait/Import and create lab")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserving dirty scene.");
            var sprites = new Sprite[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                string path = Art + Names[i] + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 512;
                importer.isReadable = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(.5f, .5f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (!sprites[i]) throw new InvalidOperationException("Missing sprite: " + path);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Bank)); AssetDatabase.Refresh();
            var bank = AssetDatabase.LoadAssetAtPath<RobotPortraitSprites>(Bank);
            if (!bank) { bank = ScriptableObject.CreateInstance<RobotPortraitSprites>(); AssetDatabase.CreateAsset(bank, Bank); }
            bank.ConfigureV02(sprites);
            EditorUtility.SetDirty(bank); AssetDatabase.SaveAssetIfDirty(bank);
            CreateLab();
            Directory.CreateDirectory("Tools/IronRequiem/Reports/RobotPortraitV03");
            File.WriteAllText("Tools/IronRequiem/Reports/RobotPortraitV03/import.txt",
                "23 Sprite Single, alpha FromInput, transparency enabled, Center pivot, FullRect, no mipmaps, no Read/Write, max 512, RGBA uncompressed.\n" +
                "One Resources reference bank; source PNGs kept byte-identical. Lab: " + Lab);
            Debug.Log("[RobotPortraitV03] Imported 23 sprites and created isolated lab. Existing scenes untouched.");
        }

        static void CreateLab()
        {
            const string campaignPath = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";
            var previous = SceneManager.GetActiveScene();
            var source = SceneManager.GetSceneByPath(campaignPath);
            bool opened = !source.isLoaded;
            if (opened) source = EditorSceneManager.OpenScene(campaignPath, OpenSceneMode.Additive);
            var lab = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(lab);
                var managers = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/GameManager.prefab"), lab);
                // Keep the lab running after death; do not launch WinScene/LoseScene during portrait checks.
                managers.GetComponentInChildren<GameFlowManager>(true).enabled = false;
                var original = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerCharacterController>(true)).Single();
                var clone = UnityEngine.Object.Instantiate(original.gameObject);
                clone.name = "PortraitLabPlayer"; SceneManager.MoveGameObjectToScene(clone, lab);
                clone.transform.SetPositionAndRotation(new Vector3(0, .1f, 0), Quaternion.identity);
                foreach (var camera in clone.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var listener in clone.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                var player = clone.GetComponent<PlayerCharacterController>();
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "LabFloor"; floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(40, 1, 40);
                var view = new GameObject("PortraitLabCamera").AddComponent<Camera>();
                view.gameObject.AddComponent<AudioListener>();
                view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.035f, .045f, .055f);
                view.transform.position = new Vector3(0, 20, -10); view.transform.rotation = Quaternion.identity;
                view.nearClipPlane = .1f; view.farClipPlane = 5;
                var root = new GameObject("PortraitLabHUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = view; canvas.planeDistance = 1;
                var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var bar = new GameObject("ClassicStatusBar", typeof(RectTransform)); bar.transform.SetParent(root.transform, false);
                var rect = (RectTransform)bar.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.right; rect.pivot = new Vector2(.5f, 0);
                rect.sizeDelta = new Vector2(0, 180); rect.anchoredPosition = Vector2.zero;
                bar.AddComponent<RetroStatusBar>().Configure(player, TMP_Settings.defaultFontAsset);
                EditorSceneManager.SaveScene(lab, Lab);
            }
            finally
            {
                EditorSceneManager.CloseScene(lab, true);
                if (opened) EditorSceneManager.CloseScene(source, true);
                if (previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
