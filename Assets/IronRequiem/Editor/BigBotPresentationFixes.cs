using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
    public static class BigBotPresentationFixes
    {
        const string Art=BigBotDestructionAuthoring.Art;
        static Transform Bone(GameObject go,string name)=>go.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        static T Save<T>(T value,string path)where T:Object{var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(value,path);return value;}
        static Mesh LocalMesh(SkinnedMeshRenderer source)
        {
            var mesh=new Mesh();source.BakeMesh(mesh,false);var scale=source.transform.lossyScale;var v=mesh.vertices;for(int i=0;i<v.Length;i++)v[i]=new Vector3(v[i].x/scale.x,v[i].y/scale.y,v[i].z/scale.z);mesh.vertices=v;mesh.RecalculateBounds();return mesh;
        }
        public static void Apply()
        {
            var go=PrefabUtility.LoadPrefabContents(BigBotDestructionAuthoring.Prefab);
            try
            {
                var skins=go.GetComponentsInChildren<SkinnedMeshRenderer>(true);var animator=go.GetComponentInChildren<Animator>();var controller=(AnimatorController)animator.runtimeAnimatorController;controller.animationClips.Single(c=>c.name=="IRN_Idle").SampleAnimation(animator.gameObject,0);
                var progression=new SerializedObject(go.GetComponent<BigBotDestruction>());var armor=progression.FindProperty("armor");for(int i=armor.arraySize-1;i>=0;i--)if(armor.GetArrayElementAtIndex(i).FindPropertyRelative("Name").stringValue!="Backpack")armor.DeleteArrayElementAtIndex(i);progression.ApplyModifiedPropertiesWithoutUndo();
                foreach(var skin in skins){skin.gameObject.SetActive(true);skin.enabled=true;}
                if(!controller.parameters.Any(p=>p.name=="TurnPhase"))controller.AddParameter("TurnPhase",AnimatorControllerParameterType.Float);
                foreach(var state in controller.layers[0].stateMachine.states)
                {
                    if(state.state.name=="TurnLeft"||state.state.name=="TurnRight")
                    {state.state.motion=Turn(go,skins,state.state.name=="TurnRight"?1:-1);state.state.timeParameter="TurnPhase";state.state.timeParameterActive=true;state.state.speedParameterActive=false;}
                }
                FitBounds(go,skins,controller);StrengthenSparks(go,skins);EditorUtility.SetDirty(controller);PrefabUtility.SaveAsPrefabAsset(go,BigBotDestructionAuthoring.Prefab);AssetDatabase.SaveAssets();
                File.WriteAllText(BigBotDestructionAuthoring.Reports+"/presentation-fixes.txt","Keep both load-bearing shoulder shells while alive; first loss is backpack. Root-bone-space bounds include locomotion, native turns, aim and death. LODs remain enabled. Articulated 45-degree turn stride follows measured angular distance. Critical electrical bursts on visible front surface and broken mount, existing URP material, no lights.");
            }
            finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        static AnimationClip Turn(GameObject go,SkinnedMeshRenderer[] skins,int sign)
        {
            var animator=go.GetComponentInChildren<Animator>();var all=skins[0].bones;var positions=all.Select(b=>b.localPosition).ToArray();var rotations=all.Select(b=>b.localRotation).ToArray();var bones=all.Where(b=>b.name=="Pelvis"||b.name=="Torso"||b.name.StartsWith("Thigh_")||b.name.StartsWith("Shin_")||b.name.StartsWith("Foot_")).ToArray();var curves=new AnimationCurve[bones.Length,7];for(int b=0;b<bones.Length;b++)for(int c=0;c<7;c++)curves[b,c]=new AnimationCurve();var left=Bone(go,"Foot_L");var right=Bone(go,"Foot_R");var footPositions=new[]{left.position,right.position};var footRotations=new[]{left.rotation,right.rotation};var origin=go.transform.position;
            for(int frame=0;frame<=30;frame++)
            {
                float u=frame/30f;for(int b=0;b<all.Length;b++){all[b].localPosition=positions[b];all[b].localRotation=rotations[b];}
                Bone(go,"Pelvis").position-=Vector3.up*(.025f*Mathf.Pow(Mathf.Sin(u*Mathf.PI*2),2));
                foreach(int side in new[]{0,1})
                {
                    float phase=Mathf.Repeat(u+side*.5f,1);float angle,lift=0;
                    if(phase<.62f)angle=sign*45*(.31f-phase);
                    else{float swing=(phase-.62f)/.38f;angle=sign*Mathf.Lerp(-13.95f,13.95f,Mathf.SmoothStep(0,1,swing));lift=.085f*Mathf.Pow(Mathf.Sin(swing*Mathf.PI),2);}
                    var target=origin+Quaternion.AngleAxis(angle,Vector3.up)*(footPositions[side]-origin)+Vector3.up*lift;Leg(go,side==0?"L":"R",target,Quaternion.AngleAxis(angle,Vector3.up)*footRotations[side]);
                }
                for(int b=0;b<bones.Length;b++){var p=bones[b].localPosition;var q=bones[b].localRotation;float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w};for(int c=0;c<7;c++)curves[b,c].AddKey(u,values[c]);}
            }
            var clip=new AnimationClip{name=sign>0?"BigBot_StepTurnRight":"BigBot_StepTurnLeft",frameRate=30};string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};for(int b=0;b<bones.Length;b++)for(int c=0;c<7;c++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bones[b],animator.transform),typeof(Transform),properties[c]),curves[b,c]);clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
            for(int b=0;b<all.Length;b++){all[b].localPosition=positions[b];all[b].localRotation=rotations[b];}return Save(clip,Art+"/"+clip.name+".anim");
        }
        static void Leg(GameObject go,string side,Vector3 target,Quaternion rotation)
        {
            var thigh=Bone(go,"Thigh_"+side);var shin=Bone(go,"Shin_"+side);var foot=Bone(go,"Foot_"+side);var hip=thigh.position;float l1=Vector3.Distance(hip,shin.position),l2=Vector3.Distance(shin.position,foot.position);var vector=target-hip;float d=Mathf.Clamp(vector.magnitude,Mathf.Abs(l1-l2)+.001f,l1+l2-.001f);var direction=vector.normalized;float a=(l1*l1-l2*l2+d*d)/(2*d);var knee=hip+direction*a+Vector3.ProjectOnPlane(go.transform.forward,direction).normalized*Mathf.Sqrt(Mathf.Max(0,l1*l1-a*a));thigh.rotation=Quaternion.FromToRotation(shin.position-hip,knee-hip)*thigh.rotation;shin.position=knee;shin.rotation=Quaternion.FromToRotation(foot.position-knee,target-knee)*shin.rotation;foot.position=target;foot.rotation=rotation;
        }
        static void FitBounds(GameObject go,SkinnedMeshRenderer[] skins,AnimatorController controller)
        {
            controller.animationClips.Single(c=>c.name=="IRN_Idle").SampleAnimation(go.GetComponentInChildren<Animator>().gameObject,0);
            var pivots=skins.Select(s=>Bone(go,s.name.Substring(0,s.name.LastIndexOf("_LOD",StringComparison.Ordinal)))).ToArray();var corners=new Vector3[skins.Length][];
            for(int i=0;i<skins.Length;i++)
            {
                var mesh=LocalMesh(skins[i]);var matrix=pivots[i].worldToLocalMatrix*skins[i].transform.localToWorldMatrix;var points=mesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray();var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);corners[i]=Enumerable.Range(0,8).Select(k=>bounds.center+Vector3.Scale(bounds.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1))).ToArray();Object.DestroyImmediate(mesh);
            }
            var fitted=new Bounds[skins.Length];bool first=true;Bounds actor=default;var model=go.GetComponentInChildren<Animator>().gameObject;
            foreach(var clip in controller.animationClips.Distinct())for(int frame=0;frame<=12;frame++)
            {
                clip.SampleAnimation(model,clip.length*frame/12);
                for(int i=0;i<skins.Length;i++)foreach(var point in corners[i])
                {
                    var world=pivots[i].TransformPoint(point);var local=skins[i].rootBone.InverseTransformPoint(world);if(first&&point==corners[i][0])fitted[i]=new Bounds(local,Vector3.zero);else fitted[i].Encapsulate(local);var root=go.transform.InverseTransformPoint(world);if(first&&i==0&&point==corners[i][0])actor=new Bounds(root,Vector3.zero);else actor.Encapsulate(root);
                }
                first=false;
            }
            controller.animationClips.Single(c=>c.name=="IRN_Idle").SampleAnimation(model,0);
            for(int i=0;i<skins.Length;i++){fitted[i].Expand(.8f/skins[i].rootBone.lossyScale.y);skins[i].localBounds=fitted[i];skins[i].updateWhenOffscreen=false;}
            actor.Expand(.7f/go.transform.lossyScale.y);var group=go.GetComponent<LODGroup>();group.localReferencePoint=actor.center;group.size=Mathf.Max(actor.size.x,actor.size.y,actor.size.z);group.fadeMode=LODFadeMode.None;
        }
        static void StrengthenSparks(GameObject go,SkinnedMeshRenderer[] skins)
        {
            var electric=go.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="DamageElectrical");var source=skins.Single(s=>s.name=="Torso_LOD0");var mesh=LocalMesh(source);var world=mesh.vertices.Select(v=>source.transform.TransformPoint(v)).ToArray();electric.transform.position=new Vector3(source.bounds.center.x,world.Average(v=>v.y),world.Max(v=>v.z)+.035f);Object.DestroyImmediate(mesh);
            foreach(var ps in new[]{electric,go.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="CannonBreak")})
            {
                var main=ps.main;main.maxParticles=ps==electric?32:24;main.startLifetime=new ParticleSystem.MinMaxCurve(.1f,.26f);main.startSize=new ParticleSystem.MinMaxCurve(.025f,.055f);main.startSpeed=new ParticleSystem.MinMaxCurve(1.4f,3);main.startColor=new Color(1,.72f,.15f,1);main.gravityModifier=.4f;
                var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2.5f;renderer.velocityScale=.03f;renderer.cameraVelocityScale=0;renderer.maxParticleSize=.05f;
            }
        }
    }
}
