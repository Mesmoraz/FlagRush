using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Ports
{
    /// <summary>Source of simulation time. Fixed-rate by contract so ticks map to seconds deterministically.</summary>
    public interface IClock
    {
        Tick Now { get; }

        /// <summary>Ticks per second.</summary>
        int TickRate { get; }

        float SecondsPerTick => 1f / TickRate;
    }
}
