using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    [RequireComponent(typeof(PlayerCharacterController),typeof(MechanicInput))]
    public sealed class AdvancedPlayerMovement : MonoBehaviour, IPlayerMotionExtension
    {
        [Header("Air dash")]
        [SerializeField,Min(1)] float dashSpeed=24;
        [SerializeField,Min(.02f)] float dashDuration=.18f;
        [Header("Ground slide")]
        [SerializeField,Min(.05f)] float slideDuration=.7f;
        [SerializeField,Min(0)] float slideMinimumSpeed=11;
        [SerializeField,Min(0)] float slideImpulse=4;
        [SerializeField,Min(0)] float slideDeceleration=18;
        [SerializeField,Min(1)] float slideMaximumSpeed=28;
        [Header("Slide cooldown")]
        [Tooltip("Seconds, counted from the end of a slide, before another slide can start. 0 keeps the previous behaviour.")]
        [SerializeField,Min(0)] float slideCooldown=0;
        [Tooltip("After a slide starts, C must be released and pressed again before the next slide.")]
        [SerializeField] bool slideRequiresRelease;
        [Header("Slide invulnerability (v4)")]
        [Tooltip("Seconds from the start of a slide during which incoming damage is ignored (Health temporary invulnerability). 0 disables it.")]
        [SerializeField,Range(0,.6f)] float slideInvulnerability=.42f;
        [Header("Slide look")]
        [SerializeField,Range(30,89)] float slideLookAngle=80;
        [Tooltip("Maximum downward camera pitch while sliding (degrees). The template allows 89.")]
        [SerializeField,Range(0,89)] float slideMaxPitch=38;
        [Tooltip("Seconds to tighten the downward limit when a slide starts (pushes the view up smoothly).")]
        [SerializeField,Min(.01f)] float slidePitchEnterTime=.12f;
        [Tooltip("Time constant used to release the limit back to 89 degrees after the slide.")]
        [SerializeField,Min(.01f)] float slidePitchReleaseTime=.18f;
        const float FreePitch=89;
        float pitchLimit=FreePitch;
        PlayerCharacterController player;
        PlayerInputHandler original;
        MechanicInput input;
        Vector3 dashDirection,slideDirection;
        float dashRemaining,slideRemaining,slideSpeed;
        bool wasGrounded;
        float slideReadyTime;
        bool crouchReleasedSinceSlide=true;
        public float SlideCooldown => slideCooldown;
        public bool SlideRequiresRelease => slideRequiresRelease;
        public float SlideCooldownRemaining => Mathf.Max(0,slideReadyTime-Time.time);
        public bool SlideReady => SlideCooldownRemaining<=0 && (!slideRequiresRelease || crouchReleasedSinceSlide);
        public void ConfigureSlideCooldown(float seconds,bool requireRelease){slideCooldown=Mathf.Max(0,seconds);slideRequiresRelease=requireRelease;}
        public bool DashAvailable { get; private set; }=true;
        public float SlideInvulnerability => slideInvulnerability;
        public void ConfigureSlideInvulnerability(float seconds)=>slideInvulnerability=Mathf.Clamp(seconds,0,.6f);
        // Time.time at which the current slide's invulnerability window ends (presentation reads it).
        public float SlideGuardUntil {get;private set;}
        public bool SlideGuardActive => Time.time<SlideGuardUntil;
        public event System.Action SlideStarted;
        Unity.FPS.Game.Health health;
        // v4 heavy-punch lunge: a short forward push applied through the same velocity path as dash and slide.
        Vector3 lungeDirection;float lungeRemaining,lungeSpeed;
        public bool IsLunging => lungeRemaining>0;
        public void BeginLunge(Vector3 direction,float distance,float duration)
        {
            if(!enabled||scripted||distance<=0)return;
            var flat=Vector3.ProjectOnPlane(direction,Vector3.up);if(flat.sqrMagnitude<1e-4f)return;
            EndDash();EndSlide(false);
            lungeDirection=flat.normalized;lungeRemaining=Mathf.Max(.02f,duration);lungeSpeed=distance/lungeRemaining;
        }
        void EndLunge(){if(lungeRemaining<=0)return;lungeRemaining=0;player.CharacterVelocity=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(player.CharacterVelocity,Vector3.up),player.MaxSpeedOnGround)+Vector3.up*player.CharacterVelocity.y;}
        public bool IsDashing => dashRemaining>0;
        public bool IsSliding => slideRemaining>0;
        public Vector3 SlideDirection => slideDirection;
        public float SlideLookAngle => slideLookAngle;
        public bool KeepCrouched => IsSliding;
        public bool SuppressWeaponBob => enabled && IsSliding;
        // Ground snapping is suspended while airborne in a scripted arc, but kept while holding a perch.
        public bool SuppressGrounding => enabled && scripted && scriptedTime<scriptedDuration;
        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;

        // Scripted motion (executions). Still applied through PlayerCharacterController.Move: this only
        // supplies the velocity, so collisions, grounding and the single movement authority are preserved.
        Vector3 scriptedFrom,scriptedTo;float scriptedTime,scriptedDuration,scriptedArc;bool scripted;
        bool aimOverride;Vector3 aimPoint;float aimSharpness;
        public bool IsScripted => scripted;
        public bool ScriptedArrived => scripted && scriptedTime>=scriptedDuration;
        public void BeginScriptedMove(Vector3 target,float duration,float arcHeight)
        {
            EndDash();EndSlide(false);EndLunge();
            scriptedFrom=transform.position;scriptedTo=target;scriptedDuration=Mathf.Max(.01f,duration);scriptedArc=arcHeight;scriptedTime=0;scripted=true;
        }
        public void EndScriptedMove()
        {
            if(!scripted)return;scripted=false;player.CharacterVelocity=Vector3.zero;
        }
        public void ResetForCheckpoint()
        {
            EndDash(); EndSlide(false); EndLunge(); EndScriptedMove(); ClearAimOverride(); SlideGuardUntil=0;
            player.CharacterVelocity=Vector3.zero;
        }
        // Smoothly turns the view towards a world point (yaw on the body, pitch on the camera) until cleared.
        public void SetAimOverride(Vector3 point,float sharpness=10){aimOverride=true;aimPoint=point;aimSharpness=sharpness;}
        public void ClearAimOverride()=>aimOverride=false;
        Vector3 AimDirection=>(aimPoint-player.PlayerCamera.transform.position).normalized;

        void Awake()
        {
            player=GetComponent<PlayerCharacterController>();original=GetComponent<PlayerInputHandler>();input=GetComponent<MechanicInput>();
            health=GetComponent<Unity.FPS.Game.Health>();
        }

        // The controller applies this delta once; no second camera/transform rotation authority.
        public float ConstrainYawDelta(float requestedDelta)
        {
            if(enabled && aimOverride)
            {
                var flat=Vector3.ProjectOnPlane(AimDirection,Vector3.up);
                if(flat.sqrMagnitude<1e-4f)return 0;
                float wanted=Mathf.Atan2(flat.x,flat.z)*Mathf.Rad2Deg;
                return Mathf.DeltaAngle(transform.eulerAngles.y,wanted)*(1-Mathf.Exp(-aimSharpness*Time.deltaTime));
            }
            if(!enabled || !IsSliding)return requestedDelta;
            float heading=Mathf.Atan2(slideDirection.x,slideDirection.z)*Mathf.Rad2Deg;
            float relative=Mathf.DeltaAngle(heading,transform.eulerAngles.y);
            return Mathf.Clamp(relative+requestedDelta,-slideLookAngle,slideLookAngle)-relative;
        }

        public float SlideMaxPitch => slideMaxPitch;
        public float CurrentPitchLimit => pitchLimit;

        // Called once per look update by the controller after its own ±89 clamp. Only the downward
        // limit moves: it tightens quickly on slide entry and eases back out afterwards, so the view
        // never snaps when the slide ends.
        public float ConstrainPitch(float pitch)
        {
            float dt=Time.deltaTime;
            if(enabled && aimOverride)
            {
                float wanted=Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(AimDirection.y,-1,1))*Mathf.Rad2Deg,-89,89);
                pitch=Mathf.Lerp(pitch,wanted,1-Mathf.Exp(-aimSharpness*dt));
            }
            if(enabled && IsSliding)
                pitchLimit=Mathf.MoveTowards(pitchLimit,slideMaxPitch,(FreePitch-slideMaxPitch)/slidePitchEnterTime*dt);
            else if(pitchLimit<FreePitch)
            {
                pitchLimit=Mathf.Lerp(pitchLimit,FreePitch,1-Mathf.Exp(-dt/slidePitchReleaseTime));
                if(FreePitch-pitchLimit<.25f)pitchLimit=FreePitch;
            }
            return Mathf.Min(pitch,pitchLimit);
        }

        public void BeforeMovement()
        {
            if(!enabled)return;
            if(!input.CrouchHeld||input.CrouchReleasedThisFrame)crouchReleasedSinceSlide=true;
            if(scripted){scriptedTime+=Time.deltaTime;wasGrounded=player.IsGrounded;return;}
            if(!input.CanAct)
            { EndDash();EndSlide(false);EndLunge();wasGrounded=player.IsGrounded;return; }
            if(IsLunging){lungeRemaining-=Time.deltaTime;if(lungeRemaining<=0){lungeRemaining=.0001f;EndLunge();}}
            if(player.IsGrounded && !wasGrounded && player.CharacterVelocity.y<=.1f)DashAvailable=true;
            wasGrounded=player.IsGrounded;
            if(IsDashing)
            {
                dashRemaining-=Time.deltaTime;
                if(dashRemaining<=0 || player.IsGrounded)EndDash();
            }
            if(IsSliding)
            {
                slideRemaining-=Time.deltaTime;slideSpeed=Mathf.MoveTowards(slideSpeed,0,slideDeceleration*Time.deltaTime);
                if(slideRemaining<=0 || !player.IsGrounded || slideSpeed<player.MaxSpeedOnGround*player.MaxSpeedCrouchedRatio)EndSlide(true);
            }
            if(input.DashPressed && !player.IsGrounded && DashAvailable)
            {
                EndSlide(false);
                var move=original.GetMoveInput();
                dashDirection=move.sqrMagnitude>.001f ? transform.TransformDirection(move) : Vector3.ProjectOnPlane(player.PlayerCamera.transform.forward,Vector3.up);
                dashDirection=Vector3.ProjectOnPlane(dashDirection,Vector3.up).normalized;
                if(dashDirection.sqrMagnitude<.1f)dashDirection=transform.forward;
                DashAvailable=false;dashRemaining=dashDuration;
                // Cancel ascent, retain descent. The original air branch applies gravity every frame.
                player.CharacterVelocity=Vector3.ProjectOnPlane(player.CharacterVelocity,Vector3.up)+Vector3.up*Mathf.Min(0,player.CharacterVelocity.y);
            }
        }

        public bool HandleCrouchInput()
        {
            if(enabled && scripted)return true; // Crouch is ignored during scripted motion.
            if(!enabled || !input.CanAct)return false;
            if(IsSliding){EndSlide(false);return true;}
            float speed=Vector3.ProjectOnPlane(player.CharacterVelocity,Vector3.up).magnitude;
            if(!player.IsGrounded || player.IsCrouching || !original.GetSprintInputHeld() || original.GetMoveInput().sqrMagnitude<.01f || speed<slideMinimumSpeed || IsDashing)return false;
            // Every other requirement is met: a press during the cooldown (or without releasing C) is consumed,
            // so repeated taps never chain a slide nor toggle crouch in the middle of a sprint.
            if(!SlideReady)return true;
            if(!player.TrySetCrouching(true))return false;
            crouchReleasedSinceSlide=false;
            slideDirection=Vector3.ProjectOnPlane(player.CharacterVelocity,Vector3.up).normalized;
            slideSpeed=Mathf.Min(slideMaximumSpeed,speed+slideImpulse);slideRemaining=slideDuration;
            // v4: short invulnerability window from the start of the slide (shared Health source, never shortens another one).
            if(slideInvulnerability>0){SlideGuardUntil=Time.time+slideInvulnerability;if(health)health.GrantInvulnerability(slideInvulnerability);}
            SlideStarted?.Invoke();return true;
        }

        public bool TryGetVelocity(Vector3 standardVelocity,out Vector3 velocity)
        {
            velocity=standardVelocity;
            if(!enabled)return false;
            if(scripted)
            {
                // Horizontal: smooth in/out. Vertical: ease-out plus an arc, so the first frame already leaves the
                // ground-snap distance (like a real jump) and the landing arrives slowly (no fall-damage speed).
                float s=Mathf.Clamp01(scriptedTime/scriptedDuration);float e=s*s*(3-2*s);float v=1-(1-s)*(1-s)*(1-s);
                var flat=Vector3.Lerp(new Vector3(scriptedFrom.x,0,scriptedFrom.z),new Vector3(scriptedTo.x,0,scriptedTo.z),e);
                var wanted=flat+Vector3.up*(Mathf.Lerp(scriptedFrom.y,scriptedTo.y,v)+scriptedArc*Mathf.Sin(Mathf.PI*s));
                velocity=(wanted-transform.position)/Mathf.Max(Time.deltaTime,1e-4f);
                velocity=Vector3.ClampMagnitude(velocity,40);return true;
            }
            if(player.HasJumpedThisFrame && IsSliding)EndSlide(false);
            if(IsDashing)
            { velocity=dashDirection*dashSpeed+Vector3.up*standardVelocity.y;return true; }
            if(IsLunging)
            { velocity=lungeDirection*lungeSpeed+Vector3.up*standardVelocity.y;return true; }
            if(IsSliding)
            { velocity=player.GetDirectionReorientedOnSlope(slideDirection,player.GroundNormal)*slideSpeed;return true; }
            return false;
        }

        public void AfterMovement(CollisionFlags collisions)
        {
            if((collisions&CollisionFlags.Sides)!=0){EndDash();EndSlide(true);EndLunge();}
        }

        void EndDash()
        {
            if(!IsDashing && dashRemaining==0)return;
            dashRemaining=0;
            player.CharacterVelocity=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(player.CharacterVelocity,Vector3.up),player.MaxSpeedInAir)+Vector3.up*Mathf.Min(player.CharacterVelocity.y,-.1f);
        }
        void EndSlide(bool stand)
        {
            if(slideRemaining<=0 && slideSpeed<=0)return;
            slideRemaining=0;slideSpeed=0;
            slideReadyTime=Time.time+slideCooldown; // The cooldown is counted from the end of the slide.
            if(stand)player.TrySetCrouching(false); // Existing obstruction query keeps crouch under a ceiling.
            float cap=player.MaxSpeedOnGround*(player.IsCrouching?player.MaxSpeedCrouchedRatio:1);
            player.CharacterVelocity=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(player.CharacterVelocity,Vector3.up),cap)+Vector3.up*player.CharacterVelocity.y;
        }
    }
}
