using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class ChangeOtAssignmentsToDateTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FromDateTime",
                table: "ot_assignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ToDateTime",
                table: "ot_assignments",
                type: "datetime2",
                nullable: true);

            // saved windows had no date: they take the day they were created. Rows moved from the staff table
            // were never checked, so a To at or before From is taken to end the next day.
            migrationBuilder.Sql(@"
UPDATE ot_assignments
SET FromDateTime = DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', FromTime), CAST(CAST(CreatedAt AS date) AS datetime2)),
    ToDateTime = DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ToTime),
        DATEADD(DAY, CASE WHEN ToTime <= FromTime THEN 1 ELSE 0 END, CAST(CAST(CreatedAt AS date) AS datetime2)));");

            migrationBuilder.AlterColumn<DateTime>(
                name: "FromDateTime",
                table: "ot_assignments",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ToDateTime",
                table: "ot_assignments",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "FromTime",
                table: "ot_assignments");

            migrationBuilder.DropColumn(
                name: "ToTime",
                table: "ot_assignments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "FromTime",
                table: "ot_assignments",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ToTime",
                table: "ot_assignments",
                type: "time",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE ot_assignments
SET FromTime = CAST(FromDateTime AS time), ToTime = CAST(ToDateTime AS time);");

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "FromTime",
                table: "ot_assignments",
                type: "time",
                nullable: false,
                oldClrType: typeof(TimeSpan),
                oldType: "time",
                oldNullable: true);

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "ToTime",
                table: "ot_assignments",
                type: "time",
                nullable: false,
                oldClrType: typeof(TimeSpan),
                oldType: "time",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "FromDateTime",
                table: "ot_assignments");

            migrationBuilder.DropColumn(
                name: "ToDateTime",
                table: "ot_assignments");
        }
    }
}
