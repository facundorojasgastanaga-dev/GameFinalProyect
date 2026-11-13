using UnityEngine;
using UnityEngine.Audio;

namespace IronRequiem
{
    /// <summary>
    /// Plays a looping 2D music clip with a short fade-in. The scene's lifetime bounds playback.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MenuMusic : MonoBehaviour
    {
        [SerializeField] AudioClip m_Clip;
        [SerializeField, Range(0f, 1f)] float m_Volume = 0.8f;
        [SerializeField, Min(0f)] float m_FadeInSeconds = 2f;
        [SerializeField] AudioMixerGroup m_OutputGroup;
        [SerializeField] CombatLabMenu m_LabMenu;
        [SerializeField, Min(.01f)] float m_FadeOutSeconds = .35f;

        AudioSource m_Source;
        float m_FadeElapsed;

        public AudioClip Clip => m_Clip;
        public void ConfigureLab(CombatLabMenu menu) => m_LabMenu = menu;

        void Awake()
        {
            m_Source = GetComponent<AudioSource>();
            m_Source.clip = m_Clip;
            m_Source.loop = true;
            m_Source.playOnAwake = false;
            m_Source.spatialBlend = 0f;
            m_Source.ignoreListenerPause = true;
            m_Source.outputAudioMixerGroup = m_OutputGroup;
        }

        void Start()
        {
            if (m_Clip == null)
            {
                Debug.LogWarning("MenuMusic has no clip assigned.", this);
                enabled = false;
                return;
            }

            m_Source.volume = m_FadeInSeconds > 0f ? 0f : m_Volume;
            m_Source.Play();
        }

        void Update()
        {
            if (m_LabMenu)
            {
                bool show = m_LabMenu.IsMainMenu;
                if (show && !m_Source.isPlaying) m_Source.Play();
                float seconds = show ? Mathf.Max(.01f, m_FadeInSeconds) : m_FadeOutSeconds;
                m_Source.volume = Mathf.MoveTowards(m_Source.volume, show ? m_Volume : 0, Time.unscaledDeltaTime * m_Volume / seconds);
                if (!show && m_Source.isPlaying && m_Source.volume <= .0001f) m_Source.Stop();
                return;
            }
            if (m_FadeElapsed >= m_FadeInSeconds)
                return;

            // Unscaled so a paused timeScale in menus never freezes the fade
            m_FadeElapsed += Time.unscaledDeltaTime;
            m_Source.volume = m_Volume * Mathf.Clamp01(m_FadeElapsed / m_FadeInSeconds);
        }
    }
}
