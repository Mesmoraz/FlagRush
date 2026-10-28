using Unity.Entities;
using Unity.NetCode;

namespace FlagRush.Demo
{
    /// <summary>Server singleton: total shots resolved since the world started (wrapping).</summary>
    public struct MatchCounters : IComponentData
    {
        public uint ShotsTotal;
    }

    /// <summary>
    /// On the <see cref="MatchCounters"/> entity: match events appended by <see cref="HitscanSystem"/> and
    /// <see cref="MatchRulesSystem"/>, drained once per frame by <see cref="MatchServerStatsSystem"/>.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct MatchEventRecord : IBufferElementData
    {
        public MatchEvent Value;
    }

    /// <summary>
    /// Server, managed, after the prediction loop: publishes <see cref="DemoStats.ShotsPerSecond"/> (MatchCounters
    /// delta over 1 s windows) and drains the <see cref="MatchEventRecord"/> buffer into
    /// <see cref="DemoStats.LastMatchEvents"/>. Below Level 4 both read empty.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PredictedSimulationSystemGroup))]
    public partial class MatchServerStatsSystem : SystemBase
    {
        public const double WindowSeconds = 1.0;

        double _windowStart = -1;
        uint _windowShots;

        protected override void OnCreate()
        {
            var counters = EntityManager.CreateEntity(typeof(MatchCounters), typeof(MatchEventRecord));
            EntityManager.SetName(counters, "MatchCounters");
        }

        protected override void OnUpdate()
        {
            var records = SystemAPI.GetSingletonBuffer<MatchEventRecord>();
            if (!DemoConfig.IsMatchLevel)
            {
                records.Clear();
                _windowStart = -1;
                DemoStats.ShotsPerSecond = 0f;
                DemoStats.LastMatchEvents.Clear();
                return;
            }

            for (int i = 0; i < records.Length; i++) DemoStats.LastMatchEvents.Add(records[i].Value);
            records.Clear();

            uint shots = SystemAPI.GetSingleton<MatchCounters>().ShotsTotal;
            double now = SystemAPI.Time.ElapsedTime;
            if (_windowStart < 0)
            {
                _windowStart = now;
                _windowShots = shots;
                return;
            }
            double elapsed = now - _windowStart;
            if (elapsed < WindowSeconds) return;
            DemoStats.ShotsPerSecond = (float)((shots - _windowShots) / elapsed);
            _windowStart = now;
            _windowShots = shots;
        }
    }
}
