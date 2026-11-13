using Unity.FPS.AI;
using UnityEngine;

namespace IronRequiem
{
    /// Enemy cannon: the weapon muzzle (projectile spawn, muzzle flash, charge effect) sits on the mouth of the cannon
    /// module carried by the arm, instead of at the template "eye laser" origin.
    /// The mouth is measured once from the module meshes (far end of the longest axis). Runs after T07CannonSocket.
    /// With aimAtTarget the muzzle also faces the target when it is inside the barrel cone, so shots leave the
    /// cannon toward the player even if the animated arm points elsewhere.
    [DefaultExecutionOrder(220)]
    public sealed class EnemyMuzzleAnchor : MonoBehaviour
    {
        [SerializeField] Transform muzzle;
        [SerializeField] Transform module;
        [SerializeField] DetachableCannon joint;
        [SerializeField, Min(0)] float forwardOffset = .05f;
        [SerializeField] bool aimAtTarget;
        [SerializeField, Range(0, 180)] float aimCone = 60;
        Vector3 mouthLocal, axisLocal; bool measured;
        EnemyController enemy;
        public Transform Muzzle => muzzle;

        public void Configure(Transform weaponMuzzle, Transform cannonModule, DetachableCannon detachable)
        { muzzle = weaponMuzzle; module = cannonModule; joint = detachable; measured = false; }
        public void SetAim(bool on, float cone) { aimAtTarget = on; aimCone = cone; }

        void Awake() => enemy = GetComponentInParent<EnemyController>();
        void Start() => Measure();

        public Vector3 MouthWorld()
        {
            if (!measured) Measure();
            return measured ? module.TransformPoint(mouthLocal) : (muzzle ? muzzle.position : transform.position);
        }

        public void Measure()
        {
            if (!module) return;
            bool any = false; var b = new Bounds();
            foreach (var r in module.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                var mf = r.GetComponent<MeshFilter>(); var mesh = mf ? mf.sharedMesh : (r as SkinnedMeshRenderer)?.sharedMesh; if (!mesh) continue;
                var mb = mesh.bounds; var toModule = module.worldToLocalMatrix * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = toModule.MultiplyPoint3x4(c); if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            if (!any) return;
            // barrel = longest axis in world size; mouth = the end farther from the mount (module parent origin)
            var s = Vector3.Scale(b.size, module.lossyScale);
            int axis = s.x >= s.y && s.x >= s.z ? 0 : s.y >= s.z ? 1 : 2;
            var dir = Vector3.zero; dir[axis] = 1;
            var origin = module.parent ? module.InverseTransformPoint(module.parent.position) : Vector3.zero;
            var hi = b.center; hi[axis] = b.max[axis]; var lo = b.center; lo[axis] = b.min[axis];
            bool hiFar = (hi - origin).sqrMagnitude >= (lo - origin).sqrMagnitude;
            mouthLocal = hiFar ? hi : lo; axisLocal = hiFar ? dir : -dir; measured = true;
        }

        void LateUpdate()
        {
            if (!measured || !muzzle || (joint && joint.IsDetached)) return;
            var axisWorld = module.TransformDirection(axisLocal).normalized;
            muzzle.position = module.TransformPoint(mouthLocal) + axisWorld * forwardOffset;
            if (!aimAtTarget) return;
            // Shots read the muzzle in the next Update, before the animator moves the arm again.
            var target = enemy ? enemy.KnownDetectedTarget : null;
            var dir = axisWorld;
            if (target)
            {
                var toTarget = (target.transform.position - muzzle.position).normalized;
                if (Vector3.Angle(axisWorld, toTarget) <= aimCone) dir = toTarget;
            }
            muzzle.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }
}
