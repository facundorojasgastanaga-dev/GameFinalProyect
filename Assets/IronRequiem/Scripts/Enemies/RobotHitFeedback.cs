using Unity.FPS.Game;
using UnityEngine;
namespace IronRequiem
{
    [DefaultExecutionOrder(-10),RequireComponent(typeof(Health))]
    public sealed class RobotHitFeedback:MonoBehaviour
    {
        [SerializeField] Renderer[] body;
        [SerializeField] Transform torso;
        [SerializeField] ParticleSystem sparks;
        [SerializeField] Gradient flash;
        [SerializeField,Range(.04f,.2f)] float flashDuration=.09f;
        [SerializeField,Min(.1f)] float flashCooldown=.14f;
        [SerializeField,Range(.1f,.4f)] float reactionDuration=.18f;
        [SerializeField,Min(.2f)] float reactionCooldown=.3f;
        [SerializeField,Range(.5f,12)] float reactionDegrees=2.5f;
        [SerializeField] bool reactToEachImpact;
        [SerializeField] AnimationCurve impactMotion;
        readonly float[] impactStarts=new float[8];int nextImpact,lastImpulseFrame=-1;
        [SerializeField,Range(2,12)] int sparkCount=6;
        sealed class Slot{public Renderer View;public int Index;public MaterialPropertyBlock Original,Work;public bool Empty;}
        Slot[] slots;Damageable[] targets;Health health;Quaternion pose;bool applied,flashing;float flashAt,nextFlash,reactionAt=-10,nextReaction;bool subscribed;
        public int ImpactCount {get;private set;}public int FlashCount {get;private set;}public int ReactionCount {get;private set;}
        public bool IsFlashing=>flashing;public Transform Torso=>torso;public ParticleSystem Sparks=>sparks;public float CurrentReactionAngle{get;private set;}
        public void Configure(Renderer[] views,Transform joint,ParticleSystem particles,Gradient pulse){body=views;torso=joint;sparks=particles;flash=pulse;}
        public void ConfigurePerImpact(AnimationCurve motion,float duration,float degrees){reactToEachImpact=true;impactMotion=motion;reactionDuration=duration;reactionDegrees=degrees;}
        void AddReaction(){if(reactToEachImpact){impactStarts[nextImpact]=Time.time;nextImpact=(nextImpact+1)%impactStarts.Length;}else{reactionAt=Time.time;nextReaction=Time.time+reactionCooldown;}lastImpulseFrame=Time.frameCount;ReactionCount++;}
        void Awake(){health=GetComponent<Health>();targets=GetComponentsInChildren<Damageable>(true);var list=new System.Collections.Generic.List<Slot>();foreach(var r in body)for(int i=0;i<r.sharedMaterials.Length;i++)list.Add(new Slot{View=r,Index=i,Original=new MaterialPropertyBlock(),Work=new MaterialPropertyBlock()});slots=list.ToArray();}
        void OnEnable(){if(!health)health=GetComponent<Health>();health.OnDamaged+=OnDamage;health.OnDie+=Cancel;foreach(var target in targets)if(target)target.Impact+=OnImpact;subscribed=true;flashAt=nextFlash=nextReaction=0;reactionAt=-10;for(int i=0;i<impactStarts.Length;i++)impactStarts[i]=-100;nextImpact=0;lastImpulseFrame=-1;ImpactCount=FlashCount=ReactionCount=0;if(sparks)sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        void OnDisable(){if(subscribed){health.OnDamaged-=OnDamage;health.OnDie-=Cancel;foreach(var target in targets)if(target)target.Impact-=OnImpact;subscribed=false;}Cancel();}
        void OnDamage(float amount,GameObject source){if(health.CurrentHealth<=0)return;if(Time.time>=nextFlash){if(flashing)RestoreFlash();foreach(var s in slots)if(s.View){s.View.GetPropertyBlock(s.Original,s.Index);s.Empty=s.Original.isEmpty;s.View.GetPropertyBlock(s.Work,s.Index);}flashAt=Time.time;nextFlash=Time.time+flashCooldown;flashing=true;FlashCount++;}if(reactToEachImpact?amount>0:Time.time>=nextReaction)AddReaction();}
        void OnImpact(Vector3 point,Vector3 normal,GameObject source){if(!isActiveAndEnabled||health.CurrentHealth<=0)return;ImpactCount++;OnDamage(0,source);if(reactToEachImpact&&lastImpulseFrame!=Time.frameCount)AddReaction();sparks.transform.SetPositionAndRotation(point+normal*.012f,Quaternion.LookRotation(normal.sqrMagnitude>.001f?normal:Vector3.up));if(!sparks.isPlaying)sparks.Play(false);sparks.Emit(sparkCount);}
        void Update(){if(applied&&torso){torso.localRotation=pose;applied=false;}}
        void LateUpdate(){if(health.CurrentHealth<=0)return;if(flashing){float t=(Time.time-flashAt)/flashDuration;if(t>=1)RestoreFlash();else{var color=flash.Evaluate(t);float brightness=Mathf.Max(color.r,Mathf.Max(color.g,color.b));foreach(var s in slots)if(s.View){s.Work.SetColor("_EmissionColor",Color.white*brightness);s.View.SetPropertyBlock(s.Work,s.Index);}}}CurrentReactionAngle=0;float kick=0;if(reactToEachImpact){foreach(float at in impactStarts){float t=(Time.time-at)/reactionDuration;if(t>=0&&t<1)kick+=impactMotion.Evaluate(t);}kick=Mathf.Clamp(kick,-1.25f,1.25f);}else{float phase=(Time.time-reactionAt)/reactionDuration;if(phase>=0&&phase<1)kick=Mathf.Sin(phase*Mathf.PI)*Mathf.Exp(-phase*2);}if(torso&&Mathf.Abs(kick)>.0001f){pose=torso.localRotation;torso.localRotation=pose*Quaternion.Euler(-reactionDegrees*kick,0,reactionDegrees*.25f*kick);CurrentReactionAngle=Quaternion.Angle(pose,torso.localRotation);applied=true;}}
        void RestoreFlash(){if(slots!=null)foreach(var s in slots)if(s.View)s.View.SetPropertyBlock(s.Empty?null:s.Original,s.Index);flashing=false;}
        void Cancel(){RestoreFlash();if(applied&&torso)torso.localRotation=pose;applied=false;reactionAt=-10;for(int i=0;i<impactStarts.Length;i++)impactStarts[i]=-100;if(sparks)sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
    }
}