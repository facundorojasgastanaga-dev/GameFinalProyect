using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using Unity.FPS.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem.EditorTools
{
    /// F6 UI: industrial HUD. Template prefabs untouched: VP copies + scene-instance overrides in RequiemCampaign.
    public static class VisualPassF6
    {
        const string Menu = "Tools/Iron Requiem/Visual Pass/F6 ";
        const string Out = "Assets/IronRequiem/Prefabs/VisualPass/UI";
        const string AmmoSrc = "Assets/FPS/Prefabs/UI/AmmoCounter.prefab";
        const string ToastSrc = "Assets/FPS/Prefabs/UI/NotificationToast.prefab";
        const string AmmoDst = Out + "/VP_AmmoCounter.prefab";
        const string ToastDst = Out + "/VP_NotificationToast.prefab";
        static readonly string Report = "Tools/IronRequiem/Reports/VisualPass/F6";

        static readonly Color Panel = new Color(.035f, .04f, .048f, .88f);
        static readonly Color Edge = new Color(.62f, .3f, .07f, 1f);
        static readonly Color Orange = new Color(1f, .52f, .12f, 1f);
        static readonly Color Warm = new Color(1f, .93f, .84f, 1f);
        static readonly Color Dim = new Color(.16f, .17f, .19f, .9f);

        [MenuItem(Menu + "Audit HUD (read only)")]
        static void Audit()
        {
            var sb = new StringBuilder();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var s = c.GetComponent<CanvasScaler>();
                sb.AppendLine($"Canvas {Path(c.transform)} mode={c.renderMode} scaler={(s ? s.referenceResolution + " match " + s.matchWidthOrHeight : "-")}");
            }
            foreach (var r in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string p = Path(r);
                if (!(p.Contains("GameHUD") || p.Contains("Campaign") || p.Contains("Mechanic"))) continue;
                var g = r.GetComponent<Graphic>();
                sb.AppendLine($"{p} a={r.anchorMin}-{r.anchorMax} pos={r.anchoredPosition} size={r.sizeDelta} {(g ? g.GetType().Name + " " + g.color : "")}{(g is TMP_Text t ? " '" + t.text.Replace("\n", " ") + "' fs=" + t.fontSize : "")}");
            }
            var flow = Object.FindAnyObjectByType<CampaignFlow>(FindObjectsInactive.Include);
            if (flow) { var m = new SerializedObject(flow).FindProperty("message").objectReferenceValue as TMP_Text; sb.AppendLine("CampaignFlow.message = " + (m ? Path(m.transform) : "null")); }
            Write("f6-u0-hud-audit.txt", sb.ToString());
        }

        [MenuItem(Menu + "Industrial HUD")]
        static void Apply()
        {
            Directory.CreateDirectory(Out);
            Directory.CreateDirectory("Tools/IronRequiem/Backups/20261109-visualpass-f6");
            var log = new StringBuilder();
            var font = FindFont();

            // 1) Weapon slot
            var ammo = PrefabUtility.LoadPrefabContents(AmmoSrc);
            var ac = ammo.GetComponent<AmmoCounter>();
            foreach (var img in ammo.GetComponentsInChildren<Image>(true))
            {
                if (img == ac.WeaponImage) { img.color = Warm; continue; }
                if (img == ac.AmmoFillImage) { img.color = Orange; continue; }
                img.color = img == ac.AmmoBackgroundImage ? Dim : new Color(Panel.r, Panel.g, Panel.b, img.color.a * Panel.a);
            }
            foreach (var t in ammo.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                t.color = t == ac.WeaponIndexText ? Orange : Warm;
                if (t == ac.BulletCounter) t.fontStyle |= FontStyles.Bold;
            }
            var fb = ac.FillBarColorChange;
            if (fb)
            {
                fb.DefaultForegroundColor = Orange; fb.FlashForegroundColorFull = new Color(1f, .86f, .55f, 1f);
                fb.DefaultBackgroundColor = Dim; fb.FlashBackgroundColorEmpty = new Color(.55f, .05f, .03f, .95f);
            }
            AddFrame(ammo.transform as RectTransform, Panel, Edge, 10, 6);
            PrefabUtility.SaveAsPrefabAsset(ammo, AmmoDst); PrefabUtility.UnloadPrefabContents(ammo);
            log.AppendLine("VP_AmmoCounter: dark slots, orange fill, frame");

            // 2) Toast
            var toast = PrefabUtility.LoadPrefabContents(ToastSrc);
            foreach (var img in toast.GetComponentsInChildren<Image>(true)) img.color = Panel;
            foreach (var t in toast.GetComponentsInChildren<TextMeshProUGUI>(true)) { t.color = Warm; t.characterSpacing = 3; t.fontStyle |= FontStyles.UpperCase; }
            AddFrame(toast.transform as RectTransform, Panel, Edge, 8, 4);
            PrefabUtility.SaveAsPrefabAsset(toast, ToastDst); PrefabUtility.UnloadPrefabContents(toast);
            log.AppendLine("VP_NotificationToast: dark plate, uppercase");

            // 3) Point scene GameHUD to the copies
            foreach (var whm in Object.FindObjectsByType<WeaponHUDManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { Undo.RecordObject(whm, "F6"); whm.AmmoCounterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AmmoDst); PrefabUtility.RecordPrefabInstancePropertyModifications(whm); log.AppendLine("WeaponHUDManager -> VP_AmmoCounter (" + Path(whm.transform) + ")"); }
            foreach (var nhm in Object.FindObjectsByType<NotificationHUDManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { Undo.RecordObject(nhm, "F6"); nhm.NotificationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ToastDst); PrefabUtility.RecordPrefabInstancePropertyModifications(nhm); log.AppendLine("NotificationHUDManager -> VP_NotificationToast"); }

            // 4) Health bar
            foreach (var hb in Object.FindObjectsByType<PlayerHealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (hb.HealthFillImage)
            {
                var root = hb.HealthFillImage.transform.parent as RectTransform;
                foreach (var img in root.GetComponentsInChildren<Image>(true))
                {
                    Undo.RecordObject(img, "F6");
                    if (img == hb.HealthFillImage) img.color = Orange;
                    else if (img.name.Contains("Background")) img.color = Dim;
                    else img.color = Warm; // icon
                    PrefabUtility.RecordPrefabInstancePropertyModifications(img);
                }
                foreach (var f in root.GetComponentsInChildren<FillBarColorChange>(true))
                {
                    Undo.RecordObject(f, "F6");
                    f.DefaultForegroundColor = Orange; f.FlashForegroundColorFull = new Color(1f, .86f, .55f, 1f);
                    f.DefaultBackgroundColor = Dim; f.FlashBackgroundColorEmpty = new Color(.55f, .05f, .03f, .95f);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(f);
                }
                var frame = AddFrame(root, Panel, Edge, 8, 4);
                var tint = root.GetComponent<HudHealthTint>(); if (!tint) tint = Undo.AddComponent<HudHealthTint>(root.gameObject);
                tint.Configure(hb.HealthFillImage, frame);
                EditorUtility.SetDirty(tint);
                if (font && !root.Find("VP_Label"))
                {
                    var lbl = Label(root, "VP_Label", "INTEGRIDAD", font, 9, Orange);
                    lbl.alignment = TextAlignmentOptions.BottomLeft;
                    var r = lbl.rectTransform; r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0, 0);
                    r.anchoredPosition = new Vector2(2, 9); r.sizeDelta = new Vector2(0, 14);
                }
                log.AppendLine("Health: frame + label + HudHealthTint (" + Path(root) + ")");
            }

            // 5) Sector / round banner: below compass, plate + punch
            var flow = Object.FindAnyObjectByType<CampaignFlow>(FindObjectsInactive.Include);
            var msg = flow ? new SerializedObject(flow).FindProperty("message").objectReferenceValue as TMP_Text : null;
            if (msg)
            {
                var r = msg.rectTransform; Undo.RecordObject(r, "F6"); Undo.RecordObject(msg, "F6");
                log.AppendLine($"Banner before: a={r.anchorMin}-{r.anchorMax} pos={r.anchoredPosition} size={r.sizeDelta} fs={msg.fontSize}");
                r.anchorMin = r.anchorMax = new Vector2(.5f, 1); r.pivot = new Vector2(.5f, 1);
                r.anchoredPosition = new Vector2(0, -112); r.sizeDelta = new Vector2(700, 40);
                msg.alignment = TextAlignmentOptions.Center; msg.textWrappingMode = TextWrappingModes.NoWrap;
                msg.fontSize = 20; msg.characterSpacing = 6; msg.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                msg.color = new Color(1f, .82f, .6f, 1f);
                var parent = r.parent;
                var plateT = parent.Find("VP_BannerPlate") as RectTransform;
                IndustrialPanelGraphic plate;
                if (!plateT)
                {
                    var go = new GameObject("VP_BannerPlate", typeof(RectTransform), typeof(CanvasRenderer), typeof(IndustrialPanelGraphic));
                    Undo.RegisterCreatedObjectUndo(go, "F6");
                    plateT = go.transform as RectTransform; plateT.SetParent(parent, false);
                }
                plateT.SetSiblingIndex(r.GetSiblingIndex());
                plateT.anchorMin = plateT.anchorMax = new Vector2(.5f, 1); plateT.pivot = new Vector2(.5f, 1);
                plateT.anchoredPosition = new Vector2(0, -108); plateT.sizeDelta = new Vector2(730, 48);
                plate = plateT.GetComponent<IndustrialPanelGraphic>(); plate.raycastTarget = false;
                plate.Configure(new Color(.03f, .035f, .04f, .82f), Edge, 12);
                var bp = msg.GetComponent<HudBannerPlate>(); if (!bp) bp = Undo.AddComponent<HudBannerPlate>(msg.gameObject);
                bp.Configure(msg, plate); EditorUtility.SetDirty(bp);
                log.AppendLine("Banner: top-center y-72 under compass, plate + punch (" + Path(r) + ")");
            }
            else log.AppendLine("Banner: CampaignFlow.message not found");

            EditorSceneManager.MarkAllScenesDirty();
            AssetDatabase.SaveAssets();
            Write("f6-u1-hud.txt", log.ToString());
        }


        [MenuItem(Menu + "Polish compass and pause hint")]
        static void Polish()
        {
            var log = new StringBuilder();
            foreach (var r in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string p = Path(r);
                if (!p.Contains("GameHUD/HUD/")) continue;
                var g = r.GetComponent<Graphic>(); if (!g) continue;
                Undo.RecordObject(g, "F6b");
                if (p.EndsWith("Compass/CompassBackground")) g.color = new Color(.03f, .035f, .04f, .72f);
                else if (p.EndsWith("CompassBackground/LineImage")) g.color = new Color(1f, .52f, .12f, .55f);
                else if (p.EndsWith("PauseMenuInfo/TABImage")) g.color = new Color(.035f, .04f, .048f, .88f);
                else if (p.EndsWith("PauseMenuInfo/TABImage/Text") && g is TMP_Text k) k.color = Orange;
                else if (p.EndsWith("PauseMenuInfo/Text (TMP)") && g is TMP_Text t) { t.text = "PAUSA / OPCIONES"; t.color = new Color(1f, .93f, .84f, .8f); t.characterSpacing = 4; }
                else continue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(g);
                log.AppendLine(p);
            }
            var flow = Object.FindAnyObjectByType<CampaignFlow>(FindObjectsInactive.Include);
            var msg = flow ? new SerializedObject(flow).FindProperty("message").objectReferenceValue as TMP_Text : null;
            if (msg)
            {
                Undo.RecordObject(msg.rectTransform, "F6b"); msg.rectTransform.anchoredPosition = new Vector2(0, -112);
                var plate = msg.rectTransform.parent.Find("VP_BannerPlate") as RectTransform;
                if (plate) { Undo.RecordObject(plate, "F6b"); plate.anchoredPosition = new Vector2(0, -108); }
                log.AppendLine("Banner y -112");
            }
            EditorSceneManager.MarkAllScenesDirty();
            Write("f6-u2-polish.txt", log.ToString());
        }


        [MenuItem(Menu + "Menu shell motion (pause + intro)")]
        static void MenuMotion()
        {
            var log = new StringBuilder();
            UnityEngine.SceneManagement.Scene intro = default; bool openedIntro = false;
            var introGuid = AssetDatabase.FindAssets("IntroMenu t:Scene").FirstOrDefault();
            if (introGuid != null)
            {
                var path = AssetDatabase.GUIDToAssetPath(introGuid);
                intro = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
                if (!intro.isLoaded) { intro = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); openedIntro = true; }
            }
            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s); if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var shell in root.GetComponentsInChildren<RectTransform>(true).Where(t => t.name == "IndustrialShell"))
                {
                    var frame = shell.Find("ConsoleFrame") as RectTransform; if (!frame) continue;
                    var panels = new[] { "Home", "OptionsPanel", "CreditsPanel" }.Select(n => frame.Find(n) as RectTransform).Where(x => x).ToArray();
                    var seams = Enumerable.Range(0, 12).Select(i => shell.Find("HullSeam" + i) as RectTransform).Where(x => x).ToArray();
                    var m = shell.GetComponent<MenuShellMotion>(); if (!m) m = Undo.AddComponent<MenuShellMotion>(shell.gameObject);
                    m.Configure(frame, panels, seams); EditorUtility.SetDirty(m);
                    EditorSceneManager.MarkSceneDirty(scene);
                    log.AppendLine($"{scene.name}: {Path(shell)} panels={panels.Length} seams={seams.Length}");
                }
            }
            EditorSceneManager.SaveOpenScenes();
            if (openedIntro) EditorSceneManager.CloseScene(intro, true);
            Write("f6-u3-menu-motion.txt", log.ToString());
        }

        static IndustrialPanelGraphic AddFrame(RectTransform host, Color fill, Color edge, float corner, float pad)
        {
            var t = host.Find("VP_Frame") as RectTransform;
            if (!t)
            {
                var go = new GameObject("VP_Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(IndustrialPanelGraphic), typeof(LayoutElement));
                if (!PrefabUtility.IsPartOfPrefabAsset(host)) Undo.RegisterCreatedObjectUndo(go, "F6");
                t = go.transform as RectTransform; t.SetParent(host, false);
            }
            t.SetAsFirstSibling();
            t.GetComponent<LayoutElement>().ignoreLayout = true;
            t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one; t.pivot = new Vector2(.5f, .5f);
            t.offsetMin = new Vector2(-pad, -pad); t.offsetMax = new Vector2(pad, pad);
            var g = t.GetComponent<IndustrialPanelGraphic>(); g.raycastTarget = false; g.Configure(fill, edge, corner);
            return g;
        }

        static TextMeshProUGUI Label(Transform parent, string name, string text, TMP_FontAsset font, float size, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(go, "F6");
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font; t.text = text; t.fontSize = size; t.color = c; t.characterSpacing = 6; t.fontStyle = FontStyles.Bold; t.raycastTarget = false;
            return t;
        }

        static TMP_FontAsset FindFont()
        {
            var flow = Object.FindAnyObjectByType<CampaignFlow>(FindObjectsInactive.Include);
            var m = flow ? new SerializedObject(flow).FindProperty("message").objectReferenceValue as TMP_Text : null;
            return m ? m.font : TMP_Settings.defaultFontAsset;
        }

        static string Path(Transform t) { var s = t.name; while ((t = t.parent)) s = t.name + "/" + s; return s; }
        static void Write(string file, string text) { Directory.CreateDirectory(Report); File.WriteAllText(System.IO.Path.Combine(Report, file), text); Debug.Log("[F6] " + file + "\n" + text); }
    }
}
