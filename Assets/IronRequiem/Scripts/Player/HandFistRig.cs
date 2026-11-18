using UnityEngine;

namespace IronRequiem
{
    /// v7 procedural closed fist for one FPS hand. Every finger hinges about the knuckle line (index to little MCP) so the
    /// fingers fold straight into the palm without twisting or spreading; the thumb is solved once (CCD) to lie across the
    /// middle phalanges of index and middle on the palm side. The closed pose is built once in hand space, the first frame
    /// the hand is posed, and then only blended: no allocation
    /// per frame.
    [System.Serializable]
    public sealed class HandFistRig
    {
        [Tooltip("Degrees per joint: knuckle (MCP), middle (PIP), tip (DIP).")]
        [SerializeField,Range(0,130)] float proximal=92,middle=106,distal=72;
        [Tooltip("0 = keep the rig's finger spread, 1 = all fingers parallel.")]
        [SerializeField,Range(0,1)] float together=.85f;
        [Tooltip("Palm side override: 0 = the rig default given by the owner (ProtagonistV01 FPS rig: right +1, left -1, checked on close-up captures), 1 or -1 = force.")]
        [SerializeField,Range(-1,1)] int palmSign;
        [SerializeField,Min(.01f)] float closeTime=.06f,openTime=.15f;
        [Header("Pose hecha a mano (Tools > Iron Requiem > Fist Pose)")]
        [Tooltip("Use the finger rotations saved with the Fist Pose tool instead of the computed fist.")]
        [SerializeField] bool useCustomPose;
        [SerializeField] string[] customNames=new string[0];
        [SerializeField] Quaternion[] customRotations=new Quaternion[0];
        public bool UsesCustomPose=>useCustomPose&&customNames!=null&&customNames.Length>0&&customRotations!=null&&customRotations.Length==customNames.Length;
        public bool UseCustomPose{get=>useCustomPose;set{useCustomPose=value;built=false;}}
        /// Finger bones under a hand (same rule as the runtime rig): every chain under the hand, up to 3 joints.
        public static System.Collections.Generic.List<Transform> FingerBones(Transform hand,System.Func<Transform,bool> isFinger)
        {
            var list=new System.Collections.Generic.List<Transform>();
            for(int i=0;i<hand.childCount;i++)
            {
                var t=hand.GetChild(i);int depth=0;
                while(t&&isFinger(t)&&depth<3){list.Add(t);Transform next=null;for(int c=0;c<t.childCount;c++)if(isFinger(t.GetChild(c))){next=t.GetChild(c);break;}t=next;depth++;}
            }
            return list;
        }
        /// Saves the current local rotation of these bones as the closed fist.
        public void StoreCustom(System.Collections.Generic.List<Transform> fingerBones)
        {
            customNames=new string[fingerBones.Count];customRotations=new Quaternion[fingerBones.Count];
            for(int i=0;i<fingerBones.Count;i++){customNames[i]=fingerBones[i].name;customRotations[i]=fingerBones[i].localRotation;}
            useCustomPose=true;built=false;
        }
        [Tooltip("Written by Fist Pose > 4. Calcular puño: flexion axis (bone local space, sign-tested) and final angle of each bone, for reference.")]
        [SerializeField] Vector3[] bakedAxes=new Vector3[0];
        [SerializeField] float[] bakedAngles=new float[0];
        /// Saves a computed fist (current local rotations of the bones) plus the axes and angles used.
        public void StoreBake(System.Collections.Generic.List<Transform> fingerBones,Vector3[] axes,float[] angles){StoreCustom(fingerBones);bakedAxes=axes;bakedAngles=angles;}
        public bool TryGetCustom(string bone,out Quaternion rotation)
        {
            rotation=Quaternion.identity;if(customNames==null)return false;
            for(int i=0;i<customNames.Length;i++)if(customNames[i]==bone){rotation=customRotations[i];return true;}
            return false;
        }

