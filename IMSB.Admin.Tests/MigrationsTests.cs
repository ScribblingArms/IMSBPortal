using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace IMSB.Admin.Tests;

/// <summary>
/// The migrations must create every column the model maps. A property added to a model without a migration
/// (e.g. SHGDetails.RejectedAt) builds and passes the in-memory tests, then fails on SQL Server with "Invalid column name".
/// None of these tests connect to a database.
/// </summary>
public class MigrationsTests
{
    private static IMSBContext SqlServerContext() =>
        new(new DbContextOptionsBuilder<IMSBContext>()
            .UseSqlServer("Server=(none);Database=imsb-model-check;Trusted_Connection=True")
            .Options);

    [Fact]
    public void Every_model_change_has_a_migration()
    {
        using var context = SqlServerContext();
        Assert.False(context.Database.HasPendingModelChanges(),
            "The model differs from the last migration snapshot. Run: dotnet ef migrations add <Name> --project IMSB.Dto --startup-project IMSB.Admin");
    }

    [Fact]
    public void The_shg_rejection_columns_are_created_by_a_migration()
    {
        using var context = SqlServerContext();
        var migrations = context.GetService<IMigrationsAssembly>().Migrations;
        Assert.Contains(migrations.Keys, id => id.EndsWith("_AddShgRejection"));

        var entity = context.Model.FindEntityType(typeof(ShgDetail))!;
        Assert.Equal("SHGDetails", entity.GetTableName());
        Assert.Equal(500, entity.FindProperty(nameof(ShgDetail.RejectionReason))!.GetMaxLength());
        Assert.Equal(100, entity.FindProperty(nameof(ShgDetail.RejectedBy))!.GetMaxLength());
        Assert.True(entity.FindProperty(nameof(ShgDetail.RejectedAt))!.IsNullable);
    }
}
