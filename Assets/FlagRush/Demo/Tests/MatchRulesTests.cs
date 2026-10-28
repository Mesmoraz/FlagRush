using NUnit.Framework;
using Unity.Collections;

namespace FlagRush.Demo.Tests
{
    public class MatchRulesTests
    {
        static MatchEvent Capture(byte team, uint tick) =>
            new() { Kind = MatchEventKind.FlagCaptured, Team = team, Actor = 5, Tick = tick };

        static NativeList<MatchEvent> Step(ref MatchData m, uint tick, params MatchEvent[] thisTick)
        {
            var events = new NativeList<MatchEvent>(Allocator.Temp);
            var arr = new NativeArray<MatchEvent>(thisTick, Allocator.Temp);
            MatchRules.Step(ref m, arr, tick, events);
            arr.Dispose();
            return events;
        }

        [Test]
        public void CaptureAddsScore()
        {
            var m = MatchRules.NewRound(0);
            var ev = Step(ref m, 10, Capture(0, 10));
            Assert.AreEqual(1, m.RedScore);
            Assert.AreEqual(MatchPhase.Playing, m.Phase);
            Assert.AreEqual(0, ev.Length);
        }

        [Test]
        public void ThirdCaptureEndsRound()
        {
            var m = MatchRules.NewRound(0);
            m.RedScore = 2;
            var ev = Step(ref m, 500, Capture(0, 500));
            Assert.AreEqual(3, m.RedScore);
            Assert.AreEqual(MatchPhase.Ended, m.Phase);
            Assert.AreEqual(800u, m.PhaseEndTick);
            Assert.AreEqual(1, ev.Length);
            Assert.AreEqual(MatchEventKind.RoundEnded, ev[0].Kind);
            Assert.AreEqual(0, ev[0].Team);
        }

        [Test]
        public void TimeoutEndsRoundAsDraw()
        {
            var m = MatchRules.NewRound(100);
            m.RedScore = 1; m.BlueScore = 1;
            var ev = Step(ref m, 100 + 18000);
            Assert.AreEqual(MatchPhase.Ended, m.Phase);
            Assert.AreEqual(1, ev.Length);
            Assert.AreEqual(255, ev[0].Team);
        }

        [Test]
        public void TimeoutLeaderWins()
        {
            var m = MatchRules.NewRound(0);
            m.RedScore = 2; m.BlueScore = 1;
            var ev = Step(ref m, 18000);
            Assert.AreEqual(0, ev[0].Team);
        }

        [Test]
        public void CaptureOnTimeoutTickCountsFirst()
        {
            var m = MatchRules.NewRound(0);
            m.RedScore = 2; m.BlueScore = 2;
            var ev = Step(ref m, 18000, Capture(1, 18000));
            Assert.AreEqual(3, m.BlueScore);
            Assert.AreEqual(1, ev[0].Team);
        }

        [Test]
        public void EndedPhaseResetsAfter300Ticks()
        {
            var m = MatchRules.NewRound(0);
            m.RedScore = 2;
            Step(ref m, 500, Capture(0, 500));
            var ev = Step(ref m, m.PhaseEndTick - 1);
            Assert.AreEqual(MatchPhase.Ended, m.Phase);
            Assert.AreEqual(0, ev.Length);
            ev = Step(ref m, m.PhaseEndTick);
            Assert.AreEqual(0, m.RedScore);
            Assert.AreEqual(0, m.BlueScore);
            Assert.AreEqual(MatchPhase.Playing, m.Phase);
            Assert.AreEqual(800u, m.RoundStartTick);
            Assert.AreEqual(1, ev.Length);
            Assert.AreEqual(MatchEventKind.RoundReset, ev[0].Kind);
        }

        [Test]
        public void CapturesIgnoredWhileEnded()
        {
            var m = MatchRules.NewRound(0);
            m.Phase = MatchPhase.Ended;
            m.PhaseEndTick = 1000;
            Step(ref m, 900, Capture(0, 900));
            Assert.AreEqual(0, m.RedScore);
        }

        [Test]
        public void TeamPickBalances()
        {
            Assert.AreEqual(0, MatchTeams.Pick(0, 0));
            Assert.AreEqual(1, MatchTeams.Pick(1, 0));
            Assert.AreEqual(1, MatchTeams.Pick(3, 2));
            Assert.AreEqual(0, MatchTeams.Pick(2, 3));
        }
    }
}
