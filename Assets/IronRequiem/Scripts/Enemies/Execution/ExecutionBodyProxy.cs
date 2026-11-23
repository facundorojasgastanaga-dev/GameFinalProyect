using UnityEngine;

namespace IronRequiem
{
    // Executions v02, Big Bot round 2 (22/11/2026). Cheap, pose-following stand-in for the robot's VISIBLE body.
    // The Big Bot has a single gameplay BoxCollider (3.5 x 3 x 2.3 m, far larger than its silhouette) and skinned parts whose
    // renderer bounds span the whole skeleton; neither tells where the armour really is. Each part here is the bone-space box of
    // one rigid skinned piece (Torso, Shoulder_R, Forearm_R, ...), baked in the Editor from the LOD0 meshes by
    // ExecutionsAuthoring (meshes are not CPU-readable in builds). Boxes move with their bone, so they follow the live pose.
    // Weapon parts (the cannon arm) are kept apart: the camera may come close to them, the hand closes on their surface.
    //
    // Rendered pose: during Update the bones hold the raw Animator pose; BigBotCombat's aim offset and ExecutionReaction are
    // added in LateUpdate and removed again before the next Animator pass. Queries made in Update would aim at a pose nobody
    // sees (0.3-0.6 m off on this 3.5x robot), so the bone matrices are cached here at the end of LateUpdate (order 500, after
    // both layers) and every query uses the last rendered pose, falling back to the live bones before the first cache.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class ExecutionBodyProxy : MonoBehaviour
    {
        [System.Serializable]
        public struct Part
        {
            public Transform bone;
            public Vector3 center;
            public Vector3 halfSize;
            public bool weapon;
            // Weapon parts only: up to 256 surface samples (bone space). The bone-space box of a long diagonal piece is far larger
            // than the piece, so the hand aims at the nearest sample instead of the box.
            public Vector3[] points;
        }

        [SerializeField] Part[] parts = new Part[0];
        Matrix4x4[] rendered; int renderedFrame = -1;

        public bool HasParts => parts != null && parts.Length > 0;
        public int PartCount => parts != null ? parts.Length : 0;
        public void SetParts(Part[] value) => parts = value ?? new Part[0];

        void LateUpdate()
        {
            if (parts == null) return;
            if (rendered == null || rendered.Length != parts.Length) rendered = new Matrix4x4[parts.Length];
            for (int i = 0; i < parts.Length; i++) if (parts[i].bone) rendered[i] = parts[i].bone.localToWorldMatrix;
            renderedFrame = Time.frameCount;
        }
        void OnDisable() => renderedFrame = -1;

        // Bone-to-world of a part as last rendered (live bone before the first LateUpdate or after a gap of more than one frame).
        Matrix4x4 ToWorld(int i) => rendered != null && i < rendered.Length && renderedFrame >= 0 && Time.frameCount - renderedFrame <= 1 ? rendered[i] : parts[i].bone.localToWorldMatrix;

        // Distance from a world point to the nearest non-weapon part (99 when there is none) and that part's closest point.
        public float BodyDistance(Vector3 point, out Vector3 nearest) => Nearest(point, false, out nearest);

        // Closest point of the weapon parts' surface to a world point. False when the proxy has no weapon part.
        public bool WeaponPoint(Vector3 from, out Vector3 point) => WeaponPoint(from, out point, out _, out _);

        // Same, plus the part and the part-local point, so a caller can follow that exact spot of the armour (PartPoint).
        public bool WeaponPoint(Vector3 from, out Vector3 point, out int part, out Vector3 local)
        {
            point = from; part = -1; local = Vector3.zero; float best = float.MaxValue;
            if (parts != null)
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!parts[i].bone || !parts[i].weapon || parts[i].points == null || parts[i].points.Length == 0) continue;
                    var m = ToWorld(i);
                    foreach (var p in parts[i].points) { var w = m.MultiplyPoint3x4(p); float d = (w - from).sqrMagnitude; if (d < best) { best = d; point = w; part = i; local = p; } }
                }
            if (part >= 0) return true;
            return Nearest(from, true, out point) < 98;
        }

        // A part-local point in the world, in the last rendered pose (the point itself when the part is unknown).
        public Vector3 PartPoint(int part, Vector3 local, Vector3 fallback) =>
            parts != null && part >= 0 && part < parts.Length && parts[part].bone ? ToWorld(part).MultiplyPoint3x4(local) : fallback;

        float Nearest(Vector3 point, bool weapon, out Vector3 nearest)
        {
            float best = 99; nearest = point;
            if (parts == null) return best;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (!part.bone || part.weapon != weapon) continue;
                var m = ToWorld(i);
                var local = m.inverse.MultiplyPoint3x4(point) - part.center;
                var clamped = new Vector3(Mathf.Clamp(local.x, -part.halfSize.x, part.halfSize.x), Mathf.Clamp(local.y, -part.halfSize.y, part.halfSize.y), Mathf.Clamp(local.z, -part.halfSize.z, part.halfSize.z));
                var world = m.MultiplyPoint3x4(clamped + part.center);
                float d = Vector3.Distance(point, world);
                if (d < best) { best = d; nearest = world; }
            }
            return best;
        }
    }
}
