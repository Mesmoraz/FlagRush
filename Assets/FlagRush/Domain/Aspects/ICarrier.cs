namespace FlagRush.Domain.Aspects
{
    /// <summary>Can carry <see cref="ICarryable"/> entities (avatars, vehicle trunks).</summary>
    public interface ICarrier : IPositioned
    {
        /// <summary>Total <see cref="ICarryable.CarryCost"/> this carrier can hold at once.</summary>
        float CarryCapacity { get; }
    }
}
