using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class DH03CustomerConflictOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_dh03_paid",
                table: "orders");

            migrationBuilder.AddColumn<DateTime>(
                name: "paymentreviewedat",
                table: "orders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "paymentreviewedbyuserid",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE orders
                SET paymentreviewedat = paymentverifiedat,
                    paymentreviewedbyuserid = paymentverifiedbyuserid
                WHERE paymentverifiedat IS NOT NULL
                  AND paymentverifiedbyuserid IS NOT NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_dh03_paid",
                table: "orders",
                sql: "status <> 'Paid' OR (stockdeducted AND paymentstatus = 'Paid' AND paymentverifiedat IS NOT NULL AND paymentverifiedbyuserid IS NOT NULL AND paymentreviewedat IS NOT NULL AND paymentreviewedbyuserid = paymentverifiedbyuserid AND finalfulfilledamount IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_payment_review_pair",
                table: "orders",
                sql: "(paymentreviewedat IS NULL) = (paymentreviewedbyuserid IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_dh03_paid",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_payment_review_pair",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "paymentreviewedat",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "paymentreviewedbyuserid",
                table: "orders");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_dh03_paid",
                table: "orders",
                sql: "status <> 'Paid' OR (stockdeducted AND paymentstatus = 'Paid' AND paymentverifiedat IS NOT NULL AND paymentverifiedbyuserid IS NOT NULL AND finalfulfilledamount IS NOT NULL)");
        }
    }
}
