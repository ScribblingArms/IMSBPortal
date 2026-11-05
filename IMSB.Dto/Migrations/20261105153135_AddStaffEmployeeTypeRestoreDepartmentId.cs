using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>
    /// Undoes the rename of staff.DepartmentId (the staff category), keeping every value, and adds Employee Type as its
    /// own optional column. Written by hand: the scaffolded version dropped the category values.
    /// </summary>
    public partial class AddStaffEmployeeTypeRestoreDepartmentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EmployeeType",
                table: "staff",
                newName: "DepartmentId");

            migrationBuilder.RenameIndex(
                name: "IX_staff_EmployeeType",
                table: "staff",
                newName: "IX_staff_DepartmentId");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeType",
                table: "staff",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeType",
                table: "staff");

            migrationBuilder.RenameColumn(
                name: "DepartmentId",
                table: "staff",
                newName: "EmployeeType");

            migrationBuilder.RenameIndex(
                name: "IX_staff_DepartmentId",
                table: "staff",
                newName: "IX_staff_EmployeeType");
        }
    }
}
