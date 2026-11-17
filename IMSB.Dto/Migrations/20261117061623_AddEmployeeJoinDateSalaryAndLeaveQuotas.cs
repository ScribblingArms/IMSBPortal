using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>
    /// The Staff page's join date, monthly salary and annual leave quotas.
    /// SERP.tbl_Employee is excluded from EF migrations (it belongs to the SERP scripts), so its two new columns are added
    /// here by hand, guarded so the migration also runs where they already exist. The quotas get IMSB's own table,
    /// employee_leave_quotas, linked to tbl_Employee.EMP_ID (deleting an employee deletes their quotas).
    /// </summary>
    public partial class AddEmployeeJoinDateSalaryAndLeaveQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SERP].[tbl_Employee]', N'JoinDate') IS NULL
    ALTER TABLE [SERP].[tbl_Employee] ADD [JoinDate] date NULL;
IF COL_LENGTH(N'[SERP].[tbl_Employee]', N'MonthlySalary') IS NULL
    ALTER TABLE [SERP].[tbl_Employee] ADD [MonthlySalary] decimal(18,2) NULL;");

            migrationBuilder.CreateTable(
                name: "employee_leave_quotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpId = table.Column<int>(type: "int", nullable: false),
                    LeaveType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    AnnualQuota = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_leave_quotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_employee_leave_quotas_tbl_Employee_EmpId",
                        column: x => x.EmpId,
                        principalSchema: "SERP",
                        principalTable: "tbl_Employee",
                        principalColumn: "EMP_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_employee_leave_quotas_EmpId_LeaveType_Year",
                table: "employee_leave_quotas",
                columns: new[] { "EmpId", "LeaveType", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_leave_quotas");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SERP].[tbl_Employee]', N'MonthlySalary') IS NOT NULL
    ALTER TABLE [SERP].[tbl_Employee] DROP COLUMN [MonthlySalary];
IF COL_LENGTH(N'[SERP].[tbl_Employee]', N'JoinDate') IS NOT NULL
    ALTER TABLE [SERP].[tbl_Employee] DROP COLUMN [JoinDate];");
        }
    }
}
