using System;
using UnityEngine;

namespace IronRequiem
{
    /// <summary>Final authority for finger rotations after Animator, arm IK and cinematic posing.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class HandPoseController : MonoBehaviour
    {
        [Serializable]
        public sealed class Hand
        {
            [SerializeField] Transform root;
            [SerializeField] HandPoseLibrary library;
            [SerializeField] Renderer openVisual, closedVisual;
            MaterialPropertyBlock openBlock, closedBlock;
            Color[] openColors, closedColors;
            Material[] originalClosedMaterials, ownedClosedMaterials;
            Transform[] bones;
            Quaternion[] source, result;
            Quaternion wristBind;
            HandPoseLibrary.Pose pose = HandPoseLibrary.Pose.Relaxed;
            float elapsed = 1, duration = .1f, fromClosure, closure;
            public float Closure => closure;
            public bool Valid => bones != null && bones.Length > 0;

            public void Configure(Transform hand, HandPoseLibrary poses) { root = hand; library = poses; }
            public void ConfigureVisuals(Renderer open, Renderer closed) { openVisual = open; closedVisual = closed; }

            public void Initialize()
            {
                if (!root || !library) return;
                var all = root.GetComponentsInChildren<Transform>(true);
                var data = library.Bones;
                bones = new Transform[data.Length]; source = new Quaternion[data.Length]; result = new Quaternion[data.Length];
                wristBind = root.localRotation;
                openBlock = new MaterialPropertyBlock(); closedBlock = new MaterialPropertyBlock();
                PrepareClosedMaterials();
                openColors = Colors(openVisual); closedColors = Colors(closedVisual);
                // Initialization only; bounded by one hand's hierarchy (normally 21 transforms).
                for (int i = 0; i < data.Length; i++)
                {
                    for (int j = 0; j < all.Length; j++) if (all[j].name == data[i].name) { bones[i] = all[j]; break; }
                    if (!bones[i]) throw new InvalidOperationException("Hand pose bone missing: " + data[i].name);
                    result[i] = source[i] = data[i].relaxed;
                }
            }

            void PrepareClosedMaterials()
            {
                ReleaseMaterials();
                if(!closedVisual)return;
                originalClosedMaterials=closedVisual.sharedMaterials;
                ownedClosedMaterials=new Material[originalClosedMaterials.Length];
                for(int i=0;i<ownedClosedMaterials.Length;i++)
                {
                    var material=new Material(originalClosedMaterials[i]);
                    // URP's Editor validation resets transparent asset ZWrite to zero. Runtime-owned
                    // copies keep depth writes, so the folded fingers cannot show through the back plate.
                    material.SetFloat("_ZWrite",1);
                    material.SetFloat("_BlendModePreserveSpecular",0);
                    material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    ownedClosedMaterials[i]=material;
                }
                closedVisual.sharedMaterials=ownedClosedMaterials;
            }

            public void ReleaseMaterials()
            {
                if(ownedClosedMaterials==null)return;
                if(closedVisual)closedVisual.sharedMaterials=originalClosedMaterials;
                foreach(var material in ownedClosedMaterials)
                    if(material){if(Application.isPlaying)UnityEngine.Object.Destroy(material);else UnityEngine.Object.DestroyImmediate(material);}
                ownedClosedMaterials=null;originalClosedMaterials=null;
            }

            public void Request(HandPoseLibrary.Pose wanted, float seconds)
            {
                if (pose == wanted) return;
                pose = wanted; elapsed = 0; duration = Mathf.Clamp(seconds, .05f, .2f); fromClosure = closure;
                if (result != null) Array.Copy(result, source, result.Length);
            }

            public void Sample(HandPoseLibrary.Pose from,HandPoseLibrary.Pose to,float weight)
            {
                if(!Valid)return;weight=Mathf.Clamp01(weight);var data=library.Bones;
                // O(N), N <= 15 joints. Timeline scrubbing samples absolute poses without relying on elapsed frame time.
                for(int i=0;i<bones.Length;i++)
                {
                    result[i]=Quaternion.Slerp(data[i].Rotation(from),data[i].Rotation(to),weight);
                    bones[i].localRotation=result[i];
                }
                closure=Mathf.Lerp(from==HandPoseLibrary.Pose.Fist?1:0,to==HandPoseLibrary.Pose.Fist?1:0,weight);
            }

            public void Apply(float deltaTime, Transform camera)
            {
                if (!Valid) return;
                elapsed = Mathf.Min(duration, elapsed + deltaTime);
                float weight = Mathf.SmoothStep(0, 1, elapsed / duration);
                var data = library.Bones;
                // O(N), N <= 15 authored finger joints. No reads from last frame's animated pose and no allocation.
                for (int i = 0; i < bones.Length; i++)
                {
                    result[i] = Quaternion.Slerp(source[i], data[i].Rotation(pose), weight);
                    bones[i].localRotation = result[i];
                }
                closure = Mathf.Lerp(fromClosure, pose == HandPoseLibrary.Pose.Fist ? 1f : 0f, weight);
                if (openVisual && closedVisual)
                {
                    closedVisual.enabled = openVisual.enabled && closure > .001f && openVisual.gameObject.activeInHierarchy;
                    // The imported skin may use an opaque pass. Hide it completely at full closure,
                    // retaining its enabled flag as the presentation's visibility request.
                    openVisual.forceRenderingOff = closure >= .999f;
                    Fade(openVisual,openBlock,openColors,1-closure);
                    Fade(closedVisual,closedBlock,closedColors,closure);
                }
                if (!camera || !root.parent || closure <= 0) return;
                var forward = (root.position - root.parent.position).normalized;
                var palm = Vector3.ProjectOnPlane(-camera.up + camera.forward * .3f, forward).normalized;
                if (palm.sqrMagnitude < .5f) return;
                var localBasis = Quaternion.LookRotation(library.FingerDirection, library.PalmNormal);
                var fistOrientation = Quaternion.LookRotation(forward, palm) * Quaternion.Inverse(localBasis);
                root.rotation = Quaternion.Slerp(root.parent.rotation * wristBind, fistOrientation, closure);
            }

            static Color[] Colors(Renderer renderer)
            {
                if (!renderer) return Array.Empty<Color>();
                var materials=renderer.sharedMaterials;var colors=new Color[materials.Length];
                for(int i=0;i<materials.Length;i++)colors[i]=materials[i]&&materials[i].HasProperty("_BaseColor")?materials[i].GetColor("_BaseColor"):Color.white;
                return colors;
            }
            static void Fade(Renderer renderer,MaterialPropertyBlock block,Color[] colors,float alpha)
            {
                // O(M), M <= 3 material slots in the authored closed fist. No material instantiation.
                for(int i=0;i<colors.Length;i++)
                {
                    renderer.GetPropertyBlock(block,i);var color=colors[i];color.a*=alpha;
                    block.SetColor("_BaseColor",color);renderer.SetPropertyBlock(block,i);
                }
            }
        }

        [SerializeField] Hand left = new Hand(), right = new Hand();
        [SerializeField] Transform firstPersonCamera;
        [SerializeField] bool timelineDriven;
        HandPoseLibrary.Pose leftFrom,leftTo,rightFrom,rightTo;
        float leftSampleWeight,rightSampleWeight;bool leftSampled,rightSampled;
        bool initialized;
        public float LeftClosure => left.Closure;
        public float RightClosure => right.Closure;
        public void Configure(Transform leftHand, HandPoseLibrary leftPoses, Transform rightHand, HandPoseLibrary rightPoses, Transform camera,bool timeline=false)
        {
            left.Configure(leftHand, leftPoses); right.Configure(rightHand, rightPoses); firstPersonCamera = camera;timelineDriven=timeline;initialized=false;
        }
        void Awake() => Initialize();
        void OnDestroy(){left.ReleaseMaterials();right.ReleaseMaterials();}
        public void Initialize()
        {
            if (initialized) return;
            left.Initialize(); right.Initialize(); initialized = true;
        }
        public void RequestLeft(HandPoseLibrary.Pose pose, float blend = .1f) => left.Request(pose, blend);
        public void RequestRight(HandPoseLibrary.Pose pose, float blend = .1f) => right.Request(pose, blend);
        public void ConfigureClosedVisuals(Renderer leftOpen, Renderer leftClosed, Renderer rightOpen, Renderer rightClosed)
        {
            left.ConfigureVisuals(leftOpen,leftClosed); right.ConfigureVisuals(rightOpen,rightClosed);
        }
        public void SampleLeft(HandPoseLibrary.Pose from,HandPoseLibrary.Pose to,float weight)
        {
            Initialize();leftFrom=from;leftTo=to;leftSampleWeight=weight;leftSampled=true;left.Sample(from,to,weight);
        }
        public void SampleRight(HandPoseLibrary.Pose from,HandPoseLibrary.Pose to,float weight)
        {
            Initialize();rightFrom=from;rightTo=to;rightSampleWeight=weight;rightSampled=true;right.Sample(from,to,weight);
        }
        void LateUpdate()
        {
            Initialize();
            if(timelineDriven)
            {
                if(leftSampled)left.Sample(leftFrom,leftTo,leftSampleWeight);
                if(rightSampled)right.Sample(rightFrom,rightTo,rightSampleWeight);
                return;
            }
            left.Apply(Time.deltaTime, firstPersonCamera); right.Apply(Time.deltaTime, firstPersonCamera);
        }
    }
}
