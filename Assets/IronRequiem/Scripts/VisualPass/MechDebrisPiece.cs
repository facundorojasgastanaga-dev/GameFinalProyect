using UnityEngine;

namespace IronRequiem
{
    // One chunk of an exploded mech. Cheap ballistic motion (no Rigidbody/Collider): raycast bounces with
    // friction, then it rests as wreckage until its sector unloads. Zero allocation per frame.
    public sealed class MechDebrisPiece : MonoBehaviour
    {
        [SerializeField, Min(1)] int maxBounces = 4;
        [SerializeField, Range(0, 1)] float restitution = .32f, friction = .55f;
        [SerializeField, Min(1)] float persistSeconds = 600f;
        Vector3 velocity, spin, baseScale; float age, radius; int bounces; bool flying; LayerMask ground;
        public bool Flying => flying;

        public void Launch(Vector3 initialVelocity, Vector3 angularDegrees, LayerMask groundMask, float seconds)
        { persistSeconds = Mathf.Max(1, seconds); Launch(initialVelocity, angularDegrees, groundMask); }

        public void Launch(Vector3 initialVelocity, Vector3 angularDegrees, LayerMask groundMask)
        {
            velocity = initialVelocity; spin = angularDegrees; ground = groundMask; age = 0; bounces = 0; flying = true;
            baseScale = transform.localScale;
            var mf = GetComponent<MeshFilter>();
            radius = mf && mf.sharedMesh ? mf.sharedMesh.bounds.extents.magnitude * .35f * transform.lossyScale.x : .1f;
            gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.deltaTime; age += dt;
            if (age > persistSeconds)
            {
                transform.localScale = baseScale * Mathf.Clamp01(1 - (age - persistSeconds) / .4f);
                if (age > persistSeconds + .4f) gameObject.SetActive(false);
                return;
            }
            if (!flying) return;
            velocity += Physics.gravity * dt;
            var delta = velocity * dt; float dist = delta.magnitude;
            if (dist > 0 && Physics.SphereCast(transform.position, radius, delta / dist, out var hit, dist, ground, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + hit.normal * (radius + .01f);
                var n = hit.normal; var vn = Vector3.Dot(velocity, n) * n; var vt = velocity - vn;
                velocity = vt * (1 - friction) - vn * restitution;
                spin *= .45f; bounces++;
                if (bounces >= maxBounces || velocity.sqrMagnitude < .6f) { flying = false; return; }
            }
            else transform.position += delta;
            transform.Rotate(spin * dt, Space.World);
        }
    }
}
