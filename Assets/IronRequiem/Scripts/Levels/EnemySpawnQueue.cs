using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronRequiem
{
    // Spreads enemy creation over frames instead of building a whole wave in one frame.
    // Prewarm: instances are deserialized under an inactive holder (no Awake/OnEnable/Start), a few per frame,
    // during calm time (menu, end of a wave). Spawn: queued requests are activated a few per frame; activation
    // moves the instance out of the holder, which runs Awake/OnEnable there and Start on the next frame.
    // A request with no prewarmed instance left is deserialized in its own frame first, then activated.
    // Instances are never recycled: enemies still destroy themselves on death exactly as before.
    public sealed class EnemySpawnQueue : MonoBehaviour
    {
        [Tooltip("Inactive instances deserialized per frame while prewarming.")]
        [SerializeField, Min(1)] int prewarmPerFrame = 1;
        [Tooltip("Enemies activated per frame while a wave is spawning.")]
        [SerializeField, Min(1)] int activationsPerFrame = 1;

        sealed class Request { public GameObject Prefab; public Vector3 Position; public Quaternion Rotation; public Action<GameObject> Spawned; public Func<Vector3?> ResolvePosition; public Func<Vector3, Quaternion> ResolveRotation; }
        readonly Dictionary<GameObject, Queue<GameObject>> ready = new Dictionary<GameObject, Queue<GameObject>>();
        readonly Queue<GameObject> prewarm = new Queue<GameObject>();
        readonly Queue<Request> spawns = new Queue<Request>();
        Transform holder;
        int lastBuildFrame = -1;

        public int Pending => spawns.Count;
        public int PrewarmPending => prewarm.Count;
        public int Ready(GameObject prefab) => ready.TryGetValue(prefab, out var q) ? q.Count : 0;
        public void Configure(int prewarmFrame, int activateFrame) { prewarmPerFrame = Mathf.Max(1, prewarmFrame); activationsPerFrame = Mathf.Max(1, activateFrame); }

        Transform Holder()
        {
            if (holder) return holder;
            var go = new GameObject("EnemySpawnQueue_Prewarmed"); go.SetActive(false);
            go.transform.SetParent(transform, false); holder = go.transform; return holder;
        }

        public void Prewarm(GameObject prefab, int count) { if (!prefab) return; for (int i = 0; i < count; i++) prewarm.Enqueue(prefab); }

        public void Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Action<GameObject> spawned, Func<Vector3?> resolvePosition = null, Func<Vector3, Quaternion> resolveRotation = null)
            => spawns.Enqueue(new Request { Prefab = prefab, Position = position, Rotation = rotation, Spawned = spawned, ResolvePosition = resolvePosition, ResolveRotation = resolveRotation });

        GameObject Build(GameObject prefab)
        {
            var instance = Instantiate(prefab, Holder());
            instance.name = prefab.name;
            lastBuildFrame = Time.frameCount;
            return instance;
        }

        void Update()
        {
            if (spawns.Count > 0) { if (Time.timeScale > 0) Activate(); return; }   // prewarming waits while a wave is coming in
            for (int i = 0; i < prewarmPerFrame && prewarm.Count > 0; i++)
            {
                var prefab = prewarm.Dequeue();
                if (!ready.TryGetValue(prefab, out var q)) ready[prefab] = q = new Queue<GameObject>();
                q.Enqueue(Build(prefab));
            }
        }

        void Activate()
        {
            for (int n = 0; n < activationsPerFrame && spawns.Count > 0; n++)
            {
                var request = spawns.Peek();
                GameObject instance = null;
                if (ready.TryGetValue(request.Prefab, out var q)) while (q.Count > 0 && !instance) instance = q.Dequeue();
                if (!instance)
                {
                    // Nothing prewarmed: deserialize now, activate next frame (never both for one enemy in one frame).
                    if (lastBuildFrame == Time.frameCount) return;
                    if (!ready.TryGetValue(request.Prefab, out q)) ready[request.Prefab] = q = new Queue<GameObject>();
                    q.Enqueue(Build(request.Prefab)); return;
                }
                // Revalidate immediately before activation: a player can dash into a selected slot
                // while its instance is still prewarming. Keep it inactive until a safe slot exists.
                if (request.ResolvePosition != null)
                {
                    var position = request.ResolvePosition();
                    if (!position.HasValue) { q.Enqueue(instance); return; }
                    request.Position = position.Value;
                }
                spawns.Dequeue();
                instance.transform.SetPositionAndRotation(request.Position, request.ResolveRotation != null ? request.ResolveRotation(request.Position) : request.Rotation);
                instance.transform.SetParent(null, true);   // activates: Awake/OnEnable now, Start next frame
                if (instance.scene != gameObject.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
                request.Spawned?.Invoke(instance);
            }
        }

        public void CancelPending() => spawns.Clear();
    }
}
