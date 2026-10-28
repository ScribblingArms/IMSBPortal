using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceLeavePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "attendance_leave_type",
                table: "leave_entitlements",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "leave_without_pay",
                table: "attendance_records",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_leave_entitlements_staff_category_year",
                table: "leave_entitlements",
                columns: new[] { "staff_category", "year" },
                unique: true,
                filter: "[attendance_leave_type] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_leave_entitlements_staff_category_year",
                table: "leave_entitlements");

            migrationBuilder.DropColumn(
                name: "attendance_leave_type",
                table: "leave_entitlements");

            migrationBuilder.DropColumn(
                name: "leave_without_pay",
                table: "attendance_records");
        }
    }
}
