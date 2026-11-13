using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// 12/11/2026 — restyles the existing options panel (RequiemMenuView: volume, sensitivity, fullscreen, back) as an
    /// industrial plant console: riveted metal plate, vertical faders, a lever switch, a lit back button, status lamps
    /// and a CCTV monitor. Only presentation changes: the same Slider/Toggle/Button components, listeners and
    /// RequiemSettings persistence are kept. Applied to the intro menu and to every RequiemMenuView of the campaign
    /// session (pause and lab menu). Also adds the intro menu's CCTV feed (same view as the menu camera) and the
    /// Carroñero only that camera sees. Idempotent.
    public static class IndustrialOptionsAuthoring
    {
        const string Intro = "Assets/FPS/Scenes/IntroMenu.unity";
        const string Session = "Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        const string UiDir = "Assets/IronRequiem/Art/Menu/UI";
        const string CctvShader = "IronRequiem/UI/CCTV";
        const string CctvMaterial = "Assets/IronRequiem/Art/Menu/UI/HG_UI_CCTV.mat";
        const string WalkerCarronero = "Assets/IronRequiem/Prefabs/Menu/MenuWalker_Carronero.prefab";
        public const int CctvLayer = 30; // unnamed layer: only the CCTV camera renders it
        static readonly Color Cyan = new Color(.12f, .82f, .91f), Orange = new Color(1f, .6f, .2f), Steel = new Color(.82f, .86f, .9f);
        static StringBuilder log;

        [MenuItem("Tools/Iron Requiem/Menu/Build Industrial Options (menu + pause)")]
        public static void Build()
        {
            log = new StringBuilder("Industrial options " + DateTime.Now + "\n");
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
                for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene open: " + SceneManager.GetSceneAt(i).path);
                var backup = "Tools/IronRequiem/Backups/" + DateTime.Now.ToString("yyyyMMdd-HHmm") + "-industrial-options";
                Directory.CreateDirectory(backup);
                foreach (var f in new[] { Intro, Session }) { File.Copy(f, Path.Combine(backup, Path.GetFileName(f)), true); File.Copy(f + ".meta", Path.Combine(backup, Path.GetFileName(f) + ".meta"), true); }
                ImportSprites();
                var mat = CctvMat();
                foreach (var path in new[] { Session, Intro }) // intro last: it stays open for the play check
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var views = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RequiemMenuView>(true)).ToArray();
                    foreach (var v in views) Restyle(v, mat);
                    if (path == Intro) BuildIntroFeed(scene);
                    UiUnifyAuthoring.Apply(scene, UiUnifyAuthoring.EnsureTheme(log), log); // v5: shared style + large centred options overlay
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    log.AppendLine($"{path}: {views.Length} options panel(s)");
                }
                Debug.Log("[IndustrialOptions] ok\n" + log);
            }
            catch (Exception e) { Debug.LogError("[IndustrialOptions] " + e); log.AppendLine("ERROR " + e); }
            Directory.CreateDirectory("Tools/IronRequiem/Reports/HangarIntro");
            File.WriteAllText("Tools/IronRequiem/Reports/HangarIntro/options.txt", log.ToString());
        }

        static Sprite Spr(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(UiDir + "/" + name + ".png");

        static void ImportSprites()
        {
            var borders = new (string name, Vector4 border)[]
            {
                ("IR_UI_Plate", new Vector4(40, 40, 40, 40)), ("IR_UI_Hazard", Vector4.zero), ("IR_UI_FaderSlot", new Vector4(0, 26, 0, 26)), ("IR_UI_FaderCap", Vector4.zero),
                ("IR_UI_LeverOff", Vector4.zero), ("IR_UI_LeverOn", Vector4.zero), ("IR_UI_Bezel", new Vector4(44, 44, 44, 44)), ("IR_UI_Lamp", Vector4.zero),
            };
            foreach (var (name, border) in borders)
            {
                var ti = AssetImporter.GetAtPath(UiDir + "/" + name + ".png") as TextureImporter; if (!ti) { log.AppendLine("missing sprite " + name); continue; }
                if (ti.textureType != TextureImporterType.Sprite || ti.spriteBorder != border || ti.mipmapEnabled)
                {
                    ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.spriteBorder = border; ti.mipmapEnabled = false;
                    ti.alphaIsTransparency = true; ti.wrapMode = name == "IR_UI_Hazard" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; ti.SaveAndReimport();
                }
            }
        }

        static Material CctvMat()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CctvMaterial);
            var shader = Shader.Find(CctvShader); if (!shader) throw new Exception("Shader missing: " + CctvShader);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, CctvMaterial); }
            mat.shader = shader; mat.SetColor("_Phosphor", new Color(.55f, 1f, .65f)); mat.SetFloat("_Lines", 150); mat.SetFloat("_Noise", .07f); mat.SetFloat("_Gain", 3.5f);
            EditorUtility.SetDirty(mat); return mat;
        }

        static RectTransform R(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.sizeDelta = size; r.anchoredPosition = pos; return r;
        }
        static void Place(RectTransform r, Vector2 size, Vector2 pos) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.sizeDelta = size; r.anchoredPosition = pos; r.localScale = Vector3.one; r.localRotation = Quaternion.identity; }
        static Image Img(RectTransform r, Sprite s, Color c, Image.Type type = Image.Type.Simple) { var i = r.gameObject.GetComponent<Image>() ?? r.gameObject.AddComponent<Image>(); i.sprite = s; i.color = c; i.type = type; i.raycastTarget = false; return i; }
        static TextMeshProUGUI T(string name, Transform parent, string text, float size, Vector2 dim, Vector2 pos, Color color, TMP_FontAsset font, TextAlignmentOptions align = TextAlignmentOptions.Center)
        { var t = R(name, parent, dim, pos).gameObject.AddComponent<TextMeshProUGUI>(); if (font) t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false; t.characterSpacing = 4; return t; }

        static void Restyle(RequiemMenuView view, Material cctv)
        {
            var so = new SerializedObject(view);
            var options = so.FindProperty("options").objectReferenceValue as GameObject;
            var volume = so.FindProperty("volume").objectReferenceValue as Slider; var sens = so.FindProperty("sensitivity").objectReferenceValue as Slider;
            var full = so.FindProperty("fullscreen").objectReferenceValue as Toggle; var back = so.FindProperty("optionsBack").objectReferenceValue as Button;
            if (!options || !volume || !sens || !full || !back) { log.AppendLine("skip view " + view.name + " (incomplete)"); return; }
            var panel = (RectTransform)options.transform;
            // v5: the options console is wrapped in a full-screen overlay (UiUnifyAuthoring); restyle the content root
            var wrapped = panel.Find("OptionsConsole") as RectTransform; if (wrapped) panel = wrapped;
            var old = panel.Find("IndustrialConsole"); if (old) Object.DestroyImmediate(old.gameObject);
            var font = options.GetComponentInChildren<TMP_Text>(true)?.font;
            // intro menu: the panel lives in the narrow left frame (600 px) -> larger scale, shifted right over the hangar;
            // pause / lab menu: the wide console frame
            bool narrow = panel.parent is RectTransform fr && fr.sizeDelta.x < 700;
            if (!wrapped)
            {
                Place(panel, new Vector2(1060, 520), narrow ? new Vector2(150, -120) : new Vector2(0, -62));
                panel.localScale = Vector3.one * (narrow ? .82f : 1f);
            }
            var shakeRow = panel.Find("ScreenShake"); var shake = shakeRow ? shakeRow.GetComponentInChildren<Slider>(true) : null;
            var console = R("IndustrialConsole", panel, Vector2.zero, Vector2.zero); console.anchorMin = Vector2.zero; console.anchorMax = Vector2.one; console.offsetMin = console.offsetMax = Vector2.zero; console.SetAsFirstSibling();
            Img(console, Spr("IR_UI_Plate"), new Color(.78f, .84f, .9f), Image.Type.Sliced);
            var hz = R("HazardStrip", console, new Vector2(980, 14), new Vector2(0, 236)); Img(hz, Spr("IR_UI_Hazard"), Color.white, Image.Type.Tiled);
            var heading = panel.Find("OptionsHeading") as RectTransform;
            if (heading) { Place(heading, new Vector2(620, 34), new Vector2(-190, 205)); var ht = heading.GetComponent<TMP_Text>(); ht.text = "CONSOLA DE PLANTA · C-04 / CONFIGURACIÓN"; ht.fontSize = 19; ht.alignment = TextAlignmentOptions.MidlineLeft; ht.color = Cyan; }
            // status lamps (decorative)
            var lamps = new[] { (new Color(.3f, 1f, .45f), "PWR"), (Orange, "SRV"), (Cyan, "NET") };
            for (int i = 0; i < lamps.Length; i++)
            {
                var l = R("Lamp" + lamps[i].Item2, console, new Vector2(22, 22), new Vector2(-470 + i * 60, 170)); Img(l, Spr("IR_UI_Lamp"), lamps[i].Item1);
                T("LampLabel", l, lamps[i].Item2, 11, new Vector2(40, 16), new Vector2(0, -20), Steel, font);
            }
            // faders: same Slider components, vertical
            Fader(volume, "VOLUMEN", new Vector2(-420, -18), font);
            Fader(sens, "SENSIBILIDAD", new Vector2(-300, -18), font);
            if (shake) Fader(shake, "TEMBLOR", new Vector2(-180, -18), font);
            // lever switch: same Toggle
            var fullRoot = (RectTransform)full.transform; Place(fullRoot, new Vector2(130, 260), new Vector2(-60, -18));
            var box = full.targetGraphic as Image; var check = full.graphic as Image;
            if (box) { Place(box.rectTransform, new Vector2(108, 144), new Vector2(0, 26)); box.sprite = Spr("IR_UI_LeverOff"); box.color = Color.white; box.raycastTarget = true; }
            if (check) { var cr = check.rectTransform; cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = cr.offsetMax = Vector2.zero; check.sprite = Spr("IR_UI_LeverOn"); check.color = Color.white; }
            var fl = fullRoot.Find("FullscreenLabel") as RectTransform;
            if (fl) { Place(fl, new Vector2(150, 50), new Vector2(0, -95)); var t = fl.GetComponent<TMP_Text>(); t.text = "PANTALLA\nCOMPLETA"; t.fontSize = 15; t.alignment = TextAlignmentOptions.Center; t.color = Steel; }
            Plate(console, new Vector2(-240, -18), new Vector2(500, 300), font, "CANAL A · AUDIO / SERVO / VIDEO");
            // lit back button
            var br = (RectTransform)back.transform; Place(br, new Vector2(330, 56), new Vector2(-240, -205));
            var lamp = br.Find("BackLamp"); if (!lamp) { var lr = R("BackLamp", br, new Vector2(20, 20), new Vector2(-140, 0)); Img(lr, Spr("IR_UI_Lamp"), Orange); }
            // CCTV monitor
            var mon = R("CctvMonitor", console, new Vector2(440, 300), new Vector2(255, 10));
            var screen = R("Screen", mon, new Vector2(372, 232), Vector2.zero).gameObject.AddComponent<RawImage>(); screen.material = cctv; screen.color = Color.white; screen.raycastTarget = false;
            var bezel = R("Bezel", mon, new Vector2(440, 300), Vector2.zero); Img(bezel, Spr("IR_UI_Bezel"), new Color(.85f, .9f, .95f), Image.Type.Sliced);
            var label = T("CamLabel", mon, "CAM 04 · HANGAR C-04", 13, new Vector2(240, 20), new Vector2(-55, 98), new Color(.6f, 1f, .7f), font, TextAlignmentOptions.MidlineLeft);
            var clock = T("Clock", mon, "00:00:00", 13, new Vector2(110, 20), new Vector2(118, 98), new Color(.6f, 1f, .7f), font, TextAlignmentOptions.MidlineRight);
            T("Rec", mon, "● REC", 12, new Vector2(80, 18), new Vector2(140, -98), new Color(1f, .3f, .25f), font, TextAlignmentOptions.MidlineRight);
            T("MonitorPlate", console, "SEGURIDAD · CIRCUITO CERRADO", 13, new Vector2(400, 20), new Vector2(255, -160), Steel, font);
            mon.gameObject.AddComponent<CctvMonitor>().Configure(screen, label, clock);
            // keyboard / gamepad navigation between the controls
            Selectable afterSens = shake ? shake : (Selectable)full;
            Nav(volume, null, sens, null, back); Nav(sens, volume, afterSens, null, back);
            if (shake) Nav(shake, sens, full, null, back);
            Nav(full, afterSens, null, null, back); Nav(back, null, null, full, null);
            var sl = so.FindProperty("volumeValue").objectReferenceValue as TMP_Text; var ss = so.FindProperty("sensitivityValue").objectReferenceValue as TMP_Text;
            log.AppendLine("restyled " + view.gameObject.name + (narrow ? " (intro frame)" : "") + (shake ? " +shake" : "") + (sl && ss ? "" : " (value labels missing)"));
        }

        static void Plate(RectTransform console, Vector2 pos, Vector2 size, TMP_FontAsset font, string text)
        {
            var p = R("ControlsPlate", console, size, pos); Img(p, Spr("IR_UI_Plate"), new Color(.55f, .6f, .66f, .55f), Image.Type.Sliced); p.SetSiblingIndex(1);
            T("PlateLabel", p, text, 12, new Vector2(size.x - 40, 18), new Vector2(0, size.y * .5f - 20), Orange, font);
        }

        static void Fader(Slider s, string caption, Vector2 pos, TMP_FontAsset font)
        {
            var track = (RectTransform)s.transform; var block = (RectTransform)track.parent;
            Place(block, new Vector2(120, 300), pos);
            Place(track, new Vector2(64, 210), new Vector2(0, 0));
            s.direction = Slider.Direction.BottomToTop;
            var bg = track.Find("Background") as RectTransform; if (bg) { Place(bg, new Vector2(60, 226), Vector2.zero); Img(bg, Spr("IR_UI_FaderSlot"), Color.white, Image.Type.Sliced); }
            var area = track.Find("FillArea") as RectTransform; if (area) Place(area, new Vector2(8, 180), Vector2.zero);
            // the Slider drives the anchors (stretched across the area's width); sizeDelta is the extra width
            if (s.fillRect) { s.fillRect.sizeDelta = Vector2.zero; var fi = s.fillRect.GetComponent<Image>(); if (fi) fi.color = new Color(.12f, .82f, .91f, .85f); }
            var handleArea = track.Find("HandleArea") as RectTransform; if (handleArea) Place(handleArea, new Vector2(64, 180), Vector2.zero);
            if (s.handleRect)
            {
                s.handleRect.sizeDelta = new Vector2(20, 44); // 64 (area) + 20 = 84 px wide cap
                var hi = s.handleRect.GetComponent<Image>(); if (hi) { hi.sprite = Spr("IR_UI_FaderCap"); hi.color = Color.white; hi.raycastTarget = true; }
            }
            var label = block.Find("Label") as RectTransform;
            if (label) { Place(label, new Vector2(124, 26), new Vector2(0, -132)); var t = label.GetComponent<TMP_Text>(); t.text = caption; t.fontSize = 14; t.color = new Color(.82f, .86f, .9f); }
            var value = block.Find("Value") as RectTransform;
            if (value)
            {
                Place(value, new Vector2(96, 30), new Vector2(0, 132));
                var t = value.GetComponent<TMP_Text>(); t.fontSize = 18; t.color = new Color(.5f, 1f, .65f);
                // LCD background lives beside the value text (sibling drawn first), so the text stays on top
                var lcd = block.Find("ValueLcd") as RectTransform;
                if (!lcd) { lcd = R("ValueLcd", block, Vector2.zero, Vector2.zero); Img(lcd, null, new Color(.02f, .06f, .04f, .95f)); }
                Place(lcd, new Vector2(100, 34), new Vector2(0, 132)); lcd.SetSiblingIndex(value.GetSiblingIndex());
            }
        }

        static void Nav(Selectable s, Selectable left, Selectable right, Selectable up, Selectable down)
        {
            var n = s.navigation; n.mode = Navigation.Mode.Explicit; n.selectOnLeft = left; n.selectOnRight = right; n.selectOnUp = up; n.selectOnDown = down; s.navigation = n;
        }

        // ------------------------------------------------------------------ intro menu CCTV feed
        static void BuildIntroFeed(Scene scene)
        {
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == "HangarCctv")) Object.DestroyImmediate(old);
            var main = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (!main) { log.AppendLine("WARN intro feed: main camera missing"); return; }
            var root = new GameObject("HangarCctv");
            var cam = AddFeed(root.transform, main.transform.position, main.transform.rotation, main.fieldOfView, main.backgroundColor);
            cam.cullingMask = main.cullingMask | (1 << CctvLayer); var camGo = cam.gameObject;
            main.cullingMask &= ~(1 << CctvLayer);
            // the Carroñero at the capsule panel, visible only on the security camera
            // the capsule sits at menu-local (1.75, 1.22, .6), at the right of the frame; the actor crouches at its left flank,
            // in the middle of the security camera picture
            var pod = FindDeep(scene, "Capsule_C04"); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WalkerCarronero);
            if (pod && prefab)
            {
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab); actor.name = "CctvCarronero"; actor.transform.SetParent(root.transform, true);
                var at = pod.position + new Vector3(-1.2f, 0, .3f); at.y = 0;
                var flatFront = Vector3.ProjectOnPlane(main.transform.position - at, Vector3.up).normalized;
                actor.transform.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.ProjectOnPlane(pod.position - at, Vector3.up)));
                PrefabUtility.UnpackPrefabInstance(actor, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                // a still actor: no bridge walking, no click hitbox (the menu patrol only looks for BridgeWalkers)
                foreach (var w in actor.GetComponentsInChildren<BridgeWalker>(true)) Object.DestroyImmediate(w);
                foreach (var c in actor.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var t in actor.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CctvLayer;
                var motion = actor.GetComponentInChildren<CarroneroMotion>(true);
                if (motion) actor.AddComponent<CctvActor>().Configure(motion); else log.AppendLine("WARN menu Carroñero has no CarroneroMotion");
                var key = new GameObject("CctvKey", typeof(Light)); key.transform.SetParent(root.transform, false); key.transform.position = at + Vector3.up * 2.2f + flatFront * .8f;
                var l = key.GetComponent<Light>(); l.type = LightType.Point; l.range = 4; l.intensity = 1.6f; l.color = new Color(1f, .7f, .45f); l.cullingMask = 1 << CctvLayer;
            }
            log.AppendLine("intro CCTV feed at " + camGo.transform.position + (pod ? " actor near " + pod.position : " (no capsule)"));
        }

        /// Security camera for the options monitors: disabled URP camera, no post/shadows/AA, rendered by CctvMonitor
        /// at low resolution and frame rate only while an options panel is open. Also used by the Room00 hangar build.
        public static Camera AddFeed(Transform parent, Vector3 position, Quaternion rotation, float fov, Color background)
        {
            var camGo = new GameObject("CctvFeed", typeof(Camera)); camGo.transform.SetParent(parent, false);
            camGo.transform.SetPositionAndRotation(position, rotation);
            var cam = camGo.GetComponent<Camera>(); cam.fieldOfView = fov; cam.nearClipPlane = .05f; cam.farClipPlane = 140; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = background;
            cam.enabled = false; cam.allowMSAA = false; cam.allowHDR = false; cam.cullingMask &= ~(1 << 5); // no UI layer
            var data = cam.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false; data.renderShadows = false; data.antialiasing = AntialiasingMode.None; data.requiresDepthTexture = false; data.requiresColorTexture = false;
            camGo.AddComponent<CctvFeed>();
            return cam;
        }

        static Transform FindDeep(Scene scene, string name)
        {
            foreach (var g in scene.GetRootGameObjects())
                foreach (var t in g.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
