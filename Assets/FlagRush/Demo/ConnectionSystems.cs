using Unity.Entities;
using Unity.NetCode;

namespace FlagRush.Demo
{
    /// <summary>
    /// Server side: a browser tab can stall for seconds while it warms up (shader compiles, a hidden tab),
    /// so give the handshake 30 s instead of the default 5 s before the connection is dropped.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ServerTickRateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var rate = new ClientServerTickRate();
            rate.ResolveDefaults();
            rate.HandshakeApprovalTimeoutMS = 30_000;
            state.EntityManager.CreateSingleton(rate);
            state.Enabled = false;
        }
    }

    /// <summary>
    /// Client side: if the connection is ever lost (timeout, stall), ask for it again a couple of seconds
    /// later. The bootstrap only auto-connects once; this makes the demo self-healing.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ReconnectSystem : ISystem
    {
        double _nextAttempt;

        public void OnUpdate(ref SystemState state)
        {
            var now = SystemAPI.Time.ElapsedTime;
            foreach (var _ in SystemAPI.Query<RefRO<NetworkStreamConnection>>())
            {
                _nextAttempt = now + 3.0; // connected or connecting: back off
                return;
            }
            if (now < _nextAttempt) return;
            _nextAttempt = now + 3.0;
            if (!ClientServerBootstrap.HasDefaultAddressAndPortSet(out var endpoint)) return;
            SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(state.EntityManager, endpoint);
            DemoStats.LastError = $"reconnecting at t={now:F0}s";
        }
    }
}
