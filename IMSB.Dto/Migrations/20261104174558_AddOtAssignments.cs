using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class AddOtAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ot_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    ToTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ot_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ot_assignments_staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ot_assignments_StaffId",
                table: "ot_assignments",
                column: "StaffId",
                unique: true);

            // timings already set on staff records move to the new table before their columns go
            migrationBuilder.Sql(@"
INSERT INTO ot_assignments (Id, StaffId, FromTime, ToTime, CreatedAt)
SELECT NEWID(), Id, OtFromTime, OtToTime, GETUTCDATE()
FROM staff
WHERE OtFromTime IS NOT NULL AND OtToTime IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "OtFromTime",
                table: "staff");

            migrationBuilder.DropColumn(
                name: "OtToTime",
                table: "staff");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "OtFromTime",
                table: "staff",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "OtToTime",
                table: "staff",
                type: "time",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE staff
SET OtFromTime = assignment.FromTime, OtToTime = assignment.ToTime
FROM staff
INNER JOIN ot_assignments assignment ON assignment.StaffId = staff.Id;");

            migrationBuilder.DropTable(
                name: "ot_assignments");
        }
    }
}
