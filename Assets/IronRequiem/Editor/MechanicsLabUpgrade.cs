using System;
using System.IO;
using System.Linq;
using TMPro;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace IronRequiem.Editor
{
    public static class MechanicsLabUpgrade
    {
        public const string CannonPrefab="Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary.prefab";
        [MenuItem("Tools/Iron Requiem/Mechanics/Verify Movement and Cannons %#F7")]
        public static void Verify() => CharacterVerificationRunner.Run();
        [MenuItem("Tools/Iron Requiem/Mechanics/Upgrade CharacterLab v02 %#F8")]
        public static void Upgrade()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("A scene has user changes; it will not be saved.");
            UpgradeInput();
            Directory.CreateDirectory(Path.GetDirectoryName(CannonPrefab));AssetDatabase.Refresh();
            BuildWeapon();
            var player=PrefabUtility.LoadPrefabContents(CharacterPrototypeBuilder.PlayerPath);
            try
            {
                Ensure<MechanicInput>(player);Ensure<AdvancedPlayerMovement>(player);Ensure<StolenWeaponCarrier>(player);
                PrefabUtility.SaveAsPrefabAsset(player,CharacterPrototypeBuilder.PlayerPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(player);}
            var enemy=PrefabUtility.LoadPrefabContents(CharacterPrototypeBuilder.EnemyPath);
            try
            {
                var module=enemy.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="CannonModule_Visual").gameObject;
                Ensure<CannonAmmo>(module);var overdrive=Ensure<CannonOverdrive>(module);
                var area=Ensure<DamageArea>(module);area.AreaOfEffectDistance=4.5f;area.DamageRatioOverDistance=AnimationCurve.Linear(0,1,1,.25f);
                var explosion=Ensure<CannonExplosion>(module);
                var stolen=Ensure<StolenCannon>(module);stolen.Configure(AssetDatabase.LoadAssetAtPath<WeaponController>(CannonPrefab));
                overdrive.Configure(enemy.GetComponent<EnemyMobile>().OnDetectSfx);
                explosion.Configure(enemy.GetComponent<EnemyController>().DeathVfx);
                var audio=module.GetComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=0;audio.volume=.6f;
                PrefabUtility.SaveAsPrefabAsset(enemy,CharacterPrototypeBuilder.EnemyPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(enemy);}
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=CharacterPrototypeBuilder.ScenePath)scene=EditorSceneManager.OpenScene(CharacterPrototypeBuilder.ScenePath);
            ConsolidateHud(scene);AddHud(scene);
            EditorSceneManager.SaveScene(scene,CharacterPrototypeBuilder.ScenePath);
            CharacterPrototypeBuilder.Validate();IronRequiemSetupTools.WriteAudit();
            File.WriteAllText("Tools/IronRequiem/Reports/mechanics-upgrade.txt","Upgraded with Editor APIs at "+DateTime.UtcNow.ToString("O"));
            Debug.Log("IRON REQUIEM v0.2: movement, stolen cannon, overdrive and HUD configured.");
        }

        static T Ensure<T>(GameObject go) where T:Component
        {var component=go.GetComponent<T>();return component?component:go.AddComponent<T>();}

        static void UpgradeInput()
        {
            const string path="Assets/FPS/InputSystem_Actions.inputactions";
            var asset=AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            var map=asset.FindActionMap("Player",true);
            Add("Dash","<Keyboard>/leftCtrl");Add("CollectWeapon","<Keyboard>/e");Add("Melee","<Keyboard>/f");Add("ThrowWeapon","<Keyboard>/g");Add("Overdrive","<Keyboard>/x");
            File.WriteAllText(path,asset.ToJson());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            void Add(string name,string binding)
            {
                if(map.FindAction(name)!=null)return;
                var action=map.AddAction(name,InputActionType.Button);action.expectedControlType="Button";action.AddBinding(binding,groups:"Keyboard&Mouse");
            }
        }

        static void BuildWeapon()
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(CannonPrefab))return;
            var weapon=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab"));
            try
            {
                weapon.name="Weapon_T07_Temporary";weapon.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var controller=weapon.GetComponent<WeaponController>();
                controller.WeaponName="CAÑÓN T-07";controller.MaxAmmo=12;controller.AutomaticReload=false;controller.HasPhysicalBullets=false;
                controller.ShootType=WeaponShootType.Automatic;controller.DelayBetweenShots=.18f;controller.RecoilForce=.2f;controller.WeaponAnimator=null;
                foreach(var renderer in weapon.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
                var enemy=AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrototypeBuilder.EnemyPath);
                var source=enemy.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="CannonModule_Visual");
                var visual=new GameObject("StolenCannonVisual");visual.transform.SetParent(controller.WeaponRoot.transform,false);
                visual.transform.localRotation=source.localRotation;visual.transform.localScale=source.lossyScale*.65f;visual.transform.localPosition=new Vector3(0,-.04f,.12f);
                visual.AddComponent<MeshFilter>().sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;
                visual.AddComponent<MeshRenderer>().sharedMaterial=source.GetComponent<Renderer>().sharedMaterial;
                var muzzle=new GameObject("T07_Muzzle").transform;muzzle.SetParent(controller.WeaponRoot.transform,false);muzzle.localPosition=new Vector3(0,-.04f,.55f);controller.WeaponMuzzle=muzzle;
                PrefabUtility.SaveAsPrefabAsset(weapon,CannonPrefab);
            }
            finally{Object.DestroyImmediate(weapon);}
        }

        static void AddHud(Scene scene)
        {
            if(scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<MechanicHud>(true)).Any())return;
            var player=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<PlayerCharacterController>()).Single();
            var manager=scene.GetRootGameObjects().Single(go=>go.GetComponent<GameFlowManager>());
            var canvas=manager.GetComponentInChildren<Unity.FPS.UI.WeaponHUDManager>(true).AmmoPanel.GetComponentInParent<Canvas>();
            if(!canvas)throw new InvalidOperationException("Existing HUD Canvas not found.");
            var root=new GameObject("MechanicsHUD",typeof(RectTransform));Undo.RegisterCreatedObjectUndo(root,"Add mechanics HUD");root.transform.SetParent(canvas.transform,false);
            var rect=root.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
            var status=Text("MechanicStatus",new Vector2(1,1),new Vector2(1,1),new Vector2(-24,-88),new Vector2(380,115));status.alignment=TextAlignmentOptions.TopRight;
            var prompt=Text("PickupPrompt",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,155),new Vector2(550,45));prompt.alignment=TextAlignmentOptions.Center;
            root.AddComponent<MechanicHud>().Configure(player.GetComponent<AdvancedPlayerMovement>(),player.GetComponent<StolenWeaponCarrier>(),status,prompt);
            TMP_Text Text(string name,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
            {
                var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(root.transform,false);
                var text=go.AddComponent<TextMeshProUGUI>();text.font=TMP_Settings.defaultFontAsset;text.fontSize=23;text.color=Color.white;text.raycastTarget=false;text.text="";
                var rt=go.GetComponent<RectTransform>();rt.anchorMin=anchor;rt.anchorMax=anchor;rt.pivot=pivot;rt.anchoredPosition=position;rt.sizeDelta=size;return text;
            }
        }

        static void ConsolidateHud(Scene scene)
        {
            var roots=scene.GetRootGameObjects();
            var manager=roots.Single(go=>go.GetComponent<GameFlowManager>());
            var canvas=manager.GetComponentInChildren<Unity.FPS.UI.WeaponHUDManager>(true).AmmoPanel.GetComponentInParent<Canvas>();
            if(!canvas)throw new InvalidOperationException("The template manager's existing HUD is missing.");
            var mechanics=roots.SelectMany(go=>go.GetComponentsInChildren<MechanicHud>(true)).SingleOrDefault();
            if(mechanics && !mechanics.transform.IsChildOf(canvas.transform))
            {
                Undo.SetTransformParent(mechanics.transform,canvas.transform,"Reuse manager HUD");
                var rect=mechanics.GetComponent<RectTransform>();Undo.RecordObject(rect,"Stretch mechanics HUD");
                rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;
            }
            // GameManager already embeds the template HUD and menu. These extra lab roots
            // were introduced in v0.1; remove only those duplicates, never the source prefabs.
            foreach(var root in roots)
                if(root!=manager && (root.name=="GameHUD" || root.name=="InGameMenu"))
                    Undo.DestroyObjectImmediate(root);
        }
    }
}
