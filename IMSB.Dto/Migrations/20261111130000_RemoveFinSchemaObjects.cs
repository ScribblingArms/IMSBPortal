using IMSB.Dto.Migrations.Scripts;
using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>
    /// Removes the FIN schema that AddGmcDbObjects brought in: tblAmountGiven, tblAmountRecieved and tblFinCustomers
    /// (with their data), the 2 procedures that use them, then the schema itself (Scripts/RemoveFin/Up.sql).
    /// Not mapped in IMSBContext, so the model snapshot is unchanged.
    /// </summary>
    [DbContext(typeof(IMSBContext))]
    [Migration("20261111130000_RemoveFinSchemaObjects")]
    public partial class RemoveFinSchemaObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            MigrationScript.Run(migrationBuilder, "RemoveFin/Up.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // re-creates only the FIN objects (tables empty), copied from AddGmcDbObjects' scripts
            MigrationScript.Run(migrationBuilder, "RemoveFin/Down.sql");
        }
    }
}
