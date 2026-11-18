using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IronRequiem.EditorTools
{
    public static class PlayerVisualRepairAuthoring
    {
        const string Campaign="Assets/IronRequiem/Scenes/RequiemCampaign.unity";
        const string Folder="Assets/IronRequiem/Art/Player/HandPoses";
        [Serializable] sealed class Geometry { public float[] vertices; public int[] triangles,materials; }

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Apply cinematic poses")]
        public static void CinematicPoses()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserving dirty scene.");
            const string path="Assets/IronRequiem/Scenes/Campaign/Room00_Hangar.unity";
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var pose=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WakeUpRobotPose>(true)).Single();
                var nodes=pose.Model.GetComponentsInChildren<Transform>(true);
                var left=nodes.Single(t=>t.name=="mixamorig:LeftHand");var right=nodes.Single(t=>t.name=="mixamorig:RightHand");
                var skin=pose.Model.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var log=new System.Text.StringBuilder("Cinematic absolute bind poses\n");
                var leftPoses=HandPoseAuthoring.BakeRig(left,skin,true,"Cinematic_",log);var rightPoses=HandPoseAuthoring.BakeRig(right,skin,false,"Cinematic_",log);
                var controller=pose.Model.GetComponent<HandPoseController>();if(!controller)controller=Undo.AddComponent<HandPoseController>(pose.Model.gameObject);
                controller.Configure(left,leftPoses,right,rightPoses,null,true);pose.ConfigureHandPoses(controller);
                EditorUtility.SetDirty(controller);EditorUtility.SetDirty(pose);PrefabUtility.RecordPrefabInstancePropertyModifications(pose);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                File.WriteAllText("Tools/IronRequiem/Reports/HandsHudCampaign/cinematic-pose-bake.txt",log.ToString());
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }

        [MenuItem("Tools/Iron Requiem/Hands HUD Campaign/Apply rigid arms and HUD")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserving dirty scene.");
            var scene=SceneManager.GetSceneByPath(Campaign);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(Campaign,OpenSceneMode.Additive);
            try
            {
                var roots=scene.GetRootGameObjects();var hands=roots.SelectMany(r=>r.GetComponentsInChildren<ProtagonistHandsPresentation>(true)).Single();
                var player=hands.GetComponent<Unity.FPS.Gameplay.PlayerCharacterController>();
                var controller=hands.GetComponent<HandPoseController>();if(!controller)throw new InvalidOperationException("Apply hand poses first.");
                var skins=hands.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var leftOpen=skins.Single(s=>s.name=="Hand_L_FPS");var rightOpen=skins.Single(s=>s.name=="Hand_R_FPS");
                var leftClosed=Closed(hands.LeftHandBone,true,leftOpen.gameObject.layer);
                var rightClosed=Closed(hands.RightHand,false,rightOpen.gameObject.layer);
                leftOpen.sharedMaterials=leftOpen.sharedMaterials.Select((m,i)=>TransparentCopy(m,"Open_L_"+i)).ToArray();
                rightOpen.sharedMaterials=rightOpen.sharedMaterials.Select((m,i)=>TransparentCopy(m,"Open_R_"+i)).ToArray();
                controller.ConfigureClosedVisuals(leftOpen,leftClosed,rightOpen,rightClosed);EditorUtility.SetDirty(controller);
                var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Art/Protagonist/CapsuleRobotRig/CapsuleRobotRig.fbx");
                var skin=model.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var left=UpperArm(hands,skin,true,out float leftLength);var right=UpperArm(hands,skin,false,out float rightLength);
                var joints=new Transform[2];
                for(int i=0;i<2;i++)
                {
                    string name=i==0?"CapsuleElbow_L":"CapsuleElbow_R";var existing=hands.FpsRoot.Find(name);
                    if(existing)joints[i]=existing;
                    else
                    {
                        var joint=GameObject.CreatePrimitive(PrimitiveType.Sphere);joint.name=name;UnityEngine.Object.DestroyImmediate(joint.GetComponent<Collider>());
                        joint.transform.SetParent(hands.FpsRoot,false);joint.transform.localScale=Vector3.one*.093f;joint.layer=leftOpen.gameObject.layer;
                        joint.GetComponent<Renderer>().sharedMaterial=Material("ElbowGraphite",new Color(.13f,.15f,.16f),false);joints[i]=joint.transform;
                    }
                }
                var arms=hands.GetComponent<FirstPersonUpperArms>();if(!arms)arms=Undo.AddComponent<FirstPersonUpperArms>(hands.gameObject);
                arms.Configure(hands,player.PlayerCamera,left,right,joints[0],joints[1],leftLength,rightLength);EditorUtility.SetDirty(arms);
                var mechanic=roots.SelectMany(r=>r.GetComponentsInChildren<MechanicHud>(true)).FirstOrDefault();
                var canvas=mechanic?mechanic.GetComponentInParent<Canvas>(true):roots.SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).FirstOrDefault(c=>c.name=="HUD"&&c.gameObject.activeInHierarchy);
                if(!canvas)throw new InvalidOperationException("Existing gameplay HUD canvas not found.");
                var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                if(!scaler)scaler=canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
                EditorUtility.SetDirty(scaler);PrefabUtility.RecordPrefabInstancePropertyModifications(scaler);
                if(!mechanic)
                {
                    // Restore the existing prompt presenter when this campaign variant omitted it; reuse its implementation.
                    var root=new GameObject("MechanicsHUD",typeof(RectTransform));root.transform.SetParent(canvas.transform,false);
                    var rootRect=(RectTransform)root.transform;rootRect.anchorMin=Vector2.zero;rootRect.anchorMax=Vector2.one;rootRect.offsetMin=rootRect.offsetMax=Vector2.zero;
                    mechanic=root.AddComponent<MechanicHud>();
                    TMP_Text Text(string name){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(root.transform,false);var t=go.AddComponent<TextMeshProUGUI>();t.font=TMP_Settings.defaultFontAsset;t.raycastTarget=false;return t;}
                    mechanic.Configure(player.GetComponent<AdvancedPlayerMovement>(),player.GetComponent<StolenWeaponCarrier>(),Text("MechanicStatus"),Text("InteractionPrompt"));
                }
                if(mechanic.transform!=canvas.transform)
                {
                    var mechanicsRect=mechanic.GetComponent<RectTransform>();mechanicsRect.anchorMin=Vector2.zero;mechanicsRect.anchorMax=Vector2.one;mechanicsRect.offsetMin=mechanicsRect.offsetMax=Vector2.zero;
                }
                var bar=canvas.transform.Find("ClassicStatusBar");
                if(!bar)
                {
                    var go=new GameObject("ClassicStatusBar",typeof(RectTransform));bar=go.transform;bar.SetParent(canvas.transform,false);
                    // 16/11 column layout: the bar object spans the HUD; RetroStatusBar builds its columns and avatar at runtime
                    var rect=(RectTransform)bar;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,.5f);
                    rect.offsetMin=rect.offsetMax=Vector2.zero;
                }
                var hud=bar.GetComponent<RetroStatusBar>();if(!hud)hud=Undo.AddComponent<RetroStatusBar>(bar.gameObject);
                bar.gameObject.layer=canvas.gameObject.layer;
                var text=mechanic.GetComponentInChildren<TMP_Text>(true);hud.Configure(player,text?text.font:TMP_Settings.defaultFontAsset);EditorUtility.SetDirty(hud);
                foreach(var oldHealth in roots.SelectMany(r=>r.GetComponentsInChildren<Unity.FPS.UI.PlayerHealthBar>(true)))
                {
                    // PlayerHealthBar lives on the GameHUD controller root, not on the health panel. Keep that root alive.
                    if(bar.IsChildOf(oldHealth.transform))oldHealth.gameObject.SetActive(true);
                    oldHealth.enabled=false;EditorUtility.SetDirty(oldHealth);PrefabUtility.RecordPrefabInstancePropertyModifications(oldHealth);
                    if(!oldHealth.HealthFillImage)continue;
                    var ownerCanvas=oldHealth.HealthFillImage.GetComponentInParent<Canvas>(true);
                    var group=oldHealth.HealthFillImage.transform;
                    while(group.parent&&ownerCanvas&&group.parent!=ownerCanvas.transform)group=group.parent;
                    if(ownerCanvas&&group!=ownerCanvas.transform&&!bar.IsChildOf(group))group.gameObject.SetActive(false);
                }
                foreach(var oldHeat in roots.SelectMany(r=>r.GetComponentsInChildren<WeaponHeatHud>(true)))
                {
                    oldHeat.enabled=false;EditorUtility.SetDirty(oldHeat);PrefabUtility.RecordPrefabInstancePropertyModifications(oldHeat);
                    var content=new SerializedObject(oldHeat).FindProperty("content").objectReferenceValue as GameObject;
                    if(content&&!bar.IsChildOf(content.transform))content.SetActive(false);
                    if(!bar.IsChildOf(oldHeat.transform)&&!oldHeat.GetComponent<Canvas>())oldHeat.gameObject.SetActive(false);
                }
                var settings=new SerializedObject(mechanic);
                mechanic.ConfigureCompactStatus(true);EditorUtility.SetDirty(mechanic);
                // same rects RetroStatusBar applies at runtime: controls beside the ARMAS column, prompt above the avatar box
                PlaceText(settings.FindProperty("status").objectReferenceValue as TMP_Text,new Vector2(0,0),RetroStatusBar.StatusPosition,RetroStatusBar.StatusSize,18,TextAlignmentOptions.BottomLeft);
                PlaceText(settings.FindProperty("prompt").objectReferenceValue as TMP_Text,new Vector2(.5f,0),RetroStatusBar.PromptPosition,RetroStatusBar.PromptSize,24,TextAlignmentOptions.Center);
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CampaignFlow>(true)).Single();
                flow.ConfigureShaderWarmup(BuildShaderWarmup());EditorUtility.SetDirty(flow);PrefabUtility.RecordPrefabInstancePropertyModifications(flow);
                PrefabUtility.RecordPrefabInstancePropertyModifications(hands);PrefabUtility.RecordPrefabInstancePropertyModifications(leftOpen);PrefabUtility.RecordPrefabInstancePropertyModifications(rightOpen);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        static void PlaceText(TMP_Text text,Vector2 anchor,Vector2 position,Vector2 size,float fontSize,TextAlignmentOptions alignment)
        {
            if(!text)return;var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(anchor.x,0);rect.anchoredPosition=position;rect.sizeDelta=size;
            text.fontSize=fontSize;text.alignment=alignment;text.color=new Color(.8f,.84f,.84f);EditorUtility.SetDirty(text);
        }
        static Renderer Closed(Transform hand,bool left,int layer)
        {
            var geometry=JsonUtility.FromJson<Geometry>(File.ReadAllText("Tools/IronRequiem/Exports/PlayerRigidFist/closed_fist.json"));
            var positions=new Vector3[geometry.vertices.Length/3];
            for(int i=0;i<positions.Length;i++)positions[i]=new Vector3(geometry.vertices[i*3]*(left?-1:1),geometry.vertices[i*3+1],geometry.vertices[i*3+2]);
            var mesh=new Mesh{name="RigidFist_"+(left?"L":"R")};mesh.vertices=positions;mesh.subMeshCount=3;
            for(int m=0;m<3;m++)
            {
                var triangles=new List<int>();for(int t=0;t<geometry.materials.Length;t++)if(geometry.materials[t]==m)
                {
                    int i=t*3;triangles.Add(geometry.triangles[i]);triangles.Add(geometry.triangles[i+(left?2:1)]);triangles.Add(geometry.triangles[i+(left?1:2)]);
                }
                mesh.SetTriangles(triangles,m);
            }
            mesh.RecalculateNormals();mesh.RecalculateBounds();mesh=SaveMesh(mesh,Folder+"/"+mesh.name+".asset");
            var node=hand.Find("RigidClosedFist");if(!node){var go=new GameObject("RigidClosedFist",typeof(MeshFilter),typeof(MeshRenderer));node=go.transform;node.SetParent(hand,false);}
            var library=AssetDatabase.LoadAssetAtPath<HandPoseLibrary>(Folder+"/FPS_"+(left?"L":"R")+".asset");
            node.localRotation=Quaternion.LookRotation(library.FingerDirection,library.PalmNormal)*Quaternion.Inverse(Quaternion.LookRotation(Vector3.up,Vector3.forward));
            node.localPosition=Vector3.zero;node.localScale=Vector3.one;node.gameObject.layer=layer;node.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=node.GetComponent<MeshRenderer>();renderer.sharedMaterials=new[]{Material("FistGraphite",new Color(.065f,.075f,.085f),true),Material("FistSteel",new Color(.43f,.46f,.47f),true),Material("FistOrange",new Color(.82f,.19f,.025f),true)};
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.enabled=false;return renderer;
        }
        static Renderer UpperArm(ProtagonistHandsPresentation hands,SkinnedMeshRenderer source,bool left,out float length)
        {
            string side=left?"Left":"Right";const float scale=2.2f;var bones=source.bones;
            int arm=Array.FindIndex(bones,b=>b&&b.name.EndsWith(":"+side+"Arm",StringComparison.Ordinal));
            int shoulder=Array.FindIndex(bones,b=>b&&b.name.EndsWith(":"+side+"Shoulder",StringComparison.Ordinal));
            int forearm=Array.FindIndex(bones,b=>b&&b.name.EndsWith(":"+side+"ForeArm",StringComparison.Ordinal));
            if(arm<0||shoulder<0||forearm<0)throw new InvalidOperationException("Capsule arm bones missing.");
            length=Vector3.Distance(bones[arm].position,bones[forearm].position)*scale;
            var sourceMesh=source.sharedMesh;var weights=sourceMesh.boneWeights;var verts=sourceMesh.vertices;var uv=sourceMesh.uv;var bind=sourceMesh.bindposes[arm];
            int[] dominant=new int[weights.Length];
            for(int i=0;i<weights.Length;i++)
            {
                var w=weights[i];float max=w.weight0;int index=w.boneIndex0;
                if(w.weight1>max){max=w.weight1;index=w.boneIndex1;}if(w.weight2>max){max=w.weight2;index=w.boneIndex2;}if(w.weight3>max)index=w.boneIndex3;dominant[i]=index;
            }
            var p=new List<Vector3>();var u=new List<Vector2>();var groups=new List<int>[sourceMesh.subMeshCount];
            for(int m=0;m<groups.Length;m++)
            {
                groups[m]=new List<int>();var triangles=sourceMesh.GetTriangles(m);
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int votes=0;for(int j=0;j<3;j++){int b=dominant[triangles[i+j]];if(b==arm||b==shoulder)votes++;}if(votes<2)continue;
                    for(int j=0;j<3;j++){int v=triangles[i+j];groups[m].Add(p.Count);p.Add(bind.MultiplyPoint3x4(verts[v])*scale);u.Add(uv.Length>v?uv[v]:Vector2.zero);}
                }
            }
            var mesh=new Mesh{name="CapsuleUpperArm_"+side};mesh.SetVertices(p);mesh.SetUVs(0,u);mesh.subMeshCount=groups.Length;
            for(int m=0;m<groups.Length;m++)mesh.SetTriangles(groups[m],m);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();mesh=SaveMesh(mesh,Folder+"/"+mesh.name+".asset");
            string name="CapsuleUpperArm_"+(left?"L":"R");var node=hands.FpsRoot.Find(name);
            if(!node){var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));node=go.transform;node.SetParent(hands.FpsRoot,false);}
            node.localScale=Vector3.one;node.gameObject.layer=hands.RightHandRenderers[0].gameObject.layer;node.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=node.GetComponent<MeshRenderer>();
            // Reuse the existing rig's textured URP materials, rather than Unity's imported Standard materials.
            var armMaterial=Material("CapsuleArmMetal",Color.white,false);
            const string textures="Assets/IronRequiem/Art/Protagonist/CapsuleRobotRig/";
            armMaterial.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(textures+"CapsuleRobotRig_BaseColor.png"));
            armMaterial.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(textures+"CapsuleRobotRig_Normal.png"));
            armMaterial.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(textures+"CapsuleRobotRig_MetalSmooth.png"));
            armMaterial.EnableKeyword("_NORMALMAP");armMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");EditorUtility.SetDirty(armMaterial);AssetDatabase.SaveAssetIfDirty(armMaterial);
            renderer.sharedMaterials=Enumerable.Repeat(armMaterial,sourceMesh.subMeshCount).ToArray();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;
        }
        static Mesh SaveMesh(Mesh mesh,string path)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);return existing;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Material Material(string name,Color color,bool transparent)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",.8f);material.SetFloat("_Smoothness",.35f);
            if(transparent){MakeTransparent(material);material.SetFloat("_ZWrite",1);}EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);return material;
        }
        static Material TransparentCopy(Material source,string name)
        {
            var path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(source);AssetDatabase.CreateAsset(material,path);}MakeTransparent(material);EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);return material;
        }
        static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");material.renderQueue=(int)RenderQueue.Transparent;
        }

        static ShaderVariantCollection BuildShaderWarmup()
        {
            string path=Folder+"/PlayerStartupVariants.shadervariants";
            var variants=AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
            if(!variants){variants=new ShaderVariantCollection();AssetDatabase.CreateAsset(variants,path);}variants.Clear();
            foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Folder}))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));if(!material||!material.shader)continue;
                var keywords=material.shaderKeywords;
                variants.Add(new ShaderVariantCollection.ShaderVariant(material.shader,PassType.ScriptableRenderPipeline,keywords));
                variants.Add(new ShaderVariantCollection.ShaderVariant(material.shader,PassType.ScriptableRenderPipeline,keywords.Concat(new[]{"_MAIN_LIGHT_SHADOWS"}).Distinct().ToArray()));
                variants.Add(new ShaderVariantCollection.ShaderVariant(material.shader,PassType.ScriptableRenderPipeline,keywords.Concat(new[]{"_MAIN_LIGHT_SHADOWS_CASCADE","_SHADOWS_SOFT"}).Distinct().ToArray()));
            }
            EditorUtility.SetDirty(variants);AssetDatabase.SaveAssetIfDirty(variants);return variants;
        }
    }
}
