using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Simulation
{
    /// <summary>
    /// Something that happened during a step. Events are the only output of the simulation: presentation,
    /// replication, persistence and tests all consume them instead of peeking at internal state.
    /// </summary>
    public interface IGameEvent
    {
        Tick Tick { get; }
    }
}
