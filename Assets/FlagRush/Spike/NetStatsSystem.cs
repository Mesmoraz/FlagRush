using Unity.Entities;
using Unity.NetCode;

namespace FlagRush.Spike
{
    /// <summary>
    /// Measures what actually crosses the transport. Runs after NetworkStreamReceiveSystem has filled the
    /// connection's IncomingSnapshotDataStreamBuffer and before GhostReceiveSystem consumes it, so the
    /// buffer length is exactly this frame's snapshot bytes.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(GhostReceiveSystem))]
    public partial struct NetStatsSystem : ISystem
    {
        long _bytesThisWindow;
        int _snapshotsThisWindow;
        double _windowStart;

        public void OnUpdate(ref SystemState state)
        {
            double now = SystemAPI.Time.ElapsedTime;
            if (_windowStart == 0) _windowStart = now;

            foreach (var (ack, buffer) in SystemAPI.Query<RefRO<NetworkSnapshotAck>, DynamicBuffer<IncomingSnapshotDataStreamBuffer>>())
            {
                if (buffer.Length > 0)
                {
                    _bytesThisWindow += buffer.Length;
                    _snapshotsThisWindow++;
                }
                SpikeStats.PacketLossPercent = ack.ValueRO.SnapshotPacketLoss.NetworkPacketLossPercent * 100.0;
            }

            double elapsed = now - _windowStart;
            if (elapsed >= 1.0)
            {
                SpikeStats.SnapshotBytesPerSecond = (float)(_bytesThisWindow / elapsed);
                SpikeStats.SnapshotsPerSecond = (float)(_snapshotsThisWindow / elapsed);
                SpikeStats.AvgSnapshotBytes = _snapshotsThisWindow > 0 ? (float)_bytesThisWindow / _snapshotsThisWindow : 0f;
                _bytesThisWindow = 0;
                _snapshotsThisWindow = 0;
                _windowStart = now;
            }
        }
    }
}
