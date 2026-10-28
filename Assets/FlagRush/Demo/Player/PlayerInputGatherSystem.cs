using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
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
            float2 playerXZ = float2.zero;
            foreach (var t in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerInput, GhostOwnerIsLocal>())
                playerXZ = t.ValueRO.Position.xz;
            PlayerInput input = TestOverride ?? ReadDevices(playerXZ);
            foreach (var playerInput in SystemAPI.Query<RefRW<PlayerInput>>().WithAll<GhostOwnerIsLocal>())
                playerInput.ValueRW = input;
        }

        static PlayerInput ReadDevices(float2 playerXZ)
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

            bool match = DemoConfig.IsMatchLevel;
            bool fire = false;
            float2 stickAim = float2.zero;
            var cam = Camera.main;
            float3 forward = new float3(0, 0, 1), right = new float3(1, 0, 0);
            if (cam != null)
            {
                var t = cam.transform;
                forward = t.forward;
                right = t.right;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.magnitude > 0.1f) raw = new float2(stick.x, stick.y);
                sprint |= pad.buttonSouth.isPressed;
                if (match)
                {
                    Vector2 aimStick = pad.rightStick.ReadValue();
                    stickAim = PlayerInputMapping.AimFromStick(new float2(aimStick.x, aimStick.y), forward, right);
                    fire |= pad.rightTrigger.isPressed;
                }
            }

            var input = PlayerInputMapping.FromAxes(raw, forward, right, sprint);
            if (!match) return input;

            float2 aim = stickAim;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                // A click on either HUD panel (sliders, hide button) is not a shot.
                if (!DemoHud.IsOverPanel(pos))
                {
                    fire |= mouse.leftButton.isPressed;
                    if (math.all(aim == float2.zero) && cam != null)
                    {
                        Ray ray = cam.ScreenPointToRay(new Vector3(pos.x, pos.y, 0f));
                        aim = PlayerInputMapping.AimFromCursor(ray.origin, ray.direction, playerXZ);
                    }
                }
            }
            input.Aim = aim;
            input.Fire = fire;
            return input;
        }
    }
}
