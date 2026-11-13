using UnityEngine;

namespace IronRequiem
{
    // 12/11/2026. Blocks gameplay input (move, look, fire, jump...) while a cinematic owns the screen.
    // Read by PlayerInputHandler.CanProcessInput; menus keep their own UI input.
    public static class PlayerInputLock
    {
        public static bool Locked { get; private set; }
        public static void Set(bool locked) => Locked = locked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() => Locked = false;
    }
}
