using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class OrderStockDeductionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "stockdeducted",
                table: "orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Before this flag existed, the admin status workflow deducted stock for
            // fulfilling orders. Preserve that state so those orders are not deducted twice.
            migrationBuilder.Sql(
                """
                UPDATE orders
                SET stockdeducted = TRUE
                WHERE LOWER(status) IN ('processed', 'processing', 'shipped', 'delivered', 'completed')
                   OR status IN ('قيد المعالجة', 'تم الشحن', 'تم التوصيل');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "stockdeducted",
                table: "orders");
        }
    }
}
