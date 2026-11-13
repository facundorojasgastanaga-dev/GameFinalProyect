using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    /// Heavy enemy death (Big Bot / Saw Bot): short hit-stop, distance shake and a camera kick. Presentation only.
    [RequireComponent(typeof(Health))]
    public sealed class HeavyDeathFeel : MonoBehaviour
    {
        [SerializeField, Min(0)] float hitStop = .08f;
        [SerializeField, Min(0)] float shakeRadius = 22f;
        [SerializeField, Range(0, 1)] float shakeStrength = .9f;
        [SerializeField, Min(.01f)] float shakeSeconds = .45f;
        [SerializeField] Vector3 kick = new Vector3(-2.2f, 0, 1.2f);
        Health health;

        public void Configure(float stop, float radius, float strength, float seconds, Vector3 cameraKick)
        { hitStop = stop; shakeRadius = radius; shakeStrength = strength; shakeSeconds = seconds; kick = cameraKick; }

        void Awake() => health = GetComponent<Health>();
        void OnEnable() { if (health) health.OnDie += Died; }
        void OnDisable() { if (health) health.OnDie -= Died; }

        void Died()
        {
            var cam = Camera.main;
            float near = cam ? Mathf.Clamp01(1 - Vector3.Distance(cam.transform.position, transform.position) / shakeRadius) : 0;
            if (near <= 0) return;
            GameFeel.HitStop(hitStop * Mathf.Lerp(.5f, 1, near));
            DistanceCameraImpulse.Emit(transform.position, shakeRadius, shakeStrength, shakeSeconds);
            DistanceCameraImpulse.Kick(cam, kick * near, new Vector3(0, -.03f * near, 0));
        }
    }
}
