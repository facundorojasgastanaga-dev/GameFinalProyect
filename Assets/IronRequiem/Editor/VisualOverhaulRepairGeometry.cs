using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    public static class VisualOverhaulRepairGeometry
    {
        static readonly string[] Robots={"BigBot_Current","T07_Current_Repaired","SawBot_Current","HoverBot_Current","Turret_Current"};
        public static string Inspect(string phase)
        {
            Directory.CreateDirectory(VisualOverhaulRepair.Report);var log=new StringBuilder();
            foreach(string name in Robots)
            {
                var root=PrefabUtility.LoadPrefabContents("Assets/IronRequiem/Prefabs/Campaign/"+name+".prefab");try
                {
                    var animator=root.GetComponentInChildren<Animator>(true);if(!animator||!animator.runtimeAnimatorController)continue;
                    var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&(r.name.Contains("LOD0")||!r.name.Contains("LOD"))).ToArray();
                    foreach(var skin in skins)
                    {
                        var mesh=new Mesh();skin.BakeMesh(mesh,false);var rest=mesh.vertices;var triangles=mesh.triangles;Object.DestroyImmediate(mesh);float worst=1;string at="rest";int longEdges=0;
                        foreach(var clip in animator.runtimeAnimatorController.animationClips.Distinct())
                        {
                            float clipWorst=1;
                            for(int sample=0;sample<7;sample++)
                            {
                                clip.SampleAnimation(animator.gameObject,clip.length*sample/6);mesh=new Mesh();skin.BakeMesh(mesh,false);var posed=mesh.vertices;Object.DestroyImmediate(mesh);
                                if(posed.Length!=rest.Length)throw new Exception("Baked vertex count changed");
                                for(int t=0;t<triangles.Length;t+=3)for(int e=0;e<3;e++)
                                {
                                    int a=triangles[t+e],b=triangles[t+(e+1)%3];float baseline=(rest[a]-rest[b]).magnitude;if(baseline<.00001f)continue;float ratio=(posed[a]-posed[b]).magnitude/baseline;
                                    if(ratio>worst){worst=ratio;at=clip.name+"@"+sample+" edge="+a+","+b;}if(ratio>clipWorst)clipWorst=ratio;if(ratio>2)longEdges++;
                                }
                            }
                            if(clipWorst>1.1f)log.AppendLine(" clip "+name+" "+skin.name+" "+clip.name+" stretch="+clipWorst.ToString("F3")+" scaleBindings="+AnimationUtility.GetCurveBindings(clip).Count(b=>b.propertyName.Contains("Scale")));
                        }
                        log.AppendLine(name+" "+skin.name+" maxEdgeStretch="+worst.ToString("F3")+" edgesOver2="+longEdges+" "+at);
                        var idle=animator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name.IndexOf("idle",StringComparison.OrdinalIgnoreCase)>=0);if(idle)idle.SampleAnimation(animator.gameObject,0);
                    }
                }finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            File.WriteAllText(VisualOverhaulRepair.Report+"/deformation-"+(string.IsNullOrEmpty(phase)?"before":phase)+".txt",log.ToString());return log.ToString();
        }
        public static string WeaponNumbers()
        {
            var log=new StringBuilder();foreach(string path in new[]{VisualOverhaulWeapons.Cannon,VisualOverhaulWeapons.Shotgun})
            {
                var root=PrefabUtility.LoadPrefabContents(path);try{var w=root.GetComponent<WeaponController>();var attachment=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Attachment");var module=root.GetComponent<ArmModuleVisual>().ModuleRoot;log.AppendLine(path+" portRoot="+root.transform.InverseTransformPoint(attachment.position).ToString("F6")+" muzzleRoot="+root.transform.InverseTransformPoint(w.WeaponMuzzle.position).ToString("F6")+" bodyBounds="+module.Find("VO_Body").GetComponent<MeshFilter>().sharedMesh.bounds+" moduleWorldScale="+module.lossyScale+" rootScale="+root.transform.lossyScale);var cells=root.GetComponent<WeaponFuelCellHandler>();if(cells)foreach(var cell in cells.FuelCells)if(cell)log.AppendLine("cell="+root.transform.InverseTransformPoint(cell.transform.position).ToString("F6"));}finally{PrefabUtility.UnloadPrefabContents(root);}
            }File.WriteAllText(VisualOverhaulRepair.Report+"/weapon-numbers.txt",log.ToString());return log.ToString();
        }
        public static string Mechanics(string phase)
        {
            var log=new StringBuilder();foreach(string path in new[]{VisualOverhaulWeapons.Cannon,VisualOverhaulWeapons.Shotgun})
            {
                var root=PrefabUtility.LoadPrefabContents(path);try
                {
                    foreach(var component in root.GetComponents<MonoBehaviour>().Where(c=>c&&!(c is ArmModuleVisual)))
                    {
                        var data=new SerializedObject(component);var p=data.GetIterator();while(p.NextVisible(true))
                        {
                            if(p.propertyType==SerializedPropertyType.Float)log.AppendLine(path+" "+component.GetType().Name+" "+p.propertyPath+"="+p.floatValue.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                            else if(p.propertyType==SerializedPropertyType.Integer||p.propertyType==SerializedPropertyType.Enum||p.propertyType==SerializedPropertyType.Boolean)log.AppendLine(path+" "+component.GetType().Name+" "+p.propertyPath+"="+(p.propertyType==SerializedPropertyType.Boolean?p.boolValue.ToString():p.intValue.ToString()));
                            else if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue&&AssetDatabase.Contains(p.objectReferenceValue))log.AppendLine(path+" "+component.GetType().Name+" "+p.propertyPath+"="+AssetDatabase.GetAssetPath(p.objectReferenceValue));
                        }
                    }
                    var w=root.GetComponent<WeaponController>();log.AppendLine("MUZZLE "+root.transform.InverseTransformPoint(w.WeaponMuzzle.position).ToString("F9")+" "+w.WeaponMuzzle.localRotation.ToString("F9"));
                }finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            File.WriteAllText(VisualOverhaulRepair.Report+"/mechanics-"+phase+".txt",log.ToString());return "Mechanics snapshot "+phase;
        }
        // Neutral matched preview. It never moves or saves user objects; bloom is deliberately absent.
        public static string Capture(string phase)
        {
            Directory.CreateDirectory(VisualOverhaulRepair.Report+"/Shots");foreach(string path in Robots.Select(n=>"Assets/IronRequiem/Prefabs/Campaign/"+n+".prefab").Concat(new[]{VisualOverhaulWeapons.Cannon,VisualOverhaulWeapons.Shotgun}))
            {
                var scene=EditorSceneManager.NewPreviewScene();try
                {
                    var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);var animator=root.GetComponentInChildren<Animator>(true);if(animator&&animator.runtimeAnimatorController){var idle=animator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name.IndexOf("idle",StringComparison.OrdinalIgnoreCase)>=0);if(idle)idle.SampleAnimation(animator.gameObject,0);}
                    foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);
                    var renderers=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&!(r is ParticleSystemRenderer)&&!(r is TrailRenderer)&&!(r is LineRenderer)&&r.bounds.size.magnitude<15).ToArray();if(renderers.Length==0)throw new Exception("No visible geometry "+path);var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                    var cameraGo=new GameObject("RepairCamera");SceneManager.MoveGameObjectToScene(cameraGo,scene);var camera=cameraGo.AddComponent<Camera>();camera.scene=scene;camera.nearClipPlane=.01f;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.11f,.14f,.18f);camera.fieldOfView=40;
                    bool weapon=root.GetComponent<WeaponController>();var direction=weapon?new Vector3(1,.45f,-1.4f):new Vector3(.2f,.08f,1.8f);camera.transform.position=bounds.center+direction.normalized*(bounds.size.magnitude*1.45f);camera.transform.LookAt(bounds.center);
                    foreach(var setting in new[]{(new Vector3(45,-25,0),1.5f),(new Vector3(20,145,0),.8f)}){var go=new GameObject("RepairLight");SceneManager.MoveGameObjectToScene(go,scene);var light=go.AddComponent<Light>();light.type=LightType.Directional;light.intensity=setting.Item2;light.color=new Color(.8f,.88f,1);light.transform.rotation=Quaternion.Euler(setting.Item1);}
                    var rt=new RenderTexture(960,720,24);var texture=new Texture2D(960,720,TextureFormat.RGB24,false);var previous=RenderTexture.active;try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,960,720),0,0);texture.Apply();File.WriteAllBytes(VisualOverhaulRepair.Report+"/Shots/"+phase+"-"+Path.GetFileNameWithoutExtension(path)+".png",texture.EncodeToPNG());}finally{camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
                }finally{EditorSceneManager.ClosePreviewScene(scene);}
            }return "Matched neutral previews saved (no bloom).";
        }
    }
}

