using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    public partial class RestoreStorefrontSettingsColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE storesettings ADD COLUMN IF NOT EXISTS featuredcategoryid integer;
                ALTER TABLE storesettings ADD COLUMN IF NOT EXISTS heromarketingtext text DEFAULT '';
                ALTER TABLE storesettings ADD COLUMN IF NOT EXISTS heromarketingdesc text DEFAULT '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE storesettings DROP COLUMN IF EXISTS heromarketingdesc;
                ALTER TABLE storesettings DROP COLUMN IF EXISTS heromarketingtext;
                ALTER TABLE storesettings DROP COLUMN IF EXISTS featuredcategoryid;
                """);
        }
    }
}
