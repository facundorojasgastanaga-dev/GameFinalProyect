using Unity.FPS.AI;
using UnityEngine;
using UnityEngine.Audio;

namespace IronRequiem
{
    /// <summary>
    /// Adaptive level music: two sample-aligned loops play in sync and crossfade between
    /// exploration and combat depending on whether any enemy currently knows about the player.
    /// </summary>
    public class CombatMusic : MonoBehaviour
    {
        [SerializeField] AudioClip m_ExploreClip;
        [SerializeField] AudioClip m_CombatClip;
        [SerializeField] AudioMixerGroup m_OutputGroup;
        [SerializeField, Range(0f, 1f)] float m_Volume = 0.8f;
        [SerializeField, Min(0.01f)] float m_FadeToCombatSeconds = 0.75f;
        [SerializeField, Min(0.01f)] float m_FadeToExploreSeconds = 3f;
        [Tooltip("Seconds without any alerted enemy before returning to exploration")]
        [SerializeField, Min(0f)] float m_CombatHoldSeconds = 6f;
        [SerializeField, Min(0.05f)] float m_PollInterval = 0.25f;
        [Tooltip("Keep playing while menus pause the game through AudioListener.pause")]
        [SerializeField] bool m_PlayWhilePaused = true;
        [SerializeField] CombatLabMenu m_LabMenu;
        float m_LabGain = 1;
        public void ConfigureLab(CombatLabMenu menu) => m_LabMenu = menu;

        AudioSource m_Explore;
        AudioSource m_Combat;
        EnemyManager m_EnemyManager;
        float m_Mix;            // 0 = exploration, 1 = combat
        float m_StartFade;      // master fade-in on scene start
        float m_LastAlertTime = Mathf.NegativeInfinity;
        float m_NextPollTime;

        public bool InCombat { get; private set; }
        public AudioClip ExploreClip => m_ExploreClip;
        public AudioClip CombatClip => m_CombatClip;

        void Awake()
        {
            m_LabGain = m_LabMenu ? 0 : 1;
            m_Explore = CreateSource(m_ExploreClip);
            m_Combat = CreateSource(m_CombatClip);
        }

        AudioSource CreateSource(AudioClip clip)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = m_PlayWhilePaused;
            source.volume = 0f;
            source.outputAudioMixerGroup = m_OutputGroup;
            return source;
        }

        void Start()
        {
            if (m_ExploreClip == null || m_CombatClip == null)
            {
                Debug.LogWarning("CombatMusic needs both exploration and combat clips.", this);
                enabled = false;
                return;
            }

            m_EnemyManager = FindAnyObjectByType<EnemyManager>();

            // Scheduling both on the same DSP time keeps the layers sample-aligned
            double startTime = AudioSettings.dspTime + 0.1;
            m_Explore.PlayScheduled(startTime);
            m_Combat.PlayScheduled(startTime);
        }

        void Update()
        {
            if (Time.unscaledTime >= m_NextPollTime)
            {
                m_NextPollTime = Time.unscaledTime + m_PollInterval;
                if (AnyEnemyAlerted())
                    m_LastAlertTime = Time.unscaledTime;
                InCombat = Time.unscaledTime - m_LastAlertTime <= m_CombatHoldSeconds;
            }

            float fade = InCombat ? m_FadeToCombatSeconds : m_FadeToExploreSeconds;
            m_Mix = Mathf.MoveTowards(m_Mix, InCombat ? 1f : 0f, Time.unscaledDeltaTime / fade);
            m_StartFade = Mathf.MoveTowards(m_StartFade, 1f, Time.unscaledDeltaTime / 2f);

            // Equal-power crossfade avoids a volume dip halfway through the transition
            m_LabGain = Mathf.MoveTowards(m_LabGain, !m_LabMenu || m_LabMenu.IsPlaying ? 1 : 0, Time.unscaledDeltaTime / .45f);
            float level = m_Volume * m_StartFade * m_LabGain;
            m_Explore.volume = level * Mathf.Cos(m_Mix * Mathf.PI * 0.5f);
            m_Combat.volume = level * Mathf.Sin(m_Mix * Mathf.PI * 0.5f);
        }

        bool AnyEnemyAlerted()
        {
            if (m_EnemyManager == null || m_EnemyManager.Enemies == null)
                return false;

            foreach (var enemy in m_EnemyManager.Enemies)
                if (enemy != null && enemy.isActiveAndEnabled && enemy.DetectionModule != null && enemy.HadKnownTarget)
                    return true;
            return false;
        }
    }
}
