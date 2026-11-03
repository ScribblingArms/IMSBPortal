using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffOtTimesAndArtisanPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "ot_from_time",
                table: "staff",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ot_to_time",
                table: "staff",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "from_date",
                table: "artisans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "to_date",
                table: "artisans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "from_date",
                table: "artisan_applications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "to_date",
                table: "artisan_applications",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ot_from_time",
                table: "staff");

            migrationBuilder.DropColumn(
                name: "ot_to_time",
                table: "staff");

            migrationBuilder.DropColumn(
                name: "from_date",
                table: "artisans");

            migrationBuilder.DropColumn(
                name: "to_date",
                table: "artisans");

            migrationBuilder.DropColumn(
                name: "from_date",
                table: "artisan_applications");

            migrationBuilder.DropColumn(
                name: "to_date",
                table: "artisan_applications");
        }
    }
}
