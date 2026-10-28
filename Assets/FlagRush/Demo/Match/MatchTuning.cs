namespace FlagRush.Demo
{
    /// <summary>Level 4a rule constants. Durations are simulation ticks at 60 Hz.</summary>
    public static class MatchTuning
    {
        public const uint FireIntervalTicks = 15;
        public const uint RespawnTicks = 180;
        public const uint AutoReturnTicks = 600;
        public const uint RoundTicks = 18000;
        public const uint EndedTicks = 300;
        public const uint HitFlashTicks = 9;

        public const byte MaxHealth = 100;
        public const byte Damage = 34;
        public const float ShotRange = 40f;
        public const float PickupRadius = 1.2f;
        public const float BaseRadius = 3f;
        public const int CapturesToWin = 3;

        public const float BotFireRange = 25f;
        public const float BotAimErrorDegrees = 6f;
        public const float BotHoldRadius = 6f;
        public const float BotSprintDistance = 15f;
        public const float BotCornerMargin = 1.5f;
    }
}
