using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class AddStallsArtisansSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artisans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    artisan_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    date_of_birth = table.Column<DateTime>(type: "date", nullable: false),
                    gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    mobile = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    aadhaar_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    aadhaar_last4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    district = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    years_of_experience = table.Column<int>(type: "int", nullable: false),
                    skills = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    joined_date = table.Column<DateTime>(type: "date", nullable: false),
                    remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artisans", x => x.id);
                    table.ForeignKey(
                        name: "FK_artisans_product_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "product_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    login_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_persistent = table.Column<bool>(type: "bit", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    revoked_reason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ip_address = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_sessions_logins_login_id",
                        column: x => x.login_id,
                        principalTable: "logins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stalls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    stall_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    block = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    row_label = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    artisan_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    allotted_date = table.Column<DateTime>(type: "date", nullable: true),
                    agreement_end_date = table.Column<DateTime>(type: "date", nullable: true),
                    monthly_rent = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stalls", x => x.id);
                    table.ForeignKey(
                        name: "FK_stalls_artisans_artisan_id",
                        column: x => x.artisan_id,
                        principalTable: "artisans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stalls_product_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "product_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stall_reallocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    request_no = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    stall_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    stall_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    current_artisan_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    current_artisan_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    new_artisan_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    new_artisan_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    reason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    requested_date = table.Column<DateTime>(type: "date", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    decided_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    decided_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    decision_remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stall_reallocations", x => x.id);
                    table.ForeignKey(
                        name: "FK_stall_reallocations_artisans_current_artisan_id",
                        column: x => x.current_artisan_id,
                        principalTable: "artisans",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_stall_reallocations_artisans_new_artisan_id",
                        column: x => x.new_artisan_id,
                        principalTable: "artisans",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_stall_reallocations_stalls_stall_id",
                        column: x => x.stall_id,
                        principalTable: "stalls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artisans_aadhaar_hash",
                table: "artisans",
                column: "aadhaar_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artisans_artisan_code",
                table: "artisans",
                column: "artisan_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artisans_category_id",
                table: "artisans",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_artisans_mobile",
                table: "artisans",
                column: "mobile",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artisans_status",
                table: "artisans",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_stall_reallocations_current_artisan_id",
                table: "stall_reallocations",
                column: "current_artisan_id");

            migrationBuilder.CreateIndex(
                name: "IX_stall_reallocations_new_artisan_id",
                table: "stall_reallocations",
                column: "new_artisan_id");

            migrationBuilder.CreateIndex(
                name: "IX_stall_reallocations_request_no",
                table: "stall_reallocations",
                column: "request_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stall_reallocations_stall_id_status",
                table: "stall_reallocations",
                columns: new[] { "stall_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_stalls_artisan_id",
                table: "stalls",
                column: "artisan_id",
                unique: true,
                filter: "[artisan_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_stalls_category_id",
                table: "stalls",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_stalls_stall_code",
                table: "stalls",
                column: "stall_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stalls_status",
                table: "stalls",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_expires_at",
                table: "user_sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_login_id",
                table: "user_sessions",
                column: "login_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stall_reallocations");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "stalls");

            migrationBuilder.DropTable(
                name: "artisans");
        }
    }
}
