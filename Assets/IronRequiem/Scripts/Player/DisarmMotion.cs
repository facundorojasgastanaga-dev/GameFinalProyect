using System;
using UnityEngine;

namespace IronRequiem
{
    // Timeline for the left-arm "grab, wrench the weapon off, then punch the robot" presentation.
    // Plain data + math; the owner (ProtagonistHandsPresentation) steps it, fits the arm and fires camera
    // kicks on the beats. All points are in camera-local metres. Field names are versioned (v2) so values
    // serialized by the first, faster version are not reused.
    //
    // Arms-fix (19/11/2026): exactly two blows read on screen, the wrench (YankTime) and the punch (StrikeTime).
    // - Reach / Strain / Yank: an open-to-grip grab. The hand comes up into view from below (it is hidden before the
    //   execution), arcs to the joint with an ease-in-out (no punch thrust) and never reaches past maxGripReachV3
    //   (the old grip clamp reached 0.78 m, a full punch extension, with a closed fist: the punch "into the air").
    // - Fist only from WindUp to the end of Impact (HandShape / FistClosed).
    // - Recover: the hand relaxes and drops out of view, so hiding it at Done never pops.
    // Every beat duration is unchanged: YankTime 0.34 and StrikeTime 0.732 are identical (ExtractionArcFx fires at
    // YankTime, the kill lands at StrikeTime).
    //
    // executions-v02 (22/11/2026): Recover 0.34 -> 0.24 (recoverTimeV4; approved, pays for the global hit-stops), the grip
    // follows the live joint every frame (still clamped to maxGripReachV3), 2-frame local hold at the wrench, a hand-off
    // window (0.44-0.52) where the left hand tosses the stolen weapon to the right arm, the fist depth follows the robot's
    // chest, an open hand while approaching, and a RipOnly profile (no punch) for the Big Bot.
    [Serializable]
    public sealed class DisarmMotion
    {
        public enum Beat { None, Reach, Strain, Yank, WindUp, Strike, Impact, Recover, Done }
        // Punch: T-07 / SawBot (wrench + punch). RipOnly: Big Bot (grab, wrench, hand-off, relax; the shot is the second blow).
        public enum Profile { Punch, RipOnly }
        [Min(.02f)] public float reachTimeV2=.18f, strainTimeV2=.16f, yankTimeV2=.18f, windUpTimeV2=.14f, strikeTimeV2=.09f, impactTimeV2=.08f, recoverTimeV2=.34f;
        [Tooltip("Hand shake amplitude while straining against the joint (metres).")]
        public float strainShakeV2=.012f;
        public Vector3 pullPointV2=new Vector3(-.19f,-.21f,.24f);
        public Vector3 windUpPointV2=new Vector3(-.26f,-.17f,.16f);
        public Vector3 strikePointV2=new Vector3(-.03f,-.08f,.80f);
        public Vector3 reachElbowV2=new Vector3(-.30f,-.40f,.10f);
        public Vector3 pullElbowV2=new Vector3(-.38f,-.42f,-.06f);
        public Vector3 windUpElbowV2=new Vector3(-.42f,-.30f,-.10f);
        public Vector3 strikeElbowV2=new Vector3(-.20f,-.34f,.26f);
        public Vector3 restElbowV2=new Vector3(-.24f,-.45f,.02f);
        [Header("v3 grab and exit (arms-fix)")]
        [Tooltip("Furthest camera-space depth of the grab (metres). Beyond ~0.6 m the reach reads as a punch thrust.")]
        public float maxGripReachV3=.55f;
        [Tooltip("Where a hidden left hand enters from (below the view) and where it leaves to on recover.")]
        public Vector3 entryPointV3=new Vector3(-.30f,-.52f,.16f), entryElbowV3=new Vector3(-.34f,-.64f,-.10f);
        public Vector3 exitPointV3=new Vector3(-.32f,-.58f,.10f), exitElbowV3=new Vector3(-.36f,-.68f,-.14f);
        [Header("executions-v02")]
        [Tooltip("Recover (hand relaxes and leaves the view). Was 0.34 (recoverTimeV2, no longer read).")]
        [Min(.02f)] public float recoverTimeV4=.24f;
        [Tooltip("RipOnly profile: recover after the hand-off (Big Bot).")]
        [Min(.02f)] public float ripRecoverTimeV4=.25f;
        [Tooltip("The hand-off window inside the Yank beat (seconds from the start): the left hand tosses the weapon to the right arm.")]
        public float handoffStartV4=.44f, handoffEndV4=.52f;
        [Tooltip("Camera-local point where the stolen weapon is handed to the right arm.")]
        public Vector3 handoffPointV4=new Vector3(.22f,-.20f,.42f);
        [Tooltip("Frames the hand pose is held still at the wrench (the clock keeps running).")]
        [Min(0)] public int yankHoldFramesV4=2;
        [Tooltip("The fist never goes deeper than this; it reaches min(this, chest depth + 0.05).")]
        public float maxStrikeDepthV4=.80f;
        [Tooltip("The hand stays open this long while the reach starts, then grips.")]
        public float openHandSecondsV4=.15f;
        [Tooltip("The right arm drops this much (metres) while the left hand grabs.")]
        public float rightArmDropV4=.04f;
        float elapsed=-1;
        Vector3 grip;
        Beat last;
        Profile profile;
        float strikeDepth=.80f;
        int holdLeft;bool holdArmed;
        public bool Active=>elapsed>=0;
        public Profile CurrentProfile=>profile;
        public float Elapsed=>elapsed;
        float StrainEnd=>reachTimeV2+strainTimeV2;
        float YankEnd=>StrainEnd+yankTimeV2;
        float WindUpEnd=>YankEnd+windUpTimeV2;
        float StrikeEnd=>WindUpEnd+strikeTimeV2;
        float ImpactEnd=>StrikeEnd+impactTimeV2;
        bool RipOnly=>profile==Profile.RipOnly;
        float RecoverStart=>RipOnly?YankEnd:ImpactEnd;
        float RecoverTime=>RipOnly?ripRecoverTimeV4:recoverTimeV4;
        public float Duration=>RecoverStart+RecoverTime;
        // Moment the fist lands; the robot knockback is scheduled for it.
        public float StrikeTime=>WindUpEnd+strikeTimeV2*.8f;
        public float YankTime=>StrainEnd;
        // First moment the hand is a fist (start of the wind-up for the punch).
        public float WindUpTime=>YankEnd;
        // executions-v02 beat timestamps for the director.
        public float ReachEndTime=>reachTimeV2;
        public float ImpactEndTime=>RecoverStart;
        public Vector3 Grip=>grip;
        public Beat Current=>Active?At(elapsed):Beat.None;
        // Fist only for the punch; grip while grabbing and wrenching; relaxed on the way out.
        public bool FistClosed{get{var b=Current;return !RipOnly&&b>=Beat.WindUp&&b<=Beat.Impact;}}
        public HandPoseLibrary.Pose HandShape
        {
            get
            {
                if(FistClosed)return HandPoseLibrary.Pose.Fist;
                var b=Current;
                if(b==Beat.Recover)return HandPoseLibrary.Pose.Relaxed;
                // executions-v02: open while approaching, grip on the joint, open again once the weapon is tossed away.
                if(b==Beat.Reach&&elapsed<openHandSecondsV4)return HandPoseLibrary.Pose.Open;
                if(b==Beat.Yank&&elapsed>=handoffStartV4+.02f)return HandPoseLibrary.Pose.Open;
                if(b==Beat.WindUp||b==Beat.Strike||b==Beat.Impact)return HandPoseLibrary.Pose.Relaxed;
                return HandPoseLibrary.Pose.Grip;
            }
        }
        // 0..1 weight of the right-arm drop (grab: down 4 cm, back up while the weapon is handed over).
        public float RightDrop
        {
            get
            {
                if(!Active)return 0;
                float t=elapsed;
                if(t<reachTimeV2)return EaseInOut(t/reachTimeV2);
                if(t<StrainEnd)return 1;
                if(t<YankEnd)return 1-EaseInOut((t-StrainEnd)/yankTimeV2);
                return 0;
            }
        }
        // 0..1 progress of the hand-off window (the left hand tosses the weapon), -1 outside it.
        public float HandoffProgress=>Active&&elapsed>=handoffStartV4&&elapsed<handoffEndV4?(elapsed-handoffStartV4)/Mathf.Max(.01f,handoffEndV4-handoffStartV4):-1;
        float PoseTime=>holdLeft>0?Mathf.Max(YankTime,0):Mathf.Max(0,elapsed);

