using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Object=UnityEngine.Object;

namespace IronRequiem.Editor
{
    public static class T07AnimationAuthoring
    {
        const string Art=T07AnimationBuilder.Art;
        const string Reports=T07AnimationBuilder.Reports;
        public const string Draft="Assets/IronRequiem/Prefabs/Enemies/T07_Common_Animated_Working.prefab";
        public const string ControllerPath=Art+"/T07_Locomotion.controller";
        internal static T Ensure<T>(GameObject go) where T:Component
        {var c=go.GetComponent<T>();return c?c:go.AddComponent<T>();}
        internal static Transform Child(Transform parent,string name)
        {var child=parent.Find(name);if(!child){child=new GameObject(name).transform;child.SetParent(parent,false);}return child;}
        public static void BuildLab()
        {
            T07AnimationBuilder.RequireClean();
            if(File.Exists(Draft))throw new InvalidOperationException("Working prefab already exists; inspect before revising.");
            T07ScenePromotion.InScratchScene(()=>
            {
                var preview=EditorSceneManager.NewPreviewScene();
                try
                {
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(T07OptimizationBuilder.Enemy),preview);
                    PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
                    ConfigureEnemy(go,true);PrefabUtility.SaveAsPrefabAsset(go,Draft);Object.DestroyImmediate(go);
                }
                finally{EditorSceneManager.ClosePreviewScene(preview);}
            });
            T07ScenePromotion.ReplaceScene(T07OptimizationBuilder.Lab,T07OptimizationBuilder.Enemy,Draft);
            IronRequiemSetupTools.WriteAudit();
        }
        public static void ConfigureEnemy(GameObject go,bool createAnimations)
        {
            var oldBody=go.GetComponentsInChildren<LODGroup>(true).Single(g=>g.name=="Body");
            var heights=oldBody.GetLODs().Select(l=>l.screenRelativeTransitionHeight).ToArray();
            foreach(var renderer in oldBody.GetLODs().SelectMany(l=>l.renderers))renderer.enabled=false;
            Object.DestroyImmediate(oldBody);
            var bodyRoot=new GameObject("Body");bodyRoot.transform.SetParent(go.transform,false);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/T07_SharedRig.fbx"),bodyRoot.transform);
            model.name="AnimatedRig";
            var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(s=>s.name).ToArray();
            if(skins.Length!=3 || skins.Any(s=>s.bones.Length!=17))throw new InvalidOperationException("Expected three skins sharing 17 bones.");
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Enemies/T07New/Materials/T07_URP.mat");
            foreach(var skin in skins)
            {
                skin.sharedMaterial=material;skin.updateWhenOffscreen=false;
                var bounds=skin.localBounds;bounds.Expand(bounds.size.magnitude*.7f);skin.localBounds=bounds;
            }
            var group=bodyRoot.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;
            group.SetLODs(Enumerable.Range(0,3).Select(i=>new LOD(heights[i],new Renderer[]{skins[i]})).ToArray());
            group.localReferencePoint=new Vector3(0,1,0);group.size=2.4f;
            var bones=model.GetComponentsInChildren<Transform>(true).Where(t=>skins[0].bones.Contains(t)).ToDictionary(t=>t.name);
            var animator=go.GetComponent<EnemyMobile>().Animator;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            if(createAnimations)CreateAnimations(go,bones);
            animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            // Curve clips target the explicit transform hierarchy; the shared generic skin remains native.
            var avatarPath=Art+"/T07_GenericAvatar.asset";
            var avatar=AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
            if(!avatar){avatar=AvatarBuilder.BuildGenericAvatar(go,"");avatar.name="T07_GenericAvatar";AssetDatabase.CreateAsset(avatar,avatarPath);}
            animator.avatar=avatar;
            var rigRoot=Child(go.transform,"FootPlacementRig");var rig=Ensure<Rig>(rigRoot.gameObject);
            var targets=new Transform[2];var hints=new Transform[2];var positions=new Vector3[2];var rotations=new Quaternion[2];
            for(int i=0;i<2;i++)
            {
                string side=i==0?"L":"R";
                var constraint=Ensure<TwoBoneIKConstraint>(Child(rigRoot,"Leg_"+side).gameObject);
                targets[i]=Child(rigRoot,"FootTarget_"+side);hints[i]=Child(rigRoot,"KneeHint_"+side);
                var foot=bones["Foot_"+side];targets[i].SetPositionAndRotation(foot.position,foot.rotation);
                hints[i].position=foot.position+go.transform.forward*.65f+go.transform.up*.45f;
                positions[i]=go.transform.InverseTransformPoint(foot.position);rotations[i]=Quaternion.Inverse(go.transform.rotation)*foot.rotation;
                var data=constraint.data;data.root=bones["Thigh_"+side];data.mid=bones["Shin_"+side];data.tip=foot;
                data.target=targets[i];data.hint=hints[i];data.targetPositionWeight=1;data.targetRotationWeight=1;data.hintWeight=1;
                data.maintainTargetPositionOffset=false;data.maintainTargetRotationOffset=false;constraint.data=data;
            }
            var builder=Ensure<RigBuilder>(go);builder.layers.Clear();builder.layers.Add(new RigLayer(rig));
            var joint=go.GetComponentInChildren<DetachableCannon>(true);
            var driver=Ensure<T07AnimationDriver>(go);driver.Configure(animator,joint,rig);
            Ensure<T07FootPlacement>(go).Configure(driver,targets,hints,positions,rotations);
            var weapon=go.GetComponentsInChildren<WeaponController>(true).Single();
            Ensure<T07CannonSocket>(go).Configure(bones["Forearm_L"],weapon.transform.parent,weapon.transform,joint,go.GetComponent<EnemyMobile>());
            go.GetComponent<EnemyController>().DeathDuration=1.65f;
            var effect=T07ExplosionAuthoring.Build();go.GetComponentInChildren<CannonExplosion>(true).Configure(effect);
        }

