using UnityEngine;
namespace IronRequiem
{
    public sealed class SawBotAnimationEvents : MonoBehaviour
    {
        [SerializeField] SawBotCombat combat;
        public void Configure(SawBotCombat target)=>combat=target;
        public void SawImpact(){if(combat)combat.Impact();}
        public void SawRecover(){if(combat)combat.Recover();}
    }
}