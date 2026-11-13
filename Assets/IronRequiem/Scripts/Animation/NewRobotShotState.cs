using UnityEngine;
namespace IronRequiem
{
    public sealed class NewRobotShotState : StateMachineBehaviour
    {
        [SerializeField] bool beginsSequence;
        public void Configure(bool begins)=>beginsSequence=begins;
        public override void OnStateEnter(Animator animator,AnimatorStateInfo stateInfo,int layerIndex)
        {
            var presentation=animator.GetComponent<NewRobotAnimationPresentation>();
            if(!presentation)return;
            if(beginsSequence)presentation.BeginShotPresentation();else presentation.EndShotPresentation();
        }
    }
}
