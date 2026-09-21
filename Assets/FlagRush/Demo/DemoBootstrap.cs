using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace FlagRush.Demo
{
    /// <summary>
    /// Client + server worlds in one process, talking over the in-process IPC transport only.
    /// On Web a server cannot listen on a socket, so the default constructor's WebSocket server
    /// driver is never registered; IPC is enough for the "simulator" build.
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public class DemoBootstrap : ClientServerBootstrap
    {
        public const ushort Port = 7979;

        public override bool Initialize(string defaultWorldName)
        {
            AutoConnectPort = Port;
            if (RequestedPlayType == PlayType.ClientAndServer)
                NetworkStreamReceiveSystem.DriverConstructor = new IpcOnlyDriverConstructor();
            var ok = base.Initialize(defaultWorldName);
            DemoStats.ServerWorldExists = ServerWorld != null;
            DemoStats.ClientWorldExists = ClientWorld != null;
            Debug.Log($"[Demo] bootstrap playType={RequestedPlayType} server={DemoStats.ServerWorldExists} client={DemoStats.ClientWorldExists}");
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
