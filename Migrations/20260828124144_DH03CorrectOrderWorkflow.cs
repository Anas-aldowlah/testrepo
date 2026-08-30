using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class DH03CorrectOrderWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_financial_resolution_balance",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_refundrequiredamount_range",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "refundreason",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "refundrequiredamount",
                table: "orders");

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelledat",
                table: "orders",
                type: "timestamp without time zone",
                nullable: true);

            // Preserve legacy stock ownership without fabricating payment-verification
            // provenance. These rows already own stock, so they become legacy Processed
            // orders rather than being released or re-deducted by this migration.
            migrationBuilder.Sql(
                """
                UPDATE orders
                SET status = 'Processed',
                    finalfulfilledamount = COALESCE(finalfulfilledamount, totalamount)
                WHERE status = 'Pending' AND stockdeducted;

                UPDATE orderitems AS item
                SET fulfilled_quantity = COALESCE(item.fulfilled_quantity, item.quantity),
                    unavailable_quantity = COALESCE(item.unavailable_quantity, 0)
                FROM orders AS order_row
                WHERE order_row.id = item.orderid
                  AND order_row.status IN ('Processed', 'Shipped', 'Delivered')
                  AND order_row.stockdeducted;

                UPDATE orders
                SET paymentstatus = CASE WHEN receipturl IS NULL THEN 'Unpaid' ELSE 'Pending' END,
                    paymentverifiedat = NULL,
                    paymentverifiedbyuserid = NULL,
                    finalfulfilledamount = NULL
                WHERE status = 'Pending' AND NOT stockdeducted;

                UPDATE orderitems AS item
                SET fulfilled_quantity = NULL,
                    unavailable_quantity = NULL
                FROM orders AS order_row
                WHERE order_row.id = item.orderid
                  AND order_row.status = 'Pending'
                  AND order_row.workflowstate IS NULL;

                UPDATE orders
                SET cancelledat = CURRENT_TIMESTAMP
                WHERE status = 'Cancelled' AND cancelledat IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_orders_cancelledat",
                table: "orders",
                column: "cancelledat");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_dh03_status",
                table: "orders",
                sql: "status IN ('Pending', 'Paid', 'Processed', 'Shipped', 'Delivered', 'Cancelled', 'Refunded')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_dh03_stock_owner",
                table: "orders",
                sql: "NOT stockdeducted OR status IN ('Paid', 'Processed', 'Shipped', 'Delivered')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_dh03_paid",
                table: "orders",
                sql: "status <> 'Paid' OR (stockdeducted AND paymentstatus = 'Paid' AND paymentverifiedat IS NOT NULL AND paymentverifiedbyuserid IS NOT NULL AND finalfulfilledamount IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_dh03_conflict",
                table: "orders",
                sql: "workflowstate <> 'ConflictAwaitingDecision' OR (status = 'Pending' AND NOT stockdeducted AND paymentstatus <> 'Paid' AND paymentverifiedat IS NULL AND paymentverifiedbyuserid IS NULL AND finalfulfilledamount IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_dh03_cancelledat",
                table: "orders",
                sql: "(status = 'Cancelled') = (cancelledat IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_dh03_cancelledat",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_dh03_conflict",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_dh03_paid",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_dh03_status",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_dh03_stock_owner",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_cancelledat",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancelledat",
                table: "orders");

            migrationBuilder.AddColumn<string>(
                name: "refundreason",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "refundrequiredamount",
                table: "orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_financial_resolution_balance",
                table: "orders",
                sql: "finalfulfilledamount IS NULL OR refundrequiredamount IS NULL OR finalfulfilledamount + refundrequiredamount = totalamount");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_refundrequiredamount_range",
                table: "orders",
                sql: "refundrequiredamount IS NULL OR (refundrequiredamount >= 0 AND refundrequiredamount <= totalamount)");
        }
    }
}
