using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronRequiem
{
    // Visual pass (09/11/2026): airlock doors slide into the ceiling instead of popping. Gameplay meaning is
    // unchanged: an open door ends inactive (no collider), a closing door is active (collider on) from the first frame.
    // Coroutines run on the caller (CampaignFlow, always active) because the door object itself gets deactivated.
    public static class AirlockDoorMotion
    {
        const float OpenSeconds = .7f, CloseSeconds = .45f;
        static readonly Dictionary<GameObject, Vector3> closedWorld = new Dictionary<GameObject, Vector3>();
        static readonly Dictionary<GameObject, (Coroutine routine, bool closing)> running = new Dictionary<GameObject, (Coroutine, bool)>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() { closedWorld.Clear(); running.Clear(); }

        public static void Set(MonoBehaviour host, GameObject door, bool closed)
        {
            if (!door) return;
            if (!closedWorld.ContainsKey(door)) closedWorld[door] = door.transform.position;
            if (running.TryGetValue(door, out var r))
            {
                if (r.routine != null && r.closing == closed) return; // already heading there
                if (r.routine != null && host) host.StopCoroutine(r.routine);
                running.Remove(door);
            }
            else if (door.activeSelf == closed && door.transform.position == closedWorld[door]) return; // already there
            if (!host || !host.isActiveAndEnabled || !Application.isPlaying) { door.transform.position = closedWorld[door]; door.SetActive(closed); return; }
            running[door] = (host.StartCoroutine(Run(door, closed)), closed);
        }

        /// True once the door is fully shut and not moving (a sector behind it may be unloaded safely).
        public static bool IsClosed(GameObject door)
        {
            if (!door) return true;
            if (running.ContainsKey(door)) return false;
            return door.activeSelf && (!closedWorld.TryGetValue(door, out var shut) || (door.transform.position - shut).sqrMagnitude < 1e-6f);
        }

        static float Height(GameObject door)
        {
            var r = door.GetComponent<Renderer>();
            return r ? r.bounds.size.y + .1f : 4f;
        }

        static IEnumerator Run(GameObject door, bool closed)
        {
            var t = door.transform; var shut = closedWorld[door]; var open = shut + Vector3.up * Height(door);
            if (closed)
            {
                if (!door.activeSelf) { t.position = open; door.SetActive(true); }
                var from = t.position;
                for (float k = 0; k < 1; k += Time.deltaTime / CloseSeconds) { t.position = Vector3.LerpUnclamped(from, shut, k * k); yield return null; }
                t.position = shut;
            }
            else
            {
                var from = t.position;
                for (float k = 0; k < 1; k += Time.deltaTime / OpenSeconds) { float e = k * k * (3 - 2 * k); t.position = Vector3.LerpUnclamped(from, open, e); yield return null; }
                door.SetActive(false); t.position = shut;
            }
            running.Remove(door);
        }
    }
}
