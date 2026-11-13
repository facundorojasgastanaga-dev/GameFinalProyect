using UnityEngine;
namespace IronRequiem
{
    // Presentation for the existing execution reservation, not a second extraction/inventory system.
    [RequireComponent(typeof(EnemyExecutionSource),typeof(SawBotCombat))]
    public sealed class SawBotWeaponSource : MonoBehaviour
    {
        [SerializeField] Renderer[] rightSawRenderers;
        [SerializeField] Collider[] rightSawColliders;
        EnemyExecutionSource source;SawBotCombat combat;bool removed;
        public bool WeaponRemoved=>removed;
        public void Configure(Renderer[] views,Collider[] colliders){rightSawRenderers=views;rightSawColliders=colliders;}
        void Awake(){source=GetComponent<EnemyExecutionSource>();combat=GetComponent<SawBotCombat>();}
        void LateUpdate()
        {
            bool taken=source.IsReserved||source.Delivered;if(taken==removed)return;removed=taken;
            foreach(var view in rightSawRenderers)if(view)view.forceRenderingOff=taken;
            foreach(var collider in rightSawColliders)if(collider)collider.enabled=!taken&&!combat.IsDead;
            combat.SetExecutionReserved(taken);
        }
    }
}
