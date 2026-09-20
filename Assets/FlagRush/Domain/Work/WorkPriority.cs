namespace FlagRush.Domain.Work
{
    /// <summary>How urgently a scheduler should advance a work item when it cannot advance everything.</summary>
    public enum WorkPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
    }
}
