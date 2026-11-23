using System;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.Audio;

namespace IronRequiem
{
    /// Capas sonoras de la cinemática de despertar de Room00_Hangar (18/11/2026; hoja de cues en
    /// Docs/IronRequiem/Map01/AUDIO_CINEMATICA_INICIO.md). Solo OBSERVA HangarIntroCinematic por sus propiedades públicas
    /// (IsPlaying, WasSkipped, Finished, Time01 x Duration): no modifica su línea de tiempo, su pista de audio ni sus objetos.
    /// - Ambientes en bucle (camas): curva de volumen/tono por segundo de cinemática, ducking en los impactos y, en juego,
    ///   un nivel más bajo que baja aún más en combate (CombatMusic.InCombat) y se apaga al salir del hangar.
    /// - Cues: tabla ordenada por tiempo, disparada con antelación de muestra (PlayScheduled) desde un pool de AudioSource
    ///   2D y 3D. Las pisadas de la cinemática (FootContactFx.Contact) añaden una capa de peso.
    /// - Salto: fundido rápido de los cues y paso de las camas al nivel de juego. Fin: los cues suenan hasta su cola y se
    ///   funden; suena «visor en línea». Reinicio tras morir (la cinemática va directa al final): solo el ambiente de juego.
    /// Sin asignaciones por fotograma (sin LINQ, cierres ni cadenas en Update).
    public sealed class HangarIntroSoundscape : MonoBehaviour
    {
        public enum Bus { Ambient, Cue, Impact }
        public enum Layer { Ambient, Robot, Capsule, Impact, Reveal, Transition }

        [Serializable]
        public sealed class Cue
        {
            public string id = "cue";
            public Layer layer = Layer.Robot;
            [Min(0)] public float time;
            public AudioClip clip;
            public Bus bus = Bus.Cue;
            [Range(0, 1)] public float volume = 1f;
            [Range(.25f, 3f)] public float pitch = 1f;
            [Range(0, .3f)] public float pitchJitter;
            [Tooltip("Vacío = 2D. Con emisor, la voz 3D se coloca en su posición al disparar.")]
            public Transform emitter;
            [Range(0, 1)] public float spatialBlend = 1f;
            [Min(.1f)] public float minDistance = 3f;
            [Min(1f)] public float maxDistance = 40f;
            [Header("Ducking de las camas (0 dB = sin ducking)")]
            [Range(-30, 0)] public float duckDb;
            [Min(0)] public float duckAttack = .02f, duckHold = .2f, duckRelease = 1f;
            public bool duckMusic;
        }

        [Serializable]
        public sealed class Bed
        {
            public string id = "bed";
            public AudioSource source;
            [Tooltip("x = segundos de cinemática, y = volumen lineal 0-1.")]
            public AnimationCurve cinematicGain = AnimationCurve.Constant(0, 30, .5f);
            [Tooltip("Opcional: tono por segundo de cinemática (vacía = 1).")]
            public AnimationCurve cinematicPitch = new AnimationCurve();
            [Range(0, 1)] public float gameplayVolume = .3f;
            [Range(0, 1)] public float combatVolume = .1f;
            public bool duckable = true;
        }

        [Serializable]
        public sealed class DuckWindow
        {
            public string id = "duck";
            [Min(0)] public float start;
            [Min(0)] public float attack = .3f, hold = .1f, release = .8f;
            [Range(-40, 0)] public float depthDb = -6f;
            public bool music;
        }

        [SerializeField] HangarIntroCinematic cinematic;
        [Header("Mezcla (MainAudioMixer)")]
        [SerializeField] AudioMixerGroup ambientGroup;
        [SerializeField] AudioMixerGroup cueGroup;
        [SerializeField] AudioMixerGroup impactGroup;
        [Header("Contenido")]
        [SerializeField] Bed[] beds = new Bed[0];
        [SerializeField] Cue[] cues = new Cue[0];
        [SerializeField] DuckWindow[] ducks = new DuckWindow[0];
        [Header("Pisadas de la cinemática (FootContactFx)")]
        [SerializeField] AudioClip stepClip;
        [SerializeField, Range(0, 1)] float stepVolume = .7f;
        [SerializeField] Vector2 stepPitch = new Vector2(.82f, .98f);
        [Header("Salto y final")]
        [SerializeField] AudioClip handoffClip;
        [SerializeField, Range(0, 1)] float handoffVolume = .5f, skipHandoffVolume = .32f;
        [SerializeField, Min(.05f)] float skipFadeSeconds = .3f, skipBedSeconds = 1f;
        [SerializeField, Min(.05f)] float endCueFadeSeconds = 1.5f, endBedSeconds = 2.5f, restartBedSeconds = 1.5f;
        [Header("Juego")]
        [SerializeField, Min(.05f)] float combatFadeSeconds = 1.5f;
        [SerializeField] bool useHangarZone = true;
        [SerializeField] Bounds hangarZone = new Bounds(new Vector3(0, 2.5f, -106), new Vector3(34, 10, 56));
        [SerializeField, Min(.5f)] float zoneFadeMeters = 8f;
        [Header("Música de campaña (CombatMusic) durante la cinemática")]
        [Tooltip("Baja la música de exploración mientras dura la cinemática para dejar sitio a los silencios y golpes; vuelve sola al terminar.")]
        [SerializeField] bool duckMusicDuringCinematic = true;
        [SerializeField, Range(-30, 0)] float cinematicMusicDb = -9f;
        [SerializeField, Min(.1f)] float musicReleaseSeconds = 3f;
        [Header("Pool de voces")]
        [SerializeField, Range(2, 16)] int voices2D = 8;
        [SerializeField, Range(2, 16)] int voices3D = 8;
        [SerializeField, Range(0, .2f)] float scheduleAhead = .05f;

