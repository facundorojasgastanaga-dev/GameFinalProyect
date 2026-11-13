using UnityEngine;
namespace IronRequiem
{
    [CreateAssetMenu(menuName="Iron Requiem/Combat tuning")]
    public sealed class CombatTuningProfile : ScriptableObject
    {
        [Min(.1f)] public float PlayerSpeed=1.15f;
        [Min(.1f)] public float EnemySpeed=1.2f;
        [Min(.1f)] public float EnemyDamage=.8f;
        [Min(.1f)] public float EnemyHealth=.8f;
        [Min(.1f)] public float StolenBigBotDamage=1.2f;
        [Min(.1f)] public float StolenBigBotEnergy=1.2f;
    }
}