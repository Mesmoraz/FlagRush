namespace FlagRush.Domain.Ports
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    /// <summary>Where the domain writes diagnostics. Adapters forward to the engine console, a file, or nothing.</summary>
    public interface ILogSink
    {
        void Log(LogLevel level, string message);
    }
}
