using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronRequiem
{
    /// v5 montage station (lobby airlock): the player steps in and presses E. Input locks and the view is framed;
    /// the right forearm rises, the wrist locks turn with steam and sparks, the robot takes its hand off and arm A
    /// carries it away; arm B brings Weapon_IR_ArmCannon_VO, aligns it, slides it on and twists it locked (cyan flash).
    /// The cannon is then really added through PlayerWeaponsManager, heats up once and cools while the HUD confirms.
    /// About 5.6 s. Hold E or Space to skip: same final state. Once mounted, a campaign restart gives the cannon back
    /// silently and the sequence never repeats (until a new game from the main menu).
    /// The pose is a pure function of the sequence time; discrete events fire once when their time is crossed.
    /// 16/11: the arm override is set in Update (before ProtagonistHandsPresentation's IK, order 110) and every prop
    /// that follows the forearm is placed in LateUpdate after it (order 1100), so clamps, lock ring, grippers and the
    /// cannon read the solved hand instead of the raw animator pose. The cannon proxy ends exactly on the pose the
    /// real weapon spawns at, so the swap is invisible; the real cannon skips its deploy motion.
    /// 18/11 v7, four readable beats in the same 5.6 s:
    ///   APPROACH  the forearm is presented into the station; arm A grips the wrist from its right side, the ring
    ///             unlocks with a snap and the hand is carried away to the right; the bare port turns toward arm B,
    ///             which lifts the module off the rack by its muzzle (end-on, jaws across the barrel).
    ///   GRIP      the module slides on and seats with a clunk (arrives with speed, small kick).
    ///   LOCK      ease-in twist, overshoot snap and a 0.25 s hold; lock sound, sparks, dock flash, light flash,
    ///             module lights, camera and forearm kick all on the same frame.
    ///   RETRACT   clamps open, drop and scale away; arm B backs off the muzzle and returns; the forearm lowers into
    ///             the mounted pose; arm A returns to rest; the key light fades. Nothing is left floating.
    /// Grippers stay out of the central third of the view; the arm solve keeps the elbows inside the station's own
    /// volume (StationArmRig). DockLight is the only light: a dim key for the whole sequence, flashing at the lock.
    [RequireComponent(typeof(BoxCollider))]
    [DefaultExecutionOrder(1100)]
    public sealed class WeaponMountStation : MonoBehaviour
    {
        [SerializeField] WeaponController weaponPrefab;
        [SerializeField] StationArmRig handArm, cannonArm;
        [SerializeField] Transform cannonRack, lockRing;
        [SerializeField] Renderer statusLamp;
        [SerializeField] Color readyColor = new Color(1f, .55f, .1f), doneColor = new Color(.2f, 1f, .85f);
        [SerializeField] ParticleSystem steam, sparks, dockFlash;
        [SerializeField] Light flashLight;
        [SerializeField] AudioSource sfx;
        [SerializeField] AudioClip hydraulicClip, sparkClip, servoClip, clampClip, lockClip;
        [SerializeField] string prompt = "[E] ACOPLAR MÓDULO DE CAÑÓN";
        [SerializeField] string message = "MÓDULO DE CAÑÓN ACOPLADO · CLIC IZQUIERDO DISPARA · [F] GOLPE (MANO IZQUIERDA)";
        [SerializeField, Min(.5f)] float messageSeconds = 4f;
        [SerializeField, Min(.2f)] float skipHoldSeconds = .6f;
        [Header("Camera-space poses (metres)")]
        // v6: the forearm is framed in the centre of the view, rising nearly straight up in front of the camera
        [SerializeField] Vector3 inspectWrist = new Vector3(.015f, -.085f, .42f), inspectElbow = new Vector3(.13f, -.36f, .10f);
        [Tooltip("v7: once the hand is off, the bare port turns toward arm B (pointing left, below the centre of the view) to receive the module.")]
        [SerializeField] Vector3 mountWrist = new Vector3(.02f, -.14f, .45f), mountElbow = new Vector3(.37f, -.18f, .29f);
        [Tooltip("v7: the forearm is pushed this far into the station while arm A takes the hand (keeps the gripper further from the camera).")]
        [SerializeField] Vector3 presentOffset = new Vector3(0, 0, .04f);
        [Tooltip("v7: where arm A's gripper holds the wrist, camera space from the hand bone: right side, level with it (outside the central third).")]
        [SerializeField] Vector3 wristGrip = new Vector3(.10f, -.015f, .03f);
        [Tooltip("24/11/2026: arm A's jaws close this far behind the hand bone, along the forearm (on the wrist / glove cuff).")]
        [SerializeField, Min(0)] float wristContactBack = .07f;
        [Tooltip("24/11/2026: the claw centre rests this far out from the wrist axis (on the cuff), and the jaws close only this much (0..1) so they hug the cuff instead of cutting through it.")]
        [SerializeField, Min(0)] float cuffRadius = .05f;
        [SerializeField, Range(0, 1)] float gripHold = .45f;
        [Tooltip("24/11/2026: the arm's jaws close on the wrist with this much reach to spare; the offered forearm moves at most maxOffer.")]
        [SerializeField, Min(0)] float reachMargin = .05f, maxOffer = .25f;
        [Header("v7 key light (DockLight: the only light, never casts shadows)")]
        [SerializeField, Min(0)] float keyIntensity = 1.8f, flashIntensity = 6f;
        [SerializeField, Min(.5f)] float keyRange = 3f;
        [Tooltip("Station-local anchor of the key light: above and in front of the work point (about 1 m from the forearm).")]
        [SerializeField] Vector3 keyAnchor = new Vector3(0, 2.35f, -.2f);

        static readonly Vector3 RaiseStartWrist = new Vector3(.17f, -.44f, .36f), RaiseStartElbow = new Vector3(.27f, -.64f, .04f);
        // timeline (seconds)
        public const float Duration = 5.6f;
        // APPROACH, hand side: rise, present, clamps in and closed, wrist ring unlocks (snap at TLocks1), hand off
        const float TRaise0 = .1f, TRaise1 = .75f, TTakeOver = .16f, TArmA0 = .55f, TArmA1 = 1.35f;
        const float TClampIn = .45f, TClamp0 = .72f, TClamp1 = .95f, TLocks0 = 1.0f, TLocks1 = 1.3f;
        const float TDetach = 1.55f, TPull = .2f, TAway1 = 2.45f;
        // APPROACH, cannon side: the bare port turns toward arm B while arm B brings the module from the rack
        const float TTurn0 = 1.85f, TTurn1 = 2.65f, TArmB0 = 1.85f, TApproach = 3.05f;
        // GRIP (slide and seat), LOCK (twist, snap, hold)
        const float TSlide = 3.45f, TLock = 3.8f, THoldEnd = 4.05f;
        // RETRACT
        const float TArmBBack = .15f, TArmBRest = .75f, TClampGone = 4.4f, TLower1 = 4.55f, TSwap = 4.6f, TRelease1 = 4.85f;
        const float TArmARet0 = 4.3f, TArmARet1 = 5.2f, TLightOut0 = 4.9f, TLightOut1 = 5.45f, THeat0 = 4.8f, THeat1 = 5.1f;
        // shapes (metres / degrees)
        const float SlideLength = .38f, TwistDegrees = 25f, LockOvershoot = .3f, LockSettle = .12f, RingTurn = 60f, LiftArc = .12f;
        const float PullLength = .05f, ArmBBackOff = .08f, ClampOpenGap = .11f, ClampClosedGap = .058f, MuzzleGripBeyond = .03f;
        static readonly Vector3 PadScale = new Vector3(.03f, .07f, .11f), KickVector = new Vector3(0, .008f, -.04f);
        static readonly Vector3 LockCameraEuler = new Vector3(-1.2f, .3f, -.4f), LockCameraOffset = new Vector3(0, -.004f, -.014f);
        static readonly int PanelBoost = Shader.PropertyToID("_PanelBoost");

        static bool mountedThisRun;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic() => mountedThisRun = false;

        PlayerWeaponsManager weapons; PlayerCharacterController player; ProtagonistHandsPresentation hands; AdvancedPlayerMovement movement;
        StolenWeaponCarrier carrier;
        CampaignFlow flow; InputAction interact, jump; MaterialPropertyBlock block;
        GameObject handProxy, cannonProxy; Transform proxyAttachment, proxyMuzzle;
        readonly List<Mesh> bakedMeshes = new List<Mesh>(4);
        Transform clampA, clampB; Vector3 detachAxis, handGripStart;
        Vector3 clampCentre, clampSide, clampAxis, clampDrop;
        Vector3 releaseGrip, releaseFwd, releaseUp;
        float t = -1, skipHeld, lockTwist; bool inside, promptShown; int fired;
        Vector3 handGripOffset, lastGripTarget, offerOffset; Quaternion handGripRotation;
        Vector3 ringScale = Vector3.one; Color keyColor = Color.white, flashColor = new Color(.35f, .9f, 1f);
        Renderer[] proxyLights = System.Array.Empty<Renderer>(), proxyRings = System.Array.Empty<Renderer>();
        float appliedLights = float.NaN, appliedRings = float.NaN;
        public bool Granted { get; private set; }
        public bool Busy => t >= 0;
        public float SequenceTime => t;
        public bool WasSkipped { get; private set; }
        public WeaponController WeaponPrefab => weaponPrefab;
        public void Configure(WeaponController prefab, StationArmRig armA, StationArmRig armB, Transform rack, Transform locks, Renderer lamp,
            ParticleSystem steamFx, ParticleSystem sparkFx, ParticleSystem flashFx, Light flash, AudioSource audio,
            AudioClip hydraulic, AudioClip spark, AudioClip servo, AudioClip clamp, AudioClip locked)
        {
            weaponPrefab = prefab; handArm = armA; cannonArm = armB; cannonRack = rack; lockRing = locks; statusLamp = lamp;
            steam = steamFx; sparks = sparkFx; dockFlash = flashFx; flashLight = flash; sfx = audio;
            hydraulicClip = hydraulic; sparkClip = spark; servoClip = servo; clampClip = clamp; lockClip = locked;
        }

        void Awake()
        {
            message = message.Replace("[F] GOLPE MECÁNICO", "[F] GOLPE (MANO IZQUIERDA)"); // v7: F strikes with the left hand (older prefab text)
            GetComponent<BoxCollider>().isTrigger = true; block = new MaterialPropertyBlock();
            if (flashLight)
            {
                flashColor = flashLight.color; keyColor = Color.Lerp(Color.white, flashColor, .35f);
                flashLight.shadows = LightShadows.None; flashLight.enabled = false;
            }
            if (lockRing) { ringScale = lockRing.localScale; lockRing.gameObject.SetActive(false); }
        }
        IEnumerator Start()
        {
            interact = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/CollectWeapon", false) : null;
            jump = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Jump", false) : null;
            yield return null; // CombatLabMenu.Start decides whether this load is a restart
            weapons = FindAnyObjectByType<PlayerWeaponsManager>(); flow = FindAnyObjectByType<CampaignFlow>();
            if (weapons) { player = weapons.GetComponent<PlayerCharacterController>(); movement = weapons.GetComponent<AdvancedPlayerMovement>(); carrier = weapons.GetComponent<StolenWeaponCarrier>(); }
            hands = FindAnyObjectByType<ProtagonistHandsPresentation>();
            if (!CombatLabMenu.ResumedAfterRestart) mountedThisRun = false;
            BuildCannonProxy();
            if (mountedThisRun && weapons && weaponPrefab)
            {
                // restart after the station: the cannon is already part of the loadout
                if (!weapons.HasWeapon(weaponPrefab) && weapons.AddWeapon(weaponPrefab)) SelectMounted(false);
                MarkDone();
            }
            else Lamp(readyColor, 3f);
            PoseArmsAtRest();
        }

        void OnTriggerEnter(Collider other) { if (other.GetComponentInParent<PlayerWeaponsManager>()) inside = true; }
        void OnTriggerExit(Collider other) { if (other.GetComponentInParent<PlayerWeaponsManager>()) { inside = false; ShowPrompt(false); } }

        void Update()
        {
            if (t < 0)
            {
                // v7: E is shared with executions and pickups (StolenWeaponCarrier, order 0, already ran this frame):
                // the station only takes it when nothing else does and the hands are free
                bool ready = inside && !Granted && weapons && player && !player.IsDead && !PlayerInputLock.Locked && CanBegin && ActionFree;
                ShowPrompt(ready);
                if (ready && interact != null && interact.WasPressedThisFrame()) Begin();
                return;
            }
            // v8: the skip hold reads the (rebindable) Interact and Jump actions, never fixed keys
            bool hold = Time.timeScale > 0 && ((interact != null && interact.IsPressed()) || (jump != null && jump.IsPressed()));
            skipHeld = hold && t > .3f ? skipHeld + Time.unscaledDeltaTime : 0;
            if (skipHeld >= skipHoldSeconds) { Skip(); return; }
            if (player && player.IsDead) { Abort(); return; }
            t += Time.deltaTime;
            Step(false);
            if (t >= Duration) { Finish(); return; }
            DriveArm();
        }

        void LateUpdate() { if (t >= 0) PoseVisuals(); }

        // no execution or disarm in progress (also guards direct Begin() calls)
        bool CanBegin => !(hands && hands.IsDisarming) && !(carrier && carrier.ExecutionInProgress);
        // the weapon is not switching and the E press is not aimed at an execution or a pickup
        bool ActionFree => weapons
            && (weapons.SwitchState == PlayerWeaponsManager.WeaponSwitchState.Up || weapons.SwitchState == PlayerWeaponsManager.WeaponSwitchState.Down)
            // only a press that would really execute or pick up belongs to the carrier (review: a far or blocked
            // candidate in the crosshair must not swallow the station's E)
            && !(carrier && ((carrier.ExecutionCandidate && carrier.ExecutionStatus == CannonPickupStatus.Ready)
                             || (carrier.Candidate && !carrier.Held && carrier.PickupStatus == CannonPickupStatus.Ready)));

        public void Begin()
        {
            if (t >= 0 || Granted || !weapons || !hands || !CanBegin) return;
            ShowPrompt(false); WasSkipped = false; t = 0; fired = 0; skipHeld = 0; lastGripTarget = Vector3.zero; offerOffset = Vector3.zero;
            PlayerInputLock.Set(true);
            var face = Vector3.ProjectOnPlane(transform.forward, Vector3.up); if (face.sqrMagnitude < 1e-4f) face = Vector3.forward;
            if (movement) movement.BeginScriptedMove(StandPoint, .45f, 0);
            framingFrom = new Vector2(player.transform.eulerAngles.y, NormalizePitch(player.PlayerCamera.transform.localEulerAngles.x));
            framingTo = new Vector2(Quaternion.LookRotation(face).eulerAngles.y, 4f);
            BuildHandProxy(); BuildClamps(); SetFpsLayer(true); releaseFwd = Vector3.zero;
            if (flashLight) { flashLight.shadows = LightShadows.None; flashLight.range = keyRange; flashLight.intensity = 0; flashLight.enabled = false; }
            Debug.Log("[Campaign] montage station: sequence started", this);
        }
        Vector2 framingFrom, framingTo;
        Vector3 StandPoint { get { var p = transform.position - transform.forward * .55f; p.y = player ? player.transform.position.y : p.y; return p; } }
        static float NormalizePitch(float x) => x > 180 ? x - 360 : x;

        // ------------------------------------------------------------------ timeline
        bool Cross(int bit, float at) { if (t < at || (fired & (1 << bit)) != 0) return false; fired |= 1 << bit; return true; }
        void Step(bool skipping)
        {
            // 19/11/2026, user request: no servo whine (servoClip is the screech the user asked to remove); arm moves
            // are silent or carried by a soft hydraulic, the hits keep their clamp/lock/spark sounds.
            Cross(0, TArmA0);                                                                                 // arm A reaches (silent)
            if (Cross(1, TClamp1) && !skipping) Play(clampClip, .7f);                                        // clamps bite
            if (Cross(2, TLocks0) && !skipping) { Play(hydraulicClip, .9f); Burst(steam); }                  // wrist locks release
            if (Cross(3, TLocks1) && !skipping) { Play(sparkClip, .8f); Burst(sparks); }                     // ring snaps open
            if (Cross(4, TDetach)) AttachHandToGripper();                                                    // hand off
            if (Cross(5, TArmB0) && !skipping) Play(hydraulicClip, .35f);                                    // arm B lifts the module
            if (Cross(6, TSlide) && !skipping) Play(clampClip, .9f);                                         // module seats
            if (Cross(7, TLock))
            {   // LOCK: sound, sparks, flash, light (PoseLight), module lights, camera and forearm kick on this frame
                if (!skipping)
                {
                    Play(lockClip, 1f, 1f); Play(sparkClip, .45f, 1f); Burst(sparks); Burst(dockFlash);
                    if (player && player.PlayerCamera) DistanceCameraImpulse.Kick(player.PlayerCamera, LockCameraEuler, LockCameraOffset);
                }
                Lamp(doneColor, 6f);
            }
            if (Cross(8, THoldEnd) && !skipping)
            {   // release: the clamps vent where they let go
                Play(hydraulicClip, .5f);
                if (steam) { steam.transform.position = clampCentre; Burst(steam); }
            }
            if (Cross(9, TSwap)) Swap();
            if (Cross(10, THeat0)) { ShowMessage(); }
        }

        // poses: pure functions of t
        // Update: framing + the right-arm override (consumed by the IK later this frame)
        void DriveArm()
        {
            if (!player || !hands) return;
            float f = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .45f));
            if (t < TRelease1) player.SetViewAngles(Mathf.LerpAngle(framingFrom.x, framingTo.x, f), Mathf.Lerp(framingFrom.y, framingTo.y, f));
            if (movement && t >= .5f && movement.IsScripted) movement.EndScriptedMove();
            bool detached = t >= TDetach && t < TSwap + .02f;
            float weight = t < TRaise0 ? 0 : t < TSwap ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - TRaise0) / TTakeOver)) : 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - TSwap) / (TRelease1 - TSwap)));
            float rise = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - TRaise0) / (TRaise1 - TRaise0)));
            Vector3 wrist = Vector3.Lerp(RaiseStartWrist, inspectWrist, rise), elbow = Vector3.Lerp(RaiseStartElbow, inspectElbow, rise);
            // 16/11: while the override takes over, pull the start pose below and behind the camera so the blend from the
            // idle pose (above the frame) passes outside the view frustum instead of crossing its centre for a frame
            if (t < TRaise0 + TTakeOver) { float u = 1 - Mathf.Clamp01((t - TRaise0) / TTakeOver), d = .9f * u * u; var away = new Vector3(0, -d, -d); wrist += away; elbow += away; }
            // APPROACH: the forearm is presented into the station while arm A takes the hand...
            var present = presentOffset * Mathf.SmoothStep(0, 1, (t - TArmA0) / (TArmA1 - TArmA0)); wrist += present; elbow += present;
            // ...then the bare port turns toward arm B to receive the module
            float turn = Mathf.SmoothStep(0, 1, (t - TTurn0) / (TTurn1 - TTurn0));
            wrist = Vector3.Lerp(wrist, mountWrist, turn); elbow = Vector3.Lerp(elbow, mountElbow, turn);
            // RETRACT: after the hold the armed forearm lowers onto the exact pose the mounted cannon keeps afterwards
            if (t > THoldEnd)
            {
                PortPose(out var portWrist, out var portElbow);
                float k = Mathf.SmoothStep(0, 1, (t - THoldEnd) / (TLower1 - THoldEnd)); wrist = Vector3.Lerp(wrist, portWrist, k); elbow = Vector3.Lerp(elbow, portElbow, k);
            }
            // the unlocking ring shakes the forearm a little (faded in/out, no step at either end)
            if (t > TLocks0 && t < TLocks1) { float env = Mathf.Sin(Mathf.PI * (t - TLocks0) / (TLocks1 - TLocks0)); float s = Mathf.Sin(t * 60f) * .003f * env; wrist += new Vector3(s, s * .5f, 0); }
            // seat (small) and lock (dry, ~2.6 cm back): the forearm recoils toward the camera and springs into place
            var kick = Kick(t - TSlide, .35f) + Kick(t - TLock, 1f); wrist += kick; elbow += .4f * kick;
            // 24/11/2026: arm A hangs 1.27 m from the presented wrist and reaches 1.07 m, so its jaws stopped ~13 cm short,
            // above the fingers. The forearm is offered toward arm A's shoulder only as far as needed for the jaws to close
            // on the wrist (measured every LateUpdate in PoseHandArm), faded in with the reach and out as the port turns.
            float offer = t < TArmA0 ? 0 : t < TArmA1 ? Mathf.SmoothStep(0, 1, (t - TArmA0) / (TArmA1 - TArmA0)) : t < TDetach + TPull ? 1 : 1 - Mathf.SmoothStep(0, 1, (t - TDetach - TPull) / (TTurn1 - TDetach - TPull));
            wrist += offerOffset * offer; elbow += offerOffset * offer;
            if (t < TRelease1) hands.SetRightArmOverride(wrist, elbow, weight, detached);
            else hands.ClearRightArmOverride();
        }
        // damped spring, starts at zero (continuous): back toward the camera and slightly up, camera space.
        // First peak 0.64 x KickVector at ~37 ms, rebound at ~0.13 s, settled by ~0.3 s.
        static Vector3 Kick(float tau, float scale)
        {
            if (tau <= 0 || tau > .5f) return Vector3.zero;
            return KickVector * (Mathf.Exp(-tau * 11f) * Mathf.Sin(tau * Mathf.PI * 2f * 5.5f) * scale);
        }
        // eases in from rest and arrives with speed at u = 1 (contact), then overshoots past 1 and settles by
        // u = 1 + settle: the dry snap of a lock. overshoot ~0.58 x the peak excess.
        static float SnapCurve(float u, float overshoot, float settle)
        {
            if (u <= 0) return 0;
            if (u < 1) return u * u * (2 - u);
            float v = (u - 1) / settle; if (v >= 1) return 1;
            return 1 + overshoot * Mathf.Sin(Mathf.PI * v) * (1 - v);
        }
        static float Arrive(float u) { u = Mathf.Clamp01(u); return u * u * (2 - u); }
        static float Pulse(float tau, float seconds) { if (tau < 0 || tau > seconds) return 0; float u = 1 - tau / seconds; return u * u; }
        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float k) { float m = 1 - k; return a * (m * m) + b * (2 * k * m) + c * (k * k); }

        // LateUpdate (after the IK): everything that follows the solved forearm
        void PoseVisuals()
        {
            if (!player || !hands) return;
            var cam = player.PlayerCamera.transform;
            var hand = hands.RightHand.position; var axis = (hands.RightHand.position - hands.RightForearmBone.position).normalized;
            // the module's Attachment sits MountedHandLead behind the hand bone once mounted (it sleeves the forearm)
            float lead = hands.MountedHandLead; var port = hand - axis * lead;
            PoseCannon(cam, axis, port);
            PoseHandArm(cam, hand);
            PoseLockRing(cam, hand, axis, port);
            PoseClamps(cam, hand, axis, lead);
            if (steam) steam.transform.position = t < THoldEnd ? hand - axis * .02f : clampCentre;
            if (sparks) sparks.transform.position = t < TApproach ? hand - axis * .02f : port;
            if (dockFlash) dockFlash.transform.position = port;
            PoseLight(cam, port);
            if (t >= TLock) Lamp(doneColor, Mathf.Lerp(6f, 2f, Mathf.SmoothStep(0, 1, (t - THoldEnd) / 1.2f)));
            if (t >= THeat0 && t < THeat1) { var heat = ActiveHeat(); if (heat) heat.PreviewHeat(Mathf.Lerp(.15f, .72f, (t - THeat0) / (THeat1 - THeat0))); }
        }

        // arm A: reaches the right side of the wrist, grips, slides the hand off along the forearm, carries it to the
        // right and up out of view; returns to rest during the retract (no pop at Finish)
        void PoseHandArm(Transform cam, Vector3 hand)
        {
            if (!handArm) return;
            if (t >= TArmA0 && t < TDetach)
            {   // shortfall of the arm toward the wrist as it would be without the offer (camera space, smoothed)
                var baseHand = hand - cam.TransformDirection(offerOffset * (t < TArmA1 ? Mathf.SmoothStep(0, 1, (t - TArmA0) / (TArmA1 - TArmA0)) : 1));
                var toShoulder = handArm.Shoulder - baseHand; float shortfall = toShoulder.magnitude - (handArm.Reach - reachMargin);
                var want = shortfall > 0 ? cam.InverseTransformDirection(toShoulder.normalized * Mathf.Min(shortfall, maxOffer)) : Vector3.zero;
                offerOffset = Vector3.Lerp(offerOffset, want, 1 - Mathf.Exp(-Time.deltaTime * 12f));
            }
            var grip = hand + cam.TransformDirection(wristGrip);
            // 24/11/2026 (user: "now it goes through the hand"): the claw still comes in from the right (a vertical approach is
            // out of the arm's reach), but it is placed by its jaw TIPS, just past the wrist, and only half closed, so the jaws
            // straddle the cuff instead of burying the whole claw in it.
            var side = -cam.right;
            var away = handArm.RestTarget + Vector3.up * .1f;
            Vector3 target = handArm.RestTarget, look = Vector3.down, up = cam.up; float g = 1;
            if (t < TArmA0) g = 0;
            else if (t < TArmA1)
            {
                float k = Mathf.SmoothStep(0, 1, (t - TArmA0) / (TArmA1 - TArmA0));
                target = Vector3.Lerp(handArm.RestTarget, grip, k); look = Vector3.Slerp(Vector3.down, side, k); g = gripHold * Mathf.Clamp01((t - TArmA1 + .2f) / .2f);
            }
            else if (t < TDetach) { target = grip; look = side; g = gripHold; }
            else if (t < TDetach + TPull)
            {   // v6: the hand first slides straight off along the forearm axis, no twist
                target = handGripStart + detachAxis * (PullLength * Mathf.SmoothStep(0, 1, (t - TDetach) / TPull)); look = side; g = gripHold;
            }
            else if (t < TAway1)
            {   // v7: then it swings out to the right before rising, so the gripper never crosses the centre of the view
                float k = Mathf.SmoothStep(0, 1, (t - TDetach - TPull) / (TAway1 - TDetach - TPull));
                var from = handGripStart + detachAxis * PullLength;
                target = Bezier(from, from + cam.right * .35f + cam.up * .05f, away, k); look = Vector3.Slerp(side, Vector3.down, k); g = gripHold;
            }
            else if (t < TArmARet0) { target = away; g = gripHold; }
            float stow = 0;
            if (t >= TArmARet0)
            {   // returning to rest: the carried hand is stowed (shrinks into the gripper) and the jaws open on the way,
                // so nothing pops at Finish (review)
                stow = Mathf.SmoothStep(0, 1, (t - TArmARet0) / (TArmARet1 - TArmARet0));
                target = Vector3.Lerp(away, handArm.RestTarget, stow); g = gripHold * (1 - stow);
            }
            handArm.Pose(target, look, g, up);
            // 24/11/2026 (user: "the animation is fine but it never touches the hand"): the solve leaves the jaws ~10 cm above
            // and beyond the requested point, at finger height. While reaching and holding, the target is corrected until the
            // jaws' centre closes on the wrist (just behind the hand bone); the correction fades in with the reach.
            if (t >= TArmA0 && t < TDetach)
            {
                float w = t < TArmA1 ? Mathf.SmoothStep(0, 1, (t - TArmA0) / (TArmA1 - TArmA0)) : 1;
                var wrist = hand - (hands.RightHand.position - hands.RightForearmBone.position).normalized * wristContactBack;
                // the claw rests ON the cuff: its centre sits cuffRadius out from the wrist axis, on the cuff's upper / near side
                // (toward the camera, so the grip reads), instead of on the axis where the whole claw went through the forearm
                var foreAxis = (hands.RightHand.position - hands.RightForearmBone.position).normalized;
                var outward = Vector3.ProjectOnPlane(cam.up - cam.forward * .6f, foreAxis); outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : cam.up;
                var onCuff = wrist + outward * cuffRadius;
                for (int i = 0; i < 3; i++) { target += Vector3.ClampMagnitude((onCuff - handArm.JawCentre) * w, .15f); handArm.Pose(target, look, g, up); }
                lastGripTarget = target;
            }
            if (handProxy && t >= TDetach)
            {
                handProxy.transform.SetPositionAndRotation(handArm.Gripper.TransformPoint(handGripOffset), handArm.Gripper.rotation * handGripRotation);
                handProxy.transform.localScale = Vector3.one * Mathf.Max(.001f, 1 - stow);
                if (stow >= 1 && handProxy.activeSelf) handProxy.SetActive(false);
            }
        }

        // module proxy: rack -> lifted along an arc to the front of the port -> slides on -> twist lock -> follows the
        // forearm down onto the real weapon pose. Arm B holds it by the muzzle until the hold ends, then lets go.
        void PoseCannon(Transform cam, Vector3 axis, Vector3 port)
        {
            lockTwist = TwistDegrees;
            if (cannonProxy && cannonProxy.activeSelf)
            {
                if (t < TArmB0) PlaceProxyOnRack();
                else if (t < TApproach)
                {
                    float k = Mathf.SmoothStep(0, 1, (t - TArmB0) / (TApproach - TArmB0));
                    var from = RackAttach(); var front = port + axis * SlideLength;
                    var at = Vector3.Lerp(from, front, k) + Vector3.up * (LiftArc * Mathf.Sin(Mathf.PI * k));
                    PlaceProxyFree(at, Quaternion.Slerp(RackRotation, MuzzleAligned(axis, cam.up, TwistDegrees), k), true);
                }
                else
                {
                    float slide = t < TSlide ? SlideLength * (1 - Arrive((t - TApproach) / (TSlide - TApproach))) : 0;
                    lockTwist = t < TSlide ? TwistDegrees : TwistDegrees * (1 - SnapCurve((t - TSlide) / (TLock - TSlide), LockOvershoot, LockSettle / (TLock - TSlide)));
                    PlaceProxyOnArm(port, axis, cam.up, slide, lockTwist);
                    // 16/11: the last stretch of the lowering settles the proxy on the exact transform the real cannon
                    // spawns with: the socket at its rest position (DefaultWeaponPosition; before the swap the socket
                    // itself still sits where the empty-handed manager left it), so the swap is not visible
                    float seat = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - (TLower1 - .22f)) / .22f));
                    if (seat > 0 && weapons)
                    {
                        var socket = weapons.WeaponParentSocket; var rest = weapons.DefaultWeaponPosition.position;
                        cannonProxy.transform.SetPositionAndRotation(Vector3.Lerp(cannonProxy.transform.position, rest, seat), Quaternion.Slerp(cannonProxy.transform.rotation, socket.rotation, seat));
                    }
                }
                // v7: the module powers up at the lock: status lights come on in sequence, rings flash and settle to
                // the real weapon's normal state (boost 0) well before the swap
                if (t < TLock) SetProxyPower(0, -1);
                else SetProxyPower(Mathf.Clamp01((t - TLock) / .3f), 3f * Pulse(t - TLock, .5f));
            }
            if (!cannonArm) return;
            if (t < THoldEnd && cannonProxy && cannonProxy.activeSelf)
            {
                var grip = MuzzleGrip(out var fwd, out var up);
                cannonArm.Pose(grip, -fwd, 1, up);
                releaseGrip = grip; releaseFwd = fwd; releaseUp = up; // the last held pose is where the release starts
            }
            else if (t >= THoldEnd && releaseFwd.sqrMagnitude > 0)
            {   // RETRACT: jaws open, the gripper backs off the muzzle along the barrel, then returns to rest
                float open = Mathf.SmoothStep(0, 1, (t - THoldEnd) / .08f);
                var back = releaseGrip + releaseFwd * ArmBBackOff;
                Vector3 target, look = -releaseFwd, up = releaseUp;
                if (t < THoldEnd + TArmBBack) target = Vector3.Lerp(releaseGrip, back, Mathf.SmoothStep(0, 1, (t - THoldEnd) / TArmBBack));
                else
                {
                    float k = Mathf.SmoothStep(0, 1, (t - THoldEnd - TArmBBack) / TArmBRest);
                    target = Vector3.Lerp(back, cannonArm.RestTarget, k); look = Vector3.Slerp(-releaseFwd, Vector3.down, k); up = Vector3.Slerp(releaseUp, Vector3.up, k);
                }
                cannonArm.Pose(target, look, 1 - open, up);
            }
        }

        // the wrist lock ring: at the wrist while the hand unlocks (turns 60 deg and snaps), then around the port for
        // the real lock, turning with the module's twist (snap at TLock); scales in and out, never pops
        void PoseLockRing(Transform cam, Vector3 hand, Vector3 axis, Vector3 port)
        {
            if (!lockRing) return;
            float size, angle; Vector3 at;
            if (t < TDetach + .05f)
            {
                size = Mathf.SmoothStep(0, 1, (t - (TLocks0 - .1f)) / .1f) * (1 - Sq(Mathf.Clamp01((t - (TDetach - .13f)) / .13f)));
                angle = RingTurn * SnapCurve((t - TLocks0) / (TLocks1 - TLocks0), LockOvershoot, .4f); at = hand - axis * .02f;
            }
            else
            {
                size = 1.15f * Mathf.SmoothStep(0, 1, (t - (TSlide - .15f)) / .15f) * (1 - Sq(Mathf.Clamp01((t - THoldEnd) / .15f)));
                angle = -RingTurn * (1 - lockTwist / TwistDegrees); at = port;
            }
            bool show = size > .01f;
            if (lockRing.gameObject.activeSelf != show) lockRing.gameObject.SetActive(show);
            if (!show) return;
            lockRing.localScale = ringScale * size;
            lockRing.SetPositionAndRotation(at, Quaternion.LookRotation(axis, cam.up) * Quaternion.Euler(0, 0, angle));
        }
        static float Sq(float x) => x * x;

        // key light: dim cool key from the station side for the whole sequence, a flicker when the ring snaps open,
        // a cyan flash next to the port on the lock frame, fade out after the retract. Never shadows.
        void PoseLight(Transform cam, Vector3 port)
        {
            if (!flashLight) return;
            float key = Mathf.SmoothStep(0, 1, t / .35f) * (1 - Mathf.SmoothStep(0, 1, (t - TLightOut0) / (TLightOut1 - TLightOut0)));
            float flash = Pulse(t - TLock, .32f), flicker = Pulse(t - TLocks1, .14f);
            float intensity = keyIntensity * key * (1 + .8f * flicker) + (flashIntensity - keyIntensity * key) * flash;
            flashLight.transform.position = Vector3.Lerp(transform.TransformPoint(keyAnchor), port + cam.up * .3f - cam.forward * .12f, flash);
            flashLight.color = Color.Lerp(keyColor, flashColor, Mathf.Max(flash, flicker));
            flashLight.intensity = intensity;
            bool on = intensity > .01f; if (flashLight.enabled != on) flashLight.enabled = on;
        }

        // v6 forearm clamps: two dark pads either side of the forearm. v7: they rise in from below the view, close with
        // an eased bite (tiny overshoot), hold the forearm until the hold ends, then open, drop away and shrink to
        // nothing (frozen where they let go, not following the forearm) and are hidden before the swap
        void BuildClamps()
        {
            if (clampA) return;
            Material mat = null; var arm = handArm ? handArm.GetComponentInChildren<Renderer>() : null; if (arm) mat = arm.sharedMaterial;
            clampA = MakePad("ClampPad_A", mat); clampB = MakePad("ClampPad_B", mat);
        }
        Transform MakePad(string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false); go.transform.localScale = PadScale;
            var r = go.GetComponent<Renderer>(); if (mat) r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (hands) go.layer = hands.RightHand.gameObject.layer; go.SetActive(false); return go.transform;
        }
        void PoseClamps(Transform cam, Vector3 hand, Vector3 axis, float lead)
        {
            if (!clampA || !clampB) return;
            bool show = t >= TClampIn && t < TClampGone;
            if (clampA.gameObject.activeSelf != show) clampA.gameObject.SetActive(show);
            if (clampB.gameObject.activeSelf != show) clampB.gameObject.SetActive(show);
            if (!show) return;
            if (t < THoldEnd)
            {   // live on the forearm, just behind the port; frozen from the release on
                clampAxis = axis; clampCentre = hand - axis * (lead + .06f); clampDrop = -cam.up;
                // perpendicular to the forearm in the view; the small right bias keeps it defined (and continuous) when
                // the forearm points along the view
                var s = Vector3.Cross(axis, cam.forward) + .25f * Vector3.ProjectOnPlane(cam.right, axis).normalized;
                clampSide = s.sqrMagnitude > 1e-6f ? s.normalized : cam.right;
            }
            float gap, size = 1, drop = 0;
            if (t < TClamp0) { float k = Mathf.SmoothStep(0, 1, (t - TClampIn) / (TClamp0 - TClampIn)); gap = Mathf.Lerp(.2f, ClampOpenGap, k); size = k; drop = .06f * (1 - k); }
            else if (t < THoldEnd) gap = Mathf.LerpUnclamped(ClampOpenGap, ClampClosedGap, SnapCurve((t - TClamp0) / (TClamp1 - TClamp0), .12f, .3f));
            else if (t < THoldEnd + .1f) gap = Mathf.Lerp(ClampClosedGap, ClampOpenGap, Mathf.SmoothStep(0, 1, (t - THoldEnd) / .1f));
            else
            {
                float u = Mathf.Clamp01((t - THoldEnd - .1f) / (TClampGone - THoldEnd - .1f));
                gap = ClampOpenGap + .06f * Mathf.SmoothStep(0, 1, u); size = 1 - u * u; drop = .08f * Mathf.SmoothStep(0, 1, u);
            }
            var rot = Quaternion.LookRotation(clampAxis, Vector3.Cross(clampSide, clampAxis));
            var centre = clampCentre + clampDrop * drop; var scale = PadScale * Mathf.Max(size, .001f);
            clampA.localScale = scale; clampB.localScale = scale;
            clampA.SetPositionAndRotation(centre + clampSide * gap, rot); clampB.SetPositionAndRotation(centre - clampSide * gap, rot);
        }

        // 16/11: camera-space wrist/elbow that ProtagonistHandsPresentation itself computes once the cannon is mounted
        // (hand bone MountedHandLead ahead of the Attachment along the muzzle, elbow hint 1 m behind), evaluated at
        // the weapon socket rest pose. Ending the lowering here makes the override fade-out after the swap motionless.
        void PortPose(out Vector3 wrist, out Vector3 elbow)
        {
            var cam = player.PlayerCamera.transform; var socket = weapons.WeaponParentSocket;
            var rest = weapons.DefaultWeaponPosition.position;
            var attachWorld = rest + socket.rotation * Vector3.Scale(proxyAttachLocal, InHandScale);
            var fwd = socket.rotation * proxyMuzzleLocal;
            var w = attachWorld + fwd * hands.MountedHandLead;
            wrist = cam.InverseTransformPoint(w); elbow = cam.InverseTransformPoint(w - fwd);
        }
        Vector3 proxyAttachLocal, proxyMuzzleLocal = Vector3.forward, proxyTipLocal;
        // world scale the weapon has under the socket (prefab root scale x socket scale)
        Vector3 InHandScale => weapons && weaponPrefab ? Vector3.Scale(weapons.WeaponParentSocket.lossyScale, weaponPrefab.transform.localScale) : Vector3.one;
        Quaternion MuzzleAligned(Vector3 axis, Vector3 up, float twist)
        {
            var muzzleLocalRot = proxyMuzzle ? Quaternion.Inverse(cannonProxy.transform.rotation) * proxyMuzzle.rotation : Quaternion.identity;
            return Quaternion.AngleAxis(twist, axis) * Quaternion.LookRotation(axis, up) * Quaternion.Inverse(muzzleLocalRot);
        }
        void PlaceProxyOnArm(Vector3 attach, Vector3 axis, Vector3 up, float slide, float twist)
        {
            cannonProxy.transform.rotation = MuzzleAligned(axis, up, twist);
            var offset = proxyAttachment ? proxyAttachment.position - cannonProxy.transform.position : Vector3.zero;
            cannonProxy.transform.position = attach + axis * slide - offset;
        }
        void PlaceProxyFree(Vector3 position, Quaternion rotation, bool byAttachment = false)
        {
            cannonProxy.transform.rotation = rotation;
            var offset = byAttachment && proxyAttachment ? proxyAttachment.position - cannonProxy.transform.position : Vector3.zero;
            cannonProxy.transform.position = position - offset;
        }
        Quaternion RackRotation => cannonRack ? cannonRack.rotation : transform.rotation;
        // v7: centred along its length on the rack point (on the shelf; keeps the muzzle grip clear of a side wall)
        void PlaceProxyOnRack()
        {
            if (!cannonProxy) return;
            var tr = cannonProxy.transform; tr.rotation = RackRotation;
            var along = proxyMuzzleLocal * Vector3.Dot(proxyBounds.center, proxyMuzzleLocal);
            tr.position = (cannonRack ? cannonRack.position : transform.position) - tr.TransformVector(along);
        }
        // where the module's Attachment sits while it rests on the rack (start of the lift, no jump at TArmB0)
        Vector3 RackAttach() { PlaceProxyOnRack(); return proxyAttachment ? proxyAttachment.position : cannonProxy.transform.position; }
        // v7: arm B holds the module end-on, just past the muzzle tip (jaws across the last centimetres of the barrel,
        // palm capping it): from the camera it stays on the far left of the module, never on top of it in the centre
        Vector3 MuzzleGrip(out Vector3 forward, out Vector3 up)
        {
            var tr = cannonProxy.transform;
            forward = proxyMuzzle ? proxyMuzzle.forward : tr.TransformDirection(proxyMuzzleLocal).normalized;
            up = proxyMuzzle ? proxyMuzzle.up : tr.up;
            return tr.TransformPoint(proxyTipLocal) + forward * MuzzleGripBeyond;
        }
        Bounds proxyBounds;

        void Swap()
        {
            if (cannonProxy) cannonProxy.SetActive(false);
            if (weapons && weaponPrefab && !weapons.HasWeapon(weaponPrefab) && weapons.AddWeapon(weaponPrefab))
            {
                // the station already slid the module on: it appears seated, without the generic deploy expansion
                var mounted = weapons.HasWeapon(weaponPrefab); var deploy = mounted ? mounted.GetComponent<WeaponDeployMotion>() : null;
                if (deploy) deploy.SkipNextDeploy();
                SelectMounted(true);
            }
            Granted = true; mountedThisRun = true;
            Debug.Log("[Campaign] montage station: " + weaponPrefab.name + " mounted", this);
        }
        void SelectMounted(bool snap)
        {
            for (int i = 0; i < 9; i++)
            {
                var slot = weapons.GetWeaponAtSlotIndex(i);
                if (slot && slot.SourcePrefab == weaponPrefab.gameObject) { weapons.SwitchToWeaponIndex(i, true); if (snap) weapons.SnapActiveWeaponUp(); break; }
            }
        }
        WeaponHeat ActiveHeat() { var w = weapons ? weapons.GetActiveWeapon() : null; return w ? w.GetComponent<WeaponHeat>() : null; }

        void Finish()
        {
            if (!Granted) Swap();
            if (hands) hands.ClearRightArmOverride();
            DestroyHandProxy();
            HideRuntimePieces();
            SetFpsLayer(false);
            if (movement && movement.IsScripted) movement.EndScriptedMove();
            PlayerInputLock.Set(false);
            t = -1; MarkDone(); PoseArmsAtRest();
        }
        /// Same final state as playing to the end.
        public void Skip()
        {
            if (t < 0) return;
            WasSkipped = true;
            t = Duration; Step(true);
            HideRuntimePieces();
            if (player) player.SetViewAngles(framingTo.x, framingTo.y);
            Finish();
        }
        void Abort()
        {
            if (hands) hands.ClearRightArmOverride();
            DestroyHandProxy(); HideRuntimePieces(); SetFpsLayer(false);
            if (cannonProxy && !Granted) { cannonProxy.SetActive(true); PlaceProxyOnRack(); SetProxyPower(0, -1); } // back on the rack, not mid-air
            if (movement && movement.IsScripted) movement.EndScriptedMove();
            PlayerInputLock.Set(false); t = -1; PoseArmsAtRest();
            if (!Granted) Lamp(readyColor, 3f); // review: aborted after the lock, nothing granted -> ready again
        }
        // every runtime piece the sequence shows: clamps, lock ring, particles and the light (Finish, Skip, Abort)
        void HideRuntimePieces()
        {
            if (clampA) { clampA.gameObject.SetActive(false); clampA.localScale = PadScale; }
            if (clampB) { clampB.gameObject.SetActive(false); clampB.localScale = PadScale; }
            if (lockRing) { lockRing.gameObject.SetActive(false); lockRing.localScale = ringScale; }
            StopFx(steam); StopFx(sparks); StopFx(dockFlash);
            if (flashLight) { flashLight.intensity = 0; flashLight.enabled = false; }
        }
        void MarkDone() { Granted = true; Lamp(doneColor, 2f); if (cannonProxy) cannonProxy.SetActive(false); }

        // ------------------------------------------------------------------ props
        void BuildCannonProxy()
        {
            if (cannonProxy || !weaponPrefab) return;
            // instantiated under an inactive holder so no weapon logic ever wakes up; display only
            var holder = new GameObject("CannonProxyHolder"); holder.SetActive(false); holder.transform.SetParent(transform, false);
            cannonProxy = Instantiate(weaponPrefab.gameObject, holder.transform);
            cannonProxy.name = "CannonProxy";
            // v7: the module's status lights and firing rings (read before the logic is stripped; the copies point at
            // the proxy's own renderers) power up at the lock through _PanelBoost, like ArmModuleVisual does
            var visual = cannonProxy.GetComponentInChildren<ArmModuleVisual>(true);
            if (visual) { if (visual.SequenceLights != null) proxyLights = visual.SequenceLights; if (visual.FiringRings != null) proxyRings = visual.FiringRings; }
            StripToVisuals(cannonProxy);
            proxyAttachment = null; proxyMuzzle = null;
            foreach (var tr in cannonProxy.GetComponentsInChildren<Transform>(true)) if (tr.name == "Attachment") { proxyAttachment = tr; break; }
            if (weaponPrefab.WeaponMuzzle) proxyMuzzle = cannonProxy.transform.Find(GetPath(weaponPrefab.transform, weaponPrefab.WeaponMuzzle));
            if (weaponPrefab.WeaponRoot) { var rootPath = GetPath(weaponPrefab.transform, weaponPrefab.WeaponRoot.transform); var vis = rootPath.Length > 0 ? cannonProxy.transform.Find(rootPath) : cannonProxy.transform; if (vis) vis.gameObject.SetActive(true); }
            cannonProxy.transform.SetParent(transform, false); Destroy(holder);
            cannonProxy.transform.SetPositionAndRotation(cannonRack ? cannonRack.position : transform.position, cannonRack ? cannonRack.rotation : transform.rotation);
            // 16/11: the proxy is the same object the player ends up holding: give it the in-hand world scale
            var parentScale = transform.lossyScale; var want = InHandScale;
            cannonProxy.transform.localScale = new Vector3(want.x / Mathf.Max(1e-4f, parentScale.x), want.y / Mathf.Max(1e-4f, parentScale.y), want.z / Mathf.Max(1e-4f, parentScale.z));
            proxyAttachLocal = proxyAttachment ? cannonProxy.transform.InverseTransformPoint(proxyAttachment.position) : Vector3.zero;
            proxyMuzzleLocal = proxyMuzzle ? cannonProxy.transform.InverseTransformDirection(proxyMuzzle.forward) : Vector3.forward;
            var renderers = cannonProxy.GetComponentsInChildren<Renderer>(true);
            proxyBounds = new Bounds(Vector3.zero, Vector3.one * .2f); bool first = true;
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) { r.enabled = false; continue; }
                if (!r.gameObject.activeInHierarchy || !r.enabled) continue;
                var local = new Bounds(cannonProxy.transform.InverseTransformPoint(r.bounds.center), Vector3.zero);
                local.Encapsulate(cannonProxy.transform.InverseTransformPoint(r.bounds.min)); local.Encapsulate(cannonProxy.transform.InverseTransformPoint(r.bounds.max));
                if (first) { proxyBounds = local; first = false; } else proxyBounds.Encapsulate(local);
            }
            // v7: the visual tip of the barrel on the muzzle line (the muzzle transform may sit inside or past the mesh)
            var dir = proxyMuzzleLocal.sqrMagnitude > 1e-6f ? proxyMuzzleLocal.normalized : Vector3.forward;
            var from = proxyMuzzle ? cannonProxy.transform.InverseTransformPoint(proxyMuzzle.position) : proxyBounds.center;
            var e = proxyBounds.extents; float support = Mathf.Abs(dir.x) * e.x + Mathf.Abs(dir.y) * e.y + Mathf.Abs(dir.z) * e.z;
            proxyTipLocal = from + dir * (support - Vector3.Dot(from - proxyBounds.center, dir));
            SetLayer(cannonProxy.transform, gameObject.layer);
            PlaceProxyOnRack();
            SetProxyPower(0, -1); // unpowered on the rack until it locks
        }
        // removes logic, audio and physics (dependents first, so RequireComponent never blocks a removal)
        static void StripToVisuals(GameObject root)
        {
            var list = new System.Collections.Generic.List<Component>();
            foreach (var c in root.GetComponentsInChildren<Component>(true))
                if (c && !(c is Transform) && !(c is Renderer) && !(c is MeshFilter)) list.Add(c);
            for (int pass = 0; pass < 6 && list.Count > 0; pass++)
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var c = list[i]; if (!c) { list.RemoveAt(i); continue; }
                    bool needed = false;
                    foreach (var other in c.GetComponents<Component>())
                    {
                        if (!other || other == c || !list.Contains(other)) continue;
                        foreach (RequireComponent req in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                            if ((req.m_Type0 != null && req.m_Type0.IsInstanceOfType(c)) || (req.m_Type1 != null && req.m_Type1.IsInstanceOfType(c)) || (req.m_Type2 != null && req.m_Type2.IsInstanceOfType(c))) needed = true;
                    }
                    if (!needed) { DestroyImmediate(c); list.RemoveAt(i); }
                }
        }
        static string GetPath(Transform root, Transform child)
        {
            if (!child || child == root) return "";
            var path = child.name; var p = child.parent;
            while (p && p != root) { path = p.name + "/" + path; p = p.parent; }
            return path;
        }
        // _PanelBoost on the proxy's module lights (0..1 = how many are on, in sequence) and rings (-1 off, 0 normal,
        // > 0 boost). Blocks are written only when a value changes.
        void SetProxyPower(float lights, float rings)
        {
            if (!Mathf.Approximately(lights, appliedLights))
            {
                appliedLights = lights;
                for (int i = 0; i < proxyLights.Length; i++)
                {
                    var r = proxyLights[i]; if (!r) continue;
                    r.GetPropertyBlock(block); block.SetFloat(PanelBoost, Mathf.Clamp01(lights * proxyLights.Length - i) - 1f); r.SetPropertyBlock(block);
                }
            }
            if (!Mathf.Approximately(rings, appliedRings))
            {
                appliedRings = rings;
                for (int i = 0; i < proxyRings.Length; i++) { var r = proxyRings[i]; if (!r) continue; r.GetPropertyBlock(block); block.SetFloat(PanelBoost, rings); r.SetPropertyBlock(block); }
            }
        }
        void BuildHandProxy()
        {
            DestroyHandProxy();
            handProxy = new GameObject("HandProxy"); handProxy.SetActive(false);
            var hand = hands.RightHand; handProxy.transform.SetPositionAndRotation(hand.position, hand.rotation);
            int layer = hand.gameObject.layer;
            foreach (var r in hands.RightHandRenderers)
            {
                if (!r) continue;
                Mesh mesh = null; Material[] mats = r.sharedMaterials;
                if (r is SkinnedMeshRenderer skin) { mesh = new Mesh(); skin.BakeMesh(mesh, true); bakedMeshes.Add(mesh); }
                else { var mf = r.GetComponent<MeshFilter>(); if (mf) mesh = mf.sharedMesh; }
                if (!mesh) continue;
                var piece = new GameObject(r.name + "_Proxy"); piece.layer = layer;
                piece.transform.SetParent(handProxy.transform, false);
                piece.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                if (r is SkinnedMeshRenderer) piece.transform.localScale = Vector3.one; else piece.transform.localScale = r.transform.lossyScale;
                piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = piece.AddComponent<MeshRenderer>(); mr.sharedMaterials = mats; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            handProxy.layer = layer;
        }
        // the baked hand meshes are runtime assets: destroyed with the proxy so repeated runs never leak them
        void DestroyHandProxy()
        {
            if (handProxy) Destroy(handProxy);
            handProxy = null;
            for (int i = 0; i < bakedMeshes.Count; i++) if (bakedMeshes[i]) Destroy(bakedMeshes[i]);
            bakedMeshes.Clear();
        }
        void AttachHandToGripper()
        {
            if (!handProxy || !handArm) return;
            var hand = hands.RightHand;
            // bake the hand where it is now (the pose may have moved since Begin)
            handProxy.transform.SetPositionAndRotation(hand.position, hand.rotation);
            handProxy.SetActive(true);
            handGripOffset = handArm.Gripper.InverseTransformPoint(handProxy.transform.position);
            handGripRotation = Quaternion.Inverse(handArm.Gripper.rotation) * handProxy.transform.rotation;
            handGripStart = lastGripTarget != Vector3.zero ? lastGripTarget : handArm.Gripper.position; // the corrected arm target held at the detach
            detachAxis = (hands.RightHand.position - hands.RightForearmBone.position).normalized;
        }
        void SetFpsLayer(bool on)
        {
            int layer = on && hands ? hands.RightHand.gameObject.layer : gameObject.layer;
            if (handArm && handArm.Gripper) SetLayer(handArm.Gripper, layer);
            if (cannonArm && cannonArm.Gripper) SetLayer(cannonArm.Gripper, layer);
            if (cannonProxy) SetLayer(cannonProxy.transform, layer);
            if (lockRing) SetLayer(lockRing, layer);
            foreach (var ps in new[] { steam, sparks, dockFlash }) if (ps) SetLayer(ps.transform, layer);
        }
        static void SetLayer(Transform root, int layer) { foreach (var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = layer; }
        void PoseArmsAtRest()
        {
            if (handArm) handArm.ResetSolver(); if (cannonArm) cannonArm.ResetSolver(); // review: no avoidance left frozen into the idle pose
            if (handArm) handArm.Pose(handArm.RestTarget, Vector3.down, 0);
            if (!cannonArm) return;
            if (cannonProxy && cannonProxy.activeSelf) { var grip = MuzzleGrip(out var fwd, out var up); cannonArm.Pose(grip, -fwd, 1, up); }
            else cannonArm.Pose(cannonArm.RestTarget, Vector3.down, 0);
        }

        // ------------------------------------------------------------------ feedback
        void Play(AudioClip clip, float volume) => Play(clip, volume, Random.Range(.95f, 1.05f));
        void Play(AudioClip clip, float volume, float pitch) { if (sfx && clip) { sfx.pitch = pitch; sfx.PlayOneShot(clip, volume); } }
        // restarts a one-shot effect even if the system is still inside its duration from an earlier burst
        static void Burst(ParticleSystem ps) { if (!ps) return; ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Play(true); }
        static void StopFx(ParticleSystem ps) { if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        string shownPrompt;
        void ShowPrompt(bool on)
        {
            if (on == promptShown) return; promptShown = on;
            var label = flow ? flow.Message : null; if (!label) return;
            if (on) { shownPrompt = ControlHints.ResolveLegacy(prompt); label.text = shownPrompt; }
            else if (shownPrompt != null && label.text == shownPrompt) { label.text = ""; shownPrompt = null; }
        }
        void ShowMessage()
        {
            var label = flow ? flow.Message : null;
            if (label) { string shown = ControlHints.ResolveLegacy(message); label.text = shown; StartCoroutine(ClearMessage(label, shown)); }
        }
        IEnumerator ClearMessage(TMP_Text label, string shown) { yield return new WaitForSeconds(messageSeconds); if (label && label.text == shown) label.text = ""; }
        void Lamp(Color c, float intensity)
        {
            if (!statusLamp) return;
            statusLamp.GetPropertyBlock(block); block.SetColor("_EmissionColor", c * intensity); block.SetColor("_BaseColor", c); statusLamp.SetPropertyBlock(block);
        }
        void OnDisable()
        {
            if (t < 0) return;
            PlayerInputLock.Set(false); if (hands) hands.ClearRightArmOverride();
            if (flashLight) flashLight.enabled = false;
            if (handProxy) handProxy.SetActive(false); // separate root: would otherwise stay in the scene
        }
        void OnDestroy() => DestroyHandProxy();
    }
}
