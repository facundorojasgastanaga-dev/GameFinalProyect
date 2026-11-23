using System.Collections.Generic;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace IronRequiem
{
    // Big Bot execution (executions-v02, 22/11/2026; replaces the 2.30 s leap-on-top version).
    // The player does NOT climb the robot: he leaps sideways to a perch hanging next to the cannon shoulder (CannonPivot + 0.45 m
    // towards the player + 0.10 m up, at camera level), grabs the real joint, wrenches the cannon off, mounts it on the right arm
    // and shoots the robot's core with it at point-blank range, then jumps back. The view is always the player's (no aim override):
    // the weapon, not the camera, turns to the core (capped at 35 degrees).
    // Inventory is already updated by StolenWeaponCarrier before Begin; the carrier no longer switches weapons at E.
    // This component owns the clock and the staging; ExecutionDirector does the per-beat work common to every execution
    // (kick, trauma, hit-stop, RaiseBeat, proxy weapon, arc FX). The robot hides its own cannon and plays all the audio.
    // Movement goes through AdvancedPlayerMovement's scripted velocity, so the controller stays the only mover.
    //
    // Clock (s):  0 Begin/leap  .28 hand starts  .40 Grab (land)  .46 Struggle  .62 Rip (blow 1)  .72 proxy tossed  .80 Handoff
    //             (cannon mounted, weapon aim .80-.92)  .95 Strike (shot + blow 2 + death)  1.05 Recover (jump back)  1.50 End (control back).
    [DefaultExecutionOrder(90)]
    [RequireComponent(typeof(AdvancedPlayerMovement),typeof(StolenWeaponCarrier))]
    public sealed class BigBotExecutionSequence : MonoBehaviour
    {
        [SerializeField,Min(.1f)] float leapSecondsV2=.40f;
        [SerializeField,Min(0)] float leapArcV2=.35f;
        [SerializeField,Min(0)] float handStartV2=.28f;
        [SerializeField,Min(.1f)] float ripAtV2=.62f;
        [SerializeField,Min(.1f)] float handoffAtV2=.80f;
        [SerializeField,Min(.1f)] float shotAtV2=.95f;
        [SerializeField,Min(.1f)] float backStartV2=.95f; // the jump back starts on the shot frame
        [SerializeField,Min(.1f)] float busyUntilV2=1.05f;
        [SerializeField,Min(.2f)] float endAtV2=1.40f;
        [SerializeField,Min(0)] float backArcV2=.6f;
        [Tooltip("Where CannonPivot sits in camera space when the player lands (metres).")]
        [SerializeField] Vector3 landingPivotInCameraV2=new Vector3(-.15f,-.10f,.40f);
        [Tooltip("The cannon turns to the core by at most this many degrees, blended in over weaponAimSecondsV2 after the mount.")]
        [SerializeField,Range(0,60)] float weaponAimMaxDegreesV2=35;
        [SerializeField,Min(.01f)] float weaponAimSecondsV2=.12f;
        [Header("Muzzle flash light (only when the existing muzzle flash prefab has no Light)")]
        [SerializeField,Min(.01f)] float flashLightSeconds=.06f;
        [SerializeField,Min(.5f)] float flashLightRange=4;
        [SerializeField,Min(0)] float flashLightIntensity=3;
        [SerializeField] Color flashLightColor=new Color(1,.72f,.4f);
        [Tooltip("Landing spot of the jump back when the starting point is blocked: metres from the perch, away from the robot.")]
        [SerializeField,Min(1)] float backFallbackDistanceV2=4.5f;
        [Tooltip("Perch: horizontal distance from CannonPivot, beside the shoulder (camera at shoulder height).")]
        [SerializeField,Min(.3f)] float perchSideDistanceV2=.42f;
        [Header("Soft view assist (no hard aim override): degrees per second at 60 FPS = value x 60")]
        [SerializeField,Range(0,1.2f)] float viewAssistDegreesPerFrameV2=1.2f;
        const float TossLead=.10f,TossSeconds=.08f,FlyMargin=.08f;

        AdvancedPlayerMovement movement;StolenWeaponCarrier carrier;PlayerCharacterController player;Health health;
        CharacterController body;ProtagonistHandsPresentation hands;ExecutionDirector director;
        EnemyExecutionSource target;BigBotCombat bot;BigBotDestruction destruction;Transform joint;ExecutionBodyProxy proxy;
        readonly List<Collider> ignored=new List<Collider>();
        readonly Collider[] overlaps=new Collider[24];
        readonly RaycastHit[] rays=new RaycastHit[16];
        Vector3 start,perch,grip,backPoint,bodyCentre,anchorLocal,anchorToCamera,eyeOffset;float clock,switchAt;
        bool running,delivered,handStarted,switchDone,tossed,backStarted,holding;
        PlayerInputHandler lookInput;DistanceCameraImpulse impulse;Vector3 lastKick;float kickRate;
        public bool IsRunning=>running;
        // Weapon switch / G / melee stay blocked until the jump back starts.
        public bool Busy=>running&&clock<busyUntilV2;
        public float Clock=>clock;
        public int ShotCount {get;private set;}
        public float TotalTime=>endAtV2;
        public Vector3 Perch=>perch;
        // Diagnostics: last collider the CharacterController touched while the sequence ran.
        public string LastContact {get;private set;}="none";
        void OnControllerColliderHit(ControllerColliderHit hit){if(IsRunning&&hit.collider&&hit.normal.y<.5f)LastContact=hit.collider.name+" ("+(hit.collider.transform.root?hit.collider.transform.root.name:"-")+") n="+hit.normal.ToString("F2")+" ignoredCount="+ignored.Count;}

        PlayerWeaponsManager weaponsManager;
        void Awake()
        {
            weaponsManager=GetComponent<PlayerWeaponsManager>();movement=GetComponent<AdvancedPlayerMovement>();carrier=GetComponent<StolenWeaponCarrier>();player=GetComponent<PlayerCharacterController>();
            health=GetComponent<Health>();body=GetComponent<CharacterController>();hands=GetComponent<ProtagonistHandsPresentation>();lookInput=GetComponent<PlayerInputHandler>();
        }
        void OnEnable(){if(health)health.OnDie+=Cancel;}
        void OnDisable()
        {
            if(health)health.OnDie-=Cancel;
            if(!running)return;
            // Teardown/disable: release the player but do not trigger a kill while objects are being destroyed.
            if(movement){movement.EndScriptedMove();movement.ClearAimOverride();}
            if(hands)hands.ClearWeaponAimOffset();
            if(director)director.Abort();
            if(bot)bot.HoldAim(false);RestoreCollisions();running=false;target=null;bot=null;destruction=null;joint=null;
        }

        public bool Begin(EnemyExecutionSource source,Vector3 gripPoint,StolenCannon module,Quaternion moduleRotation,WeaponController stolen,WeaponController outgoing)
        {
            if(running||!source||!movement||!movement.enabled||!body)return false;
            director=GetComponent<ExecutionDirector>();if(!director)return false;
            bot=source.GetComponent<BigBotCombat>();if(!bot)return false;
            bot.HoldAim(true); // the cannon stays where the player saw it when pressing E
            target=source;destruction=source.GetComponent<BigBotDestruction>();grip=gripPoint;delivered=false;
            joint=source.WeaponJoint;proxy=source.GetComponent<ExecutionBodyProxy>();
            // Freeze the robot: no new attacks, no path following. Its health and death flow stay untouched.
            var mobile=source.GetComponent<EnemyMobile>();if(mobile)mobile.enabled=false;
            var enemy=source.GetComponent<EnemyController>();
            if(enemy&&enemy.NavMeshAgent&&enemy.NavMeshAgent.enabled&&enemy.NavMeshAgent.isOnNavMesh){enemy.NavMeshAgent.isStopped=true;enemy.NavMeshAgent.velocity=Vector3.zero;}
            ignored.Clear();Bounds bounds=new Bounds(source.transform.position+Vector3.up,Vector3.one);bool first=true;
            foreach(var c in source.GetComponentsInChildren<Collider>())
            {
                if(!c.enabled||c.isTrigger)continue;
                if(first){bounds=c.bounds;first=false;}else bounds.Encapsulate(c.bounds);
                ignored.Add(c);
            }
            // The scripted move drives the root, so the eye offset is subtracted from every camera target.
            var camera=player.PlayerCamera.transform;
            start=transform.position;
            var eye=camera.position-transform.position;
            // Perch (round 2): IN FRONT of the cannon arm, on the side the player comes from (see ChoosePerchCamera).
            var views=ExecutionReachResolver.WeaponViews(source);var colliders=ignored.ToArray();
            var eyeStart=camera.position;
            var front=Vector3.ProjectOnPlane(eyeStart-bounds.center,Vector3.up);
            front=front.sqrMagnitude>1e-4f?front.normalized:-source.transform.forward;
            var cameraAtLanding=ChoosePerchCamera(eyeStart,front,bounds,colliders,views,out var grabAt);GrabPoint=grabAt;bodyCentre=bounds.center;
            // hanging on the cannon: while holding, the camera keeps this offset to the grab, which rides the cannon arm bone
            anchorPart=-1;if(!(proxy&&proxy.WeaponPoint(grabAt,out _,out anchorPart,out anchorLocal)))anchorLocal=joint?joint.InverseTransformPoint(grabAt):grabAt;
            anchorToCamera=cameraAtLanding-grabAt;eyeOffset=eye;
            perch=cameraAtLanding-eye;
            IgnoreRobot(true);
            // The leap is a short hop up and across the robot's front: low arc, bent away from the body if the straight path grazes it.
            movement.BeginScriptedMove(perch,leapSecondsV2,leapArcV2,LeapBend(transform.position,perch,eye,colliders,views,bounds.center));
            holding=false;handStarted=false;switchDone=false;tossed=false;backStarted=false;clock=0;running=true;lastKick=Vector3.zero;kickRate=0;impulse=camera.GetComponent<DistanceCameraImpulse>();
            switchAt=director.SwitchStartFor(handoffAtV2,ripAtV2,outgoing);
            director.BeginBig(source,module,moduleRotation,stolen,OnBeat);
            return true;
        }

        Vector3 JointPoint()=>joint?joint.position:destruction&&destruction.CannonPivot?destruction.CannonPivot.position:grip;
        // Where the hand closes on the cannon: the cannon parts' surface (ExecutionBodyProxy) closest to `from`; without a proxy, the
        // cannon arm bone segment (joint -> Pincer_R).
        Vector3 CannonGrabPoint(Vector3 from)
        {
            if(proxy&&proxy.WeaponPoint(from,out var surface))return surface;
            return ExecutionReachResolver.CannonArmPoint(joint,from,out var p)?p:JointPoint();
        }
        // Distance to the robot's VISIBLE body: the per-part proxy when the prefab has one (the Big Bot's only collider is a box far
        // larger than its silhouette), otherwise its colliders. The cannon never counts as body.
        float Body(Vector3 point,Collider[] colliders,Renderer[] views,out Vector3 nearest)=>
            proxy&&proxy.HasParts?proxy.BodyDistance(point,out nearest):ExecutionReachResolver.BodyDistance(point,colliders,joint,views,out nearest);
        public Vector3 GrabPoint {get;private set;}
        public string PerchInfo {get;private set;}="-";
        // The grab spot on the armour, in the last rendered pose (ExecutionBodyProxy), else on the cannon arm bone.
        public Vector3 AnchorWorld=>proxy&&anchorPart>=0?proxy.PartPoint(anchorPart,anchorLocal,GrabPoint):joint?joint.TransformPoint(anchorLocal):GrabPoint;
        int anchorPart=-1;

        // The grab walks the cannon arm (joint -> Pincer_R, 15 % to 95 %): for each arm point, the grab is the cannon surface facing the
        // player there and the camera hangs perchSideDistanceV2 in front of it (shifted sideways by 0 / +-0.15 / +-0.30 m), at grab height
        // but never under the standing eye height nor less than 0.5 m under the robot's top (the leap never goes over the shoulder).
        // Kept only when the grab is within the arm reach and the camera is 0.40 m clear of the visible body WITHOUT pushing it; the
        // winner is the spot closest to the robot's centre line (the robot stays in front of the view), then the shortest reach.
        // No candidate: the eye-closest grab, pushed out horizontally until clear of the body (round-1 behaviour).
        Vector3 ChoosePerchCamera(Vector3 eyeStart,Vector3 front,Bounds bounds,Collider[] colliders,Renderer[] views,out Vector3 grabAt)
        {
            float hi=bounds.max.y-.5f,lo=Mathf.Min(eyeStart.y,hi);
            float reachMax=hands?hands.DisarmMaxGripReach-.10f:.45f; // 0.10 m spare for the robot's sway while the player hangs
            var side=Vector3.Cross(Vector3.up,front);
            var tip=joint?joint.Find("Pincer_R"):null;var a=JointPoint();
            float best=float.MaxValue;Vector3 bestCam=Vector3.zero;grabAt=CannonGrabPoint(eyeStart);int tried=0,kept=0;float bestReach=0,bestBody=0;
            if(tip)
                for(int i=0;i<=16;i++)
                {
                    var along=Vector3.Lerp(a,tip.position,.15f+.05f*i);
                    var g=CannonGrabPoint(along+front*2f);
                    for(int j=0;j<5;j++)
                    {
                        float lat=j==0?0:(j%2==1?1:-1)*((j+1)/2)*.15f;tried++;
                        var cam=g+front*perchSideDistanceV2+side*lat;
                        cam.y=Mathf.Clamp(g.y+.05f,lo,hi);
                        var hold=CannonGrabPoint(cam);float reach=Vector3.Distance(cam,hold);if(reach>reachMax||reach<.25f)continue;
                        float body=Body(cam,colliders,views,out _);if(body<ExecutionReachResolver.BodyMargin)continue;
                        kept++;
                        float score=Mathf.Abs(Vector3.Dot(Vector3.ProjectOnPlane(cam-bounds.center,Vector3.up),side))+.5f*reach;
                        if(score<best){best=score;bestCam=cam;grabAt=hold;bestReach=reach;bestBody=body;}
                    }
                }
            PerchInfo=$"proxy {(proxy?proxy.PartCount:0)} parts, candidates {kept}/{tried}, reach {bestReach:F2}, body {bestBody:F2}, tip {(tip?tip.position.ToString("F2"):"-")}, joint {a.ToString("F2")}";
            if(best<float.MaxValue)return bestCam;
            var fallback=grabAt+front*perchSideDistanceV2;
            fallback.y=Mathf.Clamp(grabAt.y+.05f,lo,hi);
            return ClearOfBody(fallback,colliders,views,bounds.center);
        }

        void Update()
        {
            if(!running)return;
            if(!target||player.IsDead){Cancel();return;}
            clock+=Time.deltaTime;
            if(!handStarted&&clock>=handStartV2){handStarted=true;carrier.NotifyRip(target,JointPoint());} // hands: RipOnly profile, follows the live joint
            if(!tossed&&clock>=ripAtV2+TossLead){tossed=true;if(director.Carry)director.Carry.BeginThrow(TossSeconds);}
            if(!switchDone&&clock>=switchAt){switchDone=true;carrier.ExecutionShowHeld();}
            FollowCannon();
            UpdateViewAssist();
            UpdateWeaponAim();
            director.Tick(clock);
            if(running&&clock>=endAtV2)Finish();
        }

        // From the landing to the rip the player hangs on the cannon: the robot's reaction (landing sink, roll pulses, tugs) sways the
        // arm by ~0.3 m, so the root follows the grab anchor on the arm bone with a short eased move each frame (no rotation).
        void FollowCannon()
        {
            if(!holding||backStarted||clock>=ripAtV2||!joint)return;
            var want=AnchorWorld+anchorToCamera-eyeOffset;
            if((want-transform.position).sqrMagnitude>1e-4f)movement.BeginScriptedMove(want,.05f,0); // ~40 % of the gap per frame
        }

        // Soft view assist (no hard aim override): the view turns to the cannon during the leap and the grab and to the core for the shot,
        // capped at viewAssistDegreesPerFrameV2 (x dt x 60) minus whatever the camera kick is already turning this frame, and off while the
        // mouse moves. Implemented through AdvancedPlayerMovement.SetAimOverride with a per-frame sharpness that enforces the cap.
        void UpdateViewAssist()
        {
            // off from 40 ms before the shot and +-50 ms around the rip: their kicks (up to 1.5 deg/frame) must not stack with the assist
            if(backStarted||viewAssistDegreesPerFrameV2<=0||clock>=shotAtV2-.04f||Mathf.Abs(clock-ripAtV2)<.05f){movement.ClearAimOverride();return;}
            var cam=player.PlayerCamera.transform;
            if(!impulse)impulse=cam.GetComponent<DistanceCameraImpulse>();
            if(impulse){var k=impulse.KickAngle;kickRate=(k-lastKick).magnitude;lastKick=k;}
            if(lookInput&&(Mathf.Abs(lookInput.GetLookInputsHorizontal())>.01f||Mathf.Abs(lookInput.GetLookInputsVertical())>.01f)){movement.ClearAimOverride();return;}
            // coordinator: frame the cannon AND the robot (half way to the core), not the cannon alone half a metre from the eye
            // round 2: half way between the hand's grab and the robot's centre, so the cannon, the hand and the body share the frame
            var point=Vector3.Lerp(GrabPoint,bodyCentre,.5f); // fixed: the live surface point jumped between samples
            if(clock>handoffAtV2)point=Vector3.Lerp(point,Core(),Mathf.SmoothStep(0,1,(clock-handoffAtV2)/(shotAtV2-handoffAtV2)));
            var dir=point-cam.position;if(dir.sqrMagnitude<1e-4f)return;dir.Normalize();
            float yawDelta=Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y,Mathf.Atan2(dir.x,dir.z)*Mathf.Rad2Deg));
            float pitchNow=cam.localEulerAngles.x;if(pitchNow>180)pitchNow-=360;
            float pitchDelta=Mathf.Abs(-Mathf.Asin(Mathf.Clamp(dir.y,-1,1))*Mathf.Rad2Deg-pitchNow);
            float delta=Mathf.Max(yawDelta,pitchDelta),dt=Mathf.Max(Time.deltaTime,1e-4f);
            float budget=Mathf.Clamp(.95f-kickRate,0,Mathf.Min(.9f,viewAssistDegreesPerFrameV2))*dt*60f; // degrees this frame: assist + kick stay under 1.5 deg/frame (the kick rate is last frame's)
            float sharp=delta<=budget?12f:Mathf.Min(12f,-Mathf.Log(Mathf.Max(1e-4f,1-budget/delta))/dt);
            movement.SetAimOverride(point,sharp);
        }
        void UpdateWeaponAim()
        {
            if(!hands||weaponAimMaxDegreesV2<=0)return;
            if(clock<handoffAtV2||backStarted)return; // cleared by BeginJumpBack
            var camera=player.PlayerCamera.transform;
            var wanted=Quaternion.FromToRotation(camera.forward,(Core()-camera.position).normalized);
            wanted=Quaternion.RotateTowards(Quaternion.identity,wanted,weaponAimMaxDegreesV2);
            hands.SetWeaponAimOffset(Quaternion.Slerp(Quaternion.identity,wanted,Mathf.Clamp01((clock-handoffAtV2)/weaponAimSecondsV2)));
        }

        // Called by ExecutionDirector for each beat: pre = before RaiseBeat, post = after it.
        void OnBeat(ExecutionBeat beat,bool post)
        {
            if(!running)return;
            if(post){if(beat==ExecutionBeat.Strike)Deliver();return;}
            switch(beat)
            {
                case ExecutionBeat.Grab:
                    // landed: hold the perch (a 1000 s move from the current spot; FollowCannon then rides the arm until the rip)
                    movement.BeginScriptedMove(transform.position,1000,0);holding=true;
                    // round 2: the robot stays ignored while the player hangs on its cannon (its single box collider is far larger than
                    // the armour and blocked FollowCannon); restored by Finish / Cancel after the jump back.
                    break;
                case ExecutionBeat.Rip:
                {
                    var at=JointPoint();
                    carrier.ExecutionBlow(target,at); // blow 1 (the robot hides its cannon on this same beat)
                    float depth=player.PlayerCamera.transform.InverseTransformPoint(at).z;
                    if(director.Carry)director.Carry.Show(hands&&depth>hands.DisarmMaxGripReach+FlyMargin);
                    director.RipEffects(at);
                    break;
                }
                case ExecutionBeat.Handoff:
                    if(director.Carry)director.Carry.AllowSwap();
                    // Round 3: the cannon lands on the right arm and IS the mounted weapon from this frame (the outgoing weapon has
                    // been going down since switchAt). Without the snap the bazooka rose only at ~1.4 s: the shot at 0.95 s
                    // was fired with the old weapon still in hand and the tossed proxy parked at the lens. The carry proxy hides
                    // on the first frame the stolen weapon is active.
                    if(switchDone&&weaponsManager&&carrier.Held&&weaponsManager.GetActiveWeapon()!=carrier.Held.EquippedWeapon)weaponsManager.SnapActiveWeaponUp();
                    break;
                case ExecutionBeat.Strike:Shoot();break;
                case ExecutionBeat.Recover:BeginJumpBack();break;
            }
        }

        Vector3 Core()
        {
            if(!target)return perch;
            var aim=target.GetComponentInChildren<BigBotDetectionModule>();
            Vector3 core=aim&&aim.DetectionSourcePoint?aim.DetectionSourcePoint.position:target.transform.position+Vector3.up*1.6f;
            return core+Vector3.down*.35f;
        }

        void Shoot()
        {
            if(delivered||!target)return;
            ShotCount++;
            // Presentation shot with the stolen cannon: its existing muzzle flash. Energy is not consumed; the execution kill
            // (Deliver, right after the Strike beat) is the damage. No sound here: the robot-side execution audio owns it.
            var weapon=carrier.Held?carrier.Held.EquippedWeapon:null;
            if(weapon)
            {
                var muzzle=weapon.WeaponMuzzle;bool lit=false;
                // the flash never spawns closer than 0.6 m to the camera and never covers more than ~20 % of the frame height
                var camT=player.PlayerCamera.transform;var at=muzzle.position;
                float away=Vector3.Distance(at,camT.position);
                if(away<MinFlashDistance){at=camT.position+(at-camT.position).normalized*MinFlashDistance;if((at-camT.position).sqrMagnitude<1e-4f)at=camT.position+camT.forward*MinFlashDistance;away=MinFlashDistance;}
                if(weapon.MuzzleFlashPrefab)
                {
                    var flash=Instantiate(weapon.MuzzleFlashPrefab,at,muzzle.rotation,muzzle);Destroy(flash,2);
                    flash.transform.position=at;
                    float extent=FlashExtent(flash);
                    float allowed=.2f*2f*Mathf.Tan(player.PlayerCamera.fieldOfView*.5f*Mathf.Deg2Rad)*away;
                    if(extent>allowed&&extent>1e-3f)flash.transform.localScale*=allowed/extent;
                    lit=flash.GetComponentInChildren<Light>(true);
                }
                if(!lit)SpawnFlashLight(at+muzzle.forward*.1f);
            }
            carrier.ExecutionBlow(target,Core()); // blow 2, the kill follows
        }

        const float MinFlashDistance=.6f;
        // Largest world-space size of a freshly instantiated flash: mesh/sprite bounds and particle start sizes.
        static float FlashExtent(GameObject flash)
        {
            float extent=.3f;
            foreach(var r in flash.GetComponentsInChildren<Renderer>(true))
                if(!(r is ParticleSystemRenderer)){var s=r.bounds.size;extent=Mathf.Max(extent,s.x,s.y,s.z);}
            foreach(var ps in flash.GetComponentsInChildren<ParticleSystem>(true))
                extent=Mathf.Max(extent,ps.main.startSizeMultiplier*ps.transform.lossyScale.x);
            return extent;
        }
        // The camera never sits closer than 0.40 m to the robot's body (the cannon being grabbed does not count): push it out
        // from the nearest body point (or from the robot's centre when inside it).
        Vector3 ClearOfBody(Vector3 camPos,Collider[] colliders,Renderer[] views,Vector3 centre)
        {
            for(int i=0;i<10;i++)
            {
                float d=Body(camPos,colliders,views,out var near);
                if(d>=ExecutionReachResolver.BodyMargin)break;
                var outward=camPos-near;outward.y=0;if(outward.sqrMagnitude<1e-6f){outward=camPos-centre;outward.y=0;} // horizontal only: never up onto the armour
                if(outward.sqrMagnitude<1e-6f)outward=-transform.forward;
                camPos+=outward.normalized*(ExecutionReachResolver.BodyMargin-d+.03f);
            }
            return camPos;
        }
        // Sideways bulge for the leap so the camera never passes closer than 0.40 m to the body: samples the exact path that
        // AdvancedPlayerMovement follows (smoothstep across, ease-out up, sin arc and bend) and widens the bend, away from the robot's
        // centre, in 0.15 m steps up to 1.2 m.
        Vector3 LeapBend(Vector3 from,Vector3 to,Vector3 eye,Collider[] colliders,Renderer[] views,Vector3 centre)
        {
            var path=Vector3.ProjectOnPlane(to-from,Vector3.up);
            var side=Vector3.Cross(Vector3.up,path);if(side.sqrMagnitude<1e-6f)return Vector3.zero;side.Normalize();
            if(Vector3.Dot(side,Vector3.Lerp(from,to,.5f)-centre)<0)side=-side;
            for(int k=0;k<=8;k++)
            {
                var bend=side*(k*.15f);bool clear=true;
                for(int i=1;i<10&&clear;i++)
                {
                    float s=i/10f,e=s*s*(3-2*s),v=1-(1-s)*(1-s)*(1-s),bump=Mathf.Sin(Mathf.PI*s);
                    var flat=Vector3.Lerp(from,to,e)+bend*bump;
                    var cam=new Vector3(flat.x,Mathf.Lerp(from.y,to.y,v)+leapArcV2*bump,flat.z)+eye;
                    if(Body(cam,colliders,views,out _)<ExecutionReachResolver.BodyMargin)clear=false;
                }
                if(clear)return bend;
            }
            return side*1.2f;
        }
        void SpawnFlashLight(Vector3 position)
        {
            var go=new GameObject("IR_BigBotShotFlash");go.transform.position=position;
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.range=flashLightRange;light.intensity=flashLightIntensity;
            light.color=flashLightColor;light.shadows=LightShadows.None;
            Destroy(go,flashLightSeconds);
        }

        void BeginJumpBack()
        {
            if(backStarted)return;backStarted=true;movement.ClearAimOverride();
            if(hands)hands.ClearWeaponAimOffset();
            backPoint=ComputeBackPoint();
            IgnoreRobot(true);
            movement.BeginScriptedMove(backPoint,Mathf.Max(.1f,endAtV2-backStartV2),backArcV2);
            holding=false;
        }

        // The jump back must put the camera well away from the robot's core (the death burst is huge): the starting spot when it is free
        // and at least 3.5 m from the robot, otherwise a point backFallbackDistanceV2 (4.5 m) from the robot, away from the perch, on
        // the NavMesh and clear of geometry. Falls back to the starting spot.
        Vector3 ComputeBackPoint()
        {
            var robotCenter=target?target.transform.position:perch;
            float startGap=Vector3.ProjectOnPlane(start-robotCenter,Vector3.up).magnitude;
            if(startGap>=2.0f&&IsFree(start))return start; // F1 §3.3 lands back at the start when free (it is ~2.5-3 m from the core)
            var away=Vector3.ProjectOnPlane(transform.position-robotCenter,Vector3.up);
            away=away.sqrMagnitude>1e-4f?away.normalized:-transform.forward;
            for(int i=0;i<5;i++)
            {
                float angle=(i==0?0:(i%2==1?1:-1)*((i+1)/2)*35f);
                var dir=Quaternion.Euler(0,angle,0)*away;
                var probe=robotCenter+dir*backFallbackDistanceV2;
                if(!GroundAt(probe+Vector3.up*.6f,out var ground))continue;
                if(NavMesh.SamplePosition(ground,out var nav,1.5f,NavMesh.AllAreas))ground=nav.position;
                if(IsFree(ground)&&PathClear(transform.position,ground))return ground;
            }
            return start;
        }

        bool GroundAt(Vector3 from,out Vector3 point)
        {
            point=from;
            int n=Physics.RaycastNonAlloc(from+Vector3.up*3,Vector3.down,rays,30,-1,QueryTriggerInteraction.Ignore);
            float best=float.MaxValue;
            for(int i=0;i<n;i++)
            {
                var c=rays[i].collider;
                if(!c||c.transform.IsChildOf(transform)||ignored.Contains(c)||rays[i].normal.y<.6f)continue;
                if(rays[i].distance<best){best=rays[i].distance;point=rays[i].point;}
            }
            return best<float.MaxValue;
        }

        // The player capsule standing with its root at p touches nothing but the player and the robot.
        bool IsFree(Vector3 p)
        {
            float scale=Mathf.Max(transform.lossyScale.y,.01f);float radius=body.radius*scale*.95f;float half=Mathf.Max(body.height*scale*.5f-radius,0);
            var center=p+body.center*scale+Vector3.up*.06f;
            int n=Physics.OverlapCapsuleNonAlloc(center+Vector3.up*half,center-Vector3.up*half,radius,overlaps,-1,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++){var c=overlaps[i];if(c&&!c.transform.IsChildOf(transform)&&!ignored.Contains(c))return false;}
            return true;
        }

        // Nothing solid (other than the robot) between the two root positions at chest height.
        bool PathClear(Vector3 from,Vector3 to)
        {
            var a=from+Vector3.up*1.0f;var b=to+Vector3.up*1.0f;var d=b-a;float length=d.magnitude;if(length<.05f)return true;
            int n=Physics.RaycastNonAlloc(a,d/length,rays,length,-1,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++){var c=rays[i].collider;if(c&&!c.transform.IsChildOf(transform)&&!ignored.Contains(c))return false;}
            return true;
        }

        bool RobotOverlapsPlayer()
        {
            float scale=Mathf.Max(transform.lossyScale.y,.01f);float radius=body.radius*scale+.05f;float half=Mathf.Max(body.height*scale*.5f-radius,0);
            var center=transform.position+body.center*scale;
            int n=Physics.OverlapCapsuleNonAlloc(center+Vector3.up*half,center-Vector3.up*half,radius,overlaps,-1,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++)if(overlaps[i]&&ignored.Contains(overlaps[i]))return true;
            return false;
        }

        void IgnoreRobot(bool value){foreach(var c in ignored)if(c&&body)Physics.IgnoreCollision(body,c,value);}
        void RestoreCollisions(){foreach(var c in ignored)if(c&&body)Physics.IgnoreCollision(body,c,false);}
        void Deliver(){if(delivered)return;delivered=true;if(target)target.Complete();}

        void Finish()
        {
            if(!running)return;
            movement.EndScriptedMove();movement.ClearAimOverride();if(hands)hands.ClearWeaponAimOffset();RestoreCollisions();
            running=false;
            if(bot)bot.HoldAim(false);
            carrier.ExecutionFinished();
            Deliver();director.Finish();
            target=null;bot=null;destruction=null;joint=null;holding=false;
        }
        // Death, disable or a vanished target: never leave the player scripted or the robot reserved.
        public void Cancel()
        {
            if(!running)return;
            running=false;
            if(movement){movement.EndScriptedMove();movement.ClearAimOverride();}
            if(hands)hands.ClearWeaponAimOffset();
            RestoreCollisions();
            if(director)director.Abort();
            if(bot)bot.HoldAim(false);
            carrier.ExecutionAbort();
            Deliver();target=null;bot=null;destruction=null;joint=null;holding=false;
        }
    }
}
