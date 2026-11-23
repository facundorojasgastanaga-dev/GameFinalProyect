using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    // Visual overhaul (07/11/2026): forearm weapon module presentation.
    // Switching (replaces the template lower/raise for module weapons, via IWeaponSwitchMotion):
    //   put-down phase  0-30 %  lock: joint plates open, "clac", steam from the port, status lights go out in reverse
    //                   30-100% the module separates from the forearm and leaves the view
    //   bring-up phase  0-70 %  the new module comes in and seats (ease-out-back)
    //                   70 %    hard dock: thud + small camera kick, joints close
    //                   75-100% status lights come on in sequence
    // The weapon root, muzzle and rear "Attachment" never move, so the fitted forearm stays on screen; only the
    // module visual moves. Shooting stays blocked by PlayerWeaponsManager for the whole switch (state != Up).
    // Firing rings: emissive boost while the weapon fires, decays after. MaterialPropertyBlock only (no material
    // instances); blocks are written only when a value changes. No allocation per frame.
    [RequireComponent(typeof(WeaponController))]
    public sealed class ArmModuleVisual : MonoBehaviour, IWeaponSwitchMotion
    {
        [SerializeField] Transform moduleRoot;
        [SerializeField] Transform[] joints = new Transform[0];
        [SerializeField] Vector3 jointOpenEuler = new Vector3(0, 0, 28);
        [SerializeField] Renderer[] sequenceLights = new Renderer[0];
        [SerializeField] Renderer[] firingRings = new Renderer[0];
        [SerializeField, Range(.15f, .4f)] float phaseSeconds = .3f;
        [SerializeField] Vector3 ejectOffset = new Vector3(.32f, -.55f, -.05f);
        [SerializeField] Vector3 ejectEuler = new Vector3(25, 35, -40);
        [SerializeField] Vector3 insertOffset = new Vector3(.28f, -.5f, -.12f);
        [SerializeField] Vector3 insertEuler = new Vector3(-20, -30, 30);
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip unlockClip, dockClip, ventClip;
        [SerializeField] ParticleSystem unlockSteam;
        [SerializeField, Min(0)] float ringBoostPerShot = .9f, ringMaxBoost = 6f, ringDecayPerSecond = 5f;
        [Tooltip("Fuel-cell tubes sink this far into the body (cell parent space) while the module travels.")]
        [SerializeField] Vector3 cellSink = new Vector3(0, -.2f, 0);

        public bool OverridesSwitchMotion => isActiveAndEnabled && moduleRoot;
        public float SwitchPhaseSeconds => phaseSeconds;
        public Transform ModuleRoot => moduleRoot;
        public Renderer[] SequenceLights => sequenceLights;
        public Renderer[] FiringRings => firingRings;
        public float RingBoost => ringBoost;
        public int UnlockCount { get; private set; }
        public int DockCount { get; private set; }
        public float LightsOn { get; private set; } = 1f;

        static readonly int PanelBoost = Shader.PropertyToID("_PanelBoost");
        static MaterialPropertyBlock block;
        WeaponController weapon; PlayerWeaponsManager manager; PlayerCharacterController player;
        Vector3 restPosition; Quaternion restRotation; Quaternion[] jointRest;
        float ringBoost, appliedRing = float.NaN, appliedLights = float.NaN;
        bool unlocked, docked, posed;
        // Visual pass F7: template pieces (fuel-cell tubes, pistol body, supports) live beside moduleRoot, not under it,
        // so they stayed on the forearm while the module left/arrived and the tubes looked like they loaded first.
        // They now ride with the module pose; the tubes also sink inside it during the travel. Followers are the
        // siblings of moduleRoot that render something, except the forearm attachment, the muzzle and port steam.
        Transform[] followers = new Transform[0], cells = new Transform[0];
        Vector3[] followerBasePos, cellBase, followerWrittenPos, cellWritten; Quaternion[] followerBaseRot, followerWrittenRot;
        bool following;
        PlayerWeaponsManager.WeaponSwitchState lastState = PlayerWeaponsManager.WeaponSwitchState.Down;
        // Arms-fix (19/11/2026): PlayerWeaponsManager now reverses a switch that is interrupted (e.g. E pickup while this
        // module docks) instead of restarting it. A module that never docked does not replay unlock/vent/steam, and
        // the pose it had is carried into the reversed phase and faded out, so it never jumps.
        bool wasMine, carryPending; float carryStart = -1;
        Vector3 carryPos, carryDeltaPos; Quaternion carryRot = Quaternion.identity, carryDeltaRot = Quaternion.identity;
        const float CarrySpan = .3f; // phase progress over which the carried pose fades out

        public void Configure(Transform root, Transform[] jointPlates, Renderer[] lights, Renderer[] rings, AudioSource source, AudioClip unlock, AudioClip dock, AudioClip vent, ParticleSystem steam)
        { moduleRoot = root; joints = jointPlates; sequenceLights = lights; firingRings = rings; audioSource = source; unlockClip = unlock; dockClip = dock; ventClip = vent; unlockSteam = steam; }

        void Awake()
        {
            weapon = GetComponent<WeaponController>();
            if (moduleRoot) { restPosition = moduleRoot.localPosition; restRotation = moduleRoot.localRotation; }
            jointRest = new Quaternion[joints.Length];
            for (int i = 0; i < joints.Length; i++) if (joints[i]) jointRest[i] = joints[i].localRotation;
            if (block == null) block = new MaterialPropertyBlock();
            CollectFollowers();
        }

        void CollectFollowers()
        {
            var parent = moduleRoot ? moduleRoot.parent : null; if (!parent) return;
            var muzzle = weapon.WeaponMuzzle;
            int n = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1) { followers = new Transform[n]; n = 0; }
                foreach (Transform c in parent)
                {
                    if (c == moduleRoot || c == muzzle || (muzzle && muzzle.IsChildOf(c))) continue;
                    if (c.name.Contains("Attachment") || c.name.Contains("Steam")) continue;
                    if (!c.GetComponentInChildren<MeshRenderer>(true)) continue;
                    if (pass == 1) followers[n] = c; n++;
                }
            }
            followerBasePos = new Vector3[n]; followerWrittenPos = new Vector3[n]; followerBaseRot = new Quaternion[n]; followerWrittenRot = new Quaternion[n];
            var handler = GetComponent<WeaponFuelCellHandler>();
            if (handler && handler.FuelCells != null)
            {
                n = 0; foreach (var c in handler.FuelCells) if (c) n++;
                cells = new Transform[n]; n = 0;
                foreach (var c in handler.FuelCells) if (c) cells[n++] = c.transform;
            }
            cellBase = new Vector3[cells.Length]; cellWritten = new Vector3[cells.Length];
        }

        void OnEnable() { if (weapon) weapon.OnShoot += OnShot; }
        void OnDisable() { if (weapon) weapon.OnShoot -= OnShot; }
        void OnShot() => ringBoost = Mathf.Min(ringMaxBoost, ringBoost + ringBoostPerShot);

        void LateUpdate()
        {
            if (!manager && weapon.Owner) { manager = weapon.Owner.GetComponent<PlayerWeaponsManager>(); player = weapon.Owner.GetComponent<PlayerCharacterController>(); }
            // Firing rings: keep glowing while shots keep coming, fade when the trigger is released.
            ringBoost = Mathf.Max(0, ringBoost - ringDecayPerSecond * Time.deltaTime);
            if (!manager || !moduleRoot) { ApplyRings(); return; }
            var state = manager.SwitchState; float t = manager.SwitchPhaseProgress;
            bool mine = manager.GetActiveWeapon() == weapon;
            if (state != lastState)
            {
                // reversed on this module (same weapon, phase flipped): carry the current pose into the new phase
                bool reversed = mine && wasMine && posed && IsPhase(state) && IsPhase(lastState);
                if (state == PlayerWeaponsManager.WeaponSwitchState.PutDownPrevious)
                    unlocked = reversed && lastState == PlayerWeaponsManager.WeaponSwitchState.PutUpNew && !docked; // never locked: nothing to unlock
                if (state == PlayerWeaponsManager.WeaponSwitchState.PutUpNew) docked = false;
                if (reversed) { carryPos = moduleRoot.localPosition; carryRot = moduleRoot.localRotation; carryStart = t; carryPending = true; }
                else carryStart = -1;
                lastState = state;
            }
            wasMine = mine;
            if (mine && state == PlayerWeaponsManager.WeaponSwitchState.PutDownPrevious) Eject(t);
            else if (mine && state == PlayerWeaponsManager.WeaponSwitchState.PutUpNew) Dock(t);
            else if (posed) { carryStart = -1; Rest(); }
            ApplyRings();
        }

        static bool IsPhase(PlayerWeaponsManager.WeaponSwitchState s) =>
            s == PlayerWeaponsManager.WeaponSwitchState.PutDownPrevious || s == PlayerWeaponsManager.WeaponSwitchState.PutUpNew;

        // Writes the phase pose, plus the fading difference to the pose the module had when its phase was reversed.
        void PoseModule(Vector3 position, Quaternion rotation, float t)
        {
            if (carryStart >= 0)
            {
                if (carryPending) { carryPending = false; carryDeltaPos = carryPos - position; carryDeltaRot = carryRot * Quaternion.Inverse(rotation); }
                float span = Mathf.Max(.001f, Mathf.Min(CarrySpan, 1 - carryStart)); // always faded out by the phase end
                float w = 1 - Smooth(Mathf.Clamp01((t - carryStart) / span));
                position += carryDeltaPos * w; rotation = Quaternion.Slerp(Quaternion.identity, carryDeltaRot, w) * rotation;
                if (w <= 0) carryStart = -1;
            }
            moduleRoot.localPosition = position; moduleRoot.localRotation = rotation;
        }

        void Eject(float t)
        {
            posed = true;
            if (!unlocked) { unlocked = true; UnlockCount++; Play(unlockClip, .9f); Play(ventClip, .45f); if (unlockSteam) { unlockSteam.Clear(true); unlockSteam.Play(true); } }
            float lockT = Mathf.Clamp01(t / .3f), u = Mathf.Clamp01((t - .3f) / .7f), e = u * u;
            SetJoints(Smooth(lockT));
            SetLights(1 - lockT);
            // Lock: a short shudder in place; then a small pull off the port followed by the exit arc.
            float shudder = lockT < 1 ? Mathf.Sin(lockT * 60f) * .0025f * (1 - lockT) : 0;
            var pull = new Vector3(0, -.015f, -.04f) * Smooth(Mathf.Clamp01(u * 3));
            PoseModule(restPosition + new Vector3(shudder, 0, 0) + pull + ejectOffset * e,
                restRotation * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(ejectEuler), e), t);
            Follow(Smooth(lockT));                                       // tubes retract into the body during the lock
        }

        void Dock(float t)
        {
            posed = true;
            float u = Mathf.Clamp01(t / .7f), k = EaseOutBack(u);
            PoseModule(restPosition + insertOffset * (1 - k),
                restRotation * Quaternion.SlerpUnclamped(Quaternion.Euler(insertEuler), Quaternion.identity, k), t);
            Follow(1 - Smooth(Mathf.Clamp01((t - .62f) / .3f)));         // tubes come out as the module docks
            SetJoints(t < .7f ? 1 : 1 - Smooth(Mathf.Clamp01((t - .7f) / .12f)));
            if (t >= .7f && !docked)
            {
                docked = true; DockCount++; Play(dockClip, 1f);
                if (player && player.PlayerCamera) DistanceCameraImpulse.Kick(player.PlayerCamera, new Vector3(-.9f, .25f, -.3f), new Vector3(0, -.004f, -.012f));
            }
            SetLights(Mathf.Clamp01((t - .75f) / .25f));
        }

        void Rest()
        {
            posed = false;
            moduleRoot.localPosition = restPosition; moduleRoot.localRotation = restRotation;
            Unfollow();
            SetJoints(0); SetLights(1);
        }

        // Puts every follower at its rest pose offset by the module's current offset from rest. Rest poses are
        // re-read whenever another script wrote the transform since our last write (fuel-cell handler, bank
        // reload), so nothing accumulates and those scripts keep owning their motion.
        void Follow(float sink)
        {
            var parent = moduleRoot.parent;
            Quaternion delta = moduleRoot.localRotation * Quaternion.Inverse(restRotation);
            for (int i = 0; i < cells.Length; i++)
            {
                var c = cells[i]; if (!c) continue;
                if (!following || c.localPosition != cellWritten[i]) cellBase[i] = c.localPosition;
                c.localPosition = cellWritten[i] = cellBase[i] + cellSink * sink;
            }
            for (int i = 0; i < followers.Length; i++)
            {
                var f = followers[i]; if (!f) continue;
                if (!following || f.localPosition != followerWrittenPos[i]) followerBasePos[i] = f.localPosition;
                if (!following || f.localRotation != followerWrittenRot[i]) followerBaseRot[i] = f.localRotation;
                // Same parent as moduleRoot: rotate the rest offset about the module rest pivot, then translate.
                f.localPosition = followerWrittenPos[i] = moduleRoot.localPosition + delta * (followerBasePos[i] - restPosition);
                f.localRotation = followerWrittenRot[i] = delta * followerBaseRot[i];
            }
            following = true;
        }

        void Unfollow()
        {
            if (!following) return;
            for (int i = 0; i < cells.Length; i++) if (cells[i] && cells[i].localPosition == cellWritten[i]) cells[i].localPosition = cellBase[i];
            for (int i = 0; i < followers.Length; i++)
            {
                var f = followers[i]; if (!f) continue;
                if (f.localPosition == followerWrittenPos[i]) f.localPosition = followerBasePos[i];
                if (f.localRotation == followerWrittenRot[i]) f.localRotation = followerBaseRot[i];
            }
            following = false;
        }

        void SetJoints(float open)
        {
            for (int i = 0; i < joints.Length; i++)
                if (joints[i]) joints[i].localRotation = jointRest[i] * Quaternion.Euler(jointOpenEuler * (i % 2 == 0 ? open : -open));
        }

        // Lights come on (or go out) one after another: light i is lit when progress passes i / count.
        void SetLights(float progress)
        {
            LightsOn = progress;
            if (Mathf.Approximately(progress, appliedLights) || sequenceLights.Length == 0) return;
            appliedLights = progress;
            for (int i = 0; i < sequenceLights.Length; i++)
            {
                var r = sequenceLights[i]; if (!r) continue;
                float on = Mathf.Clamp01((progress * sequenceLights.Length) - i);
                r.GetPropertyBlock(block); block.SetFloat(PanelBoost, on - 1f); r.SetPropertyBlock(block); // -1 = off, 0 = normal
            }
        }

        void ApplyRings()
        {
            if (Mathf.Abs(ringBoost - appliedRing) < .01f || firingRings.Length == 0) return;
            appliedRing = ringBoost;
            foreach (var r in firingRings) { if (!r) continue; r.GetPropertyBlock(block); block.SetFloat(PanelBoost, ringBoost); r.SetPropertyBlock(block); }
        }

        void Play(AudioClip clip, float volume) { if (clip && audioSource && audioSource.isActiveAndEnabled) audioSource.PlayOneShot(clip, volume); }
        static float Smooth(float x) => x * x * (3 - 2 * x);
        static float EaseOutBack(float x) { const float c1 = 1.70158f, c3 = c1 + 1; float a = x - 1; return 1 + c3 * a * a * a + c1 * a * a; }
    }
}
