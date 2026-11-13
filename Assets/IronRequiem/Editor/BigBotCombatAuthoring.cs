using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class BigBotCombatAuthoring
    {
        public const string Source="Assets/IronRequiem/Prefabs/Robots/NewRobot_Animated_v01.prefab",Enemy="Assets/IronRequiem/Prefabs/Enemies/BigBot_Combat_v01.prefab",Art="Assets/IronRequiem/Art/Robots/BigBotCombat",Lab="Assets/IronRequiem/Scenes/BigBotCombatLab.unity",Projectile="Assets/IronRequiem/Prefabs/Projectiles/Projectile_BigBot.prefab",Reports="Tools/IronRequiem/Reports/BigBotCombat";
        const string Request="Tools/IronRequiem/Requests/bigbot-combat.txt",Key="IRON.BigBotCombat";
        static readonly DateTime LoadedAt=DateTime.UtcNow;static TestRunnerApi api;
        static BigBotCombatAuthoring(){EditorApplication.update+=Tick;if(SessionState.GetBool(Key+".testing",false))EditorApplication.delayCall+=Register;}
        static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
        static bool Dirty()=>Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty);
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(SessionState.GetBool(Key+".restore",false)&&EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".time",0)>5){SessionState.EraseBool(Key+".restore");if(!Dirty())EditorSceneManager.OpenScene(Lab);}
            if(!File.Exists(Request)||LoadedAt<File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/BigBotCombatAuthoring.cs")||LoadedAt<File.GetLastWriteTimeUtc("Assets/IronRequiem/Scripts/AI/BigBotCombat.cs"))return;
            string op=File.ReadAllText(Request).Trim();if((op=="test"||op=="contact-probe")&&LoadedAt<File.GetLastWriteTimeUtc("Assets/IronRequiem/Tests/PlayMode/BigBotCombatTests.cs"))return;File.Delete(Request);Directory.CreateDirectory(Reports);
            try
            {
                if(op=="build")Build();
                else if(op=="correct-presentation"){CorrectPresentation();File.WriteAllText(Request,"test");}
                else if(op=="fix-affiliation"){FixAffiliation();File.WriteAllText(Request,"test");}
                else if(op=="fit-sockets"){FitSockets();File.WriteAllText(Request,"test");}
                else if(op=="test"||op=="contact-probe")
                {
                    if(Dirty())throw new InvalidOperationException("Preserving dirty user scene; save user edits before Play tests.");
                    if(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetType("IronRequiem.Tests.BigBotCombatTests")!=null))throw new InvalidOperationException("Big Bot test assembly not loaded.");
                    Register();SessionState.SetBool(Key+".testing",true);api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{op=="contact-probe"?"IronRequiem.Tests.BigBotCombatTests.PlayerContactRegressionProbe":"IronRequiem.Tests.BigBotCombatTests"}}));
                }
                else throw new InvalidOperationException("Unknown fixed Big Bot operation.");
            }
            catch(Exception e){File.WriteAllText(Reports+"/error.txt",e.ToString());Debug.LogException(e);}
        }
        static void CorrectPresentation()
        {
            if(Dirty())throw new InvalidOperationException("Preserving dirty user scene; corrections target only the saved prefab.");
            var go=PrefabUtility.LoadPrefabContents(Enemy);
            try
            {
                go.GetComponent<Health>().MaxHealth=1200;
                go.GetComponent<NavMeshAgent>().stoppingDistance=3;
                go.GetComponent<NavMeshAgent>().angularSpeed=60;
                var body=go.GetComponent<BoxCollider>();body.enabled=true;body.isTrigger=false;
                var rigidbody=go.GetComponent<Rigidbody>();rigidbody.detectCollisions=true;
                var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab").GetComponent<Unity.FPS.UI.WorldspaceHealthBar>();
                var bar=go.GetComponent<Unity.FPS.UI.WorldspaceHealthBar>();
                if(!bar)
                {
                    var pivot=Object.Instantiate(template.HealthBarPivot.gameObject,go.transform).transform;pivot.name="BigBotHealthBar";
                    pivot.localPosition=Vector3.up*(3.35f/go.transform.lossyScale.y);
                    pivot.localRotation=Quaternion.identity;pivot.localScale=Vector3.one*(1.8f/go.transform.lossyScale.y);
                    bar=go.AddComponent<Unity.FPS.UI.WorldspaceHealthBar>();bar.HealthBarPivot=pivot;bar.HealthBarImage=pivot.GetComponentsInChildren<UnityEngine.UI.Image>(true).Single(i=>i.name==template.HealthBarImage.name);
                    foreach(var image in pivot.GetComponentsInChildren<UnityEngine.UI.Image>(true))image.raycastTarget=false;
                }
                bar.Health=go.GetComponent<Health>();bar.HideFullHealthBar=false;bar.HealthBarPivot.gameObject.SetActive(true);
                go.GetComponent<BigBotCombat>().ConfigureTracking(Find(go.transform,"Torso"),bar.HealthBarPivot);
                var animator=go.GetComponentInChildren<NewRobotAnimationPresentation>().Animator;
                var controller=animator.runtimeAnimatorController as AnimatorController;
                Parameter(controller,"TurnSpeed",AnimatorControllerParameterType.Float,1);
                foreach(var child in controller.layers[0].stateMachine.states)
                {
                    if(child.state.name=="TurnLeft"||child.state.name=="TurnRight")
                    {child.state.speedParameter="TurnSpeed";child.state.speedParameterActive=true;}
                }
                EditorUtility.SetDirty(controller);PrefabUtility.SaveAsPrefabAsset(go,Enemy);
            }
            finally{PrefabUtility.UnloadPrefabContents(go);}
            AssetDatabase.SaveAssets();
            // Reopen the clean lab to pick up prefab changes without writing any scene.
            if(SceneManager.GetActiveScene().path==Lab&&!Dirty())EditorSceneManager.OpenScene(Lab);
            File.WriteAllText(Reports+"/corrections-config.txt","Health 1200; existing world-space health bar always shown; turn 60 degrees/s with existing turn clips scaled by angular velocity; bounded torso/arm aim restored each frame; synchronized two-cannon volley; navigation minimum stop 3 m.");
        }
        static void FixAffiliation()
        {
            if(Dirty())throw new InvalidOperationException("Preserving dirty user scene.");
            var go=PrefabUtility.LoadPrefabContents(Enemy);
            try{go.GetComponent<Actor>().Affiliation=0;PrefabUtility.SaveAsPrefabAsset(go,Enemy);}
            finally{PrefabUtility.UnloadPrefabContents(go);}
            AssetDatabase.SaveAssets();
            if(SceneManager.GetActiveScene().path==Lab)EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        [Serializable] sealed class MuzzleFit{public Vector3 right,left;}
        static void FitSockets()
        {
            bool cleanLab=SceneManager.GetActiveScene().path==Lab&&!Dirty();var fit=JsonUtility.FromJson<MuzzleFit>(File.ReadAllText(Reports+"/muzzle-fit.json"));var go=PrefabUtility.LoadPrefabContents(Enemy);
            try
            {
                var model=go.GetComponentInChildren<NewRobotAnimationPresentation>().transform;
                Find(go.transform,"WeaponMuzzle_R").position=model.TransformPoint(fit.right);Find(go.transform,"WeaponMuzzle_L").position=model.TransformPoint(fit.left);PrefabUtility.SaveAsPrefabAsset(go,Enemy);
            }
            finally{PrefabUtility.UnloadPrefabContents(go);}
            AssetDatabase.SaveAssets();if(cleanLab&&SceneManager.GetActiveScene().isDirty)EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        static void Build()
        {
            if(File.Exists(Enemy)||File.Exists(Lab)||File.Exists(Projectile))throw new InvalidOperationException("Preserving an existing Big Bot delivery.");
            Directory.CreateDirectory(Art);Directory.CreateDirectory(Path.GetDirectoryName(Enemy));Directory.CreateDirectory(Path.GetDirectoryName(Projectile));AssetDatabase.Refresh();
            var preview=EditorSceneManager.NewPreviewScene();var bot=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source),preview);bot.name="BigBot_Combat_v01";
            try
            {
                var skins=bot.GetComponentsInChildren<SkinnedMeshRenderer>(true);var presentation=bot.GetComponentInChildren<NewRobotAnimationPresentation>();
                File.WriteAllLines(Reports+"/source-inspection.txt",new[]{"Source="+Source,"RootScale="+bot.transform.localScale,"Colliders="+bot.GetComponentsInChildren<Collider>(true).Length,"Health="+(bool)bot.GetComponent<Health>(),"Weapons="+bot.GetComponentsInChildren<WeaponController>(true).Length,"RigidSkins="+skins.Length,"Clips="+string.Join(",",presentation.Animator.runtimeAnimatorController.animationClips.Select(c=>c.name).Distinct()),"Components="+string.Join(",",bot.GetComponents<Component>().Select(c=>c.GetType().Name))});
                if(skins.Length!=45||!presentation||bot.GetComponent<EnemyController>())throw new InvalidOperationException("Unexpected source identity; review Big Bot instead of replacing another robot.");
                var bounds=StaticBounds(skins.Where(s=>s.name.EndsWith("_LOD0")));if(Mathf.Abs(bounds.size.y-3)>.05f)throw new InvalidOperationException("Source should be the 3 m animated robot.");
                var material=Object.Instantiate(skins[0].sharedMaterial);material.name="BigBot_Surface";material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",Color.black);AssetDatabase.CreateAsset(material,Art+"/BigBot_Surface.mat");foreach(var skin in skins)skin.sharedMaterial=material;
                string controllerPath=Art+"/BigBot.controller";if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(presentation.Animator.runtimeAnimatorController),controllerPath))throw new InvalidOperationException("Cannot copy source Animator.");
                var animation=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);Parameter(animation,"Alerted",AnimatorControllerParameterType.Bool,0);Parameter(animation,"OnDamaged",AnimatorControllerParameterType.Trigger,0);Parameter(animation,"PreparationSpeed",AnimatorControllerParameterType.Float,.6f/.7f);Parameter(animation,"KickSpeed",AnimatorControllerParameterType.Float,6);Parameter(animation,"RecoverySpeed",AnimatorControllerParameterType.Float,2/.65f);
                foreach(var child in animation.layers[1].stateMachine.states){if(child.state.name=="PrepareFire")child.state.speedParameter="PreparationSpeed";if(child.state.name=="Fire")child.state.speedParameter="KickSpeed";if(child.state.name=="RecoverFire")child.state.speedParameter="RecoverySpeed";}
                presentation.Animator.runtimeAnimatorController=animation;presentation.Animator.applyRootMotion=false;
                var actor=bot.AddComponent<Actor>();actor.Affiliation=0;actor.AimPoint=Point(bot.transform,"BigBotAim",new Vector3(0,1.7f,.1f));var health=bot.AddComponent<Health>();health.MaxHealth=240;
                var body=bot.AddComponent<BoxCollider>();body.center=bot.transform.InverseTransformPoint(bounds.center);var scale=bot.transform.lossyScale;body.size=new Vector3((bounds.size.x+.08f)/scale.x,bounds.size.y/scale.y,(bounds.size.z+.08f)/scale.z);bot.AddComponent<Damageable>();
                var rb=bot.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;
                var agent=bot.AddComponent<NavMeshAgent>();agent.agentTypeID=0;agent.radius=2.25f;agent.height=3.05f;agent.baseOffset=0;agent.speed=1.4f;agent.angularSpeed=90;agent.acceleration=5;agent.stoppingDistance=.2f;agent.updateRotation=false;
                var nav=bot.AddComponent<NavigationModule>();nav.MoveSpeed=1.4f;nav.AngularSpeed=90;nav.Acceleration=5;
                var detection=bot.AddComponent<BigBotDetectionModule>();detection.DetectionSourcePoint=Point(bot.transform,"BigBotDetectionSource",new Vector3(0,2.55f,.05f));detection.DetectionRange=26;detection.AttackRange=12;detection.KnownTargetTimeout=8;
                var sound=bot.AddComponent<AudioSource>();sound.spatialBlend=1;sound.playOnAwake=false;sound.volume=.3f;
                var enemy=bot.AddComponent<EnemyController>();enemy.BodyMaterial=material;enemy.SwapToNextWeapon=true;enemy.DelayAfterWeaponSwap=0;enemy.OrientationSpeed=5;enemy.PathReachingRadius=.65f;enemy.DeathDuration=2.5f;enemy.DropRate=0;enemy.DeathVfxSpawnPoint=detection.DetectionSourcePoint;enemy.OnHitBodyGradient=new Gradient();enemy.OnHitBodyGradient.SetKeys(new[]{new GradientColorKey(new Color(1,.16f,.04f),0),new GradientColorKey(Color.black,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)});
                var mobile=bot.AddComponent<EnemyMobile>();mobile.Animator=presentation.Animator;mobile.AttackStopDistanceRatio=.72f;mobile.OnDetectVfx=Array.Empty<ParticleSystem>();mobile.RandomHitSparks=Array.Empty<ParticleSystem>();mobile.PitchDistortionMovementSpeed=new MinMaxFloat{Min=.9f,Max=1.1f};
                var projectile=BuildProjectile(preview);var originalEnemy=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab");var shootSfx=originalEnemy.GetComponentInChildren<WeaponController>(true).ShootSfx;
                var right=Find(bot.transform,"WeaponMuzzle_R");var left=new GameObject("WeaponMuzzle_L").transform;left.SetParent(Find(bot.transform,"Pincer_L"),false);
                // Mirror the fitted right socket through the model's centre plane in the rest pose.
                var local=bot.transform.InverseTransformPoint(right.position);local.x=-local.x;left.position=bot.transform.TransformPoint(local);left.rotation=right.rotation;
                var weapons=new WeaponController[2];var rear=new Transform[2];var bones=new Transform[2];var warnings=new ParticleSystem[2];
                for(int i=0;i<2;i++)
                {
                    string side=i==0?"R":"L";var muzzle=i==0?right:left;var go=new GameObject("BigBotCannon_"+side);go.transform.SetParent(muzzle,false);var inherited=go.transform.lossyScale;go.transform.localScale=new Vector3(1/inherited.x,1/inherited.y,1/inherited.z);
                    var audio=go.AddComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=1;audio.volume=.5f;
                    var weapon=go.AddComponent<WeaponController>();weapon.WeaponName="Big Bot Cannon "+side;weapon.WeaponRoot=go;weapon.WeaponMuzzle=go.transform;weapon.ProjectilePrefab=projectile;weapon.ShootType=WeaponShootType.Automatic;weapon.DelayBetweenShots=.05f;weapon.MaxAmmo=10000;weapon.AutomaticReload=true;weapon.AmmoReloadDelay=0;weapon.AmmoReloadRate=1000;weapon.BulletSpreadAngle=0;weapon.BulletsPerShot=1;weapon.RecoilForce=0;weapon.ShootSfx=shootSfx;weapon.MuzzleFlashPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/VFX/VFX_MuzzleFlashGreen.prefab");weapons[i]=weapon;rear[i]=bones[i]=Find(bot.transform,"Forearm_"+side);warnings[i]=Warning(go.transform);
                }
                bot.AddComponent<BigBotCombat>().Configure(presentation,weapons,rear,bones,warnings);PrefabUtility.SaveAsPrefabAsset(bot,Enemy);
                File.WriteAllText(Reports+"/configuration.txt","Health 240; damage 12; projectile 18 m/s, range 35 m; acquisition 26 m; attack 12 m, stop about 8.64 m; preparation .7 s; interval 2.2 s after discharge; navigation 1.4 m/s, radius 2.25 m, height 3.05 m. No detachable weapon system found on source; no new pickup system added.");
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            BuildLab();AssetDatabase.SaveAssets();File.WriteAllText(Reports+"/build-status.txt","Big Bot variant and isolated combat lab built. Source model/prefab, gameplay scenes and global navigation settings preserved.");
        }
        static void Parameter(AnimatorController c,string name,AnimatorControllerParameterType type,float value){if(!c.parameters.Any(p=>p.name==name))c.AddParameter(name,type);var ps=c.parameters;foreach(var p in ps)if(p.name==name&&type==AnimatorControllerParameterType.Float)p.defaultFloat=value;c.parameters=ps;}
        static Transform Point(Transform root,string name,Vector3 offset){var t=new GameObject(name).transform;t.SetParent(root,false);t.position=root.position+offset;return t;}
        static Bounds StaticBounds(System.Collections.Generic.IEnumerable<SkinnedMeshRenderer> skins)
        {
            Bounds b=default;bool first=true;foreach(var r in skins){var m=r.sharedMesh.bounds;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var p=r.transform.TransformPoint(m.center+Vector3.Scale(m.extents,new Vector3(x,y,z)));if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}}return b;
        }
        static ProjectileBase BuildProjectile(Scene preview)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/Projectiles/Projectile_Hoverbot.prefab"),preview);go.name="Projectile_BigBot";go.transform.localScale=Vector3.one;
            var p=go.GetComponent<ProjectileStandard>();p.Speed=18;p.Damage=12;p.Radius=.1f;p.MaxLifeTime=120;p.TrajectoryCorrectionDistance=-1;p.InheritWeaponVelocity=false;p.GravityDownAcceleration=0;p.AreaOfDamage=null;p.ImpactVfxLifetime=1.2f;go.AddComponent<BigBotProjectileSettings>();
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,Projectile);Object.DestroyImmediate(go);return prefab.GetComponent<ProjectileBase>();
        }
        static ParticleSystem Warning(Transform muzzle)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/VFX/VFX_MuzzleFlashGreen.prefab"),muzzle.gameObject.scene);go.name="CannonPreparation";go.transform.SetParent(muzzle,false);go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
            var ps=go.GetComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.playOnAwake=false;main.loop=true;main.duration=1;main.maxParticles=16;main.startLifetime=new ParticleSystem.MinMaxCurve(.08f,.16f);main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(.10f,.18f);main.stopAction=ParticleSystemStopAction.None;main.scalingMode=ParticleSystemScalingMode.Shape;var emission=ps.emission;emission.SetBursts(Array.Empty<ParticleSystem.Burst>());emission.rateOverTime=40;var lights=ps.lights;lights.enabled=false;return ps;
        }
        static GameObject Instance(string path,Scene scene)=>(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);
        static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;}
        static void BuildLab()
        {
            bool dirty=Dirty();var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Robots/NewRobotAnimated/LabFloor.mat");var arena=new GameObject("BigBotNavGeometry");
            Box(arena.transform,"Floor",new Vector3(0,-.25f,0),new Vector3(40,.5f,50),mat);Box(arena.transform,"NorthWall",new Vector3(0,2.5f,25),new Vector3(40,5,1),mat);Box(arena.transform,"SouthWall",new Vector3(0,2.5f,-25),new Vector3(40,5,1),mat);Box(arena.transform,"EastWall",new Vector3(20,2.5f,0),new Vector3(1,5,50),mat);Box(arena.transform,"WestWall",new Vector3(-20,2.5f,0),new Vector3(1,5,50),mat);Box(arena.transform,"LineOfSightCover",new Vector3(0,1.8f,0),new Vector3(7,3.6f,1),mat);Box(arena.transform,"SideCover",new Vector3(11,1.8f,-8),new Vector3(3,3.6f,3),mat);
            Physics.SyncTransforms();var settings=NavMesh.GetSettingsByID(0);settings.agentRadius=2.25f;settings.agentHeight=3.05f;settings.agentClimb=.25f;settings.agentSlope=35;settings.overrideVoxelSize=true;settings.voxelSize=.12f;settings.overrideTileSize=true;settings.tileSize=128;var sources=new List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(arena.transform,-1,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            var data=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(Vector3.zero,new Vector3(44,12,54)),Vector3.zero,Quaternion.identity);if(!data)throw new InvalidOperationException("Big Bot NavMesh build failed");AssetDatabase.CreateAsset(data,Art+"/BigBotLab_NavMesh.asset");var surface=arena.AddComponent<Unity.AI.Navigation.NavMeshSurface>();surface.collectObjects=Unity.AI.Navigation.CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.agentTypeID=0;surface.navMeshData=data;surface.AddData();
            var gm=Instance("Assets/FPS/Prefabs/GameManager.prefab",scene);if(!gm.GetComponent<ActorsManager>())gm.AddComponent<ActorsManager>();if(!gm.GetComponent<EnemyManager>())gm.AddComponent<EnemyManager>();Instance("Assets/FPS/Prefabs/UI/GameHUD.prefab",scene);Instance("Assets/FPS/Prefabs/UI/InGameMenu.prefab",scene);var events=new GameObject("EventSystem");events.AddComponent<UnityEngine.EventSystems.EventSystem>();events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            var player=Instance("Assets/IronRequiem/Prefabs/Player/Player_IR_Protagonist_Actions_v02.prefab",scene);player.name="Player";player.transform.SetPositionAndRotation(new Vector3(-9,.05f,-13),Quaternion.identity);player.GetComponent<Health>().MaxHealth=100;
            var bot=Instance(Enemy,scene);bot.name="Big Bot";bot.transform.SetPositionAndRotation(new Vector3(-9,0,11),Quaternion.Euler(0,180,0));
            var route=new GameObject("BigBotPatrol").AddComponent<PatrolPath>();foreach(var pos in new[]{new Vector3(-9,0,11),new Vector3(8,0,11)}){var n=new GameObject("PatrolNode").transform;n.SetParent(route.transform);n.position=pos;route.PathNodes.Add(n);}route.EnemiesToAssign.Add(bot.GetComponent<EnemyController>());
            var light=new GameObject("LabLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(45,-35,0);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.35f,.38f,.43f);
            if(!EditorSceneManager.SaveScene(scene,Lab))throw new InvalidOperationException("Cannot save combat lab");if(dirty){EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}else EditorSceneManager.OpenScene(Lab);
        }
        sealed class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}
            public void RunFinished(ITestResultAdaptor result){if(!SessionState.GetBool(Key+".testing",false))return;SessionState.EraseBool(Key+".testing");TestRunnerApi.SaveResultToFile(result,Reports+"/playmode.xml");File.WriteAllText(Reports+"/test-status.txt",result.ResultState+" passed="+result.PassCount+" failed="+result.FailCount);SessionState.SetBool(Key+".restore",true);SessionState.SetFloat(Key+".time",(float)EditorApplication.timeSinceStartup);}
        }
    }
}
