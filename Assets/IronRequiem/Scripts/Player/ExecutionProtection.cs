using Unity.FPS.Game;
using UnityEngine;
namespace IronRequiem
{
    [DefaultExecutionOrder(120)]
    [RequireComponent(typeof(Health))]
    public sealed class ExecutionProtection : MonoBehaviour
    {
        [SerializeField,Min(0)] float postExecutionSeconds=2;
        Health health;BigBotExecutionSequence sequence;ProtagonistHandsPresentation hands;bool active;int started;
        public float PostExecutionSeconds=>postExecutionSeconds;
        void Awake(){health=GetComponent<Health>();hands=GetComponent<ProtagonistHandsPresentation>();}
        public void Begin(){if(!health)health=GetComponent<Health>();sequence=GetComponent<BigBotExecutionSequence>();active=true;started=Time.frameCount;health.GrantInvulnerability(Mathf.Max(.1f,postExecutionSeconds));}
        void LateUpdate()
        {
            if(!active||!health)return;
            if(health.CurrentHealth<=0){active=false;return;}
            if(Time.frameCount==started||(sequence&&sequence.IsRunning)||(hands&&hands.IsDisarming)){health.GrantInvulnerability(.1f);return;}
            active=false;health.GrantInvulnerability(postExecutionSeconds);
        }
        void OnDisable(){if(active&&health&&health.CurrentHealth>0)health.GrantInvulnerability(postExecutionSeconds);active=false;}
    }
}