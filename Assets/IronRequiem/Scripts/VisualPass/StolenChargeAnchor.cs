using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    /// Keeps the stolen weapon's muzzle (shots, muzzle flash, charge effect) on the mouth of the shell that
    /// StolenShellFit seats on the forearm. Before this the muzzle stayed at its authored prefab position while the
    /// shell moved every LateUpdate, so the charge appeared away from the cannon.
    /// Also adds the charge build-up on the module itself: growing vibration + swell, and a recoil pop on release.
    /// Runs after StolenShellFit (200). Position only: the muzzle keeps its own aim rotation. Zero alloc per frame.
    [DefaultExecutionOrder(210)]
    [RequireComponent(typeof(WeaponController))]
    public sealed class StolenChargeAnchor : MonoBehaviour
    {
        [SerializeField] Transform shell;
        [SerializeField] Transform muzzle;
        [Tooltip("Optional IR-ACOPLE-01 mouth socket of the shell (the module's 'Muzzle'). When set it replaces the bounds-based front.")]
        [SerializeField] Transform mouthSocket;
        [SerializeField, Min(0)] float forwardOffset = .015f;
        [SerializeField, Min(0)] float vibration = .0035f;
        [SerializeField, Range(0, .2f)] float swell = .05f;
        [SerializeField, Min(0)] float recoilDistance = .045f, recoilSeconds = .14f;
        CannonChargeShot charge; Vector3 mouthLocal, baseScale, basePos, writtenPos; bool measured, swollen, offset; float recoil;

        public void Configure(Transform shellRoot, Transform weaponMuzzle) { shell = shellRoot; muzzle = weaponMuzzle; }

        void Awake()
        {
            charge = GetComponent<CannonChargeShot>();
            if (shell) { baseScale = shell.localScale; Measure(); }
        }
        void OnEnable() { if (charge) charge.ShotReleased += Released; }
        void OnDisable() { if (charge) charge.ShotReleased -= Released; if (shell && swollen) { shell.localScale = baseScale; swollen = false; } if (shell && offset) shell.localPosition = basePos; offset = false; recoil = 0; }
        void Released(float power) => recoil = Mathf.Lerp(.5f, 1, power);

        // Front-centre of the shell meshes in shell-local space (one-time).
        void Measure()
        {
            if (mouthSocket) { mouthLocal = shell.InverseTransformPoint(mouthSocket.position); measured = true; return; }
            bool any = false; var b = new Bounds();
            foreach (var mf in shell.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                var mb = mf.sharedMesh.bounds; var toShell = shell.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = toShell.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            if (any) { mouthLocal = new Vector3(b.center.x, b.center.y, b.max.z); measured = true; }
        }

        void LateUpdate()
        {
            if (!measured || !muzzle) return;
            // Offsets are applied to the pose StolenShellFit wrote this frame. If nobody rewrote the shell since our
            // last write (fit inactive), reuse the stored base so offsets never accumulate.
            if (!offset || shell.localPosition != writtenPos) basePos = shell.localPosition;
            shell.localPosition = basePos; offset = false;
            float f = charge && charge.IsCharging ? charge.ChargeFraction : 0;
            if (f > 0)
            {
                // StolenShellFit rewrites the shell pose every frame, so these offsets never accumulate.
                float k = f * f, t = Time.time;
                shell.position += shell.rotation * new Vector3(Mathf.Sin(t * 71f), Mathf.Sin(t * 83f + 1.3f), 0) * (vibration * k)
                                  - shell.forward * (.012f * k);
                shell.localScale = baseScale * (1 + swell * k * (.75f + .25f * Mathf.Sin(t * 26f))); swollen = true;
            }
            else if (swollen) { shell.localScale = baseScale; swollen = false; }
            if (recoil > 0)
            {
                recoil = Mathf.Max(0, recoil - Time.deltaTime / Mathf.Max(.01f, recoilSeconds));
                shell.position -= shell.forward * (recoilDistance * recoil * recoil);
            }
            writtenPos = shell.localPosition; offset = true;
            muzzle.position = shell.TransformPoint(mouthLocal) + shell.forward * forwardOffset;
        }
    }
}
