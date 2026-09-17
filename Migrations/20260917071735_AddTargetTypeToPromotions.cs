using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetTypeToPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "target_type",
                table: "promotions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "All");

            migrationBuilder.Sql(@"
                UPDATE promotions
                SET target_type = 'Products'
                WHERE id IN (SELECT DISTINCT promotion_id FROM promotion_products);

                UPDATE promotions
                SET target_type = 'Categories'
                WHERE id IN (SELECT DISTINCT promotion_id FROM promotion_categories);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "target_type",
                table: "promotions");
        }
    }
}
