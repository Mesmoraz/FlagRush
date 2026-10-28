using Unity.Collections;
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

    public enum BotMatchState : byte { Return, Hunt, GoForFlag, Hold }

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

        /// <summary>Picks the bot's match state and where it wants to go. Defender = every third bot (index % 3 == 0).</summary>
        public static BotMatchState ChooseState(int botIndex, in PlayerView self, in FlagData ownFlag, in FlagData enemyFlag,
            NativeArray<PlayerView> players, in ArenaLayout layout, out float2 target)
        {
            if (enemyFlag.State == FlagState.Carried && enemyFlag.CarrierId == self.Id)
            {
                target = layout.Base(self.Team);
                return BotMatchState.Return;
            }
            if (ownFlag.State == FlagState.Carried)
            {
                target = ownFlag.Position;
                return BotMatchState.Hunt;
            }

            bool defender = botIndex % 3 == 0;
            if (!defender)
            {
                target = enemyFlag.Position;
                return BotMatchState.GoForFlag;
            }

            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (!p.Alive || p.Team == self.Team) continue;
                bool inOwnHalf = self.Team == 0 ? p.Position.x < 0f : p.Position.x > 0f;
                if (!inOwnHalf) continue;
                float d = math.distance(self.Position, p.Position);
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = i;
            }
            if (best >= 0)
            {
                target = players[best].Position;
                return BotMatchState.Hunt;
            }

            float2 stand = layout.Base(self.Team);
            target = math.distance(self.Position, stand) <= MatchTuning.BotHoldRadius ? self.Position : stand;
            return BotMatchState.Hold;
        }

        /// <summary>Unit direction toward <paramref name="target"/>, detouring around a box that blocks the straight line.</summary>
        public static float2 SteerToward(float2 position, float2 target, in ArenaLayout layout)
        {
            float2 goal = target;
            if (Obstacles.SegmentHit(position, target, layout, out _, out int box))
            {
                var b = layout.Boxes[box];
                float2 best = default;
                float bestDistance = float.MaxValue;
                for (int c = 0; c < 4; c++)
                {
                    float2 sign = new float2((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f);
                    float2 corner = b.Center + b.HalfExtents * sign;
                    float d = math.distance(position, corner);
                    if (d >= bestDistance) continue;
                    bestDistance = d;
                    best = corner + math.normalize(sign) * MatchTuning.BotCornerMargin;
                }
                goal = best;
            }
            float2 to = goal - position;
            float length = math.length(to);
            return length > 1e-5f ? to / length : float2.zero;
        }

        /// <summary>Match-mode think: pick a state, steer toward its target, shoot the nearest visible enemy.</summary>
        public static PlayerInput ThinkMatch(ref BotBrainState s, int botIndex, in PlayerView self, in FlagData ownFlag,
            in FlagData enemyFlag, NativeArray<PlayerView> players, in ArenaLayout layout)
        {
            var state = ChooseState(botIndex, self, ownFlag, enemyFlag, players, layout, out float2 target);
            bool carrying = state == BotMatchState.Return;
            float distance = math.distance(self.Position, target);

            var input = new PlayerInput();
            bool holding = state == BotMatchState.Hold && distance <= MatchTuning.BotHoldRadius;
            if (!holding) input.Move = SteerToward(self.Position, target, layout);
            input.Sprint = !carrying && distance > MatchTuning.BotSprintDistance;

            int victim = -1;
            float victimDistance = MatchTuning.BotFireRange;
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (!p.Alive || p.Team == self.Team) continue;
                float d = math.distance(self.Position, p.Position);
                if (d > victimDistance) continue;
                if (Obstacles.SegmentHit(self.Position, p.Position, layout, out _, out _)) continue;
                victimDistance = d;
                victim = i;
            }
            if (victim >= 0 && victimDistance > 1e-4f)
            {
                float2 dir = (players[victim].Position - self.Position) / victimDistance;
                float angle = math.radians(s.Rng.NextFloat(-MatchTuning.BotAimErrorDegrees, MatchTuning.BotAimErrorDegrees));
                float sin = math.sin(angle), cos = math.cos(angle);
                input.Aim = new float2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos);
                input.Fire = true;
            }
            return input;
        }
    }
}
