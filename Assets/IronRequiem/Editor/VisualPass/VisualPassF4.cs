using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using Unity.FPS.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools
{
    /// F4 game feel: heavy deaths, directional damage HUD, screen-shake option slider.
    public static class VisualPassF4
    {
        const string Menu = "Tools/Iron Requiem/Visual Pass/F4 ";
        const string Report = "Tools/IronRequiem/Reports/VisualPass/F4";
        const string Backup = "Tools/IronRequiem/Backups/20261109-visualpass-f4";

        [MenuItem(Menu + "Game feel (deaths, damage arcs, shake option)")]
        static void Apply()
        {
            var log = new StringBuilder();
            Directory.CreateDirectory(Backup);

            // 1) Heavy deaths on the campaign Big Bot / Saw Bot.
            Heavy("Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab", .09f, 24, .95f, .5f, new Vector3(-2.6f, 0, 1.4f), log);
            Heavy("Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab", .07f, 18, .75f, .4f, new Vector3(-1.8f, 0, 1f), log);

            // 2) Directional damage arcs on every GameHUD/HUD canvas of the open scene.
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.name == "HUD" && c.transform.parent && c.transform.parent.name == "GameHUD"))
            {
                var t = c.transform.Find("VP_DamageDirection") as RectTransform;
                if (!t)
                {
                    var go = new GameObject("VP_DamageDirection", typeof(RectTransform));
                    Undo.RegisterCreatedObjectUndo(go, "F4"); t = (RectTransform)go.transform; t.SetParent(c.transform, false);
                }
                t.anchorMin = t.anchorMax = t.pivot = new Vector2(.5f, .5f); t.anchoredPosition = Vector2.zero; t.sizeDelta = new Vector2(10, 10);
                if (!t.GetComponent<DamageDirectionHud>()) Undo.AddComponent<DamageDirectionHud>(t.gameObject);
                log.AppendLine("DamageDirectionHud -> " + Path(t));
            }

            // 3) Screen-shake slider in every industrial Options panel (campaign pause, lab, intro).
            Scene intro = default; bool opened = false;
            var g = AssetDatabase.FindAssets("IntroMenu t:Scene").FirstOrDefault();
            if (g != null) { var p = AssetDatabase.GUIDToAssetPath(g); intro = SceneManager.GetSceneByPath(p); if (!intro.isLoaded) { intro = EditorSceneManager.OpenScene(p, OpenSceneMode.Additive); opened = true; } }
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s); if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var options in root.GetComponentsInChildren<RectTransform>(true).Where(t => t.name == "OptionsPanel" && t.parent && t.parent.name == "ConsoleFrame"))
                {
                    var sens = options.Find("LookSensitivity") as RectTransform; if (!sens) continue;
                    var row = options.Find("ScreenShake") as RectTransform;
                    if (!row)
                    {
                        row = (RectTransform)Object.Instantiate(sens.gameObject, options).transform; row.name = "ScreenShake";
                        Undo.RegisterCreatedObjectUndo(row.gameObject, "F4");
                    }
                    Move(options, "MasterVolume", 95); Move(options, "LookSensitivity", 22); Move(options, "ScreenShake", -51); Move(options, "Fullscreen", -122);
                    var label = row.Find("Label")?.GetComponent<TMP_Text>(); if (label) label.text = "TEMBLOR DE PANTALLA";
                    var value = row.Find("Value")?.GetComponent<TMP_Text>();
                    var slider = row.GetComponentInChildren<UnityEngine.UI.Slider>(true);
                    if (slider)
                    {
                        slider.onValueChanged = new UnityEngine.UI.Slider.SliderEvent();
                        slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
                        var b = slider.GetComponent<ShakeSliderBinding>(); if (!b) b = slider.gameObject.AddComponent<ShakeSliderBinding>();
                        b.Configure(value); EditorUtility.SetDirty(b);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    log.AppendLine($"{scene.name}: shake slider -> {Path(row)}");
                }
            }
            EditorSceneManager.SaveOpenScenes();
            if (opened) EditorSceneManager.CloseScene(intro, true);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f4-g1.txt", log.ToString()); Debug.Log("[F4]\n" + log);
        }


        [MenuItem(Menu + "Diag damage arcs (play)")]
        static void DiagArcs()
        {
            var sb = new StringBuilder();
            foreach (var w in Object.FindObjectsByType<DamageWedgeGraphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var c = w.canvas;
                sb.AppendLine($"{Path(w.transform)} en={w.enabled} R={w.Radius} T={w.Thickness} lossy={w.transform.lossyScale} canvas={(c ? c.name + " sf=" + c.scaleFactor + " mode=" + c.renderMode : "-")} parentLossy={w.transform.parent.lossyScale} alpha={w.canvasRenderer.GetAlpha()}");
            }
            foreach (var g in Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None))
                if (g.isActiveAndEnabled && g.color.r > .8f && g.color.g < .3f && g.color.b < .3f) sb.AppendLine($"red graphic: {Path(g.transform)} {g.GetType().Name} {g.color} alpha={g.canvasRenderer.GetAlpha()}");
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f4-diag-arcs.txt", sb.ToString()); Debug.Log("[F4 diag]\n" + sb);
        }


        [MenuItem(Menu + "Damage flash on all HUDs")]
        static void FlashAll()
        {
            var log = new StringBuilder();
            var sprite = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath("61e9c8b64d003e047ab7cfc7f5d74555")).OfType<Sprite>().FirstOrDefault();
            foreach (var hud in Object.FindObjectsByType<Unity.FPS.UI.FeedbackFlashHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = new SerializedObject(hud);
                var image = data.FindProperty("FlashImage").objectReferenceValue as UnityEngine.UI.Image;
                log.AppendLine($"{Path(hud.transform)} before sprite={(image && image.sprite ? image.sprite.name : "none")} alpha={data.FindProperty("DamageFlashMaxAlpha").floatValue} critical={data.FindProperty("CriticaHealthVignetteMaxAlpha").floatValue}");
                if (image && sprite) { Undo.RecordObject(image, "F4"); image.sprite = sprite; image.type = UnityEngine.UI.Image.Type.Simple; PrefabUtility.RecordPrefabInstancePropertyModifications(image); }
                data.FindProperty("DamageFlashColor").colorValue = new Color(.6f, .015f, .01f, 1f);
                data.FindProperty("DamageFlashDuration").floatValue = .18f;
                data.FindProperty("DamageFlashMaxAlpha").floatValue = .32f;
                data.FindProperty("CriticaHealthVignetteMaxAlpha").floatValue = .5f;
                data.FindProperty("PulsatingVignetteFrequency").floatValue = 4f;
                data.ApplyModifiedProperties();
            }
            EditorSceneManager.MarkAllScenesDirty(); EditorSceneManager.SaveOpenScenes();
            Directory.CreateDirectory(Report); File.WriteAllText(Report + "/f4-flash.txt", log.ToString()); Debug.Log("[F4 flash]\n" + log);
        }

        static void Heavy(string path, float stop, float radius, float strength, float seconds, Vector3 kick, StringBuilder log)
        {
            if (!File.Exists(path)) { log.AppendLine("missing " + path); return; }
            File.Copy(path, System.IO.Path.Combine(Backup, System.IO.Path.GetFileName(path) + ".bak"), true);
            var root = PrefabUtility.LoadPrefabContents(path);
            var health = root.GetComponentInChildren<Health>(true);
            if (health)
            {
                var f = health.GetComponent<HeavyDeathFeel>(); if (!f) f = health.gameObject.AddComponent<HeavyDeathFeel>();
                f.Configure(stop, radius, strength, seconds, kick);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine($"HeavyDeathFeel -> {path} ({health.name})");
            }
            else log.AppendLine("no Health in " + path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void Move(Transform parent, string name, float y)
        {
            var r = parent.Find(name) as RectTransform; if (!r) return;
            Undo.RecordObject(r, "F4"); r.anchoredPosition = new Vector2(r.anchoredPosition.x, y);
        }
        static string Path(Transform t) { var s = t.name; while ((t = t.parent)) s = t.name + "/" + s; return s; }
    }
}
