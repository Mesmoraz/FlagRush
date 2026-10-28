using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace FlagRush.Demo
{
    /// <summary>
    /// Server: resolves this tick's shots. Every alive player holding <see cref="PlayerInput.Fire"/> with a non-zero
    /// <see cref="PlayerInput.Aim"/> whose fire cooldown allows it (<see cref="Combat.TryFire"/>) casts a
    /// <see cref="Hitscan.Cast"/> against the post-separation positions; a hit applies <see cref="Combat.ApplyHit"/>
    /// and a kill bumps the shooter's Kills and the target's Deaths. Shots are not predicted; the server decides hits.
    /// Skips unless the match is on and the phase is Playing.
    /// </summary>
    /// <remarks>
    /// Players are gathered into arrays first and written back in a second pass over the same query, so a tick's
    /// shots resolve in a stable order and a target killed earlier in the tick can neither be hit again nor fire.
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerSeparationSystem))]
    [BurstCompile]
    public partial struct HitscanSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerGhost>();
            state.RequireForUpdate<ActiveArena>();
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<MatchCounters>();
            state.RequireForUpdate<MatchStateGhost>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var arena = SystemAPI.GetSingleton<ActiveArena>();
            if (!arena.Match || SystemAPI.GetSingleton<MatchStateGhost>().Phase != MatchPhase.Playing) return;
            var layout = arena.Layout;
            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick;

            var positions = new NativeList<float2>(Allocator.Temp);
            var teams = new NativeList<byte>(Allocator.Temp);
            var hittable = new NativeList<bool>(Allocator.Temp);
            var aims = new NativeList<float2>(Allocator.Temp);
            var fires = new NativeList<bool>(Allocator.Temp);
            var ids = new NativeList<int>(Allocator.Temp);
            var combatants = new NativeList<Combatant>(Allocator.Temp);
            var nextFire = new NativeList<uint>(Allocator.Temp);
            var stats = new NativeList<MatchStats>(Allocator.Temp);

            foreach (var (ghost, combatant, server, input, matchStats, instance) in SystemAPI
                         .Query<RefRO<PlayerGhost>, RefRW<Combatant>, RefRW<CombatantServer>, RefRO<PlayerInput>, RefRW<MatchStats>, RefRO<GhostInstance>>()
                         .WithAll<Simulate>())
            {
                positions.Add(ghost.ValueRO.Position.xz);
                teams.Add(combatant.ValueRO.Team);
                hittable.Add(combatant.ValueRO.Alive);
                aims.Add(input.ValueRO.Aim);
                fires.Add(input.ValueRO.Fire);
                ids.Add(instance.ValueRO.ghostId);
                combatants.Add(combatant.ValueRO);
                nextFire.Add(server.ValueRO.NextFireTick);
                stats.Add(matchStats.ValueRO);
            }

            var events = new NativeList<MatchEvent>(Allocator.Temp);
            uint shots = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                // hittable doubles as "alive": a player killed earlier this tick does not fire.
                if (!hittable[i] || !fires[i] || math.lengthsq(aims[i]) < 1e-12f) continue;
                uint next = nextFire[i];
                if (!Combat.TryFire(ref next, tick)) continue;
                nextFire[i] = next;

                var shooter = stats[i];
                shooter.ShotsFired++;
                shots++;

                int t = Hitscan.Cast(positions[i], aims[i], MatchTuning.ShotRange, layout,
                    positions.AsArray(), teams.AsArray(), hittable.AsArray(), teams[i], i);
                if (t >= 0)
                {
                    var target = combatants[t];
                    bool killed = Combat.ApplyHit(ref target, tick);
                    combatants[t] = target;
                    events.Add(new MatchEvent { Kind = MatchEventKind.PlayerHit, Tick = tick, Actor = ids[i], Target = ids[t], Team = teams[i] });
                    if (killed)
                    {
                        hittable[t] = false;
                        shooter.Kills++;
                        var victim = stats[t];
                        victim.Deaths++;
                        stats[t] = victim;
                        events.Add(new MatchEvent { Kind = MatchEventKind.PlayerKilled, Tick = tick, Actor = ids[i], Target = ids[t], Team = teams[i] });
                    }
                }
                stats[i] = shooter;
            }

            int w = 0;
            foreach (var (_, combatant, server, _, matchStats, _) in SystemAPI
                         .Query<RefRO<PlayerGhost>, RefRW<Combatant>, RefRW<CombatantServer>, RefRO<PlayerInput>, RefRW<MatchStats>, RefRO<GhostInstance>>()
                         .WithAll<Simulate>())
            {
                combatant.ValueRW = combatants[w];
                server.ValueRW.NextFireTick = nextFire[w];
                matchStats.ValueRW = stats[w];
                w++;
            }

            if (shots > 0 || events.Length > 0)
            {
                SystemAPI.GetSingletonRW<MatchCounters>().ValueRW.ShotsTotal += shots;
                var records = SystemAPI.GetSingletonBuffer<MatchEventRecord>();
                for (int e = 0; e < events.Length; e++) records.Add(new MatchEventRecord { Value = events[e] });
            }

            positions.Dispose();
            teams.Dispose();
            hittable.Dispose();
            aims.Dispose();
            fires.Dispose();
            ids.Dispose();
            combatants.Dispose();
            nextFire.Dispose();
            stats.Dispose();
            events.Dispose();
        }
    }
}
