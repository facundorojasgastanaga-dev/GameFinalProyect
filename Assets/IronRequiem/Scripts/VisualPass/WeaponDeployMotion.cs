using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    /// Weapon switch presentation.
    /// The FPS template only toggles WeaponRoot when a weapon is shown/hidden, so pieces that live outside it
    /// (fuel-cell tubes, their steam) stayed visible on their own and "loaded" before the weapon.
    /// This keeps every renderer outside WeaponRoot in sync with it, and plays the deploy each time the weapon is
    /// shown: the whole module (cells included) comes out retracted along its barrel and expands as one piece.
    /// Arms-fix (19/11/2026): a weapon whose IWeaponSwitchMotion animates its own switch (ArmModuleVisual: unlock,
    /// eject, dock) is shown at full size. Scaling it as well stacked two animations and, since this runs after
    /// ProtagonistHandsPresentation (110), left the forearm fitted to last frame's scale (2-4 cm gap while docking).
    /// Renderer sync and SkipNextDeploy are unchanged.
    /// Zero alloc per frame.
    [RequireComponent(typeof(WeaponController))]
    [DefaultExecutionOrder(150)]
    public sealed class WeaponDeployMotion : MonoBehaviour
    {
        [SerializeField, Min(.05f)] float seconds = .28f;
        [SerializeField] Vector3 retracted = new Vector3(.72f, .72f, .12f);
        [SerializeField, Range(0, 3)] float overshoot = 1.6f;
        [SerializeField] float twistDegrees = -18f;
        GameObject weaponRoot; Renderer[] outside; bool[] outsideEnabled;
        Vector3 baseScale; Quaternion baseRotation; float t = 1; bool shown;
        IWeaponSwitchMotion switchMotion;
        bool SwitchAnimatedElsewhere => switchMotion is Object owner && owner && switchMotion.OverridesSwitchMotion;

        void Awake()
        {
            var weapon = GetComponent<WeaponController>();
            TryGetComponent(out switchMotion);
            weaponRoot = weapon.WeaponRoot;
            baseScale = transform.localScale; baseRotation = transform.localRotation;
            var all = GetComponentsInChildren<Renderer>(true);
            int n = 0; foreach (var r in all) if (!weaponRoot || !r.transform.IsChildOf(weaponRoot.transform)) n++;
            outside = new Renderer[n]; outsideEnabled = new bool[n]; n = 0;
            foreach (var r in all) if (!weaponRoot || !r.transform.IsChildOf(weaponRoot.transform)) { outside[n] = r; outsideEnabled[n] = r.enabled; n++; }
        }

        bool skipNext;
        /// 16/11: the next show appears fully deployed (the montage station already animated the attachment).
        public void SkipNextDeploy() { skipNext = true; if (t < 1 && shown) { t = 1; Apply(1); skipNext = false; } }

        void OnEnable() { shown = false; Sync(IsShown()); }
        void OnDisable() { transform.localScale = baseScale; transform.localRotation = baseRotation; t = 1; }

        bool IsShown() => !weaponRoot || weaponRoot.activeInHierarchy;

        void LateUpdate()
        {
            bool now = IsShown();
            if (now != shown) Sync(now);
            if (t >= 1) return;
            // a deploy started before the switch-motion component was enabled (same-frame instantiate + show)
            if (SwitchAnimatedElsewhere) { t = 1; Apply(1); return; }
            t = Mathf.Min(1, t + Time.deltaTime / seconds);
            Apply(t);
        }

        void Sync(bool visible)
        {
            if (visible && !shown) { if (skipNext || SwitchAnimatedElsewhere) { skipNext = false; t = 1; Apply(1); } else { t = 0; Apply(0); } }
            if (!visible) { transform.localScale = baseScale; transform.localRotation = baseRotation; t = 1; }
            shown = visible;
            for (int i = 0; i < outside.Length; i++)
                if (outside[i]) outside[i].enabled = visible && outsideEnabled[i];
        }

        void Apply(float x)
        {
            // Ease-out-back: fast extension, settles past full length then back.
            float c = overshoot, k = 1 + (c + 1) * Mathf.Pow(x - 1, 3) + c * Mathf.Pow(x - 1, 2);
            transform.localScale = Vector3.Scale(baseScale, Vector3.LerpUnclamped(retracted, Vector3.one, k));
            transform.localRotation = baseRotation * Quaternion.Euler(0, 0, twistDegrees * (1 - Mathf.Clamp01(k)));
        }
    }
}
