using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    entry_no = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    sold_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    total_quantity = table.Column<int>(type: "int", nullable: false),
                    remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sale_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sales_entry_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    product_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    product_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    product_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    category_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    stall_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    line_total = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_sale_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sale_items_sales_entries_sales_entry_id",
                        column: x => x.sales_entry_id,
                        principalTable: "sales_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_product_id",
                table: "sale_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_sales_entry_id",
                table: "sale_items",
                column: "sales_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_stall_code",
                table: "sale_items",
                column: "stall_code");

            migrationBuilder.CreateIndex(
                name: "IX_sales_entries_entry_no",
                table: "sales_entries",
                column: "entry_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_entries_sold_at",
                table: "sales_entries",
                column: "sold_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sale_items");

            migrationBuilder.DropTable(
                name: "sales_entries");
        }
    }
}
