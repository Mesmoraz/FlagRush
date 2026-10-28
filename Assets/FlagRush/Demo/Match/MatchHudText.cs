namespace FlagRush.Demo
{
    /// <summary>Pure text and timing helpers for the level-4 HUD and hit flash. No [BurstCompile]; managed use only.</summary>
    public static class MatchHudText
    {
        /// <summary>"m:ss" for a tick count at 60 Hz; partial seconds round up so the clock never shows 0:00 early.</summary>
        public static string Clock(uint ticksLeft)
        {
            uint seconds = (ticksLeft + 59u) / 60u;
            return $"{seconds / 60u}:{seconds % 60u:00}";
        }

        public static string Banner(short red, short blue)
            => red > blue ? $"Red wins {red}–{blue}" : blue > red ? $"Blue wins {red}–{blue}" : $"Draw {red}–{blue}";

        /// <summary>"home" | "carried by you" | "carried" | "dropped · 7 s" (seconds until auto-return, rounded up).</summary>
        public static string FlagLine(in FlagGhost flag, int ownedGhostId, uint tick)
        {
            switch (flag.State)
            {
                case FlagState.Carried:
                    return flag.CarrierGhostId == ownedGhostId ? "carried by you" : "carried";
                case FlagState.Dropped:
                    uint elapsed = tick - flag.DroppedTick;
                    if (elapsed > uint.MaxValue / 2u) elapsed = 0u; // tick estimate behind the drop: show the full wait
                    uint left = elapsed >= MatchTuning.AutoReturnTicks ? 0u : MatchTuning.AutoReturnTicks - elapsed;
                    return $"dropped · {(left + 59u) / 60u} s";
                default:
                    return "home";
            }
        }

        public const float OwnedFlashSeconds = 0.15f;

        /// <summary>Latch state for the owned player's hit flash (real time, not ticks).</summary>
        public struct HitLatch
        {
            public uint Seen;
            public float Until;
        }

        /// <summary>
        /// Owned player's flash: the predicted tick runs a round trip ahead of the snapshot that carries LastHitTick, so a
        /// tick window would be missed. A new non-zero LastHitTick starts a 0.15 s real-time flash; the same value does not
        /// restart it; 0 never flashes.
        /// </summary>
        public static bool OwnedHitFlash(ref HitLatch latch, uint lastHit, float now)
        {
            if (lastHit == 0) { latch.Seen = 0; return false; }
            if (lastHit != latch.Seen)
            {
                latch.Seen = lastHit;
                latch.Until = now + OwnedFlashSeconds;
            }
            return now < latch.Until;
        }

        /// <summary>Hit flash window: 0 ≤ (now − lastHit) ≤ HitFlashTicks, wrap-safe; lastHit 0 means never hit.</summary>
        public static bool IsHitFlash(uint now, uint lastHit)
            => lastHit != 0 && now - lastHit <= MatchTuning.HitFlashTicks;
    }
}
