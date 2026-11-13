using UnityEngine;

namespace IronRequiem
{
    /// Hangar menu conveyor: scrolls the belt surface and the chevron strip at the same speed the robots travel (+X).
    /// Works on runtime instances of the materials, so the assets are never modified.
    public sealed class ConveyorBelt : MonoBehaviour
    {
        [SerializeField] Renderer[] surfaces = new Renderer[0];
        [SerializeField] float[] metersPerTile = new float[0];
        [SerializeField] float metersPerSecond = 1.3f;
        Material[] instances; float[] offsets;
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        public float Speed => metersPerSecond;

        public void Configure(Renderer[] scrolling, float[] tileLengths, float speed) { surfaces = scrolling; metersPerTile = tileLengths; metersPerSecond = speed; }

        void Awake()
        {
            instances = new Material[surfaces.Length]; offsets = new float[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++) if (surfaces[i]) instances[i] = surfaces[i].material;
        }
        void OnDestroy() { if (instances != null) foreach (var m in instances) if (m) Destroy(m); }

        void Update()
        {
            for (int i = 0; i < instances.Length; i++)
            {
                if (!instances[i]) continue;
                float tile = i < metersPerTile.Length && metersPerTile[i] > 0 ? metersPerTile[i] : 1;
                offsets[i] = Mathf.Repeat(offsets[i] - metersPerSecond / tile * Time.deltaTime, 1);
                instances[i].SetTextureOffset(BaseMapId, new Vector2(offsets[i], 0));
            }
        }
    }
}
