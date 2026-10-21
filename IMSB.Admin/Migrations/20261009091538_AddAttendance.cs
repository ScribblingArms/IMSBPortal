using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attendance_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    staff_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    attendance_date = table.Column<DateTime>(type: "date", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    in_time = table.Column<TimeSpan>(type: "time", nullable: true),
                    out_time = table.Column<TimeSpan>(type: "time", nullable: true),
                    worked_hours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    overtime_hours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_attendance_records_staff_staff_id",
                        column: x => x.staff_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_attendance_date",
                table: "attendance_records",
                column: "attendance_date");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_staff_id_attendance_date",
                table: "attendance_records",
                columns: new[] { "staff_id", "attendance_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_status",
                table: "attendance_records",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_records");
        }
    }
}
