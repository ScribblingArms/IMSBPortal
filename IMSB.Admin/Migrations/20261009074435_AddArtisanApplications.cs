using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddArtisanApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artisan_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    application_no = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                    remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    submitted_date = table.Column<DateTime>(type: "date", nullable: false),
                    reviewer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    review_started_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    decided_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    decided_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    decision_remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    artisan_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    artisan_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    stall_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artisan_applications", x => x.id);
                    table.ForeignKey(
                        name: "FK_artisan_applications_artisans_artisan_id",
                        column: x => x.artisan_id,
                        principalTable: "artisans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_artisan_applications_product_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "product_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artisan_applications_aadhaar_hash",
                table: "artisan_applications",
                column: "aadhaar_hash");

            migrationBuilder.CreateIndex(
                name: "IX_artisan_applications_application_no",
                table: "artisan_applications",
                column: "application_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artisan_applications_artisan_id",
                table: "artisan_applications",
                column: "artisan_id");

            migrationBuilder.CreateIndex(
                name: "IX_artisan_applications_category_id",
                table: "artisan_applications",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_artisan_applications_mobile",
                table: "artisan_applications",
                column: "mobile");

            migrationBuilder.CreateIndex(
                name: "IX_artisan_applications_status",
                table: "artisan_applications",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_artisan_applications_submitted_date",
                table: "artisan_applications",
                column: "submitted_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artisan_applications");
        }
    }
}
