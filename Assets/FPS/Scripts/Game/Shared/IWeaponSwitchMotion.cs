namespace Unity.FPS.Game
{
    // Optional presentation hook on a weapon. When OverridesSwitchMotion is true, PlayerWeaponsManager keeps the
    // weapon socket in place while this weapon is put down / brought up, uses SwitchPhaseSeconds for that phase and
    // lets the weapon animate itself (for example a forearm module that unlocks, ejects and docks).
    // Shooting stays blocked during the whole switch, as with the template motion.
    public interface IWeaponSwitchMotion
    {
        bool OverridesSwitchMotion { get; }
        float SwitchPhaseSeconds { get; }
    }
}
