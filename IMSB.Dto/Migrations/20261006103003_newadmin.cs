using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    // class renamed from "newadmin" (CS8981); the migration id in the [Migration] attribute is unchanged
    public partial class NewAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.logins', N'loginType') IS NOT NULL
                    ALTER TABLE [dbo].[logins] DROP COLUMN [loginType];
                """);

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "logins",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Admin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "role",
                table: "logins");

            migrationBuilder.AddColumn<int>(
                name: "loginType",
                table: "logins",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
