using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IMSB.LoggerLibrary
{
    /// <summary>
    /// Turns a log call into an FW.TblLog row and queues it; the database write happens on <see cref="DatabaseLogWriter"/>,
    /// so a log call costs the caller no I/O.
    /// </summary>
    internal sealed class DatabaseLogger : ILogger
    {
        private const int MessageLength = 1000, LevelLength = 10, IpLength = 20, AppTypeLength = 50;

        private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

        private readonly string _category;
        private readonly LogQueue _queue;
        private readonly DatabaseLoggerSettings _settings;
        private readonly IHttpContextAccessor _http;
        private readonly bool _ignored;

        public DatabaseLogger(string category, LogQueue queue, DatabaseLoggerSettings settings, IHttpContextAccessor http)
        {
            _category = category;
            _queue = queue;
            _settings = settings;
            _http = http;
            // this library's own messages (and SqlClient's) would loop back into the queue when the database is failing
            _ignored = category.StartsWith("IMSB.LoggerLibrary", StringComparison.Ordinal)
                    || category.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal);
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            !_ignored && logLevel != LogLevel.None && logLevel >= _settings.MinimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message)) message = exception?.Message ?? string.Empty;

            var request = _http.HttpContext;   // null outside a request (startup, background work)
            var ip = ClientIp(request);
            var context = JsonSerializer.Serialize(new
            {
                category = _category,
                eventId = eventId.Id == 0 ? (int?)null : eventId.Id,
                eventName = eventId.Name,
                exception = exception?.ToString(),
                method = request?.Request.Method,
                path = request == null ? null : request.Request.Path + request.Request.QueryString,
                user = request?.User.Identity?.IsAuthenticated == true ? request.User.Identity.Name : null,
                traceId = request?.TraceIdentifier,
                ip = ip != null && ip.Length > IpLength ? ip : null,   // the full address when LogIP had to cut it
                machine = Environment.MachineName
            }, Json);

            _queue.Enqueue(new LogEntry(
                Cut(_settings.AppType, AppTypeLength),
                DateTime.Now,
                Cut(LevelName(logLevel), LevelLength)!,
                Cut(message, MessageLength)!,
                Cut(ip, IpLength),
                context));
        }

        /// <summary>Short names: "Information" would not fit the varchar(10) Level column.</summary>
        private static string LevelName(LogLevel level) => level switch
        {
            LogLevel.Trace => "Trace",
            LogLevel.Debug => "Debug",
            LogLevel.Information => "Info",
            LogLevel.Warning => "Warning",
            LogLevel.Error => "Error",
            LogLevel.Critical => "Critical",
            _ => level.ToString()
        };

        private static string? ClientIp(HttpContext? request)
        {
            var address = request?.Connection.RemoteIpAddress;
            if (address == null) return null;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            return address.Equals(IPAddress.IPv6Loopback) ? "127.0.0.1" : address.ToString();
        }

        private static string? Cut(string? value, int length) =>
            value == null || value.Length <= length ? value : value[..length];
    }
}
