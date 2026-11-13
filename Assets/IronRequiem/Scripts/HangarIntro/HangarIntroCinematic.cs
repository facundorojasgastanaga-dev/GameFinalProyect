using System;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Room00_Hangar wake-up cinematic (12/11/2026). A Timeline (PlayableDirector) owns the shot list (Cinemachine
    /// track) and the sound cues (audio track); this component reads the director time every frame and drives what the
    /// timeline cannot: the procedural robot pose, shot camera paths, FX cues, letterbox, skip and the hand-off to the
    /// regular player (PlayerCharacterController stays the only controller; input is blocked via PlayerInputLock).
    /// Plays when the campaign starts (JUGAR or a direct start); a restart after death goes straight to the end state.
    public sealed class HangarIntroCinematic : MonoBehaviour
    {
        [Serializable]
        public struct ShotPath
        {
            public Transform camera, from, to;
            public float start, end;
            public bool endAtHead; // last shot: flies into the robot's head and ends on the first-person view
        }

        [SerializeField] PlayableDirector director;
        [SerializeField] Camera cinematicCamera;
        [SerializeField] AudioListener cinematicListener;
        [SerializeField] ShotPath[] shots = new ShotPath[0];
        [SerializeField] WakeUpRobotPose pose;
        [SerializeField] CapsuleRobotIdle robotPower;
        [SerializeField] CampaignRoom room;
        [SerializeField] Transform standPoint;
        [Header("FX cues")]
        [SerializeField] ParticleSystem podSteam;
        [SerializeField] ParticleSystem cannonSparks, footDust, footSparks;
        [SerializeField] Light cannonLight;
        [SerializeField, Min(0)] float cannonLightIntensity = 1.2f;
        [Tooltip("Cinematic-only key/rim light on the robot while it stands up (off for gameplay).")]
        [SerializeField] Light heroLight;
        [SerializeField, Min(0)] float heroLightIntensity = 4f;
        [Header("Screen")]
        [SerializeField] CanvasGroup fade;
        [SerializeField] RectTransform letterboxTop, letterboxBottom;
        [SerializeField, Range(0, .2f)] float letterboxHeight = .11f;
        [SerializeField] CanvasGroup skipPrompt;
        [SerializeField] Image skipFill;
        [SerializeField, Min(.2f)] float skipHoldSeconds = .9f;
        [SerializeField] string[] hudCanvasNames = { "HUD", "FeedbackFlashCanvas", "ArenaMessages" };
        [SerializeField, Min(.1f)] float fadeInSeconds = .9f, fadeOutSeconds = .45f, handoffFadeSeconds = .7f;

        enum State { Waiting, Playing, Skipping, Handoff, Done }
        State state;
        CampaignFlow flow; PlayerCharacterController player; Camera playerCamera; AudioListener playerListener;
        Canvas[] hud = new Canvas[0];
        float duration, holdTime, skipFadeStart, handoffStart;
        bool woke, steamed, cannonFired, cracked, shattered;
        [Header("Glass break (Room00)")]
        [SerializeField] CapsuleGlassBreak glass;
        [SerializeField, Min(0)] float shakeCrack = .025f, shakeShatter = .07f;

        public bool Finished => state == State.Done;
        public bool IsPlaying => state == State.Playing || state == State.Skipping;
        public bool WasSkipped { get; private set; }
        public float Duration => duration;
        public double Time01 => director && duration > 0 ? director.time / duration : 0;

        [Header("Capsule interior (v30)")]
        [SerializeField] CapsuleRestraints restraints;

        public void ConfigureHeroLight(Light key) => heroLight = key;
        public void ConfigureRestraints(CapsuleRestraints capsuleInside) => restraints = capsuleInside;
        public void ConfigureGlass(CapsuleGlassBreak capsuleGlass) => glass = capsuleGlass;
        public void Configure(PlayableDirector timeline, Camera cam, AudioListener listener, ShotPath[] shotPaths, WakeUpRobotPose robotPose,
            CapsuleRobotIdle power, CampaignRoom owner, Transform stand, ParticleSystem steam, ParticleSystem cannon, ParticleSystem dust,
            ParticleSystem sparks, Light cannonGlow, CanvasGroup fader, RectTransform top, RectTransform bottom, CanvasGroup prompt, Image fill)
        {
            director = timeline; cinematicCamera = cam; cinematicListener = listener; shots = shotPaths; pose = robotPose; robotPower = power;
            room = owner; standPoint = stand; podSteam = steam; cannonSparks = cannon; footDust = dust; footSparks = sparks; cannonLight = cannonGlow;
            fade = fader; letterboxTop = top; letterboxBottom = bottom; skipPrompt = prompt; skipFill = fill;
        }

        void Awake()
        {
            if (fade) { fade.alpha = 1; fade.gameObject.SetActive(true); }
            if (cinematicCamera) cinematicCamera.enabled = false;
            if (cinematicListener) cinematicListener.enabled = false;
            if (skipPrompt) skipPrompt.alpha = 0;
            SetLetterbox(0);
            if (cannonLight) cannonLight.intensity = 0;
            if (heroLight) heroLight.intensity = 0;
            duration = director && director.playableAsset ? (float)director.playableAsset.duration : 12f;
            if (robotPower) { robotPower.PoseDriven = true; robotPower.GlowScale = .25f; }
        }

        void Start()
        {
            flow = FindAnyObjectByType<CampaignFlow>();
            player = flow ? flow.Player : FindAnyObjectByType<PlayerCharacterController>();
            if (player) { playerCamera = player.PlayerCamera; playerListener = playerCamera ? playerCamera.GetComponent<AudioListener>() : null; }
            if (flow && flow.gameObject.scene.IsValid())
            {
                var roots = flow.gameObject.scene.GetRootGameObjects();
                int n = 0; var found = new Canvas[16];
                foreach (var r in roots)
                    foreach (var c in r.GetComponentsInChildren<Canvas>(true))
                        if (c.isRootCanvas && Array.IndexOf(hudCanvasNames, c.name) >= 0 && n < found.Length) found[n++] = c;
                hud = new Canvas[n]; Array.Copy(found, hud, n);
            }
            if (pose) pose.Bind();
            // our own fade is black now: the session's loading cover can go
            if (flow) flow.ReleaseStartCover();
            state = State.Waiting;
        }

        bool SessionPlaying => !flow || !flow.Menu || flow.Menu.IsPlaying;

        void Update()
        {
            switch (state)
            {
                case State.Waiting:
                    if (!SessionPlaying || (flow && room && !room.Ready)) return;
                    if (CombatLabMenu.ResumedAfterRestart || !player) { WasSkipped = true; BeginHandoff(); return; }
                    Begin();
                    break;
                case State.Playing:
                    Tick((float)director.time); PlaceShots((float)director.time);
                    ReadSkip();
                    if (director.time >= duration - 1e-3f || director.state != PlayState.Playing && director.time >= duration - .05f) BeginHandoff();
                    break;
                case State.Skipping:
                    Tick((float)director.time); PlaceShots((float)director.time);
                    if (fade) fade.alpha = Mathf.Max(fade.alpha, Mathf.Clamp01((UnityEngine.Time.unscaledTime - skipFadeStart) / .25f));
                    if (UnityEngine.Time.unscaledTime - skipFadeStart >= .25f) BeginHandoff();
                    break;
                case State.Handoff:
                    if (UnityEngine.Time.timeScale <= 0) return;
                    float k = Mathf.Clamp01((UnityEngine.Time.unscaledTime - handoffStart) / handoffFadeSeconds);
                    if (fade) fade.alpha = 1 - k;
                    if (k >= 1) { if (fade) fade.gameObject.SetActive(false); state = State.Done; }
                    break;
            }
        }

        void Begin()
        {
            state = State.Playing;
            PlayerInputLock.Set(true);
            SetHud(false);
            if (playerCamera) playerCamera.enabled = false;
            if (playerListener) playerListener.enabled = false;
            if (cinematicCamera) cinematicCamera.enabled = true;
            if (cinematicListener) cinematicListener.enabled = true;
            director.time = 0; director.Play();
            Tick(0); PlaceShots(0);
        }

        void Tick(float t)
        {
            if (pose) pose.Evaluate(t);
            if (restraints) restraints.Evaluate(t, state == State.Playing);
            if (fade && state == State.Playing)
                fade.alpha = t < fadeInSeconds ? 1 - Mathf.SmoothStep(0, 1, t / fadeInSeconds) : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(duration - fadeOutSeconds, duration, t));
            SetLetterbox(Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .8f)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(duration - 1.4f, duration - .5f, t))));
            var T = pose ? pose.Times : default;
            if (!woke && t >= .8f) { woke = true; if (robotPower) robotPower.Wake(); }   // v3: a longer dark beat before the visor stutters on
            // armour lines: dim while booting, then sweep to full once standing
            if (robotPower)
            {
                float lines = Mathf.Lerp(.25f, 1f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(T.crouch + .6f, T.stand + .4f, t)));
                // curious visor pulse while it studies its hand
                float pulse = pose ? pose.Curiosity : 0;
                robotPower.GlowScale = lines * (1 + .35f * pulse * (.5f + .5f * Mathf.Sin(t * 5.5f)));
            }
            if (!cracked && t >= T.punch1) { cracked = true; if (glass) glass.Crack(); }
            if (!shattered && t >= T.punch2) { shattered = true; if (glass) glass.Shatter(); }
            bool cannonBeat = T.cannonEnd > T.cannonStart;   // v30: the servo/cannon beat was cut (start == end)
            if (cannonBeat && !cannonFired && t >= T.cannonStart + .6f) { cannonFired = true; if (cannonSparks) { cannonSparks.transform.position = pose.RightForearm ? pose.RightForearm.position : cannonSparks.transform.position; cannonSparks.Emit(28); } }
            if (cannonLight && cannonBeat)
            {
                float w = Mathf.InverseLerp(T.cannonStart + .5f, T.cannonStart + .9f, t) * (1 - Mathf.InverseLerp(T.cannonEnd - .6f, T.cannonEnd, t));
                cannonLight.intensity = cannonLightIntensity * Mathf.Clamp01(w) * (.85f + .15f * Mathf.Sin(t * 37f));
            }
            if (heroLight) heroLight.intensity = heroLightIntensity * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(T.leave, T.leave + 1.5f, t)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(duration - 1.2f, duration - .3f, t)));
            if (!steamed && t >= T.leave - .1f) { steamed = true; if (podSteam) podSteam.Play(true); }
            // v30: the footstep dust, sparks, sound and camera kick are no longer fired at fixed times here; FootContactFx
            // fires them when a sole really touches the floor.
        }

        void PlaceShots(float t)
        {
            for (int i = 0; i < shots.Length; i++)
            {
                var s = shots[i]; if (!s.camera || !s.from) continue;
                float k = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(s.start, s.end, t));
                Vector3 toPos; Quaternion toRot;
                if (s.endAtHead && pose && pose.Head && standPoint)
                {
                    var face = Vector3.ProjectOnPlane(standPoint.forward, Vector3.up).normalized;
                    toPos = pose.Head.position + face * .12f + Vector3.up * .02f; toRot = Quaternion.LookRotation(face, Vector3.up);
                    k = k * k; // accelerate into the head
                }
                else if (s.to) { toPos = s.to.position; toRot = s.to.rotation; }
                else { toPos = s.from.position; toRot = s.from.rotation; }
                s.camera.SetPositionAndRotation(Vector3.Lerp(s.from.position, toPos, k) + Shake(t), Quaternion.Slerp(s.from.rotation, toRot, k));
            }
        }

        float kickAt = -10, kickAmp;
        /// Small camera kick from a real event (a foot touching the floor: FootContactFx). Decays like the glass shake.
        public void Kick(float amplitude) { if (!director) return; kickAt = (float)director.time; kickAmp = amplitude; }

        // short decaying camera shake on each glass impact (deterministic in t) and on each foot contact
        Vector3 Shake(float t)
        {
            float a = Decay(t - kickAt) * kickAmp;
            if (pose && glass) { var T = pose.Times; a += Decay(t - T.punch1) * shakeCrack + Decay(t - T.punch2) * shakeShatter; }
            if (a <= 0) return Vector3.zero;
            return new Vector3(Mathf.Sin(t * 71f), Mathf.Sin(t * 53f + 1.3f), Mathf.Sin(t * 37f + 2.1f)) * a;
        }
        static float Decay(float dt) => dt < 0 || dt > .6f ? 0 : Mathf.Exp(-dt * 7f);

        void ReadSkip()
        {
            if (UnityEngine.Time.timeScale <= 0) return;
            var kb = Keyboard.current; var pad = Gamepad.current;
            bool held = kb != null && (kb.eKey.isPressed || kb.spaceKey.isPressed) || pad != null && pad.buttonSouth.isPressed;
            holdTime = held ? holdTime + UnityEngine.Time.unscaledDeltaTime : Mathf.Max(0, holdTime - UnityEngine.Time.unscaledDeltaTime * 2);
            if (skipPrompt) skipPrompt.alpha = Mathf.MoveTowards(skipPrompt.alpha, held || holdTime > 0 ? 1 : (director.time < 2.5 ? .55f : 0), UnityEngine.Time.unscaledDeltaTime * 4);
            if (skipFill) skipFill.fillAmount = Mathf.Clamp01(holdTime / skipHoldSeconds);
            if (holdTime >= skipHoldSeconds) { WasSkipped = true; state = State.Skipping; skipFadeStart = UnityEngine.Time.unscaledTime; }
        }

        /// Same as holding E / Space for the hold time (used by tests and debug tools).
        public void RequestSkip() { if (state == State.Playing) { WasSkipped = true; state = State.Skipping; skipFadeStart = UnityEngine.Time.unscaledTime; } }

        /// Shared end state for full playback, skip and restart: identical player placement, view, HUD and control.
        void BeginHandoff()
        {
            if (director)
            {
                director.time = duration; director.Evaluate(); director.Stop();
            }
            if (pose) pose.Evaluate(duration);
            if (restraints) restraints.Evaluate(duration, false); // restraints open and cradle forward, seen or skipped
            if (robotPower) { robotPower.GlowScale = 1; robotPower.PowerDown(); }
            if (pose && pose.Model) pose.Model.gameObject.SetActive(false); // the player *is* the robot from here on
            if (cannonLight) cannonLight.intensity = 0;
            if (heroLight) heroLight.enabled = false;
            StopFx(podSteam); StopFx(cannonSparks); StopFx(footDust); StopFx(footSparks);
            if (glass) glass.Broken(); // same end state whether the break was seen or skipped
            if (fade) { fade.gameObject.SetActive(true); fade.alpha = 1; }
            SetLetterbox(0);
            if (skipPrompt) skipPrompt.alpha = 0;
            if (cinematicCamera) cinematicCamera.enabled = false;
            if (cinematicListener) cinematicListener.enabled = false;
            if (playerCamera) playerCamera.enabled = true;
            if (playerListener) playerListener.enabled = true;
            if (player && standPoint)
            {
                player.TeleportToCheckpoint(standPoint.position + Vector3.up * .03f);
                var face = Vector3.ProjectOnPlane(standPoint.forward, Vector3.up);
                player.SetViewAngles(Quaternion.LookRotation(face.sqrMagnitude > 1e-4f ? face : Vector3.forward).eulerAngles.y, 0);
            }
            SetHud(true);
            PlayerInputLock.Set(false);
            if (room) room.Release();
            handoffStart = UnityEngine.Time.unscaledTime;
            state = State.Handoff;
        }

        static void StopFx(ParticleSystem p) { if (p) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }

        void SetHud(bool visible) { foreach (var c in hud) if (c) c.enabled = visible; }

        void SetLetterbox(float k)
        {
            float h = letterboxHeight * k;
            if (letterboxTop) { letterboxTop.anchorMin = new Vector2(0, 1 - h); letterboxTop.anchorMax = Vector2.one; letterboxTop.offsetMin = letterboxTop.offsetMax = Vector2.zero; }
            if (letterboxBottom) { letterboxBottom.anchorMin = Vector2.zero; letterboxBottom.anchorMax = new Vector2(1, h); letterboxBottom.offsetMin = letterboxBottom.offsetMax = Vector2.zero; }
        }

        void OnDestroy()
        {
            // never leave the player locked if the sector unloads mid-cinematic
            if (state == State.Playing || state == State.Skipping) { PlayerInputLock.Set(false); SetHud(true); if (playerCamera) playerCamera.enabled = true; if (playerListener) playerListener.enabled = true; }
        }
    }
}
