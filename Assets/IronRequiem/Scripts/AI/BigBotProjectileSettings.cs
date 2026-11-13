using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
namespace IronRequiem
{
    public sealed class BigBotProjectileSettings : MonoBehaviour,IProjectileChargeReceiver
    {
        public void ApplyCharge(float unused)
        {
            var projectile=GetComponent<ProjectileStandard>();var owner=projectile.Owner?projectile.Owner.GetComponent<BigBotCombat>():null;
            if(!owner)return;
            projectile.Damage=owner.Damage;projectile.Speed=owner.ProjectileSpeed;projectile.GravityDownAcceleration=0;projectile.InheritWeaponVelocity=false;projectile.TrajectoryCorrectionDistance=-1;projectile.AreaOfDamage=owner.ExplosiveShots?GetComponent<DamageArea>():null;
            if(projectile.AreaOfDamage)projectile.AreaOfDamage.AreaOfEffectDistance=owner.ExplosionRadius;
            float life=owner.ProjectileRange/owner.ProjectileSpeed;projectile.MaxLifeTime=life;Destroy(gameObject,life);owner.NotifyProjectileSpawned(projectile);
        }
    }
}
