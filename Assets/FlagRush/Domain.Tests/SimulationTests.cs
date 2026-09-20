using System.Linq;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Simulation;
using FlagRush.Domain.Tests.Fakes;
using NUnit.Framework;

namespace FlagRush.Domain.Tests
{
    public class SimulationTests
    {
        static TestSimulation NewSim(EchoRuleSet rules = null) =>
            new TestSimulation(rules ?? new EchoRuleSet(), new SeededRandom(42), new ListLog());

        [Test]
        public void RuleSetIsInitializedOnce_ThenAppliedPerStep()
        {
            var rules = new EchoRuleSet();
            var sim = NewSim(rules);
            Assert.That(rules.InitializeCalls, Is.EqualTo(1));

            sim.Step(new Tick(1), new IInput[0]);
            sim.Step(new Tick(2), new IInput[0]);

            Assert.That(rules.InitializeCalls, Is.EqualTo(1));
            Assert.That(sim.CurrentTick, Is.EqualTo(new Tick(2)));
        }

        [Test]
        public void InputsBecomeEventsStampedWithTheStepTick()
        {
            var sim = NewSim();
            var tick = new Tick(7);
            var inputs = new IInput[] { new PingInput(new PlayerId(1), tick), new PingInput(new PlayerId(2), tick) };

            var events = sim.Step(tick, inputs);

            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events.All(e => e.Tick == tick), Is.True);
            Assert.That(events.Cast<InputAcceptedEvent>().Select(e => e.Player.Value), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void StepWithNoInputsProducesNoEvents()
        {
            var sim = NewSim();
            Assert.That(sim.Step(new Tick(1), new IInput[0]), Is.Empty);
        }

        [Test]
        public void SameSeedSameDraws_SoServerAndPredictingClientAgree()
        {
            var a = new SeededRandom(1234);
            var b = new SeededRandom(1234);
            var drawsA = Enumerable.Range(0, 16).Select(_ => a.NextInt(0, 1000)).ToList();
            var drawsB = Enumerable.Range(0, 16).Select(_ => b.NextInt(0, 1000)).ToList();
            Assert.That(drawsA, Is.EqualTo(drawsB));
        }

        [Test]
        public void ReplicationChannelIsJustAnotherInputSource()
        {
            var channel = new LoopbackChannel();
            var sim = NewSim();
            channel.Send(new PingInput(new PlayerId(9), new Tick(3)));
            channel.Send(new PingInput(new PlayerId(9), new Tick(5)));

            var tick = new Tick(3);
            var events = sim.Step(tick, channel.Collect(tick));
            channel.Publish(tick, events);

            Assert.That(events.Count, Is.EqualTo(1), "only inputs at or before the tick are consumed");
            Assert.That(channel.Published.Single().Tick, Is.EqualTo(tick));
            Assert.That(channel.Collect(new Tick(5)).Count, Is.EqualTo(1), "the later input is still queued");
        }

        [Test]
        public void TickArithmeticIsOrdered()
        {
            var t = new Tick(10);
            Assert.That(t.Next(), Is.EqualTo(new Tick(11)));
            Assert.That(t.Add(5) > t, Is.True);
            Assert.That(Tick.Zero < t, Is.True);
        }
    }
}
