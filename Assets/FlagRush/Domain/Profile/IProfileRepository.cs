using FlagRush.Domain.Ids;
using FlagRush.Domain.Ports;

namespace FlagRush.Domain.Profile
{
    /// <summary>Durable store of profiles. Just a typed persistence port; exists so callers name the intent.</summary>
    public interface IProfileRepository : IPersistenceStore<AuthId, IPlayerProfile>
    {
    }
}
