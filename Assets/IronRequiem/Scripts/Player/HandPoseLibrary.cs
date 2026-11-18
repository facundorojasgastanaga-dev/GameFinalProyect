using System;
using UnityEngine;

namespace IronRequiem
{
    [CreateAssetMenu(menuName = "Iron Requiem/Hand pose library")]
    public sealed class HandPoseLibrary : ScriptableObject
    {
        public enum Pose { Open, Relaxed, Fist, Grip, Inspect }

        [Serializable]
        public struct BonePose
        {
            public string name;
            public Quaternion bind, open, relaxed, fist, grip, inspect;
            public Quaternion Rotation(Pose pose)
            {
                switch (pose)
                {
                    case Pose.Open: return open;
                    case Pose.Fist: return fist;
                    case Pose.Grip: return grip;
                    case Pose.Inspect: return inspect;
                    default: return relaxed;
                }
            }
        }

        [SerializeField] BonePose[] bones = Array.Empty<BonePose>();
        [SerializeField] Vector3 fingerDirection = Vector3.up, palmNormal = Vector3.forward;
        public BonePose[] Bones => bones;
        public Vector3 FingerDirection => fingerDirection;
        public Vector3 PalmNormal => palmNormal;

        public void Configure(BonePose[] poses, Vector3 direction, Vector3 normal)
        {
            bones = poses; fingerDirection = direction.normalized; palmNormal = normal.normalized;
        }
    }
}
