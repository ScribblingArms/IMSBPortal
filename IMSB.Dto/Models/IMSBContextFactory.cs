using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace IMSB.Dto.Models;

/// <summary>
/// Used by the EF tools only, so migrations can be added and applied from this project
/// (e.g. dotnet ef migrations add Name --project IMSB.Dto). Reads appsettings.json next to it.
/// </summary>
public class IMSBContextFactory : IDesignTimeDbContextFactory<IMSBContext>
{
    public IMSBContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "IMSB.Dto.appsettings.json"), optional: true)
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured in IMSB.Dto/appsettings.json.");

        return new IMSBContext(new DbContextOptionsBuilder<IMSBContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(IMSBContext).Assembly.FullName))
            .Options);
    }
}
