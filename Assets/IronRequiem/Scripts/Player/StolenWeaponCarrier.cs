using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem
{
    public enum CannonPickupStatus { None, Ready, TooFar, CoolingDown, InventoryFull, AlreadyHolding, Invalid }
    [RequireComponent(typeof(MechanicInput),typeof(PlayerWeaponsManager))]
    public sealed class StolenWeaponCarrier : MonoBehaviour
    {
        [SerializeField,Min(.5f)] float interactionRange=2.5f;
        [SerializeField,Min(0)] float throwSpeed=14;
        [Tooltip("Fallback seconds until the robot is punched back when no hands presentation provides the strike beat.")]
        [SerializeField,Min(0)] float shoveDelay=.26f;
        BigBotExecutionSequence bigBotSequence;
        ProtagonistHandsPresentation hands;
        public bool ExecutionInProgress=>(bigBotSequence&&bigBotSequence.IsRunning)||pendingTarget;
        // Successful rip: target and world grip point. Presentation only; inventory is already updated.
        public event System.Action<EnemyExecutionSource,Vector3> Executed;
        MechanicInput input;
        PlayerWeaponsManager manager;
        PlayerCharacterController player;
        Health health;
        WeaponController previousWeapon;
        WeaponController temporaryWeapon;
        bool releasing;
        bool quitting;
        readonly RaycastHit[] pickupHits = new RaycastHit[32];
        public StolenCannon Held { get; private set; }
        public StolenCannon Candidate { get; private set; }
        public EnemyExecutionSource ExecutionCandidate {get;private set;}
        public CannonPickupStatus ExecutionStatus {get;private set;}
        public bool CanUseHeldWeapon => input.CanAct && Held && manager.GetActiveWeapon()==temporaryWeapon;
        public float InteractionRange => interactionRange;
        public CannonPickupStatus PickupStatus { get; private set; }
        public Vector3 HeldWeaponPosition => temporaryWeapon && temporaryWeapon.IsWeaponActive
            ? temporaryWeapon.WeaponMuzzle.position
            : player.PlayerCamera.transform.TransformPoint(new Vector3(.25f,-.2f,.5f));
        void Awake()
        {
            input=GetComponent<MechanicInput>();manager=GetComponent<PlayerWeaponsManager>();player=GetComponent<PlayerCharacterController>();health=GetComponent<Health>();
            hands=GetComponent<ProtagonistHandsPresentation>();
            // Big Bot execution staging needs the scripted-motion extension; players without it keep the instant execution.
            if(GetComponent<AdvancedPlayerMovement>()){bigBotSequence=GetComponent<BigBotExecutionSequence>();if(!bigBotSequence)bigBotSequence=gameObject.AddComponent<BigBotExecutionSequence>();}
        }
        // Starts the hand rip/punch presentation for an execution whose inventory step already happened.
        public void NotifyRip(EnemyExecutionSource target,Vector3 grip)=>Executed?.Invoke(target,grip);
        void OnEnable(){if(health)health.OnDie+=OnDeath;}
        void OnDisable(){if(health)health.OnDie-=OnDeath;if(pendingTarget){pendingTarget.Complete();pendingTarget=null;}}
        void OnApplicationQuit()=>quitting=true;
        void OnDeath()=>Release(false);
        void Update()
        {
            Candidate=null; PickupStatus=CannonPickupStatus.None;ExecutionCandidate=null;ExecutionStatus=CannonPickupStatus.None;
            if(!input.CanAct)return;
            var closest=FindInteractionCollider();
            if(closest)
            {
                var module=closest.GetComponentInParent<StolenCannon>();
                if(module && (module.State==StolenCannonState.Available || module.State==StolenCannonState.Thrown))
                {Candidate=module;PickupStatus=GetPickupStatus(module);}
            }
            if(closest){var target=closest.GetComponentInParent<EnemyExecutionSource>();if(target&&target.Eligible){ExecutionCandidate=target;ExecutionStatus=GetExecutionStatus(target);}}
            if(input.InteractPressed){if(ExecutionCandidate)TryExecute(ExecutionCandidate);else if(!Held&&Candidate)TryPickup(Candidate);}
            if(Held && CanUseHeldWeapon)
            {
                if(input.ThrowPressed)Release(true);
            }
        }
        Collider FindInteractionCollider()
        {
            var camera=player.PlayerCamera.transform;
            int hitCount=Physics.RaycastNonAlloc(camera.position,camera.forward,pickupHits,interactionRange+1.5f,-1,QueryTriggerInteraction.Ignore);
            var hits=pickupHits;
            if(hitCount==pickupHits.Length){hits=Physics.RaycastAll(camera.position,camera.forward,interactionRange+1.5f,-1,QueryTriggerInteraction.Ignore);hitCount=hits.Length;}
            float nearest=float.PositiveInfinity; Collider closest=null;
            for(int i=0;i<hitCount;i++)
            {
                var hit=hits[i];
                if(hit.collider.transform.IsChildOf(transform) || hit.distance>=nearest)continue;
                nearest=hit.distance;closest=hit.collider;
            }
            return closest;
        }
        CannonPickupStatus CanReceive(WeaponController prefab)
        {
            if(Held)return CannonPickupStatus.AlreadyHolding;
            if(!prefab)return CannonPickupStatus.Invalid;
            if(manager.HasWeapon(prefab))return CannonPickupStatus.InventoryFull;
            for(int i=0;i<9;i++)if(!manager.GetWeaponAtSlotIndex(i))return CannonPickupStatus.Ready;
            return CannonPickupStatus.InventoryFull;
        }
        public CannonPickupStatus GetExecutionStatus(EnemyExecutionSource target)
        {
            if(!target||!target.Eligible)return CannonPickupStatus.Invalid;
            var closest=FindInteractionCollider();if(!closest||closest.GetComponentInParent<EnemyExecutionSource>()!=target)return CannonPickupStatus.Invalid;
            if(Vector3.Distance(closest.ClosestPoint(player.PlayerCamera.transform.position),player.PlayerCamera.transform.position)>interactionRange)return CannonPickupStatus.TooFar;
            return CanReceive(target.ModulePrefab.WeaponPrefab);
        }
        public bool TryExecute(EnemyExecutionSource target)
        {
            if(!input.CanAct||releasing||ExecutionInProgress||GetExecutionStatus(target)!=CannonPickupStatus.Ready||!target.TryReserve())return false;
            var camera=player.PlayerCamera.transform;var gripCollider=FindInteractionCollider();
            Vector3 grip=gripCollider?gripCollider.ClosestPoint(camera.position):target.transform.position+Vector3.up;
            var previous=manager.GetActiveWeapon();var prefab=target.ModulePrefab.WeaponPrefab;
            if(!manager.AddWeapon(prefab)){target.CancelReservation();return false;}
            var weapon=manager.HasWeapon(prefab);var module=target.CreateReservedModule();
            if(!module||!module.Equip(this,weapon)){if(module)Destroy(module.gameObject);manager.RemoveWeapon(weapon);target.CancelReservation();return false;}
            Held=module;previousWeapon=previous;temporaryWeapon=weapon;
            manager.SwitchToWeaponIndex(FindSlot(weapon),true);
            var protection=GetComponent<ExecutionProtection>();if(!protection)protection=gameObject.AddComponent<ExecutionProtection>();protection.Begin();
            // Big Bot: leap on top, wrench the cannon off and shoot it; the kill is delivered at the shot.
            if(bigBotSequence&&target.GetComponent<BigBotCombat>()&&bigBotSequence.Begin(target,grip))return true;
            // Wrench and punch: the weapon is already delivered; the robot reacts to the wrench and dies when the fist
            // lands, so both blows connect. The shove is scheduled for the same instant.
            float strike=hands?hands.DisarmStrikeTime:shoveDelay;
            ExecutionShove.Begin(target.gameObject,target.transform.position-transform.position,strike,transform);
            Executed?.Invoke(target,grip);
            pendingExecution=StartCoroutine(FinishExecution(target,grip,hands?hands.DisarmYankTime:0,strike));
            return true;
        }
        Coroutine pendingExecution;EnemyExecutionSource pendingTarget;
        public bool ExecutionPending=>pendingTarget;
        System.Collections.IEnumerator FinishExecution(EnemyExecutionSource target,Vector3 grip,float yank,float strike)
        {
            pendingTarget=target;
            var ai=target.GetComponent<Unity.FPS.AI.EnemyController>();var mobile=target.GetComponent<Unity.FPS.AI.EnemyMobile>();
            if(mobile)mobile.enabled=false;if(ai){ai.enabled=false;if(ai.NavMeshAgent&&ai.NavMeshAgent.isOnNavMesh)ai.NavMeshAgent.isStopped=true;}
            float start=Time.time;
            while(Time.time-start<yank&&target)yield return null;
            if(target)Blow(target,grip);
            while(Time.time-start<strike&&target)yield return null;
            if(target){Blow(target,target.transform.position+Vector3.up*1.2f);target.Complete();}
            pendingTarget=null;pendingExecution=null;
        }
        // Visual hit on the closest damageable: sparks, flash and reaction through the existing feedback (0.01 HP, the
        // minimum Damageable needs to raise its Impact event; the kill itself is Complete()).
        void Blow(EnemyExecutionSource target,Vector3 point)
        {
            Unity.FPS.Game.Damageable best=null;float distance=float.MaxValue;
            foreach(var d in target.GetComponentsInChildren<Unity.FPS.Game.Damageable>()){var c=d.GetComponent<Collider>();if(!c||!c.enabled)continue;float v=(c.ClosestPoint(point)-point).sqrMagnitude;if(v<distance){distance=v;best=d;}}
            if(!best)return;var col=best.GetComponent<Collider>();var at=col.ClosestPoint(point);
            if(best.Health&&best.Health.CurrentHealth>.05f)best.InflictDamageAtPoint(.01f,false,gameObject,at,(point-at).sqrMagnitude>1e-6f?(point-at).normalized:-target.transform.forward);
        }
        public bool TryPickup(StolenCannon module)
        {
            if(!input.CanAct || !module || module!=Candidate)return false;
            PickupStatus=GetPickupStatus(module);
            if(PickupStatus!=CannonPickupStatus.Ready)return false;
            var previous=manager.GetActiveWeapon();
            if(!manager.AddWeapon(module.WeaponPrefab))return false;
            var weapon=manager.HasWeapon(module.WeaponPrefab);
            if(!module.Equip(this,weapon)){manager.RemoveWeapon(weapon);return false;}
            Held=module;previousWeapon=previous;temporaryWeapon=weapon;
            manager.SwitchToWeaponIndex(FindSlot(weapon),true);return true;
        }
        public CannonPickupStatus GetPickupStatus(StolenCannon module)
        {
            if(Held)return CannonPickupStatus.AlreadyHolding;
            if(!module || !module.WeaponPrefab || !module.SolidCollider || module.Explosion.HasDetonated ||
                (module.State!=StolenCannonState.Available && module.State!=StolenCannonState.Thrown))return CannonPickupStatus.Invalid;
            Vector3 point=module.SolidCollider.ClosestPoint(player.PlayerCamera.transform.position);
            if((point-player.PlayerCamera.transform.position).sqrMagnitude>interactionRange*interactionRange)return CannonPickupStatus.TooFar;
            if(!module.CanPickup)return CannonPickupStatus.CoolingDown;
            if(manager.HasWeapon(module.WeaponPrefab))return CannonPickupStatus.InventoryFull;
            for(int i=0;i<9;i++)if(!manager.GetWeaponAtSlotIndex(i))return CannonPickupStatus.Ready;
            return CannonPickupStatus.InventoryFull;
        }
        public bool ThrowHeld()
        { return CanUseHeldWeapon && Release(true); }
        bool Release(bool launch)
        {
            if(releasing || !Held)return false;releasing=true;
            var module=Held;Held=null;
            Vector3 direction=player.PlayerCamera.transform.forward;
            Vector3 position=player.PlayerCamera.transform.position+direction*.6f;
            module.Release(position,direction,launch?throwSpeed:1.5f,gameObject);
            if(CampaignFlow.Owns(player))CampaignFlow.PlaceWorldModule(module.gameObject);
            if(temporaryWeapon)manager.RemoveWeapon(temporaryWeapon);
            temporaryWeapon=null;
            if(!player.IsDead && health.CurrentHealth>0)
            {
                int index=FindSlot(previousWeapon);
                if(index>=0)manager.SwitchToWeaponIndex(index,true);
                else manager.SwitchWeapon(true);
            }
            previousWeapon=null;releasing=false;return true;
        }
        public void OnModuleDestroyed(StolenCannon module)
        {
            if(releasing || Held!=module)return;
            Held=null;
            // Scene teardown destroys HUD listeners too; inventory children are already being removed.
            if(quitting || gameObject.scene!=SceneManager.GetActiveScene()&&!CampaignFlow.Owns(player))return;
            if(temporaryWeapon)manager.RemoveWeapon(temporaryWeapon);
            temporaryWeapon=null;
            if(!player.IsDead && health.CurrentHealth>0)
            {
                int index=FindSlot(previousWeapon);if(index>=0)manager.SwitchToWeaponIndex(index,true);
            }
            previousWeapon=null;
        }
        int FindSlot(WeaponController weapon)
        { if(!weapon)return -1;for(int i=0;i<9;i++)if(manager.GetWeaponAtSlotIndex(i)==weapon)return i;return -1; }
    }
}
