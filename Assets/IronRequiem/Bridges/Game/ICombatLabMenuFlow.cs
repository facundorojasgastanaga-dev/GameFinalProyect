namespace IronRequiem
{
    // Shared UI contract; the existing laboratory flow owns start, death and restart.
    public interface ICombatLabMenuFlow
    {
        void BeginGame();
        void ReturnToMainMenu();
    }
}
