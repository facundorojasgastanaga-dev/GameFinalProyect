using Unity.FPS.AI;
using UnityEngine;

namespace IronRequiem
{
    [DefaultExecutionOrder(200)]
    public sealed class T07CannonSocket : MonoBehaviour
    {
        [SerializeField] Transform socket,pivot,weapon;
        [SerializeField] DetachableCannon joint;
        [SerializeField] EnemyMobile mobile;
        [SerializeField] Vector3 positionOffset;
        [SerializeField] Quaternion rotationOffset;
        [Header("Arm aim (optional)")]
        [Tooltip("Upper arm bone turned after the animation so the cannon mouth points at the target. Empty = off.")]
        [SerializeField] Transform aimBone;
        [SerializeField] EnemyMuzzleAnchor mouth;
        [SerializeField, Range(0,1)] float aimStrength=1;
        [SerializeField, Min(.1f)] float aimBlendSpeed=5;
        EnemyController enemy;
        float aimWeight;
        // Robot anim test (17/11/2026): lets a short clip (hit reaction, shot recoil) own the arm for a moment.
        // Inert unless YieldAim is called: yieldWeight stays 0 and the aim below is unchanged.
        float yieldUntil,yieldWeight;
        public Transform Socket=>socket;
        public void YieldAim(float seconds){yieldUntil=Mathf.Max(yieldUntil,Time.time+seconds);}
        public void Configure(Transform bone,Transform mount,Transform gun,DetachableCannon detachable,EnemyMobile behavior)
        {
            socket=bone;pivot=mount;weapon=gun;joint=detachable;mobile=behavior;
            positionOffset=socket.InverseTransformPoint(pivot.position);
            rotationOffset=Quaternion.Inverse(socket.rotation)*pivot.rotation;
        }
        public void ConfigureAim(Transform upperArm,EnemyMuzzleAnchor cannonMouth){aimBone=upperArm;mouth=cannonMouth;}
        void Awake()=>enemy=GetComponent<EnemyController>();
        void LateUpdate()
        {
            if(!socket || !pivot || !weapon || !joint || joint.IsDetached)return;
            if(yieldUntil>0)yieldWeight=Mathf.MoveTowards(yieldWeight,Time.time<yieldUntil?1:0,(Time.time<yieldUntil?20:6)*Time.deltaTime);
            var aim=weapon.rotation;
            pivot.SetPositionAndRotation(socket.TransformPoint(positionOffset),socket.rotation*rotationOffset);
            if(aimBone && mouth && enemy && AimArm()) pivot.SetPositionAndRotation(socket.TransformPoint(positionOffset),socket.rotation*rotationOffset);
            // The robot's gimbal keeps the existing aim authority while the shoulder carries the mass.
            if(mobile && mobile.enabled && mobile.AiState!=EnemyMobile.AIState.Patrol)weapon.rotation=aim;
        }
        // Raises the cannon arm toward the target on top of the animated pose (weighted, so it eases in and out).
        bool AimArm()
        {
            var target=enemy.KnownDetectedTarget;
            bool want=target && (enemy.IsSeeingTarget || enemy.IsTargetInAttackRange);
            aimWeight=Mathf.MoveTowards(aimWeight,want?aimStrength:0,aimBlendSpeed*Time.deltaTime);
            if(aimWeight<=.001f || !target)return false;
            Vector3 shoulder=aimBone.position, from=mouth.MouthWorld()-shoulder, to=target.transform.position-shoulder;
            if(from.sqrMagnitude<1e-4f || to.sqrMagnitude<1e-4f)return false;
            var turn=Quaternion.FromToRotation(from,to);
            aimBone.rotation=Quaternion.Slerp(Quaternion.identity,turn,aimWeight*(1-yieldWeight))*aimBone.rotation;
            return true;
        }
    }
}
