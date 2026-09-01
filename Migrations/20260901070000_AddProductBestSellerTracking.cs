using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBestSellerTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "total_sold",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "sales_last_updated_at",
                table: "products",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_total_sold",
                table: "products",
                column: "total_sold");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_products_total_sold",
                table: "products");

            migrationBuilder.DropColumn(
                name: "sales_last_updated_at",
                table: "products");

            migrationBuilder.DropColumn(
                name: "total_sold",
                table: "products");
        }
    }
}
