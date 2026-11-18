using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    /// <summary>Sprite-only presentation of UNIDAD 01. Never changes gameplay.</summary>
    public sealed class RobotPortraitGraphic : UnityEngine.UI.Image
    {
        public enum Reaction { Idle, Attack, Overdrive, Critical, Impact, Dead, Charging }
        [SerializeField] RobotPortraitSprites portraits;
        [Header("Gameplay yaw (degrees/second)")]
        [SerializeField, Min(1)] float gazeEnterSpeed = 20;
        [SerializeField, Min(0)] float gazeExitSpeed = 7;
        [SerializeField, Range(.02f, .2f)] float gazeSmoothing = .055f;
        [SerializeField, Range(.15f, .25f)] float gazeReturnDelay = .2f;
        [Header("Discontinuities reset the look sample")]
        [SerializeField, Min(1)] float teleportDistance = 1.5f;
        [SerializeField, Range(45, 179)] float maximumYawStep = 75;
        [SerializeField, Min(180)] float maximumYawSpeed = 1800;
        Health health;
        PlayerWeaponsManager inventory;
        PlayerCharacterController player;
        AdvancedPlayerMovement movement;
        StolenWeaponCarrier carrier;
        ProtagonistMelee melee;
        WeaponController weapon;
        Camera sampledCamera;
        RobotPortraitOverlay overlay;
        Vector2 restPosition;
        Vector3 previousPosition;
        float clock, impactUntil, attackUntil, nextAttack, nextSpark;
        float previousYaw, yawSpeed, lastTurn, lookClock;
        int observedMeleeSerial = -1, gaze;
        bool meleeHitObserved, latchedDead, subscribed, hasLookSample;
        static RobotPortraitSprites cachedPortraits;
        public Reaction CurrentReaction { get; private set; }
        public RobotPortraitSprites.Pose CurrentPose { get; private set; }
        public int DamageLevel { get; private set; }
        public int GazeDirection => gaze;
        public float SmoothedYawSpeed => yawSpeed;
        public float ChargeAmount { get; private set; }
        public bool OverdriveActive { get; private set; }
        public float ReactionTime => clock;
        public int SpriteChanges { get; private set; }
        public RobotPortraitOverlay Overlay => overlay;

        protected override void Awake()
        {
            base.Awake(); raycastTarget = false; preserveAspect = true; type = Type.Simple;
            restPosition = rectTransform.anchoredPosition;
            if (!portraits)
            {
                if (!cachedPortraits) cachedPortraits = Resources.Load<RobotPortraitSprites>("IronRequiem/RobotPortraitV03");
                portraits = cachedPortraits;
            }
            // One child on the existing portrait, created once. No extra Canvas, camera, texture or material.
            var child = new GameObject("PortraitEnergyAndSparks", typeof(RectTransform), typeof(CanvasRenderer));
            child.layer = gameObject.layer;
            var rect = (RectTransform)child.transform; rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            overlay = child.AddComponent<RobotPortraitOverlay>();
            ResetReactions();
        }
        public void Bind(Health owner, PlayerWeaponsManager weapons = null,
            StolenWeaponCarrier modules = null, ProtagonistMelee fists = null)
        {
            Unbind(); health = owner; inventory = weapons; carrier = modules; melee = fists;
            player = owner ? owner.GetComponent<PlayerCharacterController>() : null;
            movement = owner ? owner.GetComponent<AdvancedPlayerMovement>() : null;
            ResetReactions(); Subscribe();
        }
        protected override void OnEnable() { base.OnEnable(); ResetLookSample(); Subscribe(); }
        protected override void OnDisable()
        {
            Unsubscribe(); ResetLookSample();
            if (overlay) overlay.Clear();
            rectTransform.anchoredPosition = restPosition; base.OnDisable();
        }
        protected override void OnDestroy() { Unbind(); base.OnDestroy(); }
        void Subscribe()
        {
            if (subscribed || !isActiveAndEnabled) return;
            subscribed = true;
            if (health) { health.OnDamaged += Damaged; health.OnDie += Died; }
            if (carrier) carrier.Executed += Executed;
            if (inventory) { inventory.OnSwitchedToWeapon += BindWeapon; BindWeapon(inventory.GetActiveWeapon()); }
        }
        void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;
            if (health) { health.OnDamaged -= Damaged; health.OnDie -= Died; }
            if (carrier) carrier.Executed -= Executed;
            if (inventory) inventory.OnSwitchedToWeapon -= BindWeapon;
            BindWeapon(null);
        }
        void Unbind()
        {
            Unsubscribe(); health = null; inventory = null; carrier = null; melee = null;
            player = null; movement = null; ResetLookSample();
        }
        void BindWeapon(WeaponController next)
        {
            if (weapon == next) return;
            if (weapon) weapon.OnShoot -= Shot;
            weapon = next;
            attackUntil = nextAttack = 0;
            // Accepted shots, including the sustained stolen saw. Never driven by raw input.
            if (weapon) weapon.OnShoot += Shot;
        }
        void Damaged(float amount, GameObject source)
        {
            if (amount > 0 && Time.timeScale > 0 && !latchedDead) impactUntil = clock + .2f;
        }
        void Died() { latchedDead = true; }
        void Shot() { Attack(false); }
        void Executed(EnemyExecutionSource enemy, Vector3 point) { Attack(true); }
        void Attack(bool execution)
        {
            if (Time.timeScale <= 0 || latchedDead) return;
            // Rapid fire cannot extend the expression forever. Leave a visible rest gap.
            if (!execution && clock < nextAttack) return;
            attackUntil = Mathf.Max(attackUntil, clock + (execution ? .5f : .14f));
            nextAttack = attackUntil + .14f;
        }
        /// <summary>Reset transient state on respawn or when binding a new player.</summary>
        public void ResetReactions()
        {
            clock = lookClock = 0; impactUntil = attackUntil = nextAttack = nextSpark = 0;
            observedMeleeSerial = melee ? melee.AttackSerial : -1;
            meleeHitObserved = melee && melee.LastHitCount > 0; latchedDead = false;
            DamageLevel = health ? Level(health.GetRatio()) : 0;
            ChargeAmount = 0; OverdriveActive = false;
            CurrentReaction = Reaction.Idle; CurrentPose = RobotPortraitSprites.Pose.Front;
            rectTransform.anchoredPosition = restPosition; color = Color.white;
            ResetLookSample();
            if (overlay) overlay.Clear(true);
            Show(portraits ? portraits.Get(DamageLevel, CurrentPose) : null);
        }
        public static int Level(float ratio) => ratio >= .75f ? 0 : ratio >= .5f ? 1 : ratio >= .20f ? 2 : 3;

        /// <summary>May also be called by a future explicit teleport notification; no gameplay mutation.</summary>
        public void ResetLookSample()
        {
            hasLookSample = false; sampledCamera = null; yawSpeed = 0; gaze = 0; lastTurn = lookClock;
        }
        void SampleGameplayYaw(float dt)
        {
            var camera = player ? player.PlayerCamera : null;
            if (!player || !camera || !player.isActiveAndEnabled || !camera.gameObject.activeInHierarchy ||
                PlayerInputLock.Locked || (movement && movement.IsScripted) || (carrier && carrier.ExecutionInProgress))
            { ResetLookSample(); return; }
            // PlayerCharacterController rotates this root for horizontal look. Camera-local yaw includes recoil/bob.
            float yaw = player.transform.eulerAngles.y;
            Vector3 position = player.transform.position;
            if (!hasLookSample || camera != sampledCamera || dt > .25f)
            { SeedLook(camera, yaw, position); return; }
            float delta = Mathf.DeltaAngle(previousYaw, yaw);
            float speed = delta / Mathf.Max(.0001f, dt);
            bool discontinuity = (position - previousPosition).sqrMagnitude > teleportDistance * teleportDistance ||
                Mathf.Abs(delta) > maximumYawStep || Mathf.Abs(speed) > maximumYawSpeed;
            previousYaw = yaw; previousPosition = position;
            if (discontinuity) { SeedLook(camera, yaw, position); return; }
            yawSpeed = Mathf.Lerp(yawSpeed, speed, 1 - Mathf.Exp(-dt / gazeSmoothing));
            float exit = Mathf.Min(gazeExitSpeed, gazeEnterSpeed * .8f);
            if (Mathf.Abs(speed) > exit) lastTurn = lookClock;
            if (yawSpeed > gazeEnterSpeed) gaze = 1;
            else if (yawSpeed < -gazeEnterSpeed) gaze = -1;
            if (Mathf.Abs(speed) <= exit && lookClock - lastTurn >= gazeReturnDelay && Mathf.Abs(yawSpeed) < gazeEnterSpeed)
                gaze = 0;
        }
        void SeedLook(Camera camera, float yaw, Vector3 position)
        {
            sampledCamera = camera; previousYaw = yaw; previousPosition = position;
            hasLookSample = true; yawSpeed = 0; gaze = 0; lastTurn = lookClock;
        }
        void LateUpdate()
        {
            if (!health || !portraits) return;
            if (health.CurrentHealth <= 0) latchedDead = true;
            if (Time.timeScale <= 0)
            {
                ResetLookSample(); if (overlay) overlay.Clear();
                if (latchedDead)
                {
                    CurrentReaction = Reaction.Dead; rectTransform.anchoredPosition = restPosition;
                    color = Color.white; Show(portraits.Offline);
                }
                return;
            }
            float dt = Time.deltaTime; clock += dt;
            // Death stays latched even if a late heal arrives. Respawn binds a new owner or explicitly resets.
            if (health.CurrentHealth <= 0) latchedDead = true;
            if (inventory) BindWeapon(inventory.GetActiveWeapon());
            if (melee)
            {
                if (observedMeleeSerial != melee.AttackSerial)
                { observedMeleeSerial = melee.AttackSerial; meleeHitObserved = false; }
                // Real collision result; a missed punch never reacts.
                if (!meleeHitObserved && melee.LastHitCount > 0)
                { meleeHitObserved = true; Attack(false); }
            }
            // Look follows real camera motion even during melee slow motion; presentation timers remain game-time based.
            float lookDelta = Time.unscaledDeltaTime; lookClock += lookDelta;
            SampleGameplayYaw(lookDelta);
            DamageLevel = Level(health.GetRatio());
            bool charging = melee && melee.IsCharging;
            ChargeAmount = charging ? Mathf.Clamp01(melee.ChargeLevel) : 0;
            var module = carrier ? carrier.Held : null;
            OverdriveActive = module && module.Overdrive && module.Overdrive.IsArmed;
            CurrentReaction = latchedDead ? Reaction.Dead : clock < impactUntil ? Reaction.Impact :
                charging ? Reaction.Charging : clock < attackUntil ? Reaction.Attack :
                OverdriveActive ? Reaction.Overdrive : DamageLevel == 3 ? Reaction.Critical : Reaction.Idle;
            CurrentPose = gaze < 0 ? RobotPortraitSprites.Pose.Left : gaze > 0 ? RobotPortraitSprites.Pose.Right : RobotPortraitSprites.Pose.Front;
            Sprite next;
            if (CurrentReaction == Reaction.Dead) next = portraits.Offline;
            else if (CurrentReaction == Reaction.Impact)
            { CurrentPose = RobotPortraitSprites.Pose.Impact; next = portraits.Get(DamageLevel, CurrentPose); }
            else if (CurrentReaction == Reaction.Charging)
            {
                if (gaze == 0) CurrentPose = RobotPortraitSprites.Pose.Charge;
                next = portraits.Get(DamageLevel, CurrentPose);
            }
            else if (CurrentReaction == Reaction.Attack && DamageLevel == 0)
            { CurrentPose = RobotPortraitSprites.Pose.Front; next = portraits.Attack; }
            else if (CurrentReaction == Reaction.Overdrive && DamageLevel == 0 && gaze == 0)
                next = portraits.Overdrive;
            else next = portraits.Get(DamageLevel, CurrentPose);
            Show(next);
            float brightness = DamageLevel == 3 && !latchedDead ?
                .94f + .03f * Mathf.Sin(clock * 7) + .03f * Mathf.Sin(clock * 17.3f) : 1;
            var tint = new Color(brightness, brightness, brightness, 1);
            if (color != tint) color = tint;
            Vector2 offset = CurrentReaction == Reaction.Impact ?
                new Vector2(Mathf.Sin(clock * 85f), Mathf.Cos(clock * 67f)) * 1.5f : Vector2.zero;
            if (rectTransform.anchoredPosition != restPosition + offset) rectTransform.anchoredPosition = restPosition + offset;
            if (latchedDead) { overlay.Clear(); ResetLookSample(); return; }
            float energy = CurrentReaction == Reaction.Charging ? (.12f + .65f * ChargeAmount) * (.9f + .1f * Mathf.Sin(clock * 8)) :
                OverdriveActive ? .28f + .08f * Mathf.Sin(clock * 4) : CurrentReaction == Reaction.Attack && DamageLevel > 0 ? .35f : 0;
            if (DamageLevel == 0 && overlay.AliveCount > 0) overlay.Clear();
            overlay.Step(dt); overlay.Present(CurrentPose, energy, DamageLevel);
            // A bounded burst on entry, then at most once per cooldown while looking into the fracture or charging.
            bool sparkDirection = gaze > 0 && CurrentPose == RobotPortraitSprites.Pose.Right;
            bool sparkCharge = charging && DamageLevel >= 2 && CurrentReaction == Reaction.Charging;
            if (DamageLevel > 0 && (sparkDirection || sparkCharge) && clock >= nextSpark)
            {
                overlay.Burst(DamageLevel);
                nextSpark = clock + (DamageLevel == 1 ? 1.4f : DamageLevel == 2 ? .8f : .48f);
            }
        }
        void Show(Sprite next)
        {
            if (sprite == next) return;
            sprite = next; SpriteChanges++;
        }
    }
}
