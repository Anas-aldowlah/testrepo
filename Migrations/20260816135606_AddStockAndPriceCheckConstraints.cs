using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAndPriceCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_days_opening_balance_nonnegative",
                table: "sales_days",
                sql: "opening_balance >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_days_totals_nonnegative",
                table: "sales_days",
                sql: "total_sales >= 0 AND total_cash >= 0 AND total_transfer >= 0 AND total_wallet >= 0 AND total_returns >= 0 AND net_total >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_amounts_nonnegative",
                table: "sales",
                sql: "total_amount >= 0 AND discount_total >= 0 AND final_amount >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sale_items_amounts_nonnegative",
                table: "sale_items",
                sql: "unit_price >= 0 AND discount >= 0 AND total >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_price_nonnegative",
                table: "products",
                sql: "price >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_stockquantity_nonnegative",
                table: "products",
                sql: "stockquantity >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_totalamount_nonnegative",
                table: "orders",
                sql: "totalamount >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orderitems_quantity_positive",
                table: "orderitems",
                sql: "quantity > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orderitems_unitprice_nonnegative",
                table: "orderitems",
                sql: "unitprice >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cartitems_quantity_nonnegative",
                table: "cartitems",
                sql: "quantity >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_days_opening_balance_nonnegative",
                table: "sales_days");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_days_totals_nonnegative",
                table: "sales_days");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_amounts_nonnegative",
                table: "sales");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sale_items_amounts_nonnegative",
                table: "sale_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_price_nonnegative",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_stockquantity_nonnegative",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_totalamount_nonnegative",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orderitems_quantity_positive",
                table: "orderitems");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orderitems_unitprice_nonnegative",
                table: "orderitems");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cartitems_quantity_nonnegative",
                table: "cartitems");
        }
    }
}
