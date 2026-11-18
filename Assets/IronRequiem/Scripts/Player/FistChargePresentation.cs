using UnityEngine;
namespace IronRequiem
{
    /// Charged punch presentation: cyan energy follows the active fist or mounted forearm while F is held; the charge hum
    /// rises in pitch, and a full charge flashes. Reads ProtagonistMelee only; no gameplay here. Zero GC per frame.
    [DefaultExecutionOrder(120)]
    public sealed class FistChargePresentation : MonoBehaviour
    {
        [SerializeField] ProtagonistMelee melee;
        [SerializeField] Transform fist;
        [SerializeField] ParticleSystem energy;
        [SerializeField] ParticleSystem fullBurst;
        [SerializeField] Light glow;
        [SerializeField] AudioSource hum;
        [SerializeField] AudioClip fullChargeClip;
        [SerializeField,Range(.3f,1.5f)] float humPitchStart=.6f;
        [SerializeField,Range(.5f,3f)] float humPitchFull=1.55f;
        [SerializeField,Min(0)] float glowIntensity=2.4f;
        [SerializeField] float energyRateStart=8,energyRateFull=70;
        float flash;
        ProtagonistHandsPresentation hands;
        void Awake(){hands=GetComponentInParent<ProtagonistHandsPresentation>();}
        public float Flash=>flash;
        public void Configure(ProtagonistMelee attack,Transform hand,ParticleSystem build,ParticleSystem burst,Light light,AudioSource loop,AudioClip full)
        {melee=attack;fist=hand;energy=build;fullBurst=burst;glow=light;hum=loop;fullChargeClip=full;}
        void OnEnable(){if(melee){melee.ChargeStarted+=OnStart;melee.ChargeFull+=OnFull;melee.ChargeEnded+=OnEnd;}OnEnd();}
        void OnDisable(){if(melee){melee.ChargeStarted-=OnStart;melee.ChargeFull-=OnFull;melee.ChargeEnded-=OnEnd;}OnEnd();}
        void OnStart(){if(energy){energy.Clear(true);energy.Play(true);}if(hum&&hum.clip){hum.pitch=humPitchStart;hum.volume=.15f;hum.Play();}}
        void OnFull(){flash=1;if(fullBurst)fullBurst.Play(true);if(hum&&fullChargeClip)hum.PlayOneShot(fullChargeClip,.9f);}
        void OnEnd(){if(energy)energy.Stop(true,ParticleSystemStopBehavior.StopEmitting);if(hum)hum.Stop();if(glow)glow.enabled=false;}
        void LateUpdate()
        {
            if(!melee)return;
            flash=Mathf.MoveTowards(flash,0,Time.unscaledDeltaTime*4f);
            var anchor=fist;
            if(hands)anchor=melee.ArmedStrike?hands.RightHand:melee.LeftStrike?hands.LeftHandBone:hands.RightHand;
            if(anchor)
            {
                Vector3 at=anchor.position;
                if(hands)
                {
                    var forearm=melee.LeftStrike?hands.LeftForearm:hands.RightForearm;
                    at=melee.ArmedStrike?Vector3.Lerp(forearm.position,anchor.position,.7f):anchor.position+(anchor.position-forearm.position).normalized*.08f;
                }
                if(energy)energy.transform.position=at;
                if(fullBurst)fullBurst.transform.position=at;
                if(glow)glow.transform.position=at;
            }
            if(!melee.IsCharging){if(glow&&flash<=0)glow.enabled=false;else if(glow){glow.enabled=true;glow.intensity=glowIntensity*2f*flash;}return;}
            float level=melee.ChargeLevel;
            if(energy){var emission=energy.emission;emission.rateOverTime=Mathf.Lerp(energyRateStart,energyRateFull,level);}
            if(hum&&hum.isPlaying){hum.pitch=Mathf.Lerp(humPitchStart,humPitchFull,level);hum.volume=Mathf.Lerp(.15f,.55f,level);}
            if(glow){glow.enabled=true;glow.intensity=glowIntensity*(level*level+2f*flash)*(melee.FullyCharged?1f+.25f*Mathf.Sin(Time.time*30f):1f);}
        }
    }
}
