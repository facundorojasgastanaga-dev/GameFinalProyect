namespace Unity.FPS.AI
{
    // Optional neutral activity (Iron Requiem hangar scavengers). While IsScavenging is true EnemyMobile stays in
    // AIState.Scavenging and lets the component drive navigation; detecting a target switches to Follow as usual.
    public interface IEnemyScavenging
    {
        bool IsScavenging { get; }
        void UpdateScavenging();
    }
}
