using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    /// Cannon heat: muzzle glow grows with heat (single additive particle, no light) and steam vents while cooling.
    /// Reads WeaponHeat only; never changes gameplay. Built once in Awake, zero alloc per frame.
    [RequireComponent(typeof(WeaponHeat), typeof(WeaponController))]
    public sealed class HeatVentFx : MonoBehaviour
    {
        [SerializeField] Material glowMaterial;
        [SerializeField] Material steamMaterial;
        [SerializeField] Color glowCold = new Color(1f, .35f, .05f, 0f);
        [SerializeField] Color glowHot = new Color(2.4f, .7f, .12f, .85f);
        [SerializeField, Min(0)] float glowSize = .045f;
        [SerializeField, Min(0)] float steamRate = 60;
        [SerializeField, Range(0, 1)] float steamFrom = .3f;
        WeaponHeat heat; WeaponController weapon;
        ParticleSystem glow, steam; ParticleSystem.Particle[] one = new ParticleSystem.Particle[1];
        float last, ventTimer; Transform vent; bool useGlow;

        static readonly Quaternion UpRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        public void Configure(Material glowMat, Material steamMat) { glowMaterial = glowMat; steamMaterial = steamMat; }

        void Awake()
        {
            heat = GetComponent<WeaponHeat>(); weapon = GetComponent<WeaponController>();
            var muzzle = weapon.WeaponMuzzle ? weapon.WeaponMuzzle : transform;
            // Cells sit between camera and muzzle on the arm cannon: vent steam from the cells so it is seen,
            // and let PrimaryModuleHeatPresentation's red cells be the glow.
            var cells = GetComponent<Unity.FPS.Gameplay.WeaponFuelCellHandler>();
            vent = cells && cells.FuelCells != null && cells.FuelCells.Length > 0 && cells.FuelCells[0] ? cells.FuelCells[0].transform : muzzle;
            useGlow = vent == muzzle;
            glow = Make(muzzle, "VP_HeatGlow", glowMaterial, false); if (useGlow) glow.Play();
            steam = Make(vent, "VP_HeatSteam", steamMaterial, true);
        }

        void OnDisable() { if (steam) steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); if (glow) glow.Clear(true); last = 0; }

        void LateUpdate()
        {
            if (!heat.HeatEnabled) return;
            float h = heat.NormalizedHeat;
            // Glow: one particle kept alive at the muzzle, colour/size follow heat.
            if (useGlow && h > .05f)
            {
                if (glow.particleCount == 0) glow.Emit(1);
                glow.GetParticles(one, 1);
                one[0].remainingLifetime = 1; one[0].position = Vector3.zero;
                one[0].startColor = Color.Lerp(glowCold, glowHot, h * h);
                one[0].startSize = glowSize * Mathf.Lerp(.4f, 1.4f, h);
                glow.SetParticles(one, 1);
            }
            else if (glow.particleCount > 0) glow.Clear();
            // Steam: while heat drops (cooling) above threshold, or overheated.
            bool cooling = h < last - .0005f;
            ventTimer = (cooling && h > steamFrom) || heat.IsOverheated ? .2f : ventTimer - Time.deltaTime;
            var e = steam.emission; e.rateOverTime = ventTimer > 0 ? steamRate * Mathf.Max(h, .35f) : 0;
            if (ventTimer > 0 && !steam.isPlaying) steam.Play();
            if (vent) steam.transform.rotation = UpRotation;
            last = h;
        }

        ParticleSystem Make(Transform parent, string name, Material mat, bool isSteam)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(0, 0, .07f); go.layer = parent.gameObject.layer;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.playOnAwake = false; m.loop = true; m.scalingMode = ParticleSystemScalingMode.Local;
            var e = ps.emission; e.rateOverTime = 0;
            var s = ps.shape; s.enabled = isSteam;
            if (isSteam)
            {
                m.maxParticles = 40; m.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .8f); m.startSpeed = new ParticleSystem.MinMaxCurve(.25f, .6f);
                m.startSize = new ParticleSystem.MinMaxCurve(.04f, .08f); m.startColor = new Color(.9f, .9f, .92f, .26f);
                m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = -.12f;
                s.shapeType = ParticleSystemShapeType.Cone; s.angle = 22; s.radius = .02f;
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, 1.8f));
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(.9f, 0), new GradientAlphaKey(0, 1) }); col.color = g;
            }
            else { m.maxParticles = 1; m.startLifetime = 1; m.startSpeed = 0; m.simulationSpace = ParticleSystemSimulationSpace.Local; }
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.maxParticleSize = isSteam ? .12f : .03f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }
    }
}
