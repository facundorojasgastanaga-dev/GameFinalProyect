using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.AI;
using Unity.FPS.UI;
using UnityEditor.Animations;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.FPS.Gameplay;
namespace IronRequiem.Editor
{
 [InitializeOnLoad] public static class SawBotSustainAuthoring
 {
  public const string Reports="Tools/IronRequiem/Reports/SawBotSustain",Queue="Tools/IronRequiem/Requests/sawbot-sustain.txt";
  static readonly DateTime Loaded=DateTime.UtcNow;
  static SawBotSustainAuthoring(){EditorApplication.update+=Tick;}
  static void Tick(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Queue)||File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/SawBotSustainAuthoring.cs")>Loaded)return;
   if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))return;
   string op=File.ReadAllText(Queue).Trim();File.Delete(Queue);try{if(op=="inspect")Inspect();else Build();}catch(Exception e){File.WriteAllText(Reports+"/error.txt",e.ToString());Debug.LogException(e);}}
  static void Inspect(){var b=new StringBuilder();var root=PrefabUtility.LoadPrefabContents("Assets/IronRequiem/Prefabs/Enemies/SawBot_AI_v01.prefab");try{
   foreach(var r in root.GetComponentsInChildren<Renderer>(true)){b.AppendLine(r.name+" type="+r.GetType().Name+" enabled="+r.enabled+" bounds="+r.bounds);if(r is ParticleSystemRenderer p)b.AppendLine(" particles="+p.renderMode+" mesh="+p.mesh+" material="+p.sharedMaterial);if(r is SkinnedMeshRenderer s){b.AppendLine(" mesh="+s.sharedMesh.name+" triangles="+s.sharedMesh.triangles.Length/3);var vertices=s.sharedMesh.vertices;var weights=s.sharedMesh.boneWeights;for(int i=0;i<s.bones.Length;i++){var points=vertices.Where((v,k)=>weights[k].boneIndex0==i).ToArray();var bounds=new Bounds(points[0],Vector3.zero);foreach(var v in points)bounds.Encapsulate(v);b.AppendLine("  bone="+s.bones[i].name+" pos="+s.bones[i].position+" axis="+s.bones[i].right+" meshBounds="+bounds);}}}
   foreach(var clip in root.GetComponentInChildren<Animator>().runtimeAnimatorController.animationClips.Distinct()){b.AppendLine("clip="+clip.name+" length="+clip.length);foreach(var curve in AnimationUtility.GetCurveBindings(clip))b.AppendLine(" curve="+curve.path+" "+curve.propertyName);}
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  foreach(string path in new[]{"Assets/IronRequiem/Scenes/CombatTwoRoomsLab.unity","Assets/IronRequiem/Scenes/SawBotLab.unity","Assets/IronRequiem/Scenes/BunkerLab.unity"}){
   var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);b.AppendLine(path);
   foreach(var go in scene.GetRootGameObjects()){foreach(var w in go.GetComponentsInChildren<PlayerWeaponsManager>(true))foreach(var weapon in w.StartingWeapons)b.AppendLine("weapon="+AssetDatabase.GetAssetPath(weapon));foreach(var c in go.GetComponentsInChildren<Canvas>(true))b.AppendLine("canvas="+c.name+" active="+c.gameObject.activeSelf+" scale="+c.GetComponent<UnityEngine.UI.CanvasScaler>()?.referenceResolution);foreach(var group in go.GetComponentsInChildren<CombatTwoRoomsLab>(true)){var data=new SerializedObject(group);var waves=data.FindProperty("waves");for(int i=0;i<waves.arraySize;i++){var list=waves.GetArrayElementAtIndex(i).FindPropertyRelative("Groups");for(int j=0;j<list.arraySize;j++)b.AppendLine("wave="+i+" prefab="+AssetDatabase.GetAssetPath(list.GetArrayElementAtIndex(j).FindPropertyRelative("Prefab").objectReferenceValue));}}}
   if(opened)EditorSceneManager.CloseScene(scene,true);
  }File.WriteAllText(Reports+"/inspection.txt",b.ToString());IronRequiemSetupTools.WriteAudit();}
  public const string Enemy="Assets/IronRequiem/Prefabs/Enemies/SawBot_Sustained_v02.prefab",Lab="Assets/IronRequiem/Scenes/SawBotSustainLab.unity",Art="Assets/IronRequiem/Art/Enemies/SawBotSustain";
  static T[] All<T>(Scene s)where T:Component=>s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
  static void Record(Scene scene){foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true)){if(PrefabUtility.IsPartOfPrefabInstance(t.gameObject))PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);foreach(var c in t.GetComponents<Component>())if(c&&PrefabUtility.IsPartOfPrefabInstance(c))PrefabUtility.RecordPrefabInstancePropertyModifications(c);}}
  static void Build()
  {
   BuildVariant();
   foreach(string path in new[]{"Assets/IronRequiem/Scenes/CombatTwoRoomsLab.unity","Assets/IronRequiem/Scenes/BunkerLab.unity","Assets/IronRequiem/Scenes/SawBotLab.unity"})
   {
    var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
    try{
     ReplaceSawReferences(scene);
     if(path.EndsWith("CombatTwoRoomsLab.unity")||path.EndsWith("BunkerLab.unity"))IntegrateInterface(scene);
     Record(scene);EditorSceneManager.SaveScene(scene);
     if(path.EndsWith("SawBotLab.unity"))EditorSceneManager.SaveScene(scene,Lab,true);
    }finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
   }
   var build=EditorBuildSettings.scenes.ToList();if(!build.Any(s=>s.path==Lab))build.Add(new EditorBuildSettingsScene(Lab,true));EditorBuildSettings.scenes=build.ToArray();AssetDatabase.SaveAssets();
   File.WriteAllText(Reports+"/build.txt","Variant "+Enemy+"; repaired wheel regions/pivots across three LODs; source assets preserved. Sustained20 DPS/.1s, approach7.2m/s accel24. CombatTwoRoomsLab + BunkerLab use thermal HUD/industrial lab menu/pause with original inventories. Saw references updated only where present; counts unchanged.");IronRequiemSetupTools.WriteAudit();
  }
  static void ReplaceSawReferences(Scene scene)
  {
   foreach(var waves in All<CombatTwoRoomsLab>(scene)){var so=new SerializedObject(waves);var list=so.FindProperty("waves");for(int i=0;i<list.arraySize;i++){var groups=list.GetArrayElementAtIndex(i).FindPropertyRelative("Groups");for(int j=0;j<groups.arraySize;j++){var p=groups.GetArrayElementAtIndex(j).FindPropertyRelative("Prefab");if(AssetDatabase.GetAssetPath(p.objectReferenceValue)=="Assets/IronRequiem/Prefabs/Enemies/SawBot_AI_v01.prefab")p.objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Enemy);}}so.ApplyModifiedPropertiesWithoutUndo();}
  }
  static void IntegrateInterface(Scene scene)
  {
   var player=All<PlayerCharacterController>(scene).Single();var inventory=player.GetComponent<PlayerWeaponsManager>();
   var replacements=new Dictionary<UnityEngine.Object,UnityEngine.Object>();
   for(int i=0;i<inventory.StartingWeapons.Count;i++)
   {
    var old=inventory.StartingWeapons[i];var thermal=ThermalMenuAuthoring.HeatVariant(old);inventory.StartingWeapons[i]=thermal;
    if(old!=thermal){replacements[old]=thermal;replacements[old.gameObject]=thermal.gameObject;}
    var source=PrefabUtility.GetCorrespondingObjectFromSource(thermal.gameObject);if(source&&source.GetComponent<WeaponController>()){replacements[source]=thermal.gameObject;replacements[source.GetComponent<WeaponController>()]=thermal;}
   }
   // Presentation compares prefab identity. Retarget player-only presentation references together with inventory variants.
   foreach(var component in player.GetComponentsInChildren<Component>(true))if(component is MonoBehaviour)
   {
    var data=new SerializedObject(component);var property=data.GetIterator();while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&property.objectReferenceValue&&replacements.TryGetValue(property.objectReferenceValue,out var replacement))property.objectReferenceValue=replacement;data.ApplyModifiedPropertiesWithoutUndo();
   }
   PrefabUtility.RecordPrefabInstancePropertyModifications(inventory);
   ThermalMenuAuthoring.UseFont(All<TMPro.TMP_Text>(scene).First(t=>t.font).font);ThermalMenuAuthoring.AddHeatHud(scene,inventory);
   var labMenu=All<CombatLabMenu>(scene).Single();var menuData=new SerializedObject(labMenu);var main=menuData.FindProperty("mainPanel").objectReferenceValue as GameObject;var death=menuData.FindProperty("deathPanel").objectReferenceValue as GameObject;var canvas=labMenu.GetComponent<Canvas>();
   if(!canvas.transform.Find("IndustrialShell"))
   {
    var play=main.GetComponentInChildren<UnityEngine.UI.Button>(true);var loader=play.GetComponent<LoadSceneButton>()??play.gameObject.AddComponent<LoadSceneButton>();
    ThermalMenuAuthoring.BuildMenu(canvas,null,loader,scene.name);
    labMenu.Configure(player,canvas.transform.Find("IndustrialShell").gameObject,death);
    labMenu.GetComponent<RequiemMenuView>().ConfigureLab(labMenu);
    var nav=labMenu.GetComponent<MenuNavigation>()??labMenu.gameObject.AddComponent<MenuNavigation>();nav.ManageCursorAtStart=false;nav.DefaultSelection=play;
    foreach(var b in death.GetComponentsInChildren<UnityEngine.UI.Button>(true))ThermalMenuAuthoring.StyleButton(b);
    foreach(var label in death.GetComponentsInChildren<TMPro.TMP_Text>(true))label.color=new Color(.88f,.95f,.95f);
   }
   var pause=All<InGameMenuManager>(scene).First(p=>p.gameObject.activeInHierarchy);foreach(var other in All<InGameMenuManager>(scene))if(other!=pause){other.enabled=false;if(other.MenuRoot)other.MenuRoot.SetActive(false);}
   pause.UseIndustrialMenu=true;pause.enabled=false;
   ThermalMenuAuthoring.BuildMenu(pause.MenuRoot.GetComponent<Canvas>(),pause,null,scene.name);
   pause.GetComponent<RequiemMenuView>().ConfigureLab(labMenu);labMenu.ConfigurePause(pause);
   var hint=All<TMPro.TMP_Text>(scene).FirstOrDefault(t=>t.transform.parent.name=="TABImage");if(hint)hint.text="Esc";
   EditorUtility.SetDirty(labMenu);EditorUtility.SetDirty(pause);
  }
  static void BuildVariant()
  {
   if(AssetDatabase.LoadAssetAtPath<GameObject>(Enemy))return;
   for(int i=0;i<3;i++){string path=Art+"/IR_SawBot_Articulated_LOD"+i+".fbx";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.None;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.isReadable=true;importer.bakeAxisConversion=true;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.SaveAndReimport();}
   var preview=EditorSceneManager.NewPreviewScene();var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Enemies/SawBot_AI_v01.prefab"),preview);
   try
   {
    root.name="SawBot_Sustained_v02";var bot=root.GetComponent<SawBotCombat>();var animator=root.GetComponentInChildren<Animator>();var visual=animator.transform;var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);var bones=skins[0].bones;var joints=bones.ToDictionary(t=>t.name);var stats=new StringBuilder();
    for(int level=0;level<3;level++)
    {
     var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/IR_SawBot_Articulated_LOD"+level+".fbx"),preview);try{
      var sources=model.GetComponentsInChildren<MeshFilter>().ToDictionary(f=>f.name.Split('.')[0]);
      if(level==0)foreach(var bone in bones)bone.position=visual.TransformPoint(model.transform.InverseTransformPoint(sources[bone.name].transform.position));
      var vs=new List<Vector3>();var ns=new List<Vector3>();var ts=new List<Vector4>();var uv=new List<Vector2>();var ws=new List<BoneWeight>();var indices=new List<int>();
      for(int b=0;b<bones.Length;b++)
      {
       var f=sources[bones[b].name];var m=f.sharedMesh;var matrix=model.transform.worldToLocalMatrix*f.transform.localToWorldMatrix;int offset=vs.Count;var normals=m.normals;var tangents=m.tangents;var tex=m.uv;var vertices=m.vertices;
       for(int v=0;v<vertices.Length;v++){vs.Add(matrix.MultiplyPoint3x4(vertices[v]));ns.Add(matrix.MultiplyVector(normals[v]).normalized);var tangent=matrix.MultiplyVector(new Vector3(tangents[v].x,tangents[v].y,tangents[v].z));ts.Add(new Vector4(tangent.x,tangent.y,tangent.z,tangents[v].w));uv.Add(tex[v]);ws.Add(new BoneWeight{boneIndex0=b,weight0=1});}indices.AddRange(m.triangles.Select(t=>t+offset));
       if(level==0&&bones[b].name.StartsWith("Wheel")){var box=bones[b].GetComponentInChildren<BoxCollider>();var local=new Bounds();bool first=true;foreach(var v in vertices){var q=box.transform.InverseTransformPoint(visual.TransformPoint(matrix.MultiplyPoint3x4(v)));if(first){local=new Bounds(q,Vector3.zero);first=false;}else local.Encapsulate(q);}box.center=local.center;box.size=local.size;}
      }
      var skin=skins.Single(s=>s.name=="Skin_LOD"+level);var mesh=new Mesh{name="SawBot_Sustain_Skin_LOD"+level,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vs);mesh.SetNormals(ns);mesh.SetTangents(ts);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.boneWeights=ws.ToArray();mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*skin.transform.localToWorldMatrix).ToArray();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Art+"/SawBot_Sustain_Skin_LOD"+level+".asset");skin.sharedMesh=mesh;stats.AppendLine("LOD"+level+" triangles="+indices.Count/3+" bones="+bones.Length);
     }finally{Object.DestroyImmediate(model);}
    }
    var reference=skins.Single(s=>s.name=="Skin_LOD0");var ends=new Transform[4];for(int side=0;side<2;side++){string key=side==0?"L":"R";var chain=joints["Chain_"+key];var points=Points(reference,chain);var center=points.Aggregate(Vector3.zero,(a,b)=>a+b)/points.Length;Vector3 axis=Vector3.up;for(int n=0;n<12;n++){var q=Vector3.zero;foreach(var p in points){var d=p-center;q+=d*Vector3.Dot(d,axis);}axis=q.normalized;}float lo=points.Min(v=>Vector3.Dot(v-center,axis)),hi=points.Max(v=>Vector3.Dot(v-center,axis));for(int e=0;e<2;e++){var t=new GameObject("SawContact"+(e==0?"Base":"Tip")).transform;t.SetParent(chain,false);t.localPosition=center+axis*(e==0?lo*.9f:hi*.9f);ends[side*2+e]=t;}}
    bot.ConfigureSustained(ends[0],ends[1],ends[2],ends[3],.28f);
    var agent=root.GetComponent<NavMeshAgent>();agent.speed=7.2f;agent.acceleration=24;agent.angularSpeed=300;agent.updateRotation=false;agent.stoppingDistance=.55f;var nav=root.GetComponentInChildren<NavigationModule>();nav.MoveSpeed=7.2f;nav.Acceleration=24;nav.AngularSpeed=300;root.GetComponentInChildren<DetectionModule>().AttackRange=3;
    animator.runtimeAnimatorController=BuildController(animator,joints,reference);animator.applyRootMotion=false;root.GetComponent<EnemyMobile>().AttackStopDistanceRatio=1;
    RecordRoot(root);PrefabUtility.SaveAsPrefabAsset(root,Enemy);File.WriteAllText(Reports+"/variant.txt",stats.ToString());
   }finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);}
  }
  static Vector3[] Points(SkinnedMeshRenderer skin,Transform bone)
  {int index=Array.IndexOf(skin.bones,bone);var mesh=skin.sharedMesh;var weights=mesh.boneWeights;return mesh.vertices.Where((v,i)=>weights[i].boneIndex0==index).Select(v=>bone.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();}
  static void RecordRoot(GameObject root){foreach(var t in root.GetComponentsInChildren<Transform>(true)){PrefabUtility.RecordPrefabInstancePropertyModifications(t);foreach(var c in t.GetComponents<Component>())if(c)PrefabUtility.RecordPrefabInstancePropertyModifications(c);}}
  static void Rotation(AnimationClip c,Transform root,Transform bone,float[] times,Quaternion[] values)
  {string path=AnimationUtility.CalculateTransformPath(bone,root);for(int k=0;k<4;k++){int axis=k;var curve=new AnimationCurve(times.Select((t,i)=>new Keyframe(t,values[i][axis])).ToArray());for(int i=0;i<times.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}AnimationUtility.SetEditorCurve(c,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[k]),curve);}c.EnsureQuaternionContinuity();}
  static AnimatorController BuildController(Animator animator,Dictionary<string,Transform> joints,SkinnedMeshRenderer skin)
  {
   string path=Art+"/SawBot_Sustained.controller";AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),path);var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);controller.AddParameter("DriveSpeed",AnimatorControllerParameterType.Float);var parameters=controller.parameters;foreach(var p in parameters)if(p.name=="DriveSpeed")p.defaultFloat=1;controller.parameters=parameters;
   var prepare=new AnimationClip{name="SawBot_ContactPrepare",frameRate=60};var hold=new AnimationClip{name="SawBot_SawHold",frameRate=60};AssetDatabase.CreateAsset(prepare,Art+"/SawBot_ContactPrepare.anim");AssetDatabase.CreateAsset(hold,Art+"/SawBot_SawHold.anim");
   foreach(string side in new[]{"L","R"})
   {
    var upper=joints["UpperArm_"+side];var forearm=joints["Forearm_"+side];var chain=joints["Chain_"+side];var points=Points(skin,chain);var center=points.Aggregate(Vector3.zero,(a,b)=>a+b)/points.Length;var restUpper=upper.localRotation;var restForearm=forearm.localRotation;var end=chain.TransformPoint(center);var a=upper.position;var b=forearm.position;float first=Vector3.Distance(a,b),second=Vector3.Distance(b,end);float sign=Mathf.Sign(a.x);
    var target=animator.transform.parent.TransformPoint(new Vector3(sign*.22f,.95f,.65f));var direction=(target-a).normalized;float distance=Mathf.Min(Vector3.Distance(a,target),first+second-.001f);float x=(first*first-second*second+distance*distance)/(2*distance);float h=Mathf.Sqrt(Mathf.Max(0,first*first-x*x));var pole=Vector3.ProjectOnPlane(new Vector3(sign*.4f,-1,0),direction).normalized;var elbow=a+direction*x+pole*h;
    upper.rotation=Quaternion.FromToRotation(b-a,elbow-a)*upper.rotation;forearm.rotation=Quaternion.FromToRotation(chain.TransformPoint(center)-forearm.position,target-forearm.position)*forearm.rotation;
    var poseUpper=upper.localRotation;var poseForearm=forearm.localRotation;Rotation(prepare,animator.transform,upper,new[]{0f,.25f},new[]{restUpper,poseUpper});Rotation(prepare,animator.transform,forearm,new[]{0f,.25f},new[]{restForearm,poseForearm});
    var times=new[]{0f,.1f,.2f,.3f,.4f};Rotation(hold,animator.transform,upper,times,times.Select(t=>poseUpper*Quaternion.AngleAxis(Mathf.Sin(t*Mathf.PI*10)*.3f,Vector3.right)).ToArray());Rotation(hold,animator.transform,forearm,times,times.Select(t=>poseForearm*Quaternion.AngleAxis(Mathf.Sin(t*Mathf.PI*10+1)*.4f,Vector3.right)).ToArray());upper.localRotation=restUpper;forearm.localRotation=restForearm;
   }
   var settings=AnimationUtility.GetAnimationClipSettings(hold);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(hold,settings);
   var sm=controller.layers[0].stateMachine;var loco=sm.states.Single(s=>s.state.name=="Locomotion").state;loco.speedParameter="DriveSpeed";loco.speedParameterActive=true;var attack=sm.states.Single(s=>s.state.name=="Attack").state;attack.motion=prepare;var loop=sm.AddState("SawHold");loop.motion=hold;
   foreach(var t in attack.transitions)if(t.hasExitTime){t.destinationState=loop;t.exitTime=1;t.duration=.08f;}
   var back=loop.AddTransition(loco);back.AddCondition(AnimatorConditionMode.If,0,"CancelAttack");back.duration=.12f;return controller;
  } }
}
