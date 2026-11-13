using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class CampaignAuthoring
    {
        public const string Main="Assets/IronRequiem/Scenes/RequiemCampaign.unity",Folder="Assets/IronRequiem/Scenes/Campaign",Art="Assets/IronRequiem/Art/Levels/Campaign",Report="Tools/IronRequiem/Reports/Campaign";
        const string Source="Assets/IronRequiem/Scenes/CombatTwoFloorsLab.unity",Queue="Tools/IronRequiem/Requests/campaign.txt";
        public static readonly string[] Names={"Room01_Foundry","Room02_Breach","Room03_Assembly","Room04_Reactor","Pursuit05"};
        static readonly DateTime Loaded=DateTime.UtcNow;
        static readonly Dictionary<Vector3,Mesh> cubes=new Dictionary<Vector3,Mesh>();
        static GameObject[] enemies;
        static CampaignAuthoring(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Queue)||File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/CampaignAuthoring.cs")>Loaded||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))return;
            string op=File.ReadAllText(Queue).Trim();File.Delete(Queue);Directory.CreateDirectory(Report);
            try{if(op=="open")EditorSceneManager.OpenScene(Main);else if(op=="inspect")Inspect();else if(op=="nav")InspectNavigation();else if(op=="finish")Finish();else if(op=="anchors")RestoreAmbushAnchors();else if(op=="promote")Promote();else if(op=="upgrade")CampaignCurrentAssets.Apply();else if(op=="eyes")CampaignCurrentAssets.RefreshPolicies();else Build();IronRequiemSetupTools.WriteAudit();}
            catch(Exception e){File.WriteAllText(Report+"/error.txt",e.ToString());Debug.LogException(e);}
        }
        static T[] All<T>(Scene s)where T:Component=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Environment/Bunker/Bunker_"+name+".mat");
        static void Field(Object owner,string name,object value){var data=new SerializedObject(owner);var p=data.FindProperty(name);if(p==null)throw new Exception(owner+" missing "+name);if(value is bool b)p.boolValue=b;else if(value is float f)p.floatValue=f;else if(value is string t)p.stringValue=t;else if(value is Bounds bounds)p.boundsValue=bounds;else if(value is Vector3 v)p.vector3Value=v;else p.objectReferenceValue=value as Object;data.ApplyModifiedPropertiesWithoutUndo();}
        static Scene Copy(string source,string target){if(File.Exists(target))throw new Exception("Preserve existing campaign asset: "+target);if(!AssetDatabase.CopyAsset(source,target))throw new Exception("Copy failed: "+target);return EditorSceneManager.OpenScene(target,OpenSceneMode.Single);}
        static void RemoveNav(Scene scene){foreach(var surface in All<Unity.AI.Navigation.NavMeshSurface>(scene))Object.DestroyImmediate(surface);}
        static Transform Point(Transform parent,string name,Vector3 p){var t=new GameObject(name).transform;t.SetParent(parent,false);t.position=p;return t;}
        static GameObject Box(Transform parent,string name,Vector3 p,Vector3 size,string material="Wall",bool solid=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=p;go.transform.localScale=size;go.transform.SetParent(parent,true);go.GetComponent<Renderer>().sharedMaterial=Mat(material);if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());
            if(!cubes.TryGetValue(size,out var mesh))
            {
                mesh=Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);mesh.name="CampaignCube_"+cubes.Count;var v=mesh.vertices;var n=mesh.normals;var uv=new Vector2[v.Length];for(int i=0;i<v.Length;i++){var q=Vector3.Scale(v[i],size);uv[i]=Mathf.Abs(n[i].x)>.5f?new Vector2(q.z,q.y):Mathf.Abs(n[i].y)>.5f?new Vector2(q.x,q.z):new Vector2(q.x,q.y);}mesh.uv=uv;mesh.RecalculateTangents();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Art+"/"+mesh.name+".asset"));cubes.Add(size,mesh);
            }
            go.GetComponent<MeshFilter>().sharedMesh=mesh;return go;
        }
        static void CeilingLamp(Transform ceiling,Vector3 position,float intensity=3.45f)
        {
            var frame=Box(ceiling,"MountedCeilingFixture",position,new Vector3(2.4f,.16f,.6f),"RustMetal",false);
            Box(frame.transform,"EmissiveTube",position+Vector3.down*.09f,new Vector3(2.05f,.05f,.24f),"LampCold",false);
            var lamp=new GameObject("CeilingLight").AddComponent<Light>();lamp.transform.SetParent(frame.transform,true);lamp.transform.position=position+Vector3.down*.25f;lamp.type=LightType.Point;lamp.range=32;lamp.intensity=intensity;lamp.color=new Color(.8f,.9f,1);lamp.shadows=LightShadows.None;
        }
        static void LightSettings(){RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.39f,.44f,.49f);}
        static void Bake(GameObject geometry,string name,Bounds bounds)
        {
            Physics.SyncTransforms();var settings=NavMesh.GetSettingsByID(0);settings.agentRadius=2.25f;settings.agentHeight=3.05f;settings.agentClimb=.25f;settings.agentSlope=35;settings.overrideVoxelSize=true;settings.voxelSize=.12f;
            var sources=new List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(geometry.transform,-1,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            var localBounds=new Bounds(geometry.transform.InverseTransformPoint(bounds.center),bounds.size);var data=NavMeshBuilder.BuildNavMeshData(settings,sources,localBounds,geometry.transform.position,geometry.transform.rotation);if(!data)throw new Exception("NavMesh failed "+name);AssetDatabase.CreateAsset(data,Art+"/"+name+"_NavMesh.asset");
            var surface=geometry.AddComponent<Unity.AI.Navigation.NavMeshSurface>();surface.agentTypeID=0;surface.collectObjects=Unity.AI.Navigation.CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.navMeshData=data;surface.AddData();
        }
        static void Inspect()
        {
            var text=new StringBuilder();var scene=EditorSceneManager.GetActiveScene();text.AppendLine("Active="+scene.path);
            foreach(var root in scene.GetRootGameObjects())text.AppendLine(root.name+" "+string.Join(",",root.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name)));
            foreach(var light in All<Light>(scene))text.AppendLine("Light "+light.name+" "+light.type+" "+light.intensity+" at"+light.transform.position);
            File.WriteAllText(Report+"/inspection.txt",text.ToString());
        }
        static void InspectNavigation()
        {
            var log=new StringBuilder();foreach(string name in Names)
            {
                var scene=EditorSceneManager.OpenScene(Folder+"/"+name+".unity");Physics.SyncTransforms();
                foreach(var surface in All<Unity.AI.Navigation.NavMeshSurface>(scene)){surface.AddData();log.AppendLine(name+" surface="+surface.transform.position+" data="+surface.navMeshData.position+" bounds="+surface.navMeshData.sourceBounds+" vertices="+NavMesh.CalculateTriangulation().vertices.Length);}
                foreach(var rounds in All<CombatTwoRoomsLab>(scene)){var p=new SerializedObject(rounds).FindProperty("firstRoomSpawns");for(int i=0;i<p.arraySize;i++){var t=(Transform)p.GetArrayElementAtIndex(i).objectReferenceValue;bool valid=NavMesh.SamplePosition(t.position,out var hit,1,NavMesh.AllAreas);log.AppendLine(t.position+" valid="+valid+" nearest="+hit.position);}}
            }File.WriteAllText(Report+"/nav.txt",log.ToString());EditorSceneManager.OpenScene(Main);
        }
        static void Finish()
        {
            var log=new StringBuilder();for(int i=0;i<4;i++)
            {
                var scene=EditorSceneManager.OpenScene(Folder+"/"+Names[i]+".unity");var rounds=All<CombatTwoRoomsLab>(scene).Single();rounds.ConfigureVariety(true);
                var slots=new SerializedObject(rounds).FindProperty("firstRoomSpawns");for(int j=0;j<slots.arraySize;j++){var t=(Transform)slots.GetArrayElementAtIndex(j).objectReferenceValue;if(!NavMesh.SamplePosition(t.position,out _,1,NavMesh.AllAreas)){var p=t.position;p.x=Mathf.Sign(p.x)*22;t.position=p;if(!NavMesh.SamplePosition(t.position,out _,.75f,NavMesh.AllAreas))throw new Exception("Invalid corrected spawn "+t.position);log.AppendLine(Names[i]+" adjusted slot "+p);}}
                EditorUtility.SetDirty(rounds);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            var pursuitScene=EditorSceneManager.OpenScene(Folder+"/"+Names[4]+".unity");var surface=All<Unity.AI.Navigation.NavMeshSurface>(pursuitScene).Single();surface.RemoveData();var data=surface.navMeshData;var sources=new List<NavMeshBuildSource>();Physics.SyncTransforms();NavMeshBuilder.CollectSources(surface.transform,-1,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);var settings=NavMesh.GetSettingsByID(0);settings.agentRadius=2.25f;settings.agentHeight=3.05f;settings.agentClimb=.25f;settings.agentSlope=35;settings.overrideVoxelSize=true;settings.voxelSize=.12f;data.position=surface.transform.position;data.rotation=surface.transform.rotation;
            if(!NavMeshBuilder.UpdateNavMeshData(data,settings,sources,new Bounds(new Vector3(0,2,91),new Vector3(22,18,224))))throw new Exception("Shifted pursuit navigation rebake failed");EditorUtility.SetDirty(data);surface.AddData();if(!NavMesh.SamplePosition(new Vector3(0,8,219),out var hit,.75f,NavMesh.AllAreas))throw new Exception("Shifted pursuit spawn not navigable");log.AppendLine("Pursuit native navigation local bake, world spawn "+hit.position+" valid");EditorSceneManager.SaveScene(pursuitScene);AssetDatabase.SaveAssets();File.WriteAllText(Report+"/finish.txt",log.ToString());EditorSceneManager.OpenScene(Main);
        }
        static void RestoreAmbushAnchors()
        {
            var source=EditorSceneManager.OpenScene("Assets/IronRequiem/Scenes/ChaseCorridorLab.unity");var original=All<ChaseCorridorEncounter>(source).Single();var positions=original.Ambushes.Select(a=>a.Points.Select(t=>t.position).ToArray()).ToArray();var scene=EditorSceneManager.OpenScene(Folder+"/"+Names[4]+".unity");var encounter=All<ChaseCorridorEncounter>(scene).Single();var data=new SerializedObject(encounter);var zones=data.FindProperty("ambushes");var root=Point(null,"AmbushPoints",Vector3.zero);
            for(int i=0;i<positions.Length;i++){var points=zones.GetArrayElementAtIndex(i).FindPropertyRelative("Points");points.arraySize=positions[i].Length;for(int j=0;j<positions[i].Length;j++)points.GetArrayElementAtIndex(j).objectReferenceValue=Point(root,"Ambush_"+i+"_"+j,positions[i][j]+new Vector3(0,8,185));}data.ApplyModifiedPropertiesWithoutUndo();foreach(var text in All<TextMeshPro>(scene))if(text.text.StartsWith("03 /"))text.text=text.text.Replace("03 /","05 /");EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText(Report+"/anchors.txt","Restored all5 original ambush anchors at shifted world positions; no source edits.");EditorSceneManager.OpenScene(Main);
        }
        static void Promote()
        {
            var pursuit=EditorSceneManager.OpenScene(Folder+"/"+Names[4]+".unity");int removed=0;foreach(var t in pursuit.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="CeilingLamp").ToArray()){Object.DestroyImmediate(t.gameObject);removed++;}if(removed>0){var probe=All<ReflectionProbe>(pursuit).Single();Lightmapping.BakeReflectionProbe(probe,Art+"/"+Names[4]+"_Reflection.exr");EditorSceneManager.MarkSceneDirty(pursuit);EditorSceneManager.SaveScene(pursuit);}
            string[] titles={"01 · FUNDICIÓN","02 · BRECHA SUPERIOR","03 · MONTAJE","04 · REACTOR"};for(int i=0;i<4;i++){var roomScene=EditorSceneManager.OpenScene(Folder+"/"+Names[i]+".unity");Field(All<CombatTwoRoomsLab>(roomScene).Single(),"arenaTitle",titles[i]);EditorSceneManager.MarkSceneDirty(roomScene);EditorSceneManager.SaveScene(roomScene);}
            string before=EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"<active scene>";File.WriteAllText("Tools/IronRequiem/Backups/20261108-campaign/play-mode-start-before.txt",before);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/FPS/Scenes/IntroMenu.unity");File.WriteAllText(Report+"/entry.txt","Editor Play starts IntroMenu; JUGAR target RequiemCampaign. Previous Play Mode start="+before);EditorSceneManager.OpenScene("Assets/FPS/Scenes/IntroMenu.unity");
        }
        static void Build()
        {
            if(File.Exists(Main)||Names.Any(n=>File.Exists(Folder+"/"+n+".unity")))throw new Exception("Campaign exists; do not overwrite it");
            Directory.CreateDirectory(Folder);Directory.CreateDirectory(Art);AssetDatabase.Refresh();cubes.Clear();
            string[] prefabs={"Assets/IronRequiem/Prefabs/PhaseFlow/T07_LegRepair_Charged_v05_Execution.prefab","Assets/IronRequiem/Prefabs/PhaseFlow/BigBot_Burst_v10_Execution.prefab","Assets/IronRequiem/Prefabs/Enemies/HoverBot_CombatPolish.prefab","Assets/IronRequiem/Prefabs/Enemies/Turret_CombatPolish.prefab","Assets/IronRequiem/Prefabs/Enemies/SawBot_SawPolish_v04.prefab"};
            enemies=prefabs.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();if(enemies.Any(g=>!g||!g.GetComponent<Health>()||!g.GetComponent<Unity.FPS.AI.EnemyController>()))throw new Exception("An actual enemy prefab is missing");
            BuildOriginalRoom(0);BuildOriginalRoom(1);BuildNewRoom(2);BuildNewRoom(3);BuildPursuit();BuildSession();
            var entries=EditorBuildSettings.scenes.ToList();foreach(string path in new[]{Main}.Concat(Names.Select(n=>Folder+"/"+n+".unity")))if(!entries.Any(e=>e.path==path))entries.Add(new EditorBuildSettingsScene(path,true));EditorBuildSettings.scenes=entries.ToArray();
            var menuScene=EditorSceneManager.OpenScene("Assets/FPS/Scenes/IntroMenu.unity");int changed=0;foreach(var loader in All<LoadSceneButton>(menuScene))if(loader.SceneName=="MainScene"||loader.SceneName=="CombatTwoFloorsLab"||loader.SceneName=="CombatTwoRoomsLab"){loader.SceneName="RequiemCampaign";EditorUtility.SetDirty(loader);if(PrefabUtility.IsPartOfPrefabInstance(loader))PrefabUtility.RecordPrefabInstancePropertyModifications(loader);changed++;}EditorSceneManager.SaveScene(menuScene);
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(Main);File.WriteAllText(Report+"/built.txt","Campaign created:4 independently streamed rooms,3 rounds each, final original pursuit. IntroMenu loaders changed="+changed+". Persistent player/menu/inventory and four2-door airlocks; unload old room before loading next; no hidden old room in memory. Shared original prefabs, URP/Input and MainScene retained. Ceiling fixtures attached; no shadowed extra lights.");
        }
        static CombatTwoRoomsLab.Wave[] FirstWaves(CombatTwoRoomsLab source)
        {
            var array=new SerializedObject(source).FindProperty("waves");var result=new CombatTwoRoomsLab.Wave[3];
            CombatTwoRoomsLab.Group[] Groups(SerializedProperty list){var groups=new CombatTwoRoomsLab.Group[list.arraySize];for(int j=0;j<groups.Length;j++){var g=list.GetArrayElementAtIndex(j);groups[j]=new CombatTwoRoomsLab.Group{Prefab=(GameObject)g.FindPropertyRelative("Prefab").objectReferenceValue,Count=g.FindPropertyRelative("Count").intValue};}return groups;}
            for(int i=0;i<3;i++){var w=array.GetArrayElementAtIndex(i);var variations=w.FindPropertyRelative("Variations");result[i]=new CombatTwoRoomsLab.Wave{Groups=Groups(w.FindPropertyRelative("Groups")),Variations=new CombatTwoRoomsLab.Composition[variations.arraySize]};for(int j=0;j<variations.arraySize;j++){var v=variations.GetArrayElementAtIndex(j);result[i].Variations[j]=new CombatTwoRoomsLab.Composition{Name=v.FindPropertyRelative("Name").stringValue,Groups=Groups(v.FindPropertyRelative("Groups"))};}}
            return result;
        }
        static void BuildOriginalRoom(int index)
        {
            var scene=Copy(Source,Folder+"/"+Names[index]+".unity");RemoveNav(scene);var original=All<CombatTwoRoomsLab>(scene).Single();var first=FirstWaves(original);var geometry=scene.GetRootGameObjects().Single(g=>g.name=="TwoFloorsGeometry");
            foreach(var child in geometry.transform.Cast<Transform>().ToArray())
            {
                string n=child.name;bool shared=n=="WestWall"||n=="SouthWall";bool keep=index==0?n=="GroundFloor"||n=="NorthLowerWall"||n=="WestLowerExtension"||n=="EastLowerWall"||shared||((n=="SupportColumn"||n=="LowCover"||n=="HighCover")&&child.position.y<7):n=="UpperNearBank"||n=="UpperFarBank"||n=="Roof"||n=="NorthWall"||n.StartsWith("EastUpper")||n=="GapWarningEdge"||shared||((n=="SupportColumn"||n=="LowCover"||n=="HighCover")&&child.position.y>7);
                if(!keep){Object.DestroyImmediate(child.gameObject);continue;}
                if(shared){var p=child.position;p.y=index==0?3.5f:12;child.position=p;var s=child.localScale;s.y=index==0?7:8;child.localScale=s;}
            }
            foreach(var root in scene.GetRootGameObjects())
            {
                bool pickup=root.GetComponentInChildren<HealthPickup>(true)&&((root.transform.position.y<7)==(index==0));
                bool hazard=index==1&&(root.GetComponent<GapFallRecovery>()||root.name=="GapReturnPoints"||root.name=="GapSign");
                if(root!=geometry&&!pickup&&!hazard)Object.DestroyImmediate(root);
            }
            Transform ceiling;
            if(index==0)ceiling=Box(geometry.transform,"Room01Ceiling",new Vector3(0,7.25f,-1.5f),new Vector3(54,.5f,65)).transform;
            else
            {
                ceiling=geometry.transform.Find("Roof");Object.DestroyImmediate(geometry.transform.Find("NorthWall").gameObject);
                foreach(float x in new[]{-16f,16f})Box(geometry.transform,"NorthExitWall",new Vector3(x,12,27),new Vector3(22,8,1));
            }
            float fixtureY=index==0?6.92f:15.92f;foreach(float x in new[]{-15f,15f})foreach(float z in new[]{-18f,18f})CeilingLamp(ceiling,new Vector3(x,fixtureY,z));
            var node=new GameObject("RoomEncounter",typeof(EnemySpawnQueue),typeof(CombatTwoRoomsLab),typeof(CampaignRoom));var queue=node.GetComponent<EnemySpawnQueue>();queue.Configure(1,1);
            var points=Point(node.transform,"WaveSlots",Vector3.zero);float floor=index==0?0:8;
            var slots=(index==0?new[]{-18f,-6f,6f,18f}:new[]{6f,14f,20f}).SelectMany(z=>new[]{-18f,0f,18f}.Select(x=>Point(points,"Spawn",new Vector3(x,floor,z)))).ToArray();
            if(index==1)slots.First(t=>Mathf.Abs(t.position.x)<.1f&&Mathf.Abs(t.position.z-14)<.1f).position=new Vector3(-7,8,20);
            var turret=Point(points,"CentralTurret",new Vector3(0,8,14));
            var rounds=index==0?first:new[]{Wave(G(0,4),G(4,2)),Wave(G(1,2),G(2,3)),Wave(G(1,4),G(3,1,turret))};
            ConfigureRounds(node,rounds,slots,index==0?new Bounds(new Vector3(0,2,0),new Vector3(48,5,48)):new Bounds(new Vector3(0,10,14),new Vector3(48,5,22)),index);
            LightSettings();Bake(geometry,Names[index],new Bounds(new Vector3(0,index==0?3:11,-1),new Vector3(58,12,70)));EditorSceneManager.SaveScene(scene);
        }
        static CombatTwoRoomsLab.Group G(int enemy,int count,Transform point=null)=>new CombatTwoRoomsLab.Group{Prefab=enemies[enemy],Count=count,FixedPoint=point};
        static CombatTwoRoomsLab.Wave Wave(params CombatTwoRoomsLab.Group[] groups)=>new CombatTwoRoomsLab.Wave{Groups=groups};
        static void ConfigureRounds(GameObject node,CombatTwoRoomsLab.Wave[] waves,Transform[] slots,Bounds bounds,int index)
        {
            var rounds=node.GetComponent<CombatTwoRoomsLab>();rounds.Configure(null,waves,slots,Array.Empty<Transform>(),null,null);rounds.UseSpawnQueue(node.GetComponent<EnemySpawnQueue>());rounds.ConfigureVariety(true);Field(rounds,"singleArena",true);Field(rounds,"arenaTitle","CUARTO "+(index+1));Field(rounds,"firstRoom",bounds);node.GetComponent<CampaignRoom>().Configure(rounds,null,node.GetComponent<EnemySpawnQueue>());
        }
        static void BuildNewRoom(int index)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var geometry=new GameObject(index==2?"AssemblyGeometry":"ReactorGeometry");var parent=geometry.transform;float south=index==2?39:103,center=south+26,north=south+52;
            Box(parent,"Floor",new Vector3(0,7.5f,center),new Vector3(54,1,52),"Floor");var ceiling=Box(parent,"Roof",new Vector3(0,16.25f,center),new Vector3(54,.5f,54)).transform;
            foreach(float x in new[]{-27f,27f})Box(parent,"SideWall",new Vector3(x,12,center),new Vector3(1,8,54));foreach(float z in new[]{south,north})foreach(float x in new[]{-16f,16f})Box(parent,"PortalWall",new Vector3(x,12,z),new Vector3(22,8,1));
            if(index==2)
            {
                foreach(float x in new[]{-10f,10f})foreach(float z in new[]{center-8,center+8})Box(parent,"AssemblyBench",new Vector3(x,8.8f,z),new Vector3(4,1.6f,10),"RustMetal");
                foreach(float x in new[]{-18f,18f})foreach(float z in new[]{center-7,center+7})Box(parent,"CeilingSupport",new Vector3(x,12,z),new Vector3(2,8,2));
            }
            else
            {
                var core=GameObject.CreatePrimitive(PrimitiveType.Cylinder);core.name="ReactorCore";core.transform.SetParent(parent,false);core.transform.position=new Vector3(0,10.75f,center);core.transform.localScale=new Vector3(7,2.75f,7);core.GetComponent<Renderer>().sharedMaterial=Mat("RustMetal");
                foreach(float x in new[]{-12f,12f})foreach(float z in new[]{center-8,center+8})Box(parent,"RingCover",new Vector3(x,8.65f,z),new Vector3(4,1.3f,3),"Hazard");
                foreach(float x in new[]{-23f,23f})Box(parent,"CeilingSupport",new Vector3(x,12,center),new Vector3(2,8,2));
            }
            foreach(float x in new[]{-15f,15f})foreach(float z in new[]{center-17,center+17})CeilingLamp(ceiling,new Vector3(x,15.92f,z));
            foreach(float x in new[]{-21f,21f}){var pickup=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/Pickups/Pickup_Health.prefab"),scene);pickup.transform.position=new Vector3(x,8.5f,center-18);pickup.GetComponent<HealthPickup>().HealAmount=25;}
            var node=new GameObject("RoomEncounter",typeof(EnemySpawnQueue),typeof(CombatTwoRoomsLab),typeof(CampaignRoom));node.GetComponent<EnemySpawnQueue>().Configure(1,1);var pointRoot=Point(node.transform,"WaveSlots",Vector3.zero);var slots=new[]{-18f,-8f,8f,18f}.SelectMany(z=>new[]{-18f,0f,18f}.Select(x=>Point(pointRoot,"Spawn",new Vector3(x,8,center+z)))).ToArray();var turret=Point(pointRoot,"TurretSlot",new Vector3(0,8,center+12));
            var waves=index==2?new[]{Wave(G(4,4),G(0,2)),Wave(G(1,2),G(0,3)),Wave(G(4,3),G(1,2),G(2,2))}:new[]{Wave(G(0,3),G(2,3)),Wave(G(1,2),G(4,2),G(3,1,turret)),Wave(G(1,3),G(4,3))};
            ConfigureRounds(node,waves,slots,new Bounds(new Vector3(0,10,center),new Vector3(48,6,46)),index);LightSettings();Bake(geometry,Names[index],new Bounds(new Vector3(0,11,center),new Vector3(58,12,58)));EditorSceneManager.SaveScene(scene,Folder+"/"+Names[index]+".unity");
        }
        static void BuildPursuit()
        {
            var scene=Copy("Assets/IronRequiem/Scenes/ChaseCorridorLab.unity",Folder+"/"+Names[4]+".unity");RemoveNav(scene);var encounter=All<ChaseCorridorEncounter>(scene).Single();var geometry=scene.GetRootGameObjects().Single(g=>g.name=="ChaseCorridorGeometry");var pointRoots=new HashSet<GameObject>(encounter.Ambushes.SelectMany(a=>a.Points).Select(t=>t.root.gameObject));
            foreach(var root in scene.GetRootGameObjects())
            {
                bool keep=root==geometry||root==encounter.gameObject||pointRoots.Contains(root)||root.name=="GiantSawCarriage"||root.name=="ChaseEntranceGate"||root.name=="ChaseExitGate"||root.name.StartsWith("ReturnPoints_")||root.GetComponent<GapFallRecovery>()||root.GetComponentInChildren<HealthPickup>(true)||root.GetComponent<TextMeshPro>();if(!keep)Object.DestroyImmediate(root);
            }
            foreach(var light in All<Light>(scene))Object.DestroyImmediate(light.gameObject);
            var offset=new Vector3(0,8,185);foreach(var root in scene.GetRootGameObjects())root.transform.position+=offset;
            var data=new SerializedObject(encounter);data.FindProperty("player").objectReferenceValue=null;data.FindProperty("menu").objectReferenceValue=null;data.FindProperty("message").objectReferenceValue=null;data.FindProperty("courseOffset").vector3Value=offset;data.FindProperty("finishZ").floatValue+=offset.z;var zones=data.FindProperty("ambushes");for(int i=0;i<zones.arraySize;i++)zones.GetArrayElementAtIndex(i).FindPropertyRelative("ActivationZ").floatValue+=offset.z;data.ApplyModifiedPropertiesWithoutUndo();
            foreach(var gap in All<GapFallRecovery>(scene)){data=new SerializedObject(gap);data.FindProperty("player").objectReferenceValue=null;data.FindProperty("upperFloorHeight").floatValue+=offset.y;data.FindProperty("gapNearZ").floatValue+=offset.z;data.FindProperty("gapFarZ").floatValue+=offset.z;data.ApplyModifiedPropertiesWithoutUndo();}
            foreach(var text in All<TextMeshPro>(scene))text.text=text.text.Replace("SECTOR 03","SECTOR 05");
            var roof=geometry.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.Contains("Roof")||t.name.Contains("Ceiling"))??geometry.transform;
            foreach(float z in new[]{-6f,24f,64f,114f,164f,190f})CeilingLamp(roof,new Vector3(0,15.72f,z+185),3.45f);
            encounter.gameObject.AddComponent<CampaignRoom>().Configure(null,encounter,encounter.GetComponent<EnemySpawnQueue>());LightSettings();Bake(geometry,Names[4],new Bounds(new Vector3(0,10,276),new Vector3(22,18,224)));EditorSceneManager.SaveScene(scene);
        }
        static void BuildSession()
        {
            var scene=Copy(Source,Main);RemoveNav(scene);var source=All<CombatTwoRoomsLab>(scene).Single();var sourceData=new SerializedObject(source);var label=(TMP_Text)sourceData.FindProperty("message").objectReferenceValue;var oldGate=(GameObject)sourceData.FindProperty("gate").objectReferenceValue;var geometry=scene.GetRootGameObjects().Single(g=>g.name=="TwoFloorsGeometry");
            foreach(var child in geometry.transform.Cast<Transform>().ToArray())if(!(child.name.StartsWith("Stair")||child.name=="UpperLanding"))Object.DestroyImmediate(child.gameObject);
            foreach(var recovery in All<GapFallRecovery>(scene))Object.DestroyImmediate(recovery.gameObject);foreach(var entrance in All<ChaseSceneEntrance>(scene)){var doorData=new SerializedObject(entrance);var d=(GameObject)doorData.FindProperty("sealedDoor").objectReferenceValue;if(d)Object.DestroyImmediate(d);Object.DestroyImmediate(entrance.gameObject);}
            Object.DestroyImmediate(source.gameObject);if(oldGate)Object.DestroyImmediate(oldGate);
            foreach(var pickup in All<HealthPickup>(scene))Object.DestroyImmediate(pickup.gameObject);
            foreach(var root in scene.GetRootGameObjects().Where(g=>g.name=="WaveSpawnPoints"||g.name=="GapReturnPoints"||g.name=="GapSign"||g.name=="StairSign"||g.name.StartsWith("Sector03")))Object.DestroyImmediate(root);
            foreach(var light in All<Light>(scene).Where(l=>l.type!=LightType.Directional))Object.DestroyImmediate(light.gameObject);
            geometry.name="PersistentAirlocks";
            Box(geometry.transform,"InnerStairGuard",new Vector3(27,4,5.5f),new Vector3(1,8,27));Box(geometry.transform,"UpperInnerStairGuard",new Vector3(27,12,12.5f),new Vector3(1,8,37));Box(geometry.transform,"LandingUnderWall",new Vector3(27,4,-11),new Vector3(1,8,6));
            var stairRoof=Box(geometry.transform,"StairRoof",new Vector3(31,16.25f,8.5f),new Vector3(8,.5f,46));foreach(float z in new[]{-10f,8f,26f})CeilingLamp(stairRoof.transform,new Vector3(31,15.92f,z),3.45f);
            var flow=new GameObject("CampaignSession").AddComponent<CampaignFlow>();var doors=new List<CampaignAirlock>();
            var lower=Box(geometry.transform,"Room01ExitDoor",new Vector3(27,3.75f,25),new Vector3(1,7.5f,12),"Hazard");var upper=Box(geometry.transform,"Room02EntryDoor",new Vector3(27,11.75f,-10),new Vector3(1,7.5f,8),"Hazard");
            var first=new GameObject("StairAirlock",typeof(BoxCollider),typeof(Rigidbody),typeof(CampaignAirlock));first.transform.SetParent(geometry.transform,false);first.transform.position=new Vector3(31,2,25);first.GetComponent<BoxCollider>().size=new Vector3(5,4,6);SetTrigger(first);first.GetComponent<CampaignAirlock>().Configure(flow,0,lower,upper);doors.Add(first.GetComponent<CampaignAirlock>());
            foreach(float z in new[]{27f,91f,155f})
            {
                var root=Point(geometry.transform,"TransferAirlock_"+z,Vector3.zero);Box(root,"AirlockFloor",new Vector3(0,7.5f,z+6),new Vector3(12,1,12),"Floor");var roof=Box(root,"AirlockCeiling",new Vector3(0,16.25f,z+6),new Vector3(12,.5f,12));foreach(float x in new[]{-6.5f,6.5f})Box(root,"AirlockWall",new Vector3(x,12,z+6),new Vector3(1,8,12));CeilingLamp(roof.transform,new Vector3(0,15.92f,z+6),3.45f);
                var back=Box(root,"SourceDoor",new Vector3(0,12,z),new Vector3(10,8,.6f),"Hazard");var next=Box(root,"DestinationDoor",new Vector3(0,12,z+12),new Vector3(10,8,.6f),"Hazard");var trigger=new GameObject("Transfer",typeof(BoxCollider),typeof(Rigidbody),typeof(CampaignAirlock));trigger.transform.SetParent(root,false);trigger.transform.position=new Vector3(0,10,z+6);trigger.GetComponent<BoxCollider>().size=new Vector3(8,4,6);SetTrigger(trigger);trigger.GetComponent<CampaignAirlock>().Configure(flow,doors.Count,back,next);doors.Add(trigger.GetComponent<CampaignAirlock>());
            }
            var lobby=Point(geometry.transform,"StartLobby",Vector3.zero);Box(lobby,"LobbyFloor",new Vector3(0,-.52f,-30.5f),new Vector3(12,1,9),"Floor");var lobbyRoof=Box(lobby,"LobbyRoof",new Vector3(0,7.25f,-30.5f),new Vector3(12,.5f,9));foreach(float x in new[]{-6.5f,6.5f})Box(lobby,"LobbyWall",new Vector3(x,3.5f,-30.5f),new Vector3(1,7,9));Box(lobby,"LobbyRear",new Vector3(0,3.5f,-35),new Vector3(14,7,1));CeilingLamp(lobbyRoof.transform,new Vector3(0,6.92f,-31));var initial=Box(lobby,"InitialLoadingDoor",new Vector3(0,3.5f,-26),new Vector3(12,7,.6f),"Hazard");
            var player=All<PlayerCharacterController>(scene).Single();player.transform.SetPositionAndRotation(new Vector3(0,.03f,-31),Quaternion.identity);PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);flow.Configure(player,All<CombatLabMenu>(scene).Single(),label,Names,doors.ToArray(),initial);
            foreach(var text in All<TMP_Text>(scene))if(text.text.Contains("LABORATORIO"))text.text="CAMPAÑA · CUATRO SECTORES";
            LightSettings();EditorSceneManager.SaveScene(scene);
        }
        static void SetTrigger(GameObject go){go.GetComponent<BoxCollider>().isTrigger=true;go.GetComponent<Rigidbody>().isKinematic=true;go.GetComponent<Rigidbody>().useGravity=false;}
    }
}