        Transform hand;Transform[] bones;Quaternion[] rest,closed,source,written;bool[] hasWritten;
        int[] fingerFirst;int fingerCount;int thumb1=-1,thumb2=-1,thumb3=-1,index1=-1,little1=-1,middle1=-1;
        bool built;float weight;
        public float Weight=>weight;
        /// Rebuilds the closed pose next frame (Inspector edits in Play mode take effect live).
        public void Invalidate(){built=false;}
        public bool Valid=>bones!=null&&bones.Length>0;
        public int ResolvedPalmSign {get;private set;}

        int defaultPalm=1;
        public void Collect(Transform handBone,System.Func<Transform,bool> isFinger,int rigPalmSign)
        {
            hand=handBone;defaultPalm=rigPalmSign>=0?1:-1;var list=new System.Collections.Generic.List<Transform>();var firsts=new System.Collections.Generic.List<int>();
            for(int i=0;i<hand.childCount;i++)
            {
                var root=hand.GetChild(i);if(!isFinger(root))continue;
                string n=root.name.ToLowerInvariant();bool isThumb=n.Contains("thumb");
                int start=list.Count;var t=root;int depth=0;
                while(t&&isFinger(t)&&depth<3){list.Add(t);Transform next=null;for(int c=0;c<t.childCount;c++)if(isFinger(t.GetChild(c))){next=t.GetChild(c);break;}t=next;depth++;}
                if(isThumb){thumb1=start;thumb2=depth>1?start+1:-1;thumb3=depth>2?start+2:-1;continue;}
                firsts.Add(start);
                if(n.Contains("index"))index1=start;else if(n.Contains("little")||n.Contains("pinky"))little1=start;else if(n.Contains("middle"))middle1=start;
            }
            bones=list.ToArray();fingerFirst=firsts.ToArray();fingerCount=fingerFirst.Length;
            if(index1<0&&fingerCount>0)index1=fingerFirst[0];if(little1<0&&fingerCount>0)little1=fingerFirst[fingerCount-1];if(middle1<0)middle1=index1;
            rest=new Quaternion[bones.Length];closed=new Quaternion[bones.Length];source=new Quaternion[bones.Length];written=new Quaternion[bones.Length];hasWritten=new bool[bones.Length];
            for(int i=0;i<bones.Length;i++){rest[i]=bones[i].localRotation;source[i]=rest[i];closed[i]=rest[i];}
        }
        bool InChain(int first,int i)=>i>=first&&i<first+3&&i<bones.Length&&bones[i].IsChildOf(bones[first]);

