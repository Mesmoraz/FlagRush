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

        /// <summary>Unit XZ aim from a camera ray through the cursor onto y = 0; zero if the ray misses or the point is on the player.</summary>
        public static float2 AimFromCursor(float3 rayOrigin, float3 rayDir, float2 playerXZ)
        {
            if (rayDir.y >= -1e-5f) return float2.zero;
            float t = -rayOrigin.y / rayDir.y;
            float2 hit = (rayOrigin + rayDir * t).xz;
            float2 d = hit - playerXZ;
            float len = math.length(d);
            return len < 0.05f ? float2.zero : d / len;
        }

        /// <summary>Unit camera-relative aim from a stick; zero below 0.2 magnitude.</summary>
        public static float2 AimFromStick(float2 stick, float3 cameraForward, float3 cameraRight)
        {
            if (math.length(stick) < 0.2f) return float2.zero;
            float2 dir = stick.y * Flatten(cameraForward, new float2(0, 1)) + stick.x * Flatten(cameraRight, new float2(1, 0));
            float len = math.length(dir);
            return len > 1e-5f ? dir / len : float2.zero;
        }

        static float2 Flatten(float3 v, float2 fallback)
        {
            float2 xz = v.xz;
            float len = math.length(xz);
            return len > 1e-5f ? xz / len : fallback;
        }
    }
}
