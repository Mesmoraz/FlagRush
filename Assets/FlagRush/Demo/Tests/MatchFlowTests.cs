using System.Collections;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlagRush.Demo.Tests
{
    /// <summary>
    /// Level 4 match flow with many bots: bots must actually play the game (take, drop, return and capture flags),
    /// not just shoot each other at midfield. The [Explicit] diagnostics log the flow over time; the gates assert it.
    /// </summary>
    public class MatchFlowTests
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
            yield return Until(() => DemoStats.FlagsOnServer == 0 && DemoStats.PlayersOnServer == 0 && DemoStats.PlayersOnClient == 0, 10f);
        }

        /// <summary>Gate: with the default bot count a flag is taken early (measured: first take after 5-14 s; window 60 s).</summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator FlagIsTakenWithin60sWith31Bots()
            => GateEvents(31, 60f, MatchEventKind.FlagTaken);

        /// <summary>
        /// Gate: a small match runs the flag rules end to end: taken, dropped and returned (measured: all three within
        /// 30-60 s; window 120 s). Captures are too rare for a timed gate (measured ~0.6-0.8 per minute at 11 bots, so
        /// P(none in 120 s) ≈ 25-30 %); <see cref="Diagnose11BotsRound"/> logs them and MatchGateTests scripts one.
        /// </summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator FlagIsTakenDroppedAndReturnedWithin120sWith11Bots()
            => GateEvents(11, 120f, MatchEventKind.FlagTaken, MatchEventKind.FlagDropped, MatchEventKind.FlagReturned);

        IEnumerator GateEvents(int bots, float windowSeconds, params MatchEventKind[] kinds)
        {
            Sandbox.Bots = bots;
            yield return Until(() => DemoStats.PlayersOnServer == bots + 1 && DemoStats.FlagsOnServer == 2, 20f);
            Assert.That(DemoStats.PlayersOnServer, Is.EqualTo(bots + 1), "server players");

            uint startTick = DemoStats.ServerTick;
            int[] start = (int[])DemoStats.MatchEventTotals.Clone();
            bool AllSeen() => System.Array.TrueForAll(kinds, k => DemoStats.MatchEventTotals[(int)k] > start[(int)k]);
            uint windowTicks = (uint)(windowSeconds * 60f);
            float deadline = Time.realtimeSinceStartup + windowSeconds * 1.5f + 10f;
            while (Time.realtimeSinceStartup < deadline && DemoStats.ServerTick - startTick < windowTicks && !AllSeen())
                yield return null;

            float at = (DemoStats.ServerTick - startTick) / 60f;
            Debug.Log($"[MatchFlow] gate {bots} bots, {string.Join("+", kinds)}: after {at:F1}s of server time; " + Totals(start));
            foreach (var kind in kinds)
                Assert.That(DemoStats.MatchEventTotals[(int)kind], Is.GreaterThan(start[(int)kind]),
                    $"no {kind} within {windowSeconds}s of server time with {bots} bots ({Totals(start)})");
        }

        [UnityTest, Explicit("diagnostic: logs the match flow for 120 s"), Timeout(600000)]
        public IEnumerator Diagnose11Bots() => Diagnose(11, 120f, 5f);

        [UnityTest, Explicit("diagnostic: capture rate over a 300 s round"), Timeout(900000)]
        public IEnumerator Diagnose11BotsRound() => Diagnose(11, 300f, 10f);

        [UnityTest, Explicit("diagnostic: capture rate over a 300 s round"), Timeout(900000)]
        public IEnumerator Diagnose31BotsRound() => Diagnose(31, 300f, 10f);

        [UnityTest, Explicit("diagnostic: logs the match flow for 120 s"), Timeout(600000)]
        public IEnumerator Diagnose31Bots() => Diagnose(31, 120f, 5f);

        [UnityTest, Explicit("diagnostic: logs the match flow for 120 s"), Timeout(600000)]
        public IEnumerator Diagnose149Bots() => Diagnose(149, 120f, 5f);

        [UnityTest, Explicit("diagnostic: a full 300 s round at 149 bots: flow plus frame/tick cost over time"), Timeout(900000)]
        public IEnumerator Soak149Bots() => Diagnose(149, 300f, 10f);

        IEnumerator Diagnose(int bots, float serverSeconds, float every)
        {
            Sandbox.Bots = bots;
            yield return Until(() => DemoStats.PlayersOnServer == bots + 1 && DemoStats.FlagsOnServer == 2, 20f);
            Assert.That(DemoStats.PlayersOnServer, Is.EqualTo(bots + 1), "server players");

            uint startTick = DemoStats.ServerTick;
            int[] startTotals = (int[])DemoStats.MatchEventTotals.Clone();
            int lastKills = startTotals[(int)MatchEventKind.PlayerKilled];
            uint lastTick = startTick;
            float lastReal = Time.realtimeSinceStartup;
            int frames = 0;
            float deadline = Time.realtimeSinceStartup + serverSeconds * 1.5f + 10f;
            float nextSample = every;
            Debug.Log($"[MatchFlow] {bots} bots, sampling every {every}s of server time for {serverSeconds}s");
            using var cost = new CostProbe();
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                frames++;
                cost.Accumulate();
                TrackCarries(ServerWorld());
                TrackKills(ServerWorld());
                float t = (DemoStats.ServerTick - startTick) / 60f;
                if (t < nextSample) continue;
                nextSample += every;

                int kills = DemoStats.MatchEventTotals[(int)MatchEventKind.PlayerKilled];
                float dt = (DemoStats.ServerTick - lastTick) / 60f;
                float real = Time.realtimeSinceStartup - lastReal;
                var line = new StringBuilder();
                line.Append($"[MatchFlow] t={t:F0}s frameMs={DemoStats.FrameMs:F1} ticks/s(real)={(DemoStats.ServerTick - lastTick) / math.max(real, 1e-3f):F1} "
                            + $"fps={frames / math.max(real, 1e-3f):F0} kills/s={(kills - lastKills) / math.max(dt, 1e-3f):F1} shots/s={DemoStats.ShotsPerSecond:F0} ");
                line.Append(Totals(startTotals));
                line.Append(Snapshot(ServerWorld(), _last));
                line.Append(KillReport());
                line.Append($" | cost per tick: {cost.Report(DemoStats.ServerTick - lastTick)}");
                line.Append($" | managed heap {System.GC.GetTotalMemory(false) / (1024f * 1024f):F1} MB, server entities {ServerWorld().EntityManager.UniversalQuery.CalculateEntityCount()}");
                Debug.Log(line.ToString());
                lastKills = kills;
                lastTick = DemoStats.ServerTick;
                lastReal = Time.realtimeSinceStartup;
                frames = 0;
                if (t >= serverSeconds) break;
            }
        }

        int _eventsSeen = -1;
        int _eventsLost;
        // [killer role, victim role]; role 0 = attacker bot, 1 = defender bot, 2 = human
        readonly int[,] _killMatrix = new int[3, 3];
        // victim distance from its own base, 10 m bins: 0-10 .. 50+
        readonly int[] _killBins = new int[6];

        int TotalEvents()
        {
            int n = 0;
            foreach (int v in DemoStats.MatchEventTotals) n += v;
            return n;
        }

        /// <summary>Per frame: classifies this frame's PlayerKilled events by killer/victim role and where the victim fell.</summary>
        void TrackKills(World server)
        {
            int total = TotalEvents();
            if (_eventsSeen < 0) { _eventsSeen = total; return; }
            int fresh = total - _eventsSeen;
            _eventsSeen = total;
            if (fresh <= 0) return;
            var ring = DemoStats.LastMatchEvents;
            if (fresh > ring.Count) { _eventsLost += fresh - ring.Count; fresh = ring.Count; }
            var em = server.EntityManager;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>(), ComponentType.ReadOnly<Combatant>(), ComponentType.ReadOnly<GhostInstance>());
            using var ghosts = q.ToComponentDataArray<PlayerGhost>(Allocator.Temp);
            using var combatants = q.ToComponentDataArray<Combatant>(Allocator.Temp);
            using var ids = q.ToComponentDataArray<GhostInstance>(Allocator.Temp);
            for (int k = ring.Count - fresh; k < ring.Count; k++)
            {
                var e = ring[k];
                if (e.Kind != MatchEventKind.PlayerKilled) continue;
                int killer = -1, victim = -1;
                for (int i = 0; i < ids.Length; i++)
                {
                    if (ids[i].ghostId == e.Actor) killer = i;
                    if (ids[i].ghostId == e.Target) victim = i;
                }
                if (killer < 0 || victim < 0) continue;
                _killMatrix[Role(ghosts[killer]), Role(ghosts[victim])]++;
                float2 home = combatants[victim].Team == 0 ? ArenaLayout.Match.RedBase : ArenaLayout.Match.BlueBase;
                _killBins[math.min(5, (int)(math.abs(ghosts[victim].Position.x - home.x) / 10f))]++;
            }
        }

        static int Role(in PlayerGhost g) => g.BotIndex < 0 ? 2 : g.BotIndex % 3 == 0 ? 1 : 0;

        string KillReport()
        {
            var sb = new StringBuilder("kills killer>victim:");
            string[] names = { "A", "D", "H" };
            for (int a = 0; a < 3; a++)
            for (int b = 0; b < 3; b++)
                if (_killMatrix[a, b] > 0) sb.Append($" {names[a]}>{names[b]}={_killMatrix[a, b]}");
            sb.Append(" | victim x-distance from own base (10 m bins):");
            foreach (int n in _killBins) sb.Append($" {n}");
            if (_eventsLost > 0) sb.Append($" | events lost {_eventsLost}");
            System.Array.Clear(_killMatrix, 0, _killMatrix.Length);
            System.Array.Clear(_killBins, 0, _killBins.Length);
            return sb.ToString();
        }

        struct Carry
        {
            public bool Active;
            public int Carrier;
            public uint StartTick;
            public float2 Start, Last;
            public float Path;
            public int MaxEnemiesNear, MaxMatesNear;
        }

        readonly Carry[] _carry = new Carry[2];

        /// <summary>Per frame: follows each carried flag and logs the carry when it ends (dropped or captured).</summary>
        void TrackCarries(World server)
        {
            var em = server.EntityManager;
            using var fq = em.CreateEntityQuery(ComponentType.ReadOnly<FlagGhost>());
            using var flags = fq.ToComponentDataArray<FlagGhost>(Allocator.Temp);
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>(), ComponentType.ReadOnly<Combatant>(), ComponentType.ReadOnly<GhostInstance>());
            using var ghosts = q.ToComponentDataArray<PlayerGhost>(Allocator.Temp);
            using var combatants = q.ToComponentDataArray<Combatant>(Allocator.Temp);
            using var ids = q.ToComponentDataArray<GhostInstance>(Allocator.Temp);
            foreach (var f in flags)
            {
                ref var c = ref _carry[f.Team];
                float2 at = f.Position.xz;
                if (f.State == FlagState.Carried)
                {
                    if (!c.Active || c.Carrier != f.CarrierGhostId)
                        c = new Carry { Active = true, Carrier = f.CarrierGhostId, StartTick = DemoStats.ServerTick, Start = at, Last = at };
                    c.Path += math.distance(c.Last, at);
                    c.Last = at;
                    int enemies = 0, mates = 0;
                    byte carrierTeam = (byte)(1 - f.Team);
                    for (int i = 0; i < ghosts.Length; i++)
                    {
                        if (!combatants[i].Alive || ids[i].ghostId == f.CarrierGhostId) continue;
                        float d = math.distance(ghosts[i].Position.xz, at);
                        if (combatants[i].Team == carrierTeam) { if (d < 2f) mates++; }
                        else if (d < MatchTuning.BotFireRange) enemies++;
                    }
                    c.MaxEnemiesNear = math.max(c.MaxEnemiesNear, enemies);
                    c.MaxMatesNear = math.max(c.MaxMatesNear, mates);
                }
                else if (c.Active)
                {
                    c.Active = false;
                    float2 home = (1 - f.Team) == 0 ? ArenaLayout.Match.RedBase : ArenaLayout.Match.BlueBase;
                    Debug.Log($"[MatchFlow] carry of {(f.Team == 0 ? "Red" : "Blue")} flag by {c.Carrier} ended {f.State}: {(DemoStats.ServerTick - c.StartTick) / 60f:F1}s, "
                              + $"from ({c.Start.x:F1},{c.Start.y:F1}) to ({c.Last.x:F1},{c.Last.y:F1}), path {c.Path:F1} m, still {math.distance(c.Last, home):F1} m from home; "
                              + $"max enemies <25m {c.MaxEnemiesNear}, max mates <2m {c.MaxMatesNear}");
                }
            }
        }

        /// <summary>
        /// Per-frame time of a few systems from the profiler markers (summed over frames, reported per server tick), to
        /// see whether per-tick cost grows over a long round.
        /// </summary>
        sealed class CostProbe : System.IDisposable
        {
            static readonly string[] Names =
            {
                "BotBrainSystem", "HitscanSystem", "MatchRulesSystem", "PlayerSeparationSystem", "PlayerMovementSystem",
                "InstancedRenderSystem", "GhostSendSystem", "PlayerLoop",
            };
            readonly System.Collections.Generic.List<(string name, Unity.Profiling.ProfilerRecorder rec)> _recorders = new();
            readonly System.Collections.Generic.Dictionary<string, double> _sumNs = new();

            public CostProbe()
            {
                var handles = new System.Collections.Generic.List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
                Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
                foreach (var h in handles)
                {
                    var d = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(h);
                    foreach (var n in Names)
                    {
                        if (d.Name != n && !d.Name.EndsWith("." + n)) continue;
                        if (_sumNs.ContainsKey(d.Name)) continue;
                        _recorders.Add((d.Name, Unity.Profiling.ProfilerRecorder.StartNew(d.Category, d.Name, 1)));
                        _sumNs[d.Name] = 0;
                    }
                }
            }

            public void Accumulate()
            {
                foreach (var (name, rec) in _recorders)
                    if (rec.Valid) _sumNs[name] += rec.LastValue;
            }

            public string Report(uint ticks)
            {
                var sb = new StringBuilder();
                foreach (var (name, _) in _recorders)
                {
                    string shortName = name.Substring(name.LastIndexOf('.') + 1).Replace("System", "");
                    sb.Append($"{shortName} {_sumNs[name] / 1e6 / math.max(1u, ticks):F3} ");
                    _sumNs[name] = 0;
                }
                return sb.Length > 0 ? sb.ToString() + "ms" : "(no markers)";
            }

            public void Dispose()
            {
                foreach (var (_, rec) in _recorders) rec.Dispose();
            }
        }

        static string Totals(int[] start)
        {
            int D(MatchEventKind k) => DemoStats.MatchEventTotals[(int)k] - start[(int)k];
            return $"taken={D(MatchEventKind.FlagTaken)} dropped={D(MatchEventKind.FlagDropped)} returned={D(MatchEventKind.FlagReturned)} "
                   + $"captured={D(MatchEventKind.FlagCaptured)} killed={D(MatchEventKind.PlayerKilled)} | ";
        }

        /// <summary>Per team: flags, attackers alive, their distance to the enemy flag, stalled bots, and where dead attackers fell.</summary>
        readonly System.Collections.Generic.Dictionary<int, float2> _last = new();

        /// <summary>Stuck = alive attacker that moved less than 3 m net since the previous sample (5-10 s ago).</summary>
        static string Snapshot(World server, System.Collections.Generic.Dictionary<int, float2> last)
        {
            var em = server.EntityManager;
            var sb = new StringBuilder();
            var layout = ArenaLayout.Match;
            float2 redFlag = layout.RedBase, blueFlag = layout.BlueBase;
            using (var fq = em.CreateEntityQuery(ComponentType.ReadOnly<FlagGhost>()))
            using (var flags = fq.ToComponentDataArray<FlagGhost>(Allocator.Temp))
                foreach (var f in flags)
                {
                    sb.Append($"{(f.Team == 0 ? "RedFlag" : "BlueFlag")}={f.State}@({f.Position.x:F0},{f.Position.z:F0}) ");
                    if (f.Team == 0) redFlag = f.Position.xz; else blueFlag = f.Position.xz;
                }
            using (var mq = em.CreateEntityQuery(ComponentType.ReadOnly<MatchStateGhost>()))
                if (mq.CalculateEntityCount() == 1)
                {
                    var m = mq.GetSingleton<MatchStateGhost>();
                    sb.Append($"score {m.RedScore}-{m.BlueScore} {m.Phase} | ");
                }

            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>(), ComponentType.ReadOnly<Combatant>());
            using var ghosts = q.ToComponentDataArray<PlayerGhost>(Allocator.Temp);
            using var combatants = q.ToComponentDataArray<Combatant>(Allocator.Temp);
            for (byte team = 0; team < 2; team++)
            {
                float2 enemyFlag = team == 0 ? blueFlag : redFlag;
                float toward = team == 0 ? 1f : -1f; // +x is toward the enemy for Red
                int stuck = 0;
                var stuckAt = new StringBuilder();
                int attackers = 0, alive = 0, within10 = 0, stalled = 0, stalledAtBox = 0, defendersAlive = 0, defenders = 0, dead = 0;
                float sumD = 0f, minD = float.MaxValue, sumProgress = 0f, maxProgress = float.MinValue, sumDeadProgress = 0f;
                for (int i = 0; i < ghosts.Length; i++)
                {
                    var g = ghosts[i];
                    var c = combatants[i];
                    if (c.Team != team || g.BotIndex < 0) continue;
                    if (g.BotIndex % 3 == 0)
                    {
                        defenders++;
                        if (c.Alive) defendersAlive++;
                        continue;
                    }
                    attackers++;
                    float progress = g.Position.x * toward;
                    bool wasAlive = last.TryGetValue(g.BotIndex, out float2 before);
                    if (c.Alive) last[g.BotIndex] = g.Position.xz; else last.Remove(g.BotIndex);
                    if (c.Alive && wasAlive && math.distance(before, g.Position.xz) < 3f)
                    {
                        stuck++;
                        if (stuck <= 4) stuckAt.Append($"({g.Position.x:F1},{g.Position.z:F1}) ");
                    }
                    if (!c.Alive)
                    {
                        dead++;
                        sumDeadProgress += progress;
                        continue;
                    }
                    alive++;
                    float d = math.distance(g.Position.xz, enemyFlag);
                    sumD += d;
                    minD = math.min(minD, d);
                    if (d < 10f) within10++;
                    sumProgress += progress;
                    maxProgress = math.max(maxProgress, progress);
                    if (math.length(g.Velocity) < 0.5f)
                    {
                        stalled++;
                        if (NearBox(g.Position.xz, layout, 1.5f)) stalledAtBox++;
                    }
                }
                sb.Append($"{(team == 0 ? "Red" : "Blue")}: atk {alive}/{attackers} alive meanD={(alive > 0 ? sumD / alive : 0f):F1} minD={(alive > 0 ? minD : 0f):F1} "
                          + $"<10m={within10} meanX+={(alive > 0 ? sumProgress / alive : 0f):F1} maxX+={(alive > 0 ? maxProgress : 0f):F1} "
                          + $"stalled={stalled} (atBox {stalledAtBox}) stuck={stuck} {stuckAt}deadMeanX+={(dead > 0 ? sumDeadProgress / dead : 0f):F1}; def {defendersAlive}/{defenders} | ");
            }
            return sb.ToString();
        }

        static bool NearBox(float2 p, in ArenaLayout layout, float margin)
        {
            for (int b = 0; b < layout.Boxes.Length; b++)
            {
                var box = layout.Boxes[b];
                float2 d = math.abs(p - box.Center) - box.HalfExtents;
                if (math.length(math.max(d, 0f)) <= margin) return true;
            }
            return false;
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
