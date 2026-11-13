using System;
using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    [RequireComponent(typeof(DamageArea))]
    public sealed class CannonExplosion : MonoBehaviour
    {
        [SerializeField,Min(0)] float damage=100;
        [SerializeField,Min(.1f)] float backupFuse=6f;
        [SerializeField] GameObject impactEffect;
        StolenCannon module;
        CannonOverdrive overdrive;
        DamageArea area;
        float deadline=float.PositiveInfinity;
        GameObject owner;
        public bool HasDetonated { get; private set; }
        public event Action Detonated;
        public float Damage => damage;
        public float BackupFuse => backupFuse;
        public GameObject PresentationPrefab => impactEffect;
        public Vector3 DetonationPosition { get; private set; }
        public void Configure(GameObject effect) => impactEffect=effect;
        void Awake(){module=GetComponent<StolenCannon>();overdrive=GetComponent<CannonOverdrive>();area=GetComponent<DamageArea>();}
        public void BeginThrow(GameObject thrower)
        { owner=thrower;if(overdrive.IsArmed)deadline=Mathf.Min(deadline,Time.time+backupFuse); }
        void Update()
        { if(overdrive.IsArmed && Time.timeScale>0 && Time.time>=deadline)TryDetonate(); }
        void OnCollisionEnter(Collision collision)
        { if(overdrive.IsArmed && module.State==StolenCannonState.Thrown && !collision.collider.isTrigger && !module.IsOwnerCollider(collision.collider))TryDetonate(); }
        public bool TryDetonate()
        {
            if(HasDetonated || !overdrive.IsArmed || module.State==StolenCannonState.Attached || Time.timeScale<=0)return false;
            // Commit before damage callbacks: a target's death cannot reenter detonation.
            module.SynchronizeEquippedPose();DetonationPosition=transform.position;
            if(module.Carrier)owner=module.Carrier.gameObject;
            HasDetonated=true;module.MarkDetonated();
            area.InflictDamageInArea(damage,transform.position,-1,QueryTriggerInteraction.Ignore,owner);
            if(impactEffect)Destroy(Instantiate(impactEffect,transform.position,Quaternion.identity),5);
            Detonated?.Invoke();Destroy(gameObject);return true;
        }
    }
}
