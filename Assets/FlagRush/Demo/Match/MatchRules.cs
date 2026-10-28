using Unity.Collections;

namespace FlagRush.Demo
{
    /// <summary>Per-round plain data (score, phase, timers).</summary>
    public struct MatchData
    {
        public short RedScore, BlueScore;
        public MatchPhase Phase;
        public uint PhaseEndTick;
        public uint RoundStartTick;
    }

    /// <summary>Pure round rules. No [BurstCompile]; inlined into Burst systems.</summary>
    public static class MatchRules
    {
        public static MatchData NewRound(uint tick) =>
            new() { Phase = MatchPhase.Playing, RoundStartTick = tick };

        /// <summary>
        /// Applies this tick's FlagCaptured events and the round timer. Appends only RoundEnded / RoundReset
        /// to events; thisTick is read, never copied into events.
        /// </summary>
        public static void Step(ref MatchData m, NativeArray<MatchEvent> thisTick, uint tick, NativeList<MatchEvent> events)
        {
            if (m.Phase == MatchPhase.Playing)
            {
                for (int i = 0; i < thisTick.Length; i++)
                {
                    if (thisTick[i].Kind != MatchEventKind.FlagCaptured) continue;
                    if (thisTick[i].Team == 0) m.RedScore++; else m.BlueScore++;
                }

                bool won = m.RedScore >= MatchTuning.CapturesToWin || m.BlueScore >= MatchTuning.CapturesToWin;
                if (won || tick - m.RoundStartTick >= MatchTuning.RoundTicks)
                {
                    m.Phase = MatchPhase.Ended;
                    m.PhaseEndTick = tick + MatchTuning.EndedTicks;
                    byte winner = m.RedScore > m.BlueScore ? (byte)0 : m.BlueScore > m.RedScore ? (byte)1 : (byte)255;
                    events.Add(new MatchEvent { Kind = MatchEventKind.RoundEnded, Tick = tick, Team = winner });
                }
            }
            else if (tick >= m.PhaseEndTick)
            {
                m = NewRound(tick);
                events.Add(new MatchEvent { Kind = MatchEventKind.RoundReset, Tick = tick });
            }
        }
    }
}
