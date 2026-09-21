using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport.Utilities;

namespace FlagRush.Demo
{
    /// <summary>
    /// Client: pushes sandbox link settings into the transport simulator stage, keeps the interpolation
    /// delay in sync, and drops the connection on request (the reconnect system then heals it).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SandboxClientSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.CreateSingleton(NetworkTimeSystem.DefaultClientTickRate);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (Sandbox.LinkVersionApplied != Sandbox.LinkVersion && SystemAPI.HasSingleton<NetworkStreamDriver>())
            {
                ref var driver = ref SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW.DriverStore.GetDriverRW(NetworkDriverStore.FirstDriverId);
                driver.ModifySimulatorStageParameters(IpcOnlyDriverConstructor.LinkParameters(Sandbox.LatencyMs, Sandbox.JitterMs, Sandbox.LossPercent));
                Sandbox.LinkVersionApplied = Sandbox.LinkVersion;
            }

            ref var tickRate = ref SystemAPI.GetSingletonRW<ClientTickRate>().ValueRW;
            if (tickRate.InterpolationTimeMS != (uint)Sandbox.InterpolationMs)
                tickRate.InterpolationTimeMS = (uint)Sandbox.InterpolationMs;

            if (Sandbox.KillConnectionRequested)
            {
                Sandbox.KillConnectionRequested = false;
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                foreach (var (_, entity) in SystemAPI.Query<RefRO<NetworkStreamConnection>>().WithEntityAccess())
                    ecb.AddComponent(entity, new NetworkStreamRequestDisconnect());
                ecb.Playback(state.EntityManager);
            }
        }
    }

    /// <summary>Server: applies the network tick rate (a change forces a reconnect so the client re-reads it).</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SandboxServerSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<ClientServerTickRate>()) return;
            ref var rate = ref SystemAPI.GetSingletonRW<ClientServerTickRate>().ValueRW;
            if (rate.NetworkTickRate != Sandbox.NetworkTickRate)
            {
                rate.NetworkTickRate = Sandbox.NetworkTickRate;
                Sandbox.KillConnectionRequested = true; // tick rates are exchanged in the handshake
            }
        }
    }
}
