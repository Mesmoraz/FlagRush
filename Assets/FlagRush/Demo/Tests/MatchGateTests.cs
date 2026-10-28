using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlagRush.Demo.Tests
{
    /// <summary>
    /// Level 4 gate: the two flag ghosts and the match-state ghost exist only at level 4+, players get balanced teams
    /// and spawn inside their team's zone, leaving the match clears per-player match state, a walked flag scores, three
    /// shots kill a stationary bot that then respawns, and a dead player cannot move.
    /// </summary>
    public class MatchGateTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DemoConfig.OverrideLevel(4);
            DemoStats.ShotsPerSecond = 0f;
            DemoStats.LastMatchEvents.Clear();
            Sandbox.Bots = 0;
            DemoHud.Collapsed = false;
            SceneManager.LoadScene("SampleScene");
            yield return Until(() => DemoStats.ClientInGame, 30f);
            Assert.That(DemoStats.ClientInGame, Is.True, "client never went in-game");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DemoConfig.OverrideLevel(null);
            PlayerInputGatherSystem.TestOverride = null;
            Sandbox.Bots = 0;
            Sandbox.SetLink(0, 0, 0);
            Sandbox.PredictionEnabled = true;
            DemoHud.Collapsed = false;
            yield return Until(() => DemoStats.FlagsOnServer == 0 && DemoStats.FlagsOnClient == 0 && !DemoStats.MatchStateOnClient
                                     && DemoStats.PlayersOnServer == 0 && DemoStats.PlayersOnClient == 0, 10f);
            Assert.That(DemoStats.FlagsOnServer, Is.EqualTo(0), "flags not despawned on the server below Level 4");
            Assert.That(DemoStats.FlagsOnClient, Is.EqualTo(0), "flags not despawned on the client below Level 4");
            Assert.That(DemoStats.MatchStateOnClient, Is.False, "match state not despawned on the client below Level 4");
            Assert.That(DemoStats.PlayersOnServer, Is.EqualTo(0), "players not despawned on the server below Level 3");
            Assert.That(DemoStats.PlayersOnClient, Is.EqualTo(0), "players not despawned on the client below Level 3");
        }

        [UnityTest]
        public IEnumerator MatchGhostsSpawn()
        {
            yield return Until(() => DemoStats.FlagsOnServer == 2 && DemoStats.FlagsOnClient == 2 && DemoStats.MatchStateOnClient, 10f);
            Assert.That(DemoStats.FlagsOnServer, Is.EqualTo(2), "flags on the server");
            Assert.That(DemoStats.FlagsOnClient, Is.EqualTo(2), "flags on the client");
            Assert.That(DemoStats.MatchStateOnClient, Is.True, "match state on the client");
        }

        [UnityTest]
        public IEnumerator TeamsAreBalanced()
        {
            Sandbox.Bots = 5;
            yield return Until(() => DemoStats.PlayersOnServer == 6 && DemoStats.PlayersOnClient == 6, 10f);
            Assert.That(DemoStats.PlayersOnServer, Is.EqualTo(6), "server players (human + 5 bots)");
            Assert.That(DemoStats.RedPlayersOnServer, Is.EqualTo(3), "Red players");
            Assert.That(DemoStats.BluePlayersOnServer, Is.EqualTo(3), "Blue players");

            var em = ServerWorld().EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>(), ComponentType.ReadOnly<Combatant>());
            using var ghosts = query.ToComponentDataArray<PlayerGhost>(Allocator.Temp);
            using var combatants = query.ToComponentDataArray<Combatant>(Allocator.Temp);
            Assert.That(ghosts.Length, Is.EqualTo(6), "server players with a Combatant");
            for (int i = 0; i < ghosts.Length; i++)
                Assert.That(InZone(combatants[i].Team, ghosts[i].Position.x, ghosts[i].Position.z), Is.True,
                    $"player {i} (team {combatants[i].Team}) at {ghosts[i].Position} is outside its spawn zone");
        }

        [UnityTest]
        public IEnumerator HumanSpawnsInOwnZone()
        {
            yield return Until(() => DemoStats.PlayersOnServer == 1 && DemoStats.PlayersOnClient == 1, 10f);
            Assert.That(DemoStats.PlayersOnServer, Is.EqualTo(1), "server player count");
            var em = ServerWorld().EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>(), ComponentType.ReadOnly<Combatant>());
            var ghost = query.GetSingleton<PlayerGhost>();
            var combatant = query.GetSingleton<Combatant>();
            Assert.That(combatant.Health, Is.EqualTo(MatchTuning.MaxHealth), "spawn health");
            Assert.That(combatant.Alive, Is.True, "spawned dead");
            Assert.That(InZone(combatant.Team, ghost.Position.x, ghost.Position.z), Is.True,
                $"human (team {combatant.Team}) at {ghost.Position} is outside its spawn zone");
        }

        [UnityTest]
        public IEnumerator LeavingMatchRevivesAndClearsCarrying()
        {
            yield return Until(() => DemoStats.PlayersOnServer == 1 && DemoStats.FlagsOnServer == 2 && DemoStats.FlagsOnClient == 2, 10f);
            Assert.That(DemoStats.PlayersOnServer, Is.EqualTo(1), "no human player at level 4");

            var em = ServerWorld().EntityManager;
            using (var query = em.CreateEntityQuery(ComponentType.ReadWrite<Combatant>()))
            {
                var human = query.GetSingletonEntity();
                var c = em.GetComponentData<Combatant>(human);
                c.RespawnTick = 999999; c.Carrying = true; c.Health = 0;
                em.SetComponentData(human, c);
            }

            DemoConfig.OverrideLevel(3);
            Sandbox.Bots = 0; // OverrideLevel re-applies the level defaults (31 bots)
            yield return Until(() => DemoStats.FlagsOnServer == 0 && DemoStats.FlagsOnClient == 0 && HumanIsReset(), 5f);
            Assert.That(DemoStats.FlagsOnServer, Is.EqualTo(0), "flags on the server after leaving the match");
            Assert.That(DemoStats.FlagsOnClient, Is.EqualTo(0), "flags on the client after leaving the match");
            using var q2 = em.CreateEntityQuery(ComponentType.ReadOnly<Combatant>());
            var after = q2.GetSingleton<Combatant>();
            Assert.That(after.RespawnTick, Is.EqualTo(0u), "RespawnTick not cleared");
            Assert.That(after.Carrying, Is.False, "Carrying not cleared");
            Assert.That(after.Health, Is.EqualTo(MatchTuning.MaxHealth), "Health not restored");
        }

        [UnityTest]
        public IEnumerator WalkingTheFlagHomeScores()
        {
            yield return Until(() => DemoStats.PlayersOnServer == 1 && OwnedReady() && DemoStats.FlagsOnServer == 2
                                     && DemoStats.MatchStateOnClient && HumanGhostId() != 0, 10f);
            Assert.That(OwnedReady(), Is.True, "no owned player at level 4");
            Assert.That(DemoStats.FlagsOnServer, Is.EqualTo(2), "flags on the server");

            byte team = HumanCombatant().Team;
            int humanId = HumanGhostId();
            float s = team == 0 ? 1f : -1f; // Red walks toward +x (the Blue flag); Blue mirrors
            var route = new[]
            {
                new float2(-24f * s, 7f), // clears the corner boxes from anywhere in the spawn zone (Ruling R5)
                new float2(-22f * s, 7f), new float2(22f * s, 7f), new float2(25f * s, 0f),
                new float2(22f * s, 7f), new float2(-22f * s, 7f), new float2(-25f * s, 0f),
            };

            float start = Time.realtimeSinceStartup;
            float deadline = start + 45f;
            int next = 0;
            while (Time.realtimeSinceStartup < deadline && TeamScore(ServerWorld(), team) < 1)
            {
                float2 at = OwnedClientPosition().xz;
                if (next < route.Length && math.distance(at, route[next]) < 0.8f) next++;
                float2 to = route[math.min(next, route.Length - 1)] - at;
                float d = math.length(to);
                float2 move = d > 1e-4f ? to / d * math.clamp(d / 2f, 0.3f, 1f) : float2.zero;
                PlayerInputGatherSystem.TestOverride = new PlayerInput { Move = move, Sprint = true };
                yield return null;
            }
            PlayerInputGatherSystem.TestOverride = null;
            float serverAt = Time.realtimeSinceStartup - start;
            yield return Until(() => TeamScore(ClientWorld(), team) >= 1, 5f);

            var events = DemoStats.LastMatchEvents.ToArray();
            int taken = System.Array.FindIndex(events, e => e.Kind == MatchEventKind.FlagTaken && e.Actor == humanId);
            int captured = System.Array.FindIndex(events, e => e.Kind == MatchEventKind.FlagCaptured && e.Actor == humanId);
            Debug.Log($"[MatchGate] team {team} reached waypoint {next}/{route.Length}; server scored after {serverAt:F1}s; events ["
                      + string.Join(", ", System.Array.ConvertAll(events, e => $"{e.Kind}@{e.Tick} a{e.Actor}")) + "]");
            Assert.That(TeamScore(ServerWorld(), team), Is.EqualTo(1), "server score of the human's team");
            Assert.That(TeamScore(ClientWorld(), team), Is.EqualTo(1), "client score of the human's team");
            Assert.That(taken, Is.GreaterThanOrEqualTo(0), "no FlagTaken by the human recorded");
            Assert.That(captured, Is.GreaterThan(taken), "no FlagCaptured by the human after the FlagTaken");
        }

        [UnityTest]
        public IEnumerator ThreeShotsKillAStationaryBotWhichRespawns()
        {
            Sandbox.Bots = 1;
            yield return Until(() => DemoStats.PlayersOnServer == 2 && DemoStats.PlayersOnClient == 2 && OwnedReady(), 10f);
            Assert.That(DemoStats.PlayersOnServer, Is.EqualTo(2), "server players (human + 1 bot)");

            var em = ServerWorld().EntityManager;
            var human = HumanEntity();
            var bot = BotEntity();
            em.RemoveComponent<BotBrainState>(bot); // the bot's PlayerInput is now zero every tick
            byte team = em.GetComponentData<Combatant>(human).Team;
            Assert.That(em.GetComponentData<Combatant>(bot).Team, Is.Not.EqualTo(team), "bot is on the human's team");
            float s = team == 0 ? 1f : -1f;
            Teleport(em, human, new float3(-24f * s, 0f, 0f));
            Teleport(em, bot, new float3(-19f * s, 0f, 0f));

            uint fireTick = DemoStats.ServerTick;
            float start = Time.realtimeSinceStartup;
            PlayerInputGatherSystem.TestOverride = new PlayerInput { Aim = new float2(s, 0f), Fire = true };
            yield return Until(() => em.GetComponentData<Combatant>(bot).RespawnTick != 0, 3f);
            PlayerInputGatherSystem.TestOverride = null;

            var dead = em.GetComponentData<Combatant>(bot);
            var botStats = em.GetComponentData<MatchStats>(bot);
            var humanStats = em.GetComponentData<MatchStats>(human);
            Debug.Log($"[MatchGate] bot killed after {Time.realtimeSinceStartup - start:F2}s ({DemoStats.ServerTick - fireTick} ticks): "
                      + $"respawnTick {dead.RespawnTick} health {dead.Health}; human kills {humanStats.Kills} shots {humanStats.ShotsFired}; bot deaths {botStats.Deaths}");
            Assert.That(dead.RespawnTick, Is.Not.EqualTo(0u), "bot not killed within 3 s");
            Assert.That(botStats.Deaths, Is.EqualTo(1), "bot deaths");
            Assert.That(humanStats.Kills, Is.EqualTo(1), "human kills");
            Assert.That(humanStats.ShotsFired, Is.GreaterThanOrEqualTo(3), "human shots fired");

            float respawnStart = Time.realtimeSinceStartup;
            yield return Until(() => em.GetComponentData<Combatant>(bot).Alive, MatchTuning.RespawnTicks / 60f + 1f);
            var revived = em.GetComponentData<Combatant>(bot);
            var at = em.GetComponentData<PlayerGhost>(bot).Position;
            Debug.Log($"[MatchGate] bot respawned after {Time.realtimeSinceStartup - respawnStart:F2}s at {at} health {revived.Health}");
            Assert.That(revived.Alive, Is.True, "bot did not respawn within 180 ticks + 1 s");
            Assert.That(revived.Health, Is.EqualTo(MatchTuning.MaxHealth), "respawn health");
            Assert.That(InZone(revived.Team, at.x, at.z), Is.True, $"bot respawned at {at}, outside its spawn zone");
        }

        [UnityTest]
        public IEnumerator DeadPlayerCannotMove()
        {
            yield return Until(() => DemoStats.PlayersOnServer == 1 && OwnedReady(), 10f);
            Assert.That(OwnedReady(), Is.True, "no owned player at level 4");

            var em = ServerWorld().EntityManager;
            var human = HumanEntity();
            var c = em.GetComponentData<Combatant>(human);
            c.RespawnTick = DemoStats.ServerTick + 600;
            c.Health = 0;
            em.SetComponentData(human, c);
            var before = em.GetComponentData<PlayerGhost>(human).Position;

            uint startTick = DemoStats.ServerTick;
            float deadline = Time.realtimeSinceStartup + 5f;
            PlayerInputGatherSystem.TestOverride = new PlayerInput { Move = new float2(1, 0) };
            while (DemoStats.ServerTick - startTick < 30 && Time.realtimeSinceStartup < deadline) yield return null;
            PlayerInputGatherSystem.TestOverride = null;

            var after = em.GetComponentData<PlayerGhost>(human).Position;
            Debug.Log($"[MatchGate] dead player {before} -> {after} over {DemoStats.ServerTick - startTick} ticks");
            Assert.That(DemoStats.ServerTick - startTick, Is.GreaterThanOrEqualTo(30u), "server did not tick 30 times");
            Assert.That(math.distance(before, after), Is.LessThanOrEqualTo(1e-3f), "dead player moved on the server");
        }

        [UnityTest]
        public IEnumerator MatchRenderDrawsArena()
        {
            Sandbox.Bots = 5;
            yield return Until(() => DemoStats.PlayersOnClient == 6 && DemoStats.FlagsOnClient == 2 && OwnedReady(), 10f);
            // 6 players + 1 ring + 8 boxes + 2 bases + 2 flags (+1 server outline while prediction is on)
            int expected = 6 + 1 + 8 + 2 + 2 + (Sandbox.PredictionEnabled ? 1 : 0);
            yield return Until(() => DemoStats.DrawnInstances == expected, 10f);
            Debug.Log($"[MatchGate] drawn {DemoStats.DrawnInstances} instances in {DemoStats.DrawCalls} calls (expected {expected})");
            Assert.That(DemoStats.DrawnInstances, Is.EqualTo(expected), "drawn instances at level 4");
        }

        /// <summary>Server teleport: the same four components a respawn writes.</summary>
        static void Teleport(EntityManager em, Entity player, float3 at)
        {
            var ghost = em.GetComponentData<PlayerGhost>(player);
            ghost.Position = at;
            ghost.Velocity = float2.zero;
            em.SetComponentData(player, ghost);
            em.SetComponentData(player, LocalTransform.FromPosition(at));
            em.SetComponentData(player, new ServerPosition { Value = at });
        }

        static Entity HumanEntity() => PlayerEntity(true);
        static Entity BotEntity() => PlayerEntity(false);

        static Entity PlayerEntity(bool human)
        {
            var world = ServerWorld();
            if (world == null) return Entity.Null;
            var em = world.EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var e in entities)
                if ((em.GetComponentData<PlayerGhost>(e).BotIndex < 0) == human) return e;
            return Entity.Null;
        }

        static Combatant HumanCombatant() => ServerWorld().EntityManager.GetComponentData<Combatant>(HumanEntity());

        static int HumanGhostId()
        {
            var human = HumanEntity();
            return human == Entity.Null ? 0 : ServerWorld().EntityManager.GetComponentData<GhostInstance>(human).ghostId;
        }

        static bool OwnedReady()
        {
            var world = ClientWorld();
            var owned = DemoStats.OwnedPlayerEntity;
            return world != null && owned != Entity.Null && world.EntityManager.Exists(owned) && HumanEntity() != Entity.Null;
        }

        static float3 OwnedClientPosition()
        {
            var world = ClientWorld();
            var owned = DemoStats.OwnedPlayerEntity;
            if (world == null || owned == Entity.Null || !world.EntityManager.Exists(owned)) return float3.zero;
            return world.EntityManager.GetComponentData<PlayerGhost>(owned).Position;
        }

        static int TeamScore(World world, byte team)
        {
            if (world == null) return -1;
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchStateGhost>());
            if (query.CalculateEntityCount() != 1) return -1;
            var m = query.GetSingleton<MatchStateGhost>();
            return team == 0 ? m.RedScore : m.BlueScore;
        }

        static World ClientWorld()
        {
            foreach (var world in World.All)
                if (world.IsClient() && !world.IsThinClient()) return world;
            return null;
        }

        static bool HumanIsReset()
        {
            var world = ServerWorld();
            if (world == null) return false;
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Combatant>());
            if (query.CalculateEntityCount() != 1) return false;
            var c = query.GetSingleton<Combatant>();
            return c.RespawnTick == 0 && !c.Carrying && c.Health == MatchTuning.MaxHealth;
        }

        // Bots start moving the tick after they spawn, so allow a small tolerance around the zone.
        static bool InZone(byte team, float x, float z)
        {
            const float tol = 1f;
            float lo = team == 0 ? -29f : 26f, hi = team == 0 ? -26f : 29f;
            float az = math.abs(z);
            return x >= lo - tol && x <= hi + tol && az >= 12f - tol && az <= 20f + tol;
        }

        static World ServerWorld()
        {
            foreach (var world in World.All)
                if (world.IsServer()) return world;
            return null;
        }

        static IEnumerator Until(System.Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline && !condition()) yield return null;
        }
    }
}
