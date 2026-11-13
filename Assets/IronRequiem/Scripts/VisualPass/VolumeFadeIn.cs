using System.Reflection;
using UnityEngine;

namespace IronRequiem
{
    // Fades a room's sector grade volume in when the room loads, so the colour grade does not pop.
    // The runtime assembly does not reference the SRP Core assembly, so the Volume weight is set by reflection
    // (one cached member, about one second of updates per room load).
    public sealed class VolumeFadeIn : MonoBehaviour
    {
        [SerializeField, Min(.05f)] float seconds = 1.2f;
        Component volume; FieldInfo weight; float t;
        void OnEnable()
        {
            volume = GetComponent("Volume"); t = 0;
            weight = volume ? volume.GetType().GetField("weight", BindingFlags.Public | BindingFlags.Instance) : null;
            weight?.SetValue(volume, 0f);
        }
        void Update()
        {
            if (t >= 1 || weight == null) return;
            t = Mathf.Min(1, t + Time.unscaledDeltaTime / seconds);
            weight.SetValue(volume, t * t * (3 - 2 * t));
        }
    }
}
