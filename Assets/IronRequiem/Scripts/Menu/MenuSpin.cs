using UnityEngine;

namespace IronRequiem
{
    /// Rotating warning beacon in the hangar menu (unscaled time).
    public sealed class MenuSpin : MonoBehaviour
    {
        [SerializeField] Vector3 axis = Vector3.up;
        [SerializeField] float degreesPerSecond = 160;
        public void Configure(Vector3 worldAxis, float speed) { axis = worldAxis; degreesPerSecond = speed; }
        void Update() => transform.Rotate(axis, degreesPerSecond * Time.unscaledDeltaTime, Space.World);
    }
}
