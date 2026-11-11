using Microsoft.Extensions.Logging;

namespace IMSB.LoggerLibrary
{
    /// <summary>
    /// The "DatabaseLogging" section of appsettings.json:
    /// <code>
    /// "DatabaseLogging": { "MinimumLevel": "Warning" }
    /// </code>
    /// Leave <see cref="Enabled"/> out to log everywhere except the "Testing" environment (the integration tests).
    /// </summary>
    public sealed class DatabaseLoggerOptions
    {
        public const string Section = "DatabaseLogging";

        /// <summary>true / false to force database logging on or off; null = on, except in the "Testing" environment.</summary>
        public bool? Enabled { get; set; }

        /// <summary>Entries below this level are not written to the database (console logging is unaffected).</summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Warning;

        /// <summary>The connection string (ConnectionStrings section) of the database that holds FW.TblLog.</summary>
        public string ConnectionStringName { get; set; } = "DefaultConnection";

        /// <summary>The AppType column; defaults to the name passed to AddImsbLogging (e.g. "IMSB.Admin").</summary>
        public string? AppType { get; set; }

        /// <summary>Entries waiting to be written; when full, the oldest are dropped rather than slowing requests down.</summary>
        public int QueueSize { get; set; } = 5000;

        /// <summary>Most entries sent to the database in one bulk insert.</summary>
        public int BatchSize { get; set; } = 200;

        /// <summary>How long the writer waits after the first entry, so a burst of entries goes in one insert.</summary>
        public int FlushIntervalMilliseconds { get; set; } = 2000;
    }
}
