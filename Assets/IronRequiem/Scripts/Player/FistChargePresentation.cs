using UnityEngine;
namespace IronRequiem
{
    /// v4 charged punch presentation: cyan energy builds on the left fist while M1 is held, the charge hum
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
            if(fist)
            {
                if(energy)energy.transform.position=fist.position;
                if(fullBurst)fullBurst.transform.position=fist.position;
                if(glow)glow.transform.position=fist.position;
            }
            if(!melee.IsCharging){if(glow&&flash<=0)glow.enabled=false;else if(glow){glow.enabled=true;glow.intensity=glowIntensity*2f*flash;}return;}
            float level=melee.ChargeLevel;
            if(energy){var emission=energy.emission;emission.rateOverTime=Mathf.Lerp(energyRateStart,energyRateFull,level);}
            if(hum&&hum.isPlaying){hum.pitch=Mathf.Lerp(humPitchStart,humPitchFull,level);hum.volume=Mathf.Lerp(.15f,.55f,level);}
            if(glow){glow.enabled=true;glow.intensity=glowIntensity*(level*level+2f*flash)*(melee.FullyCharged?1f+.25f*Mathf.Sin(Time.time*30f):1f);}
        }
    }
}
