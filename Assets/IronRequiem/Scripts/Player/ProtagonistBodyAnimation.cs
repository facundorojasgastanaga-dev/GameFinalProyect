using Unity.FPS.Gameplay;
using UnityEngine;
namespace IronRequiem
{
    public sealed class ProtagonistBodyAnimation : MonoBehaviour
    {
        [SerializeField] PlayerCharacterController player;
        [SerializeField] AdvancedPlayerMovement movement;
        [SerializeField] Animator animator;
        static readonly int MoveSpeed=Animator.StringToHash("MoveSpeed"),CycleSpeed=Animator.StringToHash("CycleSpeed");
        public void Configure(PlayerCharacterController controller,Animator target){player=controller;movement=controller.GetComponent<AdvancedPlayerMovement>();animator=target;}
        void Update()
        {
            float speed=Vector3.ProjectOnPlane(player.CharacterVelocity,Vector3.up).magnitude;
            bool locomotion=player.IsGrounded&&!player.IsDead&&!(movement&&movement.IsSliding);
            animator.SetFloat(MoveSpeed,locomotion?Mathf.Clamp01(speed/player.MaxSpeedOnGround):0);
            animator.SetFloat(CycleSpeed,Mathf.Clamp(speed/2.5f,.5f,3f));
        }
    }
}
