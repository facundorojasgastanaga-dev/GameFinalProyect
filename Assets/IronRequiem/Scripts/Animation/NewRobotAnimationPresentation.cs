using System;
using UnityEngine;
namespace IronRequiem
{
    [RequireComponent(typeof(Animator))]
    public sealed class NewRobotAnimationPresentation : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] Transform muzzle;
        [SerializeField,Min(.01f)] float referenceWalkSpeed=1.0106414f;
        bool armed;
        public bool IsShooting {get;private set;}
        public int ShotCount {get;private set;}
        public Animator Animator=>animator;
        public Transform Muzzle=>muzzle;
        public float ReferenceWalkSpeed=>referenceWalkSpeed;
        // Presentation-only hook. No projectile, sound, VFX or damage is emitted here.
        public event Action<Transform> ShotMoment;
        static readonly int Attack=Animator.StringToHash("Attack"),Cancel=Animator.StringToHash("CancelAttack");
        public void Configure(Animator target,Transform origin,float speed){animator=target;muzzle=origin;referenceWalkSpeed=speed;}
        void Awake(){if(!animator)animator=GetComponent<Animator>();}
        public void SetMotion(float metresPerSecond,float turn=0)
        {
            animator.SetFloat("MoveSpeed",Mathf.Max(0,metresPerSecond));
            animator.SetFloat("CycleSpeed",metresPerSecond>.01f?metresPerSecond/referenceWalkSpeed:1);
            animator.SetFloat("Turn",Mathf.Clamp(turn,-1,1));
        }
        [ContextMenu("Preview Shot (Play Mode)")]
        public void PreviewShot(){RequestShot();}
        public bool RequestShot()
        {
            if(!Application.isPlaying||!isActiveAndEnabled||IsShooting)return false;
            IsShooting=true;armed=false;animator.ResetTrigger(Cancel);animator.SetTrigger(Attack);return true;
        }
        [ContextMenu("Cancel Shot (Play Mode)")]
        public void CancelShot(){armed=false;IsShooting=false;if(animator){animator.ResetTrigger(Attack);animator.SetTrigger(Cancel);}}
        public void BeginShotPresentation(){IsShooting=true;armed=true;}
        public void EndShotPresentation(){IsShooting=false;armed=false;}
        // AnimationEvent, exactly once per accepted sequence, including interrupted transitions.
        public void OnPresentationShot(){if(!armed)return;armed=false;ShotCount++;ShotMoment?.Invoke(muzzle);}
        void OnDisable(){armed=false;IsShooting=false;if(animator){animator.ResetTrigger(Attack);animator.ResetTrigger(Cancel);}}
    }
}