        enum Mode { Waiting, Cinematic, Transition, Gameplay }
        Mode mode;

        AudioSource[] voices = new AudioSource[0];
        float[] voiceBase, voiceFade, voiceFadeRate;
        double[] voiceStart, voiceBusyUntil;
        int pool2D;

        float[] bedFrom, bedLevel;
        float blend, blendSeconds = 1f;
        int nextCue;

        CombatMusic music;
        AudioSource[] musicSources = new AudioSource[0];
        float[] musicBase, musicWritten;
        bool musicTouched;
        float musicGain = 1f;

        Transform listener;
        bool listenerSearched;

        public bool InCinematic => mode == Mode.Cinematic;
        public bool InGameplay => mode == Mode.Gameplay;
        public int CuesFired => nextCue;
        public int CueCount => cues.Length;
        public float MusicGain => musicGain;

        public void Configure(HangarIntroCinematic owner, AudioMixerGroup ambient, AudioMixerGroup cue, AudioMixerGroup impact,
            Bed[] bedList, Cue[] cueList, DuckWindow[] duckList, AudioClip step, AudioClip handoff, Bounds zone)
        {
            cinematic = owner; ambientGroup = ambient; cueGroup = cue; impactGroup = impact;
            beds = bedList ?? new Bed[0]; cues = cueList ?? new Cue[0]; ducks = duckList ?? new DuckWindow[0];
            stepClip = step; handoffClip = handoff; hangarZone = zone;
        }

