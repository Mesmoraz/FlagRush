namespace FlagRush.Domain.Aspects
{
    /// <summary>
    /// Can be owned (a placed machine, a parked vehicle, a dropped item). The owner itself is recorded as an
    /// <c>Owns</c> relationship so ownership can be queried from either side and stolen/transferred as data.
    /// </summary>
    public interface IOwnable : IEntity
    {
    }
}
