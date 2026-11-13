using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // Optional per-projectile hook: adjusts the damage of a single hit (for example distance falloff).
    // ProjectileStandard keeps detection, damage application and impact effects.
    public interface IProjectileHitDamageModifier
    {
        float ModifyHitDamage(float damage, Vector3 hitPoint);
    }
}
