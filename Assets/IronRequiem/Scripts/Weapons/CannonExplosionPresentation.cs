using UnityEngine;

namespace IronRequiem
{
    // Presentation only: particles, light, audio and camera impulse never apply damage.
    [RequireComponent(typeof(AudioSource))]
    public sealed class CannonExplosionPresentation : MonoBehaviour
    {
        [SerializeField, Min(.5f)] float duration=3.2f;
        [SerializeField] ParticleSystem[] layers;
        [SerializeField] Transform shockwave;
        [SerializeField] Light flash;
        [SerializeField, Min(.01f)] float flashDuration=.12f;
        [SerializeField, Min(0)] float flashIntensity=7;
        [SerializeField, Min(.1f)] float shockwaveRadius=4.5f;
        [SerializeField, Min(.01f)] float shockwaveDuration=.38f;
        [SerializeField, Min(0)] float shakeRadius=14;
        [SerializeField, Range(0,1)] float shakeStrength=.85f;
        [SerializeField, Min(.01f)] float shakeDuration=.3f;
        [SerializeField] AnimationCurve expansion=AnimationCurve.EaseInOut(0,.02f,1,1);
        [SerializeField] Color pressureColor=new Color(1,.55f,.12f,1);
        static int liveLights;
        float started;
        bool ownsLight;
        Renderer ringRenderer;
        MaterialPropertyBlock block;
        public float Duration=>duration;
        public ParticleSystem[] Layers=>layers;
        public Light Flash=>flash;
        public static int ActiveFlashLights=>liveLights;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetBudget()=>liveLights=0;
        public void Configure(ParticleSystem[] effects,Transform ring,Light light)
        {layers=effects;shockwave=ring;flash=light;}
        void Awake()
        {
            block=new MaterialPropertyBlock();
            if(shockwave)ringRenderer=shockwave.GetComponent<Renderer>();
        }
        void OnEnable()
        {
            if(!Application.isPlaying)return;
            started=Time.time;
            foreach(var layer in layers)if(layer){layer.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);layer.Play(false);}
            var camera=Camera.main;
            ownsLight=flash && liveLights<2 && camera && Vector3.Distance(camera.transform.position,transform.position)<18;
            if(ownsLight)liveLights++;
            if(flash){flash.enabled=ownsLight;flash.intensity=flashIntensity;}
            if(shockwave){shockwave.gameObject.SetActive(true);shockwave.localScale=Vector3.one*.05f;}
            var audio=GetComponent<AudioSource>();if(audio.clip)audio.Play();
            DistanceCameraImpulse.Emit(transform.position,shakeRadius,shakeStrength,shakeDuration);
            Destroy(gameObject,duration);
        }
        void Update()
        {
            float age=Time.time-started;
            if(flash && ownsLight)
            {
                flash.intensity=flashIntensity*Mathf.Pow(Mathf.Clamp01(1-age/flashDuration),2);
                if(age>=flashDuration)ReleaseLight();
            }
            if(!shockwave || !ringRenderer)return;
            float fraction=Mathf.Clamp01(age/shockwaveDuration);
            shockwave.localScale=Vector3.one*(2*shockwaveRadius*expansion.Evaluate(fraction));
            var color=pressureColor;color.a=Mathf.Pow(1-fraction,2)*.7f;
            block.SetColor("_BaseColor",color);ringRenderer.SetPropertyBlock(block);
            if(fraction>=1)shockwave.gameObject.SetActive(false);
        }
        void ReleaseLight(){if(ownsLight){liveLights=Mathf.Max(0,liveLights-1);ownsLight=false;}if(flash)flash.enabled=false;}
        void OnDisable()=>ReleaseLight();
    }
}
