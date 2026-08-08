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
            migrationBuilder.Sql(
                """
                WITH duplicate_groups AS (
                    SELECT
                        cartid,
                        productid,
                        MIN(id) AS retained_id,
                        LEAST(SUM(quantity)::bigint, 2147483647)::integer AS total_quantity
                    FROM cartitems
                    GROUP BY cartid, productid
                    HAVING COUNT(*) > 1
                ), updated_rows AS (
                    UPDATE cartitems AS retained
                    SET quantity = duplicate_groups.total_quantity
                    FROM duplicate_groups
                    WHERE retained.id = duplicate_groups.retained_id
                    RETURNING retained.id
                )
                DELETE FROM cartitems AS duplicate
                USING duplicate_groups
                WHERE duplicate.cartid = duplicate_groups.cartid
                  AND duplicate.productid = duplicate_groups.productid
                  AND duplicate.id <> duplicate_groups.retained_id;
                """);

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
