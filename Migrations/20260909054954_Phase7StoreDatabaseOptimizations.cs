using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class Phase7StoreDatabaseOptimizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sales_sales_day_id",
                table: "sales");

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS ix_sales_sales_day_status
                ON sales (sales_day_id, status);
                """);

            migrationBuilder.CreateIndex(
                name: "ix_orders_orderdate",
                table: "orders",
                column: "orderdate",
                descending: new[] { true });

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS ix_orders_status_orderdate
                ON orders (status, orderdate DESC);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_orders_status_orderdate;");

            migrationBuilder.DropIndex(
                name: "ix_orders_orderdate",
                table: "orders");

            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_sales_sales_day_status;");

            migrationBuilder.CreateIndex(
                name: "ix_sales_sales_day_id",
                table: "sales",
                column: "sales_day_id");
        }
    }
}
