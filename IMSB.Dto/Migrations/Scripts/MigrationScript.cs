using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations;

namespace IMSB.Dto.Migrations.Scripts;

/// <summary>
/// Runs a .sql file embedded from Migrations/Scripts (see IMSB.Dto.csproj) inside a migration.
/// The file is split on GO lines so each batch (CREATE PROCEDURE, CREATE VIEW, ...) is sent on its own.
/// </summary>
internal static class MigrationScript
{
    private static readonly Regex GoSeparator = new(@"^[ \t]*GO[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

    /// <param name="relativePath">path under Migrations/Scripts, e.g. "GmcDb/Tables.sql"</param>
    public static void Run(MigrationBuilder migrationBuilder, string relativePath)
    {
        foreach (var batch in GoSeparator.Split(Read(relativePath)))
        {
            if (!string.IsNullOrWhiteSpace(StripComments(batch)))
                migrationBuilder.Sql(batch.Trim());
        }
    }

    private static string Read(string relativePath)
    {
        var assembly = typeof(MigrationScript).Assembly;
        var resourceName = "IMSB.Dto.Migrations.Scripts." + relativePath.Replace('/', '.').Replace('\\', '.');
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded migration script '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // a batch holding only "--" comments (e.g. a file header) has nothing to run
    private static string StripComments(string batch) => Regex.Replace(batch, @"^\s*--.*$", "", RegexOptions.Multiline);
}
