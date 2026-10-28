using System.Collections.Generic;
using System.Linq;
using FlagRush.Domain.Aspects;
using FlagRush.Domain.Ids;
using FlagRush.Domain.Primitives;
using FlagRush.Domain.Registry;
using FlagRush.Domain.Simulation;
using Unity.Collections;
using Unity.Mathematics;

namespace FlagRush.Demo.Tests
{
    /// <summary>Match state, kept in the context so the rule set itself holds nothing between steps.</summary>
    public sealed class MatchStateEntity : IEntity
    {
        public EntityId Id { get; } = new EntityId(CaptureTheFlagRuleSet.MatchStateId);
        public FlagData Red;
        public FlagData Blue;
        public MatchData Match;
    }

    public sealed class MoveTo : IInput
    {
        public PlayerId Player { get; set; }
        public Tick Tick { get; set; }
        public float2 Position;
    }

    public sealed class MatchGameEvent : GameEvent
    {
        public MatchGameEvent(MatchEvent e) : base(new Tick(e.Tick)) { Event = e; }
        public MatchEvent Event { get; }
    }

    /// <summary>
    /// Capture-the-flag as an <see cref="IRuleSet"/>: the pure flag/round rules driven through the Domain contracts.
    /// Team ids: TeamId(1) = Red (byte 0), TeamId(2) = Blue (byte 1). An avatar's EntityId equals its PlayerId value.
    /// </summary>
    public sealed class CaptureTheFlagRuleSet : IRuleSet
    {
        public const int MatchStateId = 900;
        public const int RedFlagId = 901;
        public const int BlueFlagId = 902;

        public string Name => "CaptureTheFlag";

        public void Initialize(IRuleContext context)
        {
            var layout = ArenaLayout.Match;
            var state = new MatchStateEntity
            {
                Red = FlagData.AtHome(0, layout),
                Blue = FlagData.AtHome(1, layout),
                Match = MatchRules.NewRound(context.Tick.Value),
            };
            context.Entities.Add(state);
            context.Entities.Add(new FakeFlag(RedFlagId, new TeamId(1), ToPoint(state.Red.Position)));
            context.Entities.Add(new FakeFlag(BlueFlagId, new TeamId(2), ToPoint(state.Blue.Position)));
        }

        public void Apply(IRuleContext context, IReadOnlyList<IInput> inputs)
        {
            var layout = ArenaLayout.Match;
            context.Entities.TryGet<MatchStateEntity>(new EntityId(MatchStateId), out var state);
            uint tick = context.Tick.Value;

            foreach (var input in inputs)
            {
                if (input is MoveTo move && context.Entities.TryGet<IMovable>(new EntityId(move.Player.Value), out var avatar))
                    avatar.MoveTo(new Point3(move.Position.x, 0f, move.Position.y));
            }

            var avatars = context.Entities.OfAspect<ICarrier>().OfType<IHasTeam>().OrderBy(a => a.Id.Value).ToList();
            var players = new NativeArray<PlayerView>(avatars.Count, Allocator.Temp);
            for (int i = 0; i < avatars.Count; i++)
            {
                var p = ((IPositioned)avatars[i]).Position;
                players[i] = new PlayerView
                {
                    Id = avatars[i].Id.Value,
                    Position = new float2(p.X, p.Z),
                    Team = (byte)(avatars[i].Team.Value - 1),
                    Alive = true,
                };
            }

            var events = new NativeList<MatchEvent>(Allocator.Temp);
            FlagRules.Step(ref state.Red, ref state.Blue, players, layout, tick, events);
            var thisTick = new NativeArray<MatchEvent>(events.Length, Allocator.Temp);
            for (int i = 0; i < thisTick.Length; i++) thisTick[i] = events[i];
            MatchRules.Step(ref state.Match, thisTick, tick, events);

            Mirror(context, RedFlagId, in state.Red);
            Mirror(context, BlueFlagId, in state.Blue);

            for (int i = 0; i < events.Length; i++) context.Emit(new MatchGameEvent(events[i]));

            thisTick.Dispose();
            events.Dispose();
            players.Dispose();
        }

        /// <summary>Moves the flag entity and keeps exactly one Carries edge while carried, none otherwise.</summary>
        static void Mirror(IRuleContext context, int flagId, in FlagData flag)
        {
            var id = new EntityId(flagId);
            if (context.Entities.TryGet<IMovable>(id, out var entity)) entity.MoveTo(ToPoint(flag.Position));

            foreach (var edge in context.Relationships.To(id, RelationshipKind.Carries).ToList())
            {
                if (flag.State != FlagState.Carried || edge.Subject.Value != flag.CarrierId)
                    context.Relationships.Remove(edge);
            }
            if (flag.State == FlagState.Carried)
                context.Relationships.Add(new Relationship(RelationshipKind.Carries, new EntityId(flag.CarrierId), id));
        }

        static Point3 ToPoint(float2 p) => new Point3(p.x, 0f, p.y);
    }
}
