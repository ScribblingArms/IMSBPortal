using IMSB.Dto.Migrations.Scripts;
using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>
    /// Removes the gmc schema that AddGmcDbObjects brought in: its 61 procedures, 2 views, 43 tables (with their data)
    /// and the BeneficiaryAttendanceTVP type, then the schema itself (Scripts/RemoveGmc/Up.sql).
    /// The SERP tables and procedures stay, and so do FIN and FW (FW.TblLog is the database log); none of them refer to gmc.
    /// Not mapped in IMSBContext, so the model snapshot is unchanged.
    /// </summary>
    [DbContext(typeof(IMSBContext))]
    [Migration("20261111120000_RemoveGmcSchemaObjects")]
    public partial class RemoveGmcSchemaObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            MigrationScript.Run(migrationBuilder, "RemoveGmc/Up.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // re-creates the gmc objects (empty) with AddGmcDbObjects' scripts; every batch skips what already exists
            foreach (var script in new[]
            {
                "GmcDb/Schemas.sql", "GmcDb/Types.sql", "GmcDb/Tables.sql", "GmcDb/Defaults.sql",
                "GmcDb/ForeignKeys.sql", "GmcDb/Indexes.sql", "GmcDb/Views.sql", "GmcDb/Procedures.sql",
            })
                MigrationScript.Run(migrationBuilder, script);
        }
    }
}
