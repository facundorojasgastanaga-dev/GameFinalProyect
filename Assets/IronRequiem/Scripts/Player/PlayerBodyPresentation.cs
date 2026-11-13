using UnityEngine;
using UnityEngine.Rendering;

namespace IronRequiem
{
    // Presentation only: movement, camera and weapon handling remain in the template.
    public sealed class PlayerBodyPresentation : MonoBehaviour
    {
        [SerializeField] Transform bodyRoot;
        Renderer[] renderers;
        ShadowCastingMode[] modes;

        public void Configure(Transform root) => bodyRoot = root;

        void OnEnable()
        {
            if (!bodyRoot) return;
            renderers = bodyRoot.GetComponentsInChildren<Renderer>(true);
            modes = new ShadowCastingMode[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                modes[i] = renderers[i].shadowCastingMode;
                renderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

        void OnDisable()
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i]) renderers[i].shadowCastingMode = modes[i];
        }
    }
}
