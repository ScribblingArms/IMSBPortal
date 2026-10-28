using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffLeaveManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "staff_category",
                table: "staff",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE [staff] SET [staff_category] = CASE " +
                "WHEN NULLIF(LTRIM(RTRIM([designation])), '') IS NULL THEN 'General' " +
                "ELSE LEFT(LTRIM(RTRIM([designation])), 100) END");

            migrationBuilder.CreateTable(
                name: "leave_entitlements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    staff_category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    leave_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    year = table.Column<int>(type: "int", nullable: false),
                    annual_quota = table.Column<decimal>(type: "decimal(8,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_entitlements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staff_leave_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    staff_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    leave_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    year = table.Column<int>(type: "int", nullable: false),
                    from_date = table.Column<DateTime>(type: "date", nullable: false),
                    to_date = table.Column<DateTime>(type: "date", nullable: false),
                    days = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_leave_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_staff_leave_entries_staff_staff_id",
                        column: x => x.staff_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_leave_entitlements_staff_category_leave_type_year",
                table: "leave_entitlements",
                columns: new[] { "staff_category", "leave_type", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_leave_entries_staff_id_from_date",
                table: "staff_leave_entries",
                columns: new[] { "staff_id", "from_date" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_leave_entries_year",
                table: "staff_leave_entries",
                column: "year");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leave_entitlements");

            migrationBuilder.DropTable(
                name: "staff_leave_entries");

            migrationBuilder.DropColumn(
                name: "staff_category",
                table: "staff");
        }
    }
}
