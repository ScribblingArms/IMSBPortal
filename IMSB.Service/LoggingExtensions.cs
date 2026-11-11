using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IMSB.LoggerLibrary
{
    public static class LoggingExtensions
    {
        /// <summary>
        /// Adds database logging to FW.TblLog next to the default console/debug loggers. Every ILogger call at or above
        /// DatabaseLogging:MinimumLevel (default Warning) is stored, including unhandled exceptions logged by ASP.NET Core.
        /// Off in the "Testing" environment unless DatabaseLogging:Enabled says otherwise, and when no connection string is set.
        /// </summary>
        /// <param name="appType">The AppType column, e.g. "IMSB.Admin" or "IMSB.Api".</param>
        public static WebApplicationBuilder AddImsbLogging(this WebApplicationBuilder builder, string appType)
        {
            var options = builder.Configuration.GetSection(DatabaseLoggerOptions.Section).Get<DatabaseLoggerOptions>() ?? new DatabaseLoggerOptions();
            if (!(options.Enabled ?? !builder.Environment.IsEnvironment("Testing"))) return builder;

            var connectionString = builder.Configuration.GetConnectionString(options.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString)) return builder;

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton(new DatabaseLoggerSettings(
                connectionString,
                string.IsNullOrWhiteSpace(options.AppType) ? appType : options.AppType,
                options.MinimumLevel,
                options.QueueSize,
                Math.Clamp(options.BatchSize, 1, 5000),
                TimeSpan.FromMilliseconds(Math.Clamp(options.FlushIntervalMilliseconds, 100, 60_000))));
            builder.Services.AddSingleton<LogQueue>();
            builder.Services.AddSingleton<ILoggerProvider, DatabaseLoggerProvider>();
            builder.Services.AddHostedService<DatabaseLogWriter>();
            return builder;
        }
    }
}
