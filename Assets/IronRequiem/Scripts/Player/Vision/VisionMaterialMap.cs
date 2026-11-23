using System;
using UnityEngine;

namespace IronRequiem
{
    /// Chat 11 · passive dark readability. Pairs of enemy body materials without the HardSurface rim and their
    /// "_Vision" copy with the rim keyword on (built by Editor/Vision/VisionMaterialVariants). Loaded from Resources.
    /// Source materials are never modified; live enemy instances swap to the variant.
    [CreateAssetMenu(menuName = "Iron Requiem/Vision Material Map")]
    public sealed class VisionMaterialMap : ScriptableObject
    {
        public const string ResourcePath = "Vision/VisionMaterialMap";
        [Serializable] public sealed class Pair { public Material Source; public Material Variant; }
        public Pair[] Pairs = new Pair[0];

        public Material VariantFor(Material source)
        {
            if (!source || Pairs == null) return null;
            for (int i = 0; i < Pairs.Length; i++) if (Pairs[i] != null && Pairs[i].Source == source) return Pairs[i].Variant;
            return null;
        }
    }
}
