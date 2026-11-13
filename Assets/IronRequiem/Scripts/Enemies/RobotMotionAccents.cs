using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    // Presentation-only secondary motion on the upper body: lean into acceleration, bank while
    // turning on the move, an alert jolt, firing recoil, a heavy-hit stagger and a shove.
    // It never touches the root, the NavMeshAgent, the feet or gameplay state. The offset is applied
    // after the Animator and other bone layers, and removed first on the next frame.
    [DefaultExecutionOrder(150)]
    [RequireComponent(typeof(EnemyController),typeof(Health))]
    public sealed class RobotMotionAccents : MonoBehaviour
    {
        [SerializeField] Transform torso;
        [Header("Locomotion")]
        [SerializeField,Range(0,8)] float accelerationLean=3.5f;
        [SerializeField,Range(0,8)] float turnBank=3f;
        [Header("Events (degrees)")]
        [SerializeField,Range(0,15)] float alertJolt=6f;
        [SerializeField,Range(0,15)] float shotRecoil=5f;
        [SerializeField,Range(0,20)] float staggerDegrees=9f;
        [SerializeField,Range(0,30)] float shoveDegrees=16f;
        [Header("Spring")]
        [SerializeField,Range(4,40)] float stiffness=18f;
        [SerializeField,Range(.1f,1.5f)] float damping=.55f;
        [SerializeField,Range(5,40)] float maximumAngle=22f;
        EnemyController enemy;Health health;
        WeaponController[] weapons;
        Quaternion saved;bool applied,dead;
        Vector3 previousVelocity,angle,angularVelocity;
        float previousYaw,smoothedTurn;
        public Transform Torso=>torso;
        public Vector3 CurrentAccent=>angle;
        public void Configure(Transform upperBody)=>torso=upperBody;

        void Awake()
        {
            enemy=GetComponent<EnemyController>();health=GetComponent<Health>();
            if(!torso){var feedback=GetComponent<RobotHitFeedback>();if(feedback)torso=feedback.Torso;}
            weapons=GetComponentsInChildren<WeaponController>(true);
            previousYaw=transform.eulerAngles.y;
        }
        void OnEnable()
        {
            if(!health)health=GetComponent<Health>();
            health.OnDamaged+=OnDamaged;health.OnDie+=OnDie;
            enemy.onDetectedTarget+=OnAlert;
            foreach(var weapon in weapons)if(weapon)weapon.OnShootProcessed+=OnShot;
        }
        void OnDisable()
        {
            if(health){health.OnDamaged-=OnDamaged;health.OnDie-=OnDie;}
            if(enemy)enemy.onDetectedTarget-=OnAlert;
            foreach(var weapon in weapons)if(weapon)weapon.OnShootProcessed-=OnShot;
            Restore();angle=angularVelocity=Vector3.zero;
        }
        // x = pitch (positive leans forward), z = roll (positive banks right), y = twist.
        public void AddImpulse(Vector3 degrees)
        {
            if(dead||!isActiveAndEnabled)return;
            angularVelocity+=degrees*stiffness*.9f;
        }
        // Accepted even while the robot collapses: the execution shove lands at the moment of death.
        public void Shove(Vector3 worldDirection)
        {
            if(!isActiveAndEnabled)return;
            var local=transform.InverseTransformDirection(Vector3.ProjectOnPlane(worldDirection,Vector3.up).normalized);
            angularVelocity+=new Vector3(local.z,0,-local.x)*shoveDegrees*stiffness;
        }
        void OnAlert()=>AddImpulse(new Vector3(-alertJolt,0,0));
        void OnShot()=>AddImpulse(new Vector3(-shotRecoil,Random.Range(-1f,1f)*shotRecoil*.3f,0));
        void OnDamaged(float amount,GameObject source)
        {
            if(health.CurrentHealth<=0||amount<=0)return;
            float heavy=Mathf.Clamp01(amount/Mathf.Max(1,health.MaxHealth)*4);
            if(heavy<.15f)return; // Light chip damage is handled by RobotHitFeedback.
            var local=source?transform.InverseTransformDirection(Vector3.ProjectOnPlane(transform.position-source.transform.position,Vector3.up).normalized):Vector3.back;
            AddImpulse(new Vector3(local.z,local.x*.6f,-local.x)*staggerDegrees*heavy);
        }
        void OnDie(){dead=true;}

        void Update()=>Restore();
        void LateUpdate()
        {
            if(!torso||Time.deltaTime<=0)return;
            float dt=Time.deltaTime;
            Vector3 target=Vector3.zero;
            if(!dead)
            {
                var agent=enemy.NavMeshAgent;
                Vector3 velocity=agent&&agent.enabled?agent.velocity:Vector3.zero;
                Vector3 acceleration=(velocity-previousVelocity)/dt;previousVelocity=velocity;
                var local=transform.InverseTransformDirection(acceleration);
                float yaw=transform.eulerAngles.y;
                float turn=Mathf.DeltaAngle(previousYaw,yaw)/dt;previousYaw=yaw;
                smoothedTurn=Mathf.Lerp(smoothedTurn,turn,1-Mathf.Exp(-8*dt));
                // Bank only while travelling; turning in place stays planted.
                float travel=Mathf.Clamp01(velocity.magnitude/2.5f);
                target=new Vector3(Mathf.Clamp(local.z/6,-1,1)*accelerationLean,0,-Mathf.Clamp(smoothedTurn/120,-1,1)*turnBank*travel);
            }
            // Critically-underdamped spring toward the locomotion target; events add velocity.
            float k=stiffness*stiffness,c=2*damping*stiffness;
            angularVelocity+=((target-angle)*k-angularVelocity*c)*dt;
            angle+=angularVelocity*dt;
            angle=Vector3.ClampMagnitude(angle,maximumAngle);
            if(angle.sqrMagnitude<.0004f&&angularVelocity.sqrMagnitude<.01f)return;
            saved=torso.localRotation;
            torso.rotation=Quaternion.AngleAxis(angle.y,transform.up)*Quaternion.AngleAxis(angle.x,transform.right)*Quaternion.AngleAxis(angle.z,transform.forward)*torso.rotation;
            applied=true;
        }
        void Restore(){if(applied&&torso)torso.localRotation=saved;applied=false;}
    }
}
