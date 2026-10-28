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

        /// <summary>Role: every third bot (index % 3 == 0) defends, the rest attack.</summary>
        public static bool IsDefender(int botIndex) => botIndex % 3 == 0;

        /// <summary>
        /// Ruling R19: while <paramref name="ownFlag"/> is carried, only the <see cref="MatchTuning.BotCarrierHunters"/>
        /// alive bots of this team nearest to it hunt the carrier; every other bot keeps its normal role. Rank = number of
        /// alive teammate bots strictly nearer to the flag (ties broken by lower id), so the set is deterministic and
        /// each bot can decide on its own without a shared list.
        /// </summary>
        public static bool HuntsCarrier(in PlayerView self, in FlagData ownFlag, NativeArray<PlayerView> players)
        {
            if (ownFlag.State != FlagState.Carried) return false;
            float own = math.distancesq(self.Position, ownFlag.Position);
            int nearer = 0;
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (!p.Alive || !p.Bot || p.Team != self.Team || p.Id == self.Id) continue;
                float d = math.distancesq(p.Position, ownFlag.Position);
                if (d < own || (d == own && p.Id < self.Id))
                    if (++nearer >= MatchTuning.BotCarrierHunters) return false;
            }
            return true;
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
            if (HuntsCarrier(self, ownFlag, players))
            {
                target = ownFlag.Position;
                return BotMatchState.Hunt;
            }

            if (!IsDefender(botIndex))
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
                // Waypoint = the blocking box's corner (pushed out along its diagonal) on the shortest path
                // position -> corner -> target that the bot can actually walk to. Corners it already stands at
                // (within 0.5 m) are skipped, so it moves on to the next corner instead of orbiting one.
                var b = layout.Boxes[box];
                float2 best = default;
                float bestCost = float.MaxValue;
                float2 fallback = default;
                float fallbackDistance = float.MaxValue;
                for (int c = 0; c < 4; c++)
                {
                    float2 sign = new float2((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f);
                    float2 waypoint = b.Center + b.HalfExtents * sign + math.normalize(sign) * MatchTuning.BotCornerMargin;
                    float toWaypoint = math.distance(position, waypoint);
                    if (toWaypoint < fallbackDistance) { fallbackDistance = toWaypoint; fallback = waypoint; }
                    if (toWaypoint < 0.5f) continue;
                    if (Obstacles.SegmentHit(position, waypoint, layout, out _, out _)) continue;
                    float cost = toWaypoint + math.distance(waypoint, target);
                    if (cost >= bestCost) continue;
                    bestCost = cost;
                    best = waypoint;
                }
                goal = bestCost < float.MaxValue ? best : fallback;
            }
            float2 to = goal - position;
            float length = math.length(to);
            return length > 1e-5f ? to / length : float2.zero;
        }

        /// <summary>
        /// Match-mode think: pick a state, steer toward its target, shoot the nearest visible enemy, following the
        /// attack doctrine below (added on top of spec §3 because, without it, the two teams' attackers meet head-on at
        /// midfield and kill each other, so no flag ever moves; see fix-browser-report.md F1).
        /// <list type="bullet">
        /// <item>Wave: an attacker raiding a home flag waits in its spawn strip until its team's next wave departs
        /// (first <see cref="MatchTuning.BotWaveDepartTicks"/> of every <see cref="MatchTuning.BotWaveTicks"/>), so
        /// attackers arrive together instead of one at a time into the defenders' fire.</item>
        /// <item>Lane: while still in its own half it heads for its team's midfield lane (Red z = -14, Blue z = +14),
        /// so the two teams' waves do not run through each other.</item>
        /// <item>While raiding (enemy flag not carried) an attacker does not shoot enemy attackers, only defenders and
        /// humans. Once a flag is carried everyone shoots everyone again.</item>
        /// <item>A defender that is not hunting its flag's carrier shoots only enemies inside its own half (the zone it
        /// hunts in), so it does not mow down the other team's attackers before they cross midfield.</item>
        /// <item>Ruling R19: only the 4 nearest bots of the robbed team hunt a carrier (<see cref="HuntsCarrier"/>);
        /// the rest keep their normal role.</item>
        /// </list>
        /// </summary>
        public static PlayerInput ThinkMatch(ref BotBrainState s, int botIndex, in PlayerView self, in FlagData ownFlag,
            in FlagData enemyFlag, NativeArray<PlayerView> players, in ArenaLayout layout, uint tick)
        {
            var state = ChooseState(botIndex, self, ownFlag, enemyFlag, players, layout, out float2 target);
            bool carrying = state == BotMatchState.Return;
            bool raiding = state == BotMatchState.GoForFlag && enemyFlag.State == FlagState.Home;
            float towardEnemy = self.Team == 0 ? 1f : -1f; // +x is toward the enemy base for Red
            float progress = self.Position.x * towardEnemy;  // < 0 while in the own half
            if (raiding && progress < -1f) target = new float2(0f, -towardEnemy * MatchTuning.BotLaneZ);
            float distance = math.distance(self.Position, target);

            var input = new PlayerInput();
            bool holding = state == BotMatchState.Hold && distance <= MatchTuning.BotHoldRadius;
            bool waitingForWave = raiding && -progress >= MatchTuning.BotSpawnStripX && tick % MatchTuning.BotWaveTicks >= MatchTuning.BotWaveDepartTicks;
            if (!holding && !waitingForWave) input.Move = SteerToward(self.Position, target, layout);
            input.Sprint = !carrying && distance > MatchTuning.BotSprintDistance;

            bool ignoreAttackers = state == BotMatchState.GoForFlag && enemyFlag.State != FlagState.Carried;
            bool huntingCarrier = state == BotMatchState.Hunt && HuntsCarrier(self, ownFlag, players);
            bool ownHalfOnly = IsDefender(botIndex) && !huntingCarrier;
            int victim = -1;
            float victimDistance = MatchTuning.BotFireRange;
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (!p.Alive || p.Team == self.Team) continue;
                if (ignoreAttackers && p.Attacker) continue;
                if (ownHalfOnly && p.Position.x * towardEnemy >= 0f) continue;
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
