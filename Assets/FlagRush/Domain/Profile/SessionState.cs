namespace FlagRush.Domain.Profile
{
    public enum SessionState
    {
        Connected = 0,

        /// <summary>The connection dropped but the binding is kept so the same AuthId can resume.</summary>
        Disconnected = 1,
    }
}