        public static void ReviseWorking()
        {
            T07AnimationBuilder.RequireClean();
            T07ScenePromotion.InScratchScene(()=>
            {
                var root=PrefabUtility.LoadPrefabContents(Draft);
                try
                {
                    var skin=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.name=="Body_LOD0");
                    CreateAnimations(root,skin.bones.ToDictionary(b=>b.name),true);
                    PrefabUtility.SaveAsPrefabAsset(root,Draft);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            });
            T07ExplosionAuthoring.ReviseWorking();IronRequiemSetupTools.WriteAudit();
        }
        static void CreateAnimations(GameObject root,Dictionary<string,Transform> bones,bool revise=false)
        {
            if(File.Exists(ControllerPath) && !revise)throw new InvalidOperationException("Animation assets already exist; do not overwrite delivered clips.");
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if(controller)
            {
                controller.layers=Array.Empty<AnimatorControllerLayer>();controller.parameters=Array.Empty<AnimatorControllerParameter>();
                foreach(var child in AssetDatabase.LoadAllAssetsAtPath(ControllerPath).Where(a=>a && a!=controller))Object.DestroyImmediate(child,true);
                controller.AddLayer("Base Layer");
            }
            else controller=AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach(var p in new[]{"MoveSpeed","Cycle","Turn"})controller.AddParameter(p,AnimatorControllerParameterType.Float);
            foreach(var p in new[]{"Alerted","Disarmed","Dead"})controller.AddParameter(p,AnimatorControllerParameterType.Bool);
            foreach(var p in new[]{"Attack","OnDamaged"})controller.AddParameter(p,AnimatorControllerParameterType.Trigger);
            var idle=Clip(root,bones,"Idle",2,true,(name,t)=>Pose(name,t,"idle"));
            var combatIdle=Clip(root,bones,"CombatIdle",2,true,(name,t)=>Pose(name,t,"combat"));
            var walk=Clip(root,bones,"Walk",1,true,(name,t)=>Pose(name,t,"walk"));
            var disarmed=Clip(root,bones,"DisarmedWalk",1,true,(name,t)=>Pose(name,t,"disarmed"));
            var turn=Clip(root,bones,"Turn",1,true,(name,t)=>Pose(name,t,"turn"));
            var death=Clip(root,bones,"Death",1.35f,false,(name,t)=>Pose(name,t,"death"));
            var attack=Clip(root,bones,"CannonAttack",.46f,false,(name,t)=>Pose(name,t,"attack"),true);
            var hit=Clip(root,bones,"HitReaction",.34f,false,(name,t)=>Pose(name,t,"hit"),true);
            var neutral=Clip(root,bones,"UpperBodyNeutral",.1f,true,(name,t)=>Vector3.zero,true);
            var sm=controller.layers[0].stateMachine;
            var armed=sm.AddState("ArmedLocomotion");armed.motion=MovementTree(controller,idle,walk,turn,"ArmedTree");armed.timeParameter="Cycle";armed.timeParameterActive=true;
            var alert=sm.AddState("AlertedLocomotion");alert.motion=MovementTree(controller,combatIdle,walk,turn,"AlertedTree");alert.timeParameter="Cycle";alert.timeParameterActive=true;
            var unarmed=sm.AddState("DisarmedLocomotion");unarmed.motion=MovementTree(controller,idle,disarmed,turn,"DisarmedTree");unarmed.timeParameter="Cycle";unarmed.timeParameterActive=true;
            var dead=sm.AddState("Death");dead.motion=death;dead.writeDefaultValues=false;
            var toDisarmed=armed.AddTransition(unarmed);toDisarmed.hasExitTime=false;toDisarmed.duration=.12f;toDisarmed.AddCondition(AnimatorConditionMode.If,0,"Disarmed");
            var alertDisarmed=alert.AddTransition(unarmed);alertDisarmed.hasExitTime=false;alertDisarmed.duration=.12f;alertDisarmed.AddCondition(AnimatorConditionMode.If,0,"Disarmed");
            var detected=armed.AddTransition(alert);detected.hasExitTime=false;detected.duration=.12f;detected.AddCondition(AnimatorConditionMode.If,0,"Alerted");detected.AddCondition(AnimatorConditionMode.IfNot,0,"Disarmed");
            var lost=alert.AddTransition(armed);lost.hasExitTime=false;lost.duration=.16f;lost.AddCondition(AnimatorConditionMode.IfNot,0,"Alerted");
            var dying=sm.AddAnyStateTransition(dead);dying.hasExitTime=false;dying.duration=.06f;dying.canTransitionToSelf=false;dying.AddCondition(AnimatorConditionMode.If,0,"Dead");
            sm.defaultState=armed;
            var upperSm=new AnimatorStateMachine{name="UpperBody"};AssetDatabase.AddObjectToAsset(upperSm,controller);
            var upper=new AnimatorControllerLayer{name="UpperBody",defaultWeight=1,blendingMode=AnimatorLayerBlendingMode.Additive,stateMachine=upperSm};
            var mask=new AvatarMask{name="T07_UpperBodyMask"};
            var transforms=root.GetComponentsInChildren<Transform>(true);mask.transformCount=transforms.Length;
            for(int i=0;i<transforms.Length;i++)
            {
                string path=AnimationUtility.CalculateTransformPath(transforms[i],root.transform);mask.SetTransformPath(i,path);
                mask.SetTransformActive(i,bones["Chest"]==transforms[i] || transforms[i].IsChildOf(bones["Chest"]));
            }
            var existingMask=AssetDatabase.LoadAssetAtPath<AvatarMask>(Art+"/T07_UpperBody.mask");
            if(existingMask){EditorUtility.CopySerialized(mask,existingMask);Object.DestroyImmediate(mask);mask=existingMask;AssetDatabase.SaveAssetIfDirty(mask);}
            else AssetDatabase.CreateAsset(mask,Art+"/T07_UpperBody.mask");
            upper.avatarMask=mask;controller.AddLayer(upper);
            var rest=upperSm.AddState("Rest");rest.motion=neutral;rest.writeDefaultValues=false;
            var firing=upperSm.AddState("CannonAttack");firing.motion=attack;firing.writeDefaultValues=false;
            var damaged=upperSm.AddState("HitReaction");damaged.motion=hit;damaged.writeDefaultValues=false;upperSm.defaultState=rest;
            var fire=upperSm.AddAnyStateTransition(firing);fire.hasExitTime=false;fire.duration=.025f;fire.canTransitionToSelf=true;
            fire.AddCondition(AnimatorConditionMode.If,0,"Attack");fire.AddCondition(AnimatorConditionMode.IfNot,0,"Disarmed");fire.AddCondition(AnimatorConditionMode.IfNot,0,"Dead");
            var react=upperSm.AddAnyStateTransition(damaged);react.hasExitTime=false;react.duration=.03f;react.canTransitionToSelf=true;
            react.AddCondition(AnimatorConditionMode.If,0,"OnDamaged");react.AddCondition(AnimatorConditionMode.IfNot,0,"Dead");
            foreach(var state in new[]{firing,damaged}){var end=state.AddTransition(rest);end.hasExitTime=true;end.exitTime=.96f;end.duration=.07f;}
            var disarm= firing.AddTransition(rest);disarm.hasExitTime=false;disarm.duration=.02f;disarm.AddCondition(AnimatorConditionMode.If,0,"Disarmed");
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssetIfDirty(controller);
        }
        static BlendTree MovementTree(AnimatorController controller,AnimationClip idle,AnimationClip walk,AnimationClip turn,string name)
        {
            var tree=new BlendTree{name=name,blendType=BlendTreeType.FreeformCartesian2D,blendParameter="MoveSpeed",blendParameterY="Turn"};
            AssetDatabase.AddObjectToAsset(tree,controller);tree.AddChild(idle,new Vector2(0,0));tree.AddChild(walk,new Vector2(3.5f,0));
            tree.AddChild(turn,new Vector2(0,1));tree.AddChild(turn,new Vector2(0,-1));return tree;
        }
        static AnimationClip Clip(GameObject root,Dictionary<string,Transform> bones,string name,float length,bool loop,Func<string,float,Vector3> rotation,bool upperOnly=false)
        {
            string assetPath=Art+"/"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);bool existing=clip;
            if(!clip)clip=new AnimationClip{name=name,frameRate=30};else clip.ClearCurves();
            foreach(var pair in bones)
            {
                if(upperOnly && pair.Key!="Chest" && !pair.Value.IsChildOf(bones["Chest"]))continue;
                string path=AnimationUtility.CalculateTransformPath(pair.Value,root.transform);
                var curves=Enumerable.Range(0,4).Select(_=>new AnimationCurve()).ToArray();
                for(int i=0;i<=40;i++)
                {
                    float t=i/40f;Vector3 e=rotation(pair.Key,t);
                    Vector3 x=pair.Value.parent.InverseTransformDirection(root.transform.right),y=pair.Value.parent.InverseTransformDirection(root.transform.up),z=pair.Value.parent.InverseTransformDirection(root.transform.forward);
                    Quaternion q=Quaternion.AngleAxis(e.x,x)*Quaternion.AngleAxis(e.y,y)*Quaternion.AngleAxis(e.z,z)*pair.Value.localRotation;
                    for(int c=0;c<4;c++)curves[c].AddKey(t*length,q[c]);
                }
                for(int c=0;c<4;c++)clip.SetCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[c],curves[c]);
                if(pair.Key=="Hips")
                {
                    var positions=Enumerable.Range(0,3).Select(_=>new AnimationCurve()).ToArray();
                    for(int i=0;i<=40;i++)
                    {
                        float t=i/40f;Vector3 offset=Vector3.zero;
                        if(name.Contains("Walk"))offset=Vector3.up*(-.18f+.014f*Mathf.Cos(t*Mathf.PI*4));
                        if(name=="Idle" || name=="CombatIdle")offset=Vector3.up*(-.075f+.004f*Mathf.Sin(t*Mathf.PI*2));
                        if(name=="Turn")offset=Vector3.up*(-.10f);
                        if(name=="Death")offset=new Vector3(.12f,-.58f,-.15f)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.9f,t));
                        Vector3 p=pair.Value.localPosition+pair.Value.parent.InverseTransformVector(root.transform.TransformVector(offset));
                        for(int c=0;c<3;c++)positions[c].AddKey(t*length,p[c]);
                    }
                    for(int c=0;c<3;c++)clip.SetCurve(path,typeof(Transform),"m_LocalPosition."+"xyz"[c],positions[c]);
                }
            }
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;
            if(upperOnly){settings.hasAdditiveReferencePose=true;settings.additiveReferencePoseTime=0;settings.additiveReferencePoseClip=clip;}
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            if(existing){EditorUtility.SetDirty(clip);AssetDatabase.SaveAssetIfDirty(clip);}else AssetDatabase.CreateAsset(clip,assetPath);return clip;
        }
        static Vector3 Pose(string bone,float t,string kind)
        {
            float wave=Mathf.Sin(t*Mathf.PI*2),other=-wave,side=bone.EndsWith("_L")?1:-1;
            if(kind=="death")
            {
                float collapse=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.9f,t));
                if(bone=="Hips")return new Vector3(30*collapse,0,-58*collapse);
                if(bone.StartsWith("Shin"))return new Vector3(58*collapse,0,0);
                if(bone.StartsWith("Thigh"))return new Vector3(-35*collapse,0,side*12*collapse);
                if(bone=="Chest")return new Vector3(18*collapse,0,8*collapse);
                if(bone=="Head")return new Vector3(18*collapse,0,10*collapse);
            }
            if(kind=="attack" || kind=="hit")
            {
                float recoil=t<.12f?t/.12f:Mathf.Pow(1-Mathf.InverseLerp(.12f,1,t),2);
                if(bone=="Chest")return new Vector3((kind=="hit"?-12:5)*recoil,0,-3*recoil);
                if(bone=="UpperArm_L")return new Vector3(-9*recoil,0,0);
                if(bone=="Head")return new Vector3(-4*recoil,0,0);
            }
            if(kind=="walk" || kind=="disarmed")
            {
                if(bone.StartsWith("Thigh"))return new Vector3(24*wave*side,0,0);
                if(bone.StartsWith("Shin"))return new Vector3(32*Mathf.Max(0,-wave*side),0,0);
                if(bone.StartsWith("Foot"))return new Vector3(-12*wave*side,0,0);
                if(bone=="UpperArm_R")return new Vector3(-14*wave,0,kind=="disarmed"?-9:0);
                if(bone=="UpperArm_L")return new Vector3((kind=="disarmed"?11:3)*wave,0,kind=="disarmed"?7:0);
                if(bone=="Chest")return new Vector3(5,3*other,1.5f*wave);
                if(bone=="Head")return new Vector3(-3,-2*other,0);
            }
            if(kind=="turn")
            {
                if(bone=="Chest")return new Vector3(0,5*wave,2*other);
                if(bone.StartsWith("Thigh"))return new Vector3(8*wave*side,0,0);
            }
            if(kind=="idle" && bone=="Chest")return new Vector3(.7f*wave,1.1f*other,0);
            if(kind=="idle" && bone=="Head")return new Vector3(0,1.5f*wave,0);
            if(kind=="combat" && bone=="Chest")return new Vector3(4+.5f*wave,0,0);
            if(kind=="combat" && bone=="UpperArm_L")return new Vector3(-8,0,0);
            if(kind=="combat" && bone=="Head")return new Vector3(-2,0,0);
            return Vector3.zero;
        }
    }
}
