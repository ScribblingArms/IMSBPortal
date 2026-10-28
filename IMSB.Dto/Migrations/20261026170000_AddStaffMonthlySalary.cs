using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    [DbContext(typeof(IMSBContext))]
    [Migration("20261026170000_AddStaffMonthlySalary")]
    public partial class AddStaffMonthlySalary : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "monthly_salary",
                table: "staff",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "monthly_salary",
                table: "staff");
        }
    }
}
