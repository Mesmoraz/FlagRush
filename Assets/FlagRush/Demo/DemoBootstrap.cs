using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
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
            Debug.Log($"[Demo] bootstrap playType={RequestedPlayType} server={DemoStats.ServerWorldExists} client={DemoStats.ClientWorldExists} level={DemoConfig.Level}");
            return ok;
        }
    }

    /// <summary>
    /// IPC on both sides. The client driver also gets a transport <see cref="SimulatorPipelineStage"/> in
    /// every pipeline, so the sandbox can add latency, jitter and loss to the in-process link at runtime.
    /// </summary>
    public sealed class IpcOnlyDriverConstructor : INetworkStreamDriverConstructor
    {
        public static SimulatorUtility.Parameters LinkParameters(int latencyMs, int jitterMs, int lossPercent) => new SimulatorUtility.Parameters
        {
            MaxPacketCount = Sandbox.SimulatorPacketBuffer,
            MaxPacketSize = 1500 - 20 - 8, // transport's internal AbsoluteMaxMessageSize (Ethernet MTU minus IPv4/UDP headers)
            Mode = ApplyMode.AllPackets,
            PacketDelayMs = latencyMs,
            PacketJitterMs = jitterMs,
            PacketDropPercentage = lossPercent,
        };

        public void CreateClientDriver(World world, ref NetworkDriverStore driver, NetDebug netDebug)
        {
            var settings = DefaultDriverBuilder.GetNetworkClientSettings();
            var link = LinkParameters(Sandbox.LatencyMs, Sandbox.JitterMs, Sandbox.LossPercent);
            settings.WithSimulatorStageParameters(link.MaxPacketCount, link.MaxPacketSize, link.Mode, link.PacketDelayMs, link.PacketJitterMs, 0, link.PacketDropPercentage);

            var instance = new NetworkDriverStore.NetworkDriverInstance { driver = NetworkDriver.Create(new IPCNetworkInterface(), settings) };
            instance.unreliablePipeline = instance.driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage), typeof(SimulatorPipelineStage));
            instance.reliablePipeline = instance.driver.CreatePipeline(typeof(ReliableSequencedPipelineStage), typeof(SimulatorPipelineStage));
            instance.unreliableFragmentedPipeline = instance.driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(SimulatorPipelineStage));
            driver.RegisterDriver(TransportType.IPC, instance);
            Sandbox.LinkVersionApplied = Sandbox.LinkVersion;
        }

        public void CreateServerDriver(World world, ref NetworkDriverStore driver, NetDebug netDebug) =>
            DefaultDriverBuilder.RegisterServerIpcDriver(world, ref driver, netDebug, DefaultDriverBuilder.GetNetworkServerSettings());
    }
}
