using IMSB.Dto.Migrations.Scripts;
using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>
    /// The SERP.UpdateAttendanceManagement trigger writes the punching employee's SERP.tbl_Employee.EMP_ID as
    /// dbo.attendance_records.StaffId, as EmployeesReplaceStaff made StaffId a key of tbl_Employee
    /// (FK_attendance_records_tbl_Employee_StaffId). It used to look up a dbo.staff member by Aadhaar/mobile and write
    /// that staff Id, which skipped every employee without a staff row and broke the FK for those with one.
    /// The shift length now comes from the employee's ShiftID in dbo.Shift (as dbo.vw_StaffEmployees reads it).
    /// Then attendance_records is filled from the punches already in tbl_EMPDailyAttendance: touching them runs the
    /// trigger, which works out each employee's days. No model change, so the snapshot is unchanged.
    /// </summary>
    [DbContext(typeof(IMSBContext))]
    [Migration("20261117170000_AttendanceRecordsUseEmpId")]
    public partial class AttendanceRecordsUseEmpId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // the version of that time; AttendanceRecordsCreatedAtFromPunch replaces it
            MigrationScript.Run(migrationBuilder, "Triggers/UpdateAttendanceManagement.v3.sql");

            // UPDATE(EMP_ID) is true for a self-assignment, so the trigger recalculates every stored employee-day
            migrationBuilder.Sql(@"
UPDATE a SET a.[EMP_ID] = a.[EMP_ID]
FROM [SERP].[tbl_EMPDailyAttendance] a
WHERE EXISTS (SELECT 1 FROM [SERP].[tbl_Employee] e WHERE e.[EMP_ID] = a.[EMP_ID]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // the previous trigger; the attendance_records rows filled by Up stay (they are valid EMP_IDs)
            MigrationScript.Run(migrationBuilder, "Triggers/UpdateAttendanceManagement.v2.sql");
        }
    }
}
