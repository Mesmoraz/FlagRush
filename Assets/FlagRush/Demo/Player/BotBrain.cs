using Unity.Entities;
using Unity.Mathematics;

namespace FlagRush.Demo
{
    /// <summary>Server-only bot wander state. Added at bot spawn; not a ghost component.</summary>
    public struct BotBrainState : IComponentData
    {
        public float2 Target;
        public uint NextRetargetTick;
        public Random Rng;
        public bool Sprint;

        /// <summary>Fresh state for a bot: seeded from its index, retargets on its first think.</summary>
        public static BotBrainState Create(int botIndex) => new()
        {
            Rng = new Random((uint)(botIndex * 7919 + 1)),
        };
    }

    /// <summary>
    /// Pure, deterministic bot "brain": wander between random points inside the arena. Produces a
    /// <see cref="PlayerInput"/>, so bots drive the exact same movement path as humans.
    /// </summary>
    /// <remarks>
    /// No [BurstCompile] here: Think returns PlayerInput (contains a bool), which is not blittable for
    /// direct-call function pointers (BC1063 breaks Burst for the whole assembly). It is inlined into the
    /// Burst-compiled <see cref="BotBrainSystem"/> instead.
    /// </remarks>
    public static class BotBrain
    {
        public const float RetargetDistance = 1f;
        public const float ArriveDistance = 0.1f;
        public const float ArenaInset = 2f;
        public const uint MinRetargetTicks = 180;
        public const uint MaxRetargetTicks = 360;
        public const float SprintChance = 0.2f;

        public static PlayerInput Think(ref BotBrainState s, float2 position, uint tick, in ArenaBounds arena)
        {
            if (math.distance(s.Target, position) <= RetargetDistance || tick >= s.NextRetargetTick)
            {
                s.Target = s.Rng.NextFloat2(arena.Min + ArenaInset, arena.Max - ArenaInset);
                s.NextRetargetTick = tick + s.Rng.NextUInt(MinRetargetTicks, MaxRetargetTicks + 1);
                s.Sprint = s.Rng.NextFloat() < SprintChance;
            }

            float2 toTarget = s.Target - position;
            float distance = math.length(toTarget);
            return new PlayerInput
            {
                Move = distance > ArriveDistance ? toTarget / distance : float2.zero,
                Sprint = s.Sprint,
            };
        }
    }
}
