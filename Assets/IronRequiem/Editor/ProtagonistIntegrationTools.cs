using System;
using System.IO;
using System.Linq;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace IronRequiem.Editor
{
    public static class ProtagonistIntegrationTools
    {
        public const string Art="Assets/IronRequiem/Art/Player/ProtagonistV01";
        public const string Output="Tools/IronRequiem/Reports/Protagonist";
        public static void InspectImports()
        {
            Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            using(var writer=new StreamWriter(Output+"/unity-imports.txt"))
            {
                foreach(string path in AssetDatabase.FindAssets("t:Model",new[]{Art}).Select(AssetDatabase.GUIDToAssetPath))
                {
                    var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.materialImportMode=ModelImporterMaterialImportMode.None;
                    importer.animationType=path.Contains("Rigged")?ModelImporterAnimationType.Generic:ModelImporterAnimationType.None;
                    importer.importAnimation=path.Contains("Rigged");importer.isReadable=false;importer.SaveAndReimport();
                    var go=AssetDatabase.LoadAssetAtPath<GameObject>(path);writer.WriteLine(path);
                    foreach(var r in go.GetComponentsInChildren<Renderer>(true))writer.WriteLine(r.name+" bounds="+r.bounds+" local="+r.transform.localPosition+" scale="+r.transform.lossyScale+" skin="+(r is SkinnedMeshRenderer));
                    foreach(var t in go.GetComponentsInChildren<Transform>(true).Where(t=>new[]{"Forearm_R","Hand_R","Head","Root"}.Contains(t.name)))writer.WriteLine("bone "+t.name+" position="+t.position+" scale="+t.lossyScale);
                    foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")))writer.WriteLine("clip "+clip.name+" duration="+clip.length);
                }
                foreach(string path in new[]{CharacterPrototypeBuilder.PlayerPath,DesignV02Upgrade.PistolPath})
                {
                    var go=PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        writer.WriteLine("PREFAB "+path);
                        var wm=go.GetComponent<PlayerWeaponsManager>();if(wm)writer.WriteLine("defaults="+wm.DefaultWeaponPosition.localPosition+" aim="+wm.AimingWeaponPosition.localPosition+" starting="+string.Join(",",wm.StartingWeapons.Select(w=>w.name)));
                        foreach(var t in go.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==go.transform))writer.WriteLine("child "+t.name);
                        var wc=go.GetComponent<Unity.FPS.Game.WeaponController>();if(wc)writer.WriteLine("muzzle="+wc.WeaponMuzzle.position+" forward="+wc.WeaponMuzzle.forward);
                    }
                    finally{PrefabUtility.UnloadPrefabContents(go);}
                }
            }
            IronRequiemSetupTools.WriteAudit();
        }
    }
}
