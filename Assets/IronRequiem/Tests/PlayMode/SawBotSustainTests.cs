using System.Collections;
using System;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Unity.FPS.UI;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Tests
{
 public sealed class SawBotSustainTests
 {
  const string Reports="Tools/IronRequiem/Reports/SawBotSustain";
  [UnityTest] public IEnumerator BaselineVisual()
  {
   Time.timeScale=1;EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/SawBotLab.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
   var menu=Object.FindAnyObjectByType<CombatLabMenu>();menu.BeginGame();menu.enabled=false;var waves=Object.FindAnyObjectByType<CombatTwoRoomsLab>();waves.enabled=false;if(waves.SpawnQueue)waves.SpawnQueue.enabled=false;Object.FindAnyObjectByType<GameFlowManager>().enabled=false;Object.FindAnyObjectByType<ObjectiveManager>().enabled=false;
   var player=Object.FindAnyObjectByType<PlayerCharacterController>();player.GetComponent<Health>().Invincible=true;player.enabled=false;foreach(var r in Object.FindObjectsByType<Renderer>())r.enabled=false;
   var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Enemies/SawBot_AI_v01.prefab"),Vector3.zero,Quaternion.identity);yield return null;var bot=go.GetComponent<SawBotCombat>();bot.GetComponent<EnemyMobile>().enabled=false;bot.GetComponent<EnemyController>().enabled=false;bot.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled=false;bot.Animator.enabled=false;bot.enabled=false;go.GetComponent<LODGroup>().ForceLOD(0);
   var camera=new GameObject("SawCapture").AddComponent<Camera>();camera.transform.position=new Vector3(2.6f,1.9f,3.2f);camera.transform.LookAt(Vector3.up*.8f);camera.backgroundColor=new Color(.15f,.19f,.21f);camera.clearFlags=CameraClearFlags.SolidColor;camera.fieldOfView=36;var light=new GameObject("InspectionLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=3;light.transform.rotation=Quaternion.Euler(35,-40,0);yield return null;
   Capture(camera,"baseline-bind");var left=bot.LeftWheel.localRotation;var right=bot.RightWheel.localRotation;bot.LeftWheel.localRotation=left*Quaternion.AngleAxis(90,Vector3.right);bot.RightWheel.localRotation=right*Quaternion.AngleAxis(90,Vector3.right);yield return null;Capture(camera,"baseline-wheel90");
   Time.timeScale=1;AudioListener.pause=false;var scene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("SawSustainCleanup"));yield return SceneManager.UnloadSceneAsync(scene);
  }
  const string Variant="Assets/IronRequiem/Prefabs/Enemies/SawBot_Sustained_v02.prefab";
  PlayerCharacterController player;SawBotCombat bot;EnemyController enemy;Keyboard keyboard;Mouse mouse;
  float previousCapture;bool background;UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
  [UnitySetUp]public IEnumerator Before(){previousCapture=Time.captureDeltaTime;Time.captureDeltaTime=1f/60;background=Application.runInBackground;Application.runInBackground=true;Time.timeScale=1;AudioListener.pause=false;devices=InputSystem.actions.devices;keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keyboard.MakeCurrent();mouse.MakeCurrent();InputSystem.actions.devices=new InputDevice[]{keyboard,mouse};yield return null;}
  [UnityTearDown]public IEnumerator After(){Time.timeScale=1;AudioListener.pause=false;Time.captureDeltaTime=previousCapture;InputSystem.actions.devices=devices;InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);var scene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("SustainCleanup"+Time.frameCount));yield return SceneManager.UnloadSceneAsync(scene);Application.runInBackground=background;}
  IEnumerator Arena()
  {
   EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/SawBotSustainLab.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
   var menu=Object.FindAnyObjectByType<CombatLabMenu>();menu.BeginGame();menu.enabled=false;var waves=Object.FindAnyObjectByType<CombatTwoRoomsLab>();waves.enabled=false;if(waves.SpawnQueue)waves.SpawnQueue.enabled=false;Object.FindAnyObjectByType<GameFlowManager>().enabled=false;Object.FindAnyObjectByType<ObjectiveManager>().enabled=false;
   player=Object.FindAnyObjectByType<PlayerCharacterController>();player.GetComponent<Health>().Invincible=true;player.enabled=false;
   foreach(var c in Object.FindObjectsByType<Collider>())if(!c.transform.IsChildOf(player.transform)&&!(c.bounds.size.y<2&&c.bounds.size.x>20&&c.bounds.size.z>20))c.enabled=false;
   MovePlayer(new Vector3(-12,.03f,-10));yield return null;
  }
  void MovePlayer(Vector3 p){var c=player.GetComponent<CharacterController>();c.enabled=false;player.transform.SetPositionAndRotation(p,Quaternion.identity);c.enabled=true;player.CharacterVelocity=Vector3.zero;Physics.SyncTransforms();}
  IEnumerator Spawn(Vector3 p){Assert.That(NavMesh.SamplePosition(p,out var hit,3,NavMesh.AllAreas),Is.True);var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Variant),hit.position,Quaternion.Euler(0,180,0));bot=root.GetComponent<SawBotCombat>();enemy=root.GetComponent<EnemyController>();var path=new GameObject("SustainHunt").AddComponent<PatrolPath>();path.PathNodes.Add(player.transform);enemy.PatrolPath=path;yield return null;yield return null;}
  IEnumerator Until(Func<bool> condition,float seconds,string label){float end=Time.time+seconds,realEnd=Time.realtimeSinceStartup+60;while(!condition()&&Time.time<end&&Time.realtimeSinceStartup<realEnd)yield return null;Assert.That(condition(),Is.True,label+" pos="+(bot?bot.transform.position.ToString():"none")+" state="+(enemy?enemy.GetComponent<EnemyMobile>().AiState.ToString():"none"));}
  Camera ViewBot()
  {
   foreach(var r in Object.FindObjectsByType<Renderer>())if(!r.transform.IsChildOf(bot.transform))r.enabled=false;
   var cam=new GameObject("SawSustainView").AddComponent<Camera>();cam.tag="MainCamera";cam.transform.position=bot.transform.position+new Vector3(2.8f,1.7f,-3.4f);cam.transform.LookAt(bot.transform.position+Vector3.up*.85f);cam.backgroundColor=new Color(.15f,.19f,.21f);cam.clearFlags=CameraClearFlags.SolidColor;cam.fieldOfView=36;
   var light=new GameObject("SawSustainKey").AddComponent<Light>();light.type=LightType.Directional;light.intensity=3;light.transform.rotation=Quaternion.Euler(35,140,0);return cam;
  }
  [UnityTest]public IEnumerator WheelsGeometryAndTravel()
  {
   yield return Arena();yield return Spawn(new Vector3(-12,0,-5));enemy.GetComponent<EnemyMobile>().enabled=false;enemy.enabled=false;enemy.NavMeshAgent.enabled=false;bot.Animator.enabled=false;bot.GetComponent<LODGroup>().ForceLOD(0);
   var camera=ViewBot();yield return null;Capture(camera,"fixed-bind");var startLeft=bot.LeftWheel.localRotation;var startRight=bot.RightWheel.localRotation;var initialRoot=bot.transform.position;float radius=.28f;
   bot.transform.position+=bot.transform.forward*(radius*Mathf.PI*.5f);yield return null;
   float left=Vector3.SignedAngle(startLeft*Vector3.up,bot.LeftWheel.localRotation*Vector3.up,Vector3.right);Assert.That(Mathf.Abs(Mathf.DeltaAngle(left,-90)),Is.LessThan(1),"Quarter circumference produces quarter wheel turn around local axle");Capture(camera,"fixed-wheel90");
   var l=bot.LeftWheel.localRotation;var r=bot.RightWheel.localRotation;bot.transform.rotation*=Quaternion.Euler(0,12,0);yield return null;
   float dl=Vector3.SignedAngle(l*Vector3.up,bot.LeftWheel.localRotation*Vector3.up,Vector3.right),dr=Vector3.SignedAngle(r*Vector3.up,bot.RightWheel.localRotation*Vector3.up,Vector3.right);Assert.That(dl*dr,Is.LessThan(0),"Differential wheels oppose during an on-the-spot turn");
   l=bot.LeftWheel.localRotation;r=bot.RightWheel.localRotation;yield return new WaitForSeconds(.4f);Assert.That(Quaternion.Angle(l,bot.LeftWheel.localRotation),Is.LessThan(.01));Assert.That(Quaternion.Angle(r,bot.RightWheel.localRotation),Is.LessThan(.01));
   foreach(var skin in bot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
   {
    Assert.That(skin.bones.Length,Is.EqualTo(11));Assert.That(skin.sharedMesh.boneWeights.All(w=>w.weight0==1),Is.True);var vs=skin.sharedMesh.vertices;var ws=skin.sharedMesh.boneWeights;foreach(string name in new[]{"Wheel_L","Wheel_R"}){int index=Array.FindIndex(skin.bones,b=>b.name==name);var bone=skin.bones[index];float furthest=vs.Where((v,i)=>ws[i].boneIndex0==index).Select(v=>Vector3.ProjectOnPlane(bone.TransformPoint(skin.sharedMesh.bindposes[index].MultiplyPoint3x4(v))-bone.position,bot.transform.right).magnitude).Max();Assert.That(furthest,Is.LessThan(.32f),name+" has no orbiting chassis fragment");}
   }
   var clips=bot.Animator.runtimeAnimatorController.animationClips;Assert.That(clips.All(c=>AnimationUtility.GetCurveBindings(c).All(b=>!b.path.Contains("Wheel_"))),Is.True,"Animator does not write wheel transforms");
  }
  [UnityTest]public IEnumerator PursuitCurvesAndSmoothFacing()
  {
   yield return Arena();yield return Spawn(new Vector3(-12,0,8));Vector3 first=bot.transform.position;float fastest=0,maxYaw=0;var rotation=bot.transform.rotation;float end=Time.time+1.5f;
   while(Time.time<end){fastest=Mathf.Max(fastest,enemy.NavMeshAgent.velocity.magnitude);maxYaw=Mathf.Max(maxYaw,Quaternion.Angle(rotation,bot.transform.rotation));rotation=bot.transform.rotation;yield return null;}
   Assert.That(Vector3.Distance(first,bot.transform.position),Is.GreaterThan(5));Assert.That(fastest,Is.GreaterThan(6));MovePlayer(new Vector3(-3,.03f,-8));end=Time.time+1.2f;int aligned=0,moving=0;
   while(Time.time<end){var velocity=enemy.NavMeshAgent.velocity;if(velocity.magnitude>2){moving++;if(Vector3.Dot(bot.transform.forward,velocity.normalized)>.65f)aligned++;}maxYaw=Mathf.Max(maxYaw,Quaternion.Angle(rotation,bot.transform.rotation));rotation=bot.transform.rotation;yield return null;}
   Assert.That(maxYaw,Is.LessThan(6));Assert.That(aligned,Is.GreaterThan(moving*.75f));var camera=ViewBot();yield return null;Capture(camera,"navigation-turn");
   File.WriteAllText(Reports+"/navigation.txt","Peak actual agent speed="+fastest+" m/s. Maximum sampled yaw="+maxYaw+" degrees/frame at60Hz; aligned moving frames="+aligned+"/"+moving+". Real NavMesh movement, not scripted root translation.");
  }
  [UnityTest]public IEnumerator NavigationDetoursAroundCarvingWall()
  {
   yield return Arena();var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="NavigationWall";wall.transform.position=new Vector3(-12,2,-3);wall.transform.localScale=new Vector3(5,4,.6f);var obstacle=wall.AddComponent<NavMeshObstacle>();obstacle.carving=true;obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=Vector3.one;yield return new WaitForSeconds(.2f);yield return Spawn(new Vector3(-12,0,2));
   bool detour=false;float sideways=0,end=Time.time+9;
   while(Time.time<end&&!bot.IsAttacking){if(enemy.NavMeshAgent.hasPath&&enemy.NavMeshAgent.path.corners.Length>2)detour=true;sideways=Mathf.Max(sideways,Mathf.Abs(bot.transform.position.x+12));Assert.That(wall.GetComponent<Collider>().bounds.Contains(bot.transform.position+Vector3.up*.8f),Is.False,"Agent must not cut through wall");yield return null;}
   Assert.That(detour,Is.True);Assert.That(sideways,Is.GreaterThan(2));Assert.That(bot.IsAttacking,Is.True,"Recover sight/contact after actual detour");File.WriteAllText(Reports+"/detour.txt","NavMesh path corners>2; lateral detour="+sideways+"m; no wall intersection; contact restored.");
  }  [UnityTest]public IEnumerator ContactDamageAt30Fps(){yield return DamageRun(30);}
  [UnityTest]public IEnumerator ContactDamageAt144Fps(){yield return DamageRun(144);}
  IEnumerator DamageRun(int fps)
  {
   yield return Arena();Time.captureDeltaTime=1f/fps;var hp=player.GetComponent<Health>();hp.Invincible=false;hp.MaxHealth=hp.CurrentHealth=5000;yield return Spawn(new Vector3(-12,0,-7));
   yield return Until(()=>bot.ContactDamage>0,8,"Saws reach real player collider");float health=hp.CurrentHealth,contact=bot.ContactSeconds,damage=bot.ContactDamage;int attacks=bot.AttackCount;var position=bot.transform.position;
   for(int i=0;i<25;i++){bot.Impact();bot.Recover();}Assert.That(hp.CurrentHealth,Is.EqualTo(health),"Legacy animation callbacks cannot add damage or retract saws");
   yield return new WaitForSeconds(2);
   float seconds=bot.ContactSeconds-contact,applied=health-hp.CurrentHealth;Assert.That(seconds,Is.GreaterThan(1.95f));Assert.That(applied,Is.EqualTo(seconds*20).Within(2.05f));Assert.That(applied,Is.EqualTo(bot.ContactDamage-damage).Within(.01));Assert.That(bot.AttackCount,Is.EqualTo(attacks));Assert.That(Vector3.Distance(position,bot.transform.position),Is.LessThan(.025));
   File.WriteAllText(Reports+"/dps-"+fps+".txt","Simulation "+fps+" Hz, contact="+seconds+" s, health damage="+applied+", DPS="+applied/seconds+". One target across all saws/colliders; legacy callbacks ignored. Root displacement="+Vector3.Distance(position,bot.transform.position));
   if(fps==30){var camera=ViewBot();foreach(var other in Object.FindObjectsByType<Camera>())if(other!=camera)other.enabled=false;foreach(var canvas in Object.FindObjectsByType<Canvas>())if(canvas.renderMode!=RenderMode.WorldSpace)canvas.enabled=false;ScreenCapture.CaptureScreenshot(Path.GetFullPath(Reports+"/gameview-saw-contact.png"));for(int i=0;i<4;i++){Capture(camera,"saw-hold-"+i);yield return new WaitForSeconds(.12f);}}
   health=hp.CurrentHealth;contact=bot.ContactSeconds;Time.timeScale=0;var wheel=bot.LeftWheel.localRotation;yield return new WaitForSecondsRealtime(.25f);Assert.That(hp.CurrentHealth,Is.EqualTo(health));Assert.That(bot.ContactSeconds,Is.EqualTo(contact));Assert.That(bot.LeftWheel.localRotation,Is.EqualTo(wheel));Time.timeScale=1;
   MovePlayer(new Vector3(-12,.03f,-24));yield return null;health=hp.CurrentHealth;yield return new WaitForSeconds(.5f);Assert.That(hp.CurrentHealth,Is.EqualTo(health));Assert.That(bot.IsAttacking,Is.False);Assert.That(enemy.NavMeshAgent.velocity.magnitude,Is.GreaterThan(1));
  }
  [UnityTest]public IEnumerator ObstructionDeathAndImmediateStop()
  {
   yield return Arena();var hp=player.GetComponent<Health>();hp.Invincible=false;hp.MaxHealth=hp.CurrentHealth=5000;yield return Spawn(new Vector3(-12,0,-7));yield return Until(()=>bot.ContactDamage>0,8,"Initial saw contact");
   var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="ContactWall";wall.transform.position=(bot.transform.position+player.transform.position)*.5f+Vector3.up;wall.transform.localScale=new Vector3(5,4,.15f);var obstacle=wall.AddComponent<NavMeshObstacle>();obstacle.carving=true;Physics.SyncTransforms();yield return null;
   float before=hp.CurrentHealth;int hits=bot.HitCount;yield return new WaitForSeconds(.35f);Assert.That(hp.CurrentHealth,Is.EqualTo(before));Assert.That(bot.IsAttacking,Is.False);Assert.That(bot.HitCount,Is.EqualTo(hits));Object.Destroy(wall);yield return Until(()=>bot.IsAttacking&&bot.ContactDamage>0,8,"Attack after removing obstacle");
   before=hp.CurrentHealth;bot.GetComponent<Health>().Kill();yield return new WaitForSeconds(1);Assert.That(bot.IsDead,Is.True);Assert.That(bot.IsAttacking,Is.False);Assert.That(hp.CurrentHealth,Is.EqualTo(before));Assert.That(enemy.NavMeshAgent.enabled,Is.False);Assert.That(bot.DeathCount,Is.EqualTo(1));var camera=ViewBot();yield return null;Capture(camera,"sustain-death");
  }
  IEnumerator KeyPress(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;}
  [UnityTest]public IEnumerator TwoRoomsInterfacePauseDeathAndRestart()
  {
   EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/CombatTwoRoomsLab.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
   var menu=Object.FindAnyObjectByType<CombatLabMenu>();var view=menu.GetComponent<RequiemMenuView>();Assert.That(view,Is.Not.Null);Assert.That(menu.IsMainMenu,Is.True);Assert.That(Time.timeScale,Is.Zero);ScreenCapture.CaptureScreenshot(Path.GetFullPath(Reports+"/gameview-two-rooms-menu.png"));yield return null;
   view.Primary();yield return null;yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("CombatTwoRoomsLab"));Assert.That(menu.IsPlaying,Is.True);Assert.That(menu.IsMainMenu,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1));player=Object.FindAnyObjectByType<PlayerCharacterController>();player.GetComponent<Health>().Invincible=true;Object.FindAnyObjectByType<GameFlowManager>().enabled=false;yield return new WaitForSeconds(.7f);
   var inventory=player.GetComponent<PlayerWeaponsManager>();var weapon=inventory.GetActiveWeapon();var hands=player.GetComponent<ProtagonistHandsPresentation>();if(hands)Assert.That(hands.CurrentRepresentation,Is.Not.EqualTo(ProtagonistHandsPresentation.Representation.Normal),"Thermal variant preserves the arm-module representation");Assert.That(weapon.GetComponent<WeaponHeat>(),Is.Not.Null);Assert.That(Object.FindAnyObjectByType<WeaponHeatHud>().BoundHeat,Is.SameAs(weapon.GetComponent<WeaponHeat>()));Assert.That(weapon.HandleShootInputs(true,true,false),Is.True);yield return null;Assert.That(weapon.GetComponent<WeaponHeat>().CurrentHeat,Is.GreaterThan(0));
   var pause=Object.FindObjectsByType<InGameMenuManager>().Single(p=>p.enabled);yield return KeyPress(Key.Escape);Assert.That(pause.IsPaused,Is.True);Assert.That(Time.timeScale,Is.Zero);pause.GetComponent<RequiemMenuView>().OpenOptions();Assert.That(pause.GetComponent<RequiemMenuView>().IsOptionsOpen,Is.True);yield return KeyPress(Key.Escape);Assert.That(pause.IsPaused,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1));
   player.GetComponent<Health>().Invincible=false;player.GetComponent<Health>().Kill();yield return null;yield return null;Assert.That(menu.IsDeathMenu,Is.True);Assert.That(Time.timeScale,Is.Zero);menu.RestartFromFirstRoom();yield return null;yield return null;menu=Object.FindAnyObjectByType<CombatLabMenu>();Assert.That(menu.IsPlaying,Is.True);Assert.That(Object.FindAnyObjectByType<CombatTwoRoomsLab>().WaveIndex,Is.LessThanOrEqualTo(0));menu.ReturnToMainMenu();yield return null;yield return null;Assert.That(Object.FindAnyObjectByType<CombatLabMenu>().IsMainMenu,Is.True);Assert.That(Time.timeScale,Is.Zero);
  }  static void Capture(Camera camera,string name)
  {var target=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(Reports+"/"+name+".png",tex.EncodeToPNG());}finally{camera.targetTexture=target;RenderTexture.active=active;Object.Destroy(rt);Object.Destroy(tex);}}
 }
}
