using IMSB.Dto.Migrations.Scripts;
using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>
    /// The SERP.UpdateAttendanceManagement trigger sets dbo.attendance_records.CreatedAt to the punch's
    /// SERP.tbl_EMPDailyAttendance.CreateOn (the day's first CreateOn) when it inserts or updates a day, instead of the time
    /// the trigger ran; a change to CreateOn alone also recalculates the day. Then the records already there are brought in
    /// line: touching the stored punches runs the trigger. No model change, so the snapshot is unchanged.
    /// </summary>
    [DbContext(typeof(IMSBContext))]
    [Migration("20261117180000_AttendanceRecordsCreatedAtFromPunch")]
    public partial class AttendanceRecordsCreatedAtFromPunch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            MigrationScript.Run(migrationBuilder, "Triggers/UpdateAttendanceManagement.sql");

            // UPDATE(CreateOn) is true for a self-assignment, so the trigger re-saves every stored employee-day
            migrationBuilder.Sql(@"
UPDATE a SET a.[CreateOn] = a.[CreateOn]
FROM [SERP].[tbl_EMPDailyAttendance] a
WHERE EXISTS (SELECT 1 FROM [SERP].[tbl_Employee] e WHERE e.[EMP_ID] = a.[EMP_ID]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // the previous trigger; CreatedAt values already copied from CreateOn stay
            MigrationScript.Run(migrationBuilder, "Triggers/UpdateAttendanceManagement.v3.sql");
        }
    }
}
