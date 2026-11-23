using System;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    // executions-v02: ONE clock per execution. For T-07 / SawBot the clock is the left-hand disarm clock
    // (ProtagonistHandsPresentation.DisarmElapsed, so hands, kicks, hit-stops, blows and beats cannot drift apart); for the
    // Big Bot it is BigBotExecutionSequence's own clock, ticked through Tick(). Every beat fires exactly once, in order:
    //   kick + trauma + hit-stop  ->  kind-specific action (blow, proxy, arc fx)  ->  EnemyExecutionSource.RaiseBeat  ->  post action (kill).
    // The robot side only listens to RaiseBeat (it owns the robot's audio, bones, tint, pull-in and hiding its own weapon);
    // this class never plays a sound. Added at runtime by StolenWeaponCarrier.Awake (no player prefab edit).
    //
    // T-07 / SawBot table (clock s): Begin 0 | Grab 0 | Struggle .18 | Rip .34 (blow 1) | Handoff .44 | WindUp .52 |
    //   Strike .732 (blow 2 + death) | Recover .83 | End 1.07.   Big Bot: see BigBotExecutionSequence.
    // Reach bands: ExecutionReachResolver. Kicks are peak degrees as designed in F1; the spring peaks at ~0.78x what it is given,
    // so they are divided by 0.78 here.
    [DefaultExecutionOrder(115)]
    public sealed class ExecutionDirector : MonoBehaviour
    {
        struct Cue
        {
            public ExecutionBeat Beat;public float Time;
            public Vector3 Kick,Offset;
            public float Trauma,TraumaRadius,TraumaSeconds;
            public float StopSeconds,StopScale;
        }
        const float KickGain=1f/.78f;
        [SerializeField,Min(0)] float punchPushMetres=.9f;
        [SerializeField,Min(.05f)] float punchPushSeconds=.32f;
        [Tooltip("Camera depth beyond this at the Rip beat (maxGripReach + margin) sends the proxy flying to the hand (band C backup).")]
        [SerializeField,Min(0)] float flyMargin=.08f;
        readonly Cue[] cues=new Cue[10];
        int cueCount,nextCue;
        StolenWeaponCarrier carrier;ProtagonistHandsPresentation hands;PlayerCharacterController player;Health health;
        AdvancedPlayerMovement movement;PlayerWeaponsManager manager;ExtractionArcFx arcFx;ExecutionWeaponCarry carry;
        EnemyExecutionSource target;Transform joint;Collider[] robotColliders;Renderer[] weaponViews;ExecutionKind kind;
        Action<ExecutionBeat,bool> hook;
        bool active,budgetHeld,stepping,switched;
        float stepEnd,switchAt;
        ExecutionReachResolver.Result reach;
        public bool IsRunning=>active;
        public ExecutionKind Kind=>kind;
        public ExecutionReachResolver.Result Reach=>reach;

        void Awake()
        {
            carrier=GetComponent<StolenWeaponCarrier>();hands=GetComponent<ProtagonistHandsPresentation>();player=GetComponent<PlayerCharacterController>();
            health=GetComponent<Health>();movement=GetComponent<AdvancedPlayerMovement>();manager=GetComponent<PlayerWeaponsManager>();
            carry=GetComponent<ExecutionWeaponCarry>();if(!carry)carry=gameObject.AddComponent<ExecutionWeaponCarry>();
        }
        void OnDisable(){if(active)End(true);}

        // ---------------------------------------------------------------------------------------------- T-07 / SawBot
        // Called by StolenWeaponCarrier.TryExecute after inventory, Held and protection are done.
        public bool BeginPunch(EnemyExecutionSource source,Vector3 grip,StolenCannon module,Quaternion moduleRotation,WeaponController stolen,WeaponController outgoing)
        {
            if(active||!source||!hands||!player)return false;
            Setup(source,module,moduleRotation,stolen);
            hook=null;
            // reach: the grip is the real joint; pull and (band C) micro-step are decided now
            reach=ExecutionReachResolver.Resolve(player.PlayerCamera,GetComponent<CharacterController>(),joint,weaponViews,robotColliders,player.IsGrounded);
            stepping=false;
            if(reach.StepDistance>0&&movement&&movement.enabled)
            {
                movement.BeginScriptedMove(transform.position+reach.StepDirection*reach.StepDistance,reach.StepSeconds,0);
                stepping=true;stepEnd=reach.StepSeconds;
            }
            float strike=hands.DisarmStrikeTime,yank=hands.DisarmYankTime,windUp=hands.DisarmWindUpTime;
            BuildPunchCues(yank,windUp,strike);
            switchAt=Mathf.Clamp(windUp-OutgoingPhaseSeconds(outgoing),yank,windUp);switched=false;
            ExecutionShove.Begin(source.gameObject,source.transform.position-transform.position,strike,transform,punchPushMetres,punchPushSeconds);
            carrier.ExecutionPrepare(source);
            carrier.NotifyRip(source,grip); // hands begin (grip follows the live joint from here)
            nextCue=0;active=true;
            Fire(nextCue++); // Begin (carries PullDistance / PullSeconds), same frame as E
            return true;
        }

        void BuildPunchCues(float yank,float windUp,float strike)
        {
            cueCount=0;
            Add(ExecutionBeat.Begin,0,new Vector3(.4f,0,.3f));
            Add(ExecutionBeat.Grab,0);
            Add(ExecutionBeat.Struggle,hands.DisarmReachEndTime,new Vector3(.5f,0,.6f),0,new Vector3(.10f,4,.16f));
            Add(ExecutionBeat.Rip,yank,new Vector3(-1.8f,-.5f,1.2f),-.024f,default,.05f,.10f);
            Add(ExecutionBeat.Handoff,hands.DisarmHandoffStartTime);
            Add(ExecutionBeat.WindUp,windUp,new Vector3(-.5f,0,-.8f));
            Add(ExecutionBeat.Strike,strike,new Vector3(2.8f,.4f,-1.4f),.032f,default,.06f,.08f);
            Add(ExecutionBeat.Recover,hands.DisarmImpactEndTime);
            Add(ExecutionBeat.End,hands.DisarmDuration);
        }

        // ------------------------------------------------------------------------------------------------- Big Bot
        // BigBotExecutionSequence owns the clock and the staging; it asks the director for the common per-beat work.
        public void BeginBig(EnemyExecutionSource source,StolenCannon module,Quaternion moduleRotation,WeaponController stolen,Action<ExecutionBeat,bool> callback)
        {
            if(active)End(true);
            Setup(source,module,moduleRotation,stolen);
            hook=callback;reach=default;stepping=false;
            cueCount=0;
            Add(ExecutionBeat.Begin,0,new Vector3(.6f,0,0));
            Add(ExecutionBeat.Grab,.40f,new Vector3(1.5f,0,0));
            Add(ExecutionBeat.Struggle,.46f);
            Add(ExecutionBeat.Rip,.62f,new Vector3(-2.0f,0,1.5f),0,default,.05f,.10f);
            Add(ExecutionBeat.Handoff,.80f,new Vector3(0,0,-.6f));
            Add(ExecutionBeat.Strike,.95f,new Vector3(-3.0f,0,1.0f),-.04f,new Vector3(.4f,8,.3f),.06f,.08f);
            Add(ExecutionBeat.Recover,.95f); // jump back starts on the shot frame (retune after review)
            Add(ExecutionBeat.End,1.40f,new Vector3(1.2f,0,0));
            nextCue=0;active=true;
            Fire(nextCue++);
        }
        // Fires every cue due at clock t (Big Bot). Returns false once the sequence is over.
        float tickClock;
        public void Tick(float clock){tickClock=clock;while(active&&nextCue<cueCount&&cues[nextCue].Time<=clock)Fire(nextCue++);}
        public ExecutionBeat LastBeat=>target?target.LastBeat:ExecutionBeat.None;
        public void Finish(){if(active)End(false);}
        public void Abort(){if(active)End(true);}

        // ------------------------------------------------------------------------------------------------- shared
        void Setup(EnemyExecutionSource source,StolenCannon module,Quaternion moduleRotation,WeaponController stolen)
        {
            target=source;kind=source.Kind;joint=source.WeaponJoint;
            robotColliders=source.GetComponentsInChildren<Collider>();
            weaponViews=ExecutionReachResolver.WeaponViews(source);if(hands)hands.SetGripViews(weaponViews); // the hand closes on the weapon surface
            GameFeel.BeginExecutionBudget();budgetHeld=true;
            if(carry)carry.Prepare(module,stolen,joint,moduleRotation);
        }

        static float OutgoingPhaseSeconds(WeaponController outgoing,PlayerWeaponsManager manager)
        {
            if(outgoing&&outgoing.TryGetComponent(out IWeaponSwitchMotion motion)&&motion.OverridesSwitchMotion)return motion.SwitchPhaseSeconds;
            return manager?manager.WeaponSwitchDelay:.3f;
        }
        float OutgoingPhaseSeconds(WeaponController outgoing)=>OutgoingPhaseSeconds(outgoing,manager);
        // Big Bot: the earliest moment the stolen weapon switch can start so it is up at the hand-off beat.
        public float SwitchStartFor(float handoff,float earliest,WeaponController outgoing)=>Mathf.Clamp(handoff-OutgoingPhaseSeconds(outgoing),earliest,handoff);


        void Add(ExecutionBeat beat,float time,Vector3 kick=default,float offsetZ=0,Vector3 trauma=default,float stopSeconds=0,float stopScale=1)
        {
            var c=new Cue{Beat=beat,Time=time,Kick=kick,Offset=new Vector3(0,0,offsetZ),Trauma=trauma.x,TraumaRadius=trauma.y,TraumaSeconds=trauma.z,StopSeconds=stopSeconds,StopScale=stopScale};
            cues[cueCount++]=c;
        }

        void Update()
        {
            if(!active||hook!=null)return; // Big Bot is ticked by its sequence
            if(!target||player.IsDead||health.CurrentHealth<=0){End(true);return;}
            float t=hands.DisarmElapsed;
            bool handsDone=t<0;
            if(stepping&&(handsDone||t>=stepEnd)){movement.EndScriptedMove();stepping=false;}
            if(!switched&&(handsDone||t>=switchAt)){switched=true;carrier.ExecutionShowHeld();}
            if(handsDone)t=float.MaxValue;
            while(nextCue<cueCount&&cues[nextCue].Time<=t)Fire(nextCue++);
            if(nextCue>=cueCount)End(false);
        }

        void Fire(int index)
        {
            var c=cues[index];var cam=player.PlayerCamera;
            if(c.Kick!=Vector3.zero||c.Offset!=Vector3.zero)DistanceCameraImpulse.Kick(cam,c.Kick*KickGain,c.Offset*KickGain);
            if(c.Trauma>0)DistanceCameraImpulse.Emit(cam.transform.position,c.TraumaRadius,c.Trauma,c.TraumaSeconds);
            if(c.StopSeconds>0)GameFeel.ExecutionHitStop(c.StopSeconds,c.StopScale);
            if(hook!=null)hook(c.Beat,false);else PunchAction(c.Beat);
            Raise(c.Beat);
            if(hook!=null)hook(c.Beat,true);else PunchPost(c.Beat);
        }

        void PunchAction(ExecutionBeat beat)
        {
            switch(beat)
            {
                case ExecutionBeat.Rip:
                {
                    var at=ExecutionReachResolver.ClosestWeaponPoint(player.PlayerCamera.transform.position,weaponViews,joint);
                    carrier.ExecutionBlow(target,at);
                    // band C backup: if the joint is still out of the hand's reach, the weapon flies to the hand instead
                    float depth=player.PlayerCamera.transform.InverseTransformPoint(at).z;
                    bool fly=joint&&depth>hands.DisarmMaxGripReach+flyMargin;
                    carry.Show(fly);
                    RipEffects(at);
                    break;
                }
                case ExecutionBeat.Handoff:carry.BeginThrow(hands.DisarmWindUpTime-hands.DisarmHandoffStartTime);break;
                case ExecutionBeat.WindUp:carry.AllowSwap();break;
                case ExecutionBeat.Strike:carrier.ExecutionBlow(target,target.transform.position+Vector3.up*1.2f);break;
            }
        }
        void PunchPost(ExecutionBeat beat){if(beat==ExecutionBeat.Strike)carrier.ExecutionKill(target);}

        // Rip effect shared with the Big Bot: electric arc from the real joint to the robot's body.
        public void RipEffects(Vector3 at)
        {
            if(!arcFx)arcFx=GetComponent<ExtractionArcFx>();
            if(arcFx&&target)arcFx.Trigger(at,target.transform.position+Vector3.up*1.1f);
        }
        public ExecutionWeaponCarry Carry=>carry;

        void Raise(ExecutionBeat beat)
        {
            if(!target)return;
            var cam=player.PlayerCamera.transform;
            var toRobot=Vector3.ProjectOnPlane(target.transform.position-cam.position,Vector3.up);
            var left=hands?hands.LeftHandBone:null;
            var info=new ExecutionBeatInfo
            {
                Beat=beat,Kind=kind,PlayerPosition=cam.position,ToRobot=toRobot.sqrMagnitude>1e-6f?toRobot.normalized:target.transform.forward,
                HandPosition=left?left.position:cam.position+cam.forward*.5f,
                Time=hook!=null?tickClock:Mathf.Max(0,hands?hands.DisarmElapsed:0)
            };
            if(beat==ExecutionBeat.Begin){info.PullDistance=reach.PullDistance;info.PullSeconds=reach.PullSeconds;}
            target.RaiseBeat(info);
        }

        void End(bool cancelled)
        {
            if(!active)return;active=false;
            if(stepping&&movement){movement.EndScriptedMove();}
            stepping=false;if(hands)hands.SetGripViews(null);
            if(cancelled)
            {
                if(target&&target.InExecution)Raise(ExecutionBeat.Cancel);
                if(carry)carry.Cancel();
                GameFeel.ReleaseHitStop();
                if(hook==null&&carrier)carrier.ExecutionAbort();
            }
            else
            {
                if(carry)carry.Finish();
                if(hook==null&&carrier)carrier.ExecutionFinished();
            }
            if(budgetHeld){budgetHeld=false;GameFeel.EndExecutionBudget();}
            hook=null;
            target=null;
        }
    }
}
