using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace FlagRush.Demo
{
    /// <summary>
    /// Server: at Level 4+ keeps exactly two flag ghosts (one per team, at home) and one match-state ghost alive. Below
    /// Level 4 destroys them and clears per-player match state (the worlds outlive PlayMode tests, and Level 1-3 must not
    /// see flags or a leftover dead or carrying player). Main-thread, not Burst: it reads <see cref="DemoConfig"/>.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerSpawnSystem))]
    public partial struct MatchSpawnSystem : ISystem
    {
        EntityQuery _flags;
        EntityQuery _states;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MatchGhostPrefabs>();
            state.RequireForUpdate<NetworkTime>();
            _flags = state.GetEntityQuery(ComponentType.ReadOnly<FlagGhost>());
            _states = state.GetEntityQuery(ComponentType.ReadOnly<MatchStateGhost>());
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!DemoConfig.IsMatchLevel)
            {
                if (!_flags.IsEmpty) em.DestroyEntity(_flags);
                if (!_states.IsEmpty) em.DestroyEntity(_states);
                ReviveAll(ref state);
                DemoStats.FlagsOnServer = 0;
                DemoStats.RedPlayersOnServer = 0;
                DemoStats.BluePlayersOnServer = 0;
                return;
            }

            var prefabs = SystemAPI.GetSingleton<MatchGhostPrefabs>();
            var arena = SystemAPI.GetSingleton<ActiveArena>();
            var serverTick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;

            if (_flags.CalculateEntityCount() != 2)
            {
                em.DestroyEntity(_flags);
                for (byte team = 0; team < 2; team++)
                {
                    var data = FlagData.AtHome(team, arena.Layout);
                    var position = new float3(data.Position.x, 0f, data.Position.y);
                    var flag = em.Instantiate(prefabs.Flag);
                    em.SetComponentData(flag, new FlagGhost { Team = team, State = data.State, Position = position, CarrierGhostId = 0, DroppedTick = 0 });
                    em.SetComponentData(flag, LocalTransform.FromPosition(position));
                }
            }

            if (_states.CalculateEntityCount() != 1 && serverTick.IsValid)
            {
                em.DestroyEntity(_states);
                var round = MatchRules.NewRound(serverTick.TickIndexForValidTick);
                var matchState = em.Instantiate(prefabs.State);
                em.SetComponentData(matchState, new MatchStateGhost
                {
                    RedScore = round.RedScore, BlueScore = round.BlueScore, Phase = round.Phase,
                    PhaseEndTick = round.PhaseEndTick, RoundStartTick = round.RoundStartTick,
                });
            }

            int red = 0, blue = 0;
            foreach (var combatant in SystemAPI.Query<RefRO<Combatant>>().WithAll<PlayerGhost>())
            {
                if (combatant.ValueRO.Team == 0) red++;
                else blue++;
            }
            DemoStats.FlagsOnServer = _flags.CalculateEntityCount();
            DemoStats.RedPlayersOnServer = red;
            DemoStats.BluePlayersOnServer = blue;
        }

        /// <summary>Leaving the match: nobody stays dead, carrying a flag that no longer exists, or at partial health.</summary>
        void ReviveAll(ref SystemState state)
        {
            var stale = new NativeList<Entity>(Allocator.Temp);
            foreach (var (combatant, entity) in SystemAPI.Query<RefRO<Combatant>>().WithAll<PlayerGhost>().WithEntityAccess())
            {
                var c = combatant.ValueRO;
                if (c.Carrying || c.RespawnTick != 0 || c.Health != MatchTuning.MaxHealth) stale.Add(entity);
            }
            foreach (var entity in stale)
            {
                var c = state.EntityManager.GetComponentData<Combatant>(entity);
                c.Carrying = false;
                c.RespawnTick = 0;
                c.Health = MatchTuning.MaxHealth;
                state.EntityManager.SetComponentData(entity, c);
            }
            stale.Dispose();
        }
    }

    /// <summary>
    /// Client: counts flag and match-state ghosts for the readout and derives each flag's LocalTransform from
    /// <see cref="FlagGhost.Position"/> (LocalTransform itself is not replicated).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GoInGameSystem))]
    public partial struct MatchClientStatsSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            int flags = 0;
            foreach (var (flag, transform) in SystemAPI.Query<RefRO<FlagGhost>, RefRW<LocalTransform>>())
            {
                flags++;
                transform.ValueRW.Position = flag.ValueRO.Position;
            }
            int states = 0;
            foreach (var _ in SystemAPI.Query<RefRO<MatchStateGhost>>()) states++;
            DemoStats.FlagsOnClient = flags;
            DemoStats.MatchStateOnClient = states > 0;
        }
    }
}
