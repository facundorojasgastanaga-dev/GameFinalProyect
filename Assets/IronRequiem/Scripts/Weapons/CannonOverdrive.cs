using System.Linq;
using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class CannonOverdrive : MonoBehaviour
    {
        [SerializeField, Range(.01f, 1)] float criticalThreshold = .2f;
        [SerializeField, Min(.1f)] float criticalGraceDuration = 5;
        [SerializeField] ParticleSystem smoke;
        [SerializeField] AudioClip activationSound;
        [SerializeField] Color warningColor = new Color(1, .16f, .025f);
        StolenCannon module;
        CannonAmmo energy;
        MaterialPropertyBlock block;
        Renderer[] worldRenderers, equippedRenderers;
        float drainRate;
        public bool IsArmed { get; private set; }
        public float CriticalThreshold => criticalThreshold;
        public float CriticalGraceDuration => criticalGraceDuration;
        public float RemainingDangerSeconds => IsArmed && drainRate > 0 ? energy.Remaining / drainRate : 0;
        public ParticleSystem Smoke => smoke;
        public void Configure(AudioClip sound) => activationSound = sound;
        public void ConfigureSmoke(ParticleSystem effect) => smoke = effect;
        void Awake()
        {
            module = GetComponent<StolenCannon>(); energy = GetComponent<CannonAmmo>();
            block = new MaterialPropertyBlock();
            worldRenderers = GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            energy.EnergyChanged += CheckCritical;
        }
        public void SetEquippedVisual(WeaponController weapon) => equippedRenderers = weapon ? weapon.GetComponentsInChildren<Renderer>(true) : null;
        void CheckCritical()
        {
            if (!IsArmed && energy.Remaining <= energy.Capacity * criticalThreshold && module.State != StolenCannonState.Attached)
            {
                IsArmed = true; drainRate = Mathf.Max(.001f, energy.Remaining / criticalGraceDuration);
                if (module.EquippedWeapon) module.EquippedWeapon.CancelPendingShot();
                if (activationSound)
                {
                    var audio = GetComponent<AudioSource>(); audio.pitch = 1.2f; audio.PlayOneShot(activationSound);
                }
                if (smoke) smoke.Play(true);
            }
            if (IsArmed && energy.Remaining <= .001f) module.Explosion.TryDetonate();
        }
        void Update()
        {
            if (!IsArmed || module.State == StolenCannonState.Detonated) return;
            if (Time.timeScale > 0) energy.Drain(drainRate * Time.deltaTime);
            if (Time.timeScale > 0 && energy.Remaining <= .001f) module.Explosion.TryDetonate();
            if (smoke) { var emission = smoke.emission; emission.enabled = Time.timeScale > 0; }
        }
        void LateUpdate()
        {
            if (!IsArmed) return;
            // Imported module axes differ from Unity up; smoke must rise in world space.
            if(smoke)smoke.transform.rotation=Quaternion.identity;
            float pulse = .65f + .35f * Mathf.Sin(Time.time * 16);
            block.SetColor("_BaseColor", Color.Lerp(warningColor, Color.yellow, pulse * .5f));
            Apply(worldRenderers); Apply(equippedRenderers);
        }
        void Apply(Renderer[] renderers)
        {
            if (renderers == null) return;
            foreach (var renderer in renderers) if (renderer) renderer.SetPropertyBlock(block);
        }
        public void StopEffects() { if (smoke) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        void OnDestroy() { if (energy) energy.EnergyChanged -= CheckCritical; }
    }
}
