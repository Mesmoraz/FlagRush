using FlagRush.Domain.Ids;
using FlagRush.Domain.Ports;
using FlagRush.Domain.Registry;

namespace FlagRush.Domain.Simulation
{
    /// <summary>
    /// Everything a rule set is allowed to touch during one step. Deterministic by construction: the only
    /// sources of nondeterminism (time, randomness) are injected and tick-scoped.
    /// </summary>
    public interface IRuleContext
    {
        Tick Tick { get; }
        IEntityRegistryWriter Entities { get; }
        IRelationshipWriter Relationships { get; }
        IRandomSource Random { get; }
        ILogSink Log { get; }

        /// <summary>Records an event for this step. Events are delivered after the step completes.</summary>
        void Emit(IGameEvent gameEvent);
    }
}
