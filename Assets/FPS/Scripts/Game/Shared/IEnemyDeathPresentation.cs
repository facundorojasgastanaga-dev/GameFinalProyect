namespace Unity.FPS.Game
{
    // Optional presentation hook. EnemyController remains responsible for loot, objectives and disposal.
    public interface IEnemyDeathPresentation
    {
        bool TryPlayDeathPresentation();
    }
}
