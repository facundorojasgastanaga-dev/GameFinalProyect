using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    // Runs after ProjectileStandard's OnEnable. Its existing shot callback advances the
    // spawn by muzzle velocity even when InheritWeaponVelocity is false. Correct this
    // representation only; native collision sweeps, aim correction and damage remain active.
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(ProjectileStandard))]
    public sealed class ProjectileMuzzleOrigin : MonoBehaviour
    {
        ProjectileBase projectile;
        ProjectileStandard standard;
        TrailRenderer trail;
        void Awake() { projectile = GetComponent<ProjectileBase>(); standard = GetComponent<ProjectileStandard>(); trail = GetComponent<TrailRenderer>(); }
        void OnEnable() => projectile.OnShoot += RestoreOrigin;
        void OnDisable() => projectile.OnShoot -= RestoreOrigin;
        void RestoreOrigin()
        {
            if (standard.InheritWeaponVelocity || standard.TrajectoryCorrectionDistance == 0) return;
            transform.position = projectile.InitialPosition;
            if (trail) trail.Clear();
        }
    }
}
