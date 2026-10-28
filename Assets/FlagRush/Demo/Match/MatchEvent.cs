using Unity.Mathematics;

namespace FlagRush.Demo
{
    public enum MatchEventKind : byte { FlagTaken, FlagDropped, FlagReturned, FlagCaptured, PlayerHit, PlayerKilled, RoundEnded, RoundReset }

    /// <summary>A rule outcome. Actor/Target are player ids (ghost ids on the server); Team is the flag's or scoring team (255 = draw).</summary>
    public struct MatchEvent
    {
        public MatchEventKind Kind;
        public uint Tick;
        public int Actor;
        public int Target;
        public byte Team;
    }

    /// <summary>A player as the flag and bot rules see it. Id is never 0.</summary>
    public struct PlayerView
    {
        public int Id;
        public float2 Position;
        public byte Team;
        public bool Alive;
        /// <summary>A bot in the Attacker role (<see cref="BotBrain.IsDefender"/> is false). Humans and defenders: false.</summary>
        public bool Attacker;
    }
}
