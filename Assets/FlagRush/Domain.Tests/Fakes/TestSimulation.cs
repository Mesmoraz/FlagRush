using System.Collections.Generic;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Ports;
using FlagRush.Domain.Registry;
using FlagRush.Domain.Simulation;

namespace FlagRush.Domain.Tests.Fakes
{
    public sealed class PingInput : IInput
    {
        public PingInput(PlayerId player, Tick tick) { Player = player; Tick = tick; }
        public PlayerId Player { get; }
        public Tick Tick { get; }
    }

    public sealed class InputAcceptedEvent : GameEvent
    {
        public InputAcceptedEvent(Tick tick, PlayerId player) : base(tick) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>A rule set that acknowledges every input. Enough to prove inputs flow to events.</summary>
    public sealed class EchoRuleSet : IRuleSet
    {
        public string Name => "Echo";
        public int InitializeCalls { get; private set; }

        public void Initialize(IRuleContext context) => InitializeCalls++;

        public void Apply(IRuleContext context, IReadOnlyList<IInput> inputs)
        {
            foreach (var input in inputs) context.Emit(new InputAcceptedEvent(context.Tick, input.Player));
        }
    }

    public sealed class TestRuleContext : IRuleContext
    {
        public readonly List<IGameEvent> Events = new List<IGameEvent>();

        public TestRuleContext(Tick tick, IEntityRegistryWriter entities, IRelationshipWriter relationships, IRandomSource random, ILogSink log)
        {
            Tick = tick; Entities = entities; Relationships = relationships; Random = random; Log = log;
        }

        public Tick Tick { get; }
        public IEntityRegistryWriter Entities { get; }
        public IRelationshipWriter Relationships { get; }
        public IRandomSource Random { get; }
        public ILogSink Log { get; }
        public void Emit(IGameEvent gameEvent) => Events.Add(gameEvent);
    }

    /// <summary>Minimal ISimulation: one rule set over one registry and one relationship set.</summary>
    public sealed class TestSimulation : ISimulation
    {
        readonly IRuleSet _rules;
        readonly IRandomSource _random;
        readonly ILogSink _log;

        public readonly InMemoryEntityRegistry Entities = new InMemoryEntityRegistry();
        public readonly InMemoryRelationshipSet Relationships = new InMemoryRelationshipSet();

        public TestSimulation(IRuleSet rules, IRandomSource random, ILogSink log)
        {
            _rules = rules; _random = random; _log = log;
            _rules.Initialize(new TestRuleContext(Tick.Zero, Entities, Relationships, _random, _log));
        }

        public Tick CurrentTick { get; private set; } = Tick.Zero;

        public IReadOnlyList<IGameEvent> Step(Tick tick, IReadOnlyList<IInput> inputs)
        {
            var context = new TestRuleContext(tick, Entities, Relationships, _random, _log);
            _rules.Apply(context, inputs);
            CurrentTick = tick;
            return context.Events;
        }
    }
}
