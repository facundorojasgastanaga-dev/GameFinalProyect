using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
    [InitializeOnLoad]public static class BigBotChargeReferenceAuthoring
    {
        public const string Weapon="Assets/IronRequiem/Prefabs/Weapons/Weapon_BigBot_ReferenceCharge_v09.prefab",Pickup="Assets/IronRequiem/Prefabs/Weapons/BigBot_ReferenceChargePickup_v09.prefab",Enemy="Assets/IronRequiem/Prefabs/Enemies/BigBot_ReferenceCharge_v09.prefab",Lab="Assets/IronRequiem/Scenes/BigBotChargeReferenceLab.unity",Effect="Assets/IronRequiem/Prefabs/VFX/BigBot_ReferenceCharge_v09.prefab",Art="Assets/IronRequiem/VFX/BigBotChargeV09",Reports="Tools/IronRequiem/Reports/BigBotChargeReference";
        const string Queue="Tools/IronRequiem/Requests/bigbot-charge-reference.txt",Key="IRON.BigBotChargeReference";static readonly DateTime LoadedAt=DateTime.UtcNow;static TestRunnerApi api;
        static BigBotChargeReferenceAuthoring(){EditorApplication.update+=Tick;if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Register;}
        static bool Dirty()=>Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty);
        static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool(Key,false))return;
            if(SessionState.GetBool(Key+".restore",false)){SessionState.EraseBool(Key+".restore");if(!Dirty())EditorSceneManager.OpenScene(Lab);IronRequiemSetupTools.WriteAudit();}
            if(!File.Exists(Queue))return;foreach(var p in new[]{"Assets/IronRequiem/Editor/BigBotChargeReferenceAuthoring.cs","Assets/IronRequiem/Tests/PlayMode/BigBotChargeReferenceTests.cs","Assets/IronRequiem/Scripts/Weapons/CannonChargePresentation.cs"})if(File.GetLastWriteTimeUtc(p)>LoadedAt)return;
            var op=File.ReadAllText(Queue).Trim();File.Delete(Queue);Directory.CreateDirectory(Reports);
            try{if(Dirty())throw new InvalidOperationException("Preserving dirty user scene");if(op=="build"){Build();File.WriteAllText(Queue,"test");}else if(op=="repair-curves"){Filaments("CurvedBlueFunnel",.005f);Filaments("CurvedWhiteThreads",.00125f);AssetDatabase.SaveAssets();File.WriteAllText(Queue,"test");}else if(op=="polish"){Polish();File.WriteAllText(Queue,"test");}else if(op=="test"){Register();SessionState.SetBool(Key,true);api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{"IronRequiem.Tests.BigBotChargeReferenceTests"}}));}else throw new InvalidOperationException("Unknown reference-charge action");}
            catch(Exception e){File.WriteAllText(Reports+"/error.txt",e.ToString());Debug.LogException(e);}
        }
        static void Variant(string source,string destination)
        {if(File.Exists(destination))throw new InvalidOperationException("Preserve existing "+destination);var scene=EditorSceneManager.NewPreviewScene();try{var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source),scene);go.name=Path.GetFileNameWithoutExtension(destination);PrefabUtility.SaveAsPrefabAsset(go,destination);}finally{EditorSceneManager.ClosePreviewScene(scene);}}
        static Material Material(string name,Color color)
        {string path=Art+"/"+name+".mat";AssetDatabase.CopyAsset(CannonChargeUpgrade.Vfx+"/EnergyFrame.mat",path);var material=AssetDatabase.LoadAssetAtPath<Material>(path);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/IronRequiem/VFX/Mechanics/ParticleSoftCircle.asset"));material.SetColor("_BaseColor",color);material.SetFloat("_Cull",(float)CullMode.Off);EditorUtility.SetDirty(material);return material;}
        static Mesh Mesh(string name,List<Vector3> vertices,List<Vector2> uv,List<int> indices)
        {var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();string path=Art+"/"+name+".asset";if(vertices.Any(v=>float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z)))throw new InvalidOperationException("Nonfinite mesh vertex: "+name);var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);return existing;}AssetDatabase.CreateAsset(mesh,path);return mesh;}
        static void Quad(List<Vector3> v,List<Vector2> uv,List<int> ix,Vector3 a,Vector3 b,Vector3 c,Vector3 d,bool stripe=false)
        {int n=v.Count;v.AddRange(new[]{a,b,c,d});uv.AddRange(stripe?new[]{new Vector2(0,.5f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(0,.5f)}:new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});ix.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
        static Mesh Filaments(string name,float width)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var ix=new List<int>();
            Vector3 Point(float t,float angle){float radius=.145f*Mathf.Pow(1-t,.72f)+.004f;float phi=angle+.7f*t*t;return new Vector3(Mathf.Cos(phi)*radius,Mathf.Sin(phi)*radius,Mathf.Lerp(-.055f,.045f,t));}
            for(int strand=0;strand<8;strand++)for(int j=0;j<20;j++)
            {
                float a=j/20f,b=(j+1)/20f,angle=strand*Mathf.PI/4;var p=Point(a,angle);var q=Point(b,angle);var tangent=(q-p).normalized;var side=Vector3.Cross(tangent,Vector3.forward).normalized;var other=Vector3.Cross(tangent,side).normalized;float wa=width*Mathf.Pow(Mathf.Max(0,Mathf.Sin(a*Mathf.PI)),.7f),wb=width*Mathf.Pow(Mathf.Max(0,Mathf.Sin(b*Mathf.PI)),.7f);
                foreach(var axis in new[]{side,other})Quad(v,uv,ix,p-axis*wa,p+axis*wa,q+axis*wb,q-axis*wb,true);
            }
            return Mesh(name,v,uv,ix);
        }
        static Mesh Arcs(string name,float radius,float width,int segments,float span)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var ix=new List<int>();
            for(int ring=0;ring<segments;ring++)for(int j=0;j<24;j++){float a=(ring*360f/segments+span*j/24)*Mathf.Deg2Rad,b=(ring*360f/segments+span*(j+1)/24)*Mathf.Deg2Rad;Vector3 P(float angle,float r)=>new Vector3(Mathf.Cos(angle)*r,Mathf.Sin(angle)*r,0);Quad(v,uv,ix,P(a,radius-width),P(a,radius+width),P(b,radius+width),P(b,radius-width),true);}
            return Mesh(name,v,uv,ix);
        }
        static Mesh Core()
        {var v=new List<Vector3>();var uv=new List<Vector2>();var ix=new List<int>();foreach(var rotation in new[]{Quaternion.identity,Quaternion.Euler(0,90,0),Quaternion.Euler(90,0,0)})Quad(v,uv,ix,rotation*new Vector3(-.5f,-.5f,0),rotation*new Vector3(.5f,-.5f,0),rotation*new Vector3(.5f,.5f,0),rotation*new Vector3(-.5f,.5f,0));return Mesh("WhiteBlueCore",v,uv,ix);}
        static Mesh Marks()
        {var v=new List<Vector3>();var uv=new List<Vector2>();var ix=new List<int>();for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5+.3f;var pos=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.13f;var rotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg+25);Quad(v,uv,ix,pos+rotation*new Vector3(-.009f,-.005f,0),pos+rotation*new Vector3(.009f,-.005f,0),pos+rotation*new Vector3(.009f,.005f,0),pos+rotation*new Vector3(-.009f,.005f,0));}return Mesh("OrbitFragments",v,uv,ix);}
        static Transform Node(Transform parent,string name,Mesh mesh,Material material,Vector3 position=default)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return go.transform;}
        static void Build()
        {
            Directory.CreateDirectory(Art);AssetDatabase.Refresh();var blue=Material("BlueFilaments",new Color(.12f,.75f,2.8f,1));var white=Material("WhiteFilaments",new Color(1.5f,2.1f,3,1));var violet=Material("VioletArcs",new Color(2,.1f,3,1));var motes=Material("BlueMotes",new Color(.3f,.8f,2.8f,1));
            var ribbons=Filaments("CurvedBlueFunnel",.005f);var cores=Filaments("CurvedWhiteThreads",.00125f);var outer=Arcs("SegmentedOuterArcs",.108f,.0035f,4,52);var inner=Arcs("SegmentedInnerArcs",.078f,.0025f,3,65);var pulse=Arcs("BlueOnsetRing",1,.045f,1,360);var core=Core();var marks=Marks();
            Variant(CannonChargeUpgrade.ChargePrefab,Effect);var fx=PrefabUtility.LoadPrefabContents(Effect);
            try
            {
                foreach(Transform child in fx.transform)child.gameObject.SetActive(false);
                var center=new GameObject("ReferenceCore").transform;center.SetParent(fx.transform,false);center.localPosition=Vector3.forward*.012f;Node(center,"HotWhiteCore",core,white);var halo=Node(center,"SoftBlueHalo",core,blue);halo.localScale=Vector3.one*2;
                Node(fx.transform,"BlueFilaments",ribbons,blue);Node(fx.transform,"WhiteFilaments",cores,white);Node(fx.transform,"VioletOuter",outer,violet,Vector3.forward*.23f);Node(fx.transform,"VioletInner",inner,violet,Vector3.forward*.205f);Node(fx.transform,"OrbitMarks",marks,violet,Vector3.forward*.225f);Node(fx.transform,"BluePulseRing",pulse,blue,Vector3.forward*.008f);
                var go=new GameObject("ReferenceMotes");go.transform.SetParent(fx.transform,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.playOnAwake=false;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Local;main.maxParticles=24;main.startLifetime=new ParticleSystem.MinMaxCurve(.35f,.7f);main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(.004f,.012f);main.startColor=new Color(.5f,.8f,1,1);main.gravityModifier=0;var emission=ps.emission;emission.rateOverTime=9;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.24f;shape.radiusThickness=.15f;var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;velocity.radial=-.36f;velocity.orbitalZ=1.8f;var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(.2f,.5f,1),0),new GradientColorKey(new Color(.8f,.95f,1),.7f)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(1,.7f),new GradientAlphaKey(0,1)});color.color=gradient;ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=motes;
                fx.SetActive(false);PrefabUtility.SaveAsPrefabAsset(fx,Effect);
            }finally{PrefabUtility.UnloadPrefabContents(fx);}
            Variant(BigBotBazookaAuthoring.Weapon,Weapon);var weapon=PrefabUtility.LoadPrefabContents(Weapon);
            try
            {
                var w=weapon.GetComponent<WeaponController>();var previous=weapon.GetComponent<CannonChargePresentation>().EffectOrigin;previous.name="PreservedCharge_v08";previous.gameObject.SetActive(false);
                var effect=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Effect),weapon.scene);effect.name="ChargeEnergy_Reference";effect.transform.SetParent(w.WeaponMuzzle,false);effect.transform.localPosition=Vector3.zero;effect.transform.localRotation=Quaternion.identity;effect.transform.localScale=Vector3.one;int layer=w.WeaponMuzzle.gameObject.layer;foreach(var t in effect.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;
                Transform N(string name)=>effect.transform.Find(name);var presentation=weapon.GetComponent<CannonChargePresentation>();presentation.Configure(effect,N("ReferenceCore"),N("ReferenceMotes").GetComponent<ParticleSystem>());presentation.ConfigureFrames(null,null,null);presentation.ConfigureReference(N("BlueFilaments"),N("WhiteFilaments"),N("VioletOuter"),N("VioletInner"),N("OrbitMarks"),N("BluePulseRing"));PrefabUtility.SaveAsPrefabAsset(weapon,Weapon);
            }finally{PrefabUtility.UnloadPrefabContents(weapon);}
            Variant(BigBotBazookaAuthoring.Pickup,Pickup);var pickup=PrefabUtility.LoadPrefabContents(Pickup);try{pickup.GetComponent<StolenCannon>().Configure(AssetDatabase.LoadAssetAtPath<GameObject>(Weapon).GetComponent<WeaponController>());PrefabUtility.SaveAsPrefabAsset(pickup,Pickup);}finally{PrefabUtility.UnloadPrefabContents(pickup);}
            Variant(BigBotBazookaAuthoring.Enemy,Enemy);var enemy=PrefabUtility.LoadPrefabContents(Enemy);try{enemy.GetComponent<BigBotDestruction>().ConfigureCollectibleCannon(AssetDatabase.LoadAssetAtPath<GameObject>(Pickup).GetComponent<StolenCannon>());PrefabUtility.SaveAsPrefabAsset(enemy,Enemy);}finally{PrefabUtility.UnloadPrefabContents(enemy);}
            if(File.Exists(Lab))throw new InvalidOperationException("Preserve lab");AssetDatabase.CopyAsset(BigBotBazookaAuthoring.Lab,Lab);var scene=EditorSceneManager.OpenScene(Lab);var old=Object.FindAnyObjectByType<BigBotCombat>().gameObject;var replacement=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Enemy),scene);replacement.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);replacement.transform.localScale=old.transform.localScale;foreach(var route in Object.FindObjectsByType<Unity.FPS.AI.PatrolPath>())for(int i=0;i<route.EnemiesToAssign.Count;i++)if(route.EnemiesToAssign[i]==old.GetComponent<Unity.FPS.AI.EnemyController>())route.EnemiesToAssign[i]=replacement.GetComponent<Unity.FPS.AI.EnemyController>();Object.DestroyImmediate(old);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText(Reports+"/build.txt","Reference inspected before asset edits: one charge, 364x236, 30 fps, 7.3 s. Blue/white curved funnel, segmented magenta arcs ahead of muzzle, orbital fragments, blue motes and release flare. Reference release around 5.27 s mapped to existing 1.2 s charge. Same 0.085 s release presentation; no new projectile/beam/trail. New mesh/material copies, local particles, no lights. AI unchanged; new robot variant only references the visual weapon variant through its pickup.");
        }
        static void Polish()
        {
            Filaments("CurvedBlueFunnel",.012f);Filaments("CurvedWhiteThreads",.0018f);Arcs("SegmentedOuterArcs",.108f,.006f,4,52);Arcs("SegmentedInnerArcs",.078f,.0045f,3,65);
            foreach(var pair in new[]{("BlueFilaments",new Color(.18f,1.2f,4,1)),("WhiteFilaments",new Color(2.2f,3.6f,5.5f,1)),("VioletArcs",new Color(3.5f,.25f,5.5f,1)),("BlueMotes",new Color(.4f,1,3.5f,1))}){var m=AssetDatabase.LoadAssetAtPath<Material>(Art+"/"+pair.Item1+".mat");m.SetColor("_BaseColor",pair.Item2);EditorUtility.SetDirty(m);}
            var fx=PrefabUtility.LoadPrefabContents(Effect);try{fx.transform.Find("ReferenceCore").localPosition=Vector3.forward*.045f;fx.transform.Find("VioletOuter").localPosition=Vector3.forward*.18f;fx.transform.Find("VioletInner").localPosition=Vector3.forward*.16f;fx.transform.Find("OrbitMarks").localPosition=Vector3.forward*.175f;var main=fx.transform.Find("ReferenceMotes").GetComponent<ParticleSystem>().main;main.startSize=new ParticleSystem.MinMaxCurve(.006f,.018f);PrefabUtility.SaveAsPrefabAsset(fx,Effect);}finally{PrefabUtility.UnloadPrefabContents(fx);}AssetDatabase.SaveAssets();
        }
        sealed class Results:IErrorCallbacks
        {public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}public void OnError(string m){SessionState.EraseBool(Key);File.WriteAllText(Reports+"/runner-error.txt",m);}public void RunFinished(ITestResultAdaptor r){if(!SessionState.GetBool(Key,false))return;SessionState.EraseBool(Key);TestRunnerApi.SaveResultToFile(r,Reports+"/play.xml");File.WriteAllText(Reports+"/status.txt",r.ResultState+" passed="+r.PassCount+" failed="+r.FailCount);SessionState.SetBool(Key+".restore",true);}}
    }
}
