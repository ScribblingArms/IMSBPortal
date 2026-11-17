using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class AddShgDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SHGDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShgName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ShgId = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    VillageOrganisation = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MandalSamakhya = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    District = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Mandal = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Village = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    YearFormed = table.Column<int>(type: "int", nullable: true),
                    MemberCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SHGDetails", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SHGDetails_District",
                table: "SHGDetails",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_SHGDetails_PublicId",
                table: "SHGDetails",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SHGDetails_ShgId",
                table: "SHGDetails",
                column: "ShgId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SHGDetails");
        }
    }
}
