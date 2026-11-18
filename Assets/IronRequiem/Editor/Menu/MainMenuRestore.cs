using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace IronRequiem.Editor
{
    public static class VisualOverhaulMainMenuRestore
    {
        public const string Scene="Assets/FPS/Scenes/IntroMenu.unity",Report="Tools/IronRequiem/Reports/MainMenuRestore";
        public static string Apply()
        {
            VisualOverhaulRepairApply.Guard();Directory.CreateDirectory(Report);var scene=EditorSceneManager.OpenScene(Scene);var log=new StringBuilder();
            log.AppendLine("playModeStartScene="+AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            var roots=scene.GetRootGameObjects();var launch=roots.SelectMany(r=>r.GetComponentsInChildren<HangarLaunchSequence>(true)).Single();launch.gameObject.SetActive(true);launch.enabled=true;
            log.AppendLine("Reused launch="+launch.name+" cameras="+roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Count());
            string themePath="Assets/IronRequiem/Art/Menu/UI/MainMenuTheme.asset";
            if(!File.Exists(themePath))AssetDatabase.CopyAsset("Assets/IronRequiem/Settings/UI/IR_UiTheme.asset",themePath);
            var theme=AssetDatabase.LoadAssetAtPath<IronRequiemUiTheme>(themePath);theme.PanelRim=new Color(.43f,.32f,.17f);theme.ButtonRestFill=new Color(.045f,.05f,.045f,.95f);theme.ButtonRestRim=new Color(.3f,.31f,.26f);theme.ButtonHotRim=new Color(1,.48f,.07f);theme.ButtonHotFill=new Color(.13f,.075f,.025f,.98f);theme.ButtonRestText=new Color(.88f,.89f,.81f);theme.ButtonHotText=new Color(1,.87f,.57f);EditorUtility.SetDirty(theme);
            foreach(var fx in roots.SelectMany(r=>r.GetComponentsInChildren<HangarButtonFx>(true)))fx.ConfigureTheme(theme,fx.name.IndexOf("back",StringComparison.OrdinalIgnoreCase)>=0);
            var plate=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/IronRequiem/Art/Menu/UI/IR_UI_Plate.png");var lamp=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/IronRequiem/Art/Menu/UI/IR_UI_Lamp.png");
            foreach(var view in roots.SelectMany(r=>r.GetComponentsInChildren<RequiemMenuView>(true)))
            {
                var data=new SerializedObject(view);log.AppendLine("View="+view.name);
                foreach(var field in new[]{"primary","optionsButton","creditsButton","quitButton","optionsBack","creditsBack"})
                {
                    var button=data.FindProperty(field).objectReferenceValue as Button;if(!button)continue;
                    var rect=(RectTransform)button.transform;var frame=rect.Find("MenuOnlyMetal") as RectTransform;
                    if(!frame){frame=new GameObject("MenuOnlyMetal",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<RectTransform>();frame.SetParent(rect,false);}
                    frame.SetAsLastSibling();frame.anchorMin=Vector2.zero;frame.anchorMax=Vector2.one;frame.offsetMin=Vector2.zero;frame.offsetMax=Vector2.zero;var image=frame.GetComponent<Image>();image.sprite=plate;image.type=Image.Type.Sliced;image.color=new Color(.7f,.72f,.65f,1);image.raycastTarget=false;button.targetGraphic=image;button.transition=Selectable.Transition.ColorTint;
                    foreach(var label in button.GetComponentsInChildren<TMP_Text>(true)){var top=label.transform;while(top.parent!=rect&&top.parent)top=top.parent;if(top!=rect)top.SetAsLastSibling();}
                    var colors=button.colors;colors.normalColor=new Color(.75f,.79f,.70f);colors.highlightedColor=new Color(1,.68f,.22f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.58f,.32f,.08f);colors.disabledColor=new Color(.22f,.23f,.21f,.6f);button.colors=colors;
                    // A small status lamp uses existing art and never intercepts pointer input.
                    var indicator=rect.Find("MenuOnlyLamp") as RectTransform;if(!indicator){indicator=new GameObject("MenuOnlyLamp",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<RectTransform>();indicator.SetParent(rect,false);}indicator.anchorMin=indicator.anchorMax=new Vector2(1,.5f);indicator.anchoredPosition=new Vector2(-20,0);indicator.sizeDelta=new Vector2(12,12);var glow=indicator.GetComponent<Image>();glow.sprite=lamp;glow.color=field=="primary"?new Color(.35f,.9f,.20f):new Color(1,.5f,.09f);glow.raycastTarget=false;
                    log.AppendLine(field+"="+button.name);
                }
                foreach(string field in new[]{"home","options","credits"}){var panel=data.FindProperty(field).objectReferenceValue as GameObject;if(!panel)continue;log.AppendLine(field+"="+panel.name);foreach(var image in panel.GetComponentsInChildren<Image>(true).Where(i=>i.sprite==plate))image.color=new Color(.7f,.72f,.65f,1);}
                var optionsPanel=(data.FindProperty("options").objectReferenceValue as GameObject).transform;var creditsPanel=(data.FindProperty("credits").objectReferenceValue as GameObject).transform;
                var branding=view.GetComponentsInChildren<TMP_Text>(true).Where(t=>!t.transform.IsChildOf(optionsPanel)&&!t.transform.IsChildOf(creditsPanel)&&!t.GetComponentInParent<Button>()&&(t.text.Contains("IRON")||t.text.Contains("HIERRO")||t.text.Contains("PROTOCOLO REQUIEM"))).Cast<Graphic>().ToArray();
                var panels=view.GetComponent<MainMenuPanelPresentation>()??view.gameObject.AddComponent<MainMenuPanelPresentation>();panels.Configure(data.FindProperty("home").objectReferenceValue as GameObject,branding);
                foreach(var label in view.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.text.Contains("CANAL"))){label.gameObject.SetActive(false);log.AppendLine("Hidden decorative caption="+label.name);}
            }
            foreach(var scaler in roots.SelectMany(r=>r.GetComponentsInChildren<CanvasScaler>(true)).Where(s=>s.GetComponent<Canvas>().isRootCanvas)){scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();File.WriteAllText(Report+"/applied.txt",log.ToString());return log.ToString();
        }
    }
}
