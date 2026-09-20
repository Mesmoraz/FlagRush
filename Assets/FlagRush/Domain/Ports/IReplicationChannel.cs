using System.Collections.Generic;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Simulation;

namespace FlagRush.Domain.Ports
{
    /// <summary>
    /// The netcode seam. The authoritative side publishes what happened each tick and drains the inputs
    /// participants sent; a client-side implementation does the mirror image. An in-process implementation
    /// is a perfectly valid channel, which is how a single-process "simulator" build works.
    /// </summary>
    public interface IReplicationChannel : IInputSource
    {
        void Publish(Tick tick, IReadOnlyList<IGameEvent> events);
    }
}
