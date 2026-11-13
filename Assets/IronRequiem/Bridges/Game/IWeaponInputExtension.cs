namespace Unity.FPS.Game
{
    // Optional input policy; WeaponController remains responsible for the actual shot.
    public interface IWeaponInputExtension
    {
        bool HandleShootInputs(bool down, bool held, bool up);
        void CancelPendingShot();
    }
}
