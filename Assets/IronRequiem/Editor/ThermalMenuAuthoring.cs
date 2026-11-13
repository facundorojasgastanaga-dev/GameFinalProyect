using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using TMPro;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
 [InitializeOnLoad] public static class ThermalMenuAuthoring
 {
  public const string Report="Tools/IronRequiem/Reports/ThermalMenu",Queue="Tools/IronRequiem/Requests/thermal-menu.txt",Lab="Assets/IronRequiem/Scenes/ThermalMenuLab.unity",IntroLab="Assets/IronRequiem/Scenes/ThermalIntroMenuLab.unity";
  static readonly DateTime Loaded=DateTime.UtcNow;
  static readonly Color Cyan=new Color(.12f,.82f,.91f),Dark=new Color(.025f,.055f,.075f,.98f);
  static TMP_FontAsset font;
  public static void UseFont(TMP_FontAsset asset)=>font=asset;
  static ThermalMenuAuthoring(){EditorApplication.update+=Tick;}
  static void Tick(){if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Queue)||File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/ThermalMenuAuthoring.cs")>Loaded)return;
   if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))return;
   string op=File.ReadAllText(Queue).Trim();File.Delete(Queue);try{Build(op=="apply");File.WriteAllText(Report+"/authoring.txt",op+" complete");IronRequiemSetupTools.WriteAudit();}catch(Exception e){File.WriteAllText(Report+"/error.txt",e.ToString());Debug.LogException(e);}}
  static T[] All<T>(Scene s)where T:Component=>s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
  static void Build(bool apply)
  {
   Directory.CreateDirectory(Report);
   var main=EditorSceneManager.OpenScene("Assets/FPS/Scenes/MainScene.unity",OpenSceneMode.Additive);
   AudioMixer[] mixers=All<AudioManager>(main).First().AudioMixers;
   try
   {
    font=All<TMP_Text>(main).First(t=>t.font).font;
    var inventory=All<PlayerWeaponsManager>(main).Single();
    for(int i=0;i<inventory.StartingWeapons.Count;i++)inventory.StartingWeapons[i]=HeatVariant(inventory.StartingWeapons[i]);
    PrefabUtility.RecordPrefabInstancePropertyModifications(inventory);EditorUtility.SetDirty(inventory);
    var pause=All<InGameMenuManager>(main).Single();pause.UseIndustrialMenu=true;EditorUtility.SetDirty(pause);
    BuildMenu(pause.MenuRoot.GetComponent<Canvas>(),pause,null,apply?"IntroMenu":"ThermalIntroMenuLab");
    var hint=All<TMP_Text>(main).FirstOrDefault(t=>t.transform.parent.name=="TABImage");if(hint)hint.text="Esc";
    AddHeatHud(main,inventory);
    // Lab is a copy; MainScene remains untouched until successful verification and explicit apply operation.
    File.WriteAllText(Report+"/connections.txt","Scene "+(apply?main.path:Lab)+"\n"+string.Join("\n",inventory.StartingWeapons.Select(w=>AssetDatabase.GetAssetPath(w)+" heat="+(w.GetComponent<WeaponHeat>()!=null)+" ammo="+w.MaxAmmo+" cadence="+w.DelayBetweenShots))+"\nMissing scripts="+main.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
    Record(main);
    if(apply)EditorSceneManager.SaveScene(main);else EditorSceneManager.SaveScene(main,Lab,true);
   }finally{EditorSceneManager.CloseScene(main,true);}
   var intro=EditorSceneManager.OpenScene("Assets/FPS/Scenes/IntroMenu.unity",OpenSceneMode.Additive);
   try
   {
    var canvas=All<Canvas>(intro).First();var start=All<LoadSceneButton>(intro).First();
    BuildMenu(canvas,null,start,apply?"MainScene":"ThermalMenuLab");
    var audio=All<AudioManager>(intro).FirstOrDefault();if(!audio){var go=new GameObject("MenuAudioSettings",typeof(AudioManager));SceneManager.MoveGameObjectToScene(go,intro);audio=go.GetComponent<AudioManager>();}audio.AudioMixers=mixers;
    var master=mixers.SelectMany(m=>m.FindMatchingGroups("Master")).FirstOrDefault();
    foreach(var source in All<AudioSource>(intro))if(!source.outputAudioMixerGroup)source.outputAudioMixerGroup=master;
    Record(intro);
    if(apply)EditorSceneManager.SaveScene(intro);else EditorSceneManager.SaveScene(intro,IntroLab,true);
   }finally{EditorSceneManager.CloseScene(intro,true);}
   if(!apply){var entries=EditorBuildSettings.scenes.ToList();foreach(var p in new[]{Lab,IntroLab})if(!entries.Any(s=>s.path==p))entries.Add(new EditorBuildSettingsScene(p,true));EditorBuildSettings.scenes=entries.ToArray();}
   AssetDatabase.SaveAssets();
  }
  static void Record(Scene scene)
  {foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true)){if(PrefabUtility.IsPartOfPrefabInstance(t.gameObject))PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);foreach(var c in t.GetComponents<Component>())if(c&&PrefabUtility.IsPartOfPrefabInstance(c)){PrefabUtility.RecordPrefabInstancePropertyModifications(c);EditorUtility.SetDirty(c);}}}
  public static WeaponController HeatVariant(WeaponController source)
  {
   if(source.GetComponent<WeaponHeat>())return source;
   string path="Assets/IronRequiem/Prefabs/Weapons/"+source.name+"_Thermal.prefab";
   var existing=AssetDatabase.LoadAssetAtPath<WeaponController>(path);if(existing)return existing;
   var instance=(GameObject)PrefabUtility.InstantiatePrefab(source.gameObject);
   try{instance.name=source.name+"_Thermal";instance.AddComponent<WeaponHeat>();var result=PrefabUtility.SaveAsPrefabAsset(instance,path);return result.GetComponent<WeaponController>();}finally{Object.DestroyImmediate(instance);}
  }
  public static void AddHeatHud(Scene scene,PlayerWeaponsManager inventory)
  {
   var hud=All<Canvas>(scene).First(c=>c.name=="HUD");if(hud.transform.Find("WeaponHeatHUD"))return;
   // Existing HUD uses a 960-wide reference. Keep its geometry and existing ammo layout intact.
   var root=Rect("WeaponHeatHUD",hud.transform,new Vector2(195,125),new Vector2(-12,70),new Vector2(1,0),new Vector2(1,0));
   var content=Rect("ThermalContent",root,Vector2.zero,Vector2.zero);Stretch(content);
   var graphic=Rect("CurvedThermalChannel",content,new Vector2(150,210),Vector2.zero,new Vector2(0,0),new Vector2(0,0)).gameObject.AddComponent<ThermalGaugeGraphic>();graphic.raycastTarget=false;graphic.transform.localScale=Vector3.one*.55f;
   var title=Text("THERMAL",content,"TÉRMICO",11,new Vector2(120,18),new Vector2(35,36));title.color=Cyan;
   var percent=Text("HeatPercent",content,"0%",26,new Vector2(115,35),new Vector2(35,5));
   var state=Text("HeatState",content,"FRÍA / ESTABLE",10,new Vector2(123,30),new Vector2(35,-25));
   var binding=root.gameObject.AddComponent<WeaponHeatHud>();binding.Configure(inventory,content.gameObject,graphic,percent,state);
  }
  public static void BuildMenu(Canvas canvas,InGameMenuManager pause,LoadSceneButton existing,string target)
  {
   if(canvas.transform.Find("IndustrialShell"))return;
   var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
   if(pause){canvas.overrideSorting=true;canvas.sortingOrder=100;var cg=canvas.GetComponent<CanvasGroup>();if(cg){cg.alpha=1;cg.interactable=true;cg.blocksRaycasts=true;}}
   // Preserve template children and references. Reuse StartButton + LoadSceneButton + MenuNavigation.
   foreach(Transform child in canvas.transform)child.gameObject.SetActive(false);
   var shell=Rect("IndustrialShell",canvas.transform,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);Stretch(shell);
   var bg=shell.gameObject.AddComponent<UnityEngine.UI.Image>();bg.color=pause?new Color(.005f,.015f,.025f,.86f):new Color(.008f,.019f,.028f,1);
   for(int i=0;i<9;i++){var line=Rect("HullSeam"+i,shell,new Vector2(3,1800),new Vector2(-1100+i*310,0));line.localRotation=Quaternion.Euler(0,0,-25);var image=line.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.1f,.24f,.29f,.11f);image.raycastTarget=false;}
   var frame=Rect("ConsoleFrame",shell,new Vector2(1160,810),Vector2.zero);Panel(frame,new Color(.025f,.045f,.055f,.98f),new Color(.16f,.28f,.31f),40);
   Text("SectorLabel",frame,"// SISTEMA OPERATIVO DE COMBATE",17,new Vector2(980,30),new Vector2(0,344)).color=Cyan;
   Text("Title",frame,pause?"EN PAUSA":"IRON REQUIEM",pause?64:76,new Vector2(1050,100),new Vector2(0,255));
   Text("Subtitle",frame,pause?"SISTEMAS EN ESPERA":"HIERRO. RUIDO. SUPERVIVENCIA.",17,new Vector2(900,35),new Vector2(0,187)).color=new Color(.56f,.68f,.7f);
   Text("Footer",frame,"IRON REQUIEM  /  INTERFAZ V1",14,new Vector2(900,30),new Vector2(0,-354)).color=new Color(.4f,.57f,.6f);
   var home=Rect("Home",frame,new Vector2(940,470),new Vector2(0,-66));
   UnityEngine.UI.Button play;
   LoadSceneButton loader;
   if(existing){play=existing.GetComponent<UnityEngine.UI.Button>();play.transform.SetParent(home,false);play.gameObject.SetActive(true);Place((RectTransform)play.transform,new Vector2(650,68),new Vector2(0,112));foreach(var text in play.GetComponentsInChildren<TMP_Text>()){text.text="JUGAR";text.color=new Color(.88f,.94f,.94f);text.fontSize=25;}StyleButton(play);loader=existing;}
   else{play=Button("Continue",home,"CONTINUAR",112);loader=play.gameObject.AddComponent<LoadSceneButton>();}
   loader.SceneName=target;loader.enabled=false;play.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();
   var opts=Button("Options",home,"OPCIONES",24);
   var creds=pause?null:Button("Credits",home,"CRÉDITOS",-64);
   var quit=pause?null:Button("Quit",home,"SALIR",-152);
   var menu=pause?Button("ReturnMenu",home,"VOLVER AL MENÚ PRINCIPAL",-64):null;
   var options=Rect("OptionsPanel",frame,new Vector2(940,460),new Vector2(0,-70));
   Text("OptionsHeading",options,"CONFIGURACIÓN",22,new Vector2(850,40),new Vector2(0,146)).color=Cyan;
   var volume=Slider(options,"MasterVolume","VOLUMEN GENERAL",65,0,1,out var volumeValue);
   var sens=Slider(options,"LookSensitivity","SENSIBILIDAD",-25,.1f,5,out var sensValue);
   var fullRoot=Rect("Fullscreen",options,new Vector2(650,50),new Vector2(0,-112));
   var full=fullRoot.gameObject.AddComponent<UnityEngine.UI.Toggle>();var fullBg=Rect("Box",fullRoot,new Vector2(34,34),new Vector2(-300,0)).gameObject.AddComponent<UnityEngine.UI.Image>();fullBg.color=new Color(.15f,.27f,.3f);
   var check=Rect("Check",fullBg.transform,new Vector2(22,22),Vector2.zero).gameObject.AddComponent<UnityEngine.UI.Image>();check.color=Cyan;full.graphic=check;full.targetGraphic=fullBg;
   Text("FullscreenLabel",fullRoot,"PANTALLA COMPLETA",21,new Vector2(550,45),new Vector2(15,0));
   var optionsBack=Button("OptionsBack",options,"VOLVER",-205);
   var credits=Rect("CreditsPanel",frame,new Vector2(940,450),new Vector2(0,-65));
   Text("CreditsTitle",credits,"CRÉDITOS",25,new Vector2(850,50),new Vector2(0,100)).color=Cyan;
   var body=Text("CreditsText",credits,"Créditos pendientes de completar.",24,new Vector2(800,160),Vector2.zero);body.textWrappingMode=TextWrappingModes.Normal;
   var creditsBack=Button("CreditsBack",credits,"VOLVER",-175);
   if(pause){var pauseNav=pause.gameObject.GetComponent<MenuNavigation>()??pause.gameObject.AddComponent<MenuNavigation>();pauseNav.ManageCursorAtStart=false;pauseNav.DefaultSelection=play;}
   var view=(pause ? pause.gameObject : canvas.gameObject).AddComponent<RequiemMenuView>();
   // Intro Canvas has no owner parent in the template; attach to its own object when rooted.
   view.Configure(pause,home.gameObject,options.gameObject,credits.gameObject,play,opts,creds,quit,optionsBack,creditsBack,menu,volume,sens,full,loader,body,volumeValue,sensValue);
   options.gameObject.SetActive(false);credits.gameObject.SetActive(false);
   var nav=Object.FindObjectsByType<MenuNavigation>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(n=>n.gameObject.scene==canvas.gameObject.scene);if(nav)nav.DefaultSelection=play;
   canvas.gameObject.SetActive(!pause);
  }
  static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 pos,Vector2? anchor=null,Vector2? pivot=null)
  {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor??new Vector2(.5f,.5f);r.pivot=pivot??new Vector2(.5f,.5f);r.sizeDelta=size;r.anchoredPosition=pos;return r;}
  static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
  static void Place(RectTransform r,Vector2 size,Vector2 pos){r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.sizeDelta=size;r.anchoredPosition=pos;r.localScale=Vector3.one;}
  static TextMeshProUGUI Text(string name,Transform parent,string value,float size,Vector2 dimensions,Vector2 position)
  {var t=Rect(name,parent,dimensions,position).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=new Color(.88f,.94f,.94f);t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;}
  static void Panel(RectTransform r,Color fill,Color edge,float cut){var g=r.gameObject.AddComponent<IndustrialPanelGraphic>();g.Configure(fill,edge,cut);}
  static UnityEngine.UI.Button Button(string name,Transform parent,string label,float y)
  {var r=Rect(name,parent,new Vector2(650,68),new Vector2(0,y));var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();Panel(r,Dark,Cyan,18);b.targetGraphic=r.GetComponent<IndustrialPanelGraphic>();Text("Label",r,label,25,new Vector2(610,60),Vector2.zero);StyleButton(b);return b;}
  public static void StyleButton(UnityEngine.UI.Button b)
  {var image=b.GetComponent<UnityEngine.UI.Image>();if(image){image.enabled=false;var face=Rect("IndustrialButtonFace",b.transform,Vector2.zero,Vector2.zero);Stretch(face);face.SetAsFirstSibling();var graphic=face.gameObject.AddComponent<IndustrialPanelGraphic>();graphic.Configure(Dark,Cyan,18);b.targetGraphic=graphic;}var c=b.colors;c.normalColor=Color.white;c.highlightedColor=new Color(.35f,.9f,1);c.selectedColor=new Color(.32f,.95f,1);c.pressedColor=new Color(.12f,.47f,.6f);c.disabledColor=new Color(.2f,.25f,.27f);c.fadeDuration=.08f;b.colors=c;b.transition=UnityEngine.UI.Selectable.Transition.ColorTint;}
  static UnityEngine.UI.Slider Slider(Transform parent,string name,string label,float y,float min,float max,out TMP_Text value)
  {
   var r=Rect(name,parent,new Vector2(800,65),new Vector2(0,y));Text("Label",r,label,19,new Vector2(300,35),new Vector2(-245,0));
   var track=Rect("Track",r,new Vector2(340,38),new Vector2(70,0));var slider=track.gameObject.AddComponent<UnityEngine.UI.Slider>();slider.minValue=min;slider.maxValue=max;
   var bg=Rect("Background",track,new Vector2(340,10),Vector2.zero).gameObject.AddComponent<UnityEngine.UI.Image>();bg.color=new Color(.12f,.2f,.23f);
   var area=Rect("FillArea",track,new Vector2(320,10),Vector2.zero);var fill=Rect("Fill",area,Vector2.zero,Vector2.zero).gameObject.AddComponent<UnityEngine.UI.Image>();Stretch(fill.rectTransform);fill.color=Cyan;slider.fillRect=fill.rectTransform;
   var handleArea=Rect("HandleArea",track,new Vector2(320,32),Vector2.zero);var handle=Rect("Handle",handleArea,new Vector2(16,32),Vector2.zero).gameObject.AddComponent<UnityEngine.UI.Image>();handle.color=new Color(.65f,1,1);slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;
   value=Text("Value",r,"",20,new Vector2(110,40),new Vector2(335,0));return slider;
  }
 }
}
