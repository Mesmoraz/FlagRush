namespace FlagRush.Domain.Aspects
{
    /// <summary>Can take damage and die. Vehicles and machines can have this too, not only avatars.</summary>
    public interface IHasHealth : IEntity
    {
        float Health { get; }
        float MaxHealth { get; }

        bool IsAlive => Health > 0f;
    }
}
