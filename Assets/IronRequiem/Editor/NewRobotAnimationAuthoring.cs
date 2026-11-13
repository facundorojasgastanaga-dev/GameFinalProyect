using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class NewRobotAnimationAuthoring
    {
        public const string Art="Assets/IronRequiem/Art/Robots/NewRobotAnimated",Prefab="Assets/IronRequiem/Prefabs/Robots/NewRobot_Animated_v01.prefab",Lab="Assets/IronRequiem/Scenes/NewRobotAnimationLab.unity",Reports="Tools/IronRequiem/Reports/NewRobotAnimation";
        const string Request="Tools/IronRequiem/Requests/new-robot-animation.txt",Key="IRON.NewRobotAnimation";
        const float WalkSpeed=1.0106414f;
        static readonly DateTime LoadedAt=DateTime.UtcNow;
        static TestRunnerApi runner;
        static NewRobotAnimationAuthoring(){EditorApplication.update+=Tick;if(SessionState.GetBool(Key+".testing",false))EditorApplication.delayCall+=Register;}
        static void Register(){if(!runner){runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results());}}
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(SessionState.GetBool(Key+".restore",false)&&EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".finished",0)>5){SessionState.EraseBool(Key+".restore");if(!AnyDirty())EditorSceneManager.OpenScene(Lab);}
            if(!File.Exists(Request)||LoadedAt<File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/NewRobotAnimationAuthoring.cs"))return;
            string op=File.ReadAllText(Request).Trim();if(op=="test"&&LoadedAt<File.GetLastWriteTimeUtc("Assets/IronRequiem/Tests/PlayMode/NewRobotAnimationTests.cs"))return;
            File.Delete(Request);Directory.CreateDirectory(Reports);
            try
            {
                if(op=="build")Build();
                else if(op=="revise"){RevisePresentation();File.WriteAllText(Request,"test");}
                else if(op=="test")
                {
                    if(AnyDirty())throw new InvalidOperationException("Preserving user dirty scene; cannot enter Test Runner.");
                    AssetDatabase.ImportAsset(Art+"/IR_NewRobot_Animated.fbx",ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                    if(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetType("IronRequiem.Tests.NewRobotAnimationTests")!=null))throw new InvalidOperationException("Specific animation tests not loaded.");
                    Register();SessionState.SetBool(Key+".testing",true);runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{"IronRequiem.Tests.NewRobotAnimationTests"}}));
                }
                else throw new InvalidOperationException("Unknown fixed robot animation operation");
            }
            catch(Exception e){File.WriteAllText(Reports+"/error.txt",e.ToString());Debug.LogException(e);}
        }
        static bool AnyDirty(){for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)return true;return false;}
        static AnimationClip Clip(string name)=>AssetDatabase.LoadAllAssetsAtPath(Art+"/IR_NewRobot_Animated.fbx").OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__")&&c.name.EndsWith(name));
        static void Build()
        {
            if(File.Exists(Prefab)||File.Exists(Lab))throw new InvalidOperationException("Preserving existing robot delivery.");
            var importer=(ModelImporter)AssetImporter.GetAtPath(Art+"/IR_NewRobot_Animated.fbx");importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;importer.optimizeGameObjects=false;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.addCollider=false;
            var clips=importer.defaultClipAnimations;if(clips.Length!=7)throw new InvalidOperationException("Seven authored clips required: "+clips.Length);
            foreach(var clip in clips)
            {
                clip.name=clip.name.Split('|').Last();clip.loopTime=clip.name.Contains("Idle")||clip.name.Contains("Walk")||clip.name.Contains("Turn");clip.lockRootRotation=true;clip.lockRootHeightY=true;clip.lockRootPositionXZ=true;
                if(clip.name=="IRN_Fire")clip.events=new[]{new AnimationEvent{functionName="OnPresentationShot",time=.02f}};
            }
            importer.clipAnimations=clips;importer.SaveAndReimport();
            foreach(string p in AssetDatabase.FindAssets("t:Texture2D",new[]{Art+"/Textures"}).Select(AssetDatabase.GUIDToAssetPath))
            {var i=(TextureImporter)AssetImporter.GetAtPath(p);i.textureType=p.Contains("Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;i.sRGBTexture=p.Contains("BaseColor");i.maxTextureSize=2048;i.SaveAndReimport();}
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="NewRobot_URP"};material.SetTexture("_BaseMap",Texture("BaseColor"));material.SetTexture("_BumpMap",Texture("Normal"));material.SetTexture("_MetallicGlossMap",Texture("MetalSmooth"));material.EnableKeyword("_NORMALMAP");material.EnableKeyword("_METALLICSPECGLOSSMAP");material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",1);AssetDatabase.CreateAsset(material,Art+"/NewRobot_URP.mat");
            var root=new GameObject("NewRobot_Animated_v01");
            try
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/IR_NewRobot_Animated.fbx"));PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);model.name="Presentation";model.transform.SetParent(root.transform,false);
                foreach(var old in model.GetComponentsInChildren<LODGroup>(true))Object.DestroyImmediate(old);
                var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);if(skins.Length!=45)throw new InvalidOperationException("Expected 45 rigid skin pieces, got "+skins.Length);
                var restBounds=StaticBounds(skins.Where(s=>s.name.EndsWith("_LOD0")));
                foreach(var skin in skins){skin.sharedMaterial=material;skin.enabled=true;skin.gameObject.SetActive(true);skin.updateWhenOffscreen=false;FitSkinBounds(skin);}
                var animator=model.GetComponent<Animator>();if(!animator)animator=model.AddComponent<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.runtimeAnimatorController=Controller(model.transform);
                var muzzle=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="WeaponMuzzle_R");model.AddComponent<NewRobotAnimationPresentation>().Configure(animator,muzzle,WalkSpeed);
                var bounds=skins.First(s=>s.name.EndsWith("_LOD0")).bounds;foreach(var r in skins.Where(s=>s.name.EndsWith("_LOD0")))bounds.Encapsulate(r.bounds);
                model.transform.localPosition=Vector3.up*(-restBounds.min.y);root.transform.localScale=Vector3.one*(3/restBounds.size.y);
                var group=root.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;var lods=new LOD[3];for(int i=0;i<3;i++){var rs=skins.Where(s=>s.name.EndsWith("_LOD"+i)).Cast<Renderer>().ToArray();if(rs.Length!=15)throw new InvalidOperationException("15 corresponding parts required per LOD");lods[i]=new LOD(new[]{.55f,.25f,.025f}[i],rs);}group.SetLODs(lods);group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            }
            finally{Object.DestroyImmediate(root);}
            BuildLab(material);AssetDatabase.SaveAssets();File.WriteAllText(Reports+"/build-status.txt","Built independent 3 m rigid robot, 3 shared-skeleton LODs, seven clips and visual shot only. No AI/damage/projectile or original-scene edits.");
        }
        static Bounds StaticBounds(System.Collections.Generic.IEnumerable<SkinnedMeshRenderer> skins)
        {
            Bounds bounds=default;bool first=true;
            foreach(var r in skins)
            {
                var b=r.sharedMesh.bounds;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    var p=r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));
                    if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }
        static void FitSkinBounds(SkinnedMeshRenderer skin)
        {
            // Imported mesh transforms and the shared skeleton use different local frames.
            var mesh=skin.sharedMesh.bounds;var space=skin.rootBone?skin.rootBone:skin.transform;Bounds b=default;bool first=true;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                var p=space.InverseTransformPoint(skin.transform.TransformPoint(mesh.center+Vector3.Scale(mesh.extents,new Vector3(x,y,z))));
                if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);
            }
            var s=space.lossyScale;b.Expand(new Vector3(.22f/Mathf.Abs(s.x),.22f/Mathf.Abs(s.y),.22f/Mathf.Abs(s.z)));skin.localBounds=b;
        }
        static void ConfigureMask(AvatarMask mask,Transform model)
        {
            var transforms=model.GetComponentsInChildren<Transform>(true);mask.transformCount=transforms.Length;
            for(int i=0;i<transforms.Length;i++){var path=AnimationUtility.CalculateTransformPath(transforms[i],model);mask.SetTransformPath(i,path);mask.SetTransformActive(i,path.Split('/').Contains("Torso"));}
        }
        static void RevisePresentation()
        {
            bool cleanLab=SceneManager.GetActiveScene().path==Lab&&!AnyDirty();var go=PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                go.transform.localScale=Vector3.one;var model=go.GetComponentInChildren<NewRobotAnimationPresentation>().transform;model.localPosition=Vector3.zero;
                var skins=go.GetComponentsInChildren<SkinnedMeshRenderer>(true);var b=StaticBounds(skins.Where(s=>s.name.EndsWith("_LOD0")));
                model.localPosition=Vector3.up*(-b.min.y);foreach(var r in skins)FitSkinBounds(r);go.transform.localScale=Vector3.one*(3/b.size.y);go.GetComponent<LODGroup>().RecalculateBounds();
                var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(Art+"/Animations/UpperBody.mask");ConfigureMask(mask,model);EditorUtility.SetDirty(mask);PrefabUtility.SaveAsPrefabAsset(go,Prefab);
                File.WriteAllText(Reports+"/fit-status.txt","Rest mesh height="+b.size.y+" presentation scale="+go.transform.localScale.y+"; upper mask paths relative to Animator, local-space culling margins converted from model units.");
            }
            finally{PrefabUtility.UnloadPrefabContents(go);}
            AssetDatabase.SaveAssets();if(cleanLab&&SceneManager.GetActiveScene().isDirty)EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        static Texture2D Texture(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/IR_NewRobot_"+name+".png");
        static AnimatorController Controller(Transform model)
        {
            var c=AnimatorController.CreateAnimatorControllerAtPath(Art+"/Animations/NewRobot.controller");c.AddParameter("MoveSpeed",AnimatorControllerParameterType.Float);c.AddParameter("CycleSpeed",AnimatorControllerParameterType.Float);c.AddParameter("Turn",AnimatorControllerParameterType.Float);c.AddParameter("Attack",AnimatorControllerParameterType.Trigger);c.AddParameter("CancelAttack",AnimatorControllerParameterType.Trigger);c.AddParameter("ShotSpeed",AnimatorControllerParameterType.Float);var ps=c.parameters;foreach(var p in ps)if(p.name=="CycleSpeed"||p.name=="ShotSpeed")p.defaultFloat=1;c.parameters=ps;
            var sm=c.layers[0].stateMachine;var locomotion=sm.AddState("Locomotion");var tree=new BlendTree{name="Measured locomotion",blendParameter="MoveSpeed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,c);tree.AddChild(Clip("IRN_Idle"),0);tree.AddChild(Clip("IRN_Walk"),WalkSpeed);locomotion.motion=tree;locomotion.speedParameter="CycleSpeed";locomotion.speedParameterActive=true;locomotion.writeDefaultValues=false;sm.defaultState=locomotion;
            foreach(int direction in new[]{-1,1})
            {
                var state=sm.AddState(direction<0?"TurnLeft":"TurnRight");state.motion=Clip(direction<0?"IRN_TurnLeft":"IRN_TurnRight");state.writeDefaultValues=false;
                var enter=locomotion.AddTransition(state);enter.hasExitTime=false;enter.duration=.10f;enter.AddCondition(direction<0?AnimatorConditionMode.Less:AnimatorConditionMode.Greater,direction*.1f,"Turn");enter.AddCondition(AnimatorConditionMode.Less,.05f,"MoveSpeed");
                var exit=state.AddTransition(locomotion);exit.hasExitTime=false;exit.duration=.10f;exit.AddCondition(direction<0?AnimatorConditionMode.Greater:AnimatorConditionMode.Less,direction*.1f,"Turn");
                var move=state.AddTransition(locomotion);move.hasExitTime=false;move.duration=.10f;move.AddCondition(AnimatorConditionMode.Greater,.05f,"MoveSpeed");
            }
            var mask=new AvatarMask{name="NewRobot_UpperBody"};ConfigureMask(mask,model);AssetDatabase.CreateAsset(mask,Art+"/Animations/UpperBody.mask");
            c.AddLayer("UpperBody");var layers=c.layers;layers[1].avatarMask=mask;layers[1].defaultWeight=1;c.layers=layers;var upper=c.layers[1].stateMachine;
            var empty=upper.AddState("Empty");empty.writeDefaultValues=false;empty.AddStateMachineBehaviour<NewRobotShotState>().Configure(false);upper.defaultState=empty;
            var prep=upper.AddState("PrepareFire");prep.motion=Clip("IRN_PrepareFire");prep.AddStateMachineBehaviour<NewRobotShotState>().Configure(true);var fire=upper.AddState("Fire");fire.motion=Clip("IRN_Fire");var recover=upper.AddState("RecoverFire");recover.motion=Clip("IRN_RecoverFire");
            foreach(var state in new[]{prep,fire,recover}){state.writeDefaultValues=false;state.speedParameter="ShotSpeed";state.speedParameterActive=true;}
            var start=empty.AddTransition(prep);start.hasExitTime=false;start.duration=.08f;start.hasFixedDuration=true;start.AddCondition(AnimatorConditionMode.If,0,"Attack");
            Link(prep,fire,.04f);Link(fire,recover,.04f);Link(recover,empty,.10f);var cancel=upper.AddAnyStateTransition(empty);cancel.hasExitTime=false;cancel.hasFixedDuration=true;cancel.duration=.06f;cancel.canTransitionToSelf=false;cancel.AddCondition(AnimatorConditionMode.If,0,"CancelAttack");return c;
        }
        static void Link(AnimatorState a,AnimatorState b,float blend){var t=a.AddTransition(b);t.hasExitTime=true;t.exitTime=1;t.hasFixedDuration=true;t.duration=blend;}
        static void BuildLab(Material robotMaterial)
        {
            bool dirty=AnyDirty();var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="NewRobot_LabFloor"};floorMaterial.SetColor("_BaseColor",new Color(.07f,.09f,.11f));AssetDatabase.CreateAsset(floorMaterial,Art+"/LabFloor.mat");
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="PresentationFloor";floor.transform.position=new Vector3(0,-.06f,2);floor.transform.localScale=new Vector3(16,.1f,20);floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
            var robot=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene);robot.name="Robot_3m";var driver=new GameObject("AnimationPreview_Only").AddComponent<NewRobotAnimationPreview>();driver.Configure(robot.GetComponentInChildren<NewRobotAnimationPresentation>());
            var camera=new GameObject("LabCamera").AddComponent<Camera>();camera.transform.position=new Vector3(7,4.5f,11);camera.transform.LookAt(new Vector3(0,1.5f,1.8f));camera.fieldOfView=45;camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.03f,.045f);camera.gameObject.AddComponent<AudioListener>();
            var light=new GameObject("PresentationLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(40,-35,0);light.shadows=LightShadows.Soft;
            var scale=GameObject.CreatePrimitive(PrimitiveType.Cube);scale.name="HeightReference_1.8m";scale.transform.position=new Vector3(-3,.9f,0);scale.transform.localScale=new Vector3(.25f,1.8f,.25f);scale.GetComponent<Renderer>().sharedMaterial=floorMaterial;
            if(!EditorSceneManager.SaveScene(scene,Lab))throw new InvalidOperationException("Cannot save separate animation lab");
            if(dirty){EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}else EditorSceneManager.OpenScene(Lab);
        }
        sealed class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor test){}public void TestStarted(ITestAdaptor test){}public void TestFinished(ITestResultAdaptor test){}
            public void RunFinished(ITestResultAdaptor result){if(!SessionState.GetBool(Key+".testing",false))return;SessionState.EraseBool(Key+".testing");TestRunnerApi.SaveResultToFile(result,Reports+"/playmode.xml");File.WriteAllText(Reports+"/test-status.txt",result.ResultState+" passed="+result.PassCount+" failed="+result.FailCount);SessionState.SetBool(Key+".restore",true);SessionState.SetFloat(Key+".finished",(float)EditorApplication.timeSinceStartup);}
        }
    }
}
