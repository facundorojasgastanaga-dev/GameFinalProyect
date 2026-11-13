using UnityEngine;

namespace IronRequiem
{
    /// Reusable menu explosion: prebuilt particle systems (fire, smoke, sparks, metal debris) emitted on demand plus
    /// a short light flash. Optional extra VFX prefab from the project is spawned on top and cleaned up.
    public sealed class MenuExplosionFx : MonoBehaviour
    {
        [SerializeField] ParticleSystem fire, smoke, sparks, debris;
        [SerializeField] Light flash;
        [SerializeField] GameObject extraVfx;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip[] clips = new AudioClip[0];
        [SerializeField] float flashIntensity = 40;
        float flashT = 1;

        public void Configure(ParticleSystem fireFx, ParticleSystem smokeFx, ParticleSystem sparkFx, ParticleSystem debrisFx, Light flashLight, GameObject projectVfx, AudioSource source, AudioClip[] sounds)
        { fire = fireFx; smoke = smokeFx; sparks = sparkFx; debris = debrisFx; flash = flashLight; extraVfx = projectVfx; audioSource = source; clips = sounds ?? new AudioClip[0]; }

        void Awake() { if (flash) flash.intensity = 0; }

        public void Play(Vector3 position, float scale)
        {
            transform.position = position;
            transform.localScale = Vector3.one * scale;
            Emit(fire, 26); Emit(smoke, 14); Emit(sparks, 60); Emit(debris, 16);
            flashT = 0;
            if (extraVfx)
            {
                var fx = Instantiate(extraVfx, position, Quaternion.identity);
                fx.transform.localScale *= scale;
                Destroy(fx, 6);
            }
            if (audioSource && clips.Length > 0)
            {
                audioSource.pitch = Random.Range(.85f, 1.1f);
                audioSource.PlayOneShot(clips[Random.Range(0, clips.Length)], .9f);
            }
        }

        static void Emit(ParticleSystem ps, int count) { if (!ps) return; if (!ps.isPlaying) ps.Play(true); ps.Emit(count); }

        void Update()
        {
            if (!flash || flashT >= 1) return;
            flashT = Mathf.Min(1, flashT + Time.unscaledDeltaTime / .45f);
            float k = (1 - flashT); flash.intensity = flashIntensity * k * k * (0.8f + 0.2f * Mathf.PerlinNoise(Time.unscaledTime * 40, 0));
        }
    }
}
