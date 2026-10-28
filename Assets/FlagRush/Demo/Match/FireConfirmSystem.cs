using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace FlagRush.Demo
{
    /// <summary>
    /// Client: publishes the level-4 readouts once per frame.
    /// <list type="bullet">
    /// <item><b>Fire → server confirm</b>: on the frame <see cref="PlayerInput.Fire"/> goes true, records the time and
    /// the owned <see cref="MatchStats.ShotsFired"/>; when a snapshot carries a different count, the elapsed ms is
    /// <see cref="DemoStats.FireConfirmMs"/>. A press the server never counts (cooldown, dead) is dropped after 2 s.</item>
    /// <item>Scoreboard, round clock, the owned player's k/d/c and each flag's line, from the replicated ghosts and the
    /// client's server-tick estimate.</item>
    /// </list>
    /// Everything reads zero or empty below Level 4.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class FireConfirmSystem : SystemBase
    {
        const float PendingTimeoutSeconds = 2f;

        bool _pending;
        float _pressTime;
        ushort _shotsAtPress;
        bool _fireWasDown;

        protected override void OnUpdate()
        {
            if (!DemoConfig.IsMatchLevel)
            {
                Reset();
                return;
            }

            uint tick = 0;
            if (SystemAPI.TryGetSingleton<NetworkTime>(out var time) && time.ServerTick.IsValid)
                tick = time.ServerTick.TickIndexForValidTick;

            int ownedId = 0;
            var owned = DemoStats.OwnedPlayerEntity;
            if (owned != Entity.Null && EntityManager.Exists(owned) && EntityManager.HasComponent<MatchStats>(owned))
            {
                var stats = EntityManager.GetComponentData<MatchStats>(owned);
                DemoStats.YouKills = stats.Kills;
                DemoStats.YouDeaths = stats.Deaths;
                DemoStats.YouCaptures = stats.Captures;
                if (EntityManager.HasComponent<GhostInstance>(owned)) ownedId = EntityManager.GetComponentData<GhostInstance>(owned).ghostId;
                SampleFire(owned, stats.ShotsFired);
            }
            else
            {
                _pending = false;
                _fireWasDown = false;
            }

            foreach (var match in SystemAPI.Query<RefRO<MatchStateGhost>>())
            {
                var m = match.ValueRO;
                DemoStats.MatchRedScore = m.RedScore;
                DemoStats.MatchBlueScore = m.BlueScore;
                DemoStats.MatchEnded = m.Phase == MatchPhase.Ended;
                uint elapsed = tick - m.RoundStartTick;
                if (elapsed > uint.MaxValue / 2u) elapsed = 0u; // estimate behind the round start
                DemoStats.MatchTicksLeft = DemoStats.MatchEnded || elapsed >= MatchTuning.RoundTicks ? 0u : MatchTuning.RoundTicks - elapsed;
            }

            foreach (var flag in SystemAPI.Query<RefRO<FlagGhost>>())
            {
                string line = MatchHudText.FlagLine(flag.ValueRO, ownedId, tick);
                if (flag.ValueRO.Team == 0) DemoStats.RedFlagLine = line;
                else DemoStats.BlueFlagLine = line;
            }
        }

        void SampleFire(Entity owned, ushort shotsFired)
        {
            bool fire = EntityManager.HasComponent<PlayerInput>(owned) && EntityManager.GetComponentData<PlayerInput>(owned).Fire;
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (_pending)
            {
                if ((ushort)(shotsFired - _shotsAtPress) != 0)
                {
                    DemoStats.FireConfirmMs = (now - _pressTime) * 1000f;
                    _pending = false;
                }
                else if (now - _pressTime > PendingTimeoutSeconds) _pending = false;
            }
            if (fire && !_fireWasDown && !_pending)
            {
                _pending = true;
                _pressTime = now;
                _shotsAtPress = shotsFired;
            }
            _fireWasDown = fire;
        }

        void Reset()
        {
            _pending = false;
            _fireWasDown = false;
            DemoStats.FireConfirmMs = 0f;
            DemoStats.MatchRedScore = 0;
            DemoStats.MatchBlueScore = 0;
            DemoStats.MatchEnded = false;
            DemoStats.MatchTicksLeft = 0;
            DemoStats.YouKills = 0;
            DemoStats.YouDeaths = 0;
            DemoStats.YouCaptures = 0;
            DemoStats.RedFlagLine = "";
            DemoStats.BlueFlagLine = "";
        }
    }
}
