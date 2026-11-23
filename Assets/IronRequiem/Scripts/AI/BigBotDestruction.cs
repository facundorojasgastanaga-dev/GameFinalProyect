using System;
using Unity.FPS.Game;
using UnityEngine;
namespace IronRequiem
{
    [DefaultExecutionOrder(110),RequireComponent(typeof(Health),typeof(BigBotCombat))]
    public sealed class BigBotDestruction : MonoBehaviour
    {
        [Serializable] public sealed class ArmorPart
        {
            public string Name;public int Stage=2;public Transform Pivot;public Renderer[] Views;public Mesh Debris;
            [NonSerialized] public bool Detached;
        }
        [SerializeField,Range(.03f,.99f)] float lightThreshold=.75f;
        [SerializeField,Range(.02f,.98f)] float armorThreshold=.5f;
        [SerializeField,Range(.01f,.97f)] float criticalThreshold=.25f;
        [SerializeField,Min(.5f)] float armorLifetime=3;
        [SerializeField,Min(1)] float cannonLifetime=8;
        [SerializeField] ArmorPart[] armor;
        [SerializeField] Renderer[] body;
        [SerializeField] Material[] stages;
        [SerializeField] Material interior;
        [SerializeField] GameObject sharedPoolTemplate;
        [SerializeField] Transform cannonPivot,cannonFrontPivot;
        [SerializeField] Renderer[] cannonViews;
        [SerializeField] BigBotCannonDebris cannonPrefab;
        [SerializeField] StolenCannon collectibleCannonPrefab;
        [SerializeField] bool executionOnly;
        [SerializeField] ParticleSystem sparks,smoke,breakSparks;
        [SerializeField,Range(.3f,2)] float criticalSparkInterval=.75f;
        [SerializeField,Range(3,16)] int criticalSparkBurst=10;float nextCriticalSpark;
        [SerializeField] AudioClip collapseSound;
        [SerializeField] AnimationCurve deathFloorOffset=AnimationCurve.Constant(0,1.8f,0);
        [Serializable]public sealed class SoleSupport {public Transform Bone;public Vector3[] Points;}
        [SerializeField] SoleSupport[] soles;
        Vector3 deathVisualBase;float deathGround;
        [Header("Death presentation variant")]
        [SerializeField] bool deathBurstEnabled;
        [SerializeField,Min(0)] float deathBurstPreparationAt=.97f;
        [SerializeField,Range(.25f,3)] float deathBurstScale=1.55f;
        [SerializeField] Transform deathBurstAnchor;
        bool deathBurstRequested;
        public int DeathBurstCount {get;private set;}
        public void ConfigureDeathBurst(Transform anchor,GameObject template,float scale,float at)
        {deathBurstEnabled=true;deathBurstAnchor=anchor;sharedPoolTemplate=template;deathBurstScale=scale;deathBurstPreparationAt=at;}
        Health health;BigBotCombat combat;Animator animator;AudioSource audioSource;
        MaterialPropertyBlock power;
        int mask;float deathAge;bool impactPlayed;
        public int Stage {get;private set;}
        public int TransitionMask=>mask;
        public int TransitionCount {get;private set;}
        public int CannonDropCount {get;private set;}
        public bool CannonDetached {get;private set;}
        public bool DeathStarted {get;private set;}
        public int DeathCount {get;private set;}
        public int DetachedArmorCount {get;private set;}
        public BigBotCannonDebris FallenCannon {get;private set;}
        public ArmorPart[] Armor=>armor;
        public Renderer[] CannonViews=>cannonViews;
        public Transform CannonPivot=>cannonPivot;
        public Transform CannonFrontPivot=>cannonFrontPivot;
        public void Configure(ArmorPart[] parts,Renderer[] skins,Material[] materials,Material inside,GameObject pool,
            Transform joint,Renderer[] gun,BigBotCannonDebris remnant,ParticleSystem electric,ParticleSystem vapor,ParticleSystem fracture,AudioClip impact)
        {armor=parts;body=skins;stages=materials;interior=inside;sharedPoolTemplate=pool;cannonPivot=joint;cannonFrontPivot=joint.Find("Pincer_R");cannonViews=gun;cannonPrefab=remnant;sparks=electric;smoke=vapor;breakSparks=fracture;collapseSound=impact;}
        public void ConfigureSoles(SoleSupport[] supports)=>soles=supports;
        public void ConfigureCollectibleCannon(StolenCannon prefab)=>collectibleCannonPrefab=prefab;
        public void ConfigureDeath(AnimationCurve floorOffset)=>deathFloorOffset=floorOffset;
        void Awake()
        {
            power=new MaterialPropertyBlock();health=GetComponent<Health>();combat=GetComponent<BigBotCombat>();animator=combat.Presentation.Animator;audioSource=GetComponent<AudioSource>();
            RobotDestructionPool.Prepare(sharedPoolTemplate,gameObject.scene);
            if(executionOnly||!collectibleCannonPrefab)BigBotCannonDebris.Prepare(cannonPrefab,gameObject.scene);
        }
        void OnEnable(){if(!health)health=GetComponent<Health>();if(!combat)combat=GetComponent<BigBotCombat>();health.OnDamaged+=OnDamage;combat.CannonLost+=OnCannonLost;}
        void OnDisable(){health.OnDamaged-=OnDamage;combat.CannonLost-=OnCannonLost;StopEffects();}
        void OnValidate(){lightThreshold=Mathf.Clamp(lightThreshold,.03f,.99f);armorThreshold=Mathf.Clamp(armorThreshold,.02f,lightThreshold-.01f);criticalThreshold=Mathf.Clamp(criticalThreshold,.01f,armorThreshold-.01f);}
        void OnDamage(float amount,GameObject source){if(!DeathStarted)Evaluate();}
        void Evaluate()
        {
            float ratio=health.MaxHealth>0?health.CurrentHealth/health.MaxHealth:0;
            if(ratio<=lightThreshold)Enter(1);
            if(ratio<=armorThreshold)Enter(2);
            if(ratio<=criticalThreshold)Enter(3);
        }
        void Enter(int stage)
        {
            int bit=1<<(stage-1);if((mask&bit)!=0)return;mask|=bit;Stage=stage;TransitionCount++;
            foreach(var r in body)if(r)r.sharedMaterial=stages[stage-1];
            foreach(var part in armor)if(!part.Detached&&part.Stage<=stage)
            {
                part.Detached=true;DetachedArmorCount++;
                RobotDestructionPool.ThrowArmor(part.Debris,stages[stage-1],interior,part.Pivot.position,part.Pivot.rotation,part.Pivot.lossyScale,armorLifetime,1);
                foreach(var view in part.Views)if(view)view.gameObject.SetActive(false);
            }
            if(stage==3)DetachCannon();
            if(health.CurrentHealth<=0)return;
            var e=sparks.emission;e.rateOverTime=stage==1?2:stage==2?5:10;sparks.Play(true);
            if(stage>1){var main=smoke.main;main.startSize=stage==2?.22f:.34f;main.startColor=new Color(.15f,.16f,.17f,stage==2?.22f:.36f);var emission=smoke.emission;emission.rateOverTime=stage==2?2:5;smoke.Play(true);}
        }
        void OnCannonLost(int index){if(index==0)DetachCannon();}
        // Executions v02 (round 3): the execution tore the cannon off and the player holds it. The death flow (stage 3 -> DetachCannon)
        // then only marks it lost: no second cannon (debris or collectible) is thrown out of the dead robot. Cleared on Cancel.
        public bool CannonTakenByExecution {get;private set;}
        public void MarkCannonTakenByExecution(bool taken){if(!CannonDetached)CannonTakenByExecution=taken;}
        public bool DetachCannon()
        {
            if(CannonDetached)return false;
            if(CannonTakenByExecution){CannonDetached=true;combat.LoseCannon(0);foreach(var view in cannonViews)if(view)view.gameObject.SetActive(false);return true;}
            CannonDetached=true;CannonDropCount++;
            combat.LoseCannon(0);
            FallenCannon=!executionOnly&&collectibleCannonPrefab
                ?BigBotCannonDebris.SpawnCollectible(collectibleCannonPrefab,cannonPivot,GetComponentsInChildren<Collider>(),cannonFrontPivot)
                :BigBotCannonDebris.Spawn(cannonPivot,cannonLifetime,GetComponentsInChildren<Collider>(),cannonFrontPivot);
            foreach(var view in cannonViews)if(view)view.gameObject.SetActive(false);
            if(health.CurrentHealth>0)breakSparks.Emit(18);
            return true;
        }
        public void BeginDeath()
        {
            if(DeathStarted)return;Evaluate();DeathStarted=true;DeathCount++;StopEffects();breakSparks.Emit(14);
            deathVisualBase=animator.transform.localPosition;deathGround=transform.position.y+deathVisualBase.y*transform.lossyScale.y;animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;
            for(int i=1;i<animator.layerCount;i++)animator.SetLayerWeight(i,0);
            animator.SetFloat("MoveSpeed",0);animator.SetFloat("Turn",0);animator.CrossFadeInFixedTime("Base Layer.Death",.14f,0,0);
        }
        void LateUpdate()
        {
            if(!DeathStarted)
            {
                if(Stage>=3&&health.CurrentHealth>0&&Time.time>=nextCriticalSpark)
                {sparks.Emit(criticalSparkBurst);breakSparks.Emit(Mathf.Max(3,criticalSparkBurst/2));nextCriticalSpark=Time.time+criticalSparkInterval;}
                return;
            }
            deathAge+=Time.deltaTime;
            if(deathBurstEnabled&&!deathBurstRequested&&deathAge>=deathBurstPreparationAt){deathBurstRequested=true;if(RobotDestructionPool.Burst(sharedPoolTemplate,deathBurstAnchor,deathBurstAnchor?deathBurstAnchor.position:transform.position+Vector3.up,null,deathBurstScale))DeathBurstCount++;}
            float clipTime=animator.GetCurrentAnimatorStateInfo(0).IsName("Death")?animator.GetCurrentAnimatorStateInfo(0).normalizedTime*1.8f:deathAge;
            float offset=deathFloorOffset.Evaluate(Mathf.Clamp(clipTime,0,1.8f))*Mathf.Clamp01(deathAge/.14f);animator.transform.localPosition=deathVisualBase+Vector3.up*(offset/transform.lossyScale.y);
            // Ground the two actual sole volumes during the blend from locomotion into the native clip.
            // Cached real sole vertices cover all foot LODs; no per-frame skin baking or physics ownership.
            float soleY=float.PositiveInfinity;
            if(soles!=null)foreach(var sole in soles){var matrix=sole.Bone.localToWorldMatrix;foreach(var point in sole.Points)soleY=Mathf.Min(soleY,matrix.m10*point.x+matrix.m11*point.y+matrix.m12*point.z+matrix.m13);}
            if(soleY<deathGround+.025f)animator.transform.position+=Vector3.up*(deathGround+.025f-soleY);
            float flicker=deathAge<.45f?(Mathf.Sin(deathAge*85)>0?.9f:.3f):.3f;
            power.SetColor("_BaseColor",new Color(flicker,flicker*.96f,flicker*.92f,1));power.SetColor("_EmissionColor",Color.black);
            foreach(var r in body)if(r)r.SetPropertyBlock(power);
            if(!impactPlayed&&deathAge>=1.25f){impactPlayed=true;breakSparks.Emit(8);if(collapseSound&&audioSource)audioSource.PlayOneShot(collapseSound,.5f);}
            if(deathAge>1.8f){sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        }
        void StopEffects(){if(sparks)sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(smoke)smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(breakSparks)breakSparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
    }
}
