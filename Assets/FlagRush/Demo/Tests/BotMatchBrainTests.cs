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
        public void OnlyTheFourNearestBotsHuntACarrier()
        {
            // Red's flag is carried by Blue player 99 at (0, 0). Ten Red bots stand 1..10 m from it (ids 1..10, listed
            // in shuffled order; attackers and defenders mixed); a Red human at 0.5 m does not count toward the cap.
            var carrierAt = new float2(0f, 0f);
            var own = new FlagData { Team = 0, State = FlagState.Carried, CarrierId = 99, Position = carrierAt };
            var list = new System.Collections.Generic.List<PlayerView>
            {
                new() { Id = 99, Position = carrierAt, Team = 1, Alive = true, Attacker = true, Bot = true },
                new() { Id = 50, Position = new float2(0.5f, 0f), Team = 0, Alive = true },
            };
            int[] order = { 7, 2, 10, 4, 1, 9, 3, 6, 8, 5 };
            foreach (int id in order)
            {
                float angle = id * 0.7f;
                list.Add(new PlayerView
                {
                    Id = id, Position = carrierAt + id * new float2(math.cos(angle), math.sin(angle)), Team = 0, Alive = true,
                    Attacker = id % 3 != 0, Bot = true,
                });
            }
            using var players = new NativeArray<PlayerView>(list.ToArray(), Allocator.Temp);

            var hunters = new System.Collections.Generic.List<int>();
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (!p.Bot || p.Team != 0) continue;
                int botIndex = p.Attacker ? 1 : 0;
                var state = BotBrain.ChooseState(botIndex, p, own, Home(1, Empty), players, Empty, out float2 target);
                if (state == BotMatchState.Hunt && target.Equals(carrierAt)) hunters.Add(p.Id);
                else if (p.Attacker) Assert.That(state, Is.EqualTo(BotMatchState.GoForFlag), $"attacker {p.Id} left its role");
                else Assert.That(state, Is.EqualTo(BotMatchState.Hold), $"defender {p.Id} left its role (no intruder in its half)");
            }
            hunters.Sort();
            Assert.That(hunters, Is.EqualTo(new[] { 1, 2, 3, 4 }), "only the 4 nearest Red bots hunt the carrier");
        }

        [Test]
        public void DeadBotsDoNotTakeAHunterSlot()
        {
            var own = new FlagData { Team = 0, State = FlagState.Carried, CarrierId = 99, Position = float2.zero };
            var players = new NativeArray<PlayerView>(6, Allocator.Temp);
            for (int i = 0; i < 5; i++)
                players[i] = new PlayerView { Id = i + 1, Position = new float2(i + 1f, 0f), Team = 0, Alive = i != 0, Bot = true, Attacker = true };
            players[5] = new PlayerView { Id = 99, Position = float2.zero, Team = 1, Alive = true, Bot = true, Attacker = true };
            Assert.That(BotBrain.HuntsCarrier(players[4], own, players), Is.True, "5th nearest hunts when the nearest is dead");
            players[0] = new PlayerView { Id = 1, Position = new float2(1f, 0f), Team = 0, Alive = true, Bot = true, Attacker = true };
            Assert.That(BotBrain.HuntsCarrier(players[4], own, players), Is.False, "5th nearest does not hunt when 4 nearer bots live");
            Assert.That(BotBrain.HuntsCarrier(players[4], FlagData.AtHome(0, Empty), players), Is.False, "no carrier, no hunt");
            players.Dispose();
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
            var input = BotBrain.ThinkMatch(ref s, 0, self, Home(0, Empty), Home(1, Empty), players, Empty, 0);
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
                var input = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Empty), Home(1, Empty), players, Empty, 0);
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
            var input = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Layout), Home(1, Layout), players, Layout, 0);
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
            var input = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Empty), enemy, players, Empty, 0);
            Assert.That(math.distance(self.Position, Empty.RedBase), Is.GreaterThan(15f));
            Assert.That(input.Sprint, Is.False);
        }

        // ---- attack doctrine (fix-browser F1): waves, lanes, attackers ignore attackers, defenders fire into own half

        [Test]
        public void AttackerWaitsInSpawnStripUntilTheWaveDeparts()
        {
            var self = View(1, new float2(-27f, 4f), 0);
            using var players = new NativeArray<PlayerView>(new[] { self }, Allocator.Temp);
            var s = BotBrainState.Create(1);
            var waiting = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Layout), Home(1, Layout), players, Layout, MatchTuning.BotWaveDepartTicks);
            Assert.That(waiting.Move, Is.EqualTo(float2.zero), "left the spawn strip between waves");
            var leaving = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Layout), Home(1, Layout), players, Layout, 5 * MatchTuning.BotWaveTicks);
            Assert.That(math.length(leaving.Move), Is.EqualTo(1f).Within(1e-4f), "did not leave when the wave departed");
        }

        [Test]
        public void WaveOnlyHoldsAttackersRaidingAHomeFlag()
        {
            var self = View(1, new float2(-27f, 4f), 0);
            using var players = new NativeArray<PlayerView>(new[] { self }, Allocator.Temp);
            var s = BotBrainState.Create(1);
            var dropped = new FlagData { Team = 1, State = FlagState.Dropped, Position = new float2(5f, 0f) };
            var input = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Layout), dropped, players, Layout, MatchTuning.BotWaveDepartTicks);
            Assert.That(math.length(input.Move), Is.EqualTo(1f).Within(1e-4f), "waited for a wave while the enemy flag lay dropped");
        }

        [TestCase((byte)0, -1f)]
        [TestCase((byte)1, 1f)]
        public void AttackerCrossesMidfieldOnItsTeamLane(byte team, float laneSign)
        {
            float toward = team == 0 ? 1f : -1f; // direction of the enemy flag along x
            var self = View(1, new float2(-15f * toward, 0f), team);
            using var players = new NativeArray<PlayerView>(new[] { self }, Allocator.Temp);
            var s = BotBrainState.Create(1);
            var enemyFlag = Home((byte)(1 - team), Empty);
            var input = BotBrain.ThinkMatch(ref s, 1, self, Home(team, Empty), enemyFlag, players, Empty, 0);
            float2 lane = new float2(0f, laneSign * MatchTuning.BotLaneZ);
            Assert.That(math.distance(input.Move, math.normalize(lane - self.Position)), Is.LessThan(1e-3f), "not heading for its lane");

            var across = View(1, new float2(5f * toward, -laneSign * MatchTuning.BotLaneZ), team); // past midfield
            var direct = BotBrain.ThinkMatch(ref s, 1, across, Home(team, Empty), enemyFlag, players, Empty, 0);
            Assert.That(math.distance(direct.Move, math.normalize(enemyFlag.Position - across.Position)), Is.LessThan(1e-3f), "not heading for the flag once across");
        }

        [Test]
        public void RaidingAttackerHoldsFireAtEnemyAttackersButShootsDefenders()
        {
            var self = View(1, new float2(-20f, 3f), 0);
            var enemyAttacker = View(2, new float2(-12f, 3f), 1);
            enemyAttacker.Attacker = true;
            var enemyDefender = View(3, new float2(-5f, 3f), 1);
            var s = BotBrainState.Create(1);
            using (var onlyAttacker = new NativeArray<PlayerView>(new[] { self, enemyAttacker }, Allocator.Temp))
            {
                var input = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Empty), Home(1, Empty), onlyAttacker, Empty, 0);
                Assert.That(input.Fire, Is.False, "fired at an enemy attacker while raiding");
            }
            using (var both = new NativeArray<PlayerView>(new[] { self, enemyAttacker, enemyDefender }, Allocator.Temp))
            {
                var input = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Empty), Home(1, Empty), both, Empty, 0);
                Assert.That(input.Fire, Is.True, "did not fire at the enemy defender");
                float2 toDefender = math.normalize(enemyDefender.Position - self.Position);
                Assert.That(math.degrees(math.acos(math.clamp(math.dot(input.Aim, toDefender), -1f, 1f))),
                    Is.LessThanOrEqualTo(MatchTuning.BotAimErrorDegrees + 1e-3f), "aimed at someone other than the defender");
            }
            // Once a flag is carried the raid is over: the escort shoots enemy attackers too.
            var carried = new FlagData { Team = 1, State = FlagState.Carried, CarrierId = 9, Position = new float2(-22f, 0f) };
            using (var onlyAttacker = new NativeArray<PlayerView>(new[] { self, enemyAttacker }, Allocator.Temp))
            {
                var input = BotBrain.ThinkMatch(ref s, 1, self, Home(0, Empty), carried, onlyAttacker, Empty, 0);
                Assert.That(input.Fire, Is.True, "escort did not fire at an enemy attacker");
            }
        }

        [Test]
        public void DefenderFiresOnlyIntoItsOwnHalf()
        {
            var self = View(1, new float2(-6f, 0f), 0); // Red defender near midfield
            var acrossMidfield = View(2, new float2(6f, 0f), 1);
            var s = BotBrainState.Create(0);
            using (var players = new NativeArray<PlayerView>(new[] { self, acrossMidfield }, Allocator.Temp))
            {
                var input = BotBrain.ThinkMatch(ref s, 0, self, Home(0, Empty), Home(1, Empty), players, Empty, 0);
                Assert.That(input.Fire, Is.False, "defender fired into the enemy half");
            }
            var intruder = View(3, new float2(-14f, 0f), 1);
            using (var players = new NativeArray<PlayerView>(new[] { self, acrossMidfield, intruder }, Allocator.Temp))
            {
                var input = BotBrain.ThinkMatch(ref s, 0, self, Home(0, Empty), Home(1, Empty), players, Empty, 0);
                Assert.That(input.Fire, Is.True, "defender did not fire at the intruder");
                Assert.That(input.Aim.x, Is.LessThan(0f), "defender aimed into the enemy half");
            }
            // Hunting its own flag's carrier, a defender shoots anywhere.
            var own = new FlagData { Team = 0, State = FlagState.Carried, CarrierId = 2, Position = acrossMidfield.Position };
            using (var players = new NativeArray<PlayerView>(new[] { self, acrossMidfield }, Allocator.Temp))
            {
                var input = BotBrain.ThinkMatch(ref s, 0, self, own, Home(1, Empty), players, Empty, 0);
                Assert.That(input.Fire, Is.True, "defender did not fire at the carrier across midfield");
            }
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
