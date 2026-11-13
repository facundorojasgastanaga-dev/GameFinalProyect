using Unity.FPS.Game;
using UnityEngine;
namespace IronRequiem
{
    /// v4 slide invulnerability feedback: a subtle cue when the guard window opens and sparks + a deflect ping
    /// for every hit the window ignores (health untouched). Presentation only; the window itself lives in
    /// AdvancedPlayerMovement -> Health.GrantInvulnerability. Zero GC per frame.
    public sealed class SlideGuardFeedback : MonoBehaviour
    {
        [SerializeField] AdvancedPlayerMovement movement;
        [SerializeField] Health health;
        [SerializeField] Transform chest;
        [SerializeField] GameObject deflectSparks;
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip guardClip,deflectClip;
        [SerializeField,Range(0,1)] float guardVolume=.35f;
        float deflectFlash;
        public int IgnoredHits {get;private set;}
        public float LastIgnoredTime {get;private set;}=-10;
        /// 0..1 edge intensity for SlideGuardScreenFx: strong at the start of the window, a pulse on each deflect.
        public float EdgeIntensity
        {
            get
            {
                float window=0;
                if(movement&&movement.SlideGuardActive&&movement.SlideInvulnerability>0)
                    window=Mathf.Clamp01((movement.SlideGuardUntil-Time.time)/movement.SlideInvulnerability);
                return Mathf.Max(window*.75f,deflectFlash);
            }
        }
        public void Configure(AdvancedPlayerMovement move,Health life,Transform body,GameObject sparks,AudioSource audio,AudioClip guard,AudioClip deflect)
        {movement=move;health=life;chest=body;deflectSparks=sparks;source=audio;guardClip=guard;deflectClip=deflect;}
        void OnEnable(){if(movement)movement.SlideStarted+=OnSlide;if(health)health.OnDamageIgnored+=OnIgnored;}
        void OnDisable(){if(movement)movement.SlideStarted-=OnSlide;if(health)health.OnDamageIgnored-=OnIgnored;}
        void Update(){deflectFlash=Mathf.MoveTowards(deflectFlash,0,Time.unscaledDeltaTime*3.5f);}
        void OnSlide(){if(source&&guardClip&&movement.SlideInvulnerability>0){source.pitch=Random.Range(.97f,1.05f);source.PlayOneShot(guardClip,guardVolume);}}
        void OnIgnored(float damage,GameObject damageSource)
        {
            // Only the slide window shows this feedback (execution protection has its own presentation).
            if(!movement||!movement.SlideGuardActive)return;
            IgnoredHits++;LastIgnoredTime=Time.time;deflectFlash=1;
            Vector3 body=chest?chest.position:transform.position+Vector3.up*1.1f;
            Vector3 from=damageSource?damageSource.transform.position:body+transform.forward;
            Vector3 dir=from-body;dir=dir.sqrMagnitude>1e-4f?dir.normalized:transform.forward;
            if(deflectSparks)FxPool.Spawn(deflectSparks,body+dir*.45f,Quaternion.LookRotation(dir),1f);
            if(source&&deflectClip){source.pitch=Random.Range(1.05f,1.2f);source.PlayOneShot(deflectClip,.8f);}
        }
    }
}
