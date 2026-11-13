using System;
using System.IO;
using System.Linq;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
    [InitializeOnLoad] public static class BigBotBazookaAuthoring
    {
        public const string Enemy="Assets/IronRequiem/Prefabs/Enemies/BigBot_Explosive_v08.prefab", Weapon="Assets/IronRequiem/Prefabs/Weapons/Weapon_BigBot_Bazooka_v08.prefab", Pickup="Assets/IronRequiem/Prefabs/Weapons/BigBot_BazookaPickup_v08.prefab", Lab="Assets/IronRequiem/Scenes/BigBotBazookaLab.unity", Reports="Tools/IronRequiem/Reports/BigBotBazooka";
        const string EnemyProjectile="Assets/IronRequiem/Prefabs/Projectiles/Projectile_BigBot_Explosive_v08.prefab", PlayerProjectile="Assets/IronRequiem/Prefabs/Projectiles/Projectile_BigBot_StolenExplosive_v08.prefab", Impact="Assets/IronRequiem/Prefabs/VFX/BigBot_ProjectileImpact_v08.prefab", StolenImpact="Assets/IronRequiem/Prefabs/VFX/BigBot_StolenImpact_v08.prefab", Queue="Tools/IronRequiem/Requests/bigbot-bazooka.txt", Key="IRON.BigBotBazooka";
        static readonly DateTime LoadedAt=DateTime.UtcNow; static TestRunnerApi api;
        static BigBotBazookaAuthoring(){EditorApplication.update+=Tick;if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Register;}
        static bool Dirty()=>Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty);
        static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool(Key,false))return;
            if(SessionState.GetBool(Key+".restore",false)){SessionState.EraseBool(Key+".restore");if(!Dirty())EditorSceneManager.OpenScene(Lab);IronRequiemSetupTools.WriteAudit();}
            if(!File.Exists(Queue))return;
            foreach(var p in new[]{"Assets/IronRequiem/Editor/BigBotBazookaAuthoring.cs","Assets/IronRequiem/Tests/PlayMode/BigBotBazookaTests.cs","Assets/IronRequiem/Scripts/AI/BigBotCombat.cs","Assets/FPS/Scripts/Game/Shared/DamageArea.cs"})if(File.GetLastWriteTimeUtc(p)>LoadedAt)return;
            string op=File.ReadAllText(Queue).Trim();File.Delete(Queue);Directory.CreateDirectory(Reports);
            try{if(Dirty())throw new InvalidOperationException("Preserving dirty user scene");if(op=="build"){Build();File.WriteAllText(Queue,"test");}else if(op=="test"||op=="test-stolen"){Register();SessionState.SetString(Key+".label",op=="test-stolen"?"-stolen":"");SessionState.SetBool(Key,true);api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=op=="test-stolen"?new[]{"IronRequiem.Tests.BigBotBazookaTests.StolenBazookaFramingChargeThrowAndOverdrive"}:new[]{"IronRequiem.Tests.BigBotBazookaTests"}}));}else if(op=="audit")IronRequiemSetupTools.WriteAudit();else throw new InvalidOperationException("Unknown bazooka operation");}
            catch(Exception e){File.WriteAllText(Reports+"/error.txt",e.ToString());Debug.LogException(e);}
        }
        static void Variant(string source,string path)
        {if(File.Exists(path))throw new InvalidOperationException("Preserve existing "+path);var scene=EditorSceneManager.NewPreviewScene();try{var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source),scene);go.name=Path.GetFileNameWithoutExtension(path);PrefabUtility.SaveAsPrefabAsset(go,path);}finally{EditorSceneManager.ClosePreviewScene(scene);}}
        static void Set(SerializedObject data,string name,float value){var p=data.FindProperty(name);if(p==null)throw new InvalidOperationException(name);if(p.propertyType==SerializedPropertyType.Boolean)p.boolValue=value!=0;else if(p.propertyType==SerializedPropertyType.Integer)p.intValue=(int)value;else p.floatValue=value;}
        static void Effect(string path,float scale)
        {
            Variant("Assets/IronRequiem/Prefabs/VFX/CannonExplosion_IR.prefab",path);var go=PrefabUtility.LoadPrefabContents(path);
            try{go.transform.localScale=Vector3.one*scale;var presentation=go.GetComponent<CannonExplosionPresentation>();var data=new SerializedObject(presentation);Set(data,"duration",1.8f);Set(data,"shockwaveDuration",.24f);Set(data,"shakeStrength",0);data.FindProperty("flash").objectReferenceValue=null;data.ApplyModifiedPropertiesWithoutUndo();foreach(var l in go.GetComponentsInChildren<Light>(true))l.gameObject.SetActive(false);
                foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true)){var main=ps.main;main.loop=false;main.maxParticles=Mathf.Min(main.maxParticles,20);main.scalingMode=ParticleSystemScalingMode.Hierarchy;var emission=ps.emission;var bursts=new ParticleSystem.Burst[emission.burstCount];emission.GetBursts(bursts);for(int i=0;i<bursts.Length;i++)bursts[i].count=new ParticleSystem.MinMaxCurve(Mathf.Min(8,bursts[i].count.constantMax));emission.SetBursts(bursts);var lights=ps.lights;lights.enabled=false;}
                var audio=go.GetComponent<AudioSource>();audio.volume=.35f;audio.spatialBlend=1;audio.maxDistance=35;PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        static void Projectile(string source,string path,string effect,float damage,float radius,bool enemy)
        {
            Variant(source,path);var go=PrefabUtility.LoadPrefabContents(path);try{var p=go.GetComponent<ProjectileStandard>();var area=go.GetComponent<DamageArea>();if(!area)area=go.AddComponent<DamageArea>();area.AreaOfEffectDistance=radius;area.DamageRatioOverDistance=new AnimationCurve(new Keyframe(0,1),new Keyframe(.2f,.9f),new Keyframe(1,.25f));area.RespectObstacles=true;area.IgnoreOwner=true;area.IgnoreSameAffiliation=true;p.AreaOfDamage=area;p.ImpactVfx=AssetDatabase.LoadAssetAtPath<GameObject>(effect);p.ImpactVfxLifetime=2;p.ImpactVfxSpawnOffset=.025f;p.ImpactSfxClip=null;if(enemy){p.Damage=damage;p.Speed=22;p.MaxLifeTime=48f/22;}PrefabUtility.SaveAsPrefabAsset(go,path);}finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        static void Build()
        {
            var oldEnemy=AssetDatabase.LoadAssetAtPath<GameObject>(HitWeaponAuthoring.Big).GetComponent<BigBotCombat>();var oldWeapon=AssetDatabase.LoadAssetAtPath<GameObject>(ModularArmAuthoring.BigWeapon).GetComponent<WeaponController>();
            var oldData=new SerializedObject(oldEnemy);var charge=oldWeapon.ProjectilePrefab.GetComponent<ChargedProjectileDamage>();
            File.WriteAllText(Reports+"/before.txt","Enemy damage="+oldEnemy.Damage+" blast="+(oldEnemy.Cannons[0].ProjectilePrefab.GetComponent<ProjectileStandard>().AreaOfDamage?"yes":"no")+" speed="+oldEnemy.ProjectileSpeed+" range="+oldEnemy.ProjectileRange+" interval="+oldData.FindProperty("shotInterval").floatValue+" prepare="+oldEnemy.PreparationTime+" attack="+oldEnemy.AttackDistance+" detect="+oldData.FindProperty("detectionDistance").floatValue+" emergency multiplier="+oldEnemy.EmergencyCadenceMultiplier+"\nStolen damage="+charge.MinimumDamage+".."+charge.MaximumDamage+" speed="+oldWeapon.ProjectilePrefab.GetComponent<ProjectileStandard>().Speed+" blast="+(oldWeapon.ProjectilePrefab.GetComponent<ProjectileStandard>().AreaOfDamage?"yes":"no")+" interval="+oldWeapon.DelayBetweenShots+" charge="+oldWeapon.GetComponent<CannonChargeShot>().MaximumChargeDuration);
            Effect(Impact,.4f);Effect(StolenImpact,2.6f/4.5f);
            Projectile(AssetDatabase.GetAssetPath(oldEnemy.Cannons[0].ProjectilePrefab),EnemyProjectile,Impact,10,1.8f,true);
            Projectile(AssetDatabase.GetAssetPath(oldWeapon.ProjectilePrefab),PlayerProjectile,StolenImpact,0,2.6f,false);
            Variant(ModularArmAuthoring.BigWeapon,Weapon);var go=PrefabUtility.LoadPrefabContents(Weapon);
            try{var w=go.GetComponent<WeaponController>();w.ProjectilePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerProjectile).GetComponent<ProjectileBase>();var visual=go.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="BigBot_EquippedCannon");var attachment=go.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Attachment");Vector3 pivot=attachment.position;const float scale=1.65f;visual.position=pivot+(visual.position-pivot)*scale;visual.localScale*=scale;w.WeaponMuzzle.position=visual.position;
                var guide=new GameObject("BazookaMuzzleReference").transform;guide.SetParent(visual,false);guide.SetPositionAndRotation(w.WeaponMuzzle.position,w.WeaponMuzzle.rotation);PrefabUtility.SaveAsPrefabAsset(go,Weapon);
            }finally{PrefabUtility.UnloadPrefabContents(go);}
            Variant(ModularArmAuthoring.BigPickup,Pickup);go=PrefabUtility.LoadPrefabContents(Pickup);try{go.GetComponent<StolenCannon>().Configure(AssetDatabase.LoadAssetAtPath<GameObject>(Weapon).GetComponent<WeaponController>());PrefabUtility.SaveAsPrefabAsset(go,Pickup);}finally{PrefabUtility.UnloadPrefabContents(go);}
            Variant(HitWeaponAuthoring.Big,Enemy);go=PrefabUtility.LoadPrefabContents(Enemy);try{var combat=go.GetComponent<BigBotCombat>();var data=new SerializedObject(combat);foreach(var pair in new[]{("damage",10f),("projectileSpeed",22f),("projectileRange",48f),("shotInterval",1.8f),("attackDistance",22f),("detectionDistance",36f),("explosiveShots",1f),("explosionRadius",1.8f),("emergencyBursts",1f),("emergencyBurstCount",3f),("emergencyBurstInterval",.26f),("emergencyBurstPause",2.1f)})Set(data,pair.Item1,pair.Item2);data.ApplyModifiedPropertiesWithoutUndo();foreach(var cannon in combat.Cannons){cannon.ProjectilePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(EnemyProjectile).GetComponent<ProjectileBase>();cannon.DelayBetweenShots=Mathf.Min(cannon.DelayBetweenShots,.12f);}var detector=go.GetComponentInChildren<BigBotDetectionModule>();detector.DetectionRange=36;detector.AttackRange=22;go.GetComponent<BigBotDestruction>().ConfigureCollectibleCannon(AssetDatabase.LoadAssetAtPath<GameObject>(Pickup).GetComponent<StolenCannon>());PrefabUtility.SaveAsPrefabAsset(go,Enemy);}finally{PrefabUtility.UnloadPrefabContents(go);}
            if(File.Exists(Lab))throw new InvalidOperationException("Preserve lab");AssetDatabase.CopyAsset(ModularArmAuthoring.Lab,Lab);var scene=EditorSceneManager.OpenScene(Lab);var old=Object.FindAnyObjectByType<BigBotCombat>().gameObject;var replacement=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Enemy),scene);replacement.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);replacement.transform.localScale=old.transform.localScale;
            foreach(var route in Object.FindObjectsByType<Unity.FPS.AI.PatrolPath>())for(int i=0;i<route.EnemiesToAssign.Count;i++)if(route.EnemiesToAssign[i]==old.GetComponent<Unity.FPS.AI.EnemyController>())route.EnemiesToAssign[i]=replacement.GetComponent<Unity.FPS.AI.EnemyController>();Object.DestroyImmediate(old);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText(Reports+"/after.txt","Enemy: 10 blast damage, 1.8 m radius, 22 m/s, 48 m travel, 36 m detection, 22 m attack, 0.7 s preparation unchanged, 1.8 s normal recovery (theoretical salvo period 2.5 s). Emergency: 3 projectiles, 0.26 s spacing, 2.1 s pause after final shot; 0.7 s warning before next burst. No stacking multiplier. Nominal pressure normal 20/2.5=8 HP/s (previous 24/2.9=8.28); emergency 30/3.32=9.04 HP/s (previous 12/(2.9/1.5)=6.21). All shots avoid cover/self/allies. Stolen: unchanged 25..65 charge damage and energy/shot timing; blast radius 2.6 m. Equipped visual scale 1.65 only; world drop/enemy scale unchanged. Impacts reuse bounded damage-free CannonExplosionPresentation variants, no lights or camera impulse.");IronRequiemSetupTools.WriteAudit();
        }
        sealed class Results:IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}
            public void OnError(string m){SessionState.EraseBool(Key);File.WriteAllText(Reports+"/runner-error.txt",m);}
            public void RunFinished(ITestResultAdaptor r){if(!SessionState.GetBool(Key,false))return;SessionState.EraseBool(Key);string label=SessionState.GetString(Key+".label","");TestRunnerApi.SaveResultToFile(r,Reports+"/play"+label+".xml");File.WriteAllText(Reports+"/status"+label+".txt",r.ResultState+" passed="+r.PassCount+" failed="+r.FailCount);SessionState.SetBool(Key+".restore",true);}
        }
    }
}