        public void Begin(Vector3 gripLocal)=>Begin(gripLocal,Profile.Punch);
        public void Begin(Vector3 gripLocal,Profile style)
        {
            profile=style;strikeDepth=strikePointV2.z;holdLeft=0;holdArmed=false;
            SetGrip(gripLocal);
            elapsed=0;last=Beat.None;
        }
        // Keep the grip inside a believable arm envelope even if the joint is off-axis or far away; the depth stays
        // a grab (not a full punch extension) however far the joint is.
        // executions-v02 retune: x [-0.50, 0.20], y [-0.40, 0.18], z <= maxGripReachV3; also kept inside ~90 % of the frame (SetViewLimits) so the hand never leaves the picture.
        float viewTanH=100,viewTanV=100;
        public void SetViewLimits(float tanHalfHorizontal,float tanHalfVertical){viewTanH=tanHalfHorizontal;viewTanV=tanHalfVertical;}
        void SetGrip(Vector3 gripLocal)
        {
            float z=Mathf.Clamp(gripLocal.z,.42f,Mathf.Max(.42f,maxGripReachV3));
            float minX=Mathf.Max(-.50f,-.9f*viewTanH*z),maxX=Mathf.Min(.20f,.9f*viewTanH*z),minY=Mathf.Max(-.40f,-.9f*viewTanV*z);
            float maxY=Mathf.Min(.18f,.9f*viewTanV*z); // coordinator: tall robots (T-07 Mech) carry the weapon above the eye line
            grip=new Vector3(Mathf.Clamp(gripLocal.x,minX,maxX),Mathf.Clamp(gripLocal.y,minY,maxY),z);
        }
        // executions-v02: the hand follows the live joint until the wrench (afterwards the pull is authored).
        public void UpdateGrip(Vector3 gripLocal){if(Active&&elapsed<YankTime)SetGrip(gripLocal);}
        // executions-v02: the fist stops at min(maxStrikeDepthV4, chest depth + 0.05) so it touches the robot.
        public void SetChestDepth(float chestDepth){strikeDepth=Mathf.Clamp(Mathf.Min(maxStrikeDepthV4,chestDepth+.05f),.42f,Mathf.Max(.42f,maxStrikeDepthV4));}
        Vector3 StrikePoint=>new Vector3(strikePointV2.x,strikePointV2.y,strikeDepth);
        public void Cancel(){elapsed=-1;last=Beat.None;holdLeft=0;holdArmed=false;}

