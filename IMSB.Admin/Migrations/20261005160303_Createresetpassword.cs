using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Admin.Migrations
{
    /// <inheritdoc />
    public partial class Createresetpassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    login_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    token = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_logins_login_id",
                        column: x => x.login_id,
                        principalTable: "logins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_login_id",
                table: "password_reset_tokens",
                column: "login_id");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_token",
                table: "password_reset_tokens",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "password_reset_tokens");
        }
    }
}
