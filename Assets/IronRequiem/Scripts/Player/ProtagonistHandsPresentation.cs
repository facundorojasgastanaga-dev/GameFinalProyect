using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    // Presentation follows the real inventory. This component never adds, moves or consumes a weapon.
    [DefaultExecutionOrder(110)]
    public sealed class ProtagonistHandsPresentation : MonoBehaviour
    {
        public enum Representation { Normal, Pistol, StolenCannon, Mounted }
        [SerializeField] PlayerWeaponsManager weapons;
        [SerializeField] PlayerCharacterController player;
        [SerializeField] StolenWeaponCarrier carrier;
        [SerializeField] Health health;
        [SerializeField] WeaponController pistolPrefab;
        [SerializeField] Transform fpsRoot;
        [SerializeField] HandPoseController handPoses;
        public void ConfigureHandPoses(HandPoseController controller) => handPoses = controller;
        [SerializeField] Animator poseAnimator;
        [SerializeField] Transform leftForearm, rightForearm, leftHand, rightHand;
        [SerializeField] Transform leftModuleSocket, rightModuleSocket;
        [SerializeField] Renderer[] rightHandRenderers;
        [Header("Module attachment relative to the existing muzzle (metres)")]
        [SerializeField] Vector3 pistolWristOffset=new Vector3(0,-.015f,-.23f);
        [SerializeField] Vector3 cannonWristOffset=new Vector3(.015f,-.04f,-.38f);
        [SerializeField] Vector3 pistolSupportOffset=new Vector3(-.28f,-.10f,-.18f);
        [SerializeField] Vector3 cannonSupportOffset=new Vector3(-.16f,-.13f,-.24f);
        [Header("Inventory weapons mounted on the modular forearm (for example the secondary shotgun)")]
        [Tooltip("Weapons whose rear Attachment plugs into the right forearm receiver, like the primary module.")]
        [SerializeField] WeaponController[] mountedWeaponPrefabs=new WeaponController[0];
        [SerializeField] Vector3 mountedWristOffset=new Vector3(0,-.03f,-.42f);
        [SerializeField] Vector3 mountedSupportOffset=new Vector3(-.2f,-.12f,-.22f);
        public void ConfigureMountedWeapons(WeaponController[] prefabs)=>mountedWeaponPrefabs=prefabs??new WeaponController[0];
        bool IsMounted(WeaponController weapon)
        {
            if(!weapon||mountedWeaponPrefabs==null)return false;
            foreach(var prefab in mountedWeaponPrefabs)if(prefab&&weapon.SourcePrefab==prefab.gameObject)return true;
            return false;
        }
        Vector3 WristOffset=>CurrentRepresentation==Representation.Pistol?pistolWristOffset:CurrentRepresentation==Representation.Mounted?mountedWristOffset:cannonWristOffset;
        Vector3 SupportOffset=>CurrentRepresentation==Representation.Pistol?pistolSupportOffset:CurrentRepresentation==Representation.Mounted?mountedSupportOffset:cannonSupportOffset;
        [Header("Action variant")]
        [SerializeField] bool actionsEnabled;
        [SerializeField] ProtagonistMelee melee;
        [SerializeField] AnimationClip leftPunchClip;
        [SerializeField] Transform punchWristTarget,punchElbowTarget;
        [SerializeField] Renderer[] leftRenderers;
        [Header("Removable right forearm")]
        [SerializeField] bool modularRightArm;
        [SerializeField,Min(.03f)] float retainedForearmLength=.14f;
        [SerializeField] Renderer[] rightArmReceiverViews;
        [Header("Arm continuity (upper-arm sleeves)")]
        [Tooltip("Adds a sleeve from each forearm elbow end to a shoulder point outside the view, so the cut end of the short FPS forearm is never visible. Off keeps the previous presentation.")]
        [SerializeField] bool armSleeves;
        [SerializeField,Min(.01f)] float sleeveRadius=.05f;
        [Tooltip("Metres the sleeve overlaps into the forearm, hiding its open elbow end.")]
        [SerializeField,Min(0)] float sleeveOverlap=.06f;
        [SerializeField] Vector3 leftShoulder=new Vector3(-.30f,-.55f,-.25f), rightShoulder=new Vector3(.30f,-.55f,-.25f);
        [SerializeField] Material sleeveMaterial;
        Transform leftSleeve,rightSleeve;Renderer leftSleeveView,rightSleeveView;Renderer leftForearmView,rightForearmView;
        public bool ArmSleeves=>armSleeves;public Transform LeftForearm=>leftForearm;public Transform RightForearm=>rightForearm;public Renderer LeftForearmView=>leftForearmView;public Renderer RightForearmView=>rightForearmView;public Renderer LeftSleeve=>leftSleeveView;public Renderer RightSleeve=>rightSleeveView;
        public void ConfigureSleeves(bool enabled,float radius,Material material){armSleeves=enabled;sleeveRadius=radius;sleeveMaterial=material;}
        [Header("v7 closed fist (procedural, both hands)")]
        [Tooltip("Folds the fingers into a real fist (knuckles forward, fingers in the palm, thumb across index and middle) while that hand strikes or charges.")]
        [SerializeField] bool closedFist=true;
        [Tooltip("Per hand: finger angles, how close together, palm side (0 = rig default, 1 / -1 to flip if the fingers bend the wrong way). Editable live in Play mode.")]
        [SerializeField] HandFistRig rightFist=new HandFistRig(),leftFist=new HandFistRig();
        [Tooltip("Extra wrist rotation (degrees, hand local X/Y/Z) applied while that hand is a fist. Use it to turn the fist the right way round (e.g. 180 on one axis if it is upside down). Editable live in Play mode.")]
        [SerializeField] Vector3 leftFistWristEuler,rightFistWristEuler;
        [Tooltip("Play mode only: hold that fist in view (charge pose) so the values above can be tuned live.")]
        [SerializeField] bool previewLeftFist,previewRightFist;
        public HandFistRig RightFistRig=>rightFist;public HandFistRig LeftFistRig=>leftFist;
        public Transform LeftHandBone=>leftHand;
        public bool IsFingerBoneOf(Transform t)=>IsFingerBone(t);
        void OnValidate(){rightFist?.Invalidate();leftFist?.Invalidate();}
        public float FistClosure=>Mathf.Max(RightFistClosure,LeftFistClosure);
        public float RightFistClosure=>handPoses?handPoses.RightClosure:rightFist.Weight;public float LeftFistClosure=>handPoses?handPoses.LeftClosure:leftFist.Weight;
        public string FistDebug=>"R "+rightFist.Describe()+" | L "+leftFist.Describe();
        [Header("v6 wrist joint (mechanical collar at the hand / module port)")]
        [SerializeField] bool wristCollar=true;
        [SerializeField,Min(.01f)] float wristCollarRadius=.046f,wristCollarLength=.032f;
        [SerializeField] Color wristCollarColor=new Color(.13f,.14f,.16f),receiverColor=new Color(.16f,.17f,.19f);
        Transform wristCollarT;Renderer wristCollarView;
        public Transform WristMount=>wristCollarT;
        [Header("Disarm (wrench weapon off + punch)")]
        [SerializeField] DisarmMotion disarm=new DisarmMotion();
        [SerializeField] Vector3 yankCameraKick=new Vector3(-1.6f,-.8f,2.2f);
        [SerializeField] Vector3 shoveCameraKick=new Vector3(2.4f,.6f,-1.8f);
        int disarmKicks;
        public bool IsDisarming=>disarm!=null&&disarm.Active;
        // Seconds from the start of the disarm until the fist lands (robot knockback timing).
        public float DisarmStrikeTime=>disarm!=null?disarm.StrikeTime:.26f;
        public float DisarmYankTime=>disarm!=null?disarm.YankTime:.1f;
        Vector3 moduleSocketBindPosition;Quaternion socketBindRotation=Quaternion.identity;
        Quaternion moduleSocketBindRotation;
        Renderer[] actionRightForearm;
        WeaponController mountedWeapon;
        Transform nativeAttachment;
        public bool LeftHandVisible=>leftRenderers!=null&&System.Array.Exists(leftRenderers,r=>r&&r.enabled&&r.gameObject.activeInHierarchy);
        public void ConfigureActions(ProtagonistMelee attack,AnimationClip clip,Transform wristTarget,Transform elbowTarget,Renderer[] left)
        {actionsEnabled=true;melee=attack;leftPunchClip=clip;punchWristTarget=wristTarget;punchElbowTarget=elbowTarget;leftRenderers=left;}
        public void ConfigureModularArm(Renderer[] receivers,float retainedLength)
        {modularRightArm=true;rightArmReceiverViews=receivers;retainedForearmLength=retainedLength;}
        public bool RightForearmReplaced=>modularRightArm&&CurrentRepresentation!=Representation.Normal;
        /// 16/11: how far ahead of a mounted module's Attachment the right hand bone sits (read-only; the montage
        /// station uses it to end its override exactly on the pose this component computes after the mount).
        public float MountedHandLead=>(modularRightArm?rightLength-retainedForearmLength:0)+.003f;
        Vector3 leftAxis,rightAxis;
        Quaternion leftHandBind,rightHandBind;
        float leftLength,rightLength;
        WeaponController active;
        static readonly int HandPose=Animator.StringToHash("HandPose");
        public Representation CurrentRepresentation {get;private set;}
        // v5 scripted right-arm pose (montage station): camera-space wrist/elbow blended over the normal pose, with the
        // hand optionally detached (hidden, socket receiver shown). Presentation only.
        bool armOverride,overrideHandHidden;Vector3 overrideWrist,overrideElbow;float overrideWeight;
        public void SetRightArmOverride(Vector3 wristCamera,Vector3 elbowCamera,float weight,bool handHidden)
        {armOverride=true;overrideWrist=wristCamera;overrideElbow=elbowCamera;overrideWeight=Mathf.Clamp01(weight);overrideHandHidden=handHidden;}
        public void ClearRightArmOverride(){armOverride=false;overrideHandHidden=false;}
        public bool RightArmOverridden=>armOverride;
        public Transform RightHand=>rightHand;public Transform RightForearmBone=>rightForearm;
        public Renderer[] RightHandRenderers=>rightHandRenderers;
        public Transform FpsRoot=>fpsRoot;
        public Transform RightModuleSocket=>rightModuleSocket;
        public Transform LeftModuleSocket=>leftModuleSocket;
        public bool RightHandVisible=>rightHandRenderers.Length>0&&rightHandRenderers[0].enabled&&fpsRoot.gameObject.activeInHierarchy;
        public void Configure(PlayerWeaponsManager manager,PlayerCharacterController controller,StolenWeaponCarrier owner,WeaponController pistol,Transform visual,Animator animator,Transform lf,Transform rf,Transform lh,Transform rh,Transform ls,Transform rs,Renderer[] handRenderers)
        {weapons=manager;player=controller;carrier=owner;health=controller.GetComponent<Health>();pistolPrefab=pistol;fpsRoot=visual;poseAnimator=animator;leftForearm=lf;rightForearm=rf;leftHand=lh;rightHand=rh;leftModuleSocket=ls;rightModuleSocket=rs;rightHandRenderers=handRenderers;}
        void Awake()
        {
            actionRightForearm=fpsRoot.GetComponentsInChildren<Renderer>(true);
            moduleSocketBindPosition=rightModuleSocket.localPosition;moduleSocketBindRotation=rightModuleSocket.localRotation;
            socketBindRotation=weapons.WeaponParentSocket.localRotation;
            leftAxis=leftForearm.InverseTransformDirection(leftHand.position-leftForearm.position).normalized;
            rightAxis=rightForearm.InverseTransformDirection(rightHand.position-rightForearm.position).normalized;
            leftLength=Vector3.Distance(leftHand.position,leftForearm.position);rightLength=Vector3.Distance(rightHand.position,rightForearm.position);
            leftHandBind=Quaternion.Inverse(leftForearm.rotation)*leftHand.rotation;rightHandBind=Quaternion.Inverse(rightForearm.rotation)*rightHand.rotation;
            foreach(var r in actionRightForearm){if(r.name=="Forearm_L_FPS")leftForearmView=r;if(r.name=="Forearm_R_FPS")rightForearmView=r;}
            rightFist.Collect(rightHand,IsFingerBone,1);leftFist.Collect(leftHand,IsFingerBone,-1); // palm sides of the ProtagonistV01 rig
            if(wristCollar)BuildWristCollar();
            if(armSleeves){leftSleeve=CreateSleeve("Sleeve_L_FPS",leftForearmView,out leftSleeveView);rightSleeve=CreateSleeve("Sleeve_R_FPS",rightForearmView,out rightSleeveView);}
        }
        bool IsFingerBone(Transform t)
        {
            if(!t||t==rightModuleSocket||t==leftModuleSocket||t.GetComponent<Renderer>())return false;
            var n=t.name;return n.IndexOf("Socket",System.StringComparison.OrdinalIgnoreCase)<0&&n.IndexOf("Attachment",System.StringComparison.OrdinalIgnoreCase)<0&&n.IndexOf("Module",System.StringComparison.OrdinalIgnoreCase)<0&&!n.EndsWith("_end",System.StringComparison.OrdinalIgnoreCase);
        }
        // v6: a dark collar hides the seam between hand and forearm (and between forearm and module); the forearm receiver
        // (shown while the hand is off) is tinted dark metal instead of the bare light grey of the source mesh.
        void BuildWristCollar()
        {
            wristCollarT=CreateSleeve("WristCollar_R_FPS",rightForearmView,out wristCollarView);
            var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",wristCollarColor);block.SetColor("_Color",wristCollarColor);wristCollarView.SetPropertyBlock(block);
            if(rightArmReceiverViews!=null)foreach(var r in rightArmReceiverViews)if(r){var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);b.SetColor("_BaseColor",receiverColor);b.SetColor("_Color",receiverColor);r.SetPropertyBlock(b);}
        }
        void PoseWristCollar(Vector3 port,bool replaced)
        {
            if(!wristCollarT)return;
            bool show=rightForearmView&&rightForearmView.enabled&&rightForearmView.gameObject.activeInHierarchy;wristCollarView.enabled=show;if(!show)return;
            var dir=rightForearm.TransformDirection(rightAxis).normalized;var at=replaced?port:rightHand.position;
            wristCollarT.SetPositionAndRotation(at-dir*(wristCollarLength*.35f),Quaternion.FromToRotation(Vector3.up,dir));
            var ps=wristCollarT.parent.lossyScale;wristCollarT.localScale=new Vector3(wristCollarRadius*2/ps.x,wristCollarLength*.5f/ps.y,wristCollarRadius*2/ps.z);
        }
        Transform CreateSleeve(string name,Renderer reference,out Renderer view)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(fpsRoot,false);if(reference)go.layer=reference.gameObject.layer;
            view=go.GetComponent<Renderer>();view.sharedMaterial=sleeveMaterial?sleeveMaterial:reference?reference.sharedMaterial:view.sharedMaterial;
            view.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;view.receiveShadows=false;return go.transform;
        }
        // Elbow end of the fitted forearm -> shoulder point in camera space (behind/below the view).
        void FitSleeve(Transform sleeve,Renderer view,Renderer forearmView,Transform forearm,Vector3 axis,Vector3 shoulderLocal)
        {
            if(!sleeve)return;bool show=forearmView&&forearmView.enabled&&forearmView.gameObject.activeInHierarchy;view.enabled=show;if(!show)return;
            var dir=forearm.TransformDirection(axis).normalized;var start=forearm.position+dir*sleeveOverlap;var end=player.PlayerCamera.transform.TransformPoint(shoulderLocal);
            var span=end-start;float length=span.magnitude;if(length<1e-3f){view.enabled=false;return;}
            sleeve.SetPositionAndRotation((start+end)*.5f,Quaternion.FromToRotation(Vector3.up,span/length));
            var parentScale=sleeve.parent.lossyScale;sleeve.localScale=new Vector3(sleeveRadius*2/parentScale.x,length*.5f/parentScale.y,sleeveRadius*2/parentScale.z);
        }
        void OnEnable(){if(carrier)carrier.Executed+=OnExecuted;}
        void OnExecuted(EnemyExecutionSource target,Vector3 grip)
        {
            if(disarm==null||!player)return;
            disarm.Begin(player.PlayerCamera.transform.InverseTransformPoint(grip));disarmKicks=0;
            DistanceCameraImpulse.Kick(player.PlayerCamera,new Vector3(.6f,-.4f,.6f),new Vector3(0,0,.01f));
        }
        void StepDisarm()
        {
            if(!IsDisarming)return;
            if(player.IsDead||health.CurrentHealth<=0){disarm.Cancel();return;}
            var beat=disarm.Step(Time.deltaTime);
            if(beat==DisarmMotion.Beat.None||beat==DisarmMotion.Beat.Done)return;
            // Strain: a small tug of the view towards the joint; yank: pulled back; impact: punch lands.
            if(beat>=DisarmMotion.Beat.Strain&&disarmKicks<1){disarmKicks=1;DistanceCameraImpulse.Kick(player.PlayerCamera,new Vector3(.7f,-.3f,.8f),new Vector3(0,0,.012f));}
            if(beat>=DisarmMotion.Beat.Yank&&disarmKicks<2){disarmKicks=2;DistanceCameraImpulse.Kick(player.PlayerCamera,yankCameraKick,new Vector3(0,-.004f,-.024f));}
            if(beat>=DisarmMotion.Beat.WindUp&&disarmKicks<3){disarmKicks=3;DistanceCameraImpulse.Kick(player.PlayerCamera,new Vector3(-.8f,-.9f,1.4f),new Vector3(0,.003f,-.01f));}
            if(beat>=DisarmMotion.Beat.Impact&&disarmKicks<4){disarmKicks=4;DistanceCameraImpulse.Kick(player.PlayerCamera,shoveCameraKick,new Vector3(0,-.006f,.032f));}
        }
        void Update()
        {
            StepDisarm();
            active=weapons.GetActiveWeapon();
            CurrentRepresentation=active&&carrier.Held&&carrier.Held.EquippedWeapon==active?Representation.StolenCannon:
                active&&active.SourcePrefab==pistolPrefab.gameObject?Representation.Pistol:
                IsMounted(active)?Representation.Mounted:Representation.Normal;
            // v6: the right hand is a closed fist (NormalFist, HandPose 0) at rest and for the whole strike: tap, charge and
            // release. Only a module in hand switches to the grip pose. (v5 wrongly opened the grip during the strike.)
            poseAnimator.SetInteger(HandPose,CurrentRepresentation==Representation.Normal?0:1);
        }
        void LateUpdate()
        {
            bool disarming=IsDisarming;
            bool visible=!player.IsDead&&health.CurrentHealth>0&&(!active||active.IsWeaponActive||disarming||armOverride||(actionsEnabled&&melee&&melee.IsAttacking));
            if(fpsRoot.gameObject.activeSelf!=visible)fpsRoot.gameObject.SetActive(visible);
            if(!visible)return;
            bool replaced=CurrentRepresentation!=Representation.Normal;
            bool detached=armOverride&&overrideHandHidden;
            if(modularRightArm&&rightArmReceiverViews!=null)
                foreach(var renderer in rightArmReceiverViews)if(renderer)renderer.enabled=replaced; // v6: a bare wrist shows only the collar cap
            foreach(var renderer in rightHandRenderers)renderer.enabled=!replaced&&!detached;
            bool punching=actionsEnabled&&melee&&melee.IsAttacking&&!disarming;
            bool leftStrike=(punching&&melee.LeftStrike)||(previewLeftFist&&!disarming);bool disarmClosed=false;
            if(actionsEnabled)
            {
                // v7: F strikes with the left hand (armed or not); the left hand also appears for the disarm.
                foreach(var renderer in leftRenderers)renderer.enabled=disarming||leftStrike;
                foreach(var renderer in actionRightForearm)if(renderer.name=="Forearm_R_FPS")renderer.enabled=(!active||active.IsWeaponActive||punching||armOverride)&&!RightForearmReplaced;
                if(disarming&&leftPunchClip){disarm.Evaluate(out _,out _,out _,out bool closed);disarmClosed=closed;leftPunchClip.SampleAnimation(fpsRoot.gameObject,closed?.24f:0);}
                if(mountedWeapon!=active)
                {
                    mountedWeapon=active;nativeAttachment=null;
                    if(active&&(CurrentRepresentation==Representation.Pistol||CurrentRepresentation==Representation.Mounted||(modularRightArm&&replaced)))
                        foreach(var t in active.WeaponRoot.GetComponentsInChildren<Transform>(true))if(t.name=="Attachment"){nativeAttachment=t;break;}
                }
            }
            var frame=weapons.WeaponParentSocket;
            // v5 armed strike: the weapon on the right forearm is driven as a mechanical bash. PlayerWeaponsManager
            // rewrites the socket position every LateUpdate (bob + recoil), so the offset is additive and never accumulates.
            if(punching&&melee.ArmedStrike&&active){BashPose(out var bashPos,out var bashEuler);frame.localPosition+=bashPos;frame.localRotation=socketBindRotation*Quaternion.Euler(bashEuler);}
            else frame.localRotation=socketBindRotation;
            Vector3 leftWrist,rightWrist;
            if(replaced&&active)
            {
                var muzzle=active.WeaponMuzzle;
                rightWrist=muzzle.position+muzzle.rotation*WristOffset;
                leftWrist=muzzle.position+muzzle.rotation*SupportOffset;
            }
            else
            {
                leftWrist=frame.TransformPoint(new Vector3(-.20f,-.22f,.50f)-weapons.DefaultWeaponPosition.localPosition);
                rightWrist=frame.TransformPoint(new Vector3(.20f,-.22f,.50f)-weapons.DefaultWeaponPosition.localPosition);
            }

            var camera=player.PlayerCamera.transform;
            if(actionsEnabled&&replaced&&active&&nativeAttachment)rightWrist=nativeAttachment.position+active.WeaponMuzzle.forward*.003f;
            var rightPort=rightWrist;
            if(RightForearmReplaced&&active)
                rightWrist=rightPort+active.WeaponMuzzle.forward*(rightLength-retainedForearmLength);
            Vector3 leftElbow;Vector3 rightElbowHint=camera.TransformPoint(new Vector3(.28f,-.45f,.02f));
            bool rightJab=(punching&&!melee.LeftStrike&&!melee.ArmedStrike&&!replaced)||(previewRightFist&&!replaced&&!punching);
            if(rightJab||leftStrike)
            {
                JabPose(melee.PoseTime,out var jabWrist,out var jabElbow);
                if(melee.IsCharging)
                {
                    // v4 charged punch: the fist draws back as the charge builds and the forearm shakes harder.
                    float level=melee.ChargeLevel,t=Time.time,amp=.0025f+.009f*level*level;
                    var shake=new Vector3(Mathf.Sin(t*71f)+.5f*Mathf.Sin(t*113f),Mathf.Sin(t*83f+1.3f),.6f*Mathf.Sin(t*97f+.4f))*amp;
                    // the chambered fist comes into view (the jab chamber sits outside the horizontal FOV) and is held there
                    float show=Mathf.SmoothStep(0,1,Mathf.Clamp01(level*7f)); // v6: in view within ~0.17 s of the charge
                    jabWrist=Vector3.Lerp(jabWrist,ChargeW,show)+shake;jabElbow=Vector3.Lerp(jabElbow,ChargeE,show)+shake*.5f;
                }
                else if(melee.Power>0&&melee.PoseTime>.12f&&melee.PoseTime<.4f)
                {
                    if(melee.PoseTime<.22f)
                    {   // the strike leaves from the visible charge pose, not from the hidden chamber
                        float show=Mathf.SmoothStep(0,1,Mathf.Clamp01(melee.Power*3f)),k=Mathf.Clamp01((melee.PoseTime-.12f)/.10f);
                        jabWrist=Vector3.Lerp(Vector3.Lerp(JabChamberW,ChargeW,show),JabHitW,k);jabElbow=Vector3.Lerp(Vector3.Lerp(JabChamberE,ChargeE,show),JabHitE,k);
                    }
                    float reachBoost=Mathf.Clamp01(1-Mathf.Abs(melee.PoseTime-.24f)/.16f)*melee.Power;
                    jabWrist+=new Vector3(.01f,.01f,.09f)*reachBoost;jabElbow+=new Vector3(.005f,.005f,.07f)*reachBoost;
                }
                // the left jab as authored; the right one mirrored to the right side of the view
                if(leftStrike){leftWrist=camera.TransformPoint(jabWrist);leftElbow=camera.TransformPoint(jabElbow);}
                else{rightWrist=camera.TransformPoint(Mirror(jabWrist));rightElbowHint=camera.TransformPoint(Mirror(jabElbow));leftElbow=camera.TransformPoint(new Vector3(-.24f,-.45f,.02f));}
            }
            else leftElbow=camera.TransformPoint(new Vector3(-.24f,-.45f,.02f));
            // Inspector preview: hold the fist in the charge pose so it can be tuned live
            if(previewLeftFist&&!punching&&!disarming){leftWrist=camera.TransformPoint(ChargeW);leftElbow=camera.TransformPoint(ChargeE);}
            if(previewRightFist&&!punching&&!replaced){rightWrist=camera.TransformPoint(Mirror(ChargeW));rightElbowHint=camera.TransformPoint(Mirror(ChargeE));}
            if(disarming)
            {
                disarm.Evaluate(out var wrist,out var elbow,out float weight,out _);
                leftWrist=Vector3.Lerp(leftWrist,camera.TransformPoint(wrist),weight);leftElbow=Vector3.Lerp(leftElbow,camera.TransformPoint(elbow),weight);
            }
            FitArm(leftForearm,leftHand,leftAxis,leftHandBind,leftLength,leftWrist,leftElbow);
            if(actionsEnabled&&replaced&&active)rightElbowHint=rightWrist-active.WeaponMuzzle.forward;
            if(armOverride){rightWrist=Vector3.Lerp(rightWrist,camera.TransformPoint(overrideWrist),overrideWeight);rightElbowHint=Vector3.Lerp(rightElbowHint,camera.TransformPoint(overrideElbow),overrideWeight);}
            FitArm(rightForearm,rightHand,rightAxis,rightHandBind,rightLength,rightWrist,rightElbowHint);
            // v7: the striking hand is a real fist from the first frame of the tap / charge until it reopens after the strike
            if(handPoses)
            {
                bool rightClosed=closedFist&&rightJab&&!detached,leftClosed=closedFist&&(leftStrike||(disarming&&disarmClosed));
                handPoses.RequestRight(rightClosed?HandPoseLibrary.Pose.Fist:replaced?HandPoseLibrary.Pose.Grip:HandPoseLibrary.Pose.Relaxed,rightClosed?.06f:.15f);
                handPoses.RequestLeft(leftClosed?HandPoseLibrary.Pose.Fist:HandPoseLibrary.Pose.Relaxed,leftClosed?.06f:.15f);
            }
            else
            {
                // Legacy lab scenes retain their serialized rig until explicitly migrated; only one branch writes fingers.
                rightFist.Apply(closedFist&&rightJab&&!detached);
                leftFist.Apply(closedFist&&(leftStrike||(disarming&&disarmClosed)));
                if(leftFist.Weight>0&&leftFistWristEuler!=Vector3.zero)leftHand.rotation=leftHand.rotation*Quaternion.Slerp(Quaternion.identity,Quaternion.Euler(leftFistWristEuler),leftFist.Weight);
                if(rightFist.Weight>0&&rightFistWristEuler!=Vector3.zero)rightHand.rotation=rightHand.rotation*Quaternion.Slerp(Quaternion.identity,Quaternion.Euler(rightFistWristEuler),rightFist.Weight);
            }
            PoseWristCollar(rightPort,RightForearmReplaced&&active);
            if(armSleeves)
            {
                FitSleeve(leftSleeve,leftSleeveView,leftForearmView,leftForearm,leftAxis,leftShoulder);
                FitSleeve(rightSleeve,rightSleeveView,rightForearmView,rightForearm,rightAxis,rightShoulder);
            }
            if(replaced&&active)rightModuleSocket.rotation=active.WeaponMuzzle.rotation;
            if(RightForearmReplaced&&active)rightModuleSocket.SetPositionAndRotation(rightPort,active.WeaponMuzzle.rotation);
            else if(modularRightArm){rightModuleSocket.localPosition=moduleSocketBindPosition;rightModuleSocket.localRotation=moduleSocketBindRotation;}
        }
        // F5: straight jab along the view instead of the clip's rising (uppercut) path. Pose time: 0-.12 wind-up,
        // .12-.22 extension, .22-.26 contact, .26-.56 recovery (ProtagonistMelee). Camera-space metres.
        [SerializeField] bool straightJab=true;
        static readonly Vector3 JabGuardW=new Vector3(-.17f,-.23f,.22f),JabGuardE=new Vector3(-.28f,-.40f,-.02f);
        static readonly Vector3 JabChamberW=new Vector3(-.19f,-.25f,.13f),JabChamberE=new Vector3(-.31f,-.40f,-.10f);
        static readonly Vector3 ChargeW=new Vector3(-.10f,-.125f,.36f),ChargeE=new Vector3(-.23f,-.32f,.09f); // v6/v7: whole fist in frame, below-left of the crosshair (left hand)
        static readonly Vector3 JabHitW=new Vector3(-.055f,-.115f,.60f),JabHitE=new Vector3(-.13f,-.19f,.31f);
        static Vector3 Mirror(Vector3 v)=>new Vector3(-v.x,v.y,v.z);
        // v5 armed bash (socket space, metres / degrees): pull back and cock, drive forward with a twist, recover.
        static readonly Vector3 BashBackP=new Vector3(.035f,-.045f,-.11f),BashBackR=new Vector3(-14f,6f,-10f);
        static readonly Vector3 BashHitP=new Vector3(-.05f,.025f,.24f),BashHitR=new Vector3(9f,-8f,22f);
        void BashPose(out Vector3 position,out Vector3 euler)
        {
            float t=melee.PoseTime;
            if(melee.IsCharging)
            {
                float level=melee.ChargeLevel,amp=.002f+.006f*level*level,time=Time.time;
                var shake=new Vector3(Mathf.Sin(time*71f),Mathf.Sin(time*83f+1.3f),Mathf.Sin(time*97f+.4f))*amp;
                position=BashBackP*(1f+.35f*level)+shake;euler=BashBackR*(1f+.3f*level)+shake*300f;return;
            }
            float reach=1f+.35f*melee.Power;
            if(t<.12f){float k=Mathf.SmoothStep(0,1,t/.12f);position=BashBackP*k;euler=BashBackR*k;return;}
            if(t<.22f){float k=Mathf.Clamp01((t-.12f)/.10f);k*=k;position=Vector3.Lerp(BashBackP,BashHitP*reach,k);euler=Vector3.Lerp(BashBackR,BashHitR,k);return;}
            if(t<.26f){position=BashHitP*reach;euler=BashHitR;return;}
            {float k=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.26f)/.30f));position=Vector3.Lerp(BashHitP*reach,Vector3.zero,k);euler=Vector3.Lerp(BashHitR,Vector3.zero,k);}
        }
        static void JabPose(float t,out Vector3 wrist,out Vector3 elbow)
        {
            if(t<.12f){float k=Mathf.SmoothStep(0,1,t/.12f);wrist=Vector3.Lerp(JabGuardW,JabChamberW,k);elbow=Vector3.Lerp(JabGuardE,JabChamberE,k);return;}
            if(t<.22f){float k=Mathf.Clamp01((t-.12f)/.10f);wrist=Vector3.Lerp(JabChamberW,JabHitW,k);elbow=Vector3.Lerp(JabChamberE,JabHitE,k);return;}
            if(t<.26f){wrist=JabHitW;elbow=JabHitE;return;}
            {float k=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.26f)/.30f));wrist=Vector3.Lerp(JabHitW,JabGuardW,k);elbow=Vector3.Lerp(JabHitE,JabGuardE,k);}
        }
        static void FitArm(Transform forearm,Transform hand,Vector3 axis,Quaternion bind,float length,Vector3 wrist,Vector3 elbowHint)
        {
            Vector3 direction=(wrist-elbowHint).normalized;
            forearm.rotation=Quaternion.FromToRotation(forearm.TransformDirection(axis),direction)*forearm.rotation;
            forearm.position=wrist-direction*length;
            hand.SetPositionAndRotation(wrist,forearm.rotation*bind);
        }
        void OnDisable(){if(carrier)carrier.Executed-=OnExecuted;if(disarm!=null)disarm.Cancel();if(fpsRoot)fpsRoot.gameObject.SetActive(false);}
    }
}
