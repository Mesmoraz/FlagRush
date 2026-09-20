using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace FlagRush.Spike
{
    /// <summary>
    /// Client + server worlds in one process, talking over the in-process IPC transport only.
    /// On Web a server cannot listen on a socket, so the default constructor's WebSocket server
    /// driver is never registered; IPC is enough for the "simulator" build.
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public class SpikeBootstrap : ClientServerBootstrap
    {
        public const ushort Port = 7979;

        public override bool Initialize(string defaultWorldName)
        {
            AutoConnectPort = Port;
            if (RequestedPlayType == PlayType.ClientAndServer)
                NetworkStreamReceiveSystem.DriverConstructor = new IpcOnlyDriverConstructor();
            var ok = base.Initialize(defaultWorldName);
            SpikeStats.ServerWorldExists = ServerWorld != null;
            SpikeStats.ClientWorldExists = ClientWorld != null;
            Debug.Log($"[Spike] bootstrap playType={RequestedPlayType} server={SpikeStats.ServerWorldExists} client={SpikeStats.ClientWorldExists}");
            return ok;
        }
    }

    public sealed class IpcOnlyDriverConstructor : INetworkStreamDriverConstructor
    {
        public void CreateClientDriver(World world, ref NetworkDriverStore driver, NetDebug netDebug) =>
            DefaultDriverBuilder.RegisterClientIpcDriver(world, ref driver, netDebug, DefaultDriverBuilder.GetNetworkClientSettings());

        public void CreateServerDriver(World world, ref NetworkDriverStore driver, NetDebug netDebug) =>
            DefaultDriverBuilder.RegisterServerIpcDriver(world, ref driver, netDebug, DefaultDriverBuilder.GetNetworkServerSettings());
    }
}
