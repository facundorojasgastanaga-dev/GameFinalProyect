using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Gameplay
{
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerWeaponsManager : MonoBehaviour
    {
        public enum WeaponSwitchState
        {
            Up,
            Down,
            PutDownPrevious,
            PutUpNew,
        }

        [Tooltip("List of weapon the player will start with")]
        public List<WeaponController> StartingWeapons = new List<WeaponController>();

        [Header("References")] [Tooltip("Secondary camera used to avoid seeing weapon go throw geometries")]
        public Camera WeaponCamera;

        [Tooltip("Parent transform where all weapon will be added in the hierarchy")]
        public Transform WeaponParentSocket;

        [Tooltip("Position for weapons when active but not actively aiming")]
        public Transform DefaultWeaponPosition;

        [Tooltip("Position for weapons when aiming")]
        public Transform AimingWeaponPosition;

        [Tooltip("Position for innactive weapons")]
        public Transform DownWeaponPosition;

        [Header("Weapon Bob")]
        [Tooltip("Frequency at which the weapon will move around in the screen when the player is in movement")]
        public float BobFrequency = 10f;

        [Tooltip("How fast the weapon bob is applied, the bigger value the fastest")]
        public float BobSharpness = 10f;

        [Tooltip("Distance the weapon bobs when not aiming")]
        public float DefaultBobAmount = 0.05f;

        [Tooltip("Distance the weapon bobs when aiming")]
        public float AimingBobAmount = 0.02f;

        [Header("Weapon Recoil")]
        [Tooltip("This will affect how fast the recoil moves the weapon, the bigger the value, the fastest")]
        public float RecoilSharpness = 50f;

        [Tooltip("Maximum distance the recoil can affect the weapon")]
        public float MaxRecoilDistance = 0.5f;

        [Tooltip("How fast the weapon goes back to it's original position after the recoil is finished")]
        public float RecoilRestitutionSharpness = 10f;

        [Header("Misc")] [Tooltip("Speed at which the aiming animatoin is played")]
        public float AimingAnimationSpeed = 10f;

        [Tooltip("Field of view when not aiming")]
        public float DefaultFov = 60f;

        [Tooltip("Portion of the regular FOV to apply to the weapon camera")]
        public float WeaponFovMultiplier = 1f;

        [Tooltip("Delay before switching weapon a second time, to avoid recieving multiple inputs from mouse wheel")]
        public float WeaponSwitchDelay = 1f;

        [Tooltip("Ease the lower/raise motion of weapon switching (smoothstep) instead of a linear move with a hard stop. Off keeps the template motion.")]
        public bool SmoothWeaponSwitch = false;

        [Tooltip("Layer to set FPS weapon gameObjects to")]
        public LayerMask FpsWeaponLayer;

        public bool IsAiming { get; private set; }
        public bool IsPointingAtEnemy { get; private set; }
        public int ActiveWeaponIndex { get; private set; }
        public WeaponSwitchState SwitchState => m_WeaponSwitchState;
        // 0..1 progress of the current put-down / bring-up phase (1 when the weapon is up or down).
        public float SwitchPhaseProgress { get; private set; } = 1f;

        public UnityAction<WeaponController> OnSwitchedToWeapon;
        public UnityAction<WeaponController, int> OnAddedWeapon;
        public UnityAction<WeaponController, int> OnRemovedWeapon;

        WeaponController[] m_WeaponSlots = new WeaponController[9]; // 9 available weapon slots
        PlayerInputHandler m_InputHandler;
        PlayerCharacterController m_PlayerCharacterController;
        IPlayerMotionExtension m_MotionExtension;
        float m_WeaponBobFactor;
        Vector3 m_LastCharacterPosition;
        Vector3 m_WeaponMainLocalPosition;
        Vector3 m_WeaponBobLocalPosition;
        Vector3 m_WeaponRecoilLocalPosition;
        Vector3 m_AccumulatedRecoil;
        float m_TimeStartedWeaponSwitch;
        WeaponSwitchState m_WeaponSwitchState;
        int m_WeaponSwitchNewWeaponIndex;
        // IRON REQUIEM arms-fix (19/11/2026): each switch phase moves the socket from where it is when the phase starts
        // (no snap to Default/Down), and a phase reversed half-way continues from its progress (RetargetSwitch).
        Vector3 m_SwitchPhaseStartLocalPosition;
        float m_SwitchPhaseStartProgress;
        // Optional player-side gate for switch/fire input (IronRequiem.IWeaponSwitchGate); null keeps the template.
        IronRequiem.IWeaponSwitchGate m_SwitchGate;

        void Start()
        {
            ActiveWeaponIndex = -1;
            m_WeaponSwitchState = WeaponSwitchState.Down;

            m_InputHandler = GetComponent<PlayerInputHandler>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerInputHandler, PlayerWeaponsManager>(m_InputHandler, this,
                gameObject);

            m_PlayerCharacterController = GetComponent<PlayerCharacterController>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerCharacterController, PlayerWeaponsManager>(
                m_PlayerCharacterController, this, gameObject);

            m_MotionExtension = GetComponent<IPlayerMotionExtension>();
            m_SwitchGate = GetComponent<IronRequiem.IWeaponSwitchGate>();

            SetFov(DefaultFov);

            OnSwitchedToWeapon += OnWeaponSwitched;

            // Add starting weapons
            foreach (var weapon in StartingWeapons)
            {
                AddWeapon(weapon);
            }

            SwitchWeapon(true);
        }

        void Update()
        {
            // shoot handling
            WeaponController activeWeapon = GetActiveWeapon();

            if (activeWeapon != null && activeWeapon.IsReloading)
                return;

            // IRON REQUIEM arms-fix: no fire input to a weapon that a scripted hand-over is about to put away (and no
            // aim-down-sights left on from before the hand-over).
            if (m_SwitchGate != null && !m_SwitchGate.CanFireActiveWeapon) IsAiming = false;
            if (activeWeapon != null && m_WeaponSwitchState == WeaponSwitchState.Up &&
                (m_SwitchGate == null || m_SwitchGate.CanFireActiveWeapon))
            {
                if (!activeWeapon.AutomaticReload && activeWeapon.HasPhysicalBullets && m_InputHandler.GetReloadButtonDown() && activeWeapon.CurrentAmmoRatio < 1.0f)
                {
                    IsAiming = false;
                    activeWeapon.StartReloadAnimation();
                    return;
                }
                // handle aiming down sights
                IsAiming = m_InputHandler.GetAimInputHeld();

                // handle shooting
                bool hasFired = activeWeapon.HandleShootInputs(
                    m_InputHandler.GetFireInputDown(),
                    m_InputHandler.GetFireInputHeld(),
                    m_InputHandler.GetFireInputReleased());

                // Handle accumulating recoil
                if (hasFired)
                {
                    m_AccumulatedRecoil += Vector3.back * activeWeapon.RecoilForce;
                    m_AccumulatedRecoil = Vector3.ClampMagnitude(m_AccumulatedRecoil, MaxRecoilDistance);
                }
            }

            // weapon switch handling
            // IRON REQUIEM arms-fix: the optional gate refuses switch input while the hands are busy (execution).
            if (!IsAiming &&
                (activeWeapon == null || !activeWeapon.IsCharging) &&
                (m_WeaponSwitchState == WeaponSwitchState.Up || m_WeaponSwitchState == WeaponSwitchState.Down) &&
                (m_SwitchGate == null || m_SwitchGate.CanSwitchWeapon))
            {
                int switchWeaponInput = m_InputHandler.GetSwitchWeaponInput();
                if (switchWeaponInput != 0)
                {
                    bool switchUp = switchWeaponInput > 0;
                    SwitchWeapon(switchUp);
                }
                else
                {
                    switchWeaponInput = m_InputHandler.GetSelectWeaponInput();
                    if (switchWeaponInput != 0)
                    {
                        if (GetWeaponAtSlotIndex(switchWeaponInput - 1) != null)
                            SwitchToWeaponIndex(switchWeaponInput - 1);
                    }
                }
            }

            // Pointing at enemy handling
            IsPointingAtEnemy = false;
            if (activeWeapon)
            {
                if (Physics.Raycast(WeaponCamera.transform.position, WeaponCamera.transform.forward, out RaycastHit hit,
                    1000, -1, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<Health>() != null)
                    {
                        IsPointingAtEnemy = true;
                    }
                }
            }
        }


        // Update various animated features in LateUpdate because it needs to override the animated arm position
        void LateUpdate()
        {
            UpdateWeaponAiming();
            UpdateWeaponBob();
            UpdateWeaponRecoil();
            UpdateWeaponSwitching();

            // Set final weapon socket position based on all the combined animation influences
            WeaponParentSocket.localPosition =
                m_WeaponMainLocalPosition + m_WeaponBobLocalPosition + m_WeaponRecoilLocalPosition;
        }

        // Sets the FOV of the main camera and the weapon camera simultaneously
        public void SetFov(float fov)
        {
            m_PlayerCharacterController.PlayerCamera.fieldOfView = fov;
            WeaponCamera.fieldOfView = fov * WeaponFovMultiplier;
        }

        // Iterate on all weapon slots to find the next valid weapon to switch to
        public void SwitchWeapon(bool ascendingOrder)
        {
            int newWeaponIndex = -1;
            int closestSlotDistance = m_WeaponSlots.Length;
            for (int i = 0; i < m_WeaponSlots.Length; i++)
            {
                // If the weapon at this slot is valid, calculate its "distance" from the active slot index (either in ascending or descending order)
                // and select it if it's the closest distance yet
                if (i != ActiveWeaponIndex && GetWeaponAtSlotIndex(i) != null)
                {
                    int distanceToActiveIndex = GetDistanceBetweenWeaponSlots(ActiveWeaponIndex, i, ascendingOrder);

                    if (distanceToActiveIndex < closestSlotDistance)
                    {
                        closestSlotDistance = distanceToActiveIndex;
                        newWeaponIndex = i;
                    }
                }
            }

            // Handle switching to the new weapon index
            SwitchToWeaponIndex(newWeaponIndex);
        }

        // Switches to the given weapon index in weapon slots if the new index is a valid weapon that is different from our current one
        public void SwitchToWeaponIndex(int newWeaponIndex, bool force = false)
        {
            if (force || (newWeaponIndex != ActiveWeaponIndex && newWeaponIndex >= 0))
            {
                // IRON REQUIEM arms-fix: a request during a switch retargets or reverses it instead of restarting it.
                if (RetargetSwitch(newWeaponIndex))
                    return;

                GetActiveWeapon()?.CancelPendingShot();
                // Store data related to weapon switching animation
                m_WeaponSwitchNewWeaponIndex = newWeaponIndex;
                m_TimeStartedWeaponSwitch = Time.time;
                m_SwitchPhaseStartProgress = 0f;

                // Handle case of switching to a valid weapon for the first time (simply put it up without putting anything down first)
                if (GetActiveWeapon() == null)
                {
                    // IRON REQUIEM arms-fix: only the first weapon (nothing up yet) rises from the Down position. A weapon
                    // replacing one that was just removed (thrown, destroyed) starts where the socket already is.
                    // A template weapon (no self-animated switch) still rises from Down, so it never pops in at rest.
                    var incoming = GetWeaponAtSlotIndex(m_WeaponSwitchNewWeaponIndex);
                    bool selfAnimated = incoming && incoming.TryGetComponent(out IWeaponSwitchMotion incomingMotion) && incomingMotion.OverridesSwitchMotion;
                    if (m_WeaponSwitchState == WeaponSwitchState.Down || !selfAnimated)
                        m_WeaponMainLocalPosition = DownWeaponPosition.localPosition;
                    m_SwitchPhaseStartLocalPosition = m_WeaponMainLocalPosition;
                    m_WeaponSwitchState = WeaponSwitchState.PutUpNew;
                    ActiveWeaponIndex = m_WeaponSwitchNewWeaponIndex;

                    WeaponController newWeapon = GetWeaponAtSlotIndex(m_WeaponSwitchNewWeaponIndex);
                    if (OnSwitchedToWeapon != null)
                    {
                        OnSwitchedToWeapon.Invoke(newWeapon);
                    }
                }
                // otherwise, remember we are putting down our current weapon for switching to the next one
                else
                {
                    m_SwitchPhaseStartLocalPosition = m_WeaponMainLocalPosition;
                    m_WeaponSwitchState = WeaponSwitchState.PutDownPrevious;
                }
            }
        }

        // IRON REQUIEM arms-fix (19/11/2026): a (forced) request that arrives while a weapon is being put down or brought
        // up keeps the motion going instead of restarting it from the start of a phase (the old restart snapped the
        // socket back and made a forearm module replay its unlock). Returns false when no switch is in progress.
        bool RetargetSwitch(int newWeaponIndex)
        {
            if (GetActiveWeapon() == null)
                return false;
            if (m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious)
            {
                // back to the weapon still in hand: bring it up again from where it is; otherwise only the destination changes
                if (newWeaponIndex == ActiveWeaponIndex)
                    ReverseSwitchPhase(WeaponSwitchState.PutUpNew);
                else
                    m_WeaponSwitchNewWeaponIndex = newWeaponIndex;
                return true;
            }
            if (m_WeaponSwitchState == WeaponSwitchState.PutUpNew)
            {
                // already coming up: nothing to restart
                if (newWeaponIndex == ActiveWeaponIndex)
                    return true;
                GetActiveWeapon().CancelPendingShot();
                m_WeaponSwitchNewWeaponIndex = newWeaponIndex;
                ReverseSwitchPhase(WeaponSwitchState.PutDownPrevious);
                return true;
            }
            return false;
        }

        // Same weapon, opposite phase: what remained of the current phase becomes what is done of the reversed one, and
        // the socket moves on from its current position.
        void ReverseSwitchPhase(WeaponSwitchState reversed)
        {
            float phaseSeconds = PhaseSeconds();
            float progress = phaseSeconds > 0f ? Mathf.Clamp01((Time.time - m_TimeStartedWeaponSwitch) / phaseSeconds) : 1f;
            m_WeaponSwitchState = reversed;
            m_SwitchPhaseStartProgress = 1f - progress;
            m_SwitchPhaseStartLocalPosition = m_WeaponMainLocalPosition;
            m_TimeStartedWeaponSwitch = Time.time - m_SwitchPhaseStartProgress * phaseSeconds;
            SwitchPhaseProgress = m_SwitchPhaseStartProgress;
        }

        // IRON REQUIEM v5: finishes the current switch instantly (weapon already in hand, e.g. mounted by a station
        // sequence that animated the attachment itself), so the weapon does not drop and rise again.
        public void SnapActiveWeaponUp()
        {
            if (GetActiveWeapon() == null) return;
            if (m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious && m_WeaponSwitchNewWeaponIndex >= 0)
            {
                var previous = GetActiveWeapon(); if (previous) previous.ShowWeapon(false);
                ActiveWeaponIndex = m_WeaponSwitchNewWeaponIndex;
                var next = GetWeaponAtSlotIndex(ActiveWeaponIndex);
                // arms-fix: one OnSwitchedToWeapon (verified) whose OnWeaponSwitched listener shows the weapon; showing it
                // here as well played ChangeWeaponSfx twice. Shown directly only if no listener did it.
                OnSwitchedToWeapon?.Invoke(next);
                if (next && !next.IsWeaponActive) next.ShowWeapon(true);
            }
            m_WeaponMainLocalPosition = DefaultWeaponPosition.localPosition;
            m_WeaponSwitchState = WeaponSwitchState.Up;
        }

        public WeaponController HasWeapon(WeaponController weaponPrefab)
        {
            // Checks if we already have a weapon coming from the specified prefab
            for (var index = 0; index < m_WeaponSlots.Length; index++)
            {
                var w = m_WeaponSlots[index];
                if (w != null && w.SourcePrefab == weaponPrefab.gameObject)
                {
                    return w;
                }
            }

            return null;
        }

        // Updates weapon position and camera FoV for the aiming transition
        void UpdateWeaponAiming()
        {
            if (m_WeaponSwitchState == WeaponSwitchState.Up)
            {
                WeaponController activeWeapon = GetActiveWeapon();
                if (IsAiming && activeWeapon)
                {
                    m_WeaponMainLocalPosition = Vector3.Lerp(m_WeaponMainLocalPosition,
                        AimingWeaponPosition.localPosition + activeWeapon.AimOffset,
                        AimingAnimationSpeed * Time.deltaTime);
                    SetFov(Mathf.Lerp(m_PlayerCharacterController.PlayerCamera.fieldOfView,
                        activeWeapon.AimZoomRatio * DefaultFov, AimingAnimationSpeed * Time.deltaTime));
                }
                else
                {
                    m_WeaponMainLocalPosition = Vector3.Lerp(m_WeaponMainLocalPosition,
                        DefaultWeaponPosition.localPosition, AimingAnimationSpeed * Time.deltaTime);
                    SetFov(Mathf.Lerp(m_PlayerCharacterController.PlayerCamera.fieldOfView, DefaultFov,
                        AimingAnimationSpeed * Time.deltaTime));
                }
            }
        }

        // Updates the weapon bob animation based on character speed
        void UpdateWeaponBob()
        {
            // Sliding has no footstep sway; aiming, recoil and weapon switching remain independent.
            if (m_MotionExtension != null && m_MotionExtension.SuppressWeaponBob)
            {
                m_WeaponBobFactor = 0f;
                m_WeaponBobLocalPosition = Vector3.zero;
                m_LastCharacterPosition = m_PlayerCharacterController.transform.position;
                return;
            }
            if (Time.deltaTime > 0f)
            {
                Vector3 playerCharacterVelocity =
                    (m_PlayerCharacterController.transform.position - m_LastCharacterPosition) / Time.deltaTime;

                // calculate a smoothed weapon bob amount based on how close to our max grounded movement velocity we are
                float characterMovementFactor = 0f;
                if (m_PlayerCharacterController.IsGrounded)
                {
                    characterMovementFactor =
                        Mathf.Clamp01(playerCharacterVelocity.magnitude /
                                      (m_PlayerCharacterController.MaxSpeedOnGround *
                                       m_PlayerCharacterController.SprintSpeedModifier));
                }

                m_WeaponBobFactor =
                    Mathf.Lerp(m_WeaponBobFactor, characterMovementFactor, BobSharpness * Time.deltaTime);

                // Calculate vertical and horizontal weapon bob values based on a sine function
                float bobAmount = IsAiming ? AimingBobAmount : DefaultBobAmount;
                float frequency = BobFrequency;
                float hBobValue = Mathf.Sin(Time.time * frequency) * bobAmount * m_WeaponBobFactor;
                float vBobValue = ((Mathf.Sin(Time.time * frequency * 2f) * 0.5f) + 0.5f) * bobAmount *
                                  m_WeaponBobFactor;

                // Apply weapon bob
                m_WeaponBobLocalPosition.x = hBobValue;
                m_WeaponBobLocalPosition.y = Mathf.Abs(vBobValue);

                m_LastCharacterPosition = m_PlayerCharacterController.transform.position;
            }
        }

        // Updates the weapon recoil animation
        void UpdateWeaponRecoil()
        {
            // if the accumulated recoil is further away from the current position, make the current position move towards the recoil target
            if (m_WeaponRecoilLocalPosition.z >= m_AccumulatedRecoil.z * 0.99f)
            {
                m_WeaponRecoilLocalPosition = Vector3.Lerp(m_WeaponRecoilLocalPosition, m_AccumulatedRecoil,
                    RecoilSharpness * Time.deltaTime);
            }
            // otherwise, move recoil position to make it recover towards its resting pose
            else
            {
                m_WeaponRecoilLocalPosition = Vector3.Lerp(m_WeaponRecoilLocalPosition, Vector3.zero,
                    RecoilRestitutionSharpness * Time.deltaTime);
                m_AccumulatedRecoil = m_WeaponRecoilLocalPosition;
            }
        }

        // Updates the animated transition of switching weapons
        void UpdateWeaponSwitching()
        {
            // Calculate the time ratio (0 to 1) since weapon switch was triggered
            float switchingTimeFactor = 0f;
            float phaseSeconds = PhaseSeconds();
            if (phaseSeconds == 0f)
            {
                switchingTimeFactor = 1f;
            }
            else
            {
                switchingTimeFactor = Mathf.Clamp01((Time.time - m_TimeStartedWeaponSwitch) / phaseSeconds);
            }

            // Handle transiting to new switch state
            if (switchingTimeFactor >= 1f)
            {
                if (m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious)
                {
                    // Deactivate old weapon
                    WeaponController oldWeapon = GetWeaponAtSlotIndex(ActiveWeaponIndex);
                    if (oldWeapon != null)
                    {
                        oldWeapon.ShowWeapon(false);
                    }

                    ActiveWeaponIndex = m_WeaponSwitchNewWeaponIndex;
                    switchingTimeFactor = 0f;

                    // Activate new weapon
                    WeaponController newWeapon = GetWeaponAtSlotIndex(ActiveWeaponIndex);
                    if (OnSwitchedToWeapon != null)
                    {
                        OnSwitchedToWeapon.Invoke(newWeapon);
                    }

                    if (newWeapon)
                    {
                        m_TimeStartedWeaponSwitch = Time.time;
                        m_WeaponSwitchState = WeaponSwitchState.PutUpNew;
                        // arms-fix: the new weapon rises from where the put-down left the socket (Down for the template
                        // motion, the resting pose after a module that animated its own put-down)
                        m_SwitchPhaseStartLocalPosition = m_WeaponMainLocalPosition;
                        m_SwitchPhaseStartProgress = 0f;
                    }
                    else
                    {
                        // if new weapon is null, don't follow through with putting weapon back up
                        m_WeaponSwitchState = WeaponSwitchState.Down;
                    }
                }
                else if (m_WeaponSwitchState == WeaponSwitchState.PutUpNew)
                {
                    m_WeaponSwitchState = WeaponSwitchState.Up;
                }
            }

            SwitchPhaseProgress = m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious || m_WeaponSwitchState == WeaponSwitchState.PutUpNew ? switchingTimeFactor : 1f;
            // A weapon that animates its own switch (IWeaponSwitchMotion) keeps the socket at the resting pose.
            if (PhaseIsExternal())
            {
                m_WeaponMainLocalPosition = Vector3.Lerp(m_WeaponMainLocalPosition, DefaultWeaponPosition.localPosition, AimingAnimationSpeed * Time.deltaTime);
                return;
            }

            // Handle moving the weapon socket position for the animated weapon switching
            // IRON REQUIEM arms-fix: from the socket position at the start of the phase instead of a hard Default/Down
            // (a template weapon following a self-animated module no longer jumps to Down; a reversed phase goes on
            // from where it was). Identical to the template when a phase starts at Default/Down.
            float phaseFactor = m_SwitchPhaseStartProgress < 1f
                ? Mathf.Clamp01((switchingTimeFactor - m_SwitchPhaseStartProgress) / (1f - m_SwitchPhaseStartProgress))
                : 1f;
            if (SmoothWeaponSwitch)
                phaseFactor = phaseFactor * phaseFactor * (3f - 2f * phaseFactor);
            if (m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious)
            {
                m_WeaponMainLocalPosition = Vector3.Lerp(m_SwitchPhaseStartLocalPosition,
                    DownWeaponPosition.localPosition, phaseFactor);
            }
            else if (m_WeaponSwitchState == WeaponSwitchState.PutUpNew)
            {
                m_WeaponMainLocalPosition = Vector3.Lerp(m_SwitchPhaseStartLocalPosition,
                    DefaultWeaponPosition.localPosition, phaseFactor);
            }
        }

        // The weapon animating the current phase: the old one while putting down, the new one while bringing up.
        WeaponController PhaseWeapon() => m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious || m_WeaponSwitchState == WeaponSwitchState.PutUpNew ? GetActiveWeapon() : null;

        bool PhaseIsExternal()
        {
            var weapon = PhaseWeapon();
            return weapon && weapon.TryGetComponent(out IWeaponSwitchMotion motion) && motion.OverridesSwitchMotion;
        }

        float PhaseSeconds()
        {
            var weapon = PhaseWeapon();
            return weapon && weapon.TryGetComponent(out IWeaponSwitchMotion motion) && motion.OverridesSwitchMotion ? motion.SwitchPhaseSeconds : WeaponSwitchDelay;
        }

        // Adds a weapon to our inventory
        public bool AddWeapon(WeaponController weaponPrefab)
        {
            // if we already hold this weapon type (a weapon coming from the same source prefab), don't add the weapon
            if (HasWeapon(weaponPrefab) != null)
            {
                return false;
            }

            // search our weapon slots for the first free one, assign the weapon to it, and return true if we found one. Return false otherwise
            for (int i = 0; i < m_WeaponSlots.Length; i++)
            {
                // only add the weapon if the slot is free
                if (m_WeaponSlots[i] == null)
                {
                    // spawn the weapon prefab as child of the weapon socket
                    WeaponController weaponInstance = Instantiate(weaponPrefab, WeaponParentSocket);
                    weaponInstance.transform.localPosition = Vector3.zero;
                    weaponInstance.transform.localRotation = Quaternion.identity;

                    // Set owner to this gameObject so the weapon can alter projectile/damage logic accordingly
                    weaponInstance.Owner = gameObject;
                    weaponInstance.SourcePrefab = weaponPrefab.gameObject;
                    weaponInstance.ShowWeapon(false);

                    // Assign the first person layer to the weapon
                    int layerIndex =
                        Mathf.RoundToInt(Mathf.Log(FpsWeaponLayer.value,
                            2)); // This function converts a layermask to a layer index
                    foreach (Transform t in weaponInstance.gameObject.GetComponentsInChildren<Transform>(true))
                    {
                        t.gameObject.layer = layerIndex;
                    }

                    m_WeaponSlots[i] = weaponInstance;

                    if (OnAddedWeapon != null)
                    {
                        OnAddedWeapon.Invoke(weaponInstance, i);
                    }

                    return true;
                }
            }

            // Handle auto-switching to weapon if no weapons currently
            if (GetActiveWeapon() == null)
            {
                SwitchWeapon(true);
            }

            return false;
        }

        public bool RemoveWeapon(WeaponController weaponInstance) => RemoveWeapon(weaponInstance, -1);

        // IRON REQUIEM arms-fix (19/11/2026): switchToIndex >= 0 names the weapon that takes over when the removed one was
        // active (one switch, one OnSwitchedToWeapon), instead of the template auto-switch to the next slot followed by
        // a second, forced switch from the caller. -1 keeps the template behaviour.
        public bool RemoveWeapon(WeaponController weaponInstance, int switchToIndex)
        {
            // IRON REQUIEM v4: the permanent fist can never be lost or thrown.
            if (weaponInstance && weaponInstance.PermanentMelee) return false;
            // Look through our slots for that weapon
            for (int i = 0; i < m_WeaponSlots.Length; i++)
            {
                // when weapon found, remove it
                if (m_WeaponSlots[i] == weaponInstance)
                {
                    m_WeaponSlots[i] = null;

                    if (OnRemovedWeapon != null)
                    {
                        OnRemovedWeapon.Invoke(weaponInstance, i);
                    }

                    Destroy(weaponInstance.gameObject);

                    // Handle case of removing active weapon (switch to next weapon)
                    if (i == ActiveWeaponIndex)
                    {
                        if (switchToIndex >= 0 && switchToIndex != i && GetWeaponAtSlotIndex(switchToIndex) != null)
                            SwitchToWeaponIndex(switchToIndex);
                        else
                            SwitchWeapon(true);
                    }
                    // IRON REQUIEM arms-fix: the weapon being switched to was removed half-way: go to the requested
                    // weapon, or bring back the one still in hand, instead of finishing the switch on an empty slot.
                    else if (i == m_WeaponSwitchNewWeaponIndex && m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious)
                    {
                        SwitchToWeaponIndex(switchToIndex >= 0 && GetWeaponAtSlotIndex(switchToIndex) != null ? switchToIndex : ActiveWeaponIndex, true);
                    }

                    return true;
                }
            }

            return false;
        }

        public WeaponController GetActiveWeapon()
        {
            return GetWeaponAtSlotIndex(ActiveWeaponIndex);
        }

        public WeaponController GetWeaponAtSlotIndex(int index)
        {
            // find the active weapon in our weapon slots based on our active weapon index
            if (index >= 0 &&
                index < m_WeaponSlots.Length)
            {
                return m_WeaponSlots[index];
            }

            // if we didn't find a valid active weapon in our weapon slots, return null
            return null;
        }

        // Calculates the "distance" between two weapon slot indexes
        // For example: if we had 5 weapon slots, the distance between slots #2 and #4 would be 2 in ascending order, and 3 in descending order
        int GetDistanceBetweenWeaponSlots(int fromSlotIndex, int toSlotIndex, bool ascendingOrder)
        {
            int distanceBetweenSlots = 0;

            if (ascendingOrder)
            {
                distanceBetweenSlots = toSlotIndex - fromSlotIndex;
            }
            else
            {
                distanceBetweenSlots = -1 * (toSlotIndex - fromSlotIndex);
            }

            if (distanceBetweenSlots < 0)
            {
                distanceBetweenSlots = m_WeaponSlots.Length + distanceBetweenSlots;
            }

            return distanceBetweenSlots;
        }

        void OnWeaponSwitched(WeaponController newWeapon)
        {
            if (newWeapon != null)
            {
                newWeapon.ShowWeapon(true);
            }
        }
    }
}
