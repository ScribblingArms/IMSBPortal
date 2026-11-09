using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class AllowPastOtAssignmentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ot_assignments_StaffId",
                table: "ot_assignments");

            migrationBuilder.CreateIndex(
                name: "IX_ot_assignments_StaffId",
                table: "ot_assignments",
                column: "StaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ot_assignments_StaffId",
                table: "ot_assignments");

            migrationBuilder.CreateIndex(
                name: "IX_ot_assignments_StaffId",
                table: "ot_assignments",
                column: "StaffId",
                unique: true);
        }
    }
}
