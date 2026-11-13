using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // Movement policy and presentation state. PlayerCharacterController remains the sole movement authority.
    public interface IPlayerMotionExtension
    {
        bool KeepCrouched { get; }
        bool SuppressWeaponBob { get; }
        // True while the extension drives a scripted arc: ground detection/snapping would pull it back down.
        bool SuppressGrounding { get; }
        float ConstrainYawDelta(float requestedDelta);
        // Receives the clamped camera pitch (degrees, positive looks down) and returns the allowed value.
        float ConstrainPitch(float pitch);
        void BeforeMovement();
        bool HandleCrouchInput();
        bool TryGetVelocity(Vector3 standardVelocity, out Vector3 velocity);
        void AfterMovement(CollisionFlags collisions);
    }
}
