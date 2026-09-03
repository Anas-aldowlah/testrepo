using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class Phase1StoreDatabaseIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_products_categoryid\";");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "ix_products_brand_trgm",
                table: "products",
                column: "brand")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_products_categoryid_createdat",
                table: "products",
                columns: new[] { "categoryid", "createdat" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_products_createdat",
                table: "products",
                column: "createdat",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_products_description_trgm",
                table: "products",
                column: "description")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_products_name_trgm",
                table: "products",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_product_retail_prices_active_size",
                table: "product_retail_prices",
                column: "size_ml",
                filter: "is_active AND size_ml > 0 AND price > 0");

            migrationBuilder.Sql(
                """
                CREATE INDEX ix_sales_completed_effective_date
                ON sales (status, (COALESCE(completed_at, created_at)));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX ix_sales_completed_effective_date;");

            migrationBuilder.DropIndex(
                name: "ix_products_brand_trgm",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_categoryid_createdat",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_createdat",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_description_trgm",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_name_trgm",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_product_retail_prices_active_size",
                table: "product_retail_prices");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_products_categoryid",
                table: "products",
                column: "categoryid");
        }
    }
}
