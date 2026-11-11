using IMSB.Dto.Migrations.Scripts;
using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>
    /// Brings the gmc_db objects (GMC_sch.sql) into this database: the BeneficiaryAttendanceTVP table type,
    /// 57 tables with their defaults, keys and indexes, 2 views and 78 stored procedures.
    /// What gmc_db kept in dbo goes to the gmc schema, apart from IMSB's own dbo tables, and procedure and view bodies
    /// refer to it as [gmc].[...]; the FIN, FW and SERP schemas keep their names.
    /// They are not mapped in IMSBContext (accessed through stored procedures), so the model snapshot is unchanged.
    /// Every batch is guarded or CREATE OR ALTER, so it also runs against a database that already has some of them.
    /// Database settings, CREATE DATABASE and the database users of the original script are environment-specific and left out.
    /// </summary>
    [DbContext(typeof(IMSBContext))]
    [Migration("20261111090000_AddGmcDbObjects")]
    public partial class AddGmcDbObjects : Migration
    {
        // order matters: tables before their keys and views; procedures last
        private static readonly string[] UpScripts =
        {
            "GmcDb/Schemas.sql",
            "GmcDb/Types.sql",
            "GmcDb/Tables.sql",
            "GmcDb/Defaults.sql",
            "GmcDb/ForeignKeys.sql",
            "GmcDb/Indexes.sql",
            "GmcDb/Views.sql",
            "GmcDb/Procedures.sql",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var script in UpScripts)
                MigrationScript.Run(migrationBuilder, script);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // drops the procedures, views, tables (and their data), the table type, then FIN/FW/SERP if empty
            MigrationScript.Run(migrationBuilder, "GmcDb/Down.sql");
        }
    }
}
