using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace FlagRush.Demo
{
    /// <summary>
    /// Client: publishes the prediction readouts once per frame, after the prediction loop.
    /// <list type="bullet">
    /// <item><b>Predicted ticks / frame</b>: <see cref="PredictedSimulationSystemGroup"/> iterations this frame
    /// (counted by <see cref="PredictionTickSampleSystem"/>).</item>
    /// <item><b>Rollbacks / s</b>: frames whose first predicted tick is at or before the previous frame's last
    /// predicted tick, over a 1 s window. "Last predicted tick" is the last <i>fully</i> predicted (non-partial)
    /// tick: Netcode resumes a partial tick every frame, so comparing against a partial tick would count nearly
    /// every frame as a rollback.</item>
    /// <item><b>Command age</b>: <c>NetworkTime.ServerTick − NetworkSnapshotAck.LastReceivedSnapshotByLocal</c> in
    /// ticks, and in ms at the simulation tick rate. Not <c>ServerCommandAge</c>, which is clock-sync feedback.</item>
    /// <item><b>Misprediction</b>: distance between the owned ghost's <see cref="ServerPosition"/> and the position
    /// this client predicted for <c>LastReceivedSnapshotByLocal</c> (<see cref="PredictionHistory"/>, recorded by
    /// <see cref="PredictionHistoryRecordSystem"/>). 0 when the owned ghost is not predicted.</item>
    /// </list>
    /// Everything reads 0 below Level 3.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PredictedSimulationSystemGroup))]
    public partial class PredictionStatsSystem : SystemBase
    {
        public const double RollbackWindowSeconds = 1.0;

        // Written by the two samplers inside the prediction loop.
        internal PredictionHistory History = new PredictionHistory(64);
        internal NetworkTick LastFullPredictedTick;
        internal int TicksThisFrame;
        internal int RollbacksThisWindow;

        Entity _historyOwner;
        double _windowStart = -1;

        protected override void OnUpdate()
        {
            if (!DemoConfig.IsPlayerLevel)
            {
                ResetAll();
                return;
            }

            DemoStats.PredictedTicksPerFrame = TicksThisFrame;
            TicksThisFrame = 0;

            double now = SystemAPI.Time.ElapsedTime;
            if (_windowStart < 0) _windowStart = now;
            double elapsed = now - _windowStart;
            if (elapsed >= RollbackWindowSeconds)
            {
                DemoStats.RollbacksPerSecond = (float)(RollbacksThisWindow / elapsed);
                RollbacksThisWindow = 0;
                _windowStart = now;
            }

            var lastSnapshot = NetworkTick.Invalid;
            foreach (var ack in SystemAPI.Query<RefRO<NetworkSnapshotAck>>().WithAll<NetworkId>())
                lastSnapshot = ack.ValueRO.LastReceivedSnapshotByLocal;

            var serverTick = SystemAPI.TryGetSingleton<NetworkTime>(out var time) ? time.ServerTick : NetworkTick.Invalid;
            if (serverTick.IsValid && lastSnapshot.IsValid)
            {
                int age = math.max(0, serverTick.TicksSince(lastSnapshot));
                int rate = SystemAPI.TryGetSingleton<ClientServerTickRate>(out var tickRate) && tickRate.SimulationTickRate > 0
                    ? tickRate.SimulationTickRate : 60;
                DemoStats.CommandAgeTicks = age;
                DemoStats.CommandAgeMs = age * 1000f / rate;
            }
            else
            {
                DemoStats.CommandAgeTicks = 0;
                DemoStats.CommandAgeMs = 0f;
            }

            var owned = DemoStats.OwnedPlayerEntity;
            if (owned != _historyOwner)
            {
                History.Clear();
                _historyOwner = owned;
            }

            float error = 0f;
            if (owned != Entity.Null && EntityManager.Exists(owned) && SystemAPI.HasComponent<PredictedGhost>(owned)
                && SystemAPI.HasComponent<ServerPosition>(owned) && lastSnapshot.IsValid
                && History.TryGet(lastSnapshot.TickIndexForValidTick, out var predicted))
            {
                error = math.distance(SystemAPI.GetComponent<ServerPosition>(owned).Value, predicted);
            }
            DemoStats.MispredictionMeters = error;
        }

        void ResetAll()
        {
            History.Clear();
            _historyOwner = Entity.Null;
            LastFullPredictedTick = NetworkTick.Invalid;
            TicksThisFrame = 0;
            RollbacksThisWindow = 0;
            _windowStart = -1;
            DemoStats.PredictedTicksPerFrame = 0;
            DemoStats.RollbacksPerSecond = 0f;
            DemoStats.CommandAgeTicks = 0;
            DemoStats.CommandAgeMs = 0f;
            DemoStats.MispredictionMeters = 0f;
        }
    }

    /// <summary>
    /// Client, first system of every prediction iteration: counts iterations and detects a rollback on the first
    /// iteration of the frame (its tick is not newer than the last fully predicted tick of earlier frames).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderFirst = true)]
    public partial class PredictionTickSampleSystem : SystemBase
    {
        PredictionStatsSystem _stats;

        protected override void OnCreate() => RequireForUpdate<NetworkTime>();

        protected override void OnUpdate()
        {
            if (!DemoConfig.IsPlayerLevel) return;
            _stats ??= World.GetExistingSystemManaged<PredictionStatsSystem>();
            if (_stats == null) return;

            var time = SystemAPI.GetSingleton<NetworkTime>();
            _stats.TicksThisFrame++;
            if (time.IsFirstPredictionTick && _stats.LastFullPredictedTick.IsValid && time.ServerTick.IsValid
                && !time.ServerTick.IsNewerThan(_stats.LastFullPredictedTick))
                _stats.RollbacksThisWindow++;
        }
    }

    /// <summary>
    /// Client, last system of every prediction iteration: on full (non-partial) ticks, records the owned ghost's
    /// predicted position into <see cref="PredictionStatsSystem.History"/> and remembers the tick as the last
    /// fully predicted one. Re-simulated ticks overwrite their slot with the corrected prediction.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderLast = true)]
    public partial class PredictionHistoryRecordSystem : SystemBase
    {
        PredictionStatsSystem _stats;

        protected override void OnCreate() => RequireForUpdate<NetworkTime>();

        protected override void OnUpdate()
        {
            if (!DemoConfig.IsPlayerLevel) return;
            _stats ??= World.GetExistingSystemManaged<PredictionStatsSystem>();
            if (_stats == null) return;

            var time = SystemAPI.GetSingleton<NetworkTime>();
            if (time.IsPartialTick || !time.ServerTick.IsValid) return;
            _stats.LastFullPredictedTick = time.ServerTick;

            foreach (var ghost in SystemAPI.Query<RefRO<PlayerGhost>>().WithAll<GhostOwnerIsLocal, PredictedGhost, Simulate>())
                _stats.History.Record(time.ServerTick.TickIndexForValidTick, ghost.ValueRO.Position);
        }
    }
}
