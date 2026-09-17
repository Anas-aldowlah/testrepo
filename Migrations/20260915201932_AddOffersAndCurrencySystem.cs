using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class AddOffersAndCurrencySystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                table: "storesettings",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "YER");

            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                table: "sales",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "YER");

            migrationBuilder.AddColumn<decimal>(
                name: "manual_discount_total",
                table: "sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "promotion_discount_total",
                table: "sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "promotion_snapshot_json",
                table: "sales",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "applied_promotion_id",
                table: "sale_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "applied_promotion_title",
                table: "sale_items",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "final_unit_price",
                table: "sale_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "manual_discount_amount",
                table: "sale_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "original_unit_price",
                table: "sale_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "promotion_discount_amount",
                table: "sale_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                table: "orders",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "YER");

            migrationBuilder.AddColumn<decimal>(
                name: "discount_total",
                table: "orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "promotion_snapshot_json",
                table: "orders",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "spend_amount_discount",
                table: "orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "applied_promotion_id",
                table: "orderitems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "applied_promotion_title",
                table: "orderitems",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "applied_promotions_json",
                table: "orderitems",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_amount",
                table: "orderitems",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "final_unit_price",
                table: "orderitems",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "free_quantity",
                table: "orderitems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "original_unit_price",
                table: "orderitems",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "promotions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    promotion_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    discount_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    spend_discount_type = table.Column<int>(type: "integer", nullable: false),
                    minimum_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    buy_quantity = table.Column<int>(type: "integer", nullable: true),
                    free_quantity = table.Column<int>(type: "integer", nullable: true),
                    offer_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    can_be_combined = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    banner_image = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("promotions_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "promotion_categories",
                columns: table => new
                {
                    promotion_id = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotion_categories", x => new { x.promotion_id, x.category_id });
                    table.ForeignKey(
                        name: "fk_promotion_categories_categories",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_promotion_categories_promotions",
                        column: x => x.promotion_id,
                        principalTable: "promotions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "promotion_products",
                columns: table => new
                {
                    promotion_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotion_products", x => new { x.promotion_id, x.product_id });
                    table.ForeignKey(
                        name: "fk_promotion_products_products",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_promotion_products_promotions",
                        column: x => x.promotion_id,
                        principalTable: "promotions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_promotion_categories_category",
                table: "promotion_categories",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "idx_promotion_products_product",
                table: "promotion_products",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_promotions_active_dates",
                table: "promotions",
                columns: new[] { "is_active", "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "idx_promotions_priority",
                table: "promotions",
                columns: new[] { "priority", "created_at" });

            // Safe Backfill: Historical orders, sales, and store settings originated in SAR (Saudi Riyal)
            migrationBuilder.Sql("UPDATE storesettings SET currency_code = 'SAR' WHERE currency_code IS NULL OR currency_code = '' OR currency_code = 'YER';");
            migrationBuilder.Sql("UPDATE orders SET currency_code = 'SAR' WHERE currency_code IS NULL OR currency_code = '' OR currency_code = 'YER';");
            migrationBuilder.Sql("UPDATE sales SET currency_code = 'SAR' WHERE currency_code IS NULL OR currency_code = '' OR currency_code = 'YER';");
            migrationBuilder.Sql("UPDATE orderitems SET original_unit_price = unitprice WHERE original_unit_price = 0;");
            migrationBuilder.Sql("UPDATE orderitems SET final_unit_price = unitprice WHERE final_unit_price = 0;");
            migrationBuilder.Sql("UPDATE sale_items SET original_unit_price = unit_price WHERE original_unit_price = 0;");
            migrationBuilder.Sql("UPDATE sale_items SET final_unit_price = unit_price WHERE final_unit_price = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promotion_categories");

            migrationBuilder.DropTable(
                name: "promotion_products");

            migrationBuilder.DropTable(
                name: "promotions");

            migrationBuilder.DropColumn(
                name: "currency_code",
                table: "storesettings");

            migrationBuilder.DropColumn(
                name: "currency_code",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "manual_discount_total",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "promotion_discount_total",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "promotion_snapshot_json",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "applied_promotion_id",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "applied_promotion_title",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "final_unit_price",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "manual_discount_amount",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "original_unit_price",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "promotion_discount_amount",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "currency_code",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "discount_total",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "promotion_snapshot_json",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "spend_amount_discount",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "applied_promotion_id",
                table: "orderitems");

            migrationBuilder.DropColumn(
                name: "applied_promotion_title",
                table: "orderitems");

            migrationBuilder.DropColumn(
                name: "applied_promotions_json",
                table: "orderitems");

            migrationBuilder.DropColumn(
                name: "discount_amount",
                table: "orderitems");

            migrationBuilder.DropColumn(
                name: "final_unit_price",
                table: "orderitems");

            migrationBuilder.DropColumn(
                name: "free_quantity",
                table: "orderitems");

            migrationBuilder.DropColumn(
                name: "original_unit_price",
                table: "orderitems");
        }
    }
}
