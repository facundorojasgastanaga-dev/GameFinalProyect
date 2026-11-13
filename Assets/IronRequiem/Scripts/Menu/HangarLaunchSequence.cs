using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Hangar menu bookends. On open: black fade-in with a power-on stutter of the hangar lamps.
    /// On JUGAR (via MenuLaunchRegistry): lamps stabilise, capsule C-04 vents and opens, the protagonist's visor
    /// ignites, the camera pushes in and the screen goes black before the existing scene load runs.
    public sealed class HangarLaunchSequence : MonoBehaviour, IMenuLaunchSequence
    {
        [SerializeField] MenuFlicker[] stabilise = new MenuFlicker[0];
        [SerializeField] MenuFlicker[] powerOn = new MenuFlicker[0];
        [SerializeField] Transform canopy;
        [SerializeField] Vector3 canopyOpenOffset = new Vector3(0, 1.1f, .25f);
        [SerializeField] Renderer canopyRenderer;
        [SerializeField] CapsuleRobotIdle protagonist;
        [SerializeField] MenuCameraRig cameraRig;
        [SerializeField] Transform pushTarget, lookTarget;
        [SerializeField] Graphic fade;
        [SerializeField] CanvasGroup menuPanel;
        [SerializeField] HangarStatusTag status;
        [SerializeField] BridgePatrol patrol;
        [SerializeField] ParticleSystem vent;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip openClip;
        [SerializeField] float introSeconds = 1.6f, launchSeconds = 3.5f;
        bool playing; Vector3 canopyClosed; MaterialPropertyBlock block; Color glassColor;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        public bool IsPlaying => playing;

        public void SetLookTarget(Transform t) => lookTarget = t;
        public void Configure(MenuFlicker[] steady, MenuFlicker[] startup, Transform glass, Renderer glassRenderer, CapsuleRobotIdle robot, MenuCameraRig rig, Transform push, Graphic black, CanvasGroup panel, HangarStatusTag tag, BridgePatrol walkers, ParticleSystem steam, AudioSource source, AudioClip clip)
        { stabilise = steady; powerOn = startup; canopy = glass; canopyRenderer = glassRenderer; protagonist = robot; cameraRig = rig; pushTarget = push; fade = black; menuPanel = panel; status = tag; patrol = walkers; vent = steam; audioSource = source; openClip = clip; }

        void OnEnable() => MenuLaunchRegistry.Active = this;
        void OnDisable() { if (ReferenceEquals(MenuLaunchRegistry.Active, this)) MenuLaunchRegistry.Active = null; }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (canopy) canopyClosed = canopy.localPosition;
            if (canopyRenderer && canopyRenderer.sharedMaterial && canopyRenderer.sharedMaterial.HasProperty(BaseColorId)) glassColor = canopyRenderer.sharedMaterial.GetColor(BaseColorId);
        }

        void Start() { StartCoroutine(Intro()); }

        IEnumerator Intro()
        {
            SetFade(1);
            foreach (var f in powerOn) if (f) f.Hold(0);
            float t = 0;
            while (t < introSeconds)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, .05f);
                float k = t / introSeconds;
                SetFade(1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(k * 1.25f)));
                // Lamps catch in three stutters before settling into their own behaviour.
                for (int i = 0; i < powerOn.Length; i++)
                {
                    if (!powerOn[i]) continue;
                    float local = k * 3f - i * .35f;
                    float level = local < 0 ? 0 : local > 1.6f ? -1 : (Mathf.Repeat(local * 4.3f, 1) < .45f ? Mathf.Clamp01(local) : .03f);
                    powerOn[i].Hold(level);
                }
                yield return null;
            }
            foreach (var f in powerOn) if (f) f.Hold(-1);
            SetFade(0);
        }

        public void Play(Action onComplete)
        {
            if (playing) return;
            playing = true; StopAllCoroutines(); StartCoroutine(Launch(onComplete));
        }

        IEnumerator Launch(Action onComplete)
        {
            // Timeline (s): panel out 0–.4 · capsule vents/opens .2–1.2 · camera to the face .1–1.8 ·
            // protagonist powers on at 1.8 · EN LÍNEA 2.4 · fade 2.9–3.5.
            if (patrol) patrol.Locked = true;
            foreach (var f in powerOn) if (f) f.Hold(-1);
            foreach (var f in stabilise) if (f) f.SetStable(true);
            if (status) status.Lock("INICIANDO", new Color(1f, .55f, .12f), 6f, "CÁPSULA C-04 · DESBLOQUEO DE SELLOS");
            if (vent) vent.Play(true);
            if (audioSource && openClip) audioSource.PlayOneShot(openClip, .8f);
            float t = 0; bool woke = false, online = false;
            while (t < launchSeconds)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, .05f);
                if (menuPanel) menuPanel.alpha = 1 - Mathf.Clamp01(t / .4f);
                float open = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .2f) / 1f));
                if (canopy) canopy.localPosition = canopyClosed + canopyOpenOffset * open;
                if (canopyRenderer)
                {
                    var c = glassColor; c.a = Mathf.Lerp(glassColor.a, 0, open);
                    block.SetColor(BaseColorId, c); canopyRenderer.SetPropertyBlock(block);
                    canopyRenderer.enabled = open < .97f; // its specular glare would otherwise linger over the robot
                }
                if (cameraRig && pushTarget)
                {
                    var look = Quaternion.LookRotation((lookTarget ? lookTarget.position : pushTarget.position) - pushTarget.position);
                    cameraRig.Dolly(pushTarget.position, look, Mathf.Clamp01((t - .1f) / 1.7f));
                }
                // v4: the robot stays powered off (eyes and visor dark) for the whole approach; Room00's visor shot turns it on.
                if (!woke) { woke = true; if (protagonist) protagonist.HoldDark(); }
                if (!online && t >= 2.4f) { online = true; if (status) status.Lock("EN LÍNEA", new Color(.25f, .9f, 1f), 0f, "UNIDAD C-04 · DESPLIEGUE AUTORIZADO"); }
                SetFade(Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 2.9f) / .55f)));
                yield return null;
            }
            SetFade(1);
            yield return null; // one black frame before the synchronous load hitch
            onComplete?.Invoke();
        }

        void SetFade(float a) { if (!fade) return; var c = fade.color; c.a = a; fade.color = c; fade.enabled = a > .001f; }
    }
}