        void Awake()
        {
            SortCues();
            pool2D = voices2D;
            int n = voices2D + voices3D;
            voices = new AudioSource[n];
            voiceBase = new float[n]; voiceFade = new float[n]; voiceFadeRate = new float[n];
            voiceStart = new double[n]; voiceBusyUntil = new double[n];
            for (int i = 0; i < n; i++)
            {
                bool is3D = i >= pool2D;
                var go = new GameObject(is3D ? "Voice3D_" + (i - pool2D) : "Voice2D_" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false; s.loop = false; s.dopplerLevel = 0; s.spatialBlend = 0;
                s.rolloffMode = AudioRolloffMode.Logarithmic; s.outputAudioMixerGroup = cueGroup;
                voices[i] = s;
            }
            bedFrom = new float[beds.Length]; bedLevel = new float[beds.Length];
            for (int i = 0; i < beds.Length; i++)
            {
                var s = beds[i] != null ? beds[i].source : null; if (!s) continue;
                s.playOnAwake = false; s.loop = true; s.volume = 0; s.dopplerLevel = 0; s.Stop();
            }
            if (!cinematic) cinematic = FindAnyObjectByType<HangarIntroCinematic>();
            mode = Mode.Waiting;
        }

        void Start()
        {
            music = FindAnyObjectByType<CombatMusic>();
            if (music) musicSources = music.GetComponents<AudioSource>();
            musicBase = new float[musicSources.Length]; musicWritten = new float[musicSources.Length];
            for (int i = 0; i < musicWritten.Length; i++) musicWritten[i] = -1;
            FindListener();
        }

        void OnEnable() { FootContactFx.Contact += OnFootContact; }

        void OnDisable()
        {
            FootContactFx.Contact -= OnFootContact;
            RestoreMusic();
        }

        // ------------------------------------------------------------------------------------------------ estado
        void Update()
        {
            if (Time.timeScale <= 0f) return; // pausa: AudioListener.pause congela las fuentes y la cinemática no avanza
            float dt = Time.unscaledDeltaTime;
            switch (mode)
            {
                case Mode.Waiting:
                    if (!cinematic) { BeginTransition(restartBedSeconds); break; }
                    if (cinematic.IsPlaying && !cinematic.WasSkipped) EnterCinematic();
                    else if (cinematic.WasSkipped || cinematic.Finished) BeginTransition(restartBedSeconds); // reinicio tras morir: directo al final
                    break;
                case Mode.Cinematic:
                    if (!cinematic || cinematic.WasSkipped) { BeginSkip(); break; }
                    if (!cinematic.IsPlaying) { BeginEnding(); break; }
                    float t = CinematicTime();
                    FireCues(t);
                    ApplyBedsCinematic(t);
                    float musicDb = Mathf.Min(duckMusicDuringCinematic ? cinematicMusicDb : 0f, DuckDb(t, true));
                    musicGain = Mathf.MoveTowards(musicGain, Db(musicDb), dt * 2f);
                    break;
                case Mode.Transition:
                    blend = Mathf.MoveTowards(blend, 1f, dt / blendSeconds);
                    ApplyBedsTransition();
                    if (blend >= 1f) mode = Mode.Gameplay;
                    break;
                case Mode.Gameplay:
                    ApplyBedsGameplay(dt);
                    break;
            }
            if (mode != Mode.Cinematic) musicGain = Mathf.MoveTowards(musicGain, 1f, dt / musicReleaseSeconds);
            UpdateVoices(dt);
        }

        // CombatMusic escribe el volumen absoluto de sus fuentes en su Update; aquí (después) solo se multiplica.
        void LateUpdate()
        {
            if (musicSources.Length == 0) return;
            if (musicGain >= .9999f && !musicTouched) return;
            for (int i = 0; i < musicSources.Length; i++)
            {
                var s = musicSources[i]; if (!s) continue;
                float v = s.volume;
                float baseV = musicWritten[i] >= 0 && Mathf.Abs(v - musicWritten[i]) < 1e-6f ? musicBase[i] : v; // sin acumular si nadie la reescribió
                musicBase[i] = baseV;
                float w = baseV * musicGain;
                s.volume = w; musicWritten[i] = s.volume;
            }
            musicTouched = musicGain < .9999f;
            if (!musicTouched) for (int i = 0; i < musicWritten.Length; i++) musicWritten[i] = -1;
        }

        void RestoreMusic()
        {
            if (musicWritten == null) return;
            for (int i = 0; i < musicSources.Length; i++)
            {
                var s = musicSources[i];
                if (s && musicWritten[i] >= 0 && Mathf.Abs(s.volume - musicWritten[i]) < 1e-6f) s.volume = musicBase[i];
                musicWritten[i] = -1;
            }
            musicTouched = false;
        }

        float CinematicTime() => cinematic ? (float)(cinematic.Time01 * cinematic.Duration) : 0f;

        void EnterCinematic()
        {
            mode = Mode.Cinematic;
            nextCue = 0;
            for (int i = 0; i < beds.Length; i++) StartBed(i, 0f);
        }

        void BeginEnding()
        {
            FadeVoices(endCueFadeSeconds, false);
            if (handoffClip) PlayVoice(Acquire(false), handoffClip, cueGroup, handoffVolume, 1f, false, default, 0, 1, 1, 0, 0);
            BeginTransition(endBedSeconds);
        }

        void BeginSkip()
        {
            FadeVoices(skipFadeSeconds, true);
            // el salto funde a negro en .25 s y luego vuelve la imagen en primera persona: el «visor en línea» cae ahí
            if (handoffClip) PlayVoice(Acquire(false), handoffClip, cueGroup, skipHandoffVolume, 1f, false, default, 0, 1, 1, .25, 0);
            BeginTransition(skipBedSeconds);
        }

        void BeginTransition(float seconds)
        {
            for (int i = 0; i < beds.Length; i++)
            {
                var s = BedSource(i);
                bedFrom[i] = s ? s.volume : 0f;
                if (GameplayTarget(i) > 0f) StartBed(i, bedFrom[i]);
            }
            blend = 0f; blendSeconds = Mathf.Max(.05f, seconds);
            mode = Mode.Transition;
            FindListener();
        }

        // ------------------------------------------------------------------------------------------------ camas
        AudioSource BedSource(int i) => beds[i] != null ? beds[i].source : null;

        void StartBed(int i, float volume)
        {
            var s = BedSource(i); if (!s || !s.clip) return;
            s.volume = volume;
            if (!s.isPlaying)
            {
                // arranque en un punto distinto del bucle para que las camas no suenen siempre igual
                if (s.clip.loadType != AudioClipLoadType.Streaming && s.clip.samples > 1) s.timeSamples = UnityEngine.Random.Range(0, s.clip.samples - 1);
                s.Play();
            }
        }

        void ApplyBedsCinematic(float t)
        {
            float duck = Db(DuckDb(t, false));
            for (int i = 0; i < beds.Length; i++)
            {
                var b = beds[i]; var s = b != null ? b.source : null; if (!s) continue;
                float g = b.cinematicGain != null && b.cinematicGain.length > 0 ? Mathf.Clamp01(b.cinematicGain.Evaluate(t)) : 0f;
                if (b.duckable) g *= duck;
                s.volume = g;
                s.pitch = b.cinematicPitch != null && b.cinematicPitch.length > 0 ? Mathf.Clamp(b.cinematicPitch.Evaluate(t), .5f, 2f) : 1f;
            }
        }

        void ApplyBedsTransition()
        {
            float k = Mathf.SmoothStep(0f, 1f, blend);
            for (int i = 0; i < beds.Length; i++)
            {
                var s = BedSource(i); if (!s) continue;
                float v = Mathf.Lerp(bedFrom[i], GameplayTarget(i), k);
                s.volume = v; bedLevel[i] = v;
                s.pitch = Mathf.Lerp(s.pitch, 1f, k);
                if (k >= 1f && v <= .0001f && s.isPlaying) s.Stop();
            }
        }

        void ApplyBedsGameplay(float dt)
        {
            for (int i = 0; i < beds.Length; i++)
            {
                var s = BedSource(i); if (!s) continue;
                float target = GameplayTarget(i);
                bedLevel[i] = Mathf.MoveTowards(bedLevel[i], target, dt / combatFadeSeconds);
                if (bedLevel[i] <= .0001f && target <= 0f) { if (s.isPlaying) s.Stop(); continue; }
                if (!s.isPlaying) StartBed(i, 0f);
                s.volume = bedLevel[i]; s.pitch = 1f;
            }
        }

        float GameplayTarget(int i)
        {
            var b = beds[i]; if (b == null) return 0f;
            float v = music && music.InCombat ? b.combatVolume : b.gameplayVolume;
            return v * ZoneGain();
        }

        float ZoneGain()
        {
            if (!useHangarZone || !listener) return 1f;
            float d = Mathf.Sqrt(hangarZone.SqrDistance(listener.position));
            return 1f - Mathf.SmoothStep(0f, 1f, d / zoneFadeMeters);
        }

        void FindListener()
        {
            if (listener || listenerSearched && mode != Mode.Transition) return;
            listenerSearched = true;
            var player = FindAnyObjectByType<PlayerCharacterController>();
            listener = player && player.PlayerCamera ? player.PlayerCamera.transform : null;
        }

        // ------------------------------------------------------------------------------------------------ ducking
        static float Db(float db) => db <= -79f ? 0f : Mathf.Pow(10f, db / 20f);

        static float Window(float t, float start, float attack, float hold, float release, float depth)
        {
            float u = t - start;
            if (u < 0f || u > attack + hold + release) return 0f;
            if (u < attack) return depth * Mathf.SmoothStep(0f, 1f, attack > 0f ? u / attack : 1f);
            if (u < attack + hold) return depth;
            return depth * (1f - Mathf.SmoothStep(0f, 1f, release > 0f ? (u - attack - hold) / release : 1f));
        }

        /// dB de ducking en el instante t: la ventana más profunda manda (no se suman, para no hundir de más).
        float DuckDb(float t, bool forMusic)
        {
            float d = 0f;
            for (int i = 0; i < ducks.Length; i++)
            {
                var w = ducks[i]; if (w == null || forMusic && !w.music) continue;
                d = Mathf.Min(d, Window(t, w.start, w.attack, w.hold, w.release, w.depthDb));
            }
            for (int i = 0; i < cues.Length; i++)
            {
                var c = cues[i]; if (c == null || c.duckDb >= 0f || forMusic && !c.duckMusic) continue;
                d = Mathf.Min(d, Window(t, c.time, c.duckAttack, c.duckHold, c.duckRelease, c.duckDb));
            }
            return d;
        }

        // ------------------------------------------------------------------------------------------------ cues y voces
        void SortCues()
        {
            for (int i = 1; i < cues.Length; i++) // inserción: estable y sin asignaciones
            {
                var c = cues[i]; int j = i - 1;
                while (j >= 0 && CueTime(cues[j]) > CueTime(c)) { cues[j + 1] = cues[j]; j--; }
                cues[j + 1] = c;
            }
        }

        static float CueTime(Cue c) => c != null ? c.time : float.MaxValue;

        AudioMixerGroup Group(Bus bus) => bus == Bus.Ambient ? ambientGroup : bus == Bus.Impact ? (impactGroup ? impactGroup : cueGroup) : cueGroup;

        void FireCues(float t)
        {
            while (nextCue < cues.Length)
            {
                var c = cues[nextCue];
                if (c == null) { nextCue++; continue; }
                float lead = c.time - t;
                if (lead > scheduleAhead) break;
                nextCue++;
                if (!c.clip || c.volume <= 0f) continue;
                float pitch = (c.pitch > 0f ? c.pitch : 1f) * (1f + UnityEngine.Random.Range(-c.pitchJitter, c.pitchJitter));
                float offset = lead < -.02f ? -lead * pitch : 0f;   // llegó tarde (fotograma largo): empieza dentro del clip, alineado
                if (offset >= c.clip.length - .02f) continue;       // ya habría terminado
                bool is3D = c.emitter && c.spatialBlend > 0f;
                PlayVoice(Acquire(is3D), c.clip, Group(c.bus), c.volume, pitch, is3D, is3D ? c.emitter.position : default,
                    c.spatialBlend, c.minDistance, c.maxDistance, lead > 0f ? lead : 0.0, offset);
            }
        }

        void OnFootContact(FootContactFx source, bool right, Vector3 point, float time, float soleHeight)
        {
            if (mode != Mode.Cinematic || !stepClip) return;
            PlayVoice(Acquire(true), stepClip, impactGroup ? impactGroup : cueGroup, stepVolume,
                UnityEngine.Random.Range(stepPitch.x, stepPitch.y), true, point, .85f, 3f, 35f, 0, 0);
        }

        int Acquire(bool is3D)
        {
            double now = AudioSettings.dspTime;
            int from = is3D ? pool2D : 0, to = is3D ? voices.Length : pool2D, oldest = from;
            for (int i = from; i < to; i++)
            {
                if (voiceBusyUntil[i] <= now && !voices[i].isPlaying) return i;
                if (voiceStart[i] < voiceStart[oldest]) oldest = i;
            }
            return oldest; // se roba la voz más antigua
        }

        void PlayVoice(int i, AudioClip clip, AudioMixerGroup group, float volume, float pitch, bool is3D, Vector3 position,
            float spatial, float minDistance, float maxDistance, double delay, float offset)
        {
            if (i < 0 || i >= voices.Length || !clip) return;
            var s = voices[i];
            s.Stop();
            s.clip = clip; s.outputAudioMixerGroup = group; s.volume = volume; s.pitch = pitch;
            if (is3D)
            {
                s.transform.position = position;
                s.spatialBlend = spatial; s.minDistance = minDistance; s.maxDistance = Mathf.Max(minDistance + .1f, maxDistance);
            }
            else s.spatialBlend = 0f;
            voiceBase[i] = volume; voiceFade[i] = 1f; voiceFadeRate[i] = 0f;
            double now = AudioSettings.dspTime;
            if (offset > 0f)
            {
                s.timeSamples = Mathf.Clamp((int)(offset * clip.frequency), 0, clip.samples - 1);
                s.Play();
            }
            else
            {
                s.timeSamples = 0;
                if (delay > .002) s.PlayScheduled(now + delay); else s.Play();
            }
            voiceStart[i] = now + delay;
            voiceBusyUntil[i] = now + delay + Mathf.Max(0f, clip.length - offset) / Mathf.Max(.05f, pitch) + .05;
        }

        void FadeVoices(float seconds, bool cancelPending)
        {
            double now = AudioSettings.dspTime;
            for (int i = 0; i < voices.Length; i++)
            {
                if (voiceBusyUntil[i] <= now && !voices[i].isPlaying) continue;
                if (cancelPending && voiceStart[i] > now) { voices[i].Stop(); voiceBusyUntil[i] = 0; continue; } // aún no había empezado
                voiceFadeRate[i] = 1f / Mathf.Max(.02f, seconds);
            }
        }

        void UpdateVoices(float dt)
        {
            for (int i = 0; i < voices.Length; i++)
            {
                if (voiceFadeRate[i] <= 0f) continue;
                voiceFade[i] = Mathf.MoveTowards(voiceFade[i], 0f, voiceFadeRate[i] * dt);
                voices[i].volume = voiceBase[i] * voiceFade[i];
                if (voiceFade[i] <= 0f) { voices[i].Stop(); voiceFadeRate[i] = 0f; voiceBusyUntil[i] = 0; }
            }
        }
    }
}
