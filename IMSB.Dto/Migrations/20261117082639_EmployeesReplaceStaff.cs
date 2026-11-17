using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>
    /// IMSB.Admin reads its employees from SERP.tbl_Employee instead of dbo.staff:
    /// <list type="bullet">
    /// <item>dbo.vw_StaffEmployees presents tbl_Employee in the shape the attendance, leave, OT, payroll, dashboard and search
    /// pages read (the Staff model). Id is the EMP_ID; designation and salary come from the employee's tbl_Designation row,
    /// the Staff Category from dbo.departments, the shift code from dbo.Shift.</item>
    /// <item>attendance_records, ot_assignments, leave_entitlements and staff_leave_entries now point at tbl_Employee.EMP_ID
    /// (deleting an employee deletes their rows). Their rows for dbo.staff people are deleted, as are the pay bills:
    /// the history starts afresh.</item>
    /// <item>tbl_Designation gets an EmpId column: every employee has their own designation row.</item>
    /// <item>employee_leave_quotas is dropped: the Staff page's quotas are leave_entitlements, as the Leaves page keeps them.</item>
    /// </list>
    /// dbo.staff itself is kept, unread, as a record of the old staff list.
    /// </summary>
    public partial class EmployeesReplaceStaff : Migration
    {
        private static readonly string[] LinkedTables = { "attendance_records", "ot_assignments", "leave_entitlements", "staff_leave_entries" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_attendance_records_staff_StaffId", table: "attendance_records");
            migrationBuilder.DropForeignKey(name: "FK_leave_entitlements_staff_StaffId", table: "leave_entitlements");
            migrationBuilder.DropForeignKey(name: "FK_ot_assignments_staff_StaffId", table: "ot_assignments");
            migrationBuilder.DropForeignKey(name: "FK_staff_leave_entries_staff_StaffId", table: "staff_leave_entries");

            migrationBuilder.DropTable(name: "employee_leave_quotas");

            // start afresh: these rows belong to dbo.staff ids, which mean nothing in tbl_Employee
            migrationBuilder.Sql(@"
DELETE FROM [dbo].[attendance_records];
DELETE FROM [dbo].[ot_assignments];
DELETE FROM [dbo].[staff_leave_entries];
DELETE FROM [dbo].[leave_entitlements] WHERE [StaffId] IS NOT NULL;
DELETE FROM [dbo].[pay_bill_entries];");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SERP].[tbl_Designation]', N'EmpId') IS NULL
    ALTER TABLE [SERP].[tbl_Designation] ADD [EmpId] int NULL;");

            // the designation: the employee's own row first; tbl_Designation has no key, so TOP (1) keeps a duplicated
            // DesigID from repeating the employee
            migrationBuilder.Sql(@"
CREATE OR ALTER VIEW [dbo].[vw_StaffEmployees] AS
SELECT
    e.[EMP_ID] AS [Id],
    CAST(e.[EMP_ID] AS nvarchar(20)) AS [EmployeeId],
    CAST(ISNULL(e.[EMP_Name], N'') AS nvarchar(max)) AS [FullName],
    CAST(ISNULL(e.[Mobile], N'') AS nvarchar(32)) AS [Mobile],
    CAST(NULL AS nvarchar(200)) AS [Email],
    CAST(NULL AS nvarchar(64)) AS [AadhaarHash],
    CAST(CASE WHEN LEN(e.[Aadhar]) >= 4 THEN RIGHT(e.[Aadhar], 4) END AS nvarchar(4)) AS [AadhaarLast4],
    CAST('19000101' AS datetime2) AS [DateOfBirth],
    CAST(ISNULL(e.[Gender], N'') AS nvarchar(max)) AS [Gender],
    ISNULL(e.[DepartmentID], 0) AS [DepartmentId],
    CAST(NULL AS nvarchar(20)) AS [EmployeeType],
    CAST(ISNULL(d.[Designation], N'') AS nvarchar(max)) AS [Designation],
    CAST(ISNULL(dep.[Name], N'') AS nvarchar(100)) AS [StaffCategory],
    CAST(ISNULL(d.[BaseSalary], 0) AS decimal(18,2)) AS [MonthlySalary],
    CAST(COALESCE(e.[JoinDate], CAST(e.[CreateOn] AS date), CAST('19000101' AS date)) AS datetime2) AS [JoinDate],
    CAST(ISNULL(sh.[Code], N'') AS nvarchar(max)) AS [Shift],
    CAST(CASE WHEN e.[IsActive] = 1 THEN N'ACTIVE' ELSE N'INACTIVE' END AS nvarchar(max)) AS [Status],
    CAST(ISNULL(e.[CreateOn], '19000101') AS datetime2) AS [CreatedAt],
    CAST(e.[ModifiedOn] AS datetime2) AS [UpdatedAt],
    CAST(NULL AS nvarchar(max)) AS [CreatedBy],
    CAST(NULL AS nvarchar(max)) AS [UpdatedBy],
    CAST(NULL AS datetime2) AS [ContractEndDate]
FROM [SERP].[tbl_Employee] e
OUTER APPLY (
    SELECT TOP (1) x.[Designation], x.[BaseSalary]
    FROM [SERP].[tbl_Designation] x
    WHERE x.[DesigID] = e.[Desig]
    ORDER BY CASE WHEN x.[EmpId] = e.[EMP_ID] THEN 0 ELSE 1 END
) d
LEFT JOIN [dbo].[departments] dep ON dep.[Id] = e.[DepartmentID]
LEFT JOIN [dbo].[Shift] sh ON sh.[Id] = e.[ShiftID];");

            foreach (var table in LinkedTables)
                migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_{table}_tbl_Employee_StaffId')
    ALTER TABLE [dbo].[{table}] ADD CONSTRAINT [FK_{table}_tbl_Employee_StaffId]
        FOREIGN KEY ([StaffId]) REFERENCES [SERP].[tbl_Employee] ([EMP_ID]) ON DELETE CASCADE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in LinkedTables)
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_{table}_tbl_Employee_StaffId')
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [FK_{table}_tbl_Employee_StaffId];");

            // rows now hold EMP_IDs, which mean nothing in dbo.staff
            migrationBuilder.Sql(@"
DELETE FROM [dbo].[attendance_records];
DELETE FROM [dbo].[ot_assignments];
DELETE FROM [dbo].[staff_leave_entries];
DELETE FROM [dbo].[leave_entitlements] WHERE [StaffId] IS NOT NULL;
DELETE FROM [dbo].[pay_bill_entries];");

            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[vw_StaffEmployees];");
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SERP].[tbl_Designation]', N'EmpId') IS NOT NULL
    ALTER TABLE [SERP].[tbl_Designation] DROP COLUMN [EmpId];");

            migrationBuilder.CreateTable(
                name: "employee_leave_quotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpId = table.Column<int>(type: "int", nullable: false),
                    AnnualQuota = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaveType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Year = table.Column<int>(type: "int", nullable: false)
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

            // dbo.staff was kept by Up, so the old links can be restored
            migrationBuilder.AddForeignKey(name: "FK_attendance_records_staff_StaffId", table: "attendance_records", column: "StaffId",
                principalTable: "staff", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(name: "FK_leave_entitlements_staff_StaffId", table: "leave_entitlements", column: "StaffId",
                principalTable: "staff", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(name: "FK_ot_assignments_staff_StaffId", table: "ot_assignments", column: "StaffId",
                principalTable: "staff", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(name: "FK_staff_leave_entries_staff_StaffId", table: "staff_leave_entries", column: "StaffId",
                principalTable: "staff", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        }
    }
}
