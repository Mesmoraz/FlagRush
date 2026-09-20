namespace FlagRush.Domain.Simulation
{
    /// <summary>
    /// Can capture and restore its complete state. This is the seam client prediction and rollback need;
    /// what a snapshot looks like is the implementation's business.
    /// </summary>
    public interface ISnapshotable<TSnapshot>
    {
        TSnapshot Capture();
        void Restore(in TSnapshot snapshot);
    }
}
