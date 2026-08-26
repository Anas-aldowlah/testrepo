using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class DH03InventoryConflictWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "finalfulfilledamount",
                table: "orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "paymentverifiedat",
                table: "orders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "paymentverifiedbyuserid",
                table: "orders",
                type: "integer",
                nullable: true);

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

            migrationBuilder.AddColumn<string>(
                name: "workflowstate",
                table: "orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "fulfilled_quantity",
                table: "orderitems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "unavailable_quantity",
                table: "orderitems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_finalfulfilledamount_range",
                table: "orders",
                sql: "finalfulfilledamount IS NULL OR (finalfulfilledamount >= 0 AND finalfulfilledamount <= totalamount)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_payment_verification_pair",
                table: "orders",
                sql: "(paymentverifiedat IS NULL) = (paymentverifiedbyuserid IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_financial_resolution_balance",
                table: "orders",
                sql: "finalfulfilledamount IS NULL OR refundrequiredamount IS NULL OR finalfulfilledamount + refundrequiredamount = totalamount");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_refundrequiredamount_range",
                table: "orders",
                sql: "refundrequiredamount IS NULL OR (refundrequiredamount >= 0 AND refundrequiredamount <= totalamount)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_workflowstate",
                table: "orders",
                sql: "workflowstate IS NULL OR workflowstate IN ('ConflictAwaitingDecision', 'ConflictResolvedContinue', 'ConflictResolvedCancel')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orderitems_fulfilled_quantity_range",
                table: "orderitems",
                sql: "fulfilled_quantity IS NULL OR (fulfilled_quantity >= 0 AND fulfilled_quantity <= quantity)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orderitems_unavailable_quantity_range",
                table: "orderitems",
                sql: "unavailable_quantity IS NULL OR (unavailable_quantity >= 0 AND unavailable_quantity <= quantity)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_finalfulfilledamount_range",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_payment_verification_pair",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_financial_resolution_balance",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_refundrequiredamount_range",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_workflowstate",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orderitems_fulfilled_quantity_range",
                table: "orderitems");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orderitems_unavailable_quantity_range",
                table: "orderitems");

            migrationBuilder.DropColumn(
                name: "finalfulfilledamount",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "paymentverifiedat",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "paymentverifiedbyuserid",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "refundreason",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "refundrequiredamount",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "workflowstate",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "fulfilled_quantity",
                table: "orderitems");

            migrationBuilder.DropColumn(
                name: "unavailable_quantity",
                table: "orderitems");
        }
    }
}
