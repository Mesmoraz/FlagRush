using Unity.Collections;
using Unity.Mathematics;

namespace FlagRush.Demo
{
    /// <summary>
    /// Pure, deterministic player-vs-player separation on the XZ plane. A spatial hash (2 m cells) finds
    /// candidate pairs; each overlapping pair is pushed apart equally to exactly <c>2 * radius</c>, then every
    /// point is re-clamped to the arena. Pairs are resolved in index order (i &lt; j), in place, so the same
    /// input always gives the same output. Coincident points split along X: lower index -X, higher index +X.
    /// </summary>
    /// <remarks>No [BurstCompile] here; it is inlined into the Burst-compiled <see cref="PlayerSeparationSystem"/>.</remarks>
    public static class PlayerSeparation
    {
        public const float CellSize = 2f;
        const float CoincidentEpsilon = 1e-6f;

        public static void Resolve(NativeArray<float3> positions, float radius, in ArenaBounds arena, in ArenaLayout layout)
        {
            int count = positions.Length;
            if (count == 0) return;

            float minDistance = 2f * radius;
            var cells = new NativeParallelMultiHashMap<int2, int>(count, Allocator.Temp);
            for (int i = 0; i < count; i++)
                cells.Add(Cell(positions[i]), i);

            for (int i = 0; i < count; i++)
            {
                int2 home = Cell(positions[i]);
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!cells.TryGetFirstValue(home + new int2(dx, dz), out int j, out var it)) continue;
                    do
                    {
                        if (j <= i) continue;
                        float2 a = positions[i].xz;
                        float2 b = positions[j].xz;
                        float2 delta = b - a;
                        float distance = math.length(delta);
                        if (distance >= minDistance) continue;

                        float2 dir = distance > CoincidentEpsilon ? delta / distance : new float2(1f, 0f);
                        float2 push = dir * ((minDistance - distance) * 0.5f);
                        a -= push;
                        b += push;
                        positions[i] = new float3(a.x, positions[i].y, a.y);
                        positions[j] = new float3(b.x, positions[j].y, b.y);
                    } while (cells.TryGetNextValue(out j, ref it));
                }
            }
            cells.Dispose();

            float2 lo = arena.Min + radius;
            float2 hi = arena.Max - radius;
            for (int i = 0; i < count; i++)
            {
                var p = positions[i];
                float2 xz = math.clamp(p.xz, lo, hi);
                xz = Obstacles.PushOut(xz, radius, layout);
                xz = math.clamp(xz, lo, hi);
                positions[i] = new float3(xz.x, p.y, xz.y);
            }
        }

        static int2 Cell(float3 p) => (int2)math.floor(p.xz / CellSize);
    }
}
