using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    [DefaultExecutionOrder(110)]
    [RequireComponent(typeof(Health))]
    public sealed class RobotDamageProgression : MonoBehaviour, IEnemyDeathPresentation
    {
        [SerializeField, Range(.01f, .99f)] float lightThreshold = .75f;
        [SerializeField, Range(.01f, .99f)] float armorThreshold = .5f;
        [SerializeField, Range(.01f, .99f)] float criticalThreshold = .25f;
        [SerializeField, Min(.5f)] float debrisLifetime = 2.8f;
        [SerializeField] LayerMask debrisGroundMask = 1;
        [SerializeField] Renderer[] bodyRenderers;
        [SerializeField] Material[] damagedMaterials;
        [SerializeField] Material interiorMaterial;
        [SerializeField] RobotArmorPiece[] armor;
        [SerializeField] ParticleSystem sparks;
        [SerializeField] ParticleSystem smoke;
        [SerializeField] Transform chest;
        [SerializeField] GameObject deathBurstPrefab;
        [SerializeField] bool preserveLegacyDeathEffect;
        [SerializeField] RobotDetachedArm criticalArm;
        Health health;
        int mask, stage;
        bool dead, deathStarted, fallback;
        float fallbackAge;
        public int Stage => stage;
        public int TransitionMask => mask;
        public int TransitionCount { get; private set; }
        public int DeathPresentationCount { get; private set; }
        public RobotArmorPiece[] Armor => armor;
        public ParticleSystem Smoke => smoke;
        public ParticleSystem Sparks => sparks;
        public bool IsDead => dead;
        public RobotDetachedArm CriticalArm => criticalArm;
        public void ConfigureArm(RobotDetachedArm arm) => criticalArm=arm;
        public void Configure(Renderer[] renderers, Material[] materials, Material inside, RobotArmorPiece[] plates, ParticleSystem electric, ParticleSystem vapor, Transform anchor, GameObject burst)
        { bodyRenderers = renderers; damagedMaterials = materials; interiorMaterial = inside; armor = plates; sparks = electric; smoke = vapor; chest = anchor; deathBurstPrefab = burst; }
        void Awake() { health = GetComponent<Health>(); RobotDestructionPool.Prepare(deathBurstPrefab, gameObject.scene); }
        void OnEnable() { if (!health) health = GetComponent<Health>(); health.OnDamaged += OnDamage; health.OnDie += OnDeath; }
        void OnDisable() { health.OnDamaged -= OnDamage; health.OnDie -= OnDeath; StopLoops(); }
        void OnValidate()
        {
            lightThreshold = Mathf.Clamp(lightThreshold, .03f, .99f);
            armorThreshold = Mathf.Clamp(armorThreshold, .02f, lightThreshold - .01f);
            criticalThreshold = Mathf.Clamp(criticalThreshold, .01f, armorThreshold - .01f);
        }
        void OnDamage(float amount, GameObject source)
        {
            if (dead) return;
            float ratio = health.MaxHealth > 0 ? health.CurrentHealth / health.MaxHealth : 0;
            if (ratio < lightThreshold) Enter(0);
            if (ratio < armorThreshold) Enter(1);
            if (ratio < criticalThreshold) Enter(2);
        }
        void Enter(int index)
        {
            int bit = 1 << index; if ((mask & bit) != 0) return;
            mask |= bit; stage = index + 1; TransitionCount++;
            var material = damagedMaterials[index];
            if(index==2 && criticalArm)criticalArm.Detach(material,debrisLifetime,debrisGroundMask);
            foreach (var renderer in bodyRenderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = i == 0 ? material : interiorMaterial;
                renderer.sharedMaterials = materials;
            }
            if (index > 0) foreach (var plate in armor) if (plate.LossStage <= index) plate.Detach(material, debrisLifetime, debrisGroundMask);
            if (health.CurrentHealth <= 0) return; // Crossed stages remain idempotent; skip transient smoke on a lethal hit.
            var emission = sparks.emission; emission.rateOverTime = index == 0 ? 4 : index == 1 ? 9 : 20;
            if (!sparks.isPlaying) sparks.Play();
            // F5: each threshold (75/50/25 %) gets a readable burst so the stage change reads at range.
            sparks.Emit(index == 0 ? 10 : index == 1 ? 18 : 28);
            if (index > 0 && smoke) smoke.Emit(index * 3);
            if (index > 0)
            {
                var smokeMain = smoke.main; smokeMain.startSize = index == 1 ? .22f : .36f;
                smokeMain.startColor = new Color(.11f, .12f, .13f, index == 1 ? .38f : .62f);
                var smokeEmission = smoke.emission; smokeEmission.rateOverTime = index == 1 ? 3 : 7;
                if (!smoke.isPlaying) smoke.Play();
            }
        }
        void OnDeath()
        {
            if (dead) return; dead = true; StopLoops();
            foreach (var canvas in GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
        }
        public bool TryPlayDeathPresentation()
        {
            if (!isActiveAndEnabled || preserveLegacyDeathEffect) return false;
            if (deathStarted) return true;
            deathStarted = true; DeathPresentationCount++;
            if (!RobotDestructionPool.Burst(chest, chest ? chest.position : transform.position + Vector3.up, HideBody)) { fallback = true; fallbackAge = 0; }
            return true;
        }
        void Update()
        {
            if (!fallback) return; fallbackAge += Time.deltaTime;
            if (fallbackAge < .28f) return;
            fallback = false; sparks.Emit(8); smoke.Emit(3); HideBody();
        }
        void HideBody()
        {
            if (!this) return;
            foreach (var renderer in bodyRenderers) if (renderer) renderer.gameObject.SetActive(false);
        }
        void StopLoops()
        {
            if (sparks) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (smoke) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
