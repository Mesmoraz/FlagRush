using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlagRush.Demo.Tests
{
    /// <summary>
    /// Level 4 gate: the two flag ghosts and the match-state ghost exist only at level 4+, players get balanced teams
    /// and spawn inside their team's zone, and leaving the match clears per-player match state.
    /// </summary>
    public class MatchGateTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DemoConfig.OverrideLevel(4);
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
            return x >= lo - tol && x <= hi + tol && z >= -20f - tol && z <= 20f + tol;
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
