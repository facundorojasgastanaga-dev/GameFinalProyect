using System;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
namespace IronRequiem
{
    [DefaultExecutionOrder(80)]
    [RequireComponent(typeof(EnemyController),typeof(EnemyMobile))]
    public sealed class BigBotCombat : MonoBehaviour,IEnemyMobilePolicy,IEnemyDeathPresentation
    {
        [Header("Projectile combat")]
        [SerializeField,Min(0)] float damage=12;
        [SerializeField,Range(5,60)] float projectileSpeed=18;
        [SerializeField,Range(5,80)] float projectileRange=35;
        [SerializeField,Min(.2f)] float shotInterval=2.2f;
        [SerializeField,Range(.15f,3)] float preparationTime=.7f;
        [SerializeField,Range(3,30)] float attackDistance=12;
        [SerializeField,Range(5,60)] float detectionDistance=26;
        [Header("Explosive variant")]
        [SerializeField] bool explosiveShots;
        [SerializeField,Min(.1f)] float explosionRadius=1.8f;
        [Header("Single-cannon bursts (opt in per variant)")]
        [SerializeField] bool emergencyBursts;
        [SerializeField,Range(2,12)] int emergencyBurstCount=3;
        [SerializeField,Min(.05f)] float emergencyBurstInterval=.26f;
        [SerializeField,Min(.7f)] float emergencyBurstPause=2.1f;
        [Header("Alternating bursts (opt in per variant)")]
        [SerializeField] bool alternatingBursts;
        [SerializeField,Range(1,6)] int normalBurstCount=3;
        [SerializeField,Min(.05f)] float normalBurstInterval=.18f;
        [SerializeField,Min(.1f)] float normalBurstPause=1.5f;
        [SerializeField, Tooltip("Include the existing warning in the last-shot-to-next-burst pause.")] bool pauseIncludesPreparation;
        float burstReadyAt;
        int nextCannon;
        public bool AlternatingBursts=>alternatingBursts;
        public float NormalBurstInterval=>normalBurstInterval;
        public float NormalBurstPause=>normalBurstPause;
        float SelectedBurstInterval=>IsEmergency?emergencyBurstInterval:normalBurstInterval;
        float SelectedBurstPause=>IsEmergency?emergencyBurstPause:normalBurstPause;
        int burstRemaining,burstCannon=-1;float nextBurstShot;
        public bool ExplosiveShots=>explosiveShots;
        public float ExplosionRadius=>explosionRadius;
        public int EmergencyBurstCount=>emergencyBurstCount;
        public float EmergencyBurstInterval=>emergencyBurstInterval;
        public float EmergencyBurstPause=>emergencyBurstPause;
        public bool IsBursting=>burstRemaining>0;
        [Header("Body tracking")]
        [SerializeField,Range(20,120)] float turnSpeed=60;
        [SerializeField] Transform torso;
        [SerializeField] Transform healthBarPivot;
        [SerializeField] NewRobotAnimationPresentation presentation;
        [SerializeField] WeaponController[] cannons;
        [SerializeField] Transform[] rearPivots;
        [SerializeField] Transform[] aimingBones;
        [SerializeField] ParticleSystem[] preparationEffects;
        EnemyController controller;EnemyMobile mobile;NavMeshAgent agent;Health health;BigBotDetectionModule detection;
        readonly RaycastHit[] hits=new RaycastHit[64];readonly Collider[] overlaps=new Collider[32];NavMeshPath path;
        Vector3 lockedAim,lastSeen,cachedDestination,baseVisualPosition;float prepareStarted,nextShot,nextPathRefresh,previousYaw,turnVelocity,torsoYaw;bool pending,releaseRequested,dead,aimApplied;
        Quaternion torsoAnimation;Quaternion[] armAnimations;float[] armYaw,armPitch;
        [SerializeField,Range(1,3)] float emergencyCadenceMultiplier=1.5f;
        readonly bool[] lostCannons=new bool[2];
        BigBotDestruction destruction;float measuredTurn,turnPhase;bool steppingTurn;
        public float TurnPhase=>turnPhase;
        public event Action<int> CannonLost;
        public int ActiveCannonCount {get{int count=0;for(int i=0;i<cannons.Length;i++)if(IsCannonActive(i))count++;return count;}}
        public bool IsEmergency=>ActiveCannonCount==1;
        public float EmergencyCadenceMultiplier=>emergencyCadenceMultiplier;
        public float CurrentShotInterval=>alternatingBursts?SelectedBurstPause:IsEmergency&&emergencyBursts?emergencyBurstPause:IsEmergency?Mathf.Max(.5f,(shotInterval+preparationTime)/emergencyCadenceMultiplier-preparationTime):shotInterval;
        public bool IsCannonActive(int index)=>index>=0&&index<cannons.Length&&!lostCannons[index]&&cannons[index]&&cannons[index].isActiveAndEnabled;
        public bool LoseCannon(int index)
        {
            if(index<0||index>=cannons.Length||lostCannons[index])return false;
            int retained=alternatingBursts?burstRemaining:0;var retainedAim=lockedAim;
            lostCannons[index]=true;CancelPreparation();
            if(preparationEffects[index])preparationEffects[index].Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            cannons[index].AutomaticReload=false;cannons[index].SetAmmo(0);cannons[index].enabled=false;
            nextShot=Mathf.Max(nextShot,Time.time+.35f);CannonLost?.Invoke(index);
            if(retained>0&&ActiveCannonCount>0){burstRemaining=retained;lockedAim=retainedAim;nextBurstShot=Time.time+SelectedBurstInterval;}return true;
        }
        public float Damage=>Mathf.Max(0,damage);
        public float ProjectileSpeed=>Mathf.Clamp(projectileSpeed,5,60);
        public float ProjectileRange=>Mathf.Clamp(projectileRange,5,80);
        public float PreparationTime=>preparationTime;
        public float AttackDistance=>attackDistance;
        public bool IsDead=>dead;
        public bool IsPreparing=>pending;
        public int ShotsFired {get;private set;}
        public Vector3 LastSeenPosition=>lastSeen;
        public event Action<ProjectileBase> ProjectileSpawned;
        public NewRobotAnimationPresentation Presentation=>presentation;
        public WeaponController[] Cannons=>cannons;
        GameObject Target=>controller?controller.KnownDetectedTarget:null;
        Actor TargetActor=>Target?Target.GetComponentInParent<Actor>():null;
        bool Visible=>!dead&&detection&&detection.IsSeeingTarget&&TargetActor&&CanSeeSource(TargetActor.gameObject);
        public Vector3 AimPosition=>aimHeld?heldAim:pending||(alternatingBursts&&burstRemaining>0)?lockedAim:Visible?Target.transform.position:lastSeen;
        // Executions v02 (Big Bot round 2): while the player leaps to its cannon, the arms and torso keep the aim they had at E instead
        // of tracking the leaping player (that swung the cannon 0.6 m away from the grabbing hand). Presentation only.
        bool aimHeld,heldTrack;Vector3 heldAim;
        public void HoldAim(bool hold){if(hold&&!aimHeld){heldAim=AimPosition;heldTrack=Visible||pending;}aimHeld=hold;}
        bool Tracking=>aimHeld?heldTrack:Visible||pending;
        public bool CanAttack=>Visible&&Vector3.Distance(transform.position,Target.transform.position)<=attackDistance&&Vector3.Angle(transform.forward,Vector3.ProjectOnPlane(Target.transform.position-transform.position,Vector3.up))<30&&BothGunsClear(Target.transform.position);
        public Vector3 PursuitDestination
        {
            get
            {
                if(Time.time<nextPathRefresh)return cachedDestination;
                nextPathRefresh=Time.time+.25f;
                if(Visible)lastSeen=Target.transform.position;
                var wanted=lastSeen;
                if(Visible&&Vector3.Distance(transform.position,wanted)<attackDistance&&!BothGunsClear(wanted))
                {
                    foreach(int sign in new[]{-1,1})
                    {
                        var candidate=transform.position+transform.right*(sign*3);
                        if(NavMesh.SamplePosition(candidate,out var sample,1.5f,agent.areaMask)&&NavMesh.CalculatePath(transform.position,sample.position,agent.areaMask,path)&&path.status==NavMeshPathStatus.PathComplete&&ClearSegment(sample.position+(detection.DetectionSourcePoint.position-transform.position),wanted,.04f,TargetActor))
                        {wanted=sample.position;break;}
                    }
                }
                if(NavMesh.SamplePosition(wanted,out var goal,3,agent.areaMask))cachedDestination=goal.position;else cachedDestination=transform.position;
                return cachedDestination;
            }
        }
        public void Configure(NewRobotAnimationPresentation visual,WeaponController[] weapons,Transform[] rear,Transform[] bones,ParticleSystem[] warnings)
        {presentation=visual;cannons=weapons;rearPivots=rear;aimingBones=bones;preparationEffects=warnings;}
        public void ConfigureTracking(Transform chest,Transform bar){torso=chest;healthBarPivot=bar;}
        void Awake()
        {
            destruction=GetComponent<BigBotDestruction>();path=new NavMeshPath();armAnimations=new Quaternion[aimingBones.Length];armYaw=new float[aimingBones.Length];armPitch=new float[aimingBones.Length];
            controller=GetComponent<EnemyController>();mobile=GetComponent<EnemyMobile>();agent=GetComponent<NavMeshAgent>();health=GetComponent<Health>();detection=GetComponentInChildren<BigBotDetectionModule>();
            lastSeen=cachedDestination=transform.position;baseVisualPosition=presentation.transform.localPosition;previousYaw=transform.eulerAngles.y;health.OnDie+=Die;presentation.ShotMoment+=OnShotMoment;
            if(!GetComponent<RobotMotionAccents>())gameObject.AddComponent<RobotMotionAccents>();
        }
        void Update()
        {
            RestoreAnimationPose();
            if(dead)return;
            if(health.CurrentHealth<=0){Die();return;}
            for(int i=0;i<cannons.Length;i++)if(!lostCannons[i]&&(!cannons[i]||!cannons[i].isActiveAndEnabled))LoseCannon(i);
            detection.AttackRange=attackDistance;detection.DetectionRange=Mathf.Max(detectionDistance,attackDistance);
            if(Visible)lastSeen=Target.transform.position;
            else if(pending||burstRemaining>0||presentation.IsShooting)CancelPreparation();
            if(pending&&Vector3.Angle(transform.forward,Vector3.ProjectOnPlane(Target.transform.position-transform.position,Vector3.up))>50)CancelPreparation();
            if(pending&&Time.time>prepareStarted+preparationTime+1.5f)CancelPreparation();
        }
        public void UpdateMotion(float speed)
        {
            if(dead)return;
            if(mobile.AiState==EnemyMobile.AIState.Patrol&&agent.velocity.sqrMagnitude>.01f)OrientTowards(transform.position+agent.velocity);
            float turn=Mathf.DeltaAngle(previousYaw,transform.eulerAngles.y)/Mathf.Max(Time.deltaTime,.001f);previousYaw=transform.eulerAngles.y;
            if(destruction)
            {
                turnPhase=Mathf.Repeat(turnPhase+Mathf.Abs(turn)*Time.deltaTime/45,1);
                measuredTurn=Mathf.Lerp(measuredTurn,turn,1-Mathf.Exp(-12*Time.deltaTime));
                if(Mathf.Abs(measuredTurn)>10)steppingTurn=true;else if(Mathf.Abs(measuredTurn)<3)steppingTurn=false;
                presentation.Animator.SetFloat("TurnPhase",turnPhase);
                presentation.SetMotion(speed,speed<.05f&&steppingTurn?Mathf.Sign(measuredTurn):0);
            }
            else presentation.SetMotion(speed,speed<.05f&&Mathf.Abs(turn)>5?Mathf.Sign(turn):0);
            presentation.Animator.SetFloat("TurnSpeed",Mathf.Clamp(Mathf.Abs(turn)/45,.45f,1.7f));
        }
        public void OrientTowards(Vector3 target)
        {
            Vector3 direction=Vector3.ProjectOnPlane(target-transform.position,Vector3.up);
            if(direction.sqrMagnitude<.001f)return;
            float current=transform.eulerAngles.y;
            float wanted=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            if(Mathf.Abs(Mathf.DeltaAngle(current,wanted))<1.5f){turnVelocity=0;return;}
            float yaw=Mathf.SmoothDampAngle(current,wanted,ref turnVelocity,.16f,turnSpeed,Time.deltaTime);
            transform.rotation=Quaternion.Euler(0,yaw,0);
        }
        void RestoreAnimationPose()
        {
            if(!aimApplied)return;
            if(torso)torso.localRotation=torsoAnimation;
            for(int i=0;i<aimingBones.Length;i++)aimingBones[i].localRotation=armAnimations[i];
            aimApplied=false;
        }
        void AimPresentation(Vector3 aim,bool track)
        {
            // Save the Animator result, then apply a bounded offset once. Remove it before the next evaluation.
            float desiredYaw=0;
            if(track){var local=transform.InverseTransformDirection(aim-transform.position);float angle=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;desiredYaw=Mathf.Abs(angle)<90?Mathf.Clamp(angle,-20,20):0;}
            torsoYaw=Mathf.MoveTowards(torsoYaw,desiredYaw,50*Time.deltaTime);
            if(torso){torsoAnimation=torso.localRotation;torso.rotation=Quaternion.AngleAxis(torsoYaw,transform.up)*torso.rotation;}
            for(int i=0;i<aimingBones.Length;i++)
            {
                armAnimations[i]=aimingBones[i].localRotation;
                if(!IsCannonActive(i))continue;

                var direction=transform.InverseTransformDirection(aim-cannons[i].WeaponMuzzle.position).normalized;
                float angle=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
                bool inFront=track&&Mathf.Abs(angle)<90;
                float wantedYaw=inFront?Mathf.Clamp(angle-torsoYaw,-20,20):0;
                float wantedPitch=inFront?Mathf.Clamp(-Mathf.Asin(direction.y)*Mathf.Rad2Deg,-12,28):0;
                armYaw[i]=Mathf.MoveTowards(armYaw[i],wantedYaw,60*Time.deltaTime);armPitch[i]=Mathf.MoveTowards(armPitch[i],wantedPitch,60*Time.deltaTime);
                float yaw=armYaw[i],pitch=armPitch[i];
                aimingBones[i].rotation=Quaternion.AngleAxis(yaw,transform.up)*Quaternion.AngleAxis(pitch,transform.right)*aimingBones[i].rotation;
            }
            aimApplied=true;
        }
        bool BothGunsClear(Vector3 aim)
        {
            bool any=false;for(int i=0;i<cannons.Length;i++)if(IsCannonActive(i)){any=true;if(!GunClear(cannons[i],aim))return false;}
            return any;
        }
        public void RequestAttack()
        {
            if(dead||pending||burstRemaining>0||Time.time<nextShot||!CanAttack)return;
            if(presentation.IsShooting){if(!alternatingBursts)return;presentation.CancelShot();}
            lockedAim=Target.transform.position;prepareStarted=Time.time;releaseRequested=false;
            presentation.Animator.SetFloat("PreparationSpeed",.6f/Mathf.Max(.15f,preparationTime));
            if(!presentation.RequestShot())return;
            if(alternatingBursts){var a=presentation.Animator;int l=a.GetLayerIndex("UpperBody");if(l>=0&&a.HasState(l,Animator.StringToHash("UpperBody.PrepareFire"))){a.ResetTrigger("Attack");a.ResetTrigger("CancelAttack");a.Play("UpperBody.PrepareFire",l,0);}}
            pending=true;
            for(int i=0;i<preparationEffects.Length;i++)if(IsCannonActive(i)&&preparationEffects[i])preparationEffects[i].Play(true);
        }
        void OnShotMoment(Transform unused){if(pending&&!dead)releaseRequested=true;}
        void LateUpdate()
        {
            if(dead)return;
            AlignToFloor();
            AimPresentation(AimPosition,Tracking);
            if(Tracking)controller.OrientWeaponsTowards(AimPosition);
            if(burstRemaining>0){UpdateBurst();return;}
            if(!pending)return;
            if(!Visible){CancelPreparation();return;}
            if(!releaseRequested||Time.time<prepareStarted+preparationTime||(pauseIncludesPreparation&&Time.time<burstReadyAt))return;
            if(!CanAttack||!BothGunsClear(lockedAim)){CancelPreparation();return;}
            pending=false;releaseRequested=false;StopWarnings();
            if(alternatingBursts){burstRemaining=IsEmergency?emergencyBurstCount:normalBurstCount;nextBurstShot=Time.time;UpdateBurst();return;}
            bool fired=false;
            if(!destruction)
            {
                var first=controller.GetCurrentWeapon();fired=controller.TryAtack(lockedAim);
                var second=controller.GetCurrentWeapon();if(fired&&second!=first)second.HandleShootInputs(false,true,false);
            }
            else for(int i=0;i<cannons.Length;i++)if(IsCannonActive(i))fired|=controller.TryAtack(lockedAim,cannons[i]);
            if(fired){ShotsFired++;nextShot=Time.time+CurrentShotInterval;
                if(IsEmergency&&emergencyBursts){burstCannon=Array.FindIndex(cannons,w=>w&&IsCannonActive(Array.IndexOf(cannons,w)));burstRemaining=Mathf.Max(0,emergencyBurstCount-1);nextBurstShot=Time.time+emergencyBurstInterval;}
            }else nextShot=Time.time+.25f;
        }
        void UpdateBurst()
        {
            if(alternatingBursts)
            {
                if(dead||!Visible||!CanAttack){CancelPreparation();return;}
                if(Time.time<nextBurstShot)return;
                int selected=-1;for(int offset=0;offset<cannons.Length;offset++){int i=(nextCannon+offset)%cannons.Length;if(IsCannonActive(i)&&GunClear(cannons[i],lockedAim)){selected=i;break;}}
                if(selected<0||!controller.TryAtack(lockedAim,cannons[selected])){CancelPreparation();return;}
                nextCannon=(selected+1)%cannons.Length;ShotsFired++;burstRemaining--;nextBurstShot=Time.time+SelectedBurstInterval;
                var a=presentation.Animator;a.ResetTrigger("Attack");a.ResetTrigger("CancelAttack");int l=a.GetLayerIndex("UpperBody");if(l>=0&&a.HasState(l,Animator.StringToHash("UpperBody.Fire")))a.Play("UpperBody.Fire",l,0);
                if(burstRemaining==0){burstReadyAt=Time.time+SelectedBurstPause;nextShot=pauseIncludesPreparation?Mathf.Max(Time.time,burstReadyAt-preparationTime):burstReadyAt;}
                return;
            }
            if(dead||!Visible||!CanAttack||!IsCannonActive(burstCannon)){CancelPreparation();return;}
            if(Time.time<nextBurstShot)return;
            var aim=Target.transform.position;
            if(!GunClear(cannons[burstCannon],aim)){CancelPreparation();return;}
            if(!controller.TryAtack(aim,cannons[burstCannon])){CancelPreparation();return;}
            ShotsFired++;burstRemaining--;nextBurstShot=Time.time+emergencyBurstInterval;
            var animator=presentation.Animator;int layer=animator.GetLayerIndex("UpperBody");
            if(layer>=0&&animator.HasState(layer,Animator.StringToHash("UpperBody.Fire")))animator.Play("UpperBody.Fire",layer,0);
            if(burstRemaining==0){burstCannon=-1;nextShot=Time.time+emergencyBurstPause;}
        }
        void AlignToFloor()
        {
            int n=Physics.RaycastNonAlloc(transform.position+Vector3.up*.5f,Vector3.down,hits,1.2f,-1,QueryTriggerInteraction.Ignore);float closest=float.PositiveInfinity;bool found=false;float floor=0;
            for(int i=0;i<n;i++)if(!hits[i].collider.transform.IsChildOf(transform)&&hits[i].normal.y>.6f&&hits[i].distance<closest){closest=hits[i].distance;floor=hits[i].point.y;found=true;}
            if(found&&Mathf.Abs(floor-transform.position.y)<.4f)presentation.transform.localPosition=baseVisualPosition+Vector3.up*((floor-transform.position.y)/transform.lossyScale.y);
        }
        public bool CanSeeSource(GameObject source)
        {
            if(!source||!detection)return false;
            var actor=source.GetComponentInParent<Actor>();if(!actor||actor.Affiliation==GetComponent<Actor>().Affiliation||!actor.AimPoint)return false;
            var otherHealth=actor.GetComponent<Health>();if(otherHealth&&otherHealth.CurrentHealth<=0)return false;
            return Vector3.Distance(detection.DetectionSourcePoint.position,actor.AimPoint.position)<=Mathf.Max(detectionDistance,attackDistance)&&ClearSegment(detection.DetectionSourcePoint.position,actor.AimPoint.position,.025f,actor);
        }
        bool GunClear(WeaponController weapon,Vector3 aim)
        {
            int i=Array.IndexOf(cannons,weapon);if(i<0)return false;
            var actor=TargetActor;
            return ClearSegment(rearPivots[i].position,weapon.WeaponMuzzle.position,.10f,actor)&&ClearSegment(weapon.WeaponMuzzle.position,aim,.10f,actor);
        }
        bool ClearSegment(Vector3 from,Vector3 to,float radius,Actor accepted=null)
        {
            int count=Physics.OverlapSphereNonAlloc(from,radius,overlaps,-1,QueryTriggerInteraction.Ignore);if(count==overlaps.Length)return false;
            for(int i=0;i<count;i++)if(!Ignored(overlaps[i],accepted))return false;
            var delta=to-from;if(delta.sqrMagnitude<.0001f)return true;
            count=Physics.SphereCastNonAlloc(from,radius,delta.normalized,hits,delta.magnitude,-1,QueryTriggerInteraction.Ignore);if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(!Ignored(hits[i].collider,accepted))return false;
            return true;
        }
        bool Ignored(Collider c,Actor accepted)=>c.transform.IsChildOf(transform)||(accepted&&c.GetComponentInParent<Actor>()==accepted);
        void StopWarnings(){foreach(var ps in preparationEffects)if(ps)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        void CancelPreparation(){burstRemaining=0;burstCannon=-1;pending=false;releaseRequested=false;StopWarnings();presentation.CancelShot();nextShot=Mathf.Max(nextShot,Time.time+.25f);}
        public void NotifyProjectileSpawned(ProjectileBase projectile)=>ProjectileSpawned?.Invoke(projectile);
        public bool TryPlayDeathPresentation(){Die();return true;}
        void Die()
        {
            if(dead)return;RestoreAnimationPose();dead=true;CancelPreparation();if(healthBarPivot)healthBarPivot.gameObject.SetActive(false);mobile.enabled=false;controller.enabled=false;
            if(agent.enabled&&agent.isOnNavMesh){agent.isStopped=true;agent.ResetPath();agent.velocity=Vector3.zero;}agent.enabled=false;
            presentation.SetMotion(0);presentation.Animator.enabled=false;
            foreach(var ps in GetComponentsInChildren<ParticleSystem>(true))ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach(var source in GetComponentsInChildren<AudioSource>(true))source.Stop();
            foreach(var c in GetComponentsInChildren<Collider>())c.enabled=false;
            foreach(var weapon in cannons)if(weapon&&weapon.transform.IsChildOf(transform))weapon.ShowWeapon(false);
            if(destruction)destruction.BeginDeath();
        }
        void OnDestroy(){if(health)health.OnDie-=Die;if(presentation)presentation.ShotMoment-=OnShotMoment;}
    }
}
