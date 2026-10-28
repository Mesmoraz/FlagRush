using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace FlagRush.Demo.Tests
{
    public class BotMatchBrainTests
    {
        static readonly ArenaLayout Layout = ArenaLayout.Match;
        static readonly ArenaLayout Empty = ArenaLayout.Empty;

        static PlayerView View(int id, float2 pos, byte team, bool alive = true) =>
            new() { Id = id, Position = pos, Team = team, Alive = alive };

        static FlagData Home(byte team, in ArenaLayout layout) => FlagData.AtHome(team, layout);

        [Test]
        public void CarrierReturnsToOwnBase()
        {
            var self = View(1, new float2(-5f, 3f), 0);
            var enemy = new FlagData { Team = 1, State = FlagState.Carried, CarrierId = 1, Position = self.Position };
            using var players = new NativeArray<PlayerView>(new[] { self }, Allocator.Temp);
            var state = BotBrain.ChooseState(1, self, Home(0, Empty), enemy, players, Empty, out float2 target);
            Assert.That(state, Is.EqualTo(BotMatchState.Return));
            Assert.That(target, Is.EqualTo(Empty.RedBase));
        }

        [Test]
        public void OwnFlagTakenMeansHunt()
        {
            var self = View(1, new float2(-5f, 3f), 0);
            var own = new FlagData { Team = 0, State = FlagState.Carried, CarrierId = 9, Position = new float2(5f, 5f) };
            using var players = new NativeArray<PlayerView>(new[] { self, View(9, new float2(5f, 5f), 1) }, Allocator.Temp);
            var state = BotBrain.ChooseState(1, self, own, Home(1, Empty), players, Empty, out float2 target);
            Assert.That(state, Is.EqualTo(BotMatchState.Hunt));
            Assert.That(target, Is.EqualTo(new float2(5f, 5f)));
        }

        [Test]
        public void AttackerGoesForEnemyFlag()
        {
            var self = View(1, new float2(-20f, 3f), 0);
            var enemy = Home(1, Empty);
            using var players = new NativeArray<PlayerView>(new[] { self }, Allocator.Temp);
            var state = BotBrain.ChooseState(1, self, Home(0, Empty), enemy, players, Empty, out float2 target);
            Assert.That(state, Is.EqualTo(BotMatchState.GoForFlag));
            Assert.That(target, Is.EqualTo(enemy.Position));
        }

        [Test]
        public void DefenderHoldsNearStand()
        {
            var self = View(1, Empty.RedBase + new float2(3f, 0f), 0);
            using var players = new NativeArray<PlayerView>(new[] { self }, Allocator.Temp);
            var state = BotBrain.ChooseState(0, self, Home(0, Empty), Home(1, Empty), players, Empty, out float2 target);
            Assert.That(state, Is.EqualTo(BotMatchState.Hold));
            Assert.That(target, Is.EqualTo(self.Position));

            var s = BotBrainState.Create(0);
            var input = BotBrain.ThinkMatch(ref s, 0, self, Home(0, Empty), Home(1, Empty), players, Empty);
            Assert.That(input.Move, Is.EqualTo(float2.zero));
        }

        [Test]
        public void DefenderHuntsIntruder()
        {
            var self = View(1, Empty.RedBase, 0);
            var intruder = View(2, new float2(-10f, 4f), 1);
            var farEnemy = View(3, new float2(10f, 0f), 1);
            using var players = new NativeArray<PlayerView>(new[] { self, farEnemy, intruder }, Allocator.Temp);
            var state = BotBrain.ChooseState(0, self, Home(0, Empty), Home(1, Empty), players, Empty, out float2 target);
            Assert.That(state, Is.EqualTo(BotMatchState.Hunt));
            Assert.That(target, Is.EqualTo(intruder.Position));
        }

        [Test]
        public void SteersAroundBox()
        {
            float2 from = new float2(-20f, 0f);
            float2 dir = BotBrain.SteerToward(from, float2.zero, Layout);
            Assert.That(math.length(dir), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(math.abs(dir.y), Is.GreaterThan(0.1f), "must not head straight along +x");
            // Points at a corner of box (-12,0)(1,5), pushed out by the margin along the diagonal.
            float2 corner = new float2(-13f, 5f * math.sign(dir.y));
            float2 pushed = corner + math.normalize(new float2(-1f, math.sign(dir.y))) * MatchTuning.BotCornerMargin;
            float2 expected = math.normalize(pushed - from);
            Assert.That(math.distance(dir, expected), Is.LessThan(1e-3f));
        }

        [Test]
        public void FiresAtVisibleEnemyWithinError()
        {
            var self = View(1, new float2(-20f, 3f), 0);
            var enemy = View(2, new float2(-10f, 3f), 1);
            using var players = new NativeArray<PlayerView>(new[] { self, enemy }, Allocator.Temp);
            var s = BotBrainState.Create(1);
            for (int i = 0; i < 50; i++)
            {
                var input = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Empty), Home(1, Empty), players, Empty);
                Assert.That(input.Fire, Is.True);
                Assert.That(math.length(input.Aim), Is.EqualTo(1f).Within(1e-4f));
                float angle = math.degrees(math.acos(math.clamp(math.dot(input.Aim, new float2(1f, 0f)), -1f, 1f)));
                Assert.That(angle, Is.LessThanOrEqualTo(MatchTuning.BotAimErrorDegrees + 1e-3f));
            }
        }

        [Test]
        public void HoldsFireBehindBox()
        {
            var self = View(1, new float2(-20f, 0f), 0);
            var enemy = View(2, new float2(-5f, 0f), 1); // box (-12,0) between
            using var players = new NativeArray<PlayerView>(new[] { self, enemy }, Allocator.Temp);
            var s = BotBrainState.Create(1);
            var input = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Layout), Home(1, Layout), players, Layout);
            Assert.That(input.Fire, Is.False);
            Assert.That(input.Aim, Is.EqualTo(float2.zero));
        }

        [Test]
        public void NoSprintWhileCarrying()
        {
            var self = View(1, new float2(15f, 0f), 0);
            var enemy = new FlagData { Team = 1, State = FlagState.Carried, CarrierId = 1, Position = self.Position };
            using var players = new NativeArray<PlayerView>(new[] { self }, Allocator.Temp);
            var s = BotBrainState.Create(1);
            var input = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Empty), enemy, players, Empty);
            Assert.That(math.distance(self.Position, Empty.RedBase), Is.GreaterThan(15f));
            Assert.That(input.Sprint, Is.False);
        }

        [TestCase(-20f, 0f)]
        [TestCase(-13.5f, -5.5f)]
        [TestCase(-13.5f, 5.5f)]
        [TestCase(-9.5f, 0f)]
        public void ReachesTargetAroundBox(float x, float y)
        {
            float2 pos = new float2(x, y), target = float2.zero;
            float2 velocity = float2.zero;
            for (int i = 0; i < 600 && math.distance(pos, target) > 0.5f; i++)
            {
                float2 dir = BotBrain.SteerToward(pos, target, Layout);
                var p3 = new float3(pos.x, 0f, pos.y) + new float3(dir.x, 0f, dir.y) * 0.1f;
                Obstacles.Slide(ref p3, ref velocity, 0.6f, Layout);
                pos = p3.xz;
            }
            Assert.That(math.distance(pos, target), Is.LessThanOrEqualTo(0.5f), $"stuck at {pos}");
        }
    }
}
