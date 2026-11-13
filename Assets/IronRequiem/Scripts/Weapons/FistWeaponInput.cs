using Unity.FPS.Game;
using UnityEngine;
namespace IronRequiem
{
    /// v4 fist weapon: M1 input policy for the permanent melee slot. It never fires a projectile.
    /// Press starts the punch wind-up in ProtagonistMelee (the same rig as F), a short tap is a quick punch,
    /// holding charges a heavy punch that is released on button up.
    [RequireComponent(typeof(WeaponController))]
    public sealed class FistWeaponInput : MonoBehaviour, IWeaponInputExtension
    {
        WeaponController weapon;ProtagonistMelee melee;GameObject boundOwner;bool armed=true;
        public ProtagonistMelee Melee=>Resolve();
        void Awake()=>weapon=GetComponent<WeaponController>();
        ProtagonistMelee Resolve()
        {
            var owner=weapon?weapon.Owner:null;
            if(owner!=boundOwner){boundOwner=owner;melee=owner?owner.GetComponent<ProtagonistMelee>():null;}
            return melee;
        }
        public bool HandleShootInputs(bool down,bool held,bool up)
        {
            var m=Resolve();if(!m||!weapon.IsWeaponActive)return false;
            // a press that lands while the fist is still being raised (switch) starts the charge once it can
            if((down||held)&&armed&&!m.IsAttacking&&m.BeginCharge())armed=false;
            if(up||(!held&&!down)){m.ReleaseCharge();armed=true;}
            return false; // no recoil, no shot
        }
        public void CancelPendingShot(){var m=Resolve();if(m)m.CancelCharge();}
        // Switching away (or the weapon being lowered) while charging drops the charge instead of leaving it held.
        void Update(){if(melee&&melee.IsCharging&&(!weapon.IsWeaponActive||weapon.Owner!=boundOwner))melee.CancelCharge();}
        void OnDisable(){if(melee)melee.CancelCharge();}
    }
}
