using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>Fills the Staff Category text columns from each staff member's department, in place of the old free text.</summary>
    public partial class SyncStaffCategoryFromDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE s SET s.StaffCategory = LEFT(d.Name, 100)
                FROM [staff] s JOIN [departments] d ON d.Id = s.DepartmentId
                WHERE LTRIM(RTRIM(d.Name)) <> N'';

                UPDATE e SET e.StaffCategory = s.StaffCategory
                FROM [leave_entitlements] e JOIN [staff] s ON s.Id = e.StaffId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // the old free text is overwritten by Up and cannot be restored
        }
    }
}
