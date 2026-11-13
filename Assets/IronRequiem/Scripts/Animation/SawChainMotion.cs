using UnityEngine;

namespace IronRequiem
{
    // Prebuilt geometry phases circulate cutters around the fixed guide bar.
    // Meshes and materials are shared assets; no per-frame mesh allocations or material instances.
    public sealed class SawChainMotion : MonoBehaviour
    {
        [SerializeField] MeshFilter[] lodFilters;
        [SerializeField] Mesh[] phases;
        [SerializeField, Min(2)] int phaseCount = 8;
        [SerializeField, Min(1)] float teethPerSecond = 18;
        float travel;
        int frame = -1;
        public int CurrentFrame => frame;
        public float Travel => travel;
        public Renderer[] Views => System.Array.ConvertAll(lodFilters, filter => filter.GetComponent<Renderer>());
        public void Configure(MeshFilter[] filters, Mesh[] geometry) { lodFilters = filters; phases = geometry; Apply(0); }
        public void Advance(bool running)
        {
            if (!running || Time.deltaTime <= 0) return;
            // Each phase cycle advances two links, preserving alternating left/right cutters.
            travel = Mathf.Repeat(travel + Time.deltaTime * teethPerSecond * .5f, 1);
            Apply(Mathf.FloorToInt(travel * phaseCount) % phaseCount);
        }
        void Apply(int value)
        {
            if (value == frame || lodFilters == null || phases == null) return;
            frame = value;
            for (int lod = 0; lod < lodFilters.Length; lod++)
            {
                int index = lod * phaseCount + value;
                if (lodFilters[lod] && index < phases.Length) lodFilters[lod].sharedMesh = phases[index];
            }
        }
        void Awake() { frame = -1; Apply(0); }
    }
}
