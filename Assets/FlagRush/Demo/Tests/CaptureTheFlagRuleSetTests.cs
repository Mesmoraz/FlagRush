using System.Collections.Generic;
using System.Linq;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Primitives;
using FlagRush.Domain.Registry;
using FlagRush.Domain.Simulation;
using NUnit.Framework;
using Unity.Mathematics;

namespace FlagRush.Demo.Tests
{
    public class CaptureTheFlagRuleSetTests
    {
        static readonly EntityId RedFlag = new EntityId(CaptureTheFlagRuleSet.RedFlagId);

        static FakeRuleContext NewContext(IRuleSet rules)
        {
            var ctx = new FakeRuleContext();
            rules.Initialize(ctx);
            ctx.Entities.Add(new FakeAvatar(2, new TeamId(2), new Point3(25f, 0f, 20f)));
            return ctx;
        }

        static void Step(IRuleSet rules, FakeRuleContext ctx, uint tick, float x, float z)
        {
            ctx.Tick = new Tick(tick);
            rules.Apply(ctx, new IInput[] { new MoveTo { Player = new PlayerId(2), Tick = ctx.Tick, Position = new float2(x, z) } });
        }

        static List<MatchEvent> Events(FakeRuleContext ctx) =>
            ctx.Emitted.Cast<MatchGameEvent>().Select(e => e.Event).ToList();

        [Test]
        public void ScriptedCaptureThroughRuleSetContract()
        {
            var rules = new CaptureTheFlagRuleSet();
            var ctx = NewContext(rules);
            var carries = new Relationship(RelationshipKind.Carries, new EntityId(2), RedFlag);

            Step(rules, ctx, 1, -25f, 0f);
            Assert.IsTrue(ctx.Relationships.Contains(carries));
            var ev = Events(ctx);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(MatchEventKind.FlagTaken, ev[0].Kind);
            Assert.AreEqual(2, ev[0].Actor);

            Step(rules, ctx, 2, 0f, 7f);
            Assert.IsTrue(ctx.Relationships.Contains(carries));
            Assert.AreEqual(1, ctx.Relationships.Count);

            Step(rules, ctx, 3, 25f, 0f);
            ev = Events(ctx);
            Assert.AreEqual(MatchEventKind.FlagCaptured, ev[ev.Count - 1].Kind);
            Assert.AreEqual(1, ev[ev.Count - 1].Team);
            Assert.IsFalse(ctx.Relationships.Contains(carries));
            Assert.AreEqual(0, ctx.Relationships.Count);
        }

        [Test]
        public void RuleSetHoldsNoStateBetweenSteps()
        {
            var script = new[] { (1u, -25f, 0f), (2u, 0f, 7f), (3u, 25f, 0f), (4u, 25f, 0f) };

            var single = new CaptureTheFlagRuleSet();
            var ctxA = NewContext(single);
            foreach (var (t, x, z) in script) Step(single, ctxA, t, x, z);

            var one = new CaptureTheFlagRuleSet();
            var two = new CaptureTheFlagRuleSet();
            var ctxB = NewContext(one);
            for (int i = 0; i < script.Length; i++)
                Step(i % 2 == 0 ? one : two, ctxB, script[i].Item1, script[i].Item2, script[i].Item3);

            var a = Events(ctxA);
            var b = Events(ctxB);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Kind, b[i].Kind);
                Assert.AreEqual(a[i].Tick, b[i].Tick);
                Assert.AreEqual(a[i].Actor, b[i].Actor);
                Assert.AreEqual(a[i].Team, b[i].Team);
            }
        }
    }
}
