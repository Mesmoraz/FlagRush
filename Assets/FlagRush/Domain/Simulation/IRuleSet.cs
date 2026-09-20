using System.Collections.Generic;

namespace FlagRush.Domain.Simulation
{
    /// <summary>
    /// A game mode. Capture-the-flag, a DarkRP-style economy, or a vehicle test range are each one rule
    /// set over the same entities, relationships and inputs. Rule sets hold no state between steps: all
    /// state lives in the context so it can be snapshotted and replicated.
    /// </summary>
    public interface IRuleSet
    {
        string Name { get; }

        /// <summary>Called once when the simulation starts, to create the initial world.</summary>
        void Initialize(IRuleContext context);

        /// <summary>Advances the world by one tick using the inputs for that tick.</summary>
        void Apply(IRuleContext context, IReadOnlyList<IInput> inputs);
    }
}
