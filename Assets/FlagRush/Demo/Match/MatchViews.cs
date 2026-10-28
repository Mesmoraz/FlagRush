using Unity.NetCode;

namespace FlagRush.Demo
{
    /// <summary>Maps server components to the plain rule inputs. No [BurstCompile]; inlined into Burst systems.</summary>
    public static class MatchViews
    {
        /// <summary>
        /// Id = ghost id. A ghost id is allocated when the ghost is first sent; until then the player takes no part in
        /// rules (not alive, unique negative id that no carrier field can hold). <paramref name="ordinal"/> makes it unique.
        /// </summary>
        public static PlayerView ToView(in PlayerGhost ghost, in Combatant combatant, in GhostInstance instance, int ordinal)
        {
            int id = instance.ghostId;
            bool known = id != 0;
            return new PlayerView
            {
                Id = known ? id : -1 - ordinal,
                Position = ghost.Position.xz,
                Team = combatant.Team,
                Alive = known && combatant.Alive,
            };
        }

        public static FlagData ToData(in FlagGhost g) => new()
        {
            Team = g.Team, State = g.State, Position = g.Position.xz, CarrierId = g.CarrierGhostId, DroppedTick = g.DroppedTick,
        };
    }
}
