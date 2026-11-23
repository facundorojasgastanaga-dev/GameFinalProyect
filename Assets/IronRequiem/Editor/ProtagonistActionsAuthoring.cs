using System;
using System.IO;
using System.Linq;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
    [InitializeOnLoad] public static class ProtagonistActionsAuthoring
    {
        public const string Art="Assets/IronRequiem/Art/Player/ProtagonistActions",Lab="Assets/IronRequiem/Scenes/ProtagonistActionsLab.unity",Player="Assets/IronRequiem/Prefabs/Player/Player_IR_Protagonist_Actions_v02.prefab",Reports="Tools/IronRequiem/Reports/ProtagonistActions";
        const string Request="Tools/IronRequiem/Requests/protagonist-actions.txt",Key="IRON.ProtagonistActions";
        static TestRunnerApi api;static readonly DateTime LoadedAt=DateTime.UtcNow;
        static ProtagonistActionsAuthoring(){EditorApplication.update+=Tick;if(SessionState.GetString(Key,"")=="test")EditorApplication.delayCall+=Register;}
        static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(SessionState.GetString(Key,"")=="restore"&&EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".time",0)>6){SessionState.EraseString(Key);EditorSceneManager.OpenScene(Lab);IronRequiemSetupTools.WriteAudit();}
            if(!File.Exists(Request)||LoadedAt<File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/ProtagonistActionsAuthoring.cs"))return;
            string op=File.ReadAllText(Request).Trim();if((op=="test"||op=="test-slide")&&LoadedAt<File.GetLastWriteTimeUtc("Assets/IronRequiem/Tests/PlayMode/ProtagonistActionsTests.cs"))return;File.Delete(Request);Directory.CreateDirectory(Reports);
            try
            {
                if(op=="build")Build();
                else if(op=="test"||op=="test-slide"){T07AnimationBuilder.RequireClean();if(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetType("IronRequiem.Tests.ProtagonistActionsTests")!=null))throw new InvalidOperationException("Action test assembly not loaded");Register();SessionState.SetString(Key,"test");SessionState.SetString(Key+".reports",op=="test-slide"?Reports+"/SlideFacing":Reports);api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=op=="test-slide"?new[]{"IronRequiem.Tests.ProtagonistActionsTests.DownwardLookKeepsLegPoseAnchoredAndVisible","IronRequiem.Tests.ProtagonistActionsTests.SlideDirectionStaysFixedAndTorsoCannotTurnBehind","IronRequiem.Tests.ProtagonistActionsTests.SlideShowsOnlyLegsAndContactSparksThenClearsOnJump","IronRequiem.Tests.ProtagonistActionsTests.SlopeContactsFollowSurfaceNormalAndSlideEndsCleanly"}:new[]{"IronRequiem.Tests.ProtagonistActionsTests"}}));}
                else throw new InvalidOperationException("Unknown fixed protagonist action operation");
            }
            catch(Exception e){File.WriteAllText(Reports+"/error.txt",e.ToString());Debug.LogException(e);}
        }
        static void Build()
        {
            T07AnimationBuilder.RequireClean();if(File.Exists(Player)||File.Exists(Lab))throw new InvalidOperationException("Action draft already exists; preserving assets");
            UpgradeBindings();Import(Art+"/IR_ActionHandPoses.fbx",true);Import(Art+"/IR_SlideLegs.fbx",false);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistLabBuilder.PlayerPath));go.name="Player_IR_Protagonist_Actions_v02";
            try
            {
                var hands=go.GetComponent<ProtagonistHandsPresentation>();var fps=hands.FpsRoot;var attack=go.AddComponent<ProtagonistMelee>();
                var wrist=new GameObject("LeftPunchWrist").transform;wrist.SetParent(fps,false);var elbow=new GameObject("LeftPunchElbow").transform;elbow.SetParent(fps,false);
                var clip=CreatePunchClip(fps);hands.ConfigureActions(attack,clip,wrist,elbow,fps.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Hand_L_FPS"||r.name=="Forearm_L_FPS").ToArray());
                BuildLegs(go);var old=go.GetComponent<SlideContactEffects>().ContactParticles;if(old)old.gameObject.SetActive(false);
                go.GetComponent<SlideContactEffects>().ConfigureContacts(Sparks(go.transform,"SlideMetalSparks_L"),Sparks(go.transform,"SlideMetalSparks_R"));
                PrefabUtility.SaveAsPrefabAsset(go,Player);
            }
            finally{Object.DestroyImmediate(go);}
            if(!AssetDatabase.CopyAsset(ProtagonistLabBuilder.Lab,Lab))throw new InvalidOperationException("Cannot create separate action lab");var scene=EditorSceneManager.OpenScene(Lab);
            var actor=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerCharacterController>(true)).Single();var before=actor.transform.position;
            PrefabUtility.ReplacePrefabAssetOfPrefabInstance(actor.gameObject,AssetDatabase.LoadAssetAtPath<GameObject>(Player),new PrefabReplacingSettings{objectMatchMode=ObjectMatchMode.ByHierarchy,prefabOverridesOptions=PrefabOverridesOptions.KeepAllPossibleOverrides,changeRootNameToAssetName=false},InteractionMode.AutomatedAction);
            actor=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerCharacterController>(true)).Single();
            if(actor.transform.position!=before||!actor.GetComponent<ProtagonistMelee>())throw new InvalidOperationException("Player replacement invalid");
            var effects=actor.GetComponent<SlideContactEffects>();effects.ConfigureContacts(actor.transform.Find("SlideMetalSparks_L").GetComponent<ParticleSystem>(),actor.transform.Find("SlideMetalSparks_R").GetComponent<ParticleSystem>());PrefabUtility.RecordPrefabInstancePropertyModifications(effects);
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Cannot save action lab");AssetDatabase.SaveAssets();IronRequiemSetupTools.WriteAudit();File.WriteAllText(Reports+"/build.txt","Action variant/lab prepared. E pickup, F melee; legacy E NextWeapon binding removed, Q/wheel/digits retained. Original scenes and world/enemy scale preserved.");
        }
        static void UpgradeBindings()
        {
            const string path="Assets/FPS/InputSystem_Actions.inputactions";var source=AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);var asset=InputActionAsset.FromJson(source.ToJson());
            var map=asset.FindActionMap("Player",true);var collect=map.FindAction("CollectWeapon",true);
            for(int i=0;i<collect.bindings.Count;i++)if(collect.bindings[i].path=="<Keyboard>/f")collect.ChangeBinding(i).WithPath("<Keyboard>/e");
            var cycle=map.FindAction("NextWeapon");if(cycle!=null)for(int i=cycle.bindings.Count-1;i>=0;i--)if(cycle.bindings[i].path=="<Keyboard>/e")cycle.ChangeBinding(i).Erase(); // v8: NextWeapon was split into WeaponNext/WeaponPrevious
            var punch=map.FindAction("Melee");if(punch==null){punch=map.AddAction("Melee",InputActionType.Button);punch.expectedControlType="Button";punch.AddBinding("<Keyboard>/f",groups:"Keyboard&Mouse");}
            File.WriteAllText(path,asset.ToJson());Object.DestroyImmediate(asset);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        }
        static void Import(string path,bool animate){var i=(ModelImporter)AssetImporter.GetAtPath(path);i.materialImportMode=ModelImporterMaterialImportMode.None;i.animationType=ModelImporterAnimationType.Generic;i.importAnimation=animate;i.optimizeGameObjects=false;i.animationCompression=ModelImporterAnimationCompression.Off;i.SaveAndReimport();}
        static AnimationClip CreatePunchClip(Transform fps)
        {
            var preview=EditorSceneManager.NewPreviewScene();var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/IR_ActionHandPoses.fbx"),preview);
            try
            {
                var source=AssetDatabase.LoadAllAssetsAtPath(Art+"/IR_ActionHandPoses.fbx").OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__")&&c.name.EndsWith("IR_LeftFistClosed"));
                source.SampleAnimation(model,.2f);var clip=new AnimationClip{name="LeftPunch_Articulated",frameRate=60};
                foreach(var target in fps.GetComponentsInChildren<Transform>(true).Where(t=>t.name.EndsWith("_L")&&new[]{"Index","Middle","Ring","Little","Thumb"}.Any(prefix=>t.name.StartsWith(prefix))))
                {
                    var closed=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name==target.name).localRotation;var rest=target.localRotation;var path=AnimationUtility.CalculateTransformPath(target,fps);
                    for(int axis=0;axis<4;axis++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[axis]),new AnimationCurve(new Keyframe(0,rest[axis]),new Keyframe(.065f,closed[axis]),new Keyframe(.46f,closed[axis]),new Keyframe(.56f,rest[axis])));
                }
                Vector3[] positions={new Vector3(-.25f,-.42f,.3f),new Vector3(-.30f,-.31f,.29f),new Vector3(-.03f,-.05f,.62f),new Vector3(-.03f,-.05f,.62f),new Vector3(-.25f,-.42f,.3f)};float[] times={0,.12f,.22f,.26f,.56f};
                for(int axis=0;axis<3;axis++)
                {
                    var keys=times.Select((t,index)=>new Keyframe(t,positions[index][axis])).ToArray();AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("LeftPunchWrist",typeof(Transform),"m_LocalPosition."+"xyz"[axis]),new AnimationCurve(keys));
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("LeftPunchElbow",typeof(Transform),"m_LocalPosition."+"xyz"[axis]),AnimationCurve.Constant(0,.56f,new Vector3(-.4f,-.75f,.02f)[axis]));
                }
                clip.EnsureQuaternionContinuity();AssetDatabase.CreateAsset(clip,Art+"/LeftPunch_Articulated.anim");return clip;
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
        }
        static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        static void BuildLegs(GameObject player)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/IR_SlideLegs.fbx"));go.name="SlideLegs_FPS";go.transform.SetParent(player.GetComponent<PlayerWeaponsManager>().WeaponCamera.transform,false);
            var animator=go.GetComponent<Animator>();if(animator)animator.enabled=false;int layer=player.GetComponent<PlayerWeaponsManager>().FpsWeaponLayer.value;int index=0;while(index<31&&(layer&(1<<index))==0)index++;
            foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=index;
            foreach(var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>()){renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(ProtagonistIntegrationTools.Art+"/Materials/IR_Protagonist_URP.mat");renderer.updateWhenOffscreen=true;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}
            player.AddComponent<ProtagonistSlidePresentation>().Configure(go.transform,Find(go.transform,"Pelvis"),Find(go.transform,"Thigh_L"),Find(go.transform,"Thigh_R"),Find(go.transform,"Shin_L"),Find(go.transform,"Shin_R"),Find(go.transform,"Foot_L"),Find(go.transform,"Foot_R"));go.SetActive(false);
        }
        static ParticleSystem Sparks(Transform root,string name)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var m=ps.main;m.playOnAwake=false;m.loop=true;m.maxParticles=24;m.simulationSpace=ParticleSystemSimulationSpace.World;m.startLifetime=new ParticleSystem.MinMaxCurve(.08f,.18f);m.startSpeed=new ParticleSystem.MinMaxCurve(1.8f,3.6f);m.startSize=new ParticleSystem.MinMaxCurve(.006f,.014f);m.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.45f,.02f,1),new Color(1,.85f,.15f,1));m.gravityModifier=.9f;
            var e=ps.emission;e.rateOverTime=55;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=18;shape.radius=.025f;
            var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.8f,.05f),0),new GradientColorKey(new Color(1,.25f,0),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=gradient;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/VFX/Mechanics/MechanicParticles.mat");renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=1.4f;renderer.velocityScale=.025f;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return ps;
        }
        sealed class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}
            public void RunFinished(ITestResultAdaptor result){if(SessionState.GetString(Key,"")!="test")return;var destination=SessionState.GetString(Key+".reports",Reports);Directory.CreateDirectory(destination);TestRunnerApi.SaveResultToFile(result,destination+"/playmode.xml");File.WriteAllText(destination+"/test-status.txt",result.ResultState+" passed="+result.PassCount+" failed="+result.FailCount);SessionState.SetFloat(Key+".time",(float)EditorApplication.timeSinceStartup);SessionState.SetString(Key,"restore");}
        }
    }
}
