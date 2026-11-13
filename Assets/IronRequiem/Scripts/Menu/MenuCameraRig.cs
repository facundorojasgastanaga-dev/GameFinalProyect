using UnityEngine;
using UnityEngine.InputSystem;

namespace IronRequiem
{
    /// Hangar menu camera: slow handheld drift, mouse parallax, impulse shake and a scripted dolly for the launch.
    /// Works from the authored pose; unscaled time.
    public sealed class MenuCameraRig : MonoBehaviour
    {
        [SerializeField] float driftMeters = .035f, driftSpeed = .22f;
        [SerializeField] float parallaxMeters = .14f, parallaxDegrees = 1.6f;
        [SerializeField] float shakeDegrees = 1.4f;
        Vector3 basePos; Quaternion baseRot; Vector2 mouse; float shake, dolly; Vector3 dollyTarget; Quaternion dollyRot; float baseFov; Camera cam;
        public void Shake(float amount) => shake = Mathf.Clamp01(Mathf.Max(shake, amount));
        public void Dolly(Vector3 worldTarget, Quaternion worldRotation, float t) { dollyTarget = worldTarget; dollyRot = worldRotation; dolly = Mathf.Clamp01(t); }

        void Awake() { basePos = transform.position; baseRot = transform.rotation; cam = GetComponent<Camera>(); if (cam) baseFov = cam.fieldOfView; }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f), t = Time.unscaledTime;
            Vector2 target = Vector2.zero;
            var m = Mouse.current;
            if (m != null && Screen.width > 0 && Screen.height > 0)
            {
                var p = m.position.ReadValue();
                target = new Vector2(Mathf.Clamp(p.x / Screen.width - .5f, -.5f, .5f), Mathf.Clamp(p.y / Screen.height - .5f, -.5f, .5f)) * 2;
            }
            mouse = Vector2.Lerp(mouse, target, 1 - Mathf.Exp(-dt * 2.2f));
            var drift = new Vector3(Mathf.PerlinNoise(t * driftSpeed, 1.3f) - .5f, Mathf.PerlinNoise(2.7f, t * driftSpeed) - .5f, 0) * 2 * driftMeters;
            var right = baseRot * Vector3.right; var up = baseRot * Vector3.up;
            var pos = basePos + right * (mouse.x * parallaxMeters + drift.x) + up * (mouse.y * parallaxMeters * .5f + drift.y);
            var rot = baseRot * Quaternion.Euler(-mouse.y * parallaxDegrees * .5f, mouse.x * parallaxDegrees, 0);
            if (shake > 0)
            {
                float s = shake * shake * shakeDegrees;
                rot *= Quaternion.Euler((Mathf.PerlinNoise(t * 24, 5) - .5f) * 2 * s, (Mathf.PerlinNoise(9, t * 24) - .5f) * 2 * s, (Mathf.PerlinNoise(t * 20, 17) - .5f) * s);
                shake = Mathf.MoveTowards(shake, 0, dt * 1.6f);
            }
            if (dolly > 0)
            {
                float k = dolly * dolly * (3 - 2 * dolly);
                pos = Vector3.Lerp(pos, dollyTarget, k); rot = Quaternion.Slerp(rot, dollyRot, k);
                if (cam) cam.fieldOfView = Mathf.Lerp(baseFov, baseFov * .82f, k);
            }
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
