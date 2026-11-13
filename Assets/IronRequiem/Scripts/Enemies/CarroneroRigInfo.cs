using UnityEngine;

namespace IronRequiem
{
    /// Carroñero visual rig references, written by the editor build (C2) and read when the enemy prefab is assembled (C3).
    public sealed class CarroneroRigInfo : MonoBehaviour
    {
        public Transform[] bones;
        public Transform saw, bladeStart, bladeEnd;
        public Vector3 sawAxisLocal = Vector3.right;
        public Transform Bone(string name) { if (bones != null) foreach (var b in bones) if (b && b.name == name) return b; return null; }
    }
}
