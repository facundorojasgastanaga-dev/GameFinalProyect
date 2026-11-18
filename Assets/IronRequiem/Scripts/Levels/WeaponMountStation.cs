using System.Collections;
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

        static readonly Vector3 RaiseStartWrist = new Vector3(.17f, -.44f, .36f), RaiseStartElbow = new Vector3(.27f, -.64f, .04f);
        // v6: clamp pads close on the forearm before the locks turn and open when the cannon is in place
        const float TClamp0 = .72f, TClamp1 = .95f, TUnclamp0 = 4.6f, TUnclamp1 = 4.85f, TPull = .32f;
        Transform clampA, clampB; Vector3 detachAxis;
        // timeline (seconds)
        public const float Duration = 5.6f;
        const float TRaise0 = .1f, TRaise1 = .75f, TArmA0 = .55f, TArmA1 = 1.35f, TLocks0 = 1.0f, TLocks1 = 1.5f, TDetach = 1.55f, TAway1 = 2.45f;
        const float TArmB0 = 1.85f, TApproach = 3.05f, TSlide = 3.5f, TLock = 3.8f, TLower1 = 4.55f, TSwap = 4.6f, TRelease1 = 4.85f;
        const float THeat0 = 4.8f, THeat1 = 5.1f;

        static bool mountedThisRun;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic() => mountedThisRun = false;

        PlayerWeaponsManager weapons; PlayerCharacterController player; ProtagonistHandsPresentation hands; AdvancedPlayerMovement movement;
        CampaignFlow flow; InputAction interact; MaterialPropertyBlock block;
        GameObject handProxy, cannonProxy; Transform proxyAttachment, proxyMuzzle;
        float t = -1, skipHeld; bool inside, promptShown; int fired;
        Vector3 handGripOffset; Quaternion handGripRotation;
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
            if (flashLight) flashLight.enabled = false; if (lockRing) lockRing.gameObject.SetActive(false);
        }
        IEnumerator Start()
        {
            interact = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/CollectWeapon", false) : null;
            yield return null; // CombatLabMenu.Start decides whether this load is a restart
            weapons = FindAnyObjectByType<PlayerWeaponsManager>(); flow = FindAnyObjectByType<CampaignFlow>();
            if (weapons) { player = weapons.GetComponent<PlayerCharacterController>(); movement = weapons.GetComponent<AdvancedPlayerMovement>(); }
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
                bool ready = inside && !Granted && weapons && player && !player.IsDead && !PlayerInputLock.Locked;
                ShowPrompt(ready);
                if (ready && interact != null && interact.WasPressedThisFrame()) Begin();
                return;
            }
            var kb = Keyboard.current;
            bool hold = kb != null && (kb.eKey.isPressed || kb.spaceKey.isPressed);
            skipHeld = hold && t > .3f ? skipHeld + Time.unscaledDeltaTime : 0;
            if (skipHeld >= skipHoldSeconds) { Skip(); return; }
            if (player && player.IsDead) { Abort(); return; }
            t += Time.deltaTime;
            Step(false);
            if (t >= Duration) { Finish(); return; }
            DriveArm();
        }

        void LateUpdate() { if (t >= 0) PoseVisuals(); }

        public void Begin()
        {
            if (t >= 0 || Granted || !weapons || !hands) return;
            ShowPrompt(false); WasSkipped = false; t = 0; fired = 0; skipHeld = 0;
            PlayerInputLock.Set(true);
            var face = Vector3.ProjectOnPlane(transform.forward, Vector3.up); if (face.sqrMagnitude < 1e-4f) face = Vector3.forward;
            if (movement) movement.BeginScriptedMove(StandPoint, .45f, 0);
            framingFrom = new Vector2(player.transform.eulerAngles.y, NormalizePitch(player.PlayerCamera.transform.localEulerAngles.x));
            framingTo = new Vector2(Quaternion.LookRotation(face).eulerAngles.y, 4f);
            BuildHandProxy(); BuildClamps(); SetFpsLayer(true);
            Debug.Log("[Campaign] montage station: sequence started", this);
        }
        Vector2 framingFrom, framingTo;
        Vector3 StandPoint { get { var p = transform.position - transform.forward * .55f; p.y = player ? player.transform.position.y : p.y; return p; } }
        static float NormalizePitch(float x) => x > 180 ? x - 360 : x;

        // ------------------------------------------------------------------ timeline
        bool Cross(int bit, float at) { if (t < at || (fired & (1 << bit)) != 0) return false; fired |= 1 << bit; return true; }
        void Step(bool skipping)
        {
            if (Cross(0, TLocks0) && !skipping) { Play(hydraulicClip, .9f); if (steam) steam.Play(true); if (lockRing) lockRing.gameObject.SetActive(true); }
            if (Cross(8, TClamp1) && !skipping) Play(clampClip, .7f);
            if (Cross(1, TLocks0 + .25f) && !skipping) { Play(sparkClip, .8f); if (sparks) sparks.Play(true); }
            if (Cross(2, TDetach)) { if (!skipping) Play(servoClip, .7f); if (lockRing) lockRing.gameObject.SetActive(false); AttachHandToGripper(); }
            if (Cross(3, TArmB0) && !skipping) Play(servoClip, .6f);
            if (Cross(4, TSlide) && !skipping) Play(clampClip, .9f);
            if (Cross(5, TLock))
            {
                if (!skipping) { Play(lockClip, 1f); if (dockFlash) dockFlash.Play(true); }
                if (flashLight && !skipping) flashLight.enabled = true;
                Lamp(doneColor, 6f);
            }
            if (Cross(6, TSwap)) Swap();
            if (Cross(7, THeat0)) { ShowMessage(); }
        }

        // poses: pure functions of t
        // Update: framing + the right-arm override (consumed by the IK later this frame)
        void DriveArm()
        {
            if (!player || !hands) return;
            var cam = player.PlayerCamera.transform;
            float f = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .45f));
            if (t < TRelease1) player.SetViewAngles(Mathf.LerpAngle(framingFrom.x, framingTo.x, f), Mathf.Lerp(framingFrom.y, framingTo.y, f));
            if (movement && t >= .5f && movement.IsScripted) movement.EndScriptedMove();
            // right arm: rise to the inspect pose, hold, then lower to the exact pose the mounted cannon keeps afterwards
            bool detached = t >= TDetach && t < TSwap + .02f;
            float weight = t < TRaise0 ? 0 : t < TSwap ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - TRaise0) / TTakeOver)) : 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - TSwap) / (TRelease1 - TSwap)));
            float rise = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - TRaise0) / (TRaise1 - TRaise0)));
            Vector3 wrist = Vector3.Lerp(RaiseStartWrist, inspectWrist, rise), elbow = Vector3.Lerp(RaiseStartElbow, inspectElbow, rise);
            // 16/11: while the override takes over, pull the start pose below and behind the camera so the blend from the
            // idle pose (above the frame) passes outside the view frustum instead of crossing its centre for a frame
            if (t < TRaise0 + TTakeOver) { float u = 1 - Mathf.Clamp01((t - TRaise0) / TTakeOver), d = .9f * u * u; var away = new Vector3(0, -d, -d); wrist += away; elbow += away; }
            PortPose(out var portWrist, out var portElbow);
            if (t > TLock + .15f) { float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - TLock - .15f) / (TLower1 - TLock - .15f))); wrist = Vector3.Lerp(inspectWrist, portWrist, k); elbow = Vector3.Lerp(inspectElbow, portElbow, k); }
            // locks shake the forearm a little (faded in/out, no step at either end)
            if (t > TLocks0 && t < TLocks1) { float env = Mathf.Sin(Mathf.PI * (t - TLocks0) / (TLocks1 - TLocks0)); float s = Mathf.Sin(t * 60f) * .003f * env; wrist += new Vector3(s, s * .5f, 0); }
            // settle: the clamp seat and the lock each push the forearm back a little and it springs into place
            wrist += Kick(t - TSlide, .45f) + Kick(t - TLock, 1f); elbow += .4f * (Kick(t - TSlide, .45f) + Kick(t - TLock, 1f));
            if (t < TRelease1) hands.SetRightArmOverride(wrist, elbow, weight, detached);
            else hands.ClearRightArmOverride();
        }
        const float TTakeOver = .16f;
        // damped spring, starts at zero (continuous): back toward the camera and slightly up, camera space
        static Vector3 Kick(float tau, float scale)
        {
            if (tau <= 0 || tau > .6f) return Vector3.zero;
            float e = Mathf.Exp(-tau * 11f) * Mathf.Sin(tau * Mathf.PI * 2f * 5.5f) * scale;
            return new Vector3(0, .005f, -.016f) * e;
        }

        // LateUpdate (after the IK): everything that follows the solved forearm
        void PoseVisuals()
        {
            if (!player || !hands) return;
            var cam = player.PlayerCamera.transform;
            var handPos = hands.RightHand.position; var forearmDir = (hands.RightHand.position - hands.RightForearmBone.position).normalized;
            if (lockRing && lockRing.gameObject.activeSelf)
            {
                lockRing.position = handPos - forearmDir * .02f;
                lockRing.rotation = Quaternion.LookRotation(forearmDir, cam.up) * Quaternion.Euler(0, 0, Mathf.Clamp01((t - TLocks0) / (TLocks1 - TLocks0)) * 120f);
            }
            PoseClamps(cam, handPos, forearmDir);
            if (steam) steam.transform.position = handPos; if (sparks) sparks.transform.position = handPos;
            if (dockFlash) dockFlash.transform.position = handPos; if (flashLight) { flashLight.transform.position = handPos + cam.forward * .05f; flashLight.intensity = Mathf.Max(0, 1 - (t - TLock) / .35f) * 6f; if (t > TLock + .4f) flashLight.enabled = false; }

            // arm A: reaches the wrist, grips, takes the hand away up and to the right
            if (handArm)
            {
                var near = handPos + cam.up * .07f + cam.right * .03f;
                var away = handArm.RestTarget + Vector3.up * .1f;
                Vector3 target; float grip;
                if (t < TArmA0) { target = handArm.RestTarget; grip = 0; }
                else if (t < TArmA1) { float k = Mathf.SmoothStep(0, 1, (t - TArmA0) / (TArmA1 - TArmA0)); target = Vector3.Lerp(handArm.RestTarget, near, k); grip = Mathf.Clamp01((t - TArmA1 + .2f) / .2f); }
                else if (t < TDetach) { target = near; grip = 1; }
                else if (t < TDetach + TPull)
                {   // v6: the hand first slides straight off along the forearm axis, no twist
                    float k = Mathf.SmoothStep(0, 1, (t - TDetach) / TPull); target = handGripStart + detachAxis * (.1f * k); grip = 1;
                }
                else if (t < TAway1) { float k = Mathf.SmoothStep(0, 1, (t - TDetach - TPull) / (TAway1 - TDetach - TPull)); target = Vector3.Lerp(handGripStart + detachAxis * .1f, away, k); grip = 1; }
                else { target = away; grip = 1; }
                handArm.Pose(target, -cam.up, grip);
                if (handProxy && t >= TDetach) handProxy.transform.SetPositionAndRotation(handArm.Gripper.TransformPoint(handGripOffset), handArm.Gripper.rotation * handGripRotation);
            }
            // arm B + cannon proxy: rack -> in front of the wrist -> slides on -> twist lock -> follows the arm down
            if (cannonArm && cannonProxy)
            {
                // the module's Attachment sits MountedHandLead behind the hand bone once mounted (it sleeves the forearm)
                var axis = forearmDir; Vector3 attach = handPos - axis * hands.MountedHandLead;
                Vector3 proxyAt; float twist = 0, slide;
                var rack = cannonRack ? cannonRack.position : cannonArm.RestTarget;
                if (t < TArmB0) { PlaceProxyFree(rack, cannonRack ? cannonRack.rotation : transform.rotation); cannonArm.Pose(GripPointOnProxy(), Vector3.down, t > TArmB0 - .3f ? 1 : 0); }
                else if (t < TApproach)
                {
                    float k = Mathf.SmoothStep(0, 1, (t - TArmB0) / (TApproach - TArmB0));
                    var front = attach + axis * .38f;
                    proxyAt = Vector3.Lerp(rack, front, k);
                    var rot = Quaternion.Slerp(cannonRack ? cannonRack.rotation : transform.rotation, MuzzleAligned(axis, cam.up, 25f), k);
                    PlaceProxyFree(proxyAt, rot, true);
                    cannonArm.Pose(GripPointOnProxy(), Vector3.down, 1);
                }
                else if (t < TSwap)
                {
                    slide = t < TSlide ? .38f * (1 - Mathf.SmoothStep(0, 1, (t - TApproach) / (TSlide - TApproach))) : 0;
                    twist = t < TSlide ? 25f : t < TLock ? 25f * (1 - Mathf.SmoothStep(0, 1, (t - TSlide) / (TLock - TSlide))) : 0;
                    PlaceProxyOnArm(attach, axis, cam.up, slide, twist);
                    // 16/11: the last stretch of the lowering settles the proxy on the exact transform the real cannon
                    // spawns with: the socket at its rest position (DefaultWeaponPosition; before the swap the socket
                    // itself still sits where the empty-handed manager left it), so the swap is not visible
                    float seat = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - (TLower1 - .22f)) / .22f));
                    if (seat > 0 && weapons)
                    {
                        var socket = weapons.WeaponParentSocket; var rest = weapons.DefaultWeaponPosition.position;
                        cannonProxy.transform.SetPositionAndRotation(Vector3.Lerp(cannonProxy.transform.position, rest, seat), Quaternion.Slerp(cannonProxy.transform.rotation, socket.rotation, seat));
                        GripPointOnProxyCached = GripPointOnProxy();
                    }
                    var hold = t < TLock + .1f ? GripPointOnProxy() : Vector3.Lerp(GripPointOnProxy(), cannonArm.RestTarget, Mathf.SmoothStep(0, 1, (t - TLock - .1f) / .6f));
                    cannonArm.Pose(hold, Vector3.down, t < TLock + .1f ? 1 : 0);
                }
                else cannonArm.Pose(Vector3.Lerp(GripPointOnProxyCached, cannonArm.RestTarget, Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - TLock - .1f) / .6f))), Vector3.down, 0);
            }
            else PoseArmsAtRest();

            if (t >= THeat0 && t < THeat1) { var heat = ActiveHeat(); if (heat) heat.PreviewHeat(Mathf.Lerp(.15f, .72f, (t - THeat0) / (THeat1 - THeat0))); }
        }

        // v6 forearm clamps: two dark pads either side of the forearm, closing with an eased stroke and a clank
        void BuildClamps()
        {
            if (clampA) return;
            Material mat = null; var arm = handArm ? handArm.GetComponentInChildren<Renderer>() : null; if (arm) mat = arm.sharedMaterial;
            clampA = MakePad("ClampPad_A", mat); clampB = MakePad("ClampPad_B", mat);
        }
        Transform MakePad(string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false); go.transform.localScale = new Vector3(.03f, .07f, .11f);
            var r = go.GetComponent<Renderer>(); if (mat) r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (hands) go.layer = hands.RightHand.gameObject.layer; go.SetActive(false); return go.transform;
        }
        void PoseClamps(Transform cam, Vector3 handPos, Vector3 axis)
        {
            if (!clampA) return;
            bool show = t >= TClamp0 - .25f && t < TUnclamp1 + .2f; clampA.gameObject.SetActive(show); clampB.gameObject.SetActive(show); if (!show) return;
            float close = t < TClamp0 ? 0 : t < TClamp1 ? Mathf.SmoothStep(0, 1, (t - TClamp0) / (TClamp1 - TClamp0)) : t < TUnclamp0 ? 1 : 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - TUnclamp0) / (TUnclamp1 - TUnclamp0)));
            var side = Vector3.Cross(axis, cam.forward); if (side.sqrMagnitude < 1e-4f) side = cam.right; side.Normalize();
            var centre = handPos - axis * .13f; float gap = Mathf.Lerp(.11f, .058f, close);
            var rot = Quaternion.LookRotation(axis, Vector3.Cross(side, axis));
            clampA.SetPositionAndRotation(centre + side * gap, rot); clampB.SetPositionAndRotation(centre - side * gap, rot);
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
        Vector3 proxyAttachLocal, proxyMuzzleLocal = Vector3.forward;
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
            GripPointOnProxyCached = GripPointOnProxy();
        }
        void PlaceProxyFree(Vector3 position, Quaternion rotation, bool byAttachment = false)
        {
            cannonProxy.transform.rotation = rotation;
            var offset = byAttachment && proxyAttachment ? proxyAttachment.position - cannonProxy.transform.position : Vector3.zero;
            cannonProxy.transform.position = position - offset;
        }
        Vector3 GripPointOnProxyCached;
        Vector3 GripPointOnProxy()
        {
            if (!cannonProxy) return cannonArm.RestTarget;
            var b = proxyBounds; return cannonProxy.transform.TransformPoint(b.center + Vector3.up * b.extents.y);
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
            if (handProxy) Destroy(handProxy);
            HideClamps();
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
            if (flashLight) flashLight.enabled = false;
            if (steam) steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); if (sparks) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (lockRing) lockRing.gameObject.SetActive(false);
            if (player) player.SetViewAngles(framingTo.x, framingTo.y);
            Finish();
        }
        void Abort()
        {
            if (hands) hands.ClearRightArmOverride(); if (handProxy) Destroy(handProxy); HideClamps(); SetFpsLayer(false);
            if (cannonProxy && !Granted) cannonProxy.SetActive(true);
            PlayerInputLock.Set(false); t = -1; PoseArmsAtRest();
        }
        void HideClamps() { if (clampA) clampA.gameObject.SetActive(false); if (clampB) clampB.gameObject.SetActive(false); }
        void MarkDone() { Granted = true; Lamp(doneColor, 2f); if (cannonProxy) cannonProxy.SetActive(false); }

        // ------------------------------------------------------------------ props
        void BuildCannonProxy()
        {
            if (cannonProxy || !weaponPrefab) return;
            // instantiated under an inactive holder so no weapon logic ever wakes up; display only
            var holder = new GameObject("CannonProxyHolder"); holder.SetActive(false); holder.transform.SetParent(transform, false);
            cannonProxy = Instantiate(weaponPrefab.gameObject, holder.transform);
            cannonProxy.name = "CannonProxy";
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
            SetLayer(cannonProxy.transform, gameObject.layer);
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
        void BuildHandProxy()
        {
            if (handProxy) Destroy(handProxy);
            handProxy = new GameObject("HandProxy"); handProxy.SetActive(false);
            var hand = hands.RightHand; handProxy.transform.SetPositionAndRotation(hand.position, hand.rotation);
            int layer = hand.gameObject.layer;
            foreach (var r in hands.RightHandRenderers)
            {
                if (!r) continue;
                Mesh mesh = null; Material[] mats = r.sharedMaterials;
                if (r is SkinnedMeshRenderer skin) { mesh = new Mesh(); skin.BakeMesh(mesh, true); }
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
        Vector3 handGripStart;
        void AttachHandToGripper()
        {
            if (!handProxy || !handArm) return;
            var hand = hands.RightHand;
            // bake the hand where it is now (the pose may have moved since Begin)
            handProxy.transform.SetPositionAndRotation(hand.position, hand.rotation);
            handProxy.SetActive(true);
            handGripOffset = handArm.Gripper.InverseTransformPoint(handProxy.transform.position);
            handGripRotation = Quaternion.Inverse(handArm.Gripper.rotation) * handProxy.transform.rotation;
            handGripStart = handArm.Gripper.position;
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
            if (handArm) handArm.Pose(handArm.RestTarget, Vector3.down, 0);
            if (cannonArm) cannonArm.Pose(cannonProxy && cannonProxy.activeSelf ? GripPointOnProxy() : cannonArm.RestTarget, Vector3.down, cannonProxy && cannonProxy.activeSelf ? 1 : 0);
        }

        // ------------------------------------------------------------------ feedback
        void Play(AudioClip clip, float volume) { if (sfx && clip) { sfx.pitch = Random.Range(.95f, 1.05f); sfx.PlayOneShot(clip, volume); } }
        void ShowPrompt(bool on)
        {
            if (on == promptShown) return; promptShown = on;
            var label = flow ? flow.Message : null; if (!label) return;
            if (on) label.text = prompt; else if (label.text == prompt) label.text = "";
        }
        void ShowMessage()
        {
            var label = flow ? flow.Message : null;
            if (label) { label.text = message; StartCoroutine(ClearMessage(label, message)); }
        }
        IEnumerator ClearMessage(TMP_Text label, string shown) { yield return new WaitForSeconds(messageSeconds); if (label && label.text == shown) label.text = ""; }
        void Lamp(Color c, float intensity)
        {
            if (!statusLamp) return;
            statusLamp.GetPropertyBlock(block); block.SetColor("_EmissionColor", c * intensity); block.SetColor("_BaseColor", c); statusLamp.SetPropertyBlock(block);
        }
        void OnDisable() { if (t >= 0) { PlayerInputLock.Set(false); if (hands) hands.ClearRightArmOverride(); } }
    }
}
