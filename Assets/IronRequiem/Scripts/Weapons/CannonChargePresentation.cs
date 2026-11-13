using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(CannonChargeShot))]
    public sealed class CannonChargePresentation : MonoBehaviour
    {
        [SerializeField] GameObject effectRoot;
        [SerializeField] Transform energyCore;
        [SerializeField] ParticleSystem energyParticles;
        [SerializeField] Transform outerFrame;
        [SerializeField] Transform innerFrame;
        [SerializeField] Transform energyRays;
        [SerializeField] Transform energySpindle;
        [SerializeField, Min(.001f)] float minimumDiameter = .035f;
        [SerializeField, Min(.001f)] float maximumDiameter = .18f;
        [SerializeField, Min(0)] float releaseDuration = .085f;
        [Header("Reference vortex variant (presentation only)")]
        [SerializeField] bool referenceVortex;
        [SerializeField] Transform blueFilaments, whiteFilaments, violetOuter, violetInner, orbitMarks, bluePulseRing;
        Renderer[] vortexRenderers;
        MaterialPropertyBlock vortexBlock;
        public bool UsesReferenceVortex => referenceVortex;
        public void ConfigureReference(Transform blue, Transform white, Transform outer, Transform inner, Transform marks, Transform pulse)
        { referenceVortex=true;blueFilaments=blue;whiteFilaments=white;violetOuter=outer;violetInner=inner;orbitMarks=marks;bluePulseRing=pulse; }
        CannonChargeShot charge;
        WeaponController weapon;
        float clock, releaseRemaining, releasePower;
        bool wasCharging;
        public bool IsVisible => effectRoot && effectRoot.activeInHierarchy;
        public ParticleSystem EnergyParticles => energyParticles;
        public Transform EnergyCore => energyCore;
        public Transform EffectOrigin => effectRoot ? effectRoot.transform : null;

        public void Configure(GameObject root, Transform core, ParticleSystem particles)
        { effectRoot = root; energyCore = core; energyParticles = particles; Hide(); }

        public void ConfigureFrames(Transform outer, Transform inner, Transform rays, Transform spindle = null)
        { outerFrame = outer; innerFrame = inner; energyRays = rays; energySpindle = spindle; }

        void Awake() { charge = GetComponent<CannonChargeShot>(); weapon = GetComponent<WeaponController>(); Hide(); }
        void OnEnable()
        {
            if (!charge) charge = GetComponent<CannonChargeShot>();
            charge.ChargeCancelled += Hide;
            charge.ShotReleased += Release;
        }
        void Release(float power)
        {
            if ((!outerFrame && !referenceVortex) || !effectRoot || Time.timeScale <= 0) return;
            releasePower = power; releaseRemaining = releaseDuration;
            effectRoot.SetActive(true);
            energyParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            energyParticles.Emit(8);
        }
        void LateUpdate()
        {
            if (!charge || Time.timeScale <= 0 || !weapon.IsWeaponActive) { Hide(); return; }
            if (!effectRoot || !energyCore || !energyParticles) return;
            if (referenceVortex) { UpdateReference(); return; }
            if (!charge.IsCharging)
            {
                if (releaseRemaining <= 0) { Hide(); return; }
                releaseRemaining = Mathf.Max(0, releaseRemaining - Time.deltaTime);
                float t = 1 - releaseRemaining / Mathf.Max(.001f, releaseDuration);
                float diameter = Mathf.Lerp(minimumDiameter, maximumDiameter, releasePower);
                // Collapse into the shot. Presentation never applies damage or spawns a projectile.
                energyCore.localScale = Vector3.one * diameter * Mathf.Lerp(1.25f, .08f, t);
                outerFrame.localScale = Vector3.one * diameter * Mathf.Lerp(1.7f, .05f, t * t);
                innerFrame.localScale = outerFrame.localScale * .7f;
                energyRays.localScale = Vector3.one * diameter * Mathf.Lerp(1.6f, 0, t);
                if (energySpindle) energySpindle.localScale = new Vector3(diameter * .12f, diameter * .12f, diameter * (1 - t));
                return;
            }
            clock += Time.deltaTime;
            float fraction = charge.ChargeFraction;
            float smooth = Mathf.SmoothStep(0, 1, fraction);
            float pulse = Mathf.Pow(.5f + .5f * Mathf.Sin(clock * 18), 3);
            float diameterNow = Mathf.Lerp(minimumDiameter, maximumDiameter, smooth);
            energyCore.localScale = Vector3.one * diameterNow * (1 + .06f * pulse);
            if (outerFrame)
            {
                float breathing = Mathf.Lerp(.72f, 1.35f, pulse);
                outerFrame.localScale = Vector3.one * diameterNow * breathing;
                outerFrame.localRotation = Quaternion.Euler(18 * Mathf.Sin(clock * 3), 30 * Mathf.Cos(clock * 2), clock * Mathf.Lerp(65, 130, fraction));
                innerFrame.localScale = Vector3.one * diameterNow * breathing * .76f;
                // A short energy column grows OUT of the mouth. Its back face and emission
                // origin stay at the fitted socket; the front face supplies readable depth in FPS.
                innerFrame.localPosition = Vector3.forward * diameterNow;
                innerFrame.localRotation = Quaternion.Euler(48 + 20 * Mathf.Sin(clock * 2), -32, 45 - clock * Mathf.Lerp(52, 100, fraction));
                energyRays.localScale = Vector3.one * diameterNow * Mathf.Lerp(.9f, 1.45f, 1 - pulse);
                energyRays.localRotation = Quaternion.Euler(0, 0, -clock * 95);
                energyRays.localPosition = Vector3.forward * diameterNow;
                if (energySpindle) energySpindle.localScale = new Vector3(diameterNow * .12f, diameterNow * .12f, diameterNow);
            }
            var emission = energyParticles.emission; emission.rateOverTime = Mathf.Lerp(14, 52, fraction);
            var shape = energyParticles.shape; shape.radius = Mathf.Lerp(.05f, .16f, smooth);
            var velocity = energyParticles.velocityOverLifetime; velocity.orbitalZ = Mathf.Lerp(2, 6, fraction);
            if (!effectRoot.activeSelf)
            {
                effectRoot.SetActive(true);
            }
            // Emit() during the release can report isPlaying while emission is stopped.
            // Restart explicitly on the next charge, even when the root is already active.
            if (!wasCharging)
            {
                energyParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                energyParticles.Play(true);
            }
            wasCharging = true;
        }
        void UpdateReference()
        {
            bool releasing=!charge.IsCharging;
            if(releasing&&releaseRemaining<=0){Hide();return;}
            if(vortexBlock==null)vortexBlock=new MaterialPropertyBlock();
            if(vortexRenderers==null)vortexRenderers=effectRoot.GetComponentsInChildren<Renderer>(true);
            float fraction=releasing?Mathf.Lerp(.2f,1,releasePower):charge.ChargeFraction;
            float phase=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.015f,.12f,fraction));
            float t=0;
            if(releasing){releaseRemaining=Mathf.Max(0,releaseRemaining-Time.deltaTime);t=1-releaseRemaining/Mathf.Max(.001f,releaseDuration);}
            else clock+=Time.deltaTime;
            float pulse=.94f+.06f*Mathf.Sin(clock*17);
            float radial=Mathf.Lerp(.55f,1,phase)*pulse;
            float collapse=releasing?Mathf.Lerp(1,.025f,t):1;
            effectRoot.SetActive(true);
            energyCore.localScale=Vector3.one*(Mathf.Lerp(.025f,.043f,phase)*pulse*(releasing?Mathf.Lerp(1.8f,.1f,t):1));
            blueFilaments.localScale=Vector3.one*radial*collapse;
            whiteFilaments.localScale=blueFilaments.localScale;
            blueFilaments.localRotation=Quaternion.Euler(0,0,clock*19+4*Mathf.Sin(clock*5));
            whiteFilaments.localRotation=blueFilaments.localRotation;
            float arcGrowth=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.1f,fraction));
            violetOuter.localScale=Vector3.one*arcGrowth*(releasing?1-t:1);
            violetInner.localScale=violetOuter.localScale;
            violetOuter.localRotation=Quaternion.Euler(0,0,clock*105);
            violetInner.localRotation=Quaternion.Euler(0,0,-clock*145+22);
            orbitMarks.localScale=violetOuter.localScale;
            orbitMarks.localRotation=Quaternion.Euler(0,0,clock*71);
            float ringPhase=Mathf.Clamp01(Mathf.InverseLerp(.015f,.075f,fraction));
            bluePulseRing.localScale=Vector3.one*(releasing?Mathf.Lerp(.085f,.19f,t):Mathf.Lerp(.025f,.11f,ringPhase));
            float initialRing=1-Mathf.SmoothStep(.07f,.13f,fraction);
            bluePulseRing.gameObject.SetActive(releasing||fraction<.15f);
            foreach(var renderer in vortexRenderers)
            {
                if(renderer is ParticleSystemRenderer)continue;
                var color=renderer.sharedMaterial.GetColor("_BaseColor");
                float brightness=Mathf.Lerp(.7f,1.15f,Mathf.SmoothStep(0,1,fraction));
                color.r*=brightness;color.g*=brightness;color.b*=brightness;
                color.a*=renderer.transform==bluePulseRing?(releasing?1-t:initialRing*.8f):(releasing?1-t:Mathf.Lerp(.35f,1,phase));
                vortexBlock.SetColor("_BaseColor",color);renderer.SetPropertyBlock(vortexBlock);
            }
            if(releasing){wasCharging=false;return;}
            var emission=energyParticles.emission;emission.rateOverTime=Mathf.Lerp(9,28,fraction);
            if(!wasCharging){energyParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);energyParticles.Play(true);}
            wasCharging=true;
        }
        void Hide()
        {
            clock = 0; releaseRemaining = 0; wasCharging = false;
            if (energyParticles && (energyParticles.IsAlive(true) || energyParticles.isPlaying))
                energyParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (effectRoot && effectRoot.activeSelf) effectRoot.SetActive(false);
        }
        void OnDisable()
        {
            if (charge) { charge.ChargeCancelled -= Hide; charge.ShotReleased -= Release; }
            Hide();
        }
    }
}
