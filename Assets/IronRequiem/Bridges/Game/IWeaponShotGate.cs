namespace IronRequiem
{
    // Optional admission policy. WeaponController remains the only owner of accepted shots.
    public interface IWeaponShotGate
    {
        bool CanFire { get; }
    }
}
