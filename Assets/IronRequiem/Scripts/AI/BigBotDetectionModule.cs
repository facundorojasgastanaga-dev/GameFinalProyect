using Unity.FPS.AI;
using UnityEngine;
namespace IronRequiem
{
    public sealed class BigBotDetectionModule : DetectionModule
    {
        // Keep the template distance/LOS detector; damage alone must not acquire an unseen player.
        public override void OnDamaged(GameObject damageSource)
        {
            var bot=GetComponentInParent<BigBotCombat>();
            if(bot&&bot.CanSeeSource(damageSource))base.OnDamaged(damageSource);
        }
    }
}
