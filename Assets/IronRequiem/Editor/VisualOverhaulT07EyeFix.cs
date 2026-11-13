using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronRequiem.Editor
{
    public static class VisualOverhaulT07EyeFix
    {
        public static string Apply()
        {
            VisualOverhaulRepairApply.Guard();var log=new StringBuilder();
            const string folder="Assets/IronRequiem/Art/VisualRepair";
            string materialPath=folder+"/T07_Eye_NoHalo.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(mat,materialPath);}
            mat.SetColor("_BaseColor",new Color(.08f,.75f,.9f,1));EditorUtility.SetDirty(mat);
            foreach(string path in new[]{"Assets/IronRequiem/Prefabs/Enemies/T07_LegRepair_Charged_v05.prefab","Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab"})
            {
                var root=PrefabUtility.LoadPrefabContents(path);try
                {
                    var enemy=root.GetComponent<Unity.FPS.AI.EnemyController>();
                    log.AppendLine("BODY flash idle="+enemy.OnHitBodyGradient.Evaluate(1)+" material="+enemy.BodyMaterial);
                    if(enemy.OnHitBodyGradient.Evaluate(1).maxColorComponent>.02f)
                    {
                        var keys=enemy.OnHitBodyGradient.colorKeys;keys[keys.Length-1]=new GradientColorKey(Color.black,1);enemy.OnHitBodyGradient.SetKeys(keys,enemy.OnHitBodyGradient.alphaKeys);
                        log.AppendLine("Corrected nonblack terminal body-flash color; idle must emit zero.");
                    }
                    var eye=root.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Sphere");
                    log.AppendLine(path+" BEFORE meshDiameter="+eye.sharedMesh.bounds.size.x+" inherited="+eye.transform.lossyScale.x+" worldDiameter="+eye.sharedMesh.bounds.size.x*eye.transform.lossyScale.x);
                    // Start from the builtin sphere every time; never normalize an already-derived buffer.
                    var primitive=GameObject.CreatePrimitive(PrimitiveType.Sphere);var mesh=Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);Object.DestroyImmediate(primitive);
                    var vertices=mesh.vertices;var scale=eye.transform.lossyScale;for(int i=0;i<vertices.Length;i++)vertices[i]=new Vector3(vertices[i].x*.045f/Mathf.Abs(scale.x),vertices[i].y*.045f/Mathf.Abs(scale.y),vertices[i].z*.045f/Mathf.Abs(scale.z));mesh.vertices=vertices;mesh.RecalculateBounds();
                    eye.sharedMesh=VisualOverhaulRepairApply.SaveMesh(mesh,"T07_Eye_45mm_FixedBuffer");var renderer=eye.GetComponent<Renderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    foreach(var col in eye.GetComponents<Collider>())Object.DestroyImmediate(col);
                    var state=root.GetComponent<EnemyStateLights>();if(state)
                    {
                        var data=new SerializedObject(state);foreach(string key in new[]{"patrol","alert","attack","warning"}){var p=data.FindProperty(key);var color=p.colorValue;float max=Mathf.Max(1,color.maxColorComponent);p.colorValue=new Color(color.r/max*.9f,color.g/max*.9f,color.b/max*.9f,1);}data.ApplyModifiedPropertiesWithoutUndo();
                    }
                    log.AppendLine("AFTER worldDiameter="+eye.sharedMesh.bounds.size.x*Mathf.Abs(scale.x)+" position="+eye.transform.position.ToString("F6")+" no HDR bloom/no native Light");
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();File.WriteAllText(VisualOverhaulRepair.Report+"/t07-eye-fix.txt",log.ToString());return log.ToString();
        }
    }
}
