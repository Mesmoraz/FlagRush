using FlagRush.Domain.Ids;

namespace FlagRush.Domain.Aspects
{
    /// <summary>
    /// Anything that exists in the simulated world. Concrete kinds (avatar, flag, item, vehicle, machine)
    /// are not types of their own; they are whatever set of aspects an entity happens to implement.
    /// </summary>
    public interface IEntity
    {
        EntityId Id { get; }
    }
}
