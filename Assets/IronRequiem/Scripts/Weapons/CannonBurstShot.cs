using Unity.FPS.Game;
using UnityEngine;
namespace IronRequiem
{
    [RequireComponent(typeof(WeaponController))]
    public sealed class CannonBurstShot : MonoBehaviour, IWeaponInputExtension, IStolenWeaponBinding
    {
        [SerializeField,Range(1,6)] int burstCount=3;
        [SerializeField,Min(.05f)] float shotInterval=.18f;
        [SerializeField,Min(.1f)] float burstPause=1.5f;
        [SerializeField,Min(1)] float energyPerShot=12;
        WeaponController weapon;StolenCannon module;int remaining;float nextShot,nextBurst;bool requirePress=true;
        public int BurstCount=>burstCount;
        public float ShotInterval=>shotInterval;
        public float BurstPause=>burstPause;
        public float EnergyPerShot=>energyPerShot;
        public int PendingShots=>remaining;
        public int ShotsFired {get;private set;}
        public float TotalEnergySpent {get;private set;}
        bool CanFire=>module&&module.Carrier&&module.Carrier.CanUseHeldWeapon&&weapon.IsWeaponActive&&!module.Overdrive.IsArmed&&module.Ammo.Remaining+.001f>=energyPerShot;
        void Awake()=>weapon=GetComponent<WeaponController>();
        public void Bind(StolenCannon source){CancelPendingShot();module=source;nextBurst=Time.time;}
        public void CancelPendingShot()
        {
            remaining=0;requirePress=true;nextBurst=Mathf.Max(nextBurst,Time.time+burstPause);
            if(!weapon||!weapon.WeaponMuzzle)return;
            foreach(var ps in weapon.WeaponMuzzle.GetComponentsInChildren<ParticleSystem>(true)){ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);Destroy(ps.gameObject);}
        }
        void LateUpdate(){if(remaining>0&&!CanFire)CancelPendingShot();}
        void OnDisable()=>CancelPendingShot();
        public bool HandleShootInputs(bool down,bool held,bool up)
        {
            if(down)requirePress=false;
            if(up)requirePress=true;
            if(!CanFire){CancelPendingShot();return false;}
            if(remaining==0&&Time.time>=nextBurst&&(down||(held&&!requirePress))){remaining=Mathf.Max(1,burstCount);nextShot=Time.time;}
            if(remaining==0||Time.time<nextShot)return false;
            if(!weapon.TryShootSingleWithCharge(0))return false;
            // Commit queue state before energy callbacks, which may arm OVERDRIVE and cancel this burst.
            remaining--;ShotsFired++;TotalEnergySpent+=energyPerShot;nextShot=Time.time+shotInterval;
            if(remaining==0)nextBurst=Time.time+burstPause;
            module.Ammo.TryConsume(energyPerShot);
            return true;
        }
    }
}