        Beat At(float t)
        {
            if(t<reachTimeV2)return Beat.Reach;if(t<StrainEnd)return Beat.Strain;if(t<YankEnd)return Beat.Yank;
            if(RipOnly)return t<Duration?Beat.Recover:Beat.Done;
            return t<WindUpEnd?Beat.WindUp:t<StrikeEnd?Beat.Strike:t<ImpactEnd?Beat.Impact:t<Duration?Beat.Recover:Beat.Done;
        }

        // Advances time and returns the furthest beat entered this step (None when unchanged).
        public Beat Step(float dt)
        {
            if(!Active)return Beat.None;
            float before=elapsed;
            if(holdLeft>0)holdLeft--;
            elapsed+=dt;
            // executions-v02: local hold of the pose for yankHoldFramesV4 frames from the wrench (clock unaffected).
            if(!holdArmed&&before<YankTime&&elapsed>=YankTime){holdArmed=true;holdLeft=yankHoldFramesV4;}
            var now=At(elapsed);
            if(now==Beat.Done){Cancel();return Beat.Done;}
            if(now==last)return Beat.None;
            last=now;return now;
        }

        // Legacy signature (closedHand = FistClosed). The owner's normal wrist/elbow is blended in by weight.
        public void Evaluate(out Vector3 wrist,out Vector3 elbow,out float weight,out bool closedHand)
        {Evaluate(false,out wrist,out elbow,out weight);closedHand=FistClosed;}

