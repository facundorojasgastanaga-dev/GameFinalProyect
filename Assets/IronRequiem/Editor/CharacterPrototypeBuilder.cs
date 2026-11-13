using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    public static class CharacterPrototypeBuilder
    {
        public const string ScenePath = "Assets/IronRequiem/Scenes/CharacterLab.unity";
        public const string PlayerPath = "Assets/IronRequiem/Prefabs/Player/Player_IR_v01.prefab";
        public const string EnemyPath = "Assets/IronRequiem/Prefabs/Enemies/T07_IR_v01.prefab";
        const string Art = "Assets/IronRequiem/Art/Player/Prototype";
        const string EnemyArt = "Assets/IronRequiem/Art/Enemies/T07New";
        const string VisualPath = Art + "/SurvivorRobot_Visual.prefab";
        const string Reports = "Tools/IronRequiem/Reports";

        [MenuItem("Tools/Iron Requiem/Characters/Build Separate Character Lab")]
        public static void BuildAll()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save your own scene changes before building the separate lab.");
            try
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
                    throw new InvalidOperationException("CharacterLab already exists. Do not overwrite edited work.");
                foreach (var folder in new[] { Art, EnemyArt + "/Materials", "Assets/IronRequiem/Prefabs/Player", "Assets/IronRequiem/Prefabs/Enemies", "Assets/IronRequiem/Scenes", Reports })
                    Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                BuildPlayerVisual();
                BuildPlayer();
                BuildEnemy();
                BuildLab();
                AssetDatabase.SaveAssets();
                Validate();
                IronRequiemSetupTools.WriteAudit();
                Debug.Log("IRON REQUIEM character preparation complete.");
            }
            finally { if (setup.Length > 0 && setup.Any(s => s.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        static Material Material(string path, Color color, float metal = 0, Color? emission = null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = Path.GetFileNameWithoutExtension(path);
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Metallic", metal); mat.SetFloat("_Smoothness", 0.35f);
            if (emission.HasValue) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", emission.Value); }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static Transform Socket(Transform parent, string name, Vector3 position)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t;
        }

        static void BuildPlayerVisual()
        {
            var body = new GameObject("SurvivorRobot_Visual");
            var dark = Material(Art + "/Graphite.mat", new Color(.055f,.075f,.09f), .65f);
            var white = Material(Art + "/WornCeramic.mat", new Color(.7f,.73f,.7f), .25f);
            var orange = Material(Art + "/RescueOrange.mat", new Color(.95f,.23f,.035f), .35f);
            var cyan = Material(Art + "/EnergyCyan.mat", new Color(.02f,.65f,.8f), .25f, new Color(.02f,1.4f,1.8f));
            var t = body.transform;
            Box("Reactor", t, new Vector3(0,1.28f,0), new Vector3(.33f,.38f,.24f), dark);
            Box("ChestL", t, new Vector3(-.095f,1.36f,.13f), new Vector3(.17f,.21f,.08f), white);
            Box("ChestR", t, new Vector3(.095f,1.36f,.13f), new Vector3(.17f,.21f,.08f), white);
            Box("Core", t, new Vector3(0,1.3f,.18f), new Vector3(.045f,.13f,.025f), cyan);
            Box("Back", t, new Vector3(0,1.3f,-.16f), new Vector3(.25f,.29f,.1f), white);
            Box("Spine", t, new Vector3(0,1.08f,0), new Vector3(.19f,.16f,.18f), dark);
            Box("Pelvis", t, new Vector3(0,.96f,0), new Vector3(.29f,.15f,.22f), white);
            Box("Belt", t, new Vector3(0,1.02f,.13f), new Vector3(.24f,.045f,.025f), orange);
            Box("Neck", t, new Vector3(0,1.51f,0), new Vector3(.11f,.08f,.12f), dark);
            Box("Helmet", t, new Vector3(0,1.67f,0), new Vector3(.24f,.26f,.23f), white);
            Box("VisorRecess", t, new Vector3(0,1.7f,.122f), new Vector3(.225f,.07f,.035f), dark);
            Box("Visor", t, new Vector3(0,1.7f,.144f), new Vector3(.19f,.025f,.012f), cyan);
            Box("Jaw", t, new Vector3(0,1.59f,.13f), new Vector3(.13f,.06f,.03f), dark);
            foreach (float side in new[] {-1f,1f})
            {
                Vector3 shoulder = new Vector3(side*.25f,1.43f,0), elbow = new Vector3(side*.39f,1.2f,0), wrist = new Vector3(side*.49f,.99f,0);
                Segment("UpperArm", shoulder, elbow, .11f, white);
                Segment("Forearm", elbow, wrist, .105f, dark);
                Box("Shoulder", t, shoulder, new Vector3(.16f,.15f,.22f), white);
                Box("ShoulderStripe", t, shoulder+new Vector3(0,.08f,.02f), new Vector3(.13f,.025f,.19f), orange);
                Box("ElbowJoint", t, elbow, new Vector3(.085f,.085f,.11f), dark);
                Box("ArmSocket", t, (elbow+wrist)*.5f+Vector3.forward*.075f, new Vector3(.07f,.08f,.035f), cyan);
                Box("Palm", t, wrist+Vector3.down*.07f, new Vector3(.105f,.12f,.055f), white);
                for (int finger = 0; finger < 4; finger++)
                    Box("Finger", t, wrist+new Vector3((finger-1.5f)*.026f,-.175f,.005f), new Vector3(.018f,.085f-Mathf.Abs(finger-1.5f)*.012f,.025f), dark);
                Box("Thumb", t, wrist+new Vector3(-side*.072f,-.095f,.025f), new Vector3(.025f,.07f,.028f), dark);
                Vector3 hip=new Vector3(side*.105f,.88f,0), knee=new Vector3(side*.13f,.52f,.01f), ankle=new Vector3(side*.15f,.16f,0);
                Segment("Thigh", hip,knee,.15f,dark); Segment("Shin",knee,ankle,.14f,white);
                Box("KneeJoint",t,knee,new Vector3(.15f,.105f,.16f),orange);
                Box("ThighPlate",t,(hip+knee)*.5f+Vector3.forward*.08f,new Vector3(.13f,.24f,.035f),white);
                Box("Boot",t,new Vector3(side*.15f,.075f,.06f),new Vector3(.18f,.15f,.31f),dark);
                Box("ToeCap",t,new Vector3(side*.15f,.105f,.17f),new Vector3(.18f,.08f,.1f),white);
                Socket(t, side < 0 ? "ForearmSocket_R" : "ForearmSocket_L", wrist);
            }
            foreach (var collider in body.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            // Reuse the template's material batching, then persist its resulting meshes as assets.
            var previous = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
            MeshCombineUtility.Combine(body.GetComponentsInChildren<MeshRenderer>().ToList(), MeshCombineUtility.RendererDisposeMethod.DestroyGameObject, "SurvivorBatch");
            int index=0;
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects().Where(g => !previous.Contains(g)).ToArray())
            {
                var filter=go.GetComponent<MeshFilter>();
                if (!filter) continue;
                AssetDatabase.CreateAsset(filter.sharedMesh, Art + "/SurvivorBatch" + index++ + ".asset");
                go.name=go.GetComponent<Renderer>().sharedMaterial.name;
                go.transform.SetParent(t, true);
            }
            PrefabUtility.SaveAsPrefabAsset(body, VisualPath);
            Capture(body, "player-robot-v01.png", new Vector3(2.3f,1.5f,3.5f), new Vector3(0,1,0));
            Object.DestroyImmediate(body);

            void Segment(string name, Vector3 a, Vector3 b, float width, Material mat)
            {
                var go=Box(name,t,(a+b)*.5f,new Vector3(width,Vector3.Distance(a,b),width),mat);
                go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
            }
        }

        static GameObject Instantiate(string path) => (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));

        static void BuildPlayer()
        {
            var player=Instantiate("Assets/FPS/Prefabs/Player.prefab"); player.name="Player_IR_v01";
            var oldCapsule=player.transform.Find("Capsule");
            if (oldCapsule) foreach(var r in oldCapsule.GetComponentsInChildren<Renderer>()) r.enabled=false;
            var visual=Instantiate(VisualPath); visual.transform.SetParent(player.transform,false);
            player.AddComponent<PlayerBodyPresentation>().Configure(visual.transform);
            var manager=player.GetComponent<PlayerWeaponsManager>();
            var second=AssetDatabase.LoadAssetAtPath<WeaponController>("Assets/FPS/Prefabs/Weapons/Weapon_Shotgun.prefab");
            if (!manager.StartingWeapons.Contains(second)) manager.StartingWeapons.Add(second);
            PrefabUtility.SaveAsPrefabAsset(player,PlayerPath);
            Object.DestroyImmediate(player);
        }

        static void BuildEnemy()
        {
            foreach (string path in AssetDatabase.FindAssets("t:Texture2D",new[]{EnemyArt+"/Textures"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var ti=(TextureImporter)AssetImporter.GetAtPath(path);
                ti.maxTextureSize=2048;
                ti.textureType=path.Contains("Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
                ti.sRGBTexture=path.Contains("BaseColor"); ti.SaveAndReimport();
            }
            var mat=Material(EnemyArt+"/Materials/T07_URP.mat",Color.white,.7f);
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(EnemyArt+"/Textures/T07_BaseColor.png"));
            mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(EnemyArt+"/Textures/T07_Normal.png")); mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(EnemyArt+"/Textures/T07_MetalSmooth.png")); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.SetFloat("_Smoothness",1);
            var source=AssetImporter.GetAtPath(EnemyArt+"/T07_Prepared.fbx") as ModelImporter;
            source.animationType=ModelImporterAnimationType.None; source.importAnimation=false; source.SaveAndReimport();
            var enemy=Instantiate("Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab"); enemy.name="T07_IR_v01";
            enemy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            foreach(var r in enemy.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.enabled=false;
            foreach(var c in enemy.GetComponentsInChildren<Collider>(true)) c.enabled=false;
            var visual=Instantiate(EnemyArt+"/T07_Prepared.fbx"); visual.name="T07_Visual";visual.transform.SetParent(enemy.transform,false);
            foreach(var r in visual.GetComponentsInChildren<Renderer>()) r.sharedMaterial=mat;
            var ec=enemy.GetComponent<EnemyController>(); var mobile=enemy.GetComponent<EnemyMobile>();
            var bodyBox=enemy.AddComponent<CapsuleCollider>(); bodyBox.center=Vector3.up;bodyBox.height=2;bodyBox.radius=.3f;
            enemy.AddComponent<Damageable>(); enemy.GetComponent<Health>().MaxHealth=160;
            var aim=Socket(enemy.transform,"AimPoint",new Vector3(0,1.4f,0));
            enemy.GetComponent<Actor>().AimPoint=aim;ec.DeathVfxSpawnPoint=aim;ec.BodyMaterial=mat;ec.DropRate=0;
            var detection=enemy.GetComponentInChildren<DetectionModule>();detection.DetectionSourcePoint=aim;detection.DetectionRange=16;detection.AttackRange=10;detection.KnownTargetTimeout=2;
            var agent=enemy.GetComponent<NavMeshAgent>();agent.radius=.4f;agent.height=2.05f;agent.baseOffset=0;agent.stoppingDistance=1;
            var nav=enemy.GetComponentInChildren<NavigationModule>();if(!nav)nav=enemy.AddComponent<NavigationModule>();nav.MoveSpeed=3.5f;nav.AngularSpeed=180;nav.Acceleration=12;
            // Authored sockets are preparation guides, not an imported deformation skeleton.
            var sockets=Socket(enemy.transform,"ArticulationGuides_NoRig",Vector3.zero);
            Socket(sockets,"Head",new Vector3(0,1.65f,0));
            foreach(float s in new[]{-1f,1f})
            { Socket(sockets,"Shoulder_"+s,new Vector3(s*.33f,1.51f,0));Socket(sockets,"Elbow_"+s,new Vector3(s*.34f,1.17f,0));Socket(sockets,"Hip_"+s,new Vector3(s*.14f,.94f,0));Socket(sockets,"Knee_"+s,new Vector3(s*.17f,.56f,.01f)); }
            var controller=AnimatorController.CreateAnimatorControllerAtPath(EnemyArt+"/T07_Contract.controller");
            controller.AddParameter("MoveSpeed",AnimatorControllerParameterType.Float);controller.AddParameter("Attack",AnimatorControllerParameterType.Trigger);controller.AddParameter("Alerted",AnimatorControllerParameterType.Bool);controller.AddParameter("OnDamaged",AnimatorControllerParameterType.Trigger);
            controller.layers[0].stateMachine.AddState("StaticPrototype_NoSourceClips");
            var animator=enemy.AddComponent<Animator>();animator.runtimeAnimatorController=controller;mobile.Animator=animator;mobile.RandomHitSparks=Array.Empty<ParticleSystem>();mobile.AttackStopDistanceRatio=.7f;
            var cannon=visual.GetComponentsInChildren<Transform>().Single(t=>t.name=="CannonModule");
            var weapon=enemy.GetComponentInChildren<WeaponController>();
            var pivot=cannon.position;
            weapon.transform.parent.position=pivot;weapon.transform.parent.rotation=Quaternion.identity;
            weapon.transform.localPosition=Vector3.zero;weapon.transform.localRotation=Quaternion.identity;
            var gunRoot=Socket(weapon.transform,"CannonMount",Vector3.zero);
            var module=new GameObject("CannonModule_Visual");
            module.AddComponent<MeshFilter>().sharedMesh=cannon.GetComponent<MeshFilter>().sharedMesh;
            module.AddComponent<MeshRenderer>().sharedMaterial=mat;
            module.transform.SetPositionAndRotation(cannon.position,cannon.rotation);module.transform.localScale=cannon.lossyScale;
            module.transform.SetParent(gunRoot,true);module.transform.RotateAround(gunRoot.position,Vector3.right,-90);
            cannon.gameObject.SetActive(false);
            weapon.WeaponRoot=gunRoot.gameObject;weapon.WeaponMuzzle=Socket(gunRoot,"Muzzle",new Vector3(0,0,.62f));weapon.DelayBetweenShots=.85f;
            var joint=Socket(gunRoot,"CannonJoint",new Vector3(0,0,.03f));
            joint.gameObject.AddComponent<Health>().MaxHealth=35; joint.gameObject.AddComponent<Damageable>();
            var hitbox=joint.gameObject.AddComponent<SphereCollider>();hitbox.radius=.16f;
            var marker=Box("JointIndicator",joint,Vector3.zero,new Vector3(.19f,.11f,.08f),AssetDatabase.LoadAssetAtPath<Material>(Art+"/RescueOrange.mat")); Object.DestroyImmediate(marker.GetComponent<Collider>());
            ec.EyeColorMaterial=marker.GetComponent<Renderer>().sharedMaterial;
            var detachable=joint.gameObject.AddComponent<DetachableCannon>();detachable.Configure(module.transform,ec,mobile,hitbox);
            enemy.AddComponent<DisarmedEnemyRetreat>().Configure(detachable,ec);
            PrefabUtility.SaveAsPrefabAsset(enemy,EnemyPath);
            Capture(enemy,"enemy-t07-v01.png",new Vector3(-3,1.7f,4),new Vector3(0,1,0));
            Object.DestroyImmediate(enemy);
        }

        // One-time repair for the initial generated assets; preserves their GUIDs and variants.
        public static void FinalizeGeneratedAssets()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("A user scene is dirty.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var enemy=PrefabUtility.LoadPrefabContents(EnemyPath);
            try
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(EnemyArt+"/T07_Prepared.fbx").GetComponentsInChildren<Transform>().Single(t=>t.name=="CannonModule");
                var cannon=enemy.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="CannonModule");
                cannon.localPosition=source.localPosition;cannon.localRotation=source.localRotation;cannon.localScale=source.localScale;
                var pivot=cannon.position;var weapon=enemy.GetComponentInChildren<WeaponController>();
                weapon.transform.parent.position=pivot;weapon.transform.parent.rotation=Quaternion.identity;
                weapon.transform.localPosition=Vector3.zero;weapon.transform.localRotation=Quaternion.identity;
                var gunRoot=weapon.WeaponRoot.transform;gunRoot.localPosition=Vector3.zero;gunRoot.localRotation=Quaternion.identity;
                var module=gunRoot.Find("CannonModule_Visual");
                if(!module)module=new GameObject("CannonModule_Visual").transform;
                if(!module.GetComponent<MeshFilter>())module.gameObject.AddComponent<MeshFilter>();
                if(!module.GetComponent<MeshRenderer>())module.gameObject.AddComponent<MeshRenderer>();
                module.GetComponent<MeshFilter>().sharedMesh=cannon.GetComponent<MeshFilter>().sharedMesh;
                module.GetComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(EnemyArt+"/Materials/T07_URP.mat");
                module.SetParent(null,false);module.SetPositionAndRotation(cannon.position,cannon.rotation);module.localScale=cannon.lossyScale;
                module.SetParent(gunRoot,true);module.RotateAround(gunRoot.position,Vector3.right,-90);
                cannon.gameObject.SetActive(false);
                var joint=enemy.GetComponentInChildren<DetachableCannon>();joint.transform.SetParent(gunRoot,false);joint.transform.localPosition=new Vector3(0,0,.03f);joint.transform.localRotation=Quaternion.identity;
                joint.Configure(module,enemy.GetComponent<EnemyController>(),enemy.GetComponent<EnemyMobile>(),joint.GetComponent<Collider>());
                PrefabUtility.SaveAsPrefabAsset(enemy,EnemyPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(enemy);}
            var visual=Instantiate(VisualPath);Capture(visual,"player-robot-v01.png",new Vector3(2.3f,1.5f,3.5f),new Vector3(0,1,0));Object.DestroyImmediate(visual);
            var preview=Instantiate(EnemyPath);Capture(preview,"enemy-t07-v01.png",new Vector3(-3,1.7f,4),new Vector3(0,1,0));Object.DestroyImmediate(preview);
            AssetDatabase.SaveAssets();Validate();
            if(setup.Length>0 && setup.Any(s=>s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        static void BuildLab()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var floorMat=Material("Assets/IronRequiem/Scenes/LabFloor.mat",new Color(.13f,.16f,.18f),.15f);
            var coverMat=Material("Assets/IronRequiem/Scenes/LabCover.mat",new Color(.28f,.31f,.32f),.35f);
            var arena=new GameObject("Arena_NavGeometry");
            Box("Floor_32m",arena.transform,new Vector3(0,-.25f,0),new Vector3(32,.5f,32),floorMat);
            Box("NorthWall",arena.transform,new Vector3(0,2,16),new Vector3(32,4,.5f),coverMat);
            Box("SouthWall",arena.transform,new Vector3(0,2,-16),new Vector3(32,4,.5f),coverMat);
            Box("EastWall",arena.transform,new Vector3(16,2,0),new Vector3(.5f,4,32),coverMat);
            Box("WestWall",arena.transform,new Vector3(-16,2,0),new Vector3(.5f,4,32),coverMat);
            Box("LineOfSightCover",arena.transform,new Vector3(-4,1.4f,0),new Vector3(3,2.8f,2),coverMat);
            Box("LowObstacle",arena.transform,new Vector3(4,.3f,-4),new Vector3(2,.6f,1.5f),coverMat);
            var ramp=Box("Slope_15deg",arena.transform,new Vector3(7,.5f,2),new Vector3(3,.4f,5),coverMat);ramp.transform.localRotation=Quaternion.Euler(-15,0,0);
            Box("CrouchClearance",arena.transform,new Vector3(-7,1.4f,-6),new Vector3(3,.3f,2),coverMat);
            var surface=arena.AddComponent<Unity.AI.Navigation.NavMeshSurface>(); surface.collectObjects=Unity.AI.Navigation.CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.agentTypeID=0;surface.BuildNavMesh();
            if(!surface.navMeshData) throw new InvalidOperationException("Lab NavMesh bake failed.");
            AssetDatabase.CreateAsset(surface.navMeshData,"Assets/IronRequiem/Scenes/CharacterLab_NavMesh.asset");
            Instantiate("Assets/FPS/Prefabs/GameManager.prefab");
            Instantiate("Assets/FPS/Prefabs/UI/GameHUD.prefab");
            Instantiate("Assets/FPS/Prefabs/UI/InGameMenu.prefab");
            CopyTemplateEventSystem(scene);
            var player=Instantiate(PlayerPath);player.transform.position=new Vector3(0,.05f,-10);
            var enemy=Instantiate(EnemyPath);enemy.transform.position=new Vector3(0,.05f,8);enemy.transform.rotation=Quaternion.Euler(0,180,0);
            var path=new GameObject("T07_PatrolPath").AddComponent<PatrolPath>();
            foreach(var p in new[]{new Vector3(-4,0,8),new Vector3(4,0,8),new Vector3(4,0,12),new Vector3(-4,0,12)}) path.PathNodes.Add(Socket(path.transform,"Node",p));
            path.EnemiesToAssign.Add(enemy.GetComponent<EnemyController>());
            var display=Instantiate(VisualPath);display.name="Player_Visual_Inspection";display.transform.position=new Vector3(3,0,-7);display.transform.rotation=Quaternion.Euler(0,180,0);
            var light=new GameObject("Lab_KeyLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(45,-35,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.42f,.45f,.5f);
            EditorSceneManager.SaveScene(scene,ScenePath);
        }

        static void CopyTemplateEventSystem(Scene destination)
        {
            if(destination.GetRootGameObjects().Any(go=>go.name=="EventSystem"))return;
            var original=EditorSceneManager.OpenScene("Assets/FPS/Scenes/MainScene.unity",OpenSceneMode.Additive);
            try
            {
                var source=original.GetRootGameObjects().Single(go=>go.name=="EventSystem");
                var copy=Object.Instantiate(source);copy.name="EventSystem";Undo.RegisterCreatedObjectUndo(copy,"Copy template UI event system");
                SceneManager.MoveGameObjectToScene(copy,destination);
            }
            finally { EditorSceneManager.CloseScene(original,true); }
        }

        [MenuItem("Tools/Iron Requiem/Characters/Repair Generated Lab and Verify %#F9")]
        public static void RepairLabAndVerify()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.isDirty)throw new InvalidOperationException("The scene has pending user changes.");
            if(scene.path!=ScenePath)scene=EditorSceneManager.OpenScene(ScenePath);
            var enemy=PrefabUtility.LoadPrefabContents(EnemyPath);
            try
            {
                var ec=enemy.GetComponent<EnemyController>();
                Undo.RecordObject(ec,"Assign joint signal material");
                ec.EyeColorMaterial=enemy.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="JointIndicator").GetComponent<Renderer>().sharedMaterial;
                foreach(var animator in enemy.GetComponentsInChildren<Animator>(true))if(animator!=enemy.GetComponent<EnemyMobile>().Animator)animator.enabled=false;
                PrefabUtility.SaveAsPrefabAsset(enemy,EnemyPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(enemy);}
            CopyTemplateEventSystem(scene);EditorSceneManager.SaveScene(scene,ScenePath);
            CharacterVerificationRunner.Run();
        }

        [MenuItem("Tools/Iron Requiem/Characters/Validate Character Lab")]
        public static void Validate()
        {
            var player=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);var enemy=AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath);
            Require(PrefabUtility.GetPrefabAssetType(player)==PrefabAssetType.Variant,"Player must remain a template variant");
            Require(PrefabUtility.GetPrefabAssetType(enemy)==PrefabAssetType.Variant,"Enemy must remain a template variant");
            Require(player.GetComponent<PlayerCharacterController>() && player.GetComponent<PlayerInputHandler>() && player.GetComponent<PlayerWeaponsManager>(),"Player template components missing");
            Require(player.GetComponent<PlayerCharacterController>().PlayerCamera,"FPS camera reference missing");
            Require(player.GetComponent<PlayerWeaponsManager>().StartingWeapons.Count==1,"Design v02 requires exactly one starting pistol");
            Require(enemy.GetComponent<EnemyController>() && enemy.GetComponent<EnemyMobile>() && enemy.GetComponent<NavMeshAgent>(),"Enemy template components missing");
            Require(enemy.GetComponentInChildren<DetachableCannon>(),"Cannon attachment missing");
            int missing=new[]{player,enemy}.Sum(go=>go.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            Require(missing==0,"Missing scripts found");
            var report=new {utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,scene=ScenePath,player=PlayerPath,enemy=EnemyPath,missingScripts=missing,playerVariant=true,enemyVariant=true,sourceRig=false,sourceAnimations=false,playmodeVerified=false};
            File.WriteAllText(Reports+"/character-references.json",JsonUtility.ToJson(new ReferenceReport {utc=report.utc,unity=report.unity,scene=ScenePath,player=PlayerPath,enemy=EnemyPath,missingScripts=missing,playerVariant=true,enemyVariant=true},true));
        }

        [Serializable] class ReferenceReport { public string utc,unity,scene,player,enemy;public int missingScripts;public bool playerVariant,enemyVariant; }
        static void Require(bool result,string message) { if(!result) throw new InvalidOperationException(message); }

        static void Capture(GameObject target,string filename,Vector3 position,Vector3 aim)
        {
            var key=new GameObject("PreviewKey").AddComponent<Light>();key.type=LightType.Directional;key.intensity=2;key.transform.rotation=Quaternion.Euler(35,-30,0);
            RenderSettings.ambientLight=Color.gray;
            var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.transform.position=position;camera.transform.LookAt(aim);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.105f,.125f);camera.fieldOfView=32;
            var rt=new RenderTexture(900,900,24);camera.targetTexture=rt;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(900,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,900,900),0,0);tex.Apply();File.WriteAllBytes(Reports+"/"+filename,tex.EncodeToPNG());RenderTexture.active=previous;
            camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(key.gameObject);
        }
    }
}
