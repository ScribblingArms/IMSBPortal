using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IMSB.LoggerLibrary
{
    /// <summary>
    /// Creates the <see cref="DatabaseLogger"/>s. The alias lets appsettings set per-category levels for this provider only,
    /// e.g. "Logging": { "Database": { "LogLevel": { "Microsoft.EntityFrameworkCore": "Error" } } }.
    /// </summary>
    [ProviderAlias("Database")]
    internal sealed class DatabaseLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, DatabaseLogger> _loggers = new(StringComparer.Ordinal);
        private readonly LogQueue _queue;
        private readonly DatabaseLoggerSettings _settings;
        private readonly IHttpContextAccessor _http;

        public DatabaseLoggerProvider(LogQueue queue, DatabaseLoggerSettings settings, IHttpContextAccessor http)
        {
            _queue = queue;
            _settings = settings;
            _http = http;
        }

        public ILogger CreateLogger(string categoryName) =>
            _loggers.GetOrAdd(categoryName, name => new DatabaseLogger(name, _queue, _settings, _http));

        public void Dispose() => _loggers.Clear();
    }
}
