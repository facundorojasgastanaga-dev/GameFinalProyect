using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    /// Map01 interior shutter (class P: arena entrance portal, alarm wings, supply cage) and, in F2, the boss gate B as a portal.
    /// Spec: Docs/IronRequiem/Map01/04_PUERTAS.md §P1.5, §P3.5 (AT-4 + [cambio B]) and §Q2 (R-P1…R-P5).
    /// It only OBSERVES public state (CampaignRoom.Completed, CombatTwoRoomsLab.WaveIndex/Living/FirstRoomBounds) and moves its door
    /// exclusively through AirlockDoorMotion.Set(this, door, closed): an open door ends inactive without collision, a closing door has
    /// collision from the first frame. The door root must be in its closed pose when authored (active = closed, inactive = open).
    /// Anti-trap (never forces): it closes only when (a) the opening is clear of the player and of anything with Health, (b) the player
    /// is outside the sealed volume, (c) no living enemy of the rounds is inside the sealed volume and, for portals, (d) the player is
    /// inside the arena bounds and at least minPlayerDistance away from the sealed volume. Otherwise it retries every 0.25 s.
    [DisallowMultipleComponent]
    public sealed class ArenaShutter : MonoBehaviour
    {
        [Tooltip("Door root (child): Renderer + solid BoxCollider + NavMeshObstacle (carve). Authored in its closed pose.")]
        [SerializeField] GameObject door;
        [Tooltip("Round index (WaveIndex) that closes the shutter; -1 = never closes.")]
        [SerializeField] int closeOnWave = 0;
        [Tooltip("Round index that opens it for good; -1 = opens when the room is Completed.")]
        [SerializeField] int openOnWave = -1;
        [Tooltip("Starts closed (supply cage). Otherwise the door is authored inactive (open).")]
        [SerializeField] bool startClosed;
        [Tooltip("Portal / boss gate: closes only with the player inside the arena bounds and away from the sealed volume.")]
        [SerializeField] bool requirePlayerInArena = true;
        [SerializeField, Min(0)] float minPlayerDistance = 4f;
        [SerializeField, Min(0)] float warningSeconds = .5f;
        [Tooltip("World-space volume left behind the shutter (vestibule, landing, wing, cage). Never closes with the player or an enemy inside.")]
        [SerializeField] Bounds sealedVolume = new Bounds(Vector3.zero, Vector3.zero);
        [Tooltip("World-space opening (yaw 0, axis aligned) tested for overlaps before closing.")]
        [SerializeField] Vector3 openingCenter, openingHalfExtents = Vector3.one;
        [Tooltip("World bounds of the arena, used only when the room has no CombatTwoRoomsLab.")]
        [SerializeField] Bounds fallbackArena = new Bounds(Vector3.zero, Vector3.zero);
        [Header("Warning before closing (presentation only)")]
        [SerializeField] Renderer[] warningLamps = new Renderer[0];
        [SerializeField] AudioSource warningAudio;
        [SerializeField] AudioClip warningClip;

        const float RetrySeconds = .25f, Clearance = .35f, BlinkHz = 2f; // 2 Hz blink: below the 3 flashes/s photosensitivity limit
        static readonly Collider[] Hits = new Collider[16];
        enum ShutterState { Open, Warning, Closed, Done }

        ShutterState state;
        float nextTry;
        bool lampsOn, lampsApplied, warnedMissingRoom;
        CampaignRoom room;
        CombatTwoRoomsLab rounds;
        HashSet<Health> livingSet;
        IReadOnlyCollection<Health> living;
        PlayerCharacterController player;
        CombatLabMenu menu;

        public bool Sealed => state == ShutterState.Closed;
        public bool Warning => state == ShutterState.Warning;
        public bool Finished => state == ShutterState.Done;
        public GameObject Door => door;
        public Bounds SealedVolume => sealedVolume;

        /// Editor authoring (Map01 DoorAuthoring). Positions are world space (Map01 rooms use yaw 0).
        public void Configure(GameObject doorRoot, int closeWave, int openWave, bool closedAtStart, bool portal, float playerDistance, float warning,
            Bounds sealedWorld, Vector3 openingWorldCenter, Vector3 openingWorldHalfExtents, Bounds arenaWorld, Renderer[] lamps, AudioSource audioSource, AudioClip clip)
        {
            door = doorRoot; closeOnWave = closeWave; openOnWave = openWave; startClosed = closedAtStart; requirePlayerInArena = portal;
            minPlayerDistance = Mathf.Max(0, playerDistance); warningSeconds = Mathf.Max(0, warning); sealedVolume = sealedWorld;
            openingCenter = openingWorldCenter; openingHalfExtents = openingWorldHalfExtents; fallbackArena = arenaWorld;
            warningLamps = lamps ?? new Renderer[0]; warningAudio = audioSource; warningClip = clip;
        }

        void Start()
        {
            if (!door) { Debug.LogError("ArenaShutter sin puerta: " + name, this); enabled = false; return; }
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var candidate = root.GetComponentInChildren<CampaignRoom>(true);
                if (candidate) { room = candidate; break; }
            }
            if (room) rounds = room.Rounds;
            if (rounds) { living = rounds.Living; livingSet = living as HashSet<Health>; }
            var flow = FindAnyObjectByType<CampaignFlow>();
            if (flow) { player = flow.Player; menu = flow.Menu; }
            if (!player) player = FindAnyObjectByType<PlayerCharacterController>();
            SetLamps(false);
            state = startClosed ? ShutterState.Closed : ShutterState.Open;
            // The authored pose already matches the start state; only correct a mismatch (AirlockDoorMotion keeps the contract).
            if (door.activeSelf != startClosed) AirlockDoorMotion.Set(this, door, startClosed);
        }

        void Update()
        {
            if (state == ShutterState.Done) return;
            if (!room)
            {
                if (!warnedMissingRoom) { warnedMissingRoom = true; Debug.LogWarning("ArenaShutter: no hay CampaignRoom en la escena " + gameObject.scene.name + "; la persiana queda en su estado inicial.", this); }
                return;
            }
            if (menu && !menu.IsPlaying) return;
            int wave = rounds ? rounds.WaveIndex : -1;
            bool open = room.Completed || (openOnWave >= 0 && wave >= openOnWave);
            if (open)
            {
                if (door.activeSelf) AirlockDoorMotion.Set(this, door, false);
                SetLamps(false); state = ShutterState.Done;
                return;
            }
            switch (state)
            {
                case ShutterState.Open:
                    if (closeOnWave >= 0 && wave >= closeOnWave)
                    {
                        state = ShutterState.Warning; nextTry = Time.time + warningSeconds;
                        if (warningAudio && warningClip) warningAudio.PlayOneShot(warningClip);
                    }
                    break;
                case ShutterState.Warning:
                    Blink();
                    if (Time.time < nextTry) break;
                    nextTry = Time.time + RetrySeconds;
                    if (!CanClose()) break;
                    SetLamps(false);
                    AirlockDoorMotion.Set(this, door, true);
                    state = ShutterState.Closed;
                    break;
            }
        }

        /// R-P1: every condition must hold; nothing is forced.
        bool CanClose()
        {
            if (!player || player.IsDead) return false;
            Vector3 feet = player.transform.position;
            if (sealedVolume.size.sqrMagnitude > 0 && sealedVolume.Contains(feet)) return false;
            if (requirePlayerInArena)
            {
                Bounds arena = rounds ? rounds.FirstRoomBounds : fallbackArena;
                if (arena.size.sqrMagnitude > 0 && !arena.Contains(feet)) return false;
                if (sealedVolume.size.sqrMagnitude > 0 && sealedVolume.SqrDistance(feet) < minPlayerDistance * minPlayerDistance) return false;
            }
            if (EnemyInsideSealedVolume()) return false;
            int count = Physics.OverlapBoxNonAlloc(openingCenter, openingHalfExtents + Vector3.one * Clearance, Hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (count >= Hits.Length) return false; // saturated: be conservative
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i]; Hits[i] = null;
                if (!hit || hit.transform.IsChildOf(door.transform)) continue;
                if (hit is CharacterController || hit.GetComponentInParent<Health>()) { for (int j = i + 1; j < count; j++) Hits[j] = null; return false; }
            }
            return true;
        }

        bool EnemyInsideSealedVolume()
        {
            if (sealedVolume.size.sqrMagnitude <= 0) return false;
            if (livingSet != null)
            {
                foreach (var health in livingSet) if (health && sealedVolume.Contains(health.transform.position)) return true; // struct enumerator: no GC
                return false;
            }
            if (living != null) foreach (var health in living) if (health && sealedVolume.Contains(health.transform.position)) return true;
            return false;
        }

        void Blink() => SetLamps(Mathf.Repeat(Time.time * BlinkHz, 1f) < .5f);

        void SetLamps(bool on)
        {
            if (lampsApplied && lampsOn == on) return;
            lampsApplied = true; lampsOn = on;
            for (int i = 0; i < warningLamps.Length; i++) if (warningLamps[i]) warningLamps[i].enabled = on;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, .3f, .1f, .35f); Gizmos.DrawWireCube(sealedVolume.center, sealedVolume.size);
            Gizmos.color = new Color(1f, .8f, .1f, .5f); Gizmos.DrawWireCube(openingCenter, openingHalfExtents * 2f);
        }
    }
}
