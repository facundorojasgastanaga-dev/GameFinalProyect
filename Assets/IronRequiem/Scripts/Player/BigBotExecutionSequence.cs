using System.Collections.Generic;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    // Big Bot execution: leap onto the robot, wrench its cannon off, shoot it with that cannon, jump back.
    // Inventory is already updated by StolenWeaponCarrier before Begin; this component only stages the
    // presentation and delivers the kill (EnemyExecutionSource.Complete) exactly once, at the shot or on cancel.
    // Movement goes through AdvancedPlayerMovement's scripted velocity, so the controller stays the only mover.
    [DefaultExecutionOrder(90)]
    [RequireComponent(typeof(AdvancedPlayerMovement),typeof(StolenWeaponCarrier))]
    public sealed class BigBotExecutionSequence : MonoBehaviour
    {
        enum Phase { Idle, Leap, Rip, Aim, Recoil, Dismount }
        [SerializeField,Min(.1f)] float leapTime=.45f;
        [SerializeField,Min(0)] float leapArc=1.1f;
        [SerializeField,Min(.1f)] float ripTime=.7f;
        [SerializeField,Min(.05f)] float aimTime=.35f;
        [SerializeField,Min(.05f)] float recoilTime=.25f;
        [SerializeField,Min(.1f)] float dismountTime=.55f;
        [SerializeField,Min(0)] float dismountArc=1.2f;
        [SerializeField] Vector3 shotKick=new Vector3(-4.5f,1.2f,-2.5f);
        [SerializeField] Vector3 landKick=new Vector3(3f,0,1.5f);
        AdvancedPlayerMovement movement;StolenWeaponCarrier carrier;PlayerCharacterController player;Health health;
        CharacterController body;
        EnemyExecutionSource target;BigBotCombat bot;BigBotDestruction destruction;
        readonly List<Collider> ignored=new List<Collider>();
        Vector3 start,perch,grip;Phase phase;float phaseTime;bool delivered,cannonHidden;
        public bool IsRunning=>phase!=Phase.Idle;
        public int ShotCount {get;private set;}
        public float TotalTime=>leapTime+ripTime+aimTime+recoilTime+dismountTime;
        public Vector3 Perch=>perch;
        // Diagnostics: last collider the CharacterController touched while the sequence ran.
        public string LastContact {get;private set;}="none";
        void OnControllerColliderHit(ControllerColliderHit hit){if(IsRunning&&hit.collider&&hit.normal.y<.5f)LastContact=hit.collider.name+" ("+(hit.collider.transform.root?hit.collider.transform.root.name:"-")+") n="+hit.normal.ToString("F2")+" ignoredCount="+ignored.Count;}

        void Awake()
        {
            movement=GetComponent<AdvancedPlayerMovement>();carrier=GetComponent<StolenWeaponCarrier>();player=GetComponent<PlayerCharacterController>();
            health=GetComponent<Health>();body=GetComponent<CharacterController>();
        }
        void OnEnable(){if(health)health.OnDie+=Cancel;}
        void OnDisable()
        {
            if(health)health.OnDie-=Cancel;
            if(!IsRunning)return;
            // Teardown/disable: release the player but do not trigger a kill while objects are being destroyed.
            if(movement){movement.EndScriptedMove();movement.ClearAimOverride();}
            RestoreCollisions();phase=Phase.Idle;target=null;bot=null;destruction=null;
        }

        public bool Begin(EnemyExecutionSource source,Vector3 gripPoint)
        {
            if(IsRunning||!source||!movement||!movement.enabled||!body)return false;
            bot=source.GetComponent<BigBotCombat>();if(!bot)return false;
            target=source;destruction=source.GetComponent<BigBotDestruction>();grip=gripPoint;delivered=false;cannonHidden=false;
            // Grab the actual cannon when its views are known (the interaction ray may have hit the torso).
            if(destruction&&destruction.CannonViews!=null)
                foreach(var view in destruction.CannonViews)if(view&&view.enabled){grip=view.bounds.center;break;}
            // Freeze the robot: no new attacks, no path following. Its health and death flow stay untouched.
            var mobile=source.GetComponent<EnemyMobile>();if(mobile)mobile.enabled=false;
            var enemy=source.GetComponent<EnemyController>();
            if(enemy&&enemy.NavMeshAgent&&enemy.NavMeshAgent.enabled&&enemy.NavMeshAgent.isOnNavMesh){enemy.NavMeshAgent.isStopped=true;enemy.NavMeshAgent.velocity=Vector3.zero;}
            // Perch on top of the robot, slightly on the player's side so the head/core stays in view.
            Bounds bounds=new Bounds(source.transform.position+Vector3.up,Vector3.one);bool first=true;
            ignored.Clear();
            foreach(var c in source.GetComponentsInChildren<Collider>())
            {
                if(!c.enabled||c.isTrigger)continue;
                if(first){bounds=c.bounds;first=false;}else bounds.Encapsulate(c.bounds);
                Physics.IgnoreCollision(body,c,true);ignored.Add(c);
            }
            var toPlayer=Vector3.ProjectOnPlane(transform.position-bounds.center,Vector3.up);
            toPlayer=toPlayer.sqrMagnitude>1e-4f?toPlayer.normalized:-source.transform.forward;
            start=transform.position;
            perch=new Vector3(bounds.center.x,bounds.max.y+.05f,bounds.center.z)+toPlayer*Mathf.Min(.35f,bounds.extents.x*.4f);
            movement.BeginScriptedMove(perch,leapTime,leapArc);
            movement.SetAimOverride(grip,8);
            Enter(Phase.Leap);
            return true;
        }

        void Enter(Phase next){phase=next;phaseTime=0;}

        void Update()
        {
            if(!IsRunning)return;
            if(!target||player.IsDead){Cancel();return;}
            phaseTime+=Time.deltaTime;
            switch(phase)
            {
                case Phase.Leap:
                    if(phaseTime>=leapTime)
                    {
                        DistanceCameraImpulse.Kick(player.PlayerCamera,landKick,new Vector3(0,-.04f,0));
                        // Same hand animation as the T07: grab, strain, wrench off, then a punch into the robot.
                        carrier.NotifyRip(target,grip);
                        movement.BeginScriptedMove(perch,1000,0); // Hold on the perch.
                        Enter(Phase.Rip);
                    }
                    break;
                case Phase.Rip:
                    if(!cannonHidden&&phaseTime>=RipYankTime()){cannonHidden=true;HideCannon();}
                    if(phaseTime>=ripTime){movement.SetAimOverride(Core(),12);Enter(Phase.Aim);}
                    break;
                case Phase.Aim:
                    movement.SetAimOverride(Core(),12);
                    if(phaseTime>=aimTime){Shoot();Enter(Phase.Recoil);}
                    break;
                case Phase.Recoil:
                    if(phaseTime>=recoilTime)
                    {
                        movement.ClearAimOverride();
                        movement.BeginScriptedMove(start,dismountTime,dismountArc);
                        Enter(Phase.Dismount);
                    }
                    break;
                case Phase.Dismount:
                    // Time fallback: if the controller is not stepping (disabled), the sequence still ends.
                    if(movement.ScriptedArrived||phaseTime>=dismountTime+.3f)Finish();
                    break;
            }
        }

        float RipYankTime()
        {
            var hands=GetComponent<ProtagonistHandsPresentation>();
            return hands?hands.DisarmYankTime+.06f:.35f;
        }
        Vector3 Core()
        {
            if(!target)return perch;
            var aim=target.GetComponentInChildren<BigBotDetectionModule>();
            Vector3 core=aim&&aim.DetectionSourcePoint?aim.DetectionSourcePoint.position:target.transform.position+Vector3.up*1.6f;
            return core+Vector3.down*.35f;
        }
        void HideCannon()
        {
            if(!destruction||destruction.CannonViews==null)return;
            foreach(var view in destruction.CannonViews)if(view)view.enabled=false;
        }
        void Shoot()
        {
            if(delivered||!target)return;
            ShotCount++;
            // Presentation shot with the stolen cannon: muzzle flash, sound and kick. Energy is not consumed;
            // the execution kill below is the damage.
            var weapon=carrier.Held?carrier.Held.EquippedWeapon:null;
            if(weapon)
            {
                var muzzle=weapon.WeaponMuzzle;
                if(weapon.MuzzleFlashPrefab){var flash=Instantiate(weapon.MuzzleFlashPrefab,muzzle.position,muzzle.rotation,muzzle);Destroy(flash,2);}
                if(weapon.ShootSfx)AudioUtility.CreateSFX(weapon.ShootSfx,muzzle.position,AudioUtility.AudioGroups.WeaponShoot,0);
            }
            DistanceCameraImpulse.Kick(player.PlayerCamera,shotKick,new Vector3(0,.01f,-.05f));
            var accents=target.GetComponent<RobotMotionAccents>();if(accents)accents.Shove(target.transform.position-transform.position);
            Deliver();
        }
        void Deliver(){if(delivered)return;delivered=true;if(target)target.Complete();}
        void RestoreCollisions()
        {
            foreach(var c in ignored)if(c&&body)Physics.IgnoreCollision(body,c,false);
            ignored.Clear();
        }
        void Finish()
        {
            movement.EndScriptedMove();movement.ClearAimOverride();RestoreCollisions();
            Deliver();phase=Phase.Idle;target=null;bot=null;destruction=null;
        }
        // Death, disable or a vanished target: never leave the player scripted or the robot reserved.
        public void Cancel()
        {
            if(!IsRunning)return;
            if(movement){movement.EndScriptedMove();movement.ClearAimOverride();}
            RestoreCollisions();Deliver();phase=Phase.Idle;target=null;bot=null;destruction=null;
        }
    }
}