        // fromHidden: the left hand is hidden outside actions, so it enters from below the view and leaves the same way
        // with weight 1 throughout (no blend with a pose that follows the weapon socket). Otherwise (legacy rigs whose
        // left hand stays visible) it blends in from and back to the owner's pose with weight.
        public void Evaluate(bool fromHidden,out Vector3 wrist,out Vector3 elbow,out float weight)
        {
            float t=PoseTime;
            weight=1;
            var strikePoint=StrikePoint;
            switch(At(t))
            {
                case Beat.Reach:
                {
                    float u=t/reachTimeV2;
                    if(fromHidden)
                    {
                        // Comes up into view first, then forward onto the joint: an arc, decelerating into the grip.
                        float rise=EaseOut(u),advance=EaseInOut(u);
                        wrist=new Vector3(Mathf.Lerp(entryPointV3.x,grip.x,advance),Mathf.Lerp(entryPointV3.y,grip.y,rise),Mathf.Lerp(entryPointV3.z,grip.z,advance));
                        elbow=Vector3.Lerp(entryElbowV3,reachElbowV2,EaseInOut(u));return;
                    }
                    float k=EaseInOut(u);
                    wrist=grip;elbow=Vector3.Lerp(restElbowV2,reachElbowV2,k);weight=k;return;
                }
                case Beat.Strain:
                {
                    // Locked on the joint: small back-pull and a tremble as the arm fights the mount.
                    float k=(t-reachTimeV2)/strainTimeV2;
                    var shake=new Vector3(Mathf.Sin(t*71),Mathf.Sin(t*53+1.3f),0)*strainShakeV2*Mathf.Sin(k*Mathf.PI);
                    wrist=Vector3.Lerp(grip,pullPointV2,.12f*k)+shake;elbow=Vector3.Lerp(reachElbowV2,pullElbowV2,.25f*k);return;
                }
                case Beat.Yank:
                {
                    float k=EaseIn((t-StrainEnd)/yankTimeV2);
                    var from=Vector3.Lerp(grip,pullPointV2,.12f);
                    wrist=Vector3.Lerp(from,pullPointV2,k);elbow=Vector3.Lerp(Vector3.Lerp(reachElbowV2,pullElbowV2,.25f),pullElbowV2,k);
                    // executions-v02: toss towards the right arm inside the hand-off window and come back by its end (continuous
                    // with the wind-up, which starts from pullPoint).
                    if(t>=handoffStartV4&&t<handoffEndV4)
                    {
                        float u=(t-handoffStartV4)/Mathf.Max(.01f,handoffEndV4-handoffStartV4);
                        wrist+= (handoffPointV4-pullPointV2)*.3f*Mathf.Sin(u*Mathf.PI);
                    }
                    return;
                }
                case Beat.WindUp:
                {
                    float k=EaseInOut((t-YankEnd)/windUpTimeV2);
                    wrist=Vector3.Lerp(pullPointV2,windUpPointV2,k);elbow=Vector3.Lerp(pullElbowV2,windUpElbowV2,k);return;
                }
                case Beat.Strike:
                {
                    float k=EaseIn((t-WindUpEnd)/strikeTimeV2);
                    wrist=Vector3.Lerp(windUpPointV2,strikePoint,k);elbow=Vector3.Lerp(windUpElbowV2,strikeElbowV2,k);return;
                }
                case Beat.Impact:
                    wrist=strikePoint-Vector3.forward*.02f*Mathf.Sin((t-StrikeEnd)/impactTimeV2*Mathf.PI);elbow=strikeElbowV2;return;
                default:
                {
                    float r=Mathf.Clamp01((t-RecoverStart)/RecoverTime);
                    var fromPoint=RipOnly?pullPointV2:strikePoint;var fromElbow=RipOnly?pullElbowV2:strikeElbowV2;
                    if(fromHidden)
                    {
                        // Relaxes and drops out of view: down first, then back and aside. Hidden at Done while out of view.
                        float drop=EaseInOut(Mathf.Clamp01(r*1.4f)),back=EaseInOut(r);
                        wrist=new Vector3(Mathf.Lerp(fromPoint.x,exitPointV3.x,back),Mathf.Lerp(fromPoint.y,exitPointV3.y,drop),Mathf.Lerp(fromPoint.z,exitPointV3.z,back));
                        elbow=Vector3.Lerp(fromElbow,exitElbowV3,back);return;
                    }
                    wrist=fromPoint;elbow=fromElbow;weight=1-EaseInOut(r);return;
                }
            }
        }
        static float EaseIn(float t)=>t*t*t;
        static float EaseOut(float t)=>1-(1-t)*(1-t)*(1-t);
        static float EaseInOut(float t)=>t*t*(3-2*t);
    }
}
