using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace IronRequiem
{
    /// Hangar menu easter egg: robots keep crossing the background bridge; clicking one blows it up.
    /// UI always wins (no shot while the pointer is over a panel). Kills are counted for the session.
    public sealed class BridgePatrol : MonoBehaviour
    {
        [SerializeField] Camera view;
        [SerializeField] BridgeWalker[] pool = new BridgeWalker[0];
        [SerializeField] Transform laneLeft, laneRight;
        [SerializeField] float laneDepth = .6f;
        [SerializeField] Vector2 spawnEvery = new Vector2(1.6f, 3.6f);
        [SerializeField, Min(1)] int maxActive = 4;
        [Header("Conveyor")]
        [SerializeField] bool conveyor;
        [SerializeField] float beltSpeed = 1.3f;
        [SerializeField] Vector2 beltGap = new Vector2(3.4f, 6f);
        [SerializeField] Vector2 respawnAfterKill = new Vector2(2.5f, 5f);
        [SerializeField] MenuExplosionFx[] explosions = new MenuExplosionFx[0];
        [SerializeField] MenuCameraRig cameraRig;
        [SerializeField] CapsuleRobotIdle protagonist;
        [SerializeField] HangarStatusTag status;
        [SerializeField] MenuFlicker[] surgeOnKill = new MenuFlicker[0];
        [Header("HUD")]
        [SerializeField] RectTransform hudRoot, reticle;
        [SerializeField] CanvasGroup reticleGroup, counterGroup;
        [SerializeField] TMP_Text counter, reticleLabel;
        static int sessionKills;
        float nextSpawn, nextGap; BridgeWalker lastSpawned; int explosionIndex; float counterFlash, reticleAlpha; BridgeWalker hovered;
        readonly RaycastHit[] hits = new RaycastHit[8];
        public bool Locked { get; set; }
        public int Kills => sessionKills;

        public void Configure(Camera cam, BridgeWalker[] walkers, Transform left, Transform right, MenuExplosionFx[] fx, MenuCameraRig rig, CapsuleRobotIdle robot, HangarStatusTag tag, MenuFlicker[] surge)
        { view = cam; pool = walkers; laneLeft = left; laneRight = right; explosions = fx; cameraRig = rig; protagonist = robot; status = tag; surgeOnKill = surge; }
        public void ConfigureConveyor(float speed) { conveyor = true; beltSpeed = speed; }
        public void ConfigureHud(RectTransform root, RectTransform aim, CanvasGroup aimGroup, TMP_Text aimLabel, CanvasGroup counterCanvas, TMP_Text counterText)
        { hudRoot = root; reticle = aim; reticleGroup = aimGroup; reticleLabel = aimLabel; counterGroup = counterCanvas; counter = counterText; }

        void Start()
        {
            foreach (var w in pool) if (w) w.Kill();
            if (conveyor)
            {
                // Open with the belt already loaded, robots spaced out along it.
                float length = laneLeft && laneRight ? Vector3.Distance(laneLeft.position, laneRight.position) : 28;
                for (float d = length - Random.Range(1f, 3f); d > 1; d -= Random.Range(beltGap.x, beltGap.y)) Spawn(d / length);
                nextGap = Random.Range(beltGap.x, beltGap.y);
            }
            else for (int i = 0; i < Mathf.Min(2, pool.Length); i++) Spawn(Random.Range(.15f, .7f)); // a couple already crossing
            nextSpawn = Time.time + Random.Range(spawnEvery.x, spawnEvery.y);
            if (counterGroup) counterGroup.alpha = sessionKills > 0 ? 1 : 0;
            UpdateCounter();
            if (reticleGroup) reticleGroup.alpha = 0;
        }

        void Update()
        {
            if (conveyor)
            {
                if (!lastSpawned || !lastSpawned.Alive || lastSpawned.Travelled >= nextGap) { if (Spawn(0)) nextGap = Random.Range(beltGap.x, beltGap.y); }
            }
            else if (Time.time >= nextSpawn)
            {
                if (ActiveCount() < maxActive) Spawn(0);
                nextSpawn = Time.time + Random.Range(spawnEvery.x, spawnEvery.y);
            }
            HandlePointer();
            if (counterGroup && sessionKills > 0)
            {
                counterFlash = Mathf.MoveTowards(counterFlash, 0, Time.unscaledDeltaTime * 2);
                counterGroup.alpha = Mathf.MoveTowards(counterGroup.alpha, 1, Time.unscaledDeltaTime * 4);
                if (counter) counter.color = Color.Lerp(new Color(1f, .58f, .2f), Color.white, counterFlash);
            }
        }

        void HandlePointer()
        {
            hovered = null;
            var mouse = Mouse.current;
            if (mouse != null && view && !Locked)
            {
                bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
                if (!overUi)
                {
                    var ray = view.ScreenPointToRay(mouse.position.ReadValue());
                    int n = Physics.RaycastNonAlloc(ray, hits, 250f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                    float best = float.MaxValue;
                    for (int i = 0; i < n; i++)
                    {
                        var w = hits[i].collider.GetComponentInParent<BridgeWalker>();
                        if (w && w.Alive && hits[i].distance < best) { best = hits[i].distance; hovered = w; }
                    }
                    if (hovered && mouse.leftButton.wasPressedThisFrame) { Detonate(hovered); hovered = null; }
                }
            }
            reticleAlpha = Mathf.MoveTowards(reticleAlpha, hovered ? 1 : 0, Time.unscaledDeltaTime * (hovered ? 10 : 5));
            if (reticleGroup) reticleGroup.alpha = reticleAlpha;
            if (reticle && hudRoot && mouse != null && reticleAlpha > 0)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRoot, mouse.position.ReadValue(), null, out var local)) reticle.anchoredPosition = local;
                float pulse = 1 + .08f * Mathf.Sin(Time.unscaledTime * 12);
                reticle.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, reticleAlpha) * pulse;
                reticle.localRotation = Quaternion.Euler(0, 0, (1 - reticleAlpha) * 45);
                if (hovered && reticleLabel) reticleLabel.text = "HOSTIL · " + hovered.Label;
            }
        }

        public void Detonate(BridgeWalker w)
        {
            if (!w || !w.Alive) return;
            var fx = explosions.Length > 0 ? explosions[explosionIndex++ % explosions.Length] : null;
            if (fx) fx.Play(w.Center, w.BlastScale);
            w.Kill();
            sessionKills++; counterFlash = 1; UpdateCounter();
            if (cameraRig) cameraRig.Shake(.32f);
            if (protagonist) protagonist.Pulse(1);
            foreach (var f in surgeOnKill) if (f) f.Pulse(.6f);
            if (status) status.ReportKill(sessionKills);
            if (!conveyor) nextSpawn = Mathf.Min(nextSpawn, Time.time + Random.Range(respawnAfterKill.x, respawnAfterKill.y));
        }

        /// Editor/bridge test hook: blows up the first robot on screen.
        public void DebugDetonateFirst()
        {
            BridgeWalker best = null; float score = float.MaxValue;
            foreach (var w in pool)
            {
                if (!w || !w.Alive || !view) continue;
                var v = view.WorldToViewportPoint(w.Center); float d = Mathf.Abs(v.x - .5f) + (v.z < 0 ? 10 : 0);
                if (d < score) { score = d; best = w; }
            }
            if (best) Detonate(best);
        }

        /// Editor/bridge test hook: puts a robot near the middle of the visible stretch.
        public void DebugSpawnCentre()
        {
            foreach (var w in pool) if (w && !w.Alive) { w.Begin(laneLeft.position, laneRight.position, .45f, conveyor ? beltSpeed : -1, conveyor, 180); return; }
        }

        void UpdateCounter() { if (counter) counter.text = "HOSTILES NEUTRALIZADOS  <b>" + sessionKills.ToString("00") + "</b>"; }

        int ActiveCount() { int c = 0; foreach (var w in pool) if (w && w.Alive) c++; return c; }

        bool Spawn(float startFraction)
        {
            if (!laneLeft || !laneRight) return false;
            BridgeWalker pick = null; int seen = 0;
            foreach (var w in pool) if (w && !w.Alive && Random.Range(0, ++seen) == 0) pick = w; // reservoir pick: random free robot
            if (!pick) return false;
            if (conveyor)
            {
                var off = laneRight.forward * Random.Range(-.12f, .12f);
                pick.Begin(laneLeft.position + off, laneRight.position + off, startFraction, beltSpeed, true, 180 + Random.Range(-22f, 22f));
                lastSpawned = pick; // prefill runs far→near, so the last one is always closest to the start
                return true;
            }
            bool leftToRight = Random.value < .5f;
            var depth = (laneRight.forward) * Random.Range(-laneDepth, laneDepth);
            Vector3 a = (leftToRight ? laneLeft.position : laneRight.position) + depth, b = (leftToRight ? laneRight.position : laneLeft.position) + depth;
            pick.Begin(a, b, startFraction);
            return true;
        }
    }
}
