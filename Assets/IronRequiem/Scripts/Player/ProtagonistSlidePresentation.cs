using Unity.FPS.Gameplay;
using UnityEngine;
namespace IronRequiem
{
    [DefaultExecutionOrder(120)]
    public sealed class ProtagonistSlidePresentation : MonoBehaviour
    {
        [SerializeField] Transform visual;
        [SerializeField] Transform pelvis,leftThigh,rightThigh,leftShin,rightShin,leftFoot,rightFoot;
        [SerializeField,Min(.01f)] float blendTime=.09f;
        PlayerCharacterController player;AdvancedPlayerMovement movement;MechanicInput input;
        Vector3 leftThighAxis,rightThighAxis,leftShinAxis,rightShinAxis;
        Quaternion leftFootBind,rightFootBind,pelvisBind,slideFrame;float blend;
        bool wasSliding;
        // F5: slide legs breathe (entry kick, friction judder, tucked-knee pump, hip sway) instead of a frozen pose.
        float slideTime;Vector3 legJitter;float tuck;
        public bool IsVisible=>visual&&visual.gameObject.activeInHierarchy;
        public Transform Visual=>visual;
        public Vector3 FacingDirection=>slideFrame*Vector3.forward;
        public void Configure(Transform root,Transform p,Transform lt,Transform rt,Transform ls,Transform rs,Transform lf,Transform rf)
        {visual=root;pelvis=p;leftThigh=lt;rightThigh=rt;leftShin=ls;rightShin=rs;leftFoot=lf;rightFoot=rf;}
        void Awake()
        {
            player=GetComponent<PlayerCharacterController>();movement=GetComponent<AdvancedPlayerMovement>();input=GetComponent<MechanicInput>();
            leftThighAxis=leftThigh.InverseTransformDirection(leftShin.position-leftThigh.position).normalized;rightThighAxis=rightThigh.InverseTransformDirection(rightShin.position-rightThigh.position).normalized;
            leftShinAxis=leftShin.InverseTransformDirection(leftFoot.position-leftShin.position).normalized;rightShinAxis=rightShin.InverseTransformDirection(rightFoot.position-rightShin.position).normalized;
            leftFootBind=Quaternion.Inverse(leftShin.rotation)*leftFoot.rotation;rightFootBind=Quaternion.Inverse(rightShin.rotation)*rightFoot.rotation;
            pelvisBind=Quaternion.Inverse(visual.rotation)*pelvis.rotation;
        }
        void LateUpdate()
        {
            bool sliding=movement.IsSliding;
            if(sliding&&!wasSliding){slideFrame=Quaternion.LookRotation(movement.SlideDirection,Vector3.up);slideTime=0;}
            if(sliding)slideTime+=Time.deltaTime;
            wasSliding=sliding;
            bool contact=player.IsGrounded&&!player.HasJumpedThisFrame&&!player.IsDead&&input.CanAct;
            if(!contact){Hide();return;}
            blend=Mathf.MoveTowards(blend,sliding?1:0,Time.deltaTime/blendTime);
            if(visual.gameObject.activeSelf!=(blend>0))visual.gameObject.SetActive(blend>0);
            if(blend<=0)return;
            // Cancel the FPS camera parent's pitch/yaw. Lower body stays at the player,
            // facing the initial slide direction, so looking down only changes the view.
            float speed=Mathf.Clamp01(player.CharacterVelocity.magnitude/12f),t=Time.time;
            legJitter=new Vector3(0,(Mathf.PerlinNoise(t*17f,3.1f)-.5f)*.08f*speed,0);
            tuck=Mathf.Sin(t*8.5f)*.06f*speed;
            var sway=Quaternion.AngleAxis(Mathf.Sin(t*5.2f)*2.2f*speed,slideFrame*Vector3.forward);
            var frame=slideFrame;slideFrame=sway*slideFrame;
            visual.SetPositionAndRotation(transform.position,slideFrame);
            var origin=transform.position+Vector3.up*Mathf.Lerp(.13f,.32f,blend)+slideFrame*Vector3.forward*.07f;
            pelvis.SetPositionAndRotation(origin,slideFrame*pelvisBind);
            PoseLeg(leftThigh,leftShin,leftFoot,leftThighAxis,leftShinAxis,leftFootBind,-1,origin);
            PoseLeg(rightThigh,rightShin,rightFoot,rightThighAxis,rightShinAxis,rightFootBind,1,origin);
            float lift=Mathf.Max(FloorLift(leftFoot.position),FloorLift(rightFoot.position));if(lift>0)pelvis.position+=Vector3.up*lift;
            slideFrame=frame;
        }
        void PoseLeg(Transform thigh,Transform shin,Transform foot,Vector3 thighAxis,Vector3 shinAxis,Quaternion footBind,float side,Vector3 origin)
        {
            var hip=origin+slideFrame*Vector3.right*(side<0?-.135f:.08f);
            float entry=1-Mathf.Pow(1-Mathf.Clamp01(slideTime/.18f),3);
            // Front (right) leg kicks out from bent to straight on entry and judders on the floor; left knee pumps.
            var thighLocal=side<0?new Vector3(-.13f,.42f+tuck,1):new Vector3(0,Mathf.Lerp(-.04f,-.18f,entry),1)+legJitter;
            var shinLocal=side<0?new Vector3(.08f,-.65f-tuck*1.5f,.72f):new Vector3(0,Mathf.Lerp(-.32f,.03f,entry),1)+legJitter*.6f;
            var thighDirection=(slideFrame*thighLocal).normalized;
            var shinDirection=(slideFrame*shinLocal).normalized;
            thigh.SetPositionAndRotation(hip,Quaternion.FromToRotation(thigh.TransformDirection(thighAxis),thighDirection)*thigh.rotation);
            var knee=hip+thighDirection*.41f;shin.SetPositionAndRotation(knee,Quaternion.FromToRotation(shin.TransformDirection(shinAxis),shinDirection)*shin.rotation);
            foot.SetPositionAndRotation(knee+shinDirection*.42f,shin.rotation*footBind);
        }
        float FloorLift(Vector3 foot)
        {
            if(Physics.Raycast(foot+Vector3.up*.7f,Vector3.down,out var hit,2,player.GroundCheckLayers,QueryTriggerInteraction.Ignore)&&!hit.collider.transform.IsChildOf(transform))return Mathf.Max(0,hit.point.y+.13f-foot.y);
            return 0;
        }
        void Hide(){blend=0;if(visual)visual.gameObject.SetActive(false);}
        void OnDisable(){wasSliding=false;Hide();}
    }
}
