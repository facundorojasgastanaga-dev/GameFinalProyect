using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem
{
    /// Keeps the Carroñero of the security camera clear of capsule C-04 (16/11/2026). The actor was authored 1.2 m from the
    /// capsule pivot, but the capsule is tilted and wider than that, so its left rail went through the actor. The authored
    /// position lives in the (user-modified) IntroMenu scene, so it is corrected at run time instead: the actor is
    /// placed at a fixed offset from the capsule, turned to face it, and its key light follows.
    public static class CctvActorFitter
    {
        /// Offset from the Capsule_C04 pivot (world axes; y is kept from the authored actor).
        public static Vector3 Offset = new Vector3(-2.3f, 0, .3f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded;
            Apply();
        }
        static void OnLoaded(Scene scene, LoadSceneMode mode) => Apply();

        public static void Apply()
        {
            foreach (var actor in Object.FindObjectsByType<CctvActor>(FindObjectsInactive.Include))
            {
                var pod = FindInScene(actor.gameObject.scene, "Capsule_C04"); if (!pod) continue;
                var at = pod.position + Offset; at.y = actor.transform.position.y;
                var delta = at - actor.transform.position; if (delta.sqrMagnitude < 1e-6f) continue;
                var toPod = Vector3.ProjectOnPlane(pod.position - at, Vector3.up);
                actor.transform.SetPositionAndRotation(at, toPod.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toPod) : actor.transform.rotation);
                var key = actor.transform.parent ? actor.transform.parent.Find("CctvKey") : null;
                if (key) key.position += delta;
            }
        }

        static Transform FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
