using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace IronRequiem.Editor
{
    public static class CampaignCurrentAssets
    {
        const string Folder="Assets/IronRequiem/Prefabs/Campaign",VO="Assets/IronRequiem/Prefabs/VisualOverhaul/",Latest="Assets/IronRequiem/Prefabs/SecondaryShotgun/";
        static GameObject Variant(string source,string name)
        {
            string path=Folder+"/"+name+".prefab";
            if(!File.Exists(path)){var preview=EditorSceneManager.NewPreviewScene();try{var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source),preview);PrefabUtility.SaveAsPrefabAsset(instance,path);}finally{EditorSceneManager.ClosePreviewScene(preview);}}
            return PrefabUtility.LoadPrefabContents(path);
        }
        static void Save(GameObject root,string name){PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");PrefabUtility.UnloadPrefabContents(root);}
        static string PathOf(Transform t,Transform root)=>AnimationUtility.CalculateTransformPath(t,root);
        static void Flag(Object target,string name,bool value){var data=new SerializedObject(target);var p=data.FindProperty(name);if(p!=null){p.boolValue=value;data.ApplyModifiedPropertiesWithoutUndo();}}
        static void Policy(GameObject root)
        {
            foreach(var joint in root.GetComponentsInChildren<DetachableCannon>(true))Flag(joint,"executionOnly",true);
            var damage=root.GetComponent<BigBotDestruction>();if(damage)Flag(damage,"executionOnly",true);
            var execution=root.GetComponent<EnemyExecutionSource>();if(execution){var data=new SerializedObject(execution);data.FindProperty("healthThreshold").floatValue=.75f;data.ApplyModifiedPropertiesWithoutUndo();}
            var enemy=root.GetComponent<EnemyController>();if(enemy&&enemy.LootPrefab&&(enemy.LootPrefab.GetComponentInChildren<StolenCannon>(true)||enemy.LootPrefab.GetComponentInChildren<Unity.FPS.Gameplay.WeaponPickup>(true)))enemy.DropRate=0;
            var indicators=root.GetComponent<EnemyPowerIndicators>();
            if(!indicators||indicators.Indicators.Length==0)
            {
                var eyes=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.IndexOf("eye",StringComparison.OrdinalIgnoreCase)>=0||r.name=="CoreIndicator").ToArray();
                if(eyes.Length>0){if(!indicators)indicators=root.AddComponent<EnemyPowerIndicators>();indicators.Configure(eyes);}
            }
            if((!indicators||indicators.Indicators.Length==0)&&enemy.EyeColorMaterial)
            {
                var old=enemy.EyeColorMaterial;var views=new List<Renderer>();var slots=new List<int>();
                foreach(var view in root.GetComponentsInChildren<Renderer>(true)){var materials=view.sharedMaterials;for(int i=0;i<materials.Length;i++)if(materials[i]==old){views.Add(view);slots.Add(i);}}
                if(views.Count>0)
                {
                    string path=CampaignAuthoring.Art+"/SharedRobotEyeSlots.mat";var eye=AssetDatabase.LoadAssetAtPath<Material>(path);if(!eye){eye=new Material(old);eye.SetColor("_EmissionColor",new Color(.15f,2.3f,2.8f));eye.EnableKeyword("_EMISSION");AssetDatabase.CreateAsset(eye,path);}
                    for(int i=0;i<views.Count;i++){var materials=views[i].sharedMaterials;materials[slots[i]]=eye;views[i].sharedMaterials=materials;}enemy.EyeColorMaterial=eye;enemy.DefaultEyeColor=new Color(.15f,2.3f,2.8f);enemy.AttackEyeColor=new Color(3.4f,.2f,.08f);if(!indicators)indicators=root.AddComponent<EnemyPowerIndicators>();indicators.ConfigureMaterialSlots(views.ToArray(),slots.ToArray());
                }
            }
            if(indicators&&indicators.Indicators.Length>0&&!indicators.UsesMaterialSlots)
            {
                string eyePath=CampaignAuthoring.Art+"/SharedRobotEyes.mat";var eye=AssetDatabase.LoadAssetAtPath<Material>(eyePath);if(!eye){eye=new Material(Shader.Find("Universal Render Pipeline/Unlit"));eye.SetColor("_BaseColor",new Color(.15f,2.3f,2.8f));AssetDatabase.CreateAsset(eye,eyePath);}foreach(var view in indicators.Indicators)if(view)view.sharedMaterial=eye;
                var lights=root.GetComponent<EnemyStateLights>()??root.AddComponent<EnemyStateLights>();lights.Configure(indicators.Indicators);
                var data=new SerializedObject(lights);data.FindProperty("patrol").colorValue=new Color(.15f,2.3f,2.8f);data.FindProperty("alert").colorValue=new Color(3,1.6f,.12f);data.FindProperty("attack").colorValue=new Color(3.4f,.2f,.08f);data.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        static string Present(string source,string look,string name)
        {
            var root=Variant(source,name);var reference=AssetDatabase.LoadAssetAtPath<GameObject>(look);
            try
            {
                var views=root.GetComponentsInChildren<Renderer>(true).ToDictionary(r=>PathOf(r.transform,root.transform));
                foreach(var renderer in reference.GetComponentsInChildren<Renderer>(true))if(views.TryGetValue(PathOf(renderer.transform,reference.transform),out var target))target.sharedMaterials=renderer.sharedMaterials;
                root.GetComponent<EnemyController>().BodyMaterial=reference.GetComponent<EnemyController>().BodyMaterial;
                root.GetComponent<EnemyController>().EyeColorMaterial=reference.GetComponent<EnemyController>().EyeColorMaterial;
                var hit=root.GetComponent<RobotHitFeedback>();var srcHit=reference.GetComponent<RobotHitFeedback>();if(hit&&srcHit){var a=new SerializedObject(hit);var b=new SerializedObject(srcHit);a.FindProperty("sparkCount").intValue=b.FindProperty("sparkCount").intValue;a.ApplyModifiedPropertiesWithoutUndo();}
                var death=root.GetComponent<PooledEnemyDeathBurst>();var srcDeath=reference.GetComponent<PooledEnemyDeathBurst>();if(death&&srcDeath){var a=new SerializedObject(death);a.FindProperty("effectPrefab").objectReferenceValue=new SerializedObject(srcDeath).FindProperty("effectPrefab").objectReferenceValue;a.ApplyModifiedPropertiesWithoutUndo();}
                Policy(root);Save(root,name);root=null;
            }finally{if(root)PrefabUtility.UnloadPrefabContents(root);}
            return Folder+"/"+name+".prefab";
        }
        static Material Env(string name)=>AssetDatabase.LoadAssetAtPath<Material>(VisualOverhaulAssets.Mats+"/"+name+".mat");
        public static void RefreshPolicies()
        {
            var log=new StringBuilder();foreach(string name in new[]{"T07_Current_Repaired","BigBot_Current","HoverBot_Current","Turret_Current","SawBot_Current"})
            {
                var root=PrefabUtility.LoadPrefabContents(Folder+"/"+name+".prefab");try{Policy(root);var eyes=root.GetComponent<EnemyPowerIndicators>();if(!eyes||eyes.Indicators.Length==0)throw new Exception("No measured eye/status geometry: "+name);log.AppendLine(name+" emissive renderers="+eyes.Indicators.Length+" nativeLights="+root.GetComponentsInChildren<Light>(true).Length+" execution="+(root.GetComponent<EnemyExecutionSource>()?"strict below75%":"no compatible module"));PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");}finally{PrefabUtility.UnloadPrefabContents(root);}
            }AssetDatabase.SaveAssets();File.WriteAllText(CampaignAuthoring.Report+"/eyes-policy.txt",log.ToString());
        }
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var log=new StringBuilder();
            string t07Name="T07_Current_Repaired",bigName="BigBot_Current",sawName="SawBot_Current";
            var t07=Variant(VO+"T07_HitFeedback_v04_NavThrottle_VO.prefab",t07Name);var legs=AssetDatabase.LoadAssetAtPath<GameObject>(VO+"T07_LegRepair_Charged_v05_Execution_VO.prefab");
            try
            {
                foreach(var skin in t07.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.name.StartsWith("Body_LOD")))
                {
                    var repaired=legs.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.name==skin.name);var bones=skin.bones.ToDictionary(b=>b.name);if(repaired.bones.Any(b=>!bones.ContainsKey(b.name)))throw new Exception("Different T07 skeleton; preserve original");skin.sharedMesh=repaired.sharedMesh;skin.bones=repaired.bones.Select(b=>bones[b.name]).ToArray();skin.localBounds=repaired.localBounds;
                }
                Policy(t07);log.AppendLine("T07: latest NavThrottle+VO+native charged attack, repaired3LOD meshes and strict75% execution. Health="+t07.GetComponent<Health>().MaxHealth);Save(t07,t07Name);t07=null;
            }finally{if(t07)PrefabUtility.UnloadPrefabContents(t07);}
            var big=Variant(VO+"BigBot_Burst_v10_NavThrottle_VO.prefab",bigName);Policy(big);log.AppendLine("BigBot: latest NavThrottle/VO/explosive6-shot burst; Health="+big.GetComponent<Health>().MaxHealth);Save(big,bigName);
            var saw=Variant("Assets/IronRequiem/Prefabs/Enemies/SawBot_SawPolish_v04.prefab",sawName);Policy(saw);log.AppendLine("SawBot: existing SawPolish v04, sustained saws/audio/death; no automatic pickup drop.");Save(saw,sawName);
            string drone=Present(Latest+"HoverBot_CombatPolish_NavThrottle.prefab",VO+"HoverBot_CombatPolish_VO.prefab","HoverBot_Current");
            string turret=Present("Assets/IronRequiem/Prefabs/CombatFollowup/Turret_CombatPolish.prefab",VO+"Turret_CombatPolish_VO.prefab","Turret_Current");
            var map=new Dictionary<string,string>{{"Assets/IronRequiem/Prefabs/PhaseFlow/T07_LegRepair_Charged_v05_Execution.prefab",Folder+"/"+t07Name+".prefab"},{"Assets/IronRequiem/Prefabs/PhaseFlow/BigBot_Burst_v10_Execution.prefab",Folder+"/"+bigName+".prefab"},{"Assets/IronRequiem/Prefabs/Enemies/HoverBot_CombatPolish.prefab",drone},{"Assets/IronRequiem/Prefabs/Enemies/Turret_CombatPolish.prefab",turret},{"Assets/IronRequiem/Prefabs/Enemies/SawBot_SawPolish_v04.prefab",Folder+"/"+sawName+".prefab"}};
            foreach(string path in new[]{CampaignAuthoring.Main}.Concat(CampaignAuthoring.Names.Select(n=>CampaignAuthoring.Folder+"/"+n+".unity")))
            {
                var scene=EditorSceneManager.OpenScene(path);var roots=scene.GetRootGameObjects();int changed=VisualOverhaulScenes.RemapPrefabs(roots,map);
                foreach(var renderer in roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)))
                {
                    if(renderer is ParticleSystemRenderer)continue;var materials=renderer.sharedMaterials;bool swap=false;
                    for(int i=0;i<materials.Length;i++)
                    {
                        if(!materials[i]||!materials[i].name.StartsWith("Bunker_"))continue;string n=materials[i].name.Substring(7);string target=n=="Floor"?"VO_Env_Floor":n=="Wall"?(renderer.name.Contains("Roof")||renderer.name.Contains("Ceiling")?"VO_Env_Ceiling":"VO_Env_Wall"):n=="RustMetal"?"VO_Env_RustMetal":n=="Hazard"?"VO_Env_Hazard":n=="LampCold"?"VO_Light_Cold":n=="LampRed"?"VO_Light_Red":n=="Grating"?"VO_Env_Grating":null;
                        if(target!=null&&Env(target)){materials[i]=Env(target);swap=true;}
                    }
                    if(swap){renderer.sharedMaterials=materials;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Simple;}
                }
                foreach(var light in roots.SelectMany(r=>r.GetComponentsInChildren<Light>(true)).Where(l=>l.type!=LightType.Directional)){light.intensity=4.4f;if(path==CampaignAuthoring.Main)light.range=12;}
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.55f,.6f);
                if(path!=CampaignAuthoring.Main)
                {
                    int stage=Array.FindIndex(CampaignAuthoring.Names,n=>path.EndsWith(n+".unity"));var probe=new GameObject("SectorReflection").AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Baked;probe.boxProjection=true;probe.resolution=128;probe.hdr=true;probe.intensity=1;probe.blendDistance=3;probe.transform.position=stage==0?new Vector3(0,3,0):new Vector3(0,12,new[]{0f,10f,65f,129f,276f}[stage]);probe.size=stage==4?new Vector3(20,9,224):new Vector3(54,9,56);
                    if(!Lightmapping.BakeReflectionProbe(probe,CampaignAuthoring.Art+"/"+CampaignAuthoring.Names[stage]+"_Reflection.exr"))throw new Exception("Reflection bake failed "+path);
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);log.AppendLine(Path.GetFileName(path)+" current enemy refs="+changed+"; shared HardSurface set, fixed mounted fixtures4.4, ambient .50/.55/.60.");
            }
            AssetDatabase.SaveAssets();File.WriteAllText(CampaignAuthoring.Report+"/current-assets.txt",log.ToString());EditorSceneManager.OpenScene(CampaignAuthoring.Main);
        }
    }
}
