using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAadhaar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AadhaarHash",
                table: "staff",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AadhaarLast4",
                table: "staff",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_AadhaarHash",
                table: "staff",
                column: "AadhaarHash",
                unique: true,
                filter: "[AadhaarHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_staff_AadhaarHash",
                table: "staff");

            migrationBuilder.DropColumn(
                name: "AadhaarHash",
                table: "staff");

            migrationBuilder.DropColumn(
                name: "AadhaarLast4",
                table: "staff");
        }
    }
}
