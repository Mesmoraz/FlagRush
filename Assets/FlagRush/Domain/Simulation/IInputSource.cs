using System.Collections.Generic;
using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Simulation
{
    /// <summary>
    /// Where the inputs for a tick come from: a local input device, a network channel, a bot brain, or a
    /// recorded replay. The simulation does not care which.
    /// </summary>
    public interface IInputSource
    {
        IReadOnlyList<IInput> Collect(Tick tick);
    }
}
