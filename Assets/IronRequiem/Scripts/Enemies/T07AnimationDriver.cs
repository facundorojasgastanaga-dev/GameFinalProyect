using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace IronRequiem
{
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(EnemyController))]
    public sealed class T07AnimationDriver : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] DetachableCannon cannon;
        [SerializeField] Rig footRig;
        [SerializeField, Min(.5f)] float strideDistance = 1.5f;
        EnemyController enemy;
        EnemyMobile mobile;
        Health health;
        DisarmedEnemyRetreat retreat;
        Vector3 previousPosition;
        float previousYaw, phase;
        bool subscribed;
        public bool IsDead { get; private set; }
        public float Phase => phase;
        public float StrideDistance => strideDistance;
        public float Speed { get; private set; }
        public float TurnRate { get; private set; }
        public Animator Animator => animator;
        static readonly int Cycle=Animator.StringToHash("Cycle"), SpeedId=Animator.StringToHash("MoveSpeed"),
            Turn=Animator.StringToHash("Turn"), Disarmed=Animator.StringToHash("Disarmed"), Dead=Animator.StringToHash("Dead");
        public void Configure(Animator target,DetachableCannon joint,Rig feet)
        {animator=target;cannon=joint;footRig=feet;}
        void Awake()
        {
            enemy=GetComponent<EnemyController>();mobile=GetComponent<EnemyMobile>();health=GetComponent<Health>();retreat=GetComponent<DisarmedEnemyRetreat>();
            previousPosition=transform.position;previousYaw=transform.eulerAngles.y;
            // Upper-body secondary motion (lean, bank, alert, recoil, stagger). Presentation only.
            if(!GetComponent<RobotMotionAccents>())gameObject.AddComponent<RobotMotionAccents>();
        }
        void OnEnable()
        {
            if(!health)health=GetComponent<Health>();
            health.OnDie+=OnDeath;if(cannon)cannon.Detached+=OnDetached;subscribed=true;
        }
        void OnDisable()
        {
            if(!subscribed)return;
            health.OnDie-=OnDeath;if(cannon)cannon.Detached-=OnDetached;subscribed=false;
        }
        void Update()
        {
            if(IsDead)return;
            float distance=Vector3.ProjectOnPlane(transform.position-previousPosition,Vector3.up).magnitude;
            previousPosition=transform.position;
            var agent=enemy.NavMeshAgent;
            Speed=agent && agent.enabled?agent.velocity.magnitude:0;
            float yaw=transform.eulerAngles.y;
            TurnRate=Time.deltaTime>0?Mathf.Clamp(Mathf.DeltaAngle(previousYaw,yaw)/Time.deltaTime,-240,240):0;previousYaw=yaw;
            // Time parameter follows distance travelled; no root motion or second movement authority.
            float advance=Speed>.08f?distance/strideDistance:Mathf.Abs(TurnRate)>15?Time.deltaTime*.85f:Time.deltaTime*.35f;
            phase=Mathf.Repeat(phase+advance,1);
            animator.SetFloat(Cycle,phase);animator.SetFloat(SpeedId,Speed,.08f,Time.deltaTime);
            animator.SetFloat(Turn,Mathf.Clamp(TurnRate/120,-1,1),.1f,Time.deltaTime);
        }
        void OnDetached(){if(animator)animator.SetBool(Disarmed,true);}
        void OnDeath()
        {
            if(IsDead)return;IsDead=true;Speed=0;
            if(mobile)mobile.enabled=false;if(retreat)retreat.enabled=false;
            var agent=enemy.NavMeshAgent;
            if(agent && agent.enabled && agent.isOnNavMesh){agent.ResetPath();agent.isStopped=true;agent.enabled=false;}
            // Existing EnemyController.OnDie still unregisters, drops and schedules destruction.
            enemy.enabled=false;
            foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
            if(footRig)footRig.weight=0;
            animator.ResetTrigger("Attack");animator.ResetTrigger("OnDamaged");
            for(int layer=1;layer<animator.layerCount;layer++)animator.SetLayerWeight(layer,0);
            animator.SetBool(Dead,true);animator.SetFloat(SpeedId,0);
        }
    }
}
