using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class AddPayBillEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pay_bill_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    staff_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    year = table.Column<int>(type: "int", nullable: false),
                    month = table.Column<int>(type: "int", nullable: false),
                    employee_id = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    employee_name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    designation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    month_days = table.Column<int>(type: "int", nullable: false),
                    paid_days = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    monthly_salary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    basic_pay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    allowances = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    overtime_hours = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    overtime_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    deductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    net_pay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    generated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pay_bill_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pay_bill_entries_staff_id_year_month",
                table: "pay_bill_entries",
                columns: new[] { "staff_id", "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pay_bill_entries_year_month",
                table: "pay_bill_entries",
                columns: new[] { "year", "month" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pay_bill_entries");
        }
    }
}
