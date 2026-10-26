using Unity.Mathematics;

namespace FlagRush.Demo
{
    /// <summary>Pure mapping from raw device axes to a camera-relative <see cref="PlayerInput"/>.</summary>
    public static class PlayerInputMapping
    {
        public static PlayerInput FromAxes(float2 raw, float3 cameraForward, float3 cameraRight, bool sprint)
        {
            float2 fwd = Flatten(cameraForward, new float2(0, 1));
            float2 right = Flatten(cameraRight, new float2(1, 0));
            float2 move = raw.y * fwd + raw.x * right;
            float len = math.length(move);
            if (len > 1f) move /= len;
            return new PlayerInput { Move = move, Sprint = sprint };
        }

        static float2 Flatten(float3 v, float2 fallback)
        {
            float2 xz = v.xz;
            float len = math.length(xz);
            return len > 1e-5f ? xz / len : fallback;
        }
    }
}
