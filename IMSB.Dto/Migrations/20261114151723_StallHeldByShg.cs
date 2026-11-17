using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class StallHeldByShg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArtisan",
                table: "stalls",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ShgDetailId",
                table: "stalls",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_stalls_ShgDetailId",
                table: "stalls",
                column: "ShgDetailId",
                unique: true,
                filter: "[ShgDetailId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_stalls_SHGDetails_ShgDetailId",
                table: "stalls",
                column: "ShgDetailId",
                principalTable: "SHGDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stalls_SHGDetails_ShgDetailId",
                table: "stalls");

            migrationBuilder.DropIndex(
                name: "IX_stalls_ShgDetailId",
                table: "stalls");

            migrationBuilder.DropColumn(
                name: "IsArtisan",
                table: "stalls");

            migrationBuilder.DropColumn(
                name: "ShgDetailId",
                table: "stalls");
        }
    }
}
