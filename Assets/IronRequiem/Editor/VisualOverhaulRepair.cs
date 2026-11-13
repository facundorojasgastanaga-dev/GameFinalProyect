using System;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace IronRequiem.Editor
{
    public static class VisualOverhaulRepair
    {
        public const string Report="Tools/IronRequiem/Reports/VisualRepair",Art="Assets/IronRequiem/Art/VisualRepair";
        static readonly string[] Targets={"Assets/IronRequiem/Prefabs/Enemies/BigBot_ReferenceCharge_v09.prefab","Assets/IronRequiem/Prefabs/Enemies/T07_LegRepair_Charged_v05.prefab","Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab","Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab","Assets/IronRequiem/Prefabs/Campaign/HoverBot_Current.prefab","Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab",VisualOverhaulWeapons.Cannon,VisualOverhaulWeapons.Shotgun};
        public static string Inspect()
        {
            Directory.CreateDirectory(Report);var log=new StringBuilder();var stage=PrefabStageUtility.GetCurrentPrefabStage();log.AppendLine("PrefabStage="+(stage?stage.assetPath:"none")+" dirty="+(stage&&stage.scene.isDirty));log.AppendLine("Selected="+Selection.activeObject);
            foreach(string path in Targets)
            {
                var root=PrefabUtility.LoadPrefabContents(path);try
                {
                    log.AppendLine("PREFAB "+path+" root="+root.transform.localScale);
                    var weapon=root.GetComponent<WeaponController>();if(weapon)log.AppendLine("WEAPON muzzle="+root.transform.InverseTransformPoint(weapon.WeaponMuzzle.position)+" attachment="+root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Attachment").localPosition+" delay="+weapon.DelayBetweenShots+" projectile="+AssetDatabase.GetAssetPath(weapon.ProjectilePrefab));
                    foreach(var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        var filter=r.GetComponent<MeshFilter>();var mesh=filter?filter.sharedMesh:(r as SkinnedMeshRenderer)?.sharedMesh;
                        bool primitive=mesh&&(mesh.name=="Cube"||mesh.name=="Sphere"||mesh.name=="Cylinder");bool eye=r.name.IndexOf("eye",StringComparison.OrdinalIgnoreCase)>=0||r.name.Contains("Indicator");
                        if(primitive||eye)log.AppendLine("MARKER "+AnimationUtility.CalculateTransformPath(r.transform,root.transform)+" mesh="+(mesh?mesh.name:"null")+" self="+r.gameObject.activeSelf+" enabled="+r.enabled+" forceOff="+r.forceRenderingOff+" layer="+r.gameObject.layer+" local="+r.transform.localPosition+" rotation="+r.transform.localRotation+" scale="+r.transform.localScale+" world="+r.transform.position+" worldScale="+r.transform.lossyScale+" bounds="+r.bounds+" components="+string.Join(",",r.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name))+" mats="+string.Join(" | ",r.sharedMaterials.Select(m=>m?m.name+":"+m.shader.name+":"+(m.HasProperty("_BaseColor")?m.GetColor("_BaseColor").ToString():"")+":"+(m.HasProperty("_EmissionColor")?m.GetColor("_EmissionColor").ToString():""):"NULL")));
                        if(r is SkinnedMeshRenderer skin){log.AppendLine("SKIN "+r.name+" mesh="+AssetDatabase.GetAssetPath(mesh)+" bones="+skin.bones.Length+" bounds="+skin.localBounds+" readable="+mesh.isReadable);if(mesh.isReadable){var weights=mesh.boneWeights;log.AppendLine(" weights mixed="+weights.Count(w=>w.weight1>.05f)+" / "+weights.Length);}}
                    }
                    foreach(var animator in root.GetComponentsInChildren<Animator>(true))if(animator.runtimeAnimatorController)log.AppendLine("ANIM "+animator.name+" rootMotion="+animator.applyRootMotion+" clips="+string.Join(",",animator.runtimeAnimatorController.animationClips.Distinct().Select(c=>c.name)));
                }finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.OpenScene(CampaignAuthoring.Main);foreach(var root in scene.GetRootGameObjects())foreach(var player in root.GetComponentsInChildren<PlayerWeaponsManager>(true))log.AppendLine("LOADOUT "+player.name+" starting="+string.Join(",",player.StartingWeapons.Select(w=>w?AssetDatabase.GetAssetPath(w):"NULL")));
                foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)){var urp=camera.GetUniversalAdditionalCameraData();log.AppendLine("CAM "+camera.name+" type="+urp.renderType+" post="+urp.renderPostProcessing+" volumeMask="+urp.volumeLayerMask+" mask="+camera.cullingMask);}
                foreach(var volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))log.AppendLine("VOLUME "+volume.name+" enabled="+volume.enabled+" global="+volume.isGlobal+" layer="+volume.gameObject.layer+" profile="+AssetDatabase.GetAssetPath(volume.sharedProfile)+" overrides="+(volume.sharedProfile?string.Join(",",volume.sharedProfile.components.Select(c=>c.name+":"+c.active)):"NULL"));
            }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
            File.WriteAllText(Report+"/inspection.txt",log.ToString());return "Inspected markers, rigs, loadout and camera/volumes; "+Report+"/inspection.txt";
        }
    }
}
