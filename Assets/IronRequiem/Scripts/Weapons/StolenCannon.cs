using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    [RequireComponent(typeof(CannonAmmo),typeof(CannonOverdrive),typeof(CannonExplosion))]
    public sealed class StolenCannon : MonoBehaviour
    {
        [SerializeField] WeaponController weaponPrefab;
        [SerializeField,Min(0)] float pickupDelay=.3f;
        Rigidbody body;
        Collider solid;
        Renderer[] renderers;
        readonly List<Collider> ignoredOwnerColliders=new List<Collider>();
        float pickupAfter,restoreCollisionAfter;
        bool initialized;
        Vector3 lastCarrierPosition;
        public StolenCannonState State { get; private set; }=StolenCannonState.Attached;
        public StolenWeaponCarrier Carrier { get; private set; }
        public WeaponController EquippedWeapon { get; private set; }
        public WeaponController WeaponPrefab => weaponPrefab;
        public CannonAmmo Ammo { get; private set; }
        public CannonOverdrive Overdrive { get; private set; }
        public CannonExplosion Explosion { get; private set; }
        public bool CanPickup => (State==StolenCannonState.Available || State==StolenCannonState.Thrown) && !Explosion.HasDetonated && Time.time>=pickupAfter;
        public Collider SolidCollider => solid;
        public Rigidbody Body => body;
        public void Configure(WeaponController prefab) => weaponPrefab=prefab;
        void Awake()
        {
            Ammo=GetComponent<CannonAmmo>();Overdrive=GetComponent<CannonOverdrive>();Explosion=GetComponent<CannonExplosion>();
            renderers=System.Array.FindAll(GetComponentsInChildren<Renderer>(true),r=>!(r is ParticleSystemRenderer));
        }
        public void MakeAvailable(Rigidbody rigidbody,Collider collider,bool immediate=false)
        {
            if(initialized){if(immediate&&State==StolenCannonState.Available)pickupAfter=Time.time;return;}initialized=true;body=rigidbody;solid=collider;
            State=StolenCannonState.Available;pickupAfter=Time.time+(immediate?0:pickupDelay);
        }
        public void FitPickupCollider(BoxCollider collider)
        {
            bool found=false;var bounds=new Bounds();
            foreach(var renderer in renderers)
            {
                if(!renderer)continue;
                var filter=renderer.GetComponent<MeshFilter>();if(!filter || !filter.sharedMesh)continue;
                var boundsInMesh=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    var corner=boundsInMesh.center+Vector3.Scale(boundsInMesh.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var local=transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if(!found){bounds=new Bounds(local,Vector3.zero);found=true;}else bounds.Encapsulate(local);
                }
            }
            if(found)
            {
                // Imported FBX roots can have scale 100: minimum thickness is in WORLD metres.
                var scale=transform.lossyScale;
                var minimum=new Vector3(.06f/Mathf.Max(.00001f,Mathf.Abs(scale.x)),.06f/Mathf.Max(.00001f,Mathf.Abs(scale.y)),.06f/Mathf.Max(.00001f,Mathf.Abs(scale.z)));
                collider.center=bounds.center;collider.size=Vector3.Max(bounds.size,minimum);
            }
        }
        public bool Equip(StolenWeaponCarrier carrier,WeaponController weapon)
        {
            if(!CanPickup)return false;
            State=StolenCannonState.Equipped;Carrier=carrier;EquippedWeapon=weapon;
            lastCarrierPosition=carrier.transform.position+Vector3.up;
            if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
            body.isKinematic=true;body.useGravity=false;solid.enabled=false;
            foreach(var renderer in renderers)if(renderer)renderer.enabled=false;
            // Keep the persistent world instance independent of player activation/destruction.
            // Its inventory representation follows the existing FPS weapon socket.
            Ammo.Bind(weapon);Overdrive.SetEquippedVisual(weapon);
            SynchronizeEquippedPose();
            return true;
        }
        public bool Release(Vector3 position,Vector3 direction,float speed,GameObject thrower)
        {
            if(State!=StolenCannonState.Equipped)return false;
            Ammo.Unbind();EquippedWeapon=null;Carrier=null;Overdrive.SetEquippedVisual(null);
            transform.SetParent(null,true);transform.position=position;
            foreach(var renderer in renderers)if(renderer)renderer.enabled=true;
            body.isKinematic=false;body.useGravity=true;solid.enabled=true;
            body.linearVelocity=direction.normalized*speed;body.angularVelocity=Vector3.right*4;body.WakeUp();
            State=Overdrive.IsArmed?StolenCannonState.Thrown:StolenCannonState.Available;
            pickupAfter=Time.time+pickupDelay;
            ignoredOwnerColliders.Clear();
            if(thrower)
                foreach(var collider in thrower.GetComponentsInChildren<Collider>())
                    if(collider!=solid){Physics.IgnoreCollision(solid,collider,true);ignoredOwnerColliders.Add(collider);}
            restoreCollisionAfter=Time.time+.35f;
            Explosion.BeginThrow(thrower);return true;
        }
        void Update()
        {
            if(State==StolenCannonState.Equipped)
            {
                if(Carrier)lastCarrierPosition=Carrier.transform.position+Vector3.up;
                else Release(lastCarrierPosition,Vector3.forward,1.5f,null);
            }
            if(ignoredOwnerColliders.Count==0 || Time.time<restoreCollisionAfter || State==StolenCannonState.Equipped)return;
            foreach(var collider in ignoredOwnerColliders)if(collider && solid)Physics.IgnoreCollision(solid,collider,false);
            ignoredOwnerColliders.Clear();
        }
        void LateUpdate() => SynchronizeEquippedPose();
        public void SynchronizeEquippedPose()
        {
            if(State!=StolenCannonState.Equipped || !Carrier)return;
            transform.position=Carrier.HeldWeaponPosition;
        }
        public bool IsOwnerCollider(Collider collider) => ignoredOwnerColliders.Contains(collider);
        public void MarkDetonated()
        {
            State=StolenCannonState.Detonated;if(solid)solid.enabled=false;
            Overdrive.StopEffects();Ammo.Unbind();
            if(Carrier)Carrier.OnModuleDestroyed(this);
            Carrier=null;EquippedWeapon=null;
        }
        void OnDestroy(){if(Carrier)Carrier.OnModuleDestroyed(this);}
    }
}
