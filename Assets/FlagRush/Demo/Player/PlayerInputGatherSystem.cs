using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FlagRush.Demo
{
    /// <summary>
    /// Client-only input gathering: keyboard (WASD/arrows, Shift) and gamepad (left stick, south button),
    /// mapped camera-relative and written to the owned ghost's <see cref="PlayerInput"/> every frame.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial class PlayerInputGatherSystem : SystemBase
    {
        /// <summary>Tests set this to bypass devices; null means read real devices.</summary>
        public static PlayerInput? TestOverride;

        protected override void OnCreate() => RequireForUpdate<PlayerInput>();

        protected override void OnUpdate()
        {
            PlayerInput input = TestOverride ?? ReadDevices();
            foreach (var playerInput in SystemAPI.Query<RefRW<PlayerInput>>().WithAll<GhostOwnerIsLocal>())
                playerInput.ValueRW = input;
        }

        static PlayerInput ReadDevices()
        {
            float2 raw = float2.zero;
            bool sprint = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) raw.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) raw.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) raw.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) raw.x -= 1;
                sprint = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.magnitude > 0.1f) raw = new float2(stick.x, stick.y);
                sprint |= pad.buttonSouth.isPressed;
            }

            float3 forward = new float3(0, 0, 1), right = new float3(1, 0, 0);
            var cam = Camera.main;
            if (cam != null)
            {
                var t = cam.transform;
                forward = t.forward;
                right = t.right;
            }
            return PlayerInputMapping.FromAxes(raw, forward, right, sprint);
        }
    }
}
