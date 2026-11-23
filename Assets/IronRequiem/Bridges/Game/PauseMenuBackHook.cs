namespace IronRequiem
{
    /// Lets an overlay opened from the pause menu (the controls panel) consume the Esc/back press before the pause menu
    /// closes. InGameMenuManager (fps.UI) calls it; the overlay lives in IronRequiem.Runtime, which fps.UI cannot see.
    public static class PauseMenuBackHook
    {
        public static System.Func<bool> Override;
    }
}
