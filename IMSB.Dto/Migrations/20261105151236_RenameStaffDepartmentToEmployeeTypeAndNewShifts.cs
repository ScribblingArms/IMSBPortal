using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class RenameStaffDepartmentToEmployeeTypeAndNewShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DepartmentId",
                table: "staff",
                newName: "EmployeeType");

            migrationBuilder.RenameIndex(
                name: "IX_staff_DepartmentId",
                table: "staff",
                newName: "IX_staff_EmployeeType");

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Shift",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Existing shifts stay with DisplayOrder 0: staff on them keep them and attendance still reads their
            // length, but only these five are offered from now on. The code is the label, as staff rows show it as is.
            migrationBuilder.Sql(@"
                DECLARE @new TABLE (DisplayOrder int, Name nvarchar(50));
                INSERT INTO @new VALUES
                    (1, N'06:00 AM to 02:00 PM'), (2, N'02:00 PM to 10:00 PM'), (3, N'10:00 PM to 06:00 AM'),
                    (4, N'08:00 AM to 04:00 PM'), (5, N'01:00 PM to 09:00 PM');
                UPDATE s SET s.DisplayOrder = n.DisplayOrder, s.Name = n.Name FROM [Shift] s JOIN @new n ON s.Code = n.Name;
                INSERT INTO [Shift] (Id, Code, Name, DisplayOrder)
                    SELECT NEWID(), n.Name, n.Name, n.DisplayOrder FROM @new n
                    WHERE NOT EXISTS (SELECT 1 FROM [Shift] s WHERE s.Code = n.Name);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // staff already on these keep the text, which the staff form tolerates as an older value
            migrationBuilder.Sql(@"
                DELETE FROM [Shift] WHERE Code IN (N'06:00 AM to 02:00 PM', N'02:00 PM to 10:00 PM', N'10:00 PM to 06:00 AM',
                    N'08:00 AM to 04:00 PM', N'01:00 PM to 09:00 PM');");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Shift");

            migrationBuilder.RenameColumn(
                name: "EmployeeType",
                table: "staff",
                newName: "DepartmentId");

            migrationBuilder.RenameIndex(
                name: "IX_staff_EmployeeType",
                table: "staff",
                newName: "IX_staff_DepartmentId");
        }
    }
}
