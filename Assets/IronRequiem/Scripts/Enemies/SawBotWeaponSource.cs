using UnityEngine;
namespace IronRequiem
{
    // Presentation for the existing execution reservation, not a second extraction/inventory system.
    [RequireComponent(typeof(EnemyExecutionSource),typeof(SawBotCombat))]
    public sealed class SawBotWeaponSource : MonoBehaviour
    {
        [SerializeField] Renderer[] rightSawRenderers;
        [SerializeField] Collider[] rightSawColliders;
        EnemyExecutionSource source;SawBotCombat combat;bool removed,ripped,viewsHidden;
        public bool WeaponRemoved=>removed;
        // Executions v02: the saw mesh stays visible from E (reservation) until the Rip beat; the colliders go off at E.
        public Renderer[] RightSawRenderers=>rightSawRenderers;
        public bool WeaponHiddenByRip=>viewsHidden;
        public void Rip(){ripped=true;}
        public void ReleaseRip(){ripped=false;}
        // Executions v02: the bone the player's hand closes on: the saw arm's forearm (else upper arm) among the right-saw skin
        // bones; the skin root bone (the whole Rig) only as a last resort.
        Transform joint;
        public Transform Joint{get{if(joint)return joint;if(rightSawRenderers==null)return null;Transform fallback=null;
            foreach(var r in rightSawRenderers){if(!r)continue;var skin=r as SkinnedMeshRenderer;if(!skin){fallback=fallback?fallback:r.transform;continue;}
                foreach(var name in new[]{"Forearm_R","UpperArm_R"})foreach(var b in skin.bones)if(b&&b.name==name)return joint=b;
                fallback=fallback?fallback:(skin.rootBone?skin.rootBone:r.transform);}
            return fallback;}}
        public void Configure(Renderer[] views,Collider[] colliders){rightSawRenderers=views;rightSawColliders=colliders;}
        void Awake(){source=GetComponent<EnemyExecutionSource>();combat=GetComponent<SawBotCombat>();}
        void LateUpdate()
        {
            bool taken=source.IsReserved||source.Delivered;
            if(!taken)ripped=false;
            bool hide=(taken&&ripped)||source.Delivered;
            if(hide!=viewsHidden){viewsHidden=hide;foreach(var view in rightSawRenderers)if(view)view.forceRenderingOff=hide;}
            if(taken==removed)return;removed=taken;
            foreach(var collider in rightSawColliders)if(collider)collider.enabled=!taken&&!combat.IsDead;
            combat.SetExecutionReserved(taken);
        }
    }
}
