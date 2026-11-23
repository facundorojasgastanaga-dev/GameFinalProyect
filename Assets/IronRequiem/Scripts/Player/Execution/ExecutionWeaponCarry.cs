using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    // executions-v02 (E5, arm continuity): the stolen weapon is visible in exactly one place at every frame.
    //   robot (its own arm)  ->  [Rip beat, same frame]  proxy in the left hand  ->  [hand-off window] arc to the right arm
    //   ->  [stolen weapon active on the right arm, same frame] proxy off.
    // Proxy = the StolenCannon module that TryExecute already created and Equip hid (no second instance, no colliders, no physics):
    // its own render-only meshes are shown while this component poses the module root every LateUpdate (order 300, after
    // StolenCannon's own pose sync). Layers are switched to the FPS arms layer for the duration so it draws like the hand,
    // and restored on Hide/Cancel. The module is never destroyed here: Hide only turns its renderers off again while it is
    // still Equipped (a released/exploded module owns its renderers).
    // The robot hides ITS weapon on the Rip beat (robot side); the pose of birth is the live WeaponJoint.
    [DefaultExecutionOrder(300)]
    public sealed class ExecutionWeaponCarry : MonoBehaviour
    {
        [SerializeField,Min(.01f)] float handBlendSeconds=.05f;
        [SerializeField,Min(.01f)] float flightSeconds=.08f;
        [SerializeField,Min(0)] float arcHeight=.06f;
        [Tooltip("Camera-local offset of the weapon pivot from the left hand bone while it is held.")]
        [SerializeField] Vector3 handGripOffset=new Vector3(0,-.02f,.07f);
        [Tooltip("Safety: the proxy hides this long after the hand-off even if the real weapon has not shown yet.")]
        [SerializeField,Min(.1f)] float swapTimeoutSeconds=.6f;
        [Tooltip("Round 3: a module larger than this (world bounds diagonal, metres) shrinks to it while it flies to and sits in the hand. The Big Bot's cannon module is 2.6-2.9 m and filled the lens at 0.3 m; T-07 / SawBot modules are smaller and unaffected.")]
        [SerializeField,Min(.1f)] float maxHeldSize=.45f;
        Vector3 baseScale;float heldScale=1;bool scaled;
        enum Stage { Idle, Armed, InHand, Throw, AtArm }
        Stage stage;
        StolenCannon module;WeaponController stolen;Transform joint;
        Renderer[] views;int[] layers;
        ProtagonistHandsPresentation hands;Camera cam;
        Quaternion jointRotationOffset=Quaternion.identity;
        Vector3 birthPosition;Quaternion birthRotation;
        float stageTime,blendSeconds,throwSeconds=.08f,swapDeadline;bool fly,swapAllowed;
        int fpsLayer;
        public bool Active=>stage>=Stage.InHand;
        public bool Visible=>stage>=Stage.InHand;
        public string StageName=>stage.ToString(); // diagnostics
        public StolenCannon Module=>module;

        void Awake(){hands=GetComponent<ProtagonistHandsPresentation>();var player=GetComponent<Unity.FPS.Gameplay.PlayerCharacterController>();cam=player?player.PlayerCamera:null;}

        // At E: remember the module and the offset between the joint and the pose the weapon should be born with.
        public void Prepare(StolenCannon held,WeaponController stolenWeapon,Transform weaponJoint,Quaternion moduleRotationAtE)
        {
            Cancel();
            module=held;stolen=stolenWeapon;joint=weaponJoint;
            jointRotationOffset=joint?Quaternion.Inverse(joint.rotation)*moduleRotationAtE:Quaternion.identity;
            if(module)
            {
                var all=module.GetComponentsInChildren<Renderer>(true);int n=0;
                foreach(var r in all)if(!(r is ParticleSystemRenderer))n++;
                views=new Renderer[n];layers=new int[n];n=0;
                foreach(var r in all)if(!(r is ParticleSystemRenderer)){views[n]=r;layers[n]=r.gameObject.layer;n++;}
            }
            fpsLayer=hands&&hands.FpsRoot?hands.FpsRoot.gameObject.layer:0;
            stage=Stage.Armed;
        }

        // Rip beat, same frame as the robot hides its weapon: the proxy appears on the joint's pose.
        public void Show(bool flyToHand)
        {
            if(stage!=Stage.Armed||!module||module.State!=StolenCannonState.Equipped||views==null){return;}
            fly=flyToHand;blendSeconds=flyToHand?flightSeconds:handBlendSeconds;stageTime=0;swapAllowed=false;
            birthPosition=joint?joint.position:HandPosition();
            birthRotation=joint?joint.rotation*jointRotationOffset:cam.transform.rotation;
            for(int i=0;i<views.Length;i++)if(views[i]){views[i].gameObject.layer=fpsLayer;views[i].enabled=true;}
            baseScale=module.transform.localScale;scaled=true;heldScale=1;
            float size=0;bool any=false;var b=new Bounds();
            for(int i=0;i<views.Length;i++)if(views[i]){if(!any){b=views[i].bounds;any=true;}else b.Encapsulate(views[i].bounds);}
            if(any)size=b.size.magnitude;
            if(size>maxHeldSize)heldScale=maxHeldSize/size;
            stage=Stage.InHand;Pose();
        }
        // Hand-off window: the left hand tosses the weapon to the right arm.
        public void BeginThrow(float seconds){if(stage==Stage.InHand){throwSeconds=Mathf.Max(.02f,seconds);stageTime=0;stage=Stage.Throw;}}
        // From now the proxy hides on the first frame the real stolen weapon is active on the right arm (or after the timeout).
        public void AllowSwap(){swapAllowed=true;swapDeadline=Time.time+swapTimeoutSeconds;}
        public void Cancel(){Hide();stage=Stage.Idle;module=null;stolen=null;joint=null;views=null;layers=null;}
        public void Finish(){Hide();stage=Stage.Idle;module=null;stolen=null;joint=null;views=null;layers=null;}

        void Hide()
        {
            if(scaled&&module)module.transform.localScale=baseScale; // restored before anything else may use the module
            scaled=false;
            if(stage<Stage.InHand||views==null)return;
            bool owned=module&&module.State==StolenCannonState.Equipped; // a released module re-enabled its own renderers
            for(int i=0;i<views.Length;i++)
                if(views[i]){views[i].gameObject.layer=layers[i];if(owned)views[i].enabled=false;}
            stage=Stage.Armed;
        }

        Vector3 HandPosition()
        {
            var hand=hands?hands.LeftHandBone:null;
            return (hand?hand.position:cam.transform.position)+cam.transform.TransformVector(handGripOffset);
        }

        void LateUpdate()
        {
            if(stage<Stage.InHand)return;
            if(!module||!cam||module.State!=StolenCannonState.Equipped||!isActiveAndEnabled){Hide();return;}
            if(swapAllowed&&((stolen&&stolen.IsWeaponActive)||Time.time>=swapDeadline)){Hide();return;}
            stageTime+=Time.deltaTime;
            Pose();
        }

        void Pose()
        {
            var camT=cam.transform;
            var handPos=HandPosition();var handRot=camT.rotation;
            float shrink=stage==Stage.InHand?Mathf.SmoothStep(0,1,Mathf.Clamp01(stageTime/(blendSeconds*.6f))):1; // hand size before it reaches the lens
            if(scaled)module.transform.localScale=baseScale*Mathf.Lerp(1,heldScale,shrink);
            Vector3 position;Quaternion rotation;
            switch(stage)
            {
                case Stage.InHand:
                {
                    float u=Mathf.Clamp01(stageTime/blendSeconds);float e=u*u*(3-2*u);
                    position=Vector3.Lerp(birthPosition,handPos,e);
                    if(fly)position+=camT.up*arcHeight*Mathf.Sin(Mathf.PI*u);
                    rotation=Quaternion.Slerp(birthRotation,handRot,e);
                    break;
                }
                case Stage.Throw:
                {
                    float u=Mathf.Clamp01(stageTime/throwSeconds);float e=u*u*(3-2*u);
                    var arm=camT.TransformPoint(hands?hands.DisarmHandoffPoint:new Vector3(.22f,-.20f,.42f));
                    position=Vector3.Lerp(handPos,arm,e)+camT.up*arcHeight*Mathf.Sin(Mathf.PI*u);
                    rotation=handRot;
                    if(u>=1)stage=Stage.AtArm;
                    break;
                }
                default:
                    position=camT.TransformPoint(hands?hands.DisarmHandoffPoint:new Vector3(.22f,-.20f,.42f));rotation=handRot;break;
            }
            module.transform.SetPositionAndRotation(position,rotation);
        }

        void OnDisable(){Cancel();}
    }
}
