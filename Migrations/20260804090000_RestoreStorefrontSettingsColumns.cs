using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using YAGOT_2._0.Models;

#nullable disable

namespace YAGOT_2._0.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NeondbContext))]
    [Migration("20260804090000_RestoreStorefrontSettingsColumns")]
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
