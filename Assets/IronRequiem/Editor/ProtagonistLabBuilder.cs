using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
    public static class ProtagonistLabBuilder
    {
        const string Art=ProtagonistIntegrationTools.Art,Output=ProtagonistIntegrationTools.Output;
        public const string Lab="Assets/IronRequiem/Scenes/ProtagonistIntegrationLab.unity";
        public const string PlayerPath="Assets/IronRequiem/Prefabs/Player/Player_IR_Protagonist_v01.prefab";
        public const string BodyPath=Art+"/Protagonist_Body.prefab",FpsPath=Art+"/Protagonist_FPS.prefab";
        const string BodyModel=Art+"/Rigged/IR_Protagonist_Rigged.fbx",FpsModel=Art+"/Rigged/IR_Protagonist_FPS_Rigged.fbx";
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play first");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserving dirty user scene; build only from a clean or fresh Editor");
            if(File.Exists(PlayerPath)||File.Exists(Lab))throw new InvalidOperationException("Integration assets already exist; preserving them");
            Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            ConfigureImport(BodyModel);ConfigureImport(FpsModel);
            var material=CreateMaterial();
            var bodyController=BodyController();var fpsController=FpsController();
            CreateVisual(BodyModel,BodyPath,material,bodyController,true);
            CreateVisual(FpsModel,FpsPath,material,fpsController,false);
            BuildPlayer();BuildLab();AssetDatabase.SaveAssets();IronRequiemSetupTools.WriteAudit();
            File.WriteAllText(Output+"/build-status.txt","Built visual/model variants, Player_IR_Protagonist_v01 and ProtagonistIntegrationLab. Original scenes/prefabs preserved.");
        }
        static void ConfigureImport(string path)
        {
            var i=(ModelImporter)AssetImporter.GetAtPath(path);i.animationType=ModelImporterAnimationType.Generic;i.importAnimation=true;i.materialImportMode=ModelImporterMaterialImportMode.None;i.isReadable=false;i.animationCompression=ModelImporterAnimationCompression.Off;i.optimizeGameObjects=false;
            var clips=i.defaultClipAnimations;
            foreach(var clip in clips){clip.loopTime=!clip.name.Contains("Punch");clip.lockRootRotation=true;clip.lockRootHeightY=true;clip.lockRootPositionXZ=true;}
            i.clipAnimations=clips;i.SaveAndReimport();
        }
        static Material CreateMaterial()
        {
            foreach(string path in AssetDatabase.FindAssets("t:Texture2D",new[]{Art+"/Source/Textures"}).Select(AssetDatabase.GUIDToAssetPath))
            {var i=(TextureImporter)AssetImporter.GetAtPath(path);i.textureType=path.Contains("_Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;i.sRGBTexture=path.Contains("_BaseColor");i.maxTextureSize=2048;i.SaveAndReimport();}
            var existing=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/IR_Protagonist_URP.mat");if(existing)return existing;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="IR_Protagonist_URP"};
            mat.SetTexture("_BaseMap",Texture("BaseColor"));mat.SetTexture("_BumpMap",Texture("Normal"));mat.SetTexture("_MetallicGlossMap",Texture("MetalSmooth"));mat.EnableKeyword("_NORMALMAP");mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.SetFloat("_Metallic",1);mat.SetFloat("_Smoothness",1);
            AssetDatabase.CreateAsset(mat,Art+"/Materials/IR_Protagonist_URP.mat");return mat;
        }
        static Texture2D Texture(string type)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Source/Textures/IR_Protagonist_"+type+".png");
        static AnimationClip Clip(string model,string name)=>AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__")&&c.name.EndsWith("|"+name));
        static AnimatorController BodyController()
        {
            var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(Art+"/Rigged/Protagonist_Body.controller");if(!c)c=AnimatorController.CreateAnimatorControllerAtPath(Art+"/Rigged/Protagonist_Body.controller");if(c.layers.Length==0)c.AddLayer("Base Layer");foreach(var old in c.layers[0].stateMachine.states)c.layers[0].stateMachine.RemoveState(old.state);c.parameters=new[]{new AnimatorControllerParameter{name="MoveSpeed",type=AnimatorControllerParameterType.Float},new AnimatorControllerParameter{name="CycleSpeed",type=AnimatorControllerParameterType.Float,defaultFloat=1}};
            var state=c.layers[0].stateMachine.AddState("Locomotion");var tree=new BlendTree{name="Velocity locomotion",blendParameter="MoveSpeed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,c);tree.AddChild(Clip(BodyModel,"IR_Idle"),0);tree.AddChild(Clip(BodyModel,"IR_Walk"),1);state.motion=tree;state.speedParameter="CycleSpeed";state.speedParameterActive=true;c.layers[0].stateMachine.defaultState=state;return c;
        }
        static AnimatorController FpsController()
        {
            var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(Art+"/Rigged/Protagonist_FPS.controller");if(!c)c=AnimatorController.CreateAnimatorControllerAtPath(Art+"/Rigged/Protagonist_FPS.controller");if(c.layers.Length==0)c.AddLayer("Base Layer");foreach(var old in c.layers[0].stateMachine.states)c.layers[0].stateMachine.RemoveState(old.state);c.parameters=Array.Empty<AnimatorControllerParameter>();c.AddParameter("HandPose",AnimatorControllerParameterType.Int);var sm=c.layers[0].stateMachine;
            var normal=sm.AddState("NormalFist");normal.motion=Clip(FpsModel,"IR_HandFist");var grip=sm.AddState("ModuleSupport");grip.motion=Clip(FpsModel,"IR_HandGrip");sm.defaultState=normal;
            var a=normal.AddTransition(grip);a.hasExitTime=false;a.duration=.08f;a.hasFixedDuration=true;a.AddCondition(AnimatorConditionMode.Equals,1,"HandPose");
            var b=grip.AddTransition(normal);b.hasExitTime=false;b.duration=.08f;b.hasFixedDuration=true;b.AddCondition(AnimatorConditionMode.Equals,0,"HandPose");return c;
        }
        static GameObject Instance(string path)=> (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        static void CreateVisual(string model,string path,Material material,RuntimeAnimatorController controller,bool body)
        {
            var go=Instance(model);go.name=body?"Protagonist_Body":"Protagonist_FPS";
            try
            {
                var animator=go.GetComponent<Animator>();if(!animator)animator=go.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=body?AnimatorCullingMode.CullUpdateTransforms:AnimatorCullingMode.AlwaysAnimate;
                var renderers=go.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach(var r in renderers){r.sharedMaterial=material;r.updateWhenOffscreen=!body;if(!body){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}}
                if(body)
                {
                    var lod=go.GetComponent<LODGroup>();if(!lod)lod=go.AddComponent<LODGroup>();var levels=new LOD[3];
                    for(int i=0;i<3;i++){var rs=renderers.Where(r=>r.name.EndsWith("_LOD"+i)).Cast<Renderer>().ToArray();if(rs.Length!=5)throw new InvalidOperationException("Five pieces required per LOD");levels[i]=new LOD(new[]{.5f,.22f,.04f}[i],rs);}
                    lod.SetLODs(levels);lod.RecalculateBounds();lod.fadeMode=LODFadeMode.None;
                }
                PrefabUtility.SaveAsPrefabAsset(go,path);
            }
            finally{Object.DestroyImmediate(go);}
        }
        static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        static Transform Socket(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static void BuildPlayer()
        {
            var go=Instance(CharacterPrototypeBuilder.PlayerPath);go.name="Player_IR_Protagonist_v01";
            try
            {
                var previous=go.transform.Find("SurvivorRobot_Visual");if(previous)previous.gameObject.SetActive(false);
                var body=Instance(BodyPath);body.transform.SetParent(go.transform,false);
                var bodyPresentation=go.GetComponent<PlayerBodyPresentation>();bodyPresentation.Configure(body.transform);
                var player=go.GetComponent<PlayerCharacterController>();var weapons=go.GetComponent<PlayerWeaponsManager>();
                var fps=Instance(FpsPath);fps.transform.SetParent(weapons.WeaponParentSocket,false);
                int layer=0;while(layer<31&&(weapons.FpsWeaponLayer.value&(1<<layer))==0)layer++;
                foreach(var t in fps.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;
                if((player.PlayerCamera.cullingMask&(1<<layer))!=0||(weapons.WeaponCamera.cullingMask&(1<<layer))==0)throw new InvalidOperationException("Existing camera separation is invalid; preserving camera settings");
                var root=fps.transform;var leftHand=Find(root,"Hand_L");var rightHand=Find(root,"Hand_R");
                var leftSocket=Socket(leftHand,"ModuleSocket_L");var rightSocket=Socket(rightHand,"ModuleSocket_R");
                go.AddComponent<ProtagonistHandsPresentation>().Configure(weapons,player,go.GetComponent<StolenWeaponCarrier>(),AssetDatabase.LoadAssetAtPath<WeaponController>(DesignV02Upgrade.PistolPath),root,fps.GetComponent<Animator>(),Find(root,"Forearm_L"),Find(root,"Forearm_R"),leftHand,rightHand,leftSocket,rightSocket,root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Hand_R_FPS").ToArray());
                go.AddComponent<ProtagonistBodyAnimation>().Configure(player,body.GetComponent<Animator>());
                PrefabUtility.SaveAsPrefabAsset(go,PlayerPath);
            }
            finally{Object.DestroyImmediate(go);}
        }
        static void BuildLab()
        {
            if(!AssetDatabase.CopyAsset(CombatV03Tools.Main,Lab))throw new InvalidOperationException("Cannot copy separate laboratory");
            var scene=EditorSceneManager.OpenScene(Lab);var old=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerCharacterController>(true)).Single().gameObject;
            var replacement=Instance(PlayerPath);replacement.name=old.name;replacement.transform.SetParent(old.transform.parent,false);replacement.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);replacement.transform.localScale=old.transform.localScale;
            // Preserve serialized gameplay tuning from this scene, then remap its existing references.
            foreach(var component in old.GetComponents<Component>().Where(c=>c&&!(c is Transform)&&!(c is PlayerBodyPresentation)))
            {var target=replacement.GetComponent(component.GetType());if(!target)target=replacement.AddComponent(component.GetType());EditorUtility.CopySerialized(component,target);}
            var map=new Dictionary<Object,Object>();
            foreach(var t in old.GetComponentsInChildren<Transform>(true))
            {
                string path=AnimationUtility.CalculateTransformPath(t,old.transform);var target=path==""?replacement.transform:replacement.transform.Find(path);if(!target)continue;
                map[t]=target;map[t.gameObject]=target.gameObject;
                foreach(var component in t.GetComponents<Component>().Where(c=>c&&!(c is Transform)))
                {var equivalent=target.GetComponents(component.GetType());var peers=t.GetComponents(component.GetType());int index=Array.IndexOf(peers,component);if(index<equivalent.Length)map[component]=equivalent[index];}
            }
            int remapped=0;
            foreach(var component in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c&&!c.transform.IsChildOf(old.transform)))
            {
                var serialized=new SerializedObject(component);var property=serialized.GetIterator();bool changed=false;
                while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&property.objectReferenceValue&&map.TryGetValue(property.objectReferenceValue,out var target))
                {property.objectReferenceValue=target;changed=true;remapped++;}
                if(changed)serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Object.DestroyImmediate(old);PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.GetComponent<PlayerWeaponsManager>());
            var spawn=replacement.transform.position;var camera=replacement.GetComponent<PlayerCharacterController>().PlayerCamera.transform;
            for(int i=0;i<3;i++)
            {
                var display=Instance(BodyPath);display.name="Protagonist_Display_LOD"+i;display.transform.position=spawn+replacement.transform.forward*4+replacement.transform.right*((i-1)*1.5f);display.transform.rotation=Quaternion.LookRotation(-replacement.transform.forward);display.GetComponent<LODGroup>().ForceLOD(i);
                var label=new GameObject("LOD"+i+"_Label");label.transform.SetParent(display.transform,false);label.transform.localPosition=new Vector3(0,2.05f,0);label.transform.localRotation=Quaternion.Euler(0,180,0);var text=label.AddComponent<TextMesh>();text.text="LOD "+i;text.anchor=TextAnchor.MiddleCenter;text.characterSize=.06f;text.fontSize=32;
            }
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Laboratory save failed");
            File.WriteAllText(Output+"/lab-references.txt","Remapped existing object references: "+remapped+". Player motion/input/health/weapon tuning copied from saved MainScene. MainScene not modified.");
        }
    }
}
