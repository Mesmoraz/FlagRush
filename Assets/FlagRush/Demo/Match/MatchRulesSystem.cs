using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace FlagRush.Demo
{
    /// <summary>
    /// Server, after <see cref="HitscanSystem"/>: runs the flag and round rules once per tick and applies their events.
    /// <list type="bullet">
    /// <item><see cref="FlagRules.Step"/> (only while Playing) with every player as a <see cref="PlayerView"/>
    /// (Id = <see cref="GhostInstance.ghostId"/>), then <see cref="MatchRules.Step"/> with this tick's flag events.</item>
    /// <item>Writes <see cref="FlagGhost"/> (+ LocalTransform) and <see cref="MatchStateGhost"/>; sets
    /// <see cref="Combatant.Carrying"/> from each flag's carrier; Captures++ on a FlagCaptured actor.</item>
    /// <item>Respawns players whose <see cref="Combatant.RespawnTick"/> is due: full health, teleported into the team's
    /// spawn zone. On RoundReset every player is revived and teleported and both flags go home;
    /// <see cref="MatchStats"/> is per session and is kept.</item>
    /// </list>
    /// Teleports write PlayerGhost.Position, Velocity = 0, LocalTransform and ServerPosition.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(HitscanSystem))]
    [BurstCompile]
    public partial struct MatchRulesSystem : ISystem
    {
        int _respawnSlot;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActiveArena>();
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<MatchCounters>();
            state.RequireForUpdate<MatchStateGhost>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var arena = SystemAPI.GetSingleton<ActiveArena>();
            if (!arena.Match) return;
            var layout = arena.Layout;
            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick;

            // Players, in query order (the write-back pass below uses the same query shape).
            var views = new NativeList<PlayerView>(Allocator.Temp);
            foreach (var (ghost, combatant, _, _, _, instance) in SystemAPI
                         .Query<RefRW<PlayerGhost>, RefRW<Combatant>, RefRW<MatchStats>, RefRW<LocalTransform>, RefRW<ServerPosition>, RefRO<GhostInstance>>()
                         .WithAll<Simulate>())
            {
                views.Add(MatchViews.ToView(ghost.ValueRO, combatant.ValueRO, instance.ValueRO, views.Length));
            }

            FlagData red = default, blue = default;
            bool haveRed = false, haveBlue = false;
            foreach (var flag in SystemAPI.Query<RefRO<FlagGhost>>())
            {
                var f = MatchViews.ToData(flag.ValueRO);
                if (f.Team == 0) { red = f; haveRed = true; }
                else { blue = f; haveBlue = true; }
            }
            bool haveFlags = haveRed && haveBlue;

            var matchState = SystemAPI.GetSingletonRW<MatchStateGhost>();
            var m = ToData(matchState.ValueRO);

            var flagEvents = new NativeList<MatchEvent>(Allocator.Temp);
            if (m.Phase == MatchPhase.Playing && haveFlags)
                FlagRules.Step(ref red, ref blue, views.AsArray(), layout, tick, flagEvents);

            var roundEvents = new NativeList<MatchEvent>(Allocator.Temp);
            MatchRules.Step(ref m, flagEvents.AsArray(), tick, roundEvents);

            bool reset = false;
            for (int e = 0; e < roundEvents.Length; e++)
                if (roundEvents[e].Kind == MatchEventKind.RoundReset) reset = true;
            if (reset)
            {
                red = FlagData.AtHome(0, layout);
                blue = FlagData.AtHome(1, layout);
            }

            int i = 0, redSlot = 0, blueSlot = 0;
            foreach (var (ghost, combatant, stats, transform, serverPosition, _) in SystemAPI
                         .Query<RefRW<PlayerGhost>, RefRW<Combatant>, RefRW<MatchStats>, RefRW<LocalTransform>, RefRW<ServerPosition>, RefRO<GhostInstance>>()
                         .WithAll<Simulate>())
            {
                int id = views[i++].Id;
                var c = combatant.ValueRO;

                for (int e = 0; e < flagEvents.Length; e++)
                    if (flagEvents[e].Kind == MatchEventKind.FlagCaptured && flagEvents[e].Actor == id) stats.ValueRW.Captures++;

                c.Carrying = (red.State == FlagState.Carried && red.CarrierId == id)
                             || (blue.State == FlagState.Carried && blue.CarrierId == id);

                bool respawn = false;
                int slot = 0;
                if (reset)
                {
                    respawn = true;
                    slot = c.Team == 0 ? redSlot++ : blueSlot++;
                }
                else if (!c.Alive && (int)(tick - c.RespawnTick) >= 0)
                {
                    respawn = true;
                    slot = _respawnSlot++;
                }

                if (respawn)
                {
                    c.Health = MatchTuning.MaxHealth;
                    c.RespawnTick = 0;
                    c.Carrying = false;
                    var at = ArenaLayout.SpawnPoint(c.Team, slot);
                    ghost.ValueRW.Position = at;
                    ghost.ValueRW.Velocity = float2.zero;
                    transform.ValueRW.Position = at;
                    serverPosition.ValueRW.Value = at;
                }
                combatant.ValueRW = c;
            }
            if (_respawnSlot >= ArenaLayout.SpawnSlots) _respawnSlot = 0;

            if (haveFlags)
            {
                foreach (var (flag, transform) in SystemAPI.Query<RefRW<FlagGhost>, RefRW<LocalTransform>>())
                {
                    var f = flag.ValueRO.Team == 0 ? red : blue;
                    var position = new float3(f.Position.x, 0f, f.Position.y);
                    flag.ValueRW = new FlagGhost
                    {
                        Team = f.Team, State = f.State, Position = position, CarrierGhostId = f.CarrierId, DroppedTick = f.DroppedTick,
                    };
                    transform.ValueRW.Position = position;
                }
            }

            matchState.ValueRW = new MatchStateGhost
            {
                RedScore = m.RedScore, BlueScore = m.BlueScore, Phase = m.Phase, PhaseEndTick = m.PhaseEndTick, RoundStartTick = m.RoundStartTick,
            };

            if (flagEvents.Length > 0 || roundEvents.Length > 0)
            {
                var records = SystemAPI.GetSingletonBuffer<MatchEventRecord>();
                for (int e = 0; e < flagEvents.Length; e++) records.Add(new MatchEventRecord { Value = flagEvents[e] });
                for (int e = 0; e < roundEvents.Length; e++) records.Add(new MatchEventRecord { Value = roundEvents[e] });
            }

            views.Dispose();
            flagEvents.Dispose();
            roundEvents.Dispose();
        }

        static MatchData ToData(in MatchStateGhost g) => new()
        {
            RedScore = g.RedScore, BlueScore = g.BlueScore, Phase = g.Phase, PhaseEndTick = g.PhaseEndTick, RoundStartTick = g.RoundStartTick,
        };
    }
}
