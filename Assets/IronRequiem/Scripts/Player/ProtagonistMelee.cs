using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
namespace IronRequiem
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(MechanicInput),typeof(Health))]
    public sealed class ProtagonistMelee : MonoBehaviour
    {
        [SerializeField,Min(.1f)] float reach=1.55f;
        [SerializeField,Min(.01f)] float radius=.18f;
        [SerializeField,Min(0)] float damage=25;
        [SerializeField,Min(.01f)] float preparation=.12f,extension=.10f,impactHold=.04f,recovery=.30f;
        [SerializeField] LayerMask hitLayers=-1;
        MechanicInput input;Health health;Camera aimCamera;
        float elapsed;bool struck;
        readonly Collider[] candidates=new Collider[64];readonly RaycastHit[] blockers=new RaycastHit[64];
        readonly HashSet<GameObject> damaged=new HashSet<GameObject>();
        public bool IsAttacking {get;private set;}
        public int AttackSerial {get;private set;}
        public int LastHitCount {get;private set;}
        public float Reach=>reach;public float Damage=>damage;public float Recovery=>recovery;
        [Header("Feel")]
        [Tooltip("Extra freeze on the impact pose when the punch connects (seconds).")]
        [SerializeField,Range(0,.06f)] float hitStop=.03f;
        [Tooltip("Camera kick at wind-up, at a connecting impact and at a whiff (approximate peak degrees: pitch, yaw, roll).")]
        [SerializeField] Vector3 windUpKick=new Vector3(-1.1f,-.6f,1.4f);
        [SerializeField] Vector3 impactKick=new Vector3(2.6f,.9f,-2.4f);
        [SerializeField] Vector3 whiffKick=new Vector3(.9f,.5f,-1.1f);
        [Header("Charged punch (fist weapon M1, v4)")]
        [Tooltip("Seconds of holding M1 to reach a full charge.")]
        [SerializeField,Range(1f,1.5f)] float maxChargeSeconds=1.2f;
        [Tooltip("Releasing before this many seconds is a quick punch (same as F).")]
        [SerializeField,Range(.08f,.4f)] float tapThreshold=.18f;
        [SerializeField,Min(0)] float heavyDamage=95;
        [SerializeField,Min(.1f)] float heavyReach=2.1f;
        [SerializeField,Min(.01f)] float heavyRadius=.32f;
        [Tooltip("Metres the target is pushed at full charge (scaled by charge, reduced for big robots).")]
        [SerializeField,Min(0)] float heavyPush=3f;
        [Tooltip("Metres the player lunges forward at full charge.")]
        [SerializeField,Min(0)] float heavyLunge=1.1f;
        [Tooltip("Seconds the enemy cannot move or shoot at full charge.")]
        [SerializeField,Min(0)] float heavyStagger=.75f;
        [SerializeField,Range(0,.15f)] float heavyHitStop=.07f;
        [SerializeField] bool fullChargeSlowMotion=true;
        [SerializeField,Range(.02f,.3f)] float slowMotionSeconds=.12f;
        [SerializeField,Range(.1f,1f)] float slowMotionScale=.35f;
        [SerializeField] Vector3 heavyImpactKick=new Vector3(5.2f,1.5f,-4.2f);
        [SerializeField] GameObject heavyImpactFx;
        [SerializeField] AudioSource sfx;
        [SerializeField] AudioClip quickImpactClip,heavyImpactClip;
        float chargeElapsed;bool fullAnnounced;
        public bool IsCharging {get;private set;}
        public float ChargeLevel=>IsCharging?Mathf.Clamp01(chargeElapsed/maxChargeSeconds):0;
        public bool FullyCharged=>IsCharging&&chargeElapsed>=maxChargeSeconds;
        public float MaxChargeSeconds=>maxChargeSeconds;
        public float TapThreshold=>tapThreshold;
        public float HeavyDamage=>heavyDamage;
        // 0 = quick punch, 1 = full heavy punch. Valid for the current attack.
        public float Power {get;private set;}
        public bool LastWasHeavy {get;private set;}
        public bool LastWasFull {get;private set;}
        public int HeavySerial {get;private set;}
        public event System.Action ChargeStarted,ChargeFull,ChargeEnded;
        public event System.Action<Vector3,float,bool> HeavyImpact; // point, power, full charge
        float Hold=>impactHold+(LastHitCount>0?(Power>0?heavyHitStop:hitStop):0);
        float CurReach=>Mathf.Lerp(reach,heavyReach,Power);
        float CurRadius=>Mathf.Lerp(radius,heavyRadius,Power);
        AdvancedPlayerMovement movement;
        Unity.FPS.Gameplay.PlayerInputHandler fpsInput;Unity.FPS.Gameplay.PlayerWeaponsManager weapons;
        // v5: the strike is thrown by the right hand; with a weapon on the right forearm it is a mechanical bash.
        public bool ArmedStrike {get;private set;}
        public bool Unarmed=>!weapons||!weapons.GetActiveWeapon();
        void StartAttack()
        {
            IsAttacking=true;elapsed=0;struck=false;LastHitCount=0;AttackSerial++;damaged.Clear();Power=0;LastWasHeavy=false;LastWasFull=false;ArmedStrike=!Unarmed;
            DistanceCameraImpulse.Kick(aimCamera,windUpKick,new Vector3(0,.004f,-.012f));
        }
        // Fist weapon: M1 pressed. Starts the wind-up and holds the chambered pose while charging.
        public bool BeginCharge()
        {
            if(IsAttacking||!input.CanAct)return false;
            StartAttack();IsCharging=true;chargeElapsed=0;fullAnnounced=false;ChargeStarted?.Invoke();return true;
        }
        // Fist weapon: M1 released. Tap = quick punch, hold = heavy punch scaled by charge.
        public void ReleaseCharge()
        {
            if(!IsCharging)return;
            IsCharging=false;
            if(chargeElapsed>=tapThreshold)
            {
                Power=Mathf.Clamp(chargeElapsed/maxChargeSeconds,.15f,1f);LastWasHeavy=true;LastWasFull=chargeElapsed>=maxChargeSeconds;HeavySerial++;
                if(elapsed<preparation)elapsed=preparation;
                if(movement&&heavyLunge>0)movement.BeginLunge(aimCamera.transform.forward,heavyLunge*Power,extension+impactHold+.04f);
            }
            ChargeEnded?.Invoke();
        }
        public void CancelCharge(){if(!IsCharging)return;IsCharging=false;Cancel();ChargeEnded?.Invoke();}
        // Clip timeline (seconds) is unchanged: 0-.12 wind-up, .12-.22 extension, .22-.26 contact, .26-.56 recovery.
        // Only the speed through each segment is shaped: a slow-in wind-up, an accelerating strike that is
        // fastest at contact, a held contact pose and an ease-out return.
        public float PoseTime
        {
            get
            {
                if(!IsAttacking)return 0;
                if(elapsed<preparation)return EaseInOut(elapsed/preparation)*.12f;
                if(elapsed<preparation+extension)return .12f+EaseIn(Mathf.Clamp01((elapsed-preparation)/extension))*.10f;
                float hold=Hold;
                if(elapsed<preparation+extension+hold)return .22f+EaseOut(Mathf.Clamp01((elapsed-preparation-extension)/hold))*.04f;
                return .26f+EaseInOut(Mathf.Clamp01((elapsed-preparation-extension-hold)/recovery))*.30f;
            }
        }
        static float EaseIn(float t)=>t*t*t;
        static float EaseOut(float t)=>1-(1-t)*(1-t);
        static float EaseInOut(float t)=>t*t*(3-2*t);
        void Awake(){input=GetComponent<MechanicInput>();health=GetComponent<Health>();movement=GetComponent<AdvancedPlayerMovement>();fpsInput=GetComponent<Unity.FPS.Gameplay.PlayerInputHandler>();weapons=GetComponent<Unity.FPS.Gameplay.PlayerWeaponsManager>();aimCamera=GetComponent<Unity.FPS.Gameplay.PlayerCharacterController>().PlayerCamera;}
        void OnEnable(){if(health)health.OnDie+=Cancel;}
        void OnDisable(){if(health)health.OnDie-=Cancel;Cancel();}
        void Update()
        {
            if(!input.CanAct){if(IsCharging)CancelCharge();else Cancel();return;}
            // Separate explicit bindings: E collects, F punches. Simultaneous input favours pickup.
            // v5: F tap = quick strike, F held = charge (released on button up). Unarmed, M1 also throws the quick strike.
            if(!IsAttacking&&!input.InteractPressed)
            {
                if(input.MeleePressed)BeginCharge();
                else if(Unarmed&&fpsInput&&fpsInput.GetFireInputDown())StartAttack();
            }
            if(IsCharging&&!input.MeleeHeld)ReleaseCharge();
            if(!IsAttacking)return;
            if(IsCharging)
            {
                // Wind-up plays once, then the chambered pose is held (presentation adds the vibration).
                chargeElapsed+=Time.deltaTime;elapsed=Mathf.Min(elapsed+Time.deltaTime,preparation);
                if(!fullAnnounced&&chargeElapsed>=maxChargeSeconds){fullAnnounced=true;ChargeFull?.Invoke();}
                return;
            }
            elapsed+=Time.deltaTime;
            if(!struck&&elapsed>=preparation+extension)
            {
                struck=true;Strike();
                bool connected=LastHitCount>0;
                var kick=connected?(Power>0?Vector3.Lerp(impactKick,heavyImpactKick,Power):impactKick):whiffKick;
                DistanceCameraImpulse.Kick(aimCamera,kick,new Vector3(0,-.006f,connected?.028f+.03f*Power:.014f));
                if(connected)
                {
                    if(sfx){var clip=Power>0&&heavyImpactClip?heavyImpactClip:quickImpactClip;if(clip){sfx.pitch=Power>0?Random.Range(.86f,.95f):Random.Range(.97f,1.06f);sfx.PlayOneShot(clip,Power>0?1f:.75f);}}
                    if(Power>0)
                    {
                        if(heavyImpactFx)FxPool.Spawn(heavyImpactFx,lastHitPoint,Quaternion.LookRotation(-aimCamera.transform.forward),1.6f);
                        if(LastWasFull&&fullChargeSlowMotion)GameFeel.HitStopThenSlow(heavyHitStop,.05f,slowMotionSeconds,slowMotionScale);
                        else GameFeel.HitStop(heavyHitStop*Power,.05f);
                        HeavyImpact?.Invoke(lastHitPoint,Power,LastWasFull);
                    }
                }
            }
            if(elapsed>=preparation+extension+Hold+recovery)IsAttacking=false;
        }
        public void Cancel(){IsAttacking=false;IsCharging=false;elapsed=0;struck=true;damaged.Clear();}
        Vector3 lastHitPoint;
        bool Own(Collider c)=>c.transform.IsChildOf(transform);
        static GameObject Identity(Damageable target){var actor=target.GetComponentInParent<Actor>();return actor?actor.gameObject:target.Health.gameObject;}
        void Strike()
        {
            Physics.SyncTransforms(); // One query at impact must see current navigation/kinematic poses.
            float reach=CurReach,radius=CurRadius,damage=Mathf.Lerp(this.damage,heavyDamage,Power);
            Vector3 origin=aimCamera.transform.position,tip=origin+aimCamera.transform.forward*reach;
            int count=Physics.OverlapCapsuleNonAlloc(origin,tip,radius,candidates,hitLayers,QueryTriggerInteraction.Ignore);
            Collider[] hits=candidates;if(count==candidates.Length){hits=Physics.OverlapCapsule(origin,tip,radius,hitLayers,QueryTriggerInteraction.Ignore);count=hits.Length;}
            for(int i=0;i<count;i++)
            {
                var collider=hits[i];if(!collider||Own(collider))continue;
                var target=collider.GetComponent<Damageable>();if(!target)target=collider.GetComponentInParent<Damageable>();
                if(!target||!target.Health||target.Health==health)continue;var identity=Identity(target);if(damaged.Contains(identity))continue;
                Vector3 point=collider.ClosestPoint(origin);Vector3 delta=point-origin;
                float distance=delta.magnitude;if(distance>reach+radius)continue;
                int blockedCount=distance>.00001f?Physics.RaycastNonAlloc(origin,delta/distance,blockers,distance+.002f,hitLayers,QueryTriggerInteraction.Ignore):0;RaycastHit[] cast=blockers;
                if(blockedCount==blockers.Length){cast=Physics.RaycastAll(origin,delta/distance,distance+.002f,hitLayers,QueryTriggerInteraction.Ignore);blockedCount=cast.Length;}
                bool occluded=false;
                for(int j=0;j<blockedCount;j++)
                {
                    var hit=cast[j];if(Own(hit.collider)||hit.collider==collider||hit.distance>=distance-.003f)continue;
                    var other=hit.collider.GetComponentInParent<Damageable>();if(other&&other.Health&&Identity(other)==identity)continue;
                    occluded=true;break;
                }
                if(occluded)continue;damaged.Add(identity);LastHitCount++;lastHitPoint=point;target.InflictDamageAtPoint(damage,false,gameObject,point,distance>.00001f?-delta/distance:-aimCamera.transform.forward);
                if(Power>0)HeavyReaction(identity);
            }
        }
        // Heavy punch: physical push (ground-safe ExecutionShove) and a short stagger on the enemy AI.
        void HeavyReaction(GameObject identity)
        {
            if(!identity)return;
            var enemyHealth=identity.GetComponent<Health>();if(enemyHealth&&enemyHealth.CurrentHealth<=0)return;
            var enemy=identity.GetComponent<Unity.FPS.AI.EnemyController>();
            if(enemy)enemy.Stagger(heavyStagger*Power);
            var agent=identity.GetComponent<UnityEngine.AI.NavMeshAgent>();
            float size=agent?Mathf.Clamp01(.75f/Mathf.Max(.2f,agent.radius)):1f;
            if(heavyPush>0&&size>.05f)ExecutionShove.Begin(identity,aimCamera.transform.forward,0,transform,heavyPush*Power*size,.22f+.14f*Power);
        }
    }
}
