using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronRequiem
{
    [RequireComponent(typeof(PlayerCharacterController),typeof(PlayerInputHandler))]
    public sealed class MechanicInput : MonoBehaviour
    {
        PlayerCharacterController player;
        PlayerInputHandler original;
        GameFlowManager flow;
        InputAction dash, interact, throwWeapon, overdrive, melee, crouch;
        public bool CanAct => player && original && !player.IsDead && Time.timeScale > 0 &&
            (!flow || !flow.GameIsEnding) && original.CanProcessInput();
        public bool DashPressed => CanAct && dash != null && dash.WasPressedThisFrame();
        public bool MeleePressed => CanAct && melee != null && melee.WasPressedThisFrame();
        // v5: F is held to charge the right-hand strike; raw state so a release is never missed.
        public bool MeleeHeld => melee != null && melee.IsPressed();
        public bool InteractPressed => CanAct && interact != null && interact.WasPressedThisFrame();
        public bool ThrowPressed => CanAct && throwWeapon != null && throwWeapon.WasPressedThisFrame();
        // Raw crouch state (not gated by CanAct) so a release during pause or a scripted move is still seen.
        public bool CrouchHeld => crouch != null && crouch.IsPressed();
        public bool CrouchReleasedThisFrame => crouch != null && crouch.WasReleasedThisFrame();
        public bool OverdrivePressed => CanAct && overdrive != null && overdrive.WasPressedThisFrame();

        void Awake()
        {
            player=GetComponent<PlayerCharacterController>();original=GetComponent<PlayerInputHandler>();
        }
        void Start()
        {
            flow=FindAnyObjectByType<GameFlowManager>();
            melee=InputSystem.actions.FindAction("Player/Melee",false);if(melee!=null)melee.Enable();
            crouch=InputSystem.actions.FindAction("Player/Crouch",false);
            dash=Find("Dash");interact=Find("CollectWeapon");throwWeapon=Find("ThrowWeapon");overdrive=Find("Overdrive");
        }
        static InputAction Find(string name)
        {
            var action=InputSystem.actions.FindAction("Player/"+name,true);action.Enable();return action;
        }
    }
}
