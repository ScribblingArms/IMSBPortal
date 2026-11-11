using IMSB.LoggerLibrary;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IMSB.Admin.Tests;

/// <summary>IMSB.LoggerLibrary: what a log call queues for FW.TblLog (the database write itself is not run here).</summary>
public class DatabaseLoggerTests
{
    private static readonly DatabaseLoggerSettings Settings =
        new("Server=unused", "IMSB.Test", LogLevel.Warning, 100, 10, TimeSpan.FromSeconds(1));

    private static (DatabaseLoggerProvider Provider, LogQueue Queue) Create()
    {
        var queue = new LogQueue(Settings);
        return (new DatabaseLoggerProvider(queue, Settings, new HttpContextAccessor()), queue);
    }

    private static List<LogEntry> Drain(LogQueue queue)
    {
        var entries = new List<LogEntry>();
        while (queue.Reader.TryRead(out var e)) entries.Add(e);
        return entries;
    }

    [Fact]
    public void Entries_below_the_minimum_level_are_not_queued()
    {
        var (provider, queue) = Create();
        var logger = provider.CreateLogger("IMSB.Admin.Controllers.AccountController");
        logger.LogInformation("signed in");
        logger.LogWarning("locked out {User}", "admin");
        var entry = Assert.Single(Drain(queue));
        Assert.Equal("Warning", entry.Level);
        Assert.Equal("locked out admin", entry.Message);
        Assert.Equal("IMSB.Test", entry.AppType);
        Assert.Contains("\"category\":\"IMSB.Admin.Controllers.AccountController\"", entry.Context);
    }

    [Fact]
    public void Values_are_cut_to_the_column_sizes_and_the_exception_kept_in_context()
    {
        var (provider, queue) = Create();
        provider.CreateLogger("Test").LogCritical(new InvalidOperationException("boom"), new string('x', 1500));
        var entry = Assert.Single(Drain(queue));
        Assert.Equal("Critical", entry.Level);               // fits varchar(10); "Information" is stored as "Info"
        Assert.Equal(1000, entry.Message.Length);            // varchar(1000)
        Assert.Contains("System.InvalidOperationException: boom", entry.Context);
    }

    [Fact]
    public void The_library_does_not_log_about_itself()
    {
        var (provider, queue) = Create();
        provider.CreateLogger("IMSB.LoggerLibrary.DatabaseLogWriter").LogError("could not write");
        provider.CreateLogger("Microsoft.Data.SqlClient.SqlConnection").LogError("timeout");
        Assert.Empty(Drain(queue));
    }

    [Fact]
    public void Database_logging_is_off_in_the_Testing_environment()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration["ConnectionStrings:DefaultConnection"] = "Server=unused";
        builder.AddImsbLogging("IMSB.Test");
        Assert.DoesNotContain(builder.Services, s => s.ImplementationType == typeof(DatabaseLogWriter));
    }

    [Fact]
    public void Database_logging_is_registered_elsewhere()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.Configuration["ConnectionStrings:DefaultConnection"] = "Server=unused";
        builder.AddImsbLogging("IMSB.Test");
        Assert.Contains(builder.Services, s => s.ServiceType == typeof(ILoggerProvider) && s.ImplementationType == typeof(DatabaseLoggerProvider));
        Assert.Contains(builder.Services, s => s.ImplementationType == typeof(DatabaseLogWriter));
    }
}
