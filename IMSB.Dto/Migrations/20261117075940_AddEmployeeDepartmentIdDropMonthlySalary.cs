using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>
    /// The Staff page's designation and staff category get their own columns:
    /// tbl_Employee.Desig goes back to meaning SERP.tbl_Designation.DesigID (the designation, as the SERP procedures read it),
    /// and the staff category (dbo.departments.Id) moves to a new tbl_Employee.DepartmentID.
    /// The monthly salary now belongs to the designation (tbl_Designation.BaseSalary), so tbl_Employee.MonthlySalary is dropped.
    /// SERP.tbl_Employee is excluded from EF migrations, so this is hand-written SQL, guarded so it also runs where it already applies.
    /// </summary>
    public partial class AddEmployeeDepartmentIdDropMonthlySalary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SERP].[tbl_Employee]', N'DepartmentID') IS NULL
    ALTER TABLE [SERP].[tbl_Employee] ADD [DepartmentID] int NULL;
IF COL_LENGTH(N'[SERP].[tbl_Employee]', N'MonthlySalary') IS NOT NULL
    ALTER TABLE [SERP].[tbl_Employee] DROP COLUMN [MonthlySalary];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SERP].[tbl_Employee]', N'MonthlySalary') IS NULL
    ALTER TABLE [SERP].[tbl_Employee] ADD [MonthlySalary] decimal(18,2) NULL;
IF COL_LENGTH(N'[SERP].[tbl_Employee]', N'DepartmentID') IS NOT NULL
    ALTER TABLE [SERP].[tbl_Employee] DROP COLUMN [DepartmentID];");
        }
    }
}
