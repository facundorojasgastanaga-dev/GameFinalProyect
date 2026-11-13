using UnityEngine;

namespace IronRequiem
{
    // Keeps an ambience emitter (air dust) centred on the main camera. Looks the camera up at most twice a second.
    public sealed class FollowMainCamera : MonoBehaviour
    {
        Transform target; float nextLookup;
        void LateUpdate()
        {
            if (!target && Time.unscaledTime >= nextLookup) { nextLookup = Time.unscaledTime + .5f; var c = Camera.main; if (c) target = c.transform; }
            if (target) transform.position = target.position;
        }
    }
}
