using Microsoft.Extensions.Logging;

namespace IMSB.LoggerLibrary
{
    /// <summary><see cref="DatabaseLoggerOptions"/> resolved once at startup (connection string looked up, defaults applied).</summary>
    internal sealed record DatabaseLoggerSettings(
        string ConnectionString,
        string AppType,
        LogLevel MinimumLevel,
        int QueueSize,
        int BatchSize,
        TimeSpan FlushInterval);
}
