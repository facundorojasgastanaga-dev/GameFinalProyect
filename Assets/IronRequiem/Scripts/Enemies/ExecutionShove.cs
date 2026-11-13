using Unity.FPS.AI;
using UnityEngine;

namespace IronRequiem
{
    // Presentation-only knockback after the player rips a weapon off a robot. Added at runtime to the
    // robot root, slides it back along the ground with an ease-out, stops at walls and removes itself.
    // While the NavMeshAgent is alive it moves through agent.Move so it never leaves the NavMesh.
    public sealed class ExecutionShove : MonoBehaviour
    {
        [SerializeField,Min(0)] float distance=1.4f;
        [SerializeField,Min(.05f)] float duration=.32f;
        [SerializeField,Min(0)] float delay;
        [SerializeField,Min(.05f)] float probeRadius=.35f;
        Vector3 direction;
        Transform source;
        float elapsed,travelled;
        bool started;
        readonly RaycastHit[] hits=new RaycastHit[16];
        public bool Finished {get;private set;}
        public float Travelled=>travelled;

        // v4: heavy punch push with its own distance/duration (same ground-safe slide, stops at walls).
        public static ExecutionShove Begin(GameObject robot,Vector3 worldDirection,float delaySeconds,Transform shover,float metres,float seconds)
        {
            var shove=Begin(robot,worldDirection,delaySeconds,shover);
            if(shove){shove.distance=Mathf.Max(0,metres);shove.duration=Mathf.Max(.05f,seconds);}
            return shove;
        }
        public static ExecutionShove Begin(GameObject robot,Vector3 worldDirection,float delaySeconds,Transform shover=null)
        {
            if(!robot)return null;
            var flat=Vector3.ProjectOnPlane(worldDirection,Vector3.up);
            if(flat.sqrMagnitude<1e-4f)flat=-robot.transform.forward;
            var shove=robot.GetComponent<ExecutionShove>();
            if(!shove)shove=robot.AddComponent<ExecutionShove>();
            shove.source=shover;shove.direction=flat.normalized;shove.delay=Mathf.Max(0,delaySeconds);
            shove.elapsed=0;shove.travelled=0;shove.started=false;shove.Finished=false;shove.enabled=true;
            return shove;
        }

        void Update()
        {
            if(Time.deltaTime<=0)return;
            elapsed+=Time.deltaTime;
            float t=elapsed-delay;
            if(t<0)return;
            if(!started)
            {
                started=true;
                var accents=GetComponent<RobotMotionAccents>();if(accents)accents.Shove(direction);
            }
            float k=Mathf.Clamp01(t/duration);
            float wanted=distance*(1-(1-k)*(1-k)*(1-k))-travelled;
            if(wanted>0)
            {
                float allowed=Clear(wanted);
                if(allowed>0)
                {
                    var step=direction*allowed;
                    var enemy=GetComponent<EnemyController>();
                    var agent=enemy?enemy.NavMeshAgent:null;
                    if(agent&&agent.enabled&&agent.isOnNavMesh)agent.Move(step);
                    else transform.position+=step;
                    travelled+=allowed;
                }
                if(allowed<wanted-1e-4f)k=1; // Blocked by geometry: stop here instead of pushing through.
            }
            if(k>=1){Finished=true;Destroy(this);}
        }

        float Clear(float wanted)
        {
            var origin=transform.position+Vector3.up*(probeRadius+.35f);
            int count=Physics.SphereCastNonAlloc(origin,probeRadius,direction,hits,wanted+.05f,-1,QueryTriggerInteraction.Ignore);
            float nearest=wanted+.05f;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(hit.collider.transform.IsChildOf(transform)||hit.distance<=0)continue;
                if(hit.collider.attachedRigidbody&&!hit.collider.attachedRigidbody.isKinematic)continue; // Loose debris.
                if(source&&hit.collider.transform.IsChildOf(source))continue; // The player who shoved.
                nearest=Mathf.Min(nearest,hit.distance);
            }
            return Mathf.Clamp(nearest-.05f,0,wanted);
        }
    }
}
