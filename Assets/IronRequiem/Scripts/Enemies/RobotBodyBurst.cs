using System;
using UnityEngine;

namespace IronRequiem
{
    // Visual only. Never calls Health, DamageArea or the independent CannonExplosion.
    [RequireComponent(typeof(AudioSource))]
    public sealed class RobotBodyBurst : MonoBehaviour
    {
        [SerializeField] ParticleSystem overload;
        [SerializeField] ParticleSystem[] layers;
        [SerializeField, Min(.05f)] float overloadDuration = .28f;
        [SerializeField, Min(.5f)] float duration = 3.1f;
        AudioSource audioSource;
        Transform follow;
        Action detonated;
        float age;
        bool fired, ownsVoice;
        static int voices;
        public bool InUse { get; private set; }
        public bool HasBurst => fired;
        public static int AudioVoices => voices;
        public ParticleSystem[] Layers => layers;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetBudget() => voices = 0;
        public void Configure(ParticleSystem charge, ParticleSystem[] effects) { overload = charge; layers = effects; }
        void Awake() => audioSource = GetComponent<AudioSource>();
        public void Begin(Transform source, Vector3 position, Action onBurst, float scale = 1f)
        {
            follow = source; detonated = onBurst; age = 0; fired = false; InUse = true;
            transform.SetPositionAndRotation(position, Quaternion.identity); transform.localScale = Vector3.one * Mathf.Clamp(scale,.25f,3f); gameObject.SetActive(true);
            if (overload) { overload.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); overload.Play(); }
        }
        void Update()
        {
            age += Time.deltaTime;
            if (!fired && follow) transform.position = follow.position;
            if (!fired && age >= overloadDuration)
            {
                fired = true; follow = null;
                if (overload) overload.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach (var ps in layers) { ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Play(false); }
                var camera = Camera.main;
                if (voices < 4 && audioSource.clip && camera && Vector3.Distance(camera.transform.position, transform.position) < 24)
                { voices++; ownsVoice = true; audioSource.Play(); }
                var callback = detonated; detonated = null; callback?.Invoke();
            }
            if (ownsVoice && !audioSource.isPlaying) ReleaseVoice();
            if (age >= duration) Return();
        }
        void ReleaseVoice() { if (!ownsVoice) return; voices = Mathf.Max(0, voices - 1); ownsVoice = false; }
        public void Return()
        {
            if (overload) overload.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var ps in layers) if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            audioSource.Stop(); ReleaseVoice(); follow = null; detonated = null; InUse = false; fired = false; age = 0; transform.localScale = Vector3.one; gameObject.SetActive(false);
        }
        void OnDisable() { if (audioSource) audioSource.Stop(); ReleaseVoice(); }
    }
}
