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
            var players = new NativeArray<PlayerView>(new[] { self }, Allocator.Temp);
            var state = BotBrain.ChooseState(1, self, Home(0, Empty), enemy, players, Empty, out float2 target);
            players.Dispose();
            Assert.That(state, Is.EqualTo(BotMatchState.Return));
            Assert.That(target, Is.EqualTo(Empty.RedBase));
        }

        [Test]
        public void OwnFlagTakenMeansHunt()
        {
            var self = View(1, new float2(-5f, 3f), 0);
            var own = new FlagData { Team = 0, State = FlagState.Carried, CarrierId = 9, Position = new float2(5f, 5f) };
            var players = new NativeArray<PlayerView>(new[] { self, View(9, new float2(5f, 5f), 1) }, Allocator.Temp);
            var state = BotBrain.ChooseState(1, self, own, Home(1, Empty), players, Empty, out float2 target);
            players.Dispose();
            Assert.That(state, Is.EqualTo(BotMatchState.Hunt));
            Assert.That(target, Is.EqualTo(new float2(5f, 5f)));
        }

        [Test]
        public void AttackerGoesForEnemyFlag()
        {
            var self = View(1, new float2(-20f, 3f), 0);
            var enemy = Home(1, Empty);
            var players = new NativeArray<PlayerView>(new[] { self }, Allocator.Temp);
            var state = BotBrain.ChooseState(1, self, Home(0, Empty), enemy, players, Empty, out float2 target);
            players.Dispose();
            Assert.That(state, Is.EqualTo(BotMatchState.GoForFlag));
            Assert.That(target, Is.EqualTo(enemy.Position));
        }

        [Test]
        public void DefenderHoldsNearStand()
        {
            var self = View(1, Empty.RedBase + new float2(3f, 0f), 0);
            var players = new NativeArray<PlayerView>(new[] { self }, Allocator.Temp);
            var state = BotBrain.ChooseState(0, self, Home(0, Empty), Home(1, Empty), players, Empty, out float2 target);
            Assert.That(state, Is.EqualTo(BotMatchState.Hold));
            Assert.That(target, Is.EqualTo(self.Position));

            var s = BotBrainState.Create(0);
            var input = BotBrain.ThinkMatch(ref s, 0, self, Home(0, Empty), Home(1, Empty), players, Empty);
            players.Dispose();
            Assert.That(input.Move, Is.EqualTo(float2.zero));
        }

        [Test]
        public void DefenderHuntsIntruder()
        {
            var self = View(1, Empty.RedBase, 0);
            var intruder = View(2, new float2(-10f, 4f), 1);
            var farEnemy = View(3, new float2(10f, 0f), 1);
            var players = new NativeArray<PlayerView>(new[] { self, farEnemy, intruder }, Allocator.Temp);
            var state = BotBrain.ChooseState(0, self, Home(0, Empty), Home(1, Empty), players, Empty, out float2 target);
            players.Dispose();
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
            var players = new NativeArray<PlayerView>(new[] { self, enemy }, Allocator.Temp);
            var s = BotBrainState.Create(1);
            for (int i = 0; i < 50; i++)
            {
                var input = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Empty), Home(1, Empty), players, Empty);
                Assert.That(input.Fire, Is.True);
                Assert.That(math.length(input.Aim), Is.EqualTo(1f).Within(1e-4f));
                float angle = math.degrees(math.acos(math.clamp(math.dot(input.Aim, new float2(1f, 0f)), -1f, 1f)));
                Assert.That(angle, Is.LessThanOrEqualTo(MatchTuning.BotAimErrorDegrees + 1e-3f));
            }
            players.Dispose();
        }

        [Test]
        public void HoldsFireBehindBox()
        {
            var self = View(1, new float2(-20f, 0f), 0);
            var enemy = View(2, new float2(-5f, 0f), 1); // box (-12,0) between
            var players = new NativeArray<PlayerView>(new[] { self, enemy }, Allocator.Temp);
            var s = BotBrainState.Create(1);
            var input = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Layout), Home(1, Layout), players, Layout);
            players.Dispose();
            Assert.That(input.Fire, Is.False);
            Assert.That(input.Aim, Is.EqualTo(float2.zero));
        }

        [Test]
        public void NoSprintWhileCarrying()
        {
            var self = View(1, new float2(15f, 0f), 0);
            var enemy = new FlagData { Team = 1, State = FlagState.Carried, CarrierId = 1, Position = self.Position };
            var players = new NativeArray<PlayerView>(new[] { self }, Allocator.Temp);
            var s = BotBrainState.Create(1);
            var input = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Empty), enemy, players, Empty);
            players.Dispose();
            Assert.That(math.distance(self.Position, Empty.RedBase), Is.GreaterThan(15f));
            Assert.That(input.Sprint, Is.False);
        }
    }
}
