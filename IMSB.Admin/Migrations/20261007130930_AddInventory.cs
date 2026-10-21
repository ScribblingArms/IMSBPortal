using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IMSB.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    product_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    stall_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    min_stock = table.Column<int>(type: "int", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_products_product_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "product_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    product_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    product_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    product_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    movement_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    quantity_before = table.Column<int>(type: "int", nullable: false),
                    quantity_after = table.Column<int>(type: "int", nullable: false),
                    movement_date = table.Column<DateTime>(type: "date", nullable: false),
                    remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_movements_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "product_categories",
                columns: new[] { "id", "name", "sort_order" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-000000000001"), "Textiles", 1 },
                    { new Guid("a1000000-0000-0000-0000-000000000002"), "Handicrafts", 2 },
                    { new Guid("a1000000-0000-0000-0000-000000000003"), "Jewellery", 3 },
                    { new Guid("a1000000-0000-0000-0000-000000000004"), "Pottery", 4 },
                    { new Guid("a1000000-0000-0000-0000-000000000005"), "Paintings", 5 },
                    { new Guid("a1000000-0000-0000-0000-000000000006"), "Herbal", 6 },
                    { new Guid("a1000000-0000-0000-0000-000000000007"), "Food & Snacks", 7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_categories_name",
                table: "product_categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_product_code",
                table: "products",
                column: "product_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_stall_code",
                table: "products",
                column: "stall_code");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_created_at",
                table: "stock_movements",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_product_id",
                table: "stock_movements",
                column: "product_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "product_categories");
        }
    }
}
