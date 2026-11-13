namespace IronRequiem
{
    // All temporary weapon inputs bind to the existing per-instance modular resource.
    public interface IStolenWeaponBinding
    {
        void Bind(StolenCannon source);
    }
}
