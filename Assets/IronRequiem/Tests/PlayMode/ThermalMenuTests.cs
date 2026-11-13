using System;
using System.IO;
using System.Linq;
using System.Collections;
using NUnit.Framework;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.AI;
using Unity.FPS.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Tests
{
 public sealed class ThermalMenuTests
 {
  const string Lab="Assets/IronRequiem/Scenes/ThermalMenuLab.unity",Intro="Assets/IronRequiem/Scenes/ThermalIntroMenuLab.unity",Reports="Tools/IronRequiem/Reports/ThermalMenu";
  Keyboard keyboard;Mouse mouse;bool background,fullscreen;float capture;UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
  string[] keys={RequiemSettings.VolumeKey,RequiemSettings.SensitivityKey,RequiemSettings.FullscreenKey};bool[] has;float[] saved;
  PlayerCharacterController player;PlayerWeaponsManager weapons;
  [UnitySetUp]public IEnumerator Setup()
  {
   background=Application.runInBackground;Application.runInBackground=true;capture=Time.captureDeltaTime;Time.captureDeltaTime=1f/60;Time.timeScale=1;fullscreen=Screen.fullScreen;
   has=keys.Select(PlayerPrefs.HasKey).ToArray();saved=new[]{PlayerPrefs.GetFloat(keys[0]),PlayerPrefs.GetFloat(keys[1]),(float)PlayerPrefs.GetInt(keys[2])};
   devices=InputSystem.actions.devices;keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();InputSystem.actions.devices=new InputDevice[]{keyboard,mouse};keyboard.MakeCurrent();mouse.MakeCurrent();
   EditorSceneManager.LoadSceneInPlayMode(Lab,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;PrepareGameplay();yield return new WaitForSeconds(.7f);
  }
  void PrepareGameplay()
  {
   player=Object.FindAnyObjectByType<PlayerCharacterController>();weapons=player.GetComponent<PlayerWeaponsManager>();player.GetComponent<Health>().Invincible=true;
   foreach(var e in Object.FindObjectsByType<EnemyController>())e.gameObject.SetActive(false);
   var flow=Object.FindAnyObjectByType<GameFlowManager>();if(flow)flow.enabled=false;var objective=Object.FindAnyObjectByType<ObjectiveManager>();if(objective)objective.enabled=false;
   Time.timeScale=1;AudioListener.pause=false;Cursor.lockState=CursorLockMode.Locked;
  }
  [UnityTearDown]public IEnumerator Cleanup()
  {
   InputSystem.actions.devices=devices;InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);Time.timeScale=1;AudioListener.pause=false;Time.captureDeltaTime=capture;Cursor.lockState=CursorLockMode.None;
   for(int i=0;i<keys.Length;i++){if(!has[i])PlayerPrefs.DeleteKey(keys[i]);else if(i==2)PlayerPrefs.SetInt(keys[i],(int)saved[i]);else PlayerPrefs.SetFloat(keys[i],saved[i]);}PlayerPrefs.Save();Screen.fullScreen=fullscreen;RequiemSettings.ApplyVolume();
   var scene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("ThermalCleanup"+Time.frameCount));yield return SceneManager.UnloadSceneAsync(scene);Application.runInBackground=background;
  }
  IEnumerator Press(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;}
  IEnumerator Shot(WeaponController w){yield return new WaitForSeconds(Mathf.Max(.02f,w.DelayBetweenShots+.01f));Assert.That(w.TryShootSingleWithCharge(1),Is.True);yield return null;}
  [UnityTest]public IEnumerator HeatAcceptedShotsLockAndRecovery()
  {
   var w=weapons.GetActiveWeapon();var h=w.GetComponent<WeaponHeat>();Assert.That(h,Is.Not.Null);Assert.That(h.MaximumHeat,Is.EqualTo(100));int count=0;w.OnShootProcessed+=()=>count++;
   int originalAmmo=w.GetCurrentAmmo();Assert.That(originalAmmo,Is.GreaterThanOrEqualTo(13));
   Capture("heat-cold",1920,1080);
   for(int i=0;i<4;i++)yield return Shot(w);Assert.That(h.CurrentHeat,Is.EqualTo(32).Within(.01));Capture("heat-32",1920,1080);Capture("heat-32-4x3",1440,1080);
   int remaining=w.GetCurrentAmmo();w.SetAmmo(0);float before=h.CurrentHeat;Assert.That(w.TryShootSingleWithCharge(1),Is.False);Assert.That(h.CurrentHeat,Is.EqualTo(before).Within(.01));w.SetAmmo(remaining);
   for(int i=0;i<5;i++)yield return Shot(w);Assert.That(h.CurrentHeat,Is.EqualTo(72).Within(.01));Capture("heat-72",1920,1080);
   for(int i=0;i<4;i++)yield return Shot(w);Assert.That(count,Is.EqualTo(13));Assert.That(h.IsOverheated,Is.True);Assert.That(h.CurrentHeat,Is.EqualTo(100));Capture("heat-locked",1920,1080);
   int ammo=w.GetCurrentAmmo();yield return new WaitForSeconds(.2f);Assert.That(w.TryShootSingleWithCharge(1),Is.False);Assert.That(w.GetCurrentAmmo(),Is.EqualTo(ammo));
   yield return new WaitForSeconds(1.5f);Assert.That(h.CurrentHeat,Is.InRange(60,85));Assert.That(h.IsOverheated,Is.True,"Latch stays blocked below100");
   yield return new WaitForSeconds(2.3f);Assert.That(h.CurrentHeat,Is.LessThanOrEqualTo(35));Assert.That(h.CanFire,Is.True);yield return Shot(w);
  }
  [UnityTest]public IEnumerator NativeChargePelletsAndAutomaticInput()
  {
   var w=weapons.GetActiveWeapon();var heat=w.GetComponent<WeaponHeat>();w.MaxAmmo=1000;w.SetAmmo(1000);w.ShootType=WeaponShootType.Charge;w.BulletsPerShot=6;w.AutomaticReleaseOnCharged=false;w.MaxChargeDuration=.2f;w.DelayBetweenShots=.01f;
   int shots=0;w.OnShootProcessed+=()=>shots++;Assert.That(w.HandleShootInputs(true,true,false),Is.False);yield return new WaitForSeconds(.1f);Assert.That(heat.CurrentHeat,Is.Zero);
   Assert.That(w.HandleShootInputs(false,false,true),Is.True);Assert.That(shots,Is.EqualTo(1));Assert.That(heat.CurrentHeat,Is.EqualTo(8).Within(.01));
   w.ShootType=WeaponShootType.Automatic;w.BulletsPerShot=4;
   InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSeconds(.13f);InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
   Assert.That(shots,Is.GreaterThan(2));Assert.That(heat.CurrentHeat,Is.EqualTo(Mathf.Min(100,shots*8)).Within(.1),"Accepted shots, not pellets or held frames");
  }
  [UnityTest]public IEnumerator IndependentHeatAndHudAcrossSwitches()
  {
   var first=weapons.GetActiveWeapon();first.MaxAmmo=1000;first.SetAmmo(1000);first.DelayBetweenShots=.01f;
   for(int i=0;i<13;i++)yield return Shot(first);var originalHeat=first.GetComponent<WeaponHeat>();Assert.That(originalHeat.IsOverheated,Is.True);
   // A second inventory instance of the same thermal policy exercises native switching without editing inventory assets.
   var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponController>("Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_Pistol_Thermal.prefab"));source.ShowWeapon(false);
   Assert.That(weapons.AddWeapon(source),Is.True);weapons.SwitchToWeaponIndex(1);yield return new WaitForSeconds(.55f);
   var second=weapons.GetActiveWeapon();Assert.That(second,Is.Not.SameAs(first));Assert.That(second.GetComponent<WeaponHeat>().CurrentHeat,Is.Zero);var hud=Object.FindAnyObjectByType<WeaponHeatHud>();Assert.That(hud.BoundHeat,Is.SameAs(second.GetComponent<WeaponHeat>()));
   weapons.SwitchToWeaponIndex(0);yield return new WaitForSeconds(.55f);Assert.That(weapons.GetActiveWeapon(),Is.SameAs(first));Assert.That(hud.BoundHeat,Is.SameAs(originalHeat));Assert.That(originalHeat.IsOverheated,Is.True);Assert.That(first.TryShootSingleWithCharge(1),Is.False);
   yield return new WaitForSeconds(3);Assert.That(originalHeat.CanFire,Is.True);Object.Destroy(source.gameObject);
  }
  [UnityTest]public IEnumerator EscapePauseInputAndSettings()
  {
   var pause=Object.FindAnyObjectByType<InGameMenuManager>();var view=pause.GetComponent<RequiemMenuView>();Assert.That(pause.UseIndustrialMenu,Is.True);Assert.That(pause.IsPaused,Is.False);
   Vector3 start=player.transform.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSeconds(.15f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;Assert.That(Vector3.Distance(start,player.transform.position),Is.GreaterThan(.1f));
   Vector3 look=player.PlayerCamera.transform.forward;InputSystem.QueueDeltaStateEvent(mouse.delta,new Vector2(12,3));yield return null;yield return null;Assert.That(Vector3.Angle(look,player.PlayerCamera.transform.forward),Is.GreaterThan(.01f));
   yield return Press(Key.Escape);Assert.That(pause.IsPaused,Is.True);Assert.That(Time.timeScale,Is.Zero);Assert.That(player.GetComponent<PlayerInputHandler>().CanProcessInput(),Is.False);Capture("pause-1080",1920,1080);
   view.OpenOptions();view.ChangeVolume(.43f);view.ChangeSensitivity(1.7f);Assert.That(player.GetComponent<PlayerInputHandler>().LookSensitivity,Is.EqualTo(1.7f));Assert.That(PlayerPrefs.GetFloat(RequiemSettings.VolumeKey),Is.EqualTo(.43f));Capture("pause-options-4x3",1440,1080);
   yield return Press(Key.Escape);Assert.That(pause.IsPaused,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(player.GetComponent<PlayerInputHandler>().CanProcessInput(),Is.True);Assert.That(AudioUtility.GetMasterVolume(),Is.EqualTo(.43f).Within(.01));
  }
  [UnityTest]public IEnumerator IntroNavigationPersistenceAndPlayFlow()
  {
   RequiemSettings.SetVolume(.37f);RequiemSettings.SetSensitivity(1.35f);
   bool integrated=File.ReadAllText(Reports+"/authoring.txt").StartsWith("apply");
   EditorSceneManager.LoadSceneInPlayMode(integrated?"Assets/FPS/Scenes/IntroMenu.unity":Intro,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
   var view=Object.FindAnyObjectByType<RequiemMenuView>();Assert.That(view,Is.Not.Null);Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(AudioListener.pause,Is.False);Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));Capture("menu-main-1080",1920,1080);
   view.OpenOptions();UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);yield return Press(Key.DownArrow);Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.activeInHierarchy,Is.True);Assert.That(view.IsOptionsOpen,Is.True);Assert.That(AudioUtility.GetMasterVolume(),Is.EqualTo(.37f).Within(.01));Capture("menu-options-4x3",1440,1080);var full=view.GetComponentsInChildren<UnityEngine.UI.Toggle>(true).Single();full.isOn=!full.isOn;Assert.That(PlayerPrefs.GetInt(RequiemSettings.FullscreenKey),Is.EqualTo(full.isOn?1:0));view.ShowHome();view.OpenCredits();Capture("menu-credits",1920,1080);view.ShowHome();
   var nav=Object.FindAnyObjectByType<MenuNavigation>();Assert.That(nav.DefaultSelection,Is.Not.Null);Assert.That(nav.DefaultSelection.gameObject.activeInHierarchy,Is.True);
   view.Quit();Assert.That(Application.isPlaying,Is.True,"SALIR remains safe in Editor");
   yield return Press(Key.Enter);yield return null;yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo(integrated?"CombatTwoFloorsLab":"ThermalMenuLab"));if(integrated){var lab=Object.FindAnyObjectByType<CombatLabMenu>();Assert.That(lab,Is.Not.Null);Assert.That(lab.IsPlaying,Is.True,"JUGAR from IntroMenu starts the lab directly");Assert.That(lab.IsMainMenu,Is.False,"no second menu");}PrepareGameplay();yield return new WaitForSeconds(.6f);Assert.That(player.GetComponent<PlayerInputHandler>().LookSensitivity,Is.EqualTo(1.35f));
   var pause=Object.FindAnyObjectByType<InGameMenuManager>();pause.SetPauseMenuActivation(true);pause.GetComponent<RequiemMenuView>().ReturnToMenu();yield return null;yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo(integrated?"IntroMenu":"ThermalIntroMenuLab"));Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(AudioListener.pause,Is.False);
  }
  // 08/11/2026: opened directly (not from IntroMenu > JUGAR) the lab keeps its own start menu and its own "volver al menu".
  [UnityTest]public IEnumerator TwoFloorsLabOpenedDirectlyKeepsItsMenu()
  {
   EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/CombatTwoFloorsLab.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;yield return null;
   var lab=Object.FindAnyObjectByType<CombatLabMenu>();Assert.That(lab,Is.Not.Null);Assert.That(CombatLabMenu.LaunchedFromMainMenu,Is.False);Assert.That(lab.IsMainMenu,Is.True);Assert.That(lab.IsPlaying,Is.False);
   lab.BeginGame();yield return null;Assert.That(lab.IsPlaying,Is.True);
   lab.ReturnToMainMenu();yield return null;yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("CombatTwoFloorsLab"));lab=Object.FindAnyObjectByType<CombatLabMenu>();Assert.That(lab.IsMainMenu,Is.True);
  }
  static void Capture(string name,int width,int height)
  {
   var camera=Camera.main;bool created=!camera;if(created){camera=new GameObject("UICaptureCamera").AddComponent<Camera>();camera.backgroundColor=new Color(.008f,.019f,.028f);camera.clearFlags=CameraClearFlags.SolidColor;}
   var canvases=Object.FindObjectsByType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();var targets=canvases.Select(c=>c.worldCamera).ToArray();var distances=canvases.Select(c=>c.planeDistance).ToArray();
   var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var rt=new RenderTexture(width,height,24);var tex=new Texture2D(width,height,TextureFormat.RGB24,false);int mask=camera.cullingMask;camera.cullingMask=~0;
   try{camera.targetTexture=rt;foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.2f;}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(Reports+"/"+name+".png",tex.EncodeToPNG());}
   finally{for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=targets[i];canvases[i].planeDistance=distances[i];}camera.cullingMask=mask;camera.targetTexture=oldTarget;RenderTexture.active=oldActive;Object.Destroy(rt);Object.Destroy(tex);if(created)Object.Destroy(camera.gameObject);}
  }
 }
}
