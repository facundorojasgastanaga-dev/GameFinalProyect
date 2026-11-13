using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace IronRequiem.Editor
{
    public static class T07ExplosionAuthoring
    {
        public const string ExplosionPath="Assets/IronRequiem/Prefabs/VFX/CannonExplosion_IR.prefab";
        const string Vfx="Assets/IronRequiem/VFX/CannonExplosion";
        static Transform Child(Transform parent,string name)=>T07AnimationAuthoring.Child(parent,name);
        public static void ReviseWorking()
        {
            var root=PrefabUtility.LoadPrefabContents(ExplosionPath);
            try
            {
                var core=root.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="ExpandingCore");
                core.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=core.main;main.maxParticles=8;main.startLifetime=new ParticleSystem.MinMaxCurve(.38f,.55f);
                main.startSize=new ParticleSystem.MinMaxCurve(1.6f,2.1f);main.startSpeed=new ParticleSystem.MinMaxCurve(1.5f,3.2f);main.startColor=Color.white;
                var heat=new Gradient();heat.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.85f,.5f),.25f),new GradientColorKey(new Color(1,.45f,.06f),.65f),new GradientColorKey(new Color(.8f,.12f,.01f),1)},
                    new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.05f),new GradientAlphaKey(.7f,.6f),new GradientAlphaKey(0,1)});
                var color=core.colorOverLifetime;color.color=heat;
                var material=core.GetComponent<ParticleSystemRenderer>().sharedMaterial;material.SetColor("_BaseColor",new Color(2.8f,1.4f,.32f,1));EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
                var emission=core.emission;emission.SetBursts(new[]{new ParticleSystem.Burst(0,8)});
                var size=core.sizeOverLifetime;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.35f),new Keyframe(.25f,1.25f),new Keyframe(1,.15f)));
                var smoke=root.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="ResidualSmoke");
                var smokeMain=smoke.main;smokeMain.startColor=new Color(.35f,.37f,.4f,.7f);smokeMain.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
                PrefabUtility.SaveAsPrefabAsset(root,ExplosionPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static GameObject Build()
        {
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPath);if(existing)return existing;
            Directory.CreateDirectory(Vfx);Directory.CreateDirectory(Path.GetDirectoryName(ExplosionPath));AssetDatabase.Refresh();
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/VFX/VFX_HoverBot_Explosion.prefab");
                var root=(GameObject)PrefabUtility.InstantiatePrefab(source,preview);root.name="CannonExplosion_IR";root.transform.localScale=Vector3.one;
                foreach(var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=ps.main;main.playOnAwake=false;var emission=ps.emission;emission.enabled=false;
                    ps.GetComponent<ParticleSystemRenderer>().enabled=false;
                }
                var warm=ExplosionMaterial("WarmEnergy",true,new Color(1,.7f,.3f,1));
                var smoke=ExplosionMaterial("ResidualSmoke",false,Color.white);
                var layers=new List<ParticleSystem>();
                layers.Add(Burst(root.transform,"InitialFlash",ExplosionMaterial("HotFlash",true,new Color(3,2.8f,2.2f,1)),1,1,.085f,0,1.7f,0,0,Color.white,false));
                var core=Burst(root.transform,"ExpandingCore",warm,12,12,.42f,.025f,.65f,4,8,new Color(1,.6f,.1f,1),false);layers.Add(core);
                var size=core.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.25f,1.4f),new Keyframe(1,.1f)));
                var sparks=Burst(root.transform,"HotSparks",warm,30,30,.8f,.015f,.055f,7,16,new Color(1,.48f,.08f,1),false);layers.Add(sparks);
                var sparkMain=sparks.main;sparkMain.gravityModifier=1;
                var sparksRenderer=sparks.GetComponent<ParticleSystemRenderer>();sparksRenderer.renderMode=ParticleSystemRenderMode.Stretch;sparksRenderer.lengthScale=2.5f;sparksRenderer.velocityScale=.025f;
                var fragments=Burst(root.transform,"VisualFragments",warm,10,10,.7f,.025f,.045f,4,9,new Color(.9f,.38f,.05f,1),true);layers.Add(fragments);
                var fragmentMain=fragments.main;fragmentMain.gravityModifier=1.3f;fragmentMain.startRotation3D=true;
                fragmentMain.startRotationX=new ParticleSystem.MinMaxCurve(0,6.28f);fragmentMain.startRotationY=new ParticleSystem.MinMaxCurve(0,6.28f);
                var rotation=fragments.rotationOverLifetime;rotation.enabled=true;rotation.x=5;rotation.y=9;rotation.z=3;
                var residual=Burst(root.transform,"ResidualSmoke",smoke,16,16,2.65f,.045f,.9f,.7f,2.1f,new Color(.14f,.16f,.17f,.68f),false);layers.Add(residual);
                var smokeMain=residual.main;smokeMain.gravityModifier=-.025f;
                var grow=residual.sizeOverLifetime;grow.enabled=true;grow.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.3f,1.25f),new Keyframe(1,2.2f)));
                var smokeVelocity=residual.velocityOverLifetime;smokeVelocity.enabled=true;smokeVelocity.y=.35f;
                var ring=Child(root.transform,"PressureWave");ring.localRotation=Quaternion.identity;
                ring.gameObject.AddComponent<MeshFilter>().sharedMesh=RingMesh();var ringRenderer=ring.gameObject.AddComponent<MeshRenderer>();
                ringRenderer.sharedMaterial=ExplosionMaterial("PressureWave",true,new Color(1,.55f,.12f,.7f));
                ringRenderer.shadowCastingMode=ShadowCastingMode.Off;ringRenderer.receiveShadows=false;
                var flash=Child(root.transform,"BriefLight").gameObject.AddComponent<Light>();flash.type=LightType.Point;flash.color=new Color(1,.5f,.16f);
                flash.intensity=7;flash.range=5;flash.shadows=LightShadows.None;flash.enabled=false;
                var audio=root.GetComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=1;audio.minDistance=1.5f;audio.maxDistance=32;audio.volume=.8f;audio.loop=false;
                T07AnimationAuthoring.Ensure<CannonExplosionPresentation>(root).Configure(layers.ToArray(),ring,flash);
                PrefabUtility.SaveAsPrefabAsset(root,ExplosionPath);Object.DestroyImmediate(root);
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            return AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPath);
        }
        static ParticleSystem Burst(Transform parent,string name,Material material,int maximum,int count,float lifetime,float delay,float size,float slow,float fast,Color tint,bool mesh)
        {
            var go=Child(parent,name).gameObject;var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=.2f;main.maxParticles=maximum;
            main.startLifetime=new ParticleSystem.MinMaxCurve(lifetime*.65f,lifetime);main.startDelay=delay;
            main.startSpeed=new ParticleSystem.MinMaxCurve(slow,fast);main.startSize=new ParticleSystem.MinMaxCurve(size*.65f,size);
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Local;main.startColor=tint;
            var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.1f;
            var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(mesh?Color.white:new Color(1,.25f,.06f),.4f),new GradientColorKey(mesh?Color.white:Color.gray,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.04f),new GradientAlphaKey(.7f,.3f),new GradientAlphaKey(0,1)});color.color=gradient;
            if(name=="ResidualSmoke")
            {
                gradient.SetKeys(new[]{new GradientColorKey(new Color(.6f,.5f,.42f),0),new GradientColorKey(Color.white,.3f),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.65f,.5f),new GradientAlphaKey(0,1)});color.color=gradient;
            }
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.maxParticleSize=.33f;
            if(mesh)
            {
                var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=cube.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(cube);
            }
            return ps;
        }
        static Material ExplosionMaterial(string name,bool additive,Color color)
        {
            string path=Vfx+"/"+name+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing)return existing;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")){name=name};
            mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",additive?2:0);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend",(float)(additive?BlendMode.One:BlendMode.OneMinusSrcAlpha));mat.SetFloat("_ZWrite",0);mat.SetFloat("_Cull",0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;mat.SetColor("_BaseColor",color);
            mat.SetTexture("_BaseMap",name=="PressureWave"?EffectTexture(true):name=="ResidualSmoke"?EffectTexture(false):AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/IronRequiem/VFX/Mechanics/ParticleSoftCircle.asset"));
            AssetDatabase.CreateAsset(mat,path);return mat;
        }
        static Texture2D EffectTexture(bool ring)
        {
            string path=Vfx+(ring?"/PressureEdge.asset":"/SmokeLobe.asset");
            var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(existing)return existing;
            const int size=64;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name=ring?"PressureEdge":"SmokeLobe",wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(x+.5f)/size,v=(y+.5f)/size;
                float edge=ring?Mathf.Pow(Mathf.Sin(v*Mathf.PI),1.3f):Mathf.Pow(Mathf.Clamp01(1-Vector2.Distance(new Vector2(u,v),Vector2.one*.5f)*2),.7f);
                float noise=ring?1:.5f+.5f*(Mathf.PerlinNoise(u*5,v*5)*.65f+Mathf.PerlinNoise(u*13+17,v*13+9)*.35f);
                pixels[y*size+x]=new Color(noise,noise,noise,edge*(ring?1:noise));
            }
            texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,path);return texture;
        }
        static Mesh RingMesh()
        {
            string path=Vfx+"/PressureRing.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing)return existing;
            const int segments=64;var vertices=new Vector3[(segments+1)*2];var uv=new Vector2[vertices.Length];var triangles=new int[segments*6];
            for(int i=0;i<=segments;i++)
            {
                float angle=i/(float)segments*Mathf.PI*2;var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                vertices[i*2]=direction*.44f;vertices[i*2+1]=direction*.5f;uv[i*2]=new Vector2(i/(float)segments,0);uv[i*2+1]=new Vector2(i/(float)segments,1);
                if(i==segments)continue;int n=i*6,a=i*2;triangles[n]=a;triangles[n+1]=a+2;triangles[n+2]=a+1;triangles[n+3]=a+1;triangles[n+4]=a+2;triangles[n+5]=a+3;
            }
            var mesh=new Mesh{name="PressureRing"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}
