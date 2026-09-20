using FlagRush.Domain.Primitives;

namespace FlagRush.Domain.Aspects
{
    /// <summary>Has a location in the world. Things without this aspect are abstract (profiles, teams).</summary>
    public interface IPositioned : IEntity
    {
        Point3 Position { get; }
    }
}
