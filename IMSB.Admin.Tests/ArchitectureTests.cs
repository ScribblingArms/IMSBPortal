using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Admin.Tests;

/// <summary>
/// Controllers and views reach the database only through the IMSB.Interfaces backends: no IMSBContext,
/// DbContext or EF Core query code in IMSB.Admin / IMSB.Api controllers or in Razor views.
/// </summary>
public class ArchitectureTests
{
    private static string AdminDir() => Path.GetFullPath(typeof(ArchitectureTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "AdminProjectDir").Value!);

    private static readonly Regex DataAccess = new(
        @"\bIMSBContext\b|\bDbContext\b|Microsoft\.EntityFrameworkCore|\.SaveChanges(Async)?\(|\.ToListAsync\(|\.AsNoTracking\(",
        RegexOptions.Compiled);

    [Fact]
    public void Admin_controllers_do_not_take_a_DbContext()
    {
        var offenders = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetConstructors().SelectMany(c => c.GetParameters())
                .Any(p => typeof(DbContext).IsAssignableFrom(p.ParameterType)))
            .Select(t => t.Name)
            .ToList();
        Assert.True(offenders.Count == 0, $"inject an IMSB.Interfaces backend instead of the context: {string.Join(", ", offenders)}");
    }

    [Theory]
    [InlineData("IMSB.Admin", "Controllers", "*.cs")]
    [InlineData("IMSB.Api", "Controllers", "*.cs")]
    [InlineData("IMSB.Admin", "Views", "*.cshtml")]
    public void No_data_access_code_outside_the_backend(string project, string folder, string pattern)
    {
        var dir = Path.Combine(AdminDir(), "..", project, folder);
        var files = Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(files);
        var offenders = files
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, line, i)))
            .Where(x => DataAccess.IsMatch(x.line))
            .Select(x => $"{Path.GetRelativePath(dir, x.f)}:{x.i + 1}")
            .ToList();
        Assert.True(offenders.Count == 0, $"database access in {project}/{folder}: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Every_backend_interface_is_registered()
    {
        var interfaces = typeof(IMSB.Interfaces.IApplicationsInterface).Assembly.GetTypes()
            .Where(t => t.IsInterface && t.Namespace == "IMSB.Interfaces").ToList();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        IMSB.Backend.ServiceCollectionExtensions.AddImsbBackend(services);
        var missing = interfaces.Where(i => services.All(s => s.ServiceType != i)).Select(i => i.Name).ToList();
        Assert.True(missing.Count == 0, $"not registered in AddImsbBackend: {string.Join(", ", missing)}");
        Assert.All(services.Where(s => interfaces.Contains(s.ServiceType)),
            s => Assert.Equal(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped, s.Lifetime));
    }
}
