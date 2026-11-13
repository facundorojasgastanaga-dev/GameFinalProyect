using UnityEngine;
namespace IronRequiem
{
    // Demonstration scene only. Never attached to the reusable robot prefab or gameplay actors.
    public sealed class NewRobotAnimationPreview : MonoBehaviour
    {
        [SerializeField] NewRobotAnimationPresentation robot;
        [SerializeField] string currentStage;
        Vector3 origin;Quaternion heading;float elapsed;int previous=-1;
        public void Configure(NewRobotAnimationPresentation target)=>robot=target;
        void Start(){origin=robot.transform.parent.position;heading=robot.transform.parent.rotation;}
        void Update()
        {
            elapsed+=Time.deltaTime;float t=elapsed%26;int stage=t<2?0:t<7?1:t<9?2:t<11?3:t<18?4:5;
            if(stage!=previous){robot.CancelShot();previous=stage;return;}
            float speed=stage==1||stage==5?robot.ReferenceWalkSpeed:0;robot.SetMotion(speed,stage==2?-1:stage==3?1:0);
            var root=robot.transform.parent;root.position=origin;root.rotation=heading;
            if(stage==1)root.position+=heading*Vector3.forward*(t-2)*speed;
            if(stage==2)root.rotation=heading*Quaternion.Euler(0,-45*(t-7),0);
            if(stage==3)root.rotation=heading*Quaternion.Euler(0,-90+45*(t-9),0);
            if(stage==5)root.position+=heading*Vector3.forward*(t-18)*speed;
            currentStage=stage switch{0=>"Idle",1=>"Walk",2=>"Turn Left",3=>"Turn Right",4=>"Shot / Recovery",_=>"Walk + Shot"};
            if(stage>=4&&!robot.IsShooting)robot.RequestShot();
        }
        void OnDisable(){if(robot)robot.CancelShot();}
    }
}
