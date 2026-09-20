namespace FlagRush.Domain.Ports
{
    /// <summary>
    /// Key/value durability. Backed by memory in tests, a file on a dedicated server, or a cloud save
    /// service; the domain only needs load/save/delete.
    /// </summary>
    public interface IPersistenceStore<TKey, TRecord>
    {
        bool TryLoad(TKey key, out TRecord record);
        void Save(TKey key, TRecord record);
        bool Delete(TKey key);
    }
}
