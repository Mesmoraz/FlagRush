using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Simulation
{
    /// <summary>Convenience base for events; carries the tick and nothing else.</summary>
    public abstract class GameEvent : IGameEvent
    {
        protected GameEvent(Tick tick)
        {
            Tick = tick;
        }

        public Tick Tick { get; }

        public override string ToString() => $"{GetType().Name}@{Tick}";
    }
}
