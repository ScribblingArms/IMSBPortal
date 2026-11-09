using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class AddOtAssignmentOtHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OtHours",
                table: "ot_assignments",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            // existing assignments: OT Hrs = To − From, to 2 decimal places (as OtAssignment.HoursBetween)
            migrationBuilder.Sql(
                "UPDATE ot_assignments SET OtHours = ROUND(DATEDIFF(minute, FromDateTime, ToDateTime) / 60.0, 2) WHERE ToDateTime > FromDateTime;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtHours",
                table: "ot_assignments");
        }
    }
}
