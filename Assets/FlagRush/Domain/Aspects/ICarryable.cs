namespace FlagRush.Domain.Aspects
{
    /// <summary>
    /// Can be picked up and physically carried by an <see cref="ICarrier"/>. Flags and world items are
    /// carryable; who carries what is a <c>Carries</c> relationship, never a field here.
    /// </summary>
    public interface ICarryable : IPositioned
    {
        /// <summary>Weight/bulk in whatever unit the rule set chooses; carriers compare it to their capacity.</summary>
        float CarryCost { get; }
    }
}
