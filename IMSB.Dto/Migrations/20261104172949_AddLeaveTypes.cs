using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "leave_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_types", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "leave_types",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { new Guid("b1000000-0000-0000-0000-000000000001"), "Casual" },
                    { new Guid("b1000000-0000-0000-0000-000000000002"), "Earned" },
                    { new Guid("b1000000-0000-0000-0000-000000000003"), "Sick" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_leave_types_Name",
                table: "leave_types",
                column: "Name",
                unique: true);

            // leave types already in use stay selectable
            migrationBuilder.Sql(@"
INSERT INTO leave_types (Id, Name)
SELECT NEWID(), used.Name
FROM (
    SELECT LTRIM(RTRIM(LeaveType)) AS Name FROM leave_entitlements
    UNION
    SELECT LTRIM(RTRIM(LeaveType)) FROM staff_leave_entries
) used
WHERE used.Name <> ''
  AND NOT EXISTS (SELECT 1 FROM leave_types existing WHERE existing.Name = used.Name);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leave_types");
        }
    }
}
