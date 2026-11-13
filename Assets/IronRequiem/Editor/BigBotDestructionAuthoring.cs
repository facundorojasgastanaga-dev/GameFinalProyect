using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
    [InitializeOnLoad] public static class BigBotDestructionAuthoring
    {
        public const string Prefab="Assets/IronRequiem/Prefabs/Enemies/BigBot_Destructible_v01.prefab",Lab="Assets/IronRequiem/Scenes/BigBotDestructionLab.unity",Art="Assets/IronRequiem/Art/Robots/BigBotDestruction",Reports="Tools/IronRequiem/Reports/BigBotDestruction";
        const string Request="Tools/IronRequiem/Requests/bigbot-destruction.txt",Key="IRON.BigBotDestruction";
        static readonly DateTime LoadedAt=DateTime.UtcNow;static TestRunnerApi api;
        static BigBotDestructionAuthoring(){EditorApplication.update+=Tick;if(SessionState.GetBool(Key+".testing",false))EditorApplication.delayCall+=Register;}
        static bool Dirty()=>Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty);
        static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(SessionState.GetBool(Key+".restore",false)&&EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".at",0)>5){SessionState.EraseBool(Key+".restore");if(!Dirty())EditorSceneManager.OpenScene(Lab);}
            if(!File.Exists(Request))return;
            foreach(string file in new[]{"Assets/IronRequiem/Editor/BigBotDestructionAuthoring.cs","Assets/IronRequiem/Editor/BigBotPresentationFixes.cs","Assets/IronRequiem/Scripts/AI/BigBotDestruction.cs","Assets/IronRequiem/Scripts/AI/BigBotCombat.cs","Assets/IronRequiem/Tests/PlayMode/BigBotDestructionTests.cs"})if(File.Exists(file)&&LoadedAt<File.GetLastWriteTimeUtc(file))return;
            string op=File.ReadAllText(Request).Trim();File.Delete(Request);Directory.CreateDirectory(Reports);
            try
            {
                if(op=="build"){Build();File.WriteAllText(Request,"test");}
                else if(op=="revise"){Build(true);File.WriteAllText(Request,"test");}
                else if(op=="fix-presentation"){BigBotPresentationFixes.Apply();File.WriteAllText(Request,"test-presentation");}
                else if(op=="inspect")Inspect();
                else if(op=="test"||op=="test-presentation"||op=="test-critical-view")
                {
                    if(Dirty())throw new InvalidOperationException("Preserving dirty user scenes. Save user changes before tests.");
                    Register();SessionState.SetBool(Key+".testing",true);api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=op=="test-critical-view"?new[]{"IronRequiem.Tests.BigBotDestructionTests.MountsStayAttachedAndCriticalSparksAreVisible"}:op=="test-presentation"?new[]{"IronRequiem.Tests.BigBotDestructionTests.MountsStayAttachedAndCriticalSparksAreVisible","IronRequiem.Tests.BigBotDestructionTests.CloseCameraRendersEveryApproachAngle","IronRequiem.Tests.BigBotDestructionTests.TurnStepsFollowAngularDistance","IronRequiem.Tests.BigBotDestructionTests.HeavyDeathArticulatesStaysAboveFloorAndUsesExistingCleanup"}:new[]{"IronRequiem.Tests.BigBotDestructionTests"}}));
                }
                else throw new InvalidOperationException("Unknown fixed Big Bot destruction request");
            }
            catch(Exception e){File.WriteAllText(Reports+"/error.txt",e.ToString());Debug.LogException(e);}
        }
        static void Inspect()
        {
            var lines=new List<string>();var scene=EditorSceneManager.NewPreviewScene();
            foreach(string path in new[]{BigBotCombatAuthoring.Enemy,Prefab})
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);
                lines.Add(path+" root="+go.transform.position+" scale="+go.transform.lossyScale);
                var skin=go.GetComponentsInChildren<SkinnedMeshRenderer>(true)[0];var mesh=new Mesh();skin.BakeMesh(mesh,false);
                lines.Add("skin="+skin.name+" pos="+skin.transform.position+" scale="+skin.transform.lossyScale+" localScale="+skin.transform.localScale+" raw="+skin.sharedMesh.bounds+" bake="+mesh.bounds);
                foreach(var bone in skin.bones)lines.Add("bone="+bone.name+" world="+bone.position+" local="+bone.localPosition+" scale="+bone.lossyScale);
                Object.DestroyImmediate(mesh);Object.DestroyImmediate(go);
            }
            EditorSceneManager.ClosePreviewScene(scene);
            foreach(var surface in Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>())lines.Add("surface "+surface.name+" data="+surface.navMeshData+" asset="+AssetDatabase.GetAssetPath(surface.navMeshData));
            lines.Add("nav triangles="+UnityEngine.AI.NavMesh.CalculateTriangulation().vertices.Length);File.WriteAllLines(Reports+"/diagnostic.txt",lines);
        }
        static Transform Find(GameObject go,string name)=>go.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        static void Build(bool revise=false)
        {
            if(!revise&&(File.Exists(Prefab)||File.Exists(Lab)))throw new InvalidOperationException("Preserve existing destruction delivery.");
            Directory.CreateDirectory(Art);AssetDatabase.Refresh();var preview=EditorSceneManager.NewPreviewScene();var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BigBotCombatAuthoring.Enemy),preview);
            try
            {
                go.name="BigBot_Destructible_v01";var combat=go.GetComponent<BigBotCombat>();var animator=combat.Presentation.Animator;var skins=go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if(skins.Length!=45||skins[0].bones.Length!=16)throw new InvalidOperationException("Unexpected Big Bot rig/LOD identity");
                var inventory=skins.Select(r=>r.name+" vertices="+r.sharedMesh.vertexCount+" bones="+r.bones.Length).ToArray();File.WriteAllLines(Reports+"/geometry-inspection.txt",inventory);
                var source=skins[0].sharedMaterial;var stages=new Material[3];
                for(int i=0;i<3;i++){stages[i]=new Material(source){name="BigBot_Damage_"+(i+1)};float tint=new[]{.83f,.68f,.53f}[i];stages[i].SetColor("_BaseColor",new Color(tint,tint*.97f,tint*.92f,1));stages[i].SetFloat("_Smoothness",.25f-i*.06f);stages[i]=Save(stages[i],Art+"/Damage_"+(i+1)+".mat");}
                var interior=AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Enemies/T07Destructible/ExposedMechanism.mat");
                string controllerPath=Art+"/BigBotDestruction.controller";if(!File.Exists(controllerPath))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),controllerPath);var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);animator.runtimeAnimatorController=controller;
                var idle=controller.animationClips.Single(c=>c.name=="IRN_Idle");idle.SampleAnimation(animator.gameObject,0);
                var parts=new List<BigBotDestruction.ArmorPart>();
                foreach(var name in new[]{"Backpack","Shoulder_L","Shoulder_R"})
                {
                    var pivot=Find(go,name);var views=skins.Where(r=>r.name.StartsWith(name+"_LOD")).OrderBy(r=>r.name).ToArray();if(views.Length!=3)throw new InvalidOperationException("Missing actual rigid part "+name);
                    var mesh=BakeInBone(views[0],pivot);mesh.name=name+"_Debris";mesh.subMeshCount=2;mesh.SetTriangles(Array.Empty<int>(),1);mesh=Save(mesh,Art+"/"+mesh.name+".asset");
                    parts.Add(new BigBotDestruction.ArmorPart{Name=name,Stage=name=="Shoulder_R"?3:2,Pivot=pivot,Views=views,Debris=mesh});
                }
                var cannonViews=skins.Where(r=>r.name.StartsWith("Forearm_R_LOD")||r.name.StartsWith("Pincer_R_LOD")).ToArray();if(cannonViews.Length!=6)throw new InvalidOperationException("Cannon must be six existing rigid skins");
                var joint=Find(go,"Forearm_R");var debris=BuildCannon(go,joint,cannonViews,preview);
                var chest=Find(go,"Torso");var sparks=Effect(chest,"DamageElectrical",true);var smoke=Effect(chest,"LocalizedSmoke",false);var fracture=Effect(joint,"CannonBreak",true);var fm=fracture.main;fm.loop=false;fm.maxParticles=24;
                var damage=go.AddComponent<BigBotDestruction>();damage.Configure(parts.ToArray(),skins,stages,interior,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/VFX/T07_BodyExplosion.prefab"),joint,cannonViews,debris,sparks,smoke,fracture,AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FPS/Audio/SFX/Player/Land_Damage.wav"));
                var soles=new List<BigBotDestruction.SoleSupport>();
                foreach(string side in new[]{"L","R"})
                {
                    var foot=Find(go,"Foot_"+side);var cloud=new List<Vector3>();
                    foreach(var skin in skins.Where(r=>r.name.StartsWith("Foot_"+side+"_LOD")))
                    {var mesh=BakeInBone(skin,foot);cloud.AddRange(mesh.vertices);Object.DestroyImmediate(mesh);}
                    var points=new List<Vector3>();float bottom=cloud.Min(p=>foot.TransformPoint(p).y);
                    // Use real sole vertices rather than empty corners of a rotated bounding box.
                    foreach(var point in cloud)if(foot.TransformPoint(point).y<bottom+.025f)points.Add(point);
                    for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                    {if(x==0&&y==0&&z==0)continue;var direction=new Vector3(x,y,z);var point=cloud.OrderBy(p=>Vector3.Dot(p,direction)).First();if(!points.Contains(point))points.Add(point);}
                    soles.Add(new BigBotDestruction.SoleSupport{Bone=foot,Points=points.ToArray()});
                }
                damage.ConfigureSoles(soles.ToArray());
                var death=BuildDeath(go,skins);foreach(var old in controller.layers[0].stateMachine.states.Where(c=>c.state.name=="Death").ToArray())controller.layers[0].stateMachine.RemoveState(old.state);var state=controller.layers[0].stateMachine.AddState("Death");state.motion=death;state.writeDefaultValues=false;EditorUtility.SetDirty(controller);
                PrefabUtility.SaveAsPrefabAsset(go,Prefab);AssetDatabase.SaveAssets();
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            BigBotPresentationFixes.Apply();BuildLab();File.WriteAllText(Reports+"/build-status.txt","Variant/lab created; source assets conserved. Three thresholds, three secondary shells, right cannon with three LODs, 1.8 s rigid-skeleton death, shared capped debris pool.");
        }
        static T Save<T>(T item,string path) where T:Object
        {
            var existing=AssetDatabase.LoadAssetAtPath<T>(path);
            if(existing){EditorUtility.CopySerialized(item,existing);Object.DestroyImmediate(item);EditorUtility.SetDirty(existing);return existing;}
            AssetDatabase.CreateAsset(item,path);return item;
        }
        static void BakeLocal(SkinnedMeshRenderer skin,Mesh baked)
        {
            skin.BakeMesh(baked,false);
            // The imported rigid skeleton carries its scale through skinning; BakeMesh(false) still
            // includes that inherited scale. Strip it before using the renderer's localToWorld matrix.
            var vertices=baked.vertices;var scale=skin.transform.lossyScale;
            for(int i=0;i<vertices.Length;i++)vertices[i]=new Vector3(vertices[i].x/scale.x,vertices[i].y/scale.y,vertices[i].z/scale.z);
            baked.vertices=vertices;baked.RecalculateBounds();
        }
        static Mesh BakeInBone(SkinnedMeshRenderer r,Transform bone)
        {
            var mesh=new Mesh();BakeLocal(r,mesh);var matrix=bone.worldToLocalMatrix*r.transform.localToWorldMatrix;var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);mesh.vertices=vertices;
            var normals=mesh.normals;var normalMatrix=matrix.inverse.transpose;for(int i=0;i<normals.Length;i++)normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;mesh.normals=normals;
            var tangents=mesh.tangents;for(int i=0;i<tangents.Length;i++){var t=matrix.MultiplyVector(new Vector3(tangents[i].x,tangents[i].y,tangents[i].z)).normalized;tangents[i]=new Vector4(t.x,t.y,t.z,tangents[i].w);}mesh.tangents=tangents;mesh.bindposes=Array.Empty<Matrix4x4>();mesh.boneWeights=Array.Empty<BoneWeight>();mesh.RecalculateBounds();return mesh;
        }
        static BigBotCannonDebris BuildCannon(GameObject bot,Transform joint,SkinnedMeshRenderer[] views,Scene preview)
        {
            var root=new GameObject("BigBot_FallenRightCannon");SceneManager.MoveGameObjectToScene(root,preview);var levels=new List<Renderer>[3];Bounds bounds=default;bool first=true;
            for(int lod=0;lod<3;lod++)
            {
                levels[lod]=new List<Renderer>();foreach(var source in views.Where(v=>v.name.EndsWith("LOD"+lod)))
                {
                    var pieceBone=Find(bot,source.name.StartsWith("Pincer")?"Pincer_R":"Forearm_R");var mesh=BakeInBone(source,pieceBone);mesh.name=source.name+"_Static";mesh=Save(mesh,Art+"/"+mesh.name+".asset");var piece=new GameObject(source.name);piece.transform.SetParent(root.transform,false);piece.transform.localPosition=joint.InverseTransformPoint(pieceBone.position);piece.transform.localRotation=Quaternion.Inverse(joint.rotation)*pieceBone.rotation;piece.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=piece.AddComponent<MeshRenderer>();renderer.sharedMaterial=source.sharedMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;levels[lod].Add(renderer);
                    if(lod==0){if(first){bounds=mesh.bounds;first=false;}else bounds.Encapsulate(mesh.bounds);}
                }
            }
            var group=root.AddComponent<LODGroup>();group.SetLODs(new[]{new LOD(.18f,levels[0].ToArray()),new LOD(.07f,levels[1].ToArray()),new LOD(.015f,levels[2].ToArray())});group.RecalculateBounds();
            var collider=root.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;var rb=root.AddComponent<Rigidbody>();rb.mass=18;rb.isKinematic=true;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.linearDamping=.15f;rb.angularDamping=.4f;root.AddComponent<BigBotCannonDebris>();root.transform.localScale=joint.lossyScale;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/IronRequiem/Prefabs/Enemies/BigBot_FallenCannon.prefab");Object.DestroyImmediate(root);return prefab.GetComponent<BigBotCannonDebris>();
        }
        static ParticleSystem Effect(Transform parent,string name,bool electric)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=parent.position+Vector3.up*.08f;float scale=parent.lossyScale.x;go.transform.localScale=Vector3.one/scale;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.playOnAwake=false;main.loop=true;main.maxParticles=electric?16:12;main.startLifetime=electric?new ParticleSystem.MinMaxCurve(.08f,.22f):new ParticleSystem.MinMaxCurve(.45f,.8f);main.startSize=electric?new ParticleSystem.MinMaxCurve(.014f,.035f):new ParticleSystem.MinMaxCurve(.18f,.3f);main.startSpeed=electric?new ParticleSystem.MinMaxCurve(.6f,1.8f):new ParticleSystem.MinMaxCurve(.1f,.25f);main.startColor=electric?new Color(1,.62f,.1f):new Color(.15f,.16f,.17f,.3f);main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Shape;
            var emission=ps.emission;emission.rateOverTime=0;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.07f;
            var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.1f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var velocity=ps.velocityOverLifetime;velocity.enabled=!electric;velocity.space=ParticleSystemSimulationSpace.World;velocity.y=.45f;
            var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/VFX/CannonExplosion_IR.prefab");var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=template.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name==(electric?"HotSparks":"ResidualSmoke")).GetComponent<ParticleSystemRenderer>().sharedMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;return ps;
        }
        static AnimationClip BuildDeath(GameObject bot,SkinnedMeshRenderer[] skins)
        {
            var animator=bot.GetComponentInChildren<Animator>();var bones=skins[0].bones;var pos=bones.Select(b=>b.localPosition).ToArray();var rot=bones.Select(b=>b.localRotation).ToArray();var clip=new AnimationClip{name="BigBot_HeavyCollapse",frameRate=30};
            var curves=new AnimationCurve[bones.Length,7];for(int b=0;b<bones.Length;b++)for(int c=0;c<7;c++)curves[b,c]=new AnimationCurve();
            var root=Find(bot,"Root");var pelvis=Find(bot,"Pelvis");var torso=Find(bot,"Torso");var footL=Find(bot,"Foot_L");var footR=Find(bot,"Foot_R");Vector3 left=footL.position,right=footR.position;var bounds=new Bounds[skins.Length];var baked=new Mesh();var floorOffset=new AnimationCurve();Vector3 rootRest=root.localPosition;float lowest=float.PositiveInfinity;
            for(int frame=0;frame<=54;frame++)
            {
                float t=frame/30f;for(int i=0;i<bones.Length;i++){bones[i].localPosition=pos[i];bones[i].localRotation=rot[i];}
                float buckle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,.95f,t));float fall=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1.65f,t));
                pelvis.position+=bot.transform.up*(-.8f*buckle-.35f*fall)+bot.transform.forward*(.12f*buckle+.48f*fall);pelvis.rotation=Quaternion.AngleAxis(Mathf.Sin(t*22)*3*(1-fall),bot.transform.forward)*pelvis.rotation;
                torso.rotation=Quaternion.AngleAxis(18*buckle+62*fall,bot.transform.right)*torso.rotation;
                Leg(bot,"L",left);Leg(bot,"R",right);
                foreach(string side in new[]{"L","R"})foreach(string part in new[]{"Shoulder_","Forearm_"}){var bone=Find(bot,part+side);bone.rotation=Quaternion.AngleAxis(-20*fall,bot.transform.right)*bone.rotation;}
                float min=float.PositiveInfinity;
                foreach(var skin in skins){BakeLocal(skin,baked);foreach(var v in baked.vertices)min=Mathf.Min(min,skin.transform.TransformPoint(v).y);}
                float correction=Mathf.Max(0,bot.transform.position.y+.025f-min);floorOffset.AddKey(t,correction);root.position+=Vector3.up*correction;
                foreach(var skin in skins){BakeLocal(skin,baked);int i=Array.IndexOf(skins,skin);if(frame==0)bounds[i]=baked.bounds;else bounds[i].Encapsulate(baked.bounds);foreach(var v in baked.vertices)lowest=Mathf.Min(lowest,skin.transform.TransformPoint(v).y-bot.transform.position.y);}
                root.localPosition=rootRest;
                for(int b=0;b<bones.Length;b++){var p=bones[b].localPosition;var q=bones[b].localRotation;float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w};for(int c=0;c<7;c++)curves[b,c].AddKey(t,values[c]);}
            }
            string[] names={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
            for(int b=0;b<bones.Length;b++)for(int c=0;c<7;c++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bones[b],animator.transform),typeof(Transform),names[c]),curves[b,c]);clip.EnsureQuaternionContinuity();
            bot.GetComponent<BigBotDestruction>().ConfigureDeath(floorOffset);
            for(int i=0;i<skins.Length;i++){bounds[i].Expand(.0003f);skins[i].localBounds=bounds[i];}
            for(int b=0;b<bones.Length;b++){bones[b].localPosition=pos[b];bones[b].localRotation=rot[b];}Object.DestroyImmediate(baked);clip=Save(clip,Art+"/HeavyCollapse.anim");File.WriteAllText(Reports+"/death-geometry.txt","55 sampled poses; 45 actual skins/all LODs; minimum ground clearance "+lowest+" m. No root motion; floor correction on skeleton only.");return clip;
        }
        static void Leg(GameObject bot,string side,Vector3 foot)
        {
            var thigh=Find(bot,"Thigh_"+side);var shin=Find(bot,"Shin_"+side);var ankle=Find(bot,"Foot_"+side);var hip=thigh.position;float l1=Vector3.Distance(hip,shin.position),l2=Vector3.Distance(shin.position,ankle.position);var v=foot-hip;float d=Mathf.Clamp(v.magnitude,Mathf.Abs(l1-l2)+.001f,l1+l2-.001f);var direction=v.normalized;float a=(l1*l1-l2*l2+d*d)/(2*d);float height=Mathf.Sqrt(Mathf.Max(0,l1*l1-a*a));var bend=Vector3.ProjectOnPlane(bot.transform.forward,direction).normalized;var knee=hip+direction*a+bend*height;var ankleRotation=ankle.rotation;
            thigh.rotation=Quaternion.FromToRotation(shin.position-thigh.position,knee-hip)*thigh.rotation;shin.position=knee;shin.rotation=Quaternion.FromToRotation(ankle.position-shin.position,foot-knee)*shin.rotation;ankle.position=foot;ankle.rotation=ankleRotation;
        }
        static void BuildLab()
        {
            bool dirty=Dirty();var previous=SceneManager.GetActiveScene();if(!File.Exists(Lab))AssetDatabase.CopyAsset(BigBotCombatAuthoring.Lab,Lab);var scene=SceneManager.GetSceneByPath(Lab);if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(Lab,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var original=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BigBotCombat>(true)).Single().gameObject;var enemy=original.GetComponent<EnemyController>();var next=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene);next.name="Big Bot Destructible";next.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);next.transform.localScale=original.transform.localScale;
            foreach(var path in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PatrolPath>(true)))for(int i=0;i<path.EnemiesToAssign.Count;i++)if(path.EnemiesToAssign[i]==enemy)path.EnemiesToAssign[i]=next.GetComponent<EnemyController>();Object.DestroyImmediate(original);
            string navPath=Art+"/BigBotDestruction_NavMesh.asset";if(!File.Exists(navPath))AssetDatabase.CopyAsset(BigBotCombatAuthoring.Art+"/BigBotLab_NavMesh.asset",navPath);var surface=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Unity.AI.Navigation.NavMeshSurface>()).Single();surface.RemoveData();surface.navMeshData=AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(navPath);surface.AddData();EditorUtility.SetDirty(surface);EditorSceneManager.SaveScene(scene);
            if(dirty){EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}else EditorSceneManager.OpenScene(Lab);
        }
        sealed class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}
            public void RunFinished(ITestResultAdaptor r){if(!SessionState.GetBool(Key+".testing",false))return;SessionState.EraseBool(Key+".testing");TestRunnerApi.SaveResultToFile(r,Reports+"/playmode.xml");File.WriteAllText(Reports+"/test-status.txt",r.ResultState+" passed="+r.PassCount+" failed="+r.FailCount);SessionState.SetBool(Key+".restore",true);SessionState.SetFloat(Key+".at",(float)EditorApplication.timeSinceStartup);}
        }
    }
}
