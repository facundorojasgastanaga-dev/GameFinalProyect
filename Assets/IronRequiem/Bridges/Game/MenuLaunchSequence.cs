namespace IronRequiem
{
    // 11/11/2026. Optional cinematic that the main menu plays before loading the game (JUGAR).
    // RequiemMenuView (fps.UI) only knows this interface; the hangar scene (IronRequiem.Runtime) registers one.
    // Without a registered sequence the menu loads immediately, exactly as before.
    public interface IMenuLaunchSequence
    {
        bool IsPlaying { get; }
        void Play(System.Action onComplete);
    }

    public static class MenuLaunchRegistry
    {
        public static IMenuLaunchSequence Active;
    }
}
