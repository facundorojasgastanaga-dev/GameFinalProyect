using UnityEngine;

namespace IronRequiem
{
    /// v5 montage station: one mechanical arm (base yaw + shoulder + elbow + wrist gripper) posed by an analytic
    /// two-bone solve towards a world target. Rigid segments only; driven every frame by WeaponMountStation, which owns
    /// the timing (pure function of the sequence time). Zero GC.
    public sealed class StationArmRig : MonoBehaviour
    {
        [SerializeField] Transform yaw, upper, fore, gripper;
        [SerializeField, Min(.1f)] float upperLength = 1.3f, foreLength = 1.2f;
        [SerializeField] Transform jawA, jawB;
        [SerializeField] float jawOpen = .07f, jawClosed = .025f;
        Vector3 restTarget; bool rested;
        public Transform Gripper => gripper;
        public float Reach => upperLength + foreLength;
        public void Configure(Transform yawPivot, Transform upperArm, Transform foreArm, Transform grip, float upperLen, float foreLen, Transform a, Transform b)
        { yaw = yawPivot; upper = upperArm; fore = foreArm; gripper = grip; upperLength = upperLen; foreLength = foreLen; jawA = a; jawB = b; }
        void Awake() { if (gripper) { restTarget = gripper.position; rested = true; } }
        public Vector3 RestTarget => rested ? restTarget : gripper.position;

        /// Points the gripper at target (world), its jaws facing along approach; grip 0 = open, 1 = closed.
        public void Pose(Vector3 target, Vector3 approach, float grip)
        {
            if (!yaw || !upper || !fore || !gripper) return;
            var basePos = yaw.position;
            var flat = Vector3.ProjectOnPlane(target - basePos, Vector3.up);
            if (flat.sqrMagnitude > 1e-6f) yaw.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            var shoulder = upper.position; var toTarget = target - shoulder; float d = toTarget.magnitude;
            float reach = upperLength + foreLength - .001f; d = Mathf.Clamp(d, Mathf.Abs(upperLength - foreLength) + .01f, reach);
            var dir = toTarget.normalized;
            // elbow bends upwards (away from the floor)
            float cosA = Mathf.Clamp((upperLength * upperLength + d * d - foreLength * foreLength) / (2 * upperLength * d), -1, 1);
            float a = Mathf.Acos(cosA);
            var bendAxis = Vector3.Cross(dir, Vector3.up); if (bendAxis.sqrMagnitude < 1e-6f) bendAxis = yaw.right; bendAxis.Normalize();
            var upperDir = Quaternion.AngleAxis(-a * Mathf.Rad2Deg, bendAxis) * dir;
            var elbow = shoulder + upperDir * upperLength;
            var end = shoulder + dir * d;
            upper.rotation = Quaternion.LookRotation(upperDir, Vector3.up);
            fore.position = elbow; var foreDir = (end - elbow).normalized; fore.rotation = Quaternion.LookRotation(foreDir, Vector3.up);
            gripper.position = end;
            var look = approach.sqrMagnitude > 1e-6f ? approach.normalized : foreDir;
            gripper.rotation = Quaternion.LookRotation(look, Vector3.up);
            float jaw = Mathf.Lerp(jawOpen, jawClosed, Mathf.Clamp01(grip));
            if (jawA) jawA.localPosition = new Vector3(-jaw, 0, jawA.localPosition.z);
            if (jawB) jawB.localPosition = new Vector3(jaw, 0, jawB.localPosition.z);
        }
    }
}
