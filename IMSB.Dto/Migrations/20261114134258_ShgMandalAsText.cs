using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>
    /// The stall application's Mandal becomes a text box: SHGDetails.MandalId (FK to Mandals) is replaced by
    /// SHGDetails.Mandal (text). Existing rows keep their mandal's name. The Mandals table itself is kept.
    /// </summary>
    public partial class ShgMandalAsText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mandal",
                table: "SHGDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // copy the name before the id goes
            migrationBuilder.Sql(
                "UPDATE s SET s.[Mandal] = m.[Name] FROM [SHGDetails] s JOIN [Mandals] m ON m.[Id] = s.[MandalId];");

            migrationBuilder.DropForeignKey(
                name: "FK_SHGDetails_Mandals_MandalId",
                table: "SHGDetails");

            migrationBuilder.DropIndex(
                name: "IX_SHGDetails_MandalId",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "MandalId",
                table: "SHGDetails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MandalId",
                table: "SHGDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // typed names that match a mandal of the row's district get its id back; anything else stops the rollback
            migrationBuilder.Sql(
                "UPDATE s SET s.[MandalId] = m.[Id] FROM [SHGDetails] s " +
                "JOIN [Mandals] m ON m.[DistrictId] = s.[DistrictId] AND m.[Name] = LTRIM(RTRIM(s.[Mandal]));");
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [SHGDetails] WHERE [MandalId] = 0) " +
                "THROW 50002, 'Some SHGDetails rows have a typed mandal that is not in the Mandals list of their district. Correct them, then roll back again.', 1;");

            migrationBuilder.DropColumn(
                name: "Mandal",
                table: "SHGDetails");

            migrationBuilder.CreateIndex(
                name: "IX_SHGDetails_MandalId",
                table: "SHGDetails",
                column: "MandalId");

            migrationBuilder.AddForeignKey(
                name: "FK_SHGDetails_Mandals_MandalId",
                table: "SHGDetails",
                column: "MandalId",
                principalTable: "Mandals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
