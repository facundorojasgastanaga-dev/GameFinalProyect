using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
 [InitializeOnLoad] public static class CombatTwoRoomsAuthoring
 {
  const string Lab="Assets/IronRequiem/Scenes/CombatTwoRoomsLab.unity",Art="Assets/IronRequiem/Art/Levels/TwoRooms",Queue="Tools/IronRequiem/Requests/two-rooms.txt",Reports="Tools/IronRequiem/Reports/TwoRooms",Key="IRON.TwoRooms";static TestRunnerApi api;static readonly DateTime LoadedAt=DateTime.UtcNow;
  static CombatTwoRoomsAuthoring(){EditorApplication.update+=Tick;if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Register;}
  static bool Dirty()=>Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty);
  static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
  static void Tick(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool(Key,false))return;if(SessionState.GetBool(Key+".restore",false)){SessionState.EraseBool(Key+".restore");if(!Dirty())EditorSceneManager.OpenScene(Lab);IronRequiemSetupTools.WriteAudit();}if(!File.Exists(Queue))return;foreach(var p in new[]{"Assets/IronRequiem/Editor/CombatTwoRoomsAuthoring.cs","Assets/IronRequiem/Tests/PlayMode/CombatTwoRoomsTests.cs","Assets/IronRequiem/Scripts/Levels/CombatTwoRoomsLab.cs"})if(File.GetLastWriteTimeUtc(p)>LoadedAt)return;var op=File.ReadAllText(Queue).Trim();File.Delete(Queue);Directory.CreateDirectory(Reports);try{if(Dirty())throw new InvalidOperationException("Preserving dirty user scene");if(op=="build"){Build();File.WriteAllText(Queue,"test");}else{Register();SessionState.SetBool(Key,true);api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{"IronRequiem.Tests.CombatTwoRoomsTests"}}));}}catch(Exception e){File.WriteAllText(Reports+"/error.txt",e.ToString());Debug.LogException(e);}}
  static Material Mat(string name,Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(m,Art+"/"+name+".mat");return m;}
  static GameObject Cube(Transform root,string name,Vector3 pos,Vector3 size,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.position=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;}
  static Transform Point(Transform root,string name,Vector3 pos){var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.position=pos;return go.transform;}
  static void Build()
  {
   string[] prefabs={HitWeaponAuthoring.T07,BigBotBurstAuthoring.Enemy,CombatPolishAuthoring.Types[2],CombatPolishAuthoring.Types[3]};foreach(var p in prefabs){var g=AssetDatabase.LoadAssetAtPath<GameObject>(p);if(!g||!g.GetComponent<Health>()||!g.GetComponent<EnemyController>()||g.GetComponentsInChildren<Component>(true).Any(c=>!c))throw new InvalidOperationException("Invalid enemy prefab "+p);}
   if(File.Exists(Lab))throw new InvalidOperationException("Preserve existing lab");Directory.CreateDirectory(Art);AssetDatabase.Refresh();AssetDatabase.CopyAsset(BigBotBurstAuthoring.Lab,Lab);var scene=EditorSceneManager.OpenScene(Lab);
   foreach(var e in Object.FindObjectsByType<EnemyController>())Object.DestroyImmediate(e.gameObject);
   foreach(var surface in Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>()){surface.RemoveData();Object.DestroyImmediate(surface);}
   foreach(var root in scene.GetRootGameObjects()){bool keep=root.GetComponentsInChildren<Component>(true).Any(c=>c&&(c is PlayerCharacterController||c is Canvas||new[]{"ActorsManager","EnemyManager","GameFlowManager","ObjectiveManager","EventSystem","AudioManager"}.Contains(c.GetType().Name)));if(!keep)Object.DestroyImmediate(root);}
   foreach(var b in Object.FindObjectsByType<MonoBehaviour>())if(b is GameFlowManager||b.GetType().Name.StartsWith("Objective"))b.enabled=false;
   var player=Object.FindAnyObjectByType<PlayerCharacterController>();player.transform.SetPositionAndRotation(new Vector3(0,.03f,-28),Quaternion.identity);
   var floor=Mat("Floor",new Color(.24f,.29f,.32f));var walls=Mat("Walls",new Color(.55f,.59f,.61f));var cover=Mat("Cover",new Color(.2f,.36f,.43f));var gateMat=Mat("Gate",new Color(.85f,.45f,.10f));var geometry=new GameObject("TwoRoomsGeometry").transform;
   Cube(geometry,"Floor",new Vector3(0,-.5f,25),new Vector3(54,1,118),floor);foreach(int sign in new[]{-1,1})Cube(geometry,"SideWall",new Vector3(sign*27,3.5f,25),new Vector3(1,7,118),walls);Cube(geometry,"RearWall",new Vector3(0,3.5f,-34),new Vector3(54,7,1),walls);Cube(geometry,"FarWall",new Vector3(0,3.5f,84),new Vector3(54,7,1),walls);
   foreach(float z in new[]{24f,34f})foreach(int sign in new[]{-1,1})Cube(geometry,"Divider",new Vector3(sign*16.5f,3.5f,z),new Vector3(21,7,1),walls);
   foreach(int sign in new[]{-1,1})Cube(geometry,"CorridorSide",new Vector3(sign*6.5f,3.5f,29),new Vector3(1,7,10),walls);
   foreach(float center in new[]{0f,58f}){foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Cube(geometry,"Column",new Vector3(x*14,3.5f,center+z*12),new Vector3(2,7,2),walls);foreach(int x in new[]{-1,1}){Cube(geometry,"LowCover",new Vector3(x*8,.65f,center-4),new Vector3(4,1.3f,2),cover);Cube(geometry,"HighCover",new Vector3(x*9,1.8f,center+7),new Vector3(3,3.6f,2),cover);}}
   var gate=Cube(null,"ArenaGate",new Vector3(0,3.5f,29),new Vector3(12,7,1),gateMat);var obstacle=gate.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.carving=true;obstacle.size=Vector3.one;
   var settings=NavMesh.GetSettingsByID(0);settings.agentRadius=2.25f;settings.agentHeight=3.05f;settings.agentClimb=.25f;settings.agentSlope=35;settings.overrideVoxelSize=true;settings.voxelSize=.12f;var sources=new List<NavMeshBuildSource>();Physics.SyncTransforms();NavMeshBuilder.CollectSources(geometry,-1,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);var data=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(new Vector3(0,3,25),new Vector3(60,18,124)),Vector3.zero,Quaternion.identity);if(!data)throw new InvalidOperationException("NavMesh failed");AssetDatabase.CreateAsset(data,Art+"/TwoRoomsNavMesh.asset");var nav=geometry.gameObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();nav.agentTypeID=0;nav.collectObjects=Unity.AI.Navigation.CollectObjects.Children;nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.navMeshData=data;nav.AddData();
   var light=new GameObject("LabSun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(55,-30,0);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.55f,.6f);
   var spawns=new GameObject("WaveSpawnPoints").transform;Transform[] Points(float center)=>new[]{6f,14f,20f}.SelectMany(z=>new[]{-18f,0,18f}.Select(x=>Point(spawns,"Spawn_"+center+"_"+x+"_"+z,new Vector3(x,0,center+z)))).ToArray();var a=Points(0);var bpoints=Points(58);var turretPoint=Point(spawns,"CentralTurret",new Vector3(0,0,58));
   foreach(var p in a.Concat(bpoints).Append(turretPoint))if(!NavMesh.SamplePosition(p.position,out var sample,.5f,NavMesh.AllAreas))throw new InvalidOperationException("Unnavigable slot "+p.name);
   var canvas=new GameObject("ArenaMessages",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30;var text=new GameObject("Message",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();text.transform.SetParent(canvas.transform,false);text.font=TMP_Settings.defaultFontAsset;text.fontSize=28;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-25);rect.sizeDelta=new Vector2(1100,90);
   CombatTwoRoomsLab.Group G(int prefab,int count,Transform fixedPoint=null)=>new CombatTwoRoomsLab.Group{Prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[prefab]),Count=count,FixedPoint=fixedPoint};var coordinator=new GameObject("CombatTwoRoomsCoordinator").AddComponent<CombatTwoRoomsLab>();coordinator.Configure(player,new[]{new CombatTwoRoomsLab.Wave{Groups=new[]{G(0,6)}},new CombatTwoRoomsLab.Wave{Groups=new[]{G(0,3),G(1,1)}},new CombatTwoRoomsLab.Wave{Groups=new[]{G(2,3),G(1,2)}},new CombatTwoRoomsLab.Wave{Groups=new[]{G(1,4),G(3,1,turretPoint)}}},a,bpoints,gate,text);
   foreach(var p in new[]{new Vector3(-20,.5f,-12),new Vector3(20,.5f,12),new Vector3(-20,.5f,46),new Vector3(20,.5f,72)}){var pickup=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/Pickups/Pickup_Health.prefab"),scene);pickup.transform.position=p;pickup.GetComponent<HealthPickup>().HealAmount=25;}
   EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();File.WriteAllText(Reports+"/build.txt","All four enemy prefabs have Health/EnemyController and no missing components. AI Navigation bake radius 2.25 m. Two 52 m rooms, 12 m corridor, low/high cover, columns and four existing single-use 25 HP pickups. Original scenes/prefabs untouched; automatic template objectives disabled only in new lab.");
  }
  sealed class Results:IErrorCallbacks{public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}public void OnError(string m){SessionState.EraseBool(Key);File.WriteAllText(Reports+"/runner-error.txt",m);}public void RunFinished(ITestResultAdaptor r){if(!SessionState.GetBool(Key,false))return;SessionState.EraseBool(Key);TestRunnerApi.SaveResultToFile(r,Reports+"/play.xml");File.WriteAllText(Reports+"/status.txt",r.ResultState+" passed="+r.PassCount+" failed="+r.FailCount);SessionState.SetBool(Key+".restore",true);}}
 }
}
