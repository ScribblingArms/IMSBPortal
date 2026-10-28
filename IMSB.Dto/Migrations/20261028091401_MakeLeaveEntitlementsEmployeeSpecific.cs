using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class MakeLeaveEntitlementsEmployeeSpecific : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_leave_entitlements_staff_category_leave_type_year",
                table: "leave_entitlements");

            migrationBuilder.DropIndex(
                name: "IX_leave_entitlements_staff_category_year",
                table: "leave_entitlements");

            migrationBuilder.AddColumn<Guid>(
                name: "staff_id",
                table: "leave_entitlements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH staff_assignments AS
                (
                    SELECT entitlement.id AS entitlement_id,
                           employee.id AS staff_id,
                           employee.staff_category,
                           ROW_NUMBER() OVER (PARTITION BY entitlement.id ORDER BY employee.id) AS row_number
                    FROM leave_entitlements AS entitlement
                    INNER JOIN staff AS employee
                        ON UPPER(LTRIM(RTRIM(employee.staff_category))) =
                           UPPER(LTRIM(RTRIM(entitlement.staff_category)))
                )
                UPDATE entitlement
                SET staff_id = assignment.staff_id,
                    staff_category = assignment.staff_category
                FROM leave_entitlements AS entitlement
                INNER JOIN staff_assignments AS assignment
                    ON assignment.entitlement_id = entitlement.id
                WHERE assignment.row_number = 1;

                INSERT INTO leave_entitlements
                    (id, staff_id, staff_category, leave_type, year, annual_quota, attendance_leave_type)
                SELECT NEWID(), employee.id, employee.staff_category, entitlement.leave_type,
                       entitlement.year, entitlement.annual_quota, entitlement.attendance_leave_type
                FROM leave_entitlements AS entitlement
                INNER JOIN staff AS employee
                    ON UPPER(LTRIM(RTRIM(employee.staff_category))) =
                       UPPER(LTRIM(RTRIM(entitlement.staff_category)))
                WHERE entitlement.staff_id IS NOT NULL
                  AND entitlement.staff_id <> employee.id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_leave_entitlements_staff_id_leave_type_year",
                table: "leave_entitlements",
                columns: new[] { "staff_id", "leave_type", "year" },
                unique: true,
                filter: "[staff_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_leave_entitlements_staff_id_year",
                table: "leave_entitlements",
                columns: new[] { "staff_id", "year" },
                unique: true,
                filter: "[attendance_leave_type] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_leave_entitlements_staff_staff_id",
                table: "leave_entitlements",
                column: "staff_id",
                principalTable: "staff",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH ranked_entitlements AS
                (
                    SELECT id,
                           ROW_NUMBER() OVER
                               (PARTITION BY staff_category, leave_type, year ORDER BY staff_id, id) AS row_number
                    FROM leave_entitlements
                )
                DELETE entitlement
                FROM leave_entitlements AS entitlement
                INNER JOIN ranked_entitlements AS ranked ON ranked.id = entitlement.id
                WHERE ranked.row_number > 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_leave_entitlements_staff_staff_id",
                table: "leave_entitlements");

            migrationBuilder.DropIndex(
                name: "IX_leave_entitlements_staff_id_leave_type_year",
                table: "leave_entitlements");

            migrationBuilder.DropIndex(
                name: "IX_leave_entitlements_staff_id_year",
                table: "leave_entitlements");

            migrationBuilder.DropColumn(
                name: "staff_id",
                table: "leave_entitlements");

            migrationBuilder.CreateIndex(
                name: "IX_leave_entitlements_staff_category_leave_type_year",
                table: "leave_entitlements",
                columns: new[] { "staff_category", "leave_type", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leave_entitlements_staff_category_year",
                table: "leave_entitlements",
                columns: new[] { "staff_category", "year" },
                unique: true,
                filter: "[attendance_leave_type] = 1");
        }
    }
}