        // Builds the closed pose from the bind pose. sign: +1/-1 palm side, decided from the curl clip (or the other hand).
        void Build(int sign)
        {
            ResolvedPalmSign=sign;
            var current=new Quaternion[bones.Length];for(int i=0;i<bones.Length;i++){current[i]=bones[i].localRotation;bones[i].localRotation=rest[i];}
            var inv=Quaternion.Inverse(hand.rotation);
            Vector3 L=(inv*(bones[index1].position-bones[little1].position)).normalized;
            Vector3 D=Vector3.zero;for(int f=0;f<fingerCount;f++){int a=fingerFirst[f];if(InChain(a,a+1))D+=inv*(bones[a+1].position-bones[a].position);}
            D=Vector3.ProjectOnPlane(D,L).normalized;Vector3 N=Vector3.Cross(D,L).normalized*sign;
            Vector3 hingeHand=Vector3.Cross(D,N).normalized; // rotating D about this axis moves it toward the palm (N)
            Vector3 hingeWorld=hand.rotation*hingeHand;
            // fingers: each joint about the same hinge, expressed in its parent frame
            for(int f=0;f<fingerCount;f++)
                for(int s=0;s<3;s++)
                {
                    int i=fingerFirst[f]+s;if(!InChain(fingerFirst[f],i))break;
                    float angle=s==0?proximal:s==1?middle:distal;
                    var bone=bones[i];Vector3 axisParent=Quaternion.Inverse(bone.parent.rotation)*hingeWorld;
                    var adduct=Quaternion.identity;
                    if(s==0&&InChain(i,i+1))
                    {   // fingers together: swing each proximal phalanx in the palm plane toward the mean finger direction
                        Vector3 own=Vector3.ProjectOnPlane(inv*(bones[i+1].position-bone.position),N).normalized;
                        float spread=Mathf.Clamp(Vector3.SignedAngle(own,D,N),-25f,25f)*together;
                        adduct=Quaternion.AngleAxis(spread,Quaternion.Inverse(bone.parent.rotation)*(hand.rotation*N));
                    }
                    bone.localRotation=Quaternion.AngleAxis(angle,axisParent)*adduct*rest[i];closed[i]=bone.localRotation;
                }
            // thumb: across the middle phalanges of index and middle, on the palm side
            if(thumb1>=0&&thumb2>=0)
            {
                Transform tA=bones[index1+1<bones.Length&&InChain(index1,index1+1)?index1+1:index1],tB=bones[middle1+1<bones.Length&&InChain(middle1,middle1+1)?middle1+1:middle1];
                Transform tA2=InChain(index1,index1+2)?bones[index1+2]:tA,tB2=InChain(middle1,middle1+2)?bones[middle1+2]:tB;
                float thick=.45f*Vector3.Distance(bones[index1].position,tA.position);
                Vector3 target=(tA.position+tA2.position+tB.position+tB2.position)*.25f+hand.rotation*N*thick;
                Transform last=bones[thumb3>=0?thumb3:thumb2],prev=bones[thumb3>=0?thumb2:thumb1];
                Vector3 tipLocal=Quaternion.Inverse(last.rotation)*(last.position-prev.position)*.85f;
                int[] chain=thumb3>=0?new[]{thumb3,thumb2,thumb1}:new[]{thumb2,thumb1};
                for(int it=0;it<12;it++)
                    foreach(int i in chain)
                    {
                        var b=bones[i];Vector3 tip=last.position+last.rotation*tipLocal;
                        Vector3 from=tip-b.position,to=target-b.position;if(from.sqrMagnitude<1e-8f||to.sqrMagnitude<1e-8f)continue;
                        var step=Quaternion.FromToRotation(from,to);b.rotation=Quaternion.Slerp(Quaternion.identity,step,.7f)*b.rotation;
                    }
                foreach(int i in chain)closed[i]=bones[i].localRotation;
            }
            // a pose saved by hand with the Fist Pose tool wins over the computed one
            if(UsesCustomPose)for(int i=0;i<bones.Length;i++)if(TryGetCustom(bones[i].name,out var custom))closed[i]=custom;
            for(int i=0;i<bones.Length;i++)bones[i].localRotation=current[i];
            built=true;
        }
        /// Call after the hand is posed this frame.
        public void Apply(bool wanted)
        {
            if(!Valid)return;
            // the clip-based detection proved unreliable (rest pose may already be the clip pose): use the rig default
            if(!built)Build(palmSign!=0?palmSign:defaultPalm);
            float target=wanted?1:0;
            weight=Mathf.MoveTowards(weight,target,Time.deltaTime/(target>weight?closeTime:openTime));
            float w=weight*weight*(3-2*weight);
            for(int i=0;i<bones.Length;i++)
            {
                var bone=bones[i];var current=bone.localRotation;
                // bones the Animator does not write keep our last value: restore their animated source instead
                if(hasWritten[i]&&Quaternion.Angle(current,written[i])<.01f)current=source[i];else source[i]=current;
                if(w<=0){if(hasWritten[i]){bone.localRotation=current;hasWritten[i]=false;}continue;}
                var result=Quaternion.Slerp(current,closed[i],w);bone.localRotation=result;written[i]=result;hasWritten[i]=true;
            }
        }
        public string Describe()
        {
            if(!Valid)return "none";
            var sb=new System.Text.StringBuilder();sb.Append("palm=").Append(ResolvedPalmSign).Append(" built=").Append(built).Append(" bones=");
            for(int i=0;i<bones.Length;i++){if(i>0)sb.Append(',');sb.Append(bones[i].name);}
            return sb.ToString();
        }
    }
}
