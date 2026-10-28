using Unity.Collections;
using Unity.Mathematics;

namespace FlagRush.Demo
{
    /// <summary>Plain flag data. Position is the home stand, the carrier's position, or the drop point.</summary>
    public struct FlagData
    {
        public byte Team;
        public FlagState State;
        public float2 Position;
        public int CarrierId;
        public uint DroppedTick;

        public static FlagData AtHome(byte team, in ArenaLayout layout) =>
            new() { Team = team, State = FlagState.Home, Position = layout.Base(team) };
    }

    /// <summary>Pure flag rules (pickup, drop, return, capture). No [BurstCompile]; inlined into Burst systems.</summary>
    public static class FlagRules
    {
        const float DropRadius = 0.3f;

        public static void Step(ref FlagData red, ref FlagData blue, NativeArray<PlayerView> players,
            in ArenaLayout layout, uint tick, NativeList<MatchEvent> events)
        {
            StepFlag(ref red, players, layout, tick, events);
            StepFlag(ref blue, players, layout, tick, events);
            TryCapture(ref red, in blue, layout, tick, events);
            TryCapture(ref blue, in red, layout, tick, events);
        }

        static void StepFlag(ref FlagData f, NativeArray<PlayerView> players, in ArenaLayout layout, uint tick, NativeList<MatchEvent> events)
        {
            if (f.State == FlagState.Carried)
            {
                int c = IndexOf(players, f.CarrierId);
                if (c < 0 || !players[c].Alive)
                {
                    // A dead carrier drops where it fell; a missing one drops at the last known position.
                    float2 at = c >= 0 ? players[c].Position : f.Position;
                    events.Add(new MatchEvent { Kind = MatchEventKind.FlagDropped, Tick = tick, Actor = f.CarrierId, Team = f.Team });
                    f.State = FlagState.Dropped;
                    f.Position = Obstacles.PushOut(at, DropRadius, layout);
                    f.DroppedTick = tick;
                    f.CarrierId = 0;
                }
                else
                {
                    f.Position = players[c].Position;
                }
                return;
            }

            if (f.State == FlagState.Dropped)
            {
                if (tick - f.DroppedTick >= MatchTuning.AutoReturnTicks)
                {
                    SendHome(ref f, 0, layout, tick, events);
                    return;
                }
                int mate = Nearest(players, f.Position, f.Team, true);
                if (mate >= 0)
                {
                    SendHome(ref f, players[mate].Id, layout, tick, events);
                    return;
                }
            }

            int enemy = Nearest(players, f.Position, f.Team, false);
            if (enemy >= 0)
            {
                f.State = FlagState.Carried;
                f.CarrierId = players[enemy].Id;
                f.Position = players[enemy].Position;
                events.Add(new MatchEvent { Kind = MatchEventKind.FlagTaken, Tick = tick, Actor = f.CarrierId, Team = f.Team });
            }
        }

        static void TryCapture(ref FlagData carried, in FlagData ownFlag, in ArenaLayout layout, uint tick, NativeList<MatchEvent> events)
        {
            if (carried.State != FlagState.Carried || ownFlag.State != FlagState.Home) return;
            byte scorer = (byte)(1 - carried.Team);
            if (math.distance(carried.Position, layout.Base(scorer)) > layout.BaseRadius) return;
            events.Add(new MatchEvent { Kind = MatchEventKind.FlagCaptured, Tick = tick, Actor = carried.CarrierId, Team = scorer });
            carried.State = FlagState.Home;
            carried.Position = layout.Base(carried.Team);
            carried.CarrierId = 0;
        }

        static void SendHome(ref FlagData f, int actor, in ArenaLayout layout, uint tick, NativeList<MatchEvent> events)
        {
            f.State = FlagState.Home;
            f.Position = layout.Base(f.Team);
            f.CarrierId = 0;
            events.Add(new MatchEvent { Kind = MatchEventKind.FlagReturned, Tick = tick, Actor = actor, Team = f.Team });
        }

        static int IndexOf(NativeArray<PlayerView> players, int id)
        {
            for (int i = 0; i < players.Length; i++)
                if (players[i].Id == id) return i;
            return -1;
        }

        /// <summary>Nearest alive player within pickup radius, on the flag's team (mate) or the other team; lowest Id on ties.</summary>
        static int Nearest(NativeArray<PlayerView> players, float2 at, byte flagTeam, bool mate)
        {
            int best = -1;
            float bestD = MatchTuning.PickupRadius;
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (!p.Alive || (p.Team == flagTeam) != mate) continue;
                float d = math.distance(p.Position, at);
                if (d > MatchTuning.PickupRadius) continue;
                if (best < 0 || d < bestD || (d == bestD && p.Id < players[best].Id))
                {
                    best = i;
                    bestD = d;
                }
            }
            return best;
        }
    }
}
