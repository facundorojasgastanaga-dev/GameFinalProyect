using UnityEngine;
using UnityEngine.AI;
namespace IronRequiem
{
    // Presentation-only contact IK. NavMeshAgent/EnemyMobile retain all movement and combat authority.
    [DefaultExecutionOrder(120)]
    public sealed class BigBotTurnFootPlant : MonoBehaviour
    {
        [SerializeField] Transform[] hips,knees,feet;
        [SerializeField] Vector3[] restOffsets;
        [SerializeField] Quaternion[] restRotations;
        [SerializeField,Range(.15f,.4f)] float stepDuration=.24f;
        [SerializeField,Range(.03f,.15f)] float stepHeight=.075f;
        BigBotCombat combat;NavMeshAgent agent;float previousYaw,weight,swingTime;int swing=-1,lastStep=1;bool applied;
        readonly Vector3[] anchors=new Vector3[2],lastFeet=new Vector3[2],posePosition=new Vector3[6];
        readonly Quaternion[] anchorRotations=new Quaternion[2],lastRotations=new Quaternion[2],poseRotation=new Quaternion[6];
        Vector3 swingFrom,swingTo;Quaternion swingRotationFrom,swingRotationTo;
        readonly RaycastHit[] groundHits=new RaycastHit[12];
        public bool Stabilizing=>weight>.01f;public int SwingingFoot=>swing;
        public void Configure(Transform[] upper,Transform[] lower,Transform[] ankles)
        {hips=upper;knees=lower;feet=ankles;restOffsets=new Vector3[2];restRotations=new Quaternion[2];for(int i=0;i<2;i++){restOffsets[i]=transform.InverseTransformPoint(feet[i].position);restRotations[i]=Quaternion.Inverse(transform.rotation)*feet[i].rotation;}}
        void Awake(){combat=GetComponent<BigBotCombat>();agent=GetComponent<NavMeshAgent>();agent.updateRotation=false;combat.Presentation.Animator.applyRootMotion=false;previousYaw=transform.eulerAngles.y;for(int i=0;i<2;i++){lastFeet[i]=feet[i].position;lastRotations[i]=feet[i].rotation;}}
        Transform Joint(int i)=>i<2?hips[i]:i<4?knees[i-2]:feet[i-4];
        void Update(){if(!applied)return;for(int i=0;i<6;i++){Joint(i).localPosition=posePosition[i];Joint(i).localRotation=poseRotation[i];}applied=false;}
        Vector3 GroundTarget(int i,Quaternion rotation)
        {
            var p=transform.position+rotation*Vector3.Scale(restOffsets[i],transform.lossyScale);float nearest=10;int count=Physics.RaycastNonAlloc(p+Vector3.up*.8f,Vector3.down,groundHits,1.8f,1,QueryTriggerInteraction.Ignore);
            for(int h=0;h<count;h++)if(!groundHits[h].collider.transform.IsChildOf(transform)&&groundHits[h].normal.y>.5f&&groundHits[h].distance<nearest){nearest=groundHits[h].distance;p.y=groundHits[h].point.y+restOffsets[i].y*transform.lossyScale.y+.01f;}return p;
        }
        void LateUpdate()
        {
            if(combat.IsDead)return;float yaw=transform.eulerAngles.y;float rate=Mathf.DeltaAngle(previousYaw,yaw)/Mathf.Max(Time.deltaTime,.001f);previousYaw=yaw;bool stationary=agent.enabled&&agent.velocity.sqrMagnitude<.0256f;bool turning=stationary&&Mathf.Abs(rate)>3;
            if(weight<=0&&turning){for(int i=0;i<2;i++){anchors[i]=lastFeet[i];anchorRotations[i]=lastRotations[i];}}
            bool settling=swing>=0;
            if(stationary&&(turning||weight>0)&&swing<0)
            {
                var prediction=Quaternion.AngleAxis(Mathf.Clamp(rate,-60,60)*stepDuration*.6f,Vector3.up)*transform.rotation;
                for(int test=0;test<2;test++)
                {
                    int i=(lastStep+1+test)%2;var target=GroundTarget(i,prediction);var rotation=prediction*restRotations[i];float error=Vector3.ProjectOnPlane(target-anchors[i],Vector3.up).magnitude;float angle=Quaternion.Angle(anchorRotations[i],rotation);
                    if(error>(turning?.11f:.035f)||angle>(turning?14:5)){swing=i;lastStep=i;swingTime=0;swingFrom=anchors[i];swingTo=target;swingRotationFrom=anchorRotations[i];swingRotationTo=rotation;break;}
                }
                settling=swing>=0;
            }
            if(swing>=0)
            {
                swingTime+=Time.deltaTime;float u=Mathf.Clamp01(swingTime/stepDuration);float t=u*u*(3-2*u);anchors[swing]=Vector3.Lerp(swingFrom,swingTo,t)+Vector3.up*(stepHeight*Mathf.Sin(Mathf.PI*u));anchorRotations[swing]=Quaternion.Slerp(swingRotationFrom,swingRotationTo,t);
                if(u>=1){anchors[swing]=swingTo;anchorRotations[swing]=swingRotationTo;swing=-1;}
            }
            float wanted=stationary&&(turning||settling||swing>=0)?1:0;weight=Mathf.MoveTowards(weight,wanted,Time.deltaTime/.14f);
            if(weight>0)
            {
                for(int i=0;i<6;i++){posePosition[i]=Joint(i).localPosition;poseRotation[i]=Joint(i).localRotation;}applied=true;
                for(int i=0;i<2;i++)Solve(i,Vector3.Lerp(feet[i].position,anchors[i],weight),Quaternion.Slerp(feet[i].rotation,anchorRotations[i],weight));
            }
            for(int i=0;i<2;i++){lastFeet[i]=feet[i].position;lastRotations[i]=feet[i].rotation;}
        }
        void Solve(int i,Vector3 target,Quaternion rotation)
        {
            var hip=hips[i];var knee=knees[i];var foot=feet[i];float l1=Vector3.Distance(hip.position,knee.position),l2=Vector3.Distance(knee.position,foot.position);var vector=target-hip.position;float d=Mathf.Clamp(vector.magnitude,Mathf.Abs(l1-l2)+.001f,l1+l2-.001f);var dir=vector.normalized;float a=(l1*l1-l2*l2+d*d)/(2*d);var bend=Vector3.ProjectOnPlane(transform.forward,dir).normalized;var p=hip.position+dir*a+bend*Mathf.Sqrt(Mathf.Max(0,l1*l1-a*a));hip.rotation=Quaternion.FromToRotation(knee.position-hip.position,p-hip.position)*hip.rotation;knee.position=p;knee.rotation=Quaternion.FromToRotation(foot.position-knee.position,target-p)*knee.rotation;foot.position=target;foot.rotation=rotation;
        }
    }
}

