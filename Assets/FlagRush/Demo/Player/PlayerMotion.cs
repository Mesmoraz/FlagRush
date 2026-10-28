using Unity.Mathematics;

namespace FlagRush.Demo
{
    /// <summary>Axis-aligned XZ play area (Min/Max are x,z corners).</summary>
    public struct ArenaBounds
    {
        public float2 Min;
        public float2 Max;

        public static ArenaBounds Default => new() { Min = -30f, Max = 30f };
    }

    /// <summary>Movement constants shared by client prediction and the server.</summary>
    public static class PlayerTuning
    {
        public const float MaxSpeed = 6f;
        public const float SprintSpeed = 10f;
        public const float Acceleration = 40f;
        public const float Friction = 30f;
        public const float Radius = 0.6f;
        public const float Height = 1.6f;
    }

    /// <summary>Pure, deterministic movement step. Runs identically on client (prediction) and server.</summary>
    public static class PlayerMotion
    {
        public static void Step(ref float3 position, ref float2 velocity, in PlayerInput input, float dt, in ArenaBounds arena, in ArenaLayout layout)
        {
            float2 move = input.Move;
            float moveLen = math.length(move);
            bool hasInput = moveLen > 0f;
            if (moveLen > 1f) move /= moveLen;

            float2 target = hasInput ? move * (input.Sprint ? PlayerTuning.SprintSpeed : PlayerTuning.MaxSpeed) : float2.zero;
            float rate = hasInput ? PlayerTuning.Acceleration : PlayerTuning.Friction;

            float2 delta = target - velocity;
            float deltaLen = math.length(delta);
            float maxDelta = rate * dt;
            velocity = deltaLen <= maxDelta ? target : velocity + delta / deltaLen * maxDelta;

            float2 xz = new float2(position.x, position.z) + velocity * dt;
            float2 lo = arena.Min + PlayerTuning.Radius;
            float2 hi = arena.Max - PlayerTuning.Radius;
            float2 clamped = math.clamp(xz, lo, hi);
            if (clamped.x != xz.x) velocity.x = 0f;
            if (clamped.y != xz.y) velocity.y = 0f;

            position.x = clamped.x;
            position.z = clamped.y;

            Obstacles.Slide(ref position, ref velocity, PlayerTuning.Radius, layout);
        }
    }
}
