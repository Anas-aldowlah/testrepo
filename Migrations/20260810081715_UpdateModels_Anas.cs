using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModels_Anas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropForeignKey(
            //    name: "fk_user_logs",
            //    table: "sale_payments");

            //migrationBuilder.DropColumn(
            //    name: "Userid",
            //    table: "sale_payments");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_number",
                table: "sales",
                column: "invoice_number",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalePayment_Amount_Positive",
                table: "sale_payments",
                sql: "amount > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleItem_Quantity_Positive",
                table: "sale_items",
                sql: "quantity > 0");

            migrationBuilder.AddForeignKey(
                name: "fk_user_logs",
                table: "securitylogs",
                column: "userid",
                principalTable: "UserSite",
                principalColumn: "UserID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_logs",
                table: "securitylogs");

            migrationBuilder.DropIndex(
                name: "ix_sales_invoice_number",
                table: "sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalePayment_Amount_Positive",
                table: "sale_payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleItem_Quantity_Positive",
                table: "sale_items");

            migrationBuilder.AddColumn<int>(
                name: "Userid",
                table: "sale_payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_user_logs",
                table: "sale_payments",
                column: "Userid",
                principalTable: "UserSite",
                principalColumn: "UserID",
                onDelete: ReferentialAction.SetNull);
        }
    }
}