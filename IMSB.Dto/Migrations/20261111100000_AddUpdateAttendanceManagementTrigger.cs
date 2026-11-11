using IMSB.Dto.Migrations.Scripts;
using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>
    /// Trigger SERP.UpdateAttendanceManagement: a punch saved in SERP.tbl_EMPDailyAttendance (insert or update)
    /// inserts or updates the staff member's attendance_records row for that day, with PRESENT / HALF_DAY / ABSENT
    /// worked out from the entry and exit times (rules in Scripts/Triggers/UpdateAttendanceManagement.sql).
    /// Also adds IX_tbl_EMPDailyAttendance_EMP_ID_DOA so a punch reads only that employee's rows for the day.
    /// No model change, so the snapshot is unchanged.
    /// </summary>
    [DbContext(typeof(IMSBContext))]
    [Migration("20261111100000_AddUpdateAttendanceManagementTrigger")]
    public partial class AddUpdateAttendanceManagementTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // the version written for Guid ids; UseIntIdentityKeys re-creates it from UpdateAttendanceManagement.sql
            MigrationScript.Run(migrationBuilder, "Triggers/UpdateAttendanceManagement.v1.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [SERP].[UpdateAttendanceManagement];");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_tbl_EMPDailyAttendance_EMP_ID_DOA] ON [SERP].[tbl_EMPDailyAttendance];");
        }
    }
}
