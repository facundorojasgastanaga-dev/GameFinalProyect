using UnityEngine;

namespace IronRequiem
{
    /// Weapons Redesign v01 (C-A cannon): the magenta charge window (material slot "Charge_Emissive") glows with
    /// CannonChargeShot.ChargeFraction, pulses near full charge and flashes on release, then decays.
    /// MaterialPropertyBlock per submesh only: no material instances, no allocation per frame, SRP Batcher friendly.
    [DisallowMultipleComponent]
    public sealed class ChargeWindowGlow : MonoBehaviour
    {
        [SerializeField] CannonChargeShot charge;
        [SerializeField] Renderer[] renderers = new Renderer[0];
        [SerializeField] Material chargeMaterial;
        [SerializeField] Color idle = new Color(.25f, .04f, .35f), full = new Color(3.2f, .5f, 4.4f);
        [SerializeField, Min(0)] float pulseHz = 9f, flashSeconds = .12f;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static MaterialPropertyBlock block;
        int[] slots; float flash; float applied = -1;

        public void Configure(CannonChargeShot source, Renderer[] targets, Material material) { charge = source; renderers = targets; chargeMaterial = material; }

        void Awake()
        {
            if (block == null) block = new MaterialPropertyBlock();
            slots = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                slots[i] = -1; if (!renderers[i]) continue;
                var mats = renderers[i].sharedMaterials;
                for (int s = 0; s < mats.Length; s++) if (mats[s] == chargeMaterial) { slots[i] = s; break; }
            }
        }
        void OnEnable() { if (charge) charge.ShotReleased += Released; applied = -1; }
        void OnDisable() { if (charge) charge.ShotReleased -= Released; flash = 0; Write(0, true); }
        void Released(float power) => flash = Mathf.Max(flash, Mathf.Lerp(.6f, 1f, power));

        void LateUpdate()
        {
            float f = charge ? charge.ChargeFraction : 0;
            float level = f * f;
            if (f > .85f) level += .15f * Mathf.Sin(Time.time * pulseHz * 6.2831853f);
            if (flash > 0) { level = Mathf.Max(level, flash * 1.4f); flash = Mathf.Max(0, flash - Time.deltaTime / Mathf.Max(.01f, flashSeconds)); }
            Write(level, false);
        }

        void Write(float level, bool force)
        {
            if (!force && Mathf.Abs(level - applied) < .002f) return;
            applied = level;
            var color = Color.LerpUnclamped(idle, full, Mathf.Clamp(level, 0, 1.4f));
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i] || slots == null || slots[i] < 0) continue;
                renderers[i].GetPropertyBlock(block, slots[i]); block.SetColor(EmissionColor, color); renderers[i].SetPropertyBlock(block, slots[i]);
            }
        }
    }
}
