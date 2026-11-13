using UnityEngine;

namespace IronRequiem
{
    public sealed class RobotDetachedArm : MonoBehaviour
    {
        [SerializeField] SkinnedMeshRenderer source;
        [SerializeField] Material interior;
        public bool IsDetached { get; private set; }
        public SkinnedMeshRenderer Source => source;
        public void Configure(SkinnedMeshRenderer skin, Material cap) { source=skin;interior=cap; }
        public void Detach(Material outside,float lifetime,LayerMask ground)
        {
            if(IsDetached)return;IsDetached=true;
            if(source)RobotDestructionPool.ThrowArm(source,outside,interior,lifetime,ground);
            // Only the separated right limb; skeleton, left cannon and navigation stay intact.
            gameObject.SetActive(false);
        }
    }
}
