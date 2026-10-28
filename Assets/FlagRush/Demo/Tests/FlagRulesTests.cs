using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace FlagRush.Demo.Tests
{
    public class FlagRulesTests
    {
        static PlayerView P(int id, byte team, float x, float z, bool alive = true) =>
            new() { Id = id, Team = team, Position = new float2(x, z), Alive = alive };

        static NativeList<MatchEvent> Run(ref FlagData red, ref FlagData blue, PlayerView[] players, in ArenaLayout layout, uint tick)
        {
            var events = new NativeList<MatchEvent>(Allocator.Temp);
            var arr = new NativeArray<PlayerView>(players, Allocator.Temp);
            FlagRules.Step(ref red, ref blue, arr, layout, tick, events);
            arr.Dispose();
            return events;
        }

        static void Flags(out FlagData red, out FlagData blue)
        {
            red = FlagData.AtHome(0, ArenaLayout.Empty);
            blue = FlagData.AtHome(1, ArenaLayout.Empty);
        }

        [Test]
        public void EnemyTouchTakesFlag()
        {
            Flags(out var red, out var blue);
            var ev = Run(ref red, ref blue, new[] { P(7, 1, -24f, 0f) }, ArenaLayout.Empty, 10);
            Assert.AreEqual(FlagState.Carried, red.State);
            Assert.AreEqual(7, red.CarrierId);
            Assert.AreEqual(1, ev.Length);
            Assert.AreEqual(MatchEventKind.FlagTaken, ev[0].Kind);
            Assert.AreEqual(7, ev[0].Actor);
            Assert.AreEqual(0, ev[0].Team);
        }

        [Test]
        public void TeammateCannotTakeOwnHomeFlag()
        {
            Flags(out var red, out var blue);
            var ev = Run(ref red, ref blue, new[] { P(7, 0, -25f, 0f) }, ArenaLayout.Empty, 10);
            Assert.AreEqual(FlagState.Home, red.State);
            Assert.AreEqual(0, ev.Length);
        }

        [Test]
        public void CarriedFlagFollowsCarrier()
        {
            Flags(out var red, out var blue);
            Run(ref red, ref blue, new[] { P(7, 1, -24f, 0f) }, ArenaLayout.Empty, 10);
            Run(ref red, ref blue, new[] { P(7, 1, 0f, 3f) }, ArenaLayout.Empty, 11);
            Assert.AreEqual(new float2(0f, 3f), red.Position);
        }

        [Test]
        public void CarrierDeathDrops()
        {
            Flags(out var red, out var blue);
            Run(ref red, ref blue, new[] { P(7, 1, -24f, 0f) }, ArenaLayout.Empty, 10);
            var ev = Run(ref red, ref blue, new[] { P(7, 1, -10f, 4f, false) }, ArenaLayout.Empty, 20);
            Assert.AreEqual(FlagState.Dropped, red.State);
            Assert.AreEqual(20u, red.DroppedTick);
            Assert.AreEqual(new float2(-10f, 4f), red.Position);
            Assert.AreEqual(1, ev.Length);
            Assert.AreEqual(MatchEventKind.FlagDropped, ev[0].Kind);
            Assert.AreEqual(20u, ev[0].Tick);
        }

        [Test]
        public void CarrierMissingDropsAtLastPosition()
        {
            Flags(out var red, out var blue);
            Run(ref red, ref blue, new[] { P(7, 1, -24f, 0f) }, ArenaLayout.Empty, 10);
            Run(ref red, ref blue, new[] { P(7, 1, -5f, 2f) }, ArenaLayout.Empty, 11);
            var ev = Run(ref red, ref blue, new PlayerView[0], ArenaLayout.Empty, 12);
            Assert.AreEqual(FlagState.Dropped, red.State);
            Assert.AreEqual(new float2(-5f, 2f), red.Position);
            Assert.AreEqual(MatchEventKind.FlagDropped, ev[0].Kind);
        }

        [Test]
        public void DropInsideBoxIsPushedOut()
        {
            Flags(out var red, out var blue);
            red.State = FlagState.Carried;
            red.CarrierId = 7;
            red.Position = new float2(12f, 0f);
            Run(ref red, ref blue, new PlayerView[0], ArenaLayout.Match, 30);
            Assert.AreEqual(FlagState.Dropped, red.State);
            var l = ArenaLayout.Match;
            for (int i = 0; i < l.Boxes.Length; i++)
            {
                var b = l.Boxes[i];
                bool inside = math.abs(red.Position.x - b.Center.x) < b.HalfExtents.x && math.abs(red.Position.y - b.Center.y) < b.HalfExtents.y;
                Assert.IsFalse(inside, "dropped inside box " + i);
            }
        }

        [Test]
        public void TeammateTouchReturnsDroppedFlag()
        {
            Flags(out var red, out var blue);
            red.State = FlagState.Dropped;
            red.Position = new float2(-10f, 5f);
            red.DroppedTick = 5;
            var ev = Run(ref red, ref blue, new[] { P(3, 0, -10.5f, 5f) }, ArenaLayout.Empty, 50);
            Assert.AreEqual(FlagState.Home, red.State);
            Assert.AreEqual(new float2(-25f, 0f), red.Position);
            Assert.AreEqual(1, ev.Length);
            Assert.AreEqual(MatchEventKind.FlagReturned, ev[0].Kind);
            Assert.AreEqual(3, ev[0].Actor);
        }

        [Test]
        public void AutoReturnAtExactly600Ticks()
        {
            Flags(out var red, out var blue);
            red.State = FlagState.Dropped;
            red.Position = new float2(-10f, 5f);
            red.DroppedTick = 100;
            var ev = Run(ref red, ref blue, new PlayerView[0], ArenaLayout.Empty, 699);
            Assert.AreEqual(FlagState.Dropped, red.State);
            Assert.AreEqual(0, ev.Length);
            ev = Run(ref red, ref blue, new PlayerView[0], ArenaLayout.Empty, 700);
            Assert.AreEqual(FlagState.Home, red.State);
            Assert.AreEqual(1, ev.Length);
            Assert.AreEqual(MatchEventKind.FlagReturned, ev[0].Kind);
            Assert.AreEqual(0, ev[0].Actor);
        }

        [Test]
        public void CaptureNeedsOwnFlagHome()
        {
            Flags(out var red, out var blue);
            blue.State = FlagState.Carried;
            blue.CarrierId = 9;
            red.State = FlagState.Carried;
            red.CarrierId = 7;
            var ev = Run(ref red, ref blue, new[] { P(7, 1, 25f, 0f), P(9, 0, 0f, 0f) }, ArenaLayout.Empty, 100);
            Assert.AreEqual(FlagState.Carried, red.State);
            for (int i = 0; i < ev.Length; i++) Assert.AreNotEqual(MatchEventKind.FlagCaptured, ev[i].Kind);
        }

        [Test]
        public void CaptureScoresAndResets()
        {
            Flags(out var red, out var blue);
            red.State = FlagState.Carried;
            red.CarrierId = 7;
            var ev = Run(ref red, ref blue, new[] { P(7, 1, 25f, 0f) }, ArenaLayout.Empty, 100);
            Assert.AreEqual(FlagState.Home, red.State);
            Assert.AreEqual(new float2(-25f, 0f), red.Position);
            Assert.AreEqual(1, ev.Length);
            Assert.AreEqual(MatchEventKind.FlagCaptured, ev[0].Kind);
            Assert.AreEqual(1, ev[0].Team);
            Assert.AreEqual(7, ev[0].Actor);
        }
    }
}
