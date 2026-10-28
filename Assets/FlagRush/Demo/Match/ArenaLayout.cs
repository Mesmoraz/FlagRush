using Unity.Collections;
using Unity.Mathematics;

namespace FlagRush.Demo
{
    /// <summary>Axis-aligned box obstacle on the XZ plane (Center/HalfExtents are x,z).</summary>
    public struct Box
    {
        public float2 Center;
        public float2 HalfExtents;
    }

    /// <summary>
    /// The match arena: box obstacles, the two bases and the spawn zones. Burst code reads it only through the
    /// <see cref="ActiveArena"/> singleton; these static properties build the value on the managed side.
    /// </summary>
    public struct ArenaLayout
    {
        public const int SpawnSlots = 80;

        public FixedList512Bytes<Box> Boxes;
        public float2 RedBase;
        public float2 BlueBase;
        public float BaseRadius;

        public static ArenaLayout Empty => new() { BaseRadius = 3f, RedBase = new float2(-25f, 0f), BlueBase = new float2(25f, 0f) };

        public static ArenaLayout Match
        {
            get
            {
                var l = Empty;
                l.Boxes.Add(new Box { Center = new float2(-12f, 0f), HalfExtents = new float2(1f, 5f) });
                l.Boxes.Add(new Box { Center = new float2(12f, 0f), HalfExtents = new float2(1f, 5f) });
                l.Boxes.Add(new Box { Center = new float2(0f, -10f), HalfExtents = new float2(4f, 1f) });
                l.Boxes.Add(new Box { Center = new float2(0f, 10f), HalfExtents = new float2(4f, 1f) });
                l.Boxes.Add(new Box { Center = new float2(-20f, -16f), HalfExtents = new float2(2f, 2f) });
                l.Boxes.Add(new Box { Center = new float2(-20f, 16f), HalfExtents = new float2(2f, 2f) });
                l.Boxes.Add(new Box { Center = new float2(20f, -16f), HalfExtents = new float2(2f, 2f) });
                l.Boxes.Add(new Box { Center = new float2(20f, 16f), HalfExtents = new float2(2f, 2f) });
                return l;
            }
        }

        public float2 Base(byte team) => team == 0 ? RedBase : BlueBase;

        /// <summary>Deterministic spawn slot in the team's zone (x in [26,29], z in [-20,20]); index wraps mod 80.</summary>
        public static float3 SpawnPoint(byte team, int index)
        {
            int i = ((index % SpawnSlots) + SpawnSlots) % SpawnSlots;
            float s = team == 0 ? -1f : 1f;
            float f = (i + 0.5f) * 0.618034f;
            f -= math.floor(f);
            return new float3(s * (26f + 3f * f), 0f, -20f + 40f * (i + 0.5f) / SpawnSlots);
        }
    }
}
