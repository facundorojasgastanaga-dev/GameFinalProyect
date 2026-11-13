namespace IronRequiem
{
    // 08/11/2026. Hand-off between the main menu (fps.UI) and the level flow (IronRequiem.Runtime):
    // JUGAR in IntroMenu, or a level continuing the run, names the next scene right before loading it.
    // Direct loads (Editor, tests, Build Settings) never set it.
    public static class MainMenuLaunch
    {
        public const string MainMenuScene = "IntroMenu";
        public static string PendingTarget { get; private set; }
        public static void FromMenu(string sourceScene, string targetScene) { if (sourceScene == MainMenuScene) PendingTarget = targetScene; }
        public static void Continue(string targetScene) => PendingTarget = targetScene;
        public static string Consume() { var t = PendingTarget; PendingTarget = null; return t; }
        public static void Clear() => PendingTarget = null;
    }
}
