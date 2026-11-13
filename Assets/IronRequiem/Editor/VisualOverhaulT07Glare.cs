using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;
namespace IronRequiem.Editor
{
    public static class VisualOverhaulT07Glare
    {
        public static string Apply()
        {
            VisualOverhaulRepairApply.Guard();var log=new StringBuilder();const string folder=VisualOverhaulRepair.Art;
            var red=AssetDatabase.LoadAssetAtPath<Material>(folder+"/RobotEye_Red.mat");if(!red){red=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(red,folder+"/RobotEye_Red.mat");}red.SetColor("_BaseColor",new Color(.85f,.012f,.004f));EditorUtility.SetDirty(red);
            foreach(string path in new[]{"Assets/IronRequiem/Prefabs/Enemies/T07_LegRepair_Charged_v05.prefab","Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab","Assets/IronRequiem/Prefabs/Enemies/SawBot_SawPolish_v04.prefab","Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab"})
            {
                var root=PrefabUtility.LoadPrefabContents(path);try
                {
                    bool t07=path.Contains("T07_");
                    if(t07)
                    {
                        foreach(var weapon in root.GetComponentsInChildren<WeaponController>(true).Where(w=>w.MuzzleFlashPrefab))
                        {
                            string flashPath=folder+"/T07_MuzzleFlash_Bounded.prefab";
                            if(!File.Exists(flashPath))
                            {
                                var copy=Object.Instantiate(weapon.MuzzleFlashPrefab);copy.name="T07_MuzzleFlash_Bounded";copy.transform.SetParent(null);copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);copy.transform.localScale=Vector3.one*.22f;copy.SetActive(true);
                                foreach(var ps in copy.GetComponentsInChildren<ParticleSystem>(true)){var main=ps.main;main.loop=false;main.playOnAwake=true;main.startLifetime=.08f;main.maxParticles=8;ps.GetComponent<ParticleSystemRenderer>().maxParticleSize=.12f;}
                                PrefabUtility.SaveAsPrefabAsset(copy,flashPath);Object.DestroyImmediate(copy);
                            }
                            var old=weapon.MuzzleFlashPrefab;if(old.transform.IsChildOf(root.transform))old.SetActive(false);
                            weapon.MuzzleFlashPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(flashPath);log.AppendLine(path+" flash3.69x ->0.22x external; embedded autoplay disabled");
                        }
                        foreach(var ps in root.GetComponentsInChildren<ParticleSystem>(true).Where(p=>p.name=="DamageSparks"||p.name=="BodyDamageSmoke"||p.name=="CriticalSmoke"))
                        {
                            var t=ps.transform;var scale=t.lossyScale;t.localScale=Vector3.Scale(t.localScale,new Vector3(1/Mathf.Abs(scale.x),1/Mathf.Abs(scale.y),1/Mathf.Abs(scale.z)));log.AppendLine(ps.name+" inherited="+scale+" ->"+t.lossyScale);
                        }
                    }
                    var eye=root.GetComponentsInChildren<Renderer>(true).Single(r=>t07?r.name=="Sphere":r.name=="CoreIndicator");eye.sharedMaterial=red;
                    var lights=root.GetComponent<EnemyStateLights>();if(lights){lights.Configure(new[]{eye});var data=new SerializedObject(lights);foreach(string field in new[]{"patrol","alert","attack","warning"})data.FindProperty(field).colorValue=new Color(field=="warning"?.95f:.8f,.012f,.004f);data.ApplyModifiedPropertiesWithoutUndo();}
                    // The original auxiliary forehead strip is retained but no longer emits a second cyan eye.
                    if(t07)foreach(var r in root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("PowerIndicator")))r.enabled=false;
                    var power=root.GetComponent<EnemyPowerIndicators>();if(power)power.Configure(new[]{eye});
                    log.AppendLine(path+" red eye at "+eye.transform.position.ToString("F6"));PrefabUtility.SaveAsPrefabAsset(root,path);
                }finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();File.WriteAllText(VisualOverhaulRepair.Report+"/glare-fixed.txt",log.ToString());return log.ToString();
        }
        public static string Inspect()
        {
            var log=new StringBuilder();foreach(string path in new[]{"Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab","Assets/IronRequiem/Prefabs/Enemies/T07_LegRepair_Charged_v05.prefab","Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab","Assets/IronRequiem/Prefabs/Enemies/SawBot_SawPolish_v04.prefab"})
            {
                var root=PrefabUtility.LoadPrefabContents(path);try
                {
                    log.AppendLine("PREFAB "+path);
                    foreach(var w in root.GetComponentsInChildren<WeaponController>(true)){log.AppendLine("WEAPON "+w.name+" flash="+AssetDatabase.GetAssetPath(w.MuzzleFlashPrefab)+" flashScale="+(w.MuzzleFlashPrefab?w.MuzzleFlashPrefab.transform.lossyScale.ToString():"null")+" muzzleScale="+w.WeaponMuzzle.lossyScale);if(w.MuzzleFlashPrefab)Particles(w.MuzzleFlashPrefab,log);}
                    Particles(root,log);
                    foreach(var r in root.GetComponentsInChildren<Renderer>(true).Where(r=>!(r is ParticleSystemRenderer)))if(r.name.Contains("Indicator")||r.name.Contains("Eye")||r.name=="Sphere"||r.name.Contains("Energy"))log.AppendLine("VIEW "+AnimationUtility.CalculateTransformPath(r.transform,root.transform)+" bounds="+r.bounds+" scale="+r.transform.lossyScale+" mats="+string.Join(",",r.sharedMaterials.Select(m=>m?m.name+"/"+m.shader.name+" emit="+(m.HasProperty("_EmissionColor")?m.GetColor("_EmissionColor").ToString():"none"):"null")));
                }finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            File.WriteAllText(VisualOverhaulRepair.Report+"/glare-inspect.txt",log.ToString());return log.ToString();
        }
        static void Particles(GameObject root,StringBuilder log)
        {
            foreach(var p in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main=p.main;var r=p.GetComponent<ParticleSystemRenderer>();var mat=r?r.sharedMaterial:null;
                log.AppendLine("PS "+AnimationUtility.CalculateTransformPath(p.transform,root.transform)+" scale="+p.transform.lossyScale+" size="+main.startSizeMultiplier+" lifetime="+main.startLifetimeMultiplier+" awake="+main.playOnAwake+" scaling="+main.scalingMode+" mat="+(mat?mat.name+"/"+mat.shader.name+" base="+(mat.HasProperty("_BaseColor")?mat.GetColor("_BaseColor").ToString():"none")+" tex="+(mat.HasProperty("_BaseMap")?AssetDatabase.GetAssetPath(mat.GetTexture("_BaseMap")):"none"):"null"));
            }
        }
    }
}
