using System.Collections.Generic;
using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Simulation
{
    /// <summary>
    /// The pure core: given a tick and its inputs, advance and return what happened. The same shape runs
    /// authoritatively on a server, speculatively on a predicting client, and inside a unit test.
    /// </summary>
    public interface ISimulation
    {
        Tick CurrentTick { get; }

        IReadOnlyList<IGameEvent> Step(Tick tick, IReadOnlyList<IInput> inputs);
    }
}
