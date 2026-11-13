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
using K=IronRequiem.Editor.VisualOverhaulAssets;

namespace IronRequiem.Editor
{
    public static class VisualOverhaulRepairApply
    {
        const string Art=VisualOverhaulRepair.Art,Report=VisualOverhaulRepair.Report;
        static readonly StringBuilder log=new StringBuilder();
        public static void Guard()
        {
            var stage=PrefabStageUtility.GetCurrentPrefabStage();
            if(EditorApplication.isPlayingOrWillChangePlaymode||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty)||(stage&&stage.scene.isDirty))throw new Exception("Preserve unsaved user scene/prefab stage.");
            Directory.CreateDirectory(Art);Directory.CreateDirectory(Report);
        }
        public static Mesh SaveMesh(Mesh mesh,string name)
        {
            string path=Art+"/"+name+".asset";mesh.name=name;var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Mesh SaveKit(K.MeshKit k,string name)
        {
            var mesh=new Mesh{name=name,indexFormat=k.V.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(k.V);mesh.SetNormals(k.N);mesh.SetUVs(0,k.UV);mesh.subMeshCount=k.Sub.Count;
            for(int i=0;i<k.Sub.Count;i++)mesh.SetTriangles(k.Sub[i],i);mesh.RecalculateBounds();mesh.RecalculateTangents();return SaveMesh(mesh,name);
        }
        static void Face(K.MeshKit k,int sub,Vector3[] corners,Vector2[] uv=null)
        {
            int b=k.V.Count;var normal=Vector3.Cross(corners[1]-corners[0],corners[2]-corners[0]).normalized;
            for(int i=0;i<corners.Length;i++){k.V.Add(corners[i]);k.N.Add(normal);k.UV.Add(uv!=null?uv[i]:new Vector2(.015f,.015f));}
            for(int i=1;i<corners.Length-1;i++)k.Sub[sub].AddRange(new[]{b,b+i,b+i+1});
        }
        // A bevelled rectangular cross-section, with caps; dimensions are in weapon space.
        static void Receiver(K.MeshKit k,Vector3 center,Vector3 size,int sub,float bevel)
        {
            float x=size.x*.5f,y=size.y*.5f,z=size.z*.5f,b=Mathf.Min(bevel,Mathf.Min(x,y)*.5f);
            var ring=new[]{new Vector2(-x+b,-y),new Vector2(x-b,-y),new Vector2(x,-y+b),new Vector2(x,y-b),new Vector2(x-b,y),new Vector2(-x+b,y),new Vector2(-x,y-b),new Vector2(-x,-y+b)};
            var front=ring.Select(p=>center+new Vector3(p.x,p.y,z)).ToArray();var back=ring.Select(p=>center+new Vector3(p.x,p.y,-z)).ToArray();Face(k,sub,front);Face(k,sub,back.Reverse().ToArray());
            for(int i=0;i<8;i++){int j=(i+1)%8;Face(k,sub,new[]{back[i],back[j],front[j],front[i]},new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,size.z*4),new Vector2(0,size.z*4)});}
        }
        static Material LensMaterial()
        {
            string texturePath=Art+"/IndicatorMask.png";if(!File.Exists(texturePath))
            {
                var texture=new Texture2D(32,32,TextureFormat.RGB24,false);var pixels=new Color[1024];
                for(int y=0;y<32;y++)for(int x=0;x<32;x++){bool edge=x<4||x>27||y<4||y>27;pixels[y*32+x]=edge?new Color(.025f,.03f,.035f):new Color(.7f,.75f,.8f);}
                texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(texturePath,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(texturePath);
            }
            string path=Art+"/SharedIndicatorLens.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            var mask=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);material.SetTexture("_BaseMap",mask);material.SetTexture("_EmissionMap",mask);material.SetColor("_BaseColor",Color.white);material.SetColor("_EmissionColor",new Color(.15f,2.3f,2.8f));material.SetFloat("_Metallic",.1f);material.SetFloat("_Smoothness",.3f);material.EnableKeyword("_EMISSION");EditorUtility.SetDirty(material);return material;
        }
        static Mesh Panel()
        {
            var k=new K.MeshKit(1);Receiver(k,Vector3.zero,Vector3.one,0,.10f);
            // Only the forward face is luminous; side/back UVs sample the dark frame.
            for(int i=0;i<8;i++){var p=k.V[i];k.UV[i]=new Vector2(p.x+.5f,p.y+.5f);}
            return SaveKit(k,"FinishedIndicatorPanel");
        }
        static void FinishRenderer(Renderer r,Mesh mesh,Material material)
        {
            r.GetComponent<MeshFilter>().sharedMesh=mesh;r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            foreach(var collider in r.GetComponents<Collider>())Object.DestroyImmediate(collider);
        }
        static void BindLights(GameObject root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("PowerIndicator")||r.name=="Sphere").ToArray();
            var power=root.GetComponent<EnemyPowerIndicators>()??root.AddComponent<EnemyPowerIndicators>();power.Configure(renderers);
            var state=root.GetComponent<EnemyStateLights>()??root.AddComponent<EnemyStateLights>();state.Configure(renderers);
            var data=new SerializedObject(state);data.FindProperty("patrol").colorValue=new Color(.15f,2.3f,2.8f);data.FindProperty("alert").colorValue=new Color(2.2f,1.1f,.08f);data.FindProperty("attack").colorValue=new Color(2.8f,.12f,.05f);data.FindProperty("warning").colorValue=new Color(3,1.8f,1.2f);data.ApplyModifiedPropertiesWithoutUndo();
        }
        public static string Lights()
        {
            Guard();log.Clear();var material=LensMaterial();var panel=Panel();
            foreach(string path in new[]{"Assets/IronRequiem/Prefabs/Enemies/BigBot_ReferenceCharge_v09.prefab","Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab"})
            {
                var root=PrefabUtility.LoadPrefabContents(path);try
                {
                    var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.name.EndsWith("LOD0")).ToArray();
                    foreach(var view in root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("PowerIndicator")).ToArray())
                    {
                        var t=view.transform;var position=t.localPosition;var rotation=t.localRotation;var scale=t.localScale;var world=t.position;
                        if(t.parent==root.transform)
                        {
                            var nearest=skins.OrderBy(s=>(s.bounds.ClosestPoint(world)-world).sqrMagnitude).First();string boneName=nearest.name.Replace("_LOD0","");
                            var bone=nearest.bones.FirstOrDefault(b=>b.name==boneName);if(!bone)throw new Exception("No animated part for marker "+nearest.name);
                            // Rest parent frame is carried by the real part, so marker local values stay EXACTLY unchanged.
                            var anchor=new GameObject("IndicatorCarrier_"+view.name).transform;anchor.SetParent(bone,false);anchor.SetPositionAndRotation(root.transform.position,root.transform.rotation);anchor.localScale=Vector3.Scale(anchor.localScale,new Vector3(root.transform.lossyScale.x/anchor.lossyScale.x,root.transform.lossyScale.y/anchor.lossyScale.y,root.transform.lossyScale.z/anchor.lossyScale.z));t.SetParent(anchor,false);
                            t.localPosition=position;t.localRotation=rotation;t.localScale=scale;log.AppendLine(path+" "+view.name+" follows "+bone.name);
                        }
                        FinishRenderer(view,panel,material);
                        if((t.position-world).sqrMagnitude>1e-9f||t.localPosition!=position||t.localRotation!=rotation||t.localScale!=scale)throw new Exception("Marker transform changed "+view.name);
                    }
                    BindLights(root);PrefabUtility.SaveAsPrefabAsset(root,path);
                }finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            string source="Assets/IronRequiem/Prefabs/Enemies/T07_LegRepair_Charged_v05.prefab";Vector3 eyePosition,eyeScale;Quaternion eyeRotation;
            var original=PrefabUtility.LoadPrefabContents(source);try
            {
                var eye=original.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Sphere");eyePosition=eye.localPosition;eyeRotation=eye.localRotation;eyeScale=eye.localScale;
                var mesh=Object.Instantiate(eye.GetComponent<MeshFilter>().sharedMesh);float worldScale=eye.lossyScale.x;var vertices=mesh.vertices;float diameter=mesh.bounds.size.x;for(int i=0;i<vertices.Length;i++)vertices[i]*=.065f/(worldScale*diameter);mesh.vertices=vertices;mesh.RecalculateBounds();var fixedEye=SaveMesh(mesh,"T07_EyeLens_Normalized");
                FinishRenderer(eye.GetComponent<Renderer>(),fixedEye,material);
                foreach(var r in original.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("PowerIndicator")))FinishRenderer(r,panel,material);
                BindLights(original);PrefabUtility.SaveAsPrefabAsset(original,source);
                string target="Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab";var current=PrefabUtility.LoadPrefabContents(target);try
                {
                    string headPath=AnimationUtility.CalculateTransformPath(eye.parent,original.transform);var head=current.transform.Find(headPath);if(!head)throw new Exception("T07 head hierarchy mismatch");var targetEye=head.Find("Sphere");
                    if(!targetEye){targetEye=new GameObject("Sphere",typeof(MeshFilter),typeof(MeshRenderer)).transform;targetEye.SetParent(head,false);}
                    targetEye.localPosition=eyePosition;targetEye.localRotation=eyeRotation;targetEye.localScale=eyeScale;FinishRenderer(targetEye.GetComponent<Renderer>(),fixedEye,material);
                    foreach(var r in current.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("PowerIndicator")))FinishRenderer(r,panel,material);
                    BindLights(current);PrefabUtility.SaveAsPrefabAsset(current,target);log.AppendLine("T07 user eye exact local transform copied to matching Head. Diameter0.065m, original100m; marker collider removed. No added Light.");
                }finally{PrefabUtility.UnloadPrefabContents(current);}
            }finally{PrefabUtility.UnloadPrefabContents(original);}
            AssetDatabase.SaveAssets();File.WriteAllText(Report+"/lights-applied.txt",log.ToString());return log.ToString();
        }
        public static string Shotgun()
        {
            Guard();string path=VisualOverhaulWeapons.Shotgun;var root=PrefabUtility.LoadPrefabContents(path);try
            {
                var weapon=root.GetComponent<WeaponController>();var module=root.GetComponent<ArmModuleVisual>().ModuleRoot;var body=module.Find("VO_Body");
                var port=root.transform.InverseTransformPoint(root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Attachment").position);var muzzle=root.transform.InverseTransformPoint(weapon.WeaponMuzzle.position);
                var k=new K.MeshKit(3);float y=muzzle.y,z0=port.z,z1=muzzle.z,receiverEnd=.22f;
                k.Lathe(Matrix4x4.Translate(port),new[]{new Vector2(.044f,0),new Vector2(.052f,.008f),new Vector2(.052f,.035f),new Vector2(.045f,.05f)},20,1,4,true);
                Receiver(k,new Vector3(0,.04f,(z0+.04f+receiverEnd)*.5f),new Vector3(.112f,.15f,receiverEnd-z0-.04f),0,.018f);
                Receiver(k,new Vector3(0,.022f,.20f),new Vector3(.118f,.105f,.048f),2,.018f);
                // One dominant bore and one shorter, capped magazine tube: an unmistakable shotgun profile.
                k.Lathe(Matrix4x4.Translate(new Vector3(muzzle.x,y,receiverEnd)),new[]{new Vector2(.031f,0),new Vector2(.028f,.035f),new Vector2(.028f,z1-receiverEnd-.045f),new Vector2(.034f,z1-receiverEnd-.035f),new Vector2(.034f,z1-receiverEnd),new Vector2(.020f,z1-receiverEnd),new Vector2(.020f,z1-receiverEnd-.045f)},20,1,4);
                k.Lathe(Matrix4x4.Translate(new Vector3(0,y-.065f,.19f)),new[]{new Vector2(.020f,0),new Vector2(.020f,.50f),new Vector2(.023f,.51f),new Vector2(.023f,.54f)},16,1,4,true,true);
                Receiver(k,new Vector3(0,y-.040f,.47f),new Vector3(.083f,.075f,.23f),0,.016f);
                for(int i=0;i<5;i++)Receiver(k,new Vector3(0,y-.043f,.385f+i*.042f),new Vector3(.088f,.079f,.012f),1,.014f);
                Receiver(k,new Vector3(0,y+.041f,.59f),new Vector3(.014f,.012f,.72f),1,.004f);
                Receiver(k,new Vector3(0,y+.052f,z1-.045f),new Vector3(.011f,.023f,.014f),2,.003f);
                // Recessed ejection port, safety stripe and underside mechanical grip; no floating pistol handle.
                Receiver(k,new Vector3(-.057f,.061f,.10f),new Vector3(.004f,.034f,.074f),1,.001f);
                Receiver(k,new Vector3(0,-.042f,.065f),new Vector3(.065f,.025f,.18f),1,.007f);
                var convert=body.worldToLocalMatrix*root.transform.localToWorldMatrix;var normal=convert.inverse.transpose;
                for(int i=0;i<k.V.Count;i++){k.V[i]=convert.MultiplyPoint3x4(k.V[i]);k.N[i]=normal.MultiplyVector(k.N[i]).normalized;}
                body.GetComponent<MeshFilter>().sharedMesh=SaveKit(k,"Shotgun_SingleBore_Body");
                // Existing capsule pivots, reload, switch, muzzle, projectile and all weapon stats are retained.
                foreach(var light in module.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("VO_Light_"))){var p=light.transform.localPosition;p.x=-.0585f;light.transform.localPosition=p;}
                PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();return "Single bore at original muzzle "+muzzle.ToString("F6")+"; capped magazine, bevelled receiver/pump; four original heat capsules and all mechanisms retained.";
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static string Campaign()
        {
            Guard();var setup=EditorSceneManager.GetSceneManagerSetup();log.Clear();try
            {
                foreach(string path in new[]{CampaignAuthoring.Main}.Concat(CampaignAuthoring.Names.Select(n=>CampaignAuthoring.Folder+"/"+n+".unity")))
                {
                    var scene=EditorSceneManager.OpenScene(path);if(scene.isDirty)throw new Exception("Scene import produced unsaved state "+path);
                    if(path==CampaignAuthoring.Main)
                    {
                        var player=Object.FindAnyObjectByType<PlayerWeaponsManager>();var retained=player.StartingWeapons.Where(w=>w&&AssetDatabase.GetAssetPath(w)!=VisualOverhaulWeapons.CannonSource&&AssetDatabase.GetAssetPath(w)!=VisualOverhaulWeapons.ShotgunSource&&AssetDatabase.GetAssetPath(w)!=VisualOverhaulWeapons.Cannon&&AssetDatabase.GetAssetPath(w)!=VisualOverhaulWeapons.Shotgun).ToList();
                        player.StartingWeapons.Clear();player.StartingWeapons.Add(AssetDatabase.LoadAssetAtPath<WeaponController>(VisualOverhaulWeapons.Cannon));player.StartingWeapons.Add(AssetDatabase.LoadAssetAtPath<WeaponController>(VisualOverhaulWeapons.Shotgun));player.StartingWeapons.AddRange(retained);PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                        foreach(var volume in Object.FindObjectsByType<Volume>().Where(v=>v.isGlobal&&v.sharedProfile))
                        {
                            string profile=Art+"/Campaign_Readability.asset";if(!File.Exists(profile))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(volume.sharedProfile),profile);var copy=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profile);volume.sharedProfile=copy;
                            var grading=copy.TryGet<ColorAdjustments>(out var c)?c:copy.Add<ColorAdjustments>();grading.postExposure.Override(.30f);grading.contrast.Override(3f);
                            if(copy.TryGet<Bloom>(out var bloom)){bloom.intensity.Override(.08f);bloom.threshold.Override(1.1f);}
                            if(copy.TryGet<FilmGrain>(out var grain))grain.active=false;
                            EditorUtility.SetDirty(copy);
                        }
                    }
                    // Lift shadow detail, keep the same mounted fixtures and shadow count. No extra lights or Volumes.
                    RenderSettings.ambientLight=new Color(.55f,.60f,.65f);RenderSettings.reflectionIntensity=1.1f;
                    log.AppendLine(path+" ambient=.55,.60,.65 reflection=1.1 lights="+Object.FindObjectsByType<Light>().Length);
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
            }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
            AssetDatabase.SaveAssets();File.WriteAllText(Report+"/campaign-applied.txt",log.ToString());return log.ToString();
        }
    }
}
