using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
namespace IronRequiem
{
    [RequireComponent(typeof(WeaponController))]
    public sealed class StolenSawAttack : MonoBehaviour,IWeaponInputExtension,IStolenWeaponBinding
    {
        [SerializeField,Min(0)] float damagePerSecond=45;
        [SerializeField,Min(.1f)] float reach=1.65f;
        [SerializeField,Min(.01f)] float contactRadius=.2f;
        [SerializeField,Range(.02f,.5f)] float damageInterval=.1f;
        [SerializeField,Min(.01f)] float energyPerSecond=12;
        [SerializeField] Transform mechanicalVisual;
        [SerializeField] ParticleSystem contactSparks;
        [SerializeField] AudioClip motorLoop;
        readonly Collider[] candidates=new Collider[64];readonly RaycastHit[] blockers=new RaycastHit[64];readonly HashSet<Health> damaged=new HashSet<Health>();
        WeaponController weapon;StolenCannon module;Camera camera;AudioSource audioSource;Vector3 visualBind;float accumulator;int frame=-1;
        public bool IsRunning{get;private set;}public int TickCount{get;private set;}public float TotalEnergySpent{get;private set;}public float DamagePerSecond=>damagePerSecond;public float Reach=>reach;public float EnergyPerSecond=>energyPerSecond;
        public void Configure(Transform view,ParticleSystem sparks,AudioClip motor){mechanicalVisual=view;contactSparks=sparks;motorLoop=motor;}
        void Awake(){weapon=GetComponent<WeaponController>();audioSource=GetComponent<AudioSource>();if(mechanicalVisual)visualBind=mechanicalVisual.localPosition;}
        public void Bind(StolenCannon source){CancelPendingShot();module=source;camera=source&&source.Carrier?source.Carrier.GetComponent<PlayerCharacterController>().PlayerCamera:null;}
        bool CanRun=>module&&module.Carrier&&module.Carrier.CanUseHeldWeapon&&weapon.IsWeaponActive&&!module.Overdrive.IsArmed&&Time.timeScale>0;
        public void CancelPendingShot(){accumulator=0;IsRunning=false;if(audioSource&&audioSource.clip==motorLoop){audioSource.Stop();audioSource.loop=false;}if(contactSparks)contactSparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(mechanicalVisual)mechanicalVisual.localPosition=visualBind;}
        void OnDisable()=>CancelPendingShot();
        void LateUpdate(){if(!CanRun)CancelPendingShot();if(mechanicalVisual&&IsRunning)mechanicalVisual.localPosition=visualBind+Vector3.up*(Mathf.Sin(Time.time*125)*.0015f);}
        public bool HandleShootInputs(bool down,bool held,bool up)
        {
            if(up||(!down&&!held)||!CanRun){CancelPendingShot();return false;}
            if(frame==Time.frameCount)return false;frame=Time.frameCount;
            IsRunning=true;if(motorLoop&&audioSource&&!audioSource.isPlaying){audioSource.clip=motorLoop;audioSource.loop=true;audioSource.volume=.22f;audioSource.Play();}
            accumulator+=Time.deltaTime;int ticks=Mathf.FloorToInt((accumulator+.000001f)/damageInterval);if(ticks==0)return false;accumulator-=ticks*damageInterval;
            float cost=ticks*damageInterval*energyPerSecond;if(module.Ammo.Remaining+.001f<cost){CancelPendingShot();return false;}
            Strike(ticks*damageInterval*damagePerSecond);TickCount+=ticks;TotalEnergySpent+=cost;weapon.OnShoot?.Invoke();module.Ammo.TryConsume(cost);return true;
        }
        void Strike(float amount)
        {
            if(!camera)return;var owner=weapon.Owner;var ownHealth=owner?owner.GetComponent<Health>():null;Vector3 origin=camera.transform.position,tip=origin+camera.transform.forward*reach;
            int count=Physics.OverlapCapsuleNonAlloc(origin,tip,contactRadius,candidates,-1,QueryTriggerInteraction.Ignore);if(count==candidates.Length)return;damaged.Clear();
            for(int i=0;i<count;i++)
            {
                var collider=candidates[i];if(owner&&collider.transform.IsChildOf(owner.transform))continue;
                var target=collider.GetComponent<Damageable>();if(!target)target=collider.GetComponentInParent<Damageable>();if(!target||!target.Health||target.Health==ownHealth||target.Health.CurrentHealth<=0||damaged.Contains(target.Health))continue;
                var point=collider.ClosestPoint(origin);var delta=point-origin;float distance=delta.magnitude;if(distance>reach+contactRadius||Vector3.Dot(delta,camera.transform.forward)<0)continue;
                int n=distance>.00001f?Physics.RaycastNonAlloc(origin,delta/distance,blockers,distance+.002f,-1,QueryTriggerInteraction.Ignore):0;if(n==blockers.Length)continue;bool blocked=false;
                for(int b=0;b<n;b++){var hit=blockers[b];if((owner&&hit.collider.transform.IsChildOf(owner.transform))||hit.collider==collider||hit.distance>=distance-.003f)continue;var other=hit.collider.GetComponent<Damageable>();if(!other)other=hit.collider.GetComponentInParent<Damageable>();if(other&&other.Health==target.Health)continue;blocked=true;break;}
                if(blocked)continue;damaged.Add(target.Health);target.InflictDamageAtPoint(amount,false,owner,point,distance>.00001f?-delta/distance:-camera.transform.forward);if(contactSparks){contactSparks.transform.position=point;contactSparks.Emit(2);}
            }
        }
    }
}
