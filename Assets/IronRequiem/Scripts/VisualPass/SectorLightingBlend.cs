using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IronRequiem
{
    // Visual pass (09/11/2026): room scenes carry their own ambient/fog. When CampaignFlow switches the active scene
    // (room -> session -> next room) Unity swaps RenderSettings in one frame, which read as a lighting pop.
    // This keeps the displayed ambient/fog and eases it toward whatever the active scene asks for.
    [DisallowMultipleComponent]
    public sealed class SectorLightingBlend : MonoBehaviour
    {
        [SerializeField, Min(.05f)] float seconds = 1.4f;
        Color sky, equator, ground, fog; float density; bool has;
        Color tSky, tEquator, tGround, tFog; float tDensity; float k = 1;

        void OnEnable() { SceneManager.activeSceneChanged += Changed; Capture(out sky, out equator, out ground, out fog, out density); has = true; }
        void OnDisable() => SceneManager.activeSceneChanged -= Changed;

        static void Capture(out Color s, out Color e, out Color g, out Color f, out float d)
        { s = RenderSettings.ambientSkyColor; e = RenderSettings.ambientEquatorColor; g = RenderSettings.ambientGroundColor; f = RenderSettings.fogColor; d = RenderSettings.fogDensity; }

        void Changed(Scene from, Scene to)
        {
            if (!has) return;
            Capture(out tSky, out tEquator, out tGround, out tFog, out tDensity);
            k = 0; Apply(sky, equator, ground, fog, density);
        }

        void Update()
        {
            if (k >= 1) return;
            k = Mathf.Min(1, k + Time.unscaledDeltaTime / seconds);
            float e = k * k * (3 - 2 * k);
            Apply(Color.Lerp(sky, tSky, e), Color.Lerp(equator, tEquator, e), Color.Lerp(ground, tGround, e), Color.Lerp(fog, tFog, e), Mathf.Lerp(density, tDensity, e));
            if (k >= 1) { sky = tSky; equator = tEquator; ground = tGround; fog = tFog; density = tDensity; }
        }

        void LateUpdate()
        {
            if (k >= 1) Capture(out sky, out equator, out ground, out fog, out density);
        }

        static void Apply(Color s, Color e, Color g, Color f, float d)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = s; RenderSettings.ambientEquatorColor = e; RenderSettings.ambientGroundColor = g;
            RenderSettings.fogColor = f; RenderSettings.fogDensity = d;
            DynamicGI.UpdateEnvironment();
        }
    }
}
