using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace IronRequiem
{
    [DefaultExecutionOrder(100)]
    public sealed class T07FootPlacement : MonoBehaviour
    {
        [SerializeField] T07AnimationDriver driver;
        [SerializeField] Transform[] targets;
        [SerializeField] Transform[] hints;
        [SerializeField] Vector3[] restPositions;
        [SerializeField] Quaternion[] restRotations;
        [SerializeField] LayerMask groundLayers=1;
        [SerializeField, Range(.4f,.7f)] float stanceFraction=.55f;
        [SerializeField, Min(.01f)] float stepHeight=.13f;
        readonly Vector3[] planted=new Vector3[2],swingStart=new Vector3[2];
        readonly bool[] wasStance=new bool[2];
        bool initialized;
        public Transform[] Targets => targets;
        public bool LeftPlanted => wasStance[0];
        public bool RightPlanted => wasStance[1];
        public void Configure(T07AnimationDriver source,Transform[] feet,Transform[] knees,Vector3[] positions,Quaternion[] rotations)
        {driver=source;targets=feet;hints=knees;restPositions=positions;restRotations=rotations;}
        void Update()
        {
            if(!driver || driver.IsDead || Time.deltaTime<=0 || targets==null || targets.Length!=2)return;
            bool moving=driver.Speed>.08f;bool turning=Mathf.Abs(driver.TurnRate)>15;
            for(int i=0;i<2;i++)
            {
                float phase=Mathf.Repeat(driver.Phase+i*.5f,1);
                bool stance=(!moving && !turning)||phase<stanceFraction;
                Vector3 home=transform.TransformPoint(restPositions[i]);
                float halfStep=moving?driver.StrideDistance*stanceFraction*.5f:0;
                Vector3 landing=Ground(home+transform.forward*halfStep,restPositions[i].y);
                if(!initialized){planted[i]=Ground(home,restPositions[i].y);swingStart[i]=planted[i];}
                if(stance)
                {
                    if(!wasStance[i] && initialized)planted[i]=landing;
                    targets[i].position=planted[i];
                }
                else
                {
                    if(wasStance[i])swingStart[i]=planted[i];
                    float swing=Mathf.InverseLerp(stanceFraction,1,phase);
                    targets[i].position=Vector3.Lerp(swingStart[i],landing,Mathf.SmoothStep(0,1,swing))+Vector3.up*(Mathf.Sin(swing*Mathf.PI)*stepHeight);
                }
                // Replant after teleports; IK must never stretch across a level.
                if(Vector3.Distance(targets[i].position,home)>1.2f){planted[i]=Ground(home,restPositions[i].y);targets[i].position=planted[i];}
                targets[i].rotation=transform.rotation*restRotations[i];
                hints[i].position=home+transform.forward*.65f+Vector3.up*.45f;
                wasStance[i]=stance;
            }
            initialized=true;
        }
        Vector3 Ground(Vector3 point,float ankleHeight)
        {
            if(Physics.Raycast(point+Vector3.up*.7f,Vector3.down,out var hit,1.6f,groundLayers,QueryTriggerInteraction.Ignore))
                point.y=hit.point.y+ankleHeight;
            return point;
        }
    }
}
