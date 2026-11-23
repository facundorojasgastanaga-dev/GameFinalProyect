using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    /// Map01 respite rooms (R05, R09, R13, R17): a CampaignRoom without rounds or pursuit only reports Completed after
    /// Release() (CampaignRoom.cs:20,23). This component finds the CampaignRoom of its own scene in Start and calls
    /// Release() once, when the player enters its trigger volume (or on Start). It only observes; it never edits the room.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Map01StageRelease : MonoBehaviour
    {
        public enum ReleaseMode { OnEnterVolume, OnStart }

        [SerializeField] ReleaseMode mode = ReleaseMode.OnEnterVolume;
        [SerializeField, Min(0)] float delaySeconds;

        CampaignRoom room;
        float releaseAt = -1f;
        bool released;

        public bool Released => released;
        public CampaignRoom Room => room;

        void Awake()
        {
            var box = GetComponent<BoxCollider>();
            if (box) box.isTrigger = true;
        }

        void Start()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                room = root.GetComponentInChildren<CampaignRoom>(true);
                if (room) break;
            }
            if (!room) Debug.LogWarning("Map01StageRelease: no CampaignRoom in scene " + gameObject.scene.name + "; the exit will not open.", this);
            if (mode == ReleaseMode.OnStart) Arm();
        }

        void OnTriggerEnter(Collider other) => Contact(other);
        void OnTriggerStay(Collider other) => Contact(other);

        void Contact(Collider other)
        {
            if (released || releaseAt >= 0f || mode != ReleaseMode.OnEnterVolume) return;
            if (!other.GetComponentInParent<PlayerCharacterController>()) return;
            Arm();
        }

        void Arm()
        {
            releaseAt = Time.time + delaySeconds;
            if (delaySeconds <= 0f) ReleaseNow();
        }

        void Update()
        {
            if (!released && releaseAt >= 0f && Time.time >= releaseAt) ReleaseNow();
        }

        void ReleaseNow()
        {
            if (released || !room) return;
            released = true;
            room.Release();
        }
    }
}
