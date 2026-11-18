using UnityEngine;
using UnityEngine.Rendering;

namespace IronRequiem
{
    /// 17/11/2026 (MAP_FX_AUDIT r2, P-E2a). Room00_Hangar keeps the menu grade (HG_PostVolume / HG_MenuVolume, shared
    /// with IntroMenu) untouched during the wake-up cinematic, which is part of the protected menu zone. Only after the
    /// cinematic has finished (or was skipped) does this runtime-only global Volume add the gameplay grade
    /// (HG_GameplayVolume) one priority above it, ramping in over a short SmoothStep. Its weight always follows the
    /// source volume's weight, so the zone fades of CampaignFlow (ZoneEnvironment.SceneFade) still fade it out with the
    /// hangar. Nothing is saved: the object is created in the loaded stage scene and dies with it.
    public sealed class HangarGameplayPost : MonoBehaviour
    {
        [SerializeField, Min(.05f)] float rampSeconds = 1.5f;
        Volume source, overlay; HangarIntroCinematic intro; bool searched; float t;

        public Volume Overlay => overlay;
        public float Ramp => t;

        public static HangarGameplayPost Attach(Volume menuVolume, VolumeProfile gameplay)
        {
            if (!menuVolume || !gameplay) return null;
            var scene = menuVolume.gameObject.scene;
            foreach (var r in scene.GetRootGameObjects()) { var found = r.GetComponent<HangarGameplayPost>(); if (found && found.source == menuVolume) return found; }
            // A root of its own (never a child of the protected HG_PostVolume hierarchy).
            var go = new GameObject("HG_GameplayPost_Runtime") { layer = menuVolume.gameObject.layer };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var post = go.AddComponent<HangarGameplayPost>();
            post.source = menuVolume;
            post.overlay = go.AddComponent<Volume>();
            post.overlay.isGlobal = true;
            post.overlay.priority = menuVolume.priority + 1;
            post.overlay.sharedProfile = gameplay;
            post.overlay.weight = 0;
            return post;
        }

        void Update()
        {
            if (!source || !overlay) return;
            if (!searched)
            {
                // The cinematic lives in the same stage scene; read only (its class is protected and not modified).
                foreach (var c in FindObjectsByType<HangarIntroCinematic>(FindObjectsInactive.Include))
                    if (c.gameObject.scene == gameObject.scene) { intro = c; break; }
                searched = true;
            }
            bool done = !intro || intro.Finished;
            if (done && t < 1) t = Mathf.Min(1, t + Time.unscaledDeltaTime / rampSeconds);
            float k = t * t * (3 - 2 * t);
            overlay.weight = source.weight * k;
        }
    }
}
