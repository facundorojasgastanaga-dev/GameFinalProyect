using System;
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
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
    [InitializeOnLoad]public static class CombatPolishAuthoring
    {
        public const string Enemy="Assets/IronRequiem/Prefabs/Enemies/BigBot_CombatPolish_v01.prefab",Player="Assets/IronRequiem/Prefabs/Player/Player_CombatPolish_v01.prefab",Lab="Assets/IronRequiem/Scenes/CombatPolishLab.unity",Art="Assets/IronRequiem/Art/CombatPolish",Reports="Tools/IronRequiem/Reports/CombatPolish";
        const string Queue="Tools/IronRequiem/Requests/combat-polish.txt",Key="IRON.CombatPolish";static TestRunnerApi api;static readonly DateTime LoadedAt=DateTime.UtcNow;
        static CombatPolishAuthoring(){EditorApplication.update+=Tick;if(SessionState.GetBool(Key+".testing",false))EditorApplication.delayCall+=Register;}
        static bool Dirty()=>Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty);
        static void Register(){if(!api){api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());}}
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool(Key+".testing",false))return;
            if(SessionState.GetBool(Key+".restore",false)&&EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".at",0)>3){SessionState.EraseBool(Key+".restore");if(!Dirty())EditorSceneManager.OpenScene(Lab);}
            if(!File.Exists(Queue))return;foreach(var file in new[]{"Assets/IronRequiem/Editor/CombatPolishAuthoring.cs","Assets/IronRequiem/Tests/PlayMode/CombatPolishTests.cs","Assets/IronRequiem/Scripts/AI/BigBotTurnFootPlant.cs"})if(File.Exists(file)&&LoadedAt<File.GetLastWriteTimeUtc(file))return;
            string op=File.ReadAllText(Queue).Trim();File.Delete(Queue);Directory.CreateDirectory(Reports);
            try
            {
                if(op=="inspect")Inspect(); else if(op=="inspect-enemies")InspectEnemies();
                else if(op=="create"){Create();File.WriteAllText(Queue,"baseline-turn");}
                else if(op=="fix-turn"){FixTurn();File.WriteAllText(Queue,"test-turn");}
                else if(op=="baseline-turn"||op=="test-turn")Run(op,new[]{"IronRequiem.Tests.CombatPolishTests.TurnTransitions"});
                else if(op=="fix-weapon"){FixWeapon();File.WriteAllText(Queue,"test-weapon");}
                else if(op=="test-weapon")Run(op,new[]{"IronRequiem.Tests.CombatPolishTests.PistolMountAndBalance"});
                else if(op=="fix-visibility"){FixVisibility();File.WriteAllText(Queue,"test-visibility");}
                else if(op=="test-visibility")Run(op,new[]{"IronRequiem.Tests.CombatPolishTests.EnemiesReadableWithoutLights"});
                else if(op=="fix-death"){FixDeath();File.WriteAllText(Queue,"test-death");}
                else if(op=="test-death")Run(op,new[]{"IronRequiem.Tests.CombatPolishTests.CoordinatedVisualDeaths"});
                else if(op=="test-regression")Run(op,new[]{"IronRequiem.Tests.CombatPolishTests.BigBotDeathVisualSequence","IronRequiem.Tests.CombatPolishTests.MovementAndStolenWeaponRegression"});
                else if(op=="test-inventory")Run(op,new[]{"IronRequiem.Tests.CombatPolishTests.MovementAndStolenWeaponRegression"});
                else if(op=="diagnose-inventory")Run(op,new[]{"IronRequiem.Tests.CombatPolishTests.ConcurrentInventoryDiagnosis"});
                else if(op=="verify")Verify();
                else if(op=="fix-arm"){FixArm();File.WriteAllText(Queue,"test-arm");}
                else if(op=="test-arm")Run(op,new[]{"IronRequiem.Tests.CombatPolishTests.PunchCutStaysOutsideWeaponCamera"});
                else if(op=="baseline-melee"||op=="test-melee")Run(op,new[]{"IronRequiem.Tests.CombatPolishTests.MeleeContactAngles","IronRequiem.Tests.CombatPolishTests.MeleeTimingWallAndRange"});
                else throw new InvalidOperationException("Unknown fixed combat polish operation");
            }
            catch(Exception e){File.WriteAllText(Reports+"/error.txt",e.ToString());Debug.LogException(e);}
        }
        static void Run(string label,string[] tests)
        {if(Dirty())throw new InvalidOperationException("Preserving dirty user scene; save user changes before Play tests.");Register();SessionState.SetString(Key+".label",label);SessionState.SetBool(Key+".testing",true);api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=tests}));}
        static Transform Find(GameObject go,string name)=>go.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        static void Variant(string source,string destination,Scene preview)
        {if(File.Exists(destination))throw new InvalidOperationException("Preserve existing polish asset "+destination);var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source),preview);go.name=Path.GetFileNameWithoutExtension(destination);PrefabUtility.SaveAsPrefabAsset(go,destination);Object.DestroyImmediate(go);}
        static void Create()
        {
            Directory.CreateDirectory(Art);AssetDatabase.Refresh();var preview=EditorSceneManager.NewPreviewScene();try{Variant(BigBotDestructionAuthoring.Prefab,Enemy,preview);Variant("Assets/IronRequiem/Prefabs/Player/Player_IR_Protagonist_Actions_v02.prefab",Player,preview);}finally{EditorSceneManager.ClosePreviewScene(preview);}
            var enemy=PrefabUtility.LoadPrefabContents(Enemy);try{var animator=enemy.GetComponentInChildren<Animator>();string path=Art+"/BigBotPolish.controller";AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),path);animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);PrefabUtility.SaveAsPrefabAsset(enemy,Enemy);}finally{PrefabUtility.UnloadPrefabContents(enemy);}
            if(File.Exists(Lab))throw new InvalidOperationException("Preserve existing polish lab");bool dirty=Dirty();var previous=SceneManager.GetActiveScene();AssetDatabase.CopyAsset(BigBotDestructionAuthoring.Lab,Lab);var scene=EditorSceneManager.OpenScene(Lab,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var oldEnemy=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BigBotCombat>(true)).Single().gameObject;var next=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Enemy),scene);next.transform.SetPositionAndRotation(oldEnemy.transform.position,oldEnemy.transform.rotation);next.transform.localScale=oldEnemy.transform.localScale;
            foreach(var route in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PatrolPath>(true)))for(int i=0;i<route.EnemiesToAssign.Count;i++)if(route.EnemiesToAssign[i]==oldEnemy.GetComponent<EnemyController>())route.EnemiesToAssign[i]=next.GetComponent<EnemyController>();Object.DestroyImmediate(oldEnemy);
            var oldPlayer=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerCharacterController>(true)).Single().gameObject;var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Player),scene);player.transform.SetPositionAndRotation(oldPlayer.transform.position,oldPlayer.transform.rotation);Object.DestroyImmediate(oldPlayer);
            string nav=Art+"/CombatPolish_NavMesh.asset";AssetDatabase.CopyAsset(BigBotDestructionAuthoring.Art+"/BigBotDestruction_NavMesh.asset",nav);var surface=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Unity.AI.Navigation.NavMeshSurface>()).Single();surface.RemoveData();surface.navMeshData=AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(nav);surface.AddData();EditorUtility.SetDirty(surface);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            if(dirty){EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}else EditorSceneManager.OpenScene(Lab);
        }
        public static readonly string[] Types={Enemy,"Assets/IronRequiem/Prefabs/Enemies/T07_CombatPolish.prefab","Assets/IronRequiem/Prefabs/Enemies/HoverBot_CombatPolish.prefab","Assets/IronRequiem/Prefabs/Enemies/Turret_CombatPolish.prefab"};
        static void FixVisibility()
        {
            foreach(var stray in Object.FindObjectsByType<GameObject>().Where(g=>g.name=="SurfaceInspection"))Object.DestroyImmediate(stray);
            var sources=new[]{Enemy,"Assets/IronRequiem/Prefabs/Enemies/T07_Common_Destructible.prefab","Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab","Assets/FPS/Prefabs/Enemies/Enemy_Turret.prefab"};var preview=EditorSceneManager.NewPreviewScene();try{for(int i=1;i<4;i++)if(!File.Exists(Types[i]))Variant(sources[i],Types[i],preview);}finally{EditorSceneManager.ClosePreviewScene(preview);}
            string materialPath=Art+"/RobotStatus_Amber.mat";if(!File.Exists(materialPath)){var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.35f,.13f,.025f));mat.SetColor("_EmissionColor",new Color(1.4f,.40f,.055f));mat.EnableKeyword("_EMISSION");mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;AssetDatabase.CreateAsset(mat,materialPath);}var status=AssetDatabase.LoadAssetAtPath<Material>(materialPath);status.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;status.EnableKeyword("_EMISSION");EditorUtility.SetDirty(status);
            for(int i=0;i<4;i++){
                var go=PrefabUtility.LoadPrefabContents(Types[i]);try{
                    if(i<2){
                        var animator=go.GetComponentsInChildren<Animator>(true).First(a=>a.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r=>r.enabled));var idle=animator.runtimeAnimatorController.animationClips.First(c=>c.name.Contains("Idle"));idle.SampleAnimation(animator.gameObject,0);
                        var skin=go.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name==(i==0?"Torso_LOD0":"Body_LOD0"));var bone=skin.bones.Single(t=>t.name==(i==0?"Torso":"Head"));
                        if(!go.GetComponent<EnemyPowerIndicators>()){var marker=SurfaceMarker(skin,bone,i==0?.68f:.92f,i==0?new Vector3(.18f,.085f,.018f):new Vector3(.09f,.025f,.012f),status,go.transform);go.AddComponent<EnemyPowerIndicators>().Configure(new[]{marker});var group=go.GetComponentsInChildren<LODGroup>(true).FirstOrDefault(g=>g.GetLODs().Any(l=>l.renderers.Contains(skin)));if(group){var lods=group.GetLODs();for(int j=0;j<lods.Length;j++)lods[j].renderers=lods[j].renderers.Concat(new[]{marker}).ToArray();group.SetLODs(lods);}}
                    }else{
                        var ec=go.GetComponent<EnemyController>();var old=ec.EyeColorMaterial;string path=Art+"/"+(i==2?"HoverEye":"TurretEye")+".mat";if(!File.Exists(path))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(old),path);var mat=AssetDatabase.LoadAssetAtPath<Material>(path);mat.EnableKeyword("_EMISSION");mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;ec.EyeColorMaterial=mat;ec.DefaultEyeColor=new Color(.14f,.7f,1.15f);mat.SetColor("_EmissionColor",ec.DefaultEyeColor);mat.EnableKeyword("_EMISSION");ec.AttackEyeColor=new Color(1.6f,.22f,.035f);foreach(var r in go.GetComponentsInChildren<Renderer>(true)){var mats=r.sharedMaterials;for(int m=0;m<mats.Length;m++)if(mats[m]==old)mats[m]=mat;r.sharedMaterials=mats;}EditorUtility.SetDirty(mat);
                    }PrefabUtility.SaveAsPrefabAsset(go,Types[i]);
                }finally{PrefabUtility.UnloadPrefabContents(go);}
            }
            if(Dirty())throw new InvalidOperationException("Preserving dirty scene; variants saved but lab not saved");var scene=EditorSceneManager.OpenScene(Lab);var positions=new[]{new Vector3(-9,0,0),new Vector3(9,0,6),new Vector3(9,0,-7)};for(int i=1;i<4;i++)if(!scene.GetRootGameObjects().Any(g=>g.name==Path.GetFileNameWithoutExtension(Types[i]))){var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Types[i]),scene);instance.transform.position=positions[i-1];instance.transform.rotation=Quaternion.Euler(0,180,0);}EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        static Renderer SurfaceMarker(SkinnedMeshRenderer skin,Transform bone,float height,Vector3 size,Material material,Transform actor)
        {
            var mesh=new Mesh();skin.BakeMesh(mesh,false);var vertices=mesh.vertices;var scale=skin.transform.lossyScale;for(int v=0;v<vertices.Length;v++)vertices[v]=skin.transform.TransformPoint(new Vector3(vertices[v].x/scale.x,vertices[v].y/scale.y,vertices[v].z/scale.z));mesh.vertices=vertices;mesh.RecalculateBounds();var bounds=mesh.bounds;var origin=new Vector3(bounds.center.x,Mathf.Lerp(bounds.min.y,bounds.max.y,height),bounds.max.z+1);var ray=new Ray(origin,Vector3.back);float nearest=float.PositiveInfinity;Vector3 point=default,normal=default;var tri=mesh.triangles;for(int t=0;t<tri.Length;t+=3){var a=vertices[tri[t]];var b=vertices[tri[t+1]];var c=vertices[tri[t+2]];var e1=b-a;var e2=c-a;var p=Vector3.Cross(ray.direction,e2);float det=Vector3.Dot(e1,p);if(Mathf.Abs(det)<1e-10f)continue;float inv=1/det;var d=ray.origin-a;float u=Vector3.Dot(d,p)*inv;if(u<0||u>1)continue;var q=Vector3.Cross(d,e1);float v=Vector3.Dot(ray.direction,q)*inv;if(v<0||u+v>1)continue;float distance=Vector3.Dot(e2,q)*inv;if(distance>=0&&distance<nearest){nearest=distance;point=ray.GetPoint(distance);normal=Vector3.Cross(e1,e2).normalized;if(Vector3.Dot(normal,ray.direction)>0)normal=-normal;}}if(float.IsInfinity(nearest)){float score=float.PositiveInfinity;for(int t=0;t<tri.Length;t+=3){var a=vertices[tri[t]];var b=vertices[tri[t+1]];var c=vertices[tri[t+2]];var n=Vector3.Cross(b-a,c-a).normalized;if(n.z<.35f)continue;var centre=(a+b+c)/3;float cost=Mathf.Pow(centre.x-origin.x,2)+Mathf.Pow(centre.y-origin.y,2)+.005f*Mathf.Pow(bounds.max.z-centre.z,2);if(cost<score){score=cost;point=centre;normal=n;}}if(float.IsInfinity(score))throw new InvalidOperationException("No front-facing surface on "+skin.name);}var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(panel.GetComponent<Collider>());panel.name="PowerIndicator";panel.transform.position=point+normal*.006f;panel.transform.rotation=Quaternion.LookRotation(normal);panel.transform.localScale=size;panel.transform.SetParent(bone,true);panel.layer=actor.gameObject.layer;var r=panel.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;Object.DestroyImmediate(mesh);return r;
        }
        public const string Burst="Assets/IronRequiem/Prefabs/VFX/RobotBodyBurst_CombatPolish.prefab";
        static void FixDeath()
        {
            if(!File.Exists(Burst)){var preview=EditorSceneManager.NewPreviewScene();try{Variant("Assets/IronRequiem/Prefabs/VFX/T07_BodyExplosion.prefab",Burst,preview);}finally{EditorSceneManager.ClosePreviewScene(preview);}}
            var go=PrefabUtility.LoadPrefabContents(Burst);try{
                if(go.GetComponentInChildren<DamageArea>(true)||go.GetComponentInChildren<CannonExplosion>(true)||go.GetComponentInChildren<Light>(true))throw new InvalidOperationException("Body burst must remain cosmetic and unlit");
                foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true)){var main=ps.main;main.maxParticles=Mathf.Min(main.maxParticles,64);main.scalingMode=ParticleSystemScalingMode.Hierarchy;var lights=ps.lights;lights.enabled=false;var r=ps.GetComponent<ParticleSystemRenderer>();if(r){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.maxParticleSize=Mathf.Min(r.maxParticleSize,.3f);}}
                PrefabUtility.SaveAsPrefabAsset(go,Burst);
            }finally{PrefabUtility.UnloadPrefabContents(go);}
            var effect=AssetDatabase.LoadAssetAtPath<GameObject>(Burst);
            for(int i=0;i<4;i++){
                go=PrefabUtility.LoadPrefabContents(Types[i]);try{
                    var ec=go.GetComponent<EnemyController>();
                    if(i==0)go.GetComponent<BigBotDestruction>().ConfigureDeathBurst(Find(go,"Torso"),effect,1.55f,.97f);
                    else if(i==1){var data=new SerializedObject(go.GetComponent<RobotDamageProgression>());data.FindProperty("deathBurstPrefab").objectReferenceValue=effect;data.ApplyModifiedPropertiesWithoutUndo();}
                    else{
                        if(ec.DeathVfx.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)throw new InvalidOperationException("Preserve legacy death behaviour; inspect before replacing");var presentation=go.GetComponent<PooledEnemyDeathBurst>();if(!presentation)presentation=go.AddComponent<PooledEnemyDeathBurst>();presentation.Configure(effect,ec.DeathVfxSpawnPoint,i==2?.7f:1.4f);
                    }PrefabUtility.SaveAsPrefabAsset(go,Types[i]);
                }finally{PrefabUtility.UnloadPrefabContents(go);}
            }AssetDatabase.SaveAssets();
        }
        static void Verify()
        {
            int missing=0;foreach(var path in Types.Concat(new[]{Player,Pistol,Burst})){
                var go=PrefabUtility.LoadPrefabContents(path);try{
                    foreach(var t in go.GetComponentsInChildren<Transform>(true)){missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);foreach(var c in t.GetComponents<Component>()){if(!c)continue;var data=new SerializedObject(c);var prop=data.GetIterator();while(prop.NextVisible(true))if(prop.propertyType==SerializedPropertyType.ObjectReference&&prop.objectReferenceValue==null&&!prop.objectReferenceEntityIdValue.Equals(default(UnityEngine.EntityId)))throw new InvalidOperationException(path+" missing "+t.name+"."+prop.propertyPath);}}
                }finally{PrefabUtility.UnloadPrefabContents(go);}
            }if(missing!=0)throw new InvalidOperationException("Missing scripts "+missing);File.WriteAllText(Reports+"/references.txt","Seven variants inspected: missing scripts=0; missing serialized object references=0. MainScene and originals preserved.");IronRequiemSetupTools.WriteAudit();
        }
        static void InspectEnemies()
        {
            var text=new System.Text.StringBuilder();foreach(var path in new[]{Enemy,"Assets/IronRequiem/Prefabs/Enemies/T07_Common_Destructible.prefab","Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab","Assets/FPS/Prefabs/Enemies/Enemy_Turret.prefab"}){
                var go=PrefabUtility.LoadPrefabContents(path);try{var ec=go.GetComponent<EnemyController>();text.AppendLine(path+" rootScale="+go.transform.localScale+" eye="+AssetDatabase.GetAssetPath(ec.EyeColorMaterial)+" death="+AssetDatabase.GetAssetPath(ec.DeathVfx)+" duration="+ec.DeathDuration+" presentation="+go.GetComponent<IEnemyDeathPresentation>());foreach(var r in go.GetComponentsInChildren<Renderer>(true).Where(r=>!(r is ParticleSystemRenderer)))text.AppendLine("VIEW "+AnimationUtility.CalculateTransformPath(r.transform,go.transform)+" bounds="+r.bounds+" enabled="+r.enabled+" mats="+string.Join(",",r.sharedMaterials.Select(m=>m?AssetDatabase.GetAssetPath(m):"null")));foreach(var t in go.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Head")||t.name.Contains("Torso")||t.name.Contains("Eye")))text.AppendLine("MARKER "+AnimationUtility.CalculateTransformPath(t,go.transform)+" position="+t.position+" rot="+t.eulerAngles+" scale="+t.lossyScale);
                }finally{PrefabUtility.UnloadPrefabContents(go);}
            }File.WriteAllText(Reports+"/enemy-inspection.txt",text.ToString());
        }
        static void Inspect()
        {
            var text=new System.Text.StringBuilder();
            foreach(var path in new[]{"Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_Pistol.prefab","Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab",Player})
            {
                var go=PrefabUtility.LoadPrefabContents(path);try{
                    text.AppendLine(path);var weapon=go.GetComponent<WeaponController>();if(weapon){text.AppendLine("Muzzle "+weapon.WeaponMuzzle.position+" root "+weapon.WeaponRoot.name+" local "+weapon.WeaponMuzzle.localPosition);foreach(var t in go.GetComponentsInChildren<Transform>(true))text.AppendLine(AnimationUtility.CalculateTransformPath(t,go.transform)+" p="+t.position.ToString("F4")+" local="+t.localPosition.ToString("F4")+" scale="+t.lossyScale+" euler="+t.eulerAngles+" active="+t.gameObject.activeSelf);}
                    foreach(var r in go.GetComponentsInChildren<Renderer>(true))text.AppendLine("RENDER "+AnimationUtility.CalculateTransformPath(r.transform,go.transform)+" "+r.GetType().Name+" bounds="+r.bounds+" mats="+string.Join(",",r.sharedMaterials.Select(m=>m?AssetDatabase.GetAssetPath(m):"null"))+" mesh="+(r.GetComponent<MeshFilter>()?AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh):"skin"));
                    if(go.GetComponent<PlayerWeaponsManager>())text.AppendLine("weapons="+string.Join(",",go.GetComponent<PlayerWeaponsManager>().StartingWeapons.Select(w=>AssetDatabase.GetAssetPath(w))));
                }finally{PrefabUtility.UnloadPrefabContents(go);}
            }File.WriteAllText(Reports+"/inspection.txt",text.ToString());
        }
        public const string Pistol="Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_Pistol_CombatPolish.prefab";
        static void FixWeapon()
        {
            if(!File.Exists(Pistol)){var preview=EditorSceneManager.NewPreviewScene();try{Variant("Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_Pistol.prefab",Pistol,preview);}finally{EditorSceneManager.ClosePreviewScene(preview);}}
            var go=PrefabUtility.LoadPrefabContents(Pistol);try{
                var module=Find(go,"PistolModule_1000_Equipped");foreach(var r in module.GetComponentsInChildren<Renderer>())r.enabled=false;
                var shell=Find(go,"WeaponMesh_Pistol");foreach(var r in shell.GetComponentsInChildren<MeshRenderer>())r.enabled=true;
                foreach(var fx in shell.GetComponentsInChildren<ParticleSystem>(true))fx.gameObject.SetActive(false);
                var socket=Find(go,"Attachment");socket.position=new Vector3(0,.124f,.038f);
                if(!go.GetComponentsInChildren<Transform>().Any(t=>t.name=="RoboticWristCoupler")){
                    var collar=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Object.DestroyImmediate(collar.GetComponent<Collider>());collar.name="RoboticWristCoupler";collar.transform.SetParent(go.GetComponent<WeaponController>().WeaponRoot.transform,false);collar.transform.localPosition=new Vector3(0,.124f,.048f);collar.transform.localRotation=Quaternion.Euler(90,0,0);collar.transform.localScale=new Vector3(.165f,.045f,.165f);collar.layer=shell.gameObject.layer;collar.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/FPS/Art/Materials/Weapons/WeaponPistol.mat");}
                PrefabUtility.SaveAsPrefabAsset(go,Pistol);
            }finally{PrefabUtility.UnloadPrefabContents(go);}
            go=PrefabUtility.LoadPrefabContents(Player);try{
                var manager=go.GetComponent<PlayerWeaponsManager>();var pistol=AssetDatabase.LoadAssetAtPath<GameObject>(Pistol).GetComponent<WeaponController>();for(int i=0;i<manager.StartingWeapons.Count;i++)if(manager.StartingWeapons[i].name=="Weapon_IR_Pistol")manager.StartingWeapons[i]=pistol;
                var hands=new SerializedObject(go.GetComponent<ProtagonistHandsPresentation>());hands.FindProperty("pistolPrefab").objectReferenceValue=pistol;hands.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(go,Player);AssetDatabase.SaveAssets();
            }finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        static void FixArm()
        {
            string path=Art+"/LeftPunch_FPSFramed.anim";if(!File.Exists(path))AssetDatabase.CopyAsset("Assets/IronRequiem/Art/Player/ProtagonistActions/LeftPunch_Articulated.anim",path);var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if(binding.path=="LeftPunchWrist"&&(binding.propertyName=="m_LocalPosition.y"||binding.propertyName=="m_LocalPosition.z"))
                {var curve=AnimationUtility.GetEditorCurve(clip,binding);var keys=curve.keys;for(int i=0;i<keys.Length;i++){if(binding.propertyName.EndsWith(".y"))keys[i].value=Mathf.Min(keys[i].value,-.16f);else keys[i].value=Mathf.Min(keys[i].value,.44f);}curve.keys=keys;AnimationUtility.SetEditorCurve(clip,binding,curve);}
                if(binding.path=="LeftPunchElbow"&&binding.propertyName=="m_LocalPosition.y")AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0,.56f,-1.05f));
            }
            var go=PrefabUtility.LoadPrefabContents(Player);try{var hands=go.GetComponent<ProtagonistHandsPresentation>();var fps=hands.FpsRoot;hands.ConfigureActions(go.GetComponent<ProtagonistMelee>(),clip,Find(go,"LeftPunchWrist"),Find(go,"LeftPunchElbow"),fps.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Forearm_L_FPS"||r.name=="Hand_L_FPS").ToArray());PrefabUtility.SaveAsPrefabAsset(go,Player);EditorUtility.SetDirty(clip);AssetDatabase.SaveAssets();}finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        static void FixTurn()
        {
            var go=PrefabUtility.LoadPrefabContents(Enemy);
            try
            {
                var animator=go.GetComponentInChildren<Animator>();var controller=(AnimatorController)animator.runtimeAnimatorController;var idle=controller.animationClips.Single(c=>c.name=="IRN_Idle");idle.SampleAnimation(animator.gameObject,0);
                foreach(var state in controller.layers[0].stateMachine.states)if(state.state.name=="TurnLeft"||state.state.name=="TurnRight"){state.state.motion=idle;state.state.timeParameterActive=false;state.state.speedParameterActive=false;}
                var plant=go.AddComponent<BigBotTurnFootPlant>();plant.Configure(new[]{Find(go,"Thigh_L"),Find(go,"Thigh_R")},new[]{Find(go,"Shin_L"),Find(go,"Shin_R")},new[]{Find(go,"Foot_L"),Find(go,"Foot_R")});animator.applyRootMotion=false;go.GetComponent<UnityEngine.AI.NavMeshAgent>().updateRotation=false;EditorUtility.SetDirty(controller);PrefabUtility.SaveAsPrefabAsset(go,Enemy);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        sealed class Results:IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}
            public void OnError(string message){SessionState.EraseBool(Key+".testing");File.WriteAllText(Reports+"/runner-error.txt",message);}
            public void RunFinished(ITestResultAdaptor result){if(!SessionState.GetBool(Key+".testing",false))return;SessionState.EraseBool(Key+".testing");var label=SessionState.GetString(Key+".label","test");TestRunnerApi.SaveResultToFile(result,Reports+"/"+label+".xml");File.WriteAllText(Reports+"/status.txt",label+": "+result.ResultState+" passed="+result.PassCount+" failed="+result.FailCount);SessionState.SetBool(Key+".restore",true);SessionState.SetFloat(Key+".at",(float)EditorApplication.timeSinceStartup);}
        }
    }
}
