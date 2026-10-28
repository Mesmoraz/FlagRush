using Unity.Collections;
using Unity.Mathematics;

namespace FlagRush.Demo
{
    /// <summary>Pure ray-vs-circle hit test. No [BurstCompile]; inlined into Burst systems.</summary>
    public static class Hitscan
    {
        /// <summary>Returns the index of the nearest hittable enemy hit by the ray, or -1 (also when dir is zero).</summary>
        public static int Cast(float2 origin, float2 dir, float range, in ArenaLayout layout,
            NativeArray<float2> positions, NativeArray<byte> teams, NativeArray<bool> hittable,
            byte shooterTeam, int shooterIndex)
        {
            float len = math.length(dir);
            if (len < 1e-6f) return -1;
            float2 d = dir / len;

            const float r2 = PlayerTuning.Radius * PlayerTuning.Radius;
            int best = -1;
            float bestEntry = float.MaxValue;
            for (int i = 0; i < positions.Length; i++)
            {
                if (i == shooterIndex || !hittable[i] || teams[i] == shooterTeam) continue;
                float2 to = positions[i] - origin;
                float along = math.dot(to, d);
                float perp = d.x * to.y - d.y * to.x;
                float perp2 = perp * perp;
                if (perp2 > r2) continue;
                float entry = along - math.sqrt(r2 - perp2);
                if (entry < 0f || entry > range || entry >= bestEntry) continue;
                if (Obstacles.SegmentHit(origin, origin + d * entry, layout, out _, out _)) continue;
                best = i;
                bestEntry = entry;
            }
            return best;
        }
    }
}
