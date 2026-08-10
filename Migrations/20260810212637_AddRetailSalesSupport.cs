using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class AddRetailSalesSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_cartitems_cartid_productid",
                table: "cartitems");

            migrationBuilder.AddColumn<int>(
                name: "retail_price_id",
                table: "sale_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retail_size_ml",
                table: "sale_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_retail_enabled",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "stock_unit",
                table: "products",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Piece");

            migrationBuilder.AddColumn<int>(
                name: "volume_ml",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retail_price_id",
                table: "orderitems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retail_size_ml",
                table: "orderitems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retail_price_id",
                table: "cartitems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_retail_prices",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    size_ml = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("product_retail_prices_pkey", x => x.id);
                    table.CheckConstraint("ck_product_retail_prices_price_positive", "price > 0");
                    table.CheckConstraint("ck_product_retail_prices_size_positive", "size_ml > 0");
                    table.ForeignKey(
                        name: "fk_product_retail_prices_product",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_retail_price_id",
                table: "sale_items",
                column: "retail_price_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_retail_requires_ml",
                table: "products",
                sql: "(is_retail_enabled = false) OR (stock_unit = 'Ml' AND volume_ml IS NOT NULL AND volume_ml > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_stock_unit",
                table: "products",
                sql: "stock_unit IN ('Piece', 'Ml')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_volume_for_ml",
                table: "products",
                sql: "(stock_unit = 'Piece' AND volume_ml IS NULL) OR (stock_unit = 'Ml' AND volume_ml IS NOT NULL AND volume_ml > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_orderitems_retail_price_id",
                table: "orderitems",
                column: "retail_price_id");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX ux_cartitems_cart_product_retail
                ON cartitems (cartid, productid, COALESCE(retail_price_id, 0));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_cartitems_retail_price_id",
                table: "cartitems",
                column: "retail_price_id");

            migrationBuilder.CreateIndex(
                name: "ux_product_retail_prices_product_size",
                table: "product_retail_prices",
                columns: new[] { "product_id", "size_ml" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_cartitems_retail_price",
                table: "cartitems",
                column: "retail_price_id",
                principalTable: "product_retail_prices",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_orderitems_retail_price",
                table: "orderitems",
                column: "retail_price_id",
                principalTable: "product_retail_prices",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_sale_items_retail_price",
                table: "sale_items",
                column: "retail_price_id",
                principalTable: "product_retail_prices",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cartitems_retail_price",
                table: "cartitems");

            migrationBuilder.DropForeignKey(
                name: "fk_orderitems_retail_price",
                table: "orderitems");

            migrationBuilder.DropForeignKey(
                name: "fk_sale_items_retail_price",
                table: "sale_items");

            migrationBuilder.DropTable(
                name: "product_retail_prices");

            migrationBuilder.DropIndex(
                name: "IX_sale_items_retail_price_id",
                table: "sale_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_retail_requires_ml",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_stock_unit",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_volume_for_ml",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_orderitems_retail_price_id",
                table: "orderitems");

            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_cartitems_cart_product_retail;");

            migrationBuilder.DropIndex(
                name: "IX_cartitems_retail_price_id",
                table: "cartitems");

            migrationBuilder.DropColumn(
                name: "retail_price_id",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "retail_size_ml",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "is_retail_enabled",
                table: "products");

            migrationBuilder.DropColumn(
                name: "stock_unit",
                table: "products");

            migrationBuilder.DropColumn(
                name: "volume_ml",
                table: "products");

            migrationBuilder.DropColumn(
                name: "retail_price_id",
                table: "orderitems");

            migrationBuilder.DropColumn(
                name: "retail_size_ml",
                table: "orderitems");

            migrationBuilder.DropColumn(
                name: "retail_price_id",
                table: "cartitems");

            migrationBuilder.CreateIndex(
                name: "ux_cartitems_cartid_productid",
                table: "cartitems",
                columns: new[] { "cartid", "productid" },
                unique: true);
        }
    }
}
