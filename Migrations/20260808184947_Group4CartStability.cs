using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class Group4CartStability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove legacy duplicates before PostgreSQL validates the unique index.
            // The schema uses lowercase identifiers; retain the newest row (highest id).
            migrationBuilder.Sql(
                "DELETE FROM cartitems a USING cartitems b WHERE a.id < b.id AND a.cartid = b.cartid AND a.productid = b.productid;");

            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_cartitems_cartid\";");

            migrationBuilder.CreateIndex(
                name: "ux_cartitems_cartid_productid",
                table: "cartitems",
                columns: new[] { "cartid", "productid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_cartitems_cartid_productid",
                table: "cartitems");

            migrationBuilder.CreateIndex(
                name: "IX_cartitems_cartid",
                table: "cartitems",
                column: "cartid");
        }
    }
}
