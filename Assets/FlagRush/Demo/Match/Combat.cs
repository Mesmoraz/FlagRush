namespace FlagRush.Demo
{
    /// <summary>Pure combat rules. No [BurstCompile]; inlined into Burst systems.</summary>
    public static class Combat
    {
        public static bool TryFire(ref uint nextFireTick, uint tick)
        {
            if (tick < nextFireTick) return false;
            nextFireTick = tick + MatchTuning.FireIntervalTicks;
            return true;
        }

        /// <summary>Applies one hit; returns true when it killed the target.</summary>
        public static bool ApplyHit(ref Combatant target, uint tick)
        {
            int health = target.Health - MatchTuning.Damage;
            target.Health = (byte)(health < 0 ? 0 : health);
            target.LastHitTick = tick;
            if (target.Health != 0) return false;
            uint respawn = tick + MatchTuning.RespawnTicks;
            target.RespawnTick = respawn == 0 ? 1u : respawn;
            return true;
        }
    }
}
