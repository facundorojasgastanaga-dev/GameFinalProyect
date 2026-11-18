using System.Collections.Generic;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
namespace IronRequiem
{
    [DefaultExecutionOrder(80),RequireComponent(typeof(EnemyController),typeof(EnemyMobile),typeof(Health))]
    public sealed class SawBotCombat : MonoBehaviour,IEnemyMobilePolicy,IEnemyDeathPresentation
    {
        [Header("Saw attack")]
        [SerializeField,Min(0)] float damage=12;
        [SerializeField,Min(.3f)] float reach=1.55f;
        [SerializeField,Min(.15f)] float preparation=.45f;
        [SerializeField,Min(.15f)] float recovery=.75f;
        [SerializeField,Range(30,300)] float turnSpeed=180;
        [SerializeField] Animator animator;
        [SerializeField] Transform attackOrigin,bodyJoint,leftWheel,rightWheel,leftChain,rightChain;
        [SerializeField,Min(.01f)] float wheelRadius=.29f;
        [SerializeField] GameObject deathEffect;
        [SerializeField] ParticleSystem impactSparks;
        [SerializeField] AudioClip impactSound;
        [SerializeField] Transform healthBar;
        public void SetHealthBar(Transform pivot)=>healthBar=pivot;
        EnemyController enemy;EnemyMobile mobile;Health health;NavMeshAgent agent;
        readonly RaycastHit[] hits=new RaycastHit[64];readonly List<Collider> targetColliders=new List<Collider>(8);
        Vector3 lastSeen,previousPosition,leftChainBind,rightChainBind;Quaternion leftBind,rightBind;float previousYaw,leftAngle,rightAngle,wheelAxisSign,started,timeout,nextAttack,deathAt,nextReaction;bool dead,attacking,struck,burst;Transform coreIndicator;
        public float Damage=>damage;public float Reach=>reach;public float Preparation=>preparation;public float Recovery=>recovery;
        public bool IsAttacking=>attacking;public bool IsDead=>dead;public int AttackCount{get;private set;}public int HitCount{get;private set;}public int DeathCount{get;private set;}
        public Animator Animator=>animator;public Transform LeftWheel=>leftWheel;public Transform RightWheel=>rightWheel;
        GameObject Target=>enemy?enemy.KnownDetectedTarget:null;
        Actor TargetActor=>Target?Target.GetComponentInParent<Actor>():null;
        bool Visible {get{var actor=TargetActor;if(dead||!enemy||!enemy.IsSeeingTarget||!actor)return false;var hp=actor.GetComponent<Health>();return (!hp||hp.CurrentHealth>0)&&Clear(attackOrigin.position,actor.AimPoint.position,actor);}}
        public bool CanAttack=>sustainedSaw?SustainedCanAttack():Visible&&Vector3.Distance(attackOrigin.position,AimPosition)<=reach&&Vector3.Angle(transform.forward,Vector3.ProjectOnPlane(AimPosition-transform.position,Vector3.up))<40;
        public Vector3 PursuitDestination=>Visible?Target.transform.position:lastSeen;
        public Vector3 AimPosition=>Visible?Target.transform.position:lastSeen;
        public void Configure(Animator a,Transform origin,Transform body,Transform lw,Transform rw,Transform lc,Transform rc,GameObject effect,ParticleSystem sparks,AudioClip sound){animator=a;attackOrigin=origin;bodyJoint=body;leftWheel=lw;rightWheel=rw;leftChain=lc;rightChain=rc;deathEffect=effect;impactSparks=sparks;impactSound=sound;}
        [Header("Sustained saw variant")]
        [SerializeField] bool sustainedSaw;
        [Header("Death presentation")]
        [SerializeField] string contactDeathState="";
        [SerializeField] string disarmedDeathState="";
        [SerializeField,Min(0)] float deathBurstDelay=.85f;
        bool executionHeld;
        public void SetExecutionReserved(bool value){executionHeld=value;if(value){CancelAttack();if(agent&&agent.enabled&&agent.isOnNavMesh){agent.isStopped=true;agent.velocity=Vector3.zero;}}}
        [SerializeField, Min(.1f)] float moveSpeed=7.2f;
        [SerializeField, Min(.1f)] float acceleration=24;
        [SerializeField, Min(.1f)] float exitReach=1.4f;
        [SerializeField, Min(0)] float damagePerSecond=20;
        [SerializeField, Range(.02f,.5f)] float damageInterval=.1f;
        [SerializeField, Min(.01f)] float contactRadius=.16f;
        [SerializeField, Range(30,300)] float attackTurnSpeed=140;
        [SerializeField] Transform leftBladeStart,leftBladeEnd,rightBladeStart,rightBladeEnd;
        readonly Collider[] contacts=new Collider[32];
        Vector3 previousLeft,previousRight;Quaternion previousRotation;
        float contactAccumulator,nextContactSound;
        AudioSource sawAudio;
        public bool SustainedSaw=>sustainedSaw;
        public float DamagePerSecond=>damagePerSecond;
        public float DamageInterval=>damageInterval;
        public float MovementSpeed=>moveSpeed;
        // 12/11/2026: neutral activities (hangar scavenging) walk slower than the combat drive speed.
        public float SpeedScale {get;set;}=1;
        public float ContactSeconds{get;private set;}
        public float ContactDamage{get;private set;}
        public void ConfigureSustained(Transform ls,Transform le,Transform rs,Transform re,float radius)
        {sustainedSaw=true;leftBladeStart=ls;leftBladeEnd=le;rightBladeStart=rs;rightBladeEnd=re;wheelRadius=radius;reach=1.1f;exitReach=1.4f;preparation=.25f;turnSpeed=300;}
        void Start()
        {
            if(!sustainedSaw)return;
            agent.updateRotation=false;agent.acceleration=acceleration;agent.angularSpeed=turnSpeed;agent.speed=moveSpeed;agent.stoppingDistance=.55f;
            var navigation=GetComponentInChildren<NavigationModule>();if(navigation){navigation.MoveSpeed=moveSpeed;navigation.Acceleration=acceleration;navigation.AngularSpeed=turnSpeed;}
            previousLeft=leftWheel.position;previousRight=rightWheel.position;previousRotation=transform.rotation;sawAudio=GetComponent<AudioSource>();
        }
        bool SustainedCanAttack()
        {
            if(!Visible)return false;
            var delta=Vector3.ProjectOnPlane(Target.transform.position-transform.position,Vector3.up);
            return delta.magnitude<=(attacking?exitReach:reach)&&Vector3.Angle(transform.forward,delta)<(attacking?100:40);
        }
        void TickSustainedAttack()
        {
            if(Time.deltaTime<=0)return;
            if(attacking&&!SustainedCanAttack()){CancelAttack();return;}
            if(!attacking){if(agent.enabled&&agent.isOnNavMesh&&agent.isStopped)agent.isStopped=false;return;}
            if(agent.enabled&&agent.isOnNavMesh){agent.isStopped=true;agent.velocity=Vector3.zero;}
            if(Time.time<started+preparation){contactAccumulator=0;return;}
            if(!SawContact(out var hurt,out var point)){contactAccumulator=0;return;}
            float step=Mathf.Min(Time.deltaTime,Mathf.Max(0,Time.time-started-preparation));
            ContactSeconds+=step;contactAccumulator+=step;
            int ticks=Mathf.FloorToInt((contactAccumulator+.000001f)/damageInterval);if(ticks<=0)return;
            contactAccumulator-=ticks*damageInterval;
            float before=hurt.Health.CurrentHealth;
            hurt.InflictDamageAtPoint(damagePerSecond*damageInterval*ticks,false,gameObject,point,(attackOrigin.position-point).normalized);
            float applied=before-hurt.Health.CurrentHealth;if(applied<=0)return;
            HitCount+=ticks;ContactDamage+=applied;
            if(impactSparks){impactSparks.transform.position=point;impactSparks.Emit(Mathf.Min(4,ticks*2));}
            if(impactSound&&sawAudio&&Time.time>=nextContactSound){nextContactSound=Time.time+.3f;sawAudio.PlayOneShot(impactSound,.35f);}
        }
        bool SawContact(out Damageable hurt,out Vector3 point)
        {
            hurt=null;point=Vector3.zero;if(!Visible)return false;var actor=TargetActor;
            for(int side=0;side<2;side++)
            {
                var a=side==0?leftBladeStart:rightBladeStart;var b=side==0?leftBladeEnd:rightBladeEnd;if(!a||!b)continue;
                int count=Physics.OverlapCapsuleNonAlloc(a.position,b.position,contactRadius,contacts,-1,QueryTriggerInteraction.Ignore);
                if(count==contacts.Length)return false;
                for(int i=0;i<count;i++)
                {
                    var collider=contacts[i];if(collider.GetComponentInParent<Actor>()!=actor)continue;
                    var damageable=collider.GetComponent<Damageable>()??collider.GetComponentInParent<Damageable>();
                    if(!damageable||!damageable.Health||damageable.Health==health||damageable.Health.CurrentHealth<=0)continue;
                    var center=(a.position+b.position)*.5f;var closest=collider.ClosestPoint(center);
                    if(!Clear(center,closest,actor)||!Clear(attackOrigin.position,actor.AimPoint.position,actor))continue;
                    hurt=damageable;point=closest;return true;
                }
            }
            return false;
        }
        void SustainedWheels()
        {
            Vector3 left=leftWheel.position,right=rightWheel.position;
            var forward=Quaternion.Slerp(previousRotation,transform.rotation,.5f)*Vector3.forward;
            if(!dead&&Time.deltaTime>0)
            {
                leftAngle=Mathf.Repeat(leftAngle+Vector3.Dot(left-previousLeft,forward)/wheelRadius*Mathf.Rad2Deg,360);
                rightAngle=Mathf.Repeat(rightAngle+Vector3.Dot(right-previousRight,forward)/wheelRadius*Mathf.Rad2Deg,360);
                leftWheel.localRotation=leftBind*Quaternion.AngleAxis(leftAngle*wheelAxisSign,Vector3.right);
                rightWheel.localRotation=rightBind*Quaternion.AngleAxis(rightAngle*wheelAxisSign,Vector3.right);
            }
            previousLeft=left;previousRight=right;previousRotation=transform.rotation;
            float amplitude=dead?0:attacking?1:.2f;
            leftChain.localPosition=leftChainBind+Vector3.up*(Mathf.Sin(Time.time*130)*.0008f*amplitude);
            rightChain.localPosition=rightChainBind+Vector3.up*(Mathf.Sin(Time.time*130+2)*.0008f*amplitude);
        }
        void Awake()
        {
            enemy=GetComponent<EnemyController>();mobile=GetComponent<EnemyMobile>();health=GetComponent<Health>();agent=GetComponent<NavMeshAgent>();previousPosition=transform.position;previousYaw=transform.eulerAngles.y;lastSeen=transform.position;coreIndicator=bodyJoint.Find("CoreIndicator");
            leftBind=leftWheel.localRotation;rightBind=rightWheel.localRotation;leftChainBind=leftChain.localPosition;rightChainBind=rightChain.localPosition;wheelAxisSign=Mathf.Sign(Vector3.Dot(leftWheel.parent.TransformDirection(Vector3.right),transform.right));RobotDestructionPool.Prepare(deathEffect,gameObject.scene);
        }
        void OnEnable(){if(health)health.OnDamaged+=Damaged;}
        void OnDisable(){if(health)health.OnDamaged-=Damaged;CancelAttack();}
        void Update()
        {
            if(dead){if(coreIndicator&&Time.time-deathAt>.25f)coreIndicator.gameObject.SetActive(false);if(!burst&&Time.time-deathAt>deathBurstDelay){burst=true;RobotDestructionPool.Burst(deathEffect,null,transform.position+Vector3.up*.7f,null,.7f);}return;}
            if(executionHeld)return;
            if(Visible)lastSeen=Target.transform.position;
            if(sustainedSaw){TickSustainedAttack();return;}
            if(attacking&&(!Visible||Vector3.Distance(attackOrigin.position,AimPosition)>reach+.25f||Time.time>timeout))CancelAttack();
        }
        public void OrientTowards(Vector3 target)
        {
            if(sustainedSaw&&!attacking&&agent.enabled&&agent.hasPath)target=agent.steeringTarget;
            var direction=Vector3.ProjectOnPlane(target-transform.position,Vector3.up);if(direction.sqrMagnitude<.001f)return;
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),(sustainedSaw?(attacking?attackTurnSpeed:turnSpeed):turnSpeed*(attacking?.4f:1))*Time.deltaTime);
        }
        public void UpdateMotion(float speed)
        {
            if(dead)return;
            if(sustainedSaw){if(mobile.AiState==EnemyMobile.AIState.Patrol&&agent.enabled&&agent.hasPath)OrientTowards(agent.steeringTarget);animator.SetFloat("MoveSpeed",speed,.08f,Time.deltaTime);animator.SetFloat("DriveSpeed",Mathf.Clamp(speed/3f,.15f,3));if(!attacking&&agent.enabled){var heading=Vector3.ProjectOnPlane(agent.steeringTarget-transform.position,Vector3.up);agent.speed=moveSpeed*SpeedScale*Mathf.Clamp01((Vector3.Dot(transform.forward,heading.normalized)+.05f)/.85f);}return;}
            animator.SetFloat("MoveSpeed",speed);if(mobile.AiState==EnemyMobile.AIState.Patrol&&agent.velocity.sqrMagnitude>.01f)OrientTowards(transform.position+agent.velocity);
        }
        public void RequestAttack()
        {
            if(dead||executionHeld||attacking||Time.time<nextAttack||!CanAttack)return;
            if(sustainedSaw){attacking=true;struck=false;AttackCount++;started=Time.time;contactAccumulator=0;animator.SetFloat("AttackSpeed",.25f/preparation);animator.ResetTrigger("CancelAttack");animator.SetTrigger("Attack");return;}
            attacking=true;struck=false;AttackCount++;started=Time.time;float speed=.6f/(preparation+.15f);timeout=Time.time+1.15f/speed+.3f;
            animator.SetFloat("AttackSpeed",speed);animator.ResetTrigger("CancelAttack");animator.SetTrigger("Attack");
        }
        public void Impact()
        {
            if(sustainedSaw)return;
            if(dead||!attacking||struck)return;struck=true;nextAttack=Time.time+recovery;if(!CanAttack)return;
            var actor=TargetActor;actor.GetComponentsInChildren(false,targetColliders);Collider nearest=null;float closest=float.PositiveInfinity;Vector3 point=Vector3.zero;
            foreach(var c in targetColliders){if(!c||!c.enabled||c.isTrigger)continue;var p=c.ClosestPoint(attackOrigin.position);float distance=Vector3.Distance(attackOrigin.position,p);if(distance<closest){nearest=c;closest=distance;point=p;}}
            if(!nearest||closest>reach||!Clear(attackOrigin.position,point,actor))return;
            var hurt=nearest.GetComponent<Damageable>();if(!hurt)hurt=nearest.GetComponentInParent<Damageable>();if(!hurt||!hurt.Health||hurt.Health==health)return;
            float before=hurt.Health.CurrentHealth;hurt.InflictDamageAtPoint(damage,false,gameObject,point,(attackOrigin.position-point).normalized);
            if(hurt.Health.CurrentHealth<before)
            {HitCount++;if(impactSparks){impactSparks.transform.position=point;impactSparks.Emit(5);}if(impactSound)AudioUtility.CreateSFX(impactSound,point,AudioUtility.AudioGroups.EnemyAttack,1);}
        }
        public void Recover(){if(sustainedSaw||!attacking)return;attacking=false;nextAttack=Mathf.Max(nextAttack,Time.time+.12f);}
        void CancelAttack(){contactAccumulator=0;if(sustainedSaw&&agent&&agent.enabled&&agent.isOnNavMesh&&!dead)agent.isStopped=false;if(!attacking)return;attacking=false;struck=true;nextAttack=Mathf.Max(nextAttack,Time.time+.25f);if(animator){animator.ResetTrigger("Attack");animator.SetTrigger("CancelAttack");}if(impactSparks)impactSparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        bool Clear(Vector3 from,Vector3 to,Actor accepted)
        {
            var delta=to-from;if(delta.sqrMagnitude<.00001f)return true;int n=Physics.RaycastNonAlloc(from,delta.normalized,hits,delta.magnitude,-1,QueryTriggerInteraction.Ignore);if(n==hits.Length)return false;
            for(int i=0;i<n;i++){var c=hits[i].collider;if(c.transform.IsChildOf(transform)||c.GetComponentInParent<Actor>()==accepted)continue;return false;}return true;
        }
        void Damaged(float amount,GameObject source){if(dead||health.CurrentHealth<=0||Time.time<nextReaction)return;nextReaction=Time.time+.18f;animator.SetTrigger("OnDamaged");}
        public bool TryPlayDeathPresentation()
        {
            if(dead)return true;bool fromContact=attacking;dead=true;DeathCount++;deathAt=Time.time;CancelAttack();mobile.enabled=false;enemy.enabled=false;if(agent.enabled&&agent.isOnNavMesh){agent.isStopped=true;agent.ResetPath();agent.velocity=Vector3.zero;}agent.enabled=false;
            animator.ResetTrigger("Attack");animator.ResetTrigger("OnDamaged");if(animator.layerCount>1)animator.SetLayerWeight(1,0);if(sustainedSaw)animator.CrossFadeInFixedTime(executionHeld&&!string.IsNullOrEmpty(disarmedDeathState)?disarmedDeathState:fromContact&&!string.IsNullOrEmpty(contactDeathState)?contactDeathState:"Death",.12f,0);else animator.Play("Death",0,0);
            foreach(var c in GetComponentsInChildren<Collider>())c.enabled=false;foreach(var p in GetComponentsInChildren<ParticleSystem>(true))p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);foreach(var s in GetComponentsInChildren<AudioSource>())s.Stop();
            if(healthBar)healthBar.gameObject.SetActive(false);return true;
        }
        void LateUpdate()
        {
            if(sustainedSaw){SustainedWheels();return;}
            var delta=transform.position-previousPosition;float travel=Vector3.Dot(delta,transform.forward);float yaw=Mathf.DeltaAngle(previousYaw,transform.eulerAngles.y)*Mathf.Deg2Rad;previousPosition=transform.position;previousYaw=transform.eulerAngles.y;
            if(!dead){leftAngle+=(travel-yaw*.4f)/wheelRadius*Mathf.Rad2Deg;rightAngle+=(travel+yaw*.4f)/wheelRadius*Mathf.Rad2Deg;}
            leftWheel.localRotation=leftBind*Quaternion.AngleAxis(leftAngle*wheelAxisSign,Vector3.right);rightWheel.localRotation=rightBind*Quaternion.AngleAxis(rightAngle*wheelAxisSign,Vector3.right);
            float amplitude=dead?Mathf.Max(0,1-(Time.time-deathAt)*4):attacking?1:.2f;leftChain.localPosition=leftChainBind+Vector3.up*(Mathf.Sin(Time.time*130)*.0008f*amplitude);rightChain.localPosition=rightChainBind+Vector3.up*(Mathf.Sin(Time.time*130+2)*.0008f*amplitude);
        }
    }
}