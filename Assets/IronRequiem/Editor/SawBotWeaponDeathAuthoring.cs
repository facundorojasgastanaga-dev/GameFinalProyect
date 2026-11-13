using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Unity.FPS.Game;
using Unity.FPS.AI;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
 [InitializeOnLoad] public static class SawBotWeaponDeathAuthoring
 {
  public const string Report="Tools/IronRequiem/Reports/SawBotWeaponDeath",Queue="Tools/IronRequiem/Requests/sawbot-weapon-death.txt",Art="Assets/IronRequiem/Art/Enemies/SawBotArsenal",Enemy="Assets/IronRequiem/Prefabs/Enemies/SawBot_Arsenal_v03.prefab",Weapon="Assets/IronRequiem/Prefabs/Weapons/Weapon_SawBot_Stolen.prefab",Pickup="Assets/IronRequiem/Prefabs/Weapons/SawBot_StolenModule.prefab",Lab="Assets/IronRequiem/Scenes/SawBotWeaponDeathLab.unity";
  static readonly DateTime Loaded=DateTime.UtcNow;
  static SawBotWeaponDeathAuthoring(){EditorApplication.update+=Tick;}
  static void Tick(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Queue)||File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/SawBotWeaponDeathAuthoring.cs")>Loaded)return;if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))return;string op=File.ReadAllText(Queue).Trim();File.Delete(Queue);try{if(op=="polish")Polish();else Build();File.WriteAllText(Report+"/build.txt","Complete: heavy floor-fitted Death/DeathContact/DeathDisarmed, existing execution E supplies right saw, energy/OVERDRIVE/throw preserved. Player45DPS,1.65m,12energy/s,100 reserve.");IronRequiemSetupTools.WriteAudit();}catch(Exception e){File.WriteAllText(Report+"/error.txt",e.ToString());Debug.LogException(e);}}
  static T[] All<T>(Scene s)where T:Component=>s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
  static void SaveMesh(Mesh m,string path){var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(m,old);Object.DestroyImmediate(m);}else AssetDatabase.CreateAsset(m,path);}
  static Mesh Subset(Mesh source,Func<int,bool> selected,string name,Func<Vector3,Vector3> position=null,Func<Vector3,Vector3> direction=null,bool keepSkin=true)
  {
   var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};var map=new Dictionary<int,int>();var vs=new List<Vector3>();var ns=new List<Vector3>();var uv=new List<Vector2>();var ts=new List<Vector4>();var ws=new List<BoneWeight>();var triangles=new List<int>();var vertices=source.vertices;var normals=source.normals;var tex=source.uv;var tangents=source.tangents;var weights=source.boneWeights;var input=source.triangles;
   for(int t=0;t<input.Length;t+=3){if(!selected(input[t])||!selected(input[t+1])||!selected(input[t+2]))continue;for(int k=0;k<3;k++){int i=input[t+k];if(!map.TryGetValue(i,out int index)){index=vs.Count;map[i]=index;vs.Add(position==null?vertices[i]:position(vertices[i]));ns.Add(direction==null?normals[i]:direction(normals[i]).normalized);uv.Add(tex[i]);var q=tangents[i];var d=new Vector3(q.x,q.y,q.z);if(direction!=null)d=direction(d);ts.Add(new Vector4(d.x,d.y,d.z,q.w));if(keepSkin)ws.Add(weights[i]);}triangles.Add(index);}}
   mesh.SetVertices(vs);mesh.SetNormals(ns);mesh.SetTangents(ts);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);if(keepSkin){mesh.boneWeights=ws.ToArray();mesh.bindposes=source.bindposes;}mesh.RecalculateBounds();return mesh;
  }
  static void Build()
  {
   Directory.CreateDirectory(Art);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   if(!AssetDatabase.LoadAssetAtPath<GameObject>(Enemy))BuildAssets();
   foreach(var path in new[]{"Assets/IronRequiem/Scenes/BunkerLab.unity","Assets/IronRequiem/Scenes/SawBotLab.unity","Assets/IronRequiem/Scenes/SawBotSustainLab.unity"})
   {
    var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
    try{foreach(var waves in All<CombatTwoRoomsLab>(scene)){var data=new SerializedObject(waves);var list=data.FindProperty("waves");for(int i=0;i<list.arraySize;i++){var groups=list.GetArrayElementAtIndex(i).FindPropertyRelative("Groups");for(int g=0;g<groups.arraySize;g++){var p=groups.GetArrayElementAtIndex(g).FindPropertyRelative("Prefab");if(AssetDatabase.GetAssetPath(p.objectReferenceValue)==SawBotSustainAuthoring.Enemy)p.objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Enemy);}}data.ApplyModifiedPropertiesWithoutUndo();}EditorSceneManager.SaveScene(scene);if(path.EndsWith("SawBotLab.unity"))EditorSceneManager.SaveScene(scene,Lab,true);}finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
   }
   var settings=EditorBuildSettings.scenes.ToList();if(!settings.Any(s=>s.path==Lab))settings.Add(new EditorBuildSettingsScene(Lab,true));EditorBuildSettings.scenes=settings.ToArray();AssetDatabase.SaveAssets();
  }
  static void BuildAssets()
  {
   var preview=EditorSceneManager.NewPreviewScene();var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SawBotSustainAuthoring.Enemy),preview);
   try
   {
    root.name="SawBot_Arsenal_v03";var animator=root.GetComponentInChildren<Animator>();var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(s=>s.name).ToArray();var bones=skins[0].bones;var indices=bones.Select((b,i)=>new{b.name,index=i}).ToDictionary(b=>b.name,b=>b.index);var wanted=new HashSet<int>(new[]{indices["Forearm_R"],indices["GuideBar_R"],indices["Chain_R"]});
    var forearm=bones[indices["Forearm_R"]];var reference=skins[0];var sourceVertices=reference.sharedMesh.vertices;var sourceWeights=reference.sharedMesh.boneWeights;var chainIndices=Enumerable.Range(0,sourceVertices.Length).Where(i=>sourceWeights[i].boneIndex0==indices["Chain_R"]).ToArray();var center=chainIndices.Select(i=>reference.transform.TransformPoint(sourceVertices[i])).Aggregate(Vector3.zero,(a,b)=>a+b)/chainIndices.Length;var forward=(center-forearm.position).normalized;var right=Vector3.ProjectOnPlane(reference.transform.forward,forward).normalized;var rotation=Quaternion.LookRotation(forward,Vector3.Cross(forward,right).normalized);var inverse=Quaternion.Inverse(rotation);var origin=forearm.position;
    var staticMeshes=new Mesh[3];var sawViews=new SkinnedMeshRenderer[3];
    for(int level=0;level<3;level++)
    {
     var skin=skins.Single(s=>s.name=="Skin_LOD"+level);var source=skin.sharedMesh;var weights=source.boneWeights;
     SaveMesh(Subset(source,i=>wanted.Contains(weights[i].boneIndex0),"SawModule_LOD"+level,v=>inverse*(skin.transform.TransformPoint(v)-origin),v=>inverse*skin.transform.TransformDirection(v),false),Art+"/SawModule_LOD"+level+".asset");staticMeshes[level]=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/SawModule_LOD"+level+".asset");
     SaveMesh(Subset(source,i=>!wanted.Contains(weights[i].boneIndex0),"SawBody_LOD"+level),Art+"/SawBody_LOD"+level+".asset");SaveMesh(Subset(source,i=>wanted.Contains(weights[i].boneIndex0),"SawAttached_LOD"+level),Art+"/SawAttached_LOD"+level+".asset");skin.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/SawBody_LOD"+level+".asset");
     var view=new GameObject("RightSaw_LOD"+level).AddComponent<SkinnedMeshRenderer>();view.transform.SetParent(skin.transform.parent,false);view.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/SawAttached_LOD"+level+".asset");view.sharedMaterials=skin.sharedMaterials;view.bones=skin.bones;view.rootBone=skin.rootBone;view.localBounds=skin.localBounds;sawViews[level]=view;
    }
    var lod=root.GetComponent<LODGroup>();var lods=lod.GetLODs();for(int i=0;i<3;i++){var views=lods[i].renderers.ToList();views.Add(sawViews[i]);lods[i].renderers=views.ToArray();}lod.SetLODs(lods);
    var feedback=root.GetComponent<RobotHitFeedback>();if(feedback){var data=new SerializedObject(feedback);var array=data.FindProperty("body");array.arraySize=6;for(int i=0;i<3;i++){array.GetArrayElementAtIndex(i).objectReferenceValue=skins[i];array.GetArrayElementAtIndex(i+3).objectReferenceValue=sawViews[i];}data.ApplyModifiedPropertiesWithoutUndo();}
    BuildWeapon(staticMeshes,skins[0].sharedMaterial,feedback.Sparks,preview);BuildPickup(staticMeshes,skins[0].sharedMaterial,preview);
    var execution=root.AddComponent<EnemyExecutionSource>();execution.Configure(AssetDatabase.LoadAssetAtPath<GameObject>(Pickup).GetComponent<StolenCannon>());root.AddComponent<SawBotWeaponSource>().Configure(sawViews,forearm.GetComponentsInChildren<Collider>(true));
    BuildDeaths(animator,root,skins.Concat(sawViews).ToArray(),wanted);
    var combatData=new SerializedObject(root.GetComponent<SawBotCombat>());combatData.FindProperty("contactDeathState").stringValue="DeathContact";combatData.FindProperty("disarmedDeathState").stringValue="DeathDisarmed";combatData.FindProperty("deathBurstDelay").floatValue=1.35f;combatData.ApplyModifiedPropertiesWithoutUndo();
    foreach(var t in root.GetComponentsInChildren<Transform>(true))foreach(var c in t.GetComponents<Component>())if(c&&PrefabUtility.IsPartOfPrefabInstance(c))PrefabUtility.RecordPrefabInstancePropertyModifications(c);
    PrefabUtility.SaveAsPrefabAsset(root,Enemy);
   }finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);}
  }
  static Renderer[] ModuleViews(Transform parent,Mesh[] meshes,Material material,float scale)
  {
   var views=new Renderer[3];for(int i=0;i<3;i++){var go=new GameObject("Saw_LOD"+i,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localScale=Vector3.one*scale;go.GetComponent<MeshFilter>().sharedMesh=meshes[i];go.GetComponent<MeshRenderer>().sharedMaterial=material;views[i]=go.GetComponent<Renderer>();}
   var lod=parent.gameObject.AddComponent<LODGroup>();lod.SetLODs(Enumerable.Range(0,3).Select(i=>new LOD(new[]{.25f,.09f,.01f}[i],new[]{views[i]})).ToArray());lod.RecalculateBounds();return views;
  }
  static void BuildWeapon(Mesh[] meshes,Material material,ParticleSystem sparks,Scene scene)
  {
   var root=new GameObject("Weapon_SawBot_Stolen");SceneManager.MoveGameObjectToScene(root,scene);try{
    var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab").GetComponent<WeaponController>();var weapon=root.AddComponent<WeaponController>();EditorUtility.CopySerialized(source,weapon);weapon.WeaponName="MOTOSIERRA";weapon.ShootType=WeaponShootType.Automatic;weapon.RecoilForce=.012f;weapon.MaxAmmo=100;weapon.AutomaticReload=false;weapon.HasPhysicalBullets=false;weapon.WeaponAnimator=null;weapon.MuzzleFlashPrefab=null;weapon.UseContinuousShootSound=false;weapon.ShootSfx=null;weapon.ChangeWeaponSfx=source.ChangeWeaponSfx;weapon.AimOffset=new Vector3(-.06f,.025f,0);weapon.AimZoomRatio=.96f;weapon.DelayBetweenShots=.1f;
    var content=new GameObject("Content").transform;content.SetParent(root.transform,false);weapon.WeaponRoot=content.gameObject;var visual=new GameObject("SawMotorVisual").transform;visual.SetParent(content,false);visual.localPosition=new Vector3(0,-.035f,.015f);var views=ModuleViews(visual,meshes,material,.85f);
    var attachment=new GameObject("Attachment").transform;attachment.SetParent(visual,false);var muzzle=new GameObject("WeaponMuzzle").transform;muzzle.SetParent(visual,false);muzzle.localPosition=new Vector3(0,0,meshes[0].bounds.max.z*.85f);weapon.WeaponMuzzle=muzzle;
    var ps=Object.Instantiate(sparks.gameObject,content).GetComponent<ParticleSystem>();ps.name="SawContactSparks";ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.playOnAwake=false;main.loop=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=64;
    var attack=root.AddComponent<StolenSawAttack>();attack.Configure(visual,ps,AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FPS/Audio/SFX/Enemies/Hoverbot_Movement.wav"));var audio=root.GetComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=0;audio.outputAudioMixerGroup=AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.WeaponShoot);PrefabUtility.SaveAsPrefabAsset(root,Weapon);
   }finally{Object.DestroyImmediate(root);}
  }
  static void BuildPickup(Mesh[] meshes,Material material,Scene scene)
  {
   var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Weapons/BigBot_BurstPickup_v10.prefab");var root=new GameObject("SawBot_StolenModule");SceneManager.MoveGameObjectToScene(root,scene);try{
    var module=root.AddComponent<StolenCannon>();module.Configure(AssetDatabase.LoadAssetAtPath<GameObject>(Weapon).GetComponent<WeaponController>());var model=new GameObject("WorldSaw").transform;model.SetParent(root.transform,false);ModuleViews(model,meshes,material,1);
    var data=new SerializedObject(root.GetComponent<CannonAmmo>());data.FindProperty("capacity").floatValue=100;data.ApplyModifiedPropertiesWithoutUndo();var overdrive=root.GetComponent<CannonOverdrive>();EditorUtility.CopySerialized(source.GetComponent<CannonOverdrive>(),overdrive);var smoke=Object.Instantiate(source.GetComponent<CannonOverdrive>().Smoke.gameObject,root.transform).GetComponent<ParticleSystem>();smoke.name="CriticalSmoke";smoke.transform.localPosition=Vector3.zero;smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);overdrive.ConfigureSmoke(smoke);
    EditorUtility.CopySerialized(source.GetComponent<CannonExplosion>(),root.GetComponent<CannonExplosion>());EditorUtility.CopySerialized(source.GetComponent<DamageArea>(),root.GetComponent<DamageArea>());var body=root.AddComponent<Rigidbody>();body.mass=3.5f;body.isKinematic=true;body.useGravity=true;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;var collider=root.AddComponent<BoxCollider>();collider.center=meshes[0].bounds.center;collider.size=Vector3.Max(meshes[0].bounds.size,Vector3.one*.06f);var audio=root.GetComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=1;audio.outputAudioMixerGroup=AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.WeaponOverheat);PrefabUtility.SaveAsPrefabAsset(root,Pickup);
   }finally{Object.DestroyImmediate(root);}
  }
  static void Rot(AnimationClip clip,Transform root,Transform target,float[] times,Quaternion[] q)
  {string path=AnimationUtility.CalculateTransformPath(target,root);for(int k=0;k<4;k++){int axis=k;var curve=new AnimationCurve(times.Select((t,i)=>new Keyframe(t,q[i][axis])).ToArray());for(int i=0;i<times.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[k]),curve);}clip.EnsureQuaternionContinuity();}
  static void Curve(AnimationClip clip,Transform root,Transform target,string property,float[] times,float[] values)
  {var curve=new AnimationCurve(times.Select((t,i)=>new Keyframe(t,values[i])).ToArray());for(int i=0;i<times.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(target,root),typeof(Transform),property),curve);}
  static void BuildDeaths(Animator animator,GameObject root,SkinnedMeshRenderer[] skins,HashSet<int> removed)
  {
   string path=Art+"/SawBot_Arsenal.controller";if(!AssetDatabase.LoadAssetAtPath<AnimatorController>(path))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),path);var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);var states=controller.layers[0].stateMachine;var bones=skins[0].bones;var rig=bones[0].parent;var originalPosition=bones.Select(b=>b.localPosition).ToArray();var originalRotation=bones.Select(b=>b.localRotation).ToArray();var rigPosition=rig.localPosition;var rigRotation=rig.localRotation;
   var hold=controller.animationClips.Single(c=>c.name=="SawBot_SawHold");hold.SampleAnimation(animator.gameObject,.2f);var contactRotation=bones.Select(b=>b.localRotation).ToArray();for(int i=0;i<bones.Length;i++){bones[i].localRotation=originalRotation[i];bones[i].localPosition=originalPosition[i];}
   float[] times={0,.12f,.28f,.45f,.7f,1f,1.35f,1.75f,2.1f};float[] pitch={0,3,-5,0,-12,-28,-55,-72,-76};float[] roll={0,3,-3,5,12,14,10,8,8};
   foreach(string name in new[]{"Death","DeathContact","DeathDisarmed"})
   {
    var clip=new AnimationClip{name="SawBot_"+name+"_Heavy",frameRate=60};string clipPath=Art+"/"+clip.name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);if(old){EditorUtility.CopySerialized(clip,old);Object.DestroyImmediate(clip);clip=old;}else AssetDatabase.CreateAsset(clip,clipPath);Rot(clip,animator.transform,rig,times,times.Select((t,i)=>Quaternion.Euler(pitch[i],0,roll[i])).ToArray());
    int body=Array.FindIndex(bones,b=>b.name=="Body");Rot(clip,animator.transform,bones[body],times,new[]{0f,-3,2,-6,8,14,8,5,5}.Select(x=>Quaternion.Euler(x,0,0)).ToArray());Curve(clip,animator.transform,bones[body],"m_LocalPosition.y",times,new[]{0f,.006f,-.01f,-.03f,-.05f,-.07f,-.07f,-.08f,-.08f}.Select(x=>originalPosition[body].y+x).ToArray());
    for(int b=0;b<bones.Length;b++)if(bones[b].name.StartsWith("UpperArm_")||bones[b].name.StartsWith("Forearm_")){bool upper=bones[b].name.StartsWith("Upper");float sign=bones[b].name.EndsWith("L")?-1:1;var begin=name=="DeathContact"?contactRotation[b]:originalRotation[b];var end=Quaternion.Euler(upper?-15:-20,0,upper?sign*12:0);Rot(clip,animator.transform,bones[b],times,times.Select(t=>Quaternion.Slerp(begin,end,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,1.35f,t)))).ToArray());}
    var localPoints=new List<Vector3>();var boneIndices=new List<int>();foreach(var skin in skins){if(name=="DeathDisarmed"&&skin.name.StartsWith("RightSaw"))continue;var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;var binds=mesh.bindposes;for(int i=0;i<vertices.Length;i++){localPoints.Add(binds[weights[i].boneIndex0].MultiplyPoint3x4(vertices[i]));boneIndices.Add(weights[i].boneIndex0);}}
    var frames=Enumerable.Range(0,127).Select(i=>Mathf.Min(2.1f,i/60f)).ToArray();var heights=new float[frames.Length];for(int f=0;f<frames.Length;f++){rig.localPosition=rigPosition;clip.SampleAnimation(animator.gameObject,frames[f]);var matrices=bones.Select(b=>root.transform.worldToLocalMatrix*b.localToWorldMatrix).ToArray();float min=float.PositiveInfinity;for(int i=0;i<localPoints.Count;i++)min=Mathf.Min(min,matrices[boneIndices[i]].MultiplyPoint3x4(localPoints[i]).y);heights[f]=rigPosition.y+(.018f-min)/animator.transform.lossyScale.y;}
    Curve(clip,animator.transform,rig,"m_LocalPosition.y",frames,heights);var state=states.states.FirstOrDefault(s=>s.state.name==name).state;if(!state)state=states.AddState(name);state.motion=clip;
    rig.localPosition=rigPosition;rig.localRotation=rigRotation;for(int i=0;i<bones.Length;i++){bones[i].localRotation=originalRotation[i];bones[i].localPosition=originalPosition[i];}
   }
   animator.runtimeAnimatorController=controller;
  }
  static void Polish()
  {
   var root=PrefabUtility.LoadPrefabContents(Enemy);try{var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);var animator=root.GetComponentInChildren<Animator>();BuildDeaths(animator,root,skins,new HashSet<int>());PrefabUtility.SaveAsPrefabAsset(root,Enemy);}finally{PrefabUtility.UnloadPrefabContents(root);}
   root=PrefabUtility.LoadPrefabContents(Weapon);try{var visual=root.GetComponent<StolenSawAttack>().transform.Find("Content/SawMotorVisual");visual.localPosition=new Vector3(-.03f,.10f,.025f);visual.localRotation=Quaternion.Euler(0,-10,-6);PrefabUtility.SaveAsPrefabAsset(root,Weapon);}finally{PrefabUtility.UnloadPrefabContents(root);}AssetDatabase.SaveAssets();
  } }
}
