namespace IronRequiem
{
    // Optional admission policy on the player (arms-fix pass, 19/11/2026). PlayerWeaponsManager reads it from its own
    // GameObject. It gates player input only: scripted SwitchToWeaponIndex calls are never blocked.
    public interface IWeaponSwitchGate
    {
        // Number keys and wheel may start a weapon switch.
        bool CanSwitchWeapon { get; }
        // Fire/aim input reaches the active weapon. False only while a scripted hand-over to another weapon is
        // pending (the active weapon is about to be put away), so it cannot keep firing in that window.
        bool CanFireActiveWeapon { get; }
    }
}
