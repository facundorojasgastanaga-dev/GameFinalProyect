using UnityEngine;

namespace IronRequiem
{
    /// <summary>Positions rigid CapsuleRobotRig shoulder/upper-arm segments at the FPS elbow without stretching bones.</summary>
    [DefaultExecutionOrder(120)]
    public sealed class FirstPersonUpperArms : MonoBehaviour
    {
        [SerializeField] ProtagonistHandsPresentation hands;
        [SerializeField] Camera camera;
        [SerializeField] Renderer left, right;
        [SerializeField] Transform leftJoint, rightJoint;
        [SerializeField] float leftLength, rightLength;
        [SerializeField] Vector3 leftShoulder = new Vector3(-.3f,-.55f,-.25f), rightShoulder = new Vector3(.3f,-.55f,-.25f);
        static readonly Quaternion Canonical = Quaternion.Inverse(Quaternion.LookRotation(Vector3.up,Vector3.forward));
        public void Configure(ProtagonistHandsPresentation owner,Camera view,Renderer leftArm,Renderer rightArm,
            Transform leftElbow,Transform rightElbow,float leftReach,float rightReach)
        {
            hands=owner;camera=view;left=leftArm;right=rightArm;leftJoint=leftElbow;rightJoint=rightElbow;leftLength=leftReach;rightLength=rightReach;
        }
        void LateUpdate()
        {
            if(!hands||!camera)return;
            Fit(left,leftJoint,hands.LeftForearm,leftLength,leftShoulder,hands.LeftForearmView&&hands.LeftForearmView.enabled);
            Fit(right,rightJoint,hands.RightForearm,rightLength,rightShoulder,hands.RightForearmReplaced||hands.RightForearmView&&hands.RightForearmView.enabled);
        }
        void Fit(Renderer arm,Transform joint,Transform forearm,float length,Vector3 shoulder,bool visible)
        {
            if(!arm||!forearm)return;
            arm.enabled=visible;if(joint)joint.gameObject.SetActive(visible);
            if(!visible)return;
            Vector3 elbow=forearm.position,guide=camera.transform.TransformPoint(shoulder);
            Vector3 direction=(elbow-guide).normalized;
            var up=Vector3.ProjectOnPlane(camera.transform.forward,direction).normalized;
            if(up.sqrMagnitude<.5f)up=camera.transform.up;
            arm.transform.SetPositionAndRotation(elbow-direction*length,Quaternion.LookRotation(direction,up)*Canonical);
            if(joint)joint.position=elbow;
        }
    }
}
