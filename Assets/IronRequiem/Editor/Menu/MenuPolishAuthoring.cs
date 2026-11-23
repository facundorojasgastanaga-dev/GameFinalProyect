using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools
{
    public static class MenuPolishAuthoring
    {
        public const string Source = "Assets/FPS/Scenes/IntroMenu.unity";
        public const string Lab = "Assets/IronRequiem/Scenes/IntroMenuPolishLab.unity";
        const string Report = "Tools/IronRequiem/Reports/MenuPolish";

        static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserving user's dirty scene.");
        }

        [MenuItem("Tools/Iron Requiem/Menu Polish/Inspect and copy")]
        public static void Inspect()
        {
            Guard(); Directory.CreateDirectory(Report);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                if (!File.Exists(Lab) && !AssetDatabase.CopyAsset(Source, Lab)) throw new IOException("Could not copy menu.");
                var scene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Additive);
                var text = new StringBuilder();
                foreach (var root in scene.GetRootGameObjects()) Dump(root.transform, 0, text);
                File.WriteAllText(Report + "/before-hierarchy.txt", text.ToString());
                EditorSceneManager.CloseScene(scene, true);
            }
            finally { Restore(setup); }
        }

        [MenuItem("Tools/Iron Requiem/Menu Polish/Style lab")]
        public static void StyleLab() => Style(Lab);

        [MenuItem("Tools/Iron Requiem/Menu Polish/Apply verified style")]
        public static void Apply() => Style(Source);

        static T Field<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object =>
            new SerializedObject(owner).FindProperty(name).objectReferenceValue as T;

        static readonly Color Ivory = new Color(.91f,.89f,.80f), Orange = new Color(1,.40f,.08f), Cyan = new Color(.13f,.84f,.92f);

        static void Style(string path)
        {
            Guard(); Directory.CreateDirectory(Report);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RequiemMenuView>(true)).Single();
                var home = Field<GameObject>(view,"home");
                var frame = (RectTransform)home.transform.parent;
                var theme = AssetDatabase.LoadAssetAtPath<IronRequiemUiTheme>("Assets/IronRequiem/Art/Menu/UI/MainMenuTheme.asset");
                // Read the installed protagonist palette without changing materials or the shared UI theme.
                var palette = new StringBuilder("Menu-only palette: graphite 080A0C, ivory E8E3CC, orange FF6614, cyan 21D6EB.\n");
                foreach (var material in AssetDatabase.FindAssets("HG_Protagonist t:Material").Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))))
                    if (material.HasProperty("_BaseColor")) palette.AppendLine(material.name + " " + material.GetColor("_BaseColor"));
                File.WriteAllText(Report+"/palette.txt",palette.ToString());

                frame.anchorMin = frame.anchorMax = new Vector2(0,.5f); frame.pivot = new Vector2(0,.5f);
                frame.anchoredPosition = new Vector2(72,-12); frame.sizeDelta = new Vector2(600,800);
                var panel = frame.GetComponent<IndustrialPanelGraphic>();
                if (!panel) panel = frame.gameObject.AddComponent<IndustrialPanelGraphic>();
                panel.SetStyle(IndustrialPanelGraphic.PanelStyle.Label);
                panel.Configure(new Color(.018f,.025f,.030f,.89f),new Color(.20f,.26f,.27f,.8f),8); panel.raycastTarget = false;

                var title = frame.Find("Title").GetComponent<TMP_Text>();
                title.text = "<size=82%>IRON</size>\nREQUIEM";
                Text(title,theme.TitleFont,94,Ivory,TextAlignmentOptions.Left);
                title.characterSpacing=1; title.lineSpacing=-10;
                Rect(title.rectTransform, new Vector2(0,240),new Vector2(520,220));
                var sector=frame.Find("SectorLabel").GetComponent<TMP_Text>();
                Text(sector,theme.BodyFont,15,Cyan,TextAlignmentOptions.Left);
                sector.text="UNIDAD C-04  /  PROTOCOLO DE ACTIVACIÓN";sector.characterSpacing=3;
                Rect(sector.rectTransform,new Vector2(0,365),new Vector2(520,26));
                var subtitle=frame.Find("Subtitle").GetComponent<TMP_Text>();
                Text(subtitle,theme.BodyFont,16,new Color(.62f,.67f,.66f),TextAlignmentOptions.Left);
                subtitle.text="HIERRO. RUIDO. SUPERVIVENCIA.";subtitle.characterSpacing=2;
                Rect(subtitle.rectTransform,new Vector2(0,108),new Vector2(520,28));
                var footer=frame.Find("Footer").GetComponent<TMP_Text>();
                Text(footer,theme.BodyFont,13,new Color(.57f,.63f,.63f),TextAlignmentOptions.Left);
                footer.text="[↑ ↓] NAVEGAR     [ENTER / A] CONFIRMAR";footer.characterSpacing=1;
                Rect(footer.rectTransform,new Vector2(0,-366),new Vector2(520,28));

                var divider=frame.Find("HG_Divider").GetComponent<UnityEngine.UI.Graphic>();
                Rect(divider.rectTransform,new Vector2(0,75),new Vector2(520,1));divider.color=new Color(.19f,.29f,.30f,.9f);divider.raycastTarget=false;
                var leading=frame.Find("HG_DividerAccent").GetComponent<UnityEngine.UI.Graphic>();
                Rect(leading.rectTransform,new Vector2(-206,75),new Vector2(108,3));leading.color=Orange;leading.raycastTarget=false;
                Rect((RectTransform)home.transform,new Vector2(0,-136),new Vector2(520,410));

                var fields=new[]{"primary","optionsButton","creditsButton","quitButton"};
                var buttons=fields.Select(f=>Field<UnityEngine.UI.Button>(view,f)).Where(b=>b).ToArray();
                var positions=new[]{117f,10f,-72f,-154f};
                for(int i=0;i<buttons.Length;i++)
                {
                    var button=buttons[i]; Rect((RectTransform)button.transform,new Vector2(0,positions[i]),new Vector2(520,i==0?92:66));
                    PolishButton(button,theme,i==0,false,i+1);
                    var navigation=button.navigation;navigation.mode=UnityEngine.UI.Navigation.Mode.Explicit;
                    navigation.selectOnUp=buttons[(i+buttons.Length-1)%buttons.Length];navigation.selectOnDown=buttons[(i+1)%buttons.Length];
                    navigation.selectOnLeft=navigation.selectOnRight=null;button.navigation=navigation;
                }
                foreach(var field in new[]{"optionsBack","creditsBack"})
                {var button=Field<UnityEngine.UI.Button>(view,field);if(button)PolishButton(button,theme,false,true,0);}

                var options=Field<GameObject>(view,"options");var credits=Field<GameObject>(view,"credits");
                // Preserve controls, their bindings and the fitted options console; harmonize its existing heading.
                foreach(var text in options.GetComponentsInChildren<TMP_Text>(true))
                    if(text.name=="OptionsHeading"){text.color=Ivory;text.font=theme.TitleFont;text.characterSpacing=1;}
                var controller=view.GetComponent<MainMenuPolish>()??view.gameObject.AddComponent<MainMenuPolish>();
                var group=frame.GetComponent<CanvasGroup>()??frame.gameObject.AddComponent<CanvasGroup>();group.alpha=1;
                var sequence=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<HangarLaunchSequence>(true)).Single();
                controller.Configure(view,home,buttons[0],Field<UnityEngine.UI.Button>(view,"optionsButton"),Field<UnityEngine.UI.Button>(view,"creditsButton"),
                    Field<UnityEngine.UI.Slider>(view,"volume"),Field<UnityEngine.UI.Button>(view,"creditsBack"),options,credits,sequence);
                while(controller.ReturnHome.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(controller.ReturnHome,0);
                UnityEventTools.AddPersistentListener(controller.ReturnHome,view.ShowHome);
                var presentation=view.GetComponent<MainMenuPanelPresentation>();
                if(presentation)presentation.Configure(home,frame.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)
                    .Where(g=>!g.transform.IsChildOf(home.transform)&&!g.transform.IsChildOf(credits.transform)&&!g.transform.IsChildOf(options.transform)).ToArray());
                // Decorative graphics never steal a click from the background robot interaction.
                foreach(var graphic in frame.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                {
                    var selectable = graphic.GetComponentInParent<UnityEngine.UI.Selectable>(true);
                    if (selectable && selectable.targetGraphic == graphic) continue;
                    if(!graphic.GetComponent<UnityEngine.UI.Selectable>() && !graphic.GetComponentInParent<UnityEngine.UI.Slider>() &&
                        !graphic.GetComponentInParent<UnityEngine.UI.Toggle>())graphic.raycastTarget=false;
                }
                EditorSceneManager.SaveScene(scene);
                File.WriteAllText(Report+(path==Lab?"/styled-lab.txt":"/applied.txt"),"Styled existing menu only: "+path+"\nPreserved scene 3D objects, launch sequence, settings bindings and destination.\n");
            }
            finally { Restore(setup); }
        }

        static void Restore(SceneSetup[] setup)
        {
            if (setup.Length > 0 && setup.Any(s => s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static void Text(TMP_Text text,TMP_FontAsset font,float size,Color color,TextAlignmentOptions alignment)
        {text.font=font;text.enableAutoSizing=false;text.fontSize=size;text.margin=Vector4.zero;text.color=color;text.alignment=alignment;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Overflow;text.raycastTarget=false;}
        static void Rect(RectTransform rect,Vector2 position,Vector2 size)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one;}

        static void PolishButton(UnityEngine.UI.Button button,IronRequiemUiTheme theme,bool primary,bool back,int number)
        {
            var old=button.GetComponent<HangarButtonFx>();if(old)old.enabled=false;
            button.transition=UnityEngine.UI.Selectable.Transition.None;
            var face=button.GetComponentInChildren<IndustrialPanelGraphic>(true);
            if(!face)throw new InvalidOperationException("Existing button plate missing: "+button.name);
            face.SetStyle(IndustrialPanelGraphic.PanelStyle.Label);
            face.raycastTarget=true;button.targetGraphic=face;
            foreach(Transform child in button.transform)
                if(child.name=="MenuOnlyMetal"||child.name=="MenuOnlyLamp"||child.name=="HG_Glow"||child.name=="HG_Accent")child.gameObject.SetActive(false);
            var label=old?Field<TMP_Text>(old,"label"):null;
            if(!label)label=button.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name!="PolishIndex");
            Text(label,theme.TitleFont,primary?34:25,Ivory,TextAlignmentOptions.Left);label.characterSpacing=primary?5:3;
            var labelRect=label.rectTransform;labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;
            labelRect.offsetMin=new Vector2(number>0?72:20,4);labelRect.offsetMax=new Vector2(number>0?-48:-38,-4);
            var light=button.transform.Find("PolishIndicator") as RectTransform;
            if(!light){light=new GameObject("PolishIndicator",typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();light.SetParent(button.transform,false);}
            light.gameObject.layer=button.gameObject.layer;
            light.anchorMin=light.anchorMax=new Vector2(1,.5f);light.pivot=new Vector2(.5f,.5f);light.anchoredPosition=new Vector2(-23,0);light.sizeDelta=new Vector2(4,primary?28:18);
            var indicator=light.GetComponent<UnityEngine.UI.Image>();indicator.color=primary?Orange:Cyan;indicator.raycastTarget=false;
            if(number>0)
            {
                var index=button.transform.Find("PolishIndex") as RectTransform;
                if(!index){index=new GameObject("PolishIndex",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<RectTransform>();index.SetParent(button.transform,false);}
                index.gameObject.layer=button.gameObject.layer;
                index.anchorMin=index.anchorMax=new Vector2(0,.5f);index.pivot=new Vector2(.5f,.5f);index.anchoredPosition=new Vector2(34,0);index.sizeDelta=new Vector2(36,25);
                var text=index.GetComponent<TMP_Text>();Text(text,theme.BodyFont,14,new Color(.57f,.63f,.62f),TextAlignmentOptions.Center);text.text=number.ToString("00");
            }
            var fx=button.GetComponent<MenuPolishButtonFx>()??button.gameObject.AddComponent<MenuPolishButtonFx>();fx.Configure(face,label,indicator,theme,primary,back);
        }

        static void Dump(Transform t, int depth, StringBuilder text)
        {
            var rect = t as RectTransform;
            if (depth < 3 || rect)
            {
                text.Append(new string(' ', Math.Min(depth, 12) * 2)).Append(t.name).Append(t.gameObject.activeSelf ? "" : " [off]");
                text.Append(" [").Append(string.Join(",", t.GetComponents<Component>().Where(c => c).Select(c => c.GetType().Name))).Append(']');
                if (rect) text.Append($" anchors={rect.anchorMin}/{rect.anchorMax} pos={rect.anchoredPosition} size={rect.sizeDelta} scale={rect.localScale}");
                var label = t.GetComponent<TMP_Text>();
                if (label) text.Append($" text={label.text.Replace("\n", "|")} font={label.font.name} size={label.fontSize}");
                text.AppendLine();
            }
            foreach (Transform child in t) Dump(child, depth + 1, text);
        }
    }
}
