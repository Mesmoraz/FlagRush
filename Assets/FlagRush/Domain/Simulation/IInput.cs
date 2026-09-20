using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Simulation
{
    /// <summary>
    /// Something a participant asked the simulation to do at a tick. Concrete inputs (move, fire, interact,
    /// place machine, enter vehicle) are defined by the rule set that consumes them.
    /// </summary>
    public interface IInput
    {
        PlayerId Player { get; }
        Tick Tick { get; }
    }
}
